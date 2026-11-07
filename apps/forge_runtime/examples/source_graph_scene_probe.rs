//! Original synthetic source-graph fixture for the common scene mount.
//! The diagnostic reveal is confined to an offscreen target and does not
//! authorize source-scene visibility in Studio or an installed module.
use bevy::{
    app::PluginsState,
    camera::RenderTarget,
    prelude::*,
    render::{
        pipelined_rendering::PipelinedRenderingPlugin,
        render_resource::{TextureFormat, TextureUsages},
        view::screenshot::{Screenshot, ScreenshotCaptured, save_to_disk},
    },
    window::ExitCondition,
    winit::WinitPlugin,
};
use forge_core::bytes_sha256;
use forge_engine::{
    source_graph::SourceGraphPlugin,
    source_graph_context::HostFrame,
    source_graph_intake::{self, SceneIntake},
    source_graph_mount::{self, SourceSceneMount},
    source_graph_placement::{self, Status},
    source_graph_runtime::{SourceFrameStatus, SourceSceneResidents, SourceSceneRuntimePlugin},
};
use serde_json::{Value, json};
use std::{
    collections::BTreeMap,
    path::{Path, PathBuf},
    sync::Arc,
    time::{Duration, Instant},
};

const SIDE: u32 = 128;
#[path = "../../../crates/forge_assets/tests/support/source_graph_fixture.rs"]
mod source_graph_fixture;
use source_graph_fixture::{FORWARD, fixture};

#[derive(Resource, Default)]
struct Capture {
    received: bool,
    red_pixels: usize,
    error: Option<String>,
}

#[expect(
    clippy::needless_pass_by_value,
    reason = "Bevy observers receive an event parameter"
)]
fn observe_capture(event: On<ScreenshotCaptured>, mut capture: ResMut<Capture>) {
    capture.received = true;
    let image = &event.image;
    let Some(bytes) = &image.data else {
        capture.error = Some("GPU readback has no image bytes".into());
        return;
    };
    let size = image.texture_descriptor.size;
    if image.texture_descriptor.format != TextureFormat::Rgba8UnormSrgb
        || size.width != SIDE
        || size.height != SIDE
        || bytes.len() != (SIDE * SIDE * 4) as usize
    {
        capture.error = Some("Unexpected readback format or dimensions".into());
        return;
    }
    capture.red_pixels = bytes
        .as_chunks::<4>()
        .0
        .iter()
        .filter(|p| p[0] > 200 && p[1] < 50 && p[2] < 50)
        .count();
}

fn inspect(bytes: &[u8]) -> Result<Value, String> {
    let media = forge_assets::media_validation::validate_source_graph_media(
        bytes,
        forge_assets::media_validation::MediaLimits::default(),
        forge_assets::source_graph_glb::SourceGraphGlbReadBudget::Standard,
    )
    .map_err(|error| error.to_string())?;
    if !matches!(
        media,
        forge_assets::media_validation::MediaInfo::Glb {
            has_geometry: true,
            ..
        }
    ) {
        return Err("Validated source graph has no geometry".into());
    }
    let SceneIntake::SourceGraph { glb, geometry } = source_graph_intake::prepare(bytes, None)?
    else {
        return Err("Generated fixture did not select the source graph".into());
    };
    Ok(json!({"sha256":bytes_sha256(bytes),"bytes":bytes.len(),
        "media_validated":true,"materials":glb.materials.len(),"nodes":geometry.hierarchy.nodes.len(),
        "primitives":geometry.instances.len(),"source_geometries":geometry.source_geometries.len()}))
}

