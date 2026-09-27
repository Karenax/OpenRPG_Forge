//! Bounded, hash-pinned package reader, independent of filesystem and renderer.
use crate::AssetCatalog;
use forge_core::{ContentHash, bytes_sha256};
use serde::Deserialize;
use serde_json::Value;
use std::collections::BTreeMap;

#[derive(Deserialize)]
struct Entry {
    sha256: ContentHash,
    bytes: usize,
}
#[derive(Deserialize)]
struct Manifest {
    schema: String,
    version: u32,
    source_commit: String,
    catalog_sha256: ContentHash,
    files: BTreeMap<String, Entry>,
    license: Reference,
    material_library: Reference,
}
#[derive(Deserialize)]
struct Reference {
    file: String,
    sha256: ContentHash,
    bytes: usize,
}

/// Fully verified bytes. GPU validation and license review remain separate.
pub struct BuildingPackage {
    catalog: AssetCatalog,
    files: BTreeMap<String, Vec<u8>>,
    materials: Value,
}

impl BuildingPackage {
    /// `expected` comes from the caller's selected artifact, never from the
    /// manifest itself. Reader adapters must enforce the supplied byte limit.
    pub fn load(
        manifest: &[u8],
        expected: ContentHash,
        mut read: impl FnMut(&str, usize) -> Result<Vec<u8>, String>,
    ) -> Result<Self, String> {
        let m = parse_manifest(manifest, expected)?;
        let mut files = BTreeMap::new();
        for (name, entry) in &m.files {
            let bytes = read(name, entry.bytes)?;
            verify_payload(name, entry, &bytes)?;
            files.insert(name.clone(), bytes);
        }
        Self::finish(&m, files)
    }

    /// Sequential asynchronous transport for browser or host adapters. The full
    /// index is validated before the first request; each payload is verified
    /// before requesting the next one. Dropping this future stops further reads;
    /// the transport adapter owns cancellation of its in-flight request.
    pub async fn load_async<F, Fut>(
        manifest: &[u8],
        expected: ContentHash,
        mut read: F,
    ) -> Result<Self, String>
    where
        F: FnMut(String, usize) -> Fut,
        Fut: std::future::Future<Output = Result<Vec<u8>, String>>,
    {
        let m = parse_manifest(manifest, expected)?;
        let mut files = BTreeMap::new();
        for (name, entry) in &m.files {
            let bytes = read(name.clone(), entry.bytes).await?;
            verify_payload(name, entry, &bytes)?;
            files.insert(name.clone(), bytes);
        }
        Self::finish(&m, files)
    }

    fn finish(m: &Manifest, files: BTreeMap<String, Vec<u8>>) -> Result<Self, String> {
        check_reference(&m.license, &files)?;
        check_reference(&m.material_library, &files)?;
        let catalog_bytes = files
            .get("catalog.json")
            .ok_or("Missing building catalog")?;
        if bytes_sha256(catalog_bytes) != m.catalog_sha256 {
            return Err("Catalog pin mismatch".into());
        }
        let catalog: AssetCatalog = decode(catalog_bytes)?;
        catalog.validate_import_metadata()?;
        if catalog.assets.len() != 23 {
            return Err("Incomplete building catalog".into());
        }
        let materials: Value = decode(&files[&m.material_library.file])?;
        let package = Self {
            catalog,
            files,
            materials,
        };
        package.validate_dependencies()?;
        Ok(package)
    }

    #[must_use]
    pub fn catalog(&self) -> &AssetCatalog {
        &self.catalog
    }
    #[must_use]
    pub fn materials(&self) -> &Value {
        &self.materials
    }
    pub fn file(&self, name: &str) -> Option<&[u8]> {
        self.files.get(name).map(Vec::as_slice)
    }

