#![cfg(not(target_arch = "wasm32"))]
use forge_core::{CommandId, ContentHash, Vec3, bytes_sha256};
use forge_project::{
    assistant::{
        jobs::{JobLimits, JobState, TokenPrices},
        planning::{PlanProposal, PlannedOperation},
        provider::{ApiKind, ProviderResult, TokenUsage},
        selection::{Region, Selection, SelectionScope},
    },
    audit::AuditStamp,
    authoring::{AuthoringRequest as A, host::AuthoringHost, new_terrain},
    terrain_edit::{TerrainAction, TerrainEdit},
};
use forge_studio_frontend::{
    assistant_native::AssistantCoordinator,
    assistant_protocol::{AssistantDraft, AssistantRequest, AssistantView},
    launch_configuration::AssistantConfiguration,
};
use std::path::PathBuf;

fn configuration() -> AssistantConfiguration {
    AssistantConfiguration {
        base_url: "http://127.0.0.1:8080/v1".into(),
        model: "test-model".into(),
        api_kind: ApiKind::ChatCompletions,
        enable_reasoning: true,
        api_key_environment: None,
        limits: JobLimits {
            maximum_attempts: 2,
            maximum_input_tokens: 1024,
            maximum_output_tokens: 512,
            maximum_micro_usd: 0,
            prices: TokenPrices {
                input_per_million: 0,
                output_per_million: 0,
            },
        },
    }
}
fn fixture(label: &str) -> (PathBuf, AuthoringHost, AssistantCoordinator, AssistantDraft) {
    let root = std::env::temp_dir().join(format!(
        "forge-assistant-coordinator-{label}-{}",
        std::process::id()
    ));
    std::fs::create_dir(&root).unwrap();
    let mut host = AuthoringHost::new(&root)
        .unwrap()
        .with_zone_validator(|head| {
            if head.corrections.name.as_deref() == Some("Forbidden") {
                Err("Native candidate rejected".into())
            } else {
                Ok(())
            }
        });
    let state = host
        .execute(A::CreateZone {
            document: Box::new(new_terrain("coordinator", "zone", "Original").unwrap()),
        })
        .unwrap();
    host.execute(A::Save).unwrap();
    let draft = AssistantDraft {
        expected_head: state.zone_head.unwrap().content_hash().unwrap(),
        brief: "Rename the zone to Variant".into(),
        selection: Selection {
            zone_properties: true,
            ..Selection::default()
        },
        region: Region {
            minimum: Vec3::new(-5.0, -5.0, -5.0),
            maximum: Vec3::new(5.0, 5.0, 5.0),
        },
        resume_job: None,
        assets: None,
    };
    let coordinator = AssistantCoordinator::new(&root, configuration()).unwrap();
    (root, host, coordinator, draft)
}
fn result(view: &AssistantView, name: &str) -> ProviderResult {
    let body = view.outbound_body.as_ref().unwrap();
    let payload: serde_json::Value =
        serde_json::from_str(body["messages"][1]["content"].as_str().unwrap()).unwrap();
    ProviderResult {
        proposal: PlanProposal {
            context_hash: ContentHash::parse(payload["context_hash"].as_str().unwrap()).unwrap(),
            summary: "Simulated transport callback".into(),
            missing_capabilities: vec![],
            operations: vec![PlannedOperation::RenameZone { name: name.into() }],
        },
        usage: TokenUsage {
            input_tokens: 100,
            output_tokens: 30,
        },
        response_id: "simulated-transport-response".into(),
        model: "test-model".into(),
    }
}
fn stage(
    coordinator: &mut AssistantCoordinator,
    host: &mut AuthoringHost,
    view: &AssistantView,
    name: &str,
) {
    let id = view.job.as_ref().unwrap().job_id;
    let generation = coordinator
        .begin(host, id, view.outbound_hash.unwrap())
        .unwrap();
    let attempt = generation.attempt;
    drop(generation);
    coordinator
        .finish(id, attempt, Ok(result(view, name)))
        .unwrap();
}
fn manual_rename(host: &mut AuthoringHost, name: &str) {
    let head = host.execute(A::Inspect).unwrap().zone_head.unwrap();
    let request = head
        .prepare_terrain_action(
            TerrainAction::Edit(TerrainEdit::Rename { name: name.into() }),
            CommandId::deterministic(format!("manual/{name}")),
        )
        .unwrap();
    host.execute(A::PreviewZone {
        request: request.clone(),
    })
    .unwrap();
    host.execute(A::ApplyZone { request }).unwrap();
}
fn cleanup(root: PathBuf) {
    assert_eq!(root.parent().unwrap(), std::env::temp_dir());
    std::fs::remove_dir_all(root).unwrap();
}

