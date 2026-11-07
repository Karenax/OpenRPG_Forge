//! Late-load and replacement proof for the shared heightfield shadow graph.
#![allow(clippy::needless_pass_by_value)]
use bevy::{
    app::{AppExit, ScheduleRunnerPlugin},
    diagnostic::FrameCount,
    prelude::*,
    render::gpu_readback::{Readback, ReadbackComplete},
    window::ExitCondition,
    winit::WinitPlugin,
};
use forge_engine::source_shadows::heightfield::{self as hill, Images, PreparedInput, Settings};
use std::{error::Error, fs, io::Write, path::PathBuf, sync::atomic::Ordering, time::Duration};

#[derive(Resource)]
struct Proof {
    inputs: Vec<PreparedInput>,
    generations: Vec<Images>,
    root: PathBuf,
    expected: Vec<u8>,
    requested: bool,
    complete: usize,
    compare_engine: bool,
    received: Vec<Option<Vec<u8>>>,
}
#[derive(Component)]
struct Generation(usize);

fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args_os().skip(1).collect();
    let compare_engine = args.len() == 5 && args[4] == "--compare-engine-kernel";
    if args.len() != 4 && !compare_engine {
        return Err("SCENE_ROOT NEW_OUTPUT EXPECTED_SHADOW_BIN HEIGHTS_BIN [--compare-engine-kernel] required".into());
    }
    let source = PathBuf::from(&args[0]);
    let root = PathBuf::from(&args[1]);
    if root.exists() {
        return Err("Output already exists".into());
    }
    let meta: serde_json::Value = serde_json::from_slice(&fs::read(source.join("shadow.json"))?)?;
    let raw = fs::read(&args[3])?;
    if raw.len() != 2048 * 2048 * 4
        || meta["heights_sha256"] != forge_core::bytes_sha256(&raw).to_string()
    {
        return Err("Source heights differ".into());
    }
    let heights: Vec<_> = raw
        .as_chunks::<4>()
        .0
        .iter()
        .map(|v| f32::from_le_bytes(*v))
        .collect();
    let mips = hill::mip_chain(&heights)?;
    if meta["coarse_sha256"] != forge_core::bytes_sha256(&mips).to_string() {
        return Err("Source mip chain differs".into());
    }
    let shader = fs::read_to_string(source.join("hill-shadow.wgsl"))?;
    let sun = Vec3::from_array(serde_json::from_value::<[f32; 3]>(
        meta["sun_direction"].clone(),
    )?);
    let settings = |sun_bake| Settings {
        origin: -1024.0,
        size: 2048.0,
        sun_bake,
    };
    let mut corrupt = mips.clone();
    corrupt[0..2].copy_from_slice(&0x7c00_u16.to_le_bytes());
    if PreparedInput::new(shader.clone(), corrupt, settings(sun)).is_ok()
        || PreparedInput::new(shader.clone(), mips[..10].to_vec(), settings(sun)).is_ok()
        || PreparedInput::new(shader.clone(), mips.clone(), settings(Vec3::ZERO)).is_ok()
        || PreparedInput::new(String::new(), mips.clone(), settings(sun)).is_ok()
    {
        return Err("Invalid input accepted".into());
    }
    if !hill::SOURCE_KERNEL.ends_with(&shader) {
        return Err("Engine heightfield kernel differs from the pinned source body".into());
    }
    let actual = PreparedInput::from_heights(&heights, settings(sun))?;
    let first = PreparedInput::from_heights(&heights, settings(Vec3::Y))?;
    let baseline = PreparedInput::new(shader, mips, settings(sun))?;
    let mut empty = World::new();
    empty.insert_resource(Assets::<Image>::default());
    if actual.clone().stage(&mut empty).is_ok() || !empty.resource::<Assets<Image>>().is_empty() {
        return Err("Unregistered renderer mutated".into());
    }
    let expected = fs::read(&args[2])?;
    if expected.len() != 512 * 512 * 8 {
        return Err("Invalid expected shadow length".into());
    }
    println!(
        "expected shadow sha256 {}",
        forge_core::bytes_sha256(&expected)
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
    .add_plugins(ScheduleRunnerPlugin::run_loop(Duration::from_millis(16)))
    .add_plugins(hill::HillShadowPlugin);
    fs::create_dir(&root)?;
    app.insert_resource(Proof {
        inputs: if compare_engine {
            vec![actual, baseline, first]
        } else {
            vec![actual, first]
        },
        generations: Vec::new(),
        root,
        expected,
        requested: false,
        complete: 0,
        compare_engine,
        received: vec![None; if compare_engine { 3 } else { 2 }],
    })
    .add_systems(Update, update);
    if app.run() != AppExit::Success {
        return Err("Heightfield shadow proof failed".into());
    }
    Ok(())
}

