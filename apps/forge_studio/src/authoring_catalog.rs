//! Saved-zone chooser; listing never creates a project or replaces the active session.
use forge_core::AreaId;
use forge_project::authoring::{AuthoringRequest, UnavailableZoneReason, ZoneCatalog};
use leptos::prelude::*;

mod recovery;

#[component]
pub(super) fn ZoneChooser(on_open: Callback<AreaId>) -> impl IntoView {
    let catalog = RwSignal::new(ZoneCatalog::default());
    let selected = RwSignal::new(String::new());
    let loading = RwSignal::new(false);
    let status = RwSignal::new(
        "La liste décrit les sauvegardes, pas les retouches non sauvegardées.".to_owned(),
    );
    let refresh = move |()| {
        if loading.get_untracked() {
            return;
        }
        loading.set(true);
        selected.set(String::new());
        catalog.set(ZoneCatalog::default());
        wasm_bindgen_futures::spawn_local(async move {
            let result = super::web::invoke_tauri(&super::StudioCommand::Authoring {
                request: Box::new(AuthoringRequest::ListZones),
            })
            .await;
            match result {
                Ok(super::StudioResponse::Authoring { state }) => {
                    if let Some(list) = state.catalog {
                        status.set(format!("{} zone(s) sauvegardée(s) · {} archive(s) indisponible(s). Fermer la zone courante avant d'en ouvrir une autre.", list.zones.len(), list.unavailable.len()));
                        catalog.set(list);
                    } else {
                        status.set("Catalogue absent de la réponse native".into());
                    }
                }
                Ok(super::StudioResponse::Rejected { reason }) | Err(reason) => status.set(reason),
                Ok(_) => status.set("Réponse de catalogue inattendue".into()),
            }
            loading.set(false);
        });
    };
    Effect::new(move |_| refresh(()));
    view! {
        <fieldset><legend>"Zones sauvegardées"</legend>
            <button disabled=move || loading.get() on:click=move |_| refresh(())>"Actualiser la liste"</button>
            <label>"Choisir une zone"<select disabled=move || loading.get() prop:value=move || selected.get()
                on:change=move |ev| selected.set(event_target_value(&ev))>
                <option value="">"Sélectionner…"</option>
                {move || catalog.get().zones.into_iter().map(|zone| {
                    let id = zone.area_id.to_string();
                    let warning = if zone.pending_recovery { " · sauvegarde interrompue à examiner" } else { "" };
                    let label = format!("{}{}", zone.name, warning);
                    view! { <option value=id>{label}</option> }
                }).collect_view()}
                {move || catalog.get().unavailable.into_iter().map(|zone| {
                    let id = zone.area_id.to_string();
                    let label = format!("{} · {}", id, issue_label(zone.reason));
                    view! { <option value=id>{label}</option> }
                }).collect_view()}
            </select></label>
            <button disabled=move || loading.get() || selected.get().is_empty()
                on:click=move |_| {
                    if let Ok(id) = selected.get_untracked().parse::<AreaId>() { on_open.run(id); }
                }>"Ouvrir la zone choisie"</button>
            <recovery::RecoveryControls selected=selected/>
            <output aria-live="polite">{move || status.get()}</output>
            <Show when=move || !catalog.get().unavailable.is_empty()>
                <details><summary>"Zones indisponibles : causes"</summary>
                    <ul>{move || catalog.get().unavailable.into_iter().map(|zone| {
                        view! { <li>{format!("{} : {}", zone.area_id, issue_label(zone.reason))}</li> }
                    }).collect_view()}</ul>
                </details>
            </Show>
        </fieldset>
    }
}

fn issue_label(reason: UnavailableZoneReason) -> &'static str {
    match reason {
        UnavailableZoneReason::MissingArchive => "aucune sauvegarde terminée",
        UnavailableZoneReason::InterruptedSave => {
            "première sauvegarde interrompue ; récupération explicite nécessaire"
        }
        UnavailableZoneReason::InvalidArchive => {
            "archive invalide ; préserver le fichier et examiner une sauvegarde"
        }
        UnavailableZoneReason::IdentityMismatch => "identité du document différente du dossier",
        UnavailableZoneReason::UnsafePath => {
            "lien, chemin hors workspace ou fichier non régulier refusé"
        }
        UnavailableZoneReason::TooLarge => "archive au-delà de la limite de lecture",
        UnavailableZoneReason::Unreadable => {
            "accès impossible ; vérifier le répertoire et les droits"
        }
    }
}
