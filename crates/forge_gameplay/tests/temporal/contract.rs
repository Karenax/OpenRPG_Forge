//! Lot D acceptance oracles; register the integration target after runtime wiring.
use forge_core::{AreaId, CommandId, ContentHash, EntityId, ModuleId, Vec3};
use forge_gameplay::*;
use serde_json::json;
use std::collections::BTreeMap;

fn id(name: &str) -> EntityId {
    EntityId::deterministic(format!("temporal/{name}"))
}

fn fixture() -> GameplayDocument {
    let mut document: GameplayDocument=serde_json::from_value(json!({
        "schema":"forge.gameplay","version":1,"module_id":ModuleId::deterministic("temporal"),
        "area_id":AreaId::deterministic("temporal"),"player":id("player"),
        "profile":{"id":"temporal.minimal","version":1,"interaction_range_m":3.0,"attack_range_m":2.0,
            "attack_damage":20,"attack_stamina_cost":5,"max_stamina":100,"max_action_receipts":512,"max_events_per_tick":128},
        "actors":[
            {"id":id("player"),"name":"Player","role":"player","position":{"x":0,"y":0,"z":0},"max_vitality":100,"protection":3,"inventory_capacity":8,"dialogue":null,"patrol":[],"flees_danger":false},
            {"id":id("enemy"),"name":"Enemy","role":"enemy","position":{"x":1,"y":0,"z":0},"max_vitality":1000,"protection":0,"inventory_capacity":8,"dialogue":null,"patrol":[],"flees_danger":false},
            {"id":id("participant"),"name":"Participant","role":"enemy","position":{"x":1.5,"y":0,"z":0},"max_vitality":40,"protection":0,"inventory_capacity":8,"dialogue":null,"patrol":[],"flees_danger":false}],
        "items":[{"id":id("herb"),"name":"Herb","max_stack":8}],"objects":[],"places":[],"dialogues":[],"quests":[]
    })).unwrap();
    document.objects.push(ObjectDefinition {
        id: id("pickup"),
        name: "Herbs".into(),
        position: Vec3::new(0.5, 0.0, 0.0),
        kind: ObjectKind::Pickup {
            item: id("herb"),
            quantity: 8,
        },
        physics: ObjectPhysics::default(),
    });
    let simulation = SimulationDefinition {
        effects: vec![TimedEffectDefinition {
            id: id("ward"),
            name: "Ward".into(),
            duration_ms: 100,
            attack_protection_bonus: 10,
            stacking: EffectStacking::Reject,
            exclusive_with: vec![],
        }],
        abilities: vec![
            AbilityDefinition {
                id: id("protect"),
                name: "Protect".into(),
                target: AbilityTarget::SelfOnly,
                range_m: 0.0,
                condition: Condition::Always,
                stamina_cost: 5,
                item_costs: vec![QuestConsumption {
                    item: id("herb"),
                    quantity: 1,
                }],
                effects: vec![AbilityEffect::Timed { effect: id("ward") }],
            },
            AbilityDefinition {
                id: id("heal"),
                name: "Heal".into(),
                target: AbilityTarget::SelfOnly,
                range_m: 0.0,
                condition: Condition::Always,
                stamina_cost: 7,
                item_costs: vec![QuestConsumption {
                    item: id("herb"),
                    quantity: 1,
                }],
                effects: vec![AbilityEffect::Heal { points: 25 }],
            },
        ],
        actor_abilities: BTreeMap::from([(id("player"), vec![id("protect"), id("heal")])]),
        ..SimulationDefinition::default()
    };
    document
        .with_simulation_definition(Some(simulation))
        .unwrap()
}

fn session(document: GameplayDocument) -> GameSession {
    GameSession::new(document, ContentHash::from_bytes([71; 32])).unwrap()
}

fn act_as(
    game: &mut GameSession,
    actor: EntityId,
    action: GameAction,
) -> Result<GameReceipt, GameplayError> {
    let sequence = game.state().sequence;
    game.execute(
        &GameRequest {
            command_id: CommandId::deterministic(format!("temporal/{sequence}")),
            expected_sequence: sequence,
            actor,
            action,
        },
        ActionObservation {
            line_of_sight: true,
            confirmed_contact: true,
            water_contact: false,
        },
    )
}

