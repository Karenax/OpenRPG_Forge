//! Decode a prepared canonical import without renderer or Studio dependencies.
use forge_core::CommandId;
use forge_project::{
    TerrainDocument, VegetationDocument, zone_authoring::session::ZoneEditSession,
};
use std::{collections::BTreeSet, error::Error, fs, io::Write, path::PathBuf};

fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args_os().skip(1).collect();
    if args.len() != 3 && args.len() != 5 {
        return Err(
            "usage: verify_imported_vegetation IMPORT_DIRECTORY TERRAIN_DOCUMENT NEW_REPORT [NEW_AUTHORITY HEIGHT_BLOB]".into(),
        );
    }
    let root = PathBuf::from(&args[0]);
    let output = PathBuf::from(&args[2]);
    let document =
        VegetationDocument::from_json(&fs::read_to_string(root.join("vegetation.v2.json"))?)?;
    let source = document
        .imported_source
        .as_ref()
        .ok_or("missing imported source")?;
    let mut ids = BTreeSet::new();
    let mut blocks = Vec::new();
    for block in &source.blocks {
        let path = root.join(format!("{}.bin", block.instances_sha256));
        if fs::metadata(&path)?.len() != u64::try_from(block.byte_len()?)? {
            return Err("blob size mismatch".into());
        }
        let bytes = fs::read(path)?;
        let instances = block.decode(&bytes, document.extent_m)?;
        for instance in &instances {
            if !ids.insert(block.instance_id(
                document.project_id,
                document.area_id,
                instance.source_key,
            )) {
                return Err("duplicate canonical entity ID".into());
            }
        }
        blocks.push(serde_json::json!({"layer": block.layer_id, "count": instances.len(), "sha256": block.instances_sha256}));
    }
    let restored = VegetationDocument::from_json(&document.to_pretty_json()?)?;
    if document != restored || document.content_hash()? != restored.content_hash()? {
        return Err("document roundtrip changed".into());
    }
    let terrain = TerrainDocument::from_json(&fs::read_to_string(&args[1])?)?;
    let mut zone = ZoneEditSession::from_initial_terrain(terrain)?;
    let mut base = zone.head().base.clone();
    base.vegetation = Some(document.clone());
    let request = zone.head().prepare_regeneration(
        base,
        CommandId::deterministic("tidewater-import-vegetation"),
    )?;
    zone.execute(&request)?;
    let archive = zone.archive_bytes()?;
    let restored_zone = ZoneEditSession::from_archive_bytes(&archive)?;
    if restored_zone.head() != zone.head() {
        return Err("zone archive changed imported source".into());
    }
    let stored = if args.len() == 5 {
        Some(verify_store(
            &zone,
            &root,
            &PathBuf::from(&args[3]),
            &PathBuf::from(&args[4]),
        )?)
    } else {
        None
    };
    let report = serde_json::json!({"status":"pass", "instances":ids.len(), "blocks":blocks,
        "document_sha256":document.content_hash()?, "zone_archive_bytes": archive.len(),
        "zone_head_sha256": zone.head().content_hash()?,
        "source_store":stored,
        "scope":"canonical data and optional workspace object/blob store; Studio and renderer not qualified"});
    let mut file = fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(output)?;
    writeln!(file, "{}", serde_json::to_string_pretty(&report)?)?;
    println!("Validated {} imported instances", ids.len());
    Ok(())
}

fn verify_store(
    zone: &ZoneEditSession,
    import: &std::path::Path,
    authority: &std::path::Path,
    heights: &std::path::Path,
) -> Result<serde_json::Value, Box<dyn Error>> {
    use forge_core::bytes_sha256;
    use forge_project::workspace::{
        objects::{ReviewedZone, ZoneObjectStore},
        source_blobs::SourceBlobStore,
    };
    fs::create_dir(authority)?;
    let store = SourceBlobStore::open(authority)?;
    let terrain = zone
        .head()
        .base
        .terrain
        .sampled_source
        .as_ref()
        .ok_or("missing sampled terrain")?;
    if fs::metadata(heights)?.len() != terrain.byte_length()? as u64 {
        return Err("height blob size mismatch".into());
    }
    store.publish(terrain.samples_sha256, &fs::read(heights)?)?;
    for block in &zone
        .head()
        .base
        .vegetation
        .as_ref()
        .ok_or("missing vegetation")?
        .imported_source
        .as_ref()
        .ok_or("missing import")?
        .blocks
    {
        store.publish(
            block.instances_sha256,
            &fs::read(import.join(format!("{}.bin", block.instances_sha256)))?,
        )?;
    }
    let archive = zone.archive_bytes()?;
    let reviewed = ReviewedZone::from_bytes(&archive, bytes_sha256(&archive))?;
    ZoneObjectStore::open(authority)?.publish(&reviewed)?;
    // Reopen using only the destination authority; no source-file access below.
    let reopened_bytes =
        ZoneObjectStore::open(authority)?.read(reviewed.project_id(), reviewed.reference())?;
    let reopened = ZoneEditSession::from_archive_bytes(&reopened_bytes)?;
    let required = reopened.required_source_blobs()?;
    let destination = SourceBlobStore::open(authority)?;
    for (&hash, &length) in &required {
        destination.read(hash, length)?;
    }
    let vegetation = reopened
        .head()
        .base
        .vegetation
        .as_ref()
        .ok_or("lost vegetation")?;
    let mut instances = 0;
    for block in &vegetation
        .imported_source
        .as_ref()
        .ok_or("lost import")?
        .blocks
    {
        instances += block
            .decode(
                &destination.read(block.instances_sha256, block.byte_len()?)?,
                vegetation.extent_m,
            )?
            .len();
    }
    Ok(
        serde_json::json!({"blobs":required.len(),"bytes":required.values().sum::<usize>(),"instances_decoded_from_destination":instances,"archive_sha256":reviewed.reference().archive_hash}),
    )
}
