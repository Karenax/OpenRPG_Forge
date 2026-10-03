// SPDX-License-Identifier: MIT
// Camera sequence derives from Tidewater 1438b1abfcaee3267092b75573014f4d9b4a983c.
// Copyright 2026 DRG Software Solutions LLC; see third_party/tidewater/LICENSE.
//! Inactive foundation for the selected source-resolve instance in `RenderWorld`.
//!
//! The adapter must prepare from the actual extracted, unjittered camera once
//! per rendered view, then commit after the real resolve and both depth/HDR
//! copies. Main simulation/rAF and acknowledgement arrival never advance this
//! owner. No systems, extraction, GPU resources or installation are provided.
//!
//! One bounded sequence exists at a time. Switching/deselecting destroys it;
//! returning to the same Main Entity is a fresh instance with phase 0 and seed.
//! The adapter must prune/reallocate its histories on that lifecycle change.
//! Global preparation serials remain monotone through every lifecycle change.
//!
//! This validates camera ownership, not actual encoding or object motion.
//! Nonzero generations are checked here; the render hook must separately match
//! them against its actual allocations. Bevy previous mesh transforms may refer
//! to an earlier extraction instead of the last successful source resolve when
//! a resolve is skipped. Village/FishStand/StandardOpaque object poses therefore
//! remain a separate contract; a camera-only CPU test cannot qualify them.

use super::frame::{FrameTag, Packet, ViewPacket};
use crate::tidewater_temporal_frame::{
    Error as TemporalError, Frame, Input, PreparedFrame, Sequence, ViewKey,
};
use bevy::prelude::{Entity, Resource};
use std::{
    error::Error as StdError,
    fmt,
    sync::{Mutex, MutexGuard},
};

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Error {
    StatePoisoned,
    NotSelected,
    ForeignView,
    StaleResetRevision,
    SerialExhausted,
    SerialFloorWhilePending,
    NoPendingFrame,
    StaleTag,
    WrongEpoch,
    InvalidGeneration,
    InvalidDrawCount,
    MissingDepthCopy,
    MissingOutputCopy,
    Temporal(TemporalError),
}

impl fmt::Display for Error {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Temporal(error) => error.fmt(f),
            error => write!(f, "Render-owned temporal frame: {error:?}"),
        }
    }
}
impl StdError for Error {}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum SelectionChange {
    Unchanged,
    Selected,
    Reset,
    Deselected,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Status {
    Inactive,
    Selected {
        view: Entity,
        reset_revision: u64,
    },
    Pending {
        tag: FrameTag,
        history_epoch: u64,
        reset_revision: u64,
    },
}

/// Receipt of an accepted CPU ownership transition, never a GPU completion fence.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct Commit {
    pub tag: FrameTag,
    pub history_epoch: u64,
    pub resolve_generation: u64,
    pub auxiliary_generation: u64,
}

#[derive(Clone, Debug)]
pub struct Snapshot {
    pub status: Status,
    /// Last issued global serial; no lifecycle operation resets this value.
    pub last_serial: u64,
    pub prepared: u64,
    pub committed: u64,
    /// Invalid reservations and commit receipts; never a GPU rejection count.
    pub rejected_commits: u64,
    pub selection_changes: u64,
    pub resets: u64,
    pub pending_frame: Option<Frame>,
    /// Current instance only; cleared on reset/switch/deselection.
    pub last_frame: Option<Frame>,
    pub last_commit: Option<Commit>,
    pub last_error: Option<Error>,
}

struct Pending {
    tag: FrameTag,
    token: PreparedFrame,
}

struct Selected {
    view: Entity,
    reset_revision: u64,
    sequence: Sequence,
    pending: Option<Pending>,
    last_frame: Option<Frame>,
    last_commit: Option<Commit>,
}

#[derive(Default)]
struct State {
    selected: Option<Selected>,
    last_serial: u64,
    prepared: u64,
    committed: u64,
    rejected_commits: u64,
    selection_changes: u64,
    resets: u64,
    last_error: Option<Error>,
}

/// This resource belongs exclusively to `RenderWorld`. Main submits selection
/// intent and observes status; it must never invoke preparation or commit.
/// The initial default is inactive and allocates no sequence or GPU resource.
#[derive(Resource, Default)]
pub struct Owner(Mutex<State>);

/// Exclusive pre-encoding reservation of an already prepared frame. Holding it
/// prevents selection/reset/preparation from replacing the validated token.
/// Drop without commit releases the lock and leaves the sequence/pending intact.
/// It must not be held across a GPU wait or a call back into this `Owner`.
#[must_use = "Keep the reservation through actual encoding, or drop it to skip"]
pub struct Reservation<'owner> {
    state: MutexGuard<'owner, State>,
    tag: FrameTag,
    history_epoch: u64,
}

