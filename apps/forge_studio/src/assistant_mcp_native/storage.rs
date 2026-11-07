//! Immutable, unapproved MCP proposals. No permission or review is deserialized.
use forge_core::{
    CommandId, ContentHash, canonical_json_bytes, canonical_json_sha256,
    document_store::{DocumentStore, StoredDocument},
};
use forge_project::{assistant::planning::PlanProposal, audit::AuditStamp};
use serde::{Deserialize, Serialize};
use std::{
    fs,
    path::{Path, PathBuf},
};

#[derive(Clone, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub(super) struct Record {
    pub schema: String,
    pub command_id: CommandId,
    pub proposal_hash: ContentHash,
    pub proposal: PlanProposal,
    pub proposed_utc: String,
}

pub(super) fn command_id(context: ContentHash, proposal: ContentHash) -> CommandId {
    CommandId::deterministic(format!("forge/mcp-proposal/{context}/{proposal}"))
}

pub(super) fn load(root: &Path, context: ContentHash) -> Result<Vec<Record>, String> {
    let Some(directory) = context_directory(root, context, false)? else {
        return Ok(vec![]);
    };
    let mut records = Vec::new();
    for entry in fs::read_dir(directory).map_err(|e| e.to_string())?.take(17) {
        if records.len() == 16 {
            return Err("MCP staging exceeds its 16-proposal context budget".into());
        }
        let entry = entry.map_err(|e| e.to_string())?;
        checked_directory(&entry.path())?;
        let hash = ContentHash::parse(
            entry
                .file_name()
                .to_str()
                .ok_or("Invalid proposal identity")?,
        )
        .map_err(|error| error.to_string())?;
        let store = DocumentStore::open(&entry.path()).map_err(|e| e.to_string())?;
        records.push(read(&store, context, hash)?);
    }
    records.sort_by_key(|record| record.command_id);
    Ok(records)
}

pub(super) fn save(root: &Path, context: ContentHash, record: &Record) -> Result<(), String> {
    validate(record, context, record.proposal_hash)?;
    let parent = context_directory(root, context, true)?.ok_or("MCP staging directory absent")?;
    let directory = parent.join(record.proposal_hash.to_string());
    create_directory(&directory)?;
    let store = DocumentStore::open(&directory).map_err(|e| e.to_string())?;
    if store.load().map_err(|e| e.to_string())?.is_some()
        || store.pending().map_err(|e| e.to_string())?.is_some()
    {
        let existing = read(&store, context, record.proposal_hash)?;
        if existing.command_id != record.command_id || existing.proposal != record.proposal {
            return Err("Immutable MCP proposal differs from saved staging".into());
        }
        return Ok(());
    }
    store
        .save(
            None,
            &canonical_json_bytes(record).map_err(|e| e.to_string())?,
        )
        .map_err(|e| e.to_string())?;
    Ok(())
}

fn read(store: &DocumentStore, context: ContentHash, hash: ContentHash) -> Result<Record, String> {
    let saved = store.load().map_err(|e| e.to_string())?;
    if let Some(pending) = store.pending().map_err(|e| e.to_string())? {
        // This is an immutable proposal, not a source transaction or a job ACK.
        // Only a complete, identical record may finish its interrupted staging.
        let candidate = parse(&pending, context, hash)?;
        if let Some(saved) = &saved {
            let previous = parse(saved, context, hash)?;
            if canonical_json_bytes(&previous).map_err(|e| e.to_string())?
                != canonical_json_bytes(&candidate).map_err(|e| e.to_string())?
            {
                return Err(
                    "MCP interrupted staging differs from its immutable saved proposal".into(),
                );
            }
        }
        store
            .recover(
                saved.as_ref().map(|document| document.hash),
                pending.hash,
                true,
            )
            .map_err(|e| e.to_string())?;
        return Ok(candidate);
    }
    parse(
        &saved.ok_or("MCP proposal has no saved data")?,
        context,
        hash,
    )
}

fn parse(
    document: &StoredDocument,
    context: ContentHash,
    hash: ContentHash,
) -> Result<Record, String> {
    let record: Record = serde_json::from_slice(&document.bytes).map_err(|e| e.to_string())?;
    validate(&record, context, hash)?;
    Ok(record)
}
fn validate(record: &Record, context: ContentHash, hash: ContentHash) -> Result<(), String> {
    let bytes = canonical_json_bytes(&record.proposal).map_err(|e| e.to_string())?;
    PlanProposal::from_bytes(&bytes)?;
    AuditStamp::ai("mcp-proposal", &record.proposed_utc)?;
    if record.schema != "forge.mcp-proposal.v1"
        || record.proposal.context_hash != context
        || record.proposal_hash != hash
        || canonical_json_sha256(&record.proposal).map_err(|e| e.to_string())? != hash
        || record.command_id != command_id(context, hash)
    {
        return Err("MCP staging identity or authorized context mismatch".into());
    }
    Ok(())
}

fn context_directory(
    root: &Path,
    context: ContentHash,
    create: bool,
) -> Result<Option<PathBuf>, String> {
    let root = root.canonicalize().map_err(|e| e.to_string())?;
    let group = root.join("assistant-mcp-proposals");
    let directory = group.join(context.to_string());
    for path in [&group, &directory] {
        match fs::symlink_metadata(path) {
            Ok(_) => checked_directory(path)?,
            Err(error) if error.kind() == std::io::ErrorKind::NotFound && !create => {
                return Ok(None);
            }
            Err(error) if error.kind() == std::io::ErrorKind::NotFound => create_directory(path)?,
            Err(error) => return Err(error.to_string()),
        }
        if !path
            .canonicalize()
            .map_err(|e| e.to_string())?
            .starts_with(&root)
        {
            return Err("MCP staging directory escapes the native root".into());
        }
    }
    Ok(Some(directory))
}
fn create_directory(path: &Path) -> Result<(), String> {
    match fs::create_dir(path) {
        Ok(()) => (),
        Err(error) if error.kind() == std::io::ErrorKind::AlreadyExists => (),
        Err(error) => return Err(error.to_string()),
    }
    checked_directory(path)
}
fn checked_directory(path: &Path) -> Result<(), String> {
    let metadata = fs::symlink_metadata(path).map_err(|e| e.to_string())?;
    #[cfg(windows)]
    let linked = {
        use std::os::windows::fs::MetadataExt;
        metadata.file_attributes() & 0x400 != 0
    };
    #[cfg(not(windows))]
    let linked = metadata.file_type().is_symlink();
    if !metadata.is_dir() || linked {
        return Err("MCP staging entries cannot be links or non-directories".into());
    }
    Ok(())
}
