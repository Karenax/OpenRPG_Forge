//! ECS/Rapier coverage for opt-in Tidewater player locomotion.
use super::super::{
    ForgePlayerCharacter, PendingPlayerControlCommand, PlayerControlCommand,
    PlayerControllerPlugin, PlayerRuntimeBindings, PlayerRuntimeSnapshot, PlayerSimulationReady,
};
use super::*;
use bevy::{ecs::system::SystemState, time::TimeUpdateStrategy, transform::TransformPlugin};
use bevy_rapier3d::{
    control::{KinematicCharacterController, KinematicCharacterControllerOutput},
    prelude::{
        Collider, DefaultRapierContext, GravityScale, LockedAxes, NoUserData, RapierColliderHandle,
        RapierContextColliders, RapierPhysicsPlugin, RigidBody, TimestepMode,
    },
};
use forge_core::bytes_sha256;
use forge_project::{
    HeightSamplingAlgorithm, TERRAIN_DOCUMENT_SCHEMA_VERSION_V4, TerrainExtent,
    authoring::new_terrain,
    sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
};
use forge_world::{VisualValleyPlayerControllerRecipe, sampled_terrain::SampledTerrainPlan};
use std::{sync::Arc, time::Duration};

#[test]
fn adjacent_heightfield_tiles_preserve_raw_kcc_walkable_slope_crossing() {
    let (mut app, _) = prepared_app(0.0);
    let floors: Vec<Entity> = {
        let world = app.world_mut();
        world
            .query_filtered::<Entity, (With<Collider>, Without<ForgePlayerCharacter>)>()
            .iter(world)
            .collect()
    };
    for floor in floors {
        app.world_mut().despawn(floor);
    }
    let slope = -35.0_f32.to_radians().tan();
    for center in [-1.0, 1.0] {
        let heights: Vec<f32> = (0_u16..3)
            .flat_map(|column| std::iter::repeat_n((center - 1.0 + f32::from(column)) * slope, 3))
            .collect();
        app.world_mut().spawn((
            Collider::heightfield(heights, 3, 3, Vec3::new(2.0, 1.0, 4.0)),
            Transform::from_xyz(center, 0.0, 0.0),
        ));
    }
    for _ in 0..4 {
        app.update();
    }
    let entity = bindings(&app).player_entity;
    let mut context_state = SystemState::<ReadRapierContext>::new(app.world_mut());
    let rapier = context_state.get(app.world()).expect("context access");
    let context = rapier.single().expect("context");
    let handle = context.colliders.entity2collider()[&entity];
    let collider = &context.colliders.colliders[handle];
    let raw = bevy_rapier3d::rapier::control::KinematicCharacterController {
        offset: CharacterLength::Absolute(0.01),
        slide: true,
        autostep: Some(CharacterAutostep {
            max_height: CharacterLength::Absolute(0.4),
            min_width: CharacterLength::Absolute(0.2),
            include_dynamic_bodies: false,
        }),
        max_slope_climb_angle: 45.0_f32.to_radians(),
        min_slope_slide_angle: 50.0_f32.to_radians(),
        snap_to_ground: Some(CharacterLength::Absolute(0.3)),
        ..default()
    };
    let clearance = RADIUS * (35.0_f32.to_radians().cos().recip() - 1.0);
    let mut pose = bevy_rapier3d::rapier::math::Pose::from_translation(Vec3::new(
        0.8,
        0.8 * slope + WALK_HEIGHT * 0.5 + clearance + 0.012,
        0.0,
    ));
    let mut last_contacts = Vec::new();
    for _ in 0..60 {
        last_contacts.clear();
        let movement = context.with_query_pipeline(
            QueryFilter::default().exclude_collider(entity),
            |pipeline| {
                raw.move_shape(
                    1.0 / 60.0,
                    &pipeline.query_pipeline,
                    collider.shape(),
                    &pose,
                    Vec3::new(-0.03, -9.81 / 3600.0, 0.0),
                    |hit| {
                        last_contacts.push((
                            context.colliders.collider_entity(hit.handle),
                            hit.hit.normal1,
                        ));
                    },
                )
            },
        );
        pose.translation += movement.translation;
    }
    eprintln!(
        "adjacent 35-degree heightfields final={:?}, contacts={last_contacts:?}",
        pose.translation
    );
    assert!(
        pose.translation.x < -RADIUS,
        "The capsule must fully clear the shared seam on a walkable slope"
    );
}

#[test]
fn real_tidewater_seam_wall_guard_preserves_forward_demand() {
    let (mut app, _) = prepared_app(0.0);
    let floors: Vec<Entity> = {
        let world = app.world_mut();
        world
            .query_filtered::<Entity, (With<Collider>, Without<ForgePlayerCharacter>)>()
            .iter(world)
            .collect()
    };
    for floor in floors {
        app.world_mut().despawn(floor);
    }
    let tiles: [(&[u8], f32); 2] = [
        (
            include_bytes!("../tests/fixtures/tidewater-seam/tile-z-384.f32"),
            -320.0,
        ),
        (
            include_bytes!("../tests/fixtures/tidewater-seam/tile-z-256.f32"),
            -192.0,
        ),
    ];
    for (bytes, center_z) in tiles {
        let heights: Vec<f32> = bytes
            .as_chunks::<4>()
            .0
            .iter()
            .map(|chunk| f32::from_le_bytes(*chunk))
            .collect();
        assert_eq!(heights.len(), 129 * 129);
        app.world_mut().spawn((
            Collider::heightfield(heights, 129, 129, Vec3::new(128.0, 1.0, 128.0)),
            Transform::from_xyz(64.0, 0.0, center_z),
        ));
    }
    for _ in 0..4 {
        app.update();
    }
    let entity = bindings(&app).player_entity;
    let anchor: [f32; 3] = serde_json::from_str(include_str!(
        "../tests/fixtures/tidewater-seam/start-anchor.json"
    ))
    .expect("captured public anchor");
    app.world_mut()
        .get_mut::<Transform>(entity)
        .expect("player transform")
        .translation = Vec3::from_array(anchor) + Vec3::Y * (WALK_HEIGHT * 0.5);
    for _ in 0..4 {
        app.update();
    }
    // Exercise candidate revalidation separately from history collection. Both
    // tile identifiers represent possible earlier edge contacts, not proof of
    // the unobserved public wall-candidate list.
    let mut candidates: Vec<Entity> = {
        let world = app.world_mut();
        world
            .query_filtered::<Entity, (With<Collider>, Without<ForgePlayerCharacter>)>()
            .iter(world)
            .collect()
    };
    let controller = app
        .world()
        .get::<KinematicCharacterController>(entity)
        .expect("player KCC")
        .clone();
    let mut context_state = SystemState::<ReadRapierContext>::new(app.world_mut());
    let rapier = context_state.get(app.world()).expect("context access");
    let context = rapier.single().expect("context");
    let desired = Vec3::new(0.0, -9.81 / 3600.0, -0.05);
    let constrained = constrain_wall_drive(&context, entity, &controller, desired, &mut candidates);
    eprintln!(
        "real Tidewater guard desired={desired:?}, constrained={constrained:?}, retained={candidates:?}"
    );
    assert!(
        constrained.z < -0.01,
        "Fresh wall-candidate revalidation must preserve demand across this walkable real seam"
    );
}

