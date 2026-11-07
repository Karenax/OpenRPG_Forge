// SPDX-License-Identifier: MIT
// Adapted from Tidewater TemporalUpscale.js, PostFX.js and engine/render/Frame.js
// at 1438b1abfcaee3267092b75573014f4d9b4a983c.
// Copyright 2026 DRG Software Solutions LLC; see third_party/tidewater/LICENSE.
//! Pure derived camera-frame contract for the pinned Tidewater temporal resolve.
//!
//! This module installs no ECS system, jitter component, render pass or GPU target.
//! Preparing a frame is immutable. Only committing an encoded frame advances the
//! 32-phase sequence and the previous-camera snapshot. The adapter must commit
//! after encoding the resolve and its depth copy, before preparing the next frame.
//! Matrix/size observations are not evidence of rendered velocities or source parity.

use bevy::math::{Mat4, UVec2, Vec2, Vec3, Vec4};
use std::{error::Error as StdError, fmt};

const PHASE_COUNT: u8 = 32;
const MAX_EXACT_PIXEL_DIMENSION: u32 = 1 << 24;

/// Caller-owned identity of one derived view sequence, not an authoring identity.
///
/// The adapter must use distinct keys for independently owned live sequences.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct ViewKey(pub u64);

/// Actual unjittered camera and physical input/output sizes supplied by an adapter.
#[derive(Clone, Copy, Debug)]
pub struct Input {
    pub world_from_view: Mat4,
    pub clip_from_view: Mat4,
    pub input_size: UVec2,
    pub output_size: UVec2,
    pub near: f32,
    pub far: f32,
}

/// CPU matrices and phase of one prepared/encoded frame. No GPU handles persist here.
#[derive(Clone, Copy, Debug, PartialEq)]
pub struct Frame {
    pub view: ViewKey,
    pub phase: u8,
    pub history_epoch: u64,
    pub needs_history_seed: bool,
    pub input_size: UVec2,
    pub output_size: UVec2,
    /// Halton offset in input pixels, before conversion to clip-space translation.
    pub jitter_pixels: Vec2,
    pub near_far: Vec2,
    pub view_from_world: Mat4,
    /// Unjittered projection, as the source `frame.proj` field.
    pub clip_from_view: Mat4,
    pub unjittered_clip_from_world: Mat4,
    pub clip_from_world: Mat4,
    pub world_from_clip: Mat4,
    pub previous_unjittered_clip_from_world: Mat4,
    /// Inverse VP of the previous jittered image, for previous-depth reconstruction.
    pub previous_world_from_clip: Mat4,
    pub camera_position: Vec3,
    pub previous_camera_position: Vec3,
}

#[derive(Clone, Copy, Debug)]
struct Previous {
    input_size: UVec2,
    output_size: UVec2,
    unjittered_clip_from_world: Mat4,
    world_from_clip: Mat4,
    camera_position: Vec3,
}

/// Opaque commit token. Cloned or obsolete tokens cannot advance a sequence twice.
#[derive(Clone, Debug)]
pub struct PreparedFrame {
    frame: Frame,
    serial: u64,
    next_serial: u64,
}

impl PreparedFrame {
    #[must_use]
    pub const fn frame(&self) -> &Frame {
        &self.frame
    }
}

/// One view's committed temporal state. No clock, renderer or author document is owned.
#[derive(Debug)]
pub struct Sequence {
    view: ViewKey,
    phase: u8,
    serial: u64,
    history_epoch: u64,
    previous: Option<Previous>,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Error {
    InvalidSize,
    InvalidNearFar,
    NonfiniteMatrix,
    InvalidCameraTransform,
    NonInvertibleMatrix,
    ForeignView,
    StaleFrame,
    CounterExhausted,
}

impl fmt::Display for Error {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str(match self {
            Self::InvalidSize => "Temporal input/output sizes must be positive, exactly representable pixel dimensions, with input no larger than output",
            Self::InvalidNearFar => "Temporal near/far must be finite with 0 < near < far",
            Self::NonfiniteMatrix => "Temporal camera matrices contain nonfinite values",
            Self::InvalidCameraTransform => "Temporal camera world transform must be affine",
            Self::NonInvertibleMatrix => "Temporal camera matrices have no finite inverse",
            Self::ForeignView => "Temporal commit belongs to another view",
            Self::StaleFrame => "Temporal frame was already committed or superseded by a reset",
            Self::CounterExhausted => "Temporal sequence identity counter exhausted",
        })
    }
}

