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
            result.references += usize::from(
                (old.asset_id, old.asset_hash, &old.physics)
                    != (new.asset_id, new.asset_hash, &new.physics),
            );
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
pub struct PhysicsPackageReference {
    pub schema_version: u32,
    pub carrier_sha256: ContentHash,
    pub manifest_sha256: ContentHash,
    pub package_sha256: ContentHash,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ManualPlacement {
    pub id: EntityId,
    pub name: String,
    pub asset_id: AssetId,
    /// Pin the approved artifact; this is not a filesystem path or a licence grant.
    pub asset_hash: ContentHash,
    /// Exact saved collision package for this carrier; absent on ordinary models.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub physics: Option<Box<PhysicsPackageReference>>,
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
    /// Replace the complete reviewed reference, including an explicit absence
    /// of a collision package. Never silently keep or drop the previous package.
    ReplacePinnedAsset {
        ids: Vec<EntityId>,
        expected_asset_id: AssetId,
        expected_asset_hash: ContentHash,
        expected_physics: Option<Box<PhysicsPackageReference>>,
        asset_id: AssetId,
        asset_hash: ContentHash,
        physics: Option<Box<PhysicsPackageReference>>,
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
        if placement.physics.as_ref().is_some_and(|physics| {
            physics.schema_version != 1 || physics.carrier_sha256 != placement.asset_hash
        }) {
            return Err("Physics reference must pin the placement carrier".into());
        }
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
                ids,
                expected_asset_id,
                expected_asset_hash,
                asset_id,
                asset_hash,
            } => {
                replace_asset(
                    placements,
                    ids,
                    (expected_asset_id, expected_asset_hash),
                    (asset_id, asset_hash),
                )?;
            }
            PlacementEdit::ReplacePinnedAsset {
                ids,
                expected_asset_id,
                expected_asset_hash,
                expected_physics,
                asset_id,
                asset_hash,
                physics,
            } => {
                replace_pinned_asset(
                    placements,
                    ids,
                    &(expected_asset_id, expected_asset_hash, expected_physics),
                    &(asset_id, asset_hash, physics),
                )?;
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

type ReplacementPin = (AssetId, ContentHash, Option<Box<PhysicsPackageReference>>);

fn replace_pinned_asset(
    placements: &mut [ManualPlacement],
    mut ids: Vec<EntityId>,
    expected: &ReplacementPin,
    target: &ReplacementPin,
) -> Result<(), String> {
    if ids.is_empty() || ids.len() > MAX_PLACEMENT_BATCH {
        return Err("Asset replacement requires 1 to 256 objects".into());
    }
    ids.sort_unstable();
    if ids.windows(2).any(|pair| pair[0] == pair[1]) {
        return Err("Asset replacement requires unique object IDs".into());
    }
    if expected == target {
        return Err("Choose a different asset or physics reference".into());
    }
    for id in ids {
        let placement = find(placements, id)?;
        if (placement.asset_id, placement.asset_hash, &placement.physics)
            != (expected.0, expected.1, &expected.2)
        {
            return Err("Placement reference changed; review replacement again".into());
        }
        placement.asset_id = target.0;
        placement.asset_hash = target.1;
        placement.physics.clone_from(&target.2);
    }
    Ok(())
}

fn replace_asset(
    placements: &mut [ManualPlacement],
    mut ids: Vec<EntityId>,
    expected: (AssetId, ContentHash),
    target: (AssetId, ContentHash),
) -> Result<(), String> {
    if ids.is_empty() || ids.len() > MAX_PLACEMENT_BATCH {
        return Err("Asset replacement requires 1 to 256 objects".into());
    }
    ids.sort_unstable();
    if ids.windows(2).any(|pair| pair[0] == pair[1]) {
        return Err("Asset replacement requires unique object IDs".into());
    }
    if expected == target {
        return Err("Choose a different asset reference".into());
    }
    for id in ids {
        let placement = find(placements, id)?;
        if placement.physics.is_some() {
            return Err(
                "Replacing this asset requires an explicitly reviewed physics package".into(),
            );
        }
        if (placement.asset_id, placement.asset_hash) != expected {
            return Err("Placement asset changed; review replacement again".into());
        }
        placement.asset_id = target.0;
        placement.asset_hash = target.1;
    }
    Ok(())
}
