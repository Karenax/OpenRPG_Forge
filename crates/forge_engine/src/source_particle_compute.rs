//! Source particle compute on Bevy's shared renderer, before camera rendering.
//!
//! This is a transient GPU projection, not an authored effect document. Intake
//! must admit the shader and resources with their provenance before enqueueing.
//! No source-specific program, emitter, property, mesh or filesystem path lives
//! here. Buffers remain GPU resident and can feed subsequent passes/materials.
use bevy::{
    asset::AssetId,
    core_pipeline::schedule::camera_driver,
    prelude::*,
    render::{
        RenderApp,
        extract_resource::{ExtractResource, ExtractResourcePlugin},
        render_asset::RenderAssets,
        render_resource::{
            BindGroup, BindGroupEntries, BindGroupLayoutDescriptor, BindGroupLayoutEntries, Buffer,
            BufferInitDescriptor, BufferSize, BufferUsages, CachedComputePipelineId,
            CachedPipelineState, ComputePassDescriptor, ComputePipelineDescriptor, PipelineCache,
            SamplerBindingType, ShaderStages, TextureDimension, TextureFormat, TextureSampleType,
            TextureUsages,
            binding_types::{
                sampler, storage_buffer_read_only_sized, storage_buffer_sized, texture_2d,
                uniform_buffer_sized,
            },
        },
        renderer::{RenderContext, RenderGraph},
        storage::{GpuShaderBuffer, ShaderBuffer},
        texture::GpuImage,
    },
    shader::ShaderCacheError,
};
use std::{
    collections::{BTreeMap, HashMap},
    sync::{Arc, Mutex},
};

#[cfg(feature = "physics-rapier")]
use bevy::{
    camera::MainPassResolutionOverride,
    ecs::system::SystemParam,
    render::{
        camera::{ExtractedCamera, TemporalJitter},
        renderer::RenderQueue,
        sync_world::MainEntity,
        view::ExtractedView,
    },
};

/// The admitted source curve ABI: texture, sampler, 64 descriptors, input/output.
pub const SOURCE_PARTICLE_CURVE_BANK_BYTES: usize = 1024;

#[path = "source_particle_program.rs"]
mod program;
pub use program::register_source_particle_compute_program;

#[path = "source_particle_sequence.rs"]
mod sequence;
pub use sequence::SourceParticleComputeSequence;

#[path = "source_particle_emitter.rs"]
mod emitter;
pub use emitter::{SourceParticleEmissionCommand, SourceParticleEmitterSequence};

#[path = "source_particle_scene.rs"]
mod scene;
pub use scene::{SourceParticleSceneOwner, SourceParticleScenePlugin};

#[path = "source_particle_dependencies.rs"]
mod dependencies;
use dependencies::{admit_dependencies, dispatch_order};

#[cfg(feature = "physics-rapier")]
#[path = "source_particle_draw.rs"]
pub(crate) mod draw;
#[cfg(feature = "physics-rapier")]
#[path = "source_particle_projection.rs"]
mod projection;
#[cfg(feature = "physics-rapier")]
pub(crate) use projection::SourceParticleCameraSnapshot;
#[cfg(feature = "physics-rapier")]
pub use projection::{
    SourceParticleProjection, SourceParticleProjectionPlugin, SourceParticleProjectionUpdate,
};

#[derive(Clone, Debug, PartialEq, Eq)]
pub enum SourceParticleComputeStatus {
    WaitingForResources,
    Compiling,
    /// Command encoded; GPU completion still requires a readback or fence.
    Encoded,
    Failed(String),
}

/// One dispatch, executed at most once while retained in the queue. To advance a
/// simulation, replace it with a new request; no per-particle ECS is allocated.
#[derive(Clone)]
pub struct SourceParticleComputeRequest {
    shader: Handle<Shader>,
    entry_point: String,
    curve: Handle<Image>,
    bank: Arc<[u8; SOURCE_PARTICLE_CURVE_BANK_BYTES]>,
    input: Handle<ShaderBuffer>,
    output: Handle<ShaderBuffer>,
    workgroups: u32,
    status: Arc<Mutex<SourceParticleComputeStatus>>,
    publication: Option<SourceParticlePublication>,
    input_copy: Option<SourceParticleInputCopy>,
    after_slot: Option<u64>,
    // Private identity of the component that submitted this group. Plain
    // public queue requests carry no owner and must never be retired by a host.
    queue_owner: Option<Arc<()>>,
    #[cfg(feature = "physics-rapier")]
    raster_owner: Option<Arc<()>>,
    // Set only by a retained source-scene host. Unrelated compute clients do
    // not depend on source-scene admission.
    #[cfg(feature = "physics-rapier")]
    scene_owner: Option<Arc<()>>,
}

#[derive(Clone)]
struct SourceParticleInputCopy {
    source: Handle<ShaderBuffer>,
    bytes: u64,
}

#[derive(Clone)]
struct SourceParticlePublication {
    buffer: Handle<ShaderBuffer>,
    bytes: u64,
}

