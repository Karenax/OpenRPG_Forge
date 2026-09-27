//! Bounded terrain-space marks for density proposals, never a generated forest preview.
use crate::{TerrainGenerationPlan, vegetation::mask_weight};
use forge_core::ContentHash;
use forge_project::zone_authoring::{ZoneAuthoringHead, session::ZoneReceipt};

pub struct DensityMaskPreview {
    pub terrain_hash: ContentHash,
    pub changed_samples: usize,
    /// Ground position and proposed density multiplier (0..1).
    pub marks: Vec<([f32; 3], f64)>,
}

impl DensityMaskPreview {
    pub fn prepare(
        head: &ZoneAuthoringHead,
        receipt: &ZoneReceipt,
    ) -> Result<Option<Self>, String> {
        if head.content_hash()? != receipt.request.expected_hash
            || head.revision != receipt.request.expected_revision
        {
            return Err("Stale density preview".into());
        }
        if receipt.base_change.is_some() {
            return Err("Density overlay cannot preview regeneration".into());
        }
        let (terrain, before) = head.effective()?;
        let before = before.ok_or("No vegetation recipe")?;
        let mut candidate = head.clone();
        candidate.corrections = receipt.applied.clone();
        let after = candidate.effective()?.1.ok_or("No proposed vegetation")?;
        let mut changed = Vec::new();
        for layer in &before.layers {
            let proposed = after
                .layers
                .iter()
                .find(|l| l.id == layer.id)
                .ok_or("Missing density layer")?;
            if layer.density_mask == proposed.density_mask {
                continue;
            }
            for z in 0..=256_u16 {
                for x in 0..=256_u16 {
                    let wx = f32::from(x) * 2.0 - 256.0;
                    let wz = f32::from(z) * 2.0 - 256.0;
                    let old = layer
                        .density_mask
                        .as_ref()
                        .map_or(1.0, |m| mask_weight(m, before.extent_m, wx, wz));
                    let new = proposed
                        .density_mask
                        .as_ref()
                        .map_or(1.0, |m| mask_weight(m, after.extent_m, wx, wz));
                    if (old - new).abs() > f64::EPSILON {
                        changed.push((wx, wz, new));
                    }
                }
            }
        }
        if changed.is_empty() {
            return Ok(None);
        }
        let plan = TerrainGenerationPlan::from_document(&terrain).map_err(|e| e.to_string())?;
        let stride = changed.len().div_ceil(2048);
        let marks = changed
            .iter()
            .step_by(stride)
            .map(|&(x, z, density)| {
                Ok((
                    [x, plan.height_at(x, z).map_err(|e| e.to_string())?, z],
                    density,
                ))
            })
            .collect::<Result<Vec<_>, String>>()?;
        Ok(Some(Self {
            terrain_hash: terrain.content_hash().map_err(|e| e.to_string())?,
            changed_samples: changed.len(),
            marks,
        }))
    }
}
