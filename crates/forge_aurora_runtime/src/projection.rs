//! Forge projection of a validated Aurora module bundle.
//!
//! The projection is engine-neutral: zones become canonical `TerrainDocument` +
//! `GameplayDocument` archives, tile WOKs become a walkable surface, and every asset is
//! copied into a closed, content-addressed local package. NWN-derived bytes stay in a
//! `PrivateLocalOnly` package.

use crate::layout::{AreaAsset, AreaDocument, AreaInstance};
use crate::walkmesh::{WalkSurface, Walkmesh};
use forge_aurora_bridge::{ImportedArea, ImportedModule};
use forge_core::{
    AreaId, AssetId, ContentHash, EntityId, ModuleId, ProjectId, Revision, Vec3, bytes_sha256,
    canonical_json_sha256,
};
use forge_gameplay::{
    ActorDefinition, ActorRole, GAMEPLAY_SCHEMA, GAMEPLAY_VERSION, GameplayDocument,
    ObjectDefinition, ObjectKind, ObjectPhysics, PlaceDefinition, RulesProfile,
};
use forge_project::module_package::{
    MODULE_PACKAGE_SCHEMA, MODULE_PACKAGE_VERSION, ModuleAsset, ModuleCompatibility, ModuleFile,
    ModuleFileRole, ModulePackageManifest, ModuleProvenance, ModuleRights, ModuleZone,
};
use forge_project::zone_authoring::session::ZoneEditSession;
use forge_project::{
    HeightSamplingAlgorithm, HeightSamplingParameters, TerrainDocument, TerrainExtent,
    TerrainMaterialLayer,
};
use serde::{Deserialize, Serialize};
use std::collections::BTreeMap;
use std::fs;
use std::path::{Path, PathBuf};

pub const MAX_ASSET_BYTES: usize = 128 * 1024 * 1024;
pub const AURORA_PROJECTION_SCHEMA: &str = "forge.aurora-projection@1";

#[derive(Debug, thiserror::Error)]
pub enum ProjectionError {
    #[error("bundle area {0} is missing from the imported module")]
    MissingArea(String),
    #[error("invalid area document {area}: {detail}")]
    AreaDocument { area: String, detail: String },
    #[error("asset refused {path}: {detail}")]
    Asset { path: String, detail: String },
    #[error("walkmesh refused {path}: {detail}")]
    Walkmesh { path: String, detail: String },
    #[error("zone archive failed for {area}: {detail}")]
    ZoneArchive { area: String, detail: String },
    #[error("package manifest is invalid: {0}")]
    Manifest(String),
    #[error("I/O error at {path}: {detail}")]
    Io { path: String, detail: String },
}

