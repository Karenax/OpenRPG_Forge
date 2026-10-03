use forge_core::{AssetId, CommandId, ContentHash, EntityId, Transform, UnitQuaternion, Vec3};
use forge_project::{
    authoring::new_terrain,
    zone_authoring::{
        placements::{ManualPlacement, PlacementEdit},
        session::{ZoneAction, ZoneEditSession, ZoneRequest},
        source_objects::{self, SourceObjectState},
    },
};

fn session() -> ZoneEditSession {
    ZoneEditSession::from_initial_terrain(new_terrain("source-objects", "zone", "Zone").unwrap())
        .unwrap()
}

fn source() -> ManualPlacement {
    ManualPlacement {
        id: EntityId::deterministic("generic-source-object"),
        name: "Generic source object".into(),
        asset_id: AssetId::deterministic("generic-source-asset"),
        asset_hash: ContentHash::from_bytes([0x31; 32]),
        transform: Transform::from_translation(Vec3::new(-4.0, 2.0, 6.0)),
        locked: false,
    }
}

fn execute_source(
    session: &mut ZoneEditSession,
    source: &ManualPlacement,
    edit: PlacementEdit,
    label: &str,
) {
    let request = session
        .head()
        .prepare_source_object(source.clone(), edit, CommandId::deterministic(label))
        .unwrap();
    session.execute(&request).unwrap();
}

fn execute_history(session: &mut ZoneEditSession, action: ZoneAction, label: &str) {
    let request = ZoneRequest {
        command_id: CommandId::deterministic(label),
        expected_revision: session.head().revision,
        expected_hash: session.head().content_hash().unwrap(),
        action,
    };
    session.execute(&request).unwrap();
}

#[test]
#[allow(clippy::float_cmp)]
fn source_objects_use_the_common_transform_duplicate_and_lock_contract() {
    let mut session = session();
    let source = source();
    let transformed = Transform {
        translation_m: Vec3::new(17.25, -3.5, 42.75),
        rotation: UnitQuaternion::new(0.0, 0.707_106_77, 0.0, 0.707_106_77),
        scale: Vec3::new(2.0, 0.5, 3.25),
    };
    execute_source(
        &mut session,
        &source,
        PlacementEdit::Transform {
            id: source.id,
            transform: transformed,
        },
        "source-transform",
    );

    let copy_id = EntityId::deterministic("generic-source-copy");
    let copy_transform = Transform {
        translation_m: Vec3::new(-81.0, 7.0, 19.0),
        rotation: UnitQuaternion::new(0.382_683_43, 0.0, 0.0, 0.923_879_5),
        scale: Vec3::new(0.75, 1.5, 2.25),
    };
    execute_source(
        &mut session,
        &source,
        PlacementEdit::Duplicate {
            source_id: source.id,
            id: copy_id,
            transform: copy_transform,
        },
        "source-duplicate",
    );
    let visible = source_objects::resolve(session.head(), &source).unwrap();
    assert_eq!(visible.len(), 2);
    assert_eq!(visible[0].id, source.id);
    assert_eq!(visible[0].transform, transformed);
    assert_eq!(visible[1].id, copy_id);
    assert_eq!(visible[1].transform, copy_transform);
    assert!(!visible[1].locked);

    execute_source(
        &mut session,
        &source,
        PlacementEdit::SetLocked {
            id: copy_id,
            locked: true,
        },
        "source-lock-copy",
    );
    let before_failed_lock_edit = session.archive_bytes().unwrap();
    assert!(
        session
            .head()
            .prepare_source_object(
                source.clone(),
                PlacementEdit::Transform {
                    id: copy_id,
                    transform: Transform::IDENTITY,
                },
                CommandId::deterministic("locked-copy-transform"),
            )
            .is_err()
    );
    assert_eq!(session.archive_bytes().unwrap(), before_failed_lock_edit);

    let compact = session.compact_archive_bytes().unwrap();
    let delta = session.delta_archive_bytes().unwrap();
    for bytes in [compact, delta] {
        let mut reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
        assert_eq!(reopened.head(), session.head());
        assert_eq!(reopened.audit(), session.audit());
        execute_history(&mut reopened, ZoneAction::Undo, "source-archive-undo");
        assert!(reopened.can_redo());
        execute_history(&mut reopened, ZoneAction::Redo, "source-archive-redo");
        assert_eq!(reopened.head().base, session.head().base);
        assert_eq!(reopened.head().corrections, session.head().corrections);
        assert_eq!(reopened.head().version, session.head().version);
        assert_eq!(
            reopened.head().revision.get(),
            session.head().revision.get() + 2
        );
    }
}

