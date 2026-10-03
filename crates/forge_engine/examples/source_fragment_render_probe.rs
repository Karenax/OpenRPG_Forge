//! Bounded native GPU proof for an external, SHA-pinned imported GLB.
//! Purchased payloads and captures stay outside the repository.
use bevy::{
    app::ScheduleRunnerPlugin,
    asset::{AssetPlugin, LoadState, io::embedded::EmbeddedAssetRegistry},
    camera::RenderTarget,
    core_pipeline::prepass::{DepthPrepass, MotionVectorPrepass, NormalPrepass},
    gltf::{GltfAssetLabel, gltf},
    pbr::{
        MaterialExtension, SpecializedMaterialPipelineCache,
        SpecializedPrepassMaterialPipelineCache,
    },
    prelude::*,
    render::{
        ExtractSchedule, MainWorld, RenderApp,
        render_resource::{CachedPipelineState, PipelineCache, TextureFormat, TextureUsages},
        sync_world::MainEntity,
        view::{
            ExtractedView,
            screenshot::{Screenshot, ScreenshotCaptured, save_to_disk},
        },
    },
    shader::{Shader, ShaderCacheError, ShaderRef},
    window::ExitCondition,
    winit::WinitPlugin,
    world_serialization::{WorldAsset, WorldAssetRoot},
};
use forge_engine::source_fragment::{
    SourceFragmentExtension, SourceFragmentMaterial, SourceFragmentPlugin, SourceFragmentRejected,
    prepare_source_glb,
};
use std::{path::PathBuf, time::Duration};

#[derive(Clone, Default, Resource)]
struct PipelineObservation {
    revision: u64,
    total: usize,
    pending: usize,
    failed: usize,
    errors: Vec<String>,
    source_entities: usize,
    source_forward_ready: usize,
    source_prepass_ready: usize,
    source_pipeline_records: Vec<serde_json::Value>,
}

#[derive(Resource)]
struct Proof {
    model_name: String,
    source_sha256: String,
    capture: PathBuf,
    report: PathBuf,
    bounds_min: Vec3,
    bounds_max: Vec3,
    scene: Option<Handle<WorldAsset>>,
    target: Option<Handle<Image>>,
    camera: Option<Entity>,
    last_revision: u64,
    last_total: usize,
    stable: u32,
    stable_at_capture: u32,
    camera_direction: Vec3,
    requested: bool,
    readback_received: bool,
    readback_error: Option<String>,
    foreground_pixels: usize,
    corner_background_rgba: Option<[u8; 4]>,
    shader_sources: Vec<serde_json::Value>,
}

