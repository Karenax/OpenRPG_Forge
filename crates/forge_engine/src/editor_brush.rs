//! Presentation-only terrain targeting using Bevy's existing mesh ray caster.
use crate::editor_viewport::{EditorCamera, EditorTerrainChunk, EditorViewport};
use bevy::prelude::*;
use serde::Serialize;

fn terrain_ray_settings(filter: &impl Fn(Entity) -> bool) -> MeshRayCastSettings<'_> {
    // The ring may extend beyond the camera frustum. Use the terrain's propagated
    // visibility, not the per-view flag reset/recomputed during PostUpdate.
    MeshRayCastSettings::default()
        .with_filter(filter)
        .with_visibility(RayCastVisibility::Visible)
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct EditorBrushPointer {
    pub generation: u64,
    pub serial: u64,
    /// Normalized canvas coordinates, top-left [0, 0], bottom-right [1, 1].
    pub uv: [f32; 2],
    pub radius_m: f32,
    pub strength: f32,
}

#[derive(Clone, Copy, Debug, PartialEq, Serialize)]
pub struct EditorBrushHit {
    pub generation: u64,
    pub serial: u64,
    pub point: [f32; 3],
}

/// One Studio canvas sample used to resolve a placement drop against the presented terrain.
///
/// `viewport_uv` is normalized against the canvas client rectangle: top-left is `[0, 0]` and
/// bottom-right is `[1, 1]`. The caller must match both `generation` and `serial` on the returned
/// [`EditorSurfaceHit`] before requesting a persistent mutation.
#[derive(Clone, Copy, Debug, PartialEq)]
pub struct EditorSurfacePointer {
    pub generation: u64,
    pub serial: u64,
    pub viewport_uv: [f32; 2],
}

/// Read-only placement bridge result. The native command path remains responsible for sampling
/// the canonical terrain again before persisting Y.
#[derive(Clone, Copy, Debug, PartialEq, Serialize)]
pub struct EditorSurfaceHit {
    pub generation: u64,
    pub serial: u64,
    pub project_id: forge_core::ProjectId,
    pub area_id: forge_core::AreaId,
    pub revision: u64,
    pub world: [f32; 3],
}

#[derive(Default)]
pub(crate) struct CursorCache {
    key: Option<(EditorBrushPointer, Mat4, Vec2)>,
    hit: Option<EditorBrushHit>,
    segments: Vec<Vec<Vec3>>,
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy system parameters are injected by value"
)]
pub(crate) fn update_brush_cursor(
    viewport: Res<EditorViewport>,
    cameras: Query<(&Camera, &GlobalTransform), With<EditorCamera>>,
    terrain: Query<(), With<EditorTerrainChunk>>,
    mut ray_cast: MeshRayCast,
    mut gizmos: Gizmos,
    mut cache: Local<CursorCache>,
) {
    let Ok(mut bridge) = viewport.0.lock() else {
        return;
    };
    bridge.brush_hit = None;
    bridge.brush_sample_serial = None;
    let Some(pointer) = bridge.brush_pointer else {
        return;
    };
    if !valid_pointer(
        pointer,
        bridge.status.generation,
        bridge.desired_generation,
        bridge.status.chunks,
    ) {
        return;
    }
    let Ok((camera, transform)) = cameras.single() else {
        return;
    };
    let Some(rect) = camera.logical_viewport_rect() else {
        return;
    };
    if let Some(start) = bridge.brush_start_pointer.take()
        && valid_pointer(
            start,
            bridge.status.generation,
            bridge.desired_generation,
            bridge.status.chunks,
        )
    {
        let filter = |entity| terrain.contains(entity);
        let settings = terrain_ray_settings(&filter);
        bridge.brush_start_hit = camera
            .viewport_to_world(
                transform,
                rect.min + Vec2::from_array(start.uv) * rect.size(),
            )
            .ok()
            .and_then(|ray| {
                ray_cast
                    .cast_ray(ray, &settings)
                    .first()
                    .map(|(_, hit)| EditorBrushHit {
                        generation: start.generation,
                        serial: start.serial,
                        point: hit.point.to_array(),
                    })
            });
    }
    let key = (pointer, transform.to_matrix(), rect.size());
    if cache.key == Some(key) {
        bridge.brush_hit = cache.hit;
        bridge.brush_sample_serial = Some(pointer.serial);
        draw_cursor(&cache, pointer.strength, &mut gizmos);
        return;
    }
    let position = rect.min + Vec2::from_array(pointer.uv) * rect.size();
    let Ok(ray) = camera.viewport_to_world(transform, position) else {
        return;
    };
    *cache = CursorCache {
        key: Some(key),
        ..default()
    };
    bridge.brush_sample_serial = Some(pointer.serial);
    let filter = |entity| terrain.contains(entity);
    let settings = terrain_ray_settings(&filter);
    let Some((_, hit)) = ray_cast.cast_ray(ray, &settings).first() else {
        return;
    };
    let center = hit.point;
    bridge.brush_hit = Some(EditorBrushHit {
        generation: pointer.generation,
        serial: pointer.serial,
        point: center.to_array(),
    });
    cache.hit = bridge.brush_hit;
    // Project the ring onto the displayed terrain, including hills and chunk seams.
    let mut ring = Vec::with_capacity(49);
    for index in 0_u16..=48 {
        let angle = f32::from(index) / 48.0 * std::f32::consts::TAU;
        let x = center.x + pointer.radius_m * angle.cos();
        let z = center.z + pointer.radius_m * angle.sin();
        let ray = Ray3d::new(Vec3::new(x, 1100.0, z), Dir3::NEG_Y);
        if let Some((_, hit)) = ray_cast.cast_ray(ray, &settings).first() {
            ring.push(hit.point + Vec3::Y * 0.15);
        } else if ring.len() > 1 {
            cache.segments.push(std::mem::take(&mut ring));
        } else {
            ring.clear();
        }
    }
    if ring.len() > 1 {
        cache.segments.push(ring);
    }
    draw_cursor(&cache, pointer.strength, &mut gizmos);
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy system parameters are injected by value"
)]
pub(crate) fn update_surface_pointer(
    viewport: Res<EditorViewport>,
    cameras: Query<(&Camera, &GlobalTransform), With<EditorCamera>>,
    terrain: Query<(), With<EditorTerrainChunk>>,
    mut ray_cast: MeshRayCast,
) {
    let Ok(mut bridge) = viewport.0.lock() else {
        return;
    };
    bridge.surface_hit = None;
    bridge.surface_sample_serial = None;
    let Some(pointer) = bridge.surface_pointer else {
        return;
    };
    let Some((project_id, area_id)) = valid_surface_pointer(pointer, &bridge) else {
        return;
    };
    let Ok((camera, transform)) = cameras.single() else {
        return;
    };
    let Some(rect) = camera.logical_viewport_rect() else {
        return;
    };
    bridge.surface_sample_serial = Some(pointer.serial);
    let position = rect.min + Vec2::from_array(pointer.viewport_uv) * rect.size();
    let Ok(ray) = camera.viewport_to_world(transform, position) else {
        return;
    };
    let filter = |entity| terrain.contains(entity);
    let settings = terrain_ray_settings(&filter);
    let Some((_, hit)) = ray_cast.cast_ray(ray, &settings).first() else {
        return;
    };
    bridge.surface_hit = Some(EditorSurfaceHit {
        generation: pointer.generation,
        serial: pointer.serial,
        project_id,
        area_id,
        revision: bridge.status.revision,
        world: hit.point.to_array(),
    });
}

