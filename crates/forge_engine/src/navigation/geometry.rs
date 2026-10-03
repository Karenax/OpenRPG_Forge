//! Extract exact installed Rapier surfaces. There is no independent terrain
//! triangulator, render-mesh substitution, bounding-box replacement or side world.
use super::NavigationPolicy;
use crate::{ForgePlayerCharacter, gameplay::DangerMotionActor};
use bevy::prelude::*;
use bevy_rapier3d::{
    prelude::{
        Collider, ColliderDisabled, RapierContextColliders, RapierRigidBodySet, RigidBody, Sensor,
    },
    rapier::{math::Pose, parry::shape::Shape},
};
use forge_navigation::{
    AgentProfile, AreaVolume, CurvedObstacleProjection, TileInput, TileKey, source_hash,
};
use std::hash::{Hash, Hasher};

pub(super) struct SourceReceipt {
    min: [f32; 3],
    max: [f32; 3],
    stamp: Option<u64>,
    error: Option<String>,
}

/// Inspect each prepared collider once per schedule phase, rather than once per tile.
pub(super) fn receipts(world: &World, context: Entity) -> Result<Vec<SourceReceipt>, String> {
    let colliders = world
        .get::<RapierContextColliders>(context)
        .ok_or("missing Rapier collider context")?;
    let mut sources = Vec::new();
    for entity in world.iter_entities() {
        if excluded(world, entity.id())
            || entity.contains::<Sensor>()
            || entity.contains::<ColliderDisabled>()
        {
            continue;
        }
        if let Some(shape) = entity.get::<Collider>()
            && !colliders.entity2collider().contains_key(&entity.id())
        {
            let global = current_global(world, entity.id())
                .ok_or("navigation source hierarchy not ready")?;
            let (min, max) = pending_bounds(shape, global)?;
            sources.push(SourceReceipt {
                min: min.to_array(),
                max: max.to_array(),
                stamp: None,
                error: Some("navigation waits for a newly authored collider in Rapier".into()),
            });
        }
    }
    let mut entries: Vec<_> = colliders.entity2collider().iter().collect();
    entries.sort_by_key(|(entity, _)| entity.to_bits());
    for (&entity, &handle) in entries {
        if excluded(world, entity) {
            continue;
        }
        let backend = colliders
            .colliders
            .get(handle)
            .ok_or("missing installed collider")?;
        let bounds = backend.compute_aabb();
        let mut min = bounds.mins;
        let mut max = bounds.maxs;
        if let Some(global) = current_global(world, entity) {
            let transform = global.compute_transform();
            let (pending_min, pending_max) = if let Some(shape) = world.get::<Collider>(entity)
                && (!std::ptr::eq(shape.raw.as_ref(), backend.shape())
                    || !shape.scale().abs_diff_eq(transform.scale, 1.0e-4))
            {
                pending_bounds(shape, global)?
            } else {
                let pending = backend
                    .shape()
                    .compute_aabb(&Pose::from_parts(transform.translation, transform.rotation));
                (pending.mins, pending.maxs)
            };
            min = min.min(pending_min);
            max = max.max(pending_max);
        }
        let state = dynamic(world, context, entity, backend);
        if matches!(state, Ok(true)) {
            continue;
        }
        let sync = state.and_then(|_| synchronized(world, entity, backend));
        if let Err(error) = sync {
            sources.push(SourceReceipt {
                min: min.to_array(),
                max: max.to_array(),
                stamp: None,
                error: Some(error),
            });
            continue;
        }
        if !backend.is_enabled() || backend.is_sensor() {
            continue;
        }
        let mut receipt = std::collections::hash_map::DefaultHasher::new();
        entity.to_bits().hash(&mut receipt);
        // Shape allocation is an invalidation witness only. All portable identity
        // comes from the exact extracted shape data in collect().
        std::ptr::from_ref(backend.shape())
            .cast::<()>()
            .hash(&mut receipt);
        let pose = bevy_rapier3d::utils::iso_to_transform(backend.position());
        for value in pose
            .translation
            .to_array()
            .into_iter()
            .chain(pose.rotation.to_array())
        {
            value.to_bits().hash(&mut receipt);
        }
        sources.push(SourceReceipt {
            min: min.to_array(),
            max: max.to_array(),
            stamp: Some(receipt.finish()),
            error: None,
        });
    }
    Ok(sources)
}

