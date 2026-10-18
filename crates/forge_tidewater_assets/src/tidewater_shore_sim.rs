//! Hash-pinned Tidewater `ShoreSim` source inputs; GPU state is always derived.
use forge_core::{ContentHash, bytes_sha256};
use serde::Deserialize;
use std::collections::{BTreeMap, BTreeSet};

const COMMIT: &str = "1438b1abfcaee3267092b75573014f4d9b4a983c";
const SOURCE_BUNDLE: &str = "c739c737914261991fd9aba21c4372e7e9cafa31527f7a4139001a4db8e9b335";
const SOURCE_SHORE: &str = "21c130a70abaeba0a6ef323140edd3278c62397283962afd32b1527d9a171662";

#[derive(Debug, Deserialize)]
#[serde(deny_unknown_fields)]
struct Entry {
    sha256: ContentHash,
    bytes: usize,
}

#[derive(Debug, Deserialize)]
#[serde(deny_unknown_fields)]
struct Manifest {
    schema: String,
    version: u32,
    source_commit: String,
    source_bundle_sha256: ContentHash,
    source_shore_sha256: ContentHash,
    files: BTreeMap<String, Entry>,
}

/// Immutable source kernel and terrain/shore inputs for the render-device simulation.
#[derive(Debug)]
pub struct ShoreSimPackage {
    files: BTreeMap<String, Vec<u8>>,
}

impl ShoreSimPackage {
    /// Load a bounded package from any synchronous transport.
    pub fn load(
        manifest: &[u8],
        expected: ContentHash,
        mut read: impl FnMut(&str, usize) -> Result<Vec<u8>, String>,
    ) -> Result<Self, String> {
        let metadata = parse(manifest, expected)?;
        let mut files = BTreeMap::new();
        for (name, entry) in metadata.files {
            let bytes = read(&name, entry.bytes)?;
            verify(&entry, &bytes)?;
            files.insert(name, bytes);
        }
        validate_payloads(&files)?;
        Ok(Self { files })
    }

    /// Load through Studio's sequential same-origin browser transport.
    pub async fn load_async<F, Fut>(
        manifest: &[u8],
        expected: ContentHash,
        mut read: F,
    ) -> Result<Self, String>
    where
        F: FnMut(String, usize) -> Fut,
        Fut: std::future::Future<Output = Result<Vec<u8>, String>>,
    {
        let metadata = parse(manifest, expected)?;
        let mut files = BTreeMap::new();
        for (name, entry) in metadata.files {
            let bytes = read(name.clone(), entry.bytes).await?;
            verify(&entry, &bytes)?;
            files.insert(name, bytes);
        }
        validate_payloads(&files)?;
        Ok(Self { files })
    }

    #[must_use]
    pub fn file(&self, name: &str) -> Option<&[u8]> {
        self.files.get(name).map(Vec::as_slice)
    }
}

fn required() -> BTreeSet<String> {
    let mut names: BTreeSet<String> = [
        "shore-sim.json",
        "kernel.wgsl",
        "heights.f32.bin",
        "shore-field.rgba32.bin",
        "shore-direction.rgba32.bin",
        "shore-params.bin",
        "simulation-params.bin",
        "terrain-params.bin",
        "LICENSE-Tidewater.txt",
    ]
    .into_iter()
    .map(str::to_owned)
    .collect();
    names.extend((0..10).map(|mip| format!("lace-m{mip}.rgba8.bin")));
    names
}

fn parse(bytes: &[u8], expected: ContentHash) -> Result<Manifest, String> {
    if bytes.len() > 12 * 1024 || bytes_sha256(bytes) != expected {
        return Err("ShoreSim manifest pin mismatch or size limit".into());
    }
    let manifest: Manifest = serde_json::from_slice(bytes).map_err(|error| error.to_string())?;
    if manifest.schema != "forge.tidewater.shore-sim-input-package"
        || manifest.version != 1
        || manifest.source_commit != COMMIT
        || manifest.source_bundle_sha256.to_string() != SOURCE_BUNDLE
        || manifest.source_shore_sha256.to_string() != SOURCE_SHORE
        || manifest.files.keys().cloned().collect::<BTreeSet<_>>() != required()
    {
        return Err("Unsupported or incomplete ShoreSim package".into());
    }
    let mut total = 0_usize;
    for entry in manifest.files.values() {
        if entry.bytes == 0 || entry.bytes > 17 * 1024 * 1024 {
            return Err("ShoreSim payload size limit".into());
        }
        total = total
            .checked_add(entry.bytes)
            .ok_or("ShoreSim size overflow")?;
    }
    if total > 30 * 1024 * 1024 {
        return Err("ShoreSim package size limit".into());
    }
    Ok(manifest)
}

