//! Read-only journal viewer. Mutation controls still use the shared authoring bus.
use crate::audit_labels::audit_label;
use forge_project::authoring::{
    AuthoringRequest, AuthoringState,
    history::{HistoryAction, HistoryChange, HistoryEntry, HistoryPage, PAGE_SIZE},
};
use leptos::prelude::*;
mod zone;

#[component]
pub(super) fn HistoryControls(state: RwSignal<Option<AuthoringState>>) -> impl IntoView {
    let expanded = RwSignal::new(false);
    let offset = RwSignal::new(0usize);
    let page = RwSignal::new(None::<HistoryPage>);
    let status = RwSignal::new(String::new());
    let busy = RwSignal::new(false);
    let epoch = RwSignal::new(0u64);
    // Memo prevents a preview/status-only response from reloading the journal.
    let source_hash = Memo::new(move |_| {
        state.get().and_then(|s| match s.zone_head {
            Some(head) => head.content_hash().ok(),
            None => s.document.and_then(|d| d.content_hash().ok()),
        })
    });
    Effect::new(move |_| {
        let _ = source_hash.get();
        offset.set(0);
    });
    Effect::new(move |_| {
        let visible = expanded.get();
        let source = source_hash.get();
        let start = offset.get();
        let request_epoch = epoch.get_untracked().wrapping_add(1);
        epoch.set(request_epoch);
        page.set(None);
        busy.set(false);
        if !visible {
            return;
        }
        let Some(expected_hash) = source else {
            status.set("Ouvrir une zone pour consulter son historique.".into());
            return;
        };
        busy.set(true);
        status.set("Lecture du journal…".into());
        wasm_bindgen_futures::spawn_local(async move {
            let result = crate::web::invoke_tauri(&crate::StudioCommand::Authoring {
                request: Box::new(AuthoringRequest::ReadHistory {
                    expected_hash,
                    offset: start,
                }),
            })
            .await;
            if epoch.get_untracked() != request_epoch {
                return;
            }
            match result {
                Ok(crate::StudioResponse::Authoring { state }) => {
                    if let Some(history) = state.history {
                        status.set(format!("{} commande(s) · les plus récentes d’abord. Annuler/Rétablir sont aussi enregistrés.", history.total));
                        page.set(Some(history));
                    } else {
                        status.set("Historique absent de la réponse native".into());
                    }
                }
                Ok(crate::StudioResponse::Rejected { reason }) | Err(reason) => status.set(reason),
                Ok(_) => status.set("Réponse d’historique inattendue".into()),
            }
            busy.set(false);
        });
    });
    view! {
        <section class="zone-history" aria-label="Historique des retouches">
            <button aria-expanded=move || expanded.get().to_string()
                on:click=move |_| expanded.update(|v| *v = !*v)>
                {move || if expanded.get() { "Masquer l’historique" } else { "Afficher l’historique des retouches" }}
            </button>
            <Show when=move || expanded.get()>
                <output aria-live="polite">{move || status.get()}</output>
                <p>{move || if state.get().is_some_and(|s| s.dirty) { "La session contient des retouches non sauvegardées." } else { "Aucune retouche non sauvegardée dans la session." }}</p>
                <Show when=move || state.get().is_some_and(|s| s.zone_head.is_some())>
                    <p>"Commandes depuis la conversion en zone unifiée. L’historique antérieur reste conservé dans le checkpoint, non listé ici. Les valeurs héritées proviennent de la recette ; aucun masque complet n’est transféré dans cette liste."</p>
                </Show>
                <ol>{move || page.get().map(|p| p.entries.into_iter().map(|entry| view! {
                    <li><p>{label(&entry)}</p><p>{audit_label(&entry.audit)}</p><details><summary>"Identité de la commande"</summary>
                        <p style="overflow-wrap:anywhere">{format!("{} · {}", entry.command_id, entry.resulting_hash)}</p>
                        <pre style="white-space:pre-wrap;overflow-wrap:anywhere">{serde_json::to_string_pretty(&entry.change).unwrap_or_default()}</pre>
                    </details></li>
                }).collect_view())}</ol>
                <button disabled=move || busy.get() || offset.get() == 0
                    on:click=move |_| offset.update(|v| *v = v.saturating_sub(PAGE_SIZE))>"Plus récentes"</button>
                <button disabled=move || busy.get() || page.get().is_none_or(|p| p.offset + p.entries.len() >= p.total)
                    on:click=move |_| offset.update(|v| *v += PAGE_SIZE)>"Plus anciennes"</button>
            </Show>
        </section>
    }
}

fn label(entry: &HistoryEntry) -> String {
    let action = match entry.action {
        HistoryAction::Edit => "Retouche",
        HistoryAction::Regenerate => "Régénération",
        HistoryAction::Undo => "Annulation",
        HistoryAction::Redo => "Rétablissement",
    };
    let change = match &entry.change {
        HistoryChange::Zone { changes } => changes
            .iter()
            .map(zone::label)
            .collect::<Vec<_>>()
            .join(" ; "),
        HistoryChange::SchemaVersion { before, after } => {
            format!("format terrain : v{before} → v{after}")
        }
        HistoryChange::MaterialSamples { before, after } => {
            format!("matériaux corrigés : {before} → {after} cases")
        }
        HistoryChange::Rename { before, after } => format!("nom : {before} → {after}"),
        HistoryChange::BaseElevation { before, after } => {
            format!("altitude de base : {before} → {after} m")
        }
        HistoryChange::HeightSamples { before, after } => {
            format!("relief : {before} → {after} échantillons dans la recette")
        }
    };
    format!("{:?} · {action} · {change}", entry.revision)
}
