//! Portable source fish geometry and dedicated material contracts.
use forge_core::{AssetId, ContentHash, bytes_sha256};
use serde::Deserialize;
use serde_json::Value;
use std::collections::BTreeMap;

const COMMIT: &str = "1438b1abfcaee3267092b75573014f4d9b4a983c";
const PACKAGE: &str = "e578b44e93d18d79ec06c875b76dae005f71c17ecf1d0ff09aa3ecddd651330a";

#[must_use]
pub fn asset_id(ordinal: usize) -> Option<AssetId> {
    (ordinal < 78)
        .then(|| AssetId::deterministic(format!("tidewater:{COMMIT}:fish-prop:{ordinal}")))
}

#[must_use]
pub fn asset_ordinal(id: AssetId) -> Option<usize> {
    // IDs belong to this immutable source revision. Build the inverse table once,
    // keeping the first ordinal exactly as the previous linear search did.
    static ORDINALS: std::sync::LazyLock<BTreeMap<AssetId, usize>> =
        std::sync::LazyLock::new(|| {
            let mut ordinals = BTreeMap::new();
            for ordinal in 0..78 {
                if let Some(asset) = asset_id(ordinal) {
                    ordinals.entry(asset).or_insert(ordinal);
                }
            }
            ordinals
        });
    ORDINALS.get(&id).copied()
}

/// Immutable source package revision.
///
/// # Panics
/// Panics only if the compiled digest is invalid.
#[must_use]
pub fn package_hash() -> ContentHash {
    PACKAGE
        .parse()
        .expect("Pinned fish package digest is valid")
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
    props: Reference,
    material: Reference,
    license: Reference,
    files: BTreeMap<String, Entry>,
    qualification: String,
}

/// Immutable payloads; editable instances belong to the common zone document.
pub struct FishPropPackage {
    files: BTreeMap<String, Vec<u8>>,
}

impl FishPropPackage {
    /// Read and verify every payload before publishing the package.
    ///
    /// # Errors
    /// Rejects unsupported revisions, missing data and altered payloads.
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

    /// Acquire the same immutable package through an asynchronous transport.
    ///
    /// # Errors
    /// Rejects the same invalid inputs as `Self::load`.
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
        for reference in [&index.props, &index.material, &index.license] {
            let bytes = files.get(&reference.file).ok_or("Fish reference absent")?;
            if bytes.len() != reference.bytes || bytes_sha256(bytes) != reference.sha256 {
                return Err("Fish reference pin differs".into());
            }
        }
        let props: Value =
            serde_json::from_slice(&files[&index.props.file]).map_err(|error| error.to_string())?;
        let material: Value = serde_json::from_slice(&files[&index.material.file])
            .map_err(|error| error.to_string())?;
        if props["schema"] != "forge.tidewater.fish-prop-source"
            || props["version"] != 1
            || props["source_commit"] != COMMIT
            || props["instances"]
                .as_array()
                .is_none_or(|rows| rows.len() != 78)
            || props["prototypes"]
                .as_array()
                .is_none_or(|rows| rows.len() != 18)
            || props["draw_range_m"] != 90
            || props["shadow_range_m"] != 22
            || material["schema"] != "forge.tidewater.fish-material-source"
            || material["version"] != 1
            || material["source_commit"] != COMMIT
            || material["specular_mode"] != "intended"
            || !files[&index.license.file].starts_with(b"MIT License")
        {
            return Err("Fish source or material inventory differs".into());
        }
        Ok(Self { files })
    }

    #[must_use]
    pub fn description(&self) -> &[u8] {
        &self.files["geometry/fish-props.json"]
    }

    #[must_use]
    pub fn material_description(&self) -> &[u8] {
        &self.files["material/fish-material.json"]
    }

    #[must_use]
    pub fn file(&self, name: &str) -> Option<&[u8]> {
        self.files.get(name).map(Vec::as_slice)
    }

    /// Source instance templates advertised through the common asset catalog.
    ///
    /// # Errors
    /// Rejects invalid catalog metadata.
    pub fn catalog(&self) -> Result<forge_assets::AssetCatalog, String> {
        let assets = (0..78)
            .map(|ordinal| {
                Ok(forge_assets::AssetRecord {
                    id: asset_id(ordinal).ok_or("Fish ordinal out of range")?,
                    logical_name: format!("tidewater:{COMMIT}:fish-prop:{ordinal}"),
                    kind: forge_assets::AssetKind::Mesh,
                    source: forge_assets::AssetSource::External {
                        uri: format!("https://github.com/dgreenheck/tidewater/tree/{COMMIT}"),
                        declared_license: Some("MIT".into()),
                    },
                    content_hash: package_hash(),
                })
            })
            .collect::<Result<Vec<_>, String>>()?;
        let catalog = forge_assets::AssetCatalog {
            schema_version: 1,
            assets,
        };
        catalog.validate_import_metadata()?;
        Ok(catalog)
    }
}

