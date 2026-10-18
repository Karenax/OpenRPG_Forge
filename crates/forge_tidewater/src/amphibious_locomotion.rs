//! Parametric walk/swim locomotion math, independent from Rapier and the render graph.
// SPDX-License-Identifier: MIT
// Adapted from Tidewater `src/player/Player.js` at 1438b1abfcaee3267092b75573014f4d9b4a983c.
// Copyright 2026 DRG Software Solutions LLC; see `third_party/tidewater/LICENSE`.

use bevy::prelude::{Component, Vec2, Vec3};
mod profile;
pub use profile::{InvalidMotionProfile, MotionProfile, ValidatedMotionProfile};

pub const RADIUS: f32 = 0.3;
pub const WALK_HEIGHT: f32 = 1.75;
pub const SWIM_HEIGHT: f32 = 1.0;
#[cfg(test)]
const EYE: f32 = 1.62;
#[cfg(test)]
const SWIM_EYE: f32 = EYE * 0.1;

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq)]
pub enum Mode {
    #[default]
    Walk,
    Swim,
}

/// One logical climbable volume retained separately from its rendered geometry.
#[derive(Clone, Copy, Component, Debug, PartialEq)]
pub struct ClimbableLadder {
    /// Horizontal centre of the source ladder collider.
    pub center_xz: Vec2,
    /// Highest walkable rung returned by the source collision query.
    pub top_y: f32,
    /// Foot anchor used when the swimmer steps onto the deck.
    pub exit_anchor: Vec3,
}

impl ClimbableLadder {
    #[must_use]
    pub const fn new(center_xz: Vec2, top_y: f32, exit_anchor: Vec3) -> Self {
        Self {
            center_xz,
            top_y,
            exit_anchor,
        }
    }

    #[must_use]
    pub fn is_finite(self) -> bool {
        self.center_xz.is_finite() && self.top_y.is_finite() && self.exit_anchor.is_finite()
    }
}

/// Result of the source ladder rule for the current fixed update.
#[derive(Clone, Copy, Debug, Default, Eq, PartialEq)]
pub enum LadderStep {
    #[default]
    OutOfRange,
    Available,
    Climbing,
    Completed,
}

#[derive(Clone, Copy, Debug, Default, PartialEq)]
#[allow(clippy::struct_excessive_bools)] // Independent source keyboard states can be held together.
pub struct Input {
    pub move_axis: Vec2,
    pub yaw: f32,
    pub pitch: f32,
    pub sprint: bool,
    pub jump: bool,
    pub ascend: bool,
    pub descend: bool,
    pub control_descend: bool,
}

/// Source-space player state. `position` is the foot anchor, not a capsule centre.
#[derive(Clone, Debug)]
pub struct State {
    profile: ValidatedMotionProfile,
    pub mode: Mode,
    pub position: Vec3,
    pub velocity: Vec3,
    pub grounded: bool,
    pub water_h: f32,
    pub water_mean: Option<f32>,
    pub floating: bool,
    pub wade: f32,
    pub bob: f32,
    camera_offset_y: f32,
    camera_offset_velocity: f32,
    camera_y: Option<f32>,
    old_position: Vec3,
    mode_before: Mode,
}

impl State {
    #[must_use]
    pub fn new(position: Vec3) -> Self {
        Self {
            profile: ValidatedMotionProfile::default(),
            mode: Mode::Walk,
            position,
            velocity: Vec3::ZERO,
            grounded: false,
            water_h: 0.0,
            water_mean: None,
            floating: true,
            wade: 0.0,
            bob: 0.0,
            camera_offset_y: 0.0,
            camera_offset_velocity: 0.0,
            camera_y: None,
            old_position: position,
            mode_before: Mode::Walk,
        }
    }

    /// Install explicit validated dynamics while preserving position and history.
    pub fn set_profile(&mut self, profile: ValidatedMotionProfile) {
        self.profile = profile;
    }
    #[must_use]
    pub const fn profile(&self) -> ValidatedMotionProfile {
        self.profile
    }

