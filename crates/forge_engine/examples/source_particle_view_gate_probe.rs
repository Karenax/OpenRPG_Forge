//! Headless synthetic GPU exercise of the common scene particle view gate.
//! No imported asset or visual parity claim is part of this diagnostic.

use bevy::{
    app::PluginsState,
    asset::RenderAssetUsages,
    camera::{Hdr, RenderTarget, visibility::NoFrustumCulling},
    core_pipeline::tonemapping::Tonemapping,
    light::NotShadowCaster,
    mesh::{Indices, PrimitiveTopology},
    prelude::*,
    render::{
        Render, RenderApp, RenderSystems,
        camera::TemporalJitter,
        extract_component::ExtractComponentPlugin,
        extract_resource::{ExtractResource, ExtractResourcePlugin},
        gpu_readback::{Readback, ReadbackComplete},
        pipelined_rendering::PipelinedRenderingPlugin,
        render_resource::{TextureFormat, TextureUsages},
        sync_world::MainEntity,
        view::prepare_view_uniforms,
    },
    window::{ExitCondition, WindowPlugin},
    winit::WinitPlugin,
};
use forge_assets::source_particle_transport::SourceParticleOwnerTransport;
use forge_core::bytes_sha256;
use forge_engine::{
    source_graph::{
        SOURCE_GRAPH_NORMAL, SOURCE_GRAPH_TANGENT, SOURCE_GRAPH_UV0, SourceGraphForwardState,
        SourceGraphMaterial, SourceGraphMaterialProgram, SourceGraphPlugin,
        SourceGraphSceneSampling, SourceGraphShaderArtifact, SourceGraphShaderRegistry,
        SourceGraphUniforms, register_source_graph_material,
    },
    source_graph_context::ObjectBasis,
    source_graph_host::SourceSceneHost,
    source_graph_resources::RegisteredScene,
    source_graph_runtime::{self, RuntimePolicy, SourceFrameStatus, SourceSceneRuntimePlugin},
    source_graph_scene_attachment::{AttachedScene, SceneBindings, SourceSceneAttached},
    source_particle_compute::SourceParticleProjectionPlugin,
    source_particle_host::HostedSourceParticle,
    source_particle_transport::PreparedSourceParticleOwner,
    source_refraction::{CaptureCamera, CaptureSource},
};
use std::{
    collections::{BTreeMap, BTreeSet},
    path::{Path, PathBuf},
    sync::{Arc, Mutex},
    time::{Duration, Instant},
};

