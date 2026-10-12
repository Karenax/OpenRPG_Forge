use forge_core::{AssetId, CommandId, ContentHash, EntityId, Transform, Vec3};
use forge_project::{
    authoring::{new_terrain, playtest::PlaytestSnapshot},
    terrain_edit::TerrainEditSession,
    zone_authoring::{
        GeneratorStamp,
        migration::ZoneMigrationProposal,
        placements::{ManualPlacement, PlacementEdit},
        session::{ZoneAction, ZoneEditSession, ZoneRequest},
    },
};

fn session() -> ZoneEditSession {
    let terrain = new_terrain("placements", "zone", "Zone").unwrap();
    let hash = terrain.content_hash().unwrap();
    let legacy = TerrainEditSession::new(terrain).unwrap();
    ZoneEditSession::from_migration(
        ZoneMigrationProposal::prepare(
            &legacy.archive_bytes().unwrap(),
            hash,
            GeneratorStamp {
                name: "fixture".into(),
                version: "1".into(),
                inputs_hash: hash,
            },
            None,
        )
        .unwrap(),
    )
    .unwrap()
}

fn door() -> ManualPlacement {
    ManualPlacement {
        physics: None,
        id: EntityId::deterministic("manual-door"),
        name: "Porte".into(),
        asset_id: AssetId::deterministic("fixture-door"),
        asset_hash: ContentHash::from_bytes([3; 32]),
        transform: Transform::from_translation(Vec3::new(2.0, 3.0, 4.0)),
        locked: false,
    }
}

#[test]
fn placement_change_summary_distinguishes_pins_from_retouches_and_membership() {
    use forge_project::zone_authoring::placements::PlacementChanges;
    let first = door();
    let mut second = first.clone();
    second.id = EntityId::deterministic("summary-second");
    let mut before = vec![first.clone(), second.clone()];
    before.sort_by_key(|p| p.id);
    let mut changed = first.clone();
    changed.asset_hash = ContentHash::from_bytes([8; 32]);
    let mut after = vec![changed.clone(), second.clone()];
    after.sort_by_key(|p| p.id);
    assert_eq!(
        PlacementChanges::between(&before, &after).unwrap(),
        PlacementChanges {
            references: 1,
            unchanged: 1,
            ..PlacementChanges::default()
        }
    );
    changed.transform.translation_m.x += 1.0;
    changed.name = "Renommé".into();
    changed.locked = true;
    let mut added = second;
    added.id = EntityId::deterministic("summary-added");
    after = vec![changed, added];
    after.sort_by_key(|p| p.id);
    assert_eq!(
        PlacementChanges::between(&before, &after).unwrap(),
        PlacementChanges {
            added: 1,
            removed: 1,
            references: 1,
            transforms: 1,
            names: 1,
            locks: 1,
            unchanged: 0
        }
    );
    assert!(PlacementChanges::between(&[first.clone(), first], &after).is_err());
}

#[test]
fn asset_replacement_preserves_retouches_and_roundtrips_with_undo() {
    let mut session = session();
    let mut first = door();
    first.transform.scale = Vec3::new(1.2, 0.8, 2.0);
    let mut second = first.clone();
    second.id = EntityId::deterministic("second-wall");
    second.transform.translation_m = Vec3::new(9.0, 2.0, 1.0);
    let insert = session
        .head()
        .prepare_placement(
            CommandId::deterministic("replace-insert"),
            PlacementEdit::InsertMany {
                placements: vec![first.clone(), second.clone()],
            },
        )
        .unwrap();
    session.execute(&insert).unwrap();
    let before = session.head().corrections.placements.clone();
    let replacement = |ids| PlacementEdit::ReplaceAsset {
        ids,
        expected_asset_id: first.asset_id,
        expected_asset_hash: first.asset_hash,
        asset_id: first.asset_id,
        asset_hash: ContentHash::from_bytes([9; 32]),
    };
    for ids in [
        vec![],
        vec![first.id, first.id],
        vec![EntityId::deterministic("absent")],
    ] {
        assert!(
            session
                .head()
                .prepare_placement(CommandId::deterministic("bad-replace"), replacement(ids))
                .is_err()
        );
    }
    let request = session
        .head()
        .prepare_placement(
            CommandId::deterministic("replace"),
            replacement(vec![first.id, second.id]),
        )
        .unwrap();
    assert_eq!(session.head().corrections.placements, before);
    session.execute(&request).unwrap();
    let after = session.head().corrections.placements.clone();
    for old in before.as_ref().unwrap() {
        let mut expected = old.clone();
        expected.asset_hash = ContentHash::from_bytes([9; 32]);
        assert!(after.as_ref().unwrap().contains(&expected));
    }
    assert!(
        session
            .head()
            .prepare_placement(
                CommandId::deterministic("stale-pin"),
                replacement(vec![first.id])
            )
            .is_err()
    );
    apply(&mut session, ZoneAction::Undo);
    assert_eq!(session.head().corrections.placements, before);
    apply(&mut session, ZoneAction::Redo);
    assert_eq!(session.head().corrections.placements, after);
    let reopened = ZoneEditSession::from_archive_bytes(&session.archive_bytes().unwrap()).unwrap();
    assert_eq!(reopened.head(), session.head());
}

