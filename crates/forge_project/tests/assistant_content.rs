use forge_core::{CommandId, EntityId, ModuleId, Vec3};
use forge_gameplay::{
    ActionObservation, GameAction, GameRequest, GameSession, GameplayDocument, ObjectPhysics,
    QuestStatus,
};
use forge_project::{
    assistant::{
        self,
        content::ContentAddition,
        planning::{PlanProposal, PlannedOperation},
        provider::{ApiKind, ProviderRequest, proposal_schema},
        selection::{Region, Selection, SelectionScope},
    },
    audit::AuditStamp,
    authoring::new_terrain,
    zone_authoring::session::{ZoneAction, ZoneEditSession, ZoneRequest},
};
use serde_json::json;

fn id(key: &str) -> EntityId {
    EntityId::deterministic(format!("assistant-content/{key}"))
}
fn region() -> Region {
    Region {
        minimum: Vec3::new(-10.0, -10.0, -10.0),
        maximum: Vec3::new(10.0, 10.0, 10.0),
    }
}
fn session() -> ZoneEditSession {
    let mut session = ZoneEditSession::from_initial_terrain(
        new_terrain("content", "zone", "Existing map").unwrap(),
    )
    .unwrap();
    let document: GameplayDocument = serde_json::from_value(json!({
        "schema":"forge.gameplay","version":1,"module_id":ModuleId::deterministic("content/module"),"area_id":session.head().base.terrain.area_id,"player":id("player"),
        "profile":{"id":"existing-rules","version":1,"interaction_range_m":3.0,"attack_range_m":2.0,"attack_damage":20,"attack_stamina_cost":5,"max_stamina":100,"max_action_receipts":512,"max_events_per_tick":128},
        "actors":[{"id":id("player"),"name":"PRIVATE_PLAYER_NAME","role":"player","position":{"x":0,"y":0,"z":0},"max_vitality":100,"protection":0,"inventory_capacity":20,"dialogue":null,"patrol":[],"flees_danger":false}],
        "items":[],"objects":[{"id":id("human-door"),"name":"Human door untouched","position":{"x":30,"y":0,"z":0},"kind":{"kind":"door","initially_open":true},"physics":ObjectPhysics::default()}],
        "places":[],"dialogues":[{"id":id("human-dialogue"),"start":id("human-node"),"nodes":[{"id":id("human-node"),"text":"PRIVATE_HUMAN_LINE","choices":[]}]}],"quests":[]
    })).unwrap();
    let document = document
        .with_creator_configuration(document.creator_configuration())
        .unwrap();
    let request = session
        .head()
        .prepare_gameplay(Some(document), CommandId::deterministic("content/setup"))
        .unwrap();
    session.execute(&request).unwrap();
    session
}
fn scope(session: &ZoneEditSession, create_content: bool) -> SelectionScope {
    SelectionScope::new(
        session.head(),
        &Selection {
            create_content,
            zone_properties: !create_content,
            ..Selection::default()
        },
        region(),
    )
    .unwrap()
}
fn addition() -> ContentAddition {
    let actor = |key: &str, x: f32, dialogue: &str| json!({"id":id(key),"name":key,"role":"friendly","position":{"x":x,"y":0,"z":0},"max_vitality":100,"protection":0,"inventory_capacity":10,"dialogue":id(dialogue),"patrol":[],"flees_danger":false});
    let active = json!({"kind":"quest","quest":id("quest"),"status":"active"});
    let resolve = |key: &str| json!({"id":id(&format!("choice-{key}")),"text":key,"condition":active,"effects":[{"kind":"complete_quest_solution","quest":id("quest"),"solution":id(key)},{"kind":"set_door","object":id("door"),"open":true}],"next":null});
    serde_json::from_value(json!({
        "actors":[actor("giver",1.0,"dialogue"),actor("witness",2.0,"testimony")],
        "items":[{"id":id("proof"),"name":"Forest proof","max_stack":5},{"id":id("reward"),"name":"Reward","max_stack":5}],
        "objects":[{"id":id("door"),"name":"Interior door","position":{"x":4,"y":0,"z":0},"kind":{"kind":"door","initially_open":false},"physics":ObjectPhysics::default()},
            {"id":id("pickup"),"name":"Proof in forest","position":{"x":2,"y":0,"z":0},"kind":{"kind":"pickup","item":id("proof"),"quantity":1},"physics":ObjectPhysics::default()}],
        "places":[{"id":id("village"),"name":"Village","position":{"x":0,"y":0,"z":0},"links":[id("forest")]},{"id":id("forest"),"name":"Forest","position":{"x":2,"y":0,"z":0},"links":[id("village"),id("interior")]},{"id":id("interior"),"name":"Interior","position":{"x":4,"y":0,"z":0},"links":[id("forest")]}],
        "dialogues":[{"id":id("dialogue"),"start":id("node"),"nodes":[{"id":id("node"),"text":"Bring evidence or obtain testimony.","choices":[
            {"id":id("accept"),"text":"Accept","condition":{"kind":"quest","quest":id("quest"),"status":"available"},"effects":[{"kind":"accept_quest","quest":id("quest")}],"next":null},resolve("bring-proof"),resolve("reason")]}]},
            {"id":id("testimony"),"start":id("witness-node"),"nodes":[{"id":id("witness-node"),"text":"I witnessed the incident.","choices":[{"id":id("learn"),"text":"Listen","condition":{"kind":"always"},"effects":[{"kind":"set_variable","variable":id("knows"),"value":{"type":"boolean","value":true}}],"next":null}]}]}],
        "quests":[{"id":id("quest"),"name":"Open the refuge","giver":id("giver"),"required_item":id("proof"),"required_quantity":1,"reward_item":id("reward"),"reward_quantity":1}],
        "narrative":{"version":1,"variables":[{"id":id("knows"),"name":"Testimony obtained","initial":{"type":"boolean","value":false}}],"quests":[{"quest":id("quest"),"prerequisites":[],"solutions":[
            {"id":id("bring-proof"),"name":"Collect evidence","condition":{"kind":"has_item","item":id("proof"),"quantity":1},"consume":[{"item":id("proof"),"quantity":1}]},
            {"id":id("reason"),"name":"Use testimony","condition":{"kind":"variable","variable":id("knows"),"comparison":"equal","value":{"type":"boolean","value":true}},"consume":[]}],"failure_condition":null,"journal_available":"Ask the villager.","journal_active":"Collect proof or testimony.","journal_completed":"Refuge opened.","journal_failed":"Refuge remains closed."}]}
    })).unwrap()
}
fn plan(scope: &SelectionScope, content: ContentAddition) -> PlanProposal {
    PlanProposal {
        context_hash: scope.context().content_hash().unwrap(),
        summary: "Synthetic content proposal".into(),
        missing_capabilities: vec![],
        operations: vec![PlannedOperation::AddContent {
            content: Box::new(content),
        }],
    }
}
fn apply_content(session: &mut ZoneEditSession) {
    let scope = scope(session, true);
    let request = plan(&scope, addition())
        .compile(
            session.head(),
            &scope,
            CommandId::deterministic("content/add"),
        )
        .unwrap();
    let review = assistant::prepare(
        session,
        &request,
        scope.policy(),
        AuditStamp::ai("synthetic-content-test", "2026-10-22T19:00:00Z").unwrap(),
    )
    .unwrap();
    assert!(
        review
            .changes()
            .iter()
            .all(|change| change.path.starts_with("/corrections/gameplay/"))
    );
    let approval = review.approve(review.content_hash().unwrap()).unwrap();
    assistant::apply(session, scope.policy(), &review, &approval).unwrap();
}

