//! Transport-neutral, pinned terrain rendering resources. GPU qualification is separate.
use forge_core::{ContentHash, bytes_sha256};
use serde::Deserialize;
use std::collections::{BTreeMap, BTreeSet};
use std::sync::Arc;
#[derive(Clone, Debug, PartialEq, Eq, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ResourcePin {
    pub sha256: ContentHash,
    pub bytes: usize,
}
#[derive(Deserialize)]
struct ManifestHeader {
    schema: String,
    version: u32,
}
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct LegacyManifest {
    #[serde(rename = "schema")]
    _schema: String,
    #[serde(rename = "version")]
    _version: u32,
    source_commit: String,
    files: BTreeMap<String, ResourcePin>,
}
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct SurfaceManifest {
    #[serde(rename = "schema")]
    _schema: String,
    #[serde(rename = "version")]
    _version: u32,
    raster: RasterGeometry,
    textures: TextureDimensions,
    files: BTreeMap<String, ResourcePin>,
}
/// Verified terrain resources, independent of building assets and rendering.
#[derive(Clone)]
pub struct TerrainPackage {
    files: BTreeMap<String, Arc<[u8]>>,
    identity: ContentHash,
    geometry: RasterGeometry,
    textures: TextureDimensions,
    format: TerrainPackageFormat,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum RasterEncoding {
    Float32Le,
}

/// Spatial contract of the sampled raster paired with terrain resources.
/// The heights payload is acquired separately by its canonical zone pin.
#[derive(Clone, Copy, Debug, PartialEq, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct RasterGeometry {
    pub width: u32,
    pub height: u32,
    pub origin_xz_m: [f64; 2],
    pub sample_spacing_m: f64,
    pub outside_height_m: f32,
    pub encoding: RasterEncoding,
    pub extent_m: [f32; 2],
}

impl RasterGeometry {
    /// Compare the authored raster domain exactly, without substituting a
    /// compatible-looking package from another terrain. Encoding and content
    /// pins are admitted separately by their respective readers.
    #[must_use]
    pub fn matches_sampled_domain(
        self,
        dimensions: [u32; 2],
        origin_xz_m: [f64; 2],
        sample_spacing_m: f64,
        outside_height_m: f32,
        extent_m: [f32; 2],
        authored_spacing_m: f32,
    ) -> bool {
        [self.width, self.height] == dimensions
            && self.origin_xz_m.map(f64::to_bits) == origin_xz_m.map(f64::to_bits)
            && self.sample_spacing_m.to_bits() == sample_spacing_m.to_bits()
            && self.outside_height_m.to_bits() == outside_height_m.to_bits()
            && self.extent_m.map(f32::to_bits) == extent_m.map(f32::to_bits)
            && self.sample_spacing_m.to_bits() == f64::from(authored_spacing_m).to_bits()
    }
}

/// Full RGBA8 mip chains; normal maps match the sampled raster.
#[derive(Clone, Copy, Debug, PartialEq, Eq, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TextureDimensions {
    pub normal: [u32; 2],
    pub splat: [u32; 2],
    pub detail: [u32; 2],
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum TerrainPackageFormat {
    /// Historical transport contains the acquired programs and their licence.
    LegacySource,
    /// Only module-owned textures and their notice; programs belong to the engine.
    SurfaceData,
}

const SOURCE_TEXTURES: TextureDimensions = TextureDimensions {
    normal: [2048; 2],
    splat: [2048; 2],
    detail: [512; 2],
};

struct AdmittedManifest {
    files: BTreeMap<String, ResourcePin>,
    geometry: RasterGeometry,
    textures: TextureDimensions,
    format: TerrainPackageFormat,
}

const SOURCE_RASTER_GEOMETRY: RasterGeometry = RasterGeometry {
    width: 2048,
    height: 2048,
    origin_xz_m: [-1024.0; 2],
    sample_spacing_m: 1.0,
    outside_height_m: -90.0,
    encoding: RasterEncoding::Float32Le,
    extent_m: [2048.0; 2],
};

/// Declared capability of this fixed source provider, before loading its payloads.
/// Matching geometry is only a routing hint; package and height pins still require admission.
#[must_use]
pub const fn supported_raster_geometry() -> RasterGeometry {
    SOURCE_RASTER_GEOMETRY
}

impl TerrainPackage {
    /// Admit the complete bounded index before any payload transport is started.
    pub fn resource_pins(
        manifest: &[u8],
        expected: ContentHash,
    ) -> Result<BTreeMap<String, ResourcePin>, String> {
        Ok(parse(manifest, expected)?.files)
    }

    /// Read the validated spatial contract before requesting any payload.
    pub fn manifest_geometry(
        manifest: &[u8],
        expected: ContentHash,
    ) -> Result<RasterGeometry, String> {
        Ok(parse(manifest, expected)?.geometry)
    }

