use forge_core::{
    AreaId, CanonicalJsonError, ContentHash, CoordinateSystem, ProjectId, Revision, Vec3,
    canonical_json_sha256,
};
use serde::{Deserialize, Serialize};
use thiserror::Error;

pub const ENVIRONMENT_PROFILE_SCHEMA: &str = "forge.environment-profile";
pub const ENVIRONMENT_PROFILE_SCHEMA_VERSION: u32 = 1;
pub const MAX_ENVIRONMENT_PROFILE_JSON_BYTES: usize = 64 * 1024;

const MAX_NAME_LENGTH: usize = 128;
const MAX_ENVIRONMENT_DISTANCE_M: f32 = 100_000.0;
const MAX_ENVIRONMENT_MAP_RESOLUTION: u16 = 2_048;
const MAX_SUN_ILLUMINANCE_LUX: f32 = 200_000.0;
const MAX_VOLUME_AXIS_M: f32 = 10_000.0;
const MAX_VOLUMETRIC_STEP_COUNT: u16 = 256;

/// Versioned, renderer-independent source of truth for one area's daylight and fog.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct EnvironmentProfileDocument {
    pub schema: String,
    pub schema_version: u32,
    pub project_id: ProjectId,
    pub area_id: AreaId,
    pub revision: Revision,
    pub coordinate_system: CoordinateSystem,
    pub name: String,
    pub sky: SkyProfile,
    pub sun: SunProfile,
    pub distance_fog: DistanceFogProfile,
    pub volumetric_fog: VolumetricFogProfile,
}

impl EnvironmentProfileDocument {
    #[must_use]
    #[allow(clippy::too_many_arguments)]
    pub fn new(
        project_id: ProjectId,
        area_id: AreaId,
        revision: Revision,
        name: impl Into<String>,
        sky: SkyProfile,
        sun: SunProfile,
        distance_fog: DistanceFogProfile,
        volumetric_fog: VolumetricFogProfile,
    ) -> Self {
        Self {
            schema: ENVIRONMENT_PROFILE_SCHEMA.to_owned(),
            schema_version: ENVIRONMENT_PROFILE_SCHEMA_VERSION,
            project_id,
            area_id,
            revision,
            coordinate_system: CoordinateSystem::METRES_Y_UP_RIGHT_HANDED,
            name: name.into(),
            sky,
            sun,
            distance_fog,
            volumetric_fog,
        }
    }

    pub fn validate(&self) -> Result<(), EnvironmentProfileError> {
        if self.schema != ENVIRONMENT_PROFILE_SCHEMA {
            return Err(EnvironmentProfileError::InvalidSchema(self.schema.clone()));
        }
        if self.schema_version != ENVIRONMENT_PROFILE_SCHEMA_VERSION {
            return Err(EnvironmentProfileError::UnsupportedVersion(
                self.schema_version,
            ));
        }
        if self.revision.get() == 0 {
            return Err(EnvironmentProfileError::InvalidRevision);
        }
        if self.coordinate_system != CoordinateSystem::METRES_Y_UP_RIGHT_HANDED {
            return Err(EnvironmentProfileError::UnsupportedCoordinateSystem);
        }

        if self.name.trim().is_empty() {
            return Err(EnvironmentProfileError::EmptyName);
        }
        if self.name.len() > MAX_NAME_LENGTH {
            return Err(EnvironmentProfileError::NameTooLong(self.name.len()));
        }

        self.sky.validate()?;
        self.sun.validate()?;
        self.distance_fog.validate()?;
        self.volumetric_fog.validate()?;

        if self.sun.shadow_distance_m > self.sky.aerial_view_max_distance_m {
            return Err(EnvironmentProfileError::InvalidProfileRelation(
                "sun.shadow_distance_m cannot exceed sky.aerial_view_max_distance_m",
            ));
        }
        if self.distance_fog.measurement_distance_m() > self.sky.aerial_view_max_distance_m {
            return Err(EnvironmentProfileError::InvalidProfileRelation(
                "distance fog measurement distance cannot exceed sky aerial-view distance",
            ));
        }
        if self.volumetric_fog.maximum_distance_m > self.distance_fog.measurement_distance_m() {
            return Err(EnvironmentProfileError::InvalidProfileRelation(
                "volumetric fog maximum distance cannot exceed distance fog measurement distance",
            ));
        }
        if self.volumetric_fog.enabled
            && self.volumetric_fog.fallback == VolumetricFogFallback::DistanceFog
            && !self.distance_fog.enabled
        {
            return Err(EnvironmentProfileError::InvalidProfileRelation(
                "volumetric distance-fog fallback requires distance fog to be enabled",
            ));
        }
        Ok(())
    }

