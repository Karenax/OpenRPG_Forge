//! Disposable mesh collider children; no source or renderer identity changes.
use crate::{
    source_collider_projection::{ContactPolicy, SourceColliderIdentity, validate_node},
    source_mesh_colliders::{self, MeshCookingPolicy},
};
use bevy::prelude::*;
use bevy_rapier3d::prelude::{ColliderDisabled, Sensor};
use forge_assets::source_graph_glb::SourceGraphGlb;
use std::collections::BTreeMap;

/// Retain source options and explicit geometry evidence on physical or empty
/// children alike. This component never declares native query equivalence.
#[derive(Component)]
pub struct SourceMeshCooking {
    pub source_convex: bool,
    pub source_cooking_options: u32,
    pub geometry_evidence: Option<String>,
}

#[derive(Component)]
struct SourceMeshBatch;

pub(crate) fn owns_batch(world: &World, root: Entity) -> bool {
    world.get::<SourceMeshBatch>(root).is_some()
}

pub struct MeshColliderProjection {
    pub root: Entity,
    /// Includes observations with no source geometry; those have no Collider.
    pub entities: Vec<Entity>,
}

fn validate_policy(policy: &ContactPolicy) -> Result<(), String> {
    if policy.evidence.trim().is_empty()
        || !policy.friction.coefficient.is_finite()
        || policy.friction.coefficient < 0.0
        || !policy.restitution.coefficient.is_finite()
        || !(0.0..=1.0).contains(&policy.restitution.coefficient)
        || !policy.contact_skin.0.is_finite()
        || policy.contact_skin.0 < 0.0
    {
        return Err("mesh contact policy invalid or without evidence".into());
    }
    Ok(())
}

/// Prepare geometry and validate the entire mesh batch before allocating ECS
/// children. Empty source meshes remain explicit empty observations. Physical
/// policy and supported composed transforms require separate runtime evidence.
pub fn attach(
    world: &mut World,
    root: Entity,
    glb: &SourceGraphGlb<'_>,
    nodes: &BTreeMap<usize, Entity>,
    cooking: &BTreeMap<(String, String), MeshCookingPolicy>,
    contacts: &BTreeMap<(String, String), ContactPolicy>,
) -> Result<MeshColliderProjection, String> {
    if !world.entities().contains(root) || world.get::<SourceMeshBatch>(root).is_some() {
        return Err("source mesh root missing or already owns a batch".into());
    }
    let shapes = source_mesh_colliders::prepare(glb, cooking)?;
    if contacts.len() != shapes.iter().filter(|s| s.collider.is_some()).count() {
        return Err("mesh contact policies do not match assigned shapes".into());
    }
    for shape in &shapes {
        let parent = *nodes.get(&shape.node).ok_or("source mesh node missing")?;
        validate_node(world, root, shape.node, parent)?;
        let key = (shape.guid.clone(), shape.file_id.clone());
        if shape.collider.is_some() {
            validate_policy(contacts.get(&key).ok_or("mesh contact policy missing")?)?;
        } else if contacts.contains_key(&key) {
            return Err("empty source mesh must not have a contact policy".into());
        }
    }
    let mut entities = Vec::with_capacity(shapes.len());
    for shape in shapes {
        let policy = contacts.get(&(shape.guid.clone(), shape.file_id.clone()));
        let mut live = world.spawn((
            Transform::IDENTITY,
            GlobalTransform::IDENTITY,
            ChildOf(nodes[&shape.node]),
            SourceColliderIdentity {
                placement_root: root,
                guid: shape.guid,
                file_id: shape.file_id,
                original: shape.original,
                policy_evidence: policy.map_or_else(String::new, |p| p.evidence.clone()),
            },
            SourceMeshCooking {
                source_convex: shape.source_convex,
                source_cooking_options: shape.source_cooking_options,
                geometry_evidence: shape.geometry_evidence,
            },
        ));
        if let Some(collider) = shape.collider {
            let policy = policy.ok_or("validated mesh contact policy disappeared")?;
            live.insert((
                collider,
                policy.friction,
                policy.restitution,
                policy.contact_skin,
                policy.collision_groups,
                policy.solver_groups,
            ));
        }
        if !shape.enabled {
            live.insert(ColliderDisabled);
        }
        if shape.is_trigger {
            live.insert(Sensor);
        }
        entities.push(live.id());
    }
    world.entity_mut(root).insert(SourceMeshBatch);
    Ok(MeshColliderProjection { root, entities })
}

impl MeshColliderProjection {
    /// Clear only this mesh projection; leave placement, renderers and boxes.
    pub fn clear(self, world: &mut World) {
        for entity in self.entities {
            if world
                .get::<SourceColliderIdentity>(entity)
                .is_some_and(|i| i.placement_root == self.root)
                && world.get::<SourceMeshCooking>(entity).is_some()
            {
                world.despawn(entity);
            }
        }
        if let Ok(mut root) = world.get_entity_mut(self.root) {
            root.remove::<SourceMeshBatch>();
        }
    }
}
