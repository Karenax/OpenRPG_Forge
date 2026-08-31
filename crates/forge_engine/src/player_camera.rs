use bevy::prelude::*;
use forge_project::PlayerCameraProfile;

/// Runtime view selected for the single stable player-camera entity.
#[derive(Clone, Copy, Debug, Default, Eq, PartialEq)]
pub enum PlayerCameraMode {
    /// Orbit camera with a collision-aware boom.
    #[default]
    ThirdPerson,
    /// Eye-level camera using the same Bevy entity and projection.
    FirstPerson,
}

impl PlayerCameraMode {
    #[must_use]
    pub const fn toggled(self) -> Self {
        match self {
            Self::ThirdPerson => Self::FirstPerson,
            Self::FirstPerson => Self::ThirdPerson,
        }
    }
}

#[derive(Clone, Copy, Component, Debug)]
pub(crate) struct PlayerCameraRig {
    pub(crate) mode: PlayerCameraMode,
    pub(crate) yaw_radians: f32,
    pub(crate) pitch_radians: f32,
    pub(crate) boom_distance_m: f32,
}

impl PlayerCameraRig {
    pub(crate) fn new(profile: &PlayerCameraProfile) -> Self {
        let initial_pitch_degrees =
            (-12.0_f32).clamp(profile.pitch_min_degrees, profile.pitch_max_degrees);
        Self {
            mode: PlayerCameraMode::ThirdPerson,
            yaw_radians: 0.0,
            pitch_radians: initial_pitch_degrees.to_radians(),
            boom_distance_m: profile.third_person_distance_m,
        }
    }

    #[allow(
        clippy::cast_possible_truncation,
        reason = "yaw is wrapped to [-pi, pi) and pitch is profile-clamped before conversion to f32"
    )]
    pub(crate) fn apply_look(&mut self, look_delta: Vec2, profile: &PlayerCameraProfile) {
        // Commands guarantee finite f32 inputs, but a finite multiplication can still overflow in
        // f32. Accumulate in f64, wrap yaw, clamp pitch, then project back to finite f32 angles.
        let sensitivity = f64::from(profile.look_sensitivity_degrees_per_unit).to_radians();
        let yaw = f64::from(self.yaw_radians) - f64::from(look_delta.x) * sensitivity;
        self.yaw_radians = wrap_angle_radians(yaw) as f32;

        let pitch = f64::from(self.pitch_radians) - f64::from(look_delta.y) * sensitivity;
        self.pitch_radians = pitch.clamp(
            f64::from(profile.pitch_min_degrees).to_radians(),
            f64::from(profile.pitch_max_degrees).to_radians(),
        ) as f32;
    }

    pub(crate) fn toggle_mode(&mut self, profile: &PlayerCameraProfile) {
        self.mode = self.mode.toggled();
        if self.mode == PlayerCameraMode::ThirdPerson {
            self.boom_distance_m = self
                .boom_distance_m
                .max(profile.third_person_min_distance_m);
        }
    }

    pub(crate) fn view_rotation(self) -> Quat {
        Quat::from_rotation_y(self.yaw_radians) * Quat::from_rotation_x(self.pitch_radians)
    }

    pub(crate) fn horizontal_rotation(self) -> Quat {
        Quat::from_rotation_y(self.yaw_radians)
    }
}

