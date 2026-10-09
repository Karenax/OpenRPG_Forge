use forge_core::{
    AreaId, CommandId, ContentHash, EntityId, ModuleId, Vec3, canonical_json_bytes,
    canonical_json_sha256,
};
use forge_gameplay::*;
use serde::{Deserialize, Serialize};

#[derive(Clone, Copy)]
struct FixtureIds {
    player: EntityId,
    intendant: EntityId,
    guard: EntityId,
    seal_item: EntityId,
    reward_item: EntityId,
    filler_item: EntityId,
    seal_object: EntityId,
    filler_object: EntityId,
    door: EntityId,
    support: EntityId,
    charge: EntityId,
    dialogue: EntityId,
    intro_node: EntityId,
    active_node: EntityId,
    accept_choice: EntityId,
    turn_in_choice: EntityId,
    quest: EntityId,
    village: EntityId,
    forest: EntityId,
    reserve: EntityId,
}

fn id(key: &str) -> EntityId {
    EntityId::deterministic(format!("first-loop/{key}"))
}

fn fixture() -> (GameplayDocument, FixtureIds) {
    let ids = fixture_ids();
    let document = GameplayDocument {
        schema: GAMEPLAY_SCHEMA.into(),
        version: GAMEPLAY_VERSION,
        module_id: ModuleId::deterministic("first-loop/module"),
        area_id: AreaId::deterministic("first-loop/tidewater"),
        profile: fixture_profile(),
        player: ids.player,
        actors: fixture_actors(&ids),
        items: fixture_items(&ids),
        objects: fixture_objects(&ids),
        places: fixture_places(&ids),
        dialogues: fixture_dialogues(&ids),
        quests: fixture_quests(&ids),
    };
    (document, ids)
}

fn fixture_ids() -> FixtureIds {
    FixtureIds {
        player: id("actor/player"),
        intendant: id("actor/intendant"),
        guard: id("actor/guard"),
        seal_item: id("item/seal"),
        reward_item: id("item/reward"),
        filler_item: id("item/filler"),
        seal_object: id("object/seal"),
        filler_object: id("object/filler"),
        door: id("object/reserve-door"),
        support: id("object/charge-support"),
        charge: id("object/stone-charge"),
        dialogue: id("dialogue/intendant"),
        intro_node: id("dialogue/intendant/intro"),
        active_node: id("dialogue/intendant/active"),
        accept_choice: id("dialogue/intendant/accept"),
        turn_in_choice: id("dialogue/intendant/turn-in"),
        quest: id("quest/seal"),
        village: id("place/village"),
        forest: id("place/forest"),
        reserve: id("place/reserve"),
    }
}

fn fixture_profile() -> RulesProfile {
    RulesProfile {
        id: "forge.gate3-minimal".into(),
        version: 1,
        interaction_range_m: 3.0,
        attack_range_m: 2.0,
        attack_damage: 20,
        attack_stamina_cost: 5,
        max_stamina: 100,
        max_action_receipts: 512,
        max_events_per_tick: 128,
    }
}

fn fixture_actors(ids: &FixtureIds) -> Vec<ActorDefinition> {
    vec![
        ActorDefinition {
            id: ids.player,
            name: "Player".into(),
            role: ActorRole::Player,
            position: Vec3::ZERO,
            max_vitality: 100,
            protection: 0,
            inventory_capacity: 1,
            dialogue: None,
            patrol: vec![],
            flees_danger: false,
        },
        ActorDefinition {
            id: ids.intendant,
            name: "Intendant".into(),
            role: ActorRole::Friendly,
            position: Vec3::new(1.0, 0.0, 0.0),
            max_vitality: 100,
            protection: 0,
            inventory_capacity: 4,
            dialogue: Some(ids.dialogue),
            patrol: vec![Vec3::new(1.0, 0.0, 0.0)],
            flees_danger: false,
        },
        ActorDefinition {
            id: ids.guard,
            name: "Reserve guard".into(),
            role: ActorRole::Enemy,
            position: Vec3::new(2.0, 0.0, 0.0),
            max_vitality: 80,
            protection: 2,
            inventory_capacity: 2,
            dialogue: None,
            patrol: vec![Vec3::new(2.0, 0.0, 0.0)],
            flees_danger: true,
        },
    ]
}

