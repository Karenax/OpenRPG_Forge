//! Hash-pinned Tidewater surface inputs. GPU outputs remain renderer derivatives.
use forge_core::{ContentHash, bytes_sha256};
use serde::Deserialize;
use std::collections::{BTreeMap, BTreeSet};

const COMMIT: &str = "1438b1abfcaee3267092b75573014f4d9b4a983c";
const SOURCE_BUNDLE: &str = "c739c737914261991fd9aba21c4372e7e9cafa31527f7a4139001a4db8e9b335";
const UNIFORMS: [(&str, usize); 8] = [
    ("water-frame.bin", 720),
    ("uniform-waterSurface.bin", 32),
    ("uniform-mat.bin", 80),
    ("uniform-skyParams.bin", 32),
    ("uniform-whaleMarks.bin", 96),
    ("uniform-shoreP.bin", 64),
    ("uniform-shoreSimP.bin", 48),
    ("uniform-seaDetailParams.bin", 32),
];

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
    files: BTreeMap<String, Entry>,
}

/// Immutable source shader, foam, shore and optical inputs for one water surface.
#[derive(Debug)]
pub struct WaterSurfacePackage {
    files: BTreeMap<String, Vec<u8>>,
}

impl WaterSurfacePackage {
    /// Load bounded, hash-verified bytes from a synchronous transport.
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

    /// Load the same package through Studio's sequential browser transport.
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
        "ocean-geometry.wgsl",
        "water-optics.wgsl",
        "shore-field.rgba32.bin",
        "sea-detail-noise.rgba16.bin",
        "LICENSE-Tidewater.txt",
    ]
    .into_iter()
    .map(str::to_owned)
    .collect();
    names.extend(UNIFORMS.map(|(name, _)| name.to_owned()));
    names.extend((0..11).map(|mip| format!("foam-m{mip}.rgba16.bin")));
    names.extend((0..10).map(|mip| format!("lace-m{mip}.rgba8.bin")));
    names
}

fn parse(bytes: &[u8], expected: ContentHash) -> Result<Manifest, String> {
    if bytes.len() > 12 * 1024 || bytes_sha256(bytes) != expected {
        return Err("Water surface manifest pin mismatch or size limit".into());
    }
    let manifest: Manifest = serde_json::from_slice(bytes).map_err(|e| e.to_string())?;
    if manifest.schema != "forge.tidewater.water-surface-package"
        || manifest.version != 1
        || manifest.source_commit != COMMIT
        || manifest.source_bundle_sha256.to_string() != SOURCE_BUNDLE
        || manifest.files.keys().cloned().collect::<BTreeSet<_>>() != required()
    {
        return Err("Unsupported or incomplete water surface package".into());
    }
    let mut total = 0_usize;
    for entry in manifest.files.values() {
        if entry.bytes == 0 || entry.bytes > 9 * 1024 * 1024 {
            return Err("Water surface payload size limit".into());
        }
        total = total
            .checked_add(entry.bytes)
            .ok_or("Water surface size overflow")?;
    }
    if total > 20 * 1024 * 1024 {
        return Err("Water surface package size limit".into());
    }
    Ok(manifest)
}

fn verify(entry: &Entry, bytes: &[u8]) -> Result<(), String> {
    if bytes.len() != entry.bytes || bytes_sha256(bytes) != entry.sha256 {
        return Err("Water surface size or pin mismatch".into());
    }
    Ok(())
}

