#![cfg(all(not(target_arch = "wasm32"), feature = "physics-rapier"))]

use bevy::{prelude::*, time::TimeUpdateStrategy};
use bevy_rapier3d::prelude::{
    ActiveCollisionTypes, Collider, ColliderDisabled, CollisionEvent, ContactSkin, GravityScale,
    NoUserData, RapierPhysicsPlugin, RigidBody, Sleeping, TimestepMode, Velocity,
};
use forge_core::{CommandId, ContentHash, EntityId, Vec3 as CoreVec3};
use forge_engine::{
    DomainEntityId, ForgePlayerCharacter, PendingPlayerControlCommand, PlayerControlCommand,
    PlayerControllerPlugin, PlayerControllerSet, PlayerRuntimeSnapshot, PlayerSimulationReady,
    gameplay::{GameplayPlugin, GameplayRuntime, GameplaySet},
};
use forge_gameplay::{
    ActorDefinition, ActorRole, GAMEPLAY_SCHEMA, GAMEPLAY_VERSION, GameAction, GameRequest,
    GameplayDocument, GameplayEvent, ObjectDefinition, ObjectKind, ObjectPhysics, RulesProfile,
};
use forge_world::VisualValleyPlayerControllerRecipe;
use std::time::Duration;

#[derive(Clone, Copy)]
struct FixtureIds {
    player: EntityId,
    enemy: EntityId,
    falling: EntityId,
    support: EntityId,
    held: EntityId,
}

#[derive(Clone, Copy)]
struct RotationIds {
    player: EntityId,
    rotating: EntityId,
}

#[derive(Clone, Copy)]
struct DestructionIds {
    player: EntityId,
    weight: EntityId,
    support: EntityId,
    child: EntityId,
}

#[derive(Resource, Default)]
struct SeenCollisions(Vec<(EntityId, EntityId)>);

fn id(name: &str) -> EntityId {
    EntityId::deterministic(format!("gameplay-physics/{name}"))
}

fn actor(id: EntityId, name: &str, role: ActorRole, position: CoreVec3) -> ActorDefinition {
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
        flees_danger: false,
    }
}

fn physics(
    half_extents: CoreVec3,
    mass_kg: f32,
    pushable: bool,
    supports: Vec<EntityId>,
) -> ObjectPhysics {
    ObjectPhysics {
        body_half_extents_m: half_extents,
        mass_kg,
        pushable,
        supports,
        ..ObjectPhysics::default()
    }
}