    /// Advances source locomotion until the external capsule solver runs.
    ///
    /// The returned position is the requested foot anchor. Call [`Self::resolve`]
    /// with Rapier's resulting foot anchor before the next advance.
    #[must_use]
    pub fn advance(&mut self, dt: f32, input: Input, height: f32) -> Vec3 {
        let dt = finite_nonnegative(dt);
        self.mode_before = self.mode;
        self.old_position = self.position;
        if height.is_finite() {
            self.water_h = height;
        }
        self.water_mean = Some(match self.water_mean {
            Some(mean) => {
                mean + (self.water_h - mean)
                    * (1.0 - (-dt / self.profile.0.water_average_seconds).exp())
            }
            None => self.water_h,
        });

        match self.mode {
            Mode::Walk => self.advance_walk(dt, input),
            Mode::Swim => self.advance_swim(dt, input),
        }
        self.position
    }

    /// Applies the post-collision foot anchor and source ground/transition rules.
    pub fn resolve(&mut self, position: Vec3, ground: f32) {
        if position.is_finite() {
            self.position = position;
        }
        let ground = if ground.is_finite() {
            ground
        } else {
            self.position.y
        };
        match self.mode {
            Mode::Walk => self.resolve_walk(ground),
            Mode::Swim => self.resolve_swim(ground),
        }
    }

    /// Applies the post-swim ladder rule after collision resolution.
    pub fn climb_ladder(
        &mut self,
        dt: f32,
        climb_held: bool,
        ladder: ClimbableLadder,
    ) -> LadderStep {
        let delta = Vec2::new(self.position.x, self.position.z) - ladder.center_xz;
        if !ladder.is_finite() || delta.length() >= self.profile.0.ladder_radius_m {
            return LadderStep::OutOfRange;
        }
        if !climb_held || ladder.top_y <= self.position.y {
            return LadderStep::Available;
        }
        self.position.y += finite_nonnegative(dt) * self.profile.0.ladder_speed_mps;
        if self.position.y > ladder.top_y - self.profile.0.ladder_exit_margin_m {
            self.position = ladder.exit_anchor;
            self.mode = Mode::Walk;
            self.velocity = Vec3::ZERO;
            LadderStep::Completed
        } else {
            LadderStep::Climbing
        }
    }

    /// Computes the source camera eye after collision resolution.
    #[must_use]
    pub fn eye(&mut self, dt: f32) -> Vec3 {
        let dt = finite_nonnegative(dt);
        let mut eye = self.position;
        eye.y += match self.mode {
            Mode::Walk => {
                self.profile.0.eye_height_m + self.bob.sin() * 0.035 * (1.0 - 0.6 * self.wade)
            }
            Mode::Swim => self.profile.0.swim_eye_height_m,
        };
        match self.camera_y {
            None => {
                self.camera_offset_y = 0.0;
                self.camera_offset_velocity = 0.0;
            }
            Some(previous) if self.mode != self.mode_before => {
                self.camera_offset_y = previous - eye.y;
            }
            Some(_) => {}
        }
        let decay = (-6.0 * dt).exp();
        let j = (self.camera_offset_velocity + 6.0 * self.camera_offset_y) * dt;
        self.camera_offset_y = (self.camera_offset_y + j) * decay;
        self.camera_offset_velocity = (self.camera_offset_velocity - 6.0 * j) * decay;
        eye.y += self.camera_offset_y;
        self.camera_y = Some(eye.y);
        eye
    }

    fn advance_walk(&mut self, dt: f32, input: Input) {
        let forward = Vec3::new(-input.yaw.sin(), 0.0, -input.yaw.cos());
        let right = Vec3::new(-forward.z, 0.0, forward.x);
        let wish = (right * input.move_axis.x + forward * input.move_axis.y).normalize_or_zero();
        let depth = self.water_h - self.position.y;
        self.wade = (depth / self.profile.0.wade_depth_m).clamp(0.0, 1.0);
        let speed = if input.sprint {
            self.profile.0.sprint_speed_mps
        } else {
            self.profile.0.walk_speed_mps
        } * lerp(1.0, self.profile.0.wade_speed_factor, self.wade);
        let acceleration = if self.grounded {
            self.profile.0.ground_response
        } else {
            self.profile.0.air_response
        };
        let response = 1.0 - (-acceleration * dt).exp();
        self.velocity.x += (wish.x * speed - self.velocity.x) * response;
        self.velocity.z += (wish.z * speed - self.velocity.z) * response;
        if self.grounded && input.jump && depth < 0.9 {
            self.velocity.y = self.profile.0.jump_impulse_mps;
            self.grounded = false;
        }
        self.velocity.y -= self.profile.0.gravity_magnitude_mps2 * dt;
        if depth > 0.0 {
            self.velocity *= (-dt * depth * self.profile.0.water_drag).exp();
        }
        self.position += self.velocity * dt;
    }

