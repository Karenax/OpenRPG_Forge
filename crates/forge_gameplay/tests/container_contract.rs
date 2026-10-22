use forge_core::{AreaId, CommandId, EntityId, ModuleId, Vec3, canonical_json_sha256};
use forge_gameplay::*;
use std::collections::BTreeMap;

#[derive(Clone, Copy)]
struct Ids {
    chest: EntityId,
    token: EntityId,
    key: EntityId,
    other: EntityId,
    key_pickup: EntityId,
    other_pickup: EntityId,
}

fn fixture() -> (GameplayDocument, Ids) {
    let ids = Ids {
        chest: EntityId::deterministic("containers/chest"),
        token: EntityId::deterministic("containers/token"),
        key: EntityId::deterministic("containers/key"),
        other: EntityId::deterministic("containers/other"),
        key_pickup: EntityId::deterministic("containers/key-pickup"),
        other_pickup: EntityId::deterministic("containers/other-pickup"),
    };
    let document: GameplayDocument = serde_json::from_value(serde_json::json!({
        "schema":"forge.gameplay", "version":1,
        "module_id":ModuleId::deterministic("containers/module"), "area_id":AreaId::deterministic("containers/area"),
        "profile":{"id":"containers.minimal","version":1,"interaction_range_m":3.0,"attack_range_m":2.0,
            "attack_damage":20,"attack_stamina_cost":5,"max_stamina":100,"max_action_receipts":512,"max_events_per_tick":128},
        "player":EntityId::deterministic("containers/player"),
        "actors":[{"id":EntityId::deterministic("containers/player"),"name":"Player","role":"player",
            "position":{"x":0.0,"y":0.0,"z":0.0},"max_vitality":100,"protection":0,"inventory_capacity":6,
            "dialogue":null,"patrol":[],"flees_danger":false}],
        "items":[{"id":ids.token,"name":"Jeton","max_stack":10},{"id":ids.key,"name":"Clé","max_stack":1},{"id":ids.other,"name":"Autre","max_stack":10}],
        "objects":[
            {"id":ids.chest,"name":"Coffre","position":{"x":1.0,"y":0.0,"z":0.0},"kind":{"kind":"prop"},"physics":ObjectPhysics::default()},
            {"id":ids.key_pickup,"name":"Clé au sol","position":{"x":0.5,"y":0.0,"z":0.0},"kind":{"kind":"pickup","item":ids.key,"quantity":1},"physics":ObjectPhysics::default()},
            {"id":ids.other_pickup,"name":"Autre au sol","position":{"x":0.5,"y":0.0,"z":0.0},"kind":{"kind":"pickup","item":ids.other,"quantity":2},"physics":ObjectPhysics::default()}
        ], "places":[],"dialogues":[],"quests":[]
    })).unwrap();
    let definition = ContainerDefinition {
        capacity_units: 6,
        initial_items: BTreeMap::from([(ids.token, 4)]),
        accepts: Vec::new(),
        key: None,
    };
    (
        document
            .with_container_definition(ids.chest, definition)
            .unwrap(),
        ids,
    )
}

