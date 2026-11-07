//! Scene wind uses the normal reviewed zone-edit flow.
use forge_project::authoring::{AuthoringRequest, AuthoringState};
use leptos::prelude::*;

#[component]
pub(super) fn WindControls(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    on_command: Callback<Result<AuthoringRequest, String>>,
) -> impl IntoView {
    let heading = RwSignal::new(String::new());
    let speed = RwSignal::new(String::new());
    let accepted = Memo::new(move |_| {
        state.with(|state| {
            state
                .as_ref()
                .and_then(|state| state.zone_head.as_ref())
                .map(|head| (head.base.terrain.area_id, head.corrections.wind))
        })
    });
    Effect::new(move |_| {
        let settings = accepted.get().and_then(|(_, wind)| wind);
        heading.set(settings.map_or_else(String::new, |wind| wind.heading_degrees.to_string()));
        speed.set(settings.map_or_else(String::new, |wind| wind.speed_mps.to_string()));
    });
    let disabled = move || {
        busy.get()
            || state
                .get()
                .is_none_or(|state| state.zone_head.is_none() || state.playtest_hash.is_some())
    };
    let prepare = move |remove: bool| {
        let result = (|| {
            let current = state.get_untracked().ok_or("Aucune zone")?;
            if current.playtest_hash.is_some() {
                return Err("Revenir en édition pour régler le vent".into());
            }
            let head = current.zone_head.ok_or("Une zone est requise")?;
            let settings = if remove {
                None
            } else {
                Some(super::authoring_wind_request::draft(
                    &heading.get_untracked(),
                    &speed.get_untracked(),
                )?)
            };
            super::authoring_wind_request::preview(&head, settings)
        })();
        on_command.run(result);
    };
    view! {
        <details class="authoring-wind-settings">
            <summary>"Vent de la carte"</summary>
            <p>"Le vent souffle vers : 0° nord, 90° est (moins de 360°). Vitesse : 0 à 25 m/s. Examiner, appliquer puis sauvegarder."</p>
            <output aria-live="polite">{move || accepted.get().and_then(|(_, wind)| wind).map_or_else(
                || "Aucun vent défini : les réglages existants restent utilisés.".into(),
                |wind| format!("Vent appliqué : {} m/s vers {}°", wind.speed_mps, wind.heading_degrees),
            )}</output>
            <label>"Direction (°)"<input type="number" min="0" max="360" step="any" disabled=disabled
                prop:value=move || heading.get() on:input=move |event| { heading.set(event_target_value(&event)); super::authoring_shortcuts::cancel_preview(); }/></label>
            <label>"Vitesse (m/s)"<input type="number" min="0" max="25" step="any" disabled=disabled
                prop:value=move || speed.get() on:input=move |event| { speed.set(event_target_value(&event)); super::authoring_shortcuts::cancel_preview(); }/></label>
            <button disabled=disabled on:click=move |_| prepare(false)>"Examiner le vent"</button>
            <button disabled=move || disabled() || accepted.get().is_none_or(|(_, wind)| wind.is_none())
                on:click=move |_| prepare(true)>"Retirer le vent défini"</button>
        </details>
    }
}
