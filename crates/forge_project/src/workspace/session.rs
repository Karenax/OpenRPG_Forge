//! Pure command boundary; persistence and Studio adapters consume this session.
use super::{WorkspaceError, WorkspaceHead};
use crate::audit::AuditStamp;
use forge_core::{CommandId, ContentHash, ModuleId, ProjectId, Revision};
use serde::{Deserialize, Serialize};
use thiserror::Error;

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "action", rename_all = "snake_case", deny_unknown_fields)]
pub enum WorkspaceAction {
    CreateProject {
        id: ProjectId,
        name: String,
    },
    CreateModule {
        project_id: ProjectId,
        id: ModuleId,
        name: String,
    },
    AttachZone {
        project_id: ProjectId,
        module_id: ModuleId,
        reference: super::ZoneReference,
    },
    UpdateZone {
        project_id: ProjectId,
        module_id: ModuleId,
        before: super::ZoneReference,
        after: super::ZoneReference,
    },
    Undo,
    Redo,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorkspaceRequest {
    pub command_id: CommandId,
    pub base_revision: Revision,
    pub base_hash: ContentHash,
    pub action: WorkspaceAction,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorkspaceReceipt {
    pub request: WorkspaceRequest,
    pub before: WorkspaceHead,
    pub after: WorkspaceHead,
    #[serde(default, skip_serializing_if = "AuditStamp::is_legacy")]
    pub audit: AuditStamp,
}

#[derive(Debug, Error)]
pub enum SessionError {
    #[error("invalid audit stamp: {0}")]
    Audit(String),
    #[error(transparent)]
    Document(#[from] WorkspaceError),
    #[error("workspace changed since review")]
    Stale,
    #[error("command id was already used with different parameters")]
    CommandReuse,
    #[error("workspace has nothing to undo or redo")]
    EmptyHistory,
    #[error("workspace command history limit reached")]
    HistoryLimit,
    #[error(transparent)]
    Revision(#[from] forge_core::RevisionError),
}

pub struct WorkspaceSession {
    initial: WorkspaceHead,
    history_bytes: usize,
    head: WorkspaceHead,
    audit: Vec<WorkspaceReceipt>,
    undo: Vec<WorkspaceHead>,
    redo: Vec<WorkspaceHead>,
}

impl WorkspaceSession {
    pub fn new(head: WorkspaceHead) -> Result<Self, SessionError> {
        head.validate()?;
        Ok(Self {
            initial: head.clone(),
            history_bytes: 0,
            head,
            audit: Vec::new(),
            undo: Vec::new(),
            redo: Vec::new(),
        })
    }

    #[must_use]
    pub fn head(&self) -> &WorkspaceHead {
        &self.head
    }

    #[must_use]
    pub fn audit(&self) -> &[WorkspaceReceipt] {
        &self.audit
    }

    pub fn preview(&self, request: &WorkspaceRequest) -> Result<WorkspaceReceipt, SessionError> {
        self.preview_with_audit(request, AuditStamp::legacy())
    }

    pub fn preview_with_audit(
        &self,
        request: &WorkspaceRequest,
        audit: AuditStamp,
    ) -> Result<WorkspaceReceipt, SessionError> {
        audit.validate().map_err(SessionError::Audit)?;
        if let Some(receipt) = self
            .audit
            .iter()
            .find(|r| r.request.command_id == request.command_id)
        {
            return if receipt.request == *request {
                Ok(receipt.clone())
            } else {
                Err(SessionError::CommandReuse)
            };
        }
        if request.base_revision != self.head.revision
            || request.base_hash != self.head.content_hash()?
        {
            return Err(SessionError::Stale);
        }
        if self.audit.len() >= 1024 {
            return Err(SessionError::HistoryLimit);
        }
        let mut after = match &request.action {
            WorkspaceAction::CreateProject { id, name } => {
                self.head.with_project(*id, name.clone())?
            }
            WorkspaceAction::CreateModule {
                project_id,
                id,
                name,
            } => self.head.with_module(*project_id, *id, name.clone())?,
            WorkspaceAction::AttachZone {
                project_id,
                module_id,
                reference,
            } => {
                let mut head = self.head.clone();
                let module = head
                    .projects
                    .iter_mut()
                    .find(|p| p.id == *project_id)
                    .and_then(|p| p.modules.iter_mut().find(|m| m.id == *module_id))
                    .ok_or(WorkspaceError::MissingProject)?;
                module.zones.push(reference.clone());
                head.validate()?;
                head
            }
            WorkspaceAction::UpdateZone {
                project_id,
                module_id,
                before,
                after,
            } => {
                let mut head = self.head.clone();
                let zone = head
                    .projects
                    .iter_mut()
                    .find(|p| p.id == *project_id)
                    .and_then(|p| p.modules.iter_mut().find(|m| m.id == *module_id))
                    .and_then(|m| m.zones.iter_mut().find(|z| z.area_id == before.area_id))
                    .ok_or(WorkspaceError::MissingProject)?;
                if zone != before || before.area_id != after.area_id || before == after {
                    return Err(SessionError::Stale);
                }
                *zone = after.clone();
                head.validate()?;
                head
            }
            WorkspaceAction::Undo => self
                .undo
                .last()
                .cloned()
                .ok_or(SessionError::EmptyHistory)?,
            WorkspaceAction::Redo => self
                .redo
                .last()
                .cloned()
                .ok_or(SessionError::EmptyHistory)?,
        };
        after.revision = self.head.revision.next()?;
        if self.history_bytes + after.to_bytes()?.len() + self.head.to_bytes()?.len()
            > 32 * 1024 * 1024
        {
            return Err(SessionError::HistoryLimit);
        }
        Ok(WorkspaceReceipt {
            request: request.clone(),
            before: self.head.clone(),
            after,
            audit,
        })
    }

    pub fn execute(
        &mut self,
        request: &WorkspaceRequest,
    ) -> Result<WorkspaceReceipt, SessionError> {
        self.execute_with_audit(request, AuditStamp::legacy())
    }

    pub fn execute_with_audit(
        &mut self,
        request: &WorkspaceRequest,
        audit: AuditStamp,
    ) -> Result<WorkspaceReceipt, SessionError> {
        let receipt = self.preview_with_audit(request, audit)?;
        if self
            .audit
            .iter()
            .any(|r| r.request.command_id == request.command_id)
        {
            return Ok(receipt);
        }
        let receipt_bytes = receipt.before.to_bytes()?.len() + receipt.after.to_bytes()?.len();
        match request.action {
            WorkspaceAction::Undo => {
                self.undo.pop();
                self.redo.push(self.head.clone());
            }
            WorkspaceAction::Redo => {
                self.redo.pop();
                self.undo.push(self.head.clone());
            }
            _ => {
                self.undo.push(self.head.clone());
                self.redo.clear();
            }
        }
        self.head = receipt.after.clone();
        self.history_bytes += receipt_bytes;
        self.audit.push(receipt.clone());
        Ok(receipt)
    }

    pub fn archive_bytes(&self) -> Result<Vec<u8>, SessionError> {
        let archive = Archive {
            schema: "forge.workspace-session".into(),
            schema_version: 2,
            initial: self.initial.clone(),
            head: self.head.clone(),
            commands: self
                .audit
                .iter()
                .map(|receipt| ArchivedCommand {
                    request: receipt.request.clone(),
                    audit: receipt.audit.clone(),
                })
                .collect(),
        };
        let bytes = serde_json::to_vec(&archive).map_err(WorkspaceError::from)?;
        if bytes.len() > super::MAX_WORKSPACE_BYTES {
            return Err(SessionError::HistoryLimit);
        }
        Ok(bytes)
    }

    pub fn from_archive_bytes(bytes: &[u8]) -> Result<Self, SessionError> {
        if bytes.len() > super::MAX_WORKSPACE_BYTES {
            return Err(SessionError::HistoryLimit);
        }
        let header: Header = serde_json::from_slice(bytes).map_err(WorkspaceError::from)?;
        if header.schema != "forge.workspace-session" || !matches!(header.schema_version, 1 | 2) {
            return Err(WorkspaceError::Schema.into());
        }
        let (initial, head, commands): (WorkspaceHead, WorkspaceHead, Vec<ArchivedCommand>) =
            if header.schema_version == 1 {
                let archive: ArchiveV1 =
                    serde_json::from_slice(bytes).map_err(WorkspaceError::from)?;
                (
                    archive.initial,
                    archive.head,
                    archive
                        .commands
                        .into_iter()
                        .map(|request| ArchivedCommand {
                            request,
                            audit: AuditStamp::legacy(),
                        })
                        .collect(),
                )
            } else {
                let archive: Archive =
                    serde_json::from_slice(bytes).map_err(WorkspaceError::from)?;
                (archive.initial, archive.head, archive.commands)
            };
        if commands.len() > 1024 {
            return Err(SessionError::HistoryLimit);
        }
        let mut session = Self::new(initial)?;
        for command in &commands {
            session.execute_with_audit(&command.request, command.audit.clone())?;
        }
        if session.head != head || session.audit.len() != commands.len() {
            return Err(SessionError::Stale);
        }
        Ok(session)
    }
}

#[derive(Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct Archive {
    schema: String,
    schema_version: u32,
    initial: WorkspaceHead,
    head: WorkspaceHead,
    commands: Vec<ArchivedCommand>,
}

#[derive(Deserialize)]
struct Header {
    schema: String,
    schema_version: u32,
}

#[derive(Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct ArchivedCommand {
    request: WorkspaceRequest,
    #[serde(default, skip_serializing_if = "AuditStamp::is_legacy")]
    audit: AuditStamp,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct ArchiveV1 {
    #[serde(rename = "schema")]
    _schema: String,
    #[serde(rename = "schema_version")]
    _schema_version: u32,
    initial: WorkspaceHead,
    head: WorkspaceHead,
    commands: Vec<WorkspaceRequest>,
}
