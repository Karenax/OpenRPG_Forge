//! Actual GPU rendering of the pinned source prototype profile with world PBR.
//! Uses the existing installed package without copying its resources. This
//! isolates the material; it does not qualify module admission, raster or FPS.
use bevy::{
    app::PluginsState,
    camera::RenderTarget,
    core_pipeline::{
        prepass::{DepthPrepass, MotionVectorPrepass, NormalPrepass},
        tonemapping::DebandDither,
    },
    light::{CascadeShadowConfigBuilder, DirectionalLightShadowMap},
    pbr::MaterialPlugin,
    prelude::*,
    render::{
        RenderApp,
        pipelined_rendering::PipelinedRenderingPlugin,
        render_resource::{
            BindingType, BufferBindingType, CachedPipelineState, PipelineCache, PipelineDescriptor,
            ShaderStages, TextureFormat, TextureUsages,
        },
        renderer::{RenderAdapterInfo, RenderDevice},
        view::screenshot::{Screenshot, ScreenshotCaptured, save_to_disk},
    },
    window::ExitCondition,
    winit::WinitPlugin,
};
use forge_core::{CommandId, EntityId, bytes_sha256};
use forge_engine::render_inventory::{self, RenderInventoryPlugin};
use forge_engine::source_lighting::frame::Frame;
use forge_engine::source_vegetation::Params;
use forge_engine::source_vegetation::Record;
use forge_engine::source_vegetation::scene::streamed::Prepared;
use forge_engine::source_vegetation::scene::streamed::Resident;
use forge_engine::source_vegetation::textures::PreparedTextures;
use forge_engine::source_vegetation::world::WorldPlantMaterial;
use forge_tidewater::tidewater_plants::scene;
use forge_tidewater_assets::tidewater_plants::PlantPackage;
use std::{
    collections::BTreeMap,
    error::Error,
    fs,
    path::{Path, PathBuf},
    sync::{Arc, Mutex},
    time::{Duration, Instant},
};
type Result<T> = std::result::Result<T, Box<dyn Error>>;
type Captures = Arc<Mutex<BTreeMap<String, Vec<u8>>>>;

#[derive(Resource)]
struct HostFrame {
    state: Frame,
    buffer: Handle<bevy::render::storage::ShaderBuffer>,
    camera: Entity,
}

fn publish_frame(world: &mut World) {
    world.resource_scope(|world, mut host: Mut<HostFrame>| {
        let position = world
            .get::<Transform>(host.camera)
            .expect("proof camera")
            .translation;
        let handle = host.buffer.clone();
        host.state
            .update(
                world,
                &handle,
                position,
                Vec4::new(1.0, 0.0, 4.0, 2.0),
                Vec4::ZERO,
            )
            .expect("valid owned proof frame");
    });
}

fn advance(app: &mut App) -> Result<bool> {
    app.update();
    let cache = app
        .get_sub_app(RenderApp)
        .ok_or("Render app absent")?
        .world()
        .resource::<PipelineCache>();
    let mut total = 0;
    let mut ready = true;
    for pipeline in cache.pipelines() {
        total += 1;
        match &pipeline.state {
            CachedPipelineState::Ok(_) => {}
            CachedPipelineState::Err(
                bevy::shader::ShaderCacheError::ShaderNotLoaded(_)
                | bevy::shader::ShaderCacheError::ShaderImportNotYetAvailable,
            ) => ready = false,
            CachedPipelineState::Err(error) => {
                return Err(format!("World plant pipeline: {error}").into());
            }
            _ => ready = false,
        }
    }
    std::thread::sleep(Duration::from_millis(16));
    Ok(total > 0 && ready)
}

