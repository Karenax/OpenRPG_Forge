//! Shared broad-phase culling for a stable group of source fish.
use crate::distance_lod::Policy;
use bevy::prelude::*;
use std::collections::HashMap;

/// Mark each root so propagated transform changes invalidate cached bounds.
#[derive(Component)]
pub struct Member;

#[derive(Clone, Copy)]
struct Bounds {
    center: Vec3,
    radius: f32,
}

/// Bounds and sleep state belong to the caller's resident group.
#[derive(Default)]
pub struct Group {
    bounds: Option<Bounds>,
    members: HashMap<Entity, GlobalTransform>,
    dormant: bool,
}

impl Group {
    /// Recompute when a marked root moves. Membership changes must invalidate
    /// the group explicitly; callers should not call this for an empty group.
    ///
    /// # Errors
    /// Refuses missing or invalid transforms, empty groups and invalid cameras.
    pub fn may_contribute(
        &mut self,
        world: &World,
        roots: impl IntoIterator<Item = Entity>,
        policy: Policy,
        camera: Vec3,
        intersects: impl FnOnce(Vec3, f32) -> bool,
    ) -> Result<bool, String> {
        if !camera.is_finite() {
            return Err("Invalid fish group camera".into());
        }
        let changed = self.bounds.is_none()
            || self.members.iter().any(|(entity, previous)| {
                world.get::<Member>(*entity).is_none()
                    || world.get::<GlobalTransform>(*entity) != Some(previous)
            });
        if changed {
            let mut min = Vec3::splat(f32::INFINITY);
            let mut max = Vec3::splat(f32::NEG_INFINITY);
            let mut members = HashMap::new();
            for root in roots {
                let transform = world
                    .get::<GlobalTransform>(root)
                    .ok_or("Fish group root transform absent")?;
                if world.get::<Member>(root).is_none() || members.insert(root, *transform).is_some()
                {
                    return Err("Missing or repeated fish group member".into());
                }
                let (scale, _, center) = transform.to_scale_rotation_translation();
                let length = scale.abs().max_element();
                if !center.is_finite() || !length.is_finite() || length <= 0.0 {
                    return Err("Invalid fish group transform".into());
                }
                let radius = length * 0.8;
                min = min.min(center - Vec3::splat(radius));
                max = max.max(center + Vec3::splat(radius));
            }
            if members.is_empty() {
                return Err("Empty fish group".into());
            }
            let center = (min + max) * 0.5;
            let radius = center.distance(max);
            if !center.is_finite() || !radius.is_finite() {
                return Err("Invalid fish group bounds".into());
            }
            self.bounds = Some(Bounds { center, radius });
            self.members = members;
        }
        let bounds = self.bounds.ok_or("Fish group bounds absent")?;
        Ok(policy.group_may_contribute(
            camera.distance(bounds.center),
            bounds.radius,
            intersects(bounds.center, bounds.radius),
        ))
    }

    /// Force a bounds refresh after adding or removing a group member.
    pub fn invalidate(&mut self) {
        self.bounds = None;
    }

    #[must_use]
    pub const fn is_dormant(&self) -> bool {
        self.dormant
    }

    /// Change this only after the caller has published its visibility and shadows.
    pub fn set_dormant(&mut self, dormant: bool) {
        self.dormant = dormant;
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::cell::Cell;

    fn policy() -> Policy {
        Policy::new(90.0, 81.0, 22.0, 28.0, 0.88).unwrap()
    }

    #[test]
    fn far_groups_sleep_but_near_offscreen_shadows_and_waking_remain() {
        let mut world = World::new();
        let roots = [
            world.spawn((Member, GlobalTransform::IDENTITY)).id(),
            world
                .spawn((Member, GlobalTransform::from_translation(Vec3::X)))
                .id(),
        ];
        let mut group = Group::default();
        assert!(
            !group
                .may_contribute(&world, roots, policy(), Vec3::X * 1_000.0, |_, _| true)
                .unwrap()
        );
        group.set_dormant(true);
        assert!(group.is_dormant());
        assert!(
            group
                .may_contribute(&world, roots, policy(), Vec3::X * 10.0, |_, _| false)
                .unwrap()
        );
        assert!(
            !group
                .may_contribute(&world, roots, policy(), Vec3::X * 60.0, |_, _| false)
                .unwrap()
        );
        assert!(
            group
                .may_contribute(&world, roots, policy(), Vec3::X * 60.0, |_, _| true)
                .unwrap()
        );
        group.set_dormant(false);
        assert!(!group.is_dormant());
    }

    #[test]
    fn propagated_root_changes_refresh_cached_group_bounds() {
        let mut world = World::new();
        let first = world.spawn((Member, GlobalTransform::IDENTITY)).id();
        let second = world
            .spawn((Member, GlobalTransform::from_translation(Vec3::X * 4.0)))
            .id();
        let mut group = Group::default();
        let center = Cell::new(Vec3::ZERO);
        group
            .may_contribute(&world, [first, second], policy(), Vec3::ZERO, |c, _| {
                center.set(c);
                true
            })
            .unwrap();
        assert!((center.get().x - 2.0).abs() < 1e-5);
        let other = world.spawn((Member, GlobalTransform::IDENTITY)).id();
        world.clear_trackers();
        world
            .entity_mut(other)
            .insert(GlobalTransform::from_translation(Vec3::Y));
        let unrelated_roots = std::iter::once_with(|| -> Entity {
            panic!("An unrelated group must not invalidate these bounds")
        });
        group
            .may_contribute(&world, unrelated_roots, policy(), Vec3::ZERO, |_, _| true)
            .unwrap();
        world
            .entity_mut(second)
            .insert(GlobalTransform::from_translation(Vec3::X * 100.0));
        group
            .may_contribute(&world, [first, second], policy(), Vec3::ZERO, |c, _| {
                center.set(c);
                true
            })
            .unwrap();
        assert!((center.get().x - 50.0).abs() < 1e-5);
        group.invalidate();
        world.despawn(second);
        assert!(
            group
                .may_contribute(&world, [first, second], policy(), Vec3::ZERO, |_, _| {
                    true
                })
                .is_err()
        );
    }
}
