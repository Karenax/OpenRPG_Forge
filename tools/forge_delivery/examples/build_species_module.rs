//! Private qualification fixture from a pinned installed regression module and an existing kit.
//! It preserves imported populations when composing their selected species.
//! Construction alone does not qualify Studio export, rendering or public rights.
use forge_assets::{
    GovernedAssetKitBuildReport, VegetationPrototypeCatalog, asset_browser::AssetBrowser,
    governed_import::*,
};
use forge_core::{CommandId, bytes_sha256, canonical_json_bytes, canonical_json_sha256};
use forge_delivery::{
    manifest::{Descriptor, Kind, Rights},
    module_runtime::LoadedModuleZone,
    orpmod,
};
use forge_project::{
    module_package::*,
    zone_authoring::{session::ZoneEditSession, species::SpeciesReference},
};
use std::{
    fs,
    path::{Path, PathBuf},
};

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let args = std::env::args_os()
        .skip(1)
        .map(PathBuf::from)
        .collect::<Vec<_>>();
    if !(3..=4).contains(&args.len())
        || args[..3].iter().any(|path| !path.is_absolute())
        || args[2].exists()
    {
        return Err(
            "Expected absolute admitted module directory, kit directory, new output directory and optional area ID"
                .into(),
        );
    }
    for path in &args[..3] {
        forge_delivery::manifest::safe_chain(path)?;
    }
    let manifest = ModulePackageManifest::from_bytes(&fs::read(args[0].join("module.json"))?)?;
    let zone_index = if let Some(area) = args.get(3) {
        manifest
            .zones
            .iter()
            .position(|zone| zone.area_id.to_string() == area.to_string_lossy())
            .ok_or("Selected area is absent")?
    } else if manifest.zones.len() == 1 {
        0
    } else {
        return Err("A multi-zone fixture requires an explicit area ID".into());
    };
    if manifest.vegetation.iter().any(|binding| {
        binding
            .sources
            .iter()
            .any(|source| matches!(source, ModuleVegetationSource::GovernedSpecies { .. }))
    }) {
        return Err("Fixture requires a module without existing species companions".into());
    }
    let loaded =
        forge_delivery::module_runtime::load(&args[0], manifest.zones[zone_index].area_id, None)?;
    fs::create_dir(&args[2])?;
    let source = args[2].join("source");
    fs::create_dir(&source)?;
    let kit_bytes = bounded(&args[1].join("build.json"), 1024 * 1024)?;
    let kit: GovernedAssetKitBuildReport = serde_json::from_slice(&kit_bytes)?;
    kit.validate()?;
    let catalog: VegetationPrototypeCatalog = serde_json::from_slice(&bounded(
        &args[1].join("prototype-catalog.json"),
        1024 * 1024,
    )?)?;
    let mut module = loaded.manifest.clone();
    for file in &module.files {
        let path = source.join(&file.path);
        if let Some(parent) = path.parent() {
            fs::create_dir_all(parent)?;
        }
        // These inputs stay immutable. `replace` unlinks its destination before
        // writing any changed archive/catalog, so no shared source is mutated.
        let original = loaded.package_root.join(&file.path);
        if fs::hard_link(&original, &path).is_err() {
            fs::copy(&original, &path)?;
        }
    }
    let (zone, binding) = prepare_zone(&loaded, &catalog, bytes_sha256(&kit_bytes))?;
    let mut governed: GovernedImportCatalog =
        serde_json::from_slice(&loaded.read_payload(&module.asset_catalog_path, 1024 * 1024)?)?;
    add_models(
        &args[1],
        &source,
        &mut module,
        &mut governed,
        &kit,
        &catalog,
    )?;
    replace(
        &source,
        &mut module,
        "plants/catalog.json",
        &canonical_json_bytes(&catalog)?,
        ModuleFileRole::Vegetation,
    )?;
    replace(
        &source,
        &mut module,
        "plants/kit.json",
        &kit_bytes,
        ModuleFileRole::Vegetation,
    )?;
    let archive = zone.archive_bytes()?;
    let archive_path = module.zones[zone_index].archive_path.clone();
    replace(
        &source,
        &mut module,
        &archive_path,
        &archive,
        ModuleFileRole::ZoneArchive,
    )?;
    module.zones[zone_index].archive_sha256 = bytes_sha256(&archive);
    module.zones[zone_index].head_hash = zone.head().content_hash()?;
    module.zones[zone_index].gameplay_sha256 = zone
        .head()
        .corrections
        .gameplay
        .as_deref()
        .map(canonical_json_sha256)
        .transpose()?;
    let catalog_path = module.asset_catalog_path.clone();
    replace(
        &source,
        &mut module,
        &catalog_path,
        &serde_json::to_vec(&governed)?,
        ModuleFileRole::AssetCatalog,
    )?;
    module
        .vegetation
        .retain(|existing| existing.area_id != binding.area_id);
    module.vegetation.push(binding);
    module.compatibility.module_api_version = module
        .compatibility
        .module_api_version
        .max(MODULE_VEGETATION_API_VERSION);
    module.module_version = "1.0.1".into();
    module.provenance.source =
        "Private Gate 7 native species replay; original regression plus existing governed kit"
            .into();
    module.provenance.source_revision = "gate7-installed-species-v1".into();
    module.provenance.rights = ModuleRights::PrivateLocalOnly;
    module
        .provenance
        .license_paths
        .push("licenses/species.txt".into());
    fs::write(source.join("module.json"), module.to_bytes()?)?;
    let package = args[2].join("species.orpmod");
    let (_, transport) = orpmod::pack(
        &source,
        Descriptor {
            schema: "forge.delivery.package.v1".into(),
            id: module.module_id.to_string(),
            label: module.name.clone(),
            version: module.module_version.clone(),
            kind: Kind::Module,
            platform: "any".into(),
            source_revision: module.provenance.source_revision.clone(),
            rights: Rights::PrivateLocalOnly,
            license_paths: module.provenance.license_paths.clone(),
            files: vec![],
            product: None,
        },
        &package,
        false,
    )?;
    println!(
        "{}",
        serde_json::json!({"package": package, "package_sha256": forge_delivery::file_hash(&package)?,
        "transport_manifest_sha256": transport, "area_id": module.zones[zone_index].area_id,
        "archive_sha256": module.zones[zone_index].archive_sha256,
        "head_hash": module.zones[zone_index].head_hash, "private_only": true})
    );
    Ok(())
}

