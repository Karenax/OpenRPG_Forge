//! Resident crest and spray resources bound to an admitted shore generation.
//!
//! The shore scheduler owns the handoff and the ordered GPU passes. This
//! resource supplies shared output buffers and live feedback; it never starts
//! a second shore simulation.
use crate::{
    source_shore_sim,
    source_spray_feedback::{self, BudgetFeedback, LiveFeedback},
    source_water_host::{PreparedBreakerDisplay, PreparedBreakerInput},
};
use bevy::{
    camera::visibility::NoFrustumCulling,
    light::NotShadowCaster,
    prelude::*,
    render::{
        RenderApp,
        extract_resource::{ExtractResource, ExtractResourcePlugin},
        gpu_readback::Readback,
        storage::ShaderBuffer,
    },
    tasks::{AsyncComputeTaskPool, Task, futures::check_ready},
};
use forge_world::sampled_terrain::SampledTerrainPlan;
use std::sync::{
    Arc, Mutex,
    atomic::{AtomicBool, AtomicU32, AtomicU64, Ordering},
};

pub(crate) const FOLLOW_TRIGGER_M: f64 = 32.0;
pub(crate) const FOLLOW_ACCEPTANCE_M: f64 = 32.0;

/// GPU outputs and progress for a single admitted shore/surface generation.
#[derive(Resource, Clone, ExtractResource)]
pub struct Targets {
    pub(crate) input: PreparedBreakerInput,
    /// Crest, position, velocity, info and ring-head buffers, in that order.
    pub buffers: [Handle<ShaderBuffer>; 5],
    pub(crate) shadow_cutoff: Handle<ShaderBuffer>,
    lace: Handle<Image>,
    pub(crate) frame: Arc<Mutex<[UVec4; 45]>>,
    pub(crate) camera_ready: Arc<AtomicBool>,
    pub(crate) budget: Arc<Mutex<BudgetFeedback>>,
    pub ready: Arc<AtomicBool>,
    pub(crate) gpu_ready: Arc<AtomicBool>,
    pub completed_steps: Arc<AtomicU32>,
    pub(crate) error: Arc<Mutex<Option<String>>>,
    pub(crate) generation: u32,
    pub(crate) current_generation: Arc<AtomicU32>,
    station_count: Arc<AtomicU32>,
    switches: Arc<AtomicU32>,
    coverage_started: Arc<AtomicBool>,
    coverage_samples: Arc<AtomicU64>,
    coverage_misses: Arc<AtomicU64>,
    unknown_camera_samples: Arc<AtomicU64>,
    unavailable_samples: Arc<AtomicU64>,
}

/// Main-world samples, not a GPU or pixel-visibility fence.
#[derive(Clone, Copy, Debug, serde::Serialize)]
pub struct CoverageObservation {
    pub samples: u64,
    pub uncovered_samples: u64,
    pub unknown_camera_samples: u64,
    pub unavailable_samples: u64,
}

impl Targets {
    #[must_use]
    pub fn stations(&self) -> usize {
        self.station_count.load(Ordering::Acquire) as usize
    }

    /// Number of replacement displays published by the shared water host.
    #[must_use]
    pub fn switches(&self) -> u32 {
        self.switches.load(Ordering::Acquire)
    }

    #[must_use]
    pub fn coverage(&self) -> CoverageObservation {
        CoverageObservation {
            samples: self.coverage_samples.load(Ordering::Acquire),
            uncovered_samples: self.coverage_misses.load(Ordering::Acquire),
            unknown_camera_samples: self.unknown_camera_samples.load(Ordering::Acquire),
            unavailable_samples: self.unavailable_samples.load(Ordering::Acquire),
        }
    }

    pub(crate) fn synchronize_ready(&self) {
        if self.generation == self.current_generation.load(Ordering::Acquire)
            && self.gpu_ready.load(Ordering::Acquire)
        {
            self.ready.store(true, Ordering::Release);
        }
    }