fn plant_pipelines(app: &App) -> Result<Vec<serde_json::Value>> {
    let cache = app.sub_app(RenderApp).world().resource::<PipelineCache>();
    let limits = app
        .sub_app(RenderApp)
        .world()
        .resource::<RenderDevice>()
        .limits();
    let mut result = Vec::new();
    for pipeline in cache.pipelines() {
        let PipelineDescriptor::RenderPipelineDescriptor(descriptor) = &pipeline.descriptor else {
            continue;
        };
        if descriptor.vertex.shader != forge_engine::source_vegetation::world::SHADER {
            continue;
        }
        let defines = &descriptor.vertex.shader_defs;
        let kind = (0..=2)
            .find(|kind| defines.contains(&format!("PLANT_KIND_{kind}").into()))
            .ok_or("Native plant pipeline lacks a supported kind")?;
        let prepass = defines.contains(&"PREPASS_PIPELINE".into());
        let normal = defines.contains(&"NORMAL_PREPASS".into());
        let motion = defines.contains(&"MOTION_VECTOR_PREPASS".into());
        let storage_count = |stage| {
            descriptor
                .layout
                .iter()
                .flat_map(|layout| &layout.entries)
                .filter(|entry| {
                    entry.visibility.contains(stage)
                        && matches!(
                            entry.ty,
                            BindingType::Buffer {
                                ty: BufferBindingType::Storage { .. },
                                ..
                            }
                        )
                })
                .count()
        };
        let vertex_storage = storage_count(ShaderStages::VERTEX);
        let fragment_storage = storage_count(ShaderStages::FRAGMENT);
        let available = usize::try_from(limits.max_storage_buffers_per_shader_stage)?;
        if vertex_storage > available || fragment_storage > available {
            return Err("Plant pipeline exceeds its actual device storage limit".into());
        }
        let local_bindings: Vec<_> = descriptor
            .layout
            .iter()
            .enumerate()
            .flat_map(|(group, layout)| {
                layout.entries.iter().filter_map(move |entry| {
                    (entry.binding == 56
                        && matches!(
                            entry.ty,
                            BindingType::Buffer {
                                ty: BufferBindingType::Storage { read_only: true },
                                ..
                            }
                        ))
                    .then_some(serde_json::json!({"group":group,"binding":56,
                        "fragment_only":entry.visibility == ShaderStages::FRAGMENT}))
                })
            })
            .collect();
        if local_bindings.len() != 1 || local_bindings[0]["fragment_only"] != true {
            return Err("Plant pipeline lacks its unique fragment-only local-light binding".into());
        }
        result.push(serde_json::json!({"kind":kind,"prepass":prepass,"normal":normal,"motion":motion,
            "shadow_depth":prepass && !normal && !motion,
            "ready":matches!(pipeline.state,CachedPipelineState::Ok(_)),
            "storage_buffers":{"vertex":vertex_storage,"fragment":fragment_storage,
                "device_limit":available,"webgpu_default_limit":wgpu::Limits::default().max_storage_buffers_per_shader_stage},
            "local_lights":local_bindings,
            "fragment_matches":descriptor.fragment.as_ref().is_some_and(|stage|stage.shader==forge_engine::source_vegetation::world::SHADER)}));
    }
    Ok(result)
}

fn capture(
    app: &mut App,
    target: &Handle<Image>,
    output: &Path,
    name: &str,
    captures: &Captures,
) -> Result<()> {
    let results = captures.clone();
    let key = name.to_owned();
    app.world_mut()
        .spawn(Screenshot::image(target.clone()))
        .observe(save_to_disk(output.join(format!("{name}.png"))))
        .observe(move |event: On<ScreenshotCaptured>| {
            if let Some(bytes) = &event.image.data
                && let Ok(mut results) = results.lock()
            {
                results.insert(key.clone(), bytes.clone());
            }
        });
    let deadline = Instant::now() + Duration::from_secs(30);
    while !captures
        .lock()
        .map_err(|e| e.to_string())?
        .contains_key(name)
    {
        advance(app)?;
        if Instant::now() > deadline {
            return Err("Plant readback timed out".into());
        }
    }
    Ok(())
}

