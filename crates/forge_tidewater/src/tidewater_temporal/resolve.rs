// SPDX-License-Identifier: MIT
// Derived from Tidewater TemporalUpscale.js / PostFX.js at
// 1438b1abfcaee3267092b75573014f4d9b4a983c.
// Copyright 2026 DRG Software Solutions LLC; see third_party/tidewater/LICENSE.
//! Explicit, scale-one source temporal resolve on Bevy's existing renderer.
//!
//! Histories and their camera metadata belong to encoded resolves, not auxiliary
//! acknowledgements. This first native candidate requires non-pipelined rendering.
//! Encoding observations are not GPU completion, readback or source visual parity.
//! The adapter must route actual HDR GTAO/haze/underwater through `SourceHdrSystems`;
//! the historical Forge post-tonemapping ordering is not source-equivalent.

pub use super::hdr::SourceHdrSystems;
use super::{
    frame::{EncodingOwner, FrameTag, Frames, Packet, ViewPacket},
    hdr::HdrObservations,
    render::AuxObservations,
    targets::AuxTargets,
};
use bevy::{
    anti_alias::taa::TemporalAntiAliasing,
    camera::{CameraMainTextureUsages, Hdr},
    core_pipeline::schedule::{Core3d, Core3dSystems},
    prelude::*,
    render::{
        Render, RenderApp, RenderStartup, RenderSystems,
        camera::ExtractedCamera,
        extract_component::{ExtractComponent, ExtractComponentPlugin},
        render_resource::{
            BindGroup, BindGroupEntries, BindGroupLayoutDescriptor, BindGroupLayoutEntries,
            CachedRenderPipelineId, ColorTargetState, ColorWrites, Extent3d, FilterMode,
            FragmentState, LoadOp, Operations, PipelineCache, RenderPassColorAttachment,
            RenderPassDepthStencilAttachment, RenderPassDescriptor, RenderPipelineDescriptor,
            Sampler, SamplerBindingType, SamplerDescriptor, ShaderStages, ShaderType, StoreOp,
            Texture, TextureAspect, TextureDescriptor, TextureDimension, TextureFormat,
            TextureSampleType, TextureUsages, TextureView, UniformBuffer, VertexState,
            binding_types::{sampler, texture_2d, texture_depth_2d, uniform_buffer},
        },
        renderer::{RenderContext, RenderDevice, RenderQueue, ViewQuery},
        sync_world::MainEntity,
        texture::{CachedTexture, TextureCache},
        view::{ExtractedView, Msaa, ViewTarget},
    },
    shader::Shader,
};
use std::{
    collections::{HashMap, HashSet, VecDeque},
    fmt,
    sync::{Arc, Mutex},
};

/// Presence is explicit opt-in, only on the selected primary camera.
#[derive(Component, Clone, Copy, Debug, ExtractComponent)]
#[extract_component_filter((Without<crate::tidewater_refraction::CaptureCamera>, Without<TemporalAntiAliasing>))]
pub struct ResolveEnabled;

#[derive(Resource)]
struct Installed;

#[derive(Resource, Clone)]
pub(super) struct Shaders {
    resolve: Handle<Shader>,
    seed: Handle<Shader>,
}

/// Source controls remain fixed at their pinned scale-one defaults.
/// Previous far is encoded-camera metadata, not a graphics slider.
#[derive(Clone, Copy, Debug, ShaderType)]
pub struct Params {
    pub frame_weight: f32,
    pub depth_threshold: f32,
    pub edge_depth_diff: f32,
    pub max_velocity_length: f32,
    pub has_water_mask: f32,
    pub previous_source_far: f32,
    padding: Vec2,
}

impl Params {
    #[must_use]
    pub const fn source_scale_one(previous_source_far: f32) -> Self {
        Self {
            frame_weight: 0.06,
            depth_threshold: 0.0005,
            edge_depth_diff: 0.001,
            max_velocity_length: 128.0,
            has_water_mask: 1.0,
            previous_source_far,
            padding: Vec2::ZERO,
        }
    }
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub enum Error {
    NotRegistered,
    NotSelectedView,
    NotResolveOwner,
    CaptureView,
    ExistingBevyTaa,
    MissingHdr,
    IncompleteHdrChain,
    UnsupportedCamera,
    UnsupportedSize,
    UnsupportedMsaa,
    MissingAuxiliary,
    IncompleteAuxiliary,
    WrongTagOrGeneration,
    PipelinePending,
    ConsumerPending,
    MissingBinding,
    InvalidTexture,
    AliasedTexture,
    PreviousCameraMismatch,
    StaleAcknowledgement,
    IncompleteEncoding,
    CounterExhausted,
    StatePoisoned,
    RenderOwnership(super::render_owner::Error),
}

impl fmt::Display for Error {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(formatter, "Tidewater source temporal resolve: {self:?}")
    }
}
impl std::error::Error for Error {}

/// Complete source command encoding. This is distinct from auxiliary encoding.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct AckResolve {
    pub render_owned: bool,
    pub tag: FrameTag,
    pub history_epoch: u64,
    pub generation: u64,
    pub auxiliary_generation: u64,
    pub seed_draws: u32,
    pub resolve_draws: u32,
    pub draws: u32,
    pub previous_depth_copied: bool,
    pub output_copied: bool,
}

impl AckResolve {
    #[must_use]
    pub fn complete(self) -> bool {
        self.generation != 0
            && self.auxiliary_generation != 0
            && self.resolve_draws == 1
            && self.seed_draws <= 1
            && self.draws == self.resolve_draws + self.seed_draws
            && self.previous_depth_copied
            && self.output_copied
    }
}

#[derive(Default)]
struct AckState {
    queue: VecDeque<AckResolve>,
    latest: HashMap<Entity, u64>,
}

impl AckState {
    fn validate(&self, ack: AckResolve) -> Result<(), Error> {
        if !ack.complete() {
            return Err(Error::IncompleteEncoding);
        }
        if self
            .latest
            .get(&ack.tag.view)
            .is_some_and(|serial| *serial >= ack.tag.serial)
        {
            return Err(Error::StaleAcknowledgement);
        }
        Ok(())
    }

    fn push(&mut self, ack: AckResolve) -> Result<(), Error> {
        self.validate(ack)?;
        self.latest.insert(ack.tag.view, ack.tag.serial);
        self.queue.push_back(ack);
        Ok(())
    }
}

