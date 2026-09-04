//! Source-management UI. No renderer is mounted and no browser-only backend is simulated.

use crate::workspace_api::{WorkspaceCommand, WorkspaceState};
use crate::{StudioCommand, StudioResponse};
use forge_core::{AreaId, ModuleId};
use forge_project::WorkspaceDocument;
#[path = "workspace_authoring_ui.rs"]
mod authoring;
use authoring::{AddSourcesForms, EditMetadataForm};
use leptos::prelude::*;
use wasm_bindgen::{JsCast, JsValue};

#[derive(Clone, Copy)]
pub(crate) struct WorkspaceActions {
    pub(crate) state: ReadSignal<WorkspaceState>,
    set_state: WriteSignal<WorkspaceState>,
    pub(crate) pending: ReadSignal<bool>,
    set_pending: WriteSignal<bool>,
    error: ReadSignal<Option<String>>,
    set_error: WriteSignal<Option<String>>,
    backend_available: bool,
}

impl WorkspaceActions {
    pub(crate) fn unavailable(self) -> bool {
        !self.backend_available || self.pending.get()
    }

    pub(crate) fn send(self, command: WorkspaceCommand) {
        if !self.backend_available || self.pending.get_untracked() {
            return;
        }
        self.set_pending.set(true);
        self.set_error.set(None);
        wasm_bindgen_futures::spawn_local(async move {
            match invoke_workspace(command).await {
                Ok(state) => self.set_state.set(state),
                Err(error) => self.set_error.set(Some(error)),
            }
            self.set_pending.set(false);
        });
    }
}

#[derive(Clone, Copy, Eq, PartialEq)]
enum WorkspaceNode {
    Project,
    Module(ModuleId),
    Area(ModuleId, AreaId),
}

pub(crate) fn mount_project_workspace() {
    mount_to_body(|| view! { <ProjectWorkspace/> });
}

/// One presentation handle to the canonical native session, shared by menus and forms.
pub(crate) fn create_workspace_actions() -> WorkspaceActions {
    let backend_available = tauri_core().is_ok();
    let (state, set_state) = signal(WorkspaceState {
        revision: 0,
        document_json: None,
        can_undo: false,
        can_redo: false,
        saved_path: None,
        dirty: false,
        last_receipt_json: None,
        message: if backend_available {
            "Lecture de la session Studio…".to_owned()
        } else {
            "Mode navigateur : aucun backend Tauri connecté.".to_owned()
        },
    });
    let (pending, set_pending) = signal(false);
    let (error, set_error) = signal(None::<String>);
    let actions = WorkspaceActions {
        state,
        set_state,
        pending,
        set_pending,
        error,
        set_error,
        backend_available,
    };
    if backend_available {
        actions.send(WorkspaceCommand::Current);
    }
    actions
}

