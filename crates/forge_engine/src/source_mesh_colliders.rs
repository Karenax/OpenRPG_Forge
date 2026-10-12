//! Explicit source mesh binding and backend shape preparation. No implicit
//! convex cooking fallback, simulation admission, or native equivalence verdict.
use crate::source_colliders::{boolean, identity};
use bevy::prelude::Vec3;
use bevy_rapier3d::prelude::Collider;
use forge_assets::source_collision_geometry::{
    SourceCollisionGeometry, read_source_collision_geometry,
};
use forge_assets::source_graph_glb::SourceGraphGlb;
use forge_core::{ContentHash, bytes_sha256};
use serde_json::Value;
use std::collections::{BTreeMap, BTreeSet};

pub enum MeshShapeInput {
    /// Use the original target-frame triangles, with no convex substitution.
    SourceTriangles,
    /// Separately pinned target-frame points derived from native plane probes.
    /// Their query residual and incomplete equivalence remain caller evidence.
    NativePlaneCandidate {
        positions: Vec<[f32; 3]>,
        sha256: ContentHash,
    },
}

pub struct MeshCookingPolicy {
    pub mesh_guid: String,
    pub mesh_file_id: String,
    pub source_cooking_options: u32,
    pub evidence: String,
    pub shape: MeshShapeInput,
}

pub struct PreparedMeshCollider {
    pub node: usize,
    pub guid: String,
    pub file_id: String,
    pub enabled: bool,
    pub is_trigger: bool,
    pub source_convex: bool,
    pub source_cooking_options: u32,
    /// None retains an explicitly empty source mesh reference, not a failed cook.
    pub collider: Option<Collider>,
    pub geometry_evidence: Option<String>,
    pub original: Value,
}

fn properties(original: &Value) -> Result<BTreeMap<&str, &Value>, String> {
    let mut result = BTreeMap::new();
    for property in original["serialized"]
        .as_array()
        .ok_or("mesh collider properties missing")?
    {
        let key = property["property_path"]
            .as_str()
            .ok_or("mesh property path missing")?;
        if result.insert(key, property).is_some() {
            return Err("duplicate mesh collider property".into());
        }
    }
    Ok(result)
}

fn options(property: &Value) -> Result<u32, String> {
    let number = property["number"]
        .as_f64()
        .ok_or("mesh cooking options missing")?;
    if property["property_type"] != "Integer"
        || number.fract() != 0.0
        || !(0.0..=f64::from(u32::MAX)).contains(&number)
    {
        return Err("invalid source mesh cooking options".into());
    }
    // Validate the complete integer range before crossing the native ABI.
    #[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
    Ok(number as u32)
}

fn shape(
    geometry: &SourceCollisionGeometry,
    convex: bool,
    policy: &MeshCookingPolicy,
) -> Result<Collider, String> {
    match &policy.shape {
        MeshShapeInput::SourceTriangles if !convex => {
            let vertices = geometry
                .positions
                .iter()
                .copied()
                .map(Vec3::from_array)
                .collect();
            let indices = geometry.triangles.iter().flatten().copied().collect();
            Collider::trimesh(vertices, indices)
                .map_err(|e| format!("source triangle collider refused: {e}"))
        }
        MeshShapeInput::NativePlaneCandidate { positions, sha256 } if convex => {
            if positions.len() < 4
                || positions.len() > 65_536
                || positions.iter().flatten().any(|n| !n.is_finite())
            {
                return Err("invalid native-plane candidate vertices".into());
            }
            let raw: Vec<_> = positions
                .iter()
                .flatten()
                .flat_map(|n| n.to_le_bytes())
                .collect();
            if bytes_sha256(&raw) != *sha256 {
                return Err("native-plane candidate SHA-256 differs".into());
            }
            let vertices: Vec<_> = positions.iter().copied().map(Vec3::from_array).collect();
            Collider::convex_hull(&vertices)
                .ok_or_else(|| "native-plane candidate convex hull refused".into())
        }
        _ => Err("mesh shape policy differs from native convex flag".into()),
    }
}

