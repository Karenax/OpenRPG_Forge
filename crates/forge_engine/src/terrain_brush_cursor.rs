//! Derived terrain picking and brush outline. No authoring mutation occurs here.
use crate::sampled_terrain_physics::{SampledTerrainCollider, SampledTerrainPhysicsState};
use crate::{TerrainHeightfieldCollider, TerrainRuntimeState};
use bevy::prelude::*;
use bevy::{
    camera::primitives::Aabb,
    math::bounding::Aabb3d,
    mesh::Indices,
    picking::mesh_picking::ray_cast::{Backfaces, ray_aabb_intersection_3d, ray_mesh_intersection},
};
use bevy_rapier3d::prelude::{
    DefaultRapierContext, QueryFilter, RapierConfiguration, ReadRapierContext,
};
use forge_core::ContentHash;
use forge_world::TerrainGenerationPlan;

const MAX_PICK_DISTANCE_M: f32 = 4096.0;

type TerrainMeshes<'w, 's> = Query<
    'w,
    's,
    (
        &'static Mesh3d,
        &'static GlobalTransform,
        &'static Aabb,
        &'static InheritedVisibility,
        Option<&'static crate::cell_streaming::StreamedTerrainCell>,
        Option<&'static crate::cell_streaming::StreamedTerrainHlod>,
        Option<&'static crate::source_terrain::host::PhysicsProjection>,
    ),
    Or<(
        With<crate::cell_streaming::StreamedTerrainCell>,
        With<crate::cell_streaming::StreamedTerrainHlod>,
    )>,
>;

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
            Option<&crate::cell_streaming::StreamedTerrainCell>,
        ),
        Or<(
            With<TerrainHeightfieldCollider>,
            With<SampledTerrainCollider>,
            With<crate::cell_streaming::StreamedTerrainCell>,
        )>,
    >,
    context: ReadRapierContext,
    configuration: Query<&RapierConfiguration, With<DefaultRapierContext>>,
    state: Option<Res<TerrainRuntimeState>>,
    sampled: Option<Res<SampledTerrainPhysicsState>>,
    streamed: Option<Res<crate::cell_streaming::ResidentSampledTerrain>>,
    meshes: Option<Res<Assets<Mesh>>>,
    terrain_meshes: TerrainMeshes,
    render_source: crate::cell_streaming::TerrainRenderSource,
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
    let Ok(configuration) = configuration.single() else {
        return;
    };
    let terrain_only = |entity| {
        let Ok((procedural, imported, cell)) = colliders.get(entity) else {
            return false;
        };
        if let Some(streamed) = &streamed {
            cell.is_some_and(|cell| {
                cell.source == streamed.plan().document_hash()
                    && cell.detail == forge_world::cell_streaming::CellDetail::Full
            })
        } else if let Some(sampled) = &sampled {
            imported.is_some_and(|c| c.document_hash == sampled.plan.document_hash())
        } else {
            state.is_some() && procedural.is_some()
        }
    };
    let (path_hits, hit) = crate::physics::scene_queries::with_queries(
        &context,
        configuration.physics_pipeline_active,
        QueryFilter::default().predicate(&terrain_only),
        |queries| {
            let pick = |position: Vec2| {
                if !position.is_finite()
                    || position.min_element() < 0.0
                    || position.max_element() > 1.0
                {
                    return None;
                }
                let ray = camera.viewport_to_world(transform, position * size).ok()?;
                let physical = queries
                    .cast_ray(ray.origin, *ray.direction, MAX_PICK_DISTANCE_M, true)
                    .map(|(_, distance)| ray.origin + *ray.direction * distance);
                render_source.identity().zip(meshes.as_deref()).map_or(
                    physical,
                    |(source, meshes)| {
                        rendered_terrain_hit(ray, source, &terrain_meshes, meshes, physical)
                    },
                )
            };
            (positions.into_iter().map(pick).collect(), pick(position))
        },
    );
    cursor.path_hits = path_hits;
    let Some(hit) = hit else {
        return;
    };
    cursor.hit = Some(hit);
    // A zero radius is a read-only hover query for the Studio coordinate HUD.
    if cursor.radius_m == 0.0 {
        return;
    }
    if sampled.is_none() && streamed.is_none() {
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
    let sampled_plan = streamed
        .as_ref()
        .map(|source| source.plan().as_ref())
        .or_else(|| sampled.as_ref().map(|source| source.plan.as_ref()));
    draw_outline(
        &mut gizmos,
        hit,
        cursor.radius_m,
        sampled_plan,
        cached_plan.as_ref().map(|(_, plan)| plan),
    );
}

