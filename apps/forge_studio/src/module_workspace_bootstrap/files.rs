use forge_delivery::manifest::safe_chain;
use std::{
    fs::{self, File, OpenOptions},
    io::{Read, Write},
    path::{Path, PathBuf},
    sync::atomic::{AtomicU64, Ordering},
    time::{SystemTime, UNIX_EPOCH},
};

static NEXT: AtomicU64 = AtomicU64::new(0);

pub(super) fn fresh_destination(destination: &Path) -> Result<PathBuf, String> {
    if !destination.is_absolute() {
        return Err("Bootstrap destination must be absolute".into());
    }
    safe_chain(destination)?;
    match fs::symlink_metadata(destination) {
        Ok(_) => return Err("Bootstrap destination already exists".into()),
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => (),
        Err(error) => return Err(error.to_string()),
    }
    let parent = destination
        .parent()
        .ok_or("Bootstrap destination parent absent")?;
    safe_chain(parent)?;
    let parent = parent.canonicalize().map_err(|error| error.to_string())?;
    if !parent.is_dir() {
        return Err("Bootstrap destination parent must be an existing directory".into());
    }
    Ok(parent)
}

pub(super) fn new_stage(parent: &Path) -> Result<PathBuf, String> {
    let timestamp = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map_err(|error| error.to_string())?
        .as_nanos();
    let path = parent.join(format!(
        ".forge-module-bootstrap-{}-{timestamp}-{}",
        std::process::id(),
        NEXT.fetch_add(1, Ordering::Relaxed)
    ));
    safe_chain(&path)?;
    fs::create_dir(&path).map_err(|error| error.to_string())?;
    safe_chain(&path)?;
    Ok(path)
}

pub(super) fn read_bounded(path: &Path, maximum: u64) -> Result<Vec<u8>, String> {
    safe_chain(path)?;
    let file = File::open(path).map_err(|error| format!("{}: {error}", path.display()))?;
    let metadata = file.metadata().map_err(|error| error.to_string())?;
    if !metadata.is_file() || metadata.len() > maximum {
        return Err(format!(
            "File is not regular or exceeds byte budget: {}",
            path.display()
        ));
    }
    let mut bytes = Vec::new();
    file.take(maximum.checked_add(1).ok_or("Byte budget overflow")?)
        .read_to_end(&mut bytes)
        .map_err(|error| error.to_string())?;
    if u64::try_from(bytes.len()).ok() != Some(metadata.len()) || bytes.len() as u64 > maximum {
        return Err(format!("File changed while reading: {}", path.display()));
    }
    safe_chain(path)?;
    Ok(bytes)
}

pub(super) fn write_new(path: &Path, bytes: &[u8]) -> Result<(), String> {
    safe_chain(path)?;
    let mut file = OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(path)
        .map_err(|error| format!("{}: {error}", path.display()))?;
    file.write_all(bytes).map_err(|error| error.to_string())?;
    file.sync_all().map_err(|error| error.to_string())
}

pub(super) fn activate(stage: &Path, destination: &Path) -> Result<(), String> {
    fresh_destination(destination)?;
    safe_chain(stage)?;
    fs::rename(stage, destination)
        .map_err(|error| format!("Activate bootstrap destination: {error}"))
}
