use super::{
    TerrainDocument, TerrainDocumentError, TerrainMaterialLayerId, terrain_interval_count,
};
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TerrainMaterialWeight {
    pub layer_id: TerrainMaterialLayerId,
    pub weight: u16,
}

/// Absolute author correction, independent of the undo journal and generated meshes.
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TerrainMaterialOverride {
    pub sample_x: u16,
    pub sample_z: u16,
    pub weights: Vec<TerrainMaterialWeight>,
}

impl TerrainDocument {
    pub(super) fn validate_material_overrides(&self) -> Result<(), TerrainDocumentError> {
        let fail = |reason: &str| TerrainDocumentError::MaterialOverrides(reason.into());
        if self.material_overrides.is_empty() {
            return Ok(());
        }
        if !matches!(
            self.schema_version,
            super::TERRAIN_DOCUMENT_SCHEMA_VERSION_V3 | super::TERRAIN_DOCUMENT_SCHEMA_VERSION_V4
        ) {
            return Err(fail("explicit migration to v3 required"));
        }
        let max_x = terrain_interval_count(
            self.extent_m.size_x_m,
            self.height_sampling.sample_spacing_m,
        );
        let max_z = terrain_interval_count(
            self.extent_m.size_z_m,
            self.height_sampling.sample_spacing_m,
        );
        let maximum = ((usize::from(max_x) + 1) * (usize::from(max_z) + 1)).min(66_049);
        if self.material_overrides.len() > maximum {
            return Err(fail("sparse material grid limit exceeded"));
        }
        let mut previous = None;
        for sample in &self.material_overrides {
            let key = (sample.sample_z, sample.sample_x);
            if sample.sample_x > max_x || sample.sample_z > max_z {
                return Err(fail("sample outside source grid"));
            }
            if previous.is_some_and(|p| p >= key) {
                return Err(fail("samples must be unique and sorted Z/X"));
            }
            previous = Some(key);
            if sample.weights.is_empty() || sample.weights.len() > self.material_layers.len() {
                return Err(fail("invalid weight count"));
            }
            let mut last_id = None;
            let mut total = 0_u32;
            for value in &sample.weights {
                if value.weight == 0 || !self.material_layers.iter().any(|l| l.id == value.layer_id)
                {
                    return Err(fail(
                        "weight must be positive and refer to an existing layer ID",
                    ));
                }
                if last_id.is_some_and(|id| id >= &value.layer_id) {
                    return Err(fail("layer IDs must be unique and sorted"));
                }
                last_id = Some(&value.layer_id);
                total += u32::from(value.weight);
            }
            if total != u32::from(u16::MAX) {
                return Err(fail("weight sum must equal 65535"));
            }
        }
        Ok(())
    }
}
