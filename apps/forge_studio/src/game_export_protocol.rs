//! The Studio may select a project and metadata, never a kit or output path.
use forge_core::{ContentHash, ProjectId};
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct GameExportRequest {
    pub schema_version: u32,
    pub project_id: ProjectId,
    pub expected_workspace_hash: ContentHash,
    pub version: String,
    pub publisher: String,
    pub license_text: String,
}

impl GameExportRequest {
    pub fn validate(&self) -> Result<(), String> {
        if self.schema_version != 1
            || self.version.trim().is_empty()
            || self.version.len() > 128
            || self.publisher.trim().is_empty()
            || self.publisher.len() > 256
            || self.license_text.trim().is_empty()
            || self.license_text.len() > 32 * 1024
            || self.version.chars().any(char::is_control)
            || self.publisher.chars().any(char::is_control)
            || self
                .license_text
                .chars()
                .any(|c| c.is_control() && !matches!(c, '\n' | '\r' | '\t'))
        {
            return Err(
                "Précisez une version, un auteur et des conditions valides pour le jeu.".into(),
            );
        }
        Ok(())
    }
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct GameExportResult {
    pub directory: String,
    pub application_sha256: ContentHash,
    pub module_count: usize,
    pub workspace_sha256: ContentHash,
    pub installation_verified: bool,
    pub distribution_qualified: bool,
}