fn fixture_items(ids: &FixtureIds) -> Vec<ItemDefinition> {
    vec![
        ItemDefinition {
            id: ids.seal_item,
            name: "Quest seal".into(),
            max_stack: 2,
        },
        ItemDefinition {
            id: ids.reward_item,
            name: "Intendant's token".into(),
            max_stack: 1,
        },
        ItemDefinition {
            id: ids.filler_item,
            name: "Unrelated trinket".into(),
            max_stack: 1,
        },
    ]
}

fn fixture_objects(ids: &FixtureIds) -> Vec<ObjectDefinition> {
    vec![
        ObjectDefinition {
            id: ids.seal_object,
            name: "Seal in the reserve".into(),
            position: Vec3::new(1.0, 0.0, 0.0),
            kind: ObjectKind::Pickup {
                item: ids.seal_item,
                quantity: 1,
            },
            physics: ObjectPhysics::default(),
        },
        ObjectDefinition {
            id: ids.filler_object,
            name: "Trinket".into(),
            position: Vec3::new(1.0, 0.0, 0.0),
            kind: ObjectKind::Pickup {
                item: ids.filler_item,
                quantity: 1,
            },
            physics: ObjectPhysics::default(),
        },
        ObjectDefinition {
            id: ids.door,
            name: "Reserve door".into(),
            position: Vec3::new(2.0, 0.0, 0.0),
            kind: ObjectKind::Door {
                initially_open: false,
            },
            physics: ObjectPhysics::default(),
        },
        ObjectDefinition {
            id: ids.support,
            name: "Charge support".into(),
            position: Vec3::new(2.0, 0.0, 0.0),
            kind: ObjectKind::Prop,
            physics: ObjectPhysics {
                mass_kg: 10.0,
                body_half_extents_m: Vec3::new(0.5, 0.5, 0.5),
                pushable: true,
                break_energy_j: Some(5.0),
                supports: vec![ids.charge],
                ..ObjectPhysics::default()
            },
        },
        ObjectDefinition {
            id: ids.charge,
            name: "Stone charge".into(),
            position: Vec3::new(2.0, 1.0, 0.0),
            kind: ObjectKind::Prop,
            physics: ObjectPhysics {
                mass_kg: 4.0,
                body_half_extents_m: Vec3::new(0.5, 0.5, 0.5),
                throwable: true,
                ..ObjectPhysics::default()
            },
        },
    ]
}

fn fixture_places(ids: &FixtureIds) -> Vec<PlaceDefinition> {
    vec![
        PlaceDefinition {
            id: ids.village,
            name: "Village".into(),
            position: Vec3::ZERO,
            links: vec![ids.forest],
        },
        PlaceDefinition {
            id: ids.forest,
            name: "Forest".into(),
            position: Vec3::new(10.0, 0.0, 0.0),
            links: vec![ids.village, ids.reserve],
        },
        PlaceDefinition {
            id: ids.reserve,
            name: "Reserve".into(),
            position: Vec3::new(20.0, 0.0, 0.0),
            links: vec![ids.forest],
        },
    ]
}

fn fixture_dialogues(ids: &FixtureIds) -> Vec<DialogueDefinition> {
    vec![DialogueDefinition {
        id: ids.dialogue,
        start: ids.intro_node,
        nodes: vec![
            DialogueNode {
                id: ids.intro_node,
                text: "The seal is in the reserve beyond the forest.".into(),
                choices: vec![DialogueChoice {
                    id: ids.accept_choice,
                    text: "I will retrieve it.".into(),
                    condition: Condition::Quest {
                        quest: ids.quest,
                        status: QuestStatus::Available,
                    },
                    effects: vec![DialogueEffect::AcceptQuest { quest: ids.quest }],
                    next: Some(ids.active_node),
                }],
            },
            DialogueNode {
                id: ids.active_node,
                text: "Bring the seal back to me.".into(),
                choices: vec![DialogueChoice {
                    id: ids.turn_in_choice,
                    text: "Here is the seal.".into(),
                    condition: Condition::HasItem {
                        item: ids.seal_item,
                        quantity: 1,
                    },
                    effects: vec![DialogueEffect::CompleteQuest { quest: ids.quest }],
                    next: None,
                }],
            },
        ],
    }]
}

