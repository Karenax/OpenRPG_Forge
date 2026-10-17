//! Lossless command replay archive: derived inverses/heads are verified by digests.
use super::{ZoneEditSession, ZoneReceipt, ZoneRequest};
use crate::audit::AuditStamp;
use crate::zone_authoring::{
    MAX_ZONE_HEAD_BYTES, ZoneAuthoringHead, migration::ZoneMigrationProposal,
};
use forge_core::{ContentHash, canonical_json_bytes, canonical_json_sha256};
use serde::{Deserialize, Serialize};

#[derive(Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct CompactReceipt {
    request: ZoneRequest,
    #[serde(default, skip_serializing_if = "AuditStamp::is_legacy")]
    audit: AuditStamp,
    /// Covers the entire original receipt, including inverse and base changes.
    receipt_hash: ContentHash,
}

#[derive(Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct CompactArchive {
    schema: String,
    version: u32,
    migration: ZoneMigrationProposal,
    head_hash: ContentHash,
    commands: Vec<CompactReceipt>,
}

pub(super) fn encode(
    migration: &ZoneMigrationProposal,
    head: &ZoneAuthoringHead,
    receipts: &[ZoneReceipt],
) -> Result<Vec<u8>, String> {
    let commands = receipts
        .iter()
        .map(|receipt| {
            Ok(CompactReceipt {
                request: receipt.request.clone(),
                audit: receipt.audit.clone(),
                receipt_hash: canonical_json_sha256(receipt).map_err(|e| e.to_string())?,
            })
        })
        .collect::<Result<Vec<_>, String>>()?;
    let bytes = canonical_json_bytes(&CompactArchive {
        schema: "forge.zone-edit-session".into(),
        version: version(receipts),
        migration: migration.clone(),
        head_hash: head.content_hash()?,
        commands,
    })
    .map_err(|e| e.to_string())?;
    if bytes.len() > MAX_ZONE_HEAD_BYTES {
        return Err("Zone archive exceeds 4 MiB before mutation".into());
    }
    Ok(bytes)
}

pub(super) fn decode(bytes: &[u8]) -> Result<ZoneEditSession, String> {
    let archive: CompactArchive = serde_json::from_slice(bytes).map_err(|e| e.to_string())?;
    if archive.schema != "forge.zone-edit-session"
        || !matches!(
            archive.version,
            3 | 5
                | 9
                | 11
                | 14
                | 17
                | 20
                | 23
                | 26
                | 29
                | 32
                | 35
                | super::SOURCE_OBJECT_COMPACT_VERSION
                | super::PHYSICS_COMPACT_VERSION
                | super::PLACEMENT_PHYSICS_COMPACT_VERSION
                | super::GAMEPLAY_VISUALS_COMPACT_VERSION
        )
        || archive.commands.len() > 1024
    {
        return Err("Unsupported compact zone archive or command limit".into());
    }
    let mut session = ZoneEditSession::from_migration(archive.migration)?;
    session.compact_archive = true;
    for command in archive.commands {
        if session.previous(&command.request)?.is_some() {
            return Err("Duplicate archived receipt".into());
        }
        let receipt = session.execute_with_audit(&command.request, command.audit)?;
        if canonical_json_sha256(&receipt).map_err(|e| e.to_string())? != command.receipt_hash {
            return Err("Compact receipt replay mismatch".into());
        }
    }
    if session.head.content_hash()? != archive.head_hash {
        return Err("Compact zone head differs from audited result".into());
    }
    let expected_version = version(&session.receipts);
    if archive.version != expected_version {
        return Err("Compact archive version does not cover its commands".into());
    }
    Ok(session)
}

pub(super) fn version(receipts: &[ZoneReceipt]) -> u32 {
    if super::has_gameplay_visuals(receipts) {
        return super::GAMEPLAY_VISUALS_COMPACT_VERSION;
    }
    if super::has_placement_physics(receipts) {
        return super::PLACEMENT_PHYSICS_COMPACT_VERSION;
    }
    if super::has_physics(receipts) {
        return super::PHYSICS_COMPACT_VERSION;
    }
    if super::has_source_objects(receipts) {
        return super::SOURCE_OBJECT_COMPACT_VERSION;
    }
    if super::archive_version(receipts) == 34 {
        return 35;
    }
    if super::archive_version(receipts) == 31 {
        return 32;
    }
    match super::archive_version(receipts) {
        28 => 29,
        25 => 26,
        22 => 23,
        19 => 20,
        16 => 17,
        13 => 14,
        10 => 11,
        8 => 9,
        4 => 5,
        _ => 3,
    }
}
