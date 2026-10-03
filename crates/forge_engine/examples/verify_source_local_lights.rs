//! Compare the source CPU oracle, then upload/read back on one Bevy `RenderDevice`.
//! Scenarios and frame tags are synthetic; no scene draw or visual verdict.
use bevy::{
    app::PluginsState,
    math::{DMat4, DVec3},
    prelude::*,
    render::{
        RenderApp,
        pipelined_rendering::PipelinedRenderingPlugin,
        render_resource::{
            Buffer, BufferDescriptor, BufferUsages, CommandEncoderDescriptor, MapMode,
        },
        renderer::{RenderDevice, RenderQueue},
    },
    window::ExitCondition,
    winit::WinitPlugin,
};
use forge_core::{ContentHash, bytes_sha256};
use forge_engine::{
    source_local_lights::{
        CameraPose, Collector, Flashlight, GpuPacket, Light, PACKET_BYTES, Settings,
    },
    tidewater_temporal::frame::{FrameTag, Frames},
    tidewater_temporal_frame::Input as TemporalInput,
};
use serde::Deserialize;
use std::{
    error::Error,
    path::Path,
    time::{Duration, Instant},
};

#[derive(Deserialize)]
struct Oracle {
    schema: String,
    version: u32,
    source_commit: String,
    frames: Vec<Frame>,
}
#[derive(Deserialize)]
struct Frame {
    id: String,
    input: Input,
    resolved_sources: Vec<Source>,
    camera: CameraInput,
    state_before: State,
    state_after: State,
    uniform: Uniform,
}
#[derive(Deserialize)]
struct Input {
    dt: f64,
    night: f64,
    strength: f64,
    enabled: bool,
    flashlight: FlashAction,
}
#[derive(Deserialize)]
struct FlashAction {
    toggle: Option<bool>,
}
#[derive(Deserialize)]
struct State {
    local_lights_time: f64,
    flashlight: Torch,
}
#[derive(Deserialize)]
struct Torch {
    intensity: f64,
    range: f64,
    color: [f64; 3],
    #[serde(rename = "cosInner")]
    cos_inner: f64,
    #[serde(rename = "cosOuter")]
    cos_outer: f64,
}
#[derive(Deserialize)]
struct CameraInput {
    local_position: [f64; 3],
    #[serde(rename = "matrixWorld")]
    matrix_world: [f64; 16],
}
#[derive(Deserialize)]
struct Source {
    position: [f64; 3],
    color: [f64; 3],
    intensity: f64,
    range: f64,
    dir: Option<[f64; 3]>,
    #[serde(rename = "cosInner")]
    cos_inner: Option<f64>,
    #[serde(rename = "cosOuter")]
    cos_outer: Option<f64>,
    enabled: bool,
    scale: f64,
    flicker: f64,
    phase: f64,
}
#[derive(Deserialize)]
struct Uniform {
    count: i32,
    packed_hex_le: String,
}

