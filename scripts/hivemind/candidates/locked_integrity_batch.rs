//! Windows-only, explicitly scoped hash reuse with read handles denying writes
//! and deletion. Outside the scope the caller retains its ordinary fresh checks.
use sha2::{Digest, Sha256};
use std::{cell::RefCell, collections::HashMap, fs::{File, OpenOptions},
    io::Read, path::{Path, PathBuf}};

struct CheckedFile {
    _read_lock: File,
    expected: String,
    actual: String,
    length: u64,
}

#[derive(Default)]
struct Batch {
    files: HashMap<PathBuf, CheckedFile>,
    hits: u64,
    bytes_hashed: u64,
}

thread_local! {
    static ACTIVE: RefCell<Option<Batch>> = const { RefCell::new(None) };
}

struct Reset;
impl Drop for Reset {
    fn drop(&mut self) {
        ACTIVE.with(|active| active.borrow_mut().take());
    }
}

/// No persistent cache and no relaxation of SHA, bounds or declaration conflicts.
/// The OS handles remain alive across all accesses made by this synchronous action.
pub fn with_locked_integrity_batch<T>(action: impl FnOnce() -> Result<T, String>)
    -> Result<(T, serde_json::Value), String>
{
    #[cfg(not(windows))]
    return Err("Locked integrity batches require verified Windows sharing semantics".into());
    #[cfg(windows)]
    {
        ACTIVE.with(|active| {
            let mut active = active.borrow_mut();
            if active.is_some() { return Err("Nested integrity batch refused".to_owned()); }
            *active = Some(Batch::default());
            Ok(())
        })?;
        let _reset = Reset;
        let result = action()?;
        let stats = ACTIVE.with(|active| {
            let active = active.borrow();
            let batch = active.as_ref().expect("Active integrity batch");
            serde_json::json!({"unique_payloads_hashed":batch.files.len(),
                "repeated_full_hash_reads_avoided":batch.hits,
                "bytes_physically_hashed":batch.bytes_hashed,
                "write_and_delete_denied_until_batch_end":true,
                "persistent_cache":false,"full_import":false})
        });
        Ok((result, stats))
    }
}

/// None means there is no batch: use the unchanged ordinary hashing implementation.
pub fn scoped_file_hash(path: &Path, expected: &str, limit: u64)
    -> Option<Result<bool, String>>
{
    ACTIVE.with(|active| {
        let mut active = active.borrow_mut();
        let batch = active.as_mut()?;
        Some(check(batch, path, expected, limit))
    })
}

fn check(batch: &mut Batch, path: &Path, expected: &str, limit: u64)
    -> Result<bool, String>
{
    if expected.len() != 64 || !expected.bytes().all(|b| b.is_ascii_hexdigit()) {
        return Ok(false);
    }
    if let Some(file) = batch.files.get(path) {
        if !file.expected.eq_ignore_ascii_case(expected) {
            return Err("Conflicting SHA declarations for one batch file".into());
        }
        if file.length > limit { return Err("File exceeds this request's bound".into()); }
        batch.hits += 1;
        return Ok(file.actual.eq_ignore_ascii_case(expected));
    }
    let mut options = OpenOptions::new();
    options.read(true);
    #[cfg(windows)]
    {
        use std::os::windows::fs::OpenOptionsExt;
        // FILE_SHARE_READ only: an existing writer or delete handle also refuses
        // this lock. A metadata timestamp is never used as a substitute for SHA.
        options.share_mode(1);
    }
    let mut file = options.open(path).map_err(|e| format!("Read lock refused: {e}"))?;
    let metadata = file.metadata().map_err(|e| e.to_string())?;
    if !metadata.is_file() || metadata.len() > limit {
        return Err("File absent, nonregular or above request bound".into());
    }
    let mut hash = Sha256::new();
    let mut buffer = vec![0u8; 64 * 1024];
    let mut total = 0u64;
    loop {
        let read = file.read(&mut buffer).map_err(|e| e.to_string())?;
        if read == 0 { break; }
        total = total.checked_add(read as u64).ok_or("Byte count overflow")?;
        if total > limit { return Err("File grew beyond request bound".into()); }
        hash.update(&buffer[..read]);
    }
    if total != metadata.len() { return Err("File size changed during locked read".into()); }
    let actual = format!("{:x}", hash.finalize());
    batch.bytes_hashed = batch.bytes_hashed.checked_add(total).ok_or("Batch byte count overflow")?;
    batch.files.insert(path.to_path_buf(), CheckedFile {
        _read_lock: file, expected: expected.to_owned(), actual: actual.clone(), length: total });
    Ok(actual.eq_ignore_ascii_case(expected))
}

