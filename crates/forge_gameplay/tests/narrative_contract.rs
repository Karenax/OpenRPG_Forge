use forge_core::{AreaId, CommandId, ContentHash, EntityId, ModuleId, canonical_json_sha256};
use forge_gameplay::*;
use serde_json::json;

fn id(name: &str) -> EntityId {
    EntityId::deterministic(format!("narrative/{name}"))
}

fn fixture() -> GameplayDocument {
    let mut document: GameplayDocument = serde_json::from_value(json!({
        "schema":"forge.gameplay","version":1,"module_id":ModuleId::deterministic("story"),
        "area_id":AreaId::deterministic("story"),
        "profile":{"id":"story.minimal","version":1,"interaction_range_m":3.0,"attack_range_m":2.0,
            "attack_damage":20,"attack_stamina_cost":5,"max_stamina":100,
            "max_action_receipts":512,"max_events_per_tick":128},
        "player":id("player"),"actors":[
            {"id":id("player"),"name":"Player","role":"player","position":{"x":0,"y":0,"z":0},
             "max_vitality":100,"protection":0,"inventory_capacity":10,"dialogue":null,"patrol":[],"flees_danger":false},
            {"id":id("giver"),"name":"Witness","role":"friendly","position":{"x":1,"y":0,"z":0},
             "max_vitality":100,"protection":0,"inventory_capacity":10,"dialogue":id("dialogue"),"patrol":[],"flees_danger":false}],
        "items":[{"id":id("proof"),"name":"Proof","max_stack":10},{"id":id("reward"),"name":"Reward","max_stack":10}],
        "objects":[],"places":[],
        "dialogues":[{"id":id("dialogue"),"start":id("node"),"nodes":[{"id":id("node"),"text":"Resolve the inquiry.","choices":[]}]}],
        "quests":[{"id":id("quest"),"name":"Inquiry","giver":id("giver"),"required_item":id("proof"),
            "required_quantity":1,"reward_item":id("reward"),"reward_quantity":1}]
    })).unwrap();
    document.dialogues[0].nodes[0].choices = fixture_choices();
    document.narrative = Some(fixture_narrative());
    let mut creator = document.creator_configuration();
    creator.required_capabilities = vec![
        GameplayCapability::Actors,
        GameplayCapability::Inventory,
        GameplayCapability::Dialogue,
        GameplayCapability::Quests,
    ];
    creator.surfaces = vec![
        CreatorSurface::Actors,
        CreatorSurface::Dialogues,
        CreatorSurface::Quests,
    ];
    document.with_creator_configuration(creator).unwrap()
}

fn fixture_choices() -> Vec<DialogueChoice> {
    vec![
        choice(
            "accept",
            Condition::Quest {
                quest: id("quest"),
                status: QuestStatus::Available,
            },
            vec![DialogueEffect::AcceptQuest { quest: id("quest") }],
        ),
        choice(
            "learn",
            Condition::Always,
            vec![DialogueEffect::SetVariable {
                variable: id("knows"),
                value: NarrativeValue::Boolean(true),
            }],
        ),
        choice(
            "reason",
            Condition::Quest {
                quest: id("quest"),
                status: QuestStatus::Active,
            },
            vec![DialogueEffect::CompleteQuestSolution {
                quest: id("quest"),
                solution: id("reason"),
            }],
        ),
        choice(
            "mercy",
            Condition::Quest {
                quest: id("quest"),
                status: QuestStatus::Active,
            },
            vec![DialogueEffect::CompleteQuestSolution {
                quest: id("quest"),
                solution: id("mercy"),
            }],
        ),
        choice(
            "fail",
            Condition::Quest {
                quest: id("quest"),
                status: QuestStatus::Active,
            },
            vec![DialogueEffect::FailQuest { quest: id("quest") }],
        ),
    ]
}

fn fixture_narrative() -> NarrativeDefinition {
    NarrativeDefinition {
        version: 1,
        variables: vec![NarrativeVariable {
            id: id("knows"),
            name: "Knows the truth".into(),
            initial: NarrativeValue::Boolean(false),
        }],
        quests: vec![QuestFlow {
            quest: id("quest"),
            prerequisites: vec![],
            solutions: vec![
                QuestSolution {
                    id: id("reason"),
                    name: "Reason from testimony".into(),
                    condition: Condition::Variable {
                        variable: id("knows"),
                        comparison: NarrativeComparison::Equal,
                        value: NarrativeValue::Boolean(true),
                    },
                    consume: vec![],
                },
                QuestSolution {
                    id: id("mercy"),
                    name: "Grant mercy".into(),
                    condition: Condition::Always,
                    consume: vec![],
                },
            ],
            failure_condition: Some(Condition::Variable {
                variable: id("knows"),
                comparison: NarrativeComparison::Equal,
                value: NarrativeValue::Boolean(false),
            }),
            journal_available: "Hear the witness.".into(),
            journal_active: "Choose how to resolve the inquiry.".into(),
            journal_completed: "Inquiry resolved.".into(),
            journal_failed: "Inquiry abandoned.".into(),
        }],
    }
}

