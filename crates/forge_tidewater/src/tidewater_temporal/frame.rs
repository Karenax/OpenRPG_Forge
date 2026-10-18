// SPDX-License-Identifier: MIT
// Derived from Tidewater Frame.js, CameraVelocity.js and TemporalUpscale.js at
// 1438b1abfcaee3267092b75573014f4d9b4a983c.
// Copyright 2026 DRG Software Solutions LLC; see third_party/tidewater/LICENSE.
//! Explicitly selected main-view packets and auxiliary-encoding ownership.
//!
//! Source Halton32 and immutable preparation are provided by the pure 187 module.
//! Only an accepted `RenderWorld` acknowledgement advances that sequence. This
//! first adapter commits AUXILIARY encoding, not a source temporal resolve. A
//! future resolve must replace this transition with its own final-resolve ack.
//!
//! Drain acknowledgements before preparation, after camera computation/transform
//! propagation and before refraction synchronization. The current pending token
//! is strict: replacing it makes earlier callbacks stale. The initial candidate
//! must disable pipelined rendering and prove matching main/refraction/aux tags.
//! Production asynchronous ownership remains a separate `RenderWorld` integration;
//! retaining a packet while letting the camera move would be incoherent.

use crate::{
    tidewater_refraction::CaptureCamera,
    tidewater_temporal_frame::{
        Error as TemporalError, Frame, Input, PreparedFrame, Sequence, ViewKey,
    },
};
use bevy::{
    camera::Projection,
    prelude::*,
    render::{
        extract_component::ExtractComponent, extract_resource::ExtractResource,
        render_resource::ShaderType,
    },
};
use std::{
    collections::{HashMap, VecDeque},
    error::Error as StdError,
    fmt,
    sync::{Arc, Mutex},
};

/// Group-1/binding-0 ABI: 4 matrices (64 bytes each), then 5 aligned vec4s.
/// The shader must use the same field order. Total `ShaderType` size is 336 bytes.
#[derive(Clone, Debug, PartialEq, ShaderType)]
pub struct Packet {
    pub current_unjittered_clip_from_world: Mat4,
    pub previous_unjittered_clip_from_world: Mat4,
    pub current_world_from_jittered_clip: Mat4,
    pub previous_world_from_jittered_clip: Mat4,
    pub camera_position: Vec4,
    pub previous_camera_position: Vec4,
    /// Internal XY, output ZW in physical pixels.
    pub sizes: UVec4,
    /// Near, far, source jitter X/Y in internal pixels.
    pub near_far_jitter: Vec4,
    /// Halton phase, epoch low/high, flags: bit0 seed, bit1 current rigid models.
    /// Legacy preparations use flags 0/1. Render-owned reset/gap seeds use 3.
    pub phase_epoch_seed: UVec4,
}

impl Packet {
    #[must_use]
    #[allow(
        clippy::cast_possible_truncation,
        reason = "Split an exact u64 epoch into its low/high GPU words"
    )]
    pub fn from_frame(frame: &Frame) -> Self {
        Self {
            current_unjittered_clip_from_world: frame.unjittered_clip_from_world,
            previous_unjittered_clip_from_world: frame.previous_unjittered_clip_from_world,
            current_world_from_jittered_clip: frame.world_from_clip,
            previous_world_from_jittered_clip: frame.previous_world_from_clip,
            camera_position: frame.camera_position.extend(1.0),
            previous_camera_position: frame.previous_camera_position.extend(1.0),
            sizes: UVec4::new(
                frame.input_size.x,
                frame.input_size.y,
                frame.output_size.x,
                frame.output_size.y,
            ),
            near_far_jitter: Vec4::new(
                frame.near_far.x,
                frame.near_far.y,
                frame.jitter_pixels.x,
                frame.jitter_pixels.y,
            ),
            phase_epoch_seed: UVec4::new(
                u32::from(frame.phase),
                frame.history_epoch as u32,
                (frame.history_epoch >> 32) as u32,
                u32::from(frame.needs_history_seed),
            ),
        }
    }
}

/// Main-world view identity and monotone preparation identity; not an RAF number.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct FrameTag {
    pub view: Entity,
    pub serial: u64,
}