fn session(document: GameplayDocument) -> GameSession {
    let hash = canonical_json_sha256(&document).unwrap();
    GameSession::new(document, hash).unwrap()
}
fn visible() -> ActionObservation {
    ActionObservation {
        line_of_sight: true,
        ..ActionObservation::default()
    }
}
fn request(game: &GameSession, action: GameAction) -> GameRequest {
    GameRequest {
        command_id: CommandId::deterministic(format!("container-action/{}", game.state().sequence)),
        expected_sequence: game.state().sequence,
        actor: game.document().player,
        action,
    }
}
fn execute(game: &mut GameSession, action: GameAction) -> GameReceipt {
    game.execute(&request(game, action), visible()).unwrap()
}
fn refused(game: &mut GameSession, action: GameAction, observation: ActionObservation) {
    let before = game.state().clone();
    assert!(game.execute(&request(game, action), observation).is_err());
    assert_eq!(game.state(), &before);
}
fn transfer(ids: Ids, quantity: u32, direction: TransferDirection) -> GameAction {
    GameAction::TransferItem {
        object: ids.chest,
        item: ids.token,
        quantity,
        direction,
    }
}
fn contents(game: &GameSession, chest: EntityId) -> &ContainerState {
    game.state().objects[&chest].container.as_ref().unwrap()
}
fn rewrite(bytes: &[u8], change: impl FnOnce(&mut GameState)) -> Vec<u8> {
    let mut envelope: serde_json::Value = serde_json::from_slice(bytes).unwrap();
    let mut state: GameState = serde_json::from_value(envelope["state"].clone()).unwrap();
    change(&mut state);
    envelope["state_hash"] = serde_json::to_value(canonical_json_sha256(&state).unwrap()).unwrap();
    envelope["state"] = serde_json::to_value(state).unwrap();
    serde_json::to_vec(&envelope).unwrap()
}

#[test]
fn transfers_are_atomic_idempotent_and_survive_real_save_resume() {
    let (document, ids) = fixture();
    let mut game = session(document.clone());
    let take = request(&game, transfer(ids, 3, TransferDirection::Take));
    let receipt = game.execute(&take, visible()).unwrap();
    let after = game.state().clone();
    assert_eq!(game.execute(&take, visible()).unwrap(), receipt);
    assert_eq!(game.state(), &after);
    assert_eq!(contents(&game, ids.chest).items[&ids.token], 1);
    assert_eq!(
        game.state().actors[&document.player].inventory[&ids.token],
        3
    );
    execute(&mut game, transfer(ids, 2, TransferDirection::Put));
    assert_eq!(contents(&game, ids.chest).items[&ids.token], 3);
    assert_eq!(
        game.state().actors[&document.player].inventory[&ids.token],
        1
    );
    let bytes = game.save_bytes().unwrap();
    assert_eq!(
        serde_json::from_slice::<serde_json::Value>(&bytes).unwrap()["version"],
        2
    );
    let resumed = GameSession::resume(document, game.source_hash(), &bytes, false).unwrap();
    assert_eq!(resumed.state(), game.state());
}

#[test]
fn missing_items_zero_quantity_full_inventory_distance_and_occlusion_preserve_everything() {
    let (mut document, ids) = fixture();
    document.actors[0].inventory_capacity = 2;
    let mut game = session(document);
    for quantity in [0, 3, 5] {
        refused(
            &mut game,
            transfer(ids, quantity, TransferDirection::Take),
            visible(),
        );
    }
    refused(
        &mut game,
        transfer(ids, 1, TransferDirection::Put),
        visible(),
    );
    refused(
        &mut game,
        transfer(ids, 1, TransferDirection::Take),
        ActionObservation::default(),
    );
    refused(
        &mut game,
        GameAction::TransferItem {
            object: ids.key_pickup,
            item: ids.token,
            quantity: 1,
            direction: TransferDirection::Take,
        },
        visible(),
    );
    let player = game.document().player;
    game.observe_position(player, Vec3::new(100.0, 0.0, 0.0))
        .unwrap();
    refused(
        &mut game,
        transfer(ids, 1, TransferDirection::Take),
        visible(),
    );
    game.observe_position(player, Vec3::ZERO).unwrap();
    execute(&mut game, transfer(ids, 1, TransferDirection::Take));
    refused(
        &mut game,
        transfer(ids, 2, TransferDirection::Put),
        visible(),
    );
}

