//! Shared composition of explicitly admitted ocean, surface and shore inputs.
//! Staging is an observation of CPU publication, not a renderer fence or a gate verdict.
use crate::{
    scene_camera, source_breaker_contours, source_breaker_geometry, source_breaker_gpu,
    source_breaker_render, source_environment, source_ocean, source_ocean_geometry,
    source_shore_sim, source_terrain, source_water_optics,
};
use bevy::prelude::*;
use bevy::render::{
    extract_resource::ExtractResource,
    renderer::{RenderDevice, RenderQueue},
};
use forge_assets::{
    source_ocean::OceanInputSource, source_shore_sim_package::ShoreSimPackage,
    source_water_surface_package::WaterSurfacePackage,
};
use forge_core::ContentHash;
use forge_project::TerrainDocument;
use forge_world::sampled_terrain::SampledTerrainPlan;
use std::sync::{Arc, atomic::Ordering};
use wgpu::util::DeviceExt;
pub mod readiness;

/// CPU preparation retains no filesystem location or map identity.
pub struct PreparedHost {
    document: ContentHash,
    ocean: source_ocean::PreparedOcean,
    geometry: source_ocean_geometry::PreparedGeometry,
    shore: source_shore_sim::PreparedShoreSim,
    optics: source_water_optics::PreparedOptics,
    surface: Arc<WaterSurfacePackage>,
}

/// One admitted water host and the initial shoreline generation for its terrain.
/// The retained plan supports camera-following without reloading module assets.
pub struct PreparedResidentScene {
    water: PreparedHost,
    display: PreparedBreakerDisplay,
    plan: Arc<SampledTerrainPlan>,
    center: [f64; 2],
}

impl PreparedResidentScene {
    #[must_use]
    pub const fn document_hash(&self) -> ContentHash {
        self.water.document_hash()
    }

    #[must_use]
    pub fn station_count(&self) -> usize {
        self.display.input.programs.stations.len()
    }

    /// Install the water and its admitted effects in dependency order.
    /// `follow` must remain enabled for a moving view; a fixed view is useful
    /// only for an explicitly controlled qualification baseline.
    pub fn install(self, app: &mut App, follow: bool) -> Result<(), String> {
        self.water.install(app)?;
        crate::source_breaker_resident::install(app, self.display)?;
        if follow {
            crate::source_breaker_resident::follow(app, self.plan, self.center)?;
        }
        Ok(())
    }

    /// Validate the whole replacement before its caller publishes a new terrain.
    pub fn validate_replacement(&self, world: &World, expected: ContentHash) -> Result<(), String> {
        self.water.validate_replacement(world, expected)?;
        crate::source_breaker_resident::validate_replacement(
            world,
            &self.display.input,
            &self.plan,
            self.center,
        )
    }

    /// Reproject a terrain revision through the existing renderer and shared
    /// shoreline. The admitted FFT and its live clock remain resident.
    pub fn replace(self, world: &mut World, expected: ContentHash) -> Result<(), String> {
        self.validate_replacement(world, expected)?;
        self.water.replace(world, expected)?;
        crate::source_breaker_resident::replace(world, self.display.input, self.plan, self.center)
    }
}

/// GPU program derivatives and correction texture for one admitted terrain revision.
#[derive(Clone)]
pub struct PreparedBreakerPrograms {
    document: ContentHash,
    surface: ContentHash,
    crest: String,
    spray: String,
    stations: Vec<[f32; 4]>,
    station_bytes: Vec<u8>,
    ranges: Vec<source_breaker_geometry::LipRange>,
}

impl PreparedBreakerPrograms {
    #[must_use]
    pub fn crest(&self) -> &str {
        &self.crest
    }
    #[must_use]
    pub fn spray(&self) -> &str {
        &self.spray
    }
    #[must_use]
    pub fn stations(&self) -> &[[f32; 4]] {
        &self.stations
    }
    #[must_use]
    pub fn ranges(&self) -> &[source_breaker_geometry::LipRange] {
        &self.ranges
    }
}

/// Coherent inputs shared with the render world without duplicating terrain bytes.
#[derive(Resource, Clone, ExtractResource)]
pub struct PreparedBreakerInput {
    programs: Arc<PreparedBreakerPrograms>,
    shore: source_shore_sim::PreparedShoreSim,
    surface_params: [u8; 32],
    frame: [u8; 720],
    window: Option<source_breaker_contours::ContourConfig>,
}

/// Effect inputs and the lace image from the same admitted surface.
pub struct PreparedBreakerDisplay {
    pub(crate) input: PreparedBreakerInput,
    pub(crate) lace: Image,
}

impl source_breaker_gpu::InputFiles for PreparedBreakerInput {
    fn file(&self, name: &str) -> Option<&[u8]> {
        match name {
            "kernel.wgsl" => Some(self.programs.crest.as_bytes()),
            "spray-update.wgsl" => Some(self.programs.spray.as_bytes()),
            "surface-params.bin" => Some(&self.surface_params),
            _ => self.shore.breaker_file(name),
        }
    }

    fn terrain_layout(&self) -> Option<source_breaker_gpu::TerrainTextureLayout> {
        Some(self.shore.texture_layout())
    }
}

impl PreparedBreakerInput {
    /// Source frame admitted with the same surface as the shore generation.
    #[must_use]
    pub const fn frame(&self) -> &[u8; 720] {
        &self.frame
    }

    #[must_use]
    pub fn ranges(&self) -> &[source_breaker_geometry::LipRange] {
        self.programs.ranges()
    }

    /// Identity of the terrain and water surface used to derive these effects.
    #[must_use]
    pub fn identities(&self) -> (ContentHash, ContentHash) {
        (self.programs.document, self.programs.surface)
    }

    /// Whether the prepared rectangular domain contains the entire source
    /// emission disk at this camera position. Unknown domains are not covered.
    #[must_use]
    pub fn covers_emission(&self, camera: [f64; 2]) -> bool {
        self.window
            .is_some_and(|window| window_covers_emission(window, camera))
    }

    /// Borrow the matching resident shore inputs for an ordered coupled simulation.
    #[must_use]
    pub fn shore_gpu_inputs(&self) -> source_shore_sim::GpuInputs<'_> {
        self.shore.gpu_inputs()
    }

    #[must_use]
    pub fn gpu_inputs(&self) -> source_breaker_gpu::Inputs<'_> {
        source_breaker_gpu::Inputs {
            files: self,
            stations: &self.programs.station_bytes,
            integrate: true,
        }
    }

    /// Prepare another bounded shoreline using the same admitted terrain,
    /// surface and resident programs, without reopening the water package.
    pub fn retarget_near_camera(
        &self,
        plan: &SampledTerrainPlan,
        camera_xz: [f64; 2],
    ) -> Result<Self, String> {
        if plan.document_hash() != self.programs.document
            || self
                .programs
                .crest
                .matches("if ( i >= arrayLength( &breakersStations ) )")
                .count()
                != 1
        {
            return Err("Resident shoreline requires its admitted terrain and program".into());
        }
        let (config, packed) = resident_contours(plan, &self.frame, camera_xz)?;
        let station_bytes = packed
            .stations
            .iter()
            .flatten()
            .flat_map(|value| value.to_le_bytes())
            .collect();
        let mut next = self.clone();
        next.programs = Arc::new(PreparedBreakerPrograms {
            document: self.programs.document,
            surface: self.programs.surface,
            crest: self.programs.crest.clone(),
            spray: self.programs.spray.clone(),
            stations: packed.stations,
            station_bytes,
            ranges: packed.ranges,
        });
        next.window = Some(config);
        next.gpu_inputs().validate()?;
        Ok(next)
    }
}

