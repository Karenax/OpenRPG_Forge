//! One path, one preview and one undoable edit. Sampling is distance-based, not frame-based.
use crate::terrain_brush::TerrainBrush;
use forge_project::{TerrainDocument, terrain_edit::TerrainEdit};

pub const MAX_STROKE_POINTS: usize = 1024;
const MAX_DABS: usize = 128;

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

    pub(crate) fn centres(&self) -> Result<Vec<[f32; 2]>, String> {
        if self.points.is_empty()
            || self.points.len() > MAX_STROKE_POINTS
            || self
                .points
                .iter()
                .flatten()
                .any(|n| !n.is_finite() || n.abs() > 256.0)
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
