// SPDX-License-Identifier: MIT OR Apache-2.0
//! Opt-in, bounded GPU readback of one complete real auxiliary frame.
//!
//! `CachedTexture` is not a `Handle<Image>`, so the public Readback component cannot
//! name these allocations. This follows Bevy `gpu_readback`'s public encoder /
//! Cleanup `map_async` order instead. Completion requires all six GPU callbacks.
use super::{
    frame::{EncodingOwner, FrameTag, Frames, Packet, ViewPacket},
    render::{AuxObservations, encode},
    resolve::{ResolveObservation, ResolveObservations},
    targets::AuxTargets,
};
use bevy::{
    core_pipeline::schedule::{Core3d, Core3dSystems},
    prelude::*,
    render::{
        Render, RenderApp, RenderSystems,
        render_resource::{
            Buffer, BufferDescriptor, BufferUsages, MapMode, TexelCopyBufferInfo,
            TexelCopyBufferLayout, Texture, TextureAspect, TextureFormat, TextureUsages,
        },
        renderer::{RenderContext, ViewQuery},
        sync_world::MainEntity,
        view::ViewDepthTexture,
    },
};
use std::{
    collections::HashSet,
    fmt,
    sync::{Arc, Mutex},
};

const PLANES: usize = 6;
const MAX_BYTES: u64 = 512 * 1024 * 1024;

#[derive(Clone, Copy, Debug, Hash, PartialEq, Eq)]
pub enum Role {
    Velocity,
    Mask,
    AuxiliaryDepth,
    CurrentFinalDepth,
    PreviousDepth,
    MainDepth,
}

impl Role {
    const fn format(self) -> TextureFormat {
        match self {
            Self::Velocity => TextureFormat::Rgba16Float,
            Self::Mask => TextureFormat::Rgba8Unorm,
            _ => TextureFormat::Depth32Float,
        }
    }
    const fn pixel_bytes(self) -> u32 {
        if matches!(self, Self::Velocity) { 8 } else { 4 }
    }
    const fn depth(self) -> bool {
        !matches!(self, Self::Velocity | Self::Mask)
    }
}

#[derive(Clone, Debug)]
pub struct Plane {
    pub role: Role,
    pub format: TextureFormat,
    pub size: UVec2,
    pub texture_id: String,
    /// Actual row payload; padding is excluded by `unpadded_bytes()`.
    pub row_bytes: u32,
    /// WebGPU copy stride, aligned to 256 bytes.
    pub bytes_per_row: u32,
    /// Actual GPU-mapped bytes, including row padding.
    pub bytes: Arc<Vec<u8>>,
}

impl Plane {
    pub fn unpadded_bytes(&self) -> Result<Vec<u8>, Error> {
        let stride = usize::try_from(self.bytes_per_row).map_err(|_| Error::InvalidLayout)?;
        let row = usize::try_from(self.row_bytes).map_err(|_| Error::InvalidLayout)?;
        let height = usize::try_from(self.size.y).map_err(|_| Error::InvalidLayout)?;
        if row > stride
            || self.bytes.len() != stride.checked_mul(height).ok_or(Error::InvalidLayout)?
        {
            return Err(Error::InvalidLayout);
        }
        let capacity = row.checked_mul(height).ok_or(Error::InvalidLayout)?;
        let mut bytes = Vec::with_capacity(capacity);
        for source in self.bytes.chunks_exact(stride) {
            bytes.extend_from_slice(&source[..row]);
        }
        Ok(bytes)
    }
}

#[derive(Clone, Debug)]
pub struct Snapshot {
    pub request_id: u64,
    pub tag: FrameTag,
    pub generation: u64,
    pub input_size: UVec2,
    pub output_size: UVec2,
    /// Immutable CPU inputs bound for the exact frame copied to these buffers.
    /// GPU callbacks may complete several render frames after this snapshot.
    pub frame: crate::tidewater_temporal_frame::Frame,
    pub packet: Packet,
    /// Exact source resolve command observation at texture-copy encoding time.
    /// This metadata is not a readback of the HDR color or a GPU fence.
    pub resolve_encoding: Option<ResolveObservation>,
    /// Every plane completed `map_async` successfully after submission.
    pub planes: Vec<Plane>,
}

