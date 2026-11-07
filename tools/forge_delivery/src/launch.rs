use crate::{Result, archive, install, manifest};
use std::{
    collections::BTreeMap,
    fs,
    path::{Path, PathBuf},
    process::{Child, Command, Stdio},
};

pub fn default_root() -> Result<PathBuf> {
    if let Some(root) = std::env::var_os("FORGE_DELIVERY_INSTALL_ROOT") {
        let root = PathBuf::from(root);
        if !root.is_absolute() {
            return Err("Install root must be absolute".into());
        }
        return Ok(root);
    }
    Ok(default_data_base()?.join(if cfg!(target_os = "linux") {
        "installation"
    } else {
        "Installation"
    }))
}

/// Independent games never share the historical Forge installation root.
pub fn default_game_root(game_id: forge_core::ProjectId) -> Result<PathBuf> {
    if game_id.as_uuid().is_nil() {
        return Err("Game identity must not be empty".into());
    }
    Ok(default_data_base()?.join("games").join(game_id.to_string()))
}

pub fn user_root() -> Result<PathBuf> {
    if let Some(root) = std::env::var_os("FORGE_DELIVERY_USER_ROOT") {
        let root = PathBuf::from(root);
        if !root.is_absolute() {
            return Err("User data root must be absolute".into());
        }
        return Ok(root);
    }
    Ok(default_data_base()?.join(if cfg!(target_os = "linux") {
        "userdata"
    } else {
        "UserData"
    }))
}

fn default_data_base() -> Result<PathBuf> {
    #[cfg(windows)]
    {
        let base =
            PathBuf::from(std::env::var_os("LOCALAPPDATA").ok_or("LOCALAPPDATA is unavailable")?);
        if !base.is_absolute() {
            return Err("LOCALAPPDATA must be absolute".into());
        }
        Ok(base.join("OpenRPG Forge"))
    }
    #[cfg(target_os = "linux")]
    {
        linux_data_home(
            std::env::var_os("XDG_DATA_HOME").as_deref(),
            std::env::var_os("HOME").as_deref(),
        )
        .map(|base| base.join("openrpg-forge"))
    }
    #[cfg(not(any(windows, target_os = "linux")))]
    {
        Err("Supply explicit installation and user roots on this platform".into())
    }
}

#[cfg(target_os = "linux")]
fn linux_data_home(
    xdg: Option<&std::ffi::OsStr>,
    home: Option<&std::ffi::OsStr>,
) -> Result<PathBuf> {
    // XDG requires absolute paths; empty/relative values are ignored.
    if let Some(path) = xdg.map(PathBuf::from).filter(|path| path.is_absolute()) {
        return Ok(path);
    }
    let home = home
        .map(PathBuf::from)
        .filter(|path| path.is_absolute())
        .ok_or("Absolute HOME or XDG_DATA_HOME is required")?;
    Ok(home.join(".local/share"))
}

#[cfg(all(test, target_os = "linux"))]
#[test]
fn xdg_user_data_selection_requires_absolute_roots_and_ignores_invalid_xdg_values() {
    use std::ffi::OsStr;
    let home = Some(OsStr::new("/home/forge"));
    assert_eq!(
        linux_data_home(Some(OsStr::new("/data é")), home).unwrap(),
        Path::new("/data é")
    );
    for value in [None, Some(OsStr::new("")), Some(OsStr::new("relative"))] {
        assert_eq!(
            linux_data_home(value, home).unwrap(),
            Path::new("/home/forge/.local/share")
        );
    }
    assert!(linux_data_home(None, Some(OsStr::new("relative"))).is_err());
    assert!(linux_data_home(None, None).is_err());
}

