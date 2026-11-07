//! Installer mode of the same standalone launcher, with plans kept in native memory.
use forge_delivery::game_builder::{self, CandidateInspection, CandidateInstall};
use forge_launcher_frontend::LauncherResponse;
use std::{
    ffi::OsString,
    path::{Path, PathBuf},
    sync::Mutex,
};

#[derive(Default)]
pub(super) struct Installer {
    reviewed: Option<CandidateInspection>,
    prepared: Option<CandidateInstall>,
    completed: bool,
}

pub(super) fn durable_location() -> Result<Option<(PathBuf, PathBuf)>, String> {
    if !cfg!(feature = "standalone") {
        return Ok(None);
    }
    let executable = std::env::current_exe().map_err(|error| error.to_string())?;
    let arguments: Vec<_> = std::env::args_os().skip(1).collect();
    durable_route(&executable, &arguments, |root, executable| {
        for attempt in 0..=40 {
            match forge_delivery::launch::validate_internal_launcher(root, executable) {
                Err(error)
                    if forge_delivery::install::is_lock_contention(&error) && attempt < 40 =>
                {
                    std::thread::sleep(std::time::Duration::from_millis(25));
                }
                result => return result,
            }
        }
        Err("Vérification du lanceur interrompue.".into())
    })
}

fn durable_route(
    executable: &Path,
    arguments: &[OsString],
    validate_internal: impl FnOnce(&Path, &Path) -> Result<(), String>,
) -> Result<Option<(PathBuf, PathBuf)>, String> {
    if !is_player_name(executable) {
        return Ok(None);
    }
    if !arguments.is_empty() {
        if !(arguments.len() == 2 || arguments.len() == 4)
            || arguments[0] != "--root"
            || (arguments.len() == 4 && arguments[2] != "--expected-game-id")
        {
            return Err("Ce lanceur ne peut pas utiliser ces arguments.".into());
        }
        let root = forge_delivery::manifest::normalized_directory(Path::new(&arguments[1]))?;
        validate_internal(&root, executable)?;
        return Ok(None);
    }
    let root = executable
        .parent()
        .ok_or("Dossier du jeu absent.")?
        .to_path_buf();
    Ok(Some((root, executable.to_path_buf())))
}

pub(super) fn is_player_name(executable: &Path) -> bool {
    let name = executable
        .file_name()
        .and_then(|name| name.to_str())
        .unwrap_or_default();
    let expected = forge_delivery::install::player_filename();
    if cfg!(windows) {
        name.eq_ignore_ascii_case(expected)
    } else {
        name == expected
    }
}

pub(super) fn candidate_location() -> Result<Option<(PathBuf, PathBuf)>, String> {
    if !cfg!(feature = "standalone")
        || std::env::args_os()
            .skip(1)
            .any(|argument| argument == "--root")
    {
        return Ok(None);
    }
    let executable = std::env::current_exe().map_err(|error| error.to_string())?;
    let folder = executable
        .parent()
        .ok_or("Dossier du programme introuvable.")?
        .to_path_buf();
    match std::fs::symlink_metadata(folder.join("candidate.json")) {
        Ok(_) => Ok(Some((folder, executable))),
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => Ok(None),
        Err(error) => Err(format!("Impossible de lire le dossier du jeu : {error}")),
    }
}

fn rejection(reason: impl std::fmt::Display) -> LauncherResponse {
    LauncherResponse::Rejected {
        reason: reason.to_string(),
    }
}

fn entry_status(plan: &CandidateInstall) -> (Option<String>, Option<String>) {
    match plan.player_entry_status() {
        Ok(forge_delivery::install::PlayerEntryStatus::Ready) => {
            match forge_delivery::install::player_entry_path(plan.root()) {
                Ok(path) => (Some(path.to_string_lossy().into_owned()), None),
                Err(error) => (None, Some(error)),
            }
        }
        Ok(forge_delivery::install::PlayerEntryStatus::NeedsRepair) => (None, None),
        Err(error) => (None, Some(error)),
    }
}