fn rules_profile() -> RulesProfile {
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

fn fixture() -> (GameplayDocument, FixtureIds) {
    let ids = FixtureIds {
        player: id("player"),
        enemy: id("enemy"),
        falling: id("falling"),
        support: id("support"),
        held: id("held"),
    };
    let document = GameplayDocument {
        schema: GAMEPLAY_SCHEMA.into(),
        version: GAMEPLAY_VERSION,
        module_id: forge_core::ModuleId::deterministic("gameplay-physics/module"),
        area_id: forge_core::AreaId::deterministic("gameplay-physics/area"),
        profile: rules_profile(),
        player: ids.player,
        actors: vec![
            actor(
                ids.player,
                "Player",
                ActorRole::Player,
                CoreVec3::new(0.0, 0.95, -6.0),
            ),
            actor(
                ids.enemy,
                "Target",
                ActorRole::Enemy,
                CoreVec3::new(0.0, 0.95, 0.0),
            ),
        ],
        items: Vec::new(),
        objects: vec![
            ObjectDefinition {
                id: ids.falling,
                name: "Falling weight".into(),
                position: CoreVec3::new(0.0, 6.0, 0.0),
                kind: ObjectKind::Prop,
                physics: physics(CoreVec3::new(0.4, 0.4, 0.4), 4.0, true, Vec::new()),
            },
            ObjectDefinition {
                id: ids.support,
                name: "Support".into(),
                position: CoreVec3::new(4.0, 0.5, 0.0),
                kind: ObjectKind::Prop,
                physics: physics(CoreVec3::new(1.0, 0.5, 1.0), 0.0, false, vec![ids.held]),
            },
            ObjectDefinition {
                id: ids.held,
                name: "Supported object".into(),
                position: CoreVec3::new(4.0, 1.4, 0.0),
                kind: ObjectKind::Prop,
                physics: physics(CoreVec3::new(0.4, 0.4, 0.4), 1.0, true, Vec::new()),
            },
        ],
        places: Vec::new(),
        dialogues: Vec::new(),
        quests: Vec::new(),
    };
    (document, ids)
}

fn player_save_fixture() -> GameplayDocument {
    let player = id("save/player");
    GameplayDocument {
        schema: GAMEPLAY_SCHEMA.into(),
        version: GAMEPLAY_VERSION,
        module_id: forge_core::ModuleId::deterministic("gameplay-physics/save-module"),
        area_id: forge_core::AreaId::deterministic("gameplay-physics/save-area"),
        profile: rules_profile(),
        player,
        actors: vec![actor(
            player,
            "Save Player",
            ActorRole::Player,
            CoreVec3::new(0.0, 0.95, -6.0),
        )],
        items: Vec::new(),
        objects: Vec::new(),
        places: Vec::new(),
        dialogues: Vec::new(),
        quests: Vec::new(),
    }
}

fn rotation_fixture() -> (GameplayDocument, RotationIds) {
    let ids = RotationIds {
        player: id("rotation/player"),
        rotating: id("rotation/object"),
    };
    let document = GameplayDocument {
        schema: GAMEPLAY_SCHEMA.into(),
        version: GAMEPLAY_VERSION,
        module_id: forge_core::ModuleId::deterministic("gameplay-physics/rotation-module"),
        area_id: forge_core::AreaId::deterministic("gameplay-physics/rotation-area"),
        profile: rules_profile(),
        player: ids.player,
        actors: vec![actor(
            ids.player,
            "Player",
            ActorRole::Player,
            CoreVec3::new(0.0, 0.95, -6.0),
        )],
        items: Vec::new(),
        objects: vec![ObjectDefinition {
            id: ids.rotating,
            name: "Rotating prop".into(),
            position: CoreVec3::new(0.0, 4.0, 0.0),
            kind: ObjectKind::Prop,
            physics: physics(CoreVec3::new(0.4, 0.4, 0.4), 1.0, true, Vec::new()),
        }],
        places: Vec::new(),
        dialogues: Vec::new(),
        quests: Vec::new(),
    };
    (document, ids)
}

fn destruction_fixture() -> (GameplayDocument, DestructionIds) {
    let ids = DestructionIds {
        player: id("destruction/player"),
        weight: id("destruction/weight"),
        support: id("destruction/support"),
        child: id("destruction/child"),
    };
    let mut breakable_support = physics(CoreVec3::new(2.0, 0.2, 0.5), 0.0, false, vec![ids.child]);
    breakable_support.break_energy_j = Some(0.5);
    let document = GameplayDocument {
        schema: GAMEPLAY_SCHEMA.into(),
        version: GAMEPLAY_VERSION,
        module_id: forge_core::ModuleId::deterministic("gameplay-physics/destruction-module"),
        area_id: forge_core::AreaId::deterministic("gameplay-physics/destruction-area"),
        profile: rules_profile(),
        player: ids.player,
        actors: vec![actor(
            ids.player,
            "Player",
            ActorRole::Player,
            CoreVec3::new(0.0, 0.95, -6.0),
        )],
        items: Vec::new(),
        objects: vec![
            ObjectDefinition {
                id: ids.weight,
                name: "Break weight".into(),
                position: CoreVec3::new(-1.2, 5.0, 0.0),
                kind: ObjectKind::Prop,
                physics: physics(CoreVec3::new(0.4, 0.4, 0.4), 4.0, true, Vec::new()),
            },
            ObjectDefinition {
                id: ids.support,
                name: "Breakable board".into(),
                position: CoreVec3::new(0.0, 1.5, 0.0),
                kind: ObjectKind::Prop,
                physics: breakable_support,
            },
            ObjectDefinition {
                id: ids.child,
                name: "Offset supported load".into(),
                position: CoreVec3::new(1.2, 2.1, 0.0),
                kind: ObjectKind::Prop,
                physics: physics(CoreVec3::new(0.4, 0.4, 0.4), 1.0, true, Vec::new()),
            },
        ],
        places: Vec::new(),
        dialogues: Vec::new(),
        quests: Vec::new(),
    };
    (document, ids)
}

fn app(document: GameplayDocument, source_hash: ContentHash) -> App {
    app_with_save(document, source_hash, None)
}

fn app_with_save(document: GameplayDocument, source_hash: ContentHash, save: Option<&[u8]>) -> App {
    let profile = VisualValleyPlayerControllerRecipe.build().unwrap();
    let player_position = document.actor(document.player).unwrap().position;
    let runtime = GameplayRuntime::new(document, source_hash, save, false).unwrap();
    let mut app = App::new();
    app.add_plugins(MinimalPlugins)
        .add_plugins(TransformPlugin)
        // Headless physics needs the same Mesh3d visibility requirement as the renderer,
        // without its camera/skinned-mesh culling systems or a GPU.
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
            .with_initial_camera_target(
                Vec3::new(player_position.x, player_position.y, player_position.z) + Vec3::NEG_Z,
            )
            .with_interactive_input(false),
        )
        .insert_resource(runtime)
        .insert_resource(PlayerSimulationReady(true))
        .add_plugins(GameplayPlugin)
        .init_resource::<SeenCollisions>()
        .add_systems(FixedUpdate, record_collisions.after(GameplaySet::Observe));
    app.world_mut().spawn((
        RigidBody::Fixed,
        Collider::cuboid(20.0, 0.1, 20.0),
        Transform::from_xyz(0.0, -0.1, 0.0),
    ));
    app.world_mut()
        .resource_mut::<GameplayRuntime>()
        .set_enabled(true);
    app
}

#[test]
fn resumed_player_spawn_keeps_initial_camera_direction_and_cannot_move_live_player() {
    let document = player_save_fixture();
    let mut app = app(document, ContentHash::from_bytes([0x85; 32]));
    let saved_position = Vec3::new(3.0, 0.95, 5.0);
    forge_engine::PlayerSpawnPoint::rebase_before_start(app.world_mut(), saved_position).unwrap();
    for _ in 0..12 {
        app.update();
    }
    let snapshot = app.world().resource::<PlayerRuntimeSnapshot>();
    assert!(snapshot.player_position.distance(saved_position) < 0.05);
    assert!(snapshot.camera_yaw_radians.abs() < 0.000_01);
    let observed_position = snapshot.player_position;
    assert!(
        forge_engine::PlayerSpawnPoint::rebase_before_start(app.world_mut(), Vec3::ZERO).is_err()
    );
    assert_eq!(
        app.world()
            .resource::<PlayerRuntimeSnapshot>()
            .player_position,
        observed_position
    );
}

fn record_collisions(
    mut events: MessageReader<CollisionEvent>,
    domains: Query<&DomainEntityId>,
    mut seen: ResMut<SeenCollisions>,
) {
    for event in events.read() {
        let CollisionEvent::Started(first, second, _) = *event else {
            continue;
        };
        if let (Ok(first), Ok(second)) = (domains.get(first), domains.get(second)) {
            seen.0.push((first.0, second.0));
        }
    }
}

fn entity_for(app: &mut App, id: EntityId) -> Entity {
    let world = app.world_mut();
    let mut query = world.query::<(Entity, &DomainEntityId)>();
    query
        .iter(world)
        .find_map(|(entity, domain)| (domain.0 == id).then_some(entity))
        .expect("gameplay projection entity")
}

fn wait_for_body(app: &mut App, id: EntityId, expected: RigidBody) {
    for _ in 0..16 {
        let entity = entity_for(app, id);
        if app
            .world()
            .get::<RigidBody>(entity)
            .is_some_and(|body| *body == expected)
        {
            return;
        }
        app.update();
    }
    let runtime = app.world().resource::<GameplayRuntime>();
    let player = app
        .world()
        .resource::<forge_engine::PlayerRuntimeSnapshot>();
    panic!(
        "gameplay body {id} did not reach {expected:?}; ready={}, error={:?}, gameplay_tick={}, player_tick={}",
        runtime.is_ready(),
        runtime.error(),
        runtime.session().state().tick,
        player.fixed_tick
    );
}