#[expect(
    clippy::too_many_lines,
    reason = "The bounded GPU proof keeps setup, mount, capture and verification together"
)]
fn gpu(bytes: &[u8], image_path: &Path, placement_owner: bool) -> Result<Value, String> {
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
    )
    .add_plugins(SourceGraphPlugin)
    .insert_resource(ClearColor(Color::BLACK))
    .init_resource::<Capture>();
    if placement_owner {
        app.add_plugins(SourceSceneRuntimePlugin::default());
    }
    let deadline = Instant::now() + Duration::from_secs(90);
    while app.plugins_state() != PluginsState::Ready {
        if Instant::now() > deadline {
            return Err("GPU initialization timed out".into());
        }
        std::thread::sleep(Duration::from_millis(10));
    }
    app.finish();
    app.cleanup();
    app.update();

    let mut image = Image::new_target_texture(SIDE, SIDE, TextureFormat::Rgba8UnormSrgb, None);
    image.texture_descriptor.usage |= TextureUsages::COPY_SRC;
    let target = app.world_mut().resource_mut::<Assets<Image>>().add(image);
    app.world_mut().spawn((
        Camera3d::default(),
        RenderTarget::Image(target.clone().into()),
        Msaa::Off,
        Transform::from_xyz(0., 0., 3.).looking_at(Vec3::ZERO, Vec3::Y),
    ));
    let root = app
        .world_mut()
        .spawn((
            Visibility::Hidden,
            Transform::IDENTITY,
            GlobalTransform::IDENTITY,
        ))
        .id();
    let _direct_host = if placement_owner {
        {
            let mut commands = app.world_mut().commands();
            source_graph_placement::submit(&mut commands, root, Arc::from(bytes), None, None, 31);
        }
        app.world_mut().flush();
        source_graph_placement::refresh(app.world_mut());
        match app.world().get::<Status>(root) {
            Some(Status::MountedPendingRender) => {}
            Some(Status::Refused(error)) => return Err(error.clone()),
            _ => return Err("Source placement did not mount".into()),
        }
        if app
            .world()
            .get::<forge_engine::source_graph_scene_attachment::SourceSceneAttached>(root)
            .is_none()
            || !app
                .world()
                .resource::<SourceSceneResidents>()
                .contains(root)
        {
            return Err("Source placement did not retain its mounted scene".into());
        }
        None
    } else {
        let frame = HostFrame {
            source_world_from_target_world: Mat4::IDENTITY,
            source_absolute_world_from_target_world: Mat4::IDENTITY,
            time_parameters: Vec4::ZERO,
            previous_time_parameters: Vec4::ZERO,
            global_mip_bias: 0.,
            source_float_min: f32::MIN_POSITIVE,
        };
        let host = source_graph_mount::mount(
            app.world_mut(),
            SourceSceneMount {
                root,
                carrier: bytes,
                selected_scene: None,
                target_world_from_root: Mat4::IDENTITY,
                frame: &frame,
                ordinary: BTreeMap::new(),
                physics: None,
                particles: None,
                contacts: None,
                shadow_layer: 31,
            },
        )?
        .ok_or("Fixture unexpectedly selected ordinary glTF")?;
        if host.attached.nodes.len() != 1
            || host.attached.primitives.len() != 1
            || host.registered.bindings.source_materials.len() != 1
        {
            return Err("Unexpected mounted source hierarchy".into());
        }
        Some(host)
    };
    if app.world().get::<Visibility>(root) != Some(&Visibility::Hidden) {
        return Err("Source root became visible before admission".into());
    }
    // Isolated diagnostic only: production still requires pass-order admission.
    app.world_mut()
        .entity_mut(root)
        .insert(Visibility::Inherited);
    let mut requested = false;
    let mut attempts = 0_u32;
    let mut frames = 0_u32;
    while Instant::now() < deadline {
        app.update();
        frames += 1;
        if placement_owner && let Some(SourceFrameStatus::Refused(reason)) = app.world().get(root) {
            return Err(format!("Shared source scene update refused: {reason}"));
        }
        let observed = app.world().resource::<Capture>();
        if observed.received {
            if let Some(error) = &observed.error {
                return Err(error.clone());
            }
            if observed.red_pixels >= 512 && image_path.is_file() {
                return Ok(json!({"passed":true,"frames":frames,"attempts":attempts,
                    "red_pixels":observed.red_pixels,"fixture_sha256":bytes_sha256(bytes),
                    "forward_shader_sha256":bytes_sha256(FORWARD.as_bytes()),
                    "gpu_executed":true,"placement_owner":placement_owner,
                    "shared_runtime_schedule":placement_owner,
                    "production_reveal_qualified":false}));
            }
            app.world_mut().resource_mut::<Capture>().received = false;
            requested = false;
        }
        if frames >= 60 && frames.is_multiple_of(60) && !requested {
            requested = true;
            attempts += 1;
            let capture_path = image_path.to_path_buf();
            app.world_mut()
                .spawn(Screenshot::image(target.clone()))
                .observe(save_to_disk(capture_path))
                .observe(observe_capture);
        }
        std::thread::sleep(Duration::from_millis(10));
    }
    Err(format!(
        "GPU source-scene readback timed out after {frames} frames and {attempts} attempts"
    ))
}

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let mut args = std::env::args_os().skip(1);
    let mode = args
        .next()
        .ok_or("mode required: inspect, gpu or gpu-owner")?;
    let bytes = fixture()?;
    let summary = inspect(&bytes)?;
    if mode == "inspect" && args.next().is_none() {
        println!("{}", serde_json::to_string(&summary)?);
        return Ok(());
    }
    if mode != "gpu" && mode != "gpu-owner" {
        return Err("mode must be inspect, gpu or gpu-owner ABSOLUTE_NEW_OUTPUT_DIRECTORY".into());
    }
    let output = PathBuf::from(args.next().ok_or("output directory required")?);
    if args.next().is_some() || !output.is_absolute() || output.exists() {
        return Err("output must be a new absolute directory".into());
    }
    std::fs::create_dir(&output)?;
    let result = gpu(&bytes, &output.join("scene.png"), mode == "gpu-owner");
    let report = match &result {
        Ok(observation) => json!({"fixture":summary,"observation":observation}),
        Err(error) => json!({"fixture":summary,"observation":{"passed":false,"error":error}}),
    };
    std::fs::write(
        output.join("report.json"),
        serde_json::to_vec_pretty(&report)?,
    )?;
    println!("{}", serde_json::to_string(&report)?);
    result.map(|_| ()).map_err(Into::into)
}
