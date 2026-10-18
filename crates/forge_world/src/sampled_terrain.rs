//! Validated read-only height raster, shared by CPU collision and render adapters.
use forge_core::bytes_sha256;
use forge_project::sampled_terrain::{SampledTerrainError, SampledTerrainSource};
use thiserror::Error;

mod plan;
pub use plan::{SampledTerrainPlan, SampledTerrainPlanError};
mod mesh;
pub use mesh::SampledTerrainPatch;

#[derive(Debug)]
pub struct SampledTerrain {
    source: SampledTerrainSource,
    heights: Vec<f32>,
}

impl SampledTerrain {
    pub fn from_le_bytes(source: SampledTerrainSource, bytes: &[u8]) -> Result<Self, RasterError> {
        let expected = source.byte_length()?;
        if bytes.len() != expected {
            return Err(RasterError::Length {
                expected,
                actual: bytes.len(),
            });
        }
        if bytes_sha256(bytes) != source.samples_sha256 {
            return Err(RasterError::Hash);
        }
        let mut heights = Vec::with_capacity(bytes.len() / 4);
        for (index, chunk) in bytes.as_chunks::<4>().0.iter().enumerate() {
            let value = f32::from_le_bytes(*chunk);
            if !value.is_finite() {
                return Err(RasterError::NonFiniteHeight(index));
            }
            heights.push(value);
        }
        Ok(Self { source, heights })
    }

    #[must_use]
    pub const fn source(&self) -> &SampledTerrainSource {
        &self.source
    }

    /// Validated row-major samples, exposed read-only to renderer adapters.
    #[must_use]
    pub fn heights(&self) -> &[f32] {
        &self.heights
    }

    /// Uses double precision interpolation of exact float32 samples, preserving
    /// the source's cell-centred coordinates through a JavaScript-to-Rust import.
    #[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
    pub fn height_at(&self, x: f64, z: f64) -> Result<f64, RasterError> {
        if !x.is_finite() || !z.is_finite() {
            return Err(RasterError::Query);
        }
        let fx = (x - self.source.origin_xz_m[0]) / self.source.sample_spacing_m - 0.5;
        let fz = (z - self.source.origin_xz_m[1]) / self.source.sample_spacing_m - 0.5;
        if !fx.is_finite()
            || !fz.is_finite()
            || fx < 0.0
            || fz < 0.0
            || fx >= f64::from(self.source.width - 1)
            || fz >= f64::from(self.source.height - 1)
        {
            return Ok(f64::from(self.source.outside_height_m));
        }
        // Above checks prove nonnegative, bounded integer indices on WASM too.
        let ix = fx.floor();
        let iz = fz.floor();
        let tx = fx - ix;
        let tz = fz - iz;
        let stride = self.source.width as usize;
        let index = iz as usize * stride + ix as usize;
        let top_left = f64::from(self.heights[index]);
        let top_right = f64::from(self.heights[index + 1]);
        let bottom_left = f64::from(self.heights[index + stride]);
        let bottom_right = f64::from(self.heights[index + stride + 1]);
        Ok((top_left * (1.0 - tx) + top_right * tx) * (1.0 - tz)
            + (bottom_left * (1.0 - tx) + bottom_right * tx) * tz)
    }
}

#[derive(Debug, Error, PartialEq, Eq)]
pub enum RasterError {
    #[error(transparent)]
    Source(#[from] SampledTerrainError),
    #[error("height raster byte length: expected {expected}, found {actual}")]
    Length { expected: usize, actual: usize },
    #[error("height raster does not match its approved SHA-256")]
    Hash,
    #[error("non-finite height sample at index {0}")]
    NonFiniteHeight(usize),
    #[error("non-finite height query")]
    Query,
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_project::sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA};

