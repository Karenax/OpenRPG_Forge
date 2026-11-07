//! Streaming, uncompressed transport: magic, bounded JSON descriptor, file bytes.
//! No archive-supplied shell, compression bomb, symlink or metadata extraction.
use crate::{
    Result, file_hash, hash, json,
    manifest::{self, Descriptor, Entry},
};
use std::{
    fs::{self, File, OpenOptions},
    io::{Read, Write},
    path::{Path, PathBuf},
};

const MAGIC: &[u8; 8] = b"FORGEPK1";

pub fn header(file: &mut File) -> Result<(Descriptor, String)> {
    let length = file.metadata().map_err(|e| e.to_string())?.len();
    header_reader(file, length)
}

pub(crate) fn header_reader(reader: &mut impl Read, length: u64) -> Result<(Descriptor, String)> {
    let mut magic = [0_u8; 8];
    reader.read_exact(&mut magic).map_err(|e| e.to_string())?;
    if &magic != MAGIC {
        return Err("Invalid Forge package signature".into());
    }
    let mut size = [0_u8; 8];
    reader.read_exact(&mut size).map_err(|e| e.to_string())?;
    let size = u64::from_le_bytes(size);
    if size == 0 || size > manifest::MAX_MANIFEST {
        return Err("Package descriptor exceeds limits".into());
    }
    let mut bytes = vec![0; usize::try_from(size).map_err(|e| e.to_string())?];
    reader.read_exact(&mut bytes).map_err(|e| e.to_string())?;
    let descriptor: Descriptor = serde_json::from_slice(&bytes).map_err(|e| e.to_string())?;
    descriptor.validate()?;
    if json(&descriptor)? != bytes {
        return Err("Package descriptor must use canonical serializer order".into());
    }
    let payload = descriptor.files.iter().try_fold(0_u64, |sum, f| {
        sum.checked_add(f.bytes).ok_or("Package size overflow")
    })?;
    if length != 16 + size + payload {
        return Err("Truncated package or trailing data".into());
    }
    Ok((descriptor, hash(&bytes)))
}

pub fn inspect(path: &Path) -> Result<(Descriptor, String)> {
    manifest::no_links(path)?;
    let result = header(&mut File::open(path).map_err(|e| e.to_string())?)?;
    if path
        .extension()
        .and_then(|s| s.to_str())
        .is_some_and(|s| s.eq_ignore_ascii_case("orpmod"))
        && (result.0.kind != manifest::Kind::Module
            || !result.0.files.iter().any(|f| f.path == "module.json"))
    {
        return Err("An .orpmod requires a semantic module manifest".into());
    }
    Ok(result)
}

// Reserve every ASCII case on all hosts, including case-insensitive filesystems.
fn storage_component(name: &std::ffi::OsStr) -> bool {
    name.to_str()
        .is_some_and(|name| name.eq_ignore_ascii_case(".forge-storage"))
}

fn contains_storage(path: &Path) -> bool {
    path.components().any(|component| {
        matches!(component, std::path::Component::Normal(name) if storage_component(name))
    })
}

fn inventory(root: &Path, folder: &Path, files: &mut Vec<Entry>) -> Result<()> {
    manifest::no_links(folder)?;
    for child in fs::read_dir(folder).map_err(|e| e.to_string())? {
        let child = child.map_err(|e| e.to_string())?;
        manifest::no_links(&child.path())?;
        if storage_component(&child.file_name()) {
            continue;
        }
        let metadata = child.metadata().map_err(|e| e.to_string())?;
        if metadata.is_dir() {
            inventory(root, &child.path(), files)?;
        } else if metadata.is_file() {
            if metadata.len() > manifest::MAX_FILE_BYTES {
                return Err("File exceeds package size limit".into());
            }
            if files.len() >= manifest::MAX_FILES {
                return Err("Too many package files".into());
            }
            let relative = child
                .path()
                .strip_prefix(root)
                .map_err(|e| e.to_string())?
                .to_str()
                .ok_or("Non-Unicode filename")?
                .replace('\\', "/");
            files.push(Entry {
                path: relative,
                bytes: crate::vault_storage::logical_size(
                    root,
                    &child.path(),
                    manifest::MAX_FILE_BYTES,
                )?,
                sha256: crate::vault_storage::file_hash(
                    root,
                    &child.path(),
                    manifest::MAX_FILE_BYTES,
                )?,
            });
        } else {
            return Err("Non-regular package file".into());
        }
    }
    Ok(())
}

pub fn pack(root: &Path, descriptor: Descriptor, output: &Path) -> Result<(Descriptor, String)> {
    pack_selected(root, descriptor, output, &[])
}

