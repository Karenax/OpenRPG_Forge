use super::AuthoringHost;
use crate::workspace::ModuleOriginPin;
use forge_core::ContentHash;

impl AuthoringHost {
    /// Identity of the module that owns the currently open managed area.
    pub fn managed_module_identity(&self) -> Option<forge_core::ModuleId> {
        self.managed_area()?;
        self.gameplay_module_id()
    }

    pub(super) fn observed_module_origin(&self) -> Option<ModuleOriginPin> {
        self.managed_area()?;
        let module = self.gameplay_module_id()?;
        self.workspace
            .as_ref()?
            .session()
            .head()
            .projects
            .iter()
            .flat_map(|project| &project.modules)
            .find(|entry| entry.id == module)?
            .origin
    }

    /// Resolve the opened module's immutable origin while an edit is previewed.
    pub fn current_module_origin(
        &self,
    ) -> Result<Option<(forge_core::ModuleId, ModuleOriginPin)>, String> {
        let Some((module, pin)) = self
            .managed_module_identity()
            .zip(self.observed_module_origin())
        else {
            return Ok(None);
        };
        pin.validate().map_err(|error| error.to_string())?;
        Ok(Some((module, pin)))
    }

    /// Read-only authority for module companions. No path or identity comes from the view.
    pub fn module_origin_snapshot(
        &self,
        expected_head: ContentHash,
    ) -> Result<Option<(forge_core::ModuleId, ModuleOriginPin)>, String> {
        let Some(pin) = self.game_module_origin(expected_head)? else {
            return Ok(None);
        };
        let module = self
            .gameplay_module_id()
            .ok_or("Current owning module absent")?;
        Ok(Some((module, pin)))
    }

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
