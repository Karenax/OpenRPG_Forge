use forge_core::{CanonicalJsonError, ContentHash, canonical_json_sha256};
use serde::{Deserialize, Serialize};
use thiserror::Error;

pub const PLAYER_CONTROLLER_PROFILE_SCHEMA: &str = "forge.player-controller-profile";
pub const PLAYER_CONTROLLER_PROFILE_SCHEMA_VERSION: u32 = 1;

const MAX_NAME_LENGTH: usize = 128;
const MAX_CAPSULE_DIMENSION_M: f32 = 5.0;
const MAX_LINEAR_SPEED_MPS: f32 = 100.0;
const MAX_ACCELERATION_MPS2: f32 = 500.0;
const MAX_SPRINT_MULTIPLIER: f32 = 4.0;
const MAX_CAMERA_RECOVERY_SPEED_MPS: f32 = 100.0;
const MAX_LOOK_SENSITIVITY_DEGREES_PER_UNIT: f32 = 10.0;

/// Versioned, renderer- and physics-engine-neutral player locomotion and camera profile.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PlayerControllerProfileDocument {
    pub schema: String,
    pub schema_version: u32,
    pub name: String,
    pub controller: PlayerControllerProfile,
    pub camera: PlayerCameraProfile,
}

impl PlayerControllerProfileDocument {
    #[must_use]
    pub fn new(
        name: impl Into<String>,
        controller: PlayerControllerProfile,
        camera: PlayerCameraProfile,
    ) -> Self {
        Self {
            schema: PLAYER_CONTROLLER_PROFILE_SCHEMA.to_owned(),
            schema_version: PLAYER_CONTROLLER_PROFILE_SCHEMA_VERSION,
            name: name.into(),
            controller,
            camera,
        }
    }

    pub fn validate(&self) -> Result<(), PlayerControllerProfileError> {
        if self.schema != PLAYER_CONTROLLER_PROFILE_SCHEMA {
            return Err(PlayerControllerProfileError::InvalidSchema(
                self.schema.clone(),
            ));
        }
        if self.schema_version != PLAYER_CONTROLLER_PROFILE_SCHEMA_VERSION {
            return Err(PlayerControllerProfileError::UnsupportedVersion(
                self.schema_version,
            ));
        }

        let trimmed_name = self.name.trim();
        if trimmed_name.is_empty() {
            return Err(PlayerControllerProfileError::EmptyName);
        }
        if trimmed_name.len() > MAX_NAME_LENGTH {
            return Err(PlayerControllerProfileError::NameTooLong(
                trimmed_name.len(),
            ));
        }

        self.controller.validate()?;
        self.camera
            .validate(self.controller.total_capsule_height_m())?;
        Ok(())
    }

    pub fn from_json(json: &str) -> Result<Self, PlayerControllerProfileError> {
        let document: Self = serde_json::from_str(json)?;
        document.validate()?;
        Ok(document)
    }

    pub fn to_pretty_json(&self) -> Result<String, PlayerControllerProfileError> {
        self.validate()?;
        Ok(serde_json::to_string_pretty(self)?)
    }

    pub fn content_hash(&self) -> Result<ContentHash, PlayerControllerProfileError> {
        self.validate()?;
        Ok(canonical_json_sha256(self)?)
    }
}

/// Kinematic intent expressed in metres, seconds and degrees.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PlayerControllerProfile {
    pub capsule_radius_m: f32,
    /// Half the straight segment between capsule hemisphere centres, not half the total height.
    pub capsule_half_segment_m: f32,
    pub walk_speed_mps: f32,
    pub sprint_multiplier: f32,
    pub acceleration_mps2: f32,
    pub braking_mps2: f32,
    pub gravity_mps2: f32,
    pub jump_speed_mps: f32,
    pub max_step_height_m: f32,
    pub min_step_width_m: f32,
    pub ground_snap_m: f32,
    pub max_slope_climb_degrees: f32,
    pub min_slope_slide_degrees: f32,
}

impl PlayerControllerProfile {
    #[must_use]
    pub fn total_capsule_height_m(&self) -> f32 {
        2.0 * (self.capsule_radius_m + self.capsule_half_segment_m)
    }