    fn resolve_walk(&mut self, ground: f32) {
        if self.position.y <= ground {
            self.position.y = ground;
            if self.velocity.y < 0.0 {
                self.velocity.y = 0.0;
            }
            self.grounded = true;
        } else {
            self.grounded = self.position.y - ground < 0.06;
        }
        if self.grounded {
            let delta = self.position - self.old_position;
            self.bob += delta.x.hypot(delta.z) * 2.4;
        }
        if self.water_mean.unwrap_or(self.water_h) - self.position.y
            > self.profile.0.swim_entry_depth_m
        {
            let waded_in = self.grounded;
            self.mode = Mode::Swim;
            if waded_in {
                self.position.y = self
                    .position
                    .y
                    .max(self.water_h - self.profile.0.swim_eye_height_m + 0.1);
            }
            self.velocity.y = if waded_in {
                0.0
            } else {
                (self.velocity.y * 0.3).max(-2.5)
            };
        }
    }

    fn advance_swim(&mut self, dt: f32, input: Input) {
        let forward = yaw_pitch_forward(input.yaw, input.pitch);
        let right = Vec3::new(input.yaw.cos(), 0.0, -input.yaw.sin());
        let mut wish = forward * input.move_axis.y + right * input.move_axis.x;
        if input.ascend {
            wish.y += 1.0;
        }
        if input.descend || input.control_descend {
            wish.y -= 1.0;
        }
        wish = wish.normalize_or_zero();
        let at_surface = self.floating && self.position.y > self.water_h - 0.45;
        if at_surface && wish.y > -0.25 && !input.descend {
            wish.y = wish.y.max(0.0);
        }
        let speed = if input.sprint {
            self.profile.0.swim_sprint_speed_mps
        } else {
            self.profile.0.swim_speed_mps
        };
        wish *= speed;
        let response = 1.0 - (-dt * self.profile.0.swim_response).exp();
        self.velocity += (wish - self.velocity) * response;

        let eye_target = self.water_h - self.profile.0.swim_eye_height_m + 0.1;
        let diving =
            input.descend || (input.move_axis.y > 0.0 && input.pitch < -0.35) || wish.y < -0.1;
        if diving {
            self.floating = false;
        } else if self.position.y > eye_target - 0.15 {
            self.floating = true;
        }
        if self.floating {
            self.position.y += (eye_target - self.position.y) * (1.0 - (-dt * 5.0).exp());
            if self.velocity.y > 0.0 {
                self.velocity.y *= 0.5;
            }
        }
        self.position += self.velocity * dt;
        self.position.y = self.position.y.min(self.water_h + 0.05);
    }

    fn resolve_swim(&mut self, ground: f32) {
        self.position.y = self.position.y.max(ground + 0.25);
        if self.water_mean.unwrap_or(self.water_h) - ground < self.profile.0.stand_depth_m {
            self.mode = Mode::Walk;
            self.position.y = ground;
            self.velocity.y = 0.0;
        }
    }
}

fn yaw_pitch_forward(yaw: f32, pitch: f32) -> Vec3 {
    let cos_pitch = pitch.cos();
    Vec3::new(-yaw.sin() * cos_pitch, pitch.sin(), -yaw.cos() * cos_pitch)
}

fn finite_nonnegative(value: f32) -> f32 {
    if value.is_finite() {
        value.max(0.0)
    } else {
        0.0
    }
}

