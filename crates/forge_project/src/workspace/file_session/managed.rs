//! Reuse zone commands and codecs; only archive references are workspace edits.
use super::FileWorkspaceSession;
use crate::audit::AuditStamp;
use crate::{
    workspace::{
        ZoneReference,
        objects::ReviewedZone,
        session::{WorkspaceAction, WorkspaceReceipt, WorkspaceRequest},
    },
    zone_authoring::session::{ZoneEditSession, ZoneReceipt, ZoneRequest},
};
use forge_core::{AreaId, CommandId, ModuleId, ProjectId, bytes_sha256};

impl FileWorkspaceSession {
    #[must_use]
    pub fn zone_is_dirty(&self, area: AreaId) -> bool {
        fn reference(
            head: &crate::workspace::WorkspaceHead,
            area: AreaId,
        ) -> Option<&ZoneReference> {
            head.projects
                .iter()
                .flat_map(|p| &p.modules)
                .flat_map(|m| &m.zones)
                .find(|z| z.area_id == area)
        }
        reference(self.session.head(), area)
            != self.saved_head.as_ref().and_then(|h| reference(h, area))
    }
    pub fn read_zone(&self, area: AreaId) -> Result<ZoneEditSession, String> {
        let (project, _, reference) = self.zone_reference(area)?;
        ZoneEditSession::from_archive_bytes(&self.objects.read(project, &reference)?)
    }

    /// Produces both diffs without writing objects or changing either authority.
    pub fn preview_zone_edit(
        &self,
        area: AreaId,
        request: &ZoneRequest,
        command_id: CommandId,
    ) -> Result<(WorkspaceReceipt, ZoneReceipt), String> {
        self.preview_zone_edit_with_audit(area, request, command_id, AuditStamp::legacy())
    }

    pub fn preview_zone_edit_with_audit(
        &self,
        area: AreaId,
        request: &ZoneRequest,
        command_id: CommandId,
        audit: AuditStamp,
    ) -> Result<(WorkspaceReceipt, ZoneReceipt), String> {
        let (project_id, module_id, before) = self.zone_reference(area)?;
        let (object, zone_receipt) =
            self.edited_object(project_id, &before, request, audit.clone())?;
        let proposal = WorkspaceRequest {
            command_id,
            base_revision: self.session.head().revision,
            base_hash: self
                .session
                .head()
                .content_hash()
                .map_err(|e| e.to_string())?,
            action: WorkspaceAction::UpdateZone {
                project_id,
                module_id,
                before,
                after: object.reference().clone(),
            },
        };
        Ok((
            self.session
                .preview_with_audit(&proposal, audit)
                .map_err(|e| e.to_string())?,
            zone_receipt,
        ))
    }

    /// The exact reviewed workspace request and zone command are required together.
    /// Object publication precedes acceptance; Save remains explicit.
    pub fn apply_zone_edit(
        &mut self,
        workspace: &WorkspaceRequest,
        zone: &ZoneRequest,
    ) -> Result<(WorkspaceReceipt, ZoneReceipt), String> {
        self.apply_zone_edit_with_audit(workspace, zone, AuditStamp::legacy())
    }

    pub fn apply_zone_edit_with_audit(
        &mut self,
        workspace: &WorkspaceRequest,
        zone: &ZoneRequest,
        audit: AuditStamp,
    ) -> Result<(WorkspaceReceipt, ZoneReceipt), String> {
        let preview = self
            .session
            .preview_with_audit(workspace, audit.clone())
            .map_err(|e| e.to_string())?;
        let WorkspaceAction::UpdateZone {
            project_id,
            before,
            after,
            ..
        } = &workspace.action
        else {
            return Err("Managed zone edit requires UpdateZone".into());
        };
        let (object, zone_receipt) =
            self.edited_object(*project_id, before, zone, audit.clone())?;
        if object.reference() != after {
            return Err("Zone command differs from reviewed result".into());
        }
        if self
            .session
            .audit()
            .iter()
            .any(|r| r.request.command_id == workspace.command_id)
        {
            return Ok((preview, zone_receipt));
        }
        self.objects.publish(&object)?;
        let receipt = self
            .session
            .execute_with_audit(workspace, audit)
            .map_err(|e| e.to_string())?;
        Ok((receipt, zone_receipt))
    }

    fn edited_object(
        &self,
        project: ProjectId,
        reference: &ZoneReference,
        request: &ZoneRequest,
        audit: AuditStamp,
    ) -> Result<(ReviewedZone, ZoneReceipt), String> {
        let bytes = self.objects.read(project, reference)?;
        let mut session = ZoneEditSession::from_archive_bytes(&bytes)?;
        let receipt = session.execute_with_audit(request, audit)?;
        let bytes = session.archive_bytes()?;
        let object = ReviewedZone::from_bytes(&bytes, bytes_sha256(&bytes))?;
        Ok((object, receipt))
    }

    pub(super) fn zone_reference(
        &self,
        area: AreaId,
    ) -> Result<(ProjectId, ModuleId, ZoneReference), String> {
        for project in &self.session.head().projects {
            for module in &project.modules {
                if let Some(zone) = module.zones.iter().find(|z| z.area_id == area) {
                    return Ok((project.id, module.id, zone.clone()));
                }
            }
        }
        Err("Zone is not a current member of this workspace".into())
    }
}
