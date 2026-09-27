//! Derived static collision primitives for verified manual assets, never authoring data.
use bevy::{mesh::VertexAttributeValues, prelude::*};
use bevy_rapier3d::prelude::Collider;
use forge_core::EntityId;

#[derive(Component, Clone, Copy, Debug)]
pub struct ManualPlacementCollision(pub EntityId);

#[derive(Component, Clone, Copy, Debug)]
pub struct ManualPlacementCollisionFailed(pub &'static str);

pub struct ManualPlacementCollisionPlugin;

impl Plugin for ManualPlacementCollisionPlugin {
    fn build(&self, app: &mut App) {
        app.add_systems(
            PostUpdate,
            (materialize, super::manual_readiness::observe).chain(),
        );
    }
}

/// Shared with the governed-kit adapter; one convex primitive, never an entire asset.
pub(crate) fn convex_collider(mesh: &Mesh) -> Result<Collider, &'static str> {
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
    if vertices.len() < 4 || vertices.iter().any(|vertex| !vertex.is_finite()) {
        return Err("invalid_convex_hull_vertices");
    }
    Collider::convex_hull(&vertices).ok_or("convex_hull_generation_failed")
}

type Pending = (
    With<ManualPlacementCollision>,
    Without<Collider>,
    Without<ManualPlacementCollisionFailed>,
);

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn materialize(
    mut commands: Commands,
    meshes: Res<Assets<Mesh>>,
    pending: Query<(Entity, &Mesh3d), Pending>,
) {
    for (entity, mesh) in &pending {
        let Some(mesh) = meshes.get(mesh) else {
            continue;
        };
        match convex_collider(mesh) {
            Ok(collider) => {
                commands.entity(entity).insert(collider);
            }
            Err(reason) => {
                commands
                    .entity(entity)
                    .insert(ManualPlacementCollisionFailed(reason));
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::{asset::RenderAssetUsages, mesh::PrimitiveTopology};

    #[test]
    fn manual_collision_prepares_once_and_keeps_invalid_primitives_failed() {
        let mut app = App::new();
        app.init_resource::<Assets<Mesh>>()
            .add_plugins(ManualPlacementCollisionPlugin);
        let points = vec![
            [0.0, 0.0, 0.0],
            [2.0, 0.0, 0.0],
            [0.0, 3.0, 0.0],
            [0.0, 0.0, 4.0],
        ];
        let mut mesh = Mesh::new(
            PrimitiveTopology::TriangleList,
            RenderAssetUsages::MAIN_WORLD,
        );
        mesh.insert_attribute(Mesh::ATTRIBUTE_POSITION, points.clone());
        let handle = app.world_mut().resource_mut::<Assets<Mesh>>().add(mesh);
        let valid = app
            .world_mut()
            .spawn((
                Mesh3d(handle.clone()),
                ManualPlacementCollision(EntityId::deterministic("valid")),
            ))
            .id();
        let mut invalid = Mesh::new(
            PrimitiveTopology::TriangleList,
            RenderAssetUsages::MAIN_WORLD,
        );
        invalid.insert_attribute(Mesh::ATTRIBUTE_POSITION, vec![[0.0_f32; 3]; 3]);
        let invalid_handle = app.world_mut().resource_mut::<Assets<Mesh>>().add(invalid);
        let failed = app
            .world_mut()
            .spawn((
                Mesh3d(invalid_handle),
                ManualPlacementCollision(EntityId::deterministic("invalid")),
            ))
            .id();
        app.update();
        assert!(app.world().get::<Collider>(valid).is_some());
        assert!(app.world().get::<Collider>(failed).is_none());
        assert_eq!(
            app.world()
                .get::<ManualPlacementCollisionFailed>(failed)
                .unwrap()
                .0,
            "invalid_convex_hull_vertices"
        );
        app.update();
        assert_eq!(
            app.world_mut()
                .query::<&Collider>()
                .iter(app.world())
                .count(),
            1
        );
        let VertexAttributeValues::Float32x3(observed) = app
            .world()
            .resource::<Assets<Mesh>>()
            .get(&handle)
            .unwrap()
            .attribute(Mesh::ATTRIBUTE_POSITION)
            .unwrap()
        else {
            panic!()
        };
        assert_eq!(observed, &points);
    }
}
