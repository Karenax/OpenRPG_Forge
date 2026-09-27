//! Exact accepted placements -> derived roots and synchronized static physics.
use super::{ManualPlacementCollisionExpected, ManualPlacementCollisionStatus, manual_readiness};
use bevy::prelude::*;
use forge_project::zone_authoring::placements::ManualPlacement;

#[cfg(test)]
mod tests;

/// Attached only after the placement's pinned asset was verified by the adapter.
#[derive(Component)]
pub struct ManualPlacementProjection(pub ManualPlacement);

/// Fresh check, not a cached visual-ready flag. No authoring state is mutated.
pub fn manual_placements_ready(world: &mut World, accepted: &[ManualPlacement]) -> bool {
    let mut query = world.query::<(Entity, &ManualPlacementProjection, Option<&Transform>)>();
    if query.iter(world).count() != accepted.len() {
        return false;
    }
    let mut seen = std::collections::BTreeSet::new();
    query.iter(world).all(|(root, projected, transform)| {
        let Ok(index) = accepted.binary_search_by_key(&projected.0.id, |p| p.id) else {
            return false;
        };
        if projected.0 != accepted[index]
            || !seen.insert(projected.0.id)
            || transform != Some(&placement_transform(&projected.0))
            || world.get::<ChildOf>(root).is_some()
        {
            return false;
        }
        world
            .get::<ManualPlacementCollisionExpected>(root)
            .is_some_and(|expected| {
                expected.placement == projected.0.id
                    && expected
                        .primitives
                        .iter()
                        .all(|&entity| belongs_to(world, entity, root))
                    && matches!(
                        manual_readiness::inspect(world, expected),
                        ManualPlacementCollisionStatus::Registered { .. }
                    )
            })
    })
}

fn belongs_to(world: &World, mut entity: Entity, root: Entity) -> bool {
    for _ in 0..128 {
        if entity == root {
            return true;
        }
        let Some(parent) = world.get::<ChildOf>(entity) else {
            return false;
        };
        entity = parent.parent();
    }
    false
}

#[must_use]
pub fn placement_transform(placement: &ManualPlacement) -> Transform {
    let t = placement.transform;
    Transform {
        translation: Vec3::new(t.translation_m.x, t.translation_m.y, t.translation_m.z),
        rotation: Quat::from_xyzw(t.rotation.x, t.rotation.y, t.rotation.z, t.rotation.w),
        scale: Vec3::new(t.scale.x, t.scale.y, t.scale.z),
    }
}

/// Reject stale propagated transforms (including a still-restoring gizmo).
pub(super) fn current_global(world: &World, mut entity: Entity) -> Option<GlobalTransform> {
    let mut result = GlobalTransform::IDENTITY;
    for _ in 0..128 {
        let local = world.get::<Transform>(entity)?;
        result = GlobalTransform::from(*local) * result;
        let Some(parent) = world.get::<ChildOf>(entity) else {
            return Some(result);
        };
        entity = parent.parent();
    }
    None
}
