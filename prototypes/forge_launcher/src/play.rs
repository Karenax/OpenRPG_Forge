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

fn required_paths(root: &Path) -> [PathBuf; 15] {
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
        root.join("content/debris/procedural/debris-native.json"),
        root.join("content/debris/scanned/package.json"),
        root.join("content/debris/scanned.wgsl"),
        root.join("content/debris/pebbles/program.json"),
        root.join("content/debris/pebbles/pebbles.wgsl"),
        root.join("content/fish/game/package.json"),
        root.join("content/fish/surface/package.json"),
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
    let (capture, log_path) = output_path()?;
    let log = File::create(&log_path)
        .map_err(|error| format!("Impossible d’ouvrir le journal du jeu : {error}"))?;
    let error_log = log
        .try_clone()
        .map_err(|error| format!("Impossible de préparer le journal du jeu : {error}"))?;
    let mut command = game_command(root, &capture);
    command
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

fn game_command(root: &Path, capture: &Path) -> Command {
    let data = root.join("data");
    let debris = root.join("content/debris");
    let mut command = Command::new(root.join("runtime/tidewater.exe"));
    remove_development_overrides(&mut command, std::env::vars_os().map(|(name, _)| name));
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
        .env("FORGE_TIDEWATER_DEBRIS_ROOT", debris.join("procedural"))
        .env("FORGE_TIDEWATER_SCANNED_ROOT", debris.join("scanned"))
        .env(
            "FORGE_TIDEWATER_GAME_FISH_ROOT",
            root.join("content/fish/game"),
        )
        .env(
            "FORGE_TIDEWATER_FISH_PROP_ROOT",
            root.join("content/fish/surface"),
        )
        .env(
            "FORGE_TIDEWATER_PEBBLE_PROGRAM_ROOT",
            debris.join("pebbles"),
        )
        .env(
            "FORGE_TIDEWATER_SCANNED_PROGRAM",
            debris.join("scanned.wgsl"),
        )
        .env_remove("FORGE_TIDEWATER_STATIC_POSTFX");
    command
}

fn remove_development_overrides(
    command: &mut Command,
    names: impl IntoIterator<Item = std::ffi::OsString>,
) {
    for name in names {
        // Windows environment names are case insensitive. Clear diagnostics
        // only in the child; the launcher's environment remains untouched.
        if name
            .to_str()
            .is_some_and(|name| name.to_ascii_uppercase().starts_with("FORGE_TIDEWATER_"))
        {
            command.env_remove(name);
        }
    }
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
    fn launch_removes_inherited_diagnostics_without_changing_unrelated_settings() {
        let mut command = Command::new("unused-test-program");
        remove_development_overrides(
            &mut command,
            [
                "FORGE_TIDEWATER_UNDERWATER_DISABLED",
                "forge_tidewater_graphics_proof",
                "FORGE_TIDEWATER_REFRACTION_PROOF",
                "PATH",
                "FORGE_AUTHORING_ROOT",
            ]
            .map(std::ffi::OsString::from),
        );
        let environment: std::collections::BTreeMap<_, _> = command.get_envs().collect();
        for name in [
            "FORGE_TIDEWATER_UNDERWATER_DISABLED",
            "forge_tidewater_graphics_proof",
            "FORGE_TIDEWATER_REFRACTION_PROOF",
        ] {
            assert_eq!(environment.get(OsStr::new(name)), Some(&None));
        }
        assert!(!environment.contains_key(OsStr::new("PATH")));
        assert!(!environment.contains_key(OsStr::new("FORGE_AUTHORING_ROOT")));
    }

    #[test]
    fn relocated_distribution_overrides_development_debris_paths() {
        let root = std::env::temp_dir().join("Forge déplacé avec espaces");
        let capture = root.join("captures/test.png");
        let command = game_command(&root, &capture);
        assert_eq!(command.get_program(), root.join("runtime/tidewater.exe"));
        assert_eq!(command.get_current_dir(), Some(root.as_path()));
        let environment: std::collections::BTreeMap<_, _> = command.get_envs().collect();
        for (name, relative) in [
            ("FORGE_TIDEWATER_DEBRIS_ROOT", "content/debris/procedural"),
            ("FORGE_TIDEWATER_SCANNED_ROOT", "content/debris/scanned"),
            ("FORGE_TIDEWATER_GAME_FISH_ROOT", "content/fish/game"),
            ("FORGE_TIDEWATER_FISH_PROP_ROOT", "content/fish/surface"),
            (
                "FORGE_TIDEWATER_PEBBLE_PROGRAM_ROOT",
                "content/debris/pebbles",
            ),
            (
                "FORGE_TIDEWATER_SCANNED_PROGRAM",
                "content/debris/scanned.wgsl",
            ),
        ] {
            assert_eq!(
                environment
                    .get(OsStr::new(name))
                    .copied()
                    .flatten()
                    .map(Path::new),
                Some(root.join(relative).as_path())
            );
        }
        assert_eq!(
            environment.get(OsStr::new("FORGE_TIDEWATER_STATIC_POSTFX")),
            Some(&None)
        );
        assert_eq!(command.get_args().last(), Some(OsStr::new("--walk")));
    }

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
