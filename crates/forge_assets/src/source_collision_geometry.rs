//! Decode preserved collision streams independently of visible render meshes.
//! Coordinates are already in the carrier's declared target local frame. This
//! reader neither transforms nor welds vertices, cooks shapes, or admits physics.
use crate::source_graph_glb::SourceGraphGlb;
use forge_core::{ContentHash, bytes_sha256};
use serde_json::Value;
use std::collections::{BTreeMap, BTreeSet};

const MAX_RECORDS: usize = 4096;
const MAX_STREAM_BYTES: usize = 256 * 1024 * 1024;

#[derive(Debug)]
pub struct SourceCollisionGeometry {
    pub node_path: String,
    pub mesh_guid: String,
    pub mesh_file_id: String,
    pub positions: Vec<[f32; 3]>,
    pub normals: Option<Vec<[f32; 3]>>,
    /// Extra authored streams retain packed values and their declaration.
    pub auxiliary_attributes: BTreeMap<String, Vec<f32>>,
    /// Separate source submeshes retain their authored ordering and topology.
    pub triangles: Vec<Vec<[u32; 3]>>,
    pub original: Value,
}

fn text<'a>(value: &'a Value, field: &str) -> Result<&'a str, String> {
    value
        .get(field)
        .and_then(Value::as_str)
        .filter(|s| !s.is_empty())
        .ok_or_else(|| format!("missing collision {field}"))
}

fn count(value: &Value, field: &str) -> Result<usize, String> {
    value
        .get(field)
        .and_then(Value::as_u64)
        .and_then(|n| usize::try_from(n).ok())
        .ok_or_else(|| format!("invalid collision {field}"))
}

fn stream<'a>(
    reference: &Value,
    views: &[&'a [u8]],
    total: &mut usize,
) -> Result<&'a [u8], String> {
    let bytes = *views
        .get(count(reference, "buffer_view")?)
        .ok_or("collision view outside carrier")?;
    *total = total
        .checked_add(bytes.len())
        .ok_or("collision stream budget overflow")?;
    if *total > MAX_STREAM_BYTES {
        return Err("collision streams exceed decoded byte budget".into());
    }
    let expected = ContentHash::parse(text(reference, "sha256")?).map_err(|e| e.to_string())?;
    if bytes_sha256(bytes) != expected {
        return Err("collision stream SHA-256 differs".into());
    }
    Ok(bytes)
}

fn vectors(bytes: &[u8]) -> Result<Vec<[f32; 3]>, String> {
    if bytes.is_empty() || !bytes.len().is_multiple_of(12) {
        return Err("collision vector stream must contain packed f32 triples".into());
    }
    bytes
        .as_chunks::<12>()
        .0
        .iter()
        .map(|b| {
            let v = [
                f32::from_le_bytes(b[0..4].try_into().map_err(|_| "collision x")?),
                f32::from_le_bytes(b[4..8].try_into().map_err(|_| "collision y")?),
                f32::from_le_bytes(b[8..12].try_into().map_err(|_| "collision z")?),
            ];
            if v.iter().all(|n| n.is_finite()) {
                Ok(v)
            } else {
                Err("nonfinite collision vector".into())
            }
        })
        .collect()
}

/// Absence means no preserved mesh geometry, not absence of `BoxColliders` or
/// unassigned `MeshCollider` observations. Callers must bind native components
/// explicitly; successful decoding never authorizes a source-cooking fallback.
pub fn read_source_collision_geometry(
    glb: &SourceGraphGlb<'_>,
) -> Result<Vec<SourceCollisionGeometry>, String> {
    decode(glb.document(), &glb.views)
}