#[component]
pub(crate) fn ProjectWorkspace(
    #[prop(optional)] actions: Option<WorkspaceActions>,
    #[prop(default = false)] embedded: bool,
    #[prop(optional)] on_open_area: Option<Callback<crate::area_tabs::AreaKey>>,
) -> impl IntoView {
    let actions = actions.unwrap_or_else(create_workspace_actions);
    let WorkspaceActions {
        state,
        pending,
        backend_available,
        ..
    } = actions;
    let (selected, set_selected) = signal(WorkspaceNode::Project);
    Effect::new(move |_| {
        let valid = read_document(actions).is_some_and(|document| match selected.get() {
            WorkspaceNode::Project => true,
            WorkspaceNode::Module(id) => document.module(id).is_some(),
            WorkspaceNode::Area(module, area) => document.area(module, area).is_some(),
        });
        if !valid {
            set_selected.set(WorkspaceNode::Project);
        }
    });

    view! {
        <main class="project-workspace" data-forge-mode="project-sources" aria-busy=move || pending.get().to_string()>
            <header class="topbar project-topbar" hidden=embedded>
                <div>
                    <span class="eyebrow">"OPENRPG FORGE · STUDIO"</span>
                    <h1>"Projets et zones"</h1>
                    <p>"Préparez les sources de votre aventure."</p>
                </div>
                <nav class="project-proof-links" aria-label="Scènes de démonstration séparées">
                    <a href="?forge_scene=gate0">"Scène Gate 0"</a>
                    <a href="?forge_scene=visual-valley">"Visual Valley"</a>
                    <small>"Scènes de preuve séparées, sans lien avec ce brouillon."</small>
                </nav>
            </header>
            <section class="project-notice" aria-label="Portée de cette version">
                <strong>"Gestion des sources uniquement"</strong>
                <span>{if embedded { "Sélectionnez une zone puis Ouvrir dans Monde pour afficher son terrain. Les brosses 3D viennent ensuite." } else { "Cet assistant gère les sources. L’aperçu du projet est disponible dans Monde, depuis le bureau Forge." }}</span>
            </section>
            <Show when=move || !backend_available>
                <section class="project-notice project-warning" role="status" id="workspace-backend-unavailable">
                    <strong>"Ouvrez cette page dans le Studio Tauri pour créer un projet."</strong>
                    <span>"Les formulaires restent consultables dans le navigateur. Création, annulation, sauvegarde, ouverture et fermeture sont désactivées : aucun fichier ni projet fictif ne sera créé."</span>
                </section>
            </Show>
            <div class="project-layout">
                <aside class="panel project-tree-panel" aria-label="Arborescence du projet">
                    <ProjectTree actions selected set_selected/>
                    {on_open_area.map(|callback| view! {
                        <button type="button" class="open-world-button"
                            disabled=move || !matches!(selected.get(), WorkspaceNode::Area(..))
                            on:click=move |_| {
                                if let WorkspaceNode::Area(module_id, area_id) = selected.get_untracked()
                                    && let Some(document) = read_document(actions) {
                                    callback.run(crate::area_tabs::AreaKey { project_id: document.project.id, module_id, area_id });
                                }
                            }>"Ouvrir dans Monde"</button>
                    })}
                </aside>
                <div class="project-content">
                    <WorkspaceToolbar actions/>
                    <div class="project-status" role="status" aria-live="polite" id="workspace-status">
                        {move || if pending.get() { "Opération en cours…".to_owned() } else { state.get().message }}
                    </div>
                    {move || actions.error.get().map(|message| view! {
                        <div class="project-error" role="alert" id="workspace-error">{message}</div>
                    })}
                    <div class="project-main-grid">
                        <div hidden=move || state.get().document_json.is_some()><CreateProjectForm actions/></div>
                        <div hidden=move || state.get().document_json.is_none()>
                            <AddSourcesForms actions selected/>
                        </div>
                        <div>
                            <WorkspaceMetadata actions selected/>
                            <EditMetadataForm actions selected/>
                        </div>
                    </div>
                    <StorageForms actions/>
                </div>
            </div>
            <pre id="workspace-state-ledger" hidden>{move || {
                serde_json::to_string(&state.get()).unwrap_or_else(|error| {
                    serde_json::json!({ "serialization_error": error.to_string() }).to_string()
                })
            }}</pre>
        </main>
    }
}

#[component]
fn WorkspaceToolbar(actions: WorkspaceActions) -> impl IntoView {
    view! {
        <div class="project-toolbar">
            <div class="project-save-state" id="workspace-save-status">
                {move || {
                    let state = actions.state.get();
                    if state.document_json.is_none() {
                        "Aucun projet ouvert".to_owned()
                    } else if state.dirty || state.saved_path.is_none() {
                        "Brouillon · non enregistré".to_owned()
                    } else {
                        "Document enregistré".to_owned()
                    }
                }}
            </div>
            <div class="project-button-row">
                <button id="workspace-undo" type="button" disabled=move || actions.unavailable() || !actions.state.get().can_undo
                    on:click=move |_| actions.send(WorkspaceCommand::Undo {
                        base_revision: actions.state.get_untracked().revision,
                    })>"Annuler"</button>
                <button id="workspace-redo" type="button" disabled=move || actions.unavailable() || !actions.state.get().can_redo
                    on:click=move |_| actions.send(WorkspaceCommand::Redo {
                        base_revision: actions.state.get_untracked().revision,
                    })>"Rétablir"</button>
                <button id="workspace-close" type="button" disabled=move || actions.unavailable() || actions.state.get().document_json.is_none() || actions.state.get().dirty
                    on:click=move |_| actions.send(WorkspaceCommand::Close)>"Fermer le projet"</button>
            </div>
        </div>
    }
}

#[component]
fn TextField(
    id: &'static str,
    label: &'static str,
    value: ReadSignal<String>,
    set_value: WriteSignal<String>,
    #[prop(default = "")] placeholder: &'static str,
) -> impl IntoView {
    view! {
        <label class="project-field" for=id>
            <span>{label}</span>
            <input id=id name=id type="text" required placeholder=placeholder
                prop:value=move || value.get()
                on:input=move |event| set_value.set(event_target_value(&event))/>
        </label>
    }
}

