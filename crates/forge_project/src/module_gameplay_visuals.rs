//! Authored visual bindings: gameplay identities and physics stay authoritative.
use forge_core::{AreaId, AssetId, ContentHash, EntityId, Transform};
use forge_gameplay::GameplayDocument;
use serde::{Deserialize, Serialize};
use std::collections::BTreeSet;

pub const MAX_GAMEPLAY_VISUAL_BINDINGS: usize = 8192;

#[derive(Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct VisualAssetPin {
    pub asset_id: AssetId,
    pub sha256: ContentHash,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct GameplayVisualBinding {
    pub area_id: AreaId,
    pub entity_id: EntityId,
    pub asset: VisualAssetPin,
    /// Local to the existing gameplay body, never a change to its physical pose.
    pub local_transform: Transform,
}

pub fn validate_catalog(
    bindings: &[GameplayVisualBinding],
    areas: &[AreaId],
    assets: &[VisualAssetPin],
) -> Result<(), String> {
    if bindings.len() > MAX_GAMEPLAY_VISUAL_BINDINGS {
        return Err("Gameplay visual binding limit exceeded".into());
    }
    let areas: BTreeSet<_> = areas.iter().copied().collect();
    let assets: BTreeSet<_> = assets.iter().copied().collect();
    let mut identities = BTreeSet::new();
    for binding in bindings {
        if !areas.contains(&binding.area_id) || binding.area_id.as_uuid().is_nil() {
            return Err(format!(
                "Gameplay visual area is absent: {}",
                binding.area_id
            ));
        }
        if binding.entity_id.as_uuid().is_nil()
            || !identities.insert((binding.area_id, binding.entity_id))
        {
            return Err(format!(
                "Duplicate or invalid gameplay visual identity: {}",
                binding.entity_id
            ));
        }
        if !assets.contains(&binding.asset) || binding.asset.asset_id.as_uuid().is_nil() {
            return Err(format!(
                "Gameplay visual asset pin is absent: {}",
                binding.asset.asset_id
            ));
        }
        binding
            .local_transform
            .validate()
            .map_err(|e| format!("Gameplay visual transform {}: {e}", binding.entity_id))?;
    }
    Ok(())
}

pub fn validate_for_document(
    bindings: &[GameplayVisualBinding],
    document: &GameplayDocument,
    assets: &[VisualAssetPin],
) -> Result<(), String> {
    document.validate().map_err(|e| e.to_string())?;
    validate_catalog(bindings, &[document.area_id], assets)?;
    for binding in bindings {
        if binding.entity_id == document.player
            || (document.actor(binding.entity_id).is_none()
                && document.object(binding.entity_id).is_none())
        {
            return Err(format!(
                "Gameplay visual target is absent or is the first-person player: {}",
                binding.entity_id
            ));
        }
    }
    Ok(())
}

/// Pure reviewed replacement: failure never mutates the input or gameplay document.
pub fn replace_asset(
    bindings: &[GameplayVisualBinding],
    area: AreaId,
    entities: &[EntityId],
    expected: VisualAssetPin,
    replacement: VisualAssetPin,
    assets: &[VisualAssetPin],
) -> Result<Vec<GameplayVisualBinding>, String> {
    if entities.is_empty() || entities.len() > MAX_GAMEPLAY_VISUAL_BINDINGS {
        return Err("A bounded, nonempty visual replacement selection is required".into());
    }
    let selected: BTreeSet<_> = entities.iter().copied().collect();
    if selected.len() != entities.len() || !assets.contains(&replacement) {
        return Err("Duplicate visual target or undeclared replacement asset pin".into());
    }
    let areas = bindings.iter().map(|b| b.area_id).collect::<Vec<_>>();
    let mut prior_assets = assets.to_vec();
    prior_assets.push(expected);
    validate_catalog(bindings, &areas, &prior_assets)?;
    for id in &selected {
        if !bindings
            .iter()
            .any(|b| b.area_id == area && b.entity_id == *id && b.asset == expected)
        {
            return Err(format!(
                "Visual replacement review is stale or target is absent: {id}"
            ));
        }
    }
    let mut changed = bindings.to_vec();
    for binding in &mut changed {
        if binding.area_id == area && selected.contains(&binding.entity_id) {
            binding.asset = replacement;
        }
    }
    validate_catalog(&changed, &areas, assets)?;
    Ok(changed)
}