pub fn water_level_from_frame(frame: &[u8]) -> Result<f64, String> {
    let level = f32::from_le_bytes(
        frame
            .get(588..592)
            .ok_or("Water frame size differs")?
            .try_into()
            .map_err(|_| "Water frame size differs")?,
    );
    if !level.is_finite() {
        return Err("Water level is not finite".into());
    }
    Ok(f64::from(level))
}

#[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
fn breaker_window(
    plan: &SampledTerrainPlan,
    frame: &[u8],
    camera_xz: [f64; 2],
    margin: f64,
) -> Result<source_breaker_contours::ContourConfig, String> {
    if camera_xz.iter().any(|value| !value.is_finite()) {
        return Err("Breaker camera position is not finite".into());
    }
    let spacing = plan.raster().source().sample_spacing_m.max(0.5);
    let emission = f64::from(crate::source_breaker_params::BreakerParams::default().emit_range);
    // Do not coarsen the established sampling grid just to obtain the margin.
    let radius = (emission + margin).min((spacing * 512.0).max(emission));
    let cells = ((2.0 * radius / spacing).ceil()).clamp(1.0, 1024.0) as u32;
    let sea_level = water_level_from_frame(frame)?;
    Ok(source_breaker_contours::ContourConfig {
        min: [camera_xz[0] - radius, camera_xz[1] - radius],
        max: [camera_xz[0] + radius, camera_xz[1] + radius],
        cells: [cells; 2],
        water_level: sea_level,
        station_spacing: 0.6,
    })
}

fn window_covers_emission(
    window: source_breaker_contours::ContourConfig,
    camera: [f64; 2],
) -> bool {
    let radius = f64::from(crate::source_breaker_params::BreakerParams::default().emit_range);
    camera.iter().all(|v| v.is_finite())
        && (0..2).all(|axis| {
            camera[axis] - radius >= window.min[axis] && camera[axis] + radius <= window.max[axis]
        })
}

/// Prefer a margin covering one recenter request and one accepted task drift.
/// When this exceeds source limits, fall back to the previous emission window;
/// callers can observe the reduced coverage instead of losing the water host.
fn resident_contours(
    plan: &SampledTerrainPlan,
    frame: &[u8],
    camera: [f64; 2],
) -> Result<
    (
        source_breaker_contours::ContourConfig,
        source_breaker_geometry::PackedContours,
    ),
    String,
> {
    let margin = crate::source_breaker_resident::FOLLOW_TRIGGER_M
        + crate::source_breaker_resident::FOLLOW_ACCEPTANCE_M;
    let expanded = breaker_window(plan, frame, camera, margin)?;
    let original = breaker_window(plan, frame, camera, 0.0)?;
    let sample = |config| {
        let contours = source_breaker_contours::shore_contours(config, |x, z| {
            plan.height_at(x, z).unwrap_or(f64::NAN)
        })?;
        source_breaker_geometry::pack_contours(&contours)
    };
    select_resident_window(expanded, original, sample)
}

fn select_resident_window(
    expanded: source_breaker_contours::ContourConfig,
    original: source_breaker_contours::ContourConfig,
    sample: impl Fn(
        source_breaker_contours::ContourConfig,
    ) -> Result<source_breaker_geometry::PackedContours, &'static str>,
) -> Result<
    (
        source_breaker_contours::ContourConfig,
        source_breaker_geometry::PackedContours,
    ),
    String,
> {
    match sample(expanded) {
        Ok(packed) => Ok((expanded, packed)),
        Err("Shoreline exceeds station limit" | "Shoreline grid is too complex") => {
            sample(original)
                .map(|packed| (original, packed))
                .map_err(str::to_owned)
        }
        Err(error) => Err(error.to_owned()),
    }
}

/// Shared GPU state for the source shore, crest and spray passes.
/// Its deposit buffer is consumed by shore before the next spray pass writes it.
/// This state replaces the independent resident shore simulation when installed.
pub struct CoupledBreakerGpu {
    shore: source_shore_sim::OrderedSimulation,
    breaker: source_breaker_gpu::Resources,
    stations: u32,
    admitted: PreparedBreakerInput,
    last_head: Option<wgpu::Buffer>,
    shadow_handoff: Option<(
        crate::source_spray_generation::ShadowHandoff,
        wgpu::Buffer,
        wgpu::Buffer,
        wgpu::BindGroup,
    )>,
}

/// One ordered water step; a single packed frame is shared by all three passes.
pub struct CoupledBreakerFrame<'a> {
    pub frame: &'a [u8],
    pub ocean: &'a wgpu::Buffer,
    pub parameters: &'a wgpu::Buffer,
    pub spray_parameters: &'a wgpu::Buffer,
    pub displacement: &'a wgpu::TextureView,
    pub particles: [&'a wgpu::Buffer; 4],
    pub output: &'a wgpu::Buffer,
    pub station_count: u32,
    pub emit: bool,
}

impl CoupledBreakerGpu {
    pub(crate) fn uses_input(&self, input: &PreparedBreakerInput) -> bool {
        Arc::ptr_eq(&self.admitted.programs, &input.programs)
    }
    /// Reuse the exact admitted resident shore state without allocating another one.
    /// The caller must stop independent updates and supply the actual completed step.
    pub fn adopt(
        device: &RenderDevice,
        queue: &RenderQueue,
        input: &PreparedBreakerInput,
        shore: &source_shore_sim::GpuSimulation,
        completed_step: u32,
    ) -> Result<Self, String> {
        if !shore.belongs_to(&input.shore) {
            return Err("Resident shore generation differs from breaker input".into());
        }
        let breaker_input = input.gpu_inputs();
        breaker_input.validate_for_device(device.wgpu_device())?;
        let stations = u32::try_from(breaker_input.stations.len() / 16)
            .map_err(|_| "Breaker station count overflow")?;
        Ok(Self {
            shore: source_shore_sim::OrderedSimulation::new(shore.clone(), completed_step),
            breaker: source_breaker_gpu::Resources::new(device, queue, &breaker_input),
            stations,
            admitted: input.clone(),
            last_head: None,
            shadow_handoff: None,
        })
    }

    /// Create both source simulations against the same bounded deposit buffer.
    pub fn new(
        device: &RenderDevice,
        queue: &RenderQueue,
        input: &PreparedBreakerInput,
        deposit: wgpu::Buffer,
    ) -> Result<Self, String> {
        if deposit.size() != 768 * 768 * 4 || !deposit.usage().contains(wgpu::BufferUsages::STORAGE)
        {
            return Err("Invalid shared shore deposit buffer".into());
        }
        let breaker_input = input.gpu_inputs();
        breaker_input.validate_for_device(device.wgpu_device())?;
        let stations = u32::try_from(breaker_input.stations.len() / 16)
            .map_err(|_| "Breaker station count overflow")?;
        Ok(Self {
            shore: source_shore_sim::OrderedSimulation::new(
                source_shore_sim::initialize_gpu(
                    &input.shore_gpu_inputs(),
                    device.wgpu_device(),
                    queue,
                    Some(deposit),
                )?,
                0,
            ),
            breaker: source_breaker_gpu::Resources::new(device, queue, &breaker_input),
            stations,
            admitted: input.clone(),
            last_head: None,
            shadow_handoff: None,
        })
    }