pub(super) fn status(state: &Mutex<Installer>) -> LauncherResponse {
    let result: Result<LauncherResponse, String> = (|| {
        let Some((source, executable)) = candidate_location()? else {
            return Ok(LauncherResponse::NotInstaller);
        };
        let candidate = game_builder::inspect_candidate(&source, &executable)?;
        let destination = forge_delivery::launch::default_game_root(candidate.game_id)?;
        let response = LauncherResponse::InstallerReady {
            title: candidate.title.clone(),
            destination: destination
                .to_str()
                .ok_or("Dossier d’installation non pris en charge.")?
                .to_owned(),
        };
        let mut state = state
            .lock()
            .map_err(|_| "Installation temporairement indisponible.")?;
        state.reviewed = Some(candidate);
        state.prepared = None;
        state.completed = false;
        Ok(response)
    })();
    result.unwrap_or_else(rejection)
}

pub(super) fn prepare(state: &Mutex<Installer>, destination: &str) -> LauncherResponse {
    let result: Result<LauncherResponse, String> = (|| {
        if destination.is_empty() || destination.len() > 4096 {
            return Err("Indiquez un dossier d’installation valide.".to_owned());
        }
        if !Path::new(destination).is_absolute() {
            return Err("Indiquez le chemin complet du dossier d’installation.".to_owned());
        }
        let (source, executable) = candidate_location()?.ok_or("Installation indisponible.")?;
        let mut state = state
            .lock()
            .map_err(|_| "Installation temporairement indisponible.")?;
        state.prepared = None;
        state.completed = false;
        let plan = game_builder::prepare_candidate_install(&source, &executable, Path::new(destination))
            .map_err(|error| format!("Vérification impossible. Vérifiez les fichiers du jeu et l’accès au dossier choisi. Détails : {error}"))?;
        if state.reviewed.as_ref() != Some(plan.inspection()) {
            return Err("Les fichiers du jeu ont changé. Vérifiez-les à nouveau.".to_owned());
        }
        let already_installed = plan.is_current()?;
        let (entry, entry_error) = if already_installed {
            entry_status(&plan)
        } else {
            (None, None)
        };
        let response = LauncherResponse::InstallPrepared {
            destination: plan
                .root()
                .to_str()
                .ok_or("Dossier d’installation non pris en charge.")?
                .to_owned(),
            update: plan.is_update(),
            already_installed,
            entry,
            entry_error,
        };
        state.prepared = Some(plan);
        state.completed = already_installed;
        Ok(response)
    })();
    result.unwrap_or_else(rejection)
}

pub(super) fn apply(state: &Mutex<Installer>) -> LauncherResponse {
    let result: Result<LauncherResponse, String> = (|| {
        let mut state = state
            .lock()
            .map_err(|_| "Installation temporairement indisponible.")?;
        if state.completed {
            return Err("Cette installation est déjà terminée.".to_owned());
        }
        let plan = state
            .prepared
            .as_ref()
            .ok_or("Vérifiez à nouveau l’installation.")?;
        if let Err(error) = game_builder::apply_candidate_install(plan) {
            state.prepared = None;
            return Err(format!(
                "Installation interrompue. Vérifiez les fichiers et l’accès au dossier, puis réessayez. Détails : {error}"
            ));
        }
        state.completed = true;
        let plan = state.prepared.as_ref().ok_or("Installation introuvable.")?;
        let (entry, entry_error) = match plan.ensure_player_entry() {
            Ok(path) => (Some(path.to_string_lossy().into_owned()), None),
            Err(error) => (None, Some(error)),
        };
        Ok(LauncherResponse::InstallCompleted { entry, entry_error })
    })();
    result.unwrap_or_else(rejection)
}

pub(super) fn launch(state: &Mutex<Installer>) -> LauncherResponse {
    let result: Result<LauncherResponse, String> = (|| {
        let mut state = state
            .lock()
            .map_err(|_| "Installation temporairement indisponible.")?;
        let result = {
            let Installer {
                prepared,
                completed,
                ..
            } = &mut *state;
            launch_once(prepared, completed, |plan| {
                plan.launch_installed().map(|child| child.id())
            })
        };
        if result.is_err()
            && let Some(plan) = state.prepared.as_mut()
        {
            plan.invalidate_installed();
        }
        let process_id = result?;
        Ok(LauncherResponse::InstalledLauncherStarted { process_id })
    })();
    result.unwrap_or_else(rejection)
}

