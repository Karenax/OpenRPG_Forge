#![cfg(all(not(target_arch = "wasm32"), feature = "physics-rapier"))]
use bevy::{prelude::*, time::TimeUpdateStrategy};
use bevy_rapier3d::prelude::{
    Collider, ColliderDisabled, GravityScale, NoUserData, RapierPhysicsPlugin, RigidBody,
    TimestepMode, Velocity,
};
use forge_core::{AreaId, ContentHash, EntityId, ModuleId, Vec3 as CoreVec3};
use forge_engine::{
    DomainEntityId, PlayerControllerPlugin, PlayerSimulationReady,
    gameplay::{DangerMotionState, GameplayPlugin, GameplayRuntime},
    navigation::{
        NavigationRuntime, NavigationSet, NavigationStatus, NavigationTileObservation, TileKey,
    },
};
use forge_gameplay::{
    ActorDefinition, ActorRole, GAMEPLAY_SCHEMA, GAMEPLAY_VERSION, GameplayDocument, RulesProfile,
};
use forge_world::VisualValleyPlayerControllerRecipe;
use std::time::{Duration, Instant};

fn id(name: &str) -> EntityId {
    EntityId::deterministic(format!("navigation-product/{name}"))
}

#[derive(Resource, Default)]
struct InvalidationWitness(Option<NavigationTileObservation>);

fn install_invalidation_witness(app: &mut App, affected: TileKey) {
    app.init_resource::<InvalidationWitness>().add_systems(
        FixedUpdate,
        (move |nav: Res<NavigationRuntime>, mut witness: ResMut<InvalidationWitness>| {
            if witness.0.is_none() {
                witness.0 = nav.tiles().into_iter().find(|tile| tile.key == affected);
            }
        })
        .after(NavigationSet::Guard)
        .before(bevy_rapier3d::prelude::PhysicsSet::SyncBackend),
    );
}

fn log_patrol_speed_witness(app: &App, actor: Entity, speed: f32) {
    let motion = app.world().get::<DangerMotionState>(actor).unwrap();
    let fixed_time = app.world().resource::<Time<Fixed>>();
    let output = app
        .world()
        .get::<bevy_rapier3d::prelude::KinematicCharacterControllerOutput>(actor)
        .unwrap();
    let normals: Vec<_> = output
        .collisions
        .iter()
        .filter_map(|hit| hit.hit.details.as_ref().map(|details| details.normal1))
        .collect();
    eprintln!(
        "speed witness: speed={speed}, delta={}, timestep={}, pose={:?}, requested={:?}, effective={:?}, output_desired={:?}, grounded={}, collisions={}, normals={normals:?}",
        fixed_time.delta_secs(),
        fixed_time.timestep().as_secs_f32(),
        app.world().get::<Transform>(actor).unwrap().translation,
        motion.requested_translation,
        motion.effective_translation,
        output.desired_translation,
        motion.grounded,
        motion.collision_count,
    );
}

#[test]
fn changing_source_while_a_real_job_is_in_flight_rejects_its_old_result() {
    let (mut app, door, _npc) = app();
    let affected = TileKey { x: 0, z: -1 };
    step_until(&mut app, Duration::from_secs(30), |app| {
        app.world()
            .resource::<NavigationRuntime>()
            .tiles()
            .iter()
            .any(|tile| tile.key == affected && tile.status == NavigationStatus::Generating)
    });
    let (previous, rejected_before) = {
        let nav = app.world().resource::<NavigationRuntime>();
        let previous = nav
            .tiles()
            .into_iter()
            .find(|tile| tile.key == affected)
            .unwrap();
        eprintln!(
            "inflight before mutation: rejected={}, active={}, pending={}, affected={:?}, tiles={:?}",
            nav.rejected_stale_results(),
            nav.active_jobs(),
            nav.pending_jobs(),
            nav.tiles().into_iter().find(|tile| tile.key == affected),
            nav.tiles(),
        );
        (previous, nav.rejected_stale_results())
    };
    install_invalidation_witness(&mut app, affected);
    let mutation_started = Instant::now();
    app.world_mut().entity_mut(door).insert(ColliderDisabled);
    app.update();
    let withdrawn = app
        .world()
        .resource::<InvalidationWitness>()
        .0
        .as_ref()
        .unwrap();
    assert_eq!(withdrawn.status, NavigationStatus::Stale);
    assert_eq!(withdrawn.token, previous.token);
    assert_eq!(withdrawn.revision, previous.revision);
    assert_eq!(withdrawn.source_hash, previous.source_hash);
    assert_eq!(withdrawn.retained_bytes, 0);
    eprintln!("inflight immediate withdrawal: {withdrawn:?}");
    let mut replacement = None;
    step_until(
        &mut app,
        Duration::from_secs(30).saturating_sub(mutation_started.elapsed()),
        |app| {
            let nav = app.world().resource::<NavigationRuntime>();
            let Some(tile) = nav.tiles().into_iter().find(|tile| tile.key == affected) else {
                return false;
            };
            if replacement.is_none()
                && tile.token > previous.token
                && tile.revision > previous.revision
                && tile.source_hash != previous.source_hash
            {
                eprintln!("inflight replacement receipt: {tile:?}");
                replacement = Some(tile.clone());
            }
            replacement.as_ref().is_some_and(|receipt| {
                tile.token == receipt.token
                    && tile.revision == receipt.revision
                    && tile.source_hash == receipt.source_hash
                    && tile.status == NavigationStatus::Ready
                    && tile.polygons > 0
                    && nav.rejected_stale_results() > rejected_before
            })
        },
    );
    let nav = app.world().resource::<NavigationRuntime>();
    eprintln!(
        "inflight after mutation: rejected={}, active={}, pending={}, affected={:?}, tiles={:?}",
        nav.rejected_stale_results(),
        nav.active_jobs(),
        nav.pending_jobs(),
        nav.tiles().into_iter().find(|tile| tile.key == affected),
        nav.tiles(),
    );
    assert!(
        nav.tiles()
            .iter()
            .any(|tile| tile.key == affected && tile.revision >= 2)
    );
    let current = nav
        .tiles()
        .into_iter()
        .find(|tile| tile.key == affected)
        .unwrap();
    let replacement = replacement.unwrap();
    assert_eq!(current.token, replacement.token);
    assert_eq!(current.revision, replacement.revision);
    assert_eq!(current.source_hash, replacement.source_hash);
    assert_eq!(current.status, NavigationStatus::Ready);
    assert!(nav.rejected_stale_results() > rejected_before);
}