#[test]
fn filters_capacity_and_stack_limits_refuse_without_removing_source_items() {
    let (mut document, ids) = fixture();
    let mut definition = document.container_definition(ids.chest).unwrap().clone();
    definition.accepts = vec![ids.token];
    document = document
        .with_container_definition(ids.chest, definition)
        .unwrap();
    let mut game = session(document);
    execute(
        &mut game,
        GameAction::Pickup {
            object: ids.other_pickup,
        },
    );
    refused(
        &mut game,
        GameAction::TransferItem {
            object: ids.chest,
            item: ids.other,
            quantity: 1,
            direction: TransferDirection::Put,
        },
        visible(),
    );

    let (document, ids) = fixture();
    let mut definition = document.container_definition(ids.chest).unwrap().clone();
    definition.capacity_units = 4;
    let mut game = session(
        document
            .with_container_definition(ids.chest, definition)
            .unwrap(),
    );
    execute(
        &mut game,
        GameAction::Pickup {
            object: ids.other_pickup,
        },
    );
    refused(
        &mut game,
        GameAction::TransferItem {
            object: ids.chest,
            item: ids.other,
            quantity: 1,
            direction: TransferDirection::Put,
        },
        visible(),
    );

    let (mut document, ids) = fixture();
    document
        .items
        .iter_mut()
        .find(|item| item.id == ids.token)
        .unwrap()
        .max_stack = 4;
    document.objects.push(ObjectDefinition {
        id: EntityId::deterministic("containers/extra-token"),
        name: "Extra".into(),
        position: Vec3::ZERO,
        kind: ObjectKind::Pickup {
            item: ids.token,
            quantity: 2,
        },
        physics: ObjectPhysics::default(),
    });
    let extra = document.objects.last().unwrap().id;
    let mut game = session(document);
    execute(&mut game, GameAction::Pickup { object: extra });
    refused(
        &mut game,
        transfer(ids, 1, TransferDirection::Put),
        visible(),
    );
}

#[test]
fn locked_containers_require_a_possessed_nonconsumed_key_and_restore_their_lock() {
    let (document, ids) = fixture();
    let mut definition = document.container_definition(ids.chest).unwrap().clone();
    definition.key = Some(ids.key);
    let document = document
        .with_container_definition(ids.chest, definition)
        .unwrap();
    let mut game = session(document.clone());
    refused(
        &mut game,
        transfer(ids, 1, TransferDirection::Take),
        visible(),
    );
    refused(
        &mut game,
        GameAction::SetContainerLocked {
            object: ids.chest,
            locked: false,
        },
        visible(),
    );
    execute(
        &mut game,
        GameAction::Pickup {
            object: ids.key_pickup,
        },
    );
    execute(
        &mut game,
        GameAction::SetContainerLocked {
            object: ids.chest,
            locked: false,
        },
    );
    execute(&mut game, transfer(ids, 4, TransferDirection::Take));
    assert_eq!(game.state().actors[&document.player].inventory[&ids.key], 1);
    assert!(contents(&game, ids.chest).items.is_empty());
    let save = game.save_bytes().unwrap();
    let mut resumed = GameSession::resume(document, game.source_hash(), &save, false).unwrap();
    assert!(!contents(&resumed, ids.chest).locked);
    execute(
        &mut resumed,
        GameAction::SetContainerLocked {
            object: ids.chest,
            locked: true,
        },
    );
    assert!(contents(&resumed, ids.chest).locked);
    resumed.save_bytes().unwrap();
}

#[test]
fn invalid_definitions_and_undeclared_capabilities_are_refused_on_a_clone() {
    let (source, ids) = fixture();
    let original = source.clone();
    let definition = source.container_definition(ids.chest).unwrap().clone();
    let mut invalid = definition.clone();
    invalid.capacity_units = 10_001;
    assert!(
        source
            .with_container_definition(ids.chest, invalid)
            .is_err()
    );
    let mut invalid = definition.clone();
    invalid.accepts = vec![ids.token, ids.token];
    assert!(
        source
            .with_container_definition(ids.chest, invalid)
            .is_err()
    );
    let mut invalid = definition.clone();
    invalid.initial_items.insert(ids.token, 0);
    assert!(
        source
            .with_container_definition(ids.chest, invalid)
            .is_err()
    );
    let mut invalid = definition.clone();
    invalid.accepts = vec![ids.other];
    assert!(
        source
            .with_container_definition(ids.chest, invalid)
            .is_err()
    );
    assert!(
        source
            .with_container_definition(ids.key_pickup, definition)
            .is_err()
    );
    let mut invalid = source.clone();
    invalid.creator = None;
    invalid.version = 1;
    assert!(invalid.validate().is_err());
    let mut invalid = source.clone();
    invalid
        .creator
        .as_mut()
        .unwrap()
        .required_capabilities
        .retain(|cap| *cap != GameplayCapability::Containers);
    assert!(invalid.validate().is_err());
    assert_eq!(source, original);
}