pub(crate) const SIZE: u32 = 64;
const DARK: [bool; 3] = [false, false, false];
const RED: [bool; 3] = [true, false, false];
const RED_GREEN: [bool; 3] = [true, true, false];
const RGB: [bool; 3] = [true, true, true];
pub(crate) const COMPUTE: &str = r"
@group(0) @binding(0) var curve: texture_2d<f32>;
@group(0) @binding(1) var curve_sampler: sampler;
struct Bank { words: array<vec4<u32>,64>, }
@group(0) @binding(2) var<uniform> bank: Bank;
@group(0) @binding(3) var<storage,read> input_words: array<vec4<u32>>;
@group(0) @binding(4) var<storage,read_write> output_words: array<vec4<u32>>;
@compute @workgroup_size(1) fn advance() {
    output_words[0] = vec4<u32>(bitcast<u32>(1.0), 0u, 0u, 0u);
}
";
pub(crate) const PROJECT: &str = r"
@group(0) @binding(0) var curve: texture_2d<f32>;
@group(0) @binding(1) var curve_sampler: sampler;
struct Bank { words: array<vec4<u32>,64>, }
@group(0) @binding(2) var<uniform> bank: Bank;
@group(0) @binding(3) var<storage,read> input_words: array<vec4<u32>>;
@group(0) @binding(4) var<storage,read_write> output_words: array<vec4<u32>>;
@compute @workgroup_size(1) fn project() {
    output_words[0] = input_words[0];
    output_words[1] = bank.words[10];
    output_words[2] = bank.words[12];
}
";
pub(crate) const DRAW: &str = r"
@group(#{MATERIAL_BIND_GROUP}) @binding(130) var<storage,read> source_state:array<vec4<u32>>;
@vertex fn vertex(@location(0) position: vec3<f32>) -> @builtin(position) vec4<f32> {
    return vec4<f32>(position.xy, 0.5, 1.0);
}
@fragment fn fragment() -> @location(0) vec4<f32> {
    let state_visible = select(0.0, 1.0, source_state[0].x == bitcast<u32>(1.0));
    let jitter_or_rotation = select(0.0, 1.0, abs(bitcast<f32>(source_state[1].x)) > 0.001);
    let moved = select(0.0, 1.0, abs(bitcast<f32>(source_state[2].x)) > 0.1);
    return vec4<f32>(state_visible, jitter_or_rotation, moved, 1.0);
}
";

#[derive(Resource, Clone, ExtractResource)]
struct RenderCase {
    main: Entity,
    jitter: bool,
    missing_temporal_packet: bool,
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy render system parameters"
)]
fn stage_render_case(
    mut commands: Commands,
    case: Option<Res<RenderCase>>,
    views: Query<(Entity, &MainEntity), With<Camera3d>>,
) {
    let Some(case) = case else { return };
    for (entity, main) in &views {
        let mut entity = commands.entity(entity);
        if case.jitter {
            entity.insert(TemporalJitter {
                offset: Vec2::new(0.25, -0.375),
            });
        } else {
            entity.remove::<TemporalJitter>();
        }
        if case.missing_temporal_packet && main.id() == case.main {
            entity.insert(forge_engine::source_temporal::resolve::ResolveEnabled);
        } else {
            entity.remove::<forge_engine::source_temporal::resolve::ResolveEnabled>();
        }
    }
}

fn material(app: &mut App) -> Result<Handle<SourceGraphMaterial>, String> {
    let program = SourceGraphMaterialProgram {
        prepass_cull: 0,
        shaders: vec![SourceGraphShaderArtifact {
            source: DRAW.into(),
            expected_sha256: bytes_sha256(DRAW.as_bytes()),
        }],
        forward_index: 0,
        prepass_index: 0,
        required_texture_count: 0,
        color_write_mask: 15,
        forward_state: SourceGraphForwardState {
            color_src: 1,
            color_dst: 0,
            alpha_src: 1,
            alpha_dst: 0,
            cull: 0,
            compare: 4,
            write_depth: true,
        },
        original_transparent: false,
        original_alpha_test: false,
        original_alpha_to_mask: false,
        requires_original_vertex_color: false,
    };
    let uniforms = SourceGraphUniforms {
        source_object_from_target_object: Mat4::IDENTITY,
        target_object_from_source_object: Mat4::IDENTITY,
        source_world_from_target_world: Mat4::IDENTITY,
        source_absolute_world_from_target_world: Mat4::IDENTITY,
        source_time_parameters: Vec4::ZERO,
        source_previous_time_parameters: Vec4::ZERO,
        source_scale_mip_min: Vec4::ONE,
        normal_decode_modes: [Vec4::ZERO; 2],
    };
    let material = app
        .world_mut()
        .resource_scope(|world, mut registry: Mut<SourceGraphShaderRegistry>| {
            register_source_graph_material(
                &mut world.resource_mut::<Assets<Shader>>(),
                &mut registry,
                program,
                uniforms,
                vec![],
            )
        })
        .map_err(str::to_owned)?;
    Ok(app
        .world_mut()
        .resource_mut::<Assets<SourceGraphMaterial>>()
        .add(material))
}

