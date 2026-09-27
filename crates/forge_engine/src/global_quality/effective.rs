use super::QualityDomainProjection;
use crate::{
    EnvironmentProjectionStats, TerrainLodProjectionStats, TerrainQualityRequest,
    VegetationProjectionStats,
};
use bevy::prelude::*;
use forge_core::ContentHash;
use forge_project::{
    VisualQualityTier,
    effective_quality::{
        CapabilityStatus, EffectiveQualityPlan, QualityCapabilities, QualityCapability,
        QualityResolutionPolicy, resolve_effective_quality,
    },
};
use serde::Serialize;

#[derive(Clone, Debug, PartialEq, Serialize)]
pub struct EffectiveQualityEvidence {
    pub status: &'static str,
    pub plan: Option<EffectiveQualityPlan>,
    pub plan_hash: Option<ContentHash>,
    pub error: Option<String>,
    pub gpu_ready: bool,
}

type Capabilities = [(QualityCapability, CapabilityStatus); 14];
const ADAPTER_ID: &str = if cfg!(target_arch = "wasm32") {
    "forge-bevy-webgpu"
} else {
    "forge-bevy-native"
};

pub(super) fn application_guard(
    world: &World,
    tier: VisualQualityTier,
    sequence: u64,
    deadline: std::time::Duration,
) -> Result<forge_world::quality_application::QualityApplicationGuard, String> {
    let cache = world
        .get_resource::<EffectiveQualityCache>()
        .ok_or("missing quality cache")?;
    let request = world
        .get_resource::<TerrainQualityRequest>()
        .ok_or("missing quality source")?;
    let capabilities = QualityCapabilities {
        schema_version: 1,
        adapter_id: ADAPTER_ID.into(),
        generation: cache.generation,
        capabilities: cache
            .capabilities
            .ok_or("missing capabilities")?
            .into_iter()
            .collect(),
        terrain_constraints: cache.terrain_constraints.clone(),
        source_bindings: source_bindings(world),
    };
    let guard = forge_world::quality_application::QualityApplicationGuard::new(
        request.policy().0,
        tier,
        &capabilities,
        QualityResolutionPolicy::Adr0011,
        sequence,
        deadline,
    )
    .map_err(|error| error.to_string())?;
    if cache
        .evidence
        .as_ref()
        .and_then(|evidence| evidence.plan_hash)
        != Some(guard.identity().plan_hash)
    {
        return Err("quality snapshot changed before installation".into());
    }
    Ok(guard)
}

#[derive(Resource, Default)]
pub(super) struct EffectiveQualityCache {
    #[cfg(feature = "physics-rapier")]
    forest_binding: Option<crate::VegetationPrototypeWindCache>,
    forward_water: bool,
    source_hash: String,
    capabilities: Option<Capabilities>,
    terrain_constraints: Option<forge_project::effective_quality::TerrainQualityConstraints>,
    source_bindings: Option<forge_project::effective_quality::QualitySourceBindings>,
    tier: Option<VisualQualityTier>,
    generation: u64,
    evidence: Option<EffectiveQualityEvidence>,
}

