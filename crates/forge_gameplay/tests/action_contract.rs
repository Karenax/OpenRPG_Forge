use forge_core::{AreaId, CommandId, ContentHash, EntityId, ModuleId, Vec3};
use forge_gameplay::{
    ActionObservation, ActorDefinition, ActorRole, GAMEPLAY_SCHEMA, GameAction, GameRequest,
    GameSession, GameplayDocument, GameplayEvent, ItemDefinition, ObjectDefinition, ObjectKind,
    ObjectPhysics, RulesProfile,
};

fn fixture() -> GameplayDocument {
    let player = EntityId::deterministic("action/player");
    let item = EntityId::deterministic("action/item");
    GameplayDocument {
        narrative: None,
        factions: None,
        simulation: None,
        scripting: None,
        cinematics: None,
        creator: None,
        schema: GAMEPLAY_SCHEMA.into(),
        version: 1,
        module_id: ModuleId::deterministic("action/module"),
        area_id: AreaId::deterministic("action/area"),
        profile: RulesProfile {
            id: "forge.gate3-minimal".into(),
            version: 1,
            interaction_range_m: 3.0,
            attack_range_m: 2.0,
            attack_damage: 20,
            attack_stamina_cost: 5,
            max_stamina: 100,
            max_action_receipts: 512,
            max_events_per_tick: 128,
        },
        player,
        actors: vec![ActorDefinition {
            id: player,
            name: "Player".into(),
            role: ActorRole::Player,
            position: Vec3::ZERO,
            max_vitality: 100,
            protection: 0,
            inventory_capacity: 1,
            dialogue: None,
            patrol: vec![],
            flees_danger: false,
        }],
        items: vec![ItemDefinition {
            id: item,
            name: "Item".into(),
            max_stack: 10,
        }],
        objects: vec![ObjectDefinition {
            id: EntityId::deterministic("action/pickup"),
            name: "Pickup".into(),
            position: Vec3::new(1.0, 0.0, 0.0),
            kind: ObjectKind::Pickup { item, quantity: 1 },
            physics: ObjectPhysics::default(),
        }],
        places: vec![],
        dialogues: vec![],
        quests: vec![],
    }
}

fn session() -> GameSession {
    GameSession::new(fixture(), ContentHash::from_bytes([7; 32])).unwrap()
}
fn request(sequence: u64) -> GameRequest {
    GameRequest {
        command_id: CommandId::deterministic("action/pick"),
        expected_sequence: sequence,
        actor: fixture().player,
        action: GameAction::Pickup {
            object: fixture().objects[0].id,
        },
    }
}
fn visible() -> ActionObservation {
    ActionObservation {
        line_of_sight: true,
        confirmed_contact: false,
        water_contact: false,
    }
}

fn creature_fixture(vitality: u32, protection: u32) -> (GameplayDocument, EntityId) {
    let mut document = fixture();
    let target = EntityId::deterministic("action/creature");
    document.actors.push(ActorDefinition {
        id: target,
        name: "Configured creature".into(),
        role: ActorRole::Enemy,
        position: Vec3::new(1.0, 0.0, 0.0),
        max_vitality: vitality,
        protection,
        ..document.actors[0].clone()
    });
    (document, target)
}

fn strike(game: &mut GameSession, target: EntityId, sequence: u64) -> forge_gameplay::GameReceipt {
    game.execute(
        &GameRequest {
            command_id: CommandId::deterministic(format!("action/strike/{sequence}")),
            expected_sequence: sequence,
            actor: game.document().player,
            action: GameAction::Attack { target },
        },
        ActionObservation {
            line_of_sight: true,
            confirmed_contact: true,
            ..Default::default()
        },
    )
    .unwrap()
}