#[test]
fn removing_source_then_last_copy_keeps_a_removed_tombstone() {
    let mut session = session();
    let source = source();
    let copy_id = EntityId::deterministic("copy-to-remove");
    execute_source(
        &mut session,
        &source,
        PlacementEdit::Duplicate {
            source_id: source.id,
            id: copy_id,
            transform: Transform::from_translation(Vec3::new(10.0, 1.0, 10.0)),
        },
        "tombstone-duplicate",
    );
    execute_source(
        &mut session,
        &source,
        PlacementEdit::Remove { id: source.id },
        "tombstone-remove-source",
    );
    assert_eq!(
        source_objects::resolve(session.head(), &source)
            .unwrap()
            .iter()
            .map(|placement| placement.id)
            .collect::<Vec<_>>(),
        vec![copy_id]
    );
    execute_source(
        &mut session,
        &source,
        PlacementEdit::Remove { id: copy_id },
        "tombstone-remove-copy",
    );
    assert!(
        source_objects::resolve(session.head(), &source)
            .unwrap()
            .is_empty()
    );
    let edit = &session
        .head()
        .corrections
        .source_object_edits
        .as_ref()
        .unwrap()[0];
    assert!(matches!(edit.state, SourceObjectState::Removed));
    assert!(edit.copies.is_empty());

    let before_failed_resurrection = session.archive_bytes().unwrap();
    assert!(
        session
            .head()
            .prepare_source_object(
                source.clone(),
                PlacementEdit::Remove { id: copy_id },
                CommandId::deterministic("tombstone-repeat-remove"),
            )
            .is_err()
    );
    assert_eq!(session.archive_bytes().unwrap(), before_failed_resurrection);
}

#[test]
fn stale_pins_duplicate_ids_and_invalid_transforms_fail_without_mutating_the_head() {
    let mut session = session();
    let source = source();
    execute_source(
        &mut session,
        &source,
        PlacementEdit::Transform {
            id: source.id,
            transform: Transform::from_translation(Vec3::new(3.0, 4.0, 5.0)),
        },
        "failure-baseline-transform",
    );
    let existing_copy_id = EntityId::deterministic("existing-copy");
    execute_source(
        &mut session,
        &source,
        PlacementEdit::Duplicate {
            source_id: source.id,
            id: existing_copy_id,
            transform: Transform::IDENTITY,
        },
        "failure-seed-copy",
    );
    let before = session.archive_bytes().unwrap();

    let mut stale_pin = source.clone();
    stale_pin.asset_hash = ContentHash::from_bytes([0x99; 32]);
    assert!(
        session
            .head()
            .prepare_source_object(
                stale_pin,
                PlacementEdit::Transform {
                    id: source.id,
                    transform: Transform::IDENTITY,
                },
                CommandId::deterministic("failure-stale-pin"),
            )
            .is_err()
    );

    for (label, id) in [
        ("failure-duplicate-source-id", source.id),
        ("failure-duplicate-copy-id", existing_copy_id),
    ] {
        let before_attempt = session.archive_bytes().unwrap();
        assert!(
            session
                .head()
                .prepare_source_object(
                    source.clone(),
                    PlacementEdit::Duplicate {
                        source_id: source.id,
                        id,
                        transform: Transform::IDENTITY,
                    },
                    CommandId::deterministic(label),
                )
                .is_err()
        );
        assert_eq!(session.archive_bytes().unwrap(), before_attempt);
    }

    let mut invalid = Transform::IDENTITY;
    invalid.rotation = UnitQuaternion::new(0.0, 0.0, 0.0, 0.0);
    assert!(
        session
            .head()
            .prepare_source_object(
                source.clone(),
                PlacementEdit::Transform {
                    id: source.id,
                    transform: invalid,
                },
                CommandId::deterministic("failure-invalid-rotation"),
            )
            .is_err()
    );
    assert_eq!(session.archive_bytes().unwrap(), before);
}

#[test]
fn absent_source_object_field_preserves_legacy_head_hashes() {
    let session = session();
    assert!(session.head().corrections.source_object_edits.is_none());
    let bytes = session.head().to_bytes().unwrap();
    let json = String::from_utf8(bytes.clone()).unwrap();
    assert!(!json.contains("source_object_edits"));
    let hash = session.head().content_hash().unwrap();
    let restored = forge_project::zone_authoring::ZoneAuthoringHead::from_bytes(&bytes).unwrap();
    assert_eq!(restored.content_hash().unwrap(), hash);
    assert_eq!(restored.to_bytes().unwrap(), bytes);
}
