//! Original GPU spray emission isolated at frame one; reference particle outputs are never uploaded.
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
use forge_engine::spray_budget::{HeadSample, SprayBudget};
use std::{
    collections::{BTreeMap, BTreeSet},
    error::Error,
    fs,
    path::{Path, PathBuf},
    sync::{
        Arc, Mutex,
        atomic::{AtomicBool, Ordering},
    },
    time::Duration,
};
use wgpu::util::DeviceExt;
#[derive(Clone, Copy, PartialEq)]
enum BudgetMode {
    Recorded,
    Native,
}
struct BudgetFeedback {
    controller: SprayBudget,
    latest: Option<(u32, HeadSample)>,
    encoded_step: u32,
    encoded_time: f64,
    requested_step: u32,
    rows: Vec<serde_json::Value>,
}

#[derive(Resource)]
struct Input {
    budget_mode: BudgetMode,
    coupled: Option<super::shore_sim::CoupledInput>,
    history: bool,
    budgets: Vec<f32>,
    integrate: bool,
    replay: bool,
    files: BTreeMap<String, Vec<u8>>,
    stations: Vec<u8>,
    count: u32,
    steps: Vec<u32>,
}
#[derive(Resource, Clone)]
pub struct Targets {
    budget: Option<Arc<Mutex<BudgetFeedback>>>,
    pub shore_state: Option<Handle<Image>>,
    #[allow(dead_code)] // Consumed by the scene lip material, not the standalone compute example.
    pub stations: u32,
    pub buffers: Vec<Handle<ShaderBuffer>>,
    pub ready: Arc<AtomicBool>,
    pub readback_complete: Arc<AtomicBool>,
}
#[derive(PartialEq)]
enum Completion {
    Exit,
    Scene,
}
#[derive(Resource)]
struct Output {
    budget_report: Option<PathBuf>,
    completion: Completion,
    integrate: bool,
    replay: bool,
    step: u32,
    root: PathBuf,
    requested: bool,
    received: BTreeSet<usize>,
    last_frame: u32,
    bytes: usize,
}
#[derive(Component)]
struct Item(usize);