#[allow(
    clippy::too_many_lines,
    reason = "Keep the source prechecks, GPU readback and explicit negative cases in their execution order"
)]
fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args().skip(1).collect();
    if args.len() != 3 {
        return Err(
            "Usage: verify_source_local_lights ORACLE_JSON ORACLE_SHA256 FRESH_REPORT_JSON".into(),
        );
    }
    let output = Path::new(&args[2]);
    if output.exists() {
        return Err("proof report exists; use a fresh output path".into());
    }
    let bytes = std::fs::read(&args[0])?;
    let expected: ContentHash = args[1].parse()?;
    if bytes_sha256(&bytes) != expected {
        return Err("source oracle hash differs".into());
    }
    let oracle: Oracle = serde_json::from_slice(&bytes)?;
    if oracle.schema != "forge.source-local-lights-cpu-oracle"
        || oracle.version != 2
        || oracle.source_commit != "1438b1abfcaee3267092b75573014f4d9b4a983c"
        || oracle.frames.len() != 12
    {
        return Err("unexpected source oracle identity or frame coverage".into());
    }
    let mut collector = Collector::default();
    let mut frames = Vec::new();
    for frame in &oracle.frames {
        if collector.time().to_bits() != frame.state_before.local_lights_time.to_bits() {
            return Err(format!("{}: source time before differs", frame.id).into());
        }
        if let Some(on) = frame.input.flashlight.toggle {
            collector.toggle_flashlight(on);
        }
        let torch = &frame.state_before.flashlight;
        let lights: Vec<_> = frame
            .resolved_sources
            .iter()
            .map(|light| Light {
                position: DVec3::from_array(light.position),
                color: DVec3::from_array(light.color),
                intensity: light.intensity,
                range: light.range,
                direction: light.dir.map(DVec3::from_array),
                cos_inner: light.cos_inner,
                cos_outer: light.cos_outer,
                enabled: light.enabled,
                scale: light.scale,
                flicker: light.flicker,
                phase: light.phase,
            })
            .collect();
        let packet = collector
            .update(
                CameraPose {
                    local_position: DVec3::from_array(frame.camera.local_position),
                    world_from_view: DMat4::from_cols_array(&frame.camera.matrix_world),
                },
                Settings {
                    delta_seconds: frame.input.dt,
                    night: frame.input.night,
                    strength: frame.input.strength,
                    enabled: frame.input.enabled,
                    flashlight: Flashlight {
                        intensity: torch.intensity,
                        range: torch.range,
                        color: DVec3::from_array(torch.color),
                        cos_inner: torch.cos_inner,
                        cos_outer: torch.cos_outer,
                    },
                },
                &lights,
            )?
            .clone();
        let actual = packet.pack()?;
        let expected_bytes = decode_hex(&frame.uniform.packed_hex_le)?;
        if actual.as_slice() != expected_bytes || packet.count != frame.uniform.count {
            let differences: Vec<_> = actual
                .iter()
                .zip(&expected_bytes)
                .enumerate()
                .filter_map(|(i, (a, b))| (a != b).then_some(i))
                .collect();
            return Err(format!(
                "{}: source packet differs at bytes {differences:?}",
                frame.id
            )
            .into());
        }
        if collector.time().to_bits() != frame.state_after.local_lights_time.to_bits() {
            return Err(format!("{}: source time after differs", frame.id).into());
        }
        frames.push((
            frame.id.clone(),
            collector.clone(),
            frame.camera.matrix_world,
            actual,
        ));
    }
    let mut app = renderer_app()?;
    let view = app.world_mut().spawn_empty().id();
    let other = app.world_mut().spawn_empty().id();
    let render = app.get_sub_app(RenderApp).ok_or("Bevy RenderApp missing")?;
    let device = render.world().resource::<RenderDevice>();
    let queue = render.world().resource::<RenderQueue>();
    let mut resident = None;
    let mut views = Frames::default();
    views.activate(Some(view));
    let mut results = Vec::new();
    let mut negative_checks = 0;
    for (id, collector, matrix, expected_bytes) in frames {
        let prepared = views.prepare_aux(view, temporal_input(matrix))?;
        let frame = collector.for_view(&prepared)?;
        let tag = frame.tag();
        if let Some(buffer) = &mut resident {
            GpuPacket::update(buffer, queue, &frame)?;
        } else {
            resident = Some(GpuPacket::upload(device, queue, &frame)?);
        }
        let gpu = resident.as_ref().ok_or("GPU packet missing")?;
        let read = readback(device, queue, gpu.buffer(tag)?)?;
        if read != expected_bytes {
            return Err(format!("{id}: GPU readback differs from source packing").into());
        }
        if gpu.buffer(FrameTag { view: other, ..tag }).is_ok() {
            return Err("wrong-view buffer accepted".into());
        }
        negative_checks += 1;
        let repeated = resident
            .as_mut()
            .ok_or("GPU packet missing")?
            .update(queue, &frame);
        if repeated.is_ok() {
            return Err("repeated frame upload accepted".into());
        }
        negative_checks += 1;
        if resident
            .as_ref()
            .ok_or("GPU packet missing")?
            .buffer(tag)
            .is_ok()
        {
            return Err("failed publication left old data ready".into());
        }
        negative_checks += 1;
        results.push(
            serde_json::json!({"id":id,"count":frame.packet().count,"bytes":read.len(),
            "sha256":bytes_sha256(&read),"cpu_source_exact":true,"gpu_readback_exact":true}),
        );
    }
    let report = serde_json::json!({"status":"passed","oracle_sha256":expected,"source_commit":oracle.source_commit,
        "frames":results,"negative_checks":negative_checks,"same_bevy_render_app_device":true,
        "packet_bytes":PACKET_BYTES,"synthetic_scenarios_and_tags":true,"live_scene_resources_complete":false,
        "draw_executed":false,"appearance_qualified":false});
    std::fs::write(output, serde_json::to_vec_pretty(&report)?)?;
    println!(
        "{}",
        serde_json::json!({"status":"passed","source_frames":12,"gpu_readbacks":12,"negative_checks":negative_checks})
    );
    Ok(())
}

