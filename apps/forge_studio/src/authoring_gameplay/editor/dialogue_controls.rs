//! Nodes and choices retain identities; every change goes through shared preview.
use super::{Edit, edit, id};
use forge_core::{EntityId, canonical_json_sha256};
use forge_gameplay::{
    Condition, CreatorSurface, DialogueChoice, DialogueEffect, DialogueNode, GameplayDocument,
    NarrativeValue, QuestStatus,
};
use forge_project::authoring::AuthoringState;
use leptos::prelude::*;

#[derive(Clone, Copy)]
struct Fields {
    dialogue: RwSignal<String>,
    node: RwSignal<String>,
    choice: RwSignal<String>,
    reply: RwSignal<String>,
    text: RwSignal<String>,
    next: RwSignal<String>,
    condition: RwSignal<String>,
    reference: RwSignal<String>,
    subject: RwSignal<String>,
    comparison: RwSignal<String>,
    value: RwSignal<String>,
    effect: RwSignal<String>,
    effect_mode: RwSignal<String>,
    effect_reference: RwSignal<String>,
    effect_value: RwSignal<String>,
    solution: RwSignal<String>,
}

#[derive(Clone, Copy)]
enum GraphCommand {
    RetouchNode,
    SetStart,
    RemoveNode,
    RemoveChoice,
}

impl Fields {
    fn condition(self, document: &GameplayDocument) -> Result<Condition, String> {
        let reference = self.reference.get_untracked();
        Ok(match self.condition.get_untracked().as_str() {
            "always" => Condition::Always,
            "variable" => {
                let variable = id(&reference)?;
                Condition::Variable {
                    variable,
                    comparison: super::variable_value_field::parse_comparison(
                        &self.comparison.get_untracked(),
                    )?,
                    value: variable_value(document, variable, &self.value.get_untracked())?,
                }
            }
            "quest_active" => Condition::Quest {
                quest: id(&reference)?,
                status: QuestStatus::Active,
            },
            "quest_completed" => Condition::Quest {
                quest: id(&reference)?,
                status: QuestStatus::Completed,
            },
            "quest_available" => Condition::Quest {
                quest: id(&reference)?,
                status: QuestStatus::Available,
            },
            "quest_failed" => Condition::Quest {
                quest: id(&reference)?,
                status: QuestStatus::Failed,
            },
            "relation" => {
                Condition::Relation {
                    observer: id(&reference)?,
                    subject: if self.subject.get_untracked().is_empty() {
                        document.player
                    } else {
                        id(&self.subject.get_untracked())?
                    },
                    comparison: super::variable_value_field::parse_comparison(
                        &self.comparison.get_untracked(),
                    )?,
                    threshold: self.value.get_untracked().trim().parse().map_err(|_| {
                        "Saisir un seuil entier entre −100 et 100 points.".to_owned()
                    })?,
                }
            }
            "actor_defeated" => Condition::ActorDefeated {
                actor: id(&reference)?,
            },
            "object_destroyed" => Condition::ObjectDestroyed {
                object: id(&reference)?,
            },
            "has_item" => Condition::HasItem {
                item: id(&reference)?,
                quantity: self
                    .value
                    .get_untracked()
                    .trim()
                    .parse::<u32>()
                    .map_err(|_| "Saisir une quantité entière positive".to_owned())?,
            },
            _ => return Err("Choisir une condition".into()),
        })
    }

    fn effect(self, document: &GameplayDocument) -> Result<Vec<DialogueEffect>, String> {
        let reference = self.effect_reference.get_untracked();
        Ok(match self.effect.get_untracked().as_str() {
            "none" => vec![],
            "variable" => {
                let variable = id(&reference)?;
                vec![DialogueEffect::SetVariable {
                    variable,
                    value: variable_value(document, variable, &self.effect_value.get_untracked())?,
                }]
            }
            "accept" => vec![DialogueEffect::AcceptQuest {
                quest: id(&reference)?,
            }],
            "complete" => vec![DialogueEffect::CompleteQuest {
                quest: id(&reference)?,
            }],
            "solution" => vec![DialogueEffect::CompleteQuestSolution {
                quest: id(&reference)?,
                solution: id(&self.solution.get_untracked())?,
            }],
            "fail" => vec![DialogueEffect::FailQuest {
                quest: id(&reference)?,
            }],
            "open_door" | "close_door" => vec![DialogueEffect::SetDoor {
                object: id(&reference)?,
                open: self.effect.get_untracked() == "open_door",
            }],
            _ => return Err("Choisir un effet".into()),
        })
    }

