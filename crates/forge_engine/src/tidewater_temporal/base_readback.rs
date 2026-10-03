// SPDX-License-Identifier: MIT
//! Opt-in raw readbacks of auxiliary MRTs immediately BEFORE the first late
//! transparent draw, paired with complete auxiliary encoding of the same frame.
//! No visible-color changes and no replacement/exclusion of the composite oracle.
//! Registration is inactive until an adapter explicitly requests a scoped capture.
//! Command encoding and CPU tests cannot establish GPU or numeric source parity.

use super::{
    frame::{FrameTag, Packet, ViewPacket},
    render::AuxObservation,
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
    fs::{self, OpenOptions},
    io::Write,
    path::Path,
    sync::{Arc, Mutex},
};

pub const MAX_BYTES: u64 = 512 * 1024 * 1024;

#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub enum Role {
    BaseVelocity,
    BaseMask,
    BaseDepth,
    FinalVelocity,
    FinalMask,
    FinalDepth,
}
impl Role {
    #[must_use]
    pub const fn name(self) -> &'static str {
        match self {
            Self::BaseVelocity => "base-velocity-rgba16f",
            Self::BaseMask => "base-mask-rgba8unorm",
            Self::BaseDepth => "base-depth-depth32f",
            Self::FinalVelocity => "final-velocity-rgba16f",
            Self::FinalMask => "final-mask-rgba8unorm",
            Self::FinalDepth => "final-depth-depth32f",
        }
    }
    #[must_use]
    pub const fn format(self) -> TextureFormat {
        match self {
            Self::BaseVelocity | Self::FinalVelocity => TextureFormat::Rgba16Float,
            Self::BaseMask | Self::FinalMask => TextureFormat::Rgba8Unorm,
            Self::BaseDepth | Self::FinalDepth => TextureFormat::Depth32Float,
        }
    }
    #[must_use]
    pub const fn pixel_bytes(self) -> u32 {
        match self {
            Self::BaseVelocity | Self::FinalVelocity => 8,
            _ => 4,
        }
    }
    fn base(self) -> Self {
        match self {
            Self::FinalVelocity => Self::BaseVelocity,
            Self::FinalMask => Self::BaseMask,
            Self::FinalDepth => Self::BaseDepth,
            other => other,
        }
    }
}
const BASE: [Role; 3] = [Role::BaseVelocity, Role::BaseMask, Role::BaseDepth];
const FINAL: [Role; 3] = [Role::FinalVelocity, Role::FinalMask, Role::FinalDepth];

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Mode {
    BaseOnly,
    BaseAndFinal,
}
impl Mode {
    #[must_use]
    pub const fn count(self) -> usize {
        match self {
            Self::BaseOnly => 3,
            Self::BaseAndFinal => 6,
        }
    }
    fn contains(self, role: Role) -> bool {
        self == Self::BaseAndFinal || BASE.contains(&role)
    }
}

/// These counts come from the actual auxiliary draw counter at the boundary,
/// plus the real queue coverage. They are eligibility checks, never GPU evidence.
#[derive(Clone, Copy, Debug)]
pub struct Boundary {
    pub opaque: u32,
    pub water: u32,
    pub transparent: u32,
    pub queued: u32,
    pub base_mesh_draws: u32,
    pub sky_draws: u32,
    pub hull_draws: u32,
    pub hull_contract_satisfied: bool,
}
impl Boundary {
    fn validate(self) -> Result<(), Error> {
        let base = self
            .opaque
            .checked_add(self.water)
            .ok_or(Error::InvalidBoundary)?;
        let queued = base
            .checked_add(self.transparent)
            .ok_or(Error::InvalidBoundary)?;
        if self.base_mesh_draws != base
            || self.queued == 0
            || self.queued != queued
            || self.sky_draws != 1
            || !self.hull_contract_satisfied
        {
            return Err(Error::InvalidBoundary);
        }
        Ok(())
    }
}

