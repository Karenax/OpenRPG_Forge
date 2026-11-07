//! Independent in-memory source profile, using the existing module and asset catalogs.
use super::*;
use forge_assets::{
    AssetCatalog, AssetKind, AssetRecord, AssetSource,
    source_fish_payload::{SourceFishPayload, SourceFishProfile},
};
use forge_project::module_package::{
    MODULE_FISH_API_VERSION, ModuleFile, ModuleFilePin, ModuleFileRole, ModuleFishBinding,
    ModuleObjectAsset, ModuleObjectFiles, ModulePackageManifest,
};
use std::cell::Cell;

fn fixture() -> (
    Temp,
    ExportedModule,
    ModulePackageManifest,
    BTreeMap<String, Vec<u8>>,
) {
    let temp = Temp::new();
    let exported = Fixture::new(false)
        .build(&temp.0.join("fish-module-base"))
        .unwrap();
    let mut module = orpmod::inspect(&exported.package_path, false).unwrap();
    module.compatibility.module_api_version = MODULE_FISH_API_VERSION;
    let description =
        br#"{"prototypes":[{}],"instances":[{"source_id":"independent/fish"}]}"#.to_vec();
    let license = b"independent license".to_vec();
    let geometry = b"independent geometry".to_vec();
    let resources = [
        ("geometry/fish.json", description),
        ("geometry/fish.bin", geometry),
        ("LICENSE.txt", license),
        ("source/original.js", b"historical source only".to_vec()),
    ];
    let mut indexed = BTreeMap::new();
    for (name, bytes) in &resources {
        indexed.insert(
            *name,
            json!({"bytes":bytes.len(),"sha256":bytes_sha256(bytes)}),
        );
    }
    let license_bytes = &resources[2].1;
    let index = serde_json::to_vec(&json!({"files":indexed,"license":{"file":"LICENSE.txt","bytes":license_bytes.len(),"sha256":bytes_sha256(license_bytes)}})).unwrap();
    let package_hash = bytes_sha256(&index);
    let asset = AssetRecord {
        id: AssetId::deterministic("independent/fish"),
        logical_name: "independent/fish".into(),
        kind: AssetKind::Mesh,
        source: AssetSource::External {
            uri: "https://example.invalid/fish".into(),
            declared_license: Some("MIT".into()),
        },
        content_hash: package_hash,
    };
    let catalog = serde_json::to_vec(&AssetCatalog {
        schema_version: 1,
        assets: vec![asset.clone()],
    })
    .unwrap();
    let mut payloads = BTreeMap::new();
    let mut add = |name: &str, bytes: Vec<u8>, role: ModuleFileRole| {
        let path = format!("fish/profile/{name}");
        let pin = ModuleFilePin {
            path: path.clone(),
            sha256: bytes_sha256(&bytes),
        };
        module.files.push(ModuleFile {
            path: path.clone(),
            bytes: bytes.len() as u64,
            sha256: pin.sha256,
            role,
        });
        payloads.insert(path, bytes);
        pin
    };
    let manifest = add("package.json", index, ModuleFileRole::ObjectSource);
    let catalog_pin = add("catalog.json", catalog, ModuleFileRole::ObjectSource);
    let mut resource_pins = Vec::new();
    for (name, bytes) in resources {
        if name == "source/original.js" {
            continue;
        }
        let role = if name == "LICENSE.txt" {
            ModuleFileRole::License
        } else {
            ModuleFileRole::ObjectSource
        };
        resource_pins.push(add(name, bytes, role));
    }
    module
        .provenance
        .license_paths
        .push("fish/profile/LICENSE.txt".into());
    module.fish_profiles.push(ModuleFishBinding {
        area_id: module.zones[0].area_id,
        package: ModuleObjectFiles {
            manifest,
            resources: resource_pins,
        },
        catalog: catalog_pin,
        excluded_source_scripts: vec!["source/original.js".into()],
        description: "geometry/fish.json".into(),
        prototypes: 1,
        instances: 1,
        assets: vec![ModuleObjectAsset {
            asset_id: asset.id,
            sha256: package_hash,
        }],
    });
    module.validate().unwrap();
    (temp, exported, module, payloads)
}

