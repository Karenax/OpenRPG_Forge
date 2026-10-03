// SPDX-License-Identifier: MIT
//! Opt-in, isolated instrumentation of the source resolve, never an original-instruction proof.
//! The private draw follows the real resolve before depth/HDR overwrite. Reuse its
//! actual texture/uniform bind groups. Two post-submit GPU callbacks produce a
//! transport snapshot; the caller must join the original seven callbacks on the
//! exact tag/generations and compare every scratch/output byte before attribution.
//! Equations derive from Tidewater1438b1abfcaee3267092b75573014f4d9b4a983c,
//! copyright2026 DRG Software Solutions LLC, MIT (`third_party/tidewater/LICENSE`).

use super::{frame::FrameTag, resolve, resolve_readback};
use bevy::{
    prelude::*,
    render::{
        Render, RenderApp, RenderStartup, RenderSystems,
        render_resource::{
            BindGroup, BindGroupEntries, BindGroupLayoutDescriptor, BindGroupLayoutEntries,
            BindingType, Buffer, BufferBindingType, BufferDescriptor, BufferInitDescriptor,
            BufferUsages, CachedRenderPipelineId, ColorTargetState, ColorWrites, Extent3d,
            FragmentState, LoadOp, MapMode, Operations, PipelineCache, RenderPassColorAttachment,
            RenderPassDescriptor, RenderPipelineDescriptor, ShaderStages, ShaderType, StoreOp,
            TexelCopyBufferInfo, TexelCopyBufferLayout, Texture, TextureDescriptor,
            TextureDimension, TextureFormat, TextureSampleType, TextureUsages, VertexState,
            binding_types::{texture_2d, uniform_buffer},
        },
        renderer::{RenderContext, RenderDevice},
        sync_world::MainEntity,
    },
    shader::Shader,
};
pub use resolve_readback::{Metadata, Scope};
use std::{
    collections::{HashMap, HashSet},
    fmt,
    fs::{self, OpenOptions},
    io::Write,
    num::NonZeroU64,
    path::Path,
    sync::{Arc, Mutex},
};

pub const MAX_SITES: usize = 8;
pub const RECORD_BYTES: usize = 1024;
pub const TRACE_BYTES: usize = MAX_SITES * RECORD_BYTES;
pub const MAX_BYTES: u64 = 512 * 1024 * 1024;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct Site {
    pub x: u32,
    pub y: u32,
    pub case_id: u32,
}

#[derive(Clone, Debug, ShaderType)]
struct Selection {
    header: UVec4,
    sites: [UVec4; MAX_SITES],
}
impl Selection {
    fn new(sites: &[Site], size: UVec2) -> Result<Self, Error> {
        validate_sites(sites, Some(size))?;
        let mut packed = [UVec4::ZERO; MAX_SITES];
        for (output, site) in packed.iter_mut().zip(sites) {
            *output = UVec4::new(site.x, site.y, site.case_id, 0);
        }
        Ok(Self {
            header: UVec4::new(
                u32::try_from(sites.len()).map_err(|_| Error::InvalidSites)?,
                64,
                size.x,
                size.y,
            ),
            sites: packed,
        })
    }
    fn bytes(&self) -> Result<Vec<u8>, Error> {
        let mut uniform = bevy::render::render_resource::encase::UniformBuffer::new(Vec::new());
        uniform
            .write(self)
            .map_err(|e| Error::UniformEncoding(e.to_string()))?;
        let bytes = uniform.into_inner();
        if bytes.len() != 144 {
            return Err(Error::InvalidLayout);
        }
        Ok(bytes)
    }
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub enum Error {
    Busy,
    StatePoisoned,
    CounterExhausted,
    InvalidSites,
    DuplicateSite,
    SiteOutOfBounds,
    WrongScope,
    InvalidMetadata,
    UnsupportedScale,
    InvalidTexture,
    AliasedTexture,
    InvalidLayout,
    UnsupportedDeviceLimits,
    PipelinePending,
    TooManyBytes,
    MissingFrame,
    WrongTicket,
    DuplicateCallback,
    InvalidGpuRecord,
    SourceSnapshotMismatch,
    UniformEncoding(String),
    MapFailed(String),
    Cancelled,
}
impl fmt::Display for Error {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "Temporal diagnostic trace: {self:?}")
    }
}
impl std::error::Error for Error {}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Stage {
    Requested,
    Encoded,
    Mapping,
}
#[derive(Clone, Debug)]
pub enum Status {
    Idle,
    Pending { request_id: u64, stage: Stage },
    Ready { request_id: u64 },
    Failed { request_id: u64, error: Error },
}