#[test]
fn event_quota_refuses_the_entire_transfer_but_allows_the_original_receipt_retry() {
    let (mut document, ids) = fixture();
    document.profile.max_events_per_tick = 1;
    let mut game = session(document);
    let first = request(&game, transfer(ids, 1, TransferDirection::Take));
    let receipt = game.execute(&first, visible()).unwrap();
    refused(
        &mut game,
        transfer(ids, 1, TransferDirection::Put),
        visible(),
    );
    assert_eq!(game.execute(&first, visible()).unwrap(), receipt);
    game.save_bytes().unwrap();
}

#[test]
fn forged_inventories_and_transfer_events_fail_even_after_recalculating_state_hash() {
    let (document, ids) = fixture();
    let mut game = session(document.clone());
    let original = game.save_bytes().unwrap();
    let forged = rewrite(&original, |state| {
        state
            .actors
            .get_mut(&document.player)
            .unwrap()
            .inventory
            .insert(ids.token, 1);
        state
            .objects
            .get_mut(&ids.chest)
            .unwrap()
            .container
            .as_mut()
            .unwrap()
            .items
            .insert(ids.token, 3);
    });
    assert!(GameSession::resume(document.clone(), game.source_hash(), &forged, false).is_err());
    execute(&mut game, transfer(ids, 1, TransferDirection::Take));
    let saved = game.save_bytes().unwrap();
    let forged = rewrite(&saved, |state| {
        state
            .actors
            .get_mut(&document.player)
            .unwrap()
            .inventory
            .insert(ids.token, 2);
        state
            .objects
            .get_mut(&ids.chest)
            .unwrap()
            .container
            .as_mut()
            .unwrap()
            .items
            .insert(ids.token, 2);
        let GameplayEvent::ItemTransferred { quantity, .. } = &mut state.receipts[0].events[0]
        else {
            panic!("transfer receipt")
        };
        *quantity = 2;
    });
    assert!(GameSession::resume(document.clone(), game.source_hash(), &forged, false).is_err());
    let forged = rewrite(&saved, |state| {
        state.objects.get_mut(&ids.key_pickup).unwrap().container = Some(ContainerState {
            items: BTreeMap::new(),
            locked: false,
        });
    });
    assert!(GameSession::resume(document, game.source_hash(), &forged, false).is_err());
}

#[test]
fn action_options_offer_one_or_the_fitting_stack_and_legacy_saves_keep_their_shape() {
    let (mut document, ids) = fixture();
    let game = session(document.clone());
    let options = game.container_actions(document.player, ids.chest);
    assert!(options.contains(&transfer(ids, 1, TransferDirection::Take)));
    assert!(options.contains(&transfer(ids, 4, TransferDirection::Take)));
    document
        .objects
        .iter_mut()
        .find(|object| object.id == ids.chest)
        .unwrap()
        .kind = ObjectKind::Prop;
    document.creator = None;
    document.version = 1;
    let legacy = session(document.clone());
    let bytes = legacy.save_bytes().unwrap();
    let json: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
    assert_eq!(json["version"], 1);
    assert!(
        json["state"]["objects"]
            .as_object()
            .unwrap()
            .values()
            .all(|object| object.get("container").is_none())
    );
    GameSession::resume(document, legacy.source_hash(), &bytes, false).unwrap();
}

