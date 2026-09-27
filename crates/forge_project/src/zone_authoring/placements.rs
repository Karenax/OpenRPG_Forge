//! Manual placements live in the authoring head, not in renderer entities or undo alone.
use super::{
    ZoneAuthoringHead,
    session::{ZoneAction, ZoneRequest},
};
use forge_core::{AssetId, CommandId, ContentHash, EntityId, Transform};
use serde::{Deserialize, Serialize};

pub const MAX_MANUAL_PLACEMENTS: usize = 4096;
pub const MAX_PLACEMENT_BATCH: usize = 256;

/// Counts are per field, not mutually exclusive: one object may change several fields.
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub struct PlacementChanges {
    pub added: usize,
    pub removed: usize,
    pub references: usize,
    pub transforms: usize,
    pub names: usize,
    pub locks: usize,
    pub unchanged: usize,
}

impl PlacementChanges {
    pub fn between(before: &[ManualPlacement], after: &[ManualPlacement]) -> Result<Self, String> {
        validate(before)?;
        validate(after)?;
        let mut result = Self::default();
        for old in before {
            let Ok(index) = after.binary_search_by_key(&old.id, |p| p.id) else {
                result.removed += 1;
                continue;
            };
            let new = &after[index];
            result.references +=
                usize::from((old.asset_id, old.asset_hash) != (new.asset_id, new.asset_hash));
            result.transforms += usize::from(old.transform != new.transform);
            result.names += usize::from(old.name != new.name);
            result.locks += usize::from(old.locked != new.locked);
            result.unchanged += usize::from(old == new);
        }
        result.added = after
            .iter()
            .filter(|new| before.binary_search_by_key(&new.id, |p| p.id).is_err())
            .count();
        Ok(result)
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ManualPlacement {
    pub id: EntityId,
    pub name: String,
    pub asset_id: AssetId,
    /// Pin the approved artifact; this is not a filesystem path or a licence grant.
    pub asset_hash: ContentHash,
    pub transform: Transform,
    pub locked: bool,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum PlacementEdit {
    /// One reviewed operation, one inverse; never a sequence of partial inserts.
    InsertMany {
        placements: Vec<ManualPlacement>,
    },
    Insert {
        placement: ManualPlacement,
    },
    /// Copy the pinned source, not its identity or its lock, at an explicit transform.
    Duplicate {
        source_id: EntityId,
        id: EntityId,
        transform: Transform,
    },
    Transform {
        id: EntityId,
        transform: Transform,
    },
    /// Explicit replacement of a reviewed pin; all authored transforms remain intact.
    ReplaceAsset {
        ids: Vec<EntityId>,
        expected_asset_id: AssetId,
        expected_asset_hash: ContentHash,
        asset_id: AssetId,
        asset_hash: ContentHash,
    },
    SetLocked {
        id: EntityId,
        locked: bool,
    },
    Remove {
        id: EntityId,
    },
}

pub(super) fn validate(placements: &[ManualPlacement]) -> Result<(), String> {
    if placements.len() > MAX_MANUAL_PLACEMENTS {
        return Err("Manual placement limit exceeded".into());
    }
    let mut previous = None;
    for placement in placements {
        if previous.is_some_and(|id| id >= placement.id) {
            return Err("Manual placements require unique sorted IDs".into());
        }
        previous = Some(placement.id);
        if placement.name.trim().is_empty() || placement.name.len() > 128 {
            return Err("Placement name must be nonempty and bounded".into());
        }
        placement.transform.validate().map_err(|e| e.to_string())?;
    }
    Ok(())
}

pub(super) fn validate_locks(
    before: &[ManualPlacement],
    after: &[ManualPlacement],
) -> Result<(), String> {
    for old in before.iter().filter(|p| p.locked) {
        let Some(new) = after.iter().find(|p| p.id == old.id) else {
            return Err("Unlock placement before removing it".into());
        };
        let mut unlocked = old.clone();
        unlocked.locked = new.locked;
        if &unlocked != new {
            return Err("Unlock placement before changing it".into());
        }
    }
    Ok(())
}

impl ZoneAuthoringHead {
    pub fn prepare_placement(
        &self,
        command_id: CommandId,
        edit: PlacementEdit,
    ) -> Result<ZoneRequest, String> {
        let expected_hash = self.content_hash()?;
        let mut corrections = self.corrections.clone();
        let placements = corrections.placements.get_or_insert_with(Vec::new);
        match edit {
            PlacementEdit::InsertMany {
                placements: additions,
            } => {
                if additions.is_empty() || additions.len() > MAX_PLACEMENT_BATCH {
                    return Err("Placement batch requires 1 to 256 objects".into());
                }
                placements.extend(additions);
                placements.sort_by_key(|p| p.id);
            }
            PlacementEdit::Duplicate {
                source_id,
                id,
                transform,
            } => {
                if placements.iter().any(|p| p.id == id) {
                    return Err("Placement ID already exists".into());
                }
                let mut copy = placements
                    .iter()
                    .find(|p| p.id == source_id)
                    .cloned()
                    .ok_or("Unknown placement ID")?;
                copy.id = id;
                copy.transform = transform;
                copy.locked = false;
                placements.push(copy);
                placements.sort_by_key(|p| p.id);
            }
            PlacementEdit::Insert { placement } => {
                if placements.iter().any(|p| p.id == placement.id) {
                    return Err("Placement ID already exists".into());
                }
                placements.push(placement);
                placements.sort_by_key(|p| p.id);
            }
            PlacementEdit::Transform { id, transform } => {
                find(placements, id)?.transform = transform;
            }
            PlacementEdit::ReplaceAsset {
                mut ids,
                expected_asset_id,
                expected_asset_hash,
                asset_id,
                asset_hash,
            } => {
                if ids.is_empty() || ids.len() > MAX_PLACEMENT_BATCH {
                    return Err("Asset replacement requires 1 to 256 objects".into());
                }
                ids.sort_unstable();
                if ids.windows(2).any(|pair| pair[0] == pair[1]) {
                    return Err("Asset replacement requires unique object IDs".into());
                }
                if (expected_asset_id, expected_asset_hash) == (asset_id, asset_hash) {
                    return Err("Choose a different asset reference".into());
                }
                for id in ids {
                    let placement = find(placements, id)?;
                    if (placement.asset_id, placement.asset_hash)
                        != (expected_asset_id, expected_asset_hash)
                    {
                        return Err("Placement asset changed; review replacement again".into());
                    }
                    placement.asset_id = asset_id;
                    placement.asset_hash = asset_hash;
                }
            }
            PlacementEdit::SetLocked { id, locked } => {
                find(placements, id)?.locked = locked;
            }
            PlacementEdit::Remove { id } => {
                find(placements, id)?;
                placements.retain(|p| p.id != id);
            }
        }
        validate(placements)?;
        validate_locks(
            self.corrections.placements.as_deref().unwrap_or_default(),
            placements,
        )?;
        Ok(ZoneRequest {
            command_id,
            expected_hash,
            expected_revision: self.revision,
            action: ZoneAction::edit(corrections),
        })
    }
}

fn find(placements: &mut [ManualPlacement], id: EntityId) -> Result<&mut ManualPlacement, String> {
    placements
        .iter_mut()
        .find(|p| p.id == id)
        .ok_or_else(|| "Unknown placement ID".into())
}
