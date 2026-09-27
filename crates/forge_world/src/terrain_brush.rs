//! Deterministic local brush preparation. Commit through the shared authoring bus.
use crate::TerrainGenerationPlan;
use forge_project::{TerrainDocument, TerrainHeightDelta, terrain_edit::TerrainEdit};
use serde::{Deserialize, Serialize};
use std::collections::BTreeMap;

#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum BrushOperation {
    RaiseLower {
        metres: f32,
    },
    Flatten {
        height_m: f32,
    },
    Smooth,
    Noise {
        amplitude_m: f32,
        wavelength_m: f32,
        seed: u64,
    },
    /// Plane through the brush centre; gradients are metres of rise per metre.
    Slope {
        height_m: f32,
        gradient_x: f32,
        gradient_z: f32,
    },
}

#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TerrainBrush {
    pub x_m: f32,
    pub z_m: f32,
    pub radius_m: f32,
    /// Blend amount in (0, 1]. Radial falloff reaches zero at the edge.
    pub strength: f32,
    pub operation: BrushOperation,
}

impl TerrainBrush {
    fn validate(self) -> Result<(), String> {
        if !self.x_m.is_finite()
            || !self.z_m.is_finite()
            || !self.radius_m.is_finite()
            || !(2.0..=128.0).contains(&self.radius_m)
            || !self.strength.is_finite()
            || !(0.0..=1.0).contains(&self.strength)
            || self.strength == 0.0
            || self.x_m.abs() > 256.0
            || self.z_m.abs() > 256.0
        {
            return Err(
                "Brush requires a terrain position, radius 2–128 m and strength (0, 1]".into(),
            );
        }
        match self.operation {
            BrushOperation::RaiseLower { metres } if !metres.is_finite() || metres.abs() > 64.0 => {
                return Err("Raise/lower must be finite and within ±64 m".into());
            }
            BrushOperation::Flatten { height_m } if !height_m.is_finite() => {
                return Err("Flatten height must be finite".into());
            }
            BrushOperation::Slope {
                height_m,
                gradient_x,
                gradient_z,
            } if !height_m.is_finite()
                || !gradient_x.is_finite()
                || !gradient_z.is_finite()
                || gradient_x.abs() > 4.0
                || gradient_z.abs() > 4.0 =>
            {
                return Err("Slope needs a finite height and gradients within ±4 m/m".into());
            }
            _ => (),
        }
        Ok(())
    }

    pub fn prepare(self, document: &TerrainDocument) -> Result<TerrainEdit, String> {
        self.validate()?;
        let plan = TerrainGenerationPlan::from_document(document).map_err(|e| e.to_string())?;
        let noise = noise_plan(self.operation, document)?;
        let mut samples: BTreeMap<_, _> = document
            .height_deltas
            .iter()
            .map(|sample| (sample.sort_key(), sample.delta_m))
            .collect();
        // Read exclusively from the original plan: smoothing never depends on loop order.
        for z in 0_u16..=256 {
            for x in 0_u16..=256 {
                let wx = f32::from(x) * 2.0 - 256.0;
                let wz = f32::from(z) * 2.0 - 256.0;
                let distance = (wx - self.x_m).hypot(wz - self.z_m);
                if distance >= self.radius_m {
                    continue;
                }
                let height = plan.height_at(wx, wz).map_err(|e| e.to_string())?;
                let change = match self.operation {
                    BrushOperation::RaiseLower { metres } => metres,
                    BrushOperation::Flatten { height_m } => height_m - height,
                    BrushOperation::Smooth => neighbor_mean(&plan, wx, wz)? - height,
                    BrushOperation::Noise { .. } => noise
                        .as_ref()
                        .ok_or("Noise plan unavailable")?
                        .height_at(wx, wz)
                        .map_err(|e| e.to_string())?,
                    BrushOperation::Slope {
                        height_m,
                        gradient_x,
                        gradient_z,
                    } => {
                        height_m + (wx - self.x_m) * gradient_x + (wz - self.z_m) * gradient_z
                            - height
                    }
                };
                let weight = self.strength * (1.0 - distance / self.radius_m);
                let delta = samples.get(&(z, x)).copied().unwrap_or(0.0) + change * weight;
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
        let mut candidate = document.clone();
        candidate.height_deltas.clone_from(&samples);
        candidate.validate().map_err(|e| e.to_string())?;
        Ok(TerrainEdit::SetHeightDeltas { samples })
    }
}

fn noise_plan(
    operation: BrushOperation,
    source: &TerrainDocument,
) -> Result<Option<TerrainGenerationPlan>, String> {
    let BrushOperation::Noise {
        amplitude_m,
        wavelength_m,
        seed,
    } = operation
    else {
        return Ok(None);
    };
    if !amplitude_m.is_finite()
        || !(0.0..=64.0).contains(&amplitude_m)
        || !wavelength_m.is_finite()
        || !(4.0..=256.0).contains(&wavelength_m)
    {
        return Err("Noise requires amplitude 0–64 m and wavelength 4–256 m".into());
    }
    // Reuse the versioned terrain noise algorithm; never use runtime randomness.
    let mut recipe = source.clone();
    recipe.seed = seed;
    recipe.height_deltas.clear();
    recipe.height_sampling.base_elevation_m = 0.0;
    recipe.height_sampling.amplitude_m = amplitude_m;
    recipe.height_sampling.base_frequency_cycles_per_m = 1.0 / wavelength_m;
    recipe.height_sampling.octaves = 1;
    TerrainGenerationPlan::from_document(&recipe)
        .map(Some)
        .map_err(|e| e.to_string())
}

fn neighbor_mean(plan: &TerrainGenerationPlan, x: f32, z: f32) -> Result<f32, String> {
    let mut total = 0.0;
    let mut count = 0.0;
    for (dx, dz) in [(-2.0, 0.0), (2.0, 0.0), (0.0, -2.0), (0.0, 2.0)] {
        if (-256.0..=256.0).contains(&(x + dx)) && (-256.0..=256.0).contains(&(z + dz)) {
            total += plan.height_at(x + dx, z + dz).map_err(|e| e.to_string())?;
            count += 1.0;
        }
    }
    Ok(total / count)
}
