//! Rebuild river terrain from the immutable authoring head, never from a previous cache.
use forge_core::{ContentHash, EntityId};
use forge_project::{
    TerrainDocument, TerrainHeightDelta, WaterDocument, terrain_edit::TerrainEdit,
    zone_authoring::ZoneAuthoringHead,
};
use std::collections::{BTreeMap, BTreeSet};

/// Prepare a normal recipe command and reject unprojectable insertions/replacements
/// before host review. Unlock/removal remain available to repair invalid recipes.
pub fn prepare_edit(
    head: &ZoneAuthoringHead,
    command_id: forge_core::CommandId,
    edit: forge_project::zone_authoring::rivers::RiverEdit,
) -> Result<forge_project::zone_authoring::session::ZoneRequest, String> {
    use forge_project::zone_authoring::{rivers::RiverEdit, session::ZoneAction};
    let preflight = matches!(edit, RiverEdit::Insert { .. } | RiverEdit::Replace { .. });
    let request = head.prepare_river(command_id, edit)?;
    if preflight {
        let ZoneAction::Edit(corrections) = &request.action else {
            return Err("River preparation requires a recipe edit".into());
        };
        let mut candidate = head.clone();
        candidate.corrections = (**corrections).clone();
        candidate.version = candidate.corrections.schema_version();
        project(&candidate)?;
    }
    Ok(request)
}

#[derive(Clone, Debug, PartialEq)]
pub struct RiverProjection {
    pub source_hash: ContentHash,
    pub terrain: TerrainDocument,
    pub waters: Vec<(EntityId, WaterDocument)>,
}

/// A conflicting manual height change rejects the entire projection. No source
/// is changed, no partial output is returned, and no runtime readiness is implied.
pub fn project(head: &ZoneAuthoringHead) -> Result<RiverProjection, String> {
    let source_hash = head.content_hash()?;
    let (source, _) = head.effective()?;
    let original = values(&source.height_deltas);
    let protected = protected_samples(head);
    let mut combined = original.clone();
    let mut waters = Vec::new();
    for river in head.corrections.rivers.as_deref().unwrap_or_default() {
        let preview = crate::river_carving::derive(&source, &river.water, None)
            .map_err(|error| format!("River {}: {error}", river.id))?;
        let TerrainEdit::SetHeightDeltas { samples } = preview.edit else {
            return Err("River projection requires a height proposal".into());
        };
        let candidate = values(&samples);
        // Include removed zero entries as well, so a cut cancelling a positive
        // authored base delta is not lost during sparse-map combination.
        let keys: BTreeSet<_> = original.keys().chain(candidate.keys()).copied().collect();
        for key in keys {
            let old = original.get(&key).copied().unwrap_or(0.0);
            let new = candidate.get(&key).copied().unwrap_or(0.0);
            if new >= old {
                continue;
            }
            if protected.contains(&key) {
                return Err(format!(
                    "River {} conflicts with manual terrain sample ({}, {})",
                    river.id, key.1, key.0
                ));
            }
            let value = combined.get(&key).copied().unwrap_or(0.0).min(new);
            if value == 0.0 {
                combined.remove(&key);
            } else {
                combined.insert(key, value);
            }
        }
        // A recipe keeps its authored revision across zone edits. Like the
        // effective terrain/vegetation, its derived document belongs to the
        // current aggregate revision; never rewrite the persisted recipe.
        let mut water = river.water.clone();
        water.revision = source.revision;
        waters.push((river.id, water));
    }
    let mut terrain = source;
    terrain.height_deltas = combined
        .into_iter()
        .map(|((z, x), delta)| TerrainHeightDelta::new(x, z, delta))
        .collect();
    terrain.validate().map_err(|e| e.to_string())?;
    Ok(RiverProjection {
        source_hash,
        terrain,
        waters,
    })
}

fn values(samples: &[TerrainHeightDelta]) -> BTreeMap<(u16, u16), f32> {
    samples.iter().map(|p| (p.sort_key(), p.delta_m)).collect()
}

/// Paint the visible terrain, persisting only samples actually changed by the gesture.
/// Untouched river carving stays derived. A river conflicting with the requested
/// manual result rejects the proposal rather than erasing that retouch.
pub fn prepare_brush(
    head: &ZoneAuthoringHead,
    stroke: &crate::terrain_brush_stroke::TerrainBrushStroke,
) -> Result<TerrainEdit, String> {
    let visible = project(head)?.terrain;
    let TerrainEdit::SetHeightDeltas { samples } = stroke.prepare(&visible)? else {
        return Err("Terrain brush requires a height proposal".into());
    };
    let before = values(&visible.height_deltas);
    let after = values(&samples);
    let mut canonical = values(&head.effective()?.0.height_deltas);
    let keys: BTreeSet<_> = before.keys().chain(after.keys()).copied().collect();
    for key in keys {
        let old = before.get(&key).copied().unwrap_or(0.0);
        let new = after.get(&key).copied().unwrap_or(0.0);
        if old.to_bits() == new.to_bits() {
            continue;
        }
        if new == 0.0 {
            canonical.remove(&key);
        } else {
            canonical.insert(key, new);
        }
    }
    let samples: Vec<_> = canonical
        .into_iter()
        .map(|((z, x), delta)| TerrainHeightDelta::new(x, z, delta))
        .collect();
    let mut candidate = head.clone();
    candidate.corrections.height_deltas = Some(samples.clone());
    let projected = project(&candidate)?;
    // Also reject interactions where a changed shoulder would move untouched
    // samples: the preview must match the gesture, not a hidden secondary edit.
    if values(&projected.terrain.height_deltas) != after {
        return Err("Retouche incompatible avec le creusement de rivière : ajuster le tracé ou le profil avant de peindre ici.".into());
    }
    Ok(TerrainEdit::SetHeightDeltas { samples })
}

fn protected_samples(head: &ZoneAuthoringHead) -> BTreeSet<(u16, u16)> {
    let Some(overrides) = &head.corrections.height_deltas else {
        return BTreeSet::new();
    };
    let base = values(&head.base.terrain.height_deltas);
    let manual = values(overrides);
    base.keys()
        .chain(manual.keys())
        .copied()
        .filter(|key| {
            base.get(key).copied().unwrap_or(0.0).to_bits()
                != manual.get(key).copied().unwrap_or(0.0).to_bits()
        })
        .collect()
}
