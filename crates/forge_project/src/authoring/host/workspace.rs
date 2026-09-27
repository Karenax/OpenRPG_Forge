use super::AuthoringHost;
use crate::{
    audit::AuditStamp,
    authoring::workspace::{WorkspaceHostRequest, WorkspaceHostState, WorkspaceRecoveryReview},
    workspace::file_session::{FileWorkspaceSession, recovery as service},
};

impl AuthoringHost {
    pub fn execute_workspace_with_audit(
        &mut self,
        request: WorkspaceHostRequest,
        audit: AuditStamp,
    ) -> Result<WorkspaceHostState, String> {
        audit.validate()?;
        if request.persists_audit_receipt() && audit.is_legacy() {
            return Err(
                "new workspace receipts require an explicit human or AI audit stamp".into(),
            );
        }
        self.pending_audit = Some(audit);
        let result = self.execute_workspace(request);
        self.pending_audit = None;
        result
    }

    pub fn execute_workspace(
        &mut self,
        request: WorkspaceHostRequest,
    ) -> Result<WorkspaceHostState, String> {
        self.require_workspace_command(&request)?;
        let mut receipt = None;
        let mut recovery = None;
        let mut history = None;
        match request {
            WorkspaceHostRequest::Create | WorkspaceHostRequest::Open => {
                if self.workspace.is_some() {
                    return Err("Close the current workspace first".into());
                }
                self.workspace = Some(if matches!(request, WorkspaceHostRequest::Create) {
                    FileWorkspaceSession::create(&self.root)?
                } else {
                    FileWorkspaceSession::open(&self.root)?
                });
            }
            WorkspaceHostRequest::Inspect => (),
            WorkspaceHostRequest::ReadHistory {
                expected_hash,
                offset,
            } => {
                history = Some(crate::workspace::history::read(
                    self.workspace()?.session(),
                    expected_hash,
                    offset,
                )?);
            }
            WorkspaceHostRequest::ReviewNewZone {
                module_id,
                document,
            } => {
                self.require_closed()?;
                receipt = Some(self.workspace()?.review_new_zone(module_id, *document)?);
            }
            WorkspaceHostRequest::CreateNewZone { request, document } => {
                self.require_closed()?;
                let audit = self.workspace_audit();
                receipt = Some(
                    self.workspace()?
                        .create_new_zone_with_audit(&request, *document, audit)?,
                );
            }
            WorkspaceHostRequest::ReviewRecovery | WorkspaceHostRequest::Recover { .. } => {
                recovery = self.workspace_recovery(&request)?;
            }
            WorkspaceHostRequest::ReviewAdoption {
                area_id,
                project_id,
                module_id,
                expected_archive_hash,
            } => {
                self.require_closed()?;
                receipt = Some(self.workspace()?.review_adoption(
                    area_id,
                    project_id,
                    module_id,
                    expected_archive_hash,
                )?);
            }
            WorkspaceHostRequest::Preview { request } => {
                receipt = Some(
                    self.workspace()?
                        .session()
                        .preview(&request)
                        .map_err(|e| e.to_string())?,
                );
            }
            WorkspaceHostRequest::Apply { request } => {
                let audit = self
                    .pending_audit
                    .clone()
                    .unwrap_or_else(AuditStamp::legacy);
                receipt = Some(
                    self.workspace()?
                        .session_mut()
                        .execute_with_audit(&request, audit)
                        .map_err(|error| error.to_string())?,
                );
            }
            WorkspaceHostRequest::Adopt { request } => {
                self.require_closed()?;
                let audit = self.workspace_audit();
                receipt = Some(self.workspace()?.adopt_with_audit(&request, audit)?);
            }
            WorkspaceHostRequest::Save => self.save_workspace()?,
            WorkspaceHostRequest::Close => self.close_workspace()?,
        }
        Ok(WorkspaceHostState {
            head: self.workspace.as_ref().map(|s| s.session().head().clone()),
            dirty: self
                .workspace
                .as_ref()
                .is_some_and(FileWorkspaceSession::is_dirty),
            receipt,
            recovery,
            history,
        })
    }

    fn workspace(&mut self) -> Result<&mut FileWorkspaceSession, String> {
        self.workspace
            .as_mut()
            .ok_or_else(|| "Open or create a workspace first".into())
    }

    fn workspace_audit(&self) -> AuditStamp {
        self.pending_audit
            .clone()
            .unwrap_or_else(AuditStamp::legacy)
    }

    fn save_workspace(&mut self) -> Result<(), String> {
        if self.managed_area().is_some() {
            self.save_managed()
        } else {
            self.workspace()?.save()
        }
    }

    fn close_workspace(&mut self) -> Result<(), String> {
        if self.workspace()?.is_dirty() {
            return Err("Save workspace changes before closing".into());
        }
        self.workspace = None;
        Ok(())
    }

    fn workspace_recovery(
        &self,
        request: &WorkspaceHostRequest,
    ) -> Result<Option<WorkspaceRecoveryReview>, String> {
        self.require_closed()?;
        if self.workspace.is_some() {
            return Err("Close the workspace before recovery".into());
        }
        match request {
            WorkspaceHostRequest::Recover {
                expected_saved,
                expected_pending,
                promote,
            } => {
                service::recover(&self.root, *expected_saved, *expected_pending, *promote)?;
                Ok(None)
            }
            WorkspaceHostRequest::ReviewRecovery => service::inspect(&self.root).map(Some),
            _ => Err("Expected a workspace recovery request".into()),
        }
    }

    fn require_workspace_command(&self, request: &WorkspaceHostRequest) -> Result<(), String> {
        if self.playtest.is_some()
            && !matches!(
                request,
                WorkspaceHostRequest::Inspect | WorkspaceHostRequest::ReadHistory { .. }
            )
        {
            return Err("Stop playtest before changing the workspace".into());
        }
        if !self.managed_tabs.is_empty()
            && !matches!(
                request,
                WorkspaceHostRequest::Inspect
                    | WorkspaceHostRequest::Save
                    | WorkspaceHostRequest::ReadHistory { .. }
            )
        {
            return Err("Close the managed zone before changing its workspace".into());
        }
        Ok(())
    }
}