#[test]
fn module_fish_uses_only_its_exact_index_catalog_and_license() {
    let (_temp, _exported, module, payloads) = fixture();
    let area = module.zones[0].area_id;
    let resolved = forge_delivery::module_fish::resolve(&module, area, |path, _| {
        payloads.get(path).cloned().ok_or("missing".into())
    })
    .unwrap();
    assert_eq!(resolved.len(), 1);
    let profile = resolved[0].profile().unwrap();
    assert!(profile.file("source/original.js").is_none());
    assert_eq!(
        profile.identify(0, "independent/fish").unwrap(),
        Some(module.fish_profiles[0].assets[0].asset_id)
    );
    assert!(profile.identify(0, "other/fish").is_err());
    let calls = Cell::new(0);
    assert!(
        forge_delivery::module_fish::resolve(
            &module,
            forge_core::AreaId::deterministic("foreign"),
            |_, _| {
                calls.set(calls.get() + 1);
                Err("not expected".into())
            }
        )
        .is_err()
    );
    assert_eq!(calls.get(), 0);
}

#[test]
fn module_fish_rejects_changed_bytes_missing_resources_and_catalog_drift() {
    let (_temp, _exported, module, payloads) = fixture();
    let area = module.zones[0].area_id;
    let mut changed = payloads.clone();
    changed.get_mut("fish/profile/geometry/fish.bin").unwrap()[0] ^= 1;
    assert!(
        forge_delivery::module_fish::resolve(&module, area, |path, _| changed
            .get(path)
            .cloned()
            .ok_or("missing".into()))
        .is_err()
    );
    let mut missing = module.clone();
    missing.fish_profiles[0]
        .package
        .resources
        .retain(|pin| pin.path != "fish/profile/geometry/fish.bin");
    missing
        .files
        .retain(|file| file.path != "fish/profile/geometry/fish.bin");
    assert!(
        forge_delivery::module_fish::resolve(&missing, area, |path, _| payloads
            .get(path)
            .cloned()
            .ok_or("missing".into()))
        .is_err()
    );
    let mut unknown_source = module.clone();
    unknown_source.fish_profiles[0]
        .excluded_source_scripts
        .push("source/unknown.js".into());
    assert!(
        forge_delivery::module_fish::resolve(&unknown_source, area, |path, _| payloads
            .get(path)
            .cloned()
            .ok_or("missing".into()))
        .is_err()
    );
    let mut forbidden_exclusion = module.clone();
    forbidden_exclusion.fish_profiles[0]
        .excluded_source_scripts
        .insert(0, "geometry/fish.bin".into());
    assert!(
        forge_delivery::module_fish::resolve(&forbidden_exclusion, area, |path, _| payloads
            .get(path)
            .cloned()
            .ok_or("missing".into()))
        .is_err()
    );
    let mut altered_module = module.clone();
    let mut altered_payloads = payloads;
    let catalog_path = "fish/profile/catalog.json";
    let mut catalog: AssetCatalog =
        serde_json::from_slice(&altered_payloads[catalog_path]).unwrap();
    catalog.assets[0].logical_name = "independent/other".into();
    let bytes = serde_json::to_vec(&catalog).unwrap();
    let hash = bytes_sha256(&bytes);
    altered_module.fish_profiles[0].catalog.sha256 = hash;
    let file = altered_module
        .files
        .iter_mut()
        .find(|file| file.path == catalog_path)
        .unwrap();
    file.sha256 = hash;
    file.bytes = bytes.len() as u64;
    altered_payloads.insert(catalog_path.into(), bytes);
    assert!(
        forge_delivery::module_fish::resolve(&altered_module, area, |path, _| altered_payloads
            .get(path)
            .cloned()
            .ok_or("missing".into()))
        .is_err()
    );
}

