use forge_core::CommandId;
use forge_project::{
    assistant::{self, ScopePolicy},
    audit::AuditStamp,
    authoring::new_terrain,
    terrain_edit::{TerrainAction, TerrainEdit},
    zone_authoring::session::{ZoneAction, ZoneEditSession, ZoneRequest},
};

fn session() -> ZoneEditSession {
    ZoneEditSession::from_initial_terrain(
        new_terrain("assistant-test", "area", "Original").unwrap(),
    )
    .unwrap()
}

fn audit() -> AuditStamp {
    AuditStamp::ai("synthetic-governance-test", "2026-10-22T12:00:00Z").unwrap()
}

fn scope() -> ScopePolicy {
    ScopePolicy::new(vec!["/corrections/name".into()], vec![]).unwrap()
}

fn rename(session: &ZoneEditSession, key: &str, name: &str) -> ZoneRequest {
    session
        .head()
        .prepare_terrain_action(
            TerrainAction::Edit(TerrainEdit::Rename { name: name.into() }),
            CommandId::deterministic(key),
        )
        .unwrap()
}

#[test]
fn dry_run_is_pure_then_approved_command_reuses_audit_undo_and_archive() {
    let mut session = session();
    let original = session.archive_bytes().unwrap();
    let request = rename(&session, "rename", "Proposed");
    let review = assistant::prepare(&session, &request, &scope(), audit()).unwrap();
    assert_eq!(session.archive_bytes().unwrap(), original);
    assert_eq!(review.changes().len(), 1);
    assert_eq!(review.changes()[0].path, "/corrections/name");
    let approval = review.approve(review.content_hash().unwrap()).unwrap();
    let receipt = assistant::apply(&mut session, &scope(), &review, &approval).unwrap();
    assert_eq!(&receipt, review.receipt());
    assert_eq!(receipt.audit, audit());
    let reopened = ZoneEditSession::from_archive_bytes(&session.archive_bytes().unwrap()).unwrap();
    assert_eq!(reopened.head().effective().unwrap().0.name, "Proposed");
    assert_eq!(reopened.audit(), session.audit());
    let undo = ZoneRequest {
        command_id: CommandId::deterministic("undo"),
        expected_hash: session.head().content_hash().unwrap(),
        expected_revision: session.head().revision,
        action: ZoneAction::Undo,
    };
    session.execute(&undo).unwrap();
    assert_eq!(session.head().effective().unwrap().0.name, "Original");
}

#[test]
fn human_edit_invalidates_approval_without_losing_the_edit() {
    let mut session = session();
    let request = rename(&session, "ai", "AI proposal");
    let review = assistant::prepare(&session, &request, &scope(), audit()).unwrap();
    let approval = review.approve(review.content_hash().unwrap()).unwrap();
    session
        .execute(&rename(&session, "human", "Human correction"))
        .unwrap();
    let before = session.archive_bytes().unwrap();
    assert!(assistant::apply(&mut session, &scope(), &review, &approval).is_err());
    assert_eq!(session.archive_bytes().unwrap(), before);
    let fresh = assistant::prepare(
        &session,
        &rename(&session, "fresh", "New variant"),
        &scope(),
        audit(),
    )
    .unwrap();
    assert!(assistant::apply(&mut session, &scope(), &fresh, &approval).is_err());
    assert_eq!(session.archive_bytes().unwrap(), before);
}

#[test]
fn scope_and_revoked_permissions_are_checked_before_mutation() {
    let mut session = session();
    let request = rename(&session, "ai", "Rename");
    let other = ScopePolicy::new(vec!["/corrections/base_elevation_m".into()], vec![]).unwrap();
    let before = session.archive_bytes().unwrap();
    assert!(assistant::prepare(&session, &request, &other, audit()).is_err());
    let review = assistant::prepare(&session, &request, &scope(), audit()).unwrap();
    let approval = review.approve(review.content_hash().unwrap()).unwrap();
    assert!(assistant::apply(&mut session, &other, &review, &approval).is_err());
    assert_eq!(session.archive_bytes().unwrap(), before);
}