/// Called only from Apply: copy an immutable module seed once; preserve later edits.
pub fn seed_workspace(
    root: &Path,
    snapshot: &install::Snapshot,
    user: &Path,
    command_id: &str,
) -> Result<()> {
    let definition = install::product(root, snapshot)?;
    if definition.schema == manifest::STUDIO_PRODUCT_SCHEMA {
        return ensure_studio_workspace(user, &definition.id);
    }
    let Some(seed) = &definition.authoring_seed else {
        return Ok(());
    };
    manifest::safe_chain(user)?;
    fs::create_dir_all(user).map_err(|e| e.to_string())?;
    for folder in ["captures", "saves", "logs"] {
        fs::create_dir_all(manifest::confined(user, folder)?).map_err(|e| e.to_string())?;
    }
    let workspace = manifest::confined(user, &format!("workspaces/{}", definition.id))?;
    if workspace.try_exists().map_err(|e| e.to_string())? {
        return Ok(());
    }
    let pack = snapshot
        .packs
        .get(&seed.pack_id)
        .ok_or("Seed package missing")?;
    let pack_root = install::package_root(root, pack)?;
    let descriptor = archive::verify_directory(&pack_root, &pack.revision)?;
    let prefix = format!("{}/", seed.path);
    let files: Vec<_> = descriptor
        .files
        .iter()
        .filter_map(|entry| {
            entry
                .path
                .strip_prefix(&prefix)
                .map(|relative| (entry, relative))
        })
        .collect();
    if files.is_empty() {
        return Err("Module workspace seed has no files".into());
    }
    let parent = workspace.parent().ok_or("No workspace parent")?;
    fs::create_dir_all(parent).map_err(|e| e.to_string())?;
    let stage = parent.join(format!(".seed-{command_id}-{}", crate::attempt()?));
    fs::create_dir(&stage).map_err(|e| e.to_string())?;
    for (entry, relative) in files {
        let destination = manifest::confined(&stage, relative)?;
        fs::create_dir_all(destination.parent().ok_or("No file parent")?)
            .map_err(|e| e.to_string())?;
        fs::copy(manifest::confined(&pack_root, &entry.path)?, &destination)
            .map_err(|e| e.to_string())?;
        if crate::file_hash(&destination)? != entry.sha256 {
            return Err("Workspace seed copy mismatch".into());
        }
    }
    fs::rename(stage, workspace).map_err(|e| e.to_string())?;
    Ok(())
}

/// Create only the Studio's root directory; its documents remain user-owned.
fn ensure_studio_workspace(user: &Path, product_id: &str) -> Result<()> {
    manifest::identifier(product_id)?;
    manifest::safe_chain(user)?;
    let workspace = manifest::confined(user, &format!("workspaces/{product_id}"))?;
    let parent = workspace.parent().ok_or("Workspace parent is missing")?;
    fs::create_dir_all(parent).map_err(|error| error.to_string())?;
    manifest::safe_chain(parent)?;
    match fs::create_dir(&workspace) {
        Ok(()) => (),
        Err(error) if error.kind() == std::io::ErrorKind::AlreadyExists => (),
        Err(error) => return Err(format!("Cannot create Studio workspace: {error}")),
    }
    manifest::safe_chain(&workspace)?;
    manifest::no_links(&workspace)?;
    let metadata = fs::symlink_metadata(&workspace).map_err(|error| error.to_string())?;
    #[cfg(windows)]
    {
        use std::os::windows::fs::MetadataExt;
        if metadata.file_attributes() & 0x400 != 0 {
            return Err("Studio workspace cannot be a reparse point".into());
        }
    }
    if metadata.file_type().is_symlink() || !metadata.is_dir() {
        return Err("Studio workspace must be a physical directory".into());
    }
    Ok(())
}

fn expand(value: &str, substitutions: &BTreeMap<String, String>) -> Result<String> {
    let mut result = String::new();
    let mut rest = value;
    while let Some((before, tail)) = rest.split_once('{') {
        result.push_str(before);
        let (key, next) = tail.split_once('}').ok_or("Unclosed launch template")?;
        result.push_str(
            substitutions
                .get(key)
                .ok_or_else(|| format!("Unknown path binding: {key}"))?,
        );
        rest = next;
    }
    result.push_str(rest);
    Ok(result)
}

/// A private collection is user data, independent of immutable delivery packs.
/// Reading this preference never copies or modifies the external Vault.
fn external_asset_vault(user: &Path) -> Result<Option<PathBuf>> {
    #[derive(serde::Deserialize)]
    #[serde(deny_unknown_fields)]
    struct Preference {
        schema: String,
        path: PathBuf,
    }
    let preference = manifest::confined(user, "asset-vault.v1.json")?;
    if !preference.try_exists().map_err(|e| e.to_string())? {
        return Ok(None);
    }
    let preference: Preference = crate::read_json(&preference)?;
    if preference.schema != "forge.delivery.asset-vault.v1" {
        return Err("Unsupported external asset Vault preference".into());
    }
    if !preference.path.is_absolute()
        || !preference.path.is_dir()
        || !preference.path.join("metadata").is_dir()
    {
        return Err(
            "External asset Vault must be an existing absolute directory with metadata".into(),
        );
    }
    Ok(Some(preference.path))
}

/// No shell evaluation: each argument is passed directly to the native process.
pub fn command(root: &Path, app_name: &str) -> Result<Command> {
    let state = install::state(root)?;
    let definition = install::product(root, &state.active)?;
    if definition.schema == manifest::STANDALONE_PRODUCT_SCHEMA {
        return Err("Standalone launch requires admission under the installation lock".into());
    }
    if definition.schema == manifest::STUDIO_PRODUCT_SCHEMA {
        return Err("Studio launch requires admission under the installation lock".into());
    }
    command_for_state(root, app_name, &state, &definition)
}

