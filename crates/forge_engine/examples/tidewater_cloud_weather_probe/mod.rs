//! Original `SkyProClouds` weather and conservative bounds on Bevy's device.
#![allow(clippy::needless_pass_by_value)]
use crate::atmosphere;
use bevy::{
    app::{AppExit, ScheduleRunnerPlugin},
    diagnostic::FrameCount,
    prelude::*,
    render::{
        RenderApp,
        gpu_readback::{Readback, ReadbackComplete},
    },
    window::ExitCondition,
    winit::WinitPlugin,
};
use serde_json::Value;
use std::{
    error::Error,
    fs,
    io::Write,
    path::{Path, PathBuf},
    sync::{Arc, Mutex, atomic::Ordering},
    time::Duration,
};
mod controller;
#[path = "shadow.rs"]
mod shadow;
#[path = "trace.rs"]
mod trace;

use forge_engine::tidewater_clouds::{Input, install as install_input};
pub use forge_engine::tidewater_clouds::{SharedAtmosphere, Targets};
#[derive(Resource)]
#[allow(dead_code)]
pub struct LiveSource(pub forge_engine::tidewater_clouds::acquisition_controller::Continuation);
#[derive(Resource)]
struct Output {
    root: PathBuf,
    requested: bool,
    completed: u32,
    received: u16,
}
#[derive(Component)]
struct Item {
    index: usize,
}

#[derive(Resource)]
struct CapturedTargets(Arc<Mutex<Option<Targets>>>);

fn capture_targets(world: &mut World) {
    let Some(targets) = world.get_resource::<Targets>().cloned() else {
        return;
    };
    if let Ok(mut captured) = world.resource::<CapturedTargets>().0.lock() {
        *captured = Some(targets);
    }
}
const SIZES: [u32; 10] = [1024, 64, 256, 240, 240, 960, 960, 960, 960, 512];
const HEIGHTS: [u32; 10] = [1024, 64, 256, 135, 135, 540, 540, 540, 540, 160];
const NAMES: [&str; 10] = [
    "weather", "bounds", "shadow", "radiance", "metadata", "color0", "meta0", "color1", "meta1",
    "panorama",
];

