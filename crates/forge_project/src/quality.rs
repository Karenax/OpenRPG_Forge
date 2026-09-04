use forge_core::{
    AreaId, CanonicalJsonError, ContentHash, CoordinateSystem, ProjectId, Revision,
    canonical_json_sha256,
};
use serde::{Deserialize, Serialize};
use thiserror::Error;

pub const VISUAL_VALLEY_QUALITY_PROFILE_SCHEMA: &str = "forge.visual-valley-quality-profile";
pub const VISUAL_VALLEY_QUALITY_PROFILE_SCHEMA_VERSION: u32 = 1;
pub const MAX_VISUAL_VALLEY_QUALITY_PROFILE_JSON_BYTES: usize = 64 * 1024;

const MAX_NAME_LENGTH: usize = 128;
const MAX_PRESENTATION_DISTANCE_M: f32 = 8_192.0;
const MAX_TERRAIN_SAMPLE_STRIDE: u8 = 32;
const MAX_GRASS_BLADES_PER_PATCH: u16 = 64;
const MAX_SHADOW_CASCADES: u8 = 4;
const MAX_SHADOW_MAP_RESOLUTION: u16 = 8_192;
const MAX_ENVIRONMENT_MAP_RESOLUTION: u16 = 2_048;
const MAX_VOLUMETRIC_FOG_STEPS: u16 = 256;
const MAX_WATER_STEPS: u16 = 256;
const MAX_RAIN_DROP_BUDGET: u32 = 65_536;
const MIN_ADAPTATION_FRAME_TIME_MS: f32 = 4.0;
const MAX_ADAPTATION_FRAME_TIME_MS: f32 = 100.0;
const MAX_ADAPTATION_WINDOW_FRAMES: u16 = 3_600;

/// Versioned, renderer-neutral quality policy for the Visual Valley proof area.
///
/// This document describes future adapter inputs. Merely loading it does not apply dynamic quality
/// changes to a runtime, Studio viewport, or renderer.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct VisualValleyQualityProfileDocument {
    pub schema: String,
    pub schema_version: u32,
    pub project_id: ProjectId,
    pub area_id: AreaId,
    pub revision: Revision,
    pub coordinate_system: CoordinateSystem,
    pub name: String,
    pub default_tier: VisualQualityTier,
    pub presets: VisualQualityPresets,
    pub benchmark: FixedBenchmarkQualityProfile,
    pub adaptation: DynamicQualityAdaptationProfile,
}

impl VisualValleyQualityProfileDocument {
    #[must_use]
    #[allow(clippy::too_many_arguments)]
    pub fn new(
        project_id: ProjectId,
        area_id: AreaId,
        revision: Revision,
        name: impl Into<String>,
        default_tier: VisualQualityTier,
        presets: VisualQualityPresets,
        benchmark: FixedBenchmarkQualityProfile,
        adaptation: DynamicQualityAdaptationProfile,
    ) -> Self {
        Self {
            schema: VISUAL_VALLEY_QUALITY_PROFILE_SCHEMA.to_owned(),
            schema_version: VISUAL_VALLEY_QUALITY_PROFILE_SCHEMA_VERSION,
            project_id,
            area_id,
            revision,
            coordinate_system: CoordinateSystem::METRES_Y_UP_RIGHT_HANDED,
            name: name.into(),
            default_tier,
            presets,
            benchmark,
            adaptation,
        }
    }

    pub fn validate(&self) -> Result<(), VisualValleyQualityProfileError> {
        if self.schema != VISUAL_VALLEY_QUALITY_PROFILE_SCHEMA {
            return Err(VisualValleyQualityProfileError::InvalidSchema(
                self.schema.clone(),
            ));
        }
        if self.schema_version != VISUAL_VALLEY_QUALITY_PROFILE_SCHEMA_VERSION {
            return Err(VisualValleyQualityProfileError::UnsupportedVersion(
                self.schema_version,
            ));
        }
        if self.revision.get() == 0 {
            return Err(VisualValleyQualityProfileError::InvalidRevision);
        }
        if self.coordinate_system != CoordinateSystem::METRES_Y_UP_RIGHT_HANDED {
            return Err(VisualValleyQualityProfileError::UnsupportedCoordinateSystem);
        }
        if self.name.trim().is_empty() {
            return Err(VisualValleyQualityProfileError::EmptyName);
        }
        if self.name.len() > MAX_NAME_LENGTH {
            return Err(VisualValleyQualityProfileError::NameTooLong(
                self.name.len(),
            ));
        }

        self.presets.validate()?;
        self.benchmark.validate()?;
        self.adaptation.validate()?;
        if self.default_tier != self.adaptation.initial_tier {
            return Err(VisualValleyQualityProfileError::InvalidAdaptationRelation(
                "adaptation.initial_tier must match default_tier",
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
    ) -> Result<(), VisualValleyQualityProfileError> {
        if self.project_id != project_id {
            return Err(VisualValleyQualityProfileError::ProjectIdentityMismatch {
                expected: project_id,
                actual: self.project_id,
            });
        }
        if self.area_id != area_id {
            return Err(VisualValleyQualityProfileError::AreaIdentityMismatch {
                expected: area_id,
                actual: self.area_id,
            });
        }
        if self.revision != revision {
            return Err(VisualValleyQualityProfileError::RevisionMismatch {
                expected: revision,
                actual: self.revision,
            });
        }
        Ok(())
    }

    pub fn from_json(json: &str) -> Result<Self, VisualValleyQualityProfileError> {
        if json.len() > MAX_VISUAL_VALLEY_QUALITY_PROFILE_JSON_BYTES {
            return Err(VisualValleyQualityProfileError::JsonTooLarge {
                actual: json.len(),
                maximum: MAX_VISUAL_VALLEY_QUALITY_PROFILE_JSON_BYTES,
            });
        }
        let document: Self = serde_json::from_str(json)?;
        document.validate()?;
        Ok(document)
    }

    pub fn to_pretty_json(&self) -> Result<String, VisualValleyQualityProfileError> {
        self.validate()?;
        Ok(serde_json::to_string_pretty(self)?)
    }

    pub fn content_hash(&self) -> Result<ContentHash, VisualValleyQualityProfileError> {
        self.validate()?;
        Ok(canonical_json_sha256(self)?)
    }

    #[must_use]
    pub const fn preset(&self, tier: VisualQualityTier) -> &VisualQualityPreset {
        self.presets.get(tier)
    }
}

/// Stable quality tier ordering shared by the global preset table and adaptation bounds.
#[derive(Clone, Copy, Debug, Eq, Ord, PartialEq, PartialOrd, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum VisualQualityTier {
    Low,
    Medium,
    High,
    Ultra,
}

impl VisualQualityTier {
    const fn rank(self) -> u8 {
        match self {
            Self::Low => 0,
            Self::Medium => 1,
            Self::High => 2,
            Self::Ultra => 3,
        }
    }
}

/// Closed set of presets. Object fields prevent missing or duplicate tiers.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct VisualQualityPresets {
    pub low: VisualQualityPreset,
    pub medium: VisualQualityPreset,
    pub high: VisualQualityPreset,
    pub ultra: VisualQualityPreset,
}

impl VisualQualityPresets {
    #[must_use]
    pub const fn get(&self, tier: VisualQualityTier) -> &VisualQualityPreset {
        match tier {
            VisualQualityTier::Low => &self.low,
            VisualQualityTier::Medium => &self.medium,
            VisualQualityTier::High => &self.high,
            VisualQualityTier::Ultra => &self.ultra,
        }
    }

