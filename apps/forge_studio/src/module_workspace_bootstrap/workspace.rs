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
    let archives = read_archives(loaded)?;
    for (area, archive) in &archives {
        let directory = root.join(area.to_string());
        fs::create_dir(&directory).map_err(|error| error.to_string())?;
        let store = DocumentStore::open(&directory).map_err(|error| error.to_string())?;
        store
            .save(None, archive)
            .map_err(|error| error.to_string())?;
    }
    let mut workspace = if root
        .join("workspace-v1")
        .try_exists()
        .map_err(|error| error.to_string())?
    {
        FileWorkspaceSession::open(root)?
    } else {
        FileWorkspaceSession::create(root)?
    };
    let mut actions = Vec::new();
    if !workspace
        .session()
        .head()
        .projects
        .iter()
        .any(|project| project.id == loaded.manifest.project_id)
    {
        actions.push((
            "project",
            WorkspaceAction::CreateProject {
                id: loaded.manifest.project_id,
                name: loaded.manifest.name.clone(),
            },
        ));
    }
    actions.push((
        "module",
        WorkspaceAction::ImportModule {
            project_id: loaded.manifest.project_id,
            id: loaded.manifest.module_id,
            name: loaded.manifest.name.clone(),
            origin: pin,
        },
    ));
    for (label, action) in actions {
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
    for (area, archive) in &archives {
        let review = workspace.review_adoption(
            *area,
            loaded.manifest.project_id,
            loaded.manifest.module_id,
            bytes_sha256(archive),
        )?;
        workspace.adopt(&review.request)?;
    }
    if let Some(definition) = &loaded.manifest.creator {
        let review = workspace.preview_module_creator(
            definition.clone(),
            CommandId::deterministic(format!("module-bootstrap/{}/creator", pin.package_sha256)),
        )?;
        if !review.changed_areas.is_empty() {
            return Err("Admitted areas differ from their common Creator definition".into());
        }
        workspace.apply_module_creator(&review)?;
    }
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
    if module.zones.len() != loaded.manifest.zones.len() {
        return Err("Workspace omitted or added an admitted module area".into());
    }
    let actual = workspace
        .module_creator(module.id)?
        .map(|definition| definition.content_hash())
        .transpose()
        .map_err(|error| error.to_string())?;
    let expected = loaded
        .manifest
        .creator
        .as_ref()
        .map(forge_project::workspace::creator::ModuleCreatorProfile::content_hash)
        .transpose()
        .map_err(|error| error.to_string())?;
    if actual != expected {
        return Err("Common Creator definition changed during workspace creation".into());
    }
    for (area, archive) in read_archives(loaded)? {
        let reopened = workspace.read_zone(area)?;
        if reopened.archive_bytes()? != archive {
            return Err("Canonical zone archive changed during workspace creation".into());
        }
    }
    Ok(())
}

fn read_archives(loaded: &LoadedModuleZone) -> Result<Vec<(forge_core::AreaId, Vec<u8>)>, String> {
    let mut remaining = 256 * 1024 * 1024;
    let mut archives = Vec::new();
    for zone in &loaded.manifest.zones {
        let bytes = loaded.read_payload(
            &zone.archive_path,
            remaining.min(forge_project::zone_authoring::MAX_ZONE_HEAD_BYTES),
        )?;
        remaining -= bytes.len();
        loaded
            .manifest
            .validate_zone_archive(zone.area_id, &bytes)
            .map_err(|error| error.to_string())?;
        archives.push((zone.area_id, bytes));
    }
    Ok(archives)
}
