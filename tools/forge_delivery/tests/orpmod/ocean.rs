use super::*;

fn fixture() -> Fixture {
    let files = ocean_fixture::files();
    let index = ocean_fixture::index(&files);
    fixture_with(files, index)
}

fn fixture_with(files: BTreeMap<String, Vec<u8>>, index: Vec<u8>) -> Fixture {
    let mut f = Fixture::new("ocean-transport");
    let base = "ocean/source";
    fs::create_dir_all(f.source.join(base)).unwrap();
    let index_path = format!("{base}/package.json");
    fs::write(f.source.join(&index_path), &index).unwrap();
    let manifest = ModuleFilePin {
        path: index_path.clone(),
        sha256: bytes_sha256(&index),
    };
    f.manifest.files.push(ModuleFile {
        path: index_path,
        bytes: index.len() as u64,
        sha256: manifest.sha256,
        role: ModuleFileRole::OceanSimulation,
    });
    let mut resources = Vec::new();
    for (name, bytes) in files {
        let path = format!("{base}/{name}");
        fs::write(f.source.join(&path), &bytes).unwrap();
        let license = name == "LICENSE-Tidewater.txt";
        if license {
            f.manifest.provenance.license_paths.push(path.clone());
        }
        let pin = ModuleFilePin {
            path: path.clone(),
            sha256: bytes_sha256(&bytes),
        };
        f.manifest.files.push(ModuleFile {
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
    f.manifest.compatibility.module_api_version = MODULE_OCEAN_API_VERSION;
    f.manifest.ocean_inputs.push(ModuleOceanBinding {
        area_id: f.manifest.zones[0].area_id,
        surface: None,
        shore_sim: None,
        package: ModuleOceanFiles {
            manifest,
            resources,
        },
    });
    f.save();
    f
}

#[test]
#[ignore = "Requires an explicit pinned locally acquired ocean package"]
fn acquired_ocean_inputs_survive_module_package_and_installed_resolution() {
    let root = PathBuf::from(std::env::var_os("FORGE_OCEAN_PACKAGE").expect("ocean input path"));
    let expected: forge_core::ContentHash = std::env::var("FORGE_OCEAN_PACKAGE_SHA256")
        .expect("ocean manifest pin")
        .parse()
        .unwrap();
    assert!(root.is_absolute());
    forge_delivery::manifest::safe_chain(&root).unwrap();
    let index = fs::read(root.join("package.json")).unwrap();
    assert_eq!(bytes_sha256(&index), expected);
    let pins =
        forge_assets::source_ocean_package::OceanPackage::resource_pins(&index, expected).unwrap();
    assert_eq!(pins.len(), 260);
    let files: BTreeMap<_, _> = pins
        .into_iter()
        .map(|(name, pin)| {
            let bytes = fs::read(root.join(&name)).unwrap();
            assert_eq!(bytes.len(), pin.bytes);
            assert_eq!(bytes_sha256(&bytes), pin.sha256);
            (name, bytes)
        })
        .collect();
    let f = fixture_with(files, index);
    let mut descriptor = f.descriptor();
    descriptor.license_paths = f.manifest.provenance.license_paths.clone();
    let archive_path = f.root.join("real-ocean.orpmod");
    orpmod::pack(&f.source, descriptor, &archive_path, false).unwrap();
    let loaded = forge_delivery::module_runtime::load(
        &archive_path,
        f.manifest.zones[0].area_id,
        Some(&f.root.join("cache")),
    )
    .unwrap();
    let ocean = loaded.ocean().unwrap().unwrap();
    assert_eq!(ocean.package_sha256, expected);
    assert_eq!(ocean.package.bootstrap_frames(), 124);
    assert!(ocean.package.has_acquired_frames());
    assert!(ocean.package.file("column.wgsl").unwrap().len() > 10_000);
    assert!(loaded.reject_unrendered_ocean().is_err());
}

#[test]
#[ignore = "Requires explicit admitted water surface and shore packages"]
fn real_surface_and_shore_roundtrip_and_mismatched_parameters_are_refused() {
    let mut f = fixture();
    water_fixture::attach_real_profiles(&f.source, &mut f.manifest);
    let area = f.manifest.zones[0].area_id;
    orpmod::validate_directory(&f.source, false).unwrap();
    let mut descriptor = f.descriptor();
    descriptor.license_paths = f.manifest.provenance.license_paths.clone();
    let package = f.root.join("complete-water.orpmod");
    orpmod::pack(&f.source, descriptor, &package, false).unwrap();
    let loaded =
        forge_delivery::module_runtime::load(&package, area, Some(&f.root.join("cache"))).unwrap();
    let water = loaded.ocean().unwrap().unwrap();
    let surface = water.surface.unwrap();
    let shore = water.shore_sim.unwrap();
    assert_eq!(surface.shore_field_sha256(), shore.shore_field_sha256());
    assert_eq!(
        surface.file("uniform-shoreP.bin"),
        shore.file("shore-params.bin")
    );
    assert_eq!(
        surface.file("uniform-shoreSimP.bin"),
        shore.file("simulation-params.bin")
    );
    assert!(loaded.reject_unrendered_ocean().is_err());

    // Re-pin a valid surface package with changed parameters: independent codec
    // admission succeeds, but the paired shore package must no longer be accepted.
    let name = "ocean/surface/uniform-shoreP.bin";
    let mut bytes = fs::read(f.source.join(name)).unwrap();
    bytes[0] ^= 1;
    fs::write(f.source.join(name), &bytes).unwrap();
    let hash = bytes_sha256(&bytes);
    f.manifest
        .files
        .iter_mut()
        .find(|file| file.path == name)
        .unwrap()
        .sha256 = hash;
    let water_binding = f.manifest.ocean_inputs[0].surface.as_mut().unwrap();
    water_binding
        .resources
        .iter_mut()
        .find(|pin| pin.path == name)
        .unwrap()
        .sha256 = hash;
    let index_path = water_binding.manifest.path.clone();
    let mut index: serde_json::Value =
        serde_json::from_slice(&fs::read(f.source.join(&index_path)).unwrap()).unwrap();
    index["files"]["uniform-shoreP.bin"]["sha256"] = serde_json::json!(hash);
    let index = serde_json::to_vec(&index).unwrap();
    fs::write(f.source.join(&index_path), &index).unwrap();
    water_binding.manifest.sha256 = bytes_sha256(&index);
    let file = f
        .manifest
        .files
        .iter_mut()
        .find(|file| file.path == index_path)
        .unwrap();
    file.sha256 = water_binding.manifest.sha256;
    file.bytes = index.len() as u64;
    f.save();
    let result = orpmod::validate_directory(&f.source, false);
    assert!(matches!(result, Err(error) if error.contains("Shore and surface inputs differ")));
}

#[test]
fn closed_ocean_inputs_survive_packaging_and_installed_resolution() {
    let f = fixture();
    orpmod::validate_directory(&f.source, false).unwrap();
    let mut descriptor = f.descriptor();
    descriptor.license_paths = f.manifest.provenance.license_paths.clone();
    let archive_path = f.root.join("ocean.orpmod");
    archive::pack(&f.source, descriptor, &archive_path).unwrap();
    let inspected = orpmod::inspect(&archive_path, false).unwrap();
    assert_eq!(inspected.ocean_inputs, f.manifest.ocean_inputs);
    let loaded = forge_delivery::module_runtime::load(
        &archive_path,
        f.manifest.zones[0].area_id,
        Some(&f.root.join("cache")),
    )
    .unwrap();
    let ocean = loaded.ocean().unwrap().unwrap();
    assert!(loaded.reject_unrendered_ocean().is_err());
    assert_eq!(
        ocean.package_sha256,
        f.manifest.ocean_inputs[0].package.manifest.sha256
    );
    assert_eq!(ocean.package.bootstrap_frames(), 120);
    assert_eq!(
        ocean.package.file("column.wgsl"),
        Some(&b"@compute fn main() {}"[..])
    );
    let changed = loaded.package_root.join("ocean/source/column.wgsl");
    fs::write(changed, b"tampered").unwrap();
    assert!(loaded.ocean().is_err());
}

#[test]
fn absent_or_malformed_inputs_cannot_fall_back_to_global_ocean_data() {
    let mut f = fixture();
    let area = f.manifest.zones[0].area_id;
    let mut reads = 0;
    let mut absent = f.manifest.clone();
    absent.ocean_inputs.clear();
    absent.files.retain(|file| !file.path.starts_with("ocean/"));
    absent
        .provenance
        .license_paths
        .retain(|path| !path.starts_with("ocean/"));
    assert!(
        forge_delivery::module_ocean::resolve(&absent, area, |_, _| {
            reads += 1;
            Err("unexpected".into())
        })
        .unwrap()
        .is_none()
    );
    assert_eq!(reads, 0);
    f.manifest.ocean_inputs[0]
        .package
        .resources
        .retain(|pin| !pin.path.ends_with("column.wgsl"));
    f.manifest
        .files
        .retain(|file| !file.path.ends_with("column.wgsl"));
    assert!(
        forge_delivery::module_ocean::resolve(&f.manifest, area, |path, _| {
            fs::read(f.source.join(path)).map_err(|error| error.to_string())
        })
        .is_err()
    );
    f.save();
    assert!(orpmod::validate_directory(&f.source, false).is_err());
}

#[test]
fn ocean_program_cannot_be_relabelled_as_a_license() {
    let f = fixture();
    let mut module = f.manifest.clone();
    let file = module
        .files
        .iter_mut()
        .find(|file| file.path.ends_with("column.wgsl"))
        .unwrap();
    file.role = ModuleFileRole::License;
    module.provenance.license_paths.push(file.path.clone());
    module.validate().unwrap();
    assert!(
        matches!(forge_delivery::module_ocean::resolve(&module, module.zones[0].area_id, |path, _| {
        fs::read(f.source.join(path)).map_err(|error| error.to_string())
    }), Err(error) if error.contains("role differs from its source index"))
    );
}