    /// Replace only the station binding, retaining the ordered shore state,
    /// deposit, particle buffers owned by the caller, and compiled programs.
    /// Captures the current ring head before new emission so older particles do
    /// not read new-generation crest shadows. The caller must publish the
    /// matching crest output, cutoff and visible meshes together.
    pub fn replace_stations(
        &mut self,
        device: &RenderDevice,
        encoder: &mut wgpu::CommandEncoder,
        head: &wgpu::Buffer,
        cutoff: &wgpu::Buffer,
        input: &PreparedBreakerInput,
    ) -> Result<(), String> {
        if !self.shore.gpu().belongs_to(&input.shore)
            || self.admitted.identities() != input.identities()
            || self.admitted.surface_params != input.surface_params
            || self.admitted.frame != input.frame
            || self.admitted.programs.crest != input.programs.crest
            || self.admitted.programs.spray != input.programs.spray
            || self.last_head.as_ref() != Some(head)
            || self
                .shadow_handoff
                .as_ref()
                .is_some_and(|(_, previous_head, previous_cutoff, _)| {
                    previous_head != head || previous_cutoff != cutoff
                })
            || input
                .programs
                .crest
                .matches("if ( i >= arrayLength( &breakersStations ) )")
                .count()
                != 1
        {
            return Err("Replacement stations do not match the admitted water generation".into());
        }
        let gpu = input.gpu_inputs();
        gpu.validate()?;
        crate::source_spray_generation::ShadowHandoff::validate(head, cutoff)
            .map_err(str::to_owned)?;
        let count =
            u32::try_from(gpu.stations.len() / 16).map_err(|_| "Breaker station count overflow")?;
        let bytes = if gpu.stations.is_empty() {
            &[0_u8; 16][..]
        } else {
            gpu.stations
        };
        let stations = device
            .wgpu_device()
            .create_buffer_init(&wgpu::util::BufferInitDescriptor {
                label: Some("replacement crest stations"),
                contents: bytes,
                usage: wgpu::BufferUsages::STORAGE,
            });
        if self.shadow_handoff.is_none() {
            let handoff = crate::source_spray_generation::ShadowHandoff::new(device.wgpu_device());
            let group = handoff
                .bind(device.wgpu_device(), head, cutoff)
                .map_err(str::to_owned)?;
            self.shadow_handoff = Some((handoff, head.clone(), cutoff.clone(), group));
        }
        let (handoff, _, _, _) = self.shadow_handoff.as_ref().expect("handoff installed");
        handoff
            .begin(encoder, head, cutoff)
            .map_err(str::to_owned)?;
        self.breaker.stations = stations;
        self.stations = count;
        self.admitted = input.clone();
        Ok(())
    }

    /// Encode shore, crest and spray in source order using one frame and deposit.
    pub fn encode(
        &mut self,
        device: &wgpu::Device,
        encoder: &mut wgpu::CommandEncoder,
        input: &CoupledBreakerFrame<'_>,
    ) -> Result<wgpu::BindGroup, String> {
        if input.frame.len() != 720 || input.station_count != self.stations {
            return Err("Coupled breaker frame or station count differs".into());
        }
        if self
            .shadow_handoff
            .as_ref()
            .is_some_and(|(_, head, _, _)| head != input.particles[3])
        {
            return Err("Coupled spray ring changed after station handoff".into());
        }
        for (buffer, minimum, label) in [
            (input.ocean, 224, "ocean"),
            (input.parameters, 32, "breaker"),
            (input.spray_parameters, 240, "spray"),
        ] {
            if buffer.size() < minimum
                || buffer.size() > device.limits().max_uniform_buffer_binding_size
                || !buffer.usage().contains(wgpu::BufferUsages::UNIFORM)
            {
                return Err(format!("Invalid coupled {label} uniform buffer"));
            }
        }
        let crest_bytes = u64::from(self.stations.max(1)) * 6 * 16;
        for (buffer, minimum) in input
            .particles
            .into_iter()
            .zip([40_960 * 16, 40_960 * 16, 40_960 * 16, 4])
            .chain(std::iter::once((input.output, crest_bytes)))
        {
            if buffer.size() < minimum
                || buffer.size() > device.limits().max_storage_buffer_binding_size
                || !buffer.usage().contains(wgpu::BufferUsages::STORAGE)
            {
                return Err("Invalid coupled breaker storage buffer".into());
            }
        }
        let frame = device.create_buffer_init(&wgpu::util::BufferInitDescriptor {
            label: Some("Coupled water source frame"),
            contents: input.frame,
            usage: wgpu::BufferUsages::UNIFORM,
        });
        let step = u32::from_le_bytes(input.frame[576..580].try_into().expect("checked frame"));
        let (_, spray_group) = self.shore.encode_water_step(
            &self.breaker,
            device,
            encoder,
            &source_breaker_gpu::CrestFrame {
                frame: &frame,
                ocean: input.ocean,
                parameters: input.parameters,
                spray_parameters: input.spray_parameters,
                displacement: input.displacement,
                particles: input.particles,
                output: input.output,
                station_count: input.station_count,
                emit: input.emit,
            },
            step,
        )?;
        if let Some((handoff, _, _, group)) = &self.shadow_handoff {
            handoff.retire(encoder, group);
        }
        self.last_head = Some(input.particles[3].clone());
        Ok(spray_group)
    }

    /// Shore state and deposit are available for publication and diagnostics.
    #[must_use]
    pub const fn shore(&self) -> &source_shore_sim::GpuSimulation {
        self.shore.gpu()
    }
}

#[derive(Resource, Clone, Debug, Default, serde::Serialize)]
pub struct Status {
    pub document: Option<ContentHash>,
    pub staged: bool,
    pub error: Option<String>,
}

#[derive(Resource)]
struct Pending {
    geometry: source_ocean_geometry::PreparedGeometry,
    optics: source_water_optics::PreparedOptics,
}

#[derive(Resource)]
struct SurfaceIdentity(ContentHash);

fn validate_projection(world: &World) -> Result<(), String> {
    match (
        world.get_resource::<source_ocean_geometry::ResidentGeometry>(),
        world.get_resource::<source_water_optics::ResidentOptics>(),
    ) {
        (None, None) => Ok(()),
        (Some(geometry), Some(optics))
            if geometry.entity == optics.entity
                && world
                    .get::<MeshMaterial3d<source_water_optics::OpticalMaterial>>(geometry.entity)
                    .is_some_and(|material| material.0 == optics.material)
                && world.get::<ChildOf>(geometry.entity).is_none()
                && world.get::<Transform>(geometry.entity) == Some(&Transform::IDENTITY) =>
        {
            Ok(())
        }
        _ => Err("Water projection no longer belongs to its admitted host".into()),
    }
}

#[derive(Resource, Default)]
struct SelectionCadence {
    camera: scene_camera::SourceViewCadence,
    view: Option<forge_world::terrain_lod::CdlodView>,
    geometry_dirty: bool,
}

#[derive(SystemSet, Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub struct WaterHostSet;

#[derive(Component)]
struct OwnedUnderwaterCamera;

