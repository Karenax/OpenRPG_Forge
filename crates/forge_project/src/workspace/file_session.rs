//! Reserved workspace authority; source locks span adoption through head save.
use super::{
    WorkspaceHead,
    session::{WorkspaceReceipt, WorkspaceRequest, WorkspaceSession},
};
use super::{
    objects::{ReviewedZone, ZoneObjectStore},
    session::WorkspaceAction,
};
use crate::audit::AuditStamp;
use forge_core::{ContentHash, document_store::DocumentStore};
use std::{fs, path::Path};
mod adoption_review;
mod compaction;
mod creation;
mod managed;
pub mod recovery;

pub struct FileWorkspaceSession {
    store: DocumentStore,
    session: WorkspaceSession,
    saved_hash: Option<ContentHash>,
    saved_commands: Option<usize>,
    saved_head: Option<WorkspaceHead>,
    root: std::path::PathBuf,
    objects: ZoneObjectStore,
    adoption_locks: Vec<DocumentStore>,
    // Advisory names only, keyed by accepted immutable archive hashes.
    zone_name_cache: std::collections::BTreeMap<ContentHash, String>,
}

impl FileWorkspaceSession {
    /// Only an explicitly authorized workspace root is accepted by this adapter.
    /// Existing zone directories are never touched or implicitly adopted.
    pub fn create(root: &Path) -> Result<Self, String> {
        let directory = root.join("workspace-v1");
        fs::create_dir(&directory).map_err(|e| e.to_string())?;
        let store = DocumentStore::open(&directory).map_err(|e| e.to_string())?;
        Ok(Self {
            root: root.canonicalize().map_err(|e| e.to_string())?,
            objects: ZoneObjectStore::open(&directory)?,
            adoption_locks: Vec::new(),
            zone_name_cache: std::collections::BTreeMap::new(),
            store,
            session: WorkspaceSession::new(WorkspaceHead::default()).map_err(|e| e.to_string())?,
            saved_hash: None,
            saved_commands: None,
            saved_head: None,
        })
    }

    pub fn open(root: &Path) -> Result<Self, String> {
        let directory = root.join("workspace-v1");
        let metadata = fs::symlink_metadata(&directory).map_err(|e| e.to_string())?;
        if metadata.file_type().is_symlink() || !metadata.is_dir() {
            return Err("Workspace authority must be a directory, not a link".into());
        }
        let store = DocumentStore::open(&directory).map_err(|e| e.to_string())?;
        if store.pending().map_err(|e| e.to_string())?.is_some() {
            return Err("Workspace has an interrupted save; explicit recovery is required".into());
        }
        let saved = store
            .load()
            .map_err(|e| e.to_string())?
            .ok_or("Workspace has no saved head")?;
        let session =
            WorkspaceSession::from_archive_bytes(&saved.bytes).map_err(|e| e.to_string())?;
        let objects = ZoneObjectStore::open(&directory)?;
        verify_references(&session, &objects)?;
        let saved_commands = Some(session.audit().len());
        Ok(Self {
            root: root.canonicalize().map_err(|e| e.to_string())?,
            objects,
            adoption_locks: Vec::new(),
            zone_name_cache: std::collections::BTreeMap::new(),
            store,
            saved_head: Some(session.head().clone()),
            session,
            saved_hash: Some(saved.hash),
            saved_commands,
        })
    }

    #[must_use]
    pub fn session(&self) -> &WorkspaceSession {
        &self.session
    }

    pub fn session_mut(&mut self) -> &mut WorkspaceSession {
        &mut self.session
    }

    #[must_use]
    pub fn is_dirty(&self) -> bool {
        self.saved_commands != Some(self.session.audit().len())
    }

    pub fn execute(&mut self, request: &WorkspaceRequest) -> Result<WorkspaceReceipt, String> {
        if matches!(
            request.action,
            WorkspaceAction::AttachZone { .. } | WorkspaceAction::UpdateZone { .. }
        ) {
            return Err(
                "Zone attachment requires explicit adoption of reviewed saved bytes".into(),
            );
        }
        self.session.execute(request).map_err(|e| e.to_string())
    }

    /// The request names exact reviewed hashes, not a free-form filesystem path.
    /// Source must be saved and closed. Its lock remains held until save or drop.
    pub fn adopt(&mut self, request: &WorkspaceRequest) -> Result<WorkspaceReceipt, String> {
        self.adopt_with_audit(request, AuditStamp::legacy())
    }