    fn fixture(values: [f32; 4]) -> (SampledTerrainSource, Vec<u8>) {
        let bytes: Vec<_> = values.into_iter().flat_map(f32::to_le_bytes).collect();
        (
            SampledTerrainSource {
                schema: SAMPLED_TERRAIN_SCHEMA.into(),
                schema_version: 1,
                width: 2,
                height: 2,
                origin_xz_m: [-1.0, -1.0],
                sample_spacing_m: 1.0,
                outside_height_m: -90.0,
                encoding: HeightEncoding::Float32Le,
                samples_sha256: bytes_sha256(&bytes),
            },
            bytes,
        )
    }

    #[test]
    #[allow(clippy::float_cmp)] // Binary fractions and outside sentinel are exactly representable.
    fn cell_centres_and_bilinear_interpolation_preserve_alignment() {
        let (source, bytes) = fixture([0.0, 4.0, 8.0, 12.0]);
        let terrain = SampledTerrain::from_le_bytes(source, &bytes).unwrap();
        assert_eq!(terrain.height_at(-0.5, -0.5).unwrap(), 0.0);
        assert_eq!(terrain.height_at(0.0, 0.0).unwrap(), 6.0);
        assert_eq!(terrain.height_at(-0.25, 0.0).unwrap(), 5.0);
        assert_eq!(terrain.height_at(-0.500_001, 0.0).unwrap(), -90.0);
        assert_eq!(terrain.height_at(0.5, 0.0).unwrap(), -90.0);
        assert_eq!(terrain.height_at(0.0, 0.5).unwrap(), -90.0);
        assert_eq!(terrain.height_at(f64::MAX, 0.0).unwrap(), -90.0);
        assert_eq!(terrain.height_at(f64::NAN, 0.0), Err(RasterError::Query));
    }

    #[test]
    fn rejects_truncation_corruption_and_nonfinite_payloads() {
        let (source, mut bytes) = fixture([0.0, 1.0, 2.0, 3.0]);
        assert!(matches!(
            SampledTerrain::from_le_bytes(source.clone(), &bytes[..15]),
            Err(RasterError::Length { .. })
        ));
        bytes[0] ^= 1;
        assert!(matches!(
            SampledTerrain::from_le_bytes(source, &bytes),
            Err(RasterError::Hash)
        ));
        for invalid in [f32::NAN, f32::INFINITY, f32::NEG_INFINITY] {
            let (source, bytes) = fixture([0.0, invalid, 2.0, 3.0]);
            assert!(matches!(
                SampledTerrain::from_le_bytes(source, &bytes),
                Err(RasterError::NonFiniteHeight(1))
            ));
        }
    }

    #[test]
    fn source_round_trip_is_strict_and_versioned() {
        let (mut source, bytes) = fixture([0.0, 1.0, 2.0, 3.0]);
        let encoded = serde_json::to_string(&source).unwrap();
        assert_eq!(
            serde_json::from_str::<SampledTerrainSource>(&encoded).unwrap(),
            source
        );
        let mut value = serde_json::to_value(&source).unwrap();
        value["hidden_path"] = "C:/unexpected".into();
        assert!(serde_json::from_value::<SampledTerrainSource>(value).is_err());
        source.schema_version = 2;
        assert!(matches!(
            SampledTerrain::from_le_bytes(source, &bytes),
            Err(RasterError::Source(SampledTerrainError::Schema))
        ));
    }

    #[test]
    fn rejects_unbounded_grids_and_unrepresentable_coordinates_before_allocation() {
        let (mut source, _) = fixture([0.0, 1.0, 2.0, 3.0]);
        source.width = u32::MAX;
        assert_eq!(source.byte_length(), Err(SampledTerrainError::Dimensions));
        source.width = 2;
        source.origin_xz_m = [f64::MAX, 0.0];
        assert_eq!(source.validate(), Err(SampledTerrainError::Coordinates));
        source.origin_xz_m = [0.0, 0.0];
        source.sample_spacing_m = 0.0;
        assert_eq!(source.validate(), Err(SampledTerrainError::Coordinates));
    }
}
