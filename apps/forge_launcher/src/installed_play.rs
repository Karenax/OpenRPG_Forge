use forge_delivery::installed_modules::{self, ModuleSelection, ResumeSelection};
use forge_delivery::standalone::{LegacySaveAction, LegacySaveSource, ProjectId};
use forge_launcher_frontend::{
    GameProcessStatus, InstalledAdventure, LauncherResponse, RejectedAdventure,
};
use std::{
    path::{Path, PathBuf},
    process::Child,
    sync::Mutex,
};

#[derive(Default)]
pub(super) struct GameProcess {
    last_launch_id: u32,
    child: Option<TrackedGame>,
    restart_required: bool,
}

struct TrackedGame {
    launch_id: u32,
    child: Child,
}

/// Observe the exact launch, without mistaking a reused process ID for it.
pub(super) fn game_status(game: &Mutex<GameProcess>, launch_id: u32) -> LauncherResponse {
    let Ok(mut current) = game.lock() else {
        return LauncherResponse::Rejected {
            reason: "Impossible de suivre le jeu pour le moment.".into(),
        };
    };
    let Some(tracked) = current
        .child
        .as_mut()
        .filter(|tracked| tracked.launch_id == launch_id)
    else {
        return LauncherResponse::GameStatus {
            launch_id,
            status: GameProcessStatus::Superseded,
        };
    };
    let status = match tracked.child.try_wait() {
        Ok(None) => GameProcessStatus::Running,
        Ok(Some(exit)) => GameProcessStatus::Exited {
            success: exit.success(),
            exit_code: exit.code(),
        },
        Err(error) => {
            return LauncherResponse::Rejected {
                reason: format!("Impossible de suivre le jeu : {error}"),
            };
        }
    };
    LauncherResponse::GameStatus { launch_id, status }
}

fn validated_root(root: &Path) -> Result<PathBuf, String> {
    if !root.is_absolute() {
        return Err("Le dossier d’installation doit être absolu.".into());
    }
    forge_delivery::manifest::normalized_directory(root)
}

fn stopped(current: &mut GameProcess) -> Result<(), String> {
    if current.restart_required {
        return Err("Fermez et rouvrez le lanceur avant de continuer.".into());
    }
    if let Some(tracked) = current.child.as_mut() {
        match tracked.child.try_wait() {
            Ok(None) => return Err("Fermez le jeu avant de modifier son installation.".into()),
            Ok(Some(_)) => current.child = None,
            Err(error) => {
                return Err(format!(
                    "Impossible de vérifier si le jeu est fermé : {error}"
                ));
            }
        }
    }
    Ok(())
}

pub(super) fn prepare_rollback(
    game: &Mutex<GameProcess>,
    reviewed: &Mutex<Option<forge_delivery::install::Plan>>,
    expected: Option<ProjectId>,
    expected_user_root: Option<&str>,
) -> LauncherResponse {
    let result: Result<LauncherResponse, String> = (|| {
        let expected = expected.ok_or("Rouvrez le lanceur du jeu avant de le restaurer.")?;
        let mut process = game.lock().map_err(|_| "État du jeu indisponible.")?;
        stopped(&mut process)?;
        let mut plan = reviewed.lock().map_err(|_| "Restauration indisponible.")?;
        *plan = None;
        *plan = forge_delivery::standalone::prepare_game_rollback_for_profile(
            &distribution_root()?,
            expected,
            expected_user_root,
        )?;
        Ok(LauncherResponse::RollbackPrepared {
            available: plan.is_some(),
        })
    })();
    result.unwrap_or_else(|reason| LauncherResponse::Rejected { reason })
}

pub(super) fn apply_rollback(
    game: &Mutex<GameProcess>,
    reviewed: &Mutex<Option<forge_delivery::install::Plan>>,
    expected: Option<ProjectId>,
    expected_user_root: Option<&str>,
) -> LauncherResponse {
    let result: Result<LauncherResponse, String> = (|| {
        expected.ok_or("Rouvrez le lanceur du jeu avant de le restaurer.")?;
        let mut process = game.lock().map_err(|_| "État du jeu indisponible.")?;
        stopped(&mut process)?;
        let mut reviewed = reviewed.lock().map_err(|_| "Restauration indisponible.")?;
        let plan = reviewed
            .take()
            .ok_or("Vérifiez la version précédente avant de la restaurer.")?;
        if expected_user_root.is_some_and(|root| plan.user_root != root) {
            return Err("Le dossier de données du jeu a changé. Rouvrez son lanceur.".into());
        }
        forge_delivery::install::apply(&plan)?;
        process.restart_required = true;
        Ok(LauncherResponse::RollbackCompleted)
    })();
    result.unwrap_or_else(|reason| LauncherResponse::Rejected { reason })
}

