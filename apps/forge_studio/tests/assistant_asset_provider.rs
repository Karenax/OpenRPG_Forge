#![cfg(not(target_arch = "wasm32"))]
use forge_core::{AssetId, CommandId, Vec3, bytes_sha256};
use forge_project::{
    assistant::{
        self,
        assets::{AssetCatalog, AssetReference},
        provider::{ApiKind, ProviderRequest},
        selection::{Region, Selection, SelectionScope},
    },
    audit::AuditStamp,
    authoring::new_terrain,
    zone_authoring::session::ZoneEditSession,
};
use forge_studio_frontend::assistant_provider_native::NativeProvider;

/// This is a real HTTP/schema/compiler test with synthetic trusted metadata.
/// Native Vault integrity and actual Studio use have separate qualification.
#[test]
#[ignore = "Requires explicitly configured local provider; sends only synthetic asset metadata"]
fn local_provider_places_only_the_selected_fixture_asset() {
    let base = std::env::var("OPENRPG_GATE6_TEST_PROVIDER_URL").expect("explicit local provider");
    let model = std::env::var("OPENRPG_GATE6_TEST_PROVIDER_MODEL").expect("explicit local model");
    let provider = NativeProvider::new(&base, &model, ApiKind::ChatCompletions, None).unwrap();
    let mut session = ZoneEditSession::from_initial_terrain(
        new_terrain("live-assets", "zone", "Synthetic asset fixture").unwrap(),
    )
    .unwrap();
    let asset_id = AssetId::deterministic("live-assets/wooden-door");
    let asset_hash =
        bytes_sha256(b"synthetic asset fixture; no real model or runtime qualification");
    let catalog = AssetCatalog::new(
        bytes_sha256(b"synthetic catalog"),
        vec![AssetReference {
            asset_id,
            model_hash: Some(asset_hash),
            physics: None,
            particles: None,
            name: "Synthetic wooden door".into(),
            pack: "Qualification fixture".into(),
            family: "construction".into(),
            asset_type: "door".into(),
            license: "CC0 fixture".into(),
            dimensions_m: [1.0, 2.0, 0.2],
            triangles: 12,
            metadata_ready: true,
            runtime_qualified: false,
            model_verified: true,
        }],
    )
    .unwrap()
    .authorize_placement()
    .unwrap();
    let scope = SelectionScope::new_with_assets(
        session.head(),
        &Selection::default(),
        Region {
            minimum: Vec3::new(-10.0, -10.0, -10.0),
            maximum: Vec3::new(10.0, 10.0, 10.0),
        },
        &catalog,
    )
    .unwrap();
    let request = ProviderRequest::new(ApiKind::ChatCompletions, provider.destination(), &model,
        "Place exactly one instance of the selected synthetic wooden door, named Porte de recette, at x=1 y=0 z=2, yaw_degrees=90 and scale=1. Use the key door-1. Preserve everything else. Return exactly one place_asset operation and no missing capabilities.",
        scope.context(), 2048).unwrap().with_local_reasoning().unwrap();
    assert_eq!(
        request.body()["chat_template_kwargs"]["enable_thinking"],
        true
    );
    let approval = request
        .approve_outbound(request.content_hash().unwrap())
        .unwrap();
    let result = provider.generate(&request, &approval).unwrap();
    assert_eq!(result.proposal.operations.len(), 1);
    let command = CommandId::deterministic("live-assets/one-door");
    let edit = result
        .proposal
        .compile(session.head(), &scope, command)
        .unwrap();
    let review = assistant::prepare(
        &session,
        &edit,
        scope.policy(),
        AuditStamp::ai(&result.model, "2026-10-22T14:00:00Z").unwrap(),
    )
    .unwrap();
    let approval = review.approve(review.content_hash().unwrap()).unwrap();
    assistant::apply(&mut session, scope.policy(), &review, &approval).unwrap();
    let reopened = ZoneEditSession::from_archive_bytes(&session.archive_bytes().unwrap()).unwrap();
    let placements = reopened.head().corrections.placements.as_ref().unwrap();
    assert_eq!(placements.len(), 1);
    assert_eq!(placements[0].asset_id, asset_id);
    assert_eq!(placements[0].asset_hash, asset_hash);
    assert_eq!(placements[0].name, "Porte de recette");
    assert_eq!(
        placements[0].transform.translation_m,
        Vec3::new(1.0, 0.0, 2.0)
    );
    assert_eq!(placements[0].transform.scale, Vec3::ONE);
    println!(
        "{}",
        serde_json::json!({"provider":base,"model":result.model,"usage":result.usage,"proposal":result.proposal,"reasoning_requested":true,"metadata":"synthetic trusted fixture, not Vault or Studio qualification","resulting_hash":reopened.head().content_hash().unwrap()})
    );
}