#[test]
fn disclosure_generation_review_and_apply_are_distinct_and_retries_do_not_duplicate_sources() {
    let (root, mut host, mut coordinator, draft) = fixture("flow");
    let before = host.execute(A::Inspect).unwrap();
    let view = coordinator.prepare(&mut host, &draft).unwrap();
    let id = view.job.as_ref().unwrap().job_id;
    assert_eq!(
        view.outbound_body.as_ref().unwrap()["chat_template_kwargs"]["enable_thinking"],
        true
    );
    assert_eq!(host.execute(A::Inspect).unwrap(), before);
    assert!(
        coordinator
            .begin(&mut host, id, bytes_sha256(b"wrong approval"))
            .is_err()
    );
    stage(&mut coordinator, &mut host, &view, "Variant");
    assert_eq!(host.execute(A::Inspect).unwrap(), before);
    assert!(
        coordinator
            .apply(&mut host, id, bytes_sha256(b"no review"))
            .is_err()
    );
    let reviewed = coordinator
        .review(&mut host, id, "2026-10-22T15:00:00Z")
        .unwrap();
    assert_eq!(reviewed.zone_name_before.as_deref(), Some("Original"));
    assert_eq!(host.execute(A::Inspect).unwrap(), before);
    assert!(
        coordinator
            .apply(&mut host, id, bytes_sha256(b"wrong diff"))
            .is_err()
    );
    let applied = coordinator
        .apply(&mut host, id, reviewed.review_hash.unwrap())
        .unwrap();
    assert_eq!(applied.job.unwrap().state, JobState::Applied);
    assert_eq!(applied.zone_name_before.as_deref(), Some("Original"));
    let state = host.execute(A::Inspect).unwrap();
    assert_eq!(state.document.as_ref().unwrap().name, "Variant");
    assert!(state.dirty);
    coordinator
        .apply(&mut host, id, reviewed.review_hash.unwrap())
        .unwrap();
    assert_eq!(host.execute(A::Inspect).unwrap(), state);
    drop(coordinator);
    drop(host);
    cleanup(root);
}

#[test]
fn manual_edits_during_generation_or_after_review_block_mutation_and_keep_human_work() {
    for phase in ["during-generation", "after-review"] {
        let (root, mut host, mut coordinator, draft) = fixture(phase);
        let view = coordinator.prepare(&mut host, &draft).unwrap();
        let id = view.job.as_ref().unwrap().job_id;
        let deferred_attempt = if phase == "during-generation" {
            let generation = coordinator
                .begin(&mut host, id, view.outbound_hash.unwrap())
                .unwrap();
            let attempt = generation.attempt;
            drop(generation); // The transport callback is deliberately deferred below.
            Some(attempt)
        } else {
            stage(&mut coordinator, &mut host, &view, "AI proposal");
            None
        };
        let review = if phase == "after-review" {
            Some(
                coordinator
                    .review(&mut host, id, "2026-10-22T15:00:00Z")
                    .unwrap(),
            )
        } else {
            None
        };
        manual_rename(&mut host, "Human correction");
        let before = host.execute(A::Inspect).unwrap();
        if let Some(attempt) = deferred_attempt {
            coordinator
                .finish(id, attempt, Ok(result(&view, "AI proposal")))
                .unwrap();
        }
        if let Some(review) = review {
            assert!(
                coordinator
                    .apply(&mut host, id, review.review_hash.unwrap())
                    .is_err()
            );
        } else {
            assert!(
                coordinator
                    .review(&mut host, id, "2026-10-22T15:00:00Z")
                    .is_err()
            );
        }
        assert_eq!(host.execute(A::Inspect).unwrap(), before);
        drop(coordinator);
        drop(host);
        cleanup(root);
    }
}

