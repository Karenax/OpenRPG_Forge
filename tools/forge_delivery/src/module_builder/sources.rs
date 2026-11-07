//! Exact source closure shared by single-area and aggregate exports.
use super::*;

fn path(hash: ContentHash) -> String {
    format!("_forge-export/source-blobs/{hash}.bin")
}

pub(super) fn validate_budget(
    required: &BTreeMap<ContentHash, usize>,
    archives: usize,
) -> Result<()> {
    let mut remaining = forge_project::workspace::export::MAX_MODULE_EXPORT_SOURCE_BYTES
        .checked_sub(archives)
        .ok_or("Module export archive/source budget exceeded")?;
    for &length in required.values() {
        if length == 0 || length > forge_project::workspace::source_blobs::MAX_SOURCE_BLOB_BYTES {
            return Err("Exported source blob exceeds its size budget".into());
        }
        remaining = remaining
            .checked_sub(length)
            .ok_or("Module export archive/source budget exceeded")?;
    }
    Ok(())
}

pub(super) fn inventory(
    required: &BTreeMap<ContentHash, usize>,
    payloads: &BTreeMap<String, Vec<u8>>,
    inventory: &mut Inventory,
) -> Result<()> {
    for (&hash, &length) in required {
        let name = path(hash);
        let bytes = payloads
            .get(&name)
            .ok_or("An exported canonical source blob is missing")?;
        if bytes.len() != length || bytes_sha256(bytes) != hash {
            return Err("An exported source differs from its canonical length or hash".into());
        }
        inventory.role(&name, ModuleFileRole::SourceBlob)?;
    }
    Ok(())
}

pub(super) fn insert_payloads(
    payloads: &mut BTreeMap<String, Vec<u8>>,
    required: &BTreeMap<ContentHash, usize>,
    sources: &BTreeMap<ContentHash, Vec<u8>>,
) -> Result<()> {
    for &hash in required.keys() {
        let name = path(hash);
        if payloads.contains_key(&name) {
            return Err("Source blob path conflicts with an asset payload".into());
        }
        let bytes = sources
            .get(&hash)
            .ok_or("Module snapshot source blob missing")?;
        payloads.insert(name, bytes.clone());
    }
    Ok(())
}

pub(super) fn append(
    prepared: &mut Prepared,
    required: &BTreeMap<ContentHash, usize>,
    sources: &BTreeMap<ContentHash, Vec<u8>>,
) -> Result<()> {
    for (&hash, &length) in required {
        let name = path(hash);
        let bytes = sources
            .get(&hash)
            .ok_or("Module snapshot source blob missing")?;
        if let Some(previous) = prepared.payloads.get(&name) {
            if previous != bytes
                || !prepared.module.files.iter().any(|file| {
                    file.path == name
                        && file.sha256 == hash
                        && file.bytes == length as u64
                        && file.role == ModuleFileRole::SourceBlob
                })
            {
                return Err("Conflicting shared module source payload".into());
            }
        } else {
            prepared.module.files.push(ModuleFile {
                path: name.clone(),
                bytes: length as u64,
                sha256: hash,
                role: ModuleFileRole::SourceBlob,
            });
            prepared.payloads.insert(name, bytes.clone());
        }
    }
    Ok(())
}