fn command_for_state(
    root: &Path,
    app_name: &str,
    state: &install::State,
    definition: &manifest::Product,
) -> Result<Command> {
    let app = definition
        .apps
        .get(app_name)
        .ok_or("Unknown application entry point")?;
    let app_root = install::package_root(root, &state.active.core)?;
    let descriptor: manifest::Descriptor =
        crate::read_json(&manifest::confined(&app_root, "package-manifest.json")?)?;
    let executable = manifest::confined(&app_root, &app.executable)?;
    let pin = descriptor
        .files
        .iter()
        .find(|entry| entry.path.eq_ignore_ascii_case(&app.executable))
        .ok_or("Missing executable inventory")?;
    if crate::file_hash(&executable)? != pin.sha256 {
        return Err("Application binary integrity mismatch".into());
    }
    let user = PathBuf::from(&state.user_root);
    let workspace = manifest::confined(&user, &format!("workspaces/{}", definition.id))?;
    let mut substitutions = BTreeMap::from([
        ("session".into(), crate::attempt()?),
        ("app".into(), app_root.to_string_lossy().into_owned()),
        ("install".into(), root.to_string_lossy().into_owned()),
        ("user".into(), user.to_string_lossy().into_owned()),
        ("workspace".into(), workspace.to_string_lossy().into_owned()),
    ]);
    for (id, pack) in &state.active.packs {
        substitutions.insert(
            format!("pack:{id}"),
            install::package_root(root, pack)?
                .to_string_lossy()
                .into_owned(),
        );
    }
    let mut command = Command::new(executable);
    command.current_dir(&app_root);
    for (key, _) in std::env::vars_os() {
        if key.to_str().is_some_and(|key| {
            let normalized = key.to_ascii_uppercase();
            app.clear_env_prefixes
                .iter()
                .any(|prefix| normalized.starts_with(&prefix.to_ascii_uppercase()))
                || (definition.schema == manifest::STANDALONE_PRODUCT_SCHEMA
                    && (normalized == "OPENRPG_ASSET_VAULT"
                        || normalized.starts_with("FORGE_AUTHORING_")
                        || normalized.starts_with("FORGE_TIDEWATER_")
                        || normalized.starts_with("FORGE_NATIVE_GAME_")
                        || normalized.starts_with("FORGE_MODULE_ASSET_")
                        || normalized.starts_with("FORGE_GAME_")
                        || normalized.starts_with("WEBVIEW2_")))
        }) {
            command.env_remove(key);
        }
    }
    for argument in &app.args {
        command.arg(expand(argument, &substitutions)?);
    }
    let external_vault = if app.env.contains_key("OPENRPG_ASSET_VAULT") {
        external_asset_vault(&user)?
    } else {
        None
    };
    for (name, value) in &app.env {
        if name == "OPENRPG_ASSET_VAULT"
            && let Some(vault) = &external_vault
        {
            command.env(name, vault);
            continue;
        }
        let absent_optional = definition.optional_packs.iter().any(|id| {
            !state.active.packs.contains_key(id) && value.contains(&format!("{{pack:{id}}}"))
        });
        if absent_optional {
            command.env_remove(name);
        } else {
            command.env(name, expand(value, &substitutions)?);
        }
    }
    Ok(command)
}

pub fn start(root: &Path, app_name: &str) -> Result<Child> {
    start_with(root, app_name, |mut command| {
        command
            .spawn()
            .map_err(|e| format!("Impossible de lancer {app_name} : {e}"))
    })
}

/// Start only the exact standalone application revision approved by the
/// installer; the check and native spawn share the delivery lock.
pub fn start_standalone_pinned(root: &Path, expected: &install::InstalledPack) -> Result<Child> {
    start_standalone_pinned_with(root, expected, |mut command| {
        command
            .spawn()
            .map_err(|error| format!("Impossible de lancer le jeu : {error}"))
    })
}

fn start_standalone_pinned_with<T>(
    root: &Path,
    expected: &install::InstalledPack,
    spawn: impl FnOnce(Command) -> Result<T>,
) -> Result<T> {
    install::state(root)?;
    install::with_lock(root, || {
        let state = install::state(root)?;
        if &state.active.core != expected {
            return Err("Game installation changed; prepare installation again".into());
        }
        let definition = install::product(root, &state.active)?;
        if definition.schema != manifest::STANDALONE_PRODUCT_SCHEMA {
            return Err("A standalone game installation is required".into());
        }
        let application = crate::standalone::verify_snapshot(root, &state.active)?;
        let mut command = command_for_state(root, "launcher", &state, &definition)?;
        command
            .arg("--expected-game-id")
            .arg(application.game().game_id().to_string());
        spawn_logged(command, "launcher", &state.user_root, spawn)
    })
}

/// A durable player entry delegates to the current launcher without nesting locks.
pub fn start_player_entry(root: &Path, executable: &Path) -> Result<Child> {
    start_player_entry_with(root, executable, |mut command| {
        command
            .spawn()
            .map_err(|error| format!("Impossible de lancer le jeu : {error}"))
    })
}

