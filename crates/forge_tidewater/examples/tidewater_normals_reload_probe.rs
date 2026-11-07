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
        renderer::{RenderAdapterInfo, RenderContext, RenderGraph},
        texture::GpuImage,
    },
    window::ExitCondition,
    winit::WinitPlugin,
};
use forge_engine::source_terrain::normals::{self, Shared};
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
    mip_level: u32,
}
#[derive(Resource, Default)]
struct Proof {
    stage: usize,
    requested: bool,
    previous: Option<Shared>,
}
#[derive(Component)]
struct Expected([u8; 4]);

fn case(stage: usize) -> ([u32; 2], [u8; 4]) {
    match stage {
        0 => ([2048, 2048], [37, 71, 50, 200]),
        1 => ([2048, 2048], [91, 23, 50, 180]),
        2 => ([3, 5], [17, 63, 50, 210]),
        3 => ([1, 3], [96, 51, 50, 190]),
        4 => ([7, 5], [0, 0, 50, 0]),
        _ => unreachable!("bounded normal generation cases"),
    }
}

fn independent_terrain() -> forge_world::sampled_terrain::SampledTerrainPlan {
    use forge_project::{
        HeightSamplingAlgorithm, TERRAIN_DOCUMENT_SCHEMA_VERSION_V4, TerrainExtent,
        authoring::new_terrain,
        sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
    };
    let mut doc = new_terrain("normal-probe", "authoring", "Independent terrain").unwrap();
    doc.schema_version = TERRAIN_DOCUMENT_SCHEMA_VERSION_V4;
    doc.extent_m = TerrainExtent::new(3.5, 2.5);
    doc.height_sampling.algorithm = HeightSamplingAlgorithm::SampledRasterV1;
    doc.height_sampling.sample_spacing_m = 0.5;
    doc.height_sampling.base_elevation_m = 0.0;
    doc.height_sampling.amplitude_m = 0.0;
    doc.height_sampling.base_frequency_cycles_per_m = 1.0;
    doc.height_sampling.octaves = 1;
    doc.height_sampling.lacunarity = 2.0;
    doc.height_sampling.persistence = 0.5;
    let heights = vec![0_u8; 7 * 5 * 4];
    doc.sampled_source = Some(SampledTerrainSource {
        schema: SAMPLED_TERRAIN_SCHEMA.into(),
        schema_version: 1,
        width: 7,
        height: 5,
        origin_xz_m: [-1.75, -1.25],
        sample_spacing_m: 0.5,
        outside_height_m: -12.0,
        encoding: HeightEncoding::Float32Le,
        samples_sha256: forge_core::bytes_sha256(&heights),
    });
    forge_world::sampled_terrain::SampledTerrainPlan::from_document(&doc, &heights).unwrap()
}

fn bytes(size: [u32; 2], color: [u8; 4]) -> Vec<u8> {
    let mut result = Vec::new();
    for level in 0..=size[0].max(size[1]).ilog2() {
        let width = usize::try_from((size[0] >> level).max(1)).expect("bounded width");
        let height = usize::try_from((size[1] >> level).max(1)).expect("bounded height");
        for z in 0..height {
            for x in 0..width {
                let mut pixel = if level == 0 {
                    color
                } else {
                    [0, 0, 50 + u8::try_from(level).expect("bounded levels"), 0]
                };
                if level == 0 && size == [3, 5] {
                    pixel[0] += 10 * u8::try_from(x).expect("bounded x");
                    pixel[1] += 8 * u8::try_from(z).expect("bounded z");
                }
                if level == 0 && size == [1, 3] {
                    pixel[1] += 4 * u8::try_from(z).expect("bounded z");
                }
                result.extend_from_slice(&pixel);
            }
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
    if world.resource::<Proof>().stage == 5 {
        println!("NORMAL_RELOAD_PASS: five last-mip pixels match, rock channel preserved");
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
        let (size, mut color) = case(world.resource::<Proof>().stage);
        color[2] = 50 + u8::try_from(size[0].max(size[1]).ilog2()).expect("bounded mip");
        if size == [3, 5] {
            color[0] += 10;
            color[1] += 16;
        }
        if size == [1, 3] {
            color[1] += 4;
        }
        if size == [7, 5] {
            color = [128, 128, 52, 255];
        }
        world
            .commands()
            .spawn((Readback::texture(handle), Expected(color)))
            .observe(receive);
        world.resource_mut::<Proof>().requested = true;
        return;
    }
    let (size, color) = case(world.resource::<Proof>().stage);
    let mut images = world.resource_mut::<Assets<Image>>();
    let count = images.len();
    assert!(Shared::stage(&[0; 4], true, &mut images).is_err());
    assert_eq!(images.len(), count);
    assert!(Shared::stage_with_dimensions(&[0; 4], [3, 5], true, &mut images).is_err());
    assert_eq!(images.len(), count);
    let mut chain = bytes(size, color);
    if size == [7, 5] {
        normals::update_base_with_water_level(&independent_terrain(), &mut chain, None)
            .expect("independent CPU normal and AO bake");
    }
    let shared = Shared::stage_with_dimensions(&chain, size, true, &mut images)
        .expect("complete normal mip chain");
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
        mip_level: size[0].max(size[1]).ilog2(),
    });
    world.insert_resource(shared);
    println!("normal generation staged after startup at frame {frame}");
}

fn capture(
    mut context: RenderContext,
    input: Option<Res<Capture>>,
    gpu_images: Res<RenderAssets<GpuImage>>,
    adapter: Res<RenderAdapterInfo>,
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
    assert_ne!(adapter.0.device_type, wgpu::DeviceType::Cpu);
    println!("normal GPU adapter: {:?}", adapter.0);
    let mut from = source.texture.as_image_copy();
    from.mip_level = input.mip_level;
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
        let (size, _) = case(proof.stage);
        println!(
            "generation {} mip{} {:?}",
            proof.stage,
            size[0].max(size[1]).ilog2(),
            &event.data[..4]
        );
        proof.previous = Some(current.clone());
        proof.stage += 1;
        proof.requested = false;
        commands.remove_resource::<Capture>();
    }
    commands.entity(event.entity).despawn();
}