#[cfg(all(test, windows))]
mod tests {
    use super::*;
    use std::sync::atomic::{AtomicU64, Ordering};
    static SEQUENCE: AtomicU64 = AtomicU64::new(0);
    fn fixture() -> (PathBuf, String) {
        let root = std::env::temp_dir().join(format!("locked-hash-{}-{}", std::process::id(), SEQUENCE.fetch_add(1,Ordering::Relaxed)));
        std::fs::create_dir_all(&root).unwrap();
        let path = root.join("owned-control.bin");
        std::fs::write(&path,b"preserved-source").unwrap();
        (path,format!("{:x}",Sha256::digest(b"preserved-source")))
    }
    #[test]
    fn shared_reads_hash_once_and_deny_write_delete_until_scope_ends() {
        let (path, hash) = fixture();
        let (_, stats) = with_locked_integrity_batch(|| {
            assert!(scoped_file_hash(&path,&hash,64).unwrap()?);
            assert!(scoped_file_hash(&path,&hash,64).unwrap()?);
            assert!(OpenOptions::new().write(true).open(&path).is_err());
            assert!(std::fs::remove_file(&path).is_err());
            Ok(())
        }).unwrap();
        assert_eq!(stats["unique_payloads_hashed"],1);
        assert_eq!(stats["repeated_full_hash_reads_avoided"],1);
        assert!(OpenOptions::new().write(true).open(&path).is_ok());
        assert!(scoped_file_hash(&path,&hash,64).is_none());
    }
    #[test]
    fn stricter_limit_and_conflicting_declaration_refuse_even_after_a_hit() {
        let (path, hash) = fixture();
        with_locked_integrity_batch(|| {
            assert!(scoped_file_hash(&path,&hash,64).unwrap()?);
            assert!(scoped_file_hash(&path,&hash,1).unwrap().is_err());
            assert!(scoped_file_hash(&path,&"0".repeat(64),64).unwrap().is_err());
            Ok(())
        }).unwrap();
    }
    #[test]
    fn changed_file_is_rehashed_in_new_scope_and_fails_original_sha() {
        let (path, hash) = fixture();
        with_locked_integrity_batch(|| { assert!(scoped_file_hash(&path,&hash,64).unwrap()?); Ok(()) }).unwrap();
        std::fs::write(&path,b"modified-source!").unwrap();
        with_locked_integrity_batch(|| { assert!(!scoped_file_hash(&path,&hash,64).unwrap()?); Ok(()) }).unwrap();
    }
    #[test]
    fn action_failure_releases_every_lock_and_discards_cache() {
        let (path, hash) = fixture();
        let result: Result<((),serde_json::Value),String> = with_locked_integrity_batch(|| {
            assert!(scoped_file_hash(&path,&hash,64).unwrap()?);
            Err("controlled refusal".into())
        });
        assert!(result.is_err());
        assert!(OpenOptions::new().write(true).open(&path).is_ok());
        assert!(scoped_file_hash(&path,&hash,64).is_none());
    }
    #[test]
    fn existing_writer_and_nested_batch_refuse_without_resetting_outer_batch() {
        let (path, hash) = fixture();
        let writer = OpenOptions::new().write(true).open(&path).unwrap();
        with_locked_integrity_batch(|| {
            assert!(scoped_file_hash(&path,&hash,64).unwrap().is_err());
            assert!(with_locked_integrity_batch(||Ok(())).is_err());
            drop(writer);
            assert!(scoped_file_hash(&path,&hash,64).unwrap()?);
            Ok(())
        }).unwrap();
    }
}