#[derive(Clone, Debug)]
pub struct Snapshot {
    pub request_id: u64,
    pub metadata: Metadata,
    pub sites: Vec<Site>,
    /// Beauty, `InputHistory`, `PreviousSourceDepth`, `CurrentDepth`, Velocity, Mask, Output.
    pub original_texture_ids: [String; 7],
    pub selection_bytes: Vec<u8>,
    pub trace_bytes: Arc<Vec<u8>>,
    pub scratch: resolve_readback::Plane,
    pub diagnostic_draw_encoded: bool,
}
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct Comparison {
    pub byte_exact: bool,
    pub mismatching_bytes: usize,
    pub mismatching_pixels: usize,
    pub compared_bytes: usize,
}
impl Snapshot {
    #[must_use]
    pub fn complete(&self) -> bool {
        self.diagnostic_draw_encoded
            && validate_metadata(&self.metadata).is_ok()
            && validate_sites(&self.sites, Some(self.metadata.output_size)).is_ok()
            && Selection::new(&self.sites, self.metadata.output_size)
                .and_then(|s| s.bytes())
                .is_ok_and(|b| b == self.selection_bytes)
            && self.scratch.role == resolve_readback::Role::Output
            && self.scratch.format == TextureFormat::Rgba16Float
            && self.scratch.size == self.metadata.output_size
            && validate_records(&self.trace_bytes, &self.sites).is_ok()
            && self.scratch.unpadded_bytes().is_ok()
    }
    /// Exact transport/metadata/texture identity join, then EVERY output byte.
    /// Even equal outputs do not prove identical original float32 instructions.
    pub fn compare_original(
        &self,
        original: &resolve_readback::Snapshot,
    ) -> Result<Comparison, Error> {
        if !self.complete()
            || !original.complete()
            || !same_metadata(&self.metadata, &original.metadata)?
        {
            return Err(Error::SourceSnapshotMismatch);
        }
        let roles = source_roles();
        for (role, identity) in roles.iter().zip(&self.original_texture_ids) {
            let expected_size = if matches!(
                role,
                resolve_readback::Role::InputHistory | resolve_readback::Role::Output
            ) {
                self.metadata.output_size
            } else {
                self.metadata.input_size
            };
            if !original.planes.iter().any(|p| {
                p.role == *role
                    && p.texture_id == *identity
                    && p.format == role.format()
                    && p.size == expected_size
            }) {
                return Err(Error::SourceSnapshotMismatch);
            }
        }
        let output = original
            .planes
            .iter()
            .find(|p| p.role == resolve_readback::Role::Output)
            .ok_or(Error::SourceSnapshotMismatch)?;
        let actual = output.unpadded_bytes().map_err(|_| Error::InvalidLayout)?;
        let scratch = self
            .scratch
            .unpadded_bytes()
            .map_err(|_| Error::InvalidLayout)?;
        if actual.len() != scratch.len() {
            return Err(Error::InvalidLayout);
        }
        let mismatching_bytes = actual.iter().zip(&scratch).filter(|(a, b)| a != b).count();
        let mismatching_pixels = actual
            .as_chunks::<8>()
            .0
            .iter()
            .zip(scratch.as_chunks::<8>().0)
            .filter(|(a, b)| a != b)
            .count();
        Ok(Comparison {
            byte_exact: mismatching_bytes == 0,
            mismatching_bytes,
            mismatching_pixels,
            compared_bytes: actual.len(),
        })
    }
    pub fn record_float_bits(&self, site: usize) -> Result<Vec<u32>, Error> {
        if !self.complete() || site >= self.sites.len() {
            return Err(Error::InvalidLayout);
        }
        Ok(
            self.trace_bytes[site * RECORD_BYTES..(site + 1) * RECORD_BYTES]
                .as_chunks::<4>()
                .0
                .iter()
                .map(|b| u32::from_le_bytes(*b))
                .collect(),
        )
    }
    pub fn write_new_directory(&self, path: &Path) -> std::io::Result<()> {
        if !self.complete() {
            return Err(std::io::Error::other(
                "Incomplete two-callback instrumented trace",
            ));
        }
        let (packet, params) = self
            .metadata
            .uniform_bytes()
            .map_err(std::io::Error::other)?;
        fs::create_dir(path)?;
        let create = |name: &str, data: &[u8]| -> std::io::Result<()> {
            let mut f = OpenOptions::new()
                .write(true)
                .create_new(true)
                .open(path.join(name))?;
            f.write_all(data)?;
            f.sync_all()
        };
        create("trace-float32.bin", &self.trace_bytes)?;
        create("scratch-rgba16f.padded.bin", &self.scratch.bytes)?;
        create(
            "scratch-rgba16f.bin",
            &self
                .scratch
                .unpadded_bytes()
                .map_err(std::io::Error::other)?,
        )?;
        create("selection144.bin", &self.selection_bytes)?;
        create("packet336.bin", &packet)?;
        create("params32.bin", &params)?;
        create("metadata.txt", format!("request_id={}\n{:?}\nsites={:?}\noriginal_texture_ids={:?}\nscratch_texture_id={}\nscratch_format={:?}\nscratch_size={:?}\nscratch_row_bytes={}\nscratch_bytes_per_row={}\nscratch_padded_bytes={}\ninstrumented shader only; not original float32 instructions\n", self.request_id, self.metadata, self.sites, self.original_texture_ids, self.scratch.texture_id, self.scratch.format, self.scratch.size, self.scratch.row_bytes, self.scratch.bytes_per_row, self.scratch.bytes.len()).as_bytes())?;
        create("readback-complete.txt", b"two real diagnostic GPU callbacks received; join original seven on exact tag; instrumented shader only, not original instructions\n")
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
enum Role {
    Trace,
    Scratch,
}
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
struct Ticket {
    id: u64,
    tag: FrameTag,
    epoch: u64,
    resolve_generation: u64,
    auxiliary_generation: u64,
}
impl Ticket {
    fn matches(self, m: &Metadata) -> bool {
        self.tag == m.tag
            && self.epoch == m.history_epoch
            && self.resolve_generation == m.resolve_generation
            && self.auxiliary_generation == m.auxiliary_generation
    }
}
struct Active {
    id: u64,
    scope: Scope,
    sites: Vec<Site>,
    stage: Stage,
    metadata: Option<Metadata>,
    texture_ids: Option<[String; 7]>,
    selection: Vec<u8>,
    scratch: Option<resolve_readback::Plane>,
    trace: Option<Arc<Vec<u8>>>,
    received: HashSet<Role>,
    error: Option<Error>,
}
#[derive(Default)]
struct State {
    next_id: u64,
    active: Option<Active>,
    snapshot: Option<Snapshot>,
    failure: Option<(u64, Error)>,
    stale_callbacks: u64,
    in_flight_bytes: u64,
    outstanding: HashMap<(u64, Role), (Ticket, u64)>,
    pipeline_ready: bool,
    selected_views: HashSet<Entity>,
}
#[derive(Resource, Clone, Default)]
pub struct Observation(Arc<Mutex<State>>);
impl Observation {
    pub fn request(&self, scope: Scope, sites: Vec<Site>) -> Result<u64, Error> {
        validate_sites(&sites, None)?;
        if scope.minimum_serial == 0
            || scope.expected_resolve_generation == Some(0)
            || scope.expected_auxiliary_generation == Some(0)
        {
            return Err(Error::WrongScope);
        }
        let mut s = self.0.lock().map_err(|_| Error::StatePoisoned)?;
        if s.active.is_some() {
            return Err(Error::Busy);
        }
        s.next_id = s.next_id.checked_add(1).ok_or(Error::CounterExhausted)?;
        let id = s.next_id;
        s.failure = None;
        s.snapshot = None;
        s.active = Some(Active {
            id,
            scope,
            sites,
            stage: Stage::Requested,
            metadata: None,
            texture_ids: None,
            selection: Vec::new(),
            scratch: None,
            trace: None,
            received: HashSet::new(),
            error: None,
        });
        Ok(id)
    }
    pub fn status(&self) -> Result<Status, Error> {
        let s = self.0.lock().map_err(|_| Error::StatePoisoned)?;
        Ok(if let Some(a) = &s.active {
            Status::Pending {
                request_id: a.id,
                stage: a.stage,
            }
        } else if let Some(v) = &s.snapshot {
            Status::Ready {
                request_id: v.request_id,
            }
        } else if let Some((id, e)) = &s.failure {
            Status::Failed {
                request_id: *id,
                error: e.clone(),
            }
        } else {
            Status::Idle
        })
    }
    pub fn snapshot(&self) -> Result<Option<Snapshot>, Error> {
        Ok(self
            .0
            .lock()
            .map_err(|_| Error::StatePoisoned)?
            .snapshot
            .clone())
    }
    pub fn stale_callbacks(&self) -> Result<u64, Error> {
        Ok(self
            .0
            .lock()
            .map_err(|_| Error::StatePoisoned)?
            .stale_callbacks)
    }
    /// Includes mapping buffers from failed/cancelled older requests.
    pub fn outstanding_bytes(&self) -> Result<u64, Error> {
        Ok(self
            .0
            .lock()
            .map_err(|_| Error::StatePoisoned)?
            .in_flight_bytes)
    }
    /// Set solely by querying the actual `RenderWorld` `PipelineCache`. The adapter
    /// additionally requires a complete source resolve and scoped view readiness.
    pub fn pipeline_ready(&self, view: Entity) -> Result<bool, Error> {
        let s = self.0.lock().map_err(|_| Error::StatePoisoned)?;
        Ok(s.pipeline_ready && s.selected_views.contains(&view))
    }
    pub fn cancel(&self) -> Result<(), Error> {
        let id = self
            .0
            .lock()
            .map_err(|_| Error::StatePoisoned)?
            .active
            .as_ref()
            .map(|a| a.id);
        if let Some(id) = id {
            self.fail(id, Error::Cancelled);
        }
        Ok(())
    }
    fn fail(&self, id: u64, error: Error) {
        if let Ok(mut s) = self.0.lock()
            && s.active.as_ref().is_some_and(|a| a.id == id)
        {
            s.active = None;
            s.failure = Some((id, error));
        }
    }
    fn receive(&self, ticket: Ticket, role: Role, result: Result<Vec<u8>, Error>, reserved: u64) {
        let Ok(mut s) = self.0.lock() else {
            return;
        };
        if let Some((expected_ticket, expected_bytes)) = s.outstanding.get(&(ticket.id, role)) {
            if *expected_ticket != ticket || *expected_bytes != reserved {
                s.stale_callbacks = s.stale_callbacks.saturating_add(1);
                return;
            }
            s.outstanding.remove(&(ticket.id, role));
            s.in_flight_bytes = s.in_flight_bytes.saturating_sub(reserved);
        } else {
            if let Some(a) = &mut s.active
                && a.id == ticket.id
            {
                a.error.get_or_insert(Error::DuplicateCallback);
            } else {
                s.stale_callbacks = s.stale_callbacks.saturating_add(1);
            }
            return;
        }
        let Some(a) = &mut s.active else {
            s.stale_callbacks = s.stale_callbacks.saturating_add(1);
            return;
        };
        if a.id != ticket.id
            || a.stage != Stage::Mapping
            || !a.metadata.as_ref().is_some_and(|m| ticket.matches(m))
        {
            s.stale_callbacks = s.stale_callbacks.saturating_add(1);
            return;
        }
        if !a.received.insert(role) {
            a.error.get_or_insert(Error::DuplicateCallback);
            return;
        }
        match result {
            Err(e) => {
                a.error.get_or_insert(e);
            }
            Ok(data) => match role {
                Role::Trace => {
                    a.trace = Some(Arc::new(data));
                }
                Role::Scratch => {
                    if let Some(plane) = &mut a.scratch {
                        plane.bytes = Arc::new(data);
                    } else {
                        a.error.get_or_insert(Error::InvalidLayout);
                    }
                }
            },
        }
        if a.received.len() != 2 {
            return;
        }
        let a = s.active.take().expect("Matched trace request");
        if let Some(error) = a.error {
            s.failure = Some((a.id, error));
            return;
        }
        let (Some(metadata), Some(original_texture_ids), Some(trace_bytes), Some(scratch)) =
            (a.metadata, a.texture_ids, a.trace, a.scratch)
        else {
            s.failure = Some((a.id, Error::InvalidLayout));
            return;
        };
        let snapshot = Snapshot {
            request_id: a.id,
            metadata,
            original_texture_ids,
            trace_bytes,
            scratch,
            sites: a.sites,
            selection_bytes: a.selection,
            diagnostic_draw_encoded: true,
        };
        if snapshot.complete() {
            s.snapshot = Some(snapshot);
        } else {
            s.failure = Some((a.id, Error::InvalidGpuRecord));
        }
    }
}

#[derive(Resource)]
struct Shaders(Handle<Shader>);
#[derive(Resource)]
pub struct Pipeline {
    layout: BindGroupLayoutDescriptor,
    id: CachedRenderPipelineId,
}
struct Job {
    ticket: Ticket,
    role: Role,
    buffer: Buffer,
    bytes: u64,
}
#[derive(Resource, Default)]
pub struct Jobs(Mutex<Vec<Job>>);

/// No activation/request is implicit. Uses Bevy's already installed `RenderApp`.
pub fn register(app: &mut App) -> Result<(), String> {
    if app.world().contains_resource::<Observation>() {
        return Ok(());
    }
    if app.get_sub_app(RenderApp).is_none() {
        return Err("Trace readback requires Bevy RenderApp".into());
    }
    resolve::register(app)?;
    let shader = app
        .world_mut()
        .resource_mut::<Assets<Shader>>()
        .add(Shader::from_wgsl(
            include_str!("trace.wgsl"),
            "forge://tidewater/temporal/trace.wgsl",
        ));
    let bus = Observation::default();
    app.insert_resource(bus.clone());
    app.sub_app_mut(RenderApp)
        .insert_resource(bus)
        .insert_resource(Shaders(shader))
        .init_resource::<Jobs>()
        .add_systems(RenderStartup, initialize.after(resolve::initialize))
        .add_systems(
            Render,
            refresh_pipeline_ready.in_set(RenderSystems::PrepareResources),
        )
        .add_systems(Render, map_after_submission.in_set(RenderSystems::Cleanup));
    Ok(())
}
#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy startup systems receive render resources by value"
)]
fn initialize(
    mut commands: Commands,
    shaders: Res<Shaders>,
    source: Res<resolve::Pipeline>,
    cache: Res<PipelineCache>,
) {
    let layout = BindGroupLayoutDescriptor::new(
        "Tidewater diagnostic selection/trace/original-output",
        &BindGroupLayoutEntries::sequential(
            ShaderStages::FRAGMENT,
            (
                uniform_buffer::<Selection>(false),
                BindingType::Buffer {
                    ty: BufferBindingType::Storage { read_only: false },
                    has_dynamic_offset: false,
                    min_binding_size: NonZeroU64::new(TRACE_BYTES as u64),
                },
                texture_2d(TextureSampleType::Float { filterable: true }),
            ),
        ),
    );
    let [inputs, uniforms] = source.trace_layouts();
    let id = cache.queue_render_pipeline(RenderPipelineDescriptor {
        label: Some("Tidewater separate instrumented source resolve".into()),
        layout: vec![inputs, uniforms, layout.clone()],
        vertex: VertexState {
            shader: shaders.0.clone(),
            entry_point: Some("vs".into()),
            ..default()
        },
        fragment: Some(FragmentState {
            shader: shaders.0.clone(),
            entry_point: Some("fs".into()),
            targets: vec![Some(ColorTargetState {
                format: TextureFormat::Rgba16Float,
                blend: None,
                write_mask: ColorWrites::ALL,
            })],
            ..default()
        }),
        ..default()
    });
    commands.insert_resource(Pipeline { layout, id });
}
#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy render systems receive resources and queries by value"
)]
fn refresh_pipeline_ready(
    bus: Res<Observation>,
    pipeline: Option<Res<Pipeline>>,
    cache: Res<PipelineCache>,
    views: Query<&MainEntity, With<resolve::ResolveEnabled>>,
) {
    if let Ok(mut state) = bus.0.lock() {
        state.pipeline_ready = pipeline
            .as_ref()
            .is_some_and(|p| cache.get_render_pipeline(p.id).is_some());
        state.selected_views = selected_main_views(&views);
    }
}

