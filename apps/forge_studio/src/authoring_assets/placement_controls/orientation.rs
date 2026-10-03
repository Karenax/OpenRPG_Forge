//! Numeric controls use the same transform proposal path as translation and future gizmos.
use super::{PlacementEdit, SendCommand, edit, head};
use forge_core::{EntityId, Transform, Vec3};
use forge_project::authoring::{AuthoringRequest, AuthoringState};
use forge_world::{
    placement_pivot::rotate_about,
    placement_transform::{Axis, RotationSpace, set_scale},
};
use leptos::prelude::*;

#[component]
pub(super) fn OrientationControls(
    state: RwSignal<Option<AuthoringState>>,
    id: EntityId,
    transform: Transform,
    locked: bool,
    pivot: super::pivot::PivotSettings,
    on_command: SendCommand,
) -> impl IntoView {
    let axis = RwSignal::new("y".to_owned());
    let local = RwSignal::new(false);
    let angle = RwSignal::new("15".to_owned());
    let scales = [transform.scale.x, transform.scale.y, transform.scale.z]
        .map(|value| RwSignal::new(value.to_string()));
    let prepare = move |rotation| -> Result<AuthoringRequest, String> {
        let current = head(state.get_untracked())?;
        let source = super::super::objects::lookup(&current, id)?.transform;
        let target = if rotation {
            let axis = match axis.get_untracked().as_str() {
                "x" => Axis::X,
                "y" => Axis::Y,
                "z" => Axis::Z,
                _ => return Err("Axe de rotation invalide".into()),
            };
            let space = if local.get_untracked() {
                RotationSpace::Local
            } else {
                RotationSpace::World
            };
            let center = pivot.resolve()?.unwrap_or(source.translation_m);
            rotate_about(source, axis, number(angle)?, space, center)?
        } else {
            let [x, y, z] = scales.map(number);
            set_scale(source, Vec3::new(x?, y?, z?))?
        };
        edit(
            state.get_untracked(),
            PlacementEdit::Transform {
                id,
                transform: target,
            },
        )
    };
    view! {
        <details><summary>"Rotation et échelle précises"</summary>
            <p>"Rotation relative autour du pivot choisi. Prévisualiser plusieurs fois ne cumule pas l’angle ; Appliquer puis recommencer pour le cumuler."</p>
            <label>"Axe de rotation"<select prop:value=move || axis.get()
                on:change=move |ev| axis.set(event_target_value(&ev))>
                <option value="x">"X"</option><option value="y">"Y"</option><option value="z">"Z"</option>
            </select></label>
            <label><input type="checkbox" prop:checked=move || local.get()
                on:change=move |ev| local.set(event_target_checked(&ev))/>
                "Axe local de l’objet (sinon axe mondial)"</label>
            <label>"Angle relatif (degrés)"<input type="number" step="1"
                prop:value=move || angle.get() on:input=move |ev| angle.set(event_target_value(&ev))/></label>
            <button disabled=locked on:click=move |_| on_command.run(prepare(true))>"Prévisualiser la rotation"</button>
            <p>"Échelles locales absolues, strictement positives. La position et l’orientation sont conservées. Pas de miroir ni de cisaillement."</p>
            {scales.into_iter().zip(["Échelle X", "Échelle Y", "Échelle Z"]).map(|(value, label)| view! {
                <label>{label}<input type="number" step="0.01"
                    prop:value=move || value.get() on:input=move |ev| value.set(event_target_value(&ev))/></label>
            }).collect_view()}
            <button disabled=locked on:click=move |_| on_command.run(prepare(false))>"Prévisualiser l’échelle"</button>
        </details>
    }
}

fn number(signal: RwSignal<String>) -> Result<f32, String> {
    signal
        .get_untracked()
        .trim()
        .parse()
        .map_err(|_| "Valeur numérique invalide".into())
}