fn update(world: &mut World) {
    let frame = world.resource::<FrameCount>().0;
    if frame > 600 {
        world.write_message(AppExit::error());
        return;
    }
    if frame < 8 {
        return;
    }
    if world
        .get_resource::<Images>()
        .is_some_and(|v| !v.ready.load(Ordering::Acquire))
    {
        return;
    }
    let input = world.resource_mut::<Proof>().inputs.pop();
    if let Some(input) = input {
        let previous = world.get_resource::<Images>().cloned();
        let next = input.stage(world).expect("initialized renderer");
        assert!(!next.ready.load(Ordering::Acquire));
        if let Some(previous) = previous {
            assert_ne!(previous.output, next.output);
            assert!(previous.ready.load(Ordering::Acquire));
        }
        world.resource_mut::<Proof>().generations.push(next);
        println!("staged heightfield shadows at frame {frame}");
        return;
    }
    if world.resource::<Proof>().complete == world.resource::<Proof>().received.len() {
        let result = finish(world.resource::<Proof>());
        if let Err(error) = &result {
            eprintln!("{error}");
        }
        world.write_message(if result.is_ok() {
            AppExit::Success
        } else {
            AppExit::error()
        });
        return;
    }
    if world.resource::<Proof>().requested {
        return;
    }
    world.resource_mut::<Proof>().requested = true;
    let generations = world.resource::<Proof>().generations.clone();
    for (index, image) in generations.iter().enumerate() {
        world
            .commands()
            .spawn((Readback::texture(image.output.clone()), Generation(index)))
            .observe(receive);
    }
}

fn finish(proof: &Proof) -> Result<(), Box<dyn Error>> {
    if !proof.compare_engine {
        return Ok(());
    }
    let first = proof.received[0]
        .as_ref()
        .ok_or("Vertical generation absent")?;
    let baseline = proof.received[1]
        .as_ref()
        .ok_or("Acquired shader baseline absent")?;
    let engine = proof.received[2]
        .as_ref()
        .ok_or("Engine kernel generation absent")?;
    let same = baseline == engine && first != baseline;
    let report = serde_json::json!({
        "schema":"forge.heightfield-engine-kernel-comparison.v1",
        "passed":same,"baseline_sha256":forge_core::bytes_sha256(baseline),
        "engine_sha256":forge_core::bytes_sha256(engine),
        "archived_reference_sha256":forge_core::bytes_sha256(&proof.expected),
        "archived_reference_equal":engine == &proof.expected,
        "archived_different_bytes":engine.iter().zip(&proof.expected).filter(|(a,b)|a!=b).count(),
        "scope":"Acquired shader and engine kernel on the same device and inputs; archived reference parity remains a separate result",
        "gate_closed":false,
    });
    fs::OpenOptions::new()
        .create_new(true)
        .write(true)
        .open(proof.root.join("comparison.json"))?
        .write_all(&serde_json::to_vec_pretty(&report)?)?;
    if !same {
        return Err("Engine kernel differs from acquired shader baseline".into());
    }
    Ok(())
}

fn receive(
    event: On<ReadbackComplete>,
    items: Query<&Generation>,
    mut proof: ResMut<Proof>,
    mut commands: Commands,
    mut exit: MessageWriter<AppExit>,
) {
    let index = items.get(event.entity).expect("readback generation").0;
    let result = (|| -> Result<(), Box<dyn Error>> {
        if event.data.len() != proof.expected.len() {
            return Err("Readback length differs".into());
        }
        fs::OpenOptions::new()
            .create_new(true)
            .write(true)
            .open(proof.root.join(format!("generation-{index}.rgba16.bin")))?
            .write_all(&event.data)?;
        println!(
            "generation {index} sha256 {}",
            forge_core::bytes_sha256(&event.data)
        );
        proof.received[index] = Some(event.data.clone());
        if !proof.compare_engine && (index == 1) != (event.data == proof.expected) {
            return Err("Replacement did not produce the expected distinct generation; actual readback retained".into());
        }
        Ok(())
    })();
    match result {
        Ok(()) => proof.complete += 1,
        Err(error) => {
            eprintln!("{error}");
            exit.write(AppExit::error());
        }
    }
    commands.entity(event.entity).despawn();
}
