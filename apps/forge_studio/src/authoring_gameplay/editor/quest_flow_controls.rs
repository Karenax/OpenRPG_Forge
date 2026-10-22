//! Prepare solutions locally, then preview the entire quest flow atomically.
use super::{Edit, edit, id};
use forge_core::{EntityId, canonical_json_sha256};
use forge_gameplay::{
    Condition, CreatorSurface, GameplayDocument, QuestConsumption, QuestFlow, QuestSolution,
};
use forge_project::authoring::AuthoringState;
use leptos::prelude::*;
use std::collections::BTreeSet;

#[derive(Clone, Copy)]
struct Fields {
    quest: RwSignal<String>,
    solutions: RwSignal<Vec<QuestSolution>>,
    prerequisites: RwSignal<BTreeSet<EntityId>>,
    available: RwSignal<String>,
    active: RwSignal<String>,
    completed: RwSignal<String>,
    failed: RwSignal<String>,
    failure_variable: RwSignal<String>,
    failure_value: RwSignal<String>,
    failure_comparison: RwSignal<String>,
    name: RwSignal<String>,
    variable: RwSignal<String>,
    value: RwSignal<String>,
    comparison: RwSignal<String>,
    item: RwSignal<String>,
    quantity: RwSignal<String>,
    legacy_solution: RwSignal<String>,
}

impl Fields {
    fn prepare_solution(self, document: &GameplayDocument) -> Result<QuestSolution, String> {
        let variable = self.variable.get_untracked();
        let condition = if variable.is_empty() {
            Condition::Always
        } else {
            let variable = id(&variable)?;
            Condition::Variable {
                variable,
                comparison: super::variable_value_field::parse_comparison(
                    &self.comparison.get_untracked(),
                )?,
                value: super::dialogue_controls::variable_value(
                    document,
                    variable,
                    &self.value.get_untracked(),
                )?,
            }
        };
        let item = self.item.get_untracked();
        let consume = if item.is_empty() {
            vec![]
        } else {
            vec![QuestConsumption {
                item: id(&item)?,
                quantity: self
                    .quantity
                    .get_untracked()
                    .trim()
                    .parse::<u32>()
                    .map_err(|_| "Saisir une quantité entière positive".to_owned())?,
            }]
        };
        let name = self.name.get_untracked();
        let hash = canonical_json_sha256(&(
            document.area_id,
            self.quest.get_untracked(),
            self.solutions.get_untracked(),
            &name,
            &condition,
            &consume,
        ))
        .map_err(|error| error.to_string())?;
        Ok(QuestSolution {
            id: EntityId::deterministic(format!("quest-solution/{hash}")),
            name,
            condition,
            consume,
        })
    }

    fn preview(self, state: Option<AuthoringState>) -> Edit {
        edit(state, move |_, document| {
            let quest = id(&self.quest.get_untracked())?;
            let variable = self.failure_variable.get_untracked();
            let failure_condition = if variable.is_empty() {
                document
                    .quest_flow(quest)
                    .and_then(|flow| flow.failure_condition.as_ref())
                    .filter(|condition| !matches!(condition, Condition::Variable { .. }))
                    .cloned()
            } else {
                let variable = id(&variable)?;
                Some(Condition::Variable {
                    variable,
                    comparison: super::variable_value_field::parse_comparison(
                        &self.failure_comparison.get_untracked(),
                    )?,
                    value: super::dialogue_controls::variable_value(
                        document,
                        variable,
                        &self.failure_value.get_untracked(),
                    )?,
                })
            };
            let selected = self.prerequisites.get_untracked();
            let mut prerequisites = document.quest_flow(quest).map_or_else(Vec::new, |flow| {
                flow.prerequisites
                    .iter()
                    .copied()
                    .filter(|id| selected.contains(id))
                    .collect()
            });
            for dependency in selected {
                if !prerequisites.contains(&dependency) {
                    prerequisites.push(dependency);
                }
            }
            let flow = QuestFlow {
                quest,
                prerequisites,
                solutions: self.solutions.get_untracked(),
                failure_condition,
                journal_available: self.available.get_untracked(),
                journal_active: self.active.get_untracked(),
                journal_completed: self.completed.get_untracked(),
                journal_failed: self.failed.get_untracked(),
            };
            *document = document
                .with_quest_flow_and_legacy_solution(
                    flow,
                    id(&self.legacy_solution.get_untracked())?,
                )
                .map_err(|error| error.to_string())?;
            Ok(())
        })
    }
}

fn initial_flow(document: &GameplayDocument, quest: EntityId) -> Option<QuestFlow> {
    if let Some(flow) = document.quest_flow(quest) {
        return Some(flow.clone());
    }
    let definition = document.quest(quest)?;
    Some(QuestFlow {
        quest,
        prerequisites: vec![],
        solutions: vec![QuestSolution {
            id: EntityId::deterministic(format!("legacy-quest-solution/{quest}")),
            name: "Restituer l’objet demandé".into(),
            condition: Condition::HasItem {
                item: definition.required_item,
                quantity: definition.required_quantity,
            },
            consume: vec![QuestConsumption {
                item: definition.required_item,
                quantity: definition.required_quantity,
            }],
        }],
        failure_condition: None,
        journal_available: format!("Découvrir : {}", definition.name),
        journal_active: format!("Résoudre : {}", definition.name),
        journal_completed: format!("Terminée : {}", definition.name),
        journal_failed: format!("Échec : {}", definition.name),
    })
}

