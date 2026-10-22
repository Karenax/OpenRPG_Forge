//! One value widget follows the selected variable's authored type.
use super::id;
use forge_gameplay::{
    Condition, DialogueEffect, GameplayDocument, NarrativeComparison, NarrativeValue,
    NarrativeVariable,
};
use forge_project::authoring::AuthoringState;
use leptos::prelude::*;

#[derive(Clone, Copy, PartialEq, Eq)]
enum Kind {
    Boolean,
    Integer,
    Text,
}

fn kind(value: &NarrativeValue) -> Kind {
    match value {
        NarrativeValue::Boolean(_) => Kind::Boolean,
        NarrativeValue::Integer(_) => Kind::Integer,
        NarrativeValue::Text(_) => Kind::Text,
    }
}

fn value_text(value: NarrativeValue) -> String {
    match value {
        NarrativeValue::Boolean(value) => value.to_string(),
        NarrativeValue::Integer(value) => value.to_string(),
        NarrativeValue::Text(value) => value,
    }
}

pub(super) fn parse_value(initial: &NarrativeValue, value: &str) -> Result<NarrativeValue, String> {
    match initial {
        NarrativeValue::Boolean(_) => match value.trim() {
            "true" => Ok(NarrativeValue::Boolean(true)),
            "false" => Ok(NarrativeValue::Boolean(false)),
            _ => Err("Choisir vrai ou faux.".into()),
        },
        NarrativeValue::Integer(_) => value
            .trim()
            .parse::<i32>()
            .map(NarrativeValue::Integer)
            .map_err(|_| "Saisir un entier dans l’intervalle affiché.".into()),
        NarrativeValue::Text(_) => Ok(NarrativeValue::Text(value.into())),
    }
}

pub(super) fn comparison_key(comparison: NarrativeComparison) -> &'static str {
    match comparison {
        NarrativeComparison::Equal => "equal",
        NarrativeComparison::NotEqual => "not_equal",
        NarrativeComparison::AtLeast => "at_least",
        NarrativeComparison::AtMost => "at_most",
    }
}

pub(super) fn parse_comparison(value: &str) -> Result<NarrativeComparison, String> {
    match value {
        "equal" => Ok(NarrativeComparison::Equal),
        "not_equal" => Ok(NarrativeComparison::NotEqual),
        "at_least" => Ok(NarrativeComparison::AtLeast),
        "at_most" => Ok(NarrativeComparison::AtMost),
        _ => Err("Choisir une comparaison courante.".into()),
    }
}

fn value_label(value: &NarrativeValue) -> String {
    match value {
        NarrativeValue::Boolean(value) => if *value { "Vrai" } else { "Faux" }.into(),
        NarrativeValue::Integer(value) => value.to_string(),
        NarrativeValue::Text(value) => format!("« {value} »"),
    }
}

pub(super) fn condition_label(document: &GameplayDocument, condition: &Condition) -> String {
    match condition {
        Condition::Relation {
            observer,
            subject,
            comparison,
            threshold,
        } => {
            let actor_name = |id| {
                document
                    .actor(id)
                    .map_or("Personnage absent", |actor| actor.name.as_str())
            };
            let comparison = match comparison {
                NarrativeComparison::Equal => "égale à",
                NarrativeComparison::NotEqual => "différente de",
                NarrativeComparison::AtLeast => "au moins",
                NarrativeComparison::AtMost => "au plus",
            };
            format!(
                "Opinion de {} envers {} : {comparison} {threshold} points",
                actor_name(*observer),
                actor_name(*subject)
            )
        }
        Condition::Always => "Sans condition".into(),
        Condition::Variable {
            variable,
            comparison,
            value,
        } => {
            let name = document
                .narrative
                .as_ref()
                .and_then(|narrative| narrative.variable(*variable))
                .map_or("Variable absente", |variable| variable.name.as_str());
            let comparison = match comparison {
                NarrativeComparison::Equal => "égale à",
                NarrativeComparison::NotEqual => "différente de",
                NarrativeComparison::AtLeast => "au moins",
                NarrativeComparison::AtMost => "au plus",
            };
            format!("{name} {comparison} {}", value_label(value))
        }
        Condition::HasItem { item, quantity } => format!(
            "Posséder {} × {quantity}",
            document
                .item(*item)
                .map_or("Item absent", |item| item.name.as_str())
        ),
        Condition::Quest { quest, status } => format!(
            "{} : {}",
            document
                .quest(*quest)
                .map_or("Quête absente", |quest| quest.name.as_str()),
            status.label()
        ),
        Condition::ActorDefeated { actor } => format!(
            "{} est vaincu",
            document
                .actor(*actor)
                .map_or("Personnage absent", |actor| actor.name.as_str())
        ),
        Condition::ObjectDestroyed { object } => format!(
            "{} est détruit",
            document
                .object(*object)
                .map_or("Objet absent", |object| object.name.as_str())
        ),
        Condition::All { conditions } => format!(
            "({})",
            conditions
                .iter()
                .map(|condition| condition_label(document, condition))
                .collect::<Vec<_>>()
                .join(" et ")
        ),
        Condition::Any { conditions } => format!(
            "({})",
            conditions
                .iter()
                .map(|condition| condition_label(document, condition))
                .collect::<Vec<_>>()
                .join(" ou ")
        ),
    }
}