impl Snapshot {
    #[must_use]
    pub fn plane(&self, role: Role) -> Option<&Plane> {
        self.planes.iter().find(|plane| plane.role == role)
    }
    #[must_use]
    pub fn complete(&self) -> bool {
        self.planes.len() == PLANES
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
                .all(|plane| plane.size == self.input_size)
    }
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub enum Error {
    NotRegistered,
    Busy,
    NoActiveView,
    WrongView,
    CaptureView,
    NoCompleteAuxFrame,
    MissingFrame,
    StaleFrame,
    MissingTargets,
    IncompleteAuxFrame,
    NoCompleteResolveFrame,
    IncompleteResolveFrame,
    ResolveFrameMismatch,
    WrongEncodingOwner,
    InvalidTexture,
    AliasedTextures,
    InvalidLayout,
    TooLarge,
    CounterExhausted,
    StatePoisoned,
    MapFailed(String),
}

impl fmt::Display for Error {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::MapFailed(message) => write!(formatter, "GPU readback map failed: {message}"),
            other => write!(formatter, "Temporal GPU observation: {other:?}"),
        }
    }
}
impl std::error::Error for Error {}

#[derive(Clone, Debug)]
pub struct Failure {
    pub request_id: u64,
    pub view: Entity,
    pub tag: Option<FrameTag>,
    pub error: Error,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Stage {
    Requested,
    CopiesEncoded,
    Mapping,
}

#[derive(Clone, Debug)]
#[allow(
    clippy::large_enum_variant,
    reason = "Complete retains the public snapshot payload for diagnostics without boxing"
)]
pub enum Status {
    Idle,
    Pending {
        request_id: u64,
        view: Entity,
        stage: Stage,
        tag: Option<FrameTag>,
        received_planes: usize,
    },
    Complete(Snapshot),
    Failed(Failure),
}

#[derive(Clone, Copy)]
struct Request {
    id: u64,
    view: Entity,
    baseline_tag: FrameTag,
    generation: u64,
    owner: EncodingOwner,
    history_epoch: Option<u64>,
    resolve_generation: Option<u64>,
}
struct Active {
    request: Request,
    stage: Stage,
    tag: Option<FrameTag>,
    input: UVec2,
    output: UVec2,
    received: usize,
    planes: Vec<Plane>,
    error: Option<Error>,
    packet: Option<ViewPacket>,
    resolve_encoding: Option<ResolveObservation>,
}
#[derive(Default)]
struct State {
    next_id: u64,
    active: Option<Active>,
    snapshot: Option<Snapshot>,
    failure: Option<Failure>,
    stale_callbacks: u64,
}

/// Shared `MainWorld` / `RenderWorld` handle. Idle means there is no readback cost.
#[derive(Resource, Clone, Default)]
pub struct Observation(Arc<Mutex<State>>);

impl Observation {
    pub fn status(&self) -> Result<Status, Error> {
        let state = self.0.lock().map_err(|_| Error::StatePoisoned)?;
        Ok(if let Some(active) = &state.active {
            Status::Pending {
                request_id: active.request.id,
                view: active.request.view,
                stage: active.stage,
                tag: active.tag,
                received_planes: active.received,
            }
        } else if let Some(snapshot) = &state.snapshot {
            Status::Complete(snapshot.clone())
        } else if let Some(failure) = &state.failure {
            Status::Failed(failure.clone())
        } else {
            Status::Idle
        })
    }
    #[must_use]
    pub fn snapshot(&self) -> Option<Snapshot> {
        self.0.lock().ok()?.snapshot.clone()
    }
    pub fn stale_callbacks(&self) -> Result<u64, Error> {
        Ok(self
            .0
            .lock()
            .map_err(|_| Error::StatePoisoned)?
            .stale_callbacks)
    }

    fn fail(&self, request: Request, tag: Option<FrameTag>, error: Error) {
        if let Ok(mut state) = self.0.lock()
            && state
                .active
                .as_ref()
                .is_some_and(|active| active.request.id == request.id)
        {
            state.active = None;
            state.failure = Some(Failure {
                request_id: request.id,
                view: request.view,
                tag,
                error,
            });
        }
    }

