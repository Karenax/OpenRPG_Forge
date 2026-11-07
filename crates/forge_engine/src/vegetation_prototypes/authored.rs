//! Resolve authoring references into the existing presentation catalog, without ECS mutation.
use forge_assets::{
    GovernedAssetKitBuildReport, VegetationPrototypeCatalog, VegetationPrototypeSelection,
};
use forge_project::{VegetationKind, zone_authoring::ZoneAuthoringHead};

pub struct PromotedTreeAsset {
    pub asset_id: forge_core::AssetId,
    pub asset_hash: forge_core::ContentHash,
    /// Raw GLB root pose with prototype normalization baked in.
    pub transform: forge_core::Transform,
}

/// Resolve the same deterministic species choice as the governed forest renderer.
/// The caller must first resolve the canonical instance against its projection hashes.
/// This is metadata qualification, not proof that the referenced GLB bytes loaded.
pub fn resolve_promoted_tree_asset(
    head: &ZoneAuthoringHead,
    sources: &[VegetationPrototypeCatalog],
    browser: &forge_assets::asset_browser::AssetBrowser,
    instance: &forge_world::VegetationInstance,
) -> Result<PromotedTreeAsset, String> {
    if instance.kind != VegetationKind::Tree {
        return Err("Only canonical trees can be promoted".into());
    }
    let catalog = resolve_authored_species(head, sources, browser.report())?
        .ok_or("Choose governed species before promoting a procedural tree")?;
    let binding = catalog
        .select_binding(instance.layer_id.as_str(), instance.id.as_entity_id())
        .map_err(|error| error.to_string())?
        .ok_or("Selected tree layer has no governed prototype")?;
    let (asset_id, hash) = browser
        .reference(&binding.asset_id)
        .map_err(|error| error.to_string())?;
    if hash != binding.glb_sha256 {
        return Err("Promotion asset differs from the rendered prototype pin".into());
    }
    let [x, y, z] = binding.source_pivot_m;
    let transform = forge_world::promotion_transform::from_scatter(
        instance.position_m,
        instance.yaw_radians,
        instance.scale,
        forge_core::Vec3::new(x, y, z),
        binding.uniform_scale(),
    )?;
    Ok(PromotedTreeAsset {
        asset_id,
        asset_hash: hash,
        transform,
    })
}

/// Missing or altered catalogs are errors, never a substitute species or silent fallback.
/// This verifies metadata only; normal GLB loading must still verify content and geometry.
pub fn resolve_authored_species(
    head: &ZoneAuthoringHead,
    sources: &[VegetationPrototypeCatalog],
    kit: &GovernedAssetKitBuildReport,
) -> Result<Option<VegetationPrototypeCatalog>, String> {
    head.validate()?;
    let selections = head.corrections.species.as_deref().unwrap_or_default();
    if selections.is_empty() {
        return Ok(None);
    }
    let (_, vegetation) = head.effective()?;
    let vegetation = vegetation.ok_or("Authored species require vegetation")?;
    let target = vegetation.content_hash().map_err(|e| e.to_string())?;
    let tree_ids: Vec<_> = vegetation
        .layers
        .iter()
        .filter(|layer| layer.kind == VegetationKind::Tree)
        .map(|layer| layer.id.as_str())
        .collect();
    let selections = selections
        .iter()
        .flat_map(|layer| {
            layer.prototypes.iter().map(|reference| {
                (
                    reference.catalog_sha256,
                    VegetationPrototypeSelection {
                        source_layer_id: reference.source_layer_id.as_str().into(),
                        asset_id: reference.asset_id.clone(),
                        target_layer_id: layer.layer_id.as_str().into(),
                    },
                )
            })
        })
        .collect::<Vec<_>>();
    forge_assets::resolve_pinned_vegetation_catalogs(sources, target, &tree_ids, &selections, kit)
        .map(Some)
        .map_err(|error| error.to_string())
}
