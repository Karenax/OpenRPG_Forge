use forge_core::{AreaId, CommandId, ContentHash, EntityId, ModuleId, Vec3, canonical_json_sha256};
use forge_gameplay::*;
use serde_json::{Value, json};
use std::collections::BTreeMap;

fn id(name: &str) -> EntityId {
    EntityId::deterministic(format!("faction/{name}"))
}

fn actor(name: &str, role: ActorRole, x: f32) -> ActorDefinition {
    ActorDefinition {
        id: id(name),
        name: name.into(),
        role,
        position: Vec3::new(x, 0.0, 0.0),
        max_vitality: 100,
        protection: 0,
        inventory_capacity: 10,
        dialogue: None,
        patrol: vec![],
        flees_danger: false,
    }
}

fn legacy() -> GameplayDocument {
    serde_json::from_value(json!({
        "schema":"forge.gameplay","version":1,"module_id":ModuleId::deterministic("faction"),
        "area_id":AreaId::deterministic("faction"),
        "profile":{"id":"faction.minimal","version":1,"interaction_range_m":3.0,"attack_range_m":2.0,
            "attack_damage":20,"attack_stamina_cost":5,"max_stamina":100,
            "max_action_receipts":512,"max_events_per_tick":128},
        "player":id("player"),"actors":[actor("player",ActorRole::Player,0.0),
            actor("victim",ActorRole::Enemy,1.0),actor("visible",ActorRole::Friendly,2.0),
            actor("hidden",ActorRole::Friendly,2.0)],
        "items":[],"objects":[],"places":[],"dialogues":[],"quests":[]
    })).unwrap()
}

fn fixture() -> GameplayDocument {
    let mut document = legacy();
    for name in ["visible", "hidden"] {
        let dialogue = EntityId::deterministic(format!("faction/dialogue/{name}"));
        let node = EntityId::deterministic(format!("faction/node/{name}"));
        document
            .actors
            .iter_mut()
            .find(|actor| actor.id == id(name))
            .unwrap()
            .dialogue = Some(dialogue);
        document.dialogues.push(DialogueDefinition {
            id: dialogue,
            start: node,
            nodes: vec![DialogueNode {
                id: node,
                text: "Que me voulez-vous ?".into(),
                choices: [
                    ("welcome", NarrativeComparison::AtLeast, 0),
                    ("hostile", NarrativeComparison::AtMost, -1),
                ]
                .into_iter()
                .map(|(kind, comparison, threshold)| DialogueChoice {
                    id: EntityId::deterministic(format!("faction/choice/{name}/{kind}")),
                    text: kind.into(),
                    condition: Condition::Relation {
                        observer: id(name),
                        subject: id("player"),
                        comparison,
                        threshold,
                    },
                    effects: vec![],
                    next: None,
                })
                .collect(),
            }],
        });
    }
    // Both local observers belong to the same group and start with the same opinion.
    let factions = FactionsDefinition {
        version: 1,
        factions: vec![
            FactionDefinition {
                id: id("guards"),
                name: "Guetteurs".into(),
                perception_range_m: 10.0,
                protects_factions: vec![id("guards")],
                assault_delta: -30,
            },
            FactionDefinition {
                id: id("travellers"),
                name: "Voyageurs".into(),
                perception_range_m: 10.0,
                protects_factions: vec![],
                assault_delta: 0,
            },
        ],
        memberships: BTreeMap::from([
            (id("player"), id("travellers")),
            (id("victim"), id("guards")),
            (id("visible"), id("guards")),
            (id("hidden"), id("guards")),
        ]),
        relations: vec![FactionRelation {
            from: id("guards"),
            to: id("travellers"),
            value: 20,
        }],
    };
    // The candidate contains relation conditions, so adoption is validated as a whole.
    document.factions = Some(factions);
    let configuration = document.creator_configuration();
    document.with_creator_configuration(configuration).unwrap()
}

fn session(document: GameplayDocument) -> GameSession {
    GameSession::new(document, ContentHash::from_bytes([61; 32])).unwrap()
}

