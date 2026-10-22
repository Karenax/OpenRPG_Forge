use super::{Edit, document, edit, id, identity, integer, source_hash};
use forge_gameplay::{CalendarEventDefinition, SimulationClockDefinition};
use forge_project::authoring::AuthoringState;
use leptos::prelude::*;

#[derive(Clone, Copy)]
struct ClockFields {
    day: RwSignal<String>,
    minute: RwSignal<String>,
    duration: RwSignal<String>,
}

impl ClockFields {
    fn preview(self, state: Option<AuthoringState>) -> Edit {
        edit(state, move |_, source| {
            let mut simulation = source.simulation.clone().unwrap_or_default();
            simulation.clock = SimulationClockDefinition {
                start_day: integer(&self.day.get_untracked(), "Jour de départ")?,
                start_minute: integer(&self.minute.get_untracked(), "Minute de départ")?,
                milliseconds_per_minute: integer(
                    &self.duration.get_untracked(),
                    "Durée d’une minute",
                )?,
            };
            *source = source
                .with_simulation_definition(Some(simulation))
                .map_err(|error| error.to_string())?;
            Ok(())
        })
    }
}

#[derive(Clone, Copy)]
struct EventFields {
    selected: RwSignal<String>,
    name: RwSignal<String>,
    first: RwSignal<String>,
    recurrent: RwSignal<bool>,
    period: RwSignal<String>,
}

impl EventFields {
    fn preview(self, state: Option<AuthoringState>, create: bool) -> Edit {
        edit(state, move |_, source| {
            let event = CalendarEventDefinition {
                id: identity(
                    source,
                    "calendar-event",
                    &self.selected.get_untracked(),
                    create,
                )?,
                name: self.name.get_untracked(),
                first_minute: integer(&self.first.get_untracked(), "Première échéance")?,
                repeat_every_minutes: if self.recurrent.get_untracked() {
                    Some(integer(&self.period.get_untracked(), "Période")?)
                } else {
                    None
                },
            };
            if !create
                && source
                    .simulation
                    .as_ref()
                    .and_then(|simulation| simulation.event(event.id))
                    .is_none()
            {
                return Err("Choisir un événement courant.".into());
            }
            *source = source
                .with_calendar_event_definition(event)
                .map_err(|error| error.to_string())?;
            Ok(())
        })
    }

    fn remove(self, state: Option<AuthoringState>) -> Edit {
        edit(state, move |_, source| {
            let event = id(&self.selected.get_untracked())?;
            let mut simulation = source
                .simulation
                .clone()
                .ok_or("Aucun calendrier à retoucher.")?;
            if simulation.event(event).is_none() {
                return Err("Choisir un événement courant.".into());
            }
            if simulation
                .encounters
                .iter()
                .any(|encounter| encounter.event == event)
            {
                return Err("Retoucher d’abord les rencontres qui utilisent cet événement.".into());
            }
            simulation.events.retain(|entry| entry.id != event);
            *source = source
                .with_simulation_definition(Some(simulation))
                .map_err(|error| error.to_string())?;
            Ok(())
        })
    }
}

#[component]
pub(super) fn CalendarControls(
    state: RwSignal<Option<AuthoringState>>,
    send: Callback<Edit>,
) -> impl IntoView {
    view! {
        <p>"Cette horloge avance avec la simulation. Une pause ne consomme pas de temps réel. Les échéances et rencontres utilisent ce même calendrier."</p>
        <ClockControls state=state send=send/>
        <EventControls state=state send=send/>
        <p>"Examiner le diff, appliquer puis sauvegarder. Modifier le rythme ou les échéances peut rendre une ancienne partie incompatible."</p>
    }
}

