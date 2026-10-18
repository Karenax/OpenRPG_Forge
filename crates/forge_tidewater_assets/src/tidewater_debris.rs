//! Immutable Debris acquisition package, independent of ECS and editable documents.
//! Loading this intermediate data does not qualify its shaders or Studio rendering.
use forge_core::{AssetId, ContentHash, bytes_sha256};
use serde::Deserialize;
use serde_json::Value;
use std::collections::{BTreeMap, BTreeSet};

pub mod collisions;
pub mod native;
pub mod pebble_program;
pub mod scanned;
pub mod scanned_input;

const COMMIT: &str = "1438b1abfcaee3267092b75573014f4d9b4a983c";
const PACKAGE: &str = "0a84592f5ccaa0b8077849b6cf054935f6ecb33f41aaf04a19cec3b42255189a";

/// Immutable intermediate acquisition revision, not a visual or licence qualification.
///
/// # Panics
/// Panics only if the compiled SHA-256 literal is invalid.
#[must_use]
pub fn package_hash() -> ContentHash {
    PACKAGE.parse().expect("Pinned Debris SHA-256 is valid")
}

#[must_use]
pub fn asset_id(ordinal: usize) -> Option<AssetId> {
    (ordinal < 805)
        .then(|| AssetId::deterministic(format!("tidewater:{COMMIT}:debris-cpu-owner:{ordinal}")))
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Entry {
    bytes: usize,
    sha256: ContentHash,
}

// Geometry references additionally carry attribute dimensions and scalar types.
#[derive(Deserialize)]
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
    frames: Reference,
    materials: Reference,
    pebble_field: Reference,
    license: Reference,
    credits: Reference,
    unattached_source_colliders: Reference,
    files: BTreeMap<String, Entry>,
    qualification: String,
}

/// Verified immutable payloads; authoring placements are deliberately separate.
pub struct DebrisPackage {
    files: BTreeMap<String, Vec<u8>>,
    frames: Value,
    materials: Value,
    pebble_field: Value,
}

impl DebrisPackage {
    /// Acquire a complete, hash-pinned package through an injected transport.
    ///
    /// # Errors
    /// Rejects malformed manifests, altered payloads, invalid geometry and open references.
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

    /// Acquire the same package through an asynchronous native/browser transport.
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
        let mut referenced = BTreeSet::new();
        for reference in [
            &index.frames,
            &index.materials,
            &index.pebble_field,
            &index.license,
            &index.credits,
            &index.unattached_source_colliders,
        ] {
            check_reference(reference, index, &files, &mut referenced)?;
        }
        if !files[&index.license.file].starts_with(b"MIT License") {
            return Err("Debris license missing".into());
        }
        let json = |reference: &Reference| {
            serde_json::from_slice::<Value>(&files[&reference.file]).map_err(|e| e.to_string())
        };
        let frames = json(&index.frames)?;
        let materials = json(&index.materials)?;
        let pebble_field = json(&index.pebble_field)?;
        if frames["schema"] != "forge.tidewater.debris-frames"
            || frames["version"] != 1
            || frames["source_commit"] != COMMIT
            || frames["source_triangles"] != 112_669
            || frames["shadow_triangles"] != 50_522
            || frames["scanned_instances"]
                .as_array()
                .is_none_or(|rows| rows.len() != 141)
        {
            return Err("Unsupported Debris frame description".into());
        }
        for value in [&frames, &materials, &pebble_field] {
            visit_references(value, index, &files, &mut referenced)?;
        }
        let rows = frames["props"].as_array().ok_or("Debris props absent")?;
        if rows.len() != 805 {
            return Err("Debris inventory differs".into());
        }
        let mut triangles = 0;
        let mut shadow_triangles = 0;
        for (ordinal, row) in rows.iter().enumerate() {
            let (main, shadow) = validate_row(ordinal, row, &files)?;
            triangles += main;
            shadow_triangles += shadow;
        }
        if triangles != 112_669
            || shadow_triangles != 50_522
            || referenced != files.keys().cloned().collect()
        {
            return Err("Debris triangle counts or reference closure differ".into());
        }
        Ok(Self {
            files,
            frames,
            materials,
            pebble_field,
        })
    }

    #[must_use]
    pub fn frames(&self) -> &Value {
        &self.frames
    }

    #[must_use]
    pub fn source_materials(&self) -> &Value {
        &self.materials
    }

    #[must_use]
    pub fn pebble_field(&self) -> &Value {
        &self.pebble_field
    }

    #[must_use]
    pub fn file(&self, name: &str) -> Option<&[u8]> {
        self.files.get(name).map(Vec::as_slice)
    }
}