/// Set the shared depth-copy and immersion requirements before the caller
/// allocates its first view texture. Display exposure/tonemapping stays theirs.
pub fn prepare_camera(world: &mut World, camera: Entity) -> Result<(), String> {
    select_underwater_camera(world, camera)
}

fn select_underwater_camera(world: &mut World, camera: Entity) -> Result<(), String> {
    use bevy::render::render_resource::TextureUsages;
    if world.get::<Camera3d>(camera).is_none()
        || world
            .get::<scene_camera::OffscreenSceneCamera>(camera)
            .is_some()
    {
        return Err("Underwater requires the primary scene camera".into());
    }
    let marked: Vec<_> = world
        .query_filtered::<(Entity, Has<OwnedUnderwaterCamera>), With<crate::source_underwater::SourceUnderwaterCamera>>()
        .iter(world)
        .collect();
    if marked
        .iter()
        .any(|(entity, owned)| *entity != camera && !owned)
    {
        return Err("A foreign underwater camera is already selected".into());
    }
    for (entity, owned) in marked {
        if owned && entity != camera {
            crate::source_underwater::unselect_camera(world, entity);
            world.entity_mut(entity).remove::<OwnedUnderwaterCamera>();
        }
    }
    if world
        .get::<crate::source_underwater::SourceUnderwaterCamera>(camera)
        .is_none()
    {
        world.entity_mut(camera).insert((
            crate::source_underwater::SourceUnderwaterCamera,
            OwnedUnderwaterCamera,
        ));
    }
    let mut camera3d = world
        .get_mut::<Camera3d>(camera)
        .ok_or("Underwater camera disappeared")?;
    let usage: TextureUsages = camera3d.depth_texture_usages.into();
    let required =
        TextureUsages::RENDER_ATTACHMENT | TextureUsages::TEXTURE_BINDING | TextureUsages::COPY_SRC;
    if !usage.contains(required) {
        camera3d.depth_texture_usages = (usage | required).into();
    }
    Ok(())
}

/// Identify the actual optical projection owned by this water host.
#[must_use]
pub fn owns(world: &World, entity: Entity) -> bool {
    let Some(status) = world.get_resource::<Status>() else {
        return false;
    };
    let Some(optics) = world.get_resource::<source_water_optics::ResidentOptics>() else {
        return false;
    };
    let Some(document) = status.document else {
        return false;
    };
    status.staged
        && status.error.is_none()
        && world
            .get_resource::<source_shore_sim::Targets>()
            .is_some_and(|shore| shore.document_hash == document)
        && world
            .get_resource::<source_terrain::host::Status>()
            .is_some_and(|terrain| terrain.document == Some(document))
        && world
            .get_resource::<source_ocean_geometry::ResidentGeometry>()
            .is_some_and(|geometry| geometry.entity == optics.entity)
        && source_ocean_geometry::owns(world, entity)
        && world
            .get::<MeshMaterial3d<source_water_optics::OpticalMaterial>>(entity)
            .is_some_and(|material| material.0 == optics.material)
}

impl PreparedHost {
    fn validate_replacement(&self, world: &World, expected: ContentHash) -> Result<(), String> {
        let status = world.get_resource::<Status>().ok_or("Water host absent")?;
        let terrain = world
            .get_resource::<source_terrain::host::Status>()
            .ok_or("Water terrain absent")?;
        if status.document != Some(expected)
            || status.error.is_some()
            || terrain.error.is_some()
            || ![Some(expected), Some(self.document)].contains(&terrain.document)
            || !self.ocean.matches_staged(world)
            || world
                .get_resource::<SurfaceIdentity>()
                .is_none_or(|identity| identity.0 != self.surface.identity())
            || !world.contains_resource::<Assets<Image>>()
            || !world.contains_resource::<Assets<Mesh>>()
            || !world.contains_resource::<Assets<source_water_optics::OpticalMaterial>>()
        {
            return Err(
                "Water replacement requires its current terrain owner and unchanged source inputs"
                    .into(),
            );
        }
        validate_projection(world)
    }

    fn replace(self, world: &mut World, expected: ContentHash) -> Result<(), String> {
        self.validate_replacement(world, expected)?;
        if world.resource::<source_terrain::host::Status>().document != Some(self.document) {
            return Err("The replacement terrain must be published before its water".into());
        }
        self.shore.stage(world)?;
        if let Some(geometry) = world.get_resource::<source_ocean_geometry::ResidentGeometry>() {
            let root = geometry.entity;
            world.entity_mut(root).insert(Visibility::Hidden);
        }
        world.insert_resource(Status {
            document: Some(self.document),
            ..default()
        });
        world.insert_resource(Pending {
            geometry: self.geometry,
            optics: self.optics,
        });
        world.insert_resource(SelectionCadence::default());
        Ok(())
    }

    /// Water height from the same admitted frame used by the shared shoreline.
    pub fn water_level_m(&self) -> Result<f64, String> {
        water_level_from_frame(
            self.surface
                .file("water-frame.bin")
                .ok_or("Water frame absent")?,
        )
    }

    /// Prepare one bounded shore simulation at a module-selected position.
    /// Simulation, optics and coupled effects retain the same parameters.
    /// This does not move a resident window or transfer its live GPU state.
    pub fn with_shore_simulation_center(mut self, center: [f64; 2]) -> Result<Self, String> {
        let shore = self.shore.with_simulation_center(center)?;
        self.optics
            .set_simulation_parameters(shore.shore_field().simulation_parameters());
        self.shore = shore;
        Ok(self)
    }

    /// Validate all inputs and identities before installing anything into the world.
    pub fn new(
        ocean: &impl OceanInputSource,
        shore: &ShoreSimPackage,
        surface: impl Into<Arc<WaterSurfacePackage>>,
        document: &TerrainDocument,
        plan: &SampledTerrainPlan,
        heights: &[u8],
    ) -> Result<Self, String> {
        plan.require_document(document)
            .map_err(|error| error.to_string())?;
        let surface = surface.into();
        let ocean = source_ocean::PreparedOcean::new(ocean)?;
        let water_level = water_level_from_frame(
            surface
                .file("water-frame.bin")
                .ok_or("Water frame absent")?,
        )?;
        let geometry = source_ocean_geometry::PreparedGeometry::new_for_terrain(
            surface
                .file("ocean-geometry.wgsl")
                .ok_or("Ocean geometry shader absent")?,
            surface
                .file("uniform-waterSurface.bin")
                .ok_or("Ocean surface parameters absent")?,
            plan,
            heights,
            water_level,
        )?;
        let simulation =
            source_shore_sim::PreparedShoreSim::new(shore, &surface, document, plan, heights)?;
        let optics = source_water_optics::PreparedOptics::new(
            &surface,
            document,
            plan,
            Some(simulation.shore_field()),
            Some(shore),
        )?;
        Ok(Self {
            document: plan.document_hash(),
            ocean,
            geometry,
            shore: simulation,
            optics,
            surface,
        })
    }

    #[must_use]
    pub const fn document_hash(&self) -> ContentHash {
        self.document
    }

    /// Prepare the water and bounded camera-following shoreline from the same
    /// admitted terrain. Preparation performs no Bevy world mutation.
    pub fn prepare_resident_scene(
        self,
        plan: Arc<SampledTerrainPlan>,
        center: [f64; 2],
    ) -> Result<PreparedResidentScene, String> {
        let display = self.breaker_display(self.breakers_near_camera(&plan, center)?)?;
        Ok(PreparedResidentScene {
            water: self,
            display,
            plan,
            center,
        })
    }