fn observation(
    node: usize,
    source: &Value,
    original: &Value,
) -> Result<(PreparedMeshCollider, Value), String> {
    let (guid, file_id) = identity(&original["identity"])?;
    if source["gameobject"]["guid"].as_str() != Some(guid.as_str()) {
        return Err("mesh collider GUID differs from prefab owner".into());
    }
    let properties = properties(original)?;
    let property = |key| {
        properties
            .get(key)
            .copied()
            .ok_or_else(|| format!("mesh collider property missing: {key}"))
    };
    let owner = property("m_GameObject")?;
    if owner["property_type"] != "ObjectReference"
        || identity(&owner["reference"])? != identity(&source["gameobject"])?
    {
        return Err("mesh collider owner differs from source node".into());
    }
    let mesh = property("m_Mesh")?;
    if mesh["property_type"] != "ObjectReference" {
        return Err("mesh collider mesh reference type differs".into());
    }
    Ok((
        PreparedMeshCollider {
            node,
            guid,
            file_id,
            enabled: boolean(property("m_Enabled")?)?
                && source["active_source_parent_chain"]
                    .as_bool()
                    .ok_or("mesh owner activation missing")?,
            is_trigger: boolean(property("m_IsTrigger")?)?,
            source_convex: boolean(property("m_Convex")?)?,
            source_cooking_options: options(property("m_CookingOptions")?)?,
            collider: None,
            geometry_evidence: None,
            original: original.clone(),
        },
        mesh["reference"].clone(),
    ))
}

/// Prepare all mesh observations, including explicit null references, before
/// changing a physics World. Box observations belong to the strict box adapter;
/// every other source collider type is refused. Policies must match exactly.
pub fn prepare(
    glb: &SourceGraphGlb<'_>,
    policies: &BTreeMap<(String, String), MeshCookingPolicy>,
) -> Result<Vec<PreparedMeshCollider>, String> {
    let geometries = read_source_collision_geometry(glb)?;
    let mut used_geometry = BTreeSet::new();
    let mut used_policies = BTreeSet::new();
    let mut identities = BTreeSet::new();
    let mut result = Vec::new();
    for (node, value) in glb.document()["nodes"]
        .as_array()
        .ok_or("mesh collider nodes missing")?
        .iter()
        .enumerate()
    {
        let source = &value["extras"]["unity_source_node"];
        let Some(observations) = source["native_collider_observations"].as_array() else {
            if source["colliders"]
                .as_array()
                .is_some_and(|c| !c.is_empty())
            {
                return Err("authored collider lacks native observation".into());
            }
            continue;
        };
        for original in observations {
            let id = identity(&original["identity"])?;
            if !identities.insert(id) {
                return Err("duplicate source collider identity".into());
            }
            match original["identity"]["type"].as_str() {
                Some("UnityEngine.BoxCollider") => continue,
                Some("UnityEngine.MeshCollider") => {}
                _ => return Err("unsupported source collider adapter required".into()),
            }
            let (mut prepared, mesh) = observation(node, source, original)?;
            let key = (prepared.guid.clone(), prepared.file_id.clone());
            if ["guid", "file_id", "path", "type"]
                .iter()
                .all(|key| mesh[*key].as_str() == Some(""))
            {
                if policies.contains_key(&key) {
                    return Err("empty source mesh must not have a cooking policy".into());
                }
            } else {
                if mesh["type"] != "UnityEngine.Mesh" {
                    return Err("source collider mesh identity type differs".into());
                }
                let mesh_id = identity(&mesh)?;
                let path = source["path"]
                    .as_str()
                    .ok_or("mesh source node path missing")?;
                let (index, geometry) = geometries
                    .iter()
                    .enumerate()
                    .find(|(_, g)| {
                        g.node_path == path
                            && (g.mesh_guid.as_str(), g.mesh_file_id.as_str())
                                == (mesh_id.0.as_str(), mesh_id.1.as_str())
                    })
                    .ok_or("native mesh reference lacks its preserved geometry")?;
                let policy = policies
                    .get(&key)
                    .ok_or("explicit mesh cooking policy missing")?;
                if policy.evidence.trim().is_empty()
                    || (policy.mesh_guid.as_str(), policy.mesh_file_id.as_str())
                        != (mesh_id.0.as_str(), mesh_id.1.as_str())
                    || policy.source_cooking_options != prepared.source_cooking_options
                {
                    return Err("mesh cooking policy identity/options/evidence differs".into());
                }
                prepared.collider = Some(shape(geometry, prepared.source_convex, policy)?);
                prepared.geometry_evidence = Some(policy.evidence.clone());
                used_geometry.insert(index);
                used_policies.insert(key);
            }
            result.push(prepared);
        }
    }
    if used_geometry.len() != geometries.len() || used_policies.len() != policies.len() {
        return Err("orphaned collision geometry or cooking policy".into());
    }
    Ok(result)
}
