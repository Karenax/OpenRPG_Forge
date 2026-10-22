//! The active managed area selects the module; clients never choose a storage path.
use super::AuthoringHost;
use crate::{
    audit::AuditStamp,
    authoring::creator::{AffectedCreatorZone, ModuleCreatorContext},
    workspace::{
        ModuleCreatorReview,
        session::{WorkspaceReceipt, WorkspaceRequest},
    },
};
use forge_core::{CommandId, ContentHash, ModuleId};
use forge_gameplay::{CreatorSurface, GameplayCapability, RulesProfile};

impl AuthoringHost {
    fn creator_module(&self) -> Result<ModuleId, String> {
        self.managed_area()
            .ok_or("Open a managed area before configuring its module")?;
        self.gameplay_module_id()
            .ok_or_else(|| "Owning module is absent".into())
    }

    pub(super) fn module_creator_context(&self) -> Result<Option<ModuleCreatorContext>, String> {
        let Some(area) = self.managed_area() else {
            return Ok(None);
        };
        let module = self.creator_module()?;
        let workspace = self
            .workspace
            .as_ref()
            .ok_or("Managed workspace is unavailable")?;
        let entry = workspace
            .session()
            .head()
            .projects
            .iter()
            .flat_map(|project| &project.modules)
            .find(|entry| entry.id == module)
            .ok_or("Owning module is absent")?;
        let hash = workspace
            .session()
            .head()
            .content_hash()
            .map_err(|error| error.to_string())?;
        let can_history = |redo| {
            workspace
                .preview_module_history(
                    module,
                    hash,
                    redo,
                    CommandId::deterministic(format!("module-history-probe/{hash}/{redo}")),
                )
                .is_ok_and(|receipt| contains_area(&receipt.after, module, area))
        };
        Ok(Some(ModuleCreatorContext {
            module_id: module,
            name: entry.name.clone(),
            workspace_hash: hash,
            module_hash: workspace.module_content_hash(module)?,
            definition: workspace.module_creator(module)?,
            can_undo: can_history(false),
            can_redo: can_history(true),
        }))
    }

    pub(super) fn review_module_creator(
        &mut self,
        expected: ContentHash,
        profile: RulesProfile,
        surfaces: Vec<CreatorSurface>,
        capabilities: Vec<GameplayCapability>,
        command: CommandId,
    ) -> Result<(ModuleCreatorReview, Vec<AffectedCreatorZone>), String> {
        let module = self.creator_module()?;
        let audit = self
            .pending_audit
            .clone()
            .unwrap_or_else(AuditStamp::legacy);
        let workspace = self
            .workspace
            .as_ref()
            .ok_or("Managed workspace is unavailable")?;
        let definition =
            workspace.build_module_creator(module, expected, profile, surfaces, capabilities)?;
        let review =
            workspace.preview_module_creator_with_audit(definition, command, audit.clone())?;
        let affected = review
            .changed_areas
            .iter()
            .map(|area| {
                let zone = workspace.read_zone(*area)?;
                Ok(AffectedCreatorZone {
                    area_id: *area,
                    name: zone.head().effective()?.0.name,
                })
            })
            .collect::<Result<Vec<_>, String>>()?;
        self.module_creator_pending = Some((review.clone(), audit));
        Ok((review, affected))
    }

    pub(super) fn apply_module_creator(
        &mut self,
        review: &ModuleCreatorReview,
    ) -> Result<(), String> {
        if self.creator_module()? != review.definition.module_id {
            return Err("Creator proposal belongs to another module".into());
        }
        let workspace = self
            .workspace
            .as_mut()
            .ok_or("Managed workspace is unavailable")?;
        let accepted = workspace
            .session()
            .audit()
            .iter()
            .any(|entry| entry.request == review.request);
        let audit = if let Some((pending, audit)) = &self.module_creator_pending {
            if pending != review && !accepted {
                return Err("Review this module proposal before Apply".into());
            }
            audit.clone()
        } else if accepted {
            AuditStamp::legacy()
        } else {
            return Err("Review this module proposal before Apply".into());
        };
        workspace.apply_module_creator_with_audit(review, audit)?;
        self.module_creator_pending = None;
        self.refresh_creator_area()
    }

    pub(super) fn review_module_history(
        &mut self,
        expected: ContentHash,
        redo: bool,
        command: CommandId,
    ) -> Result<WorkspaceReceipt, String> {
        let module = self.creator_module()?;
        let area = self.managed_area().ok_or("Managed area is unavailable")?;
        let workspace = self
            .workspace
            .as_ref()
            .ok_or("Managed workspace is unavailable")?;
        let mut receipt = workspace.preview_module_history(module, expected, redo, command)?;
        if !contains_area(&receipt.after, module, area) {
            return Err("Close the active area before undoing its attachment".into());
        }
        let audit = self
            .pending_audit
            .clone()
            .unwrap_or_else(AuditStamp::legacy);
        receipt.audit = audit.clone();
        self.module_history_pending = Some((receipt.request.clone(), audit));
        Ok(receipt)
    }

    pub(super) fn apply_module_history(
        &mut self,
        request: &WorkspaceRequest,
    ) -> Result<(), String> {
        let module = self.creator_module()?;
        let area = self.managed_area().ok_or("Managed area is unavailable")?;
        let workspace = self
            .workspace
            .as_mut()
            .ok_or("Managed workspace is unavailable")?;
        let accepted = workspace
            .session()
            .audit()
            .iter()
            .any(|entry| entry.request == *request);
        if !accepted {
            let receipt = workspace
                .session()
                .preview(request)
                .map_err(|error| error.to_string())?;
            if !contains_area(&receipt.after, module, area) {
                return Err("Close the active area before undoing its attachment".into());
            }
        }
        let audit = if let Some((pending, audit)) = &self.module_history_pending {
            if pending != request && !accepted {
                return Err("Review this module history operation before Apply".into());
            }
            audit.clone()
        } else if accepted {
            AuditStamp::legacy()
        } else {
            return Err("Review this module history operation before Apply".into());
        };
        workspace.apply_module_history(module, request, audit)?;
        self.module_history_pending = None;
        self.refresh_creator_area()
    }

    fn refresh_creator_area(&mut self) -> Result<(), String> {
        let area = self.managed_area().ok_or("Managed area is unavailable")?;
        self.managed_review = None;
        self.managed_compaction = None;
        self.open_managed(area)
    }
}

fn contains_area(
    head: &crate::workspace::WorkspaceHead,
    module: ModuleId,
    area: forge_core::AreaId,
) -> bool {
    head.projects
        .iter()
        .flat_map(|project| &project.modules)
        .any(|entry| entry.id == module && entry.zones.iter().any(|zone| zone.area_id == area))
}