pub(super) fn observe(
    world: &mut World,
    tier: VisualQualityTier,
    domains: [QualityDomainProjection; 7],
) -> Option<EffectiveQualityEvidence> {
    let Some(request) = world.get_resource::<TerrainQualityRequest>() else {
        // Retain the generation counter, but never return the previous plan without its source.
        if let Some(mut cache) = world.get_resource_mut::<EffectiveQualityCache>() {
            cache.capabilities = None;
            cache.evidence = None;
        }
        return None;
    };
    let (document, source_hash) = request.policy();
    let capabilities = capabilities(world, domains, source_hash);
    #[cfg(feature = "physics-rapier")]
    let forest_binding = world.get_resource::<crate::VegetationPrototypeWindCache>();
    let forward_water = world
        .get_resource::<crate::WaterProjectionStats>()
        .is_some_and(|s| s.optical_depth_preview && s.forward_reflection_quality.is_some());
    let source_bindings = source_bindings(world);
    let terrain_constraints = world
        .get_resource::<TerrainLodProjectionStats>()
        .filter(|stats| stats.policy_hash == source_hash && stats.projected_tier == Some(tier))
        .and_then(|stats| stats.bank_constraints.clone());
    let cache = world.get_resource::<EffectiveQualityCache>()?;
    let identity_changed = cache.source_hash != source_hash
        || cache.capabilities != Some(capabilities)
        || cache.terrain_constraints != terrain_constraints;
    let identity_changed = identity_changed
        || cache.source_bindings != source_bindings
        || cache.forward_water != forward_water;
    #[cfg(feature = "physics-rapier")]
    let identity_changed = identity_changed || cache.forest_binding.as_ref() != forest_binding;
    if !identity_changed && cache.tier == Some(tier) {
        return cache.evidence.clone();
    }
    let generation = if identity_changed {
        cache.generation.checked_add(1)
    } else {
        Some(cache.generation)
    };
    let evidence = generation.map_or_else(
        || EffectiveQualityEvidence {
            status: "adapter_generation_exhausted",
            plan: None,
            plan_hash: None,
            error: Some("quality adapter generation exhausted".into()),
            gpu_ready: false,
        },
        |generation| {
            let caps = QualityCapabilities {
                schema_version: 1,
                adapter_id: ADAPTER_ID.into(),
                generation,
                capabilities: capabilities.into_iter().collect(),
                terrain_constraints: terrain_constraints.clone(),
                source_bindings,
            };
            match resolve_effective_quality(document, tier, &caps, QualityResolutionPolicy::Adr0011)
                .and_then(|plan| {
                    let hash = plan.content_hash()?;
                    Ok((plan, hash))
                }) {
                Ok((plan, hash)) => EffectiveQualityEvidence {
                    status: if !plan.blockers.is_empty() {
                        "unresolved_capabilities"
                    } else if plan.source_bindings.is_none() {
                        "unbound_source_documents"
                    } else {
                        "resolved_plan_not_applied"
                    },
                    plan: Some(plan),
                    plan_hash: Some(hash),
                    error: None,
                    gpu_ready: false,
                },
                Err(error) => EffectiveQualityEvidence {
                    status: "resolution_failed",
                    plan: None,
                    plan_hash: None,
                    error: Some(error.to_string()),
                    gpu_ready: false,
                },
            }
        },
    );
    let source_hash = source_hash.to_owned();
    #[cfg(feature = "physics-rapier")]
    let forest_binding = forest_binding.cloned();
    let mut cache = world.resource_mut::<EffectiveQualityCache>();
    if let Some(generation) = generation {
        cache.generation = generation;
    }
    cache.source_hash = source_hash;
    #[cfg(feature = "physics-rapier")]
    {
        cache.forest_binding = forest_binding;
    }
    cache.forward_water = forward_water;
    cache.capabilities = Some(capabilities);
    cache.terrain_constraints = terrain_constraints;
    cache.source_bindings = source_bindings;
    cache.tier = Some(tier);
    cache.evidence = Some(evidence.clone());
    Some(evidence)
}

fn source_bindings(
    world: &World,
) -> Option<forge_project::effective_quality::QualitySourceBindings> {
    let terrain = world.get_resource::<TerrainLodProjectionStats>()?;
    let vegetation = world.get_resource::<VegetationProjectionStats>()?;
    let water = world.get_resource::<crate::WaterProjectionStats>()?;
    if vegetation.terrain_document_hash != terrain.terrain_hash
        || water.terrain_source_hash != terrain.terrain_hash
    {
        return None;
    }
    Some(forge_project::effective_quality::QualitySourceBindings {
        terrain: terrain.terrain_hash.parse().ok()?,
        vegetation: vegetation.vegetation_document_hash.parse().ok()?,
        environment: world
            .get_resource::<EnvironmentProjectionStats>()?
            .source_hash
            .parse()
            .ok()?,
        water: water.source_hash.parse().ok()?,
        weather: world
            .get_resource::<crate::WeatherProjectionStats>()?
            .source_hash
            .parse()
            .ok()?,
    })
}

