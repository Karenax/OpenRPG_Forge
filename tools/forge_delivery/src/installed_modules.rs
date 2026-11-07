//! Installed module discovery and launch; client requests contain stable IDs only.
use crate::{Result, archive, install, manifest, module_runtime};
use forge_core::{AreaId, ContentHash, ModuleId, ProjectId};
use forge_gameplay::native_storage::{self, GameStorage};
use forge_project::authoring::playtest::PlaytestSnapshot;
use serde::{Deserialize, Serialize};
use std::{
    collections::BTreeMap,
    fs::{self, File, OpenOptions},
    io::Read,
    path::{Path, PathBuf},
    process::{Child, Command, Stdio},
};

mod recovery;
pub use recovery::{
    RECOVERED_MODULES_DIRECTORY, RecoveredModule, recover_legacy_modules,
    recover_legacy_modules_for_studio,
};

/// Trusted optional startup default; older module runtimes ignore it.
pub const GAME_DEFAULT_DISPLAY_MODE_ENV: &str = "FORGE_GAME_DEFAULT_DISPLAY_MODE";

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleSelection {
    pub module_id: ModuleId,
    pub area_id: AreaId,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ResumeSelection {
    pub selection: ModuleSelection,
    pub saved_hash: ContentHash,
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
    let product = install::product(root, &state.active)?;
    if product.schema == manifest::STANDALONE_PRODUCT_SCHEMA {
        crate::standalone::verify_snapshot(root, &state.active)?;
    }
    inventory_state(root, &state)
}

/// Keep a legacy launcher from reading a standalone replacement through its
/// pre-existing browser profile. The product is rechecked under the lock.
pub fn inventory_legacy(root: &Path) -> Result<ModuleInventory> {
    install::state(root)?;
    install::with_lock(root, || {
        let state = install::state(root)?;
        if install::product(root, &state.active)?.schema != "forge.delivery.product.v1" {
            return Err("Le jeu installé a changé. Rouvrez son lanceur.".into());
        }
        inventory_state(root, &state)
    })
}

/// Inventory for an already identified standalone game, under its install lock.
pub fn inventory_for(root: &Path, expected: ProjectId) -> Result<ModuleInventory> {
    inventory_for_profile(root, expected, None)
}

pub fn inventory_for_profile(
    root: &Path,
    expected: ProjectId,
    expected_user_root: Option<&str>,
) -> Result<ModuleInventory> {
    install::state(root)?;
    install::with_lock(root, || {
        let state = install::state(root)?;
        let application = crate::standalone::verify_snapshot(root, &state.active)?;
        crate::standalone::require_profile_root(&state, expected_user_root)?;
        if application.game().game_id() != expected {
            return Err("Le jeu installé a changé. Rouvrez son lanceur.".into());
        }
        inventory_state(root, &state)
    })
}

fn inventory_state(root: &Path, state: &install::State) -> Result<ModuleInventory> {
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
    let standalone = if product.schema == manifest::STANDALONE_PRODUCT_SCHEMA {
        let application = crate::standalone::verify_snapshot(root, &state.active)?;
        if !application.allows(selection.module_id, selection.area_id) {
            return Err("Selected module or area is not declared by this game".into());
        }
        Some(application)
    } else {
        None
    };
    let core = install::package_root(root, &state.active.core)?;
    let descriptor = if let Some(application) = &standalone {
        application.descriptor().clone()
    } else {
        archive::verify_directory(&core, &state.active.core.revision)?
    };
    let game = product
        .apps
        .get("module-game")
        // Older installations expose their module-capable runtime as `game`.
        .or_else(|| product.apps.get("game"))
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
    require_companion_runtime(&product, &loaded)?;
    let head_hash = loaded.zone.head().content_hash()?;
    let archive_sha256 = forge_core::bytes_sha256(&loaded.archive);
    let user = Path::new(&state.user_root);
    let working_directory =
        manifest::confined(user, &format!("game-sessions/module-{}", crate::attempt()?))?;
    let save = if let Some(application) = &standalone {
        application.save_root(user)?
    } else {
        manifest::confined(user, "saves")?
    };
    for output in [&working_directory, &save] {
        if prospective_path(output)?.starts_with(
            root.join("packages")
                .canonicalize()
                .map_err(|e| e.to_string())?,
        ) {
            return Err("Module session/save output must remain outside installed packages".into());
        }
    }
    let mut arguments = vec![
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
    let mut environment = environment(&working_directory)?;
    if let Some(application) = &standalone {
        // The first session uses the project's reviewed default. The runtime
        // keeps a player's saved preference ahead of this fallback.
        let quality = serde_json::to_value(application.game().configuration().defaults.quality)
            .map_err(|error| error.to_string())?;
        let quality = quality.as_str().ok_or("Invalid default graphics quality")?;
        arguments.extend(["--start-intent".into(), "fresh".into()]);
        arguments.extend(["--default-quality".into(), quality.into()]);
        let display =
            serde_json::to_value(application.game().configuration().defaults.display_mode)
                .map_err(|error| error.to_string())?;
        let display = display.as_str().ok_or("Invalid default display mode")?;
        environment.insert(GAME_DEFAULT_DISPLAY_MODE_ENV.into(), display.into());
    }
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
    let child = start_with(root, selection, |mut command| {
        command.spawn().map_err(|e| e.to_string())
    })?;
    confirm_startup(child)
}

/// Start a legacy selection only if the product remains v1 under the lock.
pub fn start_legacy(root: &Path, selection: ModuleSelection) -> Result<Child> {
    let child = start_selected_after_plan_with_owner(
        root,
        selection,
        false,
        None,
        LaunchOwner::LegacyOnly,
        || Ok(()),
        |mut command| command.spawn().map_err(|error| error.to_string()),
    )?;
    confirm_startup(child)
}

/// Start only a selection still belonging to the expected standalone game.
pub fn start_for(root: &Path, selection: ModuleSelection, expected: ProjectId) -> Result<Child> {
    start_for_profile(root, selection, expected, None)
}

pub fn start_for_profile(
    root: &Path,
    selection: ModuleSelection,
    expected: ProjectId,
    expected_user_root: Option<&str>,
) -> Result<Child> {
    let owner = profile_owner(expected, expected_user_root);
    let child = start_selected_after_plan_with_owner(
        root,
        selection,
        false,
        None,
        owner,
        || Ok(()),
        |mut command| command.spawn().map_err(|error| error.to_string()),
    )?;
    confirm_startup(child)
}

/// Start the entry declared by the active standalone game. The entry is
/// checked again under the installation lock immediately before spawn.
pub fn start_entry(root: &Path) -> Result<Child> {
    let state = install::state(root)?;
    let entry = crate::standalone::verify_snapshot(root, &state.active)?.entry();
    start_expected_entry(root, entry)
}

/// Start the entry previously presented to the player, if it remains current.
pub fn start_expected_entry(root: &Path, entry: ModuleSelection) -> Result<Child> {
    let child = start_entry_with(root, entry, |mut command| {
        command.spawn().map_err(|e| e.to_string())
    })?;
    confirm_startup(child)
}

pub fn start_expected_entry_for(
    root: &Path,
    entry: ModuleSelection,
    expected: ProjectId,
) -> Result<Child> {
    start_expected_entry_for_profile(root, entry, expected, None)
}

pub fn start_expected_entry_for_profile(
    root: &Path,
    entry: ModuleSelection,
    expected: ProjectId,
    expected_user_root: Option<&str>,
) -> Result<Child> {
    let owner = profile_owner(expected, expected_user_root);
    let child = start_selected_after_plan_with_owner(
        root,
        entry,
        true,
        None,
        owner,
        || Ok(()),
        |mut command| command.spawn().map_err(|error| error.to_string()),
    )?;
    confirm_startup(child)
}

/// Resume only the exact, previously inspected save of a selected v2 area.
pub fn start_resume(
    root: &Path,
    selection: ModuleSelection,
    saved_hash: ContentHash,
) -> Result<Child> {
    let child = start_resume_with(root, selection, saved_hash, |mut command| {
        command.spawn().map_err(|error| error.to_string())
    })?;
    confirm_startup(child).map_err(|error| {
        format!("{error} Une demande de reprise peut rester en attente ; réessayez Reprendre.")
    })
}

pub fn start_resume_for(
    root: &Path,
    selection: ModuleSelection,
    saved_hash: ContentHash,
    expected: ProjectId,
) -> Result<Child> {
    start_resume_for_profile(root, selection, saved_hash, expected, None)
}

pub fn start_resume_for_profile(
    root: &Path,
    selection: ModuleSelection,
    saved_hash: ContentHash,
    expected: ProjectId,
    expected_user_root: Option<&str>,
) -> Result<Child> {
    let owner = profile_owner(expected, expected_user_root);
    let child = start_selected_after_plan_with_owner(
        root,
        selection,
        false,
        Some(saved_hash),
        owner,
        || Ok(()),
        |mut command| command.spawn().map_err(|error| error.to_string()),
    )?;
    confirm_startup(child).map_err(|error| {
        format!("{error} Une demande de reprise peut rester en attente ; réessayez Reprendre.")
    })
}

fn confirm_startup(mut child: Child) -> Result<Child> {
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
    start_selected(root, selection, false, None, None, spawn)
}

fn start_entry_with<T>(
    root: &Path,
    selection: ModuleSelection,
    spawn: impl FnOnce(Command) -> Result<T>,
) -> Result<T> {
    start_selected(root, selection, true, None, None, spawn)
}

fn start_resume_with<T>(
    root: &Path,
    selection: ModuleSelection,
    saved_hash: ContentHash,
    spawn: impl FnOnce(Command) -> Result<T>,
) -> Result<T> {
    start_selected(root, selection, false, Some(saved_hash), None, spawn)
}

fn start_selected<T>(
    root: &Path,
    selection: ModuleSelection,
    require_entry: bool,
    resume: Option<ContentHash>,
    expected: Option<ProjectId>,
    spawn: impl FnOnce(Command) -> Result<T>,
) -> Result<T> {
    start_selected_after_plan(
        root,
        selection,
        require_entry,
        resume,
        expected,
        || Ok(()),
        spawn,
    )
}

#[derive(Clone, Copy)]
enum LaunchOwner<'a> {
    Any,
    LegacyOnly,
    Standalone(ProjectId),
    Profile(ProjectId, &'a str),
}

fn profile_owner(expected: ProjectId, root: Option<&str>) -> LaunchOwner<'_> {
    root.map_or(LaunchOwner::Standalone(expected), |root| {
        LaunchOwner::Profile(expected, root)
    })
}

fn start_selected_after_plan<T>(
    root: &Path,
    selection: ModuleSelection,
    require_entry: bool,
    resume: Option<ContentHash>,
    expected: Option<ProjectId>,
    after_plan: impl FnOnce() -> Result<()>,
    spawn: impl FnOnce(Command) -> Result<T>,
) -> Result<T> {
    let owner = expected.map_or(LaunchOwner::Any, LaunchOwner::Standalone);
    start_selected_after_plan_with_owner(
        root,
        selection,
        require_entry,
        resume,
        owner,
        after_plan,
        spawn,
    )
}

fn start_selected_after_plan_with_owner<T>(
    root: &Path,
    selection: ModuleSelection,
    require_entry: bool,
    resume: Option<ContentHash>,
    owner: LaunchOwner<'_>,
    after_plan: impl FnOnce() -> Result<()>,
    spawn: impl FnOnce(Command) -> Result<T>,
) -> Result<T> {
    let mut planned = plan(root, selection)?;
    after_plan()?;
    // Hold the same native lock as apply/uninstall throughout the final
    // admission and spawn. The child also verifies its three pins before scene setup.
    install::with_lock(root, || {
        let (state, head) = read_state(root)?;
        if head != planned.installation_head_sha256
            || state.revision != planned.installation_revision
        {
            return Err("Installation changed while preparing the module launch".into());
        }
        if let LaunchOwner::Profile(_, expected_user_root) = owner {
            crate::standalone::require_profile_root(&state, Some(expected_user_root))?;
        }
        let product = install::product(root, &state.active)?;
        if matches!(owner, LaunchOwner::LegacyOnly) && product.schema != "forge.delivery.product.v1"
        {
            return Err("Le jeu installé a changé. Rouvrez son lanceur.".into());
        }
        let application = if product.schema == manifest::STANDALONE_PRODUCT_SCHEMA {
            let application = crate::standalone::verify_snapshot(root, &state.active)?;
            if matches!(owner, LaunchOwner::Standalone(id) | LaunchOwner::Profile(id, _) if application.game().game_id() != id)
            {
                return Err("Le jeu installé a changé. Rouvrez son lanceur.".into());
            }
            if !application.allows(selection.module_id, selection.area_id) {
                return Err("Selected module or area is not declared by this game".into());
            }
            if require_entry && application.entry() != selection {
                return Err(
                    "Le point de départ du jeu a changé. Actualisez avant de jouer.".into(),
                );
            }
            Some(application)
        } else if require_entry
            || resume.is_some()
            || matches!(
                owner,
                LaunchOwner::Standalone(_) | LaunchOwner::Profile(_, _)
            )
        {
            return Err("Standalone game changed while preparing the launch".into());
        } else {
            None
        };
        let pack = state
            .active
            .packs
            .get(&selection.module_id.to_string())
            .ok_or("Selected module is no longer active")?;
        let loaded =
            module_runtime::load(&install::package_root(root, pack)?, selection.area_id, None)?;
        module_runtime::verify_transport_revision(&loaded, planned.package_manifest_sha256)?;
        require_companion_runtime(&product, &loaded)?;
        if loaded.zone.head().content_hash()? != planned.head_hash
            || forge_core::bytes_sha256(&loaded.archive) != planned.archive_sha256
            || crate::file_hash(&planned.executable)? != planned.executable_sha256.to_string()
        {
            return Err("Module archive or game executable changed before launch".into());
        }
        let mut resume_prepared = false;
        if let Some(application) = application {
            let save_root = application.save_root(Path::new(&state.user_root))?;
            let directory = save_root
                .join(selection.module_id.to_string())
                .join(selection.area_id.to_string());
            manifest::safe_chain(&directory)?;
            let intent = if let Some(expected) = resume {
                let snapshot = PlaytestSnapshot::from_zone(loaded.zone.head(), planned.head_hash)?;
                let document = snapshot
                    .gameplay()
                    .ok_or("Cette carte ne contient plus de partie à reprendre.")?;
                let mut storage = GameStorage::open_existing(&save_root, document)?
                    .ok_or("Cette sauvegarde n’est plus disponible.")?;
                if storage.resume_candidate(document, planned.head_hash, false)? != Some(expected) {
                    return Err("La sauvegarde a changé depuis sa vérification.".into());
                }
                storage.request_pinned_resume(document, planned.head_hash, false, expected)?;
                // The runtime needs the same document lock during startup.
                drop(storage);
                resume_prepared = true;
                format!("resume:{expected}")
            } else {
                if native_storage::has_pending_resume(
                    &save_root,
                    selection.module_id,
                    selection.area_id,
                )? {
                    return Err("Une reprise est déjà en attente. Choisissez Reprendre pour continuer cette partie.".into());
                }
                let snapshot = PlaytestSnapshot::from_zone(loaded.zone.head(), planned.head_hash)?;
                if let Some(document) = snapshot.gameplay()
                    && GameStorage::open_existing(&save_root, document)?.is_some()
                {
                    return Err("Une sauvegarde existe. Choisissez Reprendre pour préserver votre progression.".into());
                }
                "fresh".to_owned()
            };
            let flag = planned
                .arguments
                .iter()
                .position(|argument| argument == "--start-intent")
                .ok_or("Standalone start intent is missing from the admitted plan")?;
            *planned
                .arguments
                .get_mut(flag + 1)
                .ok_or("Standalone start intent value is missing")? = intent;
        }
        let result = prepare_command(&planned).and_then(spawn);
        if resume_prepared {
            result.map_err(|error| {
                format!(
                    "{error}. Une demande de reprise peut rester en attente ; réessayez Reprendre."
                )
            })
        } else {
            result
        }
    })
}

fn prepare_command(planned: &ModuleLaunchPlan) -> Result<Command> {
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
    Ok(command)
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

fn require_companion_runtime(
    product: &manifest::Product,
    loaded: &module_runtime::LoadedModuleZone,
) -> Result<()> {
    // The dedicated entry receives and rechecks head, archive and transport
    // pins before admitting physics/particles to the common engine. Legacy
    // game entry points retain their unsupported-host refusal.
    if product.apps.contains_key("module-game") {
        Ok(())
    } else {
        loaded.reject_unhandled_placement_companions()
    }
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

#[cfg(test)]
mod standalone_spawn_tests {
    use super::*;
    use crate::standalone_fixture;

    #[test]
    fn legacy_only_path_remains_usable_but_refuses_a_standalone_game() {
        let legacy = standalone_fixture::installed_fixture::Fixture::new();
        let legacy_selection = ModuleSelection {
            module_id: legacy.first.id,
            area_id: legacy.first.area,
        };
        assert!(inventory_legacy(&legacy.install).is_ok());
        assert_eq!(
            start_selected_after_plan_with_owner(
                &legacy.install,
                legacy_selection,
                false,
                None,
                LaunchOwner::LegacyOnly,
                || Ok(()),
                |_| Ok(37),
            )
            .unwrap(),
            37
        );
        assert!(
            crate::standalone::game_webview_profile_for(&legacy.install, None)
                .unwrap()
                .is_none()
        );
        let fixture = standalone_fixture::Fixture::new();
        let root = fixture.installation("legacy-profile-guard");
        install::apply(&fixture.prepare_new(&fixture.first, "legacy-profile-guard", "Jeu"))
            .unwrap();
        let selection = ModuleSelection {
            module_id: fixture.first.module,
            area_id: fixture.first.area,
        };
        let calls = std::cell::Cell::new(0);
        assert!(inventory_legacy(&root).is_err());
        assert!(
            start_selected_after_plan_with_owner(
                &root,
                selection,
                false,
                None,
                LaunchOwner::LegacyOnly,
                || Ok(()),
                |_| {
                    calls.set(calls.get() + 1);
                    Ok(())
                },
            )
            .is_err()
        );
        assert_eq!(calls.get(), 0);
    }

    #[test]
    fn legacy_launch_refuses_replacement_after_planning_without_spawning() {
        let legacy = standalone_fixture::installed_fixture::Fixture::new();
        let replacement = standalone_fixture::Fixture::new();
        let other = replacement.installation("legacy-profile-replacement");
        install::apply(&replacement.prepare_new(
            &replacement.first,
            "legacy-profile-replacement",
            "Jeu",
        ))
        .unwrap();
        let selection = ModuleSelection {
            module_id: legacy.first.id,
            area_id: legacy.first.area,
        };
        let old = legacy.root.join("legacy-profile-original");
        let calls = std::cell::Cell::new(0);
        let result = start_selected_after_plan_with_owner(
            &legacy.install,
            selection,
            false,
            None,
            LaunchOwner::LegacyOnly,
            || {
                fs::rename(&legacy.install, &old).map_err(|error| error.to_string())?;
                fs::rename(&other, &legacy.install).map_err(|error| error.to_string())?;
                Ok(())
            },
            |_| {
                calls.set(calls.get() + 1);
                Ok(())
            },
        );
        assert!(result.is_err());
        assert_eq!(calls.get(), 0);
    }

    #[test]
    fn expected_game_refuses_a_valid_other_game_for_view_inventory_and_launch() {
        let fixture = standalone_fixture::Fixture::new();
        let root = fixture.installation("identity-a");
        let other = fixture.installation("identity-b");
        install::apply(&fixture.prepare_new(&fixture.first, "identity-a", "Jeu A")).unwrap();
        install::apply(&fixture.prepare_new(&fixture.second, "identity-b", "Jeu B")).unwrap();
        let a = fixture.first.project;
        let b = fixture.second.project;
        let selection_b = ModuleSelection {
            module_id: fixture.second.module,
            area_id: fixture.second.area,
        };
        assert_eq!(
            crate::standalone::game_view_for(&root, Some(a))
                .unwrap()
                .game_id,
            a
        );
        assert!(inventory_for(&root, a).is_ok());
        let selection_a = ModuleSelection {
            module_id: fixture.first.module,
            area_id: fixture.first.area,
        };
        assert_eq!(
            start_selected(&root, selection_a, true, None, Some(a), |_| Ok(37)).unwrap(),
            37
        );
        let old = fixture.installation("identity-old");
        fs::rename(&root, &old).unwrap();
        fs::rename(&other, &root).unwrap();
        assert!(crate::standalone::game_view_for(&root, Some(a)).is_err());
        assert_eq!(
            crate::standalone::game_view_for(&root, Some(b))
                .unwrap()
                .game_id,
            b
        );
        assert!(inventory_for(&root, a).is_err());
        assert!(inventory_for(&root, b).is_ok());
        let calls = std::cell::Cell::new(0);
        for (entry, resume) in [
            (false, None),
            (true, None),
            (false, Some(forge_core::bytes_sha256(b"save"))),
        ] {
            let result = start_selected(&root, selection_b, entry, resume, Some(a), |_| {
                calls.set(calls.get() + 1);
                Ok(())
            });
            assert!(result.unwrap_err().contains("jeu installé a changé"));
        }
        assert_eq!(calls.get(), 0);
        assert!(!fixture.user.join("games").exists());
    }

    #[test]
    fn same_game_with_changed_user_root_refuses_profile_operations_and_spawn() {
        let fixture = standalone_fixture::Fixture::new();
        let root = fixture.installation("profile-root-change");
        install::apply(&fixture.prepare_new(&fixture.first, "profile-root-change", "Jeu")).unwrap();
        let (game, _, pinned) =
            crate::standalone::game_webview_profile_for(&root, Some(fixture.first.project))
                .unwrap()
                .unwrap();
        let selection = ModuleSelection {
            module_id: fixture.first.module,
            area_id: fixture.first.area,
        };
        assert!(crate::standalone::game_view_for_profile(&root, Some(game), Some(&pinned)).is_ok());
        assert!(inventory_for_profile(&root, game, Some(&pinned)).is_ok());
        let mut state = install::state(&root).unwrap();
        state.revision += 1;
        fs::write(root.join("installation.json"), crate::json(&state).unwrap()).unwrap();
        assert!(crate::standalone::game_view_for_profile(&root, Some(game), Some(&pinned)).is_ok());
        assert!(inventory_for_profile(&root, game, Some(&pinned)).is_ok());
        let other_user = fixture.root().join("different-user");
        fs::create_dir(&other_user).unwrap();
        state.user_root = other_user.to_str().unwrap().to_owned();
        fs::write(root.join("installation.json"), crate::json(&state).unwrap()).unwrap();
        assert!(
            crate::standalone::game_view_for_profile(&root, Some(game), Some(&pinned)).is_err()
        );
        assert!(inventory_for_profile(&root, game, Some(&pinned)).is_err());
        assert!(
            crate::standalone::prepare_game_rollback_for_profile(&root, game, Some(&pinned))
                .is_err()
        );
        assert!(
            crate::standalone::prepare_game_uninstall_helper_for_profile(
                &root,
                game,
                Some(&pinned)
            )
            .is_err()
        );
        let calls = std::cell::Cell::new(0);
        for (entry, resume) in [
            (false, None),
            (true, None),
            (false, Some(forge_core::bytes_sha256(b"save"))),
        ] {
            let result = start_selected_after_plan_with_owner(
                &root,
                selection,
                entry,
                resume,
                LaunchOwner::Profile(game, &pinned),
                || Ok(()),
                |_| {
                    calls.set(calls.get() + 1);
                    Ok(())
                },
            );
            assert!(
                result
                    .unwrap_err()
                    .contains("dossier de données du jeu a changé")
            );
        }
        assert_eq!(calls.get(), 0);
        assert!(fs::read_dir(&other_user).unwrap().next().is_none());
    }

    #[test]
    fn profile_root_changed_after_plan_is_refused_before_spawn() {
        let fixture = standalone_fixture::Fixture::new();
        let root = fixture.installation("profile-root-race");
        install::apply(&fixture.prepare_new(&fixture.first, "profile-root-race", "Jeu")).unwrap();
        let (game, _, pinned) =
            crate::standalone::game_webview_profile_for(&root, Some(fixture.first.project))
                .unwrap()
                .unwrap();
        let selection = ModuleSelection {
            module_id: fixture.first.module,
            area_id: fixture.first.area,
        };
        let calls = std::cell::Cell::new(0);
        let result = start_selected_after_plan_with_owner(
            &root,
            selection,
            false,
            None,
            LaunchOwner::Profile(game, &pinned),
            || {
                let mut state = install::state(&root)?;
                state.user_root = fixture
                    .root()
                    .join("other-user")
                    .to_str()
                    .unwrap()
                    .to_owned();
                fs::write(root.join("installation.json"), crate::json(&state)?)
                    .map_err(|error| error.to_string())?;
                Ok(())
            },
            |_| {
                calls.set(calls.get() + 1);
                Ok(())
            },
        );
        assert!(result.is_err());
        assert_eq!(calls.get(), 0);
        assert!(!fixture.root().join("other-user").exists());
    }

    #[test]
    fn expected_game_rechecks_head_if_installation_changes_after_planning() {
        let fixture = standalone_fixture::Fixture::new();
        let root = fixture.installation("identity-race-a");
        let other = fixture.installation("identity-race-b");
        install::apply(&fixture.prepare_new(&fixture.first, "identity-race-a", "Jeu A")).unwrap();
        install::apply(&fixture.prepare_new(&fixture.second, "identity-race-b", "Jeu B")).unwrap();
        let selection = ModuleSelection {
            module_id: fixture.first.module,
            area_id: fixture.first.area,
        };
        let old = fixture.installation("identity-race-old");
        let calls = std::cell::Cell::new(0);
        let result = start_selected_after_plan(
            &root,
            selection,
            false,
            None,
            Some(fixture.first.project),
            || {
                fs::rename(&root, &old).map_err(|error| error.to_string())?;
                fs::rename(&other, &root).map_err(|error| error.to_string())?;
                Ok(())
            },
            |_| {
                calls.set(calls.get() + 1);
                Ok(())
            },
        );
        assert!(result.is_err());
        assert_eq!(calls.get(), 0);
        assert!(!fixture.user.join("games").exists());
    }

    fn assert_display_env(command: &Command, expected: &str) {
        let value = command
            .get_envs()
            .find(|(key, _)| *key == std::ffi::OsStr::new(GAME_DEFAULT_DISPLAY_MODE_ENV))
            .and_then(|(_, value)| value);
        assert_eq!(value, Some(std::ffi::OsStr::new(expected)));
    }

    #[test]
    fn standalone_launch_transports_the_authored_default_quality() {
        let fixture = standalone_fixture::Fixture::new();
        let game = &fixture.first;
        let root = fixture.installation("default-quality");
        let application = fixture.application_at_with_quality(
            game,
            "default-quality",
            "Jeu",
            false,
            game.area,
            forge_project::VisualQualityTier::Low,
        );
        let mut install_plan =
            install::prepare(&root, &[application, game.archive.clone()]).unwrap();
        install_plan.user_root = fixture.user.to_str().unwrap().into();
        install_plan.command_id.clear();
        install_plan.command_id = crate::hash(&crate::json(&install_plan).unwrap());
        install::apply(&install_plan).unwrap();
        let plan = plan(
            &root,
            ModuleSelection {
                module_id: game.module,
                area_id: game.area,
            },
        )
        .unwrap();
        let index = plan
            .arguments
            .iter()
            .position(|value| value == "--default-quality")
            .unwrap();
        assert_eq!(plan.arguments[index + 1], "low");
    }

    #[test]
    fn standalone_plan_transports_each_display_default_without_new_cli_flags() {
        use forge_project::workspace::launcher::DisplayMode;
        for (mode, expected) in [
            (DisplayMode::Windowed, "windowed"),
            (DisplayMode::Borderless, "borderless"),
            (DisplayMode::Fullscreen, "fullscreen"),
        ] {
            let fixture = standalone_fixture::Fixture::new();
            let game = &fixture.first;
            let root = fixture.installation(expected);
            let application = fixture.application_at_with_defaults(
                game,
                expected,
                "Jeu",
                false,
                game.area,
                (forge_project::VisualQualityTier::High, mode),
            );
            let mut install_plan =
                install::prepare(&root, &[application, game.archive.clone()]).unwrap();
            install_plan.user_root = fixture.user.to_str().unwrap().into();
            install_plan.command_id.clear();
            install_plan.command_id = crate::hash(&crate::json(&install_plan).unwrap());
            install::apply(&install_plan).unwrap();
            let selection = ModuleSelection {
                module_id: game.module,
                area_id: game.area,
            };
            let plan = plan(&root, selection).unwrap();
            assert_eq!(
                plan.environment
                    .get(GAME_DEFAULT_DISPLAY_MODE_ENV)
                    .map(String::as_str),
                Some(expected)
            );
            assert!(
                !plan
                    .arguments
                    .iter()
                    .any(|arg| arg == "--default-display-mode")
            );
            start_entry_with(&root, selection, |command| {
                assert_display_env(&command, expected);
                Ok(())
            })
            .unwrap();
        }
    }

    fn assert_save_path_isolated(root: &Path, blocked: ModuleSelection, other: ModuleSelection) {
        use crate::standalone::ResumeAvailability;

        let view = crate::standalone::game_view(root).unwrap();
        let zones = &view.modules[0].zones;
        let bad = zones.iter().find(|zone| zone.selection == blocked).unwrap();
        assert_eq!(bad.pending_resume, None);
        assert!(matches!(
            &bad.resume,
            ResumeAvailability::Unavailable { .. }
        ));
        let good = zones.iter().find(|zone| zone.selection == other).unwrap();
        assert_eq!(good.pending_resume, Some(false));
        assert_eq!(good.resume, ResumeAvailability::Absent);

        let called = std::cell::Cell::new(false);
        assert!(
            start_with(root, blocked, |_| {
                called.set(true);
                Ok(())
            })
            .is_err()
        );
        assert!(!called.get());
        start_with(root, other, |_| Ok(())).unwrap();
    }

    #[test]
    fn invalid_save_directory_only_disables_its_area() {
        let fixture = standalone_fixture::Fixture::new();
        let game = fixture.with_extra_area(&fixture.first);
        let root = fixture.installation("bad-save-area");
        let state = install::apply(&fixture.prepare_new(&game, "bad-save-area", "Jeu un")).unwrap();
        let app = crate::standalone::verify_snapshot(&root, &state.active).unwrap();
        let module_dir = app
            .save_root(&fixture.user)
            .unwrap()
            .join(game.module.to_string());
        std::fs::create_dir_all(&module_dir).unwrap();
        std::fs::write(module_dir.join(game.area.to_string()), b"not a directory").unwrap();
        let blocked = ModuleSelection {
            module_id: game.module,
            area_id: game.area,
        };
        let other = ModuleSelection {
            module_id: game.module,
            area_id: game.extra_area.as_ref().unwrap().area,
        };
        assert_save_path_isolated(&root, blocked, other);
    }

    #[cfg(unix)]
    #[test]
    fn linked_save_directory_only_disables_its_area() {
        let fixture = standalone_fixture::Fixture::new();
        let game = fixture.with_extra_area(&fixture.first);
        let root = fixture.installation("linked-save-area");
        let state =
            install::apply(&fixture.prepare_new(&game, "linked-save-area", "Jeu un")).unwrap();
        let app = crate::standalone::verify_snapshot(&root, &state.active).unwrap();
        let module_dir = app
            .save_root(&fixture.user)
            .unwrap()
            .join(game.module.to_string());
        std::fs::create_dir_all(&module_dir).unwrap();
        let protected = fixture.root().join("outside-save");
        std::fs::create_dir(&protected).unwrap();
        std::fs::write(protected.join("sentinel"), b"preserve").unwrap();
        let link = module_dir.join(game.area.to_string());
        std::os::unix::fs::symlink(&protected, &link).unwrap();
        let blocked = ModuleSelection {
            module_id: game.module,
            area_id: game.area,
        };
        let other = ModuleSelection {
            module_id: game.module,
            area_id: game.extra_area.as_ref().unwrap().area,
        };
        assert_save_path_isolated(&root, blocked, other);
        std::fs::remove_file(&link).unwrap();
        assert_eq!(
            std::fs::read(protected.join("sentinel")).unwrap(),
            b"preserve"
        );
    }

    #[test]
    fn orphaned_resume_request_blocks_fresh_play_without_creating_or_deleting_a_save() {
        use crate::standalone::ResumeAvailability;
        let fixture = standalone_fixture::Fixture::new();
        let root = fixture.installation("orphan-resume");
        let state = install::apply(&fixture.prepare_new(&fixture.first, "orphan-resume", "Jeu un"))
            .unwrap();
        let game = &fixture.first;
        let app = crate::standalone::verify_snapshot(&root, &state.active).unwrap();
        let save_root = app.save_root(&fixture.user).unwrap();
        let directory = save_root
            .join(game.module.to_string())
            .join(game.area.to_string());
        std::fs::create_dir_all(&directory).unwrap();
        let request = directory.join("resume.json");
        std::fs::write(&request, b"{}").unwrap();
        let view = crate::standalone::game_view(&root).unwrap();
        assert_eq!(view.modules[0].zones[0].pending_resume, Some(true));
        assert_eq!(view.modules[0].zones[0].resume, ResumeAvailability::Absent);
        let selection = ModuleSelection {
            module_id: game.module,
            area_id: game.area,
        };
        let called = std::cell::Cell::new(false);
        let error = start_with(&root, selection, |_| {
            called.set(true);
            Ok(())
        })
        .unwrap_err();
        assert!(error.contains("reprise"));
        assert!(!called.get());
        assert_eq!(std::fs::read(request).unwrap(), b"{}");
        assert!(!directory.join("authoring.json").exists());
    }

    #[test]
    fn import_legacy_save_is_project_bound_and_ready_to_resume() {
        use crate::standalone::{self, LegacySaveAction, LegacySaveSource, ResumeAvailability};
        use forge_gameplay::GameSession;

        let fixture = standalone_fixture::Fixture::new();
        let game = fixture.with_gameplay(&fixture.first);
        let legacy = fixture.legacy_installation();
        let legacy_state =
            install::apply(&install::prepare(legacy, std::slice::from_ref(&game.archive)).unwrap())
                .unwrap();
        let root = fixture.installation("save-migration");
        let state = install::apply(&fixture.prepare_new(&game, "save-migration", "Jeu")).unwrap();
        let selection = ModuleSelection {
            module_id: game.module,
            area_id: game.area,
        };
        let package =
            install::package_root(&root, &state.active.packs[&game.module.to_string()]).unwrap();
        let loaded = module_runtime::load(&package, game.area, None).unwrap();
        let source = loaded.zone.head().content_hash().unwrap();
        let snapshot = PlaytestSnapshot::from_zone(loaded.zone.head(), source).unwrap();
        let document = snapshot.gameplay().unwrap();
        let legacy_saves = Path::new(&legacy_state.user_root).join("saves");
        let save_path = |saves: &Path| {
            saves
                .join(game.module.to_string())
                .join(game.area.to_string())
                .join("authoring.json")
        };
        let mut old = GameStorage::open(&legacy_saves, document).unwrap();
        let mut session = GameSession::new(document.clone(), source).unwrap();
        let saved = session.save_bytes().unwrap();
        old.save(&saved).unwrap();
        drop(old);

        assert!(
            standalone::import_legacy_save(&root, legacy, fixture.second.project, selection)
                .is_err()
        );
        let application = standalone::verify_snapshot(&root, &state.active).unwrap();
        let target = application.save_root(&fixture.user).unwrap();
        assert!(!target.exists());
        let reviewed = standalone::review_legacy_save_for_profile(
            &root,
            legacy,
            game.project,
            None,
            selection,
        )
        .unwrap();
        let changed = forge_core::bytes_sha256(b"different historical save");
        assert!(
            standalone::import_legacy_save_for_profile(
                &root,
                legacy,
                game.project,
                None,
                selection,
                Some(changed),
            )
            .is_err()
        );
        assert!(!target.exists());
        recover_legacy_modules(legacy).unwrap();
        install::uninstall(legacy).unwrap();
        assert!(install::state(legacy).is_err());
        let from_recovery = |action| {
            standalone::migrate_legacy_save_for_profile(
                &root,
                LegacySaveSource::RecoveredProfile(Path::new(&legacy_state.user_root)),
                game.project,
                None,
                selection,
                action,
            )
        };
        assert_eq!(from_recovery(LegacySaveAction::Review).unwrap(), reviewed);
        let import = || from_recovery(LegacySaveAction::Import { reviewed_save: Some(reviewed) });
        let hash = import().unwrap();
        assert_eq!(hash, reviewed);
        assert_eq!(import().unwrap(), hash);
        assert_eq!(std::fs::read(save_path(&target)).unwrap(), saved);
        assert_eq!(
            standalone::game_view(&root).unwrap().modules[0].zones[0].resume,
            ResumeAvailability::Ready { saved_hash: hash }
        );

        session
            .observe_position(document.player, forge_core::Vec3::new(2.0, 1.0, 3.0))
            .unwrap();
        let newer = session.save_bytes().unwrap();
        GameStorage::open(&target, document)
            .unwrap()
            .save(&newer)
            .unwrap();
        assert!(import().is_err());
        assert_eq!(std::fs::read(save_path(&target)).unwrap(), newer);
        assert_eq!(std::fs::read(save_path(&legacy_saves)).unwrap(), saved);
    }

    #[test]
    fn resume_requires_the_reviewed_save_and_never_deletes_a_pending_request() {
        use crate::standalone::ResumeAvailability;
        use forge_gameplay::{GameSession, native_storage::StartIntent};

        let fixture = standalone_fixture::Fixture::new();
        let game = fixture.with_gameplay(&fixture.first);
        let root = fixture.installation("resume");
        let state = install::apply(&fixture.prepare_new(&game, "resume", "Jeu un")).unwrap();
        let selection = ModuleSelection {
            module_id: game.module,
            area_id: game.area,
        };
        let before = crate::standalone::game_view(&root).unwrap();
        assert_eq!(
            before.modules[0].zones[0].resume,
            ResumeAvailability::Absent
        );
        assert_eq!(before.modules[0].zones[0].pending_resume, Some(false));
        assert!(!fixture.user.exists());
        start_with(&root, selection, |command| {
            assert_display_env(&command, "borderless");
            let args: Vec<_> = command.get_args().collect();
            let flag = args
                .iter()
                .position(|arg| *arg == "--start-intent")
                .unwrap();
            assert_eq!(args[flag + 1], "fresh");
            Ok(())
        })
        .unwrap();

        let package =
            install::package_root(&root, &state.active.packs[&game.module.to_string()]).unwrap();
        let loaded = module_runtime::load(&package, game.area, None).unwrap();
        let source = loaded.zone.head().content_hash().unwrap();
        let snapshot = PlaytestSnapshot::from_zone(loaded.zone.head(), source).unwrap();
        let document = snapshot.gameplay().unwrap();
        let application = crate::standalone::verify_snapshot(&root, &state.active).unwrap();
        let save_root = application.save_root(&fixture.user).unwrap();
        let mut storage = GameStorage::open(&save_root, document).unwrap();
        let mut session = GameSession::new(document.clone(), source).unwrap();
        session
            .observe_position(document.player, forge_core::Vec3::new(5.0, 1.0, 8.0))
            .unwrap();
        let initial_save = session.save_bytes().unwrap();
        storage.save(&initial_save).unwrap();
        drop(storage);
        let view = crate::standalone::game_view(&root).unwrap();
        let ResumeAvailability::Ready { saved_hash } = &view.modules[0].zones[0].resume else {
            panic!("valid save was not presented");
        };
        let stale = *saved_hash;
        assert!(start_with(&root, selection, |_| Ok(())).is_err());
        let mut storage = GameStorage::open_existing(&save_root, document)
            .unwrap()
            .unwrap();
        session
            .observe_position(document.player, forge_core::Vec3::new(7.0, 1.0, 9.0))
            .unwrap();
        let saved = session.save_bytes().unwrap();
        storage.save(&saved).unwrap();
        drop(storage);
        assert!(start_resume_with(&root, selection, stale, |_| Ok(())).is_err());
        assert!(!native_storage::has_pending_resume(&save_root, game.module, game.area).unwrap());
        let view = crate::standalone::game_view(&root).unwrap();
        let ResumeAvailability::Ready { saved_hash } = &view.modules[0].zones[0].resume else {
            panic!("updated save was not presented");
        };
        let saved_hash = *saved_hash;
        assert_ne!(saved_hash, stale);

        let failure = start_resume_with(&root, selection, saved_hash, |command| {
            assert_display_env(&command, "borderless");
            let args: Vec<_> = command.get_args().collect();
            let flag = args
                .iter()
                .position(|arg| *arg == "--start-intent")
                .unwrap();
            assert_eq!(args[flag + 1], format!("resume:{saved_hash}").as_str());
            Err::<(), _>("controlled spawn failure".into())
        })
        .unwrap_err();
        assert!(failure.contains("reprise"));
        let request = save_root
            .join(game.module.to_string())
            .join(game.area.to_string())
            .join("resume.json");
        let original = std::fs::read(&request).unwrap();
        let repeated = start_resume_with(&root, selection, saved_hash, |_| {
            Err::<(), _>("controlled failure".into())
        });
        assert!(repeated.is_err());
        assert_eq!(std::fs::read(&request).unwrap(), original);
        #[cfg(windows)]
        {
            use std::os::windows::process::CommandExt;
            let child = start_resume_with(&root, selection, saved_hash, |_| {
                Command::new(std::env::var_os("COMSPEC").ok_or("COMSPEC absent")?)
                    .args(["/D", "/C", "exit", "9"])
                    .creation_flags(0x0800_0000)
                    .spawn()
                    .map_err(|error| error.to_string())
            })
            .unwrap();
            assert!(confirm_startup(child).is_err());
            assert_eq!(std::fs::read(&request).unwrap(), original);
        }
        assert!(start_with(&root, selection, |_| Ok(())).is_err());
        assert_eq!(std::fs::read(&request).unwrap(), original);

        let view = crate::standalone::game_view(&root).unwrap();
        assert_eq!(view.modules[0].zones[0].pending_resume, Some(true));
        let mut storage = GameStorage::open_existing(&save_root, document)
            .unwrap()
            .unwrap();
        let (resume, compatible) = storage
            .requested_resume_with_intent(document, StartIntent::Resume(saved_hash))
            .unwrap();
        assert!(!compatible);
        assert_eq!(resume.as_deref(), Some(saved.as_slice()));
        let restored =
            GameSession::resume(document.clone(), source, &resume.unwrap(), false).unwrap();
        assert_eq!(
            restored.state().actors[&document.player].position,
            forge_core::Vec3::new(7.0, 1.0, 9.0)
        );
        assert_eq!(std::fs::read(&request).unwrap(), original);
    }

    #[test]
    fn stale_entry_is_refused_under_lock_even_if_both_areas_remain_admitted() {
        let fixture = standalone_fixture::Fixture::new();
        let game = fixture.with_extra_area(&fixture.first);
        let extra = game.extra_area.as_ref().unwrap();
        let root = fixture.installation("entry-change");
        install::apply(&fixture.prepare_new(&game, "entry-change", "Jeu un")).unwrap();
        let before = crate::standalone::game_view(&root).unwrap();
        let update = fixture.application_at(&game, "entry-update", "Jeu un", false, extra.area);
        install::apply(&install::prepare(&root, &[update]).unwrap()).unwrap();
        let after = crate::standalone::game_view(&root).unwrap();
        assert_ne!(before.entry, after.entry);
        assert_eq!(after.entry.area_id, extra.area);
        assert!(
            after.modules[0]
                .zones
                .iter()
                .any(|zone| zone.selection == before.entry)
        );

        let called = std::cell::Cell::new(false);
        let error = start_entry_with(&root, before.entry, |_| {
            called.set(true);
            Ok(())
        })
        .unwrap_err();
        assert!(error.contains("point de départ du jeu a changé"));
        assert!(!called.get());
        assert!(
            start_expected_entry(&root, before.entry)
                .unwrap_err()
                .contains("point de départ du jeu a changé")
        );
        assert!(!fixture.user.join("game-sessions").exists());

        let rollback = install::prepare_rollback(&root).unwrap();
        start_entry_with(&root, after.entry, |command| {
            let args: Vec<_> = command.get_args().collect();
            let zone = args.iter().position(|arg| *arg == "--zone").unwrap();
            assert_eq!(args[zone + 1], extra.area.to_string().as_str());
            assert!(install::apply(&rollback).is_err());
            Ok(())
        })
        .unwrap();
    }

    #[test]
    fn standalone_module_rechecks_closure_and_holds_lock_through_spawn() {
        let fixture = standalone_fixture::Fixture::new();
        let plan = fixture.prepare_new(&fixture.first, "module-lock", "Jeu un");
        let root = fixture.installation("module-lock");
        install::apply(&plan).unwrap();
        let update =
            fixture.application(&fixture.first, "module-lock-update", "Jeu modifié", false);
        install::apply(&install::prepare(&root, &[update]).unwrap()).unwrap();
        let rollback = install::prepare_rollback(&root).unwrap();
        let before = std::fs::read(root.join("installation.json")).unwrap();
        let selection = ModuleSelection {
            module_id: fixture.first.module,
            area_id: fixture.first.area,
        };
        start_with(&root, selection, |command| {
            assert_eq!(command.get_args().next().unwrap(), "--module");
            assert!(install::apply(&rollback).is_err());
            Ok(())
        })
        .unwrap();
        assert_eq!(
            std::fs::read(root.join("installation.json")).unwrap(),
            before
        );
    }
}
