use forge_studio_frontend::reconciliation_protocol::{Page, Query, TreeChoice};
use forge_world::promotion_preflight::ReconciliationChoices;
use std::sync::Mutex;

// One bounded derived population, evicted on any authoring/workspace command.
static CACHE: Mutex<Option<ReconciliationChoices>> = Mutex::new(None);

pub fn clear() {
    if let Ok(mut cache) = CACHE.lock() {
        *cache = None;
    }
}

pub fn query(query: &Query) -> Result<Page, String> {
    let head = super::with_authoring_host(|host| {
        let state = host.execute(forge_project::authoring::AuthoringRequest::Inspect)?;
        if state.playtest_hash.is_some() {
            return Err("Quitter le playtest avant de réconcilier".into());
        }
        state.zone_head.ok_or("Zone absente".into())
    })?;
    if head.content_hash()? != query.source_hash {
        return Err("Correspondances périmées".into());
    }
    if query.selections.as_ref().is_some_and(|s| s.len() > 4096) {
        return Err("Quota de correspondances dépassé".into());
    }
    let mut cache = CACHE
        .lock()
        .map_err(|_| "Cache de correspondances indisponible")?;
    let recipe_hash =
        forge_core::canonical_json_sha256(&query.recipe).map_err(|e| e.to_string())?;
    let cached_recipe_hash = cache
        .as_ref()
        .filter(|c| c.source_hash == query.source_hash)
        .map(|c| forge_core::canonical_json_sha256(&c.recipe))
        .transpose()
        .map_err(|e| e.to_string())?;
    if cached_recipe_hash != Some(recipe_hash) {
        *cache = Some(ReconciliationChoices::for_recipe(
            &head,
            query.recipe.clone(),
        )?);
    }
    let choices = cache.as_ref().ok_or("Liste absente")?;
    let ranked = choices.ranked_trees(&head, query.placement_id)?;
    let total = ranked.len();
    if query.offset > total {
        return Err("Page hors limites".into());
    }
    let request = query
        .selections
        .as_ref()
        .map(|s| choices.prepare(&head, s))
        .transpose()?;
    let trees = ranked
        .into_iter()
        .skip(query.offset)
        .take(20)
        .map(|entry| TreeChoice {
            id: entry.tree.id.as_entity_id(),
            layer: entry.tree.layer_id.clone(),
            position: [
                entry.tree.position_m.x,
                entry.tree.position_m.y,
                entry.tree.position_m.z,
            ],
            distance_m: entry.distance_m,
        })
        .collect();
    Ok(Page {
        source_hash: query.source_hash,
        recipe: query.recipe.clone(),
        offset: query.offset,
        placement_id: query.placement_id,
        total,
        trees,
        request,
    })
}