// PathBuf::push normalizes `..` on Windows verbatim paths; preserve the
// spelling until each traversed prefix has passed the link guard.
fn literal_join(base: &Path, tail: &Path) -> PathBuf {
    let mut path = base.as_os_str().to_os_string();
    let last = base.as_os_str().as_encoded_bytes().last().copied();
    let separated = if cfg!(windows) {
        matches!(last, Some(b'/' | b'\\'))
    } else {
        last == Some(b'/')
    };
    if !separated {
        path.push(std::path::MAIN_SEPARATOR_STR);
    }
    path.push(tail.as_os_str());
    PathBuf::from(path)
}

#[cfg(windows)]
fn ordinary_disk_base(base: PathBuf) -> PathBuf {
    use std::os::windows::ffi::{OsStrExt, OsStringExt};
    if matches!(
        base.components().next(),
        Some(std::path::Component::Prefix(prefix))
            if matches!(prefix.kind(), std::path::Prefix::VerbatimDisk(_))
    ) {
        let wide: Vec<_> = base.as_os_str().encode_wide().skip(4).collect();
        return PathBuf::from(std::ffi::OsString::from_wide(&wide));
    }
    base
}

// Anchor relative inputs without erasing `..` before their prefixes are checked.
fn anchored_source(root: &Path) -> Result<PathBuf> {
    if root.as_os_str().is_empty() {
        return Err("Package source path is empty".into());
    }
    if root.is_absolute() {
        return Ok(root.to_path_buf());
    }
    #[cfg(windows)]
    if let Some(std::path::Component::Prefix(prefix)) = root.components().next() {
        // Windows permits e.g. `E:folder`, relative to that drive's current
        // directory. Resolve only its anchor, never the unchecked suffix.
        if let std::path::Prefix::Disk(drive) = prefix.kind() {
            let anchor = format!("{}:.", char::from(drive));
            let anchor =
                ordinary_disk_base(std::path::absolute(&anchor).map_err(|e| e.to_string())?);
            let mut components = root.components();
            components.next();
            return Ok(literal_join(&anchor, components.as_path()));
        }
        return Err("Unsupported relative Windows source path".into());
    }
    let cwd = std::env::current_dir().map_err(|e| e.to_string())?;
    #[cfg(windows)]
    let cwd = ordinary_disk_base(cwd);
    #[cfg(windows)]
    if root.has_root() {
        if let Some(std::path::Component::Prefix(prefix)) = cwd.components().next()
            && let std::path::Prefix::Disk(drive) = prefix.kind()
        {
            let mut path = std::ffi::OsString::from(format!("{}:", char::from(drive)));
            path.push(root.as_os_str());
            return Ok(path.into());
        }
        return Err("Unsupported current Windows drive".into());
    }
    Ok(literal_join(&cwd, root))
}

fn checked_source(root: &Path) -> Result<PathBuf> {
    let source = anchored_source(root)?;
    let mut checked = PathBuf::new();
    for component in source.components() {
        match component {
            // A link traversed before `..` must not disappear from inspection.
            std::path::Component::ParentDir => {
                manifest::safe_chain(&checked)?;
                checked.pop();
            }
            std::path::Component::CurDir => {}
            _ => checked.push(component.as_os_str()),
        }
    }
    manifest::safe_chain(&checked)?;
    let source = fs::canonicalize(&source).map_err(|e| e.to_string())?;
    manifest::no_links(&source)?;
    Ok(source)
}

pub fn pack_selected(
    root: &Path,
    mut descriptor: Descriptor,
    output: &Path,
    selections: &[String],
) -> Result<(Descriptor, String)> {
    let root = checked_source(root)?;
    if contains_storage(&root) {
        return Err("Internal Vault storage is not a distributable source".into());
    }
    if output.is_relative() {
        return Err("Package output must be absolute".into());
    }
    let parent = fs::canonicalize(output.parent().ok_or("Output parent missing")?)
        .map_err(|e| e.to_string())?;
    if parent.starts_with(&root) {
        return Err("Package output cannot be inside its source".into());
    }
    descriptor.files.clear();
    if selections.is_empty() {
        inventory(&root, &root, &mut descriptor.files)?;
    } else {
        for selected in selections {
            let path = manifest::confined(&root, selected)?;
            if contains_storage(Path::new(selected)) {
                return Err("Internal Vault storage is not a distributable payload".into());
            }
            // Resolve a possible Windows short-name alias only after the existing
            // no-link confinement checks. A name may hide a reserved ancestor.
            let physical = fs::canonicalize(&path).map_err(|e| e.to_string())?;
            if contains_storage(&physical) {
                return Err("Internal Vault storage is not a distributable payload".into());
            }
            if path.is_dir() {
                inventory(&root, &path, &mut descriptor.files)?;
            } else {
                descriptor.files.push(Entry {
                    path: selected.clone(),
                    bytes: crate::vault_storage::logical_size(
                        &root,
                        &path,
                        manifest::MAX_FILE_BYTES,
                    )?,
                    sha256: crate::vault_storage::file_hash(
                        &root,
                        &path,
                        manifest::MAX_FILE_BYTES,
                    )?,
                });
            }
        }
    }
    descriptor.files.sort_by(|a, b| a.path.cmp(&b.path));
    descriptor.validate()?;
    write_package(&root, descriptor, output, &parent)
}

