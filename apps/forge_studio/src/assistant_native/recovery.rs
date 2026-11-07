//! Trusted UI recovery; neither provider IO nor source mutation occurs here.
use super::{AssistantCoordinator, AssistantView, StoredJob};
use crate::{assistant_job_native::recovery_page_ids, assistant_protocol::JobRecoveryPage};
use forge_core::{CommandId, ContentHash};
use forge_project::{
    assistant::{jobs::JobState, provider::ProviderRequest},
    authoring::host::AuthoringHost,
};

impl AssistantCoordinator {
    pub(super) fn reopen_loaded_job(
        &mut self,
        host: &AuthoringHost,
        id: CommandId,
        request: &ProviderRequest,
    ) -> Result<AssistantView, String> {
        let entry = self.jobs.get_mut(&id).ok_or("Job absent")?;
        entry.store.verify_request(request)?;
        let restored = entry.store.snapshot()?.state == JobState::Applied
            && host.assistant_receipt(id)?.is_none();
        if restored {
            entry.store.restore_unpersisted_application(request)?;
            entry.review = None;
        }
        self.restored_view(id, restored)
    }

    pub(super) fn restored_view(
        &self,
        id: CommandId,
        restored: bool,
    ) -> Result<AssistantView, String> {
        let mut view = self.inspect(id)?;
        if restored {
            view.message = "La retouche précédente n’a pas été retrouvée dans la carte sauvegardée. La proposition est conservée ; relire le diff avant de l’appliquer à nouveau.".into();
        }
        Ok(view)
    }

    pub fn inspect_recoveries(
        &self,
        offset: usize,
        current_job: Option<CommandId>,
    ) -> Result<AssistantView, String> {
        let (ids, next_offset) = recovery_page_ids(&self.root, offset)?;
        let mut jobs = Vec::new();
        for id in ids {
            if self.active.contains_key(&id) {
                continue;
            }
            let pending = if let Some(entry) = self.jobs.get(&id) {
                entry.store.recovery()?
            } else {
                StoredJob::pending_recovery(&self.root, id)?
            };
            if let Some(pending) = pending {
                jobs.push(pending);
            }
        }
        let mut view = if let Some(id) = current_job.filter(|id| self.jobs.contains_key(id)) {
            self.inspect(id)?
        } else {
            self.configuration_view()
        };
        view.message = if jobs.is_empty() {
            "Aucun enregistrement interrompu dans cette page."
        } else {
            "Examiner les deux versions avant de choisir celle à récupérer."
        }
        .into();
        view.recovery_page = Some(JobRecoveryPage {
            offset,
            next_offset,
            jobs,
        });
        Ok(view)
    }

    pub fn recover_job(
        &mut self,
        id: CommandId,
        expected_saved: Option<ContentHash>,
        expected_pending: ContentHash,
        promote: bool,
    ) -> Result<AssistantView, String> {
        if self.active.contains_key(&id) {
            return Err("Attendre la fin de l’appel actif avant de récupérer son journal.".into());
        }
        if let Some(entry) = self.jobs.get_mut(&id) {
            entry
                .store
                .recover_pending(expected_saved, expected_pending, promote)?;
        } else {
            drop(StoredJob::recover_interrupted(
                &self.root,
                id,
                expected_saved,
                expected_pending,
                promote,
            )?);
        }
        // Reopening through Prepare must bind the request and source again.
        // No previous review or approval remains reusable after this decision.
        self.jobs.remove(&id);
        let mut view = self.configuration_view();
        view.recovered_job = Some(id);
        view.message = "Journal récupéré. Reprendre le brief et la sélection, puis préparer les données ; aucune retouche du projet n’a été appliquée.".into();
        Ok(view)
    }
}
