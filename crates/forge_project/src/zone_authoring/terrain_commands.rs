//! Existing terrain tools prepare aggregate commands without replacing other domains.
use super::{
    ZoneAuthoringHead,
    session::{ZoneAction, ZoneRequest},
};
use crate::terrain_edit::{TerrainAction, TerrainEdit};
use forge_core::CommandId;

impl ZoneAuthoringHead {
    /// Pure proposal. The native session still validates the revision and persists the receipt.
    pub fn prepare_terrain_action(
        &self,
        action: TerrainAction,
        command_id: CommandId,
    ) -> Result<ZoneRequest, String> {
        let expected_hash = self.content_hash()?;
        let action = match action {
            TerrainAction::Undo => ZoneAction::Undo,
            TerrainAction::Redo => ZoneAction::Redo,
            TerrainAction::Edit(edit) => {
                let mut corrections = self.corrections.clone();
                match edit {
                    TerrainEdit::Rename { name } => corrections.name = Some(name),
                    TerrainEdit::SetBaseElevation { metres } => {
                        corrections.base_elevation_m = Some(metres);
                    }
                    TerrainEdit::SetHeightDeltas { samples } => {
                        corrections.height_deltas = Some(samples);
                    }
                    TerrainEdit::SetMaterialOverrides { samples } => {
                        corrections.material_overrides = Some(samples);
                    }
                    TerrainEdit::SetSchemaVersion { .. } => {
                        return Err("Aggregate terrain schema is managed by zone migration".into());
                    }
                }
                let mut candidate = self.clone();
                candidate.corrections = corrections.clone();
                candidate.validate()?;
                ZoneAction::edit(corrections)
            }
        };
        Ok(ZoneRequest {
            command_id,
            expected_revision: self.revision,
            expected_hash,
            action,
        })
    }
}
