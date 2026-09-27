//! Recovery is an explicit choice after native validation, never a fallback on Open.
use super::{HostRequest, WorkspaceHostState};
use forge_project::authoring::workspace::WorkspaceRecoveryVersion;
use leptos::prelude::*;

#[component]
pub(super) fn RecoveryControls(
    state: RwSignal<Option<WorkspaceHostState>>,
    send: Callback<HostRequest>,
) -> impl IntoView {
    view! {
        <details>
            <summary>"Récupérer une sauvegarde interrompue"</summary>
            <p>"Fermer les zones et le workspace. Examiner les versions avant de choisir ; les deux fichiers sont archivés avant récupération."</p>
            <button disabled=move || state.get().is_some_and(|s| s.head.is_some())
                on:click=move |_| send.run(HostRequest::ReviewRecovery)>"Examiner la récupération"</button>
            {move || state.get().and_then(|s| s.recovery).map(|review| {
                let saved = review.saved.as_ref().map_or_else(|| "Aucune version sauvegardée".to_owned(), label);
                let pending = label(&review.pending);
                let expected_saved = review.saved.as_ref().map(|v| v.archive_hash);
                let expected_pending = review.pending.archive_hash;
                let valid = review.pending.head.is_some();
                let saved_valid = review.saved.as_ref().is_some_and(|v| v.head.is_some());
                view! {
                    <p>{format!("Version sauvegardée : {saved}")}</p>
                    <p>{format!("Sauvegarde interrompue : {pending}")}</p>
                    <button disabled=!valid on:click=move |_| send.run(HostRequest::Recover {
                        expected_saved, expected_pending, promote: true,
                    })>"Récupérer la version interrompue"</button>
                    <button on:click=move |_| send.run(HostRequest::Recover {
                        expected_saved, expected_pending, promote: false,
                    })>"Conserver la version sauvegardée et archiver la tentative"</button>
                    <p>"Après récupération, ouvrir le workspace explicitement."</p>
                    <p>{if saved_valid { "" } else { "Attention : conserver la version sauvegardée ne répare pas son absence ou sa corruption." }}</p>
                }
            })}
        </details>
    }
}

fn label(version: &WorkspaceRecoveryVersion) -> String {
    if let Some(head) = &version.head {
        let names = head
            .projects
            .iter()
            .map(|p| p.name.as_str())
            .collect::<Vec<_>>()
            .join(", ");
        let modules = head.projects.iter().map(|p| p.modules.len()).sum::<usize>();
        let zones = head
            .projects
            .iter()
            .flat_map(|p| &p.modules)
            .map(|m| m.zones.len())
            .sum::<usize>();
        format!(
            "révision {} · projets : {names} · {modules} modules · {zones} zones · archives vérifiées",
            head.revision.get()
        )
    } else {
        format!(
            "invalide — {}",
            version
                .error
                .as_deref()
                .unwrap_or("validation indisponible")
        )
    }
}
