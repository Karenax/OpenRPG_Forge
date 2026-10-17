//! Metadata-only Windows path checks without opening every ancestor.
//!
//! SAFETY: All native calls and handle ownership are confined to this adapter.
//! Names are validated before conversion to NT syntax. Every FFI input/output
//! allocation has a stable address, including the IOSB and output-handle slot.
//! An unexpected pending operation retains its whole request and disables this
//! adapter for the process: returning an error must never free live kernel I/O.
//! This checks one path resolution; later I/O by name still has a TOCTOU window.
//! See ADR-0024 for the rationale, native prototype evidence and limitations.

use std::{
    cell::UnsafeCell,
    ffi::{OsString, c_void},
    fmt, fs, io,
    os::windows::{
        ffi::{OsStrExt, OsStringExt},
        fs::MetadataExt,
        io::{FromRawHandle, OwnedHandle, RawHandle},
    },
    path::{Path, PathBuf},
    ptr,
    sync::Mutex,
};

const STATUS_SUCCESS: i32 = 0;
const STATUS_PENDING: i32 = 0x103;
const STATUS_OBJECT_NAME_NOT_FOUND: i32 = 0xc0000034_u32 as i32;
const STATUS_OBJECT_PATH_NOT_FOUND: i32 = 0xc000003a_u32 as i32;
const FILE_ATTRIBUTE_REPARSE_POINT: u32 = 0x400;
const MAX_NT_UNITS: usize = u16::MAX as usize / 2;
const NT_PREFIX: &[u16] = &[92, 63, 63, 92]; // \??\
const VERBATIM_PREFIX: &[u16] = &[92, 92, 63, 92]; // \\?\

// Serialize native opens so an unexpected pending result can retain at most one
// bounded request. true means restart is required; no later native open is tried.
static DISABLED_AFTER_PENDING: Mutex<bool> = Mutex::new(false);

type GuardResult<T> = std::result::Result<T, GuardError>;

#[derive(Debug)]
enum GuardError {
    InvalidPath(&'static str),
    AbsoluteCheck(io::Error),
    Native(i32),
    InvalidHandle,
    Metadata(io::Error),
    ReparsePoint,
    NotDirectory,
    MissingRoot,
    PendingRetained,
    DisabledAfterPending,
    StatePoisoned,
}

impl fmt::Display for GuardError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::InvalidPath(reason) => write!(f, "Unsupported literal Windows path: {reason}"),
            Self::AbsoluteCheck(error) => write!(f, "Check exact Windows path: {error}"),
            Self::Native(status) => write!(f, "NtCreateFile no-reparse status 0x{:08x}", *status as u32),
            Self::InvalidHandle => f.write_str("NtCreateFile succeeded without a valid file handle"),
            Self::Metadata(error) => write!(f, "Inspect opened Windows handle: {error}"),
            Self::ReparsePoint => f.write_str("Reparse points are forbidden"),
            Self::NotDirectory => f.write_str("Existing Windows path prefix is not a directory"),
            Self::MissingRoot => f.write_str("No existing Windows path prefix could be verified"),
            Self::PendingRetained => f.write_str(
                "Unexpected STATUS_PENDING: native request retained; Windows path guard disabled until process restart",
            ),
            Self::DisabledAfterPending => f.write_str(
                "Windows path guard disabled after STATUS_PENDING; process restart required",
            ),
            Self::StatePoisoned => f.write_str("Windows path guard state is unavailable"),
        }
    }
}

struct LiteralDiskPath {
    dos: PathBuf,
    nt: Vec<u16>,
}