/// Preserve `--root` for an internal launcher even if its filename happens to
/// equal the durable entry's name; a root-level copy cannot claim that role.
pub fn validate_internal_launcher(root: &Path, executable: &Path) -> Result<()> {
    install::state(root)?;
    install::with_lock(root, || {
        let state = install::state(root)?;
        let application = crate::standalone::verify_snapshot(root, &state.active)?;
        let product = install::product(root, &state.active)?;
        let launcher = product
            .apps
            .get("launcher")
            .ok_or("Installed launcher missing")?;
        let package = install::package_root(root, &state.active.core)?;
        let expected = manifest::confined(&package, &launcher.executable)?;
        manifest::safe_chain(executable)?;
        if application.game().game_id().to_string() != product.id
            || fs::canonicalize(executable).map_err(|error| error.to_string())?
                != fs::canonicalize(expected).map_err(|error| error.to_string())?
        {
            return Err("Le lanceur ne correspond pas au jeu installé.".into());
        }
        Ok(())
    })
}

fn start_player_entry_with<T>(
    root: &Path,
    executable: &Path,
    spawn: impl FnOnce(Command) -> Result<T>,
) -> Result<T> {
    install::state(root)?;
    install::with_lock(root, || {
        let state = install::state(root)?;
        let game_id = install::check_player_entry_locked(root, &state, executable)?;
        let definition = install::product(root, &state.active)?;
        let mut command = command_for_state(root, "launcher", &state, &definition)?;
        command.arg("--expected-game-id").arg(game_id);
        spawn_logged(command, "launcher", &state.user_root, spawn)
    })
}

fn start_with<T>(
    root: &Path,
    app_name: &str,
    spawn: impl FnOnce(Command) -> Result<T>,
) -> Result<T> {
    let state = install::state(root)?;
    let definition = install::product(root, &state.active)?;
    if definition.schema == manifest::STANDALONE_PRODUCT_SCHEMA {
        if app_name != "launcher" {
            return Err("Standalone modules require a validated module selection".into());
        }
        return install::with_lock(root, || {
            let state = install::state(root)?;
            let definition = install::product(root, &state.active)?;
            if definition.schema != manifest::STANDALONE_PRODUCT_SCHEMA {
                return Err("Installation changed while preparing the launcher".into());
            }
            crate::standalone::verify_snapshot(root, &state.active)?;
            let command = command_for_state(root, app_name, &state, &definition)?;
            spawn_logged(command, app_name, &state.user_root, spawn)
        });
    }
    if definition.schema == manifest::STUDIO_PRODUCT_SCHEMA {
        if app_name != "editor" {
            return Err("Studio installation only launches the editor".into());
        }
        return install::with_lock(root, || {
            let state = install::state(root)?;
            let definition = install::product(root, &state.active)?;
            if definition.schema != manifest::STUDIO_PRODUCT_SCHEMA {
                return Err("Installation changed while preparing Studio".into());
            }
            install::verify_snapshot(root, &state.active)?;
            let mut command = command_for_state(root, app_name, &state, &definition)?;
            if let Some(reference) = state.active.packs.get(manifest::STUDIO_REFERENCE_ASSETS) {
                configure_reference_models(root, reference, &mut command)?;
            }
            spawn_logged(command, app_name, &state.user_root, spawn)
        });
    }
    install::with_lock(root, || {
        let command = command(root, app_name)?;
        let user_root = install::state(root)?.user_root;
        spawn_logged(command, app_name, &user_root, spawn)
    })
}

/// Reconnect the catalog used by the historical authoring workspace.
fn configure_reference_models(
    root: &Path,
    pack: &install::InstalledPack,
    command: &mut Command,
) -> Result<()> {
    let directory = install::package_root(root, pack)?;
    let descriptor: manifest::Descriptor =
        crate::read_json(&manifest::confined(&directory, "package-manifest.json")?)?;
    if crate::hash(&crate::json(&descriptor)?) != pack.revision {
        return Err("Studio reference assets manifest changed".into());
    }
    let catalog = manifest::reference_catalog(&descriptor)?;
    command
        .env(
            "FORGE_MODULE_ASSET_ROOT",
            manifest::confined(&directory, manifest::REFERENCE_SCENE)?,
        )
        .env("FORGE_MODULE_ASSET_CATALOG", manifest::REFERENCE_CATALOG)
        .env(
            "FORGE_MODULE_ASSET_REFERENCE_MANIFEST_SHA256",
            &pack.revision,
        )
        .env("FORGE_MODULE_ASSET_CATALOG_SHA256", &catalog.sha256);
    Ok(())
}

