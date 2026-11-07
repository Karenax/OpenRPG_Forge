// SPDX-License-Identifier: MIT
// Adapted from Tidewater TerrainData.js, copyright 2026 DRG Software Solutions LLC.
// See third_party/tidewater/LICENSE and the pinned source inventory.
//! The source's render-culling bounds policy, not physics collision bounds.
use crate::{sampled_terrain::SampledTerrainPlan, terrain_lod::CdlodError};

#[derive(Debug)]
struct Level {
    width: usize,
    height: usize,
    values: Vec<[f32; 2]>,
}

#[derive(Debug)]
pub struct RasterLodBounds {
    levels: Vec<Level>,
    origin: [f64; 2],
    spacing: f64,
    sample_counts: [u32; 2],
    outside: f64,
    adjustment: [f64; 2],
}

impl RasterLodBounds {
    /// Inclusive 8-texel tiles, max/min pyramid and -1/+2 metre source margins.
    /// Includes neighbouring samples used by bilinear interpolation and admits
    /// rectangular, partially filled tiles. Author deltas widen the bounds.
    pub fn from_plan(plan: &SampledTerrainPlan) -> Result<Self, CdlodError> {
        let raster = plan.raster();
        let source = raster.source();
        let width = source.width as usize;
        let height = source.height as usize;
        let tiles_x = width.div_ceil(8);
        let tiles_z = height.div_ceil(8);
        let mut values = Vec::with_capacity(tiles_x * tiles_z);
        for z in 0..tiles_z {
            for x in 0..tiles_x {
                let mut low = f32::INFINITY;
                let mut high = f32::NEG_INFINITY;
                for row in z * 8..=((z + 1) * 8).min(height - 1) {
                    for column in x * 8..=((x + 1) * 8).min(width - 1) {
                        let height = raster.heights()[row * width + column];
                        low = low.min(height);
                        high = high.max(height);
                    }
                }
                values.push([low, high]);
            }
        }
        let mut levels = vec![Level {
            width: tiles_x,
            height: tiles_z,
            values,
        }];
        while levels.last().expect("base level").width > 1
            || levels.last().expect("base level").height > 1
        {
            let previous = levels.last().expect("base level");
            let width = previous.width.div_ceil(2);
            let height = previous.height.div_ceil(2);
            let mut values = Vec::with_capacity(width * height);
            for z in 0..height {
                for x in 0..width {
                    let mut low = f32::INFINITY;
                    let mut high = f32::NEG_INFINITY;
                    for child_z in 2 * z..(2 * z + 2).min(previous.height) {
                        for child_x in 2 * x..(2 * x + 2).min(previous.width) {
                            let value = previous.values[child_z * previous.width + child_x];
                            low = low.min(value[0]);
                            high = high.max(value[1]);
                        }
                    }
                    values.push([low, high]);
                }
            }
            levels.push(Level {
                width,
                height,
                values,
            });
        }
        let highest = i32::try_from(levels.len() - 1).map_err(|_| CdlodError::Bounds)?;
        if !(source.sample_spacing_m * 8.0 * 2_f64.powi(highest)).is_finite() {
            return Err(CdlodError::Bounds);
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
            sample_counts: [source.width, source.height],
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
        let tile_samples = 8.0 * f64::from(1_u32 << level);
        let tile = self.spacing * tile_samples;
        // Samples sit at cell centres. The first interpolation sample can be in
        // the preceding tile even when the query begins inside the next tile.
        let first = [
            (origin[0] - self.origin[0]) / self.spacing - 0.5,
            (origin[1] - self.origin[1]) / self.spacing - 0.5,
        ];
        let last = [
            (origin[0] + size - self.origin[0]) / self.spacing - 0.5,
            (origin[1] + size - self.origin[1]) / self.spacing - 0.5,
        ];
        let x0 = (first[0] / tile_samples).floor();
        let x1 = ((origin[0] + size - self.origin[0]) / tile).floor();
        let z0 = (first[1] / tile_samples).floor();
        let z1 = ((origin[1] + size - self.origin[1]) / tile).floor();
        let count_x = data.width as f64;
        let count_z = data.height as f64;
        let outside = x0 < 0.0
            || z0 < 0.0
            || x1 >= count_x
            || z1 >= count_z
            || (0..2).any(|axis| {
                !first[axis].is_finite()
                    || !last[axis].is_finite()
                    || first[axis] < 0.0
                    || last[axis] >= f64::from(self.sample_counts[axis] - 1)
            });
        let mut low = if outside { self.outside } else { f64::INFINITY };
        let mut high = if outside {
            self.outside
        } else {
            f64::NEG_INFINITY
        };
        if x1 >= 0.0 && z1 >= 0.0 && x0 < count_x && z0 < count_z {
            for z in z0.max(0.0) as usize..=z1.min(count_z - 1.0) as usize {
                for x in x0.max(0.0) as usize..=x1.min(count_x - 1.0) as usize {
                    let value = data.values[z * data.width + x];
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