#[component]
fn CreateProjectForm(actions: WorkspaceActions) -> impl IntoView {
    let (project_name, set_project_name) = signal(String::new());
    let (module_name, set_module_name) = signal(String::new());
    let (area_name, set_area_name) = signal(String::new());
    let (seed, set_seed) = signal("1".to_owned());
    let (spawn_x, set_spawn_x) = signal("0".to_owned());
    let (spawn_y, set_spawn_y) = signal("2".to_owned());
    let (spawn_z, set_spawn_z) = signal("0".to_owned());

    view! {
        <section class="project-card" aria-labelledby="workspace-create-heading">
            <h2 id="workspace-create-heading">"Nouveau projet"</h2>
            <p class="hint">"Un projet, son premier module et une zone extérieure. Les valeurs sont validées par le backend Rust."</p>
            <form on:submit=move |event| {
                event.prevent_default();
                if actions.state.get_untracked().document_json.is_some() {
                    return;
                }
                actions.send(WorkspaceCommand::Create {
                    project_name: project_name.get_untracked(),
                    module_name: module_name.get_untracked(),
                    area_name: area_name.get_untracked(),
                    seed: seed.get_untracked(),
                    spawn_x: spawn_x.get_untracked(),
                    spawn_y: spawn_y.get_untracked(),
                    spawn_z: spawn_z.get_untracked(),
                });
            }>
                <fieldset disabled=move || actions.pending.get()>
                    <legend class="project-sr-only">"Identité du nouveau projet"</legend>
                    <TextField id="workspace-project-name" label="Nom du projet" value=project_name set_value=set_project_name placeholder="Mon aventure"/>
                    <TextField id="workspace-module-name" label="Premier module" value=module_name set_value=set_module_name placeholder="Le premier chapitre"/>
                    <TextField id="workspace-area-name" label="Zone extérieure" value=area_name set_value=set_area_name placeholder="La clairière"/>
                    <TextField id="workspace-seed" label="Seed du terrain" value=seed set_value=set_seed/>
                    <p class="hint">"Zone de 512 × 512 m. Point de départ en mètres · axe Y vertical. Ces coordonnées sont des métadonnées ; aucun personnage n’est placé dans cette tranche."</p>
                    <div class="project-spawn-fields">
                        <TextField id="workspace-spawn-x" label="Position X" value=spawn_x set_value=set_spawn_x/>
                        <TextField id="workspace-spawn-y" label="Position Y" value=spawn_y set_value=set_spawn_y/>
                        <TextField id="workspace-spawn-z" label="Position Z" value=spawn_z set_value=set_spawn_z/>
                    </div>
                    <Show when=move || actions.state.get().document_json.is_some()>
                        <p class="hint">"Un projet est déjà ouvert. Fermez le projet enregistré ou annulez le brouillon avant une nouvelle création. Aucun fichier enregistré n’est supprimé."</p>
                    </Show>
                    <button id="workspace-create" class="project-primary" type="submit"
                        disabled=move || actions.unavailable() || actions.state.get().document_json.is_some()>
                        "Créer le brouillon"
                    </button>
                    <p class="hint">"La création seule n’écrit aucun fichier. Utilisez ensuite Enregistrer."</p>
                </fieldset>
            </form>
        </section>
    }
}

