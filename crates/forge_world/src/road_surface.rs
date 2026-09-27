//! Derived visual road weights. Canonical terrain sampling and scatter remain unchanged.
use crate::{CompiledWorldSketch, GeneratedTerrain, TerrainGenerationError, TerrainGenerationPlan};
use forge_core::ContentHash;
use forge_project::TerrainDocument;
use thiserror::Error;

#[derive(Debug, Error)]
pub enum RoadSurfaceError {
    #[error("road surface inputs do not match the WorldSketch compilation hashes")]
    StaleInput,
    #[error("road surface mask must match the supported 257 by 257 terrain sample grid")]
    InvalidGrid,
    #[error("road surface sample must be finite and inside the terrain")]
    InvalidSample,
    #[error(transparent)]
    Terrain(#[from] TerrainGenerationError),
}

/// Read-only, revision-bound preparation for renderer adapters, not a saved terrain format.
#[derive(Clone, Debug)]
pub struct RoadSurfaceProjection {
    terrain: TerrainDocument,
    mask: Vec<u8>,
    source_hash: ContentHash,
}

impl RoadSurfaceProjection {
    pub fn from_compiled(compiled: &CompiledWorldSketch) -> Result<Self, RoadSurfaceError> {
        let carving = &compiled.spline_carving;
        let grid = &carving.carved_height_grid;
        if grid.vertices_x != 257
            || grid.vertices_z != 257
            || grid.sample_spacing_m.to_bits() != 2.0_f32.to_bits()
            || grid.origin_x_m.to_bits() != (-256.0_f32).to_bits()
            || grid.origin_z_m.to_bits() != (-256.0_f32).to_bits()
            || carving.road_surface_mask_u8.len() != 257 * 257
        {
            return Err(RoadSurfaceError::InvalidGrid);
        }
        if compiled
            .carved_terrain
            .content_hash()
            .map_err(|_| RoadSurfaceError::StaleInput)?
            != compiled.report.carved_terrain_sha256
            || carving
                .content_hash()
                .map_err(|_| RoadSurfaceError::StaleInput)?
                != compiled.report.spline_carving_sha256
            || carving.source_world_sketch_sha256 != compiled.report.source_world_sketch_sha256
        {
            return Err(RoadSurfaceError::StaleInput);
        }
        TerrainGenerationPlan::from_document(&compiled.carved_terrain)?;
        Ok(Self {
            terrain: compiled.carved_terrain.clone(),
            mask: carving.road_surface_mask_u8.clone(),
            source_hash: compiled.report.spline_carving_sha256,
        })
    }

    /// Additional identity alongside the terrain hash for caches using these visual weights.
    #[must_use]
    pub const fn source_hash(&self) -> ContentHash {
        self.source_hash
    }

    /// Reject accidental attachment to another scene before generating any mesh.
    pub fn validate_terrain(&self, terrain: &TerrainDocument) -> Result<(), RoadSurfaceError> {
        if terrain == &self.terrain {
            Ok(())
        } else {
            Err(RoadSurfaceError::StaleInput)
        }
    }

    pub fn generate(&self) -> Result<GeneratedTerrain, RoadSurfaceError> {
        let mut generated = TerrainGenerationPlan::from_document(&self.terrain)?.generate()?;
        for chunk in &mut generated.chunks {
            for (local, weights) in chunk.material_weights.iter_mut().enumerate() {
                let x = usize::from(chunk.coordinate.x) * 32 + local % 33;
                let z = usize::from(chunk.coordinate.z) * 32 + local / 33;
                let road = f32::from(self.mask[z * 257 + x]) / 255.0;
                weights.grass *= 1.0 - road;
                weights.rock *= 1.0 - road;
                weights.earth = weights.earth * (1.0 - road) + road;
            }
        }
        Ok(generated)
    }

