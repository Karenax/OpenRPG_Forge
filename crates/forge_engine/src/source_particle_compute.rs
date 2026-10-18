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
#[path = "source_particle_projection.rs"]
mod projection;
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

impl SourceParticleComputeQueue {
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
            usage: BufferUsages::UNIFORM,
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
) {
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
        // Admission above is complete before the first state-changing dispatch.
        // PipelineCache is immutable throughout this render system invocation.
        for (phase, (request, binding)) in requests.iter().zip(ready).enumerate() {
            encode_request(request, binding, &cache, &mut context);
            prepared.requests.get_mut(&(*slot, phase)).unwrap().encoded = true;
            request.set_status(SourceParticleComputeStatus::Encoded);
        }
    }
}
