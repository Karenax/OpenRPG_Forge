use super::super::variable_value_field::{
    ComparisonField, VariableValueField, comparison_key, parse_comparison,
};
use super::{Fields, document, id, instruction, kind, rows};
use forge_core::{EntityId, canonical_json_sha256};
use forge_gameplay::{
    GameplayDocument, NarrativeValue, ScriptBranch, ScriptDefinition, ScriptInstruction,
    ScriptOperation, ScriptTreeLocation,
};
use forge_project::authoring::AuthoringState;
use leptos::prelude::*;

#[derive(Clone, Copy)]
struct OperationFields {
    selected: RwSignal<String>,
    kind: RwSignal<String>,
    reference: RwSignal<String>,
    value: RwSignal<String>,
    comparison: RwSignal<String>,
    open: RwSignal<bool>,
    message: RwSignal<String>,
    iterations: RwSignal<String>,
    parent: RwSignal<String>,
    branch: RwSignal<String>,
    position: RwSignal<String>,
}

impl OperationFields {
    fn load(self, current: Option<ScriptInstruction>) {
        self.kind.set(
            current
                .as_ref()
                .map_or("message", |current| kind(&current.operation))
                .into(),
        );
        self.reference.set(String::new());
        self.value.set("true".into());
        self.comparison.set("equal".into());
        self.open.set(true);
        self.message.set("Nouveau message".into());
        self.iterations.set("1".into());
        let Some(current) = current else {
            return;
        };
        match current.operation {
            ScriptOperation::SetVariable { variable, value } => {
                self.reference.set(variable.to_string());
                self.load_value(value);
            }
            ScriptOperation::SetDoor { object, open } => {
                self.reference.set(object.to_string());
                self.open.set(open);
            }
            ScriptOperation::Message { text } => self.message.set(text),
            ScriptOperation::If {
                variable,
                comparison,
                value,
                ..
            } => {
                self.reference.set(variable.to_string());
                self.comparison.set(comparison_key(comparison).into());
                self.load_value(value);
            }
            ScriptOperation::Repeat { iterations, .. } => {
                self.iterations.set(iterations.to_string());
            }
        }
    }
    fn load_value(self, value: NarrativeValue) {
        self.value.set(match value {
            NarrativeValue::Boolean(value) => value.to_string(),
            NarrativeValue::Integer(value) => value.to_string(),
            NarrativeValue::Text(value) => value,
        });
    }

    fn operation(
        self,
        source: &GameplayDocument,
        draft: &ScriptDefinition,
    ) -> Result<ScriptOperation, String> {
        let selected = self.selected.get_untracked();
        let current = if selected.is_empty() {
            None
        } else {
            Some(
                instruction(&draft.steps, id(&selected)?)
                    .ok_or("Choisir une instruction courante.")?,
            )
        };
        let selected_kind = self.kind.get_untracked();
        if let Some(current) = current {
            let children = match &current.operation {
                ScriptOperation::If {
                    then_steps,
                    else_steps,
                    ..
                } => !then_steps.is_empty() || !else_steps.is_empty(),
                ScriptOperation::Repeat { steps, .. } => !steps.is_empty(),
                _ => false,
            };
            if kind(&current.operation) != selected_kind && children {
                return Err("Retirer ou déplacer d’abord les sous-instructions avant de changer le type de cette instruction.".into());
            }
        }
        let variable = || -> Result<_, String> {
            let variable = id(&self.reference.get_untracked())?;
            Ok((
                variable,
                super::super::dialogue_controls::variable_value(
                    source,
                    variable,
                    &self.value.get_untracked(),
                )?,
            ))
        };
        Ok(match selected_kind.as_str() {
            "variable" => {
                let (variable, value) = variable()?;
                ScriptOperation::SetVariable { variable, value }
            }
            "door" => ScriptOperation::SetDoor {
                object: id(&self.reference.get_untracked())?,
                open: self.open.get_untracked(),
            },
            "message" => ScriptOperation::Message {
                text: self.message.get_untracked(),
            },
            "if" => {
                let (variable, value) = variable()?;
                let (then_steps, else_steps) = current
                    .and_then(|current| {
                        if let ScriptOperation::If {
                            then_steps,
                            else_steps,
                            ..
                        } = &current.operation
                        {
                            Some((then_steps.clone(), else_steps.clone()))
                        } else {
                            None
                        }
                    })
                    .unwrap_or_default();
                ScriptOperation::If {
                    variable,
                    comparison: parse_comparison(&self.comparison.get_untracked())?,
                    value,
                    then_steps,
                    else_steps,
                }
            }
            "repeat" => {
                let steps = current
                    .and_then(|current| {
                        if let ScriptOperation::Repeat { steps, .. } = &current.operation {
                            Some(steps.clone())
                        } else {
                            None
                        }
                    })
                    .unwrap_or_default();
                ScriptOperation::Repeat {
                    iterations: self
                        .iterations
                        .get_untracked()
                        .trim()
                        .parse()
                        .map_err(|_| "Choisir un nombre entier de répétitions.".to_owned())?,
                    steps,
                }
            }
            _ => return Err("Choisir un type d’instruction courant.".into()),
        })
    }