fn spawn_logged<T>(
    mut command: Command,
    app_name: &str,
    user_root: &str,
    spawn: impl FnOnce(Command) -> Result<T>,
) -> Result<T> {
    manifest::identifier(app_name)?;
    let user = PathBuf::from(user_root);
    let logs = manifest::confined(&user, "logs")?;
    fs::create_dir_all(&logs).map_err(|e| e.to_string())?;
    let log_path = manifest::confined(&logs, &format!("{app_name}-{}.log", crate::attempt()?))?;
    let log = fs::OpenOptions::new()
        .create_new(true)
        .write(true)
        .open(log_path)
        .map_err(|e| e.to_string())?;
    command
        .stdin(Stdio::null())
        .stdout(Stdio::from(log.try_clone().map_err(|e| e.to_string())?))
        .stderr(Stdio::from(log));
    #[cfg(windows)]
    {
        use std::os::windows::process::CommandExt;
        command.creation_flags(0x0800_0000);
    }
    spawn(command)
}

#[cfg(test)]
mod standalone_tests {
    use super::*;
    use crate::standalone_fixture;

    #[test]
    fn legacy_start_holds_installation_lock_and_refuses_pending_uninstall() {
        let fixture = standalone_fixture::installed_fixture::Fixture::new();
        let rollback = install::prepare_rollback(&fixture.install).unwrap();
        let result = start_with(&fixture.install, "game", |command| {
            assert!(
                command
                    .get_program()
                    .to_string_lossy()
                    .ends_with("game.bin")
            );
            assert!(install::apply(&rollback).is_err());
            Ok(37)
        })
        .unwrap();
        assert_eq!(result, 37);
        let pending = fixture.install.join(".forge-uninstall.pending.json");
        std::fs::write(&pending, b"foreign or incomplete journal").unwrap();
        let result = start_with(&fixture.install, "game", |_| -> Result<i32> {
            panic!("pending uninstall must not spawn a legacy application")
        });
        assert!(result.is_err());
        assert_eq!(
            std::fs::read(&pending).unwrap(),
            b"foreign or incomplete journal"
        );
    }

    #[test]
    fn standalone_launcher_rechecks_closure_and_holds_lock_through_spawn() {
        let fixture = standalone_fixture::Fixture::new();
        let plan = fixture.prepare_new(&fixture.first, "launcher-lock", "Jeu un");
        let root = fixture.installation("launcher-lock");
        install::apply(&plan).unwrap();
        let update =
            fixture.application(&fixture.first, "launcher-lock-update", "Jeu modifié", false);
        install::apply(&install::prepare(&root, &[update]).unwrap()).unwrap();
        let rollback = install::prepare_rollback(&root).unwrap();
        let before = std::fs::read(root.join("installation.json")).unwrap();
        let result = start_with(&root, "launcher", |command| {
            assert_eq!(
                command
                    .get_program()
                    .to_str()
                    .unwrap()
                    .rsplit(['/', '\\'])
                    .next(),
                Some("launcher.bin")
            );
            let mut arguments = command.get_args();
            assert_eq!(arguments.next().unwrap(), "--root");
            assert_eq!(Path::new(arguments.next().unwrap()), root);
            assert!(arguments.next().is_none());
            assert!(install::apply(&rollback).is_err());
            Ok(37)
        })
        .unwrap();
        assert_eq!(result, 37);
        assert_eq!(
            std::fs::read(root.join("installation.json")).unwrap(),
            before
        );
    }

    #[test]
    fn durable_entry_delegates_to_active_launcher_and_pins_game_identity() {
        let fixture = standalone_fixture::Fixture::new();
        let root = fixture.installation("durable-launch");
        let initial = fixture.prepare_new(&fixture.first, "durable-launch", "Jeu un");
        let first = install::apply(&initial).unwrap();
        let game_id = fixture.first.project.to_string();
        let entry = install::ensure_player_entry(&root, &game_id, &first.active.core).unwrap();
        let update = fixture.application(&fixture.first, "durable-update", "Jeu mis à jour", false);
        let updated = install::apply(&install::prepare(&root, &[update]).unwrap()).unwrap();
        assert!(validate_internal_launcher(&root, &entry).is_err());
        let internal = install::package_root(&root, &updated.active.core)
            .unwrap()
            .join("launcher.bin");
        assert!(validate_internal_launcher(&root, &internal).is_ok());
        let expected = fixture.first.project.to_string();
        assert_eq!(start_standalone_pinned_with(&root, &updated.active.core, |command| {
            let arguments: Vec<_> = command.get_args().collect();
            assert!(arguments.windows(2).any(|pair| pair[0] == "--expected-game-id" && pair[1] == expected.as_str()));
            Ok(29)
        }).unwrap(), 29);
        let rollback = install::prepare_rollback(&root).unwrap();
        let result = start_player_entry_with(&root, &entry, |command| {
            let active = install::package_root(&root, &updated.active.core).unwrap();
            assert_eq!(
                Path::new(command.get_program()),
                active.join("launcher.bin")
            );
            let args: Vec<_> = command.get_args().collect();
            assert_eq!(
                args,
                [
                    std::ffi::OsStr::new("--root"),
                    root.as_os_str(),
                    std::ffi::OsStr::new("--expected-game-id"),
                    std::ffi::OsStr::new(&game_id)
                ]
            );
            assert!(install::apply(&rollback).is_err());
            Ok(37)
        })
        .unwrap();
        assert_eq!(result, 37);
        install::apply(&rollback).unwrap();
        assert_eq!(
            start_player_entry_with(&root, &entry, |_| Ok(41)).unwrap(),
            41
        );
        fs::write(&entry, b"user change").unwrap();
        let calls = std::cell::Cell::new(0);
        assert!(
            start_player_entry_with(&root, &entry, |_| {
                calls.set(1);
                Ok(())
            })
            .is_err()
        );
        assert_eq!(calls.get(), 0);
    }