fn parse(manifest: &[u8], expected: ContentHash) -> Result<Manifest, String> {
    if manifest.len() > 128 * 1024
        || bytes_sha256(manifest) != expected
        || expected.to_string() != PACKAGE
    {
        return Err("Fish manifest pin mismatch or size limit".into());
    }
    let index: Manifest = serde_json::from_slice(manifest).map_err(|error| error.to_string())?;
    if index.schema != "forge.tidewater.fish-prop-package"
        || index.version != 1
        || index.source_commit != COMMIT
        || index.props.file != "geometry/fish-props.json"
        || index.material.file != "material/fish-material.json"
        || index.license.file != "LICENSE-Tidewater.txt"
        || index.qualification
            != "source-exact-package; Rust integration and GPU visual parity pending"
        || index.files.len() != 250
        || index.files.iter().any(|(name, entry)| {
            name.contains(['\\', ':'])
                || name.split('/').any(|part| matches!(part, "" | "." | ".."))
                || entry.bytes == 0
                || entry.bytes > 2 * 1024 * 1024
        })
        || index.files.values().map(|entry| entry.bytes).sum::<usize>() > 20 * 1024 * 1024
    {
        return Err("Fish manifest contract differs".into());
    }
    Ok(index)
}

fn verify(entry: &Entry, bytes: &[u8]) -> Result<(), String> {
    if bytes.len() != entry.bytes || bytes_sha256(bytes) != entry.sha256 {
        return Err("Fish payload hash or byte count differs".into());
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::future::Future;
    use std::path::Path;
    use std::task::{Context, Poll, Waker};

    fn ready_result<T>(future: impl Future<Output = T>) -> T {
        let mut context = Context::from_waker(Waker::noop());
        let mut future = std::pin::pin!(future);
        match future.as_mut().poll(&mut context) {
            Poll::Ready(result) => result,
            Poll::Pending => panic!("Fixture transport unexpectedly yielded"),
        }
    }

    #[test]
    fn source_identity_round_trips() {
        let ids: std::collections::BTreeSet<_> = (0..78).map(|i| asset_id(i).unwrap()).collect();
        assert_eq!(ids.len(), 78);
        for ordinal in 0..78 {
            assert_eq!(asset_ordinal(asset_id(ordinal).unwrap()), Some(ordinal));
        }
        assert!(asset_id(78).is_none());
    }

    #[test]
    #[ignore = "Requires byte-pinned private fish package or acquired reference; run the documented external-fixture replay"]
    fn real_package_and_refusals() {
        let root =
            std::env::var("FORGE_TIDEWATER_FISH_PROP_PACKAGE").expect("Real package required");
        let root = Path::new(&root);
        let manifest = std::fs::read(root.join("package.json")).unwrap();
        let read = |name: &str, _: usize| std::fs::read(root.join(name)).map_err(|e| e.to_string());
        let package = FishPropPackage::load(&manifest, package_hash(), read).unwrap();
        let asynchronous = ready_result(FishPropPackage::load_async(
            &manifest,
            package_hash(),
            |name, size| std::future::ready(read(&name, size)),
        ))
        .unwrap();
        assert_eq!(package.files, asynchronous.files);
        assert!(
            ready_result(FishPropPackage::load_async(
                &manifest,
                package_hash(),
                |_, _| std::future::ready(Err("Missing".into())),
            ))
            .is_err()
        );
        assert_eq!(package.catalog().unwrap().assets.len(), 78);
        assert_eq!(
            package.file("material/skin-table-f32.bin").unwrap().len(),
            2688
        );
        assert!(!package.description().is_empty());
        assert!(!package.material_description().is_empty());
        assert!(
            FishPropPackage::load(&manifest, package_hash(), |_, _| Err("Missing".into())).is_err()
        );
        assert!(
            FishPropPackage::load(&manifest, package_hash(), |name, size| {
                let mut bytes = read(name, size)?;
                if name == "material/skin-table-f32.bin" {
                    bytes[0] ^= 1;
                }
                Ok(bytes)
            })
            .is_err()
        );
        let mut changed = manifest.clone();
        changed[0] ^= 1;
        assert!(FishPropPackage::load(&changed, package_hash(), read).is_err());
    }
}
