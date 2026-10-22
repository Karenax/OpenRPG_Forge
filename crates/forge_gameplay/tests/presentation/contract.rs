//! Register the target after lot E's real action/save adapter is integrated.
use forge_core::{AreaId, CommandId, ContentHash, EntityId, ModuleId};
use forge_gameplay::*;
use serde_json::json;
use std::collections::BTreeSet;

fn id(name: &str) -> EntityId {
    EntityId::deterministic(format!("presentation/{name}"))
}
fn step(name: &str, operation: ScriptOperation) -> ScriptInstruction {
    ScriptInstruction {
        id: id(name),
        operation,
    }
}

fn fixture() -> GameplayDocument {
    let source: GameplayDocument = serde_json::from_value(json!({
        "schema":"forge.gameplay","version":1,"module_id":ModuleId::deterministic("presentation"),
        "area_id":AreaId::deterministic("presentation"),"player":id("player"),
        "profile":{"id":"presentation.minimal","version":1,"interaction_range_m":3.0,"attack_range_m":2.0,
            "attack_damage":20,"attack_stamina_cost":5,"max_stamina":100,"max_action_receipts":512,"max_events_per_tick":512},
        "actors":[{"id":id("player"),"name":"Player","role":"player","position":{"x":0,"y":0,"z":0},"max_vitality":100,"protection":0,"inventory_capacity":8,"dialogue":null,"patrol":[],"flees_danger":false}],
        "items":[],"objects":[{"id":id("door"),"name":"Door","position":{"x":1,"y":0,"z":0},"kind":{"kind":"door","initially_open":false},"physics":ObjectPhysics::default()}],
        "places":[],"dialogues":[],"quests":[]
    })).unwrap();
    let source = source
        .with_narrative_definition(Some(NarrativeDefinition {
            version: 1,
            variables: vec![NarrativeVariable {
                id: id("flag"),
                name: "Flag".into(),
                initial: NarrativeValue::Boolean(false),
            }],
            quests: vec![],
        }))
        .unwrap();
    source
        .with_script_definition(ScriptDefinition {
            id: id("script"),
            name: "Open the door".into(),
            permissions: ScriptPermissions {
                read_variables: BTreeSet::from([id("flag")]),
                write_variables: BTreeSet::from([id("flag")]),
                doors: BTreeSet::from([id("door")]),
            },
            steps: vec![
                step(
                    "write",
                    ScriptOperation::SetVariable {
                        variable: id("flag"),
                        value: NarrativeValue::Boolean(true),
                    },
                ),
                step(
                    "open",
                    ScriptOperation::SetDoor {
                        object: id("door"),
                        open: true,
                    },
                ),
                step(
                    "message",
                    ScriptOperation::Message {
                        text: "The passage is open.".into(),
                    },
                ),
            ],
        })
        .unwrap()
}

fn session(source: GameplayDocument) -> GameSession {
    GameSession::new(source, ContentHash::from_bytes([91; 32])).unwrap()
}
fn act(game: &mut GameSession, action: GameAction) -> Result<GameReceipt, GameplayError> {
    game.execute(
        &GameRequest {
            command_id: CommandId::deterministic(format!("presentation/{}", game.state().sequence)),
            expected_sequence: game.state().sequence,
            actor: game.document().player,
            action,
        },
        ActionObservation {
            line_of_sight: true,
            confirmed_contact: true,
            water_contact: false,
        },
    )
}
fn run(game: &mut GameSession) -> Result<GameReceipt, GameplayError> {
    act(
        game,
        GameAction::RunScript {
            script: id("script"),
        },
    )
}

#[test]
fn actual_script_api_changes_variables_and_door_once_across_save_resume_retry() {
    let source = fixture();
    let before = serde_json::to_vec(&source).unwrap();
    let mut game = session(source.clone());
    let receipt = run(&mut game).unwrap();
    assert_eq!(
        game.state().narrative.as_ref().unwrap().variables[&id("flag")],
        NarrativeValue::Boolean(true)
    );
    assert!(game.state().objects[&id("door")].open);
    assert_eq!(
        receipt
            .events
            .iter()
            .filter(|event| matches!(event, GameplayEvent::ScriptMessage { .. }))
            .count(),
        1
    );
    let save = game.save_bytes().unwrap();
    assert_eq!(
        serde_json::from_slice::<serde_json::Value>(&save).unwrap()["version"],
        6
    );
    let mut resumed =
        GameSession::resume(source.clone(), game.source_hash(), &save, false).unwrap();
    assert_eq!(
        resumed
            .execute(&receipt.request, ActionObservation::default())
            .unwrap(),
        receipt
    );
    assert_eq!(resumed.save_bytes().unwrap(), save);
    assert_eq!(serde_json::to_vec(&source).unwrap(), before);
}