    /// Verifies that this area-scoped document belongs to the expected project revision.
    pub fn validate_identity(
        &self,
        project_id: ProjectId,
        area_id: AreaId,
        revision: Revision,
    ) -> Result<(), EnvironmentProfileError> {
        if self.project_id != project_id {
            return Err(EnvironmentProfileError::ProjectIdentityMismatch {
                expected: project_id,
                actual: self.project_id,
            });
        }
        if self.area_id != area_id {
            return Err(EnvironmentProfileError::AreaIdentityMismatch {
                expected: area_id,
                actual: self.area_id,
            });
        }
        if self.revision != revision {
            return Err(EnvironmentProfileError::RevisionMismatch {
                expected: revision,
                actual: self.revision,
            });
        }
        Ok(())
    }

    pub fn from_json(json: &str) -> Result<Self, EnvironmentProfileError> {
        if json.len() > MAX_ENVIRONMENT_PROFILE_JSON_BYTES {
            return Err(EnvironmentProfileError::JsonTooLarge {
                actual: json.len(),
                maximum: MAX_ENVIRONMENT_PROFILE_JSON_BYTES,
            });
        }
        let document: Self = serde_json::from_str(json)?;
        document.validate()?;
        Ok(document)
    }

    pub fn to_pretty_json(&self) -> Result<String, EnvironmentProfileError> {
        self.validate()?;
        Ok(serde_json::to_string_pretty(self)?)
    }

    pub fn content_hash(&self) -> Result<ContentHash, EnvironmentProfileError> {
        self.validate()?;
        Ok(canonical_json_sha256(self)?)
    }

    /// Resolves the requested fog path without pretending that a GPU effect executed.
    #[must_use]
    pub const fn resolve_fog_path(
        &self,
        capability: VolumetricFogCapability,
    ) -> EnvironmentFogResolution {
        if self.volumetric_fog.enabled {
            if matches!(capability, VolumetricFogCapability::Available) {
                EnvironmentFogResolution::Volumetric {
                    step_count: self.volumetric_fog.step_count,
                }
            } else {
                EnvironmentFogResolution::DistanceFogFallback {
                    requested_step_count: self.volumetric_fog.step_count,
                    capability,
                }
            }
        } else if self.distance_fog.enabled {
            EnvironmentFogResolution::DistanceFog
        } else {
            EnvironmentFogResolution::Disabled
        }
    }
}

/// Shared display-quality intent. It does not identify a renderer implementation.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum EnvironmentQualityTier {
    Low,
    Medium,
    High,
    Ultra,
}

/// Three-channel sRGB presentation color with normalized finite components.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SrgbColor {
    pub red: f32,
    pub green: f32,
    pub blue: f32,
}

impl SrgbColor {
    #[must_use]
    pub const fn new(red: f32, green: f32, blue: f32) -> Self {
        Self { red, green, blue }
    }

    fn validate(self, field: &'static str) -> Result<(), EnvironmentProfileError> {
        if [self.red, self.green, self.blue]
            .into_iter()
            .all(unit_interval)
        {
            Ok(())
        } else {
            Err(EnvironmentProfileError::InvalidNumericValue(field))
        }
    }
}

/// Physical-atmosphere intent plus an explicit clear-color fallback.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SkyProfile {
    pub enabled: bool,
    pub quality: EnvironmentQualityTier,
    pub fallback_clear_color_srgb: SrgbColor,
    pub medium_density_multiplier: f32,
    pub environment_light_intensity: f32,
    pub environment_resolution: u16,
    pub exposure_ev100: f32,
    pub aerial_view_max_distance_m: f32,
}

