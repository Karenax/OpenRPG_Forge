//! Validated content-addressed zone archives. No workspace-head publication here.
//! Assumes an application-authorized root and cooperating local processes, like
//! `DocumentStore`; not a sandbox against hostile concurrent directory replacement.
use super::ZoneReference;
use crate::zone_authoring::{MAX_ZONE_HEAD_BYTES, session::ZoneEditSession};
use forge_core::{ContentHash, ProjectId, bytes_sha256};
use std::{
    fs::{self, File, OpenOptions},
    io::{Read, Write},
    path::{Path, PathBuf},
    sync::atomic::{AtomicU64, Ordering},
};

static NEXT_STAGE: AtomicU64 = AtomicU64::new(0);

/// Exact reviewed bytes; construction always replays the archive first.
pub struct ReviewedZone {
    bytes: Vec<u8>,
    reference: ZoneReference,
    project_id: ProjectId,
    sources: std::collections::BTreeMap<ContentHash, usize>,
}

impl ReviewedZone {
    /// # Errors
    /// Rejects oversized, altered or invalid archives, including failed replay.
    pub fn from_bytes(bytes: &[u8], expected: ContentHash) -> Result<Self, String> {
        if bytes.len() > MAX_ZONE_HEAD_BYTES {
            return Err("Zone archive exceeds 4 MiB".into());
        }
        if bytes_sha256(bytes) != expected {
            return Err("Zone archive differs from reviewed byte hash".into());
        }
        let session = ZoneEditSession::from_archive_bytes(bytes)?;
        let head = session.head();
        Ok(Self {
            reference: ZoneReference {
                area_id: head.base.terrain.area_id,
                archive_hash: expected,
                head_hash: head.content_hash()?,
            },
            project_id: head.base.terrain.project_id,
            sources: session.required_source_blobs()?,
            bytes: bytes.to_vec(),
        })
    }

    #[must_use]
    pub fn reference(&self) -> &ZoneReference {
        &self.reference
    }

    #[must_use]
    pub fn project_id(&self) -> ProjectId {
        self.project_id
    }

    #[must_use]
    pub fn required_source_blobs(&self) -> &std::collections::BTreeMap<ContentHash, usize> {
        &self.sources
    }
}

pub struct ZoneObjectStore {
    pub(super) root: PathBuf,
    // Cache replay results only. Every read still verifies physical bytes and sources.
    verified: std::sync::Mutex<std::collections::BTreeMap<ContentHash, VerifiedArchive>>,
}

#[derive(Clone)]
struct VerifiedArchive {
    reference: ZoneReference,
    project_id: ProjectId,
    sources: std::collections::BTreeMap<ContentHash, usize>,
}

impl ZoneObjectStore {
    /// `authority` is the reserved workspace-v1 directory, not a source zone.
    ///
    /// # Errors
    /// Rejects filesystem failures and a linked or non-directory object store.
    pub fn open(authority: &Path) -> Result<Self, String> {
        let root = authority.join("objects");
        match fs::create_dir(&root) {
            Ok(()) => (),
            Err(e) if e.kind() == std::io::ErrorKind::AlreadyExists => (),
            Err(e) => return Err(e.to_string()),
        }
        let metadata = fs::symlink_metadata(&root).map_err(|e| e.to_string())?;
        if !metadata.is_dir() || metadata.file_type().is_symlink() {
            return Err("Object directory must not be a link".into());
        }
        Ok(Self {
            root,
            verified: std::sync::Mutex::default(),
        })
    }

    /// Stage, flush, then publish atomically without replacing any
    /// existing object. A crash before publication cannot expose a partial object.
    ///
    /// # Errors
    /// Rejects invalid sources, incompatible existing objects and filesystem failures.
    pub fn publish(&self, reviewed: &ReviewedZone) -> Result<(), String> {
        self.verify_sources(reviewed)?;
        let final_path = self.path(reviewed.reference.archive_hash);
        if final_path.try_exists().map_err(|e| e.to_string())? {
            return self.verify(reviewed.project_id, &reviewed.reference);
        }
        let sequence = NEXT_STAGE.fetch_add(1, Ordering::Relaxed);
        let stage = self
            .root
            .join(format!(".pending-{}-{sequence}", std::process::id()));
        let mut file = OpenOptions::new()
            .write(true)
            .create_new(true)
            .open(&stage)
            .map_err(|e| e.to_string())?;
        let mut consumed = false;
        let result = (|| {
            file.write_all(&reviewed.bytes).map_err(|e| e.to_string())?;
            file.sync_all().map_err(|e| e.to_string())?;
            drop(file);
            match super::atomic_publish::publish(&stage, &final_path) {
                Ok(value) => consumed = value,
                Err(e) if e.kind() == std::io::ErrorKind::AlreadyExists => (),
                Err(e) => return Err(e.to_string()),
            }
            self.verify(reviewed.project_id, &reviewed.reference)
        })();
        // Only this call's successfully create_new-owned staging file is removed.
        let cleanup = if consumed {
            Ok(())
        } else {
            fs::remove_file(&stage).map_err(|e| e.to_string())
        };
        result.and(cleanup)
    }

    /// # Errors
    /// Rejects changed bytes, invalid replay or sources, incompatible identity and I/O errors.
    pub fn read(&self, project: ProjectId, reference: &ZoneReference) -> Result<Vec<u8>, String> {
        self.read_with_budget(project, reference, MAX_ZONE_HEAD_BYTES)
    }

