//! Project/module forms; the native workspace remains the only writable authority.
use super::{StudioCommand, StudioResponse};
use forge_core::{CommandId, ModuleId, ProjectId, canonical_json_sha256};
use forge_project::{
    authoring::workspace::{WorkspaceHostRequest as HostRequest, WorkspaceHostState},
    workspace::session::{WorkspaceAction, WorkspaceRequest},
};
use leptos::prelude::*;
mod adoption;
mod creation;
mod history;
mod recovery;

#[component]
pub fn ManagedTabs(
    state: RwSignal<Option<forge_project::authoring::AuthoringState>>,
    busy: RwSignal<bool>,
    on_open: Callback<forge_core::AreaId>,
) -> impl IntoView {
    view! { <nav class="managed-zone-tabs" aria-label="Zones ouvertes">{move || state.get().map(|s| {
        let active = s.document.map(|d| d.area_id);
        let playing = s.playtest_hash.is_some();
        s.managed_tabs.into_iter().map(|tab| view! {
            <button disabled=move || busy.get() || playing || active == Some(tab.area_id)
                aria-current=if active == Some(tab.area_id) { "page" } else { "false" }
                on:click=move |_| on_open.run(tab.area_id)>{format!("{}{}", tab.name, if tab.dirty { " *" } else { "" })}</button>
        }).collect_view()
    })}</nav> }
}

#[component]
pub fn WorkspaceControls(
    on_open: Callback<forge_core::AreaId>,
    host_busy: RwSignal<bool>,
    refresh: RwSignal<u64>,
    on_saved: Callback<()>,
) -> impl IntoView {
    let state = RwSignal::new(None::<WorkspaceHostState>);
    let busy = RwSignal::new(false);
    let preview = RwSignal::new(None::<WorkspaceRequest>);
    let status = RwSignal::new("Projets et modules · ouvrir ou créer un workspace".to_owned());
    let send = sender(state, busy, preview, status, on_saved);
    connect_refresh(refresh, host_busy, busy, send);
    view! {
        <details class="scene-navigation">
            <summary>"Projets et modules"</summary>
            <fieldset disabled=move || busy.get() || host_busy.get()>
                <button on:click=move |_| send.run(HostRequest::Create)>"Créer le workspace"</button>
                <button on:click=move |_| send.run(HostRequest::Open)>"Ouvrir le workspace"</button>
                <button on:click=move |_| send.run(HostRequest::Inspect)>"Actualiser"</button>
                <recovery::RecoveryControls state=state send=send/>
                <history::HistoryControls state=state send=send/>
                <WorkspaceForms state=state preview=preview status=status send=send/>
                <ReviewedApply preview=preview send=send adoption=false/>
                <details>
                    <summary>"Créer une zone dans un module"</summary>
                    <creation::CreationControls state=state status=status send=send/>
                </details>
                <details>
                    <summary>"Adopter une zone existante"</summary>
                    <adoption::AdoptionControls state=state preview=preview status=status send=send/>
                    <ReviewedApply preview=preview send=send adoption=true/>
                </details>
                <button disabled=move || state.get().is_none_or(|s| s.head.is_none())
                    on:click=move |_| send.run(HostRequest::Save)>"Sauvegarder le workspace"</button>
                <button on:click=move |_| send.run(HostRequest::Close)>"Fermer le workspace"</button>
            </fieldset>
            <p role="status">{move || status.get()}</p>
            <p>{move || if state.get().is_some_and(|s| s.dirty) { "Modifications non sauvegardées" } else { "" }}</p>
            <ul>{move || state.get().and_then(|s| s.head).map(|head| head.projects.into_iter().map(|project| {
                let modules = project.modules.iter().map(|m| m.name.clone()).collect::<Vec<_>>().join(", ");
                let zones = project.modules.into_iter().flat_map(|m| m.zones.into_iter().map(move |z| (m.name.clone(), z.area_id))).enumerate().map(|(index, (module, area))| {
                    view! { <button title=area.to_string() disabled=move || busy.get() || host_busy.get()
                        on:click=move |_| on_open.run(area)>{format!("Ouvrir {module} · zone {}", index + 1)}</button> }
                }).collect_view();
                view! { <li>{format!("{} — modules : {}", project.name, modules)}{zones}</li> }
            }).collect_view())}</ul>
        </details>
    }
}

#[component]
fn ReviewedApply(
    preview: RwSignal<Option<WorkspaceRequest>>,
    send: Callback<HostRequest>,
    adoption: bool,
) -> impl IntoView {
    view! {
        <Show when=move || preview.get().is_some_and(|r|
            matches!(r.action, WorkspaceAction::AttachZone { .. }) == adoption)>
            <p>{move || preview.get().map(|r| review_label(&r.action))}</p>
            <button on:click=move |_| {
                if let Some(request) = preview.get_untracked() {
                    let request = Box::new(request);
                    send.run(if adoption { HostRequest::Adopt { request } } else { HostRequest::Apply { request } });
                }
            }>"Appliquer la proposition examinée"</button>
        </Show>
    }
}

