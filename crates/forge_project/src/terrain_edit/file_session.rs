//! Native application adapter; the frontend must not supply arbitrary directories.

use super::{
    TerrainDocument, TerrainEditError, TerrainEditReceipt, TerrainEditRequest, TerrainEditSession,
};
use crate::audit::AuditStamp;
use forge_core::{
    ContentHash,
    document_store::{DocumentStore, StoreError},
};
use std::path::Path;
use thiserror::Error;

#[derive(Debug, Error)]
pub enum FileSessionError {
    #[error(transparent)]
    Edit(#[from] TerrainEditError),
    #[error(transparent)]
    Store(#[from] StoreError),
    #[error("no saved authoring session in this directory")]
    Missing,
    #[error("authoring session already exists; open it instead")]
    AlreadyExists,
    #[error("save or discard current edits before recovering an interrupted save")]
    UnsavedEdits,
}

pub struct FileTerrainSession {
    store: DocumentStore,
    session: TerrainEditSession,
    saved_hash: Option<ContentHash>,
    saved_command_count: Option<usize>,
}

impl FileTerrainSession {
    /// Recover even when the first save was interrupted before a main file existed.
    /// The native caller must supply identities and hashes from an explicit review.
    pub fn recover_open(
        directory: &Path,
        area_id: forge_core::AreaId,
        expected_saved: Option<ContentHash>,
        expected_pending: ContentHash,
    ) -> Result<Self, FileSessionError> {
        let store = DocumentStore::open(directory)?;
        let pending = store.pending()?.ok_or(FileSessionError::Missing)?;
        if pending.hash != expected_pending {
            return Err(StoreError::Conflict.into());
        }
        let session = TerrainEditSession::from_archive_bytes(&pending.bytes)?;
        if session.document().area_id != area_id {
            return Err(
                TerrainEditError::Archive("candidate area identity mismatch".into()).into(),
            );
        }
        store.recover(expected_saved, expected_pending, true)?;
        let saved_command_count = Some(session.audit().len());
        Ok(Self {
            store,
            session,
            saved_hash: Some(expected_pending),
            saved_command_count,
        })
    }

    pub fn create(directory: &Path, document: TerrainDocument) -> Result<Self, FileSessionError> {
        let session = TerrainEditSession::new(document)?;
        let store = DocumentStore::open(directory)?;
        if store.load()?.is_some() {
            return Err(FileSessionError::AlreadyExists);
        }
        Ok(Self {
            store,
            session,
            saved_hash: None,
            saved_command_count: None,
        })
    }

    pub fn open(directory: &Path) -> Result<Self, FileSessionError> {
        let store = DocumentStore::open(directory)?;
        Self::from_store(store)
    }

    pub(crate) fn from_store(store: DocumentStore) -> Result<Self, FileSessionError> {
        let stored = store.load()?.ok_or(FileSessionError::Missing)?;
        let session = TerrainEditSession::from_archive_bytes(&stored.bytes)?;
        let saved_command_count = Some(session.audit().len());
        Ok(Self {
            store,
            session,
            saved_hash: Some(stored.hash),
            saved_command_count,
        })
    }

    #[must_use]
    pub const fn session(&self) -> &TerrainEditSession {
        &self.session
    }

    #[must_use]
    pub fn is_dirty(&self) -> bool {
        self.saved_command_count != Some(self.session.audit().len())
    }

    /// Exact reviewed checkpoint, including original whitespace. Never a rewritten archive.
    pub(crate) fn saved_archive(&self) -> Result<Vec<u8>, FileSessionError> {
        if self.is_dirty() {
            return Err(FileSessionError::UnsavedEdits);
        }
        let stored = self.store.load()?.ok_or(FileSessionError::Missing)?;
        if Some(stored.hash) != self.saved_hash {
            return Err(StoreError::Conflict.into());
        }
        Ok(stored.bytes)
    }

    pub fn execute(
        &mut self,
        request: &TerrainEditRequest,
    ) -> Result<TerrainEditReceipt, FileSessionError> {
        Ok(self.session.execute(request)?)
    }

    pub fn execute_with_audit(
        &mut self,
        request: &TerrainEditRequest,
        audit: AuditStamp,
    ) -> Result<TerrainEditReceipt, FileSessionError> {
        Ok(self.session.execute_with_audit(request, audit)?)
    }

    pub fn save(&mut self) -> Result<(), FileSessionError> {
        let bytes = self.session.archive_bytes()?;
        let hash = self.store.save(self.saved_hash, &bytes)?;
        self.saved_hash = Some(hash);
        self.saved_command_count = Some(self.session.audit().len());
        Ok(())
    }

    /// Inspect candidate bytes; callers may display validation errors and still keep
    /// the last saved version. No candidate is selected automatically.
    pub fn pending(
        &self,
    ) -> Result<Option<forge_core::document_store::StoredDocument>, FileSessionError> {
        Ok(self.store.pending()?)
    }

    pub fn recover_pending(
        &mut self,
        expected_pending: ContentHash,
        promote: bool,
    ) -> Result<(), FileSessionError> {
        if self.is_dirty() {
            return Err(FileSessionError::UnsavedEdits);
        }
        let pending = self.store.pending()?.ok_or(FileSessionError::Missing)?;
        if pending.hash != expected_pending {
            return Err(StoreError::Conflict.into());
        }
        let candidate = if promote {
            let candidate = TerrainEditSession::from_archive_bytes(&pending.bytes)?;
            if candidate.document().project_id != self.session.document().project_id
                || candidate.document().area_id != self.session.document().area_id
            {
                return Err(TerrainEditError::Archive(
                    "candidate belongs to another terrain".into(),
                )
                .into());
            }
            Some(candidate)
        } else {
            None
        };
        self.store
            .recover(self.saved_hash, expected_pending, promote)?;
        if let Some(candidate) = candidate {
            self.saved_command_count = Some(candidate.audit().len());
            self.saved_hash = Some(expected_pending);
            self.session = candidate;
        }
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::terrain_edit::{
        TerrainAction,
        tests::{rename, request, session},
    };

    #[test]
    fn first_save_can_be_recovered_without_a_main_file() {
        let directory =
            std::env::temp_dir().join(format!("forge-first-recovery-{}", std::process::id()));
        std::fs::create_dir(&directory).unwrap();
        let candidate = session();
        let area_id = candidate.document().area_id;
        let bytes = candidate.archive_bytes().unwrap();
        std::fs::write(directory.join("authoring.pending"), &bytes).unwrap();
        let store = DocumentStore::open(&directory).unwrap();
        let hash = store.pending().unwrap().unwrap().hash;
        drop(store);
        assert!(FileTerrainSession::open(&directory).is_err());
        assert!(
            FileTerrainSession::recover_open(
                &directory,
                forge_core::AreaId::deterministic("wrong"),
                None,
                hash
            )
            .is_err()
        );
        assert!(!directory.join("authoring.json").exists());
        let mut recovered =
            FileTerrainSession::recover_open(&directory, area_id, None, hash).unwrap();
        assert_eq!(recovered.session().document(), candidate.document());
        assert!(!recovered.is_dirty());
        recovered.save().unwrap();
        drop(recovered);
        assert_eq!(
            FileTerrainSession::open(&directory)
                .unwrap()
                .session()
                .document(),
            candidate.document()
        );
    }

    #[test]
    fn recover_validated_candidate_and_reject_corrupt_archive() {
        let directory =
            std::env::temp_dir().join(format!("forge-terrain-recovery-{}", std::process::id()));
        std::fs::create_dir(&directory).unwrap();
        let mut file =
            FileTerrainSession::create(&directory, session().document().clone()).unwrap();
        file.save().unwrap();
        let mut candidate =
            TerrainEditSession::from_archive_bytes(&file.session().archive_bytes().unwrap())
                .unwrap();
        let edit = request(&candidate, "recovered", rename("Récupérée"));
        candidate.execute(&edit).unwrap();
        std::fs::write(
            directory.join("authoring.pending"),
            candidate.archive_bytes().unwrap(),
        )
        .unwrap();
        let pending = file.pending().unwrap().unwrap();
        file.recover_pending(pending.hash, true).unwrap();
        assert_eq!(file.session().document(), candidate.document());
        assert!(!file.is_dirty());
        drop(file);
        let mut file = FileTerrainSession::open(&directory).unwrap();
        let undo = request(file.session(), "undo", TerrainAction::Undo);
        file.execute(&undo).unwrap();
        file.save().unwrap();
        std::fs::write(directory.join("authoring.pending"), b"{").unwrap();
        let pending = file.pending().unwrap().unwrap();
        assert!(file.recover_pending(pending.hash, true).is_err());
        assert!(file.pending().unwrap().is_some());
        file.recover_pending(pending.hash, false).unwrap();
        assert!(file.pending().unwrap().is_none());
        file.save().unwrap();
    }

    #[test]
    fn real_file_save_reopen_undo_retry_and_external_conflict() {
        let directory =
            std::env::temp_dir().join(format!("forge-terrain-session-{}", std::process::id()));
        std::fs::create_dir(&directory).unwrap();
        let initial = session().document().clone();
        let mut file = FileTerrainSession::create(&directory, initial.clone()).unwrap();
        assert!(file.is_dirty());
        let edit = request(file.session(), "rename", rename("Persistée"));
        file.execute(&edit).unwrap();
        file.save().unwrap();
        assert!(!file.is_dirty());
        let document = file.session().document().clone();
        drop(file);
        let mut reopened = FileTerrainSession::open(&directory).unwrap();
        assert_eq!(reopened.session().document(), &document);
        reopened.execute(&edit).unwrap();
        assert!(!reopened.is_dirty());
        let undo = request(reopened.session(), "undo", TerrainAction::Undo);
        reopened.execute(&undo).unwrap();
        assert_eq!(reopened.session().document().name, initial.name);
        assert!(reopened.is_dirty());
        std::fs::write(directory.join("authoring.json"), b"external").unwrap();
        assert!(matches!(
            reopened.save(),
            Err(FileSessionError::Store(StoreError::Conflict))
        ));
        assert!(reopened.is_dirty());
        assert_eq!(
            std::fs::read(directory.join("authoring.json")).unwrap(),
            b"external"
        );
    }
}