fn in_motion(app: &App, id: EntityId) -> bool {
    app.world()
        .resource::<GameplayRuntime>()
        .session()
        .state()
        .objects[&id]
        .in_motion
}

fn contact_exists(app: &App, source: EntityId, target: EntityId) -> bool {
    app.world()
        .resource::<GameplayRuntime>()
        .session()
        .state()
        .contacts
        .values()
        .any(|receipt| receipt.observation.source == source && receipt.observation.target == target)
}

fn run_until_contact(app: &mut App, ids: FixtureIds) -> (bool, bool) {
    let mut saw_motion = false;
    for _ in 0..360 {
        app.update();
        if in_motion(app, ids.falling) {
            saw_motion = true;
            assert!(
                app.world()
                    .resource::<GameplayRuntime>()
                    .save_bytes()
                    .is_err(),
                "moving gameplay object must block saves"
            );
        }
        if contact_exists(app, ids.falling, ids.enemy) {
            return (saw_motion, true);
        }
    }
    (saw_motion, false)
}

fn assert_contact_evidence(app: &App, ids: FixtureIds) {
    assert!(
        app.world()
            .resource::<SeenCollisions>()
            .0
            .iter()
            .any(|(first, second)| {
                (*first == ids.falling && *second == ids.enemy)
                    || (*first == ids.enemy && *second == ids.falling)
            }),
        "the gameplay contact was not backed by a real Rapier CollisionEvent"
    );
    let runtime = app.world().resource::<GameplayRuntime>();
    assert!(
        runtime.error().is_none(),
        "runtime error: {:?}",
        runtime.error()
    );
    assert!(runtime.last_events().iter().any(|event| matches!(
        event,
        GameplayEvent::Impact { source, target, .. }
            if *source == ids.falling && *target == ids.enemy
    )));
    assert!(runtime.last_events().iter().any(|event| matches!(
        event,
        GameplayEvent::Damage { target, contact: true, amount }
            if *target == ids.enemy && *amount > 0
    )));
    assert!(runtime.session().state().actors[&ids.enemy].vitality < 100);
}

fn settle_and_resume(
    app: &mut App,
    document: GameplayDocument,
    source_hash: ContentHash,
    falling: EntityId,
) {
    for _ in 0..360 {
        if !in_motion(app, falling) {
            break;
        }
        app.update();
    }
    assert!(!in_motion(app, falling), "falling object did not settle");
    let save = app
        .world()
        .resource::<GameplayRuntime>()
        .save_bytes()
        .expect("settled gameplay can be saved");
    let resumed = GameplayRuntime::new(document, source_hash, Some(&save), false).unwrap();
    assert_eq!(
        resumed.session().state(),
        app.world().resource::<GameplayRuntime>().session().state()
    );
}

fn assert_supported_object_stays_fixed(app: &mut App, id: EntityId, start: Vec3) {
    let entity = entity_for(app, id);
    let end = app.world().get::<Transform>(entity).unwrap().translation;
    assert_eq!(
        *app.world().get::<RigidBody>(entity).unwrap(),
        RigidBody::Fixed
    );
    assert_eq!(end, start, "support kept the child at rest");
}

fn assert_fixed_pose(app: &mut App, id: EntityId, start: Vec3) {
    let entity = entity_for(app, id);
    let end = app.world().get::<Transform>(entity).unwrap().translation;
    assert_eq!(
        *app.world().get::<RigidBody>(entity).unwrap(),
        RigidBody::Fixed
    );
    assert_eq!(end, start, "fixed gameplay object moved");
}

fn disable_gravity(app: &mut App, id: EntityId) {
    let entity = entity_for(app, id);
    app.world_mut().entity_mut(entity).insert(GravityScale(0.0));
}

fn set_angular_velocity(app: &mut App, id: EntityId, angular: Vec3) {
    let entity = entity_for(app, id);
    {
        let mut velocity = app.world_mut().get_mut::<Velocity>(entity).unwrap();
        velocity.linear = Vec3::ZERO;
        velocity.angular = angular;
    }
    app.world_mut()
        .entity_mut(entity)
        .insert(Sleeping::default());
}

fn orientation(app: &mut App, id: EntityId) -> Quat {
    let entity = entity_for(app, id);
    app.world().get::<Transform>(entity).unwrap().rotation
}

fn state_orientation(app: &App, id: EntityId) -> Quat {
    Quat::from_array(
        app.world()
            .resource::<GameplayRuntime>()
            .session()
            .state()
            .objects[&id]
            .rotation_xyzw,
    )
}

fn orientation_matches(expected: Quat, actual: Quat) -> bool {
    expected
        .to_array()
        .into_iter()
        .zip(actual.to_array())
        .map(|(expected, actual)| (expected - actual).abs())
        .fold(0.0, f32::max)
        <= 1.0e-4
}

fn assert_orientation_matches(expected: Quat, actual: Quat) {
    assert!(
        orientation_matches(expected, actual),
        "orientation changed during save/resume: expected {expected:?}, got {actual:?}"
    );
}

fn submit_player_command(app: &mut App, command: PlayerControlCommand) -> u64 {
    app.world_mut()
        .resource_mut::<PendingPlayerControlCommand>()
        .submit(command)
        .expect("player fixture command is finite")
}

fn wait_for_player_command(app: &mut App, sequence: u64) -> PlayerRuntimeSnapshot {
    for _ in 0..8 {
        app.update();
        let snapshot = app.world().resource::<PlayerRuntimeSnapshot>();
        if snapshot.last_applied_sequence == Some(sequence) {
            return snapshot.clone();
        }
    }
    let snapshot = app.world().resource::<PlayerRuntimeSnapshot>().clone();
    panic!("player command {sequence} was not applied: {snapshot:?}");
}

