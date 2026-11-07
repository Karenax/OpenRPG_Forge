use bevy::prelude::*;
use bevy_rapier3d::prelude::{Collider, ColliderDisabled, RigidBody};
use forge_project::{VegetationCollisionShape, VegetationDocument, VegetationKind};
use forge_world::{VegetationInstance, VegetationInstanceId};

/// Derived physical identity, deliberately separate from both visual LOD entities.
#[derive(Clone, Copy, Component, Debug, Eq, PartialEq)]
pub(super) struct TreeTrunkCollider(pub VegetationInstanceId);

#[derive(Component)]
pub(super) struct ActivationDistance(f32);

#[derive(Resource)]
struct TrunkInventory(std::collections::BTreeSet<forge_core::EntityId>);

/// Actual backend bindings, independent of visual LOD or render density.
pub fn governed_forest_collisions_ready(world: &World) -> bool {
    use bevy_rapier3d::{
        plugin::context::{DefaultRapierContext, RapierContextColliders},
        prelude::{RapierColliderHandle, RapierContextEntityLink, RapierRigidBodyHandle},
    };
    let Some(expected) = world.get_resource::<TrunkInventory>() else {
        return false;
    };
    let Some(mut query) = world.try_query::<(Entity, &TreeTrunkCollider)>() else {
        return expected.0.is_empty();
    };
    let mut seen = std::collections::BTreeSet::new();
    for (entity, identity) in query.iter(world) {
        if !seen.insert(identity.0.as_entity_id())
            || !expected.0.contains(&identity.0.as_entity_id())
        {
            return false;
        }
        let (Some(handle), Some(link), Some(body), Some(collider), Some(global)) = (
            world.get::<RapierColliderHandle>(entity),
            world.get::<RapierContextEntityLink>(entity),
            world.get::<RapierRigidBodyHandle>(entity),
            world.get::<Collider>(entity),
            world.get::<GlobalTransform>(entity),
        ) else {
            return false;
        };
        let Some(context) = world.get::<RapierContextColliders>(link.0) else {
            return false;
        };
        let Some(backend) = context.colliders.get(handle.0) else {
            return false;
        };
        let pose = bevy_rapier3d::utils::iso_to_transform(backend.position());
        let transform = global.compute_transform();
        if world.get::<DefaultRapierContext>(link.0).is_none()
            || world.get::<RigidBody>(entity) != Some(&RigidBody::Fixed)
            || context.entity2collider().get(&entity) != Some(&handle.0)
            || backend.parent() != Some(body.0)
            || backend.is_sensor()
            || backend.is_enabled() == world.get::<ColliderDisabled>(entity).is_some()
            || !pose.translation.abs_diff_eq(transform.translation, 1.0e-4)
            || !pose.rotation.abs_diff_eq(transform.rotation, 1.0e-5)
            || !collider.scale().abs_diff_eq(transform.scale, 1.0e-4)
            || !std::ptr::eq(collider.raw.as_ref(), backend.shape())
        {
            return false;
        }
    }
    seen == expected.0
}

pub(super) struct TreeTrunkProjection {
    pub id: VegetationInstanceId,
    pub transform: Transform,
    pub radius: f32,
    pub half_height: f32,
    shape: VegetationCollisionShape,
    activation_distance: f32,
}

impl TreeTrunkProjection {
    pub(super) fn from_instance(
        instance: &VegetationInstance,
        document: &VegetationDocument,
    ) -> Option<Self> {
        if instance.kind != VegetationKind::Tree {
            return None;
        }
        let profile = document
            .layers
            .iter()
            .find(|layer| layer.id == instance.layer_id)?
            .collision?;
        let half_height = profile.height_m * instance.scale * 0.5;
        Some(Self {
            id: instance.id,
            transform: Transform::from_xyz(
                instance.position_m.x,
                instance.position_m.y + half_height,
                instance.position_m.z,
            ),
            radius: profile.radius_m * instance.scale,
            half_height,
            shape: profile.shape,
            activation_distance: profile.activation_distance_m,
        })
    }