    fn preview(self, state: Option<AuthoringState>, add_node: bool) -> Edit {
        edit(state, move |_, document| {
            let dialogue_id = id(&self.dialogue.get_untracked())?;
            let mut dialogue = document
                .dialogue(dialogue_id)
                .cloned()
                .ok_or("Choisir un dialogue courant".to_owned())?;
            let hash = canonical_json_sha256(document).map_err(|error| error.to_string())?;
            if add_node {
                dialogue.nodes.push(DialogueNode {
                    id: EntityId::deterministic(format!("dialogue-node/{dialogue_id}/{hash}")),
                    text: self.reply.get_untracked(),
                    choices: vec![],
                });
            } else {
                let node_id = id(&self.node.get_untracked())?;
                let node = dialogue
                    .nodes
                    .iter_mut()
                    .find(|node| node.id == node_id)
                    .ok_or("Choisir un nœud courant".to_owned())?;
                node.text = self.reply.get_untracked();
                let choice_id = self.choice.get_untracked();
                let choice_id = if choice_id.is_empty() {
                    EntityId::deterministic(format!("dialogue-choice/{node_id}/{hash}"))
                } else {
                    id(&choice_id)?
                };
                let previous = node.choices.iter().find(|entry| entry.id == choice_id);
                let condition = if self.condition.get_untracked() == "keep" {
                    previous.map_or(Condition::Always, |choice| choice.condition.clone())
                } else {
                    self.condition(document)?
                };
                let effects = if self.effect.get_untracked() == "keep" {
                    previous.map_or_else(Vec::new, |choice| choice.effects.clone())
                } else if self.effect_mode.get_untracked() == "append" {
                    let mut effects =
                        previous.map_or_else(Vec::new, |choice| choice.effects.clone());
                    effects.extend(self.effect(document)?);
                    effects
                } else {
                    self.effect(document)?
                };
                let next = self.next.get_untracked();
                let candidate = DialogueChoice {
                    id: choice_id,
                    text: self.text.get_untracked(),
                    condition,
                    effects,
                    next: if next.is_empty() {
                        None
                    } else {
                        Some(id(&next)?)
                    },
                };
                if let Some(current) = node.choices.iter_mut().find(|entry| entry.id == choice_id) {
                    *current = candidate;
                } else {
                    node.choices.push(candidate);
                }
            }
            *document = document
                .with_dialogue_definition(dialogue)
                .map_err(|error| error.to_string())?;
            Ok(())
        })
    }
    fn command(self, state: Option<AuthoringState>, command: GraphCommand) -> Edit {
        edit(state, move |_, source| {
            let dialogue_id = id(&self.dialogue.get_untracked())?;
            let node_id = id(&self.node.get_untracked())?;
            let mut dialogue = source
                .dialogue(dialogue_id)
                .cloned()
                .ok_or("Choisir un dialogue courant".to_owned())?;
            if !dialogue.nodes.iter().any(|node| node.id == node_id) {
                return Err("Choisir un nœud courant".into());
            }
            match command {
                GraphCommand::SetStart => dialogue.start = node_id,
                GraphCommand::RemoveNode => {
                    if dialogue.start == node_id {
                        return Err(
                            "Choisir un autre nœud initial avant de retirer celui-ci.".into()
                        );
                    }
                    if dialogue
                        .nodes
                        .iter()
                        .filter(|node| node.id != node_id)
                        .flat_map(|node| &node.choices)
                        .any(|choice| choice.next == Some(node_id))
                    {
                        return Err(
                            "Retoucher les choix qui mènent à ce nœud avant de le retirer.".into(),
                        );
                    }
                    dialogue.nodes.retain(|node| node.id != node_id);
                }
                GraphCommand::RetouchNode => {
                    dialogue
                        .nodes
                        .iter_mut()
                        .find(|node| node.id == node_id)
                        .ok_or("Nœud absent")?
                        .text = self.reply.get_untracked();
                }
                GraphCommand::RemoveChoice => {
                    let choice = id(&self.choice.get_untracked())?;
                    let node = dialogue
                        .nodes
                        .iter_mut()
                        .find(|node| node.id == node_id)
                        .ok_or("Nœud absent")?;
                    if !node.choices.iter().any(|entry| entry.id == choice) {
                        return Err("Choisir un choix courant".into());
                    }
                    node.choices.retain(|entry| entry.id != choice);
                }
            }
            *source = source
                .with_dialogue_definition(dialogue)
                .map_err(|error| error.to_string())?;
            Ok(())
        })
    }
}

