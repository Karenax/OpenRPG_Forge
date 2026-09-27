//! Previewable author-owned replacement of recipe vegetation exclusions.
use super::{
    ZoneAuthoringHead,
    session::{ZoneAction, ZoneRequest},
};
use crate::VegetationExclusion;
use forge_core::CommandId;
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum VegetationExclusionEdit {
    Replace {
        exclusions: Vec<VegetationExclusion>,
    },
    RestoreInheritance,
}

pub fn validate(
    exclusions: &[VegetationExclusion],
    vegetation: Option<&crate::VegetationDocument>,
) -> Result<(), String> {
    let mut candidate = vegetation.cloned().ok_or("Zone has no vegetation recipe")?;
    candidate.exclusions = exclusions.to_vec();
    candidate.validate().map_err(|error| error.to_string())
}

impl ZoneAuthoringHead {
    pub fn prepare_vegetation_exclusions(
        &self,
        edit: VegetationExclusionEdit,
        command_id: CommandId,
    ) -> Result<ZoneRequest, String> {
        let expected_hash = self.content_hash()?;
        let mut corrections = self.corrections.clone();
        corrections.vegetation_exclusions = match edit {
            VegetationExclusionEdit::Replace { exclusions } => Some(exclusions),
            VegetationExclusionEdit::RestoreInheritance => None,
        };
        if corrections.vegetation_exclusions == self.corrections.vegetation_exclusions {
            return Err("Vegetation exclusions are unchanged".into());
        }
        let mut candidate = self.clone();
        candidate.corrections = corrections.clone();
        candidate.version = candidate.corrections.schema_version();
        candidate.validate()?;
        Ok(ZoneRequest {
            command_id,
            expected_revision: self.revision,
            expected_hash,
            action: ZoneAction::edit(corrections),
        })
    }
}
