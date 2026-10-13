//! Resolve a saved workspace zone before starting the native renderer.
use forge_core::AreaId;
use forge_project::{
    workspace::{
        file_session::FileWorkspaceSession, objects::ZoneObjectStore, source_blobs::SourceBlobStore,
    },
    zone_authoring::session::ZoneEditSession,
};
use std::{collections::BTreeMap, error::Error, path::Path};

/// Read the exact validated launch archive and immutable blobs; no workspace lock.
pub fn load_snapshot(
    root: &Path,
    expected: forge_core::ContentHash,
    files: &mut BTreeMap<String, Vec<u8>>,
) -> Result<(), Box<dyn Error>> {
    let root = root.canonicalize()?;
    let read = |name: &str, limit: usize| -> Result<Vec<u8>, Box<dyn Error>> {
        let path = root.join(name).canonicalize()?;
        if !path.starts_with(&root) || path.metadata()?.len() > u64::try_from(limit)? {
            return Err("snapshot file bound".into());
        }
        Ok(std::fs::read(path)?)
    };
    let archive = read(
        "zone.json",
        forge_project::zone_authoring::MAX_ZONE_HEAD_BYTES,
    )?;
    if forge_core::bytes_sha256(&archive) != expected {
        return Err("snapshot archive hash mismatch".into());
    }
    let zone = ZoneEditSession::from_archive_bytes(&archive)?;
    let (terrain, _) = zone.head().effective()?;
    let source = terrain
        .sampled_source
        .as_ref()
        .ok_or("snapshot raster absent")?;
    let fixture = forge_project::TerrainDocument::from_json(std::str::from_utf8(
        files.get("terrain.json").ok_or("scene terrain missing")?,
    )?)?;
    if fixture.sampled_source != terrain.sampled_source || fixture.extent_m != terrain.extent_m {
        return Err("snapshot terrain differs from runtime content".into());
    }
    let heights = read("heights.bin", source.byte_length()?)?;
    if forge_core::bytes_sha256(&heights) != source.samples_sha256 {
        return Err("snapshot raster hash mismatch".into());
    }
    let vegetation = zone
        .head()
        .base
        .vegetation
        .as_ref()
        .ok_or("snapshot vegetation absent")?;
    let population = vegetation
        .imported_source
        .as_ref()
        .ok_or("snapshot population absent")?;
    files.retain(|name, _| !name.starts_with("canonical-"));
    for block in &population.blocks {
        let bytes = read(
            &format!("{}.bin", block.instances_sha256),
            block.byte_len()?,
        )?;
        if forge_core::bytes_sha256(&bytes) != block.instances_sha256 {
            return Err("snapshot population hash mismatch".into());
        }
        files.insert(format!("canonical-{}.bin", block.instances_sha256), bytes);
    }
    files.insert("terrain.json".into(), serde_json::to_vec(&terrain)?);
    files.insert("heights.bin".into(), heights);
    files.insert(
        "canonical-vegetation.json".into(),
        serde_json::to_vec(vegetation)?,
    );
    eprintln!(
        "studio snapshot: archive={expected}, head={}",
        zone.head().content_hash()?
    );
    files.insert("canonical-zone.json".into(), archive);
    Ok(())
}

pub fn load(
    root: &Path,
    area: AreaId,
    files: &mut BTreeMap<String, Vec<u8>>,
) -> Result<(), Box<dyn Error>> {
    // The existing workspace adapter locks and validates the saved authority.
    // Keep that guard alive until the immutable archive and blobs are loaded.
    let workspace = FileWorkspaceSession::open(root)?;
    let mut matches = workspace
        .session()
        .head()
        .projects
        .iter()
        .flat_map(|project| {
            project.modules.iter().flat_map(move |module| {
                module
                    .zones
                    .iter()
                    .filter(move |zone| zone.area_id == area)
                    .map(move |zone| (project.id, zone))
            })
        });
    let (project, reference) = matches.next().ok_or("area absent from saved workspace")?;
    if matches.next().is_some() {
        return Err("ambiguous area in workspace".into());
    }
    let authority = root.join("workspace-v1");
    let archive = ZoneObjectStore::open(&authority)?.read(project, reference)?;
    let zone = ZoneEditSession::from_archive_bytes(&archive)?;
    let terrain = zone
        .head()
        .base
        .terrain
        .sampled_source
        .as_ref()
        .ok_or("workspace vegetation requires sampled Tidewater terrain")?;
    let fixture_terrain = forge_project::TerrainDocument::from_json(std::str::from_utf8(
        files
            .get("terrain.json")
            .ok_or("scene terrain descriptor missing")?,
    )?)?;
    if fixture_terrain.sampled_source.as_ref() != Some(terrain)
        || fixture_terrain.extent_m != zone.head().base.terrain.extent_m
    {
        return Err("workspace terrain differs from rendered scene".into());
    }
    let vegetation = zone
        .head()
        .base
        .vegetation
        .as_ref()
        .ok_or("workspace zone has no vegetation")?;
    let source = vegetation
        .imported_source
        .as_ref()
        .ok_or("workspace zone has no imported vegetation")?;
    let store = SourceBlobStore::open_existing(&authority)?;
    let mut replacement = BTreeMap::new();
    let (effective_terrain, _) = zone.head().effective()?;
    replacement.insert(
        "terrain.json".into(),
        serde_json::to_vec(&effective_terrain)?,
    );
    replacement.insert(
        "heights.bin".into(),
        store.read(terrain.samples_sha256, terrain.byte_length()?)?,
    );
    for block in &source.blocks {
        replacement.insert(
            format!("canonical-{}.bin", block.instances_sha256),
            store.read(block.instances_sha256, block.byte_len()?)?,
        );
    }
    replacement.insert(
        "canonical-vegetation.json".into(),
        serde_json::to_vec(vegetation)?,
    );
    replacement.insert("canonical-zone.json".into(), archive);
    // No old canonical records survive an explicit workspace selection.
    files.retain(|name, _| !name.starts_with("canonical-"));
    files.extend(replacement);
    eprintln!(
        "workspace vegetation: area={area}, archive={}, head={}",
        reference.archive_hash, reference.head_hash
    );
    Ok(())
}
