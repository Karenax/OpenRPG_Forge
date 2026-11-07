use forge_core::{AssetId, CommandId, ContentHash, EntityId, Transform, Vec3};
use forge_project::{
    authoring::new_terrain,
    zone_authoring::{
        placements::{
            LocalLightAnchor, LocalLightPackageReference, ManualPlacement, PlacementChanges,
            PlacementEdit,
        },
        session::{ZoneAction, ZoneEditSession, ZoneRequest},
    },
};

fn object() -> ManualPlacement {
    let carrier = ContentHash::from_bytes([1; 32]);
    ManualPlacement {
        id: EntityId::deterministic("light-object"),
        name: "Lampe retouchée".into(),
        asset_id: AssetId::deterministic("light-carrier"),
        asset_hash: carrier,
        physics: None,
        particles: None,
        local_lights: Some(Box::new(LocalLightPackageReference {
            schema_version: 1,
            carrier_sha256: carrier,
            manifest_sha256: ContentHash::from_bytes([2; 32]),
            descriptor_sha256: ContentHash::from_bytes([3; 32]),
            light_ids: vec![ContentHash::from_bytes([4; 32])],
            anchors: vec![LocalLightAnchor {
                frame_id: "owner".into(),
                transform: Transform::IDENTITY,
            }],
        })),
        transform: Transform::from_translation(Vec3::new(2.0, 3.0, 4.0)),
        locked: false,
    }
}
fn session() -> ZoneEditSession {
    ZoneEditSession::from_initial_terrain(new_terrain("saved-lights", "area", "Area").unwrap())
        .unwrap()
}
fn apply(s: &mut ZoneEditSession, edit: PlacementEdit, name: &str) {
    let request = s
        .head()
        .prepare_placement(CommandId::deterministic(name), edit)
        .unwrap();
    s.execute(&request).unwrap();
}
fn history(s: &mut ZoneEditSession, action: ZoneAction, name: &str) {
    s.execute(&ZoneRequest {
        command_id: CommandId::deterministic(name),
        expected_hash: s.head().content_hash().unwrap(),
        expected_revision: s.head().revision,
        action,
    })
    .unwrap();
}
fn replacement(
    source: &ManualPlacement,
    expected: Option<Box<LocalLightPackageReference>>,
    target: Option<Box<LocalLightPackageReference>>,
) -> PlacementEdit {
    PlacementEdit::ReplacePinnedAsset {
        ids: vec![source.id],
        expected_asset_id: source.asset_id,
        expected_asset_hash: source.asset_hash,
        expected_physics: source.physics.clone(),
        expected_particles: source.particles.clone(),
        expected_local_lights: expected,
        asset_id: source.asset_id,
        asset_hash: source.asset_hash,
        physics: source.physics.clone(),
        particles: source.particles.clone(),
        local_lights: target,
    }
}