/// Immutable exact frame/packet copied at the texture-copy boundary.
#[derive(Clone, Debug)]
pub struct Metadata {
    pub view_packet: ViewPacket,
    pub generation: u64,
    pub input_size: UVec2,
    pub output_size: UVec2,
    pub boundary: Boundary,
}
impl Metadata {
    #[must_use]
    pub fn tag(&self) -> FrameTag {
        self.view_packet.tag
    }
    #[must_use]
    pub fn history_epoch(&self) -> u64 {
        self.view_packet.frame.history_epoch
    }
    fn validate(&self) -> Result<(), Error> {
        let frame = &self.view_packet.frame;
        if self.generation == 0
            || self.tag().serial == 0
            || self.input_size.min_element() == 0
            || frame.view.0 != self.tag().view.to_bits()
            || frame.phase >= 32
            || self.input_size != frame.input_size
            || self.output_size != frame.output_size
            || self.output_size.min_element() == 0
            || self.view_packet.packet != Packet::from_frame(frame)
        {
            return Err(Error::InvalidMetadata);
        }
        self.boundary.validate()?;
        self.uniform_bytes()?;
        Ok(())
    }
    pub fn uniform_bytes(&self) -> Result<Vec<u8>, Error> {
        let mut buffer = encase::UniformBuffer::new(Vec::new());
        buffer
            .write(&self.view_packet.packet)
            .map_err(|e| Error::UniformEncoding(e.to_string()))?;
        let bytes = buffer.into_inner();
        if bytes.len() != 336 {
            return Err(Error::InvalidMetadata);
        }
        Ok(bytes)
    }
    fn accepts_complete(&self, observation: &AuxObservation) -> bool {
        let b = self.boundary;
        observation.complete()
            && observation.tag == self.tag()
            && observation.generation == self.generation
            && observation.coverage.queued == b.queued
            && observation.coverage.opaque == b.opaque
            && observation.coverage.water == b.water
            && observation.coverage.transparent == b.transparent
            && observation.mesh_draws == b.queued
            && observation.sky_draws == b.sky_draws
            && observation.hull_draws == b.hull_draws
            && observation.hull_contract_satisfied == b.hull_contract_satisfied
    }
}

