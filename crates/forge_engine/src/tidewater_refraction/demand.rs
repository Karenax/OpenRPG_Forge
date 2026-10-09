//! Conservative per-view demand; unknown bounds keep the capture active.
use bevy::{
    camera::primitives::{Aabb, Frustum},
    prelude::*,
};

/// A surface which samples the refraction capture. The adapter must include
/// shader displacement in this padding; it is not an authoring component.
#[derive(Component, Clone, Copy, Debug, PartialEq)]
pub struct CaptureConsumer {
    pub padding: Vec3,
}

pub(super) fn needed(world: &mut World, camera: Entity) -> bool {
    let Some(frustum) = world.get::<Frustum>(camera).copied() else {
        return true;
    };
    let mut known = false;
    for (consumer, bounds, pose, visibility, inherited) in world
        .query::<(
            &CaptureConsumer,
            Option<&Aabb>,
            Option<&GlobalTransform>,
            Option<&Visibility>,
            Option<&InheritedVisibility>,
        )>()
        .iter(world)
    {
        known = true;
        if visibility == Some(&Visibility::Hidden) || inherited.is_some_and(|v| !v.get()) {
            continue;
        }
        let (Some(bounds), Some(pose)) = (bounds, pose) else {
            return true;
        };
        if !consumer.padding.is_finite() || consumer.padding.min_element() < 0.0 {
            return true;
        }
        let padded = Aabb {
            center: bounds.center,
            half_extents: bounds.half_extents + bevy::math::Vec3A::from(consumer.padding),
        };
        // Ignore the far plane: large ocean patches can contain visible geometry
        // despite their conservative bounds crossing the scene's far distance.
        if frustum.intersects_obb(&padded, &pose.affine(), true, false) {
            return true;
        }
    }
    // Older and independent callers which do not declare consumers retain the
    // established always-on behavior rather than silently losing refraction.
    !known
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn surface_behind_the_view_suspends_and_reenters_with_current_pose() {
        let mut world = World::new();
        let projection = Mat4::perspective_rh(1.0, 1.0, 0.1, 100.0);
        let camera = world
            .spawn(Frustum(
                bevy::math::primitives::ViewFrustum::from_clip_from_world(&projection),
            ))
            .id();
        let surface = world
            .spawn((
                CaptureConsumer {
                    padding: Vec3::splat(0.1),
                },
                Aabb::from_min_max(Vec3::splat(-0.5), Vec3::splat(0.5)),
                GlobalTransform::from_translation(Vec3::new(0.0, 0.0, -5.0)),
                Visibility::Visible,
                // The production query runs after visibility propagation.
                InheritedVisibility::VISIBLE,
            ))
            .id();
        assert!(needed(&mut world, camera));
        world
            .entity_mut(surface)
            .insert(GlobalTransform::from_translation(Vec3::new(0.0, 0.0, 5.0)));
        assert!(!needed(&mut world, camera));
        world
            .entity_mut(surface)
            .insert(GlobalTransform::from_translation(Vec3::new(0.0, 0.0, -5.0)));
        assert!(needed(&mut world, camera));
    }

    #[test]
    fn missing_bounds_stay_active_and_hidden_consumers_suspend() {
        let mut world = World::new();
        let camera = world.spawn(Frustum::default()).id();
        assert!(needed(&mut world, camera));
        let surface = world
            .spawn((
                CaptureConsumer { padding: Vec3::ONE },
                Visibility::Visible,
                InheritedVisibility::VISIBLE,
            ))
            .id();
        assert!(needed(&mut world, camera));
        world.entity_mut(surface).insert(Visibility::Hidden);
        assert!(!needed(&mut world, camera));
        world.entity_mut(surface).insert(Visibility::Visible);
        assert!(needed(&mut world, camera));
        world
            .entity_mut(surface)
            .insert(InheritedVisibility::HIDDEN);
        assert!(!needed(&mut world, camera));
    }
}
