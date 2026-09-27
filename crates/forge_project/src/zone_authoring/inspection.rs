//! Read-only selected-layer provenance. No editable copy of the zone or sample arrays.
use super::{GeneratorStamp, ZoneAuthoringHead};
use crate::{VegetationDensityMask, VegetationLayerId};
use forge_core::{ContentHash, canonical_json_sha256};
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct DensityMaskSummary {
    pub hash: ContentHash,
    pub vertices_per_axis: u16,
    pub samples: usize,
    pub zero_samples: usize,
    pub minimum: u8,
    pub maximum: u8,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct DensityInspection {
    pub head_hash: ContentHash,
    pub layer_id: VegetationLayerId,
    pub display_name: String,
    pub generator: GeneratorStamp,
    pub seed: u64,
    /// None means no spatial mask, not zero vegetation density.
    pub recipe_mask: Option<DensityMaskSummary>,
    pub effective_mask: Option<DensityMaskSummary>,
    pub has_override: bool,
    pub locked: bool,
}

impl ZoneAuthoringHead {
    pub fn inspect_density(
        &self,
        layer_id: &VegetationLayerId,
    ) -> Result<DensityInspection, String> {
        self.validate()?;
        let vegetation = self
            .base
            .vegetation
            .as_ref()
            .ok_or("Zone has no vegetation recipe")?;
        let layer = vegetation
            .layers
            .iter()
            .find(|layer| &layer.id == layer_id)
            .ok_or("Unknown vegetation layer")?;
        let correction = self
            .corrections
            .density
            .iter()
            .find(|value| &value.layer_id == layer_id);
        let recipe_mask = layer.density_mask.as_ref().map(summarize).transpose()?;
        let effective_mask = match correction {
            Some(value) => Some(summarize(&value.mask)?),
            None => recipe_mask.clone(),
        };
        Ok(DensityInspection {
            head_hash: self.content_hash()?,
            layer_id: layer.id.clone(),
            display_name: layer.display_name.clone(),
            generator: self.base.generator.clone(),
            seed: vegetation.seed,
            recipe_mask,
            effective_mask,
            has_override: correction.is_some(),
            locked: correction.is_some_and(|value| value.locked),
        })
    }
}

fn summarize(mask: &VegetationDensityMask) -> Result<DensityMaskSummary, String> {
    let first = *mask.samples_u8.first().ok_or("Empty density mask")?;
    let (minimum, maximum, zero_samples) = mask.samples_u8.iter().fold(
        (first, first, 0usize),
        |(minimum, maximum, zeros), &value| {
            (
                minimum.min(value),
                maximum.max(value),
                zeros + usize::from(value == 0),
            )
        },
    );
    Ok(DensityMaskSummary {
        hash: canonical_json_sha256(mask).map_err(|error| error.to_string())?,
        vertices_per_axis: mask.vertices_per_axis,
        samples: mask.samples_u8.len(),
        zero_samples,
        minimum,
        maximum,
    })
}
