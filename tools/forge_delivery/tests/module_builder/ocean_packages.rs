use super::*;
use forge_project::module_package::{
    MODULE_OCEAN_API_VERSION, ModuleFile, ModuleFilePin, ModuleFileRole, ModuleOceanBinding,
    ModuleOceanFiles,
};

fn ocean_origin(
    original: &ExportedModule,
    area: forge_core::AreaId,
    path: &std::path::Path,
) -> (
    forge_project::module_package::ModulePackageManifest,
    BTreeMap<String, Vec<u8>>,
) {
    let mut module = orpmod::inspect(&original.package_path, false).unwrap();
    let files = ocean_fixture::files();
    let index = ocean_fixture::index(&files);
    let base = "ocean/source";
    fs::create_dir_all(original.source_root.join(base)).unwrap();
    let index_path = format!("{base}/package.json");
    fs::write(original.source_root.join(&index_path), &index).unwrap();
    let manifest = ModuleFilePin {
        path: index_path.clone(),
        sha256: bytes_sha256(&index),
    };
    module.files.push(ModuleFile {
        path: index_path,
        bytes: index.len() as u64,
        sha256: manifest.sha256,
        role: ModuleFileRole::OceanSimulation,
    });
    let mut resources = Vec::new();
    for (name, bytes) in &files {
        let path = format!("{base}/{name}");
        fs::write(original.source_root.join(&path), bytes).unwrap();
        let license = name == "LICENSE-Tidewater.txt";
        if license {
            module.provenance.license_paths.push(path.clone());
        }
        let pin = ModuleFilePin {
            path: path.clone(),
            sha256: bytes_sha256(bytes),
        };
        module.files.push(ModuleFile {
            path,
            bytes: bytes.len() as u64,
            sha256: pin.sha256,
            role: if license {
                ModuleFileRole::License
            } else {
                ModuleFileRole::OceanSimulation
            },
        });
        resources.push(pin);
    }
    module.compatibility.module_api_version = MODULE_OCEAN_API_VERSION;
    module.ocean_inputs.push(ModuleOceanBinding {
        area_id: area,
        surface: None,
        shore_sim: None,
        package: ModuleOceanFiles {
            manifest,
            resources,
        },
    });
    fs::write(
        original.source_root.join("module.json"),
        module.to_bytes().unwrap(),
    )
    .unwrap();
    let descriptor = forge_delivery::manifest::Descriptor {
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
    };
    orpmod::pack(&original.source_root, descriptor, path, false).unwrap();
    (module, files)
}

#[test]
fn origin_reexport_keeps_closed_ocean_inputs_and_their_license() {
    let temp = Temp::new();
    let fixture = Fixture::new(false);
    let original = fixture.build(&temp.0.join("original")).unwrap();
    let path = temp.0.join("ocean-origin.orpmod");
    let (module, files) = ocean_origin(&original, fixture.zone.head().base.terrain.area_id, &path);
    let bytes: std::sync::Arc<[u8]> = fs::read(&path).unwrap().into();
    let exported = build_with_origin(
        &fixture,
        &temp.0.join("reexport"),
        Fixture::metadata(),
        Some(ExportOrigin {
            expected_package_sha256: bytes_sha256(&bytes),
            package_bytes: bytes,
        }),
    )
    .unwrap();
    let admitted = module_runtime::load(
        &exported.package_path,
        module.zones[0].area_id,
        Some(&temp.0.join("cache")),
    )
    .unwrap();
    assert_eq!(admitted.manifest.ocean_inputs, module.ocean_inputs);
    assert_eq!(
        admitted.manifest.compatibility.module_api_version,
        MODULE_OCEAN_API_VERSION
    );
    let ocean = admitted.ocean().unwrap().unwrap();
    for (name, bytes) in files {
        assert_eq!(ocean.package.file(&name), Some(bytes.as_slice()));
    }
    assert!(
        admitted
            .manifest
            .provenance
            .license_paths
            .contains(&"ocean/source/LICENSE-Tidewater.txt".to_owned())
    );
}