fn particle_fixture() -> Result<PreparedSourceParticleOwner, String> {
    let mut resources = BTreeMap::new();
    let mut blob = |name: &str, bytes: Vec<u8>| {
        let pin = serde_json::json!({"path":name,"sha256":bytes_sha256(&bytes),"byte_length":bytes.len()});
        resources.insert(name.to_owned(), bytes);
        pin
    };
    let compute = blob("compute.wgsl", COMPUTE.as_bytes().to_vec());
    let project = blob("project.wgsl", PROJECT.as_bytes().to_vec());
    let bank = blob("bank.bin", vec![0; 1024]);
    let initial = blob("state.bin", vec![0; 16]);
    let atlas = blob("atlas.bin", vec![0; 8]);
    let vertices = blob("vertices.bin", vec![0; 3 * 64]);
    let declaration: SourceParticleOwnerTransport = serde_json::from_value(serde_json::json!({
        "source_component_guid":"a".repeat(32),"source_component_file_id":"11400000",
        "source_node":0,"source_primitive":0,"source_material":0,
        "compute":{"source":compute,"entry_point":"advance"},
        "curve_atlas":atlas,"curve_width":1,"curve_height":1,"curve_bank":bank.clone(),
        "initial_state":initial,"state_prefix_bytes":16,"workgroups":1,
        "control":{"delta_offset":1008,"mode_offset":1012,"stamp_offset":1016,"count_offset":1020,
            "reset_mode":3.0,"allocate_mode":2.0,"update_mode":1.0},
        "spawner":{"loop_duration_seconds":3.0,"loop_count":1,"burst_delay_seconds":0.0,
            "burst_count":1.0,"burst_active":true,"initial_play":true,"prewarm_steps":0},
        "projection":{"program":{"source":project,"entry_point":"project"},"bank":bank,
            "native_vertices":vertices,"vertex_count":3,"input_stride_bytes":64,"output_stride_bytes":112,
            "frame_layout":{"object_to_world_offset":0,"world_to_object_offset":64,
                "relative_world_to_clip_offset":128,"camera_position_offset":192,
                "evidence_sha256":bytes_sha256(b"synthetic particle view gate layout")}}
    })).map_err(|error| error.to_string())?;
    PreparedSourceParticleOwner::prepare(&declaration, &resources)
}

fn triangle() -> Mesh {
    let mut mesh = Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::default(),
    );
    mesh.insert_attribute(
        Mesh::ATTRIBUTE_POSITION,
        vec![[-0.7, -0.7, 0.0], [0.7, -0.7, 0.0], [0.0, 0.7, 0.0]],
    );
    mesh.insert_attribute(Mesh::ATTRIBUTE_UV_0, vec![[0.0, 0.0]; 3]);
    mesh.insert_attribute(SOURCE_GRAPH_NORMAL, vec![[0.0, 0.0, 1.0]; 3]);
    mesh.insert_attribute(SOURCE_GRAPH_TANGENT, vec![[1.0, 0.0, 0.0, 1.0]; 3]);
    mesh.insert_attribute(SOURCE_GRAPH_UV0, vec![[0.0; 4]; 3]);
    mesh.insert_indices(Indices::U32(vec![0, 1, 2]));
    mesh
}

pub(crate) fn image(app: &mut App) -> Handle<Image> {
    let mut image = Image::new_target_texture(SIZE, SIZE, TextureFormat::Rgba16Float, None);
    image.texture_descriptor.usage |= TextureUsages::COPY_SRC;
    app.world_mut().resource_mut::<Assets<Image>>().add(image)
}

pub(crate) fn camera(app: &mut App, target: Handle<Image>, source: Option<Entity>) -> Entity {
    let mut entity = app.world_mut().spawn((
        Camera3d::default(),
        Camera {
            clear_color: ClearColorConfig::Custom(Color::BLACK),
            ..default()
        },
        RenderTarget::Image(target.into()),
        Hdr,
        Tonemapping::None,
        Msaa::Off,
        Transform::from_xyz(0.0, 0.0, 3.0).looking_at(Vec3::ZERO, Vec3::Y),
    ));
    if let Some(view) = source {
        entity.insert((CaptureCamera, CaptureSource { view }));
    }
    entity.id()
}

