use super::{AuthoringHost, session::ActiveSession};
use crate::audit::AuditStamp;
use crate::zone_authoring::session::{ZoneReceipt, ZoneRequest};
use forge_core::{AreaId, CommandId, canonical_json_sha256};

impl AuthoringHost {
    pub(super) fn managed_area(&self) -> Option<AreaId> {
        match &self.active {
            Some(ActiveSession::Managed { session, .. }) => {
                Some(session.head().base.terrain.area_id)
            }
            _ => None,
        }
    }

    pub(super) fn open_managed(&mut self, area: AreaId) -> Result<(), String> {
        if self.managed_area().is_none() {
            self.require_closed()?;
        }
        if self.managed_tabs.len() >= 16 && !self.managed_tabs.iter().any(|t| t.area_id == area) {
            return Err("Close a zone tab before opening more than 16".into());
        }
        let workspace = self
            .workspace
            .as_ref()
            .ok_or("Open workspace before its managed zone")?;
        let session = workspace.read_zone(area)?;
        let name = session.head().effective()?.0.name;
        if let Some(tab) = self.managed_tabs.iter_mut().find(|t| t.area_id == area) {
            tab.name = name;
        } else {
            self.managed_tabs.push(crate::authoring::ManagedZoneTab {
                area_id: area,
                name,
                dirty: workspace.zone_is_dirty(area),
            });
        }
        self.active = Some(ActiveSession::Managed {
            session: Box::new(session),
            dirty: workspace.zone_is_dirty(area),
        });
        self.managed_review = None;
        self.managed_compaction = None;
        Ok(())
    }

    pub(super) fn zone_command(
        &mut self,
        request: &ZoneRequest,
        preview: bool,
    ) -> Result<ZoneReceipt, String> {
        let audit = self
            .pending_audit
            .clone()
            .unwrap_or_else(AuditStamp::legacy);
        self.validate_zone_candidate(request)?;
        let Some(area) = self.managed_area() else {
            return if preview {
                self.zone()?.session().preview_with_audit(request, audit)
            } else {
                self.zone()?.execute_with_audit(request, audit)
            };
        };
        let workspace = self
            .workspace
            .as_mut()
            .ok_or("Managed workspace is unavailable")?;
        let history_action = matches!(
            request.action,
            crate::zone_authoring::session::ZoneAction::Undo
                | crate::zone_authoring::session::ZoneAction::Redo
        );
        let already_reviewed = self
            .managed_review
            .as_ref()
            .is_some_and(|(zone, _, _)| zone == request);
        if preview || (history_action && !already_reviewed) {
            let key = canonical_json_sha256(request).map_err(|e| e.to_string())?;
            let (review, receipt) = workspace.preview_zone_edit_with_audit(
                area,
                request,
                CommandId::deterministic(format!("managed/{key}")),
                audit.clone(),
            )?;
            self.managed_review = Some((request.clone(), review.request, audit.clone()));
            if preview {
                return Ok(receipt);
            }
        }
        let (reviewed_zone, reviewed_workspace, reviewed_audit) = self
            .managed_review
            .as_ref()
            .ok_or("Review managed zone edit before Apply")?;
        if reviewed_zone != request {
            return Err("Managed zone command differs from reviewed proposal".into());
        }
        let (_, receipt) = workspace.apply_zone_edit_with_audit(
            reviewed_workspace,
            request,
            reviewed_audit.clone(),
        )?;
        let session = workspace.read_zone(area)?;
        if let Some(tab) = self.managed_tabs.iter_mut().find(|t| t.area_id == area) {
            tab.name = session.head().effective()?.0.name;
        }
        self.active = Some(ActiveSession::Managed {
            session: Box::new(session),
            dirty: workspace.zone_is_dirty(area),
        });
        Ok(receipt)
    }

    fn validate_zone_candidate(&self, request: &ZoneRequest) -> Result<(), String> {
        let session = match &self.active {
            Some(ActiveSession::Zone(file)) => file.session(),
            Some(ActiveSession::Managed { session, .. }) => session,
            _ => return Err("Open a zone before editing it".into()),
        };
        let receipt = session.preview(request)?;
        // A replay acknowledges an already accepted command, not a new mutation.
        if receipt.resulting_revision <= session.head().revision {
            return Ok(());
        }
        let mut candidate = session.head().clone();
        candidate.version = receipt.applied.schema_version();
        candidate.corrections = receipt.applied;
        if let Some(change) = receipt.base_change {
            candidate.base = change.applied;
        }
        candidate.revision = receipt.resulting_revision;
        if self.managed_area().is_some()
            && candidate
                .corrections
                .gameplay
                .as_ref()
                .is_some_and(|gameplay| Some(gameplay.module_id) != self.gameplay_module_id())
        {
            return Err("Gameplay must belong to the module that owns this managed zone".into());
        }
        self.zone_validator
            .map_or(Ok(()), |validate| validate(&candidate))
    }

    pub(super) fn save_managed(&mut self) -> Result<(), String> {
        self.workspace
            .as_mut()
            .ok_or("Managed workspace is unavailable")?
            .save()?;
        if let Some(ActiveSession::Managed { dirty, .. }) = &mut self.active {
            *dirty = false;
        }
        Ok(())
    }

    pub(super) fn managed_tab_state(&self) -> Vec<crate::authoring::ManagedZoneTab> {
        self.managed_tabs
            .iter()
            .map(|tab| {
                let mut tab = tab.clone();
                tab.dirty = self
                    .workspace
                    .as_ref()
                    .is_some_and(|w| w.zone_is_dirty(tab.area_id));
                if self.managed_area() == Some(tab.area_id)
                    && let Some(active) = &self.active
                    && let Ok(document) = active.document()
                {
                    tab.name = document.name;
                }
                tab
            })
            .collect()
    }
}