#[component]
fn ClockControls(state: RwSignal<Option<AuthoringState>>, send: Callback<Edit>) -> impl IntoView {
    let fields = ClockFields {
        day: RwSignal::new("1".into()),
        minute: RwSignal::new("480".into()),
        duration: RwSignal::new("1000".into()),
    };
    Effect::new(move |previous: Option<Option<forge_core::ContentHash>>| {
        let key = source_hash(state);
        if previous.as_ref() != Some(&key) {
            let clock = document(state)
                .and_then(|source| source.simulation)
                .unwrap_or_default()
                .clock;
            fields.day.set(clock.start_day.to_string());
            fields.minute.set(clock.start_minute.to_string());
            fields
                .duration
                .set(clock.milliseconds_per_minute.to_string());
        }
        key
    });
    view! {
        <label>"Jour de départ"<input type="number" min="0" max=u32::MAX.to_string() step="1" prop:value=move ||fields.day.get() on:input=move |event|fields.day.set(event_target_value(&event))/></label>
        <label>"Minute du jour au départ (0 à 1 439)"<input type="number" min="0" max="1439" step="1" prop:value=move ||fields.minute.get() on:input=move |event|fields.minute.set(event_target_value(&event))/></label>
        <p>"0 correspond à minuit ; 480 à 08 h 00. Le jour avance après 1 440 minutes."</p>
        <label>"Durée de simulation d’une minute du calendrier (ms)"<input type="number" min="1" max="3600000" step="1" prop:value=move ||fields.duration.get() on:input=move |event|fields.duration.set(event_target_value(&event))/></label>
        <button on:click=move |_|send.run(fields.preview(state.get_untracked()))>"Examiner l’horloge"</button>
    }
}

#[component]
fn EventControls(state: RwSignal<Option<AuthoringState>>, send: Callback<Edit>) -> impl IntoView {
    let fields = EventFields {
        selected: RwSignal::new(String::new()),
        name: RwSignal::new("Nouvel événement".into()),
        first: RwSignal::new("1".into()),
        recurrent: RwSignal::new(false),
        period: RwSignal::new("1".into()),
    };
    Effect::new(move |previous: Option<super::super::FieldSource>| {
        let key = (fields.selected.get(), source_hash(state));
        if previous.as_ref() != Some(&key) {
            let current = document(state)
                .and_then(|source| source.simulation)
                .and_then(|simulation| {
                    id(&key.0)
                        .ok()
                        .and_then(|event| simulation.event(event).cloned())
                });
            if let Some(current) = current {
                fields.name.set(current.name);
                fields.first.set(current.first_minute.to_string());
                fields.recurrent.set(current.repeat_every_minutes.is_some());
                fields
                    .period
                    .set(current.repeat_every_minutes.unwrap_or(1).to_string());
            } else {
                fields.name.set("Nouvel événement".into());
                fields.first.set("1".into());
                fields.recurrent.set(false);
                fields.period.set("1".into());
            }
        }
        key
    });
    view! {
        <label>"Événement à retoucher"<select prop:value=move ||fields.selected.get() on:change=move |event|fields.selected.set(event_target_value(&event))>
            <option value="">"Préparer un nouvel événement"</option>
            {move ||document(state).and_then(|source|source.simulation).map(|simulation|simulation.events.into_iter().map(|event|view!{<option value=event.id.to_string()>{event.name}</option>}).collect_view())}
        </select></label>
        <label>"Nom de l’événement"<input maxlength="256" prop:value=move ||fields.name.get() on:input=move |event|fields.name.set(event_target_value(&event))/></label>
        <label>"Première échéance depuis le départ (minutes du calendrier)"<input type="number" min="0" max="1000000000" step="1" prop:value=move ||fields.first.get() on:input=move |event|fields.first.set(event_target_value(&event))/></label>
        <label><input type="checkbox" prop:checked=move ||fields.recurrent.get() on:change=move |event|fields.recurrent.set(event_target_checked(&event)) />"Répéter cet événement"</label>
        <Show when=move ||fields.recurrent.get()><label>"Période (minutes du calendrier)"<input type="number" min="1" max="1000000000" step="1" prop:value=move ||fields.period.get() on:input=move |event|fields.period.set(event_target_value(&event))/></label></Show>
        <button on:click=move |_|send.run(fields.preview(state.get_untracked(),true))>"Examiner un nouvel événement"</button>
        <button on:click=move |_|send.run(fields.preview(state.get_untracked(),false))>"Examiner la retouche de l’événement"</button>
        <button on:click=move |_|send.run(fields.remove(state.get_untracked()))>"Examiner la suppression de l’événement inutilisé"</button>
    }
}