fn decode(document: &Value, views: &[&[u8]]) -> Result<Vec<SourceCollisionGeometry>, String> {
    let Some(records) = document.pointer("/extras/preserved_collider_buffers") else {
        return Ok(Vec::new());
    };
    let records = records
        .as_array()
        .ok_or("collision records must be an array")?;
    if records.len() > MAX_RECORDS {
        return Err("too many preserved collision records".into());
    }
    let nodes = document
        .get("nodes")
        .and_then(Value::as_array)
        .ok_or("collision owner nodes missing")?;
    let mut paths = BTreeSet::new();
    for node in nodes {
        if let Some(path) = node.pointer("/extras/unity_source_node/path") {
            let path = path
                .as_str()
                .filter(|p| !p.is_empty())
                .ok_or("invalid collision owner path")?;
            if !paths.insert(path) {
                return Err("duplicate collision owner path".into());
            }
        }
    }
    let mut identities = BTreeSet::new();
    let mut total = 0;
    // Validate all stream sizes and hashes before allocating decoded arrays.
    for record in records {
        let path = text(record, "node_path")?;
        let identity = record.get("identity").ok_or("collision identity missing")?;
        let guid = text(identity, "guid")?;
        let file_id = text(identity, "file_id")?;
        if guid.len() != 32
            || !guid.bytes().all(|b| b.is_ascii_hexdigit())
            || file_id.parse::<i64>().map_or(true, |n| n == 0)
            || text(identity, "type")? != "UnityEngine.Mesh"
        {
            return Err("invalid preserved mesh identity".into());
        }
        if !paths.contains(path)
            || !identities.insert((
                path,
                guid.to_ascii_lowercase(),
                file_id.parse::<i64>().map_err(|e| e.to_string())?,
            ))
        {
            return Err("missing owner or duplicate collision mesh binding".into());
        }
        let attrs = record
            .get("attributes")
            .and_then(Value::as_object)
            .ok_or("collision attributes missing")?;
        if attrs.keys().any(|name| {
            !matches!(
                name.as_str(),
                "POSITION" | "NORMAL" | "TANGENT" | "TEXCOORD_0" | "TEXCOORD_1"
            )
        }) {
            return Err("unsupported preserved collision attribute".into());
        }
        stream(
            attrs.get("POSITION").ok_or("collision POSITION missing")?,
            views,
            &mut total,
        )?;
        for (name, reference) in attrs {
            if name != "POSITION" {
                stream(reference, views, &mut total)?;
            }
        }
        let primitives = record
            .get("primitives")
            .and_then(Value::as_array)
            .ok_or("collision primitives missing")?;
        let indices = record
            .get("index_buffers")
            .and_then(Value::as_array)
            .ok_or("collision index buffers missing")?;
        if primitives.is_empty() || primitives.len() != indices.len() {
            return Err("collision primitive/index cardinality differs".into());
        }
        for index in indices {
            stream(index, views, &mut total)?;
        }
    }
    records
        .iter()
        .map(|record| decode_record(record, views))
        .collect()
}

fn auxiliary(
    record: &Value,
    views: &[&[u8]],
    vertices: usize,
) -> Result<BTreeMap<String, Vec<f32>>, String> {
    let mut result = BTreeMap::new();
    for (name, width) in [("TANGENT", 4), ("TEXCOORD_0", 2), ("TEXCOORD_1", 2)] {
        let Some(reference) = record["attributes"].get(name) else {
            continue;
        };
        let bytes = views
            .get(count(reference, "buffer_view")?)
            .ok_or("collision auxiliary view missing")?;
        if vertices.checked_mul(width).and_then(|n| n.checked_mul(4)) != Some(bytes.len()) {
            return Err("collision auxiliary cardinality differs".into());
        }
        let values: Vec<_> = bytes
            .as_chunks::<4>()
            .0
            .iter()
            .map(|b| f32::from_le_bytes(*b))
            .collect();
        if values.iter().any(|v| !v.is_finite()) {
            return Err("nonfinite collision auxiliary value".into());
        }
        result.insert(name.into(), values);
    }
    Ok(result)
}

