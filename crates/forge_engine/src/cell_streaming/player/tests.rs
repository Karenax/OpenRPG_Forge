use super::*;
use crate::{PendingPlayerControlCommand, PlayerControlCommand, PlayerControllerPlugin};
use bevy::time::TimeUpdateStrategy;
use bevy_rapier3d::prelude::Velocity;
use std::time::{Duration, Instant};

fn player_app() -> (App, Entity) {
    let mut app = super::super::tests::app_with_cell_budget(64);
    app.add_plugins(TransformPlugin)
        .insert_resource(TimeUpdateStrategy::ManualDuration(Duration::from_secs_f64(
            1.0 / 60.0,
        )));
    crate::sampled_terrain_physics::register(&mut app);
    let spawn = Vec3::new(-64.0, 2.0, -64.0);
    app.add_plugins(
        PlayerControllerPlugin::new(
            forge_world::embedded_visual_valley_player_controller_profile().unwrap(),
            spawn,
        )
        .with_interactive_input(false)
        .with_initial_camera_target(spawn + Vec3::NEG_Z),
    )
    .add_plugins(PlayerCellStreamingPlugin);
    let body = app
        .world_mut()
        .spawn((
            RigidBody::Dynamic,
            Collider::cuboid(0.5, 0.5, 0.5),
            Velocity::default(),
            Transform::from_xyz(96.0, 2.0, 96.0),
        ))
        .id();
    (app, body)
}

fn ready(app: &mut App) {
    let deadline = Instant::now() + Duration::from_secs(10);
    loop {
        app.update();
        let status = app.world().resource::<PlayerCellStreamingStatus>();
        assert!(status.error.is_none(), "{:?}", status.error);
        if status.ready {
            return;
        }
        assert!(
            Instant::now() < deadline,
            "Protected physics never became ready"
        );
        std::thread::sleep(Duration::from_millis(1));
    }
}

#[test]
fn shared_hold_stops_every_controller_and_physics_context_without_releasing_itself() {
    let mut world = World::new();
    world.insert_resource(PlayerSimulationReady(true));
    let contexts = [
        world.spawn(RapierConfiguration::new(1.0)).id(),
        world.spawn(RapierConfiguration::new(1.0)).id(),
    ];
    let controllers = [
        world
            .spawn(KinematicCharacterController {
                translation: Some(Vec3::X),
                ..default()
            })
            .id(),
        world
            .spawn(KinematicCharacterController {
                translation: Some(Vec3::ZERO),
                ..default()
            })
            .id(),
    ];

    hold_player_simulation(&mut world);
    hold_player_simulation(&mut world);

    assert!(!world.resource::<PlayerSimulationReady>().0);
    for entity in contexts {
        assert!(
            !world
                .get::<RapierConfiguration>(entity)
                .unwrap()
                .physics_pipeline_active
        );
    }
    for entity in controllers {
        assert_eq!(
            world
                .get::<KinematicCharacterController>(entity)
                .unwrap()
                .translation,
            None
        );
    }
}

#[test]
fn temporary_hold_resumes_only_its_own_changes_and_preserves_other_pauses() {
    #[derive(Resource, Default)]
    struct Guard(TemporarySimulationHold);
    #[derive(Resource, Default)]
    struct Controls {
        pending: bool,
        middle_pause: bool,
        late_pause: bool,
    }
    fn release(world: &mut World) {
        world.resource_scope(|world, mut guard: Mut<Guard>| guard.0.release(world));
    }
    fn hold(world: &mut World) {
        if world.resource::<Controls>().pending {
            world.resource_scope(|world, mut guard: Mut<Guard>| guard.0.hold(world));
        }
    }
    fn pause(world: &mut World) {
        world.resource_mut::<PlayerSimulationReady>().0 = false;
        for mut config in world.query::<&mut RapierConfiguration>().iter_mut(world) {
            config.physics_pipeline_active = false;
        }
    }
    fn middle(world: &mut World) {
        if world.resource::<Controls>().middle_pause {
            pause(world);
        }
    }
    fn late(world: &mut World) {
        if world.resource::<Controls>().late_pause {
            pause(world);
        }
    }
    fn values(app: &App, context: Entity) -> (bool, bool) {
        (
            app.world().resource::<PlayerSimulationReady>().0,
            app.world()
                .get::<RapierConfiguration>(context)
                .unwrap()
                .physics_pipeline_active,
        )
    }
    let mut app = App::new();
    app.init_resource::<Guard>()
        .init_resource::<Controls>()
        .insert_resource(PlayerSimulationReady(true))
        .add_systems(FixedUpdate, (release, hold, middle, hold, late).chain());
    let context = app.world_mut().spawn(RapierConfiguration::new(1.0)).id();
    app.world_mut().resource_mut::<Controls>().pending = true;
    app.world_mut().run_schedule(FixedUpdate);
    assert_eq!(values(&app, context), (false, false));
    app.world_mut().resource_mut::<Controls>().pending = false;
    app.world_mut().run_schedule(FixedUpdate);
    assert_eq!(values(&app, context), (true, true));

    // A separate owner can request a pause while the hold is already active.
    app.world_mut().resource_mut::<Controls>().pending = true;
    app.world_mut().resource_mut::<Controls>().middle_pause = true;
    app.world_mut().run_schedule(FixedUpdate);
    app.world_mut().resource_mut::<Controls>().pending = false;
    app.world_mut().resource_mut::<Controls>().middle_pause = false;
    app.world_mut().run_schedule(FixedUpdate);
    assert_eq!(values(&app, context), (false, false));

    // A pre-existing pause stays paused across a new hold and its release.
    app.world_mut().resource_mut::<Controls>().pending = true;
    app.world_mut().run_schedule(FixedUpdate);
    app.world_mut().resource_mut::<Controls>().pending = false;
    app.world_mut().run_schedule(FixedUpdate);
    assert_eq!(values(&app, context), (false, false));

    app.world_mut().resource_mut::<PlayerSimulationReady>().0 = true;
    app.world_mut()
        .get_mut::<RapierConfiguration>(context)
        .unwrap()
        .physics_pipeline_active = true;
    app.world_mut().resource_mut::<Controls>().pending = true;
    app.world_mut().resource_mut::<Controls>().late_pause = true;
    app.world_mut().run_schedule(FixedUpdate);
    app.world_mut().resource_mut::<Controls>().pending = false;
    app.world_mut().resource_mut::<Controls>().late_pause = false;
    app.world_mut().run_schedule(FixedUpdate);
    assert_eq!(values(&app, context), (false, false));
}