#[allow(
    clippy::too_many_lines,
    reason = "One explicit inventory of fourteen independent adapter capabilities"
)]
fn capabilities(
    world: &World,
    domains: [QualityDomainProjection; 7],
    policy_hash: &str,
) -> Capabilities {
    use CapabilityStatus::{Supported, Unavailable, Unqualified};
    use QualityCapability as C;
    let terrain = world.get_resource::<TerrainLodProjectionStats>();
    let same_policy = terrain.is_some_and(|s| s.policy_hash == policy_hash);
    let projected = |index| {
        if same_policy && domains[index] == QualityDomainProjection::CpuProjected {
            Supported
        } else {
            Unqualified
        }
    };
    let terrain_status = terrain_projection_support(terrain, projected(0));
    let vegetation = world.get_resource::<VegetationProjectionStats>();
    let vegetation_status = if vegetation.is_some_and(|s| {
        s.prototype_preview_error.is_some()
            || (s.prototype_preview.is_some() && !crate::vegetation::governed_quality_ready(world))
    }) {
        Unqualified
    } else {
        projected(1)
    };
    let environment = world.get_resource::<EnvironmentProjectionStats>();
    let sky_projected = same_policy && domains[3] == QualityDomainProjection::Partial;
    let distance_fog = sky_projected
        && environment
            .is_some_and(|s| !s.distance_fog.requested || s.distance_fog.component_present);
    let volumetric_preview = world
        .get_resource::<crate::VolumetricPreviewRequest>()
        .is_some_and(|r| r.enabled);
    let water = world.get_resource::<crate::WaterProjectionStats>();
    // Unlike Bevy's classic SSR component, the forward adapter has an explicit
    // metric endpoint. Keep that ceiling in the derived plan rather than applying
    // the classic unsupported-ceiling fallback, which would disable its ray march.
    // This field alone does not qualify reflections, refraction or GPU execution.
    let forward_distance = same_policy
        && domains[4] == QualityDomainProjection::Partial
        && forward_distance_supported(water, policy_hash);
    let forward_supported = forward_distance
        && water
            .and_then(|s| s.forward_reflection_quality.as_ref())
            .is_some_and(forward_optics_supported);
    let tint_supported = forward_supported
        || surface_tint_supported(
            true,
            same_policy && domains[4] == QualityDomainProjection::Partial,
            water.is_some_and(|s| s.shader_uniforms_match_profile),
            water.and_then(|s| s.quality.as_ref()),
        );
    let (environment_reflection, screen_reflection) = if forward_supported {
        (Supported, Supported)
    } else {
        reflection_support(
            (true, true),
            same_policy && domains[4] == QualityDomainProjection::Partial,
            water.is_some_and(|s| s.shader_uniforms_match_profile),
            water.and_then(|s| s.quality.as_ref()),
        )
    };
    [
        (C::Terrain, terrain_status),
        (C::Vegetation, vegetation_status),
        (C::Shadows, projected(2)),
        (
            C::Sky,
            if sky_projected {
                Supported
            } else {
                Unqualified
            },
        ),
        (C::Rain, projected(5)),
        (C::PostProcess, projected(6)),
        (C::EnvironmentReflection, environment_reflection),
        // The adapter has no authored per-material reflection gain on either backend.
        // This declaration does not qualify the IBL/SSR paths themselves.
        (C::ReflectionIntensity, Unavailable),
        (
            C::SurfaceTint,
            if tint_supported {
                Supported
            } else {
                Unqualified
            },
        ),
        (C::ScreenReflection, screen_reflection),
        (
            C::ReflectionDistance,
            if forward_distance {
                Supported
            } else {
                Unavailable
            },
        ),
        (
            C::ScreenRefraction,
            if forward_supported {
                Supported
            } else {
                Unavailable
            },
        ),
        (
            C::VolumetricFog,
            if volumetric_preview {
                Unqualified
            } else {
                Unavailable
            },
        ),
        (
            C::DistanceFog,
            if distance_fog { Supported } else { Unqualified },
        ),
    ]
}

