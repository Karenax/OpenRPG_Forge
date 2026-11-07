//! Retire a mesh draw without leaving it in Bevy's potential-visibility lists.
use bevy::{camera::visibility::VisibilityClass, prelude::*};
use std::any::TypeId;

/// Required visibility components survive removal of their requiring Mesh3d.
/// Retain unrelated render classes, materials, identity and gameplay state.
#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy EntityCommand transfers the live entity wrapper; direct callers share this operation"
)]
pub(crate) fn remove_mesh3d(mut entity: EntityWorldMut<'_>) {
    entity.remove::<Mesh3d>();
    if let Some(mut classes) = entity.get_mut::<VisibilityClass>() {
        classes.retain(|class| *class != TypeId::of::<Mesh3d>());
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn retiring_and_restoring_a_mesh_preserves_other_visibility_classes_and_material() {
        let mut world = World::new();
        world
            .register_component_hooks::<Mesh3d>()
            .on_add(bevy::camera::visibility::add_visibility_class::<Mesh3d>);
        let mut classes = VisibilityClass::default();
        classes.push(TypeId::of::<Mesh2d>());
        let entity = world
            .spawn((
                classes,
                Mesh3d(Handle::default()),
                MeshMaterial3d::<StandardMaterial>(Handle::default()),
            ))
            .id();
        assert!(
            world
                .get::<VisibilityClass>(entity)
                .unwrap()
                .contains(&TypeId::of::<Mesh3d>())
        );
        remove_mesh3d(world.entity_mut(entity));
        assert!(world.get::<Mesh3d>(entity).is_none());
        assert!(
            world
                .get::<MeshMaterial3d<StandardMaterial>>(entity)
                .is_some()
        );
        assert_eq!(
            world.get::<VisibilityClass>(entity).unwrap().as_slice(),
            &[TypeId::of::<Mesh2d>()]
        );
        // Bevy's existing add hook restores membership when a LOD draws again.
        world.entity_mut(entity).insert(Mesh3d(Handle::default()));
        assert_eq!(
            world
                .get::<VisibilityClass>(entity)
                .unwrap()
                .iter()
                .filter(|c| **c == TypeId::of::<Mesh3d>())
                .count(),
            1
        );
        remove_mesh3d(world.entity_mut(entity));
        remove_mesh3d(world.entity_mut(entity));
        assert_eq!(
            world.get::<VisibilityClass>(entity).unwrap().as_slice(),
            &[TypeId::of::<Mesh2d>()]
        );
    }
}
