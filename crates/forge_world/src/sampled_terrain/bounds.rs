// SPDX-License-Identifier: MIT
// Adapted from Tidewater TerrainData.js, copyright 2026 DRG Software Solutions LLC.
// See third_party/tidewater/LICENSE and the pinned source inventory.
//! The source's render-culling bounds policy, not physics collision bounds.
use super::SampledTerrainPlan;
use crate::cdlod::CdlodError;

#[derive(Debug)]
struct Level {
    side: usize,
    values: Vec<[f32; 2]>,
}

#[derive(Debug)]
pub struct RasterLodBounds {
    levels: Vec<Level>,
    origin: [f64; 2],
    spacing: f64,
    outside: f64,
    adjustment: [f64; 2],
}

impl RasterLodBounds {
    /// Inclusive 8-texel tiles, max/min pyramid and -1/+2 metre source margins.
    /// Supports square power-of-two rasters. Author deltas widen the resulting
    /// bounds by their global extrema; they cannot shrink a tile's bounds.
    pub fn from_plan(plan: &SampledTerrainPlan) -> Result<Self, CdlodError> {
        let raster = plan.raster();
        let source = raster.source();
        if source.width != source.height || source.width < 8 || !source.width.is_power_of_two() {
            return Err(CdlodError::Bounds);
        }
        let width = source.width as usize;
        let side = width / 8;
        let mut values = Vec::with_capacity(side * side);
        for z in 0..side {
            for x in 0..side {
                let mut low = f32::INFINITY;
                let mut high = f32::NEG_INFINITY;
                for row in z * 8..=((z + 1) * 8).min(width - 1) {
                    for column in x * 8..=((x + 1) * 8).min(width - 1) {
                        let height = raster.heights[row * width + column];
                        low = low.min(height);
                        high = high.max(height);
                    }
                }
                values.push([low, high]);
            }
        }
        let mut levels = vec![Level { side, values }];
        while levels.last().expect("base level").side > 1 {
            let previous = levels.last().expect("base level");
            let side = previous.side / 2;
            let mut values = Vec::with_capacity(side * side);
            for z in 0..side {
                for x in 0..side {
                    let a = 2 * z * previous.side + 2 * x;
                    let b = a + previous.side;
                    let cells = [a, a + 1, b, b + 1].map(|i| previous.values[i]);
                    values.push([
                        cells.iter().map(|v| v[0]).fold(f32::INFINITY, f32::min),
                        cells.iter().map(|v| v[1]).fold(f32::NEG_INFINITY, f32::max),
                    ]);
                }
            }
            levels.push(Level { side, values });
        }
        let mut adjustment = [0.0_f64; 2];
        for delta in &plan.document().height_deltas {
            adjustment[0] = adjustment[0].min(f64::from(delta.delta_m));
            adjustment[1] = adjustment[1].max(f64::from(delta.delta_m));
        }
        for value in &mut adjustment {
            *value += f64::from(plan.document().height_sampling.base_elevation_m);
        }
        Ok(Self {
            levels,
            origin: source.origin_xz_m,
            spacing: source.sample_spacing_m,
            outside: f64::from(source.outside_height_m),
            adjustment,
        })
    }

    #[allow(
        clippy::cast_possible_truncation,
        clippy::cast_sign_loss,
        clippy::cast_precision_loss
    )]
    pub fn bounds_for(&self, origin: [f64; 2], size: f64) -> Result<[f64; 2], CdlodError> {
        if !size.is_finite() || size <= 0.0 || origin.iter().any(|v| !v.is_finite()) {
            return Err(CdlodError::Bounds);
        }
        let span = size / (self.spacing * 8.0);
        let level =
            ((span / 2.0).max(1.0).log2().ceil().max(0.0) as usize).min(self.levels.len() - 1);
        let data = &self.levels[level];
        let tile = self.spacing * 8.0 * f64::from(1_u32 << level);
        let x0 = ((origin[0] - self.origin[0]) / tile).floor();
        let x1 = ((origin[0] + size - self.origin[0]) / tile).floor();
        let z0 = ((origin[1] - self.origin[1]) / tile).floor();
        let z1 = ((origin[1] + size - self.origin[1]) / tile).floor();
        let count = data.side as f64;
        let outside = x0 < 0.0 || z0 < 0.0 || x1 >= count || z1 >= count;
        let mut low = if outside { self.outside } else { f64::INFINITY };
        let mut high = if outside {
            self.outside
        } else {
            f64::NEG_INFINITY
        };
        if x1 >= 0.0 && z1 >= 0.0 && x0 < count && z0 < count {
            for z in z0.max(0.0) as usize..=z1.min(count - 1.0) as usize {
                for x in x0.max(0.0) as usize..=x1.min(count - 1.0) as usize {
                    let value = data.values[z * data.side + x];
                    low = low.min(f64::from(value[0]));
                    high = high.max(f64::from(value[1]));
                }
            }
        }
        let result = [
            low - 1.0 + self.adjustment[0],
            high + 2.0 + self.adjustment[1],
        ];
        if result.iter().any(|v| !v.is_finite()) {
            return Err(CdlodError::Bounds);
        }
        Ok(result)
    }
}
