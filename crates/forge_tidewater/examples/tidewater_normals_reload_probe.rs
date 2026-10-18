//! Late normal-map replacement, verified by reading the last generated GPU mip.
#![allow(clippy::needless_pass_by_value)]
use bevy::{
    app::{AppExit, ScheduleRunnerPlugin},
    asset::RenderAssetUsages,
    core_pipeline::schedule::camera_driver,
    diagnostic::FrameCount,
    prelude::*,
    render::{
        RenderApp,
        extract_resource::{ExtractResource, ExtractResourcePlugin},
        gpu_readback::{Readback, ReadbackComplete},
        render_asset::RenderAssets,
        render_resource as gpu,
        renderer::{RenderContext, RenderGraph},
        texture::GpuImage,
    },
    window::ExitCondition,
    winit::WinitPlugin,
};
use forge_tidewater::tidewater_terrain::normals::{self, Shared};
use std::{
    error::Error,
    sync::{
        Arc,
        atomic::{AtomicBool, Ordering},
    },
    time::Duration,
};

#[derive(Resource, Clone, ExtractResource)]
struct Capture {
    source: Shared,
    target: Handle<Image>,
    copied: Arc<AtomicBool>,
}
#[derive(Resource, Default)]
struct Proof {
    stage: usize,
    requested: bool,
    previous: Option<Shared>,
}
#[derive(Component)]
struct Expected([u8; 4]);

fn bytes(color: [u8; 4]) -> Vec<u8> {
    let mut result = Vec::with_capacity(22_369_620);
    for level in 0_u8..12 {
        let side = 2048_usize >> level;
        let pixel = if level == 0 {
            color
        } else {
            [0, 0, 50 + level, 0]
        };
        for _ in 0..side * side {
            result.extend_from_slice(&pixel);
        }
    }
    result
}

fn main() -> Result<(), Box<dyn Error>> {
    assert!(normals::register(&mut App::new()).is_err());
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
    normals::register(&mut app)?;
    normals::register(&mut app)?;
    app.add_plugins(ExtractResourcePlugin::<Capture>::default());
    app.sub_app_mut(RenderApp)
        .add_systems(RenderGraph, capture.after(camera_driver));
    app.init_resource::<Proof>().add_systems(Update, update);
    if app.run() != AppExit::Success {
        return Err("Normal replacement GPU verification failed".into());
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
    if world.resource::<Proof>().stage == 2 {
        println!("NORMAL_RELOAD_PASS: both last-mip pixels match, rock channel preserved");
        world.write_message(AppExit::Success);
        return;
    }
    if world.resource::<Proof>().requested {
        return;
    }
    if let Some(capture) = world.get_resource::<Capture>() {
        if !capture.copied.load(Ordering::Acquire) {
            return;
        }
        let handle = capture.target.clone();
        let color = if world.resource::<Proof>().stage == 0 {
            [37, 71, 61, 200]
        } else {
            [91, 23, 61, 180]
        };
        world
            .commands()
            .spawn((Readback::texture(handle), Expected(color)))
            .observe(receive);
        world.resource_mut::<Proof>().requested = true;
        return;
    }
    let color = if world.resource::<Proof>().stage == 0 {
        [37, 71, 50, 200]
    } else {
        [91, 23, 50, 180]
    };
    let mut images = world.resource_mut::<Assets<Image>>();
    let count = images.len();
    assert!(Shared::stage(&[0; 4], true, &mut images).is_err());
    assert_eq!(images.len(), count);
    let shared =
        Shared::stage(&bytes(color), true, &mut images).expect("complete normal mip chain");
    images
        .get_mut(&shared.image)
        .expect("staged image")
        .texture_descriptor
        .usage |= gpu::TextureUsages::COPY_SRC;
    let mut output = Image::new_fill(
        gpu::Extent3d {
            width: 1,
            height: 1,
            depth_or_array_layers: 1,
        },
        gpu::TextureDimension::D2,
        &[0; 4],
        gpu::TextureFormat::Rgba8Unorm,
        RenderAssetUsages::default(),
    );
    output.texture_descriptor.usage |= gpu::TextureUsages::COPY_DST | gpu::TextureUsages::COPY_SRC;
    let target = images.add(output);
    assert!(!shared.ready.load(Ordering::Acquire));
    if let Some(old) = &world.resource::<Proof>().previous {
        assert_ne!(old.image, shared.image);
        assert!(!Arc::ptr_eq(&old.ready, &shared.ready));
        assert!(old.ready.load(Ordering::Acquire));
    }
    world.insert_resource(Capture {
        source: shared.clone(),
        target,
        copied: Arc::new(AtomicBool::new(false)),
    });
    world.insert_resource(shared);
    println!("normal generation staged after startup at frame {frame}");
}

fn capture(
    mut context: RenderContext,
    input: Option<Res<Capture>>,
    gpu_images: Res<RenderAssets<GpuImage>>,
) {
    let Some(input) = input else {
        return;
    };
    if input.copied.load(Ordering::Acquire) || !input.source.ready.load(Ordering::Acquire) {
        return;
    }
    let (Some(source), Some(target)) = (
        gpu_images.get(&input.source.image),
        gpu_images.get(&input.target),
    ) else {
        return;
    };
    let mut from = source.texture.as_image_copy();
    from.mip_level = 11;
    context.command_encoder().copy_texture_to_texture(
        from,
        target.texture.as_image_copy(),
        gpu::Extent3d {
            width: 1,
            height: 1,
            depth_or_array_layers: 1,
        },
    );
    input.copied.store(true, Ordering::Release);
}

fn receive(
    event: On<ReadbackComplete>,
    expected: Query<&Expected>,
    mut proof: ResMut<Proof>,
    current: Res<Shared>,
    mut commands: Commands,
    mut exit: MessageWriter<AppExit>,
) {
    let expected = expected.get(event.entity).expect("readback expectation").0;
    if event.data.len() != 256 || event.data[..4] != expected {
        eprintln!(
            "last normal mip differs: {:?}, expected {expected:?}",
            &event.data[..event.data.len().min(4)]
        );
        exit.write(AppExit::error());
    } else {
        println!("generation {} mip11 {:?}", proof.stage, &event.data[..4]);
        proof.previous = Some(current.clone());
        proof.stage += 1;
        proof.requested = false;
        commands.remove_resource::<Capture>();
    }
    commands.entity(event.entity).despawn();
}
