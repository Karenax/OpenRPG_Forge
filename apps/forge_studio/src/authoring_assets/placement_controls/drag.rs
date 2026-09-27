//! One terrain drag prepares one ordinary placement transform, never a direct file/ECS mutation.
use super::{PlacementEdit, SendCommand, edit, head};
use forge_core::EntityId;
use forge_project::authoring::AuthoringState;
use forge_world::{placement_snap::PlacementSnap, placement_transform::drag_xz};
use leptos::prelude::*;

pub(super) fn arm(
    state: RwSignal<Option<AuthoringState>>,
    id: EntityId,
    snap: PlacementSnap,
    on_command: SendCommand,
) -> Result<(), String> {
    if !crate::authoring_viewport::can_preview_asset() {
        return Err("Attendre la projection de la zone avant de déplacer un objet".into());
    }
    snap.validate()?;
    let source = head(state.get_untracked())?;
    let placement = source
        .corrections
        .placements
        .as_deref()
        .unwrap_or_default()
        .iter()
        .find(|placement| placement.id == id)
        .ok_or("Objet absent de la zone")?;
    if placement.locked {
        return Err("Déverrouiller l’objet avant déplacement".into());
    }
    let original = placement.transform;
    let expected = source.content_hash()?;
    super::super::cancel_preview();
    crate::authoring_brush_pointer::arm(
        1.0,
        true,
        Callback::new(move |points: Result<Vec<[f32; 2]>, String>| {
            let request = (|| {
                let current = head(state.get_untracked())?;
                if current.content_hash()? != expected {
                    return Err(
                        "La zone a changé pendant le geste : recommencer le déplacement".into(),
                    );
                }
                let points = points?;
                let start = points
                    .first()
                    .copied()
                    .ok_or("Geste sans point de départ")?;
                let end = points.last().copied().ok_or("Geste sans point d’arrivée")?;
                let mut target = drag_xz(original, start, end)?;
                if snap.grid_step_m.is_some() || snap.surface_offset_m.is_some() {
                    target = crate::authoring_raster::align(
                        &crate::authoring_scene::terrain(&current)?,
                        snap,
                        target,
                    )?;
                }
                if target == original {
                    return Err("Geste sans déplacement : aucune retouche préparée".into());
                }
                edit(
                    state.get_untracked(),
                    PlacementEdit::Transform {
                        id,
                        transform: target,
                    },
                )
            })();
            on_command.run(request);
        }),
    );
    super::super::placements::report(
        "Glisser sur le terrain puis relâcher : déplacement X/Z relatif de cet objet. Échap ou sortie de la vue annule. Appliquer ensuite l’aperçu.",
    );
    Ok(())
}