#[test]
fn keyed_places_compile_stable_linked_ids_and_preserve_existing_content() {
    let session = session();
    let denied = scope(&session, false);
    let scope = scope(&session, true);
    let command = CommandId::deterministic("keyed-places");
    let make_plan = |places| PlanProposal {
        context_hash: scope.context().content_hash().unwrap(),
        summary: "Linked places".into(),
        missing_capabilities: vec![],
        operations: vec![PlannedOperation::CreatePlaces { places }],
    };
    let places = serde_json::from_value(json!([
        {"key":"first","name":"First place","position":{"x":0,"y":0,"z":0},"links":["second"]},
        {"key":"second","name":"Second place","position":{"x":2,"y":0,"z":0},"links":["first"]}
    ]))
    .unwrap();
    let plan = make_plan(places);
    let request = plan.compile(session.head(), &scope, command).unwrap();
    assert_eq!(
        request,
        plan.compile(session.head(), &scope, command).unwrap()
    );
    let ZoneAction::Edit(corrections) = request.action else {
        panic!("expected edit")
    };
    let current = corrections.gameplay.as_deref().unwrap();
    let existing = session.head().corrections.gameplay.as_deref().unwrap();
    assert_eq!(current.actors, existing.actors);
    assert_eq!(current.objects, existing.objects);
    assert_eq!(current.dialogues, existing.dialogues);
    assert_eq!(current.profile, existing.profile);
    assert_eq!(current.creator, existing.creator);
    assert_eq!(current.places.len(), 2);
    assert_eq!(current.places[0].links, vec![current.places[1].id]);
    assert_eq!(current.places[1].links, vec![current.places[0].id]);
    assert_ne!(current.places[0].id, current.places[1].id);
    let schema = proposal_schema(scope.context()).unwrap();
    assert!(schema.to_string().contains("create_places"));
    let PlannedOperation::CreatePlaces { places } = &plan.operations[0] else {
        unreachable!()
    };
    for invalid in [
        vec![],
        vec![places[0].clone(); 33],
        vec![places[0].clone(), places[0].clone()],
        vec![places[0].clone()], // unresolved link to another proposal
    ] {
        assert!(
            make_plan(invalid)
                .compile(session.head(), &scope, command)
                .is_err()
        );
    }
    for mutate in 0..4 {
        let mut invalid = places.clone();
        match mutate {
            0 => invalid[0].key = "../path".into(),
            1 => invalid[0].links.push("second".into()),
            2 => invalid[0].position.x = 11.0,
            _ => invalid[0].links = vec![id("human-door").to_string()],
        }
        assert!(
            make_plan(invalid)
                .compile(session.head(), &scope, command)
                .is_err()
        );
    }
    assert!(
        !proposal_schema(denied.context())
            .unwrap()
            .to_string()
            .contains("create_places")
    );
}

