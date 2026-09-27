mod authoring_paths;
mod manual_assets;
pub use authoring_paths::{
    PathSurfaceColliderIdentity, profiled_path_collider, profiled_path_collider_present,
};
mod manual_projection;
mod manual_readiness;
pub use manual_projection::{
    ManualPlacementProjection, manual_placements_ready, placement_transform,
};
pub(crate) use manual_readiness::static_collider_ready;
pub use manual_readiness::{ManualPlacementCollisionExpected, ManualPlacementCollisionStatus};
mod terrain;
pub(crate) use manual_assets::convex_collider;
pub use manual_assets::{
    ManualPlacementCollision, ManualPlacementCollisionFailed, ManualPlacementCollisionPlugin,
};

pub(crate) use terrain::build_terrain_heightfield_collider;
pub use terrain::{
    TERRAIN_PHYSICS_HZ, TerrainHeightfieldCollider, TerrainPhysicsAdapterError,
    TerrainPhysicsPlugin,
};