/// Shared main/render handle; root drains it before the next frame preparation.
/// Draining never advances Halton: the root frame owner must validate the tag.
#[derive(Resource, Clone, Default)]
pub struct ResolveAcknowledgements(Arc<Mutex<AckState>>);

impl ResolveAcknowledgements {
    pub fn drain(&self) -> Result<Vec<AckResolve>, Error> {
        let mut state = self.0.lock().map_err(|_| Error::StatePoisoned)?;
        Ok(state.queue.drain(..).collect())
    }
}

#[derive(Clone, Debug)]
#[allow(
    clippy::struct_excessive_bools,
    reason = "These independent resolve receipt flags describe separate GPU steps"
)]
pub struct ResolveObservation {
    pub tag: FrameTag,
    pub history_epoch: u64,
    pub generation: u64,
    pub auxiliary_generation: u64,
    pub input_size: UVec2,
    pub output_size: UVec2,
    /// Immutable values uploaded for this tag; observations do not prove GPU reads.
    pub bound_packet: Option<Packet>,
    pub params: Option<Params>,
    pub seed_draws: u32,
    pub resolve_draws: u32,
    pub previous_depth_cleared: bool,
    pub previous_depth_copied: bool,
    pub output_copied: bool,
    pub resolve_acknowledged: bool,
    pub frames_encoded: u64,
    pub history_input_id: String,
    pub history_output_id: String,
    pub previous_depth_id: String,
    pub error: Option<Error>,
}

impl ResolveObservation {
    #[must_use]
    pub fn complete(&self) -> bool {
        self.resolve_draws == 1
            && self.previous_depth_copied
            && self.output_copied
            && self.resolve_acknowledged
            && self.error.is_none()
    }
}

#[derive(Resource, Clone, Default)]
pub struct ResolveObservations(Arc<Mutex<HashMap<Entity, ResolveObservation>>>);

impl ResolveObservations {
    #[must_use]
    pub fn snapshot(&self, view: Entity) -> Option<ResolveObservation> {
        self.0.lock().ok()?.get(&view).cloned()
    }

    fn record(&self, observation: ResolveObservation) {
        if let Ok(mut state) = self.0.lock() {
            state.insert(observation.tag.view, observation);
        }
    }
}

#[derive(Clone, Debug, Default)]
struct History {
    current: usize,
    last_tag: Option<FrameTag>,
    last_packet: Option<Packet>,
    last_far: Option<f32>,
    frames_encoded: u64,
    needs_seed: bool,
}

fn new_allocation_history(previous: Option<History>) -> History {
    let mut history = previous.unwrap_or_default();
    history.current = 0;
    history.last_tag = None;
    history.needs_seed = true;
    history
}

/// Actual persistent resources, accessible to a later independent GPU readback.
/// Allocation or texture handles do not imply encoded or GPU-ready contents.
#[derive(Component, Clone)]
pub struct ResolveTargets {
    pub view: Entity,
    pub input_size: UVec2,
    pub output_size: UVec2,
    pub history_epoch: u64,
    pub generation: u64,
    pub auxiliary_generation: u64,
    pub histories: [CachedTexture; 2],
    pub previous_source_depth: CachedTexture,
    history: Arc<Mutex<History>>,
}

