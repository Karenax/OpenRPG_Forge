//! Build the module definition from all retained data, including hidden areas.
use super::FileWorkspaceSession;
use crate::zone_authoring::session::ZoneEditSession;
use forge_core::{ContentHash, ModuleId};
use forge_gameplay::{CreatorSurface, GameplayCapability, ModuleCreatorProfile, RulesProfile};
use std::collections::BTreeSet;

impl FileWorkspaceSession {
    pub fn build_module_creator(
        &self,
        module: ModuleId,
        expected: ContentHash,
        profile: RulesProfile,
        surfaces: Vec<CreatorSurface>,
        capabilities: Vec<GameplayCapability>,
    ) -> Result<ModuleCreatorProfile, String> {
        if self
            .session
            .head()
            .content_hash()
            .map_err(|error| error.to_string())?
            != expected
        {
            return Err("Workspace changed since the Creator inspector was read".into());
        }
        let requested_count = capabilities.len();
        let requested: BTreeSet<_> = capabilities.into_iter().collect();
        if requested.len() != requested_count {
            return Err("Duplicate module capability".into());
        }
        let mut definition = ModuleCreatorProfile::for_documents(module, profile, surfaces, &[])
            .map_err(|error| error.to_string())?;
        definition.required_capabilities.extend(requested);
        definition.required_capabilities.sort_unstable();
        definition.required_capabilities.dedup();
        definition.validate().map_err(|error| error.to_string())?;
        let (project, module) = self
            .session
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
        let mut required: BTreeSet<_> = definition.required_capabilities.iter().copied().collect();
        let mut remaining = 256 * 1024 * 1024;
        for reference in &module.zones {
            let bytes = self
                .objects
                .read_with_budget(project.id, reference, remaining)?;
            remaining -= bytes.len();
            let zone = ZoneEditSession::from_archive_bytes(&bytes)?;
            if let Some(document) = zone.head().corrections.gameplay.as_deref() {
                if document.module_id != module.id {
                    return Err("Gameplay belongs to another module".into());
                }
                required.extend(document.used_capabilities());
            }
        }
        definition.required_capabilities = required.into_iter().collect();
        definition.validate().map_err(|error| error.to_string())?;
        Ok(definition)
    }
}
