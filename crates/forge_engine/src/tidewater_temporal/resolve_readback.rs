// SPDX-License-Identifier: MIT
//! Explicit opt-in readback hooks for the source temporal resolve.
//!
//! Call `before_resolve` AFTER the optional bilinear history seed and zero-depth
//! clear, immediately BEFORE the resolve draw. Call `after_resolve` immediately
//! AFTER that draw, BEFORE previous-depth/HDR overwrite. The seven asynchronous
//! callbacks are required; command encoding alone never makes a snapshot ready.
//! This module does not acquire ResolveTargets/history/acknowledgement locks.
//! Source algorithm attribution: Tidewater commit
//! 1438b1abfcaee3267092b75573014f4d9b4a983c (see `third_party/tidewater/LICENSE`).

use super::{
    frame::{FrameTag, Packet},
    resolve::Params,
};
use bevy::{
    prelude::*,
    render::{
        Render, RenderApp, RenderSystems,
        render_resource::{
            Buffer, BufferDescriptor, BufferUsages, MapMode, TexelCopyBufferInfo,
            TexelCopyBufferLayout, Texture, TextureAspect, TextureFormat, TextureUsages, encase,
        },
        renderer::RenderContext,
    },
};
use std::{
    collections::HashSet,
    fmt,
    fmt::Write as _,
    fs::{self, OpenOptions},
    io::Write,
    path::Path,
    sync::{Arc, Mutex},
};

pub const PLANES: usize = 7;
pub const MAX_BYTES: u64 = 512 * 1024 * 1024;

#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub enum Role {
    Beauty,
    InputHistory,
    PreviousSourceDepth,
    CurrentDepth,
    Velocity,
    Mask,
    Output,
}

impl Role {
    #[must_use]
    pub const fn name(self) -> &'static str {
        match self {
            Self::Beauty => "beauty-rgba16f",
            Self::InputHistory => "input-history-rgba16f",
            Self::PreviousSourceDepth => "previous-source-depth-depth32f",
            Self::CurrentDepth => "current-depth-depth32f",
            Self::Velocity => "velocity-rgba16f",
            Self::Mask => "mask-rgba8unorm",
            Self::Output => "output-rgba16f",
        }
    }

    #[must_use]
    pub const fn format(self) -> TextureFormat {
        match self {
            Self::Beauty | Self::InputHistory | Self::Velocity | Self::Output => {
                TextureFormat::Rgba16Float
            }
            Self::PreviousSourceDepth | Self::CurrentDepth => TextureFormat::Depth32Float,
            Self::Mask => TextureFormat::Rgba8Unorm,
        }
    }

    #[must_use]
    pub const fn pixel_bytes(self) -> u32 {
        match self {
            Self::Beauty | Self::InputHistory | Self::Velocity | Self::Output => 8,
            Self::PreviousSourceDepth | Self::CurrentDepth | Self::Mask => 4,
        }
    }
}

/// Immutable copies of the actual bound uniforms and owned history state.
/// `history_seeded` describes the actual seed draw, not an inferred CPU promise.
#[derive(Clone, Debug)]
pub struct Metadata {
    pub tag: FrameTag,
    pub history_epoch: u64,
    pub resolve_generation: u64,
    pub auxiliary_generation: u64,
    pub input_size: UVec2,
    pub output_size: UVec2,
    pub history_seeded: bool,
    pub previous_depth_cleared: bool,
    pub frames_encoded_before: u64,
    pub packet: Packet,
    pub params: Params,
}

impl Metadata {
    fn validate(&self) -> Result<(), Error> {
        let epoch = u64::from(self.packet.phase_epoch_seed.y)
            | (u64::from(self.packet.phase_epoch_seed.z) << 32);
        if self.resolve_generation == 0 || self.auxiliary_generation == 0
            || self.tag.serial == 0 || self.input_size.min_element() == 0
            || self.output_size != self.input_size // This candidate is source scale=1 only.
            || self.packet.sizes != UVec4::new(self.input_size.x, self.input_size.y, self.output_size.x, self.output_size.y)
            || epoch != self.history_epoch || self.packet.phase_epoch_seed.x >= 32
            || self.history_seeded != self.previous_depth_cleared
        {
            return Err(Error::InvalidMetadata);
        }
        Ok(())
    }

    /// Exact encase bytes, including ABI padding, of the uniforms used by the draw.
    pub fn uniform_bytes(&self) -> Result<(Vec<u8>, Vec<u8>), Error> {
        let mut packet = encase::UniformBuffer::new(Vec::new());
        packet
            .write(&self.packet)
            .map_err(|error| Error::UniformEncoding(error.to_string()))?;
        let mut params = encase::UniformBuffer::new(Vec::new());
        params
            .write(&self.params)
            .map_err(|error| Error::UniformEncoding(error.to_string()))?;
        let packet = packet.into_inner();
        let params = params.into_inner();
        if packet.len() != 336 || params.len() != 32 {
            return Err(Error::InvalidMetadata);
        }
        Ok((packet, params))
    }
}