fn act(game: &mut GameSession, action: GameAction) -> Result<GameReceipt, GameplayError> {
    act_as(game, id("player"), action)
}

fn prepared(document: GameplayDocument) -> GameSession {
    let mut game = session(document);
    act(
        &mut game,
        GameAction::Pickup {
            object: id("pickup"),
        },
    )
    .unwrap();
    game
}

fn frame(game: &GameSession, step_ns: u64) -> PhysicsFrame {
    PhysicsFrame {
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
    }
}

fn step(game: &mut GameSession, step_ns: u64) -> PhysicsFrameReceipt {
    game.advance_frame(&frame(game, step_ns)).unwrap()
}

fn protect(game: &mut GameSession) -> Result<GameReceipt, GameplayError> {
    act(
        game,
        GameAction::UseAbility {
            ability: id("protect"),
            target: id("player"),
        },
    )
}

#[test]
fn ability_costs_and_healing_are_atomic_and_retry_preserves_source() {
    let document = fixture();
    let source = serde_json::to_vec(&document).unwrap();
    let mut game = prepared(document);
    act_as(
        &mut game,
        id("enemy"),
        GameAction::Attack {
            target: id("player"),
        },
    )
    .unwrap();
    assert_eq!(game.state().actors[&id("player")].vitality, 83);
    let receipt = act(
        &mut game,
        GameAction::UseAbility {
            ability: id("heal"),
            target: id("player"),
        },
    )
    .unwrap();
    assert!(
        receipt
            .events
            .iter()
            .any(|event| matches!(event, GameplayEvent::Healed { amount: 17, .. }))
    );
    assert_eq!(game.state().actors[&id("player")].vitality, 100);
    assert_eq!(game.state().actors[&id("player")].stamina, 93);
    assert_eq!(game.state().actors[&id("player")].inventory[&id("herb")], 7);
    let before = game.save_bytes().unwrap();
    assert_eq!(
        game.execute(&receipt.request, ActionObservation::default())
            .unwrap(),
        receipt
    );
    assert_eq!(game.save_bytes().unwrap(), before);
    assert!(
        act(
            &mut game,
            GameAction::UseAbility {
                ability: id("heal"),
                target: id("player")
            }
        )
        .is_err()
    );
    assert_eq!(game.save_bytes().unwrap(), before);
    assert_eq!(serde_json::to_vec(game.document()).unwrap(), source);
}

#[test]
fn timed_protection_uses_the_same_calculation_and_expires_once_after_resume() {
    let document = fixture();
    let mut game = prepared(document.clone());
    protect(&mut game).unwrap();
    assert_eq!(game.effective_attack_protection(id("player")), Some(13));
    act_as(
        &mut game,
        id("enemy"),
        GameAction::Attack {
            target: id("player"),
        },
    )
    .unwrap();
    assert_eq!(game.state().actors[&id("player")].vitality, 93);
    step(&mut game, 50_000_000);
    let saved = game.save_bytes().unwrap();
    assert_eq!(
        serde_json::from_slice::<serde_json::Value>(&saved).unwrap()["version"],
        5
    );
    let mut resumed = GameSession::resume(document, game.source_hash(), &saved, false).unwrap();
    let observed = frame(&resumed, 50_000_000);
    let expired = resumed.advance_frame(&observed).unwrap();
    assert_eq!(
        expired
            .events
            .iter()
            .filter(|event| matches!(
                event,
                GameplayEvent::TimedEffectEnded {
                    reason: EffectEndReason::Expired,
                    ..
                }
            ))
            .count(),
        1
    );
    assert_eq!(resumed.effective_attack_protection(id("player")), Some(3));
    let before = resumed.save_bytes().unwrap();
    assert_eq!(resumed.advance_frame(&observed).unwrap(), expired);
    assert_eq!(resumed.save_bytes().unwrap(), before);
    assert!(
        !step(&mut resumed, 1_000_000)
            .events
            .iter()
            .any(|event| matches!(event, GameplayEvent::TimedEffectEnded { .. }))
    );
}

