//! Derived quality plans under ADR-0011. Resolution never acknowledges renderer application.
use crate::{
    EnvironmentQualitySettings, PostProcessQualitySettings, QualityWaterReflection,
    QualityWaterRefraction, RainQualitySettings, ShadowQualitySettings, TerrainLodQualitySettings,
    VegetationQualitySettings, VisualQualityTier, VisualValleyQualityProfileDocument,
    VisualValleyQualityProfileError,
};
use forge_core::{CanonicalJsonError, ContentHash, EntityId, canonical_json_sha256};
use serde::{Deserialize, Serialize};
use std::collections::{BTreeMap, BTreeSet};
use thiserror::Error;

#[derive(Clone, Copy, Debug, Eq, PartialEq, Ord, PartialOrd, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum QualityCapability {
    Terrain,
    Vegetation,
    Shadows,
    Sky,
    Rain,
    PostProcess,
    EnvironmentReflection,
    /// Mapping of the authored water reflection gain, distinct from fixed reflectance.
    ReflectionIntensity,
    ScreenReflection,
    ReflectionDistance,
    SurfaceTint,
    ScreenRefraction,
    VolumetricFog,
    DistanceFog,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum CapabilityStatus {
    Supported,
    Unavailable,
    Unqualified,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct QualityCapabilities {
    pub schema_version: u32,
    pub adapter_id: String,
    pub generation: u64,
    /// Omission means unqualified, never implicit support.
    pub capabilities: BTreeMap<QualityCapability, CapabilityStatus>,
    #[serde(default)]
    pub terrain_constraints: Option<TerrainQualityConstraints>,
    #[serde(default)]
    pub source_bindings: Option<QualitySourceBindings>,
}

/// Source documents used by the seven domains, in addition to the quality policy hash.
#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct QualitySourceBindings {
    pub terrain: ContentHash,
    pub vegetation: ContentHash,
    pub environment: ContentHash,
    pub water: ContentHash,
    pub weather: ContentHash,
}

/// Observed derived exceptions, not permission to relax the requested terrain budget.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TerrainQualityConstraints {
    pub terrain_hash: ContentHash,
    pub canonical_chunks: u32,
    /// Strictly sorted stable IDs. These chunks retain stride 1 at every LOD.
    pub full_resolution_chunks: Vec<EntityId>,
}

impl TerrainQualityConstraints {
    fn valid(&self) -> bool {
        self.canonical_chunks > 0
            && !self.full_resolution_chunks.is_empty()
            && self.full_resolution_chunks.len() <= self.canonical_chunks as usize
            && self
                .full_resolution_chunks
                .windows(2)
                .all(|pair| pair[0] < pair[1])
    }
}