#[test]
fn admission_rejects_a_repacked_catalog_with_foreign_fish_identity() {
    let (temp, exported, mut module, mut payloads) = fixture();
    let catalog_path = module.fish_profiles[0].catalog.path.clone();
    let mut catalog: AssetCatalog = serde_json::from_slice(&payloads[&catalog_path]).unwrap();
    catalog.assets[0].logical_name = "independent/not-in-the-profile".into();
    let catalog_bytes = serde_json::to_vec(&catalog).unwrap();
    let catalog_hash = bytes_sha256(&catalog_bytes);
    module.fish_profiles[0].catalog.sha256 = catalog_hash;
    let file = module
        .files
        .iter_mut()
        .find(|file| file.path == catalog_path)
        .unwrap();
    file.sha256 = catalog_hash;
    file.bytes = catalog_bytes.len() as u64;
    payloads.insert(catalog_path, catalog_bytes);
    module.validate().unwrap();
    for (path, bytes) in &payloads {
        let output = exported.source_root.join(path);
        fs::create_dir_all(output.parent().unwrap()).unwrap();
        fs::write(output, bytes).unwrap();
    }
    fs::write(
        exported.source_root.join("module.json"),
        module.to_bytes().unwrap(),
    )
    .unwrap();
    let (mut descriptor, _) = forge_delivery::archive::inspect(&exported.package_path).unwrap();
    descriptor
        .license_paths
        .clone_from(&module.provenance.license_paths);
    let package = temp.0.join("foreign-fish.orpmod");
    forge_delivery::archive::pack(&exported.source_root, descriptor, &package).unwrap();
    let error = orpmod::inspect(&package, false).unwrap_err();
    assert!(
        error.contains("Fish catalog asset or package pin differs"),
        "{error}"
    );
    let error = match module_runtime::load(&exported.source_root, module.zones[0].area_id, None) {
        Ok(_) => panic!("A repacked foreign fish identity must not be admitted"),
        Err(error) => error,
    };
    assert!(
        error.contains("Fish catalog asset or package pin differs"),
        "{error}"
    );
}

#[test]
fn fish_module_refuses_redistribution_until_profile_rights_are_qualified() {
    let (temp, exported, mut module, payloads) = fixture();
    module.provenance.rights = ModuleRights::Redistributable;
    for (path, bytes) in &payloads {
        let output = exported.source_root.join(path);
        fs::create_dir_all(output.parent().unwrap()).unwrap();
        fs::write(output, bytes).unwrap();
    }
    fs::write(
        exported.source_root.join("module.json"),
        module.to_bytes().unwrap(),
    )
    .unwrap();
    let (mut descriptor, _) = forge_delivery::archive::inspect(&exported.package_path).unwrap();
    descriptor
        .license_paths
        .clone_from(&module.provenance.license_paths);
    descriptor.rights = forge_delivery::manifest::Rights::Redistributable;
    let package = temp.0.join("unqualified-fish-rights.orpmod");
    forge_delivery::archive::pack(&exported.source_root, descriptor, &package).unwrap();
    let error = orpmod::inspect(&package, false).unwrap_err();
    assert!(
        error.contains("Fish profile redistribution rights are not yet qualified"),
        "{error}"
    );
}

#[test]
fn admission_still_rejects_an_embedded_historical_javascript_source() {
    let (temp, exported, mut module, mut payloads) = fixture();
    module.fish_profiles[0].excluded_source_scripts.clear();
    let bytes = b"historical source only".to_vec();
    let path = "fish/profile/source/original.js".to_owned();
    let hash = bytes_sha256(&bytes);
    module.fish_profiles[0]
        .package
        .resources
        .push(ModuleFilePin {
            path: path.clone(),
            sha256: hash,
        });
    module.files.push(ModuleFile {
        path: path.clone(),
        bytes: bytes.len() as u64,
        sha256: hash,
        role: ModuleFileRole::ObjectSource,
    });
    payloads.insert(path, bytes);
    module.validate().unwrap();
    for (path, bytes) in &payloads {
        let output = exported.source_root.join(path);
        fs::create_dir_all(output.parent().unwrap()).unwrap();
        fs::write(output, bytes).unwrap();
    }
    fs::write(
        exported.source_root.join("module.json"),
        module.to_bytes().unwrap(),
    )
    .unwrap();
    let (mut descriptor, _) = forge_delivery::archive::inspect(&exported.package_path).unwrap();
    descriptor
        .license_paths
        .clone_from(&module.provenance.license_paths);
    let package = temp.0.join("executable-fish-source.orpmod");
    forge_delivery::archive::pack(&exported.source_root, descriptor, &package).unwrap();
    let error = orpmod::inspect(&package, false).unwrap_err();
    assert!(
        error.contains("Program content or extension forbidden"),
        "{error}"
    );
}

