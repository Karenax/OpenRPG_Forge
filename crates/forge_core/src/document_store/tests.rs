use super::*;
use std::sync::atomic::{AtomicU64, Ordering};

static NEXT: AtomicU64 = AtomicU64::new(0);

#[test]
fn interrupted_child() {
    let Some(root) = std::env::var_os("FORGE_STORE_CRASH_FIXTURE") else {
        return;
    };
    let store = DocumentStore::open(Path::new(&root)).unwrap();
    let hash = store.load().unwrap().unwrap().hash;
    let _ = store.save_before_replace(Some(hash), b"candidate", || std::process::exit(77));
    panic!("interruption hook was not reached");
}

#[test]
fn process_exit_preserves_versions_and_releases_os_lock() {
    let root = directory();
    let store = DocumentStore::open(&root).unwrap();
    let saved = store.save(None, b"saved").unwrap();
    drop(store);
    let status = std::process::Command::new(std::env::current_exe().unwrap())
        .args([
            "--exact",
            "document_store::tests::interrupted_child",
            "--nocapture",
        ])
        .env("FORGE_STORE_CRASH_FIXTURE", &root)
        .stdout(std::process::Stdio::null())
        .stderr(std::process::Stdio::null())
        .status()
        .unwrap();
    assert_eq!(status.code(), Some(77));
    let store = DocumentStore::open(&root).unwrap();
    assert_eq!(store.load().unwrap().unwrap().bytes, b"saved");
    let pending = store.pending().unwrap().unwrap();
    assert_eq!(pending.bytes, b"candidate");
    assert!(matches!(
        store.recover(Some(saved), saved, true),
        Err(StoreError::Conflict)
    ));
    store.recover(Some(saved), pending.hash, true).unwrap();
    assert_eq!(store.load().unwrap().unwrap().bytes, b"candidate");
    assert!(store.pending().unwrap().is_none());
    assert_eq!(
        fs::read(root.join(format!("recovery-{saved}.json"))).unwrap(),
        b"saved"
    );
    assert_eq!(
        fs::read(root.join(format!("recovery-{}.json", pending.hash))).unwrap(),
        b"candidate"
    );
    store.save(Some(pending.hash), b"after recovery").unwrap();
}

#[test]
fn keep_saved_archives_truncated_candidate_and_allows_next_save() {
    let root = directory();
    let store = DocumentStore::open(&root).unwrap();
    let saved = store.save(None, b"saved").unwrap();
    fs::write(root.join("authoring.pending"), b"{").unwrap();
    let pending = store.pending().unwrap().unwrap();
    store.recover(Some(saved), pending.hash, false).unwrap();
    assert_eq!(store.load().unwrap().unwrap().bytes, b"saved");
    assert_eq!(
        fs::read(root.join(format!("recovery-{}.json", pending.hash))).unwrap(),
        b"{"
    );
    store.save(Some(saved), b"next").unwrap();
}

fn directory() -> PathBuf {
    let path = std::env::temp_dir().join(format!(
        "forge-store-{}-{}",
        std::process::id(),
        NEXT.fetch_add(1, Ordering::Relaxed)
    ));
    fs::create_dir(&path).unwrap();
    path
}

// Each test retains its tiny, uniquely created fixture for evidence; no recursive cleanup.
#[test]
fn replacement_reopen_conflict_and_lock_release() {
    let root = directory();
    let store = DocumentStore::open(&root).unwrap();
    assert!(store.load().unwrap().is_none());
    assert!(matches!(DocumentStore::open(&root), Err(StoreError::Busy)));
    let first = store.save(None, b"first").unwrap();
    assert!(matches!(
        store.save(None, b"stale"),
        Err(StoreError::Conflict)
    ));
    let second = store.save(Some(first), b"second").unwrap();
    drop(store);
    let reopened = DocumentStore::open(&root).unwrap();
    let stored = reopened.load().unwrap().unwrap();
    assert_eq!(stored.bytes, b"second");
    assert_eq!(stored.hash, second);
}

#[test]
fn failure_before_replace_and_external_edit_preserve_committed_bytes() {
    let root = directory();
    let store = DocumentStore::open(&root).unwrap();
    let first = store.save(None, b"first").unwrap();
    assert!(
        store
            .save_before_replace(Some(first), b"new", || Err(io::Error::other(
                "injected interruption"
            )))
            .is_err()
    );
    assert_eq!(store.load().unwrap().unwrap().bytes, b"first");
    assert!(!root.join("authoring.pending").exists());
    assert!(matches!(
        store.save_before_replace(Some(first), b"new", || fs::write(
            root.join("authoring.json"),
            b"external"
        )),
        Err(StoreError::Conflict)
    ));
    assert_eq!(store.load().unwrap().unwrap().bytes, b"external");
}

#[test]
fn interrupted_staging_and_invalid_entries_are_not_overwritten() {
    let root = directory();
    let store = DocumentStore::open(&root).unwrap();
    let first = store.save(None, b"first").unwrap();
    fs::write(root.join("authoring.pending"), b"interrupted").unwrap();
    assert!(store.save(Some(first), b"new").is_err());
    assert_eq!(
        fs::read(root.join("authoring.pending")).unwrap(),
        b"interrupted"
    );
    assert_eq!(store.load().unwrap().unwrap().bytes, b"first");
    assert!(matches!(
        store.save(Some(first), &vec![0; MAX_BYTES + 1]),
        Err(StoreError::TooLarge)
    ));
    let other = directory();
    fs::create_dir(other.join("authoring.json")).unwrap();
    assert!(matches!(
        DocumentStore::open(&other).unwrap().load(),
        Err(StoreError::InvalidEntry)
    ));
}