#[test]
fn real_tidewater_tiles_preserve_raw_kcc_crossing_at_forest_stop() {
    let (mut app, _) = prepared_app(0.0);
    let floors: Vec<Entity> = {
        let world = app.world_mut();
        world
            .query_filtered::<Entity, (With<Collider>, Without<ForgePlayerCharacter>)>()
            .iter(world)
            .collect()
    };
    for floor in floors {
        app.world_mut().despawn(floor);
    }
    let tiles: [(&[u8], f32); 2] = [
        (
            include_bytes!("../tests/fixtures/tidewater-seam/tile-z-384.f32"),
            -320.0,
        ),
        (
            include_bytes!("../tests/fixtures/tidewater-seam/tile-z-256.f32"),
            -192.0,
        ),
    ];
    for (bytes, center_z) in tiles {
        let heights: Vec<f32> = bytes
            .as_chunks::<4>()
            .0
            .iter()
            .map(|chunk| f32::from_le_bytes(*chunk))
            .collect();
        assert_eq!(heights.len(), 129 * 129);
        app.world_mut().spawn((
            Collider::heightfield(heights, 129, 129, Vec3::new(128.0, 1.0, 128.0)),
            Transform::from_xyz(64.0, 0.0, center_z),
        ));
    }
    for _ in 0..4 {
        app.update();
    }
    let entity = bindings(&app).player_entity;
    let mut context_state = SystemState::<ReadRapierContext>::new(app.world_mut());
    let rapier = context_state.get(app.world()).expect("context access");
    let context = rapier.single().expect("context");
    let handle = context.colliders.entity2collider()[&entity];
    let collider = &context.colliders.colliders[handle];
    let raw = bevy_rapier3d::rapier::control::KinematicCharacterController {
        offset: CharacterLength::Absolute(0.01),
        slide: true,
        autostep: Some(CharacterAutostep {
            max_height: CharacterLength::Absolute(0.4),
            min_width: CharacterLength::Absolute(0.2),
            include_dynamic_bodies: false,
        }),
        max_slope_climb_angle: 45.0_f32.to_radians(),
        min_slope_slide_angle: 50.0_f32.to_radians(),
        snap_to_ground: Some(CharacterLength::Absolute(0.3)),
        ..default()
    };
    let anchor: [f32; 3] = serde_json::from_str(include_str!(
        "../tests/fixtures/tidewater-seam/start-anchor.json"
    ))
    .expect("captured public anchor");
    let mut pose = bevy_rapier3d::rapier::math::Pose::from_translation(
        Vec3::from_array(anchor) + Vec3::Y * (WALK_HEIGHT * 0.5),
    );
    let mut last_contacts = Vec::new();
    for _ in 0..60 {
        last_contacts.clear();
        let movement = context.with_query_pipeline(
            QueryFilter::default().exclude_collider(entity),
            |pipeline| {
                raw.move_shape(
                    1.0 / 60.0,
                    &pipeline.query_pipeline,
                    collider.shape(),
                    &pose,
                    Vec3::new(0.0, -9.81 / 3600.0, -0.05),
                    |hit| {
                        last_contacts.push((
                            context.colliders.collider_entity(hit.handle),
                            hit.hit.normal1,
                        ));
                    },
                )
            },
        );
        pose.translation += movement.translation;
    }
    eprintln!(
        "real Tidewater forest seam start={anchor:?}, final={:?}, contacts={last_contacts:?}",
        pose.translation
    );
    assert!(
        pose.translation.z < -257.0,
        "The raw KCC must cross the real Tidewater seam from the captured forest stop"
    );
}

#[test]
fn elevated_room_floor_preserves_raw_kcc_forward_translation() {
    let (mut app, _) = prepared_app(0.0);
    let floor = app
        .world_mut()
        .spawn((
            Collider::cuboid(0.5, 0.5, 0.5),
            Transform::from_xyz(46.0, 6.549_617_3, -145.0).with_scale(Vec3::new(8.0, 0.2, 6.0)),
        ))
        .id();
    for _ in 0..4 {
        app.update();
    }
    let entity = bindings(&app).player_entity;
    let mut context_state = SystemState::<ReadRapierContext>::new(app.world_mut());
    let rapier = context_state.get(app.world()).expect("context access");
    let context = rapier.single().expect("context");
    let handle = context.colliders.entity2collider()[&entity];
    let collider = &context.colliders.colliders[handle];
    let raw = bevy_rapier3d::rapier::control::KinematicCharacterController {
        offset: CharacterLength::Absolute(0.01),
        slide: true,
        autostep: None,
        snap_to_ground: None,
        ..default()
    };
    let desired = Vec3::new(0.0, -0.002_725_124_4, -0.050_003_05);
    let mut failures = Vec::new();
    for x in [45.954_227, 46.0] {
        for z in [-146.72815, -145.0] {
            for clearance in [0.0, 0.0001, 0.005, 0.01, 0.02] {
                let pose = bevy_rapier3d::rapier::math::Pose::from_translation(Vec3::new(
                    x,
                    6.649_617 + WALK_HEIGHT * 0.5 + clearance,
                    z,
                ));
                let mut contacts = Vec::new();
                let movement = context.with_query_pipeline(
                    QueryFilter::default().exclude_collider(entity),
                    |pipeline| {
                        raw.move_shape(
                            1.0 / 60.0,
                            &pipeline.query_pipeline,
                            collider.shape(),
                            &pose,
                            desired,
                            |hit| {
                                contacts.push((
                                    context.colliders.collider_entity(hit.handle),
                                    hit.hit.normal1,
                                ));
                            },
                        )
                    },
                );
                if movement.translation.z >= -0.049 {
                    failures.push((x, z, clearance, movement.translation, contacts));
                }
            }
        }
    }
    eprintln!("elevated scaled floor={floor:?}, 20 actual sweeps, failures={failures:?}");
    assert!(
        failures.is_empty(),
        "A horizontal floor must retain forward movement after its gravity contact"
    );
}

#[test]
#[allow(
    clippy::float_cmp,
    reason = "Ground contact explicitly resets vertical velocity to zero; retain the exact stop assertion."
)]
fn walkable_slope_contact_stops_gravity_without_lowering_the_capsule_into_the_floor() {
    for degrees in [40.0_f32, -40.0] {
        let (mut app, _) = prepared_app_on_slope(-10.0, [], degrees.to_radians());
        start(&mut app);
        settle(&mut app, 90);

        let output = player_controller_output(&mut app);
        let anchor = motion(&app).position;
        let ground = app.world().resource::<TidewaterPlayerState>().ground_height;
        eprintln!(
            "slope KCC grounded={}, source grounded={}, Y velocity={}, clearance={}",
            output.grounded,
            motion(&app).grounded,
            motion(&app).velocity.y,
            anchor.y - ground,
        );
        assert!(
            output.grounded,
            "the real KCC must confirm walkable contact"
        );
        assert!(
            anchor.y - ground > 0.06,
            "capsule slope clearance must exceed the old foot tolerance"
        );
        assert!(
            snapshot(&app).grounded,
            "walkable contact must survive source adaptation"
        );
        assert_eq!(
            motion(&app).velocity.y,
            0.0,
            "gravity must not accumulate in sustained contact"
        );
        let before = anchor;
        settle(&mut app, 90);
        assert!(
            motion(&app).position.distance(before) < 0.002,
            "an idle grounded capsule must remain stationary"
        );
        assert_eq!(motion(&app).velocity.y, 0.0);
    }
}

#[test]
fn positive_walkable_slope_brakes_after_meaningful_motion() {
    assert_moving_player_brakes_and_stays_on_walkable_slope(40.0);
}

#[test]
fn negative_walkable_slope_brakes_after_meaningful_motion() {
    assert_moving_player_brakes_and_stays_on_walkable_slope(-40.0);
}

#[test]
fn blocked_command_on_35_degree_walkable_slope_keeps_confirmed_support() {
    assert_blocked_command_keeps_walkable_support(35.0);
}

#[test]
fn blocked_command_on_40_degree_walkable_slope_keeps_confirmed_support() {
    assert_blocked_command_keeps_walkable_support(40.0);
}

