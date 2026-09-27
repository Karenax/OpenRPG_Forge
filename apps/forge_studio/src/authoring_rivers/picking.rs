//! Picking changes X/Z in the local draft only; level and section remain authored.
use super::{AuthoringState, Points, head};
use leptos::prelude::*;

pub(super) fn trace(
    state: RwSignal<Option<AuthoringState>>,
    points: Points,
    status: RwSignal<String>,
) -> Result<(), String> {
    if !crate::authoring_viewport::can_preview_asset() {
        return Err("Attendre la projection du terrain avant de tracer".into());
    }
    let expected = head(state.get_untracked())?.content_hash()?;
    let draft = points.get_untracked();
    let section = super::read_points(points)?
        .first()
        .copied()
        .ok_or("Premier point de rivière absent")?;
    crate::authoring_brush_pointer::arm(
        2.0,
        true,
        Callback::new(move |picked: Result<Vec<[f32; 2]>, String>| {
            let result = (|| {
                if head(state.get_untracked())?.content_hash()? != expected
                    || points.get_untracked() != draft
                {
                    return Err(
                        "Zone ou brouillon modifié pendant le tracé : recommencer".to_owned()
                    );
                }
                let prepared = forge_world::river_stroke::prepare(&picked?, &section)?;
                let count = prepared.len();
                points.set(
                    prepared
                        .into_iter()
                        .map(|p| {
                            [
                                p.center_m.x,
                                p.center_m.y,
                                p.center_m.z,
                                p.surface_width_m,
                                p.bed_depth_m,
                            ]
                            .map(|v| v.to_string())
                        })
                        .collect(),
                );
                Ok(format!(
                    "Brouillon remplacé : {count} points. Niveau, largeur et profondeur du premier point repris. Examiner puis Appliquer."
                ))
            })();
            status.set(result.unwrap_or_else(|error| error));
        }),
    );
    status.set("Tracer en maintenant le bouton gauche puis relâcher. Remplace les points du brouillon ; niveau et section du premier point constants. Échap ou sortie de vue annule.".into());
    Ok(())
}

pub(super) fn arm(
    state: RwSignal<Option<AuthoringState>>,
    points: Points,
    index: usize,
    status: RwSignal<String>,
) -> Result<(), String> {
    if !crate::authoring_viewport::can_preview_asset() {
        return Err("Attendre la projection du terrain avant de placer le point".into());
    }
    let expected = head(state.get_untracked())?.content_hash()?;
    let draft = points.get_untracked();
    if index >= draft.len() {
        return Err("Point de rivière absent".into());
    }
    crate::authoring_brush_pointer::arm(
        2.0, // Cursor outline only; the hit remains the exact terrain ray intersection.
        false,
        Callback::new(move |picked: Result<Vec<[f32; 2]>, String>| {
            let result = (|| {
                if head(state.get_untracked())?.content_hash()? != expected
                    || points.get_untracked() != draft
                {
                    return Err(
                        "Zone ou brouillon modifié pendant le placement : recommencer".to_owned(),
                    );
                }
                let picked = picked?;
                let [x, z] = *picked.first().ok_or("Aucun terrain sous le pointeur")?;
                if !x.is_finite() || !z.is_finite() {
                    return Err("Intersection terrain invalide".into());
                }
                points.update(|draft| {
                    if let Some(point) = draft.get_mut(index) {
                        point[0] = x.to_string();
                        point[2] = z.to_string();
                    }
                });
                Ok(format!(
                    "Point {} placé dans le brouillon. Niveau Y, largeur et profondeur conservés. Examiner puis Appliquer.",
                    index + 1
                ))
            })();
            status.set(result.unwrap_or_else(|error| error));
        }),
    );
    status.set(format!("Cliquer le terrain pour placer X/Z du point {}. Niveau Y inchangé ; Échap ou sortie de la vue annule.", index + 1));
    Ok(())
}