pub(super) fn start_uninstall(
    game: &Mutex<GameProcess>,
    expected: Option<ProjectId>,
    expected_user_root: Option<&str>,
) -> LauncherResponse {
    let result: Result<LauncherResponse, String> = (|| {
        let expected = expected.ok_or("Rouvrez le lanceur du jeu avant de le désinstaller.")?;
        let mut process = game.lock().map_err(|_| "État du jeu indisponible.")?;
        stopped(&mut process)?;
        let helper = forge_delivery::standalone::prepare_game_uninstall_helper_for_profile(
            &distribution_root()?,
            expected,
            expected_user_root,
        )?;
        std::process::Command::new(&helper.executable)
            .current_dir(&helper.root)
            .spawn()
            .map_err(|error| {
                format!(
                    "Impossible d’ouvrir le programme de désinstallation : {error}. Ouvrez {}.",
                    helper.executable.display()
                )
            })?;
        process.restart_required = true;
        Ok(LauncherResponse::UninstallHelperStarted {
            path: helper.executable.to_string_lossy().into_owned(),
        })
    })();
    result.unwrap_or_else(|reason| LauncherResponse::Rejected { reason })
}

fn distribution_root() -> Result<PathBuf, String> {
    let mut args = std::env::args_os().skip(1);
    while let Some(arg) = args.next() {
        if arg == "--root" {
            return validated_root(&PathBuf::from(
                args.next().ok_or("Dossier d’installation absent.")?,
            ));
        }
    }
    if let Some(root) = std::env::var_os("FORGE_DELIVERY_INSTALL_ROOT") {
        return validated_root(&PathBuf::from(root));
    }
    let executable = std::env::current_exe().map_err(|error| error.to_string())?;
    validated_root(executable.parent().ok_or("Dossier du launcher absent.")?)
}

fn expected_game_args(
    arguments: impl IntoIterator<Item = std::ffi::OsString>,
) -> Result<Option<(PathBuf, ProjectId)>, String> {
    let mut arguments = arguments.into_iter();
    let mut root = None;
    let mut expected = None;
    let mut duplicate_root = false;
    while let Some(argument) = arguments.next() {
        if argument == "--root" {
            let value = arguments.next().ok_or("Dossier d’installation absent.")?;
            duplicate_root |= root.replace(PathBuf::from(value)).is_some();
        } else if argument == "--expected-game-id" {
            let value = arguments
                .next()
                .ok_or("Identité du jeu absente.")?
                .into_string()
                .map_err(|_| "Identité du jeu invalide.")?;
            let value = value
                .parse::<ProjectId>()
                .map_err(|_| "Identité du jeu invalide.")?;
            if value.as_uuid().is_nil() {
                return Err("Identité du jeu invalide.".into());
            }
            if expected.replace(value).is_some() {
                return Err("Identité du jeu répétée.".into());
            }
        }
    }
    let Some(expected) = expected else {
        return Ok(None);
    };
    if duplicate_root {
        return Err("Dossier d’installation ambigu.".into());
    }
    let root = validated_root(&root.ok_or("Dossier du jeu absent.")?)?;
    Ok(Some((root, expected)))
}

pub(super) type VerifiedStartup = (ProjectId, Option<PathBuf>, Option<String>);

/// An entry-launched child must reject a different game even if the head
/// changes between the parent's spawn and this process starting.
#[cfg(windows)]
pub(super) fn verify_expected_startup() -> Result<Option<VerifiedStartup>, String> {
    let arguments: Vec<_> = std::env::args_os().skip(1).collect();
    let explicit = expected_game_args(arguments.clone())?;
    let (root, expected) = if let Some((root, expected)) = explicit {
        (root, Some(expected))
    } else if let Some(root) = root_without_game_id(arguments)? {
        (root, None)
    } else if super::installer::candidate_location()?.is_some() {
        // The installer can run even when an unrelated installation root is inherited.
        return Ok(None);
    } else if let Some(root) = std::env::var_os("FORGE_DELIVERY_INSTALL_ROOT") {
        (validated_root(&PathBuf::from(root))?, None)
    } else {
        let executable = std::env::current_exe().map_err(|error| error.to_string())?;
        let parent = validated_root(executable.parent().ok_or("Dossier du launcher absent.")?)?;
        if !parent
            .join("installation.json")
            .try_exists()
            .map_err(|error| error.to_string())?
        {
            return Ok(None);
        }
        (parent, None)
    };
    retry_contention(
        || forge_delivery::standalone::game_webview_profile_for(&root, expected),
        || std::thread::sleep(std::time::Duration::from_millis(25)),
    )
    .map(|result| result.map(|(game, profile, user_root)| (game, Some(profile), Some(user_root))))
}

