use super::FileWorkspaceSession;
use crate::{
    workspace::{
        objects::ReviewedZone,
        session::{WorkspaceAction, WorkspaceReceipt, WorkspaceRequest},
    },
    zone_authoring::session::{ZoneCompactionReview, ZoneEditSession},
};
use forge_core::{AreaId, CommandId, bytes_sha256, canonical_json_sha256};

impl FileWorkspaceSession {
    pub fn preview_zone_compaction(
        &self,
        area: AreaId,
    ) -> Result<(ZoneCompactionReview, WorkspaceReceipt), String> {
        let (project_id, module_id, before) = self.zone_reference(area)?;
        let mut zone = self.read_zone(area)?;
        let review = zone.review_compaction()?;
        zone.apply_compaction(&review)?;
        let bytes = zone.archive_bytes()?;
        let object = ReviewedZone::from_bytes(&bytes, bytes_sha256(&bytes))?;
        let action = WorkspaceAction::UpdateZone {
            project_id,
            module_id,
            before,
            after: object.reference().clone(),
        };
        let hash = self
            .session
            .head()
            .content_hash()
            .map_err(|e| e.to_string())?;
        let key = canonical_json_sha256(&(hash, &action)).map_err(|e| e.to_string())?;
        let request = WorkspaceRequest {
            command_id: CommandId::deterministic(format!("compact/{key}")),
            base_hash: hash,
            base_revision: self.session.head().revision,
            action,
        };
        Ok((
            review,
            self.session.preview(&request).map_err(|e| e.to_string())?,
        ))
    }

    pub fn apply_zone_compaction(
        &mut self,
        request: &WorkspaceRequest,
        review: &ZoneCompactionReview,
    ) -> Result<WorkspaceReceipt, String> {
        let preview = self.session.preview(request).map_err(|e| e.to_string())?;
        let WorkspaceAction::UpdateZone {
            project_id,
            before,
            after,
            ..
        } = &request.action
        else {
            return Err("Compaction requires a reviewed archive update".into());
        };
        let mut zone =
            ZoneEditSession::from_archive_bytes(&self.objects.read(*project_id, before)?)?;
        zone.apply_compaction(review)?;
        let bytes = zone.archive_bytes()?;
        let object = ReviewedZone::from_bytes(&bytes, bytes_sha256(&bytes))?;
        if object.reference() != after {
            return Err("Compacted archive differs from reviewed output".into());
        }
        if self
            .session
            .audit()
            .iter()
            .any(|r| r.request.command_id == request.command_id)
        {
            return Ok(preview);
        }
        self.objects.publish(&object)?;
        self.session.execute(request).map_err(|e| e.to_string())
    }
}