#[test]
fn asset_replacement_rejects_locked_batch_without_mutation() {
    let mut session = session();
    let first = door();
    let mut locked = first.clone();
    locked.id = EntityId::deterministic("locked-wall");
    locked.locked = true;
    let insert = session
        .head()
        .prepare_placement(
            CommandId::deterministic("locked-insert"),
            PlacementEdit::InsertMany {
                placements: vec![first.clone(), locked.clone()],
            },
        )
        .unwrap();
    session.execute(&insert).unwrap();
    let before = session.archive_bytes().unwrap();
    assert!(
        session
            .head()
            .prepare_placement(
                CommandId::deterministic("locked-replace"),
                PlacementEdit::ReplaceAsset {
                    ids: vec![first.id, locked.id],
                    expected_asset_id: first.asset_id,
                    expected_asset_hash: first.asset_hash,
                    asset_id: first.asset_id,
                    asset_hash: ContentHash::from_bytes([9; 32]),
                }
            )
            .is_err()
    );
    assert_eq!(session.archive_bytes().unwrap(), before);
}

#[test]
fn playtest_placements_are_pinned_immutable_and_do_not_edit_the_zone() {
    let mut session = session();
    let placement = door();
    let insert = session
        .head()
        .prepare_placement(
            CommandId::deterministic("snapshot-insert"),
            PlacementEdit::Insert {
                placement: placement.clone(),
            },
        )
        .unwrap();
    session.execute(&insert).unwrap();
    let before = session.archive_bytes().unwrap();
    let hash = session.head().content_hash().unwrap();
    let snapshot = PlaytestSnapshot::from_zone(session.head(), hash).unwrap();
    assert_eq!(snapshot.placements(), std::slice::from_ref(&placement));
    assert_eq!(session.archive_bytes().unwrap(), before);
    let remove = session
        .head()
        .prepare_placement(
            CommandId::deterministic("snapshot-remove"),
            PlacementEdit::Remove { id: placement.id },
        )
        .unwrap();
    session.execute(&remove).unwrap();
    assert_eq!(snapshot.placements(), &[placement]);
    assert!(PlaytestSnapshot::from_zone(session.head(), hash).is_err());
    let reopened = ZoneEditSession::from_archive_bytes(&before).unwrap();
    assert_eq!(
        PlaytestSnapshot::from_zone(reopened.head(), hash).unwrap(),
        snapshot
    );
}

fn apply(session: &mut ZoneEditSession, action: ZoneAction) {
    session
        .execute(&ZoneRequest {
            command_id: CommandId::deterministic(format!("action-{}", session.audit().len())),
            expected_revision: session.head().revision,
            expected_hash: session.head().content_hash().unwrap(),
            action,
        })
        .unwrap();
}