fn wait_for_player_saveable(app: &mut App) {
    for _ in 0..120 {
        app.update();
        if app
            .world()
            .resource::<GameplayRuntime>()
            .save_bytes()
            .is_ok()
        {
            return;
        }
    }
    let snapshot = app.world().resource::<PlayerRuntimeSnapshot>().clone();
    panic!("player did not reach a saveable grounded state: {snapshot:?}");
}

fn non_identity(rotation: Quat) -> bool {
    let [x, y, z, w] = rotation.to_array();
    x.abs() + y.abs() + z.abs() + (w - 1.0).abs() > 1.0e-3
}

fn rotate_settle_and_save(app: &mut App, ids: RotationIds) -> (Vec<u8>, Quat) {
    disable_gravity(app, ids.rotating);
    set_angular_velocity(app, ids.rotating, Vec3::new(0.0, 4.0, 0.0));

    let mut saw_rotation = false;
    for _ in 0..30 {
        app.update();
        if non_identity(orientation(app, ids.rotating)) {
            saw_rotation = true;
            break;
        }
    }
    assert!(saw_rotation, "Rapier angular velocity produced no rotation");
    assert!(
        app.world()
            .resource::<GameplayRuntime>()
            .save_bytes()
            .is_err(),
        "save must be blocked while angular motion is observed"
    );

    set_angular_velocity(app, ids.rotating, Vec3::ZERO);
    let mut stable = false;
    for _ in 0..120 {
        app.update();
        if !in_motion(app, ids.rotating) {
            let before = orientation(app, ids.rotating);
            app.update();
            let after = orientation(app, ids.rotating);
            if !in_motion(app, ids.rotating) && orientation_matches(before, after) {
                stable = true;
                break;
            }
        }
    }
    assert!(stable, "rotating object did not stabilize before save");

    let save = app
        .world()
        .resource::<GameplayRuntime>()
        .save_bytes()
        .expect("stabilized rotation can be saved");
    let observed = state_orientation(app, ids.rotating);
    assert!(
        non_identity(observed),
        "saved rotation unexpectedly identity"
    );
    assert_orientation_matches(observed, orientation(app, ids.rotating));
    (save, observed)
}

#[test]
fn falling_object_uses_real_collision_damage_and_supported_object_stays_fixed() {
    let (document, ids) = fixture();
    let source_hash = ContentHash::from_bytes([0x41; 32]);
    let mut app = app(document.clone(), source_hash);
    app.update();
    wait_for_body(&mut app, ids.falling, RigidBody::Dynamic);

    let held_entity = entity_for(&mut app, ids.held);
    let held_start = app
        .world()
        .get::<Transform>(held_entity)
        .unwrap()
        .translation;
    let support_entity = entity_for(&mut app, ids.support);
    let support_start = app
        .world()
        .get::<Transform>(support_entity)
        .unwrap()
        .translation;
    assert_eq!(
        *app.world().get::<RigidBody>(held_entity).unwrap(),
        RigidBody::Fixed
    );

    let (saw_motion, saw_contact) = run_until_contact(&mut app, ids);
    assert!(saw_motion, "falling object never entered motion");
    assert!(
        saw_contact,
        "Rapier collision never reached gameplay runtime"
    );
    assert_contact_evidence(&app, ids);
    settle_and_resume(&mut app, document, source_hash, ids.falling);
    assert_fixed_pose(&mut app, ids.support, support_start);
    assert_supported_object_stays_fixed(&mut app, ids.held, held_start);
    let falling_entity = entity_for(&mut app, ids.falling);
    assert_eq!(
        *app.world().get::<RigidBody>(falling_entity).unwrap(),
        RigidBody::Dynamic
    );
    assert!(
        app.world().get::<Velocity>(falling_entity).is_some(),
        "projected dynamic object has a Rapier velocity"
    );
}

#[test]
fn angular_velocity_rotation_round_trips_through_a_new_app() {
    let (document, ids) = rotation_fixture();
    let source_hash = ContentHash::from_bytes([0x52; 32]);
    let mut app = app(document.clone(), source_hash);
    app.update();
    wait_for_body(&mut app, ids.rotating, RigidBody::Dynamic);
    let (save, observed) = rotate_settle_and_save(&mut app, ids);

    let mut resumed = app_with_save(document, source_hash, Some(&save));
    resumed.update();
    disable_gravity(&mut resumed, ids.rotating);
    let restored = orientation(&mut resumed, ids.rotating);
    assert!(
        non_identity(restored),
        "resumed rotation unexpectedly identity"
    );
    assert_orientation_matches(observed, restored);
    assert_orientation_matches(observed, state_orientation(&resumed, ids.rotating));
}

#[test]
fn real_rapier_support_break_releases_offset_child() {
    let (document, ids) = destruction_fixture();
    let source_hash = ContentHash::from_bytes([0x63; 32]);
    let mut app = app(document, source_hash);
    app.update();
    wait_for_body(&mut app, ids.weight, RigidBody::Dynamic);

    let child_entity = entity_for(&mut app, ids.child);
    let child_start = app
        .world()
        .get::<Transform>(child_entity)
        .unwrap()
        .translation;
    assert_eq!(
        *app.world().get::<RigidBody>(child_entity).unwrap(),
        RigidBody::Fixed
    );

    let mut destroyed = false;
    for _ in 0..360 {
        app.update();
        let runtime = app.world().resource::<GameplayRuntime>();
        let support_state = &runtime.session().state().objects[&ids.support];
        let child_state = &runtime.session().state().objects[&ids.child];
        if support_state.destroyed && child_state.released {
            destroyed = true;
            break;
        }
    }
    assert!(destroyed, "Rapier impact never destroyed the support");

    let runtime = app.world().resource::<GameplayRuntime>();
    assert!(runtime.last_events().iter().any(|event| matches!(
        event,
        GameplayEvent::Destroyed { object } if *object == ids.support
    )));
    assert!(runtime.last_events().iter().any(|event| matches!(
        event,
        GameplayEvent::Released { object } if *object == ids.child
    )));
    assert!(
        app.world()
            .resource::<SeenCollisions>()
            .0
            .iter()
            .any(|(first, second)| {
                (*first == ids.weight && *second == ids.support)
                    || (*first == ids.support && *second == ids.weight)
            }),
        "support destruction was not backed by a real Rapier CollisionEvent"
    );

    let support_entity = entity_for(&mut app, ids.support);
    assert!(
        app.world()
            .get::<ColliderDisabled>(support_entity)
            .is_some(),
        "destroyed support collider remains active"
    );
    wait_for_body(&mut app, ids.child, RigidBody::Dynamic);
    let mut child_dropped = false;
    for _ in 0..120 {
        app.update();
        let child_entity = entity_for(&mut app, ids.child);
        let child_position = app
            .world()
            .get::<Transform>(child_entity)
            .unwrap()
            .translation;
        child_dropped |= child_position.y < child_start.y - 0.05;
        if child_dropped {
            break;
        }
    }
    assert!(
        child_dropped,
        "released child did not fall after support destruction"
    );
}