/// Caller must select the active main camera under `EncodingOwner::Resolve`.
/// An absent expected generation is deliberate for a first allocation/resize;
/// the captured generation must still be nonzero and identical across both hooks.
#[derive(Clone, Copy, Debug)]
pub struct Scope {
    pub view: Entity,
    pub minimum_serial: u64,
    pub history_epoch: u64,
    pub expected_resolve_generation: Option<u64>,
    pub expected_auxiliary_generation: Option<u64>,
}

impl Scope {
    fn accepts(self, metadata: &Metadata) -> bool {
        self.view == metadata.tag.view
            && metadata.tag.serial >= self.minimum_serial
            && self.history_epoch == metadata.history_epoch
            && self
                .expected_resolve_generation
                .is_none_or(|value| value == metadata.resolve_generation)
            && self
                .expected_auxiliary_generation
                .is_none_or(|value| value == metadata.auxiliary_generation)
    }
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub enum Error {
    Busy,
    StatePoisoned,
    CounterExhausted,
    MissingFrame,
    MissingAfterHook,
    WrongScope,
    InvalidMetadata,
    InvalidTexture,
    InvalidLayout,
    AliasedTexture,
    TooManyBytes,
    WrongTicket,
    DuplicateRole,
    MapFailed(String),
    UniformEncoding(String),
}
impl fmt::Display for Error {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(formatter, "Temporal resolve readback: {self:?}")
    }
}
impl std::error::Error for Error {}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Stage {
    Requested,
    InputsEncoded,
    Paired,
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
pub struct Plane {
    pub role: Role,
    pub format: TextureFormat,
    pub size: UVec2,
    pub texture_id: String,
    pub row_bytes: u32,
    pub bytes_per_row: u32,
    /// Real mapped GPU bytes, with 256-byte row padding still retained.
    pub bytes: Arc<Vec<u8>>,
}

impl Plane {
    pub fn unpadded_bytes(&self) -> Result<Vec<u8>, Error> {
        let (row, stride, total) = layout(self.role, self.size)?;
        if row != self.row_bytes
            || stride != self.bytes_per_row
            || u64::try_from(self.bytes.len()).ok() != Some(total)
        {
            return Err(Error::InvalidLayout);
        }
        let row = usize::try_from(row).map_err(|_| Error::InvalidLayout)?;
        let stride = usize::try_from(stride).map_err(|_| Error::InvalidLayout)?;
        let capacity = row
            .checked_mul(usize::try_from(self.size.y).map_err(|_| Error::InvalidLayout)?)
            .ok_or(Error::InvalidLayout)?;
        let mut result = Vec::with_capacity(capacity);
        for padded in self.bytes.chunks_exact(stride) {
            result.extend_from_slice(&padded[..row]);
        }
        Ok(result)
    }
}

#[derive(Clone, Debug)]
pub struct Snapshot {
    pub request_id: u64,
    pub metadata: Metadata,
    pub planes: Vec<Plane>,
    /// True only if the after hook was paired with this exact before hook.
    pub resolve_draw_encoded: bool,
}
impl Snapshot {
    #[must_use]
    pub fn complete(&self) -> bool {
        self.resolve_draw_encoded
            && self.planes.len() == PLANES
            && self
                .planes
                .iter()
                .map(|plane| plane.role)
                .collect::<HashSet<_>>()
                .len()
                == PLANES
            && self
                .planes
                .iter()
                .all(|plane| plane.unpadded_bytes().is_ok())
    }