impl ResolveTargets {
    /// Refuse to return an old frame as this tag's result.
    pub fn output_for(&self, tag: FrameTag) -> Result<&CachedTexture, Error> {
        let state = self.history.lock().map_err(|_| Error::StatePoisoned)?;
        if state.last_tag != Some(tag) {
            return Err(Error::WrongTagOrGeneration);
        }
        Ok(&self.histories[state.current])
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
struct Spec {
    size: UVec2,
    epoch: u64,
    auxiliary_generation: u64,
}

impl Spec {
    fn from_packet(packet: &ViewPacket, targets: &AuxTargets) -> Result<Self, Error> {
        if packet.frame.input_size != packet.frame.output_size
            || packet.frame.input_size.min_element() == 0
            || packet.frame.input_size.max_element() > (1 << 24)
        {
            return Err(Error::UnsupportedSize);
        }
        if targets.view != packet.tag.view
            || targets.input_size != packet.frame.input_size
            || targets.output_size != packet.frame.output_size
            || targets.history_epoch != packet.frame.history_epoch
            || targets.generation == 0
        {
            return Err(Error::WrongTagOrGeneration);
        }
        Ok(Self {
            size: packet.frame.input_size,
            epoch: packet.frame.history_epoch,
            auxiliary_generation: targets.generation,
        })
    }
}

struct Allocation {
    spec: Spec,
    targets: ResolveTargets,
    packet: UniformBuffer<Packet>,
    params: UniformBuffer<Params>,
    // Independent private cache; global frame cache resets must not lend histories.
    _cache: TextureCache,
}

#[derive(Resource, Default)]
pub(super) struct Allocator {
    views: HashMap<Entity, Allocation>,
    next_generation: u64,
}

impl Allocator {
    pub(super) fn retire_all(&mut self) {
        self.views.clear();
    }
}

#[derive(Component)]
pub struct Prepared {
    tag: FrameTag,
    generation: u64,
    uniforms: BindGroup,
    packet: Packet,
    params: Params,
}

#[derive(Component, Clone, Debug)]
pub struct PreparationFailure(Error);

#[derive(Resource)]
pub struct Pipeline {
    inputs: BindGroupLayoutDescriptor,
    uniforms: BindGroupLayoutDescriptor,
    seed_inputs: BindGroupLayoutDescriptor,
    sampler: Sampler,
    resolve: CachedRenderPipelineId,
    seed: CachedRenderPipelineId,
}

impl Pipeline {
    // Instrumentation reuses the exact group descriptors and actual bind groups.
    pub(super) fn trace_layouts(&self) -> [BindGroupLayoutDescriptor; 2] {
        [self.inputs.clone(), self.uniforms.clone()]
    }
}

/// Install inactive resolve resources. The controller explicitly activates a view.
/// This does not modify existing HDR post effects or claim their equivalence.
pub fn register(app: &mut App) -> Result<(), String> {
    if app.world().contains_resource::<Installed>() {
        return Ok(());
    }
    super::register(app)?;
    let mut assets = app.world_mut().resource_mut::<Assets<Shader>>();
    let shaders = Shaders {
        resolve: assets.add(Shader::from_wgsl(
            include_str!("resolve.wgsl"),
            "forge://tidewater/temporal/resolve.wgsl",
        )),
        seed: assets.add(Shader::from_wgsl(
            include_str!("seed.wgsl"),
            "forge://tidewater/temporal/seed.wgsl",
        )),
    };
    let acknowledgements = ResolveAcknowledgements::default();
    let observations = ResolveObservations::default();
    let hdr_observations = HdrObservations::default();
    app.insert_resource(Installed)
        .insert_resource(shaders.clone())
        .insert_resource(acknowledgements.clone())
        .insert_resource(observations.clone())
        .insert_resource(hdr_observations.clone())
        .add_plugins(ExtractComponentPlugin::<ResolveEnabled>::default());
    app.sub_app_mut(RenderApp)
        .insert_resource(shaders)
        .insert_resource(acknowledgements)
        .insert_resource(observations)
        .insert_resource(hdr_observations)
        .init_resource::<Allocator>()
        .add_systems(RenderStartup, initialize)
        .add_systems(
            Render,
            prepare
                .in_set(RenderSystems::PrepareResources)
                .after(super::targets::prepare_targets),
        )
        .configure_sets(
            Core3d,
            (
                SourceHdrSystems::Gtao,
                SourceHdrSystems::Haze,
                SourceHdrSystems::Underwater,
                SourceHdrSystems::Resolve,
            )
                .chain()
                .after(Core3dSystems::MainPass)
                .after(super::render::encode)
                .before(Core3dSystems::EarlyPostProcess),
        )
        .add_systems(Core3d, encode.in_set(SourceHdrSystems::Resolve));
    Ok(())
}

/// Select only the already selected temporal main view. TAA and captures refuse.
/// `COPY_DST` adds exact HDR output publication, without a bilinear presentation pass.
pub fn activate(world: &mut World, view: Option<Entity>) -> Result<(), Error> {
    if !world.contains_resource::<Installed>() {
        return Err(Error::NotRegistered);
    }
    if let Some(view) = view {
        if world
            .get_resource::<Frames>()
            .and_then(Frames::selected_view)
            != Some(view)
        {
            return Err(Error::NotSelectedView);
        }
        if world.resource::<Frames>().owner() != EncodingOwner::Resolve {
            return Err(Error::NotResolveOwner);
        }
        validate_camera(world, view)?;
    }
    let mut query = world.query_filtered::<Entity, With<ResolveEnabled>>();
    let old: Vec<_> = query.iter(world).collect();
    for entity in old {
        world.entity_mut(entity).remove::<ResolveEnabled>();
    }
    if let Some(view) = view {
        let usages = world
            .get::<CameraMainTextureUsages>(view)
            .copied()
            .unwrap_or_default()
            .with(TextureUsages::COPY_DST);
        world.entity_mut(view).insert((ResolveEnabled, usages));
    }
    Ok(())
}

pub(super) fn validate_camera(world: &World, view: Entity) -> Result<(), Error> {
    if world
        .get::<crate::tidewater_refraction::CaptureCamera>(view)
        .is_some()
    {
        return Err(Error::CaptureView);
    }
    if world.get::<TemporalAntiAliasing>(view).is_some() {
        return Err(Error::ExistingBevyTaa);
    }
    if world.get::<Hdr>(view).is_none() {
        return Err(Error::MissingHdr);
    }
    let camera = world.get::<Camera>(view).ok_or(Error::UnsupportedCamera)?;
    let Projection::Perspective(projection) = world
        .get::<Projection>(view)
        .ok_or(Error::UnsupportedCamera)?
    else {
        return Err(Error::UnsupportedCamera);
    };
    if !camera.is_active
        || camera.viewport.is_some()
        || camera.sub_camera_view.is_some()
        || !supported_camera_projection(projection)
    {
        return Err(Error::UnsupportedCamera);
    }
    if world
        .get::<Msaa>(view)
        .is_none_or(|msaa| msaa.samples() != 1)
    {
        return Err(Error::UnsupportedMsaa);
    }
    Ok(())
}

#[allow(
    clippy::float_cmp,
    reason = "Projection clip-plane sentinels are exact Bevy contract values"
)]
fn supported_camera_projection(projection: &PerspectiveProjection) -> bool {
    // Bevy0.19.1 adjust_perspective_matrix_for_clip_plane returns immediately
    // for x=y=0,z=-1; w is deliberately ignored for this standard plane. A
    // struct-update changing near from0.1 to0.06 therefore remains standard.
    projection.fov.is_finite()
        && projection.fov > 0.0
        && projection.fov < core::f32::consts::PI
        && projection.aspect_ratio.is_finite()
        && projection.aspect_ratio > 0.0
        && projection.near.is_finite()
        && projection.near > 0.0
        && projection.far.is_finite()
        && projection.far > projection.near
        && projection.near_clip_plane.is_finite()
        && projection.near_clip_plane.x == 0.0
        && projection.near_clip_plane.y == 0.0
        && projection.near_clip_plane.z == -1.0
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy startup systems receive render resources by value"
)]
pub(super) fn initialize(
    mut commands: Commands,
    shaders: Res<Shaders>,
    device: Res<RenderDevice>,
    cache: Res<PipelineCache>,
) {
    let inputs = BindGroupLayoutDescriptor::new(
        "Tidewater resolve textures",
        &BindGroupLayoutEntries::sequential(
            ShaderStages::FRAGMENT,
            (
                texture_2d(TextureSampleType::Float { filterable: true }),
                texture_depth_2d(),
                texture_depth_2d(),
                texture_2d(TextureSampleType::Float { filterable: true }),
                texture_2d(TextureSampleType::Float { filterable: true }),
                texture_2d(TextureSampleType::Float { filterable: true }),
                sampler(SamplerBindingType::Filtering),
            ),
        ),
    );
    let uniforms = BindGroupLayoutDescriptor::new(
        "Tidewater resolve packet336 and params32",
        &BindGroupLayoutEntries::sequential(
            ShaderStages::FRAGMENT,
            (
                uniform_buffer::<Packet>(false),
                uniform_buffer::<Params>(false),
            ),
        ),
    );
    let seed_inputs = BindGroupLayoutDescriptor::new(
        "Tidewater source bilinear seed",
        &BindGroupLayoutEntries::with_indices(
            ShaderStages::FRAGMENT,
            (
                (0, texture_2d(TextureSampleType::Float { filterable: true })),
                (6, sampler(SamplerBindingType::Filtering)),
            ),
        ),
    );
    let descriptor =
        |shader: Handle<Shader>, layout: Vec<BindGroupLayoutDescriptor>, label: &str| {
            RenderPipelineDescriptor {
                label: Some(label.to_owned().into()),
                layout,
                vertex: VertexState {
                    shader: shader.clone(),
                    entry_point: Some("vs".into()),
                    ..default()
                },
                fragment: Some(FragmentState {
                    shader,
                    entry_point: Some("fs".into()),
                    targets: vec![Some(ColorTargetState {
                        format: TextureFormat::Rgba16Float,
                        blend: None,
                        write_mask: ColorWrites::ALL,
                    })],
                    ..default()
                }),
                ..default()
            }
        };
    let resolve = cache.queue_render_pipeline(descriptor(
        shaders.resolve.clone(),
        vec![inputs.clone(), uniforms.clone()],
        "Tidewater source TAAU resolve",
    ));
    let seed = cache.queue_render_pipeline(descriptor(
        shaders.seed.clone(),
        vec![seed_inputs.clone()],
        "Tidewater source TAAU seed",
    ));
    commands.insert_resource(Pipeline {
        inputs,
        uniforms,
        seed_inputs,
        sampler: device.create_sampler(&SamplerDescriptor {
            mag_filter: FilterMode::Linear,
            min_filter: FilterMode::Linear,
            ..default()
        }),
        resolve,
        seed,
    });
}