fn parse(bytes: &[u8], expected: ContentHash) -> Result<Manifest, String> {
    if bytes.len() > 1024 * 1024
        || bytes_sha256(bytes) != expected
        || expected.to_string() != PACKAGE
    {
        return Err("Debris manifest pin or size differs".into());
    }
    let index: Manifest = serde_json::from_slice(bytes).map_err(|e| e.to_string())?;
    let total = index
        .files
        .values()
        .try_fold(0_usize, |sum, entry| sum.checked_add(entry.bytes));
    if index.schema != "forge.tidewater.debris-package"
        || index.version != 1
        || index.source_commit != COMMIT
        || index.files.len() != 4904
        || index.qualification
            != "intermediate portable frames; no runtime/Studio/GLB qualification"
        || total.is_none_or(|size| size > 32 * 1024 * 1024)
        || index.files.iter().any(|(name, entry)| {
            name != &format!("{}.bin", entry.sha256) || entry.bytes > 8 * 1024 * 1024
        })
    {
        return Err("Unsupported Debris package".into());
    }
    Ok(index)
}

fn verify(entry: &Entry, bytes: &[u8]) -> Result<(), String> {
    if bytes.len() != entry.bytes || bytes_sha256(bytes) != entry.sha256 {
        return Err("Debris payload pin or size differs".into());
    }
    Ok(())
}

fn check_reference(
    reference: &Reference,
    index: &Manifest,
    files: &BTreeMap<String, Vec<u8>>,
    referenced: &mut BTreeSet<String>,
) -> Result<(), String> {
    let entry = index
        .files
        .get(&reference.file)
        .ok_or("Debris reference missing")?;
    if entry.bytes != reference.bytes
        || entry.sha256 != reference.sha256
        || !files.contains_key(&reference.file)
    {
        return Err("Debris reference pin differs".into());
    }
    referenced.insert(reference.file.clone());
    Ok(())
}

fn visit_references(
    value: &Value,
    index: &Manifest,
    files: &BTreeMap<String, Vec<u8>>,
    referenced: &mut BTreeSet<String>,
) -> Result<(), String> {
    match value {
        Value::Object(object) => {
            if object.contains_key("file") {
                let reference: Reference =
                    serde_json::from_value(value.clone()).map_err(|e| e.to_string())?;
                check_reference(&reference, index, files, referenced)?;
            }
            for child in object.values() {
                visit_references(child, index, files, referenced)?;
            }
        }
        Value::Array(array) => {
            for child in array {
                visit_references(child, index, files, referenced)?;
            }
        }
        _ => {}
    }
    Ok(())
}

fn payload<'a>(
    reference: &Value,
    files: &'a BTreeMap<String, Vec<u8>>,
) -> Result<&'a [u8], String> {
    let name = reference["file"]
        .as_str()
        .ok_or("Debris payload name absent")?;
    files
        .get(name)
        .map(Vec::as_slice)
        .ok_or_else(|| "Debris payload absent".into())
}

fn validate_row(
    ordinal: usize,
    row: &Value,
    files: &BTreeMap<String, Vec<u8>>,
) -> Result<(usize, usize), String> {
    if row["source_id"] != format!("tidewater:{COMMIT}:debris-cpu-owner:{ordinal}")
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
    {
        return Err("Debris source identity or frame differs".into());
    }
    let parts = row["parts"].as_array().ok_or("Debris parts absent")?;
    if parts.is_empty() || parts.len() > 5 {
        return Err("Debris part count differs".into());
    }
    let mut main = 0;
    let mut shadow = 0;
    for part in parts {
        let vertices = part["vertices"]
            .as_u64()
            .ok_or("Debris vertex count absent")?;
        if vertices == 0
            || vertices > 101_850
            || !matches!(
                part["material"].as_str(),
                Some("wood" | "hard" | "fabric" | "nature")
            )
        {
            return Err("Debris vertex count or material differs".into());
        }
        for (name, width) in [("pos", 3), ("nrm", 3), ("uv", 2), ("tint", 3), ("data", 4)] {
            let bytes = payload(&part["attributes"][name], files)?;
            if bytes.len() as u64 != vertices * width * 4
                || bytes
                    .as_chunks::<4>()
                    .0
                    .iter()
                    .any(|b| !f32::from_le_bytes(*b).is_finite())
            {
                return Err("Invalid Debris float attribute".into());
            }
        }
        let positions = payload(&part["reference_position"], files)?;
        if positions.len() as u64 != vertices * 12 {
            return Err("Debris source positions differ".into());
        }
        validate_positions(row, part, files)?;
        for (key, count) in [("indices", &mut main), ("shadow_indices", &mut shadow)] {
            let bytes = payload(&part[key], files)?;
            if !bytes.len().is_multiple_of(12)
                || (key == "indices" && bytes.is_empty())
                || bytes
                    .as_chunks::<4>()
                    .0
                    .iter()
                    .any(|b| u64::from(u32::from_le_bytes(*b)) >= vertices)
            {
                return Err("Invalid Debris triangle indices".into());
            }
            *count += bytes.len() / 12;
        }
    }
    Ok((main, shadow))
}