fn validate_payloads(files: &BTreeMap<String, Vec<u8>>) -> Result<(), String> {
    for (name, length) in UNIFORMS {
        if files[name].len() != length {
            return Err(format!("Invalid water uniform size: {name}"));
        }
    }
    if files["shore-field.rgba32.bin"].len() != 512 * 512 * 16
        || files["sea-detail-noise.rgba16.bin"].len() != 256 * 256 * 8
    {
        return Err("Invalid water source texture size".into());
    }
    if !finite_f32(&files["shore-field.rgba32.bin"])
        || !finite_f16(&files["sea-detail-noise.rgba16.bin"])
        || !finite_f32(&files["uniform-seaDetailParams.bin"])
    {
        return Err("Nonfinite water source texture or detail parameters".into());
    }
    if files["water-frame.bin"][700..704] != [0; 4] || files["uniform-shoreP.bin"][52..56] != [0; 4]
    {
        return Err("Unsupported water frame padding or shore direction route".into());
    }
    for mip in 0..11 {
        let name = format!("foam-m{mip}.rgba16.bin");
        if files[&name].len() != (1024_usize >> mip).pow(2) * 8 || !finite_f16(&files[&name]) {
            return Err(format!("Invalid foam mip: {name}"));
        }
    }
    for mip in 0..10 {
        let name = format!("lace-m{mip}.rgba8.bin");
        if files[&name].len() != (512_usize >> mip).pow(2) * 4 {
            return Err(format!("Invalid shore lace mip: {name}"));
        }
    }
    for name in ["ocean-geometry.wgsl", "water-optics.wgsl"] {
        if std::str::from_utf8(&files[name])
            .map_err(|e| e.to_string())?
            .trim()
            .is_empty()
        {
            return Err(format!("Empty water shader: {name}"));
        }
    }
    if !std::str::from_utf8(&files["LICENSE-Tidewater.txt"])
        .map_err(|e| e.to_string())?
        .contains("MIT License")
    {
        return Err("Water source license missing".into());
    }
    Ok(())
}

fn finite_f32(bytes: &[u8]) -> bool {
    let (chunks, remainder) = bytes.as_chunks::<4>();
    remainder.is_empty()
        && chunks
            .iter()
            .all(|chunk| f32::from_le_bytes(*chunk).is_finite())
}

fn finite_f16(bytes: &[u8]) -> bool {
    let (chunks, remainder) = bytes.as_chunks::<2>();
    remainder.is_empty()
        && chunks
            .iter()
            .all(|chunk| u16::from_le_bytes(*chunk) & 0x7c00 != 0x7c00)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn manifest_requires_exact_source_and_file_set() {
        let manifest = serde_json::json!({
            "schema":"forge.tidewater.water-surface-package", "version":1,
            "source_commit":COMMIT, "source_bundle_sha256":SOURCE_BUNDLE,
            "files":{}
        });
        let bytes = serde_json::to_vec(&manifest).unwrap();
        assert!(
            parse(&bytes, bytes_sha256(&bytes))
                .unwrap_err()
                .contains("incomplete water surface package")
        );
        let forged = serde_json::json!({
            "schema":"forge.tidewater.water-surface-package", "version":1,
            "source_commit":COMMIT, "source_bundle_sha256":bytes_sha256(b"other"),
            "files":required().into_iter().map(|name| (name, serde_json::json!({
                "sha256":bytes_sha256(b"x"), "bytes":1
            }))).collect::<BTreeMap<_,_>>()
        });
        let bytes = serde_json::to_vec(&forged).unwrap();
        assert!(
            parse(&bytes, bytes_sha256(&bytes))
                .unwrap_err()
                .contains("incomplete water surface package")
        );
    }

    #[test]
    #[ignore = "requires the locally acquired Tidewater water surface package"]
    fn acquired_surface_inputs_match_the_pinned_source() {
        let root = std::path::PathBuf::from(
            std::env::var("FORGE_TIDEWATER_WATER_SURFACE_PACKAGE")
                .expect("set FORGE_TIDEWATER_WATER_SURFACE_PACKAGE"),
        );
        let manifest = std::fs::read(root.join("package.json")).unwrap();
        let expected = bytes_sha256(&manifest);
        assert_eq!(
            expected.to_string(),
            "307f7af005da3ba4c4b35e3268880ce4443ae19c363daa73a54beb0057abcd93"
        );
        let package = WaterSurfacePackage::load(&manifest, expected, |name, _| {
            std::fs::read(root.join(name)).map_err(|error| error.to_string())
        })
        .unwrap();
        assert!(package.file("water-optics.wgsl").unwrap().len() > 100_000);
        let error = WaterSurfacePackage::load(&manifest, expected, |name, _| {
            let mut bytes = std::fs::read(root.join(name)).map_err(|error| error.to_string())?;
            if name == "uniform-waterSurface.bin" {
                bytes[0] ^= 1;
            }
            Ok(bytes)
        })
        .unwrap_err();
        assert!(error.contains("pin mismatch"));
    }
}
