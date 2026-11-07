//! Paired GPU control: existing photometric lights and shadows on common surfaces.
//! Controlled zero solar/environment inputs isolate the native light contribution.
use bevy::{
    app::PluginsState,
    asset::RenderAssetUsages,
    camera::{RenderTarget, visibility::RenderLayers},
    core_pipeline::tonemapping::{DebandDither, Tonemapping},
    image::{ImageCompareFunction, ImageSampler, ImageSamplerDescriptor},
    prelude::*,
    render::{
        RenderApp, RenderPlugin,
        pipelined_rendering::PipelinedRenderingPlugin,
        render_resource::{
            CachedPipelineState, Extent3d, PipelineCache, TextureDimension, TextureFormat,
            TextureUsages, TextureViewDescriptor, TextureViewDimension,
        },
        settings::{Backends, RenderCreation, WgpuSettings},
        storage::ShaderBuffer,
        view::screenshot::{Screenshot, ScreenshotCaptured, save_to_disk},
    },
    window::ExitCondition,
    winit::WinitPlugin,
};
use forge_engine::{
    VisualValleyTerrainExtension, VisualValleyTerrainMaterial,
    source_lighting::frame::Frame,
    source_lighting::{
        SceneLighting,
        surfaces::{self, ModelMaterial, Receiver, TerrainMaterial},
    },
    source_shadows,
};
use forge_world::building_layout::{PartRole, RoomDimensions, RoomLayout};
use std::{
    collections::{BTreeMap, BTreeSet},
    path::{Path, PathBuf},
    sync::{Arc, Mutex},
    time::{Duration, Instant},
};

type Error = Box<dyn std::error::Error>;
type Captures = Arc<Mutex<BTreeMap<String, Vec<u8>>>>;
#[derive(Component)]
struct CaseEntity;
const WIDTH: u32 = 640;
const HEIGHT: u32 = 480;
#[derive(Clone, Copy, serde::Serialize)]
enum Emitter {
    Point,
    Spot,
    Off,
    Source {
        night: f64,
        enabled: bool,
        valid: bool,
    },
}
#[derive(Clone, Copy, serde::Serialize)]
struct Case {
    name: &'static str,
    source: bool,
    shadows: bool,
    emitter: Emitter,
}
const CASES: [Case; 6] = [
    Case {
        name: "standard-point",
        source: false,
        shadows: true,
        emitter: Emitter::Point,
    },
    Case {
        name: "source-point",
        source: true,
        shadows: true,
        emitter: Emitter::Point,
    },
    Case {
        name: "source-dark",
        source: true,
        shadows: false,
        emitter: Emitter::Off,
    },
    Case {
        name: "source-point-no-shadows",
        source: true,
        shadows: false,
        emitter: Emitter::Point,
    },
    Case {
        name: "standard-spot",
        source: false,
        shadows: true,
        emitter: Emitter::Spot,
    },
    Case {
        name: "source-spot",
        source: true,
        shadows: true,
        emitter: Emitter::Spot,
    },
];

const SOURCE_CASES: [Case; 6] = [
    Case {
        name: "source-dark",
        source: true,
        shadows: false,
        emitter: Emitter::Off,
    },
    Case {
        name: "source-units-night",
        source: true,
        shadows: false,
        emitter: Emitter::Source {
            night: 1.0,
            enabled: true,
            valid: true,
        },
    },
    Case {
        name: "source-units-day",
        source: true,
        shadows: false,
        emitter: Emitter::Source {
            night: 0.0,
            enabled: true,
            valid: true,
        },
    },
    Case {
        name: "source-units-disabled",
        source: true,
        shadows: false,
        emitter: Emitter::Source {
            night: 1.0,
            enabled: false,
            valid: true,
        },
    },
    Case {
        name: "source-units-invalid",
        source: true,
        shadows: false,
        emitter: Emitter::Source {
            night: 1.0,
            enabled: true,
            valid: false,
        },
    },
    Case {
        name: "source-units-recovered",
        source: true,
        shadows: false,
        emitter: Emitter::Source {
            night: 1.0,
            enabled: true,
            valid: true,
        },
    },
];