impl SourceParticleComputeRequest {
    #[allow(
        clippy::too_many_arguments,
        reason = "Explicit source ABI resources, no implicit defaults"
    )]
    pub fn new(
        shader: Handle<Shader>,
        entry_point: String,
        curve: Handle<Image>,
        bank: [u8; SOURCE_PARTICLE_CURVE_BANK_BYTES],
        input: Handle<ShaderBuffer>,
        output: Handle<ShaderBuffer>,
        workgroups: u32,
    ) -> Result<Self, &'static str> {
        if input.id() == output.id() {
            return Err("Source particle input/output alias; use distinct GPU buffers");
        }
        if !(1..=65_535).contains(&workgroups) {
            return Err("Source particle dispatch exceeds the portable workgroup budget");
        }
        if entry_point.is_empty()
            || !entry_point
                .bytes()
                .all(|c| c.is_ascii_alphanumeric() || c == b'_')
            || entry_point.as_bytes()[0].is_ascii_digit()
        {
            return Err("Source particle compute entry point is invalid");
        }
        Ok(Self {
            shader,
            entry_point,
            curve,
            bank: Arc::new(bank),
            input,
            output,
            workgroups,
            status: Arc::new(Mutex::new(SourceParticleComputeStatus::WaitingForResources)),
            publication: None,
            input_copy: None,
            after_slot: None,
            queue_owner: None,
            #[cfg(feature = "physics-rapier")]
            raster_owner: None,
            #[cfg(feature = "physics-rapier")]
            scene_owner: None,
        })
    }

    /// Copy the completed output into a separate stable GPU buffer. In an
    /// ordered group, only the last phase may publish; all resources are
    /// admitted before any dispatch or copy. Encoding still is not a GPU fence.
    pub fn publish_into(
        mut self,
        buffer: Handle<ShaderBuffer>,
        bytes: u64,
    ) -> Result<Self, &'static str> {
        if Arc::strong_count(&self.status) != 1
            || self.status()? != SourceParticleComputeStatus::WaitingForResources
        {
            return Err(
                "Source publication must be configured before sharing or enqueueing a request",
            );
        }
        if buffer == self.input || buffer == self.output || bytes == 0 || !bytes.is_multiple_of(4) {
            return Err("Source publication aliases simulation state or has an invalid copy size");
        }
        self.publication = Some(SourceParticlePublication { buffer, bytes });
        Ok(self)
    }

    pub fn status(&self) -> Result<SourceParticleComputeStatus, &'static str> {
        self.status
            .lock()
            .map(|s| s.clone())
            .map_err(|_| "Source particle compute status poisoned")
    }

    /// Refresh a declared input prefix on GPU, preserving its static suffix.
    pub fn copy_input_prefix_from(
        mut self,
        source: Handle<ShaderBuffer>,
        bytes: u64,
    ) -> Result<Self, &'static str> {
        if Arc::strong_count(&self.status) != 1
            || self.status()? != SourceParticleComputeStatus::WaitingForResources
            || source == self.input
            || source == self.output
            || bytes == 0
            || !bytes.is_multiple_of(4)
        {
            return Err(
                "Source input copy requires an unshared request, distinct source and aligned prefix",
            );
        }
        self.input_copy = Some(SourceParticleInputCopy { source, bytes });
        Ok(self)
    }

    /// Depend on another queue owner, independent of numeric slot ordering.
    pub fn after_slot(mut self, slot: u64) -> Result<Self, &'static str> {
        if Arc::strong_count(&self.status) != 1
            || self.status()? != SourceParticleComputeStatus::WaitingForResources
        {
            return Err("Source dependency must be configured before sharing/enqueueing");
        }
        self.after_slot = Some(slot);
        Ok(self)
    }

    fn set_status(&self, status: SourceParticleComputeStatus) {
        if let Ok(mut state) = self.status.lock() {
            *state = status;
        }
    }
}

/// Slots are caller identities. Replacing a slot cancels its undispatched job;
/// removing it retires the render-side resources on the next extracted frame.
#[derive(Resource, Clone, Default, ExtractResource)]
pub struct SourceParticleComputeQueue(BTreeMap<u64, Vec<SourceParticleComputeRequest>>);

pub(crate) type RequestIdentity = Arc<Mutex<SourceParticleComputeStatus>>;

impl SourceParticleComputeQueue {
    fn group_is_owned(requests: &[SourceParticleComputeRequest], identity: &Arc<()>) -> bool {
        !requests.is_empty()
            && requests.iter().all(|request| {
                request
                    .queue_owner
                    .as_ref()
                    .is_some_and(|owner| Arc::ptr_eq(owner, identity))
            })
    }

    fn free_or_owned(&self, slot: u64, identity: &Arc<()>) -> bool {
        self.0
            .get(&slot)
            .is_none_or(|requests| Self::group_is_owned(requests, identity))
    }

    fn matches_exact(&self, slot: u64, identity: &Arc<()>, expected: &[RequestIdentity]) -> bool {
        !expected.is_empty()
            && self.0.get(&slot).is_some_and(|requests| {
                Self::group_is_owned(requests, identity)
                    && requests.len() == expected.len()
                    && requests
                        .iter()
                        .zip(expected)
                        .all(|(request, status)| Arc::ptr_eq(&request.status, status))
            })
    }

    pub(crate) fn remove_exact(
        &mut self,
        slot: u64,
        identity: &Arc<()>,
        expected: &[RequestIdentity],
    ) {
        if self.matches_exact(slot, identity, expected) {
            self.0.remove(&slot);
        }
    }

    pub fn insert(&mut self, slot: u64, request: SourceParticleComputeRequest) {
        self.0.insert(slot, vec![request]);
    }