#[test]
fn saved_light_pins_and_anchors_survive_copy_transform_all_archives_and_history() {
    let mut s = session();
    let original = object();
    apply(
        &mut s,
        PlacementEdit::Insert {
            placement: original.clone(),
        },
        "insert",
    );
    let copy = EntityId::deterministic("copy-light");
    apply(
        &mut s,
        PlacementEdit::Duplicate {
            source_id: original.id,
            id: copy,
            transform: Transform::from_translation(Vec3::new(7.0, 8.0, 9.0)),
        },
        "copy",
    );
    for p in s.head().corrections.placements.as_ref().unwrap() {
        assert_eq!(p.local_lights, original.local_lights);
    }
    apply(
        &mut s,
        PlacementEdit::Transform {
            id: original.id,
            transform: Transform::IDENTITY,
        },
        "move",
    );
    let transformed = s.head().corrections.clone();
    assert_eq!(s.head().version, 18);
    let mut mislabeled = serde_json::to_value(s.head()).unwrap();
    mislabeled["version"] = serde_json::json!(17);
    let mislabeled: forge_project::zone_authoring::ZoneAuthoringHead =
        serde_json::from_value(mislabeled).unwrap();
    assert!(mislabeled.validate().is_err());
    for (bytes, version) in [
        (s.archive_bytes().unwrap(), 49),
        (s.compact_archive_bytes().unwrap(), 50),
        (s.delta_archive_bytes().unwrap(), 51),
    ] {
        assert_eq!(
            serde_json::from_slice::<serde_json::Value>(&bytes).unwrap()["version"],
            version
        );
        let mut reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
        assert_eq!(reopened.head(), s.head());
        history(&mut reopened, ZoneAction::Undo, "undo-move");
        assert_eq!(
            reopened
                .head()
                .corrections
                .placements
                .as_ref()
                .unwrap()
                .iter()
                .find(|p| p.id == original.id)
                .unwrap(),
            &original
        );
        history(&mut reopened, ZoneAction::Redo, "redo-move");
        assert_eq!(reopened.head().corrections, transformed);
        let current = reopened
            .head()
            .corrections
            .placements
            .as_ref()
            .unwrap()
            .iter()
            .find(|p| p.id == original.id)
            .unwrap()
            .clone();
        apply(
            &mut reopened,
            replacement(&current, current.local_lights.clone(), None),
            "remove-lights",
        );
        assert!(
            reopened
                .head()
                .corrections
                .placements
                .as_ref()
                .unwrap()
                .iter()
                .find(|p| p.id == original.id)
                .unwrap()
                .local_lights
                .is_none()
        );
        history(&mut reopened, ZoneAction::Undo, "restore-lights");
        assert_eq!(reopened.head().corrections, transformed);
    }
}

#[test]
fn light_reference_replacement_is_pinned_and_legacy_absence_stays_omitted() {
    let original = object();
    let mut s = session();
    apply(
        &mut s,
        PlacementEdit::Insert {
            placement: original.clone(),
        },
        "insert",
    );
    let before = s.head().content_hash().unwrap();
    assert!(
        s.head()
            .prepare_placement(
                CommandId::deterministic("stale"),
                replacement(&original, None, None)
            )
            .is_err()
    );
    assert!(
        s.head()
            .prepare_placement(
                CommandId::deterministic("implicit"),
                PlacementEdit::ReplaceAsset {
                    ids: vec![original.id],
                    expected_asset_id: original.asset_id,
                    expected_asset_hash: original.asset_hash,
                    asset_id: AssetId::deterministic("other"),
                    asset_hash: ContentHash::from_bytes([8; 32])
                }
            )
            .is_err()
    );
    assert_eq!(s.head().content_hash().unwrap(), before);
    let mut target = original.local_lights.clone().unwrap();
    target.manifest_sha256 = ContentHash::from_bytes([9; 32]);
    apply(
        &mut s,
        replacement(&original, original.local_lights.clone(), Some(target)),
        "new-pin",
    );
    assert_eq!(
        PlacementChanges::between(
            std::slice::from_ref(&original),
            s.head().corrections.placements.as_ref().unwrap()
        )
        .unwrap()
        .references,
        1
    );
    let mut legacy = serde_json::to_value(&original).unwrap();
    legacy.as_object_mut().unwrap().remove("local_lights");
    let decoded: ManualPlacement = serde_json::from_value(legacy.clone()).unwrap();
    assert!(decoded.local_lights.is_none());
    assert_eq!(serde_json::to_value(decoded).unwrap(), legacy);
}

