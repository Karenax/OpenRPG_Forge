//! New zones publish directly to the workspace; no intermediate legacy archive.
use super::FileWorkspaceSession;
use crate::{
    TerrainDocument,
    audit::AuditStamp,
    workspace::{
        objects::ReviewedZone,
        session::{WorkspaceAction, WorkspaceReceipt, WorkspaceRequest},
    },
    zone_authoring::session::ZoneEditSession,
};
use forge_core::{
    CommandId, ModuleId, bytes_sha256, canonical_json_sha256, document_store::DocumentStore,
};

fn object(document: TerrainDocument) -> Result<ReviewedZone, String> {
    let bytes = ZoneEditSession::from_initial_terrain(document)?.archive_bytes()?;
    ReviewedZone::from_bytes(&bytes, bytes_sha256(&bytes))
}

impl FileWorkspaceSession {
    pub fn review_new_zone(
        &self,
        module_id: ModuleId,
        document: TerrainDocument,
    ) -> Result<WorkspaceReceipt, String> {
        let reviewed = object(document)?;
        let directory = self.root.join(reviewed.reference().area_id.to_string());
        match std::fs::symlink_metadata(directory) {
            Err(e) if e.kind() == std::io::ErrorKind::NotFound => (),
            Err(e) => return Err(e.to_string()),
            Ok(_) => {
                return Err("Zone identity is already reserved; choose another zone key".into());
            }
        }
        let action = WorkspaceAction::AttachZone {
            project_id: reviewed.project_id(),
            module_id,
            reference: reviewed.reference().clone(),
        };
        let head = self.session.head();
        let hash = head.content_hash().map_err(|e| e.to_string())?;
        let key = canonical_json_sha256(&(hash, &action)).map_err(|e| e.to_string())?;
        self.session
            .preview(&WorkspaceRequest {
                command_id: CommandId::deterministic(format!("create-zone/{key}")),
                base_hash: hash,
                base_revision: head.revision,
                action,
            })
            .map_err(|e| e.to_string())
    }

    pub fn create_new_zone(
        &mut self,
        request: &WorkspaceRequest,
        document: TerrainDocument,
    ) -> Result<WorkspaceReceipt, String> {
        self.create_new_zone_with_audit(request, document, AuditStamp::legacy())
    }

    pub fn create_new_zone_with_audit(
        &mut self,
        request: &WorkspaceRequest,
        document: TerrainDocument,
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
            return Err("New zone requires the reviewed attachment request".into());
        };
        let reviewed = object(document)?;
        if reviewed.project_id() != *project_id || reviewed.reference() != reference {
            return Err("New zone differs from the reviewed recipe; inspect it again".into());
        }
        if self
            .session
            .audit()
            .iter()
            .any(|r| r.request.command_id == request.command_id)
        {
            return Ok(preview);
        }
        // Reserve only a lock directory, never a second authoring.json. This also
        // prevents a legacy Create from claiming this identity before workspace Save.
        let directory = self.root.join(reference.area_id.to_string());
        std::fs::create_dir(&directory).map_err(|e| e.to_string())?;
        let reservation = DocumentStore::open(&directory).map_err(|e| e.to_string())?;
        self.objects.publish(&reviewed)?;
        let receipt = self
            .session
            .execute_with_audit(request, audit)
            .map_err(|e| e.to_string())?;
        self.adoption_locks.push(reservation);
        Ok(receipt)
    }
}
