//! Portable, hash-pinned source pier; editable placement is a separate document.
use forge_core::{AssetId, ContentHash, bytes_sha256};
use serde::Deserialize;
use serde_json::Value;
use std::collections::{BTreeMap, BTreeSet};

const COMMIT: &str = "1438b1abfcaee3267092b75573014f4d9b4a983c";
const PACKAGE: &str = "f3d89e2a8b92d7f99044b40500d28525a223955b28c1deffe0e9f1deeb8c5e73";
const MATERIALS: &str = "390950ef51469c5e906e4833272d7968127b1bb998629013d8b85ae7b8caebce";

#[must_use]
pub fn asset_id() -> AssetId {
    AssetId::deterministic(format!("tidewater/pier/{COMMIT}/0"))
}

#[must_use]
pub fn package_hash() -> ContentHash {
    PACKAGE.parse().expect("Pinned pier SHA-256 is valid")
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
    pier: Reference,
    license: Reference,
    files: BTreeMap<String, Entry>,
    qualification: String,
}

pub struct PierPackage {
    files: BTreeMap<String, Vec<u8>>,
}

impl PierPackage {
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
        check_reference(&index.pier, &files)?;
        check_reference(&index.license, &files)?;
        if index.pier.file != "pier.json"
            || index.license.file != "LICENSE-Tidewater.txt"
            || !files.get(&index.license.file).is_some_and(|license| {
                license.starts_with(b"MIT License")
                    && license
                        .windows(b"Permission is hereby granted".len())
                        .any(|window| window == b"Permission is hereby granted")
            })
        {
            return Err("Pier provenance is incomplete".into());
        }
        let descriptor: Value =
            serde_json::from_slice(&files[&index.pier.file]).map_err(|error| error.to_string())?;
        let pier = descriptor.get("pier").ok_or("Pier record absent")?;
        if descriptor["schema"] != "forge.tidewater.pier-frames"
            || descriptor["version"] != 1
            || descriptor["source_commit"] != COMMIT
            || descriptor["triangles"] != 36_194
            || pier["source_id"] != format!("tidewater:{COMMIT}:pier:0")
            || pier["kind"] != "pier"
            || pier["source_frame"]["translation_m"] != serde_json::json!([55.0, 0.0, -64.0])
            || pier["source_frame"]["rotation_xyzw"] != serde_json::json!([0, 0, 0, 1])
            || pier["source_frame"]["scale"] != serde_json::json!([1, 1, 1])
            || pier["source_layout"]["x"] != 55
            || pier["source_layout"]["zStart"] != -64
            || pier["colliders"]["boxes"]
                .as_array()
                .is_none_or(|rows| rows.len() != 122)
            || pier["colliders"]["cylinders"]
                .as_array()
                .is_none_or(|rows| rows.len() != 93)
        {
            return Err("Unexpected source pier description".into());
        }
        let static_parts = pier["static_parts"]
            .as_array()
            .ok_or("Pier static parts absent")?;
        let sign_parts = pier["sign"]["parts"].as_array().ok_or("Pier sign absent")?;
        let lanterns = pier["lanterns"].as_array().ok_or("Pier lanterns absent")?;
        if static_parts.len() != 4 || sign_parts.len() != 2 || lanterns.len() != 11 {
            return Err("Pier component count differs".into());
        }
        let mut referenced = BTreeSet::from([index.pier.file.clone(), index.license.file.clone()]);
        let mut triangles = 0;
        for (role, part) in static_parts
            .iter()
            .map(|part| ("static", part))
            .chain(sign_parts.iter().map(|part| ("swinging-sign", part)))
            .chain(lanterns.iter().flat_map(|lantern| {
                lantern["parts"]
                    .as_array()
                    .into_iter()
                    .flatten()
                    .map(|part| ("hanging-lantern", part))
            }))
        {
            triangles += validate_pier_part(role, part, &files, &mut referenced)?;
        }
        if triangles != 36_194 || referenced != files.keys().cloned().collect() {
            return Err("Pier geometry total or payload set differs".into());
        }
        Ok(Self { files })
    }

    #[must_use]
    pub fn description(&self) -> &[u8] {
        &self.files["pier.json"]
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

fn validate_pier_part(
    role: &str,
    part: &Value,
    files: &BTreeMap<String, Vec<u8>>,
    referenced: &mut BTreeSet<String>,
) -> Result<u64, String> {
    if part["source_role"] != role
        || !matches!(
            part["material"].as_str(),
            Some("material-0" | "material-1" | "material-6")
        )
        || part["vertices"]
            .as_u64()
            .is_none_or(|count| count == 0 || count > 100_000)
    {
        return Err("Pier part role or material differs".into());
    }
    let attrs = part["attributes"]
        .as_object()
        .ok_or("Pier attributes absent")?;
    if attrs.len() != 5
        || !["pos", "nrm", "uv", "tint", "data"]
            .into_iter()
            .all(|name| attrs.contains_key(name))
    {
        return Err("Pier attribute set differs".into());
    }
    for value in attrs.values().chain([
        &part["indices"],
        &part["reference_position"],
        &part["reference_normal"],
    ]) {
        let reference: Reference =
            serde_json::from_value(value.clone()).map_err(|error| error.to_string())?;
        if reference.file != format!("{}.bin", reference.sha256) {
            return Err("Pier payload is not content addressed".into());
        }
        check_reference(&reference, files)?;
        referenced.insert(reference.file);
    }
    let indices = &part["indices"];
    let bytes = indices["bytes"].as_u64().ok_or("Pier index size absent")?;
    if bytes == 0 || bytes % 12 != 0 {
        return Err("Pier triangle index size differs".into());
    }
    Ok(bytes / 12)
}

fn parse(manifest: &[u8], expected: ContentHash) -> Result<Manifest, String> {
    if manifest.len() > 64 * 1024
        || bytes_sha256(manifest) != expected
        || expected.to_string() != PACKAGE
    {
        return Err("Pier manifest pin mismatch or size limit".into());
    }
    let index: Manifest = serde_json::from_slice(manifest).map_err(|error| error.to_string())?;
    if index.schema != "forge.tidewater.pier-package"
        || index.version != 1
        || index.source_commit != COMMIT
        || index.building_material_library_sha256.to_string() != MATERIALS
        || index.qualification
            != "verified source geometry and portable payload; Studio rendering pending"
        || index.files.len() != 123
        || index.files.iter().any(|(name, entry)| {
            name.contains(['/', '\\', ':']) || entry.bytes == 0 || entry.bytes > 1024 * 1024
        })
        || index.files.values().map(|entry| entry.bytes).sum::<usize>() > 6 * 1024 * 1024
    {
        return Err("Unsupported pier package".into());
    }
    Ok(index)
}

fn verify(entry: &Entry, bytes: &[u8]) -> Result<(), String> {
    if bytes.len() != entry.bytes || bytes_sha256(bytes) != entry.sha256 {
        return Err("Pier payload size or pin mismatch".into());
    }
    Ok(())
}

fn check_reference(reference: &Reference, files: &BTreeMap<String, Vec<u8>>) -> Result<(), String> {
    let bytes = files
        .get(&reference.file)
        .ok_or("Pier dependency missing")?;
    if bytes.len() != reference.bytes || bytes_sha256(bytes) != reference.sha256 {
        return Err("Pier dependency pin mismatch".into());
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    #[allow(
        clippy::case_sensitive_file_extension_comparisons,
        reason = "The pinned pier manifest uses the exact lowercase .bin suffix."
    )]
    fn pinned_pier_package_rejects_changed_payload() {
        let Some(root) = std::env::var_os("FORGE_TIDEWATER_PIER_PACKAGE") else {
            return;
        };
        let root = std::path::PathBuf::from(root);
        let manifest = std::fs::read(root.join("package.json")).unwrap();
        let pin = bytes_sha256(&manifest);
        let package = PierPackage::load(&manifest, pin, |name, _| {
            std::fs::read(root.join(name)).map_err(|error| error.to_string())
        })
        .unwrap();
        let descriptor: Value = serde_json::from_slice(package.description()).unwrap();
        assert_eq!(descriptor["pier"]["lanterns"].as_array().unwrap().len(), 11);
        let mut tampered = false;
        assert!(
            PierPackage::load(&manifest, pin, |name, _| {
                let mut bytes =
                    std::fs::read(root.join(name)).map_err(|error| error.to_string())?;
                if !tampered && name.ends_with(".bin") {
                    bytes[0] ^= 1;
                    tampered = true;
                }
                Ok(bytes)
            })
            .is_err()
        );
        assert!(tampered);
    }
}
