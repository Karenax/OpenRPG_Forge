//! Candidate for the common scene host: preserve every primitive instance and
//! decode each shared source geometry once. This does not register GPU assets.
use crate::source_graph_scene::{SourceGraphScenePlan, read_source_graph_scene};
use forge_assets::{
    source_graph_attributes::{SourceGraphPrimitiveGeometry, read_source_graph_primitive_geometry},
    source_graph_glb::SourceGraphGlb,
};
use serde_json::Value;
use std::collections::{BTreeMap, BTreeSet};

#[derive(Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord)]
pub struct SourcePrimitiveKey {
    pub mesh: usize,
    pub primitive: usize,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum ScenePrimitiveRoute {
    SourceGraph {
        material: usize,
    },
    /// Ordinary primitives remain the original glTF host's responsibility.
    /// This route is never a fallback for a declared source graph.
    HostGltf {
        material: Option<usize>,
    },
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct SourceScenePrimitive {
    pub source_node: usize,
    pub geometry: SourcePrimitiveKey,
    pub route: ScenePrimitiveRoute,
}

pub struct SourceGraphSceneGeometry {
    pub hierarchy: SourceGraphScenePlan,
    pub instances: Vec<SourceScenePrimitive>,
    pub source_geometries: BTreeMap<SourcePrimitiveKey, SourceGraphPrimitiveGeometry>,
}

impl SourceGraphSceneGeometry {
    /// The current GPU ABI requires an original tangent attribute. Source
    /// absence remains absence; a native-default adapter needs its own proof.
    pub fn absent_original_tangents(&self) -> impl Iterator<Item = SourcePrimitiveKey> + '_ {
        self.source_geometries.iter().filter_map(|(key, geometry)| {
            geometry.source.original_tangents.is_none().then_some(*key)
        })
    }
}

fn unsigned(value: &Value) -> Result<usize, String> {
    value
        .as_u64()
        .and_then(|v| usize::try_from(v).ok())
        .ok_or_else(|| "primitive material index must be an unsigned integer".into())
}

/// Prepare the complete selected static scene before changing host entities or
/// assets. Root placement, transforms, ordinary primitives and material indices
/// stay explicit. No name, pack, module, PBR substitute or texture is inferred.
pub fn prepare_source_graph_scene_geometry(
    glb: &SourceGraphGlb<'_>,
    selected_scene: Option<usize>,
) -> Result<SourceGraphSceneGeometry, String> {
    let hierarchy = read_source_graph_scene(glb, selected_scene)?;
    let document = glb.document();
    let meshes = document.get("meshes").and_then(Value::as_array);
    let materials = document
        .get("materials")
        .and_then(Value::as_array)
        .ok_or("source scene materials missing")?;
    let source_materials: BTreeSet<_> = glb.materials.iter().map(|(index, _)| *index).collect();
    if source_materials.len() != glb.materials.len() {
        return Err("duplicate source material identity".into());
    }
    let mut instances = Vec::new();
    let mut source_geometries = BTreeMap::new();
    let mut decoded_bytes = 0usize;
    for node in &hierarchy.nodes {
        let Some(mesh_index) = node.mesh else {
            continue;
        };
        let primitives = meshes
            .and_then(|m| m.get(mesh_index))
            .and_then(|m| m.get("primitives"))
            .and_then(Value::as_array)
            .ok_or("source scene primitives missing")?;
        for (primitive_index, primitive) in primitives.iter().enumerate() {
            if !primitive.is_object() {
                return Err("source primitive is not an object".into());
            }
            if instances.len() >= 100_000 {
                return Err("source scene exceeds primitive instance budget".into());
            }
            let material = primitive.get("material").map(unsigned).transpose()?;
            if material.is_some_and(|index| index >= materials.len()) {
                return Err("source scene primitive material is out of range".into());
            }
            let geometry = SourcePrimitiveKey {
                mesh: mesh_index,
                primitive: primitive_index,
            };
            let route = if let Some(material) =
                material.filter(|index| source_materials.contains(index))
            {
                if let std::collections::btree_map::Entry::Vacant(slot) =
                    source_geometries.entry(geometry)
                {
                    let decoded =
                        read_source_graph_primitive_geometry(glb, mesh_index, primitive_index)?;
                    let contract = &glb
                        .materials
                        .iter()
                        .find(|(index, _)| *index == material)
                        .ok_or("source material identity missing")?
                        .1;
                    if contract.requires_original_vertex_color
                        && decoded.source.original_colors.is_none()
                    {
                        return Err("source program requires absent original vertex color".into());
                    }
                    // Count all retained streams across shared geometries, before
                    // keeping another decoded mesh. Index arithmetic is bounded.
                    let mut bytes = decoded
                        .positions
                        .len()
                        .checked_mul(12)
                        .and_then(|n| n.checked_add(decoded.normals.len() * 12))
                        .and_then(|n| n.checked_add(decoded.uv0.len() * 8))
                        .and_then(|n| n.checked_add(decoded.source.original_normals.len() * 12))
                        .and_then(|n| n.checked_add(decoded.source.original_uv0.len() * 16))
                        .and_then(|n| n.checked_add(decoded.indices.len() * 4))
                        .ok_or("source geometry byte count overflows")?;
                    for values in [
                        &decoded.tangents,
                        &decoded.source.original_tangents,
                        &decoded.source.original_colors,
                    ] {
                        bytes = bytes
                            .checked_add(values.as_ref().map_or(0, |v| v.len() * 16))
                            .ok_or("source geometry byte count overflows")?;
                    }
                    for values in decoded.additional_uvs.iter().flatten() {
                        bytes = bytes
                            .checked_add(values.len() * 8)
                            .ok_or("source geometry byte count overflows")?;
                    }
                    for values in decoded.source.original_additional_uvs.iter().flatten() {
                        bytes = bytes
                            .checked_add(values.len() * 16)
                            .ok_or("source geometry byte count overflows")?;
                    }
                    decoded_bytes = decoded_bytes
                        .checked_add(bytes)
                        .ok_or("source scene geometry byte count overflows")?;
                    if decoded_bytes > 512 * 1024 * 1024 {
                        return Err(
                            "source scene decoded geometry exceeds allocation budget".into()
                        );
                    }
                    slot.insert(decoded);
                }
                ScenePrimitiveRoute::SourceGraph { material }
            } else {
                ScenePrimitiveRoute::HostGltf { material }
            };
            instances.push(SourceScenePrimitive {
                source_node: node.source_node,
                geometry,
                route,
            });
        }
    }
    if instances.is_empty() {
        return Err("selected source scene contains no mesh primitives".into());
    }
    Ok(SourceGraphSceneGeometry {
        hierarchy,
        instances,
        source_geometries,
    })
}
