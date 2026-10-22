//! Launch only a package produced by the native exporter, with its exact reviewed pins.
use super::{
    Command, ContentHash, File, GAME, OpenOptions, ReportPins, RunningGame, Stdio, SystemTime,
    UNIX_EPOCH, bytes_sha256, fs, poll_game, write_new,
};
use std::path::Path;

fn open_export(path: &Path, expected: ContentHash) -> Result<File, String> {
    forge_delivery::manifest::safe_chain(path)?;
    let mut options = OpenOptions::new();
    options.read(true);
    #[cfg(windows)]
    {
        use std::os::windows::fs::OpenOptionsExt;
        // Retain read-only sharing until the child exits: no replacement or write.
        options.share_mode(1);
    }
    let file = options.open(path).map_err(|e| e.to_string())?;
    if forge_delivery::file_hash(path)? != expected.to_string() {
        return Err("Le fichier exporté a changé ; refaire l’export.".into());
    }
    Ok(file)
}

pub(super) fn launch(expected: ContentHash, package: ContentHash) -> Result<u32, String> {
    let mut current = GAME.lock().map_err(|_| "État du jeu indisponible")?;
    let previous = poll_game(&mut current);
    if current.running.is_some() {
        previous?;
        return Err("Le jeu est déjà ouvert dans une autre fenêtre.".into());
    }
    current.last_diagnostics = None;
    let receipt = crate::native_module_export::receipt(expected, package)?;
    // Refuse a changed authoring head even when the package itself is still intact.
    crate::with_authoring_host(|host| {
        if host.module_content_revision(expected)? != receipt.module_hash {
            return Err("Le module a changé ; refaire l’export.".into());
        }
        Ok(())
    })?;
    let config = forge_studio_frontend::launch_configuration::game()?;
    if !config.executable.is_file() {
        return Err(format!("Jeu introuvable : {}", config.executable.display()));
    }
    let input = open_export(&receipt.package_path, package)?;
    let stamp = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map_err(|e| e.to_string())?
        .as_nanos();
    forge_delivery::manifest::safe_chain(&config.session_root)?;
    fs::create_dir_all(&config.session_root).map_err(|e| e.to_string())?;
    let session = config
        .session_root
        .join(format!("module-game-{stamp}-{}", std::process::id()));
    fs::create_dir(&session).map_err(|e| e.to_string())?;
    let session = session.canonicalize().map_err(|e| e.to_string())?;
    let loaded = forge_delivery::module_runtime::load(
        &receipt.package_path,
        receipt.area_id,
        Some(&session.join("cache")),
    )?;
    if loaded.zone.head().content_hash()? != expected
        || loaded.manifest.module_id != receipt.module_id
    {
        return Err("Le module exporté ne correspond plus à la carte examinée.".into());
    }
    if forge_delivery::file_hash(&receipt.package_path)? != package.to_string() {
        return Err("Le package a changé pendant sa vérification ; refaire l’export.".into());
    }
    let archive_hash = bytes_sha256(&loaded.archive);
    let functional_hash = loaded
        .zone
        .head()
        .corrections
        .gameplay
        .as_deref()
        .map(|document| document.functional_hash().map_err(|e| e.to_string()))
        .transpose()?;
    write_new(&session.join("zone.json"), &loaded.archive)?;
    let mut command = module_command(&config, &receipt, &session, expected, archive_hash)?;
    // Recheck immediately before spawn; no frontend-supplied path or arguments are accepted.
    let child = crate::with_authoring_host(|host| {
        if host.module_content_revision(expected)? != receipt.module_hash {
            return Err(
                "Le module a changé pendant la préparation du jeu ; refaire l’export.".into(),
            );
        }
        crate::native_module_export::receipt(expected, package)?;
        command
            .spawn()
            .map_err(|e| format!("Démarrage du module impossible : {e}"))
    })?;
    let id = child.id();
    current.running = Some(RunningGame {
        child,
        _input: Some(input),
        log: session.join("game.log"),
        pins: ReportPins {
            pid: id,
            source_hash: expected,
            archive_hash,
            functional_hash,
            session: session.clone(),
        },
    });
    std::thread::sleep(std::time::Duration::from_millis(150));
    if !poll_game(&mut current)? {
        return Err(format!(
            "Le jeu s’est fermé avant son ouverture. Journal : {}",
            session.join("game.log").display()
        ));
    }
    Ok(id)
}

fn module_command(
    config: &forge_studio_frontend::launch_configuration::GameConfiguration,
    receipt: &forge_delivery::module_builder::ExportedModule,
    session: &Path,
    expected: ContentHash,
    archive_hash: ContentHash,
) -> Result<Command, String> {
    let log = File::create(session.join("game.log")).map_err(|e| e.to_string())?;
    let mut command = Command::new(&config.executable);
    for (name, _) in std::env::vars_os() {
        let normalized = name.to_string_lossy().to_ascii_uppercase();
        if normalized.starts_with("FORGE_TIDEWATER_")
            || normalized.starts_with("FORGE_NATIVE_GAMEPLAY_")
            || normalized == "FORGE_GAME_OBSERVATIONS"
        {
            command.env_remove(name);
        }
    }
    command
        .current_dir(session)
        .arg("--module")
        .arg(&receipt.package_path)
        .arg("--zone")
        .arg(receipt.area_id.to_string())
        .arg("--cache")
        .arg(session.join("cache"))
        .arg("--save-root")
        .arg(config.session_root.join("saves"))
        .arg("--expected-head")
        .arg(expected.to_string())
        .arg("--expected-archive-sha256")
        .arg(archive_hash.to_string())
        .env("FORGE_GAME_REPORT_PATH", session.join("game-report.json"))
        .stdout(Stdio::from(log.try_clone().map_err(|e| e.to_string())?))
        .stderr(Stdio::from(log));
    #[cfg(windows)]
    {
        use std::os::windows::process::CommandExt;
        command.creation_flags(0x0800_0000);
    }
    Ok(command)
}