    /// Latest validated GPU ring head and the simulation step that produced it.
    #[must_use]
    pub fn latest_head(&self) -> Option<(u32, u32)> {
        self.budget
            .lock()
            .expect("budget mutex")
            .latest()
            .map(|(step, sample)| (step, sample.head))
    }
    /// Update the same source camera frame used by the optical water host.
    pub fn update_camera(&self, clip: Mat4, pose: GlobalTransform) -> Result<(), &'static str> {
        if !clip.is_finite() || !pose.to_matrix().is_finite() {
            return Err("Breaker camera contains non-finite values");
        }
        crate::source_water_frame::CameraFrame::new(clip, pose)
            .write(&mut self.frame.lock().expect("breaker frame mutex"));
        self.camera_ready.store(true, Ordering::Release);
        Ok(())
    }

    #[must_use]
    pub fn error(&self) -> Option<String> {
        self.error.lock().expect("breaker error mutex").clone()
    }
}

/// Stage outputs for an existing matching shore. Simulation starts only after
/// the render scheduler has adopted its exact current state.
#[allow(
    clippy::too_many_lines,
    reason = "Register one admitted effect generation together"
)]
pub fn install(app: &mut App, prepared: PreparedBreakerDisplay) -> Result<Targets, String> {
    let PreparedBreakerDisplay { input, lace } = prepared;
    if app.get_sub_app(RenderApp).is_none()
        || !app.world().contains_resource::<Assets<ShaderBuffer>>()
        || !app.world().contains_resource::<Assets<Image>>()
        || !app.world().contains_resource::<Assets<Shader>>()
        || !app.world().contains_resource::<Assets<Mesh>>()
        || app.world().contains_resource::<Targets>()
        || app.world().contains_resource::<LiveFeedback>()
        || app
            .world()
            .contains_resource::<crate::source_spray_mips::SpriteTextures>()
    {
        return Err("Resident breakers require exclusive GPU resources".into());
    }
    let shore = app
        .world()
        .get_resource::<source_shore_sim::Targets>()
        .ok_or("Resident breakers require a staged shore")?;
    if (shore.document_hash, shore.surface_package_sha256) != input.identities() {
        return Err("Resident breaker and shore identities differ".into());
    }
    let gpu = input.gpu_inputs();
    gpu.validate()?;
    let sprite_shader = crate::source_spray_render::prepare_resident_generation_shader(
        crate::source_spray_render::COMMON_SPRITE_SHADER,
        crate::source_breaker_gpu::COMMON_CREST_SOURCE_STATIONS,
    )?;
    if !app.is_plugin_added::<MaterialPlugin<crate::source_breaker_render::LipMaterial>>() {
        app.add_plugins(MaterialPlugin::<crate::source_breaker_render::LipMaterial>::default());
    }
    if !app.is_plugin_added::<MaterialPlugin<crate::source_spray_render::SprayMaterial>>() {
        app.add_plugins(MaterialPlugin::<crate::source_spray_render::SprayMaterial>::default());
    }
    crate::source_spray_mips::register(app)?;
    let textures = crate::source_spray_mips::SpriteTextures::stage(
        &mut app.world_mut().resource_mut::<Assets<Image>>(),
    )?;
    app.insert_resource(textures);
    {
        let mut shaders = app.world_mut().resource_mut::<Assets<Shader>>();
        shaders
            .insert(
                crate::source_breaker_render::LIP_SHADER.id(),
                Shader::from_wgsl(
                    crate::source_breaker_render::COMMON_LIP_SHADER,
                    "resident-breaker-lip.wgsl",
                ),
            )
            .map_err(|error| error.to_string())?;
        shaders
            .insert(
                crate::source_spray_render::SPRITE_SHADER.id(),
                Shader::from_wgsl(sprite_shader, "resident-spray-sprites.wgsl"),
            )
            .map_err(|error| error.to_string())?;
    }
    let lace = app.world_mut().resource_mut::<Assets<Image>>().add(lace);
    let targets = stage_targets(app.world_mut(), input, lace)?;
    LiveFeedback::install(app, Arc::clone(&targets.budget), 120)?;
    app.add_plugins(ExtractResourcePlugin::<Targets>::default())
        .insert_resource(targets.clone())
        .add_systems(Update, request_feedback);
    Ok(targets)
}