#[test]
fn blocked_command_on_oblique_corner_keeps_consecutive_slope_poses_stationary() {
    let (mut app, _) = prepared_app_on_slope(-10.0, [], -40.0_f32.to_radians());
    // Both tall walls exclude autostep. Their inward XZ normals are +X and
    // (-X + Z)/sqrt(2); the entering command must physically reach both faces.
    // The oblique face is z = x - 0.7485, leaving the spawn outside both walls.
    let wall = app
        .world_mut()
        .spawn((
            Collider::cuboid(0.1, 2.0, 2.0),
            Transform::from_xyz(-0.8, 1.0, -0.7),
        ))
        .id();
    let oblique = app
        .world_mut()
        .spawn((
            Collider::cuboid(2.0, 2.0, 0.1),
            Transform::from_xyz(-0.629_289_3, 1.0, -1.519_210_7)
                .with_rotation(Quat::from_rotation_y(-std::f32::consts::FRAC_PI_4)),
        ))
        .id();
    start(&mut app);
    settle(&mut app, 90);
    submit(
        &mut app,
        PlayerControlCommand {
            move_axis: Vec2::new(-0.5, 1.0),
            ..default()
        },
    );
    let mut encountered = [false; 2];
    for _ in 0..240 {
        settle(&mut app, 1);
        let output = player_controller_output(&mut app);
        for (index, target) in [wall, oblique].into_iter().enumerate() {
            encountered[index] |= output.collisions.iter().any(|hit| hit.entity == target);
        }
    }
    let before = motion(&app).position;
    let mut low = before;
    let mut high = before;
    let mut supported = true;
    let mut max_vertical_velocity = 0.0_f32;
    for tick in 0..120 {
        settle(&mut app, 1);
        let position = motion(&app).position;
        let output = player_controller_output(&mut app);
        low = low.min(position);
        high = high.max(position);
        supported &= output.grounded && motion(&app).grounded;
        max_vertical_velocity = max_vertical_velocity.max(motion(&app).velocity.y.abs());
        if tick >= 108 {
            let support = support_ray_observation(&mut app, position);
            eprintln!(
                "oblique corner tick={tick}: position={position:?}, source_grounded={}, vy={}, desired={:?}, effective={:?}, kcc_grounded={}, contacts={:?}, support={support:?}",
                motion(&app).grounded,
                motion(&app).velocity.y,
                snapshot(&app).desired_translation,
                output.effective_translation,
                output.grounded,
                output.collisions,
            );
        }
    }
    eprintln!(
        "oblique corner actual faces={encountered:?}, first={before:?}, extent={:?}, supported={supported}, max_vy={max_vertical_velocity}",
        high - low,
    );
    assert!(
        encountered.into_iter().all(|hit| hit),
        "the real capsule must encounter both oblique-corner colliders"
    );
    assert!(
        (high - low).length() < 0.002,
        "all 120 consecutive held-command poses must remain within the unchanged 2 mm bound"
    );
    assert!(
        supported,
        "the actual walkable floor must support every final-window pose"
    );
    assert!(
        max_vertical_velocity <= 1.0e-6,
        "supported gravity must not accumulate in the physical corner"
    );
}

#[test]
fn support_ray_rejects_distant_floors_and_unwalkable_normals() {
    let slide_angle = 50.0_f32.to_radians();
    assert!(
        !nearby_walkable_support(Vec3::Y, Vec3::ZERO, Vec3::Y, slide_angle),
        "grounded contact elsewhere cannot turn a floor one metre below feet into support"
    );
    let steep_normal = Vec3::new(-60.0_f32.to_radians().sin(), 0.5, 0.0);
    assert!(
        !nearby_walkable_support(Vec3::Y * RADIUS, Vec3::ZERO, steep_normal, slide_angle),
        "a close plane still must respect the profile slide angle"
    );
}

fn assert_blocked_command_keeps_walkable_support(degrees: f32) {
    let (mut app, wall) = prepared_blocked_slope(degrees);
    start(&mut app);
    settle(&mut app, 90);
    submit(
        &mut app,
        PlayerControlCommand {
            move_axis: Vec2::X,
            ..default()
        },
    );
    let mut encountered_slide = false;
    let mut encountered_wall = false;
    for _ in 0..120 {
        settle(&mut app, 1);
        let output = player_controller_output(&mut app);
        encountered_slide |= output.grounded && output.is_sliding_down_slope;
        encountered_wall |= output.collisions.iter().any(|hit| hit.entity == wall);
    }
    let blocked_position = motion(&app).position;
    let blocked_velocity = motion(&app).velocity;
    let mut window_min = blocked_position;
    let mut window_max = blocked_position;
    for tick in 0..120 {
        settle(&mut app, 1);
        let position = motion(&app).position;
        window_min = window_min.min(position);
        window_max = window_max.max(position);
        if tick >= 108 {
            let output = player_controller_output(&mut app);
            let ray = support_ray_observation(&mut app, position);
            eprintln!(
                "blocked window {degrees} degrees tick={tick}: position={position:?}, source_grounded={}, vy={}, ground={}, desired={:?}, effective={:?}, kcc_grounded={}, kcc_slide={}, contacts={:?}, support_ray={ray:?}",
                motion(&app).grounded,
                motion(&app).velocity.y,
                app.world().resource::<TidewaterPlayerState>().ground_height,
                snapshot(&app).desired_translation,
                output.effective_translation,
                output.grounded,
                output.is_sliding_down_slope,
                output.collisions,
            );
        }
    }
    eprintln!(
        "blocked window bounds {degrees} degrees: min={window_min:?}, max={window_max:?}, extent={:?}",
        window_max - window_min,
    );

    let output = player_controller_output(&mut app);
    let stopped = motion(&app).clone();
    let ground = app.world().resource::<TidewaterPlayerState>().ground_height;
    eprintln!(
        "blocked slope {degrees} degrees: first_position={blocked_position:?}, first_velocity={blocked_velocity:?}, final_position={:?}, final_velocity={:?}, source_grounded={}, kcc_grounded={}, kcc_slide={}, contacts={}, effective_translation={:?}, clearance={}",
        stopped.position,
        stopped.velocity,
        stopped.grounded,
        output.grounded,
        output.is_sliding_down_slope,
        output.collisions.len(),
        output.effective_translation,
        stopped.position.y - ground,
    );
    assert!(output.grounded, "Rapier must confirm real slope support");
    assert!(
        encountered_slide,
        "the real approach must exercise the overloaded Rapier slide flag"
    );
    assert!(
        encountered_wall,
        "the held command must encounter the actual physical blocker"
    );
    assert!(
        (window_max - window_min).length() < 0.002,
        "every consecutive blocked pose must remain inside the stationary window"
    );
    assert!(
        stopped.position.distance(blocked_position) < 0.002,
        "the blocked capsule must remain physically stationary"
    );
    assert!(
        stopped.position.y - ground > 0.06,
        "the capsule must retain real slope clearance above point feet"
    );
    assert!(
        stopped.grounded,
        "confirmed walkable support must survive an intentional blocked command"
    );
    assert!(
        stopped.velocity.y.abs() <= 1.0e-6,
        "gravity must not accumulate while the real capsule is supported"
    );
}

fn prepared_blocked_slope(degrees: f32) -> (App, Entity) {
    let (mut app, _) = prepared_app_on_slope(-10.0, [], degrees.to_radians());
    // A vertical obstacle stops uphill walking before the capsule leaves the
    // sampled slope. Its height excludes autostep as a way around the blockage.
    let wall = app
        .world_mut()
        .spawn((
            Collider::cuboid(0.1, 2.0, 2.0),
            Transform::from_xyz(0.8, 1.0, 0.0),
        ))
        .id();
    (app, wall)
}

fn press_into_wall(app: &mut App) {
    start(app);
    settle(app, 90);
    submit(
        app,
        PlayerControlCommand {
            move_axis: Vec2::X,
            ..default()
        },
    );
    settle(app, 120);
}