#[test]
fn changed_resume_draft_and_native_rejected_candidate_do_not_reuse_old_approval() {
    let (root, mut host, mut coordinator, draft) = fixture("rebind");
    let view = coordinator.prepare(&mut host, &draft).unwrap();
    let id = view.job.as_ref().unwrap().job_id;
    let mut changed = draft;
    changed.resume_job = Some(id);
    changed.brief = "Another brief".into();
    assert!(coordinator.prepare(&mut host, &changed).is_err());
    assert_eq!(
        coordinator.inspect(id).unwrap().outbound_hash,
        view.outbound_hash
    );
    stage(&mut coordinator, &mut host, &view, "Forbidden");
    let before = host.execute(A::Inspect).unwrap();
    assert!(
        coordinator
            .review(&mut host, id, "2026-10-22T15:00:00Z")
            .is_err()
    );
    assert_eq!(host.execute(A::Inspect).unwrap(), before);
    drop(coordinator);
    drop(host);
    cleanup(root);
}

#[test]
fn restored_running_job_requires_interruption_and_late_callback_cannot_consume_current_attempt() {
    let (root, mut host, mut coordinator, mut draft) = fixture("interrupted");
    let view = coordinator.prepare(&mut host, &draft).unwrap();
    let id = view.job.as_ref().unwrap().job_id;
    let generation = coordinator
        .begin(&mut host, id, view.outbound_hash.unwrap())
        .unwrap();
    drop(generation);
    drop(coordinator);
    draft.resume_job = Some(id);
    let mut coordinator = AssistantCoordinator::new(&root, configuration()).unwrap();
    let restored = coordinator.prepare(&mut host, &draft).unwrap();
    assert_eq!(restored.job.unwrap().state, JobState::Running);
    assert!(
        coordinator
            .begin(&mut host, id, view.outbound_hash.unwrap())
            .is_err()
    );
    coordinator.interrupt(id, 1).unwrap();
    let generation = coordinator
        .begin(&mut host, id, view.outbound_hash.unwrap())
        .unwrap();
    assert_eq!(generation.attempt, 2);
    drop(generation);
    assert!(
        coordinator
            .finish(id, 1, Ok(result(&view, "Late")))
            .is_err()
    );
    assert_eq!(
        coordinator
            .finish(id, 2, Ok(result(&view, "Current")))
            .unwrap()
            .job
            .unwrap()
            .state,
        JobState::Staged
    );
    drop(coordinator);
    drop(host);
    cleanup(root);
}

#[test]
fn resumed_job_discloses_persisted_limits_when_local_configuration_changes() {
    let (root, mut host, mut coordinator, mut draft) = fixture("persisted-limits");
    let view = coordinator.prepare(&mut host, &draft).unwrap();
    let id = view.job.as_ref().unwrap().job_id;
    let old_limits = view.provider.limits;
    stage(
        &mut coordinator,
        &mut host,
        &view,
        "Staged under the original budget",
    );
    let path = root
        .join("assistant-jobs")
        .join(id.to_string())
        .join("authoring.json");
    let before = std::fs::read(&path).unwrap();
    let source = host.execute(A::Inspect).unwrap();
    drop(coordinator);
    draft.resume_job = Some(id);
    let mut settings = configuration();
    settings.limits.maximum_attempts = 1;
    settings.limits.maximum_input_tokens = 2048;
    let current_limits = settings.limits;
    let mut coordinator = AssistantCoordinator::new(&root, settings).unwrap();
    assert_eq!(
        coordinator.configuration_view().provider.limits,
        current_limits
    );
    let reopened = coordinator.prepare(&mut host, &draft).unwrap();
    assert_eq!(reopened.provider.limits, old_limits);
    let job = reopened.job.unwrap();
    assert_eq!(job.state, JobState::Staged);
    assert_eq!(job.attempts, 1);
    assert_eq!(std::fs::read(path).unwrap(), before);
    assert_eq!(host.execute(A::Inspect).unwrap(), source);
    drop(coordinator);
    drop(host);
    cleanup(root);
}

