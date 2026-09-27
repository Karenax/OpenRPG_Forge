use super::*;

pub(super) fn session() -> TerrainEditSession {
    let document = serde_json::from_str(include_str!(
        "../../../../examples/visual_valley/terrain.v1.json"
    ))
    .unwrap();
    TerrainEditSession::new(document).unwrap()
}

pub(super) fn request(
    session: &TerrainEditSession,
    id: &str,
    action: TerrainAction,
) -> TerrainEditRequest {
    TerrainEditRequest {
        command_id: CommandId::deterministic(id),
        expected_revision: session.document().revision,
        expected_hash: hash(session.document()).unwrap(),
        action,
    }
}

pub(super) fn rename(name: &str) -> TerrainAction {
    TerrainAction::Edit(TerrainEdit::Rename { name: name.into() })
}

#[test]
fn archive_restores_document_history_retries_and_redo() {
    let mut session = session();
    let first = request(&session, "first", rename("Retouche conservée"));
    let receipt = session.execute(&first).unwrap();
    let undo = request(&session, "undo", TerrainAction::Undo);
    session.execute(&undo).unwrap();
    let bytes = session.archive_bytes().unwrap();
    let mut reopened = TerrainEditSession::from_archive_bytes(&bytes).unwrap();
    assert_eq!(reopened.document(), session.document());
    assert_eq!(reopened.audit(), session.audit());
    assert_eq!(reopened.archive_bytes().unwrap(), bytes);
    assert_eq!(reopened.execute(&first).unwrap(), receipt);
    assert_eq!(reopened.document(), session.document());
    let redo = request(&reopened, "redo", TerrainAction::Redo);
    reopened.execute(&redo).unwrap();
    assert_eq!(reopened.document().name, "Retouche conservée");
    let undo = request(&reopened, "undo-after-reopen", TerrainAction::Undo);
    reopened.execute(&undo).unwrap();
    assert_eq!(reopened.document().name, session.document().name);
}

#[test]
fn archive_rejects_corruption_unknown_versions_and_duplicate_entries() {
    let mut session = session();
    let command = request(&session, "first", rename("A"));
    session.execute(&command).unwrap();
    let valid: serde_json::Value =
        serde_json::from_slice(&session.archive_bytes().unwrap()).unwrap();
    for field in [
        "version",
        "schema",
        "final_hash",
        "inverse",
        "duplicate",
        "unknown",
    ] {
        let mut value = valid.clone();
        match field {
            "version" => value["version"] = 999.into(),
            "schema" => value["schema"] = "other".into(),
            "final_hash" => value["final_hash"] = "00".repeat(32).into(),
            "inverse" => value["receipts"][0]["inverse"]["name"] = "Forged".into(),
            "duplicate" => {
                let entry = value["receipts"][0].clone();
                value["receipts"].as_array_mut().unwrap().push(entry);
            }
            _ => value["unknown"] = true.into(),
        }
        assert!(
            TerrainEditSession::from_archive_bytes(&serde_json::to_vec(&value).unwrap()).is_err(),
            "{field}"
        );
    }
    assert!(TerrainEditSession::from_archive_bytes(b"{").is_err());
    assert!(TerrainEditSession::from_archive_bytes(&vec![b' '; MAX_ARCHIVE_BYTES + 1]).is_err());
}

#[test]
fn archive_preserves_divergent_history_and_empty_session() {
    let mut session = session();
    let empty = session.archive_bytes().unwrap();
    assert_eq!(
        TerrainEditSession::from_archive_bytes(&empty)
            .unwrap()
            .document(),
        session.document()
    );
    for (id, action) in [
        ("a", rename("A")),
        ("undo", TerrainAction::Undo),
        ("b", rename("B")),
    ] {
        let command = request(&session, id, action);
        session.execute(&command).unwrap();
    }
    let mut reopened =
        TerrainEditSession::from_archive_bytes(&session.archive_bytes().unwrap()).unwrap();
    let redo = request(&reopened, "redo", TerrainAction::Redo);
    assert!(matches!(
        reopened.execute(&redo),
        Err(TerrainEditError::EmptyHistory)
    ));
    assert_eq!(reopened.audit(), session.audit());
}