    /// Derive shoreline stations only from this host's admitted terrain revision.
    /// The caller supplies the zone's scan settings; this does not stage effects.
    pub fn shoreline_stations(
        &self,
        plan: &SampledTerrainPlan,
        scan: source_breaker_geometry::ShorelineScan,
    ) -> Result<Vec<[f32; 4]>, String> {
        if plan.document_hash() != self.document {
            return Err("Water shoreline terrain revision differs".into());
        }
        source_breaker_geometry::shoreline_stations(scan, |x, z| {
            plan.height_at(x, z).unwrap_or(f64::NAN)
        })
        .map_err(str::to_owned)
    }

    /// Derive separate shoreline contours from this host's admitted terrain revision.
    pub fn shoreline_contours(
        &self,
        plan: &SampledTerrainPlan,
        config: source_breaker_contours::ContourConfig,
    ) -> Result<Vec<source_breaker_contours::ShoreContour>, String> {
        if plan.document_hash() != self.document {
            return Err("Water shoreline terrain revision differs".into());
        }
        source_breaker_contours::shore_contours(config, |x, z| {
            plan.height_at(x, z).unwrap_or(f64::NAN)
        })
        .map_err(str::to_owned)
    }

    /// Prepare the source crest texture from the already admitted surface package.
    /// This uses its existing ten lace mips and does not stage an effect or load a new asset.
    pub fn breaker_lace(&self) -> Result<Image, String> {
        let mut data = Vec::with_capacity(1_398_100);
        for mip in 0..10 {
            let name = format!("lace-m{mip}.rgba8.bin");
            let bytes = self.surface.file(&name).ok_or_else(|| {
                format!("Water surface is missing the admitted crest texture: {name}")
            })?;
            data.extend_from_slice(bytes);
        }
        source_breaker_render::lace_image(512, 10, data).map_err(str::to_owned)
    }

    /// Bind the display texture to the same terrain and surface as its GPU inputs.
    pub fn breaker_display(
        &self,
        input: PreparedBreakerInput,
    ) -> Result<PreparedBreakerDisplay, String> {
        if input.identities() != (self.document, self.surface.identity()) {
            return Err("Breaker display belongs to another water generation".into());
        }
        Ok(PreparedBreakerDisplay {
            input,
            lace: self.breaker_lace()?,
        })
    }

    /// Prepare the built-in common breaker programs for this admitted terrain.
    pub fn common_breaker_programs(
        &self,
        plan: &SampledTerrainPlan,
        scan: source_breaker_geometry::ShorelineScan,
    ) -> Result<PreparedBreakerPrograms, String> {
        self.breaker_programs(
            plan,
            source_breaker_gpu::COMMON_CREST_PROGRAM,
            source_breaker_gpu::COMMON_SPRAY_PROGRAM,
            source_breaker_gpu::COMMON_CREST_SOURCE_STATIONS,
            scan,
        )
    }

    /// Prepare common breaker programs and separate ribbons for all admitted contours.
    pub fn common_breaker_contour_programs(
        &self,
        plan: &SampledTerrainPlan,
        config: source_breaker_contours::ContourConfig,
    ) -> Result<PreparedBreakerPrograms, String> {
        let contours = self.shoreline_contours(plan, config)?;
        let packed = source_breaker_geometry::pack_contours(&contours).map_err(str::to_owned)?;
        self.prepare_breaker_programs(
            plan,
            packed,
            source_breaker_gpu::COMMON_CREST_PROGRAM,
            source_breaker_gpu::COMMON_SPRAY_PROGRAM,
            source_breaker_gpu::COMMON_CREST_SOURCE_STATIONS,
        )
    }

    /// Prepare a bounded source-density shoreline around an admitted camera.
    /// Moving beyond this window requires a new prepared generation; callers
    /// must not treat this initial window as full-world coverage.
    pub fn breakers_near_camera(
        &self,
        plan: &SampledTerrainPlan,
        camera_xz: [f64; 2],
    ) -> Result<PreparedBreakerInput, String> {
        let frame = self
            .surface
            .file("water-frame.bin")
            .ok_or("Water frame absent")?;
        if plan.document_hash() != self.document {
            return Err("Water breaker terrain revision differs".into());
        }
        let (config, packed) = resident_contours(plan, frame, camera_xz)?;
        let mut programs = self.prepare_breaker_programs(
            plan,
            packed,
            source_breaker_gpu::COMMON_CREST_PROGRAM,
            source_breaker_gpu::COMMON_SPRAY_PROGRAM,
            source_breaker_gpu::COMMON_CREST_SOURCE_STATIONS,
        )?;
        let count =
            u32::try_from(programs.stations.len()).map_err(|_| "Breaker station count overflow")?;
        programs.crest =
            source_breaker_gpu::prepare_resident_station_guard(&programs.crest, count)?;
        let mut input = self.breaker_input(&programs)?;
        input.window = Some(config);
        Ok(input)
    }

    /// Adapt already admitted source programs to the same terrain as this host.
    /// Callers must verify the program provenance and pins before passing them.
    pub fn breaker_programs(
        &self,
        plan: &SampledTerrainPlan,
        crest: &str,
        spray: &str,
        source_station_count: u32,
        scan: source_breaker_geometry::ShorelineScan,
    ) -> Result<PreparedBreakerPrograms, String> {
        if plan.document_hash() != self.document {
            return Err("Water breaker terrain revision differs".into());
        }
        let stations = self.shoreline_stations(plan, scan)?;
        let ranges = if stations.len() >= 2 {
            vec![source_breaker_geometry::LipRange {
                start: 0,
                count: u32::try_from(stations.len())
                    .map_err(|_| "Breaker station count overflow")?,
            }]
        } else {
            Vec::new()
        };
        self.prepare_breaker_programs(
            plan,
            source_breaker_geometry::PackedContours { stations, ranges },
            crest,
            spray,
            source_station_count,
        )
    }

    fn prepare_breaker_programs(
        &self,
        plan: &SampledTerrainPlan,
        packed: source_breaker_geometry::PackedContours,
        crest: &str,
        spray: &str,
        source_station_count: u32,
    ) -> Result<PreparedBreakerPrograms, String> {
        if plan.document_hash() != self.document {
            return Err("Water breaker terrain revision differs".into());
        }
        let document = plan.document();
        let corrections = self.shore.corrections();
        let edited = !document.height_deltas.is_empty() || corrections.elevation != 0.0;
        let active = edited.then_some(corrections);
        let source_breaker_geometry::PackedContours { stations, ranges } = packed;
        let station_count =
            u32::try_from(stations.len()).map_err(|_| "Breaker station count overflow")?;
        let station_bytes = stations
            .iter()
            .flatten()
            .flat_map(|value| value.to_le_bytes())
            .collect();
        let domain = self.shore.shore_field().domain();
        let crest =
            crate::source_breaker_gpu::prepare_terrain_program(crest, document, domain, active)?;
        Ok(PreparedBreakerPrograms {
            document: self.document,
            surface: self.surface.identity(),
            crest: crate::source_breaker_gpu::prepare_station_guard(
                &crest,
                source_station_count,
                station_count,
            )?,
            spray: crate::source_breaker_gpu::prepare_terrain_program(
                spray, document, domain, active,
            )?,
            stations,
            station_bytes,
            ranges,
        })
    }

