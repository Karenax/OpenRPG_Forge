//! Actual Bevy camera extraction and shared lamp-buffer readback, without a
//! village mesh, source map, Studio or visual-parity claim.
use bevy::{
    app::PluginsState,
    camera::{CameraUpdateSystems, RenderTarget},
    math::DVec3,
    prelude::*,
    render::{
        RenderApp,
        extract_component::ExtractComponentPlugin,
        pipelined_rendering::PipelinedRenderingPlugin,
        render_asset::RenderAssets,
        render_resource::{
            Buffer, BufferDescriptor, BufferUsages, CommandEncoderDescriptor, MapMode,
            TextureFormat,
        },
        renderer::{RenderDevice, RenderQueue},
        storage::GpuShaderBuffer,
        view::Msaa,
    },
    window::ExitCondition,
    winit::WinitPlugin,
};
use forge_tidewater::{
    source_local_lights::{
        Flashlight, Light, Packet, Settings, collection::Collection, scene::SourceLight,
    },
    tidewater_material::local_lights::gpu,
    tidewater_sky::solar,
    tidewater_temporal::frame::{self, Frames, ViewPacket},
};
use std::{
    error::Error,
    io::Write,
    time::{Duration, Instant},
};

#[path = "verify_building_light_upload/source_scene.rs"]
mod source_scene;

#[derive(Resource)]
struct Proof {
    camera: Entity,
    invalid_inventory: bool,
    error: Option<String>,
    orders: Vec<u64>,
}

fn collect(world: &mut World) {
    let camera = world.resource::<Proof>().camera;
    let invalid = world.resource::<Proof>().invalid_inventory;
    let mut orders = world.resource::<Proof>().orders.clone();
    if invalid {
        orders.push(u64::MAX);
    }
    let night = solar::Clock::new(solar::Settings {
        hours: 0.0,
        ..default()
    })
    .expect("Synthetic midnight settings are valid")
    .frame()
    .night;
    let settings = Settings {
        delta_seconds: 0.0,
        night,
        enabled: true,
        strength: 1.0,
        flashlight: Flashlight::default(),
    };
    let result = world.resource_scope(|world, mut collection: Mut<Collection>| {
        collection.collect(world, camera, settings, &orders)
    });
    world.resource_mut::<Proof>().error = result.err().map(|e| e.to_string());
}

fn app() -> Result<App, Box<dyn Error>> {
    let mut app = App::new();
    app.add_plugins(
        DefaultPlugins
            .set(WindowPlugin {
                primary_window: None,
                exit_condition: ExitCondition::DontExit,
                ..default()
            })
            .disable::<WinitPlugin>()
            .disable::<PipelinedRenderingPlugin>(),
    );
    gpu::register(&mut app)?;
    app.init_resource::<Frames>()
        .add_plugins(ExtractComponentPlugin::<ViewPacket>::default());
    let target = app
        .world_mut()
        .resource_mut::<Assets<Image>>()
        .add(Image::new_target_texture(
            16,
            16,
            TextureFormat::Rgba8UnormSrgb,
            None,
        ));
    let camera = app
        .world_mut()
        .spawn((
            Camera3d::default(),
            RenderTarget::Image(target.into()),
            Msaa::Off,
            Transform::default(),
        ))
        .id();
    app.world_mut().spawn((
        SourceLight {
            registration_order: 7,
            parameters: Light {
                position: DVec3::ZERO,
                color: DVec3::new(1.0, 0.7, 0.4),
                intensity: 1.5,
                range: 11.0,
                direction: None,
                cos_inner: None,
                cos_outer: None,
                enabled: true,
                scale: 1.0,
                flicker: 0.08,
                phase: 1.0,
            },
        },
        Transform::from_xyz(1.0, 2.0, -3.0),
    ));
    app.world_mut()
        .resource_mut::<Frames>()
        .activate(Some(camera));
    app.insert_resource(Proof {
        camera,
        invalid_inventory: false,
        error: None,
        orders: vec![7],
    })
    .add_systems(
        PostUpdate,
        (collect, frame::prepare_main_view)
            .chain()
            .after(bevy::transform::TransformSystems::Propagate)
            .after(CameraUpdateSystems),
    );
    let deadline = Instant::now() + Duration::from_secs(30);
    while app.plugins_state() != PluginsState::Ready {
        if Instant::now() >= deadline {
            return Err("Renderer initialization timed out".into());
        }
        std::thread::sleep(Duration::from_millis(10));
    }
    app.finish();
    app.cleanup();
    Ok(app)
}

