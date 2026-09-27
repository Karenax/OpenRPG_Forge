//! Real Rapier synchronization and raycast after imported terrain replacement.
use bevy::{ecs::system::SystemState, prelude::*};
use bevy_rapier3d::{
    plugin::context::{DefaultRapierContext, RapierContextColliders},
    prelude::{QueryFilter, ReadRapierContext},
};
use forge_engine::sampled_terrain_physics::{
    PreparedSampledTerrainPhysics, SampledTerrainCollider, SampledTerrainPhysicsState,
};
use forge_project::{TerrainDocument, TerrainHeightDelta};
use forge_world::sampled_terrain::SampledTerrainPlan;
use std::{error::Error, fs, sync::Arc};

#[allow(clippy::cast_possible_truncation)] // Validated f32 terrain range.
fn check(app: &mut App, plan: &SampledTerrainPlan) -> f32 {
    assert!(
        !ready(app, plan),
        "new ECS generation is not synchronized yet"
    );
    app.update();
    app.world_mut().run_schedule(FixedUpdate);
    assert!(ready(app, plan), "current backend generation must be ready");
    let count = {
        let mut query = app
            .world_mut()
            .query_filtered::<&RapierContextColliders, With<DefaultRapierContext>>();
        query
            .single(app.world())
            .expect("default Rapier world")
            .colliders
            .len()
    };
    assert_eq!(count, 256, "old collisions remain in the backend");
    let expected = plan.height_at(1.0, 1.0).expect("terrain height") as f32;
    let mut query = SystemState::<ReadRapierContext>::new(app.world_mut());
    let contexts = query.get(app.world()).expect("Rapier system parameters");
    let context = contexts.single().expect("Rapier query world");
    let (entity, distance) = context
        .cast_ray(
            Vec3::new(1.0, expected + 100.0, 1.0),
            Vec3::NEG_Y,
            200.0,
            true,
            QueryFilter::default(),
        )
        .expect("imported ground hit");
    let height = expected + 100.0 - distance;
    assert!((height - expected).abs() < 0.001);
    let marker = app
        .world()
        .get::<SampledTerrainCollider>(entity)
        .expect("current terrain tile");
    assert_eq!(marker.document_hash, plan.document_hash());
    assert!(
        app.world()
            .resource::<SampledTerrainPhysicsState>()
            .entities
            .contains(&entity)
    );
    height
}

fn ready(app: &App, plan: &SampledTerrainPlan) -> bool {
    app.world()
        .resource::<SampledTerrainPhysicsState>()
        .is_synchronized(app.world(), plan.document_hash())
}
fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args().skip(1).collect();
    if args.len() != 2 {
        return Err("TERRAIN_JSON HEIGHTS_BIN required".into());
    }
    let mut doc = TerrainDocument::from_json(&fs::read_to_string(&args[0])?)?;
    let bytes = fs::read(&args[1])?;
    let original = Arc::new(SampledTerrainPlan::from_document(&doc, &bytes)?);
    let mut app = App::new();
    app.add_plugins((MinimalPlugins, TransformPlugin));
    forge_engine::sampled_terrain_physics::register(&mut app);
    forge_engine::sampled_terrain_physics::register(&mut app);
    PreparedSampledTerrainPhysics::new(original.clone())?.stage(app.world_mut());
    let first = check(&mut app, &original);
    let tile = app
        .world()
        .resource::<SampledTerrainPhysicsState>()
        .entities[0];
    let saved = *app.world().get::<Transform>(tile).expect("tile pose");
    app.world_mut()
        .get_mut::<Transform>(tile)
        .expect("tile pose")
        .translation
        .y += 1.0;
    assert!(
        !ready(&app, &original),
        "unsynchronized local pose must be rejected"
    );
    *app.world_mut()
        .get_mut::<Transform>(tile)
        .expect("tile pose") = saved;
    assert!(ready(&app, &original));
    doc.height_deltas = vec![TerrainHeightDelta::new(1025, 1025, 12.0)];
    let edited = Arc::new(SampledTerrainPlan::from_document(&doc, &bytes)?);
    assert!(
        !ready(&app, &edited),
        "old generation cannot acknowledge the edit"
    );
    PreparedSampledTerrainPhysics::new(edited.clone())?.stage(app.world_mut());
    let second = check(&mut app, &edited);
    assert!((second - first - 12.0).abs() < 0.001);
    PreparedSampledTerrainPhysics::new(original.clone())?.stage(app.world_mut());
    let restored = check(&mut app, &original);
    assert_eq!(first.to_bits(), restored.to_bits());
    println!(
        "SAMPLED_BACKEND_PASS backend_colliders=256 first={first} edited={second} restored={restored}"
    );
    Ok(())
}
