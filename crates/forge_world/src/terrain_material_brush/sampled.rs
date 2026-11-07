//! Same material stroke and quantization over the verified raster's corner grid.
use super::{Dab, Grid, TerrainMaterialBrushStroke};
use crate::{
    sampled_terrain::SampledTerrainPlan,
    terrain_brush::{BrushOperation, TerrainBrush},
    terrain_brush_stroke::{MAX_SAMPLED_STROKE_VISITS, TerrainBrushStroke},
};
use forge_project::terrain_edit::TerrainEdit;

impl TerrainMaterialBrushStroke {
    /// Prepare one absolute paint edit without mutating or copying the raster.
    /// The existing relief footprint/resampler bounds the work before evaluation.
    pub fn prepare_sampled(&self, plan: &SampledTerrainPlan) -> Result<TerrainEdit, String> {
        self.validate_strength()?;
        let source = plan.raster().source();
        let dimensions = [source.width, source.height];
        let maximum = std::array::from_fn(|axis| {
            source.origin_xz_m[axis] + f64::from(dimensions[axis]) * source.sample_spacing_m
        });
        let centres = TerrainBrushStroke {
            brush: TerrainBrush {
                x_m: 0.0,
                z_m: 0.0,
                radius_m: self.radius_m,
                strength: self.strength,
                operation: BrushOperation::Smooth,
            },
            points: self.points.clone(),
        }
        .centres_in(source.origin_xz_m, maximum)?;
        let mut visits = 0_usize;
        let mut dabs = Vec::with_capacity(centres.len());
        for centre in centres {
            let footprint = TerrainBrush {
                x_m: centre[0],
                z_m: centre[1],
                radius_m: self.radius_m,
                strength: self.strength,
                operation: BrushOperation::Smooth,
            }
            .sampled_footprint(plan)?;
            visits = visits
                .checked_add(footprint.visits)
                .ok_or("Raster paint visit count overflow")?;
            if visits > MAX_SAMPLED_STROKE_VISITS {
                return Err("Le tracé exige trop d’échantillons du raster ; raccourcir le geste ou réduire le rayon.".into());
            }
            dabs.push(Dab {
                centre,
                minimum: footprint.minimum,
                maximum: footprint.maximum,
            });
        }
        self.prepare_dabs(
            plan.document(),
            &Grid {
                origin: source.origin_xz_m,
                spacing: source.sample_spacing_m,
            },
            &dabs,
            |x, z, _, _| {
                let vertex = plan.sample_vertex([x, z]).map_err(|e| e.to_string())?;
                Ok(crate::terrain_generation::material_weights_for(
                    plan.document(),
                    vertex.position_m[1],
                    vertex.normal[1],
                    x,
                    z,
                )
                .as_array()
                .map(f64::from))
            },
        )
    }
}