/// Fast process-local dirty receipt. Portable identity uses exact triangles in `collect()`.
pub(super) fn stamp(
    sources: &[SourceReceipt],
    key: TileKey,
    profile: AgentProfile,
    policy: &NavigationPolicy,
) -> Result<u64, String> {
    let mut receipt = std::collections::hash_map::DefaultHasher::new();
    for source in sources {
        if !intersects(source.min, source.max, key, profile) {
            continue;
        }
        if let Some(error) = &source.error {
            return Err(error.clone());
        }
        source.stamp.hash(&mut receipt);
    }
    policy.revision.hash(&mut receipt);
    policy.source_hash.hash(&mut receipt);
    Ok(receipt.finish())
}

pub(super) fn collect(
    world: &World,
    context: Entity,
    key: TileKey,
    profile: AgentProfile,
    revision: u64,
    token: u64,
) -> Result<TileInput, String> {
    let colliders = world
        .get::<RapierContextColliders>(context)
        .ok_or("missing Rapier collider context")?;
    let mut triangles = Vec::<[[f32; 3]; 3]>::new();
    let mut obstacles = Vec::<(AreaVolume, CurvedObstacleProjection)>::new();
    for (&entity, &handle) in colliders.entity2collider() {
        if excluded(world, entity) {
            continue;
        }
        let backend = colliders
            .colliders
            .get(handle)
            .ok_or("missing installed collider")?;
        let bounds = backend.compute_aabb();
        if !intersects(bounds.mins.to_array(), bounds.maxs.to_array(), key, profile) {
            continue;
        }
        if dynamic(world, context, entity, backend)? {
            continue;
        }
        synchronized(world, entity, backend)?;
        if !backend.is_enabled() || backend.is_sensor() {
            continue;
        }
        append_shape(
            backend.shape(),
            backend.position(),
            key,
            profile,
            &mut triangles,
            &mut obstacles,
            0,
        )?;
        if triangles.len() > 200_000 || obstacles.len() > 256 {
            return Err("navigation tile geometry budget exceeded".into());
        }
    }
    // Deterministic geometry identity does not depend on hash-map iteration or Entity IDs.
    triangles.sort_by(|a, b| {
        a.iter()
            .flatten()
            .zip(b.iter().flatten())
            .map(|(a, b)| a.total_cmp(b))
            .find(|order| !order.is_eq())
            .unwrap_or(std::cmp::Ordering::Equal)
    });
    // Portable obstacle ordering excludes ECS identities and map iteration.
    let mut ordered = obstacles
        .into_iter()
        .map(|pair| {
            serde_json::to_vec(&pair)
                .map(|bytes| (bytes, pair))
                .map_err(|error| error.to_string())
        })
        .collect::<Result<Vec<_>, _>>()?;
    ordered.sort_by(|a, b| a.0.cmp(&b.0));
    let policy = world.resource::<NavigationPolicy>();
    let mut input = TileInput {
        key,
        profile,
        revision,
        source_hash: String::new(),
        token,
        vertices: Vec::with_capacity(triangles.len() * 3),
        triangles: Vec::with_capacity(triangles.len()),
        areas: policy.areas.clone(),
        policy_source_hash: (!policy.source_hash.is_empty()).then(|| policy.source_hash.clone()),
        curved_obstacles: Vec::with_capacity(ordered.len()),
    };
    for (_, (area, receipt)) in ordered {
        input.areas.push(area);
        input.curved_obstacles.push(receipt);
    }
    if input.areas.len() > 256 {
        return Err("navigation tile area budget exceeded".into());
    }
    for triangle in triangles {
        let start =
            u32::try_from(input.vertices.len()).map_err(|_| "navigation vertex budget exceeded")?;
        input.vertices.extend(triangle);
        input.triangles.push([start, start + 1, start + 2]);
    }
    input.source_hash = source_hash(&input)?;
    Ok(input)
}