    pub fn adopt_with_audit(
        &mut self,
        request: &WorkspaceRequest,
        audit: AuditStamp,
    ) -> Result<WorkspaceReceipt, String> {
        let preview = self
            .session
            .preview_with_audit(request, audit.clone())
            .map_err(|e| e.to_string())?;
        let WorkspaceAction::AttachZone {
            project_id,
            reference,
            ..
        } = &request.action
        else {
            return Err("Adoption requires an AttachZone request".into());
        };
        if self
            .session
            .audit()
            .iter()
            .any(|r| r.request.command_id == request.command_id)
        {
            return Ok(preview);
        }
        let directory = self
            .root
            .join(reference.area_id.to_string())
            .canonicalize()
            .map_err(|e| e.to_string())?;
        if directory.parent() != Some(self.root.as_path()) {
            return Err("Zone source leaves workspace root".into());
        }
        let source = DocumentStore::open(&directory).map_err(|e| e.to_string())?;
        super::ownership::require_unmanaged(&self.root, reference.area_id)?;
        if source.pending().map_err(|e| e.to_string())?.is_some() {
            return Err("Resolve interrupted zone save before adoption".into());
        }
        let saved = source
            .load()
            .map_err(|e| e.to_string())?
            .ok_or("Save zone before adoption")?;
        let reviewed = ReviewedZone::from_bytes(&saved.bytes, reference.archive_hash)?;
        if reviewed.project_id() != *project_id || reviewed.reference() != reference {
            return Err("Reviewed zone project/area/head differs from adoption request".into());
        }
        if !reviewed.required_source_blobs().is_empty() {
            let source_blobs = super::source_blobs::SourceBlobStore::open_existing(&directory)?;
            let destination =
                super::source_blobs::SourceBlobStore::open(&self.root.join("workspace-v1"))?;
            for (&hash, &length) in reviewed.required_source_blobs() {
                destination.publish(hash, &source_blobs.read(hash, length)?)?;
            }
        }
        self.objects.publish(&reviewed)?;
        let receipt = self
            .session
            .execute_with_audit(request, audit)
            .map_err(|e| e.to_string())?;
        self.adoption_locks.push(source);
        Ok(receipt)
    }

    pub fn save(&mut self) -> Result<(), String> {
        verify_references(&self.session, &self.objects)?;
        let bytes = self.session.archive_bytes().map_err(|e| e.to_string())?;
        self.saved_hash = Some(
            self.store
                .save(self.saved_hash, &bytes)
                .map_err(|e| e.to_string())?,
        );
        self.saved_commands = Some(self.session.audit().len());
        self.saved_head = Some(self.session.head().clone());
        self.adoption_locks.clear();
        Ok(())
    }
}

fn verify_references(session: &WorkspaceSession, objects: &ZoneObjectStore) -> Result<(), String> {
    let mut seen = std::collections::BTreeSet::new();
    let heads = std::iter::once(session.head())
        .chain(session.audit().iter().flat_map(|r| [&r.before, &r.after]));
    let mut total_bytes = 0;
    let mut pending = Vec::new();
    for head in heads {
        for project in &head.projects {
            for zone in project.modules.iter().flat_map(|m| &m.zones) {
                if seen.insert((project.id, zone.area_id, zone.archive_hash, zone.head_hash)) {
                    // Reserve physical sizes before concurrent replay. Each read is
                    // bounded by its reservation and rechecks bytes, identity and sources.
                    let size = objects.verification_size(zone)?;
                    if size > 256 * 1024 * 1024 - total_bytes {
                        return Err(
                            "Zone object exceeds remaining archive verification budget".into()
                        );
                    }
                    total_bytes += size;
                    pending.push((project.id, zone, size));
                }
            }
        }
    }
    if pending.is_empty() {
        return Ok(());
    }
    // Keep at most four domain replays resident, without a second persistence path.
    // Join every worker before releasing the workspace authority or returning an error.
    let workers = std::thread::available_parallelism()
        .map_or(1, std::num::NonZeroUsize::get)
        .min(4)
        .min(pending.len());
    std::thread::scope(|scope| {
        let handles: Vec<_> = pending
            .chunks(pending.len().div_ceil(workers))
            .map(|chunk| {
                scope.spawn(move || {
                    for (project, zone, size) in chunk {
                        objects.read_with_budget(*project, zone, *size)?;
                    }
                    Ok::<(), String>(())
                })
            })
            .collect();
        let mut result = Ok(());
        for handle in handles {
            let worker = handle
                .join()
                .unwrap_or_else(|_| Err("Archive verification worker panicked".into()));
            if result.is_ok() {
                result = worker;
            }
        }
        result
    })
}