#[test]
fn player_motion_blocks_save_until_kcc_stops_and_lands() {
    let document = player_save_fixture();
    let source_hash = ContentHash::from_bytes([0x74; 32]);
    let mut app = app(document, source_hash);

    wait_for_player_saveable(&mut app);
    assert!(
        app.world()
            .resource::<GameplayRuntime>()
            .save_bytes()
            .is_ok(),
        "a grounded stationary player can be saved"
    );

    let move_sequence = submit_player_command(
        &mut app,
        PlayerControlCommand {
            move_axis: Vec2::Y,
            ..PlayerControlCommand::default()
        },
    );
    let moving = wait_for_player_command(&mut app, move_sequence);
    assert!(
        moving.horizontal_velocity.xz().length() > 0.05
            || moving.desired_translation.xz().length() > 0.001
            || moving.effective_translation.xz().length() > 0.001,
        "KCC movement did not produce a real horizontal motion: {moving:?}"
    );
    assert!(
        app.world()
            .resource::<GameplayRuntime>()
            .save_bytes()
            .is_err(),
        "a moving player must block saves"
    );

    let stop_sequence = submit_player_command(&mut app, PlayerControlCommand::default());
    let stopped = wait_for_player_command(&mut app, stop_sequence);
    assert_eq!(stopped.last_applied_sequence, Some(stop_sequence));
    wait_for_player_saveable(&mut app);

    let jump_sequence = submit_player_command(
        &mut app,
        PlayerControlCommand {
            jump: true,
            ..PlayerControlCommand::default()
        },
    );
    let airborne = wait_for_player_command(&mut app, jump_sequence);
    assert!(!airborne.grounded);
    assert!(airborne.vertical_velocity_mps > 0.05);
    assert!(
        app.world()
            .resource::<GameplayRuntime>()
            .save_bytes()
            .is_err(),
        "an airborne player must block saves"
    );

    let mut landed_and_saveable = false;
    for _ in 0..240 {
        app.update();
        let snapshot = app.world().resource::<PlayerRuntimeSnapshot>().clone();
        if !snapshot.grounded || snapshot.vertical_velocity_mps.abs() > 0.05 {
            assert!(
                app.world()
                    .resource::<GameplayRuntime>()
                    .save_bytes()
                    .is_err(),
                "save became available before the KCC player landed: {snapshot:?}"
            );
        } else if app
            .world()
            .resource::<GameplayRuntime>()
            .save_bytes()
            .is_ok()
        {
            landed_and_saveable = true;
            break;
        }
    }
    assert!(
        landed_and_saveable,
        "player did not return to a grounded saveable state"
    );
}

#[test]
fn blocked_kcc_command_does_not_repeat_damage_from_stationary_object() {
    let mut document = player_save_fixture();
    let player = document.player;
    let obstacle = id("blocked-command/obstacle");
    document.objects.push(ObjectDefinition {
        id: obstacle,
        name: "Stationary collision obstacle".into(),
        position: CoreVec3::new(0.0, 1.0, -8.0),
        kind: ObjectKind::Prop,
        physics: physics(CoreVec3::new(2.0, 1.0, 0.5), 1.0, false, Vec::new()),
    });
    let mut app = app(document, ContentHash::from_bytes([0x75; 32]));
    wait_for_player_saveable(&mut app);
    let obstacle_entity = entity_for(&mut app, obstacle);
    // Keep a real manifold active across the KCC query skin. The test exercises
    // physical contact, not just a requested movement or a distance check.
    app.world_mut()
        .entity_mut(obstacle_entity)
        .insert((ActiveCollisionTypes::all(), ContactSkin(0.02)));
    wait_for_body(&mut app, obstacle, RigidBody::Fixed);
    let initial_position = app
        .world()
        .get::<Transform>(obstacle_entity)
        .unwrap()
        .translation;
    let sequence = submit_player_command(
        &mut app,
        PlayerControlCommand {
            move_axis: Vec2::Y,
            ..PlayerControlCommand::default()
        },
    );
    wait_for_player_command(&mut app, sequence);
    let mut blocked = false;
    for _ in 0..240 {
        app.update();
        let snapshot = app.world().resource::<PlayerRuntimeSnapshot>();
        if snapshot.desired_translation.z.abs() > 0.01
            && snapshot.effective_translation.z.abs() < 0.005
            && snapshot.collisions > 0
            && contact_exists(&app, obstacle, player)
        {
            blocked = true;
            break;
        }
    }
    assert!(
        blocked,
        "the fixture must produce a blocked KCC and a real gameplay contact"
    );
    let vitality = app
        .world()
        .resource::<GameplayRuntime>()
        .session()
        .state()
        .actors[&player]
        .vitality;
    assert!(
        vitality > 0,
        "the first physical approach must leave a live actor"
    );
    for _ in 0..60 {
        app.update();
        let snapshot = app.world().resource::<PlayerRuntimeSnapshot>();
        assert!(snapshot.horizontal_velocity.length() > 1.0);
        assert!(snapshot.desired_translation.z.abs() > 0.01);
        assert!(
            snapshot.effective_translation.z.abs() < 0.005,
            "KCC must stay blocked while the command remains held: {snapshot:?}"
        );
        assert!(snapshot.collisions > 0);
    }
    let runtime = app.world().resource::<GameplayRuntime>();
    assert_eq!(
        runtime.session().state().actors[&player].vitality,
        vitality,
        "holding a blocked command must not create kinetic energy and repeat damage"
    );
    assert!(!runtime.session().state().objects[&obstacle].in_motion);
    assert_eq!(
        app.world()
            .get::<Transform>(obstacle_entity)
            .unwrap()
            .translation,
        initial_position
    );
}