fn transport_fish(
    temp: &Temp,
    exported: &ExportedModule,
    module: &ModulePackageManifest,
    payloads: &BTreeMap<String, Vec<u8>>,
) -> Vec<forge_delivery::module_fish::ResolvedFish> {
    let (mut descriptor, _) = forge_delivery::archive::inspect(&exported.package_path).unwrap();
    descriptor
        .license_paths
        .clone_from(&module.provenance.license_paths);
    for (path, bytes) in payloads {
        let output = exported.source_root.join(path);
        fs::create_dir_all(output.parent().unwrap()).unwrap();
        fs::write(output, bytes).unwrap();
    }
    fs::write(
        exported.source_root.join("module.json"),
        module.to_bytes().unwrap(),
    )
    .unwrap();

    let package = temp.0.join("fish-qualification.orpmod");
    let (_, revision) =
        forge_delivery::archive::pack(&exported.source_root, descriptor, &package).unwrap();
    let (packed, inspected_revision) = forge_delivery::archive::inspect(&package).unwrap();
    assert_eq!(inspected_revision, revision);
    assert!(
        packed
            .files
            .iter()
            .all(|file| !file.path.to_ascii_lowercase().ends_with(".js"))
    );
    assert_eq!(orpmod::inspect(&package, false).unwrap(), *module);
    let from_package = module_runtime::load(
        &package,
        module.zones[0].area_id,
        Some(&temp.0.join("fish-cache")),
    )
    .unwrap();
    module_runtime::verify_transport_revision(&from_package, revision.parse().unwrap()).unwrap();
    assert_eq!(
        from_package.fish_profiles().unwrap().len(),
        module.fish_profiles.len()
    );

    let extracted = temp.0.join("extracted");
    forge_delivery::archive::extract(&package, &extracted, &revision).unwrap();
    let loaded = module_runtime::load(&extracted, module.zones[0].area_id, None).unwrap();
    let resolved = loaded.fish_profiles().unwrap();
    let changed_path = extracted.join(&loaded.manifest.fish_profiles[0].package.resources[0].path);
    let mut changed = fs::read(&changed_path).unwrap();
    changed[0] ^= 1;
    fs::write(changed_path, changed).unwrap();
    assert!(loaded.fish_profiles().is_err());
    assert!(module_runtime::load(&extracted, module.zones[0].area_id, None).is_err());
    resolved
}

#[test]
fn module_fish_survives_semantic_admission_and_installed_transport() {
    let (temp, exported, module, payloads) = fixture();
    let resolved = transport_fish(&temp, &exported, &module, &payloads);
    assert_eq!(resolved.len(), 1);
    assert_eq!(
        resolved[0]
            .profile()
            .unwrap()
            .identify(0, "independent/fish")
            .unwrap(),
        Some(module.fish_profiles[0].assets[0].asset_id)
    );
}

