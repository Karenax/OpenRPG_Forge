//! Parametric rooms and explicit door corrections in the single zone authority.
use super::{
    ZoneAuthoringHead,
    session::{ZoneAction, ZoneRequest},
};
use crate::{TerrainDocument, building::RoomDimensions};
use forge_core::{CommandId, EntityId, Vec3};
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AuthoredBuilding {
    pub id: EntityId,
    pub name: String,
    pub generator_version: u32,
    pub origin_m: Vec3,
    pub yaw_degrees: f32,
    pub dimensions: RoomDimensions,
    /// Absolute local X override, preserved when changing the room dimensions.
    pub door_offset_override_m: Option<f32>,
    pub locked: bool,
}

impl AuthoredBuilding {
    pub fn effective_dimensions(&self) -> Result<RoomDimensions, String> {
        self.dimensions.validate()?;
        let mut result = self.dimensions;
        if let Some(offset) = self.door_offset_override_m {
            result.door_offset_x_m = offset;
        }
        result.validate()?;
        Ok(result)
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum BuildingEdit {
    Insert {
        building: AuthoredBuilding,
    },
    Duplicate {
        id: EntityId,
        new_id: EntityId,
        name: String,
        origin_m: Vec3,
    },
    SetDimensions {
        id: EntityId,
        dimensions: RoomDimensions,
    },
    SetName {
        id: EntityId,
        name: String,
    },
    SetPose {
        id: EntityId,
        origin_m: Vec3,
        yaw_degrees: f32,
    },
    SetDoorOffset {
        id: EntityId,
        offset_m: Option<f32>,
    },
    SetLocked {
        id: EntityId,
        locked: bool,
    },
    Remove {
        id: EntityId,
    },
}

pub fn validate(buildings: &[AuthoredBuilding], terrain: &TerrainDocument) -> Result<(), String> {
    if buildings.len() > 32 {
        return Err("Building limit exceeded (32)".into());
    }
    let mut previous = None;
    for building in buildings {
        if previous.is_some_and(|id| id >= building.id) {
            return Err("Buildings require unique sorted IDs".into());
        }
        previous = Some(building.id);
        let dimensions = building.effective_dimensions()?;
        if building.generator_version != 1
            || building.name.trim().is_empty()
            || building.name.len() > 128
        {
            return Err("Unsupported building generator or invalid name".into());
        }
        let p = building.origin_m;
        if ![p.x, p.y, p.z, building.yaw_degrees]
            .into_iter()
            .all(f32::is_finite)
            || p.y.abs() > 4096.0
            || building.yaw_degrees.abs() > 360.0
        {
            return Err("Building pose must be finite and bounded".into());
        }
        let (s, c) = building.yaw_degrees.to_radians().sin_cos();
        let half_x = f32::midpoint(c.abs() * dimensions.width_m, s.abs() * dimensions.depth_m);
        let half_z = f32::midpoint(s.abs() * dimensions.width_m, c.abs() * dimensions.depth_m);
        if p.x.abs() + half_x > terrain.extent_m.size_x_m * 0.5
            || p.z.abs() + half_z > terrain.extent_m.size_z_m * 0.5
        {
            return Err("Building footprint exceeds terrain bounds".into());
        }
    }
    Ok(())
}

pub(super) fn validate_locks(
    before: &[AuthoredBuilding],
    after: &[AuthoredBuilding],
) -> Result<(), String> {
    for old in before.iter().filter(|b| b.locked) {
        let new = after
            .iter()
            .find(|b| b.id == old.id)
            .ok_or("Unlock building before removing it")?;
        let mut allowed = old.clone();
        allowed.locked = new.locked;
        if &allowed != new {
            return Err("Unlock building before changing it".into());
        }
    }
    Ok(())
}

impl ZoneAuthoringHead {
    /// Validated disposable proposal for adapters; never advances this authority.
    pub fn building_preview(&self, receipt: &super::session::ZoneReceipt) -> Result<Self, String> {
        if receipt.request.expected_hash != self.content_hash()?
            || receipt.request.expected_revision != self.revision
            || receipt.inverse != self.corrections
            || receipt.base_change.is_some()
            || receipt.resulting_revision != self.revision.next().map_err(|e| e.to_string())?
            || receipt.request.action != ZoneAction::edit(receipt.applied.clone())
        {
            return Err("Building preview receipt is stale or inconsistent".into());
        }
        let mut unchanged = receipt.applied.clone();
        unchanged.buildings.clone_from(&self.corrections.buildings);
        if unchanged != self.corrections || receipt.applied.buildings == self.corrections.buildings
        {
            return Err("Building preview requires a buildings-only edit".into());
        }
        validate_locks(
            self.corrections.buildings.as_deref().unwrap_or_default(),
            receipt.applied.buildings.as_deref().unwrap_or_default(),
        )?;
        let mut candidate = self.clone();
        candidate.corrections = receipt.applied.clone();
        candidate.version = candidate.corrections.schema_version();
        candidate.revision = receipt.resulting_revision;
        if candidate.content_hash()? != receipt.resulting_hash {
            return Err("Building preview result hash mismatch".into());
        }
        Ok(candidate)
    }

    pub fn prepare_building(
        &self,
        command_id: CommandId,
        edit: BuildingEdit,
    ) -> Result<ZoneRequest, String> {
        let expected_hash = self.content_hash()?;
        let mut corrections = self.corrections.clone();
        let buildings = corrections.buildings.get_or_insert_with(Vec::new);
        match edit {
            BuildingEdit::Duplicate {
                id,
                new_id,
                name,
                origin_m,
            } => {
                if buildings.iter().any(|building| building.id == new_id) {
                    return Err("Building ID already exists".into());
                }
                let mut copy = find(buildings, id)?.clone();
                copy.id = new_id;
                copy.name = name;
                copy.origin_m = origin_m;
                copy.locked = false;
                buildings.push(copy);
                buildings.sort_by_key(|building| building.id);
            }
            BuildingEdit::Insert { building } => {
                if buildings.iter().any(|b| b.id == building.id) {
                    return Err("Building ID already exists".into());
                }
                buildings.push(building);
                buildings.sort_by_key(|b| b.id);
            }
            BuildingEdit::SetDimensions { id, dimensions } => {
                find(buildings, id)?.dimensions = dimensions;
            }
            BuildingEdit::SetName { id, name } => {
                find(buildings, id)?.name = name;
            }
            BuildingEdit::SetPose {
                id,
                origin_m,
                yaw_degrees,
            } => {
                let building = find(buildings, id)?;
                building.origin_m = origin_m;
                building.yaw_degrees = yaw_degrees;
            }
            BuildingEdit::SetDoorOffset { id, offset_m } => {
                find(buildings, id)?.door_offset_override_m = offset_m;
            }
            BuildingEdit::SetLocked { id, locked } => {
                find(buildings, id)?.locked = locked;
            }
            BuildingEdit::Remove { id } => {
                find(buildings, id)?;
                buildings.retain(|b| b.id != id);
            }
        }
        validate(buildings, &self.base.terrain)?;
        validate_locks(
            self.corrections.buildings.as_deref().unwrap_or_default(),
            buildings,
        )?;
        Ok(ZoneRequest {
            command_id,
            expected_hash,
            expected_revision: self.revision,
            action: ZoneAction::edit(corrections),
        })
    }
}

fn find(buildings: &mut [AuthoredBuilding], id: EntityId) -> Result<&mut AuthoredBuilding, String> {
    buildings
        .iter_mut()
        .find(|b| b.id == id)
        .ok_or_else(|| "Unknown building ID".into())
}