#[derive(Resource)]
struct CapsuleResyncProbe {
    phase: u8,
    object: EntityId,
    old_pose: Vec3,
    old_collider: Collider,
}

// Exercise the same public schedule boundary as a Tidewater ladder exit:
// resolve changes the ECS capsule/pose after writeback, then drive skips one
// sweep while Rapier installs it. Contacts/output themselves are never injected.
fn morph_capsule_after_writeback(
    mut probe: ResMut<CapsuleResyncProbe>,
    mut player: Query<(&mut Transform, &mut Collider), With<ForgePlayerCharacter>>,
    mut snapshot: ResMut<PlayerRuntimeSnapshot>,
    mut runtime: ResMut<GameplayRuntime>,
) {
    if probe.phase != 1 {
        return;
    }
    let (mut transform, mut collider) = player.single_mut().unwrap();
    transform.translation.x += 1.4;
    *collider = Collider::capsule_y(0.15, 0.3);
    snapshot.player_position = transform.translation;
    let state = runtime.session().state();
    let request = GameRequest {
        command_id: CommandId::deterministic("gameplay-physics/resync/stale-push"),
        expected_sequence: state.sequence,
        actor: runtime.session().document().player,
        action: GameAction::Push {
            object: probe.object,
            impulse: CoreVec3::new(1.0, 0.0, 0.0),
        },
    };
    runtime.queue(request).unwrap();
    probe.phase = 2;
}

fn skip_resync_sweep(
    mut probe: ResMut<CapsuleResyncProbe>,
    mut player: Query<
        &mut bevy_rapier3d::prelude::KinematicCharacterController,
        With<ForgePlayerCharacter>,
    >,
    mut snapshot: ResMut<PlayerRuntimeSnapshot>,
) {
    if probe.phase == 2 {
        player.single_mut().unwrap().translation = None;
        snapshot.desired_translation = Vec3::ZERO;
        snapshot.effective_translation = Vec3::ZERO;
        probe.phase = 3;
    }
}

fn prepared_capsule_resync_contact_witness() -> (App, EntityId, Entity, Entity) {
    use bevy_rapier3d::prelude::KinematicCharacterControllerOutput;
    let mut document = player_save_fixture();
    let object = id("resync/contact-object");
    document.objects.push(ObjectDefinition {
        id: object,
        name: "Contact witness".into(),
        position: CoreVec3::new(0.0, 1.0, -8.0),
        kind: ObjectKind::Prop,
        physics: physics(CoreVec3::new(0.25, 0.5, 0.25), 1.0, true, Vec::new()),
    });
    let mut app = app(document, ContentHash::from_bytes([0x76; 32]));
    wait_for_player_saveable(&mut app);
    let object_entity = entity_for(&mut app, object);
    app.world_mut()
        .entity_mut(object_entity)
        .insert((ActiveCollisionTypes::all(), ContactSkin(0.02)));
    let sequence = submit_player_command(
        &mut app,
        PlayerControlCommand {
            move_axis: Vec2::Y,
            ..default()
        },
    );
    wait_for_player_command(&mut app, sequence);
    let player = app
        .world_mut()
        .query_filtered::<Entity, With<ForgePlayerCharacter>>()
        .single(app.world())
        .unwrap();
    let mut witnessed = false;
    for _ in 0..240 {
        app.update();
        let output = app
            .world()
            .get::<KinematicCharacterControllerOutput>(player)
            .unwrap();
        if output.collisions.iter().any(|c| c.entity == object_entity) {
            witnessed = true;
            break;
        }
    }
    assert!(
        witnessed,
        "fixture needs a real KCC collision with the target"
    );
    (app, object, object_entity, player)
}

fn restore_capsule_resync_contact(app: &mut App, player: Entity, object_entity: Entity) {
    use bevy_rapier3d::prelude::KinematicCharacterControllerOutput;
    let probe = app.world().resource::<CapsuleResyncProbe>();
    let (pose, collider) = (probe.old_pose, probe.old_collider.clone());
    app.world_mut()
        .get_mut::<Transform>(player)
        .unwrap()
        .translation = pose;
    *app.world_mut().get_mut::<Collider>(player).unwrap() = collider;
    // Reinstallation needs the same skipped sweep: KCC runs before SyncBackend.
    app.world_mut().resource_mut::<CapsuleResyncProbe>().phase = 2;
    for _ in 0..4 {
        app.update();
    }
    assert!(
        app.world()
            .get::<KinematicCharacterControllerOutput>(player)
            .unwrap()
            .collisions
            .iter()
            .any(|c| c.entity == object_entity)
    );
}