    /// The module notice is distinct from the engine's own source licences.
    pub fn manifest_license_name(
        manifest: &[u8],
        expected: ContentHash,
    ) -> Result<&'static str, String> {
        Ok(match parse(manifest, expected)?.format {
            TerrainPackageFormat::LegacySource => Self::license_name(),
            TerrainPackageFormat::SurfaceData => "LICENSE.txt",
        })
    }

    #[must_use]
    pub const fn license_name() -> &'static str {
        "LICENSE-Tidewater.txt"
    }

    /// The caller pins the manifest and bounds each transport read to the supplied size.
    pub fn load(
        manifest: &[u8],
        expected: ContentHash,
        mut read: impl FnMut(&str, usize) -> Result<Vec<u8>, String>,
    ) -> Result<Self, String> {
        let m = parse(manifest, expected)?;
        let mut files = BTreeMap::new();
        for (name, entry) in m.files {
            let bytes = read(&name, entry.bytes)?;
            verify(&name, &entry, &bytes)?;
            files.insert(name, Arc::<[u8]>::from(bytes));
        }
        Ok(Self {
            files,
            identity: expected,
            geometry: m.geometry,
            textures: m.textures,
            format: m.format,
        })
    }
    /// Sequential browser transport; failed payloads stop subsequent requests.
    pub async fn load_async<F, Fut>(
        manifest: &[u8],
        expected: ContentHash,
        mut read: F,
    ) -> Result<Self, String>
    where
        F: FnMut(String, usize) -> Fut,
        Fut: std::future::Future<Output = Result<Vec<u8>, String>>,
    {
        let m = parse(manifest, expected)?;
        let mut files = BTreeMap::new();
        for (name, entry) in m.files {
            let bytes = read(name.clone(), entry.bytes).await?;
            verify(&name, &entry, &bytes)?;
            files.insert(name, Arc::<[u8]>::from(bytes));
        }
        Ok(Self {
            files,
            identity: expected,
            geometry: m.geometry,
            textures: m.textures,
            format: m.format,
        })
    }
    pub fn file(&self, name: &str) -> Option<&[u8]> {
        self.files.get(name).map(AsRef::as_ref)
    }
    /// Hash of the admitted complete manifest, including every payload pin.
    #[must_use]
    pub const fn identity(&self) -> ContentHash {
        self.identity
    }
    #[must_use]
    pub fn file_count(&self) -> usize {
        self.files.len()
    }

    /// Available only from a package whose manifest and payloads were verified.
    #[must_use]
    pub const fn raster_geometry(&self) -> RasterGeometry {
        self.geometry
    }

    #[must_use]
    pub const fn texture_dimensions(&self) -> TextureDimensions {
        self.textures
    }

    #[must_use]
    pub const fn format(&self) -> TerrainPackageFormat {
        self.format
    }
}

fn verify(name: &str, entry: &ResourcePin, bytes: &[u8]) -> Result<(), String> {
    if bytes.len() != entry.bytes || bytes_sha256(bytes) != entry.sha256 {
        return Err("Terrain payload size or pin mismatch".into());
    }
    if !is_texture(name) {
        let text = std::str::from_utf8(bytes).map_err(|e| e.to_string())?;
        if text.trim().is_empty() {
            return Err(format!("Empty terrain text resource: {name}"));
        }
    }
    Ok(())
}
const RESOURCES: [(&str, usize); 6] = [
    ("terrain.wgsl", 1_000_000),
    ("terrain-lod.wgsl", 1_000_000),
    ("terrain-normal.bin", 22_369_620),
    ("terrain-splat.bin", 22_369_620),
    ("terrain-detail.bin", 1_398_100),
    ("LICENSE-Tidewater.txt", 32_768),
];
fn parse(bytes: &[u8], expected: ContentHash) -> Result<AdmittedManifest, String> {
    if bytes.len() > 16 * 1024 || bytes_sha256(bytes) != expected {
        return Err("Terrain manifest pin mismatch or size limit".into());
    }
    let header: ManifestHeader = serde_json::from_slice(bytes).map_err(|e| e.to_string())?;
    let (files, geometry, textures, format, resources) =
        match (header.schema.as_str(), header.version) {
            ("forge.tidewater.terrain-package", version @ (1 | 2)) => {
                let manifest: LegacyManifest =
                    serde_json::from_slice(bytes).map_err(|e| e.to_string())?;
                if manifest.source_commit != "1438b1abfcaee3267092b75573014f4d9b4a983c" {
                    return Err("Unsupported historical terrain package metadata".into());
                }
                let mut resources = RESOURCES.to_vec();
                if version == 2 {
                    resources.push(("hill-shadow.wgsl", 1_000_000));
                }
                (
                    manifest.files,
                    SOURCE_RASTER_GEOMETRY,
                    SOURCE_TEXTURES,
                    TerrainPackageFormat::LegacySource,
                    resources,
                )
            }
            ("forge.terrain-surface-package", 1) => {
                let manifest: SurfaceManifest =
                    serde_json::from_slice(bytes).map_err(|e| e.to_string())?;
                let geometry = manifest.raster;
                let textures = manifest.textures;
                validate_geometry(geometry)?;
                if textures.normal != [geometry.width, geometry.height] {
                    return Err("Terrain normal dimensions must match the raster".into());
                }
                let resources = vec![
                    ("terrain-normal.bin", mip_bytes(textures.normal)?),
                    ("terrain-splat.bin", mip_bytes(textures.splat)?),
                    ("terrain-detail.bin", mip_bytes(textures.detail)?),
                    ("LICENSE.txt", 32_768),
                ];
                (
                    manifest.files,
                    geometry,
                    textures,
                    TerrainPackageFormat::SurfaceData,
                    resources,
                )
            }
            _ => return Err("Unsupported or incomplete terrain package".into()),
        };
    let required: BTreeSet<_> = resources.iter().map(|(name, _)| *name).collect();
    if files.keys().map(String::as_str).collect::<BTreeSet<_>>() != required {
        return Err("Unsupported or incomplete terrain package".into());
    }
    // Validate the entire index before transport; binary sizes include every RGBA8 mip.
    for (name, limit) in resources {
        let entry = &files[name];
        if entry.bytes == 0 || entry.bytes > limit || (is_texture(name) && entry.bytes != limit) {
            return Err(format!("Invalid terrain resource size: {name}"));
        }
    }
    Ok(AdmittedManifest {
        files,
        geometry,
        textures,
        format,
    })
}