#[component]
#[allow(
    clippy::too_many_lines,
    reason = "The prepared flow shares one preview transaction"
)]
pub(super) fn QuestFlowControls(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    send: Callback<Edit>,
) -> impl IntoView {
    let fields = Fields {
        quest: RwSignal::new(String::new()),
        solutions: RwSignal::new(vec![]),
        prerequisites: RwSignal::new(BTreeSet::new()),
        available: RwSignal::new(String::new()),
        active: RwSignal::new(String::new()),
        completed: RwSignal::new(String::new()),
        failed: RwSignal::new(String::new()),
        failure_variable: RwSignal::new(String::new()),
        failure_value: RwSignal::new("false".into()),
        failure_comparison: RwSignal::new("equal".into()),
        name: RwSignal::new("Nouvelle solution".into()),
        variable: RwSignal::new(String::new()),
        value: RwSignal::new("true".into()),
        comparison: RwSignal::new("equal".into()),
        item: RwSignal::new(String::new()),
        quantity: RwSignal::new("1".into()),
        legacy_solution: RwSignal::new(String::new()),
    };
    let document = move || {
        state
            .get()
            .and_then(|state| state.zone_head)
            .and_then(|head| head.corrections.gameplay.map(|document| *document))
    };
    Effect::new(move |previous: Option<super::FieldSource>| {
        let key = (fields.quest.get(), super::source_hash(state));
        if previous.as_ref() == Some(&key) {
            return key;
        }
        if let Some(flow) = document().and_then(|document| {
            id(&fields.quest.get())
                .ok()
                .and_then(|quest| initial_flow(&document, quest))
        }) {
            fields.legacy_solution.set(
                flow.solutions
                    .first()
                    .map_or_else(String::new, |solution| solution.id.to_string()),
            );
            fields.solutions.set(flow.solutions);
            fields
                .prerequisites
                .set(flow.prerequisites.into_iter().collect());
            fields.available.set(flow.journal_available);
            fields.active.set(flow.journal_active);
            fields.completed.set(flow.journal_completed);
            fields.failed.set(flow.journal_failed);
            if let Some(Condition::Variable {
                variable,
                value,
                comparison,
            }) = flow.failure_condition
            {
                fields.failure_variable.set(variable.to_string());
                fields
                    .failure_comparison
                    .set(super::variable_value_field::comparison_key(comparison).into());
                fields.failure_value.set(match value {
                    forge_gameplay::NarrativeValue::Boolean(value) => value.to_string(),
                    forge_gameplay::NarrativeValue::Integer(value) => value.to_string(),
                    forge_gameplay::NarrativeValue::Text(value) => value,
                });
            } else {
                fields.failure_variable.set(String::new());
                fields.failure_comparison.set("equal".into());
            }
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
        <Show when=move ||document().is_some_and(|document|document.surface_visible(CreatorSurface::Quests))>
            <details class="creator-quest-flow"><summary>"Solutions et journal de quête"</summary><fieldset disabled=disabled>
                <label>"Quête"<select prop:value=move ||fields.quest.get() on:change=move |event|fields.quest.set(event_target_value(&event))><option value="">"Choisir une quête"</option>{move ||document().map(|document|document.quests.into_iter().map(|quest|view!{<option value=quest.id.to_string()>{quest.name}</option>}).collect_view())}</select></label>
                <p>"Les solutions partagent la récompense définie sur la quête. Une seule solution peut être validée pendant une partie."</p>
                <ul>{move ||fields.solutions.get().into_iter().map(|solution| {
                    let key=solution.id;
                    let description=document().map_or_else(String::new,|document| {
                        let condition=super::variable_value_field::condition_label(&document,&solution.condition);
                        let costs=solution.consume.iter().map(|cost|format!("{} × {}",document.item(cost.item).map_or("Item absent",|item|item.name.as_str()),cost.quantity)).collect::<Vec<_>>();
                        format!("{condition} · consommation : {}",if costs.is_empty() {"aucune".into()} else {costs.join(", ")})
                    });
                    view!{<li><label>"Nom de la solution"<input value=solution.name on:change=move |event| {
                        let name=event_target_value(&event);
                        fields.solutions.update(|solutions| {if let Some(solution)=solutions.iter_mut().find(|solution|solution.id==key) {solution.name=name;} });
                    }/></label>{description}<button on:click=move |_|fields.solutions.update(|solutions|solutions.retain(|entry|entry.id!=key))>"Retirer de la préparation"</button></li>}
                }).collect_view()}</ul>
                <label>"Nom de la solution"<input prop:value=move ||fields.name.get() on:input=move |event|fields.name.set(event_target_value(&event))/></label>
                <label>"Variable requise"<select prop:value=move ||fields.variable.get() on:change=move |event|fields.variable.set(event_target_value(&event))><option value="">"Aucune condition de variable"</option>{move ||document().and_then(|document|document.narrative).map(|narrative|narrative.variables.into_iter().map(|variable|view!{<option value=variable.id.to_string()>{variable.name}</option>}).collect_view())}</select></label>
                <Show when=move ||!fields.variable.get().is_empty()>
                    <super::variable_value_field::ComparisonField value=fields.comparison label="Comparaison de la solution"/>
                    <super::variable_value_field::VariableValueField state=state selected=fields.variable value=fields.value label="Valeur attendue"/>
                </Show>
                <label>"Item consommé"<select prop:value=move ||fields.item.get() on:change=move |event|fields.item.set(event_target_value(&event))><option value="">"Aucun item consommé"</option>{move ||document().map(|document|document.items.into_iter().map(|item|view!{<option value=item.id.to_string()>{item.name}</option>}).collect_view())}</select></label>
                <label>"Quantité consommée"<input type="number" min="1" prop:value=move ||fields.quantity.get() on:input=move |event|fields.quantity.set(event_target_value(&event))/></label>
                <button on:click=move |_| {
                    let result=document().ok_or("Ouvrir un document RPG".to_owned()).and_then(|document|fields.prepare_solution(&document));
                    match result {Ok(solution)=>fields.solutions.update(|solutions|solutions.push(solution)),Err(error)=>send.run(Err(error))}
                }>"Préparer cette solution"</button>
                <label>"Solution des anciens choix de restitution"<select prop:value=move ||fields.legacy_solution.get() on:change=move |event|fields.legacy_solution.set(event_target_value(&event))><option value="">"Choisir une solution"</option>{move ||fields.solutions.get().into_iter().map(|solution|view!{<option value=solution.id.to_string()>{solution.name}</option>}).collect_view()}</select></label>
                <p>"Les anciens effets de restitution de cette quête seront reliés à la solution choisie dans le même aperçu. Leurs identités, textes, conditions et destinations sont conservés."</p>
                <details><summary>"Quêtes préalables"</summary>{move ||document().map(|document|document.quests.into_iter().filter(|quest|Some(quest.id)!=id(&fields.quest.get()).ok()).map(|quest| {
                    let key=quest.id;
                    view!{<label><input type="checkbox" prop:checked=move ||fields.prerequisites.get().contains(&key) on:change=move |event|fields.prerequisites.update(|prerequisites| {if event_target_checked(&event) {prerequisites.insert(key);} else {prerequisites.remove(&key);}})/>{quest.name}</label>}
                }).collect_view())}</details>
                <label>"Journal — disponible"<textarea prop:value=move ||fields.available.get() on:input=move |event|fields.available.set(event_target_value(&event))/></label>
                <label>"Journal — active"<textarea prop:value=move ||fields.active.get() on:input=move |event|fields.active.set(event_target_value(&event))/></label>
                <label>"Journal — terminée"<textarea prop:value=move ||fields.completed.get() on:input=move |event|fields.completed.set(event_target_value(&event))/></label>
                <label>"Journal — échec"<textarea prop:value=move ||fields.failed.get() on:input=move |event|fields.failed.set(event_target_value(&event))/></label>
                <label>"Variable permettant l’échec"<select prop:value=move ||fields.failure_variable.get() on:change=move |event|fields.failure_variable.set(event_target_value(&event))><option value="">"Sans transition d’échec"</option>{move ||document().and_then(|document|document.narrative).map(|narrative|narrative.variables.into_iter().map(|variable|view!{<option value=variable.id.to_string()>{variable.name}</option>}).collect_view())}</select></label>
                <Show when=move ||document().and_then(|document|id(&fields.quest.get()).ok().and_then(|quest|document.quest_flow(quest).cloned())).is_some_and(|flow|flow.failure_condition.is_some_and(|condition|!matches!(condition,Condition::Variable { .. })))>
                    <p>"Cette quête possède une condition d’échec avancée. Elle est conservée ; choisir une variable ci-dessus permet de la remplacer explicitement."</p>
                </Show>
                <Show when=move ||!fields.failure_variable.get().is_empty()>
                    <super::variable_value_field::ComparisonField value=fields.failure_comparison label="Comparaison permettant l’échec"/>
                    <super::variable_value_field::VariableValueField state=state selected=fields.failure_variable value=fields.failure_value label="Valeur permettant l’échec"/>
                </Show>
                <p>"L’échec s’applique uniquement par un choix de dialogue portant cet effet, si sa condition est satisfaite. Il est terminal et n’attribue aucune récompense."</p>
                <button on:click=move |_|send.run(fields.preview(state.get_untracked()))>"Examiner le flux et le journal"</button>
            </fieldset></details>
        </Show>
    }
}
