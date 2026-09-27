//! Real imported caster readback before/after live root edits and removal.
#![allow(clippy::needless_pass_by_value)]
use super::{MaterialHandles, prepare};
use bevy::prelude::*;
use bevy::{
    app::{AppExit, ScheduleRunnerPlugin},
    diagnostic::FrameCount,
    math::{DMat4, DQuat, DVec3},
    render::gpu_readback::{Readback, ReadbackComplete},
    window::ExitCondition,
    winit::WinitPlugin,
};
use forge_engine::{
    sun_cascades::CascadeView,
    tidewater_object_shadow::{graph, scene},
};
use forge_engine::{
    tidewater_assets::scene::{PreparedScene, ResidentScene},
    tidewater_material::VillageMaterial,
};
use std::{error::Error, fs};
use std::{path::PathBuf, sync::atomic::Ordering, time::Duration};
#[derive(Resource)]
struct Proof {
    targets: Vec<graph::Target>,
    hashes: Vec<Option<forge_core::ContentHash>>,
    root: PathBuf,
    requested: bool,
}
#[derive(Component)]
struct Generation(usize);
pub(super) fn run(
    mut cpu: App,
    materials: &MaterialHandles,
    root: PathBuf,
) -> Result<(), Box<dyn Error>> {
    if root.exists() {
        return Err("Shadow output directory already exists".into());
    }
    let sources = cpu
        .world()
        .resource::<ResidentScene>()
        .instances
        .iter()
        .map(|i| i.source.clone())
        .collect::<Vec<_>>();
    let prepared = prepare(&cpu, &sources, materials)?;
    // Reuse the validated portable geometry, never source JS or baked depth.
    let meshes = cpu
        .world_mut()
        .remove_resource::<Assets<Mesh>>()
        .ok_or("meshes")?;
    let materials = cpu
        .world_mut()
        .remove_resource::<Assets<VillageMaterial>>()
        .ok_or("materials")?;
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
    app.insert_resource(meshes).insert_resource(materials);
    graph::register(&mut app)?;
    prepared.stage(app.world_mut());
    fs::create_dir(&root)?;
    app.insert_resource(Proof {
        targets: Vec::new(),
        hashes: vec![None; 3],
        root,
        requested: false,
    })
    .add_systems(Update, update);
    if app.run() != AppExit::Success {
        return Err("Building shadow GPU proof failed".into());
    }
    Ok(())
}
fn input(world: &World) -> Result<graph::Input, String> {
    let view = CascadeView {
        world_from_view: DMat4::from_rotation_translation(
            DQuat::from_xyzw(0.0, 0.995_004_2, 0.099_833_414, 0.0).normalize(),
            DVec3::new(42.0, 14.0, -90.0),
        ),
        near: 0.06,
        fov_degrees: 62.0,
        aspect: 1920.0 / 1080.0,
    };
    scene::prepare(
        view,
        DVec3::new(-0.4, 0.8, -0.2).normalize(),
        scene::buildings(world)?,
        None,
    )
}
fn update(world: &mut World) {
    let frame = world.resource::<FrameCount>().0;
    if frame > 600 {
        world.write_message(AppExit::error());
        return;
    }
    if frame < 8
        || world
            .get_resource::<graph::Target>()
            .is_some_and(|t| !t.ready.load(Ordering::Acquire))
    {
        return;
    }
    let count = world.resource::<Proof>().targets.len();
    if count < 3 {
        if count == 1 {
            let roots = world
                .resource::<ResidentScene>()
                .instances
                .iter()
                .map(|i| i.root)
                .collect::<Vec<_>>();
            for root in roots {
                let mut transform = world.get_mut::<Transform>(root).expect("root");
                transform.translation.x += 45.0;
                transform.rotation *= Quat::from_rotation_y(0.7);
                transform.scale *= Vec3::new(1.25, 0.9, 1.1);
            }
            // Pose edits are visible immediately, before GlobalTransform propagation.
            assert_eq!(world.resource::<ResidentScene>().instances.len(), 23);
        } else if count == 2 {
            PreparedScene::default().stage(world);
        }
        let input = input(world).expect("live scene casters");
        assert_eq!(input.draws.is_empty(), count == 2);
        println!(
            "BUILDING_SHADOW_STAGE {count}: {} opaque parts",
            input.draws.len()
        );
        let target = graph::stage(world, input).expect("valid shadow generation");
        assert!(!target.ready.load(Ordering::Acquire));
        world.resource_mut::<Proof>().targets.push(target);
        return;
    }
    if world.resource::<Proof>().hashes.iter().all(Option::is_some) {
        let hashes = &world.resource::<Proof>().hashes;
        assert_ne!(hashes[0], hashes[1], "live poses must change depth");
        assert_ne!(hashes[1], hashes[2], "removal must clear depth");
        println!(
            "BUILDING_SHADOW_GPU_PASS real buildings, live translate/rotate/scale, removal, three cascades and generation retention"
        );
        world.write_message(AppExit::Success);
        return;
    }
    if world.resource::<Proof>().requested {
        return;
    }
    world.resource_mut::<Proof>().requested = true;
    let targets = world.resource::<Proof>().targets.clone();
    for (index, target) in targets.iter().enumerate() {
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
) {
    let index = items.get(event.entity).expect("generation").0;
    assert_eq!(event.data.len(), 2048 * 2048 * 3 * 4);
    let mut occluded = 0;
    for bytes in event.data.as_chunks::<4>().0 {
        let depth = f32::from_le_bytes(*bytes);
        assert!(depth.is_finite() && (0.0..=1.0).contains(&depth));
        if depth < 1.0 {
            occluded += 1;
        }
    }
    assert_eq!(occluded == 0, index == 2, "caster/removal depth");
    let hash = forge_core::bytes_sha256(&event.data);
    fs::write(
        proof.root.join(format!("generation-{index}.depth.bin")),
        &event.data,
    )
    .expect("depth proof");
    proof.hashes[index] = Some(hash);
    println!("BUILDING_SHADOW_DEPTH {index}: {occluded} occluded texels; sha256 {hash}");
    commands.entity(event.entity).despawn();
}
