//! Read-only authority check while the caller holds the legacy zone lock.
use super::{MAX_WORKSPACE_BYTES, WorkspaceHead, session::WorkspaceSession};
use forge_core::AreaId;
use std::{
    fs::{self, File},
    io::Read,
    path::Path,
};

pub fn require_unmanaged(root: &Path, area: AreaId) -> Result<(), String> {
    let authority = root.join("workspace-v1");
    let meta = match fs::symlink_metadata(&authority) {
        Ok(meta) => meta,
        Err(e) if e.kind() == std::io::ErrorKind::NotFound => return Ok(()),
        Err(e) => return Err(e.to_string()),
    };
    if !meta.is_dir() || meta.file_type().is_symlink() {
        return Err("Invalid workspace authority directory".into());
    }
    match fs::symlink_metadata(authority.join("authoring.pending")) {
        Ok(_) => {
            return Err("Resolve interrupted workspace save before opening a legacy zone".into());
        }
        Err(e) if e.kind() == std::io::ErrorKind::NotFound => (),
        Err(e) => return Err(e.to_string()),
    }
    let path = authority.join("authoring.json");
    let meta = match fs::symlink_metadata(&path) {
        Ok(meta) => meta,
        Err(e) if e.kind() == std::io::ErrorKind::NotFound => return Ok(()),
        Err(e) => return Err(e.to_string()),
    };
    if !meta.is_file() || meta.file_type().is_symlink() || meta.len() > MAX_WORKSPACE_BYTES as u64 {
        return Err("Invalid workspace authority file".into());
    }
    let mut bytes = Vec::new();
    File::open(path)
        .map_err(|e| e.to_string())?
        .take(MAX_WORKSPACE_BYTES as u64 + 1)
        .read_to_end(&mut bytes)
        .map_err(|e| e.to_string())?;
    let session = WorkspaceSession::from_archive_bytes(&bytes).map_err(|e| e.to_string())?;
    if owns(&session, area) {
        return Err(
            "This zone belongs to the workspace; legacy editing/recovery is read-only".into(),
        );
    }
    Ok(())
}

/// Undoing membership does not make the historical source writable again: redo
/// and retained audit still own its identity. No implicit reverse migration.
#[must_use]
pub fn owns(session: &WorkspaceSession, area: AreaId) -> bool {
    let has = |head: &WorkspaceHead| {
        head.projects
            .iter()
            .flat_map(|p| &p.modules)
            .flat_map(|m| &m.zones)
            .any(|z| z.area_id == area)
    };
    has(session.head())
        || session
            .audit()
            .iter()
            .any(|r| has(&r.before) || has(&r.after))
}
