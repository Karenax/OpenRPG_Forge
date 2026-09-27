//! Explicit interrupted-save recovery. Exact versions are archived before selection.
use super::{WorkspaceSession, verify_references};
use crate::{
    authoring::workspace::{WorkspaceRecoveryReview, WorkspaceRecoveryVersion},
    workspace::objects::ZoneObjectStore,
};
use forge_core::{
    ContentHash,
    document_store::{DocumentStore, StoredDocument},
};
use std::{fs, path::Path};

fn store(root: &Path) -> Result<(DocumentStore, ZoneObjectStore), String> {
    let directory = root.join("workspace-v1");
    let meta = fs::symlink_metadata(&directory).map_err(|e| e.to_string())?;
    if !meta.is_dir() || meta.file_type().is_symlink() {
        return Err("Workspace authority must be a directory, not a link".into());
    }
    let store = DocumentStore::open(&directory).map_err(|e| e.to_string())?;
    Ok((store, ZoneObjectStore::open(&directory)?))
}

fn version(stored: &StoredDocument, objects: &ZoneObjectStore) -> WorkspaceRecoveryVersion {
    let parsed = WorkspaceSession::from_archive_bytes(&stored.bytes)
        .map_err(|e| e.to_string())
        .and_then(|session| {
            verify_references(&session, objects)?;
            Ok(session.head().clone())
        });
    let (head, error) = match parsed {
        Ok(head) => (Some(head), None),
        Err(error) => (None, Some(error)),
    };
    WorkspaceRecoveryVersion {
        archive_hash: stored.hash,
        head,
        error,
    }
}

fn review(
    store: &DocumentStore,
    objects: &ZoneObjectStore,
) -> Result<WorkspaceRecoveryReview, String> {
    Ok(WorkspaceRecoveryReview {
        saved: store
            .load()
            .map_err(|e| e.to_string())?
            .map(|v| version(&v, objects)),
        pending: version(
            &store
                .pending()
                .map_err(|e| e.to_string())?
                .ok_or("No interrupted workspace save to recover")?,
            objects,
        ),
    })
}

pub fn inspect(root: &Path) -> Result<WorkspaceRecoveryReview, String> {
    let (store, objects) = store(root)?;
    review(&store, &objects)
}

pub fn recover(
    root: &Path,
    expected_saved: Option<ContentHash>,
    expected_pending: ContentHash,
    promote: bool,
) -> Result<(), String> {
    let (store, objects) = store(root)?;
    let review = review(&store, &objects)?;
    if review.saved.as_ref().map(|v| v.archive_hash) != expected_saved
        || review.pending.archive_hash != expected_pending
    {
        return Err("Recovery versions changed: inspect them again".into());
    }
    if promote && review.pending.head.is_none() {
        return Err("Invalid workspace candidate or zone objects cannot be promoted".into());
    }
    // A pre-existing legacy editor must not survive promotion of an adoption.
    // Pending authority already rejects new legacy writers; hold old source locks
    // through replacement as adoption does. Do not create missing zone directories.
    let _source_locks = if promote {
        lock_sources(root, &store)?
    } else {
        Vec::new()
    };
    store
        .recover(expected_saved, expected_pending, promote)
        .map_err(|e| e.to_string())
}

fn lock_sources(root: &Path, store: &DocumentStore) -> Result<Vec<DocumentStore>, String> {
    let pending = store
        .pending()
        .map_err(|e| e.to_string())?
        .ok_or("Pending save disappeared")?;
    let session =
        WorkspaceSession::from_archive_bytes(&pending.bytes).map_err(|e| e.to_string())?;
    let heads = std::iter::once(session.head())
        .chain(session.audit().iter().flat_map(|r| [&r.before, &r.after]));
    let areas = heads
        .flat_map(|h| &h.projects)
        .flat_map(|p| &p.modules)
        .flat_map(|m| &m.zones)
        .map(|z| z.area_id)
        .collect::<std::collections::BTreeSet<_>>();
    let mut locks = Vec::new();
    for area in areas {
        let directory = root.join(area.to_string());
        let meta = match fs::symlink_metadata(&directory) {
            Ok(meta) => meta,
            Err(e) if e.kind() == std::io::ErrorKind::NotFound => continue,
            Err(e) => return Err(e.to_string()),
        };
        if !meta.is_dir() || meta.file_type().is_symlink() {
            return Err("Legacy source must be a directory, not a link".into());
        }
        locks.push(DocumentStore::open(&directory).map_err(|e| e.to_string())?);
    }
    Ok(locks)
}
