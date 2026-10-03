use forge_core::{AreaId, EntityId, ModuleId, Vec3};
use forge_gameplay::{
    ActorDefinition, ActorRole, GAMEPLAY_SCHEMA, GAMEPLAY_VERSION, GameplayDocument,
    ItemDefinition, ObjectDefinition, ObjectKind, ObjectPhysics, RulesProfile,
};

fn valid_document() -> GameplayDocument {
    let player = EntityId::deterministic("contract/player");
    GameplayDocument {
        schema: GAMEPLAY_SCHEMA.into(),
        version: GAMEPLAY_VERSION,
        module_id: ModuleId::deterministic("contract"),
        area_id: AreaId::deterministic("contract/area"),
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
            inventory_capacity: 12,
            dialogue: None,
            patrol: vec![],
            flees_danger: false,
        }],
        items: vec![],
        objects: vec![],
        places: vec![],
        dialogues: vec![],
        quests: vec![],
    }
}

#[test]
fn valid_authored_document_round_trips_without_engine_handles() {
    let document = valid_document();
    assert!(document.validate().is_ok());
    let bytes = serde_json::to_vec(&document).unwrap();
    let decoded: GameplayDocument = serde_json::from_slice(&bytes).unwrap();
    assert_eq!(decoded, document);
    assert!(decoded.validate().is_ok());
}

#[test]
fn duplicate_actor_and_cross_category_ids_are_rejected() {
    let mut document = valid_document();
    document.actors.push(document.actors[0].clone());
    assert!(document.validate().is_err());
    document.actors.pop();
    document.items.push(ItemDefinition {
        id: document.player,
        name: "Duplicate".into(),
        max_stack: 1,
    });
    assert!(document.validate().is_err());
}

#[test]
fn nonexistent_dialogue_is_an_actionable_reference_error() {
    let mut document = valid_document();
    let missing = EntityId::deterministic("contract/missing-dialogue");
    document.actors[0].dialogue = Some(missing);
    let error = document.validate().unwrap_err().to_string();
    assert!(error.contains(&missing.to_string()), "{error}");
}

#[test]
fn nonfinite_positions_and_unbounded_profile_are_refused() {
    let mut document = valid_document();
    document.actors[0].position.x = f32::NAN;
    assert!(document.validate().is_err());
    document.actors[0].position = Vec3::ZERO;
    document.profile.max_events_per_tick = usize::MAX;
    assert!(document.validate().is_err());
}

#[test]
fn invalid_pickup_quantity_is_not_promoted_to_runtime() {
    let mut document = valid_document();
    let item = EntityId::deterministic("contract/item");
    document.items.push(ItemDefinition {
        id: item,
        name: "Item".into(),
        max_stack: 1,
    });
    document.objects.push(ObjectDefinition {
        id: EntityId::deterministic("contract/pickup"),
        name: "Pickup".into(),
        position: Vec3::ZERO,
        kind: ObjectKind::Pickup { item, quantity: 0 },
        physics: ObjectPhysics::default(),
    });
    assert!(document.validate().is_err());
}
