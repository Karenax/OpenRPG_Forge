use super::{AssetImportStore, ContentHash, GovernedAssetFile, Head, Path, PathBuf};
use sha2::{Digest, Sha256};
use std::{
    fs::{self, File, OpenOptions},
    io::{Read, Write},
    sync::atomic::{AtomicU64, Ordering},
};
static NEXT: AtomicU64 = AtomicU64::new(0);
pub(super) const MAX_DOCUMENT: usize = 16 * 1024 * 1024;

pub(super) fn no_links(path: &Path) -> Result<(), String> {
    for ancestor in path.ancestors() {
        match fs::symlink_metadata(ancestor) {
            Ok(metadata) => {
                if metadata.file_type().is_symlink() {
                    return Err(format!(
                        "Import path is a symbolic link: {}",
                        ancestor.display()
                    ));
                }
                #[cfg(windows)]
                {
                    use std::os::windows::fs::MetadataExt;
                    if metadata.file_attributes() & 0x400 != 0 {
                        return Err(format!(
                            "Import path is a reparse point: {}",
                            ancestor.display()
                        ));
                    }
                }
            }
            Err(error) if error.kind() == std::io::ErrorKind::NotFound => {}
            Err(error) => return Err(error.to_string()),
        }
    }
    Ok(())
}
impl AssetImportStore {
    pub(super) fn path(&self, relative: &str) -> Result<PathBuf, String> {
        if !relative.is_ascii()
            || relative.is_empty()
            || relative.contains('\\')
            || relative.contains(':')
            || relative
                .split('/')
                .any(|p| p.is_empty() || p == "." || p == "..")
        {
            return Err("Invalid internal import path".into());
        }
        let path = self.root.join(relative);
        no_links(&path)?;
        Ok(path)
    }
    pub(super) fn ensure_root(&self) -> Result<(), String> {
        no_links(&self.root)?;
        fs::create_dir_all(&self.root).map_err(|e| e.to_string())?;
        no_links(&self.root)
    }
    pub(super) fn lock(&self) -> Result<File, String> {
        self.ensure_root()?;
        let path = self.path(".import.lock")?;
        let lock = OpenOptions::new()
            .read(true)
            .write(true)
            .create(true)
            .truncate(false)
            .open(path)
            .map_err(|e| e.to_string())?;
        lock.try_lock()
            .map_err(|_| "Another asset import is running".to_string())?;
        Ok(lock)
    }
    pub(super) fn read_bytes(&self, relative: &str, limit: usize) -> Result<Vec<u8>, String> {
        let path = self.path(relative)?;
        let file = File::open(&path).map_err(|e| format!("{}: {e}", path.display()))?;
        let metadata = file.metadata().map_err(|e| e.to_string())?;
        if !metadata.is_file()
            || metadata.len() > u64::try_from(limit).map_err(|e| e.to_string())?
        {
            return Err(format!(
                "Import file absent, nonregular or oversized: {relative}"
            ));
        }
        let mut bytes = Vec::new();
        file.take(u64::try_from(limit).map_err(|e| e.to_string())? + 1)
            .read_to_end(&mut bytes)
            .map_err(|e| e.to_string())?;
        if bytes.len() > limit || u64::try_from(bytes.len()).ok() != Some(metadata.len()) {
            return Err(format!("Import file changed while reading: {relative}"));
        }
        Ok(bytes)
    }
    pub(super) fn verify_file(&self, pin: &GovernedAssetFile) -> Result<(), String> {
        let path = self.path(&pin.path)?;
        let mut file = File::open(&path).map_err(|e| format!("{}: {e}", pin.path))?;
        let metadata = file.metadata().map_err(|e| e.to_string())?;
        if !metadata.is_file() || metadata.len() != pin.bytes {
            return Err(format!("Imported file length differs: {}", pin.path));
        }
        let mut hash = Sha256::new();
        let mut count = 0_u64;
        let mut buffer = vec![0_u8; 65536].into_boxed_slice();
        loop {
            let n = file.read(&mut buffer).map_err(|e| e.to_string())?;
            if n == 0 {
                break;
            }
            count = count
                .checked_add(u64::try_from(n).map_err(|e| e.to_string())?)
                .ok_or("Imported file size overflow")?;
            if count > pin.bytes {
                return Err(format!("Imported file grew: {}", pin.path));
            }
            hash.update(&buffer[..n]);
        }
        if count != pin.bytes || ContentHash::from_bytes(hash.finalize().into()) != pin.sha256 {
            return Err(format!("Imported file SHA-256 differs: {}", pin.path));
        }
        Ok(())
    }
    pub(super) fn publish_bytes(&self, relative: &str, bytes: &[u8]) -> Result<(), String> {
        let path = self.path(relative)?;
        let parent = path.parent().ok_or("Import parent absent")?;
        fs::create_dir_all(parent).map_err(|e| e.to_string())?;
        no_links(parent)?;
        if path.exists() {
            if self.read_bytes(relative, bytes.len())? == bytes {
                return Ok(());
            }
            return Err(format!("Immutable imported file differs: {relative}"));
        }
        let stage = parent.join(format!(
            ".stage-{}-{}",
            std::process::id(),
            NEXT.fetch_add(1, Ordering::Relaxed)
        ));
        let mut owned_stage = false;
        let result = (|| {
            let mut file = OpenOptions::new()
                .write(true)
                .create_new(true)
                .open(&stage)
                .map_err(|e| format!("Create immutable import stage {relative}: {e}"))?;
            owned_stage = true;
            file.write_all(bytes)
                .and_then(|()| file.sync_all())
                .map_err(|e| format!("Flush immutable import stage {relative}: {e}"))?;
            drop(file);
            no_links(&path)?;
            // Every store writer holds .import.lock through this check and
            // rename. Existing immutable destinations are compared, never
            // intentionally replaced; hard-link support is not required.
            if path.exists() {
                if self.read_bytes(relative, bytes.len())? == bytes {
                    return Ok(());
                }
                return Err(format!("Concurrent immutable import conflict: {relative}"));
            }
            fs::rename(&stage, &path)
                .map_err(|e| format!("Publish immutable import {relative}: {e}"))
        })();
        if owned_stage {
            let _ = fs::remove_file(stage);
        }
        result
    }
    pub(super) fn write_head(&self, head: &Head) -> Result<(), String> {
        let bytes = serde_json::to_vec(head).map_err(|e| e.to_string())?;
        let destination = self.path("head.json")?;
        let stage = self.path(&format!(
            ".head-{}-{}",
            std::process::id(),
            NEXT.fetch_add(1, Ordering::Relaxed)
        ))?;
        let mut owned_stage = false;
        let result = (|| {
            let mut file = OpenOptions::new()
                .write(true)
                .create_new(true)
                .open(&stage)
                .map_err(|e| e.to_string())?;
            owned_stage = true;
            file.write_all(&bytes)
                .and_then(|()| file.sync_all())
                .map_err(|e| e.to_string())?;
            drop(file);
            fs::rename(&stage, &destination).map_err(|e| e.to_string())
        })();
        if owned_stage && result.is_err() {
            let _ = fs::remove_file(stage);
        }
        result
    }
}
