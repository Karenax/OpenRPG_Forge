//! Headless late replacement, disabled graph and atomic rejection proof.
#![allow(clippy::needless_pass_by_value)]
use bevy::{
    app::{AppExit, ScheduleRunnerPlugin},
    diagnostic::FrameCount,
    prelude::*,
    render::gpu_readback::{Readback, ReadbackComplete},
    window::ExitCondition,
    winit::WinitPlugin,
};
use forge_engine::{
    tidewater_material::ObjectShadowSettings,
    tidewater_object_shadow::{
        OpaqueDraw,
        graph::{self, Input, Target},
    },
};
use std::{error::Error, fs, io::Write, path::PathBuf, sync::atomic::Ordering, time::Duration};
#[derive(Resource)]
struct Proof {
    pending: Vec<Input>,
    generations: Vec<Target>,
    requested: bool,
    completed: usize,
    root: PathBuf,
}
#[derive(Component)]
struct Generation(usize);
fn input(enabled: bool) -> Input {
    let mut settings = ObjectShadowSettings::default();
    let mut draws = Vec::new();
    if enabled {
        settings.enabled = 1.0;
        let positions = [-0.5_f32, -0.5, 0.5, 0.5, -0.5, 0.5, 0.0, 0.5, 0.5];
        draws.push(
            OpaqueDraw::new(
                positions.into_iter().flat_map(f32::to_le_bytes).collect(),
                [0_u32, 1, 2]
                    .into_iter()
                    .flat_map(u32::to_le_bytes)
                    .collect(),
                Mat4::IDENTITY,
            )
            .expect("triangle"),
        );
    }
    Input {
        settings,
        draws,
        matrices: if enabled {
            vec![Mat4::IDENTITY; 3]
        } else {
            Vec::new()
        },
        eyes: if enabled {
            vec![Vec4::ZERO; 3]
        } else {
            Vec::new()
        },
        terrain: None,
        plants: None,
    }
}
fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args_os().skip(1).collect();
    if args.len() != 1 {
        return Err("NEW_OUTPUT_DIRECTORY required".into());
    }
    let root = PathBuf::from(&args[0]);
    if root.exists() {
        return Err("Output exists".into());
    }
    let mut empty = World::new();
    empty.insert_resource(Assets::<Image>::default());
    assert!(graph::stage(&mut empty, input(false)).is_err());
    assert!(empty.resource::<Assets<Image>>().is_empty());
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
    graph::register(&mut app)?;
    graph::register(&mut app)?;
    fs::create_dir(&root)?;
    app.insert_resource(Proof {
        pending: vec![input(true), input(false)],
        generations: Vec::new(),
        requested: false,
        completed: 0,
        root,
    })
    .add_systems(Update, update);
    if app.run() != AppExit::Success {
        return Err("Object shadow replacement failed".into());
    }
    Ok(())
}
fn update(world: &mut World) {
    let frame = world.resource::<FrameCount>().0;
    if frame > 600 {
        world.write_message(AppExit::error());
        return;
    }
    if frame < 8
        || world
            .get_resource::<Target>()
            .is_some_and(|v| !v.ready.load(Ordering::Acquire))
    {
        return;
    }
    if let Some(next) = world.resource_mut::<Proof>().pending.pop() {
        let previous = world.get_resource::<Target>().cloned();
        if let Some(previous) = &previous {
            let before = world.resource::<Assets<Image>>().len();
            let mut invalid = input(true);
            invalid.matrices[0] = Mat4::from_cols_array(&[f32::NAN; 16]);
            assert!(graph::stage(world, invalid).is_err());
            assert_eq!(world.resource::<Assets<Image>>().len(), before);
            assert_eq!(world.resource::<Target>().image, previous.image);
        }
        let target = graph::stage(world, next).expect("registered graph");
        assert!(!target.ready.load(Ordering::Acquire));
        if let Some(previous) = previous {
            assert_ne!(previous.image, target.image);
            assert!(previous.ready.load(Ordering::Acquire));
        }
        world.resource_mut::<Proof>().generations.push(target);
        println!("staged object shadows at frame {frame}");
        return;
    }
    if world.resource::<Proof>().completed == 2 {
        world.write_message(AppExit::Success);
        return;
    }
    if world.resource::<Proof>().requested {
        return;
    }
    world.resource_mut::<Proof>().requested = true;
    let generations = world.resource::<Proof>().generations.clone();
    for (index, target) in generations.iter().enumerate() {
        world
            .commands()
            .spawn((Readback::texture(target.image.clone()), Generation(index)))
            .observe(receive);
    }
}
fn receive(
    event: On<ReadbackComplete>,
    items: Query<&Generation>,
    mut proof: ResMut<Proof>,
    mut commands: Commands,
    mut exit: MessageWriter<AppExit>,
) {
    let index = items.get(event.entity).expect("generation").0;
    let result = (|| -> Result<(), Box<dyn Error>> {
        if index == 0 {
            if event.data.len() != 768
                || (0..3).any(|i| event.data[i * 256..i * 256 + 4] != 1.0_f32.to_le_bytes())
            {
                return Err("Disabled depth layers were not cleared".into());
            }
        } else {
            if event.data.len() != 2048 * 2048 * 3 * 4 {
                return Err("Enabled depth size differs".into());
            }
            let mut occluded = 0;
            for v in event.data.as_chunks::<4>().0 {
                let depth = f32::from_le_bytes(*v);
                if !depth.is_finite() || !(0.0..=1.0).contains(&depth) {
                    return Err("Invalid depth sample".into());
                }
                if depth < 0.9 {
                    occluded += 1;
                }
            }
            if occluded == 0 {
                return Err("Replacement caster was not rendered".into());
            }
            println!("replacement occluded samples {occluded}");
        }
        fs::OpenOptions::new()
            .create_new(true)
            .write(true)
            .open(proof.root.join(format!("generation-{index}.depth.bin")))?
            .write_all(&event.data)?;
        println!(
            "generation {index} sha256 {}",
            forge_core::bytes_sha256(&event.data)
        );
        Ok(())
    })();
    match result {
        Ok(()) => proof.completed += 1,
        Err(e) => {
            eprintln!("{e}");
            exit.write(AppExit::error());
        }
    }
    commands.entity(event.entity).despawn();
}
