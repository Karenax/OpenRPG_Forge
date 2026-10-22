//! Local typed script drafts use the common source command boundary.
use super::{Edit, edit, id, source_hash};
use forge_core::{EntityId, canonical_json_sha256};
use forge_gameplay::{
    CreatorSurface, GameplayDocument, GameplayError, ObjectKind, ScriptDefinition,
    ScriptDiagnostic, ScriptInstruction, ScriptOperation, ScriptPermissions,
};
use forge_project::authoring::AuthoringState;
use leptos::prelude::*;

mod diagnostics;
mod instruction_fields;

fn document(state: RwSignal<Option<AuthoringState>>) -> Option<GameplayDocument> {
    state
        .get()?
        .zone_head?
        .corrections
        .gameplay
        .map(|source| *source)
}
fn blank(source: Option<&GameplayDocument>) -> ScriptDefinition {
    let key = source
        .and_then(|source| canonical_json_sha256(source).ok())
        .map_or_else(|| "uncommitted".into(), |hash| hash.to_string());
    ScriptDefinition {
        id: EntityId::deterministic(format!("script/{key}")),
        name: "Nouveau script".into(),
        permissions: ScriptPermissions::default(),
        steps: vec![],
    }
}

#[derive(Clone, Copy)]
struct Fields {
    selected: RwSignal<String>,
    draft: RwSignal<ScriptDefinition>,
    error: RwSignal<Option<String>>,
    diagnostic: RwSignal<Option<ScriptDiagnostic>>,
    instruction: RwSignal<String>,
    verification: RwSignal<Option<String>>,
}
impl Fields {
    fn record_error(self, error: &GameplayError) -> String {
        if let GameplayError::ScriptRefused(diagnostic) = error {
            self.diagnostic.set(Some(diagnostic.clone()));
        }
        error.to_string()
    }
    fn preview(self, state: Option<AuthoringState>, create: bool) -> Edit {
        edit(state, move |_, source| {
            let draft = self.draft.get_untracked();
            if create && !self.selected.get_untracked().is_empty() {
                return Err(
                    "Choisir « Préparer un nouveau script » pour créer un programme distinct."
                        .into(),
                );
            }
            if !create
                && (id(&self.selected.get_untracked())? != draft.id
                    || source.script_definition(draft.id).is_none())
            {
                return Err("Choisir un script courant.".into());
            }
            *source = source
                .with_script_definition(draft)
                .map_err(|error| self.record_error(&error))?;
            Ok(())
        })
    }
    fn remove(self, state: Option<AuthoringState>) -> Edit {
        edit(state, move |_, source| {
            let script = id(&self.selected.get_untracked())?;
            let mut definitions = source
                .scripting
                .clone()
                .ok_or("Aucun script à retoucher.")?;
            if !definitions.scripts.iter().any(|entry| entry.id == script) {
                return Err("Choisir un script courant.".into());
            }
            definitions.scripts.retain(|entry| entry.id != script);
            *source = source
                .with_scripting_definition(Some(definitions))
                .map_err(|error| error.to_string())?;
            Ok(())
        })
    }
}

fn instruction(steps: &[ScriptInstruction], id: EntityId) -> Option<&ScriptInstruction> {
    for current in steps {
        if current.id == id {
            return Some(current);
        }
        let found = match &current.operation {
            ScriptOperation::If {
                then_steps,
                else_steps,
                ..
            } => instruction(then_steps, id).or_else(|| instruction(else_steps, id)),
            ScriptOperation::Repeat { steps, .. } => instruction(steps, id),
            _ => None,
        };
        if found.is_some() {
            return found;
        }
    }
    None
}

fn kind(operation: &ScriptOperation) -> &'static str {
    match operation {
        ScriptOperation::SetVariable { .. } => "variable",
        ScriptOperation::SetDoor { .. } => "door",
        ScriptOperation::Message { .. } => "message",
        ScriptOperation::If { .. } => "if",
        ScriptOperation::Repeat { .. } => "repeat",
    }
}

fn rows(
    source: Option<&GameplayDocument>,
    steps: &[ScriptInstruction],
    depth: usize,
) -> Vec<(EntityId, String)> {
    let mut result = vec![];
    for current in steps {
        let label = match &current.operation {
            ScriptOperation::Message { text } => {
                format!("Message : {}", text.chars().take(60).collect::<String>())
            }
            ScriptOperation::SetVariable { variable, .. } => format!(
                "Affecter {}",
                source
                    .and_then(|source| source.narrative.as_ref()?.variable(*variable))
                    .map_or("Variable absente", |variable| variable.name.as_str())
            ),
            ScriptOperation::SetDoor { object, open } => format!(
                "{} {}",
                if *open { "Ouvrir" } else { "Fermer" },
                source
                    .and_then(|source| source.object(*object))
                    .map_or("Porte absente", |object| object.name.as_str())
            ),
            ScriptOperation::If { variable, .. } => format!(
                "Si {}",
                source
                    .and_then(|source| source.narrative.as_ref()?.variable(*variable))
                    .map_or("Variable absente", |variable| variable.name.as_str())
            ),
            ScriptOperation::Repeat { iterations, .. } => format!("Répéter {iterations} fois"),
        };
        result.push((current.id, format!("{}{label}", "· ".repeat(depth))));
        match &current.operation {
            ScriptOperation::If {
                then_steps,
                else_steps,
                ..
            } => {
                result.extend(
                    rows(source, then_steps, depth + 1)
                        .into_iter()
                        .map(|(id, label)| (id, format!("Alors · {label}"))),
                );
                result.extend(
                    rows(source, else_steps, depth + 1)
                        .into_iter()
                        .map(|(id, label)| (id, format!("Sinon · {label}"))),
                );
            }
            ScriptOperation::Repeat { steps, .. } => result.extend(
                rows(source, steps, depth + 1)
                    .into_iter()
                    .map(|(id, label)| (id, format!("Répétition · {label}"))),
            ),
            _ => (),
        }
    }
    result
}