#[test]
fn rejection_stacking_refresh_and_cancellation_preserve_their_individual_causes() {
    let mut rejected = prepared(fixture());
    protect(&mut rejected).unwrap();
    let before = rejected.save_bytes().unwrap();
    assert!(protect(&mut rejected).is_err());
    assert_eq!(rejected.save_bytes().unwrap(), before);
    let mut document = fixture();
    document.simulation.as_mut().unwrap().effects[0].stacking =
        EffectStacking::Stack { max_instances: 2 };
    let mut stacked = prepared(document);
    protect(&mut stacked).unwrap();
    protect(&mut stacked).unwrap();
    assert_eq!(stacked.effective_attack_protection(id("player")), Some(23));
    let before = stacked.save_bytes().unwrap();
    assert!(protect(&mut stacked).is_err());
    assert_eq!(stacked.save_bytes().unwrap(), before);
    let mut document = fixture();
    document.simulation.as_mut().unwrap().effects[0].stacking = EffectStacking::Refresh;
    let mut refreshed = prepared(document);
    protect(&mut refreshed).unwrap();
    step(&mut refreshed, 50_000_000);
    let receipt = protect(&mut refreshed).unwrap();
    assert_eq!(
        receipt
            .events
            .iter()
            .filter(|event| matches!(
                event,
                GameplayEvent::TimedEffectEnded {
                    reason: EffectEndReason::Refreshed,
                    ..
                }
            ))
            .count(),
        1
    );
    assert_eq!(
        refreshed.state().simulation.as_ref().unwrap().effects.len(),
        1
    );
    assert!(
        !step(&mut refreshed, 50_000_000)
            .events
            .iter()
            .any(|event| matches!(event, GameplayEvent::TimedEffectEnded { .. }))
    );
    let instance = *refreshed
        .state()
        .simulation
        .as_ref()
        .unwrap()
        .effects
        .keys()
        .next()
        .unwrap();
    let cancelled = act(&mut refreshed, GameAction::CancelEffect { instance }).unwrap();
    assert_eq!(
        cancelled.events,
        vec![GameplayEvent::TimedEffectEnded {
            instance,
            reason: EffectEndReason::Cancelled
        }]
    );
    let before = refreshed.save_bytes().unwrap();
    assert!(act(&mut refreshed, GameAction::CancelEffect { instance }).is_err());
    assert_eq!(refreshed.save_bytes().unwrap(), before);
    assert!(
        !step(&mut refreshed, 100_000_000)
            .events
            .iter()
            .any(|event| matches!(event, GameplayEvent::TimedEffectEnded { .. }))
    );
}

#[test]
fn an_incompatible_later_effect_rolls_back_earlier_healing_and_all_costs() {
    let mut document = fixture();
    document.simulation.as_mut().unwrap().abilities[0]
        .effects
        .insert(0, AbilityEffect::Heal { points: 25 });
    let mut game = prepared(document);
    protect(&mut game).unwrap();
    act_as(
        &mut game,
        id("enemy"),
        GameAction::Attack {
            target: id("player"),
        },
    )
    .unwrap();
    assert_eq!(game.state().actors[&id("player")].vitality, 93);
    let before = game.save_bytes().unwrap();
    assert!(protect(&mut game).is_err());
    assert_eq!(game.save_bytes().unwrap(), before);
}

fn with_encounter(mut document: GameplayDocument, rearm: EncounterRearm) -> GameplayDocument {
    let simulation = document.simulation.as_mut().unwrap();
    simulation.events.push(CalendarEventDefinition {
        id: id("arrival"),
        name: "Arrival".into(),
        first_minute: 0,
        repeat_every_minutes: Some(1),
    });
    simulation.encounters.push(EncounterDefinition {
        id: id("encounter"),
        name: "Encounter".into(),
        event: id("arrival"),
        participants: vec![id("participant")],
        condition: Condition::Always,
        rearm,
    });
    document.validate().unwrap();
    document
}

