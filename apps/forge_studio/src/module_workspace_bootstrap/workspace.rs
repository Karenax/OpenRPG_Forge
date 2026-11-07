use forge_delivery::module_runtime::LoadedModuleZone;
use forge_project::{
    audit::AuditStamp,
    workspace::{ModuleOriginPin, file_session::FileWorkspaceSession},
};
use std::path::Path;

pub(super) fn seed(
    root: &Path,
    loaded: &LoadedModuleZone,
    pin: ModuleOriginPin,
) -> Result<(), String> {
    let mut workspace = if root
        .join("workspace-v1")
        .try_exists()
        .map_err(|error| error.to_string())?
    {
        FileWorkspaceSession::open(root)?
    } else {
        FileWorkspaceSession::create(root)?
    };
    let read_payload = |path: &str, limit| loaded.read_payload(path, limit);
    let prepared = workspace.prepare_module_import(
        &loaded.manifest,
        pin,
        AuditStamp::legacy(),
        read_payload,
    )?;
    workspace.apply_module_import(prepared, read_payload)?;
    workspace.save()
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
