//! Author-owned offsets over immutable imported vegetation records.
use super::{
    ZoneAuthoringHead,
    session::{ZoneAction, ZoneRequest},
};
use crate::VegetationLayerId;
use forge_core::{CommandId, ContentHash, EntityId};
use serde::{Deserialize, Serialize};

pub const MAX_IMPORTED_PLANT_OFFSETS: usize = 4096;
pub const MAX_IMPORTED_PLANT_PLACEMENTS: usize = 1024;

#[must_use]
pub fn is_zero_translation(values: [f64; 3]) -> bool {
    values.iter().all(|value| value.abs().to_bits() == 0)
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ImportedPlantOffset {
    pub layer_id: VegetationLayerId,
    /// Stable key from the immutable imported block, independent of record order.
    pub source_key: ContentHash,
    /// Metres relative to the source transform. Y is an authored ground offset.
    pub translation_m: [f64; 3],
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum ImportedPlantEdit {
    SetOffset {
        offset: ImportedPlantOffset,
    },
    RestoreSource {
        layer_id: VegetationLayerId,
        source_key: ContentHash,
    },
}

/// An authored copy of one immutable source plant, with its own stable identity.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ImportedPlantPlacement {
    pub id: EntityId,
    pub layer_id: VegetationLayerId,
    pub source_key: ContentHash,
    /// Relative to the source transform; Y is the authored ground clearance delta.
    pub translation_m: [f64; 3],
    /// Yaw added around world up; absent in older heads and when inherited.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub yaw_degrees: Option<f64>,
    /// Uniform multiplier over the immutable source scale.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub scale_factor: Option<f64>,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum ImportedPlantPlacementEdit {
    Insert {
        placement: ImportedPlantPlacement,
    },
    Move {
        id: EntityId,
        translation_m: [f64; 3],
    },
    SetAppearance {
        id: EntityId,
        yaw_degrees: Option<f64>,
        scale_factor: Option<f64>,
    },
    Remove {
        id: EntityId,
    },
}

pub(super) fn validate_placements(
    head: &ZoneAuthoringHead,
    placements: &[ImportedPlantPlacement],
) -> Result<(), String> {
    if placements.is_empty() || placements.len() > MAX_IMPORTED_PLANT_PLACEMENTS {
        return Err("Imported plant placement count is invalid".into());
    }
    let source = head
        .base
        .vegetation
        .as_ref()
        .and_then(|vegetation| vegetation.imported_source.as_ref())
        .ok_or("Imported vegetation source required for plant placement")?;
    let mut previous = None;
    for placement in placements {
        if previous.is_some_and(|id| id >= placement.id) {
            return Err("Imported plant placements must have sorted unique IDs".into());
        }
        previous = Some(placement.id);
        if !source
            .blocks
            .iter()
            .any(|block| block.layer_id == placement.layer_id.as_str())
        {
            return Err("Imported plant placement references an absent source layer".into());
        }
        if placement
            .translation_m
            .iter()
            .any(|value| !value.is_finite() || value.abs() > 4096.0)
        {
            return Err("Imported plant placement translation must be finite and bounded".into());
        }
        if placement
            .yaw_degrees
            .is_some_and(|yaw| !yaw.is_finite() || yaw.abs() > 360.0)
        {
            return Err(
                "Imported plant placement yaw must be finite and within 360 degrees".into(),
            );
        }
        if placement
            .scale_factor
            .is_some_and(|scale| !scale.is_finite() || !(0.1..=10.0).contains(&scale))
        {
            return Err("Imported plant placement scale must be between 0.1 and 10".into());
        }
    }
    Ok(())
}

pub(super) fn validate(
    head: &ZoneAuthoringHead,
    offsets: &[ImportedPlantOffset],
) -> Result<(), String> {
    if offsets.is_empty() {
        return Err("Empty imported plant offsets must restore inheritance".into());
    }
    if offsets.len() > MAX_IMPORTED_PLANT_OFFSETS {
        return Err("Imported plant offset limit exceeded".into());
    }
    let source = head
        .base
        .vegetation
        .as_ref()
        .and_then(|vegetation| vegetation.imported_source.as_ref());
    let mut previous = None;
    for offset in offsets {
        let key = (&offset.layer_id, offset.source_key);
        if previous.is_some_and(|old| old >= key) {
            return Err("Imported plant offsets must be sorted and unique".into());
        }
        previous = Some(key);
        if source.is_none_or(|source| {
            !source
                .blocks
                .iter()
                .any(|block| block.layer_id == offset.layer_id.as_str())
        }) {
            return Err("Imported plant offset references an absent source layer".into());
        }
        if offset
            .translation_m
            .iter()
            .any(|value| !value.is_finite() || value.abs() > 4096.0)
            || is_zero_translation(offset.translation_m)
        {
            return Err("Imported plant offset must be finite, nonzero and bounded".into());
        }
    }
    Ok(())
}

impl ZoneAuthoringHead {
    /// Validated proposal only. The world adapter resolves the source key and
    /// checks the resulting position against the imported terrain extent.
    pub fn prepare_imported_plant_edit(
        &self,
        edit: ImportedPlantEdit,
        command_id: CommandId,
    ) -> Result<ZoneRequest, String> {
        self.validate()?;
        let mut corrections = self.corrections.clone();
        let offsets = corrections
            .imported_plant_offsets
            .get_or_insert_with(Vec::new);
        match edit {
            ImportedPlantEdit::SetOffset { offset } => {
                let key = (&offset.layer_id, offset.source_key);
                match offsets.binary_search_by(|existing| {
                    (&existing.layer_id, existing.source_key).cmp(&key)
                }) {
                    Ok(index) => offsets[index] = offset,
                    Err(index) => offsets.insert(index, offset),
                }
            }
            ImportedPlantEdit::RestoreSource {
                layer_id,
                source_key,
            } => {
                let index = offsets
                    .binary_search_by(|existing| {
                        (&existing.layer_id, existing.source_key).cmp(&(&layer_id, source_key))
                    })
                    .map_err(|_| "Imported plant has no authored offset")?;
                offsets.remove(index);
            }
        }
        if offsets.is_empty() {
            corrections.imported_plant_offsets = None;
        }
        if corrections == self.corrections {
            return Err("Imported plant offset is unchanged".into());
        }
        let mut candidate = self.clone();
        candidate.corrections = corrections.clone();
        candidate.version = corrections.schema_version();
        candidate.validate()?;
        Ok(ZoneRequest {
            command_id,
            expected_revision: self.revision,
            expected_hash: self.content_hash()?,
            action: ZoneAction::edit(corrections),
        })
    }

    pub fn prepare_imported_plant_placement(
        &self,
        edit: ImportedPlantPlacementEdit,
        command_id: CommandId,
    ) -> Result<ZoneRequest, String> {
        self.validate()?;
        let mut corrections = self.corrections.clone();
        let placements = corrections
            .imported_plant_placements
            .get_or_insert_with(Vec::new);
        match edit {
            ImportedPlantPlacementEdit::Insert { placement } => {
                match placements.binary_search_by_key(&placement.id, |existing| existing.id) {
                    Ok(_) => return Err("Imported plant placement ID already exists".into()),
                    Err(index) => placements.insert(index, placement),
                }
            }
            ImportedPlantPlacementEdit::Move { id, translation_m } => {
                let index = placements
                    .binary_search_by_key(&id, |existing| existing.id)
                    .map_err(|_| "Imported plant placement absent")?;
                placements[index].translation_m = translation_m;
            }
            ImportedPlantPlacementEdit::SetAppearance {
                id,
                yaw_degrees,
                scale_factor,
            } => {
                let index = placements
                    .binary_search_by_key(&id, |existing| existing.id)
                    .map_err(|_| "Imported plant placement absent")?;
                placements[index].yaw_degrees = yaw_degrees;
                placements[index].scale_factor = scale_factor;
            }
            ImportedPlantPlacementEdit::Remove { id } => {
                let index = placements
                    .binary_search_by_key(&id, |existing| existing.id)
                    .map_err(|_| "Imported plant placement absent")?;
                placements.remove(index);
            }
        }
        if placements.is_empty() {
            corrections.imported_plant_placements = None;
        }
        if corrections == self.corrections {
            return Err("Imported plant placement is unchanged".into());
        }
        let mut candidate = self.clone();
        candidate.corrections = corrections.clone();
        candidate.version = corrections.schema_version();
        candidate.validate()?;
        Ok(ZoneRequest {
            command_id,
            expected_revision: self.revision,
            expected_hash: self.content_hash()?,
            action: ZoneAction::edit(corrections),
        })
    }
}