fn request(sequence: u64, actor: EntityId, target: EntityId) -> GameRequest {
    GameRequest {
        command_id: CommandId::deterministic(format!("faction/action/{sequence}")),
        expected_sequence: sequence,
        actor,
        action: GameAction::Attack { target },
    }
}

fn contact() -> ActionObservation {
    ActionObservation {
        line_of_sight: true,
        confirmed_contact: true,
        water_contact: false,
    }
}

fn witnesses() -> [FactionWitnessObservation; 2] {
    [
        FactionWitnessObservation {
            observer: id("visible"),
            line_of_sight: true,
        },
        FactionWitnessObservation {
            observer: id("hidden"),
            line_of_sight: false,
        },
    ]
}

fn assault(game: &mut GameSession) -> GameReceipt {
    game.execute_with_witnesses(
        &request(game.state().sequence, id("player"), id("victim")),
        contact(),
        &witnesses(),
    )
    .unwrap()
}

fn rehashed(value: &mut Value) -> Vec<u8> {
    value["state_hash"] = json!(canonical_json_sha256(&value["state"]).unwrap());
    serde_json::to_vec(value).unwrap()
}

#[test]
fn only_the_visible_local_witness_changes_and_retry_keeps_the_original_observation() {
    let document = fixture();
    let authored = serde_json::to_vec(&document).unwrap();
    let mut game = session(document);
    assert_eq!(
        game.personal_relation(id("visible"), id("player")),
        Some(20)
    );
    assert_eq!(game.personal_relation(id("player"), id("visible")), Some(0));
    let receipt = assault(&mut game);
    assert_eq!(receipt.faction_witnesses.len(), 1);
    assert_eq!(
        game.personal_relation(id("visible"), id("player")),
        Some(-10)
    );
    assert_eq!(game.personal_relation(id("hidden"), id("player")), Some(20));
    assert_eq!(
        game.dialogue_view(id("player"), id("visible"))
            .unwrap()
            .choices[0]
            .text,
        "hostile"
    );
    assert_eq!(
        game.dialogue_view(id("player"), id("hidden"))
            .unwrap()
            .choices[0]
            .text,
        "welcome"
    );
    let before = game.save_bytes().unwrap();
    assert_eq!(
        game.execute_with_witnesses(
            &receipt.request,
            ActionObservation::default(),
            &vec![witnesses()[1]; 17]
        )
        .unwrap(),
        receipt
    );
    assert_eq!(game.save_bytes().unwrap(), before);
    assert_eq!(game.state().actors[&id("player")].stamina, 95);
    assert_eq!(serde_json::to_vec(game.document()).unwrap(), authored);
}

#[test]
fn local_relations_resume_with_their_causes_and_rehashed_isolated_changes_are_refused() {
    let document = fixture();
    let mut game = session(document.clone());
    assault(&mut game);
    let bytes = game.save_bytes().unwrap();
    let mut value: Value = serde_json::from_slice(&bytes).unwrap();
    assert_eq!(value["version"], 4);
    let resumed = GameSession::resume(document.clone(), game.source_hash(), &bytes, false).unwrap();
    assert_eq!(
        resumed.personal_relation(id("visible"), id("player")),
        Some(-10)
    );
    assert_eq!(
        resumed.personal_relation(id("hidden"), id("player")),
        Some(20)
    );
    value["state"]["factions"]["opinions"][id("visible").to_string()][id("player").to_string()] =
        json!(99);
    assert!(
        GameSession::resume(document, game.source_hash(), &rehashed(&mut value), false).is_err()
    );
}

