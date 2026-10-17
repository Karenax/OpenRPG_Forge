use forge_assets::governed_import::GovernedAssetKind;
use forge_core::{AreaId, AssetId, EntityId, Transform, bytes_sha256};
use forge_project::module_gameplay_visuals::{GameplayVisualBinding, VisualAssetPin};
use forge_studio_frontend::{
    asset_import_protocol::{ImportUpload, ImportedFile},
    gameplay_visual_native::verify_models,
};

fn fixture() -> (GameplayVisualBinding, ImportedFile) {
    let bytes = b"already admitted native model".to_vec();
    let asset_id = AssetId::deterministic("policy-model");
    let sha256 = bytes_sha256(&bytes);
    (
        GameplayVisualBinding {
            area_id: AreaId::deterministic("policy-area"),
            entity_id: EntityId::deterministic("policy-actor"),
            asset: VisualAssetPin { asset_id, sha256 },
            local_transform: Transform::IDENTITY,
        },
        ImportedFile {
            asset_id,
            sha256,
            catalog_sha256: bytes_sha256(b"native catalog"),
            kind: GovernedAssetKind::Mesh,
            mime_type: "model/gltf-binary".into(),
            upload: ImportUpload {
                name: "model.glb".into(),
                byte_length: bytes.len() as u64,
                bytes,
            },
        },
    )
}

#[test]
fn absent_visuals_leave_legacy_zones_independent_of_the_asset_library() {
    verify_models(&[], |_, _| panic!("No visual lookup is needed")).unwrap();
}

#[test]
fn exact_native_pin_is_resolved_once_when_shared_by_gameplay_objects() {
    let (binding, model) = fixture();
    let mut second = binding.clone();
    second.entity_id = EntityId::deterministic("policy-second-actor");
    let mut calls = 0;
    verify_models(&[binding.clone(), second], |id, pin| {
        assert_eq!((id, pin), (binding.asset.asset_id, binding.asset.sha256));
        calls += 1;
        Ok(Some(model.clone()))
    })
    .unwrap();
    assert_eq!(calls, 1);
}

#[test]
fn missing_wrong_kind_or_changed_payload_cannot_be_accepted_from_webview_pins() {
    let (binding, model) = fixture();
    assert!(verify_models(std::slice::from_ref(&binding), |_, _| Ok(None)).is_err());
    for property in 0..4 {
        let mut bad = model.clone();
        match property {
            0 => bad.kind = GovernedAssetKind::Audio,
            1 => bad.upload.bytes[0] ^= 1,
            2 => bad.sha256 = bytes_sha256(b"other pin"),
            _ => bad.asset_id = AssetId::deterministic("other identity"),
        }
        assert!(
            verify_models(std::slice::from_ref(&binding), |_, _| Ok(Some(bad.clone()))).is_err()
        );
    }
}
