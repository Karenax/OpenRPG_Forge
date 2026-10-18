//! Opt-in GPU observations; never supplies exposure to the renderer.
use bevy::{
    prelude::*,
    render::{
        RenderApp,
        gpu_readback::{Readback, ReadbackComplete},
        render_asset::RenderAssets,
        storage::{GpuShaderBuffer, ShaderBuffer},
    },
};
use std::{
    path::PathBuf,
    sync::{
        Arc,
        atomic::{AtomicU32, AtomicU64, Ordering},
    },
};

pub(super) const CAPACITY: usize = 2048;
pub(super) const STRIDE: usize = 80;

#[derive(Resource, Clone)]
pub(super) struct Trace {
    pub packet: Handle<ShaderBuffer>,
    path: PathBuf,
    accepted: Arc<AtomicU32>,
    owner: Arc<AtomicU64>,
}

pub(super) fn install(app: &mut App) {
    let Some(path) = std::env::var_os("FORGE_TIDEWATER_SOURCE_FINAL_TRACE") else {
        return;
    };
    let mut buffer = ShaderBuffer::from(vec![0_u32; CAPACITY * STRIDE / 4]);
    buffer.buffer_description.usage |= wgpu::BufferUsages::COPY_SRC | wgpu::BufferUsages::COPY_DST;
    let packet = app
        .world_mut()
        .resource_mut::<Assets<ShaderBuffer>>()
        .add(buffer);
    let trace = Trace {
        packet,
        path: PathBuf::from(path),
        accepted: Arc::new(AtomicU32::new(0)),
        owner: Arc::new(AtomicU64::new(u64::MAX)),
    };
    app.insert_resource(trace.clone())
        .add_systems(Startup, request);
    if let Some(render) = app.get_sub_app_mut(RenderApp) {
        render.insert_resource(trace);
    }
}
fn request(mut commands: Commands, trace: Res<Trace>) {
    commands
        .spawn(Readback::buffer(trace.packet.clone()))
        .observe(receive);
}

pub(super) fn packet<'a>(
    trace: Option<&Trace>,
    buffers: &'a RenderAssets<GpuShaderBuffer>,
    entity: Entity,
) -> Option<&'a GpuShaderBuffer> {
    let trace = trace?;
    let owner = trace
        .owner
        .compare_exchange(
            u64::MAX,
            entity.to_bits(),
            Ordering::AcqRel,
            Ordering::Acquire,
        )
        .unwrap_or_else(|old| old);
    if owner != u64::MAX && owner != entity.to_bits() {
        return None;
    }
    buffers.get(&trace.packet)
}

fn receive(
    event: On<ReadbackComplete>,
    trace: Res<Trace>,
    mut exit: MessageWriter<bevy::app::AppExit>,
) {
    if event.data.len() != CAPACITY * STRIDE {
        error!("Source final GPU trace has invalid byte length");
        exit.write(bevy::app::AppExit::error());
        return;
    }
    let mut rows = Vec::new();
    for (i, bytes) in event.data.as_chunks::<STRIDE>().0.iter().enumerate() {
        let word = |j: usize| {
            u32::from_le_bytes([
                bytes[j * 4],
                bytes[j * 4 + 1],
                bytes[j * 4 + 2],
                bytes[j * 4 + 3],
            ])
        };
        let completed = word(18);
        if completed == 0 {
            break;
        }
        if completed as usize != i + 1 || word(12) != completed {
            error!("Source final GPU trace is not a consecutive completed history");
            exit.write(bevy::app::AppExit::error());
            return;
        }
        let values: Vec<_> = (0..12).map(|j| f32::from_bits(word(j))).collect();
        let before = f32::from_bits(word(16));
        let after = f32::from_bits(word(17));
        if values.iter().any(|v| !v.is_finite())
            || !before.is_finite()
            || !after.is_finite()
            || before <= 0.0
            || after <= 0.0
        {
            error!("Source final GPU trace contains non-finite or non-positive exposure");
            exit.write(bevy::app::AppExit::error());
            return;
        }
        rows.push(serde_json::json!({"submitted":completed,"control_f32_bits":(0..12).map(word).collect::<Vec<_>>(),
            "control_f32":values,"frame_u32":(12..16).map(word).collect::<Vec<_>>(),
            "exposure_before":before,"exposure_after":after,
            "exposure_before_bits":word(16),"exposure_after_bits":word(17)}));
    }
    let completed = u32::try_from(rows.len()).unwrap_or(u32::MAX);
    if completed < 124 || completed <= trace.accepted.load(Ordering::Acquire) {
        return;
    }
    let receipt = serde_json::json!({"schema":"forge.tidewater.source-final-gpu-trace","version":1,
        "completed":completed,"capacity":CAPACITY,"row_bytes":STRIDE,"rows":rows,"render_view_bits":trace.owner.load(Ordering::Acquire),
        "actual_gpu_copies":true,"source_exposure_injected":false,"history_paired":false,
        "limits":["Asynchronous readback; completion can precede screenshot","Native render dt and geometry history; not acquired source history"]});
    let saved = serde_json::to_vec_pretty(&receipt)
        .map_err(|e| e.to_string())
        .and_then(|bytes| std::fs::write(&trace.path, bytes).map_err(|e| e.to_string()));
    if let Err(error) = saved {
        error!(%error,"Unable to save source final GPU trace");
        exit.write(bevy::app::AppExit::error());
        return;
    }
    trace.accepted.store(completed, Ordering::Release);
}
