//! Numeric counterpart of local-axis handles; both submit ordinary Transform proposals.
use super::{PlacementEdit, PlacementSnap, SendCommand, edit, head};
use forge_core::EntityId;
use forge_project::authoring::{AuthoringRequest, AuthoringState};
use forge_world::{placement_axis::translate_local, placement_transform::Axis};
use leptos::prelude::*;

#[component]
pub(super) fn LocalTranslationControls(
    state: RwSignal<Option<AuthoringState>>,
    id: EntityId,
    locked: bool,
    alignment: Callback<(), Result<PlacementSnap, String>>,
    on_command: SendCommand,
) -> impl IntoView {
    let axis = RwSignal::new("x".to_owned());
    let distance = RwSignal::new("1".to_owned());
    let prepare = move || -> Result<AuthoringRequest, String> {
        let current = head(state.get_untracked())?;
        let source = super::super::objects::lookup(&current, id)?.transform;
        let axis = match axis.get_untracked().as_str() {
            "x" => Axis::X,
            "y" => Axis::Y,
            "z" => Axis::Z,
            _ => return Err("Axe local invalide".into()),
        };
        let distance = distance
            .get_untracked()
            .trim()
            .parse::<f32>()
            .map_err(|_| "Distance locale invalide")?;
        let snap = alignment.run(())?;
        snap.validate()?;
        if snap.surface_offset_m.is_some() {
            return Err("Désactiver l’alignement surface pour déplacer selon un axe local".into());
        }
        let transform = translate_local(source, axis, distance, snap.grid_step_m)?;
        if transform == source {
            return Err("Aucun déplacement après alignement : aucune retouche préparée".into());
        }
        edit(
            state.get_untracked(),
            PlacementEdit::Transform { id, transform },
        )
    };
    view! {
        <details><summary>"Déplacement local précis"</summary>
            <label>"Axe local"<select prop:value=move || axis.get()
                on:change=move |ev| axis.set(event_target_value(&ev))>
                <option value="x">"X"</option><option value="y">"Y"</option><option value="z">"Z"</option>
            </select></label>
            <label>"Distance relative (m)"<input type="number" step="0.01"
                prop:value=move || distance.get() on:input=move |ev| distance.set(event_target_value(&ev))/></label>
            <p>"Selon l’orientation actuelle, sans multiplier la distance par l’échelle. Même pas de grille que les poignées locales, depuis le départ. Désactiver la surface. Prévisualiser ne cumule pas ; Appliquer puis recommencer pour cumuler."</p>
            <button disabled=locked on:click=move |_| on_command.run(prepare())>"Prévisualiser le déplacement local"</button>
        </details>
    }
}
