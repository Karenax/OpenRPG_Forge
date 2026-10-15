//! Disposable source collider children beneath the existing placement hierarchy.
//! Contact policy is an explicit caller input, never inferred from render LODs.
use crate::{source_colliders, source_graph_scene::SourceGraphNode};
use bevy::prelude::*;
use bevy_rapier3d::prelude::{
    ColliderDisabled, CollisionGroups, ContactSkin, Friction, Restitution, Sensor, SolverGroups,
};
use serde_json::Value;
use std::collections::{BTreeMap, BTreeSet};

/// Resolved, transient backend settings, with the caller's evidence reference.
/// This is not a replacement for the original Unity material reference.
#[derive(Clone)]
pub struct ContactPolicy {
    pub friction: Friction,
    pub restitution: Restitution,
    pub contact_skin: ContactSkin,
    pub collision_groups: CollisionGroups,
    pub solver_groups: SolverGroups,
    pub evidence: String,
}

impl ContactPolicy {
    pub fn validate(&self) -> Result<(), String> {
        if self.evidence.trim().is_empty()
            || !self.friction.coefficient.is_finite()
            || self.friction.coefficient < 0.0
            || !self.restitution.coefficient.is_finite()
            || !(0.0..=1.0).contains(&self.restitution.coefficient)
            || !self.contact_skin.0.is_finite()
            || self.contact_skin.0 < 0.0
        {
            return Err("Source collider policy invalid or lacks provenance".into());
        }
        Ok(())
    }
}

/// Original observation retained on each disposable physical child.
#[derive(Component)]
pub struct SourceColliderIdentity {
    pub placement_root: Entity,
    pub guid: String,
    pub file_id: String,
    pub original: Value,
    pub policy_evidence: String,
}

#[derive(Component)]
struct SourceColliderBatch;

pub(crate) fn owns_batch(world: &World, root: Entity) -> bool {
    world.get::<SourceColliderBatch>(root).is_some()
}

/// Owned physical children; no renderer, document or placement identity is changed.
pub struct ColliderProjection {
    pub root: Entity,
    pub entities: Vec<Entity>,
}

pub(crate) fn under_root(world: &World, mut entity: Entity, root: Entity) -> bool {
    let mut seen = BTreeSet::new();
    while entity != root {
        if !seen.insert(entity) {
            return false;
        }
        let Some(parent) = world.get::<ChildOf>(entity) else {
            return false;
        };
        entity = parent.parent();
    }
    true
}

pub(crate) fn validate_node(
    world: &World,
    root: Entity,
    index: usize,
    parent: Entity,
) -> Result<(), String> {
    if !under_root(world, parent, root)
        || world
            .get::<SourceGraphNode>(parent)
            .is_none_or(|node| node.source_node != index)
        || world.get::<GlobalTransform>(parent).is_none()
        || !world
            .get::<Transform>(parent)
            .is_some_and(|pose| pose.to_matrix().is_finite())
    {
        return Err("Source collider node is outside its current placement hierarchy".into());
    }
    Ok(())
}

/// Validate the whole batch before spawning any collider. Source disabled and
/// trigger states remain independent of visual visibility. The caller must
/// qualify policy and supported composed transforms before admitting gameplay.
///
/// # Errors
/// Refuses missing/foreign nodes, duplicate ownership, incomplete policies or
/// non-finite backend settings. Unsupported source shapes are refused by prepare.
pub fn attach(
    world: &mut World,
    root: Entity,
    document: &Value,
    target_from_source: Mat4,
    nodes: &BTreeMap<usize, Entity>,
    policies: &BTreeMap<(String, String), ContactPolicy>,
) -> Result<ColliderProjection, String> {
    if !world.entities().contains(root) || world.get::<SourceColliderBatch>(root).is_some() {
        return Err("Source collider root missing or already owns a batch".into());
    }
    let boxes = source_colliders::prepare(document, target_from_source)?;
    if policies.len() != boxes.len() {
        return Err("Source collider policies do not match the complete batch".into());
    }
    for shape in &boxes {
        let parent = *nodes
            .get(&shape.node)
            .ok_or("Source collider node absent")?;
        validate_node(world, root, shape.node, parent)?;
        let policy = policies
            .get(&(shape.guid.clone(), shape.file_id.clone()))
            .ok_or("Source collider policy absent")?;
        policy.validate()?;
    }
    let mut entities = Vec::with_capacity(boxes.len());
    for shape in boxes {
        let parent = nodes[&shape.node];
        let policy = &policies[&(shape.guid.clone(), shape.file_id.clone())];
        let mut live = world.spawn((
            Transform::from_translation(shape.center),
            GlobalTransform::IDENTITY,
            ChildOf(parent),
            shape.collider,
            policy.friction,
            policy.restitution,
            policy.contact_skin,
            policy.collision_groups,
            policy.solver_groups,
            SourceColliderIdentity {
                placement_root: root,
                guid: shape.guid,
                file_id: shape.file_id,
                original: shape.original,
                policy_evidence: policy.evidence.clone(),
            },
        ));
        if !shape.enabled {
            live.insert(ColliderDisabled);
        }
        if shape.is_trigger {
            live.insert(Sensor);
        }
        entities.push(live.id());
    }
    world.entity_mut(root).insert(SourceColliderBatch);
    Ok(ColliderProjection { root, entities })
}

impl ColliderProjection {
    /// Remove only this projection's surviving children, preserving the placement.
    pub fn clear(self, world: &mut World) {
        for entity in self.entities {
            if world
                .get::<SourceColliderIdentity>(entity)
                .is_some_and(|identity| identity.placement_root == self.root)
            {
                world.despawn(entity);
            }
        }
        if let Ok(mut root) = world.get_entity_mut(self.root) {
            root.remove::<SourceColliderBatch>();
        }
    }
}
