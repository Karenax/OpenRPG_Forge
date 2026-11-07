//! Guided creation through the existing reviewed workspace commands.
use super::{StudioCommand, StudioResponse};
use forge_core::{CommandId, ModuleId, ProjectId};
use forge_project::authoring::{
    AuthoringState,
    workspace::{WorkspaceHostRequest, WorkspaceHostState},
};
use forge_project::{
    authoring::creation::{NewTerrainSettings, prepare_module_terrain},
    workspace::session::{WorkspaceAction, WorkspaceRequest},
};
use leptos::prelude::*;
mod game;
mod game_export;
mod game_export_form;
mod game_form;
mod game_visuals;
mod module_import;

#[derive(Clone, Copy)]
struct Creation {
    state: RwSignal<Option<WorkspaceHostState>>,
    busy: RwSignal<bool>,
    error: RwSignal<String>,
    pending: RwSignal<Option<WorkspaceHostRequest>>,
    summary: RwSignal<String>,
    summary_details: RwSignal<Vec<String>>,
}

impl Creation {
    fn send(self, request: WorkspaceHostRequest, document: Option<forge_project::TerrainDocument>) {
        if self.busy.get_untracked() {
            return;
        }
        self.busy.set(true);
        self.error.set(String::new());
        self.pending.set(None);
        let reviewing = matches!(
            request,
            WorkspaceHostRequest::Preview { .. } | WorkspaceHostRequest::ReviewNewZone { .. }
        );
        let saving = matches!(request, WorkspaceHostRequest::Save);
        wasm_bindgen_futures::spawn_local(async move {
            let result = crate::web::invoke_tauri(&StudioCommand::Workspace {
                request: Box::new(request),
            })
            .await;
            match result {
                Ok(StudioResponse::Workspace { state }) => {
                    if reviewing && let Some(receipt) = &state.receipt {
                        let describe = matches!(
                            receipt.request.action,
                            WorkspaceAction::Undo
                                | WorkspaceAction::Redo
                                | WorkspaceAction::ConfigureGame { .. }
                        );
                        let details = if describe {
                            game_form::describe_workspace_change(
                                &receipt.before,
                                &receipt.after,
                                &state.modules,
                            )
                        } else {
                            Vec::new()
                        };
                        if describe && details.is_empty() {
                            self.error.set("Impossible de décrire cette opération de l’espace de travail. Aucune modification appliquée.".into());
                        } else {
                            self.summary_details.set(details);
                            self.pending.set(Some(if let Some(document) = document {
                                WorkspaceHostRequest::CreateNewZone {
                                    request: Box::new(receipt.request.clone()),
                                    document: Box::new(document),
                                }
                            } else {
                                WorkspaceHostRequest::Apply {
                                    request: Box::new(receipt.request.clone()),
                                }
                            }));
                        }
                    }
                    self.state.set(Some(*state));
                }
                Ok(StudioResponse::Rejected { reason }) | Err(reason) => {
                    self.error.set(if saving {
                        format!("Sauvegarde du projet impossible : {reason}")
                    } else if reason == "workspace has nothing to undo or redo" {
                        "Aucune opération à annuler ou à rétablir dans l’espace de travail.".into()
                    } else {
                        reason
                    });
                }
                Ok(_) => self
                    .error
                    .set("L’espace de travail n’a pas reçu de réponse valide.".into()),
            }
            self.busy.set(false);
        });
    }

    fn review(self, action: WorkspaceAction, summary: String) {
        let result = self
            .state
            .get_untracked()
            .and_then(|state| state.head)
            .ok_or("Créez d’abord l’espace de travail.".to_owned())
            .and_then(|head| {
                Ok(WorkspaceRequest {
                    command_id: CommandId::deterministic(format!(
                        "studio-create/{action:?}/{}",
                        head.content_hash().map_err(|error| error.to_string())?
                    )),
                    base_revision: head.revision,
                    base_hash: head.content_hash().map_err(|error| error.to_string())?,
                    action,
                })
            });
        match result {
            Ok(request) => {
                self.summary.set(summary);
                self.summary_details.set(Vec::new());
                self.send(
                    WorkspaceHostRequest::Preview {
                        request: Box::new(request),
                    },
                    None,
                );
            }
            Err(error) => self.error.set(error),
        }
    }
}

