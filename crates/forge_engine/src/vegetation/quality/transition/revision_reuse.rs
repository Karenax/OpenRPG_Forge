//! A new receipt may acknowledge unchanged, fully settled derived geometry.
use super::{
    VegetationProjectionStats, VegetationQualityBindings, VegetationQualityRequest, staging,
};
use bevy::prelude::{Assets, Mesh, World};
use forge_project::{TerrainDocument, VegetationDocument};

pub(super) fn can_reuse(
    world: &World,
    terrain: &TerrainDocument,
    vegetation: &VegetationDocument,
    buildings: &[forge_project::zone_authoring::buildings::AuthoredBuilding],
) -> Result<bool, String> {
    let bindings = world.resource::<VegetationQualityBindings>();
    let request = world.resource::<VegetationQualityRequest>();
    let stats = world.resource::<VegetationProjectionStats>();
    if bindings.source_changed
        || stats.prototype_preview.is_some()
        || stats.prototype_preview_error.is_some()
        || stats.quality.as_ref().is_none_or(|quality| {
            quality.projected_tier != request.tier
                || quality.policy_hash != request.hash
                || quality.status.starts_with("rejected_")
        })
        || bindings
            .entities
            .values()
            .any(|entity| world.get_entity(*entity).is_err())
        || bindings
            .meshes
            .values()
            .any(|(handle, _)| !world.resource::<Assets<Mesh>>().contains(handle.id()))
    {
        return Ok(false);
    }
    #[cfg(not(target_arch = "wasm32"))]
    if bindings.pending.is_some() {
        return Ok(false);
    }
    #[cfg(any(target_arch = "wasm32", test))]
    if bindings.incremental.is_some() {
        return Ok(false);
    }
    if staging::is_pending(world) {
        return Ok(false);
    }
    if stats.terrain_document_hash
        != bindings
            .terrain
            .content_hash()
            .map_err(|e| e.to_string())?
            .to_string()
        || stats.vegetation_document_hash != bindings.vegetation_key()?
    {
        return Ok(false);
    }
    let mut normalized_terrain = terrain.clone();
    normalized_terrain.revision = bindings.terrain.revision;
    let mut normalized_vegetation = vegetation.clone();
    normalized_vegetation.revision = bindings.vegetation.revision;
    if normalized_terrain != bindings.terrain || normalized_vegetation != bindings.vegetation {
        return Ok(false);
    }
    let normalized = forge_world::VegetationGenerationPlan::from_documents_with_buildings(
        &normalized_terrain,
        &normalized_vegetation,
        &bindings.paths,
        &bindings.rivers,
        buildings,
    )
    .and_then(|plan| plan.with_promotions(&bindings.promotions))
    .map_err(|error| error.to_string())?;
    Ok(normalized.source_hash().to_string() == bindings.vegetation_key()?)
}