    fn validate(&self) -> Result<(), PlayerControllerProfileError> {
        positive_bounded(
            self.capsule_radius_m,
            MAX_CAPSULE_DIMENSION_M,
            "capsule_radius_m",
        )?;
        positive_bounded(
            self.capsule_half_segment_m,
            MAX_CAPSULE_DIMENSION_M,
            "capsule_half_segment_m",
        )?;
        positive_bounded(self.walk_speed_mps, MAX_LINEAR_SPEED_MPS, "walk_speed_mps")?;
        bounded(
            self.sprint_multiplier,
            1.0,
            MAX_SPRINT_MULTIPLIER,
            "sprint_multiplier",
        )?;
        positive_bounded(
            self.acceleration_mps2,
            MAX_ACCELERATION_MPS2,
            "acceleration_mps2",
        )?;
        positive_bounded(self.braking_mps2, MAX_ACCELERATION_MPS2, "braking_mps2")?;
        if !self.gravity_mps2.is_finite()
            || self.gravity_mps2 >= 0.0
            || self.gravity_mps2 < -MAX_ACCELERATION_MPS2
        {
            return Err(PlayerControllerProfileError::InvalidControllerValue(
                "gravity_mps2",
            ));
        }
        positive_bounded(self.jump_speed_mps, MAX_LINEAR_SPEED_MPS, "jump_speed_mps")?;
        positive_bounded(
            self.max_step_height_m,
            MAX_CAPSULE_DIMENSION_M,
            "max_step_height_m",
        )?;
        positive_bounded(
            self.min_step_width_m,
            MAX_CAPSULE_DIMENSION_M,
            "min_step_width_m",
        )?;
        positive_bounded(self.ground_snap_m, MAX_CAPSULE_DIMENSION_M, "ground_snap_m")?;
        bounded(
            self.max_slope_climb_degrees,
            f32::EPSILON,
            89.0,
            "max_slope_climb_degrees",
        )?;
        bounded(
            self.min_slope_slide_degrees,
            f32::EPSILON,
            89.0,
            "min_slope_slide_degrees",
        )?;

        let total_height = self.total_capsule_height_m();
        if !total_height.is_finite() || self.max_step_height_m >= total_height {
            return Err(PlayerControllerProfileError::InvalidControllerRelation(
                "max_step_height_m must be lower than total capsule height",
            ));
        }
        if self.min_step_width_m < self.capsule_radius_m {
            return Err(PlayerControllerProfileError::InvalidControllerRelation(
                "min_step_width_m must be at least capsule_radius_m",
            ));
        }
        if self.ground_snap_m > self.max_step_height_m {
            return Err(PlayerControllerProfileError::InvalidControllerRelation(
                "ground_snap_m cannot exceed max_step_height_m",
            ));
        }
        if self.min_slope_slide_degrees < self.max_slope_climb_degrees {
            return Err(PlayerControllerProfileError::InvalidControllerRelation(
                "min_slope_slide_degrees cannot be lower than max_slope_climb_degrees",
            ));
        }
        Ok(())
    }
}

/// Camera intent independent from an input framework, renderer or physics query implementation.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PlayerCameraProfile {
    pub eye_height_m: f32,
    pub pivot_height_m: f32,
    pub third_person_distance_m: f32,
    pub third_person_min_distance_m: f32,
    pub collision_radius_m: f32,
    pub recovery_speed_mps: f32,
    pub pitch_min_degrees: f32,
    pub pitch_max_degrees: f32,
    pub third_person_vertical_fov_degrees: f32,
    pub first_person_vertical_fov_degrees: f32,
    pub look_sensitivity_degrees_per_unit: f32,
}

