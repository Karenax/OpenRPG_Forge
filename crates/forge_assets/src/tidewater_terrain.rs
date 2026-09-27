//! Transport-neutral, pinned terrain rendering resources. GPU qualification is separate.
use forge_core::{ContentHash, bytes_sha256};
use serde::Deserialize;
use std::collections::{BTreeMap, BTreeSet};
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Entry {
    sha256: ContentHash,
    bytes: usize,
}
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Manifest {
    schema: String,
    version: u32,
    source_commit: String,
    files: BTreeMap<String, Entry>,
}
/// Verified resources for the fixed source terrain, independent of building assets.
pub struct TerrainPackage {
    files: BTreeMap<String, Vec<u8>>,
}
impl TerrainPackage {
    /// The caller pins the manifest and bounds each transport read to the supplied size.
    pub fn load(
        manifest: &[u8],
        expected: ContentHash,
        mut read: impl FnMut(&str, usize) -> Result<Vec<u8>, String>,
    ) -> Result<Self, String> {
        let m = parse(manifest, expected)?;
        let mut files = BTreeMap::new();
        for (name, entry) in m.files {
            let bytes = read(&name, entry.bytes)?;
            verify(&name, &entry, &bytes)?;
            files.insert(name, bytes);
        }
        Ok(Self { files })
    }
    /// Sequential browser transport; failed payloads stop subsequent requests.
    pub async fn load_async<F, Fut>(
        manifest: &[u8],
        expected: ContentHash,
        mut read: F,
    ) -> Result<Self, String>
    where
        F: FnMut(String, usize) -> Fut,
        Fut: std::future::Future<Output = Result<Vec<u8>, String>>,
    {
        let m = parse(manifest, expected)?;
        let mut files = BTreeMap::new();
        for (name, entry) in m.files {
            let bytes = read(name.clone(), entry.bytes).await?;
            verify(&name, &entry, &bytes)?;
            files.insert(name, bytes);
        }
        Ok(Self { files })
    }
    pub fn file(&self, name: &str) -> Option<&[u8]> {
        self.files.get(name).map(Vec::as_slice)
    }
    #[must_use]
    pub fn file_count(&self) -> usize {
        self.files.len()
    }
}
fn verify(name: &str, entry: &Entry, bytes: &[u8]) -> Result<(), String> {
    if bytes.len() != entry.bytes || bytes_sha256(bytes) != entry.sha256 {
        return Err("Terrain payload size or pin mismatch".into());
    }
    if !is_texture(name) {
        let text = std::str::from_utf8(bytes).map_err(|e| e.to_string())?;
        if text.trim().is_empty() {
            return Err(format!("Empty terrain text resource: {name}"));
        }
    }
    Ok(())
}
const RESOURCES: [(&str, usize); 6] = [
    ("terrain.wgsl", 1_000_000),
    ("terrain-lod.wgsl", 1_000_000),
    ("terrain-normal.bin", 22_369_620),
    ("terrain-splat.bin", 22_369_620),
    ("terrain-detail.bin", 1_398_100),
    ("LICENSE-Tidewater.txt", 32_768),
];
fn parse(bytes: &[u8], expected: ContentHash) -> Result<Manifest, String> {
    if bytes.len() > 16 * 1024 || bytes_sha256(bytes) != expected {
        return Err("Terrain manifest pin mismatch or size limit".into());
    }
    let manifest: Manifest = serde_json::from_slice(bytes).map_err(|e| e.to_string())?;
    let mut resources = RESOURCES.to_vec();
    if manifest.version == 2 {
        resources.push(("hill-shadow.wgsl", 1_000_000));
    }
    let required: BTreeSet<_> = resources.iter().map(|(name, _)| *name).collect();
    if manifest.schema != "forge.tidewater.terrain-package"
        || !matches!(manifest.version, 1 | 2)
        || manifest.source_commit != "1438b1abfcaee3267092b75573014f4d9b4a983c"
        || manifest
            .files
            .keys()
            .map(String::as_str)
            .collect::<BTreeSet<_>>()
            != required
    {
        return Err("Unsupported or incomplete terrain package".into());
    }
    // Validate the entire index before transport; binary sizes include every RGBA8 mip.
    for (name, limit) in resources {
        let entry = &manifest.files[name];
        if entry.bytes == 0 || entry.bytes > limit || (is_texture(name) && entry.bytes != limit) {
            return Err(format!("Invalid terrain resource size: {name}"));
        }
    }
    Ok(manifest)
}

fn is_texture(name: &str) -> bool {
    matches!(
        name,
        "terrain-normal.bin" | "terrain-splat.bin" | "terrain-detail.bin"
    )
}
