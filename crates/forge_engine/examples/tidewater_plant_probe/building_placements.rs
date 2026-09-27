//! Apply canonical instance transforms to the split village before both render passes.
use bevy::prelude::{Mat4, Quat, Vec3};
use forge_core::{AssetId, ContentHash, bytes_sha256};
use forge_project::zone_authoring::session::ZoneEditSession;
use serde::Deserialize;
use serde_json::Value;
use std::{collections::BTreeMap, error::Error};

#[derive(Deserialize)]
struct Catalog {
    schema_version: u32,
    assets: Vec<Record>,
}
#[derive(Deserialize)]
struct Record {
    id: AssetId,
    logical_name: String,
    content_hash: ContentHash,
}

pub fn apply(files: &mut BTreeMap<String, Vec<u8>>) -> Result<(), Box<dyn Error>> {
    let Some(bytes) = files.get("building-catalog.json") else {
        return Ok(());
    };
    let catalog: Catalog = serde_json::from_slice(bytes)?;
    if catalog.schema_version != 1 || catalog.assets.len() != 23 {
        return Err("Unsupported building catalog".into());
    }
    let zone = ZoneEditSession::from_archive_bytes(
        files
            .get("canonical-zone.json")
            .ok_or("Building catalog requires canonical zone")?,
    )?;
    let placements = zone
        .head()
        .corrections
        .placements
        .as_ref()
        .ok_or("Missing canonical building placements")?;
    let mut village: Value =
        serde_json::from_slice(files.get("village.json").ok_or("Missing split village")?)?;
    if village["version"] != 2 {
        return Err("Building placements require split village".into());
    }
    let mut bindings = BTreeMap::new();
    let mut ids = std::collections::BTreeSet::new();
    for record in catalog.assets {
        if !ids.insert(record.id) {
            return Err("Duplicate building asset ID".into());
        }
        let bytes = files
            .get(&format!("building-{}.json", record.content_hash))
            .ok_or("Missing building descriptor")?;
        if bytes_sha256(bytes) != record.content_hash {
            return Err("Building descriptor hash mismatch".into());
        }
        let descriptor: Value = serde_json::from_slice(bytes)?;
        if descriptor["schema"] != "forge.tidewater.building-asset"
            || descriptor["version"] != 1
            || descriptor["building"]["source_id"] != record.logical_name
        {
            return Err("Building descriptor identity mismatch".into());
        }
        let (t, q, s) = source_frame(&descriptor["building"]["source_frame"])?;
        let source = Mat4::from_scale_rotation_translation(s, q, t);
        let mut instances = Vec::new();
        for p in placements.iter().filter(|p| p.asset_id == record.id) {
            if p.asset_hash != record.content_hash {
                return Err("Stale building content pin".into());
            }
            let a = p.transform;
            let pt = Vec3::new(a.translation_m.x, a.translation_m.y, a.translation_m.z);
            let pq = Quat::from_xyzw(a.rotation.x, a.rotation.y, a.rotation.z, a.rotation.w);
            let ps = Vec3::new(a.scale.x, a.scale.y, a.scale.z);
            // Avoid inverse/multiply round-off at the exact original authored pose.
            let delta = if pt == t && pq == q && ps == s {
                Mat4::IDENTITY
            } else {
                Mat4::from_scale_rotation_translation(ps, pq, pt) * source.inverse()
            };
            instances.push((p.id, delta));
        }
        if bindings.insert(record.logical_name, instances).is_some() {
            return Err("Duplicate building source key".into());
        }
    }
    let mut nodes = Vec::new();
    let mut encountered = std::collections::BTreeSet::new();
    for node in village["nodes"].as_array().ok_or("Missing village nodes")? {
        let Some(key) = node["building_source_id"].as_str() else {
            nodes.push(node.clone());
            continue;
        };
        encountered.insert(key.to_owned());
        let instances = bindings.get(key).ok_or("Unbound split building")?;
        let original = Mat4::from_cols_array(&serde_json::from_value::<[f32; 16]>(
            node["matrix_world"].clone(),
        )?);
        for (id, delta) in instances {
            let mut node = node.clone();
            if *delta != Mat4::IDENTITY {
                node["matrix_world"] = serde_json::to_value((*delta * original).to_cols_array())?;
            }
            node["entity_id"] = serde_json::to_value(id)?;
            nodes.push(node);
        }
    }
    if encountered.len() != bindings.len() || nodes.len() > 2048 {
        return Err("Incomplete or oversized building scene".into());
    }
    village["nodes"] = serde_json::to_value(nodes)?;
    files.insert("village.json".into(), serde_json::to_vec(&village)?);
    Ok(())
}

fn source_frame(frame: &Value) -> Result<(Vec3, Quat, Vec3), Box<dyn Error>> {
    let t = Vec3::from_array(serde_json::from_value::<[f32; 3]>(
        frame["translation_m"].clone(),
    )?);
    let q = Quat::from_array(serde_json::from_value::<[f32; 4]>(
        frame["rotation_xyzw"].clone(),
    )?);
    let s = Vec3::from_array(serde_json::from_value::<[f32; 3]>(frame["scale"].clone())?);
    if !t.is_finite()
        || !q.is_finite()
        || !q.is_normalized()
        || !s.is_finite()
        || s.min_element() <= 0.0
    {
        return Err("Invalid building source transform".into());
    }
    Ok((t, q, s))
}
