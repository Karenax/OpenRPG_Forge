//! Pinned Tidewater source-profile resources, without world, renderer or filesystem dependencies.
//! Dataset qualification guards stay in this source-profile decoder, never in the generic engine.
#![allow(clippy::case_sensitive_file_extension_comparisons)] // Pinned resource names are case-sensitive identities, including in browser URLs.
use forge_core::{ContentHash, bytes_sha256};
use serde::{Deserialize, Serialize};
use std::collections::BTreeMap;
mod descriptions;
pub mod geometry;
pub mod grass;
pub mod packing;
pub mod profile;
pub mod scene;
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
#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct PlantResourcePin {
    pub sha256: ContentHash,
    pub bytes: usize,
}
#[derive(Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
struct Manifest {
    schema: String,
    version: u32,
    source_commit: String,
    source_fixture_sha256: ContentHash,
    files: BTreeMap<String, PlantResourcePin>,
}
pub struct PlantPackage {
    files: BTreeMap<String, Vec<u8>>,
    source_fixture: ContentHash,
    manifest_sha256: ContentHash,
}

/// The two metadata files that the historic flat fixture did not ship.
pub struct LegacyPlantIndex {
    pub manifest: Vec<u8>,
    pub texture_metadata: Vec<u8>,
    pub files: BTreeMap<String, PlantResourcePin>,
}

#[derive(Deserialize)]
struct LegacyFixture {
    schema: String,
    version: u32,
    source: LegacySource,
    files: BTreeMap<String, ContentHash>,
    leaf_texture: descriptions::Texture,
    impostor_texture: descriptions::Texture,
}

#[derive(Deserialize)]
struct LegacySource {
    commit: String,
}

impl PlantPackage {
    /// Adapt a verified installed fixture to the existing plant index. Every
    /// dependency must agree with both its fixture pin and its delivery inventory.
    pub fn index_legacy_fixture(
        fixture: &[u8],
        fixture_pin: ContentHash,
        terrain: ContentHash,
        mut inventory: impl FnMut(&str) -> Result<PlantResourcePin, String>,
        mut read: impl FnMut(&str, usize) -> Result<Vec<u8>, String>,
    ) -> Result<LegacyPlantIndex, String> {
        if fixture.len() > 4 * 1024 * 1024 || bytes_sha256(fixture) != fixture_pin {
            return Err("Legacy plant fixture size or pin mismatch".into());
        }
        let fixture: LegacyFixture = serde_json::from_slice(fixture).map_err(|e| e.to_string())?;
        if fixture.schema != "forge.tidewater.plant-probe"
            || fixture.version != 2
            || fixture.source.commit != COMMIT
        {
            return Err("Unsupported legacy plant fixture".into());
        }
        let mut checked_pin = |name: &str| {
            let pin = inventory(name)?;
            if fixture.files.get(name) != Some(&pin.sha256) {
                return Err(format!("Legacy plant inventory pin differs: {name}"));
            }
            Ok(pin)
        };
        let mut descriptions = BTreeMap::new();
        for name in ["vegetation-scene.json", "grass.json"] {
            let pin = checked_pin(name)?;
            if pin.bytes > 1_000_000 {
                return Err(format!("Legacy plant description is too large: {name}"));
            }
            let bytes = read(name, pin.bytes)?;
            verify(name, &pin, &bytes)?;
            descriptions.insert(name.to_owned(), bytes);
        }
        let required = descriptions::legacy_dependencies(&descriptions, terrain)?;
        let texture_metadata = serde_json::to_vec(&serde_json::json!({
            "schema": "forge.tidewater.plant-textures",
            "version": 1,
            "leaf": fixture.leaf_texture,
            "impostor": fixture.impostor_texture,
        }))
        .map_err(|e| e.to_string())?;
        let files = required
            .into_iter()
            .map(|name| {
                let pin = if name == "plant-textures.json" {
                    PlantResourcePin {
                        sha256: bytes_sha256(&texture_metadata),
                        bytes: texture_metadata.len(),
                    }
                } else {
                    checked_pin(&name)?
                };
                Ok((name, pin))
            })
            .collect::<Result<_, String>>()?;
        let manifest = Manifest {
            schema: "forge.tidewater.plant-package".into(),
            version: 1,
            source_commit: COMMIT.into(),
            source_fixture_sha256: fixture_pin,
            files,
        };
        descriptions.insert("plant-textures.json".into(), texture_metadata.clone());
        let bytes = serde_json::to_vec(&manifest).map_err(|e| e.to_string())?;
        parse(&bytes, bytes_sha256(&bytes))?;
        descriptions::validate(&manifest, &descriptions)?;
        Ok(LegacyPlantIndex {
            manifest: bytes,
            texture_metadata,
            files: manifest.files,
        })
    }

    /// Parse the existing index and its bounds before any resource transport.
    pub fn resource_pins(
        manifest: &[u8],
        expected: ContentHash,
    ) -> Result<BTreeMap<String, PlantResourcePin>, String> {
        Ok(parse(manifest, expected)?.files)
    }
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
            manifest_sha256: expected,
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
            manifest_sha256: expected,
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
    #[must_use]
    pub const fn manifest_sha256(&self) -> ContentHash {
        self.manifest_sha256
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
fn verify(name: &str, entry: &PlantResourcePin, bytes: &[u8]) -> Result<(), String> {
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