fn sender(
    state: RwSignal<Option<WorkspaceHostState>>,
    busy: RwSignal<bool>,
    preview: RwSignal<Option<WorkspaceRequest>>,
    status: RwSignal<String>,
    on_saved: Callback<()>,
) -> Callback<HostRequest> {
    Callback::new(move |request: HostRequest| {
        if busy.get_untracked() {
            return;
        }
        let reviewing = matches!(
            request,
            HostRequest::Preview { .. } | HostRequest::ReviewAdoption { .. }
        );
        let saving = matches!(request, HostRequest::Save);
        busy.set(true);
        preview.set(None);
        state.update(|s| {
            if let Some(s) = s {
                s.recovery = None;
                s.receipt = None;
                s.history = None;
            }
        });
        wasm_bindgen_futures::spawn_local(async move {
            let response = super::web::invoke_tauri(&StudioCommand::Workspace {
                request: Box::new(request),
            })
            .await;
            match response {
                Ok(StudioResponse::Workspace { state: result }) => {
                    if reviewing {
                        if let Some(receipt) = &result.receipt {
                            status.set(review_label(&receipt.request.action));
                            preview.set(Some(receipt.request.clone()));
                        }
                    } else {
                        status.set("État du workspace relu depuis le service natif".into());
                    }
                    state.set(Some(*result));
                    if saving {
                        on_saved.run(());
                    }
                }
                Ok(StudioResponse::Rejected { reason }) | Err(reason) => status.set(reason),
                Ok(_) => status.set("Réponse workspace inattendue".into()),
            }
            busy.set(false);
        });
    })
}

fn connect_refresh(
    refresh: RwSignal<u64>,
    host_busy: RwSignal<bool>,
    busy: RwSignal<bool>,
    send: Callback<HostRequest>,
) {
    let observed = RwSignal::new(None::<u64>);
    Effect::new(move |_| {
        let generation = refresh.get();
        if !host_busy.get() && !busy.get() && observed.get_untracked() != Some(generation) {
            observed.set(Some(generation));
            send.run(HostRequest::Inspect);
        }
    });
}

#[component]
fn WorkspaceForms(
    state: RwSignal<Option<WorkspaceHostState>>,
    preview: RwSignal<Option<WorkspaceRequest>>,
    status: RwSignal<String>,
    send: Callback<HostRequest>,
) -> impl IntoView {
    let project_key = RwSignal::new("mon-projet".to_owned());
    let project_name = RwSignal::new("Mon projet".to_owned());
    let module_key = RwSignal::new("module-1".to_owned());
    let module_name = RwSignal::new("Nouveau module".to_owned());
    let selected = RwSignal::new(String::new());
    let examine = move |action: WorkspaceAction| {
        let result = state
            .get_untracked()
            .and_then(|s| s.head)
            .ok_or("Ouvrir un workspace d’abord".to_owned())
            .and_then(|head| {
                let hash = head.content_hash().map_err(|e| e.to_string())?;
                let key = canonical_json_sha256(&(hash, &action)).map_err(|e| e.to_string())?;
                Ok(WorkspaceRequest {
                    command_id: CommandId::deterministic(key.to_string()),
                    base_hash: hash,
                    base_revision: head.revision,
                    action,
                })
            });
        match result {
            Ok(request) => send.run(HostRequest::Preview {
                request: Box::new(request),
            }),
            Err(e) => status.set(e),
        }
    };
    view! {
        <fieldset disabled=move || state.get().is_none_or(|s| s.head.is_none())
            on:input=move |_| preview.set(None) on:change=move |_| preview.set(None)>
            <legend>"Nouveau projet"</legend>
            <label>"Clé stable du projet"<input prop:value=move || project_key.get() on:input=move |e| project_key.set(event_target_value(&e))/></label>
            <label>"Nom du projet"<input prop:value=move || project_name.get() on:input=move |e| project_name.set(event_target_value(&e))/></label>
            <button on:click=move |_| examine(WorkspaceAction::CreateProject { id: ProjectId::deterministic(project_key.get_untracked()), name: project_name.get_untracked() })>"Examiner le projet"</button>
            <label>"Projet du nouveau module"<select prop:value=move || selected.get() on:change=move |e| selected.set(event_target_value(&e))>
                <option value="">"Choisir un projet"</option>
                {move || state.get().and_then(|s| s.head).map(|head| head.projects.into_iter().map(|p| view! { <option value=p.id.to_string()>{p.name}</option> }).collect_view())}
            </select></label>
            <label>"Clé stable du module"<input prop:value=move || module_key.get() on:input=move |e| module_key.set(event_target_value(&e))/></label>
            <label>"Nom du module"<input prop:value=move || module_name.get() on:input=move |e| module_name.set(event_target_value(&e))/></label>
            <button on:click=move |_| { match selected.get_untracked().parse::<ProjectId>() {
                Ok(project_id) => examine(WorkspaceAction::CreateModule { project_id, id: ModuleId::deterministic(format!("{project_id}/{}", module_key.get_untracked())), name: module_name.get_untracked() }),
                Err(_) => status.set("Choisir un projet pour le module".into()),
            } }>"Examiner le module"</button>
            <button on:click=move |_| examine(WorkspaceAction::Undo)>"Examiner Annuler"</button>
            <button on:click=move |_| examine(WorkspaceAction::Redo)>"Examiner Rétablir"</button>
        </fieldset>
    }
}

fn review_label(action: &WorkspaceAction) -> String {
    let description = match action {
        WorkspaceAction::CreateProject { name, .. } => format!("Créer le projet « {name} »"),
        WorkspaceAction::CreateModule { name, .. } => {
            format!("Créer le module « {name} » dans le projet choisi")
        }
        WorkspaceAction::Undo => "Annuler la dernière modification du workspace".into(),
        WorkspaceAction::Redo => "Rétablir la modification annulée du workspace".into(),
        WorkspaceAction::AttachZone { reference, .. } => format!(
            "Adopter la zone {} sans modifier sa source historique (archive {})",
            reference.area_id, reference.archive_hash
        ),
        WorkspaceAction::UpdateZone { .. } => "Modifier une référence de zone".into(),
    };
    format!("Proposition non appliquée : {description}. Sauvegarde nécessaire après application.")
}