#[cfg(not(windows))]
pub(super) fn verify_expected_startup() -> Result<Option<VerifiedStartup>, String> {
    let Some((root, expected)) = expected_game_args(std::env::args_os().skip(1))? else {
        return Ok(None);
    };
    verify_expected_with(
        expected,
        || {
            forge_delivery::standalone::game_view_for(&root, Some(expected))
                .map(|game| game.game_id)
        },
        || std::thread::sleep(std::time::Duration::from_millis(25)),
    )?;
    Ok(Some((expected, None, None)))
}

#[cfg(any(windows, test))]
fn root_without_game_id(
    arguments: impl IntoIterator<Item = std::ffi::OsString>,
) -> Result<Option<PathBuf>, String> {
    let mut args = arguments.into_iter();
    let mut root = None;
    while let Some(argument) = args.next() {
        if argument == "--root" {
            let value = PathBuf::from(args.next().ok_or("Dossier d’installation absent.")?);
            if root.replace(validated_root(&value)?).is_some() {
                return Err("Dossier d’installation ambigu.".into());
            }
        }
    }
    Ok(root)
}

fn retry_contention<T>(
    mut read: impl FnMut() -> Result<T, String>,
    mut pause: impl FnMut(),
) -> Result<T, String> {
    for attempt in 0..=40 {
        match read() {
            Ok(actual) => return Ok(actual),
            Err(error) if forge_delivery::install::is_lock_contention(&error) && attempt < 40 => {
                pause();
            }
            Err(error) => return Err(error),
        }
    }
    Err("Vérification du jeu interrompue.".into())
}

#[cfg(any(not(windows), test))]
fn verify_expected_with(
    expected: ProjectId,
    mut read: impl FnMut() -> Result<ProjectId, String>,
    pause: impl FnMut(),
) -> Result<(), String> {
    retry_contention(
        || read().and_then(|actual| require_expected_game(actual, expected)),
        pause,
    )
}

#[cfg(any(not(windows), test))]
fn require_expected_game(actual: ProjectId, expected: ProjectId) -> Result<(), String> {
    if actual != expected {
        return Err(
            "Le jeu installé a changé. Fermez cette fenêtre et rouvrez son lanceur.".into(),
        );
    }
    Ok(())
}

pub(super) fn runtime_connected() -> bool {
    distribution_root().is_ok_and(|root| runtime_connected_at(&root))
}

fn runtime_connected_at(root: &Path) -> bool {
    // Snapshot is still consumed by the Aube frontend as Tidewater readiness.
    // The v2 view has its own admission-backed availability instead.
    forge_delivery::launch::command(root, "game").is_ok()
}

pub(super) fn game_view(
    expected: Option<ProjectId>,
    expected_user_root: Option<&str>,
) -> LauncherResponse {
    if cfg!(all(windows, feature = "standalone")) && expected.is_none() {
        return LauncherResponse::Rejected {
            reason: "Rouvrez le lanceur du jeu installé.".into(),
        };
    }
    match distribution_root().and_then(|root| {
        forge_delivery::standalone::game_view_for_profile(&root, expected, expected_user_root)
    }) {
        Ok(game) => LauncherResponse::GameView {
            game,
            runtime_connected: true,
            legacy_installation_found: registered_legacy_root().is_some()
                || recovered_legacy_profile().is_some(),
        },
        Err(reason) => LauncherResponse::Rejected { reason },
    }
}

fn registered_legacy_root() -> Option<PathBuf> {
    registered_install_root(r"Software\OpenRPG Forge")
}

