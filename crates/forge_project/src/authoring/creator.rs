//! Read models for the common module operation; no writable duplicate session.
use forge_core::{AreaId, ContentHash, ModuleId};
use forge_gameplay::ModuleCreatorProfile;
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleCreatorContext {
    pub module_id: ModuleId,
    pub name: String,
    pub workspace_hash: ContentHash,
    pub module_hash: ContentHash,
    pub definition: Option<ModuleCreatorProfile>,
    pub can_undo: bool,
    pub can_redo: bool,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AffectedCreatorZone {
    pub area_id: AreaId,
    pub name: String,
}
