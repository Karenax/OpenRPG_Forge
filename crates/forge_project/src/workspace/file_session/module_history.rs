//! Module history reuses the workspace snapshots and refuses cross-module effects.
use super::{FileWorkspaceSession, verify_references};
use crate::{
    audit::AuditStamp,
    workspace::{
        WorkspaceHead,
        session::{WorkspaceAction, WorkspaceReceipt, WorkspaceRequest},
    },
};
use forge_core::{CommandId, ContentHash, ModuleId};

impl FileWorkspaceSession {
    pub fn preview_module_history(
        &self,
        module: ModuleId,
        expected: ContentHash,
        redo: bool,
        command: CommandId,
    ) -> Result<WorkspaceReceipt, String> {
        if self
            .session
            .head()
            .content_hash()
            .map_err(|error| error.to_string())?
            != expected
        {
            return Err("Workspace changed since the module history was read".into());
        }
        let request = WorkspaceRequest {
            command_id: command,
            base_hash: expected,
            base_revision: self.session.head().revision,
            action: if redo {
                WorkspaceAction::Redo
            } else {
                WorkspaceAction::Undo
            },
        };
        let receipt = self
            .session
            .preview(&request)
            .map_err(|error| error.to_string())?;
        require_module_scope(&receipt.before, &receipt.after, module)?;
        Ok(receipt)
    }

    pub fn apply_module_history(
        &mut self,
        module: ModuleId,
        request: &WorkspaceRequest,
        audit: AuditStamp,
    ) -> Result<WorkspaceReceipt, String> {
        if !matches!(
            request.action,
            WorkspaceAction::Undo | WorkspaceAction::Redo
        ) {
            return Err("Module history accepts only Undo or Redo".into());
        }
        let receipt = self
            .session
            .preview_with_audit(request, audit.clone())
            .map_err(|error| error.to_string())?;
        require_module_scope(&receipt.before, &receipt.after, module)?;
        // Undo and Redo only reference snapshots retained in this authority's journal.
        // Verify their objects before moving the head, so a corrupt target cannot
        // leave the active tab pointing to an unreadable result.
        verify_references(&self.session, &self.objects)?;
        self.session
            .execute_with_audit(request, audit)
            .map_err(|error| error.to_string())
    }
}

fn require_module_scope(
    before: &WorkspaceHead,
    after: &WorkspaceHead,
    module: ModuleId,
) -> Result<(), String> {
    let mut projected = before.projects.clone();
    let mut changed = false;
    for project in &mut projected {
        if let Some(index) = project.modules.iter().position(|entry| entry.id == module) {
            let replacement = after
                .projects
                .iter()
                .find(|entry| entry.id == project.id)
                .and_then(|entry| entry.modules.iter().find(|entry| entry.id == module))
                .ok_or("Module history cannot remove the active module")?;
            changed = project.modules[index] != *replacement;
            project.modules[index] = replacement.clone();
        }
    }
    if !changed || projected != after.projects {
        return Err("The next history operation affects another module or its project".into());
    }
    Ok(())
}