fn load(
    root: &Path,
    integrate: bool,
    step: u32,
    replay: bool,
    history: bool,
    budget_mode: BudgetMode,
) -> Result<Input, Box<dyn Error>> {
    let meta: serde_json::Value = serde_json::from_slice(&fs::read(root.join("spray.json"))?)?;
    if meta["schema"] != "forge.tidewater.spray"
        || meta["version"] != 1
        || meta["source_commit"] != "1438b1abfcaee3267092b75573014f4d9b4a983c"
        || meta["options"]
            != serde_json::json!({"spray":true,"clouds":false,"cpu_emitters":false,"body":false,"shore_deposit":true})
        || meta["capacities"] != serde_json::json!({"gpu":32768,"cpu":8192,"total":40960})
        || meta["terrain"] != serde_json::json!({"resolution":2048,"origin":-1024,"texel":1})
    {
        return Err("incompatible crest oracle".into());
    }
    let mut names: Vec<String> = [
        "kernel.wgsl",
        "heights.f32.bin",
        "stations.f32.bin",
        "shore-field.rgba32.bin",
        "shore-params.bin",
        "terrain-params.bin",
    ]
    .into_iter()
    .map(str::to_owned)
    .collect();
    if integrate {
        names.extend(
            [
                "spray-update.wgsl",
                "shore-sim-params.bin",
                "surface-params.bin",
            ]
            .map(str::to_owned),
        );
    }
    names.push(format!("frame-{step:04}-spray-params.bin"));
    if replay {
        for n in ["pos", "vel", "info", "head"] {
            names.push(format!("emitted-{step:04}-{n}.bin"));
        }
    }
    let steps = vec![step];
    for step in &steps {
        for suffix in ["frame.bin", "params.bin", "ocean.bin"] {
            names.push(format!("frame-{step:04}-{suffix}"));
        }
        for mip in 0..if history { 0 } else { 9 } {
            names.push(format!("frame-{step:04}-displacement-m{mip}.rgba16.bin"));
        }
    }
    let mut files = BTreeMap::new();
    for name in names {
        let data = fs::read(root.join(&name))?;
        if data.len() > 20_000_000
            || meta["files"][&name] != forge_core::bytes_sha256(&data).to_string()
        {
            return Err("crest input integrity".into());
        }
        files.insert(name, data);
    }
    for (name, size) in [
        ("heights.f32.bin", 2048 * 2048 * 4),
        ("shore-field.rgba32.bin", 512 * 512 * 16),
        ("shore-params.bin", 64),
        ("terrain-params.bin", 32),
    ] {
        if files[name].len() != size {
            return Err("crest input length".into());
        }
    }
    if files["shore-params.bin"][52..56] != 0_f32.to_le_bytes() {
        return Err("shore direction fallback required".into());
    }
    for step in &steps {
        for (suffix, size) in [("frame.bin", 720), ("params.bin", 32), ("ocean.bin", 224)] {
            if files[&format!("frame-{step:04}-{suffix}")].len() != size {
                return Err("crest uniform length".into());
            }
        }
        if files[&format!("frame-{step:04}-frame.bin")][700..704] != [0; 4] {
            return Err("Frame.pad0 must be zero".into());
        }
        for mip in 0..if history { 0 } else { 9 } {
            let data = &files[&format!("frame-{step:04}-displacement-m{mip}.rgba16.bin")];
            if data.len() != (256 >> mip) * (256 >> mip) * 4 * 8
                || data
                    .as_chunks::<2>()
                    .0
                    .iter()
                    .any(|v| u16::from_le_bytes(*v) & 0x7c00 == 0x7c00)
            {
                return Err("invalid displacement mip".into());
            }
        }
    }
    let heights: Vec<f64> = files["heights.f32.bin"]
        .as_chunks::<4>()
        .0
        .iter()
        .map(|v| f64::from(f32::from_le_bytes(*v)))
        .collect();
    if heights.iter().any(|v| !v.is_finite()) {
        return Err("nonfinite terrain".into());
    }
    let generated =
        forge_engine::breaker_geometry::beach_stations(|x, z| sample_height(&heights, x, z));
    let count = u32::try_from(generated.len())?;
    let stations: Vec<u8> = generated
        .iter()
        .flatten()
        .flat_map(|v| v.to_le_bytes())
        .collect();
    if !(2..=4096).contains(&count)
        || meta["stations"] != count
        || stations != files["stations.f32.bin"]
    {
        return Err("native beach differs".into());
    }
    let params = files
        .get(&format!("frame-{step:04}-spray-params.bin"))
        .expect("loaded spray params");
    if params.len() != 240 {
        return Err("spray parameter size".into());
    }
    if params[..4] != step.to_le_bytes() {
        return Err("unexpected effective emission seed".into());
    } // Both passes share the final uniform upload before submission.
    let original = std::str::from_utf8(&files["kernel.wgsl"])?;
    let hash =
        "fn shoreHash1( x: f32 ) -> f32 { return fract( sin( x * 127.1 + 311.7 ) * 43758.5453 ); }";
    if original.matches(hash).count() != 1 {
        return Err("shore hash source changed".into());
    }
    let adapted = original.replace(
        hash,
        include_str!("../../../../scripts/tidewater/native-shore-hash.wgsl"),
    );
    files.insert("kernel.wgsl".into(), adapted.into_bytes());
    if integrate {
        for (name, size) in [("shore-sim-params.bin", 48), ("surface-params.bin", 32)] {
            if files[name].len() != size {
                return Err("integration uniform size".into());
            }
        }
        let original = std::str::from_utf8(&files["spray-update.wgsl"])?;
        if original.matches(hash).count() != 1 {
            return Err("integration shore hash changed".into());
        }
        let adapted = original.replace(
            hash,
            include_str!("../../../../scripts/tidewater/native-shore-hash.wgsl"),
        );
        files.insert("spray-update.wgsl".into(), adapted.into_bytes());
    }
    if replay {
        for n in ["pos", "vel", "info", "head"] {
            let data = &files[&format!("emitted-{step:04}-{n}.bin")];
            if data.len() != if n == "head" { 4 } else { 40960 * 16 } {
                return Err("replay input size".into());
            }
            if n != "head"
                && data
                    .as_chunks::<4>()
                    .0
                    .iter()
                    .any(|v| !f32::from_le_bytes(*v).is_finite())
            {
                return Err("nonfinite replay particles".into());
            }
        }
    }
    let mut budgets = Vec::new();
    if history && budget_mode == BudgetMode::Recorded {
        for (i, s) in meta["schedule"]
            .as_array()
            .ok_or("schedule missing")?
            .iter()
            .take(120)
            .enumerate()
        {
            if s["frame"] != i + 1 || s["effective_gpu_seed"] != i + 1 {
                return Err("history schedule changed".into());
            }
            let value = s["emitter_budget"]
                .as_f64()
                .ok_or("effective emitter budget missing")?;
            if !(0.05..=1.0).contains(&value) {
                return Err("budget range".into());
            }
            #[allow(clippy::cast_possible_truncation)]
            budgets.push(value as f32);
        }
        if budgets.len() != 120 {
            return Err("history schedule size".into());
        }
    }
    Ok(Input {
        budget_mode,
        coupled: None,
        history,
        budgets,
        integrate,
        replay,
        files,
        stations,
        count,
        steps,
    })
}
#[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)] // Coordinates bounded before conversion.
fn sample_height(h: &[f64], x: f64, z: f64) -> f64 {
    let (fx, fz) = (x + 1024.0 - 0.5, z + 1024.0 - 0.5);
    if !(0.0..2047.0).contains(&fx) || !(0.0..2047.0).contains(&fz) {
        return -90.0;
    }
    let base = fz.floor() as usize * 2048 + fx.floor() as usize;
    let (tx, tz) = (fx - fx.floor(), fz - fz.floor());
    (h[base] * (1.0 - tx) + h[base + 1] * tx) * (1.0 - tz)
        + (h[base + 2048] * (1.0 - tx) + h[base + 2049] * tx) * tz
}
#[allow(dead_code)] // Also compiled as a shared scene module.
pub fn run() -> Result<(), Box<dyn Error>> {
    let mut args: Vec<_> = std::env::args_os().skip(1).collect();
    let budget_mode = if args.last().is_some_and(|v| v == "--native-budget") {
        args.pop();
        BudgetMode::Native
    } else {
        BudgetMode::Recorded
    };
    if !(args.len() == 2
        || (args.len() == 3 && args[2] == "--integrate")
        || (args.len() == 4 && (args[2] == "--replay-update" || args[2] == "--native-history"))
        || (args.len() == 6 && args[2] == "--native-history" && args[4] == "--coupled-shore"))
    {
        return Err(
            "SPRAY_EXPORT NEW_OUTPUT [--integrate | --replay-update 120|540 | --native-history OCEAN_EXPORT [--coupled-shore SHORE_EXPORT]] [--native-budget] required".into(),
        );
    }
    let root = PathBuf::from(&args[1]);
    if root.exists() {
        return Err("output exists".into());
    }
    let history = args.len() >= 4 && args[2] == "--native-history";
    let replay = args.len() == 4 && !history;
    let step = if replay {
        args[3].to_str().ok_or("step encoding")?.parse::<u32>()?
    } else {
        1
    };
    if replay && ![120, 540].contains(&step) {
        return Err("unsupported replay step".into());
    }
    if budget_mode == BudgetMode::Native && !history {
        return Err("native budget requires native history".into());
    }
    let mut input = load(
        Path::new(&args[0]),
        args.len() >= 3,
        step,
        replay,
        history,
        budget_mode,
    )?;
    if args.len() == 6 {
        input.coupled = Some(super::shore_sim::CoupledInput::load(
            Path::new(&args[5]),
            &input.files,
        )?);
    }
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
    if history {
        let fft = super::ocean::install(
            &mut app,
            Path::new(&args[3]),
            120,
            Some(root.with_extension("ocean")),
        )?;
        let mut params: Vec<u8> = fft
            .parameters
            .iter()
            .flat_map(UVec4::to_array)
            .flat_map(u32::to_le_bytes)
            .collect();
        params[212..216].copy_from_slice(&input.files["frame-0001-ocean.bin"][212..216]);
        if params != input.files["frame-0001-ocean.bin"] {
            return Err("native ocean options differ from spray fixture".into());
        }
        fft.permitted_step.store(1, Ordering::Release);
        super::ocean::enable_stepping(&mut app);
    }
    configure(&mut app, Path::new(&args[0]), input, root, true)?;
    if app.run() != AppExit::Success {
        return Err("crest compute failed".into());
    }
    Ok(())
}