#[test]
fn physics_waits_for_backend_colliders_and_keeps_a_distant_dynamic_body_supported() {
    let (mut app, body) = player_app();
    app.update();
    app.world_mut().run_schedule(FixedUpdate);
    assert!(!app.world().resource::<PlayerSimulationReady>().0);
    assert!((app.world().get::<Transform>(body).unwrap().translation.y - 2.0).abs() < 0.0001);
    ready(&mut app);
    assert!(
        app.world()
            .resource::<PlayerCellStreamingStatus>()
            .protected_cells
            > 1
    );
    for _ in 0..240 {
        app.update();
    }
    let y = app.world().get::<Transform>(body).unwrap().translation.y;
    assert!((0.45..0.6).contains(&y), "Distant body lost its floor: {y}");
    let context = app
        .world_mut()
        .query_filtered::<&RapierConfiguration, With<DefaultRapierContext>>()
        .single(app.world())
        .unwrap();
    assert!(context.physics_pipeline_active);
}

#[test]
fn removed_protected_collision_or_outside_body_holds_all_dynamics_without_moving_them() {
    let (mut app, body) = player_app();
    ready(&mut app);
    let before = app.world().get::<Transform>(body).unwrap().translation;
    let entity = app
        .world()
        .resource::<State>()
        .live
        .values()
        .find(|live| {
            app.world()
                .get::<super::super::StreamedTerrainCell>(live.entity)
                .is_some_and(|cell| cell.detail == CellDetail::Full)
        })
        .unwrap()
        .entity;
    app.world_mut().entity_mut(entity).remove::<Collider>();
    app.update();
    assert!(!app.world().resource::<PlayerSimulationReady>().0);
    assert!(
        app.world()
            .resource::<PlayerCellStreamingStatus>()
            .error
            .is_some()
    );
    assert!(
        app.world()
            .get::<Transform>(body)
            .unwrap()
            .translation
            .abs_diff_eq(before, 0.0001)
    );

    let (mut app, body) = player_app();
    ready(&mut app);
    app.world_mut()
        .get_mut::<Transform>(body)
        .unwrap()
        .translation
        .x = 200.0;
    let before = app.world().get::<Transform>(body).unwrap().translation;
    app.update();
    assert!(!app.world().resource::<PlayerSimulationReady>().0);
    assert!(
        app.world()
            .resource::<PlayerCellStreamingStatus>()
            .error
            .is_some()
    );
    assert!(
        app.world()
            .get::<Transform>(body)
            .unwrap()
            .translation
            .abs_diff_eq(before, 0.0001)
    );
}

#[test]
fn real_controller_crosses_cell_boundaries_and_returns_without_losing_the_distant_floor() {
    let (mut app, body) = player_app();
    ready(&mut app);
    let source = app
        .world()
        .resource::<State>()
        .source
        .terrain()
        .document_hash();
    let id = CellKey { x: 2, z: 2 }.entity_id(
        app.world()
            .resource::<State>()
            .source
            .terrain()
            .document()
            .area_id,
    );
    for target in [48.0, -64.0] {
        let direction = if target > 0.0 { 1.0 } else { -1.0 };
        let deadline = Instant::now() + Duration::from_secs(20);
        loop {
            app.world_mut()
                .resource_mut::<PendingPlayerControlCommand>()
                .submit(PlayerControlCommand {
                    move_axis: Vec2::new(direction, 0.0),
                    ..default()
                })
                .unwrap();
            app.update();
            let status = app.world().resource::<PlayerCellStreamingStatus>();
            assert!(status.error.is_none(), "{:?}", status.error);
            let position = app
                .world()
                .resource::<crate::PlayerRuntimeSnapshot>()
                .player_position;
            assert!(
                position.y > 0.5,
                "Player fell through streamed terrain: {position}"
            );
            if (direction > 0.0 && position.x >= target)
                || (direction < 0.0 && position.x <= target)
            {
                break;
            }
            assert!(
                Instant::now() < deadline,
                "Player stalled at {position}; {status:?}; {:?}",
                app.world().resource::<crate::PlayerRuntimeSnapshot>()
            );
            if !status.ready {
                std::thread::sleep(Duration::from_millis(1));
            }
        }
    }
    ready(&mut app);
    assert!(
        app.world()
            .resource::<super::super::CellStreamingStats>()
            .committed_transitions
            > 3
    );
    let cell = app
        .world_mut()
        .query::<&super::super::StreamedTerrainCell>()
        .iter(app.world())
        .find(|cell| cell.key == CellKey { x: 2, z: 2 })
        .unwrap();
    assert_eq!(cell.logical_id, id);
    assert_eq!(cell.source, source);
    assert!(app.world().get::<Transform>(body).unwrap().translation.y > 0.4);
}

