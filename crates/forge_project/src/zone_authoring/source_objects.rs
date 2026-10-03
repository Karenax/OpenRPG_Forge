//! Sparse, undoable edits over verified immutable objects. No module-specific identities.
use super::{
    ZoneAuthoringHead,
    placements::{ManualPlacement, PlacementEdit},
    session::{ZoneAction, ZoneRequest},
};
use forge_core::{CommandId, EntityId, canonical_json_bytes};
use serde::{Deserialize, Serialize};
use std::collections::BTreeSet;

pub const MAX_SOURCE_OBJECT_EDITS: usize = 4096;

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum SourceObjectState {
    Inherited,
    Edited { placement: ManualPlacement },
    Removed,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceObjectEdit {
    /// Original identity and pin, retained even after deleting the last copy.
    pub source: ManualPlacement,
    pub state: SourceObjectState,
    pub copies: Vec<ManualPlacement>,
}

pub fn validate(head: &ZoneAuthoringHead, edits: &[SourceObjectEdit]) -> Result<(), String> {
    if edits.is_empty() || edits.len() > MAX_SOURCE_OBJECT_EDITS {
        return Err("Invalid source object edit count".into());
    }
    if edits
        .iter()
        .try_fold(edits.len(), |count, edit| {
            count.checked_add(edit.copies.len())
        })
        .is_none_or(|count| count > MAX_SOURCE_OBJECT_EDITS)
    {
        return Err("Source object edit budget exceeded".into());
    }
    let mut objects: BTreeSet<_> = head
        .corrections
        .placements
        .as_deref()
        .unwrap_or_default()
        .iter()
        .map(|p| p.id)
        .collect();
    let manual_ids = objects.clone();
    for edit in edits {
        if manual_ids.contains(&edit.source.id) && !matches!(edit.state, SourceObjectState::Removed)
        {
            return Err("Source object identity collides with a manual placement".into());
        }
        objects.insert(edit.source.id);
    }
    let mut previous = None;
    for edit in edits {
        if previous.is_some_and(|id| id >= edit.source.id) {
            return Err("Source object edits require sorted unique identities".into());
        }
        previous = Some(edit.source.id);
        super::placements::validate(std::slice::from_ref(&edit.source))?;
        if let SourceObjectState::Edited { placement } = &edit.state {
            super::placements::validate(std::slice::from_ref(placement))?;
            if placement.id != edit.source.id
                || placement.asset_id != edit.source.asset_id
                || placement.asset_hash != edit.source.asset_hash
            {
                return Err("Source object edit changes its identity or verified pin".into());
            }
        }
        super::placements::validate(&edit.copies)?;
        for copy in &edit.copies {
            if copy.asset_id != edit.source.asset_id
                || copy.asset_hash != edit.source.asset_hash
                || !objects.insert(copy.id)
            {
                return Err("Source object copy has an invalid identity or pin".into());
            }
        }
    }
    Ok(())
}

/// Revalidate direct Edit requests, not only operations prepared by the Studio.
/// Undo/Redo replay their reviewed inverses and do not use this forward transition.
pub fn validate_transition(
    before: &[SourceObjectEdit],
    after: &[SourceObjectEdit],
) -> Result<(), String> {
    for old in before {
        let index = after
            .binary_search_by_key(&old.source.id, |edit| edit.source.id)
            .map_err(|_| "Source object records persist; use Undo to restore inheritance")?;
        let new = &after[index];
        if canonical_json_bytes(&old.source).map_err(|e| e.to_string())?
            != canonical_json_bytes(&new.source).map_err(|e| e.to_string())?
        {
            return Err("Source object provenance is immutable".into());
        }
        if matches!(old.state, SourceObjectState::Removed)
            && !matches!(new.state, SourceObjectState::Removed)
        {
            return Err("Source object deletion persists; use Undo to restore it".into());
        }
        if let Some(placement) = visible_source(old).filter(|placement| placement.locked) {
            validate_locked(placement, visible_source(new))?;
        }
        for copy in old.copies.iter().filter(|copy| copy.locked) {
            let replacement = new
                .copies
                .binary_search_by_key(&copy.id, |p| p.id)
                .ok()
                .map(|index| &new.copies[index]);
            validate_locked(copy, replacement)?;
        }
    }
    Ok(())
}

fn visible_source(record: &SourceObjectEdit) -> Option<&ManualPlacement> {
    match &record.state {
        SourceObjectState::Inherited => Some(&record.source),
        SourceObjectState::Edited { placement } => Some(placement),
        SourceObjectState::Removed => None,
    }
}

fn validate_locked(old: &ManualPlacement, new: Option<&ManualPlacement>) -> Result<(), String> {
    let new = new.ok_or("Unlock source object before removing it")?;
    let mut unlocked = old.clone();
    unlocked.locked = new.locked;
    if canonical_json_bytes(&unlocked).map_err(|e| e.to_string())?
        != canonical_json_bytes(new).map_err(|e| e.to_string())?
    {
        return Err("Unlock source object before changing it".into());
    }
    Ok(())
}

/// Resolve a verified source into its current visible object and copies.
pub fn resolve(
    head: &ZoneAuthoringHead,
    source: &ManualPlacement,
) -> Result<Vec<ManualPlacement>, String> {
    let edits = head
        .corrections
        .source_object_edits
        .as_deref()
        .unwrap_or_default();
    let edit = edits
        .binary_search_by_key(&source.id, |e| e.source.id)
        .ok()
        .map(|index| &edits[index]);
    let Some(edit) = edit else {
        return Ok(vec![source.clone()]);
    };
    if edit.source.asset_id != source.asset_id || edit.source.asset_hash != source.asset_hash {
        return Err("Edited source object pin has changed; reconcile the import".into());
    }
    let mut result = match &edit.state {
        SourceObjectState::Inherited => vec![source.clone()],
        SourceObjectState::Edited { placement } => vec![placement.clone()],
        SourceObjectState::Removed => Vec::new(),
    };
    result.extend(edit.copies.iter().cloned());
    Ok(result)
}

impl ZoneAuthoringHead {
    /// Uses the same `PlacementEdit` operations as ordinary placements.
    #[allow(
        clippy::needless_pass_by_value,
        reason = "Owns the source snapshot and command at the same boundary as prepare_placement"
    )]
    pub fn prepare_source_object(
        &self,
        source: ManualPlacement,
        action: PlacementEdit,
        command_id: CommandId,
    ) -> Result<ZoneRequest, String> {
        self.validate()?;
        super::placements::validate(std::slice::from_ref(&source))?;
        let mut corrections = self.corrections.clone();
        let edits = corrections.source_object_edits.get_or_insert_with(Vec::new);
        let index = match edits.binary_search_by_key(&source.id, |edit| edit.source.id) {
            Ok(index) => index,
            Err(index) => {
                edits.insert(
                    index,
                    SourceObjectEdit {
                        source: source.clone(),
                        state: SourceObjectState::Inherited,
                        copies: Vec::new(),
                    },
                );
                index
            }
        };
        let record = &mut edits[index];
        if record.source.asset_id != source.asset_id
            || record.source.asset_hash != source.asset_hash
        {
            return Err("Source object pin changed".into());
        }
        let mut visible = resolve(self, &source)?;
        let find = |id: EntityId, values: &[ManualPlacement]| {
            values
                .iter()
                .position(|p| p.id == id)
                .ok_or_else(|| "Unknown source object identity".to_owned())
        };
        match action {
            PlacementEdit::Duplicate {
                source_id,
                id,
                transform,
            } => {
                let mut copy = visible[find(source_id, &visible)?].clone();
                copy.id = id;
                copy.transform = transform;
                copy.locked = false;
                record.copies.push(copy);
                record.copies.sort_by_key(|p| p.id);
            }
            PlacementEdit::Transform { id, transform } => {
                let index = find(id, &visible)?;
                if visible[index].locked {
                    return Err("Source object is locked".into());
                }
                visible[index].transform = transform;
                set_object(record, visible[index].clone())?;
            }
            PlacementEdit::SetLocked { id, locked } => {
                let index = find(id, &visible)?;
                visible[index].locked = locked;
                set_object(record, visible[index].clone())?;
            }
            PlacementEdit::Remove { id } => {
                let index = find(id, &visible)?;
                if visible[index].locked {
                    return Err("Source object is locked".into());
                }
                if id == source.id {
                    record.state = SourceObjectState::Removed;
                } else {
                    record.copies.retain(|p| p.id != id);
                }
            }
            _ => return Err("This operation requires an authored placement".into()),
        }
        if corrections == self.corrections {
            return Err("Source object edit is unchanged".into());
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

fn set_object(record: &mut SourceObjectEdit, placement: ManualPlacement) -> Result<(), String> {
    if placement.id == record.source.id {
        record.state = SourceObjectState::Edited { placement };
    } else {
        let copy = record
            .copies
            .iter_mut()
            .find(|p| p.id == placement.id)
            .ok_or("Source object copy disappeared")?;
        *copy = placement;
    }
    Ok(())
}