#[test]
fn temporary_playtest_start_is_bounded_and_never_changes_the_snapshot_identity() {
    use forge_project::authoring::playtest::PlaytestStart;
    let session = session();
    let hash = session.head().content_hash().unwrap();
    let snapshot = PlaytestSnapshot::from_zone(session.head(), hash).unwrap();
    let chosen = PlaytestStart {
        x_m: -84.0,
        z_m: 90.0,
        facing_degrees: Some(90.0),
    };
    let relocated = snapshot.clone().with_start(Some(chosen)).unwrap();
    assert_eq!(relocated.identity(), snapshot.identity());
    assert_eq!(relocated.terrain(), snapshot.terrain());
    assert_eq!(relocated.placements(), snapshot.placements());
    assert_eq!(relocated.start(), Some(chosen));
    assert_eq!(snapshot.start(), None);
    let old: PlaytestStart = serde_json::from_str(r#"{"x_m":0.0,"z_m":0.0}"#).unwrap();
    assert_eq!(old.facing_degrees, None);
    assert!(
        !serde_json::to_string(&old)
            .unwrap()
            .contains("facing_degrees")
    );
    assert_eq!(
        serde_json::from_str::<PlaytestStart>(&serde_json::to_string(&chosen).unwrap()).unwrap(),
        chosen
    );
    for point in [
        PlaytestStart {
            x_m: f32::NAN,
            z_m: 0.0,
            facing_degrees: None,
        },
        PlaytestStart {
            x_m: 0.0,
            z_m: f32::INFINITY,
            facing_degrees: None,
        },
        PlaytestStart {
            x_m: 256.0,
            z_m: 0.0,
            facing_degrees: None,
        },
        PlaytestStart {
            x_m: 0.0,
            z_m: -256.0,
            facing_degrees: None,
        },
        PlaytestStart {
            x_m: 0.0,
            z_m: 0.0,
            facing_degrees: Some(f32::NAN),
        },
    ] {
        assert!(snapshot.clone().with_start(Some(point)).is_err());
    }
    assert!(
        snapshot
            .clone()
            .with_start(Some(PlaytestStart {
                x_m: 255.0,
                z_m: -255.0,
                facing_degrees: None,
            }))
            .is_ok()
    );
    assert_eq!(session.head().content_hash().unwrap(), hash);
}

#[test]
fn duplicate_preserves_locked_source_and_has_its_own_undoable_identity() {
    let mut session = session();
    let mut source = door();
    source.locked = true;
    let insert = session
        .head()
        .prepare_placement(
            CommandId::deterministic("original"),
            PlacementEdit::Insert {
                placement: source.clone(),
            },
        )
        .unwrap();
    session.execute(&insert).unwrap();
    let before = session.head().clone();
    let id = EntityId::deterministic("copied-door");
    let transform = Transform::from_translation(Vec3::new(-12.25, 3.5, 42.0));
    let request = before
        .prepare_placement(
            CommandId::deterministic("duplicate"),
            PlacementEdit::Duplicate {
                source_id: source.id,
                id,
                transform,
            },
        )
        .unwrap();
    let preview = session.preview(&request).unwrap();
    assert_eq!(session.head(), &before);
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(session.execute(&request).unwrap(), preview);
    let placements = session.head().corrections.placements.as_ref().unwrap();
    assert_eq!(placements.iter().find(|p| p.id == source.id), Some(&source));
    let copied = placements.iter().find(|p| p.id == id).unwrap().clone();
    assert_eq!(copied.asset_id, source.asset_id);
    assert_eq!(copied.asset_hash, source.asset_hash);
    assert_eq!(copied.name, source.name);
    assert_eq!(copied.transform, transform);
    assert!(!copied.locked);
    apply(&mut session, ZoneAction::Undo);
    assert_eq!(session.head().corrections, before.corrections);
    apply(&mut session, ZoneAction::Redo);
    let reopened =
        ZoneEditSession::from_archive_bytes(&session.compact_archive_bytes().unwrap()).unwrap();
    assert_eq!(reopened.head(), session.head());
    for edit in [
        PlacementEdit::Duplicate {
            source_id: source.id,
            id: source.id,
            transform,
        },
        PlacementEdit::Duplicate {
            source_id: EntityId::deterministic("absent"),
            id: EntityId::deterministic("new"),
            transform,
        },
        PlacementEdit::Duplicate {
            source_id: source.id,
            id: EntityId::deterministic("invalid"),
            transform: Transform::from_translation(Vec3::new(f32::NAN, 0.0, 0.0)),
        },
    ] {
        assert!(
            session
                .head()
                .prepare_placement(CommandId::deterministic("invalid-copy"), edit)
                .is_err()
        );
    }
    assert_eq!(reopened.head(), session.head());
}

#[test]
fn manual_placement_survives_regeneration_and_both_archive_encodings() {
    let mut session = session();
    let initial = session.head().clone();
    assert!(
        !String::from_utf8(initial.to_bytes().unwrap())
            .unwrap()
            .contains("placements")
    );
    let placement = door();
    let request = session
        .head()
        .prepare_placement(
            CommandId::deterministic("insert"),
            PlacementEdit::Insert {
                placement: placement.clone(),
            },
        )
        .unwrap();
    let preview = session.preview(&request).unwrap();
    assert_eq!(session.head(), &initial);
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(session.head().version, 2);
    apply(&mut session, ZoneAction::Undo);
    assert_eq!(session.head().version, 1);
    assert!(session.head().corrections.placements.is_none());
    apply(&mut session, ZoneAction::Redo);
    let lock = session
        .head()
        .prepare_placement(
            CommandId::deterministic("lock"),
            PlacementEdit::SetLocked {
                id: placement.id,
                locked: true,
            },
        )
        .unwrap();
    session.execute(&lock).unwrap();
    let retained = session.head().corrections.clone();
    let mut base = session.head().base.clone();
    base.terrain.seed += 1;
    apply(&mut session, ZoneAction::Regenerate(Box::new(base)));
    assert_eq!(session.head().corrections, retained);
    for (bytes, version) in [
        (session.archive_bytes().unwrap(), 4),
        (session.compact_archive_bytes().unwrap(), 5),
    ] {
        let value: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
        assert_eq!(value["version"], version);
        let reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
        assert_eq!(reopened.head(), session.head());
        assert_eq!(reopened.audit(), session.audit());
        let mut forged = value;
        forged["version"] = serde_json::json!(if version == 5 { 3 } else { 2 });
        assert!(
            ZoneEditSession::from_archive_bytes(&serde_json::to_vec(&forged).unwrap()).is_err()
        );
    }
    let snapshot =
        PlaytestSnapshot::from_zone(session.head(), session.head().content_hash().unwrap())
            .unwrap();
    assert_eq!(
        snapshot.placements(),
        session.head().corrections.placements.as_deref().unwrap()
    );
}

#[test]
fn locks_ids_and_invalid_transforms_cannot_be_bypassed_by_raw_commands() {
    let mut session = session();
    let mut placement = door();
    placement.locked = true;
    let request = session
        .head()
        .prepare_placement(
            CommandId::deterministic("insert"),
            PlacementEdit::Insert {
                placement: placement.clone(),
            },
        )
        .unwrap();
    session.execute(&request).unwrap();
    let before = session.archive_bytes().unwrap();
    for edit in [
        PlacementEdit::Remove { id: placement.id },
        PlacementEdit::Transform {
            id: placement.id,
            transform: Transform::IDENTITY,
        },
        PlacementEdit::Insert {
            placement: placement.clone(),
        },
    ] {
        assert!(
            session
                .head()
                .prepare_placement(CommandId::deterministic("invalid"), edit)
                .is_err()
        );
    }
    let mut bypass = session.head().corrections.clone();
    bypass.placements = None;
    assert!(
        session
            .preview(&ZoneRequest {
                command_id: CommandId::deterministic("bypass"),
                expected_revision: session.head().revision,
                expected_hash: session.head().content_hash().unwrap(),
                action: ZoneAction::edit(bypass)
            })
            .is_err()
    );
    assert_eq!(session.archive_bytes().unwrap(), before);
    let unlock = session
        .head()
        .prepare_placement(
            CommandId::deterministic("unlock"),
            PlacementEdit::SetLocked {
                id: placement.id,
                locked: false,
            },
        )
        .unwrap();
    session.execute(&unlock).unwrap();
    let mut invalid = Transform::IDENTITY;
    invalid.translation_m.x = f32::NAN;
    assert!(
        session
            .head()
            .prepare_placement(
                CommandId::deterministic("nan"),
                PlacementEdit::Transform {
                    id: placement.id,
                    transform: invalid
                }
            )
            .is_err()
    );
    let moved = Transform::from_translation(Vec3::new(8.0, 3.0, 4.0));
    let request = session
        .head()
        .prepare_placement(
            CommandId::deterministic("move"),
            PlacementEdit::Transform {
                id: placement.id,
                transform: moved,
            },
        )
        .unwrap();
    session.execute(&request).unwrap();
    assert_eq!(
        session.head().corrections.placements.as_ref().unwrap()[0].transform,
        moved
    );
    apply(&mut session, ZoneAction::Undo);
    assert_eq!(
        session.head().corrections.placements.as_ref().unwrap()[0].transform,
        placement.transform
    );
}

#[test]
fn head_versions_and_canonical_placement_ids_are_strict() {
    let mut session = session();
    let first = door();
    let mut second = first.clone();
    second.id = EntityId::deterministic("other-placement");
    for placement in [second.clone(), first.clone()] {
        let request = session
            .head()
            .prepare_placement(
                CommandId::deterministic(placement.id.to_string()),
                PlacementEdit::Insert { placement },
            )
            .unwrap();
        session.execute(&request).unwrap();
    }
    let head = session.head();
    let values = head.corrections.placements.as_ref().unwrap();
    assert!(values[0].id < values[1].id);
    assert!(values.contains(&first) && values.contains(&second));
    let mut invalid = head.clone();
    invalid.version = 1;
    assert!(invalid.validate().is_err());
    invalid = head.clone();
    invalid.corrections.placements.as_mut().unwrap().push(first);
    assert!(invalid.validate().is_err());
    let mut base = head.base.clone();
    base.terrain.seed += 1;
    assert_eq!(
        head.review_regeneration(base)
            .unwrap()
            .effective
            .unwrap()
            .corrections,
        head.corrections
    );
}
