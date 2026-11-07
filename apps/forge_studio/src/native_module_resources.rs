use forge_studio_frontend::{
    module_resources_native::Resources,
    module_resources_protocol::{Reply, Request},
};
use std::sync::Mutex;

static RESOURCES: Mutex<Resources> = Mutex::new(Resources::new());

pub(super) fn execute(request: Request) -> Result<Reply, String> {
    let root = forge_studio_frontend::launch_configuration::authoring_root()?
        .ok_or("Ouvrir un module avant de lire ses ressources végétales")?;
    super::with_authoring_host(|host| {
        RESOURCES
            .lock()
            .map_err(|_| "Module vegetation reader unavailable".to_owned())?
            .execute(std::path::Path::new(&root), host, request)
    })
}

pub(super) fn read_historical_model(
    root: &std::path::Path,
    asset_id: forge_core::AssetId,
    sha256: forge_core::ContentHash,
) -> Result<Option<forge_studio_frontend::asset_library_protocol::ModuleModel>, String> {
    super::with_authoring_host(|host| {
        RESOURCES
            .lock()
            .map_err(|_| "Module resource reader unavailable".to_owned())?
            .read_historical_model(root, host, asset_id, sha256)
    })
}