pub(super) fn reverify_installed(state: &Mutex<Installer>) -> LauncherResponse {
    let result: Result<LauncherResponse, String> = (|| {
        let mut state = state
            .lock()
            .map_err(|_| "Installation temporairement indisponible.")?;
        state.completed = false;
        let plan = state.prepared.as_mut().ok_or("Installation introuvable.")?;
        plan.invalidate_installed();
        plan.revalidate_installed()
            .map_err(|error| format!("Vérification impossible. Détails : {error}"))?;
        let (entry, entry_error) = entry_status(plan);
        state.completed = true;
        Ok(LauncherResponse::InstallRevalidated { entry, entry_error })
    })();
    result.unwrap_or_else(rejection)
}

pub(super) fn ensure_entry(state: &Mutex<Installer>) -> LauncherResponse {
    let result: Result<LauncherResponse, String> = (|| {
        let state = state
            .lock()
            .map_err(|_| "Installation temporairement indisponible.")?;
        if !state.completed {
            return Err("Vérifiez l’installation avant de réparer son lanceur.".to_owned());
        }
        let plan = state.prepared.as_ref().ok_or("Installation introuvable.")?;
        let path = plan.ensure_player_entry().map_err(|error| {
            format!("Le jeu est installé, mais son lanceur doit être réparé. Détails : {error}")
        })?;
        Ok(LauncherResponse::PlayerEntryReady {
            path: path.to_string_lossy().into_owned(),
        })
    })();
    result.unwrap_or_else(rejection)
}

fn launch_once<T>(
    prepared: &mut Option<T>,
    completed: &mut bool,
    open: impl FnOnce(&T) -> Result<u32, String>,
) -> Result<u32, String> {
    if !*completed {
        return Err("Vérifiez l’installation avant de lancer le jeu.".to_owned());
    }
    let plan = prepared.as_ref().ok_or("Installation introuvable.")?;
    match open(plan) {
        Ok(process_id) => Ok(process_id),
        Err(error) => {
            *completed = false;
            Err(format!(
                "Ouverture impossible. L’installation est conservée ; vérifiez-la à nouveau. Détails : {error}"
            ))
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn player_entry_rejects_root_override_unless_the_internal_path_is_verified() {
        let root = forge_delivery::manifest::normalized_directory(&std::env::temp_dir()).unwrap();
        let entry = root.join(forge_delivery::install::player_filename());
        let args = [OsString::from("--root"), root.clone().into_os_string()];
        assert!(durable_route(&entry, &args, |_, _| Err("foreign launcher".into())).is_err());
        assert!(
            durable_route(&entry, &args, |checked, exe| {
                assert_eq!(checked, root.as_path());
                assert_eq!(exe, entry.as_path());
                Ok(())
            })
            .unwrap()
            .is_none()
        );
        assert!(
            durable_route(&entry, &[], |_, _| panic!("entry must not use an override"))
                .unwrap()
                .is_some()
        );
        assert!(durable_route(&entry, &[OsString::from("--unexpected")], |_, _| Ok(())).is_err());
    }

    #[test]
    fn failed_open_invalidates_the_plan_before_any_retry() {
        let mut prepared = Some("reviewed revision");
        let mut completed = true;
        let calls = std::cell::Cell::new(0);
        let failure = launch_once(&mut prepared, &mut completed, |_| {
            calls.set(calls.get() + 1);
            Err("Game installation changed".into())
        });
        assert!(failure.unwrap_err().contains("installation est conservée"));
        assert!(prepared.is_some());
        assert!(!completed);
        assert!(
            launch_once(&mut prepared, &mut completed, |_| {
                calls.set(calls.get() + 1);
                Ok(1)
            })
            .is_err()
        );
        assert_eq!(calls.get(), 1);
    }
}