#[test]
fn physical_regression_capsule_resync_cannot_reuse_previous_contact_for_push() {
    use bevy_rapier3d::prelude::{ExternalImpulse, KinematicCharacterControllerOutput, PhysicsSet};
    let (mut app, object, object_entity, player) = prepared_capsule_resync_contact_witness();
    assert!(app.world().get::<ExternalImpulse>(object_entity).is_none());
    let old_pose = app.world().get::<Transform>(player).unwrap().translation;
    let old_collider = app.world().get::<Collider>(player).unwrap().clone();
    let (before, before_tick) = {
        let runtime = app.world().resource::<GameplayRuntime>();
        (
            runtime.session().state().sequence,
            runtime.session().state().tick,
        )
    };
    app.insert_resource(CapsuleResyncProbe {
        phase: 1,
        object,
        old_pose,
        old_collider,
    })
    .add_systems(
        FixedUpdate,
        morph_capsule_after_writeback
            .after(PlayerControllerSet::Observe)
            .before(GameplaySet::Observe),
    )
    .add_systems(
        FixedUpdate,
        skip_resync_sweep
            .after(PlayerControllerSet::Drive)
            .before(PhysicsSet::SyncBackend),
    );
    app.update();
    let runtime = app.world().resource::<GameplayRuntime>();
    assert_eq!(
        runtime.session().state().tick,
        before_tick + 1,
        "backend synchronization must defer actions without pausing the world frame"
    );
    assert_eq!(
        runtime.session().state().sequence,
        before,
        "a future ECS capsule must not authenticate the previous backend contact"
    );
    assert!(
        runtime.has_queued_actions(),
        "action must wait for backend synchronization"
    );
    app.update();
    let output = app
        .world()
        .get::<KinematicCharacterControllerOutput>(player)
        .unwrap();
    assert!(
        output.collisions.iter().any(|c| c.entity == object_entity),
        "the skipped sweep must retain its previous real KCC output"
    );
    let runtime = app.world().resource::<GameplayRuntime>();
    assert!(!runtime.has_queued_actions());
    assert_eq!(runtime.session().state().sequence, before);
    assert!(
        runtime.feedback().contains("confirmed contact"),
        "{}",
        runtime.feedback()
    );
    // Contact with a real dynamic body can leave solver motion before this
    // request. Test the refused action's effects rather than assuming rest.
    assert_eq!(runtime.session().state().objects[&object].initiator, None);
    assert!(
        app.world().get::<ExternalImpulse>(object_entity).is_none(),
        "a refused stale contact must not project a gameplay impulse"
    );

    // A new genuine contact must still authorize the same action.
    restore_capsule_resync_contact(&mut app, player, object_entity);
    let mut runtime = app.world_mut().resource_mut::<GameplayRuntime>();
    let actor = runtime.session().document().player;
    runtime
        .queue(GameRequest {
            command_id: CommandId::deterministic("gameplay-physics/resync/fresh-push"),
            expected_sequence: before,
            actor,
            action: GameAction::Push {
                object,
                impulse: CoreVec3::new(1.0, 0.0, 0.0),
            },
        })
        .unwrap();
    app.update();
    let world = app.world();
    let after = world.resource::<GameplayRuntime>().session().state();
    assert_eq!(after.sequence, before + 1);
    assert_eq!(after.objects[&object].initiator, Some(actor));
    assert_eq!(
        world
            .get::<ExternalImpulse>(object_entity)
            .expect("the accepted fresh contact must project its impulse")
            .impulse,
        Vec3::X
    );
}

fn stationary_attack_app(separation: f32) -> (App, EntityId, EntityId) {
    let mut document = player_save_fixture();
    let player = document.player;
    let enemy = id("stationary-attack/enemy");
    let mut target = actor(
        enemy,
        "Stationary attack target",
        ActorRole::Enemy,
        CoreVec3::new(separation, 0.95, -6.0),
    );
    target.max_vitality = 40;
    document.actors.push(target);
    let mut app = app(document, ContentHash::from_bytes([0x86; 32]));
    wait_for_player_saveable(&mut app);
    wait_for_body(&mut app, enemy, RigidBody::KinematicPositionBased);
    for _ in 0..12 {
        app.update();
    }
    (app, player, enemy)
}

fn queue_stationary_attack(app: &mut App, target: EntityId, command: &str) {
    let mut runtime = app.world_mut().resource_mut::<GameplayRuntime>();
    let actor = runtime.session().document().player;
    let expected_sequence = runtime.session().state().sequence;
    runtime
        .queue(GameRequest {
            command_id: CommandId::deterministic(command),
            expected_sequence,
            actor,
            action: GameAction::Attack { target },
        })
        .unwrap();
    for _ in 0..8 {
        app.update();
        if !app
            .world()
            .resource::<GameplayRuntime>()
            .has_queued_actions()
        {
            return;
        }
    }
    panic!("stationary attack was not processed");
}

#[test]
fn stationary_kcc_contact_allows_attack_after_real_approach_and_stop() {
    use bevy_rapier3d::prelude::KinematicCharacterControllerOutput;
    // This start offset exercises a real discrete KCC stop in the outer part
    // of its prediction gap, beyond the previous 0.05 m unskinned action cast.
    let (mut app, player_id, enemy) = stationary_attack_app(2.525);
    let command = submit_player_command(
        &mut app,
        PlayerControlCommand {
            move_axis: Vec2::X,
            ..default()
        },
    );
    wait_for_player_command(&mut app, command);
    let mut blocked = false;
    for _ in 0..240 {
        app.update();
        let snapshot = app.world().resource::<PlayerRuntimeSnapshot>();
        if snapshot.player_position.x > 1.0
            && snapshot.desired_translation.x > 0.01
            && snapshot.effective_translation.x.abs() < 0.0001
        {
            blocked = true;
            break;
        }
    }
    assert!(blocked, "a real KCC approach must be stopped by the target");
    let command = submit_player_command(&mut app, PlayerControlCommand::default());
    wait_for_player_command(&mut app, command);
    for _ in 0..60 {
        app.update();
    }
    let stopped = app.world().resource::<PlayerRuntimeSnapshot>().clone();
    let enemy_entity = entity_for(&mut app, enemy);
    let target = app
        .world()
        .get::<Transform>(enemy_entity)
        .unwrap()
        .translation;
    let gap = stopped.player_position.xz().distance(target.xz()) - 0.7;
    assert!(gap > 0.05 && gap <= 0.06, "resting KCC gap: {gap}");
    let player = app
        .world_mut()
        .query_filtered::<Entity, With<ForgePlayerCharacter>>()
        .single(app.world())
        .unwrap();
    assert!(
        !app.world()
            .get::<KinematicCharacterControllerOutput>(player)
            .unwrap()
            .collisions
            .iter()
            .any(|collision| collision.entity == enemy_entity),
        "the test must exercise stationary proximity, not a retained target collision"
    );
    for _ in 0..12 {
        app.update();
        let snapshot = app.world().resource::<PlayerRuntimeSnapshot>();
        assert!(snapshot.player_position.distance(stopped.player_position) < 0.001);
        assert!(snapshot.effective_translation.x.abs() < 0.0001);
    }
    for (index, expected_health, expected_stamina) in [(0, 20, 95), (1, 0, 90)] {
        queue_stationary_attack(&mut app, enemy, &format!("stationary-attack/{index}"));
        let runtime = app.world().resource::<GameplayRuntime>();
        let state = runtime.session().state();
        assert_eq!(
            state.actors[&enemy].vitality,
            expected_health,
            "{}",
            runtime.feedback()
        );
        assert_eq!(state.actors[&player_id].stamina, expected_stamina);
        assert_eq!(state.sequence, index + 1);
    }
}