fn prepare_zone(
    module: &LoadedModuleZone,
    catalog: &VegetationPrototypeCatalog,
    kit_hash: forge_core::ContentHash,
) -> Result<(ZoneEditSession, ModuleVegetationBinding), Box<dyn std::error::Error>> {
    let mut zone = ZoneEditSession::from_archive_bytes(&module.archive)?;
    if zone
        .head()
        .effective()?
        .1
        .as_ref()
        .is_some_and(|v| v.imported_source.is_some())
    {
        return prepare_imported_zone(zone, module, catalog, kit_hash);
    }
    let mut base = zone.head().base.clone();
    let mut vegetation = forge_world::editor_vegetation::prepare(&base.terrain, 17)?;
    vegetation.cells_per_chunk_axis = 2;
    vegetation
        .layers
        .retain(|layer| layer.kind == forge_project::VegetationKind::Tree);
    let layer = vegetation
        .layers
        .first_mut()
        .ok_or("Tree fixture layer absent")?;
    layer.material_filter = None;
    layer.density_mask = None;
    layer.attempts_per_cell = 1;
    layer.minimum_spacing_m = 6.0;
    layer.collision = Some(forge_project::VegetationCollisionProfile {
        shape: forge_project::VegetationCollisionShape::Cylinder,
        radius_m: 0.25,
        height_m: 2.8,
        activation_distance_m: 48.0,
    });
    let layer_id = layer.id.clone();
    base.vegetation = Some(vegetation);
    let request = zone
        .head()
        .prepare_regeneration(base, CommandId::deterministic("gate7/species/base"))?;
    zone.execute(&request)?;
    let hash = canonical_json_sha256(catalog)?;
    let references = catalog
        .bindings
        .iter()
        .map(|binding| SpeciesReference {
            catalog_sha256: hash,
            source_layer_id: forge_project::VegetationLayerId::new(binding.layer_id.clone()),
            asset_id: binding.asset_id.clone(),
        })
        .collect();
    let request = zone.head().prepare_species(
        layer_id,
        Some(references),
        CommandId::deterministic("gate7/species/selection"),
    )?;
    zone.execute(&request)?;
    let binding = ModuleVegetationBinding {
        area_id: zone.head().base.terrain.area_id,
        vegetation_sha256: zone
            .head()
            .effective()?
            .1
            .ok_or("Vegetation absent")?
            .content_hash()?,
        sources: vec![ModuleVegetationSource::GovernedSpecies {
            catalogs: vec![ModuleFilePin {
                path: "plants/catalog.json".into(),
                sha256: hash,
            }],
            asset_kit: ModuleFilePin {
                path: "plants/kit.json".into(),
                sha256: kit_hash,
            },
        }],
    };
    Ok((zone, binding))
}

