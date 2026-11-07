//! Read-only qualification of original pinned plant/grass geometry through the shared decoder.
use forge_tidewater_assets::tidewater_plants::PlantPackage;
use forge_tidewater_assets::tidewater_plants::SourceVegetationGeometry;
use serde_json::{Value, json};
use std::{collections::BTreeMap, fs::File, io::Read, path::Path};

fn read(root: &Path, name: &str, maximum: usize) -> Result<Vec<u8>, String> {
    let path = root.join(name);
    for ancestor in path.ancestors() {
        let metadata = ancestor.symlink_metadata().map_err(|e| e.to_string())?;
        if metadata.file_type().is_symlink() {
            return Err("Qualification input cannot use a symbolic link".into());
        }
        #[cfg(windows)]
        {
            use std::os::windows::fs::MetadataExt;
            if metadata.file_attributes() & 0x400 != 0 {
                return Err("Qualification input cannot use a reparse point".into());
            }
        }
    }
    let mut bytes = Vec::new();
    File::open(&path)
        .map_err(|e| e.to_string())?
        .take(u64::try_from(maximum).map_err(|e| e.to_string())? + 1)
        .read_to_end(&mut bytes)
        .map_err(|e| e.to_string())?;
    if bytes.len() > maximum {
        return Err(format!("Qualification resource exceeds bound: {name}"));
    }
    Ok(bytes)
}

fn verify_stream<const N: usize>(
    package: &PlantPackage,
    attributes: &BTreeMap<String, String>,
    name: &str,
    decoded: Option<&[[f32; N]]>,
) -> Result<(), String> {
    match (attributes.get(name), decoded) {
        (None, None) => Ok(()),
        (Some(path), Some(values)) => {
            let original = package.file(path).ok_or("Missing original stream")?;
            let mut reconstructed = values.iter().flatten().flat_map(|v| v.to_le_bytes());
            if !reconstructed.by_ref().eq(original.iter().copied()) {
                return Err(format!("Source stream changed after decoding: {name}"));
            }
            Ok(())
        }
        _ => Err(format!("Source stream presence changed: {name}")),
    }
}

fn level(
    package: &PlantPackage,
    group: &str,
    index: usize,
    value: &Value,
) -> Result<Value, String> {
    let attributes: BTreeMap<String, String> = serde_json::from_value(
        value
            .get("attributes")
            .cloned()
            .ok_or("Missing attributes")?,
    )
    .map_err(|e| e.to_string())?;
    let indices = value
        .get("indices")
        .and_then(Value::as_str)
        .ok_or("Missing indices")?;
    let geometry = SourceVegetationGeometry::decode(&attributes, indices, |name| {
        package
            .file(name)
            .ok_or_else(|| format!("Missing resource: {name}"))
    })?;
    verify_stream(package, &attributes, "position", Some(&geometry.positions))?;
    verify_stream(package, &attributes, "normal", Some(&geometry.normals))?;
    verify_stream(package, &attributes, "uv", geometry.uv.as_deref())?;
    verify_stream(package, &attributes, "aVeg", geometry.vegetation.as_deref())?;
    verify_stream(package, &attributes, "aMat", geometry.material.as_deref())?;
    verify_stream(package, &attributes, "aLobe", geometry.lobe.as_deref())?;
    let original = package.file(indices).ok_or("Missing original indices")?;
    if !geometry
        .indices
        .iter()
        .flat_map(|v| v.to_le_bytes())
        .eq(original.iter().copied())
    {
        return Err("Triangle indices changed after decoding".into());
    }
    Ok(
        json!({"group": group, "level": index, "vertices": geometry.positions.len(),
        "indices": geometry.indices.len(), "original_stream_bytes_equal": true,
        "declared_attributes": attributes.keys().collect::<Vec<_>>()}),
    )
}

fn main() -> Result<(), String> {
    let args: Vec<_> = std::env::args().skip(1).collect();
    let [root, pin] = args.as_slice() else {
        return Err("Usage: verify_plant_geometry ABSOLUTE_PACKAGE_ROOT MANIFEST_SHA256".into());
    };
    let root = Path::new(root);
    if !root.is_absolute() {
        return Err("Package root must be absolute".into());
    }
    let expected = pin
        .parse()
        .map_err(|e| format!("Invalid manifest pin: {e}"))?;
    let raw = read(root, "package.json", 256 * 1024)?;
    let package = PlantPackage::load(&raw, expected, |name, maximum| read(root, name, maximum))?;
    let parse = |name| {
        serde_json::from_slice::<Value>(package.file(name).ok_or("Missing description")?)
            .map_err(|e| e.to_string())
    };
    let plants = parse("vegetation-scene.json")?;
    let grass = parse("grass.json")?;
    let mut results = Vec::new();
    for group in plants["groups"].as_array().ok_or("Missing groups")? {
        let name = group["name"].as_str().ok_or("Missing group identity")?;
        for (index, value) in group["levels"]
            .as_array()
            .ok_or("Missing levels")?
            .iter()
            .enumerate()
        {
            results.push(level(&package, name, index, value)?);
        }
    }
    for (index, value) in grass["levels"]
        .as_array()
        .ok_or("Missing grass levels")?
        .iter()
        .enumerate()
    {
        results.push(level(&package, "grass", index, value)?);
    }
    println!("{}", serde_json::to_string_pretty(&json!({
        "schema": "forge.gate7.original-plant-geometry-decoding.v1", "passed": true,
        "manifest_sha256": pin, "source_fixture_sha256": package.source_fixture(),
        "package_files": package.files().len(), "levels": results,
        "scope": "Pinned package admission and exact decoded CPU geometry; no renderer, GPU or installed-module qualification",
        "gate_closed": false,
    })).map_err(|e| e.to_string())?);
    Ok(())
}
