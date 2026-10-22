use super::super::variable_value_field::{
    ComparisonField, VariableValueField, comparison_key, condition_label, parse_comparison,
};
use super::{document, id};
use forge_gameplay::{Condition, GameplayDocument};
use forge_project::authoring::AuthoringState;
use leptos::prelude::*;

#[derive(Clone, Copy)]
pub(super) struct ConditionFields {
    mode: RwSignal<String>,
    variable: RwSignal<String>,
    comparison: RwSignal<String>,
    value: RwSignal<String>,
    original: RwSignal<Condition>,
}

impl ConditionFields {
    pub(super) fn new() -> Self {
        Self {
            mode: RwSignal::new("always".into()),
            variable: RwSignal::new(String::new()),
            comparison: RwSignal::new("equal".into()),
            value: RwSignal::new("true".into()),
            original: RwSignal::new(Condition::Always),
        }
    }

    pub(super) fn load(self, condition: Condition) {
        self.original.set(condition.clone());
        match condition {
            Condition::Always => {
                self.mode.set("always".into());
                self.variable.set(String::new());
            }
            Condition::Variable {
                variable,
                comparison,
                value,
            } => {
                self.mode.set("variable".into());
                self.variable.set(variable.to_string());
                self.comparison.set(comparison_key(comparison).into());
                self.value.set(match value {
                    forge_gameplay::NarrativeValue::Boolean(value) => value.to_string(),
                    forge_gameplay::NarrativeValue::Integer(value) => value.to_string(),
                    forge_gameplay::NarrativeValue::Text(value) => value,
                });
            }
            _ => self.mode.set("keep".into()),
        }
    }

    pub(super) fn resolve(
        self,
        source: &GameplayDocument,
        existing: Option<&Condition>,
    ) -> Result<Condition, String> {
        match self.mode.get_untracked().as_str() {
            "keep" => Ok(existing.cloned().unwrap_or(Condition::Always)),
            "always" => Ok(Condition::Always),
            "variable" => {
                let variable = id(&self.variable.get_untracked())?;
                Ok(Condition::Variable {
                    variable,
                    comparison: parse_comparison(&self.comparison.get_untracked())?,
                    value: super::super::dialogue_controls::variable_value(
                        source,
                        variable,
                        &self.value.get_untracked(),
                    )?,
                })
            }
            _ => Err("Choisir une condition courante.".into()),
        }
    }
}

#[component]
pub(super) fn ConditionControls(
    fields: ConditionFields,
    state: RwSignal<Option<AuthoringState>>,
) -> impl IntoView {
    view! {
        <p>{move ||document(state).map(|source|format!("Condition courante : {}", condition_label(&source,&fields.original.get())))}</p>
        <label>"Condition d’activation"<select prop:value=move ||fields.mode.get() on:change=move |event|fields.mode.set(event_target_value(&event))>
            <option value="keep">"Conserver la condition courante"</option><option value="always">"Toujours"</option><option value="variable">"Comparer une variable"</option>
        </select></label>
        <Show when=move ||fields.mode.get()=="variable">
            <label>"Variable"<select prop:value=move ||fields.variable.get() on:change=move |event|fields.variable.set(event_target_value(&event))>
                <option value="">"Choisir une variable"</option>
                {move ||document(state).and_then(|source|source.narrative).map(|narrative|narrative.variables.into_iter().map(|variable|view!{<option value=variable.id.to_string()>{variable.name}</option>}).collect_view())}
            </select></label>
            <ComparisonField value=fields.comparison label="Comparaison"/>
            <VariableValueField state=state selected=fields.variable value=fields.value label="Valeur attendue"/>
        </Show>
    }
}