fn stage_targets(
    world: &mut World,
    input: PreparedBreakerInput,
    lace: Handle<Image>,
) -> Result<Targets, String> {
    let stations = input.gpu_inputs().stations.len() / 16;
    let words = [
        stations.max(1) * 6 * 4,
        40_960 * 4,
        40_960 * 4,
        40_960 * 4,
        1,
    ];
    let buffers = words.map(|len| {
        let mut buffer = ShaderBuffer::from(vec![0_u32; len]);
        buffer.buffer_description.usage |= wgpu::BufferUsages::COPY_SRC;
        world.resource_mut::<Assets<ShaderBuffer>>().add(buffer)
    });
    let mut cutoff = ShaderBuffer::from(vec![0_u32; 2]);
    cutoff.buffer_description.usage |= wgpu::BufferUsages::COPY_SRC | wgpu::BufferUsages::COPY_DST;
    let shadow_cutoff = world.resource_mut::<Assets<ShaderBuffer>>().add(cutoff);
    let bytes = input.frame();
    let frame = std::array::from_fn(|row| {
        UVec4::from_array(std::array::from_fn(|column| {
            let offset = row * 16 + column * 4;
            u32::from_le_bytes(
                bytes[offset..offset + 4]
                    .try_into()
                    .expect("frame size checked"),
            )
        }))
    });
    let budget = Arc::new(Mutex::new(BudgetFeedback::new(32_768)?));
    Ok(Targets {
        input,
        buffers,
        shadow_cutoff,
        lace,
        frame: Arc::new(Mutex::new(frame)),
        camera_ready: Arc::new(AtomicBool::new(false)),
        budget: budget.clone(),
        ready: Arc::new(AtomicBool::new(false)),
        gpu_ready: Arc::new(AtomicBool::new(false)),
        completed_steps: Arc::new(AtomicU32::new(0)),
        error: Arc::new(Mutex::new(None)),
        generation: 0,
        current_generation: Arc::new(AtomicU32::new(0)),
        station_count: Arc::new(AtomicU32::new(
            u32::try_from(stations).map_err(|e| e.to_string())?,
        )),
        switches: Arc::new(AtomicU32::new(0)),
        coverage_started: Arc::new(AtomicBool::new(false)),
        coverage_samples: Arc::new(AtomicU64::new(0)),
        coverage_misses: Arc::new(AtomicU64::new(0)),
        unknown_camera_samples: Arc::new(AtomicU64::new(0)),
        unavailable_samples: Arc::new(AtomicU64::new(0)),
    })
}

#[derive(Component)]
struct FeedbackReader;

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn request_feedback(
    mut commands: Commands,
    targets: Res<Targets>,
    feedback: Res<LiveFeedback>,
    mut started: Local<Option<AssetId<ShaderBuffer>>>,
    readers: Query<Entity, With<FeedbackReader>>,
) {
    let packet = feedback.packet().id();
    if *started == Some(packet) || !targets.ready.load(Ordering::Acquire) {
        return;
    }
    for reader in &readers {
        commands.entity(reader).despawn();
    }
    *started = Some(packet);
    commands
        .spawn((Readback::buffer(feedback.packet().clone()), FeedbackReader))
        .observe(source_spray_feedback::receive);
}

#[derive(Resource)]
struct Display {
    crest: Handle<ShaderBuffer>,
    lip: Option<(
        Entity,
        Handle<Mesh>,
        Handle<crate::source_breaker_render::LipMaterial>,
    )>,
    spray: (
        Entity,
        Handle<Mesh>,
        Handle<crate::source_spray_render::SprayMaterial>,
    ),
}

/// CPU-side projection admission; renderer material fences remain separate.
#[must_use]
pub fn display_ready(world: &World) -> bool {
    let Some(targets) = world.get_resource::<Targets>() else {
        return false;
    };
    let Some(display) = world.get_resource::<Display>() else {
        return false;
    };
    if display.crest != targets.buffers[0] {
        return false;
    }
    let lip = display
        .lip
        .as_ref()
        .map_or(targets.input.ranges().is_empty(), |(entity, _, _)| {
            owns_lip(world, *entity)
        });
    lip && owns_spray(world, display.spray.0)
}