    #[test]
    fn pinned_launcher_child_refuses_another_game_after_spawn() {
        let fixture = standalone_fixture::Fixture::new();
        let root = fixture.installation("child-identity-a");
        let other = fixture.installation("child-identity-b");
        let first =
            install::apply(&fixture.prepare_new(&fixture.first, "child-identity-a", "Jeu A"))
                .unwrap();
        install::apply(&fixture.prepare_new(&fixture.second, "child-identity-b", "Jeu B")).unwrap();
        let expected = fixture.first.project;
        start_standalone_pinned_with(&root, &first.active.core, |command| {
            let arguments: Vec<_> = command.get_args().collect();
            assert!(
                arguments
                    .windows(2)
                    .any(|pair| pair[0] == "--expected-game-id"
                        && pair[1] == expected.to_string().as_str())
            );
            Ok(())
        })
        .unwrap();
        let old = fixture.installation("child-identity-old");
        fs::rename(&root, &old).unwrap();
        fs::rename(&other, &root).unwrap();
        assert!(crate::standalone::game_view_for(&root, Some(expected)).is_err());
        assert_eq!(
            crate::standalone::game_view(&root).unwrap().game_id,
            fixture.second.project
        );
    }
}

#[cfg(test)]
mod studio_workspace_tests {
    use super::*;
    use std::sync::atomic::{AtomicU64, Ordering};

    static NEXT: AtomicU64 = AtomicU64::new(0);

    struct Temp {
        root: PathBuf,
        parent: PathBuf,
    }

    impl Temp {
        fn new() -> Self {
            let parent = std::env::temp_dir().canonicalize().unwrap();
            let root = parent.join(format!(
                "forge-studio-workspace-{}-{}",
                std::process::id(),
                NEXT.fetch_add(1, Ordering::Relaxed)
            ));
            fs::create_dir(&root).unwrap();
            Self { root, parent }
        }
    }

    impl Drop for Temp {
        fn drop(&mut self) {
            assert_eq!(self.root.parent(), Some(self.parent.as_path()));
            fs::remove_dir_all(&self.root).unwrap();
        }
    }

    #[test]
    fn creates_only_a_missing_workspace_and_preserves_existing_projects() {
        let temporary = Temp::new();
        let user = temporary.root.join("user");
        let workspace = user.join("workspaces/openrpg-forge");
        ensure_studio_workspace(&user, manifest::STUDIO_PRODUCT_ID).unwrap();
        assert_eq!(fs::read_dir(&workspace).unwrap().count(), 0);
        forge_project::authoring::host::AuthoringHost::new(&workspace).unwrap();

        let current = workspace.join("existing-project.json");
        fs::write(&current, b"retouches utilisateur").unwrap();
        let other = user.join("workspaces/other-project");
        fs::create_dir(&other).unwrap();
        fs::write(other.join("project.json"), b"autre projet").unwrap();
        ensure_studio_workspace(&user, manifest::STUDIO_PRODUCT_ID).unwrap();
        assert_eq!(fs::read(&current).unwrap(), b"retouches utilisateur");
        assert_eq!(
            fs::read(other.join("project.json")).unwrap(),
            b"autre projet"
        );
    }

    #[test]
    fn refuses_a_file_at_the_workspace_path_without_replacing_it() {
        let temporary = Temp::new();
        let user = temporary.root.join("user");
        fs::create_dir_all(user.join("workspaces")).unwrap();
        let file = user.join("workspaces/openrpg-forge");
        fs::write(&file, b"ne pas remplacer").unwrap();
        assert!(ensure_studio_workspace(&user, manifest::STUDIO_PRODUCT_ID).is_err());
        assert_eq!(fs::read(file).unwrap(), b"ne pas remplacer");
    }

