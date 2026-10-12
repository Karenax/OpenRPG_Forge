//! Prepare speculative edits using identities resolved on the accepted authoring head.
use forge_core::{CommandId, EntityId};
use forge_project::zone_authoring::{
    ZoneAuthoringHead,
    placements::{ManualPlacement, PlacementEdit},
    session::{ZoneAction, ZoneRequest},
};
use std::collections::{BTreeMap, btree_map::Entry};

/// Population/library guards belong to the accepted head. Only the final view is speculative.
pub(super) fn prepare(
    head: &ZoneAuthoringHead,
    static_sources: &[ManualPlacement],
    action: PlacementEdit,
    command: CommandId,
    mut lookup: impl FnMut(&ZoneAuthoringHead, EntityId) -> Result<ManualPlacement, String>,
    mut validate_view: impl FnMut(&ZoneAuthoringHead, &ZoneAuthoringHead) -> Result<(), String>,
) -> Result<ZoneRequest, String> {
    let ids = match &action {
        PlacementEdit::Transform { id, .. }
        | PlacementEdit::Remove { id }
        | PlacementEdit::SetLocked { id, .. } => vec![*id],
        PlacementEdit::Duplicate { source_id, .. } => vec![*source_id],
        PlacementEdit::ReplaceAsset { ids, .. } | PlacementEdit::ReplacePinnedAsset { ids, .. } => {
            ids.clone()
        }
        PlacementEdit::Insert { .. } | PlacementEdit::InsertMany { .. } => Vec::new(),
    };
    let mut resolved = BTreeMap::new();
    for id in ids {
        if let Entry::Vacant(entry) = resolved.entry(id) {
            entry.insert(lookup(head, id)?);
        }
    }
    // Freezing provenance can itself change the hash. Never resolve an identity on that copy.
    let frozen = super::manual_sources::freeze_sources(head, static_sources, &action)?;
    let mut request = prepare_frozen(&frozen, action, command, &resolved)?;
    request.expected_revision = head.revision;
    request.expected_hash = head.content_hash()?;
    let mut candidate = head.clone();
    if let ZoneAction::Edit(corrections) = &request.action {
        candidate.corrections = (**corrections).clone();
        candidate.version = candidate.corrections.schema_version();
    }
    if candidate
        .corrections
        .source_object_edits
        .as_deref()
        .unwrap_or_default()
        .iter()
        .any(|record| {
            forge_world::imported_vegetation::editable::is_plant_asset(
                &candidate,
                record.source.asset_id,
            )
        })
    {
        validate_view(head, &candidate)?;
    }
    Ok(request)
}

fn prepare_frozen(
    head: &ZoneAuthoringHead,
    action: PlacementEdit,
    command: CommandId,
    resolved: &BTreeMap<EntityId, ManualPlacement>,
) -> Result<ZoneRequest, String> {
    if let PlacementEdit::ReplaceAsset { ids, .. } = &action {
        let mut candidate = head.clone();
        for id in ids {
            if candidate
                .corrections
                .placements
                .as_deref()
                .unwrap_or_default()
                .iter()
                .any(|p| p.id == *id)
            {
                continue;
            }
            let object = resolved
                .get(id)
                .cloned()
                .ok_or("Objet absent du head accepté")?;
            let source = candidate
                .corrections
                .source_object_edits
                .as_deref()
                .unwrap_or_default()
                .iter()
                .find(|record| {
                    record.source.id == *id || record.copies.iter().any(|copy| copy.id == *id)
                })
                .map_or_else(|| object.clone(), |record| record.source.clone());
            let removed = candidate.prepare_source_object(
                source,
                PlacementEdit::Remove { id: *id },
                command,
            )?;
            if let ZoneAction::Edit(corrections) = removed.action {
                candidate.corrections = *corrections;
            }
            let placements = candidate
                .corrections
                .placements
                .get_or_insert_with(Vec::new);
            placements.push(object);
            placements.sort_by_key(|placement| placement.id);
            candidate.version = candidate.corrections.schema_version();
            candidate.validate()?;
        }
        let mut request = candidate.prepare_placement(command, action)?;
        request.expected_revision = head.revision;
        request.expected_hash = head.content_hash()?;
        return Ok(request);
    }
    let id = match &action {
        PlacementEdit::Transform { id, .. }
        | PlacementEdit::Remove { id }
        | PlacementEdit::SetLocked { id, .. } => Some(*id),
        PlacementEdit::Duplicate { source_id, .. } => Some(*source_id),
        _ => None,
    };
    let Some(id) = id else {
        return head.prepare_placement(command, action);
    };
    if head
        .corrections
        .placements
        .as_deref()
        .unwrap_or_default()
        .iter()
        .any(|p| p.id == id)
    {
        return head.prepare_placement(command, action);
    }
    let source = head
        .corrections
        .source_object_edits
        .as_deref()
        .unwrap_or_default()
        .iter()
        .find(|r| r.source.id == id || r.copies.iter().any(|p| p.id == id))
        .map(|r| r.source.clone());
    let source = match source {
        Some(source) => source,
        None => resolved
            .get(&id)
            .cloned()
            .ok_or("Objet absent du head accepté")?,
    };
    head.prepare_source_object(source, action, command)
}