#[test]
fn disconnected_destination_never_falls_back_to_a_straight_kcc_command() {
    let (mut app, door, npc) = app();
    app.world_mut()
        .entity_mut(door)
        .insert(Collider::cuboid(0.2, 2.0, 40.0));
    step_until(&mut app, Duration::from_secs(30), |app| {
        app.world()
            .resource::<NavigationRuntime>()
            .actors()
            .iter()
            .any(|actor| actor.actor == npc && actor.status == NavigationStatus::NoPath)
    });
    let actor = entity(&mut app, npc);
    let before = app.world().get::<Transform>(actor).unwrap().translation;
    for _ in 0..180 {
        app.update();
    }
    let after = app.world().get::<Transform>(actor).unwrap().translation;
    assert!((after - before).with_y(0.0).length() < 0.01);
    let observation = app
        .world()
        .resource::<NavigationRuntime>()
        .actors()
        .into_iter()
        .find(|actor| actor.actor == npc)
        .unwrap();
    assert_eq!(observation.status, NavigationStatus::NoPath);
    assert!(Vec3::from_array(observation.desired_velocity).length() < f32::EPSILON);
    assert!(observation.grounded && observation.collision_count > 0);
}

#[test]
fn moving_dynamic_body_keeps_static_tile_receipts_current() {
    let (mut app, _door, _npc) = app();
    let moving = app
        .world_mut()
        .spawn((
            RigidBody::Dynamic,
            Collider::ball(0.5),
            GravityScale(0.0),
            Velocity::linear(Vec3::X),
            Transform::from_xyz(8.0, 1.0, 15.0),
        ))
        .id();
    step_until(&mut app, Duration::from_secs(30), |app| {
        let nav = app.world().resource::<NavigationRuntime>();
        !nav.tiles().is_empty()
            && nav.active_jobs() == 0
            && nav.pending_jobs() == 0
            && nav.tiles().iter().all(|tile| {
                matches!(
                    tile.status,
                    NavigationStatus::Ready | NavigationStatus::EmptySource
                )
            })
    });
    let before = app.world().resource::<NavigationRuntime>().tiles();
    let body_before = app.world().get::<Transform>(moving).unwrap().translation;
    for _ in 0..30 {
        app.update();
    }
    let body_after = app.world().get::<Transform>(moving).unwrap().translation;
    assert!(body_after.x - body_before.x > 0.4);
    let after = app.world().resource::<NavigationRuntime>().tiles();
    for old in &before {
        if let Some(tile) = after.iter().find(|tile| tile.key == old.key) {
            assert_eq!(
                tile.token, old.token,
                "moving dynamics must not rebuild a static raster"
            );
            assert_eq!(tile.source_hash, old.source_hash);
        }
    }
}
fn actor(
    name: &str,
    role: ActorRole,
    position: CoreVec3,
    patrol: Vec<CoreVec3>,
) -> ActorDefinition {
    ActorDefinition {
        id: id(name),
        name: name.into(),
        role,
        position,
        max_vitality: 100,
        protection: 0,
        inventory_capacity: 8,
        dialogue: None,
        patrol,
        flees_danger: false,
    }
}
fn app() -> (App, Entity, EntityId) {
    let npc = id("patrol");
    let document = GameplayDocument {
        schema: GAMEPLAY_SCHEMA.into(),
        version: GAMEPLAY_VERSION,
        module_id: ModuleId::deterministic("navigation-product/module"),
        area_id: AreaId::deterministic("navigation-product/area"),
        profile: RulesProfile {
            id: "navigation-product".into(),
            version: 1,
            interaction_range_m: 3.0,
            attack_range_m: 2.0,
            attack_damage: 20,
            attack_stamina_cost: 5,
            max_stamina: 100,
            max_action_receipts: 512,
            max_events_per_tick: 128,
        },
        player: id("player"),
        actors: vec![
            actor(
                "player",
                ActorRole::Player,
                CoreVec3::new(-10.0, 0.95, -12.0),
                vec![],
            ),
            actor(
                "patrol",
                ActorRole::Friendly,
                CoreVec3::new(-8.0, 0.95, -6.0),
                vec![CoreVec3::new(42.0, 0.95, -6.0)],
            ),
        ],
        items: vec![],
        objects: vec![],
        places: vec![],
        dialogues: vec![],
        quests: vec![],
    };
    let runtime =
        GameplayRuntime::new(document, ContentHash::from_bytes([0x97; 32]), None, false).unwrap();
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
                VisualValleyPlayerControllerRecipe.build().unwrap(),
                Vec3::new(-10.0, 0.95, -12.0),
            )
            .with_interactive_input(false),
        )
        .insert_resource(runtime)
        .insert_resource(PlayerSimulationReady(true))
        .add_plugins(GameplayPlugin);
    app.world_mut().spawn((
        RigidBody::Fixed,
        Collider::cuboid(70.0, 0.1, 30.0),
        Transform::from_xyz(10.0, -0.1, -6.0),
    ));
    let door = app
        .world_mut()
        .spawn((
            RigidBody::Fixed,
            Collider::cuboid(0.2, 2.0, 4.0),
            Transform::from_xyz(0.0, 2.0, -6.0),
        ))
        .id();
    app.world_mut()
        .resource_mut::<GameplayRuntime>()
        .set_enabled(true);
    (app, door, npc)
}
fn entity(app: &mut App, id: EntityId) -> Entity {
    let world = app.world_mut();
    world
        .query::<(Entity, &DomainEntityId)>()
        .iter(world)
        .find_map(|(entity, domain)| (domain.0 == id).then_some(entity))
        .unwrap()
}