#[derive(Component, Clone, Debug, ExtractComponent)]
#[extract_component_filter(Without<CaptureCamera>)]
pub struct ViewPacket {
    pub packet: Packet,
    pub tag: FrameTag,
    pub frame: Frame,
}

/// Which actual encoded pass owns the sequence; never inferred from readiness.
#[derive(Resource, Clone, Copy, Debug, Default, PartialEq, Eq)]
pub enum EncodingOwner {
    #[default]
    Auxiliary,
    Resolve,
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy Extract systems receive resource wrappers by value"
)]
pub fn extract_encoding_owner(mut commands: Commands, frames: bevy::render::Extract<Res<Frames>>) {
    commands.insert_resource(frames.owner());
}

#[derive(SystemSet, Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum FrameSystems {
    Acknowledge,
    Prepare,
}

/// Actual command-encoding counters supplied by the auxiliary render pass.
/// Neither this value nor acknowledgement implies GPU completion/readback validity.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct AuxEncoding {
    pub draw_count: u32,
    pub final_depth_copied: bool,
}

#[derive(Clone, Copy, Debug)]
pub struct AuxAcknowledgement {
    tag: FrameTag,
    encoding: AuxEncoding,
}

impl AuxAcknowledgement {
    #[must_use]
    pub const fn tag(self) -> FrameTag {
        self.tag
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum AckError {
    NoDrawEncoded,
    NoFinalDepthCopy,
    DuplicateQueued,
    QueuePoisoned,
}

impl fmt::Display for AckError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        formatter.write_str(match self {
            Self::NoDrawEncoded => "No auxiliary draw was encoded",
            Self::NoFinalDepthCopy => "The main final-depth copy was not encoded",
            Self::DuplicateQueued => "An auxiliary acknowledgement with this tag is already queued",
            Self::QueuePoisoned => "Auxiliary acknowledgement mutex is poisoned",
        })
    }
}

impl StdError for AckError {}

/// Share this exact instance into `RenderWorld` with `ExtractResourcePlugin`.
#[derive(Resource, Clone, Default, ExtractResource)]
pub struct Acknowledgements {
    queue: Arc<Mutex<VecDeque<AuxAcknowledgement>>>,
}

impl Acknowledgements {
    pub fn drain(&self) -> Result<Vec<AuxAcknowledgement>, AckError> {
        let mut queue = self.queue.lock().map_err(|_| AckError::QueuePoisoned)?;
        Ok(queue.drain(..).collect())
    }
}

/// `RenderWorld` entry point, only after real auxiliary draws and final-depth copy.
/// This is deliberately named `commit_aux`; it must not be a resolve acknowledgement.
pub fn commit_aux(
    acknowledgements: &Acknowledgements,
    tag: FrameTag,
    encoding: AuxEncoding,
) -> Result<(), AckError> {
    if encoding.draw_count == 0 {
        return Err(AckError::NoDrawEncoded);
    }
    if !encoding.final_depth_copied {
        return Err(AckError::NoFinalDepthCopy);
    }
    let mut queue = acknowledgements
        .queue
        .lock()
        .map_err(|_| AckError::QueuePoisoned)?;
    if queue.iter().any(|ack| ack.tag == tag) {
        return Err(AckError::DuplicateQueued);
    }
    queue.push_back(AuxAcknowledgement { tag, encoding });
    Ok(())
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum FrameError {
    NotActiveView,
    ViewUnavailable,
    CameraInactive,
    UnsupportedProjection,
    NoComputedViewport,
    NoPendingFrame,
    StaleAcknowledgement,
    SerialExhausted,
    InvalidEncoding,
    WrongEncodingOwner,
    WrongHistoryEpoch,
    Temporal(TemporalError),
    Acknowledgement(AckError),
}

impl fmt::Display for FrameError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Temporal(error) => error.fmt(formatter),
            Self::Acknowledgement(error) => error.fmt(formatter),
            error => formatter.write_str(match error {
                Self::NotActiveView => {
                    "Temporal preparation/acknowledgement belongs to an inactive view"
                }
                Self::ViewUnavailable => {
                    "Selected temporal main view is unavailable or is a capture camera"
                }
                Self::CameraInactive => "Selected temporal camera is inactive",
                Self::UnsupportedProjection => {
                    "Source temporal auxiliary proof requires a perspective projection"
                }
                Self::NoComputedViewport => {
                    "Camera computation has not provided a physical viewport"
                }
                Self::NoPendingFrame => "No pending temporal frame owns this acknowledgement",
                Self::StaleAcknowledgement => {
                    "Temporal acknowledgement is stale or belongs to another prepared serial"
                }
                Self::SerialExhausted => "Temporal prepared-tag serial exhausted",
                Self::InvalidEncoding => {
                    "Auxiliary acknowledgement lacks draws or final-depth copy"
                }
                Self::WrongEncodingOwner => "The other encoding pass owns this temporal sequence",
                Self::WrongHistoryEpoch => {
                    "Resolve acknowledgement belongs to another history epoch"
                }
                Self::Temporal(_) | Self::Acknowledgement(_) => unreachable!(),
            }),
        }
    }
}