/// A pending edit may replace a shape or enlarge it before `SyncBackend`. This
/// envelope is an invalidation witness only. Tile triangles still come solely
/// from the synchronized installed Rapier shape in `collect()`/`append_shape()`.
fn pending_bounds(shape: &Collider, global: GlobalTransform) -> Result<(Vec3, Vec3), String> {
    let unscaled = shape.as_unscaled_typed_shape().to_shared_shape();
    let local = unscaled.compute_local_aabb();
    let mut min = Vec3::splat(f32::INFINITY);
    let mut max = Vec3::splat(f32::NEG_INFINITY);
    for x in [local.mins.x, local.maxs.x] {
        for y in [local.mins.y, local.maxs.y] {
            for z in [local.mins.z, local.maxs.z] {
                let point = global.affine().transform_point3(Vec3::new(x, y, z));
                min = min.min(point);
                max = max.max(point);
            }
        }
    }
    if !min.is_finite()
        || !max.is_finite()
        || min.abs().max_element() > 100_000.0
        || max.abs().max_element() > 100_000.0
    {
        return Err("invalid pending navigation source envelope".into());
    }
    Ok((min, max))
}

fn excluded(world: &World, entity: Entity) -> bool {
    world.get::<DangerMotionActor>(entity).is_some()
        || world.get::<ForgePlayerCharacter>(entity).is_some()
}

// Moving dynamic bodies are avoidance/KCC obstacles, not per-tick raster inputs.
// The transition between fixed and dynamic still changes local tile membership.
fn dynamic(
    world: &World,
    context: Entity,
    entity: Entity,
    backend: &bevy_rapier3d::rapier::geometry::Collider,
) -> Result<bool, String> {
    let bodies = world
        .get::<RapierRigidBodySet>(context)
        .ok_or("missing Rapier bodies")?;
    let parent = backend.parent();
    let body_entity = parent
        .and_then(|handle| bodies.rigid_body_entity(handle))
        .unwrap_or(entity);
    let expected = world.get::<RigidBody>(body_entity) == Some(&RigidBody::Dynamic);
    let actual = parent
        .and_then(|handle| bodies.bodies.get(handle))
        .is_some_and(bevy_rapier3d::rapier::dynamics::RigidBody::is_dynamic);
    if expected != actual {
        return Err("navigation waits for body-kind synchronization".into());
    }
    Ok(actual)
}

fn synchronized(
    world: &World,
    entity: Entity,
    backend: &bevy_rapier3d::rapier::geometry::Collider,
) -> Result<(), String> {
    let shape = world
        .get::<Collider>(entity)
        .ok_or("navigation collider missing from ECS")?;
    let current = current_global(world, entity).ok_or("navigation source hierarchy not ready")?;
    let transform = current.compute_transform();
    if !current
        .affine()
        .abs_diff_eq(GlobalTransform::from(transform).affine(), 1.0e-4)
    {
        return Err("navigation collider has unsupported shear".into());
    }
    let pose = bevy_rapier3d::utils::iso_to_transform(backend.position());
    if !pose.translation.abs_diff_eq(transform.translation, 1.0e-4)
        || !pose.rotation.abs_diff_eq(transform.rotation, 1.0e-5)
        || !shape.scale().abs_diff_eq(transform.scale, 1.0e-4)
        || !std::ptr::eq(shape.raw.as_ref(), backend.shape())
        || backend.is_enabled() == world.get::<ColliderDisabled>(entity).is_some()
        || backend.is_sensor() != world.get::<Sensor>(entity).is_some()
    {
        return Err("navigation waits for the current collider revision in Rapier".into());
    }
    Ok(())
}