#[test]
fn removed_authored_actor_releases_its_routes_and_tile_demand() {
    let (mut app, _door, npc) = app();
    step_until(&mut app, Duration::from_secs(30), |app| {
        !app.world()
            .resource::<NavigationRuntime>()
            .actors()
            .is_empty()
    });
    let projected = entity(&mut app, npc);
    let mut document = app
        .world()
        .resource::<GameplayRuntime>()
        .session()
        .document()
        .clone();
    document.actors.retain(|actor| actor.id != npc);
    let mut replacement =
        GameplayRuntime::new(document, ContentHash::from_bytes([0x98; 32]), None, false).unwrap();
    replacement.set_enabled(true);
    app.insert_resource(replacement);
    app.world_mut().despawn(projected);
    app.update();
    let navigation = app.world().resource::<NavigationRuntime>();
    assert!(navigation.actors().is_empty());
    assert!(navigation.tiles().is_empty());
    assert_eq!(navigation.pending_jobs(), 0);
    step_until(&mut app, Duration::from_secs(30), |app| {
        app.world().resource::<NavigationRuntime>().active_jobs() == 0
    });
}
fn step_until(app: &mut App, timeout: Duration, mut done: impl FnMut(&mut App) -> bool) {
    let deadline = Instant::now() + timeout;
    while Instant::now() < deadline {
        app.update();
        std::thread::yield_now();
        if done(app) {
            return;
        }
    }
    panic!(
        "navigation condition timed out: tiles={:?}, actors={:?}",
        app.world().resource::<NavigationRuntime>().tiles(),
        app.world().resource::<NavigationRuntime>().actors()
    );
}

