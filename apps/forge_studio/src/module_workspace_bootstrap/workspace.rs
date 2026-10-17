use forge_core::{CommandId, bytes_sha256, document_store::DocumentStore};
use forge_delivery::module_runtime::LoadedModuleZone;
use forge_project::workspace::{
    ModuleOriginPin,
    file_session::FileWorkspaceSession,
    session::{WorkspaceAction, WorkspaceRequest},
};
use std::{fs, path::Path};

pub(super) fn seed(
    root: &Path,
    loaded: &LoadedModuleZone,
    pin: ModuleOriginPin,
) -> Result<(), String> {
    let area = loaded.zone.head().base.terrain.area_id;
    let directory = root.join(area.to_string());
    fs::create_dir(&directory).map_err(|error| error.to_string())?;
    let store = DocumentStore::open(&directory).map_err(|error| error.to_string())?;
    store
        .save(None, &loaded.archive)
        .map_err(|error| error.to_string())?;
    drop(store);
    let mut workspace = FileWorkspaceSession::create(root)?;
    for (label, action) in [
        (
            "project",
            WorkspaceAction::CreateProject {
                id: loaded.manifest.project_id,
                name: loaded.manifest.name.clone(),
            },
        ),
        (
            "module",
            WorkspaceAction::ImportModule {
                project_id: loaded.manifest.project_id,
                id: loaded.manifest.module_id,
                name: loaded.manifest.name.clone(),
                origin: pin,
            },
        ),
    ] {
        workspace.execute(&WorkspaceRequest {
            command_id: CommandId::deterministic(format!(
                "module-bootstrap/{}/{label}",
                pin.package_sha256
            )),
            base_revision: workspace.session().head().revision,
            base_hash: workspace
                .session()
                .head()
                .content_hash()
                .map_err(|error| error.to_string())?,
            action,
        })?;
    }
    let review = workspace.review_adoption(
        area,
        loaded.manifest.project_id,
        loaded.manifest.module_id,
        bytes_sha256(&loaded.archive),
    )?;
    workspace.adopt(&review.request)?;
    workspace.save()?;
    Ok(())
}

pub(super) fn verify(
    root: &Path,
    loaded: &LoadedModuleZone,
    pin: ModuleOriginPin,
) -> Result<(), String> {
    let workspace = FileWorkspaceSession::open(root)?;
    let module = workspace
        .session()
        .head()
        .projects
        .iter()
        .find(|project| project.id == loaded.manifest.project_id)
        .and_then(|project| {
            project
                .modules
                .iter()
                .find(|module| module.id == loaded.manifest.module_id)
        })
        .ok_or("Imported module absent after workspace reopen")?;
    if module.origin != Some(pin) {
        return Err("Canonical module origin changed during workspace creation".into());
    }
    let reopened = workspace.read_zone(loaded.zone.head().base.terrain.area_id)?;
    if reopened.archive_bytes()? != loaded.archive
        || reopened.head().content_hash()? != loaded.zone.head().content_hash()?
    {
        return Err("Canonical zone archive changed during workspace creation".into());
    }
    Ok(())
}