#[derive(Resource, Clone, Copy)]
struct SourceInput {
    camera: Entity,
    night: f64,
    enabled: bool,
    valid: bool,
}

fn collect_source(world: &mut World) {
    use forge_engine::source_lighting::local_lights::{
        Flashlight, Settings, collection::Collection,
    };
    let input = world.get_resource::<SourceInput>().copied();
    world.resource_scope(|world, mut collection: Mut<Collection>| {
        let Some(input) = input else {
            collection.invalidate();
            return;
        };
        // This fixture's canonical inventory declares exactly registration 1.
        // The deliberately wrong declaration must clear the previously lit GPU buffer.
        let inventory = if input.valid { [1] } else { [2] };
        let result = collection.collect(
            world,
            input.camera,
            Settings {
                delta_seconds: 1.0 / 60.0,
                night: input.night,
                enabled: input.enabled,
                strength: 1.0,
                flashlight: Flashlight::default(),
            },
            &inventory,
        );
        assert_eq!(result.is_ok(), input.valid);
    });
}

fn image(world: &mut World, layers: u32, dimension: TextureViewDimension) -> Handle<Image> {
    let mut image = Image::new_fill(
        Extent3d {
            width: 2,
            height: 2,
            depth_or_array_layers: layers,
        },
        TextureDimension::D2,
        &[0; 8],
        TextureFormat::Rgba16Float,
        RenderAssetUsages::all(),
    );
    image.texture_view_descriptor = Some(TextureViewDescriptor {
        dimension: Some(dimension),
        ..default()
    });
    world.resource_mut::<Assets<Image>>().add(image)
}

fn receiver(world: &mut World) -> Result<Receiver, Error> {
    let scene = SceneLighting::new([0.0, 1.0, 0.0], [0.0; 3], [[0.0; 3]; 9])?;
    let scene = world
        .resource_mut::<Assets<ShaderBuffer>>()
        .add(ShaderBuffer::from(scene));
    let frame = Frame::new(Vec3::new(0.0, 1.7, -2.6), Vec4::X, Vec4::ZERO)?.stage(world)?;
    source_shadows::receiver::install(world)?;
    let mut depth = Image::new_uninit(
        Extent3d {
            width: 1,
            height: 1,
            depth_or_array_layers: 3,
        },
        TextureDimension::D2,
        TextureFormat::Depth32Float,
        RenderAssetUsages::RENDER_WORLD,
    );
    depth.texture_descriptor.usage =
        TextureUsages::TEXTURE_BINDING | TextureUsages::RENDER_ATTACHMENT;
    depth.texture_view_descriptor = Some(TextureViewDescriptor {
        dimension: Some(TextureViewDimension::D2Array),
        ..default()
    });
    depth.sampler = ImageSampler::Descriptor(ImageSamplerDescriptor {
        compare: Some(ImageCompareFunction::LessEqual),
        ..default()
    });
    let objects = world.resource_mut::<Assets<Image>>().add(depth);
    Ok(Receiver {
        scene,
        shadows: source_shadows::receiver::DEFAULT_BUFFER,
        frame,
        local_lights: forge_engine::source_lighting::local_lights::gpu::BUFFER,
        environment: image(world, 6, TextureViewDimension::Cube),
        hill: image(world, 1, TextureViewDimension::D2),
        objects,
        object_settings: default(),
        clouds: default(),
        cloud_shadow: image(world, 1, TextureViewDimension::D2),
        domain: Vec4::new(-8.0, -8.0, 16.0, 0.0),
        terrain: false,
    })
}