#[test]
fn presentation_transfer_requests_explain_filter_and_capacity_refusals_atomically() {
    let (document, ids) = fixture();
    let mut definition = document.container_definition(ids.chest).unwrap().clone();
    definition.accepts = vec![ids.token];
    let mut game = session(
        document
            .with_container_definition(ids.chest, definition)
            .unwrap(),
    );
    execute(
        &mut game,
        GameAction::Pickup {
            object: ids.other_pickup,
        },
    );
    let rejected = GameAction::TransferItem {
        object: ids.chest,
        item: ids.other,
        quantity: 2,
        direction: TransferDirection::Put,
    };
    let before = game.state().clone();
    assert!(
        !game
            .container_actions(game.document().player, ids.chest)
            .contains(&rejected)
    );
    assert!(
        game.container_transfer_requests(game.document().player, ids.chest)
            .contains(&rejected)
    );
    assert_eq!(game.state(), &before);
    let error = game
        .execute(&request(&game, rejected), visible())
        .unwrap_err();
    assert!(error.to_string().contains("filter"), "{error}");
    assert_eq!(game.state(), &before);

    let (mut document, ids) = fixture();
    document.actors[0].inventory_capacity = 2;
    let mut game = session(document.clone());
    let oversized = transfer(ids, 4, TransferDirection::Take);
    assert!(
        !game
            .container_actions(document.player, ids.chest)
            .contains(&oversized)
    );
    let options = game.container_transfer_requests(document.player, ids.chest);
    assert!(options.contains(&transfer(ids, 2, TransferDirection::Take)));
    assert!(options.contains(&oversized));
    assert_eq!(
        options
            .iter()
            .filter(|action| **action == oversized)
            .count(),
        1
    );
    let before = game.state().clone();
    let error = game
        .execute(&request(&game, oversized), visible())
        .unwrap_err();
    assert_eq!(error.to_string(), "action refused: inventory full");
    assert_eq!(game.state(), &before);
    let bytes = game.save_bytes().unwrap();
    let resumed = GameSession::resume(document, game.source_hash(), &bytes, false).unwrap();
    assert_eq!(resumed.state(), &before);
}

#[test]
fn quest_consumption_and_unique_reward_join_the_same_saved_item_ledger() {
    let (mut document, ids) = fixture();
    let speaker = EntityId::deterministic("containers/giver");
    let dialogue = EntityId::deterministic("containers/dialogue");
    let node = EntityId::deterministic("containers/node");
    let quest = EntityId::deterministic("containers/quest");
    let accept = EntityId::deterministic("containers/accept");
    let complete = EntityId::deterministic("containers/complete");
    let mut giver = document.actors[0].clone();
    giver.id = speaker;
    giver.name = "Donneur".into();
    giver.role = ActorRole::Friendly;
    giver.dialogue = Some(dialogue);
    document.actors.push(giver);
    document.quests.push(QuestDefinition {
        id: quest,
        name: "Restituer deux jetons".into(),
        giver: speaker,
        required_item: ids.token,
        required_quantity: 2,
        reward_item: Some(ids.other),
        reward_quantity: 1,
    });
    document.dialogues.push(DialogueDefinition {
        id: dialogue,
        start: node,
        nodes: vec![DialogueNode {
            id: node,
            text: "Restitution".into(),
            choices: vec![
                DialogueChoice {
                    id: accept,
                    text: "Accepter".into(),
                    condition: Condition::Quest {
                        quest,
                        status: QuestStatus::Available,
                    },
                    effects: vec![DialogueEffect::AcceptQuest { quest }],
                    next: Some(node),
                },
                DialogueChoice {
                    id: complete,
                    text: "Restituer".into(),
                    condition: Condition::Quest {
                        quest,
                        status: QuestStatus::Active,
                    },
                    effects: vec![DialogueEffect::CompleteQuest { quest }],
                    next: None,
                },
            ],
        }],
    });
    let mut game = session(document.clone());
    execute(&mut game, transfer(ids, 2, TransferDirection::Take));
    execute(
        &mut game,
        GameAction::ChooseDialogue {
            speaker,
            node,
            choice: accept,
        },
    );
    let receipt = execute(
        &mut game,
        GameAction::ChooseDialogue {
            speaker,
            node,
            choice: complete,
        },
    );
    assert_eq!(game.state().quests[&quest], QuestStatus::Completed);
    assert_eq!(
        game.state().actors[&document.player].inventory,
        BTreeMap::from([(ids.other, 1)])
    );
    let bytes = game.save_bytes().unwrap();
    let mut resumed = GameSession::resume(document, game.source_hash(), &bytes, false).unwrap();
    assert_eq!(
        resumed.execute(&receipt.request, visible()).unwrap(),
        receipt
    );
    refused(
        &mut resumed,
        GameAction::ChooseDialogue {
            speaker,
            node,
            choice: complete,
        },
        visible(),
    );
    assert_eq!(resumed.state(), game.state());
}

