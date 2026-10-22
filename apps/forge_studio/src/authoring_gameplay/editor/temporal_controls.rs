//! Temporal authoring through the common preview/apply/history boundary.
use super::{Edit, edit, id, source_hash};
use forge_core::{EntityId, canonical_json_sha256};
use forge_gameplay::{CreatorSurface, GameplayDocument};
use forge_project::authoring::AuthoringState;
use leptos::prelude::*;

mod abilities;
mod calendar;
mod condition;
mod effects;
mod encounters;

fn document(state: RwSignal<Option<AuthoringState>>) -> Option<GameplayDocument> {
    state
        .get()?
        .zone_head?
        .corrections
        .gameplay
        .map(|document| *document)
}

fn identity(
    source: &GameplayDocument,
    prefix: &str,
    selected: &str,
    create: bool,
) -> Result<EntityId, String> {
    if !create {
        return id(selected);
    }
    let hash = canonical_json_sha256(source).map_err(|error| error.to_string())?;
    Ok(EntityId::deterministic(format!(
        "{prefix}/{}/{hash}",
        source.area_id
    )))
}

fn integer<T: std::str::FromStr>(value: &str, label: &str) -> Result<T, String> {
    value
        .trim()
        .parse()
        .map_err(|_| format!("{label} : saisir un entier dans les bornes affichées."))
}

#[component]
pub(super) fn TemporalControls(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    send: Callback<Edit>,
) -> impl IntoView {
    let disabled = move || {
        busy.get()
            || state
                .get()
                .is_none_or(|state| state.zone_head.is_none() || state.playtest_hash.is_some())
    };
    view! {
        <Show when=move || document(state).is_some_and(|source|source.surface_visible(CreatorSurface::Calendar))>
            <details><summary>"Horloge et calendrier"</summary><fieldset disabled=disabled>
                <calendar::CalendarControls state=state send=send/>
            </fieldset></details>
        </Show>
        <Show when=move || document(state).is_some_and(|source|source.surface_visible(CreatorSurface::AbilitiesAndEffects))>
            <details><summary>"Effets temporaires"</summary><fieldset disabled=disabled>
                <effects::EffectControls state=state send=send/>
                <abilities::AbilityControls state=state send=send/>
            </fieldset></details>
        </Show>
        <Show when=move || document(state).is_some_and(|source|source.surface_visible(CreatorSurface::Encounters))>
            <details><summary>"Rencontres planifiées"</summary><fieldset disabled=disabled>
                <encounters::EncounterControls state=state send=send/>
            </fieldset></details>
        </Show>
    }
}
