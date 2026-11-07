//! Exact-hash recovery of an interrupted journal save, never a source Apply.
use super::{StoredJob, job_directory};
use forge_core::{
    CommandId, ContentHash,
    document_store::{DocumentStore, StoredDocument},
};
use forge_project::assistant::jobs::{JobJournal, JobSnapshot, JobState};
use serde_json::Value;
use std::path::Path;

pub use crate::assistant_protocol::JobRecoveryView;

impl StoredJob {
    pub fn pending_recovery(root: &Path, id: CommandId) -> Result<Option<JobRecoveryView>, String> {
        let store = DocumentStore::open(&job_directory(root, id, false)?)
            .map_err(|error| error.to_string())?;
        inspect(&store, id)
    }

    pub fn recovery(&self) -> Result<Option<JobRecoveryView>, String> {
        inspect(&self.store, self.journal.snapshot()?.job_id)
    }

    pub fn recover_pending(
        &mut self,
        expected_saved: Option<ContentHash>,
        expected_pending: ContentHash,
        promote: bool,
    ) -> Result<JobSnapshot, String> {
        if self.saved_hash != expected_saved {
            return Err("Le journal chargé a changé ; recharger sa récupération.".into());
        }
        let saved = confirm(
            &self.store,
            self.journal.snapshot()?.job_id,
            expected_saved,
            expected_pending,
            promote,
        )?;
        self.journal = JobJournal::from_bytes(&saved.bytes)?;
        self.saved_hash = Some(saved.hash);
        self.snapshot()
    }

    pub fn recover_interrupted(
        root: &Path,
        id: CommandId,
        expected_saved: Option<ContentHash>,
        expected_pending: ContentHash,
        promote: bool,
    ) -> Result<Self, String> {
        let store = DocumentStore::open(&job_directory(root, id, false)?)
            .map_err(|error| error.to_string())?;
        let saved = confirm(&store, id, expected_saved, expected_pending, promote)?;
        Ok(Self {
            journal: JobJournal::from_bytes(&saved.bytes)?,
            saved_hash: Some(saved.hash),
            store,
        })
    }
}

fn parse(document: &StoredDocument, id: CommandId) -> Result<JobJournal, String> {
    let journal = JobJournal::from_bytes(&document.bytes)?;
    if journal.snapshot()?.job_id != id {
        return Err("Le journal ne correspond pas au job sélectionné.".into());
    }
    Ok(journal)
}

fn inspect(store: &DocumentStore, id: CommandId) -> Result<Option<JobRecoveryView>, String> {
    let Some(pending) = store.pending().map_err(|error| error.to_string())? else {
        return Ok(None);
    };
    let saved = store.load().map_err(|error| error.to_string())?;
    let current = saved
        .as_ref()
        .map(|document| parse(document, id))
        .transpose();
    let candidate = parse(&pending, id);
    let can_promote = match (&current, &candidate) {
        (Ok(current), Ok(candidate)) => follows(current.as_ref(), candidate)?,
        _ => false,
    };
    let can_keep_saved = matches!(&current, Ok(Some(_)));
    let saved_error = current.as_ref().err().cloned();
    let candidate_error = candidate.as_ref().err().cloned();
    Ok(Some(JobRecoveryView {
        job_id: id,
        saved_hash: saved.map(|document| document.hash),
        pending_hash: pending.hash,
        saved: current
            .ok()
            .flatten()
            .map(|journal| journal.snapshot())
            .transpose()?,
        candidate: candidate
            .ok()
            .map(|journal| journal.snapshot())
            .transpose()?,
        saved_error,
        candidate_error,
        can_promote,
        can_keep_saved,
    }))
}

/// A save can append one event, never replace metadata or rewrite past events.
fn follows(saved: Option<&JobJournal>, candidate: &JobJournal) -> Result<bool, String> {
    let (candidate_header, candidate_events) = parts(candidate)?;
    let Some(saved) = saved else {
        return Ok(candidate_events.is_empty() && candidate.snapshot()?.state == JobState::Prepared);
    };
    let (saved_header, saved_events) = parts(saved)?;
    Ok(candidate_header == saved_header
        && candidate_events.starts_with(&saved_events)
        && candidate_events.len() <= saved_events.len() + 1)
}

fn parts(journal: &JobJournal) -> Result<(Value, Vec<Value>), String> {
    let mut header: Value =
        serde_json::from_slice(&journal.archive_bytes()?).map_err(|error| error.to_string())?;
    let events = header
        .as_object_mut()
        .and_then(|object| object.remove("events"))
        .and_then(|events| events.as_array().cloned())
        .ok_or("Historique de job invalide")?;
    Ok((header, events))
}

fn confirm(
    store: &DocumentStore,
    id: CommandId,
    expected_saved: Option<ContentHash>,
    expected_pending: ContentHash,
    promote: bool,
) -> Result<StoredDocument, String> {
    let view = inspect(store, id)?.ok_or("Aucun enregistrement interrompu à récupérer.")?;
    if view.saved_hash != expected_saved || view.pending_hash != expected_pending {
        return Err("Les versions du journal ont changé ; les examiner à nouveau.".into());
    }
    if (promote && !view.can_promote) || (!promote && !view.can_keep_saved) {
        return Err("Cette version ne peut pas être récupérée automatiquement ; les données restent conservées.".into());
    }
    store
        .recover(expected_saved, expected_pending, promote)
        .map_err(|error| error.to_string())?;
    store
        .load()
        .map_err(|error| error.to_string())?
        .ok_or("Journal récupéré absent".into())
}
