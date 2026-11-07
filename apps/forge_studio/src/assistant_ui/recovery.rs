//! Recovery of local job journals is separate from approving source changes.
use super::Controls;
use crate::assistant_protocol::{AssistantRequest, JobRecoveryView};
use forge_project::assistant::jobs::{JobSnapshot, JobState};
use leptos::prelude::*;

fn busy(controls: Controls) -> bool {
    controls.working.get() || controls.generating.get() || crate::authoring_native_game::paused()
}

fn inspect(controls: Controls, offset: usize) {
    controls.send(AssistantRequest::InspectRecoveries {
        offset,
        current_job: controls
            .view
            .get_untracked()
            .and_then(|view| view.job)
            .map(|job| job.job_id),
    });
}

#[component]
pub(super) fn RecoveryControls(controls: Controls) -> impl IntoView {
    view! {<details class="assistant-job-recovery">
        <summary>"Récupérer un enregistrement interrompu"</summary>
        <p>"Récupérer un journal n’applique pas sa proposition. Les deux versions seront archivées."</p>
        <button type="button" disabled=move||busy(controls) on:click=move |_|inspect(controls,0)>"Examiner les enregistrements"</button>
        <Show when=move||controls.view.get().is_some_and(|view|view.recovery_page.is_some())>
            <p role="status">{move||controls.view.get().and_then(|view|view.recovery_page).map(|page|if page.jobs.is_empty(){"Aucun enregistrement interrompu dans cette page."}else{"Relire les états et les résultats avant de choisir une version."})}</p>
            <For each=move||controls.view.get().and_then(|view|view.recovery_page).map(|page|page.jobs).unwrap_or_default()
                key=|job| (job.job_id,job.saved_hash,job.pending_hash)
                children=move|job|view!{<RecoveryRow job controls/>}/>
            <button type="button" disabled=move||busy(controls)||controls.view.get().and_then(|view|view.recovery_page).is_none_or(|page|page.offset==0)
                on:click=move |_|if let Some(page)=controls.view.get_untracked().and_then(|view|view.recovery_page){inspect(controls,page.offset.saturating_sub(32));}>"Page précédente"</button>
            <button type="button" disabled=move||busy(controls)||controls.view.get().and_then(|view|view.recovery_page).is_none_or(|page|page.next_offset.is_none())
                on:click=move |_|if let Some(offset)=controls.view.get_untracked().and_then(|view|view.recovery_page).and_then(|page|page.next_offset){inspect(controls,offset);}>"Page suivante"</button>
        </Show>
        <Show when=move||controls.recovery_target.get().is_some()>
            <p>"Reprendre le brief et la sélection de la demande récupérée, puis préparer les données."</p>
            <button type="button" disabled=move||busy(controls) on:click=move |_|controls.recovery_target.set(None)>"Préparer une autre demande"</button>
        </Show>
    </details>}
}

#[component]
fn RecoveryRow(job: JobRecoveryView, controls: Controls) -> impl IntoView {
    let reviewed = RwSignal::new(false);
    let promote = job.clone();
    let keep = job.clone();
    let diagnostics = serde_json::to_string_pretty(&job).unwrap_or_default();
    view! {<section class="assistant-recovery-entry" aria-label="Versions d’une demande interrompue">
        <p>{format!("Version enregistrée : {}",describe(job.saved.as_ref(),"Aucune version enregistrée"))}</p>
        <p>{format!("Version interrompue : {}",describe(job.candidate.as_ref(),"Version illisible"))}</p>
        <Show when=move||!job.can_promote><p>"La version interrompue ne peut pas être récupérée automatiquement."</p></Show>
        <label><input type="checkbox" prop:checked=move||reviewed.get() on:change=move|event|reviewed.set(event_target_checked(&event))/>"J’ai relu les deux versions de cette demande"</label>
        <button type="button" disabled=move||busy(controls)||!job.can_promote||!reviewed.get()
            on:click=move |_|controls.send(AssistantRequest::RecoverJob{job_id:promote.job_id,expected_saved:promote.saved_hash,expected_pending:promote.pending_hash,promote:true})>"Récupérer la version interrompue"</button>
        <button type="button" disabled=move||busy(controls)||!job.can_keep_saved||!reviewed.get()
            on:click=move |_|controls.send(AssistantRequest::RecoverJob{job_id:keep.job_id,expected_saved:keep.saved_hash,expected_pending:keep.pending_hash,promote:false})>"Conserver la version enregistrée"</button>
        <details><summary>"Diagnostics de cet enregistrement"</summary><pre style="white-space:pre-wrap;overflow-wrap:anywhere;max-height:18rem;overflow:auto">{diagnostics}</pre></details>
    </section>}
}

fn describe(snapshot: Option<&JobSnapshot>, absent: &str) -> String {
    snapshot.map_or_else(
        || absent.into(),
        |snapshot| {
            let state = match snapshot.state {
                JobState::Prepared => "Préparation conservée",
                JobState::Running => "Appel démarré ; résultat incertain",
                JobState::Interrupted => "Appel interrompu ; réservation conservée",
                JobState::Staged => "Proposition conservée, sans application",
                JobState::Applied => "Application notée dans le journal",
                JobState::Cancelled => "Demande annulée",
                JobState::BudgetExceeded => "Plafond dépassé ; résultat conservé",
            };
            let summary = snapshot.result.as_ref().map_or_else(String::new, |result| {
                format!(" · {}", result.proposal.summary)
            });
            format!("{state} · {} tentative(s){summary}", snapshot.attempts)
        },
    )
}
