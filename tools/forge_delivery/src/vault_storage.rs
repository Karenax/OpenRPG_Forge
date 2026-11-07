//! Lossless physical storage adapter for a private Vault; logical hashes stay unchanged.
//! Packages exported from the Vault must contain reconstructed, autonomous assets.
use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};
use std::{
    collections::HashMap,
    fs::{self, File},
    io::{Read, Seek, SeekFrom, Write},
    path::{Path, PathBuf},
};

pub const MAGIC: &[u8; 8] = b"ORPVLT1\n";
pub const MAX_DESCRIPTOR: u64 = 2 * 1024 * 1024;
const MAX_BLOCK: u64 = 16 * 1024 * 1024;
const MAX_SEGMENTS: usize = 16_384;

#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct Descriptor {
    pub schema: u32,
    pub vault_id: String,
    pub bytes: u64,
    pub sha256: String,
    pub segments: Vec<Segment>,
}

#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct Segment {
    pub pack: String,
    pub offset: u64,
    pub stored_bytes: u64,
    pub bytes: u64,
    pub sha256: String,
    pub codec: String,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Root {
    schema: u32,
    vault_id: String,
}

fn hex(value: &str, length: usize) -> bool {
    value.len() == length
        && value
            .bytes()
            .all(|c| c.is_ascii_digit() || (b'a'..=b'f').contains(&c))
}

fn confined(root: &Path, path: &Path) -> Result<PathBuf, String> {
    if !root.is_absolute() || !path.is_absolute() || !path.starts_with(root) {
        return Err("Vault storage path outside configured root".into());
    }
    if path
        .components()
        .any(|c| matches!(c, std::path::Component::ParentDir))
    {
        return Err("Vault storage parent traversal".into());
    }
    // Check the root and its parents too: a configured alias cannot hide a link.
    for part in path.ancestors() {
        let metadata = fs::symlink_metadata(part).map_err(|e| e.to_string())?;
        if metadata.file_type().is_symlink() {
            return Err("Vault storage symbolic link refused".into());
        }
        #[cfg(windows)]
        {
            use std::os::windows::fs::MetadataExt;
            if metadata.file_attributes() & 0x400 != 0 {
                return Err("Vault storage reparse point refused".into());
            }
        }
    }
    Ok(path.to_owned())
}

fn descriptor(root: &Path, path: &Path, limit: u64) -> Result<(File, Option<Descriptor>), String> {
    let path = confined(root, path)?;
    let mut file = File::open(path).map_err(|e| e.to_string())?;
    let metadata = file.metadata().map_err(|e| e.to_string())?;
    if !metadata.is_file() {
        return Err("Vault payload is not a regular file".into());
    }
    let mut magic = [0; 8];
    let count = if metadata.len() >= MAGIC.len() as u64 {
        file.read_exact(&mut magic).map_err(|e| e.to_string())?;
        MAGIC.len()
    } else {
        0
    };
    if count != MAGIC.len() || &magic != MAGIC {
        if metadata.len() > limit {
            return Err("Vault logical payload exceeds byte budget".into());
        }
        file.rewind().map_err(|e| e.to_string())?;
        return Ok((file, None));
    }
    if metadata.len() > MAX_DESCRIPTOR {
        return Err("Vault descriptor exceeds byte budget".into());
    }
    let mut raw = Vec::new();
    (&mut file)
        .take(MAX_DESCRIPTOR + 1)
        .read_to_end(&mut raw)
        .map_err(|e| e.to_string())?;
    if raw.len() as u64 > MAX_DESCRIPTOR - MAGIC.len() as u64 {
        return Err("Vault descriptor grew during read".into());
    }
    let value: Descriptor = serde_json::from_slice(&raw).map_err(|e| e.to_string())?;
    validate(&value, limit)?;
    let marker = confined(root, &root.join(".forge-storage/root.v1.json"))?;
    let mut marker_bytes = Vec::new();
    File::open(marker)
        .map_err(|e| e.to_string())?
        .take(4097)
        .read_to_end(&mut marker_bytes)
        .map_err(|e| e.to_string())?;
    if marker_bytes.len() > 4096 {
        return Err("Vault storage root marker exceeds byte budget".into());
    }
    let marker: Root = serde_json::from_slice(&marker_bytes).map_err(|e| e.to_string())?;
    if marker.schema != 1 || marker.vault_id != value.vault_id {
        return Err("Vault storage root identity mismatch".into());
    }
    Ok((file, Some(value)))
}

fn validate(value: &Descriptor, limit: u64) -> Result<(), String> {
    if value.schema != 1
        || !hex(&value.vault_id, 32)
        || !hex(&value.sha256, 64)
        || value.bytes > limit
        || value.segments.is_empty()
        || value.segments.len() > MAX_SEGMENTS
    {
        return Err("Invalid Vault storage descriptor or logical byte budget".into());
    }
    let mut sum = 0_u64;
    for segment in &value.segments {
        if !hex(&segment.pack, 32)
            || !hex(&segment.sha256, 64)
            || segment.bytes == 0
            || segment.bytes > MAX_BLOCK
            || segment.stored_bytes == 0
            || segment.stored_bytes > MAX_BLOCK
            || !matches!(segment.codec.as_str(), "raw" | "zlib")
            || (segment.codec == "raw" && segment.bytes != segment.stored_bytes)
            || segment.offset.checked_add(segment.stored_bytes).is_none()
        {
            return Err("Invalid Vault storage segment".into());
        }
        sum = sum
            .checked_add(segment.bytes)
            .ok_or("Vault segment length overflow")?;
    }
    if sum != value.bytes {
        return Err("Vault segment total differs from original byte length".into());
    }
    Ok(())
}