#[test]
fn finite_map_edges_keep_valid_starts_walkable_and_allow_turning_back() {
    for (spawn, outward) in [
        (Vec3::new(127.0, 2.0, 0.0), Vec2::X),
        (Vec3::new(-127.0, 2.0, 0.0), Vec2::NEG_X),
        (Vec3::new(0.0, 2.0, 127.0), Vec2::NEG_Y),
        (Vec3::new(0.0, 2.0, -127.0), Vec2::Y),
        (Vec3::new(127.0, 2.0, -127.0), Vec2::new(1.0, 1.0)),
    ] {
        let (mut app, body) = player_app();
        crate::PlayerSpawnPoint::rebase_before_start(app.world_mut(), spawn).unwrap();
        ready(&mut app);
        for axis in [outward, -outward] {
            drive_ready_edge_steps(&mut app, spawn, axis);
        }
        let player = app.world().resource::<crate::PlayerRuntimeSnapshot>();
        assert!(
            player.player_position.x.abs() < 126.0 && player.player_position.z.abs() < 126.0,
            "Player could not return from {spawn}: {:?}",
            player.player_position
        );
        assert!(app.world().get::<Transform>(body).unwrap().translation.y > 0.4);
    }
}

fn drive_ready_edge_steps(app: &mut App, spawn: Vec3, axis: Vec2) {
    let deadline = Instant::now() + Duration::from_secs(10);
    let mut steps = 0;
    while steps < 180 {
        app.world_mut()
            .resource_mut::<PendingPlayerControlCommand>()
            .submit(PlayerControlCommand {
                move_axis: axis,
                ..default()
            })
            .unwrap();
        app.update();
        let status = app.world().resource::<PlayerCellStreamingStatus>();
        assert!(status.error.is_none(), "{spawn}: {status:?}");
        let advancing = status.ready && app.world().resource::<PlayerSimulationReady>().0;
        let player = app.world().resource::<crate::PlayerRuntimeSnapshot>();
        assert!(
            player.player_position.x.abs() < 128.0,
            "{spawn}, {axis}: {player:?}"
        );
        assert!(
            player.player_position.z.abs() < 128.0,
            "{spawn}, {axis}: {player:?}"
        );
        assert!(player.player_position.y > 0.5);
        assert!(
            Instant::now() < deadline,
            "Edge motion did not receive 180 ready simulation steps: {steps}; {status:?}; {player:?}"
        );
        if advancing {
            steps += 1;
        } else {
            std::thread::sleep(Duration::from_millis(1));
        }
    }
}

#[test]
fn delayed_source_acknowledgement_cannot_count_held_updates_as_edge_motion() {
    let (mut app, _) = player_app();
    let spawn = Vec3::new(127.0, 2.0, 0.0);
    crate::PlayerSpawnPoint::rebase_before_start(app.world_mut(), spawn).unwrap();
    ready(&mut app);
    drive_ready_edge_steps(&mut app, spawn, Vec2::X);
    let before = app
        .world()
        .resource::<crate::PlayerRuntimeSnapshot>()
        .player_position;
    let original = app
        .world()
        .resource::<crate::cell_streaming::ResidentSampledTerrain>()
        .plan()
        .clone();
    let mut document = original.document().clone();
    document.name.push_str(" / delayed receipt");
    let next = std::sync::Arc::new(original.with_document(&document).unwrap());
    crate::cell_streaming::request_source_update(app.world_mut(), original.document_hash(), next)
        .unwrap();
    app.update();
    let job = app
        .world_mut()
        .resource_mut::<crate::cell_streaming::State>()
        .pending
        .take()
        .unwrap();
    let crate::cell_streaming::Job {
        request,
        plan,
        replacement,
        task,
    } = job;
    let (release, received) = std::sync::mpsc::channel();
    let pool = bevy::tasks::AsyncComputeTaskPool::get();
    let gated = pool.spawn(async move {
        if received.recv_timeout(Duration::from_secs(5)).is_err() {
            return (
                Err("Controlled source acknowledgement was not released".into()),
                5000.0,
            );
        }
        task.await
    });
    app.world_mut()
        .resource_mut::<crate::cell_streaming::State>()
        .pending = Some(crate::cell_streaming::Job {
        request,
        plan,
        replacement,
        task: gated,
    });
    for _ in 0..180 {
        app.world_mut()
            .resource_mut::<PendingPlayerControlCommand>()
            .submit(PlayerControlCommand {
                move_axis: Vec2::NEG_X,
                ..default()
            })
            .unwrap();
        app.update();
        assert!(!app.world().resource::<PlayerCellStreamingStatus>().ready);
        assert!(
            app.world()
                .resource::<crate::PlayerRuntimeSnapshot>()
                .player_position
                .abs_diff_eq(before, 0.0001)
        );
    }
    release.send(()).unwrap();
    drive_ready_edge_steps(&mut app, spawn, Vec2::NEG_X);
    let player = app.world().resource::<crate::PlayerRuntimeSnapshot>();
    assert!(player.player_position.x < 126.0, "{player:?}");
}