#[test]
fn authored_patrol_uses_real_kcc_ground_detour_and_connected_tile_seams() {
    let (mut app, _door, npc) = app();
    let mut seen_ground = false;
    let mut detour = 0.0_f32;
    let mut maximum_speed = 0.0_f32;
    let mut maximum_requested_horizontal_speed = 0.0_f32;
    let mut maximum_requested_speed = 0.0_f32;
    let mut current_mobile_feedback_count = 0;
    let mut pending_feedback_count = 0;
    step_until(&mut app, Duration::from_secs(30), |app| {
        let actor = entity(app, npc);
        let motion = app.world().get::<DangerMotionState>(actor).unwrap();
        seen_ground |= motion.grounded && motion.collision_count > 0;
        let fixed_time = app.world().resource::<Time<Fixed>>();
        let dt = fixed_time.timestep().as_secs_f32();
        if let Some(requested) = motion.requested_translation {
            let requested_horizontal_speed = requested.with_y(0.0).length() / dt;
            assert!(
                requested_horizontal_speed <= 2.001,
                "horizontal command exceeds the 2 m/s profile: requested={requested:?}, dt={dt}"
            );
            if motion.feedback_current {
                let requested_speed = requested.length() / dt;
                let speed = motion.effective_translation.with_y(0.0).length() / dt;
                // Gravity is part of the 3D request. Rapier projects it onto the contact
                // tangent, so resolved XZ can exceed the horizontal command without
                // exceeding the length of the physical request (plus numerical error).
                assert!(
                    speed <= requested_speed + 0.001,
                    "resolved XZ exceeds the same-step 3D request: actual={speed}, requested_3d={requested_speed}, requested={requested:?}, effective={:?}, dt={dt}",
                    motion.effective_translation,
                );
                if speed > maximum_speed && speed > 2.001 {
                    log_patrol_speed_witness(app, actor, speed);
                }
                maximum_speed = maximum_speed.max(speed);
                maximum_requested_speed = maximum_requested_speed.max(requested_speed);
                if requested_horizontal_speed > 1.9 && speed > 1.9 {
                    current_mobile_feedback_count += 1;
                }
            } else {
                assert_eq!(motion.effective_translation, Vec3::ZERO);
                assert!(!motion.grounded);
                assert_eq!(motion.collision_count, 0);
                if pending_feedback_count == 0 {
                    eprintln!(
                        "initial pending KCC feedback: requested={requested:?}, pose={:?}, effective={:?}, grounded={}, collisions={}",
                        app.world().get::<Transform>(actor).unwrap().translation,
                        motion.effective_translation,
                        motion.grounded,
                        motion.collision_count,
                    );
                }
                pending_feedback_count += 1;
            }
            maximum_requested_horizontal_speed =
                maximum_requested_horizontal_speed.max(requested_horizontal_speed);
        }
        let transform = app.world().get::<Transform>(actor).unwrap();
        detour = detour.max((transform.translation.z + 6.0).abs());
        transform.translation.distance(Vec3::new(42.0, 0.95, -6.0)) < 0.4
    });
    assert!(
        seen_ground,
        "initial and ongoing ground support must be real KCC contact"
    );
    assert!(detour > 3.5, "planner must route around the installed wall");
    assert!(maximum_requested_horizontal_speed > 1.9);
    assert!(current_mobile_feedback_count > 0);
    eprintln!(
        "patrol speed maxima: requested_xz={maximum_requested_horizontal_speed}, current_requested_3d={maximum_requested_speed}, current_resolved_xz={maximum_speed}, current_mobile_feedback_count={current_mobile_feedback_count}, pending_feedback_count={pending_feedback_count}"
    );
    let runtime = app.world().resource::<NavigationRuntime>();
    assert!(
        runtime
            .tiles()
            .iter()
            .filter(|tile| tile.status == NavigationStatus::Ready)
            .count()
            >= 3
    );
    assert!(runtime.active_jobs() <= 2 && runtime.pending_jobs() <= 8);
}

#[test]
fn exact_curved_rapier_obstacle_blocks_navigation_without_a_walkable_tessellation() {
    let (mut app, obstacle, npc) = app();
    app.world_mut()
        .entity_mut(obstacle)
        .insert(Collider::cylinder(2.0, 4.0));
    let mut detour = 0.0_f32;
    let mut minimum_distance = f32::INFINITY;
    step_until(&mut app, Duration::from_secs(30), |app| {
        let actor = entity(app, npc);
        let center = app.world().get::<Transform>(actor).unwrap().translation;
        detour = detour.max((center.z + 6.0).abs());
        minimum_distance = minimum_distance.min(Vec2::new(center.x, center.z + 6.0).length());
        center.distance(Vec3::new(42.0, 0.95, -6.0)) < 0.4
    });
    assert!(detour > 4.3);
    assert!(minimum_distance > 4.34);
    assert!(
        app.world()
            .get::<Collider>(obstacle)
            .unwrap()
            .raw
            .as_cylinder()
            .is_some(),
        "KCC must keep the exact installed cylinder"
    );
}

#[test]
fn generation_profile_matches_the_current_projected_capsule_and_kcc() {
    use bevy_rapier3d::control::{CharacterLength, KinematicCharacterController};
    let (mut app, _door, npc) = app();
    app.update();
    let actor = entity(&mut app, npc);
    let profile = app.world().resource::<NavigationRuntime>().profile();
    let collider = app.world().get::<Collider>(actor).unwrap();
    let capsule = collider.raw.as_capsule().unwrap();
    let height = capsule.segment.a.distance(capsule.segment.b) + 2.0 * capsule.radius;
    assert!((height - profile.height_m).abs() < 1.0e-5);
    assert!((capsule.radius - profile.radius_m).abs() < 1.0e-5);
    let controller = app
        .world()
        .get::<KinematicCharacterController>(actor)
        .unwrap();
    let CharacterLength::Absolute(step) = controller.autostep.unwrap().max_height else {
        panic!("expected absolute step height");
    };
    let Some(CharacterLength::Absolute(snap)) = controller.snap_to_ground else {
        panic!("expected absolute ground snap");
    };
    assert!((step - profile.step_m).abs() < f32::EPSILON);
    assert!((snap - profile.snap_m).abs() < f32::EPSILON);
    assert!((controller.max_slope_climb_angle - profile.max_slope_radians).abs() < f32::EPSILON);
}

