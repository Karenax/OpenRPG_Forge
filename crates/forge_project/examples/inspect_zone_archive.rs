//! Read-only audit of an existing archive and an explicitly selected lossless encoding.
use forge_project::zone_authoring::session::ZoneEditSession;

fn main() -> Result<(), String> {
    let path = std::env::args().nth(1).ok_or("Expected an archive path")?;
    let encoding = std::env::args().nth(2).unwrap_or_else(|| "compact".into());
    if !matches!(encoding.as_str(), "compact" | "delta" | "summary") {
        return Err("Expected encoding compact, delta or summary".into());
    }
    let metadata = std::fs::metadata(&path).map_err(|error| error.to_string())?;
    if metadata.len() > forge_project::zone_authoring::MAX_ZONE_HEAD_BYTES as u64 {
        return Err("Archive exceeds 4 MiB".into());
    }
    let original = std::fs::read(path).map_err(|error| error.to_string())?;
    let session = ZoneEditSession::from_archive_bytes(&original)
        .map_err(|e| format!("Original archive replay: {e}"))?;
    if encoding == "summary" {
        println!(
            "{}",
            serde_json::to_string_pretty(&serde_json::json!({
                "head_hash": session.head().content_hash()?,
                "receipts": session.audit().len(),
                "can_redo": session.can_redo(),
                "placements": session.head().corrections.placements,
                "vegetation": session.head().base.vegetation,
                "corrections_hash": forge_core::canonical_json_sha256(&session.head().corrections)
                    .map_err(|error| error.to_string())?,
                "base_terrain_hash": session.head().base.terrain.content_hash()
                    .map_err(|error| error.to_string())?,
                "files_written": 0,
            }))
            .map_err(|error| error.to_string())?
        );
        return Ok(());
    }
    let compact = match encoding.as_str() {
        "delta" => session.delta_archive_bytes(),
        _ => session.compact_archive_bytes(),
    }
    .map_err(|e| format!("{encoding} serialization: {e}"))?;
    let reopened = ZoneEditSession::from_archive_bytes(&compact)
        .map_err(|e| format!("{encoding} archive replay: {e}"))?;
    if session.head() != reopened.head() || session.audit() != reopened.audit() {
        return Err("Compaction changed the head or receipts".into());
    }
    println!(
        "encoding={encoding} original_bytes={} encoded_bytes={} receipts={} head_hash={} lossless=true files_written=0",
        original.len(),
        compact.len(),
        reopened.audit().len(),
        reopened.head().content_hash()?
    );
    Ok(())
}