    #[test]
    #[cfg(any(unix, windows))]
    fn refuses_a_workspace_link_without_touching_its_target() {
        struct Link(PathBuf);
        impl Drop for Link {
            fn drop(&mut self) {
                #[cfg(unix)]
                fs::remove_file(&self.0).unwrap();
                #[cfg(windows)]
                fs::remove_dir(&self.0).unwrap();
            }
        }
        let temporary = Temp::new();
        let user = temporary.root.join("user");
        fs::create_dir_all(user.join("workspaces")).unwrap();
        let target = temporary.root.join("external");
        fs::create_dir(&target).unwrap();
        fs::write(target.join("project.json"), b"unique user project").unwrap();
        let path = user.join("workspaces/openrpg-forge");
        #[cfg(unix)]
        std::os::unix::fs::symlink(&target, &path).unwrap();
        #[cfg(windows)]
        {
            use std::os::windows::process::CommandExt;
            let output = std::process::Command::new("cmd")
                .args(["/c", "mklink", "/J"])
                .arg(&path)
                .arg(&target)
                .creation_flags(0x0800_0000)
                .output()
                .unwrap();
            assert!(output.status.success(), "failed to create test junction");
        }
        let _link = Link(path);
        assert!(ensure_studio_workspace(&user, manifest::STUDIO_PRODUCT_ID).is_err());
        assert_eq!(
            fs::read(target.join("project.json")).unwrap(),
            b"unique user project"
        );
    }
}

#[cfg(all(test, target_os = "windows", target_arch = "x86_64"))]
use crate as delivery;
#[cfg(all(test, target_os = "windows", target_arch = "x86_64"))]
#[path = "../tests/support/studio_workspace_fixture.rs"]
mod studio_fixture;

#[cfg(all(test, target_os = "windows", target_arch = "x86_64"))]
mod studio_start_tests {
    use super::*;
    use std::cell::Cell;

    #[test]
    fn studio_migrates_reference_catalog_and_launches_without_the_legacy_installation() {
        let fixture = studio_fixture::Fixture::new();
        let first = fixture.pack("1.0.0");
        install::apply(&fixture.first_plan(&first)).unwrap();
        let root = fixture.install();
        let legacy_root = fixture.root.join("legacy-installation");
        let mut descriptor = archive::inspect(&first).unwrap().0;
        let legacy_product = descriptor.product.as_mut().unwrap();
        legacy_product.schema = "forge.delivery.product.v1".into();
        legacy_product
            .apps
            .insert("game".into(), legacy_product.apps["editor"].clone());
        let legacy_app = fixture.root.join("legacy-application.forgepack");
        archive::pack(&fixture.root.join("source-1.0.0"), descriptor, &legacy_app).unwrap();
        let source = fixture.root.join("reference-assets-input");
        fs::create_dir_all(source.join("data/scene")).unwrap();
        let catalog = br#"{"assets":[]}"#;
        fs::write(source.join("data/scene/static-assets.json"), catalog).unwrap();
        fs::write(source.join("LICENSE.txt"), b"fixture license").unwrap();
        let credits_path = "data/scene/CREDITS-Scene.md";
        let credits = b"Scene media attribution";
        fs::write(source.join(credits_path), credits).unwrap();
        let reference = fixture.root.join("reference-assets.forgepack");
        archive::pack(
            &source,
            manifest::Descriptor {
                schema: "forge.delivery.package.v1".into(),
                id: manifest::STUDIO_REFERENCE_ASSETS.into(),
                label: "Historical reference assets".into(),
                version: "1.0.0".into(),
                kind: manifest::Kind::Assets,
                platform: "windows-x64".into(),
                source_revision: crate::hash(b"fixture source"),
                rights: manifest::Rights::PrivateLocalOnly,
                license_paths: vec!["LICENSE.txt".into()],
                files: Vec::new(),
                product: None,
            },
            &reference,
        )
        .unwrap();
        let mut legacy_plan = install::prepare(&legacy_root, &[legacy_app, reference]).unwrap();
        legacy_plan.user_root = fixture.user().to_string_lossy().into_owned();
        legacy_plan.command_id.clear();
        legacy_plan.command_id = crate::hash(&crate::json(&legacy_plan).unwrap());
        let legacy = install::apply(&legacy_plan).unwrap();
        let app =
            fixture.pack_with_options("2.0.0", vec![manifest::STUDIO_REFERENCE_ASSETS.into()]);
        let upgrade = install::prepare_studio_installer(&root, &app, Some(&legacy_root)).unwrap();
        assert_eq!(upgrade.inputs.len(), 2);
        // NSIS already marked the destination owned for the first Studio install.
        fs::write(root.join(".studio-classic-installer-owned"), b"pending\n").unwrap();
        let active = install::apply_studio_installer(&upgrade).unwrap();
        let pack = &active.active.packs[manifest::STUDIO_REFERENCE_ASSETS];
        assert_eq!(
            pack,
            &legacy.active.packs[manifest::STUDIO_REFERENCE_ASSETS]
        );
        let retry = install::prepare_studio_installer(&root, &app, Some(&legacy_root)).unwrap();
        assert_eq!(retry.inputs.len(), 1);
        assert_eq!(
            install::apply_studio_installer(&retry).unwrap().revision,
            active.revision
        );
        install::uninstall(&legacy_root).unwrap();
        let older = install::apply(&install::prepare_rollback(&root).unwrap()).unwrap();
        assert!(
            !older
                .active
                .packs
                .contains_key(manifest::STUDIO_REFERENCE_ASSETS)
        );
        let renewed = install::prepare_studio_installer(&root, &app, Some(&legacy_root)).unwrap();
        assert_eq!(renewed.inputs.len(), 1);
        let renewed = install::apply_studio_installer(&renewed).unwrap();
        assert_eq!(
            &renewed.active.packs[manifest::STUDIO_REFERENCE_ASSETS],
            pack
        );
        let scene = install::package_root(&root, pack)
            .unwrap()
            .join("data/scene");
        let project = fixture.workspace().join("my-project.txt");
        fs::write(&project, b"user-authored data").unwrap();
        install::uninstall_studio(&root).unwrap();
        assert_eq!(fs::read(scene.join("static-assets.json")).unwrap(), catalog);
        assert_eq!(fs::read(project).unwrap(), b"user-authored data");
        let reinstall = install::prepare_studio_installer(&root, &app, None).unwrap();
        assert_eq!(reinstall.inputs.len(), 1);
        let restored = install::apply_studio_installer(&reinstall).unwrap();
        assert_eq!(&restored.active.packs[manifest::STUDIO_REFERENCE_ASSETS], pack);
        start_with(&root, "editor", |command| {
            let env: BTreeMap<_, _> = command.get_envs().collect();
            let value = |name| env[std::ffi::OsStr::new(name)].unwrap();
            assert_eq!(Path::new(value("FORGE_MODULE_ASSET_ROOT")), scene);
            assert_eq!(value("FORGE_MODULE_ASSET_CATALOG"), "static-assets.json");
            assert_eq!(
                value("FORGE_MODULE_ASSET_CATALOG_SHA256"),
                crate::hash(catalog).as_str()
            );
            Ok(())
        })
        .unwrap();

        let reference = crate::legacy_reference::ReferenceScene::open(
            &scene,
            forge_core::ContentHash::parse(&pack.revision).unwrap(),
        )
        .unwrap();
        assert_eq!(
            reference.read_notices().unwrap(),
            vec![
                ("LICENSE.txt".into(), b"fixture license".to_vec()),
                (credits_path.into(), credits.to_vec()),
            ],
        );
        fs::write(scene.join("CREDITS-Scene.md"), b"changed").unwrap();
        assert!(reference.read_notices().is_err());
        fs::write(scene.join("CREDITS-Scene.md"), credits).unwrap();

        fs::write(scene.join("static-assets.json"), b"changed").unwrap();
        assert!(
            start_with::<()>(&root, "editor", |_| panic!("tampered catalog admitted")).is_err()
        );
        fs::write(scene.join("static-assets.json"), catalog).unwrap();
    }

