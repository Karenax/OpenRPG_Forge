//! Installed module discovery and launch; client requests contain stable IDs only.
use crate::{Result, archive, install, manifest, module_runtime};
use forge_core::{AreaId, ContentHash, ModuleId};
use serde::{Deserialize, Serialize};
use std::{
    collections::BTreeMap,
    fs::{self, File, OpenOptions},
    io::Read,
    path::{Path, PathBuf},
    process::{Child, Command, Stdio},
};

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleSelection {
    pub module_id: ModuleId,
    pub area_id: AreaId,
}

#[derive(Clone, Debug, Serialize)]
pub struct InstalledModule {
    pub module_id: ModuleId,
    pub name: String,
    pub version: String,
    pub package_manifest_sha256: ContentHash,
    pub zones: Vec<InstalledZone>,
}

#[derive(Clone, Debug, Serialize)]
pub struct InstalledZone {
    pub area_id: AreaId,
    pub head_hash: ContentHash,
    pub archive_sha256: ContentHash,
}

#[derive(Clone, Debug, Serialize)]
pub struct RejectedModule {
    pub pack_id: String,
    pub reason: String,
}

#[derive(Clone, Debug, Default, Serialize)]
pub struct ModuleInventory {
    pub modules: Vec<InstalledModule>,
    pub rejected: Vec<RejectedModule>,
}

#[derive(Clone, Debug, Serialize)]
pub struct ModuleLaunchPlan {
    pub selection: ModuleSelection,
    pub installation_revision: u64,
    pub installation_head_sha256: ContentHash,
    pub package_manifest_sha256: ContentHash,
    pub head_hash: ContentHash,
    pub archive_sha256: ContentHash,
    pub executable: PathBuf,
    pub executable_sha256: ContentHash,
    pub arguments: Vec<String>,
    pub working_directory: PathBuf,
    pub environment: BTreeMap<String, String>,
}

pub fn inventory(root: &Path) -> Result<ModuleInventory> {
    let (state, _) = read_state(root)?;
    install::product(root, &state.active)?;
    let mut result = ModuleInventory::default();
    for (id, pack) in &state.active.packs {
        if pack.kind != manifest::Kind::Module {
            continue;
        }
        match installed_module(root, id, pack) {
            Ok(Some(module)) => result.modules.push(module),
            Ok(None) => (),
            Err(reason) => result.rejected.push(RejectedModule {
                pack_id: id.clone(),
                reason,
            }),
        }
    }
    Ok(result)
}

pub fn plan(root: &Path, selection: ModuleSelection) -> Result<ModuleLaunchPlan> {
    let (state, installation_head_sha256) = read_state(root)?;
    let product = install::product(root, &state.active)?;
    let core = install::package_root(root, &state.active.core)?;
    let descriptor = archive::verify_directory(&core, &state.active.core.revision)?;
    let game = product
        .apps
        .get("game")
        .ok_or("Installed game entry point is absent")?;
    let executable = manifest::confined(&core, &game.executable)?;
    let executable_pin = descriptor
        .files
        .iter()
        .find(|entry| entry.path.eq_ignore_ascii_case(&game.executable))
        .ok_or("Installed game executable is absent from its inventory")?;
    let pack = state
        .active
        .packs
        .get(&selection.module_id.to_string())
        .ok_or("Requested module is absent from the active installation")?;
    if pack.id != selection.module_id.to_string() || pack.kind != manifest::Kind::Module {
        return Err("Requested installed package is not the selected module".into());
    }
    let package = install::package_root(root, pack)?;
    let package_manifest_sha256 = ContentHash::parse(&pack.revision).map_err(|e| e.to_string())?;
    let installed = module_runtime::inspect_installed(&package, &pack.revision)?;
    if installed.module_id != selection.module_id {
        return Err("Installed module identity disagrees with the requested ID".into());
    }
    let loaded = module_runtime::load(&package, selection.area_id, None)?;
    module_runtime::verify_transport_revision(&loaded, package_manifest_sha256)?;
    let head_hash = loaded.zone.head().content_hash()?;
    let archive_sha256 = forge_core::bytes_sha256(&loaded.archive);
    let user = Path::new(&state.user_root);
    let working_directory =
        manifest::confined(user, &format!("game-sessions/module-{}", crate::attempt()?))?;
    let save = manifest::confined(user, "saves")?;
    for output in [&working_directory, &save] {
        if prospective_path(output)?.starts_with(
            root.join("packages")
                .canonicalize()
                .map_err(|e| e.to_string())?,
        ) {
            return Err("Module session/save output must remain outside installed packages".into());
        }
    }
    let arguments = vec![
        "--module".into(),
        path_text(&package)?,
        "--zone".into(),
        selection.area_id.to_string(),
        "--cache".into(),
        path_text(&working_directory.join("cache"))?,
        "--save-root".into(),
        path_text(&save)?,
        "--expected-head".into(),
        head_hash.to_string(),
        "--expected-archive-sha256".into(),
        archive_sha256.to_string(),
        "--expected-package-manifest-sha256".into(),
        package_manifest_sha256.to_string(),
    ];
    let environment = environment(&working_directory)?;
    Ok(ModuleLaunchPlan {
        selection,
        installation_revision: state.revision,
        installation_head_sha256,
        package_manifest_sha256,
        head_hash,
        archive_sha256,
        executable,
        executable_sha256: ContentHash::parse(&executable_pin.sha256).map_err(|e| e.to_string())?,
        arguments,
        working_directory,
        environment,
    })
}

