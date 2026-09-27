use super::{HostRequest, StudioCommand, StudioResponse, WorkspaceHostState, WorkspaceRequest};
use forge_project::authoring::{AuthoringRequest, SavedZone};
use leptos::prelude::*;

#[component]
pub fn AdoptionControls(
    state: RwSignal<Option<WorkspaceHostState>>,
    preview: RwSignal<Option<WorkspaceRequest>>,
    status: RwSignal<String>,
    send: Callback<HostRequest>,
) -> impl IntoView {
    let zones = RwSignal::new(Vec::<SavedZone>::new());
    let zone_id = RwSignal::new(String::new());
    let module_id = RwSignal::new(String::new());
    let loading = RwSignal::new(false);
    view! {
        <fieldset disabled=move || loading.get() || state.get().is_none_or(|s| s.head.is_none())
            on:change=move |_| preview.set(None)>
            <legend>"Adopter une zone sauvegardée"</legend>
            <p>"Fermez la zone avant l’examen. Son projet doit correspondre à celui du module. La source historique sera conservée."</p>
            <button on:click=move |_| {
                loading.set(true); preview.set(None);
                wasm_bindgen_futures::spawn_local(async move {
                    match super::super::web::invoke_tauri(&StudioCommand::Authoring { request: Box::new(AuthoringRequest::ListZones) }).await {
                        Ok(StudioResponse::Authoring { state }) => { zones.set(state.catalog.map_or_else(Vec::new, |c| c.zones)); zone_id.set(String::new()); }
                        Ok(StudioResponse::Rejected { reason }) | Err(reason) => status.set(reason),
                        Ok(_) => status.set("Réponse de catalogue inattendue".into()),
                    }
                    loading.set(false);
                });
            }>"Lister les zones sauvegardées"</button>
            <label>"Zone source"<select prop:value=move || zone_id.get() on:change=move |e| zone_id.set(event_target_value(&e))>
                <option value="">"Choisir une zone"</option>
                {move || zones.get().into_iter().filter(|z| !z.pending_recovery).map(|z| view! { <option value=z.area_id.to_string()>{z.name}</option> }).collect_view()}
            </select></label>
            <label>"Module destinataire"<select prop:value=move || module_id.get() on:change=move |e| module_id.set(event_target_value(&e))>
                <option value="">"Choisir un module"</option>
                {move || state.get().and_then(|s| s.head).map(|head| head.projects.into_iter().flat_map(|p| p.modules.into_iter().map(move |m| view! { <option value=m.id.to_string()>{format!("{} / {}", p.name, m.name)}</option> })).collect_view())}
            </select></label>
            <button on:click=move |_| {
                let selected_zone = zones.get_untracked().into_iter().find(|z| z.area_id.to_string() == zone_id.get_untracked());
                let selected_module = state.get_untracked().and_then(|s| s.head).and_then(|head| head.projects.into_iter().find_map(|p| p.modules.iter().find(|m| m.id.to_string() == module_id.get_untracked()).map(|m| (p.id, m.id))));
                if let (Some(zone), Some((project_id, module_id))) = (selected_zone, selected_module) {
                    send.run(HostRequest::ReviewAdoption { area_id: zone.area_id, project_id, module_id, expected_archive_hash: zone.saved_hash });
                } else { status.set("Choisir une zone et un module".into()); }
            }>"Examiner l’adoption"</button>
        </fieldset>
    }
}
