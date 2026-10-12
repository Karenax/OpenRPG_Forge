//! Complete box/mesh routing for a source placement. Native observations remain
//! unchanged; each shape adapter owns only its projection beneath source nodes.
use crate::{
    source_collider_projection::{self, ColliderProjection, ContactPolicy, validate_node},
    source_colliders,
    source_mesh_collider_projection::{self, MeshColliderProjection},
    source_mesh_colliders::{self, MeshCookingPolicy},
};
use bevy::prelude::{Entity, Mat4, World};
use forge_assets::source_graph_glb::SourceGraphGlb;
use serde_json::Value;
use std::collections::{BTreeMap, BTreeSet};

pub struct ColliderSceneProjection {
    boxes: ColliderProjection,
    meshes: MeshColliderProjection,
}

impl ColliderSceneProjection {
    #[must_use]
    pub fn observation_count(&self) -> usize {
        self.boxes.entities.len() + self.meshes.entities.len()
    }

    pub fn clear(self, world: &mut World) {
        self.meshes.clear(world);
        self.boxes.clear(world);
    }
}

fn box_document(document: &Value) -> Result<Value, String> {
    let mut result = document.clone();
    for node in result["nodes"]
        .as_array_mut()
        .ok_or("source collider scene nodes missing")?
    {
        if let Some(observations) =
            node.pointer_mut("/extras/unity_source_node/native_collider_observations")
        {
            let observations = observations
                .as_array_mut()
                .ok_or("native collider observations must be an array")?;
            observations.retain(|o| o["identity"]["type"] != "UnityEngine.MeshCollider");
        }
    }
    Ok(result)
}

/// Explicit dispatch of a complete scene. All geometry, policies, ownership and
/// supported shape types are checked before allocating any ECS children. No
/// asynchronous systems run between the two shape projections; rollback removes
/// only allocations made by this call if a projection nevertheless fails.
pub fn attach(
    world: &mut World,
    root: Entity,
    glb: &SourceGraphGlb<'_>,
    basis: Mat4,
    nodes: &BTreeMap<usize, Entity>,
    cooking: &BTreeMap<(String, String), MeshCookingPolicy>,
    contacts: &BTreeMap<(String, String), ContactPolicy>,
) -> Result<ColliderSceneProjection, String> {
    if !world.entities().contains(root)
        || source_collider_projection::owns_batch(world, root)
        || source_mesh_collider_projection::owns_batch(world, root)
    {
        return Err("source collider scene root missing or occupied".into());
    }
    // Mesh preparation refuses unknown collider types and duplicate native IDs,
    // including box IDs. Filtering below dispatches only that validated type.
    let meshes = source_mesh_colliders::prepare(glb, cooking)?;
    let box_doc = box_document(glb.document())?;
    let boxes = source_colliders::prepare(&box_doc, basis)?;
    let box_keys: BTreeSet<_> = boxes
        .iter()
        .map(|s| (s.guid.clone(), s.file_id.clone()))
        .collect();
    let mesh_keys: BTreeSet<_> = meshes
        .iter()
        .filter(|s| s.collider.is_some())
        .map(|s| (s.guid.clone(), s.file_id.clone()))
        .collect();
    if contacts.len() != box_keys.len() + mesh_keys.len() {
        return Err("contact policies differ from the complete source collider scene".into());
    }
    for key in box_keys.iter().chain(&mesh_keys) {
        contacts
            .get(key)
            .ok_or("source collider contact policy missing")?
            .validate()?;
    }
    for node in boxes
        .iter()
        .map(|s| s.node)
        .chain(meshes.iter().map(|s| s.node))
    {
        validate_node(
            world,
            root,
            node,
            *nodes
                .get(&node)
                .ok_or("source collider scene owner absent")?,
        )?;
    }
    let box_contacts = contacts
        .iter()
        .filter(|(id, _)| box_keys.contains(*id))
        .map(|(id, p)| (id.clone(), p.clone()))
        .collect();
    let mesh_contacts = contacts
        .iter()
        .filter(|(id, _)| mesh_keys.contains(*id))
        .map(|(id, p)| (id.clone(), p.clone()))
        .collect();
    let boxes =
        source_collider_projection::attach(world, root, &box_doc, basis, nodes, &box_contacts)?;
    match source_mesh_collider_projection::attach(world, root, glb, nodes, cooking, &mesh_contacts)
    {
        Ok(meshes) => Ok(ColliderSceneProjection { boxes, meshes }),
        Err(error) => {
            boxes.clear(world);
            Err(error)
        }
    }
}