impl Owner {
    /// Adopt serials already issued by the legacy owner before preparing here.
    /// Only the global serial changes; selection, history and diagnostics remain
    /// intact. Raising it while a frame is pending would break issuance order.
    pub fn raise_serial_floor(&self, floor: u64) -> Result<(), Error> {
        let mut state = self.0.lock().map_err(|_| Error::StatePoisoned)?;
        if floor > state.last_serial && state.selected.as_ref().is_some_and(|s| s.pending.is_some())
        {
            return Err(Error::SerialFloorWhilePending);
        }
        state.last_serial = state.last_serial.max(floor);
        Ok(())
    }

    /// A fresh view establishes its initial revision; only a higher revision
    /// for the same active instance resets it. Lower revisions of that instance
    /// are rejected without invalidating its pending frame. An unchanged intent
    /// is a true no-op, including its diagnostics and pending token.
    pub fn apply_selection(
        &self,
        view: Option<Entity>,
        reset_revision: u64,
    ) -> Result<SelectionChange, Error> {
        let mut state = self.0.lock().map_err(|_| Error::StatePoisoned)?;
        let result = (|| {
            let Some(view) = view else {
                return Ok(if state.selected.take().is_some() {
                    state.selection_changes = state.selection_changes.saturating_add(1);
                    SelectionChange::Deselected
                } else {
                    SelectionChange::Unchanged
                });
            };
            if let Some(selected) = &mut state.selected
                && selected.view == view
            {
                if reset_revision < selected.reset_revision {
                    return Err(Error::StaleResetRevision);
                }
                if reset_revision == selected.reset_revision {
                    return Ok(SelectionChange::Unchanged);
                }
                selected.sequence.reset().map_err(Error::Temporal)?;
                selected.reset_revision = reset_revision;
                selected.pending = None;
                selected.last_frame = None;
                selected.last_commit = None;
                state.resets = state.resets.saturating_add(1);
                return Ok(SelectionChange::Reset);
            }
            state.selected = Some(Selected {
                view,
                reset_revision,
                sequence: Sequence::new(ViewKey(view.to_bits())),
                pending: None,
                last_frame: None,
                last_commit: None,
            });
            state.selection_changes = state.selection_changes.saturating_add(1);
            Ok(SelectionChange::Selected)
        })();
        match result {
            Ok(SelectionChange::Unchanged) => {}
            Ok(_) => state.last_error = None,
            Err(error) => state.last_error = Some(error),
        }
        result
    }

    /// Replace a skipped preparation with current extracted camera input. This
    /// does not consume a phase or promote either camera to previous history.
    /// Invalid input leaves an existing pending frame and the serial untouched.
    pub fn prepare(&self, view: Entity, input: Input) -> Result<ViewPacket, Error> {
        let mut state = self.0.lock().map_err(|_| Error::StatePoisoned)?;
        let result = (|| {
            let selected = state.selected.as_ref().ok_or(Error::NotSelected)?;
            if selected.view != view {
                return Err(Error::ForeignView);
            }
            let serial = state
                .last_serial
                .checked_add(1)
                .ok_or(Error::SerialExhausted)?;
            let token = selected.sequence.prepare(input).map_err(Error::Temporal)?;
            let frame = *token.frame();
            let tag = FrameTag { view, serial };
            let packet = ViewPacket {
                packet: Packet::from_frame(&frame),
                tag,
                frame,
            };
            state
                .selected
                .as_mut()
                .expect("Selected owner held under the same lock")
                .pending = Some(Pending { tag, token });
            state.last_serial = serial;
            state.prepared = state.prepared.saturating_add(1);
            Ok(packet)
        })();
        state.last_error = result.as_ref().err().copied();
        result
    }

    /// Check before encoding starts. No receipt/observation bus participates,
    /// and checking never advances or replaces a pending frame.
    pub fn validate_pending(&self, tag: FrameTag, history_epoch: u64) -> Result<(), Error> {
        let state = self.0.lock().map_err(|_| Error::StatePoisoned)?;
        validate(&state, tag, history_epoch).map(|_| ())
    }

