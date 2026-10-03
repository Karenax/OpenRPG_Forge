//! Hash-pinned, portable path lights; editable placements live in the zone document.
use forge_core::{AssetId, ContentHash, bytes_sha256};
use serde::Deserialize;
use serde_json::Value;
use std::collections::{BTreeMap, BTreeSet};

const COMMIT: &str = "1438b1abfcaee3267092b75573014f4d9b4a983c";
const PACKAGE: &str = "2c270ef9c0ab9e471160f1e92d95ee99255833804583e2807a117c1eeee71789";
const MATERIALS: &str = "390950ef51469c5e906e4833272d7968127b1bb998629013d8b85ae7b8caebce";

#[must_use]
pub fn asset_id(ordinal: usize) -> Option<AssetId> {
    (ordinal < 3)
        .then(|| AssetId::deterministic(format!("tidewater:{COMMIT}:village-path-light:{ordinal}")))
}

#[must_use]
pub fn asset_ordinal(id: AssetId) -> Option<usize> {
    (0..3).find(|&ordinal| asset_id(ordinal) == Some(id))
}

/// Immutable package revision.
///
/// # Panics
/// Panics only if the compiled SHA-256 literal is invalid.
#[must_use]
pub fn package_hash() -> ContentHash {
    PACKAGE.parse().expect("Pinned path_light SHA-256 is valid")
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
pub struct PathLightPackage {
    files: BTreeMap<String, Vec<u8>>,
}

impl PathLightPackage {
    /// Acquire the complete package through a caller-provided transport.
    ///
    /// # Errors
    /// Rejects unsupported manifests, missing payloads, altered bytes or open references.
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

    /// Acquire the same package through an asynchronous transport.
    ///
    /// # Errors
    /// Rejects the same invalid inputs as `Self::load` and propagates transport failures.
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
        if index.props.file != "path-lights.json"
            || index.license.file != "LICENSE-Tidewater.txt"
            || !files.get(&index.license.file).is_some_and(|license| {
                license.starts_with(b"MIT License")
                    && license
                        .windows(b"Permission is hereby granted".len())
                        .any(|window| window == b"Permission is hereby granted")
            })
        {
            return Err("PathLight provenance is incomplete".into());
        }
        let description: Value =
            serde_json::from_slice(&files[&index.props.file]).map_err(|error| error.to_string())?;
        let rows = description["props"]
            .as_array()
            .ok_or("PathLight props absent")?;
        if description["schema"] != "forge.tidewater.path-light-frames"
            || description["version"] != 1
            || description["source_commit"] != COMMIT
            || description["matched_source_triangles"] != 312
            || rows.len() != 3
        {
            return Err("Unexpected source path_light description".into());
        }
        let mut referenced = BTreeSet::from([index.props.file.clone(), index.license.file.clone()]);
        let mut triangles = 0_u64;
        for (ordinal, row) in rows.iter().enumerate() {
            triangles += validate_path_light_row(ordinal, row, &files, &mut referenced)?;
        }
        if triangles != 312 || referenced != files.keys().cloned().collect() {
            return Err("PathLight triangle count or payload closure differs".into());
        }
        Ok(Self { files })
    }

    #[must_use]
    pub fn description(&self) -> &[u8] {
        &self.files["path-lights.json"]
    }

    #[must_use]
    pub fn file(&self, name: &str) -> Option<&[u8]> {
        self.files.get(name).map(Vec::as_slice)
    }

    /// Standard discovery metadata; it does not install assets or create placements.
    ///
    /// # Errors
    /// Rejects an inconsistent compiled source inventory or invalid catalogue metadata.
    pub fn catalog(&self) -> Result<crate::AssetCatalog, String> {
        let mut assets = Vec::new();
        for ordinal in 0..3 {
            let id =
                asset_id(ordinal).ok_or("Path-light ordinal is outside the source inventory")?;
            assets.push(crate::AssetRecord {
                id,
                logical_name: format!("tidewater:{COMMIT}:village-path-light:{ordinal}"),
                kind: crate::AssetKind::Mesh,
                source: crate::AssetSource::External {
                    uri: format!("https://github.com/dgreenheck/tidewater/tree/{COMMIT}"),
                    declared_license: Some("MIT".into()),
                },
                content_hash: package_hash(),
            });
        }
        let catalog = crate::AssetCatalog {
            schema_version: 1,
            assets,
        };
        catalog.validate_import_metadata()?;
        Ok(catalog)
    }

    #[must_use]
    pub fn material_library_hash(&self) -> &'static str {
        MATERIALS
    }
}

