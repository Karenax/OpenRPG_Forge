//! Explicit recovery under the same native lock as saving; no automatic selection.
use crate::authoring::{RecoveryReview, RecoveryVersion};
use forge_core::{
    AreaId, ContentHash,
    document_store::{DocumentStore, StoredDocument},
};
use std::path::Path;

fn store(root: &Path, area_id: AreaId) -> Result<DocumentStore, String> {
    let path = root
        .join(area_id.to_string())
        .canonicalize()
        .map_err(|e| e.to_string())?;
    if path.parent() != Some(root) {
        return Err("zone path leaves authorized root".into());
    }
    DocumentStore::open(&path).map_err(|e| e.to_string())
}

fn version(stored: &StoredDocument, area_id: AreaId) -> RecoveryVersion {
    let parsed = super::session::archive_summary(&stored.bytes).and_then(|(document, _)| {
        if document.area_id == area_id {
            Ok(document)
        } else {
            Err("zone identity does not match directory".into())
        }
    });
    let (document, error) = match parsed {
        Ok(document) => (Some(document), None),
        Err(error) => (None, Some(error)),
    };
    RecoveryVersion {
        archive_hash: stored.hash,
        document,
        error,
    }
}

fn review(store: &DocumentStore, area_id: AreaId) -> Result<RecoveryReview, String> {
    let saved = store
        .load()
        .map_err(|e| e.to_string())?
        .map(|v| version(&v, area_id));
    let mut pending = version(
        &store
            .pending()
            .map_err(|e| e.to_string())?
            .ok_or("no interrupted save to recover")?,
        area_id,
    );
    if let (Some(saved), Some(candidate)) = (
        saved.as_ref().and_then(|v| v.document.as_ref()),
        pending.document.as_ref(),
    ) && saved.project_id != candidate.project_id
    {
        pending.document = None;
        pending.error = Some("candidate belongs to another project".into());
    }
    Ok(RecoveryReview {
        area_id,
        saved,
        pending,
    })
}

pub(super) fn inspect(root: &Path, area_id: AreaId) -> Result<RecoveryReview, String> {
    review(&store(root, area_id)?, area_id)
}

pub(super) fn recover(
    root: &Path,
    area_id: AreaId,
    expected_saved: Option<ContentHash>,
    expected_pending: ContentHash,
    promote: bool,
) -> Result<(), String> {
    let store = store(root, area_id)?;
    crate::workspace::ownership::require_unmanaged(root, area_id)?;
    let review = review(&store, area_id)?;
    if review.saved.as_ref().map(|v| v.archive_hash) != expected_saved
        || review.pending.archive_hash != expected_pending
    {
        return Err("recovery versions changed: inspect them again".into());
    }
    if promote && review.pending.document.is_none() {
        return Err("invalid candidate cannot be promoted".into());
    }
    store
        .recover(expected_saved, expected_pending, promote)
        .map_err(|e| e.to_string())
}