    /// Presentation-only road coverage on the same triangle grid as the terrain.
    /// Consumers blend this overlay with their own underlying surface weights.
    #[allow(
        clippy::cast_possible_truncation,
        clippy::cast_sign_loss,
        reason = "finite coordinates are checked against the fixed 257 vertex grid"
    )]
    pub fn coverage_at(&self, x_m: f32, z_m: f32) -> Result<f32, RoadSurfaceError> {
        if [x_m, z_m]
            .into_iter()
            .any(|v| !v.is_finite() || !(-256.0..=256.0).contains(&v))
        {
            return Err(RoadSurfaceError::InvalidSample);
        }
        let x = x_m.midpoint(256.0);
        let z = z_m.midpoint(256.0);
        let ix = (x.floor() as usize).min(255);
        let iz = (z.floor() as usize).min(255);
        let u = x - f32::from(ix as u16);
        let v = z - f32::from(iz as u16);
        let at = |dx, dz| f32::from(self.mask[(iz + dz) * 257 + ix + dx]) / 255.0;
        Ok(if u + v <= 1.0 {
            at(0, 0) * (1.0 - u - v) + at(1, 0) * u + at(0, 1) * v
        } else {
            at(1, 0) * (1.0 - v) + at(0, 1) * (1.0 - u) + at(1, 1) * (u + v - 1.0)
        })
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn road_coverage_uses_terrain_triangles_and_rejects_invalid_coordinates() {
        let compiled =
            crate::compile_world_sketch(&crate::embedded_visual_valley_world_sketch().unwrap())
                .unwrap();
        let mut projection = RoadSurfaceProjection::from_compiled(&compiled).unwrap();
        projection.mask.fill(0);
        projection.mask[0] = 255;
        projection.mask[258] = 255;
        assert_eq!(
            projection.coverage_at(-256.0, -256.0).unwrap().to_bits(),
            1.0_f32.to_bits()
        );
        assert_eq!(
            projection.coverage_at(-255.5, -255.5).unwrap().to_bits(),
            0.5_f32.to_bits()
        );
        assert_eq!(
            projection.coverage_at(-254.5, -254.5).unwrap().to_bits(),
            0.5_f32.to_bits()
        );
        assert_eq!(
            projection.coverage_at(-255.0, -255.0).unwrap().to_bits(),
            0.0_f32.to_bits()
        );
        assert_eq!(
            projection.coverage_at(256.0, 256.0).unwrap().to_bits(),
            0.0_f32.to_bits()
        );
        for (x, z) in [
            (f32::NAN, 0.0),
            (0.0, f32::INFINITY),
            (-256.1, 0.0),
            (0.0, 256.1),
        ] {
            assert!(matches!(
                projection.coverage_at(x, z),
                Err(RoadSurfaceError::InvalidSample)
            ));
        }
    }

    #[test]
    fn roads_change_only_visual_weights_and_preserve_geometry() {
        let compiled =
            crate::compile_world_sketch(&crate::embedded_visual_valley_world_sketch().unwrap())
                .unwrap();
        let source = compiled.clone();
        let baseline = TerrainGenerationPlan::from_document(&compiled.carved_terrain)
            .unwrap()
            .generate()
            .unwrap();
        let projection = RoadSurfaceProjection::from_compiled(&compiled).unwrap();
        let projected = projection.generate().unwrap();
        assert_eq!(projected, projection.generate().unwrap());
        assert_eq!(compiled, source);
        let mut changed = 0;
        for (before, after) in baseline.chunks.iter().zip(&projected.chunks) {
            let mut restored = after.clone();
            restored
                .material_weights
                .clone_from(&before.material_weights);
            assert_eq!(*before, restored);
            for (a, b) in before.material_weights.iter().zip(&after.material_weights) {
                assert!((b.grass + b.earth + b.rock - 1.0).abs() < 0.000_001);
                assert!(b.earth >= a.earth - 0.000_001);
                changed += usize::from(a != b);
            }
        }
        assert!(changed > 0);
        println!("road surface changed vertices={changed}");
    }

    #[test]
    fn rejects_malformed_or_stale_masks() {
        let mut compiled =
            crate::compile_world_sketch(&crate::embedded_visual_valley_world_sketch().unwrap())
                .unwrap();
        compiled.spline_carving.road_surface_mask_u8[0] ^= 1;
        assert!(matches!(
            RoadSurfaceProjection::from_compiled(&compiled),
            Err(RoadSurfaceError::StaleInput)
        ));
        compiled.spline_carving.road_surface_mask_u8.pop();
        assert!(matches!(
            RoadSurfaceProjection::from_compiled(&compiled),
            Err(RoadSurfaceError::InvalidGrid)
        ));
    }

    #[test]
    fn zero_mask_is_identity_and_other_terrain_is_rejected() {
        let mut compiled =
            crate::compile_world_sketch(&crate::embedded_visual_valley_world_sketch().unwrap())
                .unwrap();
        compiled.spline_carving.road_surface_mask_u8.fill(0);
        compiled.report.spline_carving_sha256 = compiled.spline_carving.content_hash().unwrap();
        let projection = RoadSurfaceProjection::from_compiled(&compiled).unwrap();
        assert_eq!(
            projection.generate().unwrap(),
            TerrainGenerationPlan::from_document(&compiled.carved_terrain)
                .unwrap()
                .generate()
                .unwrap()
        );
        assert!(
            projection
                .validate_terrain(&compiled.carved_terrain)
                .is_ok()
        );
        assert!(projection.validate_terrain(&compiled.terrain).is_err());
    }
}