#[test]
fn scheduled_events_resume_without_repeating_the_already_crossed_boundary() {
    let mut document = fixture();
    document.simulation.as_mut().unwrap().events = vec![
        CalendarEventDefinition {
            id: id("once"),
            name: "Once".into(),
            first_minute: 0,
            repeat_every_minutes: None,
        },
        CalendarEventDefinition {
            id: id("repeat"),
            name: "Repeat".into(),
            first_minute: 1,
            repeat_every_minutes: Some(2),
        },
    ];
    let mut game = session(document.clone());
    assert_eq!(
        step(&mut game, 500_000_000)
            .events
            .iter()
            .filter(|event| matches!(event, GameplayEvent::CalendarTriggered { .. }))
            .count(),
        1
    );
    assert_eq!(
        step(&mut game, 500_000_000)
            .events
            .iter()
            .filter(|event| matches!(event, GameplayEvent::CalendarTriggered { .. }))
            .count(),
        1
    );
    let mut resumed = GameSession::resume(
        document.clone(),
        game.source_hash(),
        &game.save_bytes().unwrap(),
        false,
    )
    .unwrap();
    assert!(
        !step(&mut resumed, 1_000_000_000)
            .events
            .iter()
            .any(|event| matches!(event, GameplayEvent::CalendarTriggered { .. }))
    );
    let events = step(&mut resumed, 1_000_000_000).events;
    assert!(events.iter().any(|event|matches!(event,GameplayEvent::CalendarTriggered {event,cycle:1,..} if *event==id("repeat"))));
    let moment = document
        .simulation
        .as_ref()
        .unwrap()
        .clock
        .moment_at(resumed.state().simulation.as_ref().unwrap().elapsed_ns)
        .unwrap();
    assert_eq!(
        moment,
        CalendarMoment {
            day: 1,
            hour: 8,
            minute: 3
        }
    );
}

#[test]
fn one_shot_participants_are_absent_then_present_and_do_not_duplicate_after_resume() {
    let document = with_encounter(fixture(), EncounterRearm::Once);
    let mut game = session(document.clone());
    assert!(!game.state().actors[&id("participant")].active);
    assert!(
        act(
            &mut game,
            GameAction::Attack {
                target: id("participant")
            }
        )
        .is_err()
    );
    step(&mut game, 1_000_000);
    step(&mut game, 1_000_000);
    assert!(game.state().actors[&id("participant")].active);
    assert_eq!(
        game.state().simulation.as_ref().unwrap().activations.len(),
        1
    );
    step(&mut game, 1_000_000_000);
    let mut resumed = GameSession::resume(
        document,
        game.source_hash(),
        &game.save_bytes().unwrap(),
        false,
    )
    .unwrap();
    step(&mut resumed, 1_000_000_000);
    assert_eq!(
        resumed
            .state()
            .simulation
            .as_ref()
            .unwrap()
            .activations
            .len(),
        1
    );
}

#[test]
fn recurring_encounter_requires_defeat_and_cooldown_before_rearming_the_same_ids() {
    let document = with_encounter(
        fixture(),
        EncounterRearm::AfterDefeat {
            cooldown_minutes: 1,
        },
    );
    let mut game = session(document.clone());
    step(&mut game, 1_000_000);
    step(&mut game, 1_000_000);
    for _ in 0..2 {
        act(
            &mut game,
            GameAction::Attack {
                target: id("participant"),
            },
        )
        .unwrap();
    }
    assert_eq!(game.state().actors[&id("participant")].vitality, 0);
    step(&mut game, 900_000_000);
    assert_eq!(
        game.state().simulation.as_ref().unwrap().activations.len(),
        1
    );
    step(&mut game, 100_000_000);
    step(&mut game, 1_000_000);
    assert_eq!(game.state().actors[&id("participant")].vitality, 40);
    let activations = &game.state().simulation.as_ref().unwrap().activations;
    assert_eq!(activations.len(), 2);
    assert_eq!(activations[1].previous_vitality[&id("participant")], 0);
    assert!(
        GameSession::resume(
            document,
            game.source_hash(),
            &game.save_bytes().unwrap(),
            false
        )
        .is_ok()
    );
}

