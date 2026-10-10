//! Crest compute with paired source FFT or shared native FFT; source crests are never uploaded.
#![allow(clippy::needless_pass_by_value, clippy::too_many_lines)]
use bevy::{
    app::{AppExit, ScheduleRunnerPlugin},
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
use std::{
    collections::{BTreeMap, BTreeSet},
    error::Error,
    fs,
    path::{Path, PathBuf},
    sync::{
        Arc,
        atomic::{AtomicBool, Ordering},
    },
    time::Duration,
};
use wgpu::util::DeviceExt;
const STEPS: [u32; 3] = [1, 120, 540];
#[derive(Resource)]
struct Input {
    files: BTreeMap<String, Vec<u8>>,
    stations: Vec<u8>,
    count: u32,
    steps: Vec<u32>,
    native_ocean: bool,
}
#[derive(Resource, Clone)]
pub struct Targets {
    #[allow(dead_code)] // Consumed by the scene lip material, not the standalone compute example.
    pub stations: u32,
    pub buffers: Vec<Handle<ShaderBuffer>>,
    pub ready: Arc<AtomicBool>,
    pub readback_complete: Arc<AtomicBool>,
}
#[derive(Resource)]
struct Output {
    root: PathBuf,
    exit_when_complete: bool,
    requested: bool,
    received: BTreeSet<usize>,
    last_frame: u32,
    bytes: usize,
    steps: Vec<u32>,
}
#[derive(Component)]
struct Item(usize);

fn load(root: &Path, native_ocean: bool) -> Result<Input, Box<dyn Error>> {
    let meta: serde_json::Value = serde_json::from_slice(&fs::read(root.join("breakers.json"))?)?;
    if meta["schema"] != "forge.tidewater.breakers"
        || meta["version"] != 1
        || meta["source_commit"] != "1438b1abfcaee3267092b75573014f4d9b4a983c"
        || meta["options"] != serde_json::json!({"spray":false,"clouds":false})
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
    let steps = if native_ocean {
        vec![120]
    } else {
        STEPS.to_vec()
    };
    for step in &steps {
        for suffix in ["frame.bin", "params.bin", "ocean.bin"] {
            names.push(format!("frame-{step:04}-{suffix}"));
        }
        for mip in 0..if native_ocean { 0 } else { 9 } {
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
        for mip in 0..if native_ocean { 0 } else { 9 } {
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
    Ok(Input {
        files,
        stations,
        count,
        steps,
        native_ocean,
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
    let args: Vec<_> = std::env::args_os().skip(1).collect();
    if !(args.len() == 2 || (args.len() == 4 && args[2] == "--native-ocean")) {
        return Err("BREAKERS_EXPORT NEW_OUTPUT [--native-ocean OCEAN_EXPORT] required".into());
    }
    let root = PathBuf::from(&args[1]);
    if root.exists() {
        return Err("output exists".into());
    }
    let native_ocean = args.len() == 4;
    let mut input = load(Path::new(&args[0]), native_ocean)?;
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
    if native_ocean {
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
        params[212..216].copy_from_slice(&2.0_f32.to_le_bytes());
        if params != input.files["frame-0120-ocean.bin"] {
            return Err("native FFT parameters differ from crest reference".into());
        }
        input.files.insert("frame-0120-ocean.bin".into(), params);
    }
    configure(&mut app, Path::new(&args[0]), input, root, true)?;
    if app.run() != AppExit::Success {
        return Err("crest compute failed".into());
    }
    Ok(())
}

/// Attach crest computation to an App which already owns the native ocean.
#[allow(dead_code)] // The standalone oracle uses configure directly.
pub fn install(app: &mut App, source: &Path, output: PathBuf) -> Result<Targets, Box<dyn Error>> {
    let mut input = load(source, true)?;
    let fft = app
        .world()
        .get_resource::<super::ocean::Targets>()
        .ok_or("install ocean before crests")?;
    let mut params: Vec<u8> = fft
        .parameters
        .iter()
        .flat_map(UVec4::to_array)
        .flat_map(u32::to_le_bytes)
        .collect();
    params[212..216].copy_from_slice(&2.0_f32.to_le_bytes());
    if params != input.files["frame-0120-ocean.bin"] {
        return Err("native FFT parameters differ from crest reference".into());
    }
    input.files.insert("frame-0120-ocean.bin".into(), params);
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
    let bytes = input.count as usize * 6 * 16;
    let buffers = input
        .steps
        .iter()
        .map(|_| {
            let mut buffer = ShaderBuffer::from(vec![0_u32; bytes / 4]);
            buffer.buffer_description.usage |= wgpu::BufferUsages::COPY_SRC;
            app.world_mut()
                .resource_mut::<Assets<ShaderBuffer>>()
                .add(buffer)
        })
        .collect();
    let targets = Targets {
        stations: input.count,
        buffers,
        ready: Arc::new(AtomicBool::new(false)),
        readback_complete: Arc::new(AtomicBool::new(false)),
    };
    fs::create_dir(&root)?;
    fs::write(
        root.join("probe.json"),
        serde_json::to_vec_pretty(&serde_json::json!({
            "schema":"forge.tidewater.native-crest-probe", "version":1,
            "mode":if input.native_ocean {"shared_native_fft_frame120"}else{"isolated_source_fft_inputs"}, "source_commit":"1438b1abfcaee3267092b75573014f4d9b4a983c",
            "source_manifest_sha256":forge_core::bytes_sha256(&fs::read(source.join("breakers.json"))?).to_string(),
            "native_stations_sha256":forge_core::bytes_sha256(&input.stations).to_string(),
            "adapted_kernel_sha256":forge_core::bytes_sha256(&input.files["kernel.wgsl"]).to_string(),
            "snapshots":input.steps, "source_crest_results_uploaded":false,
            "source_fft_textures_uploaded":!input.native_ocean,
            "limits":if input.native_ocean {vec!["fixed native frame120", "no visible lip yet", "no spray or clouds"]}else{vec!["source FFT snapshots as paired input", "not yet shared native FFT or visible lip", "no spray or clouds"]}
        }))?,
    )?;
    app.insert_resource(targets.clone())
        .insert_resource(Output {
            root,
            exit_when_complete,
            requested: false,
            received: BTreeSet::new(),
            last_frame: 0,
            bytes,
            steps: input.steps.clone(),
        })
        .add_systems(Update, readback);
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
    ocean_targets: Option<Res<super::ocean::Targets>>,
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
    if gpu.len() != input.steps.len() {
        return;
    }
    let native_image = if input.native_ocean {
        let ocean = ocean_targets.as_ref().expect("native FFT installed");
        if !ocean.ready.load(Ordering::Acquire) {
            return;
        }
        let Some(image) = images.get(&ocean.images[0]) else {
            return;
        };
        Some(image)
    } else {
        None
    };
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
    let entries1: Vec<_> = (0..15)
        .map(|i| {
            entry(
                i,
                match i {
                    6 | 8 | 11 | 12 => ub,
                    13 | 14 => wgpu::BindingType::Buffer {
                        ty: wgpu::BufferBindingType::Storage { read_only: i == 13 },
                        has_dynamic_offset: false,
                        min_binding_size: None,
                    },
                    0 | 4 => tex(false, false),
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
    let stations = buffer(&input.stations, wgpu::BufferUsages::STORAGE);
    let height = texture(
        device,
        &queue,
        2048,
        1,
        wgpu::TextureFormat::R32Float,
        &[&input.files["heights.f32.bin"]],
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
        let frame = uniform(&input.files[&format!("frame-{step:04}-frame.bin")]);
        let ocean = uniform(&input.files[&format!("frame-{step:04}-ocean.bin")]);
        let params = uniform(&input.files[&format!("frame-{step:04}-params.bin")]);
        let acquired;
        let displacement: &wgpu::TextureView = if let Some(image) = native_image {
            &image.texture_view
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
            view(&dummy),
            view(&field),
            view(&dummy),
            terrain.as_entire_binding(),
            view(&dummy),
            shore.as_entire_binding(),
            view(displacement),
            view(displacement),
            ocean.as_entire_binding(),
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
        pass.dispatch_workgroups(input.count.div_ceil(64), 1, 1);
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
    if frame.0 > 600 {
        exit.write(AppExit::error());
        return;
    }
    if output.received.len() == targets.buffers.len() {
        targets.readback_complete.store(true, Ordering::Release);
        if output.exit_when_complete
            && frame.0 >= output.last_frame + 8
            && ocean
                .as_ref()
                .is_none_or(|o| o.readback_complete.load(Ordering::Acquire))
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
                    let name = format!("frame-{:04}-crests.f32.bin", output.steps[item.0]);
                    if event.data.len() != output.bytes {
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