/// Run the pinned native renderer proof, with an optional opposite camera view.
///
/// # Errors
/// Refuses changed inputs, existing outputs, unsupported view names or failed GPU proofs.
#[allow(
    clippy::too_many_lines,
    reason = "The executable keeps the pinned input, renderer setup, and proof receipt sequence together."
)]
pub fn main() -> Result<(), Box<dyn std::error::Error>> {
    let args: Vec<_> = std::env::args_os().skip(1).collect();
    if !(4..=5).contains(&args.len()) {
        return Err(
            "usage: source_fragment_render_probe GLB SHA256 CAPTURE_PNG REPORT_JSON [default|reverse]".into(),
        );
    }
    let camera_direction = match args.get(4).and_then(|value| value.to_str()) {
        None | Some("default") => Vec3::new(1.6, 1.0, 2.0).normalize(),
        Some("reverse") => -Vec3::new(1.6, 1.0, 2.0).normalize(),
        Some(_) => return Err("capture view must be default or reverse".into()),
    };
    let source = PathBuf::from(&args[0]).canonicalize()?;
    let source_sha256 = args[1].to_str().ok_or("SHA must be UTF-8")?.to_owned();
    let bytes = std::fs::read(&source)?;
    if forge_core::bytes_sha256(&bytes).to_string() != source_sha256 {
        return Err("source GLB SHA mismatch".into());
    }
    let adapted = prepare_source_glb(&bytes)?;
    let glb = gltf::Gltf::from_slice(&adapted)?;
    let scene = glb
        .default_scene()
        .or_else(|| glb.scenes().next())
        .ok_or("GLB scene missing")?;
    if scene.index() != 0 {
        return Err("probe requires the first scene".into());
    }
    let mut bounds_min = Vec3::splat(f32::INFINITY);
    let mut bounds_max = Vec3::splat(f32::NEG_INFINITY);
    for node in scene.nodes() {
        include_bounds(
            &glb.document,
            node.index(),
            Mat4::IDENTITY,
            &mut bounds_min,
            &mut bounds_max,
        )?;
    }
    let extent = bounds_max - bounds_min;
    if !extent.is_finite() || extent.min_element() < 0.0 || extent.max_element() <= 0.0 {
        return Err("finite, ordered, nonempty model bounds required".into());
    }
    let model_name = source
        .file_name()
        .and_then(|s| s.to_str())
        .ok_or("GLB filename must be UTF-8")?
        .to_owned();
    let file_path = source
        .parent()
        .and_then(|s| s.to_str())
        .ok_or("GLB parent must be UTF-8")?
        .to_owned();
    let capture = PathBuf::from(&args[2]);
    let report = PathBuf::from(&args[3]);
    if capture.exists() || report.exists() {
        return Err("proof outputs already exist; use fresh paths".into());
    }
    let mut app = App::new();
    app.add_plugins(
        DefaultPlugins
            .set(AssetPlugin {
                file_path,
                ..default()
            })
            .set(WindowPlugin {
                primary_window: None,
                exit_condition: ExitCondition::DontExit,
                ..default()
            })
            .disable::<WinitPlugin>(),
    )
    .add_plugins(ScheduleRunnerPlugin::run_loop(Duration::from_millis(16)))
    .add_plugins(SourceFragmentPlugin)
    .insert_resource(ClearColor(Color::srgb(0.14, 0.16, 0.19)))
    .insert_resource(GlobalAmbientLight {
        brightness: 250.0,
        ..default()
    })
    .init_resource::<PipelineObservation>()
    .insert_resource(Proof {
        model_name,
        source_sha256,
        capture,
        report,
        bounds_min,
        bounds_max,
        scene: None,
        target: None,
        camera: None,
        last_revision: 0,
        last_total: 0,
        stable: 0,
        stable_at_capture: 0,
        camera_direction,
        requested: false,
        readback_received: false,
        readback_error: None,
        foreground_pixels: 0,
        corner_background_rgba: None,
        shader_sources: Vec::new(),
    })
    .add_systems(Startup, setup)
    .add_systems(Update, capture_when_ready);
    let shader_sources = install_shader_proof_sources(&app)?;
    app.world_mut().resource_mut::<Proof>().shader_sources = shader_sources;
    if let Some(render_app) = app.get_sub_app_mut(RenderApp) {
        render_app.add_systems(ExtractSchedule, observe_pipelines);
    } else {
        return Err("render sub-app missing".into());
    }
    let status = app.run();
    if status.is_error() {
        return Err("GPU proof failed; inspect the report and renderer log".into());
    }
    Ok(())
}

// A proof may test a pinned copy of the product WGSL before a final rebuild.
// The renderer, material layout and loader remain those of SourceFragmentPlugin.
// Reports distinguish this diagnostic route from the compiled embedded shaders.
fn install_shader_proof_sources(
    app: &App,
) -> Result<Vec<serde_json::Value>, Box<dyn std::error::Error>> {
    let Some(root) = std::env::var_os("OPENRPG_SOURCE_SHADER_PROOF_ROOT") else {
        return Ok(Vec::new());
    };
    let root = PathBuf::from(root).canonicalize()?;
    let registry = app.world().resource::<EmbeddedAssetRegistry>();
    let mut pins = Vec::new();
    for name in [
        "source_fragment.wgsl",
        "source_fragment_common.wgsl",
        "source_fragment_prepass.wgsl",
        "source_pbr_lighting.wgsl",
    ] {
        let path = root.join(name).canonicalize()?;
        if !path.starts_with(&root) || std::fs::metadata(&path)?.len() > 128 * 1024 {
            return Err("shader proof source escapes its root or exceeds the byte limit".into());
        }
        let bytes = std::fs::read(&path)?;
        std::str::from_utf8(&bytes)?;
        pins.push(
            serde_json::json!({"path":path,"sha256":forge_core::bytes_sha256(&bytes).to_string()}),
        );
        registry.insert_asset(
            path,
            &PathBuf::from(format!("forge_engine/shaders/{name}")),
            bytes,
        );
    }
    Ok(pins)
}