fn lerp(start: f32, end: f32, amount: f32) -> f32 {
    start + (end - start) * amount
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::prelude::default;

    fn walk_input() -> Input {
        Input {
            move_axis: Vec2::Y,
            ..default()
        }
    }

    #[test]
    fn wading_slows_a_grounded_jump() {
        let mut state = State::new(Vec3::ZERO);
        state.grounded = true;
        let input = Input {
            jump: true,
            ..walk_input()
        };
        let mut dry = state.clone();
        let _ = dry.advance(1.0 / 60.0, input, 0.0);
        let _ = state.advance(1.0 / 60.0, input, 0.6);
        assert_eq!(state.wade.to_bits(), 0.5_f32.to_bits());
        assert!(state.velocity.y > 0.0);
        assert!(state.velocity.z.abs() < dry.velocity.z.abs());
        assert!(state.velocity.y < dry.velocity.y);
    }

    #[test]
    fn deep_wade_transitions_to_swimming_at_the_surface() {
        let mut state = State::new(Vec3::ZERO);
        state.grounded = true;
        let _ = state.advance(1.0 / 60.0, Input::default(), 2.0);
        state.resolve(Vec3::ZERO, 0.0);
        assert_eq!(state.mode, Mode::Swim);
        assert_eq!(state.position.y.to_bits(), (2.0 - SWIM_EYE + 0.1).to_bits());
    }

    #[test]
    fn underwater_swimmer_has_neutral_buoyancy() {
        let mut state = State::new(Vec3::new(0.0, -2.0, 0.0));
        state.mode = Mode::Swim;
        state.floating = false;
        state.water_mean = Some(3.0);
        let _ = state.advance(0.1, Input::default(), 0.0);
        assert_eq!(state.position.y.to_bits(), (-2.0_f32).to_bits());
    }

    #[test]
    fn surface_motion_is_clamped_above_the_water() {
        let mut state = State::new(Vec3::new(0.0, 0.04, 0.0));
        state.mode = Mode::Swim;
        state.floating = true;
        state.water_mean = Some(2.0);
        let _ = state.advance(
            1.0 / 60.0,
            Input {
                ascend: true,
                ..default()
            },
            0.0,
        );
        assert!(state.position.y <= 0.05);
    }

    #[test]
    fn swimming_collision_result_is_clamped_to_the_floor() {
        let mut state = State::new(Vec3::ZERO);
        state.mode = Mode::Swim;
        state.water_mean = Some(4.0);
        state.resolve(Vec3::new(0.0, -9.0, 0.0), 1.0);
        assert_eq!(state.position.y.to_bits(), 1.25_f32.to_bits());
        assert_eq!(state.mode, Mode::Swim);
    }

    #[test]
    fn camera_eases_between_walk_and_swim_eyes() {
        let mut state = State::new(Vec3::ZERO);
        let walk = state.eye(1.0 / 60.0);
        state.mode_before = Mode::Walk;
        state.mode = Mode::Swim;
        state.position.y = -SWIM_EYE + 0.1;
        let entering = state.eye(1.0 / 60.0);
        assert!(entering.y > state.position.y + SWIM_EYE);
        assert!(entering.y < walk.y);
        state.mode_before = Mode::Swim;
        for _ in 0..120 {
            let _ = state.eye(1.0 / 60.0);
        }
        assert!((state.eye(1.0 / 60.0).y - 0.1).abs() < 0.01);
    }

    #[test]
    fn ladder_only_prompts_inside_the_source_radius() {
        let ladder = ClimbableLadder::new(Vec2::ZERO, 1.95, Vec3::new(-1.2, 2.3, 0.0));
        let mut state = State::new(Vec3::new(1.1, 0.0, 0.0));
        state.mode = Mode::Swim;
        assert_eq!(
            state.climb_ladder(1.0 / 60.0, true, ladder),
            LadderStep::OutOfRange
        );
        state.position.x = 1.099;
        assert_eq!(
            state.climb_ladder(1.0 / 60.0, false, ladder),
            LadderStep::Available
        );
        assert_eq!(state.position.y.to_bits(), 0.0_f32.to_bits());
    }

    #[test]
    fn ladder_climbs_at_the_source_speed() {
        let ladder = ClimbableLadder::new(Vec2::ZERO, 1.95, Vec3::new(-1.2, 2.3, 0.0));
        let mut state = State::new(Vec3::ZERO);
        state.mode = Mode::Swim;
        assert_eq!(state.climb_ladder(0.25, true, ladder), LadderStep::Climbing);
        assert_eq!(state.position.y.to_bits(), 0.4_f32.to_bits());
        assert_eq!(state.mode, Mode::Swim);
    }

    #[test]
    fn ladder_steps_onto_the_deck_past_the_source_threshold() {
        let exit = Vec3::new(61.12, 2.3, 35.1);
        let ladder = ClimbableLadder::new(Vec2::new(62.32, 35.1), 1.95, exit);
        let mut state = State::new(Vec3::new(62.32, 0.94, 35.1));
        state.mode = Mode::Swim;
        state.velocity = Vec3::splat(2.0);
        assert_eq!(
            state.climb_ladder(1.0 / 60.0, true, ladder),
            LadderStep::Completed
        );
        assert_eq!(state.position, exit);
        assert_eq!(state.mode, Mode::Walk);
        assert_eq!(state.velocity, Vec3::ZERO);
    }
}
