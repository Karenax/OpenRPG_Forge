//! Real pinned water packages attached only to isolated distribution fixtures.
use forge_assets::{
    source_shore_sim_package::ShoreSimPackage, source_water_surface_package::WaterSurfacePackage,
};
use forge_core::{ContentHash, bytes_sha256};
use forge_project::module_package::{
    MODULE_OCEAN_SURFACE_API_VERSION, ModuleFile, ModuleFilePin, ModuleFileRole, ModuleOceanFiles,
    ModulePackageManifest,
};
use std::{
    collections::BTreeMap,
    fs,
    path::{Path, PathBuf},
};

fn input(name: &str) -> PathBuf {
    let root = PathBuf::from(std::env::var_os(name).expect("explicit package path"));
    assert!(root.is_absolute());
    forge_delivery::manifest::safe_chain(&root).unwrap();
    root
}

fn copy_package(
    source: &Path,
    module: &mut ModulePackageManifest,
    root: &Path,
    base: &str,
    index: &[u8],
    pins: BTreeMap<String, (ContentHash, usize)>,
    role: ModuleFileRole,
) -> ModuleOceanFiles {
    let index_path = format!("{base}/package.json");
    fs::create_dir_all(source.join(base)).unwrap();
    fs::write(source.join(&index_path), index).unwrap();
    let manifest = ModuleFilePin {
        path: index_path.clone(),
        sha256: bytes_sha256(index),
    };
    module.files.push(ModuleFile {
        path: index_path,
        bytes: index.len() as u64,
        sha256: manifest.sha256,
        role,
    });
    let mut resources = Vec::new();
    for (name, (hash, length)) in pins {
        let data = fs::read(root.join(&name)).unwrap();
        assert_eq!(data.len(), length);
        assert_eq!(bytes_sha256(&data), hash);
        let path = format!("{base}/{name}");
        fs::write(source.join(&path), &data).unwrap();
        let license = name == "LICENSE-Tidewater.txt";
        if license {
            module.provenance.license_paths.push(path.clone());
        }
        module.files.push(ModuleFile {
            path: path.clone(),
            bytes: data.len() as u64,
            sha256: hash,
            role: if license {
                ModuleFileRole::License
            } else {
                role
            },
        });
        resources.push(ModuleFilePin { path, sha256: hash });
    }
    ModuleOceanFiles {
        manifest,
        resources,
    }
}

pub fn attach_real_profiles(source: &Path, module: &mut ModulePackageManifest) {
    let surface = input("FORGE_WATER_SURFACE_PACKAGE");
    let shore = input("FORGE_SHORE_PACKAGE");
    let water_pin: ContentHash = std::env::var("FORGE_WATER_SURFACE_PACKAGE_SHA256")
        .unwrap()
        .parse()
        .unwrap();
    let shore_pin: ContentHash = std::env::var("FORGE_SHORE_PACKAGE_SHA256")
        .unwrap()
        .parse()
        .unwrap();
    let water_index = fs::read(surface.join("package.json")).unwrap();
    let shore_index = fs::read(shore.join("package.json")).unwrap();
    let water = WaterSurfacePackage::resource_pins(&water_index, water_pin)
        .unwrap()
        .into_iter()
        .map(|(name, pin)| (name, (pin.sha256, pin.bytes)))
        .collect();
    let sim = ShoreSimPackage::resource_pins(&shore_index, shore_pin)
        .unwrap()
        .into_iter()
        .map(|(name, pin)| (name, (pin.sha256, pin.bytes)))
        .collect();
    let water = copy_package(
        source,
        module,
        &surface,
        "ocean/surface",
        &water_index,
        water,
        ModuleFileRole::OceanSurface,
    );
    let sim = copy_package(
        source,
        module,
        &shore,
        "ocean/shore",
        &shore_index,
        sim,
        ModuleFileRole::ShoreSimulation,
    );
    assert_eq!(module.ocean_inputs.len(), 1);
    assert!(module.ocean_inputs[0].surface.is_none() && module.ocean_inputs[0].shore_sim.is_none());
    module.ocean_inputs[0].surface = Some(water);
    module.ocean_inputs[0].shore_sim = Some(sim);
    module.compatibility.module_api_version = MODULE_OCEAN_SURFACE_API_VERSION;
    fs::write(source.join("module.json"), module.to_bytes().unwrap()).unwrap();
}
