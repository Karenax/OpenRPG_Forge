//! Typed variables use the normal gameplay preview and its retained history.
use super::{Edit, edit, id};
use forge_core::{EntityId, canonical_json_sha256};
use forge_gameplay::{CreatorSurface, NarrativeDefinition, NarrativeValue, NarrativeVariable};
use forge_project::authoring::AuthoringState;
use leptos::prelude::*;

#[derive(Clone, Copy)]
struct Fields {
    selected: RwSignal<String>,
    name: RwSignal<String>,
    kind: RwSignal<String>,
    value: RwSignal<String>,
}

impl Fields {
    fn initial(self) -> Result<NarrativeValue, String> {
        let value = self.value.get_untracked();
        match self.kind.get_untracked().as_str() {
            "boolean" => match value.trim() {
                "true" => Ok(NarrativeValue::Boolean(true)),
                "false" => Ok(NarrativeValue::Boolean(false)),
                _ => Err("Choisir vrai ou faux pour cette variable.".into()),
            },
            "integer" => value
                .trim()
                .parse::<i32>()
                .map(NarrativeValue::Integer)
                .map_err(|_| "Saisir un entier signé sur 32 bits.".into()),
            "text" => Ok(NarrativeValue::Text(value)),
            _ => Err("Choisir un type de variable.".into()),
        }
    }

    fn preview(self, state: Option<AuthoringState>, create: bool) -> Edit {
        edit(state, move |_, source| {
            let variable = if create {
                EntityId::deterministic(format!(
                    "narrative-variable/{}/{hash}",
                    source.area_id,
                    hash = canonical_json_sha256(source).map_err(|error| error.to_string())?
                ))
            } else {
                id(&self.selected.get_untracked())?
            };
            let mut narrative = source.narrative.clone().unwrap_or(NarrativeDefinition {
                version: 1,
                variables: vec![],
                quests: vec![],
            });
            let candidate = NarrativeVariable {
                id: variable,
                name: self.name.get_untracked(),
                initial: self.initial()?,
            };
            if create {
                narrative.variables.push(candidate);
            } else {
                let current = narrative
                    .variables
                    .iter_mut()
                    .find(|entry| entry.id == variable)
                    .ok_or("Choisir une variable courante".to_owned())?;
                *current = candidate;
            }
            *source = source
                .with_narrative_definition(Some(narrative))
                .map_err(|error| error.to_string())?;
            Ok(())
        })
    }
}

#[component]
pub(super) fn VariableControls(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    send: Callback<Edit>,
) -> impl IntoView {
    let fields = Fields {
        selected: RwSignal::new(String::new()),
        name: RwSignal::new("Nouvelle variable".into()),
        kind: RwSignal::new("boolean".into()),
        value: RwSignal::new("false".into()),
    };
    let document = move || {
        state
            .get()
            .and_then(|state| state.zone_head)
            .and_then(|head| head.corrections.gameplay.map(|document| *document))
    };
    Effect::new(move |previous: Option<super::FieldSource>| {
        let key = (fields.selected.get(), super::source_hash(state));
        if previous.as_ref() == Some(&key) {
            return key;
        }
        if let Some(variable) =
            document()
                .and_then(|document| document.narrative)
                .and_then(|narrative| {
                    id(&fields.selected.get())
                        .ok()
                        .and_then(|id| narrative.variable(id).cloned())
                })
        {
            fields.name.set(variable.name);
            let (kind, value) = match variable.initial {
                NarrativeValue::Boolean(value) => ("boolean", value.to_string()),
                NarrativeValue::Integer(value) => ("integer", value.to_string()),
                NarrativeValue::Text(value) => ("text", value),
            };
            fields.kind.set(kind.into());
            fields.value.set(value);
        }
        key
    });
    let disabled = move || {
        busy.get()
            || state
                .get()
                .is_none_or(|state| state.zone_head.is_none() || state.playtest_hash.is_some())
    };
    view! {
        <Show when=move ||document().is_some_and(|document|document.surface_visible(CreatorSurface::Dialogues))>
            <details class="creator-narrative-variables"><summary>"Variables du dialogue"</summary>
                <fieldset disabled=disabled>
                    <label>"Variable à retoucher"<select prop:value=move ||fields.selected.get() on:change=move |event|fields.selected.set(event_target_value(&event))>
                        <option value="">"Choisir une variable"</option>
                        {move ||document().and_then(|document|document.narrative).map(|narrative|narrative.variables.into_iter().map(|variable|view!{<option value=variable.id.to_string()>{variable.name}</option>}).collect_view())}
                    </select></label>
                    <label>"Nom"<input prop:value=move ||fields.name.get() on:input=move |event|fields.name.set(event_target_value(&event))/></label>
                    <label>"Type"<select prop:value=move ||fields.kind.get() on:change=move |event| {
                        let kind=event_target_value(&event);
                        fields.value.set(if kind=="boolean" {"false"} else if kind=="integer" {"0"} else {""}.into());
                        fields.kind.set(kind);
                    }><option value="boolean">"Booléen"</option><option value="integer">"Entier signé"</option><option value="text">"Texte"</option></select></label>
                    <Show when=move ||fields.kind.get()=="boolean" fallback=move ||view! {
                        <label>"Valeur initiale"<input maxlength="256" prop:value=move ||fields.value.get() on:input=move |event|fields.value.set(event_target_value(&event))/></label>
                    }>
                        <label>"Valeur initiale"<select prop:value=move ||fields.value.get() on:change=move |event|fields.value.set(event_target_value(&event))><option value="false">"Faux"</option><option value="true">"Vrai"</option></select></label>
                    </Show>
                    <p>"Le type reste fixe pendant une partie. Modifier le type ou la valeur initiale exige une nouvelle partie. Les textes sont limités à 256 octets."</p>
                    <button on:click=move |_|send.run(fields.preview(state.get_untracked(),true))>"Examiner la nouvelle variable"</button>
                    <button on:click=move |_|send.run(fields.preview(state.get_untracked(),false))>"Examiner la retouche de la variable"</button>
                </fieldset>
            </details>
        </Show>
    }
}