fn selected_main_views(
    views: &Query<&MainEntity, With<resolve::ResolveEnabled>>,
) -> HashSet<Entity> {
    views.iter().map(MainEntity::id).collect()
}

pub struct Inputs<'a> {
    /// EXACT groups used by the preceding real source resolve draw.
    pub texture_group: &'a BindGroup,
    pub uniform_group: &'a BindGroup,
    pub textures: [&'a Texture; 7],
}
/// Caller has already encoded a complete original source resolve. Its source,
/// HDR/Aux/owner/tag guards remain authoritative and are never bypassed here.
#[allow(
    clippy::too_many_arguments,
    clippy::needless_pass_by_value,
    clippy::too_many_lines,
    reason = "This ordered diagnostic hook owns the instrumented resolve transaction"
)]
pub fn after_resolve(
    observation: &Observation,
    jobs: &Jobs,
    pipeline: Option<&Pipeline>,
    cache: &PipelineCache,
    context: &mut RenderContext,
    metadata: Metadata,
    inputs: Inputs<'_>,
) -> Result<bool, Error> {
    let pending = observation
        .0
        .lock()
        .map_err(|_| Error::StatePoisoned)?
        .active
        .as_ref()
        .map(|a| (a.id, a.scope, a.stage, a.sites.clone()));
    let Some((id, scope, stage, sites)) = pending else {
        return Ok(false);
    };
    if scope.view != metadata.tag.view {
        return Ok(false);
    }
    // Mapping may legitimately finish on a later submitted frame. Never replace
    // its immutable metadata or fail it merely because another frame is drawn.
    if stage != Stage::Requested {
        return Ok(false);
    }
    let result = (|| {
        validate_metadata(&metadata)?;
        if !scope_accepts(scope, &metadata) {
            return Err(Error::WrongScope);
        }
        let selection = Selection::new(&sites, metadata.output_size)?.bytes()?;
        let (scratch_row, scratch_stride, scratch_bytes) = color_layout(metadata.output_size)?;
        let source_bytes = validate_textures(&metadata, &inputs.textures)?;
        let limits = context.render_device().limits();
        if limits.max_bind_groups < 3
            || limits.max_storage_buffers_per_shader_stage < 1
            || limits.max_storage_buffer_binding_size < TRACE_BYTES as u64
            || metadata.output_size.max_element() > limits.max_texture_dimension_2d
        {
            return Err(Error::UnsupportedDeviceLimits);
        }
        let pipeline = pipeline.ok_or(Error::PipelinePending)?;
        let render_pipeline = cache
            .get_render_pipeline(pipeline.id)
            .ok_or(Error::PipelinePending)?;
        let extras = scratch_bytes
            .checked_add(TRACE_BYTES as u64)
            .ok_or(Error::TooManyBytes)?;
        {
            let s = observation.0.lock().map_err(|_| Error::StatePoisoned)?;
            candidate_budget(s.in_flight_bytes, extras, source_bytes)?;
        }
        let device = context.render_device();
        let size = metadata.output_size;
        let scratch = device.create_texture(&TextureDescriptor {
            label: Some("Tidewater trace private scratch"),
            size: Extent3d {
                width: size.x,
                height: size.y,
                depth_or_array_layers: 1,
            },
            mip_level_count: 1,
            sample_count: 1,
            dimension: TextureDimension::D2,
            format: TextureFormat::Rgba16Float,
            usage: TextureUsages::RENDER_ATTACHMENT | TextureUsages::COPY_SRC,
            view_formats: &[],
        });
        if inputs.textures.iter().any(|t| t.id() == scratch.id()) {
            return Err(Error::AliasedTexture);
        }
        let scratch_view = scratch.create_view(&default());
        let selection_buffer = device.create_buffer_with_data(&BufferInitDescriptor {
            label: Some("Tidewater trace selection144"),
            contents: &selection,
            usage: BufferUsages::UNIFORM,
        });
        let storage = device.create_buffer(&BufferDescriptor {
            label: Some("Tidewater trace real float32 storage"),
            size: TRACE_BYTES as u64,
            usage: BufferUsages::STORAGE | BufferUsages::COPY_SRC,
            mapped_at_creation: false,
        });
        let original_view = inputs.textures[6].create_view(&default());
        let diagnostic_group = device.create_bind_group(
            "Tidewater trace only resources",
            &cache.get_bind_group_layout(&pipeline.layout),
            &BindGroupEntries::sequential((
                selection_buffer.as_entire_binding(),
                storage.as_entire_binding(),
                &original_view,
            )),
        );
        let trace_staging = staging(device, TRACE_BYTES as u64);
        let scratch_staging = staging(device, scratch_bytes);
        let ticket = Ticket {
            id,
            tag: metadata.tag,
            epoch: metadata.history_epoch,
            resolve_generation: metadata.resolve_generation,
            auxiliary_generation: metadata.auxiliary_generation,
        };
        let scratch_plane = resolve_readback::Plane {
            role: resolve_readback::Role::Output,
            format: TextureFormat::Rgba16Float,
            size,
            texture_id: format!("{:?}", scratch.id()),
            row_bytes: scratch_row,
            bytes_per_row: scratch_stride,
            bytes: Arc::new(Vec::new()),
        };
        let mut job_queue = jobs.0.lock().map_err(|_| Error::StatePoisoned)?;
        {
            let mut s = observation.0.lock().map_err(|_| Error::StatePoisoned)?;
            let a = s
                .active
                .as_mut()
                .filter(|a| a.id == id && a.stage == Stage::Requested)
                .ok_or(Error::WrongTicket)?;
            a.metadata = Some(metadata);
            a.selection = selection;
            a.texture_ids = Some(inputs.textures.map(|t| format!("{:?}", t.id())));
            a.scratch = Some(scratch_plane);
            a.stage = Stage::Encoded;
            s.in_flight_bytes = s
                .in_flight_bytes
                .checked_add(extras)
                .ok_or(Error::TooManyBytes)?;
            s.outstanding.extend([
                ((id, Role::Trace), (ticket, TRACE_BYTES as u64)),
                ((id, Role::Scratch), (ticket, scratch_bytes)),
            ]);
        }
        {
            let attachments = [Some(RenderPassColorAttachment {
                view: &scratch_view,
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
                    label: Some("Tidewater diagnostic private resolve, original untouched"),
                    color_attachments: &attachments,
                    ..default()
                });
            pass.set_pipeline(render_pipeline);
            pass.set_bind_group(0, inputs.texture_group, &[]);
            pass.set_bind_group(1, inputs.uniform_group, &[]);
            pass.set_bind_group(2, &diagnostic_group, &[]);
            pass.draw(0..3, 0..1);
        }
        context.command_encoder().copy_buffer_to_buffer(
            &storage,
            0,
            &trace_staging,
            0,
            TRACE_BYTES as u64,
        );
        context.command_encoder().copy_texture_to_buffer(
            scratch.as_image_copy(),
            TexelCopyBufferInfo {
                buffer: &scratch_staging,
                layout: TexelCopyBufferLayout {
                    offset: 0,
                    bytes_per_row: Some(scratch_stride),
                    rows_per_image: Some(size.y),
                },
            },
            scratch.size(),
        );
        job_queue.extend([
            Job {
                ticket,
                role: Role::Trace,
                buffer: trace_staging,
                bytes: TRACE_BYTES as u64,
            },
            Job {
                ticket,
                role: Role::Scratch,
                buffer: scratch_staging,
                bytes: scratch_bytes,
            },
        ]);
        Ok(true)
    })();
    if let Err(error) = &result {
        observation.fail(id, error.clone());
    }
    result
}
fn staging(device: &RenderDevice, bytes: u64) -> Buffer {
    device.create_buffer(&BufferDescriptor {
        label: Some("Tidewater diagnostic post-submit staging"),
        size: bytes,
        usage: BufferUsages::COPY_DST | BufferUsages::MAP_READ,
        mapped_at_creation: false,
    })
}
#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy Cleanup systems receive resource wrappers by value"
)]
fn map_after_submission(jobs: Res<Jobs>, observation: Res<Observation>) {
    let mut ready = if let Ok(mut jobs) = jobs.0.lock() {
        std::mem::take(&mut *jobs)
    } else {
        if let Ok(s) = observation.0.lock() {
            let id = s.active.as_ref().map(|a| a.id);
            drop(s);
            if let Some(id) = id {
                observation.fail(id, Error::StatePoisoned);
            }
        }
        return;
    };
    let transition = observation.0.lock().ok().and_then(|mut s| {
        let a = s.active.as_mut()?;
        match a.stage {
            Stage::Encoded if ready.iter().filter(|j| j.ticket.id == a.id).count() == 2 => {
                a.stage = Stage::Mapping;
                None
            }
            Stage::Requested | Stage::Encoded => Some((a.id, Error::MissingFrame)),
            Stage::Mapping => None,
        }
    });
    if let Some((id, error)) = transition {
        observation.fail(id, error);
    }
    for job in ready.drain(..) {
        let buffer = job.buffer.clone();
        let bus = (*observation).clone();
        job.buffer
            .slice(..)
            .map_async(MapMode::Read, move |mapped| {
                let result = mapped
                    .map_err(|e| Error::MapFailed(e.to_string()))
                    .and_then(|()| {
                        let data = buffer.slice(..).get_mapped_range();
                        let result = if data.len() as u64 == job.bytes {
                            Ok(data.to_vec())
                        } else {
                            Err(Error::InvalidLayout)
                        };
                        drop(data);
                        buffer.unmap();
                        result
                    });
                bus.receive(job.ticket, job.role, result, job.bytes);
            });
    }
}

