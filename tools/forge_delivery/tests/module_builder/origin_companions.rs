//! Explicit private end-to-end fixture; ordinary tests keep no external source dependency.
use super::*;
use forge_core::{AreaId, Revision};
use forge_project::{
    module_package::{MODULE_SOLAR_CLOCK_API_VERSION, ModuleEnvironmentSource},
    workspace::export::{ModuleAreaArchive, ModuleExportSnapshot},
    zone_authoring::session::{ZoneAction, ZoneRequest},
};
use std::{collections::BTreeSet, sync::Arc};

fn input(name: &str) -> PathBuf {
    let path = PathBuf::from(std::env::var_os(name).expect("Explicit private qualification input"));
    assert!(path.is_absolute());
    forge_delivery::manifest::safe_chain(&path).unwrap();
    path
}

fn assets(
    loaded: &module_runtime::LoadedModuleZone,
) -> (GovernedImportCatalog, BTreeMap<String, Vec<u8>>) {
    let raw = loaded
        .read_payload(&loaded.manifest.asset_catalog_path, 4 * 1024 * 1024)
        .unwrap();
    let catalog: GovernedImportCatalog = serde_json::from_slice(&raw).unwrap();
    let mut paths = BTreeSet::new();
    for asset in &catalog.assets {
        paths.insert(asset.payload.path.clone());
        paths.extend(asset.thumbnail.as_ref().map(|p| p.path.clone()));
        if let Some(
            DurableAssetSource::AuthoredFile { file } | DurableAssetSource::Recipe { file },
        ) = &asset.durable_source
        {
            paths.insert(file.path.clone());
        }
        paths.insert(asset.provenance.evidence.clone());
        paths.extend(
            asset
                .provenance
                .rights
                .redistribution_evidence
                .iter()
                .cloned(),
        );
    }
    let payloads = paths
        .into_iter()
        .map(|path| {
            let bytes = loaded.read_payload(&path, 64 * 1024 * 1024).unwrap();
            (path, bytes)
        })
        .collect();
    (catalog, payloads)
}

fn assert_same_pins(
    original: &module_runtime::LoadedModuleZone,
    exported: &module_runtime::LoadedModuleZone,
) {
    let area = original.zone.head().base.terrain.area_id;
    let old = original
        .manifest
        .environments
        .iter()
        .find(|b| b.area_id == area)
        .unwrap();
    let new = exported
        .manifest
        .environments
        .iter()
        .find(|b| b.area_id == area)
        .unwrap();
    assert_eq!(old, new);
    let ModuleEnvironmentSource::SourceProfile {
        environment,
        programs,
    } = &old.source;
    let mut paths: Vec<_> = std::iter::once(environment)
        .chain(programs)
        .flat_map(|p| std::iter::once(&p.manifest).chain(&p.resources))
        .collect();
    for light in &original.manifest.local_light_packages {
        paths.extend(std::iter::once(&light.manifest).chain(&light.resources));
    }
    for pin in paths {
        assert_eq!(
            exported.read_payload(&pin.path, 64 * 1024 * 1024).unwrap(),
            original.read_payload(&pin.path, 64 * 1024 * 1024).unwrap()
        );
    }
    assert_eq!(
        original.manifest.vegetation[0].sources,
        exported.manifest.vegetation[0].sources
    );
    assert_ne!(
        original.manifest.vegetation[0].vegetation_sha256,
        exported.manifest.vegetation[0].vegetation_sha256
    );
    assert_eq!(original.zone.head().base, exported.zone.head().base);
    assert_eq!(
        original.manifest.local_light_packages,
        exported.manifest.local_light_packages
    );
}

