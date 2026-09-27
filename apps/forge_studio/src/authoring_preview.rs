//! Derived proposal overlay; the accepted terrain and its physics stay untouched.
use bevy::prelude::*;
use forge_project::{authoring::AuthoringState, terrain_edit::TerrainEditReceipt};
use forge_world::terrain_brush_preview::TerrainBrushPreview;
use std::cell::RefCell;

thread_local! {
    static PREVIEW: RefCell<Option<TerrainBrushPreview>> = const { RefCell::new(None) };
    static DENSITY: RefCell<Option<forge_world::vegetation_density_preview::DensityMaskPreview>> = const { RefCell::new(None) };
}

pub(super) fn clear() {
    super::authoring_vegetation::cancel_promotion_gate();
    super::authoring_vegetation::restore();
    super::authoring_building_projection::restore();
    super::authoring_path_projection::restore();
    super::authoring_assets::placements::restore();
    PREVIEW.with(|slot| slot.replace(None));
    DENSITY.with(|slot| slot.replace(None));
}

pub(super) fn prepare_density(
    head: &forge_project::zone_authoring::ZoneAuthoringHead,
    receipt: &forge_project::zone_authoring::session::ZoneReceipt,
) -> String {
    clear();
    match forge_world::vegetation_density_preview::DensityMaskPreview::prepare(head, receipt) {
        Ok(Some(preview)) => {
            let label = format!(
                " · masque logique : {}/{} marques, magenta < 50 %, vert ≥ 50 % de la densité recette",
                preview.marks.len(),
                preview.changed_samples
            );
            DENSITY.with(|slot| slot.replace(Some(preview)));
            label
        }
        Ok(None) => " · aucun changement de densité spatiale".into(),
        Err(error) => format!(" · aperçu indisponible : {error}"),
    }
}

pub(super) fn prepare(state: &AuthoringState, receipt: &TerrainEditReceipt) -> String {
    clear();
    let result = (|| {
        let document = state.document.as_ref().ok_or("Aucune zone")?;
        if document.content_hash().map_err(|e| e.to_string())? != receipt.request.expected_hash {
            return Err("Aperçu périmé".to_owned());
        }
        TerrainBrushPreview::prepare(document, &receipt.applied)
    })();
    display(result)
}

pub(super) fn prepare_zone(
    state: &AuthoringState,
    receipt: &forge_project::zone_authoring::session::ZoneReceipt,
) -> String {
    use forge_project::terrain_edit::TerrainEdit;
    clear();
    let result = (|| {
        let head = state.zone_head.as_ref().ok_or("Aucune tête de zone")?;
        if head.content_hash()? != receipt.request.expected_hash
            || head.revision != receipt.request.expected_revision
        {
            return Err("Aperçu de zone périmé".to_owned());
        }
        let before = super::authoring_scene::terrain(head)?;
        let mut candidate = head.clone();
        candidate.corrections = receipt.applied.clone();
        candidate.version = candidate.corrections.schema_version();
        let after = super::authoring_scene::terrain(&candidate)?;
        // Current terrain tools change exactly one typed layer per proposal.
        let edit = if before.height_deltas != after.height_deltas {
            TerrainEdit::SetHeightDeltas {
                samples: after.height_deltas,
            }
        } else if before.material_overrides != after.material_overrides {
            TerrainEdit::SetMaterialOverrides {
                samples: after.material_overrides,
            }
        } else if before.height_sampling.base_elevation_m.to_bits()
            != after.height_sampling.base_elevation_m.to_bits()
        {
            TerrainEdit::SetBaseElevation {
                metres: after.height_sampling.base_elevation_m,
            }
        } else {
            return Ok(None);
        };
        TerrainBrushPreview::prepare(&before, &edit)
    })();
    display(result)
}

fn display(result: Result<Option<TerrainBrushPreview>, String>) -> String {
    match result {
        Ok(Some(preview)) => {
            let label = format!(
                " · aperçu filaire/couleur logique : {}/{} points (non appliqué)",
                preview.displayed_samples, preview.changed_samples
            );
            PREVIEW.with(|slot| slot.replace(Some(preview)));
            label
        }
        Ok(None) => String::new(),
        Err(error) => format!(" · aperçu 3D indisponible : {error}"),
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
pub(super) fn draw(state: Res<forge_engine::TerrainRuntimeState>, mut gizmos: Gizmos) {
    draw_density(state.cache().source_document_hash, &mut gizmos);
    PREVIEW.with(|slot| {
        let mut preview = slot.borrow_mut();
        if preview
            .as_ref()
            .is_some_and(|p| p.source_hash != state.cache().source_document_hash)
        {
            *preview = None;
        }
        if let Some(preview) = preview.as_ref() {
            let lift = Vec3::Y * 0.07;
            for (position, weights) in &preview.material_marks {
                let point = Vec3::from_array(*position) + lift;
                let [grass, earth, rock] = *weights;
                let color = Color::srgb(
                    0.2 * grass + 0.7 * earth + 0.65 * rock,
                    0.9 * grass + 0.4 * earth + 0.65 * rock,
                    0.15 * grass + 0.15 * earth + 0.7 * rock,
                );
                gizmos.line(point - Vec3::X * 0.8, point + Vec3::X * 0.8, color);
                gizmos.line(point - Vec3::Z * 0.8, point + Vec3::Z * 0.8, color);
            }
            for [before, after, neighbor] in &preview.lines {
                gizmos.line(
                    Vec3::from_array(*before) + lift,
                    Vec3::from_array(*after) + lift,
                    Color::srgb(1.0, 0.5, 0.0),
                );
                gizmos.line(
                    Vec3::from_array(*after) + lift,
                    Vec3::from_array(*neighbor) + lift,
                    Color::srgb(0.1, 1.0, 1.0),
                );
            }
        }
    });
}

fn draw_density(hash: forge_core::ContentHash, gizmos: &mut Gizmos) {
    DENSITY.with(|slot| {
        let mut slot = slot.borrow_mut();
        if slot.as_ref().is_some_and(|p| p.terrain_hash != hash) {
            *slot = None;
        }
        if let Some(preview) = slot.as_ref() {
            for (position, density) in &preview.marks {
                let point = Vec3::from_array(*position) + Vec3::Y * 0.1;
                let color = if *density < 0.5 {
                    Color::srgb(1.0, 0.1, 0.8)
                } else {
                    Color::srgb(0.1, 1.0, 0.5)
                };
                gizmos.line(point - Vec3::X, point + Vec3::X, color);
                gizmos.line(point - Vec3::Z, point + Vec3::Z, color);
            }
        }
    });
}
