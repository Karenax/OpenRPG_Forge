//! Derived one-metre collision tiles for the imported terrain authoring surface.
//! Installation in ECS is not a receipt that Rapier has synchronized its query world.
use bevy::prelude::*;
use bevy_rapier3d::prelude::Collider;
use forge_core::ContentHash;
use forge_world::sampled_terrain::SampledTerrainPlan;
use std::sync::Arc;

/// Register the same fixed-step backend without constructing a procedural terrain.
pub fn register(app: &mut App) {
    use bevy_rapier3d::prelude::{NoUserData, RapierPhysicsPlugin, TimestepMode};
    if !app.is_plugin_added::<RapierPhysicsPlugin<NoUserData>>() {
        app.insert_resource(Time::<Fixed>::from_hz(60.0))
            .insert_resource(TimestepMode::Fixed {
                dt: 1.0 / 60.0,
                substeps: 1,
            })
            .add_plugins(RapierPhysicsPlugin::<NoUserData>::default().in_fixed_schedule());
    }
}

#[derive(Component, Clone, Copy, Debug)]
pub struct SampledTerrainCollider {
    pub document_hash: ContentHash,
    pub minimum_cell: [u16; 2],
}

#[derive(Resource)]
pub struct SampledTerrainPhysicsState {
    pub plan: Arc<SampledTerrainPlan>,
    pub entities: Vec<Entity>,
}

impl SampledTerrainPhysicsState {
    /// Checks live backend shapes and poses, not merely the presence of ECS handles.
    /// Call again after source replacement; this is not a persistent readiness receipt.
    #[must_use]
    pub fn is_synchronized(&self, world: &World, expected: ContentHash) -> bool {
        if self.plan.document_hash() != expected || self.entities.len() != 256 {
            return false;
        }
        let mut cells = std::collections::HashSet::with_capacity(256);
        self.entities.iter().all(|&entity| {
            world
                .get::<SampledTerrainCollider>(entity)
                .is_some_and(|marker| {
                    marker.document_hash == expected
                        && marker
                            .minimum_cell
                            .iter()
                            .all(|&cell| cell < 2048 && cell % 128 == 0)
                        && cells.insert(marker.minimum_cell)
                        && crate::physics::static_collider_ready(world, entity)
                })
        })
    }
}

pub struct PreparedSampledTerrainPhysics {
    plan: Arc<SampledTerrainPlan>,
    tiles: Vec<Tile>,
}
struct Tile {
    minimum_cell: [u16; 2],
    collider: Collider,
    center: Vec3,
}

impl PreparedSampledTerrainPhysics {
    /// Fixed unit sampling, independent of camera LOD. No persistent alternate terrain.
    #[allow(clippy::cast_possible_truncation)] // Validated f32 heights and fixed integer coordinates.
    pub fn new(plan: Arc<SampledTerrainPlan>) -> Result<Self, String> {
        let doc = plan.document();
        let source = doc
            .sampled_source
            .as_ref()
            .ok_or("Missing imported terrain source")?;
        if source.width != 2048
            || source.height != 2048
            || source.sample_spacing_m.to_bits() != 1.0_f64.to_bits()
            || source.origin_xz_m.map(f64::to_bits) != [-1024.0_f64; 2].map(f64::to_bits)
            || doc.height_sampling.sample_spacing_m.to_bits() != 1.0_f32.to_bits()
            || doc.extent_m.size_x_m.to_bits() != 2048.0_f32.to_bits()
            || doc.extent_m.size_z_m.to_bits() != 2048.0_f32.to_bits()
        {
            return Err("Imported collisions require the 2048 metre unit grid".into());
        }
        let mut tiles = Vec::with_capacity(256);
        for z in (0_u16..2048).step_by(128) {
            for x in (0_u16..2048).step_by(128) {
                let mut heights = Vec::with_capacity(129 * 129);
                // Rapier uses column-major samples: Z changes fastest, then X.
                for column in 0_u16..=128 {
                    for row in 0_u16..=128 {
                        let height = plan
                            .height_at(f64::from(x + column) - 1024.0, f64::from(z + row) - 1024.0)
                            .map_err(|e| e.to_string())?
                            as f32;
                        if !height.is_finite() {
                            return Err("Nonfinite collision height".into());
                        }
                        heights.push(height);
                    }
                }
                tiles.push(Tile {
                    minimum_cell: [x, z],
                    collider: Collider::heightfield(
                        heights,
                        129,
                        129,
                        Vec3::new(128.0, 1.0, 128.0),
                    ),
                    center: Vec3::new(f32::from(x) - 960.0, 0.0, f32::from(z) - 960.0),
                });
            }
        }
        Ok(Self { plan, tiles })
    }

    #[must_use]
    pub fn tile_count(&self) -> usize {
        self.tiles.len()
    }

    /// Caller supplies the existing Rapier world. Backend synchronization is separate.
    pub fn stage(self, world: &mut World) {
        let hash = self.plan.document_hash();
        let entities = self
            .tiles
            .into_iter()
            .map(|tile| {
                world
                    .spawn((
                        SampledTerrainCollider {
                            document_hash: hash,
                            minimum_cell: tile.minimum_cell,
                        },
                        // A collider without a rigid body is static in Rapier.
                        tile.collider,
                        Transform::from_translation(tile.center),
                    ))
                    .id()
            })
            .collect();
        if let Some(previous) = world.remove_resource::<SampledTerrainPhysicsState>() {
            for entity in previous.entities {
                world.despawn(entity);
            }
        }
        world.insert_resource(SampledTerrainPhysicsState {
            plan: self.plan,
            entities,
        });
    }
}