#[test]
#[ignore = "Requires explicit private origin root/package and a fresh persistent export destination"]
fn private_origin_keeps_all_source_companions_on_a_retouched_secondary_area() {
    let root = input("FORGE_EXPORT_ORIGIN_ROOT");
    let package = input("FORGE_EXPORT_ORIGIN_PACKAGE");
    let output = input("FORGE_EXPORT_TEST_OUTPUT");
    assert!(!output.exists());
    fs::create_dir(&output).unwrap();
    let admitted = orpmod::validate_directory(&root, false).unwrap();
    assert_eq!(
        admitted.compatibility.module_api_version,
        MODULE_SOLAR_CLOCK_API_VERSION
    );
    assert_eq!(admitted.environments.len(), 1);
    assert_eq!(admitted.vegetation.len(), 1);
    assert_eq!(admitted.local_light_packages.len(), 1);
    let area = admitted.zones[0].area_id;
    let original = module_runtime::load(&root, area, None).unwrap();
    let (catalog, payloads) = assets(&original);
    let mut reviewed = ZoneEditSession::from_archive_bytes(&original.archive).unwrap();
    let mut corrections = reviewed.head().corrections.clone();
    let carrier = corrections
        .placements
        .as_mut()
        .unwrap()
        .iter_mut()
        .find(|p| p.local_lights.is_some())
        .unwrap();
    carrier.transform.translation_m.x += 0.5;
    reviewed
        .execute(&ZoneRequest {
            command_id: CommandId::deterministic("origin-companions/carrier-retouch"),
            expected_revision: reviewed.head().revision,
            expected_hash: reviewed.head().content_hash().unwrap(),
            action: ZoneAction::edit(corrections),
        })
        .unwrap();
    let mut terrain = reviewed.head().base.terrain.clone();
    terrain.revision = Revision::INITIAL;
    terrain.area_id = AreaId::deterministic("origin-companions/new-primary");
    let primary_area = terrain.area_id;
    let mut primary = ZoneEditSession::from_initial_terrain(terrain).unwrap();
    let mut gameplay = reviewed
        .head()
        .corrections
        .gameplay
        .as_deref()
        .unwrap()
        .clone();
    gameplay.area_id = primary_area;
    let request = primary
        .head()
        .prepare_gameplay(
            Some(gameplay),
            CommandId::deterministic("origin-companions/new-primary-gameplay"),
        )
        .unwrap();
    primary.execute(&request).unwrap();
    let primary_head = primary.head().content_hash().unwrap();
    let snapshot = ModuleExportSnapshot {
        module_id: admitted.module_id,
        project_id: admitted.project_id,
        primary_area,
        primary_head,
        workspace_hash: None,
        creator: admitted.creator.clone(),
        zones: vec![
            ModuleAreaArchive {
                area_id: primary_area,
                head_hash: primary_head,
                bytes: primary.archive_bytes().unwrap(),
            },
            ModuleAreaArchive {
                area_id: area,
                head_hash: reviewed.head().content_hash().unwrap(),
                bytes: reviewed.archive_bytes().unwrap(),
            },
        ],
        source_blobs: original.source_blobs.clone(),
    };
    let bytes: Arc<[u8]> = fs::read(&package).unwrap().into();
    assert!(bytes.len() as u64 <= forge_project::workspace::MAX_MODULE_ORIGIN_PACKAGE_BYTES);
    let origin = ExportOrigin {
        expected_package_sha256: bytes_sha256(&bytes),
        package_bytes: bytes,
    };
    let mut publish = Fixture::metadata();
    publish.rights = ModuleRights::Redistributable;
    let error = build_module_with_origin(
        &output.join("private-to-public"),
        &snapshot,
        publish,
        catalog.clone(),
        payloads.clone(),
        Some(origin.clone()),
    )
    .unwrap_err();
    assert!(error.contains("private/local origin"), "{error}");
    assert!(!output.join("private-to-public").exists());
    let binding = &admitted.environments[0];
    let ModuleEnvironmentSource::SourceProfile { environment, .. } = &binding.source;
    let mut conflict = payloads.clone();
    conflict.insert(
        environment.manifest.path.clone(),
        b"changed source environment".to_vec(),
    );
    assert!(
        build_module_with_origin(
            &output.join("conflicting-companion"),
            &snapshot,
            Fixture::metadata(),
            catalog.clone(),
            conflict,
            Some(origin.clone())
        )
        .is_err()
    );
    assert!(!output.join("conflicting-companion").exists());
    let exported = build_module_with_origin(
        &output.join("exports"),
        &snapshot,
        Fixture::metadata(),
        catalog,
        payloads,
        Some(origin),
    )
    .unwrap();
    let loaded = module_runtime::load(&exported.source_root, area, None).unwrap();
    assert_eq!(
        loaded.manifest.compatibility.module_api_version,
        MODULE_SOLAR_CLOCK_API_VERSION
    );
    assert_eq!(loaded.manifest.zones.len(), 2);
    assert_eq!(loaded.zone.head(), reviewed.head());
    assert_eq!(loaded.manifest.environments.len(), 1);
    assert_eq!(loaded.manifest.environments[0].area_id, area);
    assert_same_pins(&original, &loaded);
    assert!(loaded.environment().unwrap().unwrap().programs.is_some());
    assert!(loaded.vegetation_imported().unwrap().is_some());
    assert_eq!(loaded.local_lights().unwrap().len(), 1);
    assert_eq!(
        orpmod::inspect(&exported.package_path, false).unwrap(),
        loaded.manifest
    );
    let metadata = serde_json::json!({"schema":"forge.gate7.origin-companions-export-proof.v1",
        "exported":exported,"retouched_area":area,"retouched_head":reviewed.head().content_hash().unwrap(),
        "original_package":package,"original_package_sha256":forge_delivery::file_hash(&package).unwrap(),
        "original_head":admitted.zones[0].head_hash,"negative_private_publication_refused":true,
        "negative_companion_conflict_refused":true,"primary_has_environment":false,"secondary_has_environment":true,
        "source_companions_preserved":true,"native_gpu_qualified":false,"studio_ui_qualified":false,"gate_closed":false});
    fs::write(
        output.join("export-proof.json"),
        serde_json::to_vec_pretty(&metadata).unwrap(),
    )
    .unwrap();
    println!("{}", serde_json::to_string(&metadata).unwrap());
}

