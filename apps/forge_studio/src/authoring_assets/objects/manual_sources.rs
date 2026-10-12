//! Provenance preparation for manual operations over verified static sources.
//!
//! This module deliberately knows only the authoring-head model. Studio's package/library
//! lookup remains at the caller, so the transition is testable without a renderer or a Tauri
//! host.
use forge_core::{AssetId, ContentHash, EntityId};
use forge_project::zone_authoring::{
    ZoneAuthoringHead,
    placements::{ManualPlacement, PlacementEdit},
    source_objects::{SourceObjectEdit, SourceObjectState},
};
use std::collections::BTreeSet;

type AssetKey = (AssetId, ContentHash);

/// Add sparse records for every static source affected by `action` before preparing that action.
///
/// A source with no manual placement sharing its verified pin is recorded as `Inherited`, which
/// lets a later removal of the new placement reveal the source again. A source that already had
/// a same-pin manual placement predates this provenance record and is recorded as `Removed`,
/// preserving the historical explicit-import behavior. Existing records are never rewritten.
pub fn freeze_sources(
    head: &ZoneAuthoringHead,
    sources: &[ManualPlacement],
    action: &PlacementEdit,
) -> Result<ZoneAuthoringHead, String> {
    let concerned = concerned_assets(head, sources, action);
    if concerned.is_empty() {
        return Ok(head.clone());
    }

    let existing = head
        .corrections
        .source_object_edits
        .as_deref()
        .unwrap_or_default();
    let manual = head.corrections.placements.as_deref().unwrap_or_default();
    let mut additions = Vec::new();
    for source in sources {
        let key = (source.asset_id, source.asset_hash);
        if !concerned.contains(&key) || existing.iter().any(|record| record.source.id == source.id)
        {
            continue;
        }
        let state = if manual
            .iter()
            .any(|placement| (placement.asset_id, placement.asset_hash) == key)
        {
            SourceObjectState::Removed
        } else {
            SourceObjectState::Inherited
        };
        additions.push(SourceObjectEdit {
            source: source.clone(),
            state,
            copies: Vec::new(),
        });
    }
    if additions.is_empty() {
        return Ok(head.clone());
    }

    let mut candidate = head.clone();
    let edits = candidate
        .corrections
        .source_object_edits
        .get_or_insert_with(Vec::new);
    edits.extend(additions);
    edits.sort_by_key(|record| record.source.id);
    candidate.version = candidate.corrections.schema_version();
    candidate.validate()?;
    Ok(candidate)
}

fn concerned_assets(
    head: &ZoneAuthoringHead,
    sources: &[ManualPlacement],
    action: &PlacementEdit,
) -> BTreeSet<AssetKey> {
    let mut result = BTreeSet::new();
    match action {
        PlacementEdit::Insert { placement } => insert_key(&mut result, placement),
        PlacementEdit::InsertMany { placements } => {
            for placement in placements {
                insert_key(&mut result, placement);
            }
        }
        PlacementEdit::Duplicate { source_id, .. }
        | PlacementEdit::Transform { id: source_id, .. }
        | PlacementEdit::SetLocked { id: source_id, .. }
        | PlacementEdit::Remove { id: source_id } => {
            if let Some(key) = identity_for_id(head, sources, *source_id) {
                result.insert(key);
            }
        }
        PlacementEdit::ReplaceAsset {
            ids,
            expected_asset_id,
            expected_asset_hash,
            asset_id,
            asset_hash,
        }
        | PlacementEdit::ReplacePinnedAsset {
            ids,
            expected_asset_id,
            expected_asset_hash,
            asset_id,
            asset_hash,
            ..
        } => {
            result.insert((*expected_asset_id, *expected_asset_hash));
            result.insert((*asset_id, *asset_hash));
            for id in ids {
                if let Some(key) = identity_for_id(head, sources, *id) {
                    result.insert(key);
                }
            }
        }
    }
    result
}

fn insert_key(result: &mut BTreeSet<AssetKey>, placement: &ManualPlacement) {
    result.insert((placement.asset_id, placement.asset_hash));
}

fn identity_for_id(
    head: &ZoneAuthoringHead,
    sources: &[ManualPlacement],
    id: EntityId,
) -> Option<AssetKey> {
    if let Some(placement) = head
        .corrections
        .placements
        .as_deref()
        .unwrap_or_default()
        .iter()
        .find(|placement| placement.id == id)
    {
        return Some((placement.asset_id, placement.asset_hash));
    }
    if let Some(source) = sources.iter().find(|source| source.id == id) {
        return Some((source.asset_id, source.asset_hash));
    }
    head.corrections
        .source_object_edits
        .as_deref()
        .unwrap_or_default()
        .iter()
        .find(|record| record.source.id == id || record.copies.iter().any(|copy| copy.id == id))
        .map(|record| (record.source.asset_id, record.source.asset_hash))
}
