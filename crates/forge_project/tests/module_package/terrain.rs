use super::*;
use forge_project::module_package::{
    MODULE_TERRAIN_API_VERSION, ModuleFilePin, ModuleTerrainBinding, ModuleTerrainFiles,
    ModuleTerrainSource,
};

pub(super) fn with_terrain(key: &str) -> (ModulePackageManifest, Vec<u8>) {
    let (mut module, archive) = fixture(key);
    module.compatibility.module_api_version = MODULE_TERRAIN_API_VERSION;
    let manifest = ModuleFilePin {
        path: "terrain/profile/package.json".into(),
        sha256: bytes_sha256(b"index"),
    };
    let resource = ModuleFilePin {
        path: "terrain/profile/texture.bin".into(),
        sha256: bytes_sha256(b"texture"),
    };
    for (pin, bytes) in [(&manifest, 5), (&resource, 7)] {
        module.files.push(ModuleFile {
            path: pin.path.clone(),
            sha256: pin.sha256,
            bytes,
            role: ModuleFileRole::TerrainSurface,
        });
    }
    module.terrain_surfaces.push(ModuleTerrainBinding {
        area_id: module.zones[0].area_id,
        source: ModuleTerrainSource::SourceRaster {
            source_descriptor_sha256: bytes_sha256(b"descriptor"),
            package: ModuleTerrainFiles {
                manifest,
                resources: vec![resource],
            },
        },
    });
    (module, archive)
}

#[test]
fn terrain_bindings_are_explicit_closed_and_admitted_by_supported_apis() {
    let (module, archive) = with_terrain("terrain-binding");
    module.validate().unwrap();
    assert_eq!(
        ModulePackageManifest::from_bytes(&module.to_bytes().unwrap()).unwrap(),
        module
    );
    // The archive is procedural: a declared raster companion cannot invent a source.
    assert!(
        module
            .validate_zone_archive(module.zones[0].area_id, &archive)
            .is_err()
    );
    let mut old = module.clone();
    old.compatibility.module_api_version = 7;
    assert!(old.validate().is_err());
    let mut newer = module.clone();
    newer.compatibility.module_api_version =
        forge_project::module_package::MODULE_OCEAN_API_VERSION;
    newer.validate().unwrap();
    let mut foreign = module.clone();
    foreign.terrain_surfaces[0].area_id = AreaId::deterministic("foreign terrain area");
    assert!(foreign.validate().is_err());
    let mut duplicate = module.clone();
    duplicate
        .terrain_surfaces
        .push(module.terrain_surfaces[0].clone());
    assert!(duplicate.validate().is_err());
    let mut wrong_role = module.clone();
    wrong_role.files.last_mut().unwrap().role = ModuleFileRole::AssetAuxiliary;
    assert!(wrong_role.validate().is_err());
    let mut stale = module;
    stale.files.last_mut().unwrap().sha256 = bytes_sha256(b"other texture");
    assert!(stale.validate().is_err());
}

#[test]
fn old_module_encodings_omit_terrain_and_reject_even_an_empty_new_field() {
    for api in 1..MODULE_TERRAIN_API_VERSION {
        let (mut module, _) = fixture("old-terrain-contract");
        module.compatibility.module_api_version = api;
        let bytes = module.to_bytes().unwrap();
        let mut value: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
        assert!(value.get("terrain_surfaces").is_none());
        assert_eq!(ModulePackageManifest::from_bytes(&bytes).unwrap(), module);
        value["terrain_surfaces"] = serde_json::json!([]);
        assert!(ModulePackageManifest::from_bytes(&serde_json::to_vec(&value).unwrap()).is_err());
    }
}

#[test]
fn terrain_source_identity_includes_the_spatial_frame_and_not_only_samples() {
    let (mut module, archive) = with_terrain("terrain-frame");
    let session = ZoneEditSession::from_archive_bytes(&archive).unwrap();
    let mut document = session.head().effective().unwrap().0;
    document.sampled_source = Some(forge_project::sampled_terrain::SampledTerrainSource {
        schema: forge_project::sampled_terrain::SAMPLED_TERRAIN_SCHEMA.into(),
        schema_version: 1,
        width: 64,
        height: 64,
        origin_xz_m: [-256.0; 2],
        sample_spacing_m: 8.0,
        outside_height_m: -90.0,
        encoding: forge_project::sampled_terrain::HeightEncoding::Float32Le,
        samples_sha256: bytes_sha256(b"the same samples"),
    });
    let ModuleTerrainSource::SourceRaster {
        source_descriptor_sha256,
        ..
    } = &mut module.terrain_surfaces[0].source;
    *source_descriptor_sha256 =
        forge_core::canonical_json_sha256(document.sampled_source.as_ref().unwrap()).unwrap();
    module.validate_terrain_document(&document).unwrap();
    let mut changed = document.clone();
    changed.sampled_source.as_mut().unwrap().origin_xz_m[0] += 1.0;
    assert!(module.validate_terrain_document(&changed).is_err());
    let mut changed = document.clone();
    changed.sampled_source.as_mut().unwrap().outside_height_m = -1.0;
    assert!(module.validate_terrain_document(&changed).is_err());
    document.project_id = forge_core::ProjectId::deterministic("other terrain project");
    assert!(module.validate_terrain_document(&document).is_err());
}
