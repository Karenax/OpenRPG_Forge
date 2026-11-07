use super::*;
use forge_project::module_package::{
    MODULE_OBJECTS_API_VERSION, ModuleFilePin, ModuleObjectAsset, ModuleObjectBinding,
    ModuleObjectFiles, ModuleObjectSource,
};

pub(super) fn with_objects(key: &str) -> (ModulePackageManifest, Vec<u8>) {
    let (mut module, archive) = fixture(key);
    module.compatibility.module_api_version = MODULE_OBJECTS_API_VERSION;
    let manifest = ModuleFilePin {
        path: "objects/profile/package.json".into(),
        sha256: bytes_sha256(b"index"),
    };
    let resource = ModuleFilePin {
        path: "objects/profile/model.bin".into(),
        sha256: bytes_sha256(b"geometry"),
    };
    for (pin, bytes) in [(&manifest, 5), (&resource, 8)] {
        module.files.push(ModuleFile {
            path: pin.path.clone(),
            sha256: pin.sha256,
            bytes,
            role: ModuleFileRole::ObjectSource,
        });
    }
    module.objects.push(ModuleObjectBinding {
        area_id: module.zones[0].area_id,
        source: ModuleObjectSource::SourceSurfaces {
            package: ModuleObjectFiles {
                manifest,
                resources: vec![resource],
            },
            assets: vec![ModuleObjectAsset {
                asset_id: AssetId::deterministic("explicit source object"),
                sha256: bytes_sha256(b"descriptor"),
            }],
        },
    });
    (module, archive)
}

#[test]
fn old_apis_keep_their_wire_shape_and_refuse_even_empty_future_object_fields() {
    let (legacy, _) = fixture("legacy-objects-wire");
    let bytes = legacy.to_bytes().unwrap();
    let mut value: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
    assert!(value.get("objects").is_none());
    assert_eq!(
        ModulePackageManifest::from_bytes(&bytes)
            .unwrap()
            .to_bytes()
            .unwrap(),
        bytes
    );
    value["objects"] = serde_json::json!([]);
    assert!(ModulePackageManifest::from_bytes(&serde_json::to_vec(&value).unwrap()).is_err());
}

#[test]
fn object_binding_requires_supported_area_role_pins_and_disjoint_asset_identity() {
    let (module, _) = with_objects("objects-contract");
    module.validate().unwrap();
    assert_eq!(
        ModulePackageManifest::from_bytes(&module.to_bytes().unwrap()).unwrap(),
        module
    );
    let mut old = module.clone();
    old.compatibility.module_api_version = 6;
    assert!(old.validate().is_err());
    let mut foreign = module.clone();
    foreign.objects[0].area_id = AreaId::deterministic("absent area");
    assert!(foreign.validate().is_err());
    let mut duplicate = module.clone();
    duplicate.objects.push(module.objects[0].clone());
    assert!(duplicate.validate().is_err());
    let mut role = module.clone();
    role.files.last_mut().unwrap().role = ModuleFileRole::AssetAuxiliary;
    assert!(role.validate().is_err());
    let mut stale = module.clone();
    stale.files.last_mut().unwrap().sha256 = bytes_sha256(b"stale");
    assert!(stale.validate().is_err());
    let mut collision = module.clone();
    let ModuleObjectSource::SourceSurfaces { assets, .. } = &mut collision.objects[0].source;
    assets[0].asset_id = collision.assets[0].asset_id;
    assert!(collision.validate().is_err());
    let mut large = module;
    large.files.last_mut().unwrap().bytes =
        forge_project::module_package::objects::MAX_MODULE_OBJECT_BYTES + 1;
    assert!(large.validate().is_err());
}

#[test]
fn only_explicit_object_asset_pins_authorize_canonical_placements() {
    let (mut module, archive) = with_objects("objects-placement");
    let mut zone = ZoneEditSession::from_archive_bytes(&archive).unwrap();
    let ModuleObjectSource::SourceSurfaces { assets, .. } = &module.objects[0].source;
    let mut corrections = zone.head().corrections.clone();
    corrections.placements = Some(vec![
        forge_project::zone_authoring::placements::ManualPlacement {
            id: forge_core::EntityId::deterministic("surface placement"),
            name: "Source object".into(),
            asset_id: assets[0].asset_id,
            asset_hash: assets[0].sha256,
            transform: forge_core::Transform::IDENTITY,
            locked: false,
            local_lights: None,
            physics: None,
            particles: None,
        },
    ]);
    zone.execute(&forge_project::zone_authoring::session::ZoneRequest {
        command_id: forge_core::CommandId::deterministic("place source object"),
        expected_hash: zone.head().content_hash().unwrap(),
        expected_revision: zone.head().revision,
        action: forge_project::zone_authoring::session::ZoneAction::edit(corrections),
    })
    .unwrap();
    let archive = repin_archive(&mut module, &zone);
    module
        .validate_zone_archive(module.zones[0].area_id, &archive)
        .unwrap();
    let mut absent = module.clone();
    absent.objects.clear();
    assert!(
        absent
            .validate_zone_archive(absent.zones[0].area_id, &archive)
            .is_err()
    );
    let ModuleObjectSource::SourceSurfaces { assets, .. } = &mut module.objects[0].source;
    assets[0].sha256 = bytes_sha256(b"different object revision");
    assert!(
        module
            .validate_zone_archive(module.zones[0].area_id, &archive)
            .is_err()
    );
}
