//! Material strokes prepare one absolute, reversible edit; never mutate their source.
use crate::{
    TerrainGenerationPlan,
    terrain_brush::{BrushOperation, TerrainBrush},
    terrain_brush_stroke::TerrainBrushStroke,
};
use forge_project::{
    TERRAIN_DOCUMENT_SCHEMA_VERSION_V3, TerrainDocument, TerrainMaterialLayerId,
    TerrainMaterialOverride, TerrainMaterialWeight, terrain_edit::TerrainEdit,
};
use std::collections::BTreeMap;

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
        if source.schema_version != TERRAIN_DOCUMENT_SCHEMA_VERSION_V3 {
            return Err("Migrer explicitement le terrain vers v3 avant de peindre".into());
        }
        if !self.strength.is_finite() || self.strength <= 0.0 || self.strength > 1.0 {
            return Err("Intensité requise : (0, 1]".into());
        }
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
        for [cx, cz] in centres {
            // The generation plan enforces the current 512 m / 2 m grid contract.
            for z in 0_u16..=256 {
                let wz = f32::from(z) * 2.0 - 256.0;
                if (wz - cz).abs() >= self.radius_m {
                    continue;
                }
                for x in 0_u16..=256 {
                    let wx = f32::from(x) * 2.0 - 256.0;
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
                        plan.surface_at(wx, wz)
                            .map_err(|e| e.to_string())?
                            .material_weights
                            .as_array()
                            .map(f64::from)
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