fn forward_distance_supported(
    water: Option<&crate::WaterProjectionStats>,
    policy_hash: &str,
) -> bool {
    water.is_some_and(|s| {
        s.optical_depth_preview
            && s.shader_uniforms_match_profile
            && s.forward_reflection_quality
                .as_ref()
                .is_some_and(|o| o.policy_hash.as_deref() == Some(policy_hash) && o.budgets_match())
    })
}

fn forward_optics_supported(
    observation: &crate::water::quality::ForwardReflectionObservation,
) -> bool {
    !observation.manual_optics_override
        && observation.full_target_viewport
        && observation.budgets_match()
        && observation.surface_tint_projected
        && observation.environment_textures_available
        && observation
            .environment_light_intensity
            .is_some_and(|v| v.is_finite() && v > 0.0)
        && observation
            .material_reflectance
            .is_some_and(|v| v.is_finite() && v > 0.0 && v <= 1.0)
}

fn terrain_projection_support(
    stats: Option<&TerrainLodProjectionStats>,
    projected: CapabilityStatus,
) -> CapabilityStatus {
    let consistent =
        stats.is_some_and(|s| match (s.bank_constrained_chunks, &s.bank_constraints) {
            (0, None) => true,
            (count, Some(constraints)) if count > 0 => {
                s.projected_tier.is_some()
                    && s.projected_tier == s.requested_tier
                    && constraints.terrain_hash.to_string() == s.terrain_hash
                    && constraints.canonical_chunks as usize == s.canonical_chunks
                    && constraints.full_resolution_chunks.len() == count
            }
            _ => false,
        });
    if consistent {
        projected
    } else {
        CapabilityStatus::Unqualified
    }
}

fn reflection_support(
    qualified_paths: (bool, bool),
    matching_water_domain: bool,
    shader_uniforms_match: bool,
    observation: Option<&crate::WaterQualityObservation>,
) -> (CapabilityStatus, CapabilityStatus) {
    use CapabilityStatus::{Supported, Unqualified};
    // Native/Web IBL and SSR isolation establish path support, not current GPU readiness.
    // See R1_WEB_OPTICAL_SAMPLE_PROTOCOL.md (7 September, repeated IBL 1/0/1).
    // Gain and metric ceiling remain separate unavailable capabilities under ADR-0011.
    let Some(s) = observation.filter(|s| {
        (qualified_paths.0 || qualified_paths.1)
            && matching_water_domain
            && shader_uniforms_match
            && !s.reflections_disabled_preview
            && s.requested.reflection != forge_project::QualityWaterReflection::Disabled
            && s.environment_textures_available
            && s.environment_light_intensity
                .is_some_and(|v| v.is_finite() && v > 0.0)
            && s.material_reflectance_projected
            && s.material_reflectance
                .is_some_and(|v| v.is_finite() && v > 0.0 && v <= 1.0)
    }) else {
        return (Unqualified, Unqualified);
    };
    let screen = qualified_paths.1
        && !s.screen_reflections_disabled_preview
        && s.requested.reflection == forge_project::QualityWaterReflection::ScreenSpace
        && s.ssr_budget_projected
        && s.requested.reflection_steps > 0
        && s.ssr_linear_steps == Some(u32::from(s.requested.reflection_steps));
    (
        if qualified_paths.0 {
            Supported
        } else {
            Unqualified
        },
        if screen { Supported } else { Unqualified },
    )
}