impl SkyProfile {
    fn validate(self) -> Result<(), EnvironmentProfileError> {
        self.fallback_clear_color_srgb
            .validate("sky.fallback_clear_color_srgb")?;
        bounded(
            self.medium_density_multiplier,
            0.0,
            100.0,
            "sky.medium_density_multiplier",
        )?;
        bounded(
            self.environment_light_intensity,
            0.0,
            1_000_000.0,
            "sky.environment_light_intensity",
        )?;
        if self.environment_resolution < 16
            || self.environment_resolution > MAX_ENVIRONMENT_MAP_RESOLUTION
            || !self.environment_resolution.is_power_of_two()
        {
            return Err(EnvironmentProfileError::InvalidEnvironmentResolution(
                self.environment_resolution,
            ));
        }
        bounded(self.exposure_ev100, -16.0, 32.0, "sky.exposure_ev100")?;
        positive_bounded(
            self.aerial_view_max_distance_m,
            MAX_ENVIRONMENT_DISTANCE_M,
            "sky.aerial_view_max_distance_m",
        )?;
        Ok(())
    }
}

/// Directional daylight intent expressed without an engine transform.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SunProfile {
    /// Horizontal bearing toward the sun: 0 degrees is world +Z and positive turns toward +X.
    pub azimuth_degrees: f32,
    /// Angle above the horizon; the derived vector points from the world toward the sun.
    pub elevation_degrees: f32,
    pub color_srgb: SrgbColor,
    pub illuminance_lux: f32,
    pub disk_angular_diameter_degrees: f32,
    pub disk_intensity: f32,
    pub shadow_distance_m: f32,
}

impl SunProfile {
    fn validate(self) -> Result<(), EnvironmentProfileError> {
        bounded(self.azimuth_degrees, -180.0, 180.0, "sun.azimuth_degrees")?;
        bounded(self.elevation_degrees, -18.0, 90.0, "sun.elevation_degrees")?;
        self.color_srgb.validate("sun.color_srgb")?;
        bounded(
            self.illuminance_lux,
            0.0,
            MAX_SUN_ILLUMINANCE_LUX,
            "sun.illuminance_lux",
        )?;
        positive_bounded(
            self.disk_angular_diameter_degrees,
            5.0,
            "sun.disk_angular_diameter_degrees",
        )?;
        bounded(self.disk_intensity, 0.0, 100.0, "sun.disk_intensity")?;
        positive_bounded(
            self.shadow_distance_m,
            MAX_ENVIRONMENT_DISTANCE_M,
            "sun.shadow_distance_m",
        )?;
        Ok(())
    }

    /// Unit direction from the world toward the sun in the Forge Y-up coordinate system.
    #[must_use]
    pub fn direction_toward_sun(self) -> Vec3 {
        let azimuth = self.azimuth_degrees.to_radians();
        let elevation = self.elevation_degrees.to_radians();
        Vec3::new(
            azimuth.sin() * elevation.cos(),
            elevation.sin(),
            azimuth.cos() * elevation.cos(),
        )
    }
}

/// Engine-neutral distance attenuation curve.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "mode", rename_all = "snake_case", deny_unknown_fields)]
pub enum DistanceFogFalloff {
    Linear {
        start_distance_m: f32,
        end_distance_m: f32,
    },
    /// Visibility is the distance at which contrast reaches `contrast_threshold`.
    Atmospheric {
        visibility_distance_m: f32,
        contrast_threshold: f32,
    },
}

impl DistanceFogFalloff {
    fn validate(self) -> Result<(), EnvironmentProfileError> {
        match self {
            Self::Linear {
                start_distance_m,
                end_distance_m,
            } => {
                bounded(
                    start_distance_m,
                    0.0,
                    MAX_ENVIRONMENT_DISTANCE_M,
                    "distance_fog.falloff.start_distance_m",
                )?;
                positive_bounded(
                    end_distance_m,
                    MAX_ENVIRONMENT_DISTANCE_M,
                    "distance_fog.falloff.end_distance_m",
                )?;
                if end_distance_m <= start_distance_m {
                    return Err(EnvironmentProfileError::InvalidProfileRelation(
                        "linear distance fog end must be greater than its start",
                    ));
                }
            }
            Self::Atmospheric {
                visibility_distance_m,
                contrast_threshold,
            } => {
                positive_bounded(
                    visibility_distance_m,
                    MAX_ENVIRONMENT_DISTANCE_M,
                    "distance_fog.falloff.visibility_distance_m",
                )?;
                exclusive_bounded(
                    contrast_threshold,
                    0.0,
                    1.0,
                    "distance_fog.falloff.contrast_threshold",
                )?;
            }
        }
        Ok(())
    }