impl StdError for Error {}

impl Sequence {
    #[must_use]
    pub const fn new(view: ViewKey) -> Self {
        Self {
            view,
            phase: 0,
            serial: 0,
            history_epoch: 0,
            previous: None,
        }
    }

    /// Prepare from the last committed camera without consuming a Halton phase.
    ///
    /// Size changes request a history seed but preserve camera history and phase,
    /// matching the pinned source's resize behavior. Projection changes alone do
    /// not reset history. An adapter can explicitly reset on a view lifecycle cut.
    pub fn prepare(&self, input: Input) -> Result<PreparedFrame, Error> {
        validate(input)?;
        let next_serial = self.serial.checked_add(1).ok_or(Error::CounterExhausted)?;
        let view_from_world = inverse(input.world_from_view)?;
        let unjittered_clip_from_world = input.clip_from_view * view_from_world;
        let jitter_pixels = halton_offset(self.phase);
        let clip_shift = jitter_pixels * Vec2::new(-2.0, 2.0) / input.input_size.as_vec2();
        // Source setFrameCamera premultiplies the completed unjittered VP by a
        // clip translation, instead of jittering current/previous object poses.
        let clip_from_world =
            Mat4::from_translation(clip_shift.extend(0.0)) * unjittered_clip_from_world;
        let world_from_clip = inverse(clip_from_world)?;
        let camera_position = input.world_from_view.w_axis.truncate();
        let resized = self.previous.is_some_and(|previous| {
            previous.input_size != input.input_size || previous.output_size != input.output_size
        });
        let history_epoch = if resized {
            self.history_epoch
                .checked_add(1)
                .ok_or(Error::CounterExhausted)?
        } else {
            self.history_epoch
        };
        let previous = self.previous.unwrap_or(Previous {
            input_size: input.input_size,
            output_size: input.output_size,
            unjittered_clip_from_world,
            world_from_clip,
            camera_position,
        });
        Ok(PreparedFrame {
            frame: Frame {
                view: self.view,
                phase: self.phase,
                history_epoch,
                needs_history_seed: self.previous.is_none() || resized,
                input_size: input.input_size,
                output_size: input.output_size,
                jitter_pixels,
                near_far: Vec2::new(input.near, input.far),
                view_from_world,
                clip_from_view: input.clip_from_view,
                unjittered_clip_from_world,
                clip_from_world,
                world_from_clip,
                previous_unjittered_clip_from_world: previous.unjittered_clip_from_world,
                previous_world_from_clip: previous.world_from_clip,
                camera_position,
                previous_camera_position: previous.camera_position,
            },
            serial: self.serial,
            next_serial,
        })
    }

    /// Advance after this view's frame and previous-depth copy have been encoded.
    ///
    /// This call is a CPU ownership transition, not a GPU completion fence.
    #[allow(
        clippy::needless_pass_by_value,
        reason = "Commit consumes the prepared token so callers cannot reuse it."
    )]
    pub fn commit(&mut self, prepared: PreparedFrame) -> Result<Frame, Error> {
        if prepared.frame.view != self.view {
            return Err(Error::ForeignView);
        }
        if prepared.serial != self.serial {
            return Err(Error::StaleFrame);
        }
        let frame = prepared.frame;
        self.previous = Some(Previous {
            input_size: frame.input_size,
            output_size: frame.output_size,
            unjittered_clip_from_world: frame.unjittered_clip_from_world,
            world_from_clip: frame.world_from_clip,
            camera_position: frame.camera_position,
        });
        self.serial = prepared.next_serial;
        self.history_epoch = frame.history_epoch;
        self.phase = (self.phase + 1) % PHASE_COUNT;
        Ok(frame)
    }

    /// Reinitialize this view, equivalent to a new source temporal instance.
    ///
    /// The pinned source does not automatically detect camera cuts. Callers own
    /// this explicit lifecycle policy; resize by itself must not call this method.
    pub fn reset(&mut self) -> Result<(), Error> {
        let serial = self.serial.checked_add(1).ok_or(Error::CounterExhausted)?;
        let history_epoch = self
            .history_epoch
            .checked_add(1)
            .ok_or(Error::CounterExhausted)?;
        self.serial = serial;
        self.history_epoch = history_epoch;
        self.phase = 0;
        self.previous = None;
        Ok(())
    }
}

