//! Temporary, reopenable uninstall mode of the existing standalone launcher.
use forge_delivery::{install, standalone::UninstallHelper};
use forge_launcher_frontend::LauncherResponse;
#[cfg(any(not(windows), test))]
use std::fs;
use std::{path::Path, sync::Mutex};

const COMPLETION: &str = "complete.json";

pub(super) struct Session {
    helper: UninstallHelper,
    completion_sha256: Option<String>,
}

fn directory(helper: &UninstallHelper) -> Result<&Path, String> {
    helper
        .executable
        .parent()
        .ok_or_else(|| "Dossier temporaire absent.".into())
}

pub(super) fn detect(executable: &Path) -> Result<Option<Mutex<Session>>, String> {
    let Some(helper) = forge_delivery::standalone::read_game_uninstall_helper(executable)? else {
        return Ok(None);
    };
    Ok(Some(Mutex::new(Session {
        helper,
        completion_sha256: None,
    })))
}

pub(super) fn status(session: &Mutex<Session>) -> LauncherResponse {
    let Ok(mut state) = session.lock() else {
        return reject("Désinstallation indisponible.");
    };
    match install::game_uninstall_helper_status(&state.helper) {
        Ok(Some(done)) => {
            if state
                .completion_sha256
                .as_ref()
                .is_some_and(|expected| expected != &done.receipt_sha256)
            {
                return reject("Le reçu final a changé ; le programme temporaire est conservé.");
            }
            state.completion_sha256 = Some(done.receipt_sha256);
            LauncherResponse::UninstallCompleted {
                path: state.helper.executable.to_string_lossy().into_owned(),
                preserved_files: done.report.preserved_files,
                preserved_packages: done.report.preserved_packages,
                counts_partial: done.report.counts_partial,
            }
        }
        Ok(None) if state.completion_sha256.is_none() => LauncherResponse::UninstallHelperReady {
            title: state.helper.title.clone(),
            path: state.helper.executable.to_string_lossy().into_owned(),
        },
        Ok(None) => reject("Le reçu final a disparu ; le programme temporaire est conservé."),
        Err(reason) => reject(&reason),
    }
}

pub(super) fn apply(session: &Mutex<Session>) -> LauncherResponse {
    let result: Result<LauncherResponse, String> = (|| {
        let mut state = session
            .lock()
            .map_err(|_| "Désinstallation indisponible.")?;
        if state.completion_sha256.is_some() {
            return Err("Désinstallation déjà terminée.".into());
        }
        let done = install::uninstall_game_with_helper(&state.helper)?;
        state.completion_sha256 = Some(done.receipt_sha256);
        Ok(LauncherResponse::UninstallCompleted {
            path: state.helper.executable.to_string_lossy().into_owned(),
            preserved_files: done.report.preserved_files,
            preserved_packages: done.report.preserved_packages,
            counts_partial: done.report.counts_partial,
        })
    })();
    result.unwrap_or_else(|reason| reject(&reason))
}

fn reject(reason: &str) -> LauncherResponse {
    LauncherResponse::Rejected {
        reason: reason.to_owned(),
    }
}

pub(super) fn cleanup(session: &Mutex<Session>) -> LauncherResponse {
    let result: Result<LauncherResponse, String> = (|| {
        let state = session
            .lock()
            .map_err(|_| "Désinstallation indisponible.")?;
        let done = install::game_uninstall_helper_status(&state.helper)?
            .ok_or("La désinstallation doit être terminée avant le nettoyage.")?;
        if state.completion_sha256.as_deref() != Some(done.receipt_sha256.as_str()) {
            return Err("Le reçu final a changé ; le programme temporaire est conservé.".into());
        }
        schedule_cleanup(&state.helper, &done.receipt_sha256)?;
        Ok(LauncherResponse::UninstallCleanupRequested)
    })();
    result.unwrap_or_else(|reason| reject(&reason))
}