#[test]
fn staged_job_survives_restart_and_source_receipt_recovers_lost_acknowledgement() {
    let (root, mut host, mut coordinator, mut draft) = fixture("reconcile");
    let view = coordinator.prepare(&mut host, &draft).unwrap();
    let id = view.job.as_ref().unwrap().job_id;
    stage(&mut coordinator, &mut host, &view, "Recovered");
    drop(coordinator);
    draft.resume_job = Some(id);
    let mut coordinator = AssistantCoordinator::new(&root, configuration()).unwrap();
    assert_eq!(
        coordinator
            .prepare(&mut host, &draft)
            .unwrap()
            .job
            .unwrap()
            .state,
        JobState::Staged
    );
    drop(coordinator);
    // Simulate source Apply accepted just before process loss, without a job ACK.
    let head = host.execute(A::Inspect).unwrap().zone_head.unwrap();
    let scope = SelectionScope::new(&head, &draft.selection, draft.region).unwrap();
    let request = result(&view, "Recovered")
        .proposal
        .compile(&head, &scope, id)
        .unwrap();
    let review = host
        .prepare_assistant(
            request,
            scope.policy(),
            AuditStamp::ai("test-model", "2026-10-22T15:00:00Z").unwrap(),
        )
        .unwrap();
    let approval = review.approve(review.content_hash().unwrap()).unwrap();
    host.apply_assistant(scope.policy(), &review, &approval)
        .unwrap();
    host.execute(A::Save).unwrap();
    let before = host.execute(A::Inspect).unwrap();
    let hash = before.zone_head.as_ref().unwrap().content_hash().unwrap();
    let archive = host.game_archive(hash).unwrap();
    let mut settings = configuration();
    settings.limits.maximum_output_tokens = 16384;
    let mut coordinator = AssistantCoordinator::new(&root, settings).unwrap();
    let recovered = coordinator.prepare(&mut host, &draft).unwrap();
    assert_eq!(recovered.provider.limits, configuration().limits);
    assert_eq!(recovered.provider.maximum_request_seconds, 120);
    assert_eq!(recovered.job.unwrap().state, JobState::Applied);
    assert_eq!(host.execute(A::Inspect).unwrap(), before);
    assert_eq!(host.game_archive(hash).unwrap(), archive);
    drop(coordinator);
    drop(host);
    cleanup(root);
}

#[test]
fn cancelled_generation_cannot_restore_an_applicable_plan_and_protocol_rejects_provider_overrides()
{
    let (root, mut host, mut coordinator, draft) = fixture("cancel");
    let view = coordinator.prepare(&mut host, &draft).unwrap();
    let id = view.job.as_ref().unwrap().job_id;
    let generation = coordinator
        .begin(&mut host, id, view.outbound_hash.unwrap())
        .unwrap();
    drop(generation);
    let before = host.execute(A::Inspect).unwrap();
    coordinator.cancel(&mut host, id).unwrap();
    assert_eq!(
        coordinator
            .finish(id, 1, Ok(result(&view, "Late cancelled")))
            .unwrap()
            .job
            .unwrap()
            .state,
        JobState::Cancelled
    );
    assert!(
        coordinator
            .review(&mut host, id, "2026-10-22T15:00:00Z")
            .is_err()
    );
    assert_eq!(host.execute(A::Inspect).unwrap(), before);
    let invalid = serde_json::json!({"action":"generate","job_id":id,"expected_outbound_hash":view.outbound_hash,"base_url":"https://unapproved.test/v1"});
    assert!(serde_json::from_value::<AssistantRequest>(invalid).is_err());
    drop(coordinator);
    drop(host);
    cleanup(root);
}