struct Materials {
    standard: Handle<StandardMaterial>,
    model: Handle<ModelMaterial>,
    authored: Handle<VisualValleyTerrainMaterial>,
    lit_terrain: Handle<TerrainMaterial>,
}
fn materials(world: &mut World, bindings: &Receiver) -> Materials {
    let surface = StandardMaterial {
        base_color: Color::srgb(0.65, 0.55, 0.4),
        perceptual_roughness: 0.65,
        ..default()
    };
    let standard = world
        .resource_mut::<Assets<StandardMaterial>>()
        .add(surface.clone());
    let model = world
        .resource_mut::<Assets<ModelMaterial>>()
        .add(ModelMaterial {
            base: surface,
            extension: bindings.clone(),
        });
    let detail = world.resource_mut::<Assets<Image>>().add(Image::new_fill(
        Extent3d::default(),
        TextureDimension::D2,
        &[128, 128, 128, 255],
        TextureFormat::Rgba8Unorm,
        RenderAssetUsages::all(),
    ));
    let terrain = VisualValleyTerrainMaterial {
        base: StandardMaterial::default(),
        extension: VisualValleyTerrainExtension {
            detail_texture: detail,
            ..default()
        },
    };
    let authored = world
        .resource_mut::<Assets<VisualValleyTerrainMaterial>>()
        .add(terrain.clone());
    let mut terrain_bindings = bindings.clone();
    terrain_bindings.terrain = true;
    let lit_terrain = world
        .resource_mut::<Assets<TerrainMaterial>>()
        .add(TerrainMaterial {
            base: terrain,
            extension: terrain_bindings,
        });
    Materials {
        standard,
        model,
        authored,
        lit_terrain,
    }
}
fn spawn_room(
    world: &mut World,
    layer: usize,
    source: bool,
    materials: Materials,
) -> Result<(), Error> {
    let Materials {
        standard,
        model,
        authored,
        lit_terrain,
    } = materials;
    let cube = world
        .resource_mut::<Assets<Mesh>>()
        .add(Cuboid::new(1.0, 1.0, 1.0));
    let mut floor_mesh = Mesh::from(Cuboid::new(1.0, 1.0, 1.0));
    floor_mesh.insert_attribute(
        Mesh::ATTRIBUTE_COLOR,
        vec![[1.0, 0.0, 0.0, 0.0]; floor_mesh.count_vertices()],
    );
    let floor = world.resource_mut::<Assets<Mesh>>().add(floor_mesh);
    let room = RoomLayout::generate(
        RoomDimensions {
            width_m: 8.0,
            depth_m: 6.0,
            clear_height_m: 3.0,
            wall_m: 0.25,
            slab_m: 0.2,
            door_width_m: 1.4,
            door_height_m: 2.2,
            door_offset_x_m: 0.0,
        },
        forge_core::EntityId::deterministic("local-light-control"),
    )?;
    for part in room.parts {
        let min = Vec3::from_array(part.bounds.min);
        let max = Vec3::from_array(part.bounds.max);
        let is_floor = part.role == PartRole::Floor;
        let mut entity = world.spawn((
            Mesh3d(if is_floor {
                floor.clone()
            } else {
                cube.clone()
            }),
            Transform::from_translation(min.midpoint(max)).with_scale(max - min),
            RenderLayers::layer(layer),
            CaseEntity,
        ));
        if is_floor {
            if source {
                entity.insert(MeshMaterial3d(lit_terrain.clone()));
            } else {
                entity.insert(MeshMaterial3d(authored.clone()));
            }
        } else if source {
            entity.insert(MeshMaterial3d(model.clone()));
        } else {
            entity.insert(MeshMaterial3d(standard.clone()));
        }
    }
    let mut blocker = world.spawn((
        Mesh3d(cube),
        Transform::from_xyz(0.6, 0.55, 0.7).with_scale(Vec3::new(0.6, 1.1, 0.6)),
        RenderLayers::layer(layer),
        CaseEntity,
    ));
    if source {
        blocker.insert(MeshMaterial3d(model));
    } else {
        blocker.insert(MeshMaterial3d(standard));
    }
    Ok(())
}
fn scene(
    app: &mut App,
    layer: usize,
    case: &Case,
    bindings: &Receiver,
) -> Result<Handle<Image>, Error> {
    let world = app.world_mut();
    let mut target = Image::new_target_texture(WIDTH, HEIGHT, TextureFormat::Rgba8UnormSrgb, None);
    target.texture_descriptor.usage |= TextureUsages::COPY_SRC;
    let target = world.resource_mut::<Assets<Image>>().add(target);
    let mut camera = world.spawn((
        Camera3d::default(),
        Camera {
            clear_color: ClearColorConfig::Custom(Color::BLACK),
            ..default()
        },
        RenderTarget::Image(target.clone().into()),
        RenderLayers::layer(layer),
        CaseEntity,
        Msaa::Off,
        Tonemapping::None,
        DebandDither::Disabled,
        Transform::from_xyz(0.0, 1.7, -2.6).looking_at(Vec3::new(0.0, 0.7, 0.8), Vec3::Y),
    ));
    camera.insert((
        bevy::camera::ShadowLodOrigin,
        bevy::light::ShadowFilteringMethod::Hardware2x2,
    ));
    let camera = camera.id();
    let materials = materials(world, bindings);
    spawn_room(world, layer, case.source, materials)?;
    world.remove_resource::<SourceInput>();
    if let Emitter::Source {
        night,
        enabled,
        valid,
    } = case.emitter
    {
        use forge_engine::source_lighting::local_lights::{Light, scene::SourceLight};
        world.insert_resource(SourceInput {
            camera,
            night,
            enabled,
            valid,
        });
        world.spawn((
            SourceLight {
                registration_order: 1,
                parameters: Light {
                    position: bevy::math::DVec3::ZERO,
                    color: bevy::math::DVec3::new(1.0, 0.7, 0.4),
                    intensity: 12.0,
                    range: 8.0,
                    direction: None,
                    cos_inner: None,
                    cos_outer: None,
                    enabled: true,
                    scale: 1.0,
                    flicker: 0.0,
                    phase: 1.0,
                },
            },
            Transform::from_xyz(-0.5, 2.25, -0.2),
            CaseEntity,
        ));
    } else if !matches!(case.emitter, Emitter::Off) {
        let transform =
            Transform::from_xyz(-0.5, 2.25, -0.2).looking_at(Vec3::new(0.5, 0.0, 1.0), Vec3::Y);
        let mut light = world.spawn((transform, RenderLayers::layer(layer), CaseEntity));
        if matches!(case.emitter, Emitter::Spot) {
            light.insert(SpotLight {
                intensity: 25_000.0,
                range: 8.0,
                radius: 0.25,
                shadow_maps_enabled: case.shadows,
                inner_angle: 0.45,
                outer_angle: 0.9,
                ..default()
            });
        } else {
            light.insert(PointLight {
                intensity: 25_000.0,
                range: 8.0,
                radius: 0.25,
                shadow_maps_enabled: case.shadows,
                ..default()
            });
        }
    }
    Ok(target)
}

