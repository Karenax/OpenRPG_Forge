use super::*;
use bevy::{ecs::system::SystemState, prelude::*, time::TimeUpdateStrategy};
use bevy_rapier3d::prelude::*;
use std::time::Duration;

fn app() -> App {
    let mut app = App::new();
    app.add_plugins((MinimalPlugins, TransformPlugin))
        .insert_resource(TimeUpdateStrategy::ManualDuration(Duration::from_secs_f64(
            1.0 / 60.0,
        )));
    crate::sampled_terrain_physics::register(&mut app);
    app.update();
    for mut configuration in app
        .world_mut()
        .query::<&mut RapierConfiguration>()
        .iter_mut(app.world_mut())
    {
        configuration.physics_pipeline_active = false;
    }
    app
}

fn ray(world: &mut World, active: bool, x: f32, expected: Entity) -> Option<(Entity, f32)> {
    let mut state = SystemState::<ReadRapierContext>::new(world);
    let read = state.get(world).unwrap();
    let context = read.single().unwrap();
    let predicate = |entity| entity == expected;
    with_queries(
        &context,
        active,
        QueryFilter::default().predicate(&predicate),
        |queries| queries.cast_ray(Vec3::new(x, 10.0, 0.0), Vec3::NEG_Y, 20.0, true),
    )
}

#[test]
fn held_world_queries_fresh_shapes_without_advancing_dynamics_and_resume_normally() {
    let mut app = app();
    let floor = app
        .world_mut()
        .spawn((Collider::cuboid(2.0, 0.5, 2.0), Transform::IDENTITY))
        .id();
    let body = app
        .world_mut()
        .spawn((
            RigidBody::Dynamic,
            Collider::ball(0.5),
            Transform::from_xyz(0.0, 4.0, 0.0),
            Velocity::linear(Vec3::X),
        ))
        .id();
    for _ in 0..3 {
        app.update();
    }
    assert!(crate::physics::static_collider_ready(app.world(), floor));
    // The original ReadRapierContext query misses even this synchronized floor.
    assert!(ray(app.world_mut(), true, 0.0, floor).is_none());
    let (hit, distance) = ray(app.world_mut(), false, 0.0, floor).unwrap();
    assert_eq!(hit, floor);
    assert!((distance - 9.5).abs() < 0.0001);
    for _ in 0..4 {
        assert!(ray(app.world_mut(), false, 0.0, floor).is_some());
    }
    assert_eq!(
        app.world()
            .get::<Transform>(body)
            .unwrap()
            .translation
            .to_array()
            .map(f32::to_bits),
        [0.0_f32, 4.0, 0.0].map(f32::to_bits)
    );
    assert_eq!(
        app.world()
            .get::<Velocity>(body)
            .unwrap()
            .linear
            .to_array()
            .map(f32::to_bits),
        Vec3::X.to_array().map(f32::to_bits)
    );
    for mut configuration in app
        .world_mut()
        .query::<&mut RapierConfiguration>()
        .iter_mut(app.world_mut())
    {
        configuration.physics_pipeline_active = true;
    }
    for _ in 0..3 {
        app.update();
    }
    assert!(ray(app.world_mut(), true, 0.0, floor).is_some());
    assert!(app.world().get::<Transform>(body).unwrap().translation.x > 0.0);
}

#[test]
fn held_queries_follow_moves_disabled_shapes_and_removal_without_stale_hits() {
    let mut app = app();
    let floor = app
        .world_mut()
        .spawn((Collider::cuboid(2.0, 0.5, 2.0), Transform::IDENTITY))
        .id();
    for _ in 0..3 {
        app.update();
    }
    assert!(ray(app.world_mut(), false, 0.0, floor).is_some());
    app.world_mut()
        .get_mut::<Transform>(floor)
        .unwrap()
        .translation
        .x = 20.0;
    for _ in 0..2 {
        app.update();
    }
    assert!(ray(app.world_mut(), false, 0.0, floor).is_none());
    assert!(ray(app.world_mut(), false, 20.0, floor).is_some());
    app.world_mut().entity_mut(floor).insert(ColliderDisabled);
    for _ in 0..2 {
        app.update();
    }
    assert!(ray(app.world_mut(), false, 20.0, floor).is_none());
    app.world_mut()
        .entity_mut(floor)
        .remove::<ColliderDisabled>();
    for _ in 0..2 {
        app.update();
    }
    assert!(ray(app.world_mut(), false, 20.0, floor).is_some());
    app.world_mut().despawn(floor);
    for _ in 0..2 {
        app.update();
    }
    assert!(ray(app.world_mut(), false, 20.0, floor).is_none());
}