/// Transfer a verified installed package without changing its descriptor or rights.
pub(crate) fn export_installed(root: &Path, revision: &str, output: &Path) -> Result<()> {
    let descriptor = verify_directory(root, revision)?;
    let parent = output.parent().ok_or("Output parent missing")?;
    manifest::safe_chain(parent)?;
    if !output.is_absolute() || !crate::install::physically_disjoint(root, output)? {
        return Err("Installed package export requires a separate absolute directory".into());
    }
    write_package(root, descriptor, output, parent)?;
    Ok(())
}

/// Recheck a previously transferred package without extracting a second copy.
pub(crate) fn verify_payload(path: &Path, revision: &str) -> Result<Descriptor> {
    manifest::safe_chain(path)?;
    if !fs::metadata(path).map_err(|error| error.to_string())?.is_file() {
        return Err("Recovered package must be a regular file".into());
    }
    let mut input = File::open(path).map_err(|error| error.to_string())?;
    let (descriptor, actual_revision) = header(&mut input)?;
    if actual_revision != revision {
        return Err("Recovered package manifest differs from the installed revision".into());
    }
    let mut sink = std::io::sink();
    for entry in &descriptor.files {
        copy_verified(&mut input, &mut sink, entry)?;
    }
    Ok(descriptor)
}

fn write_package(
    root: &Path,
    descriptor: Descriptor,
    output: &Path,
    parent: &Path,
) -> Result<(Descriptor, String)> {
    let bytes = json(&descriptor)?;
    if u64::try_from(bytes.len()).map_err(|e| e.to_string())? > manifest::MAX_MANIFEST {
        return Err("Descriptor exceeds limit".into());
    }
    let stage = parent.join(format!(
        ".pack-{}-{}.tmp",
        std::process::id(),
        descriptor.id
    ));
    let mut file = OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(&stage)
        .map_err(|e| e.to_string())?;
    let result = (|| {
        file.write_all(MAGIC).map_err(|e| e.to_string())?;
        file.write_all(&(bytes.len() as u64).to_le_bytes())
            .map_err(|e| e.to_string())?;
        file.write_all(&bytes).map_err(|e| e.to_string())?;
        for entry in &descriptor.files {
            let input = manifest::confined(root, &entry.path)?;
            let count = crate::vault_storage::copy_verified(
                root,
                &input,
                entry.bytes,
                &entry.sha256,
                &mut file,
            )?;
            if count != entry.bytes {
                return Err("Logical export size changed".into());
            }
        }
        file.sync_all().map_err(|e| e.to_string())?;
        drop(file);
        if output.try_exists().map_err(|e| e.to_string())? {
            return Err("Refusing to overwrite a package".into());
        }
        fs::rename(&stage, output).map_err(|e| e.to_string())?;
        Ok((descriptor, hash(&bytes)))
    })();
    if result.is_err() {
        let _ = fs::remove_file(&stage);
    }
    result
}

fn copy_verified(input: &mut impl Read, output: &mut impl Write, entry: &Entry) -> Result<()> {
    use sha2::{Digest, Sha256};
    let mut digest = Sha256::new();
    let mut remaining = entry.bytes;
    let mut buffer = [0_u8; 65536];
    while remaining > 0 {
        let count =
            usize::try_from(remaining.min(buffer.len() as u64)).map_err(|e| e.to_string())?;
        input
            .read_exact(&mut buffer[..count])
            .map_err(|e| e.to_string())?;
        output
            .write_all(&buffer[..count])
            .map_err(|e| e.to_string())?;
        digest.update(&buffer[..count]);
        remaining -= count as u64;
    }
    if crate::hex(&digest.finalize()) != entry.sha256 {
        return Err(format!("File integrity mismatch: {}", entry.path));
    }
    Ok(())
}

