//! Original ocean spectrum kernels on Bevy's device, with native uniforms.
#![allow(clippy::needless_pass_by_value)]
use bevy::{
    app::{AppExit, ScheduleRunnerPlugin},
    asset::RenderAssetUsages,
    core_pipeline::schedule::camera_driver,
    diagnostic::FrameCount,
    prelude::*,
    render::{
        RenderApp,
        diagnostic::RecordDiagnostics,
        gpu_readback::{Readback, ReadbackComplete},
        render_asset::RenderAssets,
        renderer::{RenderContext, RenderDevice, RenderGraph, RenderGraphSystems},
        storage::{GpuShaderBuffer, ShaderBuffer},
        texture::GpuImage,
    },
    window::ExitCondition,
    winit::WinitPlugin,
};
use forge_engine::ocean_spectrum::{OceanFrame, OceanSpectrum};
use serde_json::Value;
use std::{
    collections::BTreeSet,
    error::Error,
    fs,
    io::Write,
    path::{Path, PathBuf},
    sync::{
        Arc,
        atomic::{AtomicBool, AtomicU32, Ordering},
    },
    time::Duration,
};
const NAMES: [&str; 9] = [
    "initSpectrum",
    "conjugate",
    "copyH0",
    "row",
    "column",
    "mipA-0",
    "mipA-1",
    "mipB-0",
    "mipB-1",
];
const TOTAL: usize = 256 * 256 * 4;
pub mod acquired;
/// Dependents may consume the published FFT images after this render-system set.
#[derive(SystemSet, Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub struct OceanComputeSet;
#[derive(Resource, Default)]
struct StepState {
    next: u32,
    time: f64,
}
/// Advance one simulation step per render submission, retaining GPU state.
#[allow(dead_code)]
pub fn enable_stepping(app: &mut App) {
    app.sub_app_mut(RenderApp).init_resource::<StepState>();
}

#[allow(
    dead_code,
    reason = "Clock controls and snapshots are used by the shared plant probes and clock tests; the static spectrum example only offers and acknowledges frames"
)]
pub mod live_clock;
#[derive(Resource, Clone, Default, Deref)]
pub struct LiveClock(live_clock::LiveClock);

#[derive(Resource)]
pub struct LiveCoupled;
#[derive(Resource)]
struct LiveReady(Arc<AtomicBool>);
#[allow(dead_code)]
pub fn enable_live_coupled(app: &mut App, ready: Arc<AtomicBool>) {
    enable_live(app);
    app.insert_resource(LiveReady(ready));
    app.sub_app_mut(RenderApp).insert_resource(LiveCoupled);
}
#[allow(dead_code)]
pub fn enable_live(app: &mut App) {
    enable_stepping(app);
    let clock = LiveClock::default();
    app.insert_resource(clock.clone())
        .add_systems(Update, live_clock);
    app.sub_app_mut(RenderApp).insert_resource(clock);
}

fn live_clock(
    time: Res<Time<Real>>,
    target: Res<Targets>,
    clock: Res<LiveClock>,
    gate: Option<Res<LiveReady>>,
) {
    if !target.readback_complete.load(Ordering::Acquire)
        || gate.is_some_and(|gate| !gate.0.load(Ordering::Acquire))
    {
        return;
    }
    // Tidewater's Engine clamps the RAF delta to 100 ms. Do not consume elapsed
    // time while the render graph still owns the prior transaction.
    clock.offer(time.elapsed_secs_f64());
}

