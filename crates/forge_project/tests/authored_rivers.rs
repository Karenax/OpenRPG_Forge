use forge_core::{CommandId, EntityId};
use forge_project::{
    authoring::{new_terrain, playtest::PlaytestSnapshot},
    terrain_edit::TerrainEditSession,
    zone_authoring::{
        GeneratorStamp,
        migration::ZoneMigrationProposal,
        rivers::{AuthoredRiver, RiverEdit},
        session::{ZoneAction, ZoneEditSession, ZoneRequest},
    },
};

fn fixture() -> (ZoneEditSession, AuthoredRiver) {
    let terrain = new_terrain("rivers", "zone", "Zone").unwrap();
    let hash = terrain.content_hash().unwrap();
    let mut water: forge_project::WaterDocument = serde_json::from_str(include_str!(
        "../../../examples/visual_valley/water.v1.json"
    ))
    .unwrap();
    water.project_id = terrain.project_id;
    water.area_id = terrain.area_id;
    let legacy = TerrainEditSession::new(terrain).unwrap();
    let session = ZoneEditSession::from_migration(
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
    .unwrap();
    (
        session,
        AuthoredRiver {
            id: EntityId::deterministic("river"),
            water,
            maximum_cut_m: 8.0,
            locked: false,
        },
    )
}

fn action(session: &mut ZoneEditSession, action: ZoneAction) {
    session
        .execute(&ZoneRequest {
            command_id: CommandId::deterministic(format!("action-{}", session.audit().len())),
            expected_revision: session.head().revision,
            expected_hash: session.head().content_hash().unwrap(),
            action,
        })
        .unwrap();
}

fn archives(session: &ZoneEditSession) {
    for (bytes, version, old_version) in [
        (session.archive_bytes().unwrap(), 10, 8),
        (session.compact_archive_bytes().unwrap(), 11, 9),
        (session.delta_archive_bytes().unwrap(), 12, 7),
    ] {
        let reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
        assert_eq!(reopened.head(), session.head());
        assert_eq!(reopened.audit(), session.audit());
        let mut json: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
        assert_eq!(json["version"], version);
        json["version"] = serde_json::json!(old_version);
        assert!(ZoneEditSession::from_archive_bytes(&serde_json::to_vec(&json).unwrap()).is_err());
    }
}

#[test]
fn recipes_preview_retry_undo_redo_regenerate_and_reopen() {
    let (mut session, river) = fixture();
    let original = session.head().clone();
    let request = original
        .prepare_river(
            CommandId::deterministic("insert"),
            RiverEdit::Insert {
                river: river.clone(),
            },
        )
        .unwrap();
    let preview = session.preview(&request).unwrap();
    assert_eq!(session.head(), &original);
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(session.audit().len(), 1);
    assert_eq!(session.head().version, 4);
    let snapshot =
        PlaytestSnapshot::from_zone(session.head(), session.head().content_hash().unwrap())
            .unwrap();
    assert_eq!(snapshot.rivers(), std::slice::from_ref(&river));
    assert_eq!(snapshot.terrain(), &session.head().effective().unwrap().0);
    archives(&session);
    action(&mut session, ZoneAction::Undo);
    assert!(session.head().corrections.rivers.is_none());
    assert_eq!(snapshot.rivers(), std::slice::from_ref(&river)); // Frozen independently of Undo.
    assert_eq!(session.head().version, 1);
    archives(&session);
    action(&mut session, ZoneAction::Redo);
    let mut base = session.head().base.clone();
    base.generator.version = "2".into();
    let regen = session
        .head()
        .prepare_regeneration(base, CommandId::deterministic("regen"))
        .unwrap();
    session.execute(&regen).unwrap();
    assert_eq!(
        session.head().corrections.rivers.as_ref().unwrap(),
        &[river]
    );
    archives(&session);
}

#[test]
fn locks_are_enforced_for_raw_commands_and_invalid_recipes_are_atomic() {
    let (mut session, mut river) = fixture();
    river.locked = true;
    let insert = session
        .head()
        .prepare_river(
            CommandId::deterministic("locked"),
            RiverEdit::Insert {
                river: river.clone(),
            },
        )
        .unwrap();
    session.execute(&insert).unwrap();
    let before = session.head().clone();
    assert!(
        before
            .prepare_river(
                CommandId::deterministic("remove"),
                RiverEdit::Remove { id: river.id }
            )
            .is_err()
    );
    let mut forged = before.corrections.clone();
    forged.rivers = Some(vec![]);
    assert!(
        session
            .execute(&ZoneRequest {
                command_id: CommandId::deterministic("raw"),
                expected_revision: before.revision,
                expected_hash: before.content_hash().unwrap(),
                action: ZoneAction::edit(forged)
            })
            .is_err()
    );
    assert_eq!(session.head(), &before);
    let unlock = before
        .prepare_river(
            CommandId::deterministic("unlock"),
            RiverEdit::SetLocked {
                id: river.id,
                locked: false,
            },
        )
        .unwrap();
    session.execute(&unlock).unwrap();
    river.locked = false;
    river.maximum_cut_m = f32::NAN;
    assert!(
        session
            .head()
            .prepare_river(
                CommandId::deterministic("invalid"),
                RiverEdit::Replace { river }
            )
            .is_err()
    );
    archives(&session);
}
