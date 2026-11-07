//! Real source-profile closure and re-export, with explicit persistent private inputs.
use super::*;
use forge_assets::source_surface_package::SurfacePackage;
use forge_project::module_package::{
    MODULE_OBJECTS_API_VERSION, ModuleFile, ModuleFilePin, ModuleFileRole, ModuleObjectAsset,
    ModuleObjectBinding, ModuleObjectFiles, ModuleObjectSource,
};
use forge_project::zone_authoring::{
    placements::ManualPlacement,
    session::{ZoneAction, ZoneRequest},
};
use std::{
    io::{Read, Write},
    path::Path,
    sync::Arc,
};

#[test]
fn absent_object_binding_never_requests_resources_or_infers_them_from_terrain() {
    let temp = Temp::new();
    let fixture = Fixture::new(false);
    let exported = fixture.build(&temp.0.join("objects-absent")).unwrap();
    let module = orpmod::inspect(&exported.package_path, false).unwrap();
    let calls = std::cell::Cell::new(0);
    assert!(
        forge_delivery::module_objects::resolve(&module, module.zones[0].area_id, |_, _| {
            calls.set(calls.get() + 1);
            Err("No resources expected".into())
        })
        .unwrap()
        .is_none()
    );
    assert!(
        forge_delivery::module_objects::resolve(
            &module,
            forge_core::AreaId::deterministic("foreign area"),
            |_, _| {
                calls.set(calls.get() + 1);
                Err("No resources expected".into())
            }
        )
        .is_err()
    );
    assert_eq!(calls.get(), 0);
}

fn bounded(root: &Path, name: &str, maximum: usize) -> Result<Vec<u8>, String> {
    let base = root.canonicalize().map_err(|e| e.to_string())?;
    let path = base.join(name).canonicalize().map_err(|e| e.to_string())?;
    if !path.starts_with(&base) {
        return Err("Escaping qualification input".into());
    }
    let mut bytes = Vec::new();
    fs::File::open(path)
        .map_err(|e| e.to_string())?
        .take(maximum as u64 + 1)
        .read_to_end(&mut bytes)
        .map_err(|e| e.to_string())?;
    if bytes.len() > maximum {
        return Err("Qualification resource exceeds limit".into());
    }
    Ok(bytes)
}

fn write_new(path: &Path, bytes: &[u8]) {
    fs::create_dir_all(path.parent().unwrap()).unwrap();
    fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(path)
        .unwrap()
        .write_all(bytes)
        .unwrap();
}

fn attach(
    module: &mut forge_project::module_package::ModulePackageManifest,
    payloads: &mut BTreeMap<String, Vec<u8>>,
    index: &[u8],
    pin: forge_core::ContentHash,
    profile: &SurfacePackage,
) {
    let mut resources = Vec::new();
    let license = SurfacePackage::license_name(index, pin).unwrap();
    for (name, entry) in SurfacePackage::resource_pins(index, pin).unwrap() {
        let path = format!("objects/profile/{name}");
        let bytes = profile.file(&name).unwrap().to_vec();
        assert_eq!(bytes.len(), entry.bytes);
        let role = if name == license {
            module.provenance.license_paths.push(path.clone());
            ModuleFileRole::License
        } else {
            ModuleFileRole::ObjectSource
        };
        module.files.push(ModuleFile {
            path: path.clone(),
            bytes: bytes.len() as u64,
            sha256: entry.sha256,
            role,
        });
        resources.push(ModuleFilePin {
            path: path.clone(),
            sha256: entry.sha256,
        });
        payloads.insert(path, bytes);
    }
    let path = "objects/profile/package.json".to_owned();
    module.files.push(ModuleFile {
        path: path.clone(),
        bytes: index.len() as u64,
        sha256: pin,
        role: ModuleFileRole::ObjectSource,
    });
    payloads.insert(path.clone(), index.to_vec());
    let mut assets: Vec<_> = profile
        .catalog()
        .assets
        .iter()
        .map(|a| ModuleObjectAsset {
            asset_id: a.id,
            sha256: a.content_hash,
        })
        .collect();
    assets.sort_by_key(|a| a.asset_id);
    module.compatibility.module_api_version = MODULE_OBJECTS_API_VERSION;
    module.objects.push(ModuleObjectBinding {
        area_id: module.zones[0].area_id,
        source: ModuleObjectSource::SourceSurfaces {
            package: ModuleObjectFiles {
                manifest: ModuleFilePin { path, sha256: pin },
                resources,
            },
            assets,
        },
    });
}