pub fn start(root: &Path, selection: ModuleSelection) -> Result<Child> {
    let mut child = start_with(root, selection, |mut command| {
        command.spawn().map_err(|e| e.to_string())
    })?;
    std::thread::sleep(std::time::Duration::from_millis(150));
    if let Some(status) = child.try_wait().map_err(|e| e.to_string())? {
        return Err(format!(
            "Le module s’est arrêté pendant son ouverture ({status}) ; consulter son journal dans les sessions utilisateur."
        ));
    }
    Ok(child)
}

fn start_with<T>(
    root: &Path,
    selection: ModuleSelection,
    spawn: impl FnOnce(Command) -> Result<T>,
) -> Result<T> {
    let planned = plan(root, selection)?;
    manifest::safe_chain(&planned.working_directory)?;
    fs::create_dir_all(
        planned
            .working_directory
            .parent()
            .ok_or("Missing session parent")?,
    )
    .map_err(|e| e.to_string())?;
    fs::create_dir(&planned.working_directory).map_err(|e| e.to_string())?;
    let log = OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(planned.working_directory.join("game.log"))
        .map_err(|e| e.to_string())?;
    let mut command = Command::new(&planned.executable);
    command
        .env_clear()
        .envs(&planned.environment)
        .args(&planned.arguments)
        .current_dir(&planned.working_directory)
        .stdin(Stdio::null())
        .stdout(Stdio::from(log.try_clone().map_err(|e| e.to_string())?))
        .stderr(Stdio::from(log));
    #[cfg(windows)]
    {
        use std::os::windows::process::CommandExt;
        command.creation_flags(0x0800_0000);
    }
    // Hold the same native lock as apply/uninstall throughout the final
    // admission and spawn. The child also verifies its three pins before scene setup.
    install::with_lock(root, || {
        let (state, head) = read_state(root)?;
        if head != planned.installation_head_sha256
            || state.revision != planned.installation_revision
        {
            return Err("Installation changed while preparing the module launch".into());
        }
        let pack = state
            .active
            .packs
            .get(&selection.module_id.to_string())
            .ok_or("Selected module is no longer active")?;
        let loaded =
            module_runtime::load(&install::package_root(root, pack)?, selection.area_id, None)?;
        module_runtime::verify_transport_revision(&loaded, planned.package_manifest_sha256)?;
        if loaded.zone.head().content_hash()? != planned.head_hash
            || forge_core::bytes_sha256(&loaded.archive) != planned.archive_sha256
            || crate::file_hash(&planned.executable)? != planned.executable_sha256.to_string()
        {
            return Err("Module archive or game executable changed before launch".into());
        }
        spawn(command)
    })
}