#[test]
fn invalid_policy_cost_does_not_mutate_accepted_policy() {
    use forge_engine::navigation::NavigationPolicy;
    let mut policy = NavigationPolicy::default();
    let source = "ab".repeat(32);
    policy
        .replace(
            source.clone(),
            vec![],
            std::collections::BTreeMap::from([(2, 3.0)]),
        )
        .unwrap();
    let before = policy.revision();
    assert!(
        policy
            .replace(
                "cd".repeat(32),
                vec![],
                std::collections::BTreeMap::from([(2, f32::NAN)])
            )
            .is_err()
    );
    assert_eq!(policy.source_hash(), source);
    assert_eq!(policy.revision(), before);
}

#[test]
fn room_door_and_low_overhead_clearance_use_the_current_190cm_kcc_profile() {
    let (mut app, door, npc) = app();
    app.world_mut()
        .entity_mut(door)
        .insert(Collider::cuboid(0.2, 2.0, 0.8));
    app.world_mut().spawn((
        RigidBody::Fixed,
        Collider::compound(vec![
            (
                Vec3::new(0.0, 0.0, -20.4),
                Quat::IDENTITY,
                Collider::cuboid(0.2, 2.0, 19.6),
            ),
            (
                Vec3::new(0.0, 0.0, 20.4),
                Quat::IDENTITY,
                Collider::cuboid(0.2, 2.0, 19.6),
            ),
        ]),
        Transform::from_xyz(0.0, 2.0, -6.0),
    ));
    let ceiling = app
        .world_mut()
        .spawn((
            RigidBody::Fixed,
            Collider::cuboid(0.2, 1.125, 0.8),
            Transform::from_xyz(0.0, 2.875, -6.0),
        ))
        .id();
    let no_path = |app: &mut App| {
        app.world()
            .resource::<NavigationRuntime>()
            .actors()
            .iter()
            .any(|actor| actor.actor == npc && actor.status == NavigationStatus::NoPath)
    };
    step_until(&mut app, Duration::from_secs(30), no_path);
    app.world_mut().entity_mut(door).insert(ColliderDisabled);
    // Opening the door cannot make a 1.75 m overhead passage fit this NPC.
    step_until(&mut app, Duration::from_secs(30), |app| {
        let nav = app.world().resource::<NavigationRuntime>();
        nav.active_jobs() == 0
            && nav.pending_jobs() == 0
            && nav.tiles().iter().all(|tile| {
                matches!(
                    tile.status,
                    NavigationStatus::Ready | NavigationStatus::EmptySource
                )
            })
            && no_path(app)
    });
    for _ in 0..60 {
        app.update();
    }
    let actor = entity(&mut app, npc);
    assert!(app.world().get::<Transform>(actor).unwrap().translation.x < -7.5);
    app.world_mut().entity_mut(ceiling).insert((
        Collider::cuboid(0.2, 0.9, 0.8),
        Transform::from_xyz(0.0, 3.1, -6.0),
    ));
    step_until(&mut app, Duration::from_secs(30), |app| {
        let actor = entity(app, npc);
        app.world()
            .get::<Transform>(actor)
            .unwrap()
            .translation
            .distance(Vec3::new(42.0, 0.95, -6.0))
            < 0.4
    });
    let observation = app
        .world()
        .resource::<NavigationRuntime>()
        .actors()
        .into_iter()
        .find(|actor| actor.actor == npc)
        .unwrap();
    assert!(observation.grounded && observation.collision_count > 0);
    assert!(
        (app.world()
            .resource::<NavigationRuntime>()
            .profile()
            .height_m
            - 1.9)
            .abs()
            < f32::EPSILON
    );
}

#[test]
fn changing_installed_door_state_invalidates_only_affected_tiles() {
    let (mut app, door, _npc) = app();
    step_until(&mut app, Duration::from_secs(30), |app| {
        let nav = app.world().resource::<NavigationRuntime>();
        !nav.tiles().is_empty()
            && nav.active_jobs() == 0
            && nav.pending_jobs() == 0
            && nav.tiles().iter().all(|tile| {
                matches!(
                    tile.status,
                    NavigationStatus::Ready | NavigationStatus::EmptySource
                )
            })
    });
    let before = app.world().resource::<NavigationRuntime>().tiles();
    app.world_mut().entity_mut(door).insert(ColliderDisabled);
    app.update();
    let after = app.world().resource::<NavigationRuntime>().tiles();
    assert!(
        after.iter().any(|tile| matches!(
            tile.status,
            NavigationStatus::WaitingForSource
                | NavigationStatus::Stale
                | NavigationStatus::Queued
                | NavigationStatus::Generating
        )),
        "changed source must withdraw its live old route"
    );
    step_until(&mut app, Duration::from_secs(30), |app| {
        app.world()
            .resource::<NavigationRuntime>()
            .tiles()
            .iter()
            .all(|tile| {
                matches!(
                    tile.status,
                    NavigationStatus::Ready | NavigationStatus::EmptySource
                )
            })
    });
    let restored = app.world().resource::<NavigationRuntime>().tiles();
    assert!(restored.iter().any(|tile| {
        before
            .iter()
            .any(|old| tile.key == old.key && tile.source_hash != old.source_hash)
    }));
    assert!(
        restored
            .iter()
            .any(|tile| before.iter().any(|old| tile.key == old.key
                && tile.source_hash == old.source_hash
                && tile.token == old.token)),
        "unaffected tile receipts must remain unchanged"
    );
}

