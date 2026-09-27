use forge_core::{
    AreaId, CanonicalJsonError, ContentHash, CoordinateSystem, ProjectId, Revision, Vec3,
    canonical_json_sha256,
};
use serde::{Deserialize, Serialize};
use thiserror::Error;

mod medium;
pub use medium::WaterMediumProfile;

pub const WATER_DOCUMENT_SCHEMA: &str = "forge.water";
pub const WATER_DOCUMENT_SCHEMA_VERSION: u32 = 1;
pub const MIN_RIVER_CONTROL_POINTS: usize = 4;
pub const MAX_RIVER_CONTROL_POINTS: usize = 64;
pub const MAX_LINEAR_RIVER_CONTROL_POINTS: usize = 2_048;
pub const MAX_RIVER_SAMPLE_COUNT: u32 = 262_144;
pub const MAX_WATER_DOCUMENT_JSON_BYTES: usize = 256 * 1_024;

const MAX_NAME_LENGTH: usize = 128;
const MAX_WORLD_COORDINATE_M: f32 = 100_000.0;
const MAX_RIVER_LENGTH_M: f32 = 100_000.0;
const MAX_RIVER_SAMPLE_COUNT_F32: f32 = 262_144.0;
const MIN_CONTROL_POINT_DISTANCE_M: f32 = 0.5;
const MAX_RIVER_WIDTH_M: f32 = 2_048.0;
const MAX_RIVER_DEPTH_M: f32 = 256.0;
const MAX_OPTICS_STEPS: u16 = 256;

/// Versioned, renderer-independent source of truth for one area's river water.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WaterDocument {
    pub schema: String,
    pub schema_version: u32,
    pub project_id: ProjectId,
    pub area_id: AreaId,
    pub revision: Revision,
    pub coordinate_system: CoordinateSystem,
    pub name: String,
    /// Stable seed for procedural bank erosion and surface phase derivation.
    pub seed: u64,
    pub river: RiverProfile,
    pub surface: WaterSurfaceProfile,
    pub banks: RiverBankProfile,
    pub optics: WaterOpticsProfile,
    /// Opt-in homogeneous RGB medium; absence preserves the legacy palette model.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub medium: Option<WaterMediumProfile>,
}

impl WaterDocument {
    #[must_use]
    #[allow(clippy::too_many_arguments)]
    pub fn new(
        project_id: ProjectId,
        area_id: AreaId,
        revision: Revision,
        name: impl Into<String>,
        seed: u64,
        river: RiverProfile,
        surface: WaterSurfaceProfile,
        banks: RiverBankProfile,
        optics: WaterOpticsProfile,
    ) -> Self {
        Self {
            schema: WATER_DOCUMENT_SCHEMA.to_owned(),
            schema_version: WATER_DOCUMENT_SCHEMA_VERSION,
            project_id,
            area_id,
            revision,
            coordinate_system: CoordinateSystem::METRES_Y_UP_RIGHT_HANDED,
            name: name.into(),
            seed,
            river,
            surface,
            banks,
            optics,
            medium: None,
        }
    }

