//! Verified transient physics evidence, independent of filesystem and backend.
//! A transport never admits contact policy or claims native shape equivalence.
use crate::source_graph_glb::SourceGraphGlb;
use forge_core::{ContentHash, bytes_sha256};
use serde_json::Value;
use std::collections::{BTreeMap, BTreeSet};

pub struct PhysicsTransportComponent {
    pub source_node: usize,
    pub original: Value,
    pub native_metrics: Option<Value>,
    pub candidate_positions: Option<Vec<[f32; 3]>>,
    pub candidate_sha256: Option<ContentHash>,
    pub evidence: Value,
}

fn text<'a>(v: &'a Value, key: &str) -> Result<&'a str, String> {
    v[key]
        .as_str()
        .filter(|s| !s.is_empty())
        .ok_or_else(|| format!("physics transport {key} absent"))
}

fn identity(v: &Value) -> Result<(String, String), String> {
    let guid = text(v, "guid")?;
    let file = text(v, "file_id")?;
    if guid.len() != 32
        || !guid.bytes().all(|b| b.is_ascii_hexdigit())
        || file.parse::<i64>().is_err()
    {
        return Err("physics transport identity invalid".into());
    }
    Ok((guid.into(), file.into()))
}

fn candidate(bytes: &[u8], expected: ContentHash) -> Result<Vec<[f32; 3]>, String> {
    if bytes.len() < 48
        || bytes.len() > 65536 * 12
        || !bytes.len().is_multiple_of(12)
        || bytes_sha256(bytes) != expected
    {
        return Err("physics candidate length or hash differs".into());
    }
    bytes
        .as_chunks::<12>()
        .0
        .iter()
        .map(|b| {
            let mut p = [0.; 3];
            for (i, x) in p.iter_mut().enumerate() {
                *x = f32::from_le_bytes(
                    b[i * 4..i * 4 + 4]
                        .try_into()
                        .map_err(|_| "candidate scalar")?,
                );
            }
            if p.iter().all(|v| v.is_finite()) {
                Ok(p)
            } else {
                Err("physics candidate nonfinite".into())
            }
        })
        .collect()
}

fn resolve_candidate(
    shape: &Value,
    remaining_bytes: &mut usize,
    resolve: &mut impl FnMut(&str) -> Result<Vec<u8>, String>,
) -> Result<(Vec<[f32; 3]>, ContentHash), String> {
    let path = text(shape, "relative_path")?;
    if path.contains(['\\', ':'])
        || path.starts_with('/')
        || path
            .split('/')
            .any(|p| p.is_empty() || p == "." || p == "..")
        || shape["coordinate_space"] != "target_local"
        || shape["native_shape_equivalence"] != false
    {
        return Err("physics candidate path or declaration invalid".into());
    }
    let expected = ContentHash::parse(text(shape, "sha256")?).map_err(|e| e.to_string())?;
    let bytes = resolve(path)?;
    *remaining_bytes = remaining_bytes
        .checked_sub(bytes.len())
        .ok_or("physics candidate batch budget exceeded")?;
    Ok((candidate(&bytes, expected)?, expected))
}

fn select_carrier(doc: &Value, carrier_hash: ContentHash) -> Result<&Value, String> {
    if doc["schema"] != "openrpg.source-physics-transport.v1" {
        return Err("physics manifest schema differs".into());
    }
    let rows = doc["rows"].as_array().ok_or("physics rows absent")?;
    if rows.len() > 4096 {
        return Err("physics row budget exceeded".into());
    }
    let mut selected = None;
    for row in rows {
        let hash = ContentHash::parse(text(row, "carrier_sha256")?).map_err(|e| e.to_string())?;
        if hash == carrier_hash && selected.replace(row).is_some() {
            return Err("duplicate physics carrier".into());
        }
    }
    selected.ok_or_else(|| "physics carrier absent".into())
}