fn texture_descriptor(size: UVec2, depth: bool, label: &'static str) -> TextureDescriptor<'static> {
    TextureDescriptor {
        label: Some(label),
        size: Extent3d {
            width: size.x,
            height: size.y,
            depth_or_array_layers: 1,
        },
        mip_level_count: 1,
        sample_count: 1,
        dimension: TextureDimension::D2,
        format: if depth {
            TextureFormat::Depth32Float
        } else {
            TextureFormat::Rgba16Float
        },
        usage: TextureUsages::TEXTURE_BINDING
            | TextureUsages::RENDER_ATTACHMENT
            | TextureUsages::COPY_SRC
            | TextureUsages::COPY_DST,
        view_formats: &[],
    }
}

fn standard_perspective(packet: &ViewPacket) -> bool {
    forge_engine::finite_perspective::standard_reverse_perspective(
        packet.frame.clip_from_view,
        packet.frame.near_far.x,
        packet.frame.near_far.y,
    )
}

#[allow(
    clippy::too_many_arguments,
    clippy::needless_pass_by_value,
    clippy::type_complexity,
    clippy::too_many_lines,
    reason = "This ordered Bevy resource preparation system validates and publishes a complete set of resolve targets atomically"
)]
fn prepare(
    mut commands: Commands,
    device: Res<RenderDevice>,
    queue: Res<RenderQueue>,
    cache: Res<PipelineCache>,
    pipeline: Option<Res<Pipeline>>,
    mut allocator: ResMut<Allocator>,
    owners: Query<&MainEntity, With<ResolveEnabled>>,
    views: Query<
        (
            Entity,
            &ViewPacket,
            &AuxTargets,
            &ExtractedView,
            &ViewTarget,
            Option<&Msaa>,
        ),
        With<ResolveEnabled>,
    >,
    stale: Query<Entity, (With<ResolveTargets>, Without<ResolveEnabled>)>,
) {
    // Missing resources temporarily skip a frame; they do not retire its last
    // source resolve image/camera. Only explicit marker retirement releases it.
    let active: HashSet<_> = owners.iter().map(MainEntity::id).collect();
    allocator.views.retain(|view, _| active.contains(view));
    for entity in &stale {
        commands
            .entity(entity)
            .remove::<(ResolveTargets, Prepared, PreparationFailure)>();
    }
    let Some(pipeline) = pipeline else {
        return;
    };
    for (entity, packet, auxiliary, extracted, target, msaa) in &views {
        let result = (|| -> Result<(ResolveTargets, Prepared), Error> {
            let spec = Spec::from_packet(packet, auxiliary)?;
            if msaa.is_none_or(|msaa| msaa.samples() != 1) {
                return Err(Error::UnsupportedMsaa);
            }
            if !standard_perspective(packet)
                || extracted.viewport.x != 0
                || extracted.viewport.y != 0
                || UVec2::new(extracted.viewport.z, extracted.viewport.w) != spec.size
            {
                return Err(Error::UnsupportedCamera);
            }
            validate_color(
                target.main_texture(),
                spec.size,
                TextureUsages::TEXTURE_BINDING | TextureUsages::COPY_DST,
            )?;
            if allocator
                .views
                .get(&packet.tag.view)
                .is_none_or(|entry| entry.spec != spec)
            {
                let previous = match allocator.views.get(&packet.tag.view) {
                    // Resize preserves camera history; an explicit epoch cut
                    // at unchanged dimensions starts a fresh source instance.
                    Some(old) if old.spec.epoch != spec.epoch && old.spec.size == spec.size => None,
                    Some(old) => Some(
                        old.targets
                            .history
                            .lock()
                            .map_err(|_| Error::StatePoisoned)?
                            .clone(),
                    ),
                    None => None,
                };
                let generation = allocator
                    .next_generation
                    .checked_add(1)
                    .ok_or(Error::CounterExhausted)?;
                let mut texture_cache = TextureCache::default();
                let history = new_allocation_history(previous);
                let targets = ResolveTargets {
                    view: packet.tag.view,
                    input_size: spec.size,
                    output_size: spec.size,
                    history_epoch: spec.epoch,
                    generation,
                    auxiliary_generation: spec.auxiliary_generation,
                    histories: [
                        texture_cache.get(
                            &device,
                            texture_descriptor(spec.size, false, "Tidewater resolve history0"),
                        ),
                        texture_cache.get(
                            &device,
                            texture_descriptor(spec.size, false, "Tidewater resolve history1"),
                        ),
                    ],
                    previous_source_depth: texture_cache.get(
                        &device,
                        texture_descriptor(
                            spec.size,
                            true,
                            "Tidewater source previous depth clear0",
                        ),
                    ),
                    history: Arc::new(Mutex::new(history)),
                };
                allocator.views.insert(
                    packet.tag.view,
                    Allocation {
                        spec,
                        targets,
                        packet: UniformBuffer::from(packet.packet.clone()),
                        params: UniformBuffer::from(Params::source_scale_one(
                            packet.frame.near_far.y,
                        )),
                        _cache: texture_cache,
                    },
                );
                allocator.next_generation = generation;
            }
            let allocation = allocator
                .views
                .get_mut(&packet.tag.view)
                .ok_or(Error::MissingBinding)?;
            let history = allocation
                .targets
                .history
                .lock()
                .map_err(|_| Error::StatePoisoned)?;
            let previous_far = history.last_far.unwrap_or(packet.frame.near_far.y);
            validate_previous_camera(&packet.packet, history.last_packet.as_ref())?;
            allocation.packet.set(packet.packet.clone());
            allocation
                .params
                .set(Params::source_scale_one(previous_far));
            allocation.packet.write_buffer(&device, &queue);
            allocation.params.write_buffer(&device, &queue);
            let uniforms = device.create_bind_group(
                "Tidewater source resolve per-view immutable uniforms",
                &cache.get_bind_group_layout(&pipeline.uniforms),
                &BindGroupEntries::sequential((
                    allocation.packet.binding().ok_or(Error::MissingBinding)?,
                    allocation.params.binding().ok_or(Error::MissingBinding)?,
                )),
            );
            Ok((
                allocation.targets.clone(),
                Prepared {
                    tag: packet.tag,
                    generation: allocation.targets.generation,
                    uniforms,
                    packet: packet.packet.clone(),
                    params: Params::source_scale_one(previous_far),
                },
            ))
        })();
        match result {
            Ok(value) => {
                commands
                    .entity(entity)
                    .insert(value)
                    .remove::<PreparationFailure>();
            }
            Err(error) => {
                commands
                    .entity(entity)
                    .remove::<(ResolveTargets, Prepared)>()
                    .insert(PreparationFailure(error));
            }
        }
    }
}

