//! Owned physical limits of a finite streamed map; no floor or navigation mesh.
use super::super::StreamedTerrainBoundary;
use bevy::prelude::*;
use bevy_rapier3d::prelude::{
    Collider, ColliderDisabled, CollisionGroups, RigidBody, Sensor, SolverGroups,
};
use forge_world::cell_streaming::CellStreamingPolicy;

struct Surface {
    entity: Entity,
    collider: Collider,
    transform: Transform,
}

#[derive(Resource)]
struct Boundaries {
    policy: CellStreamingPolicy,
    surfaces: [Surface; 4],
}

pub(super) fn install(world: &mut World, policy: &CellStreamingPolicy) {
    assert!(!world.contains_resource::<Boundaries>());
    let (minimum, maximum) = planes(policy);
    let surfaces = [
        (Vec3::X, Vec3::new(minimum[0], 0.0, 0.0)),
        (Vec3::NEG_X, Vec3::new(maximum[0], 0.0, 0.0)),
        (Vec3::Z, Vec3::new(0.0, 0.0, minimum[1])),
        (Vec3::NEG_Z, Vec3::new(0.0, 0.0, maximum[1])),
    ]
    .map(|(normal, position)| {
        let collider = Collider::halfspace(normal).expect("Unit cardinal plane normal");
        let transform = Transform::from_translation(position);
        let entity = world
            .spawn((
                Name::new("Finite streamed map boundary"),
                StreamedTerrainBoundary,
                collider.clone(),
                transform,
            ))
            .id();
        Surface {
            entity,
            collider,
            transform,
        }
    });
    world.insert_resource(Boundaries {
        policy: policy.clone(),
        surfaces,
    });
}

/// Grid coordinates are already finite and bounded by policy validation.
/// Round inwards so f32 physics never permits a centre beyond the f64 grid.
#[allow(clippy::cast_possible_truncation)]
fn planes(policy: &CellStreamingPolicy) -> ([f32; 2], [f32; 2]) {
    let minimum = policy.origin_xz_m.map(|value| {
        let rounded = value as f32;
        if f64::from(rounded) < value {
            rounded.next_up()
        } else {
            rounded
        }
    });
    let maximum = std::array::from_fn(|axis| {
        let value =
            policy.origin_xz_m[axis] + f64::from(policy.dimensions[axis]) * policy.cell_size_m;
        let rounded = value as f32;
        if f64::from(rounded) > value {
            rounded.next_down()
        } else {
            rounded
        }
    });
    (minimum, maximum)
}

pub(super) fn ready(world: &World, policy: &CellStreamingPolicy) -> Result<bool, String> {
    let boundaries = world
        .get_resource::<Boundaries>()
        .ok_or("Finite streamed map boundaries are absent")?;
    if boundaries.policy != *policy {
        return Err("Finite streamed map boundary grid changed".into());
    }
    let mut ready = true;
    for surface in &boundaries.surfaces {
        let entity = surface.entity;
        if world.get::<StreamedTerrainBoundary>(entity).is_none()
            || world.get::<Transform>(entity) != Some(&surface.transform)
            || world.get::<ChildOf>(entity).is_some()
            || world.get::<RigidBody>(entity).is_some()
            || world.get::<CollisionGroups>(entity).is_some()
            || world.get::<SolverGroups>(entity).is_some()
            || world.get::<ColliderDisabled>(entity).is_some()
            || world.get::<Sensor>(entity).is_some()
            || world.get::<Collider>(entity).is_none_or(|collider| {
                !std::ptr::eq(collider.raw.as_ref(), surface.collider.raw.as_ref())
            })
        {
            return Err("Finite streamed map boundary was removed or changed".into());
        }
        ready &= crate::physics::static_collider_ready(world, entity);
    }
    Ok(ready)
}

/// The caller verified physical boundaries and both current/predicted centres.
/// Protect only the in-map intersection of the conservative body envelope.
pub(super) fn clip(
    policy: &CellStreamingPolicy,
    minimum: Vec3,
    maximum: Vec3,
) -> ([f64; 2], [f64; 2]) {
    let low = [f64::from(minimum.x), f64::from(minimum.z)];
    let high = [f64::from(maximum.x), f64::from(maximum.z)];
    (
        std::array::from_fn(|axis| low[axis].max(policy.origin_xz_m[axis])),
        std::array::from_fn(|axis| {
            // Only the cell index is consumed. A point in the final cell is a
            // stable sentinel: subtracting the origin from end.next_down() can
            // round back to the excluded endpoint on centred grids.
            let last_cell = policy.origin_xz_m[axis]
                + (f64::from(policy.dimensions[axis]) - 0.5) * policy.cell_size_m;
            high[axis].min(last_cell)
        }),
    )
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn physical_planes_round_inside_nonrepresentable_grid_coordinates() {
        let source = crate::cell_streaming::tests::terrain_fixture();
        let mut policy = crate::cell_streaming::sampled_policy(source.raster().source()).unwrap();
        policy.origin_xz_m = [0.1, -0.1];
        let (low, high) = planes(&policy);
        for axis in 0..2 {
            let end =
                policy.origin_xz_m[axis] + f64::from(policy.dimensions[axis]) * policy.cell_size_m;
            assert!(f64::from(low[axis]) >= policy.origin_xz_m[axis]);
            assert!(f64::from(high[axis]) <= end);
            assert!(low[axis] < high[axis]);
        }
    }
}
