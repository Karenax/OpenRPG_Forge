//! Streaming, uncompressed transport: magic, bounded JSON descriptor, file bytes.
//! No archive-supplied shell, compression bomb, symlink or metadata extraction.
use crate::{
    Result, file_hash, hash, json,
    manifest::{self, Descriptor, Entry},
};
use std::{
    fs::{self, File, OpenOptions},
    io::{Read, Write},
    path::Path,
};

const MAGIC: &[u8; 8] = b"FORGEPK1";

pub fn header(file: &mut File) -> Result<(Descriptor, String)> {
    let mut magic = [0_u8; 8];
    file.read_exact(&mut magic).map_err(|e| e.to_string())?;
    if &magic != MAGIC {
        return Err("Invalid Forge package signature".into());
    }
    let mut size = [0_u8; 8];
    file.read_exact(&mut size).map_err(|e| e.to_string())?;
    let size = u64::from_le_bytes(size);
    if size == 0 || size > manifest::MAX_MANIFEST {
        return Err("Package descriptor exceeds limits".into());
    }
    let mut bytes = vec![0; usize::try_from(size).map_err(|e| e.to_string())?];
    file.read_exact(&mut bytes).map_err(|e| e.to_string())?;
    let descriptor: Descriptor = serde_json::from_slice(&bytes).map_err(|e| e.to_string())?;
    descriptor.validate()?;
    if json(&descriptor)? != bytes {
        return Err("Package descriptor must use canonical serializer order".into());
    }
    let payload = descriptor.files.iter().try_fold(0_u64, |sum, f| {
        sum.checked_add(f.bytes).ok_or("Package size overflow")
    })?;
    if file.metadata().map_err(|e| e.to_string())?.len() != 16 + size + payload {
        return Err("Truncated package or trailing data".into());
    }
    Ok((descriptor, hash(&bytes)))
}

pub fn inspect(path: &Path) -> Result<(Descriptor, String)> {
    manifest::no_links(path)?;
    header(&mut File::open(path).map_err(|e| e.to_string())?)
}

fn inventory(root: &Path, folder: &Path, files: &mut Vec<Entry>) -> Result<()> {
    manifest::no_links(folder)?;
    for child in fs::read_dir(folder).map_err(|e| e.to_string())? {
        let child = child.map_err(|e| e.to_string())?;
        manifest::no_links(&child.path())?;
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
                bytes: metadata.len(),
                sha256: file_hash(&child.path())?,
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

pub fn pack_selected(
    root: &Path,
    mut descriptor: Descriptor,
    output: &Path,
    selections: &[String],
) -> Result<(Descriptor, String)> {
    let root = fs::canonicalize(root).map_err(|e| e.to_string())?;
    manifest::no_links(&root)?;
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
            if path.is_dir() {
                inventory(&root, &path, &mut descriptor.files)?;
            } else {
                descriptor.files.push(Entry {
                    path: selected.clone(),
                    bytes: fs::metadata(&path).map_err(|e| e.to_string())?.len(),
                    sha256: file_hash(&path)?,
                });
            }
        }
    }
    descriptor.files.sort_by(|a, b| a.path.cmp(&b.path));
    descriptor.validate()?;
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
            let input = manifest::confined(&root, &entry.path)?;
            let mut source = File::open(&input).map_err(|e| e.to_string())?;
            copy_verified(&mut source, &mut file, entry)?;
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
    }
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