    pub fn validate(&self) -> Result<(), WaterDocumentError> {
        if self.schema != WATER_DOCUMENT_SCHEMA {
            return Err(WaterDocumentError::InvalidSchema(self.schema.clone()));
        }
        if self.schema_version != WATER_DOCUMENT_SCHEMA_VERSION {
            return Err(WaterDocumentError::UnsupportedVersion(self.schema_version));
        }
        if self.revision.get() == 0 {
            return Err(WaterDocumentError::InvalidRevision);
        }
        if self.coordinate_system != CoordinateSystem::METRES_Y_UP_RIGHT_HANDED {
            return Err(WaterDocumentError::UnsupportedCoordinateSystem);
        }

        let trimmed_name = self.name.trim();
        if trimmed_name.is_empty() {
            return Err(WaterDocumentError::EmptyName);
        }
        if self.name.len() > MAX_NAME_LENGTH {
            return Err(WaterDocumentError::NameTooLong(self.name.len()));
        }

        self.river.validate()?;
        self.surface.validate()?;
        self.banks.validate()?;
        self.optics.validate()?;
        if let Some(medium) = self.medium {
            medium.validate()?;
        }

        let minimum_depth_m = self
            .river
            .control_points
            .iter()
            .map(|point| point.bed_depth_m)
            .fold(f32::INFINITY, f32::min);
        if self.surface.wave_amplitude_m >= minimum_depth_m {
            return Err(WaterDocumentError::InvalidProfileRelation(
                "surface wave amplitude must remain below every authored bed depth",
            ));
        }
        if let WaterRefractionProfile::ScreenSpace { max_depth_m, .. } = self.optics.refraction
            && max_depth_m > self.river.maximum_bed_depth_m()
        {
            return Err(WaterDocumentError::InvalidProfileRelation(
                "screen-space refraction depth cannot exceed the authored river depth envelope",
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
    ) -> Result<(), WaterDocumentError> {
        if self.project_id != project_id {
            return Err(WaterDocumentError::ProjectIdentityMismatch {
                expected: project_id,
                actual: self.project_id,
            });
        }
        if self.area_id != area_id {
            return Err(WaterDocumentError::AreaIdentityMismatch {
                expected: area_id,
                actual: self.area_id,
            });
        }
        if self.revision != revision {
            return Err(WaterDocumentError::RevisionMismatch {
                expected: revision,
                actual: self.revision,
            });
        }
        Ok(())
    }

    pub fn from_json(json: &str) -> Result<Self, WaterDocumentError> {
        if json.len() > MAX_WATER_DOCUMENT_JSON_BYTES {
            return Err(WaterDocumentError::JsonTooLarge {
                length: json.len(),
                maximum: MAX_WATER_DOCUMENT_JSON_BYTES,
            });
        }
        let document: Self = serde_json::from_str(json)?;
        document.validate()?;
        Ok(document)
    }

    pub fn to_pretty_json(&self) -> Result<String, WaterDocumentError> {
        self.validate()?;
        Ok(serde_json::to_string_pretty(self)?)
    }

    pub fn content_hash(&self) -> Result<ContentHash, WaterDocumentError> {
        self.validate()?;
        Ok(canonical_json_sha256(self)?)
    }

    /// Resolves requested optical paths for metrics without claiming that a GPU pass executed.
    #[must_use]
    pub fn resolve_optics_path(
        &self,
        capabilities: WaterOpticsCapabilities,
    ) -> WaterOpticsResolution {
        let (reflection, reflection_fallback_used) = self.optics.resolve_reflection(capabilities);
        let (refraction, refraction_fallback_used) = self.optics.resolve_refraction(capabilities);

        WaterOpticsResolution {
            requested_quality: self.optics.quality,
            capabilities,
            reflection,
            refraction,
            reflection_fallback_used,
            refraction_fallback_used,
        }
    }
}

/// Stable interpolation contract for the authored river centreline.
///
/// Version 1 uses the centripetal Catmull-Rom formula with alpha `0.5` for plan-view X/Z, with knot
/// spacing derived from XYZ distance. The canonical sampler extrapolates endpoint neighbours
/// linearly, allocates
/// `ceil(horizontal_chord / spacing)` intervals per authored segment, and samples them at uniform
/// segment parameter. Surface Y, width, and depth interpolate linearly so source-to-outlet descent
/// and positive cross-sections cannot overshoot. `sample_spacing_m` is therefore a deterministic
/// control-polygon tessellation budget, not a promise of exact arc-length spacing on a curve.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum RiverPathAlgorithm {
    CentripetalCatmullRomV1,
    /// Preserve an already sampled carving polyline without a second smoothing pass.
    PiecewiseLinearV1,
}

/// Explicit surface-height authority, independent of horizontal path interpolation.
#[derive(Clone, Copy, Debug, Default, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum RiverElevationPolicy {
    #[default]
    TerrainClearanceMonotoneV1,
    /// Keep authored surface levels; terrain may occlude an incompletely carved bed.
    AuthoredSurfaceV1,
}

impl RiverElevationPolicy {
    #[allow(
        clippy::trivially_copy_pass_by_ref,
        reason = "serde skip_serializing_if requires a borrowed value"
    )]
    fn is_default(&self) -> bool {
        *self == Self::TerrainClearanceMonotoneV1
    }
}

/// Ordered control points and deterministic sampling parameters for one bounded river.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct RiverProfile {
    pub algorithm: RiverPathAlgorithm,
    #[serde(default, skip_serializing_if = "RiverElevationPolicy::is_default")]
    pub elevation_policy: RiverElevationPolicy,
    pub sample_spacing_m: f32,
    pub control_points: Vec<RiverControlPoint>,
}