#[test]
fn removing_or_forging_a_retained_cause_is_refused_after_rehashing() {
    let document = fixture();
    let mut game = session(document.clone());
    assault(&mut game);
    let baseline: Value = serde_json::from_slice(&game.save_bytes().unwrap()).unwrap();
    for variant in 0..4 {
        let mut value = baseline.clone();
        let receipt = &mut value["state"]["receipts"][0];
        match variant {
            0 => receipt["faction_witnesses"] = json!([]),
            1 => {
                receipt["events"][1]["change"]["cause"] =
                    json!(CommandId::deterministic("other-cause"));
            }
            2 => receipt["events"][0]["contact"] = json!(false),
            _ => {
                let witness = receipt["faction_witnesses"][0].clone();
                receipt["faction_witnesses"]
                    .as_array_mut()
                    .unwrap()
                    .push(witness);
            }
        }
        assert!(
            GameSession::resume(
                document.clone(),
                game.source_hash(),
                &rehashed(&mut value),
                false
            )
            .is_err(),
            "variant {variant}"
        );
    }
}

#[test]
fn failed_contact_duplicate_witnesses_and_event_quota_leave_costs_and_relations_unchanged() {
    let mut game = session(fixture());
    let before = game.save_bytes().unwrap();
    let action = request(0, id("player"), id("victim"));
    assert!(
        game.execute_with_witnesses(
            &action,
            ActionObservation {
                line_of_sight: true,
                ..Default::default()
            },
            &witnesses()
        )
        .is_err()
    );
    assert!(
        game.execute_with_witnesses(&action, contact(), &[witnesses()[0]; 2])
            .is_err()
    );
    assert!(
        game.execute_with_witnesses(
            &action,
            contact(),
            &vec![witnesses()[0]; MAX_FACTION_WITNESSES + 1]
        )
        .is_err()
    );
    assert_eq!(game.save_bytes().unwrap(), before);
    let mut document = fixture();
    document.profile.max_events_per_tick = 1;
    let mut bounded = session(document);
    let before = bounded.save_bytes().unwrap();
    assert!(
        bounded
            .execute_with_witnesses(&action, contact(), &witnesses())
            .is_err()
    );
    assert_eq!(bounded.save_bytes().unwrap(), before);
}

#[test]
fn observers_outside_perception_or_already_defeated_do_not_change() {
    let mut document = fixture();
    document.factions.as_mut().unwrap().factions[0].perception_range_m = 1.5;
    let mut game = session(document);
    assert!(assault(&mut game).faction_witnesses.is_empty());
    assert_eq!(
        game.personal_relation(id("visible"), id("player")),
        Some(20)
    );
    let mut document = fixture();
    document
        .actors
        .iter_mut()
        .find(|actor| actor.id == id("visible"))
        .unwrap()
        .max_vitality = 10;
    let mut game = session(document);
    game.execute(&request(0, id("player"), id("visible")), contact())
        .unwrap();
    assert!(assault(&mut game).faction_witnesses.is_empty());
    assert_eq!(
        game.personal_relation(id("visible"), id("player")),
        Some(20)
    );
}