fn choice(key: &str, condition: Condition, effects: Vec<DialogueEffect>) -> DialogueChoice {
    DialogueChoice {
        id: id(&format!("choice/{key}")),
        text: key.into(),
        condition,
        effects,
        next: None,
    }
}

fn choose(game: &mut GameSession, key: &str, command: &str) -> Result<GameReceipt, GameplayError> {
    game.execute(
        &GameRequest {
            command_id: CommandId::deterministic(command),
            expected_sequence: game.state().sequence,
            actor: id("player"),
            action: GameAction::ChooseDialogue {
                speaker: id("giver"),
                node: id("node"),
                choice: id(&format!("choice/{key}")),
            },
        },
        ActionObservation {
            line_of_sight: true,
            ..Default::default()
        },
    )
}

fn session(document: GameplayDocument) -> GameSession {
    GameSession::new(document, ContentHash::from_bytes([30; 32])).unwrap()
}

#[test]
fn two_solutions_have_one_reward_and_a_persistent_journal_after_resume() {
    for solution in ["reason", "mercy"] {
        let document = fixture();
        let mut game = session(document.clone());
        choose(&mut game, "accept", "accept").unwrap();
        if solution == "reason" {
            choose(&mut game, "learn", "learn").unwrap();
        }
        let receipt = choose(&mut game, solution, "resolve").unwrap();
        let saved = game.save_bytes().unwrap();
        let mut resumed = GameSession::resume(document, game.source_hash(), &saved, false).unwrap();
        assert_eq!(resumed.quest_journal()[0].solution, Some(id(solution)));
        assert_eq!(resumed.quest_journal()[0].text, "Inquiry resolved.");
        assert_eq!(
            resumed.state().actors[&id("player")].inventory[&id("reward")],
            1
        );
        assert_eq!(
            resumed
                .execute(&receipt.request, ActionObservation::default())
                .unwrap(),
            receipt
        );
        assert!(choose(&mut resumed, solution, "duplicate-reward").is_err());
        assert_eq!(saved, resumed.save_bytes().unwrap());
    }
}

#[test]
fn failed_conditions_and_reward_overflow_preserve_all_effects_and_cursor() {
    let mut document = fixture();
    let mut game = session(document.clone());
    choose(&mut game, "accept", "accept").unwrap();
    let before = game.save_bytes().unwrap();
    assert!(choose(&mut game, "reason", "unknown-truth").is_err());
    assert_eq!(before, game.save_bytes().unwrap());
    document.actors[0].inventory_capacity = 0;
    document.dialogues[0].nodes[0].choices[3].effects.insert(
        0,
        DialogueEffect::SetVariable {
            variable: id("knows"),
            value: NarrativeValue::Boolean(true),
        },
    );
    let mut game = session(document);
    choose(&mut game, "accept", "accept").unwrap();
    let before = game.save_bytes().unwrap();
    assert!(choose(&mut game, "mercy", "overflow").is_err());
    assert_eq!(before, game.save_bytes().unwrap());
}

#[test]
fn failure_is_terminal_and_has_no_reward() {
    let mut game = session(fixture());
    choose(&mut game, "accept", "accept").unwrap();
    choose(&mut game, "fail", "fail").unwrap();
    assert_eq!(game.quest_journal()[0].status, QuestStatus::Failed);
    assert!(game.state().actors[&id("player")].inventory.is_empty());
    assert!(choose(&mut game, "mercy", "failed-quest-reward").is_err());
    assert!(game.save_bytes().is_ok());
}

#[test]
fn forged_variables_or_solution_are_refused_even_after_rehashing() {
    let document = fixture();
    let game = session(document.clone());
    let mut saved: serde_json::Value = serde_json::from_slice(&game.save_bytes().unwrap()).unwrap();
    saved["state"]["narrative"]["variables"][id("knows").to_string()] =
        serde_json::to_value(NarrativeValue::Boolean(true)).unwrap();
    saved["state_hash"] = json!(canonical_json_sha256(&saved["state"]).unwrap());
    assert!(
        GameSession::resume(
            document,
            game.source_hash(),
            &serde_json::to_vec(&saved).unwrap(),
            false
        )
        .is_err()
    );
}