#[derive(Resource)]
struct Input {
    shaders: Vec<String>,
    params: [u8; 224],
    frames: u32,
    acquired: Option<Arc<Vec<acquired::Step>>>,
    acquired_manifest: Option<String>,
}
#[derive(Resource, Clone)]
pub struct Targets {
    /// Last bootstrap step shared with coupled consumers before live continuation.
    pub bootstrap_steps: u32,
    /// Exact source uniforms for downstream coupled static bootstrap passes.
    pub acquired: Option<Arc<Vec<acquired::Step>>>,
    pub acquired_manifest: Option<String>,
    /// Most recently encoded simulation step; images may be consumed after `OceanComputeSet`.
    pub completed_steps: Arc<AtomicU32>,
    /// Upper step allowed by a coupled consumer; unlimited by default.
    pub permitted_step: Arc<AtomicU32>,
    buffers: Vec<Handle<ShaderBuffer>>,
    pub images: Vec<Handle<Image>>,
    #[allow(dead_code)] // Consumed by scene materials, not the headless wrapper.
    pub sizes: [Vec4; 4],
    #[allow(dead_code)] // Optical scene material; exact native packed parameter bits.
    pub parameters: [UVec4; 14],
    copies: Vec<CopyOutput>,
    pub ready: Arc<AtomicBool>,
    pub readback_complete: Arc<AtomicBool>,
}
#[derive(Resource)]
struct Output {
    root: PathBuf,
    requested: bool,
    completed: usize,
    last_frame: u32,
    received: BTreeSet<usize>,
    exit_on_complete: bool,
}
#[derive(Component)]
struct Item(usize);
#[derive(Clone)]
struct CopyOutput {
    buffer: usize,
    name: String,
    bytes: usize,
    row: usize,
    padded_row: usize,
}
fn checked(root: &Path, meta: &Value, name: &str) -> Result<Vec<u8>, Box<dyn Error>> {
    let bytes = fs::read(root.join(name))?;
    if bytes.len() > 1_000_000
        || meta["files"][name] != forge_core::bytes_sha256(&bytes).to_string()
    {
        return Err("ocean fixture integrity failure".into());
    }
    Ok(bytes)
}
#[allow(dead_code)]
pub fn run() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args_os().skip(1).collect();
    if !((2..=3).contains(&args.len()) || (args.len() == 4 && args[3] == "--stepped")) {
        return Err("OCEAN_FIXTURE NEW_OUTPUT_DIRECTORY [1|60|120 [--stepped]] required".into());
    }
    let frames = if args.len() >= 3 {
        args[2].to_str().ok_or("invalid frames")?.parse::<u32>()?
    } else {
        0
    };
    if ![0, 1, 60, 120].contains(&frames) {
        return Err("supported snapshots: 1, 60, 120".into());
    }
    let root = Path::new(&args[0]);
    let output = PathBuf::from(&args[1]);
    if output.exists() {
        return Err("output already exists".into());
    }
    let input = load(root, frames)?;
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
    let targets = create_targets(&mut app, frames, &input.params);
    app.insert_resource(targets.clone());
    app.sub_app_mut(RenderApp)
        .insert_resource(input)
        .insert_resource(targets)
        .add_systems(
            RenderGraph,
            compute
                .in_set(OceanComputeSet)
                .in_set(RenderGraphSystems::Render)
                .before(camera_driver),
        );
    if args.len() == 4 {
        enable_stepping(&mut app);
    }
    fs::create_dir(&output)?;
    app.insert_resource(Output {
        root: output,
        requested: false,
        completed: 0,
        last_frame: 0,
        received: BTreeSet::new(),
        exit_on_complete: true,
    })
    .add_systems(Update, readback);
    if app.run() != AppExit::Success {
        return Err("native ocean spectrum failed".into());
    }
    Ok(())
}