impl RiverProfile {
    fn validate(&self) -> Result<(), WaterDocumentError> {
        if self.control_points.len() < MIN_RIVER_CONTROL_POINTS {
            return Err(WaterDocumentError::TooFewRiverControlPoints(
                self.control_points.len(),
            ));
        }
        let maximum = match self.algorithm {
            RiverPathAlgorithm::CentripetalCatmullRomV1 => MAX_RIVER_CONTROL_POINTS,
            RiverPathAlgorithm::PiecewiseLinearV1 => MAX_LINEAR_RIVER_CONTROL_POINTS,
        };
        if self.control_points.len() > maximum {
            return Err(WaterDocumentError::TooManyRiverControlPoints(
                self.control_points.len(),
            ));
        }
        positive_bounded(
            self.sample_spacing_m,
            MAX_RIVER_LENGTH_M,
            "river.sample_spacing_m",
        )?;

        for (index, point) in self.control_points.iter().enumerate() {
            point.validate(index)?;
        }

        let mut path_length_m = 0.0;
        let mut requested_sample_count = 1.0;
        for (index, pair) in self.control_points.windows(2).enumerate() {
            let delta_x = pair[1].center_m.x - pair[0].center_m.x;
            let delta_z = pair[1].center_m.z - pair[0].center_m.z;
            let horizontal_distance_m = delta_x.hypot(delta_z);
            if !horizontal_distance_m.is_finite()
                || horizontal_distance_m < MIN_CONTROL_POINT_DISTANCE_M
            {
                return Err(WaterDocumentError::ControlPointsTooClose {
                    first_index: index,
                    second_index: index + 1,
                });
            }
            if pair[1].center_m.y > pair[0].center_m.y {
                return Err(WaterDocumentError::UphillRiverSegment {
                    first_index: index,
                    second_index: index + 1,
                });
            }
            path_length_m += horizontal_distance_m;
            requested_sample_count += (horizontal_distance_m / self.sample_spacing_m).ceil();
        }
        if !path_length_m.is_finite() || path_length_m > MAX_RIVER_LENGTH_M {
            return Err(WaterDocumentError::RiverPathTooLong);
        }
        if self.sample_spacing_m > path_length_m {
            return Err(WaterDocumentError::InvalidProfileRelation(
                "river sample spacing cannot exceed the control-point path length",
            ));
        }
        if !requested_sample_count.is_finite()
            || requested_sample_count > MAX_RIVER_SAMPLE_COUNT_F32
        {
            return Err(WaterDocumentError::TooManyRiverSamples {
                maximum: MAX_RIVER_SAMPLE_COUNT,
            });
        }
        Ok(())
    }

    #[must_use]
    pub fn approximate_path_length_m(&self) -> f32 {
        self.control_points
            .windows(2)
            .map(|pair| {
                let delta_x = pair[1].center_m.x - pair[0].center_m.x;
                let delta_z = pair[1].center_m.z - pair[0].center_m.z;
                delta_x.hypot(delta_z)
            })
            .sum()
    }

    #[must_use]
    pub fn maximum_bed_depth_m(&self) -> f32 {
        self.control_points
            .iter()
            .map(|point| point.bed_depth_m)
            .fold(0.0, f32::max)
    }

    /// Number of samples emitted by the canonical chord-budget sampler, including both endpoints.
    pub fn requested_sample_count(&self) -> Result<u32, WaterDocumentError> {
        self.validate()?;
        let sample_count = self
            .control_points
            .windows(2)
            .map(|pair| {
                let delta_x = pair[1].center_m.x - pair[0].center_m.x;
                let delta_z = pair[1].center_m.z - pair[0].center_m.z;
                (delta_x.hypot(delta_z) / self.sample_spacing_m).ceil()
            })
            .sum::<f32>()
            + 1.0;

        #[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
        Ok(sample_count as u32)
    }
}

/// One stable river-centreline sample in metres, in document order from source to outlet.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct RiverControlPoint {
    pub center_m: Vec3,
    pub surface_width_m: f32,
    pub bed_depth_m: f32,
}

impl RiverControlPoint {
    fn validate(self, index: usize) -> Result<(), WaterDocumentError> {
        if !self.center_m.is_finite()
            || self.center_m.x.abs() > MAX_WORLD_COORDINATE_M
            || self.center_m.y.abs() > MAX_WORLD_COORDINATE_M
            || self.center_m.z.abs() > MAX_WORLD_COORDINATE_M
        {
            return Err(WaterDocumentError::InvalidControlPoint {
                index,
                field: "center_m",
            });
        }
        if !self.surface_width_m.is_finite()
            || self.surface_width_m <= 0.0
            || self.surface_width_m > MAX_RIVER_WIDTH_M
        {
            return Err(WaterDocumentError::InvalidControlPoint {
                index,
                field: "surface_width_m",
            });
        }
        if !self.bed_depth_m.is_finite()
            || self.bed_depth_m <= 0.0
            || self.bed_depth_m > MAX_RIVER_DEPTH_M
        {
            return Err(WaterDocumentError::InvalidControlPoint {
                index,
                field: "bed_depth_m",
            });
        }
        Ok(())
    }
}

/// Engine-neutral surface appearance and procedural motion intent.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WaterSurfaceProfile {
    pub shallow_color_srgb: WaterSrgbColor,
    pub deep_color_srgb: WaterSrgbColor,
    pub opacity: f32,
    pub roughness: f32,
    pub flow_speed_mps: f32,
    pub wave_amplitude_m: f32,
    pub wave_length_m: f32,
    pub normal_strength: f32,
}