fn validate_previous_camera(packet: &Packet, previous: Option<&Packet>) -> Result<(), Error> {
    if previous.is_some_and(|previous| {
        packet.previous_unjittered_clip_from_world != previous.current_unjittered_clip_from_world
            || packet.previous_world_from_jittered_clip != previous.current_world_from_jittered_clip
            || packet.previous_camera_position != previous.camera_position
    }) {
        return Err(Error::PreviousCameraMismatch);
    }
    Ok(())
}

fn validate_color(texture: &Texture, size: UVec2, usage: TextureUsages) -> Result<(), Error> {
    if texture.format() != TextureFormat::Rgba16Float {
        return Err(Error::MissingHdr);
    }
    if texture.width() != size.x
        || texture.height() != size.y
        || texture.depth_or_array_layers() != 1
        || texture.sample_count() != 1
        || !texture.usage().contains(usage)
    {
        return Err(Error::InvalidTexture);
    }
    Ok(())
}

fn validate_depth(texture: &Texture, size: UVec2, usage: TextureUsages) -> Result<(), Error> {
    if texture.format() != TextureFormat::Depth32Float
        || texture.width() != size.x
        || texture.height() != size.y
        || texture.depth_or_array_layers() != 1
        || texture.sample_count() != 1
        || !texture.usage().contains(usage)
    {
        return Err(Error::InvalidTexture);
    }
    Ok(())
}

