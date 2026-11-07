use super::*;
use forge_project::module_package::{
    MODULE_OCEAN_API_VERSION, MODULE_OCEAN_SURFACE_API_VERSION, MODULE_TERRAIN_API_VERSION,
    ModuleFilePin, ModuleOceanBinding, ModuleOceanFiles,
};

pub(super) fn with_ocean() -> ModulePackageManifest {
    let (mut module, _) = fixture("ocean-contract");
    module.compatibility.module_api_version = MODULE_OCEAN_API_VERSION;
    let manifest = ModuleFilePin {
        path: "ocean/package.json".into(),
        sha256: bytes_sha256(b"index"),
    };
    let resource = ModuleFilePin {
        path: "ocean/fft.wgsl".into(),
        sha256: bytes_sha256(b"shader"),
    };
    for pin in [&manifest, &resource] {
        module.files.push(ModuleFile {
            path: pin.path.clone(),
            bytes: 5,
            sha256: pin.sha256,
            role: ModuleFileRole::OceanSimulation,
        });
    }
    module.ocean_inputs.push(ModuleOceanBinding {
        area_id: module.zones[0].area_id,
        surface: None,
        shore_sim: None,
        package: ModuleOceanFiles {
            manifest,
            resources: vec![resource],
        },
    });
    module
}

#[test]
fn ocean_bindings_require_their_area_api_and_closed_role_pins() {
    let module = with_ocean();
    module.validate().unwrap();
    assert_eq!(
        ModulePackageManifest::from_bytes(&module.to_bytes().unwrap()).unwrap(),
        module
    );
    let mut altered = module.clone();
    altered.compatibility.module_api_version = MODULE_TERRAIN_API_VERSION;
    assert!(altered.validate().is_err());
    let mut altered = module.clone();
    altered.ocean_inputs[0].area_id = AreaId::deterministic("other area");
    assert!(altered.validate().is_err());
    let mut altered = module.clone();
    altered.ocean_inputs.push(module.ocean_inputs[0].clone());
    assert!(altered.validate().is_err());
    let mut altered = module.clone();
    altered.files.last_mut().unwrap().sha256 = bytes_sha256(b"other");
    assert!(altered.validate().is_err());
    let mut altered = module.clone();
    altered.files.last_mut().unwrap().role = ModuleFileRole::AssetAuxiliary;
    assert!(altered.validate().is_err());
    let mut altered = module;
    let duplicate = altered.ocean_inputs[0].package.manifest.clone();
    altered.ocean_inputs[0].package.resources.push(duplicate);
    assert!(altered.validate().is_err());
}

#[test]
fn older_api_encodings_reject_even_an_empty_ocean_field() {
    for api in 1..MODULE_OCEAN_API_VERSION {
        let (mut module, _) = fixture("old-ocean-contract");
        module.compatibility.module_api_version = api;
        let bytes = module.to_bytes().unwrap();
        let mut value: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
        assert!(value.get("ocean_inputs").is_none());
        value["ocean_inputs"] = serde_json::json!([]);
        assert!(ModulePackageManifest::from_bytes(&serde_json::to_vec(&value).unwrap()).is_err());
    }
}

#[test]
fn optical_and_shore_inputs_are_paired_and_require_the_new_api() {
    let mut module = with_ocean();
    let make = |module: &mut ModulePackageManifest, base: &str, role| {
        let manifest = ModuleFilePin {
            path: format!("{base}/package.json"),
            sha256: bytes_sha256(b"index"),
        };
        let resource = ModuleFilePin {
            path: format!("{base}/resource.bin"),
            sha256: bytes_sha256(b"data"),
        };
        for pin in [&manifest, &resource] {
            module.files.push(ModuleFile {
                path: pin.path.clone(),
                bytes: 5,
                sha256: pin.sha256,
                role,
            });
        }
        ModuleOceanFiles {
            manifest,
            resources: vec![resource],
        }
    };
    let surface = make(&mut module, "water", ModuleFileRole::OceanSurface);
    let shore = make(&mut module, "shore", ModuleFileRole::ShoreSimulation);
    module.ocean_inputs[0].surface = Some(surface);
    module.ocean_inputs[0].shore_sim = Some(shore);
    assert!(module.validate().is_err());
    module.compatibility.module_api_version = MODULE_OCEAN_SURFACE_API_VERSION;
    module.validate().unwrap();
    assert_eq!(
        ModulePackageManifest::from_bytes(&module.to_bytes().unwrap()).unwrap(),
        module
    );
    let mut incomplete = module.clone();
    incomplete.ocean_inputs[0].shore_sim = None;
    assert!(incomplete.validate().is_err());
    let mut wrong_role = module;
    wrong_role.files.last_mut().unwrap().role = ModuleFileRole::OceanSurface;
    assert!(wrong_role.validate().is_err());
}

#[test]
fn api_nine_refuses_even_null_future_surface_fields() {
    let module = with_ocean();
    let bytes = module.to_bytes().unwrap();
    let mut value: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
    assert!(value["ocean_inputs"][0].get("surface").is_none());
    value["ocean_inputs"][0]["surface"] = serde_json::Value::Null;
    assert!(ModulePackageManifest::from_bytes(&serde_json::to_vec(&value).unwrap()).is_err());
}