    /// Acquire before any resolve mutation/encoding. Poisoned locks or stale
    /// packets fail here, before the adapter writes any histories or output.
    /// Generation identity must still be checked against actual GPU allocations.
    pub fn reserve_pending(
        &self,
        tag: FrameTag,
        history_epoch: u64,
    ) -> Result<Reservation<'_>, Error> {
        let mut state = self.0.lock().map_err(|_| Error::StatePoisoned)?;
        if let Err(error) = validate(&state, tag, history_epoch) {
            state.rejected_commits = state.rejected_commits.saturating_add(1);
            state.last_error = Some(error);
            return Err(error);
        }
        Ok(Reservation {
            state,
            tag,
            history_epoch,
        })
    }

    /// Convenience CPU transaction. The render adapter must instead reserve
    /// BEFORE encoding and call `Reservation::commit_resolve` AFTER encoding.
    /// Invalid receipts keep pending intact; numbers here are not GPU evidence.
    #[allow(
        clippy::too_many_arguments,
        reason = "One explicit encoded resolve transaction"
    )]
    pub fn commit_resolve(
        &self,
        tag: FrameTag,
        history_epoch: u64,
        resolve_generation: u64,
        auxiliary_generation: u64,
        draws: u32,
        depth_copied: bool,
        output_copied: bool,
    ) -> Result<Frame, Error> {
        self.reserve_pending(tag, history_epoch)?.commit_resolve(
            resolve_generation,
            auxiliary_generation,
            draws,
            depth_copied,
            output_copied,
        )
    }

    pub fn snapshot(&self) -> Result<Snapshot, Error> {
        let state = self.0.lock().map_err(|_| Error::StatePoisoned)?;
        let status = match &state.selected {
            None => Status::Inactive,
            Some(selected) => match &selected.pending {
                Some(pending) => Status::Pending {
                    tag: pending.tag,
                    history_epoch: pending.token.frame().history_epoch,
                    reset_revision: selected.reset_revision,
                },
                None => Status::Selected {
                    view: selected.view,
                    reset_revision: selected.reset_revision,
                },
            },
        };
        Ok(Snapshot {
            status,
            last_serial: state.last_serial,
            prepared: state.prepared,
            committed: state.committed,
            rejected_commits: state.rejected_commits,
            selection_changes: state.selection_changes,
            resets: state.resets,
            pending_frame: state
                .selected
                .as_ref()
                .and_then(|s| s.pending.as_ref())
                .map(|p| *p.token.frame()),
            last_frame: state.selected.as_ref().and_then(|s| s.last_frame),
            last_commit: state.selected.as_ref().and_then(|s| s.last_commit),
            last_error: state.last_error,
        })
    }
}

impl Reservation<'_> {
    #[must_use]
    pub const fn tag(&self) -> FrameTag {
        self.tag
    }

    #[must_use]
    pub fn frame(&self) -> &Frame {
        validate(&self.state, self.tag, self.history_epoch)
            .expect("Reservation keeps its validated token exclusively locked")
            .token
            .frame()
    }

    /// Consume this reservation only after the actual draw and both copies have
    /// been encoded. On error the lock is released but pending remains intact.
    pub fn commit_resolve(
        mut self,
        resolve_generation: u64,
        auxiliary_generation: u64,
        draws: u32,
        depth_copied: bool,
        output_copied: bool,
    ) -> Result<Frame, Error> {
        let result = (|| {
            if resolve_generation == 0 || auxiliary_generation == 0 {
                return Err(Error::InvalidGeneration);
            }
            if draws != 1 {
                return Err(Error::InvalidDrawCount);
            }
            if !depth_copied {
                return Err(Error::MissingDepthCopy);
            }
            if !output_copied {
                return Err(Error::MissingOutputCopy);
            }
            let selected = self
                .state
                .selected
                .as_mut()
                .expect("Reservation keeps its selected instance exclusively locked");
            // Clone only inside this transaction. Sequence validates its opaque
            // serial before mutation; no token is removed if that check fails.
            let token = selected
                .pending
                .as_ref()
                .expect("Reservation keeps its pending token exclusively locked")
                .token
                .clone();
            let frame = selected.sequence.commit(token).map_err(Error::Temporal)?;
            selected.pending = None;
            selected.last_frame = Some(frame);
            selected.last_commit = Some(Commit {
                tag: self.tag,
                history_epoch: self.history_epoch,
                resolve_generation,
                auxiliary_generation,
            });
            self.state.committed = self.state.committed.saturating_add(1);
            Ok(frame)
        })();
        if result.is_err() {
            self.state.rejected_commits = self.state.rejected_commits.saturating_add(1);
        }
        self.state.last_error = result.as_ref().err().copied();
        result
    }
}