#[test]
fn living_idle_actor_prewarms_then_physically_escapes_an_authored_heat_danger() {
    use forge_core::CommandId;
    use forge_gameplay::{GameAction, GameRequest, ObjectDefinition, ObjectKind, ObjectPhysics};
    let (mut app, _door, npc) = app();
    let mut document = app
        .world()
        .resource::<GameplayRuntime>()
        .session()
        .document()
        .clone();
    let definition = document
        .actors
        .iter_mut()
        .find(|actor| actor.id == npc)
        .unwrap();
    definition.position = CoreVec3::new(-8.0, 0.95, -10.0);
    definition.patrol.clear();
    definition.flees_danger = true;
    let fire = id("idle-flee-fire");
    document.objects.push(ObjectDefinition {
        id: fire,
        name: "Authored heat source".into(),
        position: CoreVec3::new(-8.0, 0.5, -11.5),
        kind: ObjectKind::Prop,
        physics: ObjectPhysics {
            body_half_extents_m: CoreVec3::new(0.3, 0.5, 0.3),
            flammable: true,
            heat_damage_per_tick: 1,
            ..ObjectPhysics::default()
        },
    });
    let mut runtime =
        GameplayRuntime::new(document, ContentHash::from_bytes([0x99; 32]), None, false).unwrap();
    runtime.set_enabled(true);
    app.insert_resource(runtime);
    step_until(&mut app, Duration::from_secs(30), |app| {
        let nav = app.world().resource::<NavigationRuntime>();
        nav.actors().iter().any(|actor| {
            actor.actor == npc && actor.target.is_none() && !actor.required_tiles.is_empty()
        }) && nav.active_jobs() == 0
            && nav.pending_jobs() == 0
            && !nav.tiles().is_empty()
            && nav.tiles().iter().all(|tile| {
                matches!(
                    tile.status,
                    NavigationStatus::Ready | NavigationStatus::EmptySource
                )
            })
    });
    let projected = entity(&mut app, npc);
    let before = app.world().get::<Transform>(projected).unwrap().translation;
    let (player, sequence) = {
        let runtime = app.world().resource::<GameplayRuntime>();
        (
            runtime.session().document().player,
            runtime.session().state().sequence,
        )
    };
    app.world_mut()
        .resource_mut::<GameplayRuntime>()
        .queue(GameRequest {
            command_id: CommandId::deterministic("navigation-product/ignite-idle-danger"),
            expected_sequence: sequence,
            actor: player,
            action: GameAction::Ignite { object: fire },
        })
        .unwrap();
    let mut saw_fleeing = false;
    let mut saw_ground = false;
    step_until(&mut app, Duration::from_secs(30), |app| {
        let runtime = app.world().resource::<GameplayRuntime>();
        let state = &runtime.session().state().actors[&npc];
        saw_fleeing |= state.fleeing;
        let feedback = app.world().get::<DangerMotionState>(projected).unwrap();
        saw_ground |= feedback.grounded && feedback.collision_count > 0;
        let center = app.world().get::<Transform>(projected).unwrap().translation;
        center.distance(Vec3::new(-8.0, 0.5, -11.5)) > 3.0 && state.vitality > 0
    });
    let runtime = app.world().resource::<GameplayRuntime>();
    assert!(
        runtime.session().state().objects[&fire].burning,
        "public action must really ignite the authored source"
    );
    let state = &runtime.session().state().actors[&npc];
    assert!(
        state.vitality > 0 && state.vitality < 100,
        "NPC must survive actual measured heat exposure"
    );
    assert!(saw_fleeing && saw_ground);
    let after = app.world().get::<Transform>(projected).unwrap().translation;
    assert!(
        (after - before).with_y(0.0).length() > 1.4,
        "a fleeing flag alone does not prove escape"
    );
}

#[test]
fn curved_obstacle_vertical_volume_preserves_high_clearance_and_blocks_low_clearance() {
    for (height, high_passage) in [(3.0, true), (1.7, false)] {
        let (mut app, obstacle, npc) = app();
        app.world_mut().entity_mut(obstacle).insert((
            Collider::cylinder(0.2, 4.0),
            Transform::from_xyz(0.0, height, -6.0),
        ));
        let mut detour = 0.0_f32;
        let mut saw_ground = false;
        step_until(&mut app, Duration::from_secs(30), |app| {
            let projected = entity(app, npc);
            let center = app.world().get::<Transform>(projected).unwrap().translation;
            let feedback = app.world().get::<DangerMotionState>(projected).unwrap();
            saw_ground |= feedback.grounded && feedback.collision_count > 0;
            detour = detour.max((center.z + 6.0).abs());
            center.distance(Vec3::new(42.0, 0.95, -6.0)) < 0.4
        });
        assert!(saw_ground);
        if high_passage {
            assert!(
                detour < 1.0,
                "the 2.8 m underside must leave this 1.9 m NPC passage open"
            );
        } else {
            assert!(
                detour > 4.3,
                "the 1.5 m underside must block this 1.9 m NPC route"
            );
        }
        assert!(
            app.world()
                .resource::<NavigationRuntime>()
                .tiles()
                .iter()
                .any(|tile| tile.curved_obstacles > 0)
        );
    }
}