// The transport-only fixture encoded a zero signed canopy stretch. Preserve
// it on disk and repair only this explicitly identified in-memory GPU fixture.
fn repair_fixture(
    vegetation: &mut forge_project::VegetationDocument,
    blobs: &mut BTreeMap<forge_core::ContentHash, Vec<u8>>,
) -> Result<usize> {
    let mut repaired = 0;
    let extent = vegetation.extent_m;
    for block in &mut vegetation
        .imported_source
        .as_mut()
        .ok_or("Proof source absent")?
        .blocks
    {
        if block.prototype_key != "tidewater_trees" {
            return Err("Unexpected proof prototype".into());
        }
        let original = blobs
            .get(&block.instances_sha256)
            .ok_or("Proof source block absent")?;
        let mut records = block.decode(original, extent)?;
        let mut bytes = Vec::new();
        for record in &mut records {
            if record.parameters[1] == 0.0 {
                record.parameters[1] = 1.0;
                repaired += 1;
            }
            bytes.extend_from_slice(record.source_key.as_bytes());
            for value in record
                .position_m
                .iter()
                .chain(&record.rotation_xyzw)
                .chain(&record.scale)
                .chain(&record.parameters)
            {
                bytes.extend_from_slice(&value.to_le_bytes());
            }
        }
        block.instances_sha256 = bytes_sha256(&bytes);
        blobs.insert(block.instances_sha256, bytes);
    }
    Ok(repaired)
}

/// Additional, explicitly synthetic source-profile cases share the same closed
/// prototype resources. No module/index file is written or claimed exported.
fn add_bound_profiles(
    vegetation: &mut forge_project::VegetationDocument,
    blobs: &mut BTreeMap<forge_core::ContentHash, Vec<u8>>,
) -> Result<()> {
    use forge_project::{
        VegetationLayerId,
        imported_vegetation::{ImportedInstanceEncoding, ImportedVegetationBlock},
    };
    let template = vegetation.layers[0].clone();
    let source = vegetation
        .imported_source
        .as_mut()
        .ok_or("Proof source absent")?;
    for (index, (key, lean, height, order)) in [
        ("tidewater_palms", 0.1, 8.0, 0.0),
        ("tidewater_bananas", 0.0, 1.0, 0.0),
        ("tidewater_young_palms", 0.1, 4.0, 0.0),
        ("tidewater_ferns", 0.0, 1.0, 1.0),
        ("tidewater_monsteras", 0.0, 1.0, 0.0),
        ("tidewater_elephant_ears", 0.0, 2.0, 0.0),
        ("tidewater_heliconias", 0.0, 2.0, 1.0),
        ("tidewater_strelitzias", 0.0, 2.0, 2.0),
        ("tidewater_shrubs", -1.0, 1.0, 12.0),
    ]
    .into_iter()
    .enumerate()
    {
        let mut layer = template.clone();
        layer.id = VegetationLayerId::new(format!("bound_profile_{index}"));
        let id = layer.id.to_string();
        vegetation.layers.push(layer);
        let mut bytes = bytes_sha256(key.as_bytes()).as_bytes().to_vec();
        for value in [
            190.0_f64, 0.0, 190.0, 0.0, 0.0, 0.0, 1.0, 1.0, 1.0, 1.0, 0.0, lean, height, 0.25, 1.0,
            order,
        ] {
            bytes.extend(value.to_le_bytes());
        }
        let pin = bytes_sha256(&bytes);
        blobs.insert(pin, bytes);
        let mut names: Vec<String> = forge_assets::source_vegetation_package::profile::PARAMETERS
            .iter()
            .map(|s| (*s).into())
            .collect();
        names.push("source_order".into());
        source.blocks.push(ImportedVegetationBlock {
            layer_id: id,
            prototype_key: key.into(),
            count: 1,
            encoding: ImportedInstanceEncoding::TransformF64LeV1,
            instances_sha256: pin,
            parameter_names: names,
        });
    }
    Ok(())
}