#[test]
#[ignore = "Requires a pinned private installed-module origin and a fresh output"]
fn private_origin_scene_wind_preserves_real_module_companions() {
    use forge_project::{
        module_package::MODULE_SCENE_WIND_API_VERSION, zone_authoring::wind::SceneWindSettings,
    };
    let root = input("FORGE_WIND_ORIGIN_ROOT");
    let package = input("FORGE_WIND_ORIGIN_PACKAGE");
    let output = input("FORGE_WIND_TEST_OUTPUT");
    let existing = std::env::var_os("FORGE_WIND_EXISTING_EXPORT").map(PathBuf::from);
    assert_eq!(output.exists(), existing.is_some());
    let expected_package = std::env::var("FORGE_WIND_ORIGIN_SHA256").unwrap();
    let expected_manifest = std::env::var("FORGE_WIND_MANIFEST_SHA256").unwrap();
    assert_eq!(
        forge_delivery::file_hash(&package).unwrap().to_string(),
        expected_package
    );
    assert_eq!(
        forge_delivery::file_hash(&root.join("module.json"))
            .unwrap()
            .to_string(),
        expected_manifest
    );
    let admitted = orpmod::validate_directory(&root, false).unwrap();
    assert_eq!(orpmod::inspect(&package, false).unwrap(), admitted);
    assert_eq!(admitted.zones.len(), 2);
    let area = admitted.terrain_surfaces[0].area_id;
    let original = module_runtime::load(&root, area, None).unwrap();
    assert!(original.environment().unwrap().is_some());
    assert!(original.vegetation_imported().unwrap().is_some());
    assert!(!admitted.terrain_surfaces.is_empty());
    assert!(!admitted.objects.is_empty());
    let (catalog, payloads) = assets(&original);
    let mut reviewed = ZoneEditSession::from_archive_bytes(&original.archive).unwrap();
    let wind = SceneWindSettings {
        schema_version: 1,
        heading_degrees: 90.0,
        speed_mps: 7.0,
    };
    let edit = reviewed
        .head()
        .prepare_wind(
            Some(wind),
            CommandId::deterministic("gate7/real-module-scene-wind"),
        )
        .unwrap();
    reviewed.execute(&edit).unwrap();
    let head = reviewed.head().content_hash().unwrap();
    let archive = reviewed.archive_bytes().unwrap();
    let old_vegetation = original.zone.head().effective().unwrap().1.unwrap();
    let mut new_vegetation = reviewed.head().effective().unwrap().1.unwrap();
    let vegetation_hash = new_vegetation.content_hash().unwrap();
    new_vegetation.revision = old_vegetation.revision;
    assert!(
        new_vegetation == old_vegetation,
        "Wind changed vegetation beyond the shared zone revision"
    );
    let primary_area = admitted.zones[0].area_id;
    assert_ne!(
        primary_area, area,
        "This fixture exercises the secondary area"
    );
    let mut source_blobs = original.source_blobs.clone();
    let mut zones = Vec::new();
    for binding in &admitted.zones {
        if binding.area_id == area {
            zones.push(ModuleAreaArchive {
                area_id: area,
                head_hash: head,
                bytes: archive.clone(),
            });
        } else {
            let other = module_runtime::load(&root, binding.area_id, None).unwrap();
            for (hash, bytes) in other.source_blobs {
                if let Some(previous) = source_blobs.insert(hash, bytes.clone()) {
                    assert_eq!(previous, bytes);
                }
            }
            zones.push(ModuleAreaArchive {
                area_id: binding.area_id,
                head_hash: other.zone.head().content_hash().unwrap(),
                bytes: other.archive,
            });
        }
    }
    let primary_head = zones
        .iter()
        .find(|zone| zone.area_id == primary_area)
        .unwrap()
        .head_hash;
    let snapshot = ModuleExportSnapshot {
        module_id: admitted.module_id,
        project_id: admitted.project_id,
        primary_area,
        primary_head,
        workspace_hash: None,
        creator: admitted.creator.clone(),
        zones,
        source_blobs,
    };
    let origin: Arc<[u8]> = fs::read(&package).unwrap().into();
    let origin = ExportOrigin {
        expected_package_sha256: bytes_sha256(&origin),
        package_bytes: origin,
    };
    let resumed = existing.is_some();
    let exported = if let Some(directory) = existing {
        forge_delivery::manifest::safe_chain(&directory).unwrap();
        assert_eq!(directory.parent(), Some(output.join("exports").as_path()));
        let source_root = directory.join("source");
        let package_path = directory.join("module.orpmod");
        let module = orpmod::validate_directory(&source_root, false).unwrap();
        assert_eq!(orpmod::inspect(&package_path, false).unwrap(), module);
        let (_, transport_manifest_sha256) =
            forge_delivery::archive::inspect(&package_path).unwrap();
        ExportedModule {
            module_hash: None,
            module_id: module.module_id,
            area_id: primary_area,
            head_hash: primary_head,
            package_sha256: forge_core::ContentHash::parse(
                &forge_delivery::file_hash(&package_path).unwrap(),
            )
            .unwrap(),
            package_path,
            source_root,
            transport_manifest_sha256,
        }
    } else {
        fs::create_dir(&output).unwrap();
        build_module_with_origin(
            &output.join("exports"),
            &snapshot,
            Fixture::metadata(),
            catalog,
            payloads,
            Some(origin),
        )
        .unwrap()
    };
    let loaded = module_runtime::load(&exported.source_root, area, None).unwrap();
    assert_eq!(loaded.zone.head(), reviewed.head());
    assert_eq!(loaded.archive, archive);
    assert_eq!(loaded.manifest.zones.len(), admitted.zones.len());
    assert_eq!(
        loaded.manifest.compatibility.module_api_version,
        MODULE_SCENE_WIND_API_VERSION
    );
    assert!(
        loaded.manifest.environments == admitted.environments,
        "Environment bindings changed"
    );
    assert_eq!(loaded.manifest.vegetation.len(), admitted.vegetation.len());
    for (next, prior) in loaded.manifest.vegetation.iter().zip(&admitted.vegetation) {
        assert_eq!(next.area_id, prior.area_id);
        assert!(
            next.sources == prior.sources,
            "Vegetation source pins changed"
        );
        assert_eq!(next.vegetation_sha256, vegetation_hash);
    }
    assert!(
        loaded.manifest.objects == admitted.objects,
        "Object bindings changed"
    );
    assert!(
        loaded.manifest.terrain_surfaces == admitted.terrain_surfaces,
        "Terrain bindings changed"
    );
    let other = module_runtime::load(&exported.source_root, primary_area, None).unwrap();
    let original_other = module_runtime::load(&root, primary_area, None).unwrap();
    assert_eq!(other.archive, original_other.archive);
    assert!(loaded.environment().unwrap().is_some());
    assert!(loaded.vegetation_imported().unwrap().is_some());
    assert_eq!(
        forge_delivery::file_hash(&package).unwrap().to_string(),
        expected_package
    );
    assert_eq!(
        forge_delivery::file_hash(&root.join("module.json"))
            .unwrap()
            .to_string(),
        expected_manifest
    );
    let proof = serde_json::json!({
        "schema": "forge.gate7.real-module-scene-wind.v1",
        "exported": exported,
        "area": area,
        "head": head,
        "archive_sha256": bytes_sha256(&archive),
        "wind_heading_degrees": wind.heading_degrees,
        "wind_speed_mps": wind.speed_mps,
        "origin_package_sha256": expected_package,
        "origin_manifest_sha256": expected_manifest,
        "reused_export_after_assertion_fix": resumed,
        "gpu_or_visual_qualified": false,
        "gate_closed": false
    });
    use std::io::Write;
    let mut proof_file = fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(output.join("proof.json"))
        .unwrap();
    proof_file
        .write_all(&serde_json::to_vec_pretty(&proof).unwrap())
        .unwrap();
    println!("{}", serde_json::to_string(&proof).unwrap());
}
