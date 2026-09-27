//! Fixed Rapier collision derived only from accepted profiled path surfaces.
use bevy::prelude::*;
use bevy_rapier3d::prelude::Collider;
use forge_core::EntityId;
use forge_world::path_surface::PathSurfaceMesh;

/// Stable domain identity carried by one transient profiled-path collider.
#[derive(Component, Clone, Copy, Debug, Eq, PartialEq)]
pub struct PathSurfaceColliderIdentity(pub EntityId);

/// Builds the fixed trimesh that matches an already-derived profiled surface.
pub fn profiled_path_collider(mesh: &PathSurfaceMesh) -> Result<Collider, String> {
    if !mesh.profiled {
        return Err("Unprofiled paths retain terrain collision".into());
    }
    if mesh.indices.len() < 3 || !mesh.indices.len().is_multiple_of(3) {
        return Err("Profiled path mesh has invalid triangle indices".into());
    }
    let vertices = mesh
        .positions
        .iter()
        .copied()
        .map(Vec3::from_array)
        .collect();
    let triangles = mesh.indices.as_chunks::<3>().0.to_vec();
    Collider::trimesh(vertices, triangles)
        .map_err(|error| format!("Profiled path collider could not build trimesh: {error:?}"))
}

/// Requires the identity and a collider on the exact transient entity.
#[must_use]
pub fn profiled_path_collider_present(world: &World, entity: Entity, id: EntityId) -> bool {
    world.get::<PathSurfaceColliderIdentity>(entity) == Some(&PathSurfaceColliderIdentity(id))
        && world.get::<Collider>(entity).is_some()
        && super::static_collider_ready(world, entity)
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy_rapier3d::prelude::{NoUserData, RapierPhysicsPlugin};

    #[test]
    fn collider_presence_requires_registered_profiled_trimesh_and_identity() {
        let mesh = PathSurfaceMesh {
            id: EntityId::deterministic("profiled_path"),
            profiled: true,
            positions: vec![[0.0, 1.0, 0.0], [1.0, 1.0, 0.0], [0.0, 1.0, 1.0]],
            normals: vec![[0.0, 1.0, 0.0]; 3],
            colors: vec![[1.0; 4]; 3],
            indices: vec![0, 1, 2],
        };
        let mut app = App::new();
        app.add_plugins((
            MinimalPlugins,
            TransformPlugin,
            RapierPhysicsPlugin::<NoUserData>::default().in_fixed_schedule(),
        ));
        let id = EntityId::deterministic("profiled_path");
        let entity = app
            .world_mut()
            .spawn((
                PathSurfaceColliderIdentity(id),
                profiled_path_collider(&mesh).unwrap(),
                Transform::IDENTITY,
            ))
            .id();
        assert!(!profiled_path_collider_present(app.world(), entity, id));
        app.update();
        app.world_mut().run_schedule(FixedUpdate);
        assert!(profiled_path_collider_present(app.world(), entity, id));
        assert!(!profiled_path_collider_present(
            app.world(),
            entity,
            EntityId::deterministic("other_path")
        ));
        app.world_mut()
            .entity_mut(entity)
            .remove::<PathSurfaceColliderIdentity>();
        assert!(!profiled_path_collider_present(app.world(), entity, id));
        app.world_mut()
            .entity_mut(entity)
            .insert(PathSurfaceColliderIdentity(id))
            .remove::<Collider>();
        assert!(!profiled_path_collider_present(app.world(), entity, id));
    }
}