/// Install the same numerical graph in a scene, sharing its device and images.
/// The optional diagnostic readback never exits the scene on success.
#[allow(dead_code)]
pub fn install(
    app: &mut App,
    root: &Path,
    frames: u32,
    output: Option<PathBuf>,
) -> Result<Targets, Box<dyn Error>> {
    if ![1, 60, 120].contains(&frames) {
        return Err("supported scene snapshots: 1, 60, 120".into());
    }
    if output.as_ref().is_some_and(|p| p.exists()) {
        return Err("ocean output already exists".into());
    }
    let input = match acquired::load()? {
        Some(input) => input,
        None => load(root, frames)?,
    };
    let mut targets = create_targets(app, input.frames, &input.params);
    targets.acquired.clone_from(&input.acquired);
    targets
        .acquired_manifest
        .clone_from(&input.acquired_manifest);
    app.insert_resource(targets.clone());
    app.sub_app_mut(RenderApp)
        .insert_resource(input)
        .insert_resource(targets.clone())
        .add_systems(
            RenderGraph,
            compute
                .in_set(OceanComputeSet)
                .in_set(RenderGraphSystems::Render)
                .before(camera_driver),
        );
    if output.is_none() {
        targets.readback_complete.store(true, Ordering::Release);
    }
    if let Some(root) = output {
        fs::create_dir(&root)?;
        app.insert_resource(Output {
            root,
            requested: false,
            completed: 0,
            last_frame: 0,
            received: BTreeSet::new(),
            exit_on_complete: false,
        })
        .add_systems(Update, readback);
    }
    Ok(targets)
}

