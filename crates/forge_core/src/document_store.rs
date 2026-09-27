//! Native byte-storage adapter. The application supplies an authorized directory.
//! Cooperating writers use a process-scoped OS lock; hashes detect stale saves.
//! Not a sandbox against hostile processes replacing directories concurrently.

use crate::ContentHash;
use sha2::{Digest, Sha256};
use std::fs::{self, File, OpenOptions};
use std::io::{self, Read, Write};
use std::path::{Path, PathBuf};
use thiserror::Error;

const MAX_BYTES: usize = 4 * 1024 * 1024;

#[derive(Debug, Error)]
pub enum StoreError {
    #[error("document storage I/O: {0}")]
    Io(#[from] io::Error),
    #[error("document is open for editing in another session")]
    Busy,
    #[error("stored document changed; reopen before saving")]
    Conflict,
    #[error("document exceeds 4 MiB")]
    TooLarge,
    #[error("storage entry must be a regular file, not a link or directory")]
    InvalidEntry,
}

pub struct StoredDocument {
    pub bytes: Vec<u8>,
    pub hash: ContentHash,
}

/// One directory per authoring document; filenames never come from the frontend.
pub struct DocumentStore {
    root: PathBuf,
    _lock: File,
}

impl DocumentStore {
    /// Directory must already exist and be authorized by the native application.
    pub fn open(directory: &Path) -> Result<Self, StoreError> {
        let root = directory.canonicalize()?;
        if !root.is_dir() {
            return Err(StoreError::InvalidEntry);
        }
        let lock_path = root.join("authoring.lock");
        check_entry(&lock_path)?;
        let lock = OpenOptions::new()
            .read(true)
            .write(true)
            .create(true)
            .truncate(false)
            .open(lock_path)?;
        match lock.try_lock() {
            Ok(()) => (),
            Err(std::fs::TryLockError::WouldBlock) => return Err(StoreError::Busy),
            Err(std::fs::TryLockError::Error(error)) => return Err(StoreError::Io(error)),
        }
        Ok(Self { root, _lock: lock })
    }

    pub fn load(&self) -> Result<Option<StoredDocument>, StoreError> {
        self.read_entry("authoring.json")
    }

    pub fn pending(&self) -> Result<Option<StoredDocument>, StoreError> {
        self.read_entry("authoring.pending")
    }

    /// Explicit recovery of the exact candidate inspected by the application.
    /// Both versions are archived before promotion or removal of staging.
    /// Domain validation of a promoted candidate belongs to the application.
    pub fn recover(
        &self,
        expected: Option<ContentHash>,
        pending_hash: ContentHash,
        promote: bool,
    ) -> Result<(), StoreError> {
        self.check_revision(expected)?;
        let pending = self.pending()?.ok_or(StoreError::Conflict)?;
        if pending.hash != pending_hash {
            return Err(StoreError::Conflict);
        }
        self.archive_recovery(&pending)?;
        if let Some(current) = self.load()? {
            self.archive_recovery(&current)?;
        }
        self.check_revision(expected)?;
        if self.pending()?.map(|value| value.hash) != Some(pending_hash) {
            return Err(StoreError::Conflict);
        }
        let path = self.root.join("authoring.pending");
        if promote {
            fs::rename(path, self.root.join("authoring.json"))?;
        } else {
            fs::remove_file(path)?;
        }
        Ok(())
    }

    fn archive_recovery(&self, stored: &StoredDocument) -> Result<(), StoreError> {
        let name = format!("recovery-{}.json", stored.hash);
        if let Some(existing) = self.read_entry(&name)? {
            return if existing.bytes == stored.bytes {
                Ok(())
            } else {
                Err(StoreError::Conflict)
            };
        }
        let mut file = OpenOptions::new()
            .write(true)
            .create_new(true)
            .open(self.root.join(name))?;
        file.write_all(&stored.bytes)?;
        file.sync_all()?;
        Ok(())
    }

    fn read_entry(&self, name: &str) -> Result<Option<StoredDocument>, StoreError> {
        let path = self.root.join(name);
        check_entry(&path)?;
        let file = match File::open(path) {
            Ok(file) => file,
            Err(error) if error.kind() == io::ErrorKind::NotFound => return Ok(None),
            Err(error) => return Err(error.into()),
        };
        let mut bytes = Vec::new();
        file.take((MAX_BYTES + 1) as u64).read_to_end(&mut bytes)?;
        if bytes.len() > MAX_BYTES {
            return Err(StoreError::TooLarge);
        }
        Ok(Some(StoredDocument {
            hash: digest(&bytes),
            bytes,
        }))
    }

    /// Same-directory replacement after flushing the complete staging file.
    /// A leftover staging file is preserved and blocks a save for explicit recovery.
    /// Does not claim directory durability after power loss on all filesystems.
    pub fn save(
        &self,
        expected: Option<ContentHash>,
        bytes: &[u8],
    ) -> Result<ContentHash, StoreError> {
        self.save_before_replace(expected, bytes, || Ok(()))
    }

    fn save_before_replace(
        &self,
        expected: Option<ContentHash>,
        bytes: &[u8],
        before_replace: impl FnOnce() -> io::Result<()>,
    ) -> Result<ContentHash, StoreError> {
        if bytes.len() > MAX_BYTES {
            return Err(StoreError::TooLarge);
        }
        self.check_revision(expected)?;
        let staging = self.root.join("authoring.pending");
        // create_new cannot overwrite a prior interrupted save or follow a link.
        let mut file = OpenOptions::new()
            .write(true)
            .create_new(true)
            .open(&staging)?;
        let result = (|| {
            file.write_all(bytes)?;
            file.sync_all()?;
            drop(file);
            before_replace()?;
            self.check_revision(expected)?;
            fs::rename(&staging, self.root.join("authoring.json"))?;
            Ok(digest(bytes))
        })();
        if result.is_err() {
            // Only our successfully created staging file; never delete an older one.
            let _ = fs::remove_file(&staging);
        }
        result
    }

    fn check_revision(&self, expected: Option<ContentHash>) -> Result<(), StoreError> {
        if self.load()?.map(|stored| stored.hash) != expected {
            return Err(StoreError::Conflict);
        }
        Ok(())
    }
}

fn digest(bytes: &[u8]) -> ContentHash {
    ContentHash::from_bytes(Sha256::digest(bytes).into())
}

fn check_entry(path: &Path) -> Result<(), StoreError> {
    match fs::symlink_metadata(path) {
        Ok(metadata) if !metadata.file_type().is_file() => Err(StoreError::InvalidEntry),
        Ok(_) => Ok(()),
        Err(error) if error.kind() == io::ErrorKind::NotFound => Ok(()),
        Err(error) => Err(error.into()),
    }
}

#[cfg(test)]
mod tests;