#[derive(Resource, Default)]
struct PendingScaleGuardReceipt {
    armed: bool,
    tiles: Option<Vec<forge_engine::navigation::NavigationTileObservation>>,
}

#[test]
fn enlarged_pending_shape_withdraws_boundary_tile_before_sync_and_rejects_old_job() {
    use bevy_rapier3d::prelude::PhysicsSet;
    use forge_engine::navigation::NavigationSet;
    let (mut app, obstacle, _npc) = app();
    app.world_mut()
        .entity_mut(obstacle)
        .insert(Transform::from_xyz(31.8, 2.0, -6.0));
    app.init_resource::<PendingScaleGuardReceipt>().add_systems(
        FixedUpdate,
        (|navigation: Res<NavigationRuntime>, mut receipt: ResMut<PendingScaleGuardReceipt>| {
            if receipt.armed && receipt.tiles.is_none() {
                receipt.tiles = Some(navigation.tiles());
            }
        })
        .after(NavigationSet::Guard)
        .before(PhysicsSet::SyncBackend),
    );
    let affected = TileKey { x: 1, z: -1 };
    step_until(&mut app, Duration::from_secs(30), |app| {
        app.world()
            .resource::<NavigationRuntime>()
            .tiles()
            .iter()
            .any(|tile| tile.key == affected && tile.status == NavigationStatus::Generating)
    });
    let original = app
        .world()
        .resource::<NavigationRuntime>()
        .tiles()
        .into_iter()
        .find(|tile| tile.key == affected)
        .unwrap();
    app.world_mut().entity_mut(obstacle).insert(Transform {
        translation: Vec3::new(31.8, 2.0, -6.0),
        scale: Vec3::new(12.0, 1.0, 1.0),
        ..default()
    });
    app.world_mut()
        .resource_mut::<PendingScaleGuardReceipt>()
        .armed = true;
    step_until(&mut app, Duration::from_secs(30), |app| {
        app.world()
            .resource::<NavigationRuntime>()
            .rejected_stale_results()
            > 0
    });
    let observed = app
        .world()
        .resource::<PendingScaleGuardReceipt>()
        .tiles
        .as_ref()
        .unwrap();
    assert!(
        observed
            .iter()
            .any(|tile| tile.key == affected && tile.status == NavigationStatus::Stale),
        "new extent must withdraw an old boundary tile before Rapier prepares the new scale"
    );
    step_until(&mut app, Duration::from_secs(30), |app| {
        app.world()
            .resource::<NavigationRuntime>()
            .tiles()
            .iter()
            .any(|tile| {
                tile.key == affected
                    && tile.status == NavigationStatus::Ready
                    && tile.revision > original.revision
            })
    });
    let current = app
        .world()
        .resource::<NavigationRuntime>()
        .tiles()
        .into_iter()
        .find(|tile| tile.key == affected)
        .unwrap();
    assert_ne!(current.source_hash, original.source_hash);
    assert!(
        current.source_triangles > original.source_triangles,
        "only the newly installed exact shape may supply the additional tile triangles"
    );
    assert!(
        app.world()
            .get::<Collider>(obstacle)
            .unwrap()
            .scale()
            .abs_diff_eq(Vec3::new(12.0, 1.0, 1.0), 1.0e-5)
    );
}

#[test]
fn public_navigation_feedback_is_the_completed_current_kcc_step_and_clears_when_paused() {
    use bevy_rapier3d::prelude::KinematicCharacterControllerOutput;
    let (mut app, _door, npc) = app();
    step_until(&mut app, Duration::from_secs(30), |app| {
        app.world()
            .resource::<NavigationRuntime>()
            .actors()
            .iter()
            .any(|actor| {
                actor.actor == npc
                    && actor.feedback_tick.is_some()
                    && actor.status == NavigationStatus::Moving
            })
    });
    let projected = entity(&mut app, npc);
    for _ in 0..8 {
        app.update();
        let tick = app
            .world()
            .resource::<GameplayRuntime>()
            .session()
            .state()
            .tick;
        let observation = app
            .world()
            .resource::<NavigationRuntime>()
            .actors()
            .into_iter()
            .find(|actor| actor.actor == npc)
            .unwrap();
        let output = app
            .world()
            .get::<KinematicCharacterControllerOutput>(projected)
            .unwrap();
        let center = app.world().get::<Transform>(projected).unwrap().translation;
        let profile = app.world().resource::<NavigationRuntime>().profile();
        assert_eq!(observation.feedback_tick, Some(tick));
        assert!(
            Vec3::from_array(observation.feet)
                .abs_diff_eq(center - Vec3::Y * (profile.height_m * 0.5), 1.0e-5)
        );
        assert!(
            Vec3::from_array(observation.actual_velocity)
                .abs_diff_eq(output.effective_translation * 60.0, 1.0e-4)
        );
        assert!(
            Vec3::from_array(observation.requested_translation.unwrap())
                .abs_diff_eq(output.desired_translation, 1.0e-6)
        );
        assert_eq!(observation.grounded, output.grounded);
        assert_eq!(observation.collision_count, output.collisions.len());
    }
    app.world_mut()
        .resource_mut::<GameplayRuntime>()
        .set_enabled(false);
    app.update();
    let observation = app
        .world()
        .resource::<NavigationRuntime>()
        .actors()
        .into_iter()
        .find(|actor| actor.actor == npc)
        .unwrap();
    assert_eq!(observation.status, NavigationStatus::Disabled);
    assert!(observation.requested_translation.is_none() && observation.feedback_tick.is_none());
}