/// Prepare bounded shoreline replacements off the main thread. The current
/// qualification window is provisional and does not yet prove full coverage.
#[derive(Resource)]
struct Following {
    plan: Arc<SampledTerrainPlan>,
    center: [f64; 2],
    task: Option<FollowTask>,
}

struct FollowTask {
    center: [f64; 2],
    task: Task<Result<PreparedBreakerInput, String>>,
}

pub(crate) fn validate_replacement(
    world: &World,
    input: &PreparedBreakerInput,
    plan: &SampledTerrainPlan,
    center: [f64; 2],
) -> Result<(), String> {
    let previous = world
        .get_resource::<Targets>()
        .ok_or("Resident breakers absent")?;
    let (document, surface) = input.identities();
    if document != plan.document_hash()
        || surface != previous.input.identities().1
        || center.iter().any(|value| !value.is_finite())
        || previous.generation != previous.current_generation.load(Ordering::Acquire)
        || previous.generation == u32::MAX
        || previous.error().is_some()
        || !world.contains_resource::<Assets<ShaderBuffer>>()
        || !world.contains_resource::<LiveFeedback>()
    {
        return Err("Resident shoreline replacement differs from its owner or surface".into());
    }
    input.gpu_inputs().validate()?;
    validate_display(world)?;
    Ok(())
}

/// A terrain revision starts a coherent shore/effect generation. The existing
/// renderer, shaders, sprite textures and immutable lace remain shared.
pub(crate) fn replace(
    world: &mut World,
    input: PreparedBreakerInput,
    plan: Arc<SampledTerrainPlan>,
    center: [f64; 2],
) -> Result<(), String> {
    validate_replacement(world, &input, &plan, center)?;
    let shore = world
        .get_resource::<source_shore_sim::Targets>()
        .ok_or("Staged shore absent")?;
    if (shore.document_hash, shore.surface_package_sha256) != input.identities() {
        return Err("Replacement effects do not match the newly staged shore".into());
    }
    let previous = world.resource::<Targets>().clone();
    let generation = previous
        .generation
        .checked_add(1)
        .ok_or("Shoreline generation exhausted")?;
    let mut targets = stage_targets(world, input, previous.lace.clone())?;
    LiveFeedback::replace(world, Arc::clone(&targets.budget), 120)?;
    hide_display(world)?;
    previous.ready.store(false, Ordering::Release);
    previous
        .current_generation
        .store(generation, Ordering::Release);
    targets.generation = generation;
    targets.current_generation = previous.current_generation;
    targets.switches = previous.switches;
    world.insert_resource(targets);
    world.insert_resource(Following {
        plan,
        center,
        task: None,
    });
    Ok(())
}

fn validate_display(world: &World) -> Result<(), String> {
    if let Some(display) = world.get_resource::<Display>()
        && (display
            .lip
            .as_ref()
            .is_some_and(|(entity, _, _)| !owns_lip(world, *entity))
            || !owns_spray(world, display.spray.0))
    {
        return Err("Resident effect display no longer belongs to its owner".into());
    }
    Ok(())
}

fn hide_display(world: &mut World) -> Result<(), String> {
    validate_display(world)?;
    if let Some(display) = world.get_resource::<Display>() {
        let lip = display.lip.as_ref().map(|(entity, _, _)| *entity);
        let spray = display.spray.0;
        if let Some(lip) = lip {
            world.entity_mut(lip).insert(Visibility::Hidden);
        }
        world.entity_mut(spray).insert(Visibility::Hidden);
    }
    Ok(())
}

/// Enable follow for the admitted water host; no module-specific assets are read.
pub fn follow(
    app: &mut App,
    plan: Arc<SampledTerrainPlan>,
    center: [f64; 2],
) -> Result<(), String> {
    let targets = app
        .world()
        .get_resource::<Targets>()
        .ok_or("Resident breakers absent")?;
    if targets.input.identities().0 != plan.document_hash()
        || center.iter().any(|value| !value.is_finite())
        || app.world().contains_resource::<Following>()
    {
        return Err("Invalid resident shoreline follow inputs".into());
    }
    app.insert_resource(Following {
        plan,
        center,
        task: None,
    });
    Ok(())
}

