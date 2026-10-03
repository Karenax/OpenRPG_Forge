//! One editable-object facade shared by source objects, batched instances and authored placements.
use forge_core::{CommandId, EntityId};
use forge_project::zone_authoring::session::ZoneRequest;
use forge_project::zone_authoring::{
    ZoneAuthoringHead,
    placements::{ManualPlacement, PlacementEdit},
    source_objects,
};
use std::collections::{BTreeMap, BTreeSet};

mod manual_sources;
mod planning;

pub(crate) fn sources(head: &ZoneAuthoringHead) -> Result<Vec<ManualPlacement>, String> {
    super::imported_buildings::source_placements(head)
}

pub(crate) fn list(head: &ZoneAuthoringHead) -> Result<Vec<ManualPlacement>, String> {
    let manual = head.corrections.placements.as_deref().unwrap_or_default();
    let mut result = manual.to_vec();
    let sources = sources(head)?;
    for source in &sources {
        // A head predating provenance records may contain an explicit import for the same
        // verified asset. Keep its historical suppression until the first manual operation
        // freezes a Removed record; new insertions freeze Inherited before reaching this list.
        let explicitly_edited = head
            .corrections
            .source_object_edits
            .as_deref()
            .unwrap_or_default()
            .binary_search_by_key(&source.id, |record| record.source.id)
            .is_ok();
        if !explicitly_edited
            && manual
                .iter()
                .any(|p| p.asset_id == source.asset_id && p.asset_hash == source.asset_hash)
        {
            continue;
        }
        result.extend(source_objects::resolve(head, source)?);
    }
    for record in head
        .corrections
        .source_object_edits
        .as_deref()
        .unwrap_or_default()
    {
        if forge_world::imported_vegetation::editable::is_plant_asset(head, record.source.asset_id)
        {
            continue;
        }
        let source = sources
            .iter()
            .find(|source| source.id == record.source.id)
            .ok_or("Source de l’objet retouché absente de la bibliothèque vérifiée")?;
        source_objects::resolve(head, source)?;
    }
    result.sort_by_key(|p| p.id);
    if result.windows(2).any(|pair| pair[0].id == pair[1].id) {
        return Err("Identités d’objets en collision".into());
    }
    Ok(result)
}

/// Keep the clicked batched instance in the selector without expanding the population into DOM options.
pub(crate) fn choices(
    head: &ZoneAuthoringHead,
    selected: Option<EntityId>,
) -> Result<Vec<ManualPlacement>, String> {
    let mut objects = list(head)?;
    let mut wanted = BTreeSet::new();
    let mut locked = BTreeMap::new();
    for record in head
        .corrections
        .source_object_edits
        .as_deref()
        .unwrap_or_default()
        .iter()
        .filter(|record| {
            forge_world::imported_vegetation::editable::is_plant_asset(head, record.source.asset_id)
        })
    {
        wanted.insert(record.source.id);
        match &record.state {
            source_objects::SourceObjectState::Inherited => {
                locked.insert(record.source.id, record.source.locked);
            }
            source_objects::SourceObjectState::Edited { placement } => {
                locked.insert(record.source.id, placement.locked);
            }
            source_objects::SourceObjectState::Removed => {}
        }
        for copy in &record.copies {
            wanted.insert(copy.id);
            locked.insert(copy.id, copy.locked);
        }
    }
    if !wanted.is_empty() {
        let population = crate::authoring_imported_population::population(head)?;
        let terrain =
            crate::authoring_raster::source_bytes(&crate::authoring_scene::terrain(head)?)?;
        let view = population.terrain_view(head, &terrain)?;
        for layer in &view.layers {
            for entity in layer
                .instances
                .iter()
                .filter(|entity| wanted.contains(&entity.id))
            {
                let mut object = forge_world::imported_vegetation::editable::placement(
                    head,
                    layer.layer_id,
                    entity,
                )?;
                if let Some(is_locked) = locked.get(&entity.id) {
                    object.locked = *is_locked;
                }
                if !objects.iter().any(|existing| existing.id == object.id) {
                    objects.push(object);
                }
            }
        }
    }
    if let Some(id) = selected
        && !objects.iter().any(|object| object.id == id)
        && let Ok(object) = lookup(head, id)
    {
        objects.push(object);
    }
    objects.sort_by_key(|object| object.id);
    Ok(objects)
}

pub(crate) fn lookup(head: &ZoneAuthoringHead, id: EntityId) -> Result<ManualPlacement, String> {
    if let Some(p) = list(head)?.into_iter().find(|p| p.id == id) {
        return Ok(p);
    }
    let population = crate::authoring_imported_population::population(head)?;
    let terrain = crate::authoring_raster::source_bytes(&crate::authoring_scene::terrain(head)?)?;
    let mut placement = population
        .editable_placement(head, &terrain, id)?
        .ok_or("Objet absent de la zone courante")?;
    if let Some(record) = head
        .corrections
        .source_object_edits
        .as_deref()
        .unwrap_or_default()
        .iter()
        .find(|r| r.source.id == id || r.copies.iter().any(|p| p.id == id))
        && let Some(current) = source_objects::resolve(head, &record.source)?
            .into_iter()
            .find(|p| p.id == id)
    {
        placement.locked = current.locked;
    }
    Ok(placement)
}

pub(crate) fn matching(
    head: &ZoneAuthoringHead,
    id: EntityId,
) -> Result<Vec<ManualPlacement>, String> {
    let source = lookup(head, id)?;
    let mut objects = list(head)?;
    if !objects.iter().any(|object| object.id == id) {
        objects.push(source.clone());
    }
    for record in head
        .corrections
        .source_object_edits
        .as_deref()
        .unwrap_or_default()
        .iter()
        .filter(|record| {
            record.source.asset_id == source.asset_id
                && record.source.asset_hash == source.asset_hash
        })
    {
        for object in source_objects::resolve(head, &record.source)? {
            if !objects.iter().any(|existing| existing.id == object.id)
                && lookup(head, object.id).is_ok()
            {
                objects.push(object);
            }
        }
    }
    objects.retain(|object| {
        object.asset_id == source.asset_id && object.asset_hash == source.asset_hash
    });
    objects.sort_by_key(|object| object.id);
    Ok(objects)
}

pub(crate) fn is_batched(head: &ZoneAuthoringHead, id: EntityId) -> bool {
    lookup(head, id)
        .is_ok_and(|p| forge_world::imported_vegetation::editable::is_plant_asset(head, p.asset_id))
}

pub(crate) fn edit(
    head: &ZoneAuthoringHead,
    action: PlacementEdit,
    command: CommandId,
) -> Result<ZoneRequest, String> {
    let static_sources = sources(head)?;
    planning::prepare(
        head,
        &static_sources,
        action,
        command,
        lookup,
        |accepted, candidate| {
            let population = crate::authoring_imported_population::population(accepted)?;
            let bytes =
                crate::authoring_raster::source_bytes(&crate::authoring_scene::terrain(accepted)?)?;
            population.terrain_view(candidate, &bytes)?;
            Ok(())
        },
    )
}