    /// All phases wait for admission before any command is encoded. Their
    /// ordered output/input chain executes before cameras in one render frame.
    pub fn insert_ordered(
        &mut self,
        slot: u64,
        requests: Vec<SourceParticleComputeRequest>,
    ) -> Result<(), &'static str> {
        if requests.is_empty() {
            return Err("Source particle compute group is empty");
        }
        if requests
            .windows(2)
            .any(|pair| pair[0].output != pair[1].input)
        {
            return Err("Source particle compute group has a disconnected state chain");
        }
        if requests[..requests.len() - 1]
            .iter()
            .any(|request| request.publication.is_some())
            || requests
                .last()
                .and_then(|request| request.publication.as_ref())
                .is_some_and(|publication| {
                    requests.iter().any(|request| {
                        publication.buffer == request.input || publication.buffer == request.output
                    })
                })
        {
            return Err(
                "Source publication must follow every phase and use distinct stable storage",
            );
        }
        if requests
            .iter()
            .skip(1)
            .any(|request| request.input_copy.is_some())
        {
            return Err("Source input prefix copy must precede every phase of a group");
        }
        self.0.insert(slot, requests);
        Ok(())
    }

    pub fn remove(&mut self, slot: u64) {
        self.0.remove(&slot);
    }

    #[must_use]
    pub fn contains_slot(&self, slot: u64) -> bool {
        self.0.contains_key(&slot)
    }
}

/// Opt-in; no effect is enabled merely by adding the plugin.
pub struct SourceParticleComputePlugin;

impl Plugin for SourceParticleComputePlugin {
    fn build(&self, app: &mut App) {
        app.init_resource::<SourceParticleComputeQueue>()
            .add_plugins(ExtractResourcePlugin::<SourceParticleComputeQueue>::default());
        if let Some(render) = app.get_sub_app_mut(RenderApp) {
            render
                .init_resource::<PreparedRequests>()
                .add_systems(RenderGraph, encode.before(camera_driver));
            #[cfg(feature = "physics-rapier")]
            render.init_resource::<draw::CurrentFrames>();
        }
    }
}

struct PreparedRequest {
    owner: Arc<Mutex<SourceParticleComputeStatus>>,
    pipeline: CachedComputePipelineId,
    layout: BindGroupLayoutDescriptor,
    bank: Buffer,
    encoded: bool,
}

#[derive(Resource, Default)]
struct PreparedRequests {
    requests: BTreeMap<(u64, usize), PreparedRequest>,
    programs: PreparedPrograms,
}

type PreparedPrograms =
    HashMap<(AssetId<Shader>, String), (CachedComputePipelineId, BindGroupLayoutDescriptor)>;

fn prepare(
    request: &SourceParticleComputeRequest,
    context: &RenderContext,
    cache: &PipelineCache,
    programs: &mut PreparedPrograms,
) -> PreparedRequest {
    let (pipeline, layout) = programs
        .entry((request.shader.id(), request.entry_point.clone()))
        .or_insert_with(|| {
            let layout = BindGroupLayoutDescriptor::new(
                "source particle compute",
                &BindGroupLayoutEntries::sequential(
                    ShaderStages::COMPUTE,
                    (
                        texture_2d(TextureSampleType::Float { filterable: true }),
                        sampler(SamplerBindingType::Filtering),
                        uniform_buffer_sized(
                            false,
                            BufferSize::new(SOURCE_PARTICLE_CURVE_BANK_BYTES as u64),
                        ),
                        storage_buffer_read_only_sized(false, None),
                        storage_buffer_sized(false, None),
                    ),
                ),
            );
            let pipeline = cache.queue_compute_pipeline(ComputePipelineDescriptor {
                label: Some("source particle compute".into()),
                layout: vec![layout.clone()],
                shader: request.shader.clone(),
                entry_point: Some(request.entry_point.clone().into()),
                ..default()
            });
            (pipeline, layout)
        });
    let bank = context
        .render_device()
        .create_buffer_with_data(&BufferInitDescriptor {
            label: Some("source particle curve descriptors"),
            contents: request.bank.as_ref(),
            usage: BufferUsages::UNIFORM | BufferUsages::COPY_DST,
        });
    PreparedRequest {
        owner: request.status.clone(),
        pipeline: *pipeline,
        layout: layout.clone(),
        bank,
        encoded: false,
    }
}

struct ReadyRequest {
    pipeline: CachedComputePipelineId,
    bindings: BindGroup,
    publication: Option<ReadyPublication>,
    input_copy: Option<ReadyPublication>,
}

struct ReadyPublication {
    source: Buffer,
    destination: Buffer,
    bytes: u64,
}

fn ready_publication(
    request: &SourceParticleComputeRequest,
    buffers: &RenderAssets<GpuShaderBuffer>,
    input: &GpuShaderBuffer,
    output: &GpuShaderBuffer,
    max_storage_bytes: u64,
) -> Result<Option<ReadyPublication>, String> {
    let Some(publication) = &request.publication else {
        return Ok(None);
    };
    let Some(destination) = buffers.get(&publication.buffer) else {
        return Ok(None);
    };
    if !output
        .buffer_descriptor
        .usage
        .contains(BufferUsages::COPY_SRC)
        || !destination
            .buffer_descriptor
            .usage
            .contains(BufferUsages::COPY_DST | BufferUsages::STORAGE)
        || publication.bytes > output.buffer.size()
        || publication.bytes > destination.buffer.size()
        || destination.buffer.size() > max_storage_bytes
        || destination.buffer.id() == input.buffer.id()
        || destination.buffer.id() == output.buffer.id()
    {
        return Err("Source publication exceeds the distinct storage/copy device ABI".into());
    }
    Ok(Some(ReadyPublication {
        source: output.buffer.clone(),
        destination: destination.buffer.clone(),
        bytes: publication.bytes,
    }))
}