#[test]
fn physics_events_cannot_forge_an_item_command_or_unlock_a_container() {
    let (document, ids) = fixture();
    let mut game = session(document.clone());
    game.advance_frame(&PhysicsFrame {
        simulation_step_ns: None,
        source_hash: game.source_hash(),
        tick: 1,
        actors: Vec::new(),
        objects: Vec::new(),
        contacts: Vec::new(),
        heat: Vec::new(),
        danger: Vec::new(),
    })
    .unwrap();
    let bytes = game.save_bytes().unwrap();
    let forged = rewrite(&bytes, |state| {
        state
            .last_frame
            .as_mut()
            .unwrap()
            .events
            .push(GameplayEvent::ItemTransferred {
                object: ids.chest,
                actor: document.player,
                item: ids.token,
                quantity: 1,
                direction: TransferDirection::Take,
            });
        state.tick_events_used += 1;
    });
    assert!(GameSession::resume(document, game.source_hash(), &forged, false).is_err());
}

#[test]
fn structural_sources_include_containers_and_do_not_invent_an_unavailable_key() {
    let (document, ids) = fixture();
    let blocked = EntityId::deterministic("containers/source-test-quest");
    let available = document.potential_item_quantities(None, blocked, ids.token, 2);
    assert_eq!(available.get(&ids.token), Some(&4));
    assert!(
        !document
            .potential_item_quantities(Some(ids.chest), blocked, ids.token, 2)
            .contains_key(&ids.token)
    );
    let mut definition = document.container_definition(ids.chest).unwrap().clone();
    definition.key = Some(ids.key);
    let locked = document
        .with_container_definition(ids.chest, definition)
        .unwrap();
    assert_eq!(
        locked
            .potential_item_quantities(None, blocked, ids.token, 2)
            .get(&ids.token),
        Some(&4)
    );
    assert!(
        !locked
            .potential_item_quantities(Some(ids.key_pickup), blocked, ids.token, 2)
            .contains_key(&ids.token)
    );
}

#[test]
fn catalog_edits_share_stack_rules_and_refuse_to_delete_referenced_items() {
    let (source, ids) = fixture();
    let added = ItemDefinition {
        id: EntityId::deterministic("containers/catalog-new"),
        name: "Nouveau".into(),
        max_stack: 4,
    };
    let candidate = source.with_item_definition(added.clone()).unwrap();
    assert!(source.item(added.id).is_none());
    assert_eq!(candidate.objects, source.objects);
    let removed = candidate.without_item_definition(added.id).unwrap();
    assert_eq!(removed, source);
    let mut renamed = source.item(ids.token).unwrap().clone();
    renamed.name = "Jeton renommé".into();
    let renamed_source = source.with_item_definition(renamed.clone()).unwrap();
    assert_eq!(
        renamed_source.functional_hash().unwrap(),
        source.functional_hash().unwrap()
    );
    renamed.max_stack = 3;
    assert!(source.with_item_definition(renamed).is_err());
    assert!(source.without_item_definition(ids.key).is_err());
    assert!(source.without_item_definition(ids.token).is_err());
    let before = source.clone();
    assert!(
        source
            .with_item_definition(ItemDefinition {
                id: source.player,
                name: "Collision".into(),
                max_stack: 1
            })
            .is_err()
    );
    assert_eq!(source, before);
}