pub(super) fn variable_value(
    document: &GameplayDocument,
    variable: EntityId,
    value: &str,
) -> Result<NarrativeValue, String> {
    let initial = &document
        .narrative
        .as_ref()
        .and_then(|narrative| narrative.variable(variable))
        .ok_or("Choisir une variable courante".to_owned())?
        .initial;
    super::variable_value_field::parse_value(initial, value)
}

#[component]
#[allow(
    clippy::too_many_lines,
    reason = "A node/choice form shares its atomic preview callback"
)]
pub(super) fn DialogueControls(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    send: Callback<Edit>,
) -> impl IntoView {
    let fields = Fields {
        dialogue: RwSignal::new(String::new()),
        node: RwSignal::new(String::new()),
        choice: RwSignal::new(String::new()),
        reply: RwSignal::new("Nouvelle réplique".into()),
        text: RwSignal::new("Nouveau choix".into()),
        next: RwSignal::new(String::new()),
        condition: RwSignal::new("always".into()),
        reference: RwSignal::new(String::new()),
        subject: RwSignal::new(String::new()),
        comparison: RwSignal::new("equal".into()),
        value: RwSignal::new("true".into()),
        effect: RwSignal::new("none".into()),
        effect_mode: RwSignal::new("replace".into()),
        effect_reference: RwSignal::new(String::new()),
        effect_value: RwSignal::new("true".into()),
        solution: RwSignal::new(String::new()),
    };
    let document = move || {
        state
            .get()
            .and_then(|state| state.zone_head)
            .and_then(|head| head.corrections.gameplay.map(|document| *document))
    };
    let graph = move || {
        document().and_then(|document| {
            id(&fields.dialogue.get())
                .ok()
                .and_then(|id| document.dialogue(id).cloned())
        })
    };
    let selected_choice = move || {
        let node = id(&fields.node.get()).ok()?;
        let choice = id(&fields.choice.get()).ok()?;
        graph()?
            .nodes
            .into_iter()
            .find(|entry| entry.id == node)?
            .choices
            .into_iter()
            .find(|entry| entry.id == choice)
    };
    Effect::new(
        move |previous: Option<(String, String, Option<forge_core::ContentHash>)>| {
            let key = (
                fields.dialogue.get(),
                fields.node.get(),
                super::source_hash(state),
            );
            if previous.as_ref() == Some(&key) {
                return key;
            }
            if let Some(node) = graph().and_then(|dialogue| {
                id(&fields.node.get())
                    .ok()
                    .and_then(|id| dialogue.nodes.into_iter().find(|node| node.id == id))
            }) {
                fields.reply.set(node.text);
            }
            key
        },
    );
    Effect::new(
        move |previous: Option<(String, String, String, Option<forge_core::ContentHash>)>| {
            let key = (
                fields.dialogue.get(),
                fields.node.get(),
                fields.choice.get(),
                super::source_hash(state),
            );
            if previous.as_ref() == Some(&key) {
                return key;
            }
            if let Some(choice) = graph()
                .and_then(|dialogue| {
                    id(&fields.node.get())
                        .ok()
                        .and_then(|id| dialogue.nodes.into_iter().find(|node| node.id == id))
                })
                .and_then(|node| {
                    id(&fields.choice.get())
                        .ok()
                        .and_then(|id| node.choices.into_iter().find(|choice| choice.id == id))
                })
            {
                fields.text.set(choice.text);
                fields.next.set(
                    choice
                        .next
                        .map_or_else(String::new, |next| next.to_string()),
                );
                fields.condition.set("keep".into());
                fields.effect.set("keep".into());
            } else {
                fields.text.set("Nouveau choix".into());
                fields.next.set(String::new());
                fields.condition.set("always".into());
                fields.effect.set("none".into());
            }
            key
        },
    );
    let disabled = move || {
        busy.get()
            || state
                .get()
                .is_none_or(|state| state.zone_head.is_none() || state.playtest_hash.is_some())
    };
    view! {
        <Show when=move ||document().is_some_and(|document|document.surface_visible(CreatorSurface::Dialogues))>
            <details class="creator-dialogue-graph"><summary>"Graphe des dialogues"</summary><fieldset disabled=disabled>
                <label>"Dialogue"<select prop:value=move ||fields.dialogue.get() on:change=move |event|{fields.dialogue.set(event_target_value(&event));fields.node.set(String::new());fields.choice.set(String::new());}>
                    <option value="">"Choisir un dialogue"</option>{move ||document().map(|document|document.dialogues.into_iter().map(|dialogue| {
                        let name=document.actors.iter().find(|actor|actor.dialogue==Some(dialogue.id)).map_or_else(||dialogue.id.to_string(),|actor|actor.name.clone());
                        view!{<option value=dialogue.id.to_string()>{name}</option>}
                    }).collect_view())}
                </select></label>
                <ol>{move ||graph().map(|dialogue| {
                    let names=dialogue.nodes.iter().map(|node|(node.id,node.text.clone())).collect::<std::collections::BTreeMap<_,_>>();
                    let start=dialogue.start;
                    dialogue.nodes.into_iter().map(|node| {
                    let node_id=node.id;
                    let links=node.choices.iter().map(|choice| {
                        let destination=choice.next.map_or_else(||"Fin du dialogue".into(),|id|names.get(&id).cloned().unwrap_or_else(||"Référence absente".into()));
                        (format!("{} → {destination}",choice.text),choice.next)
                    }).collect::<Vec<_>>();
                    let title=if node_id==start {format!("{} · Début",node.text)} else {node.text};
                    view!{<li><button on:click=move |_|{fields.node.set(node_id.to_string());fields.choice.set(String::new());}>{title}</button><ul>{links.into_iter().map(|(text,next)|view!{<li><button disabled=next.is_none() on:click=move |_| {
                        if let Some(next)=next {fields.node.set(next.to_string());fields.choice.set(String::new());}
                    }>{text}</button></li>}).collect_view()}</ul></li>}
                }).collect_view()})}</ol>
                <label>"Réplique"<textarea prop:value=move ||fields.reply.get() on:input=move |event|fields.reply.set(event_target_value(&event))/></label>
                <button on:click=move |_|send.run(fields.preview(state.get_untracked(),true))>"Examiner un nouveau nœud"</button>
                <button on:click=move |_|send.run(fields.command(state.get_untracked(),GraphCommand::RetouchNode))>"Examiner la réplique seule"</button>
                <button on:click=move |_|send.run(fields.command(state.get_untracked(),GraphCommand::SetStart))>"Examiner ce nœud comme début"</button>
                <button on:click=move |_|send.run(fields.command(state.get_untracked(),GraphCommand::RemoveNode))>"Examiner la suppression du nœud"</button>
                <label>"Choix à remplacer"<select prop:value=move ||fields.choice.get() on:change=move |event|fields.choice.set(event_target_value(&event))>
                    <option value="">"Créer un nouveau choix"</option>{move ||graph().and_then(|dialogue|id(&fields.node.get()).ok().and_then(|id|dialogue.nodes.into_iter().find(|node|node.id==id))).map(|node|node.choices.into_iter().map(|choice|view!{<option value=choice.id.to_string()>{choice.text}</option>}).collect_view())}
                </select></label>
                <button on:click=move |_|send.run(fields.command(state.get_untracked(),GraphCommand::RemoveChoice))>"Examiner la suppression du choix"</button>
                <p>"La retouche conserve l’identité du choix. Garder la condition et les effets existants permet de retoucher son texte et ses liens sans les réécrire. Examiner le diff avant application."</p>
                {move ||document().zip(selected_choice()).map(|(document,choice)| {
                    let condition=super::variable_value_field::condition_label(&document,&choice.condition);
                    let effects=choice.effects.iter().map(|effect|super::variable_value_field::effect_label(&document,effect)).collect::<Vec<_>>();
                    view!{<p>{format!("Condition enregistrée : {condition}")}</p><p>{format!("Effets enregistrés, dans l’ordre : {}",if effects.is_empty() {"Aucun".into()} else {effects.join(" ; ")})}</p>}
                })}
                <label>"Texte du choix"<input prop:value=move ||fields.text.get() on:input=move |event|fields.text.set(event_target_value(&event))/></label>
                <label>"Destination"<select prop:value=move ||fields.next.get() on:change=move |event|fields.next.set(event_target_value(&event))><option value="">"Fin du dialogue"</option>{move ||graph().map(|dialogue|dialogue.nodes.into_iter().map(|node|view!{<option value=node.id.to_string()>{node.text}</option>}).collect_view())}</select></label>
                <label>"Condition"<select prop:value=move ||fields.condition.get() on:change=move |event| {
                    let mode=event_target_value(&event);
                    fields.reference.set(String::new());
                    if mode=="relation" {fields.value.set("0".into());}
                    else if mode=="has_item" {fields.value.set("1".into());}
                    fields.condition.set(mode);
                }>
                    <option value="keep">"Garder la condition existante"</option><option value="always">"Toujours"</option><option value="variable">"Variable"</option><option value="relation">"Opinion locale d’un personnage"</option><option value="quest_available">"Quête disponible"</option><option value="quest_active">"Quête active"</option><option value="quest_completed">"Quête terminée"</option><option value="quest_failed">"Quête échouée"</option><option value="has_item">"Item possédé"</option><option value="actor_defeated">"Personnage vaincu"</option><option value="object_destroyed">"Objet détruit"</option>
                </select></label>
                <Show when=move ||!matches!(fields.condition.get().as_str(),"always"|"keep")>
                <label>{move ||if fields.condition.get()=="relation" {"Personnage observateur"} else {"Référence de la condition"}}<select prop:value=move ||fields.reference.get() on:change=move |event|fields.reference.set(event_target_value(&event))>
                    <option value="">"Choisir une référence"</option>{move ||document().map(|document|condition_options(&document,&fields.condition.get()).into_iter().map(|(id,name)|view!{<option value=id.to_string()>{name}</option>}).collect_view())}
                </select></label>
                </Show>
                <Show when=move ||fields.condition.get()=="variable">
                    <super::variable_value_field::ComparisonField value=fields.comparison label="Comparaison de variable"/>
                    <super::variable_value_field::VariableValueField state=state selected=fields.reference value=fields.value label="Valeur de la condition"/>
                </Show>
                <Show when=move ||fields.condition.get()=="relation">
                    <label>"Opinion envers"<select prop:value=move ||fields.subject.get() on:change=move |event|fields.subject.set(event_target_value(&event))>
                        <option value="">"Le joueur de cette carte"</option>{move ||document().map(|document|document.actors.into_iter().filter(|actor|actor.id!=document.player).map(|actor|view!{<option value=actor.id.to_string()>{actor.name}</option>}).collect_view())}
                    </select></label>
                    <super::variable_value_field::ComparisonField value=fields.comparison label="Comparaison de l’opinion"/>
                    <label>"Seuil d’opinion (points)"<input type="number" min=forge_gameplay::MIN_RELATION.to_string() max=forge_gameplay::MAX_RELATION.to_string() step="1" prop:value=move ||fields.value.get() on:input=move |event|fields.value.set(event_target_value(&event))/></label>
                </Show>
                <Show when=move ||fields.condition.get()=="has_item">
                    <label>"Quantité requise"<input type="number" min="1" step="1" prop:value=move ||fields.value.get() on:input=move |event|fields.value.set(event_target_value(&event))/></label>
                </Show>
                <label>"Effet"<select prop:value=move ||fields.effect.get() on:change=move |event|fields.effect.set(event_target_value(&event))><option value="keep">"Garder les effets existants"</option><option value="none">"Aucun"</option><option value="variable">"Affecter une variable"</option><option value="accept">"Accepter une quête"</option><option value="complete">"Terminer une récupération simple"</option><option value="solution">"Résoudre par une solution"</option><option value="fail">"Échouer une quête"</option><option value="open_door">"Ouvrir une porte"</option><option value="close_door">"Fermer une porte"</option></select></label>
                <Show when=move ||!matches!(fields.effect.get().as_str(),"none"|"keep")>
                <label>"Référence de l’effet"<select prop:value=move ||fields.effect_reference.get() on:change=move |event|fields.effect_reference.set(event_target_value(&event))><option value="">"Choisir une référence"</option>{move ||document().map(|document|condition_options(&document,match fields.effect.get().as_str() {"variable"=>"variable","open_door"|"close_door"=>"door",_=>"quest_active"}).into_iter().map(|(id,name)|view!{<option value=id.to_string()>{name}</option>}).collect_view())}</select></label>
                </Show>
                <Show when=move ||fields.effect.get()=="variable">
                    <super::variable_value_field::VariableValueField state=state selected=fields.effect_reference value=fields.effect_value label="Nouvelle valeur de variable"/>
                </Show>
                <Show when=move ||fields.effect.get()!="keep">
                    <label>"Application du nouvel effet"<select prop:value=move ||fields.effect_mode.get() on:change=move |event|fields.effect_mode.set(event_target_value(&event))><option value="replace">"Remplacer la liste des effets"</option><option value="append">"Ajouter après les effets existants"</option></select></label>
                </Show>
                <Show when=move ||fields.effect.get()=="solution">
                    <label>"Solution"<select prop:value=move ||fields.solution.get() on:change=move |event|fields.solution.set(event_target_value(&event))><option value="">"Choisir une solution"</option>{move ||document().and_then(|document|id(&fields.effect_reference.get()).ok().and_then(|id|document.quest_flow(id).cloned())).map(|flow|flow.solutions.into_iter().map(|solution|view!{<option value=solution.id.to_string()>{solution.name}</option>}).collect_view())}</select></label>
                </Show>
                <button on:click=move |_|send.run(fields.preview(state.get_untracked(),false))>"Examiner le nœud et le choix"</button>
            </fieldset></details>
        </Show>
    }
}