#[component]
pub(super) fn CreateControls(
    state: RwSignal<Option<WorkspaceHostState>>,
    busy: RwSignal<bool>,
    error: RwSignal<String>,
    authoring: RwSignal<Option<AuthoringState>>,
    host_busy: RwSignal<bool>,
    preview_pending: Signal<bool>,
    selected: RwSignal<String>,
) -> impl IntoView {
    let controls = Creation {
        state,
        busy,
        error,
        pending: RwSignal::new(None),
        summary: RwSignal::new(String::new()),
        summary_details: RwSignal::new(Vec::new()),
    };
    let game_draft_dirty = RwSignal::new(false);
    let game_project = RwSignal::new(String::new());
    let project = RwSignal::new(String::new());
    let project_name = RwSignal::new("Mon projet".to_owned());
    let module_name = RwSignal::new("Mon module".to_owned());
    let map_name = RwSignal::new("Ma carte".to_owned());
    let key = StoredValue::new(format!("studio/{}", js_sys::Date::now()));
    let disabled = move || {
        busy.get()
            || host_busy.get()
            || preview_pending.get()
            || authoring
                .get()
                .is_some_and(|state| state.document.is_some())
    };
    let export_blocked = Signal::derive(disabled);
    view! {
        <details class="workspace-create"><summary>"Projet et jeu"</summary>
            <p>"Fermez la carte courante pour modifier l’espace de travail. Examinez et confirmez les changements avant de sauvegarder."</p>
            <fieldset disabled=move || export_blocked.get() on:input=move |_|controls.pending.set(None) on:change=move |_|controls.pending.set(None)>
                <Show when=move ||state.get().is_none_or(|state|state.head.is_none())>
                    <p>"Aucun espace de travail ouvert. Cette action initialise un espace vide si aucun n’existe ; un espace existant n’est jamais remplacé."</p>
                    <button type="button" on:click=move |_|controls.send(WorkspaceHostRequest::Create,None)>"Initialiser l’espace de travail"</button>
                </Show>
                <Show when=move ||state.get().is_some_and(|state|state.head.is_some())>
                  <details><summary>"Créer un projet, un module ou une carte"</summary>
                    <fieldset disabled=move || game_draft_dirty.get()>
                    <label>"Nom du nouveau projet"<input maxlength="256" prop:value=move ||project_name.get() on:input=move |event|project_name.set(event_target_value(&event))/></label>
                    <button type="button" on:click=move |_| controls.review(WorkspaceAction::CreateProject {
                        id: ProjectId::deterministic(format!("{}/{}",key.get_value(),project_name.get_untracked())),name:project_name.get_untracked(),
                    },format!("Créer le projet « {} »",project_name.get_untracked()))>"Examiner le nouveau projet"</button>
                    <label>"Projet du module"<select prop:value=move ||project.get() on:change=move |event|project.set(event_target_value(&event))>
                        <option value="">"Choisir un projet"</option>
                        {move ||state.get().and_then(|state|state.head).map(|head|head.projects.into_iter().map(|project|view!{<option value=project.id.to_string()>{project.name}</option>}).collect_view())}
                    </select></label>
                    <label>"Nom du nouveau module"<input maxlength="256" prop:value=move ||module_name.get() on:input=move |event|module_name.set(event_target_value(&event))/></label>
                    <button type="button" disabled=move ||project.get().is_empty() on:click=move |_| {
                        if let Ok(project_id) = project.get_untracked().parse() {
                            controls.review(WorkspaceAction::CreateModule { project_id,id:ModuleId::deterministic(format!("{project_id}/{}/{}",key.get_value(),module_name.get_untracked())), name:module_name.get_untracked() },format!("Créer le module « {} »",module_name.get_untracked()));
                        }
                    }>"Examiner le nouveau module"</button>
                    <label>"Nom de la nouvelle carte"<input maxlength="256" prop:value=move ||map_name.get() on:input=move |event|map_name.set(event_target_value(&event))/></label>
                    <p>"La carte sera ajoutée au module sélectionné au-dessus : terrain plat de 512 × 512 m, sans contenu de démonstration."</p>
                    <button type="button" disabled=move ||selected.get().is_empty() on:click=move |_| {
                        let module = state.get_untracked().and_then(|state|state.head).and_then(|head|head.projects.into_iter().find_map(|project|project.modules.iter().find(|module|module.id.to_string()==selected.get_untracked()).map(|module|(project.id,module.id))));
                        let result = module.ok_or("Choisir un module.".to_owned()).and_then(|(project,module)|prepare_module_terrain(project,module,&forge_core::AreaId::deterministic(format!("{}/{}",key.get_value(),map_name.get_untracked())).to_string(),&map_name.get_untracked(),NewTerrainSettings::default()).map(|document|(module,document)));
                        match result {
                            Ok((module_id,document)) => {controls.summary.set(format!("Créer la carte « {} » dans le module sélectionné",map_name.get_untracked()));controls.send(WorkspaceHostRequest::ReviewNewZone {module_id,document:Box::new(document.clone())},Some(document));}
                            Err(error) => controls.error.set(error),
                        }
                    }>"Examiner la nouvelle carte"</button>
                    </fieldset>
                  </details>
                  <module_import::ModuleImportControls controls=controls blocked=Signal::derive(move || game_draft_dirty.get() || controls.pending.get().is_some())/>
                  <game::GameControls state=state controls=controls draft_dirty=game_draft_dirty selected=game_project/>
                  <fieldset disabled=move || game_draft_dirty.get() || controls.pending.get().is_some()>
                    <super::history::HistoryControls state=state send=Callback::new(move |request| controls.send(request, None))/>
                    <p>"L’annulation et le rétablissement concernent la dernière opération de tout l’espace de travail."</p>
                    <button type="button" on:click=move |_| controls.review(WorkspaceAction::Undo, "Examiner l’annulation dans l’espace de travail".into())>"Examiner l’annulation de l’espace de travail"</button>
                    <button type="button" on:click=move |_| controls.review(WorkspaceAction::Redo, "Examiner le rétablissement dans l’espace de travail".into())>"Examiner le rétablissement de l’espace de travail"</button>
                  </fieldset>
                </Show>
                <Show when=move ||controls.pending.get().is_some()>
                    <p role="status">{move ||controls.summary.get()}</p>
                    <ul>{move ||controls.summary_details.get().into_iter().map(|item|view!{<li>{item}</li>}).collect_view()}</ul>
                    <button type="button" on:click=move |_| {
                        if let Some(request) = controls.pending.get_untracked() {
                            controls.send(request,None);
                        }
                    }>"Confirmer cette modification"</button>
                    <button type="button" on:click=move |_|controls.pending.set(None)>"Abandonner la proposition"</button>
                </Show>
                <p role="status">{move ||if state.get().is_some_and(|state|state.dirty) {"Changements du projet non sauvegardés."}else{"Aucun changement du projet en attente de sauvegarde."}}</p>
                <Show when=move ||game_draft_dirty.get()><p role="status">"Examinez ou abandonnez les saisies du jeu avant de sauvegarder."</p></Show>
                <button type="button" disabled=move ||game_draft_dirty.get() || controls.pending.get().is_some() || state.get().is_none_or(|state|!state.dirty)
                    on:click=move |_|controls.send(WorkspaceHostRequest::Save,None)>"Sauvegarder le projet et ses cartes"</button>
            </fieldset>
            <game_export::GameExportControls state=state controls=controls selected=game_project blocked=export_blocked/>
        </details>
    }
}