impl StdError for FrameError {}

struct Pending {
    tag: FrameTag,
    token: PreparedFrame,
}

struct OwnedView {
    sequence: Sequence,
    pending: Option<Pending>,
    owner: EncodingOwner,
}

/// Explicit activation only: default selects no view and installs no rendering.
#[derive(Resource, Default)]
pub struct Frames {
    selected: Option<Entity>,
    views: HashMap<Entity, OwnedView>,
    next_serial: u64,
    owner: EncodingOwner,
    pub committed_aux: u64,
    pub rejected_aux: u64,
    pub committed_resolve: u64,
    pub rejected_resolve: u64,
    pub last_error: Option<FrameError>,
}

impl Frames {
    /// Main intent/telemetry only for the Render-owned adapter. Discard any old
    /// Main sequence on transfer; retain the global tag floor and counters.
    pub(super) fn begin_render_owned(&mut self, selected: Option<Entity>) -> u64 {
        self.selected = selected;
        self.owner = EncodingOwner::Resolve;
        self.views.clear();
        self.next_serial
    }

    pub(super) fn observe_render_encoded(&mut self, tag: FrameTag) {
        self.next_serial = self.next_serial.max(tag.serial);
        self.committed_resolve = self.committed_resolve.saturating_add(1);
    }

    #[must_use]
    pub const fn selected_view(&self) -> Option<Entity> {
        self.selected
    }

    #[must_use]
    pub const fn owner(&self) -> EncodingOwner {
        self.owner
    }

    /// A deliberate new resolve instance starts a fresh source history.
    /// Switching the selected instance never promotes auxiliary acknowledgements.
    pub fn set_encoding_owner(&mut self, owner: EncodingOwner) -> Result<(), FrameError> {
        if self.owner == owner {
            return Ok(());
        }
        let view = self.selected.ok_or(FrameError::NotActiveView)?;
        if let Some(selected) = self.views.get_mut(&view) {
            selected.sequence.reset().map_err(FrameError::Temporal)?;
            selected.owner = owner;
        }
        for selected in self.views.values_mut() {
            selected.pending = None;
        }
        self.owner = owner;
        Ok(())
    }

    /// Adapters call this explicitly for Native/WASM. Switching invalidates all
    /// pending tokens but does not invent a camera-cut/reset policy.
    pub fn activate(&mut self, selected: Option<Entity>) {
        if self.selected != selected {
            for owner in self.views.values_mut() {
                owner.pending = None;
            }
            self.selected = selected;
        }
    }

    pub fn reset(&mut self, view: Entity) -> Result<(), FrameError> {
        let owner = self
            .views
            .get_mut(&view)
            .ok_or(FrameError::ViewUnavailable)?;
        owner.sequence.reset().map_err(FrameError::Temporal)?;
        owner.pending = None;
        Ok(())
    }

    /// Prepare once from actual unjittered camera state. Replacing pending does
    /// not commit previous matrices or consume a Halton phase; old acks are stale.
    pub fn prepare_aux(&mut self, view: Entity, input: Input) -> Result<ViewPacket, FrameError> {
        if self.selected != Some(view) {
            return Err(FrameError::NotActiveView);
        }
        let serial = self
            .next_serial
            .checked_add(1)
            .ok_or(FrameError::SerialExhausted)?;
        let owner = self.views.entry(view).or_insert_with(|| OwnedView {
            sequence: Sequence::new(ViewKey(view.to_bits())),
            pending: None,
            owner: self.owner,
        });
        if owner.owner != self.owner {
            owner.sequence.reset().map_err(FrameError::Temporal)?;
            owner.pending = None;
            owner.owner = self.owner;
        }
        let token = owner
            .sequence
            .prepare(input)
            .map_err(FrameError::Temporal)?;
        let frame = *token.frame();
        let tag = FrameTag { view, serial };
        owner.pending = Some(Pending { tag, token });
        self.next_serial = serial;
        Ok(ViewPacket {
            packet: Packet::from_frame(&frame),
            tag,
            frame,
        })
    }