#[test]
fn late_oversized_character_command_cannot_bypass_the_physics_hold() {
    #[allow(clippy::needless_pass_by_value)] // Bevy system parameters.
    fn oversized(mut controllers: Query<&mut KinematicCharacterController>) {
        for mut controller in &mut controllers {
            controller.translation = Some(Vec3::X * 40.0);
        }
    }
    let (mut app, body) = player_app();
    ready(&mut app);
    let before = app
        .world()
        .resource::<crate::PlayerRuntimeSnapshot>()
        .player_position;
    let body_before = app.world().get::<Transform>(body).unwrap().translation;
    app.add_systems(
        FixedUpdate,
        oversized
            .after(PlayerControllerSet::Drive)
            .before(PlayerCellStreamingSet::Enforce),
    );
    app.update();
    assert!(!app.world().resource::<PlayerCellStreamingStatus>().ready);
    assert!(
        app.world()
            .resource::<PlayerCellStreamingStatus>()
            .error
            .is_some()
    );
    assert!(
        app.world()
            .resource::<crate::PlayerRuntimeSnapshot>()
            .player_position
            .abs_diff_eq(before, 0.0001),
        "Before {before:?}; after {:?}; status {:?}",
        app.world().resource::<crate::PlayerRuntimeSnapshot>(),
        app.world().resource::<PlayerCellStreamingStatus>()
    );
    assert!(
        app.world()
            .get::<Transform>(body)
            .unwrap()
            .translation
            .abs_diff_eq(body_before, 0.0001)
    );
}

#[test]
fn altered_physical_map_limits_hold_every_body_before_backend_sync() {
    use bevy_rapier3d::prelude::{ColliderDisabled, Sensor};
    for fault in 0..5 {
        let (mut app, body) = player_app();
        ready(&mut app);
        let player_before = app
            .world()
            .resource::<crate::PlayerRuntimeSnapshot>()
            .player_position;
        let body_before = app.world().get::<Transform>(body).unwrap().translation;
        let edge = app
            .world_mut()
            .query_filtered::<Entity, With<crate::cell_streaming::StreamedTerrainBoundary>>()
            .iter(app.world())
            .next()
            .unwrap();
        match fault {
            0 => {
                app.world_mut().entity_mut(edge).remove::<Collider>();
            }
            1 => {
                app.world_mut()
                    .get_mut::<Transform>(edge)
                    .unwrap()
                    .translation
                    .x += 0.25;
            }
            2 => {
                app.world_mut().entity_mut(edge).insert(Collider::ball(0.5));
            }
            3 => {
                app.world_mut().entity_mut(edge).insert(ColliderDisabled);
            }
            _ => {
                app.world_mut().entity_mut(edge).insert(Sensor);
            }
        }
        app.update();
        let status = app.world().resource::<PlayerCellStreamingStatus>();
        assert!(
            !status.ready
                && status
                    .error
                    .as_deref()
                    .is_some_and(|s| s.contains("boundary"))
        );
        assert!(
            app.world()
                .resource::<crate::PlayerRuntimeSnapshot>()
                .player_position
                .abs_diff_eq(player_before, 0.0001)
        );
        assert!(
            app.world()
                .get::<Transform>(body)
                .unwrap()
                .translation
                .abs_diff_eq(body_before, 0.0001)
        );
    }
}

#[test]
fn physical_map_limits_do_not_admit_a_dynamic_sweep_outside_the_grid() {
    let (mut app, body) = player_app();
    ready(&mut app);
    app.world_mut()
        .get_mut::<Transform>(body)
        .unwrap()
        .translation
        .x = 120.0;
    app.world_mut().get_mut::<Velocity>(body).unwrap().linear.x = 1000.0;
    let before = app.world().get::<Transform>(body).unwrap().translation;
    app.update();
    assert!(!app.world().resource::<PlayerCellStreamingStatus>().ready);
    assert!(
        app.world()
            .resource::<PlayerCellStreamingStatus>()
            .error
            .is_some()
    );
    assert!(
        app.world()
            .get::<Transform>(body)
            .unwrap()
            .translation
            .abs_diff_eq(before, 0.0001)
    );
}

#[test]
fn a_native_recovery_hold_remains_authoritative_after_streaming_is_ready() {
    #[allow(clippy::needless_pass_by_value)] // Bevy system parameters.
    fn recovery(mut ready: ResMut<PlayerSimulationReady>) {
        ready.0 = false;
    }
    let (mut app, body) = player_app();
    ready(&mut app);
    let body_before = app.world().get::<Transform>(body).unwrap().translation;
    app.add_systems(
        FixedUpdate,
        recovery
            .in_set(crate::native_gameplay::NativeGameplayReadinessSet)
            .before(PlayerControllerSet::Drive),
    );
    app.update();
    assert!(!app.world().resource::<PlayerSimulationReady>().0);
    assert!(!app.world().resource::<PlayerCellStreamingStatus>().ready);
    assert!(
        app.world()
            .get::<Transform>(body)
            .unwrap()
            .translation
            .abs_diff_eq(body_before, 0.0001)
    );
}