    fn location(self) -> Result<ScriptTreeLocation, String> {
        let parent = self.parent.get_untracked();
        let parent = if parent.is_empty() {
            None
        } else {
            Some(id(&parent)?)
        };
        let branch = if parent.is_none() {
            ScriptBranch::Root
        } else {
            match self.branch.get_untracked().as_str() {
                "then" => ScriptBranch::Then,
                "else" => ScriptBranch::Else,
                "body" => ScriptBranch::RepeatBody,
                _ => return Err("Choisir la branche de destination.".into()),
            }
        };
        let index = self
            .position
            .get_untracked()
            .trim()
            .parse::<usize>()
            .ok()
            .and_then(|position| position.checked_sub(1))
            .ok_or("La première position est 1.")?;
        Ok(ScriptTreeLocation {
            parent,
            branch,
            index,
        })
    }
    fn stage(self, fields: Fields, state: RwSignal<Option<AuthoringState>>) -> Result<(), String> {
        let source =
            document(state).ok_or("Ouvrir une carte avant de préparer une instruction.")?;
        let draft = fields.draft.get_untracked();
        let selected = self.selected.get_untracked();
        let operation = self.operation(&source, &draft)?;
        let node_id = if selected.is_empty() {
            let hash = canonical_json_sha256(&(draft.id, &draft.steps, &operation))
                .map_err(|error| error.to_string())?;
            EntityId::deterministic(format!("script-instruction/{hash}"))
        } else {
            id(&selected)?
        };
        let node = ScriptInstruction {
            id: node_id,
            operation,
        };
        let result = if selected.is_empty() {
            draft.draft_insert_instruction(self.location()?, node)
        } else {
            draft.draft_replace_instruction(node)
        };
        fields
            .draft
            .set(result.map_err(|error| format!("{} {}", error.cause, error.remedy))?);
        Ok(())
    }
    fn move_selected(self, fields: Fields) -> Result<(), String> {
        let result = fields
            .draft
            .get_untracked()
            .draft_move_instruction(id(&self.selected.get_untracked())?, self.location()?)
            .map_err(|error| format!("{} {}", error.cause, error.remedy))?;
        fields.draft.set(result);
        Ok(())
    }
    fn remove(self, fields: Fields) -> Result<(), String> {
        let result = fields
            .draft
            .get_untracked()
            .draft_remove_instruction(id(&self.selected.get_untracked())?)
            .map_err(|error| format!("{} {}", error.cause, error.remedy))?;
        fields.draft.set(result);
        self.selected.set(String::new());
        Ok(())
    }
}

#[component]
pub(super) fn InstructionControls(
    fields: Fields,
    state: RwSignal<Option<AuthoringState>>,
) -> impl IntoView {
    let operation = OperationFields {
        selected: fields.instruction,
        kind: RwSignal::new("message".into()),
        reference: RwSignal::new(String::new()),
        value: RwSignal::new("true".into()),
        comparison: RwSignal::new("equal".into()),
        open: RwSignal::new(true),
        message: RwSignal::new("Nouveau message".into()),
        iterations: RwSignal::new("1".into()),
        parent: RwSignal::new(String::new()),
        branch: RwSignal::new("root".into()),
        position: RwSignal::new("1".into()),
    };
    Effect::new(
        move |previous: Option<(String, Option<forge_core::ContentHash>)>| {
            let draft = fields.draft.get();
            let key = (
                operation.selected.get(),
                canonical_json_sha256(&draft.steps).ok(),
            );
            if previous.as_ref() != Some(&key) {
                operation.load(
                    id(&key.0)
                        .ok()
                        .and_then(|id| instruction(&draft.steps, id).cloned()),
                );
            }
            key
        },
    );
    Effect::new(move |_: Option<()>| {
        let parent = operation.parent.get();
        let draft = fields.draft.get();
        let current = id(&parent)
            .ok()
            .and_then(|id| instruction(&draft.steps, id));
        let branch = operation.branch.get_untracked();
        match current.map(|current| &current.operation) {
            Some(ScriptOperation::If { .. }) => {
                if branch != "then" && branch != "else" {
                    operation.branch.set("then".into());
                }
            }
            Some(ScriptOperation::Repeat { .. }) => operation.branch.set("body".into()),
            _ => operation.branch.set("root".into()),
        }
    });
    view! {
        <p>{move ||format!("{} instructions dans le brouillon",rows(document(state).as_ref(),&fields.draft.get().steps,0).len())}</p>
        <label>"Instruction à retoucher"<select id=move ||format!("script-instruction-{}",fields.draft.get().id) prop:value=move ||operation.selected.get() on:change=move |event|operation.selected.set(event_target_value(&event))>
            <option value="">"Préparer une nouvelle instruction"</option>
            {move ||rows(document(state).as_ref(),&fields.draft.get().steps,0).into_iter().map(|(id,label)|view!{<option value=id.to_string()>{label}</option>}).collect_view()}
        </select></label>
        <label>"Type d’instruction"<select prop:value=move ||operation.kind.get() on:change=move |event|operation.kind.set(event_target_value(&event))>
            <option value="message">"Afficher un message"</option><option value="variable">"Affecter une variable"</option><option value="door">"Ouvrir ou fermer une porte"</option><option value="if">"Branche conditionnelle"</option><option value="repeat">"Répétition bornée"</option>
        </select></label>
        <OperationValueControls operation=operation state=state/>
        <DestinationControls operation=operation fields=fields state=state/>
        <button on:click=move |_|fields.error.set(operation.stage(fields,state).err())>"Ajouter ou retoucher l’instruction dans le brouillon"</button>
        <button disabled=move ||operation.selected.get().is_empty() on:click=move |_|fields.error.set(operation.move_selected(fields).err())>"Déplacer l’instruction retenue dans le brouillon"</button>
        <button disabled=move ||operation.selected.get().is_empty() on:click=move |_|fields.error.set(operation.remove(fields).err())>"Retirer l’instruction et ses sous-instructions du brouillon"</button>
    }
}

