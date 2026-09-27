//! Authored river recipes; carved heights and GPU water remain derived.
use super::{
    ZoneAuthoringHead,
    session::{ZoneAction, ZoneRequest},
};
use crate::{TerrainDocument, WaterDocument};
use forge_core::{CommandId, EntityId};
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AuthoredRiver {
    pub id: EntityId,
    pub water: WaterDocument,
    /// Legacy serialized value kept for archive/hash compatibility. Studio
    /// no longer caps the derived river cut with this field; zero means none.
    pub maximum_cut_m: f32,
    pub locked: bool,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum RiverEdit {
    Insert { river: AuthoredRiver },
    Replace { river: AuthoredRiver },
    SetLocked { id: EntityId, locked: bool },
    Remove { id: EntityId },
}

pub fn validate(rivers: &[AuthoredRiver], terrain: &TerrainDocument) -> Result<(), String> {
    if rivers.len() > 16 {
        return Err("River limit exceeded (16)".into());
    }
    let mut previous = None;
    for river in rivers {
        if previous.is_some_and(|id| id >= river.id) {
            return Err("Rivers require unique sorted IDs".into());
        }
        previous = Some(river.id);
        river.water.validate().map_err(|e| e.to_string())?;
        if river.water.project_id != terrain.project_id || river.water.area_id != terrain.area_id {
            return Err("River identity differs from terrain".into());
        }
        if !river.maximum_cut_m.is_finite()
            || (river.maximum_cut_m != 0.0 && !(0.001..=64.0).contains(&river.maximum_cut_m))
        {
            return Err("Legacy river cut marker must be zero or within 0.001–64 m".into());
        }
        if river.water.river.control_points.iter().any(|p| {
            p.center_m.x.abs() > terrain.extent_m.size_x_m * 0.5
                || p.center_m.z.abs() > terrain.extent_m.size_z_m * 0.5
        }) {
            return Err("River control outside terrain".into());
        }
    }
    Ok(())
}

pub(super) fn validate_locks(
    before: &[AuthoredRiver],
    after: &[AuthoredRiver],
) -> Result<(), String> {
    for old in before.iter().filter(|r| r.locked) {
        let new = after
            .iter()
            .find(|r| r.id == old.id)
            .ok_or("Unlock river before removing it")?;
        let mut allowed = old.clone();
        allowed.locked = new.locked;
        if &allowed != new {
            return Err("Unlock river before changing it".into());
        }
    }
    Ok(())
}

impl ZoneAuthoringHead {
    pub fn prepare_river(
        &self,
        command_id: CommandId,
        edit: RiverEdit,
    ) -> Result<ZoneRequest, String> {
        let expected_hash = self.content_hash()?;
        let mut corrections = self.corrections.clone();
        let rivers = corrections.rivers.get_or_insert_with(Vec::new);
        match edit {
            RiverEdit::Insert { river } => {
                if rivers.iter().any(|r| r.id == river.id) {
                    return Err("River ID already exists".into());
                }
                rivers.push(river);
                rivers.sort_by_key(|r| r.id);
            }
            RiverEdit::Replace { river } => {
                let target = find(rivers, river.id)?;
                *target = river;
            }
            RiverEdit::SetLocked { id, locked } => {
                find(rivers, id)?.locked = locked;
            }
            RiverEdit::Remove { id } => {
                find(rivers, id)?;
                rivers.retain(|r| r.id != id);
            }
        }
        validate(rivers, &self.base.terrain)?;
        validate_locks(
            self.corrections.rivers.as_deref().unwrap_or_default(),
            rivers,
        )?;
        Ok(ZoneRequest {
            command_id,
            expected_hash,
            expected_revision: self.revision,
            action: ZoneAction::edit(corrections),
        })
    }
}

fn find(rivers: &mut [AuthoredRiver], id: EntityId) -> Result<&mut AuthoredRiver, String> {
    rivers
        .iter_mut()
        .find(|r| r.id == id)
        .ok_or_else(|| "Unknown river ID".into())
}