#[test]
fn a_second_physics_context_fails_closed_instead_of_leaving_foreign_dynamics_running() {
    let (mut app, body) = player_app();
    ready(&mut app);
    let before = app.world().get::<Transform>(body).unwrap().translation;
    let extra = app.world_mut().spawn(RapierConfiguration::new(1.0)).id();
    app.update();
    assert!(!app.world().resource::<PlayerSimulationReady>().0);
    assert!(
        app.world()
            .resource::<PlayerCellStreamingStatus>()
            .error
            .as_deref()
            .unwrap()
            .contains("exclusive default")
    );
    for config in app
        .world_mut()
        .query::<&RapierConfiguration>()
        .iter(app.world())
    {
        assert!(!config.physics_pipeline_active);
    }
    assert!(
        app.world()
            .get::<Transform>(body)
            .unwrap()
            .translation
            .abs_diff_eq(before, 0.0001)
    );
    app.world_mut().despawn(extra);
    ready(&mut app);
}

#[test]
fn a_foreign_body_context_link_is_rejected_before_backend_sync() {
    let (mut app, body) = player_app();
    ready(&mut app);
    let link = *app.world().get::<RapierContextEntityLink>(body).unwrap();
    app.world_mut()
        .entity_mut(body)
        .insert(RapierContextEntityLink(Entity::PLACEHOLDER));
    // A dangling ECS context reference is an invalid Rapier world, not a
    // supported backend configuration. Check admission before calling Rapier.
    guard(app.world_mut());
    enforce(app.world_mut());
    assert!(!app.world().resource::<PlayerSimulationReady>().0);
    assert!(
        app.world()
            .resource::<PlayerCellStreamingStatus>()
            .error
            .as_deref()
            .unwrap()
            .contains("different Rapier context")
    );
    app.world_mut().entity_mut(body).insert(link);
    ready(&mut app);
}

#[test]
fn failed_admission_keeps_physics_held_until_an_explicit_retry() {
    let (mut app, body) = player_app();
    ready(&mut app);
    let previous = app.world().resource::<State>().stream.resident().clone();
    app.world_mut()
        .get_mut::<Transform>(body)
        .unwrap()
        .translation = Vec3::new(96.0, 2.0, -96.0);
    guard(app.world_mut());
    app.update();
    assert!(app.world().resource::<State>().pending.is_some());
    let entity = app
        .world()
        .resource::<State>()
        .live
        .values()
        .find(|cell| cell.collider.is_none())
        .expect("an unprotected visual cell")
        .entity;
    let previous_transform = *app.world().get::<Transform>(entity).unwrap();
    app.world_mut()
        .get_mut::<Transform>(entity)
        .unwrap()
        .translation
        .x += 0.25;
    assert!(wait_for_admission_error(&mut app).contains("removed or replaced"));
    assert_eq!(app.world().resource::<State>().stream.resident(), &previous);
    *app.world_mut().get_mut::<Transform>(entity).unwrap() = previous_transform;
    // The failed job recorded its request. Without checking the admitted plan,
    // its remaining old and intact colliders alone falsely make refresh ready.
    let state = app.world().resource::<State>();
    let request = requested(app.world(), state).unwrap();
    assert_eq!(state.last_request.as_ref(), Some(&request));
    let plan = state
        .stream
        .prepare_with_protected_cells(
            app.world()
                .resource::<CellStreamingObserver>()
                .position_xz_m,
            &request.protected,
        )
        .unwrap();
    assert!(!plan.acquisitions().is_empty());
    // Even if another observer clears the diagnostic, the missing cells still
    // prevent readiness; the actual admission receipt is the authority.
    let error = app
        .world_mut()
        .resource_mut::<super::super::CellStreamingStats>()
        .last_error
        .take()
        .unwrap();
    assert!(!refresh(app.world_mut()).unwrap());
    app.world_mut()
        .resource_mut::<super::super::CellStreamingStats>()
        .last_error = Some(error);
    let before = app.world().get::<Transform>(body).unwrap().translation;
    for _ in 0..5 {
        app.update();
        assert!(!app.world().resource::<PlayerSimulationReady>().0);
        assert!(app.world().resource::<State>().pending.is_none());
        assert!(
            app.world()
                .resource::<PlayerCellStreamingStatus>()
                .error
                .is_some()
        );
        assert!(
            app.world()
                .get::<Transform>(body)
                .unwrap()
                .translation
                .abs_diff_eq(before, 0.0001)
        );
    }
    app.world_mut()
        .resource_mut::<CellStreamingObserver>()
        .retry_sequence += 1;
    wait_for_explicit_retry(&mut app);
    assert!(
        app.world()
            .resource::<super::super::CellStreamingStats>()
            .last_error
            .is_none()
    );
    assert!(app.world().resource::<State>().pending.is_none());
    assert!(app.world().resource::<PlayerSimulationReady>().0);
    let context = app
        .world_mut()
        .query_filtered::<&RapierConfiguration, With<DefaultRapierContext>>()
        .single(app.world())
        .unwrap();
    assert!(context.physics_pipeline_active);
    assert_body_moves_after_retry(&mut app, body);
}