#[test]
fn additive_adventure_is_one_reviewed_transaction_and_preserves_existing_content_after_reopen_and_undo()
 {
    let mut session = session();
    let before = session.head().corrections.clone();
    let original = before.gameplay.as_ref().unwrap();
    apply_content(&mut session);
    let restored = ZoneEditSession::from_archive_bytes(&session.archive_bytes().unwrap()).unwrap();
    let document = restored.head().corrections.gameplay.as_ref().unwrap();
    assert_eq!(document.player, original.player);
    assert_eq!(document.profile, original.profile);
    assert_eq!(document.creator, original.creator);
    assert_eq!(
        document.actor(original.player),
        original.actor(original.player)
    );
    assert_eq!(
        document.object(id("human-door")),
        original.object(id("human-door"))
    );
    assert_eq!(
        document.dialogue(id("human-dialogue")),
        original.dialogue(id("human-dialogue"))
    );
    assert_eq!(document.places.len(), 3);
    assert_eq!(
        document.narrative.as_ref().unwrap().quests[0]
            .solutions
            .len(),
        2
    );
    let request = ZoneRequest {
        command_id: CommandId::deterministic("content/undo"),
        expected_revision: session.head().revision,
        expected_hash: session.head().content_hash().unwrap(),
        action: ZoneAction::Undo,
    };
    session.execute(&request).unwrap();
    assert_eq!(session.head().corrections, before);
}