impl LiteralDiskPath {
    fn new(path: &Path) -> GuardResult<Self> {
        // Bound the copy before checking the UNICODE_STRING byte lengths.
        let mut wide: Vec<u16> = path
            .as_os_str()
            .encode_wide()
            .take(MAX_NT_UNITS + 1)
            .collect();
        let verbatim = wide.starts_with(VERBATIM_PREFIX);
        if !verbatim {
            // Win32 converts '/' to '\\' for ordinary Disk paths, including
            // portable suffixes appended by PathBuf::push. Never do this for a
            // verbatim input, whose slash has different native semantics.
            for unit in &mut wide {
                if *unit == 47 {
                    *unit = 92;
                }
            }
        }
        // Use the ORIGINAL prefix kind: separator conversion must not promote
        // unsupported spellings such as //?/C:/file into a verbatim namespace.
        let dos = if verbatim {
            &wide[VERBATIM_PREFIX.len()..]
        } else {
            &wide
        };
        if dos.len() < 3
            || !((65..=90).contains(&dos[0]) || (97..=122).contains(&dos[0]))
            || dos[1] != 58
            || dos[2] != 92
        {
            return Err(GuardError::InvalidPath(
                "only absolute Disk/VerbatimDisk paths are supported; UNC and device namespaces are forbidden",
            ));
        }
        if dos.len() + NT_PREFIX.len() + 1 > MAX_NT_UNITS {
            return Err(GuardError::InvalidPath("NT Unicode path is too long"));
        }
        if dos.contains(&0) || dos.contains(&47) {
            return Err(GuardError::InvalidPath(
                "NUL and verbatim slash separators are forbidden",
            ));
        }
        // A drive root is valid; every component below it must be literal.
        if dos.len() > 3 {
            for component in dos[3..].split(|unit| *unit == 92) {
                validate_component(component)?;
            }
        }
        let dos_path = PathBuf::from(OsString::from_wide(dos));
        // Like Rust 1.98's is_absolute_exact: do not rely only on a blacklist of
        // DOS aliases. After ordinary-Disk separator conversion, reject any
        // additional spelling change. Compare raw UTF-16 rather than Path
        // equality (which itself normalizes components).
        let absolute = std::path::absolute(&dos_path).map_err(GuardError::AbsoluteCheck)?;
        if !absolute.as_os_str().encode_wide().eq(dos.iter().copied()) {
            return Err(GuardError::InvalidPath(
                "Win32 would change the path spelling",
            ));
        }
        let mut nt = Vec::with_capacity(NT_PREFIX.len() + dos.len() + 1);
        nt.extend_from_slice(NT_PREFIX);
        nt.extend_from_slice(dos);
        nt.push(0);
        Ok(Self { dos: dos_path, nt })
    }
}

fn ascii_name(units: &[u16], name: &[u8]) -> bool {
    units.len() == name.len()
        && units.iter().zip(name).all(|(&unit, &letter)| {
            let upper = if (97..=122).contains(&unit) {
                unit - 32
            } else {
                unit
            };
            upper == u16::from(letter)
        })
}

fn validate_component(component: &[u16]) -> GuardResult<()> {
    if component.is_empty()
        || component
            .last()
            .is_some_and(|unit| matches!(*unit, 32 | 46))
        || component
            .iter()
            .any(|unit| *unit < 32 || [34, 42, 47, 58, 60, 62, 63, 124].contains(unit))
    {
        return Err(GuardError::InvalidPath(
            "empty/dot components, ADS, reserved characters and trailing points/spaces are forbidden",
        ));
    }
    let stem = component
        .split(|unit| *unit == 46)
        .next()
        .unwrap_or_default();
    let stem_end = stem
        .iter()
        .rposition(|unit| !matches!(*unit, 32 | 46))
        .map_or(0, |n| n + 1);
    let stem = &stem[..stem_end];
    let device = [
        b"CON".as_slice(),
        b"PRN",
        b"AUX",
        b"NUL",
        b"CLOCK$",
        b"CONIN$",
        b"CONOUT$",
    ]
    .iter()
    .any(|name| ascii_name(stem, name))
        || (stem.len() == 4
            && (ascii_name(&stem[..3], b"COM") || ascii_name(&stem[..3], b"LPT"))
            && matches!(stem[3], 49..=57 | 0xb9 | 0xb2 | 0xb3));
    if device {
        return Err(GuardError::InvalidPath("DOS device aliases are forbidden"));
    }
    Ok(())
}

#[repr(C)]
struct UnicodeString {
    length: u16,
    maximum_length: u16,
    buffer: *mut u16,
}

#[repr(C)]
struct ObjectAttributes {
    length: u32,
    root_directory: RawHandle,
    object_name: *mut UnicodeString,
    attributes: u32,
    security_descriptor: *mut c_void,
    security_quality_of_service: *mut c_void,
}