fn ready_input_copy(
    request: &SourceParticleComputeRequest,
    buffers: &RenderAssets<GpuShaderBuffer>,
    input: &GpuShaderBuffer,
) -> Result<Option<ReadyPublication>, String> {
    let Some(copy) = &request.input_copy else {
        return Ok(None);
    };
    let Some(source) = buffers.get(&copy.source) else {
        return Ok(None);
    };
    if !source
        .buffer_descriptor
        .usage
        .contains(BufferUsages::COPY_SRC)
        || !input
            .buffer_descriptor
            .usage
            .contains(BufferUsages::COPY_DST)
        || copy.bytes > source.buffer.size()
        || copy.bytes > input.buffer.size()
        || source.buffer.id() == input.buffer.id()
    {
        return Err("Source input prefix copy exceeds the distinct GPU copy ABI".into());
    }
    Ok(Some(ReadyPublication {
        source: source.buffer.clone(),
        destination: input.buffer.clone(),
        bytes: copy.bytes,
    }))
}

fn ready_request(
    request: &SourceParticleComputeRequest,
    state: &PreparedRequest,
    cache: &PipelineCache,
    buffers: &RenderAssets<GpuShaderBuffer>,
    images: &RenderAssets<GpuImage>,
    context: &RenderContext,
) -> Result<Option<ReadyRequest>, String> {
    if let CachedPipelineState::Err(error) = cache.get_compute_pipeline_state(state.pipeline) {
        if matches!(
            error,
            ShaderCacheError::ShaderNotLoaded(_) | ShaderCacheError::ShaderImportNotYetAvailable
        ) {
            request.set_status(SourceParticleComputeStatus::WaitingForResources);
            return Ok(None);
        }
        return Err(error.to_string());
    }
    if cache.get_compute_pipeline(state.pipeline).is_none() {
        request.set_status(SourceParticleComputeStatus::Compiling);
        return Ok(None);
    }
    let (Some(curve), Some(input), Some(output)) = (
        images.get(&request.curve),
        buffers.get(&request.input),
        buffers.get(&request.output),
    ) else {
        request.set_status(SourceParticleComputeStatus::WaitingForResources);
        return Ok(None);
    };
    let limits = context.render_device().limits();
    if request.workgroups > limits.max_compute_workgroups_per_dimension
        || curve.texture_descriptor.format != TextureFormat::Rgba16Float
        || curve.texture_descriptor.dimension != TextureDimension::D2
        || curve.texture_descriptor.size.depth_or_array_layers != 1
        || curve.texture_descriptor.sample_count != 1
        || !curve
            .texture_descriptor
            .usage
            .contains(TextureUsages::TEXTURE_BINDING)
        || !input
            .buffer_descriptor
            .usage
            .contains(BufferUsages::STORAGE)
        || !output
            .buffer_descriptor
            .usage
            .contains(BufferUsages::STORAGE)
        || input.buffer.size() == 0
        || output.buffer.size() == 0
        || input.buffer.size() > limits.max_storage_buffer_binding_size
        || output.buffer.size() > limits.max_storage_buffer_binding_size
    {
        return Err(
            "Source particle resources exceed the admitted RGBAHalf/2D/storage device ABI".into(),
        );
    }
    let input_copy = ready_input_copy(request, buffers, input)?;
    if request.input_copy.is_some() && input_copy.is_none() {
        request.set_status(SourceParticleComputeStatus::WaitingForResources);
        return Ok(None);
    }
    let publication = ready_publication(
        request,
        buffers,
        input,
        output,
        limits.max_storage_buffer_binding_size,
    )?;
    if request.publication.is_some() && publication.is_none() {
        request.set_status(SourceParticleComputeStatus::WaitingForResources);
        return Ok(None);
    }
    let group = context.render_device().create_bind_group(
        "source particle compute",
        &cache.get_bind_group_layout(&state.layout),
        &BindGroupEntries::sequential((
            &curve.texture_view,
            &curve.sampler,
            state.bank.as_entire_binding(),
            input.buffer.as_entire_binding(),
            output.buffer.as_entire_binding(),
        )),
    );
    Ok(Some(ReadyRequest {
        pipeline: state.pipeline,
        bindings: group,
        publication,
        input_copy,
    }))
}

fn encode_request(
    request: &SourceParticleComputeRequest,
    binding: ReadyRequest,
    cache: &PipelineCache,
    context: &mut RenderContext,
) {
    let pipeline = cache
        .get_compute_pipeline(binding.pipeline)
        .expect("Admitted source compute pipeline remains ready");
    if let Some(copy) = binding.input_copy {
        context.command_encoder().copy_buffer_to_buffer(
            &copy.source,
            0,
            &copy.destination,
            0,
            copy.bytes,
        );
    }
    let mut pass = context
        .command_encoder()
        .begin_compute_pass(&ComputePassDescriptor {
            label: Some("source particle compute"),
            ..default()
        });
    pass.set_pipeline(pipeline);
    pass.set_bind_group(0, &binding.bindings, &[]);
    pass.dispatch_workgroups(request.workgroups, 1, 1);
    drop(pass);
    if let Some(publication) = binding.publication {
        context.command_encoder().copy_buffer_to_buffer(
            &publication.source,
            0,
            &publication.destination,
            0,
            publication.bytes,
        );
    }
}

