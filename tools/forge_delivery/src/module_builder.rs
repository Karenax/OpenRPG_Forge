//! Native assembly from one reviewed canonical RPG zone and closed governed assets.
//! Filesystem roots are supplied by the native host, never by frontend IPC paths.
use crate::{
    Result,
    manifest::{Descriptor, Kind, Rights},
    orpmod,
};
use forge_assets::governed_import::{
    DurableAssetSource, GovernedAssetFile, GovernedAssetKind, GovernedImportCatalog,
};
use forge_core::{AreaId, ContentHash, ModuleId, bytes_sha256};
use forge_project::{module_package::*, zone_authoring::session::ZoneEditSession};
use serde::{Deserialize, Serialize};
use std::{
    collections::BTreeMap,
    fs::{self, OpenOptions},
    io::Write,
    path::{Path, PathBuf},
    sync::Arc,
};

const ZONE: &str = "_forge-export/zone.json";
const CATALOG: &str = "_forge-export/assets.json";
const LICENSE: &str = "_forge-export/license.txt";
const SOURCE: &str = "_forge-export/source.json";
const CREDITS: &str = "_forge-export/credits.txt";

mod aggregate;
mod companions;
mod history;
mod lights;
mod origin;
mod placement_companions;
mod sources;
pub use aggregate::{
    build_module_with_history, build_module_with_origin, build_module_with_reference,
};
pub use history::ExportHistoricalModel;

pub enum CompanionInput<'a> {
    Origin(Option<ExportOrigin>),
    Reference(&'a crate::legacy_reference::ReferenceScene),
}

struct Prepared {
    module: ModulePackageManifest,
    payloads: BTreeMap<String, Vec<u8>>,
    origin: Option<origin::PreparedOrigin>,
}