fn installed_module(
    root: &Path,
    id: &str,
    pack: &install::InstalledPack,
) -> Result<Option<InstalledModule>> {
    if id != pack.id {
        return Err("Installed pack index has a different identity".into());
    }
    let directory = install::package_root(root, pack)?;
    let descriptor = archive::verify_directory(&directory, &pack.revision)?;
    if !descriptor
        .files
        .iter()
        .any(|f| f.path == forge_project::module_package::MODULE_MANIFEST_PATH)
    {
        return Ok(None);
    }
    let module = module_runtime::inspect_installed(&directory, &pack.revision)?;
    if module.module_id.to_string() != id {
        return Err("Semantic module ID differs from the installation index".into());
    }
    Ok(Some(InstalledModule {
        module_id: module.module_id,
        name: module.name,
        version: module.module_version,
        package_manifest_sha256: ContentHash::parse(&pack.revision).map_err(|e| e.to_string())?,
        zones: module
            .zones
            .into_iter()
            .map(|zone| InstalledZone {
                area_id: zone.area_id,
                head_hash: zone.head_hash,
                archive_sha256: zone.archive_sha256,
            })
            .collect(),
    }))
}

fn read_state(root: &Path) -> Result<(install::State, ContentHash)> {
    let path = manifest::confined(root, "installation.json")?;
    let file = File::open(path).map_err(|e| e.to_string())?;
    let mut bytes = Vec::new();
    file.take(manifest::MAX_MANIFEST + 1)
        .read_to_end(&mut bytes)
        .map_err(|e| e.to_string())?;
    if bytes.len() as u64 > manifest::MAX_MANIFEST {
        return Err("Installed state exceeds its read bound".into());
    }
    let state: install::State = serde_json::from_slice(&bytes).map_err(|e| e.to_string())?;
    if state.schema != "forge.delivery.installation.v1"
        || state.receipts.len() > 1024
        || state.active.packs.len() > 1024
        || state.active.core.kind != manifest::Kind::Applications
    {
        return Err("Invalid active installation state".into());
    }
    Ok((state, forge_core::bytes_sha256(&bytes)))
}

fn prospective_path(path: &Path) -> Result<PathBuf> {
    manifest::safe_chain(path)?;
    let mut ancestor = path;
    let mut suffix = vec![];
    while !ancestor.try_exists().map_err(|e| e.to_string())? {
        suffix.push(
            ancestor
                .file_name()
                .ok_or("Missing output path ancestor")?
                .to_os_string(),
        );
        ancestor = ancestor.parent().ok_or("Missing output parent")?;
    }
    let mut result = ancestor.canonicalize().map_err(|e| e.to_string())?;
    for component in suffix.into_iter().rev() {
        result.push(component);
    }
    Ok(result)
}

fn path_text(path: &Path) -> Result<String> {
    path.to_str()
        .map(str::to_owned)
        .ok_or_else(|| "Non-Unicode native module path".into())
}

fn environment(session: &Path) -> Result<BTreeMap<String, String>> {
    // OS paths required by windowing, fonts and GPU drivers; no developer/game
    // configuration or arbitrary inherited PATH reaches this module entry point.
    let mut result = BTreeMap::new();
    for name in [
        "SystemRoot",
        "WINDIR",
        "SystemDrive",
        "ProgramData",
        "ProgramFiles",
        "ProgramFiles(x86)",
        "CommonProgramFiles",
        "CommonProgramFiles(x86)",
        "USERPROFILE",
        "APPDATA",
        "LOCALAPPDATA",
        "TEMP",
        "TMP",
        "USERNAME",
        "USERDOMAIN",
        "PROCESSOR_ARCHITECTURE",
        "NUMBER_OF_PROCESSORS",
        "DISPLAY",
        "WAYLAND_DISPLAY",
        "XDG_RUNTIME_DIR",
        "HOME",
        "LANG",
        "LC_ALL",
    ] {
        if let Some(value) = std::env::var_os(name) {
            result.insert(
                name.into(),
                value
                    .into_string()
                    .map_err(|_| format!("Non-Unicode OS environment value: {name}"))?,
            );
        }
    }
    #[cfg(windows)]
    if let Some(system) = result.get("SystemRoot").cloned() {
        result.insert("PATH".into(), format!("{system}\\System32;{system}"));
    }
    result.insert(
        "FORGE_GAME_REPORT_PATH".into(),
        path_text(&session.join("game-report.json"))?,
    );
    Ok(result)
}

#[cfg(test)]
#[path = "../tests/support/installed_modules_spawn.rs"]
mod tests;