impl PlayerCameraProfile {
    fn validate(&self, total_capsule_height_m: f32) -> Result<(), PlayerControllerProfileError> {
        positive_bounded(self.eye_height_m, total_capsule_height_m, "eye_height_m")?;
        positive_bounded(
            self.pivot_height_m,
            total_capsule_height_m,
            "pivot_height_m",
        )?;
        positive_bounded(
            self.third_person_distance_m,
            100.0,
            "third_person_distance_m",
        )?;
        positive_bounded(
            self.third_person_min_distance_m,
            100.0,
            "third_person_min_distance_m",
        )?;
        positive_bounded(self.collision_radius_m, 5.0, "collision_radius_m")?;
        positive_bounded(
            self.recovery_speed_mps,
            MAX_CAMERA_RECOVERY_SPEED_MPS,
            "recovery_speed_mps",
        )?;
        if !self.pitch_min_degrees.is_finite()
            || self.pitch_min_degrees <= -89.0
            || self.pitch_min_degrees >= 0.0
        {
            return Err(PlayerControllerProfileError::InvalidCameraValue(
                "pitch_min_degrees",
            ));
        }
        if !self.pitch_max_degrees.is_finite()
            || self.pitch_max_degrees <= 0.0
            || self.pitch_max_degrees >= 89.0
        {
            return Err(PlayerControllerProfileError::InvalidCameraValue(
                "pitch_max_degrees",
            ));
        }
        exclusive_bounded(
            self.third_person_vertical_fov_degrees,
            1.0,
            179.0,
            "third_person_vertical_fov_degrees",
        )?;
        exclusive_bounded(
            self.first_person_vertical_fov_degrees,
            1.0,
            179.0,
            "first_person_vertical_fov_degrees",
        )?;
        positive_bounded(
            self.look_sensitivity_degrees_per_unit,
            MAX_LOOK_SENSITIVITY_DEGREES_PER_UNIT,
            "look_sensitivity_degrees_per_unit",
        )?;

        if self.pivot_height_m > self.eye_height_m {
            return Err(PlayerControllerProfileError::InvalidCameraRelation(
                "pivot_height_m cannot exceed eye_height_m",
            ));
        }
        if self.third_person_min_distance_m >= self.third_person_distance_m {
            return Err(PlayerControllerProfileError::InvalidCameraRelation(
                "third_person_min_distance_m must be lower than third_person_distance_m",
            ));
        }
        if self.collision_radius_m >= self.third_person_min_distance_m {
            return Err(PlayerControllerProfileError::InvalidCameraRelation(
                "collision_radius_m must be lower than third_person_min_distance_m",
            ));
        }
        Ok(())
    }
}

fn positive_bounded(
    value: f32,
    maximum: f32,
    field: &'static str,
) -> Result<(), PlayerControllerProfileError> {
    if !value.is_finite() || value <= 0.0 || value > maximum {
        return Err(PlayerControllerProfileError::InvalidNumericValue(field));
    }
    Ok(())
}

fn bounded(
    value: f32,
    minimum: f32,
    maximum: f32,
    field: &'static str,
) -> Result<(), PlayerControllerProfileError> {
    if !value.is_finite() || value < minimum || value > maximum {
        return Err(PlayerControllerProfileError::InvalidNumericValue(field));
    }
    Ok(())
}

fn exclusive_bounded(
    value: f32,
    minimum: f32,
    maximum: f32,
    field: &'static str,
) -> Result<(), PlayerControllerProfileError> {
    if !value.is_finite() || value <= minimum || value >= maximum {
        return Err(PlayerControllerProfileError::InvalidNumericValue(field));
    }
    Ok(())
}

