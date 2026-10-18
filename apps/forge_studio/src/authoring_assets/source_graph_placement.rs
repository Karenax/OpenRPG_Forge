//! Source data mounts under the existing common placement root, without PBR fallback.
//! Data attachment is not GPU admission: these roots remain hidden pending rendering.
use super::{source_graph_context, source_graph_host, source_graph_runtime};
use bevy::{ecs as bevy_ecs, prelude::*};
use forge_project::zone_authoring::physics::PhysicsSceneSettings;
use std::{collections::BTreeMap, sync::Arc};

#[derive(Component, Clone, Debug)]
pub(super) enum Status {
    Waiting,
    MountedPendingRender,
    Refused(String),
}

#[derive(Default)]
struct OwnedPhysics {
    simple: Option<forge_engine::source_collider_projection::ColliderProjection>,
    scene: Option<forge_engine::source_collider_scene::ColliderSceneProjection>,
}
impl OwnedPhysics {
    fn clear(&mut self, world: &mut World) {
        if let Some(scene) = self.scene.take() {
            scene.clear(world);
        }
        if let Some(simple) = self.simple.take() {
            simple.clear(world);
        }
    }
}

struct Record {
    carrier: Arc<[u8]>,
    physics: Option<super::source_graph_physics::SourcePhysicsPayload>,
    particles: Option<super::source_graph_particles::SourceParticlePayload>,
    owned_nodes: Vec<Entity>,
    pose: Option<Mat4>,
    contacts: Option<PhysicsSceneSettings>,
    projected_physics: OwnedPhysics,
}
impl Record {
    fn clear_nodes(&mut self, world: &mut World, root: Entity) {
        for entity in self.owned_nodes.drain(..) {
            if entity != root && world.get_entity(entity).is_ok() {
                world.despawn(entity);
            }
        }
    }
}
#[derive(Resource, Default)]
struct Records(BTreeMap<Entity, Record>);

pub(super) fn submit(
    commands: &mut Commands,
    root: Entity,
    carrier: Arc<[u8]>,
    physics: Option<super::source_graph_physics::SourcePhysicsPayload>,
    particles: Option<super::source_graph_particles::SourceParticlePayload>,
) {
    commands.queue(move |world: &mut World| {
        if world.get_entity(root).is_err() {
            return;
        }
        if world.resource::<Records>().0.contains_key(&root) {
            world.entity_mut(root).insert(Status::Refused(
                "Un host possède déjà ce root source".into(),
            ));
            return;
        }
        world.resource_mut::<Records>().0.insert(
            root,
            Record {
                carrier,
                physics,
                particles,
                owned_nodes: Vec::new(),
                pose: None,
                contacts: None,
                projected_physics: OwnedPhysics::default(),
            },
        );
        world.entity_mut(root).insert(Status::Waiting);
    });
}

/// Release owned physical handles before common root despawning.
pub(super) fn remove(commands: &mut Commands, root: Entity) {
    commands.queue(move |world: &mut World| {
        let record = world.resource_mut::<Records>().0.remove(&root);
        if let Some(mut record) = record {
            record.projected_physics.clear(world);
            record.clear_nodes(world, root);
        }
        source_graph_runtime::release(world, root);
        if world.get_entity(root).is_ok() {
            world.despawn(root);
        }
    });
}