fn readback(app: &App) -> Result<Vec<u8>, Box<dyn Error>> {
    let render = app
        .get_sub_app(RenderApp)
        .ok_or("Missing RenderApp")?
        .world();
    let device = render.resource::<RenderDevice>();
    let queue = render.resource::<RenderQueue>();
    let source = &render
        .resource::<RenderAssets<GpuShaderBuffer>>()
        .get(&gpu::BUFFER)
        .ok_or("Shared lamp buffer was not prepared")?
        .buffer;
    copy_and_read(device, queue, source)
}

fn copy_and_read(
    device: &RenderDevice,
    queue: &RenderQueue,
    source: &Buffer,
) -> Result<Vec<u8>, Box<dyn Error>> {
    let output = device.create_buffer(&BufferDescriptor {
        label: Some("lamp upload readback"),
        size: 448,
        usage: BufferUsages::COPY_DST | BufferUsages::MAP_READ,
        mapped_at_creation: false,
    });
    let mut encoder = device.create_command_encoder(&CommandEncoderDescriptor::default());
    encoder.copy_buffer_to_buffer(source, 0, &output, 0, 448);
    queue.submit([encoder.finish()]);
    let (send, receive) = std::sync::mpsc::channel();
    output.slice(..).map_async(MapMode::Read, move |result| {
        let _ = send.send(result);
    });
    let deadline = Instant::now() + Duration::from_secs(30);
    loop {
        device.poll(wgpu::PollType::Poll)?;
        if let Ok(result) = receive.try_recv() {
            result?;
            break;
        }
        if Instant::now() >= deadline {
            return Err("Lamp-buffer map timed out".into());
        }
        std::thread::sleep(Duration::from_millis(2));
    }
    let mapped = output.slice(..).get_mapped_range();
    let bytes = mapped.to_vec();
    drop(mapped);
    output.unmap();
    Ok(bytes)
}

fn configure_source(
    app: &mut App,
    args: &[String],
) -> Result<Option<serde_json::Value>, Box<dyn Error>> {
    if args.len() == 1 {
        return Ok(None);
    }
    // Replace the synthetic inventory before any source collection is published.
    let synthetic: Vec<_> = app
        .world_mut()
        .query_filtered::<Entity, With<SourceLight>>()
        .iter(app.world())
        .collect();
    for entity in synthetic {
        app.world_mut().despawn(entity);
    }
    app.world_mut().resource_mut::<Proof>().orders.clear();
    Ok(Some(source_scene::install(app, &args[1..])?))
}

fn verify_without_temporal(
    app: &mut App,
    camera: Entity,
    expected: &[u8],
    count: i32,
) -> Result<(), Box<dyn Error>> {
    // The production adapter must also work with Off/TAA: no source temporal
    // sequence and no ViewPacket may be required to publish the camera's lamps.
    app.world_mut().resource_mut::<Frames>().activate(None);
    app.update();
    if app.world().get::<ViewPacket>(camera).is_some() {
        return Err("Temporal packet survived deactivation".into());
    }
    let observation = app
        .world()
        .resource::<gpu::Status>()
        .0
        .lock()
        .map_err(|e| e.to_string())?
        .clone();
    if observation.queued_tag.is_some()
        || observation.queued_camera != Some(camera)
        || observation.queued_collection_revision.is_none()
        || observation.lights != count
        || observation.error.is_some()
    {
        return Err("Camera lamps require an active temporal packet".into());
    }
    if readback(app)? != expected {
        return Err("Non-temporal camera GPU lamps differ".into());
    }
    app.world_mut().resource_mut::<Proof>().invalid_inventory = true;
    app.update();
    if readback(app)? != Packet::default().pack()? {
        return Err("Non-temporal bad inventory did not clear GPU lamps".into());
    }
    if app
        .world()
        .resource::<gpu::Status>()
        .0
        .lock()
        .map_err(|e| e.to_string())?
        .queued_camera
        .is_some()
    {
        return Err("Non-temporal bad inventory left publication ready".into());
    }
    app.world_mut().resource_mut::<Proof>().invalid_inventory = false;
    app.update();
    if readback(app)? != expected {
        return Err("Non-temporal lamp recovery differs".into());
    }
    Ok(())
}

fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args().skip(1).collect();
    if args.len() != 1 && args.len() != 5 {
        return Err("Usage: verify_building_light_upload REPORT [BUILDING_ROOT BUILDING_SHA LIGHT_ROOT LIGHT_SHA]".into());
    }
    let output = args.first().ok_or("Expected a fresh report path")?;
    if std::path::Path::new(&output).exists() {
        return Err("Report already exists".into());
    }
    let mut app = app()?;
    let source = configure_source(&mut app, &args)?;
    let deadline = Instant::now() + Duration::from_secs(30);
    loop {
        app.update();
        let ready = app
            .world()
            .resource::<gpu::Status>()
            .0
            .lock()
            .map_err(|e| e.to_string())?
            .queued_tag
            .is_some();
        if ready {
            break;
        }
        if Instant::now() >= deadline {
            return Err("No camera-bound lamp upload before deadline".into());
        }
        std::thread::sleep(Duration::from_millis(10));
    }
    let camera = app.world().resource::<Proof>().camera;
    let view = app
        .world()
        .get::<ViewPacket>(camera)
        .ok_or("No Main camera packet")?;
    let expected = app
        .world()
        .resource::<Collection>()
        .for_view(view)?
        .packet()
        .pack()?;
    let observation = app
        .world()
        .resource::<gpu::Status>()
        .0
        .lock()
        .map_err(|e| e.to_string())?
        .clone();
    let count = app
        .world()
        .resource::<Collection>()
        .for_view(view)?
        .packet()
        .count;
    if observation.queued_tag != Some(view.tag) || observation.lights != count || count < 1 {
        return Err("Camera upload observation differs".into());
    }
    let first = readback(&app)?;
    if first != expected {
        return Err("GPU lamp bytes differ from live collection".into());
    }
    app.world_mut().resource_mut::<Proof>().invalid_inventory = true;
    app.update();
    if app.world().resource::<Proof>().error.is_none() {
        return Err("Bad inventory accepted".into());
    }
    if app
        .world()
        .resource::<gpu::Status>()
        .0
        .lock()
        .map_err(|e| e.to_string())?
        .queued_tag
        .is_some()
    {
        return Err("Bad inventory left GPU publication ready".into());
    }
    let cleared = readback(&app)?;
    if cleared != Packet::default().pack()? {
        return Err("Bad inventory did not clear GPU lamps".into());
    }
    app.world_mut().resource_mut::<Proof>().invalid_inventory = false;
    app.update();
    if readback(&app)? != expected {
        return Err("Lamp recovery differs".into());
    }
    verify_without_temporal(&mut app, camera, &expected, count)?;
    let report = serde_json::json!({"schema":"forge.building-light-upload-proof","version":1,
        "gpu_readbacks":6,"packet_bytes":448,"live_camera_identity_verified":true,
        "without_temporal_packet_verified":true,
        "non_temporal_invalid_inventory_cleared":true,"non_temporal_recovery_verified":true,
        "invalid_inventory_cleared_gpu_buffer":true,"recovery_verified":true,
        "same_bevy_render_app_device":true,"synthetic_lamp":source.is_none(),
        "source_scene":source,"selected_lights":count,
        "village_material_draw_executed":false,"studio_executed":false,"visual_parity":false,
        "packet_sha256":forge_core::bytes_sha256(&first)});
    save_report(output, &report)?;
    println!("{report}");
    Ok(())
}

fn save_report(output: &str, report: &serde_json::Value) -> Result<(), Box<dyn Error>> {
    let mut file = std::fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(output)?;
    file.write_all(&serde_json::to_vec_pretty(report)?)?;
    Ok(())
}
