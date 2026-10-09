//! Source-authored static triangle meshes. Never replace them with convex hulls.
use bevy::{
    mesh::{Indices, PrimitiveTopology, VertexAttributeValues},
    prelude::*,
};
use bevy_rapier3d::prelude::Collider;

/// Derived from the admitted asset's source collider declaration.
/// Existing governed UCX primitives retain their convex behavior.
#[derive(Component, Clone, Copy, Debug, Default, PartialEq, Eq)]
pub enum ManualPlacementCollisionGeometry {
    #[default]
    ConvexPrimitive,
    TriangleMesh,
}

pub(super) fn triangle_collider(mesh: &Mesh) -> Result<Collider, &'static str> {
    if mesh.primitive_topology() != PrimitiveTopology::TriangleList {
        return Err("collision_requires_triangle_list");
    }
    let Some(VertexAttributeValues::Float32x3(positions)) =
        mesh.attribute(Mesh::ATTRIBUTE_POSITION)
    else {
        return Err("missing_float32x3_positions");
    };
    let vertices = positions
        .iter()
        .copied()
        .map(Vec3::from_array)
        .collect::<Vec<_>>();
    if vertices.len() < 3 || vertices.iter().any(|vertex| !vertex.is_finite()) {
        return Err("invalid_triangle_mesh_vertices");
    }
    let indices = match mesh.indices() {
        Some(Indices::U16(values)) => values.iter().copied().map(u32::from).collect::<Vec<_>>(),
        Some(Indices::U32(values)) => values.clone(),
        None => {
            let count = u32::try_from(vertices.len()).map_err(|_| "triangle_mesh_vertex_limit")?;
            (0..count).collect()
        }
    };
    if indices.is_empty()
        || indices.len() % 3 != 0
        || indices
            .iter()
            .any(|&index| index as usize >= vertices.len())
    {
        return Err("invalid_triangle_mesh_indices");
    }
    let triangles = indices
        .chunks_exact(3)
        .map(|face| [face[0], face[1], face[2]])
        .collect();
    Collider::trimesh(vertices, triangles).map_err(|_| "triangle_mesh_generation_failed")
}

#[cfg(test)]
mod tests {
    use super::super::{ManualPlacementCollision, ManualPlacementCollisionPlugin};
    use super::*;
    use bevy::asset::RenderAssetUsages;
    use forge_core::EntityId;

    fn separated_surfaces() -> Mesh {
        let mut mesh = Mesh::new(
            PrimitiveTopology::TriangleList,
            RenderAssetUsages::MAIN_WORLD,
        );
        mesh.insert_attribute(
            Mesh::ATTRIBUTE_POSITION,
            vec![
                [-3., 0., -1.],
                [-1., 0., -1.],
                [-1., 0., 1.],
                [-3., 0., 1.],
                [1., 0., -1.],
                [3., 0., -1.],
                [3., 0., 1.],
                [1., 0., 1.],
            ],
        );
        mesh.insert_indices(Indices::U32(vec![0, 2, 1, 0, 3, 2, 4, 6, 5, 4, 7, 6]));
        mesh
    }

    #[test]
    fn concave_source_mesh_keeps_the_gap_between_surfaces() {
        let collider = triangle_collider(&separated_surfaces()).unwrap();
        assert!(collider.as_trimesh().is_some());
        assert!(collider.as_convex_polyhedron().is_none());
        assert!(
            collider
                .cast_local_ray(Vec3::new(0., 1., 0.), Vec3::NEG_Y, 2., false)
                .is_none()
        );
        assert_eq!(
            collider.cast_local_ray(Vec3::new(-2., 1., 0.), Vec3::NEG_Y, 2., false),
            Some(1.)
        );
    }

    #[test]
    fn invalid_triangle_indices_are_rejected_without_convex_fallback() {
        let mut mesh = separated_surfaces();
        mesh.insert_indices(Indices::U16(vec![0, 1, 999]));
        assert_eq!(
            triangle_collider(&mesh).unwrap_err(),
            "invalid_triangle_mesh_indices"
        );
        mesh.insert_indices(Indices::U16(vec![0, 1]));
        assert_eq!(
            triangle_collider(&mesh).unwrap_err(),
            "invalid_triangle_mesh_indices"
        );
    }

    #[test]
    fn manual_projection_materializes_explicit_triangle_geometry() {
        let mut app = App::new();
        app.init_resource::<Assets<Mesh>>()
            .add_plugins(ManualPlacementCollisionPlugin);
        let handle = app
            .world_mut()
            .resource_mut::<Assets<Mesh>>()
            .add(separated_surfaces());
        let entity = app
            .world_mut()
            .spawn((
                Mesh3d(handle),
                ManualPlacementCollision(EntityId::deterministic("triangle-source")),
                ManualPlacementCollisionGeometry::TriangleMesh,
            ))
            .id();
        app.update();
        let collider = app.world().get::<Collider>(entity).unwrap();
        assert!(collider.as_trimesh().is_some());
        assert!(
            collider
                .cast_local_ray(Vec3::new(0., 1., 0.), Vec3::NEG_Y, 2., false)
                .is_none()
        );
        app.update();
        assert_eq!(
            app.world_mut()
                .query::<&Collider>()
                .iter(app.world())
                .count(),
            1
        );
    }
}