    /// Bind derived programs to exactly the terrain and surface that prepared them.
    pub fn breaker_input(
        &self,
        programs: &PreparedBreakerPrograms,
    ) -> Result<PreparedBreakerInput, String> {
        if programs.document != self.document || programs.surface != self.surface.identity() {
            return Err("Breaker inputs belong to another water generation".into());
        }
        let input = PreparedBreakerInput {
            programs: Arc::new(programs.clone()),
            shore: self.shore.clone(),
            surface_params: self
                .surface
                .file("uniform-waterSurface.bin")
                .ok_or("Water surface parameters absent")?
                .try_into()
                .map_err(|_| "Water surface parameter size differs")?,
            frame: self
                .surface
                .file("water-frame.bin")
                .ok_or("Water frame absent")?
                .try_into()
                .map_err(|_| "Water frame size differs")?,
            window: None,
        };
        input.gpu_inputs().validate()?;
        Ok(input)
    }

    /// Install the existing renderer components; final GPU readiness is assessed separately.
    pub fn install(self, app: &mut App) -> Result<(), String> {
        if app.world().contains_resource::<Status>()
            || app.world().contains_resource::<source_ocean::Targets>()
            || app.world().contains_resource::<source_shore_sim::Targets>()
            || !app
                .world()
                .contains_resource::<source_environment::host::Status>()
            || app
                .world()
                .get_resource::<source_terrain::host::Status>()
                .is_none_or(|status| status.document != Some(self.document))
        {
            return Err(
                "Water host requires its exclusive admitted environment and terrain".into(),
            );
        }
        source_ocean::register(app)?;
        source_ocean_geometry::register(app)?;
        source_shore_sim::register(app)?;
        source_water_optics::register(app)?;
        self.ocean.stage(app.world_mut())?;
        self.shore.stage(app.world_mut())?;
        source_water_optics::register_underwater(app, &self.surface)?;
        self.optics.prime_programs(app.world_mut())?;
        app.insert_resource(SurfaceIdentity(self.surface.identity()))
            .insert_resource(Status {
                document: Some(self.document),
                ..default()
            })
            .insert_resource(Pending {
                geometry: self.geometry,
                optics: self.optics,
            })
            .init_resource::<SelectionCadence>()
            .add_systems(
                PostUpdate,
                update
                    .in_set(WaterHostSet)
                    .after(source_environment::host::EnvironmentHostSet)
                    .after(source_terrain::host::TerrainHostSet)
                    .after(bevy::transform::TransformSystems::Propagate)
                    .before(bevy::asset::AssetEventSystems)
                    .before(bevy::camera::visibility::VisibilitySystems::VisibilityPropagate),
            );
        Ok(())
    }
}

fn update(world: &mut World) {
    note_geometry_changes(world);
    if world.resource::<Status>().error.is_some() {
        return;
    }
    crate::source_breaker_resident::observe_follow(world);
    if let Err(error) = update_inner(world) {
        error!(%error, "Admitted water projection failed");
        world.resource_mut::<Status>().error = Some(error);
        #[cfg(feature = "physics-rapier")]
        if let Some(mut ready) = world.get_resource_mut::<crate::PlayerSimulationReady>() {
            ready.0 = false;
        }
        world.write_message(bevy::app::AppExit::error());
    }
}

fn update_inner(world: &mut World) -> Result<(), String> {
    if let Some(error) = world
        .get_resource::<crate::source_breaker_resident::Targets>()
        .and_then(crate::source_breaker_resident::Targets::error)
    {
        return Err(error);
    }
    if let Some(error) = world.resource::<source_shore_sim::Targets>().error() {
        return Err(error);
    }
    let expected = world
        .resource::<Status>()
        .document
        .ok_or("Water document absent")?;
    let terrain = world.resource::<source_terrain::host::Status>();
    if terrain.document != Some(expected) || terrain.error.is_some() {
        return Err("Water terrain revision changed or failed".into());
    }
    let environment = world.resource::<source_environment::host::Status>();
    if environment.error.is_some() {
        return Err("Water environment failed".into());
    }
    if !environment.ready
        || terrain.pending
        || !terrain.normals_ready
        || !world
            .resource::<source_ocean::Targets>()
            .ready
            .load(Ordering::Acquire)
        || !world
            .resource::<source_shore_sim::Targets>()
            .ready
            .load(Ordering::Acquire)
    {
        return Ok(());
    }
    let (camera, _) = scene_camera::primary(world)?;
    if world
        .get::<Camera>(camera)
        .and_then(Camera::physical_target_size)
        .is_none_or(|size| size.x == 0 || size.y == 0)
    {
        return Ok(());
    }
    let transform = *world
        .get::<Transform>(camera)
        .ok_or("Water camera pose absent")?;
    let Some(Projection::Perspective(projection)) = world.get::<Projection>(camera) else {
        return Err("Water requires a perspective camera".into());
    };
    let projection = projection.clone();
    if let Some(effects) = world.get_resource::<crate::source_breaker_resident::Targets>() {
        let clip = world
            .get::<Camera>(camera)
            .ok_or("Water camera absent")?
            .clip_from_view();
        let pose = *world
            .get::<GlobalTransform>(camera)
            .ok_or("Water camera global pose absent")?;
        effects.update_camera(clip, pose).map_err(str::to_owned)?;
        effects.synchronize_ready();
    }
    let current_view = scene_camera::source_view(&transform, &projection)?;
    let cadence = world.resource::<SelectionCadence>();
    let refresh = cadence
        .camera
        .needs_refresh(camera, &transform, &projection)
        || cadence.view.is_none();
    let view = if refresh {
        current_view
    } else {
        cadence.view.expect("a retained view exists")
    };
    select_underwater_camera(world, camera)?;
    source_water_optics::update(world, camera)?;
    let selected = stage_and_update_geometry(world, view, refresh)?;
    crate::source_breaker_resident::update_display(world)?;
    let camera_position = world
        .get::<GlobalTransform>(camera)
        .ok_or("Water camera global pose absent")?
        .translation();
    crate::source_breaker_resident::update_follow(
        world,
        [f64::from(camera_position.x), f64::from(camera_position.z)],
    )?;
    if refresh || selected {
        let mut cadence = world.resource_mut::<SelectionCadence>();
        if selected {
            cadence.geometry_dirty = false;
        }
        if refresh {
            cadence.camera.record(camera, &transform, &projection);
            cadence.view = Some(view);
        }
    }
    Ok(())
}

fn stage_and_update_geometry(
    world: &mut World,
    view: forge_world::terrain_lod::CdlodView,
    refresh: bool,
) -> Result<bool, String> {
    let staged = if let Some(pending) = world.remove_resource::<Pending>() {
        pending.geometry.stage(world, view)?;
        pending.optics.stage(world)?;
        world.resource_mut::<Status>().staged = true;
        true
    } else {
        false
    };
    update_geometry_if_needed(
        world,
        view,
        refresh,
        staged,
        source_ocean_geometry::update_view,
    )
}

// Remember changes before any readiness/error return. A one-frame Bevy change
// tick alone would otherwise be lost while this host is waiting to resume.
fn note_geometry_changes(world: &mut World) {
    if !world.contains_resource::<source_ocean_geometry::ResidentGeometry>()
        || world.is_resource_changed::<source_ocean_geometry::ResidentGeometry>()
    {
        world.resource_mut::<SelectionCadence>().geometry_dirty = true;
    }
}

