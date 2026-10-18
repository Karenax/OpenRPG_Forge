//! Immutable acquired scanned resources; no ECS, filesystem or GPU dependency.
use forge_core::{ContentHash, bytes_sha256};
use serde::Deserialize;
use serde_json::Value;
use std::collections::BTreeMap;

#[path = "scanned/atlases.rs"]
pub mod atlases;
#[path = "scanned/geometry.rs"]
pub mod geometry;
#[path = "scanned/templates.rs"]
pub mod templates;

const PIN: &str = "f7faf1c052d59edae78420f22372daa2f00369c05f2bb60846e965bcd86c4ad4";
const COMMIT: &str = "1438b1abfcaee3267092b75573014f4d9b4a983c";

/// Acquired package revision, not a rendered or authoring qualification.
///
/// # Panics
/// Only if the compiled SHA-256 literal is invalid.
#[must_use]
pub fn revision() -> ContentHash {
    PIN.parse().expect("Pinned scanned package hash is valid")
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
struct Counts {
    assets: usize,
    lod_meshes: usize,
    instances: usize,
    scanned_colliders: usize,
    atlas_mips: usize,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Manifest {
    schema: String,
    version: u32,
    source_commit: String,
    descriptors: BTreeMap<String, Reference>,
    files: BTreeMap<String, Entry>,
    counts: Counts,
    preparation_sha256: ContentHash,
    limits: String,
}

/// Verified resources read through a caller-owned transport, equally usable on WASM.
pub struct ScannedPackage {
    files: BTreeMap<String, Vec<u8>>,
    documents: BTreeMap<String, Value>,
    preparation: ContentHash,
}

impl ScannedPackage {
    /// Read the complete acquired package with bounded, closed relative names.
    ///
    /// # Errors
    /// Refuses manifest drift, unsafe names, oversized or modified payloads.
    pub fn load(
        manifest: &[u8],
        mut read: impl FnMut(&str, usize) -> Result<Vec<u8>, String>,
    ) -> Result<Self, String> {
        let index = parse(manifest)?;
        let mut files = BTreeMap::new();
        for (name, entry) in &index.files {
            let bytes = read(name, entry.bytes)?;
            verify(entry, &bytes)?;
            files.insert(name.clone(), bytes);
        }
        Self::finish(&index, files)
    }

    /// Use the same validation with an asynchronous browser/native transport.
    ///
    /// # Errors
    /// Propagates transport errors and rejects the same inputs as `Self::load`.
    pub async fn load_async<F, Fut>(manifest: &[u8], mut read: F) -> Result<Self, String>
    where
        F: FnMut(String, usize) -> Fut,
        Fut: std::future::Future<Output = Result<Vec<u8>, String>>,
    {
        let index = parse(manifest)?;
        let mut files = BTreeMap::new();
        for (name, entry) in &index.files {
            let bytes = read(name.clone(), entry.bytes).await?;
            verify(entry, &bytes)?;
            files.insert(name.clone(), bytes);
        }
        Self::finish(&index, files)
    }

    fn finish(index: &Manifest, files: BTreeMap<String, Vec<u8>>) -> Result<Self, String> {
        let mut documents = BTreeMap::new();
        for (kind, reference) in &index.descriptors {
            let entry = index
                .files
                .get(&reference.file)
                .ok_or("Scanned document absent")?;
            if entry.bytes != reference.bytes || entry.sha256 != reference.sha256 {
                return Err("Scanned document reference differs".into());
            }
            let bytes = files
                .get(&reference.file)
                .ok_or("Scanned document unread")?;
            let value: Value = serde_json::from_slice(bytes).map_err(|e| e.to_string())?;
            if value["source_commit"] != COMMIT || value["version"] != 1 {
                return Err("Scanned document revision differs".into());
            }
            documents.insert(kind.clone(), value);
        }
        Ok(Self {
            files,
            documents,
            preparation: index.preparation_sha256,
        })
    }

    /// Immutable acquired metadata; it is not an editable placement document.
    #[must_use]
    pub fn document(&self, kind: &str) -> Option<&Value> {
        self.documents.get(kind)
    }

    #[must_use]
    pub fn file(&self, name: &str) -> Option<&[u8]> {
        self.files.get(name).map(Vec::as_slice)
    }

    #[must_use]
    pub const fn preparation(&self) -> ContentHash {
        self.preparation
    }
}

fn safe_name(name: &str) -> bool {
    let Some((folder, leaf)) = name.split_once('/') else {
        return false;
    };
    ["geometry", "atlases", "material", "program", "collisions"].contains(&folder)
        && !leaf.is_empty()
        && leaf != "."
        && leaf != ".."
        && leaf
            .bytes()
            .all(|b| b.is_ascii_alphanumeric() || b".-_".contains(&b))
}

fn parse(bytes: &[u8]) -> Result<Manifest, String> {
    if bytes.len() > 64 * 1024 || bytes_sha256(bytes) != revision() {
        return Err("Scanned manifest pin or size differs".into());
    }
    let index: Manifest = serde_json::from_slice(bytes).map_err(|e| e.to_string())?;
    let total = index
        .files
        .values()
        .try_fold(0_usize, |sum, e| sum.checked_add(e.bytes));
    let counts = &index.counts;
    if index.schema != "forge.tidewater.debris-scanned-package"
        || index.version != 1
        || index.source_commit != COMMIT
        || index.files.len() != 82
        || index.descriptors.len() != 5
        || counts.assets != 4
        || counts.lod_meshes != 12
        || counts.instances != 141
        || counts.scanned_colliders != 60
        || counts.atlas_mips != 36
        || index.limits.is_empty()
        || total.is_none_or(|n| n > 80 * 1024 * 1024)
        || index
            .files
            .iter()
            .any(|(name, e)| !safe_name(name) || e.bytes > 24 * 1024 * 1024)
        || ["geometry", "atlases", "material", "program", "collisions"]
            .iter()
            .any(|kind| !index.descriptors.contains_key(*kind))
    {
        return Err("Unsupported scanned package".into());
    }
    Ok(index)
}

fn verify(entry: &Entry, bytes: &[u8]) -> Result<(), String> {
    if bytes.len() != entry.bytes || bytes_sha256(bytes) != entry.sha256 {
        return Err("Scanned payload pin or size differs".into());
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn altered_manifest_is_rejected_before_transport() {
        let mut reads = 0;
        assert!(
            ScannedPackage::load(b"{}", |_, _| {
                reads += 1;
                Ok(Vec::new())
            })
            .is_err()
        );
        assert_eq!(reads, 0);
        for name in [
            "../file",
            "geometry/../file",
            "geometry/..",
            "geometry/a:b",
            "geometry/a\\b",
            "other/file",
        ] {
            assert!(!safe_name(name));
        }
    }

    #[test]
    #[ignore = "Requires the real standalone scanned acquisition"]
    fn acquired_package_loads_and_corrupt_texture_is_refused() {
        let root = std::path::PathBuf::from(std::env::var("FORGE_DEBRIS_SCANNED_FIXTURE").unwrap());
        let manifest = std::fs::read(root.join("package.json")).unwrap();
        let package = ScannedPackage::load(&manifest, |name, size| {
            let bytes = std::fs::read(root.join(name)).map_err(|e| e.to_string())?;
            assert_eq!(bytes.len(), size);
            Ok(bytes)
        })
        .unwrap();
        assert_eq!(
            package.document("geometry").unwrap()["instances"]
                .as_array()
                .unwrap()
                .len(),
            141
        );
        assert_eq!(
            package
                .file("atlases/scan-albedo.mips.rgba8.bin")
                .unwrap()
                .len(),
            22_369_620
        );
        assert!(
            ScannedPackage::load(&manifest, |name, _| {
                let mut bytes = std::fs::read(root.join(name)).map_err(|e| e.to_string())?;
                if name == "atlases/scan-albedo.mips.rgba8.bin" {
                    bytes[0] ^= 1;
                }
                Ok(bytes)
            })
            .is_err()
        );
    }
}