fn repin(
    module: &mut forge_project::module_package::ModulePackageManifest,
    zone: &ZoneEditSession,
    payloads: &mut BTreeMap<String, Vec<u8>>,
) {
    let archive = zone.archive_bytes().unwrap();
    let hash = bytes_sha256(&archive);
    module.zones[0].head_hash = zone.head().content_hash().unwrap();
    module.zones[0].archive_sha256 = hash;
    let path = module.zones[0].archive_path.clone();
    let file = module.files.iter_mut().find(|f| f.path == path).unwrap();
    file.bytes = archive.len() as u64;
    file.sha256 = hash;
    payloads.insert(path, archive);
}

fn edit(zone: &mut ZoneEditSession, asset: ModuleObjectAsset, x: f32) {
    let mut corrections = zone.head().corrections.clone();
    let id = EntityId::deterministic("explicit surface object placement");
    let placements = corrections.placements.get_or_insert_with(Vec::new);
    if let Some(p) = placements.iter_mut().find(|p| p.id == id) {
        p.transform.translation_m.x = x;
    } else {
        placements.push(ManualPlacement {
            id,
            name: "Object from explicit module package".into(),
            asset_id: asset.asset_id,
            asset_hash: asset.sha256,
            transform: Transform {
                translation_m: Vec3 { x, y: 0.0, z: 0.0 },
                ..Transform::IDENTITY
            },
            locked: false,
            local_lights: None,
            physics: None,
            particles: None,
        });
        placements.sort_by_key(|p| p.id);
    }
    zone.execute(&ZoneRequest {
        command_id: CommandId::deterministic(format!("object position {x}")),
        expected_hash: zone.head().content_hash().unwrap(),
        expected_revision: zone.head().revision,
        action: ZoneAction::edit(corrections),
    })
    .unwrap();
}

fn reject_bad_closures(module: &forge_project::module_package::ModulePackageManifest, root: &Path) {
    let area = module.zones[0].area_id;
    let mut no_license = module.clone();
    let path = no_license
        .provenance
        .license_paths
        .iter()
        .find(|path| path.starts_with("objects/profile/"))
        .unwrap()
        .clone();
    no_license.provenance.license_paths.retain(|p| p != &path);
    no_license
        .files
        .iter_mut()
        .find(|file| file.path == path)
        .unwrap()
        .role = ModuleFileRole::ObjectSource;
    let license_calls = std::cell::Cell::new(0);
    assert!(
        forge_delivery::module_objects::resolve(&no_license, area, |name, max| {
            license_calls.set(license_calls.get() + 1);
            bounded(root, name, max)
        })
        .err()
        .unwrap()
        .contains("license")
    );
    assert_eq!(license_calls.get(), 1);
    let mut extra = module.clone();
    let pin = ModuleFilePin {
        path: "objects/profile/extra.bin".into(),
        sha256: bytes_sha256(b"extra"),
    };
    extra.files.push(ModuleFile {
        path: pin.path.clone(),
        bytes: 5,
        sha256: pin.sha256,
        role: ModuleFileRole::ObjectSource,
    });
    let ModuleObjectSource::SourceSurfaces { package, .. } = &mut extra.objects[0].source;
    package.resources.push(pin);
    let calls = std::cell::Cell::new(0);
    assert!(
        forge_delivery::module_objects::resolve(&extra, area, |name, max| {
            calls.set(calls.get() + 1);
            bounded(root, name, max)
        })
        .err()
        .unwrap()
        .contains("unexpected")
    );
    assert_eq!(calls.get(), 1);
    let mut missing = module.clone();
    let ModuleObjectSource::SourceSurfaces { package, .. } = &mut missing.objects[0].source;
    let removed = package
        .resources
        .iter()
        .find(|pin| pin.path.ends_with(".bin"))
        .unwrap()
        .path
        .clone();
    package.resources.retain(|pin| pin.path != removed);
    missing.files.retain(|file| file.path != removed);
    calls.set(0);
    assert!(
        forge_delivery::module_objects::resolve(&missing, area, |name, max| {
            calls.set(calls.get() + 1);
            bounded(root, name, max)
        })
        .is_err()
    );
    assert_eq!(calls.get(), 1);
    assert!(
        forge_delivery::module_objects::resolve(module, area, |name, max| {
            let mut bytes = bounded(root, name, max)?;
            if name.ends_with(".bin") {
                bytes[0] ^= 1;
            }
            Ok(bytes)
        })
        .is_err()
    );
}