impl WaterSurfaceProfile {
    fn validate(self) -> Result<(), WaterDocumentError> {
        self.shallow_color_srgb
            .validate("surface.shallow_color_srgb")?;
        self.deep_color_srgb.validate("surface.deep_color_srgb")?;
        bounded(self.opacity, 0.0, 1.0, "surface.opacity")?;
        if self.opacity <= 0.0 {
            return Err(WaterDocumentError::InvalidProfileRelation(
                "water surface opacity must be positive",
            ));
        }
        bounded(self.roughness, 0.0, 1.0, "surface.roughness")?;
        bounded(self.flow_speed_mps, 0.0, 20.0, "surface.flow_speed_mps")?;
        bounded(self.wave_amplitude_m, 0.0, 5.0, "surface.wave_amplitude_m")?;
        positive_bounded(self.wave_length_m, 1_000.0, "surface.wave_length_m")?;
        bounded(self.normal_strength, 0.0, 8.0, "surface.normal_strength")?;
        Ok(())
    }
}

/// Three-channel sRGB water color with normalized finite components.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WaterSrgbColor {
    pub red: f32,
    pub green: f32,
    pub blue: f32,
}

impl WaterSrgbColor {
    #[must_use]
    pub const fn new(red: f32, green: f32, blue: f32) -> Self {
        Self { red, green, blue }
    }

    fn validate(self, field: &'static str) -> Result<(), WaterDocumentError> {
        if [self.red, self.green, self.blue]
            .into_iter()
            .all(unit_interval)
        {
            Ok(())
        } else {
            Err(WaterDocumentError::InvalidNumericValue(field))
        }
    }
}

/// Terrain-carving and wet-edge intent around the authored water surface.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct RiverBankProfile {
    pub shoulder_width_m: f32,
    pub blend_width_m: f32,
    pub wet_margin_m: f32,
    pub horizontal_to_vertical_slope: f32,
    pub erosion_amplitude_m: f32,
    pub erosion_frequency_cycles_per_m: f32,
}

impl RiverBankProfile {
    fn validate(self) -> Result<(), WaterDocumentError> {
        positive_bounded(
            self.shoulder_width_m,
            MAX_RIVER_WIDTH_M,
            "banks.shoulder_width_m",
        )?;
        positive_bounded(self.blend_width_m, MAX_RIVER_WIDTH_M, "banks.blend_width_m")?;
        bounded(
            self.wet_margin_m,
            0.0,
            MAX_RIVER_WIDTH_M,
            "banks.wet_margin_m",
        )?;
        if self.wet_margin_m > self.shoulder_width_m + self.blend_width_m {
            return Err(WaterDocumentError::InvalidProfileRelation(
                "bank wet margin cannot exceed shoulder plus blend width",
            ));
        }
        positive_bounded(
            self.horizontal_to_vertical_slope,
            100.0,
            "banks.horizontal_to_vertical_slope",
        )?;
        bounded(
            self.erosion_amplitude_m,
            0.0,
            20.0,
            "banks.erosion_amplitude_m",
        )?;
        positive_bounded(
            self.erosion_frequency_cycles_per_m,
            100.0,
            "banks.erosion_frequency_cycles_per_m",
        )?;
        Ok(())
    }
}

/// Shared display-quality intent. It does not identify a renderer implementation.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum WaterQualityTier {
    Low,
    Medium,
    High,
    Ultra,
}

/// Requested reflection implementation and its bounded work budget.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "mode", rename_all = "snake_case", deny_unknown_fields)]
pub enum WaterReflectionProfile {
    Disabled,
    Environment {
        intensity: f32,
    },
    ScreenSpace {
        intensity: f32,
        max_steps: u16,
        max_distance_m: f32,
        thickness_m: f32,
    },
}

impl WaterReflectionProfile {
    fn validate(self) -> Result<(), WaterDocumentError> {
        match self {
            Self::Disabled => Ok(()),
            Self::Environment { intensity } => {
                positive_bounded(intensity, 4.0, "optics.reflection.intensity")
            }
            Self::ScreenSpace {
                intensity,
                max_steps,
                max_distance_m,
                thickness_m,
            } => {
                positive_bounded(intensity, 4.0, "optics.reflection.intensity")?;
                validate_step_count(max_steps, "reflection")?;
                positive_bounded(
                    max_distance_m,
                    MAX_RIVER_LENGTH_M,
                    "optics.reflection.max_distance_m",
                )?;
                positive_bounded(
                    thickness_m,
                    MAX_RIVER_DEPTH_M,
                    "optics.reflection.thickness_m",
                )
            }
        }
    }
}

/// Requested refraction implementation and its bounded work budget.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "mode", rename_all = "snake_case", deny_unknown_fields)]
pub enum WaterRefractionProfile {
    Disabled,
    SurfaceTint {
        strength: f32,
    },
    ScreenSpace {
        strength: f32,
        max_steps: u16,
        max_depth_m: f32,
    },
}

