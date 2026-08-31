use crate::SrgbColor;
use forge_core::{
    AreaId, CanonicalJsonError, ContentHash, CoordinateSystem, ProjectId, Revision, Vec3,
    canonical_json_sha256,
};
use serde::{Deserialize, Serialize};
use thiserror::Error;

pub const WEATHER_PROFILE_SCHEMA: &str = "forge.weather-profile";
pub const WEATHER_PROFILE_SCHEMA_VERSION: u32 = 1;
pub const MAX_WEATHER_PROFILE_JSON_BYTES: usize = 64 * 1024;

const MAX_NAME_LENGTH: usize = 128;
const MAX_RAIN_AXIS_M: f32 = 1_024.0;
const MAX_RAIN_DROPS: u32 = 262_144;

/// Renderer-independent clear/rain proof profile for one area revision.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WeatherProfileDocument {
    pub schema: String,
    pub schema_version: u32,
    pub project_id: ProjectId,
    pub area_id: AreaId,
    pub revision: Revision,
    pub coordinate_system: CoordinateSystem,
    pub name: String,
    pub seed: u64,
    pub initial_state: WeatherState,
    pub rain: RainProfile,
    pub wetness: WetnessProfile,
    pub post_processing: PostProcessingProfile,
}

impl WeatherProfileDocument {
    #[must_use]
    #[allow(clippy::too_many_arguments)]
    pub fn new(
        project_id: ProjectId,
        area_id: AreaId,
        revision: Revision,
        name: impl Into<String>,
        seed: u64,
        initial_state: WeatherState,
        rain: RainProfile,
        wetness: WetnessProfile,
        post_processing: PostProcessingProfile,
    ) -> Self {
        Self {
            schema: WEATHER_PROFILE_SCHEMA.to_owned(),
            schema_version: WEATHER_PROFILE_SCHEMA_VERSION,
            project_id,
            area_id,
            revision,
            coordinate_system: CoordinateSystem::METRES_Y_UP_RIGHT_HANDED,
            name: name.into(),
            seed,
            initial_state,
            rain,
            wetness,
            post_processing,
        }
    }

    pub fn validate(&self) -> Result<(), WeatherProfileError> {
        if self.schema != WEATHER_PROFILE_SCHEMA {
            return Err(WeatherProfileError::InvalidSchema(self.schema.clone()));
        }
        if self.schema_version != WEATHER_PROFILE_SCHEMA_VERSION {
            return Err(WeatherProfileError::UnsupportedVersion(self.schema_version));
        }
        if self.revision.get() == 0 {
            return Err(WeatherProfileError::InvalidRevision);
        }
        if self.coordinate_system != CoordinateSystem::METRES_Y_UP_RIGHT_HANDED {
            return Err(WeatherProfileError::UnsupportedCoordinateSystem);
        }
        if self.name.trim().is_empty() {
            return Err(WeatherProfileError::EmptyName);
        }
        if self.name.len() > MAX_NAME_LENGTH {
            return Err(WeatherProfileError::NameTooLong(self.name.len()));
        }

        self.rain.validate()?;
        self.wetness.validate()?;
        self.post_processing.validate()?;
        Ok(())
    }

    pub fn validate_identity(
        &self,
        project_id: ProjectId,
        area_id: AreaId,
        revision: Revision,
    ) -> Result<(), WeatherProfileError> {
        if self.project_id != project_id {
            return Err(WeatherProfileError::ProjectIdentityMismatch {
                expected: project_id,
                actual: self.project_id,
            });
        }
        if self.area_id != area_id {
            return Err(WeatherProfileError::AreaIdentityMismatch {
                expected: area_id,
                actual: self.area_id,
            });
        }
        if self.revision != revision {
            return Err(WeatherProfileError::RevisionMismatch {
                expected: revision,
                actual: self.revision,
            });
        }
        Ok(())
    }

    pub fn from_json(json: &str) -> Result<Self, WeatherProfileError> {
        if json.len() > MAX_WEATHER_PROFILE_JSON_BYTES {
            return Err(WeatherProfileError::JsonTooLarge {
                actual: json.len(),
                maximum: MAX_WEATHER_PROFILE_JSON_BYTES,
            });
        }
        let document: Self = serde_json::from_str(json)?;
        document.validate()?;
        Ok(document)
    }

