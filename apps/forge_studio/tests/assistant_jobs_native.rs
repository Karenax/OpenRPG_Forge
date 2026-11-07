#![cfg(not(target_arch = "wasm32"))]
use forge_core::{CommandId, Vec3};
use forge_project::{
    assistant::{
        jobs::{JobJournal, JobLimits, JobState, TokenPrices},
        planning::{PlanProposal, PlannedOperation},
        provider::{ApiKind, ProviderRequest, ProviderResult, TokenUsage},
        selection::{Region, Selection, SelectionScope},
    },
    authoring::new_terrain,
    zone_authoring::session::ZoneEditSession,
};
use forge_studio_frontend::assistant_job_native::StoredJob;

fn request() -> ProviderRequest {
    let session =
        ZoneEditSession::from_initial_terrain(new_terrain("stored-job", "zone", "Zone").unwrap())
            .unwrap();
    let scope = SelectionScope::new(
        session.head(),
        &Selection {
            zone_properties: true,
            ..Selection::default()
        },
        Region {
            minimum: Vec3::new(-5.0, -5.0, -5.0),
            maximum: Vec3::new(5.0, 5.0, 5.0),
        },
    )
    .unwrap();
    ProviderRequest::new(
        ApiKind::ChatCompletions,
        "http://127.0.0.1:8080/v1/chat/completions",
        "test-model",
        "Rename",
        scope.context(),
        512,
    )
    .unwrap()
}
fn journal(id: CommandId, request: &ProviderRequest) -> JobJournal {
    JobJournal::new(
        id,
        request,
        JobLimits {
            maximum_attempts: 2,
            maximum_input_tokens: 1024,
            maximum_output_tokens: 512,
            maximum_micro_usd: 0,
            prices: TokenPrices {
                input_per_million: 0,
                output_per_million: 0,
            },
        },
    )
    .unwrap()
}

