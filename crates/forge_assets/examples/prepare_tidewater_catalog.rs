//! Materialize imported asset metadata from the verified frame acquisition.
//! This is not a Studio import or payload qualification.
use forge_assets::{AssetCatalog, AssetKind, AssetRecord, AssetSource};
use forge_core::{AssetId, bytes_sha256, canonical_json_bytes};
use serde_json::Value;
use std::{env, fs, path::PathBuf};

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let args: Vec<_> = env::args_os().skip(1).collect();
    if args.len() != 2 {
        return Err("usage: prepare_tidewater_catalog FRAMES_JSON NEW_DIRECTORY".into());
    }
    let input = PathBuf::from(&args[0]);
    let output = PathBuf::from(&args[1]);
    let bytes = fs::read(input)?;
    if bytes.len() > 4 * 1024 * 1024 {
        return Err("Frame metadata exceeds 4 MiB".into());
    }
    let frames: Value = serde_json::from_slice(&bytes)?;
    if frames["schema"] != "forge.tidewater.building-frames" || frames["version"] != 1 {
        return Err("Unsupported building frames".into());
    }
    let commit = frames["source_commit"]
        .as_str()
        .ok_or("Missing source commit")?;
    if commit != "1438b1abfcaee3267092b75573014f4d9b4a983c" {
        return Err("Unqualified Tidewater revision".into());
    }
    let buildings = frames["buildings"].as_array().ok_or("Missing buildings")?;
    if buildings.len() != 23 {
        return Err("Expected 23 source buildings".into());
    }
    let mut catalog = AssetCatalog {
        schema_version: 1,
        assets: Vec::new(),
    };
    let mut descriptors = Vec::new();
    for building in buildings {
        let name = building["source_id"]
            .as_str()
            .ok_or("Missing source identity")?;
        let descriptor = serde_json::json!({
            "schema": "forge.tidewater.building-asset", "version": 1,
            "source_commit": commit, "frame_acquisition_sha256": bytes_sha256(&bytes),
            "building": building,
        });
        let payload = canonical_json_bytes(&descriptor)?;
        let hash = bytes_sha256(&payload);
        catalog.assets.push(AssetRecord {
            id: AssetId::deterministic(name.as_bytes()),
            logical_name: name.into(),
            kind: AssetKind::Mesh,
            content_hash: hash,
            source: AssetSource::External {
                uri: format!("https://github.com/dgreenheck/tidewater/tree/{commit}"),
                declared_license: Some("MIT".into()),
            },
        });
        descriptors.push((hash, payload));
    }
    catalog.validate_import_metadata()?;
    let serialized = canonical_json_bytes(&catalog)?;
    let reopened: AssetCatalog = serde_json::from_slice(&serialized)?;
    for record in &catalog.assets {
        assert_eq!(
            reopened.resolve_import_pin(record.id, record.content_hash)?,
            record
        );
    }
    // Refuse replacement of an existing artifact directory.
    fs::create_dir(&output)?;
    for (hash, payload) in descriptors {
        fs::write(output.join(format!("{hash}.json")), payload)?;
    }
    fs::write(output.join("catalog.json"), &serialized)?;
    println!(
        "23 imported metadata records; catalog {}",
        bytes_sha256(&serialized)
    );
    Ok(())
}
