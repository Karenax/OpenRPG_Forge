//! Read back the actual resident Studio ocean after its acquired bootstrap.
use bevy::{
    app::{AppExit, ScheduleRunnerPlugin},
    diagnostic::FrameCount,
    prelude::*,
    render::{
        RenderApp,
        extract_resource::{ExtractResource, ExtractResourcePlugin},
        gpu_readback::{Readback, ReadbackComplete},
        render_asset::RenderAssets,
        render_resource as gpu,
        renderer::{RenderContext, RenderGraph},
        storage::{GpuShaderBuffer, ShaderBuffer},
        texture::GpuImage,
    },
    window::ExitCondition,
    winit::WinitPlugin,
};
use forge_tidewater::tidewater_ocean::{self, OceanComputeSet, PreparedOcean, Targets};
use forge_tidewater_assets::tidewater_ocean::OceanPackage;
use std::{
    error::Error,
    fs,
    path::PathBuf,
    sync::{
        Arc, Mutex,
        atomic::{AtomicBool, Ordering},
    },
    time::Duration,
};

#[derive(Resource, Clone, ExtractResource)]
struct Copies {
    buffers: Vec<Handle<ShaderBuffer>>,
    raw: [Handle<ShaderBuffer>; 3],
    encoded: Arc<AtomicBool>,
}
#[derive(Resource)]
struct Output {
    root: PathBuf,
    requested: bool,
    received: u32,
    failed: bool,
    finished: Arc<AtomicBool>,
    captured: Arc<Mutex<Option<tidewater_ocean::live_clock::LiveStep>>>,
}
#[derive(Component)]
struct Item(usize);

fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args_os().skip(1).collect();
    if args.len() != 3 {
        return Err("usage: tidewater_resident_ocean_probe PACKAGE SHA256 OUTPUT".into());
    }
    let package_root = PathBuf::from(&args[0]);
    let expected = args[1].to_string_lossy().parse()?;
    let root = PathBuf::from(&args[2]);
    if root.exists() {
        return Err("output already exists; preserve previous evidence".into());
    }
    let manifest = fs::read(package_root.join("package.json"))?;
    let package = OceanPackage::load(&manifest, expected, |name, _| {
        fs::read(package_root.join(name)).map_err(|e| e.to_string())
    })?;
    let frames = u32::try_from(package.bootstrap_frames())?;
    let prepared = PreparedOcean::new(&package)?;
    fs::create_dir_all(&root)?;
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
    tidewater_ocean::register(&mut app)?;
    let targets = prepared.stage(app.world_mut())?;
    let clock = tidewater_ocean::live_clock(app.world()).ok_or("resident clock absent")?;
    clock.pause();
    let buffers = (0..18)
        .map(|index| {
            let side = 256_usize >> (index % 9);
            let bytes = (side * 8).div_ceil(256) * 256 * side * 4;
            let mut buffer = ShaderBuffer::from(vec![0_u32; bytes / 4]);
            buffer.buffer_description.usage |=
                gpu::BufferUsages::COPY_DST | gpu::BufferUsages::COPY_SRC;
            app.world_mut()
                .resource_mut::<Assets<ShaderBuffer>>()
                .add(buffer)
        })
        .collect();
    app.insert_resource(Copies {
        buffers,
        raw: targets.readback_buffers(),
        encoded: Arc::new(AtomicBool::new(false)),
    });
    let finished = Arc::new(AtomicBool::new(false));
    let captured = Arc::new(Mutex::new(None));
    app.insert_resource(Output {
        root: root.clone(),
        requested: false,
        received: 0,
        failed: false,
        finished: finished.clone(),
        captured: captured.clone(),
    });
    app.add_plugins(ExtractResourcePlugin::<Copies>::default());
    app.sub_app_mut(RenderApp)
        .add_systems(RenderGraph, copy_outputs.after(OceanComputeSet));
    app.add_systems(Update, (capture_frame.before(readback), readback));
    let exit = app.run();
    let steps = targets.completed_steps.load(Ordering::Acquire);
    if exit != AppExit::Success || !finished.load(Ordering::Acquire) || steps != frames {
        return Err("resident ocean readbacks incomplete or advanced beyond acquired frame".into());
    }
    let completed = captured
        .lock()
        .map_err(|_| "GPU frame capture lock poisoned")?
        .ok_or("encoded acquired frame absent from dependent-material clock")?;
    let final_params = package
        .file(&format!("ocean-step{frames:03}-params.bin"))
        .ok_or("probe requires explicit acquired ocean frames")?;
    #[allow(clippy::cast_possible_truncation)]
    let published_time = (completed.time as f32).to_le_bytes();
    if completed.step != frames
        || published_time != final_params[212..216]
        || completed.dt.to_bits() != (1.0_f32 / 60.0).to_bits()
    {
        return Err("dependent-material clock differs from encoded acquired frame".into());
    }
    fs::write(
        root.join("execution.json"),
        serde_json::to_vec_pretty(&serde_json::json!({
            "manifest_sha256": forge_core::bytes_sha256(&manifest).to_string(),
            "bootstrap_frames": frames, "completed_steps": steps,
        "outputs": 75, "texture_outputs": 72, "buffer_outputs": 3,
        "live_paused": clock.snapshot().paused,
        "completed_frame": {"step": completed.step, "time": completed.time, "dt": completed.dt},
            "scope": "actual PreparedOcean; two textures, four cascades, nine mips, h0/waveData/foam buffers",
            "visual_parity": false
        }))?,
    )?;
    Ok(())
}

