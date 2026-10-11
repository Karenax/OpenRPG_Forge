//! One module selector, then the maps belonging to that module.
use super::{StudioCommand, StudioResponse};
use forge_project::authoring::{
    AuthoringState,
    workspace::{WorkspaceHostRequest, WorkspaceHostState},
};
use leptos::prelude::*;

#[component]
#[allow(
    clippy::too_many_lines,
    reason = "One mounted navigation component retains module and native session selection"
)]
pub fn ModuleBrowser(
    authoring: RwSignal<Option<AuthoringState>>,
    host_busy: RwSignal<bool>,
    preview_pending: Signal<bool>,
    refresh: RwSignal<u64>,
    on_open: Callback<forge_core::AreaId>,
    on_close: Callback<()>,
) -> impl IntoView {
    let state = RwSignal::new(None::<WorkspaceHostState>);
    let busy = RwSignal::new(false);
    let selected = RwSignal::new(String::new());
    let error = RwSignal::new(String::new());
    let map_select = NodeRef::<leptos::html::Select>::new();
    let observed = RwSignal::new(None::<u64>);
    let load = Callback::new(move |()| {
        if busy.get_untracked() || host_busy.get_untracked() {
            return;
        }
        busy.set(true);
        error.set(String::new());
        wasm_bindgen_futures::spawn_local(async move {
            let response = super::web::invoke_tauri(&StudioCommand::Workspace {
                request: Box::new(WorkspaceHostRequest::Browse),
            })
            .await;
            match response {
                Ok(StudioResponse::Workspace { state: result }) => state.set(Some(*result)),
                Ok(StudioResponse::Rejected { reason }) | Err(reason) => error.set(reason),
                Ok(_) => error.set("La liste des modules n’a pas pu être chargée.".into()),
            }
            busy.set(false);
        });
    });
    Effect::new(move |_| {
        let generation = refresh.get();
        if !host_busy.get() && !busy.get() && observed.get_untracked() != Some(generation) {
            observed.set(Some(generation));
            load.run(());
        }
    });
    // Retain the selected module on refresh. A sole module needs no extra click.
    Effect::new(move |_| {
        let Some(loaded) = state.get() else { return };
        let active = authoring.get().and_then(|s| s.document).map(|d| d.area_id);
        let current = selected.get_untracked();
        if loaded
            .modules
            .iter()
            .any(|module| module.id.to_string() == current)
        {
            return;
        }
        let module = loaded
            .modules
            .iter()
            .find(|module| {
                active.is_some_and(|area| module.maps.iter().any(|map| map.area_id == area))
            })
            .or_else(|| (loaded.modules.len() == 1).then(|| &loaded.modules[0]));
        selected.set(module.map_or_else(String::new, |module| module.id.to_string()));
    });
    let maps = Memo::new(move |_| {
        state
            .get()
            .and_then(|loaded| {
                loaded
                    .modules
                    .into_iter()
                    .find(|module| module.id.to_string() == selected.get())
            })
            .map(|module| module.maps)
            .unwrap_or_default()
    });
    let active_map = move || {
        authoring
            .get()
            .and_then(|s| s.document)
            .map(|d| d.area_id)
            .filter(|area| maps.get().iter().any(|map| map.area_id == *area))
            .map_or_else(String::new, |area| area.to_string())
    };
    let disabled = move || {
        busy.get()
            || host_busy.get()
            || preview_pending.get()
            || authoring.get().is_some_and(|s| s.playtest_hash.is_some())
    };
    view! {
        <div class="module-map-browser" aria-busy=move || busy.get().to_string()>
            <label>"Module"
                <select aria-label="Module" disabled=disabled prop:value=move || selected.get()
                    on:change=move |event| selected.set(event_target_value(&event))>
                    <option value="">"Choisir un module…"</option>
                    {move || state.get().map(|loaded| {
                        loaded.modules.iter().map(|module| {
                            let duplicate = loaded.modules.iter().filter(|other| other.name == module.name).count() > 1;
                            let label = if duplicate { format!("{} · {}", module.name, module.project_name) } else { module.name.clone() };
                            view! { <option value=module.id.to_string()>{label}</option> }
                        }).collect_view()
                    })}
                </select>
            </label>
            <label>"Carte"
                <select node_ref=map_select aria-label="Carte" disabled=move || disabled() || maps.get().is_empty()
                    prop:value=active_map on:change=move |event| {
                        let value = event_target_value(&event);
                        // Display the accepted map until its native opening succeeds.
                        if let Some(select) = map_select.get() { select.set_value(&active_map()); }
                        if let Some(map) = maps.get_untracked().into_iter().find(|map| map.area_id.to_string() == value) {
                            on_open.run(map.area_id);
                        }
                    }>
                    <option value="">"Choisir une carte…"</option>
                    {move || maps.get().into_iter().map(|map| view! {
                        <option value=map.area_id.to_string()>{format!("{}{}", map.name, if map.dirty { " *" } else { "" })}</option>
                    }).collect_view()}
                </select>
            </label>
            <Show when=move || !selected.get().is_empty() && !busy.get() && maps.get().is_empty()>
                <p>"Ce module ne contient aucune carte."</p>
            </Show>
            <Show when=move || busy.get() && state.get().is_none()><p role="status">"Chargement des modules…"</p></Show>
            <Show when=move || preview_pending.get()><p>"Appliquez ou annulez l’aperçu avant de changer de carte."</p></Show>
            <Show when=move || authoring.get().is_some_and(|s| s.playtest_hash.is_some())><p>"Revenez à l’édition pour choisir une autre carte."</p></Show>
            <Show when=move || !error.get().is_empty()>
                <p role="alert">{move || error.get()}</p>
                <button disabled=disabled on:click=move |_| load.run(())>"Réessayer"</button>
            </Show>
            <Show when=move || authoring.get().is_some_and(|s| s.document.is_some())>
                <button class="module-map-close" disabled=disabled
                    on:click=move |_| on_close.run(())>"Fermer la carte"</button>
            </Show>
        </div>
    }
}