/// Halton(2,3), indices 1..32, converted as the source's f64 JS numbers to f32 uniforms.
#[must_use]
#[allow(
    clippy::cast_possible_truncation,
    reason = "Source writes bounded JS f64 offsets into f32 GPU uniforms"
)]
pub fn halton_offset(phase: u8) -> Vec2 {
    let index = u32::from(phase % PHASE_COUNT) + 1;
    Vec2::new(
        (halton(index, 2) - 0.5) as f32,
        (halton(index, 3) - 0.5) as f32,
    )
}

fn halton(mut index: u32, base: u32) -> f64 {
    let mut fraction = 1.0;
    let mut result = 0.0;
    while index > 0 {
        fraction /= f64::from(base);
        result += fraction * f64::from(index % base);
        index /= base;
    }
    result
}

fn validate(input: Input) -> Result<(), Error> {
    if input.input_size.min_element() == 0
        || input.output_size.min_element() == 0
        || input.input_size.max_element() > MAX_EXACT_PIXEL_DIMENSION
        || input.output_size.max_element() > MAX_EXACT_PIXEL_DIMENSION
        || input.input_size.cmpgt(input.output_size).any()
    {
        return Err(Error::InvalidSize);
    }
    if !input.near.is_finite()
        || !input.far.is_finite()
        || input.near <= 0.0
        || input.far <= input.near
    {
        return Err(Error::InvalidNearFar);
    }
    if !input.world_from_view.is_finite() || !input.clip_from_view.is_finite() {
        return Err(Error::NonfiniteMatrix);
    }
    if input.world_from_view.row(3) != Vec4::W {
        return Err(Error::InvalidCameraTransform);
    }
    Ok(())
}