    pub(super) fn read_with_budget(
        &self,
        project: ProjectId,
        reference: &ZoneReference,
        budget: usize,
    ) -> Result<Vec<u8>, String> {
        let limit = budget.min(MAX_ZONE_HEAD_BYTES);
        let path = self.path(reference.archive_hash);
        let metadata = fs::symlink_metadata(&path).map_err(|e| e.to_string())?;
        if !metadata.is_file()
            || metadata.file_type().is_symlink()
            || metadata.len() > MAX_ZONE_HEAD_BYTES as u64
        {
            return Err("Object must be a regular zone archive of at most 4 MiB".into());
        }
        if metadata.len() > limit as u64 {
            return Err("Zone object exceeds remaining archive verification budget".into());
        }
        let mut bytes = Vec::new();
        File::open(path)
            .map_err(|e| e.to_string())?
            .take(limit as u64 + 1)
            .read_to_end(&mut bytes)
            .map_err(|e| e.to_string())?;
        if bytes.len() > limit {
            return Err("Zone object exceeds remaining archive verification budget".into());
        }
        if bytes_sha256(&bytes) != reference.archive_hash {
            return Err("Zone archive differs from reviewed byte hash".into());
        }
        let cached = self
            .verified
            .lock()
            .map_err(|_| "Archive verification cache poisoned")?
            .get(&reference.archive_hash)
            .cloned();
        let reviewed = if let Some(cached) = cached {
            cached
        } else {
            let replayed = ReviewedZone::from_bytes(&bytes, reference.archive_hash)?;
            VerifiedArchive {
                reference: replayed.reference,
                project_id: replayed.project_id,
                sources: replayed.sources,
            }
        };
        if reviewed.project_id != project || reviewed.reference != *reference {
            return Err("Zone object has incompatible project, area or domain-head hash".into());
        }
        self.verify_source_map(&reviewed.sources)?;
        let mut cache = self
            .verified
            .lock()
            .map_err(|_| "Archive verification cache poisoned")?;
        // Bound derived memory independently of the workspace history size.
        if cache.len() >= 1024 {
            cache.clear();
        }
        cache.insert(reference.archive_hash, reviewed);
        Ok(bytes)
    }

    pub(super) fn verification_size(&self, reference: &ZoneReference) -> Result<usize, String> {
        let metadata = fs::symlink_metadata(self.path(reference.archive_hash))
            .map_err(|error| error.to_string())?;
        if !metadata.is_file()
            || metadata.file_type().is_symlink()
            || metadata.len() > MAX_ZONE_HEAD_BYTES as u64
        {
            return Err("Object must be a regular zone archive of at most 4 MiB".into());
        }
        usize::try_from(metadata.len()).map_err(|error| error.to_string())
    }

    /// # Errors
    /// Returns any archive, identity, source or filesystem error found by `read`.
    pub fn verify(&self, project: ProjectId, reference: &ZoneReference) -> Result<(), String> {
        self.read(project, reference).map(|_| ())
    }

    fn path(&self, hash: ContentHash) -> PathBuf {
        self.root.join(format!("{hash}.json"))
    }

    fn verify_sources(&self, reviewed: &ReviewedZone) -> Result<(), String> {
        self.verify_source_map(&reviewed.sources)
    }

    fn verify_source_map(
        &self,
        sources: &std::collections::BTreeMap<ContentHash, usize>,
    ) -> Result<(), String> {
        if sources.is_empty() {
            return Ok(());
        }
        let store = super::source_blobs::SourceBlobStore::open_existing(
            self.root.parent().ok_or("Missing workspace authority")?,
        )?;
        for (&hash, &bytes) in sources {
            store.read(hash, bytes)?;
        }
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn object_read_charges_actual_bytes_and_rejects_insufficient_budget() {
        let root = std::env::temp_dir().join(format!("forge-object-budget-{}", std::process::id()));
        fs::create_dir(&root).unwrap();
        let terrain = crate::authoring::new_terrain("budget", "area", "Budget").unwrap();
        let bytes = ZoneEditSession::from_initial_terrain(terrain)
            .unwrap()
            .archive_bytes()
            .unwrap();
        let reviewed = ReviewedZone::from_bytes(&bytes, bytes_sha256(&bytes)).unwrap();
        let store = ZoneObjectStore::open(&root).unwrap();
        store.publish(&reviewed).unwrap();
        for budget in [0, bytes.len() - 1] {
            assert!(
                store
                    .read_with_budget(reviewed.project_id(), reviewed.reference(), budget)
                    .is_err()
            );
        }
        assert_eq!(
            store
                .read_with_budget(reviewed.project_id(), reviewed.reference(), bytes.len())
                .unwrap(),
            bytes
        );
        assert_eq!(store.verified.lock().unwrap().len(), 1);
        // Warm entries must still reject a different domain reference or physical bytes.
        let mut wrong_reference = reviewed.reference().clone();
        wrong_reference.head_hash = ContentHash::from_bytes([0; 32]);
        assert!(store.read(reviewed.project_id(), &wrong_reference).is_err());
        let mut altered = bytes.clone();
        altered[0] ^= 1;
        fs::write(store.path(reviewed.reference().archive_hash), &altered).unwrap();
        assert!(
            store
                .read(reviewed.project_id(), reviewed.reference())
                .is_err()
        );
        fs::write(store.path(reviewed.reference().archive_hash), &bytes).unwrap();
        assert_eq!(
            store
                .read(reviewed.project_id(), reviewed.reference())
                .unwrap(),
            bytes
        );
        drop(store);
        fs::remove_dir_all(root).unwrap();
    }
}
