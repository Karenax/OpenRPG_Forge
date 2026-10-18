//! Derived terrain picking and brush outline. No authoring mutation occurs here.
use crate::sampled_terrain_physics::{SampledTerrainCollider, SampledTerrainPhysicsState};
use crate::{TerrainHeightfieldCollider, TerrainRuntimeState};
use bevy::prelude::*;
use bevy_rapier3d::prelude::{QueryFilter, ReadRapierContext};
use forge_core::ContentHash;
use forge_world::TerrainGenerationPlan;

#[derive(Resource, Default)]
pub struct TerrainBrushCursor {
    pub normalized_position: Option<Vec2>,
    pub radius_m: f32,
    pub hit: Option<Vec3>,
    /// Ordered event positions, including events received between rendered frames.
    pub pending_positions: Vec<Vec2>,
    /// One result per submitted position; misses must not silently join separate paths.
    pub path_hits: Vec<Option<Vec3>>,
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::type_complexity,
    clippy::too_many_arguments,
    clippy::cast_possible_truncation,
    reason = "Bevy queries and validated finite terrain heights"
)]
pub fn update_terrain_brush_cursor(
    mut cursor: ResMut<TerrainBrushCursor>,
    cameras: Query<
        (&Camera, &GlobalTransform),
        (
            With<Camera3d>,
            Without<crate::scene_camera::OffscreenSceneCamera>,
        ),
    >,
    colliders: Query<
        (
            Option<&TerrainHeightfieldCollider>,
            Option<&SampledTerrainCollider>,
        ),
        Or<(
            With<TerrainHeightfieldCollider>,
            With<SampledTerrainCollider>,
        )>,
    >,
    context: ReadRapierContext,
    state: Option<Res<TerrainRuntimeState>>,
    sampled: Option<Res<SampledTerrainPhysicsState>>,
    mut gizmos: Gizmos,
    mut cached_plan: Local<Option<(ContentHash, TerrainGenerationPlan)>>,
) {
    cursor.hit = None;
    let positions = std::mem::take(&mut cursor.pending_positions);
    cursor.path_hits = vec![None; positions.len().min(1024)];
    if positions.len() > 1024 {
        return;
    }
    let Some(position) = cursor.normalized_position else {
        return;
    };
    if !position.is_finite()
        || position.min_element() < 0.0
        || position.max_element() > 1.0
        || !cursor.radius_m.is_finite()
        || (cursor.radius_m != 0.0 && !(2.0..=128.0).contains(&cursor.radius_m))
    {
        return;
    }
    let Ok((camera, transform)) = cameras.single() else {
        return;
    };
    let Some(size) = camera.logical_viewport_size() else {
        return;
    };
    let Ok(context) = context.single() else {
        return;
    };
    let terrain_only = |entity| {
        let Ok((procedural, imported)) = colliders.get(entity) else {
            return false;
        };
        if let Some(sampled) = &sampled {
            imported.is_some_and(|c| c.document_hash == sampled.plan.document_hash())
        } else {
            state.is_some() && procedural.is_some()
        }
    };
    let pick = |position: Vec2| {
        if !position.is_finite() || position.min_element() < 0.0 || position.max_element() > 1.0 {
            return None;
        }
        let ray = camera.viewport_to_world(transform, position * size).ok()?;
        let (_, distance) = context.cast_ray(
            ray.origin,
            *ray.direction,
            4096.0,
            true,
            QueryFilter::default().predicate(&terrain_only),
        )?;
        Some(ray.origin + *ray.direction * distance)
    };
    cursor.path_hits = positions.into_iter().map(pick).collect();
    let Some(hit) = pick(position) else {
        return;
    };
    cursor.hit = Some(hit);
    // A zero radius is a read-only hover query for the Studio coordinate HUD.
    if cursor.radius_m == 0.0 {
        return;
    }
    if sampled.is_none() {
        let Some(state) = &state else {
            return;
        };
        let hash = state.cache().source_document_hash;
        if cached_plan.as_ref().is_none_or(|(old, _)| *old != hash) {
            *cached_plan = TerrainGenerationPlan::from_document(state.document())
                .ok()
                .map(|plan| (hash, plan));
        }
    }
    let mut previous = None;
    for index in 0_u16..=64 {
        let angle = f32::from(index) / 64.0 * std::f32::consts::TAU;
        let x = hit.x + angle.cos() * cursor.radius_m;
        let z = hit.z + angle.sin() * cursor.radius_m;
        let height = if let Some(sampled) = &sampled {
            sampled
                .plan
                .height_at(f64::from(x), f64::from(z))
                .ok()
                .map(|h| h as f32)
        } else {
            cached_plan
                .as_ref()
                .and_then(|(_, plan)| plan.height_at(x, z).ok())
        };
        let next = height.map(|height| Vec3::new(x, height + 0.06, z));
        if let (Some(from), Some(to)) = (previous, next) {
            gizmos.line(from, to, Color::srgb(1.0, 0.75, 0.1));
        }
        previous = next;
    }
}