pub(crate) fn mount_scene(app: &mut App) -> Result<Entity, String> {
    let material = material(app)?;
    let mesh = app
        .world_mut()
        .resource_mut::<Assets<Mesh>>()
        .add(triangle());
    let root = app
        .world_mut()
        .spawn((
            Transform::IDENTITY,
            Visibility::Inherited,
            SourceSceneAttached,
        ))
        .id();
    let node = app
        .world_mut()
        .spawn((Transform::IDENTITY, Visibility::Inherited, ChildOf(root)))
        .id();
    let draw = app
        .world_mut()
        .spawn((
            Mesh3d(mesh),
            MeshMaterial3d(material.clone()),
            Transform::IDENTITY,
            Visibility::Inherited,
            ChildOf(node),
            NoFrustumCulling,
            NotShadowCaster,
        ))
        .id();
    let particle = HostedSourceParticle::install(
        app.world_mut(),
        particle_fixture()?,
        node,
        material.clone(),
        7,
        1,
    )?;
    let host = SourceSceneHost {
        root,
        attached: AttachedScene {
            nodes: BTreeMap::from([(0, node)]),
            primitives: vec![draw],
        },
        registered: RegisteredScene {
            owned_image_ids: BTreeSet::new(),
            sampling: SourceGraphSceneSampling::default(),
            bindings: SceneBindings {
                source_materials: BTreeMap::from([((0, 0), material)]),
                ..default()
            },
            original_passes: BTreeMap::new(),
        },
        original_state: None,
        object_basis: Some(ObjectBasis {
            source_from_target: Mat4::IDENTITY,
            target_from_source: Mat4::IDENTITY,
        }),
        source_document: serde_json::json!({}),
        carrier_sha256: bytes_sha256(b"synthetic view gate carrier"),
        particle_instances: vec![particle],
        collider_projection: None,
        collider_scene_projection: None,
    };
    source_graph_runtime::retain(
        app.world_mut(),
        host,
        RuntimePolicy {
            source_world_from_target_world: Mat4::IDENTITY,
            source_absolute_world_from_target_world: Mat4::IDENTITY,
            global_mip_bias: 0.0,
            source_float_min: f32::MIN_POSITIVE,
        },
    )
    .map_err(|error| error.reason)?;
    Ok(root)
}

pub(crate) type PixelSlot = Arc<Mutex<(u64, Option<Vec<u8>>)>>;
pub(crate) type Sample = (Vec<u8>, Vec<u8>, [[usize; 3]; 2]);

pub(crate) fn readback(app: &mut App, target: Handle<Image>) -> PixelSlot {
    let output = PixelSlot::default();
    let sink = output.clone();
    app.world_mut()
        .spawn(Readback::texture(target))
        .observe(move |event: On<ReadbackComplete>| {
            if let Ok(mut bytes) = sink.lock() {
                bytes.0 = bytes.0.saturating_add(1);
                bytes.1 = Some(event.data.clone());
            }
        });
    output
}

pub(crate) fn colored_pixels(bytes: &[u8]) -> Result<[usize; 3], String> {
    let width = usize::try_from(SIZE).map_err(|error| error.to_string())?;
    if bytes.len() != width * width * 8 {
        return Err(format!("Unexpected RGBA16F readback size {}", bytes.len()));
    }
    Ok(std::array::from_fn(|channel| {
        bytes
            .as_chunks::<8>()
            .0
            .iter()
            .filter(|pixel| pixel[channel * 2] != 0 || pixel[channel * 2 + 1] != 0)
            .count()
    }))
}

fn wait_for_readbacks(
    app: &mut App,
    root: Entity,
    main: &PixelSlot,
    capture: &PixelSlot,
    expected: ([bool; 3], [bool; 3]),
    deadline: Instant,
) -> Result<Sample, String> {
    let mut stable = 0;
    let mut seen = [
        main.lock().map_err(|_| "Main readback lock poisoned")?.0,
        capture
            .lock()
            .map_err(|_| "Capture readback lock poisoned")?
            .0,
    ];
    loop {
        if Instant::now() > deadline {
            return Err(format!(
                "Timed out waiting for main/capture visibility {expected:?}"
            ));
        }
        app.update();
        if let Some(SourceFrameStatus::Refused(reason)) = app.world().get::<SourceFrameStatus>(root)
        {
            return Err(format!("Synthetic scene frame refused: {reason}"));
        }
        let a = main
            .lock()
            .map_err(|_| "Main readback lock poisoned")?
            .clone();
        let b = capture
            .lock()
            .map_err(|_| "Capture readback lock poisoned")?
            .clone();
        if a.0 > seen[0]
            && b.0 > seen[1]
            && let (Some(a_bytes), Some(b_bytes)) = (a.1, b.1)
        {
            seen = [a.0, b.0];
            let (a, b) = (a_bytes, b_bytes);
            let counts = [colored_pixels(&a)?, colored_pixels(&b)?];
            let matches = counts
                .iter()
                .zip([expected.0, expected.1])
                .all(|(counts, wanted)| {
                    counts
                        .iter()
                        .zip(wanted)
                        .all(|(&count, on)| if on { count >= 20 } else { count == 0 })
                });
            if matches {
                stable += 1;
                if stable >= 4 {
                    return Ok((a, b, counts));
                }
            } else {
                stable = 0;
            }
        }
        std::thread::sleep(Duration::from_millis(4));
    }
}