#[test]
fn blocked_wall_keeps_tangent_motion_without_vertical_jitter() {
    let (mut app, _) = prepared_blocked_slope(40.0);
    press_into_wall(&mut app);
    let before = motion(&app).position;
    submit(
        &mut app,
        PlayerControlCommand {
            move_axis: Vec2::ONE,
            ..default()
        },
    );
    let mut low = before;
    let mut high = before;
    for _ in 0..24 {
        settle(&mut app, 1);
        low = low.min(motion(&app).position);
        high = high.max(motion(&app).position);
    }
    let after = motion(&app).position;
    eprintln!(
        "wall tangent before={before:?}, after={after:?}, extent={:?}",
        high - low
    );
    assert!(
        after.z < before.z - 0.3,
        "the wall tangent must remain movable"
    );
    assert!(
        high.x - low.x < 0.002,
        "the blocked component must stay stopped"
    );
    assert!(
        high.y - low.y < 0.002,
        "tangent movement cannot induce a wall bounce"
    );
    assert!(motion(&app).grounded);
}

#[test]
fn blocked_wall_witness_releases_when_collider_is_removed() {
    let (mut app, wall) = prepared_blocked_slope(40.0);
    press_into_wall(&mut app);
    let before = motion(&app).position;
    // Door opening removes its collider while retaining the authored entity.
    app.world_mut().entity_mut(wall).remove::<Collider>();
    settle(&mut app, 24);
    assert!(
        motion(&app).position.x > before.x + 0.6,
        "an opened door must release current casts immediately"
    );
}

#[test]
fn blocked_wall_witness_releases_when_obstacle_moves_away() {
    let (mut app, wall) = prepared_blocked_slope(40.0);
    press_into_wall(&mut app);
    let before = motion(&app).position;
    app.world_mut()
        .entity_mut(wall)
        .get_mut::<Transform>()
        .expect("wall transform")
        .translation
        .x = 1.5;
    settle(&mut app, 16);
    assert!(
        motion(&app).position.x > before.x + 0.3,
        "a moved obstacle cannot leave a stale clamp"
    );
}

#[test]
fn blocked_wall_does_not_capture_a_grounded_jump() {
    let (mut app, _) = prepared_blocked_slope(40.0);
    press_into_wall(&mut app);
    let before = motion(&app).position;
    submit(
        &mut app,
        PlayerControlCommand {
            move_axis: Vec2::X,
            jump: true,
            ..default()
        },
    );
    settle(&mut app, 6);
    assert!(motion(&app).position.y > before.y + 0.2);
    assert!(motion(&app).velocity.y > 0.0);
    assert!(!motion(&app).grounded);
}

#[test]
fn dynamic_wall_retains_the_real_kcc_drive_and_impulse_path() {
    let (mut app, wall) = prepared_blocked_slope(40.0);
    app.world_mut().entity_mut(wall).insert((
        RigidBody::Dynamic,
        GravityScale(0.0),
        LockedAxes::all(),
    ));
    press_into_wall(&mut app);
    assert!(
        snapshot(&app).desired_translation.x > 0.04,
        "dynamic bodies must keep the ordinary KCC drive"
    );
    assert!(
        player_controller_output(&mut app)
            .collisions
            .iter()
            .any(|hit| hit.entity == wall)
    );
}

#[test]
fn real_autostep_keeps_forward_progress_through_side_contact() {
    let (mut app, _) = prepared_app_on_slope(-10.0, [], 0.0);
    app.world_mut().spawn((
        Collider::cuboid(0.4, 0.1, 1.0),
        Transform::from_xyz(0.85, 0.1, 0.0),
    ));
    start(&mut app);
    settle(&mut app, 90);
    submit(
        &mut app,
        PlayerControlCommand {
            move_axis: Vec2::X,
            ..default()
        },
    );
    settle(&mut app, 24);
    eprintln!(
        "autostep final position={:?}, grounded={}",
        motion(&app).position,
        motion(&app).grounded
    );
    assert!(
        motion(&app).position.x > 0.8,
        "the real KCC must advance over the step"
    );
    assert!(
        motion(&app).position.y > 0.15,
        "the capsule must climb the authored 20cm step"
    );
    assert!(motion(&app).grounded);
}

pub(super) struct WallGuardTimer {
    started: std::time::Instant,
    casts: u64,
    preflights: u64,
}

impl WallGuardTimer {
    pub(super) fn new() -> Self {
        Self {
            started: std::time::Instant::now(),
            casts: 0,
            preflights: 0,
        }
    }
    pub(super) fn cast(&mut self) {
        self.casts += 1;
    }
    pub(super) fn preflight(&mut self) {
        self.preflights += 1;
    }
}

impl Drop for WallGuardTimer {
    fn drop(&mut self) {
        use std::sync::atomic::{AtomicU64, Ordering};
        static STEPS: AtomicU64 = AtomicU64::new(0);
        static CASTS: AtomicU64 = AtomicU64::new(0);
        static PREFLIGHTS: AtomicU64 = AtomicU64::new(0);
        static TOTAL_NS: AtomicU64 = AtomicU64::new(0);
        static MAX_NS: AtomicU64 = AtomicU64::new(0);
        let nanos = u64::try_from(self.started.elapsed().as_nanos()).unwrap_or(u64::MAX);
        CASTS.fetch_add(self.casts, Ordering::Relaxed);
        PREFLIGHTS.fetch_add(self.preflights, Ordering::Relaxed);
        TOTAL_NS.fetch_add(nanos, Ordering::Relaxed);
        MAX_NS.fetch_max(nanos, Ordering::Relaxed);
        let steps = STEPS.fetch_add(1, Ordering::Relaxed) + 1;
        if steps.is_multiple_of(120) {
            eprintln!(
                "wall guard cost: sampled_steps={steps}, casts={}, preflights={}, average_ns_per_guarded_step={}, max_ns={}",
                CASTS.load(Ordering::Relaxed),
                PREFLIGHTS.load(Ordering::Relaxed),
                TOTAL_NS.load(Ordering::Relaxed) / steps,
                MAX_NS.load(Ordering::Relaxed)
            );
        }
    }
}

fn support_ray_observation(app: &mut App, anchor: Vec3) -> Option<(Vec3, Vec3, bool)> {
    let entity = app
        .world()
        .resource::<PlayerRuntimeBindings>()
        .player_entity?;
    let slide_angle = {
        let world = app.world_mut();
        world
            .query_filtered::<&KinematicCharacterController, With<ForgePlayerCharacter>>()
            .single(world)
            .expect("one player controller")
            .min_slope_slide_angle
    };
    let mut context_state = SystemState::<ReadRapierContext>::new(app.world_mut());
    let rapier = context_state.get(app.world()).ok()?;
    let context = rapier.single().ok()?;
    context
        .cast_ray_and_get_normal(
            anchor + Vec3::Y * 0.45,
            Vec3::NEG_Y,
            4096.0,
            true,
            QueryFilter::default()
                .exclude_sensors()
                .exclude_collider(entity),
        )
        .map(|(_, hit)| {
            (
                hit.point,
                hit.normal,
                hit.time_of_impact > 0.0
                    && nearby_walkable_support(anchor, hit.point, hit.normal, slide_angle),
            )
        })
}