fn registered_install_root(registry_key: &str) -> Option<PathBuf> {
    #[cfg(all(windows, feature = "standalone"))]
    {
        use winsafe::{HKEY, RegistryValue, co};
        let value = HKEY::CURRENT_USER
            .RegGetValue(Some(registry_key), Some("InstallRoot"), co::RRF::RT_REG_SZ)
            .ok()?;
        if let RegistryValue::Sz(path) = value {
            let root = validated_root(Path::new(&path)).ok()?;
            return root.join("installation.json").is_file().then_some(root);
        }
    }
    #[cfg(not(all(windows, feature = "standalone")))]
    let _ = registry_key;
    None
}

fn recovered_legacy_profile() -> Option<PathBuf> {
    let studio_user = registered_install_root(r"Software\OpenRPG Forge Studio").and_then(|root| {
        let state = forge_delivery::install::state(&root).ok()?;
        let product = forge_delivery::install::product(&root, &state.active).ok()?;
        (product.schema == forge_delivery::manifest::STUDIO_PRODUCT_SCHEMA)
            .then(|| PathBuf::from(state.user_root))
    });
    [studio_user, forge_delivery::launch::user_root().ok()]
        .into_iter()
        .flatten()
        .find(|user| {
            forge_delivery::manifest::confined(user, installed_modules::RECOVERED_MODULES_DIRECTORY)
                .is_ok_and(|directory| directory.is_dir())
        })
}

pub(super) fn legacy_save(
    game: &Mutex<GameProcess>,
    selection: ModuleSelection,
    reviewed: Option<ResumeSelection>,
    expected: Option<ProjectId>,
    expected_user_root: Option<&str>,
) -> LauncherResponse {
    let run = || {
        let expected = expected.ok_or("Rouvrez le lanceur du jeu installé.")?;
        let installed = registered_legacy_root();
        let recovered = if installed.is_none() {
            recovered_legacy_profile()
        } else {
            None
        };
        let legacy = installed
            .as_deref()
            .map(LegacySaveSource::Installed)
            .or_else(|| recovered.as_deref().map(LegacySaveSource::RecoveredProfile))
            .ok_or("Aucune source de sauvegardes historiques Forge détectée.")?;
        let root = distribution_root()?;
        let mut current = game
            .lock()
            .map_err(|_| "Le jeu est temporairement indisponible.")?;
        stopped(&mut current)?;
        let action = reviewed.map_or(LegacySaveAction::Review, |reviewed| {
            LegacySaveAction::Import {
                reviewed_save: Some(reviewed.saved_hash),
            }
        });
        let hash = forge_delivery::standalone::migrate_legacy_save_for_profile(
            &root,
            legacy,
            expected,
            expected_user_root,
            selection,
            action,
        )?;
        if reviewed.is_some() {
            Ok(LauncherResponse::LegacySaveImported)
        } else {
            Ok(LauncherResponse::LegacySaveReviewed {
                saved_hash: hash.to_string(),
            })
        }
    };
    run().unwrap_or_else(|reason| LauncherResponse::Rejected { reason })
}

pub(super) fn modules(
    expected: Option<ProjectId>,
    expected_user_root: Option<&str>,
) -> LauncherResponse {
    match distribution_root().and_then(|root| match expected {
        Some(id) => installed_modules::inventory_for_profile(&root, id, expected_user_root),
        None if cfg!(all(windows, feature = "standalone")) => {
            installed_modules::inventory_legacy(&root)
        }
        None => installed_modules::inventory(&root),
    }) {
        Ok(inventory) => LauncherResponse::Modules {
            modules: inventory
                .modules
                .into_iter()
                .map(|module| InstalledAdventure {
                    name: module.name,
                    version: module.version,
                    zones: module
                        .zones
                        .into_iter()
                        .map(|zone| ModuleSelection {
                            module_id: module.module_id,
                            area_id: zone.area_id,
                        })
                        .collect(),
                })
                .collect(),
            rejected: inventory
                .rejected
                .into_iter()
                .map(|entry| RejectedAdventure {
                    pack_id: entry.pack_id,
                    reason: entry.reason,
                })
                .collect(),
        },
        Err(reason) => LauncherResponse::Rejected { reason },
    }
}

fn start_game(root: &Path) -> Result<Child, String> {
    // The manifest owns paths, workspace, content packs and diagnostic cleanup.
    confirm_startup(forge_delivery::launch::start(root, "game")?)
}

