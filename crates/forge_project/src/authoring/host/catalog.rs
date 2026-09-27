//! Bounded advisory discovery. No session lock, save, or active-document replacement.
use crate::authoring::{SavedZone, UnavailableZone, UnavailableZoneReason as Reason, ZoneCatalog};
use forge_core::AreaId;
use std::io::Read;
use std::path::Path;

const ARCHIVE_LIMIT: usize = 4 * 1024 * 1024;
const CATALOG_BYTES_LIMIT: usize = 16 * 1024 * 1024;

pub(super) fn list(root: &Path) -> Result<ZoneCatalog, String> {
    let mut catalog = ZoneCatalog::default();
    let mut bytes_read = 0;
    for (index, entry) in std::fs::read_dir(root)
        .map_err(|e| e.to_string())?
        .enumerate()
    {
        if index >= 256 {
            return Err("Catalogue limité à 256 entrées du répertoire autorisé".into());
        }
        let entry = entry.map_err(|e| e.to_string())?;
        let Some(id) = entry
            .file_name()
            .to_str()
            .and_then(|name| name.parse::<AreaId>().ok())
        else {
            continue;
        };
        match read_zone(root, &entry.path(), id, &mut bytes_read) {
            Ok(zone) => catalog.zones.push(zone),
            Err(reason) => catalog.unavailable.push(UnavailableZone {
                area_id: id,
                reason,
            }),
        }
        if bytes_read > CATALOG_BYTES_LIMIT {
            return Err("Catalogue limité à 16 Mio d'archives ; réduire le workspace".into());
        }
    }
    catalog.zones.sort_by(|a, b| {
        a.name
            .cmp(&b.name)
            .then_with(|| a.area_id.to_string().cmp(&b.area_id.to_string()))
    });
    catalog
        .unavailable
        .sort_by_key(|zone| zone.area_id.to_string());
    Ok(catalog)
}

fn read_zone(
    root: &Path,
    directory: &Path,
    id: AreaId,
    bytes_read: &mut usize,
) -> Result<SavedZone, Reason> {
    let directory = directory.canonicalize().map_err(|_| Reason::Unreadable)?;
    if directory.parent() != Some(root) {
        return Err(Reason::UnsafePath);
    }
    let pending_recovery = match std::fs::symlink_metadata(directory.join("authoring.pending")) {
        Ok(_) => true,
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => false,
        Err(_) => return Err(Reason::Unreadable),
    };
    let path = directory.join("authoring.json");
    let metadata = std::fs::symlink_metadata(&path).map_err(|error| {
        if error.kind() == std::io::ErrorKind::NotFound {
            if pending_recovery {
                Reason::InterruptedSave
            } else {
                Reason::MissingArchive
            }
        } else {
            Reason::Unreadable
        }
    })?;
    if !metadata.is_file() || metadata.file_type().is_symlink() {
        return Err(Reason::UnsafePath);
    }
    let path = path.canonicalize().map_err(|_| Reason::Unreadable)?;
    if path.parent() != Some(directory.as_path()) {
        return Err(Reason::UnsafePath);
    }
    let file = std::fs::File::open(path).map_err(|_| Reason::Unreadable)?;
    let mut bytes = Vec::new();
    file.take((ARCHIVE_LIMIT + 1) as u64)
        .read_to_end(&mut bytes)
        .map_err(|_| Reason::Unreadable)?;
    *bytes_read += bytes.len();
    if bytes.len() > ARCHIVE_LIMIT || *bytes_read > CATALOG_BYTES_LIMIT {
        return Err(Reason::TooLarge);
    }
    let (document, saved_hash) =
        super::session::archive_summary(&bytes).map_err(|_| Reason::InvalidArchive)?;
    if document.area_id != id {
        return Err(Reason::IdentityMismatch);
    }
    Ok(SavedZone {
        area_id: id,
        project_id: document.project_id,
        name: document.name.clone(),
        saved_hash,
        pending_recovery,
    })
}