fn assert_moving_player_brakes_and_stays_on_walkable_slope(degrees: f32) {
    let (mut app, _) = prepared_app_on_slope(-10.0, [], degrees.to_radians());
    start(&mut app);
    settle(&mut app, 90);
    let start_position = motion(&app).position;
    eprintln!(
        "tidewater braking {degrees}Â° start position={start_position:?}, grounded={}",
        motion(&app).grounded
    );

    submit(
        &mut app,
        PlayerControlCommand {
            move_axis: Vec2::Y,
            ..default()
        },
    );
    settle(&mut app, 24);
    let moving = motion(&app).clone();
    eprintln!(
        "tidewater braking {degrees}Â° moving position={:?}, velocity={:?}, grounded={}",
        moving.position, moving.velocity, moving.grounded
    );
    assert!(
        moving.position.xz().distance(start_position.xz()) > 0.10,
        "the KCC test must first observe meaningful movement"
    );
    assert!(
        moving.velocity.xz().length() > 0.05,
        "the source must expose horizontal motion before release"
    );

    submit(&mut app, PlayerControlCommand::default());
    let release_position = moving.position;
    settle(&mut app, 240);
    let stopped = motion(&app).clone();
    let release_displacement = stopped.position.xz().distance(release_position.xz());
    let stopped_output = player_controller_output(&mut app);
    eprintln!(
        "tidewater braking {degrees}Â° stopped position={:?}, velocity={:?}, grounded={}, kcc_grounded={}, kcc_sliding={}, release_displacement_m={release_displacement}",
        stopped.position,
        stopped.velocity,
        stopped.grounded,
        stopped_output.grounded,
        stopped_output.is_sliding_down_slope,
    );
    assert!(
        release_displacement <= 0.35,
        "post-release physical displacement must stay bounded: {release_displacement} m"
    );
    assert!(
        stopped.grounded,
        "the source must remain grounded after braking"
    );
    assert!(
        stopped_output.grounded,
        "the real KCC must confirm grounded contact after braking"
    );
    assert!(
        !stopped_output.is_sliding_down_slope,
        "a walkable slope must not report a slide after braking"
    );
    assert!(
        stopped.velocity.xz().length() <= 1.0e-4,
        "released walk velocity must be below 0.1 mm/s"
    );

    let settled_position = stopped.position;
    settle(&mut app, 120);
    let after_idle = motion(&app).clone();
    assert!(
        after_idle.position.xz().distance(settled_position.xz()) <= 0.02,
        "an idle grounded capsule must remain stationary after braking"
    );
    assert!(after_idle.velocity.xz().length() <= 1.0e-4);
}

#[test]
#[allow(
    clippy::float_cmp,
    reason = "The landing assertion verifies the exact zero velocity assigned by confirmed support."
)]
fn slope_contact_does_not_suppress_a_jump_or_turn_a_slide_into_support() {
    let (mut app, _) = prepared_app_on_slope(-10.0, [], 40.0_f32.to_radians());
    start(&mut app);
    settle(&mut app, 90);
    let grounded_y = motion(&app).position.y;
    submit(
        &mut app,
        PlayerControlCommand {
            jump: true,
            ..default()
        },
    );
    settle(&mut app, 12);
    assert!(!player_controller_output(&mut app).grounded);
    assert!(!snapshot(&app).grounded);
    assert!(motion(&app).position.y > grounded_y + 0.4);
    assert!(motion(&app).velocity.y > 1.0);
    settle(&mut app, 90);
    assert!(snapshot(&app).grounded);
    assert_eq!(motion(&app).velocity.y, 0.0);

    let (mut app, _) = prepared_app_on_slope(-10.0, [], 60.0_f32.to_radians());
    start(&mut app);
    let start_position = motion(&app).position;
    let mut saw_slide = false;
    for _ in 0..30 {
        settle(&mut app, 1);
        saw_slide |= player_controller_output(&mut app).is_sliding_down_slope;
    }
    assert!(saw_slide, "the real KCC must report the steep slope slide");
    assert!(motion(&app).position.y < start_position.y - 0.1);
    assert!(motion(&app).position.x < start_position.x - 0.05);
    assert!(
        motion(&app).velocity.y < -1.0,
        "a sliding capsule must retain gravity"
    );
}

#[test]
fn grounded_entry_switches_the_live_capsule_without_replacing_rapier_handle() {
    let (mut app, _) = prepared_app(2.0);
    let before =
        rapier_handle(&mut app).expect("Rapier must create the player collider while paused");

    start(&mut app);
    settle(&mut app, 2);

    let state = app.world().resource::<TidewaterPlayerState>();
    assert_eq!(state.motion.mode, Mode::Swim);
    assert_eq!(state.swim_entries, 1);
    assert_eq!(state.collider_mode, Mode::Swim);
    assert!(state.motion.floating);
    assert!((state.motion.position.y - (2.0 - 0.162 + 0.1)).abs() < 0.03);
    let (radius, segment_height, autostep, snap) = collider_configuration(&mut app);
    assert_eq!(radius.to_bits(), RADIUS.to_bits());
    assert!((segment_height - (SWIM_HEIGHT - 2.0 * RADIUS)).abs() <= 1.0e-6);
    assert!(!autostep && !snap);
    assert_eq!(rapier_handle(&mut app), Some(before));
    assert!(bindings(&app).backend_identity_stable);
}

#[test]
fn swimming_uses_real_fixed_updates_for_dive_neutrality_ascend_and_float() {
    let (mut app, _) = prepared_app(3.0);
    start(&mut app);
    settle(&mut app, 2);
    assert_eq!(motion(&app).mode, Mode::Swim);

    app.world_mut()
        .resource_mut::<TidewaterPlayerInput>()
        .descend = true;
    submit(
        &mut app,
        PlayerControlCommand {
            move_axis: Vec2::Y,
            ..default()
        },
    );
    for _ in 0..180 {
        settle(&mut app, 1);
        if motion(&app).water_h - motion(&app).position.y > 1.2 {
            break;
        }
    }
    assert!(!motion(&app).floating);
    assert!(motion(&app).velocity.y < 0.0);
    assert!(motion(&app).water_h - motion(&app).position.y > 1.2);

    app.world_mut()
        .resource_mut::<TidewaterPlayerInput>()
        .descend = false;
    submit(&mut app, PlayerControlCommand::default());
    settle(&mut app, 180);
    let neutral_y = motion(&app).position.y;
    settle(&mut app, 120);
    assert!((motion(&app).position.y - neutral_y).abs() < 0.002);
    assert!(!motion(&app).floating);

    app.world_mut()
        .resource_mut::<TidewaterPlayerInput>()
        .ascend = true;
    for _ in 0..360 {
        settle(&mut app, 1);
        if motion(&app).floating {
            break;
        }
    }
    assert!(motion(&app).floating);
    assert!(motion(&app).position.y <= motion(&app).water_h + 0.05);
    app.world_mut()
        .resource_mut::<TidewaterPlayerInput>()
        .ascend = false;
    let target = motion(&app).water_h - 0.162 + 0.1;
    let before_error = (motion(&app).position.y - target).abs();
    settle(&mut app, 60);
    assert!((motion(&app).position.y - target).abs() < before_error);
}

#[test]
fn shallow_water_returns_to_walk_with_the_walk_capsule() {
    let (mut app, queries) = prepared_app(3.0);
    start(&mut app);
    settle(&mut app, 2);
    assert_eq!(motion(&app).mode, Mode::Swim);

    accept_height(&queries, 2, 0.5);
    for _ in 0..600 {
        settle(&mut app, 1);
        if motion(&app).mode == Mode::Walk {
            break;
        }
    }

    let state = app.world().resource::<TidewaterPlayerState>();
    assert_eq!(state.motion.mode, Mode::Walk);
    assert_eq!(state.walk_returns, 1);
    assert!(state.motion.grounded);
    let (radius, segment_height, autostep, snap) = collider_configuration(&mut app);
    assert_eq!(radius.to_bits(), RADIUS.to_bits());
    assert!((segment_height - (WALK_HEIGHT - 2.0 * RADIUS)).abs() <= 1.0e-6);
    assert!(autostep && snap);
}

#[test]
fn readiness_barrier_keeps_player_and_water_sample_frozen() {
    let (mut app, queries) = prepared_app(0.0);
    let before = motion(&app).position;
    accept_height(&queries, 2, 3.0);
    submit(
        &mut app,
        PlayerControlCommand {
            move_axis: Vec2::Y,
            jump: true,
            ..default()
        },
    );
    app.world_mut()
        .resource_mut::<TidewaterPlayerInput>()
        .descend = true;

    for _ in 0..3 {
        app.update();
    }

    let state = app.world().resource::<TidewaterPlayerState>();
    assert_eq!(state.motion.mode, Mode::Walk);
    assert_eq!(state.motion.position, before);
    assert_eq!(state.water_query_step, 0);
    assert_eq!(snapshot(&app).fixed_tick, 0);
    assert_eq!(player_controller_translation(&mut app), None);
}

