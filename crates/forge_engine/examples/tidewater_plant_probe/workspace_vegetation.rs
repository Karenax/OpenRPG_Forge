//! Resolve a saved workspace zone before starting the native renderer.
use forge_core::AreaId;
use forge_project::{
    workspace::{
        file_session::FileWorkspaceSession, objects::ZoneObjectStore, source_blobs::SourceBlobStore,
    },
    zone_authoring::session::ZoneEditSession,
};
use std::{collections::BTreeMap, error::Error, path::Path};

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
