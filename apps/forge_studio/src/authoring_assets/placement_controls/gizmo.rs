use super::super::placements::gizmo::Mode;
use super::{PlacementEdit, SendCommand, edit, head};
use forge_core::{EntityId, Transform};
use forge_project::authoring::AuthoringState;
use forge_world::placement_snap::PlacementSnap;
use forge_world::placement_transform::RotationSpace;
use leptos::prelude::*;

pub(super) fn bind_selection(state: RwSignal<Option<AuthoringState>>, on_command: SendCommand) {
    super::super::placements::gizmo::bind_auto(Callback::new(move |id| {
        crate::authoring_brush_pointer::cancel();
        arm(
            state,
            id,
            Mode::Translation,
            PlacementSnap::default(),
            on_command,
        )
    }));
    on_cleanup(super::super::placements::gizmo::unbind_auto);
}

#[component]
pub(super) fn GizmoControls(
    state: RwSignal<Option<AuthoringState>>,
    id: EntityId,
    locked: bool,
    pivot: super::pivot::PivotSettings,
    alignment: Callback<(), Result<PlacementSnap, String>>,
    on_command: SendCommand,
) -> impl IntoView {
    let angular_step = RwSignal::new("free".to_owned());
    let local_rotation = RwSignal::new(false);
    let local_translation = RwSignal::new(false);
    let options = RwSignal::new(false);
    view! {
        <div class="gold-gizmo" class:gold-gizmo-options=move || options.get()>
        <button on:click=move |_| options.update(|value| *value = !*value)>{move || if options.get() { "← Masquer les options de manipulation" } else { "Options de manipulation" }}</button>
        <label><input type="checkbox" prop:checked=move || local_translation.get()
            on:change=move |ev| local_translation.set(event_target_checked(&ev))/>
            "Déplacement selon les axes locaux (sinon mondiaux)"</label>
        <button disabled=locked on:click=move |_| {
            let result = alignment.run(()).and_then(|snap| {
                let mode = if local_translation.get_untracked() { Mode::LocalTranslation { step_m: snap.grid_step_m } } else { Mode::Translation };
                arm(state, id, mode, snap, on_command)
            });
            if let Err(error) = result { on_command.run(Err(error)); }
        }>"Déplacer · X / Y / Z"</button>
        <p>"Repère local : pas de grille appliqué à la distance depuis le départ, sur l’axe choisi, en mètres indépendants de l’échelle. Désactiver l’alignement surface ; il imposerait un déplacement hors de cet axe. Réactiver les poignées après un changement de réglage."</p>
        <p>"Glisser une poignée rouge X, verte Y ou bleue Z. Relâcher puis Appliquer. Échap annule. En repère mondial : grille sur l’axe X/Z actif seulement, selon les réglages de Position précise au départ. Désactiver la surface pour déplacer Y. Tourner la caméra si un axe est vu de face."</p>
        <label>"Pas angulaire des anneaux (degrés)"
            <select prop:value=move || angular_step.get()
                on:change=move |ev| angular_step.set(event_target_value(&ev))>
                <option value="free">"Libre"</option>
                <option value="1">"1°"</option><option value="5">"5°"</option>
                <option value="15">"15°"</option><option value="30">"30°"</option>
                <option value="45">"45°"</option><option value="90">"90°"</option>
            </select>
        </label>
        <label><input type="checkbox" prop:checked=move || local_rotation.get()
            on:change=move |ev| local_rotation.set(event_target_checked(&ev))/>
            "Anneaux dans le repère local de l’objet (sinon mondial)"</label>
        <button disabled=locked on:click=move |_| {
            let result = (|| {
                let value = angular_step.get_untracked();
                let step_degrees = if value == "free" { None } else {
                    Some(value.parse::<f32>().map_err(|_| "Pas angulaire invalide".to_owned())?)
                };
                forge_world::placement_rotation::snap_degrees(0.0, step_degrees)?;
                let space = if local_rotation.get_untracked() { RotationSpace::Local } else { RotationSpace::World };
                arm(state, id, Mode::Rotation { step_degrees, space, pivot: pivot.resolve()? }, PlacementSnap::default(), on_command)
            })();
            if let Err(error) = result { on_command.run(Err(error)); }
        }>"Tourner · X / Y / Z"</button>
        <p>"Glisser un anneau autour du pivot : position et échelle conservées. Le repère et le pas sont capturés à l’activation. L’angle reste relatif au départ, sans cumul par frame. Réactiver les anneaux après un changement de réglage. Anneau vu de profil : tourner la caméra. Relâcher puis Appliquer ; Échap annule."</p>
        <button disabled=locked on:click=move |_| {
            if let Err(error) = arm(state, id, Mode::Scale, PlacementSnap::default(), on_command) { on_command.run(Err(error)); }
        }>"Échelle · X / Y / Z"</button>
        <p>"Glisser une poignée cubique : échelle relative sur cet axe local, pivot et rotation conservés. Une longueur de poignée ajoute 100 % à l’échelle de départ. Échelle nulle/négative refusée, aucun miroir. Relâcher puis Appliquer ; Échap annule."</p>
        <output id="forge-placement-gizmo-diagnostic" aria-live="off" style="display:none">"{\"schema\":\"forge-placement-gizmo-diagnostic\",\"version\":1,\"state\":\"idle\"}"</output>
        </div>
    }
}

fn arm(
    state: RwSignal<Option<AuthoringState>>,
    id: EntityId,
    mode: Mode,
    snap: PlacementSnap,
    on_command: SendCommand,
) -> Result<(), String> {
    if !super::super::placements::gizmo::can_arm() {
        return Err("Attendre la projection de la zone".into());
    }
    let current = head(state.get_untracked())?;
    snap.validate()?;
    let local = matches!(mode, Mode::LocalTranslation { .. });
    if local && snap.surface_offset_m.is_some() {
        return Err("Désactiver l’alignement surface pour déplacer selon un axe local".into());
    }
    let alignment = if !local && (snap.grid_step_m.is_some() || snap.surface_offset_m.is_some()) {
        Some(crate::authoring_raster::prepare_alignment(
            &crate::authoring_scene::terrain(&current)?,
            snap,
        )?)
    } else {
        None
    };
    let placement = super::super::objects::lookup(&current, id)?;
    if placement.locked {
        return Err("Déverrouiller l’objet avant déplacement".into());
    }
    let original = placement.transform;
    let expected = current.content_hash()?;
    super::super::cancel_preview();
    super::super::placements::gizmo::arm(
        mode,
        id,
        original,
        alignment,
        Callback::new(move |result: Result<Transform, String>| {
            let request = (|| {
                let current = head(state.get_untracked())?;
                if current.content_hash()? != expected {
                    return Err("Zone modifiée pendant le geste ; déplacement abandonné".into());
                }
                let transform = result?;
                if transform == original {
                    return Err("Aucun déplacement : aucune retouche préparée".into());
                }
                edit(
                    state.get_untracked(),
                    PlacementEdit::Transform { id, transform },
                )
            })();
            on_command.run(request);
        }),
    );
    super::super::placements::report(
        "Poignées X/Y/Z actives. Glisser une poignée puis relâcher pour prévisualiser une retouche.",
    );
    Ok(())
}
