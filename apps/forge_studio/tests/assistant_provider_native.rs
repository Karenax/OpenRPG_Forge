#![cfg(not(target_arch = "wasm32"))]
use forge_project::assistant::provider::ApiKind;
use forge_studio_frontend::assistant_provider_native::NativeProvider;

#[test]
fn native_configuration_rejects_credential_urls_plain_remote_http_and_invalid_schemes() {
    for base in [
        "http://example.test/v1",
        "file:///v1",
        "https://user:secret@example.test/v1",
        "https://example.test/v1?key=secret",
        "https://example.test/v1#fragment",
        "http://127.0.0.1:8080/not-an-api",
    ] {
        assert!(NativeProvider::new(base, "test-model", ApiKind::ChatCompletions, None).is_err());
    }
    assert!(
        NativeProvider::new(
            "http://127.0.0.1:8080/v1",
            "test-model",
            ApiKind::ChatCompletions,
            None
        )
        .is_ok()
    );
    assert!(
        NativeProvider::new(
            "https://api.openai.com/v1",
            "test-model",
            ApiKind::Responses,
            None
        )
        .is_ok()
    );
}

#[test]
#[ignore = "Requires an explicitly configured local provider; sends only a synthetic zone name"]
fn configured_local_provider_generates_a_plan_that_uses_the_real_governed_transaction() {
    use forge_core::{CommandId, Vec3};
    use forge_project::{
        assistant::{
            self,
            jobs::{JobJournal, JobLimits, JobState, TokenPrices},
            provider::ProviderRequest,
            selection::{Region, Selection, SelectionScope},
        },
        audit::AuditStamp,
        authoring::new_terrain,
        zone_authoring::session::ZoneEditSession,
    };
    use forge_studio_frontend::assistant_job_native::StoredJob;
    let base =
        std::env::var("OPENRPG_GATE6_TEST_PROVIDER_URL").expect("explicit test provider URL");
    let model =
        std::env::var("OPENRPG_GATE6_TEST_PROVIDER_MODEL").expect("explicit test provider model");
    let provider = NativeProvider::new(&base, &model, ApiKind::ChatCompletions, None).unwrap();
    let mut session = ZoneEditSession::from_initial_terrain(
        new_terrain("live-provider-smoke", "zone", "Synthetic zone").unwrap(),
    )
    .unwrap();
    let scope = SelectionScope::new(
        session.head(),
        &Selection {
            zone_properties: true,
            ..Selection::default()
        },
        Region {
            minimum: Vec3::new(-10.0, -10.0, -10.0),
            maximum: Vec3::new(10.0, 10.0, 10.0),
        },
    )
    .unwrap();
    let request = ProviderRequest::new(ApiKind::ChatCompletions, provider.destination(), &model, "Rename the zone to Local provider smoke. Do not change elevation or anything else. Use exactly one rename_zone operation.", scope.context(), 2048).unwrap();
    let request = request.with_local_reasoning().unwrap();
    let approval = request
        .approve_outbound(request.content_hash().unwrap())
        .unwrap();
    let job_id = CommandId::deterministic("live-provider-smoke/rename");
    let root = std::env::temp_dir().join(format!("forge-live-provider-job-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let journal = JobJournal::new(
        job_id,
        &request,
        JobLimits {
            maximum_attempts: 1,
            maximum_input_tokens: 100_000,
            maximum_output_tokens: 2048,
            maximum_micro_usd: 0,
            prices: TokenPrices {
                input_per_million: 0,
                output_per_million: 0,
            },
        },
    )
    .unwrap();
    let mut stored = StoredJob::create(&root, journal).unwrap();
    let attempt = stored.begin(&request, &approval).unwrap();
    let result = provider.generate(&request, &approval).unwrap();
    stored.stage(attempt, result.clone()).unwrap();
    drop(stored);
    let mut stored = StoredJob::open(&root, job_id).unwrap();
    assert_eq!(stored.snapshot().unwrap().state, JobState::Staged);
    let result = stored.snapshot().unwrap().result.unwrap();
    let command = result
        .proposal
        .compile(session.head(), &scope, job_id)
        .unwrap();
    let review = assistant::prepare(
        &session,
        &command,
        scope.policy(),
        AuditStamp::ai(&result.model, "2026-10-22T14:00:00Z").unwrap(),
    )
    .unwrap();
    assert_eq!(review.changes().len(), 1);
    assert_eq!(review.changes()[0].path, "/corrections/name");
    let approval = review.approve(review.content_hash().unwrap()).unwrap();
    let receipt = assistant::apply(&mut session, scope.policy(), &review, &approval).unwrap();
    stored
        .acknowledge_applied(receipt.request.command_id, receipt.resulting_hash)
        .unwrap();
    assert_eq!(stored.snapshot().unwrap().state, JobState::Applied);
    let reopened = ZoneEditSession::from_archive_bytes(&session.archive_bytes().unwrap()).unwrap();
    assert_eq!(
        reopened.head().effective().unwrap().0.name,
        "Local provider smoke"
    );
    println!(
        "{}",
        serde_json::json!({"provider":base,"model":result.model,"response_id":result.response_id,"usage":result.usage,"proposal":result.proposal,"resulting_hash":reopened.head().content_hash().unwrap()})
    );
    drop(stored);
    assert_eq!(root.parent().unwrap(), std::env::temp_dir());
    std::fs::remove_dir_all(root).unwrap();
}