fn validate_geometry(geometry: RasterGeometry) -> Result<(), String> {
    if !(2..=4096).contains(&geometry.width)
        || !(2..=4096).contains(&geometry.height)
        || !geometry.sample_spacing_m.is_finite()
        || geometry.sample_spacing_m <= 0.0
        || !geometry.outside_height_m.is_finite()
    {
        return Err("Invalid sampled terrain geometry".into());
    }
    #[allow(clippy::cast_possible_truncation)] // Match the persisted f32 document contract exactly.
    let spacing = geometry.sample_spacing_m as f32;
    if !spacing.is_normal() || f64::from(spacing).to_bits() != geometry.sample_spacing_m.to_bits() {
        return Err("Terrain spacing exceeds document precision".into());
    }
    for (axis, count) in [geometry.width, geometry.height].into_iter().enumerate() {
        let extent = geometry.extent_m[axis];
        if !(extent * 0.5).is_normal()
            || extent <= 0.0
            || (f64::from(count) * geometry.sample_spacing_m).to_bits()
                != f64::from(extent).to_bits()
            || geometry.origin_xz_m[axis].to_bits() != (-f64::from(extent) * 0.5).to_bits()
        {
            return Err("Terrain raster does not match its centred document domain".into());
        }
    }
    Ok(())
}

fn mip_bytes(size: [u32; 2]) -> Result<usize, String> {
    if size.iter().any(|axis| !(1..=4096).contains(axis)) {
        return Err("Invalid terrain texture dimensions".into());
    }
    let levels = size[0].max(size[1]).ilog2() + 1;
    Ok((0..levels)
        .map(|level| ((size[0] >> level).max(1) * (size[1] >> level).max(1) * 4) as usize)
        .sum())
}

fn is_texture(name: &str) -> bool {
    matches!(
        name,
        "terrain-normal.bin" | "terrain-splat.bin" | "terrain-detail.bin"
    )
}

#[cfg(test)]
mod shared_payload_tests {
    use super::*;
    #[test]
    fn a_cloned_package_shares_immutable_payloads_and_retains_them_after_the_origin_is_dropped() {
        let bytes: Arc<[u8]> = Arc::from([1_u8, 2, 3]);
        let original = TerrainPackage {
            files: BTreeMap::from([("payload".into(), bytes.clone())]),
            identity: bytes_sha256(b"test manifest"),
            geometry: SOURCE_RASTER_GEOMETRY,
            textures: SOURCE_TEXTURES,
            format: TerrainPackageFormat::LegacySource,
        };
        let cloned = original.clone();
        assert_eq!(original.identity(), cloned.identity());
        assert!(Arc::ptr_eq(
            &original.files["payload"],
            &cloned.files["payload"]
        ));
        assert_eq!(Arc::strong_count(&bytes), 3);
        drop(original);
        assert_eq!(cloned.file("payload"), Some(&[1, 2, 3][..]));
        assert_eq!(Arc::strong_count(&bytes), 2);
    }
}
