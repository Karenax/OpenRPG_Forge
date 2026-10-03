//! Hash-pinned, portable source boardwalks; rendering and authored placement stay separate.
use forge_core::{AssetId, ContentHash, bytes_sha256};
use serde::Deserialize;
use serde_json::Value;
use std::collections::{BTreeMap, BTreeSet};

const COMMIT: &str = "1438b1abfcaee3267092b75573014f4d9b4a983c";
const PACKAGE: &str = "2d8270d51c0666b256618740a68069e79f6c1dda6c254062194717c937c734e6";
const MATERIALS: &str = "390950ef51469c5e906e4833272d7968127b1bb998629013d8b85ae7b8caebce";

/// Three independently placeable source objects share one immutable payload pin.
#[must_use]
pub fn asset_id(ordinal: usize) -> Option<AssetId> {
    (ordinal < 3).then(|| AssetId::deterministic(format!("tidewater/boardwalk/{COMMIT}/{ordinal}")))
}

#[must_use]
pub fn asset_ordinal(id: AssetId) -> Option<usize> {
    (0..3).find(|&ordinal| asset_id(ordinal) == Some(id))
}

#[must_use]
pub fn package_hash() -> ContentHash {
    PACKAGE.parse().expect("Pinned boardwalk SHA-256 is valid")
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Entry {
    bytes: usize,
    sha256: ContentHash,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Reference {
    file: String,
    bytes: usize,
    sha256: ContentHash,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Manifest {
    schema: String,
    version: u32,
    source_commit: String,
    building_material_library_sha256: ContentHash,
    boardwalks: Reference,
    license: Reference,
    files: BTreeMap<String, Entry>,
    qualification: String,
}

/// Immutable source objects, still separate from a Forge zone's mutable placements.
pub struct BoardwalkPackage {
    files: BTreeMap<String, Vec<u8>>,
}

impl BoardwalkPackage {
    pub fn load(
        manifest: &[u8],
        expected: ContentHash,
        mut read: impl FnMut(&str, usize) -> Result<Vec<u8>, String>,
    ) -> Result<Self, String> {
        let index = parse(manifest, expected)?;
        let mut files = BTreeMap::new();
        for (name, entry) in &index.files {
            let bytes = read(name, entry.bytes)?;
            verify(entry, &bytes)?;
            files.insert(name.clone(), bytes);
        }
        Self::finish(&index, files)
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
        let index = parse(manifest, expected)?;
        let mut files = BTreeMap::new();
        for (name, entry) in &index.files {
            let bytes = read(name.clone(), entry.bytes).await?;
            verify(entry, &bytes)?;
            files.insert(name.clone(), bytes);
        }
        Self::finish(&index, files)
    }

    fn finish(index: &Manifest, files: BTreeMap<String, Vec<u8>>) -> Result<Self, String> {
        check_reference(&index.boardwalks, &files)?;
        check_reference(&index.license, &files)?;
        if index.boardwalks.file != "boardwalks.json"
            || index.license.file != "LICENSE-Tidewater.txt"
            || !files.get(&index.license.file).is_some_and(|license| {
                license.starts_with(b"MIT License")
                    && license
                        .windows(b"Permission is hereby granted".len())
                        .any(|window| window == b"Permission is hereby granted")
            })
        {
            return Err("Boardwalk provenance is incomplete".into());
        }
        let description: Value = serde_json::from_slice(&files[&index.boardwalks.file])
            .map_err(|error| error.to_string())?;
        let rows = description["boardwalks"]
            .as_array()
            .ok_or("Boardwalk records absent")?;
        if description["schema"] != "forge.tidewater.boardwalk-frames"
            || description["version"] != 1
            || description["source_commit"] != COMMIT
            || rows.len() != 3
        {
            return Err("Unexpected source boardwalk description".into());
        }
        let mut referenced =
            BTreeSet::from([index.boardwalks.file.clone(), index.license.file.clone()]);
        for (ordinal, row) in rows.iter().enumerate() {
            if row["source_id"] != format!("tidewater:{COMMIT}:boardwalk:{ordinal}")
                || row["kind"] != "boardwalk"
                || row["role"]
                    != if ordinal == 0 {
                        "pier-to-plaza"
                    } else {
                        "side-path"
                    }
            {
                return Err("Boardwalk source identity mismatch".into());
            }
            let parts = row["parts"].as_array().ok_or("Boardwalk parts absent")?;
            if parts.is_empty() || parts.len() > 4 {
                return Err("Invalid boardwalk part count".into());
            }
            for part in parts {
                if !matches!(part["material"].as_str(), Some("material-0" | "material-1")) {
                    return Err("Unknown boardwalk material".into());
                }
                let attrs = part["attributes"]
                    .as_object()
                    .ok_or("Boardwalk attributes absent")?;
                if attrs.len() != 5
                    || !["pos", "nrm", "uv", "tint", "data"]
                        .into_iter()
                        .all(|name| attrs.contains_key(name))
                {
                    return Err("Boardwalk attribute set differs".into());
                }
                for value in attrs.values().chain([
                    &part["indices"],
                    &part["reference_position"],
                    &part["reference_normal"],
                ]) {
                    let reference: Reference =
                        serde_json::from_value(value.clone()).map_err(|e| e.to_string())?;
                    if reference.file != format!("{}.bin", reference.sha256) {
                        return Err("Boardwalk payload is not content addressed".into());
                    }
                    check_reference(&reference, &files)?;
                    referenced.insert(reference.file);
                }
            }
        }
        if referenced != files.keys().cloned().collect() {
            return Err("Unclaimed boardwalk payload".into());
        }
        Ok(Self { files })
    }

    #[must_use]
    pub fn description(&self) -> &[u8] {
        &self.files["boardwalks.json"]
    }

    #[must_use]
    pub fn file(&self, name: &str) -> Option<&[u8]> {
        self.files.get(name).map(Vec::as_slice)
    }

    #[must_use]
    pub fn material_library_hash(&self) -> &'static str {
        MATERIALS
    }
}

fn parse(manifest: &[u8], expected: ContentHash) -> Result<Manifest, String> {
    if manifest.len() > 16 * 1024
        || bytes_sha256(manifest) != expected
        || expected.to_string() != PACKAGE
    {
        return Err("Boardwalk manifest pin mismatch or size limit".into());
    }
    let index: Manifest = serde_json::from_slice(manifest).map_err(|error| error.to_string())?;
    if index.schema != "forge.tidewater.boardwalk-package"
        || index.version != 1
        || index.source_commit != COMMIT
        || index.building_material_library_sha256.to_string() != MATERIALS
        || index.qualification
            != "verified portable payload; no Studio rendering or authoring proof"
        || index.files.len() != 37
        || index.files.iter().any(|(name, entry)| {
            name.contains(['/', '\\', ':']) || entry.bytes == 0 || entry.bytes > 2 * 1024 * 1024
        })
        || index.files.values().map(|entry| entry.bytes).sum::<usize>() > 2 * 1024 * 1024
    {
        return Err("Unsupported boardwalk package".into());
    }
    Ok(index)
}

fn verify(entry: &Entry, bytes: &[u8]) -> Result<(), String> {
    if bytes.len() != entry.bytes || bytes_sha256(bytes) != entry.sha256 {
        return Err("Boardwalk payload size or pin mismatch".into());
    }
    Ok(())
}

fn check_reference(reference: &Reference, files: &BTreeMap<String, Vec<u8>>) -> Result<(), String> {
    let bytes = files
        .get(&reference.file)
        .ok_or("Boardwalk dependency missing")?;
    if bytes.len() != reference.bytes || bytes_sha256(bytes) != reference.sha256 {
        return Err("Boardwalk dependency pin mismatch".into());
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    #[allow(
        clippy::case_sensitive_file_extension_comparisons,
        reason = "The pinned boardwalk manifest uses the exact lowercase .bin suffix."
    )]
    fn pinned_boardwalk_package_rejects_a_changed_payload() {
        let Some(root) = std::env::var_os("FORGE_TIDEWATER_BOARDWALK_PACKAGE") else {
            return;
        };
        let root = std::path::PathBuf::from(root);
        let manifest = std::fs::read(root.join("package.json")).unwrap();
        let pin = bytes_sha256(&manifest);
        let package = BoardwalkPackage::load(&manifest, pin, |name, _| {
            std::fs::read(root.join(name)).map_err(|error| error.to_string())
        })
        .unwrap();
        let description: Value = serde_json::from_slice(package.description()).unwrap();
        assert_eq!(description["boardwalks"].as_array().unwrap().len(), 3);
        let mut tampered_once = false;
        assert!(
            BoardwalkPackage::load(&manifest, pin, |name, _| {
                let mut bytes = std::fs::read(root.join(name)).map_err(|e| e.to_string())?;
                if !tampered_once && name.ends_with(".bin") {
                    bytes[0] ^= 1;
                    tampered_once = true;
                }
                Ok(bytes)
            })
            .is_err()
        );
        assert!(tampered_once);
        let mut changed = manifest;
        changed[0] ^= 1;
        assert!(BoardwalkPackage::load(&changed, pin, |_, _| unreachable!()).is_err());
    }
}