fn assert_body_moves_after_retry(app: &mut App, body: Entity) {
    assert_eq!(
        app.world().get::<RigidBody>(body),
        Some(&RigidBody::Dynamic)
    );
    let start_x = app.world().get::<Transform>(body).unwrap().translation.x;
    app.world_mut().get_mut::<Velocity>(body).unwrap().linear = Vec3::X * 2.0;
    let deadline = Instant::now() + Duration::from_secs(10);
    loop {
        app.update();
        let status = app.world().resource::<PlayerCellStreamingStatus>();
        assert!(
            status.error.is_none(),
            "physics failed after retry: {status:?}"
        );
        if app.world().get::<Transform>(body).unwrap().translation.x > start_x + 0.05 {
            return;
        }
        assert!(
            Instant::now() < deadline,
            "dynamic body did not move after retry"
        );
        if !status.ready {
            std::thread::sleep(Duration::from_millis(1));
        }
    }
}

fn wait_for_admission_error(app: &mut App) -> String {
    let deadline = Instant::now() + Duration::from_secs(10);
    loop {
        app.update();
        if let Some(error) = &app
            .world()
            .resource::<super::super::CellStreamingStats>()
            .last_error
        {
            return error.clone();
        }
        assert!(
            Instant::now() < deadline,
            "damaged admission did not finish"
        );
        std::thread::sleep(Duration::from_millis(1));
    }
}

fn wait_for_explicit_retry(app: &mut App) {
    let deadline = Instant::now() + Duration::from_secs(10);
    loop {
        app.update();
        if app.world().resource::<PlayerCellStreamingStatus>().ready {
            return;
        }
        assert!(
            Instant::now() < deadline,
            "explicit retry did not restore the admitted terrain"
        );
        std::thread::sleep(Duration::from_millis(1));
    }
}

// These tests exercise source cancellation through the same playable sampled
// terrain host as the retry test above, including the real Rapier pipeline.
fn modified_sampled_source(
    original: &std::sync::Arc<forge_world::sampled_terrain::SampledTerrainPlan>,
) -> std::sync::Arc<forge_world::sampled_terrain::SampledTerrainPlan> {
    let mut document = original.document().clone();
    document.height_sampling.base_elevation_m += 3.0;
    let bytes: Vec<_> = original
        .raster()
        .heights()
        .iter()
        .flat_map(|height| height.to_le_bytes())
        .collect();
    std::sync::Arc::new(
        forge_world::sampled_terrain::SampledTerrainPlan::from_document(&document, &bytes).unwrap(),
    )
}

fn live_cell_identities(app: &App) -> Vec<(CellKey, Entity, Option<Entity>)> {
    app.world()
        .resource::<State>()
        .live
        .iter()
        .map(|(key, live)| (*key, live.entity, live.collider_entity))
        .collect()
}

fn wait_for_cancelled_sampled_source(
    app: &mut App,
    original: &std::sync::Arc<forge_world::sampled_terrain::SampledTerrainPlan>,
    resident: &std::collections::BTreeMap<CellKey, forge_world::cell_streaming::LoadedCell>,
    live: &[(CellKey, Entity, Option<Entity>)],
) {
    let deadline = Instant::now() + Duration::from_secs(10);
    loop {
        app.update();
        let state = app.world().resource::<State>();
        assert_eq!(state.source.document_hash(), original.document_hash());
        assert_eq!(state.stream.resident(), resident);
        assert_eq!(live_cell_identities(app).as_slice(), live);
        assert!(std::sync::Arc::ptr_eq(
            app.world()
                .resource::<super::super::ResidentSampledTerrain>()
                .plan(),
            original,
        ));
        if state.pending.is_some() {
            assert!(
                !app.world().resource::<PlayerSimulationReady>().0,
                "physics resumed before the obsolete source job was discarded"
            );
        }
        if app.world().resource::<PlayerCellStreamingStatus>().ready {
            assert!(state.pending.is_none());
            assert!(state.requested_source.is_none());
            assert!(
                app.world()
                    .resource::<CellStreamingStats>()
                    .last_error
                    .is_none()
            );
            assert!(app.world().resource::<PlayerSimulationReady>().0);
            let context = app
                .world_mut()
                .query_filtered::<&RapierConfiguration, With<DefaultRapierContext>>()
                .single(app.world())
                .unwrap();
            assert!(context.physics_pipeline_active);
            return;
        }
        assert!(
            Instant::now() < deadline,
            "cancelled source never resumed physics"
        );
        std::thread::sleep(Duration::from_millis(1));
    }
}

