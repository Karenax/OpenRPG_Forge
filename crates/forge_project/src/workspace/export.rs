//! Bounded read-only module export input from the existing workspace authority.
use super::{file_session::FileWorkspaceSession, launcher::DerivedGameManifest};
use crate::zone_authoring::session::ZoneEditSession;
use forge_core::{AreaId, ContentHash, ModuleId, ProjectId};
use forge_gameplay::ModuleCreatorProfile;
use std::{collections::BTreeMap, path::Path};

pub const MAX_MODULE_EXPORT_SOURCE_BYTES: usize = 256 * 1024 * 1024;

/// Small, read-only plan for one saved project. Module archives and their
/// assets are materialized one at a time through the existing snapshot path.
pub struct GameExportPlan {
    pub workspace_hash: ContentHash,
    pub game: DerivedGameManifest,
    pub modules: Vec<GameExportModule>,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct GameExportModule {
    pub module_id: ModuleId,
    pub name: String,
    pub origin: Option<super::ModuleOriginPin>,
    pub primary_area: AreaId,
    pub primary_head: ContentHash,
}

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
    /// Native snapshot only, never serialized to the `WebView` or canonical head.
    /// Archives pin these immutable bytes, including retained edit history.
    pub source_blobs: BTreeMap<ContentHash, Vec<u8>>,
}

impl ModuleExportSnapshot {
    pub fn source_requirements(&self) -> Result<BTreeMap<ContentHash, usize>, String> {
        if self.zones.is_empty() || self.zones.len() > crate::module_package::MAX_MODULE_ZONES {
            return Err("Module export area budget exceeded".into());
        }
        let mut remaining = MAX_MODULE_EXPORT_SOURCE_BYTES;
        let mut sources = BTreeMap::new();
        for zone in &self.zones {
            remaining = remaining
                .checked_sub(zone.bytes.len())
                .ok_or("Module export archive/source budget exceeded")?;
            let archive = ZoneEditSession::from_archive_bytes(&zone.bytes)?;
            for (hash, length) in archive.required_source_blobs()? {
                if let Some(previous) = sources.get(&hash) {
                    if *previous != length {
                        return Err("Conflicting lengths for a shared module source".into());
                    }
                } else {
                    if length == 0 || length > super::source_blobs::MAX_SOURCE_BLOB_BYTES {
                        return Err("Module source blob exceeds its size budget".into());
                    }
                    remaining = remaining
                        .checked_sub(length)
                        .ok_or("Module export archive/source budget exceeded")?;
                    sources.insert(hash, length);
                }
            }
        }
        Ok(sources)
    }

    pub(crate) fn capture_sources(&mut self, authority: &Path) -> Result<(), String> {
        let requirements = self.source_requirements()?;
        if requirements.is_empty() {
            self.source_blobs.clear();
            return Ok(());
        }
        let store = super::source_blobs::SourceBlobStore::open_existing(authority)?;
        let mut sources = BTreeMap::new();
        for (hash, length) in requirements {
            sources.insert(hash, store.read(hash, length)?);
        }
        self.source_blobs = sources;
        Ok(())
    }

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
    /// Bind a game's export intent to the saved workspace and existing module
    /// references. This does not make any claim about assets, rights or a kit.
    pub fn game_export_plan(
        &self,
        project_id: ProjectId,
        expected: ContentHash,
    ) -> Result<GameExportPlan, String> {
        self.require_saved_unchanged()?;
        let head = self.session().head();
        if head.content_hash().map_err(|error| error.to_string())? != expected {
            return Err("Workspace changed since the game export was reviewed".into());
        }
        let game = head
            .derive_game_manifest(project_id)
            .map_err(|error| error.to_string())?;
        let project = head
            .projects
            .iter()
            .find(|project| project.id == project_id)
            .ok_or("Game project is absent from the workspace")?;
        let mut modules = Vec::with_capacity(game.configuration().contents.len());
        for selected in &game.configuration().contents {
            let module = project
                .modules
                .iter()
                .find(|module| module.id == selected.module_id)
                .ok_or("Selected module is absent from the game project")?;
            let primary = if selected.module_id == game.configuration().entry.module_id {
                game.configuration().entry.area_id
            } else {
                *selected
                    .areas
                    .first()
                    .ok_or("Selected module has no areas")?
            };
            let area = module
                .zones
                .iter()
                .find(|zone| zone.area_id == primary)
                .ok_or("Primary area is absent from the selected module")?;
            modules.push(GameExportModule {
                module_id: module.id,
                name: module.name.clone(),
                origin: module.origin,
                primary_area: area.area_id,
                primary_head: area.head_hash,
            });
        }
        Ok(GameExportPlan {
            workspace_hash: expected,
            game,
            modules,
        })
    }

    /// Only modules selected by this exact saved game may be materialized.
    pub fn game_export_module(
        &self,
        project: ProjectId,
        module: ModuleId,
        expected: ContentHash,
    ) -> Result<ModuleExportSnapshot, String> {
        let plan = self.game_export_plan(project, expected)?;
        let selected = plan
            .modules
            .iter()
            .find(|selected| selected.module_id == module)
            .ok_or("Module is not selected for this game")?;
        self.module_export_snapshot(
            module,
            selected.primary_area,
            selected.primary_head,
            expected,
        )
    }

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
        let mut snapshot = ModuleExportSnapshot {
            module_id: module,
            project_id: project.id,
            primary_area: primary,
            primary_head,
            workspace_hash: Some(expected),
            creator,
            zones,
            source_blobs: BTreeMap::new(),
        };
        self.read_export_sources(&mut snapshot)?;
        Ok(snapshot)
    }
}