fn condition_options(document: &GameplayDocument, kind: &str) -> Vec<(EntityId, String)> {
    match kind {
        "relation" => document
            .actors
            .iter()
            .filter(|actor| {
                document
                    .factions
                    .as_ref()
                    .and_then(|factions| factions.actor_faction(actor.id))
                    .is_some()
            })
            .map(|actor| (actor.id, actor.name.clone()))
            .collect(),
        "variable" => document
            .narrative
            .as_ref()
            .map_or_else(Vec::new, |narrative| {
                narrative
                    .variables
                    .iter()
                    .map(|variable| (variable.id, variable.name.clone()))
                    .collect()
            }),
        "has_item" => document
            .items
            .iter()
            .map(|item| (item.id, item.name.clone()))
            .collect(),
        "actor_defeated" => document
            .actors
            .iter()
            .map(|actor| (actor.id, actor.name.clone()))
            .collect(),
        "object_destroyed" | "door" => document
            .objects
            .iter()
            .filter(|object| {
                kind != "door" || matches!(object.kind, forge_gameplay::ObjectKind::Door { .. })
            })
            .map(|object| (object.id, object.name.clone()))
            .collect(),
        "always" => vec![],
        _ => document
            .quests
            .iter()
            .map(|quest| (quest.id, quest.name.clone()))
            .collect(),
    }
}