#[test]
fn denied_script_access_after_real_variable_write_keeps_whole_party_unchanged() {
    let mut source = fixture();
    source.scripting.as_mut().unwrap().scripts[0]
        .permissions
        .doors
        .clear();
    let mut game = session(source);
    let before = game.save_bytes().unwrap();
    let GameplayError::ScriptRefused(error) = run(&mut game).unwrap_err() else {
        panic!("expected actionable script diagnostic")
    };
    assert_eq!(error.kind, ScriptDiagnosticKind::Permission);
    assert_eq!(error.instruction, Some(id("open")));
    assert_eq!(game.save_bytes().unwrap(), before);
}

#[test]
fn script_cpu_budget_and_output_event_budget_both_refuse_atomically() {
    let mut source = fixture();
    source.scripting.as_mut().unwrap().scripts[0].steps = vec![
        step(
            "write",
            ScriptOperation::SetVariable {
                variable: id("flag"),
                value: NarrativeValue::Boolean(true),
            },
        ),
        step(
            "outer",
            ScriptOperation::Repeat {
                iterations: 16,
                steps: vec![step(
                    "inner",
                    ScriptOperation::Repeat {
                        iterations: 16,
                        steps: vec![step(
                            "message",
                            ScriptOperation::Message {
                                text: "hello".into(),
                            },
                        )],
                    },
                )],
            },
        ),
    ];
    let mut game = session(source);
    let before = game.save_bytes().unwrap();
    let GameplayError::ScriptRefused(error) = run(&mut game).unwrap_err() else {
        panic!("expected budget diagnostic")
    };
    assert_eq!(error.kind, ScriptDiagnosticKind::Budget);
    assert_eq!(game.save_bytes().unwrap(), before);
    let mut source = fixture();
    source.profile.max_events_per_tick = 2;
    let mut game = session(source);
    let before = game.save_bytes().unwrap();
    let GameplayError::ScriptRefused(error) = run(&mut game).unwrap_err() else {
        panic!("expected event budget diagnostic")
    };
    assert_eq!(error.kind, ScriptDiagnosticKind::Budget);
    assert_eq!(error.instruction, Some(id("open")));
    assert!(error.cause.contains("event budget"));
    assert!(error.remedy.contains("Reduce repetitions"));
    assert_eq!(game.save_bytes().unwrap(), before);
}

#[test]
fn permitted_script_door_still_uses_ordinary_interaction_range() {
    let mut source = fixture();
    source.objects[0].position.x = 50.0;
    let mut game = session(source);
    let before = game.save_bytes().unwrap();
    let GameplayError::ScriptRefused(error) = run(&mut game).unwrap_err() else {
        panic!("expected game API diagnostic")
    };
    assert_eq!(error.kind, ScriptDiagnosticKind::GameApi);
    assert_eq!(error.instruction, Some(id("open")));
    assert_eq!(game.save_bytes().unwrap(), before);
}

#[test]
fn rehashed_script_variable_door_or_execution_count_tampering_is_refused() {
    let source = fixture();
    let mut game = session(source.clone());
    run(&mut game).unwrap();
    let save: serde_json::Value = serde_json::from_slice(&game.save_bytes().unwrap()).unwrap();
    for mutation in 0..3 {
        let mut forged = save.clone();
        match mutation {
            0 => {
                forged["state"]["narrative"]["variables"][id("flag").to_string()]["value"] =
                    json!(false);
            }
            1 => forged["state"]["objects"][id("door").to_string()]["open"] = json!(false),
            _ => {
                let events = forged["state"]["receipts"][0]["events"]
                    .as_array_mut()
                    .unwrap();
                let executed = events
                    .iter_mut()
                    .find(|event| event["kind"] == "script_executed")
                    .unwrap();
                executed["steps"] = json!(1);
            }
        }
        forged["state_hash"] =
            serde_json::to_value(forge_core::canonical_json_sha256(&forged["state"]).unwrap())
                .unwrap();
        assert!(
            GameSession::resume(
                source.clone(),
                game.source_hash(),
                &serde_json::to_vec(&forged).unwrap(),
                false
            )
            .is_err(),
            "forgery {mutation}"
        );
    }
}