#[expect(
    clippy::too_many_lines,
    reason = "closed in-memory fixture and actual prototype-envelope inventory"
)]
fn fixture() -> Result<(PlantPackage, Prepared, Vec3, serde_json::Value)> {
    use forge_project::{
        HeightSamplingAlgorithm,
        sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
    };
    let root = PathBuf::from(
        std::env::var_os("FORGE_PLANT_MODULE_ROOT")
            .ok_or("Explicit installed module root required")?,
    );
    if !root.is_absolute() {
        return Err("Absolute module root required".into());
    }
    let area = std::env::var("FORGE_PLANT_MODULE_AREA")?.parse()?;
    let mut module = forge_delivery::module_runtime::load(&root, area, None)?;
    let original_head = module.zone.head().content_hash()?;
    let resolved = module
        .vegetation_imported()?
        .ok_or("Imported prototypes absent")?;
    // The existing private module has a flat procedural terrain. Adapt only
    // its in-memory fixture to an equivalent flat raster for the source packer.
    let heights = vec![0_u8; 512 * 512 * 4];
    let mut base = module.zone.head().base.clone();
    let parameter_repairs = repair_fixture(
        base.vegetation.as_mut().ok_or("Proof vegetation absent")?,
        &mut module.source_blobs,
    )?;
    add_bound_profiles(
        base.vegetation.as_mut().ok_or("Proof vegetation absent")?,
        &mut module.source_blobs,
    )?;
    if base.terrain.sampled_source.is_some()
        || base.terrain.height_sampling.amplitude_m != 0.0
        || base.terrain.height_sampling.base_elevation_m != 0.0
    {
        return Err("Proof requires the existing flat procedural module".into());
    }
    base.terrain.schema_version = 4;
    base.terrain.height_sampling.algorithm = HeightSamplingAlgorithm::SampledRasterV1;
    base.terrain.height_sampling.sample_spacing_m = 1.0;
    base.terrain.height_sampling.base_frequency_cycles_per_m = 1.0;
    base.terrain.height_sampling.octaves = 1;
    base.terrain.height_sampling.lacunarity = 2.0;
    base.terrain.height_sampling.persistence = 0.5;
    base.terrain.sampled_source = Some(SampledTerrainSource {
        schema: SAMPLED_TERRAIN_SCHEMA.into(),
        schema_version: 1,
        width: 512,
        height: 512,
        origin_xz_m: [-256.0; 2],
        sample_spacing_m: 1.0,
        outside_height_m: 0.0,
        encoding: HeightEncoding::Float32Le,
        samples_sha256: bytes_sha256(&heights),
    });
    module.zone = forge_project::zone_authoring::session::ZoneEditSession::from_initial_terrain(
        base.terrain.clone(),
    )?;
    let command = module.zone.head().prepare_regeneration(
        base,
        CommandId::deterministic("world-plant-material/flat-raster-fixture"),
    )?;
    module.zone.execute(&command)?;
    let vegetation = module
        .zone
        .head()
        .base
        .vegetation
        .as_ref()
        .ok_or("Authored base plants absent")?;
    let population = forge_world::imported_vegetation::ImportedPopulation::resolve(
        vegetation,
        |hash, maximum| {
            let bytes = module
                .source_blobs
                .get(&hash)
                .ok_or("Authored plant block absent")?;
            if bytes.len() > maximum {
                return Err("Authored block exceeds bound".into());
            }
            Ok(bytes.clone())
        },
    )?;
    let position = population.layers()[0]
        .instances()
        .iter()
        .min_by(|a, b| a.source.parameters[5].total_cmp(&b.source.parameters[5]))
        .ok_or("Empty authored proof population")?
        .source
        .position_m;
    let center = bevy::math::DVec3::from_array(position).as_vec3();
    let terrain = module.zone.head().effective()?.0.content_hash()?;
    let prepared = Prepared::new(
        &resolved.package,
        resolved.geometry,
        &population,
        module.zone.head(),
        &heights,
        terrain,
    )?;
    let view = population.terrain_view(module.zone.head(), &heights)?;
    let bounds = prepared.world_envelopes(&population, &view)?;
    let envelopes: BTreeMap<_, _> = bounds
        .prototypes()
        .iter()
        .map(|(key, value)| {
            (
                key.clone(),
                serde_json::json!({"radius_m":value.radius_m,"deformation_m":value.deformation_m}),
            )
        })
        .collect();
    if envelopes.len() != 10 {
        return Err("All ten source profiles must have verified envelopes".into());
    }
    let identity = serde_json::json!({"original_module_head":original_head,"fixture_head":module.zone.head().content_hash()?,
        "authored_instances":population.instance_count(),"in_memory_flat_raster":true,"installed_module_modified":false,
        "fixture_canopy_stretch_repairs":parameter_repairs,"synthetic_additional_profiles":9,
        "world_envelope_identity":bounds.identity(),"source_wind_speed_max":forge_engine::source_lighting::frame::MAX_WIND_SPEED,
        "prototype_envelopes":envelopes});
    Ok((resolved.package, prepared, center, identity))
}

