//! Private qualification modules with original pinned resources.
//! Composition/transport checks do not qualify Studio export or native rendering.
#[path = "build_imported_module/source_world.rs"]
mod source_world;
use forge_assets::source_vegetation_package::{PlantPackage, profile::PARAMETERS};
use forge_core::{CommandId, ContentHash, bytes_sha256};
use forge_delivery::{
    manifest::{Descriptor, Kind, Rights},
    orpmod,
};
use forge_project::{
    imported_vegetation::{
        ImportedInstanceEncoding, ImportedVegetationBlock, ImportedVegetationSource,
    },
    module_package::*,
    zone_authoring::session::ZoneEditSession,
};
use std::{
    collections::BTreeMap,
    fs,
    io::Read,
    path::{Path, PathBuf},
};

fn bounded(path: &Path, maximum: usize) -> Result<Vec<u8>, String> {
    forge_delivery::manifest::safe_chain(path)?;
    let file = fs::File::open(path).map_err(|e| e.to_string())?;
    if !file.metadata().map_err(|e| e.to_string())?.is_file() {
        return Err("Expected a regular fixture input".into());
    }
    let mut bytes = Vec::new();
    file.take(maximum as u64 + 1)
        .read_to_end(&mut bytes)
        .map_err(|e| e.to_string())?;
    if bytes.len() > maximum {
        return Err("Fixture input exceeds its bound".into());
    }
    Ok(bytes)
}

fn put(
    root: &Path,
    module: &mut ModulePackageManifest,
    path: &str,
    bytes: &[u8],
    role: ModuleFileRole,
) -> Result<ModuleFilePin, String> {
    forge_delivery::manifest::relative(path)?;
    let target = root.join(path);
    if let Some(parent) = target.parent() {
        fs::create_dir_all(parent).map_err(|e| e.to_string())?;
    }
    fs::write(target, bytes).map_err(|e| e.to_string())?;
    let sha256 = bytes_sha256(bytes);
    module.files.retain(|file| file.path != path);
    module.files.push(ModuleFile {
        path: path.into(),
        bytes: bytes.len() as u64,
        sha256,
        role,
    });
    Ok(ModuleFilePin {
        path: path.into(),
        sha256,
    })
}

fn prepare_zone(
    loaded: &forge_delivery::module_runtime::LoadedModuleZone,
    world: bool,
) -> Result<(ZoneEditSession, Vec<u8>), String> {
    let mut zone = ZoneEditSession::from_archive_bytes(&loaded.archive)?;
    let mut base = zone.head().base.clone();
    if base.terrain.sampled_source.is_some() != world
        || base.terrain.height_sampling.amplitude_m.to_bits() != 0
        || base.terrain.height_sampling.base_elevation_m.to_bits() != 0
    {
        return Err(
            "The fixture requires a flat regression: procedural for transport, raster for --world"
                .into(),
        );
    }
    let mut vegetation = if world {
        world_vegetation(&base.terrain)
    } else {
        forge_world::editor_vegetation::prepare(&base.terrain, 17)?
    };
    vegetation
        .layers
        .retain(|layer| layer.kind == forge_project::VegetationKind::Tree);
    vegetation.layers.truncate(1);
    let layer = vegetation
        .layers
        .first_mut()
        .ok_or("Missing fixture tree layer")?;
    layer.material_filter = None;
    layer.density_mask = None;
    layer.collision = Some(forge_project::VegetationCollisionProfile {
        shape: forge_project::VegetationCollisionShape::Cylinder,
        radius_m: 0.5,
        height_m: 3.0,
        activation_distance_m: 48.0,
    });
    let mut rows = BTreeMap::new();
    for index in 0_u32..12 {
        let x = f64::from(index % 4) * 10.0 - 15.0 + if world { 256.0 } else { 0.0 };
        let z = -12.0 - f64::from(index / 4) * 12.0;
        let key = bytes_sha256(format!("gate7/private-imported-tree/{index}").as_bytes());
        rows.insert(
            key,
            [
                x,
                0.0,
                z,
                0.0,
                0.0,
                0.0,
                1.0,
                1.0,
                1.0,
                1.0,
                0.0,
                if world { 1.0 } else { 0.0 },
                1.0,
                0.25,
                1.0,
                f64::from(index),
            ],
        );
    }
    let mut blob = Vec::new();
    for (key, values) in rows {
        blob.extend_from_slice(key.as_bytes());
        for value in values {
            blob.extend_from_slice(&value.to_le_bytes());
        }
    }
    let mut names: Vec<String> = PARAMETERS.iter().map(|name| (*name).into()).collect();
    names.push("source_order".into());
    vegetation.schema_version = 2;
    vegetation.imported_source = Some(ImportedVegetationSource {
        grass: None,
        schema: "forge.imported-vegetation".into(),
        version: 1,
        blocks: vec![ImportedVegetationBlock {
            layer_id: layer.id.to_string(),
            prototype_key: "tidewater_trees".into(),
            count: 12,
            encoding: ImportedInstanceEncoding::TransformF64LeV1,
            instances_sha256: bytes_sha256(&blob),
            parameter_names: names,
        }],
    });
    base.vegetation = Some(vegetation);
    zone.execute(&zone.head().prepare_regeneration(
        base,
        CommandId::deterministic("gate7/private-imported-base"),
    )?)?;
    if world {
        zone.execute(&zone.head().prepare_start(
            Some(forge_project::zone_authoring::start::ZoneStart {
                x_m: 256.0,
                z_m: 3.0,
                facing_degrees: Some(0.0),
            }),
            CommandId::deterministic("gate7/private-imported-world-start"),
        )?)?;
    }
    Ok((zone, blob))
}