fn distance(a: [f64; 2], b: [f64; 2]) -> f64 {
    (a[0] - b[0]).hypot(a[1] - b[1])
}

/// Observe every host update after the first available display, including
/// frames in which terrain or other prerequisites prevent a follow update.
pub(crate) fn observe_follow(world: &mut World) {
    if !world.contains_resource::<Following>() {
        return;
    }
    let camera = crate::scene_camera::primary(world)
        .ok()
        .and_then(|(entity, _)| {
            world.get::<GlobalTransform>(entity).map(|pose| {
                let position = pose.translation();
                [f64::from(position.x), f64::from(position.z)]
            })
        });
    let Some(targets) = world.get_resource::<Targets>() else {
        return;
    };
    let available = targets.ready.load(Ordering::Acquire) && display_ready(world);
    if available {
        targets.coverage_started.store(true, Ordering::Release);
    }
    if !targets.coverage_started.load(Ordering::Acquire) {
        return;
    }
    targets.coverage_samples.fetch_add(1, Ordering::Relaxed);
    match camera.filter(|camera| camera.iter().all(|value| value.is_finite())) {
        Some(camera) if !targets.input.covers_emission(camera) => {
            targets.coverage_misses.fetch_add(1, Ordering::Relaxed);
        }
        None => {
            targets
                .unknown_camera_samples
                .fetch_add(1, Ordering::Relaxed);
        }
        _ => {}
    }
    if !available {
        targets.unavailable_samples.fetch_add(1, Ordering::Relaxed);
    }
}

/// Stage one latest non-stale candidate; the old display is hidden until its
/// replacement has been encoded and the matching materials are installed.
pub(crate) fn update_follow(world: &mut World, camera: [f64; 2]) -> Result<(), String> {
    let Some(mut follow) = world.remove_resource::<Following>() else {
        return Ok(());
    };
    let result = (|| {
        if camera.iter().any(|value| !value.is_finite()) {
            return Err("Resident shoreline camera is not finite".into());
        }
        if let Some(job) = follow.task.as_mut()
            && let Some(prepared) = check_ready(&mut job.task)
        {
            let prepared_center = job.center;
            follow.task = None;
            if distance(prepared_center, camera) <= FOLLOW_ACCEPTANCE_M {
                let input = prepared?;
                let mut targets = world.resource::<Targets>().clone();
                let stations = input.gpu_inputs().stations.len() / 16;
                let count = u32::try_from(stations).map_err(|e| e.to_string())?;
                let generation = targets
                    .generation
                    .checked_add(1)
                    .ok_or("Shoreline generation exhausted")?;
                let mut output = ShaderBuffer::from(vec![0_u32; stations.max(1) * 6 * 4]);
                output.buffer_description.usage |= wgpu::BufferUsages::COPY_SRC;
                let output = world.resource_mut::<Assets<ShaderBuffer>>().add(output);
                hide_display(world)?;
                targets.ready.store(false, Ordering::Release);
                targets.station_count.store(count, Ordering::Release);
                targets
                    .current_generation
                    .store(generation, Ordering::Release);
                targets.generation = generation;
                targets.gpu_ready = Arc::new(AtomicBool::new(false));
                targets.input = input;
                targets.buffers[0] = output;
                world.insert_resource(targets);
                follow.center = prepared_center;
            }
        }
        let targets = world.resource::<Targets>();
        if follow.task.is_none()
            && targets.ready.load(Ordering::Acquire)
            && display_ready(world)
            && distance(follow.center, camera) > FOLLOW_TRIGGER_M
        {
            let plan = follow.plan.clone();
            let input = targets.input.clone();
            let pool =
                AsyncComputeTaskPool::try_get().ok_or("Shoreline compute pool unavailable")?;
            follow.task = Some(FollowTask {
                center: camera,
                task: pool.spawn(async move { input.retarget_near_camera(&plan, camera) }),
            });
        }
        Ok(())
    })();
    world.insert_resource(follow);
    result
}