#[cfg(feature = "physics-rapier")]
#[derive(SystemParam)]
struct RasterViews<'w, 's> {
    inputs: Option<Res<'w, projection::RasterInputs>>,
    validation: Option<Res<'w, projection::SceneValidation>>,
    views: Query<'w, 's, RasterView, With<Camera3d>>,
    render_queue: Res<'w, RenderQueue>,
    frames: ResMut<'w, draw::CurrentFrames>,
}

#[cfg(feature = "physics-rapier")]
type RasterView = (
    &'static MainEntity,
    &'static ExtractedView,
    &'static ExtractedCamera,
    Option<&'static TemporalJitter>,
    Option<&'static MainPassResolutionOverride>,
    Option<&'static crate::source_temporal::frame::ViewPacket>,
    Has<crate::source_temporal::resolve::ResolveEnabled>,
    Has<bevy::anti_alias::taa::TemporalAntiAliasing>,
);

#[cfg(feature = "physics-rapier")]
type RasterBanks = Vec<Option<[u8; 1024]>>;

#[cfg(feature = "physics-rapier")]
fn matching_taa_mode(
    input: &projection::RasterInput,
    bevy_taa: bool,
    jitter: Option<&TemporalJitter>,
    packet: Option<&crate::source_temporal::frame::ViewPacket>,
    resolve_enabled: bool,
) -> bool {
    if input.camera.bevy_taa != bevy_taa {
        return false;
    }
    !bevy_taa || (jitter.is_some() && packet.is_none() && !resolve_enabled)
}

#[cfg(feature = "physics-rapier")]
fn raster_view_bank(
    input: &projection::RasterInput,
    view: &ExtractedView,
    camera: &ExtractedCamera,
    jitter: Option<&TemporalJitter>,
    resolution: Option<&MainPassResolutionOverride>,
    packet: Option<&crate::source_temporal::frame::ViewPacket>,
    resolve_enabled: bool,
) -> Result<Option<[u8; 1024]>, String> {
    // A selected temporal resolve can temporarily lack a fresh packet while
    // its owner prepares or refuses the frame. Do not treat that as a
    // non-temporal view and encode against a different projection.
    if resolve_enabled && packet.is_none() {
        return Ok(None);
    }
    let world_from_view = view.world_from_view.to_matrix();
    if !world_from_view.abs_diff_eq(input.camera.world_from_view, 1.0e-5)
        || !view
            .clip_from_view
            .abs_diff_eq(input.camera.clip_from_view, 1.0e-5)
    {
        return Ok(None);
    }
    let Some((clip, size)) = raster_view_clip(view, camera, jitter, resolution)? else {
        return Ok(None);
    };
    if let Some(packet) = packet {
        let current = clip * world_from_view.inverse();
        if packet.tag.view != input.camera.view
            || packet.tag.serial == 0
            || packet.frame.input_size != size
            || !packet.frame.clip_from_world.abs_diff_eq(current, 1.0e-4)
        {
            return Err("Particle raster temporal packet differs from the current view".into());
        }
    }
    crate::source_particle_frame::raster_bank(
        &input.bank,
        input.relative_offset,
        input.camera.clip_from_view,
        clip,
    )
    .map(Some)
}

#[cfg(feature = "physics-rapier")]
fn raster_view_clip(
    view: &ExtractedView,
    camera: &ExtractedCamera,
    jitter: Option<&TemporalJitter>,
    resolution: Option<&MainPassResolutionOverride>,
) -> Result<Option<(Mat4, UVec2)>, String> {
    let size = resolution.map_or_else(
        || UVec2::new(view.viewport.z, view.viewport.w),
        |override_size| override_size.0,
    );
    if camera
        .physical_viewport_size
        .is_none_or(|viewport| viewport.min_element() == 0)
        || size.min_element() == 0
    {
        return Ok(None);
    }
    let mut clip = view.clip_from_view;
    if let Some(jitter) = jitter {
        if !jitter.offset.is_finite() {
            return Err("Particle raster jitter is nonfinite".into());
        }
        jitter.jitter_projection(&mut clip, size.as_vec2());
    } else if let Some(clip_from_world) = view.clip_from_world {
        clip = clip_from_world * view.world_from_view.to_matrix();
    }
    Ok(Some((clip, size)))
}

#[cfg(feature = "physics-rapier")]
fn raster_banks(
    slot: u64,
    requests: &[SourceParticleComputeRequest],
    raster: &RasterViews<'_, '_>,
) -> Result<Option<RasterBanks>, String> {
    let mut result = Vec::with_capacity(requests.len());
    for request in requests {
        if let Some(owner) = &request.scene_owner
            && !raster
                .validation
                .as_ref()
                .is_some_and(|validation| validation.producer_allowed(slot, owner))
        {
            return Ok(None);
        }
        let Some(owner) = &request.raster_owner else {
            result.push(None);
            continue;
        };
        if !raster
            .validation
            .as_ref()
            .is_some_and(|validation| validation.projection_allowed(slot, owner))
        {
            return Ok(None);
        }
        let Some(input) = raster
            .inputs
            .as_ref()
            .and_then(|inputs| inputs.0.get(&slot))
        else {
            return Ok(None);
        };
        if !Arc::ptr_eq(owner, &input.owner) {
            return Err("Particle raster owner changed".into());
        }
        let mut matches = raster
            .views
            .iter()
            .filter(|(main, ..)| main.id() == input.camera.view);
        let Some((_, view, camera, jitter, resolution, packet, resolve_enabled, bevy_taa)) =
            matches.next()
        else {
            return Ok(None);
        };
        if matches.next().is_some() {
            return Err("Particle raster camera has multiple extracted views".into());
        }
        if !matching_taa_mode(input, bevy_taa, jitter, packet, resolve_enabled) {
            return Ok(None);
        }
        let Some(bank) = raster_view_bank(
            input,
            view,
            camera,
            jitter,
            resolution,
            packet,
            resolve_enabled,
        )?
        else {
            return Ok(None);
        };
        result.push(Some(bank));
    }
    Ok(Some(result))
}