fn act(game: &mut GameSession, key: &str, action: GameAction) {
    game.execute(
        &GameRequest {
            command_id: CommandId::deterministic(format!("content/play/{key}")),
            expected_sequence: game.state().sequence,
            actor: id("player"),
            action,
        },
        ActionObservation {
            line_of_sight: true,
            ..Default::default()
        },
    )
    .unwrap();
}
#[test]
fn both_authored_solutions_are_playable_and_the_consequence_survives_a_save_without_a_provider() {
    let mut session = session();
    apply_content(&mut session);
    let document = session
        .head()
        .corrections
        .gameplay
        .as_deref()
        .unwrap()
        .clone();
    let hash = session.head().content_hash().unwrap();
    for solution in ["bring-proof", "reason"] {
        let mut game = GameSession::new(document.clone(), hash).unwrap();
        act(
            &mut game,
            "accept",
            GameAction::ChooseDialogue {
                speaker: id("giver"),
                node: id("node"),
                choice: id("accept"),
            },
        );
        if solution == "bring-proof" {
            act(
                &mut game,
                "pickup",
                GameAction::Pickup {
                    object: id("pickup"),
                },
            );
        } else {
            act(
                &mut game,
                "learn",
                GameAction::ChooseDialogue {
                    speaker: id("witness"),
                    node: id("witness-node"),
                    choice: id("learn"),
                },
            );
        }
        act(
            &mut game,
            "resolve",
            GameAction::ChooseDialogue {
                speaker: id("giver"),
                node: id("node"),
                choice: id(&format!("choice-{solution}")),
            },
        );
        let saved = game.save_bytes().unwrap();
        let resumed = GameSession::resume(document.clone(), hash, &saved, false).unwrap();
        assert_eq!(resumed.state().quests[&id("quest")], QuestStatus::Completed);
        assert!(resumed.state().objects[&id("door")].open);
        assert_eq!(resumed.quest_journal()[0].solution, Some(id(solution)));
        assert_eq!(
            resumed.state().actors[&id("player")].inventory[&id("reward")],
            1
        );
        assert_eq!(resumed.state(), game.state());
    }
}

#[test]
fn creation_requires_its_own_scope_and_discloses_rules_without_unselected_content() {
    let session = session();
    let denied = scope(&session, false);
    assert!(
        plan(&denied, addition())
            .compile(session.head(), &denied, CommandId::deterministic("denied"))
            .is_err()
    );
    let allowed = scope(&session, true);
    let context = serde_json::to_string(allowed.context()).unwrap();
    assert!(!context.contains("PRIVATE_PLAYER_NAME"));
    assert!(!context.contains("PRIVATE_HUMAN_LINE"));
    assert!(!context.contains("Human door untouched"));
    assert!(context.contains("existing-rules"));
    let request = |scope: &SelectionScope| {
        ProviderRequest::new(
            ApiKind::ChatCompletions,
            "http://127.0.0.1:8080/v1/chat/completions",
            "fixture-model",
            "Create content",
            scope.context(),
            4096,
        )
        .unwrap()
    };
    assert!(
        request(&allowed)
            .approve_outbound(request(&denied).content_hash().unwrap())
            .is_err()
    );
    assert!(
        proposal_schema(allowed.context())
            .unwrap()
            .get("$defs")
            .is_some()
    );
    assert!(
        proposal_schema(denied.context())
            .unwrap()
            .get("$defs")
            .is_none()
    );
}

#[test]
fn collisions_hidden_references_outside_positions_and_player_replacement_fail_before_mutation() {
    let session = session();
    let scope = scope(&session, true);
    let mut cases = Vec::new();
    let mut value = addition();
    value.actors[0].id = id("player");
    cases.push(value);
    let mut value = addition();
    value.actors[0].role = forge_gameplay::ActorRole::Player;
    cases.push(value);
    let mut value = addition();
    value.actors[0].position.x = 11.0;
    cases.push(value);
    let mut value = addition();
    value.actors[0].patrol = vec![Vec3::new(11.0, 0.0, 0.0)];
    cases.push(value);
    let mut value = addition();
    value.objects[0].physics.supports.push(id("human-door"));
    cases.push(value);
    let mut value = addition();
    value.objects[0].id = id("human-door");
    cases.push(value);
    let mut value = addition();
    value.dialogues[0].nodes[0].choices[1]
        .effects
        .push(forge_gameplay::DialogueEffect::SetDoor {
            object: id("human-door"),
            open: false,
        });
    cases.push(value);
    let mut value = addition();
    value.quests[0].giver = id("player");
    cases.push(value);
    let mut value = addition();
    value.places[0].position.z = 11.0;
    cases.push(value);
    let before = session.archive_bytes().unwrap();
    for content in cases {
        assert!(
            plan(&scope, content)
                .compile(
                    session.head(),
                    &scope,
                    CommandId::deterministic("content/refused")
                )
                .is_err()
        );
        assert_eq!(session.archive_bytes().unwrap(), before);
    }
}

