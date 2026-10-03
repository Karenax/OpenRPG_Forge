use super::{AuthoringHost, session::ActiveSession};
use forge_core::ModuleId;

impl AuthoringHost {
    pub(super) fn gameplay_module_id(&self) -> Option<ModuleId> {
        if let Some(area) = self.managed_area() {
            return self
                .workspace
                .as_ref()?
                .session()
                .head()
                .projects
                .iter()
                .flat_map(|project| &project.modules)
                .find(|module| module.zones.iter().any(|zone| zone.area_id == area))
                .map(|module| module.id);
        }
        let active = self.active.as_ref()?;
        if let Some(document) = active
            .head()
            .and_then(|head| head.corrections.gameplay.as_deref())
        {
            return Some(document.module_id);
        }
        let terrain = ActiveSession::document(active).ok()?;
        Some(ModuleId::deterministic(format!(
            "standalone-module/{}",
            terrain.project_id
        )))
    }
}
