//! One-shot qualification observation of actual backend queries, not contact/shape acceptance.
use super::{
    GovernedAssetKitCollisionPrimitive, GovernedAssetKitPlacementMarker, GovernedAssetKitReadiness,
    GovernedAssetKitRuntimeStats, MAX_GOVERNED_ASSET_HIERARCHY_DEPTH,
};
use bevy::{math::DVec3, mesh::VertexAttributeValues, prelude::*};
use bevy_rapier3d::prelude::{QueryFilter, RapierColliderHandle, ReadRapierContext};
use tracing::info;

type RegisteredPrimitives<'w, 's> = Query<
    'w,
    's,
    (Entity, &'static Mesh3d, &'static GlobalTransform),
    (
        With<GovernedAssetKitCollisionPrimitive>,
        With<RapierColliderHandle>,
    ),
>;

fn probe_points(mesh: &Mesh, transform: &GlobalTransform) -> Option<(Vec3, Vec3, Vec3)> {
    let VertexAttributeValues::Float32x3(vertices) = mesh.attribute(Mesh::ATTRIBUTE_POSITION)?
    else {
        return None;
    };
    let count = u32::try_from(vertices.len()).ok()?;
    if count < 4 {
        return None;
    }
    let mut sum = DVec3::ZERO;
    let mut minimum = f64::INFINITY;
    let mut maximum = f64::NEG_INFINITY;
    for point in vertices {
        let point = Vec3::from_array(*point);
        if !point.is_finite() {
            return None;
        }
        sum += point.as_dvec3();
        minimum = minimum.min(f64::from(point.y));
        maximum = maximum.max(f64::from(point.y));
    }
    // A convex combination of all hull-source points lies inside the convex hull.
    let center = sum / f64::from(count);
    let outside = DVec3::new(center.x, maximum + (maximum - minimum) + 1.0, center.z);
    let inside = transform.transform_point(center.as_vec3());
    let outside = transform.transform_point(outside.as_vec3());
    let direction = transform
        .affine()
        .transform_vector3(Vec3::Y)
        .try_normalize()?;
    (inside.is_finite() && outside.is_finite()).then_some((inside, outside, direction))
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::too_many_arguments,
    reason = "Read-only Bevy qualification system"
)]
pub(super) fn observe(
    readiness: Res<GovernedAssetKitReadiness>,
    stats: Res<GovernedAssetKitRuntimeStats>,
    meshes: Res<Assets<Mesh>>,
    colliders: RegisteredPrimitives,
    parents: Query<&ChildOf>,
    placements: Query<&GovernedAssetKitPlacementMarker>,
    rapier: ReadRapierContext,
    mut stable_frames: Local<u8>,
    mut finished: Local<bool>,
) {
    if *finished {
        return;
    }
    if !readiness.ready {
        *stable_frames = 0;
        return;
    }
    *stable_frames = stable_frames.saturating_add(1);
    if *stable_frames < 8 {
        return;
    }
    let Ok(context) = rapier.single() else {
        return;
    };
    let mut checked = 0_usize;
    let mut passed = 0_usize;
    for (entity, mesh, transform) in &colliders {
        checked += 1;
        let mut ancestor = entity;
        let mut placement = None;
        for _ in 0..MAX_GOVERNED_ASSET_HIERARCHY_DEPTH {
            if let Ok(marker) = placements.get(ancestor) {
                placement = Some(marker);
                break;
            }
            let Ok(parent) = parents.get(ancestor) else {
                break;
            };
            ancestor = parent.parent();
        }
        let points = meshes
            .get(mesh)
            .and_then(|mesh| probe_points(mesh, transform));
        let mut inside_hit = false;
        let mut outside_clear = false;
        if let Some((inside, outside, direction)) = points {
            let only_this = |candidate| candidate == entity;
            inside_hit = context
                .cast_ray(
                    inside,
                    direction,
                    0.01,
                    true,
                    QueryFilter::default().predicate(&only_this),
                )
                .is_some_and(|(hit, distance)| hit == entity && distance.abs() <= 1.0e-5);
            outside_clear = context
                .cast_ray(
                    outside,
                    direction,
                    10.0,
                    true,
                    QueryFilter::default().predicate(&only_this),
                )
                .is_none();
        }
        let matched = placement.is_some() && inside_hit && outside_clear;
        passed += usize::from(matched);
        info!(target: "forge::assets", event_kind = "governed_asset_kit_physics_query",
            ?entity, placement_id = placement.map(|p| p.placement_id.as_str()),
            asset_id = placement.map(|p| p.asset_id.as_str()),
            inside_hit, outside_clear, matched, ?points,
            "source mesh points compared with the registered Rapier collider");
    }
    *finished = true;
    info!(target: "forge::assets", schema = "forge.runtime-event", schema_version = 1_u64,
        event_kind = "governed_asset_kit_physics_query_summary",
        kit_id = %stats.asset_kit_id, terrain_hash = %stats.source_terrain_sha256,
        world_sketch_hash = %stats.source_world_sketch_sha256,
        expected = readiness.registered_colliders, checked, passed,
        complete = checked > 0 && checked == readiness.registered_colliders && passed == checked,
        "one-shot backend query proof; not dynamic contact or artistic acceptance");
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn samples_follow_source_hierarchy_transform_without_mutating_mesh() {
        let mesh = super::super::tests::collision_test_mesh(vec![
            [0.0, 0.0, 0.0],
            [2.0, 0.0, 0.0],
            [0.0, 3.0, 0.0],
            [0.0, 0.0, 4.0],
        ]);
        let transform = GlobalTransform::from(
            Transform::from_xyz(12.0, -5.0, 7.0)
                .with_rotation(Quat::from_rotation_z(0.6))
                .with_scale(Vec3::new(2.0, 3.0, 0.5)),
        );
        let (inside, outside, direction) = probe_points(&mesh, &transform).unwrap();
        assert_eq!(inside, transform.transform_point(Vec3::new(0.5, 0.75, 1.0)));
        assert_eq!(outside, transform.transform_point(Vec3::new(0.5, 7.0, 1.0)));
        assert!((direction.length() - 1.0).abs() < 1.0e-6);
        assert_eq!(mesh.count_vertices(), 4);
    }
}