#[component]
fn OperationValueControls(
    operation: OperationFields,
    state: RwSignal<Option<AuthoringState>>,
) -> impl IntoView {
    view! {
        <Show when=move ||operation.kind.get()=="variable"||operation.kind.get()=="if">
            <label>"Variable"<select prop:value=move ||operation.reference.get() on:change=move |event|operation.reference.set(event_target_value(&event))>
                <option value="">"Choisir une variable"</option>
                {move ||document(state).and_then(|source|source.narrative).map(|narrative|narrative.variables.into_iter().map(|variable|view!{<option value=variable.id.to_string()>{variable.name}</option>}).collect_view())}
            </select></label>
            <Show when=move ||operation.kind.get()=="if"><ComparisonField value=operation.comparison label="Comparaison"/></Show>
            <VariableValueField state=state selected=operation.reference value=operation.value label="Valeur"/>
        </Show>
        <Show when=move ||operation.kind.get()=="door">
            <label>"Porte"<select prop:value=move ||operation.reference.get() on:change=move |event|operation.reference.set(event_target_value(&event))>
                <option value="">"Choisir une porte"</option>
                {move ||document(state).map(|source|source.objects.into_iter().filter(|object|matches!(object.kind,forge_gameplay::ObjectKind::Door{..})).map(|object|view!{<option value=object.id.to_string()>{object.name}</option>}).collect_view())}
            </select></label>
            <label><input type="checkbox" prop:checked=move ||operation.open.get() on:change=move |event|operation.open.set(event_target_checked(&event)) />"Ouvrir (décocher pour fermer)"</label>
        </Show>
        <Show when=move ||operation.kind.get()=="message"><label>"Message littéral"<textarea maxlength=forge_gameplay::MAX_SCRIPT_MESSAGE_BYTES.to_string() prop:value=move ||operation.message.get() on:input=move |event|operation.message.set(event_target_value(&event))></textarea></label></Show>
        <Show when=move ||operation.kind.get()=="repeat"><label>"Nombre de répétitions"<input type="number" min="1" max="16" step="1" prop:value=move ||operation.iterations.get() on:input=move |event|operation.iterations.set(event_target_value(&event))/></label><p>"Ajouter ensuite les instructions de son corps avec la destination ci-dessous."</p></Show>
    }
}

#[component]
fn DestinationControls(
    operation: OperationFields,
    fields: Fields,
    state: RwSignal<Option<AuthoringState>>,
) -> impl IntoView {
    view! {
        <label>"Destination de l’ajout ou du déplacement"<select prop:value=move ||operation.parent.get() on:change=move |event|operation.parent.set(event_target_value(&event))>
            <option value="">"Programme principal"</option>
            {move ||{
                let draft=fields.draft.get();rows(document(state).as_ref(),&draft.steps,0).into_iter().filter(|(id,_)|instruction(&draft.steps,*id).is_some_and(|node|matches!(node.operation,ScriptOperation::If{..}|ScriptOperation::Repeat{..}))).map(|(id,label)|view!{<option value=id.to_string()>{label}</option>}).collect_view()
            }}
        </select></label>
        <Show when=move ||!operation.parent.get().is_empty()><label>"Branche de destination"<select prop:value=move ||operation.branch.get() on:change=move |event|operation.branch.set(event_target_value(&event))>
            <option value="then">"Alors"</option><option value="else">"Sinon"</option><option value="body">"Corps de la répétition"</option>
        </select></label></Show>
        <label>"Position dans la destination (1 = début)"<input type="number" min="1" max="129" step="1" prop:value=move ||operation.position.get() on:input=move |event|operation.position.set(event_target_value(&event))/></label>
    }
}
