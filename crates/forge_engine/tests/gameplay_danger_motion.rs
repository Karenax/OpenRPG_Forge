#![cfg(all(not(target_arch = "wasm32"), feature = "physics-rapier"))]

use bevy::{prelude::*, time::TimeUpdateStrategy};
use bevy_rapier3d::prelude::{Collider, NoUserData, RapierPhysicsPlugin, RigidBody, TimestepMode};
use forge_core::{AreaId, CommandId, ContentHash, EntityId, ModuleId, Vec3 as CoreVec3};
use forge_engine::{
    DomainEntityId, PlayerControllerPlugin, PlayerSimulationReady,
    gameplay::{DangerMotionState, GameplayPlugin, GameplayRuntime},
};
use forge_gameplay::{
    ActorDefinition, ActorRole, GAMEPLAY_SCHEMA, GAMEPLAY_VERSION, GameAction, GameRequest,
    GameplayDocument, ObjectDefinition, ObjectKind, ObjectPhysics, RulesProfile,
};
use forge_world::VisualValleyPlayerControllerRecipe;
use std::time::{Duration, Instant};

#[derive(Clone, Copy)]
struct Ids {
    player: EntityId,
    enemy: EntityId,
    fire: EntityId,
}

fn id(name: &str) -> EntityId {
    EntityId::deterministic(format!("gameplay-danger-motion/{name}"))
}

fn actor(
    id: EntityId,
    name: &str,
    role: ActorRole,
    position: CoreVec3,
    flees_danger: bool,
) -> ActorDefinition {
    ActorDefinition {
        id,
        name: name.into(),
        role,
        position,
        max_vitality: 100,
        protection: 0,
        inventory_capacity: 8,
        dialogue: None,
        patrol: Vec::new(),
        flees_danger,
    }
}

fn document() -> (GameplayDocument, Ids) {
    let ids = Ids {
        player: id("player"),
        enemy: id("enemy"),
        fire: id("fire"),
    };
    let document = GameplayDocument {
        narrative: None,
        factions: None,
        simulation: None,
        scripting: None,
        cinematics: None,
        creator: None,
        schema: GAMEPLAY_SCHEMA.into(),
        version: GAMEPLAY_VERSION,
        module_id: ModuleId::deterministic("gameplay-danger-motion/module"),
        area_id: AreaId::deterministic("gameplay-danger-motion/area"),
        profile: RulesProfile {
            id: "forge.gate3-danger-motion".into(),
            version: 1,
            interaction_range_m: 3.0,
            attack_range_m: 2.0,
            attack_damage: 20,
            attack_stamina_cost: 5,
            max_stamina: 100,
            max_action_receipts: 128,
            max_events_per_tick: 64,
        },
        player: ids.player,
        actors: vec![
            actor(
                ids.player,
                "Player",
                ActorRole::Player,
                CoreVec3::new(-2.0, 0.95, 0.0),
                false,
            ),
            actor(
                ids.enemy,
                "Danger target",
                ActorRole::Enemy,
                CoreVec3::new(2.0, 0.95, 0.0),
                true,
            ),
        ],
        items: Vec::new(),
        objects: vec![ObjectDefinition {
            id: ids.fire,
            name: "Observed heat source".into(),
            position: CoreVec3::new(0.0, 0.5, 0.0),
            kind: ObjectKind::Prop,
            physics: ObjectPhysics {
                body_half_extents_m: CoreVec3::new(0.25, 0.5, 0.25),
                flammable: true,
                // This test isolates motion/contact/save behavior. Keep a
                // visible burning danger while actors survive the wall probe;
                // heat damage is covered by the environmental contract tests.
                heat_damage_per_tick: 0,
                ..ObjectPhysics::default()
            },
        }],
        places: Vec::new(),
        dialogues: Vec::new(),
        quests: Vec::new(),
    };
    (document, ids)
}