/// Real fullscreen seed/resolve, followed by source-depth and exact HDR copies.
/// No older history is published when this tag is not fully ready.
#[allow(
    clippy::too_many_arguments,
    clippy::needless_pass_by_value,
    clippy::too_many_lines,
    clippy::type_complexity,
    clippy::similar_names,
    reason = "This ordered Bevy render pass owns source resolve and copy sequencing"
)]
pub fn encode(
    view: ViewQuery<(
        &ResolveEnabled,
        &ViewPacket,
        &AuxTargets,
        Option<&ResolveTargets>,
        Option<&Prepared>,
        Option<&PreparationFailure>,
        &ViewTarget,
        &ExtractedCamera,
        Option<&TemporalAntiAliasing>,
        Option<&super::render_owned::OwnedPacket>,
    )>,
    pipeline: Option<Res<Pipeline>>,
    cache: Res<PipelineCache>,
    owner: Option<Res<EncodingOwner>>,
    auxiliary_observations: Res<AuxObservations>,
    hdr_observations: Res<HdrObservations>,
    observations: Res<ResolveObservations>,
    consumer: Option<Res<super::consumer::ConsumerGate>>,
    acknowledgements: Res<ResolveAcknowledgements>,
    readback: Option<Res<super::resolve_readback::Observation>>,
    mut readback_jobs: Option<ResMut<super::resolve_readback::Jobs>>,
    trace: Option<Res<super::trace_readback::Observation>>,
    trace_jobs: Option<Res<super::trace_readback::Jobs>>,
    trace_pipeline: Option<Res<super::trace_readback::Pipeline>>,
    render_owned_state: (
        Option<Res<super::render_owner::Owner>>,
        Option<Res<super::render_owned::Runtime>>,
        Option<Res<super::render_owned::Observation>>,
    ),
    mut context: RenderContext,
) {
    let (render_owner, render_runtime, render_observation) = render_owned_state;
    let (
        _,
        packet,
        auxiliary,
        targets,
        prepared,
        preparation_failure,
        target,
        camera,
        taa,
        owned_packet,
    ) = view.into_inner();
    let mut observation = ResolveObservation {
        tag: packet.tag,
        history_epoch: packet.frame.history_epoch,
        generation: targets.map_or(0, |targets| targets.generation),
        auxiliary_generation: auxiliary.generation,
        input_size: packet.frame.input_size,
        output_size: packet.frame.output_size,
        bound_packet: prepared.map(|prepared| prepared.packet.clone()),
        params: prepared.map(|prepared| prepared.params),
        seed_draws: 0,
        resolve_draws: 0,
        previous_depth_cleared: false,
        previous_depth_copied: false,
        output_copied: false,
        resolve_acknowledged: false,
        frames_encoded: 0,
        history_input_id: String::new(),
        history_output_id: String::new(),
        previous_depth_id: String::new(),
        error: None,
    };
    let result = (|| -> Result<(), Error> {
        if owner.as_deref() != Some(&EncodingOwner::Resolve) {
            return Err(Error::NotResolveOwner);
        }
        if taa.is_some() {
            return Err(Error::ExistingBevyTaa);
        }
        if !camera.hdr {
            return Err(Error::MissingHdr);
        }
        if !hdr_observations.complete(packet.tag) {
            return Err(Error::IncompleteHdrChain);
        }
        if consumer
            .as_deref()
            .is_some_and(|gate| !gate.ready(packet.tag))
        {
            return Err(Error::ConsumerPending);
        }
        let preparation_error =
            || preparation_failure.map_or(Error::MissingBinding, |failure| failure.0.clone());
        let targets = targets.ok_or_else(preparation_error)?;
        let prepared = prepared.ok_or_else(preparation_error)?;
        let pipeline = pipeline.ok_or(Error::PipelinePending)?;
        let resolve_pipeline = cache
            .get_render_pipeline(pipeline.resolve)
            .ok_or(Error::PipelinePending)?;
        let seed_pipeline = cache
            .get_render_pipeline(pipeline.seed)
            .ok_or(Error::PipelinePending)?;
        let aux_observation = auxiliary_observations
            .snapshot(packet.tag.view)
            .ok_or(Error::MissingAuxiliary)?;
        if !aux_observation.complete() {
            return Err(Error::IncompleteAuxiliary);
        }
        if aux_observation.tag != packet.tag
            || aux_observation.generation != auxiliary.generation
            || !auxiliary
                .was_aux_encoded(packet.tag)
                .map_err(|_| Error::IncompleteAuxiliary)?
            || targets.view != packet.tag.view
            || prepared.tag != packet.tag
            || prepared.packet != packet.packet
            || prepared.generation != targets.generation
            || targets.auxiliary_generation != auxiliary.generation
            || targets.history_epoch != packet.frame.history_epoch
        {
            return Err(Error::WrongTagOrGeneration);
        }
        let size = targets.input_size;
        if size != packet.frame.input_size
            || targets.output_size != packet.frame.output_size
            || size != targets.output_size
        {
            return Err(Error::UnsupportedSize);
        }
        validate_color(
            target.main_texture(),
            size,
            TextureUsages::TEXTURE_BINDING | TextureUsages::COPY_DST,
        )?;
        validate_depth(
            &auxiliary.current_final_depth.texture,
            size,
            TextureUsages::TEXTURE_BINDING | TextureUsages::COPY_SRC,
        )?;
        validate_depth(
            &targets.previous_source_depth.texture,
            size,
            TextureUsages::TEXTURE_BINDING
                | TextureUsages::COPY_DST
                | TextureUsages::RENDER_ATTACHMENT,
        )?;
        validate_color(
            &auxiliary.velocity.texture,
            size,
            TextureUsages::TEXTURE_BINDING,
        )?;
        let mask = &auxiliary.mask.texture;
        if mask.format() != TextureFormat::Rgba8Unorm
            || mask.width() != size.x
            || mask.height() != size.y
            || mask.depth_or_array_layers() != 1
            || mask.sample_count() != 1
            || !mask.usage().contains(TextureUsages::TEXTURE_BINDING)
        {
            return Err(Error::InvalidTexture);
        }
        let mut state = targets.history.lock().map_err(|_| Error::StatePoisoned)?;
        validate_previous_camera(&packet.packet, state.last_packet.as_ref())?;
        if state
            .last_tag
            .is_some_and(|tag| tag.serial >= packet.tag.serial)
        {
            return Err(Error::StaleAcknowledgement);
        }
        let frames_encoded = state
            .frames_encoded
            .checked_add(1)
            .ok_or(Error::CounterExhausted)?;
        let seed = state.needs_seed;
        let input = &targets.histories[state.current];
        let output = &targets.histories[1 - state.current];
        let ids = [
            input.texture.id(),
            output.texture.id(),
            target.main_texture().id(),
            auxiliary.velocity.texture.id(),
            auxiliary.mask.texture.id(),
        ];
        if ids.into_iter().collect::<HashSet<_>>().len() != ids.len()
            || targets.previous_source_depth.texture.id()
                == auxiliary.current_final_depth.texture.id()
        {
            return Err(Error::AliasedTexture);
        }
        validate_color(
            &input.texture,
            size,
            TextureUsages::TEXTURE_BINDING | TextureUsages::RENDER_ATTACHMENT,
        )?;
        validate_color(
            &output.texture,
            size,
            TextureUsages::COPY_SRC | TextureUsages::RENDER_ATTACHMENT,
        )?;
        let ack = AckResolve {
            render_owned: owned_packet.is_some(),
            tag: packet.tag,
            history_epoch: targets.history_epoch,
            generation: targets.generation,
            auxiliary_generation: auxiliary.generation,
            seed_draws: u32::from(seed),
            resolve_draws: 1,
            draws: 1 + u32::from(seed),
            previous_depth_copied: true,
            output_copied: true,
        };
        // Reserve both owners before mutating any texture; no late poisoned bus
        // may consume the image while failing to acknowledge its frame.
        let mut ack_state = acknowledgements
            .0
            .lock()
            .map_err(|_| Error::StatePoisoned)?;
        ack_state.validate(ack)?;
        let render_reservation = if owned_packet.is_some() {
            Some(
                render_owner
                    .as_deref()
                    .ok_or(Error::NotResolveOwner)?
                    .reserve_pending(packet.tag, packet.frame.history_epoch)
                    .map_err(Error::RenderOwnership)?,
            )
        } else {
            None
        };
        let device = context.render_device();
        let inputs = device.create_bind_group(
            "Tidewater source resolve actual HDR/depth/velocity/mask/history",
            &cache.get_bind_group_layout(&pipeline.inputs),
            &BindGroupEntries::sequential((
                target.main_texture_view(),
                &auxiliary.current_final_depth.default_view,
                &targets.previous_source_depth.default_view,
                &auxiliary.velocity.default_view,
                &auxiliary.mask.default_view,
                &input.default_view,
                &pipeline.sampler,
            )),
        );
        let seed_inputs = device.create_bind_group(
            "Tidewater source history seed actual HDR",
            &cache.get_bind_group_layout(&pipeline.seed_inputs),
            &BindGroupEntries::with_indices((
                (0, target.main_texture_view()),
                (6, &pipeline.sampler),
            )),
        );
        observation.history_input_id = format!("{:?}", input.texture.id());
        observation.history_output_id = format!("{:?}", output.texture.id());
        observation.previous_depth_id = format!("{:?}", targets.previous_source_depth.texture.id());
        if seed {
            // Source WebGPU first/new previous depth is zero, unlike Aux seed=N.
            clear_previous_depth(&mut context, &targets.previous_source_depth.default_view);
            observation.previous_depth_cleared = true;
            draw(
                &mut context,
                seed_pipeline,
                &input.default_view,
                &seed_inputs,
                None,
                "Tidewater source temporal bilinear seed",
            );
            observation.seed_draws = 1;
        }
        // All existing owner/HDR/aux/tag/generation/texture/pipeline guards
        // above remain mandatory. Preserve seed -> input copy -> resolve.
        let readback_ticket =
            if let (Some(bus), Some(jobs)) = (readback.as_deref(), readback_jobs.as_deref_mut()) {
                let metadata = super::resolve_readback::Metadata {
                    tag: packet.tag,
                    history_epoch: targets.history_epoch,
                    resolve_generation: targets.generation,
                    auxiliary_generation: auxiliary.generation,
                    input_size: targets.input_size,
                    output_size: targets.output_size,
                    history_seeded: observation.seed_draws == 1,
                    previous_depth_cleared: observation.previous_depth_cleared,
                    frames_encoded_before: state.frames_encoded,
                    packet: prepared.packet.clone(),
                    params: prepared.params,
                };
                match super::resolve_readback::before_resolve(
                    bus,
                    jobs,
                    &mut context,
                    metadata,
                    super::resolve_readback::Inputs {
                        beauty: target.main_texture(),
                        input_history: &input.texture,
                        previous_source_depth: &targets.previous_source_depth.texture,
                        current_depth: &auxiliary.current_final_depth.texture,
                        velocity: &auxiliary.velocity.texture,
                        mask: &auxiliary.mask.texture,
                        expected_output: &output.texture,
                    },
                ) {
                    Ok(ticket) => ticket,
                    Err(error) => {
                        // The bus already records failure; do not qualify/read a
                        // stale snapshot or alter the source resolve controls.
                        warn!("Source resolve readback inputs failed: {error}");
                        None
                    }
                }
            } else {
                None
            };
        draw(
            &mut context,
            resolve_pipeline,
            &output.default_view,
            &inputs,
            Some(&prepared.uniforms),
            "Tidewater source temporal resolve",
        );
        observation.resolve_draws = 1;
        // Private instrumented pass reads the same seven textures/uniform groups
        // before any original previous-depth/HDR overwrite. It never changes the
        // original source draw, owner or acknowledgement, even on diagnostic FAIL.
        if let (Some(bus), Some(jobs)) = (trace.as_deref(), trace_jobs.as_deref())
            && let Err(error) = super::trace_readback::after_resolve(
                bus,
                jobs,
                trace_pipeline.as_deref(),
                &cache,
                &mut context,
                super::resolve_readback::Metadata {
                    tag: packet.tag,
                    history_epoch: targets.history_epoch,
                    resolve_generation: targets.generation,
                    auxiliary_generation: auxiliary.generation,
                    input_size: targets.input_size,
                    output_size: targets.output_size,
                    history_seeded: observation.seed_draws == 1,
                    previous_depth_cleared: observation.previous_depth_cleared,
                    frames_encoded_before: state.frames_encoded,
                    packet: prepared.packet.clone(),
                    params: prepared.params,
                },
                super::trace_readback::Inputs {
                    texture_group: &inputs,
                    uniform_group: &prepared.uniforms,
                    textures: [
                        target.main_texture(),
                        &input.texture,
                        &targets.previous_source_depth.texture,
                        &auxiliary.current_final_depth.texture,
                        &auxiliary.velocity.texture,
                        &auxiliary.mask.texture,
                        &output.texture,
                    ],
                },
            )
        {
            warn!("Source resolve private diagnostic trace failed: {error}");
        }
        // Copy the actual RGBA16F output before HDR/previous-depth mutation.
        if let (Some(ticket), Some(bus), Some(jobs)) = (
            readback_ticket,
            readback.as_deref(),
            readback_jobs.as_deref_mut(),
        ) && let Err(error) =
            super::resolve_readback::after_resolve(bus, jobs, &mut context, ticket, &output.texture)
        {
            warn!("Source resolve readback output failed: {error}");
        }
        let mut from = auxiliary.current_final_depth.texture.as_image_copy();
        from.aspect = TextureAspect::DepthOnly;
        let mut to = targets.previous_source_depth.texture.as_image_copy();
        to.aspect = TextureAspect::DepthOnly;
        context.command_encoder().copy_texture_to_texture(
            from,
            to,
            targets.previous_source_depth.texture.size(),
        );
        observation.previous_depth_copied = true;
        context.command_encoder().copy_texture_to_texture(
            output.texture.as_image_copy(),
            target.main_texture().as_image_copy(),
            output.texture.size(),
        );
        observation.output_copied = true;
        if let Some(reservation) = render_reservation {
            reservation
                .commit_resolve(
                    targets.generation,
                    auxiliary.generation,
                    observation.resolve_draws,
                    observation.previous_depth_copied,
                    observation.output_copied,
                )
                .map_err(Error::RenderOwnership)?;
        }
        ack_state.push(ack)?;
        state.current = 1 - state.current;
        state.last_tag = Some(packet.tag);
        state.last_packet = Some(packet.packet.clone());
        state.last_far = Some(packet.frame.near_far.y);
        state.frames_encoded = frames_encoded;
        state.needs_seed = false;
        observation.frames_encoded = frames_encoded;
        observation.resolve_acknowledged = true;
        Ok(())
    })();
    observation.error = result.err();
    if owned_packet.is_some()
        && let (Some(owner), Some(runtime), Some(bus)) = (
            render_owner.as_deref(),
            render_runtime.as_deref(),
            render_observation.as_deref(),
        )
    {
        bus.publish(
            owner,
            runtime,
            observation.error.as_ref().map(ToString::to_string),
        );
    }
    observations.record(observation);
}