fn load(root: &Path, frames: u32) -> Result<Input, Box<dyn Error>> {
    let meta: Value = serde_json::from_slice(&fs::read(root.join("ocean.json"))?)?;
    if meta["schema"] != "forge.tidewater.ocean-probe"
        || meta["version"] != 1
        || meta["source_commit"] != "1438b1abfcaee3267092b75573014f4d9b4a983c"
        || meta["cascades"] != 4
        || meta["fft_size"] != 256
    {
        return Err("invalid ocean fixture".into());
    }
    let options: OceanSpectrum = meta
        .get("simulation_options")
        .map(|v| serde_json::from_value(v.clone()))
        .transpose()?
        .unwrap_or_default();
    let frame: OceanFrame = meta
        .get("simulation_frame")
        .map(|v| serde_json::from_value(v.clone()))
        .transpose()?
        .unwrap_or_default();
    if options.cascades != 4 {
        return Err("four cascade probe required".into());
    }
    let params = options.uniform_bytes(frame)?;
    if params.as_slice() != checked(root, &meta, "params-initial.bin")? {
        return Err("native ocean uniforms differ".into());
    }
    if frames > 0 {
        let source_frame = checked(root, &meta, &format!("frame-{frames:03}-frame.bin"))?;
        if source_frame.len() != 720 || source_frame[584..588] != (1.0_f32 / 60.0).to_le_bytes() {
            return Err("source Frame layout or dt changed".into());
        }
        let mut expected = params;
        let mut time = 0.0_f64;
        for _ in 0..frames {
            time += 1.0 / 60.0;
        }
        #[allow(clippy::cast_possible_truncation)]
        expected[212..216].copy_from_slice(&(time as f32).to_le_bytes());
        if expected.as_slice() != checked(root, &meta, &format!("frame-{frames:03}-params.bin"))? {
            return Err("native step parameters differ".into());
        }
    }
    let shaders = NAMES
        .into_iter()
        .map(|n| {
            Ok(String::from_utf8(checked(
                root,
                &meta,
                &format!("{n}.wgsl"),
            )?)?)
        })
        .collect::<Result<Vec<_>, Box<dyn Error>>>()?;
    Ok(Input {
        shaders,
        params,
        frames,
        acquired: None,
        acquired_manifest: None,
    })
}
#[allow(clippy::too_many_lines)] // Shared allocation of six work buffers and all mip readbacks.
fn create_targets(app: &mut App, frames: u32, params: &[u8; 224]) -> Targets {
    let mut buffers = vec![];
    // h0, waveData, scratch, foam, mipSrc, mipMid; all initially zero.
    for count in [
        TOTAL * 4,
        TOTAL * 4,
        TOTAL * 8,
        TOTAL,
        TOTAL * 8,
        64 * 4 * 8,
    ] {
        let mut buffer = ShaderBuffer::from(vec![0.0_f32; count]);
        buffer.buffer_description.usage |= wgpu::BufferUsages::COPY_SRC;
        buffers.push(
            app.world_mut()
                .resource_mut::<Assets<ShaderBuffer>>()
                .add(buffer),
        );
    }
    let mut copies = vec![
        CopyOutput {
            buffer: 0,
            name: "h0.f32.bin".into(),
            bytes: TOTAL * 16,
            row: TOTAL * 16,
            padded_row: TOTAL * 16,
        },
        CopyOutput {
            buffer: 1,
            name: "waveData.f32.bin".into(),
            bytes: TOTAL * 16,
            row: TOTAL * 16,
            padded_row: TOTAL * 16,
        },
    ];
    let mut images = vec![];
    if frames > 0 {
        copies.push(CopyOutput {
            buffer: 3,
            name: "foam.f32.bin".into(),
            bytes: TOTAL * 4,
            row: TOTAL * 4,
            padded_row: TOTAL * 4,
        });
        for name in ["displacement", "derivatives"] {
            let mut image = Image::new_uninit(
                wgpu::Extent3d {
                    width: 256,
                    height: 256,
                    depth_or_array_layers: 4,
                },
                wgpu::TextureDimension::D2,
                wgpu::TextureFormat::Rgba16Float,
                RenderAssetUsages::RENDER_WORLD,
            );
            image.sampler =
                bevy::image::ImageSampler::Descriptor(bevy::image::ImageSamplerDescriptor {
                    address_mode_u: bevy::image::ImageAddressMode::Repeat,
                    address_mode_v: bevy::image::ImageAddressMode::Repeat,
                    min_filter: bevy::image::ImageFilterMode::Linear,
                    mag_filter: bevy::image::ImageFilterMode::Linear,
                    mipmap_filter: bevy::image::ImageFilterMode::Linear,
                    ..default()
                });
            image.texture_descriptor.mip_level_count = 9;
            image.texture_descriptor.usage = wgpu::TextureUsages::STORAGE_BINDING
                | wgpu::TextureUsages::TEXTURE_BINDING
                | wgpu::TextureUsages::COPY_SRC;
            images.push(app.world_mut().resource_mut::<Assets<Image>>().add(image));
            for mip in 0..9 {
                let side = 256_usize >> mip;
                let row = side * 8;
                let padded_row = row.div_ceil(256) * 256;
                let bytes = padded_row * side * 4;
                let mut buffer = ShaderBuffer::from(vec![0_u32; bytes / 4]);
                buffer.buffer_description.usage |=
                    wgpu::BufferUsages::COPY_DST | wgpu::BufferUsages::COPY_SRC;
                copies.push(CopyOutput {
                    buffer: buffers.len(),
                    name: format!("{name}-m{mip}.rgba16.bin"),
                    bytes,
                    row,
                    padded_row,
                });
                buffers.push(
                    app.world_mut()
                        .resource_mut::<Assets<ShaderBuffer>>()
                        .add(buffer),
                );
            }
        }
    }
    Targets {
        bootstrap_steps: frames,
        acquired: None,
        acquired_manifest: None,
        completed_steps: Arc::new(AtomicU32::new(0)),
        permitted_step: Arc::new(AtomicU32::new(u32::MAX)),
        sizes: std::array::from_fn(|i| {
            Vec4::new(
                f32::from_le_bytes(params[i * 16..i * 16 + 4].try_into().expect("sizes")),
                0.0,
                0.0,
                0.0,
            )
        }),
        parameters: std::array::from_fn(|i| {
            UVec4::from_array(std::array::from_fn(|j| {
                u32::from_le_bytes(
                    params[i * 16 + j * 4..i * 16 + j * 4 + 4]
                        .try_into()
                        .expect("parameter word"),
                )
            }))
        }),
        buffers,
        images,
        copies,
        ready: Arc::new(AtomicBool::new(false)),
        readback_complete: Arc::new(AtomicBool::new(false)),
    }
}
#[allow(clippy::too_many_lines, clippy::too_many_arguments)]
fn compute(
    mut context: RenderContext,
    input: Res<Input>,
    targets: Res<Targets>,
    device: Res<RenderDevice>,
    buffers: Res<RenderAssets<GpuShaderBuffer>>,
    images: Res<RenderAssets<GpuImage>>,
    mut stepping: Option<ResMut<StepState>>,
    live: Option<Res<LiveClock>>,
    coupled_live: Option<Res<LiveCoupled>>,
    mut cached: Local<Option<Vec<bevy::render::render_resource::ComputePipeline>>>,
) {
    let was_ready = targets.ready.load(Ordering::Acquire);
    if was_ready && (live.is_none() || !targets.readback_complete.load(Ordering::Acquire)) {
        return;
    }
    let gpu: Vec<_> = targets
        .buffers
        .iter()
        .filter_map(|h| buffers.get(h))
        .collect();
    let textures: Vec<_> = targets
        .images
        .iter()
        .filter_map(|h| images.get(h))
        .collect();
    if gpu.len() != targets.buffers.len() || textures.len() != targets.images.len() {
        return;
    }
    let views: Vec<Vec<_>> = textures
        .iter()
        .map(|t| {
            (0..9)
                .map(|mip| {
                    t.texture.create_view(&wgpu::TextureViewDescriptor {
                        dimension: Some(wgpu::TextureViewDimension::D2Array),
                        base_mip_level: mip,
                        mip_level_count: Some(1),
                        ..default()
                    })
                })
                .collect()
        })
        .collect();
    let count = if input.frames == 0 { 3 } else { 9 };
    let pipelines = cached.get_or_insert_with(|| {
        input.shaders[..count]
            .iter()
            .enumerate()
            .map(|(index, source)| {
                let shader =
                    device.create_and_validate_shader_module(wgpu::ShaderModuleDescriptor {
                        label: Some(NAMES[index]),
                        source: wgpu::ShaderSource::Wgsl(source.as_str().into()),
                    });
                device.create_compute_pipeline(&wgpu::ComputePipelineDescriptor {
                    label: Some(NAMES[index]),
                    layout: None,
                    module: &shader,
                    entry_point: Some("main"),
                    compilation_options: default(),
                    cache: None,
                })
            })
            .collect()
    });
    // Only frame.dt is read by these kernels. Static acquisition retains its
    // fixed source snapshot; live runs exactly one offered source-frame update.
    let mut time = stepping.as_ref().map_or(0.0, |s| s.time);
    let first = stepping.as_ref().map_or(0, |s| s.next);
    if (!was_ready || coupled_live.is_some())
        && stepping.is_some()
        && first > targets.permitted_step.load(Ordering::Acquire)
    {
        return;
    }
    let live_step = if was_ready {
        let Some(frame) = live.as_ref().expect("live continuation").pending(first) else {
            return;
        };
        Some(frame)
    } else {
        None
    };
    let last = if was_ready {
        first
    } else if stepping.is_some() {
        first
            .max(1)
            .min(input.frames)
            .min(targets.permitted_step.load(Ordering::Acquire))
    } else {
        input.frames
    };
    let dt = live_step.map_or(1.0_f32 / 60.0, |frame| frame.dt);
    let diagnostics = context.diagnostic_recorder();
    let recorder = diagnostics.as_deref();
    let compute_span = recorder.time_span(context.command_encoder(), "tidewater_ocean");
    for step in first..=last {
        let mut params = input.params;
        let mut frame_bytes = [0_u8; 720];
        frame_bytes[584..588].copy_from_slice(&dt.to_le_bytes());
        if let Some(frame) = live_step {
            debug_assert_eq!(step, frame.step);
            time = frame.time;
        } else if step > 0
            && let Some(acquired_frames) = &input.acquired
        {
            let acquired = &acquired_frames[step as usize - 1];
            params = acquired.params;
            frame_bytes = acquired.global;
            time = f64::from(f32::from_le_bytes(
                params[212..216].try_into().expect("ocean time"),
            ));
        } else if step > 0 {
            time += 1.0 / 60.0;
        }
        #[allow(clippy::cast_possible_truncation)]
        params[212..216].copy_from_slice(&(time as f32).to_le_bytes());
        let frame_uniform = device.create_buffer_with_data(&wgpu::util::BufferInitDescriptor {
            label: Some("native ocean frame dt"),
            contents: &frame_bytes,
            usage: wgpu::BufferUsages::UNIFORM,
        });
        let uniform = device.create_buffer_with_data(&wgpu::util::BufferInitDescriptor {
            label: Some("native ocean step"),
            contents: &params,
            usage: wgpu::BufferUsages::UNIFORM,
        });
        let range = if step == 0 { 0..3 } else { 3..9 };
        for index in range {
            let pipeline = &pipelines[index];
            let frame_entries = if index == 4 {
                vec![wgpu::BindGroupEntry {
                    binding: 0,
                    resource: frame_uniform.as_entire_binding(),
                }]
            } else {
                vec![]
            };
            let group0 = device.create_bind_group(
                NAMES[index],
                &pipeline.get_bind_group_layout(0).into(),
                &frame_entries,
            );
            let buffer_binding = |i: usize| gpu[i].buffer.as_entire_binding();
            let texture_binding =
                |tex: usize, mip: usize| wgpu::BindingResource::TextureView(&views[tex][mip]);
            let resources = match index {
                0 => vec![
                    uniform.as_entire_binding(),
                    buffer_binding(0),
                    buffer_binding(1),
                ],
                1 => vec![buffer_binding(0), buffer_binding(2)],
                2 => vec![buffer_binding(0), buffer_binding(2), buffer_binding(3)],
                3 => vec![
                    uniform.as_entire_binding(),
                    buffer_binding(0),
                    buffer_binding(1),
                    buffer_binding(2),
                ],
                4 => vec![
                    uniform.as_entire_binding(),
                    buffer_binding(2),
                    buffer_binding(3),
                    buffer_binding(4),
                    texture_binding(0, 0),
                    texture_binding(1, 0),
                ],
                5 | 6 => {
                    let tex = index - 5;
                    vec![
                        buffer_binding(4),
                        buffer_binding(5),
                        texture_binding(tex, 1),
                        texture_binding(tex, 2),
                        texture_binding(tex, 3),
                        texture_binding(tex, 4),
                        texture_binding(tex, 5),
                    ]
                }
                _ => {
                    let tex = index - 7;
                    vec![
                        buffer_binding(5),
                        texture_binding(tex, 6),
                        texture_binding(tex, 7),
                        texture_binding(tex, 8),
                    ]
                }
            };
            let entries: Vec<_> = resources
                .into_iter()
                .enumerate()
                .map(|(i, resource)| wgpu::BindGroupEntry {
                    binding: u32::try_from(i).expect("small binding index"),
                    resource,
                })
                .collect();
            let group1 = device.create_bind_group(
                NAMES[index],
                &pipeline.get_bind_group_layout(1).into(),
                &entries,
            );
            let mut pass =
                context
                    .command_encoder()
                    .begin_compute_pass(&wgpu::ComputePassDescriptor {
                        label: Some(NAMES[index]),
                        ..default()
                    });
            pass.set_pipeline(pipeline);
            pass.set_bind_group(0, &*group0, &[]);
            pass.set_bind_group(1, &*group1, &[]);
            let (x, y, z) = match index {
                0..=2 => (16, 16, 4),
                3 | 4 => (256, 4, 1),
                5 | 6 => (8, 8, 4),
                _ => (1, 1, 4),
            };
            pass.dispatch_workgroups(x, y, z);
        }
    }
    compute_span.end(context.command_encoder());
    targets.completed_steps.store(last, Ordering::Release);
    if let Some(state) = stepping.as_mut() {
        state.next = last + 1;
        state.time = time;
    }
    if was_ready && coupled_live.is_none() {
        live.expect("live continuation").acknowledge(last);
    }
    if was_ready {
        return;
    }
    if last < input.frames {
        return;
    }
    if input.frames > 0 {
        for (i, copy) in targets.copies[3..].iter().enumerate() {
            let mip = u32::try_from(i % 9).expect("mip");
            let side = 256 >> mip;
            context.command_encoder().copy_texture_to_buffer(
                wgpu::TexelCopyTextureInfo {
                    texture: &textures[i / 9].texture,
                    mip_level: mip,
                    origin: wgpu::Origin3d::ZERO,
                    aspect: wgpu::TextureAspect::All,
                },
                wgpu::TexelCopyBufferInfo {
                    buffer: &gpu[copy.buffer].buffer,
                    layout: wgpu::TexelCopyBufferLayout {
                        offset: 0,
                        bytes_per_row: Some(u32::try_from(copy.padded_row).expect("row")),
                        rows_per_image: Some(side),
                    },
                },
                wgpu::Extent3d {
                    width: side,
                    height: side,
                    depth_or_array_layers: 4,
                },
            );
        }
    }
    targets.ready.store(true, Ordering::Release);
}
fn readback(
    mut commands: Commands,
    targets: Res<Targets>,
    mut output: ResMut<Output>,
    frame: Res<FrameCount>,
    mut exit: MessageWriter<AppExit>,
) {
    if output.completed == targets.copies.len() {
        if output.exit_on_complete && frame.0 >= output.last_frame + 8 {
            exit.write(AppExit::Success);
        }
        return;
    }
    if output.requested || !targets.ready.load(Ordering::Acquire) {
        return;
    }
    output.requested = true;
    for (i, copy) in targets.copies.iter().enumerate() {
        commands
            .spawn((
                Readback::buffer(targets.buffers[copy.buffer].clone()),
                Item(i),
            ))
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
                    let i = item.0;
                    // Readback can have several submissions in flight before
                    // the despawn reaches the render world. Consume once.
                    if !output.received.insert(i) {
                        return;
                    }
                    let copy = &targets.copies[i];
                    if event.data.len() != copy.bytes {
                        eprintln!("ocean readback size mismatch");
                        exit.write(AppExit::error());
                        commands.entity(event.entity).despawn();
                        return;
                    }
                    let data: Vec<u8> = event
                        .data
                        .chunks(copy.padded_row)
                        .flat_map(|row| row[..copy.row].iter().copied())
                        .collect();
                    let result = (|| -> std::io::Result<()> {
                        fs::OpenOptions::new()
                            .write(true)
                            .create_new(true)
                            .open(output.root.join(&copy.name))?
                            .write_all(&data)
                    })();
                    match result {
                        Ok(()) => {
                            println!("{} sha256 {}", copy.name, forge_core::bytes_sha256(&data));
                            output.completed += 1;
                            if output.completed == targets.copies.len() {
                                targets.readback_complete.store(true, Ordering::Release);
                                info!(
                                    main_frame = frame.0,
                                    completed_steps =
                                        targets.completed_steps.load(Ordering::Acquire),
                                    readbacks = output.completed,
                                    bootstrap_frames = targets.bootstrap_steps,
                                    acquired_global_time_s = ?targets.acquired.as_ref().and_then(|steps| steps.last()).map(|step| step.global_time),
                                    acquired_spray_callback = ?targets.acquired.as_ref().and_then(|steps| steps.last()).map(|step| match step.feedback_order {
                                        acquired::CallbackOrder::LegacyMetadata => ("legacy_metadata", None),
                                        acquired::CallbackOrder::Unavailable => ("unavailable", None),
                                        acquired::CallbackOrder::Completed(frame) => ("completed", Some(frame)),
                                    }),
                                    "Ocean initialization complete"
                                );
                            }
                            output.last_frame = frame.0;
                        }
                        Err(e) => {
                            eprintln!("{e}");
                            exit.write(AppExit::error());
                        }
                    }
                    commands.entity(event.entity).despawn();
                },
            );
    }
}