fn real_source_profile(
    variable: &str,
    expected: &str,
    description: &str,
    first: usize,
    total: usize,
) {
    let root =
        std::path::PathBuf::from(std::env::var(variable).expect("Private fish source required"));
    let manifest = fs::read(root.join("package.json")).unwrap();
    let package_hash: forge_core::ContentHash = expected.parse().unwrap();
    let index = forge_assets::source_fish_package::resource_index(&manifest, package_hash).unwrap();
    let (temp, exported, mut module, _) = fixture();
    module.fish_profiles.clear();
    module
        .files
        .retain(|file| !file.path.starts_with("fish/profile/"));
    module
        .provenance
        .license_paths
        .retain(|path| !path.starts_with("fish/profile/"));
    let mut payloads = BTreeMap::new();
    let mut resource_pins = Vec::new();
    let excluded_source_scripts: Vec<_> = index
        .files
        .keys()
        .filter(|name| name.ends_with(".js"))
        .cloned()
        .collect();
    assert!(!excluded_source_scripts.is_empty());
    for (name, pin) in &index.files {
        let bytes = fs::read(root.join(name)).unwrap();
        assert_eq!(bytes.len(), pin.bytes);
        assert_eq!(bytes_sha256(&bytes), pin.sha256);
        if excluded_source_scripts.binary_search(name).is_ok() {
            continue;
        }
        let path = format!("fish/profile/{name}");
        let role = if *name == index.license {
            ModuleFileRole::License
        } else {
            ModuleFileRole::ObjectSource
        };
        if role == ModuleFileRole::License {
            module.provenance.license_paths.push(path.clone());
        }
        module.files.push(ModuleFile {
            path: path.clone(),
            bytes: bytes.len() as u64,
            sha256: pin.sha256,
            role,
        });
        resource_pins.push(ModuleFilePin {
            path: path.clone(),
            sha256: pin.sha256,
        });
        payloads.insert(path, bytes);
    }
    let description_path = format!("fish/profile/{description}");
    let document: serde_json::Value = serde_json::from_slice(&payloads[&description_path]).unwrap();
    let instances = document["instances"].as_array().unwrap();
    assert_eq!(instances.len(), total);
    let mut catalog = AssetCatalog {
        schema_version: 1,
        assets: Vec::new(),
    };
    for instance in &instances[first..] {
        let source_id = instance["source_id"].as_str().unwrap();
        catalog.assets.push(AssetRecord {
            id: AssetId::deterministic(source_id),
            logical_name: source_id.into(),
            kind: AssetKind::Mesh,
            source: AssetSource::External {
                uri: "https://github.com/dgreenheck/tidewater".into(),
                declared_license: Some("MIT".into()),
            },
            content_hash: package_hash,
        });
    }
    let mut assets: Vec<_> = catalog
        .assets
        .iter()
        .map(|asset| ModuleObjectAsset {
            asset_id: asset.id,
            sha256: asset.content_hash,
        })
        .collect();
    assets.sort_by_key(|asset| asset.asset_id);
    let catalog_bytes = serde_json::to_vec(&catalog).unwrap();
    let catalog_path = "fish/profile/catalog.json".to_owned();
    let catalog_pin = ModuleFilePin {
        path: catalog_path.clone(),
        sha256: bytes_sha256(&catalog_bytes),
    };
    module.files.push(ModuleFile {
        path: catalog_path.clone(),
        bytes: catalog_bytes.len() as u64,
        sha256: catalog_pin.sha256,
        role: ModuleFileRole::ObjectSource,
    });
    payloads.insert(catalog_path, catalog_bytes);
    let manifest_path = "fish/profile/package.json".to_owned();
    module.files.push(ModuleFile {
        path: manifest_path.clone(),
        bytes: manifest.len() as u64,
        sha256: package_hash,
        role: ModuleFileRole::ObjectSource,
    });
    payloads.insert(manifest_path.clone(), manifest);
    module.fish_profiles.push(ModuleFishBinding {
        area_id: module.zones[0].area_id,
        package: ModuleObjectFiles {
            manifest: ModuleFilePin {
                path: manifest_path,
                sha256: package_hash,
            },
            resources: resource_pins,
        },
        catalog: catalog_pin,
        excluded_source_scripts,
        description: description.into(),
        prototypes: 18,
        instances: u32::try_from(total).unwrap(),
        assets,
    });
    module.validate().unwrap();
    let resolved =
        forge_delivery::module_fish::resolve(&module, module.zones[0].area_id, |path, _| {
            payloads.get(path).cloned().ok_or("missing".into())
        })
        .unwrap();
    assert_eq!(resolved.len(), 1);
    let profile = resolved[0].profile().unwrap();
    for (ordinal, instance) in instances.iter().enumerate() {
        let source_id = instance["source_id"].as_str().unwrap();
        let expected = (ordinal >= first).then(|| AssetId::deterministic(source_id));
        assert_eq!(profile.identify(ordinal, source_id).unwrap(), expected);
    }
    let archived = transport_fish(&temp, &exported, &module, &payloads);
    assert_eq!(archived.len(), 1);
    let archived_profile = archived[0].profile().unwrap();
    for (ordinal, instance) in instances.iter().enumerate() {
        let source_id = instance["source_id"].as_str().unwrap();
        let expected = (ordinal >= first).then(|| AssetId::deterministic(source_id));
        assert_eq!(
            archived_profile.identify(ordinal, source_id).unwrap(),
            expected
        );
    }
}

#[test]
#[ignore = "requires the byte-pinned private village fish package"]
fn real_village_source_closure_is_resolved_from_explicit_module_resources() {
    real_source_profile(
        "FORGE_TIDEWATER_FISH_PROP_PACKAGE",
        "e578b44e93d18d79ec06c875b76dae005f71c17ecf1d0ff09aa3ecddd651330a",
        "geometry/fish-props.json",
        0,
        78,
    );
}

#[test]
#[ignore = "requires the byte-pinned private game fish package"]
fn real_game_fish_closure_excludes_parked_slots_in_module_resources() {
    real_source_profile(
        "FORGE_TIDEWATER_GAME_FISH_PACKAGE",
        "f26aceb2fdcee7f9e2261f90bd7e75fa7d66b672eb854dc66caa453c904c456d",
        "geometry/game-fish.json",
        18,
        23,
    );
}
