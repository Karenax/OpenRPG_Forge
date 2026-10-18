//! Multiple admitted owner clocks in one renderer; native oracles stay on CPU.
use super::{Manifest, Step, emission, read};
use bevy::{
    prelude::App,
    render::{RenderApp, render_asset::RenderAssets, storage::GpuShaderBuffer},
};
use forge_core::{ContentHash, bytes_sha256};
use std::path::{Path, PathBuf};

pub(super) struct Companion {
    pub steps: Vec<Step>,
    pub expected: Vec<Option<Vec<u8>>>,
}

/// This diagnostic shares one admitted mesh/material/camera only when every
/// non-schedule input is identical. Distinct source transforms/materials need
/// their own qualification, not inferred defaults here.
pub(super) fn load_companion(
    args: &mut impl Iterator<Item = String>,
    primary_bytes: &[u8],
) -> Result<Option<Companion>, String> {
    let Some(path) = args.next() else {
        return Ok(None);
    };
    let expected_hash = args
        .next()
        .ok_or("Companion manifest SHA absent")?
        .parse::<ContentHash>()
        .map_err(|e| e.to_string())?;
    if args.next().is_some() {
        return Err("Unexpected source draw arguments".into());
    }
    let path = Path::new(&path);
    let bytes = std::fs::read(path).map_err(|e| e.to_string())?;
    if bytes_sha256(&bytes) != expected_hash {
        return Err("Companion manifest SHA mismatch".into());
    }
    let mut primary: serde_json::Value =
        serde_json::from_slice(primary_bytes).map_err(|e| e.to_string())?;
    let mut companion: serde_json::Value =
        serde_json::from_slice(&bytes).map_err(|e| e.to_string())?;
    primary
        .as_object_mut()
        .ok_or("Primary manifest must be an object")?
        .remove("steps");
    companion
        .as_object_mut()
        .ok_or("Companion manifest must be an object")?
        .remove("steps");
    if primary != companion {
        return Err("Paired owner mesh/material/program inputs differ".into());
    }
    let manifest: Manifest = serde_json::from_slice(&bytes).map_err(|e| e.to_string())?;
    if manifest.source_spawner.is_none() || manifest.steps.is_empty() || manifest.steps.len() > 4096
    {
        return Err("Paired owners require admitted autonomous source schedules".into());
    }
    emission::validate(&manifest.steps, manifest.source_spawner)?;
    let root = path.parent().ok_or("Companion manifest parent absent")?;
    let expected = manifest
        .steps
        .iter()
        .map(|s| s.expected.as_ref().map(|p| read(root, p)).transpose())
        .collect::<Result<_, _>>()?;
    Ok(Some(Companion {
        steps: manifest.steps,
        expected,
    }))
}

pub(super) struct Timeline {
    pub sequence: emission::SimulationSequence,
    pub steps: Vec<Step>,
    pub expected: Vec<Option<Vec<u8>>>,
    pub directory: PathBuf,
    pub draws: Vec<serde_json::Value>,
    pub projections: Vec<serde_json::Value>,
    pub emission_frames: Vec<serde_json::Value>,
    pub native_checkpoints: u32,
}

impl Timeline {
    pub(super) fn new(
        sequence: emission::SimulationSequence,
        steps: Vec<Step>,
        expected: Vec<Option<Vec<u8>>>,
        directory: PathBuf,
    ) -> Result<Self, String> {
        if steps.len() != expected.len() {
            return Err("Owner expected schedule cardinality mismatch".into());
        }
        std::fs::create_dir_all(&directory).map_err(|e| e.to_string())?;
        Ok(Self {
            sequence,
            steps,
            expected,
            directory,
            draws: Vec::new(),
            projections: Vec::new(),
            emission_frames: Vec::new(),
            native_checkpoints: 0,
        })
    }

    pub(super) fn verify_draws(&self) -> Result<(), String> {
        let live: Vec<_> = self.draws.iter().filter(|r| r["alive"] == true).collect();
        let image_motion = live.windows(2).any(|r| r[0]["sha256"] != r[1]["sha256"]);
        let mut projected_motion = false;
        let mut previous_positions: Option<Vec<u8>> = None;
        for draw in &live {
            let stamp = draw["step"].as_u64().ok_or("Draw stamp invalid")?;
            let Some(projection) = self
                .projections
                .iter()
                .find(|p| p["step"].as_u64() == Some(stamp))
            else {
                continue;
            };
            let prefix = projection["resident_source_state_echo_bytes"]
                .as_u64()
                .and_then(|n| usize::try_from(n).ok())
                .ok_or("Projection state prefix invalid")?;
            let bytes = std::fs::read(
                self.directory
                    .join(format!("step-{stamp}.projected-vertices.bin")),
            )
            .map_err(|e| e.to_string())?;
            let vertices = bytes
                .get(prefix..)
                .ok_or("Projection vertex prefix outside buffer")?;
            if !vertices.len().is_multiple_of(112) {
                return Err("Projection vertex ABI invalid".into());
            }
            let positions: Vec<_> = vertices
                .as_chunks::<112>()
                .0
                .iter()
                .flat_map(|v| v[..16].iter().copied())
                .collect();
            if previous_positions.as_ref().is_some_and(|p| p != &positions) {
                projected_motion = true;
            }
            previous_positions = Some(positions);
        }
        if live.len() < 2
            || !self.draws.iter().any(|r| r["alive"] == false)
            || !(image_motion || projected_motion)
        {
            return Err("Owner motion/death coverage evidence insufficient".into());
        }
        Ok(())
    }

    pub(super) fn report(&self, app: &App) -> Result<serde_json::Value, String> {
        Ok(
            serde_json::json!({"steps":self.steps.len(),"native_checkpoints":self.native_checkpoints,
            "encoded_compute_phases":self.sequence.encoded_steps(app)?,"source_emission_frames":self.emission_frames,
            "draws":self.draws,"resident_vertex_projection_draws":self.projections,
            "CPU_state_uploads_between_steps":0,"same_renderer_independent_owner":true,
            "scene_owner_component_used":self.sequence.entity().is_some()}),
        )
    }
}

pub(super) fn verify_distinct_gpu_buffers(
    app: &App,
    owners: &[Timeline],
    state_bytes: u64,
) -> Result<(), String> {
    let render = app
        .get_sub_app(RenderApp)
        .ok_or("Owner renderer absent")?
        .world();
    let buffers = render.resource::<RenderAssets<GpuShaderBuffer>>();
    let mut ids = Vec::new();
    for owner in owners {
        let gpu = buffers
            .get(&owner.sequence.current_buffer(app)?)
            .ok_or("Owner GPU state absent")?;
        if gpu.buffer.size() != state_bytes || ids.contains(&gpu.buffer.id()) {
            return Err("Independent owners share a physical GPU state buffer or wrong ABI".into());
        }
        ids.push(gpu.buffer.id());
    }
    Ok(())
}