    fn receive(&self, request: Request, tag: FrameTag, result: Result<Plane, Error>) {
        let Ok(mut state) = self.0.lock() else {
            return;
        };
        let Some(active) = &mut state.active else {
            state.stale_callbacks += 1;
            return;
        };
        if active.request.id != request.id
            || active.tag != Some(tag)
            || active.stage != Stage::Mapping
        {
            state.stale_callbacks += 1;
            return;
        }
        active.received += 1;
        match result {
            Ok(plane) => {
                if active
                    .planes
                    .iter()
                    .any(|existing| existing.role == plane.role)
                {
                    active.error = Some(Error::StaleFrame);
                } else {
                    active.planes.push(plane);
                }
            }
            Err(error) => {
                if active.error.is_none() {
                    active.error = Some(error);
                }
            }
        }
        if active.received == PLANES {
            let active = state.active.take().expect("Active readback existed");
            if let Some(error) = active.error {
                state.failure = Some(Failure {
                    request_id: request.id,
                    view: request.view,
                    tag: Some(tag),
                    error,
                });
            } else {
                let Some(packet) = active.packet else {
                    state.failure = Some(Failure {
                        request_id: request.id,
                        view: request.view,
                        tag: Some(tag),
                        error: Error::MissingFrame,
                    });
                    return;
                };
                state.snapshot = Some(Snapshot {
                    request_id: request.id,
                    tag,
                    generation: request.generation,
                    input_size: active.input,
                    output_size: active.output,
                    frame: packet.frame,
                    packet: packet.packet,
                    resolve_encoding: active.resolve_encoding,
                    planes: active.planes,
                });
            }
        }
    }
}

/// Select the next complete encoded frame of the already-active main view.
/// One request is allowed at a time; a caller should hold its camera while probing.
#[allow(
    clippy::cast_possible_truncation,
    reason = "The phase epoch ABI intentionally stores the u64 as two u32 words"
)]
pub fn request(world: &mut World, main_view: Entity) -> Result<u64, Error> {
    let observation = world
        .get_resource::<Observation>()
        .ok_or(Error::NotRegistered)?
        .clone();
    let frames = world.get_resource::<Frames>().ok_or(Error::NoActiveView)?;
    let selected = frames.selected_view().ok_or(Error::NoActiveView)?;
    let owner = frames.owner();
    if selected != main_view {
        return Err(Error::WrongView);
    }
    if world
        .get::<crate::tidewater_refraction::CaptureCamera>(main_view)
        .is_some()
    {
        return Err(Error::CaptureView);
    }
    if !world
        .get::<Camera>(main_view)
        .is_some_and(|camera| camera.is_active)
    {
        return Err(Error::NoActiveView);
    }
    let baseline = world
        .get_resource::<AuxObservations>()
        .and_then(|observations| observations.snapshot(main_view))
        .filter(|encoded| {
            encoded.complete()
                && (owner == EncodingOwner::Resolve || encoded.auxiliary_acknowledged)
        })
        .ok_or(Error::NoCompleteAuxFrame)?;
    let baseline_resolve = if owner == EncodingOwner::Resolve {
        let resolved = world
            .get_resource::<ResolveObservations>()
            .and_then(|observations| observations.snapshot(main_view))
            .filter(ResolveObservation::complete)
            .ok_or(Error::NoCompleteResolveFrame)?;
        let main_packet = world
            .get::<ViewPacket>(main_view)
            .ok_or(Error::MissingFrame)?;
        if resolved.tag != baseline.tag
            || resolved.auxiliary_generation != baseline.generation
            || resolved.history_epoch != main_packet.frame.history_epoch
            || resolved.bound_packet.as_ref().is_none_or(|packet| {
                packet.phase_epoch_seed.y != resolved.history_epoch as u32
                    || packet.phase_epoch_seed.z != (resolved.history_epoch >> 32) as u32
            })
        {
            return Err(Error::ResolveFrameMismatch);
        }
        Some(resolved)
    } else {
        None
    };
    let mut state = observation.0.lock().map_err(|_| Error::StatePoisoned)?;
    if state.active.is_some() {
        return Err(Error::Busy);
    }
    let id = state
        .next_id
        .checked_add(1)
        .ok_or(Error::CounterExhausted)?;
    state.next_id = id;
    state.snapshot = None;
    state.failure = None;
    state.active = Some(Active {
        request: Request {
            id,
            view: main_view,
            baseline_tag: baseline.tag,
            generation: baseline.generation,
            owner,
            history_epoch: baseline_resolve
                .as_ref()
                .map(|resolved| resolved.history_epoch),
            resolve_generation: baseline_resolve
                .as_ref()
                .map(|resolved| resolved.generation),
        },
        stage: Stage::Requested,
        tag: None,
        input: UVec2::ZERO,
        output: UVec2::ZERO,
        received: 0,
        planes: Vec::with_capacity(PLANES),
        error: None,
        packet: None,
        resolve_encoding: None,
    });
    Ok(id)
}