#[test]
fn cancelling_an_in_flight_raster_keeps_old_cells_and_resumes_player_physics() {
    let (mut app, body) = player_app();
    ready(&mut app);
    let original = app
        .world()
        .resource::<super::super::ResidentSampledTerrain>()
        .plan()
        .clone();
    let resident = app.world().resource::<State>().stream.resident().clone();
    let live = live_cell_identities(&app);
    let discarded = app.world().resource::<CellStreamingStats>().discarded_jobs;
    let changed = modified_sampled_source(&original);
    assert_ne!(changed.document_hash(), original.document_hash());
    super::super::request_source_update(app.world_mut(), original.document_hash(), changed)
        .unwrap();
    guard(app.world_mut());
    assert!(!app.world().resource::<PlayerSimulationReady>().0);
    app.update();
    assert!(app.world().resource::<State>().pending.is_some());
    super::super::request_source_update(
        app.world_mut(),
        original.document_hash(),
        original.clone(),
    )
    .unwrap();
    assert!(app.world().resource::<State>().pending.is_some());
    guard(app.world_mut());
    assert!(!app.world().resource::<PlayerSimulationReady>().0);
    wait_for_cancelled_sampled_source(&mut app, &original, &resident, &live);
    assert_eq!(
        app.world().resource::<CellStreamingStats>().discarded_jobs,
        discarded + 1
    );
    assert_body_moves_after_retry(&mut app, body);
}

#[test]
fn cancelling_failed_raster_after_repair_resumes_on_the_old_source() {
    let (mut app, body) = player_app();
    ready(&mut app);
    let original = app
        .world()
        .resource::<super::super::ResidentSampledTerrain>()
        .plan()
        .clone();
    let resident = app.world().resource::<State>().stream.resident().clone();
    let live = live_cell_identities(&app);
    let changed = modified_sampled_source(&original);
    super::super::request_source_update(app.world_mut(), original.document_hash(), changed)
        .unwrap();
    guard(app.world_mut());
    app.update();
    assert!(app.world().resource::<State>().pending.is_some());
    let entity = app
        .world()
        .resource::<State>()
        .live
        .values()
        .find(|cell| cell.collider.is_none())
        .expect("an unprotected visual cell")
        .entity;
    let previous_transform = *app.world().get::<Transform>(entity).unwrap();
    app.world_mut()
        .get_mut::<Transform>(entity)
        .unwrap()
        .translation
        .x += 0.25;
    assert!(wait_for_admission_error(&mut app).contains("removed or replaced"));
    assert_eq!(
        app.world().resource::<State>().source.document_hash(),
        original.document_hash()
    );
    assert_eq!(app.world().resource::<State>().stream.resident(), &resident);
    assert!(app.world().resource::<State>().requested_source.is_some());
    assert!(!app.world().resource::<PlayerSimulationReady>().0);
    *app.world_mut().get_mut::<Transform>(entity).unwrap() = previous_transform;
    super::super::request_source_update(
        app.world_mut(),
        original.document_hash(),
        original.clone(),
    )
    .unwrap();
    guard(app.world_mut());
    assert!(!app.world().resource::<PlayerSimulationReady>().0);
    assert!(
        app.world()
            .resource::<CellStreamingStats>()
            .last_error
            .is_some()
    );
    wait_for_cancelled_sampled_source(&mut app, &original, &resident, &live);
    assert_body_moves_after_retry(&mut app, body);
}

#[test]
fn empty_plan_keeps_failure_until_damaged_ground_is_repaired() {
    let (mut app, body) = player_app();
    ready(&mut app);
    let original = app
        .world()
        .resource::<super::super::ResidentSampledTerrain>()
        .plan()
        .clone();
    let resident = app.world().resource::<State>().stream.resident().clone();
    let live = live_cell_identities(&app);
    let changed = modified_sampled_source(&original);
    super::super::request_source_update(app.world_mut(), original.document_hash(), changed)
        .unwrap();
    guard(app.world_mut());
    app.update();
    assert!(app.world().resource::<State>().pending.is_some());
    let collider_entity = app
        .world()
        .resource::<State>()
        .live
        .values()
        .find_map(|cell| cell.collider_entity)
        .expect("a full ground cell");
    let collider = app
        .world()
        .get::<Collider>(collider_entity)
        .unwrap()
        .clone();
    app.world_mut()
        .entity_mut(collider_entity)
        .remove::<Collider>();
    assert!(wait_for_admission_error(&mut app).contains("removed or replaced"));
    assert!(app.world().resource::<State>().pending.is_none());
    assert!(!app.world().resource::<PlayerSimulationReady>().0);

    // Retrying the failed request while its original collider is still absent
    // must fail closed again and never resume the physics pipeline.
    assert!(super::super::retry_failed_terrain(app.world_mut()));
    assert!(!super::super::retry_failed_terrain(app.world_mut()));
    super::super::update(app.world_mut());
    assert!(app.world().resource::<State>().pending.is_some());
    let deadline = Instant::now() + Duration::from_secs(10);
    loop {
        app.update();
        let state = app.world().resource::<State>();
        let current = super::super::requested(app.world(), state).unwrap();
        if state.pending.is_none() && state.last_request.as_ref() == Some(&current) {
            break;
        }
        assert!(Instant::now() < deadline, "damaged retry did not finish");
        std::thread::sleep(Duration::from_millis(1));
    }
    let failure = app
        .world()
        .resource::<CellStreamingStats>()
        .last_error
        .as_deref()
        .expect("the damaged retry must remain diagnosed");
    assert!(failure.contains("removed or replaced"), "{failure}");
    assert!(!app.world().resource::<PlayerSimulationReady>().0);

    // Cancel B back to admitted A while the collider is still absent. An empty
    // cell plan is not proof that the old projection is intact.
    super::super::request_source_update(
        app.world_mut(),
        original.document_hash(),
        original.clone(),
    )
    .unwrap();
    guard(app.world_mut());
    super::super::update(app.world_mut());
    let failure = app
        .world()
        .resource::<CellStreamingStats>()
        .last_error
        .as_deref()
        .expect("empty plan must not erase the damaged projection diagnostic");
    assert!(failure.contains("removed or replaced"), "{failure}");
    assert!(app.world().resource::<State>().last_request.is_none());
    assert!(app.world().resource::<State>().pending.is_none());
    guard(app.world_mut());
    assert!(!app.world().resource::<PlayerSimulationReady>().0);
    assert_eq!(app.world().resource::<State>().stream.resident(), &resident);

    app.world_mut().entity_mut(collider_entity).insert(collider);
    assert!(!app.world().resource::<PlayerSimulationReady>().0);
    super::super::update(app.world_mut());
    wait_for_cancelled_sampled_source(&mut app, &original, &resident, &live);
    assert_body_moves_after_retry(&mut app, body);
}