#[test]
fn held_forward_climbs_an_authored_ladder_on_the_same_rapier_player() {
    let exit = Vec3::new(1.0, 0.0, 0.0);
    let ladder = TidewaterLadder::new(Vec2::ZERO, 1.95, exit);
    let (mut app, _) = prepared_app_with_ladders(1.5, [ladder]);
    let before = rapier_handle(&mut app).expect("Rapier player collider");
    start(&mut app);
    settle(&mut app, 2);
    assert_eq!(motion(&app).mode, Mode::Swim);
    assert!(app.world().resource::<TidewaterPlayerState>().ladder_nearby);

    submit(
        &mut app,
        PlayerControlCommand {
            move_axis: Vec2::Y,
            ..default()
        },
    );
    settle(&mut app, 1);

    let state = app.world().resource::<TidewaterPlayerState>();
    assert_eq!(state.motion.mode, Mode::Walk);
    assert_eq!(state.motion.position, exit);
    assert_eq!(state.motion.velocity, Vec3::ZERO);
    assert_eq!(state.ladder_climbs, 1);
    assert_eq!(state.walk_returns, 1);
    let (_, segment_height, autostep, snap) = collider_configuration(&mut app);
    assert!((segment_height - (WALK_HEIGHT - 2.0 * RADIUS)).abs() <= 1.0e-6);
    assert!(autostep && snap);
    assert_eq!(rapier_handle(&mut app), Some(before));
    assert!(bindings(&app).backend_identity_stable);
}

#[test]
fn ladder_exit_resyncs_backend_pose_and_capsule_before_the_next_kcc_sweep() {
    let exit = Vec3::new(1.0, 0.0, 0.0);
    let ladder = TidewaterLadder::new(Vec2::ZERO, 1.95, exit);
    let (mut app, _) = prepared_app_with_ladders(1.5, [ladder]);
    start(&mut app);
    settle(&mut app, 2);

    submit(
        &mut app,
        PlayerControlCommand {
            move_axis: Vec2::Y,
            ..default()
        },
    );
    settle(&mut app, 1);
    assert_eq!(motion(&app).mode, Mode::Walk);
    assert!(
        app.world()
            .resource::<TidewaterPlayerState>()
            .backend_sync_pending()
    );

    let teleported_center = player_transform(&mut app).translation;
    let (stale_center, _, _) = backend_collider_configuration(&mut app);
    assert!(
        stale_center.distance(teleported_center) > 0.25,
        "the fixture must observe the post-physics teleport before backend synchronization"
    );

    settle(&mut app, 1);
    assert!(
        !app.world()
            .resource::<TidewaterPlayerState>()
            .backend_sync_pending()
    );
    assert_eq!(snapshot(&app).desired_translation, Vec3::ZERO);
    assert_eq!(snapshot(&app).effective_translation, Vec3::ZERO);
    let (backend_center, radius, segment_height) = backend_collider_configuration(&mut app);
    assert!(backend_center.distance(teleported_center) <= 1.0e-6);
    assert_eq!(radius.to_bits(), RADIUS.to_bits());
    assert!((segment_height - (WALK_HEIGHT - 2.0 * RADIUS)).abs() <= 1.0e-6);

    settle(&mut app, 1);
    let output = player_controller_output(&mut app);
    assert!(output.desired_translation.length() > 0.0);
    assert!(output.desired_translation.is_finite());
    assert!(output.effective_translation.is_finite());
    assert!(
        output
            .desired_translation
            .distance(snapshot(&app).desired_translation)
            <= 1.0e-6
    );
}

#[test]
fn held_ascend_climb_progress_survives_the_swim_surface_clamp() {
    let exit = Vec3::new(1.0, 0.0, 0.0);
    let ladder = TidewaterLadder::new(Vec2::ZERO, 3.0, exit);
    let (mut app, _) = prepared_app_with_ladders(1.5, [ladder]);
    start(&mut app);
    settle(&mut app, 2);
    assert_eq!(motion(&app).mode, Mode::Swim);
    app.world_mut()
        .resource_mut::<TidewaterPlayerInput>()
        .ascend = true;

    for _ in 0..120 {
        settle(&mut app, 1);
        if app.world().resource::<TidewaterPlayerState>().ladder_climbs == 1 {
            break;
        }
    }

    let state = app.world().resource::<TidewaterPlayerState>();
    assert_eq!(state.ladder_climbs, 1);
    assert_eq!(state.motion.mode, Mode::Walk);
    assert_eq!(state.motion.position, exit);
}

#[test]
fn authored_ladder_does_not_capture_a_walking_player() {
    let ladder = TidewaterLadder::new(Vec2::ZERO, 1.95, Vec3::new(1.0, 0.0, 0.0));
    let (mut app, _) = prepared_app_with_ladders(0.0, [ladder]);
    start(&mut app);
    settle(&mut app, 2);
    assert_eq!(motion(&app).mode, Mode::Walk);

    submit(
        &mut app,
        PlayerControlCommand {
            move_axis: Vec2::Y,
            ..default()
        },
    );
    settle(&mut app, 1);

    let state = app.world().resource::<TidewaterPlayerState>();
    assert_eq!(state.motion.mode, Mode::Walk);
    assert_eq!(state.ladder_climbs, 0);
    assert!(!state.ladder_nearby);
}

fn prepared_app(initial_height: f32) -> (App, Queries) {
    prepared_app_with_ladders(initial_height, [])
}

fn prepared_app_with_ladders(
    initial_height: f32,
    ladders: impl IntoIterator<Item = TidewaterLadder>,
) -> (App, Queries) {
    prepared_app_on_slope(initial_height, ladders, 0.0)
}

fn prepared_app_on_slope(
    initial_height: f32,
    ladders: impl IntoIterator<Item = TidewaterLadder>,
    slope_radians: f32,
) -> (App, Queries) {
    let queries = Queries::default();
    let terrain = Arc::new(terrain_on_slope(slope_radians));
    let mut profile = VisualValleyPlayerControllerRecipe
        .build()
        .expect("valid profile");
    profile.controller.capsule_radius_m = RADIUS;
    profile.controller.capsule_half_segment_m = WALK_HEIGHT * 0.5 - RADIUS;
    profile.controller.max_step_height_m = 0.4;
    profile.controller.walk_speed_mps = 3.0;
    profile.controller.sprint_multiplier = 6.2 / 3.0;
    profile.controller.jump_speed_mps = 4.6;
    profile.camera.eye_height_m = 1.62;
    let slope_clearance = RADIUS * (slope_radians.cos().recip() - 1.0);
    let spawn = Vec3::new(0.0, WALK_HEIGHT * 0.5 + slope_clearance + 0.02, 0.0);
    let mut app = App::new();
    app.add_plugins(MinimalPlugins)
        .add_plugins(TransformPlugin)
        .insert_resource(Time::<Fixed>::from_hz(60.0))
        .insert_resource(TimeUpdateStrategy::ManualDuration(Duration::from_secs_f64(
            1.0 / 60.0,
        )))
        .insert_resource(TimestepMode::Fixed {
            dt: 1.0 / 60.0,
            substeps: 1,
        })
        .add_plugins(RapierPhysicsPlugin::<NoUserData>::default().in_fixed_schedule())
        .add_plugins(PlayerControllerPlugin::new(profile, spawn).with_interactive_input(false))
        .add_plugins(
            TidewaterPlayerPlugin::new(queries.clone(), terrain)
                .expect("allocate source player water slot")
                .with_interactive_input(false)
                .with_ladders(ladders),
        )
        .insert_resource(PlayerSimulationReady(false));
    app.world_mut().spawn((
        Collider::cuboid(3.0, 0.1, 3.0),
        Transform::from_xyz(0.0, -0.1 / slope_radians.cos(), 0.0)
            .with_rotation(Quat::from_rotation_z(slope_radians)),
    ));
    accept_height(&queries, 1, initial_height);
    for _ in 0..4 {
        app.update();
    }
    assert_eq!(snapshot(&app).fixed_tick, 0);
    (app, queries)
}

