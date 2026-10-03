//! Engine-neutral workspace head (ADR-0016). No filesystem or implicit adoption.
use forge_core::{AreaId, ContentHash, ModuleId, ProjectId, Revision, canonical_json_sha256};
use serde::{Deserialize, Serialize};
use std::collections::BTreeSet;
use thiserror::Error;

#[cfg(not(target_arch = "wasm32"))]
mod atomic_publish;
#[cfg(not(target_arch = "wasm32"))]
pub mod file_session;
pub mod history;
#[cfg(not(target_arch = "wasm32"))]
pub mod objects;
#[cfg(not(target_arch = "wasm32"))]
pub mod ownership;
pub mod session;
#[cfg(not(target_arch = "wasm32"))]
pub mod source_blobs;

pub const MAX_WORKSPACE_BYTES: usize = 4 * 1024 * 1024;
pub const MAX_PROJECTS: usize = 64;
pub const MAX_MODULES: usize = 256;
pub const MAX_ZONES: usize = 4096;
pub const MAX_NAME_BYTES: usize = 256;
const SCHEMA: &str = "forge.workspace";

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorkspaceHead {
    pub schema: String,
    pub schema_version: u32,
    pub revision: Revision,
    pub projects: Vec<ProjectEntry>,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ProjectEntry {
    pub id: ProjectId,
    pub name: String,
    pub modules: Vec<ModuleEntry>,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleEntry {
    pub id: ModuleId,
    pub name: String,
    pub zones: Vec<ZoneReference>,
}

/// Hashes, never a user-controlled filesystem path. Storage verifies the archive
/// actually contains this area and belongs to its enclosing project before save.
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ZoneReference {
    pub area_id: AreaId,
    pub archive_hash: ContentHash,
    pub head_hash: ContentHash,
}

#[derive(Debug, Error)]
pub enum WorkspaceError {
    #[error("unsupported workspace schema or revision")]
    Schema,
    #[error("workspace limit exceeded: {0}")]
    Limit(&'static str),
    #[error("workspace names must be trimmed, nonempty and without control characters")]
    Name,
    #[error("duplicate workspace identity or sibling name")]
    Duplicate,
    #[error("project is absent from this workspace")]
    MissingProject,
    #[error("invalid workspace JSON: {0}")]
    Json(#[from] serde_json::Error),
    #[error("cannot hash workspace: {0}")]
    Hash(#[from] forge_core::CanonicalJsonError),
}

impl Default for WorkspaceHead {
    fn default() -> Self {
        Self {
            schema: SCHEMA.into(),
            schema_version: 1,
            revision: Revision::INITIAL,
            projects: Vec::new(),
        }
    }
}

impl WorkspaceHead {
    pub fn validate(&self) -> Result<(), WorkspaceError> {
        if self.schema != SCHEMA || self.schema_version != 1 || self.revision.get() == 0 {
            return Err(WorkspaceError::Schema);
        }
        if self.projects.len() > MAX_PROJECTS {
            return Err(WorkspaceError::Limit("projects"));
        }
        let mut projects = BTreeSet::new();
        let mut project_names = BTreeSet::new();
        let mut modules = BTreeSet::new();
        let mut areas = BTreeSet::new();
        for project in &self.projects {
            validate_name(&project.name)?;
            if !projects.insert(project.id) || !project_names.insert(&project.name) {
                return Err(WorkspaceError::Duplicate);
            }
            let mut module_names = BTreeSet::new();
            for module in &project.modules {
                validate_name(&module.name)?;
                if !modules.insert(module.id) || !module_names.insert(&module.name) {
                    return Err(WorkspaceError::Duplicate);
                }
                if modules.len() > MAX_MODULES {
                    return Err(WorkspaceError::Limit("modules"));
                }
                for zone in &module.zones {
                    if !areas.insert(zone.area_id) {
                        return Err(WorkspaceError::Duplicate);
                    }
                    if areas.len() > MAX_ZONES {
                        return Err(WorkspaceError::Limit("zones"));
                    }
                }
            }
        }
        Ok(())
    }

    pub fn from_bytes(bytes: &[u8]) -> Result<Self, WorkspaceError> {
        if bytes.len() > MAX_WORKSPACE_BYTES {
            return Err(WorkspaceError::Limit("bytes"));
        }
        let head: Self = serde_json::from_slice(bytes)?;
        head.validate()?;
        Ok(head)
    }

    pub fn to_bytes(&self) -> Result<Vec<u8>, WorkspaceError> {
        self.validate()?;
        let bytes = serde_json::to_vec(self)?;
        if bytes.len() > MAX_WORKSPACE_BYTES {
            return Err(WorkspaceError::Limit("bytes"));
        }
        Ok(bytes)
    }

    pub fn content_hash(&self) -> Result<ContentHash, WorkspaceError> {
        self.validate()?;
        Ok(canonical_json_sha256(self)?)
    }

    /// Pure proposal: callers must route acceptance through the command session.
    pub fn with_project(&self, id: ProjectId, name: String) -> Result<Self, WorkspaceError> {
        let mut proposed = self.clone();
        proposed.projects.push(ProjectEntry {
            id,
            name,
            modules: Vec::new(),
        });
        proposed.validate()?;
        Ok(proposed)
    }

    pub fn with_module(
        &self,
        project_id: ProjectId,
        id: ModuleId,
        name: String,
    ) -> Result<Self, WorkspaceError> {
        let mut proposed = self.clone();
        let project = proposed
            .projects
            .iter_mut()
            .find(|p| p.id == project_id)
            .ok_or(WorkspaceError::MissingProject)?;
        project.modules.push(ModuleEntry {
            id,
            name,
            zones: Vec::new(),
        });
        proposed.validate()?;
        Ok(proposed)
    }
}

fn validate_name(name: &str) -> Result<(), WorkspaceError> {
    if name.len() > MAX_NAME_BYTES {
        return Err(WorkspaceError::Limit("name bytes"));
    }
    if name.is_empty() || name.trim() != name || name.chars().any(char::is_control) {
        return Err(WorkspaceError::Name);
    }
    Ok(())
}