fn pipelines(app: &App) -> Result<bool, Error> {
    let cache = app
        .get_sub_app(RenderApp)
        .ok_or("Render app absent")?
        .world()
        .resource::<PipelineCache>();
    let mut total = 0;
    let mut pending = false;
    for pipeline in cache.pipelines() {
        total += 1;
        match &pipeline.state {
            CachedPipelineState::Ok(_) => {}
            CachedPipelineState::Err(
                bevy::shader::ShaderCacheError::ShaderNotLoaded(_)
                | bevy::shader::ShaderCacheError::ShaderImportNotYetAvailable,
            ) => pending = true,
            CachedPipelineState::Err(error) => {
                return Err(format!("GPU pipeline refused: {error:?}").into());
            }
            _ => pending = true,
        }
    }
    Ok(total > 0 && !pending)
}

fn missing_source_imports(app: &App) -> Vec<String> {
    let shaders = app.world().resource::<Assets<Shader>>();
    let known: BTreeSet<_> = shaders
        .iter()
        .map(|(_, shader)| format!("{:?}", shader.import_path))
        .collect();
    shaders
        .iter()
        .filter(|(_, shader)| shader.path.starts_with("source-"))
        .flat_map(|(_, shader)| {
            shader.imports.iter().filter_map(|import| {
                let key = format!("{import:?}");
                (!known.contains(&key)).then(|| format!("{}: {key}", shader.path))
            })
        })
        .collect()
}

