//! Project/module application protocol: no UI paths or filesystem handles.
use crate::workspace::{
    WorkspaceHead,
    session::{WorkspaceReceipt, WorkspaceRequest},
};
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "action", rename_all = "snake_case", deny_unknown_fields)]
pub enum WorkspaceHostRequest {
    Create,
    Open,
    Inspect,
    /// Open an existing workspace if needed, then return its module/map browser.
    /// Never creates, saves, adopts or replaces an active zone.
    Browse,
    ReadHistory {
        expected_hash: forge_core::ContentHash,
        offset: usize,
    },
    ReviewRecovery,
    ReviewNewZone {
        module_id: forge_core::ModuleId,
        document: Box<crate::TerrainDocument>,
    },
    CreateNewZone {
        request: Box<WorkspaceRequest>,
        document: Box<crate::TerrainDocument>,
    },
    Recover {
        expected_saved: Option<forge_core::ContentHash>,
        expected_pending: forge_core::ContentHash,
        promote: bool,
    },
    ReviewAdoption {
        area_id: forge_core::AreaId,
        project_id: forge_core::ProjectId,
        module_id: forge_core::ModuleId,
        expected_archive_hash: forge_core::ContentHash,
    },
    Preview {
        request: Box<WorkspaceRequest>,
    },
    Apply {
        request: Box<WorkspaceRequest>,
    },
    Adopt {
        request: Box<WorkspaceRequest>,
    },
    Save,
    Close,
}

impl WorkspaceHostRequest {
    #[must_use]
    pub const fn persists_audit_receipt(&self) -> bool {
        matches!(
            self,
            Self::Apply { .. } | Self::Adopt { .. } | Self::CreateNewZone { .. }
        )
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorkspaceHostState {
    pub head: Option<WorkspaceHead>,
    #[serde(default)]
    pub modules: Vec<WorkspaceModuleSummary>,
    pub dirty: bool,
    pub receipt: Option<WorkspaceReceipt>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub recovery: Option<WorkspaceRecoveryReview>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub history: Option<crate::workspace::history::WorkspaceHistoryPage>,
}

/// Derived labels bound to accepted managed-zone references, never another authority.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorkspaceModuleSummary {
    pub id: forge_core::ModuleId,
    pub name: String,
    pub project_name: String,
    pub maps: Vec<WorkspaceMapSummary>,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorkspaceMapSummary {
    pub area_id: forge_core::AreaId,
    pub name: String,
    pub dirty: bool,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorkspaceRecoveryVersion {
    pub archive_hash: forge_core::ContentHash,
    pub head: Option<WorkspaceHead>,
    pub error: Option<String>,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorkspaceRecoveryReview {
    pub saved: Option<WorkspaceRecoveryVersion>,
    pub pending: WorkspaceRecoveryVersion,
}
