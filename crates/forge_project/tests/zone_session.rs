use forge_core::CommandId;
use forge_project::{
    authoring::new_terrain,
    terrain_edit::TerrainEditSession,
    zone_authoring::{
        GeneratorStamp,
        migration::ZoneMigrationProposal,
        session::{ZoneAction, ZoneEditSession, ZoneRequest},
    },
};

fn session() -> ZoneEditSession {
    let legacy =
        TerrainEditSession::new(new_terrain("session", "zone", "Before").unwrap()).unwrap();
    let hash = legacy.document().content_hash().unwrap();
    ZoneEditSession::from_migration(
        ZoneMigrationProposal::prepare(
            &legacy.archive_bytes().unwrap(),
            hash,
            GeneratorStamp {
                name: "accepted-legacy".into(),
                version: "1".into(),
                inputs_hash: hash,
            },
            None,
        )
        .unwrap(),
    )
    .unwrap()
}

fn request(session: &ZoneEditSession, action: ZoneAction) -> ZoneRequest {
    ZoneRequest {
        command_id: CommandId::deterministic(format!("zone-command-{}", session.audit().len())),
        expected_revision: session.head().revision,
        expected_hash: session.head().content_hash().unwrap(),
        action,
    }
}

#[test]
fn compact_archive_preserves_receipts_checkpoint_and_redo() {
    let mut session = session();
    for index in 0..8 {
        let mut corrections = session.head().corrections.clone();
        corrections.name = Some(format!("Retouche {index}"));
        let command = request(&session, ZoneAction::edit(corrections));
        session.execute(&command).unwrap();
    }
    let mut base = session.head().base.clone();
    base.terrain.seed += 1;
    let regeneration = request(&session, ZoneAction::Regenerate(Box::new(base)));
    session.execute(&regeneration).unwrap();
    let undo = request(&session, ZoneAction::Undo);
    session.execute(&undo).unwrap();
    let old = session.archive_bytes().unwrap();
    let compact = session.compact_archive_bytes().unwrap();
    assert!(compact.len() < old.len());
    assert_eq!(session.archive_bytes().unwrap(), old);
    let mut reopened = ZoneEditSession::from_archive_bytes(&compact).unwrap();
    assert_eq!(reopened.head(), session.head());
    assert_eq!(reopened.audit(), session.audit());
    assert_eq!(reopened.archive_bytes().unwrap(), compact);
    let old_json: serde_json::Value = serde_json::from_slice(&old).unwrap();
    let compact_json: serde_json::Value = serde_json::from_slice(&compact).unwrap();
    assert_eq!(old_json["migration"], compact_json["migration"]);
    let redo = request(&reopened, ZoneAction::Redo);
    assert_eq!(
        reopened.execute(&redo).unwrap(),
        session.execute(&redo).unwrap()
    );
    let after = reopened.archive_bytes().unwrap();
    reopened.execute(&redo).unwrap();
    assert_eq!(reopened.archive_bytes().unwrap(), after);
    assert_eq!(
        ZoneEditSession::from_archive_bytes(&after).unwrap().audit(),
        reopened.audit()
    );
}

#[test]
fn compact_archive_rejects_tampered_replay_and_duplicate_commands() {
    let mut session = session();
    let mut corrections = session.head().corrections.clone();
    corrections.name = Some("Manuel".into());
    session
        .execute(&request(&session, ZoneAction::edit(corrections)))
        .unwrap();
    let value: serde_json::Value =
        serde_json::from_slice(&session.compact_archive_bytes().unwrap()).unwrap();
    let mut altered = value.clone();
    altered["commands"][0]["receipt_hash"] =
        serde_json::to_value(session.head().content_hash().unwrap()).unwrap();
    assert!(ZoneEditSession::from_archive_bytes(&serde_json::to_vec(&altered).unwrap()).is_err());
    let mut altered = value.clone();
    altered["commands"][0]["request"]["action"]["corrections"]["name"] = "Falsifié".into();
    assert!(ZoneEditSession::from_archive_bytes(&serde_json::to_vec(&altered).unwrap()).is_err());
    let mut altered = value;
    let duplicate = altered["commands"][0].clone();
    altered["commands"].as_array_mut().unwrap().push(duplicate);
    assert!(ZoneEditSession::from_archive_bytes(&serde_json::to_vec(&altered).unwrap()).is_err());
}

#[test]
fn aggregate_commands_preview_retry_undo_redo_and_archive_are_consistent() {
    let mut session = session();
    let initial = session.archive_bytes().unwrap();
    let mut corrections = session.head().corrections.clone();
    corrections.name = Some("After".into());
    let command = request(&session, ZoneAction::edit(corrections));
    let preview = session.preview(&command).unwrap();
    assert_eq!(session.archive_bytes().unwrap(), initial);
    assert_eq!(session.execute(&command).unwrap(), preview);
    let once = session.archive_bytes().unwrap();
    assert_eq!(session.execute(&command).unwrap(), preview);
    assert_eq!(session.archive_bytes().unwrap(), once);
    let undo = request(&session, ZoneAction::Undo);
    session.execute(&undo).unwrap();
    assert_eq!(session.head().effective().unwrap().0.name, "Before");
    let archive = session.archive_bytes().unwrap();
    let mut reopened = ZoneEditSession::from_archive_bytes(&archive).unwrap();
    assert_eq!(reopened.archive_bytes().unwrap(), archive);
    let redo = request(&reopened, ZoneAction::Redo);
    reopened.execute(&redo).unwrap();
    assert_eq!(reopened.head().effective().unwrap().0.name, "After");
    assert_eq!(reopened.audit().len(), 3);
    let mut altered: serde_json::Value =
        serde_json::from_slice(&reopened.archive_bytes().unwrap()).unwrap();
    altered["head"]["corrections"]["name"] = "Unrecorded".into();
    assert!(ZoneEditSession::from_archive_bytes(&serde_json::to_vec(&altered).unwrap()).is_err());
}

#[test]
fn invalid_stale_and_reused_commands_do_not_mutate() {
    let mut session = session();
    let initial = session.archive_bytes().unwrap();
    let mut corrections = session.head().corrections.clone();
    corrections.name = Some(String::new());
    assert!(
        session
            .execute(&request(&session, ZoneAction::edit(corrections.clone())))
            .is_err()
    );
    assert_eq!(session.archive_bytes().unwrap(), initial);
    corrections.name = Some("Valid".into());
    let valid = request(&session, ZoneAction::edit(corrections));
    session.execute(&valid).unwrap();
    let after = session.archive_bytes().unwrap();
    let mut reused = valid.clone();
    reused.action = ZoneAction::Undo;
    assert!(session.execute(&reused).is_err());
    reused.command_id = CommandId::deterministic("stale");
    assert!(session.execute(&reused).is_err());
    assert_eq!(session.archive_bytes().unwrap(), after);
}
