//! Read the pinned resources of an admitted scanned-geometry package.
use forge_core::{ContentHash, bytes_sha256};
use serde::Deserialize;
use serde_json::Value;
use std::collections::BTreeMap;

pub mod atlases;
pub mod geometry;
pub mod templates;

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
    instance_count: usize,
}

impl ScannedPackage {
    /// Read the complete acquired package with bounded, closed relative names.
    ///
    /// # Errors
    /// Refuses manifest drift, unsafe names, oversized or modified payloads.
    pub fn load(
        manifest: &[u8],
        expected_revision: ContentHash,
        mut read: impl FnMut(&str, usize) -> Result<Vec<u8>, String>,
    ) -> Result<Self, String> {
        let index = parse(manifest, expected_revision)?;
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
    pub async fn load_async<F, Fut>(
        manifest: &[u8],
        expected_revision: ContentHash,
        mut read: F,
    ) -> Result<Self, String>
    where
        F: FnMut(String, usize) -> Fut,
        Fut: std::future::Future<Output = Result<Vec<u8>, String>>,
    {
        let index = parse(manifest, expected_revision)?;
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
            if value["source_commit"] != index.source_commit || value["version"] != 1 {
                return Err("Scanned document revision differs".into());
            }
            documents.insert(kind.clone(), value);
        }
        Ok(Self {
            files,
            documents,
            preparation: index.preparation_sha256,
            instance_count: index.counts.instances,
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

fn parse(bytes: &[u8], expected_revision: ContentHash) -> Result<Manifest, String> {
    if bytes.len() > 64 * 1024 || bytes_sha256(bytes) != expected_revision {
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
        || index.source_commit.len() != 40
        || !index.source_commit.bytes().all(|b| b.is_ascii_hexdigit())
        || index.files.len() > 4096
        || index.descriptors.len() != 5
        || counts.assets != 4
        || counts.lod_meshes != 12
        || counts.instances > 16_384
        || counts.scanned_colliders > 16_384
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
    fn unsafe_resource_names_are_refused() {
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
    fn module_supplied_revision_and_resource_hashes_are_checked() {
        let commit = "0123456789abcdef0123456789abcdef01234567";
        let document = serde_json::to_vec(&serde_json::json!({
            "source_commit": commit,
            "version": 1
        }))
        .unwrap();
        let mut files = BTreeMap::new();
        let mut descriptors = BTreeMap::new();
        for kind in ["geometry", "atlases", "material", "program", "collisions"] {
            let path = format!("{kind}/payload.json");
            let pin =
                serde_json::json!({"bytes": document.len(), "sha256": bytes_sha256(&document)});
            files.insert(path.clone(), pin);
            descriptors.insert(
                kind,
                serde_json::json!({"file": path, "bytes": document.len(), "sha256": bytes_sha256(&document)}),
            );
        }
        let manifest = serde_json::to_vec(&serde_json::json!({
            "schema": "forge.tidewater.debris-scanned-package",
            "version": 1,
            "source_commit": commit,
            "descriptors": descriptors,
            "files": files,
            "counts": {"assets": 4, "lod_meshes": 12, "instances": 1, "scanned_colliders": 0, "atlas_mips": 36},
            "preparation_sha256": bytes_sha256(b"preparation"),
            "limits": "test package"
        }))
        .unwrap();
        let revision = bytes_sha256(&manifest);
        assert!(ScannedPackage::load(&manifest, revision, |_, _| Ok(document.clone())).is_ok());
        assert!(
            ScannedPackage::load(&manifest, bytes_sha256(b"other revision"), |_, _| {
                panic!("A foreign manifest must be rejected before reading resources");
            })
            .is_err()
        );
        assert!(ScannedPackage::load(&manifest, revision, |_, size| Ok(vec![0; size])).is_err());
    }
}
