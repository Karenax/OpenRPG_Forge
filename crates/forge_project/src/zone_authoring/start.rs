//! Engine-neutral terrain start, usable as authoring metadata or a temporary override.
use super::{
    ZoneAuthoringHead,
    session::{ZoneAction, ZoneRequest},
};
use crate::TerrainDocument;
use forge_core::CommandId;
use serde::{Deserialize, Serialize};

#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ZoneStart {
    pub x_m: f32,
    pub z_m: f32,
    /// Zero faces -Z, positive turns around +Y. None uses the default camera heading.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub facing_degrees: Option<f32>,
}

impl ZoneStart {
    pub fn validate(self, terrain: &TerrainDocument) -> Result<(), String> {
        if self.facing_degrees.is_some_and(|angle| !angle.is_finite()) {
            return Err("Playtest facing must be finite".into());
        }
        if !self.x_m.is_finite()
            || !self.z_m.is_finite()
            || self.x_m.abs() > terrain.extent_m.size_x_m * 0.5 - 1.0
            || self.z_m.abs() > terrain.extent_m.size_z_m * 0.5 - 1.0
        {
            return Err("Playtest start must be finite and at least 1 m inside the terrain".into());
        }
        Ok(())
    }
}

impl ZoneAuthoringHead {
    pub fn prepare_start(
        &self,
        start: Option<ZoneStart>,
        command_id: CommandId,
    ) -> Result<ZoneRequest, String> {
        self.validate()?;
        if let Some(start) = start {
            start.validate(&self.base.terrain)?;
        }
        let mut corrections = self.corrections.clone();
        corrections.start = start.map(Box::new);
        Ok(ZoneRequest {
            command_id,
            expected_hash: self.content_hash()?,
            expected_revision: self.revision,
            action: ZoneAction::edit(corrections),
        })
    }
}