/// Reuse Bevy's mesh intersection on the already resident terrain only.
/// The source renderer deliberately hides its matching partition geometry;
/// that owned projection remains queryable without another raster or collider.
fn rendered_terrain_hit(
    ray: Ray3d,
    source: ContentHash,
    terrain: &TerrainMeshes,
    meshes: &Assets<Mesh>,
    physical: Option<Vec3>,
) -> Option<Vec3> {
    let mut nearest = physical;
    let mut distance = physical.map_or(MAX_PICK_DISTANCE_M, |point| point.distance(ray.origin));
    for (mesh, transform, bounds, visibility, cell, hlod, projection) in terrain {
        let owned = match (cell, hlod) {
            (Some(cell), None) => cell.source == source,
            (None, Some(hlod)) => hlod.source == source,
            _ => false,
        };
        let source_projection = projection.is_some_and(|projection| projection.0 == source);
        if !owned || (!visibility.get() && !source_projection) {
            continue;
        }
        let affine = transform.affine();
        if ray_aabb_intersection_3d(
            ray,
            &Aabb3d::new(bounds.center, bounds.half_extents),
            &affine,
        )
        .is_none_or(|near| near > distance)
        {
            continue;
        }
        let Some(mesh) = meshes.get(&mesh.0) else {
            continue;
        };
        if mesh.primitive_topology() != bevy::mesh::PrimitiveTopology::TriangleList {
            continue;
        }
        let Some(positions) = mesh
            .try_attribute(Mesh::ATTRIBUTE_POSITION)
            .ok()
            .and_then(|positions| positions.as_float3())
        else {
            continue;
        };
        // Normal/UV interpolation is unnecessary for an authoring position.
        let hit = match mesh.try_indices().ok() {
            Some(Indices::U16(indices)) => ray_mesh_intersection(
                ray,
                &affine,
                positions,
                None,
                Some(indices),
                None,
                Backfaces::Include,
            ),
            Some(Indices::U32(indices)) => ray_mesh_intersection(
                ray,
                &affine,
                positions,
                None,
                Some(indices),
                None,
                Backfaces::Include,
            ),
            None => ray_mesh_intersection::<u32>(
                ray,
                &affine,
                positions,
                None,
                None,
                None,
                Backfaces::Include,
            ),
        };
        if let Some(hit) = hit
            && hit.distance <= distance
        {
            distance = hit.distance;
            nearest = Some(hit.point);
        }
    }
    nearest
}

