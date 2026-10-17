use super::AuthoringHost;
use crate::workspace::ModuleOriginPin;
use forge_core::ContentHash;

impl AuthoringHost {
    pub fn game_module_origin(
        &self,
        expected_head: ContentHash,
    ) -> Result<Option<ModuleOriginPin>, String> {
        let active = self
            .active
            .as_ref()
            .ok_or("Open a module zone before reading its origin")?;
        active.playtest_snapshot(expected_head)?;
        let Some(area) = self.managed_area() else {
            return Ok(None);
        };
        let workspace = self
            .workspace
            .as_ref()
            .ok_or("Managed workspace is unavailable")?;
        workspace
            .session()
            .head()
            .validate()
            .map_err(|error| error.to_string())?;
        let module = workspace
            .session()
            .head()
            .projects
            .iter()
            .flat_map(|project| &project.modules)
            .find(|module| module.zones.iter().any(|zone| zone.area_id == area))
            .ok_or("Managed area has no current owning module")?;
        if !module
            .zones
            .iter()
            .any(|zone| zone.area_id == area && zone.head_hash == expected_head)
        {
            return Err("Managed module reference differs from the active zone snapshot".into());
        }
        module
            .origin
            .map(|pin| {
                pin.validate()
                    .map(|()| pin)
                    .map_err(|error| error.to_string())
            })
            .transpose()
    }
}