#[component]
pub(super) fn ScriptControls(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    send: Callback<Edit>,
) -> impl IntoView {
    let fields = Fields {
        selected: RwSignal::new(String::new()),
        draft: RwSignal::new(blank(None)),
        error: RwSignal::new(None),
        diagnostic: RwSignal::new(None),
        instruction: RwSignal::new(String::new()),
        verification: RwSignal::new(None),
    };
    Effect::new(move |previous: Option<super::FieldSource>| {
        let key = (fields.selected.get(), source_hash(state));
        if previous.as_ref() != Some(&key) {
            let source = document(state);
            let current = source.as_ref().and_then(|source| {
                id(&key.0)
                    .ok()
                    .and_then(|id| source.script_definition(id).cloned())
            });
            fields
                .draft
                .set(current.unwrap_or_else(|| blank(source.as_ref())));
            fields.error.set(None);
            fields.instruction.set(String::new());
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
        <Show when=move ||document(state).is_some_and(|source|source.surface_visible(CreatorSurface::SandboxedScripts))>
            <details><summary>"Scripts bornés et permissions"</summary><fieldset disabled=disabled>
                <p>"Composer un programme avec variables, portes, messages, branches et répétitions. Un accès interdit ou un quota dépassé annule l’appel entier. Le script n’a aucun accès aux fichiers, au réseau ou aux processus."</p>
                <label>"Script à retoucher"<select prop:value=move ||fields.selected.get() on:change=move |event|fields.selected.set(event_target_value(&event))>
                    <option value="">"Préparer un nouveau script"</option>
                    {move ||document(state).and_then(|source|source.scripting).map(|definitions|definitions.scripts.into_iter().map(|script|view!{<option value=script.id.to_string()>{script.name}</option>}).collect_view())}
                </select></label>
                <label>"Nom du script"<input maxlength="256" prop:value=move ||fields.draft.get().name on:input=move |event|fields.draft.update(|draft|draft.name=event_target_value(&event))/></label>
                <p>"Au plus 128 instructions et 8 niveaux par programme, 16 répétitions par boucle et 256 instructions exécutées par appel. Le budget d’événements de la partie s’applique aussi."</p>
                <PermissionControls fields=fields state=state/>
                <instruction_fields::InstructionControls fields=fields state=state/>
                <diagnostics::DiagnosticControls fields=fields state=state/>
                <p role="alert">{move ||fields.error.get()}</p>
                <button disabled=move ||!fields.selected.get().is_empty() on:click=move |_|send.run(fields.preview(state.get_untracked(),true))>"Examiner un nouveau script"</button>
                <button disabled=move ||fields.selected.get().is_empty() on:click=move |_|send.run(fields.preview(state.get_untracked(),false))>"Examiner la retouche du script"</button>
                <button disabled=move ||fields.selected.get().is_empty() on:click=move |_|send.run(fields.remove(state.get_untracked()))>"Examiner la suppression du script"</button>
                <p>"Le programme reste un brouillon avant application. Modifier ses instructions ou permissions peut rendre une partie sauvegardée incompatible."</p>
            </fieldset></details>
        </Show>
    }
}

#[component]
fn PermissionControls(fields: Fields, state: RwSignal<Option<AuthoringState>>) -> impl IntoView {
    view! {
        <h4>"Variables accessibles"</h4>
        {move ||document(state).and_then(|source|source.narrative).map(|narrative|narrative.variables.into_iter().map(|variable|{
            let id=variable.id;let name=variable.name;
            let read_label=format!("Lire la variable {name}");let write_label=format!("Modifier la variable {name}");
            view!{<p>{name}
                <label><input type="checkbox" aria-label=read_label prop:checked=move ||fields.draft.get().permissions.read_variables.contains(&id) on:change=move |event|fields.draft.update(|draft|{
                    if event_target_checked(&event){draft.permissions.read_variables.insert(id);}else{draft.permissions.read_variables.remove(&id);}
                })/>"Lecture"</label>
                <label><input type="checkbox" aria-label=write_label prop:checked=move ||fields.draft.get().permissions.write_variables.contains(&id) on:change=move |event|fields.draft.update(|draft|{
                    if event_target_checked(&event){draft.permissions.write_variables.insert(id);}else{draft.permissions.write_variables.remove(&id);}
                })/>"Écriture"</label>
            </p>}
        }).collect_view())}
        <h4>"Portes accessibles"</h4>
        {move ||document(state).map(|source|source.objects.into_iter().filter(|object|matches!(object.kind,ObjectKind::Door{..})).map(|object|{
            let id=object.id;view!{<label><input type="checkbox" prop:checked=move ||fields.draft.get().permissions.doors.contains(&id) on:change=move |event|fields.draft.update(|draft|{
                if event_target_checked(&event){draft.permissions.doors.insert(id);}else{draft.permissions.doors.remove(&id);}
            })/>{object.name}</label>}
        }).collect_view())}
    }
}
