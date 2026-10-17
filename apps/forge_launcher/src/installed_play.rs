use forge_delivery::installed_modules::{self, ModuleSelection};
use forge_launcher_frontend::{InstalledAdventure, LauncherResponse, RejectedAdventure};
use std::{
    path::{Path, PathBuf},
    process::Child,
    sync::Mutex,
};

fn validated_root(root: PathBuf) -> Result<PathBuf, String> {
    if !root.is_absolute() {
        return Err("Le dossier d’installation doit être absolu.".into());
    }
    forge_delivery::manifest::safe_chain(&root)?;
    Ok(root)
}

fn distribution_root() -> Result<PathBuf, String> {
    let mut args = std::env::args_os().skip(1);
    while let Some(arg) = args.next() {
        if arg == "--root" {
            return validated_root(PathBuf::from(
                args.next().ok_or("Dossier d’installation absent.")?,
            ));
        }
    }
    if let Some(root) = std::env::var_os("FORGE_DELIVERY_INSTALL_ROOT") {
        return validated_root(PathBuf::from(root));
    }
    let executable = std::env::current_exe().map_err(|error| error.to_string())?;
    validated_root(
        executable
            .parent()
            .ok_or("Dossier du launcher absent.")?
            .to_path_buf(),
    )
}

pub(super) fn runtime_connected() -> bool {
    distribution_root().is_ok_and(|root| forge_delivery::launch::command(&root, "game").is_ok())
}

pub(super) fn modules() -> LauncherResponse {
    match distribution_root().and_then(|root| installed_modules::inventory(&root)) {
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

pub(super) fn launch(game: &Mutex<Option<Child>>) -> LauncherResponse {
    let root = match distribution_root() {
        Ok(root) => root,
        Err(reason) => return blocked("runtime-missing", reason),
    };
    launch_at(&root, game, None)
}

pub(super) fn launch_module(
    game: &Mutex<Option<Child>>,
    selection: ModuleSelection,
) -> LauncherResponse {
    let root = match distribution_root() {
        Ok(root) => root,
        Err(reason) => return blocked("runtime-missing", reason),
    };
    launch_at(&root, game, Some(selection))
}

fn launch_at(
    root: &Path,
    game: &Mutex<Option<Child>>,
    selection: Option<ModuleSelection>,
) -> LauncherResponse {
    let Ok(mut current) = game.lock() else {
        return blocked("runtime-unavailable", "État du jeu indisponible.".into());
    };
    if let Some(child) = current.as_mut() {
        match child.try_wait() {
            Ok(None) => return blocked("already-running", "Le jeu est déjà ouvert.".into()),
            Ok(Some(_)) => *current = None,
            Err(error) => return blocked("runtime-unavailable", error.to_string()),
        }
    }
    let result = match selection {
        // Rebuild and admit the plan natively from IDs, never from frontend paths.
        Some(selection) => installed_modules::start(root, selection).and_then(confirm_startup),
        None => start_game(root),
    };
    match result {
        Ok(child) => {
            let process_id = child.id();
            *current = Some(child);
            LauncherResponse::LaunchStarted { process_id }
        }
        Err(reason) => blocked(
            if selection.is_some() {
                "module-launch-failed"
            } else {
                "runtime-missing"
            },
            reason,
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
        assert!(validated_root(PathBuf::from("relative-installation")).is_err());
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
        let game = Mutex::new(None);
        assert!(matches!(
            launch_at(&root, &game, Some(selection)),
            LauncherResponse::LaunchBlocked { code, .. } if code == "module-launch-failed"
        ));
        assert!(game.lock().unwrap().is_none());
        assert!(!root.exists());
    }
}
