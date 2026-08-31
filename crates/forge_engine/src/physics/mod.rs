mod terrain;

pub(crate) use terrain::build_terrain_heightfield_collider;
pub use terrain::{
    TERRAIN_PHYSICS_HZ, TerrainHeightfieldCollider, TerrainPhysicsAdapterError,
    TerrainPhysicsPlugin,
};