fn app(document: GameplayDocument, source_hash: ContentHash) -> (App, Ids, Entity) {
    let ids = ids_for(&document);
    let profile = VisualValleyPlayerControllerRecipe.build().unwrap();
    let player_position = document.actor(document.player).unwrap().position;
    let runtime = GameplayRuntime::new(document, source_hash, None, false).unwrap();
    let mut app = App::new();
    app.add_plugins(MinimalPlugins)
        .add_plugins(TransformPlugin)
        .register_required_components::<Mesh3d, Visibility>()
        .insert_resource(Time::<Fixed>::from_hz(60.0))
        .insert_resource(TimeUpdateStrategy::ManualDuration(Duration::from_secs_f64(
            1.0 / 60.0,
        )))
        .insert_resource(TimestepMode::Fixed {
            dt: 1.0 / 60.0,
            substeps: 1,
        })
        .add_plugins(RapierPhysicsPlugin::<NoUserData>::default().in_fixed_schedule())
        .init_resource::<Assets<Mesh>>()
        .init_resource::<Assets<StandardMaterial>>()
        .add_plugins(
            PlayerControllerPlugin::new(
                profile,
                Vec3::new(player_position.x, player_position.y, player_position.z),
            )
            .with_interactive_input(false),
        )
        .insert_resource(runtime)
        .insert_resource(PlayerSimulationReady(true))
        .add_plugins(GameplayPlugin);
    app.world_mut().spawn((
        RigidBody::Fixed,
        Collider::cuboid(20.0, 0.1, 20.0),
        Transform::from_xyz(0.0, -0.1, 0.0),
    ));
    // The wall is beyond the enemy in the direction away from the heat source.
    // It lies inside the three-metre danger range. Navigation must protect
    // its volume while finding a physical route away from the visible fire.
    let wall = app
        .world_mut()
        .spawn((
            RigidBody::Fixed,
            Collider::cuboid(0.1, 1.0, 2.0),
            Transform::from_xyz(2.7, 1.0, 0.0),
        ))
        .id();
    app.world_mut()
        .resource_mut::<GameplayRuntime>()
        .set_enabled(true);
    (app, ids, wall)
}

fn ids_for(document: &GameplayDocument) -> Ids {
    let player = document.player;
    let enemy = document
        .actors
        .iter()
        .find(|actor| actor.role == ActorRole::Enemy)
        .map(|actor| actor.id)
        .expect("enemy actor");
    let fire = document.objects.first().expect("fire object").id;
    Ids {
        player,
        enemy,
        fire,
    }
}

fn entity_for(app: &mut App, id: EntityId) -> Entity {
    let world = app.world_mut();
    let mut query = world.query::<(Entity, &DomainEntityId)>();
    query
        .iter(world)
        .find_map(|(entity, domain)| (domain.0 == id).then_some(entity))
        .expect("projected gameplay entity")
}

#[derive(Default)]
struct EscapeWitness {
    saw_fleeing: bool,
    maximum_x: f32,
    maximum_lateral_m: f32,
    current_grounded_motion: bool,
}

fn assert_outside_wall(app: &App, enemy: Entity, wall: Entity) {
    let world = app.world();
    let position = world.get::<Transform>(enemy).unwrap().translation;
    let wall_position = world.get::<Transform>(wall).unwrap().translation;
    let extents = world
        .get::<Collider>(wall)
        .unwrap()
        .as_cuboid()
        .unwrap()
        .half_extents();
    let radius = world
        .get::<Collider>(enemy)
        .unwrap()
        .as_capsule()
        .unwrap()
        .radius();
    // This upright capsule and identity-rotated wall overlap vertically on
    // the flat fixture floor. Distance to the XZ rectangle also handles its
    // rounded clearance at corners, without demanding a needless wall hit.
    let relative = position - wall_position;
    let clearance = Vec2::new(
        (relative.x.abs() - extents.x).max(0.0),
        (relative.z.abs() - extents.z).max(0.0),
    )
    .length();
    assert!(
        clearance + 0.001 >= radius,
        "NPC penetrated the installed blocking wall: pose={position:?}, clearance={clearance}, radius={radius}"
    );
}

fn observe_escape(
    app: &mut App,
    ids: Ids,
    enemy: Entity,
    wall: Entity,
    initial: Vec3,
) -> EscapeWitness {
    let fire = entity_for(app, ids.fire);
    let deadline = Instant::now() + Duration::from_secs(30);
    let mut witness = EscapeWitness {
        maximum_x: initial.x,
        ..default()
    };
    while Instant::now() < deadline {
        app.update();
        assert_outside_wall(app, enemy, wall);
        let world = app.world();
        let runtime = world.resource::<GameplayRuntime>();
        let position = world.get::<Transform>(enemy).unwrap().translation;
        let fire_position = world.get::<Transform>(fire).unwrap().translation;
        let fleeing = runtime.session().state().actors[&ids.enemy].fleeing;
        let motion = world.get::<DangerMotionState>(enemy).unwrap();
        witness.saw_fleeing |= fleeing;
        witness.maximum_x = witness.maximum_x.max(position.x);
        witness.maximum_lateral_m = witness
            .maximum_lateral_m
            .max((position.z - initial.z).abs());
        witness.current_grounded_motion |= motion.feedback_current
            && motion.grounded
            && Vec2::new(
                motion.effective_translation.x,
                motion.effective_translation.z,
            )
            .length()
                > f32::EPSILON;
        if witness.saw_fleeing
            && !fleeing
            && position.distance(fire_position)
                > runtime.session().document().profile.interaction_range_m
        {
            return witness;
        }
        // Native generation is asynchronous; fixed simulation frames alone
        // must not substitute for a bounded real opportunity to complete it.
        std::thread::sleep(Duration::from_millis(1));
    }
    panic!("NPC did not physically escape the observed danger within the bounded window");
}