fn inverse(matrix: Mat4) -> Result<Mat4, Error> {
    let determinant = matrix.determinant();
    if !determinant.is_finite() || determinant.abs() <= 0.0 {
        return Err(Error::NonInvertibleMatrix);
    }
    let inverse = matrix.inverse();
    if inverse.is_finite() {
        Ok(inverse)
    } else {
        Err(Error::NonInvertibleMatrix)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::math::Quat;

    fn input() -> Input {
        Input {
            world_from_view: Mat4::IDENTITY,
            clip_from_view: Mat4::perspective_infinite_reverse_rh(
                62.0_f32.to_radians(),
                1920.0 / 1080.0,
                0.06,
            ),
            input_size: UVec2::new(1920, 1080),
            output_size: UVec2::new(1920, 1080),
            near: 0.06,
            far: 60_000.0,
        }
    }

    fn assert_matrix_near(actual: Mat4, expected: Mat4) {
        for (actual, expected) in actual
            .to_cols_array()
            .into_iter()
            .zip(expected.to_cols_array())
        {
            assert!((actual - expected).abs() < 1e-5, "{actual} != {expected}");
        }
    }

    // Independently extracted from the pinned JS algorithm, then written through
    // Float32Array / Uint32Array by Node. These are not rounded Bevy Halton8 values.
    #[test]
    fn all_32_halton_uniform_bits_match_pinned_source() {
        let bits = [
            [0x0000_0000, 0xbe2a_aaab],
            [0xbe80_0000, 0x3e2a_aaab],
            [0x3e80_0000, 0xbec7_1c72],
            [0xbec0_0000, 0xbd63_8e39],
            [0x3e00_0000, 0x3e8e_38e4],
            [0xbe00_0000, 0xbe8e_38e4],
            [0x3ec0_0000, 0x3d63_8e39],
            [0xbee0_0000, 0x3ec7_1c72],
            [0x3d80_0000, 0xbeed_097b],
            [0xbe40_0000, 0xbe04_bda1],
            [0x3ea0_0000, 0x3e50_97b4],
            [0xbea0_0000, 0xbeb4_25ed],
            [0x3e40_0000, 0xbc97_b426],
            [0xbd80_0000, 0x3ea1_2f68],
            [0x3ee0_0000, 0xbe76_84be],
            [0xbef0_0000, 0x3dbd_a12f],
            [0x3d00_0000, 0x3eda_12f7],
            [0xbe60_0000, 0xbeda_12f7],
            [0x3e90_0000, 0xbdbd_a12f],
            [0xbeb0_0000, 0x3e76_84be],
            [0x3e20_0000, 0xbea1_2f68],
            [0xbdc0_0000, 0x3c97_b426],
            [0x3ed0_0000, 0x3eb4_25ed],
            [0xbed0_0000, 0xbe50_97b4],
            [0x3dc0_0000, 0x3e04_bda1],
            [0xbe20_0000, 0x3eed_097b],
            [0x3eb0_0000, 0xbef9_add4],
            [0xbe90_0000, 0xbe1e_0652],
            [0x3e60_0000, 0x3e37_4f03],
            [0xbd00_0000, 0xbec0_ca46],
            [0x3ef0_0000, 0xbd30_fcd7],
            [0xbef8_0000, 0x3e94_8b10],
        ];
        for (phase, expected) in (0..32).zip(bits) {
            assert_eq!(halton_offset(phase).to_array().map(f32::to_bits), expected);
        }
        assert_eq!(halton_offset(32), halton_offset(0));
    }

    #[test]
    fn first_frame_repeated_prepare_and_unique_commit_preserve_history() {
        let mut sequence = Sequence::new(ViewKey(1));
        let first = sequence.prepare(input()).expect("first frame");
        let repeated = sequence.prepare(input()).expect("repeated preparation");
        assert_eq!(first.frame(), repeated.frame());
        assert_eq!(first.frame().phase, 0);
        assert!(first.frame().needs_history_seed);
        assert_eq!(
            first.frame().previous_world_from_clip,
            first.frame().world_from_clip
        );
        assert_eq!(
            first.frame().previous_unjittered_clip_from_world,
            first.frame().unjittered_clip_from_world
        );
        let committed = sequence.commit(first).expect("commit once");
        assert_eq!(sequence.commit(repeated), Err(Error::StaleFrame));
        let next = sequence.prepare(input()).expect("next frame");
        assert_eq!(next.frame().phase, 1);
        assert!(!next.frame().needs_history_seed);
        assert_eq!(
            next.frame().previous_world_from_clip,
            committed.world_from_clip
        );
        assert_eq!(
            next.frame().previous_unjittered_clip_from_world,
            committed.unjittered_clip_from_world
        );
        let mut other = Sequence::new(ViewKey(2));
        assert_eq!(other.commit(next), Err(Error::ForeignView));
        for phase in 1..32 {
            let frame = sequence.prepare(input()).expect("wrapped sequence");
            assert_eq!(frame.frame().phase, phase);
            sequence.commit(frame).expect("wrapped commit");
        }
        assert_eq!(
            sequence.prepare(input()).expect("phase zero").frame().phase,
            0
        );
    }

    #[test]
    fn only_clip_matrix_is_jittered_and_previous_camera_tracks_commits() {
        let mut sequence = Sequence::new(ViewKey(3));
        let first = sequence.prepare(input()).expect("first frame");
        let first = sequence.commit(first).expect("first commit");
        let mut moved = input();
        moved.world_from_view =
            Mat4::from_rotation_translation(Quat::from_rotation_y(0.07), Vec3::new(0.3, 0.1, -0.2));
        let second = sequence.prepare(moved).expect("moved camera");
        let frame = second.frame();
        assert_eq!(frame.camera_position, Vec3::new(0.3, 0.1, -0.2));
        assert_eq!(frame.previous_camera_position, Vec3::ZERO);
        assert_eq!(
            frame.previous_unjittered_clip_from_world,
            first.unjittered_clip_from_world
        );
        assert_matrix_near(
            frame.unjittered_clip_from_world,
            moved.clip_from_view * moved.world_from_view.inverse(),
        );
        assert_matrix_near(
            frame.world_from_clip * frame.clip_from_world,
            Mat4::IDENTITY,
        );
        let point = Vec3::new(2.0, 1.0, -15.0).extend(1.0);
        let current = frame.unjittered_clip_from_world * point;
        let jittered = frame.clip_from_world * point;
        let clip_delta =
            jittered.truncate().truncate() / jittered.w - current.truncate().truncate() / current.w;
        let expected = frame.jitter_pixels * Vec2::new(-2.0, 2.0) / moved.input_size.as_vec2();
        assert!((clip_delta - expected).length() < 1e-6);
        for full in [moved.input_size, UVec2::new(1919, 1079)] {
            let half = UVec2::new(full.x.div_ceil(2), full.y.div_ceil(2));
            let scaled = frame.jitter_pixels * half.as_vec2() / full.as_vec2();
            let capture_delta = scaled * Vec2::new(-2.0, 2.0) / half.as_vec2();
            let main_delta = frame.jitter_pixels * Vec2::new(-2.0, 2.0) / full.as_vec2();
            assert!((capture_delta - main_delta).length() < 1e-8);
        }
        let second = sequence.commit(second).expect("moved commit");
        let third = sequence.prepare(moved).expect("same pose, next jitter");
        assert_eq!(third.frame().camera_position, second.camera_position);
        assert_eq!(
            third.frame().previous_camera_position,
            second.camera_position
        );
        assert_eq!(
            third.frame().unjittered_clip_from_world,
            second.unjittered_clip_from_world
        );
        assert_ne!(third.frame().clip_from_world, second.clip_from_world);
        let p = third.frame().unjittered_clip_from_world * point;
        let q = third.frame().previous_unjittered_clip_from_world * point;
        let velocity =
            (p.truncate().truncate() / p.w - q.truncate().truncate() / q.w) * Vec2::new(0.5, -0.5);
        assert_eq!(velocity, Vec2::ZERO);
    }

    #[test]
    fn resize_seeds_without_resetting_phase_but_explicit_reset_invalidates_tokens() {
        let mut sequence = Sequence::new(ViewKey(4));
        let first = sequence.prepare(input()).expect("first frame");
        let first = sequence.commit(first).expect("first commit");
        let resized = Input {
            input_size: UVec2::new(960, 540),
            output_size: UVec2::new(1919, 1079),
            ..input()
        };
        let resized = sequence.prepare(resized).expect("odd resize");
        assert_eq!(resized.frame().phase, 1);
        assert_eq!(resized.frame().history_epoch, 1);
        assert!(resized.frame().needs_history_seed);
        assert_eq!(
            resized.frame().previous_world_from_clip,
            first.world_from_clip
        );
        assert_eq!(
            sequence
                .prepare(input())
                .expect("uncommitted resize")
                .frame()
                .history_epoch,
            0
        );
        sequence.reset().expect("explicit lifecycle reset");
        assert_eq!(sequence.commit(resized), Err(Error::StaleFrame));
        let restart = sequence.prepare(input()).expect("restart");
        assert_eq!(restart.frame().phase, 0);
        assert_eq!(restart.frame().history_epoch, 1);
        assert!(restart.frame().needs_history_seed);
        assert_eq!(
            restart.frame().previous_world_from_clip,
            restart.frame().world_from_clip
        );
    }

    #[test]
    fn invalid_inputs_and_exhausted_counters_never_mutate_the_sequence() {
        let mut sequence = Sequence::new(ViewKey(5));
        let original = sequence.prepare(input()).expect("valid frame");
        for (invalid, expected) in [
            (
                Input {
                    input_size: UVec2::ZERO,
                    ..input()
                },
                Error::InvalidSize,
            ),
            (
                Input {
                    input_size: UVec2::new(1921, 1080),
                    ..input()
                },
                Error::InvalidSize,
            ),
            (
                Input {
                    output_size: UVec2::splat(MAX_EXACT_PIXEL_DIMENSION + 1),
                    ..input()
                },
                Error::InvalidSize,
            ),
            (
                Input {
                    near: f32::NAN,
                    ..input()
                },
                Error::InvalidNearFar,
            ),
            (
                Input {
                    far: f32::INFINITY,
                    ..input()
                },
                Error::InvalidNearFar,
            ),
            (
                Input {
                    far: 0.06,
                    ..input()
                },
                Error::InvalidNearFar,
            ),
            (
                Input {
                    clip_from_view: Mat4::from_cols_array(&[f32::NAN; 16]),
                    ..input()
                },
                Error::NonfiniteMatrix,
            ),
            (
                Input {
                    clip_from_view: Mat4::ZERO,
                    ..input()
                },
                Error::NonInvertibleMatrix,
            ),
            (
                Input {
                    world_from_view: Mat4::from_scale(Vec3::new(0.0, 1.0, 1.0)),
                    ..input()
                },
                Error::NonInvertibleMatrix,
            ),
            (
                Input {
                    world_from_view: input().clip_from_view,
                    ..input()
                },
                Error::InvalidCameraTransform,
            ),
        ] {
            assert_eq!(
                sequence.prepare(invalid).expect_err("invalid input"),
                expected
            );
            assert_eq!(
                sequence.prepare(input()).expect("unchanged").frame(),
                original.frame()
            );
        }
        sequence.serial = u64::MAX;
        assert_eq!(
            sequence.prepare(input()).expect_err("serial exhausted"),
            Error::CounterExhausted
        );
        assert_eq!(sequence.reset(), Err(Error::CounterExhausted));
        assert_eq!(sequence.phase, 0);
        assert_eq!(sequence.history_epoch, 0);
        assert!(sequence.previous.is_none());
    }
}