#[test]
fn aggregate_reexport_preserves_an_ocean_bound_only_to_a_secondary_area() {
    let temp = Temp::new();
    let mut fixture = Fixture::new(false);
    let snapshot = aggregate_snapshot(&mut fixture);
    let original = build_module_with_origin(
        &temp.0.join("original"),
        &snapshot,
        Fixture::metadata(),
        fixture.catalog.clone(),
        fixture.payloads.clone(),
        None,
    )
    .unwrap();
    let primary = snapshot.zones[0].area_id;
    let secondary = snapshot.zones[1].area_id;
    let path = temp.0.join("secondary-ocean.orpmod");
    let (module, _) = ocean_origin(&original, secondary, &path);
    let bytes: std::sync::Arc<[u8]> = fs::read(&path).unwrap().into();
    let exported = build_module_with_origin(
        &temp.0.join("reexport"),
        &snapshot,
        Fixture::metadata(),
        fixture.catalog,
        fixture.payloads,
        Some(ExportOrigin {
            expected_package_sha256: bytes_sha256(&bytes),
            package_bytes: bytes,
        }),
    )
    .unwrap();
    let cache = temp.0.join("cache");
    let first = module_runtime::load(&exported.package_path, primary, Some(&cache)).unwrap();
    assert!(first.ocean().unwrap().is_none());
    assert!(first.reject_unrendered_ocean().is_ok());
    let second = module_runtime::load(&exported.package_path, secondary, Some(&cache)).unwrap();
    assert_eq!(second.manifest.ocean_inputs, module.ocean_inputs);
    assert_eq!(
        second.ocean().unwrap().unwrap().package.bootstrap_frames(),
        120
    );
    assert!(second.reject_unrendered_ocean().is_err());
}

#[test]
#[ignore = "Requires explicit admitted water surface and shore packages"]
fn real_water_companions_survive_module_reexport() {
    let temp = Temp::new();
    let fixture = Fixture::new(false);
    let original = fixture.build(&temp.0.join("original")).unwrap();
    let area = fixture.zone.head().base.terrain.area_id;
    let (mut module, _) = ocean_origin(&original, area, &temp.0.join("fft-origin.orpmod"));
    water_fixture::attach_real_profiles(&original.source_root, &mut module);
    let path = temp.0.join("water-origin.orpmod");
    let descriptor = forge_delivery::manifest::Descriptor {
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
    };
    orpmod::pack(&original.source_root, descriptor, &path, false).unwrap();
    let bytes: std::sync::Arc<[u8]> = fs::read(&path).unwrap().into();
    let exported = build_with_origin(
        &fixture,
        &temp.0.join("reexport"),
        Fixture::metadata(),
        Some(ExportOrigin {
            expected_package_sha256: bytes_sha256(&bytes),
            package_bytes: bytes,
        }),
    )
    .unwrap();
    let loaded =
        module_runtime::load(&exported.package_path, area, Some(&temp.0.join("cache"))).unwrap();
    assert_eq!(loaded.manifest.ocean_inputs, module.ocean_inputs);
    assert_eq!(
        loaded.manifest.compatibility.module_api_version,
        forge_project::module_package::MODULE_OCEAN_SURFACE_API_VERSION
    );
    let ocean = loaded.ocean().unwrap().unwrap();
    let surface = ocean.surface.unwrap();
    let shore = ocean.shore_sim.unwrap();
    assert_eq!(surface.shore_field_sha256(), shore.shore_field_sha256());
    assert!(loaded.reject_unrendered_ocean().is_err());
    for binding in [
        &module.ocean_inputs[0].surface,
        &module.ocean_inputs[0].shore_sim,
    ] {
        for pin in std::iter::once(&binding.as_ref().unwrap().manifest)
            .chain(&binding.as_ref().unwrap().resources)
        {
            let bytes = loaded.read_payload(&pin.path, 32 * 1024 * 1024).unwrap();
            assert_eq!(bytes_sha256(&bytes), pin.sha256);
        }
    }
}