#[test]
fn danger_motion_uses_real_los_kcc_obstacle_and_saves_actor_pose() {
    let (document, _) = document();
    let source_hash = ContentHash::from_bytes([0x71; 32]);
    let (mut app, ids, wall) = app(document.clone(), source_hash);

    // Ignite through the same queued command path used by the HUD. Actual
    // Rapier LOS/range observations then make the domain report fleeing.
    for _ in 0..120 {
        app.update();
    }
    app.world_mut()
        .resource_mut::<GameplayRuntime>()
        .queue(GameRequest {
            command_id: CommandId::deterministic("gameplay-danger-motion/ignite"),
            expected_sequence: 0,
            actor: ids.player,
            action: GameAction::Ignite { object: ids.fire },
        })
        .expect("queue ignition");

    let enemy = entity_for(&mut app, ids.enemy);
    let initial = app.world().get::<Transform>(enemy).unwrap().translation;
    let witness = observe_escape(&mut app, ids, enemy, wall, initial);
    // Allow fresh stopped KCC feedback after the domain has left danger.
    for _ in 0..8 {
        app.update();
        assert_outside_wall(&app, enemy, wall);
    }
    let world = app.world();
    let runtime = world.resource::<GameplayRuntime>();
    let final_position = world.get::<Transform>(enemy).unwrap().translation;
    let motion = world.get::<DangerMotionState>(enemy).unwrap();
    assert!(
        runtime.error().is_none(),
        "runtime error: {:?}",
        runtime.error()
    );
    assert!(
        witness.saw_fleeing,
        "Rapier danger was never observed by the domain"
    );
    assert!(
        witness.maximum_x > initial.x + 0.05,
        "NPC never advanced away from danger"
    );
    assert!(
        witness.maximum_lateral_m > 0.05,
        "NPC did not take a lateral route beside the wall"
    );
    assert!(
        witness.current_grounded_motion,
        "NPC never reported current grounded physical movement"
    );
    assert!(
        motion.feedback_current && motion.grounded,
        "NPC lost current grounded KCC feedback"
    );
    assert!(
        Vec2::new(
            motion.effective_translation.x,
            motion.effective_translation.z
        )
        .length()
            <= f32::EPSILON
    );
    let saved_actor = &runtime.session().state().actors[&ids.enemy];
    assert!(
        !saved_actor.fleeing,
        "NPC kept fleeing after leaving the observed danger range"
    );
    let saved_position = Vec3::new(
        saved_actor.position.x,
        saved_actor.position.y,
        saved_actor.position.z,
    );
    let saved_fleeing = saved_actor.fleeing;
    assert!(saved_position.distance(final_position) < 0.02);
    let save = runtime.save_bytes().expect("stable gameplay saves");
    let resumed = GameplayRuntime::new(document, source_hash, Some(&save), false)
        .expect("save resumes in a new runtime");
    let resumed_actor = &resumed.session().state().actors[&ids.enemy];
    let resumed_position = Vec3::new(
        resumed_actor.position.x,
        resumed_actor.position.y,
        resumed_actor.position.z,
    );
    assert!(resumed_position.distance(saved_position) < 0.02);
    assert_eq!(resumed_actor.fleeing, saved_fleeing);
}

#[test]
fn npc_mesh_capsule_profile_is_one_point_nine_metres() {
    let (document, ids) = document();
    let source_hash = ContentHash::from_bytes([0x72; 32]);
    let (mut app, _, _) = app(document, source_hash);
    app.update();
    let entity = entity_for(&mut app, ids.enemy);
    let collider = app.world().get::<Collider>(entity).expect("NPC collider");
    let capsule = collider.as_capsule().expect("NPC capsule collider");
    // Rapier's segment and radius are f32 values. Allow ten micrometres for
    // their rounded sum while still detecting a mismatched capsule profile.
    assert!((capsule.height() + 2.0 * capsule.radius() - 1.9).abs() < 0.00001);
    assert!((capsule.radius() - 0.35).abs() < f32::EPSILON);
    let mesh_handle = &app
        .world()
        .get::<Mesh3d>(entity)
        .expect("NPC mesh handle")
        .0;
    let mesh = app
        .world()
        .resource::<Assets<Mesh>>()
        .get(mesh_handle)
        .expect("NPC mesh asset");
    let positions = mesh
        .attribute(Mesh::ATTRIBUTE_POSITION)
        .and_then(|attribute| attribute.as_float3())
        .expect("NPC mesh positions");
    let (bottom, top) = positions.iter().fold(
        (f32::INFINITY, f32::NEG_INFINITY),
        |(bottom, top), position| (bottom.min(position[1]), top.max(position[1])),
    );
    assert!((top - bottom - 1.9).abs() < 0.00001);
}