#[cfg(windows)]
fn schedule_cleanup(helper: &UninstallHelper, completion_sha256: &str) -> Result<(), String> {
    use std::os::windows::process::CommandExt;
    // A separate OS process waits for the mapped image to close. Arguments are
    // carried in environment variables, never interpolated into the script.
    const SCRIPT: &str = r"
$ErrorActionPreference = 'Stop'
function Digest([string]$p) {
  $s = [IO.File]::OpenRead($p)
  try { $a = [Security.Cryptography.SHA256]::Create(); try { ([BitConverter]::ToString($a.ComputeHash($s))).Replace('-','').ToLowerInvariant() } finally { $a.Dispose() } } finally { $s.Dispose() }
}

function Safe([string]$p) {
  $item = Get-Item -Force -LiteralPath $p
  if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'reparse point' }
}
function SafeParents([IO.DirectoryInfo]$d) {
  for ($p = $d; $null -ne $p; $p = $p.Parent) { Safe $p.FullName }
}
try {
  $dir = [IO.DirectoryInfo]::new($env:FORGE_CLEAN_DIRECTORY)
  for ($i = 0; $i -lt 150; $i++) {
    SafeParents $dir
    Safe $env:FORGE_CLEAN_EXECUTABLE; Safe $env:FORGE_CLEAN_RECEIPT; Safe $env:FORGE_CLEAN_COMPLETION
    if ((Digest $env:FORGE_CLEAN_EXECUTABLE) -ne $env:FORGE_CLEAN_EXECUTABLE_SHA -or
        (Digest $env:FORGE_CLEAN_RECEIPT) -ne $env:FORGE_CLEAN_RECEIPT_SHA -or
        (Digest $env:FORGE_CLEAN_COMPLETION) -ne $env:FORGE_CLEAN_COMPLETION_SHA) { exit 2 }
    try { Remove-Item -LiteralPath $env:FORGE_CLEAN_EXECUTABLE -Force -ErrorAction Stop; break }
    catch { Start-Sleep -Milliseconds 200 }
  }
  if (Test-Path -LiteralPath $env:FORGE_CLEAN_EXECUTABLE) { exit 3 }
  SafeParents $dir
  Safe $env:FORGE_CLEAN_RECEIPT; Safe $env:FORGE_CLEAN_COMPLETION
  if ((Digest $env:FORGE_CLEAN_RECEIPT) -ne $env:FORGE_CLEAN_RECEIPT_SHA -or
      (Digest $env:FORGE_CLEAN_COMPLETION) -ne $env:FORGE_CLEAN_COMPLETION_SHA) { exit 4 }
  Remove-Item -LiteralPath $env:FORGE_CLEAN_RECEIPT -Force
  Remove-Item -LiteralPath $env:FORGE_CLEAN_COMPLETION -Force
  if (-not (Get-ChildItem -Force -LiteralPath $dir.FullName | Select-Object -First 1)) { Remove-Item -LiteralPath $dir.FullName }
} catch { exit 1 }
";
    let dir = directory(helper)?;
    let receipt = dir.join("helper.json");
    let done = dir.join(COMPLETION);
    let system = std::env::var_os("SystemRoot").ok_or("SystemRoot indisponible.")?;
    let powershell =
        std::path::PathBuf::from(system).join("System32/WindowsPowerShell/v1.0/powershell.exe");
    if !powershell.is_absolute() {
        return Err("Programme de nettoyage indisponible.".into());
    }
    std::process::Command::new(powershell)
        .args([
            "-NoLogo",
            "-NoProfile",
            "-NonInteractive",
            "-Command",
            SCRIPT,
        ])
        .env("FORGE_CLEAN_DIRECTORY", dir)
        .env("FORGE_CLEAN_EXECUTABLE", &helper.executable)
        .env("FORGE_CLEAN_RECEIPT", receipt)
        .env("FORGE_CLEAN_COMPLETION", done)
        .env("FORGE_CLEAN_EXECUTABLE_SHA", &helper.executable_sha256)
        .env("FORGE_CLEAN_RECEIPT_SHA", &helper.receipt_sha256)
        .env("FORGE_CLEAN_COMPLETION_SHA", completion_sha256)
        .creation_flags(0x0800_0000)
        .spawn()
        .map_err(|error| format!("Nettoyage temporaire impossible : {error}"))?;
    Ok(())
}