#[test]
fn malformed_light_references_are_rejected_before_authoring_changes() {
    let original = object();
    let source = original.local_lights.clone().unwrap();
    for index in 0..8 {
        let mut p = original.clone();
        let r = p.local_lights.as_mut().unwrap();
        match index {
            0 => r.schema_version = 2,
            1 => r.carrier_sha256 = ContentHash::from_bytes([9; 32]),
            2 => r.light_ids.clear(),
            3 => r.light_ids.push(r.light_ids[0]),
            4 => r.anchors.clear(),
            5 => r.anchors.push(r.anchors[0].clone()),
            6 => r.anchors[0].frame_id = "bad\0anchor".into(),
            _ => r.anchors[0].transform.scale = Vec3::ZERO,
        }
        let s = session();
        assert!(
            s.head()
                .prepare_placement(
                    CommandId::deterministic(format!("bad-{index}")),
                    PlacementEdit::Insert { placement: p }
                )
                .is_err()
        );
    }
    source.validate(original.asset_hash).unwrap();
}

#[test]
fn light_package_closure_survives_removal_undo_redo_and_all_archive_encodings() {
    let mut s = session();
    let original = object();
    let reference = original.local_lights.as_ref().unwrap();
    let expected = std::collections::BTreeMap::from([(
        reference.manifest_sha256,
        reference.descriptor_sha256,
    )]);
    apply(
        &mut s,
        PlacementEdit::Insert {
            placement: original.clone(),
        },
        "insert",
    );
    apply(
        &mut s,
        replacement(&original, original.local_lights.clone(), None),
        "remove-lights",
    );
    assert!(
        s.head().corrections.placements.as_ref().unwrap()[0]
            .local_lights
            .is_none()
    );
    assert_eq!(s.required_local_light_packages().unwrap(), expected);
    for bytes in [
        s.archive_bytes().unwrap(),
        s.compact_archive_bytes().unwrap(),
        s.delta_archive_bytes().unwrap(),
    ] {
        let mut reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
        assert_eq!(reopened.required_local_light_packages().unwrap(), expected);
        history(&mut reopened, ZoneAction::Undo, "undo");
        assert_eq!(reopened.required_local_light_packages().unwrap(), expected);
        history(&mut reopened, ZoneAction::Redo, "redo");
        assert_eq!(reopened.required_local_light_packages().unwrap(), expected);
    }
    let mut changed = original.local_lights.clone().unwrap();
    changed.descriptor_sha256 = ContentHash::from_bytes([9; 32]);
    let current = s.head().corrections.placements.as_ref().unwrap()[0].clone();
    apply(
        &mut s,
        replacement(&current, None, Some(changed)),
        "conflicting-pins",
    );
    assert!(s.required_local_light_packages().is_err());
}

#[test]
fn copied_source_object_light_is_retained_across_archive_and_undo_history() {
    use forge_project::zone_authoring::source_objects::{SourceObjectEdit, SourceObjectState};

    let mut session = session();
    let mut source = object();
    source.local_lights = None;
    let mut copy = object();
    copy.id = EntityId::deterministic("source-copy-with-light");
    let light = copy.local_lights.as_ref().unwrap();
    let expected =
        std::collections::BTreeMap::from([(light.manifest_sha256, light.descriptor_sha256)]);
    let mut corrections = session.head().corrections.clone();
    corrections.source_object_edits = Some(vec![SourceObjectEdit {
        source,
        state: SourceObjectState::Removed,
        copies: vec![copy],
    }]);
    session
        .execute(&ZoneRequest {
            command_id: CommandId::deterministic("source-copy-light"),
            expected_hash: session.head().content_hash().unwrap(),
            expected_revision: session.head().revision,
            action: ZoneAction::edit(corrections),
        })
        .unwrap();
    assert_eq!(session.required_local_light_packages().unwrap(), expected);
    for bytes in [
        session.archive_bytes().unwrap(),
        session.compact_archive_bytes().unwrap(),
        session.delta_archive_bytes().unwrap(),
    ] {
        let mut reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
        assert_eq!(reopened.required_local_light_packages().unwrap(), expected);
        history(&mut reopened, ZoneAction::Undo, "undo-source-copy-light");
        assert_eq!(reopened.required_local_light_packages().unwrap(), expected);
        history(&mut reopened, ZoneAction::Redo, "redo-source-copy-light");
        assert_eq!(reopened.required_local_light_packages().unwrap(), expected);
    }
}
