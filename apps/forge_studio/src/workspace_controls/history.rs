use super::{HostRequest, WorkspaceAction, WorkspaceHostState};
use crate::audit_labels::audit_label;
use leptos::prelude::*;

#[component]
pub(super) fn HistoryControls(
    state: RwSignal<Option<WorkspaceHostState>>,
    send: Callback<HostRequest>,
) -> impl IntoView {
    view! {
        <details class="workspace-history">
            <summary>"Historique du workspace"</summary>
            <p>"Opérations acceptées, plus récentes en premier. Une lecture ne sauvegarde ni ne modifie le projet."</p>
            <button disabled=move || state.get().is_none_or(|s| s.head.is_none()) on:click=move |_| {
                if let Some(hash) = state.get_untracked().and_then(|s| s.head).and_then(|h| h.content_hash().ok()) {
                    send.run(HostRequest::ReadHistory { expected_hash: hash, offset: 0 });
                }
            }>"Lire les dernières opérations"</button>
            {move || state.get().and_then(|s| s.history).map(|page| {
                let next = page.next_offset;
                let hash = page.head_hash;
                let previous = page.offset.saturating_sub(forge_project::workspace::history::PAGE_SIZE);
                let at_start = page.offset == 0;
                let range = if page.entries.is_empty() { "Aucune opération".to_owned() }
                    else { format!("Opérations {}–{} sur {}", page.offset + 1, page.offset + page.entries.len(), page.total) };
                view! {
                    <p>{range}</p>
                    <ul>{page.entries.into_iter().map(|entry| view! {
                        <li title=entry.command_id.to_string()>
                            <p>{format!("Révision {} — {}{}", entry.revision.get(), label(&entry.action), entry.scope.map_or_else(String::new, |s| format!(" · {s}")))}</p>
                            <p data-testid="workspace-history-audit-stamp">{audit_label(&entry.audit)}</p>
                        </li>
                    }).collect_view()}</ul>
                    <button disabled=at_start on:click=move |_| send.run(HostRequest::ReadHistory { expected_hash: hash, offset: previous })>"Plus récentes"</button>
                    <button disabled=next.is_none() on:click=move |_| { if let Some(offset) = next { send.run(HostRequest::ReadHistory { expected_hash: hash, offset }); } }>"Plus anciennes"</button>
                }
            })}
        </details>
    }
}

fn label(action: &WorkspaceAction) -> String {
    match action {
        WorkspaceAction::CreateProject { name, .. } => format!("Création du projet « {name} »"),
        WorkspaceAction::CreateModule { name, .. } => format!("Création du module « {name} »"),
        WorkspaceAction::AttachZone { .. } => {
            "Ajout d’une zone au module (création ou adoption)".into()
        }
        WorkspaceAction::UpdateZone { before, after, .. } => {
            if before.head_hash == after.head_hash {
                "Archivage de zone, contenu inchangé".into()
            } else {
                "Modification de zone — détails dans son historique".into()
            }
        }
        WorkspaceAction::Undo => "Annulation de la dernière opération du workspace".into(),
        WorkspaceAction::Redo => "Rétablissement d’une opération du workspace".into(),
    }
}