#[derive(Clone, Copy, Debug)]
pub struct Scope {
    pub view: Entity,
    pub minimum_serial: u64,
    pub history_epoch: u64,
    pub expected_generation: Option<u64>,
    pub mode: Mode,
}
impl Scope {
    fn accepts(self, metadata: &Metadata) -> bool {
        self.view == metadata.tag().view
            && metadata.tag().serial >= self.minimum_serial
            && self.history_epoch == metadata.history_epoch()
            && self
                .expected_generation
                .is_none_or(|n| n == metadata.generation)
    }
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub enum Error {
    Busy,
    StatePoisoned,
    CounterExhausted,
    WrongScope,
    InvalidMetadata,
    InvalidBoundary,
    MissingFrame,
    MissingAfterHook,
    IncompleteAuxiliary,
    WrongTicket,
    DuplicateRole,
    InvalidTexture,
    AliasedTexture,
    InvalidLayout,
    TooManyBytes,
    MapFailed(String),
    UniformEncoding(String),
}
impl fmt::Display for Error {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "Temporal base readback: {self:?}")
    }
}
impl std::error::Error for Error {}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Stage {
    Requested,
    BaseEncoded,
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
    /// Mapped bytes from GPU; padding is retained and checked independently.
    pub bytes: Arc<Vec<u8>>,
}
impl Plane {
    pub fn unpadded_bytes(&self) -> Result<Vec<u8>, Error> {
        let (row, stride, total) = layout(self.role, self.size)?;
        if self.format != self.role.format()
            || row != self.row_bytes
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
    pub mode: Mode,
    pub metadata: Metadata,
    pub planes: Vec<Plane>,
    /// Actual post-transparent encoding observation of this same frame.
    pub final_encoding: AuxObservation,
}
impl Snapshot {
    #[must_use]
    pub fn complete(&self) -> bool {
        if self.metadata.validate().is_err()
            || !self.metadata.accepts_complete(&self.final_encoding)
            || self.planes.len() != self.mode.count()
        {
            return false;
        }
        let roles = self.planes.iter().map(|p| p.role).collect::<HashSet<_>>();
        if roles.len() != self.mode.count()
            || BASE.iter().any(|r| !roles.contains(r))
            || self.planes.iter().any(|p| {
                !self.mode.contains(p.role)
                    || p.size != self.metadata.input_size
                    || p.unpadded_bytes().is_err()
            })
        {
            return false;
        }
        let bases = self
            .planes
            .iter()
            .filter(|p| BASE.contains(&p.role))
            .collect::<Vec<_>>();
        if bases
            .iter()
            .map(|p| &p.texture_id)
            .collect::<HashSet<_>>()
            .len()
            != 3
        {
            return false;
        }
        self.planes
            .iter()
            .filter(|p| FINAL.contains(&p.role))
            .all(|final_plane| {
                bases.iter().any(|base| {
                    base.role == final_plane.role.base()
                        && base.texture_id == final_plane.texture_id
                        && base.format == final_plane.format
                })
            })
    }
    /// Creates only new paths, completion marker last. Partial I/O is retained.
    /// This exports raw evidence; it makes no camera/blend/source-parity verdict.
    pub fn write_new_directory(&self, path: &Path) -> std::io::Result<()> {
        if !self.complete() {
            return Err(std::io::Error::other("Incomplete base GPU snapshot"));
        }
        let packet = self
            .metadata
            .uniform_bytes()
            .map_err(std::io::Error::other)?;
        fs::create_dir(path)?;
        let write = |name: &str, bytes: &[u8]| -> std::io::Result<()> {
            let mut file = OpenOptions::new()
                .write(true)
                .create_new(true)
                .open(path.join(name))?;
            file.write_all(bytes)?;
            file.sync_all()
        };
        for p in &self.planes {
            write(&format!("{}.padded.bin", p.role.name()), &p.bytes)?;
            write(
                &format!("{}.bin", p.role.name()),
                &p.unpadded_bytes().map_err(std::io::Error::other)?,
            )?;
        }
        write("packet336.bin", &packet)?;
        write(
            "metadata.txt",
            format!(
                "request_id={}\nmode={:?}\n{:?}\nfinal_encoding={:?}\nplanes={:?}\n",
                self.request_id,
                self.mode,
                self.metadata,
                self.final_encoding,
                self.planes
                    .iter()
                    .map(|p| (
                        p.role,
                        p.format,
                        p.size,
                        &p.texture_id,
                        p.row_bytes,
                        p.bytes_per_row,
                        p.bytes.len()
                    ))
                    .collect::<Vec<_>>()
            )
            .as_bytes(),
        )?;
        write("readback-complete.txt", b"role byte container and same-frame encoding metadata complete; runtime callback provenance comes from Observation bus; no composite/source parity claim\n")
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct Ticket {
    request_id: u64,
    tag: FrameTag,
    generation: u64,
}
impl Ticket {
    fn matches(self, m: &Metadata) -> bool {
        self.tag == m.tag() && self.generation == m.generation
    }
}
struct Active {
    id: u64,
    scope: Scope,
    stage: Stage,
    metadata: Option<Metadata>,
    expected_base: Vec<Plane>,
    reserved_final_bytes: u64,
    final_encoding: Option<AuxObservation>,
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
    in_flight_bytes: u64,
}
#[derive(Resource, Clone, Default)]
pub struct Observation(Arc<Mutex<State>>);
impl Observation {
    pub fn request(&self, scope: Scope) -> Result<u64, Error> {
        if scope.minimum_serial == 0 || scope.expected_generation == Some(0) {
            return Err(Error::WrongScope);
        }
        let mut state = self.0.lock().map_err(|_| Error::StatePoisoned)?;
        // Failed requests cannot overlap unresolved old buffers/callbacks.
        if state.active.is_some() || state.in_flight_bytes != 0 {
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
            expected_base: Vec::new(),
            reserved_final_bytes: 0,
            final_encoding: None,
            received: HashSet::new(),
            planes: Vec::with_capacity(scope.mode.count()),
            error: None,
        });
        Ok(id)
    }
    pub fn status(&self) -> Result<Status, Error> {
        let state = self.0.lock().map_err(|_| Error::StatePoisoned)?;
        Ok(if let Some(a) = &state.active {
            Status::Pending {
                request_id: a.id,
                stage: a.stage,
            }
        } else if let Some(s) = &state.snapshot {
            Status::Ready {
                request_id: s.request_id,
            }
        } else if let Some((id, e)) = &state.failure {
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
    fn fail(&self, id: u64, error: Error) {
        if let Ok(mut s) = self.0.lock()
            && s.active.as_ref().is_some_and(|a| a.id == id)
        {
            if let Some(a) = s.active.take() {
                s.in_flight_bytes = s.in_flight_bytes.saturating_sub(a.reserved_final_bytes);
            }
            s.failure = Some((id, error));
        }
    }
    fn release_bytes(&self, n: u64) {
        if let Ok(mut s) = self.0.lock() {
            s.in_flight_bytes = s.in_flight_bytes.saturating_sub(n);
        }
    }
    fn receive(&self, ticket: Ticket, role: Role, result: Result<Plane, Error>) {
        let Ok(mut s) = self.0.lock() else {
            return;
        };
        let Some(a) = s.active.as_mut() else {
            s.stale_callbacks = s.stale_callbacks.saturating_add(1);
            return;
        };
        if a.id != ticket.request_id
            || a.stage != Stage::Mapping
            || !a.metadata.as_ref().is_some_and(|m| ticket.matches(m))
        {
            s.stale_callbacks = s.stale_callbacks.saturating_add(1);
            return;
        }
        if !a.scope.mode.contains(role) {
            a.error.get_or_insert(Error::DuplicateRole);
            return;
        }
        if !a.received.insert(role) {
            a.error.get_or_insert(Error::DuplicateRole);
            return;
        }
        match result {
            Ok(p) if p.role == role && p.unpadded_bytes().is_ok() => a.planes.push(p),
            Ok(_) => {
                a.error.get_or_insert(Error::InvalidLayout);
            }
            Err(e) => {
                a.error.get_or_insert(e);
            }
        }
        if a.received.len() != a.scope.mode.count() {
            return;
        }
        let a = s.active.take().expect("matched active request");
        if let Some(e) = a.error {
            s.failure = Some((a.id, e));
            return;
        }
        let (Some(metadata), Some(final_encoding)) = (a.metadata, a.final_encoding) else {
            s.failure = Some((a.id, Error::MissingAfterHook));
            return;
        };
        let snapshot = Snapshot {
            request_id: a.id,
            mode: a.scope.mode,
            metadata,
            planes: a.planes,
            final_encoding,
        };
        if snapshot.complete() {
            s.snapshot = Some(snapshot);
        } else {
            s.failure = Some((a.id, Error::InvalidLayout));
        }
    }
}

/// Same three texture identities before and after late draws, not six textures.
#[derive(Clone, Copy)]
pub struct Inputs<'a> {
    pub velocity: &'a Texture,
    pub mask: &'a Texture,
    pub depth: &'a Texture,
}
impl<'a> Inputs<'a> {
    fn textures(self) -> [&'a Texture; 3] {
        [self.velocity, self.mask, self.depth]
    }
}
struct Job {
    ticket: Ticket,
    plane: Plane,
    buffer: Buffer,
}
#[derive(Resource, Default)]
pub struct Jobs(Mutex<Vec<Job>>);

pub fn register(app: &mut App) -> Result<(), String> {
    if app.world().contains_resource::<Observation>() {
        return Ok(());
    }
    if app.get_sub_app(RenderApp).is_none() {
        return Err("Base readback requires Bevy RenderApp".into());
    }
    let observation = Observation::default();
    app.insert_resource(observation.clone());
    app.sub_app_mut(RenderApp)
        .insert_resource(observation)
        .init_resource::<Jobs>()
        .add_systems(Render, map_after_submission.in_set(RenderSystems::Cleanup));
    Ok(())
}

/// AFTER the water render pass closes, BEFORE any late transparent pass opens.
/// Caller verifies full coverage/preparing errors before requesting this hook.
/// Inactive requests/other views are no-ops; failures affect diagnostics only.
pub fn before_late(
    observation: &Observation,
    jobs: &Jobs,
    context: &mut RenderContext,
    metadata: Metadata,
    inputs: Inputs<'_>,
) -> Result<Option<Ticket>, Error> {
    let pending = observation
        .0
        .lock()
        .map_err(|_| Error::StatePoisoned)?
        .active
        .as_ref()
        .map(|a| (a.id, a.scope, a.stage));
    let Some((id, scope, stage)) = pending else {
        return Ok(None);
    };
    if scope.view != metadata.tag().view {
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
        let textures = inputs.textures();
        if textures
            .iter()
            .map(|t| t.id())
            .collect::<HashSet<_>>()
            .len()
            != 3
        {
            return Err(Error::AliasedTexture);
        }
        let descriptions = BASE
            .into_iter()
            .zip(textures)
            .map(|(r, t)| describe(r, t, metadata.input_size))
            .collect::<Result<Vec<_>, _>>()?;
        let base_bytes = descriptions
            .iter()
            .try_fold(0_u64, |sum, p| sum.checked_add(plane_bytes(p)))
            .ok_or(Error::TooManyBytes)?;
        let final_bytes = if scope.mode == Mode::BaseAndFinal {
            base_bytes
        } else {
            0
        };
        let total = base_bytes
            .checked_add(final_bytes)
            .ok_or(Error::TooManyBytes)?;
        if total > MAX_BYTES {
            return Err(Error::TooManyBytes);
        }
        let ticket = Ticket {
            request_id: id,
            tag: metadata.tag(),
            generation: metadata.generation,
        };
        let mut pending_jobs = jobs.0.lock().map_err(|_| Error::StatePoisoned)?;
        {
            let mut s = observation.0.lock().map_err(|_| Error::StatePoisoned)?;
            let a = s
                .active
                .as_mut()
                .filter(|a| a.id == id && a.stage == Stage::Requested)
                .ok_or(Error::WrongTicket)?;
            a.metadata = Some(metadata);
            a.expected_base.clone_from(&descriptions);
            a.reserved_final_bytes = final_bytes;
            a.stage = Stage::BaseEncoded;
            s.in_flight_bytes = total;
        }
        for (p, t) in descriptions.into_iter().zip(textures) {
            encode_copy(&mut pending_jobs, context, ticket, p, t);
        }
        Ok(Some(ticket))
    })();
    if let Err(e) = &result {
        observation.fail(id, e.clone());
    }
    result
}

/// AFTER every late draw and actual aux depth/history copies + mark/commit.
/// Complete encoding remains mandatory in Resolve mode (auxAck may be false).
/// No readiness until Cleanup maps all required buffers and callbacks complete.
pub fn after_aux(
    observation: &Observation,
    jobs: &Jobs,
    context: &mut RenderContext,
    ticket: Ticket,
    completion: &AuxObservation,
    inputs: Inputs<'_>,
) -> Result<(), Error> {
    let result = (|| {
        let (mode, expected) = {
            let s = observation.0.lock().map_err(|_| Error::StatePoisoned)?;
            let a = s
                .active
                .as_ref()
                .filter(|a| a.id == ticket.request_id && a.stage == Stage::BaseEncoded)
                .ok_or(Error::WrongTicket)?;
            let m = a
                .metadata
                .as_ref()
                .filter(|m| ticket.matches(m))
                .ok_or(Error::WrongTicket)?;
            if !m.accepts_complete(completion) {
                return Err(Error::IncompleteAuxiliary);
            }
            (a.scope.mode, a.expected_base.clone())
        };
        let textures = inputs.textures();
        let descriptions = FINAL
            .into_iter()
            .zip(textures)
            .zip(expected)
            .map(|((r, t), base)| {
                let p = describe(r, t, base.size)?;
                if p.texture_id != base.texture_id
                    || p.format != base.format
                    || p.bytes_per_row != base.bytes_per_row
                {
                    return Err(Error::WrongTicket);
                }
                Ok(p)
            })
            .collect::<Result<Vec<_>, Error>>()?;
        let mut pending_jobs = jobs.0.lock().map_err(|_| Error::StatePoisoned)?;
        if mode == Mode::BaseAndFinal {
            for (p, t) in descriptions.into_iter().zip(textures) {
                encode_copy(&mut pending_jobs, context, ticket, p, t);
            }
        }
        let mut s = observation.0.lock().map_err(|_| Error::StatePoisoned)?;
        let a = s
            .active
            .as_mut()
            .filter(|a| a.id == ticket.request_id && a.stage == Stage::BaseEncoded)
            .ok_or(Error::WrongTicket)?;
        a.final_encoding = Some(completion.clone());
        a.reserved_final_bytes = 0;
        a.stage = Stage::Paired;
        Ok(())
    })();
    if let Err(e) = &result {
        observation.fail(ticket.request_id, e.clone());
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
fn plane_bytes(p: &Plane) -> u64 {
    u64::from(p.bytes_per_row) * u64::from(p.size.y)
}
fn encode_copy(
    jobs: &mut Vec<Job>,
    context: &mut RenderContext,
    ticket: Ticket,
    plane: Plane,
    texture: &Texture,
) {
    let buffer = context.render_device().create_buffer(&BufferDescriptor {
        label: Some("Tidewater auxiliary pre/post-late diagnostic readback"),
        size: plane_bytes(&plane),
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
    jobs.push(Job {
        ticket,
        plane,
        buffer,
    });
}
/// Bevy Cleanup occurs after command submission. No map-before-submit or poll.
#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy SystemParam wrappers are passed by value to a registered system"
)]
fn map_after_submission(jobs: Res<Jobs>, observation: Res<Observation>) {
    // Interior synchronization permits the public DrawMesh system's &World
    // read access. A ResMut<Jobs> would conflict with that SystemParam.
    let ready_jobs = if let Ok(mut pending) = jobs.0.lock() {
        std::mem::take(&mut *pending)
    } else {
        let id = observation
            .0
            .lock()
            .ok()
            .and_then(|s| s.active.as_ref().map(|a| a.id));
        if let Some(id) = id {
            observation.fail(id, Error::StatePoisoned);
        }
        return;
    };
    let transition = observation.0.lock().ok().and_then(|mut s| {
        let a = s.active.as_mut()?;
        match a.stage {
            Stage::Requested => Some((a.id, Error::MissingFrame)),
            Stage::BaseEncoded => Some((a.id, Error::MissingAfterHook)),
            Stage::Paired => {
                let count = ready_jobs
                    .iter()
                    .filter(|j| j.ticket.request_id == a.id)
                    .count();
                if count == a.scope.mode.count() {
                    a.stage = Stage::Mapping;
                    None
                } else {
                    Some((a.id, Error::MissingAfterHook))
                }
            }
            Stage::Mapping => None,
        }
    });
    if let Some((id, e)) = transition {
        observation.fail(id, e);
    }
    for job in ready_jobs {
        let buffer = job.buffer.clone();
        let shared = (*observation).clone();
        let role = job.plane.role;
        let reserved = plane_bytes(&job.plane);
        job.buffer
            .slice(..)
            .map_async(MapMode::Read, move |result| {
                let result = result
                    .map_err(|e| Error::MapFailed(e.to_string()))
                    .and_then(|()| {
                        let mapped = buffer.slice(..).get_mapped_range();
                        let result = if u64::try_from(mapped.len()).ok() == Some(reserved) {
                            let mut p = job.plane;
                            p.bytes = Arc::new(mapped.to_vec());
                            Ok(p)
                        } else {
                            Err(Error::InvalidLayout)
                        };
                        drop(mapped);
                        buffer.unmap();
                        result
                    });
                shared.release_bytes(reserved);
                shared.receive(job.ticket, role, result);
            });
    }
}

#[cfg(test)]
mod tests {
    // Artificial CPU byte fixtures test state/layout/filesystem only. They are
    // explicitly not recorded as mapped GPU data or source numeric qualification.
    use super::*;
    use crate::{
        tidewater_temporal::phase::AuxCoverage,
        tidewater_temporal_frame::{Input, Sequence, ViewKey},
    };
    use bevy::ecs::system::RunSystemOnce;
    #[test]
    fn readonly_jobs_can_share_the_real_drawmesh_world_system_parameter() {
        #[allow(
            clippy::needless_pass_by_value,
            reason = "The test initializes actual Bevy SystemParam wrappers passed by value"
        )]
        fn real_parameter_shapes(
            world: &World,
            _view: Query<&ViewPacket>,
            jobs: Option<Res<Jobs>>,
            observer: Option<Res<Observation>>,
        ) {
            assert!(world.contains_resource::<Jobs>());
            assert!(jobs.unwrap().0.lock().unwrap().is_empty());
            assert!(observer.is_some());
        }
        let mut world = World::new();
        world.insert_resource(Jobs::default());
        world.insert_resource(Observation::default());
        world.run_system_once(real_parameter_shapes).unwrap();
    }
    fn metadata() -> Metadata {
        let view = Entity::from_raw_u32(7).unwrap();
        let frame = *Sequence::new(ViewKey(view.to_bits()))
            .prepare(Input {
                world_from_view: Mat4::IDENTITY,
                clip_from_view: Mat4::perspective_infinite_reverse_rh(1.0, 1.0, 0.1),
                input_size: UVec2::ONE,
                output_size: UVec2::ONE,
                near: 0.1,
                far: 1000.0,
            })
            .unwrap()
            .frame();
        Metadata {
            view_packet: ViewPacket {
                packet: Packet::from_frame(&frame),
                tag: FrameTag { view, serial: 1 },
                frame,
            },
            generation: 3,
            input_size: UVec2::ONE,
            output_size: UVec2::ONE,
            boundary: Boundary {
                opaque: 2,
                water: 1,
                transparent: 2,
                queued: 5,
                base_mesh_draws: 3,
                sky_draws: 1,
                hull_draws: 0,
                hull_contract_satisfied: true,
            },
        }
    }
    fn completion(m: &Metadata) -> AuxObservation {
        AuxObservation {
            tag: m.tag(),
            generation: m.generation,
            coverage: AuxCoverage {
                visible: 5,
                registered: 5,
                queued: 5,
                opaque: 2,
                water: 1,
                transparent: 2,
                ..default()
            },
            mesh_draws: 5,
            sky_draws: 1,
            hull_draws: 0,
            hull_contract_satisfied: true,
            final_depth_copied: true,
            previous_depth_copied: true,
            history_seeded: true,
            auxiliary_acknowledged: false,
            draw_diagnostics: Vec::new(),
            draw_diagnostics_omitted: 0,
            errors: Vec::new(),
        }
    }
    fn scope(m: &Metadata, mode: Mode) -> Scope {
        Scope {
            view: m.tag().view,
            minimum_serial: 1,
            history_epoch: m.history_epoch(),
            expected_generation: Some(3),
            mode,
        }
    }
    fn plane(role: Role) -> Plane {
        let (row_bytes, bytes_per_row, total) = layout(role, UVec2::ONE).unwrap();
        Plane {
            role,
            format: role.format(),
            size: UVec2::ONE,
            texture_id: role.base().name().into(),
            row_bytes,
            bytes_per_row,
            bytes: Arc::new(vec![0; usize::try_from(total).unwrap()]),
        }
    }
    fn mapping(mode: Mode) -> (Observation, Ticket) {
        let m = metadata();
        let o = Observation::default();
        let id = o.request(scope(&m, mode)).unwrap();
        let ticket = Ticket {
            request_id: id,
            tag: m.tag(),
            generation: m.generation,
        };
        let mut s = o.0.lock().unwrap();
        let a = s.active.as_mut().unwrap();
        a.stage = Stage::Mapping;
        a.final_encoding = Some(completion(&m));
        a.metadata = Some(m);
        drop(s);
        (o, ticket)
    }
    #[test]
    fn base_requires_three_distinct_callbacks_and_actual_pair() {
        let (o, t) = mapping(Mode::BaseOnly);
        for r in [Role::BaseVelocity, Role::BaseMask] {
            o.receive(t, r, Ok(plane(r)));
        }
        assert!(o.snapshot().unwrap().is_none());
        o.receive(t, Role::BaseDepth, Ok(plane(Role::BaseDepth)));
        let s = o.snapshot().unwrap().unwrap();
        assert!(s.complete());
        assert!(!s.final_encoding.auxiliary_acknowledged); // Resolve mode is allowed.
        let (o, t) = mapping(Mode::BaseOnly);
        o.0.lock().unwrap().active.as_mut().unwrap().final_encoding = None;
        for r in BASE {
            o.receive(t, r, Ok(plane(r)));
        }
        assert!(matches!(
            o.status().unwrap(),
            Status::Failed {
                error: Error::MissingAfterHook,
                ..
            }
        ));
    }
    #[test]
    fn final_requires_six_callbacks_and_same_three_texture_identities() {
        let (o, t) = mapping(Mode::BaseAndFinal);
        for r in BASE {
            o.receive(t, r, Ok(plane(r)));
        }
        assert!(o.snapshot().unwrap().is_none());
        for r in FINAL {
            o.receive(t, r, Ok(plane(r)));
        }
        assert!(o.snapshot().unwrap().unwrap().complete());
        let (o, t) = mapping(Mode::BaseAndFinal);
        for r in BASE.into_iter().chain(FINAL) {
            let mut p = plane(r);
            if r == Role::FinalDepth {
                p.texture_id = "different-depth-target".into();
            }
            o.receive(t, r, Ok(p));
        }
        assert!(matches!(
            o.status().unwrap(),
            Status::Failed {
                error: Error::InvalidLayout,
                ..
            }
        ));
    }
    #[test]
    fn stale_duplicate_and_map_error_never_promote() {
        let (o, t) = mapping(Mode::BaseOnly);
        let mut stale = t;
        stale.tag.serial += 1;
        o.receive(stale, Role::BaseDepth, Ok(plane(Role::BaseDepth)));
        assert_eq!(o.stale_callbacks().unwrap(), 1);
        o.receive(t, Role::BaseVelocity, Ok(plane(Role::BaseVelocity)));
        o.receive(t, Role::BaseVelocity, Ok(plane(Role::BaseVelocity)));
        for r in [Role::BaseMask, Role::BaseDepth] {
            o.receive(t, r, Ok(plane(r)));
        }
        assert!(matches!(
            o.status().unwrap(),
            Status::Failed {
                error: Error::DuplicateRole,
                ..
            }
        ));
        let (o, t) = mapping(Mode::BaseOnly);
        o.receive(
            t,
            Role::BaseVelocity,
            Err(Error::MapFailed("actual mapping error fixture".into())),
        );
        for r in [Role::BaseMask, Role::BaseDepth] {
            o.receive(t, r, Ok(plane(r)));
        }
        assert!(matches!(
            o.status().unwrap(),
            Status::Failed {
                error: Error::MapFailed(_),
                ..
            }
        ));
    }
    #[test]
    fn partial_base_or_final_draws_and_frame_scope_are_refused() {
        let mut m = metadata();
        m.validate().unwrap();
        assert_eq!(m.uniform_bytes().unwrap().len(), 336);
        let good = completion(&m);
        assert!(m.accepts_complete(&good));
        for bad in 0..5 {
            let mut c = good.clone();
            match bad {
                0 => c.mesh_draws -= 1,
                1 => c.coverage.unsupported = 1,
                2 => c.generation += 1,
                3 => c.tag.serial += 1,
                _ => c.previous_depth_copied = false,
            }
            assert!(!m.accepts_complete(&c));
        }
        m.boundary.base_mesh_draws -= 1;
        assert_eq!(m.validate(), Err(Error::InvalidBoundary));
        let mut m = metadata();
        let scope = scope(&m, Mode::BaseOnly);
        assert!(scope.accepts(&m));
        m.generation += 1;
        assert!(!scope.accepts(&m));
        let mut m = metadata();
        m.view_packet.packet.camera_position.x = 1.0;
        assert_eq!(m.validate(), Err(Error::InvalidMetadata));
    }
    #[test]
    fn missing_after_hook_and_failed_buffer_budget_are_explicit() {
        let (o, t) = mapping(Mode::BaseAndFinal);
        {
            let mut s = o.0.lock().unwrap();
            s.in_flight_bytes = 6 * 256;
            let a = s.active.as_mut().unwrap();
            a.stage = Stage::BaseEncoded;
            a.reserved_final_bytes = 3 * 256;
        }
        let mut world = World::new();
        world.insert_resource(o.clone());
        world.insert_resource(Jobs::default());
        world.run_system_once(map_after_submission).unwrap();
        assert!(matches!(
            o.status().unwrap(),
            Status::Failed {
                error: Error::MissingAfterHook,
                ..
            }
        ));
        assert_eq!(o.0.lock().unwrap().in_flight_bytes, 3 * 256);
        assert_eq!(
            o.request(scope(&metadata(), Mode::BaseOnly)),
            Err(Error::Busy)
        );
        for r in BASE {
            o.release_bytes(256);
            o.receive(t, r, Ok(plane(r)));
        }
        assert_eq!(o.stale_callbacks().unwrap(), 3);
        assert!(o.request(scope(&metadata(), Mode::BaseOnly)).is_ok());
    }
    #[test]
    fn row_padding_is_removed_exactly_and_bad_lengths_rejected() {
        let mut p = plane(Role::BaseVelocity);
        let mut bytes = vec![0xcd; 256];
        bytes[..8].copy_from_slice(&[1, 2, 3, 4, 5, 6, 7, 8]);
        p.bytes = Arc::new(bytes);
        assert_eq!(p.unpadded_bytes().unwrap(), vec![1, 2, 3, 4, 5, 6, 7, 8]);
        p.bytes = Arc::new(vec![0; 255]);
        assert_eq!(p.unpadded_bytes(), Err(Error::InvalidLayout));
        assert_eq!(
            layout(Role::BaseVelocity, UVec2::new(u32::MAX, 1)),
            Err(Error::InvalidLayout)
        );
    }
    #[test]
    fn alias_and_overwrite_are_refused_in_cpu_filesystem_fixture() {
        let m = metadata();
        let mut s = Snapshot {
            request_id: 1,
            mode: Mode::BaseOnly,
            final_encoding: completion(&m),
            metadata: m,
            planes: BASE.into_iter().map(plane).collect(),
        };
        s.planes[1].texture_id = s.planes[0].texture_id.clone();
        assert!(!s.complete());
        s.planes[1] = plane(Role::BaseMask);
        assert!(s.complete());
        let root = option_env!("FORGE_TIDEWATER_BASE_READBACK_CPU_DIR")
            .map(std::path::PathBuf::from)
            .unwrap_or_else(|| std::env::temp_dir().join("forge-temporal-base-readback-cpu"));
        fs::create_dir_all(&root).unwrap();
        let unique = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        let path = root.join(format!(
            "cpu-artificial-bytes-{}-{unique}",
            std::process::id()
        ));
        s.write_new_directory(&path).unwrap();
        let original = fs::read(path.join("packet336.bin")).unwrap();
        assert_eq!(
            s.write_new_directory(&path).unwrap_err().kind(),
            std::io::ErrorKind::AlreadyExists
        );
        assert_eq!(fs::read(path.join("packet336.bin")).unwrap(), original);
    }
}