struct Job {
    request: Request,
    tag: FrameTag,
    plane: Plane,
    buffer: Buffer,
}
#[derive(Resource, Default)]
pub(crate) struct Jobs(Vec<Job>);

pub fn register(app: &mut App) -> Result<(), String> {
    if app.world().contains_resource::<Observation>() {
        return Ok(());
    }
    if app.get_sub_app(RenderApp).is_none() {
        return Err("Temporal GPU observation requires Bevy RenderApp".into());
    }
    let observation = Observation::default();
    app.insert_resource(observation.clone());
    app.sub_app_mut(RenderApp)
        .insert_resource(observation)
        .init_resource::<Jobs>()
        .add_systems(
            Core3d,
            encode_reads
                .after(encode)
                .after(super::resolve::encode)
                .before(Core3dSystems::EarlyPostProcess),
        )
        .add_systems(Render, map_after_submission.in_set(RenderSystems::Cleanup));
    Ok(())
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::too_many_lines,
    clippy::type_complexity,
    reason = "This registered Bevy render system owns the ordered readback transaction"
)]
pub(crate) fn encode_reads(
    view: ViewQuery<(
        Option<&MainEntity>,
        Option<&ViewPacket>,
        Option<&AuxTargets>,
        Option<&ViewDepthTexture>,
    )>,
    observation: Res<Observation>,
    encoded: Res<AuxObservations>,
    owner: Option<Res<EncodingOwner>>,
    resolved: Option<Res<ResolveObservations>>,
    mut jobs: ResMut<Jobs>,
    mut context: RenderContext,
) {
    let request = match observation.0.lock() {
        Ok(state) => state
            .active
            .as_ref()
            .filter(|active| active.stage == Stage::Requested)
            .map(|active| active.request),
        Err(_) => return,
    };
    let Some(request) = request else {
        return;
    };
    let (main_entity, packet, targets, main_depth) = view.into_inner();
    let actual_view = packet
        .map(|packet| packet.tag.view)
        .or_else(|| main_entity.map(MainEntity::id));
    if actual_view != Some(request.view) {
        return;
    }
    let Some(packet) = packet else {
        observation.fail(request, None, Error::MissingFrame);
        return;
    };
    let Some(targets) = targets else {
        observation.fail(request, Some(packet.tag), Error::MissingTargets);
        return;
    };
    let Some(main_depth) = main_depth else {
        observation.fail(request, Some(packet.tag), Error::InvalidTexture);
        return;
    };
    if packet.tag.serial < request.baseline_tag.serial || targets.generation != request.generation {
        observation.fail(request, Some(packet.tag), Error::StaleFrame);
        return;
    }
    if owner.as_deref().copied().unwrap_or_default() != request.owner {
        observation.fail(request, Some(packet.tag), Error::WrongEncodingOwner);
        return;
    }
    let valid = encoded.snapshot(request.view).is_some_and(|frame| {
        frame.tag == packet.tag
            && frame.generation == targets.generation
            && frame.complete()
            && (request.owner == EncodingOwner::Resolve || frame.auxiliary_acknowledged)
    }) && targets.was_aux_encoded(packet.tag).unwrap_or(false);
    if !valid {
        observation.fail(request, Some(packet.tag), Error::IncompleteAuxFrame);
        return;
    }
    let resolve_encoding = if request.owner == EncodingOwner::Resolve {
        let Some(resolved) = resolved
            .as_ref()
            .and_then(|observations| observations.snapshot(request.view))
        else {
            observation.fail(request, Some(packet.tag), Error::IncompleteResolveFrame);
            return;
        };
        if !resolved.complete() {
            observation.fail(request, Some(packet.tag), Error::IncompleteResolveFrame);
            return;
        }
        if resolved.tag != packet.tag
            || resolved.auxiliary_generation != targets.generation
            || Some(resolved.generation) != request.resolve_generation
            || resolved.history_epoch != packet.frame.history_epoch
            || Some(resolved.history_epoch) != request.history_epoch
            || resolved.bound_packet.as_ref() != Some(&packet.packet)
            || resolved.input_size != targets.input_size
            || resolved.output_size != targets.output_size
        {
            observation.fail(request, Some(packet.tag), Error::ResolveFrameMismatch);
            return;
        }
        Some(resolved)
    } else {
        None
    };
    let textures = [
        (Role::Velocity, &targets.velocity.texture),
        (Role::Mask, &targets.mask.texture),
        (Role::AuxiliaryDepth, &targets.depth_aux.texture),
        (
            Role::CurrentFinalDepth,
            &targets.current_final_depth.texture,
        ),
        (Role::PreviousDepth, &targets.previous_depth.texture),
        (Role::MainDepth, &main_depth.texture),
    ];
    let mut ids = HashSet::new();
    let mut total = 0_u64;
    let mut prepared = Vec::with_capacity(PLANES);
    for (role, texture) in textures {
        if !ids.insert(texture.id()) {
            observation.fail(request, Some(packet.tag), Error::AliasedTextures);
            return;
        }
        let plane = match describe(role, texture, targets.input_size) {
            Ok(plane) => plane,
            Err(error) => {
                observation.fail(request, Some(packet.tag), error);
                return;
            }
        };
        let size = u64::from(plane.bytes_per_row) * u64::from(plane.size.y);
        let Some(next_total) = total.checked_add(size) else {
            observation.fail(request, Some(packet.tag), Error::TooLarge);
            return;
        };
        total = next_total;
        if total > MAX_BYTES {
            observation.fail(request, Some(packet.tag), Error::TooLarge);
            return;
        }
        prepared.push((plane, texture));
    }
    let Ok(mut state) = observation.0.lock() else {
        return;
    };
    let Some(active) = &mut state.active else {
        return;
    };
    if active.request.id != request.id || active.stage != Stage::Requested {
        return;
    }
    active.tag = Some(packet.tag);
    active.packet = Some(packet.clone());
    active.resolve_encoding = resolve_encoding;
    active.input = targets.input_size;
    active.output = targets.output_size;
    active.stage = Stage::CopiesEncoded;
    for (plane, texture) in prepared {
        let buffer = context.render_device().create_buffer(&BufferDescriptor {
            label: Some("Tidewater temporal one-shot GPU observation"),
            size: u64::from(plane.bytes_per_row) * u64::from(plane.size.y),
            usage: BufferUsages::COPY_DST | BufferUsages::MAP_READ,
            mapped_at_creation: false,
        });
        let mut source = texture.as_image_copy();
        source.aspect = if plane.role.depth() {
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
            request,
            tag: packet.tag,
            plane,
            buffer,
        });
    }
}