#[cfg(feature = "physics-rapier")]
fn prepare_raster_banks(
    slot: u64,
    requests: &[SourceParticleComputeRequest],
    raster: &RasterViews<'_, '_>,
    prepared: &PreparedRequests,
) -> bool {
    let banks = match raster_banks(slot, requests, raster) {
        Ok(Some(banks)) => banks,
        Ok(None) => {
            for request in requests {
                request.set_status(SourceParticleComputeStatus::WaitingForResources);
            }
            return false;
        }
        Err(error) => {
            for request in requests {
                request.set_status(SourceParticleComputeStatus::Failed(error.clone()));
            }
            return false;
        }
    };
    for (phase, bank) in banks.iter().enumerate() {
        if let Some(bank) = bank {
            raster
                .render_queue
                .write_buffer(&prepared.requests[&(slot, phase)].bank, 0, bank);
        }
    }
    true
}

#[cfg(feature = "physics-rapier")]
fn record_raster_frame(
    slot: u64,
    requests: &[SourceParticleComputeRequest],
    raster: &mut RasterViews<'_, '_>,
) {
    if let Some(owner) = requests
        .first()
        .and_then(|request| request.raster_owner.as_ref())
        && requests.len() == 1
        && requests[0].status() == Ok(SourceParticleComputeStatus::Encoded)
        && let Some(input) = raster
            .inputs
            .as_ref()
            .and_then(|inputs| inputs.0.get(&slot))
        && let Some((_, view, camera, jitter, resolution, packet, resolve, bevy_taa)) = raster
            .views
            .iter()
            .find(|(main, ..)| main.id() == input.camera.view)
        && matching_taa_mode(input, bevy_taa, jitter, packet, resolve)
        && let Ok(Some((clip, _))) = raster_view_clip(view, camera, jitter, resolution)
    {
        raster.frames.record(
            draw::ParticleFrameOwner {
                slot,
                identity: owner.clone(),
                output: requests[0].output.id(),
            },
            input.camera.view,
            view.world_from_view.to_matrix(),
            clip,
        );
    }
}

/// A shared status outlives `RenderWorld`'s prepared cache. An encoded or
/// failed request must not run again if that cache entry was later evicted.
fn skip_previous_group(requests: &[SourceParticleComputeRequest]) -> bool {
    let mut encoded = 0;
    for request in requests {
        match request.status() {
            Ok(SourceParticleComputeStatus::Encoded) => encoded += 1,
            Ok(
                SourceParticleComputeStatus::WaitingForResources
                | SourceParticleComputeStatus::Compiling,
            ) => {}
            Ok(SourceParticleComputeStatus::Failed(_)) | Err(_) => return true,
        }
    }
    if encoded == 0 {
        return false;
    }
    if encoded != requests.len() {
        for request in requests {
            if matches!(
                request.status(),
                Ok(SourceParticleComputeStatus::WaitingForResources
                    | SourceParticleComputeStatus::Compiling)
            ) {
                request.set_status(SourceParticleComputeStatus::Failed(
                    "Source particle group contains a previously encoded partial request".into(),
                ));
            }
        }
    }
    true
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy render system parameters"
)]
fn encode(
    queue: Option<Res<SourceParticleComputeQueue>>,
    mut prepared: ResMut<PreparedRequests>,
    cache: Res<PipelineCache>,
    buffers: Res<RenderAssets<GpuShaderBuffer>>,
    images: Res<RenderAssets<GpuImage>>,
    mut context: RenderContext,
    #[cfg(feature = "physics-rapier")] mut raster: RasterViews,
) {
    #[cfg(feature = "physics-rapier")]
    raster.frames.clear();
    let Some(queue) = queue else {
        return;
    };
    prepared.requests.retain(|(slot, phase), _| {
        queue
            .0
            .get(slot)
            .is_some_and(|requests| *phase < requests.len())
    });
    let (ordered, cyclic) = dispatch_order(&queue.0);
    for slot in cyclic {
        for request in &queue.0[&slot] {
            request.set_status(SourceParticleComputeStatus::Failed(
                "Source compute dependency cycle".into(),
            ));
        }
    }
    for slot in &ordered {
        let requests = &queue.0[slot];
        if skip_previous_group(requests) {
            continue;
        }
        if !admit_dependencies(requests, &queue.0) {
            continue;
        }
        for (phase, request) in requests.iter().enumerate() {
            let key = (*slot, phase);
            let replace = prepared
                .requests
                .get(&key)
                .is_none_or(|p| !Arc::ptr_eq(&p.owner, &request.status));
            if replace {
                let state = prepare(request, &context, &cache, &mut prepared.programs);
                prepared.requests.insert(key, state);
            }
        }
        let encoded = (0..requests.len())
            .filter(|phase| prepared.requests[&(*slot, *phase)].encoded)
            .count();
        if encoded == requests.len() {
            continue;
        }
        if encoded != 0 {
            for request in requests {
                request.set_status(SourceParticleComputeStatus::Failed(
                    "Source particle group reused a partially encoded request".into(),
                ));
            }
            continue;
        }
        let mut ready = Vec::with_capacity(requests.len());
        let mut failure = None;
        for (phase, request) in requests.iter().enumerate() {
            match ready_request(
                request,
                &prepared.requests[&(*slot, phase)],
                &cache,
                &buffers,
                &images,
                &context,
            ) {
                Ok(Some(binding)) => ready.push(binding),
                Ok(None) => {}
                Err(error) => {
                    failure = Some(error);
                    break;
                }
            }
        }
        if let Some(error) = failure {
            for request in requests {
                request.set_status(SourceParticleComputeStatus::Failed(error.clone()));
            }
            continue;
        }
        if ready.len() != requests.len() {
            continue;
        }
        #[cfg(feature = "physics-rapier")]
        if !prepare_raster_banks(*slot, requests, &raster, &prepared) {
            continue;
        }
        // Admission above is complete before the first state-changing dispatch.
        // PipelineCache is immutable throughout this render system invocation.
        for (phase, (request, binding)) in requests.iter().zip(ready).enumerate() {
            encode_request(request, binding, &cache, &mut context);
            prepared.requests.get_mut(&(*slot, phase)).unwrap().encoded = true;
            request.set_status(SourceParticleComputeStatus::Encoded);
        }
        #[cfg(feature = "physics-rapier")]
        record_raster_frame(*slot, requests, &mut raster);
    }
}

