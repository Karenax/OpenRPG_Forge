//! Original `ShoreSim` kernel on Bevy's GPU, from zero over two shore periods.
//! Inputs are immutable acquired terrain/parameters/lace; no source state is uploaded.
#![allow(clippy::needless_pass_by_value, clippy::too_many_lines)]
use bevy::{
    app::{AppExit, ScheduleRunnerPlugin},
    asset::RenderAssetUsages,
    core_pipeline::schedule::camera_driver,
    diagnostic::FrameCount,
    prelude::*,
    render::{
        RenderApp,
        gpu_readback::{Readback, ReadbackComplete},
        render_asset::RenderAssets,
        renderer::{RenderContext, RenderDevice, RenderGraph, RenderQueue},
        storage::{GpuShaderBuffer, ShaderBuffer},
        texture::GpuImage,
    },
    window::ExitCondition,
    winit::WinitPlugin,
};
use serde_json::Value;
use std::{
    collections::{BTreeMap, BTreeSet},
    error::Error,
    fs,
    io::Write,
    path::{Path, PathBuf},
    sync::{
        Arc,
        atomic::{AtomicBool, Ordering},
    },
    time::Duration,
};
const SNAPSHOTS: [u32; 5] = [0, 1, 120, 540, 1080];
#[allow(dead_code)]
mod authored;
#[allow(unused_imports)]
pub use authored::AuthoredTerrain;
const STATE_BYTES: usize = 768 * 768 * 8;
const DEPOSIT_BYTES: usize = 768 * 768 * 4;
#[derive(Resource)]
struct Input {
    frames: u32,
    files: BTreeMap<String, Vec<u8>>,
}
#[derive(Resource, Clone)]
pub struct Targets {
    buffers: Vec<Handle<ShaderBuffer>>,
    pub state: Handle<Image>,
    pub ready: Arc<AtomicBool>,
    pub readback_complete: Arc<AtomicBool>,
}
#[derive(Resource)]
struct Output {
    root: PathBuf,
    requested: bool,
    received: BTreeSet<usize>,
    last_frame: u32,
    exit_on_complete: bool,
}
#[derive(Component)]
struct Item(usize);
fn name(i: usize) -> String {
    format!(
        "frame-{:04}-{}",
        SNAPSHOTS[i / 2],
        if i.is_multiple_of(2) {
            "state.rgba16.bin"
        } else {
            "deposit.u32.bin"
        }
    )
}
fn load(root: &Path, kind: &str, frames: u32) -> Result<Input, Box<dyn Error>> {
    let meta: Value = serde_json::from_slice(&fs::read(root.join("shore-sim.json"))?)?;
    if (kind != "simulation" && meta["probe_kind"] != kind)
        || (kind == "simulation"
            && !meta["probe_kind"].is_null()
            && meta["probe_kind"] != "simulation")
    {
        return Err("simulation/diagnostic mode mismatch".into());
    }
    if meta["schema"] != "forge.tidewater.shore-sim"
        || meta["version"] != 1
        || meta["source_commit"] != "1438b1abfcaee3267092b75573014f4d9b4a983c"
        || meta["resolution"] != 768
        || meta["steps"] != 1080
        || meta["initial_state"] != "zero"
        || meta["deposit_schedule"] != serde_json::json!([])
        || meta["direction_mode"] != "original_terrain_fallback"
    {
        return Err("incompatible shore simulation fixture".into());
    }
    let mut files = BTreeMap::new();
    for n in [
        "kernel.wgsl",
        "heights.f32.bin",
        "shore-field.rgba32.bin",
        "shore-direction.rgba32.bin",
        "shore-params.bin",
        "simulation-params.bin",
        "terrain-params.bin",
    ]
    .into_iter()
    .map(str::to_owned)
    .chain((0..10).map(|i| format!("lace-m{i}.rgba8.bin")))
    {
        let data = fs::read(root.join(&n))?;
        if data.len() > 20_000_000
            || meta["files"][&n] != forge_core::bytes_sha256(&data).to_string()
        {
            return Err(format!("fixture integrity: {n}").into());
        }
        files.insert(n, data);
    }
    for (n, len) in [
        ("heights.f32.bin", 2048 * 2048 * 4),
        ("shore-field.rgba32.bin", 512 * 512 * 16),
        ("shore-direction.rgba32.bin", 16),
        ("shore-params.bin", 64),
        ("simulation-params.bin", 48),
        ("terrain-params.bin", 32),
    ] {
        if files[n].len() != len {
            return Err(format!("fixture length: {n}: {} expected {len}", files[n].len()).into());
        }
    }
    for i in 0..10 {
        if files[&format!("lace-m{i}.rgba8.bin")].len() != (512 >> i) * (512 >> i) * 4 {
            return Err("lace mip size".into());
        }
    }
    let original = std::str::from_utf8(&files["kernel.wgsl"])?;
    let hash_definition =
        "fn shoreHash1( x: f32 ) -> f32 { return fract( sin( x * 127.1 + 311.7 ) * 43758.5453 ); }";
    if original.matches(hash_definition).count() != 1 {
        return Err("original shore hash definition changed".into());
    }
    let adapted = original.replace(
        hash_definition,
        forge_engine::source_shore_sim::SHORE_HASH_SOURCE,
    );
    files.insert("kernel.wgsl".into(), adapted.into_bytes());
    Ok(Input { frames, files })
}
#[allow(dead_code)] // Headless wrapper; scenes use install.
pub fn run() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args_os().skip(1).collect();
    if !(2..=3).contains(&args.len()) {
        return Err(
            "SHORE_SIM_FIXTURE NEW_OUTPUT [1|120|540|1080|--phase-probe|--hash-probe] required"
                .into(),
        );
    }
    let mode = args.get(2).and_then(|a| a.to_str()).unwrap_or("1080");
    let (kind, frames) = match mode {
        "--phase-probe" => ("phase_diagnostic", 1080),
        "--hash-probe" => ("hash_diagnostic", 1080),
        _ => ("simulation", mode.parse::<u32>()?),
    };
    let output = PathBuf::from(&args[1]);
    if output.exists() {
        return Err("output exists".into());
    }
    if ![1, 120, 540, 1080].contains(&frames) {
        return Err("unsupported snapshot".into());
    }
    let input = load(Path::new(&args[0]), kind, frames)?;
    let mut app = App::new();
    app.add_plugins(
        DefaultPlugins
            .set(WindowPlugin {
                primary_window: None,
                exit_condition: ExitCondition::DontExit,
                ..default()
            })
            .disable::<WinitPlugin>(),
    )
    .add_plugins(ScheduleRunnerPlugin::run_loop(Duration::from_millis(16)));
    install_input(&mut app, input, Some(output), true)?;
    if app.run() != AppExit::Success {
        return Err("native shore simulation failed".into());
    }
    Ok(())
}
/// Share the computed state with scene materials on the existing Bevy device.
#[allow(dead_code)]
pub fn install(
    app: &mut App,
    root: &Path,
    frames: u32,
    output: Option<PathBuf>,
) -> Result<Targets, Box<dyn Error>> {
    if ![1, 120, 540, 1080].contains(&frames) {
        return Err("unsupported snapshot".into());
    }
    let input = load(root, "simulation", frames)?;
    install_input(app, input, output, false)
}
fn install_input(
    app: &mut App,
    input: Input,
    output: Option<PathBuf>,
    exit_on_complete: bool,
) -> Result<Targets, Box<dyn Error>> {
    if output.as_ref().is_some_and(|p| p.exists()) {
        return Err("output exists".into());
    }
    let mut buffers = vec![];
    for _ in SNAPSHOTS.into_iter().filter(|&frame| frame <= input.frames) {
        for bytes in [STATE_BYTES, DEPOSIT_BYTES] {
            let mut b = ShaderBuffer::from(vec![0_u32; bytes / 4]);
            b.buffer_description.usage |=
                wgpu::BufferUsages::COPY_DST | wgpu::BufferUsages::COPY_SRC;
            buffers.push(
                app.world_mut()
                    .resource_mut::<Assets<ShaderBuffer>>()
                    .add(b),
            );
        }
    }
    let mut state = Image::new_fill(
        wgpu::Extent3d {
            width: 768,
            height: 768,
            depth_or_array_layers: 1,
        },
        wgpu::TextureDimension::D2,
        &[0; 8],
        wgpu::TextureFormat::Rgba16Float,
        RenderAssetUsages::RENDER_WORLD,
    );
    state.texture_descriptor.usage = wgpu::TextureUsages::TEXTURE_BINDING
        | wgpu::TextureUsages::COPY_DST
        | wgpu::TextureUsages::COPY_SRC;
    let state = app.world_mut().resource_mut::<Assets<Image>>().add(state);
    let targets = Targets {
        buffers,
        state,
        readback_complete: Arc::new(AtomicBool::new(output.is_none())),
        ready: Arc::new(AtomicBool::new(false)),
    };
    app.insert_resource(targets.clone());
    app.sub_app_mut(RenderApp)
        .insert_resource(input)
        .insert_resource(targets.clone())
        .add_systems(
            RenderGraph,
            compute
                .in_set(forge_engine::source_water_schedule::ShoreComputeSet)
                .after(forge_engine::source_water_schedule::OceanComputeSet)
                .before(camera_driver),
        );
    if let Some(output) = output {
        fs::create_dir(&output)?;
        app.insert_resource(Output {
            root: output,
            requested: false,
            received: BTreeSet::new(),
            last_frame: 0,
            exit_on_complete,
        })
        .add_systems(Update, readback);
    }
    Ok(targets)
}

