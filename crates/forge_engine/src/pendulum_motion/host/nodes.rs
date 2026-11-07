//! Bind the common pendulum host to stable nodes of an admitted model scene.
use super::{Binding, Motion, resolve};
use crate::model_nodes::ModelNodeIdentity;
use bevy::prelude::*;
use std::collections::BTreeSet;

/// Initial motion for a node index in the placement's pinned model.
#[derive(Clone, Copy, Debug)]
pub struct NodeMotion {
    pub node_index: u64,
    pub motion: Motion,
}

/// Resolve only requested nodes in this placement. Unrelated model nodes are
/// preserved; repeated requested identities or ambiguous scene nodes fail.
/// The original local frame comes from the loader, not a possibly animated pose.
pub fn bindings(
    world: &World,
    root: Entity,
    motions: &[NodeMotion],
) -> Result<Vec<Binding>, String> {
    let keys: Vec<_> = motions.iter().map(|motion| motion.node_index).collect();
    let selected: BTreeSet<_> = keys.iter().copied().collect();
    if selected.len() != keys.len() {
        return Err("Model motion node identity is duplicated".into());
    }
    let nodes = resolve::entities(world, root, &keys, |world, entity| {
        Ok(world
            .get::<ModelNodeIdentity>(entity)
            .map(|node| node.index())
            .filter(|index| selected.contains(index)))
    })?;
    motions
        .iter()
        .zip(nodes)
        .map(|(motion, entity)| {
            let rest = world
                .get::<ModelNodeIdentity>(entity)
                .ok_or("Resolved model node identity disappeared")?
                .rest();
            Ok(Binding {
                key: motion.node_index,
                entity,
                pivot: rest.translation,
                rest_rotation: rest.rotation,
                rest_scale: rest.scale,
                motion: motion.motion,
            })
        })
        .collect()
}