fn fixture_quests(ids: &FixtureIds) -> Vec<QuestDefinition> {
    vec![QuestDefinition {
        id: ids.quest,
        name: "Recover the seal".into(),
        giver: ids.intendant,
        required_item: ids.seal_item,
        required_quantity: 1,
        reward_item: Some(ids.reward_item),
        reward_quantity: 1,
    }]
}

fn request(label: &str, sequence: u64, actor: EntityId, action: GameAction) -> GameRequest {
    GameRequest {
        command_id: CommandId::deterministic(format!("first-loop/command/{label}")),
        expected_sequence: sequence,
        actor,
        action,
    }
}

fn visible() -> ActionObservation {
    ActionObservation {
        line_of_sight: true,
        confirmed_contact: true,
        water_contact: false,
    }
}

fn resume_error(
    document: &GameplayDocument,
    source_hash: ContentHash,
    bytes: &[u8],
    compatible: bool,
    message: &str,
) -> GameplayError {
    match GameSession::resume(document.clone(), source_hash, bytes, compatible) {
        Err(error) => error,
        Ok(_) => panic!("{message}"),
    }
}

fn accept_quest(session: &mut GameSession, ids: &FixtureIds) {
    let view = session.dialogue_view(ids.player, ids.intendant).unwrap();
    assert_eq!(view.node, ids.intro_node);
    assert!(
        view.choices
            .iter()
            .any(|choice| choice.id == ids.accept_choice)
    );
    session
        .execute(
            &request(
                "accept",
                0,
                ids.player,
                GameAction::ChooseDialogue {
                    speaker: ids.intendant,
                    node: ids.intro_node,
                    choice: ids.accept_choice,
                },
            ),
            visible(),
        )
        .unwrap();
}

fn pickup_seal(session: &mut GameSession, ids: &FixtureIds, label: &str) {
    session
        .execute(
            &request(
                label,
                session.state().sequence,
                ids.player,
                GameAction::Pickup {
                    object: ids.seal_object,
                },
            ),
            visible(),
        )
        .unwrap();
}

#[test]
fn first_loop_accepts_quest_picks_up_seal_and_turns_it_in() {
    let (document, ids) = fixture();
    let mut session = GameSession::new(document, ContentHash::from_bytes([1; 32])).unwrap();

    accept_quest(&mut session, &ids);
    pickup_seal(&mut session, &ids, "pickup");
    let view = session.dialogue_view(ids.player, ids.intendant).unwrap();
    assert_eq!(view.node, ids.active_node);
    assert!(
        view.choices
            .iter()
            .any(|choice| choice.id == ids.turn_in_choice)
    );

    session
        .execute(
            &request(
                "turn-in",
                2,
                ids.player,
                GameAction::ChooseDialogue {
                    speaker: ids.intendant,
                    node: ids.active_node,
                    choice: ids.turn_in_choice,
                },
            ),
            visible(),
        )
        .unwrap();

    let state = session.state();
    assert_eq!(state.quests[&ids.quest], QuestStatus::Completed);
    assert!(
        !state.actors[&ids.player]
            .inventory
            .contains_key(&ids.seal_item)
    );
    assert_eq!(state.actors[&ids.player].inventory[&ids.reward_item], 1);
}