    #[must_use]
    pub const fn measurement_distance_m(self) -> f32 {
        match self {
            Self::Linear { end_distance_m, .. } => end_distance_m,
            Self::Atmospheric {
                visibility_distance_m,
                ..
            } => visibility_distance_m,
        }
    }
}

/// Distance fog that also remains available as the volumetric fallback.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct DistanceFogProfile {
    pub enabled: bool,
    pub color_srgb: SrgbColor,
    pub maximum_opacity: f32,
    pub directional_light_color_srgb: SrgbColor,
    pub directional_light_exponent: f32,
    pub falloff: DistanceFogFalloff,
}

impl DistanceFogProfile {
    fn validate(self) -> Result<(), EnvironmentProfileError> {
        self.color_srgb.validate("distance_fog.color_srgb")?;
        bounded(
            self.maximum_opacity,
            0.0,
            1.0,
            "distance_fog.maximum_opacity",
        )?;
        if self.enabled && self.maximum_opacity <= 0.0 {
            return Err(EnvironmentProfileError::InvalidProfileRelation(
                "enabled distance fog requires positive maximum opacity",
            ));
        }
        self.directional_light_color_srgb
            .validate("distance_fog.directional_light_color_srgb")?;
        bounded(
            self.directional_light_exponent,
            0.0,
            1_024.0,
            "distance_fog.directional_light_exponent",
        )?;
        self.falloff.validate()
    }

    #[must_use]
    pub const fn measurement_distance_m(self) -> f32 {
        self.falloff.measurement_distance_m()
    }
}

/// Required behavior when volumetric rendering cannot execute.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum VolumetricFogFallback {
    DistanceFog,
}

/// Bounded global fog volume. The adapter may reduce quality but must report the resolved path.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct VolumetricFogProfile {
    pub enabled: bool,
    pub quality: EnvironmentQualityTier,
    pub step_count: u16,
    pub jitter: bool,
    pub ambient_color_srgb: SrgbColor,
    pub ambient_intensity: f32,
    pub volume_center_m: Vec3,
    pub volume_size_m: Vec3,
    pub density_factor: f32,
    pub absorption: f32,
    pub scattering: f32,
    pub scattering_asymmetry: f32,
    pub maximum_distance_m: f32,
    pub fallback: VolumetricFogFallback,
}

impl VolumetricFogProfile {
    fn validate(self) -> Result<(), EnvironmentProfileError> {
        if !(1..=MAX_VOLUMETRIC_STEP_COUNT).contains(&self.step_count) {
            return Err(EnvironmentProfileError::InvalidVolumetricStepCount(
                self.step_count,
            ));
        }
        self.ambient_color_srgb
            .validate("volumetric_fog.ambient_color_srgb")?;
        bounded(
            self.ambient_intensity,
            0.0,
            100.0,
            "volumetric_fog.ambient_intensity",
        )?;
        if !self.volume_center_m.is_finite()
            || self.volume_center_m.x.abs() > MAX_ENVIRONMENT_DISTANCE_M
            || self.volume_center_m.y.abs() > MAX_ENVIRONMENT_DISTANCE_M
            || self.volume_center_m.z.abs() > MAX_ENVIRONMENT_DISTANCE_M
        {
            return Err(EnvironmentProfileError::InvalidVolumeGeometry(
                "volume_center_m",
            ));
        }
        if !self.volume_size_m.is_finite()
            || self.volume_size_m.x <= 0.0
            || self.volume_size_m.y <= 0.0
            || self.volume_size_m.z <= 0.0
            || self.volume_size_m.x > MAX_VOLUME_AXIS_M
            || self.volume_size_m.y > MAX_VOLUME_AXIS_M
            || self.volume_size_m.z > MAX_VOLUME_AXIS_M
        {
            return Err(EnvironmentProfileError::InvalidVolumeGeometry(
                "volume_size_m",
            ));
        }
        bounded(
            self.density_factor,
            0.0,
            10.0,
            "volumetric_fog.density_factor",
        )?;
        bounded(self.absorption, 0.0, 10.0, "volumetric_fog.absorption")?;
        bounded(self.scattering, 0.0, 10.0, "volumetric_fog.scattering")?;
        bounded(
            self.scattering_asymmetry,
            -0.99,
            0.99,
            "volumetric_fog.scattering_asymmetry",
        )?;
        positive_bounded(
            self.maximum_distance_m,
            MAX_ENVIRONMENT_DISTANCE_M,
            "volumetric_fog.maximum_distance_m",
        )?;
        if self.enabled && (self.density_factor <= 0.0 || self.scattering <= 0.0) {
            return Err(EnvironmentProfileError::InvalidProfileRelation(
                "enabled volumetric fog requires positive density and scattering",
            ));
        }
        Ok(())
    }
}

