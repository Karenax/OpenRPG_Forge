use forge_core::{AssetId, CommandId, ContentHash, EntityId, Transform, Vec3};
use forge_project::{
    authoring::new_terrain,
    zone_authoring::{
        placements::{ManualPlacement, PhysicsPackageReference, PlacementEdit},
        session::{ZoneAction, ZoneEditSession, ZoneRequest},
    },
};

fn object() -> ManualPlacement {
    ManualPlacement {
        id: EntityId::deterministic("physical-object"),
        name: "Retouche conservée".into(),
        asset_id: AssetId::deterministic("physical-asset"),
        asset_hash: ContentHash::from_bytes([3; 32]),
        physics: Some(Box::new(PhysicsPackageReference {
            schema_version: 1,
            carrier_sha256: ContentHash::from_bytes([3; 32]),
            manifest_sha256: ContentHash::from_bytes([4; 32]),
            package_sha256: ContentHash::from_bytes([5; 32]),
        })),
        transform: Transform::from_translation(Vec3::new(2., 3., 4.)),
        locked: false,
    }
}
fn session() -> ZoneEditSession {
    ZoneEditSession::from_initial_terrain(
        new_terrain("physical-placements", "zone", "Zone").unwrap(),
    )
    .unwrap()
}
fn apply(session: &mut ZoneEditSession, edit: PlacementEdit, key: &str) {
    let request = session
        .head()
        .prepare_placement(CommandId::deterministic(key), edit)
        .unwrap();
    session.execute(&request).unwrap();
}
fn undo(session: &mut ZoneEditSession, key: &str) {
    session
        .execute(&ZoneRequest {
            command_id: CommandId::deterministic(key),
            expected_hash: session.head().content_hash().unwrap(),
            expected_revision: session.head().revision,
            action: ZoneAction::Undo,
        })
        .unwrap();
}

fn replacement(
    source: &ManualPlacement,
    ids: Vec<EntityId>,
    physics: Option<Box<PhysicsPackageReference>>,
) -> PlacementEdit {
    PlacementEdit::ReplacePinnedAsset {
        ids,
        expected_asset_id: source.asset_id,
        expected_asset_hash: source.asset_hash,
        expected_physics: source.physics.clone(),
        asset_id: source.asset_id,
        asset_hash: source.asset_hash,
        physics,
    }
}

#[test]
fn reviewed_package_replacement_preserves_retouches_and_history_in_all_codecs() {
    let mut session = session();
    let original = object();
    let mut copy = original.clone();
    copy.id = EntityId::deterministic("replacement-copy");
    copy.name = "Nom de copie retouché".into();
    copy.transform.translation_m.x = 11.0;
    apply(
        &mut session,
        PlacementEdit::InsertMany {
            placements: vec![original.clone(), copy.clone()],
        },
        "replacement-insert",
    );
    let mut changed = original.physics.clone().unwrap();
    changed.package_sha256 = ContentHash::from_bytes([6; 32]);
    let ids = vec![original.id, copy.id];
    apply(
        &mut session,
        replacement(&original, ids.clone(), Some(changed.clone())),
        "replace-package",
    );
    for source in [&original, &copy] {
        let placement = session
            .head()
            .corrections
            .placements
            .as_ref()
            .unwrap()
            .iter()
            .find(|p| p.id == source.id)
            .unwrap();
        assert_eq!(placement.asset_id, source.asset_id);
        assert_eq!(placement.asset_hash, source.asset_hash);
        assert_eq!(placement.transform, source.transform);
        assert_eq!(placement.name, source.name);
        assert_eq!(placement.locked, source.locked);
        assert_eq!(placement.physics.as_ref(), Some(&changed));
    }
    for bytes in [
        session.archive_bytes().unwrap(),
        session.compact_archive_bytes().unwrap(),
        session.delta_archive_bytes().unwrap(),
    ] {
        let mut reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
        assert_eq!(reopened.head(), session.head());
        undo(&mut reopened, "undo-replace-package");
        assert!(
            reopened
                .head()
                .corrections
                .placements
                .as_ref()
                .unwrap()
                .iter()
                .all(|p| p.physics == original.physics)
        );
        reopened
            .execute(&ZoneRequest {
                command_id: CommandId::deterministic("redo-replace-package"),
                expected_hash: reopened.head().content_hash().unwrap(),
                expected_revision: reopened.head().revision,
                action: ZoneAction::Redo,
            })
            .unwrap();
        assert_eq!(reopened.head().corrections, session.head().corrections);
        let mut current = original.clone();
        current.physics = Some(changed.clone());
        apply(
            &mut reopened,
            replacement(&current, ids.clone(), None),
            "explicit-remove-package",
        );
        assert!(
            reopened
                .head()
                .corrections
                .placements
                .as_ref()
                .unwrap()
                .iter()
                .all(|p| p.physics.is_none())
        );
        let empty = reopened.archive_bytes().unwrap();
        let mut restored = ZoneEditSession::from_archive_bytes(&empty).unwrap();
        undo(&mut restored, "undo-explicit-remove-package");
        assert!(
            restored
                .head()
                .corrections
                .placements
                .as_ref()
                .unwrap()
                .iter()
                .all(|p| p.physics.as_ref() == Some(&changed))
        );
    }
}