fn validate_path_light_row(
    ordinal: usize,
    row: &Value,
    files: &BTreeMap<String, Vec<u8>>,
    referenced: &mut BTreeSet<String>,
) -> Result<u64, String> {
    if row["source_id"] != format!("tidewater:{COMMIT}:village-path-light:{ordinal}")
        || row["kind"] != "pathLight"
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
            .is_none_or(|boxes| !boxes.is_empty())
        || row["colliders"]["cylinders"]
            .as_array()
            .is_none_or(|cylinders| cylinders.len() != 1)
        || row["lights"]
            .as_array()
            .is_none_or(|lights| lights.len() != 1)
    {
        return Err("PathLight source object differs".into());
    }
    let parts = row["parts"].as_array().ok_or("PathLight parts absent")?;
    if parts.len() != 3 {
        return Err("Invalid path_light part count".into());
    }
    let mut triangles = 0_u64;
    for part in parts {
        if !matches!(part["material"].as_str(), Some("material-0" | "material-1"))
            || part["vertices"]
                .as_u64()
                .is_none_or(|v| v == 0 || v > 20_000)
        {
            return Err("PathLight material or vertex count differs".into());
        }
        let attrs = part["attributes"]
            .as_object()
            .ok_or("PathLight attributes absent")?;
        if attrs.len() != 5
            || !["pos", "nrm", "uv", "tint", "data"]
                .into_iter()
                .all(|name| attrs.contains_key(name))
        {
            return Err("PathLight attribute set differs".into());
        }
        for value in attrs.values().chain([
            &part["indices"],
            &part["reference_position"],
            &part["reference_normal"],
        ]) {
            let reference: Reference =
                serde_json::from_value(value.clone()).map_err(|error| error.to_string())?;
            if reference.file != format!("{}.bin", reference.sha256) {
                return Err("PathLight payload is not content addressed".into());
            }
            check_reference(&reference, files)?;
            referenced.insert(reference.file);
        }
        let indices: Reference =
            serde_json::from_value(part["indices"].clone()).map_err(|error| error.to_string())?;
        if indices.bytes == 0 || !indices.bytes.is_multiple_of(12) {
            return Err("PathLight triangle stream differs".into());
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
        return Err("PathLight manifest pin mismatch or size limit".into());
    }
    let index: Manifest = serde_json::from_slice(manifest).map_err(|error| error.to_string())?;
    if index.schema != "forge.tidewater.path-light-package"
        || index.version != 1
        || index.source_commit != COMMIT
        || index.building_material_library_sha256.to_string() != MATERIALS
        || index.qualification
            != "verified portable payload; no Studio rendering or authoring proof"
        || index.files.len() != 40
        || index.files.iter().any(|(name, entry)| {
            name.contains(['/', '\\', ':']) || entry.bytes == 0 || entry.bytes > 256 * 1024
        })
        || index.files.values().map(|entry| entry.bytes).sum::<usize>() > 512 * 1024
    {
        return Err("Unsupported path_light package".into());
    }
    Ok(index)
}

fn verify(entry: &Entry, bytes: &[u8]) -> Result<(), String> {
    if bytes.len() != entry.bytes || bytes_sha256(bytes) != entry.sha256 {
        return Err("PathLight payload size or pin mismatch".into());
    }
    Ok(())
}

fn check_reference(reference: &Reference, files: &BTreeMap<String, Vec<u8>>) -> Result<(), String> {
    let bytes = files
        .get(&reference.file)
        .ok_or("PathLight dependency missing")?;
    if bytes.len() != reference.bytes || bytes_sha256(bytes) != reference.sha256 {
        return Err("PathLight dependency pin mismatch".into());
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    #[ignore = "Requires a real hash-pinned path-light package fixture"]
    #[allow(
        clippy::case_sensitive_file_extension_comparisons,
        reason = "The pinned path_light manifest uses the exact lowercase .bin suffix."
    )]
    fn pinned_path_light_package_rejects_changed_payload() {
        let root = std::env::var_os("FORGE_TIDEWATER_PATH_LIGHT_PACKAGE")
            .expect("Real path-light fixture required for this integration test");
        let root = std::path::PathBuf::from(root);
        let manifest = std::fs::read(root.join("package.json")).unwrap();
        let package = PathLightPackage::load(&manifest, package_hash(), |name, _| {
            std::fs::read(root.join(name)).map_err(|error| error.to_string())
        })
        .unwrap();
        let calls = std::cell::Cell::new(0);
        let future = PathLightPackage::load_async(&manifest, package_hash(), |name, _| {
            calls.set(calls.get() + 1);
            std::future::ready(std::fs::read(root.join(name)).map_err(|error| error.to_string()))
        });
        let mut future = std::pin::pin!(future);
        let mut context = std::task::Context::from_waker(std::task::Waker::noop());
        let async_package = match std::future::Future::poll(future.as_mut(), &mut context) {
            std::task::Poll::Ready(result) => result.unwrap(),
            std::task::Poll::Pending => panic!("Immediate fixture reads must complete"),
        };
        assert_eq!(calls.get(), 40);
        assert_eq!(async_package.description(), package.description());
        assert_eq!(package.material_library_hash(), MATERIALS);
        assert!(package.file("missing.bin").is_none());
        for (name, bytes) in &package.files {
            assert_eq!(async_package.file(name), Some(bytes.as_slice()));
        }
        let description: Value = serde_json::from_slice(package.description()).unwrap();
        assert_eq!(description["props"].as_array().unwrap().len(), 3);
        assert_eq!(asset_ordinal(asset_id(2).unwrap()), Some(2));
        assert!(asset_id(3).is_none());
        let catalog = package.catalog().unwrap();
        assert_eq!(catalog.assets.len(), 3);
        for asset in &catalog.assets {
            assert_eq!(asset.id, AssetId::deterministic(&asset.logical_name));
            assert_eq!(
                catalog
                    .resolve_import_pin(asset.id, package_hash())
                    .unwrap(),
                asset
            );
        }
        let mut tampered = false;
        assert!(
            PathLightPackage::load(&manifest, package_hash(), |name, _| {
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
        let mut changed = manifest.clone();
        changed[0] ^= 1;
        assert!(PathLightPackage::load(&changed, package_hash(), |_, _| unreachable!()).is_err());
    }
}