#[test]
fn protected_descendant_cannot_be_erased_by_parent_replacement() {
    let session = session();
    let policy = ScopePolicy::new(
        vec!["/corrections".into()],
        vec!["/corrections/name/protected-descendant".into()],
    )
    .unwrap();
    assert!(
        assistant::prepare(
            &session,
            &rename(&session, "ai", "Rename"),
            &policy,
            audit()
        )
        .is_err()
    );
    assert!(ScopePolicy::new(vec!["/".into()], vec![]).is_err());
    assert!(ScopePolicy::new(vec!["/corrections-evil".into()], vec![]).is_err());
    assert!(ScopePolicy::new(vec!["/corrections/name~2".into()], vec![]).is_err());
}

#[test]
fn lost_response_resume_is_idempotent_even_after_reopen_and_later_human_edit() {
    let mut session = session();
    let review = assistant::prepare(
        &session,
        &rename(&session, "ai", "Rename"),
        &scope(),
        audit(),
    )
    .unwrap();
    let approval = review.approve(review.content_hash().unwrap()).unwrap();
    let receipt = assistant::apply(&mut session, &scope(), &review, &approval).unwrap();
    let mut reopened =
        ZoneEditSession::from_archive_bytes(&session.archive_bytes().unwrap()).unwrap();
    reopened
        .execute(&rename(&reopened, "human-later", "Keep this"))
        .unwrap();
    let before = reopened.archive_bytes().unwrap();
    assert_eq!(
        assistant::apply(&mut reopened, &scope(), &review, &approval).unwrap(),
        receipt
    );
    assert_eq!(reopened.archive_bytes().unwrap(), before);
}

#[test]
fn different_payload_with_same_command_id_cannot_resume() {
    let mut session = session();
    let review = assistant::prepare(
        &session,
        &rename(&session, "same-id", "AI proposal"),
        &scope(),
        audit(),
    )
    .unwrap();
    let approval = review.approve(review.content_hash().unwrap()).unwrap();
    session
        .execute(&rename(&session, "same-id", "Different proposal"))
        .unwrap();
    let before = session.archive_bytes().unwrap();
    assert!(assistant::apply(&mut session, &scope(), &review, &approval).is_err());
    assert_eq!(session.archive_bytes().unwrap(), before);
}

#[test]
fn wrong_diff_approval_and_missing_provenance_are_rejected() {
    let session = session();
    let request = rename(&session, "ai", "Proposal");
    assert!(assistant::prepare(&session, &request, &scope(), AuditStamp::legacy()).is_err());
    let review = assistant::prepare(&session, &request, &scope(), audit()).unwrap();
    assert!(
        review
            .approve(session.head().content_hash().unwrap())
            .is_err()
    );
}

#[test]
fn rename_preserves_human_terrain_correction_across_reopen() {
    let mut session = session();
    let human = session
        .head()
        .prepare_terrain_action(
            TerrainAction::Edit(TerrainEdit::SetBaseElevation { metres: 7.0 }),
            CommandId::deterministic("human-elevation"),
        )
        .unwrap();
    session.execute(&human).unwrap();
    let review = assistant::prepare(
        &session,
        &rename(&session, "ai", "Variant"),
        &scope(),
        audit(),
    )
    .unwrap();
    let approval = review.approve(review.content_hash().unwrap()).unwrap();
    assistant::apply(&mut session, &scope(), &review, &approval).unwrap();
    let reopened = ZoneEditSession::from_archive_bytes(&session.archive_bytes().unwrap()).unwrap();
    assert_eq!(reopened.head().corrections.base_elevation_m, Some(7.0));
    assert_eq!(reopened.head().corrections.name.as_deref(), Some("Variant"));
}