fn mount(
    world: &mut World,
    root: Entity,
    record: &Record,
    pose: Mat4,
    contacts: Option<&PhysicsSceneSettings>,
) -> Result<(Vec<Entity>, OwnedPhysics), String> {
    let glb = super::source_graph_route::read(&record.carrier)?
        .ok_or("Placement sans déclaration source")?;
    source_graph_context::ObjectBasis::from_document(glb.document())?
        .ok_or("Conversion source déclarée absente")?;
    // An object-local bake/normalization is not a scene-world coordinate policy.
    // Persisted roots require declared world mappings; no preview offset/default.
    let policy = source_graph_runtime::RuntimePolicy {
        source_world_from_target_world: declared_world_mapping(
            glb.document(),
            "source_world_from_target_world",
        )?,
        source_absolute_world_from_target_world: declared_world_mapping(
            glb.document(),
            "source_absolute_world_from_target_world",
        )?,
        global_mip_bias: 0.,
        source_float_min: f32::MIN_POSITIVE,
    };
    let clock = world
        .get_resource::<source_graph_runtime::SourceClock>()
        .copied()
        .ok_or("Horloge source explicite absente")?;
    let settings = if let Some(physics) = &record.physics {
        physics.decode_for_carrier(forge_core::bytes_sha256(&record.carrier))?;
        let settings = contacts.ok_or("Paramètres physiques acceptés absents")?;
        settings.validate()?;
        Some(super::source_graph_physics::SceneContactSettings(settings.clone()).resolved()?)
    } else {
        None
    };
    let mut host = source_graph_host::mount_initial(
        world,
        root,
        &record.carrier,
        None,
        pose,
        &policy.frame(clock)?,
        BTreeMap::new(),
    )?
    .ok_or("Déclaration source absente au montage")?;
    let owned = host
        .attached
        .nodes
        .values()
        .copied()
        .chain(host.attached.primitives.iter().copied())
        .collect::<Vec<_>>();
    let result = (|| {
        if let Some(particles) = &record.particles {
            particles.install(world, &mut host, &record.carrier)?;
        }
        if let (Some(physics), Some(settings)) = (&record.physics, settings) {
            host.install_physics_package_with_settings(
                world,
                &record.carrier,
                &physics.wire,
                physics.manifest_sha256,
                &settings,
            )?;
        }
        host.install_shadow_routes(world, 31)?;
        Ok::<_, String>(())
    })();
    if let Err(error) = result {
        host.remove_colliders(world);
        host.release_particle_instances(world);
        for &entity in &owned {
            if entity != root && world.get_entity(entity).is_ok() {
                world.despawn(entity);
            }
        }
        world
            .entity_mut(root)
            .remove::<super::source_graph_scene::SourceSceneAttached>();
        return Err(error);
    }
    // This placement adapter owns physical records separately so contact/pose
    // replacement and a vanished root can release them even after reparenting.
    let mut physics = OwnedPhysics {
        simple: host.collider_projection.take(),
        scene: host.collider_scene_projection.take(),
    };
    if let Err(error) = source_graph_runtime::retain(world, host, policy) {
        physics.clear(world);
        for &entity in &owned {
            if entity != root && world.get_entity(entity).is_ok() {
                world.despawn(entity);
            }
        }
        world
            .entity_mut(root)
            .remove::<super::source_graph_scene::SourceSceneAttached>();
        return Err(error);
    }
    Ok((owned, physics))
}

fn declared_world_mapping(document: &serde_json::Value, key: &str) -> Result<Mat4, String> {
    let values = document["extras"]["source_frame_binding"][key]
        .as_array()
        .ok_or_else(|| format!("Conversion monde source explicite absente : {key}"))?;
    if values.len() != 16 {
        return Err("Conversion monde source : 16 valeurs requises".into());
    }
    let mut rows = [0.; 16];
    for (value, target) in values.iter().zip(&mut rows) {
        *target = source_graph_context::number(value, "Conversion monde source")?;
    }
    source_graph_context::world_mapping(Mat4::from_cols_array(&rows).transpose(), Vec3::ZERO)
}

fn current_pose(world: &World, root: Entity) -> Result<Mat4, String> {
    let mut node = Some(root);
    let mut seen = std::collections::BTreeSet::new();
    let mut pose = Mat4::IDENTITY;
    while let Some(entity) = node {
        if !seen.insert(entity) {
            return Err("Cycle dans les parents du placement source".into());
        }
        let local = world
            .get::<Transform>(entity)
            .ok_or("Transformation source absente")?
            .to_matrix();
        pose = local * pose;
        node = world.get::<ChildOf>(entity).map(ChildOf::parent);
    }
    if !pose.is_finite() || !pose.determinant().is_finite() || pose.determinant() == 0. {
        return Err("Pose source non finie ou non inversible".into());
    }
    Ok(pose)
}

/// Rebuild only owned derived nodes when author pose or accepted contacts change.
/// Common root, persisted identity, selection and editor components are retained.
fn refresh(world: &mut World) {
    let contacts = world
        .get_resource::<super::source_graph_physics::SceneContactSettings>()
        .map(|settings| settings.0.clone());
    world.resource_scope(|world, mut records: Mut<Records>| {
        records.0.retain(|&root, record| {
            if world.get_entity(root).is_err() {
                record.projected_physics.clear(world);
                source_graph_runtime::release(world, root);
                record.clear_nodes(world, root);
                return false;
            }
            let pose = match current_pose(world, root) {
                Ok(pose) => pose,
                Err(error) => {
                    record.projected_physics.clear(world);
                    source_graph_runtime::release(world, root);
                    record.clear_nodes(world, root);
                    record.pose = None;
                    world
                        .entity_mut(root)
                        .remove::<super::source_graph_scene::SourceSceneAttached>()
                        .insert((Visibility::Hidden, Status::Refused(error)));
                    return true;
                }
            };
            let should_rebuild = record.pose != Some(pose)
                || record.physics.is_some() && record.contacts != contacts;
            if !should_rebuild {
                return true;
            }
            record.projected_physics.clear(world);
            source_graph_runtime::release(world, root);
            record.clear_nodes(world, root);
            world
                .entity_mut(root)
                .remove::<super::source_graph_scene::SourceSceneAttached>();
            world
                .entity_mut(root)
                .insert((Visibility::Hidden, GlobalTransform::from(pose)));
            record.pose = Some(pose);
            record.contacts.clone_from(&contacts);
            let status = match mount(world, root, record, pose, contacts.as_ref()) {
                Ok((nodes, physics)) => {
                    record.owned_nodes = nodes;
                    record.projected_physics = physics;
                    Status::MountedPendingRender
                }
                Err(error) => Status::Refused(error),
            };
            world.entity_mut(root).insert(status);
            true
        });
    });
}

