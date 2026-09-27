//! Atomic replacement of the same authoring.json, never a second writable source.
use super::{
    migration::ZoneMigrationProposal,
    session::{ZoneEditSession, ZoneReceipt, ZoneRequest},
};
use crate::audit::AuditStamp;
use forge_core::{ContentHash, document_store::DocumentStore};
use std::path::Path;

pub struct FileZoneSession {
    store: DocumentStore,
    session: ZoneEditSession,
    saved_hash: Option<ContentHash>,
    saved_command_count: Option<usize>,
}

impl FileZoneSession {
    /// No legacy file and no implicit first save. The caller owns a fresh directory.
    pub(crate) fn create(directory: &Path, session: ZoneEditSession) -> Result<Self, String> {
        session.archive_bytes()?;
        let store = DocumentStore::open(directory).map_err(|e| e.to_string())?;
        if store.load().map_err(|e| e.to_string())?.is_some()
            || store.pending().map_err(|e| e.to_string())?.is_some()
        {
            return Err("Zone archive already exists or requires recovery".into());
        }
        Ok(Self {
            store,
            session,
            saved_hash: None,
            saved_command_count: None,
        })
    }

    /// Opens an existing aggregate only. A legacy archive is never migrated on open.
    pub fn open(directory: &Path) -> Result<Self, String> {
        let store = DocumentStore::open(directory).map_err(|e| e.to_string())?;
        Self::from_store(store)
    }

    pub(crate) fn from_store(store: DocumentStore) -> Result<Self, String> {
        let stored = store
            .load()
            .map_err(|e| e.to_string())?
            .ok_or("No saved zone")?;
        let session = ZoneEditSession::from_archive_bytes(&stored.bytes)?;
        let saved_command_count = Some(session.audit().len());
        Ok(Self {
            store,
            session,
            saved_hash: Some(stored.hash),
            saved_command_count,
        })
    }

    /// Explicit migration preparation against exact saved bytes. No write until save.
    /// The old session must release its OS lock before this handoff.
    pub fn prepare_migration(
        directory: &Path,
        proposal: &ZoneMigrationProposal,
    ) -> Result<Self, String> {
        let session = ZoneEditSession::from_migration(proposal.clone())?;
        let store = DocumentStore::open(directory).map_err(|e| e.to_string())?;
        let stored = store
            .load()
            .map_err(|e| e.to_string())?
            .ok_or("Save legacy session before migrating")?;
        if stored.bytes != proposal.legacy_archive() {
            return Err("Saved legacy archive differs from reviewed migration".into());
        }
        if store.pending().map_err(|e| e.to_string())?.is_some() {
            return Err("Resolve interrupted save before migration".into());
        }
        Ok(Self {
            store,
            session,
            saved_hash: Some(stored.hash),
            saved_command_count: None,
        })
    }

    #[must_use]
    pub const fn session(&self) -> &ZoneEditSession {
        &self.session
    }

    #[must_use]
    pub fn is_dirty(&self) -> bool {
        self.saved_command_count != Some(self.session.audit().len())
    }

    pub fn execute(&mut self, request: &ZoneRequest) -> Result<ZoneReceipt, String> {
        self.session.execute(request)
    }

    pub fn execute_with_audit(
        &mut self,
        request: &ZoneRequest,
        audit: AuditStamp,
    ) -> Result<ZoneReceipt, String> {
        self.session.execute_with_audit(request, audit)
    }

    pub fn compact(&mut self, review: &super::session::ZoneCompactionReview) -> Result<(), String> {
        self.session.apply_compaction(review)?;
        // Format changed even though the document revision and command count did not.
        self.saved_command_count = None;
        Ok(())
    }

    pub fn save(&mut self) -> Result<(), String> {
        let bytes = self.session.archive_bytes()?;
        self.saved_hash = Some(
            self.store
                .save(self.saved_hash, &bytes)
                .map_err(|e| e.to_string())?,
        );
        self.saved_command_count = Some(self.session.audit().len());
        Ok(())
    }
}