fn stage_palm(
    world: &mut World,
    package: &PlantPackage,
    base: &WorldPlantMaterial,
    position: Vec3,
) -> Result<()> {
    use bevy::{
        camera::visibility::NoFrustumCulling, mesh::MeshTag, render::storage::ShaderBuffer,
    };
    let description: serde_json::Value = serde_json::from_slice(
        package
            .file("vegetation-scene.json")
            .ok_or("Scene descriptor absent")?,
    )?;
    let palm = description["groups"]
        .as_array()
        .ok_or("Groups absent")?
        .iter()
        .find(|g| g["name"] == "palms")
        .ok_or("Palm group absent")?;
    let level: forge_engine::source_vegetation::scene::LevelDescription =
        serde_json::from_value(palm["levels"][0].clone())?;
    let mesh = scene::mesh(&level, package.files())?;
    let mesh = world.resource_mut::<Assets<Mesh>>().add(mesh);
    let mut material = base.clone();
    material.kind = 0;
    material.params.gust_lod.z = level.range[0];
    material.params.gust_lod.w = level.range[1];
    material.params.lod_end_frame.x = level.range[2];
    material.instances = world
        .resource_mut::<Assets<ShaderBuffer>>()
        .add(ShaderBuffer::from(vec![Record {
            i_pos: position.extend(1.0),
            i_dat: Vec4::new(0.0, 0.05, 8.0, 0.25),
        }]));
    material.models = world
        .resource_mut::<Assets<ShaderBuffer>>()
        .add(ShaderBuffer::from(vec![Mat4::from_translation(position)]));
    let material = world
        .resource_mut::<Assets<WorldPlantMaterial>>()
        .add(material);
    world.spawn((
        Mesh3d(mesh),
        MeshMaterial3d(material),
        MeshTag(0),
        Transform::IDENTITY,
        NoFrustumCulling,
    ));
    Ok(())
}