pub(super) fn register(app: &mut App) {
    app.init_resource::<Records>();
    app.add_systems(
        PostUpdate,
        refresh
            .after(super::preview::PreviewSystems::Requests)
            .after(bevy::transform::TransformSystems::Propagate)
            .after(super::placements::PlacementSystems::Projection)
            .after(source_graph_runtime::update),
    );
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_engine::source_collider_projection::{ColliderProjection, SourceColliderIdentity};

    fn fixture(world: &mut World) -> (Entity, Entity, Entity, Entity) {
        world.init_resource::<Records>();
        let root = world
            .spawn((Transform::IDENTITY, Name::new("author identity")))
            .id();
        // Detached children still belong to the placement's explicit records.
        let node = world.spawn_empty().id();
        let physical = world
            .spawn(SourceColliderIdentity {
                placement_root: root,
                guid: "owned".into(),
                file_id: "1".into(),
                original: serde_json::json!({}),
                policy_evidence: "fixture".into(),
            })
            .id();
        let other = world.spawn_empty().id();
        let foreign = world
            .spawn(SourceColliderIdentity {
                placement_root: other,
                guid: "foreign".into(),
                file_id: "2".into(),
                original: serde_json::json!({}),
                policy_evidence: "fixture".into(),
            })
            .id();
        world.resource_mut::<Records>().0.insert(
            root,
            Record {
                carrier: Arc::from([]),
                physics: None,
                owned_nodes: vec![node],
                pose: Some(Mat4::IDENTITY),
                contacts: None,
                projected_physics: OwnedPhysics {
                    simple: Some(ColliderProjection {
                        root,
                        entities: vec![physical, foreign],
                    }),
                    scene: None,
                },
            },
        );
        (root, node, physical, foreign)
    }

    #[test]
    fn vanished_root_cleans_detached_owned_nodes_and_physics_only() {
        let mut world = World::new();
        let (root, node, physical, foreign) = fixture(&mut world);
        world.despawn(root);
        refresh(&mut world);
        assert!(world.get_entity(node).is_err());
        assert!(world.get_entity(physical).is_err());
        assert!(world.get_entity(foreign).is_ok());
        assert!(world.resource::<Records>().0.is_empty());
        refresh(&mut world);
        assert!(world.get_entity(foreign).is_ok());
    }

    #[test]
    fn invalid_pose_clears_derivatives_preserves_author_identity_and_refuses() {
        let mut world = World::new();
        let (root, node, physical, foreign) = fixture(&mut world);
        world.get_mut::<Transform>(root).unwrap().scale = Vec3::ZERO;
        refresh(&mut world);
        assert!(world.get_entity(node).is_err());
        assert!(world.get_entity(physical).is_err());
        assert!(world.get_entity(foreign).is_ok());
        assert_eq!(world.get::<Name>(root).unwrap().as_str(), "author identity");
        assert!(matches!(
            world.get::<Status>(root),
            Some(Status::Refused(_))
        ));
        assert_eq!(*world.get::<Visibility>(root).unwrap(), Visibility::Hidden);
        assert!(world.resource::<Records>().0[&root].pose.is_none());
    }

    #[test]
    fn pose_composes_parent_rotation_and_nonuniform_scale_without_decomposition() {
        let mut world = World::new();
        let parent_pose =
            Transform::from_scale(Vec3::new(2., 3., 4.)).with_rotation(Quat::from_rotation_y(0.7));
        let child_pose = Transform::from_xyz(3., 2., 1.).with_rotation(Quat::from_rotation_x(0.3));
        let parent = world.spawn(parent_pose).id();
        let root = world.spawn((child_pose, ChildOf(parent))).id();
        assert_eq!(
            current_pose(&world, root).unwrap(),
            parent_pose.to_matrix() * child_pose.to_matrix()
        );
        world.get_mut::<Transform>(parent).unwrap().translation = Vec3::X;
        assert_ne!(
            current_pose(&world, root).unwrap(),
            parent_pose.to_matrix() * child_pose.to_matrix()
        );
    }

    #[test]
    fn object_local_mapping_cannot_replace_explicit_world_mapping() {
        let mut document = serde_json::json!({"extras":{"source_frame_binding":{
            "source_object_from_target_object": [1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1]
        }}});
        assert!(declared_world_mapping(&document, "source_world_from_target_world").is_err());
        document["extras"]["source_frame_binding"]["source_world_from_target_world"] =
            serde_json::json!([1, 0, 0, 2, 0, 1, 0, 3, 0, 0, 1, 4, 0, 0, 0, 1]);
        assert_eq!(
            declared_world_mapping(&document, "source_world_from_target_world").unwrap(),
            Mat4::from_translation(Vec3::new(2., 3., 4.))
        );
    }
}