#[test]
fn content_cannot_enable_hidden_surfaces_or_add_scripts_profiles_and_module_settings() {
    let mut session = session();
    let mut document = session
        .head()
        .corrections
        .gameplay
        .as_deref()
        .unwrap()
        .clone();
    document
        .creator
        .as_mut()
        .unwrap()
        .surfaces
        .retain(|surface| *surface != forge_gameplay::CreatorSurface::Quests);
    let request = session
        .head()
        .prepare_gameplay(
            Some(document),
            CommandId::deterministic("content/hide-quests"),
        )
        .unwrap();
    session.execute(&request).unwrap();
    let scope = scope(&session, true);
    assert!(
        plan(&scope, addition())
            .compile(
                session.head(),
                &scope,
                CommandId::deterministic("content/hidden")
            )
            .is_err()
    );
    for field in [
        "profile",
        "player",
        "module_id",
        "scripting",
        "creator",
        "root",
    ] {
        let mut value = serde_json::to_value(addition()).unwrap();
        value[field] = json!("forbidden");
        assert!(serde_json::from_value::<ContentAddition>(value).is_err());
    }
}

fn quest_draft() -> forge_project::assistant::content::quest::FetchQuestDraft {
    serde_json::from_value(json!({"key":"refuge","name":"Ouvrir le refuge",
        "giver":{"name":"Gardien","position":{"x":1,"y":0,"z":0},"greeting":"Apportez une preuve ou un temoignage."},
        "accept_text":"Je vais enqueter.","collection":{"name":"Recueillir une preuve","choice_text":"Voici la preuve.","item_name":"Preuve","pickup_name":"Preuve dans la foret","position":{"x":2,"y":0,"z":0},"quantity":1},
        "alternatives":[{"approach":"testimony","key":"witness","name":"Ecouter le temoin","choice_text":"Le temoin confirme.","witness":{"name":"Temoin","position":{"x":2,"y":0,"z":0},"greeting":"Je confirme les faits."},"learn_text":"Ecouter"}],
        "reward_name":"Insigne","reward_quantity":1,
        "journal":{"available":"Parler au gardien.","active":"Chercher preuve ou temoignage.","completed":"Refuge ouvert.","failed":"Refuge ferme."},
        "door":{"name":"Porte du refuge","position":{"x":4,"y":0,"z":0},"initially_open":false,"open_after_completion":true}
    })).unwrap()
}

#[test]
fn concise_quest_command_expands_into_editable_content_with_deterministic_identities_and_two_playable_solutions()
 {
    let mut session = session();
    let existing = session.head().corrections.gameplay.clone().unwrap();
    let scope = scope(&session, true);
    let command = CommandId::deterministic("concise-quest");
    let draft = quest_draft();
    let content = draft.expand(command).unwrap();
    assert_eq!(content, draft.expand(command).unwrap());
    assert_ne!(
        content,
        draft
            .expand(CommandId::deterministic("other-quest"))
            .unwrap()
    );
    let compact = serde_json::to_vec(&draft).unwrap().len();
    let expanded = serde_json::to_vec(&content).unwrap().len();
    assert!(compact < expanded);
    let plan = PlanProposal {
        context_hash: scope.context().content_hash().unwrap(),
        summary: "Concise authored quest".into(),
        missing_capabilities: vec![],
        operations: vec![PlannedOperation::CreateFetchQuest {
            quest: Box::new(draft),
        }],
    };
    let request = plan.compile(session.head(), &scope, command).unwrap();
    let review = assistant::prepare(
        &session,
        &request,
        scope.policy(),
        AuditStamp::ai("synthetic-quest-test", "2026-10-22T20:00:00Z").unwrap(),
    )
    .unwrap();
    let approval = review.approve(review.content_hash().unwrap()).unwrap();
    assistant::apply(&mut session, scope.policy(), &review, &approval).unwrap();
    let restored = ZoneEditSession::from_archive_bytes(&session.archive_bytes().unwrap()).unwrap();
    let document = restored.head().corrections.gameplay.as_deref().unwrap();
    assert_eq!(document.actor(id("player")), existing.actor(id("player")));
    assert_eq!(document.profile, existing.profile);
    assert_eq!(document.creator, existing.creator);
    play_saved_solutions(document, restored.head().content_hash().unwrap());
    println!(
        "{}",
        json!({"concise_bytes":compact,"expanded_bytes":expanded})
    );
}