fn current_global(world: &World, mut entity: Entity) -> Option<GlobalTransform> {
    let mut transforms = Vec::new();
    let mut visited = std::collections::HashSet::new();
    for _ in 0..64 {
        if !visited.insert(entity) {
            return None;
        }
        transforms.push(*world.get::<Transform>(entity)?);
        if let Some(parent) = world.get::<ChildOf>(entity) {
            entity = parent.parent();
        } else {
            let mut global = GlobalTransform::IDENTITY;
            for transform in transforms.iter().rev() {
                global = global.mul_transform(*transform);
            }
            return Some(global);
        }
    }
    None
}

fn intersects(min: [f32; 3], max: [f32; 3], key: TileKey, profile: AgentProfile) -> bool {
    let (tile_min, tile_max) = key.bounds(profile);
    let border = profile.border_m();
    min[0] <= tile_max[0] + border
        && max[0] >= tile_min[0] - border
        && min[2] <= tile_max[1] + border
        && max[2] >= tile_min[1] - border
}

#[allow(
    clippy::too_many_lines,
    reason = "all exact installed shape cases share one triangle filter and recursion budget"
)]
fn append_shape(
    shape: &dyn Shape,
    pose: &Pose,
    key: TileKey,
    profile: AgentProfile,
    result: &mut Vec<[[f32; 3]; 3]>,
    obstacles: &mut Vec<(AreaVolume, CurvedObstacleProjection)>,
    depth: usize,
) -> Result<(), String> {
    if depth > 16 {
        return Err("navigation compound depth exceeded".into());
    }
    let transform = bevy_rapier3d::utils::iso_to_transform(pose);
    let mut append = |triangle: [[f32; 3]; 3]| {
        let points = triangle.map(|point| {
            transform
                .transform_point(Vec3::from_array(point))
                .to_array()
        });
        let min = points
            .into_iter()
            .fold(Vec3::splat(f32::INFINITY), |acc, p| {
                acc.min(Vec3::from_array(p))
            });
        let max = points
            .into_iter()
            .fold(Vec3::splat(f32::NEG_INFINITY), |acc, p| {
                acc.max(Vec3::from_array(p))
            });
        if intersects(min.to_array(), max.to_array(), key, profile) {
            result.push(points);
        }
    };
    if let Some(field) = shape.as_heightfield() {
        // Select cells using the installed shape's own scale, diagonals and cell flags.
        let (min, max) = key.bounds(profile);
        let pad = profile.border_m();
        let bounds = shape.compute_aabb(pose);
        let inverse = transform.compute_affine().inverse();
        let mut lo = (usize::MAX, usize::MAX);
        let mut hi = (0, 0);
        for x in [min[0] - pad, max[0] + pad] {
            for z in [min[1] - pad, max[1] + pad] {
                for y in [bounds.mins.y, bounds.maxs.y] {
                    let local = inverse.transform_point3(Vec3::new(x, y, z));
                    let (row, col) = field.closest_cell_at_point(local);
                    lo = (lo.0.min(row), lo.1.min(col));
                    hi = (hi.0.max(row), hi.1.max(col));
                }
            }
        }
        for row in lo.0..=hi.0 {
            for col in lo.1..=hi.1 {
                let (a, b) = field.triangles_at(row, col);
                for triangle in [a, b].into_iter().flatten() {
                    append([
                        triangle.a.to_array(),
                        triangle.b.to_array(),
                        triangle.c.to_array(),
                    ]);
                }
            }
        }
    } else if let Some(mesh) = shape.as_trimesh() {
        for triangle in mesh.triangles() {
            append([
                triangle.a.to_array(),
                triangle.b.to_array(),
                triangle.c.to_array(),
            ]);
        }
    } else if let Some(cuboid) = shape.as_cuboid() {
        let (vertices, indices) = cuboid.to_trimesh();
        for [a, b, c] in indices {
            append([
                vertices[a as usize].to_array(),
                vertices[b as usize].to_array(),
                vertices[c as usize].to_array(),
            ]);
        }
    } else if let Some(polyhedron) = shape.as_convex_polyhedron() {
        let (vertices, indices) = polyhedron.to_trimesh();
        for [a, b, c] in indices {
            append([
                vertices[a as usize].to_array(),
                vertices[b as usize].to_array(),
                vertices[c as usize].to_array(),
            ]);
        }
    } else if let Some(compound) = shape.as_compound() {
        for (local, child) in compound.shapes() {
            append_shape(
                child.as_ref(),
                &(*pose * *local),
                key,
                profile,
                result,
                obstacles,
                depth + 1,
            )?;
        }
    } else if let Some(triangle) = shape.as_triangle() {
        append([
            triangle.a.to_array(),
            triangle.b.to_array(),
            triangle.c.to_array(),
        ]);
    } else if shape.as_cylinder().is_some()
        || shape.as_capsule().is_some()
        || shape.as_ball().is_some()
    {
        obstacles.push(super::curved_obstacles::project(shape, pose, profile)?);
    } else {
        return Err(format!(
            "unsupported exact navigation collider shape {:?}",
            shape.shape_type()
        ));
    }
    Ok(())
}

