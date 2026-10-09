//! Live backend registration check; this is not a source/transform playtest gate.
use super::{ManualPlacementCollision, ManualPlacementCollisionFailed};
use bevy::prelude::*;
use bevy_rapier3d::{
    plugin::context::{DefaultRapierContext, RapierContextColliders},
    prelude::{Collider, RapierColliderHandle, RapierContextEntityLink},
};
use forge_core::EntityId;

/// Exact primitives discovered in a loaded, verified placement instance.
#[derive(Component)]
pub struct ManualPlacementCollisionExpected {
    pub placement: EntityId,
    pub primitives: Vec<Entity>,
}

/// Derived only from a verified prototype that explicitly has no solid primitives.
/// This is renderer state, not a user-authored waiver for missing collisions.
#[derive(Component, Clone, Copy, Debug)]
pub struct ManualPlacementCollisionNotRequired(pub EntityId);

#[derive(Component, Clone, Copy, Debug, Eq, PartialEq)]
pub enum ManualPlacementCollisionStatus {
    MissingPrimitives,
    Waiting { registered: usize, expected: usize },
    Failed(&'static str),
    Registered { primitives: usize },
}

pub(super) fn observe(world: &mut World) {
    let updates: Vec<_> = world
        .query::<(Entity, &ManualPlacementCollisionExpected)>()
        .iter(world)
        .map(|(root, expected)| (root, inspect(world, root, expected)))
        .collect();
    for (root, status) in updates {
        if world.get::<ManualPlacementCollisionStatus>(root) != Some(&status) {
            world.entity_mut(root).insert(status);
        }
    }
}

pub(super) fn inspect(
    world: &World,
    root: Entity,
    expected: &ManualPlacementCollisionExpected,
) -> ManualPlacementCollisionStatus {
    use ManualPlacementCollisionStatus as Status;
    if let Some(policy) = world.get::<ManualPlacementCollisionNotRequired>(root) {
        return if policy.0 == expected.placement && expected.primitives.is_empty() {
            Status::Registered { primitives: 0 }
        } else {
            Status::Failed("noncolliding_prototype_contract_mismatch")
        };
    }
    if expected.primitives.is_empty() {
        return Status::MissingPrimitives;
    }
    let mut registered = 0;
    for (index, &entity) in expected.primitives.iter().enumerate() {
        if expected.primitives[..index].contains(&entity) {
            return Status::Failed("duplicate_collision_primitive");
        }
        if world
            .get::<ManualPlacementCollision>(entity)
            .is_none_or(|marker| marker.0 != expected.placement)
        {
            return Status::Failed("missing_or_replaced_collision_primitive");
        }
        if let Some(failure) = world.get::<ManualPlacementCollisionFailed>(entity) {
            return Status::Failed(failure.0);
        }
        if static_collider_ready(world, entity) {
            registered += 1;
        }
    }
    if registered == expected.primitives.len() {
        Status::Registered {
            primitives: registered,
        }
    } else {
        Status::Waiting {
            registered,
            expected: expected.primitives.len(),
        }
    }
}

pub(crate) fn static_collider_ready(world: &World, entity: Entity) -> bool {
    collider_ready(world, entity, false)
}

pub(crate) fn static_sensor_ready(world: &World, entity: Entity) -> bool {
    collider_ready(world, entity, true)
}

fn collider_ready(world: &World, entity: Entity, sensor: bool) -> bool {
    let binding = world.get::<Collider>(entity).and_then(|_| {
        Some((
            world.get::<RapierColliderHandle>(entity)?,
            world.get::<RapierContextEntityLink>(entity)?,
        ))
    });
    binding.is_some_and(|(handle, link)| {
        world.get::<DefaultRapierContext>(link.0).is_some()
            && world
                .get::<RapierContextColliders>(link.0)
                .is_some_and(|context| {
                    context.entity2collider().get(&entity) == Some(&handle.0)
                        && context
                            .colliders
                            .get(handle.0)
                            .is_some_and(|backend| synchronized(world, entity, backend, sensor))
                })
    })
}

fn synchronized(
    world: &World,
    entity: Entity,
    backend: &bevy_rapier3d::rapier::geometry::Collider,
    sensor: bool,
) -> bool {
    let Some(global) = world.get::<GlobalTransform>(entity) else {
        return false;
    };
    let Some(current) = super::manual_projection::current_global(world, entity) else {
        return false;
    };
    let transform = global.compute_transform();
    // Rapier represents translation/rotation/scale, not a sheared affine transform.
    let affine = global.affine();
    if !affine.abs_diff_eq(current.affine(), 1.0e-4)
        || !affine.abs_diff_eq(GlobalTransform::from(transform).affine(), 1.0e-4)
    {
        return false;
    }
    let Some(collider) = world.get::<Collider>(entity) else {
        return false;
    };
    let pose = bevy_rapier3d::utils::iso_to_transform(backend.position());
    pose.translation.abs_diff_eq(transform.translation, 1.0e-4)
        && pose.rotation.abs_diff_eq(transform.rotation, 1.0e-5)
        && collider.scale().abs_diff_eq(transform.scale, 1.0e-4)
        && std::ptr::eq(collider.raw.as_ref(), backend.shape())
        && backend.is_enabled()
        && backend.is_sensor() == sensor
        && backend.parent().is_none()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn verified_noncolliding_policy_is_explicit_and_bound_to_the_placement() {
        let mut world = World::new();
        let id = EntityId::deterministic("noncolliding-contract");
        let root = world.spawn_empty().id();
        let mut expected = ManualPlacementCollisionExpected {
            placement: id,
            primitives: Vec::new(),
        };
        assert_eq!(
            inspect(&world, root, &expected),
            ManualPlacementCollisionStatus::MissingPrimitives
        );
        world
            .entity_mut(root)
            .insert(ManualPlacementCollisionNotRequired(id));
        assert_eq!(
            inspect(&world, root, &expected),
            ManualPlacementCollisionStatus::Registered { primitives: 0 }
        );
        expected.placement = EntityId::deterministic("wrong-placement");
        assert_eq!(
            inspect(&world, root, &expected),
            ManualPlacementCollisionStatus::Failed("noncolliding_prototype_contract_mismatch")
        );
        expected.placement = id;
        expected.primitives.push(Entity::PLACEHOLDER);
        assert_eq!(
            inspect(&world, root, &expected),
            ManualPlacementCollisionStatus::Failed("noncolliding_prototype_contract_mismatch")
        );
        world
            .entity_mut(root)
            .remove::<ManualPlacementCollisionNotRequired>();
        assert_eq!(
            inspect(&world, root, &expected),
            ManualPlacementCollisionStatus::Failed("missing_or_replaced_collision_primitive")
        );
    }
    use bevy_rapier3d::prelude::{NoUserData, RapierPhysicsPlugin};

    #[test]
    fn manual_readiness_requires_real_backend_and_exact_primitives() {
        let mut app = App::new();
        app.add_plugins((
            MinimalPlugins,
            TransformPlugin,
            RapierPhysicsPlugin::<NoUserData>::default().in_fixed_schedule(),
        ));
        let id = EntityId::deterministic("manual-readiness");
        let primitive = app
            .world_mut()
            .spawn((
                ManualPlacementCollision(id),
                Collider::cuboid(1.0, 1.0, 1.0),
                Transform::default(),
            ))
            .id();
        let mut expected = ManualPlacementCollisionExpected {
            placement: id,
            primitives: vec![primitive],
        };
        assert_eq!(
            inspect(app.world(), Entity::PLACEHOLDER, &expected),
            ManualPlacementCollisionStatus::Waiting {
                registered: 0,
                expected: 1
            }
        );
        app.update();
        app.world_mut().run_schedule(FixedUpdate);
        assert_eq!(
            inspect(app.world(), Entity::PLACEHOLDER, &expected),
            ManualPlacementCollisionStatus::Registered { primitives: 1 }
        );
        expected.primitives.push(primitive);
        assert_eq!(
            inspect(app.world(), Entity::PLACEHOLDER, &expected),
            ManualPlacementCollisionStatus::Failed("duplicate_collision_primitive")
        );
        expected.primitives.pop();
        app.world_mut()
            .entity_mut(primitive)
            .insert(ManualPlacementCollisionFailed("invalid"));
        assert_eq!(
            inspect(app.world(), Entity::PLACEHOLDER, &expected),
            ManualPlacementCollisionStatus::Failed("invalid")
        );
        app.world_mut()
            .entity_mut(primitive)
            .remove::<ManualPlacementCollisionFailed>();
        app.world_mut()
            .entity_mut(primitive)
            .insert(RapierContextEntityLink(Entity::PLACEHOLDER));
        assert_eq!(
            inspect(app.world(), Entity::PLACEHOLDER, &expected),
            ManualPlacementCollisionStatus::Waiting {
                registered: 0,
                expected: 1
            }
        );
        app.world_mut().despawn(primitive);
        assert_eq!(
            inspect(app.world(), Entity::PLACEHOLDER, &expected),
            ManualPlacementCollisionStatus::Failed("missing_or_replaced_collision_primitive")
        );
        expected.primitives.clear();
        assert_eq!(
            inspect(app.world(), Entity::PLACEHOLDER, &expected),
            ManualPlacementCollisionStatus::MissingPrimitives
        );
    }
}