#[test]
fn stationary_kcc_contact_refuses_target_beyond_contact_envelope_inside_rpg_range() {
    let (mut app, player, enemy) = stationary_attack_app(0.77);
    queue_stationary_attack(&mut app, enemy, "stationary-attack/distant");
    let runtime = app.world().resource::<GameplayRuntime>();
    assert!(runtime.feedback().contains("no physical contact confirmed"));
    assert_eq!(runtime.session().state().sequence, 0);
    assert_eq!(runtime.session().state().actors[&enemy].vitality, 40);
    assert_eq!(runtime.session().state().actors[&player].stamina, 100);
}

#[test]
fn stationary_kcc_contact_uses_effective_relative_controller_offset() {
    use bevy_rapier3d::control::{CharacterLength, KinematicCharacterController};
    let (mut app, player, enemy) = stationary_attack_app(0.77);
    app.world_mut()
        .query_filtered::<&mut KinematicCharacterController, With<ForgePlayerCharacter>>()
        .single_mut(app.world_mut())
        .unwrap()
        .offset = CharacterLength::Relative(0.03 / 1.8);
    for _ in 0..12 {
        app.update();
    }
    queue_stationary_attack(&mut app, enemy, "stationary-attack/relative-offset");
    let runtime = app.world().resource::<GameplayRuntime>();
    assert_eq!(
        runtime.session().state().sequence,
        1,
        "{}",
        runtime.feedback()
    );
    assert_eq!(runtime.session().state().actors[&enemy].vitality, 20);
    assert_eq!(runtime.session().state().actors[&player].stamina, 95);
}

#[test]
fn stationary_kcc_contact_keeps_real_occlusion_and_releases_when_wall_is_removed() {
    let (mut app, player, enemy) = stationary_attack_app(0.75);
    let wall = app
        .world_mut()
        .spawn((
            RigidBody::Fixed,
            Collider::cuboid(0.005, 2.0, 0.5),
            Transform::from_xyz(0.375, 0.95, -6.0),
        ))
        .id();
    for _ in 0..4 {
        app.update();
    }
    queue_stationary_attack(&mut app, enemy, "stationary-attack/occluded");
    let runtime = app.world().resource::<GameplayRuntime>();
    assert!(runtime.feedback().contains("target is occluded"));
    assert_eq!(runtime.session().state().sequence, 0);
    assert_eq!(runtime.session().state().actors[&enemy].vitality, 40);
    assert_eq!(runtime.session().state().actors[&player].stamina, 100);
    app.world_mut().despawn(wall);
    for _ in 0..4 {
        app.update();
    }
    queue_stationary_attack(&mut app, enemy, "stationary-attack/wall-removed");
    let runtime = app.world().resource::<GameplayRuntime>();
    assert_eq!(
        runtime.session().state().sequence,
        1,
        "{}",
        runtime.feedback()
    );
    assert_eq!(runtime.session().state().actors[&enemy].vitality, 20);
    assert_eq!(runtime.session().state().actors[&player].stamina, 95);
}

#[test]
fn stationary_kcc_contact_allows_attack_from_oblique_resting_pose() {
    use bevy_rapier3d::prelude::KinematicCharacterControllerOutput;
    // Start from the recorded oblique placement, then let the real controller
    // settle. Exact immutable backend geometry is covered by the private
    // contact-band unit test; this integration checks resolved physics/actions.
    let start = Vec3::new(
        f32::from_bits(0x3fdf_8f6b),
        f32::from_bits(0x3f68_f5bb),
        f32::from_bits(0xc03f_f628),
    );
    let mut document = player_save_fixture();
    document.actors[0].position = CoreVec3::new(start.x, start.y, start.z);
    let player_id = document.player;
    let enemy = id("stationary-attack/oblique-resting-pose");
    let mut target = actor(
        enemy,
        "Oblique stationary capsule target",
        ActorRole::Enemy,
        CoreVec3::new(2.5, f32::from_bits(0x3f66_6666), -3.0),
    );
    target.max_vitality = 40;
    document.actors.push(target);
    let mut app = app(document, ContentHash::from_bytes([0x87; 32]));
    wait_for_player_saveable(&mut app);
    wait_for_body(&mut app, enemy, RigidBody::KinematicPositionBased);
    for _ in 0..60 {
        app.update();
    }
    let stopped = app.world().resource::<PlayerRuntimeSnapshot>().clone();
    let player = app
        .world_mut()
        .query_filtered::<Entity, With<ForgePlayerCharacter>>()
        .single(app.world())
        .unwrap();
    let enemy_entity = entity_for(&mut app, enemy);
    assert!(
        !app.world()
            .get::<KinematicCharacterControllerOutput>(player)
            .unwrap()
            .collisions
            .iter()
            .any(|collision| collision.entity == enemy_entity),
        "a retained KCC target collision must not authenticate stationary proximity"
    );
    for _ in 0..12 {
        app.update();
        let snapshot = app.world().resource::<PlayerRuntimeSnapshot>();
        assert!(snapshot.player_position.distance(stopped.player_position) < 0.001);
        assert!(snapshot.desired_translation.xz().length() <= 0.001);
        assert!(snapshot.effective_translation.length() <= 0.001);
    }
    for (index, expected_health, expected_stamina) in [(0, 20, 95), (1, 0, 90)] {
        queue_stationary_attack(
            &mut app,
            enemy,
            &format!("stationary-attack/oblique/{index}"),
        );
        let runtime = app.world().resource::<GameplayRuntime>();
        assert_eq!(
            runtime.session().state().actors[&enemy].vitality,
            expected_health,
            "{}",
            runtime.feedback()
        );
        assert_eq!(
            runtime.session().state().actors[&player_id].stamina,
            expected_stamina
        );
        assert_eq!(runtime.session().state().sequence, index + 1);
    }
}