#[test]
fn recovering_a_loaded_journal_drops_the_old_review_and_never_applies_source() {
    let (root, mut host, mut coordinator, mut draft) = fixture("pending-loaded");
    let view = coordinator.prepare(&mut host, &draft).unwrap();
    let id = view.job.as_ref().unwrap().job_id;
    stage(&mut coordinator, &mut host, &view, "Recovered pending");
    let old = coordinator
        .review(&mut host, id, "2026-10-22T17:00:00Z")
        .unwrap();
    let path = root.join("assistant-jobs").join(id.to_string());
    std::fs::copy(path.join("authoring.json"), path.join("authoring.pending")).unwrap();
    let before = host.execute(A::Inspect).unwrap();
    assert!(
        coordinator
            .apply(&mut host, id, old.review_hash.unwrap())
            .is_err()
    );
    let page = coordinator.inspect_recoveries(0, Some(id)).unwrap();
    let pending = &page.recovery_page.unwrap().jobs[0];
    let recovered = coordinator
        .recover_job(id, pending.saved_hash, pending.pending_hash, true)
        .unwrap();
    assert_eq!(recovered.recovered_job, Some(id));
    assert!(
        recovered.job.is_none()
            && recovered.review_hash.is_none()
            && recovered.outbound_hash.is_none()
    );
    assert!(
        coordinator
            .apply(&mut host, id, old.review_hash.unwrap())
            .is_err()
    );
    assert_eq!(host.execute(A::Inspect).unwrap(), before);
    draft.resume_job = Some(id);
    let resumed = coordinator.prepare(&mut host, &draft).unwrap();
    assert_eq!(resumed.job.as_ref().unwrap().attempts, 1);
    assert!(resumed.review_hash.is_none());
    let new = coordinator
        .review(&mut host, id, "2026-10-22T17:01:00Z")
        .unwrap();
    assert_ne!(old.review_hash, new.review_hash);
    assert!(
        coordinator
            .apply(&mut host, id, old.review_hash.unwrap())
            .is_err()
    );
    coordinator
        .apply(&mut host, id, new.review_hash.unwrap())
        .unwrap();
    assert_eq!(
        host.execute(A::Inspect)
            .unwrap()
            .zone_head
            .unwrap()
            .effective()
            .unwrap()
            .0
            .name,
        "Recovered pending"
    );
    drop(coordinator);
    drop(host);
    cleanup(root);
}

fn recovery_request(
    host: &mut AuthoringHost,
    draft: &AssistantDraft,
    view: &AssistantView,
) -> forge_project::assistant::provider::ProviderRequest {
    use forge_project::assistant::provider::ProviderRequest;
    let head = host.execute(A::Inspect).unwrap().zone_head.unwrap();
    let scope = SelectionScope::new(&head, &draft.selection, draft.region).unwrap();
    let request = ProviderRequest::new(
        ApiKind::ChatCompletions,
        &view.provider.destination,
        &view.provider.model,
        &draft.brief,
        scope.context(),
        512,
    )
    .unwrap()
    .with_local_reasoning()
    .unwrap();
    assert_eq!(request.content_hash().unwrap(), view.outbound_hash.unwrap());
    request
}

