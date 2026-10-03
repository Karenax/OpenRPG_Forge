//! Hash-pinned, portable plaza props; editable placements live in the zone document.
use forge_core::{AssetId, ContentHash, bytes_sha256};
use serde::Deserialize;
use serde_json::Value;
use std::collections::{BTreeMap, BTreeSet};

const COMMIT: &str = "1438b1abfcaee3267092b75573014f4d9b4a983c";
const PACKAGE: &str = "c0b3ce259e9de735204e5ea03e2cce5b6bdf0a332c3ffaca9fab59052712a23a";
const MATERIALS: &str = "390950ef51469c5e906e4833272d7968127b1bb998629013d8b85ae7b8caebce";

#[must_use]
pub fn asset_id(ordinal: usize) -> Option<AssetId> {
    (ordinal < 5).then(|| AssetId::deterministic(format!("tidewater/plaza/{COMMIT}/{ordinal}")))
}

#[must_use]
pub fn asset_ordinal(id: AssetId) -> Option<usize> {
    (0..5).find(|&ordinal| asset_id(ordinal) == Some(id))
}

#[must_use]
pub fn package_hash() -> ContentHash {
    PACKAGE.parse().expect("Pinned plaza SHA-256 is valid")
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
    props: Reference,
    license: Reference,
    files: BTreeMap<String, Entry>,
    qualification: String,
}

/// Immutable source meshes and metadata, kept separate from mutable placement records.
pub struct PlazaPackage {
    files: BTreeMap<String, Vec<u8>>,
}

