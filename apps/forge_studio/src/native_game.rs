//! Launch the configured native runtime with an immutable current-zone snapshot.
use forge_core::{ContentHash, bytes_sha256};
use forge_project::{authoring::AuthoringRequest, zone_authoring::session::ZoneEditSession};
use std::{
    fs::{self, File, OpenOptions},
    io::Write,
    path::{Path, PathBuf},
    process::{Child, Command, Stdio},
    sync::Mutex,
    time::{SystemTime, UNIX_EPOCH},
};

struct RunningGame {
    child: Child,
    log: PathBuf,
}
static GAME: Mutex<Option<RunningGame>> = Mutex::new(None);

#[tauri::command]
pub fn native_game_running() -> Result<bool, String> {
    let mut game = GAME.lock().map_err(|_| "État du jeu indisponible")?;
    if let Some(child) = game.as_mut() {
        let Some(exit) = child.child.try_wait().map_err(|e| e.to_string())? else {
            return Ok(true);
        };
        let failure = (!exit.success()).then(|| {
            format!(
                "Le jeu s’est arrêté sur une erreur ({exit}). Journal : {}",
                child.log.display()
            )
        });
        *game = None;
        if let Some(failure) = failure {
            return Err(failure);
        }
    }
    Ok(false)
}

fn write_new(path: &Path, bytes: &[u8]) -> Result<(), String> {
    OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(path)
        .and_then(|mut file| file.write_all(bytes))
        .map_err(|e| e.to_string())
}

#[tauri::command]
pub async fn launch_native_game(expected_hash: String) -> Result<u32, String> {
    let expected = ContentHash::parse(&expected_hash).map_err(|e| e.to_string())?;
    tauri::async_runtime::spawn_blocking(move || launch(expected))
        .await
        .map_err(|e| e.to_string())?
}

#[allow(
    clippy::too_many_lines,
    reason = "Snapshot, child logging and spawn are one ordered launch transaction"
)]
fn launch(expected: ContentHash) -> Result<u32, String> {
    let mut current = GAME.lock().map_err(|_| "État du jeu indisponible")?;
    if let Some(child) = current.as_mut() {
        if child.child.try_wait().map_err(|e| e.to_string())?.is_none() {
            return Err("Le jeu est déjà ouvert dans une autre fenêtre.".into());
        }
        *current = None;
    }
    let config = forge_studio_frontend::launch_configuration::game()?;
    if !config.executable.is_file() {
        return Err(format!("Jeu introuvable : {}", config.executable.display()));
    }
    let archive = super::with_authoring_host(|host| host.game_archive(expected))?;
    let zone = ZoneEditSession::from_archive_bytes(&archive)?;
    let (terrain, _) = zone.head().effective()?;
    let source = terrain
        .sampled_source
        .as_ref()
        .ok_or("Le runtime configuré attend un terrain importé")?;
    let vegetation = zone
        .head()
        .base
        .vegetation
        .as_ref()
        .and_then(|v| v.imported_source.as_ref())
        .ok_or("Végétation importée absente")?;
    let stamp = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map_err(|e| e.to_string())?
        .as_nanos();
    fs::create_dir_all(&config.session_root).map_err(|e| e.to_string())?;
    let session = config
        .session_root
        .join(format!("game-{stamp}-{}", std::process::id()));
    fs::create_dir(&session).map_err(|e| e.to_string())?;
    write_new(&session.join("zone.json"), &archive)?;
    super::with_authoring_host(|host| {
        let inspected = host.execute(AuthoringRequest::Inspect)?;
        let document = inspected.document.ok_or("Carte fermée")?;
        let terrain_hash = document.content_hash().map_err(|e| e.to_string())?;
        let mut bytes = Vec::with_capacity(source.byte_length().map_err(|e| e.to_string())?);
        while bytes.len() < source.byte_length().map_err(|e| e.to_string())? {
            bytes.extend(host.read_terrain_source(terrain_hash, bytes.len())?.bytes);
        }
        if bytes_sha256(&bytes) != source.samples_sha256 {
            return Err("Raster du jeu incohérent".into());
        }
        write_new(&session.join("heights.bin"), &bytes)?;
        for (index, block) in vegetation.blocks.iter().enumerate() {
            let mut bytes = Vec::with_capacity(block.byte_len()?);
            while bytes.len() < block.byte_len()? {
                bytes.extend(
                    host.read_vegetation_source(expected, index, bytes.len())?
                        .bytes,
                );
            }
            if bytes_sha256(&bytes) != block.instances_sha256 {
                return Err("Population du jeu incohérente".into());
            }
            write_new(
                &session.join(format!("{}.bin", block.instances_sha256)),
                &bytes,
            )?;
        }
        host.game_archive(expected)?; // Reject a session change before spawning.
        Ok(())
    })?;
    let log = File::create(session.join("game.log")).map_err(|e| e.to_string())?;
    let mut command = Command::new(&config.executable);
    for (name, _) in std::env::vars_os() {
        if name
            .to_string_lossy()
            .to_ascii_uppercase()
            .starts_with("FORGE_TIDEWATER_")
        {
            command.env_remove(name);
        }
    }
    let data = config.content_root.join("data");
    let building_hash =
        bytes_sha256(&fs::read(data.join("buildings/package.json")).map_err(|e| e.to_string())?);
    command
        .current_dir(&config.content_root)
        .arg(data.join("scene"))
        .arg(session.join("game.png"))
        .arg("--water-spray-clouds")
        .arg(data.join("ocean"))
        .arg(data.join("shore"))
        .arg(data.join("spray"))
        .arg(data.join("clouds"))
        .arg("--studio-snapshot")
        .arg(&session)
        .arg(bytes_sha256(&archive).to_string())
        .arg("--building-package")
        .arg(data.join("buildings"))
        .arg(building_hash.to_string())
        .arg("--walk")
        .stdout(Stdio::from(log.try_clone().map_err(|e| e.to_string())?))
        .stderr(Stdio::from(log));
    let debris = config.content_root.join("content/debris");
    command
        .env("FORGE_TIDEWATER_DEBRIS_ROOT", debris.join("procedural"))
        .env("FORGE_TIDEWATER_SCANNED_ROOT", debris.join("scanned"))
        .env(
            "FORGE_TIDEWATER_SCANNED_PROGRAM",
            debris.join("scanned.wgsl"),
        )
        .env(
            "FORGE_TIDEWATER_PEBBLE_PROGRAM_ROOT",
            debris.join("pebbles"),
        )
        .env(
            "FORGE_TIDEWATER_GAME_FISH_ROOT",
            config.content_root.join("content/fish/game"),
        )
        .env(
            "FORGE_TIDEWATER_FISH_PROP_ROOT",
            config.content_root.join("content/fish/surface"),
        );
    #[cfg(windows)]
    {
        use std::os::windows::process::CommandExt;
        command.creation_flags(0x0800_0000);
    }
    let mut child = command
        .spawn()
        .map_err(|e| format!("Démarrage du jeu impossible : {e}"))?;
    std::thread::sleep(std::time::Duration::from_millis(150));
    if let Some(exit) = child.try_wait().map_err(|e| e.to_string())? {
        return Err(format!(
            "Le jeu a refusé le démarrage ({exit}). Journal : {}",
            session.join("game.log").display()
        ));
    }
    let id = child.id();
    *current = Some(RunningGame {
        child,
        log: session.join("game.log"),
    });
    Ok(id)
}