fn start_default(
    root: &Path,
    expected: Option<ProjectId>,
    expected_user_root: Option<&str>,
) -> Result<Child, String> {
    if let Some(id) = expected {
        let entry =
            forge_delivery::standalone::game_view_for_profile(root, Some(id), expected_user_root)?
                .entry;
        return confirm_startup(installed_modules::start_expected_entry_for_profile(
            root,
            entry,
            id,
            expected_user_root,
        )?);
    }
    if cfg!(all(windows, feature = "standalone")) {
        // The legacy service rechecks under the installation lock and refuses v2.
        return start_game(root);
    }
    let state = forge_delivery::install::state(root)?;
    let product = forge_delivery::install::product(root, &state.active)?;
    if product.schema == forge_delivery::manifest::STANDALONE_PRODUCT_SCHEMA {
        confirm_startup(installed_modules::start_entry(root)?)
    } else {
        start_game(root)
    }
}

fn confirm_startup(mut child: Child) -> Result<Child, String> {
    // A successful spawn alone is not a successful game startup.
    let deadline = std::time::Instant::now() + std::time::Duration::from_secs(3);
    while std::time::Instant::now() < deadline {
        if let Some(status) = child.try_wait().map_err(|error| error.to_string())? {
            return Err(format!(
                "Le jeu s’est arrêté pendant son ouverture ({status}). Consultez le dernier journal game dans le dossier de données utilisateur."
            ));
        }
        std::thread::sleep(std::time::Duration::from_millis(100));
    }
    Ok(child)
}

pub(super) fn launch(
    game: &Mutex<GameProcess>,
    expected: Option<ProjectId>,
    expected_user_root: Option<&str>,
) -> LauncherResponse {
    let root = match distribution_root() {
        Ok(root) => root,
        Err(reason) => return blocked("runtime-missing", reason),
    };
    launch_at(&root, game, None, expected, expected_user_root)
}

pub(super) fn launch_module(
    game: &Mutex<GameProcess>,
    selection: ModuleSelection,
    expected: Option<ProjectId>,
    expected_user_root: Option<&str>,
) -> LauncherResponse {
    let root = match distribution_root() {
        Ok(root) => root,
        Err(reason) => return blocked("runtime-missing", reason),
    };
    launch_at(&root, game, Some(selection), expected, expected_user_root)
}

pub(super) fn launch_entry(
    game: &Mutex<GameProcess>,
    selection: ModuleSelection,
    expected: Option<ProjectId>,
    expected_user_root: Option<&str>,
) -> LauncherResponse {
    let root = match distribution_root() {
        Ok(root) => root,
        Err(reason) => return blocked("runtime-missing", reason),
    };
    launch_request(
        &root,
        game,
        LaunchRequest::Entry(selection),
        expected,
        expected_user_root,
    )
}

pub(super) fn resume(
    game: &Mutex<GameProcess>,
    resume: ResumeSelection,
    expected: Option<ProjectId>,
    expected_user_root: Option<&str>,
) -> LauncherResponse {
    let root = match distribution_root() {
        Ok(root) => root,
        Err(reason) => return blocked("runtime-missing", reason),
    };
    launch_request(
        &root,
        game,
        LaunchRequest::Resume(resume),
        expected,
        expected_user_root,
    )
}

#[derive(Clone, Copy)]
enum LaunchRequest {
    Default,
    Entry(ModuleSelection),
    Module(ModuleSelection),
    Resume(ResumeSelection),
}

fn launch_at(
    root: &Path,
    game: &Mutex<GameProcess>,
    selection: Option<ModuleSelection>,
    expected: Option<ProjectId>,
    expected_user_root: Option<&str>,
) -> LauncherResponse {
    let request = selection.map_or(LaunchRequest::Default, LaunchRequest::Module);
    launch_request(root, game, request, expected, expected_user_root)
}