impl PlazaPackage {
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
        check_reference(&index.props, &files)?;
        check_reference(&index.license, &files)?;
        if index.props.file != "plaza-props.json"
            || index.license.file != "LICENSE-Tidewater.txt"
            || !files.get(&index.license.file).is_some_and(|license| {
                license.starts_with(b"MIT License")
                    && license
                        .windows(b"Permission is hereby granted".len())
                        .any(|window| window == b"Permission is hereby granted")
            })
        {
            return Err("Plaza provenance is incomplete".into());
        }
        let description: Value =
            serde_json::from_slice(&files[&index.props.file]).map_err(|error| error.to_string())?;
        let rows = description["props"]
            .as_array()
            .ok_or("Plaza props absent")?;
        if description["schema"] != "forge.tidewater.plaza-frames"
            || description["version"] != 1
            || description["source_commit"] != COMMIT
            || description["matched_source_triangles"] != 1_207
            || rows.len() != 5
        {
            return Err("Unexpected source plaza description".into());
        }
        let mut referenced = BTreeSet::from([index.props.file.clone(), index.license.file.clone()]);
        let mut triangles = 0_u64;
        for (ordinal, row) in rows.iter().enumerate() {
            triangles += validate_plaza_row(ordinal, row, &files, &mut referenced)?;
        }
        if triangles != 1_207 || referenced != files.keys().cloned().collect() {
            return Err("Plaza triangle count or payload closure differs".into());
        }
        Ok(Self { files })
    }

    #[must_use]
    pub fn description(&self) -> &[u8] {
        &self.files["plaza-props.json"]
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

fn validate_plaza_row(
    ordinal: usize,
    row: &Value,
    files: &BTreeMap<String, Vec<u8>>,
    referenced: &mut BTreeSet<String>,
) -> Result<u64, String> {
    let kind = match ordinal {
        0 | 1 => "bench",
        2 => "lampPost",
        _ => "barrel",
    };
    if row["source_id"] != format!("tidewater:{COMMIT}:plaza-prop:{ordinal}")
        || row["kind"] != kind
        || row["source_frame"]["rotation_xyzw"] != serde_json::json!([0, 0, 0, 1])
        || row["source_frame"]["scale"] != serde_json::json!([1, 1, 1])
        || row["source_frame"]["translation_m"]
            .as_array()
            .is_none_or(|values| {
                values.len() != 3
                    || values
                        .iter()
                        .any(|v| v.as_f64().is_none_or(|n| !n.is_finite()))
            })
        || row["colliders"]["boxes"]
            .as_array()
            .is_none_or(|boxes| boxes.len() != usize::from(ordinal < 2))
        || row["colliders"]["cylinders"]
            .as_array()
            .is_none_or(|cylinders| cylinders.len() != usize::from(ordinal >= 2))
        || row["lights"]
            .as_array()
            .is_none_or(|lights| lights.len() != usize::from(ordinal == 2))
    {
        return Err("Plaza source object differs".into());
    }
    let parts = row["parts"].as_array().ok_or("Plaza parts absent")?;
    if parts.is_empty() || parts.len() > 3 {
        return Err("Invalid plaza part count".into());
    }
    let mut triangles = 0_u64;
    for part in parts {
        if !matches!(part["material"].as_str(), Some("material-0" | "material-1"))
            || part["vertices"]
                .as_u64()
                .is_none_or(|v| v == 0 || v > 20_000)
        {
            return Err("Plaza material or vertex count differs".into());
        }
        let attrs = part["attributes"]
            .as_object()
            .ok_or("Plaza attributes absent")?;
        if attrs.len() != 5
            || !["pos", "nrm", "uv", "tint", "data"]
                .into_iter()
                .all(|name| attrs.contains_key(name))
        {
            return Err("Plaza attribute set differs".into());
        }
        for value in attrs.values().chain([
            &part["indices"],
            &part["reference_position"],
            &part["reference_normal"],
        ]) {
            let reference: Reference =
                serde_json::from_value(value.clone()).map_err(|error| error.to_string())?;
            if reference.file != format!("{}.bin", reference.sha256) {
                return Err("Plaza payload is not content addressed".into());
            }
            check_reference(&reference, files)?;
            referenced.insert(reference.file);
        }
        let indices: Reference =
            serde_json::from_value(part["indices"].clone()).map_err(|error| error.to_string())?;
        if indices.bytes == 0 || !indices.bytes.is_multiple_of(12) {
            return Err("Plaza triangle stream differs".into());
        }
        triangles += (indices.bytes / 12) as u64;
    }
    Ok(triangles)
}

fn parse(manifest: &[u8], expected: ContentHash) -> Result<Manifest, String> {
    if manifest.len() > 16 * 1024
        || bytes_sha256(manifest) != expected
        || expected.to_string() != PACKAGE
    {
        return Err("Plaza manifest pin mismatch or size limit".into());
    }
    let index: Manifest = serde_json::from_slice(manifest).map_err(|error| error.to_string())?;
    if index.schema != "forge.tidewater.plaza-package"
        || index.version != 1
        || index.source_commit != COMMIT
        || index.building_material_library_sha256.to_string() != MATERIALS
        || index.qualification
            != "verified portable payload; no Studio rendering or authoring proof"
        || index.files.len() != 59
        || index.files.iter().any(|(name, entry)| {
            name.contains(['/', '\\', ':']) || entry.bytes == 0 || entry.bytes > 256 * 1024
        })
        || index.files.values().map(|entry| entry.bytes).sum::<usize>() > 512 * 1024
    {
        return Err("Unsupported plaza package".into());
    }
    Ok(index)
}

fn verify(entry: &Entry, bytes: &[u8]) -> Result<(), String> {
    if bytes.len() != entry.bytes || bytes_sha256(bytes) != entry.sha256 {
        return Err("Plaza payload size or pin mismatch".into());
    }
    Ok(())
}

fn check_reference(reference: &Reference, files: &BTreeMap<String, Vec<u8>>) -> Result<(), String> {
    let bytes = files
        .get(&reference.file)
        .ok_or("Plaza dependency missing")?;
    if bytes.len() != reference.bytes || bytes_sha256(bytes) != reference.sha256 {
        return Err("Plaza dependency pin mismatch".into());
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    #[allow(
        clippy::case_sensitive_file_extension_comparisons,
        reason = "The pinned plaza manifest uses the exact lowercase .bin suffix."
    )]
    fn pinned_plaza_package_rejects_changed_payload() {
        let Some(root) = std::env::var_os("FORGE_TIDEWATER_PLAZA_PACKAGE") else {
            return;
        };
        let root = std::path::PathBuf::from(root);
        let manifest = std::fs::read(root.join("package.json")).unwrap();
        let package = PlazaPackage::load(&manifest, package_hash(), |name, _| {
            std::fs::read(root.join(name)).map_err(|error| error.to_string())
        })
        .unwrap();
        let description: Value = serde_json::from_slice(package.description()).unwrap();
        assert_eq!(description["props"].as_array().unwrap().len(), 5);
        assert_eq!(asset_ordinal(asset_id(3).unwrap()), Some(3));
        let mut tampered = false;
        assert!(
            PlazaPackage::load(&manifest, package_hash(), |name, _| {
                let mut bytes = std::fs::read(root.join(name)).map_err(|e| e.to_string())?;
                if !tampered && name.ends_with(".bin") {
                    bytes[0] ^= 1;
                    tampered = true;
                }
                Ok(bytes)
            })
            .is_err()
        );
        assert!(tampered);
        let mut changed = manifest;
        changed[0] ^= 1;
        assert!(PlazaPackage::load(&changed, package_hash(), |_, _| unreachable!()).is_err());
    }
}
