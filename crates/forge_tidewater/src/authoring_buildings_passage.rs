//! CPU integration: production room projection and production capsule controller.
use super::{BuildingProjection, tests::head};
use bevy::{prelude::*, time::TimeUpdateStrategy};
use bevy_rapier3d::prelude::{Collider, NoUserData, RapierPhysicsPlugin, TimestepMode};
use forge_engine::{
    PendingPlayerControlCommand, PlayerControlCommand, PlayerControllerPlugin,
    PlayerRuntimeSnapshot,
};
use std::time::Duration;

#[test]
fn capsule_enters_and_leaves_rotated_door_but_cannot_cross_adjacent_wall() {
    for yaw in [0.0_f32, 90.0, 180.0, -90.0] {
        for through_door in [true, false] {
            let mut source = head();
            source.corrections.buildings.as_mut().unwrap()[0].yaw_degrees = yaw;
            let original_hash = source.content_hash().unwrap();
            let profile = forge_world::VisualValleyPlayerControllerRecipe
                .build()
                .unwrap();
            let half_height =
                profile.controller.capsule_radius_m + profile.controller.capsule_half_segment_m;
            let rotation = Quat::from_rotation_y(yaw.to_radians());
            let origin = Vec3::new(10.0, 2.0, 20.0);
            let lane = if through_door { 1.5 } else { -1.5 };
            let spawn = origin + rotation * Vec3::new(lane, half_height + 0.02, -5.0);
            let mut app = App::new();
            app.add_plugins((MinimalPlugins, TransformPlugin))
                .insert_resource(Time::<Fixed>::from_hz(60.0))
                .insert_resource(TimeUpdateStrategy::ManualDuration(Duration::from_secs_f64(
                    1.0 / 60.0,
                )))
                .insert_resource(TimestepMode::Fixed {
                    dt: 1.0 / 60.0,
                    substeps: 1,
                })
                .add_plugins(RapierPhysicsPlugin::<NoUserData>::default().in_fixed_schedule())
                .add_plugins(
                    PlayerControllerPlugin::new(profile, spawn)
                        .with_initial_camera_target(spawn + rotation * Vec3::Z)
                        .with_interactive_input(false),
                );
            app.init_resource::<Assets<Mesh>>();
            app.init_resource::<Assets<StandardMaterial>>();
            // Synthetic flat ground at the authored finished-floor level.
            app.world_mut().spawn((
                Collider::cuboid(30.0, 0.1, 30.0),
                Transform::from_translation(origin - Vec3::Y * 0.1),
            ));
            let mut projection = BuildingProjection::default();
            projection.replace(app.world_mut(), &source, false).unwrap();
            for _ in 0..6 {
                app.update();
            }
            assert!(projection.ready(app.world(), original_hash, 1));
            assert!(app.world().resource::<PlayerRuntimeSnapshot>().grounded);
            drive(&mut app, Vec2::Y, 60);
            let state = app.world().resource::<PlayerRuntimeSnapshot>();
            let local = rotation.inverse() * (state.player_position - origin);
            assert!((local.x - lane).abs() < 0.15, "lateral drift: {local:?}");
            assert!(state.grounded, "lost floor at {yaw}: {local:?}");
            if through_door {
                assert!(
                    local.z > -2.0 && local.z < 2.5,
                    "entry failed at {yaw}: {local:?}"
                );
                // Reversing must first brake the forward velocity. Give the
                // return leg 1.5 s; retain the clearance assertion unchanged.
                drive(&mut app, Vec2::NEG_Y, 90);
                let local = rotation.inverse()
                    * (app
                        .world()
                        .resource::<PlayerRuntimeSnapshot>()
                        .player_position
                        - origin);
                assert!(local.z < -4.0, "exit failed at {yaw}: {local:?}");
            } else {
                assert!(
                    local.z < -3.0 && local.z > -4.0,
                    "wall did not block at {yaw}: {local:?}"
                );
            }
            assert_eq!(source.content_hash().unwrap(), original_hash);
            projection.clear(app.world_mut());
        }
    }
}

fn drive(app: &mut App, move_axis: Vec2, ticks: usize) {
    app.world_mut()
        .resource_mut::<PendingPlayerControlCommand>()
        .submit(PlayerControlCommand {
            move_axis,
            ..default()
        })
        .unwrap();
    for _ in 0..ticks {
        let before = app.world().resource::<PlayerRuntimeSnapshot>().fixed_tick;
        app.update();
        assert_eq!(
            app.world().resource::<PlayerRuntimeSnapshot>().fixed_tick,
            before + 1
        );
    }
}
