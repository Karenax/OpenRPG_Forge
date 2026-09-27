//! Original atmosphere kernels executed on Bevy's device, with native parameters.
#![allow(clippy::needless_pass_by_value)]
use bevy::{
    app::{AppExit, ScheduleRunnerPlugin},
    diagnostic::FrameCount,
    prelude::*,
    render::gpu_readback::{Readback, ReadbackComplete},
    window::ExitCondition,
    winit::WinitPlugin,
};
use serde_json::Value;
use std::{
    error::Error,
    fs,
    io::Write,
    path::{Path, PathBuf},
    sync::atomic::Ordering,
    time::Duration,
};
const NAMES: [&str; 4] = ["transmittance", "multiScat", "skyView", "irradiance"];
const SIZES: [(u32, u32); 3] = [(256, 64), (32, 32), (192, 108)];
pub use forge_engine::tidewater_atmosphere::Targets;
use forge_engine::tidewater_atmosphere::{Input, install as install_input};
#[derive(Resource)]
struct Output {
    root: PathBuf,
    requested: bool,
    completed: usize,
    last_frame: u32,
}
#[derive(Component)]
struct Item {
    name: String,
    bytes: usize,
}
fn load(root: &Path, raw_source: bool) -> Result<Input, Box<dyn Error>> {
    let meta: Value = serde_json::from_slice(&fs::read(root.join("atmosphere.json"))?)?;
    forge_engine::tidewater_atmosphere::from_acquisition(&meta, raw_source, |name| {
        fs::read(root.join(name)).map_err(|e| e.to_string())
    })
}
#[allow(dead_code)]
pub fn run() -> Result<(), Box<dyn Error>> {
    let kernels = || std::array::from_fn(|_| "qualified-placeholder".to_owned());
    let mut invalid = [0.0; 16];
    invalid[7] = f32::NAN;
    if Input::new(kernels(), invalid).is_ok()
        || Input::new(std::array::from_fn(|_| String::new()), [0.0; 16]).is_ok()
    {
        return Err("atmosphere accepted invalid input".into());
    }
    let mut uninitialized = App::new();
    if install_input(&mut uninitialized, Input::new(kernels(), [0.0; 16])?).is_ok()
        || uninitialized.world().contains_resource::<Targets>()
    {
        return Err("atmosphere mutated an uninitialized renderer".into());
    }
    let args: Vec<_> = std::env::args_os().skip(1).collect();
    if !(2..=3).contains(&args.len())
        || (args.len() == 3 && args[2] != "--raw-source" && args[2] != "--reload")
    {
        return Err(
            "ATMOSPHERE_FIXTURE NEW_OUTPUT_DIRECTORY [--raw-source|--reload] required".into(),
        );
    }
    let reload = args.len() == 3 && args[2] == "--reload";
    let input = load(Path::new(&args[0]), args.len() == 3 && !reload)?;
    let root = PathBuf::from(&args[1]);
    if root.exists() {
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
    if reload {
        forge_engine::tidewater_atmosphere::register(&mut app)?;
        app.insert_resource(PendingAtmospheres(vec![
            load(Path::new(&args[0]), false)?,
            input,
        ]));
        app.add_systems(Update, stage_generations.before(readback));
    } else {
        install_input(&mut app, input)?;
    }
    fs::create_dir(&root)?;
    app.insert_resource(Output {
        root,
        requested: false,
        completed: 0,
        last_frame: 0,
    })
    .add_systems(
        Update,
        readback
            .run_if(resource_exists::<Targets>)
            .run_if(not(resource_exists::<PendingAtmospheres>)),
    );
    if app.run() != AppExit::Success {
        return Err("native atmosphere probe failed".into());
    }
    Ok(())
}
#[derive(Resource)]
struct PendingAtmospheres(Vec<Input>);

fn stage_generations(world: &mut World) {
    let frame = world.resource::<FrameCount>().0;
    if frame < 8 || !world.contains_resource::<PendingAtmospheres>() {
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
    let input = world
        .resource_mut::<PendingAtmospheres>()
        .0
        .pop()
        .expect("two generations");
    let targets =
        forge_engine::tidewater_atmosphere::stage(world, input).expect("registered renderer");
    assert!(!targets.ready.load(Ordering::Acquire));
    if let Some(previous) = previous {
        assert_ne!(previous.images, targets.images);
        assert_ne!(previous.irradiance, targets.irradiance);
        assert!(previous.ready.load(Ordering::Acquire));
    }
    println!("staged atmosphere at frame {frame}");
    if world.resource::<PendingAtmospheres>().0.is_empty() {
        world.remove_resource::<PendingAtmospheres>();
    }
}

#[allow(dead_code)]
pub fn install(app: &mut App, root: &Path) -> Result<Targets, Box<dyn Error>> {
    Ok(install_input(app, load(root, false)?)?)
}
fn readback(
    mut commands: Commands,
    targets: Res<Targets>,
    mut output: ResMut<Output>,
    frame: Res<FrameCount>,
    mut exit: MessageWriter<AppExit>,
) {
    if output.completed == 4 && frame.0 >= output.last_frame + 8 {
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
    for (index, name) in NAMES.into_iter().enumerate() {
        let (readback, bytes, extension) = if index < 3 {
            (
                Readback::texture(targets.images[index].clone()),
                (SIZES[index].0 * SIZES[index].1 * 8) as usize,
                "rgba16.bin",
            )
        } else {
            (Readback::buffer(targets.irradiance.clone()), 64, "bin")
        };
        commands
            .spawn((
                readback,
                Item {
                    name: format!("{name}.{extension}"),
                    bytes,
                },
            ))
            .observe(
                |event: On<ReadbackComplete>,
                 items: Query<&Item>,
                 mut output: ResMut<Output>,
                 frame: Res<FrameCount>,
                 mut commands: Commands,
                 mut exit: MessageWriter<AppExit>| {
                    let item = items.get(event.entity).expect("readback item");
                    let result = (|| -> std::io::Result<()> {
                        if event.data.len() != item.bytes {
                            return Err(std::io::Error::other("atmosphere readback size"));
                        }
                        fs::OpenOptions::new()
                            .write(true)
                            .create_new(true)
                            .open(output.root.join(&item.name))?
                            .write_all(&event.data)
                    })();
                    match result {
                        Ok(()) => {
                            println!(
                                "{} sha256 {}",
                                item.name,
                                forge_core::bytes_sha256(&event.data)
                            );
                            output.completed += 1;
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