fn source_roles() -> [resolve_readback::Role; 7] {
    [
        resolve_readback::Role::Beauty,
        resolve_readback::Role::InputHistory,
        resolve_readback::Role::PreviousSourceDepth,
        resolve_readback::Role::CurrentDepth,
        resolve_readback::Role::Velocity,
        resolve_readback::Role::Mask,
        resolve_readback::Role::Output,
    ]
}
fn scope_accepts(scope: Scope, m: &Metadata) -> bool {
    scope.view == m.tag.view
        && m.tag.serial >= scope.minimum_serial
        && scope.history_epoch == m.history_epoch
        && scope
            .expected_resolve_generation
            .is_none_or(|g| g == m.resolve_generation)
        && scope
            .expected_auxiliary_generation
            .is_none_or(|g| g == m.auxiliary_generation)
}
fn validate_metadata(m: &Metadata) -> Result<(), Error> {
    if m.input_size != m.output_size {
        return Err(Error::UnsupportedScale);
    }
    let epoch =
        u64::from(m.packet.phase_epoch_seed.y) | (u64::from(m.packet.phase_epoch_seed.z) << 32);
    if m.input_size.min_element() == 0
        || m.resolve_generation == 0
        || m.auxiliary_generation == 0
        || m.tag.serial == 0
        || m.packet.sizes
            != UVec4::new(
                m.input_size.x,
                m.input_size.y,
                m.output_size.x,
                m.output_size.y,
            )
        || epoch != m.history_epoch
        || m.packet.phase_epoch_seed.x >= 32
        || m.history_seeded != m.previous_depth_cleared
    {
        return Err(Error::InvalidMetadata);
    }
    m.uniform_bytes().map_err(|_| Error::InvalidMetadata)?;
    Ok(())
}
fn same_metadata(a: &Metadata, b: &Metadata) -> Result<bool, Error> {
    Ok(a.tag == b.tag
        && a.history_epoch == b.history_epoch
        && a.resolve_generation == b.resolve_generation
        && a.auxiliary_generation == b.auxiliary_generation
        && a.input_size == b.input_size
        && a.output_size == b.output_size
        && a.history_seeded == b.history_seeded
        && a.previous_depth_cleared == b.previous_depth_cleared
        && a.frames_encoded_before == b.frames_encoded_before
        && a.uniform_bytes().map_err(|_| Error::InvalidMetadata)?
            == b.uniform_bytes().map_err(|_| Error::InvalidMetadata)?)
}
fn validate_sites(sites: &[Site], size: Option<UVec2>) -> Result<(), Error> {
    if sites.is_empty() || sites.len() > MAX_SITES || sites.iter().any(|s| s.case_id > 2) {
        return Err(Error::InvalidSites);
    }
    if sites
        .iter()
        .map(|s| (s.x, s.y))
        .collect::<HashSet<_>>()
        .len()
        != sites.len()
    {
        return Err(Error::DuplicateSite);
    }
    if size.is_some_and(|n| n.min_element() == 0 || sites.iter().any(|s| s.x >= n.x || s.y >= n.y))
    {
        return Err(Error::SiteOutOfBounds);
    }
    Ok(())
}
fn color_layout(size: UVec2) -> Result<(u32, u32, u64), Error> {
    if size.min_element() == 0 {
        return Err(Error::InvalidLayout);
    }
    let row = size.x.checked_mul(8).ok_or(Error::InvalidLayout)?;
    let stride = row.checked_add(255).ok_or(Error::InvalidLayout)? / 256 * 256;
    Ok((
        row,
        stride,
        u64::from(stride)
            .checked_mul(u64::from(size.y))
            .ok_or(Error::TooManyBytes)?,
    ))
}
// Conservative single-candidate staging payload: source seven planes, private
// scratch/trace, selection and our outstanding maps. This is not a global budget
// of all observers/GPU allocations. The adapter must wait its prior nine callbacks
// and outstanding_bytes()==0 before arming the next candidate; no FAIL retry.
fn candidate_budget(in_flight: u64, extras: u64, source: u64) -> Result<(), Error> {
    if in_flight
        .checked_add(extras)
        .and_then(|n| n.checked_add(source))
        .and_then(|n| n.checked_add(144))
        .is_none_or(|n| n > MAX_BYTES)
    {
        return Err(Error::TooManyBytes);
    }
    Ok(())
}
fn validate_textures(m: &Metadata, textures: &[&Texture; 7]) -> Result<u64, Error> {
    if textures
        .iter()
        .map(|t| t.id())
        .collect::<HashSet<_>>()
        .len()
        != 7
    {
        return Err(Error::AliasedTexture);
    }
    let mut total = 0_u64;
    for (role, t) in source_roles().iter().zip(textures) {
        let size = if matches!(
            role,
            resolve_readback::Role::InputHistory | resolve_readback::Role::Output
        ) {
            m.output_size
        } else {
            m.input_size
        };
        if t.width() != size.x
            || t.height() != size.y
            || t.depth_or_array_layers() != 1
            || t.sample_count() != 1
            || t.format() != role.format()
            || !t
                .usage()
                .contains(TextureUsages::TEXTURE_BINDING | TextureUsages::COPY_SRC)
        {
            return Err(Error::InvalidTexture);
        }
        let row = size
            .x
            .checked_mul(role.pixel_bytes())
            .ok_or(Error::InvalidLayout)?;
        let stride = row.checked_add(255).ok_or(Error::InvalidLayout)? / 256 * 256;
        total = total
            .checked_add(u64::from(stride) * u64::from(size.y))
            .ok_or(Error::TooManyBytes)?;
    }
    Ok(total)
}
#[allow(
    clippy::float_cmp,
    clippy::cast_precision_loss,
    reason = "The GPU trace ABI uses exact f32 sentinels and bounded integer IDs"
)]
fn validate_records(bytes: &[u8], sites: &[Site]) -> Result<(), Error> {
    validate_sites(sites, None)?;
    if bytes.len() != TRACE_BYTES {
        return Err(Error::InvalidLayout);
    }
    for (i, site) in sites.iter().enumerate() {
        let read = |offset: usize| {
            f32::from_le_bytes(
                bytes[offset..offset + 4]
                    .try_into()
                    .expect("Checked record layout"),
            )
        };
        let base = i * RECORD_BYTES;
        if read(base) != site.x as f32 + 0.5
            || read(base + 4) != site.y as f32 + 0.5
            || read(base + 63 * 16) != 1.0
            || read(base + 63 * 16 + 4) != 1.0
            || read(base + 63 * 16 + 8) != i as f32
            || read(base + 63 * 16 + 12) != site.case_id as f32
        {
            return Err(Error::InvalidGpuRecord);
        }
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::super::{frame::Packet, resolve::Params};
    use super::*;
    fn metadata() -> Metadata {
        Metadata {
            tag: FrameTag {
                view: Entity::from_raw_u32(1).unwrap(),
                serial: 17,
            },
            history_epoch: 4,
            resolve_generation: 2,
            auxiliary_generation: 3,
            input_size: UVec2::ONE,
            output_size: UVec2::ONE,
            history_seeded: true,
            previous_depth_cleared: true,
            frames_encoded_before: 0,
            packet: Packet {
                current_unjittered_clip_from_world: Mat4::IDENTITY,
                previous_unjittered_clip_from_world: Mat4::IDENTITY,
                current_world_from_jittered_clip: Mat4::IDENTITY,
                previous_world_from_jittered_clip: Mat4::IDENTITY,
                camera_position: Vec4::W,
                previous_camera_position: Vec4::W,
                sizes: UVec4::ONE,
                near_far_jitter: Vec4::new(0.1, 1000.0, 0.0, 0.0),
                phase_epoch_seed: UVec4::new(0, 4, 0, 1),
            },
            params: Params::source_scale_one(1000.0),
        }
    }
    fn scope() -> Scope {
        Scope {
            view: metadata().tag.view,
            minimum_serial: 17,
            history_epoch: 4,
            expected_resolve_generation: Some(2),
            expected_auxiliary_generation: Some(3),
        }
    }
    fn sites() -> Vec<Site> {
        vec![Site {
            x: 0,
            y: 0,
            case_id: 0,
        }]
    }
    fn trace() -> Vec<u8> {
        let mut bytes = vec![0; TRACE_BYTES];
        for (offset, value) in [(0, 0.5_f32), (4, 0.5), (63 * 16, 1.0), (63 * 16 + 4, 1.0)] {
            bytes[offset..offset + 4].copy_from_slice(&value.to_le_bytes());
        }
        bytes
    }
    fn plane(role: resolve_readback::Role) -> resolve_readback::Plane {
        resolve_readback::Plane {
            role,
            format: role.format(),
            size: UVec2::ONE,
            texture_id: role.name().into(),
            row_bytes: role.pixel_bytes(),
            bytes_per_row: 256,
            bytes: Arc::new(vec![0; 256]),
        }
    }
    fn mapping() -> (Observation, Ticket) {
        let bus = Observation::default();
        let id = bus.request(scope(), sites()).unwrap();
        let m = metadata();
        let ticket = Ticket {
            id,
            tag: m.tag,
            epoch: m.history_epoch,
            resolve_generation: m.resolve_generation,
            auxiliary_generation: m.auxiliary_generation,
        };
        let mut s = bus.0.lock().unwrap();
        let a = s.active.as_mut().unwrap();
        a.stage = Stage::Mapping;
        a.metadata = Some(m);
        a.texture_ids = Some(source_roles().map(|r| r.name().into()));
        a.selection = Selection::new(&sites(), UVec2::ONE)
            .unwrap()
            .bytes()
            .unwrap();
        a.scratch = Some(plane(resolve_readback::Role::Output));
        s.in_flight_bytes = TRACE_BYTES as u64 + 256;
        s.outstanding.extend([
            ((id, Role::Trace), (ticket, TRACE_BYTES as u64)),
            ((id, Role::Scratch), (ticket, 256)),
        ]);
        drop(s);
        (bus, ticket)
    }
    fn complete() -> Snapshot {
        let (bus, ticket) = mapping();
        bus.receive(ticket, Role::Trace, Ok(trace()), TRACE_BYTES as u64);
        bus.receive(ticket, Role::Scratch, Ok(vec![0; 256]), 256);
        bus.snapshot().unwrap().unwrap()
    }
    #[test]
    fn selection_is_exact_144_byte_output_abi_and_rejects_duplicate_sites() {
        let chosen = [Site {
            x: 1919,
            y: 1079,
            case_id: 2,
        }];
        let bytes = Selection::new(&chosen, UVec2::new(1920, 1080))
            .unwrap()
            .bytes()
            .unwrap();
        let words: Vec<u32> = bytes
            .as_chunks::<4>()
            .0
            .iter()
            .map(|b| u32::from_le_bytes(*b))
            .collect();
        assert_eq!(&words[..8], &[1, 64, 1920, 1080, 1919, 1079, 2, 0]);
        assert!(words[8..].iter().all(|v| *v == 0));
        assert_eq!(
            validate_sites(&[chosen[0], chosen[0]], None),
            Err(Error::DuplicateSite)
        );
        assert_eq!(
            Selection::new(&chosen, UVec2::ONE).unwrap_err(),
            Error::SiteOutOfBounds
        );
    }
    #[test]
    fn exact_scope_rejects_stale_tag_epoch_and_both_generations() {
        let m = metadata();
        assert!(scope_accepts(scope(), &m));
        for mutate in [0, 1, 2, 3, 4] {
            let mut wrong = m.clone();
            match mutate {
                0 => wrong.tag.serial -= 1,
                1 => wrong.history_epoch += 1,
                2 => wrong.resolve_generation += 1,
                3 => wrong.auxiliary_generation += 1,
                _ => wrong.tag.view = Entity::from_raw_u32(2).unwrap(),
            }
            assert!(!scope_accepts(scope(), &wrong));
        }
        let mut resized = m.clone();
        resized.output_size = UVec2::new(2, 1);
        assert_eq!(validate_metadata(&resized), Err(Error::UnsupportedScale));
        let mut epoch = m;
        epoch.packet.phase_epoch_seed.y += 1;
        assert_eq!(validate_metadata(&epoch), Err(Error::InvalidMetadata));
    }
    #[test]
    fn two_unique_callbacks_required_and_outstanding_release_exact() {
        let (bus, ticket) = mapping();
        bus.receive(ticket, Role::Trace, Ok(trace()), TRACE_BYTES as u64);
        assert!(bus.snapshot().unwrap().is_none());
        assert_eq!(bus.outstanding_bytes().unwrap(), 256);
        bus.receive(ticket, Role::Scratch, Ok(vec![0; 256]), 256);
        assert!(bus.snapshot().unwrap().unwrap().complete());
        assert_eq!(bus.outstanding_bytes().unwrap(), 0);
    }
    #[test]
    fn stale_ticket_cannot_consume_current_mapping_reservation() {
        let (bus, ticket) = mapping();
        let mut wrong = ticket;
        wrong.epoch += 1;
        let before = bus.outstanding_bytes().unwrap();
        bus.receive(wrong, Role::Trace, Ok(trace()), TRACE_BYTES as u64);
        assert_eq!(bus.outstanding_bytes().unwrap(), before);
        assert_eq!(bus.stale_callbacks().unwrap(), 1);
        bus.receive(ticket, Role::Trace, Ok(trace()), TRACE_BYTES as u64);
        bus.receive(ticket, Role::Scratch, Ok(vec![0; 256]), 256);
        assert!(bus.snapshot().unwrap().unwrap().complete());
    }
    #[test]
    fn duplicate_callback_fails_without_releasing_other_reservation() {
        let (bus, ticket) = mapping();
        bus.receive(ticket, Role::Trace, Ok(trace()), TRACE_BYTES as u64);
        bus.receive(ticket, Role::Trace, Ok(trace()), TRACE_BYTES as u64);
        assert_eq!(bus.outstanding_bytes().unwrap(), 256);
        bus.receive(ticket, Role::Scratch, Ok(vec![0; 256]), 256);
        assert!(matches!(
            bus.status().unwrap(),
            Status::Failed {
                error: Error::DuplicateCallback,
                ..
            }
        ));
        assert!(bus.snapshot().unwrap().is_none());
        assert_eq!(bus.outstanding_bytes().unwrap(), 0);
    }
    #[test]
    fn cancelled_callback_releases_only_its_old_budget() {
        let (bus, ticket) = mapping();
        bus.cancel().unwrap();
        let id = bus.request(scope(), sites()).unwrap();
        bus.receive(ticket, Role::Trace, Ok(trace()), TRACE_BYTES as u64);
        assert_eq!(bus.outstanding_bytes().unwrap(), 256);
        bus.receive(ticket, Role::Scratch, Ok(vec![0; 256]), 256);
        assert!(
            matches!(bus.status().unwrap(), Status::Pending { request_id, stage: Stage::Requested } if request_id == id)
        );
        assert_eq!(bus.outstanding_bytes().unwrap(), 0);
        assert!(bus.snapshot().unwrap().is_none());
    }
    #[test]
    fn raw_nan_and_signed_zero_bits_preserved_but_missing_marker_refused() {
        let mut snapshot = complete();
        let mut data = (*snapshot.trace_bytes).clone();
        data[16..20].copy_from_slice(&0x7fc0_1234_u32.to_le_bytes());
        data[20..24].copy_from_slice(&0x8000_0000_u32.to_le_bytes());
        snapshot.trace_bytes = Arc::new(data);
        assert!(snapshot.complete());
        assert_eq!(
            &snapshot.record_float_bits(0).unwrap()[4..6],
            &[0x7fc0_1234, 0x8000_0000]
        );
        let mut incomplete = (*snapshot.trace_bytes).clone();
        incomplete[63 * 16 + 4..63 * 16 + 8].fill(0);
        snapshot.trace_bytes = Arc::new(incomplete);
        assert!(!snapshot.complete());
    }
    #[test]
    fn byte_gate_reports_numeric_difference_without_losing_snapshots() {
        let snapshot = complete();
        let mut original = resolve_readback::Snapshot {
            request_id: 99,
            metadata: metadata(),
            planes: source_roles().into_iter().map(plane).collect(),
            resolve_draw_encoded: true,
        };
        assert_eq!(
            snapshot.compare_original(&original).unwrap(),
            Comparison {
                byte_exact: true,
                compared_bytes: 8,
                mismatching_bytes: 0,
                mismatching_pixels: 0
            }
        );
        let out = original
            .planes
            .iter_mut()
            .find(|p| p.role == resolve_readback::Role::Output)
            .unwrap();
        let mut data = (*out.bytes).clone();
        data[3] = 1;
        out.bytes = Arc::new(data);
        assert_eq!(
            snapshot.compare_original(&original).unwrap(),
            Comparison {
                byte_exact: false,
                compared_bytes: 8,
                mismatching_bytes: 1,
                mismatching_pixels: 1
            }
        );
        assert!(snapshot.complete() && original.complete());
        original.planes[0].format = TextureFormat::Rgba8Unorm;
        assert_eq!(
            snapshot.compare_original(&original),
            Err(Error::SourceSnapshotMismatch)
        );
    }
    #[test]
    fn uniform_byte_drift_is_a_join_error_not_numeric_output_difference() {
        let snapshot = complete();
        let mut m = metadata();
        m.params.frame_weight = 0.07;
        let original = resolve_readback::Snapshot {
            request_id: 99,
            metadata: m,
            planes: source_roles().into_iter().map(plane).collect(),
            resolve_draw_encoded: true,
        };
        assert_eq!(
            snapshot.compare_original(&original),
            Err(Error::SourceSnapshotMismatch)
        );
    }
    #[test]
    fn odd_padded_layout_and_budget_never_overflow_or_ignore_outstanding() {
        assert_eq!(color_layout(UVec2::new(33, 3)).unwrap(), (264, 512, 1536));
        assert_eq!(color_layout(UVec2::ZERO), Err(Error::InvalidLayout));
        assert_eq!(
            color_layout(UVec2::new(u32::MAX, 1)),
            Err(Error::InvalidLayout)
        );
        assert!(candidate_budget(0, 8192, MAX_BYTES - 8192 - 144).is_ok());
        assert_eq!(
            candidate_budget(1, 8192, MAX_BYTES - 8192 - 144),
            Err(Error::TooManyBytes)
        );
        assert_eq!(
            candidate_budget(u64::MAX, 8192, 0),
            Err(Error::TooManyBytes)
        );
    }
    #[test]
    fn ready_requires_real_pipeline_flag_and_selected_view() {
        let bus = Observation::default();
        let view = metadata().tag.view;
        assert!(!bus.pipeline_ready(view).unwrap());
        {
            let mut state = bus.0.lock().unwrap();
            state.pipeline_ready = true;
        }
        assert!(!bus.pipeline_ready(view).unwrap());
        {
            let mut state = bus.0.lock().unwrap();
            state.selected_views.insert(view);
        }
        assert!(bus.pipeline_ready(view).unwrap());
        assert!(
            !bus.pipeline_ready(Entity::from_raw_u32(2).unwrap())
                .unwrap()
        );
    }
    #[test]
    fn selected_render_view_maps_to_its_distinct_main_entity_without_gpu_readiness() {
        use bevy::ecs::system::RunSystemOnce;
        let mut main_world = World::new();
        let main_view = main_world.spawn_empty().id();
        let unselected_main = main_world.spawn_empty().id();
        let mut render_world = World::new();
        render_world.spawn_empty();
        let render_view = render_world
            .spawn((resolve::ResolveEnabled, MainEntity::from(main_view)))
            .id();
        assert_ne!(main_view, render_view);
        render_world.spawn(MainEntity::from(unselected_main));
        render_world.spawn(resolve::ResolveEnabled); // no MainEntity mapping
        let bus = Observation::default();
        render_world.insert_resource(bus.clone());
        render_world
            .run_system_once(
                |views: Query<&MainEntity, With<resolve::ResolveEnabled>>,
                 bus: Res<Observation>| {
                    bus.0.lock().unwrap().selected_views = selected_main_views(&views);
                },
            )
            .unwrap();
        let state = bus.0.lock().unwrap();
        assert_eq!(state.selected_views, HashSet::from([main_view]));
        assert!(!state.selected_views.contains(&render_view));
        assert!(!state.selected_views.contains(&unselected_main));
        assert!(!state.pipeline_ready); // no PipelineCache/device was supplied
        drop(state);
        assert!(!bus.pipeline_ready(main_view).unwrap());
        assert!(!bus.pipeline_ready(render_view).unwrap());
    }
}