fn launch_request(
    root: &Path,
    game: &Mutex<GameProcess>,
    request: LaunchRequest,
    expected: Option<ProjectId>,
    expected_user_root: Option<&str>,
) -> LauncherResponse {
    let Ok(mut current) = game.lock() else {
        return blocked("runtime-unavailable", "État du jeu indisponible.".into());
    };
    if current.restart_required {
        return blocked(
            "restart-required",
            "Fermez et rouvrez le lanceur avant de jouer.".into(),
        );
    }
    if let Some(tracked) = current.child.as_mut() {
        match tracked.child.try_wait() {
            Ok(None) => return blocked("already-running", "Le jeu est déjà ouvert.".into()),
            Ok(Some(_)) => current.child = None,
            Err(error) => return blocked("runtime-unavailable", error.to_string()),
        }
    }
    let Some(launch_id) = current.last_launch_id.checked_add(1) else {
        return blocked(
            "runtime-unavailable",
            "Redémarrez le launcher avant une nouvelle partie.".into(),
        );
    };
    current.last_launch_id = launch_id;
    let result = match request {
        // Rebuild and admit the plan natively from IDs, never from frontend paths.
        LaunchRequest::Entry(selection) => match expected {
            Some(id) => installed_modules::start_expected_entry_for_profile(
                root,
                selection,
                id,
                expected_user_root,
            ),
            None if cfg!(all(windows, feature = "standalone")) => {
                Err("Rouvrez le lanceur du jeu installé.".into())
            }
            None => installed_modules::start_expected_entry(root, selection),
        }
        .and_then(confirm_startup),
        LaunchRequest::Module(selection) => match expected {
            Some(id) => {
                installed_modules::start_for_profile(root, selection, id, expected_user_root)
            }
            None if cfg!(all(windows, feature = "standalone")) => {
                installed_modules::start_legacy(root, selection)
            }
            None => installed_modules::start(root, selection),
        }
        .and_then(confirm_startup),
        LaunchRequest::Resume(resume) => match expected {
            Some(id) => installed_modules::start_resume_for_profile(
                root,
                resume.selection,
                resume.saved_hash,
                id,
                expected_user_root,
            ),
            None if cfg!(all(windows, feature = "standalone")) => {
                Err("Rouvrez le lanceur du jeu installé.".into())
            }
            None => installed_modules::start_resume(root, resume.selection, resume.saved_hash),
        }
        .and_then(confirm_startup),
        LaunchRequest::Default => start_default(root, expected, expected_user_root),
    };
    match result {
        Ok(child) => {
            let process_id = child.id();
            current.child = Some(TrackedGame { launch_id, child });
            LauncherResponse::LaunchStarted {
                process_id,
                launch_id: Some(launch_id),
            }
        }
        Err(reason) => blocked(
            match request {
                LaunchRequest::Entry(_) => "entry-launch-failed",
                LaunchRequest::Module(_) => "module-launch-failed",
                LaunchRequest::Resume(_) => "resume-failed",
                LaunchRequest::Default => "runtime-missing",
            },
            if matches!(request, LaunchRequest::Resume(_)) {
                format!(
                    "{reason} Une demande de reprise peut rester en attente ; actualisez avant de réessayer."
                )
            } else {
                reason
            },
        ),
    }
}