#[test]
fn reviewed_replacement_rejects_stale_pins_invalid_targets_and_locked_batches_atomically() {
    let mut session = session();
    let original = object();
    let mut copy = original.clone();
    copy.id = EntityId::deterministic("replacement-refusal-copy");
    apply(
        &mut session,
        PlacementEdit::InsertMany {
            placements: vec![original.clone(), copy.clone()],
        },
        "refusal-insert",
    );
    let mut changed = original.physics.clone().unwrap();
    changed.package_sha256 = ContentHash::from_bytes([6; 32]);
    let before = session.archive_bytes().unwrap();
    let mut stale = original.clone();
    stale.physics.as_mut().unwrap().manifest_sha256 = ContentHash::from_bytes([7; 32]);
    let mut bad_carrier = changed.clone();
    bad_carrier.carrier_sha256 = ContentHash::from_bytes([8; 32]);
    let mut bad_schema = changed.clone();
    bad_schema.schema_version = 2;
    for edit in [
        replacement(&stale, vec![original.id, copy.id], Some(changed.clone())),
        replacement(
            &original,
            vec![original.id, EntityId::deterministic("unknown-replacement")],
            Some(changed.clone()),
        ),
        replacement(
            &original,
            vec![original.id, original.id],
            Some(changed.clone()),
        ),
        replacement(&original, vec![], Some(changed.clone())),
        replacement(&original, vec![original.id], Some(bad_carrier)),
        replacement(&original, vec![original.id], Some(bad_schema)),
        replacement(&original, vec![original.id], original.physics.clone()),
    ] {
        assert!(
            session
                .head()
                .prepare_placement(CommandId::deterministic("invalid-replacement"), edit)
                .is_err()
        );
        assert_eq!(session.archive_bytes().unwrap(), before);
    }
    apply(
        &mut session,
        PlacementEdit::SetLocked {
            id: copy.id,
            locked: true,
        },
        "lock-replacement-copy",
    );
    let locked_before = session.archive_bytes().unwrap();
    assert!(
        session
            .head()
            .prepare_placement(
                CommandId::deterministic("locked-replacement"),
                replacement(&original, vec![original.id, copy.id], Some(changed)),
            )
            .is_err()
    );
    assert_eq!(session.archive_bytes().unwrap(), locked_before);
}

