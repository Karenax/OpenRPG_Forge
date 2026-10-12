//! Explicit scene contact policy in the existing canonical authoring head.
use super::{
    ZoneAuthoringHead,
    session::{ZoneAction, ZoneRequest},
};
use forge_core::CommandId;
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PhysicsSceneSettings {
    pub schema_version: u32,
    pub layer_matrix: [u32; 32],
    pub contact_skin_m: f32,
    pub evidence: String,
}

impl PhysicsSceneSettings {
    pub fn validate(&self) -> Result<(), String> {
        if self.schema_version != 1
            || !self.contact_skin_m.is_finite()
            || self.contact_skin_m < 0.
            || self.evidence.trim().is_empty()
            || self.evidence.len() > 2048
            || self.evidence.chars().any(char::is_control)
        {
            return Err("Invalid explicit scene physics settings".into());
        }
        for (layer, mask) in self.layer_matrix.iter().enumerate() {
            for (other, other_mask) in self.layer_matrix.iter().enumerate() {
                if ((mask >> other) & 1) != ((other_mask >> layer) & 1) {
                    return Err("Scene physics layer matrix must be symmetric".into());
                }
            }
        }
        Ok(())
    }
}

impl ZoneAuthoringHead {
    pub fn prepare_physics(
        &self,
        settings: Option<PhysicsSceneSettings>,
        command_id: CommandId,
    ) -> Result<ZoneRequest, String> {
        self.validate()?;
        if let Some(settings) = &settings {
            settings.validate()?;
        }
        let mut corrections = self.corrections.clone();
        corrections.physics = settings.map(Box::new);
        Ok(ZoneRequest {
            command_id,
            expected_hash: self.content_hash()?,
            expected_revision: self.revision,
            action: ZoneAction::edit(corrections),
        })
    }
}