#[test]
#[ignore = "Requires explicit source package and a fresh persistent qualification output"]
#[allow(
    clippy::too_many_lines,
    reason = "One persistent qualification transaction keeps admission, refusal, retouch and re-export evidence together"
)]
fn private_surface_objects_keep_exact_resources_through_module_admission_and_reexport() {
    let package = PathBuf::from(std::env::var_os("FORGE_OBJECT_PACKAGE").unwrap());
    let output = PathBuf::from(std::env::var_os("FORGE_OBJECT_OUTPUT").unwrap());
    assert!(package.is_absolute() && output.is_absolute() && !output.exists());
    fs::create_dir_all(&output).unwrap();
    let index = bounded(&package, "package.json", 2 * 1024 * 1024).unwrap();
    let pin = "a53b6987563b0b0d5bb5b9113e1c3f27142baf53658a9e1a78338854f99599e6"
        .parse()
        .unwrap();
    let profile =
        SurfacePackage::load(&index, pin, |name, max| bounded(&package, name, max)).unwrap();
    let mut fixture = Fixture::new(false);
    let baseline = fixture.build(&output.join("baseline")).unwrap();
    let mut module = orpmod::inspect(&baseline.package_path, false).unwrap();
    let mut payloads: BTreeMap<_, _> = module
        .files
        .iter()
        .map(|file| {
            (
                file.path.clone(),
                bounded(&baseline.source_root, &file.path, 64 * 1024 * 1024).unwrap(),
            )
        })
        .collect();
    attach(&mut module, &mut payloads, &index, pin, &profile);
    let ModuleObjectSource::SourceSurfaces { assets, .. } = &module.objects[0].source;
    let asset = assets[0];
    edit(&mut fixture.zone, asset, 0.0);
    repin(&mut module, &fixture.zone, &mut payloads);
    module
        .validate_zone_archive(
            module.zones[0].area_id,
            &fixture.zone.archive_bytes().unwrap(),
        )
        .unwrap();
    let source = output.join("source");
    for (path, bytes) in payloads {
        write_new(&source.join(path), &bytes);
    }
    write_new(&source.join("module.json"), &module.to_bytes().unwrap());
    reject_bad_closures(&module, &source);
    let received = output.join("objects.orpmod");
    let (_, transport) = orpmod::pack(
        &source,
        forge_delivery::manifest::Descriptor {
            schema: "forge.delivery.package.v1".into(),
            id: module.module_id.to_string(),
            label: module.name.clone(),
            version: module.module_version.clone(),
            kind: forge_delivery::manifest::Kind::Module,
            platform: "any".into(),
            source_revision: module.provenance.source_revision.clone(),
            rights: forge_delivery::manifest::Rights::PrivateLocalOnly,
            license_paths: module.provenance.license_paths.clone(),
            files: vec![],
            product: None,
        },
        &received,
        false,
    )
    .unwrap();
    assert_eq!(orpmod::inspect(&received, false).unwrap(), module);
    let resolved =
        forge_delivery::module_objects::resolve(&module, module.zones[0].area_id, |name, max| {
            bounded(&source, name, max)
        })
        .unwrap()
        .unwrap();
    assert_eq!(resolved.package.catalog().assets.len(), 23);
    assert_eq!(resolved.package_sha256, pin);
    let old_head = fixture.zone.head().content_hash().unwrap();
    edit(&mut fixture.zone, asset, 0.5);
    let new_head = fixture.zone.head().content_hash().unwrap();
    assert_ne!(old_head, new_head);
    let original: Arc<[u8]> = fs::read(&received).unwrap().into();
    let export = build_zone_with_origin(
        &output.join("exports"),
        &fixture.zone.archive_bytes().unwrap(),
        new_head,
        Fixture::metadata(),
        fixture.catalog.clone(),
        fixture.payloads.clone(),
        Some(ExportOrigin {
            expected_package_sha256: bytes_sha256(&original),
            package_bytes: Arc::clone(&original),
        }),
    )
    .unwrap();
    let admitted = orpmod::inspect(&export.package_path, false).unwrap();
    assert_eq!(admitted.objects, module.objects);
    let ModuleObjectSource::SourceSurfaces { package, .. } = &module.objects[0].source;
    for resource in std::iter::once(&package.manifest).chain(&package.resources) {
        assert_eq!(
            bounded(&source, &resource.path, 32 * 1024 * 1024).unwrap(),
            bounded(&export.source_root, &resource.path, 32 * 1024 * 1024).unwrap()
        );
    }
    let mut public = Fixture::metadata();
    public.rights = ModuleRights::Redistributable;
    assert!(
        build_zone_with_origin(
            &output.join("public-refused"),
            &fixture.zone.archive_bytes().unwrap(),
            new_head,
            public,
            fixture.catalog,
            fixture.payloads,
            Some(ExportOrigin {
                expected_package_sha256: bytes_sha256(&original),
                package_bytes: original
            })
        )
        .is_err()
    );
    assert!(!output.join("public-refused").exists());
    write_new(&output.join("proof.json"),&serde_json::to_vec_pretty(&json!({"schema":"forge.gate7.module-source-objects-proof.v1","package_index_sha256":pin,"surface_assets":23,"resources":package.resources.len(),"resources_preserved":true,"old_head":old_head,"new_head":new_head,"transport":transport,"exported":export,"received_package":received,"invalid_closures_refused":true,"private_publication_refused":true,"studio_qualified":false,"runtime_draw_qualified":false,"gate_closed":false})).unwrap());
}