    pub fn to_pretty_json(&self) -> Result<String, WeatherProfileError> {
        self.validate()?;
        Ok(serde_json::to_string_pretty(self)?)
    }

    pub fn content_hash(&self) -> Result<ContentHash, WeatherProfileError> {
        self.validate()?;
        Ok(canonical_json_sha256(self)?)
    }

    /// Advances only the canonical scalar wetness. Rendering remains an adapter concern.
    pub fn advance_wetness(
        &self,
        current: f32,
        target_state: WeatherState,
        delta_seconds: f32,
    ) -> Result<f32, WeatherProfileError> {
        self.validate()?;
        if !current.is_finite()
            || !(0.0..=1.0).contains(&current)
            || !delta_seconds.is_finite()
            || delta_seconds < 0.0
        {
            return Err(WeatherProfileError::InvalidTransitionInput);
        }
        let target = self.wetness.target_for(target_state);
        let rate = if current < target {
            self.wetness.wetting_rate_per_second
        } else {
            self.wetness.drying_rate_per_second
        };
        let step = rate * delta_seconds;
        Ok(if current < target {
            (current + step).min(target)
        } else {
            (current - step).max(target)
        })
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum WeatherState {
    Clear,
    Rain,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum WeatherQualityTier {
    Low,
    Medium,
    High,
    Ultra,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct RainDropBudgets {
    pub low: u32,
    pub medium: u32,
    pub high: u32,
    pub ultra: u32,
}

impl RainDropBudgets {
    fn validate(self) -> Result<(), WeatherProfileError> {
        if self.low == 0
            || self.low > self.medium
            || self.medium > self.high
            || self.high > self.ultra
            || self.ultra > MAX_RAIN_DROPS
        {
            return Err(WeatherProfileError::InvalidRainDropBudgets);
        }
        Ok(())
    }

    #[must_use]
    pub const fn for_quality(self, quality: WeatherQualityTier) -> u32 {
        match quality {
            WeatherQualityTier::Low => self.low,
            WeatherQualityTier::Medium => self.medium,
            WeatherQualityTier::High => self.high,
            WeatherQualityTier::Ultra => self.ultra,
        }
    }
}

/// Procedural rain intent. The counts are batch budgets, never ECS entity counts.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct RainProfile {
    pub quality: WeatherQualityTier,
    pub drop_budgets: RainDropBudgets,
    pub volume_size_m: Vec3,
    pub fall_speed_mps: f32,
    pub streak_length_m: f32,
    pub wind_velocity_mps: Vec3,
    pub color_srgb: SrgbColor,
    pub opacity: f32,
}

impl RainProfile {
    fn validate(self) -> Result<(), WeatherProfileError> {
        self.drop_budgets.validate()?;
        if !self.volume_size_m.is_finite()
            || self.volume_size_m.x <= 0.0
            || self.volume_size_m.y <= 0.0
            || self.volume_size_m.z <= 0.0
            || self.volume_size_m.x > MAX_RAIN_AXIS_M
            || self.volume_size_m.y > MAX_RAIN_AXIS_M
            || self.volume_size_m.z > MAX_RAIN_AXIS_M
        {
            return Err(WeatherProfileError::InvalidRainVolume);
        }
        positive_bounded(self.fall_speed_mps, 128.0, "rain.fall_speed_mps")?;
        positive_bounded(self.streak_length_m, 8.0, "rain.streak_length_m")?;
        if !self.wind_velocity_mps.is_finite()
            || self.wind_velocity_mps.x.abs() > 64.0
            || self.wind_velocity_mps.y.abs() > 64.0
            || self.wind_velocity_mps.z.abs() > 64.0
        {
            return Err(WeatherProfileError::InvalidNumericValue(
                "rain.wind_velocity_mps",
            ));
        }
        validate_color(self.color_srgb, "rain.color_srgb")?;
        exclusive_bounded(self.opacity, 0.0, 1.0, "rain.opacity")
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WetnessProfile {
    pub clear_target: f32,
    pub rain_target: f32,
    pub wetting_rate_per_second: f32,
    pub drying_rate_per_second: f32,
    pub full_wet_roughness_multiplier: f32,
    pub full_wet_albedo_multiplier: f32,
}

impl WetnessProfile {
    fn validate(self) -> Result<(), WeatherProfileError> {
        bounded(self.clear_target, 0.0, 1.0, "wetness.clear_target")?;
        bounded(self.rain_target, 0.0, 1.0, "wetness.rain_target")?;
        if self.rain_target <= self.clear_target {
            return Err(WeatherProfileError::InvalidProfileRelation(
                "rain wetness target must exceed the clear target",
            ));
        }
        positive_bounded(
            self.wetting_rate_per_second,
            10.0,
            "wetness.wetting_rate_per_second",
        )?;
        positive_bounded(
            self.drying_rate_per_second,
            10.0,
            "wetness.drying_rate_per_second",
        )?;
        exclusive_bounded(
            self.full_wet_roughness_multiplier,
            0.0,
            1.0,
            "wetness.full_wet_roughness_multiplier",
        )?;
        exclusive_bounded(
            self.full_wet_albedo_multiplier,
            0.0,
            1.0,
            "wetness.full_wet_albedo_multiplier",
        )
    }

    #[must_use]
    pub const fn target_for(self, state: WeatherState) -> f32 {
        match state {
            WeatherState::Clear => self.clear_target,
            WeatherState::Rain => self.rain_target,
        }
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct BloomProfile {
    pub enabled: bool,
    pub intensity: f32,
    pub low_frequency_boost: f32,
    pub high_pass_frequency: f32,
}

impl BloomProfile {
    fn validate(self) -> Result<(), WeatherProfileError> {
        bounded(self.intensity, 0.0, 1.0, "post_processing.bloom.intensity")?;
        bounded(
            self.low_frequency_boost,
            0.0,
            1.0,
            "post_processing.bloom.low_frequency_boost",
        )?;
        bounded(
            self.high_pass_frequency,
            0.0,
            1.0,
            "post_processing.bloom.high_pass_frequency",
        )?;
        if self.enabled && self.intensity <= 0.0 {
            return Err(WeatherProfileError::InvalidProfileRelation(
                "enabled bloom requires positive intensity",
            ));
        }
        Ok(())
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ColorGradeProfile {
    pub exposure_ev: f32,
    pub temperature: f32,
    pub tint: f32,
    pub saturation: f32,
    pub contrast: f32,
    pub gamma: f32,
}

impl ColorGradeProfile {
    fn validate(self, prefix: &'static str) -> Result<(), WeatherProfileError> {
        if !self.exposure_ev.is_finite()
            || !(-8.0..=8.0).contains(&self.exposure_ev)
            || !self.temperature.is_finite()
            || !(-1.0..=1.0).contains(&self.temperature)
            || !self.tint.is_finite()
            || !(-1.0..=1.0).contains(&self.tint)
            || !self.saturation.is_finite()
            || !(0.0..=2.0).contains(&self.saturation)
            || !self.contrast.is_finite()
            || !(0.0..=2.0).contains(&self.contrast)
            || !self.gamma.is_finite()
            || !(0.1..=4.0).contains(&self.gamma)
        {
            return Err(WeatherProfileError::InvalidColorGrade(prefix));
        }
        Ok(())
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PostProcessingProfile {
    pub bloom: BloomProfile,
    pub clear_grade: ColorGradeProfile,
    pub rain_grade: ColorGradeProfile,
}

impl PostProcessingProfile {
    fn validate(self) -> Result<(), WeatherProfileError> {
        self.bloom.validate()?;
        self.clear_grade.validate("post_processing.clear_grade")?;
        self.rain_grade.validate("post_processing.rain_grade")
    }

    #[must_use]
    pub const fn grade_for(self, state: WeatherState) -> ColorGradeProfile {
        match state {
            WeatherState::Clear => self.clear_grade,
            WeatherState::Rain => self.rain_grade,
        }
    }
}

fn validate_color(color: SrgbColor, field: &'static str) -> Result<(), WeatherProfileError> {
    if [color.red, color.green, color.blue]
        .into_iter()
        .all(|component| component.is_finite() && (0.0..=1.0).contains(&component))
    {
        Ok(())
    } else {
        Err(WeatherProfileError::InvalidNumericValue(field))
    }
}

fn positive_bounded(
    value: f32,
    maximum: f32,
    field: &'static str,
) -> Result<(), WeatherProfileError> {
    if !value.is_finite() || value <= 0.0 || value > maximum {
        return Err(WeatherProfileError::InvalidNumericValue(field));
    }
    Ok(())
}

fn bounded(
    value: f32,
    minimum: f32,
    maximum: f32,
    field: &'static str,
) -> Result<(), WeatherProfileError> {
    if !value.is_finite() || !(minimum..=maximum).contains(&value) {
        return Err(WeatherProfileError::InvalidNumericValue(field));
    }
    Ok(())
}

fn exclusive_bounded(
    value: f32,
    minimum: f32,
    maximum: f32,
    field: &'static str,
) -> Result<(), WeatherProfileError> {
    if !value.is_finite() || value <= minimum || value >= maximum {
        return Err(WeatherProfileError::InvalidNumericValue(field));
    }
    Ok(())
}

#[derive(Debug, Error)]
pub enum WeatherProfileError {
    #[error("invalid weather profile schema `{0}`")]
    InvalidSchema(String),
    #[error("unsupported weather profile schema version {0}")]
    UnsupportedVersion(u32),
    #[error("weather profile revision must be greater than zero")]
    InvalidRevision,
    #[error("weather profile coordinate system must be metres, Y-up, right-handed")]
    UnsupportedCoordinateSystem,
    #[error("weather profile name cannot be empty")]
    EmptyName,
    #[error("weather profile name is too long: {0} bytes")]
    NameTooLong(usize),
    #[error("weather profile JSON is too large: {actual} bytes, maximum {maximum}")]
    JsonTooLarge { actual: usize, maximum: usize },
    #[error("weather profile has an invalid numeric value for `{0}`")]
    InvalidNumericValue(&'static str),
    #[error("weather rain drop budgets must be positive, monotone and bounded")]
    InvalidRainDropBudgets,
    #[error("weather rain volume must be finite, positive and bounded")]
    InvalidRainVolume,
    #[error("weather color grade `{0}` is outside its bounded range")]
    InvalidColorGrade(&'static str),
    #[error("weather profile violates relation: {0}")]
    InvalidProfileRelation(&'static str),
    #[error("weather transition input must be finite and bounded")]
    InvalidTransitionInput,
    #[error("weather project identity mismatch: expected {expected}, got {actual}")]
    ProjectIdentityMismatch {
        expected: ProjectId,
        actual: ProjectId,
    },
    #[error("weather area identity mismatch: expected {expected}, got {actual}")]
    AreaIdentityMismatch { expected: AreaId, actual: AreaId },
    #[error("weather revision mismatch: expected {expected:?}, got {actual:?}")]
    RevisionMismatch {
        expected: Revision,
        actual: Revision,
    },
    #[error("invalid weather profile JSON: {0}")]
    Json(#[from] serde_json::Error),
    #[error("cannot hash weather profile: {0}")]
    Hash(#[from] CanonicalJsonError),
}

#[cfg(test)]
mod tests {
    use super::*;

    fn valid_document() -> WeatherProfileDocument {
        WeatherProfileDocument::new(
            ProjectId::deterministic("weather-test-project"),
            AreaId::deterministic("weather-test-area"),
            Revision::INITIAL,
            "Test weather",
            17,
            WeatherState::Clear,
            RainProfile {
                quality: WeatherQualityTier::High,
                drop_budgets: RainDropBudgets {
                    low: 512,
                    medium: 1_024,
                    high: 2_048,
                    ultra: 4_096,
                },
                volume_size_m: Vec3::new(96.0, 48.0, 96.0),
                fall_speed_mps: 18.0,
                streak_length_m: 0.65,
                wind_velocity_mps: Vec3::new(-2.0, 0.0, 1.0),
                color_srgb: SrgbColor::new(0.68, 0.78, 0.88),
                opacity: 0.42,
            },
            WetnessProfile {
                clear_target: 0.0,
                rain_target: 0.78,
                wetting_rate_per_second: 0.18,
                drying_rate_per_second: 0.07,
                full_wet_roughness_multiplier: 0.32,
                full_wet_albedo_multiplier: 0.72,
            },
            PostProcessingProfile {
                bloom: BloomProfile {
                    enabled: true,
                    intensity: 0.08,
                    low_frequency_boost: 0.35,
                    high_pass_frequency: 0.82,
                },
                clear_grade: ColorGradeProfile {
                    exposure_ev: 0.0,
                    temperature: 0.08,
                    tint: 0.01,
                    saturation: 1.04,
                    contrast: 1.06,
                    gamma: 1.0,
                },
                rain_grade: ColorGradeProfile {
                    exposure_ev: -0.35,
                    temperature: -0.12,
                    tint: 0.02,
                    saturation: 0.86,
                    contrast: 1.08,
                    gamma: 1.0,
                },
            },
        )
    }

    #[test]
    fn versioned_json_round_trip_and_hash_are_deterministic() {
        let document = valid_document();
        let json = document.to_pretty_json().unwrap();
        let restored = WeatherProfileDocument::from_json(&json).unwrap();
        assert_eq!(restored, document);
        assert_eq!(restored.to_pretty_json().unwrap(), json);
        assert_eq!(
            restored.content_hash().unwrap(),
            document.content_hash().unwrap()
        );
    }

    #[test]
    fn clear_rain_clear_transition_is_bounded_and_reversible() {
        let document = valid_document();
        let wet = document
            .advance_wetness(0.0, WeatherState::Rain, 100.0)
            .unwrap();
        assert_eq!(wet.to_bits(), document.wetness.rain_target.to_bits());
        let dry = document
            .advance_wetness(wet, WeatherState::Clear, 100.0)
            .unwrap();
        assert_eq!(dry.to_bits(), document.wetness.clear_target.to_bits());
        assert!(
            document
                .advance_wetness(f32::NAN, WeatherState::Rain, 1.0)
                .is_err()
        );
        assert!(
            document
                .advance_wetness(0.0, WeatherState::Rain, -1.0)
                .is_err()
        );
    }

    #[test]
    fn budgets_and_numeric_relations_are_strict() {
        let mut document = valid_document();
        document.rain.drop_budgets.medium = document.rain.drop_budgets.low - 1;
        assert!(matches!(
            document.validate(),
            Err(WeatherProfileError::InvalidRainDropBudgets)
        ));

        document = valid_document();
        document.wetness.rain_target = document.wetness.clear_target;
        assert!(matches!(
            document.validate(),
            Err(WeatherProfileError::InvalidProfileRelation(_))
        ));

        document = valid_document();
        document.post_processing.rain_grade.gamma = f32::INFINITY;
        assert!(matches!(
            document.validate(),
            Err(WeatherProfileError::InvalidColorGrade(_))
        ));
    }

    #[test]
    fn schema_identity_unknown_fields_and_size_are_strict() {
        let document = valid_document();
        document
            .validate_identity(document.project_id, document.area_id, document.revision)
            .unwrap();
        assert!(matches!(
            document.validate_identity(
                ProjectId::deterministic("other"),
                document.area_id,
                document.revision
            ),
            Err(WeatherProfileError::ProjectIdentityMismatch { .. })
        ));

        let json = document.to_pretty_json().unwrap();
        let unknown = json.replacen(
            "\"initial_state\": \"clear\",",
            "\"initial_state\": \"clear\",\n  \"engine_entity\": 4,",
            1,
        );
        assert!(matches!(
            WeatherProfileDocument::from_json(&unknown),
            Err(WeatherProfileError::Json(_))
        ));
        let oversized = " ".repeat(MAX_WEATHER_PROFILE_JSON_BYTES + 1);
        assert!(matches!(
            WeatherProfileDocument::from_json(&oversized),
            Err(WeatherProfileError::JsonTooLarge { .. })
        ));
    }
}