fn content_session() -> ZoneEditSession {
    use forge_core::{EntityId, ModuleId};
    let mut session = ZoneEditSession::from_initial_terrain(
        new_terrain("live-content", "zone", "Synthetic existing map").unwrap(),
    )
    .unwrap();
    let player = EntityId::deterministic("live-content/player");
    let document: forge_gameplay::GameplayDocument = serde_json::from_value(serde_json::json!({
        "schema":"forge.gameplay","version":1,"module_id":ModuleId::deterministic("live-content/module"),"area_id":session.head().base.terrain.area_id,"player":player,
        "profile":{"id":"synthetic-author-rules","version":1,"interaction_range_m":3.0,"attack_range_m":2.0,"attack_damage":20,"attack_stamina_cost":5,"max_stamina":100,"max_action_receipts":512,"max_events_per_tick":128},
        "actors":[{"id":player,"name":"Existing player","role":"player","position":{"x":0,"y":0,"z":0},"max_vitality":100,"protection":0,"inventory_capacity":20,"dialogue":null,"patrol":[],"flees_danger":false}],
        "items":[],"objects":[],"places":[],"dialogues":[],"quests":[]
    })).unwrap();
    let document = document
        .with_creator_configuration(document.creator_configuration())
        .unwrap();
    let request = session
        .head()
        .prepare_gameplay(
            Some(document),
            CommandId::deterministic("live-content/setup"),
        )
        .unwrap();
    session.execute(&request).unwrap();
    session
}