#[test]
fn durable_start_survives_process_loss_and_staged_output_reopens_without_resending() {
    let root = std::env::temp_dir().join(format!("forge-stored-job-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let request = request();
    let approval = request
        .approve_outbound(request.content_hash().unwrap())
        .unwrap();
    let id = CommandId::deterministic("stored-job");
    let mut stored = StoredJob::create(&root, journal(id, &request)).unwrap();
    assert!(
        StoredJob::open(&root, id).is_err(),
        "another writer cannot acquire the job"
    );
    assert_eq!(stored.begin(&request, &approval).unwrap(), 1);
    drop(stored);
    let mut stored = StoredJob::open(&root, id).unwrap();
    assert_eq!(stored.snapshot().unwrap().state, JobState::Running);
    assert!(stored.begin(&request, &approval).is_err());
    stored.interrupt(1).unwrap();
    let attempt = stored.begin(&request, &approval).unwrap();
    stored
        .stage(
            attempt,
            ProviderResult {
                proposal: PlanProposal {
                    context_hash: request.context_hash(),
                    summary: "Rename".into(),
                    missing_capabilities: vec![],
                    operations: vec![PlannedOperation::RenameZone {
                        name: "Variant".into(),
                    }],
                },
                usage: TokenUsage {
                    input_tokens: 100,
                    output_tokens: 50,
                },
                response_id: "stored-response".into(),
                model: "test-model".into(),
            },
        )
        .unwrap();
    drop(stored);
    let mut stored = StoredJob::open(&root, id).unwrap();
    assert_eq!(stored.snapshot().unwrap().state, JobState::Staged);
    assert!(stored.snapshot().unwrap().result.is_some());
    assert!(stored.begin(&request, &approval).is_err());
    drop(stored);
    assert_eq!(root.parent().unwrap(), std::env::temp_dir());
    std::fs::remove_dir_all(root).unwrap();
}

#[test]
fn storage_rejects_non_directory_group_and_does_not_create_absent_jobs_on_open() {
    let root = std::env::temp_dir().join(format!("forge-invalid-job-store-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let id = CommandId::deterministic("missing-job");
    assert!(StoredJob::open(&root, id).is_err());
    assert!(!root.join("assistant-jobs").exists());
    std::fs::write(root.join("assistant-jobs"), b"keep").unwrap();
    let request = request();
    assert!(StoredJob::create(&root, journal(id, &request)).is_err());
    assert_eq!(std::fs::read(root.join("assistant-jobs")).unwrap(), b"keep");
    assert_eq!(root.parent().unwrap(), std::env::temp_dir());
    std::fs::remove_dir_all(root).unwrap();
}

fn recovery_fixture(key: &str) -> (std::path::PathBuf, CommandId, ProviderRequest, StoredJob) {
    let root =
        std::env::temp_dir().join(format!("forge-job-recovery-{key}-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let id = CommandId::deterministic(format!("recovery/{key}"));
    let request = request();
    let stored = StoredJob::create(&root, journal(id, &request)).unwrap();
    (root, id, request, stored)
}

fn recovery_paths(
    root: &std::path::Path,
    id: CommandId,
) -> (std::path::PathBuf, std::path::PathBuf) {
    let directory = root.join("assistant-jobs").join(id.to_string());
    (
        directory.join("authoring.json"),
        directory.join("authoring.pending"),
    )
}

fn discard_recovery_fixture(root: &std::path::Path) {
    let resolved = root.canonicalize().unwrap();
    assert_eq!(
        resolved.parent().unwrap(),
        std::env::temp_dir().canonicalize().unwrap()
    );
    assert!(
        resolved
            .file_name()
            .unwrap()
            .to_str()
            .unwrap()
            .starts_with("forge-job-recovery-")
    );
    std::fs::remove_dir_all(resolved).unwrap();
}

#[test]
fn exact_pending_start_recovers_once_without_dispatching_or_forgetting_the_reservation() {
    let (root, id, request, mut stored) = recovery_fixture("start");
    let (saved_path, pending_path) = recovery_paths(&root, id);
    let before = std::fs::read(&saved_path).unwrap();
    let mut candidate = JobJournal::from_bytes(&before).unwrap();
    let approval = request
        .approve_outbound(request.content_hash().unwrap())
        .unwrap();
    candidate.begin(&request, &approval).unwrap();
    let pending = candidate.archive_bytes().unwrap();
    std::fs::write(&pending_path, &pending).unwrap();
    let view = stored.recovery().unwrap().unwrap();
    assert!(view.can_promote && view.can_keep_saved);
    assert_eq!(view.candidate.unwrap().state, JobState::Running);
    assert_eq!(std::fs::read(&saved_path).unwrap(), before);
    let recovered = stored
        .recover_pending(view.saved_hash, view.pending_hash, true)
        .unwrap();
    assert_eq!(recovered.state, JobState::Running);
    assert_eq!(recovered.attempts, 1);
    assert_eq!(recovered.uncertain_attempts, 1);
    assert!(stored.begin(&request, &approval).is_err());
    assert!(
        stored
            .recover_pending(view.saved_hash, view.pending_hash, true)
            .is_err()
    );
    assert_eq!(
        std::fs::read(saved_path.parent().unwrap().join(format!(
            "recovery-{}.json",
            forge_core::bytes_sha256(&before)
        )))
        .unwrap(),
        before
    );
    assert_eq!(
        std::fs::read(
            saved_path
                .parent()
                .unwrap()
                .join(format!("recovery-{}.json", view.pending_hash))
        )
        .unwrap(),
        pending
    );
    drop(stored);
    let reopened = StoredJob::open(&root, id).unwrap();
    assert_eq!(reopened.snapshot().unwrap(), recovered);
    drop(reopened);
    discard_recovery_fixture(&root);
}

#[test]
fn recovery_rejects_changed_pending_bytes_and_a_multi_event_jump_without_touching_either_version() {
    let (root, id, request, mut stored) = recovery_fixture("changed");
    let (saved_path, pending_path) = recovery_paths(&root, id);
    let before = std::fs::read(&saved_path).unwrap();
    let mut candidate = JobJournal::from_bytes(&before).unwrap();
    let approval = request
        .approve_outbound(request.content_hash().unwrap())
        .unwrap();
    candidate.begin(&request, &approval).unwrap();
    std::fs::write(&pending_path, candidate.archive_bytes().unwrap()).unwrap();
    let old_view = stored.recovery().unwrap().unwrap();
    candidate.interrupt(1).unwrap();
    let newer = candidate.archive_bytes().unwrap();
    std::fs::write(&pending_path, &newer).unwrap();
    assert!(
        stored
            .recover_pending(old_view.saved_hash, old_view.pending_hash, true)
            .is_err()
    );
    let new_view = stored.recovery().unwrap().unwrap();
    assert!(!new_view.can_promote);
    assert!(
        stored
            .recover_pending(new_view.saved_hash, new_view.pending_hash, true)
            .is_err()
    );
    assert_eq!(std::fs::read(&saved_path).unwrap(), before);
    assert_eq!(std::fs::read(&pending_path).unwrap(), newer);
    drop(stored);
    discard_recovery_fixture(&root);
}

#[test]
fn foreign_metadata_is_not_a_recoverable_extension_and_keeping_saved_archives_the_candidate() {
    let (root, id, _, stored) = recovery_fixture("foreign");
    let (saved_path, pending_path) = recovery_paths(&root, id);
    let before = std::fs::read(&saved_path).unwrap();
    let mut changed: serde_json::Value = serde_json::from_slice(&before).unwrap();
    changed["request_hash"] = serde_json::json!(forge_core::bytes_sha256(b"another request"));
    let foreign = serde_json::to_vec(&changed).unwrap();
    assert!(JobJournal::from_bytes(&foreign).is_ok());
    std::fs::write(&pending_path, &foreign).unwrap();
    drop(stored);
    assert!(StoredJob::open(&root, id).is_err());
    let view = StoredJob::pending_recovery(&root, id).unwrap().unwrap();
    assert!(!view.can_promote && view.can_keep_saved);
    assert!(
        StoredJob::recover_interrupted(&root, id, view.saved_hash, view.pending_hash, true)
            .is_err()
    );
    let stored =
        StoredJob::recover_interrupted(&root, id, view.saved_hash, view.pending_hash, false)
            .unwrap();
    assert_eq!(stored.snapshot().unwrap().state, JobState::Prepared);
    assert_eq!(std::fs::read(&saved_path).unwrap(), before);
    assert_eq!(
        std::fs::read(
            saved_path
                .parent()
                .unwrap()
                .join(format!("recovery-{}.json", view.pending_hash))
        )
        .unwrap(),
        foreign
    );
    assert!(!pending_path.exists());
    drop(stored);
    discard_recovery_fixture(&root);
}

#[test]
fn malformed_pending_data_can_only_be_archived_while_preserving_a_valid_saved_journal() {
    let (root, id, _, mut stored) = recovery_fixture("malformed");
    let (saved_path, pending_path) = recovery_paths(&root, id);
    let before = std::fs::read(&saved_path).unwrap();
    std::fs::write(&pending_path, b"interrupted invalid JSON").unwrap();
    let view = stored.recovery().unwrap().unwrap();
    assert!(view.candidate_error.is_some());
    assert!(view.candidate.is_none());
    assert!(!view.can_promote && view.can_keep_saved);
    assert!(
        stored
            .recover_pending(view.saved_hash, view.pending_hash, true)
            .is_err()
    );
    stored
        .recover_pending(view.saved_hash, view.pending_hash, false)
        .unwrap();
    assert_eq!(
        std::fs::read(
            saved_path
                .parent()
                .unwrap()
                .join(format!("recovery-{}.json", view.pending_hash))
        )
        .unwrap(),
        b"interrupted invalid JSON"
    );
    assert_eq!(std::fs::read(&saved_path).unwrap(), before);
    drop(stored);
    discard_recovery_fixture(&root);
}

#[test]
fn an_initial_pending_journal_can_be_promoted_but_cannot_skip_the_prepared_state() {
    let (root, id, request, stored) = recovery_fixture("initial");
    let (saved_path, pending_path) = recovery_paths(&root, id);
    drop(stored);
    std::fs::rename(&saved_path, &pending_path).unwrap();
    let view = StoredJob::pending_recovery(&root, id).unwrap().unwrap();
    assert_eq!(view.saved_hash, None);
    assert!(view.can_promote && !view.can_keep_saved);
    assert!(StoredJob::recover_interrupted(&root, id, None, view.pending_hash, false).is_err());
    let stored = StoredJob::recover_interrupted(&root, id, None, view.pending_hash, true).unwrap();
    stored.verify_request(&request).unwrap();
    drop(stored);
    let before = std::fs::read(&saved_path).unwrap();
    let mut skipped = JobJournal::from_bytes(&before).unwrap();
    skipped
        .begin(
            &request,
            &request
                .approve_outbound(request.content_hash().unwrap())
                .unwrap(),
        )
        .unwrap();
    std::fs::remove_file(&saved_path).unwrap();
    std::fs::write(&pending_path, skipped.archive_bytes().unwrap()).unwrap();
    let view = StoredJob::pending_recovery(&root, id).unwrap().unwrap();
    assert!(!view.can_promote && !view.can_keep_saved);
    assert!(StoredJob::recover_interrupted(&root, id, None, view.pending_hash, true).is_err());
    assert!(pending_path.exists());
    discard_recovery_fixture(&root);
}

#[test]
fn recovery_cannot_roll_back_a_durable_start_into_a_resendable_preparation() {
    let (root, id, request, mut stored) = recovery_fixture("rollback");
    let (saved_path, pending_path) = recovery_paths(&root, id);
    let old_preparation = std::fs::read(&saved_path).unwrap();
    let approval = request
        .approve_outbound(request.content_hash().unwrap())
        .unwrap();
    stored.begin(&request, &approval).unwrap();
    let started = std::fs::read(&saved_path).unwrap();
    std::fs::write(&pending_path, old_preparation).unwrap();
    let view = stored.recovery().unwrap().unwrap();
    assert!(!view.can_promote && view.can_keep_saved);
    assert!(
        stored
            .recover_pending(view.saved_hash, view.pending_hash, true)
            .is_err()
    );
    stored
        .recover_pending(view.saved_hash, view.pending_hash, false)
        .unwrap();
    assert_eq!(std::fs::read(&saved_path).unwrap(), started);
    assert_eq!(stored.snapshot().unwrap().uncertain_attempts, 1);
    assert!(stored.begin(&request, &approval).is_err());
    drop(stored);
    discard_recovery_fixture(&root);
}