/// Runtime capability input used to choose a truthful observable path.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum VolumetricFogCapability {
    Available,
    Unavailable,
    /// No real GPU execution has qualified the effect yet; this is not a hardware verdict.
    NotQualified,
}

/// Derived intent for metrics. It is not evidence that the GPU executed the requested effect.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum EnvironmentFogResolution {
    Disabled,
    DistanceFog,
    Volumetric {
        step_count: u16,
    },
    DistanceFogFallback {
        requested_step_count: u16,
        capability: VolumetricFogCapability,
    },
}

fn unit_interval(value: f32) -> bool {
    value.is_finite() && (0.0..=1.0).contains(&value)
}

fn positive_bounded(
    value: f32,
    maximum: f32,
    field: &'static str,
) -> Result<(), EnvironmentProfileError> {
    if !value.is_finite() || value <= 0.0 || value > maximum {
        return Err(EnvironmentProfileError::InvalidNumericValue(field));
    }
    Ok(())
}

fn bounded(
    value: f32,
    minimum: f32,
    maximum: f32,
    field: &'static str,
) -> Result<(), EnvironmentProfileError> {
    if !value.is_finite() || value < minimum || value > maximum {
        return Err(EnvironmentProfileError::InvalidNumericValue(field));
    }
    Ok(())
}

fn exclusive_bounded(
    value: f32,
    minimum: f32,
    maximum: f32,
    field: &'static str,
) -> Result<(), EnvironmentProfileError> {
    if !value.is_finite() || value <= minimum || value >= maximum {
        return Err(EnvironmentProfileError::InvalidNumericValue(field));
    }
    Ok(())
}