impl WaterRefractionProfile {
    fn validate(self) -> Result<(), WaterDocumentError> {
        match self {
            Self::Disabled => Ok(()),
            Self::SurfaceTint { strength } => {
                positive_bounded(strength, 1.0, "optics.refraction.strength")
            }
            Self::ScreenSpace {
                strength,
                max_steps,
                max_depth_m,
            } => {
                positive_bounded(strength, 1.0, "optics.refraction.strength")?;
                validate_step_count(max_steps, "refraction")?;
                positive_bounded(
                    max_depth_m,
                    MAX_RIVER_DEPTH_M,
                    "optics.refraction.max_depth_m",
                )
            }
        }
    }
}

/// Required reflection downgrade when the requested path is unavailable.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum WaterReflectionFallback {
    EnvironmentThenDisabled,
    Disabled,
}

/// Required refraction downgrade when the requested path is unavailable.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum WaterRefractionFallback {
    SurfaceTint,
    Disabled,
}

/// Quality-scoped optical intent plus explicit, observable downgrade paths.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WaterOpticsProfile {
    pub quality: WaterQualityTier,
    pub reflection: WaterReflectionProfile,
    pub refraction: WaterRefractionProfile,
    pub reflection_fallback: WaterReflectionFallback,
    pub refraction_fallback: WaterRefractionFallback,
}

impl WaterOpticsProfile {
    fn validate(self) -> Result<(), WaterDocumentError> {
        self.reflection.validate()?;
        self.refraction.validate()
    }

    fn resolve_reflection(
        self,
        capabilities: WaterOpticsCapabilities,
    ) -> (ResolvedWaterReflectionPath, bool) {
        match self.reflection {
            WaterReflectionProfile::Disabled => (ResolvedWaterReflectionPath::Disabled, false),
            WaterReflectionProfile::Environment { .. } => {
                if capabilities.environment_reflections == WaterOpticsCapabilityStatus::Available {
                    (ResolvedWaterReflectionPath::Environment, false)
                } else {
                    (ResolvedWaterReflectionPath::Disabled, true)
                }
            }
            WaterReflectionProfile::ScreenSpace { max_steps, .. } => {
                if capabilities.screen_space_reflections == WaterOpticsCapabilityStatus::Available {
                    (
                        ResolvedWaterReflectionPath::ScreenSpace { max_steps },
                        false,
                    )
                } else {
                    match self.reflection_fallback {
                        WaterReflectionFallback::EnvironmentThenDisabled
                            if capabilities.environment_reflections
                                == WaterOpticsCapabilityStatus::Available =>
                        {
                            (ResolvedWaterReflectionPath::Environment, true)
                        }
                        WaterReflectionFallback::EnvironmentThenDisabled
                        | WaterReflectionFallback::Disabled => {
                            (ResolvedWaterReflectionPath::Disabled, true)
                        }
                    }
                }
            }
        }
    }

    fn resolve_refraction(
        self,
        capabilities: WaterOpticsCapabilities,
    ) -> (ResolvedWaterRefractionPath, bool) {
        match self.refraction {
            WaterRefractionProfile::Disabled => (ResolvedWaterRefractionPath::Disabled, false),
            WaterRefractionProfile::SurfaceTint { .. } => {
                (ResolvedWaterRefractionPath::SurfaceTint, false)
            }
            WaterRefractionProfile::ScreenSpace { max_steps, .. } => {
                if capabilities.screen_space_refraction == WaterOpticsCapabilityStatus::Available {
                    (
                        ResolvedWaterRefractionPath::ScreenSpace { max_steps },
                        false,
                    )
                } else {
                    match self.refraction_fallback {
                        WaterRefractionFallback::SurfaceTint => {
                            (ResolvedWaterRefractionPath::SurfaceTint, true)
                        }
                        WaterRefractionFallback::Disabled => {
                            (ResolvedWaterRefractionPath::Disabled, true)
                        }
                    }
                }
            }
        }
    }
}

/// Qualification state for one renderer capability.
#[derive(Clone, Copy, Debug, Default, Eq, PartialEq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum WaterOpticsCapabilityStatus {
    Available,
    Unavailable,
    #[default]
    NotQualified,
}

/// Renderer capability observations supplied before choosing a truthful optical path.
#[derive(Clone, Copy, Debug, Default, Eq, PartialEq, Serialize)]
#[serde(deny_unknown_fields)]
pub struct WaterOpticsCapabilities {
    pub screen_space_reflections: WaterOpticsCapabilityStatus,
    pub screen_space_refraction: WaterOpticsCapabilityStatus,
    pub environment_reflections: WaterOpticsCapabilityStatus,
}

/// Reflection path selected for metrics; it is not proof that a GPU pass executed.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize)]
#[serde(tag = "mode", rename_all = "snake_case")]
pub enum ResolvedWaterReflectionPath {
    Disabled,
    Environment,
    ScreenSpace { max_steps: u16 },
}