/// Install the native coupled history on the scene's existing ocean/device.
#[allow(dead_code)]
pub fn install(
    app: &mut App,
    source: &Path,
    shore: &Path,
    output: PathBuf,
    scene_frame: &[u8],
    native_budget: bool,
    authored: Option<&super::shore_sim::AuthoredTerrain>,
) -> Result<Targets, Box<dyn Error>> {
    let mut input = load(
        source,
        true,
        1,
        false,
        true,
        if native_budget {
            BudgetMode::Native
        } else {
            BudgetMode::Recorded
        },
    )?;
    let simulation_frame = &input.files["frame-0001-frame.bin"];
    if scene_frame.len() != 720
        || scene_frame[668..672] != simulation_frame[668..672]
        || scene_frame[688..696] != simulation_frame[688..696]
    {
        return Err("scene and spray simulation wind differ".into());
    }
    input.coupled = Some(super::shore_sim::CoupledInput::load(shore, &input.files)?);
    if let Some(terrain) = authored {
        // The comparison above verifies the original acquired inputs first.
        input
            .coupled
            .as_mut()
            .expect("loaded coupled input")
            .apply_authored(terrain)?;
        terrain.apply(&mut input.files, &["kernel.wgsl", "spray-update.wgsl"])?;
        let stations = terrain.stations(&input.files["heights.f32.bin"])?;
        let count = u32::try_from(stations.len())?;
        let old_guard = format!("if ( i >= {}u )", input.count);
        let kernel = std::str::from_utf8(&input.files["kernel.wgsl"])?;
        if kernel.matches(&old_guard).count() != 1 {
            return Err("crest station guard mismatch".into());
        }
        let kernel = kernel.replacen(&old_guard, &format!("if ( i >= {count}u )"), 1);
        input
            .files
            .insert("kernel.wgsl".into(), kernel.into_bytes());
        input.stations = stations
            .iter()
            .flatten()
            .flat_map(|v| v.to_le_bytes())
            .collect();
        input.count = count;
        eprintln!(
            "authored breaker stations: count={count}, sha256={}",
            forge_core::bytes_sha256(&input.stations)
        );
    }
    let fft = app.world().resource::<super::ocean::Targets>();
    let mut params: Vec<u8> = fft
        .parameters
        .iter()
        .flat_map(UVec4::to_array)
        .flat_map(u32::to_le_bytes)
        .collect();
    params[212..216].copy_from_slice(&input.files["frame-0001-ocean.bin"][212..216]);
    if params != input.files["frame-0001-ocean.bin"] {
        return Err("native ocean options differ from spray fixture".into());
    }
    fft.permitted_step.store(1, Ordering::Release);
    super::ocean::enable_stepping(app);
    configure(app, source, input, output, false)
}

