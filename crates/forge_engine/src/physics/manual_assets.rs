//! Derived static collision primitives for verified manual assets, never authoring data.
use bevy::{mesh::VertexAttributeValues, prelude::*};
use bevy_rapier3d::prelude::Collider;
use forge_core::EntityId;

#[derive(Component, Clone, Copy, Debug)]
pub struct ManualPlacementCollision(pub EntityId);

#[derive(Component, Clone, Copy, Debug)]
pub struct ManualPlacementCollisionFailed(pub &'static str);

pub struct ManualPlacementCollisionPlugin;

/// Bake a box's local yaw into its shape so a placement's nonuniform scale
/// applies to the exact volume without introducing a sheared child transform.
pub(crate) fn yawed_box_collider(half: Vec3, yaw: f32) -> Result<Collider, &'static str> {
    if !half.is_finite() || half.min_element() <= 0.0 || !yaw.is_finite() {
        return Err("invalid_oriented_box");
    }
    if yaw == 0.0 {
        return Ok(Collider::cuboid(half.x, half.y, half.z));
    }
    let rotation = Quat::from_rotation_y(yaw);
    let points = std::array::from_fn::<_, 8, _>(|corner| {
        let signs = Vec3::new(
            if corner & 1 == 0 { -1.0 } else { 1.0 },
            if corner & 2 == 0 { -1.0 } else { 1.0 },
            if corner & 4 == 0 { -1.0 } else { 1.0 },
        );
        rotation * (signs * half)
    });
    Collider::convex_hull(&points).ok_or("oriented_box_generation_failed")
}

/// Source-authored `FishStand` solid volumes in the placement-local frame.
/// The parent placement applies the single authored yaw; children stay local.
#[must_use]
pub fn fish_stand_colliders() -> Vec<(Transform, Collider)> {
    [
        (Vec3::new(0.0, 1.2, 0.0), Vec3::new(1.45, 1.2, 0.95)),
        (Vec3::new(-1.85, 0.35, 0.25), Vec3::new(0.45, 0.35, 0.25)),
        (Vec3::new(1.8, 0.23, -0.2), Vec3::new(0.3, 0.23, 0.6)),
        (Vec3::new(1.55, 0.28, 1.05), Vec3::new(0.2, 0.28, 0.2)),
        (Vec3::new(1.95, 0.42, 1.45), Vec3::new(0.35, 0.42, 0.25)),
    ]
    .into_iter()
    .map(|(center, half)| {
        (
            Transform::from_translation(center),
            Collider::cuboid(half.x, half.y, half.z),
        )
    })
    .collect()
}

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
    pending: Query<
        (
            Entity,
            &Mesh3d,
            Option<&super::ManualPlacementCollisionGeometry>,
        ),
        Pending,
    >,
) {
    for (entity, mesh, geometry) in &pending {
        let Some(mesh) = meshes.get(mesh) else {
            continue;
        };
        let prepared = match geometry.copied().unwrap_or_default() {
            super::ManualPlacementCollisionGeometry::ConvexPrimitive => convex_collider(mesh),
            super::ManualPlacementCollisionGeometry::TriangleMesh => {
                super::manual_triangle::triangle_collider(mesh)
            }
        };
        match prepared {
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
    use bevy_rapier3d::prelude::{NoUserData, RapierPhysicsPlugin};

    #[test]
    fn oriented_box_preserves_rotated_corners_under_nonuniform_scale() {
        let half = Vec3::new(1.0, 2.0, 3.0);
        let yaw = 0.6;
        let scale = Vec3::new(2.0, 0.5, 1.25);
        let mut collider = yawed_box_collider(half, yaw).unwrap();
        collider.set_scale(scale, 20);
        let hull = collider.as_convex_polyhedron().unwrap();
        let points = hull.points().collect::<Vec<_>>();
        assert_eq!(points.len(), 8);
        for x in [-half.x, half.x] {
            for y in [-half.y, half.y] {
                for z in [-half.z, half.z] {
                    let expected = scale * (Quat::from_rotation_y(yaw) * Vec3::new(x, y, z));
                    assert!(
                        points
                            .iter()
                            .any(|point| point.abs_diff_eq(expected, 1.0e-5))
                    );
                }
            }
        }
        assert!(yawed_box_collider(half, 0.0).unwrap().as_cuboid().is_some());
        assert!(yawed_box_collider(Vec3::ZERO, yaw).is_err());
        assert!(yawed_box_collider(half, f32::NAN).is_err());
    }

    #[test]
    fn fish_stand_colliders_follow_common_rotation_and_nonuniform_scale() {
        let mut app = App::new();
        app.add_plugins((
            MinimalPlugins,
            TransformPlugin,
            RapierPhysicsPlugin::<NoUserData>::default().in_fixed_schedule(),
        ));
        let root = app
            .world_mut()
            .spawn(Transform {
                translation: Vec3::new(5.0, 2.0, -7.0),
                rotation: Quat::from_euler(EulerRot::XYZ, 0.2, 0.7, 0.1),
                scale: Vec3::new(2.0, 1.5, 0.5),
            })
            .id();
        let primitives = fish_stand_colliders()
            .into_iter()
            .map(|(transform, collider)| {
                app.world_mut()
                    .spawn((transform, collider, ChildOf(root)))
                    .id()
            })
            .collect::<Vec<_>>();
        assert_eq!(primitives.len(), 5);
        app.update();
        app.world_mut().run_schedule(FixedUpdate);
        for primitive in primitives {
            assert!(super::super::static_collider_ready(app.world(), primitive));
            assert!(
                app.world()
                    .get::<Collider>(primitive)
                    .unwrap()
                    .scale()
                    .abs_diff_eq(Vec3::new(2.0, 1.5, 0.5), 1.0e-4)
            );
        }
    }

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