#[component]
fn StorageForms(actions: WorkspaceActions) -> impl IntoView {
    let (directory, set_directory) = signal(String::new());
    let (path, set_path) = signal(String::new());
    view! {
        <section class="project-card" aria-labelledby="workspace-files-heading">
            <h2 id="workspace-files-heading">"Enregistrer ou ouvrir"</h2>
            <p class="hint">"Les chemins sont saisis explicitement. Aucun accès fichier n’a lieu avant votre action."</p>
            <div class="project-storage-grid">
                <form on:submit=move |event| {
                    event.prevent_default();
                    actions.send(WorkspaceCommand::Save {
                        directory: directory.get_untracked(),
                        base_revision: actions.state.get_untracked().revision,
                    });
                }>
                    <TextField id="workspace-save-directory" label="Dossier existant de destination (chemin absolu)" value=directory set_value=set_directory placeholder="E:\\Mes projets Forge"/>
                    <p class="hint">"Enregistre un fichier identifiant-révision.forge.json. Aucun contenu existant n’est écrasé ; choisissez un autre dossier en cas de conflit."</p>
                    <button id="workspace-save" type="submit" disabled=move || actions.unavailable() || actions.state.get().document_json.is_none()>"Enregistrer"</button>
                </form>
                <form on:submit=move |event| {
                    event.prevent_default();
                    if actions.state.get_untracked().dirty {
                        return;
                    }
                    actions.send(WorkspaceCommand::Open { path: path.get_untracked() });
                }>
                    <TextField id="workspace-open-path" label="Fichier projet à ouvrir (chemin absolu exact)" value=path set_value=set_path placeholder="E:\\Mes projets Forge\\…forge.json"/>
                    <Show when=move || actions.state.get().dirty>
                        <p class="hint">"Enregistrez ou annulez le brouillon avant d’ouvrir un fichier."</p>
                    </Show>
                    <button id="workspace-open" type="submit" disabled=move || actions.unavailable() || actions.state.get().dirty>"Ouvrir"</button>
                </form>
            </div>
            <p class="project-file-path" id="workspace-saved-path">{move || {
                actions.state.get().saved_path.map_or_else(
                    || "Aucun emplacement enregistré.".to_owned(),
                    |path| format!("Dernier emplacement : {path}"),
                )
            }}</p>
        </section>
    }
}

fn read_document(actions: WorkspaceActions) -> Option<WorkspaceDocument> {
    actions
        .state
        .get()
        .document_json
        .as_deref()
        .and_then(|json| WorkspaceDocument::from_json(json).ok())
}

#[component]
fn ProjectTree(
    actions: WorkspaceActions,
    selected: ReadSignal<WorkspaceNode>,
    set_selected: WriteSignal<WorkspaceNode>,
) -> impl IntoView {
    view! {
        <h2>"Arborescence"</h2>
        {move || read_document(actions).map_or_else(
            || view! { <p class="hint">"Aucun projet. Créez un brouillon ou ouvrez un fichier enregistré."</p> }.into_any(),
            |document| view! {
                <ul class="project-tree" aria-label="Projet, module et zone">
                    <li>
                        <button type="button" class:selected=move || selected.get() == WorkspaceNode::Project
                            aria-pressed=move || (selected.get() == WorkspaceNode::Project).to_string()
                            on:click=move |_| set_selected.set(WorkspaceNode::Project)>
                            <small>"PROJET"</small><span>{document.project.name.clone()}</span>
                        </button>
                        <ul>{document.project.modules.iter().map(|module| {
                            let module_id = module.id;
                            let module_node = WorkspaceNode::Module(module_id);
                            view! { <li>
                                <button type="button" class:selected=move || selected.get() == module_node
                                    aria-pressed=move || (selected.get() == module_node).to_string()
                                    on:click=move |_| set_selected.set(module_node)>
                                    <small>"MODULE"</small><span>{module.name.clone()}</span>
                                </button>
                                <ul>{module.areas.iter().map(|area| {
                                    let area_node = WorkspaceNode::Area(module_id, area.id);
                                    view! { <li>
                                        <button type="button" class:selected=move || selected.get() == area_node
                                            aria-pressed=move || (selected.get() == area_node).to_string()
                                            on:click=move |_| set_selected.set(area_node)>
                                            <small>"ZONE EXTÉRIEURE"</small><span>{area.name.clone()}</span>
                                        </button>
                                    </li> }
                                }).collect_view()}</ul>
                                {module.areas.is_empty().then(|| view! { <p class="hint">"Aucune zone"</p> })}
                            </li> }
                        }).collect_view()}</ul>
                    </li>
                </ul>
            }.into_any(),
        )}
    }
}