    pub fn accept_aux(&mut self, acknowledgement: AuxAcknowledgement) -> Result<Frame, FrameError> {
        if self.owner != EncodingOwner::Auxiliary {
            return Err(FrameError::WrongEncodingOwner);
        }
        let tag = acknowledgement.tag;
        if self.selected != Some(tag.view) {
            return Err(FrameError::NotActiveView);
        }
        if acknowledgement.encoding.draw_count == 0 || !acknowledgement.encoding.final_depth_copied
        {
            return Err(FrameError::InvalidEncoding);
        }
        let owner = self
            .views
            .get_mut(&tag.view)
            .ok_or(FrameError::ViewUnavailable)?;
        let pending = owner.pending.as_ref().ok_or(FrameError::NoPendingFrame)?;
        if pending.tag != tag {
            return Err(FrameError::StaleAcknowledgement);
        }
        let pending = owner.pending.take().ok_or(FrameError::NoPendingFrame)?;
        let frame = owner
            .sequence
            .commit(pending.token)
            .map_err(FrameError::Temporal)?;
        self.committed_aux = self.committed_aux.saturating_add(1);
        Ok(frame)
    }

    /// Consume a distinct resolve encoding receipt. The `RenderWorld` issuer owns
    /// generation validation; this CPU bridge also rejects stale tags/epochs.
    pub fn accept_resolve(
        &mut self,
        tag: FrameTag,
        history_epoch: u64,
        generation: u64,
        draws: u32,
        depth_copied: bool,
    ) -> Result<Frame, FrameError> {
        if self.owner != EncodingOwner::Resolve {
            return Err(FrameError::WrongEncodingOwner);
        }
        if self.selected != Some(tag.view) {
            return Err(FrameError::NotActiveView);
        }
        if generation == 0 || draws == 0 || !depth_copied {
            return Err(FrameError::InvalidEncoding);
        }
        let owner = self
            .views
            .get_mut(&tag.view)
            .ok_or(FrameError::ViewUnavailable)?;
        let pending = owner.pending.as_ref().ok_or(FrameError::NoPendingFrame)?;
        if pending.tag != tag {
            return Err(FrameError::StaleAcknowledgement);
        }
        if pending.token.frame().history_epoch != history_epoch {
            return Err(FrameError::WrongHistoryEpoch);
        }
        let pending = owner.pending.take().ok_or(FrameError::NoPendingFrame)?;
        let frame = owner
            .sequence
            .commit(pending.token)
            .map_err(FrameError::Temporal)?;
        self.committed_resolve = self.committed_resolve.saturating_add(1);
        Ok(frame)
    }
}

/// First part of the parent plugin's ordered `PostUpdate` chain.
#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems receive Res and ResMut wrappers by value"
)]
pub fn drain_aux_acknowledgements(
    acknowledgements: Res<Acknowledgements>,
    mut frames: ResMut<Frames>,
) {
    match acknowledgements.drain() {
        Ok(acks) => {
            for ack in acks {
                if frames.owner() != EncodingOwner::Auxiliary {
                    continue;
                }
                if let Err(error) = frames.accept_aux(ack) {
                    frames.rejected_aux = frames.rejected_aux.saturating_add(1);
                    frames.last_error = Some(error);
                }
            }
        }
        Err(error) => frames.last_error = Some(FrameError::Acknowledgement(error)),
    }
}