/// Refraction path selected for metrics; it is not proof that a GPU pass executed.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize)]
#[serde(tag = "mode", rename_all = "snake_case")]
pub enum ResolvedWaterRefractionPath {
    Disabled,
    SurfaceTint,
    ScreenSpace { max_steps: u16 },
}

/// Fully resolved optical intent suitable for a structured runtime report.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize)]
#[serde(deny_unknown_fields)]
pub struct WaterOpticsResolution {
    pub requested_quality: WaterQualityTier,
    pub capabilities: WaterOpticsCapabilities,
    pub reflection: ResolvedWaterReflectionPath,
    pub refraction: ResolvedWaterRefractionPath,
    pub reflection_fallback_used: bool,
    pub refraction_fallback_used: bool,
}

fn validate_step_count(value: u16, effect: &'static str) -> Result<(), WaterDocumentError> {
    if !(1..=MAX_OPTICS_STEPS).contains(&value) {
        return Err(WaterDocumentError::InvalidOpticsStepCount { effect, value });
    }
    Ok(())
}

fn unit_interval(value: f32) -> bool {
    value.is_finite() && (0.0..=1.0).contains(&value)
}

fn positive_bounded(
    value: f32,
    maximum: f32,
    field: &'static str,
) -> Result<(), WaterDocumentError> {
    if !value.is_finite() || value <= 0.0 || value > maximum {
        return Err(WaterDocumentError::InvalidNumericValue(field));
    }
    Ok(())
}

fn bounded(
    value: f32,
    minimum: f32,
    maximum: f32,
    field: &'static str,
) -> Result<(), WaterDocumentError> {
    if !value.is_finite() || value < minimum || value > maximum {
        return Err(WaterDocumentError::InvalidNumericValue(field));
    }
    Ok(())
}