#[test]
fn pending_start_is_discovered_after_restart_and_cannot_dispatch_or_recover_during_an_active_call()
{
    use forge_project::assistant::jobs::JobJournal;
    let (root, mut host, mut coordinator, mut draft) = fixture("pending-restart");
    let view = coordinator.prepare(&mut host, &draft).unwrap();
    let id = view.job.as_ref().unwrap().job_id;
    let request = recovery_request(&mut host, &draft, &view);
    let directory = root.join("assistant-jobs").join(id.to_string());
    let mut pending =
        JobJournal::from_bytes(&std::fs::read(directory.join("authoring.json")).unwrap()).unwrap();
    pending
        .begin(
            &request,
            &request
                .approve_outbound(request.content_hash().unwrap())
                .unwrap(),
        )
        .unwrap();
    std::fs::write(
        directory.join("authoring.pending"),
        pending.archive_bytes().unwrap(),
    )
    .unwrap();
    drop(coordinator);
    let mut coordinator = AssistantCoordinator::new(&root, configuration()).unwrap();
    let page = coordinator.inspect_recoveries(0, None).unwrap();
    let entry = &page.recovery_page.unwrap().jobs[0];
    let before = host.execute(A::Inspect).unwrap();
    coordinator
        .recover_job(id, entry.saved_hash, entry.pending_hash, true)
        .unwrap();
    draft.resume_job = Some(id);
    let running = coordinator.prepare(&mut host, &draft).unwrap();
    assert_eq!(running.job.as_ref().unwrap().state, JobState::Running);
    assert_eq!(running.job.as_ref().unwrap().uncertain_attempts, 1);
    assert!(
        coordinator
            .begin(&mut host, id, running.outbound_hash.unwrap())
            .is_err()
    );
    coordinator.interrupt(id, 1).unwrap();
    let generation = coordinator
        .begin(&mut host, id, running.outbound_hash.unwrap())
        .unwrap();
    assert!(
        coordinator
            .recover_job(id, entry.saved_hash, entry.pending_hash, true)
            .is_err()
    );
    assert_eq!(host.execute(A::Inspect).unwrap(), before);
    drop(generation);
    drop(coordinator);
    drop(host);
    cleanup(root);
}

#[test]
fn paged_recovery_finds_an_initial_journal_without_a_loaded_or_remembered_job() {
    use forge_project::assistant::jobs::JobJournal;
    use forge_studio_frontend::assistant_job_native::StoredJob;
    let (root, mut host, mut coordinator, mut draft) = fixture("pending-pages");
    let view = coordinator.prepare(&mut host, &draft).unwrap();
    let request = recovery_request(&mut host, &draft, &view);
    let mut orphan = None;
    for index in 0..33 {
        let id = CommandId::deterministic(format!("recovery-page/{index}"));
        let store = StoredJob::create(
            &root,
            JobJournal::new(id, &request, configuration().limits).unwrap(),
        )
        .unwrap();
        drop(store);
        if index == 32 {
            let directory = root.join("assistant-jobs").join(id.to_string());
            std::fs::rename(
                directory.join("authoring.json"),
                directory.join("authoring.pending"),
            )
            .unwrap();
            orphan = Some(id);
        }
    }
    let mut entries = Vec::new();
    let mut offset = 0;
    loop {
        let page = coordinator
            .inspect_recoveries(offset, None)
            .unwrap()
            .recovery_page
            .unwrap();
        entries.extend(page.jobs);
        let Some(next) = page.next_offset else {
            break;
        };
        assert!(next > offset);
        offset = next;
    }
    assert_eq!(entries.len(), 1);
    assert_eq!(Some(entries[0].job_id), orphan);
    assert_eq!(entries[0].saved_hash, None);
    assert!(entries[0].can_promote);
    let before = host.execute(A::Inspect).unwrap();
    coordinator
        .recover_job(entries[0].job_id, None, entries[0].pending_hash, true)
        .unwrap();
    assert_eq!(host.execute(A::Inspect).unwrap(), before);
    draft.resume_job = orphan;
    let restored = coordinator.prepare(&mut host, &draft).unwrap();
    assert_eq!(restored.job.unwrap().state, JobState::Prepared);
    drop(coordinator);
    drop(host);
    cleanup(root);
}

#[test]
fn recovery_protocol_cannot_supply_a_filesystem_root_or_override_validation_flags() {
    let id = CommandId::deterministic("recovery-protocol");
    for extra in ["root", "can_promote"] {
        let mut value = serde_json::json!({"action":"recover_job","job_id":id,"expected_saved":null,"expected_pending":bytes_sha256(b"pending"),"promote":true});
        value[extra] = serde_json::json!("forbidden");
        assert!(serde_json::from_value::<AssistantRequest>(value).is_err());
    }
}