    #[test]
    fn studio_rechecks_its_pack_and_holds_the_installation_lock_during_spawn() {
        let fixture = studio_fixture::Fixture::new();
        let first = fixture.pack("1.0.0");
        install::apply(&fixture.first_plan(&first)).unwrap();
        let second = fixture.pack("2.0.0");
        let root = fixture.install();
        let active = install::apply(&install::prepare(&root, &[second]).unwrap()).unwrap();
        let rollback = install::prepare_rollback(&root).unwrap();
        let before = fs::read(root.join("installation.json")).unwrap();
        let result = start_with(&root, "editor", |command| {
            assert_eq!(
                command
                    .get_program()
                    .to_str()
                    .unwrap()
                    .rsplit(['/', '\\'])
                    .next(),
                Some("forge-studio.exe")
            );
            let authoring = command
                .get_envs()
                .find(|(name, _)| *name == "FORGE_AUTHORING_ROOT")
                .and_then(|(_, value)| value)
                .unwrap();
            assert_eq!(Path::new(authoring), fixture.workspace());
            assert!(install::apply(&rollback).is_err());
            Ok(37)
        })
        .unwrap();
        assert_eq!(result, 37);
        assert_eq!(fs::read(root.join("installation.json")).unwrap(), before);
        assert!(start_with(&root, "game", |_| Ok(())).is_err());

        let application = install::package_root(&root, &active.active.core).unwrap();
        fs::write(
            application.join("bin/game-export-kit/bin/forge-module.exe"),
            b"tampered runtime",
        )
        .unwrap();
        let spawned = Cell::new(false);
        assert!(
            start_with(&root, "editor", |_| {
                spawned.set(true);
                Ok(())
            })
            .is_err()
        );
        assert!(!spawned.get());
    }
}