/// Logical size for budgets and exports, before allocating reconstructed bytes.
pub fn logical_size(root: &Path, path: &Path, limit: u64) -> Result<u64, String> {
    let (file, value) = descriptor(root, path, limit)?;
    value.map_or_else(
        || file.metadata().map(|m| m.len()).map_err(|e| e.to_string()),
        |v| Ok(v.bytes),
    )
}

fn segment(
    root: &Path,
    value: &Segment,
    handles: &mut HashMap<String, File>,
) -> Result<Vec<u8>, String> {
    if !handles.contains_key(&value.pack) {
        if handles.len() >= 16 {
            handles.clear();
        }
        let path = confined(
            root,
            &root
                .join(".forge-storage/packs")
                .join(format!("{}.pack", value.pack)),
        )?;
        handles.insert(
            value.pack.clone(),
            File::open(path).map_err(|e| e.to_string())?,
        );
    }
    let file = handles
        .get_mut(&value.pack)
        .ok_or("Vault pack handle missing")?;
    let metadata = file.metadata().map_err(|e| e.to_string())?;
    if !metadata.is_file() || value.offset + value.stored_bytes > metadata.len() {
        return Err("Vault pack segment outside regular file".into());
    }
    file.seek(SeekFrom::Start(value.offset))
        .map_err(|e| e.to_string())?;
    let mut stored = Vec::new();
    (&mut *file)
        .take(value.stored_bytes)
        .read_to_end(&mut stored)
        .map_err(|e| e.to_string())?;
    if stored.len() as u64 != value.stored_bytes {
        return Err("Truncated Vault pack segment".into());
    }
    let bytes = if value.codec == "raw" {
        stored
    } else {
        let mut decoder = flate2::read::ZlibDecoder::new(stored.as_slice());
        let mut decoded = Vec::new();
        (&mut decoder)
            .take(value.bytes + 1)
            .read_to_end(&mut decoded)
            .map_err(|e| e.to_string())?;
        if decoder.total_in() != value.stored_bytes {
            return Err("Vault zlib segment has trailing or unconsumed bytes".into());
        }
        decoded
    };
    if bytes.len() as u64 != value.bytes || crate::hash(&bytes) != value.sha256 {
        return Err("Vault segment length or SHA-256 mismatch".into());
    }
    Ok(bytes)
}

/// Stream the logical file. A writer must publish its output only after success.
fn copy_logical(
    root: &Path,
    path: &Path,
    limit: u64,
    output: &mut impl Write,
) -> Result<(u64, String), String> {
    let (mut file, value) = descriptor(root, path, limit)?;
    if let Some(value) = value {
        let mut hash = Sha256::new();
        let mut handles = HashMap::new();
        for item in &value.segments {
            let bytes = segment(root, item, &mut handles)?;
            hash.update(&bytes);
            output.write_all(&bytes).map_err(|e| e.to_string())?;
        }
        let digest = crate::hex(&hash.finalize());
        if digest != value.sha256 {
            return Err("Reconstructed Vault model SHA-256 mismatch".into());
        }
        Ok((value.bytes, digest))
    } else {
        let before = file.metadata().map_err(|e| e.to_string())?;
        let mut hash = Sha256::new();
        let mut count = 0_u64;
        let mut buffer = vec![0_u8; 128 * 1024];
        loop {
            let read = file.read(&mut buffer).map_err(|e| e.to_string())?;
            if read == 0 {
                break;
            }
            count = count
                .checked_add(read as u64)
                .ok_or("Vault payload length overflow")?;
            if count > limit {
                return Err("Vault payload grew beyond byte budget".into());
            }
            hash.update(&buffer[..read]);
            output
                .write_all(&buffer[..read])
                .map_err(|e| e.to_string())?;
        }
        let after = file.metadata().map_err(|e| e.to_string())?;
        if count != before.len()
            || count > limit
            || before.len() != after.len()
            || before.modified().ok() != after.modified().ok()
        {
            return Err("Vault payload changed while reading".into());
        }
        Ok((count, crate::hex(&hash.finalize())))
    }
}

pub fn read_file(root: &Path, path: &Path, limit: u64) -> Result<Vec<u8>, String> {
    read_file_with_hash(root, path, limit).map(|(bytes, _)| bytes)
}

/// Return the digest of the bytes actually read; hosts need not hash the carrier again.
pub fn read_file_with_hash(
    root: &Path,
    path: &Path,
    limit: u64,
) -> Result<(Vec<u8>, String), String> {
    let size = logical_size(root, path, limit)?;
    let mut bytes = Vec::new();
    bytes
        .try_reserve_exact(usize::try_from(size).map_err(|_| "Vault allocation overflow")?)
        .map_err(|e| e.to_string())?;
    let (_, digest) = copy_logical(root, path, limit, &mut bytes)?;
    if bytes.len() as u64 != size {
        return Err("Vault logical size changed during read".into());
    }
    Ok((bytes, digest))
}

pub fn file_hash(root: &Path, path: &Path, limit: u64) -> Result<String, String> {
    copy_logical(root, path, limit, &mut std::io::sink()).map(|(_, digest)| digest)
}

/// Stream only to a staged output; a caller must publish after this returns success.
pub fn copy_to(
    root: &Path,
    path: &Path,
    limit: u64,
    output: &mut impl Write,
) -> Result<u64, String> {
    copy_logical(root, path, limit, output).map(|(count, _)| count)
}

pub fn copy_verified(
    root: &Path,
    path: &Path,
    limit: u64,
    expected: &str,
    output: &mut impl Write,
) -> Result<u64, String> {
    let (count, digest) = copy_logical(root, path, limit, output)?;
    if !hex(expected, 64) || digest != expected {
        return Err("Vault export content pin mismatch".into());
    }
    Ok(count)
}

#[cfg(test)]
mod tests;
