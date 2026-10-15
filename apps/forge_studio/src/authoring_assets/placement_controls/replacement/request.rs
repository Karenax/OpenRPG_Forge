//! A reviewed ordinary target never silently drops a saved physical package.
use forge_core::{AssetId, ContentHash};
use forge_project::zone_authoring::placements::{ManualPlacement, PlacementEdit};

pub(super) fn same_reference(a: &ManualPlacement, b: &ManualPlacement) -> bool {
    (a.asset_id, a.asset_hash, &a.physics, &a.particles)
        == (b.asset_id, b.asset_hash, &b.physics, &b.particles)
}

pub(super) fn prepare(
    placements: &[ManualPlacement],
    source: &ManualPlacement,
    target: (AssetId, ContentHash),
    include_matching: bool,
    remove_physics: bool,
) -> Result<PlacementEdit, String> {
    let ids = placements
        .iter()
        .filter(|p| p.id == source.id || (include_matching && same_reference(p, source)))
        .map(|p| p.id)
        .collect();
    if source.physics.is_some() || source.particles.is_some() {
        if !remove_physics {
            return Err("Confirmer le retrait du paquet physique pour ce remplacement".into());
        }
        Ok(PlacementEdit::ReplacePinnedAsset {
            ids,
            expected_asset_id: source.asset_id,
            expected_asset_hash: source.asset_hash,
            expected_physics: source.physics.clone(),
            expected_particles: source.particles.clone(),
            asset_id: target.0,
            asset_hash: target.1,
            physics: None,
            particles: None,
        })
    } else {
        Ok(PlacementEdit::ReplaceAsset {
            ids,
            expected_asset_id: source.asset_id,
            expected_asset_hash: source.asset_hash,
            asset_id: target.0,
            asset_hash: target.1,
        })
    }
}
