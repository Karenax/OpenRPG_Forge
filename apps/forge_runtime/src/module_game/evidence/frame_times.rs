//! Opt-in compact frame intervals, separate from the bounded projection report.
use super::super::evidence_contract::{GraphicsObservation, HostStatus};
use super::metadata::{Metadata, Snapshot};
use super::{FrameTiming, MAX_FRAME_TIMINGS};
use forge_core::ContentHash;
use serde::Serialize;
use std::{ffi::OsString, fs::OpenOptions, io::Write, path::Path};

const SCOPE: &str = "Every recorded main-frame real-time interval; includes instrumentation and scheduling, excludes GPU timestamps and present latency. Select stable windows after preparation. Camera values are yaw, pitch, boom and vertical FOV in radians/metres. Sample graphics/imported values index dated metadata snapshots. GPU fences are evidence only at snapshot.frame, never at a later referencing sample. projection_ready is evaluated independently for every sample. Snapshots record semantic changes and a heartbeat at most 60 main frames apart; any quota truncation makes the measurement incomplete.";
const MAX_FRAME_FILE_BYTES: usize = 32 * 1024 * 1024;

pub(super) fn enabled(value: Option<OsString>) -> Result<bool, String> {
    match value.as_deref() {
        None => Ok(false),
        Some(value) if value == "0" => Ok(false),
        Some(value) if value == "1" => Ok(true),
        _ => Err("FORGE_GAME_FRAME_TIMES must be 0 or 1".into()),
    }
}

#[derive(Serialize)]
struct FrameFile<'a> {
    schema: &'static str,
    pid: u32,
    source_hash: ContentHash,
    scope: &'static str,
    maximum_samples: usize,
    maximum_metadata_snapshots_per_domain: usize,
    truncated: bool,
    samples: &'a [FrameTiming],
    graphics_observations: &'a [Snapshot<GraphicsObservation>],
    imported_observations: &'a [Snapshot<HostStatus>],
}

pub(super) fn write(
    report: &Path,
    enabled: bool,
    samples: &[FrameTiming],
    truncated: bool,
    source: ContentHash,
    metadata: &Metadata,
) -> Result<serde_json::Value, String> {
    let truncated = truncated || metadata.truncated;
    let mut summary = serde_json::json!({"enabled":enabled,"maximum_samples":MAX_FRAME_TIMINGS,
        "recorded":samples.len(),"truncated":truncated,"scope":SCOPE,
        "graphics_snapshots":metadata.graphics.len(),"imported_snapshots":metadata.imported.len()});
    if !enabled {
        return Ok(summary);
    }
    let path = report
        .parent()
        .ok_or("Frame timing parent missing")?
        .join("frame-timings.json");
    forge_delivery::manifest::safe_chain(&path)?;
    let bytes = encode(samples, truncated, source, metadata)?;
    let mut file = OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(&path)
        .map_err(|error| error.to_string())?;
    file.write_all(&bytes)
        .and_then(|()| file.sync_all())
        .map_err(|error| error.to_string())?;
    summary["artifact"] = "frame-timings.json".into();
    summary["sha256"] = forge_core::bytes_sha256(&bytes).to_string().into();
    Ok(summary)
}

pub(super) fn encode(
    samples: &[FrameTiming],
    truncated: bool,
    source: ContentHash,
    metadata: &Metadata,
) -> Result<Vec<u8>, String> {
    let schema = if samples.iter().all(|sample| {
        sample.selected_triangles.is_some()
            && sample.rendered_batches.is_some()
            && sample.generation.is_some()
            && sample.pending.is_some()
    }) {
        "forge.native-main-frame-intervals.v2"
    } else {
        "forge.native-main-frame-intervals.v3"
    };
    let bytes = serde_json::to_vec(&FrameFile {
        schema,
        pid: std::process::id(),
        source_hash: source,
        scope: SCOPE,
        maximum_samples: MAX_FRAME_TIMINGS,
        maximum_metadata_snapshots_per_domain: super::metadata::MAX_SNAPSHOTS,
        truncated: truncated || metadata.truncated,
        samples,
        graphics_observations: &metadata.graphics,
        imported_observations: &metadata.imported,
    })
    .map_err(|error| error.to_string())?;
    if bytes.len() > MAX_FRAME_FILE_BYTES {
        return Err("Frame timings exceed 32 MiB".into());
    }
    Ok(bytes)
}