fn start(app: &mut App) {
    app.world_mut().resource_mut::<PlayerSimulationReady>().0 = true;
    settle(app, 1);
}

fn terrain_on_slope(slope_radians: f32) -> SampledTerrainPlan {
    let bytes: Vec<u8> = (0..4)
        .flat_map(|_| [-1.5_f32, -0.5, 0.5, 1.5])
        .map(|x| x * slope_radians.tan())
        .flat_map(f32::to_le_bytes)
        .collect();
    let mut document = new_terrain("swim", "test", "Swim test terrain").expect("terrain");
    document.schema_version = TERRAIN_DOCUMENT_SCHEMA_VERSION_V4;
    document.extent_m = TerrainExtent::new(4.0, 4.0);
    document.height_sampling.algorithm = HeightSamplingAlgorithm::SampledRasterV1;
    document.height_sampling.sample_spacing_m = 1.0;
    document.height_sampling.base_elevation_m = 0.0;
    document.height_sampling.amplitude_m = 0.0;
    document.height_sampling.base_frequency_cycles_per_m = 1.0;
    document.height_sampling.octaves = 1;
    document.height_sampling.lacunarity = 2.0;
    document.height_sampling.persistence = 0.5;
    document.sampled_source = Some(SampledTerrainSource {
        schema: SAMPLED_TERRAIN_SCHEMA.into(),
        schema_version: 1,
        width: 4,
        height: 4,
        origin_xz_m: [-2.0; 2],
        sample_spacing_m: 1.0,
        outside_height_m: -90.0,
        encoding: HeightEncoding::Float32Le,
        samples_sha256: bytes_sha256(&bytes),
    });
    SampledTerrainPlan::from_document(&document, &bytes).expect("flat sampled terrain")
}

fn accept_height(queries: &Queries, step: u32, height: f32) {
    let mut bytes = vec![0_u8; crate::tidewater_water_query::PACKET_BYTES];
    bytes[..4].copy_from_slice(&step.to_le_bytes());
    bytes[4..8].copy_from_slice(&2_u32.to_le_bytes());
    bytes[8..16].copy_from_slice(&(f64::from(step) / 60.0).to_le_bytes());
    for slot in 0..crate::tidewater_water_query::MAX_QUERIES {
        let result = 16 + crate::tidewater_water_query::MAX_QUERIES * 16 + slot * 16;
        bytes[result..result + 4].copy_from_slice(&height.to_le_bytes());
    }
    assert_eq!(queries.accept(&bytes), Ok(true));
}

fn settle(app: &mut App, ticks: usize) {
    for _ in 0..ticks {
        let before = snapshot(app).fixed_tick;
        app.update();
        assert_eq!(
            snapshot(app).fixed_tick,
            before + 1,
            "one update must execute exactly one Tidewater fixed drive"
        );
    }
}

fn submit(app: &mut App, command: PlayerControlCommand) {
    app.world_mut()
        .resource_mut::<PendingPlayerControlCommand>()
        .submit(command)
        .expect("finite probe command");
}

fn motion(app: &App) -> &crate::tidewater_locomotion::State {
    &app.world().resource::<TidewaterPlayerState>().motion
}

fn snapshot(app: &App) -> PlayerRuntimeSnapshot {
    app.world().resource::<PlayerRuntimeSnapshot>().clone()
}

fn bindings(app: &App) -> super::super::PlayerRuntimeBindingSnapshot {
    app.world()
        .resource::<PlayerRuntimeBindings>()
        .snapshot()
        .expect("player bindings")
}

fn rapier_handle(app: &mut App) -> Option<(u32, u32)> {
    let world = app.world_mut();
    world
        .query_filtered::<&RapierColliderHandle, With<ForgePlayerCharacter>>()
        .single(world)
        .ok()
        .map(|handle| handle.0.into_raw_parts())
}

fn collider_configuration(app: &mut App) -> (f32, f32, bool, bool) {
    let world = app.world_mut();
    let (collider, controller) = world
        .query_filtered::<(&Collider, &KinematicCharacterController), With<ForgePlayerCharacter>>()
        .single(world)
        .expect("one player collider");
    let capsule = collider.as_capsule().expect("capsule collider");
    (
        capsule.radius(),
        capsule.height(),
        controller.autostep.is_some(),
        controller.snap_to_ground.is_some(),
    )
}

fn backend_collider_configuration(app: &mut App) -> (Vec3, f32, f32) {
    let handle = {
        let world = app.world_mut();
        world
            .query_filtered::<&RapierColliderHandle, With<ForgePlayerCharacter>>()
            .single(world)
            .expect("one player collider handle")
            .0
    };
    let world = app.world_mut();
    let colliders = world
        .query_filtered::<&RapierContextColliders, With<DefaultRapierContext>>()
        .single(world)
        .expect("one default Rapier collider set");
    let collider = colliders
        .colliders
        .get(handle)
        .expect("player collider in Rapier backend");
    let translation = collider.position().translation;
    let capsule = collider
        .shape()
        .as_capsule()
        .expect("player backend shape is a capsule");
    (
        Vec3::new(translation.x, translation.y, translation.z),
        capsule.radius,
        capsule.height(),
    )
}

fn player_transform(app: &mut App) -> Transform {
    let world = app.world_mut();
    *world
        .query_filtered::<&Transform, With<ForgePlayerCharacter>>()
        .single(world)
        .expect("one player transform")
}

fn player_controller_output(app: &mut App) -> KinematicCharacterControllerOutput {
    let world = app.world_mut();
    world
        .query_filtered::<&KinematicCharacterControllerOutput, With<ForgePlayerCharacter>>()
        .single(world)
        .expect("fresh player KCC output")
        .clone()
}

fn player_controller_translation(app: &mut App) -> Option<Vec3> {
    let world = app.world_mut();
    world
        .query_filtered::<&KinematicCharacterController, With<ForgePlayerCharacter>>()
        .single(world)
        .expect("one player controller")
        .translation
}

#[test]
fn real_tidewater_boardwalk_entry_guard_preserves_recoverable_step() {
    let (mut app, _) = prepared_app(0.0);
    let floors: Vec<Entity> = {
        let world = app.world_mut();
        world
            .query_filtered::<Entity, (With<Collider>, Without<ForgePlayerCharacter>)>()
            .iter(world)
            .collect()
    };
    for floor in floors {
        app.world_mut().despawn(floor);
    }
    let bytes = include_bytes!("../tests/fixtures/tidewater-entry/tile.f32");
    let heights = bytes
        .as_chunks::<4>()
        .0
        .iter()
        .map(|b| f32::from_le_bytes(*b))
        .collect();
    app.world_mut().spawn((
        Collider::heightfield(heights, 129, 129, Vec3::new(128.0, 1.0, 128.0)),
        Transform::from_xyz(64.0, 0.0, -64.0),
    ));
    let board = app
        .world_mut()
        .spawn((
            crate::physics::yawed_box_collider(
                Vec3::new(0.9, 0.281_936_14, 0.521_314_6),
                -2.829_825_6,
            )
            .expect("verified box"),
            Transform::from_xyz(42.753_773, 3.681_234, -106.722_855),
        ))
        .id();
    for _ in 0..4 {
        app.update();
    }
    let entity = bindings(&app).player_entity;
    app.world_mut()
        .get_mut::<Transform>(entity)
        .expect("transform")
        .translation = Vec3::new(42.667_072, 3.699_310_3 + WALK_HEIGHT * 0.5, -107.56561);
    for _ in 0..4 {
        app.update();
    }
    let controller = app
        .world()
        .get::<KinematicCharacterController>(entity)
        .expect("KCC")
        .clone();
    let mut context_state = SystemState::<ReadRapierContext>::new(app.world_mut());
    let rapier = context_state.get(app.world()).expect("context access");
    let context = rapier.single().expect("context");
    let desired = Vec3::new(0.0, -9.81 / 3600.0, 0.05);
    let mut candidates = vec![board];
    let constrained = constrain_wall_drive(&context, entity, &controller, desired, &mut candidates);
    eprintln!(
        "boardwalk entry desired={desired:?} constrained={constrained:?} candidates={candidates:?}"
    );
    assert!(
        constrained.z > 0.02,
        "Recoverable source step must retain forward demand instead of projecting into terrain-box deadlock: {constrained:?}"
    );
}