#[expect(
    clippy::too_many_lines,
    reason = "bounded native material qualification, no product flow"
)]
fn run(output: &Path) -> Result<serde_json::Value> {
    let (package, prepared, canopy, fixture_identity) = fixture()?;
    let pin = package.manifest_sha256();
    let source = std::str::from_utf8(package.file("plant.wgsl").ok_or("Plant WGSL absent")?)?;
    let source_pin = bytes_sha256(source.as_bytes());
    let palm = canopy + Vec3::X * 30.0;
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
    .add_plugins((
        MaterialPlugin::<WorldPlantMaterial>::default(),
        RenderInventoryPlugin,
    ))
    .insert_resource(DirectionalLightShadowMap { size: 1024 })
    .insert_resource(GlobalAmbientLight {
        brightness: 80.0,
        ..default()
    });
    let deadline = Instant::now() + Duration::from_secs(30);
    while app.plugins_state() != PluginsState::Ready {
        if Instant::now() > deadline {
            return Err("GPU initialization timed out".into());
        }
        std::thread::sleep(Duration::from_millis(16));
    }
    app.finish();
    app.cleanup();
    let adapter_info = app
        .sub_app(RenderApp)
        .world()
        .resource::<RenderAdapterInfo>();
    let adapter = serde_json::json!({"name":adapter_info.name,
        "backend":format!("{:?}",adapter_info.backend),"vendor":adapter_info.vendor,"device":adapter_info.device});
    let world = app.world_mut();
    forge_engine::source_vegetation::world::stage_shader(world, source)?;
    let textures =
        PreparedTextures::from_package(&package)?.stage(&mut world.resource_mut::<Assets<Image>>());
    let frame = Frame::new(
        palm + Vec3::new(12.0, 10.0, 16.0),
        Vec4::new(1.0, 0.0, 4.0, 2.0),
        Vec4::ZERO,
    )?;
    let frame_handle = frame.stage(world)?;
    let material = WorldPlantMaterial {
        params: Params {
            i_pos: Vec4::ZERO,
            i_dat: Vec4::ZERO,
            wind: Vec4::ZERO,
            gust_lod: Vec4::ZERO,
            sun_night: Vec4::ZERO,
            lod_end_frame: Vec4::ZERO,
        },
        detail: textures.detail,
        leaf: textures.leaf,
        impostor_a: textures.impostors[0].clone(),
        impostor_b: textures.impostors[1].clone(),
        instances: default(),
        models: default(),
        frame: frame_handle.clone(),
        grass_heights: None,
        grass_mask: None,
        grass_deltas: None,
        grass_field: Vec4::ZERO,
        scene_lighting: frame_handle.clone(),
        live_shadows: frame_handle.clone(),
        local_lights: forge_engine::source_lighting::local_lights::gpu::BUFFER,
        environment: None,
        hill_shadow: None,
        object_shadow: Some(forge_engine::source_vegetation::world::PBR_DEPTH_BINDING),
        object_settings: forge_engine::source_shadows::ObjectShadowSettings::default(),
        clouds: [Vec4::ZERO; 5],
        cloud_shadow: None,
        source_lighting: false,
        hill_domain: bevy::prelude::Vec4::new(-1024.0, -1024.0, 2048.0, 0.0),
        kind: 0,
    };
    let owner = EntityId::deterministic("world-plant-material-proof");
    let resident = Resident::stage(world, prepared, &material, owner)?;
    let warm_entities = resident.entity_count();
    if resident.ready(world) {
        return Err("Warmup must not qualify gameplay publication".into());
    }
    render_inventory::release(world, owner)?;
    stage_palm(world, &package, &material, palm)?;
    // The warm entities are diagnostic draws, never a publication by Resident.
    // Retain each acquired level's actual LOD range, as Resident::stage does.
    let mut image = Image::new_target_texture(960, 600, TextureFormat::Rgba8UnormSrgb, None);
    image.texture_descriptor.usage |= TextureUsages::COPY_SRC;
    let target = world.resource_mut::<Assets<Image>>().add(image);
    let camera = world
        .spawn((
            Camera3d::default(),
            Camera {
                clear_color: ClearColorConfig::Custom(Color::srgb(0.18, 0.24, 0.32)),
                ..default()
            },
            RenderTarget::Image(target.clone().into()),
            Msaa::Off,
            DebandDither::Disabled,
            DepthPrepass,
            NormalPrepass,
            MotionVectorPrepass,
            Transform::default(),
        ))
        .id();
    let sun = world
        .spawn((
            DirectionalLight {
                illuminance: 15000.0,
                shadow_maps_enabled: true,
                ..default()
            },
            CascadeShadowConfigBuilder {
                num_cascades: 2,
                maximum_distance: 180.0,
                ..default()
            }
            .build(),
            Transform::default().looking_to(Vec3::new(-0.8, -1.0, -0.3), Vec3::Y),
        ))
        .id();
    let mesh = world
        .resource_mut::<Assets<Mesh>>()
        .add(Plane3d::default().mesh().size(400.0, 400.0));
    let floor = world
        .resource_mut::<Assets<StandardMaterial>>()
        .add(StandardMaterial {
            base_color: Color::srgb(0.55, 0.50, 0.39),
            perceptual_roughness: 1.0,
            ..default()
        });
    let ground = world
        .spawn((Mesh3d(mesh), MeshMaterial3d(floor), Transform::default()))
        .id();
    world.insert_resource(HostFrame {
        state: frame,
        buffer: frame_handle,
        camera,
    });
    app.add_systems(PostUpdate, publish_frame);
    let captures: Captures = Arc::default();
    let mut rows = Vec::new();
    for (index, (name, center, offset, shadows, hidden, kind)) in [
        ("palms", palm, Vec3::new(12.0, 10.0, 16.0), true, false, 0),
        (
            "palms-hidden",
            palm,
            Vec3::new(12.0, 10.0, 16.0),
            true,
            true,
            0,
        ),
        (
            "canopy-shadow",
            canopy,
            Vec3::new(14.0, 10.0, 20.0),
            true,
            false,
            1,
        ),
        (
            "canopy-no-shadow",
            canopy,
            Vec3::new(14.0, 10.0, 20.0),
            false,
            false,
            1,
        ),
        (
            "canopy-far",
            canopy,
            Vec3::new(75.0, 20.0, 90.0),
            true,
            false,
            2,
        ),
        (
            "canopy-hidden",
            canopy,
            Vec3::new(14.0, 10.0, 20.0),
            true,
            true,
            1,
        ),
        (
            "canopy-far-hidden",
            canopy,
            Vec3::new(75.0, 20.0, 90.0),
            true,
            true,
            2,
        ),
    ]
    .into_iter()
    .enumerate()
    {
        let position = center + offset;
        let world = app.world_mut();
        *world.get_mut::<Transform>(camera).ok_or("Camera absent")? =
            Transform::from_translation(position).looking_at(center + Vec3::Y * 6.0, Vec3::Y);
        *world.get_mut::<Transform>(ground).ok_or("Ground absent")? =
            Transform::from_translation(center - Vec3::Y * 0.1);
        world
            .get_mut::<DirectionalLight>(sun)
            .ok_or("Sun absent")?
            .shadow_maps_enabled = shadows;
        let entities: Vec<_> = world
            .query_filtered::<Entity, With<MeshMaterial3d<WorldPlantMaterial>>>()
            .iter(world)
            .collect();
        for entity in &entities {
            let handle = &world
                .get::<MeshMaterial3d<WorldPlantMaterial>>(*entity)
                .ok_or("Plant material binding absent")?
                .0;
            let visible = !hidden
                && world
                    .resource::<Assets<WorldPlantMaterial>>()
                    .get(handle)
                    .ok_or("Plant material absent")?
                    .kind
                    == kind;
            world.entity_mut(*entity).insert(if visible {
                Visibility::Visible
            } else {
                Visibility::Hidden
            });
        }
        let meshes: Vec<_> = entities
            .iter()
            .filter_map(|e| world.get::<Mesh3d>(*e).map(|m| (m.0.id(), m.0.clone())))
            .collect::<BTreeMap<_, _>>()
            .into_values()
            .collect();
        let materials: Vec<_> = entities
            .iter()
            .filter_map(|e| {
                world
                    .get::<MeshMaterial3d<WorldPlantMaterial>>(*e)
                    .map(|m| (m.0.id(), m.0.clone()))
            })
            .collect::<BTreeMap<_, _>>()
            .into_values()
            .collect();
        render_inventory::request(
            world,
            owner,
            source_pin,
            index as u64 + 1,
            &meshes,
            &materials,
        )?;
        let deadline = Instant::now() + Duration::from_secs(120);
        let mut stable = 0;
        while stable < 20 {
            let ready = advance(&mut app)?
                && render_inventory::acquire(app.world(), owner, source_pin, index as u64 + 1)
                    .is_some();
            stable = if ready { stable + 1 } else { 0 };
            if Instant::now() > deadline {
                return Err(format!("World plant warmup timed out: {name}").into());
            }
        }
        let requested_frame = app.world().resource::<bevy::diagnostic::FrameCount>().0;
        capture(&mut app, &target, output, name, &captures)?;
        // Screenshot setup may itself add a pipeline. Revalidate stability and
        // a real completed submission covering this capture, on this generation.
        let deadline = Instant::now() + Duration::from_secs(30);
        loop {
            if advance(&mut app)?
                && render_inventory::acquire(app.world(), owner, source_pin, index as u64 + 1)
                    .is_some_and(|token| token.covers_frame(app.world(), requested_frame))
            {
                break;
            }
            if Instant::now() > deadline {
                return Err("Post-capture plant fence timed out".into());
            }
        }
        rows.push(
            serde_json::json!({ "name":name, "position":position.to_array(), "shadows":shadows,
            "hidden":hidden, "kind":kind, "entities":entities.len(), "stable_gpu_observations":stable,
            "capture_requested_frame":requested_frame,"post_capture_fence_ready":true,
            "gpu":app.world().get_resource::<render_inventory::RenderInventoryEvidence>() }),
        );
    }
    let pixels = captures.lock().map_err(|e| e.to_string())?;
    let difference = |a: &str, b: &str| -> Result<usize> {
        let a = pixels.get(a).ok_or("Capture absent")?;
        let b = pixels.get(b).ok_or("Capture absent")?;
        Ok(a.as_chunks::<4>()
            .0
            .iter()
            .zip(b.as_chunks::<4>().0.iter())
            .filter(|(a, b)| a.iter().zip(*b).take(3).any(|(&a, &b)| a.abs_diff(b) > 8))
            .count())
    };
    let plant_pixels = difference("canopy-shadow", "canopy-hidden")?;
    let palm_pixels = difference("palms", "palms-hidden")?;
    let impostor_pixels = difference("canopy-far", "canopy-far-hidden")?;
    let shadow_pixels = difference("canopy-shadow", "canopy-no-shadow")?;
    let pipelines = plant_pipelines(&app)?;
    let pipelines_complete = (0..=2).all(|kind| {
        let ready = |row: &&serde_json::Value| {
            row["kind"] == kind && row["ready"] == true && row["fragment_matches"] == true
        };
        pipelines
            .iter()
            .filter(ready)
            .any(|row| row["prepass"] == false)
            && pipelines
                .iter()
                .filter(ready)
                .any(|row| row["normal"] == true && row["motion"] == true)
            && pipelines
                .iter()
                .filter(ready)
                .any(|row| row["shadow_depth"] == true)
    });
    Ok(
        serde_json::json!({ "schema":"forge.world-plant-material-gpu-proof.v1",
        "passed":plant_pixels > 256 && palm_pixels > 256 && impostor_pixels > 64 && shadow_pixels > 16 && pipelines_complete, "adapter":adapter,
        "package_manifest_sha256":pin, "source_shader_sha256":source_pin,"fixture":fixture_identity,
        "warm_authored_entities":warm_entities,"synthetic_palm_instances":1,"gameplay_publication":false,
        "captures":rows, "plant_difference_pixels":plant_pixels, "shadow_difference_pixels":shadow_pixels,
        "palm_difference_pixels":palm_pixels,"impostor_difference_pixels":impostor_pixels,"cell_selected_material_probe":true,
        "source_pipeline_records":pipelines,"all_three_kinds_forward_prepass_shadow_compiled":pipelines_complete,
        "prepass_pixels_qualified":false,
        "reference_pixel_parity":false, "module_raster_qualified":false, "performance_qualified":false,
        "lighting_adaptation":"source surfaces and masks; Bevy world PBR and diffuse transmission" }),
    )
}

fn main() -> Result<()> {
    let output = PathBuf::from(
        std::env::args()
            .nth(1)
            .ok_or("Fresh absolute proof output directory required")?,
    );
    if !output.is_absolute() {
        return Err("Absolute proof output required".into());
    }
    fs::create_dir(&output)?;
    let result = match run(&output) {
        Ok(result) => result,
        Err(error) => {
            let report =
                serde_json::json!({"passed":false,"error":error.to_string(),"gate_closed":false});
            fs::write(
                output.join("proof.json"),
                serde_json::to_vec_pretty(&report)?,
            )?;
            return Err(error);
        }
    };
    fs::write(
        output.join("proof.json"),
        serde_json::to_vec_pretty(&result)?,
    )?;
    if result["passed"] != true {
        return Err("World plant pixel checks failed; inspect proof.json".into());
    }
    Ok(())
}