impl ProjectionError {
    fn io(path: &Path, error: std::io::Error) -> Self {
        Self::Io {
            path: path.display().to_string(),
            detail: error.to_string(),
        }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct NavigationStats {
    pub wok_files: usize,
    pub dwk_files: usize,
    pub pwk_files: usize,
    pub triangles: usize,
    pub unsupported: usize,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct ProjectedDoor {
    pub id: EntityId,
    pub tag: Option<String>,
    pub position: Vec3,
    pub destination: Option<String>,
    pub initially_open: bool,
    pub transition_flags: Option<u32>,
    pub model_assets: Vec<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct ProjectedInstance {
    pub id: EntityId,
    pub category: String,
    pub tag: Option<String>,
    pub template_resref: Option<String>,
    pub position: Vec3,
    pub source_status: String,
    pub model_assets: Vec<String>,
}

#[derive(Debug, Clone)]
pub struct ProjectedZone {
    pub resref: String,
    pub name: String,
    pub area_id: AreaId,
    pub terrain: TerrainDocument,
    pub gameplay: GameplayDocument,
    pub walkmesh: WalkSurface,
    pub doors: Vec<ProjectedDoor>,
    pub instances: Vec<ProjectedInstance>,
    pub spawn: Vec3,
    pub spawn_yaw: f32,
    pub archive_path: String,
    pub archive_bytes: Vec<u8>,
    pub archive_sha256: ContentHash,
    pub head_hash: ContentHash,
    pub gameplay_sha256: ContentHash,
    pub tile_count: usize,
    pub navigation: NavigationStats,
    pub diagnostics: Vec<String>,
    pub area_root: PathBuf,
}

#[derive(Debug, Clone)]
pub struct ProjectedAsset {
    pub source_path: String,
    pub package_path: String,
    pub sha256: ContentHash,
    pub bytes: Vec<u8>,
    pub kind: String,
}

#[derive(Debug, Clone)]
pub struct ProjectedModule {
    pub root: PathBuf,
    pub module_id: ModuleId,
    pub project_id: ProjectId,
    pub name: String,
    pub entry_area: String,
    pub source_module_file_name: String,
    pub source_module_sha256: ContentHash,
    pub zones: BTreeMap<String, ProjectedZone>,
    pub assets: Vec<ProjectedAsset>,
    pub manifest: ModulePackageManifest,
    pub diagnostics: Vec<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct PackageReport {
    pub package_path: String,
    pub manifest_sha256: ContentHash,
    pub zone_count: usize,
    pub asset_count: usize,
    pub file_count: usize,
    pub payload_bytes: u64,
    pub zones: Vec<ProjectedZoneSummary>,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct ProjectedZoneSummary {
    pub resref: String,
    pub area_id: AreaId,
    pub archive_sha256: ContentHash,
    pub head_hash: ContentHash,
    pub walkmesh_triangles: usize,
    pub doors: usize,
    pub instances: usize,
}

struct ZoneBuild {
    zone: ProjectedZone,
    assets: Vec<ProjectedAsset>,
}

pub fn project_module(imported: &ImportedModule) -> Result<ProjectedModule, ProjectionError> {
    if imported.areas.is_empty() {
        return Err(ProjectionError::MissingArea(
            "module has no areas".to_owned(),
        ));
    }
    let module_id = ModuleId::deterministic(&format!(
        "aurora:{}",
        imported.manifest.source.module_resref
    ));
    let project_id = ProjectId::deterministic(&format!(
        "aurora-project:{}",
        imported.manifest.source.module_resref
    ));
    let source_module_sha256 = ContentHash::parse(&imported.manifest.source.module_sha256)
        .map_err(|error| ProjectionError::Manifest(error.to_string()))?;
    let mut zones = BTreeMap::new();
    let mut assets = Vec::new();
    let mut diagnostics = Vec::new();
    for area in &imported.areas {
        let build = build_zone(imported, area, module_id, project_id)?;
        for asset in build.assets {
            if !assets
                .iter()
                .any(|candidate: &ProjectedAsset| candidate.package_path == asset.package_path)
            {
                assets.push(asset);
            }
        }
        diagnostics.extend(build.zone.diagnostics.iter().cloned());
        if zones
            .insert(build.zone.resref.clone(), build.zone)
            .is_some()
        {
            return Err(ProjectionError::AreaDocument {
                area: area.manifest.resref.clone(),
                detail: "duplicate area identity".to_owned(),
            });
        }
    }
    assets.sort_by(|left, right| left.package_path.cmp(&right.package_path));
    let manifest = build_manifest(imported, module_id, project_id, &zones, &assets)?;
    manifest
        .validate()
        .map_err(|error| ProjectionError::Manifest(error.to_string()))?;
    Ok(ProjectedModule {
        root: imported.root.clone(),
        module_id,
        project_id,
        name: imported.module.identity.name.clone(),
        entry_area: imported.module.entry_area_resref.clone(),
        source_module_file_name: imported.manifest.source.module_file_name.clone(),
        source_module_sha256,
        zones,
        assets,
        manifest,
        diagnostics,
    })
}

fn build_zone(
    imported: &ImportedModule,
    area: &ImportedArea,
    module_id: ModuleId,
    project_id: ProjectId,
) -> Result<ZoneBuild, ProjectionError> {
    let area_root = imported
        .root
        .join(&area.manifest.path)
        .parent()
        .ok_or_else(|| ProjectionError::AreaDocument {
            area: area.manifest.resref.clone(),
            detail: "area manifest has no parent".to_owned(),
        })?
        .to_path_buf();
    let area_json = area_root.join("area.json");
    let bytes = fs::read(&area_json).map_err(|error| ProjectionError::io(&area_json, error))?;
    let document: AreaDocument =
        serde_json::from_slice(&bytes).map_err(|error| ProjectionError::AreaDocument {
            area: area.manifest.resref.clone(),
            detail: format!("{}: {error}", area_json.display()),
        })?;
    let area_id = AreaId::deterministic(&format!("aurora:{}", document.resref));
    let offset = document.offset();
    let dimensions = document.dimensions_m();
    let mut diagnostics = Vec::new();
    let mut walkmesh = WalkSurface {
        triangles: Vec::new(),
        min: Vec3::new(f32::INFINITY, f32::INFINITY, f32::INFINITY),
        max: Vec3::new(f32::NEG_INFINITY, f32::NEG_INFINITY, f32::NEG_INFINITY),
    };
    let mut navigation = NavigationStats {
        wok_files: 0,
        dwk_files: 0,
        pwk_files: 0,
        triangles: 0,
        unsupported: 0,
    };
    let mut wok_cache = BTreeMap::<String, Walkmesh>::new();
    for tile in &document.tiles {
        let Some(model_resref) = tile.model_resref.as_deref() else {
            diagnostics.push(format!(
                "Tuile {} sans modèle résolu ; non navigable.",
                tile.id
            ));
            continue;
        };
        let Some(navigation_path) = document.navigation_path_for_model(model_resref) else {
            diagnostics.push(format!(
                "Tuile {} ({}) sans WOK résolu ; la tuile reste non navigable.",
                tile.id, model_resref
            ));
            continue;
        };
        let mesh = if let Some(mesh) = wok_cache.get(navigation_path) {
            mesh.clone()
        } else {
            let path = area_root.join(navigation_path);
            let bytes = fs::read(&path).map_err(|error| ProjectionError::io(&path, error))?;
            let mesh = Walkmesh::parse(&bytes).map_err(|error| ProjectionError::Walkmesh {
                path: navigation_path.to_owned(),
                detail: error.to_string(),
            })?;
            navigation.wok_files += 1;
            navigation.triangles += mesh.faces.len();
            wok_cache.insert(navigation_path.to_owned(), mesh.clone());
            mesh
        };
        let position = document.canonical_tile_position(tile);
        let surface = mesh
            .transformed(
                [
                    position.x + offset.x,
                    position.y + offset.y,
                    position.z + offset.z,
                ],
                tile.transform.rotation,
            )
            .map_err(|error| ProjectionError::Walkmesh {
                path: navigation_path.to_owned(),
                detail: error.to_string(),
            })?;
        walkmesh.merge(surface);
    }
    if walkmesh.triangles.is_empty() {
        diagnostics.push(
            "Aucun WOK exploitable : une surface plate de secours couvre la zone ; elle n'est pas une preuve de navigation NWN."
                .to_owned(),
        );
        walkmesh = fallback_surface(dimensions);
    }
    // PWK/DWK are parsed and bounded now; their conditional geometry is modelled by the
    // session's door blockers, not merged into the floor.
    for asset in &document.assets {
        if asset.kind != "navigation-source" {
            continue;
        }
        let extension = Path::new(&asset.path)
            .extension()
            .and_then(|value| value.to_str())
            .unwrap_or_default()
            .to_ascii_lowercase();
        if !matches!(extension.as_str(), "dwk" | "pwk") {
            continue;
        }
        let path = area_root.join(&asset.path);
        let bytes = fs::read(&path).map_err(|error| ProjectionError::io(&path, error))?;
        match Walkmesh::parse(&bytes) {
            Ok(mesh) => {
                navigation.triangles += mesh.faces.len();
                if extension == "dwk" {
                    navigation.dwk_files += 1;
                } else {
                    navigation.pwk_files += 1;
                }
            }
            Err(error) => {
                navigation.unsupported += 1;
                diagnostics.push(format!("{} non converti : {error}", asset.path));
            }
        }
    }
    let mut assets = Vec::new();
    for asset in &document.assets {
        if let Some(projected) = project_asset(&area_root, asset, &mut diagnostics)? {
            assets.push(projected);
        }
    }
    let spawn = spawn_for(imported, &document, offset);
    let gameplay = build_gameplay(&document, area_id, module_id, offset, spawn)?;
    gameplay
        .validate()
        .map_err(|error| ProjectionError::AreaDocument {
            area: document.resref.clone(),
            detail: error.to_string(),
        })?;
    let terrain = build_terrain(&document, area_id, project_id)?;
    terrain
        .validate()
        .map_err(|error| ProjectionError::AreaDocument {
            area: document.resref.clone(),
            detail: error.to_string(),
        })?;
    let (archive_bytes, head_hash) = build_zone_archive(&document, &terrain, &gameplay)?;
    let archive_sha256 = bytes_sha256(&archive_bytes);
    let gameplay_sha256 =
        canonical_json_sha256(&gameplay).map_err(|error| ProjectionError::ZoneArchive {
            area: document.resref.clone(),
            detail: error.to_string(),
        })?;
    let doors = document
        .instances
        .iter()
        .filter(|instance| instance.source.category == "door")
        .map(|instance| {
            let position = instance.transform.position;
            ProjectedDoor {
                id: EntityId::deterministic(&format!("aurora:{}:{}", document.resref, instance.id)),
                tag: instance.source.tag.clone(),
                position: Vec3::new(
                    position[0] + offset.x,
                    position[1] + offset.y,
                    position[2] + offset.z,
                ),
                destination: instance.source.transition_destination.clone(),
                initially_open: false,
                transition_flags: instance.source.transition_flags,
                model_assets: instance.model_assets.clone(),
            }
        })
        .collect::<Vec<_>>();
    let instances = document
        .instances
        .iter()
        .map(|instance| projected_instance(&document, instance, offset))
        .collect::<Vec<_>>();
    Ok(ZoneBuild {
        zone: ProjectedZone {
            resref: document.resref.clone(),
            name: document.name.clone(),
            area_id,
            terrain,
            gameplay,
            walkmesh,
            doors,
            instances,
            spawn,
            spawn_yaw: spawn_yaw(imported, &document),
            archive_path: format!("zones/{}.zone-archive.json", document.resref),
            archive_bytes,
            archive_sha256,
            head_hash,
            gameplay_sha256,
            tile_count: document.tiles.len(),
            navigation,
            diagnostics,
            area_root,
        },
        assets,
    })
}

fn build_terrain(
    document: &AreaDocument,
    area_id: AreaId,
    project_id: ProjectId,
) -> Result<TerrainDocument, ProjectionError> {
    let dimensions = document.dimensions_m();
    let height_sampling = HeightSamplingParameters {
        algorithm: HeightSamplingAlgorithm::FractalValueNoiseV1,
        sample_spacing_m: 1.0,
        base_elevation_m: 0.0,
        amplitude_m: 0.0,
        base_frequency_cycles_per_m: 0.01,
        octaves: 1,
        lacunarity: 2.0,
        persistence: 0.5,
    };
    let terrain = TerrainDocument::new(
        project_id,
        area_id,
        Revision::INITIAL,
        format!("{} (Aurora)", document.name),
        u64::from(document.dimensions[0]) << 32 | u64::from(document.dimensions[1]),
        TerrainExtent::new(dimensions.x, dimensions.z),
        height_sampling,
        vec![TerrainMaterialLayer::new("aurora_ground", "Sol Aurora")],
    );
    Ok(terrain)
}

fn build_gameplay(
    document: &AreaDocument,
    area_id: AreaId,
    module_id: ModuleId,
    offset: Vec3,
    spawn: Vec3,
) -> Result<GameplayDocument, ProjectionError> {
    let player = EntityId::deterministic("aurora:player");
    let mut actors = Vec::new();
    let mut objects = Vec::new();
    let mut places = Vec::new();
    for instance in &document.instances {
        let position = Vec3::new(
            instance.transform.position[0] + offset.x,
            instance.transform.position[1] + offset.y,
            instance.transform.position[2] + offset.z,
        );
        let id = EntityId::deterministic(&format!("aurora:{}:{}", document.resref, instance.id));
        let name = instance
            .source
            .tag
            .clone()
            .or_else(|| instance.source.template_resref.clone())
            .unwrap_or_else(|| format!("{} {}", document.resref, instance.source.category));
        match instance.source.category.as_str() {
            "creature" => actors.push(ActorDefinition {
                id,
                name,
                role: ActorRole::Friendly,
                position,
                max_vitality: 10,
                protection: 0,
                inventory_capacity: 10,
                dialogue: None,
                patrol: Vec::new(),
                flees_danger: false,
            }),
            "door" => objects.push(ObjectDefinition {
                id,
                name,
                position,
                kind: ObjectKind::Door {
                    initially_open: false,
                },
                physics: ObjectPhysics::default(),
            }),
            "placeable" => objects.push(ObjectDefinition {
                id,
                name,
                position,
                kind: ObjectKind::Prop,
                physics: ObjectPhysics::default(),
            }),
            "trigger" | "waypoint" => places.push(PlaceDefinition {
                id,
                name,
                position,
                links: Vec::new(),
            }),
            _ => {}
        }
    }
    actors.push(ActorDefinition {
        id: player,
        name: "Joueur Aurora".to_owned(),
        role: ActorRole::Player,
        position: spawn,
        max_vitality: 100,
        protection: 0,
        inventory_capacity: 20,
        dialogue: None,
        patrol: Vec::new(),
        flees_danger: false,
    });
    Ok(GameplayDocument {
        schema: GAMEPLAY_SCHEMA.to_owned(),
        version: GAMEPLAY_VERSION,
        module_id,
        area_id,
        profile: RulesProfile {
            id: "forge.aurora.m2".to_owned(),
            version: 1,
            interaction_range_m: 2.0,
            attack_range_m: 1.5,
            attack_damage: 1,
            attack_stamina_cost: 1,
            max_stamina: 100,
            max_action_receipts: 128,
            max_events_per_tick: 64,
        },
        creator: None,
        narrative: None,
        factions: None,
        simulation: None,
        scripting: None,
        cinematics: None,
        player,
        actors,
        items: Vec::new(),
        objects,
        places,
        dialogues: Vec::new(),
        quests: Vec::new(),
    })
}

fn build_zone_archive(
    document: &AreaDocument,
    terrain: &TerrainDocument,
    gameplay: &GameplayDocument,
) -> Result<(Vec<u8>, ContentHash), ProjectionError> {
    let mut session = ZoneEditSession::from_initial_terrain(terrain.clone()).map_err(|detail| {
        ProjectionError::ZoneArchive {
            area: document.resref.clone(),
            detail,
        }
    })?;
    let request = session
        .head()
        .prepare_gameplay(
            Some(gameplay.clone()),
            forge_core::CommandId::deterministic(&format!("aurora:{}:gameplay", document.resref)),
        )
        .map_err(|detail| ProjectionError::ZoneArchive {
            area: document.resref.clone(),
            detail,
        })?;
    session
        .execute(&request)
        .map_err(|detail| ProjectionError::ZoneArchive {
            area: document.resref.clone(),
            detail,
        })?;
    let head_hash =
        session
            .head()
            .content_hash()
            .map_err(|detail| ProjectionError::ZoneArchive {
                area: document.resref.clone(),
                detail,
            })?;
    let bytes = session
        .archive_bytes()
        .map_err(|detail| ProjectionError::ZoneArchive {
            area: document.resref.clone(),
            detail,
        })?;
    Ok((bytes, head_hash))
}

fn project_asset(
    area_root: &Path,
    asset: &AreaAsset,
    diagnostics: &mut Vec<String>,
) -> Result<Option<ProjectedAsset>, ProjectionError> {
    if asset.path.trim().is_empty() {
        return Ok(None);
    }
    let path = area_root.join(&asset.path);
    let bytes = fs::read(&path).map_err(|error| ProjectionError::io(&path, error))?;
    if bytes.is_empty() || bytes.len() > MAX_ASSET_BYTES {
        return Err(ProjectionError::Asset {
            path: asset.path.clone(),
            detail: format!("invalid size {}", bytes.len()),
        });
    }
    let extension = Path::new(&asset.path)
        .extension()
        .and_then(|value| value.to_str())
        .unwrap_or_default()
        .to_ascii_lowercase();
    if let Err(detail) = validate_asset_bytes(&bytes, &extension) {
        diagnostics.push(format!("{} ignoré : {detail}", asset.path));
        return Ok(None);
    }
    let sha256 = bytes_sha256(&bytes);
    Ok(Some(ProjectedAsset {
        source_path: asset.path.clone(),
        package_path: format!("assets/{sha256}.{extension}"),
        sha256,
        bytes,
        kind: asset.kind.clone(),
    }))
}

fn validate_asset_bytes(bytes: &[u8], extension: &str) -> Result<(), String> {
    match extension {
        "glb" => {
            if bytes.len() < 12 || &bytes[..4] != b"glTF" {
                return Err("GLB magic is absent".to_owned());
            }
            let version = u32::from_le_bytes([bytes[4], bytes[5], bytes[6], bytes[7]]);
            if version != 2 {
                return Err(format!("unsupported GLB version {version}"));
            }
        }
        "png" => {
            if !bytes.starts_with(b"\x89PNG\r\n\x1a\n") {
                return Err("PNG signature is absent".to_owned());
            }
        }
        "wok" | "pwk" | "dwk" => {
            Walkmesh::parse(bytes).map_err(|error| error.to_string())?;
        }
        "wav" => {
            if !bytes.starts_with(b"RIFF") || bytes.get(8..12) != Some(b"WAVE") {
                return Err("WAV header is absent".to_owned());
            }
        }
        "ogg" => {
            if !bytes.starts_with(b"OggS") {
                return Err("OGG signature is absent".to_owned());
            }
        }
        "dds" => {
            if !bytes.starts_with(b"DDS ") {
                return Err("DDS signature is absent".to_owned());
            }
        }
        "tga" => {}
        _ => return Err(format!("unsupported asset extension .{extension}")),
    }
    Ok(())
}

fn build_manifest(
    imported: &ImportedModule,
    module_id: ModuleId,
    project_id: ProjectId,
    zones: &BTreeMap<String, ProjectedZone>,
    assets: &[ProjectedAsset],
) -> Result<ModulePackageManifest, ProjectionError> {
    let mut files = Vec::new();
    let mut module_zones = Vec::new();
    for zone in zones.values() {
        files.push(ModuleFile {
            path: zone.archive_path.clone(),
            bytes: zone.archive_bytes.len() as u64,
            sha256: zone.archive_sha256,
            role: ModuleFileRole::ZoneArchive,
        });
        module_zones.push(ModuleZone {
            area_id: zone.area_id,
            archive_path: zone.archive_path.clone(),
            archive_sha256: zone.archive_sha256,
            head_hash: zone.head_hash,
            gameplay_sha256: Some(zone.gameplay_sha256),
            source_blobs: Vec::new(),
        });
    }
    let mut module_assets = Vec::new();
    for asset in assets {
        files.push(ModuleFile {
            path: asset.package_path.clone(),
            bytes: asset.bytes.len() as u64,
            sha256: asset.sha256,
            role: ModuleFileRole::Asset,
        });
        module_assets.push(ModuleAsset {
            asset_id: AssetId::deterministic(&format!("aurora:{}", asset.package_path)),
            file_path: asset.package_path.clone(),
            sha256: asset.sha256,
            dependencies: Vec::new(),
        });
    }
    for (path, bytes, role) in extra_files(
        &imported.manifest.source.module_sha256,
        &imported.manifest.source.module_file_name,
        assets,
    )? {
        files.push(ModuleFile {
            path,
            bytes: bytes.len() as u64,
            sha256: bytes_sha256(&bytes),
            role,
        });
    }
    files.sort_by(|left, right| left.path.cmp(&right.path));
    Ok(ModulePackageManifest {
        schema: MODULE_PACKAGE_SCHEMA.to_owned(),
        version: MODULE_PACKAGE_VERSION,
        project_id,
        module_id,
        name: imported.module.identity.name.clone(),
        module_version: "0.1.0-m2".to_owned(),
        compatibility: ModuleCompatibility {
            engine_version: env!("CARGO_PKG_VERSION").to_owned(),
            module_api_version: MODULE_PACKAGE_VERSION,
        },
        provenance: ModuleProvenance {
            publisher: "OpenRPG Forge".to_owned(),
            source: "OpenNever module-migration-bundle@1.0.0".to_owned(),
            source_revision: imported.manifest.source.module_sha256.clone(),
            rights: ModuleRights::PrivateLocalOnly,
            license_paths: vec!["LICENSE-MIT.txt".to_owned()],
            proof_paths: vec!["provenance.json".to_owned()],
            credits_path: "credits.txt".to_owned(),
        },
        asset_catalog_path: "asset-catalog.json".to_owned(),
        files,
        zones: module_zones,
        assets: module_assets,
        historical_models: Vec::new(),
        gameplay_visuals: Vec::new(),
        vegetation: Vec::new(),
        environments: Vec::new(),
        objects: Vec::new(),
        fish_profiles: Vec::new(),
        terrain_surfaces: Vec::new(),
        ocean_inputs: Vec::new(),
        local_light_packages: Vec::new(),
        placement_companions: Vec::new(),
        creator: None,
    })
}

fn asset_catalog(assets: &[ProjectedAsset]) -> serde_json::Value {
    serde_json::json!({
        "schema": "forge.aurora-asset-catalog@1",
        "version": 1,
        "assets": assets.iter().map(|asset| serde_json::json!({
            "sourcePath": asset.source_path,
            "packagePath": asset.package_path,
            "sha256": asset.sha256.to_string(),
            "bytes": asset.bytes.len(),
            "kind": asset.kind,
        })).collect::<Vec<_>>(),
    })
}

fn extra_files(
    source_module_sha256: &str,
    source_module_name: &str,
    assets: &[ProjectedAsset],
) -> Result<Vec<(String, Vec<u8>, ModuleFileRole)>, ProjectionError> {
    let catalog = serde_json::to_vec_pretty(&asset_catalog(assets))
        .map_err(|error| ProjectionError::Manifest(error.to_string()))?;
    let provenance = serde_json::to_vec_pretty(&serde_json::json!({
        "schema": "forge.aurora-provenance@1",
        "sourceBundle": "module-migration-bundle@1.0.0",
        "sourceModuleSha256": source_module_sha256,
        "sourceModuleName": source_module_name,
        "generatedBy": "forge_aurora_runtime",
        "rights": "private_local_only",
        "note": "Les octets NWN restent propriétaires et ne sont pas redistribuables."
    }))
    .map_err(|error| ProjectionError::Manifest(error.to_string()))?;
    Ok(vec![
        (
            "asset-catalog.json".to_owned(),
            catalog,
            ModuleFileRole::AssetCatalog,
        ),
        (
            "provenance.json".to_owned(),
            provenance,
            ModuleFileRole::Provenance,
        ),
        (
            "credits.txt".to_owned(),
            b"Aurora bridge projection generated by OpenRPG Forge. NWN:EE data remains proprietary.\n"
                .to_vec(),
            ModuleFileRole::Credits,
        ),
        (
            "LICENSE-MIT.txt".to_owned(),
            b"Original bridge code: MIT. NWN-derived assets: private local use only, no redistribution.\n"
                .to_vec(),
            ModuleFileRole::License,
        ),
    ])
}

impl ProjectedModule {
    pub fn zone(&self, resref: &str) -> Option<&ProjectedZone> {
        self.zones.get(resref)
    }

    pub fn write_package(&self, destination: &Path) -> Result<PackageReport, ProjectionError> {
        let mut report = self.package_report(destination);
        if destination.exists() {
            return Err(ProjectionError::Io {
                path: destination.display().to_string(),
                detail: "destination already exists".to_owned(),
            });
        }
        let parent = destination.parent().ok_or_else(|| ProjectionError::Io {
            path: destination.display().to_string(),
            detail: "destination has no parent".to_owned(),
        })?;
        if !parent.is_dir() {
            return Err(ProjectionError::Io {
                path: parent.display().to_string(),
                detail: "destination parent is not a directory".to_owned(),
            });
        }
        let staging = parent.join(format!(
            ".aurora-package-{}-{}.tmp",
            std::process::id(),
            self.manifest_short_hash()
        ));
        let _ = fs::remove_dir_all(&staging);
        fs::create_dir_all(&staging).map_err(|error| ProjectionError::io(&staging, error))?;
        let result = self.write_package_inner(&staging);
        if let Err(error) = result {
            let _ = fs::remove_dir_all(&staging);
            return Err(error);
        }
        fs::rename(&staging, destination).map_err(|error| {
            let _ = fs::remove_dir_all(&staging);
            ProjectionError::io(destination, error)
        })?;
        report.package_path = destination.display().to_string();
        Ok(report)
    }

    fn write_package_inner(&self, root: &Path) -> Result<(), ProjectionError> {
        for zone in self.zones.values() {
            write_bytes(root, &zone.archive_path, &zone.archive_bytes)?;
        }
        for asset in &self.assets {
            write_bytes(root, &asset.package_path, &asset.bytes)?;
        }
        for (path, bytes, _) in extra_files(
            &self.source_module_sha256.to_string(),
            &self.source_module_file_name,
            &self.assets,
        )? {
            write_bytes(root, &path, &bytes)?;
        }
        let manifest_bytes = self
            .manifest
            .to_bytes()
            .map_err(|error| ProjectionError::Manifest(error.to_string()))?;
        write_bytes(root, "module.json", &manifest_bytes)?;
        Ok(())
    }

    fn manifest_short_hash(&self) -> String {
        self.manifest
            .content_hash()
            .map(|hash| hash.to_string()[..12].to_owned())
            .unwrap_or_else(|_| "unverified".to_owned())
    }

    fn package_report(&self, destination: &Path) -> PackageReport {
        PackageReport {
            package_path: destination.display().to_string(),
            manifest_sha256: self
                .manifest
                .content_hash()
                .unwrap_or_else(|_| bytes_sha256(b"invalid")),
            zone_count: self.zones.len(),
            asset_count: self.assets.len(),
            file_count: self.manifest.files.len() + 1,
            payload_bytes: self.manifest.files.iter().map(|file| file.bytes).sum(),
            zones: self
                .zones
                .values()
                .map(|zone| ProjectedZoneSummary {
                    resref: zone.resref.clone(),
                    area_id: zone.area_id,
                    archive_sha256: zone.archive_sha256,
                    head_hash: zone.head_hash,
                    walkmesh_triangles: zone.walkmesh.triangles.len(),
                    doors: zone.doors.len(),
                    instances: zone.instances.len(),
                })
                .collect(),
        }
    }
}

fn write_bytes(root: &Path, relative: &str, bytes: &[u8]) -> Result<(), ProjectionError> {
    let path = root.join(relative);
    if let Some(parent) = path.parent() {
        fs::create_dir_all(parent).map_err(|error| ProjectionError::io(parent, error))?;
    }
    fs::write(&path, bytes).map_err(|error| ProjectionError::io(&path, error))
}

#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "camelCase")]
struct StartPosition {
    area_resref: String,
    position: [f32; 3],
    direction: [f32; 2],
}

fn start_position_for(imported: &ImportedModule, area: &str) -> Option<StartPosition> {
    imported.module.start_positions.iter().find_map(|value| {
        let position: StartPosition = serde_json::from_value(value.clone()).ok()?;
        (position.area_resref == area).then_some(position)
    })
}

fn spawn_for(imported: &ImportedModule, document: &AreaDocument, offset: Vec3) -> Vec3 {
    start_position_for(imported, &document.resref)
        .and_then(|position| {
            let source = position.position;
            source
                .iter()
                .all(|value| value.is_finite())
                .then_some(Vec3::new(
                    source[0] + offset.x,
                    source[2] + offset.y,
                    -source[1] + offset.z,
                ))
        })
        .unwrap_or(Vec3::ZERO)
}

fn spawn_yaw(imported: &ImportedModule, document: &AreaDocument) -> f32 {
    start_position_for(imported, &document.resref)
        .map(|position| {
            let [x, y] = position.direction;
            if x.is_finite() && y.is_finite() {
                y.atan2(x)
            } else {
                0.0
            }
        })
        .unwrap_or(0.0)
}

fn projected_instance(
    document: &AreaDocument,
    instance: &AreaInstance,
    offset: Vec3,
) -> ProjectedInstance {
    let position = instance.transform.position;
    ProjectedInstance {
        id: EntityId::deterministic(&format!("aurora:{}:{}", document.resref, instance.id)),
        category: instance.source.category.clone(),
        tag: instance.source.tag.clone(),
        template_resref: instance.source.template_resref.clone(),
        position: Vec3::new(
            position[0] + offset.x,
            position[1] + offset.y,
            position[2] + offset.z,
        ),
        source_status: instance.status.clone(),
        model_assets: instance.model_assets.clone(),
    }
}

fn fallback_surface(dimensions: Vec3) -> WalkSurface {
    let half_x = dimensions.x * 0.5;
    let half_z = dimensions.z * 0.5;
    let a = Vec3::new(-half_x, 0.0, -half_z);
    let b = Vec3::new(half_x, 0.0, -half_z);
    let c = Vec3::new(half_x, 0.0, half_z);
    let d = Vec3::new(-half_x, 0.0, half_z);
    WalkSurface {
        triangles: vec![
            crate::walkmesh::WorldTriangle {
                a,
                b,
                c,
                surface: 3,
            },
            crate::walkmesh::WorldTriangle {
                a,
                b: c,
                c: d,
                surface: 3,
            },
        ],
        min: Vec3::new(-half_x, 0.0, -half_z),
        max: Vec3::new(half_x, 0.0, half_z),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn asset_validation_rejects_bad_magic() {
        assert!(validate_asset_bytes(b"not a glb", "glb").is_err());
        assert!(validate_asset_bytes(b"not a png", "png").is_err());
        assert!(validate_asset_bytes(b"\x89PNG\r\n\x1a\nrest", "png").is_ok());
    }

    #[test]
    fn fallback_surface_covers_the_zone_centre() {
        let surface = fallback_surface(Vec3::new(20.0, 0.0, 10.0));
        assert_eq!(surface.height_at(0.0, 0.0), Some(0.0));
        assert_eq!(surface.height_at(10.0, 5.0), Some(0.0));
        assert_eq!(surface.height_at(11.0, 0.0), None);
    }
}