fn valid_surface_pointer(
    pointer: EditorSurfacePointer,
    bridge: &crate::editor_viewport::EditorViewportBridge,
) -> Option<(forge_core::ProjectId, forge_core::AreaId)> {
    if pointer.generation != bridge.status.generation
        || pointer.generation != bridge.desired_generation
        || bridge.status.chunks != 64
        || !pointer
            .viewport_uv
            .iter()
            .all(|value| value.is_finite() && (0.0..=1.0).contains(value))
    {
        return None;
    }
    Some((bridge.status.project_id?, bridge.status.area_id?))
}

fn draw_cursor(cache: &CursorCache, strength: f32, gizmos: &mut Gizmos) {
    for segment in &cache.segments {
        gizmos.linestrip(segment.iter().copied(), Color::srgb(1.0, 0.80, 0.25));
    }
    if let Some(hit) = cache.hit {
        let center = Vec3::from_array(hit.point);
        gizmos.line(
            center + Vec3::Y * 0.2,
            center + Vec3::Y * (0.2 + 6.0 * strength),
            Color::WHITE,
        );
    }
}

fn valid_pointer(
    pointer: EditorBrushPointer,
    generation: u64,
    desired: u64,
    chunks: usize,
) -> bool {
    pointer.generation == generation
        && generation == desired
        && chunks == 64
        && pointer
            .uv
            .iter()
            .all(|value| value.is_finite() && (0.0..=1.0).contains(value))
        && pointer.radius_m.is_finite()
        && (2.0..=64.0).contains(&pointer.radius_m)
        && pointer.strength.is_finite()
        && (f32::MIN_POSITIVE..=1.0).contains(&pointer.strength)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn brush_raycast_uses_visible_terrain_without_requiring_a_view_flag() {
        use bevy::{camera::primitives::MeshAabb, ecs::system::SystemState};

        let mut app = App::new();
        app.add_plugins(MinimalPlugins);
        let world = app.world_mut();
        let mesh = Mesh::from(Cuboid::new(16.0, 2.0, 16.0));
        let bounds = mesh.compute_aabb().unwrap();
        let mut meshes = Assets::<Mesh>::default();
        let handle = meshes.add(mesh);
        world.insert_resource(meshes);
        let terrain_entity = world
            .spawn((
                EditorTerrainChunk,
                Mesh3d(handle.clone()),
                bounds,
                GlobalTransform::IDENTITY,
                InheritedVisibility::VISIBLE,
                ViewVisibility::HIDDEN,
            ))
            .id();
        // A closer non-terrain marker must never intercept the brush.
        world.spawn((
            Mesh3d(handle),
            bounds,
            GlobalTransform::from_translation(Vec3::Y * 5.0),
            InheritedVisibility::VISIBLE,
            ViewVisibility::HIDDEN,
        ));
        let ray = Ray3d::new(Vec3::Y * 20.0, Dir3::NEG_Y);
        let mut state =
            SystemState::<(MeshRayCast, Query<(), With<EditorTerrainChunk>>)>::new(world);
        {
            let (mut ray_cast, terrain) = state.get_mut(world).unwrap();
            let filter = |entity| terrain.contains(entity);
            assert!(
                ray_cast
                    .cast_ray(ray, &MeshRayCastSettings::default().with_filter(&filter))
                    .is_empty()
            );
            let hits = ray_cast.cast_ray(ray, &terrain_ray_settings(&filter));
            assert_eq!(hits.len(), 1);
            assert_eq!(hits[0].0, terrain_entity);
            assert!((hits[0].1.point.y - 1.0).abs() < 0.0001);
        }
        world
            .entity_mut(terrain_entity)
            .insert(InheritedVisibility::HIDDEN);
        let (mut ray_cast, terrain) = state.get_mut(world).unwrap();
        let filter = |entity| terrain.contains(entity);
        assert!(
            ray_cast
                .cast_ray(ray, &terrain_ray_settings(&filter))
                .is_empty()
        );
    }
    #[test]
    fn bevy_raycast_hits_the_same_authored_triangle_surface() {
        use bevy::picking::mesh_picking::ray_cast::{Backfaces, ray_mesh_intersection};
        let mut document = forge_world::embedded_visual_valley_terrain_document()
            .unwrap()
            .migrate_to_latest()
            .unwrap();
        document.height_deltas = vec![forge_project::TerrainHeightDelta::new(136, 139, 15.0)];
        let plan = forge_world::TerrainGenerationPlan::from_document(&document).unwrap();
        let terrain = plan.generate().unwrap();
        let chunk = terrain
            .chunks
            .iter()
            .find(|chunk| chunk.coordinate.x == 4 && chunk.coordinate.z == 4)
            .unwrap();
        for (x, z) in [(16.25, 22.25), (17.75, 23.75), (20.0, 25.0)] {
            let ray = Ray3d::new(Vec3::new(x, 1100.0, z), Dir3::NEG_Y);
            let hit = ray_mesh_intersection(
                ray,
                &bevy::math::Affine3A::IDENTITY,
                &chunk.positions_m,
                Some(&chunk.normals),
                Some(&chunk.indices),
                None,
                Backfaces::Cull,
            )
            .unwrap();
            assert!((hit.point.y - plan.surface_at(x, z).unwrap().height_m).abs() < 0.0002);
        }
    }
    #[test]
    fn pointer_rejects_stale_partial_and_invalid_projection() {
        let valid = EditorBrushPointer {
            generation: 8,
            serial: 1,
            uv: [0.5, 0.5],
            radius_m: 8.0,
            strength: 0.5,
        };
        assert!(valid_pointer(valid, 8, 8, 64));
        assert!(!valid_pointer(valid, 7, 8, 64));
        assert!(!valid_pointer(valid, 8, 9, 64));
        assert!(!valid_pointer(valid, 8, 8, 63));
        assert!(!valid_pointer(
            EditorBrushPointer {
                uv: [f32::NAN, 0.5],
                ..valid
            },
            8,
            8,
            64
        ));
        assert!(!valid_pointer(
            EditorBrushPointer {
                radius_m: 1000.0,
                ..valid
            },
            8,
            8,
            64
        ));
    }

    #[test]
    fn placement_surface_pointer_requires_exact_presented_generation_and_scope() {
        let pointer = EditorSurfacePointer {
            generation: 8,
            serial: 21,
            viewport_uv: [0.25, 0.75],
        };
        let project_id = forge_core::ProjectId::deterministic("surface/project");
        let area_id = forge_core::AreaId::deterministic("surface/area");
        let mut bridge = crate::editor_viewport::EditorViewportBridge {
            desired_generation: 8,
            status: crate::editor_viewport::EditorViewportStatus {
                generation: 8,
                project_id: Some(project_id),
                area_id: Some(area_id),
                chunks: 64,
                ..default()
            },
            ..default()
        };

        assert_eq!(
            valid_surface_pointer(pointer, &bridge),
            Some((project_id, area_id))
        );
        bridge.desired_generation = 9;
        assert_eq!(valid_surface_pointer(pointer, &bridge), None);
        bridge.desired_generation = 8;
        bridge.status.project_id = None;
        assert_eq!(valid_surface_pointer(pointer, &bridge), None);
        bridge.status.project_id = Some(project_id);
        assert_eq!(
            valid_surface_pointer(
                EditorSurfacePointer {
                    viewport_uv: [f32::NAN, 0.5],
                    ..pointer
                },
                &bridge,
            ),
            None
        );
    }
}
