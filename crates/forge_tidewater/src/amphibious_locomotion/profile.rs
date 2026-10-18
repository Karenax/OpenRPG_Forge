//! Validated scene-independent motion parameters. Defaults retain the imported response.
#[derive(Clone, Copy, Debug)]
pub struct MotionProfile {
    pub walk_speed_mps: f32,
    pub sprint_speed_mps: f32,
    pub swim_speed_mps: f32,
    pub swim_sprint_speed_mps: f32,
    /// Positive downward acceleration magnitude; not a persistent project field.
    pub gravity_magnitude_mps2: f32,
    pub jump_impulse_mps: f32,
    pub ground_response: f32,
    pub air_response: f32,
    pub swim_response: f32,
    pub wade_depth_m: f32,
    pub wade_speed_factor: f32,
    pub water_drag: f32,
    pub swim_entry_depth_m: f32,
    pub stand_depth_m: f32,
    pub water_average_seconds: f32,
    pub ladder_radius_m: f32,
    pub ladder_speed_mps: f32,
    pub ladder_exit_margin_m: f32,
    pub eye_height_m: f32,
    pub swim_eye_height_m: f32,
}
impl Default for MotionProfile {
    fn default() -> Self {
        Self {
            walk_speed_mps: 3.0,
            sprint_speed_mps: 6.2,
            swim_speed_mps: 1.5,
            swim_sprint_speed_mps: 2.5,
            gravity_magnitude_mps2: 9.81,
            jump_impulse_mps: 4.6,
            ground_response: 14.0,
            air_response: 2.5,
            swim_response: 3.0,
            wade_depth_m: 1.2,
            wade_speed_factor: 0.42,
            water_drag: 0.8,
            swim_entry_depth_m: 1.35,
            stand_depth_m: 1.1,
            water_average_seconds: 4.0,
            ladder_radius_m: 1.1,
            ladder_speed_mps: 1.6,
            ladder_exit_margin_m: 1.0,
            eye_height_m: 1.62,
            swim_eye_height_m: 1.62 * 0.1,
        }
    }
}

/// Only validated values can be installed on a running controller.
#[derive(Clone, Copy, Debug, Default)]
pub struct ValidatedMotionProfile(pub(super) MotionProfile);

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct InvalidMotionProfile {
    pub field: &'static str,
}
impl std::fmt::Display for InvalidMotionProfile {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "Invalid motion profile: {}", self.field)
    }
}
impl std::error::Error for InvalidMotionProfile {}

impl MotionProfile {
    pub fn validate(self) -> Result<ValidatedMotionProfile, InvalidMotionProfile> {
        for (field, value) in [
            ("walk_speed_mps", self.walk_speed_mps),
            ("sprint_speed_mps", self.sprint_speed_mps),
            ("swim_speed_mps", self.swim_speed_mps),
            ("swim_sprint_speed_mps", self.swim_sprint_speed_mps),
            ("gravity_magnitude_mps2", self.gravity_magnitude_mps2),
            ("jump_impulse_mps", self.jump_impulse_mps),
            ("ground_response", self.ground_response),
            ("air_response", self.air_response),
            ("swim_response", self.swim_response),
            ("wade_depth_m", self.wade_depth_m),
            ("wade_speed_factor", self.wade_speed_factor),
            ("water_drag", self.water_drag),
            ("swim_entry_depth_m", self.swim_entry_depth_m),
            ("stand_depth_m", self.stand_depth_m),
            ("water_average_seconds", self.water_average_seconds),
            ("ladder_radius_m", self.ladder_radius_m),
            ("ladder_speed_mps", self.ladder_speed_mps),
            ("ladder_exit_margin_m", self.ladder_exit_margin_m),
            ("eye_height_m", self.eye_height_m),
            ("swim_eye_height_m", self.swim_eye_height_m),
        ] {
            if !value.is_finite() || value <= 0.0 {
                return Err(InvalidMotionProfile { field });
            }
        }
        if self.swim_entry_depth_m <= self.stand_depth_m || self.wade_speed_factor > 1.0 {
            return Err(InvalidMotionProfile {
                field: "immersion hysteresis or wading factor",
            });
        }
        Ok(ValidatedMotionProfile(self))
    }
}