#[repr(C)]
union IoStatus {
    status: i32,
    pointer: *mut c_void,
}

#[repr(C)]
struct IoStatusBlock {
    status: IoStatus,
    information: usize,
}

#[link(name = "ntdll")]
unsafe extern "system" {
    fn NtCreateFile(
        file_handle: *mut RawHandle,
        desired_access: u32,
        object_attributes: *mut ObjectAttributes,
        io_status_block: *mut IoStatusBlock,
        allocation_size: *const i64,
        file_attributes: u32,
        share_access: u32,
        create_disposition: u32,
        create_options: u32,
        ea_buffer: *const c_void,
        ea_length: u32,
    ) -> i32;
}

struct NativeRequest {
    // Fixed Vec buffers, not Box pointees: Vec::as_mut_ptr does not materialize
    // element references (Rust 1.98 alloc/src/vec/mod.rs). Moving these owners
    // therefore does not reassert Box's uniqueness over a derived FFI pointer.
    // Once wired, never resize a buffer, index its elements, or borrow its slice.
    // Access pointees only through as_mut_ptr/raw_get until native completion.
    _name: Vec<u16>,
    _unicode: Vec<UnicodeString>,
    attributes: Vec<ObjectAttributes>,
    io: Vec<UnsafeCell<IoStatusBlock>>,
    handle: Vec<UnsafeCell<RawHandle>>,
    #[cfg(test)]
    dropped: Option<std::sync::Arc<std::sync::atomic::AtomicUsize>>,
}

impl NativeRequest {
    fn new(path: &LiteralDiskPath) -> GuardResult<Self> {
        let mut name = path.nt.clone();
        let maximum_length = u16::try_from(name.len() * 2)
            .map_err(|_| GuardError::InvalidPath("NT Unicode path is too long"))?;
        let mut unicode = vec![UnicodeString {
            length: maximum_length - 2,
            maximum_length,
            buffer: name.as_mut_ptr(),
        }];
        let attributes = vec![ObjectAttributes {
            length: std::mem::size_of::<ObjectAttributes>() as u32,
            root_directory: ptr::null_mut(),
            object_name: unicode.as_mut_ptr(),
            attributes: 0x1000 | 0x40, // OBJ_DONT_REPARSE | OBJ_CASE_INSENSITIVE.
            security_descriptor: ptr::null_mut(),
            security_quality_of_service: ptr::null_mut(),
        }];
        Ok(Self {
            _name: name,
            _unicode: unicode,
            attributes,
            io: vec![UnsafeCell::new(IoStatusBlock {
                status: IoStatus {
                    pointer: ptr::null_mut(),
                },
                information: 0,
            })],
            handle: vec![UnsafeCell::new(ptr::null_mut())],
            #[cfg(test)]
            dropped: None,
        })
    }

    fn open(&mut self) -> i32 {
        // SAFETY: repr(C) layouts follow winternl.h. Name/US/OA/IOSB/output-handle
        // allocations remain stable through the call, and the caller retains all
        // of them forever on STATUS_PENDING. FILE_OPEN never creates or replaces.
        // No input requests data, ACL changes, impersonation or backup privileges.
        unsafe {
            NtCreateFile(
                UnsafeCell::raw_get(self.handle.as_mut_ptr()),
                0x80 | 0x100000, // FILE_READ_ATTRIBUTES | SYNCHRONIZE.
                self.attributes.as_mut_ptr(),
                UnsafeCell::raw_get(self.io.as_mut_ptr()),
                ptr::null(),
                0,
                1 | 2 | 4, // FILE_SHARE_READ | WRITE | DELETE: metadata, not immutability.
                1,         // FILE_OPEN.
                0x20,      // FILE_SYNCHRONOUS_IO_NONALERT, no type or leaf-reparse flag.
                ptr::null(),
                0,
            )
        }
    }

