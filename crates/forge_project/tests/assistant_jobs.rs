use forge_core::{CommandId, Vec3, bytes_sha256};
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

fn request() -> ProviderRequest {
    let session =
        ZoneEditSession::from_initial_terrain(new_terrain("jobs", "zone", "Zone").unwrap())
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
fn limits(ceiling: u64) -> JobLimits {
    JobLimits {
        maximum_attempts: 2,
        maximum_input_tokens: 1024,
        maximum_output_tokens: 512,
        maximum_micro_usd: ceiling,
        prices: TokenPrices {
            input_per_million: 1_000_000,
            output_per_million: 2_000_000,
        },
    }
}
fn result(request: &ProviderRequest, output_tokens: u64) -> ProviderResult {
    ProviderResult {
        proposal: PlanProposal {
            context_hash: request.context_hash(),
            summary: "Proposed rename".into(),
            missing_capabilities: vec![],
            operations: vec![PlannedOperation::RenameZone {
                name: "Variant".into(),
            }],
        },
        usage: TokenUsage {
            input_tokens: 100,
            output_tokens,
        },
        response_id: "test-response".into(),
        model: "test-model".into(),
    }
}

#[test]
fn persisted_running_job_requires_explicit_interruption_and_keeps_uncertain_reservation() {
    let request = request();
    let approval = request
        .approve_outbound(request.content_hash().unwrap())
        .unwrap();
    let mut job = JobJournal::new(CommandId::deterministic("job"), &request, limits(4096)).unwrap();
    assert_eq!(job.begin(&request, &approval).unwrap(), 1);
    let mut reopened = JobJournal::from_bytes(&job.archive_bytes().unwrap()).unwrap();
    assert_eq!(reopened.snapshot().unwrap().state, JobState::Running);
    assert!(reopened.begin(&request, &approval).is_err());
    reopened.interrupt(1).unwrap();
    assert_eq!(reopened.begin(&request, &approval).unwrap(), 2);
    assert!(reopened.stage(1, result(&request, 20)).is_err());
    reopened.stage(2, result(&request, 20)).unwrap();
    let snapshot = reopened.snapshot().unwrap();
    assert_eq!(snapshot.state, JobState::Staged);
    assert_eq!(snapshot.committed_or_reserved_micro_usd, 2188);
    assert_eq!(snapshot.uncertain_attempts, 1);
    let mut reopened = JobJournal::from_bytes(&reopened.archive_bytes().unwrap()).unwrap();
    assert!(reopened.begin(&request, &approval).is_err());
    assert!(reopened.stage(2, result(&request, 20)).is_err());
    let hash = bytes_sha256(b"canonical-project-receipt");
    reopened
        .acknowledge_applied(CommandId::deterministic("job"), hash)
        .unwrap();
    let before = reopened.archive_bytes().unwrap();
    reopened
        .acknowledge_applied(CommandId::deterministic("job"), hash)
        .unwrap();
    assert_eq!(reopened.archive_bytes().unwrap(), before);
}

#[test]
fn ceiling_blocks_retry_without_mutating_the_journal() {
    let request = request();
    let approval = request
        .approve_outbound(request.content_hash().unwrap())
        .unwrap();
    let mut job = JobJournal::new(CommandId::deterministic("job"), &request, limits(2048)).unwrap();
    job.begin(&request, &approval).unwrap();
    job.interrupt(1).unwrap();
    let before = job.archive_bytes().unwrap();
    assert!(job.begin(&request, &approval).is_err());
    assert_eq!(job.archive_bytes().unwrap(), before);
    assert_eq!(
        job.snapshot().unwrap().committed_or_reserved_micro_usd,
        2048
    );
}

#[test]
fn over_budget_output_is_retained_but_cannot_be_applied_or_retried() {
    let request = request();
    let approval = request
        .approve_outbound(request.content_hash().unwrap())
        .unwrap();
    let mut job = JobJournal::new(CommandId::deterministic("job"), &request, limits(4096)).unwrap();
    job.begin(&request, &approval).unwrap();
    job.stage(1, result(&request, 513)).unwrap();
    let snapshot = job.snapshot().unwrap();
    assert_eq!(snapshot.state, JobState::BudgetExceeded);
    assert!(snapshot.result.is_some());
    assert!(job.begin(&request, &approval).is_err());
    assert!(
        job.acknowledge_applied(CommandId::deterministic("job"), bytes_sha256(b"fake"))
            .is_err()
    );
    assert_eq!(
        JobJournal::from_bytes(&job.archive_bytes().unwrap())
            .unwrap()
            .snapshot()
            .unwrap(),
        snapshot
    );
}

#[test]
fn invalid_replay_wrong_context_and_arithmetic_overflow_fail_closed() {
    let request = request();
    let approval = request
        .approve_outbound(request.content_hash().unwrap())
        .unwrap();
    let mut job = JobJournal::new(CommandId::deterministic("job"), &request, limits(4096)).unwrap();
    job.begin(&request, &approval).unwrap();
    let mut wrong = result(&request, 20);
    wrong.proposal.context_hash = bytes_sha256(b"wrong");
    assert!(job.stage(1, wrong).is_err());
    let mut value: serde_json::Value =
        serde_json::from_slice(&job.archive_bytes().unwrap()).unwrap();
    value["events"][0]["attempt"] = serde_json::json!(2);
    assert!(JobJournal::from_bytes(&serde_json::to_vec(&value).unwrap()).is_err());
    let prices = TokenPrices {
        input_per_million: u64::MAX,
        output_per_million: u64::MAX,
    };
    assert!(
        prices
            .charge(TokenUsage {
                input_tokens: u64::MAX,
                output_tokens: u64::MAX
            })
            .is_err()
    );
}

#[test]
fn lost_source_restores_only_the_exact_applied_request_and_retains_usage_and_history() {
    let request = request();
    let approval = request
        .approve_outbound(request.content_hash().unwrap())
        .unwrap();
    let id = CommandId::deterministic("lost-source");
    let mut job = JobJournal::new(id, &request, limits(4096)).unwrap();
    assert!(job.restore_unpersisted_application(&request).is_err());
    job.begin(&request, &approval).unwrap();
    job.stage(1, result(&request, 20)).unwrap();
    assert!(job.restore_unpersisted_application(&request).is_err());
    let applied_hash = bytes_sha256(b"unsaved-source");
    job.acknowledge_applied(id, applied_hash).unwrap();
    let applied_bytes = job.archive_bytes().unwrap();
    let original = job.snapshot().unwrap();
    let different_request = request.clone().with_local_reasoning().unwrap();
    assert!(
        job.restore_unpersisted_application(&different_request)
            .is_err()
    );
    assert_eq!(job.archive_bytes().unwrap(), applied_bytes);
    job.restore_unpersisted_application(&request).unwrap();
    let restored = job.snapshot().unwrap();
    assert_eq!(restored.state, JobState::Staged);
    assert_eq!(restored.result, original.result);
    assert_eq!(restored.attempts, original.attempts);
    assert_eq!(
        restored.committed_or_reserved_micro_usd,
        original.committed_or_reserved_micro_usd
    );
    assert_eq!(restored.uncertain_attempts, original.uncertain_attempts);
    assert_eq!(restored.resulting_hash, None);
    assert!(job.begin(&request, &approval).is_err());
    let restored_bytes = job.archive_bytes().unwrap();
    assert_eq!(
        JobJournal::from_bytes(&restored_bytes)
            .unwrap()
            .snapshot()
            .unwrap(),
        restored
    );
    let mut tampered: serde_json::Value = serde_json::from_slice(&restored_bytes).unwrap();
    tampered["events"][3]["resulting_hash"] = serde_json::json!(bytes_sha256(b"foreign"));
    assert!(JobJournal::from_bytes(&serde_json::to_vec(&tampered).unwrap()).is_err());
    assert!(job.restore_unpersisted_application(&request).is_err());
    assert_eq!(job.archive_bytes().unwrap(), restored_bytes);
    job.acknowledge_applied(id, bytes_sha256(b"saved-after-fresh-review"))
        .unwrap();
    assert_eq!(job.snapshot().unwrap().state, JobState::Applied);
    let history: serde_json::Value = serde_json::from_slice(&job.archive_bytes().unwrap()).unwrap();
    assert_eq!(history["events"].as_array().unwrap().len(), 5);
}

#[test]
fn reasoning_budget_increase_is_bounded_and_requires_its_own_outbound_approval() {
    let session = ZoneEditSession::from_initial_terrain(
        new_terrain("reasoning-budget", "zone", "Zone").unwrap(),
    )
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
    let request = |tokens| {
        ProviderRequest::new(
            ApiKind::ChatCompletions,
            "http://127.0.0.1:8080/v1/chat/completions",
            "test-model",
            "Rename",
            scope.context(),
            tokens,
        )
    };
    let old = request(8192).unwrap();
    let enlarged = request(16_384).unwrap();
    let old_approval = old.approve_outbound(old.content_hash().unwrap()).unwrap();
    assert!(enlarged.verify_outbound(&old_approval).is_err());
    assert!(request(16_385).is_err());
    let mut ceiling = limits(100_000);
    ceiling.maximum_output_tokens = 16_384;
    let mut journal = JobJournal::new(
        CommandId::deterministic("reasoning-budget"),
        &enlarged,
        ceiling,
    )
    .unwrap();
    let approval = enlarged
        .approve_outbound(enlarged.content_hash().unwrap())
        .unwrap();
    assert_eq!(journal.begin(&enlarged, &approval).unwrap(), 1);
    let before = journal.archive_bytes().unwrap();
    ceiling.maximum_output_tokens = 16_385;
    assert!(JobJournal::new(CommandId::deterministic("too-large"), &enlarged, ceiling).is_err());
    assert_eq!(journal.archive_bytes().unwrap(), before);
}
