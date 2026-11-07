//! Brush edits on the verified raster's corner grid, with bounded preparation work.
use super::{BrushOperation, TerrainBrush};
use crate::sampled_terrain::SampledTerrainPlan;
use forge_project::{TerrainHeightDelta, terrain_edit::TerrainEdit};
use std::collections::BTreeMap;

const MAX_DAB_VISITS: usize = 262_144;

pub(crate) struct Footprint {
    pub(crate) minimum: [u16; 2],
    pub(crate) maximum: [u16; 2],
    pub(crate) visits: usize,
}

impl TerrainBrush {
    /// Prepare an ordinary absolute edit; the immutable source and input plan stay unchanged.
    #[allow(
        clippy::cast_possible_truncation,
        reason = "candidate validation rejects nonfinite deltas"
    )]
    pub fn prepare_sampled(self, plan: &SampledTerrainPlan) -> Result<TerrainEdit, String> {
        let footprint = self.sampled_footprint(plan)?;
        if let BrushOperation::Noise {
            amplitude_m,
            wavelength_m,
            seed,
        } = self.operation
        {
            crate::terrain_generation::sample_brush_noise(
                seed,
                [f64::from(self.x_m), f64::from(self.z_m)],
                amplitude_m,
                wavelength_m,
            )?;
        }
        let source = plan.raster().source();
        let mut samples: BTreeMap<_, _> = plan
            .document()
            .height_deltas
            .iter()
            .map(|sample| (sample.sort_key(), sample.delta_m))
            .collect();
        for z in footprint.minimum[1]..=footprint.maximum[1] {
            for x in footprint.minimum[0]..=footprint.maximum[0] {
                let point = [
                    source.origin_xz_m[0] + f64::from(x) * source.sample_spacing_m,
                    source.origin_xz_m[1] + f64::from(z) * source.sample_spacing_m,
                ];
                let distance =
                    (point[0] - f64::from(self.x_m)).hypot(point[1] - f64::from(self.z_m));
                if distance >= f64::from(self.radius_m) {
                    continue;
                }
                let change = sampled_change(self, plan, point, [x, z])?;
                let weight = f64::from(self.strength) * (1.0 - distance / f64::from(self.radius_m));
                let delta = (f64::from(samples.get(&(z, x)).copied().unwrap_or(0.0))
                    + change * weight) as f32;
                if delta == 0.0 {
                    samples.remove(&(z, x));
                } else {
                    samples.insert((z, x), delta);
                }
            }
        }
        let samples: Vec<_> = samples
            .into_iter()
            .map(|((z, x), delta)| TerrainHeightDelta::new(x, z, delta))
            .collect();
        let mut candidate = plan.document().clone();
        candidate.height_deltas.clone_from(&samples);
        // Also enforce the total source + elevation + correction height range.
        plan.with_document(&candidate)
            .map_err(|error| error.to_string())?;
        Ok(TerrainEdit::SetHeightDeltas { samples })
    }

    pub(crate) fn sampled_visits(self, plan: &SampledTerrainPlan) -> Result<usize, String> {
        Ok(self.sampled_footprint(plan)?.visits)
    }

    #[allow(
        clippy::cast_possible_truncation,
        clippy::cast_sign_loss,
        reason = "finite grid coordinates are clamped to validated axes of at most 4096 intervals"
    )]
    pub(crate) fn sampled_footprint(self, plan: &SampledTerrainPlan) -> Result<Footprint, String> {
        let source = plan.raster().source();
        let dimensions = [source.width, source.height];
        let end = std::array::from_fn(|axis| {
            source.origin_xz_m[axis] + f64::from(dimensions[axis]) * source.sample_spacing_m
        });
        self.validate(source.origin_xz_m, end)?;
        let center = [f64::from(self.x_m), f64::from(self.z_m)];
        let minimum = std::array::from_fn(|axis| {
            ((center[axis] - f64::from(self.radius_m) - source.origin_xz_m[axis])
                / source.sample_spacing_m)
                .floor()
                .clamp(0.0, f64::from(dimensions[axis])) as u16
        });
        let maximum = std::array::from_fn(|axis| {
            ((center[axis] + f64::from(self.radius_m) - source.origin_xz_m[axis])
                / source.sample_spacing_m)
                .ceil()
                .clamp(0.0, f64::from(dimensions[axis])) as u16
        });
        let visits =
            usize::from(maximum[0] - minimum[0] + 1) * usize::from(maximum[1] - minimum[1] + 1);
        if visits > MAX_DAB_VISITS {
            return Err("Le rayon exige trop d’échantillons du raster ; réduire le rayon.".into());
        }
        Ok(Footprint {
            minimum,
            maximum,
            visits,
        })
    }
}

fn sampled_change(
    brush: TerrainBrush,
    plan: &SampledTerrainPlan,
    point: [f64; 2],
    index: [u16; 2],
) -> Result<f64, String> {
    let height = plan
        .height_at(point[0], point[1])
        .map_err(|e| e.to_string())?;
    Ok(match brush.operation {
        BrushOperation::RaiseLower { metres } => f64::from(metres),
        BrushOperation::Flatten { height_m } => f64::from(height_m) - height,
        BrushOperation::Smooth => sampled_neighbor_mean(plan, point, index)? - height,
        BrushOperation::Noise {
            amplitude_m,
            wavelength_m,
            seed,
        } => crate::terrain_generation::sample_brush_noise(seed, point, amplitude_m, wavelength_m)?,
        BrushOperation::Slope {
            height_m,
            gradient_x,
            gradient_z,
        } => {
            f64::from(height_m)
                + (point[0] - f64::from(brush.x_m)) * f64::from(gradient_x)
                + (point[1] - f64::from(brush.z_m)) * f64::from(gradient_z)
                - height
        }
    })
}

fn sampled_neighbor_mean(
    plan: &SampledTerrainPlan,
    point: [f64; 2],
    index: [u16; 2],
) -> Result<f64, String> {
    let source = plan.raster().source();
    let mut sum = 0.0;
    let mut count = 0_u8;
    for axis in 0..2 {
        let limit = if axis == 0 {
            source.width
        } else {
            source.height
        };
        for direction in [-1_i8, 1] {
            if (direction < 0 && index[axis] == 0)
                || (direction > 0 && u32::from(index[axis]) == limit)
            {
                continue;
            }
            let mut neighbor = point;
            neighbor[axis] += f64::from(direction) * source.sample_spacing_m;
            sum += plan
                .height_at(neighbor[0], neighbor[1])
                .map_err(|e| e.to_string())?;
            count += 1;
        }
    }
    Ok(sum / f64::from(count))
}
