//! Publish a flushed, same-directory stage without replacing an existing object.
//! Windows uses a no-replace rename, including on exFAT. Other platforms retain
//! the hard-link publication. Neither path claims power-loss directory durability.
use std::{io, path::Path};

/// Whether publication consumed the caller-owned staging file.
pub(super) fn publish(stage: &Path, destination: &Path) -> io::Result<bool> {
    if stage.parent().is_none() || stage.parent() != destination.parent() {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "Object publication requires a same-directory stage",
        ));
    }
    #[cfg(windows)]
    {
        windows::rename_no_replace(stage, destination)?;
        Ok(true)
    }
    #[cfg(not(windows))]
    {
        std::fs::hard_link(stage, destination)?;
        Ok(false)
    }
}

// Isolate the only FFI required for the no-replace Windows rename. std::fs::rename
// may replace the destination; hard links are unavailable on exFAT.
#[cfg(windows)]
#[allow(unsafe_code)]
mod windows {
    use std::{io, os::windows::ffi::OsStrExt, path::Path};

    #[link(name = "kernel32")]
    unsafe extern "system" {
        fn MoveFileExW(existing: *const u16, destination: *const u16, flags: u32) -> i32;
    }

    fn wide(path: &Path) -> io::Result<Vec<u16>> {
        let mut value: Vec<_> = path.as_os_str().encode_wide().collect();
        if value.contains(&0) {
            return Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                "NUL in publication path",
            ));
        }
        value.push(0);
        Ok(value)
    }

    pub(super) fn rename_no_replace(stage: &Path, destination: &Path) -> io::Result<()> {
        let stage = wide(stage)?;
        let destination = wide(destination)?;
        // SAFETY: both buffers are valid NUL-terminated UTF-16 strings, remain
        // alive throughout this synchronous call, and contain no interior NUL.
        // Flags=0 forbids replacement and cross-volume copy. The caller checked
        // same-directory paths, so a complete flushed stage is renamed in place.
        if unsafe { MoveFileExW(stage.as_ptr(), destination.as_ptr(), 0) } == 0 {
            return Err(io::Error::last_os_error());
        }
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::{
        fs,
        sync::atomic::{AtomicU64, Ordering},
    };
    static NEXT: AtomicU64 = AtomicU64::new(0);

    fn root() -> std::path::PathBuf {
        let base = std::env::var_os("FORGE_ATOMIC_PUBLISH_TEST_ROOT")
            .map_or_else(std::env::temp_dir, std::path::PathBuf::from);
        let root = base.join(format!(
            "forge-publication-{}-{}",
            std::process::id(),
            NEXT.fetch_add(1, Ordering::Relaxed)
        ));
        fs::create_dir(&root).unwrap();
        root
    }

    #[test]
    fn publishes_complete_stage() {
        let root = root();
        let stage = root.join("stage");
        let destination = root.join("object");
        let file = fs::File::create(&stage).unwrap();
        fs::write(&stage, b"complete reviewed bytes").unwrap();
        file.sync_all().unwrap();
        drop(file);
        let consumed = publish(&stage, &destination).unwrap();
        assert_eq!(fs::read(destination).unwrap(), b"complete reviewed bytes");
        assert_eq!(stage.exists(), !consumed);
    }

    #[test]
    fn existing_object_is_never_replaced() {
        let root = root();
        let stage = root.join("stage");
        let destination = root.join("object");
        fs::write(&stage, b"new bytes").unwrap();
        fs::write(&destination, b"existing bytes").unwrap();
        assert_eq!(
            publish(&stage, &destination).unwrap_err().kind(),
            io::ErrorKind::AlreadyExists
        );
        assert_eq!(fs::read(destination).unwrap(), b"existing bytes");
        assert_eq!(fs::read(stage).unwrap(), b"new bytes");
    }

    #[test]
    fn cross_directory_publication_is_rejected() {
        let root = root();
        let stage = root.join("stage");
        fs::write(&stage, b"complete bytes").unwrap();
        assert_eq!(
            publish(&stage, &root.join("other/object"))
                .unwrap_err()
                .kind(),
            io::ErrorKind::InvalidInput
        );
        assert_eq!(fs::read(stage).unwrap(), b"complete bytes");
    }

    #[cfg(windows)]
    #[test]
    fn nul_path_is_rejected_before_ffi() {
        assert_eq!(
            windows::rename_no_replace(Path::new("a\0b"), Path::new("c"))
                .unwrap_err()
                .kind(),
            io::ErrorKind::InvalidInput
        );
    }
}