#[derive(Debug, Error)]
pub enum EnvironmentProfileError {
    #[error("invalid environment profile schema `{0}`")]
    InvalidSchema(String),
    #[error("unsupported environment profile schema version {0}")]
    UnsupportedVersion(u32),
    #[error("environment profile revision must be greater than zero")]
    InvalidRevision,
    #[error("environment profile coordinate system must be metres, Y-up, right-handed")]
    UnsupportedCoordinateSystem,
    #[error("environment profile name cannot be empty")]
    EmptyName,
    #[error("environment profile name is too long: {0} bytes")]
    NameTooLong(usize),
    #[error("environment profile JSON is too large: {actual} bytes, maximum {maximum}")]
    JsonTooLarge { actual: usize, maximum: usize },
    #[error("environment profile has an invalid numeric value for `{0}`")]
    InvalidNumericValue(&'static str),
    #[error("environment map resolution must be a power of two from 16 to 2048, got {0}")]
    InvalidEnvironmentResolution(u16),
    #[error("volumetric fog step count must be from 1 to 256, got {0}")]
    InvalidVolumetricStepCount(u16),
    #[error("volumetric fog has invalid geometry for `{0}`")]
    InvalidVolumeGeometry(&'static str),
    #[error("environment profile violates relation: {0}")]
    InvalidProfileRelation(&'static str),
    #[error("environment project identity mismatch: expected {expected}, got {actual}")]
    ProjectIdentityMismatch {
        expected: ProjectId,
        actual: ProjectId,
    },
    #[error("environment area identity mismatch: expected {expected}, got {actual}")]
    AreaIdentityMismatch { expected: AreaId, actual: AreaId },
    #[error("environment revision mismatch: expected {expected:?}, got {actual:?}")]
    RevisionMismatch {
        expected: Revision,
        actual: Revision,
    },
    #[error("invalid environment profile JSON: {0}")]
    Json(#[from] serde_json::Error),
    #[error("cannot hash environment profile: {0}")]
    Hash(#[from] CanonicalJsonError),
}

#[cfg(test)]
mod tests {
    use super::*;

    fn valid_document() -> EnvironmentProfileDocument {
        EnvironmentProfileDocument::new(
            ProjectId::deterministic("environment-test-project"),
            AreaId::deterministic("environment-test-area"),
            Revision::INITIAL,
            "Test environment",
            SkyProfile {
                enabled: true,
                quality: EnvironmentQualityTier::High,
                fallback_clear_color_srgb: SrgbColor::new(0.08, 0.14, 0.24),
                medium_density_multiplier: 1.0,
                environment_light_intensity: 180.0,
                environment_resolution: 256,
                exposure_ev100: 12.0,
                aerial_view_max_distance_m: 1_600.0,
            },
            SunProfile {
                azimuth_degrees: -52.0,
                elevation_degrees: 38.0,
                color_srgb: SrgbColor::new(1.0, 0.93, 0.78),
                illuminance_lux: 55_000.0,
                disk_angular_diameter_degrees: 0.53,
                disk_intensity: 1.0,
                shadow_distance_m: 128.0,
            },
            DistanceFogProfile {
                enabled: true,
                color_srgb: SrgbColor::new(0.64, 0.72, 0.78),
                maximum_opacity: 0.82,
                directional_light_color_srgb: SrgbColor::new(1.0, 0.87, 0.68),
                directional_light_exponent: 8.0,
                falloff: DistanceFogFalloff::Atmospheric {
                    visibility_distance_m: 640.0,
                    contrast_threshold: 0.05,
                },
            },
            VolumetricFogProfile {
                enabled: true,
                quality: EnvironmentQualityTier::High,
                step_count: 64,
                jitter: true,
                ambient_color_srgb: SrgbColor::new(0.56, 0.65, 0.72),
                ambient_intensity: 0.18,
                volume_center_m: Vec3::new(0.0, 32.0, 0.0),
                volume_size_m: Vec3::new(512.0, 192.0, 512.0),
                density_factor: 0.035,
                absorption: 0.08,
                scattering: 0.22,
                scattering_asymmetry: 0.35,
                maximum_distance_m: 320.0,
                fallback: VolumetricFogFallback::DistanceFog,
            },
        )
    }

    #[test]
    fn versioned_json_round_trip_and_hash_are_deterministic() {
        let document = valid_document();
        let json = document.to_pretty_json().unwrap();
        let restored = EnvironmentProfileDocument::from_json(&json).unwrap();

        assert_eq!(restored, document);
        assert_eq!(restored.to_pretty_json().unwrap(), json);
        assert_eq!(
            restored.content_hash().unwrap(),
            document.content_hash().unwrap()
        );
    }

    #[test]
    fn unknown_fields_and_non_finite_json_are_rejected() {
        let json = valid_document().to_pretty_json().unwrap();
        let unknown = json.replacen(
            "\"medium_density_multiplier\": 1.0,",
            "\"medium_density_multiplier\": 1.0,\n    \"engine_handle\": 7,",
            1,
        );
        assert!(matches!(
            EnvironmentProfileDocument::from_json(&unknown),
            Err(EnvironmentProfileError::Json(_))
        ));

        let non_finite = json.replacen(
            "\"illuminance_lux\": 55000.0",
            "\"illuminance_lux\": NaN",
            1,
        );
        assert!(matches!(
            EnvironmentProfileDocument::from_json(&non_finite),
            Err(EnvironmentProfileError::Json(_))
        ));
    }

    #[test]
    fn identity_is_checked_as_one_project_area_revision_scope() {
        let document = valid_document();
        assert!(
            document
                .validate_identity(document.project_id, document.area_id, document.revision)
                .is_ok()
        );
        assert!(matches!(
            document.validate_identity(
                ProjectId::deterministic("other-project"),
                document.area_id,
                document.revision
            ),
            Err(EnvironmentProfileError::ProjectIdentityMismatch { .. })
        ));
        assert!(matches!(
            document.validate_identity(
                document.project_id,
                AreaId::deterministic("other-area"),
                document.revision
            ),
            Err(EnvironmentProfileError::AreaIdentityMismatch { .. })
        ));
        assert!(matches!(
            document.validate_identity(document.project_id, document.area_id, Revision::new(2)),
            Err(EnvironmentProfileError::RevisionMismatch { .. })
        ));
    }

    #[test]
    fn numeric_bounds_and_cross_profile_relations_are_strict() {
        let mut document = valid_document();
        document.sky.environment_resolution = 300;
        assert!(matches!(
            document.validate(),
            Err(EnvironmentProfileError::InvalidEnvironmentResolution(300))
        ));

        document = valid_document();
        document.sun.illuminance_lux = f32::INFINITY;
        assert!(document.validate().is_err());

        document = valid_document();
        document.distance_fog.falloff = DistanceFogFalloff::Atmospheric {
            visibility_distance_m: 640.0,
            contrast_threshold: 1.0,
        };
        assert!(document.validate().is_err());

        document = valid_document();
        document.volumetric_fog.step_count = 0;
        assert!(matches!(
            document.validate(),
            Err(EnvironmentProfileError::InvalidVolumetricStepCount(0))
        ));

        document = valid_document();
        document.volumetric_fog.volume_size_m.x = -1.0;
        assert!(matches!(
            document.validate(),
            Err(EnvironmentProfileError::InvalidVolumeGeometry(
                "volume_size_m"
            ))
        ));

        document = valid_document();
        document.distance_fog.enabled = false;
        assert!(matches!(
            document.validate(),
            Err(EnvironmentProfileError::InvalidProfileRelation(_))
        ));

        document = valid_document();
        document.volumetric_fog.maximum_distance_m = 800.0;
        assert!(matches!(
            document.validate(),
            Err(EnvironmentProfileError::InvalidProfileRelation(_))
        ));
    }

    #[test]
    fn fog_fallback_resolution_is_explicit_and_measurable() {
        let document = valid_document();
        assert_eq!(
            document.resolve_fog_path(VolumetricFogCapability::Available),
            EnvironmentFogResolution::Volumetric { step_count: 64 }
        );
        assert_eq!(
            document.resolve_fog_path(VolumetricFogCapability::Unavailable),
            EnvironmentFogResolution::DistanceFogFallback {
                requested_step_count: 64,
                capability: VolumetricFogCapability::Unavailable,
            }
        );
        assert_eq!(
            document.resolve_fog_path(VolumetricFogCapability::NotQualified),
            EnvironmentFogResolution::DistanceFogFallback {
                requested_step_count: 64,
                capability: VolumetricFogCapability::NotQualified,
            }
        );

        let mut distance_only = valid_document();
        distance_only.volumetric_fog.enabled = false;
        assert_eq!(
            distance_only.resolve_fog_path(VolumetricFogCapability::Unavailable),
            EnvironmentFogResolution::DistanceFog
        );
        distance_only.distance_fog.enabled = false;
        assert_eq!(
            distance_only.resolve_fog_path(VolumetricFogCapability::Unavailable),
            EnvironmentFogResolution::Disabled
        );
    }

    #[test]
    fn raw_json_name_and_volume_center_are_bounded() {
        let mut document = valid_document();
        document.name = format!("ok{}", " ".repeat(MAX_NAME_LENGTH));
        assert!(matches!(
            document.validate(),
            Err(EnvironmentProfileError::NameTooLong(_))
        ));

        let oversized = " ".repeat(MAX_ENVIRONMENT_PROFILE_JSON_BYTES + 1);
        assert!(matches!(
            EnvironmentProfileDocument::from_json(&oversized),
            Err(EnvironmentProfileError::JsonTooLarge { .. })
        ));

        document = valid_document();
        document.volumetric_fog.volume_center_m.x = MAX_ENVIRONMENT_DISTANCE_M + 1.0;
        assert!(matches!(
            document.validate(),
            Err(EnvironmentProfileError::InvalidVolumeGeometry(
                "volume_center_m"
            ))
        ));
    }

    #[test]
    fn sun_direction_convention_has_cardinal_goldens() {
        let mut sun = valid_document().sun;
        sun.azimuth_degrees = 0.0;
        sun.elevation_degrees = 0.0;
        assert_eq!(sun.direction_toward_sun(), Vec3::new(0.0, 0.0, 1.0));

        sun.azimuth_degrees = 90.0;
        let east = sun.direction_toward_sun();
        assert!((east.x - 1.0).abs() < 1.0e-6);
        assert!(east.y.abs() < 1.0e-6);
        assert!(east.z.abs() < 1.0e-6);

        sun.azimuth_degrees = 0.0;
        sun.elevation_degrees = 90.0;
        let zenith = sun.direction_toward_sun();
        assert!(zenith.x.abs() < 1.0e-6);
        assert!((zenith.y - 1.0).abs() < 1.0e-6);
        assert!(zenith.z.abs() < 1.0e-6);
    }
}