#[test]
fn returning_to_admitted_cells_after_a_failed_plan_requires_one_verified_retry() {
    // Reuse the real sampled host but make a distant dynamic body exceed the
    // residency budget before any replacement job is submitted.
    let mut app = super::super::tests::app_with_cell_budget(25);
    app.add_plugins(TransformPlugin)
        .insert_resource(TimeUpdateStrategy::ManualDuration(Duration::from_secs_f64(
            1.0 / 60.0,
        )));
    crate::sampled_terrain_physics::register(&mut app);
    let spawn = Vec3::new(-64.0, 2.0, -64.0);
    app.add_plugins(
        PlayerControllerPlugin::new(
            forge_world::embedded_visual_valley_player_controller_profile().unwrap(),
            spawn,
        )
        .with_interactive_input(false)
        .with_initial_camera_target(spawn + Vec3::NEG_Z),
    )
    .add_plugins(PlayerCellStreamingPlugin);
    let body = app
        .world_mut()
        .spawn((
            RigidBody::Dynamic,
            Collider::cuboid(0.5, 0.5, 0.5),
            Velocity::default(),
            Transform::from_xyz(-56.0, 2.0, -56.0),
        ))
        .id();
    ready(&mut app);
    let deadline = Instant::now() + Duration::from_secs(10);
    loop {
        let state = app.world().resource::<State>();
        let request = super::super::requested(app.world(), state).unwrap();
        if state.pending.is_none() && state.last_request.as_ref() == Some(&request) {
            break;
        }
        assert!(Instant::now() < deadline, "initial request never settled");
        app.update();
        std::thread::sleep(Duration::from_millis(1));
    }
    let admitted = app
        .world()
        .resource::<State>()
        .last_request
        .clone()
        .unwrap();
    let resident = app.world().resource::<State>().stream.resident().clone();
    let live = live_cell_identities(&app);
    let original = *app.world().get::<Transform>(body).unwrap();
    app.world_mut()
        .get_mut::<Transform>(body)
        .unwrap()
        .translation = Vec3::new(96.0, 2.0, 96.0);
    guard(app.world_mut());
    assert!(!app.world().resource::<PlayerSimulationReady>().0);
    // Run the actual exclusive Update owner once; it records the planning error.
    super::super::update(app.world_mut());
    let failure = app
        .world()
        .resource::<CellStreamingStats>()
        .last_error
        .clone()
        .expect("distant body must exceed the configured count");
    assert!(failure.contains("configured cell count"), "{failure}");
    assert_eq!(
        app.world().resource::<State>().last_request.as_ref(),
        Some(&admitted)
    );
    assert!(app.world().resource::<State>().pending.is_none());
    assert_eq!(app.world().resource::<State>().stream.resident(), &resident);
    // Restore the body to A. An equal last_request alone must not silently
    // erase the error or enable physics; the user-visible retry is explicit.
    *app.world_mut().get_mut::<Transform>(body).unwrap() = original;
    guard(app.world_mut());
    let current = super::super::requested(app.world(), app.world().resource::<State>()).unwrap();
    assert_eq!(current, admitted);
    super::super::update(app.world_mut());
    assert_eq!(
        app.world()
            .resource::<CellStreamingStats>()
            .last_error
            .as_deref(),
        Some(failure.as_str())
    );
    assert!(!app.world().resource::<PlayerSimulationReady>().0);
    assert!(super::super::retry_failed_terrain(app.world_mut()));
    assert!(!super::super::retry_failed_terrain(app.world_mut()));
    assert_eq!(
        app.world()
            .resource::<CellStreamingStats>()
            .last_error
            .as_deref(),
        Some(failure.as_str())
    );
    wait_for_explicit_retry(&mut app);
    assert!(
        app.world()
            .resource::<CellStreamingStats>()
            .last_error
            .is_none()
    );
    assert_eq!(app.world().resource::<State>().stream.resident(), &resident);
    assert_eq!(live_cell_identities(&app), live);
    assert_body_moves_after_retry(&mut app, body);
}