fn pipeline_diagnostic(app: &App) -> Result<String, Error> {
    let cache = app
        .get_sub_app(RenderApp)
        .ok_or("Render app absent")?
        .world()
        .resource::<PipelineCache>();
    let pending: Vec<_> = cache
        .pipelines()
        .filter(|p| !matches!(p.state, CachedPipelineState::Ok(_)))
        .take(8)
        .map(|p| {
            format!("state={:?}, descriptor={:?}", p.state, p.descriptor)
                .chars()
                .take(2048)
                .collect::<String>()
        })
        .collect();
    Ok(format!(
        "Pipeline preparation timeout: total={}, waiting={}, missing source imports={:?}, pending={pending:?}",
        cache.pipelines().count(),
        cache.waiting_pipelines().count(),
        missing_source_imports(app)
    ))
}

fn advance(app: &mut App) -> Result<(), Error> {
    app.update();
    if let Some(exit) = app.should_exit() {
        return Err(format!("Unexpected application exit: {exit:?}").into());
    }
    std::thread::sleep(Duration::from_millis(8));
    Ok(())
}

fn settle(app: &mut App, deadline: Instant) -> Result<(), Error> {
    let mut stable = 0;
    while stable < 30 {
        advance(app)?;
        if pipelines(app)? {
            stable += 1;
        } else {
            stable = 0;
        }
        if Instant::now() > deadline {
            return Err(pipeline_diagnostic(app)?.into());
        }
    }
    Ok(())
}

fn capture(
    app: &mut App,
    target: Handle<Image>,
    output: &Path,
    name: &'static str,
    captures: &Captures,
    deadline: Instant,
) -> Result<(), Error> {
    let before = captures.lock().map_err(|e| e.to_string())?.len();
    let completed = captures.clone();
    app.world_mut()
        .spawn(Screenshot::image(target))
        .observe(save_to_disk(output.join(format!("{name}.png"))))
        .observe(move |event: On<ScreenshotCaptured>| {
            if let Some(bytes) = &event.image.data
                && event.image.width() == WIDTH
                && event.image.height() == HEIGHT
                && let Ok(mut captures) = completed.lock()
            {
                captures.insert(name.to_owned(), bytes.clone());
            }
        });
    while captures.lock().map_err(|e| e.to_string())?.len() != before + 1 {
        advance(app)?;
        if Instant::now() > deadline {
            return Err("Screenshot completion timeout".into());
        }
    }
    Ok(())
}

fn source_observation(app: &App, case: &Case) -> Result<Option<serde_json::Value>, Error> {
    let Emitter::Source {
        night,
        enabled,
        valid,
    } = case.emitter
    else {
        return Ok(None);
    };
    let observed = app
        .world()
        .resource::<forge_engine::source_lighting::local_lights::gpu::Status>()
        .0
        .lock()
        .map_err(|e| e.to_string())?
        .clone();
    let lights = i32::from(night > 0.0 && enabled && valid);
    if observed.lights != lights || observed.error.is_none() != valid {
        return Err(format!("Source packet publication differs: {observed:?}").into());
    }
    Ok(Some(
        serde_json::json!({"case":case.name,"lights":observed.lights,
        "error":observed.error,"revision":observed.queued_collection_revision,
        "queued_writes":observed.queued_writes}),
    ))
}

