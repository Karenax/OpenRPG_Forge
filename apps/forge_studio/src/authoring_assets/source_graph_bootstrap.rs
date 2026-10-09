//! Initial contexts for the shared source host, before entity attachment.
//! Resident materials replace the initial decoder fields with admitted modes.
//! No registration, reveal, LOD selection or GPU qualification occurs here.
use super::source_graph_context::{HostFrame, ObjectBasis};
use bevy::prelude::*;
use forge_assets::source_graph_glb::SourceGraphGlb;
use forge_engine::{
    source_graph::SourceGraphUniforms,
    source_graph_scene_geometry::{ScenePrimitiveRoute, SourceGraphSceneGeometry},
};
use std::collections::BTreeMap;

pub(super) fn initial_contexts(
    glb: &SourceGraphGlb<'_>,
    scene: &SourceGraphSceneGeometry,
    target_world_from_root: Mat4,
    frame: &HostFrame,
) -> Result<BTreeMap<(usize, usize), SourceGraphUniforms>, String> {
    let basis = ObjectBasis::from_document(glb.document())?
        .ok_or("Declared object mapping required for initial source frames")?;
    // The scene reader already admits parent-first, original local transforms.
    // Reject changed order/identity instead of silently using an identity parent.
    let mut globals: BTreeMap<usize, Mat4> = BTreeMap::new();
    for node in &scene.hierarchy.nodes {
        if globals.contains_key(&node.source_node) {
            return Err("Duplicate node in initial source hierarchy".into());
        }
        let parent = match node.parent_source_node {
            Some(index) => *globals
                .get(&index)
                .ok_or("Initial source parent absent or out of order")?,
            None => target_world_from_root,
        };
        let declared = node.local_matrix;
        let composed = parent * declared;
        // Check through the same frame builder used after transform propagation.
        // Decoders are admitted by source_graph_resources, never chosen here.
        frame.uniform(basis, composed, [Vec4::ZERO; 2])?;
        globals.insert(node.source_node, composed);
    }
    let mut contexts = BTreeMap::new();
    for primitive in &scene.instances {
        if let ScenePrimitiveRoute::SourceGraph { material } = primitive.route {
            let pose = *globals
                .get(&primitive.source_node)
                .ok_or("Initial draw node absent")?;
            contexts
                .entry((primitive.source_node, material))
                .or_insert(frame.uniform(basis, pose, [Vec4::ZERO; 2])?);
        }
    }
    if contexts.is_empty() {
        return Err("No source material instances in initial scene".into());
    }
    Ok(contexts)
}