// Proposal only: append as a cfg(test) unit module to navigation/geometry.rs
// after PHYS explicitly releases its frozen source window.
#[cfg(test)]
mod tests {
    use super::*;
    use bevy_rapier3d::rapier::parry::{
        shape::{HeightField, HeightFieldCellStatus},
        utils::Array2,
    };

    #[test]
    fn prepared_heightfield_scale_diagonal_and_removed_cell_are_consumed_exactly() {
        let mut field = HeightField::new(
            Array2::new(3, 3, vec![0.0, 0.1, 0.3, 0.2, 0.8, 0.4, 0.7, 0.5, 0.9]),
            Vec3::new(8.0, 2.0, 6.0),
        );
        field.set_cell_status(0, 0, HeightFieldCellStatus::ZIGZAG_SUBDIVISION);
        field.set_cell_status(1, 0, HeightFieldCellStatus::LEFT_TRIANGLE_REMOVED);
        field.set_cell_status(1, 1, HeightFieldCellStatus::CELL_REMOVED);
        let pose = Pose::from_parts(Vec3::new(15.0, 1.5, 15.0), Quat::from_rotation_y(0.3));
        let mut actual = Vec::new();
        let mut curves = Vec::new();
        append_shape(
            &field,
            &pose,
            TileKey { x: 0, z: 0 },
            AgentProfile::default(),
            &mut actual,
            &mut curves,
            0,
        )
        .unwrap();
        let transform = bevy_rapier3d::utils::iso_to_transform(&pose);
        let expected: Vec<_> = (0..field.nrows())
            .flat_map(|row| (0..field.ncols()).map(move |col| (row, col)))
            .flat_map(|(row, col)| {
                let (left, right) = field.triangles_at(row, col);
                [left, right].into_iter().flatten()
            })
            .map(|triangle| {
                [triangle.a, triangle.b, triangle.c]
                    .map(|point| transform.transform_point(point).to_array())
            })
            .collect();
        assert_eq!(
            actual, expected,
            "extractor must preserve the installed split and triangle removals"
        );
        assert_eq!(actual.len(), 5);
        assert!(curves.is_empty());
        let ordinary = HeightField::new(field.heights().clone(), field.scale());
        let (zigzag, _) = field.triangles_at(0, 0);
        let (default, _) = ordinary.triangles_at(0, 0);
        assert_ne!(
            zigzag.unwrap(),
            default.unwrap(),
            "fixture must distinguish the alternate split"
        );
    }
}