#[allow(
    clippy::cast_possible_truncation,
    reason = "validated finite terrain heights are rendered in Bevy coordinates"
)]
fn draw_outline(
    gizmos: &mut Gizmos,
    hit: Vec3,
    radius_m: f32,
    sampled: Option<&forge_world::sampled_terrain::SampledTerrainPlan>,
    procedural: Option<&TerrainGenerationPlan>,
) {
    let mut previous = None;
    for index in 0_u16..=64 {
        let angle = f32::from(index) / 64.0 * std::f32::consts::TAU;
        let x = hit.x + angle.cos() * radius_m;
        let z = hit.z + angle.sin() * radius_m;
        let height = if let Some(sampled) = sampled {
            sampled
                .height_at(f64::from(x), f64::from(z))
                .ok()
                .map(|h| h as f32)
        } else {
            procedural.and_then(|plan| plan.height_at(x, z).ok())
        };
        let next = height.map(|height| Vec3::new(x, height + 0.06, z));
        if let (Some(from), Some(to)) = (previous, next) {
            gizmos.line(from, to, Color::srgb(1.0, 0.75, 0.1));
        }
        previous = next;
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::{asset::RenderAssetUsages, ecs::system::SystemState, mesh::PrimitiveTopology};
    use forge_core::EntityId;
    use forge_world::{
        cell_streaming::{CellDetail, CellKey},
        terrain_hlod::{TerrainHlodKey, TerrainHlodTile},
    };

    fn quad(world: &mut World, height: f32) -> Entity {
        let mut mesh = Mesh::new(
            PrimitiveTopology::TriangleList,
            RenderAssetUsages::default(),
        );
        mesh.insert_attribute(
            Mesh::ATTRIBUTE_POSITION,
            vec![
                [-2.0, 0.0, -2.0],
                [2.0, 0.0, -2.0],
                [-2.0, 0.0, 2.0],
                [2.0, 0.0, 2.0],
            ],
        );
        mesh.insert_indices(Indices::U32(vec![0, 2, 1, 1, 2, 3]));
        let handle = world.resource_mut::<Assets<Mesh>>().add(mesh);
        world
            .spawn((
                Mesh3d(handle),
                GlobalTransform::from_translation(Vec3::Y * height),
                Aabb::from_min_max(Vec3::new(-2.0, 0.0, -2.0), Vec3::new(2.0, 0.0, 2.0)),
                InheritedVisibility::VISIBLE,
            ))
            .id()
    }

    fn cell(source: ContentHash) -> crate::cell_streaming::StreamedTerrainCell {
        crate::cell_streaming::StreamedTerrainCell {
            logical_id: EntityId::deterministic("cursor-cell"),
            key: CellKey { x: 0, z: 0 },
            detail: CellDetail::Hlod,
            source,
        }
    }

    fn hlod(source: ContentHash) -> crate::cell_streaming::StreamedTerrainHlod {
        crate::cell_streaming::StreamedTerrainHlod {
            logical_id: EntityId::deterministic("cursor-hlod"),
            tile: TerrainHlodTile {
                key: TerrainHlodKey {
                    x: 0,
                    z: 0,
                    level: 1,
                },
                finer_edges: [false; 4],
            },
            source,
        }
    }

    fn hit(world: &mut World, source: ContentHash, physical: Option<Vec3>) -> Option<Vec3> {
        let mut state: SystemState<(TerrainMeshes, Res<Assets<Mesh>>)> = SystemState::new(world);
        let (query, meshes) = state.get(world).unwrap();
        rendered_terrain_hit(
            Ray3d::new(Vec3::new(0.25, 10.0, 0.25), Dir3::NEG_Y),
            source,
            &query,
            &meshes,
            physical,
        )
    }

    #[test]
    fn visible_terrain_without_colliders_is_picked_by_source_and_nearest_surface() {
        let mut world = World::new();
        world.init_resource::<Assets<Mesh>>();
        let source = ContentHash::from_bytes([3; 32]);
        quad(&mut world, 9.0); // A nearer scene object is not the authoring terrain.
        let stale = quad(&mut world, 8.0);
        world
            .entity_mut(stale)
            .insert(cell(ContentHash::from_bytes([4; 32])));
        let hidden = quad(&mut world, 7.0);
        world
            .entity_mut(hidden)
            .insert((cell(source), InheritedVisibility::HIDDEN));
        let coarse = quad(&mut world, 0.0);
        world.entity_mut(coarse).insert(cell(source));
        let distant = quad(&mut world, 2.0);
        world.entity_mut(distant).insert(hlod(source));
        let expected = Vec3::new(0.25, 2.0, 0.25);
        assert!(hit(&mut world, source, None).unwrap().distance(expected) < 1e-5);
        let physical = Vec3::new(0.25, 4.0, 0.25);
        assert_eq!(hit(&mut world, source, Some(physical)), Some(physical));
        assert!(
            hit(&mut world, source, Some(Vec3::new(0.25, -1.0, 0.25)))
                .unwrap()
                .distance(expected)
                < 1e-5
        );
        world
            .entity_mut(distant)
            .insert(hlod(ContentHash::from_bytes([4; 32])));
        assert!(hit(&mut world, source, None).unwrap().y.abs() < 1e-5);
        world.entity_mut(coarse).insert(InheritedVisibility::HIDDEN);
        assert!(hit(&mut world, source, None).is_none());
        assert!(
            world
                .get::<bevy_rapier3d::prelude::Collider>(coarse)
                .is_none()
        );
        assert!(
            world
                .get::<bevy_rapier3d::prelude::Collider>(distant)
                .is_none()
        );
    }

    #[test]
    fn terrain_picking_refuses_mixed_ownership_missing_payloads_and_excessive_distance() {
        let mut world = World::new();
        world.init_resource::<Assets<Mesh>>();
        let source = ContentHash::from_bytes([3; 32]);
        let far = quad(&mut world, -MAX_PICK_DISTANCE_M);
        world.entity_mut(far).insert(hlod(source));
        assert!(hit(&mut world, source, None).is_none());
        let entity = quad(&mut world, 2.0);
        world
            .entity_mut(entity)
            .insert((cell(source), hlod(source)));
        assert!(hit(&mut world, source, None).is_none());
        world
            .entity_mut(entity)
            .remove::<crate::cell_streaming::StreamedTerrainHlod>();
        assert!(hit(&mut world, source, None).is_some());
        let mesh = world.get::<Mesh3d>(entity).unwrap().0.id();
        world.resource_mut::<Assets<Mesh>>().remove(mesh);
        assert!(hit(&mut world, source, None).is_none());
    }

    #[test]
    fn source_renderers_hidden_partition_is_queryable_only_with_its_matching_owner() {
        use crate::source_terrain::host::PhysicsProjection;
        let mut world = World::new();
        world.init_resource::<Assets<Mesh>>();
        let source = ContentHash::from_bytes([3; 32]);
        let entity = quad(&mut world, 2.0);
        world.entity_mut(entity).insert((
            hlod(source),
            InheritedVisibility::HIDDEN,
            PhysicsProjection(source),
        ));
        assert!(hit(&mut world, source, None).is_some());
        world
            .entity_mut(entity)
            .insert(PhysicsProjection(ContentHash::from_bytes([4; 32])));
        assert!(hit(&mut world, source, None).is_none());
        world.entity_mut(entity).insert(PhysicsProjection(source));
        world
            .entity_mut(entity)
            .insert(hlod(ContentHash::from_bytes([4; 32])));
        assert!(hit(&mut world, source, None).is_none());
        world
            .entity_mut(entity)
            .remove::<crate::cell_streaming::StreamedTerrainHlod>();
        assert!(hit(&mut world, source, None).is_none());
    }
}