#[expect(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned resource and render-context parameter wrappers"
)]
fn copy_outputs(
    targets: Res<Targets>,
    copies: Res<Copies>,
    images: Res<RenderAssets<GpuImage>>,
    buffers: Res<RenderAssets<GpuShaderBuffer>>,
    mut context: RenderContext,
) {
    if !targets.ready.load(Ordering::Acquire) || copies.encoded.load(Ordering::Acquire) {
        return;
    }
    if targets.images.iter().any(|h| images.get(h).is_none())
        || copies.buffers.iter().any(|h| buffers.get(h).is_none())
    {
        return;
    }
    for (index, handle) in copies.buffers.iter().enumerate() {
        let mip = u32::try_from(index % 9).expect("mip bounded by nine");
        let side = 256_u32 >> mip;
        let padded = (side * 8).div_ceil(256) * 256;
        context.command_encoder().copy_texture_to_buffer(
            gpu::TexelCopyTextureInfo {
                texture: &images
                    .get(&targets.images[index / 9])
                    .expect("checked image")
                    .texture,
                mip_level: mip,
                origin: gpu::Origin3d::ZERO,
                aspect: gpu::TextureAspect::All,
            },
            gpu::TexelCopyBufferInfo {
                buffer: &buffers.get(handle).expect("checked buffer").buffer,
                layout: gpu::TexelCopyBufferLayout {
                    offset: 0,
                    bytes_per_row: Some(padded),
                    rows_per_image: Some(side),
                },
            },
            gpu::Extent3d {
                width: side,
                height: side,
                depth_or_array_layers: 4,
            },
        );
    }
    copies.encoded.store(true, Ordering::Release);
}

fn capture_frame(world: &mut World) {
    let Some(frame) = tidewater_ocean::completed_frame(world) else {
        return;
    };
    if let Ok(mut captured) = world.resource::<Output>().captured.lock() {
        *captured = Some(frame);
    }
}

#[expect(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned resource and command parameter wrappers"
)]
fn readback(
    mut commands: Commands,
    copies: Res<Copies>,
    mut output: ResMut<Output>,
    frame: Res<FrameCount>,
    mut exit: MessageWriter<AppExit>,
) {
    if frame.0 > 600 {
        output.failed = true;
        exit.write(AppExit::error());
        return;
    }
    if output.requested || !copies.encoded.load(Ordering::Acquire) {
        return;
    }
    output.requested = true;
    for (index, buffer) in copies.buffers.iter().chain(&copies.raw).enumerate() {
        commands
            .spawn((Readback::buffer(buffer.clone()), Item(index)))
            .observe(
                |event: On<ReadbackComplete>,
                 items: Query<&Item>,
                 mut output: ResMut<Output>,
                 mut commands: Commands,
                 mut exit: MessageWriter<AppExit>| {
                    let Ok(item) = items.get(event.entity) else {
                        return;
                    };
                    let index = item.0;
                    let bit = 1_u32 << index;
                    if output.received & bit != 0 {
                        return;
                    }
                    let result = write_layers(&output.root, index, &event.data);
                    if let Err(error) = result {
                        eprintln!("{error}");
                        output.failed = true;
                        exit.write(AppExit::error());
                    } else {
                        output.received |= bit;
                        if output.received == (1 << 21) - 1 {
                            output.finished.store(true, Ordering::Release);
                            exit.write(AppExit::Success);
                        }
                    }
                    commands.entity(event.entity).despawn();
                },
            );
    }
}

fn write_layers(root: &std::path::Path, index: usize, raw: &[u8]) -> std::io::Result<()> {
    use std::io::Write;
    if index >= 18 {
        let (name, expected) = match index {
            18 => ("h0.f32.bin", 256 * 256 * 4 * 16),
            19 => ("waveData.f32.bin", 256 * 256 * 4 * 16),
            20 => ("foam.f32.bin", 256 * 256 * 4 * 4),
            _ => return Err(std::io::Error::other("invalid raw ocean readback index")),
        };
        if raw.len() != expected {
            return Err(std::io::Error::other("raw ocean readback size mismatch"));
        }
        return fs::OpenOptions::new()
            .write(true)
            .create_new(true)
            .open(root.join(name))?
            .write_all(raw);
    }
    let mip = index % 9;
    let side = 256_usize >> mip;
    let row = side * 8;
    let padded = row.div_ceil(256) * 256;
    let layer_bytes = padded * side;
    if raw.len() != layer_bytes * 4 {
        return Err(std::io::Error::other("ocean mip readback size mismatch"));
    }
    let name = if index < 9 {
        "displacement"
    } else {
        "derivatives"
    };
    for (layer, data) in raw.chunks_exact(layer_bytes).enumerate() {
        let packed: Vec<_> = data
            .chunks_exact(padded)
            .flat_map(|r| r[..row].iter().copied())
            .collect();
        fs::OpenOptions::new()
            .write(true)
            .create_new(true)
            .open(root.join(format!("{name}-c{layer}-m{mip}.rgba16.bin")))?
            .write_all(&packed)?;
    }
    Ok(())
}
