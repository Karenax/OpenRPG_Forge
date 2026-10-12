use forge_launcher_frontend::LauncherResponse;
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
    match start_game(&root) {
        Ok(child) => {
            let process_id = child.id();
            *current = Some(child);
            LauncherResponse::LaunchStarted { process_id }
        }
        Err(reason) => blocked("runtime-missing", reason),
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
        let child = std::process::Command::new(std::env::var_os("COMSPEC").unwrap())
            .args(["/D", "/C", "exit 9"])
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
}