#[test]
fn saturation_retains_the_contact_without_duplicate_reaction_events() {
    let document = fixture();
    let mut game = session(document.clone());
    for _ in 0..4 {
        assault(&mut game);
    }
    assert_eq!(
        game.personal_relation(id("visible"), id("player")),
        Some(MIN_RELATION)
    );
    let saturated = assault(&mut game);
    assert_eq!(saturated.faction_witnesses.len(), 1);
    assert!(
        !saturated
            .events
            .iter()
            .any(|event| matches!(event, GameplayEvent::FactionReaction { .. }))
    );
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
fn definitions_and_relation_conditions_reject_invalid_bounds_and_references() {
    let document = fixture();
    for variant in 0..6 {
        let mut changed = document.clone();
        let factions = changed.factions.as_mut().unwrap();
        match variant {
            0 => factions.factions.push(factions.factions[0].clone()),
            1 => factions.factions[0].perception_range_m = f32::NAN,
            2 => {
                factions.memberships.insert(id("absent"), id("guards"));
            }
            3 => factions.relations[0].value = 101,
            4 => factions.factions[0].assault_delta = 1,
            _ => factions.factions[0].protects_factions.push(id("absent")),
        }
        assert!(changed.validate().is_err(), "variant {variant}");
    }
    let mut changed = document;
    changed.dialogues[0].nodes[0].choices[0].condition = Condition::Relation {
        observer: id("visible"),
        subject: id("player"),
        comparison: NarrativeComparison::AtLeast,
        threshold: 101,
    };
    assert!(changed.validate().is_err());
}

#[test]
fn names_are_compatible_but_reaction_rules_and_memberships_are_functional() {
    let document = fixture();
    let mut game = session(document.clone());
    assault(&mut game);
    let mut changed = document.clone();
    changed.factions.as_mut().unwrap().factions[0].name = "Veilleurs".into();
    assert_eq!(
        document.functional_hash().unwrap(),
        changed.functional_hash().unwrap()
    );
    assert!(
        GameSession::resume(
            changed.clone(),
            ContentHash::from_bytes([62; 32]),
            &game.save_bytes().unwrap(),
            true
        )
        .is_ok()
    );
    changed.factions.as_mut().unwrap().factions[0].assault_delta = -40;
    assert_ne!(
        document.functional_hash().unwrap(),
        changed.functional_hash().unwrap()
    );
    assert!(
        GameSession::resume(
            changed,
            ContentHash::from_bytes([62; 32]),
            &game.save_bytes().unwrap(),
            true
        )
        .is_err()
    );
}

#[test]
fn historical_content_has_no_new_save_fields_and_rejects_undeclared_observations() {
    let document = legacy();
    assert!(
        !serde_json::to_value(&document)
            .unwrap()
            .as_object()
            .unwrap()
            .contains_key("factions")
    );
    let mut game = session(document);
    let before = game.save_bytes().unwrap();
    assert!(
        game.execute_with_witnesses(
            &request(0, id("player"), id("victim")),
            contact(),
            &witnesses()
        )
        .is_err()
    );
    assert_eq!(game.save_bytes().unwrap(), before);
    game.execute(&request(0, id("player"), id("victim")), contact())
        .unwrap();
    let value: Value = serde_json::from_slice(&game.save_bytes().unwrap()).unwrap();
    assert_eq!(value["version"], 1);
    assert!(!value["state"].as_object().unwrap().contains_key("factions"));
    assert!(
        !value["state"]["receipts"][0]
            .as_object()
            .unwrap()
            .contains_key("faction_witnesses")
    );
}

#[test]
fn personal_relation_quota_refuses_the_entire_next_assault() {
    let mut document = fixture();
    document.profile.max_events_per_tick = 4096;
    document
        .actors
        .iter_mut()
        .find(|actor| actor.id == id("victim"))
        .unwrap()
        .max_vitality = 10_000;
    let mut observers = vec![id("visible")];
    let mut aggressors = vec![id("player")];
    for index in 1..16 {
        let name = format!("observer-{index}");
        document.actors.push(actor(&name, ActorRole::Friendly, 2.0));
        observers.push(id(&name));
        document
            .factions
            .as_mut()
            .unwrap()
            .memberships
            .insert(id(&name), id("guards"));
    }
    for index in 1..65 {
        let name = format!("aggressor-{index}");
        document.actors.push(actor(&name, ActorRole::Enemy, 0.0));
        aggressors.push(id(&name));
        document
            .factions
            .as_mut()
            .unwrap()
            .memberships
            .insert(id(&name), id("travellers"));
    }
    let observations: Vec<_> = observers
        .into_iter()
        .map(|observer| FactionWitnessObservation {
            observer,
            line_of_sight: true,
        })
        .collect();
    let mut game = session(document);
    for (sequence, aggressor) in aggressors.iter().take(64).enumerate() {
        game.execute_with_witnesses(
            &request(sequence as u64, *aggressor, id("victim")),
            contact(),
            &observations,
        )
        .unwrap();
    }
    assert_eq!(
        game.state().factions.as_ref().unwrap().modified_relations(),
        MAX_PERSONAL_RELATIONS
    );
    let before = game.save_bytes().unwrap();
    assert!(
        game.execute_with_witnesses(
            &request(64, aggressors[64], id("victim")),
            contact(),
            &observations
        )
        .is_err()
    );
    assert_eq!(game.save_bytes().unwrap(), before);
}