fn surface_tint_supported(
    qualified_path: bool,
    matching_water_domain: bool,
    shader_uniforms_match: bool,
    observation: Option<&crate::WaterQualityObservation>,
) -> bool {
    // Frozen native/Web on/off/restore proofs establish execution of the authored tint.
    // Web contrast remains below the conservative optical heuristic; path support is
    // not artistic contrast acceptance or current GPU readiness. See the optical report.
    qualified_path
        && matching_water_domain
        && shader_uniforms_match
        && observation.is_some_and(|s| {
            s.surface_tint_projected
                && s.surface_tint_preview != Some(false)
                && s.requested.refraction != forge_project::QualityWaterRefraction::Disabled
                && s.surface_tint_strength
                    .is_some_and(|strength| strength.is_finite() && (0.0..=1.0).contains(&strength))
        })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn forward_support_requires_current_optics_not_just_matching_uniforms() {
        let mut observation = crate::water::ForwardReflectionObservation {
            manual_optics_override: false,
            environment_textures_available: false,
            environment_light_intensity: None,
            full_target_viewport: false,
            surface_tint_projected: false,
            policy_hash: None,
            requested_tier: None,
            effective_request: None,
            screen_refraction_disabled_preview: false,
            requested: None,
            linear_steps: None,
            max_distance_m: None,
            refraction_linear_steps: None,
            refraction_max_path_m: None,
            material_reflectance: None,
            projection_matches: false,
            status: "synthetic_not_gpu",
        };
        observation.requested = Some(observed_water().renderer_requested);
        observation.linear_steps = Some(48.0);
        observation.max_distance_m = Some(96.0);
        observation.refraction_linear_steps = Some(24.0);
        observation.refraction_max_path_m = Some(3.2);
        observation.material_reflectance = Some(0.36);
        observation.projection_matches = true;
        observation.surface_tint_projected = true;
        observation.full_target_viewport = true;
        observation.environment_textures_available = true;
        observation.environment_light_intensity = Some(1.0);
        assert!(forward_optics_supported(&observation));
        for mutate in [
            |s: &mut crate::water::ForwardReflectionObservation| s.manual_optics_override = true,
            |s: &mut crate::water::ForwardReflectionObservation| s.full_target_viewport = false,
            |s: &mut crate::water::ForwardReflectionObservation| s.projection_matches = false,
            |s: &mut crate::water::ForwardReflectionObservation| s.surface_tint_projected = false,
            |s: &mut crate::water::ForwardReflectionObservation| {
                s.environment_textures_available = false;
            },
            |s: &mut crate::water::ForwardReflectionObservation| {
                s.environment_light_intensity = Some(0.0);
            },
            |s: &mut crate::water::ForwardReflectionObservation| s.material_reflectance = None,
            |s: &mut crate::water::ForwardReflectionObservation| s.max_distance_m = Some(64.0),
        ] {
            let mut invalid = observation.clone();
            mutate(&mut invalid);
            assert!(!forward_optics_supported(&invalid));
        }
    }

    #[test]
    fn terrain_support_requires_a_consistent_current_constraint_inventory() {
        use CapabilityStatus::{Supported, Unqualified};
        let hash: ContentHash = "11".repeat(32).parse().unwrap();
        let stats = TerrainLodProjectionStats {
            terrain_hash: hash.to_string(),
            requested_tier: Some(VisualQualityTier::High),
            projected_tier: Some(VisualQualityTier::High),
            canonical_chunks: 64,
            bank_constrained_chunks: 1,
            bank_constraints: Some(
                forge_project::effective_quality::TerrainQualityConstraints {
                    terrain_hash: hash,
                    canonical_chunks: 64,
                    full_resolution_chunks: vec![forge_core::EntityId::deterministic("bank")],
                },
            ),
            ..default()
        };
        assert_eq!(
            terrain_projection_support(Some(&stats), Supported),
            Supported
        );
        assert_eq!(
            terrain_projection_support(Some(&stats), Unqualified),
            Unqualified
        );
        assert_eq!(terrain_projection_support(None, Supported), Unqualified);
        for mutate in [
            |s: &mut TerrainLodProjectionStats| s.bank_constraints = None,
            |s: &mut TerrainLodProjectionStats| s.bank_constrained_chunks = 2,
            |s: &mut TerrainLodProjectionStats| s.bank_constrained_chunks = 0,
            |s: &mut TerrainLodProjectionStats| s.canonical_chunks = 32,
            |s: &mut TerrainLodProjectionStats| s.terrain_hash = "22".repeat(32),
            |s: &mut TerrainLodProjectionStats| s.projected_tier = None,
            |s: &mut TerrainLodProjectionStats| s.requested_tier = Some(VisualQualityTier::Low),
        ] {
            let mut changed = stats.clone();
            mutate(&mut changed);
            assert_eq!(
                terrain_projection_support(Some(&changed), Supported),
                Unqualified
            );
        }
    }

    fn observed_water() -> crate::WaterQualityObservation {
        let policy = forge_project::VisualValleyQualityProfileDocument::from_json(include_str!(
            "../../../../examples/visual_valley/quality-profile.v1.json"
        ))
        .unwrap();
        crate::WaterQualityObservation {
            surface_tint_preview: None,
            reflections_disabled_preview: false,
            screen_reflections_disabled_preview: false,
            policy_hash: policy.content_hash().unwrap().to_string(),
            requested_tier: VisualQualityTier::High,
            requested: policy.presets.high.water,
            renderer_requested: forge_project::effective_quality::EffectiveWaterSettings {
                reflection: policy.presets.high.water.reflection,
                reflection_steps: policy.presets.high.water.reflection_steps,
                reflection_max_distance_m: Some(
                    policy.presets.high.water.reflection_max_distance_m,
                ),
                refraction: policy.presets.high.water.refraction,
                refraction_steps: policy.presets.high.water.refraction_steps,
                refraction_max_depth_m: policy.presets.high.water.refraction_max_depth_m,
            },
            effective_request: None,
            ssr_budget_projected: true,
            ssr_linear_steps: Some(48),
            reflection_path_status: "synthetic",
            reflection_distance_status: "synthetic",
            refraction_status: "synthetic",
            surface_tint_strength: Some(0.18),
            surface_tint_projected: true,
            material_reflectance: Some(0.36),
            material_reflectance_projected: true,
            environment_textures_available: true,
            environment_light_intensity: Some(1.0),
            full_preset_applied: false,
            status: "synthetic",
        }
    }

    #[test]
    fn native_reflection_mapping_requires_current_evidence_and_manual_choices() {
        use CapabilityStatus::{Supported, Unqualified};
        let observed = observed_water();
        let support = |s: &crate::WaterQualityObservation| {
            reflection_support((true, true), true, true, Some(s))
        };
        assert_eq!(support(&observed), (Supported, Supported));
        for (native, domain, uniforms) in [
            (false, true, true),
            (true, false, true),
            (true, true, false),
        ] {
            assert_eq!(
                reflection_support((native, native), domain, uniforms, Some(&observed)),
                (Unqualified, Unqualified)
            );
        }
        assert_eq!(
            reflection_support((true, true), true, true, None),
            (Unqualified, Unqualified)
        );
        for intensity in [
            None,
            Some(0.0),
            Some(-1.0),
            Some(f32::NAN),
            Some(f32::INFINITY),
        ] {
            let mut changed = observed.clone();
            changed.environment_light_intensity = intensity;
            assert_eq!(support(&changed), (Unqualified, Unqualified));
        }
        for mutate in [
            |s: &mut crate::WaterQualityObservation| s.environment_textures_available = false,
            |s: &mut crate::WaterQualityObservation| s.reflections_disabled_preview = true,
            |s: &mut crate::WaterQualityObservation| s.material_reflectance_projected = false,
            |s: &mut crate::WaterQualityObservation| s.material_reflectance = Some(0.0),
        ] {
            let mut changed = observed.clone();
            mutate(&mut changed);
            assert_eq!(support(&changed), (Unqualified, Unqualified));
        }
        for mutate in [
            |s: &mut crate::WaterQualityObservation| s.screen_reflections_disabled_preview = true,
            |s: &mut crate::WaterQualityObservation| s.ssr_budget_projected = false,
            |s: &mut crate::WaterQualityObservation| s.ssr_linear_steps = Some(1),
            |s: &mut crate::WaterQualityObservation| {
                s.requested.reflection = forge_project::QualityWaterReflection::Environment;
            },
        ] {
            let mut changed = observed.clone();
            mutate(&mut changed);
            assert_eq!(support(&changed), (Supported, Unqualified));
        }
    }

    #[test]
    fn screen_only_qualification_does_not_promote_ibl_and_keeps_current_frame_guards() {
        use CapabilityStatus::{Supported, Unqualified};
        let observed = observed_water();
        assert_eq!(
            reflection_support((false, true), true, true, Some(&observed)),
            (Unqualified, Supported)
        );
        for (domain, uniforms) in [(false, true), (true, false)] {
            assert_eq!(
                reflection_support((false, true), domain, uniforms, Some(&observed)),
                (Unqualified, Unqualified)
            );
        }
        assert_eq!(
            reflection_support((false, true), true, true, None),
            (Unqualified, Unqualified)
        );
        for mutate in [
            |s: &mut crate::WaterQualityObservation| s.screen_reflections_disabled_preview = true,
            |s: &mut crate::WaterQualityObservation| s.reflections_disabled_preview = true,
            |s: &mut crate::WaterQualityObservation| s.ssr_budget_projected = false,
            |s: &mut crate::WaterQualityObservation| s.ssr_linear_steps = Some(0),
            |s: &mut crate::WaterQualityObservation| s.material_reflectance_projected = false,
            |s: &mut crate::WaterQualityObservation| s.environment_textures_available = false,
        ] {
            let mut changed = observed.clone();
            mutate(&mut changed);
            assert_eq!(
                reflection_support((false, true), true, true, Some(&changed)),
                (Unqualified, Unqualified)
            );
        }
    }

    #[test]
    fn qualified_ibl_is_independent_of_ssr_and_rejects_zero_intensity_until_restored() {
        use CapabilityStatus::{Supported, Unqualified};
        let mut observed = observed_water();
        observed.screen_reflections_disabled_preview = true;
        observed.ssr_budget_projected = false;
        observed.ssr_linear_steps = Some(0);
        let support = |s: &crate::WaterQualityObservation| {
            reflection_support((true, true), true, true, Some(s))
        };
        assert_eq!(support(&observed), (Supported, Unqualified));
        let initial = observed.environment_light_intensity;
        observed.environment_light_intensity = Some(0.0);
        assert_eq!(support(&observed), (Unqualified, Unqualified));
        observed.environment_light_intensity = initial;
        assert_eq!(support(&observed), (Supported, Unqualified));
    }

    #[test]
    fn tint_support_requires_current_material_and_respects_manual_disable() {
        let observed = observed_water();
        assert!(surface_tint_supported(true, true, true, Some(&observed)));
        for (qualified_path, domain, uniforms) in [
            (false, true, true),
            (true, false, true),
            (true, true, false),
        ] {
            assert!(!surface_tint_supported(
                qualified_path,
                domain,
                uniforms,
                Some(&observed)
            ));
        }
        assert!(!surface_tint_supported(true, true, true, None));
        let mut changed = observed.clone();
        changed.surface_tint_preview = Some(false);
        assert!(!surface_tint_supported(true, true, true, Some(&changed)));
        changed.surface_tint_preview = Some(true);
        assert!(surface_tint_supported(true, true, true, Some(&changed)));
        changed.surface_tint_projected = false;
        assert!(!surface_tint_supported(true, true, true, Some(&changed)));
        for strength in [None, Some(f32::NAN), Some(-0.1), Some(1.1)] {
            let mut changed = observed.clone();
            changed.surface_tint_strength = strength;
            assert!(!surface_tint_supported(true, true, true, Some(&changed)));
        }
        let mut changed = observed;
        changed.requested.refraction = forge_project::QualityWaterRefraction::Disabled;
        assert!(!surface_tint_supported(true, true, true, Some(&changed)));
    }

    #[test]
    fn plan_is_cached_invalidated_and_never_acknowledges_gpu() {
        let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
        let policy = forge_project::VisualValleyQualityProfileDocument::from_json(include_str!(
            "../../../../examples/visual_valley/quality-profile.v1.json"
        ))
        .unwrap();
        let mut app = App::new();
        app.add_plugins(
            crate::VisualValleyTerrainQualityPlugin::new(
                &terrain,
                policy.clone(),
                VisualQualityTier::High,
            )
            .unwrap(),
        );
        app.init_resource::<EffectiveQualityCache>();
        let world = app.world_mut();
        world
            .resource_mut::<TerrainLodProjectionStats>()
            .policy_hash = policy.content_hash().unwrap().to_string();
        let domains = [QualityDomainProjection::CpuProjected; 7];
        let first = observe(world, VisualQualityTier::High, domains).unwrap();
        assert_eq!(first.status, "unresolved_capabilities");
        assert!(!first.gpu_ready);
        let plan = first.plan.as_ref().unwrap();
        assert!(plan.substitutions.contains(
            &forge_project::effective_quality::QualitySubstitution::ReflectionIntensityNotApplied
        ));
        assert!(
            plan.blockers
                .contains(&QualityCapability::EnvironmentReflection)
        );
        assert!(plan.blockers.contains(&QualityCapability::ScreenReflection));
        assert_eq!(
            first,
            observe(world, VisualQualityTier::High, domains).unwrap()
        );
        assert_constraint_identity_changes(world, domains);
        world
            .resource_mut::<TerrainLodProjectionStats>()
            .bank_constrained_chunks = 1;
        let constrained = observe(world, VisualQualityTier::High, domains).unwrap();
        assert_ne!(first.plan_hash, constrained.plan_hash);
        assert!(
            constrained
                .plan
                .as_ref()
                .unwrap()
                .blockers
                .contains(&QualityCapability::Terrain)
        );
        let request = world.remove_resource::<TerrainQualityRequest>().unwrap();
        assert!(observe(world, VisualQualityTier::High, domains).is_none());
        world.insert_resource(request);
        let restored = observe(world, VisualQualityTier::High, domains).unwrap();
        assert_ne!(constrained.plan_hash, restored.plan_hash);
        assert!(
            restored.plan.unwrap().adapter_generation
                > constrained.plan.unwrap().adapter_generation
        );
    }

    fn assert_constraint_identity_changes(
        world: &mut World,
        domains: [QualityDomainProjection; 7],
    ) {
        use forge_project::effective_quality::TerrainQualityConstraints;
        let mut stats = world.resource_mut::<TerrainLodProjectionStats>();
        stats.projected_tier = Some(VisualQualityTier::High);
        stats.bank_constrained_chunks = 1;
        stats.bank_constraints = Some(TerrainQualityConstraints {
            terrain_hash: "11".repeat(32).parse().unwrap(),
            canonical_chunks: 64,
            full_resolution_chunks: vec![forge_core::EntityId::deterministic("bank-a")],
        });
        let first = observe(world, VisualQualityTier::High, domains).unwrap();
        world
            .resource_mut::<TerrainLodProjectionStats>()
            .bank_constraints
            .as_mut()
            .unwrap()
            .full_resolution_chunks[0] = forge_core::EntityId::deterministic("bank-b");
        let moved = observe(world, VisualQualityTier::High, domains).unwrap();
        assert_ne!(first.plan_hash, moved.plan_hash);
        assert!(
            moved.plan.as_ref().unwrap().adapter_generation
                > first.plan.as_ref().unwrap().adapter_generation
        );
        world
            .resource_mut::<TerrainLodProjectionStats>()
            .bank_constraints
            .as_mut()
            .unwrap()
            .terrain_hash = "22".repeat(32).parse().unwrap();
        let edited = observe(world, VisualQualityTier::High, domains).unwrap();
        assert_ne!(moved.plan_hash, edited.plan_hash);
        assert!(!edited.gpu_ready);
        world
            .resource_mut::<TerrainLodProjectionStats>()
            .projected_tier = None;
        let pending = observe(world, VisualQualityTier::High, domains).unwrap();
        assert!(pending.plan.unwrap().terrain_constraints.is_none());
    }
}
