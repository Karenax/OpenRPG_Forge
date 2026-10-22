//! Bounded read-only module export input from the existing workspace authority.
use super::file_session::FileWorkspaceSession;
use crate::zone_authoring::session::ZoneEditSession;
use forge_core::{AreaId, ContentHash, ModuleId, ProjectId};
use forge_gameplay::ModuleCreatorProfile;

pub struct ModuleAreaArchive {
    pub area_id: AreaId,
    pub head_hash: ContentHash,
    pub bytes: Vec<u8>,
}

pub struct ModuleExportSnapshot {
    pub module_id: ModuleId,
    pub project_id: ProjectId,
    pub primary_area: AreaId,
    pub primary_head: ContentHash,
    pub workspace_hash: Option<ContentHash>,
    pub creator: Option<ModuleCreatorProfile>,
    pub zones: Vec<ModuleAreaArchive>,
}

impl ModuleExportSnapshot {
    pub fn content_hash(&self) -> Result<ContentHash, String> {
        let creator = self
            .creator
            .as_ref()
            .map(|definition| {
                let bytes = definition.to_bytes().map_err(|error| error.to_string())?;
                Ok::<_, String>(super::ModuleCreatorReference {
                    sha256: forge_core::bytes_sha256(&bytes),
                    bytes: bytes.len() as u64,
                })
            })
            .transpose()?;
        let mut zones: Vec<_> = self
            .zones
            .iter()
            .map(|zone| super::ZoneReference {
                area_id: zone.area_id,
                head_hash: zone.head_hash,
                archive_hash: forge_core::bytes_sha256(&zone.bytes),
            })
            .collect();
        zones.sort_by_key(|zone| zone.area_id);
        forge_core::canonical_json_sha256(&(self.project_id, self.module_id, creator, zones))
            .map_err(|error| error.to_string())
    }
}

impl FileWorkspaceSession {
    pub fn module_content_hash(&self, module: ModuleId) -> Result<ContentHash, String> {
        let (project, entry) = self
            .session()
            .head()
            .projects
            .iter()
            .find_map(|project| {
                project
                    .modules
                    .iter()
                    .find(|entry| entry.id == module)
                    .map(|entry| (project, entry))
            })
            .ok_or("Module is absent from workspace")?;
        let mut zones = entry.zones.clone();
        zones.sort_by_key(|zone| zone.area_id);
        forge_core::canonical_json_sha256(&(project.id, module, entry.creator, zones))
            .map_err(|error| error.to_string())
    }
    pub fn module_export_snapshot(
        &self,
        module: ModuleId,
        primary: AreaId,
        primary_head: ContentHash,
        expected: ContentHash,
    ) -> Result<ModuleExportSnapshot, String> {
        if self
            .session()
            .head()
            .content_hash()
            .map_err(|error| error.to_string())?
            != expected
        {
            return Err("Module changed since the export was reviewed".into());
        }
        let (project, entry) = self
            .session()
            .head()
            .projects
            .iter()
            .find_map(|project| {
                project
                    .modules
                    .iter()
                    .find(|entry| entry.id == module)
                    .map(|entry| (project, entry))
            })
            .ok_or("Module is absent from workspace")?;
        if !entry
            .zones
            .iter()
            .any(|zone| zone.area_id == primary && zone.head_hash == primary_head)
        {
            return Err("Active area differs from its managed module reference".into());
        }
        let mut remaining = 256 * 1024 * 1024;
        let mut zones = Vec::new();
        let creator = self.module_creator(module)?;
        for reference in &entry.zones {
            let bytes = self.read_export_area(project.id, reference, remaining)?;
            remaining -= bytes.len();
            let zone = ZoneEditSession::from_archive_bytes(&bytes)?;
            if let Some(document) = zone.head().corrections.gameplay.as_deref() {
                if document.module_id != module {
                    return Err("Area gameplay belongs to another module".into());
                }
                if let Some(definition) = &creator {
                    definition
                        .validate_document(document)
                        .map_err(|error| error.to_string())?;
                }
            }
            zones.push(ModuleAreaArchive {
                area_id: reference.area_id,
                head_hash: reference.head_hash,
                bytes,
            });
        }
        Ok(ModuleExportSnapshot {
            module_id: module,
            project_id: project.id,
            primary_area: primary,
            primary_head,
            workspace_hash: Some(expected),
            creator,
            zones,
        })
    }
}