fn describe(role: Role, texture: &Texture, size: UVec2) -> Result<Plane, Error> {
    let extent = texture.size();
    if size.min_element() == 0
        || extent.width != size.x
        || extent.height != size.y
        || extent.depth_or_array_layers != 1
        || texture.sample_count() != 1
        || texture.format() != role.format()
        || !texture.usage().contains(TextureUsages::COPY_SRC)
    {
        return Err(Error::InvalidTexture);
    }
    let row_bytes = size
        .x
        .checked_mul(role.pixel_bytes())
        .ok_or(Error::InvalidLayout)?;
    let bytes_per_row = row_bytes.checked_add(255).ok_or(Error::InvalidLayout)? / 256 * 256;
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

/// Cleanup follows render-system submission, exactly as Bevy `GpuReadbackPlugin`.
/// The callback reads bytes only after the GPU map succeeds; nothing blocks/polls here.
#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy Cleanup systems receive resource wrappers by value"
)]
fn map_after_submission(mut jobs: ResMut<Jobs>, observation: Res<Observation>) {
    if jobs.0.is_empty() {
        let unrendered = observation.0.lock().ok().and_then(|state| {
            state
                .active
                .as_ref()
                .filter(|active| active.stage == Stage::Requested)
                .map(|active| active.request)
        });
        if let Some(request) = unrendered {
            observation.fail(request, None, Error::MissingFrame);
        }
        return;
    }
    if let Ok(mut state) = observation.0.lock()
        && let Some(active) = &mut state.active
        && active.stage == Stage::CopiesEncoded
    {
        active.stage = Stage::Mapping;
    }
    for job in jobs.0.drain(..) {
        let buffer = job.buffer.clone();
        let shared = (*observation).clone();
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
                shared.receive(job.request, job.tag, result);
            });
    }
}
