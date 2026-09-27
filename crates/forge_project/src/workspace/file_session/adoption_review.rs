use super::FileWorkspaceSession;
use crate::workspace::{
    objects::ReviewedZone,
    session::{WorkspaceAction, WorkspaceReceipt, WorkspaceRequest},
};
use forge_core::{
    AreaId, CommandId, ContentHash, ModuleId, ProjectId, canonical_json_sha256,
    document_store::DocumentStore,
};

impl FileWorkspaceSession {
    /// Read-only preparation from saved source bytes. Apply reacquires and checks
    /// the source lock and both hashes; this review does not reserve ownership.
    pub fn review_adoption(
        &self,
        area: AreaId,
        project: ProjectId,
        module: ModuleId,
        expected: ContentHash,
    ) -> Result<WorkspaceReceipt, String> {
        let directory = self
            .root
            .join(area.to_string())
            .canonicalize()
            .map_err(|e| e.to_string())?;
        if directory.parent() != Some(self.root.as_path()) {
            return Err("Zone source leaves workspace root".into());
        }
        let source = DocumentStore::open(&directory).map_err(|e| e.to_string())?;
        crate::workspace::ownership::require_unmanaged(&self.root, area)?;
        if source.pending().map_err(|e| e.to_string())?.is_some() {
            return Err("Resolve interrupted zone save before adoption".into());
        }
        let saved = source
            .load()
            .map_err(|e| e.to_string())?
            .ok_or("Zone has no saved archive")?;
        let reviewed = ReviewedZone::from_bytes(&saved.bytes, expected)?;
        if reviewed.project_id() != project || reviewed.reference().area_id != area {
            return Err("Zone belongs to a different project or area".into());
        }
        if !reviewed.required_source_blobs().is_empty() {
            let blobs = crate::workspace::source_blobs::SourceBlobStore::open_existing(&directory)?;
            for (&hash, &length) in reviewed.required_source_blobs() {
                blobs.read(hash, length)?;
            }
        }
        let action = WorkspaceAction::AttachZone {
            project_id: project,
            module_id: module,
            reference: reviewed.reference().clone(),
        };
        let head = self.session.head();
        let hash = head.content_hash().map_err(|e| e.to_string())?;
        let key = canonical_json_sha256(&(hash, &action)).map_err(|e| e.to_string())?;
        self.session
            .preview(&WorkspaceRequest {
                command_id: CommandId::deterministic(format!("adopt/{key}")),
                base_hash: hash,
                base_revision: head.revision,
                action,
            })
            .map_err(|e| e.to_string())
    }
}