#[test]
fn application_lost_before_source_save_returns_to_review_without_another_provider_call() {
    let (root, mut host, mut coordinator, mut draft) = fixture("unsaved-application");
    let original = host.execute(A::Inspect).unwrap().zone_head.unwrap();
    let view = coordinator.prepare(&mut host, &draft).unwrap();
    let id = view.job.as_ref().unwrap().job_id;
    stage(&mut coordinator, &mut host, &view, "Recovered unsaved");
    let reviewed = coordinator
        .review(&mut host, id, "2026-10-22T18:00:00Z")
        .unwrap();
    let old_review = reviewed.review_hash.unwrap();
    coordinator.apply(&mut host, id, old_review).unwrap();
    // Source Save is deliberately omitted: a full process loss discards memory.
    drop(coordinator);
    drop(host);
    let mut host = AuthoringHost::new(&root).unwrap();
    host.execute(A::Open {
        area_id: original.base.terrain.area_id,
    })
    .unwrap();
    assert_eq!(
        host.execute(A::Inspect).unwrap().zone_head.unwrap(),
        original
    );
    assert!(host.assistant_receipt(id).unwrap().is_none());
    let mut coordinator = AssistantCoordinator::new(&root, configuration()).unwrap();
    draft.resume_job = Some(id);
    let restored = coordinator.prepare(&mut host, &draft).unwrap();
    let snapshot = restored.job.unwrap();
    assert_eq!(snapshot.state, JobState::Staged);
    assert_eq!(snapshot.attempts, 1);
    assert!(snapshot.result.is_some());
    assert!(restored.review_hash.is_none());
    assert!(coordinator.apply(&mut host, id, old_review).is_err());
    assert!(
        coordinator
            .begin(&mut host, id, restored.outbound_hash.unwrap())
            .is_err()
    );
    let review = coordinator
        .review(&mut host, id, "2026-10-22T18:01:00Z")
        .unwrap();
    coordinator
        .apply(&mut host, id, review.review_hash.unwrap())
        .unwrap();
    host.execute(A::Save).unwrap();
    let saved = host.execute(A::Inspect).unwrap();
    drop(coordinator);
    drop(host);
    let mut host = AuthoringHost::new(&root).unwrap();
    host.execute(A::Open {
        area_id: original.base.terrain.area_id,
    })
    .unwrap();
    let mut coordinator = AssistantCoordinator::new(&root, configuration()).unwrap();
    assert_eq!(
        coordinator
            .prepare(&mut host, &draft)
            .unwrap()
            .job
            .unwrap()
            .state,
        JobState::Applied
    );
    assert_eq!(host.execute(A::Inspect).unwrap().zone_head, saved.zone_head);
    drop(coordinator);
    drop(host);
    cleanup(root);
}

#[test]
fn lost_application_cannot_be_restored_over_later_human_source_changes() {
    let (root, mut host, mut coordinator, mut draft) = fixture("unsaved-human-change");
    let original = host.execute(A::Inspect).unwrap().zone_head.unwrap();
    let view = coordinator.prepare(&mut host, &draft).unwrap();
    let id = view.job.as_ref().unwrap().job_id;
    stage(&mut coordinator, &mut host, &view, "Lost AI change");
    let review = coordinator
        .review(&mut host, id, "2026-10-22T18:00:00Z")
        .unwrap();
    coordinator
        .apply(&mut host, id, review.review_hash.unwrap())
        .unwrap();
    drop(coordinator);
    drop(host);
    let mut host = AuthoringHost::new(&root).unwrap();
    host.execute(A::Open {
        area_id: original.base.terrain.area_id,
    })
    .unwrap();
    manual_rename(&mut host, "Human change after restart");
    host.execute(A::Save).unwrap();
    let before = host.execute(A::Inspect).unwrap();
    let path = root
        .join("assistant-jobs")
        .join(id.to_string())
        .join("authoring.json");
    let journal = std::fs::read(&path).unwrap();
    let mut coordinator = AssistantCoordinator::new(&root, configuration()).unwrap();
    draft.resume_job = Some(id);
    assert!(coordinator.prepare(&mut host, &draft).is_err());
    draft.expected_head = before.zone_head.as_ref().unwrap().content_hash().unwrap();
    assert!(coordinator.prepare(&mut host, &draft).is_err());
    assert_eq!(host.execute(A::Inspect).unwrap(), before);
    assert_eq!(std::fs::read(path).unwrap(), journal);
    drop(coordinator);
    drop(host);
    cleanup(root);
}