#[derive(Debug, Error)]
pub enum WaterDocumentError {
    #[error("invalid water schema `{0}`")]
    InvalidSchema(String),
    #[error("unsupported water schema version {0}")]
    UnsupportedVersion(u32),
    #[error("water revision must be greater than zero")]
    InvalidRevision,
    #[error("water coordinate system must be metres, Y-up, right-handed")]
    UnsupportedCoordinateSystem,
    #[error("water name cannot be empty")]
    EmptyName,
    #[error("water name is too long: {0} bytes")]
    NameTooLong(usize),
    #[error("water needs at least {MIN_RIVER_CONTROL_POINTS} river control points, found {0}")]
    TooFewRiverControlPoints(usize),
    #[error("river control-point limit exceeded for the selected path algorithm: {0}")]
    TooManyRiverControlPoints(usize),
    #[error("river control point {index} has an invalid `{field}`")]
    InvalidControlPoint { index: usize, field: &'static str },
    #[error("river control points {first_index} and {second_index} are too close horizontally")]
    ControlPointsTooClose {
        first_index: usize,
        second_index: usize,
    },
    #[error("river segment {first_index}..{second_index} rises above its upstream control point")]
    UphillRiverSegment {
        first_index: usize,
        second_index: usize,
    },
    #[error("river control-point path exceeds the supported length")]
    RiverPathTooLong,
    #[error("river sampling exceeds the supported limit of {maximum} samples")]
    TooManyRiverSamples { maximum: u32 },
    #[error("water has an invalid numeric value for `{0}`")]
    InvalidNumericValue(&'static str),
    #[error("water {effect} step count must be from 1 to 256, got {value}")]
    InvalidOpticsStepCount { effect: &'static str, value: u16 },
    #[error("water profile violates relation: {0}")]
    InvalidProfileRelation(&'static str),
    #[error("water JSON is {length} bytes, exceeding the limit of {maximum}")]
    JsonTooLarge { length: usize, maximum: usize },
    #[error("water project identity mismatch: expected {expected}, got {actual}")]
    ProjectIdentityMismatch {
        expected: ProjectId,
        actual: ProjectId,
    },
    #[error("water area identity mismatch: expected {expected}, got {actual}")]
    AreaIdentityMismatch { expected: AreaId, actual: AreaId },
    #[error("water revision mismatch: expected {expected:?}, got {actual:?}")]
    RevisionMismatch {
        expected: Revision,
        actual: Revision,
    },
    #[error("invalid water JSON: {0}")]
    Json(#[from] serde_json::Error),
    #[error("cannot hash water document: {0}")]
    Hash(#[from] CanonicalJsonError),
}

#[cfg(test)]
mod tests {
    use super::*;

    fn valid_document(seed: u64) -> WaterDocument {
        WaterDocument::new(
            ProjectId::deterministic("water-test-project"),
            AreaId::deterministic("water-test-area"),
            Revision::INITIAL,
            "Water contract test",
            seed,
            RiverProfile {
                algorithm: RiverPathAlgorithm::CentripetalCatmullRomV1,
                elevation_policy: RiverElevationPolicy::default(),
                sample_spacing_m: 2.0,
                control_points: vec![
                    RiverControlPoint {
                        center_m: Vec3::new(-30.0, 8.0, -24.0),
                        surface_width_m: 9.0,
                        bed_depth_m: 2.0,
                    },
                    RiverControlPoint {
                        center_m: Vec3::new(-10.0, 7.8, -8.0),
                        surface_width_m: 10.0,
                        bed_depth_m: 2.4,
                    },
                    RiverControlPoint {
                        center_m: Vec3::new(12.0, 7.5, 9.0),
                        surface_width_m: 11.0,
                        bed_depth_m: 2.8,
                    },
                    RiverControlPoint {
                        center_m: Vec3::new(34.0, 7.2, 28.0),
                        surface_width_m: 12.0,
                        bed_depth_m: 3.1,
                    },
                ],
            },
            WaterSurfaceProfile {
                shallow_color_srgb: WaterSrgbColor::new(0.12, 0.38, 0.42),
                deep_color_srgb: WaterSrgbColor::new(0.02, 0.12, 0.19),
                opacity: 0.82,
                roughness: 0.18,
                flow_speed_mps: 0.7,
                wave_amplitude_m: 0.08,
                wave_length_m: 4.0,
                normal_strength: 0.55,
            },
            RiverBankProfile {
                shoulder_width_m: 4.0,
                blend_width_m: 7.0,
                wet_margin_m: 1.5,
                horizontal_to_vertical_slope: 2.5,
                erosion_amplitude_m: 0.4,
                erosion_frequency_cycles_per_m: 0.035,
            },
            WaterOpticsProfile {
                quality: WaterQualityTier::High,
                reflection: WaterReflectionProfile::ScreenSpace {
                    intensity: 0.7,
                    max_steps: 48,
                    max_distance_m: 96.0,
                    thickness_m: 0.8,
                },
                refraction: WaterRefractionProfile::ScreenSpace {
                    strength: 0.16,
                    max_steps: 24,
                    max_depth_m: 3.0,
                },
                reflection_fallback: WaterReflectionFallback::EnvironmentThenDisabled,
                refraction_fallback: WaterRefractionFallback::SurfaceTint,
            },
        )
    }

    #[test]
    fn versioned_json_round_trip_and_hash_are_deterministic() {
        let document = valid_document(42);
        let json = document.to_pretty_json().unwrap();
        let restored = WaterDocument::from_json(&json).unwrap();

        assert_eq!(restored, document);
        assert_eq!(restored.to_pretty_json().unwrap(), json);
        assert_eq!(
            restored.content_hash().unwrap(),
            document.content_hash().unwrap()
        );
        assert_ne!(
            valid_document(42).content_hash().unwrap(),
            valid_document(43).content_hash().unwrap()
        );
    }

    #[test]
    fn optional_medium_round_trips_without_changing_legacy_serialization() {
        let mut document = valid_document(42);
        let legacy_json = document.to_pretty_json().unwrap();
        let legacy_hash = document.content_hash().unwrap();
        assert!(!legacy_json.contains("medium"));
        let legacy_value = serde_json::to_value(&document).unwrap();
        assert_eq!(canonical_json_sha256(&legacy_value).unwrap(), legacy_hash);
        document.medium = Some(WaterMediumProfile {
            absorption_per_m: [0.8, 0.2, 0.1],
            scattering_per_m: [0.1; 3],
        });
        let json = document.to_pretty_json().unwrap();
        assert_eq!(WaterDocument::from_json(&json).unwrap(), document);
        assert_ne!(document.content_hash().unwrap(), legacy_hash);
        assert!(WaterDocument::from_json(&json.replace("absorption_per_m", "unknown")).is_err());
        document.medium.as_mut().unwrap().scattering_per_m[1] = -1.0;
        assert!(document.validate().is_err());
        document.medium = None;
        assert_eq!(document.to_pretty_json().unwrap(), legacy_json);
        assert_eq!(document.content_hash().unwrap(), legacy_hash);
    }

    #[test]
    fn unknown_fields_and_non_finite_json_are_rejected() {
        let json = valid_document(42).to_pretty_json().unwrap();
        let unknown = json.replacen(
            "\"sample_spacing_m\": 2.0,",
            "\"sample_spacing_m\": 2.0,\n    \"engine_handle\": 7,",
            1,
        );
        assert!(matches!(
            WaterDocument::from_json(&unknown),
            Err(WaterDocumentError::Json(_))
        ));

        let non_finite = json.replacen("\"flow_speed_mps\": 0.7", "\"flow_speed_mps\": NaN", 1);
        assert!(matches!(
            WaterDocument::from_json(&non_finite),
            Err(WaterDocumentError::Json(_))
        ));

        let oversized = " ".repeat(MAX_WATER_DOCUMENT_JSON_BYTES + 1);
        assert!(matches!(
            WaterDocument::from_json(&oversized),
            Err(WaterDocumentError::JsonTooLarge { .. })
        ));

        let mut padded_name = valid_document(42);
        padded_name.name = format!("{}river", " ".repeat(MAX_NAME_LENGTH));
        assert!(matches!(
            padded_name.validate(),
            Err(WaterDocumentError::NameTooLong(_))
        ));
    }

    #[test]
    fn identity_and_geometry_bounds_are_strict() {
        let document = valid_document(42);
        document
            .validate_identity(document.project_id, document.area_id, document.revision)
            .unwrap();
        assert!(matches!(
            document.validate_identity(
                ProjectId::deterministic("other-project"),
                document.area_id,
                document.revision
            ),
            Err(WaterDocumentError::ProjectIdentityMismatch { .. })
        ));

        let mut too_short = valid_document(42);
        too_short.river.control_points.truncate(3);
        assert!(matches!(
            too_short.validate(),
            Err(WaterDocumentError::TooFewRiverControlPoints(3))
        ));

        let mut duplicate = valid_document(42);
        duplicate.river.control_points[1].center_m = duplicate.river.control_points[0].center_m;
        assert!(matches!(
            duplicate.validate(),
            Err(WaterDocumentError::ControlPointsTooClose { .. })
        ));

        let mut non_finite = valid_document(42);
        non_finite.river.control_points[0].center_m.y = f32::INFINITY;
        assert!(matches!(
            non_finite.validate(),
            Err(WaterDocumentError::InvalidControlPoint {
                index: 0,
                field: "center_m"
            })
        ));

        let mut excessive_sampling = valid_document(42);
        excessive_sampling.river.sample_spacing_m = 1.0e-30;
        assert!(matches!(
            excessive_sampling.validate(),
            Err(WaterDocumentError::TooManyRiverSamples {
                maximum: MAX_RIVER_SAMPLE_COUNT
            })
        ));
    }

    #[test]
    fn profile_relations_reject_unbounded_or_impossible_requests() {
        let mut document = valid_document(42);
        document.surface.wave_amplitude_m = 2.0;
        assert!(matches!(
            document.validate(),
            Err(WaterDocumentError::InvalidProfileRelation(_))
        ));

        document = valid_document(42);
        document.optics.reflection = WaterReflectionProfile::ScreenSpace {
            intensity: 0.7,
            max_steps: 0,
            max_distance_m: 96.0,
            thickness_m: 0.8,
        };
        assert!(matches!(
            document.validate(),
            Err(WaterDocumentError::InvalidOpticsStepCount {
                effect: "reflection",
                value: 0
            })
        ));

        document = valid_document(42);
        document.banks.wet_margin_m = 20.0;
        assert!(matches!(
            document.validate(),
            Err(WaterDocumentError::InvalidProfileRelation(_))
        ));
    }

    #[test]
    fn optical_fallback_resolution_is_explicit_and_measurable() {
        let document = valid_document(42);
        let capabilities = WaterOpticsCapabilities {
            screen_space_reflections: WaterOpticsCapabilityStatus::Available,
            screen_space_refraction: WaterOpticsCapabilityStatus::Available,
            environment_reflections: WaterOpticsCapabilityStatus::Available,
        };
        let full = document.resolve_optics_path(capabilities);
        assert_eq!(
            full,
            WaterOpticsResolution {
                requested_quality: WaterQualityTier::High,
                capabilities,
                reflection: ResolvedWaterReflectionPath::ScreenSpace { max_steps: 48 },
                refraction: ResolvedWaterRefractionPath::ScreenSpace { max_steps: 24 },
                reflection_fallback_used: false,
                refraction_fallback_used: false,
            }
        );

        let fallback = document.resolve_optics_path(WaterOpticsCapabilities {
            environment_reflections: WaterOpticsCapabilityStatus::Available,
            ..WaterOpticsCapabilities::default()
        });
        assert_eq!(
            fallback.reflection,
            ResolvedWaterReflectionPath::Environment
        );
        assert_eq!(
            fallback.refraction,
            ResolvedWaterRefractionPath::SurfaceTint
        );
        assert!(fallback.reflection_fallback_used);
        assert!(fallback.refraction_fallback_used);
        assert_eq!(
            fallback.capabilities.screen_space_reflections,
            WaterOpticsCapabilityStatus::NotQualified
        );

        let minimum = document.resolve_optics_path(WaterOpticsCapabilities::default());
        assert_eq!(minimum.reflection, ResolvedWaterReflectionPath::Disabled);
        assert_eq!(minimum.refraction, ResolvedWaterRefractionPath::SurfaceTint);
    }
}