fn checked(root: &Path, meta: &Value, name: &str) -> Result<Vec<u8>, Box<dyn Error>> {
    let bytes = fs::read(root.join(name))?;
    if bytes.len() > 2_000_000
        || meta["files"][name] != forge_core::bytes_sha256(&bytes).to_string()
    {
        return Err("cloud fixture hash or size mismatch".into());
    }
    Ok(bytes)
}
#[allow(dead_code)]
pub fn install(
    app: &mut App,
    root: &Path,
    atmosphere: SharedAtmosphere,
    view_params: &[u8],
) -> Result<Targets, Box<dyn Error>> {
    let meta: Value = serde_json::from_slice(&fs::read(root.join("environment.json"))?)?;
    if meta["schema"] != "forge.tidewater.environment-probe"
        || meta["version"] != 1
        || meta["source_commit"] != "1438b1abfcaee3267092b75573014f4d9b4a983c"
    {
        return Err("invalid cloud source".into());
    }
    let params: Vec<_> = atmosphere
        .params
        .iter()
        .flat_map(Vec4::to_array)
        .flat_map(f32::to_le_bytes)
        .collect();
    let generated = controller::generate(&meta)?;
    if view_params != forge_engine::cloud_controller::bytes(&generated[119].public) {
        return Err("cloud sampling camera parameters mismatch".into());
    }
    if params != checked(root, &meta, "cloud-step120-atmosphere.bin")? {
        return Err("scene/cloud atmosphere mismatch".into());
    }
    let settings = forge_engine::cloud_controller::bytes(&generated[0].settings);
    if settings.len() != 208 {
        return Err("cloud settings size".into());
    }
    let input = Input {
        stepped: true,
        settings,
        shaders: [
            String::from_utf8(checked(root, &meta, "cloud-weather.wgsl")?)?,
            String::from_utf8(checked(root, &meta, "cloud-bounds.wgsl")?)?,
        ],
        shadow: Some(shadow::load(root, &meta, true, true, true, true)?),
    };
    app.sub_app_mut(RenderApp).insert_resource(atmosphere);
    let targets = install_input(app, input, 10)?;
    app.insert_resource(LiveSource(
        forge_engine::tidewater_clouds::acquisition_controller::continuation(&meta)?,
    ));
    Ok(targets)
}
#[allow(clippy::too_many_lines)] // Input validation, headless Bevy setup and provenance stay together.
pub fn run() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args_os().skip(1).collect();
    if args.len() == 4
        && (args[0] == "--package"
            || args[0] == "--package-reload"
            || args[0] == "--package-preview")
    {
        return run_package(
            Path::new(&args[1]),
            &args[2].to_string_lossy(),
            Path::new(&args[3]),
            args[0] == "--package-reload",
            args[0] == "--package-preview",
        );
    }
    let stepped = args.len() == 4 && args[2] == "--native-stepped";
    let native_controller = stepped || (args.len() == 4 && args[2] == "--native-controller");
    let native_atmosphere =
        native_controller || (args.len() == 4 && args[2] == "--native-atmosphere");
    if !native_atmosphere
        && (!(2..=3).contains(&args.len())
            || (args.len() == 3
                && args[2] != "--shadow"
                && args[2] != "--trace"
                && args[2] != "--history"))
    {
        return Err(
            "CLOUD_SOURCE NEW_OUTPUT_DIRECTORY [--shadow|--trace|--history] required".into(),
        );
    }
    let with_shadow = args.len() >= 3;
    let with_history = native_atmosphere || (with_shadow && args[2] == "--history");
    let with_trace = with_shadow && (args[2] == "--trace" || with_history);
    let root = Path::new(&args[0]);
    let meta: Value = serde_json::from_slice(&fs::read(root.join("environment.json"))?)?;
    if meta["schema"] != "forge.tidewater.environment-probe"
        || meta["version"] != 1
        || meta["source_commit"] != "1438b1abfcaee3267092b75573014f4d9b4a983c"
    {
        return Err("invalid cloud source".into());
    }
    let settings = if native_controller {
        forge_engine::cloud_controller::bytes(&controller::generate(&meta)?[0].settings)
    } else {
        checked(root, &meta, "cloud-step001-settings.bin")?
    };
    if settings.len() != 208
        || settings
            .as_chunks::<4>()
            .0
            .iter()
            .any(|v| !f32::from_le_bytes(*v).is_finite())
    {
        return Err("invalid cloud settings".into());
    }
    let input = Input {
        stepped,
        shadow: if with_shadow {
            Some(shadow::load(
                root,
                &meta,
                with_trace,
                with_history,
                native_atmosphere,
                native_controller,
            )?)
        } else {
            None
        },
        settings,
        shaders: [
            String::from_utf8(checked(root, &meta, "cloud-weather.wgsl")?)?,
            String::from_utf8(checked(root, &meta, "cloud-bounds.wgsl")?)?,
        ],
    };
    let output = PathBuf::from(&args[1]);
    if output.exists() {
        return Err("output exists".into());
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
    let initial_images = app.world().resource::<Assets<Image>>().len();
    for (size, count) in [(207, 2), (208, 1)] {
        let invalid = Input {
            stepped: true,
            shaders: ["placeholder".into(), "placeholder".into()],
            settings: vec![0; size],
            shadow: None,
        };
        if install_input(&mut app, invalid, count).is_ok()
            || app.world().resource::<Assets<Image>>().len() != initial_images
            || app.world().contains_resource::<Targets>()
        {
            return Err("invalid cloud input mutated renderer assets".into());
        }
    }
    if native_atmosphere {
        let targets = atmosphere::install(&mut app, Path::new(&args[3]))?;
        let params: Vec<_> = targets
            .params
            .iter()
            .flat_map(Vec4::to_array)
            .flat_map(f32::to_le_bytes)
            .collect();
        if params != checked(root, &meta, "cloud-step120-atmosphere.bin")? {
            return Err("native atmosphere differs from cloud acquisition parameters".into());
        }
        app.sub_app_mut(RenderApp)
            .insert_resource(SharedAtmosphere {
                images: targets.images,
                ready: targets.ready,
                params: targets.params,
            });
    }
    install_input(
        &mut app,
        input,
        if with_history {
            10
        } else if with_trace {
            5
        } else if with_shadow {
            3
        } else {
            2
        },
    )?;
    fs::create_dir(&output)?;
    fs::write(
        output.join("probe.json"),
        serde_json::to_vec_pretty(&serde_json::json!({
            "schema":"forge.tidewater.native-cloud-weather", "version":1,
            "source_commit":meta["source_commit"],
            "source_manifest_sha256":forge_core::bytes_sha256(&fs::read(root.join("environment.json"))?).to_string(),
            "source_results_uploaded":with_trace && !native_atmosphere,"source_cloud_results_uploaded":false,"native_shadow":with_shadow,"native_trace":with_trace,
            "source_atmosphere_luts_uploaded":with_trace && !native_atmosphere,"native_atmosphere":native_atmosphere,
            "native_atmosphere_manifest_sha256":if native_atmosphere {Some(forge_core::bytes_sha256(&fs::read(Path::new(&args[3]).join("atmosphere.json"))?).to_string())} else {None},
            "stepped_render_graph":stepped,"native_controller":native_controller,"source_cloud_frame_uniforms_uploaded":!native_controller,
            "native_history_steps":if with_history {120} else {0},
            "native_panorama_steps":if with_history {120} else {0},
            "limits":[if native_controller {"native cloud controller; global sun/irradiance frames still acquired"} else {"source uniform replay; sun/irradiance frame values still acquired"}, "atmosphere mode declared explicitly", "fixed-pose batch; interactive render loop not qualified", "not full cloud parity"]
        }))?,
    )?;
    app.insert_resource(Output {
        root: output,
        requested: false,
        completed: 0,
        received: 0,
    })
    .add_systems(Update, readback);
    if app.run() != AppExit::Success {
        return Err("native cloud weather failed".into());
    }
    Ok(())
}
fn readback(
    mut commands: Commands,
    targets: Res<Targets>,
    mut output: ResMut<Output>,
    frame: Res<FrameCount>,
    mut exit: MessageWriter<AppExit>,
) {
    if usize::try_from(output.completed).expect("three outputs") == targets.images.len() {
        fs::write(output.root.join("execution.json"),serde_json::to_vec_pretty(&serde_json::json!({"completed_steps":targets.completed_steps.load(Ordering::Acquire),"render_graph_submissions":targets.submissions.load(Ordering::Acquire)})).expect("execution JSON")).expect("execution report");
        exit.write(AppExit::Success);
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
    for index in 0..targets.images.len() {
        commands
            .spawn((
                Readback::texture(targets.images[index].clone()),
                Item { index },
            ))
            .observe(
                |event: On<ReadbackComplete>,
                 items: Query<&Item>,
                 mut output: ResMut<Output>,
                 mut commands: Commands,
                 mut exit: MessageWriter<AppExit>| {
                    let index = items.get(event.entity).expect("readback item").index;
                    // A long GPU batch can queue several readbacks before despawn propagates.
                    let mask = 1_u16 << index;
                    if output.received & mask != 0 {
                        return;
                    }
                    output.received |= mask;
                    let result = (|| -> std::io::Result<()> {
                        let row = usize::try_from(SIZES[index] * if index > 2 { 8 } else { 4 })
                            .expect("row bytes");
                        let height = usize::try_from(HEIGHTS[index]).expect("height");
                        let padded = row.div_ceil(256) * 256;
                        if event.data.len() != padded * height {
                            return Err(std::io::Error::other("cloud readback size"));
                        }
                        let packed: Vec<u8> = event
                            .data
                            .chunks_exact(padded)
                            .flat_map(|r| r[..row].iter().copied())
                            .collect();
                        fs::OpenOptions::new()
                            .write(true)
                            .create_new(true)
                            .open(output.root.join(format!(
                                "{}.{}.bin",
                                NAMES[index],
                                match index {
                                    0 | 1 => "rgba8",
                                    2 => "r32",
                                    _ => "rgba16",
                                }
                            )))?
                            .write_all(&packed)
                    })();
                    match result {
                        Ok(()) => output.completed += 1,
                        Err(error) => {
                            eprintln!("{error}");
                            exit.write(AppExit::error());
                        }
                    }
                    commands.entity(event.entity).despawn();
                },
            );
    }
}

/// Exercise precisely the memory package path intended for Studio.
fn run_package(
    root: &Path,
    pin: &str,
    output: &Path,
    reload: bool,
    preview: bool,
) -> Result<(), Box<dyn Error>> {
    use forge_assets::tidewater_environment::EnvironmentPackage;
    use forge_engine::tidewater_scene_environment::PreparedEnvironment;
    use std::io::Read;
    if output.exists() {
        return Err("output exists".into());
    }
    let root = root.canonicalize()?;
    let mut manifest = Vec::new();
    fs::File::open(root.join("package.json"))?
        .take(256 * 1024 + 1)
        .read_to_end(&mut manifest)?;
    let package = EnvironmentPackage::load(&manifest, pin.parse()?, |name, limit| {
        let path = root.join(name).canonicalize().map_err(|e| e.to_string())?;
        if path.parent() != Some(root.as_path()) {
            return Err("environment resource escapes package".into());
        }
        let mut bytes = Vec::new();
        fs::File::open(path)
            .map_err(|e| e.to_string())?
            .take(limit as u64 + 1)
            .read_to_end(&mut bytes)
            .map_err(|e| e.to_string())?;
        Ok(bytes)
    })?;
    let prepared = PreparedEnvironment::new(&package)?;
    let bootstrap_frames = u32::try_from(package.bootstrap_frames())?;
    assert_eq!(
        prepared.environment.data.as_deref(),
        package.file("environment-cube.bin")
    );
    println!(
        "environment package prepared; cube bytes preserved, cloud sampling {:?}",
        prepared.cloud_view[0]
    );
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
    if preview {
        forge_engine::tidewater_sky::preview::install(&mut app);
        forge_engine::tidewater_atmosphere::register(&mut app)?;
        forge_engine::tidewater_clouds::register(&mut app)?;
        let resident = prepared.stage(app.world_mut())?;
        app.insert_resource(resident);
    } else if reload {
        forge_engine::tidewater_atmosphere::register(&mut app)?;
        forge_engine::tidewater_clouds::register(&mut app)?;
        app.insert_resource(PendingEnvironments(
            vec![PreparedEnvironment::new(&package)?, prepared],
            bootstrap_frames,
        ));
        app.add_systems(Update, stage_environments.before(readback));
    } else {
        let atmosphere =
            forge_engine::tidewater_atmosphere::install(&mut app, prepared.atmosphere)?;
        app.sub_app_mut(RenderApp)
            .insert_resource(SharedAtmosphere {
                images: atmosphere.images,
                params: atmosphere.params,
                ready: atmosphere.ready,
            });
        install_input(&mut app, prepared.clouds, 10)?;
    }
    fs::create_dir(output)?;
    app.insert_resource(Output {
        root: output.to_owned(),
        requested: false,
        completed: 0,
        received: 0,
    })
    .add_systems(
        Update,
        readback
            .run_if(not(resource_exists::<PreviewMode>))
            .run_if(resource_exists::<Targets>)
            .run_if(not(resource_exists::<PendingEnvironments>)),
    );
    if preview {
        app.insert_resource(PreviewMode);
        app.add_systems(Update, preview_output);
    }
    let captured = Arc::new(Mutex::new(None));
    app.insert_resource(CapturedTargets(captured.clone()));
    app.add_systems(
        Update,
        capture_targets
            .after(stage_environments)
            .before(readback)
            .before(preview_output),
    );
    if app.run() != AppExit::Success {
        return Err("package environment GPU proof failed".into());
    }
    let targets = captured
        .lock()
        .map_err(|_| "GPU result capture lock poisoned")?
        .clone()
        .ok_or("GPU result targets were not captured before runner exit")?;
    if targets.completed_steps.load(Ordering::Acquire) != bootstrap_frames
        || targets.submissions.load(Ordering::Acquire) != bootstrap_frames
        || !targets.ready.load(Ordering::Acquire)
    {
        return Err("package GPU sequence did not complete every acquired frame".into());
    }
    fs::write(
        output.join("package-execution.json"),
        serde_json::to_vec_pretty(&serde_json::json!({
            "manifest_sha256": forge_core::bytes_sha256(&manifest).to_string(),
            "bootstrap_frames": bootstrap_frames,
            "completed_steps": targets.completed_steps.load(Ordering::Acquire),
            "render_graph_submissions": targets.submissions.load(Ordering::Acquire),
            "reload": reload,
            "preview": preview,
            "visual_parity": false,
        }))?,
    )?;
    Ok(())
}

#[derive(Resource)]
struct PendingEnvironments(
    Vec<forge_engine::tidewater_scene_environment::PreparedEnvironment>,
    u32,
);

fn stage_environments(world: &mut World) {
    let frame = world.resource::<FrameCount>().0;
    if frame < 8 || !world.contains_resource::<PendingEnvironments>() {
        return;
    }
    if let Some(previous) = world.get_resource::<Targets>()
        && !previous.ready.load(Ordering::Acquire)
    {
        if frame > 600 {
            world.write_message(AppExit::error());
        }
        return;
    }
    let previous = world.get_resource::<Targets>().cloned();
    let images = world.resource::<Assets<Image>>().len();
    let invalid = Input {
        stepped: true,
        shaders: ["invalid".into(), "invalid".into()],
        settings: vec![0; 207],
        shadow: None,
    };
    assert!(forge_engine::tidewater_clouds::stage(world, invalid, 2, None).is_err());
    assert_eq!(images, world.resource::<Assets<Image>>().len());
    assert_eq!(
        previous.as_ref().map(|t| &t.images),
        world.get_resource::<Targets>().map(|t| &t.images)
    );
    let prepared = world
        .resource_mut::<PendingEnvironments>()
        .0
        .pop()
        .expect("two generations");
    let resident = prepared.stage(world).expect("registered environment");
    let targets = resident.clouds.clone();
    world.insert_resource(resident);
    assert!(!targets.ready.load(Ordering::Acquire));
    assert_eq!(targets.completed_steps.load(Ordering::Acquire), 0);
    if let Some(previous) = previous {
        assert_ne!(previous.images, targets.images);
        let expected = world.resource::<PendingEnvironments>().1;
        assert_eq!(previous.completed_steps.load(Ordering::Acquire), expected);
        assert_eq!(previous.submissions.load(Ordering::Acquire), expected);
    }
    println!("staged environment at frame {frame}");
    if world.resource::<PendingEnvironments>().0.is_empty() {
        world.remove_resource::<PendingEnvironments>();
    }
}

#[derive(Resource)]
struct PreviewMode;
#[derive(Resource)]
struct PreviewRequested;
fn preview_output(world: &mut World) {
    use forge_engine::tidewater_sky::preview;
    if world.resource::<FrameCount>().0 > 600 {
        world.write_message(AppExit::error());
        return;
    }
    if !world.contains_resource::<PreviewRequested>()
        && world.resource::<Targets>().ready.load(Ordering::Acquire)
    {
        preview::request(world).expect("resident environment");
        world.insert_resource(PreviewRequested);
    }
    if let Some(preview::Snapshot(result)) = world.remove_resource::<preview::Snapshot>() {
        match result {
            Ok(bytes) => {
                fs::write(world.resource::<Output>().root.join("sky.rgba8.bin"), bytes)
                    .expect("sky pixels");
                world.write_message(AppExit::Success);
            }
            Err(error) => {
                eprintln!("{error}");
                world.write_message(AppExit::error());
            }
        }
    }
}