pub fn extract(path: &Path, destination: &Path, expected_manifest: &str) -> Result<Descriptor> {
    manifest::no_links(path)?;
    let mut file = File::open(path).map_err(|e| e.to_string())?;
    let (descriptor, revision) = header(&mut file)?;
    if revision != expected_manifest {
        return Err("Package descriptor changed after plan".into());
    }
    fs::create_dir(destination).map_err(|e| e.to_string())?;
    for entry in &descriptor.files {
        let output = manifest::confined(destination, &entry.path)?;
        fs::create_dir_all(output.parent().ok_or("No parent")?).map_err(|e| e.to_string())?;
        let mut output = OpenOptions::new()
            .create_new(true)
            .write(true)
            .open(&output)
            .map_err(|e| e.to_string())?;
        copy_verified(&mut file, &mut output, entry)?;
        #[cfg(unix)]
        {
            use std::os::unix::fs::PermissionsExt;
            // The transport carries no Unix modes. Grant execution only to
            // declared app entries and the two validated Studio v3 kit tools.
            let executable = descriptor.requires_execution_permission(&entry.path);
            output
                .set_permissions(fs::Permissions::from_mode(if executable {
                    0o755
                } else {
                    0o644
                }))
                .map_err(|e| e.to_string())?;
        }
        output.sync_all().map_err(|e| e.to_string())?;
    }
    let mut manifest = OpenOptions::new()
        .create_new(true)
        .write(true)
        .open(destination.join("package-manifest.json"))
        .map_err(|e| e.to_string())?;
    manifest
        .write_all(&json(&descriptor)?)
        .map_err(|e| e.to_string())?;
    manifest.sync_all().map_err(|e| e.to_string())?;
    Ok(descriptor)
}

pub fn verify_directory(root: &Path, revision: &str) -> Result<Descriptor> {
    let descriptor: Descriptor =
        crate::read_json(&manifest::confined(root, "package-manifest.json")?)?;
    descriptor.validate()?;
    if hash(&json(&descriptor)?) != revision {
        return Err("Installed manifest integrity mismatch".into());
    }
    let expected: std::collections::BTreeSet<_> = descriptor
        .files
        .iter()
        .map(|f| f.path.as_str())
        .chain(std::iter::once("package-manifest.json"))
        .collect();
    audit_layout(root, root, &expected)?;
    for entry in &descriptor.files {
        let path = manifest::confined(root, &entry.path)?;
        if fs::metadata(&path).map_err(|e| e.to_string())?.len() != entry.bytes
            || file_hash(&path)? != entry.sha256
        {
            return Err(format!("Installed file integrity mismatch: {}", entry.path));
        }
        #[cfg(unix)]
        {
            use std::os::unix::fs::PermissionsExt;
            let mode = fs::metadata(&path)
                .map_err(|e| e.to_string())?
                .permissions()
                .mode();
            let executable = descriptor.requires_execution_permission(&entry.path);
            let expected = if executable { 0o111 } else { 0 };
            if mode & 0o7000 != 0 || mode & 0o111 != expected {
                return Err(format!(
                    "Installed file execution permissions mismatch: {}",
                    entry.path
                ));
            }
        }
    }
    crate::orpmod::verify_installed(root, &descriptor)?;
    Ok(descriptor)
}

fn audit_layout(
    root: &Path,
    folder: &Path,
    expected: &std::collections::BTreeSet<&str>,
) -> Result<()> {
    manifest::no_links(folder)?;
    for child in fs::read_dir(folder).map_err(|e| e.to_string())? {
        let child = child.map_err(|e| e.to_string())?;
        manifest::no_links(&child.path())?;
        let relative = child
            .path()
            .strip_prefix(root)
            .map_err(|e| e.to_string())?
            .to_str()
            .ok_or("Non-Unicode file")?
            .replace('\\', "/");
        if child.file_type().map_err(|e| e.to_string())?.is_dir() {
            let prefix = format!("{relative}/");
            let first = expected
                .range::<str, _>((
                    std::ops::Bound::Included(prefix.as_str()),
                    std::ops::Bound::Unbounded,
                ))
                .next();
            if !first.is_some_and(|name| name.starts_with(&prefix)) {
                return Err(format!("Unexpected installed directory: {relative}"));
            }
            audit_layout(root, &child.path(), expected)?;
        } else if !expected.contains(relative.as_str()) {
            return Err(format!("Unexpected installed file: {relative}"));
        }
    }
    Ok(())
}

#[cfg(test)]
mod source_path_tests {
    use super::*;

    #[test]
    fn literal_join_does_not_erase_a_parent_segment() {
        #[cfg(windows)]
        let base = Path::new(r"\\?\C:\fixture");
        #[cfg(not(windows))]
        let base = Path::new("/fixture");
        let relative = Path::new("source-link").join("..").join("target");
        let actual = literal_join(base, &relative);
        assert!(
            actual
                .components()
                .any(|part| part == std::path::Component::ParentDir)
        );
    }

    #[cfg(windows)]
    #[test]
    fn verbatim_current_directory_becomes_an_equivalent_disk_base() {
        let base = PathBuf::from(r"\\?\E:\épreuve\fixture");
        assert_eq!(
            ordinary_disk_base(base),
            PathBuf::from(r"E:\épreuve\fixture")
        );
    }
}