// The retained view has already passed source selection. Re-select it only
// when the camera cadence, staging, or another system changes its resident.
fn update_geometry_if_needed(
    world: &mut World,
    view: forge_world::terrain_lod::CdlodView,
    refresh: bool,
    staged: bool,
    select: impl FnOnce(&mut World, forge_world::terrain_lod::CdlodView) -> Result<usize, String>,
) -> Result<bool, String> {
    if refresh
        || staged
        || world.resource::<SelectionCadence>().geometry_dirty
        || !world.contains_resource::<source_ocean_geometry::ResidentGeometry>()
        || world.is_resource_changed::<source_ocean_geometry::ResidentGeometry>()
    {
        // Retain the obligation even if selection or a later host update fails.
        world.resource_mut::<SelectionCadence>().geometry_dirty = true;
        select(world, view)?;
        return Ok(true);
    }
    Ok(false)
}

#[cfg(test)]
mod underwater_camera_tests {
    use super::*;
    use bevy::render::render_resource::TextureUsages;

    #[test]
    fn scene_water_level_is_read_from_the_admitted_frame_and_must_be_finite() {
        let mut frame = [0_u8; 720];
        frame[588..592].copy_from_slice(&3.5_f32.to_le_bytes());
        assert_eq!(
            water_level_from_frame(&frame).unwrap().to_bits(),
            3.5_f64.to_bits()
        );
        frame[588..592].copy_from_slice(&0.0_f32.to_le_bytes());
        assert_eq!(
            water_level_from_frame(&frame).unwrap().to_bits(),
            0.0_f64.to_bits()
        );
        assert!(water_level_from_frame(&frame[..591]).is_err());
        frame[588..592].copy_from_slice(&f32::NAN.to_le_bytes());
        assert!(water_level_from_frame(&frame).is_err());
    }

    #[test]
    fn emission_coverage_requires_the_full_disk_and_finite_camera() {
        let window = source_breaker_contours::ContourConfig {
            min: [-304.0, -304.0],
            max: [304.0, 304.0],
            cells: [608, 608],
            water_level: 0.0,
            station_spacing: 0.6,
        };
        assert!(window_covers_emission(window, [0.0, 0.0]));
        assert!(window_covers_emission(window, [64.0, -64.0]));
        assert!(!window_covers_emission(window, [64.001, 0.0]));
        assert!(!window_covers_emission(window, [f64::NAN, 0.0]));
    }

    #[test]
    fn complex_margin_falls_back_without_hiding_other_errors() {
        let expanded = source_breaker_contours::ContourConfig {
            min: [-304.0, -304.0],
            max: [304.0, 304.0],
            cells: [608, 608],
            water_level: 0.0,
            station_spacing: 0.6,
        };
        let original = source_breaker_contours::ContourConfig {
            min: [-240.0, -240.0],
            max: [240.0, 240.0],
            cells: [480, 480],
            ..expanded
        };
        let sample = |config| {
            let contours = source_breaker_contours::shore_contours(config, |x, z| {
                if x > 250.0 { (z * 1.2).sin() } else { -1.0 }
            })?;
            source_breaker_geometry::pack_contours(&contours)
        };
        assert!(matches!(
            sample(expanded),
            Err("Shoreline exceeds station limit")
        ));
        let (chosen, packed) = select_resident_window(expanded, original, sample).unwrap();
        assert!(packed.stations.is_empty());
        assert!(window_covers_emission(chosen, [0.0, 0.0]));
        assert!(!window_covers_emission(chosen, [0.01, 0.0]));
        assert!(matches!(
            select_resident_window(expanded, original, |_| Err("Non-finite shoreline height")),
            Err(error) if error == "Non-finite shoreline height"
        ));
    }

    #[test]
    fn selects_primary_camera_with_required_depth_and_moves_only_owned_marker() {
        let mut world = World::new();
        let first = world.spawn(Camera3d::default()).id();
        let second = world.spawn(Camera3d::default()).id();
        select_underwater_camera(&mut world, first).unwrap();
        assert!(
            world
                .get::<crate::source_underwater::SourceUnderwaterCamera>(first)
                .is_some()
        );
        let usage: TextureUsages = world
            .get::<Camera3d>(first)
            .unwrap()
            .depth_texture_usages
            .into();
        assert!(usage.contains(
            TextureUsages::TEXTURE_BINDING
                | TextureUsages::COPY_SRC
                | TextureUsages::RENDER_ATTACHMENT
        ));
        select_underwater_camera(&mut world, second).unwrap();
        assert!(
            world
                .get::<crate::source_underwater::SourceUnderwaterCamera>(first)
                .is_none()
        );
        assert!(
            world
                .get::<crate::source_underwater::SourceUnderwaterCamera>(second)
                .is_some()
        );
    }

    #[test]
    fn refuses_foreign_underwater_and_offscreen_cameras() {
        let mut world = World::new();
        let foreign = world
            .spawn((
                Camera3d::default(),
                crate::source_underwater::SourceUnderwaterCamera,
            ))
            .id();
        let main = world.spawn(Camera3d::default()).id();
        let offscreen = world
            .spawn((Camera3d::default(), scene_camera::OffscreenSceneCamera))
            .id();
        assert!(select_underwater_camera(&mut world, main).is_err());
        assert!(
            world
                .get::<crate::source_underwater::SourceUnderwaterCamera>(foreign)
                .is_some()
        );
        assert!(select_underwater_camera(&mut world, offscreen).is_err());
    }
}

#[cfg(test)]
mod selection_cadence_tests {
    use super::*;

    #[derive(Clone, Copy, PartialEq, Eq)]
    enum SelectionFailure {
        DuringSelection,
        AfterSelection,
    }

    #[derive(Resource)]
    struct SelectionProbe {
        camera: Entity,
        transform: Transform,
        projection: PerspectiveProjection,
        ready: bool,
        staged: bool,
        failure: Option<SelectionFailure>,
        external: bool,
        external_tiles: Vec<Entity>,
        calls: usize,
        error: Option<String>,
    }

    // Exercise the helper under Bevy's actual exclusive-system change ticks,
    // while observing real source-ocean selection without a render device.
    fn observe_selection(world: &mut World) {
        note_geometry_changes(world);
        if !world.resource::<SelectionProbe>().ready {
            return;
        }
        let probe = world.resource::<SelectionProbe>();
        let (camera, transform, projection, staged, failure) = (
            probe.camera,
            probe.transform,
            probe.projection.clone(),
            probe.staged,
            probe.failure,
        );
        let result = (|| -> Result<(), String> {
            let current_view = scene_camera::source_view(&transform, &projection)?;
            let cadence = world.resource::<SelectionCadence>();
            let refresh = cadence
                .camera
                .needs_refresh(camera, &transform, &projection)
                || cadence.view.is_none();
            let view = if refresh {
                current_view
            } else {
                cadence.view.expect("retained test view")
            };
            let selected =
                update_geometry_if_needed(world, view, refresh, staged, |world, view| {
                    world.resource_mut::<SelectionProbe>().calls += 1;
                    if failure == Some(SelectionFailure::DuringSelection) {
                        return Err("test selection failed".into());
                    }
                    source_ocean_geometry::update_view(world, view)
                })?;
            if failure == Some(SelectionFailure::AfterSelection) {
                return Err("test later host update failed".into());
            }
            if refresh || selected {
                let mut cadence = world.resource_mut::<SelectionCadence>();
                if selected {
                    cadence.geometry_dirty = false;
                }
                if refresh {
                    cadence.camera.record(camera, &transform, &projection);
                    cadence.view = Some(view);
                }
            }
            Ok(())
        })();
        world.resource_mut::<SelectionProbe>().error = result.err();
    }