    fn collider(&self) -> Collider {
        match self.shape {
            VegetationCollisionShape::Cylinder => Collider::cylinder(self.half_height, self.radius),
            VegetationCollisionShape::Capsule => {
                Collider::capsule_y((self.half_height - self.radius).max(0.0), self.radius)
            }
        }
    }
}

pub(super) fn spawn_tree_trunks(commands: &mut Commands, trunks: Vec<TreeTrunkProjection>) {
    let count = trunks.len();
    commands.insert_resource(TrunkInventory(
        trunks.iter().map(|trunk| trunk.id.as_entity_id()).collect(),
    ));
    for trunk in trunks {
        commands.spawn(trunk_bundle(&trunk));
    }
    info!(target: "forge::vegetation", schema = "forge.runtime-event", schema_version = 1_u64,
        event_kind = "vegetation_tree_colliders_materialized", tree_colliders = count,
        "Canonical tree trunk colliders materialized independently of visual LODs");
}

fn trunk_bundle(trunk: &TreeTrunkProjection) -> impl Bundle + use<> {
    (
        Name::new("Procedural tree trunk collision"),
        TreeTrunkCollider(trunk.id),
        ActivationDistance(trunk.activation_distance),
        trunk.transform,
        trunk.collider(),
        RigidBody::Fixed,
    )
}

/// Source changes replace canonical obstacles before the new source hashes are
/// acknowledged. Quality-only transitions never call this or change physics identity.
pub(super) fn replace_tree_trunks(world: &mut World, trunks: Vec<TreeTrunkProjection>) {
    world.insert_resource(TrunkInventory(
        trunks.iter().map(|trunk| trunk.id.as_entity_id()).collect(),
    ));
    let old: Vec<_> = world
        .query_filtered::<Entity, With<TreeTrunkCollider>>()
        .iter(world)
        .collect();
    for entity in old {
        world.despawn(entity);
    }
    for trunk in trunks {
        world.spawn(trunk_bundle(&trunk));
    }
}