fn run(output: &Path, source_units: bool) -> Result<serde_json::Value, Error> {
    std::fs::create_dir(output)?;
    let mut app = App::new();
    app.add_plugins(
        DefaultPlugins
            .set(WindowPlugin {
                primary_window: None,
                exit_condition: ExitCondition::DontExit,
                ..default()
            })
            .set(RenderPlugin {
                render_creation: RenderCreation::Automatic(Box::new(WgpuSettings {
                    backends: Some(Backends::VULKAN),
                    ..default()
                })),
                ..default()
            })
            .disable::<WinitPlugin>()
            .disable::<PipelinedRenderingPlugin>(),
    );
    app.insert_resource(GlobalAmbientLight::NONE);
    surfaces::host::register(&mut app)?;
    if source_units {
        forge_engine::source_lighting::local_lights::gpu::register(&mut app)?;
        app.add_systems(
            PostUpdate,
            collect_source.after(bevy::transform::TransformSystems::Propagate),
        );
    }
    let deadline = Instant::now() + Duration::from_secs(90);
    while app.plugins_state() != PluginsState::Ready {
        if Instant::now() > deadline {
            return Err("Plugin initialization timeout".into());
        }
        std::thread::sleep(Duration::from_millis(8));
    }
    app.finish();
    app.cleanup();
    advance(&mut app)?;
    surfaces::stage_shaders(app.world_mut())?;
    let missing = missing_source_imports(&app);
    if !missing.is_empty() {
        return Err(format!("Source imports absent: {missing:?}").into());
    }
    let bindings = receiver(app.world_mut())?;
    let captures: Captures = Arc::default();
    let cases = if source_units { &SOURCE_CASES } else { &CASES };
    let mut source_uploads = Vec::new();
    for (layer, case) in cases.iter().enumerate() {
        let target = scene(&mut app, layer, case, &bindings)?;
        settle(&mut app, deadline)?;
        let points = app
            .world_mut()
            .query::<&PointLight>()
            .iter(app.world())
            .count();
        let spots = app
            .world_mut()
            .query::<&SpotLight>()
            .iter(app.world())
            .count();
        let expected = match case.emitter {
            Emitter::Off | Emitter::Source { .. } => (0, 0),
            Emitter::Point => (1, 0),
            Emitter::Spot => (0, 1),
        };
        if (points, spots) != expected {
            return Err("Case light isolation failed".into());
        }
        if let Some(observed) = source_observation(&app, case)? {
            source_uploads.push(observed);
        }
        capture(&mut app, target, output, case.name, &captures, deadline)?;
        app.world_mut().remove_resource::<SourceInput>();
        let owned: Vec<_> = app
            .world_mut()
            .query_filtered::<Entity, With<CaseEntity>>()
            .iter(app.world())
            .collect();
        for entity in owned {
            app.world_mut().despawn(entity);
        }
        advance(&mut app)?;
    }
    let captures = captures.lock().map_err(|e| e.to_string())?;
    let mut report = compare(&captures, source_units)?;
    report["source_uploads"] = serde_json::json!(source_uploads);
    Ok(report)
}
fn compare(
    captures: &BTreeMap<String, Vec<u8>>,
    source_units: bool,
) -> Result<serde_json::Value, Error> {
    let expected = usize::try_from(WIDTH * HEIGHT * 4)?;
    if captures.values().any(|bytes| bytes.len() != expected) {
        return Err("Invalid RGBA readback length".into());
    }
    let dark_pixels = captures["source-dark"]
        .as_chunks::<4>()
        .0
        .iter()
        .filter(|pixel| pixel[..3] != [0, 0, 0])
        .count();
    let native_comparisons = [
        ("point-parity", "standard-point", "source-point", true),
        ("spot-parity", "standard-spot", "source-spot", true),
        ("light-contribution", "source-point", "source-dark", false),
        ("spot-contribution", "source-spot", "source-dark", false),
        (
            "shadow-contribution",
            "source-point",
            "source-point-no-shadows",
            false,
        ),
    ];
    let source_comparisons = [
        (
            "source-unit-contribution",
            "source-units-night",
            "source-dark",
            false,
        ),
        ("day-clears-source", "source-units-day", "source-dark", true),
        (
            "disabled-clears-source",
            "source-units-disabled",
            "source-dark",
            true,
        ),
        (
            "invalid-inventory-clears-source",
            "source-units-invalid",
            "source-dark",
            true,
        ),
        (
            "recovery-is-exact",
            "source-units-recovered",
            "source-units-night",
            true,
        ),
    ];
    let comparisons = if source_units {
        &source_comparisons
    } else {
        &native_comparisons
    };
    let mut results = Vec::new();
    for &(name, left, right, equal) in comparisons {
        let a = &captures[left];
        let b = &captures[right];
        if a.len() != b.len() {
            return Err("Readback lengths differ".into());
        }
        let different = a
            .as_chunks::<4>()
            .0
            .iter()
            .zip(b.as_chunks::<4>().0.iter())
            .filter(|(a, b)| a[..3] != b[..3])
            .count();
        let maximum = a
            .as_chunks::<4>()
            .0
            .iter()
            .zip(b.as_chunks::<4>().0.iter())
            .flat_map(|(a, b)| (0..3).map(move |i| a[i].abs_diff(b[i])))
            .max()
            .unwrap_or(0);
        let passed = if equal {
            different == 0
        } else {
            different > 100 && maximum > 8
        };
        results.push(serde_json::json!({"control":name,"different_rgb_pixels":different,"maximum_channel_difference":maximum,"passed":passed}));
    }
    let scope = if source_units {
        "Actual shared source-unit collector/queue and standard/authored-terrain mesh draws in a dry room; zero sun/SH/HDR and no native point/spot lights. Night/day/disabled/invalid-inventory/recovery controls. Not full module/profile qualification."
    } else {
        "Paired GPU readback with original room layout and explicit zero sun/SH/HDR; native point/spot photometric contributions and point shadows only. Not full module/profile qualification."
    };
    Ok(
        serde_json::json!({"schema":"forge.surface-local-lights-gpu.v1","width":WIDTH,"height":HEIGHT,
        "scope":scope,
        "cases":if source_units { &SOURCE_CASES } else { &CASES },"comparisons":results,"dark_nonzero_rgb_pixels":dark_pixels,
        "passed":dark_pixels==0 && results.iter().all(|r|r["passed"]==true),"gate_qualified":false}),
    )
}
fn main() -> Result<(), Error> {
    let output = PathBuf::from(
        std::env::args_os()
            .nth(1)
            .ok_or("Supply a fresh output directory")?,
    );
    if output.exists() {
        return Err("Use a fresh output directory".into());
    }
    let source_units = match std::env::args().nth(2).as_deref() {
        None => false,
        Some("--source-units") => true,
        Some(_) => return Err("Only --source-units is accepted after the output directory".into()),
    };
    let report=run(&output, source_units).unwrap_or_else(|error|serde_json::json!({"passed":false,"error":error.to_string(),"gate_qualified":false}));
    std::fs::write(
        output.join("report.json"),
        serde_json::to_vec_pretty(&report)?,
    )?;
    println!("{report}");
    if report["passed"] != true {
        return Err("Local-light GPU qualification failed".into());
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn native_shader_imports_resolve_modules_instead_of_the_crate_root() {
        let source = include_str!("../src/source_lighting/surfaces/native_lights.wgsl");
        let shader = Shader::from_wgsl(source, "native-import-control");
        println!("Complete native imports: {:?}", shader.imports);
        assert!(shader.imports.iter().any(|import| matches!(import,bevy::shader::ShaderImport::Custom(name) if name=="bevy_pbr::mesh_view_bindings")));
        assert!(!shader.imports.iter().any(
            |import| matches!(import,bevy::shader::ShaderImport::Custom(name) if name=="bevy_pbr")
        ));
    }
}