fn prepare_real_tidewater_entry_app() -> App {
    let (mut app, _) = prepared_app(0.0);
    let document: forge_project::TerrainDocument = serde_json::from_str(include_str!(
        "../tests/fixtures/tidewater-entry-full/terrain-document.json"
    ))
    .expect("canonical terrain document");
    let plan = Arc::new(
        SampledTerrainPlan::from_document(
            &document,
            include_bytes!("../tests/fixtures/tidewater-entry-full/terrain-samples.bin"),
        )
        .expect("pinned raster"),
    );
    app.world_mut().resource_mut::<Surface>().terrain = plan;
    let floors: Vec<Entity> = {
        let world = app.world_mut();
        world
            .query_filtered::<Entity, (With<Collider>, Without<ForgePlayerCharacter>)>()
            .iter(world)
            .collect()
    };
    for floor in floors {
        app.world_mut().despawn(floor);
    }
    let bytes = include_bytes!("../tests/fixtures/tidewater-entry/tile.f32");
    let heights = bytes
        .as_chunks::<4>()
        .0
        .iter()
        .map(|b| f32::from_le_bytes(*b))
        .collect();
    app.world_mut().spawn((
        Collider::heightfield(heights, 129, 129, Vec3::new(128.0, 1.0, 128.0)),
        Transform::from_xyz(64.0, 0.0, -64.0),
    ));
    app
}

fn spawn_real_tidewater_entry_boardwalks(app: &mut App) -> (Vec<Entity>, Entity) {
    let boardwalks: serde_json::Value = serde_json::from_str(include_str!(
        "../tests/fixtures/tidewater-entry-full/boardwalks.json"
    ))
    .expect("pinned boardwalk projection");
    let mut board_entities = Vec::new();
    let mut observed_board = None;
    for segment in boardwalks["boardwalks"].as_array().expect("segments") {
        let vec3 = |v: &serde_json::Value| {
            Vec3::from_array(serde_json::from_value(v.clone()).expect("source vector"))
        };
        assert_eq!(
            segment["source_frame"]["rotation_xyzw"],
            serde_json::json!([0, 0, 0, 1])
        );
        assert_eq!(
            segment["source_frame"]["scale"],
            serde_json::json!([1, 1, 1])
        );
        let frame = vec3(&segment["source_frame"]["translation_m"]);
        for b in segment["colliders"]["boxes"].as_array().expect("boxes") {
            if b["solid"].as_bool() != Some(true) {
                continue;
            }
            let collider = crate::physics::yawed_box_collider(
                vec3(&b["half"]),
                serde_json::from_value(b["yaw"].clone()).expect("source yaw"),
            )
            .expect("source box");
            let center = frame + vec3(&b["center"]);
            let entity = app
                .world_mut()
                .spawn((collider, Transform::from_translation(center)))
                .id();
            board_entities.push(entity);
            if center.distance(Vec3::new(42.753_773, 3.681_234, -106.722_855)) < 0.0001 {
                observed_board = Some(entity);
            }
        }
    }
    (
        board_entities,
        observed_board.expect("observed blocking box"),
    )
}

fn observe_source_deck_sample(
    app: &mut App,
    entity: Entity,
    board_entities: &[Entity],
    sample: u32,
) {
    if sample.is_multiple_of(10) || sample == 119 {
        let state = app.world().resource::<TidewaterPlayerState>();
        let anchor = state.motion.position;
        eprintln!(
            "DECK_SAMPLE sample={sample} anchor={anchor:?} velocity={:?} grounded={} ground_height={} feedback={:?} deck_contact_ids={:?}",
            state.motion.velocity,
            state.motion.grounded,
            state.ground_height,
            state.kcc_feedback(),
            state
                .kcc_contacts()
                .iter()
                .filter(|(e, _)| board_entities.contains(e))
                .collect::<Vec<_>>()
        );
        let mut ctx_state = SystemState::<ReadRapierContext>::new(app.world_mut());
        let rapier = ctx_state.get(app.world()).expect("diagnostic context");
        let context = rapier.single().expect("single context");
        let support = context.cast_ray_and_get_normal(
            anchor + Vec3::Y * 0.45,
            Vec3::NEG_Y,
            4096.0,
            true,
            QueryFilter::default()
                .exclude_collider(entity)
                .exclude_sensors(),
        );
        eprintln!(
            "DECK_RAY sample={sample} support={support:?} source_deck={}",
            support
                .as_ref()
                .is_some_and(|(e, _)| board_entities.contains(e))
        );
    }
}

#[test]
fn real_tidewater_boardwalk_entry_full_motion_advances_over_deck() {
    let mut app = prepare_real_tidewater_entry_app();
    let (board_entities, observed_board) = spawn_real_tidewater_entry_boardwalks(&mut app);
    let anchor = Vec3::new(42.667_072, 3.699_310_3, -107.56561);
    let entity = bindings(&app).player_entity;
    app.world_mut()
        .get_mut::<Transform>(entity)
        .expect("transform")
        .translation = anchor + Vec3::Y * (WALK_HEIGHT * 0.5);
    {
        let mut state = app.world_mut().resource_mut::<TidewaterPlayerState>();
        state.motion.position = anchor;
        state.motion.velocity = Vec3::ZERO;
        state.motion.grounded = true;
        state.motion.mode = Mode::Walk;
        state.ground_height = anchor.y;
        state.wall_candidates = vec![observed_board];
    }
    for _ in 0..4 {
        app.update();
    }
    start(&mut app);
    submit(
        &mut app,
        PlayerControlCommand {
            move_axis: Vec2::new(0.3, -1.0),
            ..default()
        },
    );
    app.world_mut()
        .resource_mut::<TidewaterPlayerState>()
        .wall_candidates = vec![observed_board];
    let mut deck_support = false;
    for sample in 0..120 {
        app.update();
        observe_source_deck_sample(&mut app, entity, &board_entities, sample);
        deck_support |= app
            .world()
            .resource::<TidewaterPlayerState>()
            .kcc_contacts()
            .iter()
            .any(|(entity, normal)| {
                board_entities.contains(entity)
                    && normal.is_some_and(|n| n.y > 45.0_f32.to_radians().cos())
            });
    }
    assert!(
        deck_support,
        "Crossing requires observed walkable support on a source deck box, not terrain beside it"
    );
    let after = motion(&app);
    eprintln!(
        "full source deck start={anchor:?} final={:?} grounded={} contacts={:?}",
        after.position,
        after.grounded,
        app.world()
            .resource::<TidewaterPlayerState>()
            .kcc_contacts()
    );
    assert!(
        after.position.z > -105.5,
        "Full ECS controller must pass the source deck entry: {:?}",
        after.position
    );
    assert!(
        after.grounded,
        "Source deck crossing must keep real support"
    );
    assert!(after.position.is_finite());
}
