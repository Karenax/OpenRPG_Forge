//! Immutable sampled terrain source metadata. No path or renderer handle.
//!
//! The owning zone will reference this source by hash. This foundation does not
//! change existing `TerrainDocument` encodings or make imported zones editable yet.
use forge_core::ContentHash;
use serde::{Deserialize, Serialize};
use thiserror::Error;

pub const SAMPLED_TERRAIN_SCHEMA: &str = "forge.sampled-terrain";
pub const MAX_SAMPLED_TERRAIN_AXIS: u32 = 4096;

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum HeightEncoding {
    Float32Le,
}

/// Grid origin denotes the minimum cell corner; samples lie at cell centres.
/// Bilinear sampling uses the half-open interior between first and last centres.
/// Outside that region the explicit outside height applies. No silent clamping.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SampledTerrainSource {
    pub schema: String,
    pub schema_version: u32,
    pub width: u32,
    pub height: u32,
    pub origin_xz_m: [f64; 2],
    pub sample_spacing_m: f64,
    pub outside_height_m: f32,
    pub encoding: HeightEncoding,
    pub samples_sha256: ContentHash,
}

impl SampledTerrainSource {
    pub fn validate(&self) -> Result<(), SampledTerrainError> {
        if self.schema != SAMPLED_TERRAIN_SCHEMA || self.schema_version != 1 {
            return Err(SampledTerrainError::Schema);
        }
        if !(2..=MAX_SAMPLED_TERRAIN_AXIS).contains(&self.width)
            || !(2..=MAX_SAMPLED_TERRAIN_AXIS).contains(&self.height)
        {
            return Err(SampledTerrainError::Dimensions);
        }
        if !self.sample_spacing_m.is_finite()
            || self.sample_spacing_m <= 0.0
            || !self.outside_height_m.is_finite()
            || self.origin_xz_m.iter().any(|v| !v.is_finite())
        {
            return Err(SampledTerrainError::Coordinates);
        }
        for (origin, count) in self.origin_xz_m.iter().zip([self.width, self.height]) {
            let end = origin + f64::from(count) * self.sample_spacing_m;
            // Reject overflow and grids whose spacing vanishes at this origin.
            if !end.is_finite() || end <= *origin || origin + self.sample_spacing_m <= *origin {
                return Err(SampledTerrainError::Coordinates);
            }
        }
        Ok(())
    }

    pub fn byte_length(&self) -> Result<usize, SampledTerrainError> {
        self.validate()?;
        // Dimensions above bound this to 64 MiB on both native and WASM32.
        Ok(self.width as usize * self.height as usize * 4)
    }
}

#[derive(Clone, Copy, Debug, Error, PartialEq, Eq)]
pub enum SampledTerrainError {
    #[error("unsupported sampled terrain schema or version")]
    Schema,
    #[error("sampled terrain axes must be between 2 and 4096 samples")]
    Dimensions,
    #[error("invalid sampled terrain origin, spacing or outside height")]
    Coordinates,
}
