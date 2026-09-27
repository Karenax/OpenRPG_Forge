//! Filesystem adapter for the shared authored terrain shadow preparation.
use super::checked_file;
use forge_engine::sun_cascades::CascadeView;
pub(super) use forge_engine::tidewater_object_shadow::terrain::Input as Terrain;
use serde_json::Value;
use std::{error::Error, path::Path};

pub(super) fn load(
    root: &Path,
    fixture: &Value,
    view: CascadeView,
    scene_files: Option<&std::collections::BTreeMap<String, Vec<u8>>>,
) -> Result<Terrain, Box<dyn Error>> {
    let read = |name: &str| -> Result<Vec<u8>, Box<dyn Error>> {
        match scene_files {
            Some(files) => files
                .get(name)
                .cloned()
                .ok_or_else(|| "missing shared terrain input".into()),
            None => checked_file(root, fixture, name),
        }
    };
    let heights = read("heights.bin")?;
    let doc =
        forge_project::TerrainDocument::from_json(std::str::from_utf8(&read("terrain.json")?)?)?;
    let shader = String::from_utf8(checked_file(root, fixture, "terrain-lod.wgsl")?)?;
    forge_engine::tidewater_object_shadow::terrain::authored::prepare(&doc, heights, &shader, view)
}