/// Concrete camera adapter. Scale1 is the pinned source's initial render scale.
/// Computed projection remains UNJITTERED; parent applies this packet's Halton to
/// the real main/refraction views before render uniforms are prepared.
#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems receive Commands, queries, and optional resources by value"
)]
pub fn prepare_main_view(
    mut commands: Commands,
    mut frames: ResMut<Frames>,
    cameras: Query<(Entity, &Camera, &GlobalTransform, &Projection), Without<CaptureCamera>>,
    packets: Query<Entity, With<ViewPacket>>,
    render_control: Option<Res<super::render_owned::Control>>,
) {
    if render_control
        .as_ref()
        .is_some_and(|control| control.selected_view().is_some())
    {
        for entity in &packets {
            commands.entity(entity).remove::<ViewPacket>();
        }
        return;
    }
    let selected = frames.selected_view();
    for entity in &packets {
        if Some(entity) != selected {
            commands.entity(entity).remove::<ViewPacket>();
        }
    }
    let Some(view) = selected else { return };
    let result = (|| {
        let (_, camera, transform, projection) =
            cameras.get(view).map_err(|_| FrameError::ViewUnavailable)?;
        if !camera.is_active {
            return Err(FrameError::CameraInactive);
        }
        let Projection::Perspective(projection) = projection else {
            return Err(FrameError::UnsupportedProjection);
        };
        let size = camera
            .physical_viewport_size()
            .ok_or(FrameError::NoComputedViewport)?;
        frames.prepare_aux(
            view,
            Input {
                world_from_view: transform.to_matrix(),
                clip_from_view: camera.clip_from_view(),
                input_size: size,
                output_size: size,
                near: projection.near,
                far: projection.far,
            },
        )
    })();
    match result {
        Ok(packet) => {
            frames.last_error = None;
            commands.entity(view).insert(packet);
        }
        Err(error) => {
            frames.last_error = Some(error);
            if packets.get(view).is_ok() {
                commands.entity(view).remove::<ViewPacket>();
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::render::{camera::TemporalJitter, render_resource::encase::UniformBuffer};

    fn input() -> Input {
        Input {
            world_from_view: Mat4::IDENTITY,
            clip_from_view: Mat4::perspective_infinite_reverse_rh(
                62.0_f32.to_radians(),
                1919.0 / 1079.0,
                0.06,
            ),
            input_size: UVec2::new(1919, 1079),
            output_size: UVec2::new(1919, 1079),
            near: 0.06,
            far: 60_000.0,
        }
    }

    fn views() -> [Entity; 2] {
        let mut world = World::new();
        [world.spawn_empty().id(), world.spawn_empty().id()]
    }

    fn encoded() -> AuxEncoding {
        AuxEncoding {
            draw_count: 1,
            final_depth_copied: true,
        }
    }

    #[test]
    fn missing_stale_duplicate_and_foreign_acks_do_not_advance_phase() {
        let [view, other] = views();
        let mut frames = Frames::default();
        assert!(matches!(
            frames.prepare_aux(view, input()),
            Err(FrameError::NotActiveView)
        ));
        frames.activate(Some(view));
        let first = frames.prepare_aux(view, input()).expect("selected view");
        let mut moved = input();
        moved.world_from_view = Mat4::from_translation(Vec3::X);
        let replacement = frames
            .prepare_aux(view, moved)
            .expect("unencoded frame replacement");
        assert_ne!(first.tag.serial, replacement.tag.serial);
        assert_eq!(replacement.frame.phase, 0);
        assert_eq!(
            frames.accept_aux(AuxAcknowledgement {
                tag: first.tag,
                encoding: encoded()
            }),
            Err(FrameError::StaleAcknowledgement)
        );
        assert_eq!(
            frames.accept_aux(AuxAcknowledgement {
                tag: FrameTag {
                    view: other,
                    serial: replacement.tag.serial
                },
                encoding: encoded()
            }),
            Err(FrameError::NotActiveView)
        );
        frames
            .accept_aux(AuxAcknowledgement {
                tag: replacement.tag,
                encoding: encoded(),
            })
            .expect("one encoded aux image");
        assert_eq!(
            frames.accept_aux(AuxAcknowledgement {
                tag: replacement.tag,
                encoding: encoded()
            }),
            Err(FrameError::NoPendingFrame)
        );
        let next = frames.prepare_aux(view, input()).expect("next phase");
        assert_eq!(next.frame.phase, 1);
        assert_eq!(next.frame.previous_camera_position, Vec3::X);
        assert_eq!(frames.committed_aux, 1);
    }

    #[test]
    fn only_actual_draw_and_copy_markers_enter_shared_ack_queue() {
        let [view, _] = views();
        let tag = FrameTag { view, serial: 1 };
        let main = Acknowledgements::default();
        let render = main.clone();
        assert_eq!(
            commit_aux(
                &render,
                tag,
                AuxEncoding {
                    draw_count: 0,
                    final_depth_copied: true
                }
            ),
            Err(AckError::NoDrawEncoded)
        );
        assert_eq!(
            commit_aux(
                &render,
                tag,
                AuxEncoding {
                    draw_count: 1,
                    final_depth_copied: false
                }
            ),
            Err(AckError::NoFinalDepthCopy)
        );
        assert!(main.drain().expect("empty queue").is_empty());
        commit_aux(&render, tag, encoded()).expect("encoded commands");
        assert_eq!(
            commit_aux(&render, tag, encoded()),
            Err(AckError::DuplicateQueued)
        );
        assert_eq!(main.drain().expect("shared queue")[0].tag(), tag);
        assert!(render.drain().expect("drained queue").is_empty());
    }

    #[test]
    fn deactivate_and_reset_invalidate_pending_without_render_clock_advancement() {
        let [view, _] = views();
        let mut frames = Frames::default();
        frames.activate(Some(view));
        let pending = frames.prepare_aux(view, input()).expect("first");
        frames.activate(None);
        assert_eq!(
            frames.accept_aux(AuxAcknowledgement {
                tag: pending.tag,
                encoding: encoded()
            }),
            Err(FrameError::NotActiveView)
        );
        frames.activate(Some(view));
        let current = frames.prepare_aux(view, input()).expect("resume");
        assert_eq!(current.frame.phase, 0);
        frames.reset(view).expect("explicit cut");
        assert_eq!(
            frames.accept_aux(AuxAcknowledgement {
                tag: current.tag,
                encoding: encoded()
            }),
            Err(FrameError::NoPendingFrame)
        );
        let cut = frames.prepare_aux(view, input()).expect("new epoch");
        assert!(cut.frame.needs_history_seed);
        assert_eq!(cut.frame.history_epoch, 1);
        assert!(cut.tag.serial > current.tag.serial);
    }

    #[test]
    fn auxiliary_receipt_cannot_advance_resolve_or_consume_a_failed_frame() {
        let [view, other] = views();
        let mut frames = Frames::default();
        frames.activate(Some(view));
        let old = frames.prepare_aux(view, input()).expect("auxiliary frame");
        frames
            .set_encoding_owner(EncodingOwner::Resolve)
            .expect("explicit resolve owner");
        let first = frames
            .prepare_aux(view, input())
            .expect("fresh resolve frame");
        assert_eq!(first.frame.phase, 0);
        assert!(first.frame.needs_history_seed);
        assert_ne!(first.frame.history_epoch, old.frame.history_epoch);
        assert_eq!(
            frames.accept_aux(AuxAcknowledgement {
                tag: first.tag,
                encoding: encoded()
            }),
            Err(FrameError::WrongEncodingOwner)
        );
        assert_eq!(
            frames.accept_resolve(first.tag, first.frame.history_epoch, 1, 1, false),
            Err(FrameError::InvalidEncoding)
        );
        assert_eq!(
            frames.accept_resolve(first.tag, first.frame.history_epoch + 1, 1, 1, true),
            Err(FrameError::WrongHistoryEpoch)
        );
        assert_eq!(
            frames.accept_resolve(
                FrameTag {
                    view: other,
                    serial: first.tag.serial
                },
                first.frame.history_epoch,
                1,
                1,
                true
            ),
            Err(FrameError::NotActiveView)
        );
        let moved = Input {
            world_from_view: Mat4::from_translation(Vec3::X),
            ..input()
        };
        let replacement = frames
            .prepare_aux(view, moved)
            .expect("camera moved while no resolve encoded");
        assert_eq!(replacement.frame.phase, 0);
        assert_eq!(
            frames.accept_resolve(first.tag, first.frame.history_epoch, 1, 1, true),
            Err(FrameError::StaleAcknowledgement)
        );
        frames
            .accept_resolve(replacement.tag, replacement.frame.history_epoch, 1, 1, true)
            .expect("real complete resolve encoding receipt");
        assert_eq!(
            frames.accept_resolve(replacement.tag, replacement.frame.history_epoch, 1, 1, true),
            Err(FrameError::NoPendingFrame)
        );
        let next = frames
            .prepare_aux(view, moved)
            .expect("next resolved phase");
        assert_eq!(next.frame.phase, 1);
        assert_eq!(next.frame.previous_camera_position, Vec3::X);
        assert_eq!(frames.committed_resolve, 1);
        assert_eq!(frames.committed_aux, 0);
    }

    #[test]
    fn switching_views_does_not_reuse_an_auxiliary_camera_as_resolve_history() {
        let [first, second] = views();
        let mut frames = Frames::default();
        frames.activate(Some(second));
        let old_camera = Input {
            world_from_view: Mat4::from_translation(Vec3::X),
            ..input()
        };
        let old = frames.prepare_aux(second, old_camera).expect("aux view");
        frames
            .accept_aux(AuxAcknowledgement {
                tag: old.tag,
                encoding: encoded(),
            })
            .expect("old aux image");
        frames.activate(Some(first));
        frames
            .set_encoding_owner(EncodingOwner::Resolve)
            .expect("resolve selected view");
        frames
            .prepare_aux(first, input())
            .expect("first source instance");
        frames.activate(Some(second));
        let current = frames
            .prepare_aux(second, input())
            .expect("new source instance for second view");
        assert_eq!(current.frame.phase, 0);
        assert!(current.frame.needs_history_seed);
        assert_eq!(
            current.frame.previous_camera_position,
            current.frame.camera_position
        );
        assert_eq!(current.frame.history_epoch, old.frame.history_epoch + 1);
    }

    #[test]
    fn uniform_packet_has_64_byte_matrices_and_exact_336_byte_shader_abi() {
        let [view, _] = views();
        let mut frames = Frames::default();
        frames.activate(Some(view));
        let mut packet = frames.prepare_aux(view, input()).expect("packet");
        packet.frame.history_epoch = 0x1234_5678_9abc_def0;
        packet.packet = Packet::from_frame(&packet.frame);
        assert_eq!(Mat4::min_size().get(), 64);
        assert_eq!(Packet::min_size().get(), 336);
        let mut uniform = UniformBuffer::new(Vec::new());
        uniform.write(&packet.packet).expect("uniform layout");
        let bytes = uniform.into_inner();
        assert_eq!(bytes.len(), 336);
        let word =
            |offset| u32::from_le_bytes(bytes[offset..offset + 4].try_into().expect("four bytes"));
        assert_eq!(word(288), 1919);
        assert_eq!(word(292), 1079);
        assert_eq!(word(296), 1919);
        assert_eq!(word(300), 1079);
        assert_eq!(word(304), 0.06_f32.to_bits());
        assert_eq!(word(308), 60_000.0_f32.to_bits());
        assert_eq!(word(320), 0);
        assert_eq!(word(324), 0x9abc_def0);
        assert_eq!(word(328), 0x1234_5678);
        assert_eq!(word(332), 1);
        for (index, value) in packet
            .frame
            .unjittered_clip_from_world
            .to_cols_array()
            .iter()
            .enumerate()
        {
            assert_eq!(word(index * 4), value.to_bits());
        }
    }

    #[test]
    fn bevy_jitter_projection_matches_source_clip_translation_at_odd_size() {
        let [view, _] = views();
        let mut frames = Frames::default();
        frames.activate(Some(view));
        let camera = Input {
            world_from_view: Mat4::from_rotation_translation(
                Quat::from_rotation_y(0.07),
                Vec3::new(0.3, 0.1, -0.2),
            ),
            ..input()
        };
        for phase in 0..32 {
            let packet = frames.prepare_aux(view, camera).expect("frame");
            assert_eq!(packet.frame.phase, phase);
            let mut projection = camera.clip_from_view;
            TemporalJitter {
                offset: packet.frame.jitter_pixels,
            }
            .jitter_projection(&mut projection, packet.frame.input_size.as_vec2());
            let actual = projection * camera.world_from_view.inverse();
            for (actual, expected) in actual
                .to_cols_array()
                .into_iter()
                .zip(packet.frame.clip_from_world.to_cols_array())
            {
                assert!((actual - expected).abs() < 1e-6);
            }
            frames
                .accept_aux(AuxAcknowledgement {
                    tag: packet.tag,
                    encoding: encoded(),
                })
                .expect("advance after auxiliary encoding");
        }
    }
}