#[test]
fn exact_turn_in_retry_is_idempotent_and_cannot_duplicate_reward() {
    let (document, ids) = fixture();
    let mut session = GameSession::new(document, ContentHash::from_bytes([2; 32])).unwrap();
    accept_quest(&mut session, &ids);
    pickup_seal(&mut session, &ids, "pickup-retry");
    let turn_in = request(
        "turn-in-retry",
        2,
        ids.player,
        GameAction::ChooseDialogue {
            speaker: ids.intendant,
            node: ids.active_node,
            choice: ids.turn_in_choice,
        },
    );

    let first = session.execute(&turn_in, visible()).unwrap();
    let after_first = session.state().clone();
    let retry = session.execute(&turn_in, visible()).unwrap();

    assert_eq!(retry, first);
    assert_eq!(session.state(), &after_first);
    assert_eq!(
        session.state().actors[&ids.player].inventory[&ids.reward_item],
        1
    );
}

#[test]
fn exhausted_stamina_refuses_attack_atomically_and_survives_resume() {
    let (mut document, ids) = fixture();
    document.profile.max_stamina = document.profile.attack_stamina_cost;
    let original = document.clone();
    let source_hash = ContentHash::from_bytes([19; 32]);
    let mut session = GameSession::new(document, source_hash).unwrap();
    session
        .execute(
            &request(
                "last-stamina",
                0,
                ids.player,
                GameAction::Attack { target: ids.guard },
            ),
            visible(),
        )
        .unwrap();
    assert_eq!(session.state().actors[&ids.player].stamina, 0);
    assert_eq!(session.state().actors[&ids.guard].vitality, 62);
    let exhausted_state = session.state().clone();
    let refused_attack = request(
        "exhausted-attack",
        1,
        ids.player,
        GameAction::Attack { target: ids.guard },
    );
    let error = session.execute(&refused_attack, visible()).unwrap_err();
    assert!(matches!(error, GameplayError::ActionRefused(_)));
    assert_eq!(session.state(), &exhausted_state);
    assert_eq!(session.document(), &original);

    let bytes = session.save_bytes().unwrap();
    let mut resumed = GameSession::resume(original.clone(), source_hash, &bytes, false).unwrap();
    assert_eq!(resumed.state(), &exhausted_state);
    let error = resumed.execute(&refused_attack, visible()).unwrap_err();
    assert!(matches!(error, GameplayError::ActionRefused(_)));
    assert_eq!(resumed.state(), &exhausted_state);
    assert_eq!(resumed.save_bytes().unwrap(), bytes);
    assert_eq!(resumed.document(), &original);
}

#[test]
fn full_inventory_rolls_back_pickup_and_keeps_the_quest_object() {
    let (document, ids) = fixture();
    let original = document.clone();
    let mut session = GameSession::new(document, ContentHash::from_bytes([3; 32])).unwrap();
    session
        .execute(
            &request(
                "fill-inventory",
                0,
                ids.player,
                GameAction::Pickup {
                    object: ids.filler_object,
                },
            ),
            visible(),
        )
        .unwrap();
    let before_failed_pickup = session.state().clone();

    let error = session
        .execute(
            &request(
                "pickup-full",
                1,
                ids.player,
                GameAction::Pickup {
                    object: ids.seal_object,
                },
            ),
            visible(),
        )
        .expect_err("full inventory must refuse the pickup");

    assert!(matches!(error, GameplayError::ActionRefused(_)));
    assert_eq!(session.state(), &before_failed_pickup);
    assert!(!session.state().objects[&ids.seal_object].consumed);
    assert_eq!(session.document(), &original);
}

#[test]
fn save_resume_uses_a_new_session_and_keeps_authored_source_immutable() {
    let (document, ids) = fixture();
    let original = document.clone();
    let source_hash = ContentHash::from_bytes([4; 32]);
    let mut session = GameSession::new(document, source_hash).unwrap();
    accept_quest(&mut session, &ids);
    session
        .observe_position(ids.player, Vec3::new(0.5, 0.0, 0.0))
        .unwrap();
    assert_eq!(session.document(), &original);

    let bytes = session.save_bytes().unwrap();
    let resumed = GameSession::resume(original.clone(), source_hash, &bytes, false).unwrap();

    assert_eq!(resumed.source_hash(), source_hash);
    assert_eq!(resumed.document(), &original);
    assert_eq!(resumed.state().quests[&ids.quest], QuestStatus::Active);
    assert_eq!(
        resumed.state().actors[&ids.player].position,
        Vec3::new(0.5, 0.0, 0.0)
    );
    assert_eq!(session.document(), &original);
}