#[test]
fn concise_quest_still_refuses_ungranted_regions_duplicate_keys_and_invalid_quantities() {
    let session = session();
    let scope = scope(&session, true);
    let command = CommandId::deterministic("refused-quest");
    let mut outside = quest_draft();
    outside.giver.position.x = 11.0;
    assert!(
        outside
            .expand(command)
            .unwrap()
            .compile(session.head(), &scope, command)
            .is_err()
    );
    let mut duplicate = quest_draft();
    duplicate
        .alternatives
        .push(duplicate.alternatives[0].clone());
    assert!(duplicate.expand(command).is_err());
    let mut excessive = quest_draft();
    excessive.reward_quantity = 10_001;
    assert!(excessive.expand(command).is_err());
    let mut invalid = quest_draft();
    invalid.key = "../untrusted".into();
    assert!(invalid.expand(command).is_err());
    let denied = SelectionScope::new(
        session.head(),
        &Selection {
            zone_properties: true,
            ..Selection::default()
        },
        region(),
    )
    .unwrap();
    assert!(
        quest_draft()
            .expand(command)
            .unwrap()
            .compile(session.head(), &denied, command)
            .is_err()
    );
}

#[test]
fn concise_combat_alternative_uses_existing_damage_rules_and_persists_its_solution() {
    use forge_project::assistant::content::quest::QuestAlternative;
    let mut session = session();
    let scope = scope(&session, true);
    let command = CommandId::deterministic("combat-quest");
    let mut draft = quest_draft();
    draft.alternatives = vec![QuestAlternative::Defeat {
        key: "threat".into(),
        name: "Ecarter la menace".into(),
        choice_text: "La menace est ecartee.".into(),
        enemy_name: "Adversaire".into(),
        position: Vec3::new(1.5, 0.0, 0.0),
        vitality: 20,
        protection: 0,
    }];
    let request = draft
        .expand(command)
        .unwrap()
        .compile(session.head(), &scope, command)
        .unwrap();
    session.execute(&request).unwrap();
    play_saved_solutions(
        session.head().corrections.gameplay.as_deref().unwrap(),
        session.head().content_hash().unwrap(),
    );
}

fn real_action(
    game: &mut GameSession,
    document: &GameplayDocument,
    action: GameAction,
) -> forge_gameplay::GameReceipt {
    game.execute(
        &GameRequest {
            command_id: CommandId::deterministic(format!(
                "saved-real-content/{}",
                game.state().sequence
            )),
            expected_sequence: game.state().sequence,
            actor: document.player,
            action,
        },
        ActionObservation {
            line_of_sight: true,
            ..Default::default()
        },
    )
    .unwrap()
}

fn real_choice(
    document: &GameplayDocument,
    speaker: EntityId,
    predicate: impl Fn(&forge_gameplay::DialogueEffect) -> bool,
) -> GameAction {
    let dialogue = document
        .dialogue(document.actor(speaker).unwrap().dialogue.unwrap())
        .unwrap();
    let node = dialogue
        .nodes
        .iter()
        .find(|node| node.id == dialogue.start)
        .unwrap();
    let choice = node
        .choices
        .iter()
        .find(|choice| choice.effects.iter().any(&predicate))
        .unwrap();
    GameAction::ChooseDialogue {
        speaker,
        node: node.id,
        choice: choice.id,
    }
}

fn has_item_condition(condition: &forge_gameplay::Condition) -> bool {
    match condition {
        forge_gameplay::Condition::HasItem { .. } => true,
        forge_gameplay::Condition::All { conditions }
        | forge_gameplay::Condition::Any { conditions } => {
            conditions.iter().any(has_item_condition)
        }
        _ => false,
    }
}