    fn ordered(&self) -> [(VisualQualityTier, &VisualQualityPreset); 4] {
        [
            (VisualQualityTier::Low, &self.low),
            (VisualQualityTier::Medium, &self.medium),
            (VisualQualityTier::High, &self.high),
            (VisualQualityTier::Ultra, &self.ultra),
        ]
    }

    fn validate(&self) -> Result<(), VisualValleyQualityProfileError> {
        for (tier, preset) in self.ordered() {
            preset.validate(tier)?;
        }

        let presets = [&self.low, &self.medium, &self.high, &self.ultra];
        Self::validate_terrain_progression(presets)?;
        Self::validate_vegetation_progression(presets)?;
        Self::validate_shadow_progression(presets)?;
        Self::validate_environment_progression(presets)?;
        Self::validate_water_progression(presets)?;
        Self::validate_weather_progression(presets)
    }

    fn validate_terrain_progression(
        presets: [&VisualQualityPreset; 4],
    ) -> Result<(), VisualValleyQualityProfileError> {
        non_decreasing_f32(
            "terrain.lod0_distance_m",
            presets.map(|preset| preset.terrain.lod0_distance_m),
        )?;
        non_decreasing_f32(
            "terrain.lod1_distance_m",
            presets.map(|preset| preset.terrain.lod1_distance_m),
        )?;
        non_decreasing_f32(
            "terrain.lod2_distance_m",
            presets.map(|preset| preset.terrain.lod2_distance_m),
        )?;
        non_decreasing_f32(
            "terrain.cull_distance_m",
            presets.map(|preset| preset.terrain.cull_distance_m),
        )?;
        non_increasing_u8(
            "terrain.lod1_sample_stride",
            presets.map(|preset| preset.terrain.lod1_sample_stride),
        )?;
        non_increasing_u8(
            "terrain.lod2_sample_stride",
            presets.map(|preset| preset.terrain.lod2_sample_stride),
        )
    }

    fn validate_vegetation_progression(
        presets: [&VisualQualityPreset; 4],
    ) -> Result<(), VisualValleyQualityProfileError> {
        non_decreasing_f32(
            "vegetation.density_multiplier",
            presets.map(|preset| preset.vegetation.density_multiplier),
        )?;
        non_decreasing_f32(
            "vegetation.near_lod_distance_m",
            presets.map(|preset| preset.vegetation.near_lod_distance_m),
        )?;
        non_decreasing_f32(
            "vegetation.medium_lod_distance_m",
            presets.map(|preset| preset.vegetation.medium_lod_distance_m),
        )?;
        non_decreasing_f32(
            "vegetation.hlod_distance_m",
            presets.map(|preset| preset.vegetation.hlod_distance_m),
        )?;
        non_decreasing_f32(
            "vegetation.cull_distance_m",
            presets.map(|preset| preset.vegetation.cull_distance_m),
        )?;
        non_decreasing_u16(
            "vegetation.grass_blades_per_patch",
            presets.map(|preset| preset.vegetation.grass_blades_per_patch),
        )
    }

    fn validate_shadow_progression(
        presets: [&VisualQualityPreset; 4],
    ) -> Result<(), VisualValleyQualityProfileError> {
        non_decreasing_bool(
            "shadows.enabled",
            presets.map(|preset| preset.shadows.enabled),
        )?;
        non_decreasing_u8(
            "shadows.cascade_count",
            presets.map(|preset| preset.shadows.cascade_count),
        )?;
        non_decreasing_u16(
            "shadows.map_resolution",
            presets.map(|preset| preset.shadows.map_resolution),
        )?;
        non_decreasing_f32(
            "shadows.maximum_distance_m",
            presets.map(|preset| preset.shadows.maximum_distance_m),
        )?;
        non_decreasing_bool(
            "shadows.contact_shadows_enabled",
            presets.map(|preset| preset.shadows.contact_shadows_enabled),
        )
    }