#[test]
#[ignore = "Requires explicit admitted raster module, surface package and fresh persistent output"]
#[allow(
    clippy::too_many_lines,
    clippy::cast_possible_truncation,
    reason = "One bounded private fixture transaction; admitted raster heights fit f32"
)]
fn source_objects_join_an_independent_raster_module_without_reference_fallback() {
    let origin = PathBuf::from(std::env::var_os("FORGE_OBJECT_ORIGIN").unwrap());
    let package = PathBuf::from(std::env::var_os("FORGE_OBJECT_PACKAGE").unwrap());
    let output = PathBuf::from(std::env::var_os("FORGE_OBJECT_OUTPUT").unwrap());
    assert!(
        origin.is_absolute() && package.is_absolute() && output.is_absolute() && !output.exists()
    );
    let area = "3d5bc933-f29e-53fe-8afa-5d312baa58b4".parse().unwrap();
    let loaded = forge_delivery::module_runtime::load(&origin, area, None).unwrap();
    assert!(loaded.objects().unwrap().is_none());
    let (terrain, _) = loaded.zone.head().effective().unwrap();
    let raster = terrain.sampled_source.as_ref().unwrap();
    let plan = forge_world::sampled_terrain::SampledTerrainPlan::from_document(
        &terrain,
        &loaded.source_blobs[&raster.samples_sha256],
    )
    .unwrap();
    let mut module = loaded.manifest;
    let origin_head = loaded.zone.head().content_hash().unwrap();
    let mut zone = loaded.zone;
    let mut payloads: BTreeMap<_, _> = module
        .files
        .iter()
        .map(|file| {
            (
                file.path.clone(),
                bounded(&origin, &file.path, 64 * 1024 * 1024).unwrap(),
            )
        })
        .collect();
    let index = bounded(&package, "package.json", 2 * 1024 * 1024).unwrap();
    let pin = "a53b6987563b0b0d5bb5b9113e1c3f27142baf53658a9e1a78338854f99599e6"
        .parse()
        .unwrap();
    let profile =
        SurfacePackage::load(&index, pin, |name, max| bounded(&package, name, max)).unwrap();
    attach(&mut module, &mut payloads, &index, pin, &profile);
    module.objects[0].area_id = area;
    let asset = &profile.catalog().assets[0];
    let mut corrections = zone.head().corrections.clone();
    let placements = corrections.placements.get_or_insert_with(Vec::new);
    let mut added = Vec::new();
    for (number, x) in [(0, 8.0_f32), (1, 24.0_f32)] {
        let placement = ManualPlacement {
            id: EntityId::deterministic(format!("module source object native {number}")),
            name: format!("Native source building {number}"),
            asset_id: asset.id,
            asset_hash: asset.content_hash,
            transform: Transform {
                translation_m: Vec3::new(
                    x,
                    plan.height_at(f64::from(x), -111.0).unwrap() as f32,
                    -111.0,
                ),
                ..Transform::IDENTITY
            },
            locked: false,
            local_lights: None,
            physics: None,
            particles: None,
        };
        added.push(placement.clone());
        placements.push(placement);
    }
    placements.sort_by_key(|placement| placement.id);
    zone.execute(&ZoneRequest {
        command_id: CommandId::deterministic("native source objects fixture"),
        expected_hash: origin_head,
        expected_revision: zone.head().revision,
        action: ZoneAction::edit(corrections),
    })
    .unwrap();
    let archive = zone.archive_bytes().unwrap();
    let selected = module
        .zones
        .iter_mut()
        .find(|zone| zone.area_id == area)
        .unwrap();
    selected.head_hash = zone.head().content_hash().unwrap();
    selected.archive_sha256 = bytes_sha256(&archive);
    let file = module
        .files
        .iter_mut()
        .find(|file| file.path == selected.archive_path)
        .unwrap();
    file.bytes = archive.len() as u64;
    file.sha256 = selected.archive_sha256;
    payloads.insert(selected.archive_path.clone(), archive);
    module
        .vegetation
        .iter_mut()
        .find(|binding| binding.area_id == area)
        .unwrap()
        .vegetation_sha256 = zone
        .head()
        .effective()
        .unwrap()
        .1
        .unwrap()
        .content_hash()
        .unwrap();
    // This fresh daytime fixture retains the origin's acquired environment and
    // live solar algorithm; only its explicitly authored initial clock changes.
    module
        .environments
        .iter_mut()
        .find(|binding| binding.area_id == area)
        .unwrap()
        .solar_clock
        .as_mut()
        .unwrap()
        .start_minutes = 972;
    let source = output.join("source");
    for (path, bytes) in payloads {
        write_new(&source.join(path), &bytes);
    }
    write_new(&source.join("module.json"), &module.to_bytes().unwrap());
    let received = output.join("objects.orpmod");
    let (_, transport) = orpmod::pack(
        &source,
        forge_delivery::manifest::Descriptor {
            schema: "forge.delivery.package.v1".into(),
            id: module.module_id.to_string(),
            label: module.name.clone(),
            version: module.module_version.clone(),
            kind: forge_delivery::manifest::Kind::Module,
            platform: "any".into(),
            source_revision: module.provenance.source_revision.clone(),
            rights: forge_delivery::manifest::Rights::PrivateLocalOnly,
            license_paths: module.provenance.license_paths.clone(),
            files: vec![],
            product: None,
        },
        &received,
        false,
    )
    .unwrap();
    assert_eq!(orpmod::inspect(&received, false).unwrap(), module);
    let admitted =
        forge_delivery::module_runtime::load(&received, area, Some(&output.join("cache"))).unwrap();
    assert_eq!(admitted.objects().unwrap().unwrap().package_sha256, pin);
    let plain = module
        .zones
        .iter()
        .find(|zone| zone.area_id != area)
        .unwrap()
        .area_id;
    assert!(
        forge_delivery::module_runtime::load(&source, plain, None)
            .unwrap()
            .objects()
            .unwrap()
            .is_none()
    );
    write_new(&output.join("proof.json"), &serde_json::to_vec_pretty(&json!({
        "schema":"forge.gate7.module-object-runtime-fixture.v1", "origin":origin, "origin_head":origin_head,
        "head":admitted.zone.head().content_hash().unwrap(), "area":area, "archive_sha256":bytes_sha256(&admitted.archive),
        "package":received, "transport":transport, "source":source, "placements":added, "palette_assets":23,
        "environment_start_minutes":972, "other_area_objects_absent":true, "runtime_draw_qualified":false, "gate_closed":false,
    })).unwrap());
}
