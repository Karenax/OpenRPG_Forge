use forge_core::AreaId;
use forge_project::authoring::{AuthoringRequest, RecoveryReview, RecoveryVersion};
use leptos::prelude::*;

#[component]
pub(super) fn RecoveryControls(selected: RwSignal<String>) -> impl IntoView {
    let review = RwSignal::new(None::<RecoveryReview>);
    let busy = RwSignal::new(false);
    let confirmed = RwSignal::new(false);
    let status = RwSignal::new(String::new());
    Effect::new(move |_| {
        let _ = selected.get();
        review.set(None);
        confirmed.set(false);
        status.set(String::new());
    });
    let execute = move |request: AuthoringRequest| {
        if busy.get_untracked() {
            return;
        }
        let selection = selected.get_untracked();
        let inspecting = matches!(request, AuthoringRequest::InspectRecovery { .. });
        busy.set(true);
        review.set(None);
        confirmed.set(false);
        wasm_bindgen_futures::spawn_local(async move {
            let result = crate::web::invoke_tauri(&crate::StudioCommand::Authoring {
                request: Box::new(request),
            })
            .await;
            if selected.get_untracked() == selection {
                match result {
                    Ok(crate::StudioResponse::Authoring { state }) if inspecting => {
                        status.set("Examiner les deux versions avant de confirmer un choix.".into());
                        review.set(state.recovery);
                    }
                    Ok(crate::StudioResponse::Authoring { .. }) => status.set(
                        "Choix appliqué. Les archives recovery-<hash>.json conservent les versions. Actualiser la liste puis ouvrir la zone.".into()),
                    Ok(crate::StudioResponse::Rejected { reason }) | Err(reason) => status.set(reason),
                    Ok(_) => status.set("Réponse de récupération inattendue".into()),
                }
            }
            busy.set(false);
        });
    };
    let choose = move |promote| {
        if !confirmed.get_untracked() {
            return;
        }
        if let Some(r) = review.get_untracked() {
            execute(AuthoringRequest::Recover {
                area_id: r.area_id,
                expected_saved: r.saved.map(|v| v.archive_hash),
                expected_pending: r.pending.archive_hash,
                promote,
            });
        }
    };
    view! {
        <details><summary>"Récupérer une sauvegarde interrompue"</summary>
            <p>"Fermer la zone courante. Aucun choix n’est automatique ; les deux fichiers sont archivés avant modification."</p>
            <button disabled=move || busy.get() || selected.get().is_empty()
                on:click=move |_| {
                    if let Ok(area_id) = selected.get_untracked().parse::<AreaId>() {
                        execute(AuthoringRequest::InspectRecovery { area_id });
                    }
                }>"Examiner les versions de la zone choisie"</button>
            <Show when=move || review.get().is_some()>
                <pre style="white-space:pre-wrap;overflow-wrap:anywhere">{move || review.get().map(|r| format!(
                    "Sauvegarde terminée :\n{}\n\nCandidat interrompu :\n{}",
                    r.saved.as_ref().map_or_else(|| "Aucune (première sauvegarde)".into(), summary), summary(&r.pending)
                )).unwrap_or_default()}</pre>
                <label><input type="checkbox" prop:checked=move || confirmed.get()
                    on:change=move |ev| confirmed.set(event_target_checked(&ev))/>
                    "J’ai examiné ces versions et confirme le choix du bouton suivant."</label>
                <button disabled=move || busy.get() || !confirmed.get() || review.get().is_none_or(|r| r.pending.document.is_none())
                    on:click=move |_| choose(true)>"Récupérer le candidat valide"</button>
                <button disabled=move || busy.get() || !confirmed.get() || review.get().is_none()
                    on:click=move |_| choose(false)>"Conserver la sauvegarde terminée et archiver le candidat"</button>
            </Show>
            <output aria-live="polite">{move || status.get()}</output>
        </details>
    }
}

fn summary(version: &RecoveryVersion) -> String {
    let details = version.document.as_ref().map_or_else(
        || {
            format!(
                "Invalide : {}",
                version.error.as_deref().unwrap_or("document absent")
            )
        },
        |d| {
            format!(
                "{} · révision {:?} · projet {}\nAltitude de base : {} m · {} échantillons retouchés",
                d.name, d.revision, d.project_id, d.height_sampling.base_elevation_m, d.height_deltas.len()
            )
        },
    );
    format!("{details}\nSHA-256 archive : {}", version.archive_hash)
}