/// Observations are synthetic; native rendering, navigation and UI remain separate.
#[test]
#[ignore = "Reads the saved real-provider archive from an explicitly supplied qualification root"]
fn saved_real_provider_story_plays_each_solution_and_retains_consequence() {
    let root = std::path::PathBuf::from(
        std::env::var("OPENRPG_GATE6_TEST_ROOT").expect("saved provider evidence root"),
    );
    let session = ZoneEditSession::from_archive_bytes(
        &std::fs::read(root.join("source.archive.json")).unwrap(),
    )
    .unwrap();
    let document = session.head().corrections.gameplay.as_deref().unwrap();
    let hash = session.head().content_hash().unwrap();
    let branches = play_saved_solutions(document, hash);
    println!(
        "{}",
        json!({"evidence_root":root,"source_hash":hash,"branches":branches,"provider_called":false,"observations":"synthetic line of sight; not native game or UI qualification"})
    );
}

fn play_saved_solutions(
    document: &GameplayDocument,
    hash: forge_core::ContentHash,
) -> Vec<serde_json::Value> {
    use forge_gameplay::{DialogueEffect, ObjectKind};
    assert_eq!(document.quests.len(), 1);
    let quest = &document.quests[0];
    let flow = &document.narrative.as_ref().unwrap().quests[0];
    assert_eq!(flow.solutions.len(), 2);
    let mut branches = Vec::new();
    for solution in &flow.solutions {
        let mut game = GameSession::new(document.clone(), hash).unwrap();
        let accept = real_choice(
            document,
            quest.giver,
            |effect| matches!(effect, DialogueEffect::AcceptQuest{quest:id} if *id==quest.id),
        );
        real_action(&mut game, document, accept);
        if has_item_condition(&solution.condition) {
            let pickup = document.objects.iter().find(|object|matches!(&object.kind,ObjectKind::Pickup{item,..} if *item==quest.required_item)).unwrap();
            real_action(
                &mut game,
                document,
                GameAction::Pickup { object: pickup.id },
            );
        } else if let forge_gameplay::Condition::ActorDefeated { actor: target } =
            solution.condition
        {
            game.execute(
                &GameRequest {
                    command_id: CommandId::deterministic("saved-real-content/confirmed-hit"),
                    expected_sequence: game.state().sequence,
                    actor: document.player,
                    action: GameAction::Attack { target },
                },
                ActionObservation {
                    line_of_sight: true,
                    confirmed_contact: true,
                    ..Default::default()
                },
            )
            .unwrap();
        } else {
            let witness = document
                .actors
                .iter()
                .find(|actor| actor.id != quest.giver && actor.dialogue.is_some())
                .unwrap();
            let testimony = real_choice(document, witness.id, |effect| {
                matches!(effect, DialogueEffect::SetVariable { .. })
            });
            real_action(&mut game, document, testimony);
        }
        assert!(game.condition(document.player, &solution.condition));
        let complete = real_choice(
            document,
            quest.giver,
            |effect| matches!(effect,DialogueEffect::CompleteQuestSolution{quest:id,solution:selected} if *id==quest.id && *selected==solution.id),
        );
        let receipt = real_action(&mut game, document, complete);
        let saved = game.save_bytes().unwrap();
        let mut resumed = GameSession::resume(document.clone(), hash, &saved, false).unwrap();
        assert_eq!(resumed.state(), game.state());
        assert_eq!(resumed.state().quests[&quest.id], QuestStatus::Completed);
        assert_eq!(resumed.quest_journal()[0].solution, Some(solution.id));
        assert_eq!(
            resumed.state().actors[&document.player].inventory[&quest.reward_item.unwrap()],
            quest.reward_quantity
        );
        let doors: Vec<_> = document
            .objects
            .iter()
            .filter(|object| matches!(object.kind, ObjectKind::Door { .. }))
            .collect();
        assert!(!doors.is_empty());
        assert!(
            doors
                .iter()
                .all(|door| resumed.state().objects[&door.id].open)
        );
        assert_eq!(
            resumed
                .execute(&receipt.request, ActionObservation::default())
                .unwrap(),
            receipt
        );
        assert_eq!(resumed.state(), game.state());
        branches.push(json!({"solution":solution.id,"name":solution.name,"steps":game.state().sequence,"saved_bytes":saved.len(),"consequence":"door open; one reward; replay identical"}));
    }
    branches
}