fn verify(entry: &Entry, bytes: &[u8]) -> Result<(), String> {
    if bytes.len() != entry.bytes || bytes_sha256(bytes) != entry.sha256 {
        return Err("ShoreSim payload size or pin mismatch".into());
    }
    Ok(())
}

fn validate_payloads(files: &BTreeMap<String, Vec<u8>>) -> Result<(), String> {
    if bytes_sha256(&files["shore-sim.json"]).to_string() != SOURCE_SHORE {
        return Err("ShoreSim source descriptor mismatch".into());
    }
    let descriptor: serde_json::Value =
        serde_json::from_slice(&files["shore-sim.json"]).map_err(|error| error.to_string())?;
    if descriptor["schema"] != "forge.tidewater.shore-sim"
        || descriptor["version"] != 1
        || descriptor["source_commit"] != COMMIT
        || descriptor["resolution"] != 768
        || descriptor["initial_state"] != "zero"
        || descriptor["direction_mode"] != "original_terrain_fallback"
        || descriptor["deposit_schedule"] != serde_json::json!([])
    {
        return Err("ShoreSim source descriptor unsupported".into());
    }
    for (name, size) in [
        ("heights.f32.bin", 2048 * 2048 * 4),
        ("shore-field.rgba32.bin", 512 * 512 * 16),
        ("shore-direction.rgba32.bin", 16),
        ("shore-params.bin", 64),
        ("simulation-params.bin", 48),
        ("terrain-params.bin", 32),
    ] {
        if files[name].len() != size
            || descriptor["files"][name] != bytes_sha256(&files[name]).to_string()
        {
            return Err(format!("ShoreSim source input mismatch: {name}"));
        }
    }
    for mip in 0..10 {
        let name = format!("lace-m{mip}.rgba8.bin");
        if files[&name].len() != (512_usize >> mip).pow(2) * 4
            || descriptor["files"][&name] != bytes_sha256(&files[&name]).to_string()
        {
            return Err(format!("ShoreSim lace mismatch: {name}"));
        }
    }
    let kernel = std::str::from_utf8(&files["kernel.wgsl"]).map_err(|error| error.to_string())?;
    if descriptor["files"]["kernel.wgsl"] != bytes_sha256(&files["kernel.wgsl"]).to_string()
        || !kernel.contains("@compute")
        || !std::str::from_utf8(&files["LICENSE-Tidewater.txt"])
            .map_err(|error| error.to_string())?
            .contains("MIT License")
    {
        return Err("ShoreSim kernel or license mismatch".into());
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn rejects_incomplete_manifest() {
        let bytes = serde_json::to_vec(&serde_json::json!({
            "schema": "forge.tidewater.shore-sim-input-package",
            "version": 1,
            "source_commit": COMMIT,
            "source_bundle_sha256": SOURCE_BUNDLE,
            "source_shore_sha256": SOURCE_SHORE,
            "files": {}
        }))
        .unwrap();
        assert!(
            parse(&bytes, bytes_sha256(&bytes))
                .unwrap_err()
                .contains("incomplete ShoreSim package")
        );
    }

    #[test]
    #[ignore = "requires the locally acquired Tidewater ShoreSim package"]
    fn acquired_inputs_match_the_pinned_source() {
        let root = std::path::PathBuf::from(
            std::env::var("FORGE_TIDEWATER_SHORE_SIM_PACKAGE")
                .expect("set FORGE_TIDEWATER_SHORE_SIM_PACKAGE"),
        );
        let manifest = std::fs::read(root.join("package.json")).unwrap();
        let expected = bytes_sha256(&manifest);
        assert_eq!(
            expected.to_string(),
            "2dda3c022af1f62a3148e811155ecafc83b825d4e7e0c4069124ff69e7fca754"
        );
        let package = ShoreSimPackage::load(&manifest, expected, |name, limit| {
            let bytes = std::fs::read(root.join(name)).map_err(|error| error.to_string())?;
            if bytes.len() > limit {
                return Err("oversized ShoreSim file".into());
            }
            Ok(bytes)
        })
        .unwrap();
        assert_eq!(
            package.file("heights.f32.bin").unwrap().len(),
            2048 * 2048 * 4
        );
    }
}