fn cinematic_source() -> GameplayDocument {
    let mut source = fixture();
    source.actors.push(ActorDefinition {
        id: id("giver"),
        name: "Courier".into(),
        role: ActorRole::Friendly,
        position: forge_core::Vec3::new(1.5, 0.0, 0.0),
        max_vitality: 100,
        protection: 0,
        inventory_capacity: 8,
        dialogue: Some(id("quest-dialogue")),
        patrol: vec![],
        flees_danger: false,
    });
    source.items.extend([
        ItemDefinition {
            id: id("parcel"),
            name: "Parcel".into(),
            max_stack: 1,
        },
        ItemDefinition {
            id: id("reward"),
            name: "Reward".into(),
            max_stack: 1,
        },
    ]);
    source.objects.push(ObjectDefinition {
        id: id("parcel-pickup"),
        name: "Parcel".into(),
        position: forge_core::Vec3::new(0.5, 0.0, 0.0),
        kind: ObjectKind::Pickup {
            item: id("parcel"),
            quantity: 1,
        },
        physics: ObjectPhysics::default(),
    });
    source.quests.push(QuestDefinition {
        id: id("quest"),
        name: "Return the parcel".into(),
        giver: id("giver"),
        required_item: id("parcel"),
        required_quantity: 1,
        reward_item: Some(id("reward")),
        reward_quantity: 1,
    });
    source.dialogues.push(DialogueDefinition {
        id: id("quest-dialogue"),
        start: id("quest-node"),
        nodes: vec![DialogueNode {
            id: id("quest-node"),
            text: "Can you return the parcel?".into(),
            choices: vec![
                DialogueChoice {
                    id: id("accept"),
                    text: "I will find it.".into(),
                    condition: Condition::Quest {
                        quest: id("quest"),
                        status: QuestStatus::Available,
                    },
                    effects: vec![DialogueEffect::AcceptQuest { quest: id("quest") }],
                    next: None,
                },
                DialogueChoice {
                    id: id("turn-in"),
                    text: "Here it is.".into(),
                    condition: Condition::Quest {
                        quest: id("quest"),
                        status: QuestStatus::Active,
                    },
                    effects: vec![DialogueEffect::CompleteQuest { quest: id("quest") }],
                    next: None,
                },
            ],
        }],
    });
    source
        .with_cinematic_definition(CinematicDefinition {
            id: id("sequence"),
            name: "Arrival".into(),
            cues: vec![
                CinematicCue {
                    id: id("first-cue"),
                    duration_ms: 100,
                    speaker: Some(id("player")),
                    text: Some("First line".into()),
                    camera: Some(CinematicCamera {
                        eye: forge_core::Vec3::new(0.0, 2.0, -4.0),
                        target: forge_core::Vec3::ZERO,
                        vertical_fov_degrees: 60.0,
                    }),
                },
                CinematicCue {
                    id: id("second-cue"),
                    duration_ms: 100,
                    speaker: None,
                    text: Some("Second line".into()),
                    camera: None,
                },
            ],
        })
        .unwrap()
}
fn advance(game: &mut GameSession, step_ns: u64) -> PhysicsFrameReceipt {
    let frame = PhysicsFrame {
        source_hash: game.source_hash(),
        tick: game.state().tick + 1,
        simulation_step_ns: Some(step_ns),
        actors: game
            .state()
            .actors
            .iter()
            .map(|(actor, state)| ActorObservation {
                actor: *actor,
                position: state.position,
            })
            .collect(),
        objects: vec![],
        contacts: vec![],
        heat: vec![],
        danger: vec![],
    };
    game.advance_frame(&frame).unwrap()
}
fn start(game: &mut GameSession) -> GameReceipt {
    act(
        game,
        GameAction::StartCinematic {
            sequence: id("sequence"),
        },
    )
    .unwrap()
}

#[test]
fn cinematic_resume_inside_cue_then_finish_restores_control_once_and_keeps_quests() {
    let source = cinematic_source();
    let mut game = session(source.clone());
    let quests = game.state().quests.clone();
    start(&mut game);
    advance(&mut game, 50_000_000);
    assert!(game.cinematic_controls_player());
    let presentation = game.cinematic_presentation().unwrap().unwrap();
    assert_eq!(presentation.cue_elapsed_ns, 50_000_000);
    let save = game.save_bytes().unwrap();
    let mut resumed = GameSession::resume(source, game.source_hash(), &save, false).unwrap();
    assert_eq!(
        resumed.cinematic_presentation().unwrap(),
        Some(presentation)
    );
    let boundary = advance(&mut resumed, 50_000_000);
    assert!(
        !boundary
            .events
            .iter()
            .any(|event| matches!(event, GameplayEvent::CinematicEnded { .. }))
    );
    assert_eq!(
        resumed.cinematic_presentation().unwrap().unwrap().cue,
        id("second-cue")
    );
    let ended = advance(&mut resumed, 100_000_000);
    assert_eq!(
        ended
            .events
            .iter()
            .filter(|event| matches!(
                event,
                GameplayEvent::CinematicEnded {
                    reason: CinematicEndReason::Finished,
                    ..
                }
            ))
            .count(),
        1
    );
    assert!(!resumed.cinematic_controls_player());
    assert!(resumed.cinematic_presentation().unwrap().is_none());
    assert_eq!(resumed.state().quests, quests);
    assert_eq!(resumed.advance_frame(&ended.frame).unwrap(), ended);
    assert!(
        !advance(&mut resumed, 1)
            .events
            .iter()
            .any(|event| matches!(event, GameplayEvent::CinematicEnded { .. }))
    );
}