#[component]
fn WorkspaceMetadata(
    actions: WorkspaceActions,
    selected: ReadSignal<WorkspaceNode>,
) -> impl IntoView {
    view! {
        <section class="project-card" aria-labelledby="workspace-metadata-heading">
            <h2 id="workspace-metadata-heading">"Métadonnées du document"</h2>
            <p class="hint">"Lecture des sources acceptées par le backend, distincte du formulaire de création."</p>
            {move || read_document(actions).map_or_else(
                || view! { <p class="project-empty">"Le projet créé apparaîtra ici avec son module, sa zone et son point de départ."</p> }.into_any(),
                |document| {
                    let (kind, name, id, area) = match selected.get() {
                        WorkspaceNode::Module(id) => document.module(id).map(|module| ("Module", module.name.clone(), id.to_string(), None)),
                        WorkspaceNode::Area(module, id) => document.area(module, id).map(|area| ("Zone extérieure", area.name.clone(), id.to_string(), Some(area.clone()))),
                        WorkspaceNode::Project => Some(("Projet", document.project.name.clone(), document.project.id.to_string(), None)),
                    }.unwrap_or(("Projet", document.project.name.clone(), document.project.id.to_string(), None));
                    view! {
                        <dl class="project-metadata" id="workspace-document-metadata">
                            <dt>"Sélection"</dt><dd>{kind}</dd>
                            <dt>"Nom"</dt><dd>{name}</dd>
                            <dt>"Identifiant"</dt><dd class="project-identifier">{id}</dd>
                            <dt>"Révision"</dt><dd>{document.revision.get().to_string()}</dd>
                            {area.map(|area| view! {
                                <dt>"Seed de la zone"</dt><dd>{area.terrain.seed.to_string()}</dd>
                                <dt>"Point de départ · X / Y / Z"</dt><dd>{format!("{} / {} / {} m", area.spawn.x, area.spawn.y, area.spawn.z)}</dd>
                            })}
                            <dt>"Modules / zones"</dt><dd>{format!("{} / {}", document.project.modules.len(), document.project.modules.iter().map(|m| m.areas.len()).sum::<usize>())}</dd>
                        </dl>
                    }.into_any()
                },
            )}
        </section>
    }
}

fn tauri_core() -> Result<JsValue, String> {
    let window = web_sys::window().ok_or_else(|| "Fenêtre navigateur absente.".to_owned())?;
    let tauri = js_sys::Reflect::get(window.as_ref(), &JsValue::from_str("__TAURI__"))
        .map_err(|error| format!("API Tauri inaccessible : {error:?}"))?;
    if tauri.is_null() || tauri.is_undefined() {
        return Err(
            "Mode navigateur : backend Tauri absent ; aucune opération effectuée.".to_owned(),
        );
    }
    let core = js_sys::Reflect::get(&tauri, &JsValue::from_str("core"))
        .map_err(|error| format!("API Tauri core inaccessible : {error:?}"))?;
    let invoke = js_sys::Reflect::get(&core, &JsValue::from_str("invoke"))
        .map_err(|error| format!("API Tauri invoke inaccessible : {error:?}"))?;
    if !invoke.is_function() {
        return Err("Backend Tauri indisponible : API invoke absente.".to_owned());
    }
    Ok(core)
}

async fn invoke_workspace(command: WorkspaceCommand) -> Result<WorkspaceState, String> {
    match invoke_studio(StudioCommand::Workspace { request: command }).await? {
        StudioResponse::Workspace { state } => {
            if let Some(json) = state.document_json.as_deref() {
                WorkspaceDocument::from_json(json)
                    .map_err(|error| format!("Document retourné invalide : {error}"))?;
            }
            Ok(state)
        }
        StudioResponse::Rejected { reason } => Err(reason),
        _ => Err("Réponse reçue pour une autre opération Studio.".to_owned()),
    }
}

pub(crate) async fn invoke_studio(command: StudioCommand) -> Result<StudioResponse, String> {
    let core = tauri_core()?;
    let invoke = js_sys::Reflect::get(&core, &JsValue::from_str("invoke"))
        .map_err(|error| format!("Commande Tauri inaccessible : {error:?}"))?
        .dyn_into::<js_sys::Function>()
        .map_err(|_| "API Tauri invoke invalide.".to_owned())?;
    let payload = serde_wasm_bindgen::to_value(&command)
        .map_err(|error| format!("Commande non sérialisable : {error}"))?;
    let arguments = js_sys::Object::new();
    js_sys::Reflect::set(&arguments, &JsValue::from_str("command"), &payload)
        .map_err(|error| format!("Arguments Tauri invalides : {error:?}"))?;
    let result = invoke
        .call2(
            &core,
            &JsValue::from_str("execute_studio_command"),
            arguments.as_ref(),
        )
        .map_err(|error| format!("Invocation Tauri impossible : {error:?}"))?;
    let promise = result
        .dyn_into::<js_sys::Promise>()
        .map_err(|_| "Tauri n’a pas retourné de Promise.".to_owned())?;
    let value = wasm_bindgen_futures::JsFuture::from(promise)
        .await
        .map_err(|error| format!("Commande refusée ou échouée : {error:?}"))?;
    serde_wasm_bindgen::from_value(value)
        .map_err(|error| format!("Réponse Studio invalide : {error}"))
}
