//! Material strokes prepare one absolute, reversible edit; never mutate their source.
use crate::{
    TerrainGenerationPlan,
    terrain_brush::{BrushOperation, TerrainBrush},
    terrain_brush_stroke::TerrainBrushStroke,
};
use forge_project::{
    TERRAIN_DOCUMENT_SCHEMA_VERSION_V3, TERRAIN_DOCUMENT_SCHEMA_VERSION_V4, TerrainDocument,
    TerrainMaterialLayerId, TerrainMaterialOverride, TerrainMaterialWeight,
    terrain_edit::TerrainEdit,
};
use std::collections::BTreeMap;
mod sampled;

struct Grid {
    origin: [f64; 2],
    spacing: f64,
}
impl Grid {
    #[allow(clippy::cast_possible_truncation)] // Same finite f32 positions as the validated terrain evaluator.
    fn coordinate(&self, axis: usize, sample: u16) -> f32 {
        (self.origin[axis] + f64::from(sample) * self.spacing) as f32
    }
}
struct Dab {
    centre: [f32; 2],
    minimum: [u16; 2],
    maximum: [u16; 2],
}

#[derive(Clone, Debug)]
pub enum MaterialBrushOperation {
    Paint(TerrainMaterialLayerId),
    /// Restore procedural weights inside the stroke, not a blend toward another layer.
    Erase,
}

#[derive(Clone, Debug)]
pub struct TerrainMaterialBrushStroke {
    pub radius_m: f32,
    pub strength: f32,
    pub points: Vec<[f32; 2]>,
    pub operation: MaterialBrushOperation,
}

impl TerrainMaterialBrushStroke {
    pub fn prepare(&self, source: &TerrainDocument) -> Result<TerrainEdit, String> {
        if !matches!(
            source.schema_version,
            TERRAIN_DOCUMENT_SCHEMA_VERSION_V3 | TERRAIN_DOCUMENT_SCHEMA_VERSION_V4
        ) {
            return Err("Migrer explicitement le terrain vers v3 avant de peindre".into());
        }
        self.validate_strength()?;
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
        .centres()?;
        let plan = TerrainGenerationPlan::from_document(source).map_err(|e| e.to_string())?;
        let dabs = centres
            .into_iter()
            .map(|centre| Dab {
                centre,
                minimum: [0; 2],
                maximum: [256; 2],
            })
            .collect::<Vec<_>>();
        self.prepare_dabs(
            source,
            &Grid {
                origin: [-256.0; 2],
                spacing: 2.0,
            },
            &dabs,
            |_, _, wx, wz| {
                Ok(plan
                    .surface_at(wx, wz)
                    .map_err(|e| e.to_string())?
                    .material_weights
                    .as_array()
                    .map(f64::from))
            },
        )
    }

    fn validate_strength(&self) -> Result<(), String> {
        if !self.strength.is_finite() || self.strength <= 0.0 || self.strength > 1.0 {
            return Err("Intensité requise : (0, 1]".into());
        }
        Ok(())
    }

    fn prepare_dabs(
        &self,
        source: &TerrainDocument,
        grid: &Grid,
        dabs: &[Dab],
        base_weights: impl Fn(u16, u16, f32, f32) -> Result<[f64; 3], String>,
    ) -> Result<TerrainEdit, String> {
        if source.material_layers.len() != 3 {
            return Err("La peinture nécessite les trois couches de la palette du terrain.".into());
        }
        let layer = match &self.operation {
            MaterialBrushOperation::Paint(id) => Some(
                source
                    .material_layers
                    .iter()
                    .take(3)
                    .position(|l| &l.id == id)
                    .ok_or("Couche absente de la palette rendue")?,
            ),
            MaterialBrushOperation::Erase => None,
        };
        let mut samples: BTreeMap<_, _> = source
            .material_overrides
            .iter()
            .map(|s| ((s.sample_z, s.sample_x), s.clone()))
            .collect();
        for dab in dabs {
            let [cx, cz] = dab.centre;
            for z in dab.minimum[1]..=dab.maximum[1] {
                let wz = grid.coordinate(1, z);
                if (wz - cz).abs() >= self.radius_m {
                    continue;
                }
                for x in dab.minimum[0]..=dab.maximum[0] {
                    let wx = grid.coordinate(0, x);
                    let distance = (wx - cx).hypot(wz - cz);
                    if distance >= self.radius_m {
                        continue;
                    }
                    let Some(layer) = layer else {
                        samples.remove(&(z, x));
                        continue;
                    };
                    let mut weights = if let Some(sample) = samples.get(&(z, x)) {
                        std::array::from_fn(|i| {
                            sample
                                .weights
                                .iter()
                                .find(|w| w.layer_id == source.material_layers[i].id)
                                .map_or(0.0, |w| f64::from(w.weight) / 65535.0)
                        })
                    } else {
                        base_weights(x, z, wx, wz)?
                    };
                    let amount = f64::from(self.strength * (1.0 - distance / self.radius_m));
                    for value in &mut weights {
                        *value *= 1.0 - amount;
                    }
                    weights[layer] += amount;
                    let quantized = quantize(weights);
                    let mut values: Vec<_> = quantized
                        .into_iter()
                        .enumerate()
                        .filter(|(_, weight)| *weight > 0)
                        .map(|(i, weight)| TerrainMaterialWeight {
                            layer_id: source.material_layers[i].id.clone(),
                            weight,
                        })
                        .collect();
                    values.sort_by(|a, b| a.layer_id.cmp(&b.layer_id));
                    samples.insert(
                        (z, x),
                        TerrainMaterialOverride {
                            sample_x: x,
                            sample_z: z,
                            weights: values,
                        },
                    );
                }
            }
        }
        let samples: Vec<_> = samples.into_values().collect();
        let mut candidate = source.clone();
        candidate.material_overrides.clone_from(&samples);
        candidate.validate().map_err(|e| e.to_string())?;
        Ok(TerrainEdit::SetMaterialOverrides { samples })
    }
}

// Largest remainder, stable palette-order tie breaking; exactly 65535 in every case.
fn quantize(weights: [f64; 3]) -> [u16; 3] {
    let total: f64 = weights.iter().sum();
    let scaled = weights.map(|v| v / total * 65535.0);
    // Inputs are nonnegative finite surface weights blended with a unit layer.
    // Normalization bounds each value to [0, 65535]; floor is intentional here.
    #[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
    let mut result = scaled.map(|v| v.floor() as u16);
    let remaining = 65535_u32 - result.iter().map(|v| u32::from(*v)).sum::<u32>();
    let mut order = [0, 1, 2];
    order.sort_by(|a, b| {
        (scaled[*b] - scaled[*b].floor())
            .total_cmp(&(scaled[*a] - scaled[*a].floor()))
            .then(a.cmp(b))
    });
    for &index in order.iter().take(remaining as usize) {
        result[index] += 1;
    }
    result
}