    fn validate_environment_progression(
        presets: [&VisualQualityPreset; 4],
    ) -> Result<(), VisualValleyQualityProfileError> {
        non_decreasing_u16(
            "environment.environment_map_resolution",
            presets.map(|preset| preset.environment.environment_map_resolution),
        )?;
        non_decreasing_bool(
            "environment.volumetric_fog_enabled",
            presets.map(|preset| preset.environment.volumetric_fog_enabled),
        )?;
        non_decreasing_u16(
            "environment.volumetric_fog_step_count",
            presets.map(|preset| preset.environment.volumetric_fog_step_count),
        )?;
        non_decreasing_f32(
            "environment.volumetric_fog_max_distance_m",
            presets.map(|preset| preset.environment.volumetric_fog_max_distance_m),
        )
    }

    fn validate_water_progression(
        presets: [&VisualQualityPreset; 4],
    ) -> Result<(), VisualValleyQualityProfileError> {
        non_decreasing_u8(
            "water.reflection",
            presets.map(|preset| preset.water.reflection.rank()),
        )?;
        non_decreasing_u16(
            "water.reflection_steps",
            presets.map(|preset| preset.water.reflection_steps),
        )?;
        non_decreasing_f32(
            "water.reflection_max_distance_m",
            presets.map(|preset| preset.water.reflection_max_distance_m),
        )?;
        non_decreasing_u8(
            "water.refraction",
            presets.map(|preset| preset.water.refraction.rank()),
        )?;
        non_decreasing_u16(
            "water.refraction_steps",
            presets.map(|preset| preset.water.refraction_steps),
        )?;
        non_decreasing_f32(
            "water.refraction_max_depth_m",
            presets.map(|preset| preset.water.refraction_max_depth_m),
        )
    }