fn prepare_imported_zone(
    mut zone: ZoneEditSession,
    module: &LoadedModuleZone,
    catalog: &VegetationPrototypeCatalog,
    kit_hash: forge_core::ContentHash,
) -> Result<(ZoneEditSession, ModuleVegetationBinding), Box<dyn std::error::Error>> {
    let hash = canonical_json_sha256(catalog)?;
    let (_, vegetation) = zone.head().effective()?;
    let vegetation = vegetation.ok_or("Imported vegetation absent")?;
    let tree_layers = vegetation
        .layers
        .iter()
        .filter(|layer| layer.kind == forge_project::VegetationKind::Tree)
        .map(|layer| layer.id.clone())
        .collect::<Vec<_>>();
    if tree_layers.is_empty() {
        return Err("Imported fixture has no tree layers".into());
    }
    for layer in tree_layers {
        let references = catalog
            .bindings
            .iter()
            .map(|binding| SpeciesReference {
                catalog_sha256: hash,
                source_layer_id: forge_project::VegetationLayerId::new(binding.layer_id.clone()),
                asset_id: binding.asset_id.clone(),
            })
            .collect();
        let request = zone.head().prepare_species(
            layer.clone(),
            Some(references),
            CommandId::deterministic(format!("gate7/imported-species/{layer}")),
        )?;
        zone.execute(&request)?;
    }
    let mut binding = module
        .manifest
        .vegetation
        .iter()
        .find(|binding| binding.area_id == zone.head().base.terrain.area_id)
        .ok_or("Imported module companion absent")?
        .clone();
    binding.vegetation_sha256 = zone
        .head()
        .effective()?
        .1
        .ok_or("Vegetation absent")?
        .content_hash()?;
    binding
        .sources
        .push(ModuleVegetationSource::GovernedSpecies {
            catalogs: vec![ModuleFilePin {
                path: "plants/catalog.json".into(),
                sha256: hash,
            }],
            asset_kit: ModuleFilePin {
                path: "plants/kit.json".into(),
                sha256: kit_hash,
            },
        });
    Ok((zone, binding))
}

fn add_models(
    kit_root: &Path,
    source: &Path,
    module: &mut ModulePackageManifest,
    governed: &mut GovernedImportCatalog,
    kit: &GovernedAssetKitBuildReport,
    catalog: &VegetationPrototypeCatalog,
) -> Result<(), Box<dyn std::error::Error>> {
    let browser = AssetBrowser::new(kit.clone())?;
    let notice = serde_json::to_vec_pretty(&kit.provenance)?;
    replace(
        source,
        module,
        "licenses/species.txt",
        &notice,
        ModuleFileRole::License,
    )?;
    let mut seen = std::collections::BTreeSet::new();
    for binding in &catalog.bindings {
        if !seen.insert(&binding.asset_id) {
            continue;
        }
        let (id, pin) = browser.reference(&binding.asset_id)?;
        let asset = browser.resolve(id, pin)?;
        forge_delivery::manifest::relative(&asset.glb.path)?;
        let bytes = bounded(&kit_root.join(&asset.glb.path), 64 * 1024 * 1024)?;
        if bytes_sha256(&bytes) != pin || pin != binding.glb_sha256 {
            return Err("Source model differs from catalog".into());
        }
        let path = format!("plants/{pin}.glb");
        replace(source, module, &path, &bytes, ModuleFileRole::Asset)?;
        governed.assets.push(GovernedImportAsset {
            id,
            name: binding.asset_id.clone(),
            kind: GovernedAssetKind::Mesh,
            payload: GovernedAssetFile {
                path: path.clone(),
                bytes: bytes.len() as u64,
                sha256: pin,
            },
            provenance: AssetProvenance {
                source: "Existing governed kit; exact report embedded as plants/kit.json".into(),
                author: "OpenRPG Forge project".into(),
                license: "LicenseRef-OpenRPG-Forge-Project-Generated".into(),
                evidence: "licenses/species.txt".into(),
                acquired_on: "2026-10-24".into(),
                rights: AssetRights {
                    local_use: true,
                    redistribution: RedistributionRights::Forbidden,
                    redistribution_evidence: None,
                },
            },
            derived_from: None,
            transformations: vec![],
            authored_changes: false,
            durable_source: None,
            thumbnail: None,
        });
        module.assets.push(ModuleAsset {
            asset_id: id,
            file_path: path,
            sha256: pin,
            dependencies: vec![],
        });
    }
    Ok(())
}

fn bounded(path: &Path, maximum: usize) -> Result<Vec<u8>, Box<dyn std::error::Error>> {
    use std::io::Read;
    forge_delivery::manifest::safe_chain(path)?;
    let file = fs::File::open(path)?;
    if !file.metadata()?.is_file() || file.metadata()?.len() > maximum as u64 {
        return Err("Fixture input is oversized or not a file".into());
    }
    let mut bytes = Vec::new();
    file.take(maximum as u64 + 1).read_to_end(&mut bytes)?;
    if bytes.len() > maximum {
        return Err("Fixture input changed size".into());
    }
    Ok(bytes)
}

fn replace(
    source: &Path,
    module: &mut ModulePackageManifest,
    path: &str,
    bytes: &[u8],
    role: ModuleFileRole,
) -> Result<(), Box<dyn std::error::Error>> {
    forge_delivery::manifest::relative(path)?;
    let destination = source.join(path);
    if let Some(parent) = destination.parent() {
        fs::create_dir_all(parent)?;
    }
    // The fresh fixture may share immutable inputs by hard link. Replacing the
    // link first gives every mutation its own physical file.
    if destination.try_exists()? {
        fs::remove_file(&destination)?;
    }
    fs::write(destination, bytes)?;
    module.files.retain(|file| file.path != path);
    module.files.push(ModuleFile {
        path: path.into(),
        bytes: bytes.len() as u64,
        sha256: bytes_sha256(bytes),
        role,
    });
    Ok(())
}
