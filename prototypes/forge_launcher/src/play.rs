use forge_launcher_frontend::LauncherResponse;
use std::{
    ffi::OsStr,
    fs::{self, File},
    path::{Path, PathBuf},
    process::{Child, Command, Stdio},
    sync::Mutex,
    time::{SystemTime, UNIX_EPOCH},
};

const AREA_ID: &str = "e620dd19-ead9-5913-a6d6-0f93109148c0";
const BUILDING_PACKAGE: &str = "a53b6987563b0b0d5bb5b9113e1c3f27142baf53658a9e1a78338854f99599e6";
#[cfg(windows)]
const CREATE_NO_WINDOW: u32 = 0x0800_0000;

fn distribution_root() -> Result<PathBuf, String> {
    std::env::current_exe()
        .map_err(|error| format!("Chemin du lanceur introuvable : {error}"))?
        .parent()
        .map(Path::to_path_buf)
        .ok_or_else(|| "Dossier du lanceur introuvable.".to_owned())
}

fn required_paths(root: &Path) -> [PathBuf; 8] {
    let data = root.join("data");
    [
        root.join("runtime/tidewater.exe"),
        data.join("scene/fixture.json"),
        data.join("ocean/ocean.json"),
        data.join("shore/shore-sim.json"),
        data.join("spray/spray.json"),
        data.join("clouds/environment.json"),
        data.join(format!("workspace/{AREA_ID}")),
        data.join("buildings"),
    ]
}

fn missing_file(root: &Path) -> Option<PathBuf> {
    required_paths(root).into_iter().find(|path| !path.exists())
}

pub(super) fn runtime_connected() -> bool {
    distribution_root().is_ok_and(|root| missing_file(&root).is_none())
}

fn output_path() -> Result<(PathBuf, PathBuf), String> {
    let base = std::env::var_os("LOCALAPPDATA").map_or_else(std::env::temp_dir, PathBuf::from);
    let directory = base
        .join("OpenRPG Forge")
        .join("Tidewater")
        .join("captures");
    fs::create_dir_all(&directory)
        .map_err(|error| format!("Impossible de créer le dossier de captures : {error}"))?;
    let timestamp = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map_err(|error| format!("Horloge système indisponible : {error}"))?
        .as_nanos();
    let stem = format!("tidewater-{timestamp}-{}", std::process::id());
    Ok((
        directory.join(format!("{stem}.png")),
        directory.join(format!("{stem}.log")),
    ))
}

fn start_game(root: &Path) -> Result<Child, String> {
    if let Some(path) = missing_file(root) {
        return Err(format!("Fichier de l’île absent : {}", path.display()));
    }
    let data = root.join("data");
    let (capture, log_path) = output_path()?;
    let log = File::create(&log_path)
        .map_err(|error| format!("Impossible d’ouvrir le journal du jeu : {error}"))?;
    let error_log = log
        .try_clone()
        .map_err(|error| format!("Impossible de préparer le journal du jeu : {error}"))?;
    let mut command = Command::new(root.join("runtime/tidewater.exe"));
    command
        .current_dir(root)
        .arg(data.join("scene"))
        .arg(capture)
        .arg(OsStr::new("--water-spray-clouds"))
        .arg(data.join("ocean"))
        .arg(data.join("shore"))
        .arg(data.join("spray"))
        .arg(data.join("clouds"))
        .arg(OsStr::new("--workspace-vegetation"))
        .arg(data.join("workspace"))
        .arg(AREA_ID)
        .arg(OsStr::new("--building-package"))
        .arg(data.join("buildings"))
        .arg(BUILDING_PACKAGE)
        .arg(OsStr::new("--walk"))
        .stdout(Stdio::from(log))
        .stderr(Stdio::from(error_log));
    #[cfg(windows)]
    {
        use std::os::windows::process::CommandExt;
        command.creation_flags(CREATE_NO_WINDOW);
    }
    command
        .spawn()
        .map_err(|error| format!("Impossible de démarrer l’île Tidewater : {error}"))
}

pub(super) fn launch(game: &Mutex<Option<Child>>) -> LauncherResponse {
    let root = match distribution_root() {
        Ok(root) => root,
        Err(reason) => return blocked("runtime-missing", reason),
    };
    let Ok(mut current) = game.lock() else {
        return blocked(
            "runtime-unavailable",
            "État du jeu indisponible.".to_owned(),
        );
    };
    if let Some(child) = current.as_mut() {
        match child.try_wait() {
            Ok(None) => {
                return blocked(
                    "already-running",
                    "L’île Tidewater est déjà ouverte.".to_owned(),
                );
            }
            Ok(Some(_)) => *current = None,
            Err(error) => {
                return blocked(
                    "runtime-unavailable",
                    format!("État du jeu illisible : {error}"),
                );
            }
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
        code: code.to_owned(),
        reason,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn package_requires_the_island_data_and_native_game() {
        let unique = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        let root = std::env::temp_dir().join(format!("forge-tidewater-launcher-{unique}"));
        fs::create_dir_all(&root).unwrap();
        assert_eq!(
            missing_file(&root),
            Some(root.join("runtime/tidewater.exe"))
        );
        for path in required_paths(&root) {
            if path.extension().is_some() {
                fs::create_dir_all(path.parent().unwrap()).unwrap();
                File::create(path).unwrap();
            } else {
                fs::create_dir_all(path).unwrap();
            }
        }
        assert_eq!(missing_file(&root), None);
        fs::remove_dir_all(root).unwrap();
    }
}
