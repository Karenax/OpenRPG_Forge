//! Resolve authoring references into the existing presentation catalog, without ECS mutation.
use forge_assets::{
    GovernedAssetKitBuildReport, VegetationPrototypeCatalog, VegetationPrototypeSelection,
};
use forge_core::canonical_json_sha256;
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
    if sources.len() > 64 || sources.iter().any(|source| source.bindings.len() > 64) {
        return Err("Authored species catalog quota exceeded".into());
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
    let indexed = sources
        .iter()
        .map(|source| {
            canonical_json_sha256(source)
                .map(|hash| (hash, source))
                .map_err(|e| e.to_string())
        })
        .collect::<Result<Vec<_>, _>>()?;
    let mut bindings = Vec::new();
    for layer in selections {
        for reference in &layer.prototypes {
            let source = indexed
                .iter()
                .find(|(hash, _)| *hash == reference.catalog_sha256)
                .map(|(_, source)| *source)
                .ok_or_else(|| {
                    format!(
                        "Missing pinned species catalog {}",
                        reference.catalog_sha256
                    )
                })?;
            let selected = VegetationPrototypeSelection {
                source_layer_id: reference.source_layer_id.as_str().into(),
                asset_id: reference.asset_id.clone(),
                target_layer_id: layer.layer_id.as_str().into(),
            };
            let resolved = source
                .prepare_pinned_selection(
                    reference.catalog_sha256,
                    target,
                    &[layer.layer_id.as_str()],
                    &[selected],
                    kit,
                )
                .map_err(|e| e.to_string())?;
            bindings.extend(resolved.bindings);
        }
    }
    bindings.sort_by(|a, b| (&a.layer_id, &a.asset_id).cmp(&(&b.layer_id, &b.asset_id)));
    let catalog = VegetationPrototypeCatalog {
        schema: "forge.vegetation-prototype-catalog".into(),
        schema_version: 2,
        vegetation_sha256: target,
        asset_kit_id: kit.asset_kit_id.clone(),
        asset_kit_revision: kit.asset_kit_revision,
        bindings,
    };
    catalog
        .validate_against(target, &tree_ids, kit)
        .map_err(|e| e.to_string())?;
    Ok(Some(catalog))
}
