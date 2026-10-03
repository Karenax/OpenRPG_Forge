//! Hash-pinned Tidewater ground-bounce map for the imported village.
use forge_core::{ContentHash, bytes_sha256};
use serde::Deserialize;
use std::collections::{BTreeMap, BTreeSet};

const COMMIT: &str = "1438b1abfcaee3267092b75573014f4d9b4a983c";
const BUNDLE: &str = "c739c737914261991fd9aba21c4372e7e9cafa31527f7a4139001a4db8e9b335";
const DESCRIPTOR: &str = "db19ec968d1dcd8dd0c9d48d23bb189dcccfe24e1b435fb61fc3326e2bcf5c9b";
const MAP: &str = "70e99b2c01613f1f8b064d462a88e5ff8717d557b9248810efe4ae089a561843";

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
    source_bundle_sha256: ContentHash,
    source_descriptor_sha256: ContentHash,
    files: BTreeMap<String, Entry>,
}

#[derive(Deserialize)]
struct Description {
    schema: String,
    version: u32,
    source_commit: String,
    width: u32,
    height: u32,
    format: String,
    sun_direction: [f32; 3],
    sea_level: f32,
    strength: f32,
    files: BTreeMap<String, String>,
}

/// Source-baked indirect-light field. Author edits must eventually rebake it.
pub struct GroundBouncePackage {
    pixels: Vec<u8>,
    sun_direction: [f32; 3],
    sea_level: f32,
    strength: f32,
}

impl GroundBouncePackage {
    pub fn load(
        manifest: &[u8],
        expected: ContentHash,
        mut read: impl FnMut(&str, usize) -> Result<Vec<u8>, String>,
    ) -> Result<Self, String> {
        let files = parse(manifest, expected)?;
        let mut payloads = BTreeMap::new();
        for (name, entry) in files {
            let data = read(&name, entry.bytes)?;
            verify(&entry, &data)?;
            payloads.insert(name, data);
        }
        Self::from_payloads(payloads)
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
        let files = parse(manifest, expected)?;
        let mut payloads = BTreeMap::new();
        for (name, entry) in files {
            let data = read(name.clone(), entry.bytes).await?;
            verify(&entry, &data)?;
            payloads.insert(name, data);
        }
        Self::from_payloads(payloads)
    }

    fn from_payloads(mut files: BTreeMap<String, Vec<u8>>) -> Result<Self, String> {
        let metadata = files
            .remove("ground-bounce.json")
            .ok_or("Ground bounce descriptor missing")?;
        if bytes_sha256(&metadata).to_string() != DESCRIPTOR {
            return Err("Ground bounce source descriptor mismatch".into());
        }
        let description: Description =
            serde_json::from_slice(&metadata).map_err(|e| e.to_string())?;
        let pixels = files
            .remove("ground-bounce.rgba16.bin")
            .ok_or("Ground bounce map missing")?;
        if description.schema != "forge.tidewater.ground-bounce-probe"
            || description.version != 1
            || description.source_commit != COMMIT
            || description.width != 512
            || description.height != 512
            || description.format != "rgba16float"
            || description
                .files
                .get("ground-bounce.rgba16.bin")
                .is_none_or(|hash| hash != MAP)
            || bytes_sha256(&pixels).to_string() != MAP
            || pixels.len() != 512 * 512 * 8
            || !description.sun_direction.into_iter().all(f32::is_finite)
            || !description.sea_level.is_finite()
            || !description.strength.is_finite()
            || !(0.0..=2.0).contains(&description.strength)
            || !files
                .remove("LICENSE-Tidewater.txt")
                .is_some_and(|license| {
                    license.starts_with(b"MIT License")
                        && license
                            .windows(b"Permission is hereby granted".len())
                            .any(|window| window == b"Permission is hereby granted")
                })
            || !files.is_empty()
        {
            return Err("Invalid ground bounce source".into());
        }
        Ok(Self {
            pixels,
            sun_direction: description.sun_direction,
            sea_level: description.sea_level,
            strength: description.strength,
        })
    }

    #[must_use]
    pub fn pixels(&self) -> &[u8] {
        &self.pixels
    }

    #[must_use]
    pub fn sun_direction(&self) -> [f32; 3] {
        self.sun_direction
    }

    #[must_use]
    pub fn sea_level(&self) -> f32 {
        self.sea_level
    }

    #[must_use]
    pub fn strength(&self) -> f32 {
        self.strength
    }
}

fn parse(manifest: &[u8], expected: ContentHash) -> Result<BTreeMap<String, Entry>, String> {
    if manifest.len() > 4096 || bytes_sha256(manifest) != expected {
        return Err("Ground bounce manifest pin mismatch or size limit".into());
    }
    let manifest: Manifest = serde_json::from_slice(manifest).map_err(|e| e.to_string())?;
    let required: BTreeSet<_> = [
        "ground-bounce.json",
        "ground-bounce.rgba16.bin",
        "LICENSE-Tidewater.txt",
    ]
    .into_iter()
    .map(str::to_owned)
    .collect();
    if manifest.schema != "forge.tidewater.ground-bounce-input-package"
        || manifest.version != 1
        || manifest.source_commit != COMMIT
        || manifest.source_bundle_sha256.to_string() != BUNDLE
        || manifest.source_descriptor_sha256.to_string() != DESCRIPTOR
        || manifest.files.keys().cloned().collect::<BTreeSet<_>>() != required
        || manifest
            .files
            .values()
            .any(|entry| entry.bytes == 0 || entry.bytes > 3 * 1024 * 1024)
    {
        return Err("Unsupported ground bounce package".into());
    }
    Ok(manifest.files)
}

fn verify(entry: &Entry, bytes: &[u8]) -> Result<(), String> {
    if bytes.len() != entry.bytes || bytes_sha256(bytes) != entry.sha256 {
        return Err("Ground bounce payload size or pin mismatch".into());
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    #[allow(
        clippy::float_cmp,
        reason = "Verifies exact scalar and vector constants read from the pinned acquired package."
    )]
    fn acquired_ground_bounce_package_is_pinned() {
        let Some(root) = std::env::var_os("FORGE_TIDEWATER_GROUND_BOUNCE_PACKAGE") else {
            return;
        };
        let root = std::path::PathBuf::from(root);
        let manifest = std::fs::read(root.join("package.json")).unwrap();
        let pin = bytes_sha256(&manifest);
        let package = GroundBouncePackage::load(&manifest, pin, |name, _| {
            std::fs::read(root.join(name)).map_err(|error| error.to_string())
        })
        .unwrap();
        assert_eq!(package.pixels().len(), 512 * 512 * 8);
        assert_eq!(package.strength(), 1.0);
        assert_eq!(package.sea_level(), 0.0);
        assert_eq!(
            package.sun_direction(),
            [-0.886_125_5, 0.454_984_52, 0.088_151_515]
        );
        let mut tampered = manifest;
        tampered[0] ^= 1;
        assert!(GroundBouncePackage::load(&tampered, pin, |_, _| unreachable!()).is_err());
    }
}
