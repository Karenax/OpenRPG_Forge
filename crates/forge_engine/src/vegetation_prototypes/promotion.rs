//! Read-only preparation of an atomic generated-tree promotion.
use forge_core::{CommandId, ContentHash, EntityId};
use forge_project::{
    VegetationLayerId,
    zone_authoring::{
        ZoneAuthoringHead, placements::ManualPlacement, promotion::PromotedInstance,
        session::ZoneRequest,
    },
};

/// Captured when selecting a tree; never refreshed implicitly during application.
pub struct TreePromotionSelection {
    pub head_hash: ContentHash,
    pub terrain_hash: ContentHash,
    pub vegetation_hash: ContentHash,
    pub layer_id: VegetationLayerId,
    pub instance_id: EntityId,
}

/// Rebuild authoritative sources instead of trusting a caller-supplied pose or plan.
/// No document or file mutation. Normal preview/execute and GLB verification remain required.
pub fn prepare_tree_promotion(
    head: &ZoneAuthoringHead,
    selection: &TreePromotionSelection,
    sources: &[forge_assets::VegetationPrototypeCatalog],
    browser: &forge_assets::asset_browser::AssetBrowser,
    placement_id: EntityId,
    name: String,
    command_id: CommandId,
) -> Result<ZoneRequest, String> {
    if head.content_hash()? != selection.head_hash {
        return Err("Promotion selection belongs to an older zone revision".into());
    }
    let (_, vegetation) = head.effective()?;
    let vegetation = vegetation.ok_or("Promotion requires a vegetation source")?;
    let terrain = forge_world::river_projection::project(head)?.terrain;
    let plan = forge_world::VegetationGenerationPlan::from_documents_with_buildings(
        &terrain,
        &vegetation,
        head.corrections.paths.as_deref().unwrap_or_default(),
        head.corrections.rivers.as_deref().unwrap_or_default(),
        head.corrections.buildings.as_deref().unwrap_or_default(),
    )
    .and_then(|plan| {
        plan.with_promotions(head.corrections.promotions.as_deref().unwrap_or_default())
    })
    .map_err(|error| error.to_string())?;
    let instance = plan
        .resolve_promotion_source(
            selection.terrain_hash,
            selection.vegetation_hash,
            &selection.layer_id,
            selection.instance_id,
        )
        .map_err(|error| error.to_string())?;
    let asset = super::resolve_promoted_tree_asset(head, sources, browser, &instance)?;
    head.prepare_promotion(
        PromotedInstance {
            layer_id: instance.layer_id,
            instance_id: instance.id.as_entity_id(),
            generation_hash: plan.source_hash(),
            placement_id,
        },
        ManualPlacement {
            particles: None,
            physics: None,
            id: placement_id,
            name,
            asset_id: asset.asset_id,
            asset_hash: asset.asset_hash,
            transform: asset.transform,
            locked: false,
        },
        command_id,
    )
}