fn configure(
    app: &mut App,
    source: &Path,
    input: Input,
    root: PathBuf,
    exit_when_complete: bool,
) -> Result<Targets, Box<dyn Error>> {
    if root.exists() {
        return Err("crest output exists".into());
    }
    let manifest_bytes = fs::read(source.join("spray.json"))?;
    let manifest: serde_json::Value = serde_json::from_slice(&manifest_bytes)?;
    let acquired_manifest_hash = manifest["acquired_manifest_sha256"].as_str().map_or_else(
        || forge_core::bytes_sha256(&manifest_bytes).to_string(),
        str::to_owned,
    );
    // Empty authored shorelines retain one inert allocation; the logical count stays zero.
    let bytes = input.count.max(1) as usize * 6 * 16;
    let mut sizes = vec![bytes, 40960 * 16, 40960 * 16, 40960 * 16, 4];
    if input.integrate {
        sizes.push(768 * 768 * 4);
    }
    if input.history {
        sizes.push(120 * 4);
    }
    if input.coupled.is_some() {
        sizes.push(768 * 768 * 8);
    }
    let buffers = sizes
        .into_iter()
        .enumerate()
        .map(|(i, size)| {
            let words = if input.replay && (1..=4).contains(&i) {
                let n = ["", "pos", "vel", "info", "head"][i];
                input.files[&format!("emitted-{:04}-{n}.bin", input.steps[0])]
                    .as_chunks::<4>()
                    .0
                    .iter()
                    .map(|v| u32::from_le_bytes(*v))
                    .collect()
            } else {
                vec![0_u32; size / 4]
            };
            let mut buffer = ShaderBuffer::from(words);
            buffer.buffer_description.usage |= wgpu::BufferUsages::COPY_SRC;
            app.world_mut()
                .resource_mut::<Assets<ShaderBuffer>>()
                .add(buffer)
        })
        .collect();
    let shore_state = if input.coupled.is_some() {
        let mut image = Image::new_fill(
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
        image.texture_descriptor.usage = wgpu::TextureUsages::TEXTURE_BINDING
            | wgpu::TextureUsages::COPY_DST
            | wgpu::TextureUsages::COPY_SRC;
        Some(app.world_mut().resource_mut::<Assets<Image>>().add(image))
    } else {
        None
    };
    let budget = if input.budget_mode == BudgetMode::Native {
        Some(Arc::new(Mutex::new(BudgetFeedback {
            controller: SprayBudget::new(32768)?,
            latest: None,
            encoded_step: 0,
            encoded_time: 0.0,
            requested_step: 0,
            rows: vec![],
        })))
    } else {
        None
    };
    let targets = Targets {
        budget,
        shore_state,
        stations: input.count,
        buffers,
        ready: Arc::new(AtomicBool::new(false)),
        readback_complete: Arc::new(AtomicBool::new(false)),
    };
    fs::create_dir(&root)?;
    fs::write(
        root.join("probe.json"),
        serde_json::to_vec_pretty(&serde_json::json!({
            "schema":"forge.tidewater.native-spray-emission-probe", "version":1,
            "mode":if input.budget_mode == BudgetMode::Native {"native_history_120_native_budget"} else if input.history {"native_history_120_source_budget"}else if input.replay {"paired_source_particle_update"}else if input.integrate {"isolated_source_fft_integrated_frame1"}else{"isolated_source_fft_frame1"}, "source_commit":"1438b1abfcaee3267092b75573014f4d9b4a983c",
            "source_manifest_sha256":forge_core::bytes_sha256(&manifest_bytes).to_string(), "source_acquisition_manifest_sha256":acquired_manifest_hash,
            "native_stations_sha256":forge_core::bytes_sha256(&input.stations).to_string(),
            "adapted_kernel_sha256":forge_core::bytes_sha256(&input.files["kernel.wgsl"]).to_string(),
            "snapshots":if input.history {vec![120]} else {input.steps.clone()}, "source_crest_results_uploaded":false, "source_particle_results_uploaded":input.replay, "source_particle_expected_outputs_uploaded":false, "source_particle_initial_state_uploaded":input.replay, "crest_kernel_executed":!input.replay,
            "shore_source_manifest_sha256":input.coupled.as_ref().map(super::shore_sim::CoupledInput::manifest_sha256),
            "shore_coupled":input.coupled.is_some(), "source_shore_state_uploaded":false,
            "source_fft_textures_uploaded":!input.history, "source_budget_schedule":input.history && input.budget_mode == BudgetMode::Recorded, "native_budget_feedback":input.budget_mode == BudgetMode::Native,
            "integration":input.integrate, "limits":if input.history {vec!["native FFT and particle history for 120 steps", if input.budget_mode == BudgetMode::Native {"native GPU head feedback; controlled one-step simulation latency"} else {"source emission budget replay"}, if input.coupled.is_some() {"native shore consumes preceding deposit before emission"} else {"deposits cleared each step; shore state not coupled"}, "no spray rendering"]}else{vec!["source FFT snapshot as paired input", "single update step", "no coupled shore update or sprite rendering"]}
        }))?,
    )?;
    app.insert_resource(targets.clone())
        .insert_resource(Output {
            budget_report: targets
                .budget
                .as_ref()
                .map(|_| root.join("native-budget.json")),
            completion: if exit_when_complete {
                Completion::Exit
            } else {
                Completion::Scene
            },
            integrate: input.integrate,
            replay: input.replay,
            step: if input.history { 120 } else { input.steps[0] },
            root,
            requested: false,
            received: if input.replay {
                BTreeSet::from([0])
            } else {
                BTreeSet::new()
            },
            last_frame: 0,
            bytes,
        })
        .add_systems(Update, (readback, read_budget));
    app.sub_app_mut(RenderApp)
        .insert_resource(input)
        .insert_resource(targets.clone())
        .add_systems(
            RenderGraph,
            compute
                .after(super::ocean::OceanComputeSet)
                .before(camera_driver),
        );
    Ok(targets)
}

fn texture(
    device: &wgpu::Device,
    queue: &RenderQueue,
    side: u32,
    layers: u32,
    format: wgpu::TextureFormat,
    mips: &[&[u8]],
    bpp: u32,
) -> wgpu::TextureView {
    let texture = device.create_texture(&wgpu::TextureDescriptor {
        label: Some("crest input"),
        size: wgpu::Extent3d {
            width: side,
            height: side,
            depth_or_array_layers: layers,
        },
        mip_level_count: u32::try_from(mips.len()).expect("mips"),
        sample_count: 1,
        dimension: wgpu::TextureDimension::D2,
        format,
        usage: wgpu::TextureUsages::TEXTURE_BINDING | wgpu::TextureUsages::COPY_DST,
        view_formats: &[],
    });
    for (mip, data) in mips.iter().enumerate() {
        let n = side >> mip;
        queue.write_texture(
            wgpu::TexelCopyTextureInfo {
                texture: &texture,
                mip_level: u32::try_from(mip).expect("mip"),
                origin: wgpu::Origin3d::ZERO,
                aspect: wgpu::TextureAspect::All,
            },
            data,
            wgpu::TexelCopyBufferLayout {
                offset: 0,
                bytes_per_row: Some(n * bpp),
                rows_per_image: Some(n),
            },
            wgpu::Extent3d {
                width: n,
                height: n,
                depth_or_array_layers: layers,
            },
        );
    }
    texture.create_view(&wgpu::TextureViewDescriptor {
        dimension: Some(if layers > 1 {
            wgpu::TextureViewDimension::D2Array
        } else {
            wgpu::TextureViewDimension::D2
        }),
        ..default()
    })
}
#[allow(clippy::too_many_arguments)] // Shared FFT images and readiness are render resources.
fn compute(
    mut context: RenderContext,
    input: Res<Input>,
    targets: Res<Targets>,
    device: Res<RenderDevice>,
    queue: Res<RenderQueue>,
    buffers: Res<RenderAssets<GpuShaderBuffer>>,
    ocean: Option<Res<super::ocean::Targets>>,
    images: Res<RenderAssets<GpuImage>>,
    mut progress: Local<(u32, f64)>,
    mut coupled: Local<Option<super::shore_sim::CoupledSimulation>>,
) {
    if targets.ready.load(Ordering::Acquire) {
        return;
    }
    let gpu: Vec<_> = targets
        .buffers
        .iter()
        .filter_map(|h| buffers.get(h))
        .collect();
    if gpu.len()
        != if input.history {
            if input.coupled.is_some() { 8 } else { 7 }
        } else if input.integrate {
            6
        } else {
            5
        }
    {
        return;
    }
    let public_shore = if let Some(handle) = &targets.shore_state {
        let Some(image) = images.get(handle) else {
            return;
        };
        Some(image)
    } else {
        None
    };
    let mut native_step = 0;
    let native_image = if input.history {
        let fft = ocean.as_ref().expect("native ocean installed");
        native_step = fft.completed_steps.load(Ordering::Acquire);
        if native_step == 0 || native_step == progress.0 {
            return;
        }
        assert_eq!(native_step, progress.0 + 1, "missed ocean step");
        let Some(image) = images.get(&fft.images[0]) else {
            return;
        };
        Some(image)
    } else {
        None
    };
    if input.history {
        progress.0 = native_step;
        progress.1 += 1.0 / 60.0;
    }
    let device = device.wgpu_device();
    let ub = wgpu::BindingType::Buffer {
        ty: wgpu::BufferBindingType::Uniform,
        has_dynamic_offset: false,
        min_binding_size: None,
    };
    let tex = |filterable, array| wgpu::BindingType::Texture {
        sample_type: wgpu::TextureSampleType::Float { filterable },
        view_dimension: if array {
            wgpu::TextureViewDimension::D2Array
        } else {
            wgpu::TextureViewDimension::D2
        },
        multisampled: false,
    };
    let entry = |binding, ty| wgpu::BindGroupLayoutEntry {
        binding,
        visibility: wgpu::ShaderStages::COMPUTE,
        ty,
        count: None,
    };
    let mut entries0 = vec![entry(0, ub)];
    for i in 1..10 {
        entries0.push(entry(
            i,
            wgpu::BindingType::Sampler(if i == 9 {
                wgpu::SamplerBindingType::Comparison
            } else if i >= 7 {
                wgpu::SamplerBindingType::NonFiltering
            } else {
                wgpu::SamplerBindingType::Filtering
            }),
        ));
    }
    let layout0 = device.create_bind_group_layout(&wgpu::BindGroupLayoutDescriptor {
        label: Some("crest frame"),
        entries: &entries0,
    });
    let entries1: Vec<_> = (0..20)
        .map(|i| {
            entry(
                i,
                match i {
                    6 | 8 | 11 | 16 | 17 => ub,
                    12..=15 | 18 | 19 => wgpu::BindingType::Buffer {
                        ty: wgpu::BufferBindingType::Storage { read_only: i == 18 },
                        has_dynamic_offset: false,
                        min_binding_size: None,
                    },
                    0 | 3 | 4 => tex(false, false),
                    9 | 10 => tex(true, true),
                    _ => tex(true, false),
                },
            )
        })
        .collect();
    let layout1 = device.create_bind_group_layout(&wgpu::BindGroupLayoutDescriptor {
        label: Some("crest resources"),
        entries: &entries1,
    });
    let layout = device.create_pipeline_layout(&wgpu::PipelineLayoutDescriptor {
        label: Some("crest compute"),
        bind_group_layouts: &[Some(&layout0), Some(&layout1)],
        immediate_size: 0,
    });
    let shader = device.create_shader_module(wgpu::ShaderModuleDescriptor {
        label: Some("original crest kernel"),
        source: wgpu::ShaderSource::Wgsl(
            std::str::from_utf8(&input.files["kernel.wgsl"])
                .expect("checked WGSL")
                .into(),
        ),
    });
    let pipeline = device.create_compute_pipeline(&wgpu::ComputePipelineDescriptor {
        label: Some("crest compute"),
        layout: Some(&layout),
        module: &shader,
        entry_point: Some("main"),
        compilation_options: default(),
        cache: None,
    });
    let samplers: Vec<_> = (1..10)
        .map(|i| {
            let address = match i {
                1 | 4 | 6 | 8 => wgpu::AddressMode::Repeat,
                3 => wgpu::AddressMode::MirrorRepeat,
                _ => wgpu::AddressMode::ClampToEdge,
            };
            let filter = if i >= 7 {
                wgpu::FilterMode::Nearest
            } else {
                wgpu::FilterMode::Linear
            };
            device.create_sampler(&wgpu::SamplerDescriptor {
                address_mode_u: address,
                address_mode_v: address,
                address_mode_w: address,
                min_filter: filter,
                mag_filter: filter,
                mipmap_filter: if i >= 7 {
                    wgpu::MipmapFilterMode::Nearest
                } else {
                    wgpu::MipmapFilterMode::Linear
                },
                anisotropy_clamp: match i {
                    4 | 5 => 8,
                    6 => 4,
                    _ => 1,
                },
                compare: if i == 9 {
                    Some(wgpu::CompareFunction::LessEqual)
                } else {
                    None
                },
                ..default()
            })
        })
        .collect();
    let buffer = |data: &[u8], usage| {
        device.create_buffer_init(&wgpu::util::BufferInitDescriptor {
            label: Some("crest input"),
            contents: data,
            usage,
        })
    };
    let uniform = |data: &[u8]| buffer(data, wgpu::BufferUsages::UNIFORM);
    let terrain = uniform(&input.files["terrain-params.bin"]);
    let shore = uniform(&input.files["shore-params.bin"]);
    let station_bytes = if input.stations.is_empty() {
        &[0_u8; 16][..]
    } else {
        &input.stations
    };
    let stations = buffer(station_bytes, wgpu::BufferUsages::STORAGE);
    let height = texture(
        device,
        &queue,
        2048,
        1,
        wgpu::TextureFormat::R32Float,
        &[&input.files["heights.f32.bin"]],
        4,
    );
    let delta_bytes = input
        .files
        .get("authored-height-deltas.bin")
        .map_or(&[0_u8; 4][..], Vec::as_slice);
    let delta_side = if delta_bytes.len() == 4 { 1 } else { 2049 };
    let deltas = texture(
        device,
        &queue,
        delta_side,
        1,
        wgpu::TextureFormat::R32Float,
        &[delta_bytes],
        4,
    );
    let field = texture(
        device,
        &queue,
        512,
        1,
        wgpu::TextureFormat::Rgba32Float,
        &[&input.files["shore-field.rgba32.bin"]],
        16,
    );
    let dummy = texture(
        device,
        &queue,
        1,
        1,
        wgpu::TextureFormat::Rgba8Unorm,
        &[&[0; 4]],
        4,
    );
    for (index, step) in input.steps.iter().enumerate() {
        let mut frame_bytes = input.files[&format!("frame-{step:04}-frame.bin")].clone();
        let mut ocean_bytes = input.files[&format!("frame-{step:04}-ocean.bin")].clone();
        let mut params_bytes = input.files[&format!("frame-{step:04}-params.bin")].clone();
        let mut spray_bytes = input.files[&format!("frame-{step:04}-spray-params.bin")].clone();
        if input.history {
            #[allow(clippy::cast_possible_truncation)]
            let time = progress.1 as f32;
            frame_bytes[576..580].copy_from_slice(&native_step.to_le_bytes());
            frame_bytes[580..584].copy_from_slice(&time.to_le_bytes());
            ocean_bytes = ocean
                .as_ref()
                .expect("ocean")
                .parameters
                .iter()
                .flat_map(UVec4::to_array)
                .flat_map(u32::to_le_bytes)
                .collect();
            ocean_bytes[212..216].copy_from_slice(&time.to_le_bytes());
            #[allow(clippy::cast_possible_truncation)]
            // The source packs a double budget as f32 for GPU emission.
            let budget = if let Some(shared) = &targets.budget {
                shared.lock().expect("budget mutex").controller.value() as f32
            } else {
                input.budgets[native_step as usize - 1]
            };
            params_bytes[28..32].copy_from_slice(&budget.to_le_bytes());
            spray_bytes[..4].copy_from_slice(&native_step.to_le_bytes());
            if let Some(shore_input) = &input.coupled {
                let sim = coupled.get_or_insert_with(|| {
                    shore_input.initialize(device, &queue, (*gpu[5].buffer).clone())
                });
                sim.step(device, context.command_encoder(), &frame_bytes);
            } else {
                context
                    .command_encoder()
                    .clear_buffer(&gpu[5].buffer, 0, None);
            }
        }
        let frame = uniform(&frame_bytes);
        let ocean = uniform(&ocean_bytes);
        let params = uniform(&params_bytes);
        let spray_params = uniform(&spray_bytes);
        let acquired;
        let displacement = if let Some(image) = native_image {
            &*image.texture_view
        } else {
            let mips: Vec<&[u8]> = (0..9)
                .map(|m| {
                    input.files[&format!("frame-{step:04}-displacement-m{m}.rgba16.bin")].as_slice()
                })
                .collect();
            acquired = texture(
                device,
                &queue,
                256,
                4,
                wgpu::TextureFormat::Rgba16Float,
                &mips,
                8,
            );
            &acquired
        };
        let mut entries = vec![wgpu::BindGroupEntry {
            binding: 0,
            resource: frame.as_entire_binding(),
        }];
        for (i, sampler) in samplers.iter().enumerate() {
            entries.push(wgpu::BindGroupEntry {
                binding: u32::try_from(i + 1).expect("binding"),
                resource: wgpu::BindingResource::Sampler(sampler),
            });
        }
        let group0 = device.create_bind_group(&wgpu::BindGroupDescriptor {
            label: Some("crest frame"),
            layout: &layout0,
            entries: &entries,
        });
        let view = wgpu::BindingResource::TextureView;
        let resources = [
            view(&height),
            view(&dummy),
            view(&dummy),
            view(&deltas),
            view(&field),
            view(&dummy),
            terrain.as_entire_binding(),
            view(&dummy),
            shore.as_entire_binding(),
            view(displacement),
            view(displacement),
            ocean.as_entire_binding(),
            gpu[1].buffer.as_entire_binding(),
            gpu[2].buffer.as_entire_binding(),
            gpu[3].buffer.as_entire_binding(),
            gpu[4].buffer.as_entire_binding(),
            spray_params.as_entire_binding(),
            params.as_entire_binding(),
            stations.as_entire_binding(),
            gpu[index].buffer.as_entire_binding(),
        ];
        let entries: Vec<_> = resources
            .into_iter()
            .enumerate()
            .map(|(i, resource)| wgpu::BindGroupEntry {
                binding: u32::try_from(i).expect("binding"),
                resource,
            })
            .collect();
        let group1 = device.create_bind_group(&wgpu::BindGroupDescriptor {
            label: Some("crest resources"),
            layout: &layout1,
            entries: &entries,
        });
        let mut pass = context
            .command_encoder()
            .begin_compute_pass(&wgpu::ComputePassDescriptor {
                label: Some("native crests"),
                timestamp_writes: None,
            });
        pass.set_pipeline(&pipeline);
        pass.set_bind_group(0, &group0, &[]);
        pass.set_bind_group(1, &group1, &[]);
        if !input.replay {
            pass.dispatch_workgroups(input.count.div_ceil(64), 1, 1);
        }
        drop(pass);
        if input.integrate {
            let entries: Vec<_> = (0..24)
                .map(|i| {
                    entry(
                        i,
                        match i {
                            0..=3 | 5 | 22 | 23 => wgpu::BindingType::Buffer {
                                ty: wgpu::BufferBindingType::Storage {
                                    read_only: i == 5 || i == 23,
                                },
                                has_dynamic_offset: false,
                                min_binding_size: None,
                            },
                            4 | 8 | 15 | 17 | 18 | 21 => ub,
                            6 | 7 => tex(true, true),
                            9 | 12 | 13 => tex(false, false),
                            _ => tex(true, false),
                        },
                    )
                })
                .collect();
            let update_layout = device.create_bind_group_layout(&wgpu::BindGroupLayoutDescriptor {
                label: Some("spray integration resources"),
                entries: &entries,
            });
            let layout = device.create_pipeline_layout(&wgpu::PipelineLayoutDescriptor {
                label: Some("spray integration"),
                bind_group_layouts: &[Some(&layout0), Some(&update_layout)],
                immediate_size: 0,
            });
            let shader = device.create_shader_module(wgpu::ShaderModuleDescriptor {
                label: Some("original spray update"),
                source: wgpu::ShaderSource::Wgsl(
                    std::str::from_utf8(&input.files["spray-update.wgsl"])
                        .expect("checked WGSL")
                        .into(),
                ),
            });
            let update = device.create_compute_pipeline(&wgpu::ComputePipelineDescriptor {
                label: Some("spray integration"),
                layout: Some(&layout),
                module: &shader,
                entry_point: Some("main"),
                compilation_options: default(),
                cache: None,
            });
            let query = buffer(&[0; 64 * 16], wgpu::BufferUsages::STORAGE);
            let requests = buffer(&[0; 32 * 4 * 16], wgpu::BufferUsages::STORAGE);
            let surface = uniform(&input.files["surface-params.bin"]);
            let sim = uniform(&input.files["shore-sim-params.bin"]);
            let resources = [
                gpu[1].buffer.as_entire_binding(),
                gpu[2].buffer.as_entire_binding(),
                gpu[3].buffer.as_entire_binding(),
                gpu[4].buffer.as_entire_binding(),
                spray_params.as_entire_binding(),
                query.as_entire_binding(),
                view(displacement),
                view(displacement),
                ocean.as_entire_binding(),
                view(&height),
                view(&dummy),
                view(&dummy),
                view(&deltas),
                view(&field),
                view(&dummy),
                terrain.as_entire_binding(),
                view(&dummy),
                shore.as_entire_binding(),
                surface.as_entire_binding(),
                view(&dummy),
                view(&dummy),
                sim.as_entire_binding(),
                gpu[5].buffer.as_entire_binding(),
                requests.as_entire_binding(),
            ];
            let entries: Vec<_> = resources
                .into_iter()
                .enumerate()
                .map(|(i, resource)| wgpu::BindGroupEntry {
                    binding: u32::try_from(i).expect("binding"),
                    resource,
                })
                .collect();
            let group = device.create_bind_group(&wgpu::BindGroupDescriptor {
                label: Some("spray integration"),
                layout: &update_layout,
                entries: &entries,
            });
            let mut pass =
                context
                    .command_encoder()
                    .begin_compute_pass(&wgpu::ComputePassDescriptor {
                        label: Some("native spray integration"),
                        timestamp_writes: None,
                    });
            pass.set_pipeline(&update);
            pass.set_bind_group(0, &group0, &[]);
            pass.set_bind_group(1, &group, &[]);
            pass.dispatch_workgroups(40960 / 64, 1, 1);
        }
    }
    if input.history {
        context.command_encoder().copy_buffer_to_buffer(
            &gpu[4].buffer,
            0,
            &gpu[6].buffer,
            u64::from(native_step - 1) * 4,
            4,
        );
        if let Some(shared) = &targets.budget {
            let mut feedback = shared.lock().expect("budget mutex");
            assert_eq!(
                feedback.latest.map(|v| v.0),
                if native_step == 1 {
                    None
                } else {
                    Some(native_step - 1)
                },
                "missing previous GPU head"
            );
            let emitter = feedback.controller.value();
            let latest = feedback.latest;
            feedback
                .controller
                .after_emission(latest.map(|v| v.1))
                .expect("ordered native feedback");
            let next_budget = feedback.controller.value();
            feedback.rows.push(serde_json::json!({"frame":native_step,"time":progress.1,"emitter_budget":emitter,"budget":next_budget,"feedback_frame":latest.map(|v|v.0),"feedback_head":latest.map(|v|v.1.head),"feedback_time":latest.map(|v|v.1.time)}));
            feedback.encoded_step = native_step;
            feedback.encoded_time = progress.1;
        } else {
            ocean
                .as_ref()
                .expect("ocean")
                .permitted_step
                .store(native_step + 1, Ordering::Release);
        }
        if native_step < 120 {
            return;
        }
    }
    if let Some(sim) = coupled.as_ref() {
        let image = public_shore.expect("coupled shore image");
        sim.publish(context.command_encoder(), &image.texture);
        context.command_encoder().copy_texture_to_buffer(
            image.texture.as_image_copy(),
            wgpu::TexelCopyBufferInfo {
                buffer: &gpu[7].buffer,
                layout: wgpu::TexelCopyBufferLayout {
                    offset: 0,
                    bytes_per_row: Some(768 * 8),
                    rows_per_image: Some(768),
                },
            },
            wgpu::Extent3d {
                width: 768,
                height: 768,
                depth_or_array_layers: 1,
            },
        );
    }
    targets.ready.store(true, Ordering::Release);
}
fn readback(
    mut commands: Commands,
    targets: Res<Targets>,
    mut output: ResMut<Output>,
    frame: Res<FrameCount>,
    mut exit: MessageWriter<AppExit>,
    ocean: Option<Res<super::ocean::Targets>>,
) {
    if frame.0 > 600 && !targets.readback_complete.load(Ordering::Acquire) {
        exit.write(AppExit::error());
        return;
    }
    if output.received.len() == targets.buffers.len() {
        if let Some(path) = output.budget_report.take() {
            let feedback = targets
                .budget
                .as_ref()
                .expect("native budget")
                .lock()
                .expect("budget mutex");
            let report = serde_json::json!({"schema":"forge.tidewater.native-spray-budget","version":1,"steps":120,"feedback":"native_gpu_head_previous_simulation_step","source_schedule_loaded":false,"schedule":feedback.rows,"limits":["simulation waits for each head readback to match controlled source acquisition latency","not a real-time performance qualification"]});
            if let Err(error) = fs::write(path, serde_json::to_vec_pretty(&report).expect("JSON")) {
                eprintln!("{error}");
                exit.write(AppExit::error());
                return;
            }
        }
        targets.readback_complete.store(true, Ordering::Release);
        if output.completion == Completion::Exit
            && frame.0 >= output.last_frame + 8
            && ocean
                .as_ref()
                .is_none_or(|v| v.readback_complete.load(Ordering::Acquire))
        {
            exit.write(AppExit::Success);
        }
        return;
    }
    if output.requested || !targets.ready.load(Ordering::Acquire) {
        return;
    }
    output.requested = true;
    for (i, buffer) in targets.buffers.iter().enumerate() {
        if output.replay && i == 0 {
            continue;
        }
        commands
            .spawn((Readback::buffer(buffer.clone()), Item(i)))
            .observe(
                |event: On<ReadbackComplete>,
                 items: Query<&Item>,
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
                    let names = if output.integrate {
                        [
                            "frame-0001-crests.f32.bin",
                            "frame-0001-pos.bin",
                            "frame-0001-vel.bin",
                            "frame-0001-info.bin",
                            "frame-0001-head.bin",
                            "frame-0001-deposit.bin",
                        ]
                    } else {
                        [
                            "frame-0001-crests.f32.bin",
                            "emitted-0001-pos.bin",
                            "emitted-0001-vel.bin",
                            "emitted-0001-info.bin",
                            "emitted-0001-head.bin",
                            "unused",
                        ]
                    };
                    let name = if item.0 == 7 {
                        "frame-0120-shore.rgba16.bin".to_owned()
                    } else if item.0 == 6 {
                        "history-head.u32.bin".to_owned()
                    } else {
                        names[item.0].replace("0001", &format!("{:04}", output.step))
                    };
                    let expected = if item.0 == 7 {
                        768 * 768 * 8
                    } else if item.0 == 6 {
                        120 * 4
                    } else if item.0 == 0 {
                        output.bytes
                    } else if item.0 == 5 {
                        768 * 768 * 4
                    } else if item.0 == 4 {
                        4
                    } else {
                        40960 * 16
                    };
                    if event.data.len() != expected {
                        exit.write(AppExit::error());
                        return;
                    }
                    if let Err(e) = fs::write(output.root.join(&name), &event.data) {
                        eprintln!("{e}");
                        exit.write(AppExit::error());
                    } else {
                        println!("{name} sha256 {}", forge_core::bytes_sha256(&event.data));
                    }
                    output.last_frame = frame.0;
                    commands.entity(event.entity).despawn();
                },
            );
    }
}

