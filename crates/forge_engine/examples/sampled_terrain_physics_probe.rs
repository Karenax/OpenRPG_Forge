//! Imported terrain collision shapes and atomic ECS replacement, without GPU claims.
use bevy::prelude::*;
use bevy_rapier3d::{
    parry::{
        math::Vector,
        query::{Ray, RayCast},
    },
    prelude::Collider,
};
use forge_engine::sampled_terrain_physics::{
    PreparedSampledTerrainPhysics, SampledTerrainCollider, SampledTerrainPhysicsState,
};
use forge_project::{TerrainDocument, TerrainHeightDelta};
use forge_world::sampled_terrain::SampledTerrainPlan;
use std::{error::Error, fs, sync::Arc};

fn hit(world: &mut World, cell: [u16; 2], x: f32, z: f32, expected: f32) -> f32 {
    let mut query = world.query::<(&SampledTerrainCollider, &Collider, &Transform)>();
    let (_, collider, transform) = query
        .iter(world)
        .find(|(c, _, _)| c.minimum_cell == cell)
        .expect("collision tile");
    let ray = Ray::new(
        Vector::new(
            x - transform.translation.x,
            expected + 100.0,
            z - transform.translation.z,
        ),
        Vector::new(0.0, -1.0, 0.0),
    );
    let time = collider
        .as_heightfield()
        .expect("heightfield")
        .raw
        .cast_local_ray(&ray, 200.0, true)
        .expect("terrain hit");
    let height = expected + 100.0 - time;
    assert!(
        (height - expected).abs() < 0.001,
        "collision height {height}, expected {expected}"
    );
    height
}
#[allow(clippy::cast_possible_truncation)] // Verified finite terrain heights.
fn verify(world: &mut World, plan: &SampledTerrainPlan) -> f32 {
    assert_eq!(
        world.query::<&SampledTerrainCollider>().iter(world).count(),
        256
    );
    assert_eq!(
        world
            .resource::<SampledTerrainPhysicsState>()
            .plan
            .document_hash(),
        plan.document_hash()
    );
    for cell in [[0, 0], [896, 1024], [1024, 1024], [1920, 1920]] {
        let x = f32::from(cell[0]) - 1024.0 + 16.0;
        let z = f32::from(cell[1]) - 1024.0 + 21.0;
        let expected = plan.height_at(f64::from(x), f64::from(z)).unwrap() as f32;
        hit(world, cell, x, z, expected);
    }
    let center = plan.height_at(0.0, 16.0).unwrap();
    for (cell, x, neighbor) in [([896, 1024], -0.001, -1.0), ([1024, 1024], 0.001, 1.0)] {
        let expected = (center * 0.999 + plan.height_at(neighbor, 16.0).unwrap() * 0.001) as f32;
        hit(world, cell, x, 16.0, expected);
    }
    let expected = plan.height_at(1.0, 1.0).unwrap() as f32;
    hit(world, [1024, 1024], 1.0, 1.0, expected)
}
fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args().skip(1).collect();
    if args.len() != 2 {
        return Err("TERRAIN_JSON HEIGHTS_BIN required".into());
    }
    let mut doc = TerrainDocument::from_json(&fs::read_to_string(&args[0])?)?;
    let bytes = fs::read(&args[1])?;
    let original = Arc::new(SampledTerrainPlan::from_document(&doc, &bytes)?);
    let mut world = World::new();
    let before = world.entities().len();
    let prepared = PreparedSampledTerrainPhysics::new(original.clone())?;
    assert_eq!(prepared.tile_count(), 256);
    assert_eq!(world.entities().len(), before);
    prepared.stage(&mut world);
    let initial = verify(&mut world, &original);
    let old = world
        .resource::<SampledTerrainPhysicsState>()
        .entities
        .clone();
    doc.height_deltas = vec![TerrainHeightDelta::new(1025, 1025, 12.0)];
    let edited = Arc::new(SampledTerrainPlan::from_document(&doc, &bytes)?);
    PreparedSampledTerrainPhysics::new(edited.clone())?.stage(&mut world);
    assert!(old.into_iter().all(|e| world.get_entity(e).is_err()));
    let raised = verify(&mut world, &edited);
    assert!((raised - initial - 12.0).abs() < 0.001);
    PreparedSampledTerrainPhysics::new(original.clone())?.stage(&mut world);
    let restored = verify(&mut world, &original);
    assert_eq!(initial.to_bits(), restored.to_bits());
    println!(
        "SAMPLED_PHYSICS_PASS tiles=256 initial={initial} raised={raised} restored={restored}; rays and seams verified"
    );
    Ok(())
}