fn decode_record(record: &Value, views: &[&[u8]]) -> Result<SourceCollisionGeometry, String> {
    // References have already passed bounds and hash validation above.
    let view = |reference: &Value| -> Result<&[u8], String> {
        views
            .get(count(reference, "buffer_view")?)
            .copied()
            .ok_or_else(|| "collision view outside carrier".into())
    };
    let positions = vectors(view(&record["attributes"]["POSITION"])?)?;
    let normals = record["attributes"]
        .get("NORMAL")
        .map(|n| vectors(view(n)?))
        .transpose()?;
    if normals.as_ref().is_some_and(|n| n.len() != positions.len()) {
        return Err("collision normal cardinality differs".into());
    }
    let primitives = record["primitives"]
        .as_array()
        .ok_or("collision primitives missing")?;
    let indices = record["index_buffers"]
        .as_array()
        .ok_or("collision index buffers missing")?;
    let mut triangles = Vec::with_capacity(primitives.len());
    for (primitive, reference) in primitives.iter().zip(indices) {
        let bytes = view(reference)?;
        if bytes.is_empty()
            || !bytes.len().is_multiple_of(12)
            || count(primitive, "indices")? != bytes.len() / 4
            || count(primitive, "triangles")? != bytes.len() / 12
            || primitive
                .pointer("/source_submesh/topology")
                .and_then(Value::as_str)
                != Some("Triangles")
        {
            return Err("preserved collision triangle layout differs".into());
        }
        let mut submesh = Vec::with_capacity(bytes.len() / 12);
        for b in bytes.as_chunks::<12>().0 {
            let mut triangle = [0; 3];
            for (i, word) in b.as_chunks::<4>().0.iter().enumerate() {
                triangle[i] = u32::from_le_bytes(*word);
                if usize::try_from(triangle[i]).map_or(true, |n| n >= positions.len()) {
                    return Err("collision index outside original vertex domain".into());
                }
            }
            submesh.push(triangle);
        }
        triangles.push(submesh);
    }
    Ok(SourceCollisionGeometry {
        node_path: text(record, "node_path")?.into(),
        mesh_guid: text(&record["identity"], "guid")?.into(),
        mesh_file_id: text(&record["identity"], "file_id")?.into(),
        auxiliary_attributes: auxiliary(record, views, positions.len())?,
        positions,
        normals,
        triangles,
        original: record.clone(),
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::json;

    fn fixture() -> (Value, Vec<u8>, Vec<u8>) {
        let positions: Vec<_> = [0.0_f32, 0.0, -2.0, 1.0, 0.0, -2.0, 0.0, 1.0, -2.0]
            .into_iter()
            .flat_map(f32::to_le_bytes)
            .collect();
        let indices: Vec<_> = [0_u32, 2, 1]
            .into_iter()
            .flat_map(u32::to_le_bytes)
            .collect();
        let doc = json!({"nodes":[{"extras":{"unity_source_node":{"path":"root"}}}], "extras":{"preserved_collider_buffers":[{
            "node_path":"root","identity":{"guid":"0123456789abcdef0123456789abcdef","file_id":"4300000","type":"UnityEngine.Mesh"},
            "attributes":{"POSITION":{"buffer_view":0,"sha256":bytes_sha256(&positions)}},
            "primitives":[{"indices":3,"triangles":1,"source_submesh":{"topology":"Triangles"}}],
            "index_buffers":[{"buffer_view":1,"sha256":bytes_sha256(&indices)}],"actual_runtime_admission":false
        }]}});
        (doc, positions, indices)
    }

    #[test]
    fn preserves_target_frame_and_authored_winding_without_admission() {
        let (d, p, i) = fixture();
        let rows = decode(&d, &[&p, &i]).unwrap();
        // Decoding preserves the original f32 payload, including signed zero.
        assert_eq!(
            rows[0].positions[0].map(f32::to_bits),
            [0.0_f32, 0.0, -2.0].map(f32::to_bits)
        );
        assert_eq!(rows[0].triangles[0], [[0, 2, 1]]);
        assert!(rows[0].normals.is_none());
        assert_eq!(rows[0].original["actual_runtime_admission"], false);
    }

    #[test]
    fn retains_and_checks_auxiliary_streams() {
        let (mut d, p, i) = fixture();
        let uv: Vec<_> = [0.0_f32, 1.0, 2.0, 3.0, 4.0, 5.0]
            .into_iter()
            .flat_map(f32::to_le_bytes)
            .collect();
        d["extras"]["preserved_collider_buffers"][0]["attributes"]["TEXCOORD_1"] =
            json!({"buffer_view":2,"sha256":bytes_sha256(&uv)});
        let rows = decode(&d, &[&p, &i, &uv]).unwrap();
        assert_eq!(
            rows[0].auxiliary_attributes["TEXCOORD_1"],
            [0.0, 1.0, 2.0, 3.0, 4.0, 5.0]
        );
        let short = &uv[..16];
        d["extras"]["preserved_collider_buffers"][0]["attributes"]["TEXCOORD_1"]["sha256"] =
            json!(bytes_sha256(short));
        assert!(
            decode(&d, &[&p, &i, short])
                .unwrap_err()
                .contains("cardinality")
        );
        let mut bad = uv;
        bad[..4].copy_from_slice(&f32::INFINITY.to_le_bytes());
        d["extras"]["preserved_collider_buffers"][0]["attributes"]["TEXCOORD_1"]["sha256"] =
            json!(bytes_sha256(&bad));
        assert!(
            decode(&d, &[&p, &i, &bad])
                .unwrap_err()
                .contains("nonfinite")
        );
    }

    #[test]
    fn refuses_corruption_and_invalid_rehashed_geometry() {
        let (d, p, i) = fixture();
        let mut corrupt = p.clone();
        corrupt[0] ^= 1;
        assert!(decode(&d, &[&corrupt, &i]).unwrap_err().contains("SHA-256"));
        let mut d2 = d.clone();
        let mut bad_index = i.clone();
        bad_index[..4].copy_from_slice(&3_u32.to_le_bytes());
        d2["extras"]["preserved_collider_buffers"][0]["index_buffers"][0]["sha256"] =
            json!(bytes_sha256(&bad_index));
        assert!(
            decode(&d2, &[&p, &bad_index])
                .unwrap_err()
                .contains("vertex domain")
        );
        let mut bad_position = p.clone();
        bad_position[..4].copy_from_slice(&f32::NAN.to_le_bytes());
        d2 = d.clone();
        d2["extras"]["preserved_collider_buffers"][0]["attributes"]["POSITION"]["sha256"] =
            json!(bytes_sha256(&bad_position));
        assert!(
            decode(&d2, &[&bad_position, &i])
                .unwrap_err()
                .contains("nonfinite")
        );
        d2 = d.clone();
        d2["extras"]["preserved_collider_buffers"][0]["node_path"] = json!("other");
        assert!(decode(&d2, &[&p, &i]).unwrap_err().contains("owner"));
        d2 = d.clone();
        let duplicate = d2["extras"]["preserved_collider_buffers"][0].clone();
        d2["extras"]["preserved_collider_buffers"]
            .as_array_mut()
            .unwrap()
            .push(duplicate);
        assert!(decode(&d2, &[&p, &i]).unwrap_err().contains("duplicate"));
        d2 = d;
        d2["extras"]["preserved_collider_buffers"][0]["primitives"][0]["source_submesh"]["topology"] =
            json!("Lines");
        assert!(decode(&d2, &[&p, &i]).unwrap_err().contains("layout"));
    }
}