#[test]
fn cinematic_skip_and_interrupt_restore_control_without_mutating_script_state() {
    for stop in [GameAction::SkipCinematic, GameAction::InterruptCinematic] {
        let mut game = session(cinematic_source());
        let variables = game.state().narrative.clone();
        start(&mut game);
        advance(&mut game, 50_000_000);
        let receipt = act(&mut game, stop).unwrap();
        assert!(!game.cinematic_controls_player());
        assert!(game.cinematic_presentation().unwrap().is_none());
        assert_eq!(game.state().narrative, variables);
        assert_eq!(
            game.execute(&receipt.request, ActionObservation::default())
                .unwrap(),
            receipt
        );
        assert_eq!(
            receipt
                .events
                .iter()
                .filter(|event| matches!(event, GameplayEvent::CinematicEnded { .. }))
                .count(),
            1
        );
    }
}

#[test]
fn gameplay_actions_during_presentation_are_refused_but_skip_remains_available() {
    let mut game = session(cinematic_source());
    start(&mut game);
    let before = game.save_bytes().unwrap();
    assert!(run(&mut game).is_err());
    assert_eq!(game.save_bytes().unwrap(), before);
    act(&mut game, GameAction::SkipCinematic).unwrap();
    assert!(run(&mut game).is_ok());
}

#[test]
fn skipping_a_cinematic_keeps_an_active_quest_playable_and_its_reward_unique_after_resume() {
    let source = cinematic_source();
    let mut game = session(source.clone());
    act(
        &mut game,
        GameAction::ChooseDialogue {
            speaker: id("giver"),
            node: id("quest-node"),
            choice: id("accept"),
        },
    )
    .unwrap();
    act(
        &mut game,
        GameAction::Pickup {
            object: id("parcel-pickup"),
        },
    )
    .unwrap();
    assert_eq!(game.state().quests[&id("quest")], QuestStatus::Active);
    start(&mut game);
    advance(&mut game, 50_000_000);
    act(&mut game, GameAction::SkipCinematic).unwrap();
    assert_eq!(game.state().quests[&id("quest")], QuestStatus::Active);
    let receipt = act(
        &mut game,
        GameAction::ChooseDialogue {
            speaker: id("giver"),
            node: id("quest-node"),
            choice: id("turn-in"),
        },
    )
    .unwrap();
    assert_eq!(game.state().quests[&id("quest")], QuestStatus::Completed);
    assert_eq!(
        game.state().actors[&id("player")]
            .inventory
            .get(&id("reward")),
        Some(&1)
    );
    let save = game.save_bytes().unwrap();
    let mut resumed = GameSession::resume(source, game.source_hash(), &save, false).unwrap();
    assert_eq!(
        resumed
            .execute(&receipt.request, ActionObservation::default())
            .unwrap(),
        receipt
    );
    assert_eq!(
        resumed.state().actors[&id("player")]
            .inventory
            .get(&id("reward")),
        Some(&1)
    );
    assert_eq!(resumed.save_bytes().unwrap(), save);
}

#[test]
fn rehashed_cinematic_progress_must_match_its_retained_start_receipt() {
    let source = cinematic_source();
    let mut game = session(source.clone());
    start(&mut game);
    advance(&mut game, 50_000_000);
    let mut forged: serde_json::Value =
        serde_json::from_slice(&game.save_bytes().unwrap()).unwrap();
    forged["state"]["cinematic"]["started_at_ns"] = json!(1);
    forged["state_hash"] =
        serde_json::to_value(forge_core::canonical_json_sha256(&forged["state"]).unwrap()).unwrap();
    assert!(
        GameSession::resume(
            source,
            game.source_hash(),
            &serde_json::to_vec(&forged).unwrap(),
            false
        )
        .is_err()
    );
}

#[test]
fn rehashed_missing_cinematic_termination_at_last_frame_is_refused() {
    let source = cinematic_source();
    let mut game = session(source.clone());
    start(&mut game);
    advance(&mut game, 200_000_000);
    let mut forged: serde_json::Value =
        serde_json::from_slice(&game.save_bytes().unwrap()).unwrap();
    forged["state"]["last_frame"]["events"]
        .as_array_mut()
        .unwrap()
        .retain(|event| event["kind"] != "cinematic_ended");
    forged["state_hash"] =
        serde_json::to_value(forge_core::canonical_json_sha256(&forged["state"]).unwrap()).unwrap();
    assert!(
        GameSession::resume(
            source,
            game.source_hash(),
            &serde_json::to_vec(&forged).unwrap(),
            false
        )
        .is_err()
    );
}