/// Identify the actual common lip projection for graphics admission.
#[must_use]
pub fn owns_lip(world: &World, entity: Entity) -> bool {
    world
        .get_resource::<Display>()
        .and_then(|display| display.lip.as_ref())
        .is_some_and(|(owner, mesh, material)| {
            *owner == entity
                && world
                    .get::<Mesh3d>(entity)
                    .is_some_and(|value| &value.0 == mesh)
                && world
                    .get::<MeshMaterial3d<crate::source_breaker_render::LipMaterial>>(entity)
                    .is_some_and(|value| &value.0 == material)
        })
}

/// Identify the actual common spray projection for graphics admission.
#[must_use]
pub fn owns_spray(world: &World, entity: Entity) -> bool {
    world.get_resource::<Display>().is_some_and(|display| {
        let (owner, mesh, material) = &display.spray;
        *owner == entity
            && world
                .get::<Mesh3d>(entity)
                .is_some_and(|value| &value.0 == mesh)
            && world
                .get::<MeshMaterial3d<crate::source_spray_render::SprayMaterial>>(entity)
                .is_some_and(|value| &value.0 == material)
    })
}

/// Install and update visible effects only after the shared simulation and
/// source textures are staged. GPU material admission is checked separately.
#[allow(
    clippy::too_many_lines,
    reason = "Keep the paired effect projection and updates together"
)]
pub(crate) fn update_display(world: &mut World) -> Result<(), String> {
    let Some(targets) = world.get_resource::<Targets>().cloned() else {
        return Ok(());
    };
    if !targets.ready.load(Ordering::Acquire) {
        return Ok(());
    }
    validate_display(world)?;
    let Some(textures) = world
        .get_resource::<crate::source_spray_mips::SpriteTextures>()
        .cloned()
    else {
        return Err("Resident spray textures absent".into());
    };
    if !textures.ready.load(Ordering::Acquire) {
        return Ok(());
    }
    let Some(effect_frame) = crate::source_water_optics::effect_frame(world)? else {
        return Ok(());
    };
    let frame = effect_frame.frame;
    let sky = effect_frame.sky;
    let environment = world
        .get_resource::<crate::source_environment::scene::ResidentEnvironment>()
        .ok_or("Resident breaker environment absent")?;
    let atmosphere = environment.atmosphere.params;
    let transmittance = environment.atmosphere.images[0].clone();
    let sky_lut = environment.atmosphere.images[2].clone();
    let clouds = environment.cloud_view;
    let cloud_panorama = environment.clouds.images[9].clone();
    let cloud_shadow = environment.clouds.images[2].clone();
    let depth = world
        .get_resource::<crate::source_scene_depth::SceneDepth>()
        .ok_or("Resident spray depth absent")?
        .0
        .clone();
    if !world.contains_resource::<Display>() {
        let lip = if let Some(mesh) =
            crate::source_breaker_render::lip_mesh_ranges(targets.input.ranges())
        {
            let mesh = world.resource_mut::<Assets<Mesh>>().add(mesh);
            let material = world
                .resource_mut::<Assets<crate::source_breaker_render::LipMaterial>>()
                .add(crate::source_breaker_render::LipMaterial {
                    frame,
                    crest: targets.buffers[0].clone(),
                    lace: targets.lace.clone(),
                    atmosphere,
                    sky,
                    transmittance: transmittance.clone(),
                    sky_lut: sky_lut.clone(),
                    material: crate::source_breaker_params::unlit_material_words(1.0, 0.0),
                    clouds,
                    cloud_panorama: Some(cloud_panorama.clone()),
                    cloud_shadow: Some(cloud_shadow.clone()),
                });
            let entity = world
                .spawn((
                    Mesh3d(mesh.clone()),
                    MeshMaterial3d(material.clone()),
                    Transform::default(),
                    NoFrustumCulling,
                    NotShadowCaster,
                ))
                .id();
            Some((entity, mesh, material))
        } else {
            None
        };
        let mesh = world.resource_mut::<Assets<Mesh>>().add(
            crate::source_spray_render::sprite_mesh(crate::source_spray_render::MAX_SPRITES)
                .map_err(str::to_owned)?,
        );
        let material = world
            .resource_mut::<Assets<crate::source_spray_render::SprayMaterial>>()
            .add(crate::source_spray_render::SprayMaterial {
                frame,
                clouds,
                cloud_shadow: Some(cloud_shadow.clone()),
                pos: targets.buffers[1].clone(),
                vel: targets.buffers[2].clone(),
                info: targets.buffers[3].clone(),
                crest: targets.buffers[0].clone(),
                ring_head: targets.buffers[4].clone(),
                shadow_cutoff: targets.shadow_cutoff.clone(),
                puff: textures.puff,
                dots: textures.dots,
                depth,
                material: crate::source_breaker_params::unlit_material_words(1.0, 320.0),
            });
        let entity = world
            .spawn((
                Mesh3d(mesh.clone()),
                MeshMaterial3d(material.clone()),
                Transform::default(),
                NoFrustumCulling,
                NotShadowCaster,
            ))
            .id();
        world.insert_resource(Display {
            crest: targets.buffers[0].clone(),
            lip,
            spray: (entity, mesh, material),
        });
    }
    if world.resource::<Display>().crest != targets.buffers[0] {
        let old = world.remove_resource::<Display>().expect("display present");
        if let Some((entity, _, _)) = old.lip {
            world.despawn(entity);
        }
        let lip = if let Some(mesh) =
            crate::source_breaker_render::lip_mesh_ranges(targets.input.ranges())
        {
            let mesh = world.resource_mut::<Assets<Mesh>>().add(mesh);
            let material = world
                .resource_mut::<Assets<crate::source_breaker_render::LipMaterial>>()
                .add(crate::source_breaker_render::LipMaterial {
                    frame,
                    crest: targets.buffers[0].clone(),
                    lace: targets.lace.clone(),
                    atmosphere,
                    sky,
                    transmittance: transmittance.clone(),
                    sky_lut: sky_lut.clone(),
                    material: crate::source_breaker_params::unlit_material_words(1.0, 0.0),
                    clouds,
                    cloud_panorama: Some(cloud_panorama.clone()),
                    cloud_shadow: Some(cloud_shadow.clone()),
                });
            let entity = world
                .spawn((
                    Mesh3d(mesh.clone()),
                    MeshMaterial3d(material.clone()),
                    Transform::default(),
                    NoFrustumCulling,
                    NotShadowCaster,
                ))
                .id();
            Some((entity, mesh, material))
        } else {
            None
        };
        world
            .resource_mut::<Assets<crate::source_spray_render::SprayMaterial>>()
            .get_mut(&old.spray.2)
            .ok_or("Resident spray material missing")?
            .crest = targets.buffers[0].clone();
        world.entity_mut(old.spray.0).insert(Visibility::Inherited);
        world.insert_resource(Display {
            crest: targets.buffers[0].clone(),
            lip,
            spray: old.spray,
        });
        targets.switches.fetch_add(1, Ordering::AcqRel);
    }
    let display = world.resource::<Display>();
    let lip_material = display
        .lip
        .as_ref()
        .map(|(_, _, material)| material.clone());
    let spray_material = display.spray.2.clone();
    if let Some(handle) = lip_material {
        let mut materials =
            world.resource_mut::<Assets<crate::source_breaker_render::LipMaterial>>();
        let mut material = materials
            .get_mut(&handle)
            .ok_or("Resident lip material missing")?;
        material.frame = frame;
        material.sky = sky;
        material.atmosphere = atmosphere;
        material.clouds = clouds;
        material.transmittance = transmittance;
        material.sky_lut = sky_lut;
        material.cloud_panorama = Some(cloud_panorama);
        material.cloud_shadow = Some(cloud_shadow.clone());
    }
    let mut materials = world.resource_mut::<Assets<crate::source_spray_render::SprayMaterial>>();
    let mut material = materials
        .get_mut(&spray_material)
        .ok_or("Resident spray material missing")?;
    material.frame = frame;
    material.clouds = clouds;
    material.cloud_shadow = Some(cloud_shadow);
    material.pos = targets.buffers[1].clone();
    material.vel = targets.buffers[2].clone();
    material.info = targets.buffers[3].clone();
    material.ring_head = targets.buffers[4].clone();
    material.shadow_cutoff = targets.shadow_cutoff.clone();
    Ok(())
}