/// Real provider over synthetic metadata; this is not the Studio reference recipe.
#[test]
#[ignore = "Requires an explicitly configured local provider and a fresh evidence directory"]
fn local_provider_creates_a_reviewable_adventure_with_two_solutions() {
    use forge_project::assistant::{jobs::JobState, planning::PlannedOperation};
    let base = std::env::var("OPENRPG_GATE6_TEST_PROVIDER_URL").expect("explicit provider");
    let model = std::env::var("OPENRPG_GATE6_TEST_PROVIDER_MODEL").expect("explicit model");
    let utc = std::env::var("OPENRPG_GATE6_TEST_UTC").expect("actual UTC qualification timestamp");
    let maximum_output_tokens = std::env::var("OPENRPG_GATE6_TEST_OUTPUT_TOKENS")
        .map_or(8192, |value| {
            value.parse::<u32>().expect("explicit output token ceiling")
        });
    let root = std::path::PathBuf::from(
        std::env::var("OPENRPG_GATE6_TEST_ROOT").expect("fresh evidence root"),
    );
    std::fs::create_dir(&root).unwrap();
    let provider = NativeProvider::new(&base, &model, ApiKind::ChatCompletions, None).unwrap();
    let mut session = content_session();
    let original = session.head().clone();
    let scope = SelectionScope::new(
        session.head(),
        &Selection {
            create_content: true,
            ..Selection::default()
        },
        Region {
            minimum: Vec3::new(-10.0, -10.0, -10.0),
            maximum: Vec3::new(10.0, 10.0, 10.0),
        },
    )
    .unwrap();
    let brief = "Crée une courte aventure en français, La preuve du refuge. Le Gardien en (1,0,0) propose une quête avec deux solutions alternatives : rapporter une Preuve à ramasser en (2,0,0), ou écouter le témoignage du Temoin en (2,0,0), puis revenir voir le Gardien. Chaque solution termine la même quête et donne une seule fois un Insigne. Une Porte du refuge, initialement fermée en (4,0,0), reste ouverte après la réussite. Écris des salutations, choix de dialogue et textes de journal courts et cohérents. Ajoute trois lieux reliés : Village (0,0,0), Foret (2,0,0), Refuge (4,0,0), avec des liens aller-retour entre lieux voisins. Conserve intégralement le joueur, les règles et tout contenu déjà présent. Les deux approches doivent être jouables depuis (0,0,0). Ne demande aucune capacité absente. Raisonnement concis.";
    let request = ProviderRequest::new(
        ApiKind::ChatCompletions,
        provider.destination(),
        &model,
        brief,
        scope.context(),
        maximum_output_tokens,
    )
    .unwrap()
    .with_local_reasoning()
    .unwrap();
    assert_eq!(
        request.body()["chat_template_kwargs"]["enable_thinking"],
        true
    );
    let approval = request
        .approve_outbound(request.content_hash().unwrap())
        .unwrap();
    let id = CommandId::deterministic("live-content/adventure");
    let mut stored = content_job(&root, id, &request);
    let attempt = stored.begin(&request, &approval).unwrap();
    std::fs::write(
        root.join("outbound.json"),
        serde_json::to_vec_pretty(request.body()).unwrap(),
    )
    .unwrap();
    let started = std::time::Instant::now();
    let result = provider.generate(&request, &approval).unwrap();
    let seconds = started.elapsed().as_secs_f64();
    stored.stage(attempt, result.clone()).unwrap();
    assert_eq!(result.proposal.operations.len(), 2);
    assert!(
        result
            .proposal
            .operations
            .iter()
            .any(|operation| matches!(operation, PlannedOperation::CreateFetchQuest { .. }))
    );
    assert!(
        result
            .proposal
            .operations
            .iter()
            .any(|operation| matches!(operation, PlannedOperation::CreatePlaces { .. }))
    );
    let edit = result.proposal.compile(session.head(), &scope, id).unwrap();
    let review = assistant::prepare(
        &session,
        &edit,
        scope.policy(),
        AuditStamp::ai(&result.model, &utc).unwrap(),
    )
    .unwrap();
    let approval = review.approve(review.content_hash().unwrap()).unwrap();
    let receipt = assistant::apply(&mut session, scope.policy(), &review, &approval).unwrap();
    stored
        .acknowledge_applied(id, receipt.resulting_hash)
        .unwrap();
    assert_eq!(stored.snapshot().unwrap().state, JobState::Applied);
    verify_content_sources(&session, &original);
    std::fs::write(
        root.join("source.archive.json"),
        session.archive_bytes().unwrap(),
    )
    .unwrap();
    println!(
        "{}",
        serde_json::json!({"provider":base,"model":result.model,"reasoning_requested":true,"seconds":seconds,"usage":result.usage,"proposal":result.proposal,"review_hash":review.content_hash().unwrap(),"resulting_hash":receipt.resulting_hash,"scope":"real provider, synthetic metadata; Studio and playtest not qualified"})
    );
}

fn verify_content_sources(
    session: &ZoneEditSession,
    original: &forge_project::zone_authoring::ZoneAuthoringHead,
) {
    let reopened = ZoneEditSession::from_archive_bytes(&session.archive_bytes().unwrap()).unwrap();
    let document = reopened.head().corrections.gameplay.as_ref().unwrap();
    let before = original.corrections.gameplay.as_ref().unwrap();
    assert_eq!(document.profile, before.profile);
    assert_eq!(document.creator, before.creator);
    assert_eq!(document.actor(before.player), before.actor(before.player));
    assert_eq!(document.places.len(), 3);
    assert_eq!(document.quests.len(), 1);
    assert_eq!(
        document.narrative.as_ref().unwrap().quests[0]
            .solutions
            .len(),
        2
    );
}

fn content_job(
    root: &std::path::Path,
    id: CommandId,
    request: &ProviderRequest,
) -> forge_studio_frontend::assistant_job_native::StoredJob {
    use forge_project::assistant::jobs::{JobJournal, JobLimits, TokenPrices};
    forge_studio_frontend::assistant_job_native::StoredJob::create(
        root,
        JobJournal::new(
            id,
            request,
            JobLimits {
                maximum_attempts: 1,
                maximum_input_tokens: 100_000,
                maximum_output_tokens: u64::from(request.maximum_output_tokens()),
                maximum_micro_usd: 0,
                prices: TokenPrices {
                    input_per_million: 0,
                    output_per_million: 0,
                },
            },
        )
        .unwrap(),
    )
    .unwrap()
}
