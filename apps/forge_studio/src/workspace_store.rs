//! Native storage for immutable workspace snapshots. No arbitrary write/overwrite command.

use forge_project::{MAX_WORKSPACE_JSON_BYTES, WorkspaceDocument};
use std::{
    fs::{self, File, OpenOptions},
    io::{Read, Write},
    path::{Component, Path, PathBuf},
    sync::atomic::{AtomicU64, Ordering},
    time::{SystemTime, UNIX_EPOCH},
};

static NONCE: AtomicU64 = AtomicU64::new(0);

pub(crate) fn unique_key() -> Result<String, String> {
    let now = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map_err(|e| e.to_string())?;
    Ok(format!(
        "{}-{}-{}",
        std::process::id(),
        now.as_nanos(),
        NONCE.fetch_add(1, Ordering::Relaxed)
    ))
}

fn checked_path(path: &Path, directory: bool) -> Result<PathBuf, String> {
    if !path.is_absolute() || path.components().any(|c| matches!(c, Component::ParentDir)) {
        return Err("Choisir un chemin absolu, sans segment '..'.".into());
    }
    #[cfg(windows)]
    for component in path.components() {
        if let Component::Prefix(prefix) = component
            && !matches!(
                prefix.kind(),
                std::path::Prefix::Disk(_) | std::path::Prefix::VerbatimDisk(_)
            )
        {
            return Err("Seuls les chemins de disque local sont pris en charge.".into());
        }
    }
    for ancestor in path.ancestors() {
        let metadata =
            fs::symlink_metadata(ancestor).map_err(|e| format!("Chemin inaccessible : {e}"))?;
        if metadata.file_type().is_symlink() {
            return Err("Les liens symboliques ne sont pas acceptés pour ce parcours.".into());
        }
        #[cfg(windows)]
        {
            use std::os::windows::fs::MetadataExt;
            if metadata.file_attributes() & 0x400 != 0 {
                return Err(
                    "Les points de réanalyse ne sont pas acceptés pour ce parcours.".into(),
                );
            }
        }
    }
    let canonical = fs::canonicalize(path).map_err(|e| e.to_string())?;
    if directory != canonical.is_dir() || (!directory && !canonical.is_file()) {
        return Err("Le type de chemin choisi ne correspond pas à l'opération.".into());
    }
    if directory && canonical.parent().is_none() {
        return Err("Choisir un dossier de projet, pas la racine du disque.".into());
    }
    Ok(canonical)
}

pub(crate) fn open_snapshot(path: &Path) -> Result<(WorkspaceDocument, PathBuf), String> {
    if !path
        .file_name()
        .and_then(|n| n.to_str())
        .is_some_and(|n| n.ends_with(".forge.json"))
    {
        return Err("Choisir un fichier projet .forge.json.".into());
    }
    let canonical = checked_path(path, false)?;
    let mut bytes = Vec::new();
    File::open(&canonical)
        .map_err(|e| e.to_string())?
        .take(MAX_WORKSPACE_JSON_BYTES as u64 + 1)
        .read_to_end(&mut bytes)
        .map_err(|e| e.to_string())?;
    if bytes.len() > MAX_WORKSPACE_JSON_BYTES {
        return Err("Le fichier projet dépasse la taille autorisée.".into());
    }
    let json = std::str::from_utf8(&bytes).map_err(|e| e.to_string())?;
    let document = WorkspaceDocument::from_json(json).map_err(|e| e.to_string())?;
    Ok((document, canonical))
}

pub(crate) fn save_snapshot(
    document: &WorkspaceDocument,
    directory: &Path,
) -> Result<PathBuf, String> {
    let directory = checked_path(directory, true)?;
    let json = document.to_pretty_json().map_err(|e| e.to_string())?;
    let destination = directory.join(format!(
        "{}-r{}.forge.json",
        document.project.id,
        document.revision.get()
    ));
    // Existing identical snapshots are idempotent, conflicting ones are never overwritten.
    if destination.exists() {
        return accept_existing(document, &destination);
    }
    let temporary = directory.join(format!(".forge-pending-{}", unique_key()?));
    let mut file = OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(&temporary)
        .map_err(|e| e.to_string())?;
    let result = (|| {
        file.write_all(json.as_bytes()).map_err(|e| e.to_string())?;
        file.sync_all().map_err(|e| e.to_string())?;
        drop(file);
        // A hard link publishes a fully written file atomically without a replacement race.
        match fs::hard_link(&temporary, &destination) {
            Ok(()) => Ok(destination.clone()),
            Err(error) if error.kind() == std::io::ErrorKind::AlreadyExists => {
                accept_existing(document, &destination)
            }
            Err(error) => Err(format!(
                "Publication atomique impossible sur ce dossier : {error}"
            )),
        }
    })();
    // This exact nonce file was created exclusively by this invocation; no recursive cleanup.
    let cleanup = fs::remove_file(&temporary);
    match (result, cleanup) {
        (Ok(path), Ok(())) => Ok(path),
        (Ok(path), Err(error)) => Err(format!(
            "Snapshot publié dans {}, mais nettoyage temporaire impossible : {error}",
            path.display()
        )),
        (Err(error), _) => Err(error),
    }
}

fn accept_existing(document: &WorkspaceDocument, path: &Path) -> Result<PathBuf, String> {
    let (existing, canonical) = open_snapshot(path)?;
    if existing.content_hash().map_err(|e| e.to_string())?
        != document.content_hash().map_err(|e| e.to_string())?
    {
        return Err("Un fichier différent existe déjà : aucun octet n'a été écrasé.".into());
    }
    Ok(canonical)
}
