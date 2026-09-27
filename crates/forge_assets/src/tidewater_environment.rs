//! Transport-neutral, pinned scene environment resources. GPU qualification is separate.
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
/// Verified resources for the fixed source environment, independent of building assets.
pub struct EnvironmentPackage {
    files: BTreeMap<String, Vec<u8>>,
}
impl EnvironmentPackage {
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
            verify(&entry, &bytes)?;
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
            verify(&entry, &bytes)?;
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
fn verify(entry: &Entry, bytes: &[u8]) -> Result<(), String> {
    if bytes.len() != entry.bytes || bytes_sha256(bytes) != entry.sha256 {
        return Err("Environment payload size or pin mismatch".into());
    }
    Ok(())
}
fn required() -> BTreeSet<String> {
    let mut names: BTreeSet<_> = [
        "atmosphere.json",
        "cloud-environment.json",
        "params.bin",
        "transmittance.wgsl",
        "multiScat.wgsl",
        "skyView.wgsl",
        "irradiance.wgsl",
        "sky.wgsl",
        "environment-cube.bin",
        "village-lighting.json",
        "cloud-view-params.bin",
        "cloud-weather.wgsl",
        "cloud-bounds.wgsl",
        "cloud-shadow.wgsl",
        "cloud-trace.wgsl",
        "cloud-temporal.wgsl",
        "cloud-pano.wgsl",
        "cloud-blueNoise.bin",
        "LICENSE-Tidewater.txt",
    ]
    .into_iter()
    .map(str::to_owned)
    .collect();
    for level in 0..7 {
        names.insert(format!("cloud-volume-m{level}.rgba8.bin"));
    }
    for step in 1..=120 {
        for kind in ["global", "atmosphere"] {
            names.insert(format!("cloud-step{step:03}-{kind}.bin"));
        }
    }
    names
}
fn parse(bytes: &[u8], expected: ContentHash) -> Result<Manifest, String> {
    if bytes.len() > 256 * 1024 || bytes_sha256(bytes) != expected {
        return Err("Environment manifest pin mismatch or size limit".into());
    }
    let manifest: Manifest = serde_json::from_slice(bytes).map_err(|e| e.to_string())?;
    if manifest.schema != "forge.tidewater.environment-package"
        || manifest.version != 1
        || manifest.source_commit != "1438b1abfcaee3267092b75573014f4d9b4a983c"
        || manifest.files.keys().cloned().collect::<BTreeSet<_>>() != required()
    {
        return Err("Unsupported or incomplete environment package".into());
    }
    // Exact allowlist also rules out path traversal and platform-specific filenames.
    let mut total = 0_usize;
    for entry in manifest.files.values() {
        if entry.bytes == 0 || entry.bytes > 2 * 1024 * 1024 {
            return Err("Environment payload size limit".into());
        }
        total = total
            .checked_add(entry.bytes)
            .ok_or("Environment size overflow")?;
    }
    if total > 16 * 1024 * 1024 {
        return Err("Environment package size limit".into());
    }
    Ok(manifest)
}