    fn validate_weather_progression(
        presets: [&VisualQualityPreset; 4],
    ) -> Result<(), VisualValleyQualityProfileError> {
        non_decreasing_u32(
            "rain.drop_budget",
            presets.map(|preset| preset.rain.drop_budget),
        )?;
        non_decreasing_bool(
            "rain.wetness_enabled",
            presets.map(|preset| preset.rain.wetness_enabled),
        )?;
        non_decreasing_bool(
            "post_process.bloom_enabled",
            presets.map(|preset| preset.post_process.bloom_enabled),
        )?;
        non_decreasing_bool(
            "post_process.color_grading_enabled",
            presets.map(|preset| preset.post_process.color_grading_enabled),
        )?;
        non_decreasing_bool(
            "post_process.tonemapping_enabled",
            presets.map(|preset| preset.post_process.tonemapping_enabled),
        )
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct VisualQualityPreset {
    pub terrain: TerrainLodQualitySettings,
    pub vegetation: VegetationQualitySettings,
    pub shadows: ShadowQualitySettings,
    pub environment: EnvironmentQualitySettings,
    pub water: WaterQualitySettings,
    pub rain: RainQualitySettings,
    pub post_process: PostProcessQualitySettings,
}

impl VisualQualityPreset {
    fn validate(&self, tier: VisualQualityTier) -> Result<(), VisualValleyQualityProfileError> {
        self.terrain.validate(tier)?;
        self.vegetation.validate(tier)?;
        self.shadows.validate(tier)?;
        self.environment.validate(tier)?;
        self.water.validate(tier)?;
        self.rain.validate(tier)
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TerrainLodQualitySettings {
    pub lod0_distance_m: f32,
    pub lod1_distance_m: f32,
    pub lod2_distance_m: f32,
    pub cull_distance_m: f32,
    pub lod1_sample_stride: u8,
    pub lod2_sample_stride: u8,
}

impl TerrainLodQualitySettings {
    fn validate(self, tier: VisualQualityTier) -> Result<(), VisualValleyQualityProfileError> {
        for (field, value) in [
            ("terrain.lod0_distance_m", self.lod0_distance_m),
            ("terrain.lod1_distance_m", self.lod1_distance_m),
            ("terrain.lod2_distance_m", self.lod2_distance_m),
            ("terrain.cull_distance_m", self.cull_distance_m),
        ] {
            positive_bounded(tier, field, value, MAX_PRESENTATION_DISTANCE_M)?;
        }
        if !(self.lod0_distance_m < self.lod1_distance_m
            && self.lod1_distance_m < self.lod2_distance_m
            && self.lod2_distance_m < self.cull_distance_m)
        {
            return Err(VisualValleyQualityProfileError::InvalidPresetRelation {
                tier,
                relation: "terrain LOD and cull distances must be strictly increasing",
            });
        }
        if !valid_sample_stride(self.lod1_sample_stride)
            || !valid_sample_stride(self.lod2_sample_stride)
            || self.lod1_sample_stride >= self.lod2_sample_stride
        {
            return Err(VisualValleyQualityProfileError::InvalidPresetRelation {
                tier,
                relation: "terrain sample strides must be distinct powers of two in ascending LOD order",
            });
        }
        Ok(())
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct VegetationQualitySettings {
    pub density_multiplier: f32,
    pub near_lod_distance_m: f32,
    pub medium_lod_distance_m: f32,
    pub hlod_distance_m: f32,
    pub cull_distance_m: f32,
    pub grass_blades_per_patch: u16,
}

impl VegetationQualitySettings {
    fn validate(self, tier: VisualQualityTier) -> Result<(), VisualValleyQualityProfileError> {
        bounded(
            tier,
            "vegetation.density_multiplier",
            self.density_multiplier,
            0.1,
            2.0,
        )?;
        for (field, value) in [
            ("vegetation.near_lod_distance_m", self.near_lod_distance_m),
            (
                "vegetation.medium_lod_distance_m",
                self.medium_lod_distance_m,
            ),
            ("vegetation.hlod_distance_m", self.hlod_distance_m),
            ("vegetation.cull_distance_m", self.cull_distance_m),
        ] {
            positive_bounded(tier, field, value, MAX_PRESENTATION_DISTANCE_M)?;
        }
        if !(self.near_lod_distance_m < self.medium_lod_distance_m
            && self.medium_lod_distance_m < self.hlod_distance_m
            && self.hlod_distance_m < self.cull_distance_m)
        {
            return Err(VisualValleyQualityProfileError::InvalidPresetRelation {
                tier,
                relation: "vegetation LOD and cull distances must be strictly increasing",
            });
        }
        if !(1..=MAX_GRASS_BLADES_PER_PATCH).contains(&self.grass_blades_per_patch) {
            return Err(VisualValleyQualityProfileError::InvalidPresetInteger {
                tier,
                field: "vegetation.grass_blades_per_patch",
                value: u64::from(self.grass_blades_per_patch),
            });
        }
        Ok(())
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ShadowQualitySettings {
    pub enabled: bool,
    pub cascade_count: u8,
    pub map_resolution: u16,
    pub maximum_distance_m: f32,
    pub contact_shadows_enabled: bool,
}

impl ShadowQualitySettings {
    fn validate(self, tier: VisualQualityTier) -> Result<(), VisualValleyQualityProfileError> {
        if self.enabled {
            if !(1..=MAX_SHADOW_CASCADES).contains(&self.cascade_count) {
                return Err(VisualValleyQualityProfileError::InvalidPresetInteger {
                    tier,
                    field: "shadows.cascade_count",
                    value: u64::from(self.cascade_count),
                });
            }
            if !power_of_two_in_range(self.map_resolution, 512, MAX_SHADOW_MAP_RESOLUTION) {
                return Err(VisualValleyQualityProfileError::InvalidPresetInteger {
                    tier,
                    field: "shadows.map_resolution",
                    value: u64::from(self.map_resolution),
                });
            }
            positive_bounded(
                tier,
                "shadows.maximum_distance_m",
                self.maximum_distance_m,
                MAX_PRESENTATION_DISTANCE_M,
            )?;
        } else if self.cascade_count != 0
            || self.map_resolution != 0
            || self.maximum_distance_m.to_bits() != 0.0_f32.to_bits()
            || self.contact_shadows_enabled
        {
            return Err(VisualValleyQualityProfileError::InvalidPresetRelation {
                tier,
                relation: "disabled shadows require zero budgets and disabled contact shadows",
            });
        }
        Ok(())
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct EnvironmentQualitySettings {
    pub atmosphere_enabled: bool,
    pub environment_map_resolution: u16,
    pub volumetric_fog_enabled: bool,
    pub volumetric_fog_step_count: u16,
    pub volumetric_fog_max_distance_m: f32,
}

impl EnvironmentQualitySettings {
    fn validate(self, tier: VisualQualityTier) -> Result<(), VisualValleyQualityProfileError> {
        if self.atmosphere_enabled {
            if !power_of_two_in_range(
                self.environment_map_resolution,
                16,
                MAX_ENVIRONMENT_MAP_RESOLUTION,
            ) {
                return Err(VisualValleyQualityProfileError::InvalidPresetInteger {
                    tier,
                    field: "environment.environment_map_resolution",
                    value: u64::from(self.environment_map_resolution),
                });
            }
        } else if self.environment_map_resolution != 0 {
            return Err(VisualValleyQualityProfileError::InvalidPresetRelation {
                tier,
                relation: "disabled atmosphere requires a zero environment-map resolution",
            });
        }

        if self.volumetric_fog_enabled {
            if !(1..=MAX_VOLUMETRIC_FOG_STEPS).contains(&self.volumetric_fog_step_count) {
                return Err(VisualValleyQualityProfileError::InvalidPresetInteger {
                    tier,
                    field: "environment.volumetric_fog_step_count",
                    value: u64::from(self.volumetric_fog_step_count),
                });
            }
            positive_bounded(
                tier,
                "environment.volumetric_fog_max_distance_m",
                self.volumetric_fog_max_distance_m,
                MAX_PRESENTATION_DISTANCE_M,
            )?;
        } else if self.volumetric_fog_step_count != 0
            || self.volumetric_fog_max_distance_m.to_bits() != 0.0_f32.to_bits()
        {
            return Err(VisualValleyQualityProfileError::InvalidPresetRelation {
                tier,
                relation: "disabled volumetric fog requires zero work and distance budgets",
            });
        }
        Ok(())
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum QualityWaterReflection {
    Disabled,
    Environment,
    ScreenSpace,
}

impl QualityWaterReflection {
    const fn rank(self) -> u8 {
        match self {
            Self::Disabled => 0,
            Self::Environment => 1,
            Self::ScreenSpace => 2,
        }
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum QualityWaterRefraction {
    Disabled,
    SurfaceTint,
    ScreenSpace,
}

impl QualityWaterRefraction {
    const fn rank(self) -> u8 {
        match self {
            Self::Disabled => 0,
            Self::SurfaceTint => 1,
            Self::ScreenSpace => 2,
        }
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WaterQualitySettings {
    pub reflection: QualityWaterReflection,
    pub reflection_steps: u16,
    pub reflection_max_distance_m: f32,
    pub refraction: QualityWaterRefraction,
    pub refraction_steps: u16,
    pub refraction_max_depth_m: f32,
}

impl WaterQualitySettings {
    fn validate(self, tier: VisualQualityTier) -> Result<(), VisualValleyQualityProfileError> {
        match self.reflection {
            QualityWaterReflection::ScreenSpace => {
                validate_effect_steps(tier, "water.reflection_steps", self.reflection_steps)?;
                positive_bounded(
                    tier,
                    "water.reflection_max_distance_m",
                    self.reflection_max_distance_m,
                    MAX_PRESENTATION_DISTANCE_M,
                )?;
            }
            QualityWaterReflection::Disabled | QualityWaterReflection::Environment => {
                if self.reflection_steps != 0
                    || self.reflection_max_distance_m.to_bits() != 0.0_f32.to_bits()
                {
                    return Err(VisualValleyQualityProfileError::InvalidPresetRelation {
                        tier,
                        relation: "non-screen-space water reflection requires zero ray-march budgets",
                    });
                }
            }
        }

        match self.refraction {
            QualityWaterRefraction::ScreenSpace => {
                validate_effect_steps(tier, "water.refraction_steps", self.refraction_steps)?;
                positive_bounded(
                    tier,
                    "water.refraction_max_depth_m",
                    self.refraction_max_depth_m,
                    64.0,
                )?;
            }
            QualityWaterRefraction::Disabled | QualityWaterRefraction::SurfaceTint => {
                if self.refraction_steps != 0
                    || self.refraction_max_depth_m.to_bits() != 0.0_f32.to_bits()
                {
                    return Err(VisualValleyQualityProfileError::InvalidPresetRelation {
                        tier,
                        relation: "non-screen-space water refraction requires zero ray-march budgets",
                    });
                }
            }
        }
        Ok(())
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct RainQualitySettings {
    pub enabled: bool,
    pub drop_budget: u32,
    pub wetness_enabled: bool,
}

impl RainQualitySettings {
    fn validate(self, tier: VisualQualityTier) -> Result<(), VisualValleyQualityProfileError> {
        if self.enabled {
            if !(1..=MAX_RAIN_DROP_BUDGET).contains(&self.drop_budget) {
                return Err(VisualValleyQualityProfileError::InvalidPresetInteger {
                    tier,
                    field: "rain.drop_budget",
                    value: u64::from(self.drop_budget),
                });
            }
        } else if self.drop_budget != 0 || self.wetness_enabled {
            return Err(VisualValleyQualityProfileError::InvalidPresetRelation {
                tier,
                relation: "disabled rain requires a zero drop budget and disabled wetness",
            });
        }
        Ok(())
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PostProcessQualitySettings {
    pub bloom_enabled: bool,
    pub color_grading_enabled: bool,
    pub tonemapping_enabled: bool,
}

/// Gate 1 comparison mode. Its fixed High tier and disabled adaptation are invariants, not defaults.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct FixedBenchmarkQualityProfile {
    pub fixed_tier: VisualQualityTier,
    pub adaptation_enabled: bool,
}

impl FixedBenchmarkQualityProfile {
    fn validate(self) -> Result<(), VisualValleyQualityProfileError> {
        if self.fixed_tier != VisualQualityTier::High {
            return Err(VisualValleyQualityProfileError::BenchmarkMustUseHigh);
        }
        if self.adaptation_enabled {
            return Err(VisualValleyQualityProfileError::BenchmarkAdaptationEnabled);
        }
        Ok(())
    }
}

/// Bounded policy inputs for a future adapter. No runtime schedule consumes this type yet.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct DynamicQualityAdaptationProfile {
    pub policy_enabled: bool,
    pub initial_tier: VisualQualityTier,
    pub minimum_tier: VisualQualityTier,
    pub maximum_tier: VisualQualityTier,
    pub target_frame_time_ms: f32,
    pub upgrade_below_frame_time_ms: f32,
    pub downgrade_above_frame_time_ms: f32,
    pub upgrade_sustain_frames: u16,
    pub downgrade_sustain_frames: u16,
    pub cooldown_frames: u16,
    pub maximum_tier_step: u8,
}

impl DynamicQualityAdaptationProfile {
    fn validate(self) -> Result<(), VisualValleyQualityProfileError> {
        if self.minimum_tier > self.maximum_tier
            || self.initial_tier < self.minimum_tier
            || self.initial_tier > self.maximum_tier
        {
            return Err(VisualValleyQualityProfileError::InvalidAdaptationRelation(
                "minimum_tier <= initial_tier <= maximum_tier is required",
            ));
        }
        for (field, value) in [
            ("target_frame_time_ms", self.target_frame_time_ms),
            (
                "upgrade_below_frame_time_ms",
                self.upgrade_below_frame_time_ms,
            ),
            (
                "downgrade_above_frame_time_ms",
                self.downgrade_above_frame_time_ms,
            ),
        ] {
            if !value.is_finite()
                || !(MIN_ADAPTATION_FRAME_TIME_MS..=MAX_ADAPTATION_FRAME_TIME_MS).contains(&value)
            {
                return Err(VisualValleyQualityProfileError::InvalidAdaptationNumericValue(field));
            }
        }
        if !(self.upgrade_below_frame_time_ms < self.target_frame_time_ms
            && self.target_frame_time_ms < self.downgrade_above_frame_time_ms)
        {
            return Err(VisualValleyQualityProfileError::InvalidAdaptationRelation(
                "upgrade threshold must be below target and downgrade threshold above target",
            ));
        }
        if !(1..=MAX_ADAPTATION_WINDOW_FRAMES).contains(&self.upgrade_sustain_frames)
            || !(1..=MAX_ADAPTATION_WINDOW_FRAMES).contains(&self.downgrade_sustain_frames)
            || !(1..=MAX_ADAPTATION_WINDOW_FRAMES).contains(&self.cooldown_frames)
        {
            return Err(VisualValleyQualityProfileError::InvalidAdaptationRelation(
                "adaptation sustain and cooldown windows must be in 1..=3600 frames",
            ));
        }
        if self.downgrade_sustain_frames > self.upgrade_sustain_frames {
            return Err(VisualValleyQualityProfileError::InvalidAdaptationRelation(
                "downgrade response cannot require more sustained frames than upgrade",
            ));
        }
        let tier_span = self.maximum_tier.rank() - self.minimum_tier.rank();
        if self.maximum_tier_step == 0
            || self.maximum_tier_step > 3
            || self.maximum_tier_step > tier_span
        {
            return Err(VisualValleyQualityProfileError::InvalidAdaptationRelation(
                "maximum_tier_step must fit the bounded quality range",
            ));
        }
        Ok(())
    }
}

fn valid_sample_stride(value: u8) -> bool {
    (2..=MAX_TERRAIN_SAMPLE_STRIDE).contains(&value) && value.is_power_of_two()
}

fn power_of_two_in_range(value: u16, minimum: u16, maximum: u16) -> bool {
    (minimum..=maximum).contains(&value) && value.is_power_of_two()
}

fn validate_effect_steps(
    tier: VisualQualityTier,
    field: &'static str,
    value: u16,
) -> Result<(), VisualValleyQualityProfileError> {
    if !(1..=MAX_WATER_STEPS).contains(&value) {
        return Err(VisualValleyQualityProfileError::InvalidPresetInteger {
            tier,
            field,
            value: u64::from(value),
        });
    }
    Ok(())
}

fn positive_bounded(
    tier: VisualQualityTier,
    field: &'static str,
    value: f32,
    maximum: f32,
) -> Result<(), VisualValleyQualityProfileError> {
    bounded(tier, field, value, f32::MIN_POSITIVE, maximum)
}

fn bounded(
    tier: VisualQualityTier,
    field: &'static str,
    value: f32,
    minimum: f32,
    maximum: f32,
) -> Result<(), VisualValleyQualityProfileError> {
    if !value.is_finite() || !(minimum..=maximum).contains(&value) {
        return Err(VisualValleyQualityProfileError::InvalidPresetNumeric { tier, field });
    }
    Ok(())
}

fn non_decreasing_f32(
    field: &'static str,
    values: [f32; 4],
) -> Result<(), VisualValleyQualityProfileError> {
    if values.windows(2).all(|pair| pair[0] <= pair[1]) {
        Ok(())
    } else {
        Err(VisualValleyQualityProfileError::NonMonotonicPresets(field))
    }
}

fn non_decreasing_bool(
    field: &'static str,
    values: [bool; 4],
) -> Result<(), VisualValleyQualityProfileError> {
    non_decreasing_u8(field, values.map(u8::from))
}

fn non_decreasing_u8(
    field: &'static str,
    values: [u8; 4],
) -> Result<(), VisualValleyQualityProfileError> {
    if values.windows(2).all(|pair| pair[0] <= pair[1]) {
        Ok(())
    } else {
        Err(VisualValleyQualityProfileError::NonMonotonicPresets(field))
    }
}

fn non_increasing_u8(
    field: &'static str,
    values: [u8; 4],
) -> Result<(), VisualValleyQualityProfileError> {
    if values.windows(2).all(|pair| pair[0] >= pair[1]) {
        Ok(())
    } else {
        Err(VisualValleyQualityProfileError::NonMonotonicPresets(field))
    }
}

fn non_decreasing_u16(
    field: &'static str,
    values: [u16; 4],
) -> Result<(), VisualValleyQualityProfileError> {
    if values.windows(2).all(|pair| pair[0] <= pair[1]) {
        Ok(())
    } else {
        Err(VisualValleyQualityProfileError::NonMonotonicPresets(field))
    }
}

fn non_decreasing_u32(
    field: &'static str,
    values: [u32; 4],
) -> Result<(), VisualValleyQualityProfileError> {
    if values.windows(2).all(|pair| pair[0] <= pair[1]) {
        Ok(())
    } else {
        Err(VisualValleyQualityProfileError::NonMonotonicPresets(field))
    }
}

#[derive(Debug, Error)]
pub enum VisualValleyQualityProfileError {
    #[error("invalid Visual Valley quality-profile schema `{0}`")]
    InvalidSchema(String),
    #[error("unsupported Visual Valley quality-profile schema version {0}")]
    UnsupportedVersion(u32),
    #[error("Visual Valley quality-profile revision must be non-zero")]
    InvalidRevision,
    #[error("Visual Valley quality-profile coordinate system must be metres, Y-up, right-handed")]
    UnsupportedCoordinateSystem,
    #[error("Visual Valley quality-profile name cannot be empty")]
    EmptyName,
    #[error("Visual Valley quality-profile name exceeds 128 bytes: {0}")]
    NameTooLong(usize),
    #[error("quality preset {tier:?} has invalid numeric field `{field}`")]
    InvalidPresetNumeric {
        tier: VisualQualityTier,
        field: &'static str,
    },
    #[error("quality preset {tier:?} has invalid integer field `{field}`: {value}")]
    InvalidPresetInteger {
        tier: VisualQualityTier,
        field: &'static str,
        value: u64,
    },
    #[error("quality preset {tier:?} has invalid relation: {relation}")]
    InvalidPresetRelation {
        tier: VisualQualityTier,
        relation: &'static str,
    },
    #[error("quality presets are not monotonic for `{0}`")]
    NonMonotonicPresets(&'static str),
    #[error("benchmark quality tier must be fixed to High")]
    BenchmarkMustUseHigh,
    #[error("benchmark quality adaptation must be explicitly disabled")]
    BenchmarkAdaptationEnabled,
    #[error("invalid adaptation numeric field `{0}`")]
    InvalidAdaptationNumericValue(&'static str),
    #[error("invalid adaptation relation: {0}")]
    InvalidAdaptationRelation(&'static str),
    #[error("quality-profile project identity mismatch: expected {expected}, found {actual}")]
    ProjectIdentityMismatch {
        expected: ProjectId,
        actual: ProjectId,
    },
    #[error("quality-profile area identity mismatch: expected {expected}, found {actual}")]
    AreaIdentityMismatch { expected: AreaId, actual: AreaId },
    #[error("quality-profile revision mismatch: expected {expected:?}, found {actual:?}")]
    RevisionMismatch {
        expected: Revision,
        actual: Revision,
    },
    #[error("Visual Valley quality-profile JSON contains {actual} bytes, exceeding {maximum}")]
    JsonTooLarge { actual: usize, maximum: usize },
    #[error("invalid Visual Valley quality-profile JSON: {0}")]
    Json(#[from] serde_json::Error),
    #[error("cannot hash Visual Valley quality profile: {0}")]
    Hash(#[from] CanonicalJsonError),
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::{
        EnvironmentProfileDocument, TerrainDocument, VegetationDocument, WaterDocument,
        WaterReflectionProfile, WaterRefractionProfile, WeatherProfileDocument,
    };

    const VISUAL_VALLEY_QUALITY_JSON: &str =
        include_str!("../../../examples/visual_valley/quality-profile.v1.json");
    const VISUAL_VALLEY_TERRAIN_JSON: &str =
        include_str!("../../../examples/visual_valley/terrain.v1.json");
    const VISUAL_VALLEY_VEGETATION_JSON: &str =
        include_str!("../../../examples/visual_valley/vegetation.v1.json");
    const VISUAL_VALLEY_ENVIRONMENT_JSON: &str =
        include_str!("../../../examples/visual_valley/environment.v1.json");
    const VISUAL_VALLEY_WATER_JSON: &str =
        include_str!("../../../examples/visual_valley/water.v1.json");
    const VISUAL_VALLEY_WEATHER_JSON: &str =
        include_str!("../../../examples/visual_valley/weather.v1.json");

    fn valid_document() -> VisualValleyQualityProfileDocument {
        VisualValleyQualityProfileDocument::from_json(VISUAL_VALLEY_QUALITY_JSON).unwrap()
    }

    #[test]
    fn visual_valley_fixture_round_trips_and_has_a_golden_hash() {
        let document = valid_document();
        let pretty = document.to_pretty_json().unwrap();
        let restored = VisualValleyQualityProfileDocument::from_json(&pretty).unwrap();

        assert_eq!(restored, document);
        assert_eq!(
            restored.content_hash().unwrap(),
            document.content_hash().unwrap()
        );
        assert_eq!(
            document.content_hash().unwrap().to_string(),
            "0c4573220cc8c314e58938f617fbd7a46bdac4037a599b9a73c3be61aeb699cb"
        );
    }

    fn assert_shared_identity(
        document: &VisualValleyQualityProfileDocument,
        identities: [(ProjectId, AreaId, Revision); 5],
    ) {
        for (project_id, area_id, revision) in identities {
            document
                .validate_identity(project_id, area_id, revision)
                .unwrap();
        }
    }

    fn assert_high_vegetation_and_environment(
        high: &VisualQualityPreset,
        vegetation: &VegetationDocument,
        environment: &EnvironmentProfileDocument,
    ) {
        for (actual, expected) in [
            (
                high.vegetation.near_lod_distance_m,
                vegetation.presentation.near_lod_distance_m,
            ),
            (
                high.vegetation.medium_lod_distance_m,
                vegetation.presentation.medium_lod_distance_m,
            ),
            (
                high.vegetation.hlod_distance_m,
                vegetation.presentation.hlod_distance_m,
            ),
            (
                high.vegetation.cull_distance_m,
                vegetation.presentation.cull_distance_m,
            ),
            (
                high.shadows.maximum_distance_m,
                environment.sun.shadow_distance_m,
            ),
            (
                high.shadows.maximum_distance_m,
                vegetation.presentation.shadow_distance_m,
            ),
            (
                high.environment.volumetric_fog_max_distance_m,
                environment.volumetric_fog.maximum_distance_m,
            ),
        ] {
            assert_eq!(actual.to_bits(), expected.to_bits());
        }
        assert_eq!(
            high.vegetation.grass_blades_per_patch,
            vegetation.presentation.grass_blades_per_patch
        );
        assert_eq!(
            high.environment.environment_map_resolution,
            environment.sky.environment_resolution
        );
        assert_eq!(
            high.environment.volumetric_fog_step_count,
            environment.volumetric_fog.step_count
        );
    }

    fn assert_high_water_and_weather(
        high: &VisualQualityPreset,
        water: &WaterDocument,
        weather: &WeatherProfileDocument,
    ) {
        let WaterReflectionProfile::ScreenSpace {
            max_steps,
            max_distance_m,
            ..
        } = water.optics.reflection
        else {
            panic!("Visual Valley High water must request screen-space reflection");
        };
        assert_eq!(high.water.reflection, QualityWaterReflection::ScreenSpace);
        assert_eq!(high.water.reflection_steps, max_steps);
        assert_eq!(
            high.water.reflection_max_distance_m.to_bits(),
            max_distance_m.to_bits()
        );

        let WaterRefractionProfile::ScreenSpace {
            max_steps,
            max_depth_m,
            ..
        } = water.optics.refraction
        else {
            panic!("Visual Valley High water must request screen-space refraction");
        };
        assert_eq!(high.water.refraction, QualityWaterRefraction::ScreenSpace);
        assert_eq!(high.water.refraction_steps, max_steps);
        assert_eq!(
            high.water.refraction_max_depth_m.to_bits(),
            max_depth_m.to_bits()
        );
        assert_eq!(
            high.rain.drop_budget,
            weather.rain.drop_budgets.for_quality(weather.rain.quality)
        );
        assert_eq!(
            high.post_process.bloom_enabled,
            weather.post_processing.bloom.enabled
        );
        assert!(high.post_process.color_grading_enabled);
    }

    #[test]
    fn high_preset_matches_the_existing_visual_valley_authored_budgets() {
        let document = valid_document();
        let terrain = TerrainDocument::from_json(VISUAL_VALLEY_TERRAIN_JSON).unwrap();
        let vegetation = VegetationDocument::from_json(VISUAL_VALLEY_VEGETATION_JSON).unwrap();
        let environment =
            EnvironmentProfileDocument::from_json(VISUAL_VALLEY_ENVIRONMENT_JSON).unwrap();
        let water = WaterDocument::from_json(VISUAL_VALLEY_WATER_JSON).unwrap();
        let weather = WeatherProfileDocument::from_json(VISUAL_VALLEY_WEATHER_JSON).unwrap();

        assert_shared_identity(
            &document,
            [
                (terrain.project_id, terrain.area_id, terrain.revision),
                (
                    vegetation.project_id,
                    vegetation.area_id,
                    vegetation.revision,
                ),
                (
                    environment.project_id,
                    environment.area_id,
                    environment.revision,
                ),
                (water.project_id, water.area_id, water.revision),
                (weather.project_id, weather.area_id, weather.revision),
            ],
        );
        let high = document.preset(VisualQualityTier::High);
        assert_high_vegetation_and_environment(high, &vegetation, &environment);
        assert_high_water_and_weather(high, &water, &weather);
    }

    #[test]
    fn benchmark_is_strictly_high_and_non_adaptive() {
        let mut document = valid_document();
        document.benchmark.fixed_tier = VisualQualityTier::Ultra;
        assert!(matches!(
            document.validate(),
            Err(VisualValleyQualityProfileError::BenchmarkMustUseHigh)
        ));

        let mut document = valid_document();
        document.benchmark.adaptation_enabled = true;
        assert!(matches!(
            document.validate(),
            Err(VisualValleyQualityProfileError::BenchmarkAdaptationEnabled)
        ));
    }

    #[test]
    fn adaptation_hysteresis_and_tier_bounds_are_enforced() {
        let mut document = valid_document();
        document.adaptation.upgrade_below_frame_time_ms = document.adaptation.target_frame_time_ms;
        assert!(matches!(
            document.validate(),
            Err(VisualValleyQualityProfileError::InvalidAdaptationRelation(
                _
            ))
        ));

        let mut document = valid_document();
        document.adaptation.minimum_tier = VisualQualityTier::Ultra;
        assert!(matches!(
            document.validate(),
            Err(VisualValleyQualityProfileError::InvalidAdaptationRelation(
                _
            ))
        ));

        let mut document = valid_document();
        document.adaptation.target_frame_time_ms = f32::NAN;
        assert!(matches!(
            document.validate(),
            Err(
                VisualValleyQualityProfileError::InvalidAdaptationNumericValue(
                    "target_frame_time_ms"
                )
            )
        ));
    }

    #[test]
    fn preset_relations_and_monotonic_quality_are_enforced() {
        let mut document = valid_document();
        document.presets.high.terrain.lod1_distance_m =
            document.presets.high.terrain.lod0_distance_m;
        assert!(matches!(
            document.validate(),
            Err(VisualValleyQualityProfileError::InvalidPresetRelation {
                tier: VisualQualityTier::High,
                ..
            })
        ));

        let mut document = valid_document();
        document.presets.ultra.rain.drop_budget = document.presets.medium.rain.drop_budget;
        assert!(matches!(
            document.validate(),
            Err(VisualValleyQualityProfileError::NonMonotonicPresets(
                "rain.drop_budget"
            ))
        ));
    }

    #[test]
    fn unknown_fields_non_finite_values_and_oversized_json_are_rejected() {
        let unknown = VISUAL_VALLEY_QUALITY_JSON.replacen(
            "\"default_tier\": \"high\",",
            "\"default_tier\": \"high\",\n  \"gpu_handle\": 7,",
            1,
        );
        assert!(matches!(
            VisualValleyQualityProfileDocument::from_json(&unknown),
            Err(VisualValleyQualityProfileError::Json(_))
        ));

        let non_finite = VISUAL_VALLEY_QUALITY_JSON.replacen(
            "\"density_multiplier\": 1.0",
            "\"density_multiplier\": NaN",
            1,
        );
        assert!(matches!(
            VisualValleyQualityProfileDocument::from_json(&non_finite),
            Err(VisualValleyQualityProfileError::Json(_))
        ));

        let oversized = " ".repeat(MAX_VISUAL_VALLEY_QUALITY_PROFILE_JSON_BYTES + 1);
        assert!(matches!(
            VisualValleyQualityProfileDocument::from_json(&oversized),
            Err(VisualValleyQualityProfileError::JsonTooLarge { .. })
        ));
    }

    #[test]
    fn area_identity_and_revision_are_checked_without_renderer_types() {
        let document = valid_document();
        assert!(
            document
                .validate_identity(document.project_id, document.area_id, document.revision)
                .is_ok()
        );
        assert!(matches!(
            document.validate_identity(
                ProjectId::deterministic("another-project"),
                document.area_id,
                document.revision,
            ),
            Err(VisualValleyQualityProfileError::ProjectIdentityMismatch { .. })
        ));
        assert!(matches!(
            document.validate_identity(
                document.project_id,
                AreaId::deterministic("another-area"),
                document.revision,
            ),
            Err(VisualValleyQualityProfileError::AreaIdentityMismatch { .. })
        ));
        assert!(matches!(
            document.validate_identity(
                document.project_id,
                document.area_id,
                Revision::new(document.revision.get() + 1),
            ),
            Err(VisualValleyQualityProfileError::RevisionMismatch { .. })
        ));
    }
}