#[test]
fn physical_placement_roundtrips_all_codecs_and_keeps_pins_after_edits() {
    let mut session = session();
    let original = object();
    apply(
        &mut session,
        PlacementEdit::Insert {
            placement: original.clone(),
        },
        "insert",
    );
    let copy = EntityId::deterministic("physical-copy");
    apply(
        &mut session,
        PlacementEdit::Duplicate {
            source_id: original.id,
            id: copy,
            transform: Transform::from_translation(Vec3::new(8., 9., 10.)),
        },
        "copy",
    );
    apply(
        &mut session,
        PlacementEdit::Transform {
            id: original.id,
            transform: Transform::from_translation(Vec3::new(5., 6., 7.)),
        },
        "move",
    );
    assert_eq!(session.head().version, 16);
    for (bytes, version, old_version) in [
        (session.archive_bytes().unwrap(), 43, 40),
        (session.compact_archive_bytes().unwrap(), 44, 41),
        (session.delta_archive_bytes().unwrap(), 45, 42),
    ] {
        let mut value: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
        assert_eq!(value["version"], version);
        let mut reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
        assert_eq!(reopened.head(), session.head());
        for placement in reopened.head().corrections.placements.as_ref().unwrap() {
            assert_eq!(placement.physics, original.physics);
            assert_eq!(placement.name, original.name);
        }
        apply(
            &mut reopened,
            PlacementEdit::Remove { id: original.id },
            "remove-original",
        );
        apply(
            &mut reopened,
            PlacementEdit::Remove { id: copy },
            "remove-copy",
        );
        assert!(
            reopened
                .head()
                .corrections
                .placements
                .as_ref()
                .unwrap()
                .is_empty()
        );
        let empty_archive = reopened.archive_bytes().unwrap();
        assert_eq!(
            serde_json::from_slice::<serde_json::Value>(&empty_archive).unwrap()["version"],
            version
        );
        let mut empty_reopened = ZoneEditSession::from_archive_bytes(&empty_archive).unwrap();
        assert_eq!(empty_reopened.head(), reopened.head());
        undo(&mut empty_reopened, "undo-remove-copy-after-reopen");
        assert_eq!(
            empty_reopened
                .head()
                .corrections
                .placements
                .as_ref()
                .unwrap()[0]
                .physics,
            original.physics
        );
        undo(&mut reopened, "undo-remove-copy");
        assert_eq!(
            reopened.head().corrections.placements.as_ref().unwrap()[0].physics,
            original.physics
        );
        value["version"] = old_version.into();
        assert!(ZoneEditSession::from_archive_bytes(&serde_json::to_vec(&value).unwrap()).is_err());
    }
}

#[test]
fn absent_pin_omitted_and_invalid_carrier_or_unreviewed_replacement_refused() {
    let mut session = session();
    let mut ordinary = object();
    ordinary.physics = None;
    assert!(
        serde_json::to_value(&ordinary)
            .unwrap()
            .get("physics")
            .is_none()
    );
    apply(
        &mut session,
        PlacementEdit::Insert {
            placement: ordinary,
        },
        "ordinary",
    );
    assert!(session.head().version < 16);
    let before = session.archive_bytes().unwrap();
    let mut bad = object();
    bad.id = EntityId::deterministic("bad-carrier");
    bad.asset_hash = ContentHash::from_bytes([8; 32]);
    assert!(
        session
            .head()
            .prepare_placement(
                CommandId::deterministic("bad-carrier"),
                PlacementEdit::Insert { placement: bad }
            )
            .is_err()
    );
    assert_eq!(session.archive_bytes().unwrap(), before);
    let mut physical = object();
    physical.id = EntityId::deterministic("physical-second");
    apply(
        &mut session,
        PlacementEdit::Insert {
            placement: physical.clone(),
        },
        "physical-second",
    );
    let before = session.archive_bytes().unwrap();
    assert!(
        session
            .head()
            .prepare_placement(
                CommandId::deterministic("unreviewed-replace"),
                PlacementEdit::ReplaceAsset {
                    ids: vec![physical.id],
                    expected_asset_id: physical.asset_id,
                    expected_asset_hash: physical.asset_hash,
                    asset_id: AssetId::deterministic("replacement"),
                    asset_hash: ContentHash::from_bytes([8; 32]),
                }
            )
            .is_err()
    );
    assert_eq!(session.archive_bytes().unwrap(), before);
}
