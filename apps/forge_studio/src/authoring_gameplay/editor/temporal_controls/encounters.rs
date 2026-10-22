use super::condition::{ConditionControls, ConditionFields};
use super::{Edit, document, edit, id, identity, integer, source_hash};
use forge_core::EntityId;
use forge_gameplay::{ActorRole, Condition, EncounterDefinition, EncounterRearm};
use forge_project::authoring::AuthoringState;
use leptos::prelude::*;
use std::collections::BTreeSet;

#[derive(Clone, Copy)]
struct Fields {
    selected: RwSignal<String>,
    name: RwSignal<String>,
    event: RwSignal<String>,
    participants: RwSignal<BTreeSet<EntityId>>,
    recurrent: RwSignal<bool>,
    cooldown: RwSignal<String>,
    condition: ConditionFields,
}

impl Fields {
    fn preview(self, state: Option<AuthoringState>, create: bool) -> Edit {
        edit(state, move |_, source| {
            let encounter_id =
                identity(source, "encounter", &self.selected.get_untracked(), create)?;
            let previous = source
                .simulation
                .as_ref()
                .and_then(|simulation| simulation.encounter(encounter_id));
            if !create && previous.is_none() {
                return Err("Choisir une rencontre courante.".into());
            }
            let encounter = EncounterDefinition {
                id: encounter_id,
                name: self.name.get_untracked(),
                event: id(&self.event.get_untracked())?,
                participants: self.participants.get_untracked().into_iter().collect(),
                condition: self
                    .condition
                    .resolve(source, previous.map(|encounter| &encounter.condition))?,
                rearm: if self.recurrent.get_untracked() {
                    EncounterRearm::AfterDefeat {
                        cooldown_minutes: integer(
                            &self.cooldown.get_untracked(),
                            "Délai de réarmement",
                        )?,
                    }
                } else {
                    EncounterRearm::Once
                },
            };
            *source = source
                .with_encounter_definition(encounter)
                .map_err(|error| error.to_string())?;
            Ok(())
        })
    }

    fn remove(self, state: Option<AuthoringState>) -> Edit {
        edit(state, move |_, source| {
            let encounter = id(&self.selected.get_untracked())?;
            let mut simulation = source
                .simulation
                .clone()
                .ok_or("Aucune rencontre à retoucher.")?;
            if simulation.encounter(encounter).is_none() {
                return Err("Choisir une rencontre courante.".into());
            }
            simulation.encounters.retain(|entry| entry.id != encounter);
            *source = source
                .with_simulation_definition(Some(simulation))
                .map_err(|error| error.to_string())?;
            Ok(())
        })
    }

    fn load(self, current: Option<EncounterDefinition>) {
        if let Some(current) = current {
            self.name.set(current.name);
            self.event.set(current.event.to_string());
            self.participants
                .set(current.participants.into_iter().collect());
            self.condition.load(current.condition);
            self.recurrent
                .set(matches!(current.rearm, EncounterRearm::AfterDefeat { .. }));
            self.cooldown.set(match current.rearm {
                EncounterRearm::Once => "1".into(),
                EncounterRearm::AfterDefeat { cooldown_minutes } => cooldown_minutes.to_string(),
            });
        } else {
            self.name.set("Nouvelle rencontre".into());
            self.event.set(String::new());
            self.participants.set(BTreeSet::new());
            self.condition.load(Condition::Always);
            self.recurrent.set(false);
            self.cooldown.set("1".into());
        }
    }
}

#[component]
pub(super) fn EncounterControls(
    state: RwSignal<Option<AuthoringState>>,
    send: Callback<Edit>,
) -> impl IntoView {
    let fields = Fields {
        selected: RwSignal::new(String::new()),
        name: RwSignal::new("Nouvelle rencontre".into()),
        event: RwSignal::new(String::new()),
        participants: RwSignal::new(BTreeSet::new()),
        recurrent: RwSignal::new(false),
        cooldown: RwSignal::new("1".into()),
        condition: ConditionFields::new(),
    };
    Effect::new(move |previous: Option<super::super::FieldSource>| {
        let key = (fields.selected.get(), source_hash(state));
        if previous.as_ref() != Some(&key) {
            fields.load(
                document(state)
                    .and_then(|source| source.simulation)
                    .and_then(|simulation| {
                        id(&key.0)
                            .ok()
                            .and_then(|id| simulation.encounter(id).cloned())
                    }),
            );
        }
        key
    });
    view! {
        <p>"Les participants restent absents avant l’échéance. Une rencontre récurrente attend leur défaite et son délai de réarmement ; elle réutilise leurs identités et ne duplique pas leurs inventaires."</p>
        <label>"Rencontre à retoucher"<select prop:value=move ||fields.selected.get() on:change=move |event|fields.selected.set(event_target_value(&event))>
            <option value="">"Préparer une nouvelle rencontre"</option>
            {move ||document(state).and_then(|source|source.simulation).map(|simulation|simulation.encounters.into_iter().map(|entry|view!{<option value=entry.id.to_string()>{entry.name}</option>}).collect_view())}
        </select></label>
        <label>"Nom de la rencontre"<input maxlength="256" prop:value=move ||fields.name.get() on:input=move |event|fields.name.set(event_target_value(&event))/></label>
        <label>"Événement déclencheur"<select prop:value=move ||fields.event.get() on:change=move |event|fields.event.set(event_target_value(&event))>
            <option value="">"Choisir un événement du calendrier"</option>
            {move ||document(state).and_then(|source|source.simulation).map(|simulation|simulation.events.into_iter().map(|entry|view!{<option value=entry.id.to_string()>{entry.name}</option>}).collect_view())}
        </select></label>
        <p>"Participants : choisir un à seize personnages, chacun affecté à une seule rencontre. Le joueur reste présent et ne peut pas être un participant."</p>
        {move ||document(state).map(|source|source.actors.into_iter().filter(|actor|actor.role!=ActorRole::Player).map(|actor|{
            let actor_id=actor.id;
            view!{<label><input type="checkbox" prop:checked=move ||fields.participants.get().contains(&actor_id) on:change=move |event|fields.participants.update(|participants| {
                if event_target_checked(&event) { participants.insert(actor_id); } else { participants.remove(&actor_id); }
            })/>{actor.name}</label>}
        }).collect_view())}
        <ConditionControls fields=fields.condition state=state/>
        <label><input type="checkbox" prop:checked=move ||fields.recurrent.get() on:change=move |event|fields.recurrent.set(event_target_checked(&event)) />"Réarmer après la défaite de tous les participants"</label>
        <Show when=move ||fields.recurrent.get()><label>"Délai depuis la dernière activation (minutes du calendrier)"<input type="number" min="0" max="1000000000" step="1" prop:value=move ||fields.cooldown.get() on:input=move |event|fields.cooldown.set(event_target_value(&event))/></label></Show>
        <button on:click=move |_|send.run(fields.preview(state.get_untracked(),true))>"Examiner une nouvelle rencontre"</button>
        <button on:click=move |_|send.run(fields.preview(state.get_untracked(),false))>"Examiner la retouche de la rencontre"</button>
        <button on:click=move |_|send.run(fields.remove(state.get_untracked()))>"Examiner la suppression de la rencontre"</button>
        <p>"Supprimer une rencontre rend ses anciens participants présents dès le début d’une nouvelle partie. Examiner ce changement et la compatibilité des sauvegardes avant application."</p>
    }
}
