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
pub use manual_readiness::{ManualPlacementCollisionExpected, ManualPlacementCollisionStatus};
pub(crate) use manual_readiness::{static_collider_ready, static_sensor_ready};
mod terrain;
pub use manual_assets::{
    ManualPlacementCollision, ManualPlacementCollisionFailed, ManualPlacementCollisionPlugin,
    fish_stand_colliders,
};
pub(crate) use manual_assets::{convex_collider, yawed_box_collider};

pub(crate) use terrain::build_terrain_heightfield_collider;
pub use terrain::{
    TERRAIN_PHYSICS_HZ, TerrainHeightfieldCollider, TerrainPhysicsAdapterError,
    TerrainPhysicsPlugin,
};
