//! Terrain picking fills the start draft only; acceptance remains a reviewed command.
use forge_project::authoring::{AuthoringState, playtest::PlaytestStart};
use leptos::prelude::*;

pub(super) fn arm(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    x: RwSignal<String>,
    z: RwSignal<String>,
    facing: RwSignal<String>,
    message: RwSignal<String>,
) -> Result<(), String> {
    if !crate::authoring_viewport::can_preview_asset() {
        return Err("Attendre la projection du terrain avant de placer le départ".into());
    }
    let current = state.get_untracked().ok_or("Aucune zone")?;
    if busy.get_untracked() || current.playtest_hash.is_some() {
        return Err("Revenir en édition et attendre la commande en cours".into());
    }
    let expected = current
        .zone_head
        .ok_or("Une zone agrégée est requise")?
        .content_hash()?;
    let draft = (x.get_untracked(), z.get_untracked(), facing.get_untracked());
    let heading = if draft.2.trim().is_empty() {
        None
    } else {
        Some(draft.2.trim().parse::<f32>().map_err(|_| "Cap invalide")?)
    };
    if heading.is_some_and(|angle| !angle.is_finite()) {
        return Err("Cap fini requis".into());
    }
    crate::authoring_shortcuts::cancel_preview();
    crate::authoring_brush_pointer::arm(
        2.0,
        false,
        Callback::new(move |picked: Result<Vec<[f32; 2]>, String>| {
            let result = (|| {
                let current = state.get_untracked().ok_or("Zone fermée")?;
                if busy.get_untracked() || current.playtest_hash.is_some() {
                    return Err("Commande ou playtest en cours : recommencer le placement".into());
                }
                let head = current.zone_head.ok_or("Zone absente")?;
                if head.content_hash()? != expected
                    || (x.get_untracked(), z.get_untracked(), facing.get_untracked()) != draft
                {
                    return Err("Zone ou brouillon modifié : recommencer le placement".into());
                }
                let picked = picked?;
                let [px, pz] = *picked.first().ok_or("Aucun terrain sous le pointeur")?;
                PlaytestStart {
                    x_m: px,
                    z_m: pz,
                    facing_degrees: heading,
                }
                .validate(&head.base.terrain)?;
                x.set(px.to_string());
                z.set(pz.to_string());
                crate::authoring_shortcuts::cancel_preview();
                Ok("Départ placé dans le brouillon, cap conservé. Examiner puis Appliquer et Sauvegarder ; aucun point encore enregistré.".into())
            })();
            message.set(result.unwrap_or_else(|error| error));
        }),
    );
    message.set("Cliquer le terrain pour choisir le départ. Échap ou sortie de la vue annule ; éviter les obstacles.".into());
    Ok(())
}