#[test]
fn reopening_unsaved_source_also_clears_a_review_retained_in_the_coordinator() {
    let (root, mut host, mut coordinator, mut draft) = fixture("unsaved-loaded-review");
    let original = host.execute(A::Inspect).unwrap().zone_head.unwrap();
    let view = coordinator.prepare(&mut host, &draft).unwrap();
    let id = view.job.as_ref().unwrap().job_id;
    stage(&mut coordinator, &mut host, &view, "Unsaved loaded change");
    let review = coordinator
        .review(&mut host, id, "2026-10-22T18:00:00Z")
        .unwrap();
    let old_hash = review.review_hash.unwrap();
    coordinator.apply(&mut host, id, old_hash).unwrap();
    drop(host);
    let mut host = AuthoringHost::new(&root).unwrap();
    host.execute(A::Open {
        area_id: original.base.terrain.area_id,
    })
    .unwrap();
    draft.resume_job = Some(id);
    let restored = coordinator.prepare(&mut host, &draft).unwrap();
    assert_eq!(restored.job.unwrap().state, JobState::Staged);
    assert!(restored.review_hash.is_none());
    assert!(restored.message.contains("carte sauvegardée"));
    assert!(coordinator.apply(&mut host, id, old_hash).is_err());
    assert_eq!(
        host.execute(A::Inspect).unwrap().zone_head.unwrap(),
        original
    );
    drop(coordinator);
    drop(host);
    cleanup(root);
}

#[test]
fn over_budget_wire_result_is_preserved_durably_without_application_or_retry() {
    let (root, mut host, mut coordinator, draft) = fixture("over-budget-wire");
    let view = coordinator.prepare(&mut host, &draft).unwrap();
    let id = view.job.as_ref().unwrap().job_id;
    let before = host.execute(A::Inspect).unwrap();
    let request = recovery_request(&mut host, &draft, &view);
    let generation = coordinator
        .begin(&mut host, id, view.outbound_hash.unwrap())
        .unwrap();
    drop(generation);
    let proposal = result(&view, "Over budget retained").proposal;
    let wire = serde_json::json!({"id":"over-budget-response","model":"test-model",
        "usage":{"prompt_tokens":100,"completion_tokens":513},"choices":[{"finish_reason":"stop","message":{"role":"assistant","content":serde_json::to_string(&proposal).unwrap()}}]});
    let parsed = request
        .parse_response(&serde_json::to_vec(&wire).unwrap())
        .unwrap();
    let finished = coordinator.finish(id, 1, Ok(parsed.clone())).unwrap();
    assert_eq!(finished.job.unwrap().state, JobState::BudgetExceeded);
    assert!(
        coordinator
            .review(&mut host, id, "2026-10-22T20:00:00Z")
            .is_err()
    );
    assert!(
        coordinator
            .apply(&mut host, id, bytes_sha256(b"forged approval"))
            .is_err()
    );
    assert!(
        coordinator
            .begin(&mut host, id, view.outbound_hash.unwrap())
            .is_err()
    );
    assert_eq!(host.execute(A::Inspect).unwrap(), before);
    drop(coordinator);
    let stored = forge_studio_frontend::assistant_job_native::StoredJob::open(&root, id).unwrap();
    let snapshot = stored.snapshot().unwrap();
    assert_eq!(snapshot.state, JobState::BudgetExceeded);
    assert_eq!(snapshot.result, Some(parsed));
    assert_eq!(snapshot.attempts, 1);
    drop(stored);
    drop(host);
    cleanup(root);
}
