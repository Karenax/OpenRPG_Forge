//! One path, one preview and one undoable edit. Sampling is distance-based, not frame-based.
use crate::terrain_brush::TerrainBrush;
use forge_project::{TerrainDocument, terrain_edit::TerrainEdit};

pub const MAX_STROKE_POINTS: usize = 1024;
const MAX_DABS: usize = 128;
pub(crate) const MAX_SAMPLED_STROKE_VISITS: usize = 2_000_000;

#[derive(Clone, Debug)]
pub struct TerrainBrushStroke {
    pub brush: TerrainBrush,
    pub points: Vec<[f32; 2]>,
}

impl TerrainBrushStroke {
    #[must_use]
    pub fn single(brush: TerrainBrush) -> Self {
        Self {
            points: vec![[brush.x_m, brush.z_m]],
            brush,
        }
    }

    pub fn prepare(&self, source: &TerrainDocument) -> Result<TerrainEdit, String> {
        // Validate and resample the entire gesture before any preparation work.
        let centres = self.centres()?;
        let mut candidate = source.clone();
        for [x_m, z_m] in centres {
            let dab = TerrainBrush {
                x_m,
                z_m,
                ..self.brush
            };
            let TerrainEdit::SetHeightDeltas { samples } = dab.prepare(&candidate)? else {
                return Err("Unexpected brush edit".into());
            };
            candidate.height_deltas = samples;
        }
        Ok(TerrainEdit::SetHeightDeltas {
            samples: candidate.height_deltas,
        })
    }

    /// Prepare the complete gesture over the verified source's actual corner grid.
    /// Intermediate plans share immutable raster samples; no partial edit is published.
    pub fn prepare_sampled(
        &self,
        source: &crate::sampled_terrain::SampledTerrainPlan,
    ) -> Result<TerrainEdit, String> {
        let metadata = source.raster().source();
        let dimensions = [metadata.width, metadata.height];
        let maximum = std::array::from_fn(|axis| {
            metadata.origin_xz_m[axis] + f64::from(dimensions[axis]) * metadata.sample_spacing_m
        });
        let centres = self.centres_in(metadata.origin_xz_m, maximum)?;
        let mut visits = 0_usize;
        for &[x_m, z_m] in &centres {
            let dab = TerrainBrush {
                x_m,
                z_m,
                ..self.brush
            };
            visits = visits
                .checked_add(dab.sampled_visits(source)?)
                .ok_or("Raster brush visit count overflow")?;
            if visits > MAX_SAMPLED_STROKE_VISITS {
                return Err("Le tracé exige trop d’échantillons du raster ; raccourcir le geste ou réduire le rayon.".into());
            }
        }
        let mut plan = source
            .with_document(source.document())
            .map_err(|e| e.to_string())?;
        for [x_m, z_m] in centres {
            let TerrainEdit::SetHeightDeltas { samples } = TerrainBrush {
                x_m,
                z_m,
                ..self.brush
            }
            .prepare_sampled(&plan)?
            else {
                return Err("Unexpected sampled brush edit".into());
            };
            let mut candidate = plan.document().clone();
            candidate.height_deltas = samples;
            plan = plan.with_document(&candidate).map_err(|e| e.to_string())?;
        }
        Ok(TerrainEdit::SetHeightDeltas {
            samples: plan.document().height_deltas.clone(),
        })
    }

    pub(crate) fn centres(&self) -> Result<Vec<[f32; 2]>, String> {
        self.centres_in([-256.0; 2], [256.0; 2])
    }

    pub(crate) fn centres_in(
        &self,
        minimum: [f64; 2],
        maximum: [f64; 2],
    ) -> Result<Vec<[f32; 2]>, String> {
        if self.points.is_empty()
            || self.points.len() > MAX_STROKE_POINTS
            || self.points.iter().any(|point| {
                point.iter().enumerate().any(|(axis, &value)| {
                    !value.is_finite()
                        || f64::from(value) < minimum[axis]
                        || f64::from(value) > maximum[axis]
                })
            })
            || !self.brush.radius_m.is_finite()
            || !(2.0..=128.0).contains(&self.brush.radius_m)
        {
            return Err("Invalid brush path (1–1024 terrain points, radius 2–128 m)".into());
        }
        let spacing = self.brush.radius_m * 0.5;
        let mut centres = vec![self.points[0]];
        let mut remaining = spacing;
        for segment in self.points.windows(2) {
            let mut from = segment[0];
            let end = segment[1];
            let mut length = (end[0] - from[0]).hypot(end[1] - from[1]);
            while length >= remaining {
                if centres.len() == MAX_DABS {
                    return Err("Trait trop long : raccourcir le geste ou augmenter le rayon (128 empreintes maximum)".into());
                }
                let ratio = remaining / length;
                from = [
                    from[0] + (end[0] - from[0]) * ratio,
                    from[1] + (end[1] - from[1]) * ratio,
                ];
                centres.push(from);
                length = (end[0] - from[0]).hypot(end[1] - from[1]);
                remaining = spacing;
            }
            remaining -= length;
        }
        Ok(centres)
    }
}