impl QualityCapabilities {
    fn validate_identity(&self) -> Result<(), QualityResolutionError> {
        if self
            .source_bindings
            .zip(self.terrain_constraints.as_ref())
            .is_some_and(|(sources, constraints)| sources.terrain != constraints.terrain_hash)
        {
            return Err(QualityResolutionError::InvalidTerrainConstraints);
        }
        if self
            .terrain_constraints
            .as_ref()
            .is_some_and(|constraints| !constraints.valid())
        {
            return Err(QualityResolutionError::InvalidTerrainConstraints);
        }
        if self.schema_version != 1
            || self.generation == 0
            || self.adapter_id.is_empty()
            || self.adapter_id.len() > 96
            || !self
                .adapter_id
                .bytes()
                .all(|c| c.is_ascii_alphanumeric() || b"-_.".contains(&c))
        {
            return Err(QualityResolutionError::InvalidCapabilities);
        }
        Ok(())
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum QualityResolutionPolicy {
    Strict,
    Adr0011,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum QualitySubstitution {
    ScreenRefractionToSurfaceTint,
    VolumetricToAuthoredDistanceFog,
    ReflectionDistanceNotGuaranteed,
    /// Authored gain remains in its source but is not applied by the candidate adapter.
    ReflectionIntensityNotApplied,
    /// Geometrically constrained bank chunks retain stride 1 at all terrain LODs.
    TerrainBanksFullResolution,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct EffectiveWaterSettings {
    pub reflection: QualityWaterReflection,
    pub reflection_steps: u16,
    /// None explicitly means no guaranteed metric ceiling, not a zero-length ray.
    pub reflection_max_distance_m: Option<f32>,
    pub refraction: QualityWaterRefraction,
    pub refraction_steps: u16,
    pub refraction_max_depth_m: f32,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct EffectiveQualitySettings {
    pub terrain: TerrainLodQualitySettings,
    pub vegetation: VegetationQualitySettings,
    pub shadows: ShadowQualitySettings,
    pub environment: EnvironmentQualitySettings,
    pub water: EffectiveWaterSettings,
    pub rain: RainQualitySettings,
    pub post_process: PostProcessQualitySettings,
}

/// A candidate plan. Empty blockers means resolvable, not installed or GPU-ready.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct EffectiveQualityPlan {
    pub schema_version: u32,
    pub requested_policy_hash: ContentHash,
    pub requested_tier: VisualQualityTier,
    pub capability_hash: ContentHash,
    pub adapter_generation: u64,
    pub resolution_policy: QualityResolutionPolicy,
    pub effective: EffectiveQualitySettings,
    pub substitutions: Vec<QualitySubstitution>,
    pub blockers: Vec<QualityCapability>,
    #[serde(default)]
    pub terrain_constraints: Option<TerrainQualityConstraints>,
    #[serde(default)]
    pub source_bindings: Option<QualitySourceBindings>,
}

#[derive(Debug, Error)]
pub enum QualityResolutionError {
    #[error(transparent)]
    Profile(#[from] VisualValleyQualityProfileError),
    #[error(transparent)]
    Hash(#[from] CanonicalJsonError),
    #[error("invalid quality capability schema, adapter identifier or generation")]
    InvalidCapabilities,
    #[error("invalid terrain constraint inventory")]
    InvalidTerrainConstraints,
    #[error("effective quality plan does not match its current source and capabilities")]
    StalePlan,
}

impl EffectiveQualityPlan {
    pub fn content_hash(&self) -> Result<ContentHash, QualityResolutionError> {
        Ok(canonical_json_sha256(self)?)
    }

    /// Recompute before consuming an imported plan; deserialization alone establishes no trust.
    pub fn verify(
        &self,
        source: &VisualValleyQualityProfileDocument,
        requested_tier: VisualQualityTier,
        capabilities: &QualityCapabilities,
        policy: QualityResolutionPolicy,
    ) -> Result<(), QualityResolutionError> {
        if *self == resolve_effective_quality(source, requested_tier, capabilities, policy)? {
            Ok(())
        } else {
            Err(QualityResolutionError::StalePlan)
        }
    }
}

pub fn resolve_effective_quality(
    source: &VisualValleyQualityProfileDocument,
    tier: VisualQualityTier,
    capabilities: &QualityCapabilities,
    policy: QualityResolutionPolicy,
) -> Result<EffectiveQualityPlan, QualityResolutionError> {
    use QualityCapability as C;
    capabilities.validate_identity()?;
    let requested_policy_hash = source.content_hash()?;
    let requested = source.preset(tier);
    let mut effective = EffectiveQualitySettings {
        terrain: requested.terrain,
        vegetation: requested.vegetation,
        shadows: requested.shadows,
        environment: requested.environment,
        water: EffectiveWaterSettings {
            reflection: requested.water.reflection,
            reflection_steps: requested.water.reflection_steps,
            reflection_max_distance_m: Some(requested.water.reflection_max_distance_m),
            refraction: requested.water.refraction,
            refraction_steps: requested.water.refraction_steps,
            refraction_max_depth_m: requested.water.refraction_max_depth_m,
        },
        rain: requested.rain,
        post_process: requested.post_process,
    };
    let mut resolver = CapabilityResolver {
        capabilities,
        policy,
        blockers: BTreeSet::new(),
        substitutions: Vec::new(),
    };
    resolver.resolve_terrain_constraints();
    // These adapter declarations cover their complete requested budget, including disabled states.
    for cap in [
        C::Terrain,
        C::Vegetation,
        C::Shadows,
        C::Sky,
        C::Rain,
        C::PostProcess,
        C::DistanceFog,
    ] {
        resolver.require(cap);
    }
    if requested.water.reflection != QualityWaterReflection::Disabled {
        resolver.require(C::EnvironmentReflection);
        resolver.substitute(
            C::ReflectionIntensity,
            None,
            QualitySubstitution::ReflectionIntensityNotApplied,
        );
    }
    if requested.water.reflection == QualityWaterReflection::ScreenSpace {
        resolver.require(C::ScreenReflection);
        if resolver.substitute(
            C::ReflectionDistance,
            None,
            QualitySubstitution::ReflectionDistanceNotGuaranteed,
        ) {
            effective.water.reflection_max_distance_m = None;
        }
    }
    match requested.water.refraction {
        QualityWaterRefraction::SurfaceTint => {
            resolver.require(C::SurfaceTint);
        }
        QualityWaterRefraction::ScreenSpace => {
            if resolver.substitute(
                C::ScreenRefraction,
                Some(C::SurfaceTint),
                QualitySubstitution::ScreenRefractionToSurfaceTint,
            ) {
                effective.water.refraction = QualityWaterRefraction::SurfaceTint;
                effective.water.refraction_steps = 0;
                effective.water.refraction_max_depth_m = 0.0;
            }
        }
        QualityWaterRefraction::Disabled => {}
    }
    if requested.environment.volumetric_fog_enabled
        && resolver.substitute(
            C::VolumetricFog,
            Some(C::DistanceFog),
            QualitySubstitution::VolumetricToAuthoredDistanceFog,
        )
    {
        effective.environment.volumetric_fog_enabled = false;
        effective.environment.volumetric_fog_step_count = 0;
        effective.environment.volumetric_fog_max_distance_m = 0.0;
    }
    Ok(EffectiveQualityPlan {
        schema_version: 1,
        requested_policy_hash,
        requested_tier: tier,
        capability_hash: canonical_json_sha256(capabilities)?,
        adapter_generation: capabilities.generation,
        resolution_policy: policy,
        effective,
        substitutions: resolver.substitutions,
        blockers: resolver.blockers.into_iter().collect(),
        terrain_constraints: capabilities.terrain_constraints.clone(),
        source_bindings: capabilities.source_bindings,
    })
}

struct CapabilityResolver<'a> {
    capabilities: &'a QualityCapabilities,
    policy: QualityResolutionPolicy,
    blockers: BTreeSet<QualityCapability>,
    substitutions: Vec<QualitySubstitution>,
}

impl CapabilityResolver<'_> {
    fn resolve_terrain_constraints(&mut self) {
        if self.capabilities.terrain_constraints.is_none() {
            return;
        }
        if self.policy == QualityResolutionPolicy::Adr0011
            && self.require(QualityCapability::Terrain)
        {
            self.substitutions
                .push(QualitySubstitution::TerrainBanksFullResolution);
        } else {
            self.blockers.insert(QualityCapability::Terrain);
        }
    }

    fn status(&self, capability: QualityCapability) -> CapabilityStatus {
        self.capabilities
            .capabilities
            .get(&capability)
            .copied()
            .unwrap_or(CapabilityStatus::Unqualified)
    }

    fn require(&mut self, capability: QualityCapability) -> bool {
        if self.status(capability) == CapabilityStatus::Supported {
            return true;
        }
        self.blockers.insert(capability);
        false
    }

    fn substitute(
        &mut self,
        capability: QualityCapability,
        replacement: Option<QualityCapability>,
        reason: QualitySubstitution,
    ) -> bool {
        match self.status(capability) {
            CapabilityStatus::Supported => false,
            CapabilityStatus::Unavailable if self.policy == QualityResolutionPolicy::Adr0011 => {
                if replacement.is_some_and(|replacement| !self.require(replacement)) {
                    self.blockers.insert(capability);
                    return false;
                }
                self.substitutions.push(reason);
                true
            }
            _ => {
                self.blockers.insert(capability);
                false
            }
        }
    }
}

#[cfg(test)]
mod tests;
