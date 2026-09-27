use super::{HostRequest, WorkspaceAction, WorkspaceHostState};
use forge_project::{
    TerrainDocument,
    authoring::creation::{NewTerrainSettings, prepare_module_terrain},
};
use leptos::prelude::*;

#[component]
pub(super) fn CreationControls(
    state: RwSignal<Option<WorkspaceHostState>>,
    status: RwSignal<String>,
    send: Callback<HostRequest>,
) -> impl IntoView {
    let module = RwSignal::new(String::new());
    let key = RwSignal::new("zone-1".to_owned());
    let name = RwSignal::new("Nouvelle zone".to_owned());
    let terrain_seed = RwSignal::new("0".to_owned());
    let elevation = RwSignal::new("0".to_owned());
    let amplitude = RwSignal::new("0".to_owned());
    let wavelength = RwSignal::new("100".to_owned());
    let draft = RwSignal::new(None::<TerrainDocument>);
    let accepted = move || {
        state.get().and_then(|s| s.receipt).filter(|r| {
            draft.get().is_some_and(|d| {
                matches!(&r.request.action, WorkspaceAction::AttachZone {
            project_id, reference, ..
        } if *project_id == d.project_id && reference.area_id == d.area_id)
            })
        })
    };
    view! {
        <fieldset disabled=move || state.get().is_none_or(|s| s.head.is_none())
            on:input=move |_| draft.set(None) on:change=move |_| draft.set(None)>
            <legend>"Nouvelle zone dans un module"</legend>
            <p>"Fermer les zones ouvertes. Terrain 512 × 512 m, pas 2 m ; aucun contenu de démonstration ajouté. Sauvegarde du workspace nécessaire après création."</p>
            <label>"Module"<select prop:value=move || module.get() on:change=move |e| module.set(event_target_value(&e))>
                <option value="">"Choisir un module"</option>
                {move || state.get().and_then(|s| s.head).map(|h| h.projects.into_iter().flat_map(|p| p.modules.into_iter().map(move |m| view! { <option value=m.id.to_string()>{format!("{} / {}", p.name, m.name)}</option> })).collect_view())}
            </select></label>
            <Field label="Clé stable de zone" value=key/>
            <Field label="Nom de zone" value=name/>
            <Field label="Seed" value=terrain_seed/>
            <Field label="Altitude (m)" value=elevation/>
            <Field label="Amplitude (m)" value=amplitude/>
            <Field label="Échelle du relief (m)" value=wavelength/>
            <button on:click=move |_| {
                draft.set(None);
                let selected = state.get_untracked().and_then(|s| s.head).and_then(|h| h.projects.into_iter().find_map(|p| p.modules.iter().find(|m| m.id.to_string() == module.get_untracked()).map(|m| (p.id, m.id))));
                let result = selected.ok_or("Choisir un module".to_owned()).and_then(|(p, m)| {
                    let number = |s: String| s.trim().parse::<f32>().map_err(|_| "Valeur numérique invalide".to_owned());
                    let settings = NewTerrainSettings {
                        seed: terrain_seed.get_untracked().trim().parse().map_err(|_| "Seed entière positive ou nulle requise".to_owned())?,
                        elevation_m: number(elevation.get_untracked())?, amplitude_m: number(amplitude.get_untracked())?, wavelength_m: number(wavelength.get_untracked())?,
                    };
                    prepare_module_terrain(p, m, &key.get_untracked(), &name.get_untracked(), settings).map(|d| (m, d))
                });
                match result {
                    Ok((module_id, document)) => { draft.set(Some(document.clone())); send.run(HostRequest::ReviewNewZone { module_id, document: Box::new(document) }); }
                    Err(e) => status.set(e),
                }
            }>"Examiner la zone du module"</button>
            <p>{move || accepted().and_then(|_| draft.get()).map(|d| format!("Proposition : {} · seed {} · altitude {} m · amplitude {} m. Résumé seulement, pas un aperçu 3D.", d.name, d.seed, d.height_sampling.base_elevation_m, d.height_sampling.amplitude_m))}</p>
            <button disabled=move || accepted().is_none() on:click=move |_| {
                if let (Some(receipt), Some(document)) = (accepted(), draft.get_untracked()) {
                    draft.set(None);
                    send.run(HostRequest::CreateNewZone { request: Box::new(receipt.request), document: Box::new(document) });
                }
            }>"Créer cette zone dans le module"</button>
            <button on:click=move |_| draft.set(None)>"Abandonner la proposition"</button>
        </fieldset>
    }
}

#[component]
fn Field(label: &'static str, value: RwSignal<String>) -> impl IntoView {
    view! { <label>{label}<input prop:value=move || value.get() on:input=move |e| value.set(event_target_value(&e))/></label> }
}
