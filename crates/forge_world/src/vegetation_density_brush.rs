//! Pure density-mask preparation. Publishing belongs to the shared zone transaction.
use crate::{
    terrain_brush::{BrushOperation, TerrainBrush},
    terrain_brush_stroke::TerrainBrushStroke,
};
use forge_project::{VegetationDensityMask, VegetationDocument, VegetationLayerId};

#[derive(Clone, Debug)]
pub struct VegetationDensityBrushStroke {
    pub layer_id: VegetationLayerId,
    pub radius_m: f32,
    pub strength: f32,
    /// Absolute desired multiplier: zero excludes, 255 preserves the recipe density.
    pub target_density: u8,
    pub points: Vec<[f32; 2]>,
}

impl VegetationDensityBrushStroke {
    /// Paint the effective mask and bind the resulting shared command to the whole zone.
    pub fn prepare_request(
        &self,
        head: &forge_project::zone_authoring::ZoneAuthoringHead,
        command_id: forge_core::CommandId,
    ) -> Result<forge_project::zone_authoring::session::ZoneRequest, String> {
        let vegetation = head.effective()?.1.ok_or("Zone has no vegetation recipe")?;
        let mask = self.prepare_mask(&vegetation)?;
        head.prepare_vegetation_edit(
            forge_project::zone_authoring::vegetation_commands::VegetationEdit::SetDensityMask {
                layer_id: self.layer_id.clone(),
                mask,
            },
            command_id,
        )
    }

    /// Existing mask resolution is preserved. Missing masks start at full density.
    /// This is a proposal, not a commit or a replacement authoring authority.
    pub fn prepare_mask(
        &self,
        source: &VegetationDocument,
    ) -> Result<VegetationDensityMask, String> {
        source.validate().map_err(|e| e.to_string())?;
        if !self.strength.is_finite() || self.strength <= 0.0 || self.strength > 1.0 {
            return Err("Density brush strength must be in (0, 1]".into());
        }
        let layer = source
            .layers
            .iter()
            .find(|layer| layer.id == self.layer_id)
            .ok_or("Unknown vegetation layer")?;
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
        let mut mask = layer
            .density_mask
            .clone()
            .unwrap_or_else(|| VegetationDensityMask {
                vertices_per_axis: 257,
                samples_u8: vec![255; 257 * 257],
            });
        let side = mask.vertices_per_axis;
        let spacing = 512.0 / f32::from(side - 1);
        for [cx, cz] in centres {
            for z in 0..side {
                let wz = f32::from(z) * spacing - 256.0;
                if (wz - cz).abs() >= self.radius_m {
                    continue;
                }
                for x in 0..side {
                    let wx = f32::from(x) * spacing - 256.0;
                    let distance = (wx - cx).hypot(wz - cz);
                    if distance >= self.radius_m {
                        continue;
                    }
                    let index = usize::from(z) * usize::from(side) + usize::from(x);
                    let old = f32::from(mask.samples_u8[index]);
                    let blend = self.strength * (1.0 - distance / self.radius_m);
                    let value = old + (f32::from(self.target_density) - old) * blend;
                    // Convex interpolation of two u8 values; explicit nearest-integer quantization.
                    #[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
                    let quantized = value.round().clamp(0.0, 255.0) as u8;
                    mask.samples_u8[index] = quantized;
                }
            }
        }
        Ok(mask)
    }
}