fn wrap_angle_radians(angle: f64) -> f64 {
    (angle + std::f64::consts::PI).rem_euclid(std::f64::consts::TAU) - std::f64::consts::PI
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub(crate) struct PlayerCameraPose {
    pub(crate) translation: Vec3,
    pub(crate) forward: Vec3,
    pub(crate) pivot: Vec3,
}

pub(crate) fn third_person_pose(
    player_center: Vec3,
    capsule_half_height_m: f32,
    rig: PlayerCameraRig,
    profile: &PlayerCameraProfile,
) -> PlayerCameraPose {
    let foot = player_center - Vec3::Y * capsule_half_height_m;
    let pivot = foot + Vec3::Y * profile.pivot_height_m;
    let forward = rig.view_rotation() * Vec3::NEG_Z;
    PlayerCameraPose {
        translation: pivot - forward * rig.boom_distance_m,
        forward,
        pivot,
    }
}

pub(crate) fn first_person_pose(
    player_center: Vec3,
    capsule_half_height_m: f32,
    rig: PlayerCameraRig,
    profile: &PlayerCameraProfile,
) -> PlayerCameraPose {
    let foot = player_center - Vec3::Y * capsule_half_height_m;
    let eye = foot + Vec3::Y * profile.eye_height_m;
    PlayerCameraPose {
        translation: eye,
        forward: rig.view_rotation() * Vec3::NEG_Z,
        pivot: eye,
    }
}

/// Collision closes the boom immediately; an opening recovers at a bounded monotone rate.
pub(crate) fn resolve_boom_distance(
    current_m: f32,
    unobstructed_m: f32,
    collision_limit_m: Option<f32>,
    recovery_speed_mps: f32,
    delta_seconds: f32,
) -> f32 {
    let current_m = current_m.clamp(0.0, unobstructed_m);
    let target = collision_limit_m.map_or(unobstructed_m, |limit| limit.clamp(0.0, unobstructed_m));
    if target <= current_m {
        target
    } else {
        (current_m + recovery_speed_mps * delta_seconds.max(0.0)).min(target)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn profile() -> PlayerCameraProfile {
        PlayerCameraProfile {
            eye_height_m: 1.62,
            pivot_height_m: 1.5,
            third_person_distance_m: 4.5,
            third_person_min_distance_m: 0.6,
            collision_radius_m: 0.18,
            recovery_speed_mps: 8.0,
            pitch_min_degrees: -70.0,
            pitch_max_degrees: 70.0,
            third_person_vertical_fov_degrees: 60.0,
            first_person_vertical_fov_degrees: 75.0,
            look_sensitivity_degrees_per_unit: 0.12,
        }
    }

    #[test]
    fn pitch_is_clamped_and_yaw_remains_deterministic() {
        let profile = profile();
        let mut rig = PlayerCameraRig::new(&profile);
        rig.apply_look(Vec2::new(25.0, 10_000.0), &profile);

        assert_eq!(
            rig.pitch_radians.to_bits(),
            profile.pitch_min_degrees.to_radians().to_bits()
        );
        let expected_yaw = (-25.0 * profile.look_sensitivity_degrees_per_unit).to_radians();
        assert!((rig.yaw_radians - expected_yaw).abs() <= 1.0e-6);
    }

    #[test]
    fn initial_pitch_is_clamped_to_a_valid_narrow_profile() {
        let mut profile = profile();
        profile.pitch_min_degrees = -5.0;
        profile.pitch_max_degrees = 5.0;

        let rig = PlayerCameraRig::new(&profile);

        assert_eq!(
            rig.pitch_radians.to_bits(),
            profile.pitch_min_degrees.to_radians().to_bits()
        );
    }

    #[test]
    fn extreme_finite_look_commands_keep_camera_angles_finite_and_bounded() {
        let profile = profile();
        let mut rig = PlayerCameraRig::new(&profile);

        for look_delta in [
            Vec2::splat(f32::MAX),
            Vec2::splat(-f32::MAX),
            Vec2::new(f32::MAX, -f32::MAX),
        ] {
            rig.apply_look(look_delta, &profile);
            assert!(rig.yaw_radians.is_finite());
            assert!(rig.pitch_radians.is_finite());
            assert!(rig.yaw_radians >= -std::f32::consts::PI);
            assert!(rig.yaw_radians < std::f32::consts::PI);
            assert!(
                rig.pitch_radians >= profile.pitch_min_degrees.to_radians()
                    && rig.pitch_radians <= profile.pitch_max_degrees.to_radians()
            );
        }
    }

    #[test]
    fn camera_mode_toggles_without_replacing_identity() {
        assert_eq!(
            PlayerCameraMode::ThirdPerson.toggled(),
            PlayerCameraMode::FirstPerson
        );
        assert_eq!(
            PlayerCameraMode::FirstPerson.toggled(),
            PlayerCameraMode::ThirdPerson
        );
    }

    #[test]
    fn collision_reduces_immediately_and_clearance_recovers_monotonically() {
        let closed = resolve_boom_distance(4.5, 4.5, Some(1.4), 8.0, 1.0 / 60.0);
        assert_eq!(closed.to_bits(), 1.4_f32.to_bits());

        let first = resolve_boom_distance(closed, 4.5, None, 8.0, 1.0 / 60.0);
        let second = resolve_boom_distance(first, 4.5, None, 8.0, 1.0 / 60.0);
        assert!(first > closed && first < 4.5);
        assert!(second > first && second <= 4.5);
        assert!((first - closed - 8.0 / 60.0).abs() <= 1.0e-6);
    }

    #[test]
    fn returning_from_first_person_restores_at_least_the_minimum_boom() {
        let profile = profile();
        let mut rig = PlayerCameraRig::new(&profile);
        rig.mode = PlayerCameraMode::FirstPerson;
        rig.boom_distance_m = 0.0;
        rig.toggle_mode(&profile);
        let first = resolve_boom_distance(rig.boom_distance_m, 4.5, None, 8.0, 1.0 / 60.0);
        let second = resolve_boom_distance(first, 4.5, None, 8.0, 1.0 / 60.0);

        assert!(first >= profile.third_person_min_distance_m);
        assert!(second > first);
        assert!(second <= 4.5);
    }

    #[test]
    fn close_collision_is_allowed_inside_the_preferred_minimum() {
        let close = resolve_boom_distance(4.5, 4.5, Some(0.22), 8.0, 1.0 / 60.0);
        assert_eq!(close.to_bits(), 0.22_f32.to_bits());
    }

    #[test]
    fn first_and_third_person_poses_share_the_same_rig_orientation() {
        let profile = profile();
        let rig = PlayerCameraRig::new(&profile);
        let first = first_person_pose(Vec3::new(2.0, 3.0, 4.0), 0.9, rig, &profile);
        let third = third_person_pose(Vec3::new(2.0, 3.0, 4.0), 0.9, rig, &profile);

        assert!(first.forward.abs_diff_eq(third.forward, 1.0e-6));
        assert!(first.translation.y > 3.0);
        assert!((third.translation - third.pivot).length() > 4.49);
    }
}