#[test]
fn lethal_attack_on_a_low_vitality_creature_remains_saveable_and_retry_safe() {
    let (document, target) = creature_fixture(7, 0);
    let mut game = GameSession::new(document.clone(), ContentHash::from_bytes([7; 32])).unwrap();
    let receipt = strike(&mut game, target, 0);
    assert_eq!(game.state().actors[&target].vitality, 0);
    assert_eq!(game.state().actors[&document.player].stamina, 95);
    let bytes = game
        .save_bytes()
        .expect("a legitimate lethal hit must remain saveable");
    assert_eq!(
        receipt.events,
        vec![GameplayEvent::Damage {
            target,
            contact: true,
            amount: 7
        }]
    );
    assert_eq!(
        game.execute(&receipt.request, ActionObservation::default())
            .unwrap(),
        receipt
    );
    assert_eq!(game.save_bytes().unwrap(), bytes);
    let resumed = GameSession::resume(document.clone(), game.source_hash(), &bytes, false).unwrap();
    assert_eq!(resumed.state(), game.state());
    assert_eq!(game.document(), &document);
}

#[test]
fn final_hit_records_only_remaining_vitality_after_protection() {
    let (document, target) = creature_fixture(25, 3);
    let mut game = GameSession::new(document.clone(), ContentHash::from_bytes([7; 32])).unwrap();
    assert_eq!(
        strike(&mut game, target, 0).events,
        vec![GameplayEvent::Damage {
            target,
            contact: true,
            amount: 17
        }]
    );
    assert_eq!(game.state().actors[&target].vitality, 8);
    assert_eq!(
        strike(&mut game, target, 1).events,
        vec![GameplayEvent::Damage {
            target,
            contact: true,
            amount: 8
        }]
    );
    assert_eq!(game.state().actors[&target].vitality, 0);
    assert_eq!(game.state().actors[&document.player].stamina, 90);
    let bytes = game.save_bytes().unwrap();
    assert!(GameSession::resume(document, game.source_hash(), &bytes, false).is_ok());
}

#[test]
fn pickup_is_atomic_and_exact_retry_cannot_duplicate_inventory() {
    let mut session = session();
    let before = session.document().clone();
    let request = request(0);
    let first = session.execute(&request, visible()).unwrap();
    assert_eq!(session.execute(&request, visible()).unwrap(), first);
    assert_eq!(session.state().sequence, 1);
    assert_eq!(
        session.state().actors[&before.player].inventory[&before.items[0].id],
        1
    );
    assert!(session.state().objects[&before.objects[0].id].consumed);
    assert_eq!(session.document(), &before);
}

#[test]
fn blocked_or_distant_pickup_leaves_the_entire_state_unchanged() {
    let mut session = session();
    let initial = session.state().clone();
    assert!(
        session
            .execute(&request(0), ActionObservation::default())
            .is_err()
    );
    assert_eq!(session.state(), &initial);
    session
        .observe_position(fixture().player, Vec3::new(20.0, 0.0, 0.0))
        .unwrap();
    let distant = session.state().clone();
    assert!(session.execute(&request(0), visible()).is_err());
    assert_eq!(session.state(), &distant);
}

#[test]
fn reused_command_identity_with_new_parameters_is_refused() {
    let mut session = session();
    session.execute(&request(0), visible()).unwrap();
    let before = session.state().clone();
    let mut changed = request(1);
    changed.action = GameAction::Ignite {
        object: fixture().objects[0].id,
    };
    assert!(session.execute(&changed, visible()).is_err());
    assert_eq!(session.state(), &before);
}

#[test]
fn save_reopens_consumed_item_and_keeps_the_source_immutable() {
    let mut session = session();
    session.execute(&request(0), visible()).unwrap();
    let bytes = session.save_bytes().unwrap();
    let loaded = GameSession::resume(fixture(), session.source_hash(), &bytes, false).unwrap();
    assert_eq!(loaded.state(), session.state());
    assert_eq!(loaded.document(), &fixture());
    assert!(
        GameSession::resume(fixture(), ContentHash::from_bytes([8; 32]), &bytes, false).is_err()
    );
    assert!(
        GameSession::resume(
            fixture(),
            session.source_hash(),
            &bytes[..bytes.len() / 2],
            false
        )
        .is_err()
    );
}