fn include_bounds(
    document: &gltf::Document,
    index: usize,
    parent: Mat4,
    min: &mut Vec3,
    max: &mut Vec3,
) -> Result<(), String> {
    let node = document
        .nodes()
        .nth(index)
        .ok_or("invalid scene node index")?;
    let world = parent * Mat4::from_cols_array_2d(&node.transform().matrix());
    if let Some(mesh) = node.mesh() {
        for primitive in mesh.primitives() {
            let bounds = primitive.bounding_box();
            for x in [bounds.min[0], bounds.max[0]] {
                for y in [bounds.min[1], bounds.max[1]] {
                    for z in [bounds.min[2], bounds.max[2]] {
                        let point = world.transform_point3(Vec3::new(x, y, z));
                        *min = min.min(point);
                        *max = max.max(point);
                    }
                }
            }
        }
    }
    for child in node.children() {
        include_bounds(document, child.index(), world, min, max)?;
    }
    Ok(())
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy extracts owned resource parameters for this startup system."
)]
fn setup(
    mut commands: Commands,
    server: Res<AssetServer>,
    mut images: ResMut<Assets<Image>>,
    mut proof: ResMut<Proof>,
) {
    let center = (proof.bounds_min + proof.bounds_max) * 0.5;
    let radius = (proof.bounds_max - proof.bounds_min).length() * 0.5;
    let mut image = Image::new_target_texture(512, 512, TextureFormat::Rgba8UnormSrgb, None);
    image.texture_descriptor.usage |= TextureUsages::COPY_SRC;
    let target = images.add(image);
    proof.camera = Some(
        commands
            .spawn((
                Camera3d::default(),
                RenderTarget::Image(target.clone().into()),
                Msaa::Off,
                DepthPrepass,
                NormalPrepass,
                MotionVectorPrepass,
                Transform::from_translation(center + proof.camera_direction * radius * 3.4)
                    .looking_at(center, Vec3::Y),
            ))
            .id(),
    );
    commands.spawn((
        DirectionalLight {
            illuminance: 8_000.0,
            shadow_maps_enabled: true,
            ..default()
        },
        Transform::from_translation(center + Vec3::new(3.0, 5.0, 4.0)).looking_at(center, Vec3::Y),
    ));
    let scene: Handle<WorldAsset> =
        server.load(GltfAssetLabel::Scene(0).from_asset(proof.model_name.clone()));
    commands.spawn(WorldAssetRoot(scene.clone()));
    proof.scene = Some(scene);
    proof.target = Some(target);
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy extracts owned render resources for this render-world observation system."
)]
fn observe_pipelines(
    mut main_world: ResMut<MainWorld>,
    pipelines: Res<PipelineCache>,
    forward: Res<SpecializedMaterialPipelineCache>,
    prepass: Res<SpecializedPrepassMaterialPipelineCache>,
    views: Query<&ExtractedView>,
) {
    let entities = {
        let mut query = main_world
            .query_filtered::<Entity, (With<Mesh3d>, With<MeshMaterial3d<SourceFragmentMaterial>>)>(
            );
        query
            .iter(&main_world)
            .map(MainEntity::from)
            .collect::<Vec<_>>()
    };
    let camera = main_world.resource::<Proof>().camera;
    let server = main_world.resource::<AssetServer>();
    let forward_shader = source_shader_handle(server, SourceFragmentExtension::fragment_shader());
    let prepass_shader =
        source_shader_handle(server, SourceFragmentExtension::prepass_fragment_shader());
    let view = camera.and_then(|camera| {
        views.iter().find(|view| {
            view.retained_view_entity.main_entity == MainEntity::from(camera)
                && view.retained_view_entity.auxiliary_entity.id() == Entity::PLACEHOLDER
        })
    });
    let Some(mut observation) = main_world.get_resource_mut::<PipelineObservation>() else {
        return;
    };
    observation.revision = observation.revision.saturating_add(1);
    observation.total = 0;
    observation.pending = 0;
    observation.failed = 0;
    observation.errors.clear();
    observation.source_entities = entities.len();
    observation.source_forward_ready = 0;
    observation.source_prepass_ready = 0;
    observation.source_pipeline_records.clear();
    if let Some(view) = view {
        let view_key = view.retained_view_entity;
        for entity in entities {
            let forward_id = forward
                .get(&view_key)
                .and_then(|cache| cache.get(&entity))
                .copied();
            let prepass_id = prepass
                .get(&view_key)
                .and_then(|cache| cache.get(&entity))
                .map(|(_, id, _)| *id);
            let forward_ok = forward_id.is_some_and(|id| {
                pipelines.get_render_pipeline(id).is_some()
                    && pipelines
                        .get_render_pipeline_descriptor(id)
                        .fragment
                        .as_ref()
                        .is_some_and(|stage| forward_shader.as_ref() == Some(&stage.shader))
            });
            let prepass_ok = prepass_id.is_some_and(|id| {
                pipelines.get_render_pipeline(id).is_some()
                    && pipelines
                        .get_render_pipeline_descriptor(id)
                        .fragment
                        .as_ref()
                        .is_some_and(|stage| prepass_shader.as_ref() == Some(&stage.shader))
            });
            observation.source_forward_ready += usize::from(forward_ok);
            observation.source_prepass_ready += usize::from(prepass_ok);
            observation.source_pipeline_records.push(serde_json::json!({
                "main_entity":entity.id().to_bits(), "camera":camera.map(Entity::to_bits),
                "forward_pipeline_id":forward_id.map(|id| id.id()),
                "prepass_pipeline_id":prepass_id.map(|id| id.id()),
                "forward_state_ok_and_shader_match":forward_ok,
                "prepass_state_ok_and_shader_match":prepass_ok,
            }));
        }
    }
    for pipeline in pipelines.pipelines() {
        observation.total += 1;
        match &pipeline.state {
            CachedPipelineState::Ok(_) => {}
            CachedPipelineState::Queued | CachedPipelineState::Creating(_) => {
                observation.pending += 1;
            }
            CachedPipelineState::Err(
                ShaderCacheError::ShaderNotLoaded(_)
                | ShaderCacheError::ShaderImportNotYetAvailable,
            ) => observation.pending += 1,
            CachedPipelineState::Err(error) => {
                observation.failed += 1;
                observation.errors.push(error.to_string());
            }
        }
    }
}

