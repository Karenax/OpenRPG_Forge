//! Replace building payloads with the explicitly pinned portable package.
use forge_assets::tidewater_package::BuildingPackage;
use forge_core::{ContentHash, bytes_sha256};
use serde_json::{Value, json};
use std::{
    collections::{BTreeMap, BTreeSet},
    error::Error,
    fs,
    io::Read,
    path::Path,
};

pub fn load(
    root: &Path,
    hash: ContentHash,
    files: &mut BTreeMap<String, Vec<u8>>,
) -> Result<(), Box<dyn Error>> {
    let root = root.canonicalize()?;
    let mut manifest = Vec::new();
    fs::File::open(root.join("package.json"))?
        .take(2 * 1024 * 1024 + 1)
        .read_to_end(&mut manifest)?;
    let package = BuildingPackage::load(&manifest, hash, |name, limit| {
        let path = root.join(name).canonicalize().map_err(|e| e.to_string())?;
        if path.parent() != Some(root.as_path()) {
            return Err("Escaping building package".into());
        }
        let mut bytes = Vec::new();
        fs::File::open(path)
            .map_err(|e| e.to_string())?
            .take(limit as u64 + 1)
            .read_to_end(&mut bytes)
            .map_err(|e| e.to_string())?;
        Ok(bytes)
    })?;
    replace(&package, files)
}

fn replace(
    package: &BuildingPackage,
    files: &mut BTreeMap<String, Vec<u8>>,
) -> Result<(), Box<dyn Error>> {
    // Validate the complete GPU-facing library even before uploading resources.
    let _ = forge_engine::tidewater_assets::PreparedLibrary::load(package)?;
    let mut scene: Value = serde_json::from_slice(
        files
            .get("village.json")
            .ok_or("Missing residual village")?,
    )?;
    if scene["version"] != 2 {
        return Err("Portable buildings require split village".into());
    }
    let mut nodes = Vec::new();
    let mut emitted = BTreeSet::new();
    for node in scene["nodes"].as_array().ok_or("Missing village nodes")? {
        let Some(key) = node["building_source_id"].as_str() else {
            nodes.push(node.clone());
            continue;
        };
        if !emitted.insert(key.to_owned()) {
            continue;
        }
        let record = package
            .catalog()
            .assets
            .iter()
            .find(|r| r.logical_name == key)
            .ok_or("Building absent from package")?;
        let filename = format!("{}.json", record.content_hash);
        let bytes = package.file(&filename).ok_or("Missing descriptor")?;
        files.insert(format!("building-{filename}"), bytes.to_vec());
        let descriptor: Value = serde_json::from_slice(bytes)?;
        nodes.extend(package_nodes(package, files, &descriptor, key)?);
    }
    if emitted.len() != 23 {
        return Err("Incomplete package building scene".into());
    }
    for node in &mut nodes {
        if let Some(geometry) = node
            .as_object_mut()
            .ok_or("Invalid node")?
            .remove("package_geometry")
        {
            let key = node["geometry"]
                .as_str()
                .ok_or("Missing geometry identity")?
                .to_owned();
            scene["geometries"][key] = geometry;
        }
    }
    let used: BTreeSet<_> = nodes
        .iter()
        .filter_map(|n| n["geometry"].as_str())
        .map(str::to_owned)
        .collect();
    scene["geometries"]
        .as_object_mut()
        .ok_or("Missing geometries")?
        .retain(|k, _| used.contains(k));
    scene["nodes"] = json!(nodes);
    scene["materials"] = json!(
        (0..7)
            .map(|i| package.materials()["materials"][format!("material-{i}")].clone())
            .collect::<Vec<_>>()
    );
    scene["textures"] = package.materials()["textures"].clone();
    for texture in scene["textures"]
        .as_object()
        .ok_or("Missing textures")?
        .values()
    {
        let name = texture["file"].as_str().ok_or("Missing texture file")?;
        files.insert(
            name.into(),
            package.file(name).ok_or("Missing texture bytes")?.to_vec(),
        );
    }
    files.insert(
        "village.wgsl".into(),
        package
            .file("village.wgsl")
            .ok_or("Missing village shader")?
            .to_vec(),
    );
    files.insert(
        "building-catalog.json".into(),
        serde_json::to_vec(package.catalog())?,
    );
    files.insert("village.json".into(), serde_json::to_vec(&scene)?);
    Ok(())
}

fn payload(
    package: &BuildingPackage,
    files: &mut BTreeMap<String, Vec<u8>>,
    reference: &Value,
) -> Result<String, Box<dyn Error>> {
    let name = reference["file"].as_str().ok_or("Missing payload name")?;
    files.insert(
        name.into(),
        package
            .file(name)
            .ok_or("Missing package payload")?
            .to_vec(),
    );
    Ok(name.into())
}

fn package_nodes(
    package: &BuildingPackage,
    files: &mut BTreeMap<String, Vec<u8>>,
    descriptor: &Value,
    key: &str,
) -> Result<Vec<Value>, Box<dyn Error>> {
    let mut nodes = Vec::new();
    for (index, part) in descriptor["building"]["parts"]
        .as_array()
        .ok_or("Missing parts")?
        .iter()
        .enumerate()
    {
        let material_id = part["material"].as_str().ok_or("Missing material ID")?;
        let material = &package.materials()["materials"][material_id];
        let kind = material["kind"].as_u64().ok_or("Missing material kind")?;
        let mut attrs = serde_json::Map::new();
        for (target, source) in [
            ("position", "pos"),
            ("normal", "nrm"),
            ("uv", "uv"),
            ("tint", "tint"),
            ("vdata", "data"),
        ] {
            let reference = match target {
                "position" => &part["reference_position"],
                "normal" => &part["reference_normal"],
                _ => &part["attributes"][source],
            };
            attrs.insert(target.into(), json!(payload(package, files, reference)?));
        }
        let indices = payload(package, files, &part["indices"])?;
        let geometry = json!({"attributes":attrs,"indices":indices});
        let geometry_id = bytes_sha256(&serde_json::to_vec(&geometry)?).to_string();
        // Added to the separate map below, to avoid borrowing the node array mutably.
        nodes.push(json!({"name":format!("village_{}",part["target"].as_str().ok_or("Missing source category")?),
                "source_id":format!("{key}/package/{index}"),"building_source_id":key,
                "geometry":geometry_id,"material":kind,"visible":true,
                "matrix_world":[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1],
                "package_geometry":geometry}));
    }
    Ok(nodes)
}