    fn validate_dependencies(&self) -> Result<(), String> {
        if self.materials["schema"] != "forge.tidewater.building-material-library"
            || self.materials["version"] != 1
        {
            return Err("Unsupported material library".into());
        }
        let materials = self.materials["materials"]
            .as_object()
            .ok_or("Missing materials")?;
        let textures = self.materials["textures"]
            .as_object()
            .ok_or("Missing textures")?;
        if materials.len() != 7 || textures.len() != 14 {
            return Err("Incomplete material library".into());
        }
        check_reference(
            &serde_json::from_value(self.materials["shader"].clone()).map_err(|e| e.to_string())?,
            &self.files,
        )?;
        for texture in textures.values() {
            let name = texture["file"].as_str().ok_or("Missing texture payload")?;
            if !self.files.contains_key(name) {
                return Err("Absent texture payload".into());
            }
        }
        for material in materials.values() {
            for texture in material["textures"]
                .as_array()
                .ok_or("Missing material textures")?
            {
                if !texture
                    .as_str()
                    .is_some_and(|name| textures.contains_key(name))
                {
                    return Err("Unknown material texture".into());
                }
            }
        }
        for record in &self.catalog.assets {
            let bytes = self
                .files
                .get(&format!("{}.json", record.content_hash))
                .ok_or("Missing building descriptor")?;
            if bytes_sha256(bytes) != record.content_hash {
                return Err("Building descriptor pin mismatch".into());
            }
            let descriptor: Value = decode(bytes)?;
            if descriptor["schema"] != "forge.tidewater.building-asset"
                || descriptor["version"] != 1
                || descriptor["building"]["source_id"] != record.logical_name
            {
                return Err("Building descriptor identity mismatch".into());
            }
            for part in descriptor["building"]["parts"]
                .as_array()
                .ok_or("Missing building parts")?
            {
                if !part["material"]
                    .as_str()
                    .is_some_and(|id| materials.contains_key(id))
                {
                    return Err("Unknown building material".into());
                }
                let attrs = part["attributes"].as_object().ok_or("Missing attributes")?;
                for value in attrs.values().chain([
                    &part["indices"],
                    &part["reference_position"],
                    &part["reference_normal"],
                ]) {
                    let reference: Reference =
                        serde_json::from_value(value.clone()).map_err(|e| e.to_string())?;
                    check_reference(&reference, &self.files)?;
                }
            }
        }
        Ok(())
    }
}

fn decode<T: serde::de::DeserializeOwned>(bytes: &[u8]) -> Result<T, String> {
    serde_json::from_slice(bytes).map_err(|e| e.to_string())
}
fn check_reference(r: &Reference, files: &BTreeMap<String, Vec<u8>>) -> Result<(), String> {
    let bytes = files.get(&r.file).ok_or("Missing package dependency")?;
    if bytes.len() != r.bytes || bytes_sha256(bytes) != r.sha256 {
        return Err("Package dependency differs".into());
    }
    Ok(())
}
fn safe_name(name: &str) -> bool {
    !name.is_empty()
        && name.len() <= 128
        && !name.starts_with('.')
        && name
            .bytes()
            .all(|c| c.is_ascii_alphanumeric() || b"-_.".contains(&c))
        && !name.ends_with('.')
}

fn parse_manifest(manifest: &[u8], expected: ContentHash) -> Result<Manifest, String> {
    if manifest.len() > 2 * 1024 * 1024 || bytes_sha256(manifest) != expected {
        return Err("Building package manifest pin mismatch or size limit".into());
    }
    let m: Manifest = decode(manifest)?;
    if m.schema != "forge.tidewater.building-package"
        || m.version != 1
        || m.source_commit != "1438b1abfcaee3267092b75573014f4d9b4a983c"
        || m.files.is_empty()
        || m.files.len() > 4096
    {
        return Err("Unsupported building package".into());
    }
    let mut total = 0_usize;
    // Validate the entire index before invoking any reader.
    for (name, entry) in &m.files {
        if !safe_name(name) || entry.bytes == 0 || entry.bytes > 32 * 1024 * 1024 {
            return Err("Unsafe or oversized building payload".into());
        }
        total = total
            .checked_add(entry.bytes)
            .ok_or("Building package size overflow")?;
        if total > 128 * 1024 * 1024 {
            return Err("Building package exceeds 128 MiB".into());
        }
    }
    Ok(m)
}

fn verify_payload(name: &str, entry: &Entry, bytes: &[u8]) -> Result<(), String> {
    if bytes.len() != entry.bytes || bytes_sha256(bytes) != entry.sha256 {
        return Err(format!("Building payload differs: {name}"));
    }
    Ok(())
}