fn source_shader_handle(server: &AssetServer, reference: ShaderRef) -> Option<Handle<Shader>> {
    match reference {
        ShaderRef::Path(path) => server.get_handle(path),
        ShaderRef::Handle(handle) => Some(handle),
        ShaderRef::Default => None,
    }
}

#[allow(
    clippy::too_many_arguments,
    clippy::too_many_lines,
    clippy::needless_pass_by_value,
    reason = "independent real render prerequisites"
)]
fn capture_when_ready(
    mut commands: Commands,
    server: Res<AssetServer>,
    time: Res<Time<Real>>,
    pipeline: Res<PipelineObservation>,
    meshes: Query<(), With<Mesh3d>>,
    source_materials: Query<(), With<MeshMaterial3d<SourceFragmentMaterial>>>,
    standard_materials: Query<(), With<MeshMaterial3d<StandardMaterial>>>,
    rejected: Query<&SourceFragmentRejected>,
    mut proof: ResMut<Proof>,
    mut exit: MessageWriter<AppExit>,
) {
    let failure = if let Some(reason) = rejected.iter().next() {
        Some(format!("source fragment rejected: {}", reason.0))
    } else if let Some(reason) = &proof.readback_error {
        Some(reason.clone())
    } else if pipeline.failed != 0 {
        Some("GPU pipeline compilation failed".into())
    } else if proof.scene.as_ref().is_some_and(|scene| {
        matches!(
            server.get_load_state(scene.id()),
            Some(LoadState::Failed(_))
        )
    }) {
        Some("GLB scene load failed".into())
    } else if time.elapsed_secs_f64() > 120.0 {
        Some("120 second render/readback watchdog expired".into())
    } else {
        None
    };
    if let Some(reason) = failure {
        let _ = std::fs::write(
            &proof.report,
            serde_json::to_vec_pretty(&serde_json::json!({
                "schema":"openrpg.source-fragment-gpu-proof.v1", "passed":false,
                "reason":reason, "source_sha256":proof.source_sha256,
                "pipelines_total":pipeline.total,"pipelines_failed":pipeline.failed,
                "pipeline_errors":pipeline.errors,
                "diagnostic_shader_sources":proof.shader_sources,
                "foreground_pixels":proof.foreground_pixels,
                "source_pipeline_records":pipeline.source_pipeline_records,
                "source_forward_ready":pipeline.source_forward_ready,
                "source_prepass_ready":pipeline.source_prepass_ready,
            }))
            .unwrap_or_default(),
        );
        error!("{reason}");
        exit.write(AppExit::error());
        return;
    }
    let mesh_count = meshes.iter().count();
    let source_count = source_materials.iter().count();
    let ready = proof
        .scene
        .as_ref()
        .is_some_and(|s| server.is_loaded_with_dependencies(s.id()))
        && mesh_count > 0
        && mesh_count == source_count
        && standard_materials.is_empty()
        && pipeline.total > 0
        && pipeline.pending == 0
        && pipeline.source_entities == source_count
        && pipeline.source_forward_ready == source_count
        && pipeline.source_prepass_ready == source_count;
    if !ready {
        proof.stable = 0;
    } else if pipeline.revision != proof.last_revision {
        proof.stable = if pipeline.total == proof.last_total {
            proof.stable + 1
        } else {
            1
        };
    }
    proof.last_revision = pipeline.revision;
    proof.last_total = pipeline.total;
    if !proof.requested && proof.stable >= 20 {
        proof.stable_at_capture = proof.stable;
        proof.requested = true;
        if let Some(target) = &proof.target {
            commands
                .spawn(Screenshot::image(target.clone()))
                .observe(save_to_disk(proof.capture.clone()))
                .observe(record_readback);
        }
    }
    if proof.readback_received
        && proof.readback_error.is_none()
        && proof.foreground_pixels >= 256
        && proof.capture.is_file()
        && ready
    {
        let result = serde_json::to_vec_pretty(&serde_json::json!({
            "schema":"openrpg.source-fragment-gpu-proof.v1", "passed":true,
            "source_sha256":proof.source_sha256, "capture":proof.capture,
            "meshes":mesh_count,"source_materials":source_count,
            "standard_fallback_materials":0,"rejected":0,
            "pipelines_total":pipeline.total,"pipelines_pending":pipeline.pending,"pipelines_failed":pipeline.failed,
            "stable_observations":proof.stable,"readback_received":true,
            "stable_observations_at_capture_request":proof.stable_at_capture,
            "camera_direction":proof.camera_direction.to_array(),
            "capture_view_changes_source_transform":false,
            "foreground_pixels":proof.foreground_pixels,
            "corner_background_rgba":proof.corner_background_rgba,
            "foreground_test":"at least 256 pixels differ by more than 16 from median corner RGBA",
            "source_pipeline_draw_membership_qualified":true,
            "readback_foreground_attribution":"at least one source draw; per-primitive pixel coverage is unverified",
            "source_pipeline_records":pipeline.source_pipeline_records,
            "source_forward_ready":pipeline.source_forward_ready,
            "source_prepass_ready":pipeline.source_prepass_ready,
            "prepass_output_pixels_qualified":false,
            "diagnostic_shader_sources":proof.shader_sources,
            "bounds_min":proof.bounds_min.to_array(),"bounds_max":proof.bounds_max.to_array(),
            "pixel_review":false,"unity_render_parity":false,"studio_webgpu_qualified":false,
        })).map_err(std::io::Error::other).and_then(|bytes| std::fs::write(&proof.report, bytes));
        match result {
            Ok(()) => {
                exit.write(AppExit::Success);
            }
            Err(error) => {
                error!("proof report write failed: {error}");
                exit.write(AppExit::error());
            }
        }
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy observers receive the event wrapper by value."
)]
fn record_readback(event: On<ScreenshotCaptured>, mut proof: ResMut<Proof>) {
    proof.readback_received = true;
    let image = &event.image;
    let size = image.texture_descriptor.size;
    let Some(bytes) = &image.data else {
        proof.readback_error = Some("GPU readback has no CPU image bytes".into());
        return;
    };
    if image.texture_descriptor.format != TextureFormat::Rgba8UnormSrgb
        || size.width != 512
        || size.height != 512
        || bytes.len() != 512 * 512 * 4
    {
        proof.readback_error =
            Some("GPU readback format/dimensions do not match the proof target".into());
        return;
    }
    let corners = [0, 511, 511 * 512, 512 * 512 - 1];
    let background = std::array::from_fn(|channel| {
        let mut values = corners.map(|pixel| bytes[pixel * 4 + channel]);
        values.sort_unstable();
        values[1]
    });
    proof.corner_background_rgba = Some(background);
    proof.foreground_pixels = bytes
        .as_chunks::<4>()
        .0
        .iter()
        .filter(|pixel| {
            pixel
                .iter()
                .zip(background)
                .any(|(value, reference)| value.abs_diff(reference) > 16)
        })
        .count();
    if proof.foreground_pixels < 256 {
        proof.readback_error =
            Some("GPU readback is empty or has too little visible foreground".into());
    }
}
