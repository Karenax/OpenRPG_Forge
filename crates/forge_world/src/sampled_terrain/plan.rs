//! Derived raster evaluator of the canonical terrain document, not another document.
use super::{RasterError, SampledTerrain};
use forge_core::ContentHash;
use forge_project::{TerrainDocument, TerrainDocumentError};
use std::collections::BTreeMap;
use std::sync::Arc;
use thiserror::Error;

#[derive(Debug)]
pub struct SampledTerrainPlan {
    document: TerrainDocument,
    document_hash: ContentHash,
    raster: Arc<SampledTerrain>,
    maximum_base_height: f64,
    corrections: BTreeMap<(u16, u16), f64>,
}

impl SampledTerrainPlan {
    /// Only an explicit authored stroke; no generated biome weights are synthesized.
    #[must_use]
    pub fn authored_material_weights(
        &self,
        x: u16,
        z: u16,
    ) -> Option<crate::TerrainMaterialWeights> {
        if self.document.material_layers.len() != 3 {
            return None;
        }
        self.document
            .material_overrides
            .binary_search_by_key(&(z, x), |sample| (sample.sample_z, sample.sample_x))
            .ok()?;
        // The shared evaluator finds this explicit sample before its generated branch.
        Some(crate::terrain_generation::material_weights_for(
            &self.document,
            0.0,
            1.0,
            x,
            z,
        ))
    }

    /// Validated raster, borrowed immutably by projection adapters.
    #[must_use]
    pub fn raster(&self) -> &SampledTerrain {
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
        let raster = Arc::new(SampledTerrain::from_le_bytes(source, bytes)?);
        let max_base = raster
            .heights
            .iter()
            .copied()
            .map(f64::from)
            .map(f64::abs)
            .fold(f64::from(raster.source.outside_height_m).abs(), f64::max);
        Self::from_validated_document(document, raster, max_base)
    }

    /// Re-evaluate edits over the same immutable raster without copying its samples.
    /// Source metadata, project and zone remain pinned; a different raster requires admission.
    pub fn with_document(
        &self,
        document: &TerrainDocument,
    ) -> Result<Self, SampledTerrainPlanError> {
        document.validate()?;
        let source = document
            .sampled_source
            .as_ref()
            .ok_or(SampledTerrainPlanError::MissingSource)?;
        let original = self.raster.source();
        if document.project_id != self.document.project_id
            || document.area_id != self.document.area_id
            || source != original
            || source.origin_xz_m.map(f64::to_bits) != original.origin_xz_m.map(f64::to_bits)
            || source.sample_spacing_m.to_bits() != original.sample_spacing_m.to_bits()
            || source.outside_height_m.to_bits() != original.outside_height_m.to_bits()
        {
            return Err(SampledTerrainPlanError::SourceMismatch);
        }
        Self::from_validated_document(document, self.raster.clone(), self.maximum_base_height)
    }

    fn from_validated_document(
        document: &TerrainDocument,
        raster: Arc<SampledTerrain>,
        maximum_base_height: f64,
    ) -> Result<Self, SampledTerrainPlanError> {
        let max_delta = document
            .height_deltas
            .iter()
            .map(|d| f64::from(d.delta_m).abs())
            .fold(0.0, f64::max);
        if maximum_base_height
            + max_delta
            + f64::from(document.height_sampling.base_elevation_m).abs()
            > f64::from(f32::MAX)
        {
            return Err(SampledTerrainPlanError::HeightRange);
        }
        Ok(Self {
            document: document.clone(),
            document_hash: document.content_hash()?,
            raster,
            maximum_base_height,
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

    /// Refuse a stale plan before deriving a render resource from a document.
    pub fn require_document(
        &self,
        document: &TerrainDocument,
    ) -> Result<(), SampledTerrainPlanError> {
        if document.content_hash()? != self.document_hash {
            return Err(SampledTerrainPlanError::DocumentMismatch);
        }
        Ok(())
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
    #[error("terrain edit does not reference the same immutable raster, project and zone")]
    SourceMismatch,
    #[error("sampled terrain plan belongs to a different document revision")]
    DocumentMismatch,
    #[error("raster and corrections exceed finite float32 height range")]
    HeightRange,
    #[error("sampled terrain patch exceeds the source domain or 128 intervals per axis")]
    PatchBounds,
}
