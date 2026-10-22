//! Bounded audit projection; never transports the before/after workspace snapshots.
use super::session::{WorkspaceAction, WorkspaceSession};
use crate::audit::AuditStamp;
use forge_core::{CommandId, ContentHash, Revision};
use serde::{Deserialize, Serialize};

pub const PAGE_SIZE: usize = 20;

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorkspaceHistoryEntry {
    pub command_id: CommandId,
    pub revision: Revision,
    pub action: WorkspaceAction,
    pub scope: Option<String>,
    #[serde(default, skip_serializing_if = "AuditStamp::is_legacy")]
    pub audit: AuditStamp,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorkspaceHistoryPage {
    pub head_hash: ContentHash,
    pub total: usize,
    pub offset: usize,
    pub next_offset: Option<usize>,
    pub entries: Vec<WorkspaceHistoryEntry>,
}

pub fn read(
    session: &WorkspaceSession,
    expected_hash: ContentHash,
    offset: usize,
) -> Result<WorkspaceHistoryPage, String> {
    if session.head().content_hash().map_err(|e| e.to_string())? != expected_hash {
        return Err("Workspace changed: refresh its history from the first page".into());
    }
    let total = session.audit().len();
    if offset > total {
        return Err("Workspace history offset exceeds available commands".into());
    }
    let entries: Vec<_> = session
        .audit()
        .iter()
        .rev()
        .skip(offset)
        .take(PAGE_SIZE)
        .map(|r| WorkspaceHistoryEntry {
            command_id: r.request.command_id,
            revision: r.after.revision,
            action: r.request.action.clone(),
            scope: zone_scope(r),
            audit: r.audit.clone(),
        })
        .collect();
    let next = offset + entries.len();
    Ok(WorkspaceHistoryPage {
        head_hash: expected_hash,
        total,
        offset,
        next_offset: (next < total).then_some(next),
        entries,
    })
}

fn zone_scope(receipt: &super::session::WorkspaceReceipt) -> Option<String> {
    if let WorkspaceAction::ConfigureCreator {
        project_id,
        module_id,
        ..
    } = &receipt.request.action
    {
        let project = receipt
            .after
            .projects
            .iter()
            .find(|project| project.id == *project_id)?;
        let module = project
            .modules
            .iter()
            .find(|module| module.id == *module_id)?;
        return Some(format!(
            "{} / {} / toutes les zones",
            project.name, module.name
        ));
    }
    let (project_id, module_id, area_id) = match &receipt.request.action {
        WorkspaceAction::AttachZone {
            project_id,
            module_id,
            reference,
        } => (*project_id, *module_id, reference.area_id),
        WorkspaceAction::UpdateZone {
            project_id,
            module_id,
            after,
            ..
        } => (*project_id, *module_id, after.area_id),
        _ => return None,
    };
    let project = receipt.after.projects.iter().find(|p| p.id == project_id)?;
    let module = project.modules.iter().find(|m| m.id == module_id)?;
    let ordinal = module.zones.iter().position(|z| z.area_id == area_id)? + 1;
    Some(format!(
        "{} / {} / zone {ordinal}",
        project.name, module.name
    ))
}