#[test]
fn text_and_door_pose_edits_require_explicit_compatibility_acceptance() {
    let (document, ids) = fixture();
    let source_hash = ContentHash::from_bytes([5; 32]);
    let mut session = GameSession::new(document.clone(), source_hash).unwrap();
    session
        .execute(
            &request(
                "open-door",
                0,
                ids.player,
                GameAction::SetDoor {
                    object: ids.door,
                    open: true,
                },
            ),
            visible(),
        )
        .unwrap();
    let bytes = session.save_bytes().unwrap();

    let mut edited = document.clone();
    edited.dialogues[0].nodes[0].text = "The revised seal instructions.".into();
    edited
        .objects
        .iter_mut()
        .find(|object| object.id == ids.door)
        .unwrap()
        .position = Vec3::new(4.0, 0.0, 0.0);
    let edited_hash = ContentHash::from_bytes([6; 32]);

    let refused = resume_error(
        &edited,
        edited_hash,
        &bytes,
        false,
        "source revision changes require explicit acceptance",
    );
    assert!(matches!(refused, GameplayError::IncompatibleSave(_)));

    let resumed = GameSession::resume(edited.clone(), edited_hash, &bytes, true).unwrap();
    assert_eq!(resumed.source_hash(), edited_hash);
    assert_eq!(resumed.document(), &edited);
    assert!(resumed.state().objects[&ids.door].open);
    assert_eq!(
        resumed.state().objects[&ids.door].position,
        Vec3::new(4.0, 0.0, 0.0)
    );
    assert_ne!(session.document(), &edited);
}

#[derive(Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct SaveEnvelope {
    schema: String,
    version: u32,
    module_id: ModuleId,
    area_id: AreaId,
    source_hash: ContentHash,
    functional_hash: ContentHash,
    state_hash: ContentHash,
    state: GameState,
}

#[test]
fn removed_references_and_functional_changes_are_rejected() {
    let (document, ids) = fixture();
    let source_hash = ContentHash::from_bytes([7; 32]);
    let session = GameSession::new(document.clone(), source_hash).unwrap();
    let bytes = session.save_bytes().unwrap();

    let mut removed = document.clone();
    removed.items.retain(|item| item.id != ids.seal_item);
    let missing = resume_error(
        &removed,
        source_hash,
        &bytes,
        true,
        "removing a referenced quest item must be rejected",
    );
    assert!(matches!(missing, GameplayError::MissingReference(_)));

    let mut incompatible = document;
    incompatible.quests[0].required_quantity = 2;
    let changed = resume_error(
        &incompatible,
        source_hash,
        &bytes,
        true,
        "functional gameplay changes must be rejected",
    );
    assert!(matches!(changed, GameplayError::IncompatibleSave(_)));
}

#[test]
fn resume_rejects_a_save_with_an_unsettled_physical_object() {
    let (document, ids) = fixture();
    let source_hash = ContentHash::from_bytes([8; 32]);
    let session = GameSession::new(document.clone(), source_hash).unwrap();
    let stable_bytes = session.save_bytes().unwrap();
    let mut envelope: SaveEnvelope = serde_json::from_slice(&stable_bytes).unwrap();
    envelope
        .state
        .objects
        .get_mut(&ids.support)
        .unwrap()
        .in_motion = true;
    envelope.state_hash = canonical_json_sha256(&envelope.state).unwrap();
    let unsettled_bytes = canonical_json_bytes(&envelope).unwrap();

    let error = resume_error(
        &document,
        source_hash,
        &unsettled_bytes,
        false,
        "an unsettled object must not be resumed",
    );
    assert!(matches!(error, GameplayError::IncompatibleSave(_)));
}