#[allow(
    clippy::cast_possible_truncation,
    reason = "Convert proof cameras to Bevy f32; for_view checks matrix agreement"
)]
fn temporal_input(matrix: [f64; 16]) -> TemporalInput {
    TemporalInput {
        world_from_view: Mat4::from_cols_array(&matrix.map(|v| v as f32)),
        clip_from_view: Mat4::perspective_rh(1.0, 1.0, 0.1, 1000.0),
        input_size: UVec2::splat(16),
        output_size: UVec2::splat(16),
        near: 0.1,
        far: 1000.0,
    }
}

fn decode_hex(hex: &str) -> Result<Vec<u8>, Box<dyn Error>> {
    if hex.len() != PACKET_BYTES * 2 || !hex.is_ascii() {
        return Err("source hex packet length differs".into());
    }
    hex.as_bytes()
        .as_chunks::<2>()
        .0
        .iter()
        .map(|pair| {
            let hi = char::from(pair[0])
                .to_digit(16)
                .ok_or("invalid hex digit")?;
            let lo = char::from(pair[1])
                .to_digit(16)
                .ok_or("invalid hex digit")?;
            Ok(u8::try_from(hi * 16 + lo)?)
        })
        .collect()
}

fn renderer_app() -> Result<App, Box<dyn Error>> {
    let mut app = App::new();
    app.add_plugins(
        DefaultPlugins
            .set(WindowPlugin {
                primary_window: None,
                exit_condition: ExitCondition::DontExit,
                ..default()
            })
            .disable::<WinitPlugin>()
            .disable::<PipelinedRenderingPlugin>(),
    );
    let deadline = Instant::now() + Duration::from_secs(30);
    while app.plugins_state() != PluginsState::Ready {
        if Instant::now() >= deadline {
            return Err("Bevy renderer initialization timed out".into());
        }
        std::thread::sleep(Duration::from_millis(10));
    }
    app.finish();
    app.cleanup();
    Ok(app)
}

fn readback(
    device: &RenderDevice,
    queue: &RenderQueue,
    input: &Buffer,
) -> Result<Vec<u8>, Box<dyn Error>> {
    let output = device.create_buffer(&BufferDescriptor {
        label: Some("source local-light proof readback"),
        size: 448,
        usage: BufferUsages::COPY_DST | BufferUsages::MAP_READ,
        mapped_at_creation: false,
    });
    let mut encoder = device.create_command_encoder(&CommandEncoderDescriptor::default());
    encoder.copy_buffer_to_buffer(input, 0, &output, 0, 448);
    queue.submit([encoder.finish()]);
    let (send, receive) = std::sync::mpsc::channel();
    output.slice(..).map_async(MapMode::Read, move |result| {
        let _ = send.send(result);
    });
    let deadline = Instant::now() + Duration::from_secs(30);
    loop {
        device.poll(wgpu::PollType::Poll)?;
        if let Ok(result) = receive.try_recv() {
            result?;
            break;
        }
        if Instant::now() >= deadline {
            return Err("GPU buffer map timed out".into());
        }
        std::thread::sleep(Duration::from_millis(2));
    }
    let mapped = output.slice(..).get_mapped_range();
    let bytes = mapped.to_vec();
    drop(mapped);
    output.unmap();
    Ok(bytes)
}
