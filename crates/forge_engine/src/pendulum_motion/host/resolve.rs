//! Resolve source-independent motion identities inside one admitted placement.
//!
//! The caller supplies data and a key reader for its already verified scene;
//! source-specific identity components remain owned by that scene.
use super::{Binding, Motion};
use bevy::prelude::*;
use std::collections::{BTreeMap, BTreeSet};

/// A verified asset's local frame and initial state for one identified pivot.
#[derive(Clone, Copy, Debug)]
pub struct Target {
    pub key: u64,
    pub pivot: Vec3,
    pub rest_rotation: Quat,
    pub rest_scale: Vec3,
    pub motion: Motion,
}

/// Join the complete desired pivot inventory to this placement's hierarchy.
/// Other placements may reuse the same keys. Missing, duplicate or unexpected
/// keys fail before a binding or scene state is changed.
pub fn bindings(
    world: &World,
    root: Entity,
    targets: &[Target],
    pivot_key: impl FnMut(&World, Entity) -> Result<Option<u64>, String>,
) -> Result<Vec<Binding>, String> {
    let keys: Vec<_> = targets.iter().map(|target| target.key).collect();
    let entities = entities(world, root, &keys, pivot_key)?;
    Ok(targets
        .iter()
        .zip(entities)
        .map(|(target, entity)| Binding {
            key: target.key,
            entity,
            pivot: target.pivot,
            rest_rotation: target.rest_rotation,
            rest_scale: target.rest_scale,
            motion: target.motion,
        })
        .collect())
}

/// Resolve identities first so a caller can also consume immutable node frames.
pub(crate) fn entities(
    world: &World,
    root: Entity,
    keys: &[u64],
    mut pivot_key: impl FnMut(&World, Entity) -> Result<Option<u64>, String>,
) -> Result<Vec<Entity>, String> {
    if world.get_entity(root).is_err() || keys.is_empty() {
        return Err("Motion root or target inventory is absent".into());
    }
    let mut pending = vec![root];
    let mut visited = BTreeSet::new();
    let mut found = BTreeMap::new();
    while let Some(entity) = pending.pop() {
        if !visited.insert(entity) {
            return Err("Motion hierarchy contains a cycle or duplicate child".into());
        }
        if let Some(key) = pivot_key(world, entity)? {
            if entity == root || found.insert(key, entity).is_some() {
                return Err("Motion pivot identity is ambiguous".into());
            }
            // The host applies the same ancestry bound and checks each link.
            super::ancestors(world, entity, root)?;
        }
        if let Some(children) = world.get::<Children>(entity) {
            for child in children.iter() {
                if world
                    .get::<ChildOf>(child)
                    .is_none_or(|parent| parent.parent() != entity)
                {
                    return Err("Motion hierarchy has an inconsistent child".into());
                }
                pending.push(child);
            }
        }
    }
    if found.len() != keys.len() {
        return Err("Motion pivot inventory differs from its asset".into());
    }
    let mut result = Vec::with_capacity(keys.len());
    for key in keys {
        let entity = found
            .remove(key)
            .ok_or("Motion pivot identity is missing or duplicated")?;
        result.push(entity);
    }
    if !found.is_empty() {
        return Err("Motion pivot inventory has unexpected identities".into());
    }
    Ok(result)
}
