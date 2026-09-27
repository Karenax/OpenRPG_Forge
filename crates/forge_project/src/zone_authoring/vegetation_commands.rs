//! Typed layer-scoped authoring, shared by manual brushes and future governed clients.
use super::{
    DensityOverride, ZoneAuthoringHead,
    session::{ZoneAction, ZoneRequest},
};
use crate::{VegetationDensityMask, VegetationLayerId};
use forge_core::CommandId;
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum VegetationEdit {
    SetDensityMask {
        layer_id: VegetationLayerId,
        mask: VegetationDensityMask,
    },
    SetDensityLock {
        layer_id: VegetationLayerId,
        locked: bool,
    },
    /// Restore inheritance from the accepted recipe, unlike painting zero density.
    ClearDensityOverride { layer_id: VegetationLayerId },
}

impl ZoneAuthoringHead {
    /// Validated proposal only; does not mutate this head or bypass the session guards.
    pub fn prepare_vegetation_edit(
        &self,
        edit: VegetationEdit,
        command_id: CommandId,
    ) -> Result<ZoneRequest, String> {
        let expected_hash = self.content_hash()?;
        let layer_id = match &edit {
            VegetationEdit::SetDensityMask { layer_id, .. }
            | VegetationEdit::SetDensityLock { layer_id, .. }
            | VegetationEdit::ClearDensityOverride { layer_id } => layer_id,
        };
        let vegetation = self
            .base
            .vegetation
            .as_ref()
            .ok_or("Zone has no vegetation recipe")?;
        if !vegetation.layers.iter().any(|layer| layer.id == *layer_id) {
            return Err("Unknown vegetation layer".into());
        }
        let mut corrections = self.corrections.clone();
        let index = corrections
            .density
            .binary_search_by(|c| c.layer_id.as_str().cmp(layer_id.as_str()));
        match edit {
            VegetationEdit::SetDensityMask { layer_id, mask } => match index {
                Ok(i) => {
                    if corrections.density[i].locked && corrections.density[i].mask != mask {
                        return Err("Unlock density correction before painting".into());
                    }
                    corrections.density[i].mask = mask;
                }
                Err(i) => corrections.density.insert(
                    i,
                    DensityOverride {
                        layer_id,
                        mask,
                        locked: false,
                    },
                ),
            },
            VegetationEdit::SetDensityLock { locked, .. } => {
                let i = index.map_err(|_| "Paint a density correction before locking it")?;
                corrections.density[i].locked = locked;
            }
            VegetationEdit::ClearDensityOverride { .. } => {
                let i = index.map_err(|_| "No density override to clear")?;
                if corrections.density[i].locked {
                    return Err("Unlock density correction before clearing it".into());
                }
                corrections.density.remove(i);
            }
        }
        let mut candidate = self.clone();
        candidate.corrections = corrections.clone();
        candidate.validate()?;
        Ok(ZoneRequest {
            command_id,
            expected_revision: self.revision,
            expected_hash,
            action: ZoneAction::edit(corrections),
        })
    }
}
