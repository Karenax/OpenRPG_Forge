//! Launch the configured native runtime with an immutable current-zone snapshot.
use forge_core::{ContentHash, EntityId, bytes_sha256};
use forge_gameplay::QuestStatus;
use forge_project::{authoring::AuthoringRequest, zone_authoring::session::ZoneEditSession};
use serde::{Deserialize, Serialize};
use std::{
    fs::{self, File, OpenOptions},
    io::{Read, Write},
    path::{Path, PathBuf},
    process::{Child, Command, Stdio},
    sync::Mutex,
    time::{SystemTime, UNIX_EPOCH},
};
#[path = "native_game_module.rs"]
mod module;

struct RunningGame {
    child: Child,
    _input: Option<File>,
    log: PathBuf,
    pins: ReportPins,
}
struct ReportPins {
    pid: u32,
    source_hash: ContentHash,
    archive_hash: ContentHash,
    functional_hash: Option<ContentHash>,
    session: PathBuf,
}

struct GameState {
    running: Option<RunningGame>,
    last_diagnostics: Option<NativeGameDiagnostics>,
}
static GAME: Mutex<GameState> = Mutex::new(GameState {
    running: None,
    last_diagnostics: None,
});

const MAX_REPORT_BYTES: u64 = 65_536;

#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct NativeGameReport {
    schema: String,
    version: u32,
    pid: u32,
    source_hash: ContentHash,
    archive_hash: ContentHash,
    functional_hash: ContentHash,
    exit_success: bool,
    ready: bool,
    tick: u64,
    sequence: u64,
    quests: Vec<QuestReport>,
    inventory_total: u64,
    health: u32,
    stamina: u32,
    saved: Option<SavedReport>,
    feedback: String,
    last_command_error: Option<String>,
    simulation_error: Option<String>,
    resume_refused: Option<String>,
}

#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
struct QuestReport {
    id: EntityId,
    status: QuestStatus,
}

#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
struct SavedReport {
    path: PathBuf,
    sha256: ContentHash,
    state_hash: ContentHash,
    source_hash: ContentHash,
    functional_hash: ContentHash,
}

/// A bounded report associated with the exited child, never a qualification verdict.
#[derive(Clone, Debug, Serialize)]
pub struct NativeGameDiagnostics {
    status: &'static str,
    pid: u32,
    source_hash: ContentHash,
    archive_hash: ContentHash,
    exit_success: bool,
    process_error: Option<String>,
    report_sha256: Option<ContentHash>,
    report: Option<NativeGameReport>,
    reason: Option<String>,
}

#[tauri::command]
pub fn native_game_running() -> Result<bool, String> {
    let mut game = GAME.lock().map_err(|_| "État du jeu indisponible")?;
    poll_game(&mut game)
}

#[tauri::command]
pub fn native_game_diagnostics() -> Result<Option<NativeGameDiagnostics>, String> {
    let mut game = GAME.lock().map_err(|_| "État du jeu indisponible")?;
    // Process failures have their own explicit diagnostics. A still-running or
    // unreadable process must never inherit the report of an earlier launch.
    let _ = poll_game(&mut game);
    Ok(if game.running.is_some() {
        None
    } else {
        game.last_diagnostics.clone()
    })
}

fn poll_game(game: &mut GameState) -> Result<bool, String> {
    let Some(mut running) = game.running.take() else {
        return Ok(false);
    };
    let exit = match running.child.try_wait() {
        Ok(Some(exit)) => exit,
        Ok(None) => {
            game.running = Some(running);
            return Ok(true);
        }
        Err(error) => {
            game.running = Some(running);
            return Err(error.to_string());
        }
    };
    let process_error = (!exit.success()).then(|| {
        format!(
            "Le jeu s’est arrêté sur une erreur ({exit}). Journal : {}",
            running.log.display()
        )
    });
    game.last_diagnostics = Some(finished_diagnostics(
        &running.pins,
        exit.success(),
        process_error.clone(),
    ));
    if let Some(error) = process_error {
        return Err(error);
    }
    Ok(false)
}