/// The caller supplies a pinned manifest, the full carrier hash and a confined
/// byte resolver. Absolute source/evidence paths are retained but never opened.
/// All native observations must match; missing/extra entries refuse the batch.
pub fn read_source_physics_transport(
    manifest: &[u8],
    expected_manifest: ContentHash,
    carrier_hash: ContentHash,
    glb: &SourceGraphGlb<'_>,
    mut resolve: impl FnMut(&str) -> Result<Vec<u8>, String>,
) -> Result<Vec<PhysicsTransportComponent>, String> {
    if manifest.len() > 64 * 1024 * 1024 || bytes_sha256(manifest) != expected_manifest {
        return Err("physics manifest budget or hash differs".into());
    }
    let doc: Value = serde_json::from_slice(manifest).map_err(|e| e.to_string())?;
    let row = select_carrier(&doc, carrier_hash)?;
    let components = row["components"]
        .as_array()
        .ok_or("physics components absent")?;
    if components.len() > 4096 {
        return Err("physics component budget exceeded".into());
    }
    let mut originals = BTreeMap::new();
    for (index, node) in glb.document()["nodes"]
        .as_array()
        .ok_or("physics source nodes absent")?
        .iter()
        .enumerate()
    {
        if let Some(observations) =
            node.pointer("/extras/unity_source_node/native_collider_observations")
        {
            for original in observations
                .as_array()
                .ok_or("physics observations invalid")?
            {
                let key = identity(&original["identity"])?;
                if originals.insert(key, (index, node, original)).is_some() {
                    return Err("duplicate native collider".into());
                }
            }
        }
    }
    let mut seen = BTreeSet::new();
    let mut result = Vec::new();
    let mut remaining_bytes = 64usize * 1024 * 1024;
    for component in components {
        let binding = &component["source_binding"];
        let key = identity(&binding["source_component"])?;
        let (index, node, original) = originals
            .get(&key)
            .ok_or("transport collider absent from carrier")?;
        if binding["glb_sha256"] != row["carrier_sha256"]
            || !seen.insert(key.clone())
            || binding["node_index"].as_u64() != u64::try_from(*index).ok()
            || binding["node_path"] != node["extras"]["unity_source_node"]["path"]
            || binding["source_component"]["type"] != original["identity"]["type"]
            || binding["source_serialized_properties"] != original["serialized"]
            || component["backend_policy_qualified"] != false
        {
            return Err("physics transport source binding or admission differs".into());
        }
        let shape = &component["shape_transport"];
        let mut positions = None;
        let mut hash = None;
        match text(binding, "kind")? {
            "unassigned_mesh" if shape.is_null() && component["native_metrics"].is_null() => {}
            "box" | "assigned_mesh" => {
                let metrics = &component["native_metrics"];
                if metrics["source_guid"] != key.0 || metrics["source_file_id"] != key.1 {
                    return Err("physics metric identity differs".into());
                }
                match text(shape, "kind")? {
                    "native_plane_candidate" if binding["source_convex"] == true => {
                        let (points, expected) =
                            resolve_candidate(shape, &mut remaining_bytes, &mut resolve)?;
                        positions = Some(points);
                        hash = Some(expected);
                    }
                    "source_triangles"
                        if binding["kind"] == "assigned_mesh"
                            && binding["source_convex"] == false => {}
                    "native_box_observation" if binding["kind"] == "box" => {}
                    _ => return Err("physics shape differs from source collider".into()),
                }
            }
            _ => return Err("physics collider kind unsupported".into()),
        }
        result.push(PhysicsTransportComponent {
            source_node: *index,
            original: (*original).clone(),
            native_metrics: component
                .get("native_metrics")
                .filter(|v| !v.is_null())
                .cloned(),
            candidate_positions: positions,
            candidate_sha256: hash,
            evidence: component.clone(),
        });
    }
    if seen.len() != originals.len() {
        return Err("physics transport omitted native collider".into());
    }
    Ok(result)
}
