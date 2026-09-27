//! Bounded, transport-neutral plant resources; populations remain canonical workspace data.
#![allow(clippy::case_sensitive_file_extension_comparisons)] // Pinned resource names are case-sensitive identities, including in browser URLs.
use forge_core::{ContentHash, bytes_sha256};
use serde::Deserialize;
use std::collections::BTreeMap;
mod descriptions;
const COMMIT: &str = "1438b1abfcaee3267092b75573014f4d9b4a983c";
const DESCRIPTIONS: [&str; 3] = ["vegetation-scene.json", "grass.json", "plant-textures.json"];
const BASE: [&str; 9] = [
    "vegetation-scene.json",
    "grass.json",
    "plant-textures.json",
    "plant.wgsl",
    "detail.bin",
    "leaf.bin",
    "impostor-a.bin",
    "impostor-b.bin",
    "LICENSE-Tidewater.txt",
];
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
    source_fixture_sha256: ContentHash,
    files: BTreeMap<String, Entry>,
}
pub struct PlantPackage {
    files: BTreeMap<String, Vec<u8>>,
    source_fixture: ContentHash,
}
impl PlantPackage {
    /// Index and descriptors are validated before fetching binary resources.
    pub fn load(
        manifest: &[u8],
        expected: ContentHash,
        mut read: impl FnMut(&str, usize) -> Result<Vec<u8>, String>,
    ) -> Result<Self, String> {
        let m = parse(manifest, expected)?;
        let mut files = BTreeMap::new();
        for name in DESCRIPTIONS {
            let e = &m.files[name];
            let bytes = read(name, e.bytes)?;
            verify(name, e, &bytes)?;
            files.insert(name.to_owned(), bytes);
        }
        descriptions::validate(&m, &files)?;
        for (name, e) in &m.files {
            if files.contains_key(name) {
                continue;
            }
            let bytes = read(name, e.bytes)?;
            verify(name, e, &bytes)?;
            files.insert(name.clone(), bytes);
        }
        Ok(Self {
            files,
            source_fixture: m.source_fixture_sha256,
        })
    }
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
        for name in DESCRIPTIONS {
            let e = &m.files[name];
            let bytes = read(name.to_owned(), e.bytes).await?;
            verify(name, e, &bytes)?;
            files.insert(name.to_owned(), bytes);
        }
        descriptions::validate(&m, &files)?;
        for (name, e) in &m.files {
            if files.contains_key(name) {
                continue;
            }
            let bytes = read(name.clone(), e.bytes).await?;
            verify(name, e, &bytes)?;
            files.insert(name.clone(), bytes);
        }
        Ok(Self {
            files,
            source_fixture: m.source_fixture_sha256,
        })
    }
    pub fn file(&self, name: &str) -> Option<&[u8]> {
        self.files.get(name).map(Vec::as_slice)
    }
    #[must_use]
    pub const fn files(&self) -> &BTreeMap<String, Vec<u8>> {
        &self.files
    }
    #[must_use]
    pub const fn source_fixture(&self) -> ContentHash {
        self.source_fixture
    }
}
fn parse(bytes: &[u8], expected: ContentHash) -> Result<Manifest, String> {
    if bytes.len() > 256 * 1024 || bytes_sha256(bytes) != expected {
        return Err("Plant manifest size or pin mismatch".into());
    }
    let m: Manifest = serde_json::from_slice(bytes).map_err(|e| e.to_string())?;
    if m.schema != "forge.tidewater.plant-package"
        || m.version != 1
        || m.source_commit != COMMIT
        || m.files.len() > 256
        || BASE.iter().any(|n| !m.files.contains_key(*n))
    {
        return Err("Unsupported or incomplete plant manifest".into());
    }
    let mut total = 0_usize;
    for (name, e) in &m.files {
        let cap = if name.ends_with(".json") || name.ends_with(".wgsl") {
            1_000_000
        } else if name == "LICENSE-Tidewater.txt" {
            32_768
        } else if name.ends_with(".bin") {
            64 * 1024 * 1024
        } else {
            return Err("Unknown plant resource type".into());
        };
        if !valid_name(name) || e.bytes == 0 || e.bytes > cap {
            return Err("Invalid plant payload name or size".into());
        }
        total = total
            .checked_add(e.bytes)
            .ok_or("Plant byte count overflow")?;
        if total > 128 * 1024 * 1024 {
            return Err("Plant package budget exceeded".into());
        }
    }
    Ok(m)
}
fn valid_name(name: &str) -> bool {
    !name.is_empty()
        && name.len() <= 128
        && name.as_bytes()[0].is_ascii_alphanumeric()
        && name
            .bytes()
            .all(|b| b.is_ascii_alphanumeric() || matches!(b, b'-' | b'_' | b'.'))
}
fn verify(name: &str, entry: &Entry, bytes: &[u8]) -> Result<(), String> {
    if entry.bytes != bytes.len() || entry.sha256 != bytes_sha256(bytes) {
        return Err(format!("Plant payload size or pin mismatch: {name}"));
    }
    if !name.ends_with(".bin")
        && std::str::from_utf8(bytes)
            .map_err(|e| e.to_string())?
            .trim()
            .is_empty()
    {
        return Err("Empty plant text resource".into());
    }
    Ok(())
}