fn blocked(code: &str, reason: String) -> LauncherResponse {
    LauncherResponse::LaunchBlocked {
        code: code.into(),
        reason,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn maintenance_requires_the_game_identity_and_blocks_launch_after_handoff() {
        let game = Mutex::new(GameProcess::default());
        let reviewed = Mutex::new(None);
        assert!(matches!(
            prepare_rollback(&game, &reviewed, None, None),
            LauncherResponse::Rejected { .. }
        ));
        assert!(matches!(
            apply_rollback(&game, &reviewed, None, None),
            LauncherResponse::Rejected { .. }
        ));
        assert!(matches!(
            start_uninstall(&game, None, None),
            LauncherResponse::Rejected { .. }
        ));
        game.lock().unwrap().restart_required = true;
        assert!(matches!(
            launch_request(Path::new("unused"), &game, LaunchRequest::Default, None, None),
            LauncherResponse::LaunchBlocked { code, .. } if code == "restart-required"
        ));
    }

    #[test]
    fn direct_root_without_game_id_is_found_and_duplicate_refused() {
        let root = validated_root(&std::env::temp_dir()).unwrap();
        let args = [
            std::ffi::OsString::from("--root"),
            root.clone().into_os_string(),
        ];
        assert!(expected_game_args(args.clone()).unwrap().is_none());
        assert_eq!(root_without_game_id(args.clone()).unwrap(), Some(root));
        let duplicate = [
            args[0].clone(),
            args[1].clone(),
            args[0].clone(),
            args[1].clone(),
        ];
        assert!(root_without_game_id(duplicate).is_err());
    }

    #[test]
    fn entry_child_requires_the_expected_game_and_an_unambiguous_root() {
        let game_a = ProjectId::deterministic("game-a");
        let game_b = ProjectId::deterministic("game-b");
        let root = forge_delivery::manifest::normalized_directory(&std::env::temp_dir()).unwrap();
        let mut trailing = root.clone().into_os_string();
        trailing.push(std::path::MAIN_SEPARATOR_STR);
        let args = [
            std::ffi::OsString::from("--root"),
            trailing,
            std::ffi::OsString::from("--expected-game-id"),
            std::ffi::OsString::from(game_a.to_string()),
        ];
        let parsed = expected_game_args(args.clone()).unwrap().unwrap();
        assert_eq!(parsed, (root.clone(), game_a));
        assert!(require_expected_game(game_a, parsed.1).is_ok());
        assert!(require_expected_game(game_b, parsed.1).is_err());
        let calls = std::cell::Cell::new(0);
        assert!(
            verify_expected_with(
                game_a,
                || {
                    calls.set(calls.get() + 1);
                    if calls.get() < 3 {
                        Err("Un autre processus modifie cette installation : occupé".into())
                    } else {
                        Ok(game_a)
                    }
                },
                || {}
            )
            .is_ok()
        );
        assert_eq!(calls.get(), 3);
        assert!(verify_expected_with(game_a, || Ok(game_b), || {}).is_err());
        let mut duplicate = args.to_vec();
        duplicate.extend([std::ffi::OsString::from("--root"), root.into_os_string()]);
        assert!(expected_game_args(duplicate).is_err());
        assert!(
            expected_game_args([
                std::ffi::OsString::from("--root"),
                std::ffi::OsString::from("relative")
            ])
            .unwrap()
            .is_none()
        );
    }

    // This helper runs only in its own test subprocess, without opening a game or window.
    #[test]
    fn process_status_test_child() {
        match std::env::var("FORGE_LAUNCHER_PROCESS_STATUS_CHILD").as_deref() {
            Ok("late-error") => {
                std::thread::sleep(std::time::Duration::from_secs(5));
                std::process::exit(9);
            }
            Ok("normal") => std::process::exit(0),
            _ => (),
        }
    }

    fn status_test_child(mode: &str) -> Child {
        let mut command = std::process::Command::new(std::env::current_exe().unwrap());
        command
            .args(["--exact", "play::tests::process_status_test_child"])
            .env("FORGE_LAUNCHER_PROCESS_STATUS_CHILD", mode)
            .stdin(std::process::Stdio::null())
            .stdout(std::process::Stdio::null())
            .stderr(std::process::Stdio::null());
        #[cfg(windows)]
        {
            use std::os::windows::process::CommandExt;
            command.creation_flags(0x0800_0000);
        }
        command.spawn().unwrap()
    }

    #[test]
    fn process_status_reports_late_failure_and_never_confuses_launch_ids() {
        let child = confirm_startup(status_test_child("late-error"))
            .expect("the process outlives the initial startup observation");
        let game = Mutex::new(GameProcess {
            last_launch_id: 2,
            child: Some(TrackedGame {
                launch_id: 2,
                child,
            }),
            ..GameProcess::default()
        });
        assert_eq!(
            game_status(&game, 1),
            LauncherResponse::GameStatus {
                launch_id: 1,
                status: GameProcessStatus::Superseded,
            }
        );
        assert_eq!(
            game_status(&game, 2),
            LauncherResponse::GameStatus {
                launch_id: 2,
                status: GameProcessStatus::Running,
            }
        );
        let deadline = std::time::Instant::now() + std::time::Duration::from_secs(8);
        loop {
            match game_status(&game, 2) {
                LauncherResponse::GameStatus {
                    status:
                        GameProcessStatus::Exited {
                            success: false,
                            exit_code: Some(9),
                        },
                    ..
                } => break,
                LauncherResponse::GameStatus {
                    status: GameProcessStatus::Running,
                    ..
                } if std::time::Instant::now() < deadline => {
                    std::thread::sleep(std::time::Duration::from_millis(50));
                }
                other => panic!("unexpected process status: {other:?}"),
            }
        }
    }

    #[test]
    fn process_status_reports_a_normal_exit() {
        let mut child = status_test_child("normal");
        assert!(child.wait().unwrap().success());
        let game = Mutex::new(GameProcess {
            last_launch_id: 1,
            child: Some(TrackedGame {
                launch_id: 1,
                child,
            }),
            ..GameProcess::default()
        });
        assert_eq!(
            game_status(&game, 1),
            LauncherResponse::GameStatus {
                launch_id: 1,
                status: GameProcessStatus::Exited {
                    success: true,
                    exit_code: Some(0)
                },
            }
        );
    }

    #[test]
    fn legacy_product_keeps_its_game_entry_and_has_no_standalone_view() {
        use forge_delivery::{archive, install, manifest::*};
        use std::{collections::BTreeMap, fs};

        let root = std::env::temp_dir().join(format!(
            "forge-launcher-v1-{}",
            forge_delivery::attempt().unwrap()
        ));
        let source = root.join("source");
        let installation = root.join("installation");
        fs::create_dir(&root).unwrap();
        fs::create_dir(&source).unwrap();
        fs::write(source.join("game.bin"), b"inert test executable").unwrap();
        fs::write(source.join("LICENSE.txt"), b"fixture CC0-1.0").unwrap();
        let recipe = Launch {
            executable: "game.bin".into(),
            args: Vec::new(),
            env: BTreeMap::new(),
            clear_env_prefixes: Vec::new(),
        };
        let descriptor = Descriptor {
            schema: "forge.delivery.package.v1".into(),
            id: "legacy-applications".into(),
            label: "Aube fixture".into(),
            version: "1.0.0".into(),
            kind: Kind::Applications,
            platform: "any".into(),
            source_revision: "fixture".into(),
            rights: Rights::PrivateLocalOnly,
            license_paths: vec!["LICENSE.txt".into()],
            files: Vec::new(),
            product: Some(Product {
                schema: "forge.delivery.product.v1".into(),
                id: "legacy-game".into(),
                title: "Aube".into(),
                apps: BTreeMap::from([("game".into(), recipe.clone()), ("editor".into(), recipe)]),
                required_packs: Vec::new(),
                optional_packs: Vec::new(),
                authoring_seed: None,
            }),
        };
        let package = root.join("legacy.forgepack");
        archive::pack(&source, descriptor, &package).unwrap();
        let mut plan = install::prepare(&installation, &[package]).unwrap();
        plan.user_root = root.join("user").to_str().unwrap().into();
        plan.command_id.clear();
        plan.command_id = forge_delivery::hash(&forge_delivery::json(&plan).unwrap());
        install::apply(&plan).unwrap();
        assert!(runtime_connected_at(&installation));
        assert!(forge_delivery::launch::command(&installation, "game").is_ok());
        assert!(forge_delivery::standalone::game_view(&installation).is_err());
        assert!(installed_modules::start_entry(&installation).is_err());
        forge_delivery::manifest::safe_chain(&root).unwrap();
        let resolved = root.canonicalize().unwrap();
        let temp = std::env::temp_dir().canonicalize().unwrap();
        assert_eq!(resolved.parent(), Some(temp.as_path()));
        assert!(
            resolved
                .file_name()
                .unwrap()
                .to_string_lossy()
                .starts_with("forge-launcher-v1-")
        );
        fs::remove_dir_all(resolved).unwrap();
    }

    #[test]
    #[cfg(windows)]
    fn immediate_process_failure_is_reported_instead_of_launch_success() {
        use std::os::windows::process::CommandExt;
        let child = std::process::Command::new(std::env::var_os("COMSPEC").unwrap())
            .args(["/D", "/C", "exit 9"])
            .creation_flags(0x0800_0000)
            .spawn()
            .unwrap();
        assert!(confirm_startup(child).is_err());
    }
    #[test]
    fn relative_installation_is_rejected_before_any_launch() {
        assert!(validated_root(Path::new("relative-installation")).is_err());
    }
    #[test]
    fn missing_installation_cannot_launch_a_development_runtime() {
        let root =
            std::env::temp_dir().join(format!("forge-launcher-absent-{}", std::process::id()));
        assert!(!root.exists());
        assert!(start_game(&root).is_err());
    }

    #[test]
    fn absent_module_installation_is_refused_before_spawn() {
        let root = std::env::temp_dir().join(format!(
            "forge-launcher-absent-module-{}",
            std::process::id()
        ));
        assert!(!root.exists());
        let selection: ModuleSelection = serde_json::from_value(serde_json::json!({
            "module_id": "b65b7a97-77d4-58a1-9a0f-3304d680ff2c",
            "area_id": "30e53b52-88ff-50bf-8ded-18283e0a48d6",
        }))
        .unwrap();
        let game = Mutex::new(GameProcess::default());
        assert!(matches!(
            launch_at(&root, &game, Some(selection), None, None),
            LauncherResponse::LaunchBlocked { code, .. } if code == "module-launch-failed"
        ));
        assert!(game.lock().unwrap().child.is_none());
        assert!(!root.exists());
    }
}