// FixedUpdate precedes Rapier SyncBackend, including deferred enable/disable commands.
// No unique player: retain physical obstacles instead of disabling them silently.
#[allow(
    clippy::needless_pass_by_value,
    clippy::type_complexity,
    reason = "Bevy system parameters"
)]
pub(super) fn update_activation(
    mut commands: Commands,
    observers: Query<
        &Transform,
        Or<(
            With<crate::player_controller::ForgePlayerCharacter>,
            With<crate::gameplay::GameplayBody>,
        )>,
    >,
    trunks: Query<
        (
            Entity,
            &Transform,
            &ActivationDistance,
            Has<ColliderDisabled>,
        ),
        With<TreeTrunkCollider>,
    >,
) {
    for (entity, transform, distance, disabled) in &trunks {
        let active = observers.is_empty()
            || observers.iter().any(|observer| {
                let delta = (observer.translation - transform.translation).xz();
                delta.length_squared() <= distance.0 * distance.0
            });
        if active && disabled {
            commands.entity(entity).remove::<ColliderDisabled>();
        } else if !active && !disabled {
            commands.entity(entity).insert(ColliderDisabled);
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::transform::TransformPlugin;
    use bevy_rapier3d::prelude::*;

    fn first_trunk() -> TreeTrunkProjection {
        let source = forge_world::compile_world_sketch(
            &forge_world::embedded_visual_valley_world_sketch().unwrap(),
        )
        .unwrap();
        let generated = forge_world::VegetationGenerationPlan::from_documents(
            &source.carved_terrain,
            &source.vegetation,
        )
        .unwrap()
        .generate()
        .unwrap();
        generated
            .chunks
            .iter()
            .flat_map(|chunk| &chunk.instances)
            .find_map(|instance| TreeTrunkProjection::from_instance(instance, &source.vegetation))
            .unwrap()
    }

    #[test]
    fn canonical_trunk_inventory_requires_real_backend_bindings_and_detects_removal() {
        let trunk = first_trunk();
        let mut app = App::new();
        app.add_plugins((MinimalPlugins, TransformPlugin))
            .insert_resource(bevy::time::TimeUpdateStrategy::ManualDuration(
                std::time::Duration::from_secs_f64(1.0 / 60.0),
            ))
            .insert_resource(TimestepMode::Fixed {
                dt: 1.0 / 60.0,
                substeps: 1,
            })
            .add_plugins(RapierPhysicsPlugin::<NoUserData>::default().in_fixed_schedule());
        app.insert_resource(TrunkInventory(std::collections::BTreeSet::from([trunk
            .id
            .as_entity_id()])));
        let entity = app.world_mut().spawn(trunk_bundle(&trunk)).id();
        assert!(!governed_forest_collisions_ready(app.world()));
        for _ in 0..3 {
            app.update();
        }
        assert!(governed_forest_collisions_ready(app.world()));
        app.world_mut().entity_mut(entity).insert(ColliderDisabled);
        assert!(!governed_forest_collisions_ready(app.world()));
        app.update();
        assert!(governed_forest_collisions_ready(app.world()));
        app.world_mut().despawn(entity);
        assert!(!governed_forest_collisions_ready(app.world()));
    }

    #[test]
    fn a_distant_player_cannot_disable_a_trunk_protecting_another_gameplay_body() {
        let trunk = first_trunk();
        let mut app = App::new();
        app.add_systems(Update, update_activation);
        let entity = app
            .world_mut()
            .spawn((
                TreeTrunkCollider(trunk.id),
                Transform::default(),
                ActivationDistance(32.0),
                ColliderDisabled,
            ))
            .id();
        app.world_mut().spawn((
            crate::ForgePlayerCharacter,
            Transform::from_xyz(5000.0, 0.0, 0.0),
        ));
        let actor = app
            .world_mut()
            .spawn((
                crate::gameplay::GameplayBody {
                    previous_velocity: Vec3::ZERO,
                },
                Transform::default(),
            ))
            .id();
        app.update();
        assert!(app.world().get::<ColliderDisabled>(entity).is_none());
        app.world_mut()
            .get_mut::<Transform>(actor)
            .unwrap()
            .translation
            .x = 5000.0;
        app.update();
        assert!(app.world().get::<ColliderDisabled>(entity).is_some());
    }

    #[test]
    fn collision_profile_and_activation_follow_the_document_not_visual_lod() {
        let source = forge_world::compile_world_sketch(
            &forge_world::embedded_visual_valley_world_sketch().unwrap(),
        )
        .unwrap();
        let generated = forge_world::VegetationGenerationPlan::from_documents(
            &source.carved_terrain,
            &source.vegetation,
        )
        .unwrap()
        .generate()
        .unwrap();
        let instance = generated
            .chunks
            .iter()
            .flat_map(|chunk| &chunk.instances)
            .find(|instance| instance.kind == VegetationKind::Tree)
            .unwrap();
        let mut document = source.vegetation.clone();
        let layer_index = document
            .layers
            .iter()
            .position(|layer| layer.id == instance.layer_id)
            .unwrap();
        let mut profile = document.layers[layer_index].collision.unwrap();
        profile.shape = VegetationCollisionShape::Cylinder;
        document.layers[layer_index].collision = Some(profile);
        let cylinder = TreeTrunkProjection::from_instance(instance, &document).unwrap();
        assert!(cylinder.collider().as_cylinder().is_some());
        document.layers[layer_index].collision = None;
        assert!(TreeTrunkProjection::from_instance(instance, &document).is_none());
        let mut app = App::new();
        app.add_systems(Update, update_activation);
        let tree = app
            .world_mut()
            .spawn((
                TreeTrunkCollider(instance.id),
                Transform::default(),
                ActivationDistance(profile.activation_distance_m),
            ))
            .id();
        let player = app
            .world_mut()
            .spawn((
                crate::player_controller::ForgePlayerCharacter,
                Transform::default(),
            ))
            .id();
        for (distance, disabled) in [
            (0.0, false),
            (profile.activation_distance_m, false),
            (profile.activation_distance_m + 0.1, true),
            (0.0, false),
        ] {
            app.world_mut()
                .get_mut::<Transform>(player)
                .unwrap()
                .translation
                .x = distance;
            app.update();
            assert_eq!(
                app.world().get::<ColliderDisabled>(tree).is_some(),
                disabled
            );
            assert!(app.world().get::<TreeTrunkCollider>(tree).is_some());
        }
        app.world_mut().entity_mut(tree).insert(ColliderDisabled);
        app.world_mut().despawn(player);
        app.update();
        assert!(app.world().get::<ColliderDisabled>(tree).is_none());
    }

    #[test]
    fn rapier_controller_is_stopped_by_scaled_trunk_but_can_pass_beside_it() {
        let mut app = App::new();
        app.add_plugins((
            MinimalPlugins,
            TransformPlugin,
            RapierPhysicsPlugin::<NoUserData>::default(),
        ));
        let source = forge_world::compile_world_sketch(
            &forge_world::embedded_visual_valley_world_sketch().unwrap(),
        )
        .unwrap();
        let generated = forge_world::VegetationGenerationPlan::from_documents(
            &source.carved_terrain,
            &source.vegetation,
        )
        .unwrap()
        .generate()
        .unwrap();
        let instance = generated
            .chunks
            .iter()
            .flat_map(|chunk| &chunk.instances)
            .find(|instance| instance.kind == VegetationKind::Tree)
            .unwrap();
        let trunk = TreeTrunkProjection::from_instance(instance, &source.vegetation).unwrap();
        assert_eq!(trunk.id, instance.id);
        assert_eq!(
            trunk.radius.to_bits(),
            (source
                .vegetation
                .layers
                .iter()
                .find(|layer| layer.id == instance.layer_id)
                .unwrap()
                .collision
                .unwrap()
                .radius_m
                * instance.scale)
                .to_bits()
        );
        assert!(
            (trunk.transform.translation.y - trunk.half_height - instance.position_m.y).abs()
                < 0.0001
        );
        let center = trunk.transform.translation;
        let radius = trunk.radius;
        let tree = app
            .world_mut()
            .spawn((trunk.transform, trunk.collider(), RigidBody::Fixed))
            .id();
        let mut actors = Vec::new();
        for offset in [0.0, radius + 1.0] {
            actors.push(
                app.world_mut()
                    .spawn((
                        Transform::from_translation(center + Vec3::new(-2.0, 0.0, offset)),
                        Collider::capsule_y(0.4, 0.25),
                        KinematicCharacterController {
                            translation: Some(Vec3::X * 4.0),
                            ..default()
                        },
                    ))
                    .id(),
            );
        }
        for _ in 0..3 {
            app.update();
        }
        assert!(app.world().get::<RapierColliderHandle>(tree).is_some());
        let blocked = app
            .world()
            .get::<KinematicCharacterControllerOutput>(actors[0])
            .unwrap();
        assert!(blocked.collisions.iter().any(|hit| hit.entity == tree));
        let blocked_x = app
            .world()
            .get::<Transform>(actors[0])
            .unwrap()
            .translation
            .x;
        assert!(
            blocked_x < center.x - radius,
            "controller crossed the trunk: {blocked_x}"
        );
        let clear_x = app
            .world()
            .get::<Transform>(actors[1])
            .unwrap()
            .translation
            .x;
        assert!(
            clear_x > center.x + 1.9,
            "empty side corridor blocked: {clear_x}"
        );
    }
}