#[test]
fn variable_types_references_and_condition_recursion_are_validated() {
    let document = fixture();
    let mut wrong = document.clone();
    wrong.dialogues[0].nodes[0].choices[1].effects = vec![DialogueEffect::SetVariable {
        variable: id("knows"),
        value: NarrativeValue::Integer(1),
    }];
    assert!(wrong.validate().is_err());
    let mut wrong = document.clone();
    wrong.dialogues[0].nodes[0].choices[1].condition = Condition::Variable {
        variable: id("missing"),
        comparison: NarrativeComparison::Equal,
        value: NarrativeValue::Boolean(true),
    };
    assert!(wrong.validate().is_err());
    let mut nested = Condition::Always;
    for _ in 0..10 {
        nested = Condition::All {
            conditions: vec![nested],
        };
    }
    let mut wrong = document;
    wrong.dialogues[0].nodes[0].choices[1].condition = nested;
    assert!(wrong.validate().is_err());
}

#[test]
fn narrative_text_retouch_is_compatible_but_variables_and_solutions_are_functional() {
    let document = fixture();
    let baseline = document.functional_hash().unwrap();
    let mut edited = document.clone();
    edited.narrative.as_mut().unwrap().quests[0].journal_completed =
        "Inquiry resolved peacefully.".into();
    edited.narrative.as_mut().unwrap().quests[0].solutions[0].name = "Reconsider testimony".into();
    assert_eq!(baseline, edited.functional_hash().unwrap());
    edited.narrative.as_mut().unwrap().variables[0].initial = NarrativeValue::Boolean(true);
    assert_ne!(baseline, edited.functional_hash().unwrap());
}

#[test]
fn solution_costs_are_consumed_once_and_checked_by_the_save_ledger() {
    let mut document = fixture();
    document.narrative.as_mut().unwrap().quests[0].solutions[1].consume = vec![QuestConsumption {
        item: id("proof"),
        quantity: 1,
    }];
    document.objects.push(ObjectDefinition {
        id: id("pickup"),
        name: "Evidence".into(),
        position: forge_core::Vec3::ZERO,
        kind: ObjectKind::Pickup {
            item: id("proof"),
            quantity: 1,
        },
        physics: ObjectPhysics::default(),
    });
    let mut game = session(document.clone());
    choose(&mut game, "accept", "accept").unwrap();
    assert!(choose(&mut game, "mercy", "without-item").is_err());
    game.execute(
        &GameRequest {
            command_id: CommandId::deterministic("pickup"),
            expected_sequence: game.state().sequence,
            actor: id("player"),
            action: GameAction::Pickup {
                object: id("pickup"),
            },
        },
        ActionObservation {
            line_of_sight: true,
            ..Default::default()
        },
    )
    .unwrap();
    choose(&mut game, "mercy", "resolve").unwrap();
    assert!(
        !game.state().actors[&id("player")]
            .inventory
            .contains_key(&id("proof"))
    );
    let bytes = game.save_bytes().unwrap();
    assert!(GameSession::resume(document, game.source_hash(), &bytes, false).is_ok());
}

#[test]
fn prerequisites_prevent_acceptance_and_cycles_are_rejected() {
    let mut document = fixture();
    let mut dependency = document.quests[0].clone();
    dependency.id = id("dependency");
    dependency.name = "First inquiry".into();
    document.quests.push(dependency);
    document.narrative.as_mut().unwrap().quests[0].prerequisites = vec![id("dependency")];
    let mut game = session(document.clone());
    let before = game.save_bytes().unwrap();
    assert!(choose(&mut game, "accept", "too-soon").is_err());
    assert_eq!(before, game.save_bytes().unwrap());
    let mut cycle = document.narrative.as_ref().unwrap().quests[0].clone();
    cycle.quest = id("dependency");
    cycle.prerequisites = vec![id("quest")];
    cycle.solutions[0].id = id("cycle-a");
    cycle.solutions[1].id = id("cycle-b");
    document.narrative.as_mut().unwrap().quests.push(cycle);
    assert!(document.validate().is_err());
}

#[test]
fn explicit_adoption_preserves_choice_identity_text_and_links() {
    let mut document = fixture();
    let flow = document.narrative.as_mut().unwrap().quests.remove(0);
    let selected = &mut document.dialogues[0].nodes[0].choices[2];
    selected.effects = vec![DialogueEffect::CompleteQuest { quest: id("quest") }];
    let baseline = selected.clone();
    // Other solution/failure effects must be removed with their former definition.
    document.dialogues[0].nodes[0].choices.truncate(3);
    document.validate().unwrap();
    assert!(document.with_quest_flow(flow.clone()).is_err());
    let adopted = document
        .with_quest_flow_and_legacy_solution(flow, id("reason"))
        .unwrap();
    let mut expected = baseline;
    expected.effects = vec![DialogueEffect::CompleteQuestSolution {
        quest: id("quest"),
        solution: id("reason"),
    }];
    assert_eq!(adopted.dialogues[0].nodes[0].choices[2], expected);
}