#[cfg(test)]
mod replay_tests {
    use super::*;

    fn request() -> SourceParticleComputeRequest {
        let mut buffers = Assets::<ShaderBuffer>::default();
        SourceParticleComputeRequest::new(
            Handle::default(),
            "advance".into(),
            Handle::default(),
            [0; 1024],
            buffers.add(ShaderBuffer::new(&[0_u8; 16], default())),
            buffers.add(ShaderBuffer::new(&[0_u8; 16], default())),
            1,
        )
        .unwrap()
    }

    #[test]
    fn encoded_group_cannot_replay_after_queue_and_prepared_cache_eviction() {
        let first = request();
        let mut queue = SourceParticleComputeQueue::default();
        queue.insert(7, first.clone());
        let saved = queue.clone();
        assert!(!skip_previous_group(&queue.0[&7]));
        first.set_status(SourceParticleComputeStatus::Encoded);
        queue.remove(7);
        let prepared = PreparedRequests::default();
        assert!(prepared.requests.is_empty());
        queue = saved;
        assert!(skip_previous_group(&queue.0[&7]));
        assert_eq!(
            queue.0[&7][0].status(),
            Ok(SourceParticleComputeStatus::Encoded)
        );
    }

    #[test]
    fn partial_or_failed_group_never_becomes_a_new_dispatch() {
        let first = request();
        let second = request();
        first.set_status(SourceParticleComputeStatus::Encoded);
        assert!(skip_previous_group(&[first.clone(), second.clone()]));
        assert_eq!(first.status(), Ok(SourceParticleComputeStatus::Encoded));
        assert!(matches!(
            second.status(),
            Ok(SourceParticleComputeStatus::Failed(_))
        ));
        let failed = request();
        failed.set_status(SourceParticleComputeStatus::Failed(
            "original failure".into(),
        ));
        assert!(skip_previous_group(std::slice::from_ref(&failed)));
        assert_eq!(
            failed.status(),
            Ok(SourceParticleComputeStatus::Failed(
                "original failure".into()
            ))
        );
    }
}

#[cfg(all(test, feature = "physics-rapier"))]
mod raster_tests {
    use super::*;
    use bevy::{ecs::schedule::ScheduleLabel, render::view::RetainedViewEntity};

    pub(super) fn fixture() -> (projection::RasterInput, ExtractedView, ExtractedCamera) {
        let mut world = World::new();
        let camera_entity = world.spawn_empty().id();
        let size = UVec2::new(640, 480);
        let clip = Mat4::perspective_infinite_reverse_rh(1.0, 4.0 / 3.0, 0.1);
        let pose = Mat4::from_translation(Vec3::new(2.0, 3.0, 4.0));
        let frame = crate::source_particle_frame::SourceParticleFrame::from_current_scene(
            Mat4::IDENTITY,
            Mat4::IDENTITY,
            Mat4::IDENTITY,
            pose,
            clip,
        )
        .unwrap();
        let layout = forge_assets::source_particle_transport::SourceParticleFrameLayout {
            object_to_world_offset: 0,
            world_to_object_offset: 64,
            relative_world_to_clip_offset: 128,
            camera_position_offset: 192,
            evidence_sha256: forge_core::bytes_sha256(b"raster view fixture"),
        };
        let input = projection::RasterInput {
            owner: Arc::new(()),
            camera: SourceParticleCameraSnapshot {
                view: camera_entity,
                world_from_view: pose,
                clip_from_view: clip,
                bevy_taa: false,
                witness: None,
                clock: None,
                creation_tick: None,
            },
            camera_tick: None,
            relative_offset: 128,
            bank: Arc::new(frame.apply_bank(&layout, &[0x5a; 1024]).unwrap()),
            witness: None,
        };
        let view = ExtractedView {
            retained_view_entity: RetainedViewEntity::new(camera_entity.into(), None, 0),
            clip_from_view: clip,
            world_from_view: GlobalTransform::from(pose),
            clip_from_world: None,
            target_format: TextureFormat::Rgba16Float,
            viewport: UVec4::new(0, 0, size.x, size.y),
            color_grading: default(),
            invert_culling: false,
        };
        let camera = ExtractedCamera {
            target: None,
            physical_viewport_size: Some(size),
            physical_target_size: Some(size),
            viewport: None,
            schedule: bevy::core_pipeline::schedule::Core3d.intern(),
            order: 0,
            output_mode: default(),
            msaa_writeback: bevy::camera::MsaaWriteback::Off,
            clear_color: default(),
            sorted_camera_index_for_target: 0,
            exposure: 1.0,
            hdr: true,
            compositing_space: None,
        };
        (input, view, camera)
    }