#[test]
fn excessive_calendar_work_or_invalid_step_leaves_the_entire_frame_unchanged() {
    let mut document = fixture();
    let simulation = document.simulation.as_mut().unwrap();
    simulation.clock.milliseconds_per_minute = 1;
    simulation.events = (0..MAX_CALENDAR_EVENTS)
        .map(|index| CalendarEventDefinition {
            id: id(&format!("event-{index}")),
            name: format!("Event {index}"),
            first_minute: 0,
            repeat_every_minutes: Some(1),
        })
        .collect();
    let mut game = session(document);
    let before = game.save_bytes().unwrap();
    assert!(game.advance_frame(&frame(&game, 1_000_000_000)).is_err());
    assert_eq!(game.save_bytes().unwrap(), before);
    assert!(game.advance_frame(&frame(&game, 0)).is_err());
    assert!(
        game.advance_frame(&frame(&game, MAX_SIMULATION_STEP_NS + 1))
            .is_err()
    );
    assert_eq!(game.save_bytes().unwrap(), before);
}

#[test]
fn timed_effect_exclusivity_is_symmetric_and_refusal_keeps_costs_unchanged() {
    let mut document = fixture();
    let simulation = document.simulation.as_mut().unwrap();
    let mut opposing = simulation.effects[0].clone();
    opposing.id = id("opposing-ward");
    opposing.name = "Opposing ward".into();
    // Only the original effect declares the exclusion: both application orders
    // must enforce the same author constraint.
    simulation.effects[0].exclusive_with.push(opposing.id);
    simulation.effects.push(opposing);
    let mut ability = simulation.abilities[0].clone();
    ability.id = id("opposing-protect");
    ability.effects = vec![AbilityEffect::Timed {
        effect: id("opposing-ward"),
    }];
    simulation.abilities.push(ability);
    simulation
        .actor_abilities
        .get_mut(&id("player"))
        .unwrap()
        .push(id("opposing-protect"));
    document.validate().unwrap();
    for (first, second) in [
        (id("protect"), id("opposing-protect")),
        (id("opposing-protect"), id("protect")),
    ] {
        let mut game = prepared(document.clone());
        act(
            &mut game,
            GameAction::UseAbility {
                ability: first,
                target: id("player"),
            },
        )
        .unwrap();
        let before = game.save_bytes().unwrap();
        assert!(
            act(
                &mut game,
                GameAction::UseAbility {
                    ability: second,
                    target: id("player")
                }
            )
            .is_err()
        );
        assert_eq!(game.save_bytes().unwrap(), before);
    }
}

#[test]
fn calendar_definitions_reject_timestamps_that_overflow_before_runtime() {
    let mut document = fixture();
    let simulation = document.simulation.as_mut().unwrap();
    simulation.clock.milliseconds_per_minute = 3_600_000;
    simulation.events.push(CalendarEventDefinition {
        id: id("overflow"),
        name: "Overflow".into(),
        first_minute: 1_000_000_000,
        repeat_every_minutes: None,
    });
    assert!(document.validate().is_err());
    let simulation = document.simulation.as_mut().unwrap();
    simulation.events[0].first_minute = 0;
    simulation.events[0].repeat_every_minutes = Some(1_000_000_000);
    assert!(document.validate().is_err());
    let simulation = document.simulation.as_mut().unwrap();
    simulation.events[0].repeat_every_minutes = Some(1);
    simulation.encounters.push(EncounterDefinition {
        id: id("overflow-encounter"),
        name: "Overflow encounter".into(),
        event: id("overflow"),
        participants: vec![id("participant")],
        condition: Condition::Always,
        rearm: EncounterRearm::AfterDefeat {
            cooldown_minutes: 1_000_000_000,
        },
    });
    assert!(document.validate().is_err());
}

#[test]
fn a_recurrent_encounter_requires_future_calendar_occurrences() {
    let mut document = with_encounter(
        fixture(),
        EncounterRearm::AfterDefeat {
            cooldown_minutes: 1,
        },
    );
    document.simulation.as_mut().unwrap().events[0].repeat_every_minutes = None;
    assert!(document.validate().is_err());
}