fn clear_previous_depth(context: &mut RenderContext, depth: &TextureView) {
    let pass = context
        .command_encoder()
        .begin_render_pass(&RenderPassDescriptor {
            label: Some("Tidewater source temporal previous depth clear0"),
            color_attachments: &[],
            depth_stencil_attachment: Some(RenderPassDepthStencilAttachment {
                view: depth,
                depth_ops: Some(Operations {
                    load: LoadOp::Clear(0.0),
                    store: StoreOp::Store,
                }),
                stencil_ops: None,
            }),
            ..default()
        });
    drop(pass);
}

fn draw(
    context: &mut RenderContext,
    pipeline: &bevy::render::render_resource::RenderPipeline,
    destination: &TextureView,
    inputs: &BindGroup,
    uniforms: Option<&BindGroup>,
    label: &str,
) {
    let attachments = [Some(RenderPassColorAttachment {
        view: destination,
        depth_slice: None,
        resolve_target: None,
        ops: Operations {
            load: LoadOp::Clear(default()),
            store: StoreOp::Store,
        },
    })];
    let mut pass = context
        .command_encoder()
        .begin_render_pass(&RenderPassDescriptor {
            label: Some(label),
            color_attachments: &attachments,
            depth_stencil_attachment: None,
            ..default()
        });
    pass.set_pipeline(pipeline);
    pass.set_bind_group(0, inputs, &[]);
    if let Some(uniforms) = uniforms {
        pass.set_bind_group(1, uniforms, &[]);
    }
    pass.draw(0..3, 0..1);
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::camera::CameraProjection;

    #[test]
    #[allow(
        clippy::float_cmp,
        reason = "The plane component must preserve the exact authored near value."
    )]
    fn standard_bevy_plane_accepts_native_near_update_without_pixel_change() {
        let native = PerspectiveProjection {
            near: 0.06,
            far: 60_000.0,
            ..default()
        };
        // Exact native constructor: near changes, default plane.w remains−0.1.
        assert_eq!(native.near_clip_plane.w, -0.1);
        assert!(supported_camera_projection(&native));
        let mut canonical = native.clone();
        canonical.near_clip_plane.w = -native.near;
        assert!(supported_camera_projection(&canonical));
        assert_eq!(native.get_clip_from_view(), canonical.get_clip_from_view());
        let mut oblique = native.clone();
        oblique.near_clip_plane.x = 0.1;
        assert!(!supported_camera_projection(&oblique));
        assert_ne!(native.get_clip_from_view(), oblique.get_clip_from_view());
        let mut invalid = native.clone();
        invalid.fov = f32::NAN;
        assert!(!supported_camera_projection(&invalid));
        invalid = native.clone();
        invalid.near = 0.0;
        assert!(!supported_camera_projection(&invalid));
        invalid = native;
        invalid.far = f32::INFINITY;
        assert!(!supported_camera_projection(&invalid));
    }

    fn ack(view: Entity, serial: u64) -> AckResolve {
        AckResolve {
            render_owned: false,
            tag: FrameTag { view, serial },
            history_epoch: 7,
            generation: 2,
            auxiliary_generation: 3,
            seed_draws: 1,
            resolve_draws: 1,
            draws: 2,
            previous_depth_copied: true,
            output_copied: true,
        }
    }

    #[test]
    fn resolve_bus_requires_all_encoding_and_retains_duplicate_guard_after_drain() {
        let first = Entity::from_bits(7);
        let second = Entity::from_bits(8);
        let bus = ResolveAcknowledgements::default();
        let mut state = bus.0.lock().expect("bus");
        let mut incomplete = ack(first, 4);
        incomplete.previous_depth_copied = false;
        assert_eq!(state.push(incomplete), Err(Error::IncompleteEncoding));
        assert!(state.queue.is_empty());
        state.push(ack(first, 4)).expect("first");
        state.push(ack(second, 4)).expect("other view");
        assert_eq!(state.push(ack(first, 4)), Err(Error::StaleAcknowledgement));
        drop(state);
        assert_eq!(bus.drain().expect("drain").len(), 2);
        assert_eq!(
            bus.0.lock().expect("bus").push(ack(first, 3)),
            Err(Error::StaleAcknowledgement)
        );
        assert!(bus.drain().expect("empty").is_empty());
    }

    #[test]
    fn source_histories_have_exact_odd_size_distinct_roles_and_copy_usages() {
        let odd = UVec2::new(801, 601);
        let color = texture_descriptor(odd, false, "history");
        let depth = texture_descriptor(odd, true, "previous-depth");
        assert_eq!(color.size.width, 801);
        assert_eq!(color.size.height, 601);
        assert_eq!(color.format, TextureFormat::Rgba16Float);
        assert_eq!(depth.format, TextureFormat::Depth32Float);
        assert_ne!(color.label, depth.label);
        assert!(
            depth
                .usage
                .contains(TextureUsages::COPY_SRC | TextureUsages::COPY_DST)
        );
        assert_eq!(Params::min_size().get(), 32);
        assert_eq!(Packet::min_size().get(), 336);
    }

    #[test]
    fn resize_seeds_color_and_clear_depth_without_losing_previous_camera_far() {
        let packet = Packet {
            current_unjittered_clip_from_world: Mat4::IDENTITY,
            previous_unjittered_clip_from_world: Mat4::IDENTITY,
            current_world_from_jittered_clip: Mat4::IDENTITY,
            previous_world_from_jittered_clip: Mat4::IDENTITY,
            camera_position: Vec4::new(3.0, 1.0, -7.0, 1.0),
            previous_camera_position: Vec4::new(3.0, 1.0, -7.0, 1.0),
            sizes: UVec4::new(801, 601, 801, 601),
            near_far_jitter: Vec4::new(0.1, 60_000.0, 0.0, -0.166_666_67),
            phase_epoch_seed: UVec4::ZERO,
        };
        let resized = new_allocation_history(Some(History {
            current: 1,
            last_tag: Some(FrameTag {
                view: Entity::from_bits(7),
                serial: 18,
            }),
            last_packet: Some(packet.clone()),
            last_far: Some(60_000.0),
            frames_encoded: 17,
            needs_seed: false,
        }));
        assert_eq!(resized.current, 0);
        assert!(resized.needs_seed);
        assert_eq!(resized.last_tag, None);
        assert_eq!(resized.last_packet, Some(packet.clone()));
        assert_eq!(resized.last_far, Some(60_000.0));
        assert_eq!(resized.frames_encoded, 17);
        validate_previous_camera(&packet, resized.last_packet.as_ref())
            .expect("same last encoded camera");
        let mut wrong = packet;
        wrong.previous_camera_position.x += 1.0;
        assert_eq!(
            validate_previous_camera(&wrong, resized.last_packet.as_ref()),
            Err(Error::PreviousCameraMismatch)
        );
        let first = new_allocation_history(None);
        assert!(first.needs_seed);
        assert!(first.last_far.is_none());
        assert!(first.last_packet.is_none());
    }
}