    fn take_completed_handle(&mut self) -> Option<OwnedHandle> {
        // SAFETY: Called only after STATUS_SUCCESS, when NtCreateFile guarantees
        // the returned file handle. Never inspect an unspecified failure output.
        // raw_get/as_mut_ptr form no references to the output element.
        let raw = unsafe { UnsafeCell::raw_get(self.handle.as_mut_ptr()).replace(ptr::null_mut()) };
        if raw.is_null() || raw == -1_isize as RawHandle {
            None
        } else {
            // SAFETY: NtCreateFile returned this owned file handle. Taking it out
            // of the output slot transfers its sole CloseHandle owner to RAII.
            Some(unsafe { OwnedHandle::from_raw_handle(raw) })
        }
    }
}

#[cfg(test)]
impl Drop for NativeRequest {
    fn drop(&mut self) {
        if let Some(counter) = &self.dropped {
            counter.fetch_add(1, std::sync::atomic::Ordering::SeqCst);
        }
    }
}

fn open_with(
    path: &LiteralDiskPath,
    disabled: &Mutex<bool>,
    invoke: impl FnOnce(&mut NativeRequest) -> i32,
) -> GuardResult<Option<fs::File>> {
    let mut disabled = disabled.lock().map_err(|_| GuardError::StatePoisoned)?;
    if *disabled {
        return Err(GuardError::DisabledAfterPending);
    }
    let mut request = NativeRequest::new(path)?;
    let status = invoke(&mut request);
    if status == STATUS_PENDING {
        // SAFETY: The kernel might still write the IOSB/handle slot. Keep every
        // pointee alive, do not read IOSB, close, cancel, retry or wait speculatively.
        // This lock and sticky disabled flag bound retention to one request per
        // process (under 64 KiB of name plus fixed structs/handle). Restart releases
        // the address space safely. This anomaly is always a visible failure.
        *disabled = true;
        std::mem::forget(request);
        return Err(GuardError::PendingRetained);
    }
    match status {
        STATUS_SUCCESS => request
            .take_completed_handle()
            .map(fs::File::from)
            .map(Some)
            .ok_or(GuardError::InvalidHandle),
        STATUS_OBJECT_NAME_NOT_FOUND | STATUS_OBJECT_PATH_NOT_FOUND => Ok(None),
        // Do not consult IOSB here: it may be zero even on a reparse/access error.
        // No Rtl/Win32 error mapping can broaden the two allowed missing statuses.
        _ => Err(GuardError::Native(status)),
    }
}

#[derive(Clone, Copy)]
enum Presence {
    Existing { is_dir: bool },
    Missing,
}

fn probe(path: &LiteralDiskPath) -> GuardResult<Presence> {
    let Some(file) = open_with(path, &DISABLED_AFTER_PENDING, NativeRequest::open)? else {
        return Ok(Presence::Missing);
    };
    // File::from(OwnedHandle) and File::metadata preserve the same handle; neither
    // reopens a name. Dropping this metadata-only handle provides no identity lock
    // for later readers, which retain their own existing sharing contracts.
    let metadata = file.metadata().map_err(GuardError::Metadata)?;
    if metadata.file_attributes() & FILE_ATTRIBUTE_REPARSE_POINT != 0 {
        return Err(GuardError::ReparsePoint);
    }
    Ok(Presence::Existing {
        is_dir: metadata.is_dir(),
    })
}

fn check_chain(
    path: &Path,
    mut inspect: impl FnMut(&LiteralDiskPath) -> GuardResult<Presence>,
) -> GuardResult<()> {
    let mut candidate = LiteralDiskPath::new(path)?;
    let mut missing_tail = false;
    loop {
        match inspect(&candidate)? {
            Presence::Existing { is_dir: false } if missing_tail => {
                return Err(GuardError::NotDirectory);
            }
            Presence::Existing { .. } => return Ok(()),
            Presence::Missing => {
                // A file is valid as the original target, never as the existing
                // prefix of a destination whose remaining components are absent.
                missing_tail = true;
                let parent = candidate.dos.parent().ok_or(GuardError::MissingRoot)?;
                candidate = LiteralDiskPath::new(parent)?;
            }
        }
    }
}

pub(super) fn safe_chain(path: &Path) -> crate::Result<()> {
    check_chain(path, probe)
        .map_err(|error| format!("Inspect Windows package path {}: {error}", path.display()))
}

#[cfg(test)]
mod tests;