    // A second exclusive system represents a distinct consumer of the shared
    // geometry after the water host has already finished its current frame.
    fn select_external_view(world: &mut World) {
        if !world.resource::<SelectionProbe>().external {
            return;
        }
        let external = scene_camera::source_view(
            &Transform::from_xyz(600.0, 8.0, 0.0),
            &PerspectiveProjection::default(),
        )
        .unwrap();
        assert!(
            source_ocean_geometry::update_view_observed(world, external)
                .unwrap()
                .changed
        );
        let tiles = source_ocean_geometry::tile_entities(world);
        let mut probe = world.resource_mut::<SelectionProbe>();
        probe.external = false;
        probe.external_tiles = tiles;
    }

    fn fixture() -> (App, Entity) {
        let mut app = App::new();
        app.init_resource::<Assets<Mesh>>();
        let mesh = app
            .world_mut()
            .resource_mut::<Assets<Mesh>>()
            .add(Mesh::from(Cuboid::default()));
        let root = app
            .world_mut()
            .spawn((
                Mesh3d(mesh),
                MeshMaterial3d(Handle::<source_water_optics::OpticalMaterial>::default()),
                Transform::default(),
                Visibility::Inherited,
            ))
            .id();
        source_ocean_geometry::adopt_optical_surface(app.world_mut(), root).unwrap();
        let camera = app.world_mut().spawn_empty().id();
        app.insert_resource(SelectionProbe {
            camera,
            transform: Transform::from_xyz(0.0, 8.0, 0.0),
            projection: PerspectiveProjection::default(),
            ready: true,
            staged: false,
            failure: None,
            external: false,
            external_tiles: Vec::new(),
            calls: 0,
            error: None,
        })
        .init_resource::<SelectionCadence>()
        .add_systems(Update, (observe_selection, select_external_view).chain());
        (app, root)
    }

    #[test]
    fn water_selection_uses_cadence_and_detects_other_resident_changes() {
        let (mut app, root) = fixture();
        app.update();
        assert_eq!(app.world().resource::<SelectionProbe>().calls, 1);
        app.update();
        assert_eq!(app.world().resource::<SelectionProbe>().calls, 1);

        app.world_mut()
            .resource_mut::<SelectionProbe>()
            .transform
            .translation
            .x = 0.5;
        app.update();
        assert_eq!(app.world().resource::<SelectionProbe>().calls, 1);
        app.world_mut().resource_mut::<SelectionProbe>().staged = true;
        app.update();
        assert_eq!(app.world().resource::<SelectionProbe>().calls, 2);
        app.world_mut().resource_mut::<SelectionProbe>().staged = false;

        app.world_mut()
            .resource_mut::<SelectionProbe>()
            .transform
            .translation
            .x = 2.0;
        app.update();
        assert_eq!(app.world().resource::<SelectionProbe>().calls, 3);
        app.world_mut()
            .resource_mut::<SelectionProbe>()
            .projection
            .fov = 0.8;
        app.update();
        assert_eq!(app.world().resource::<SelectionProbe>().calls, 4);
        let other_camera = app.world_mut().spawn_empty().id();
        app.world_mut().resource_mut::<SelectionProbe>().camera = other_camera;
        app.update();
        assert_eq!(app.world().resource::<SelectionProbe>().calls, 5);

        {
            let mut probe = app.world_mut().resource_mut::<SelectionProbe>();
            probe.ready = false;
            probe.external = true;
        }
        app.update();
        assert_eq!(app.world().resource::<SelectionProbe>().calls, 5);
        let external_tiles = app
            .world()
            .resource::<SelectionProbe>()
            .external_tiles
            .clone();
        app.update();
        app.update();
        assert_eq!(app.world().resource::<SelectionProbe>().calls, 5);
        assert!(app.world().resource::<SelectionCadence>().geometry_dirty);
        app.world_mut().resource_mut::<SelectionProbe>().ready = true;
        app.update();
        assert_eq!(app.world().resource::<SelectionProbe>().calls, 6);
        assert_ne!(
            source_ocean_geometry::tile_entities(app.world()),
            external_tiles
        );
        app.update();
        assert_eq!(app.world().resource::<SelectionProbe>().calls, 6);

        app.world_mut()
            .remove_resource::<source_ocean_geometry::ResidentGeometry>();
        app.update();
        let probe = app.world().resource::<SelectionProbe>();
        assert_eq!(probe.calls, 7);
        assert_eq!(probe.error.as_deref(), Some("Ocean geometry not staged"));
        source_ocean_geometry::adopt_optical_surface(app.world_mut(), root).unwrap();
        app.update();
        let probe = app.world().resource::<SelectionProbe>();
        assert_eq!(probe.calls, 8);
        assert!(probe.error.is_none());
    }

    #[test]
    fn water_selection_retries_failed_refresh_without_recording_it() {
        let (mut app, _) = fixture();
        app.update();
        {
            let mut probe = app.world_mut().resource_mut::<SelectionProbe>();
            probe.transform.translation.x = 3.0;
            probe.failure = Some(SelectionFailure::DuringSelection);
        }
        app.update();
        let probe = app.world().resource::<SelectionProbe>();
        assert_eq!(probe.calls, 2);
        assert_eq!(probe.error.as_deref(), Some("test selection failed"));
        app.world_mut().resource_mut::<SelectionProbe>().failure = None;
        app.update();
        let probe = app.world().resource::<SelectionProbe>();
        assert_eq!(probe.calls, 3);
        assert!(probe.error.is_none());
        app.update();
        assert_eq!(app.world().resource::<SelectionProbe>().calls, 3);

        app.world_mut()
            .resource_mut::<SelectionProbe>()
            .transform
            .translation
            .x = f32::NAN;
        app.update();
        let probe = app.world().resource::<SelectionProbe>();
        assert_eq!(probe.calls, 3);
        assert!(probe.error.is_some());
        app.world_mut()
            .resource_mut::<SelectionProbe>()
            .transform
            .translation
            .x = 3.0;
        app.update();
        assert!(app.world().resource::<SelectionProbe>().error.is_none());
    }

    #[test]
    fn water_selection_keeps_invalidation_after_later_host_failure() {
        let (mut app, _) = fixture();
        app.update();
        {
            let mut probe = app.world_mut().resource_mut::<SelectionProbe>();
            probe.staged = true;
            probe.failure = Some(SelectionFailure::AfterSelection);
        }
        app.update();
        assert!(app.world().resource::<SelectionCadence>().geometry_dirty);
        assert_eq!(app.world().resource::<SelectionProbe>().calls, 2);
        {
            let mut probe = app.world_mut().resource_mut::<SelectionProbe>();
            probe.staged = false;
            probe.failure = None;
        }
        app.update();
        assert_eq!(app.world().resource::<SelectionProbe>().calls, 3);
        assert!(!app.world().resource::<SelectionCadence>().geometry_dirty);
    }
}
