//! Derived raster evaluator of the canonical terrain document, not another document.
use super::{RasterError, SampledTerrain};
use forge_core::ContentHash;
use forge_project::{TerrainDocument, TerrainDocumentError};
use std::collections::BTreeMap;
use thiserror::Error;

#[derive(Debug)]
pub struct SampledTerrainPlan {
    document: TerrainDocument,
    document_hash: ContentHash,
    raster: SampledTerrain,
    corrections: BTreeMap<(u16, u16), f64>,
}

impl SampledTerrainPlan {
    /// Validated raster, borrowed immutably by projection adapters.
    #[must_use]
    pub const fn raster(&self) -> &SampledTerrain {
        &self.raster
    }
    /// Caller resolves immutable bytes from the document's content hash.
    /// Missing/corrupt content never falls back to procedural generation.
    pub fn from_document(
        document: &TerrainDocument,
        bytes: &[u8],
    ) -> Result<Self, SampledTerrainPlanError> {
        document.validate()?;
        let source = document
            .sampled_source
            .clone()
            .ok_or(SampledTerrainPlanError::MissingSource)?;
        let raster = SampledTerrain::from_le_bytes(source, bytes)?;
        let max_base = raster
            .heights
            .iter()
            .copied()
            .map(f64::from)
            .map(f64::abs)
            .fold(f64::from(raster.source.outside_height_m).abs(), f64::max);
        let max_delta = document
            .height_deltas
            .iter()
            .map(|d| f64::from(d.delta_m).abs())
            .fold(0.0, f64::max);
        if max_base + max_delta + f64::from(document.height_sampling.base_elevation_m).abs()
            > f64::from(f32::MAX)
        {
            return Err(SampledTerrainPlanError::HeightRange);
        }
        Ok(Self {
            document: document.clone(),
            document_hash: document.content_hash()?,
            raster,
            corrections: document
                .height_deltas
                .iter()
                .map(|d| ((d.sample_x, d.sample_z), f64::from(d.delta_m)))
                .collect(),
        })
    }

    #[must_use]
    pub const fn document(&self) -> &TerrainDocument {
        &self.document
    }

    #[must_use]
    pub const fn document_hash(&self) -> ContentHash {
        self.document_hash
    }

    /// Derived samples for normal maps and coarse shadow pyramids. These are not
    /// a replacement canonical source: resampling corner deltas onto centres
    /// cannot represent every correction between cells. Unlike `height_at`, this
    /// preserves the actual source texels on the final row and column.
    #[must_use]
    #[allow(clippy::cast_possible_truncation)] // Validated grid <=4096 and finite f32 range.
    pub fn cell_center_heights(&self) -> Vec<f32> {
        let mut heights = self.raster.heights.clone();
        let elevation = f64::from(self.document.height_sampling.base_elevation_m);
        if elevation == 0.0 && self.corrections.is_empty() {
            return heights;
        }
        let width = self.raster.source.width as usize;
        let delta = |x, z| self.corrections.get(&(x, z)).copied().unwrap_or(0.0);
        for (index, height) in heights.iter_mut().enumerate() {
            let x = (index % width) as u16;
            let z = (index / width) as u16;
            let correction =
                (delta(x, z) + delta(x + 1, z) + delta(x, z + 1) + delta(x + 1, z + 1)) * 0.25;
            if correction != 0.0 || elevation != 0.0 {
                *height = (f64::from(*height) + elevation + correction) as f32;
            }
        }
        heights
    }

    /// Immutable cell-centred height plus authored corner-grid corrections.
    /// Source and correction grids are intentionally not conflated.
    #[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
    pub fn height_at(&self, x: f64, z: f64) -> Result<f64, SampledTerrainPlanError> {
        let base = self.raster.height_at(x, z)?
            + f64::from(self.document.height_sampling.base_elevation_m);
        let source = &self.raster.source;
        let gx = (x - source.origin_xz_m[0]) / source.sample_spacing_m;
        let gz = (z - source.origin_xz_m[1]) / source.sample_spacing_m;
        if gx < 0.0 || gz < 0.0 || gx > f64::from(source.width) || gz > f64::from(source.height) {
            return Ok(base);
        }
        let ix = (gx.floor() as u16).min(source.width as u16 - 1);
        let iz = (gz.floor() as u16).min(source.height as u16 - 1);
        let tx = gx - f64::from(ix);
        let tz = gz - f64::from(iz);
        let d = |x, z| self.corrections.get(&(x, z)).copied().unwrap_or(0.0);
        Ok(base
            + (d(ix, iz) * (1.0 - tx) + d(ix + 1, iz) * tx) * (1.0 - tz)
            + (d(ix, iz + 1) * (1.0 - tx) + d(ix + 1, iz + 1) * tx) * tz)
    }
}

#[derive(Debug, Error)]
pub enum SampledTerrainPlanError {
    #[error(transparent)]
    Document(#[from] TerrainDocumentError),
    #[error(transparent)]
    Raster(#[from] RasterError),
    #[error("terrain document has no immutable raster source")]
    MissingSource,
    #[error("raster and corrections exceed finite float32 height range")]
    HeightRange,
    #[error("sampled terrain patch exceeds the source domain or 128 intervals per axis")]
    PatchBounds,
}