    /// Writes only a NEW directory and files. Existing paths are never replaced.
    /// This is called explicitly after readiness, never inside a GPU callback.
    /// A partial I/O failure remains visible in the newly created directory.
    pub fn write_new_directory(&self, path: &Path) -> std::io::Result<()> {
        if !self.complete() {
            return Err(std::io::Error::other("Incomplete seven-plane GPU snapshot"));
        }
        let (packet, params) = self
            .metadata
            .uniform_bytes()
            .map_err(std::io::Error::other)?;
        fs::create_dir(path)?;
        let create = |name: &str, bytes: &[u8]| -> std::io::Result<()> {
            let mut file = OpenOptions::new()
                .write(true)
                .create_new(true)
                .open(path.join(name))?;
            file.write_all(bytes)?;
            file.sync_all()
        };
        for plane in &self.planes {
            create(&format!("{}.padded.bin", plane.role.name()), &plane.bytes)?;
            create(
                &format!("{}.bin", plane.role.name()),
                &plane.unpadded_bytes().map_err(std::io::Error::other)?,
            )?;
        }
        create("packet336.bin", &packet)?;
        create("params32.bin", &params)?;
        let mut text = format!(
            "request_id={}\nresolve_draw_encoded={}\n{:?}\n",
            self.request_id, self.resolve_draw_encoded, self.metadata
        );
        for plane in &self.planes {
            writeln!(
                &mut text,
                "role={} format={:?} size={:?} texture_id={} row_bytes={} bytes_per_row={} mapped_bytes={}",
                plane.role.name(),
                plane.format,
                plane.size,
                plane.texture_id,
                plane.row_bytes,
                plane.bytes_per_row,
                plane.bytes.len()
            )
            .expect("writing readback metadata into String cannot fail");
        }
        create("metadata.txt", text.as_bytes())?;
        // Created last: consumers must require this file, not infer completion
        // merely from directory existence. SHA/provenance is an external audit.
        create(
            "readback-complete.txt",
            b"seven real GPU callbacks received; no PNG conversion; not numeric source parity\n",
        )
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct Ticket {
    request_id: u64,
    tag: FrameTag,
    generation: u64,
    auxiliary_generation: u64,
}

struct Active {
    id: u64,
    scope: Scope,
    stage: Stage,
    metadata: Option<Metadata>,
    output: Option<Plane>,
    reserved_output_bytes: u64,
    received: HashSet<Role>,
    planes: Vec<Plane>,
    error: Option<Error>,
}
#[derive(Default)]
struct State {
    next_id: u64,
    active: Option<Active>,
    snapshot: Option<Snapshot>,
    failure: Option<(u64, Error)>,
    stale_callbacks: u64,
    /// Includes buffers already submitted/mapping from a failed old request.
    in_flight_bytes: u64,
}

#[derive(Resource, Clone, Default)]
pub struct Observation(Arc<Mutex<State>>);

impl Observation {
    pub fn request(&self, scope: Scope) -> Result<u64, Error> {
        if scope.minimum_serial == 0
            || scope.expected_resolve_generation == Some(0)
            || scope.expected_auxiliary_generation == Some(0)
        {
            return Err(Error::WrongScope);
        }
        let mut state = self.0.lock().map_err(|_| Error::StatePoisoned)?;
        if state.active.is_some() {
            return Err(Error::Busy);
        }
        state.next_id = state
            .next_id
            .checked_add(1)
            .ok_or(Error::CounterExhausted)?;
        let id = state.next_id;
        state.snapshot = None;
        state.failure = None;
        state.active = Some(Active {
            id,
            scope,
            stage: Stage::Requested,
            metadata: None,
            output: None,
            reserved_output_bytes: 0,
            received: HashSet::new(),
            planes: Vec::with_capacity(PLANES),
            error: None,
        });
        Ok(id)
    }

    pub fn status(&self) -> Result<Status, Error> {
        let state = self.0.lock().map_err(|_| Error::StatePoisoned)?;
        Ok(if let Some(active) = &state.active {
            Status::Pending {
                request_id: active.id,
                stage: active.stage,
            }
        } else if let Some(snapshot) = &state.snapshot {
            Status::Ready {
                request_id: snapshot.request_id,
            }
        } else if let Some((id, error)) = &state.failure {
            Status::Failed {
                request_id: *id,
                error: error.clone(),
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

    fn fail(&self, id: u64, error: Error) {
        if let Ok(mut state) = self.0.lock()
            && state.active.as_ref().is_some_and(|active| active.id == id)
        {
            if let Some(active) = state.active.take() {
                state.in_flight_bytes = state
                    .in_flight_bytes
                    .saturating_sub(active.reserved_output_bytes);
            }
            state.failure = Some((id, error));
        }
    }

    fn release_bytes(&self, bytes: u64) {
        if let Ok(mut state) = self.0.lock() {
            state.in_flight_bytes = state.in_flight_bytes.saturating_sub(bytes);
        }
    }

    fn receive(&self, ticket: Ticket, role: Role, result: Result<Plane, Error>) {
        let Ok(mut state) = self.0.lock() else {
            return;
        };
        let Some(active) = &mut state.active else {
            state.stale_callbacks = state.stale_callbacks.saturating_add(1);
            return;
        };
        let matching = active.id == ticket.request_id
            && active.stage == Stage::Mapping
            && active
                .metadata
                .as_ref()
                .is_some_and(|metadata| ticket.matches(metadata));
        if !matching {
            state.stale_callbacks = state.stale_callbacks.saturating_add(1);
            return;
        }
        if !active.received.insert(role) {
            active.error.get_or_insert(Error::DuplicateRole);
            return;
        }
        match result {
            Ok(plane) if plane.role == role && plane.unpadded_bytes().is_ok() => {
                active.planes.push(plane);
            }
            Ok(_) => {
                active.error.get_or_insert(Error::InvalidLayout);
            }
            Err(error) => {
                active.error.get_or_insert(error);
            }
        }
        if active.received.len() != PLANES {
            return;
        }
        let active = state.active.take().expect("Matched active request");
        if let Some(error) = active.error {
            state.failure = Some((active.id, error));
            return;
        }
        let Some(metadata) = active.metadata else {
            state.failure = Some((active.id, Error::InvalidMetadata));
            return;
        };
        let snapshot = Snapshot {
            request_id: active.id,
            metadata,
            planes: active.planes,
            resolve_draw_encoded: true,
        };
        if snapshot.complete() {
            state.snapshot = Some(snapshot);
        } else {
            state.failure = Some((active.id, Error::InvalidLayout));
        }
    }
}

impl Ticket {
    fn matches(self, metadata: &Metadata) -> bool {
        self.tag == metadata.tag
            && self.generation == metadata.resolve_generation
            && self.auxiliary_generation == metadata.auxiliary_generation
    }
}

/// All seven textures are validated before copying any input. Output is only
/// described here; its bytes are copied exclusively by the after-draw hook.
pub struct Inputs<'a> {
    pub beauty: &'a Texture,
    pub input_history: &'a Texture,
    pub previous_source_depth: &'a Texture,
    pub current_depth: &'a Texture,
    pub velocity: &'a Texture,
    pub mask: &'a Texture,
    pub expected_output: &'a Texture,
}

struct Job {
    ticket: Ticket,
    plane: Plane,
    buffer: Buffer,
}
#[derive(Resource, Default)]
pub struct Jobs(Vec<Job>);

/// Installs ONLY the shared bus and post-submit mapping. Render hooks remain an
/// explicit caller responsibility. No resource extraction or second renderer.
pub fn register(app: &mut App) -> Result<(), String> {
    if app.world().contains_resource::<Observation>() {
        return Ok(());
    }
    if app.get_sub_app(RenderApp).is_none() {
        return Err("Temporal resolve readback requires Bevy RenderApp".into());
    }
    let observation = Observation::default();
    app.insert_resource(observation.clone());
    app.sub_app_mut(RenderApp)
        .insert_resource(observation)
        .init_resource::<Jobs>()
        .add_systems(Render, map_after_submission.in_set(RenderSystems::Cleanup));
    Ok(())
}

/// AFTER optional source bilinear seed+clear0; BEFORE actual resolve draw.
/// Inactive requests and other views are no-ops. Failure affects readback only.
#[allow(
    clippy::needless_pass_by_value,
    clippy::similar_names,
    reason = "The public hook consumes immutable per-draw metadata and input snapshots"
)]
pub fn before_resolve(
    observation: &Observation,
    jobs: &mut Jobs,
    context: &mut RenderContext,
    metadata: Metadata,
    inputs: Inputs<'_>,
) -> Result<Option<Ticket>, Error> {
    let pending = {
        let state = observation.0.lock().map_err(|_| Error::StatePoisoned)?;
        state
            .active
            .as_ref()
            .map(|active| (active.id, active.scope, active.stage))
    };
    let Some((id, scope, stage)) = pending else {
        return Ok(None);
    };
    if scope.view != metadata.tag.view {
        return Ok(None);
    }
    let result = (|| {
        if stage != Stage::Requested {
            return Err(Error::WrongTicket);
        }
        metadata.validate()?;
        if !scope.accepts(&metadata) {
            return Err(Error::WrongScope);
        }
        metadata.uniform_bytes()?; // Verify the exact bound ABI before any copy.
        let textures = [
            (Role::Beauty, inputs.beauty, metadata.input_size),
            (
                Role::InputHistory,
                inputs.input_history,
                metadata.output_size,
            ),
            (
                Role::PreviousSourceDepth,
                inputs.previous_source_depth,
                metadata.input_size,
            ),
            (
                Role::CurrentDepth,
                inputs.current_depth,
                metadata.input_size,
            ),
            (Role::Velocity, inputs.velocity, metadata.input_size),
            (Role::Mask, inputs.mask, metadata.input_size),
            (Role::Output, inputs.expected_output, metadata.output_size),
        ];
        let ids = textures
            .iter()
            .map(|(_, texture, _)| texture.id())
            .collect::<HashSet<_>>();
        if ids.len() != PLANES {
            return Err(Error::AliasedTexture);
        }
        let mut descriptions = Vec::with_capacity(PLANES);
        let mut total = 0_u64;
        for (role, texture, size) in textures {
            let plane = describe(role, texture, size)?;
            total = total
                .checked_add(u64::from(plane.bytes_per_row) * u64::from(plane.size.y))
                .ok_or(Error::TooManyBytes)?;
            if total > MAX_BYTES {
                return Err(Error::TooManyBytes);
            }
            descriptions.push(plane);
        }
        let ticket = Ticket {
            request_id: id,
            tag: metadata.tag,
            generation: metadata.resolve_generation,
            auxiliary_generation: metadata.auxiliary_generation,
        };
        {
            let mut state = observation.0.lock().map_err(|_| Error::StatePoisoned)?;
            let reserved = state
                .in_flight_bytes
                .checked_add(total)
                .ok_or(Error::TooManyBytes)?;
            if reserved > MAX_BYTES {
                return Err(Error::TooManyBytes);
            }
            let pending = state
                .active
                .as_mut()
                .filter(|active| active.id == id && active.stage == Stage::Requested)
                .ok_or(Error::WrongTicket)?;
            pending.output = descriptions.pop();
            pending.reserved_output_bytes = pending.output.as_ref().map_or(0, |plane| {
                u64::from(plane.bytes_per_row) * u64::from(plane.size.y)
            });
            pending.metadata = Some(metadata);
            pending.stage = Stage::InputsEncoded;
            state.in_flight_bytes = reserved;
        }
        for ((_, texture, _), plane) in textures.into_iter().take(6).zip(descriptions) {
            encode_copy(jobs, context, ticket, plane, texture);
        }
        Ok(Some(ticket))
    })();
    if let Err(error) = &result {
        observation.fail(id, error.clone());
    }
    result
}

/// AFTER actual resolve draw; BEFORE depth/history/HDR overwrite.
pub fn after_resolve(
    observation: &Observation,
    jobs: &mut Jobs,
    context: &mut RenderContext,
    ticket: Ticket,
    output: &Texture,
) -> Result<(), Error> {
    let result = (|| {
        let expected = {
            let state = observation.0.lock().map_err(|_| Error::StatePoisoned)?;
            let active = state
                .active
                .as_ref()
                .filter(|active| {
                    active.id == ticket.request_id && active.stage == Stage::InputsEncoded
                })
                .ok_or(Error::WrongTicket)?;
            if !active
                .metadata
                .as_ref()
                .is_some_and(|metadata| ticket.matches(metadata))
            {
                return Err(Error::WrongTicket);
            }
            active.output.clone().ok_or(Error::WrongTicket)?
        };
        let actual = describe(Role::Output, output, expected.size)?;
        if actual.texture_id != expected.texture_id
            || actual.format != expected.format
            || actual.bytes_per_row != expected.bytes_per_row
        {
            return Err(Error::WrongTicket);
        }
        encode_copy(jobs, context, ticket, actual, output);
        let mut state = observation.0.lock().map_err(|_| Error::StatePoisoned)?;
        let active = state
            .active
            .as_mut()
            .filter(|active| active.id == ticket.request_id && active.stage == Stage::InputsEncoded)
            .ok_or(Error::WrongTicket)?;
        active.stage = Stage::Paired;
        active.reserved_output_bytes = 0;
        Ok(())
    })();
    if let Err(error) = &result {
        observation.fail(ticket.request_id, error.clone());
    }
    result
}

fn layout(role: Role, size: UVec2) -> Result<(u32, u32, u64), Error> {
    if size.min_element() == 0 {
        return Err(Error::InvalidLayout);
    }
    let row = size
        .x
        .checked_mul(role.pixel_bytes())
        .ok_or(Error::InvalidLayout)?;
    let stride = row.checked_add(255).ok_or(Error::InvalidLayout)? / 256 * 256;
    let total = u64::from(stride)
        .checked_mul(u64::from(size.y))
        .ok_or(Error::InvalidLayout)?;
    Ok((row, stride, total))
}

fn describe(role: Role, texture: &Texture, size: UVec2) -> Result<Plane, Error> {
    let extent = texture.size();
    if extent.width != size.x
        || extent.height != size.y
        || extent.depth_or_array_layers != 1
        || texture.sample_count() != 1
        || texture.format() != role.format()
        || !texture.usage().contains(TextureUsages::COPY_SRC)
    {
        return Err(Error::InvalidTexture);
    }
    let (row_bytes, bytes_per_row, _) = layout(role, size)?;
    Ok(Plane {
        role,
        format: role.format(),
        size,
        texture_id: format!("{:?}", texture.id()),
        row_bytes,
        bytes_per_row,
        bytes: Arc::new(Vec::new()),
    })
}

fn encode_copy(
    jobs: &mut Jobs,
    context: &mut RenderContext,
    ticket: Ticket,
    plane: Plane,
    texture: &Texture,
) {
    let buffer = context.render_device().create_buffer(&BufferDescriptor {
        label: Some("Tidewater actual source resolve seven-plane readback"),
        size: u64::from(plane.bytes_per_row) * u64::from(plane.size.y),
        usage: BufferUsages::COPY_DST | BufferUsages::MAP_READ,
        mapped_at_creation: false,
    });
    let mut source = texture.as_image_copy();
    source.aspect = if plane.format == TextureFormat::Depth32Float {
        TextureAspect::DepthOnly
    } else {
        TextureAspect::All
    };
    context.command_encoder().copy_texture_to_buffer(
        source,
        TexelCopyBufferInfo {
            buffer: &buffer,
            layout: TexelCopyBufferLayout {
                offset: 0,
                bytes_per_row: Some(plane.bytes_per_row),
                rows_per_image: Some(plane.size.y),
            },
        },
        texture.size(),
    );
    jobs.0.push(Job {
        ticket,
        plane,
        buffer,
    });
}

/// Render Cleanup runs after Bevy command submission. No blocking device poll,
/// adapter creation, CPU substitute for GPU bytes, or synchronous wait.
#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy Cleanup systems receive resource wrappers by value"
)]
fn map_after_submission(mut jobs: ResMut<Jobs>, observation: Res<Observation>) {
    let transition = observation.0.lock().ok().and_then(|mut state| {
        let active = state.active.as_mut()?;
        match active.stage {
            Stage::Requested => Some((active.id, Error::MissingFrame)),
            Stage::InputsEncoded => Some((active.id, Error::MissingAfterHook)),
            Stage::Paired => {
                if jobs
                    .0
                    .iter()
                    .filter(|job| job.ticket.request_id == active.id)
                    .count()
                    == PLANES
                {
                    active.stage = Stage::Mapping;
                    None
                } else {
                    Some((active.id, Error::MissingAfterHook))
                }
            }
            Stage::Mapping => None,
        }
    });
    if let Some((id, error)) = transition {
        observation.fail(id, error);
    }
    for job in jobs.0.drain(..) {
        let buffer = job.buffer.clone();
        let shared = (*observation).clone();
        let role = job.plane.role;
        let reserved_bytes = u64::from(job.plane.bytes_per_row) * u64::from(job.plane.size.y);
        job.buffer
            .slice(..)
            .map_async(MapMode::Read, move |result| {
                let result = result
                    .map_err(|error| Error::MapFailed(error.to_string()))
                    .and_then(|()| {
                        let mapped = buffer.slice(..).get_mapped_range();
                        let expected =
                            u64::from(job.plane.bytes_per_row) * u64::from(job.plane.size.y);
                        let result = if u64::try_from(mapped.len()).ok() == Some(expected) {
                            let mut plane = job.plane;
                            plane.bytes = Arc::new(mapped.to_vec());
                            Ok(plane)
                        } else {
                            Err(Error::InvalidLayout)
                        };
                        drop(mapped);
                        buffer.unmap();
                        result
                    });
                shared.release_bytes(reserved_bytes);
                shared.receive(job.ticket, role, result);
            });
    }
}

/// Exact IEEE-754 binary16 value decoded into f32 (no clamp/tone/PNG conversion).
/// NaN/Infinity survive; the numeric auditor must count/reject them explicitly.
#[must_use]
pub fn binary16_to_f32(bits: u16) -> f32 {
    let sign = u32::from(bits & 0x8000) << 16;
    let exponent = (bits >> 10) & 0x1f;
    let fraction = u32::from(bits & 0x03ff);
    let result = match exponent {
        0 if fraction == 0 => sign,
        0 => {
            let shift = fraction.leading_zeros() - 21;
            sign | ((113 - shift) << 23) | ((fraction << shift & 0x03ff) << 13)
        }
        31 => sign | 0x7f80_0000 | (fraction << 13),
        value => sign | ((u32::from(value) + 112) << 23) | (fraction << 13),
    };
    f32::from_bits(result)
}

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub struct Finiteness {
    pub values: u64,
    pub nan: u64,
    pub infinite: u64,
}
pub fn finiteness(plane: &Plane) -> Result<Finiteness, Error> {
    let bytes = plane.unpadded_bytes()?;
    let mut result = Finiteness::default();
    let mut count = |value: f32| {
        result.values += 1;
        result.nan += u64::from(value.is_nan());
        result.infinite += u64::from(value.is_infinite());
    };
    match plane.format {
        TextureFormat::Rgba16Float => {
            for pair in bytes.as_chunks::<2>().0 {
                count(binary16_to_f32(u16::from_le_bytes(*pair)));
            }
        }
        TextureFormat::Depth32Float => {
            for word in bytes.as_chunks::<4>().0 {
                count(f32::from_le_bytes(*word));
            }
        }
        TextureFormat::Rgba8Unorm => {
            result.values = u64::try_from(bytes.len()).map_err(|_| Error::InvalidLayout)?;
        }
        _ => return Err(Error::InvalidTexture),
    }
    Ok(result)
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::ecs::system::RunSystemOnce;
    fn metadata() -> Metadata {
        Metadata {
            tag: FrameTag {
                view: Entity::from_raw_u32(1).unwrap(),
                serial: 1,
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
    fn plane(role: Role) -> Plane {
        let size = UVec2::ONE;
        let (row_bytes, bytes_per_row, total) = layout(role, size).unwrap();
        Plane {
            role,
            format: role.format(),
            size,
            texture_id: role.name().into(),
            row_bytes,
            bytes_per_row,
            bytes: Arc::new(vec![
                0;
                usize::try_from(total).expect(
                    "test readback allocation must fit the platform address space"
                )
            ]),
        }
    }
    fn mapping() -> (Observation, Ticket) {
        let metadata = metadata();
        let observer = Observation::default();
        let id = observer
            .request(Scope {
                view: metadata.tag.view,
                minimum_serial: 1,
                history_epoch: 4,
                expected_resolve_generation: Some(2),
                expected_auxiliary_generation: Some(3),
            })
            .unwrap();
        let ticket = Ticket {
            request_id: id,
            tag: metadata.tag,
            generation: 2,
            auxiliary_generation: 3,
        };
        let mut state = observer.0.lock().unwrap();
        let active = state.active.as_mut().unwrap();
        active.stage = Stage::Mapping;
        active.metadata = Some(metadata);
        drop(state);
        (observer, ticket)
    }
    #[test]
    fn seven_distinct_callbacks_required() {
        let (observer, ticket) = mapping();
        for role in [
            Role::Beauty,
            Role::InputHistory,
            Role::PreviousSourceDepth,
            Role::CurrentDepth,
            Role::Velocity,
            Role::Mask,
        ] {
            observer.receive(ticket, role, Ok(plane(role)));
        }
        assert!(observer.snapshot().unwrap().is_none());
        observer.receive(ticket, Role::Output, Ok(plane(Role::Output)));
        assert!(observer.snapshot().unwrap().unwrap().complete());
    }

    #[test]
    fn missing_after_hook_is_a_failure_even_after_inputs_were_encoded() {
        let (observer, _) = mapping();
        observer.0.lock().unwrap().active.as_mut().unwrap().stage = Stage::InputsEncoded;
        let mut world = World::new();
        world.insert_resource(observer.clone());
        world.insert_resource(Jobs::default());
        world.run_system_once(map_after_submission).unwrap();
        assert!(matches!(
            observer.status().unwrap(),
            Status::Failed {
                error: Error::MissingAfterHook,
                ..
            }
        ));
        assert!(observer.snapshot().unwrap().is_none());
    }

    #[test]
    fn failed_request_releases_only_uncopied_output_reservation() {
        let (observer, ticket) = mapping();
        {
            let mut state = observer.0.lock().unwrap();
            state.in_flight_bytes = 7 * 256;
            state.active.as_mut().unwrap().reserved_output_bytes = 256;
        }
        observer.fail(ticket.request_id, Error::MissingAfterHook);
        assert_eq!(observer.0.lock().unwrap().in_flight_bytes, 6 * 256);
        for _ in 0..6 {
            observer.release_bytes(256);
        }
        assert_eq!(observer.0.lock().unwrap().in_flight_bytes, 0);
    }

    #[test]
    fn export_refuses_an_existing_path_without_replacing_bytes() {
        // Artificial CPU bytes test filesystem semantics only, never GPU proof.
        let fixture = Snapshot {
            request_id: 1,
            metadata: metadata(),
            planes: [
                Role::Beauty,
                Role::InputHistory,
                Role::PreviousSourceDepth,
                Role::CurrentDepth,
                Role::Velocity,
                Role::Mask,
                Role::Output,
            ]
            .into_iter()
            .map(plane)
            .collect(),
            resolve_draw_encoded: true,
        };
        let unique = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        let root = option_env!("FORGE_TIDEWATER_READBACK_CPU_DIR")
            .map(std::path::PathBuf::from)
            .unwrap_or_else(|| std::env::temp_dir().join("forge-temporal-readback-cpu"));
        fs::create_dir_all(&root).unwrap();
        let path = root.join(format!(
            "cpu-filesystem-fixture-{}-{unique}",
            std::process::id()
        ));
        fixture.write_new_directory(&path).unwrap();
        let packet = fs::read(path.join("packet336.bin")).unwrap();
        assert_eq!(
            fixture.write_new_directory(&path).unwrap_err().kind(),
            std::io::ErrorKind::AlreadyExists
        );
        assert_eq!(fs::read(path.join("packet336.bin")).unwrap(), packet);
    }
    #[test]
    fn stale_or_duplicate_callbacks_cannot_promote_readiness() {
        let (observer, ticket) = mapping();
        let mut stale = ticket;
        stale.generation += 1;
        observer.receive(stale, Role::Output, Ok(plane(Role::Output)));
        assert_eq!(observer.stale_callbacks().unwrap(), 1);
        observer.receive(ticket, Role::Beauty, Ok(plane(Role::Beauty)));
        observer.receive(ticket, Role::Beauty, Ok(plane(Role::Beauty)));
        for role in [
            Role::InputHistory,
            Role::PreviousSourceDepth,
            Role::CurrentDepth,
            Role::Velocity,
            Role::Mask,
            Role::Output,
        ] {
            observer.receive(ticket, role, Ok(plane(role)));
        }
        assert!(matches!(
            observer.status().unwrap(),
            Status::Failed {
                error: Error::DuplicateRole,
                ..
            }
        ));
        assert!(observer.snapshot().unwrap().is_none());
    }
    #[test]
    #[allow(
        clippy::float_cmp,
        reason = "Checks exact serialized float32 uniform bytes."
    )]
    fn request_scope_and_actual_uniform_bytes_are_strict() {
        let mut metadata = metadata();
        metadata.validate().unwrap();
        let (packet, params) = metadata.uniform_bytes().unwrap();
        assert_eq!(packet.len(), 336);
        assert_eq!(params.len(), 32);
        assert_eq!(f32::from_le_bytes(params[..4].try_into().unwrap()), 0.06);
        let scope = Scope {
            view: metadata.tag.view,
            minimum_serial: 2,
            history_epoch: 4,
            expected_resolve_generation: Some(2),
            expected_auxiliary_generation: Some(3),
        };
        assert!(!scope.accepts(&metadata));
        metadata.tag.serial = 2;
        assert!(scope.accepts(&metadata));
        metadata.resolve_generation = 8;
        assert!(!scope.accepts(&metadata));
        metadata.history_seeded = false;
        assert_eq!(metadata.validate(), Err(Error::InvalidMetadata));
    }
    #[test]
    fn odd_width_stride_preserves_original_bytes_without_padding() {
        let size = UVec2::new(801, 601);
        let (row, stride, total) = layout(Role::Beauty, size).unwrap();
        assert_eq!(row, 6408);
        assert_eq!(stride, 6656);
        assert_eq!(total, 4_000_256);
        let mut plane = plane(Role::Beauty);
        plane.size = size;
        plane.row_bytes = row;
        plane.bytes_per_row = stride;
        let mut bytes = vec![
            9;
            usize::try_from(total).expect(
                "test readback allocation must fit the platform address space"
            )
        ];
        for padded in bytes.chunks_exact_mut(stride as usize) {
            padded[..row as usize].fill(7);
        }
        plane.bytes = Arc::new(bytes);
        let compact = plane.unpadded_bytes().unwrap();
        assert_eq!(compact.len(), 3_851_208);
        assert!(compact.iter().all(|value| *value == 7));
        assert_eq!(
            layout(Role::Output, UVec2::new(u32::MAX, 1)),
            Err(Error::InvalidLayout)
        );
    }
    #[test]
    #[allow(
        clippy::float_cmp,
        reason = "Binary16 endpoint fixtures decode to exact powers of two and integer values."
    )]
    fn binary16_and_depth_finiteness_never_clamp_nonfinite_values() {
        assert_eq!(binary16_to_f32(0).to_bits(), 0);
        assert_eq!(binary16_to_f32(0x8000).to_bits(), 0x8000_0000);
        assert_eq!(binary16_to_f32(1), 2_f32.powi(-24));
        assert_eq!(binary16_to_f32(0x03ff), 1023.0 * 2_f32.powi(-24));
        assert_eq!(binary16_to_f32(0x3c00), 1.0);
        assert_eq!(binary16_to_f32(0x7bff), 65504.0);
        assert!(binary16_to_f32(0x7c00).is_infinite());
        assert!(binary16_to_f32(0x7e00).is_nan());
        let mut half = plane(Role::Output);
        let mut bytes = vec![0; 256];
        bytes[..8].copy_from_slice(&[0, 0, 0, 0x7c, 0, 0x7e, 0, 0x3c]);
        half.bytes = Arc::new(bytes);
        assert_eq!(
            finiteness(&half).unwrap(),
            Finiteness {
                values: 4,
                nan: 1,
                infinite: 1
            }
        );
        let mut depth = plane(Role::CurrentDepth);
        let mut bytes = vec![0; 256];
        bytes[..4].copy_from_slice(&f32::NAN.to_le_bytes());
        depth.bytes = Arc::new(bytes);
        assert_eq!(
            finiteness(&depth).unwrap(),
            Finiteness {
                values: 1,
                nan: 1,
                infinite: 0
            }
        );
    }
}