fn assert_cancelled_navigation_budget(app: &App) {
    let navigation = app.world().resource::<NavigationRuntime>();
    assert!(navigation.rejected_stale_results() > 0);
    assert!(navigation.actors().iter().all(|actor| {
        actor
            .desired_velocity
            .iter()
            .all(|component| component.classify() == std::num::FpCategory::Zero)
    }));
    assert!(navigation.tiles().len() <= 64);
}

// Proposal only: real bounded runtime overflow drains jobs and can recover.
#[test]
fn over_budget_corridors_cancel_generation_and_recover_after_authored_removal() {
    let (mut app, _door, npc) = app();
    app.world_mut().spawn((
        RigidBody::Fixed,
        Collider::cuboid(600.0, 0.1, 600.0),
        Transform::from_xyz(0.0, -0.1, 0.0),
    ));
    // Install all authored actors before the production Startup projector runs.
    // Initially their idle tiles overlap, so actual generation can begin.
    let mut document = app
        .world()
        .resource::<GameplayRuntime>()
        .session()
        .document()
        .clone();
    let mut additional = Vec::new();
    for index in 0..7_u16 {
        let name = format!("overflow-{index}");
        let actor = actor(
            &name,
            ActorRole::Friendly,
            CoreVec3::new(-8.0 + f32::from(index) * 0.8, 0.95, -15.0),
            vec![],
        );
        additional.push(actor.id);
        document.actors.push(actor);
    }
    let mut replacement =
        GameplayRuntime::new(document, ContentHash::from_bytes([0xa0; 32]), None, false).unwrap();
    replacement.set_enabled(true);
    app.insert_resource(replacement);
    step_until(&mut app, Duration::from_secs(30), |app| {
        app.world().resource::<NavigationRuntime>().active_jobs() > 0
    });
    let mut document = app
        .world()
        .resource::<GameplayRuntime>()
        .session()
        .document()
        .clone();
    for (index, id) in additional.iter().enumerate() {
        let index = f32::from(u16::try_from(index).unwrap());
        let position = CoreVec3::new(-300.0 + index * 100.0, 0.95, 100.0);
        document
            .actors
            .iter_mut()
            .find(|actor| actor.id == *id)
            .unwrap()
            .position = position;
        // An authored pose revision changes the physical input before SyncBackend.
        // Never modify KCC outputs, navigation statuses or generated receipts.
        let projected = entity(&mut app, *id);
        app.world_mut()
            .entity_mut(projected)
            .insert(Transform::from_xyz(position.x, position.y, position.z));
    }
    let mut replacement =
        GameplayRuntime::new(document, ContentHash::from_bytes([0xa1; 32]), None, false).unwrap();
    replacement.set_enabled(true);
    app.insert_resource(replacement);
    step_until(&mut app, Duration::from_secs(30), |app| {
        let navigation = app.world().resource::<NavigationRuntime>();
        navigation
            .actors()
            .iter()
            .any(|actor| actor.status == NavigationStatus::BudgetExceeded)
            && navigation.pending_jobs() == 0
            && navigation.active_jobs() == 0
    });
    assert_cancelled_navigation_budget(&app);
    let mut document = app
        .world()
        .resource::<GameplayRuntime>()
        .session()
        .document()
        .clone();
    document
        .actors
        .retain(|actor| !additional.contains(&actor.id));
    let projected: Vec<_> = additional
        .into_iter()
        .map(|id| entity(&mut app, id))
        .collect();
    let mut replacement =
        GameplayRuntime::new(document, ContentHash::from_bytes([0xa2; 32]), None, false).unwrap();
    replacement.set_enabled(true);
    app.insert_resource(replacement);
    for projected in projected {
        app.world_mut().despawn(projected);
    }
    step_until(&mut app, Duration::from_secs(30), |app| {
        let navigation = app.world().resource::<NavigationRuntime>();
        navigation
            .actors()
            .iter()
            .any(|actor| actor.actor == npc && actor.status == NavigationStatus::Moving)
    });
    assert_eq!(
        app.world().resource::<NavigationRuntime>().actors().len(),
        1
    );
}