fn validate(state: &State, tag: FrameTag, history_epoch: u64) -> Result<&Pending, Error> {
    let selected = state.selected.as_ref().ok_or(Error::NotSelected)?;
    if selected.view != tag.view {
        return Err(Error::ForeignView);
    }
    let pending = selected.pending.as_ref().ok_or(Error::NoPendingFrame)?;
    if pending.tag != tag {
        return Err(Error::StaleTag);
    }
    if pending.token.frame().history_epoch != history_epoch {
        return Err(Error::WrongEpoch);
    }
    Ok(pending)
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::math::{Mat4, UVec2, Vec3};

    fn view(index: u32) -> Entity {
        Entity::from_raw_u32(index).unwrap()
    }
    fn input(x: f32, size: UVec2) -> Input {
        Input {
            world_from_view: Mat4::from_translation(Vec3::new(x, 2.0, 3.0)),
            clip_from_view: Mat4::perspective_infinite_reverse_rh(
                62.0_f32.to_radians(),
                size.as_vec2().x / size.as_vec2().y,
                0.06,
            ),
            input_size: size,
            output_size: size,
            near: 0.06,
            far: 60_000.0,
        }
    }
    fn prepare(owner: &Owner, x: f32) -> ViewPacket {
        owner
            .prepare(view(1), input(x, UVec2::new(1920, 1080)))
            .unwrap()
    }
    fn selected() -> Owner {
        let owner = Owner::default();
        assert_eq!(
            owner.apply_selection(Some(view(1)), 0).unwrap(),
            SelectionChange::Selected
        );
        owner
    }
    fn commit(owner: &Owner, packet: &ViewPacket) -> Frame {
        owner
            .commit_resolve(packet.tag, packet.frame.history_epoch, 7, 9, 1, true, true)
            .unwrap()
    }

    fn assert_state_except_serial(before: &Snapshot, after: &Snapshot) {
        assert_eq!(after.status, before.status);
        assert_eq!(after.prepared, before.prepared);
        assert_eq!(after.committed, before.committed);
        assert_eq!(after.rejected_commits, before.rejected_commits);
        assert_eq!(after.selection_changes, before.selection_changes);
        assert_eq!(after.resets, before.resets);
        assert_eq!(after.pending_frame, before.pending_frame);
        assert_eq!(after.last_frame, before.last_frame);
        assert_eq!(after.last_commit, before.last_commit);
        assert_eq!(after.last_error, before.last_error);
    }

    #[test]
    fn serial_floor_adopts_legacy_before_selection_and_after_commit() {
        let owner = Owner::default();
        let inactive = owner.snapshot().unwrap();
        owner.raise_serial_floor(40).unwrap();
        let raised = owner.snapshot().unwrap();
        assert_eq!(raised.last_serial, 40);
        assert_state_except_serial(&inactive, &raised);
        owner.apply_selection(Some(view(1)), 0).unwrap();
        let first = prepare(&owner, 1.0);
        assert_eq!(first.tag.serial, 41);
        commit(&owner, &first);
        let committed = owner.snapshot().unwrap();
        owner.raise_serial_floor(90).unwrap();
        let raised = owner.snapshot().unwrap();
        assert_eq!(raised.last_serial, 90);
        assert_state_except_serial(&committed, &raised);
        let next = prepare(&owner, 2.0);
        assert_eq!(next.tag.serial, 91);
        assert_eq!(next.frame.phase, 1);
        assert_eq!(
            next.frame.previous_camera_position,
            first.frame.camera_position
        );
        assert!(!next.frame.needs_history_seed);
    }

    #[test]
    fn lower_or_equal_serial_floor_is_a_noop_with_pending_and_error() {
        let owner = selected();
        owner.raise_serial_floor(90).unwrap();
        let packet = prepare(&owner, 1.0);
        assert!(matches!(
            owner.prepare(view(2), input(2.0, UVec2::ONE)),
            Err(Error::ForeignView)
        ));
        let before = owner.snapshot().unwrap();
        for floor in [0, 90, packet.tag.serial] {
            owner.raise_serial_floor(floor).unwrap();
            let after = owner.snapshot().unwrap();
            assert_eq!(after.last_serial, before.last_serial);
            assert_state_except_serial(&before, &after);
            owner
                .validate_pending(packet.tag, packet.frame.history_epoch)
                .unwrap();
        }
    }

    #[test]
    fn raising_serial_floor_with_pending_rejects_without_mutation() {
        let owner = selected();
        let packet = prepare(&owner, 1.0);
        let before = owner.snapshot().unwrap();
        assert_eq!(
            owner.raise_serial_floor(packet.tag.serial + 1),
            Err(Error::SerialFloorWhilePending)
        );
        let after = owner.snapshot().unwrap();
        assert_eq!(after.last_serial, before.last_serial);
        assert_state_except_serial(&before, &after);
        let reservation = owner
            .reserve_pending(packet.tag, packet.frame.history_epoch)
            .unwrap();
        assert_eq!(reservation.tag(), packet.tag);
        assert_eq!(reservation.frame(), &packet.frame);
        assert_eq!(
            reservation.commit_resolve(7, 9, 1, true, true),
            Ok(packet.frame)
        );
    }

    #[test]
    fn maximum_serial_floor_prevents_prepare_without_advancing_sequence() {
        let owner = selected();
        let packet = prepare(&owner, 1.0);
        commit(&owner, &packet);
        let next_sequence_frame = |owner: &Owner| {
            *owner
                .0
                .lock()
                .unwrap()
                .selected
                .as_ref()
                .unwrap()
                .sequence
                .prepare(input(2.0, UVec2::new(1920, 1080)))
                .unwrap()
                .frame()
        };
        let frame_before = next_sequence_frame(&owner);
        let before_floor = owner.snapshot().unwrap();
        owner.raise_serial_floor(u64::MAX).unwrap();
        let before_prepare = owner.snapshot().unwrap();
        assert_eq!(before_prepare.last_serial, u64::MAX);
        assert_state_except_serial(&before_floor, &before_prepare);
        assert!(matches!(
            owner.prepare(view(1), input(2.0, UVec2::new(1920, 1080))),
            Err(Error::SerialExhausted)
        ));
        let after = owner.snapshot().unwrap();
        assert_eq!(after.last_serial, u64::MAX);
        assert_eq!(after.last_error, Some(Error::SerialExhausted));
        let mut expected = before_prepare;
        expected.last_error = Some(Error::SerialExhausted);
        assert_state_except_serial(&expected, &after);
        assert_eq!(next_sequence_frame(&owner), frame_before);
    }

    #[test]
    fn temporal_commit_error_preserves_pending_and_last_accepted_frame() {
        let owner = selected();
        let first = prepare(&owner, 1.0);
        commit(&owner, &first);
        let pending = prepare(&owner, 2.0);
        // Test-only invariant fault: public reset clears pending, and a live
        // reservation blocks resets. Preserve a stale token to exercise the
        // underlying Sequence::commit error rather than a receipt guard.
        owner
            .0
            .lock()
            .unwrap()
            .selected
            .as_mut()
            .unwrap()
            .sequence
            .reset()
            .unwrap();
        let before = owner.snapshot().unwrap();
        let reservation = owner
            .reserve_pending(pending.tag, pending.frame.history_epoch)
            .unwrap();
        assert_eq!(
            reservation.commit_resolve(7, 9, 1, true, true),
            Err(Error::Temporal(TemporalError::StaleFrame))
        );
        let after = owner.snapshot().unwrap();
        let mut expected = before;
        expected.rejected_commits += 1;
        expected.last_error = Some(Error::Temporal(TemporalError::StaleFrame));
        assert_eq!(after.last_serial, expected.last_serial);
        assert_state_except_serial(&expected, &after);
        assert_eq!(after.pending_frame, Some(pending.frame));
        assert_eq!(after.last_frame, Some(first.frame));
    }

    #[test]
    fn inactive_default_and_wrong_view_cannot_prepare_or_commit() {
        let owner = Owner::default();
        assert_eq!(owner.snapshot().unwrap().status, Status::Inactive);
        assert!(matches!(
            owner.prepare(view(1), input(0.0, UVec2::ONE)),
            Err(Error::NotSelected)
        ));
        assert_eq!(
            owner.apply_selection(None, 99).unwrap(),
            SelectionChange::Unchanged
        );
        owner.apply_selection(Some(view(1)), 0).unwrap();
        assert!(matches!(
            owner.prepare(view(2), input(0.0, UVec2::ONE)),
            Err(Error::ForeignView)
        ));
        assert_eq!(owner.snapshot().unwrap().last_serial, 0);
    }

    #[test]
    #[allow(
        clippy::float_cmp,
        reason = "Checks exact authored camera translation supplied to the committed render packet."
    )]
    fn main_poses_between_render_prepares_do_not_become_previous_camera() {
        let owner = selected();
        let first = prepare(&owner, 1.0);
        commit(&owner, &first);
        // Main simulation may update any number of poses; only the actual final
        // extracted input is supplied to this RenderWorld preparation.
        let main_poses = [
            input(2.0, UVec2::new(1920, 1080)),
            input(3.0, UVec2::new(1920, 1080)),
            input(4.0, UVec2::new(1920, 1080)),
        ];
        let next = owner.prepare(view(1), main_poses[2]).unwrap();
        assert_eq!(next.frame.phase, 1);
        assert_eq!(
            next.frame.previous_camera_position,
            first.frame.camera_position
        );
        assert_eq!(
            next.frame.previous_unjittered_clip_from_world,
            first.frame.unjittered_clip_from_world
        );
        assert_eq!(
            next.frame.previous_world_from_clip,
            first.frame.world_from_clip
        );
        assert_eq!(next.frame.camera_position.x, 4.0);
        assert_eq!(owner.snapshot().unwrap().committed, 1);
    }

    #[test]
    fn skipped_preparations_keep_phase_and_last_committed_camera() {
        let owner = selected();
        let first = prepare(&owner, 1.0);
        commit(&owner, &first);
        let skipped = prepare(&owner, 2.0);
        let next = prepare(&owner, 4.0);
        assert_eq!(next.frame.phase, skipped.frame.phase);
        assert_eq!(
            next.frame.previous_camera_position,
            first.frame.camera_position
        );
        assert_eq!(
            owner.validate_pending(skipped.tag, skipped.frame.history_epoch),
            Err(Error::StaleTag)
        );
        assert!(
            owner
                .validate_pending(next.tag, next.frame.history_epoch)
                .is_ok()
        );
        assert!(next.tag.serial > skipped.tag.serial);
        assert_eq!(owner.snapshot().unwrap().committed, 1);
        commit(&owner, &next);
        assert_eq!(
            prepare(&owner, 5.0).frame.previous_camera_position,
            next.frame.camera_position
        );
    }

    #[test]
    fn invalid_receipts_preserve_pending_and_do_not_consume_halton() {
        let owner = selected();
        let packet = prepare(&owner, 1.0);
        for (rg, ag, draws, depth, output, error) in [
            (0, 9, 1, true, true, Error::InvalidGeneration),
            (7, 0, 1, true, true, Error::InvalidGeneration),
            (7, 9, 0, true, true, Error::InvalidDrawCount),
            (7, 9, 2, true, true, Error::InvalidDrawCount),
            (7, 9, 1, false, true, Error::MissingDepthCopy),
            (7, 9, 1, true, false, Error::MissingOutputCopy),
        ] {
            assert_eq!(
                owner.commit_resolve(
                    packet.tag,
                    packet.frame.history_epoch,
                    rg,
                    ag,
                    draws,
                    depth,
                    output
                ),
                Err(error)
            );
            assert!(
                owner
                    .validate_pending(packet.tag, packet.frame.history_epoch)
                    .is_ok()
            );
            assert_eq!(owner.snapshot().unwrap().pending_frame, Some(packet.frame));
        }
        assert_eq!(owner.snapshot().unwrap().rejected_commits, 6);
        let next = prepare(&owner, 4.0);
        assert_eq!(next.frame.phase, 0);
        assert_eq!(
            next.frame.previous_camera_position,
            next.frame.camera_position
        );
        assert!(next.frame.needs_history_seed);
        assert_eq!(owner.snapshot().unwrap().committed, 0);
    }

    #[test]
    fn tag_and_epoch_validation_is_strict_without_replacing_token() {
        let owner = selected();
        let packet = prepare(&owner, 1.0);
        let mut foreign = packet.tag;
        foreign.view = view(2);
        let mut wrong_tag = packet.tag;
        wrong_tag.serial += 1;
        for (tag, epoch, error) in [
            (foreign, 0, Error::ForeignView),
            (wrong_tag, 0, Error::StaleTag),
            (packet.tag, 1, Error::WrongEpoch),
        ] {
            assert_eq!(owner.validate_pending(tag, epoch), Err(error));
            assert_eq!(
                owner.commit_resolve(tag, epoch, 7, 9, 1, true, true),
                Err(error)
            );
            assert_eq!(owner.snapshot().unwrap().pending_frame, Some(packet.frame));
        }
        assert_eq!(commit(&owner, &packet), packet.frame);
    }

    #[test]
    fn reset_revision_is_once_idempotent_and_lower_revision_keeps_pending() {
        let owner = selected();
        let first = prepare(&owner, 1.0);
        commit(&owner, &first);
        let obsolete = prepare(&owner, 2.0);
        assert_eq!(
            owner.apply_selection(Some(view(1)), 7).unwrap(),
            SelectionChange::Reset
        );
        assert_eq!(
            owner.validate_pending(obsolete.tag, obsolete.frame.history_epoch),
            Err(Error::NoPendingFrame)
        );
        let fresh = prepare(&owner, 3.0);
        assert_eq!(fresh.frame.phase, 0);
        assert_eq!(fresh.frame.history_epoch, 1);
        assert!(fresh.frame.needs_history_seed);
        assert_eq!(
            fresh.frame.previous_camera_position,
            fresh.frame.camera_position
        );
        assert_eq!(
            owner.apply_selection(Some(view(1)), 7).unwrap(),
            SelectionChange::Unchanged
        );
        assert_eq!(
            owner.apply_selection(Some(view(1)), 6),
            Err(Error::StaleResetRevision)
        );
        assert!(
            owner
                .validate_pending(fresh.tag, fresh.frame.history_epoch)
                .is_ok()
        );
        assert_eq!(owner.snapshot().unwrap().resets, 1);
        assert!(owner.snapshot().unwrap().last_frame.is_none());
        commit(&owner, &fresh);
        assert_eq!(prepare(&owner, 4.0).frame.history_epoch, 1);
    }

    #[test]
    fn odd_resize_preserves_phase_and_camera_but_changes_epoch_and_seeds() {
        let owner = selected();
        let first = prepare(&owner, 1.0);
        commit(&owner, &first);
        let resized = owner
            .prepare(view(1), input(2.0, UVec2::new(1919, 1079)))
            .unwrap();
        assert_eq!(resized.frame.phase, 1);
        assert_eq!(resized.frame.history_epoch, 1);
        assert!(resized.frame.needs_history_seed);
        assert_eq!(
            resized.frame.previous_camera_position,
            first.frame.camera_position
        );
        assert_eq!(
            resized.frame.previous_world_from_clip,
            first.frame.world_from_clip
        );
        let repeated = owner
            .prepare(view(1), input(3.0, UVec2::new(1919, 1079)))
            .unwrap();
        assert_eq!(repeated.frame.history_epoch, 1);
        assert_eq!(repeated.frame.phase, 1);
        commit(&owner, &repeated);
        let stable = owner
            .prepare(view(1), input(4.0, UVec2::new(1919, 1079)))
            .unwrap();
        assert_eq!(stable.frame.history_epoch, 1);
        assert_eq!(stable.frame.phase, 2);
        assert!(!stable.frame.needs_history_seed);
    }

    #[test]
    fn switch_and_return_create_fresh_instance_without_reusing_serials() {
        let owner = selected();
        let first = prepare(&owner, 1.0);
        commit(&owner, &first);
        let obsolete = prepare(&owner, 2.0);
        owner.apply_selection(Some(view(2)), 0).unwrap();
        let other = owner
            .prepare(view(2), input(9.0, UVec2::new(1920, 1080)))
            .unwrap();
        assert_eq!(other.frame.phase, 0);
        assert!(other.frame.needs_history_seed);
        assert_eq!(
            other.frame.previous_camera_position,
            other.frame.camera_position
        );
        assert_eq!(
            owner.validate_pending(obsolete.tag, 0),
            Err(Error::ForeignView)
        );
        owner.apply_selection(Some(view(1)), 1).unwrap();
        let returned = prepare(&owner, 10.0);
        assert_eq!(returned.frame.phase, 0);
        assert!(returned.frame.needs_history_seed);
        assert_eq!(
            returned.frame.previous_camera_position,
            returned.frame.camera_position
        );
        assert!(returned.tag.serial > other.tag.serial && other.tag.serial > obsolete.tag.serial);
        assert_eq!(
            owner.validate_pending(obsolete.tag, 0),
            Err(Error::StaleTag)
        );
        assert!(owner.snapshot().unwrap().last_frame.is_none());
    }

    #[test]
    fn deselection_destroys_pending_and_previous_camera_but_keeps_global_serial() {
        let owner = selected();
        let first = prepare(&owner, 1.0);
        commit(&owner, &first);
        let pending = prepare(&owner, 2.0);
        assert_eq!(
            owner.apply_selection(None, 0).unwrap(),
            SelectionChange::Deselected
        );
        assert_eq!(
            owner.validate_pending(pending.tag, 0),
            Err(Error::NotSelected)
        );
        assert_eq!(owner.snapshot().unwrap().last_serial, pending.tag.serial);
        owner.apply_selection(Some(view(1)), 1).unwrap();
        let next = prepare(&owner, 3.0);
        assert!(next.tag.serial > pending.tag.serial);
        assert_eq!(next.frame.phase, 0);
        assert_eq!(
            next.frame.previous_camera_position,
            next.frame.camera_position
        );
        assert_eq!(owner.snapshot().unwrap().committed, 1);
    }

    #[test]
    fn cloned_packet_and_double_receipt_cannot_commit_twice() {
        let owner = selected();
        let packet = prepare(&owner, 1.0);
        let clone = packet.clone();
        commit(&owner, &packet);
        assert_eq!(
            owner.commit_resolve(clone.tag, clone.frame.history_epoch, 7, 9, 1, true, true),
            Err(Error::NoPendingFrame)
        );
        let next = prepare(&owner, 2.0);
        assert_eq!(
            owner.commit_resolve(clone.tag, clone.frame.history_epoch, 7, 9, 1, true, true),
            Err(Error::StaleTag)
        );
        assert_eq!(next.frame.phase, 1);
        assert_eq!(owner.snapshot().unwrap().committed, 1);
    }

    #[test]
    fn dropped_reservation_keeps_pending_phase_and_previous_camera() {
        let owner = selected();
        let first = prepare(&owner, 1.0);
        commit(&owner, &first);
        let packet = prepare(&owner, 2.0);
        let reservation = owner
            .reserve_pending(packet.tag, packet.frame.history_epoch)
            .unwrap();
        assert_eq!(reservation.tag(), packet.tag);
        assert_eq!(reservation.frame(), &packet.frame);
        assert!(owner.0.try_lock().is_err());
        drop(reservation);
        assert_eq!(owner.snapshot().unwrap().pending_frame, Some(packet.frame));
        assert_eq!(owner.snapshot().unwrap().committed, 1);
        let next = prepare(&owner, 4.0);
        assert_eq!(next.frame.phase, packet.frame.phase);
        assert_eq!(
            next.frame.previous_camera_position,
            first.frame.camera_position
        );
        assert_eq!(owner.snapshot().unwrap().rejected_commits, 0);
    }

    #[test]
    fn invalid_reserved_receipt_releases_lock_but_keeps_token_for_retry() {
        let owner = selected();
        let packet = prepare(&owner, 1.0);
        let reservation = owner
            .reserve_pending(packet.tag, packet.frame.history_epoch)
            .unwrap();
        assert_eq!(
            reservation.commit_resolve(7, 9, 1, false, true),
            Err(Error::MissingDepthCopy)
        );
        assert_eq!(owner.snapshot().unwrap().pending_frame, Some(packet.frame));
        assert_eq!(owner.snapshot().unwrap().committed, 0);
        assert_eq!(owner.snapshot().unwrap().rejected_commits, 1);
        let reservation = owner
            .reserve_pending(packet.tag, packet.frame.history_epoch)
            .unwrap();
        assert_eq!(
            reservation.commit_resolve(7, 9, 1, true, true),
            Ok(packet.frame)
        );
        assert_eq!(owner.snapshot().unwrap().committed, 1);
        assert_eq!(prepare(&owner, 4.0).frame.phase, 1);
    }

    #[test]
    fn poisoned_owner_is_rejected_at_reservation_before_encoding() {
        let owner = selected();
        let packet = prepare(&owner, 1.0);
        let intentional_panic = std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| {
            let _guard = owner.0.lock().unwrap();
            panic!("Test deliberate lock poisoning before any encoding");
        }));
        assert!(intentional_panic.is_err());
        assert!(matches!(
            owner.reserve_pending(packet.tag, packet.frame.history_epoch),
            Err(Error::StatePoisoned)
        ));
        // Inspect poisoned CPU state solely in this test: no token was consumed.
        let poisoned = owner.0.lock().err().unwrap().into_inner();
        assert_eq!(poisoned.committed, 0);
        assert_eq!(
            poisoned
                .selected
                .as_ref()
                .unwrap()
                .pending
                .as_ref()
                .unwrap()
                .tag,
            packet.tag
        );
    }

    #[test]
    fn invalid_camera_input_and_exhausted_serial_preserve_current_pending() {
        let owner = selected();
        let packet = prepare(&owner, 1.0);
        let mut invalid = input(2.0, UVec2::new(1920, 1080));
        invalid.near = 0.0;
        assert!(matches!(
            owner.prepare(view(1), invalid),
            Err(Error::Temporal(TemporalError::InvalidNearFar))
        ));
        assert_eq!(owner.snapshot().unwrap().last_serial, packet.tag.serial);
        assert!(
            owner
                .validate_pending(packet.tag, packet.frame.history_epoch)
                .is_ok()
        );
        owner.0.lock().unwrap().last_serial = u64::MAX;
        assert!(matches!(
            owner.prepare(view(1), input(2.0, UVec2::ONE)),
            Err(Error::SerialExhausted)
        ));
        assert!(
            owner
                .validate_pending(packet.tag, packet.frame.history_epoch)
                .is_ok()
        );
    }

    #[test]
    fn snapshot_records_only_accepted_current_instance_commits() {
        let owner = selected();
        let packet = prepare(&owner, 1.0);
        let committed = commit(&owner, &packet);
        let snapshot = owner.snapshot().unwrap();
        assert_eq!(snapshot.last_frame, Some(committed));
        assert_eq!(
            snapshot.last_commit,
            Some(Commit {
                tag: packet.tag,
                history_epoch: 0,
                resolve_generation: 7,
                auxiliary_generation: 9
            })
        );
        assert_eq!(
            snapshot.status,
            Status::Selected {
                view: view(1),
                reset_revision: 0
            }
        );
        assert!(snapshot.pending_frame.is_none());
        assert_eq!(snapshot.prepared, 1);
        assert_eq!(snapshot.committed, 1);
        owner.apply_selection(Some(view(2)), 0).unwrap();
        assert!(owner.snapshot().unwrap().last_frame.is_none());
        assert!(owner.snapshot().unwrap().last_commit.is_none());
    }
}
