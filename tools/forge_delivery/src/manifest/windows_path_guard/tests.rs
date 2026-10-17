use super::*;
use std::{
    mem::{offset_of, size_of},
    os::windows::fs::{OpenOptionsExt, symlink_dir, symlink_file},
    sync::{
        Arc,
        atomic::{AtomicUsize, Ordering},
    },
};

#[test]
fn native_layouts_match_windows_pointer_width() {
    if size_of::<usize>() == 8 {
        assert_eq!(size_of::<UnicodeString>(), 16);
        assert_eq!(offset_of!(UnicodeString, buffer), 8);
        assert_eq!(size_of::<ObjectAttributes>(), 48);
        assert_eq!(offset_of!(ObjectAttributes, object_name), 16);
        assert_eq!(offset_of!(ObjectAttributes, attributes), 24);
        assert_eq!(size_of::<IoStatusBlock>(), 16);
        assert_eq!(offset_of!(IoStatusBlock, information), 8);
    } else {
        assert_eq!(size_of::<UnicodeString>(), 8);
        assert_eq!(size_of::<ObjectAttributes>(), 24);
        assert_eq!(size_of::<IoStatusBlock>(), 8);
    }
}

#[test]
fn literal_paths_keep_unicode_and_match_the_verbatim_disk_spelling() {
    let disk = LiteralDiskPath::new(Path::new(r"C:\épreuve-Îlot\journal.bin")).unwrap();
    let verbatim = LiteralDiskPath::new(Path::new(r"\\?\C:\épreuve-Îlot\journal.bin")).unwrap();
    assert_eq!(disk.nt, verbatim.nt);
    assert_eq!(
        disk.nt,
        "\\??\\C:\\épreuve-Îlot\\journal.bin\0"
            .encode_utf16()
            .collect::<Vec<_>>()
    );
    assert!(LiteralDiskPath::new(Path::new(r"C:\")).is_ok());
}

#[test]
fn disk_slashes_and_portable_pushes_keep_the_same_unicode_nt_target() {
    let expected = LiteralDiskPath::new(Path::new(
        r"C:\épreuve-Îlot\source\_forge-export\assets.json",
    ))
    .unwrap();
    for spelling in [
        r"C:/épreuve-Îlot/source/_forge-export/assets.json",
        r"C:\épreuve-Îlot/source\_forge-export/assets.json",
    ] {
        let literal = LiteralDiskPath::new(Path::new(spelling)).unwrap();
        assert_eq!(literal.nt, expected.nt);
        assert!(
            literal
                .dos
                .as_os_str()
                .encode_wide()
                .eq(expected.dos.as_os_str().encode_wide())
        );
    }
    let mut disk = PathBuf::from(r"C:\épreuve-Îlot\source");
    disk.push("_forge-export/assets.json");
    assert!(disk.as_os_str().encode_wide().any(|unit| unit == 47));
    assert_eq!(LiteralDiskPath::new(&disk).unwrap().nt, expected.nt);

    // Rust 1.98's verbatim push reconstructs an ordinary suffix's components
    // with MAIN_SEPARATOR_STR. This already-normalized input remains supported.
    let mut verbatim = PathBuf::from(r"\\?\C:\épreuve-Îlot\source");
    verbatim.push("_forge-export/assets.json");
    assert!(!verbatim.as_os_str().encode_wide().any(|unit| unit == 47));
    assert_eq!(LiteralDiskPath::new(&verbatim).unwrap().nt, expected.nt);
}

#[test]
fn literal_paths_preserve_unpaired_utf16_in_valid_components() {
    let mut units: Vec<u16> = r"C:\private\".encode_utf16().collect();
    units.extend([0xd800, b'.' as u16, b'x' as u16]);
    let path = PathBuf::from(OsString::from_wide(&units));
    let literal = LiteralDiskPath::new(&path).unwrap();
    assert_eq!(&literal.nt[NT_PREFIX.len()..literal.nt.len() - 1], units);
}

#[test]
fn alias_spellings_are_refused_before_any_native_open() {
    for spelling in [
        r"C:relative",
        r"\rooted",
        r"relative",
        r"C:\a\\b",
        r"C:\a\",
        r"C:\a\.\b",
        r"C:\a\..\b",
        r"C:/a/../b",
        r"C:\junction.\missing",
        r"C:\junction \missing",
        r"C:/junction./missing",
        r"C:\junction /missing",
        r"C:\a:stream",
        r"C:\NUL.txt",
        r"C:\NUL .txt",
        r"C:\conout$",
        r"C:\COM¹.txt",
        r"C:\LPT².bin",
        r"C:\COM³.bin",
        r"C:\aux\file",
        r"C:\a?b",
        r"C:\a*b",
        "C:\\a\0b",
        r"\\server\share\file",
        r"\\?\UNC\server\share\file",
        r"\\.\C:\file",
        r"\\?\GLOBALROOT\Device\HarddiskVolume1\file",
        r"\\?\C:\junction.\missing",
        r"\\?\C:\NUL .txt",
        r"\\?\C:\a/b",
        r"//?/C:/file",
    ] {
        let mut called = false;
        let result = check_chain(Path::new(spelling), |_| {
            called = true;
            Ok(Presence::Existing { is_dir: false })
        });
        assert!(result.is_err(), "{spelling:?}");
        assert!(!called, "native open must not see {spelling:?}");
    }
}

#[test]
fn native_unicode_length_is_bounded_before_open() {
    let path = PathBuf::from(format!("C:\\{}", "a".repeat(MAX_NT_UNITS)));
    assert!(matches!(
        LiteralDiskPath::new(&path),
        Err(GuardError::InvalidPath(_))
    ));
}

#[test]
fn missing_tail_stops_at_the_first_existing_private_prefix() {
    let mut seen = Vec::new();
    check_chain(Path::new(r"C:\private\absent\nested\file"), |path| {
        seen.push(path.dos.clone());
        Ok(if path.dos == Path::new(r"C:\private") {
            Presence::Existing { is_dir: true }
        } else {
            Presence::Missing
        })
    })
    .unwrap();
    assert_eq!(
        seen,
        [
            r"C:\private\absent\nested\file",
            r"C:\private\absent\nested",
            r"C:\private\absent",
            r"C:\private"
        ]
        .map(PathBuf::from)
    );
}

#[test]
fn existing_file_does_not_probe_its_inaccessible_ancestors() {
    let mut seen = Vec::new();
    check_chain(Path::new(r"C:\Packages\private\file"), |path| {
        seen.push(path.dos.clone());
        if seen.len() > 1 {
            return Err(GuardError::Native(0xc0000022_u32 as i32));
        }
        Ok(Presence::Existing { is_dir: false })
    })
    .unwrap();
    assert_eq!(seen, [PathBuf::from(r"C:\Packages\private\file")]);
}

#[test]
fn an_existing_file_cannot_be_the_prefix_of_a_missing_destination() {
    for suffix in ["missing.bin", "absent\\nested\\missing.bin"] {
        let source = Path::new(r"C:\private\source.bin");
        let mut seen = Vec::new();
        let result = check_chain(&source.join(suffix), |path| {
            seen.push(path.dos.clone());
            Ok(if path.dos == source {
                Presence::Existing { is_dir: false }
            } else {
                Presence::Missing
            })
        });
        assert!(matches!(result, Err(GuardError::NotDirectory)));
        assert_eq!(seen.last().map(PathBuf::as_path), Some(source));
        assert!(!seen.contains(&PathBuf::from(r"C:\private")));
    }
}

#[test]
fn access_reparse_sharing_and_unrecognised_errors_never_probe_a_parent() {
    for status in [0xc0000022_u32, 0xc000050b, 0xc0000043, 0xc000000f] {
        let mut calls = 0;
        let error = check_chain(Path::new(r"C:\private\file"), |_| {
            calls += 1;
            Err(GuardError::Native(status as i32))
        });
        assert!(matches!(error, Err(GuardError::Native(observed)) if observed == status as i32));
        assert_eq!(calls, 1);
    }
}

#[test]
fn metadata_and_reparse_attribute_failures_never_probe_a_parent() {
    for error in [
        GuardError::Metadata(io::Error::new(
            io::ErrorKind::PermissionDenied,
            "handle metadata denied",
        )),
        GuardError::ReparsePoint,
    ] {
        let mut error = Some(error);
        let mut calls = 0;
        let result = check_chain(Path::new(r"C:\private\file"), |_| {
            calls += 1;
            Err(error.take().unwrap())
        });
        assert!(matches!(
            result,
            Err(GuardError::Metadata(_) | GuardError::ReparsePoint)
        ));
        assert_eq!(calls, 1);
    }
}

#[test]
fn no_verified_existing_prefix_is_an_error() {
    assert!(matches!(
        check_chain(Path::new(r"C:\missing"), |_| Ok(Presence::Missing)),
        Err(GuardError::MissingRoot)
    ));
}

#[test]
fn direct_native_status_is_authoritative_even_when_iosb_is_zero() {
    let literal = LiteralDiskPath::new(Path::new(r"C:\private\file")).unwrap();
    let state = Mutex::new(false);
    for status in [STATUS_OBJECT_NAME_NOT_FOUND, STATUS_OBJECT_PATH_NOT_FOUND] {
        assert!(open_with(&literal, &state, |_| status).unwrap().is_none());
    }
    for status in [
        0xc0000022_u32 as i32,
        0xc000050b_u32 as i32,
        0xc0000043_u32 as i32,
    ] {
        assert!(
            matches!(open_with(&literal, &state, |_| status), Err(GuardError::Native(observed)) if observed == status)
        );
    }
    assert!(matches!(
        open_with(&literal, &state, |_| STATUS_SUCCESS),
        Err(GuardError::InvalidHandle)
    ));
}

#[test]
fn pending_retains_the_request_and_disables_further_native_opens() {
    let literal = LiteralDiskPath::new(Path::new(r"C:\private\file")).unwrap();
    let state = Mutex::new(false);
    let dropped = Arc::new(AtomicUsize::new(0));
    let calls = AtomicUsize::new(0);
    let mut pointers = None;
    let first = open_with(&literal, &state, |request| {
        calls.fetch_add(1, Ordering::SeqCst);
        request.dropped = Some(Arc::clone(&dropped));
        pointers = Some((
            request._name.as_mut_ptr(),
            request._unicode.as_mut_ptr(),
            request.attributes.as_mut_ptr(),
            UnsafeCell::raw_get(request.io.as_mut_ptr()),
            UnsafeCell::raw_get(request.handle.as_mut_ptr()),
        ));
        STATUS_PENDING
    });
    assert!(matches!(first, Err(GuardError::PendingRetained)));
    assert_eq!(dropped.load(Ordering::SeqCst), 0);
    let (name, unicode, attributes, iosb, handle) = pointers.unwrap();
    // SAFETY: This injected callback never submitted kernel I/O. Its fixed Vec
    // buffers have been retained despite owner moves; these raw pointers may be
    // inspected here without a kernel writer. Production never reads a pending
    // IOSB/handle. This tests Rust ownership, not actual pending-I/O completion.
    unsafe {
        assert_eq!((*unicode).buffer, name);
        assert_eq!((*attributes).object_name, unicode);
        assert_eq!(
            std::slice::from_raw_parts(name, literal.nt.len()),
            literal.nt
        );
        assert_eq!((*iosb).information, 0);
        assert!((*handle).is_null());
    }
    let second = open_with(&literal, &state, |_| {
        calls.fetch_add(1, Ordering::SeqCst);
        STATUS_SUCCESS
    });
    assert!(matches!(second, Err(GuardError::DisabledAfterPending)));
    assert_eq!(calls.load(Ordering::SeqCst), 1);
    assert_eq!(dropped.load(Ordering::SeqCst), 0);
}

struct Fixture(PathBuf, PathBuf);

impl Fixture {
    fn new() -> Self {
        let parent = fs::canonicalize(std::env::temp_dir()).unwrap();
        let root = parent.join(format!(
            "forge-windows-path-guard-{}",
            crate::attempt().unwrap()
        ));
        fs::create_dir(&root).unwrap();
        Self(fs::canonicalize(root).unwrap(), parent)
    }

    fn path(&self, relative: &str) -> PathBuf {
        self.0.join(relative)
    }
}

impl Drop for Fixture {
    fn drop(&mut self) {
        // Verify this generated absolute root remains inside its owned parent.
        // Tests normally remove links explicitly; after an assertion failure,
        // std::remove_dir_all removes link entries without following targets.
        // Never change an ancestor ACL or recursively remove an unverified root.
        let owned_name = self.0.file_name().is_some_and(|name| {
            name.to_string_lossy()
                .starts_with("forge-windows-path-guard-")
        });
        let plain_root = fs::symlink_metadata(&self.0)
            .is_ok_and(|metadata| metadata.file_attributes() & FILE_ATTRIBUTE_REPARSE_POINT == 0);
        if self.0.is_absolute()
            && self.0.parent() == Some(self.1.as_path())
            && owned_name
            && plain_root
        {
            let _ = fs::remove_dir_all(&self.0);
        }
    }
}

#[test]
fn real_file_directory_and_missing_tail_checks_preserve_source_bytes() {
    let fixture = Fixture::new();
    let source = fixture.path("épreuve-Îlot.bin");
    fs::write(&source, b"original source bytes").unwrap();
    safe_chain(&source).unwrap();
    let source_wide: Vec<u16> = source.as_os_str().encode_wide().collect();
    let mut ordinary = source_wide
        .strip_prefix(VERBATIM_PREFIX)
        .unwrap_or(&source_wide)
        .to_vec();
    for unit in &mut ordinary {
        if *unit == 92 {
            *unit = 47;
        }
    }
    let ordinary = PathBuf::from(OsString::from_wide(&ordinary));
    safe_chain(&ordinary).unwrap();
    assert_eq!(fs::read(&ordinary).unwrap(), b"original source bytes");
    safe_chain(&fixture.0).unwrap();
    safe_chain(&fixture.path("absent\\nested\\file")).unwrap();
    assert!(safe_chain(&source.join("missing.bin")).is_err());
    assert!(!fixture.path("absent").exists());
    assert_eq!(fs::read(&source).unwrap(), b"original source bytes");
}

#[test]
fn metadata_uses_the_original_handle_after_rename_and_allows_existing_writers() {
    let fixture = Fixture::new();
    let source = fixture.path("source.bin");
    fs::write(&source, b"original").unwrap();
    let writer = fs::OpenOptions::new()
        .write(true)
        .share_mode(7)
        .open(&source)
        .unwrap();
    let literal = LiteralDiskPath::new(&source).unwrap();
    let file = open_with(&literal, &DISABLED_AFTER_PENDING, NativeRequest::open)
        .unwrap()
        .unwrap();
    let before = file.metadata().unwrap();
    fs::rename(&source, fixture.path("renamed.bin")).unwrap();
    fs::write(&source, b"replacement with different length").unwrap();
    let after = file.metadata().unwrap();
    assert_eq!(after.len(), before.len());
    assert_ne!(after.len(), fs::metadata(&source).unwrap().len());
    assert_eq!(fs::read(fixture.path("renamed.bin")).unwrap(), b"original");
    drop(file);
    drop(writer);
}

#[link(name = "kernel32")]
unsafe extern "system" {
    fn DeviceIoControl(
        handle: RawHandle,
        code: u32,
        input: *const c_void,
        input_len: u32,
        output: *mut c_void,
        output_len: u32,
        returned: *mut u32,
        overlapped: *mut c_void,
    ) -> i32;
}

fn junction(link: &Path, target: &Path) {
    use std::os::windows::io::AsRawHandle;
    let literal = LiteralDiskPath::new(target).unwrap();
    let substitute = &literal.nt[..literal.nt.len() - 1];
    let print: Vec<u16> = literal.dos.as_os_str().encode_wide().collect();
    let data_length = u16::try_from(8 + (substitute.len() + print.len() + 2) * 2).unwrap();
    let mut buffer = Vec::new();
    buffer.extend_from_slice(&0xa0000003_u32.to_le_bytes()); // IO_REPARSE_TAG_MOUNT_POINT.
    for value in [
        data_length,
        0,
        0,
        u16::try_from(substitute.len() * 2).unwrap(),
        u16::try_from((substitute.len() + 1) * 2).unwrap(),
        u16::try_from(print.len() * 2).unwrap(),
    ] {
        buffer.extend_from_slice(&value.to_le_bytes());
    }
    for unit in substitute
        .iter()
        .copied()
        .chain([0])
        .chain(print)
        .chain([0])
    {
        buffer.extend_from_slice(&unit.to_le_bytes());
    }
    fs::create_dir(link).unwrap();
    let file = fs::OpenOptions::new()
        .access_mode(0x40000000)
        .custom_flags(0x02200000)
        .open(link)
        .unwrap();
    let mut returned = 0;
    // SAFETY: The fixture directory handle is valid and exclusively owned by this
    // test. Buffer is a bounded REPARSE_DATA_BUFFER mount-point layout. This is a
    // synchronous call (no OVERLAPPED), and every pointee lives until it returns.
    let result = unsafe {
        DeviceIoControl(
            file.as_raw_handle(),
            0x000900a4,
            buffer.as_ptr().cast(),
            u32::try_from(buffer.len()).unwrap(),
            ptr::null_mut(),
            0,
            &mut returned,
            ptr::null_mut(),
        )
    };
    assert_ne!(
        result,
        0,
        "create owned junction: {}",
        io::Error::last_os_error()
    );
}

#[test]
fn real_junctions_are_refused_even_when_the_target_or_final_tail_is_absent() {
    let fixture = Fixture::new();
    let target = fixture.path("target");
    fs::create_dir(&target).unwrap();
    fs::write(target.join("source.bin"), b"unchanged").unwrap();
    let link = fixture.path("junction");
    let dangling = fixture.path("dangling");
    junction(&link, &target);
    junction(&dangling, &fixture.path("missing-target"));
    for path in [
        &link,
        &link.join("source.bin"),
        &link.join("missing.bin"),
        &dangling,
    ] {
        assert!(
            safe_chain(path).unwrap_err().contains("0xc000050b"),
            "{path:?}"
        );
    }
    // Use an ordinary Disk base: joining to the canonical verbatim fixture root
    // would itself normalize the portable suffix before this guard sees it.
    let ordinary_root = LiteralDiskPath::new(&fixture.0).unwrap().dos;
    let mixed = ordinary_root.join("junction/missing.bin");
    assert!(mixed.as_os_str().encode_wide().any(|unit| unit == 47));
    assert!(safe_chain(&mixed).unwrap_err().contains("0xc000050b"));
    for alias in [
        "junction.\\missing.bin",
        "junction \\missing.bin",
        "junction./missing.bin",
        "junction /missing.bin",
    ] {
        assert!(
            safe_chain(&ordinary_root.join(alias))
                .unwrap_err()
                .contains("Unsupported literal Windows path"),
            "{alias:?}"
        );
    }
    assert_eq!(fs::read(target.join("source.bin")).unwrap(), b"unchanged");
    fs::remove_dir(&link).unwrap();
    fs::remove_dir(&dangling).unwrap();
}

#[test]
#[ignore = "requires Windows Developer Mode or symbolic-link privilege; junction regression is unconditional"]
fn real_symbolic_links_and_dangling_links_are_refused() {
    let fixture = Fixture::new();
    let target = fixture.path("target");
    fs::create_dir(&target).unwrap();
    fs::write(target.join("source.bin"), b"unchanged").unwrap();
    let directory = fixture.path("directory-link");
    let file = fixture.path("file-link");
    let dangling = fixture.path("dangling-file-link");
    symlink_dir(&target, &directory).unwrap();
    symlink_file(target.join("source.bin"), &file).unwrap();
    symlink_file(fixture.path("missing.bin"), &dangling).unwrap();
    for path in [&directory, &directory.join("missing.bin"), &file, &dangling] {
        assert!(safe_chain(path).is_err(), "{path:?}");
    }
    fs::remove_file(&file).unwrap();
    fs::remove_file(&dangling).unwrap();
    fs::remove_dir(&directory).unwrap();
}