pub(crate) fn save_case(
    output: &Path,
    name: &str,
    result: Sample,
) -> Result<serde_json::Value, String> {
    let (main, capture, counts) = result;
    for (suffix, bytes) in [("main", &main), ("capture", &capture)] {
        std::fs::write(output.join(format!("{name}-{suffix}.rgba16f")), bytes)
            .map_err(|error| error.to_string())?;
    }
    Ok(
        serde_json::json!({"main_rgb_pixels":counts[0],"capture_rgb_pixels":counts[1],
        "main_sha256":bytes_sha256(&main),"capture_sha256":bytes_sha256(&capture)}),
    )
}

#[expect(
    clippy::too_many_lines,
    reason = "The ordered synthetic GPU cases share one actual scene and renderer"
)]
fn run(output: &Path) -> Result<(), String> {
    std::fs::create_dir(output).map_err(|error| error.to_string())?;
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
    .add_plugins((
        SourceGraphPlugin,
        SourceParticleProjectionPlugin,
        SourceSceneRuntimePlugin {
            capture_bevy_clock: false,
            ..default()
        },
        ExtractComponentPlugin::<CaptureCamera>::default(),
        ExtractComponentPlugin::<CaptureSource>::default(),
        ExtractResourcePlugin::<RenderCase>::default(),
    ))
    .insert_resource(source_graph_runtime::SourceClock {
        sequence: 1,
        ..default()
    });
    if !app.is_plugin_added::<PipelinedRenderingPlugin>() {
        return Err("Pipelined renderer is unavailable in this build".into());
    }
    app.get_sub_app_mut(RenderApp)
        .ok_or("RenderApp absent")?
        .add_systems(
            Render,
            stage_render_case
                .in_set(RenderSystems::PrepareViews)
                .before(prepare_view_uniforms),
        );
    let main_image = image(&mut app);
    let capture_image = image(&mut app);
    let main = camera(&mut app, main_image.clone(), None);
    let capture = camera(&mut app, capture_image.clone(), Some(main));
    app.insert_resource(RenderCase {
        main,
        jitter: false,
        missing_temporal_packet: false,
    });
    let deadline = Instant::now() + Duration::from_secs(180);
    while app.plugins_state() != PluginsState::Ready {
        if Instant::now() > deadline {
            return Err("Renderer initialization timeout".into());
        }
        std::thread::sleep(Duration::from_millis(8));
    }
    app.finish();
    let adapter = app
        .get_sub_app(RenderApp)
        .ok_or("RenderApp absent before pipelined start")?
        .world()
        .resource::<bevy::render::renderer::RenderAdapter>()
        .get_info();
    app.cleanup();
    loop {
        if Instant::now() > deadline {
            return Err("Camera viewport initialization timeout".into());
        }
        app.update();
        if [main, capture].into_iter().all(|entity| {
            app.world()
                .get::<Camera>(entity)
                .is_some_and(|camera| camera.physical_viewport_size() == Some(UVec2::splat(SIZE)))
        }) {
            break;
        }
        std::thread::sleep(Duration::from_millis(4));
    }
    let root = mount_scene(&mut app)?;
    let main_pixels = readback(&mut app, main_image);
    let capture_pixels = readback(&mut app, capture_image);
    let mut cases = serde_json::Map::new();
    cases.insert(
        "baseline".into(),
        save_case(
            output,
            "baseline",
            wait_for_readbacks(
                &mut app,
                root,
                &main_pixels,
                &capture_pixels,
                (RED, RED),
                deadline,
            )?,
        )?,
    );
    app.world_mut().entity_mut(capture).insert(CaptureSource {
        view: Entity::PLACEHOLDER,
    });
    cases.insert(
        "foreign_capture".into(),
        save_case(
            output,
            "foreign-capture",
            wait_for_readbacks(
                &mut app,
                root,
                &main_pixels,
                &capture_pixels,
                (RED, DARK),
                deadline,
            )?,
        )?,
    );
    app.world_mut()
        .entity_mut(capture)
        .insert(CaptureSource { view: main });
    app.world_mut().resource_mut::<RenderCase>().jitter = true;
    cases.insert(
        "render_jitter".into(),
        save_case(
            output,
            "render-jitter",
            wait_for_readbacks(
                &mut app,
                root,
                &main_pixels,
                &capture_pixels,
                (RED_GREEN, RED_GREEN),
                deadline,
            )?,
        )?,
    );
    let moved = Transform::from_xyz(0.4, 0.2, 3.0).looking_at(Vec3::ZERO, Vec3::Y);
    app.world_mut().entity_mut(main).insert(moved);
    app.world_mut().entity_mut(capture).insert(moved);
    cases.insert(
        "moved_render_jitter".into(),
        save_case(
            output,
            "moved-jitter",
            wait_for_readbacks(
                &mut app,
                root,
                &main_pixels,
                &capture_pixels,
                (RGB, RGB),
                deadline,
            )?,
        )?,
    );
    if app.world().get::<TemporalJitter>(main).is_some() {
        return Err("Probe jitter unexpectedly changed the main-world camera".into());
    }
    app.world_mut()
        .resource_mut::<RenderCase>()
        .missing_temporal_packet = true;
    cases.insert(
        "missing_packet".into(),
        save_case(
            output,
            "missing-packet",
            wait_for_readbacks(
                &mut app,
                root,
                &main_pixels,
                &capture_pixels,
                (DARK, DARK),
                deadline,
            )?,
        )?,
    );
    app.world_mut()
        .resource_mut::<RenderCase>()
        .missing_temporal_packet = false;
    cases.insert(
        "recovered".into(),
        save_case(
            output,
            "recovered",
            wait_for_readbacks(
                &mut app,
                root,
                &main_pixels,
                &capture_pixels,
                (RGB, RGB),
                deadline,
            )?,
        )?,
    );
    let report = serde_json::json!({
        "schema":"forge.synthetic-particle-view-gate-gpu.v1",
        "status":"GPU_SYNTHETIC_PARTICLE_VIEW_GATE_PASS",
        "adapter":{"name":adapter.name,"backend":format!("{:?}",adapter.backend),
            "vendor":adapter.vendor,"device":adapter.device,"driver":adapter.driver},
        "pipelined_rendering_enabled":true,
        "compute_sha256":bytes_sha256(COMPUTE.as_bytes()),
        "projection_sha256":bytes_sha256(PROJECT.as_bytes()),
        "draw_shader_sha256":bytes_sha256(DRAW.as_bytes()),
        "rgb_channels":"red=published state, green=projected clip xz, blue=source camera x",
        "cases":cases,
        "limitations":["synthetic shader, mesh and owner; no imported effect or authoring parity",
            "readback samples are stable repeats, not same-frame GPU timestamps",
            "render-only jitter exercised; no full temporal resolve; all shadow views are refused pending their own projection"]
    });
    std::fs::write(
        output.join("RESULT.v1.json"),
        serde_json::to_vec_pretty(&report).map_err(|error| error.to_string())?,
    )
    .map_err(|error| error.to_string())?;
    println!("GPU synthetic scene particle view checks passed");
    Ok(())
}

fn main() {
    let result = std::env::args_os()
        .nth(1)
        .ok_or("Fresh output directory required".to_owned())
        .and_then(|path| run(&PathBuf::from(path)));
    if let Err(error) = result {
        eprintln!("FAIL {error}");
        std::process::exit(1);
    }
}