fn finished_diagnostics(
    pins: &ReportPins,
    exit_success: bool,
    process_error: Option<String>,
) -> NativeGameDiagnostics {
    let mut diagnostics = NativeGameDiagnostics {
        status: "absent",
        pid: pins.pid,
        source_hash: pins.source_hash,
        archive_hash: pins.archive_hash,
        exit_success,
        process_error,
        report_sha256: None,
        report: None,
        reason: None,
    };
    match read_report(pins, exit_success) {
        Ok(Some((report, hash))) => {
            diagnostics.status = "received";
            diagnostics.report = Some(report);
            diagnostics.report_sha256 = Some(hash);
        }
        Ok(None) => {
            diagnostics.reason = Some(
                "Aucun bilan de partie n’a été fourni. Le parcours n’est pas confirmé par ce retour.".into(),
            );
        }
        Err(error) => {
            diagnostics.status = "invalid";
            diagnostics.reason = Some(format!("Bilan de partie refusé : {error}"));
        }
    }
    diagnostics
}

fn regular_report_file(path: &Path) -> Result<Option<fs::Metadata>, String> {
    match fs::symlink_metadata(path) {
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => Ok(None),
        Err(error) => Err(error.to_string()),
        Ok(metadata) => {
            if !metadata.is_file()
                || metadata.file_type().is_symlink()
                || metadata.len() > MAX_REPORT_BYTES
            {
                return Err("fichier absent du contrat, lien ou quota dépassé".into());
            }
            Ok(Some(metadata))
        }
    }
}

fn same_report_entry(left: &fs::Metadata, right: &fs::Metadata) -> bool {
    if left.len() != right.len() || left.modified().ok() != right.modified().ok() {
        return false;
    }
    #[cfg(unix)]
    {
        use std::os::unix::fs::MetadataExt;
        left.dev() == right.dev() && left.ino() == right.ino()
    }
    #[cfg(windows)]
    {
        use std::os::windows::fs::MetadataExt;
        left.creation_time() == right.creation_time()
            && left.file_attributes() == right.file_attributes()
    }
    #[cfg(not(any(unix, windows)))]
    {
        left.created().ok() == right.created().ok()
    }
}

fn read_report(
    pins: &ReportPins,
    exit_success: bool,
) -> Result<Option<(NativeGameReport, ContentHash)>, String> {
    read_report_with_checkpoint(pins, exit_success, || Ok(()))
}

fn read_report_with_checkpoint(
    pins: &ReportPins,
    exit_success: bool,
    after_open: impl FnOnce() -> Result<(), String>,
) -> Result<Option<(NativeGameReport, ContentHash)>, String> {
    let path = pins.session.join("game-report.json");
    let Some(before) = regular_report_file(&path)? else {
        return Ok(None);
    };
    if path.canonicalize().map_err(|e| e.to_string())? != path {
        return Err("le fichier de bilan a quitté le dossier de session".into());
    }
    let mut options = OpenOptions::new();
    options.read(true);
    #[cfg(windows)]
    {
        use std::os::windows::fs::OpenOptionsExt;
        // Open a reparse point itself rather than following a substituted link.
        options.custom_flags(0x0020_0000);
    }
    let file = options.open(&path).map_err(|e| e.to_string())?;
    let opened = file.metadata().map_err(|e| e.to_string())?;
    if !opened.is_file() || opened.file_type().is_symlink() || !same_report_entry(&before, &opened)
    {
        return Err("le fichier de bilan a été substitué avant lecture".into());
    }
    after_open()?;
    let mut bytes = Vec::new();
    file.take(MAX_REPORT_BYTES + 1)
        .read_to_end(&mut bytes)
        .map_err(|e| e.to_string())?;
    let after = regular_report_file(&path)?.ok_or("le fichier de bilan a disparu")?;
    if bytes.len() as u64 > MAX_REPORT_BYTES || !same_report_entry(&before, &after) {
        return Err("le fichier de bilan a changé pendant lecture".into());
    }
    let report = parse_report(&bytes, pins, exit_success)?;
    Ok(Some((report, bytes_sha256(&bytes))))
}