#[test]
fn preview_apply_retry_undo_redo_are_consistent() {
    let mut session = session();
    let original = session.document().clone();
    let command = request(&session, "rename", rename("Ma zone"));
    let preview = session.preview(&command).unwrap();
    assert_eq!(session.document(), &original);
    assert!(session.audit().is_empty());
    assert_eq!(session.execute(&command).unwrap(), preview);
    assert_eq!(session.execute(&command).unwrap(), preview);
    assert_eq!(session.audit().len(), 1);
    let undo = request(&session, "undo", TerrainAction::Undo);
    session.execute(&undo).unwrap();
    assert_eq!(session.document().name, original.name);
    assert_eq!(
        session.document().revision.get(),
        original.revision.get() + 2
    );
    let redo = request(&session, "redo", TerrainAction::Redo);
    session.execute(&redo).unwrap();
    assert_eq!(session.document().name, "Ma zone");
    let state = session.document().clone();
    session.execute(&command).unwrap();
    assert_eq!(session.document(), &state);
    assert_eq!(session.audit().len(), 3);
}

#[test]
fn stale_invalid_and_reused_commands_leave_everything_unchanged() {
    let mut session = session();
    let outdated = request(&session, "stale", rename("Périmée"));
    let command = request(&session, "first", rename("Valide"));
    session.execute(&command).unwrap();
    let state = session.document().clone();
    assert!(matches!(
        session.execute(&outdated),
        Err(TerrainEditError::Conflict)
    ));
    let reused = request(&session, "first", rename("Autre"));
    assert!(matches!(
        session.execute(&reused),
        Err(TerrainEditError::DuplicateIdentity)
    ));
    let invalid = request(&session, "invalid", rename(" "));
    assert!(session.execute(&invalid).is_err());
    assert_eq!(session.document(), &state);
    assert_eq!(session.audit().len(), 1);
}

#[test]
fn divergent_edit_discards_redo_but_preserves_audit() {
    let mut session = session();
    for (id, action) in [
        ("a", rename("A")),
        ("b", TerrainAction::Undo),
        ("c", rename("C")),
    ] {
        let command = request(&session, id, action);
        session.execute(&command).unwrap();
    }
    let redo = request(&session, "redo", TerrainAction::Redo);
    assert!(matches!(
        session.execute(&redo),
        Err(TerrainEditError::EmptyHistory)
    ));
    assert_eq!(session.audit().len(), 3);
}

#[test]
fn elevation_is_validated_and_document_round_trips_without_history() {
    let mut session = session();
    let invalid = request(
        &session,
        "nan",
        TerrainAction::Edit(TerrainEdit::SetBaseElevation { metres: f32::NAN }),
    );
    assert!(session.execute(&invalid).is_err());
    let command = request(
        &session,
        "height",
        TerrainAction::Edit(TerrainEdit::SetBaseElevation { metres: 7.5 }),
    );
    session.execute(&command).unwrap();
    let bytes = serde_json::to_vec(session.document()).unwrap();
    let reopened = TerrainEditSession::new(serde_json::from_slice(&bytes).unwrap()).unwrap();
    assert_eq!(reopened.document(), session.document());
    assert!(reopened.audit().is_empty());
    // This is a serialization test, not an atomic filesystem save or Studio proof.
}

#[test]
fn same_revision_wrong_hash_and_revision_overflow_are_rejected() {
    let mut session = session();
    let mut command = request(&session, "hash", rename("Changed"));
    let mut different = session.document().clone();
    different.name = "Different source".into();
    command.expected_hash = hash(&different).unwrap();
    assert!(matches!(
        session.execute(&command),
        Err(TerrainEditError::Conflict)
    ));
    session.document.revision = Revision::new(u64::MAX);
    let command = request(&session, "overflow", rename("Changed"));
    assert!(matches!(
        session.execute(&command),
        Err(TerrainEditError::RevisionOverflow)
    ));
    assert!(session.audit().is_empty());
}

#[test]
fn no_op_empty_history_and_oversized_name_do_not_mutate() {
    let mut session = session();
    for (id, action) in [
        ("undo", TerrainAction::Undo),
        ("redo", TerrainAction::Redo),
        ("noop", rename(&session.document().name)),
        ("large", rename(&"x".repeat(129))),
    ] {
        let command = request(&session, id, action);
        assert!(session.execute(&command).is_err());
    }
    assert!(session.audit().is_empty());
}

#[test]
fn session_limit_preserves_receipts_and_still_accepts_exact_retry() {
    let mut session = session();
    let first = request(&session, "first", rename("First"));
    session.execute(&first).unwrap();
    for index in 1..MAX_COMMANDS {
        let command = request(
            &session,
            &format!("command-{index}"),
            rename(&format!("Zone {index}")),
        );
        session.execute(&command).unwrap();
    }
    let state = session.document().clone();
    let command = request(&session, "over-limit", rename("Refused"));
    assert!(matches!(
        session.execute(&command),
        Err(TerrainEditError::SessionLimit)
    ));
    session.execute(&first).unwrap();
    assert_eq!(session.document(), &state);
    assert_eq!(session.audit().len(), MAX_COMMANDS);
}