fn initialize(
    input: &Input,
    device: &wgpu::Device,
    queue: &RenderQueue,
    shared_deposit: Option<wgpu::Buffer>,
) -> Simulation {
    let delta = input
        .files
        .get("authored-height-deltas.bin")
        .map_or(&[0_u8; 4][..], Vec::as_slice);
    let gpu = forge_engine::source_shore_sim::GpuInputs {
        generation: None,
        kernel: std::str::from_utf8(&input.files["kernel.wgsl"]).expect("validated WGSL"),
        heights: &input.files["heights.f32.bin"],
        shore: &input.files["shore-field.rgba32.bin"],
        direction: &input.files["shore-direction.rgba32.bin"],
        lace: std::array::from_fn(|i| input.files[&format!("lace-m{i}.rgba8.bin")].as_slice()),
        simulation_params: &input.files["simulation-params.bin"],
        terrain_params: &input.files["terrain-params.bin"],
        shore_params: &input.files["shore-params.bin"],
        deltas: delta,
        layout: forge_engine::source_breaker_gpu::TerrainTextureLayout {
            heights: [2048, 2048],
            shore_side: 512,
            deltas: if delta.len() == 4 {
                [1, 1]
            } else {
                [2049, 2049]
            },
        },
        water_level: 0.0,
    };
    Simulation {
        gpu: forge_engine::source_shore_sim::initialize_gpu(&gpu, device, queue, shared_deposit)
            .expect("validated reference shore fits the render device"),
        next_step: 0,
        time: 0.0,
    }
}
struct Simulation {
    gpu: forge_engine::source_shore_sim::GpuSimulation,
    next_step: u32,
    time: f64,
}
#[allow(clippy::too_many_arguments)] // Render device, shared image and readback resources.
fn compute(
    mut context: RenderContext,
    input: Res<Input>,
    targets: Res<Targets>,
    render_device: Res<RenderDevice>,
    queue: Res<RenderQueue>,
    buffers: Res<RenderAssets<GpuShaderBuffer>>,
    mut simulation: Local<Option<Simulation>>,
    images: Res<RenderAssets<GpuImage>>,
) {
    if targets.ready.load(Ordering::Acquire) {
        return;
    }
    let gpu: Vec<_> = targets
        .buffers
        .iter()
        .filter_map(|h| buffers.get(h))
        .collect();
    if gpu.len() != targets.buffers.len() {
        return;
    }
    let Some(shared_state) = images.get(&targets.state) else {
        return;
    };
    let device = render_device.wgpu_device();
    let sim = simulation.get_or_insert_with(|| initialize(&input, device, &queue, None));
    let extent = wgpu::Extent3d {
        width: 768,
        height: 768,
        depth_or_array_layers: 1,
    };
    // Four fixed steps per render submission; preserve state and bounded GPU work.
    let end = (sim.next_step + 4).min(input.frames + 1);
    for step in sim.next_step..end {
        if step > 0 {
            sim.time += 1.0 / 60.0;
            // Only time, dt, seaLevel are consumed by the compute closure.
            // Use native packed fields and distinct allocations to avoid aliasing.
            let mut frame = [0_u8; 720];
            frame[576..580].copy_from_slice(&step.to_le_bytes());
            #[allow(clippy::cast_possible_truncation)]
            frame[580..584].copy_from_slice(&(sim.time as f32).to_le_bytes());
            frame[584..588].copy_from_slice(&(1.0_f32 / 60.0).to_le_bytes());
            sim.gpu
                .encode_frame(device, context.command_encoder(), &frame);
        }
        if let Some(i) = SNAPSHOTS.iter().position(|&v| v == step) {
            context.command_encoder().copy_texture_to_buffer(
                sim.gpu.state_texture().as_image_copy(),
                wgpu::TexelCopyBufferInfo {
                    buffer: &gpu[i * 2].buffer,
                    layout: wgpu::TexelCopyBufferLayout {
                        offset: 0,
                        bytes_per_row: Some(768 * 8),
                        rows_per_image: Some(768),
                    },
                },
                extent,
            );
            context.command_encoder().copy_buffer_to_buffer(
                sim.gpu.deposit_buffer(),
                0,
                &gpu[i * 2 + 1].buffer,
                0,
                DEPOSIT_BYTES as u64,
            );
        }
    }
    sim.next_step = end;
    if end == input.frames + 1 {
        // GPU-only publication: scene materials never consume CPU oracle states.
        context.command_encoder().copy_texture_to_texture(
            sim.gpu.state_texture().as_image_copy(),
            shared_state.texture.as_image_copy(),
            extent,
        );
        // The final diagnostic reads the public image, proving publication as
        // well as the internal computation. Intermediate snapshots stay intact.
        context.command_encoder().copy_texture_to_buffer(
            shared_state.texture.as_image_copy(),
            wgpu::TexelCopyBufferInfo {
                buffer: &gpu[gpu.len() - 2].buffer,
                layout: wgpu::TexelCopyBufferLayout {
                    offset: 0,
                    bytes_per_row: Some(768 * 8),
                    rows_per_image: Some(768),
                },
            },
            extent,
        );
        targets.ready.store(true, Ordering::Release);
    }
}
fn readback(
    mut commands: Commands,
    targets: Res<Targets>,
    mut output: ResMut<Output>,
    frame: Res<FrameCount>,
    mut exit: MessageWriter<AppExit>,
) {
    if output.received.len() == targets.buffers.len() {
        if output.exit_on_complete && frame.0 >= output.last_frame + 8 {
            exit.write(AppExit::Success);
        }
        return;
    }
    if frame.0 > 600 {
        exit.write(AppExit::error());
        return;
    }
    if output.requested || !targets.ready.load(Ordering::Acquire) {
        return;
    }
    output.requested = true;
    for (i, buffer) in targets.buffers.iter().enumerate() {
        commands
            .spawn((Readback::buffer(buffer.clone()), Item(i)))
            .observe(
                |event: On<ReadbackComplete>,
                 items: Query<&Item>,
                 targets: Res<Targets>,
                 mut output: ResMut<Output>,
                 frame: Res<FrameCount>,
                 mut commands: Commands,
                 mut exit: MessageWriter<AppExit>| {
                    let Ok(item) = items.get(event.entity) else {
                        return;
                    };
                    if !output.received.insert(item.0) {
                        return;
                    }
                    let bytes = if item.0 % 2 == 0 {
                        STATE_BYTES
                    } else {
                        DEPOSIT_BYTES
                    };
                    let result = (|| -> std::io::Result<()> {
                        if event.data.len() != bytes {
                            return Err(std::io::Error::other("shore readback size"));
                        }
                        fs::OpenOptions::new()
                            .write(true)
                            .create_new(true)
                            .open(output.root.join(name(item.0)))?
                            .write_all(&event.data)
                    })();
                    if let Err(e) = result {
                        eprintln!("{e}");
                        exit.write(AppExit::error());
                    } else {
                        println!(
                            "{} sha256 {}",
                            name(item.0),
                            forge_core::bytes_sha256(&event.data)
                        );
                    }
                    if output.received.len() == targets.buffers.len() {
                        targets.readback_complete.store(true, Ordering::Release);
                    }
                    output.last_frame = frame.0;
                    commands.entity(event.entity).despawn();
                },
            );
    }
}