fn parse_report(
    bytes: &[u8],
    pins: &ReportPins,
    exit_success: bool,
) -> Result<NativeGameReport, String> {
    if bytes.len() as u64 > MAX_REPORT_BYTES {
        return Err("quota de bilan dépassé".into());
    }
    let report: NativeGameReport = serde_json::from_slice(bytes).map_err(|e| e.to_string())?;
    let wire: serde_json::Value = serde_json::from_slice(bytes).map_err(|e| e.to_string())?;
    if [
        "saved",
        "last_command_error",
        "simulation_error",
        "resume_refused",
    ]
    .iter()
    .any(|field| wire.get(*field).is_none())
    {
        return Err("champs de bilan obligatoires absents".into());
    }
    if report.schema != "forge.native-gameplay.report"
        || report.version != 1
        || report.pid != pins.pid
        || report.source_hash != pins.source_hash
        || report.archive_hash != pins.archive_hash
        || Some(report.functional_hash) != pins.functional_hash
        || report.exit_success != exit_success
    {
        return Err("identité ou sortie du bilan différente du processus lancé".into());
    }
    if [&report.feedback]
        .into_iter()
        .chain(report.last_command_error.as_ref())
        .chain(report.simulation_error.as_ref())
        .chain(report.resume_refused.as_ref())
        .any(|text| text.len() > 1024)
        || report
            .saved
            .as_ref()
            .is_some_and(|saved| !saved.path.is_absolute())
    {
        return Err("texte de bilan hors quota ou chemin de sauvegarde invalide".into());
    }
    Ok(report)
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

/// The browser names a native export receipt by pins, never by a filesystem path.
#[tauri::command]
pub async fn launch_exported_module(
    expected_hash: String,
    package_sha256: String,
) -> Result<u32, String> {
    let expected = ContentHash::parse(&expected_hash).map_err(|e| e.to_string())?;
    let package = ContentHash::parse(&package_sha256).map_err(|e| e.to_string())?;
    tauri::async_runtime::spawn_blocking(move || module::launch(expected, package))
        .await
        .map_err(|e| e.to_string())?
}

#[allow(
    clippy::too_many_lines,
    reason = "Snapshot, child logging and spawn are one ordered launch transaction"
)]
fn launch(expected: ContentHash) -> Result<u32, String> {
    let mut current = GAME.lock().map_err(|_| "État du jeu indisponible")?;
    let previous = poll_game(&mut current);
    if current.running.is_some() {
        previous?;
        return Err("Le jeu est déjà ouvert dans une autre fenêtre.".into());
    }
    current.last_diagnostics = None;
    let config = forge_studio_frontend::launch_configuration::game()?;
    if !config.executable.is_file() {
        return Err(format!("Jeu introuvable : {}", config.executable.display()));
    }
    let archive = super::with_authoring_host(|host| host.game_archive(expected))?;
    let zone = ZoneEditSession::from_archive_bytes(&archive)?;
    let archive_hash = bytes_sha256(&archive);
    let functional_hash = zone
        .head()
        .corrections
        .gameplay
        .as_deref()
        .map(|document| document.functional_hash().map_err(|e| e.to_string()))
        .transpose()?;
    let (terrain, _) = zone.head().effective()?;
    if terrain.sampled_source.is_none() {
        if functional_hash.is_none() {
            return Err("Créer les définitions RPG de cette carte avant de lancer le jeu.".into());
        }
        // The exporter and module launcher acquire their own host/game locks.
        // Keep the ordinary imported-terrain recipe below unchanged.
        drop(current);
        return launch_authored_module(expected, terrain.name);
    }
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
    let session = session.canonicalize().map_err(|e| e.to_string())?;
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
        let normalized = name.to_string_lossy().to_ascii_uppercase();
        if normalized.starts_with("FORGE_TIDEWATER_")
            || normalized.starts_with("FORGE_NATIVE_GAMEPLAY_")
            || normalized == "FORGE_GAME_OBSERVATIONS"
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
        .arg(archive_hash.to_string())
        .arg("--building-package")
        .arg(data.join("buildings"))
        .arg(building_hash.to_string())
        .arg("--walk")
        .env("FORGE_GAME_SAVE_ROOT", config.session_root.join("saves"))
        .env("FORGE_GAME_REPORT_PATH", session.join("game-report.json"))
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
    let child = super::with_authoring_host(|host| {
        host.game_archive(expected)?;
        command
            .spawn()
            .map_err(|e| format!("Démarrage du jeu impossible : {e}"))
    })?;
    let id = child.id();
    current.running = Some(RunningGame {
        child,
        _input: None,
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

fn launch_authored_module(expected: ContentHash, name: String) -> Result<u32, String> {
    let exported = crate::native_module_export::execute(
        forge_studio_frontend::module_export_protocol::ModuleExportRequest {
            schema_version: 1,
            expected_head: expected,
            name,
            version: "0.0.0-local".into(),
            publisher: "Session locale du Studio".into(),
            license_text: "Instantané de jeu à usage local. Les licences et conditions du contenu importé restent applicables et sont conservées avec leurs preuves. Aucun droit de redistribution supplémentaire n’est accordé par ce lancement.".into(),
            redistributable: false,
        },
    )?;
    module::launch(exported.source_hash, exported.package_sha256)
}

#[cfg(test)]
mod tests {
    use super::*;

    struct Directory {
        root: PathBuf,
        parent: PathBuf,
    }

    impl Directory {
        fn new() -> Self {
            let parent = std::env::temp_dir().canonicalize().unwrap();
            let stamp = SystemTime::now()
                .duration_since(UNIX_EPOCH)
                .unwrap()
                .as_nanos();
            let root = parent.join(format!(
                "forge-studio-native-report-{}-{stamp}",
                std::process::id()
            ));
            fs::create_dir(&root).unwrap();
            Self {
                root: root.canonicalize().unwrap(),
                parent,
            }
        }
    }

    impl Drop for Directory {
        fn drop(&mut self) {
            if self
                .root
                .canonicalize()
                .ok()
                .as_ref()
                .and_then(|path| path.parent())
                == Some(self.parent.as_path())
            {
                let _ = fs::remove_dir_all(&self.root);
            }
        }
    }

    fn fixture(session: PathBuf) -> (ReportPins, Vec<u8>) {
        let source_hash = ContentHash::from_bytes([1; 32]);
        let archive_hash = ContentHash::from_bytes([2; 32]);
        let functional_hash = ContentHash::from_bytes([3; 32]);
        let report = NativeGameReport {
            schema: "forge.native-gameplay.report".into(),
            version: 1,
            pid: 42,
            source_hash,
            archive_hash,
            functional_hash,
            exit_success: true,
            ready: true,
            tick: 17,
            sequence: 2,
            quests: vec![QuestReport {
                id: EntityId::deterministic("report/quest"),
                status: QuestStatus::Completed,
            }],
            inventory_total: 1,
            health: 80,
            stamina: 95,
            saved: None,
            feedback: "Partie terminée".into(),
            last_command_error: None,
            simulation_error: None,
            resume_refused: None,
        };
        (
            ReportPins {
                pid: 42,
                source_hash,
                archive_hash,
                functional_hash: Some(functional_hash),
                session,
            },
            serde_json::to_vec(&report).unwrap(),
        )
    }

    #[test]
    fn native_report_accepts_only_the_exited_process_and_exact_launch_pins() {
        let directory = Directory::new();
        let (mut pins, bytes) = fixture(directory.root.clone());
        let report = parse_report(&bytes, &pins, true).unwrap();
        assert_eq!(report.health, 80);
        assert_eq!(report.quests[0].status, QuestStatus::Completed);
        assert!(parse_report(&bytes, &pins, false).is_err());
        pins.pid += 1;
        assert!(parse_report(&bytes, &pins, true).is_err());
        pins.pid -= 1;
        pins.source_hash = ContentHash::from_bytes([8; 32]);
        assert!(parse_report(&bytes, &pins, true).is_err());
        pins.source_hash = ContentHash::from_bytes([1; 32]);
        pins.archive_hash = ContentHash::from_bytes([8; 32]);
        assert!(parse_report(&bytes, &pins, true).is_err());
        pins.archive_hash = ContentHash::from_bytes([2; 32]);
        pins.functional_hash = Some(ContentHash::from_bytes([8; 32]));
        assert!(parse_report(&bytes, &pins, true).is_err());
    }

    #[test]
    fn native_report_rejects_partial_unknown_duplicate_or_oversize_data() {
        let directory = Directory::new();
        let (pins, bytes) = fixture(directory.root.clone());
        for field in [
            "saved",
            "last_command_error",
            "simulation_error",
            "resume_refused",
        ] {
            let mut partial: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
            partial.as_object_mut().unwrap().remove(field);
            assert!(parse_report(&serde_json::to_vec(&partial).unwrap(), &pins, true).is_err());
        }
        let mut unknown: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
        unknown["extra"] = serde_json::json!(true);
        assert!(parse_report(&serde_json::to_vec(&unknown).unwrap(), &pins, true).is_err());
        let duplicate = String::from_utf8(bytes.clone())
            .unwrap()
            .replacen('{', "{\"pid\":42,", 1);
        assert!(parse_report(duplicate.as_bytes(), &pins, true).is_err());
        assert!(parse_report(&bytes[..bytes.len() - 1], &pins, true).is_err());
        assert!(parse_report(&vec![b' '; 65_537], &pins, true).is_err());
        let mut long: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
        long["feedback"] = serde_json::json!("x".repeat(1025));
        assert!(parse_report(&serde_json::to_vec(&long).unwrap(), &pins, true).is_err());
    }

    #[test]
    fn native_report_missing_or_invalid_is_explicit_and_last_diagnostic_survives_idle() {
        let directory = Directory::new();
        let (pins, bytes) = fixture(directory.root.clone());
        let process_error = format!(
            "Le jeu s’est arrêté sur une erreur (7). Journal : {}",
            pins.session.join("game.log").display()
        );
        let absent = finished_diagnostics(&pins, false, Some(process_error.clone()));
        assert_eq!(absent.status, "absent");
        assert!(absent.report.is_none() && absent.reason.is_some());
        assert!(!absent.exit_success);
        let mut crashed = GameState {
            running: None,
            last_diagnostics: Some(absent),
        };
        assert!(!poll_game(&mut crashed).unwrap());
        assert!(!poll_game(&mut crashed).unwrap());
        let persisted = serde_json::to_value(crashed.last_diagnostics.as_ref().unwrap()).unwrap();
        assert_eq!(persisted["status"], "absent");
        assert_eq!(persisted["exit_success"], false);
        assert_eq!(persisted["process_error"], process_error);
        fs::write(pins.session.join("game-report.json"), &bytes).unwrap();
        let received = finished_diagnostics(&pins, true, None);
        assert_eq!(received.status, "received");
        assert_eq!(received.report_sha256, Some(bytes_sha256(&bytes)));
        let mut state = GameState {
            running: None,
            last_diagnostics: Some(received),
        };
        assert!(!poll_game(&mut state).unwrap());
        assert!(!poll_game(&mut state).unwrap());
        assert_eq!(state.last_diagnostics.as_ref().unwrap().status, "received");
        fs::write(pins.session.join("game-report.json"), b"not json").unwrap();
        let invalid = finished_diagnostics(&pins, false, Some(process_error.clone()));
        assert_eq!(invalid.status, "invalid");
        assert!(invalid.report.is_none() && invalid.reason.is_some());
        assert!(!invalid.exit_success);
        assert_eq!(invalid.process_error, Some(process_error));
    }

    #[test]
    fn native_report_file_quota_and_substitution_are_refused() {
        let directory = Directory::new();
        let (pins, bytes) = fixture(directory.root.clone());
        let path = pins.session.join("game-report.json");
        fs::write(&path, vec![b' '; 65_537]).unwrap();
        assert!(read_report(&pins, true).is_err());
        fs::write(&path, &bytes).unwrap();
        let replacement = read_report_with_checkpoint(&pins, true, || {
            fs::rename(&path, pins.session.join("original-report.json"))
                .map_err(|e| e.to_string())?;
            let mut new_bytes = bytes.clone();
            new_bytes.push(b' ');
            fs::write(&path, new_bytes).map_err(|e| e.to_string())
        });
        assert!(replacement.unwrap_err().contains("changé pendant lecture"));
    }

    #[cfg(unix)]
    #[test]
    fn native_report_symlink_with_matching_pins_is_refused() {
        let directory = Directory::new();
        let (pins, bytes) = fixture(directory.root.clone());
        let other = pins.session.join("other.json");
        fs::write(&other, bytes).unwrap();
        std::os::unix::fs::symlink(&other, pins.session.join("game-report.json")).unwrap();
        assert!(read_report(&pins, true).is_err());
    }
}
