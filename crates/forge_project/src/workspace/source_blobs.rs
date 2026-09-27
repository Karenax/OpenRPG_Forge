//! Immutable binary sources beneath the authorized workspace authority.
//! Like the zone object store, assumes cooperating local processes; not a sandbox.
use forge_core::{ContentHash, bytes_sha256};
use std::{
    fs::{self, File, OpenOptions},
    io::{Read, Write},
    path::{Path, PathBuf},
    sync::atomic::{AtomicU64, Ordering},
};

pub const MAX_SOURCE_BLOB_BYTES: usize = 64 * 1024 * 1024;
static NEXT_STAGE: AtomicU64 = AtomicU64::new(0);

pub struct SourceBlobStore {
    root: PathBuf,
}

impl SourceBlobStore {
    pub fn open(authority: &Path) -> Result<Self, String> {
        let root = authority.join("source-blobs");
        match fs::create_dir(&root) {
            Ok(()) => (),
            Err(e) if e.kind() == std::io::ErrorKind::AlreadyExists => (),
            Err(e) => return Err(e.to_string()),
        }
        Self::open_existing(authority)
    }

    /// Read-only open: never creates a directory in a reviewed source zone.
    pub fn open_existing(authority: &Path) -> Result<Self, String> {
        let root = authority.join("source-blobs");
        let metadata = fs::symlink_metadata(&root).map_err(|e| e.to_string())?;
        if !metadata.is_dir() || metadata.file_type().is_symlink() {
            return Err("Source blob directory must not be a link".into());
        }
        Ok(Self { root })
    }

    /// Verify, stage and flush; atomically publish without replacing any object.
    pub fn publish(&self, hash: ContentHash, bytes: &[u8]) -> Result<(), String> {
        if bytes.is_empty() || bytes.len() > MAX_SOURCE_BLOB_BYTES || bytes_sha256(bytes) != hash {
            return Err("Invalid source blob size or hash".into());
        }
        let destination = self.path(hash);
        if destination.try_exists().map_err(|e| e.to_string())? {
            return self.read(hash, bytes.len()).map(|_| ());
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
        let result = (|| {
            file.write_all(bytes).map_err(|e| e.to_string())?;
            file.sync_all().map_err(|e| e.to_string())?;
            drop(file);
            match fs::hard_link(&stage, &destination) {
                Ok(()) => (),
                Err(e) if e.kind() == std::io::ErrorKind::AlreadyExists => (),
                Err(e) => return Err(e.to_string()),
            }
            self.read(hash, bytes.len()).map(|_| ())
        })();
        let cleanup = fs::remove_file(&stage).map_err(|e| e.to_string());
        result.and(cleanup)
    }

    pub fn read(&self, hash: ContentHash, expected_bytes: usize) -> Result<Vec<u8>, String> {
        if expected_bytes == 0 || expected_bytes > MAX_SOURCE_BLOB_BYTES {
            return Err("Invalid source blob budget".into());
        }
        let path = self.path(hash);
        let metadata = fs::symlink_metadata(&path).map_err(|e| e.to_string())?;
        if !metadata.is_file()
            || metadata.file_type().is_symlink()
            || metadata.len() != expected_bytes as u64
        {
            return Err("Source blob must be a regular file of the expected size".into());
        }
        let mut bytes = Vec::new();
        File::open(path)
            .map_err(|e| e.to_string())?
            .take(expected_bytes as u64 + 1)
            .read_to_end(&mut bytes)
            .map_err(|e| e.to_string())?;
        if bytes.len() != expected_bytes || bytes_sha256(&bytes) != hash {
            return Err("Source blob length or hash mismatch".into());
        }
        Ok(bytes)
    }

    fn path(&self, hash: ContentHash) -> PathBuf {
        self.root.join(format!("{hash}.bin"))
    }
}