/// Immutable, validated shore inputs for a caller-owned simulation clock.
#[allow(dead_code)]
pub struct CoupledInput(Input, String);
#[allow(dead_code)]
impl CoupledInput {
    pub fn apply_authored(&mut self, terrain: &AuthoredTerrain) -> Result<(), Box<dyn Error>> {
        terrain.apply(&mut self.0.files, &["kernel.wgsl"])
    }
    pub fn manifest_sha256(&self) -> &str {
        &self.1
    }
    pub fn load(
        root: &Path,
        spray_files: &BTreeMap<String, Vec<u8>>,
    ) -> Result<Self, Box<dyn Error>> {
        let input = load(root, "simulation", 120)?;
        for (shore_name, spray_name) in [
            ("heights.f32.bin", "heights.f32.bin"),
            ("shore-field.rgba32.bin", "shore-field.rgba32.bin"),
            ("shore-params.bin", "shore-params.bin"),
            ("terrain-params.bin", "terrain-params.bin"),
            ("simulation-params.bin", "shore-sim-params.bin"),
        ] {
            if input.files[shore_name] != spray_files[spray_name] {
                return Err(format!("coupled shore input mismatch: {shore_name}").into());
            }
        }
        Ok(Self(
            input,
            forge_core::bytes_sha256(&fs::read(root.join("shore-sim.json"))?).to_string(),
        ))
    }
    pub fn initialize(
        &self,
        device: &wgpu::Device,
        queue: &RenderQueue,
        deposit: wgpu::Buffer,
    ) -> forge_engine::source_shore_sim::OrderedSimulation {
        let simulation = initialize(&self.0, device, queue, Some(deposit));
        forge_engine::source_shore_sim::OrderedSimulation::new(simulation.gpu, 0)
    }
}
/// Expose a caller-produced GPU state to existing shore materials, without another simulation.
#[allow(dead_code)]
pub fn install_external(
    app: &mut App,
    state: Handle<Image>,
    ready: Arc<AtomicBool>,
    readback_complete: Arc<AtomicBool>,
) {
    app.insert_resource(Targets {
        buffers: vec![],
        state,
        ready,
        readback_complete,
    });
}