fn world_vegetation(terrain: &forge_project::TerrainDocument) -> forge_project::VegetationDocument {
    use forge_project::{
        VegetationDocument, VegetationExtent, VegetationKind, VegetationLayer,
        VegetationPresentationProfile,
    };
    // Construct the imported document before validation; the procedural v1
    // generator intentionally supports only its historical 512 m extent.
    VegetationDocument::new(
        terrain.project_id,
        terrain.area_id,
        terrain.revision,
        "Private raster plant qualification",
        17,
        VegetationExtent::new(terrain.extent_m.size_x_m, terrain.extent_m.size_z_m),
        8,
        vec![VegetationLayer::new(
            "editor_trees",
            "Arbres",
            VegetationKind::Tree,
            0.35,
            2,
            5.0,
            0.9,
            1.4,
            0.65,
            -4096.0,
            4096.0,
            None,
            None,
            0.35,
        )],
        vec![],
        VegetationPresentationProfile {
            near_lod_distance_m: 24.0,
            medium_lod_distance_m: 72.0,
            hlod_distance_m: 160.0,
            cull_distance_m: 320.0,
            shadow_distance_m: 96.0,
            wind_strength: 0.5,
            wind_speed_hz: 0.18,
            interaction_radius_m: 2.0,
            interaction_strength: 0.6,
            grass_blades_per_patch: 24,
        },
    )
}

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let args: Vec<_> = std::env::args().skip(1).collect();
    let (inputs, world, source_world, include_grass) = match args.as_slice() {
        [_, _, _, _] => (args.as_slice(), false, None, false),
        [_, _, _, _, flag] if flag == "--world" => (&args[..4], true, None, false),
        [_, _, _, _, flag, source] if flag == "--source-world" =>
            (&args[..4], true, Some(Path::new(source)), false),
        [_, _, _, _, flag, source, grass] if flag == "--source-world" && grass == "--include-source-grass" =>
            (&args[..4], true, Some(Path::new(source)), true),
        _ => return Err("Expected BASE_MODULE_DIRECTORY SOURCE_PACKAGE_ROOT NEW_OUTPUT_DIRECTORY MANIFEST_SHA256 [--world | --source-world ABSOLUTE_SOURCE_DIRECTORY [--include-source-grass]]".into()),
    };
    let [base, resources, output, pin] = inputs else {
        return Err("Expected BASE_MODULE_DIRECTORY SOURCE_PACKAGE_ROOT NEW_OUTPUT_DIRECTORY MANIFEST_SHA256".into());
    };
    let paths = [
        PathBuf::from(base),
        PathBuf::from(resources),
        PathBuf::from(output),
    ];
    if paths.iter().any(|path| !path.is_absolute()) || paths[2].exists() {
        return Err("Expected absolute inputs and a new output directory".into());
    }
    for path in &paths {
        forge_delivery::manifest::safe_chain(path)?;
    }
    let expected: ContentHash = pin.parse()?;
    let raw = bounded(&paths[1].join("package.json"), 256 * 1024)?;
    let plants = PlantPackage::load(&raw, expected, |name, maximum| {
        bounded(&paths[1].join(name), maximum)
    })?;
    let original = ModulePackageManifest::from_bytes(&bounded(
        &paths[0].join("module.json"),
        MAX_MODULE_MANIFEST_BYTES,
    )?)?;
    if original.zones.len() != 1 || !original.vegetation.is_empty() {
        return Err("Expected one existing regression zone without companions".into());
    }
    let loaded = forge_delivery::module_runtime::load(&paths[0], original.zones[0].area_id, None)?;
    let (zone, blobs, composition) = if let Some(root) = source_world {
        let prepared = source_world::prepare(&loaded, root, include_grass.then_some(expected))?;
        (prepared.zone, prepared.blobs, Some(prepared.report))
    } else {
        let (zone, blob) = prepare_zone(&loaded, world)?;
        (zone, vec![blob], None)
    };
    fs::create_dir(&paths[2])?;
    let source = paths[2].join("source");
    fs::create_dir(&source)?;
    let mut module = loaded.manifest.clone();
    let required_sources = zone.required_source_blobs()?;
    for file in loaded.manifest.files.iter() {
        // A new source-world fork has no flat-regression undo timeline. Other
        // modes retain every immutable source needed by their actual history.
        if file.role == ModuleFileRole::SourceBlob && !required_sources.contains_key(&file.sha256) {
            module.files.retain(|entry| entry.path != file.path);
            continue;
        }
        put(
            &source,
            &mut module,
            &file.path,
            &loaded.read_payload(&file.path, 64 * 1024 * 1024)?,
            file.role,
        )?;
    }
    let index = put(
        &source,
        &mut module,
        "plants/imported/package.json",
        &raw,
        ModuleFileRole::Vegetation,
    )?;
    let mut resource_pins = Vec::new();
    for (name, bytes) in plants.files() {
        let path = format!("plants/imported/{name}");
        let role = if name == "LICENSE-Tidewater.txt" {
            module.provenance.license_paths.push(path.clone());
            ModuleFileRole::License
        } else {
            ModuleFileRole::Vegetation
        };
        resource_pins.push(put(&source, &mut module, &path, bytes, role)?);
    }
    for blob in blobs {
        let hash = bytes_sha256(&blob);
        if required_sources.get(&hash) != Some(&blob.len()) {
            return Err("Composition produced an unreferenced source blob".into());
        }
        put(
            &source,
            &mut module,
            &format!("sources/{hash}.bin"),
            &blob,
            ModuleFileRole::SourceBlob,
        )?;
    }
    if let Some(report) = &composition {
        module
            .provenance
            .proof_paths
            .push("provenance/world-composition.json".into());
        put(
            &source,
            &mut module,
            "provenance/world-composition.json",
            &serde_json::to_vec_pretty(report)?,
            ModuleFileRole::Provenance,
        )?;
    }
    let archive = zone.archive_bytes()?;
    let archive_path = module.zones[0].archive_path.clone();
    put(
        &source,
        &mut module,
        &archive_path,
        &archive,
        ModuleFileRole::ZoneArchive,
    )?;
    module.zones[0].archive_sha256 = bytes_sha256(&archive);
    module.zones[0].head_hash = zone.head().content_hash()?;
    module.zones[0].gameplay_sha256 = zone
        .head()
        .corrections
        .gameplay
        .as_deref()
        .map(forge_core::canonical_json_sha256)
        .transpose()?;
    module.zones[0].source_blobs = zone.required_source_blobs()?.keys().copied().collect();
    module.vegetation = vec![ModuleVegetationBinding {
        area_id: module.zones[0].area_id,
        vegetation_sha256: zone
            .head()
            .effective()?
            .1
            .ok_or("Missing vegetation")?
            .content_hash()?,
        sources: vec![ModuleVegetationSource::ImportedPrototypes {
            index,
            resources: resource_pins,
        }],
    }];
    module.compatibility.module_api_version = MODULE_VEGETATION_API_VERSION;
    module.module_version = if include_grass {
        "1.0.5"
    } else if composition.is_some() {
        "1.0.4"
    } else if world {
        "1.0.3"
    } else {
        "1.0.2"
    }
    .into();
    module.provenance.source="Private Gate 7: existing regression with twelve synthetic authored placements and original pinned Tidewater prototype resources".into();
    module.provenance.source_revision = if world {
        "gate7-private-imported-world-v1"
    } else {
        "gate7-private-imported-prototypes-v1"
    }
    .into();
    module.provenance.rights = ModuleRights::PrivateLocalOnly;
    if composition.is_some() {
        module.name = "Le relais de l’île — qualification monde étendu".into();
        module.provenance.source = "Private Gate 7 qualification fork: verified full source terrain/population, existing regression RPG identities, explicitly relocated camp and authored trunk collision proxies. Source archives remain unchanged; this new fork does not import their undo timelines.".into();
        module.provenance.source_revision = "gate7-source-world-v1".into();
        if include_grass {
            module.provenance.source_revision = "gate7-source-world-grass-v1".into();
        }
    }
    fs::write(source.join("module.json"), module.to_bytes()?)?;
    let package = paths[2].join("imported.orpmod");
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
    let installed = forge_delivery::module_runtime::load(
        &package,
        module.zones[0].area_id,
        Some(&paths[2].join("cache")),
    )?;
    let imported = installed
        .vegetation_imported()?
        .ok_or("Installed imported resources missing")?;
    println!(
        "{}",
        serde_json::json!({"package":package,"package_sha256":forge_delivery::file_hash(&package)?,"transport_manifest_sha256":transport,"source_index_sha256":expected,"area_id":module.zones[0].area_id,"head_hash":module.zones[0].head_hash,"archive_sha256":module.zones[0].archive_sha256,"installed_root":installed.package_root,"source_groups":imported.geometry.groups.len(),"source_levels":imported.geometry.groups.values().map(Vec::len).sum::<usize>(),"grass_levels":imported.geometry.grass.len(),"authored_instances":zone.head().effective()?.1.and_then(|v|v.imported_source).map(|s|s.blocks.iter().map(|b|u64::from(b.count)).sum::<u64>()).unwrap_or(0),"composition":composition,"source_shader_bytes":imported.package.file("plant.wgsl").ok_or("Source shader missing")?.len(),"private_only":true,"native_render_qualified":false,"gate_closed":false})
    );
    Ok(())
}