#[derive(Debug, Error)]
pub enum PlayerControllerProfileError {
    #[error("invalid player-controller profile schema `{0}`")]
    InvalidSchema(String),
    #[error("unsupported player-controller profile schema version {0}")]
    UnsupportedVersion(u32),
    #[error("player-controller profile name cannot be empty")]
    EmptyName,
    #[error("player-controller profile name is too long: {0} bytes")]
    NameTooLong(usize),
    #[error("player-controller profile has an invalid numeric value for `{0}`")]
    InvalidNumericValue(&'static str),
    #[error("player-controller profile has an invalid controller value for `{0}`")]
    InvalidControllerValue(&'static str),
    #[error("player-controller profile violates controller relation: {0}")]
    InvalidControllerRelation(&'static str),
    #[error("player-controller profile has an invalid camera value for `{0}`")]
    InvalidCameraValue(&'static str),
    #[error("player-controller profile violates camera relation: {0}")]
    InvalidCameraRelation(&'static str),
    #[error("invalid player-controller profile JSON: {0}")]
    Json(#[from] serde_json::Error),
    #[error("cannot hash player-controller profile: {0}")]
    Hash(#[from] CanonicalJsonError),
}

#[cfg(test)]
mod tests {
    use super::*;

    fn valid_document() -> PlayerControllerProfileDocument {
        PlayerControllerProfileDocument::new(
            "Test player controller",
            PlayerControllerProfile {
                capsule_radius_m: 0.35,
                capsule_half_segment_m: 0.55,
                walk_speed_mps: 5.0,
                sprint_multiplier: 1.6,
                acceleration_mps2: 24.0,
                braking_mps2: 32.0,
                gravity_mps2: -24.0,
                jump_speed_mps: 8.0,
                max_step_height_m: 0.4,
                min_step_width_m: 0.35,
                ground_snap_m: 0.25,
                max_slope_climb_degrees: 45.0,
                min_slope_slide_degrees: 50.0,
            },
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
            },
        )
    }

    #[test]
    fn versioned_json_round_trip_and_hash_are_deterministic() {
        let document = valid_document();
        let json = document.to_pretty_json().unwrap();
        let restored = PlayerControllerProfileDocument::from_json(&json).unwrap();

        assert_eq!(restored, document);
        assert_eq!(restored.to_pretty_json().unwrap(), json);
        assert_eq!(
            restored.content_hash().unwrap(),
            document.content_hash().unwrap()
        );
        assert_eq!(
            document.controller.total_capsule_height_m().to_bits(),
            1.8_f32.to_bits()
        );
    }

    #[test]
    fn unknown_fields_and_non_finite_json_are_rejected() {
        let json = valid_document().to_pretty_json().unwrap();
        let unknown = json.replacen(
            "\"capsule_radius_m\": 0.35,",
            "\"capsule_radius_m\": 0.35,\n    \"engine_handle\": 7,",
            1,
        );
        assert!(matches!(
            PlayerControllerProfileDocument::from_json(&unknown),
            Err(PlayerControllerProfileError::Json(_))
        ));

        let non_finite = json.replacen("\"walk_speed_mps\": 5.0", "\"walk_speed_mps\": NaN", 1);
        assert!(matches!(
            PlayerControllerProfileDocument::from_json(&non_finite),
            Err(PlayerControllerProfileError::Json(_))
        ));
    }

    #[test]
    fn schema_and_version_are_strict() {
        let mut document = valid_document();
        document.schema = "forge.player-controller-profile.test".to_owned();
        assert!(matches!(
            document.validate(),
            Err(PlayerControllerProfileError::InvalidSchema(_))
        ));

        document = valid_document();
        document.schema_version = PLAYER_CONTROLLER_PROFILE_SCHEMA_VERSION + 1;
        assert!(matches!(
            document.validate(),
            Err(PlayerControllerProfileError::UnsupportedVersion(2))
        ));
    }

    #[test]
    fn non_positive_non_finite_and_out_of_range_values_are_rejected() {
        let mut document = valid_document();
        document.controller.capsule_radius_m = 0.0;
        assert!(document.validate().is_err());

        document = valid_document();
        document.controller.acceleration_mps2 = f32::NAN;
        assert!(document.validate().is_err());

        document = valid_document();
        document.controller.walk_speed_mps = MAX_LINEAR_SPEED_MPS + 1.0;
        assert!(document.validate().is_err());

        document = valid_document();
        document.controller.gravity_mps2 = 0.0;
        assert!(document.validate().is_err());

        document = valid_document();
        document.camera.third_person_vertical_fov_degrees = 179.0;
        assert!(document.validate().is_err());

        document = valid_document();
        document.camera.look_sensitivity_degrees_per_unit =
            MAX_LOOK_SENSITIVITY_DEGREES_PER_UNIT + 1.0;
        assert!(document.validate().is_err());
    }

    #[test]
    fn invalid_geometry_angle_and_distance_relations_are_rejected() {
        let mut document = valid_document();
        document.controller.min_step_width_m = document.controller.capsule_radius_m / 2.0;
        assert!(matches!(
            document.validate(),
            Err(PlayerControllerProfileError::InvalidControllerRelation(_))
        ));

        document = valid_document();
        document.controller.min_slope_slide_degrees =
            document.controller.max_slope_climb_degrees - 1.0;
        assert!(matches!(
            document.validate(),
            Err(PlayerControllerProfileError::InvalidControllerRelation(_))
        ));

        document = valid_document();
        document.camera.pivot_height_m = document.camera.eye_height_m + 0.1;
        assert!(matches!(
            document.validate(),
            Err(PlayerControllerProfileError::InvalidCameraRelation(_))
        ));

        document = valid_document();
        document.camera.third_person_min_distance_m = document.camera.third_person_distance_m;
        assert!(matches!(
            document.validate(),
            Err(PlayerControllerProfileError::InvalidCameraRelation(_))
        ));

        document = valid_document();
        document.camera.pitch_min_degrees = 0.0;
        assert!(matches!(
            document.validate(),
            Err(PlayerControllerProfileError::InvalidCameraValue(
                "pitch_min_degrees"
            ))
        ));
    }
}
