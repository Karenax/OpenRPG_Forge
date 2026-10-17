//! Export requests identify the current document, never a filesystem destination.
use forge_core::{AreaId, ContentHash, ModuleId};
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleExportRequest {
    pub schema_version: u32,
    pub expected_head: ContentHash,
    pub name: String,
    pub version: String,
    pub publisher: String,
    pub license_text: String,
    pub redistributable: bool,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleExportResult {
    pub module_id: ModuleId,
    pub area_id: AreaId,
    pub source_hash: ContentHash,
    pub package_sha256: ContentHash,
    pub package_path: String,
    pub redistributable: bool,
}