fn validate_positions(
    row: &Value,
    part: &Value,
    files: &BTreeMap<String, Vec<u8>>,
) -> Result<(), String> {
    let local = payload(&part["attributes"]["pos"], files)?;
    let reference = payload(&part["reference_position"], files)?;
    if local.len() != reference.len() {
        return Err("Debris position streams differ".into());
    }
    let frame = &row["source_frame"]["translation_m"];
    let anchor = [
        frame[0].as_f64().ok_or("Debris pivot X absent")?,
        frame[1].as_f64().ok_or("Debris pivot Y absent")?,
        frame[2].as_f64().ok_or("Debris pivot Z absent")?,
    ];
    for (i, (local, source)) in local
        .as_chunks::<4>()
        .0
        .iter()
        .zip(reference.as_chunks::<4>().0.iter())
        .enumerate()
    {
        let source = f64::from(f32::from_le_bytes(*source));
        let restored = f64::from(f32::from_le_bytes(*local)) + anchor[i % 3];
        if !source.is_finite() || (source - restored).abs() >= 0.0001 {
            return Err("Debris source position roundtrip differs".into());
        }
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn invalid_manifest_is_rejected_before_transport() {
        let expected: ContentHash = PACKAGE.parse().unwrap();
        assert!(
            DebrisPackage::load(b"{}", expected, |_, _| {
                panic!("Invalid manifest must not start transport")
            })
            .is_err()
        );
        assert!(asset_id(804).is_some());
        assert!(asset_id(805).is_none());
    }

    #[test]
    #[ignore = "Requires the real hash-pinned Debris frame acquisition"]
    fn pinned_package_loads_and_rejects_tampering() {
        let root = std::path::PathBuf::from(
            std::env::var_os("FORGE_DEBRIS_PACKAGE_FIXTURE")
                .expect("Explicit real Debris fixture required"),
        );
        let manifest = std::fs::read(root.join("package.json")).unwrap();
        let expected: ContentHash = PACKAGE.parse().unwrap();
        let package = DebrisPackage::load(&manifest, expected, |name, _| {
            std::fs::read(root.join(name)).map_err(|error| error.to_string())
        })
        .unwrap();
        assert_eq!(package.frames()["props"].as_array().unwrap().len(), 805);
        assert_eq!(
            package.source_materials()["source_contracts"]
                .as_object()
                .unwrap()
                .len(),
            5
        );
        assert_eq!(package.pebble_field()["occupied_cells"], 10_415);
        assert!(package.file("absent.bin").is_none());
        let mut invalid_frame = package.frames()["props"][0].clone();
        invalid_frame["source_frame"]["translation_m"][0] = serde_json::json!(100_000);
        assert!(validate_row(0, &invalid_frame, &package.files).is_err());
        for (ordinal, row) in package.frames()["props"]
            .as_array()
            .unwrap()
            .iter()
            .enumerate()
        {
            assert_eq!(
                asset_id(ordinal),
                Some(AssetId::deterministic(row["source_id"].as_str().unwrap()))
            );
        }
        let calls = std::cell::Cell::new(0);
        let future = DebrisPackage::load_async(&manifest, expected, |name, _| {
            calls.set(calls.get() + 1);
            std::future::ready(std::fs::read(root.join(name)).map_err(|error| error.to_string()))
        });
        let mut future = std::pin::pin!(future);
        let mut context = std::task::Context::from_waker(std::task::Waker::noop());
        let async_package = match std::future::Future::poll(future.as_mut(), &mut context) {
            std::task::Poll::Ready(result) => result.unwrap(),
            std::task::Poll::Pending => panic!("Immediate fixture transport must complete"),
        };
        assert_eq!(calls.get(), 4904);
        assert_eq!(async_package.frames(), package.frames());
        for (name, bytes) in &package.files {
            assert_eq!(async_package.file(name), Some(bytes.as_slice()));
        }
        let mut changed = false;
        assert!(
            DebrisPackage::load(&manifest, expected, |name, _| {
                let mut bytes =
                    std::fs::read(root.join(name)).map_err(|error| error.to_string())?;
                if !changed && !bytes.is_empty() {
                    bytes[0] ^= 1;
                    changed = true;
                }
                Ok(bytes)
            })
            .is_err()
        );
        assert!(changed);
        assert!(
            DebrisPackage::load(&manifest, expected, |_, _| Err("Missing payload".into())).is_err()
        );
    }
}