// Keep the simulation on the encoded step until its head copy is delivered. Rendering may
// continue meanwhile; the next FFT cannot overwrite the state associated with this sample.
fn read_budget(
    mut commands: Commands,
    targets: Res<Targets>,
    ocean: Option<Res<super::ocean::Targets>>,
) {
    let Some(shared) = &targets.budget else {
        return;
    };
    let (step, time) = {
        let mut feedback = shared.lock().expect("budget mutex");
        let step = feedback.encoded_step;
        if step == 0 || step == 120 || step == feedback.requested_step {
            return;
        }
        feedback.requested_step = step;
        (step, feedback.encoded_time)
    };
    let shared = shared.clone();
    let permitted = ocean.expect("native ocean").permitted_step.clone();
    commands
        .spawn(Readback::buffer(targets.buffers[4].clone()))
        .observe(
            move |event: On<ReadbackComplete>,
                  mut commands: Commands,
                  mut exit: MessageWriter<AppExit>| {
                let Ok(bytes) = <[u8; 4]>::try_from(event.data.as_slice()) else {
                    exit.write(AppExit::error());
                    return;
                };
                let mut feedback = shared.lock().expect("budget mutex");
                if feedback.encoded_step != step || feedback.latest.is_some_and(|v| v.0 >= step) {
                    exit.write(AppExit::error());
                    return;
                }
                feedback.latest = Some((
                    step,
                    HeadSample {
                        time,
                        head: u32::from_le_bytes(bytes),
                    },
                ));
                permitted.store(step + 1, Ordering::Release);
                commands.entity(event.entity).despawn();
            },
        );
}