pub(super) fn effect_label(document: &GameplayDocument, effect: &DialogueEffect) -> String {
    let quest_name = |id| {
        document
            .quest(id)
            .map_or("Quête absente", |quest| quest.name.as_str())
    };
    match effect {
        DialogueEffect::SetVariable { variable, value } => {
            let name = document
                .narrative
                .as_ref()
                .and_then(|narrative| narrative.variable(*variable))
                .map_or("Variable absente", |variable| variable.name.as_str());
            format!("{name} prend la valeur {}", value_label(value))
        }
        DialogueEffect::AcceptQuest { quest } => format!("Accepter : {}", quest_name(*quest)),
        DialogueEffect::CompleteQuest { quest } => format!("Terminer : {}", quest_name(*quest)),
        DialogueEffect::FailQuest { quest } => format!("Échouer : {}", quest_name(*quest)),
        DialogueEffect::CompleteQuestSolution { quest, solution } => {
            let solution = document
                .quest_flow(*quest)
                .and_then(|flow| flow.solutions.iter().find(|entry| entry.id == *solution))
                .map_or("Solution absente", |solution| solution.name.as_str());
            format!("Résoudre {} par {solution}", quest_name(*quest))
        }
        DialogueEffect::SetDoor { object, open } => {
            let name = document
                .object(*object)
                .map_or("Porte absente", |object| object.name.as_str());
            format!("{} : {name}", if *open { "Ouvrir" } else { "Fermer" })
        }
    }
}

#[component]
pub(super) fn ComparisonField(value: RwSignal<String>, label: &'static str) -> impl IntoView {
    view! {<label>{label}<select prop:value=move ||value.get() on:change=move |event|value.set(event_target_value(&event))>
        <option value="equal">"Égale à"</option><option value="not_equal">"Différente de"</option>
        <option value="at_least">"Au moins (entier)"</option><option value="at_most">"Au plus (entier)"</option>
    </select></label>}
}

#[component]
pub(super) fn VariableValueField(
    state: RwSignal<Option<AuthoringState>>,
    selected: RwSignal<String>,
    value: RwSignal<String>,
    label: &'static str,
) -> impl IntoView {
    let variable = move || -> Option<NarrativeVariable> {
        let selected = id(&selected.get()).ok()?;
        state
            .get()?
            .zone_head?
            .corrections
            .gameplay?
            .narrative?
            .variable(selected)
            .cloned()
    };
    let value_kind = Memo::new(move |_| variable().map(|variable| kind(&variable.initial)));
    Effect::new(move |previous: Option<(String, Option<Kind>)>| {
        let current = variable();
        let key = (
            selected.get(),
            current.as_ref().map(|variable| kind(&variable.initial)),
        );
        if previous.as_ref() != Some(&key)
            && let Some(variable) = current
            && parse_value(&variable.initial, &value.get_untracked()).is_err()
        {
            value.set(value_text(variable.initial));
        }
        key
    });
    view! {
        <label>{label}{move ||match value_kind.get() {
            Some(Kind::Boolean)=>view! {
                <select prop:value=move ||value.get() on:change=move |event|value.set(event_target_value(&event))>
                    <option value="false">"Faux"</option><option value="true">"Vrai"</option>
                </select>
            }.into_any(),
            Some(Kind::Integer)=>view! {
                <input type="number" step="1" min=i32::MIN.to_string() max=i32::MAX.to_string()
                    prop:value=move ||value.get() on:input=move |event|value.set(event_target_value(&event))/>
            }.into_any(),
            Some(Kind::Text)=>view! {
                <input type="text" maxlength="256" prop:value=move ||value.get()
                    on:input=move |event|value.set(event_target_value(&event))/>
            }.into_any(),
            None=>view! {<span>"Choisir une variable pour saisir sa valeur."</span>}.into_any(),
        }}</label>
    }
}