    #[test]
    fn render_view_uses_actual_jitter_and_effective_resolution() {
        let (input, view, camera) = fixture();
        let jitter = TemporalJitter {
            offset: Vec2::new(0.25, -0.375),
        };
        let resolution = MainPassResolutionOverride(UVec2::new(320, 240));
        let actual = raster_view_bank(
            &input,
            &view,
            &camera,
            Some(&jitter),
            Some(&resolution),
            None,
            false,
        )
        .unwrap()
        .unwrap();
        let mut clip = view.clip_from_view;
        jitter.jitter_projection(&mut clip, resolution.0.as_vec2());
        let expected =
            crate::source_particle_frame::raster_bank(&input.bank, 128, view.clip_from_view, clip)
                .unwrap();
        assert_eq!(actual, expected);
        assert_ne!(actual, *input.bank);
        assert_eq!(&actual[..128], &input.bank[..128]);
        assert_eq!(&actual[192..], &input.bank[192..]);
    }

    #[test]
    fn render_view_waits_when_bevy_taa_mode_or_jitter_differs_from_snapshot() {
        let (mut input, _, _) = fixture();
        let jitter = TemporalJitter {
            offset: Vec2::new(0.25, -0.375),
        };
        assert!(matching_taa_mode(&input, false, None, None, false));
        assert!(!matching_taa_mode(&input, true, Some(&jitter), None, false));
        input.camera.bevy_taa = true;
        assert!(!matching_taa_mode(
            &input,
            false,
            Some(&jitter),
            None,
            false
        ));
        assert!(!matching_taa_mode(&input, true, None, None, false));
        assert!(matching_taa_mode(&input, true, Some(&jitter), None, false));
        assert!(!matching_taa_mode(&input, true, Some(&jitter), None, true));
    }

    #[test]
    fn render_view_waits_for_matching_camera_and_rejects_invalid_jitter() {
        let (input, mut view, mut camera) = fixture();
        view.world_from_view = GlobalTransform::from_translation(Vec3::X);
        assert!(
            raster_view_bank(&input, &view, &camera, None, None, None, false)
                .unwrap()
                .is_none()
        );
        view.world_from_view = GlobalTransform::from(input.camera.world_from_view);
        camera.physical_viewport_size = None;
        assert!(
            raster_view_bank(&input, &view, &camera, None, None, None, false)
                .unwrap()
                .is_none()
        );
        camera.physical_viewport_size = Some(UVec2::ONE);
        let bad = TemporalJitter {
            offset: Vec2::splat(f32::NAN),
        };
        assert!(raster_view_bank(&input, &view, &camera, Some(&bad), None, None, false).is_err());
        let zero = MainPassResolutionOverride(UVec2::ZERO);
        assert!(
            raster_view_bank(&input, &view, &camera, None, Some(&zero), None, false)
                .unwrap()
                .is_none()
        );
    }

    #[test]
    fn render_view_requires_matching_temporal_packet_identity() {
        let (input, view, camera) = fixture();
        let size = camera.physical_viewport_size.unwrap();
        let mut frames = crate::source_temporal::frame::Frames::default();
        frames.activate(Some(input.camera.view));
        let mut packet = frames
            .prepare_aux(
                input.camera.view,
                crate::source_temporal_frame::Input {
                    world_from_view: input.camera.world_from_view,
                    clip_from_view: input.camera.clip_from_view,
                    input_size: size,
                    output_size: size,
                    near: 0.1,
                    far: 100.0,
                },
            )
            .unwrap();
        let jitter = TemporalJitter {
            offset: packet.frame.jitter_pixels,
        };
        assert!(
            raster_view_bank(
                &input,
                &view,
                &camera,
                Some(&jitter),
                None,
                Some(&packet),
                true
            )
            .unwrap()
            .is_some()
        );
        packet.tag.view = Entity::PLACEHOLDER;
        assert!(
            raster_view_bank(
                &input,
                &view,
                &camera,
                Some(&jitter),
                None,
                Some(&packet),
                true
            )
            .is_err()
        );
    }

    #[test]
    fn render_view_waits_for_enabled_temporal_resolve_without_packet() {
        let (input, view, camera) = fixture();
        assert!(
            raster_view_bank(&input, &view, &camera, None, None, None, true)
                .unwrap()
                .is_none()
        );
        assert!(
            raster_view_bank(&input, &view, &camera, None, None, None, false)
                .unwrap()
                .is_some()
        );
    }
}