#[derive(Default)]
struct Inventory {
    roles: BTreeMap<String, ModuleFileRole>,
    media: BTreeMap<String, GovernedAssetKind>,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ExportMetadata {
    pub name: String,
    pub version: String,
    pub publisher: String,
    pub license_text: String,
    pub rights: ModuleRights,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ExportedModule {
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub module_hash: Option<ContentHash>,
    pub module_id: ModuleId,
    pub area_id: AreaId,
    pub head_hash: ContentHash,
    pub package_path: PathBuf,
    pub source_root: PathBuf,
    pub package_sha256: ContentHash,
    pub transport_manifest_sha256: String,
}

/// Native origin bytes selected through the canonical workspace origin pin.
#[derive(Clone)]
pub struct ExportOrigin {
    pub package_bytes: Arc<[u8]>,
    pub expected_package_sha256: ContentHash,
}

pub fn build_zone_with_origin(
    exports_root: &Path,
    archive: &[u8],
    expected_head: ContentHash,
    metadata: ExportMetadata,
    catalog: GovernedImportCatalog,
    payloads: BTreeMap<String, Vec<u8>>,
    origin: Option<ExportOrigin>,
) -> Result<ExportedModule> {
    if !exports_root.is_absolute() {
        return Err("The native export root must be absolute".into());
    }
    crate::manifest::safe_chain(exports_root)?;
    let prepared = prepare(
        archive,
        expected_head,
        metadata,
        &catalog,
        payloads,
        CompanionInput::Origin(origin),
        Vec::new(),
    )?;
    publish_prepared(exports_root, prepared, expected_head)
}

/// Export one RPG zone with its exact governed assets and immutable source closure.
/// Blob payloads use `_forge-export/source-blobs/<sha256>.bin`; missing, corrupt
/// or extra files are refused before creating an output.
pub fn build_zone(
    exports_root: &Path,
    archive: &[u8],
    expected_head: ContentHash,
    metadata: ExportMetadata,
    catalog: GovernedImportCatalog,
    payloads: BTreeMap<String, Vec<u8>>,
) -> Result<ExportedModule> {
    build_zone_with_origin(
        exports_root,
        archive,
        expected_head,
        metadata,
        catalog,
        payloads,
        None,
    )
}

fn publish_prepared(
    exports_root: &Path,
    prepared: Prepared,
    expected_head: ContentHash,
) -> Result<ExportedModule> {
    let directory = exports_root.join(format!("module-{}", crate::attempt()?));
    crate::manifest::safe_chain(&directory)?;
    fs::create_dir_all(exports_root).map_err(|e| e.to_string())?;
    crate::manifest::safe_chain(exports_root)?;
    fs::create_dir(&directory).map_err(|e| e.to_string())?;
    let source_root = directory.join("source");
    fs::create_dir(&source_root).map_err(|e| e.to_string())?;
    write_payloads(&source_root, &prepared.payloads)?;
    let module = &prepared.module;
    let package_path = directory.join("module.orpmod");
    let (_, transport_manifest_sha256) = orpmod::pack(
        &source_root,
        Descriptor {
            schema: "forge.delivery.package.v1".into(),
            id: module.module_id.to_string(),
            label: module.name.clone(),
            version: module.module_version.clone(),
            kind: Kind::Module,
            platform: "any".into(),
            source_revision: module.provenance.source_revision.clone(),
            rights: match module.provenance.rights {
                ModuleRights::PrivateLocalOnly => Rights::PrivateLocalOnly,
                ModuleRights::Redistributable => Rights::Redistributable,
            },
            license_paths: module.provenance.license_paths.clone(),
            files: vec![],
            product: None,
        },
        &package_path,
        module.provenance.rights == ModuleRights::Redistributable,
    )?;
    Ok(ExportedModule {
        module_hash: None,
        module_id: module.module_id,
        area_id: module.zones[0].area_id,
        head_hash: expected_head,
        package_sha256: ContentHash::parse(&crate::file_hash(&package_path)?)
            .map_err(|e| e.to_string())?,
        package_path,
        source_root,
        transport_manifest_sha256,
    })
}

fn prepare(
    archive: &[u8],
    expected_head: ContentHash,
    metadata: ExportMetadata,
    catalog: &GovernedImportCatalog,
    mut payloads: BTreeMap<String, Vec<u8>>,
    input: CompanionInput<'_>,
    historical_models: Vec<ExportHistoricalModel>,
) -> Result<Prepared> {
    let zone = ZoneEditSession::from_archive_bytes(archive)?;
    if zone.head().content_hash()? != expected_head {
        return Err("The reviewed canonical zone has changed".into());
    }
    let required_sources = zone.required_source_blobs()?;
    sources::validate_budget(&required_sources, archive.len())?;
    let head = zone.head();
    let gameplay = head
        .corrections
        .gameplay
        .as_deref()
        .ok_or("Studio module export requires canonical RPG definitions")?;
    if metadata.license_text.trim().is_empty()
        || metadata.license_text.len() > 65536
        || metadata
            .license_text
            .chars()
            .any(|c| c.is_control() && !matches!(c, '\n' | '\r' | '\t'))
    {
        return Err("A bounded, explicit UTF-8 module licence is required".into());
    }
    let publishing = metadata.rights == ModuleRights::Redistributable;
    let reference_revision = match &input {
        CompanionInput::Reference(scene) => Some(scene.revision()),
        CompanionInput::Origin(_) => None,
    };
    let (origin, companions) = match input {
        CompanionInput::Origin(origin) => {
            let origin = origin::prepare(origin, gameplay.module_id, metadata.rights)?;
            let companions = companions::prepare(origin.as_ref(), &zone, &mut payloads)?;
            (origin, companions)
        }
        CompanionInput::Reference(scene) => (
            None,
            companions::prepare_reference(scene, &zone, &mut payloads)?,
        ),
    };
    companions::supply_lights(origin.as_ref(), &zone, &mut payloads)?;
    orpmod::validate_module_catalog(catalog, publishing)?;
    validate_models(head, catalog, companions.objects.as_ref(), &companions.fish)?;
    let light_packages = lights::prepare(&zone, &payloads)?;
    let placement_packages = placement_companions::prepare(&zone, &payloads)?;
    let has_placement_packages = !placement_packages.is_empty();
    if publishing && !placement_packages.is_empty() {
        return Err("Placement companion redistribution rights are not yet qualified".into());
    }
    let mut inventory = inventory(
        catalog,
        &payloads,
        publishing,
        &required_sources,
        &light_packages,
        &placement_packages,
        &companions,
    )?;
    let historical_models =
        history::capture(historical_models, publishing, &mut payloads, &mut inventory)?;
    let has_historical_models = !historical_models.is_empty();
    let catalog_bytes = serde_json::to_vec(catalog).map_err(|e| e.to_string())?;
    if catalog_bytes.len() > MAX_MODULE_MANIFEST_BYTES {
        return Err("The exported governed catalogue exceeds its document budget".into());
    }
    let mut source = serde_json::json!({"schema":"forge.studio-module-export-source","version":1,
        "scope":"one reviewed RPG zone and the full explicitly selected governed catalogue",
        "project_id":head.base.terrain.project_id,"module_id":gameplay.module_id,
        "area_id":gameplay.area_id,"canonical_head_sha256":expected_head,
        "archive_sha256":bytes_sha256(archive),"publisher":metadata.publisher});
    if let Some(revision) = reference_revision {
        source["reference_assets_manifest_sha256"] =
            serde_json::to_value(revision).map_err(|error| error.to_string())?;
    }
    let mut credits = format!(
        "{}\n{}\nAsset authors, licences, lineage and preserved sources are recorded in the governed catalogue.",
        metadata.name, metadata.publisher
    );
    let license = if let Some(origin) = &origin {
        source["origin"] = origin.proof(expected_head);
        credits.push_str(&origin.credits_reference());
        format!("Conditions declared for the newly authored changes. Source module conditions remain applicable and are preserved under _forge-export/origin/.\n\n{}",metadata.license_text).into_bytes()
    } else {
        metadata.license_text.as_bytes().to_vec()
    };
    for (path, bytes, role) in [
        (ZONE, archive.to_vec(), ModuleFileRole::ZoneArchive),
        (CATALOG, catalog_bytes, ModuleFileRole::AssetCatalog),
        (LICENSE, license, ModuleFileRole::License),
        (SOURCE, crate::json(&source)?, ModuleFileRole::Provenance),
        (CREDITS, credits.into_bytes(), ModuleFileRole::Credits),
    ] {
        if payloads.contains_key(path) {
            return Err(format!(
                "Asset input conflicts with a generated module document: {path}"
            ));
        }
        payloads.insert(path.into(), bytes);
        inventory.roles.insert(path.into(), role);
    }
    if let Some(origin) = &origin {
        origin.append(expected_head, &mut payloads, &mut inventory.roles)?;
    }
    let files = payloads
        .iter()
        .map(|(path, bytes)| {
            Ok(ModuleFile {
                path: path.clone(),
                bytes: u64::try_from(bytes.len()).map_err(|e| e.to_string())?,
                sha256: bytes_sha256(bytes),
                role: *inventory
                    .roles
                    .get(path)
                    .ok_or("Unreferenced export file")?,
            })
        })
        .collect::<Result<Vec<_>>>()?;
    let module = ModulePackageManifest {
        historical_models,
        terrain_surfaces: companions.terrain.iter().cloned().collect(),
        ocean_inputs: companions.ocean.iter().cloned().collect(),
        local_light_packages: light_packages,
        placement_companions: placement_packages,
        environments: companions.environment.iter().cloned().collect(),
        objects: companions.objects.iter().cloned().collect(),
        fish_profiles: companions.fish.clone(),
        vegetation: companions.vegetation.iter().cloned().collect(),
        creator: None,
        schema: MODULE_PACKAGE_SCHEMA.into(),
        version: MODULE_PACKAGE_VERSION,
        project_id: head.base.terrain.project_id,
        module_id: gameplay.module_id,
        name: metadata.name,
        module_version: metadata.version,
        compatibility: ModuleCompatibility {
            engine_version: orpmod::ENGINE_VERSION.into(),
            module_api_version: (if zone.required_local_light_packages()?.is_empty() {
                orpmod::MODULE_API_VERSION
            } else {
                MODULE_LOCAL_LIGHTS_API_VERSION
            })
            .max(companions.api_version())
            .max(if zone.has_wind_history() {
                MODULE_SCENE_WIND_API_VERSION
            } else {
                orpmod::MODULE_API_VERSION
            })
            .max(if has_placement_packages {
                MODULE_PLACEMENT_COMPANIONS_API_VERSION
            } else {
                orpmod::MODULE_API_VERSION
            })
            .max(if has_historical_models {
                MODULE_HISTORICAL_MODELS_API_VERSION
            } else {
                orpmod::MODULE_API_VERSION
            }),
        },
        provenance: ModuleProvenance {
            publisher: metadata.publisher,
            source: "Canonical Studio RPG zone and explicitly selected governed asset catalogue"
                .into(),
            source_revision: format!("studio/{expected_head}"),
            rights: metadata.rights,
            license_paths: inventory
                .roles
                .iter()
                .filter(|(_, role)| **role == ModuleFileRole::License)
                .map(|(path, _)| path.clone())
                .collect(),
            proof_paths: inventory
                .roles
                .iter()
                .filter(|(_, role)| **role == ModuleFileRole::Provenance)
                .map(|(path, _)| path.clone())
                .collect(),
            credits_path: CREDITS.into(),
        },
        asset_catalog_path: CATALOG.into(),
        files,
        zones: vec![ModuleZone {
            area_id: gameplay.area_id,
            archive_path: ZONE.into(),
            archive_sha256: bytes_sha256(archive),
            head_hash: expected_head,
            gameplay_sha256: Some(
                forge_core::canonical_json_sha256(gameplay).map_err(|e| e.to_string())?,
            ),
            source_blobs: required_sources.keys().copied().collect(),
        }],
        assets: catalog
            .assets
            .iter()
            .map(|asset| ModuleAsset {
                asset_id: asset.id,
                file_path: asset.payload.path.clone(),
                sha256: asset.payload.sha256,
                dependencies: asset.derived_from.into_iter().collect(),
            })
            .collect(),
        gameplay_visuals: head
            .corrections
            .gameplay_visuals
            .clone()
            .unwrap_or_default(),
    };
    module
        .validate_zone_archive(gameplay.area_id, archive)
        .map_err(|e| e.to_string())?;
    validate_placement_payloads(&module, catalog, &payloads)?;
    for (path, bytes) in &payloads {
        orpmod::validate_payload_bytes(path, bytes, inventory.media.get(path).copied())?;
    }
    payloads.insert(
        MODULE_MANIFEST_PATH.into(),
        module.to_bytes().map_err(|e| e.to_string())?,
    );
    Ok(Prepared {
        module,
        payloads,
        origin,
    })
}

fn inventory(
    catalog: &GovernedImportCatalog,
    payloads: &BTreeMap<String, Vec<u8>>,
    publishing: bool,
    sources: &BTreeMap<ContentHash, usize>,
    lights: &[ModuleLocalLightPackage],
    placements: &[ModulePlacementCompanion],
    companions: &companions::Companions,
) -> Result<Inventory> {
    let mut inventory = Inventory::default();
    for asset in &catalog.assets {
        inventory.file(&asset.payload, ModuleFileRole::Asset, payloads)?;
        inventory.kind(&asset.payload.path, asset.kind)?;
        if let Some(file) = &asset.thumbnail {
            inventory.file(file, ModuleFileRole::AssetAuxiliary, payloads)?;
            inventory.kind(&file.path, GovernedAssetKind::Texture)?;
        }
        if let Some(
            DurableAssetSource::AuthoredFile { file } | DurableAssetSource::Recipe { file },
        ) = &asset.durable_source
        {
            inventory.file(file, ModuleFileRole::AssetAuxiliary, payloads)?;
        }
        inventory.evidence(&asset.provenance.evidence, payloads)?;
        if let Some(path) = &asset.provenance.rights.redistribution_evidence
            && (publishing || payloads.contains_key(path))
        {
            inventory.evidence(path, payloads)?;
        }
    }
    sources::inventory(sources, payloads, &mut inventory)?;
    lights::inventory(lights, &mut inventory)?;
    placement_companions::inventory(placements, &mut inventory)?;
    for (path, role) in &companions.roles {
        inventory.role(path, *role)?;
    }
    if payloads.len() != inventory.roles.len()
        || payloads
            .keys()
            .any(|path| !inventory.roles.contains_key(path))
    {
        return Err(
            "Export payloads must exactly match catalogued media, sources, thumbnails and evidence"
                .into(),
        );
    }
    Ok(inventory)
}

fn validate_placement_payloads(
    module: &ModulePackageManifest,
    catalog: &GovernedImportCatalog,
    payloads: &BTreeMap<String, Vec<u8>>,
) -> Result<()> {
    crate::module_placement_companions::validate(module, catalog, |path, maximum| {
        let bytes = payloads.get(path).ok_or("Companion payload is missing")?;
        if bytes.len() > maximum {
            return Err("Companion payload exceeds its budget".into());
        }
        Ok(bytes.clone())
    })
}

fn validate_models(
    head: &forge_project::zone_authoring::ZoneAuthoringHead,
    catalog: &GovernedImportCatalog,
    objects: Option<&ModuleObjectBinding>,
    fish: &[ModuleFishBinding],
) -> Result<()> {
    let require = |id, pin| {
        if catalog.assets.iter().any(|asset| {
            asset.id == id && asset.payload.sha256 == pin && asset.kind == GovernedAssetKind::Mesh
        }) {
            Ok(())
        } else {
            Err("A canonical model reference has no exact governed mesh revision".to_owned())
        }
    };
    for binding in head
        .corrections
        .gameplay_visuals
        .as_deref()
        .unwrap_or_default()
    {
        require(binding.asset.asset_id, binding.asset.sha256)?;
    }
    let require_placement = |p: &forge_project::zone_authoring::placements::ManualPlacement| {
        if objects.is_some_and(|binding| match &binding.source {
            ModuleObjectSource::SourceSurfaces { assets, .. } => assets
                .iter()
                .any(|asset| asset.asset_id == p.asset_id && asset.sha256 == p.asset_hash),
        }) {
            return Ok(());
        }
        if fish.iter().any(|binding| {
            binding.area_id == head.base.terrain.area_id
                && binding
                    .assets
                    .iter()
                    .any(|asset| asset.asset_id == p.asset_id && asset.sha256 == p.asset_hash)
        }) {
            return Ok(());
        }
        require(p.asset_id, p.asset_hash)
    };
    for placement in head.corrections.placements.as_deref().unwrap_or_default() {
        require_placement(placement)?;
    }
    let require_source = |p: &forge_project::zone_authoring::placements::ManualPlacement| {
        if forge_project::zone_authoring::imported_plants::is_source_asset(
            head,
            p.asset_id,
            p.asset_hash,
        ) {
            Ok(())
        } else {
            require_placement(p)
        }
    };
    for edit in head
        .corrections
        .source_object_edits
        .as_deref()
        .unwrap_or_default()
    {
        require_source(&edit.source)?;
        if let forge_project::zone_authoring::source_objects::SourceObjectState::Edited {
            placement,
        } = &edit.state
        {
            require_source(placement)?;
        }
        for placement in &edit.copies {
            require_source(placement)?;
        }
    }
    Ok(())
}

impl Inventory {
    fn file(
        &mut self,
        file: &GovernedAssetFile,
        role: ModuleFileRole,
        payloads: &BTreeMap<String, Vec<u8>>,
    ) -> Result<()> {
        let bytes = payloads
            .get(&file.path)
            .ok_or_else(|| format!("Missing exported asset/source: {}", file.path))?;
        if u64::try_from(bytes.len()).ok() != Some(file.bytes) || bytes_sha256(bytes) != file.sha256
        {
            return Err(format!(
                "Exported payload differs from its exact catalogue pin: {}",
                file.path
            ));
        }
        self.role(&file.path, role)
    }
    fn role(&mut self, path: &str, role: ModuleFileRole) -> Result<()> {
        if let Some(old) = self.roles.get(path).copied() {
            if old == role {
                return Ok(());
            }
            if matches!(
                (old, role),
                (ModuleFileRole::Asset, ModuleFileRole::AssetAuxiliary)
                    | (ModuleFileRole::AssetAuxiliary, ModuleFileRole::Asset)
            ) {
                self.roles.insert(path.into(), ModuleFileRole::Asset);
                return Ok(());
            }
            return Err(format!("Conflicting exported file roles: {path}"));
        }
        self.roles.insert(path.into(), role);
        Ok(())
    }
    fn kind(&mut self, path: &str, kind: GovernedAssetKind) -> Result<()> {
        if self
            .media
            .insert(path.into(), kind)
            .is_some_and(|old| old != kind)
        {
            return Err(format!("Conflicting exported media kinds: {path}"));
        }
        Ok(())
    }
    fn evidence(&mut self, path: &str, payloads: &BTreeMap<String, Vec<u8>>) -> Result<()> {
        if !payloads.contains_key(path) {
            return Err(format!("Missing embedded asset rights evidence: {path}"));
        }
        self.role(path, ModuleFileRole::License)
    }
}

fn write_payloads(root: &Path, payloads: &BTreeMap<String, Vec<u8>>) -> Result<()> {
    for (name, bytes) in payloads {
        let path = root.join(name);
        crate::manifest::safe_chain(&path)?;
        let parent = path.parent().ok_or("Export payload parent missing")?;
        fs::create_dir_all(parent).map_err(|e| e.to_string())?;
        crate::manifest::safe_chain(parent)?;
        let mut file = OpenOptions::new()
            .write(true)
            .create_new(true)
            .open(&path)
            .map_err(|e| e.to_string())?;
        file.write_all(bytes).map_err(|e| e.to_string())?;
        file.sync_all().map_err(|e| e.to_string())?;
    }
    Ok(())
}