#[cfg(not(windows))]
fn schedule_cleanup(helper: &UninstallHelper, completion_sha256: &str) -> Result<(), String> {
    let dir = directory(helper)?;
    let receipt = dir.join("helper.json");
    let done = dir.join(COMPLETION);
    let entries = [
        (&helper.executable, helper.executable_sha256.as_str()),
        (&receipt, helper.receipt_sha256.as_str()),
        (&done, completion_sha256),
    ];
    for (path, expected) in entries {
        verify_cleanup_file(dir, path, expected)?;
    }
    for (path, expected) in entries {
        verify_cleanup_file(dir, path, expected)?;
        fs::remove_file(path).map_err(|error| error.to_string())?;
    }
    forge_delivery::manifest::safe_chain(dir)?;
    match fs::remove_dir(dir) {
        Ok(()) => Ok(()),
        Err(error) if error.kind() == std::io::ErrorKind::DirectoryNotEmpty => Ok(()),
        Err(error) => Err(error.to_string()),
    }
}

#[cfg(not(windows))]
fn verify_cleanup_file(directory: &Path, path: &Path, expected: &str) -> Result<(), String> {
    forge_delivery::manifest::safe_chain(directory)?;
    forge_delivery::manifest::no_links(path)?;
    let metadata = fs::symlink_metadata(path).map_err(|error| error.to_string())?;
    if !metadata.is_file() || forge_delivery::file_hash(path)? != expected {
        return Err("Fichier temporaire modifié ; il est conservé.".into());
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_delivery::standalone::ProjectId;
    use serde::Serialize;
    use std::path::PathBuf;

    #[derive(Serialize)]
    struct Receipt {
        schema: &'static str,
        installation_root: String,
        game_id: ProjectId,
        title: &'static str,
        bytes: u64,
        sha256: String,
    }

    struct Fixture(PathBuf);

    impl Fixture {
        fn new() -> (Self, PathBuf, ProjectId) {
            let root = std::env::temp_dir().join(format!(
                "forge-maintenance-test-{}",
                forge_delivery::attempt().unwrap()
            ));
            let directory = root.join(".forge-game-uninstall-test");
            fs::create_dir_all(&directory).unwrap();
            let executable = directory.join(if cfg!(windows) {
                "Desinstaller.exe"
            } else {
                "Desinstaller"
            });
            let payload = b"inert helper";
            fs::write(&executable, payload).unwrap();
            let game_id = ProjectId::deterministic("maintenance-test");
            let receipt = Receipt {
                schema: "forge.delivery.game-uninstall-helper.v2",
                installation_root: fs::canonicalize(&root)
                    .unwrap()
                    .to_str()
                    .unwrap()
                    .to_owned(),
                game_id,
                title: "Jeu de test",
                bytes: payload.len() as u64,
                sha256: forge_delivery::hash(payload),
            };
            fs::write(
                directory.join("helper.json"),
                forge_delivery::json(&receipt).unwrap(),
            )
            .unwrap();
            (Self(root), executable, game_id)
        }
    }

    impl Drop for Fixture {
        fn drop(&mut self) {
            fs::remove_dir_all(&self.0).unwrap();
        }
    }

    #[test]
    fn helper_preserves_ambiguous_or_modified_completion() {
        let (_fixture, executable, _game_id) = Fixture::new();
        let session = detect(&executable).unwrap().unwrap();
        assert!(!matches!(
            status(&session),
            LauncherResponse::UninstallCompleted { .. }
        ));
        assert!(matches!(
            cleanup(&session),
            LauncherResponse::Rejected { .. }
        ));
        fs::write(executable.parent().unwrap().join(COMPLETION), b"modified").unwrap();
        assert!(matches!(
            status(&session),
            LauncherResponse::Rejected { .. }
        ));
        assert!(matches!(
            cleanup(&session),
            LauncherResponse::Rejected { .. }
        ));
        assert!(executable.is_file());
    }

    #[cfg(unix)]
    #[test]
    fn unix_cleanup_refuses_links_and_preserves_the_target() {
        let (fixture, executable, _game_id) = Fixture::new();
        let foreign = fixture.0.join("personal.txt");
        fs::write(&foreign, b"personal").unwrap();
        fs::remove_file(&executable).unwrap();
        std::os::unix::fs::symlink(&foreign, &executable).unwrap();
        assert!(
            verify_cleanup_file(
                executable.parent().unwrap(),
                &executable,
                &forge_delivery::hash(b"personal")
            )
            .is_err()
        );
        assert_eq!(fs::read(foreign).unwrap(), b"personal");
    }
}
