use crate::vegetation::VegetationPresentation;
use crate::visual_valley::VisualValleySun;
use crate::water::{VisualValleyWaterProfile, apply_quality_optics_projection};
use crate::weather::{
    VisualValleyWeatherProfile, visual_valley_bloom_component,
    visual_valley_color_grading_component,
};
use crate::{
    VegetationChunkBatchMarker, VegetationLodRanges, VegetationPlacementMarker,
    VegetationProjectionLod, VisualValleyRainBatch, VisualValleyShadowObservationStatus,
    VisualValleyShadowStats, WaterProjectionStats, WeatherRuntimeState,
};
use bevy::{
    camera::visibility::VisibilityRange,
    core_pipeline::{prepass::DeferredPrepass, tonemapping::Tonemapping},
    light::{CascadeShadowConfig, CascadeShadowConfigBuilder, DirectionalLightShadowMap},
    pbr::{ContactShadows, ScreenSpaceReflections},
    post_process::bloom::Bloom,
    prelude::*,
    render::view::ColorGrading,
};
use forge_project::{
    DynamicQualityAdaptationProfile, QualityWaterReflection, ResolvedWaterReflectionPath,
    VisualQualityPreset, VisualQualityTier, VisualValleyQualityProfileDocument,
};
use serde::Serialize;

/// Strict runtime mode selected by the native runtime or Studio viewport.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize)]
#[serde(tag = "mode", content = "tier", rename_all = "snake_case")]
pub enum VisualValleyQualitySelection {
    Fixed(VisualQualityTier),
    Adaptive,
    Benchmark,
}

impl VisualValleyQualitySelection {
    #[must_use]
    pub const fn initial_tier(
        self,
        profile: &VisualValleyQualityProfileDocument,
    ) -> VisualQualityTier {
        match self {
            Self::Fixed(tier) => tier,
            Self::Adaptive => profile.adaptation.initial_tier,
            Self::Benchmark => profile.benchmark.fixed_tier,
        }
    }

    #[must_use]
    pub const fn adaptation_enabled(self, profile: &VisualValleyQualityProfileDocument) -> bool {
        matches!(self, Self::Adaptive) && profile.adaptation.policy_enabled
    }

    #[must_use]
    pub const fn label(self) -> &'static str {
        match self {
            Self::Fixed(_) => "fixed",
            Self::Adaptive => "adaptive",
            Self::Benchmark => "benchmark",
        }
    }
}

/// Whether a quality-profile field reaches a real engine API.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum QualityFieldProjectionStatus {
    AppliedAtStartup,
    AppliedOnTierTransition,
    NotApplicable,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
pub struct QualityFieldProjection {
    pub field: &'static str,
    pub status: QualityFieldProjectionStatus,
    pub evidence: &'static str,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize)]
pub struct QualityTierTransition {
    pub frame: u64,
    pub from: VisualQualityTier,
    pub to: VisualQualityTier,
    pub reason: &'static str,
}

/// Machine-readable state included in native and Studio ledgers.
#[derive(Clone, Debug, Resource, Serialize)]
pub struct VisualValleyQualityStats {
    pub schema: &'static str,
    pub schema_version: u32,
    pub profile_hash: String,
    pub selection: VisualValleyQualitySelection,
    pub startup_tier: VisualQualityTier,
    pub current_tier: VisualQualityTier,
    pub dynamically_applied_tier: Option<VisualQualityTier>,
    pub adaptation_enabled: bool,
    pub adaptation_policy: Option<DynamicQualityAdaptationProfile>,
    pub benchmark_fixed_high: bool,
    pub observed_frames: u64,
    pub cooldown_remaining_frames: u16,
    pub transition_count: u64,
    pub last_transition: Option<QualityTierTransition>,
    pub requested_preset: VisualQualityPreset,
    pub field_projections: Vec<QualityFieldProjection>,
}

#[derive(Resource)]
struct VisualValleyQualityRuntime {
    profile: VisualValleyQualityProfileDocument,
    selection: VisualValleyQualitySelection,
    current_tier: VisualQualityTier,
    last_applied_tier: Option<VisualQualityTier>,
    observed_frames: u64,
    upgrade_frames: u16,
    downgrade_frames: u16,
    cooldown_remaining_frames: u16,
    transition_count: u64,
    last_transition: Option<QualityTierTransition>,
}

impl VisualValleyQualityRuntime {
    fn observe_frame(&mut self, frame_ms: f32) -> Option<QualityTierTransition> {
        if !frame_ms.is_finite() || frame_ms <= 0.0 {
            return None;
        }
        self.observed_frames = self.observed_frames.saturating_add(1);
        if !self.selection.adaptation_enabled(&self.profile) {
            return None;
        }
        if self.cooldown_remaining_frames > 0 {
            self.cooldown_remaining_frames -= 1;
            self.upgrade_frames = 0;
            self.downgrade_frames = 0;
            return None;
        }

        let policy = self.profile.adaptation;
        let (candidate, reason) = if frame_ms > policy.downgrade_above_frame_time_ms {
            self.downgrade_frames = self.downgrade_frames.saturating_add(1);
            self.upgrade_frames = 0;
            (
                (self.downgrade_frames >= policy.downgrade_sustain_frames)
                    .then(|| next_lower(self.current_tier, policy.minimum_tier)),
                "sustained_frame_time_above_downgrade_threshold",
            )
        } else if frame_ms < policy.upgrade_below_frame_time_ms {
            self.upgrade_frames = self.upgrade_frames.saturating_add(1);
            self.downgrade_frames = 0;
            (
                (self.upgrade_frames >= policy.upgrade_sustain_frames)
                    .then(|| next_higher(self.current_tier, policy.maximum_tier)),
                "sustained_frame_time_below_upgrade_threshold",
            )
        } else {
            self.upgrade_frames = 0;
            self.downgrade_frames = 0;
            (None, "inside_hysteresis_band")
        };

        let next = candidate.flatten()?;
        let transition = QualityTierTransition {
            frame: self.observed_frames,
            from: self.current_tier,
            to: next,
            reason,
        };
        self.current_tier = next;
        self.upgrade_frames = 0;
        self.downgrade_frames = 0;
        self.cooldown_remaining_frames = policy.cooldown_frames;
        self.transition_count = self.transition_count.saturating_add(1);
        self.last_transition = Some(transition);
        Some(transition)
    }
}

/// Runtime/Studio adapter for the renderer-neutral Visual Valley quality profile.
pub struct VisualValleyQualityPlugin {
    profile: VisualValleyQualityProfileDocument,
    selection: VisualValleyQualitySelection,
}

#[derive(Clone, Copy, Debug, Eq, Hash, PartialEq, SystemSet)]
pub(crate) enum VisualValleyQualitySet {
    Apply,
}

impl VisualValleyQualityPlugin {
    #[must_use]
    pub const fn new(
        profile: VisualValleyQualityProfileDocument,
        selection: VisualValleyQualitySelection,
    ) -> Self {
        Self { profile, selection }
    }

    #[must_use]
    pub fn startup_preset(&self) -> VisualQualityPreset {
        self.profile
            .preset(self.selection.initial_tier(&self.profile))
            .clone()
    }

    #[must_use]
    pub const fn startup_tier(&self) -> VisualQualityTier {
        self.selection.initial_tier(&self.profile)
    }
}

impl Plugin for VisualValleyQualityPlugin {
    fn build(&self, app: &mut App) {
        self.profile
            .validate()
            .expect("VisualValleyQualityPlugin requires a validated quality profile");
        let startup_tier = self.selection.initial_tier(&self.profile);
        assert!(
            !matches!(self.selection, VisualValleyQualitySelection::Benchmark)
                || (startup_tier == VisualQualityTier::High
                    && !self.profile.benchmark.adaptation_enabled),
            "benchmark quality must remain fixed High with adaptation disabled"
        );
        let profile_hash = self
            .profile
            .content_hash()
            .expect("validated quality profile must have a canonical hash")
            .to_string();
        app.insert_resource(VisualValleyQualityRuntime {
            profile: self.profile.clone(),
            selection: self.selection,
            current_tier: startup_tier,
            last_applied_tier: None,
            observed_frames: 0,
            upgrade_frames: 0,
            downgrade_frames: 0,
            cooldown_remaining_frames: 0,
            transition_count: 0,
            last_transition: None,
        })
        .insert_resource(VisualValleyQualityStats {
            schema: "forge.visual-valley-quality-runtime",
            schema_version: 1,
            profile_hash,
            selection: self.selection,
            startup_tier,
            current_tier: startup_tier,
            dynamically_applied_tier: None,
            adaptation_enabled: self.selection.adaptation_enabled(&self.profile),
            adaptation_policy: self
                .selection
                .adaptation_enabled(&self.profile)
                .then_some(self.profile.adaptation),
            benchmark_fixed_high: matches!(self.selection, VisualValleyQualitySelection::Benchmark),
            observed_frames: 0,
            cooldown_remaining_frames: 0,
            transition_count: 0,
            last_transition: None,
            requested_preset: self.profile.preset(startup_tier).clone(),
            field_projections: quality_field_projections(),
        })
        .add_systems(
            Last,
            (advance_quality_adaptation, apply_quality_tier)
                .chain()
                .in_set(VisualValleyQualitySet::Apply),
        );
    }
}

#[allow(
    clippy::cast_possible_truncation,
    reason = "validated frame deltas are compared with f32 document thresholds"
)]
fn advance_quality_adaptation(
    time: Option<Res<Time<Real>>>,
    mut runtime: ResMut<VisualValleyQualityRuntime>,
) {
    let Some(time) = time else {
        return;
    };
    let frame_ms = (time.delta_secs_f64() * 1_000.0) as f32;
    runtime.observe_frame(frame_ms);
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::too_many_arguments,
    clippy::too_many_lines,
    clippy::type_complexity,
    reason = "one quality projection transaction updates the correlated renderer components"
)]
fn apply_quality_tier(
    mut commands: Commands,
    mut runtime: ResMut<VisualValleyQualityRuntime>,
    mut stats: ResMut<VisualValleyQualityStats>,
    mut presentation: Option<ResMut<VegetationPresentation>>,
    mut placements: Query<
        (&VegetationPlacementMarker, &mut VisibilityRange),
        Without<VegetationChunkBatchMarker>,
    >,
    mut batches: Query<
        (&VegetationChunkBatchMarker, &mut VisibilityRange),
        Without<VegetationPlacementMarker>,
    >,
    mut suns: Query<(&mut DirectionalLight, &mut CascadeShadowConfig), With<VisualValleySun>>,
    mut shadow_stats: Option<ResMut<VisualValleyShadowStats>>,
    mut shadow_map: Option<ResMut<DirectionalLightShadowMap>>,
    weather_profile: Option<Res<VisualValleyWeatherProfile>>,
    weather_state: Option<Res<WeatherRuntimeState>>,
    water_profile: Option<Res<VisualValleyWaterProfile>>,
    mut water_projection: Option<ResMut<WaterProjectionStats>>,
    cameras: Query<
        (
            Entity,
            Option<&ScreenSpaceReflections>,
            Option<&Bloom>,
            Option<&ColorGrading>,
            Option<&Tonemapping>,
        ),
        With<Camera3d>,
    >,
    mut rain_batches: Query<&mut Visibility, With<VisualValleyRainBatch>>,
) {
    sync_quality_stats(&runtime, &mut stats);
    if runtime.last_applied_tier == Some(runtime.current_tier) {
        return;
    }
    let preset = runtime.profile.preset(runtime.current_tier).clone();

    if let Some(presentation) = presentation.as_deref_mut() {
        presentation.0.near_lod_distance_m = preset.vegetation.near_lod_distance_m;
        presentation.0.medium_lod_distance_m = preset.vegetation.medium_lod_distance_m;
        presentation.0.hlod_distance_m = preset.vegetation.hlod_distance_m;
        presentation.0.cull_distance_m = preset.vegetation.cull_distance_m;
        presentation.0.shadow_distance_m = preset.shadows.maximum_distance_m;
        let ranges = VegetationLodRanges::from_profile(presentation.0);
        for (marker, mut range) in &mut placements {
            *range = match (marker.kind, marker.lod) {
                (_, VegetationProjectionLod::Medium) => ranges.medium.clone(),
                (forge_project::VegetationKind::Tree, VegetationProjectionLod::Near) => {
                    ranges.near.clone()
                }
                _ => ranges.individual.clone(),
            };
        }
        for (marker, mut range) in &mut batches {
            *range = match marker.lod {
                VegetationProjectionLod::GrassBatch => ranges.grass.clone(),
                VegetationProjectionLod::Hlod => ranges.hlod.clone(),
                VegetationProjectionLod::Near | VegetationProjectionLod::Medium => continue,
            };
        }
    }

    let sun_entities = suns.iter_mut().count();
    let mut observed_shadow = None;
    for (mut light, mut cascade) in &mut suns {
        light.shadow_maps_enabled = preset.shadows.enabled;
        if preset.shadows.enabled {
            let first_bound = (preset.shadows.maximum_distance_m * 0.5).min(24.0);
            *cascade = CascadeShadowConfigBuilder {
                num_cascades: usize::from(preset.shadows.cascade_count),
                minimum_distance: 0.0,
                maximum_distance: preset.shadows.maximum_distance_m,
                first_cascade_far_bound: first_bound,
                ..default()
            }
            .build();
        }
        observed_shadow = Some((light.shadow_maps_enabled, cascade.bounds.clone()));
    }
    if let Some(shadow_map) = shadow_map.as_deref_mut()
        && preset.shadows.enabled
    {
        shadow_map.size = usize::from(preset.shadows.map_resolution);
    }
    if let Some(shadow_stats) = shadow_stats.as_deref_mut() {
        shadow_stats.sun_entities = sun_entities;
        if sun_entities == 1 {
            let (enabled, bounds) = observed_shadow
                .expect("the single Visual Valley sun was observed in the quality query");
            shadow_stats.status = VisualValleyShadowObservationStatus::ObservedSingleSun;
            shadow_stats.shadow_maps_enabled = Some(enabled);
            shadow_stats.configured_maximum_distance_m = bounds.last().copied();
            shadow_stats.cascade_bounds_m = bounds;
        } else {
            shadow_stats.status = if sun_entities == 0 {
                VisualValleyShadowObservationStatus::UnavailableNoSun
            } else {
                VisualValleyShadowObservationStatus::UnavailableMultipleSuns
            };
            shadow_stats.shadow_maps_enabled = None;
            shadow_stats.configured_maximum_distance_m = None;
            shadow_stats.cascade_bounds_m.clear();
        }
    }

    if let Some(water_projection) = water_projection.as_deref_mut()
        && let Some(water_profile) = water_profile.as_deref()
    {
        apply_quality_optics_projection(
            water_projection,
            &water_profile.document,
            preset.water,
            runtime.current_tier,
        );
    }
    let screen_space_reflections_available = water_projection.as_deref().is_some_and(|water| {
        matches!(
            water.resolved_optics.reflection,
            ResolvedWaterReflectionPath::ScreenSpace { .. }
        )
    });
    for (camera, ssr, bloom, color_grading, tonemapping) in &cameras {
        if preset.shadows.contact_shadows_enabled {
            commands.entity(camera).insert(ContactShadows::default());
        } else {
            commands.entity(camera).remove::<ContactShadows>();
        }
        match preset.water.reflection {
            QualityWaterReflection::ScreenSpace if screen_space_reflections_available => {
                let mut settings = ssr.cloned().unwrap_or_default();
                settings.linear_steps = u32::from(preset.water.reflection_steps);
                commands.entity(camera).insert(settings);
            }
            QualityWaterReflection::ScreenSpace
            | QualityWaterReflection::Disabled
            | QualityWaterReflection::Environment => {
                commands
                    .entity(camera)
                    .remove::<(ScreenSpaceReflections, DeferredPrepass)>();
            }
        }
        if preset.post_process.bloom_enabled {
            if bloom.is_none() {
                let configured = weather_profile
                    .as_deref()
                    .map_or(Bloom::NATURAL, |profile| {
                        visual_valley_bloom_component(&profile.document)
                    });
                commands.entity(camera).insert(configured);
            }
        } else if bloom.is_some() {
            commands.entity(camera).remove::<Bloom>();
        }
        if preset.post_process.color_grading_enabled {
            if color_grading.is_none() {
                let configured = weather_profile
                    .as_deref()
                    .zip(weather_state.as_deref())
                    .map_or_else(ColorGrading::default, |(profile, state)| {
                        visual_valley_color_grading_component(profile, state)
                    });
                commands.entity(camera).insert(configured);
            }
        } else if color_grading.is_some() {
            commands.entity(camera).remove::<ColorGrading>();
        }
        if preset.post_process.tonemapping_enabled {
            if tonemapping.is_none() {
                commands.entity(camera).insert(Tonemapping::TonyMcMapface);
            }
        } else {
            commands.entity(camera).remove::<Tonemapping>();
        }
    }
    for mut visibility in &mut rain_batches {
        *visibility = if preset.rain.enabled {
            Visibility::Visible
        } else {
            Visibility::Hidden
        };
    }

    runtime.last_applied_tier = Some(runtime.current_tier);
    sync_quality_stats(&runtime, &mut stats);
}

fn sync_quality_stats(runtime: &VisualValleyQualityRuntime, stats: &mut VisualValleyQualityStats) {
    stats.current_tier = runtime.current_tier;
    stats.dynamically_applied_tier = runtime.last_applied_tier;
    stats.observed_frames = runtime.observed_frames;
    stats.cooldown_remaining_frames = runtime.cooldown_remaining_frames;
    stats.transition_count = runtime.transition_count;
    stats.last_transition = runtime.last_transition;
    stats.requested_preset = runtime.profile.preset(runtime.current_tier).clone();
}

fn next_lower(tier: VisualQualityTier, minimum: VisualQualityTier) -> Option<VisualQualityTier> {
    let candidate = match tier {
        VisualQualityTier::Low => return None,
        VisualQualityTier::Medium => VisualQualityTier::Low,
        VisualQualityTier::High => VisualQualityTier::Medium,
        VisualQualityTier::Ultra => VisualQualityTier::High,
    };
    (candidate >= minimum).then_some(candidate)
}

fn next_higher(tier: VisualQualityTier, maximum: VisualQualityTier) -> Option<VisualQualityTier> {
    let candidate = match tier {
        VisualQualityTier::Low => VisualQualityTier::Medium,
        VisualQualityTier::Medium => VisualQualityTier::High,
        VisualQualityTier::High => VisualQualityTier::Ultra,
        VisualQualityTier::Ultra => return None,
    };
    (candidate <= maximum).then_some(candidate)
}

pub(crate) const QUALITY_FIELD_PROJECTION_CONTRACT: [(&str, QualityFieldProjectionStatus); 34] = [
    (
        "vegetation.near_lod_distance_m",
        QualityFieldProjectionStatus::AppliedOnTierTransition,
    ),
    (
        "vegetation.medium_lod_distance_m",
        QualityFieldProjectionStatus::AppliedOnTierTransition,
    ),
    (
        "vegetation.hlod_distance_m",
        QualityFieldProjectionStatus::AppliedOnTierTransition,
    ),
    (
        "vegetation.cull_distance_m",
        QualityFieldProjectionStatus::AppliedOnTierTransition,
    ),
    (
        "shadows.enabled",
        QualityFieldProjectionStatus::AppliedOnTierTransition,
    ),
    (
        "shadows.cascade_count",
        QualityFieldProjectionStatus::AppliedOnTierTransition,
    ),
    (
        "shadows.map_resolution",
        QualityFieldProjectionStatus::AppliedOnTierTransition,
    ),
    (
        "shadows.maximum_distance_m",
        QualityFieldProjectionStatus::AppliedOnTierTransition,
    ),
    (
        "shadows.contact_shadows_enabled",
        QualityFieldProjectionStatus::AppliedOnTierTransition,
    ),
    (
        "water.reflection",
        QualityFieldProjectionStatus::AppliedOnTierTransition,
    ),
    (
        "water.reflection_steps",
        QualityFieldProjectionStatus::NotApplicable,
    ),
    (
        "rain.enabled",
        QualityFieldProjectionStatus::AppliedOnTierTransition,
    ),
    (
        "post_process.bloom_enabled",
        QualityFieldProjectionStatus::AppliedOnTierTransition,
    ),
    (
        "post_process.color_grading_enabled",
        QualityFieldProjectionStatus::AppliedOnTierTransition,
    ),
    (
        "post_process.tonemapping_enabled",
        QualityFieldProjectionStatus::AppliedOnTierTransition,
    ),
    (
        "vegetation.grass_blades_per_patch",
        QualityFieldProjectionStatus::AppliedAtStartup,
    ),
    (
        "environment.atmosphere_enabled",
        QualityFieldProjectionStatus::AppliedAtStartup,
    ),
    (
        "environment.environment_map_resolution",
        QualityFieldProjectionStatus::AppliedAtStartup,
    ),
    (
        "rain.drop_budget",
        QualityFieldProjectionStatus::AppliedAtStartup,
    ),
    (
        "rain.wetness_enabled",
        QualityFieldProjectionStatus::AppliedAtStartup,
    ),
    (
        "terrain.lod0_distance_m",
        QualityFieldProjectionStatus::NotApplicable,
    ),
    (
        "terrain.lod1_distance_m",
        QualityFieldProjectionStatus::NotApplicable,
    ),
    (
        "terrain.lod2_distance_m",
        QualityFieldProjectionStatus::NotApplicable,
    ),
    (
        "terrain.cull_distance_m",
        QualityFieldProjectionStatus::NotApplicable,
    ),
    (
        "terrain.lod1_sample_stride",
        QualityFieldProjectionStatus::NotApplicable,
    ),
    (
        "terrain.lod2_sample_stride",
        QualityFieldProjectionStatus::NotApplicable,
    ),
    (
        "vegetation.density_multiplier",
        QualityFieldProjectionStatus::NotApplicable,
    ),
    (
        "environment.volumetric_fog_enabled",
        QualityFieldProjectionStatus::NotApplicable,
    ),
    (
        "environment.volumetric_fog_step_count",
        QualityFieldProjectionStatus::NotApplicable,
    ),
    (
        "environment.volumetric_fog_max_distance_m",
        QualityFieldProjectionStatus::NotApplicable,
    ),
    (
        "water.reflection_max_distance_m",
        QualityFieldProjectionStatus::NotApplicable,
    ),
    (
        "water.refraction",
        QualityFieldProjectionStatus::NotApplicable,
    ),
    (
        "water.refraction_steps",
        QualityFieldProjectionStatus::NotApplicable,
    ),
    (
        "water.refraction_max_depth_m",
        QualityFieldProjectionStatus::NotApplicable,
    ),
];

fn quality_field_projections() -> Vec<QualityFieldProjection> {
    QUALITY_FIELD_PROJECTION_CONTRACT
        .iter()
        .map(|(field, status)| QualityFieldProjection {
            field,
            status: *status,
            evidence: match status {
                QualityFieldProjectionStatus::AppliedAtStartup => {
                    "startup_projection_requires_resource_or_mesh_rebuild_for_later_tiers"
                }
                QualityFieldProjectionStatus::AppliedOnTierTransition => {
                    "main_world_component_projection_gpu_execution_not_observed"
                }
                QualityFieldProjectionStatus::NotApplicable => {
                    "no_qualified_runtime_mapping_in_current_bevy_adapter"
                }
            },
        })
        .collect()
}

#[cfg(test)]
mod tests {
    use super::*;

    const QUALITY_JSON: &str =
        include_str!("../../../examples/visual_valley/quality-profile.v1.json");

    fn runtime(selection: VisualValleyQualitySelection) -> VisualValleyQualityRuntime {
        let profile = VisualValleyQualityProfileDocument::from_json(QUALITY_JSON).unwrap();
        let current_tier = selection.initial_tier(&profile);
        VisualValleyQualityRuntime {
            profile,
            selection,
            current_tier,
            last_applied_tier: None,
            observed_frames: 0,
            upgrade_frames: 0,
            downgrade_frames: 0,
            cooldown_remaining_frames: 0,
            transition_count: 0,
            last_transition: None,
        }
    }

    #[test]
    fn benchmark_is_fixed_high_even_under_sustained_slow_frames() {
        let mut runtime = runtime(VisualValleyQualitySelection::Benchmark);
        for _ in 0..1_000 {
            assert_eq!(runtime.observe_frame(40.0), None);
        }
        assert_eq!(runtime.current_tier, VisualQualityTier::High);
        assert_eq!(runtime.transition_count, 0);
    }

    #[test]
    fn explicit_fixed_tier_never_adapts() {
        let mut runtime = runtime(VisualValleyQualitySelection::Fixed(
            VisualQualityTier::Ultra,
        ));
        for _ in 0..1_000 {
            assert_eq!(runtime.observe_frame(40.0), None);
        }
        assert_eq!(runtime.current_tier, VisualQualityTier::Ultra);
    }

    #[test]
    fn adaptive_transitions_are_sustained_cooled_down_and_single_step() {
        let mut runtime = runtime(VisualValleyQualitySelection::Adaptive);
        for _ in 0..89 {
            assert_eq!(runtime.observe_frame(20.0), None);
        }
        let transition = runtime.observe_frame(20.0).unwrap();
        assert_eq!(transition.from, VisualQualityTier::High);
        assert_eq!(transition.to, VisualQualityTier::Medium);
        for _ in 0..240 {
            assert_eq!(runtime.observe_frame(20.0), None);
        }
        for _ in 0..89 {
            assert_eq!(runtime.observe_frame(20.0), None);
        }
        assert_eq!(
            runtime.observe_frame(20.0).unwrap().to,
            VisualQualityTier::Low
        );
    }

    #[test]
    fn runtime_tier_transition_updates_water_ledger_without_reenabling_ssr() {
        let profile = VisualValleyQualityProfileDocument::from_json(QUALITY_JSON).unwrap();
        let low = profile.preset(VisualQualityTier::Low).clone();
        let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
        let water = forge_world::embedded_visual_valley_water_document().unwrap();
        let mut app = App::new();
        app.add_plugins((MinimalPlugins, bevy::asset::AssetPlugin::default()));
        app.init_asset::<Mesh>();
        app.init_asset::<StandardMaterial>();
        let camera = app
            .world_mut()
            .spawn((
                Camera3d::default(),
                DeferredPrepass,
                ScreenSpaceReflections::default(),
            ))
            .id();
        app.add_plugins((
            crate::VisualValleyWaterPlugin::new(terrain, water)
                .with_quality_preset(low, VisualQualityTier::Low),
            VisualValleyQualityPlugin::new(
                profile,
                VisualValleyQualitySelection::Fixed(VisualQualityTier::Low),
            ),
        ));
        app.update();
        app.update();
        assert_eq!(
            app.world()
                .resource::<WaterProjectionStats>()
                .resolved_optics
                .requested_quality,
            forge_project::WaterQualityTier::Low
        );

        {
            let mut runtime = app.world_mut().resource_mut::<VisualValleyQualityRuntime>();
            runtime.current_tier = VisualQualityTier::Ultra;
        }
        app.update();
        app.update();

        let world = app.world();
        let water = world.resource::<WaterProjectionStats>();
        assert_eq!(
            water.resolved_optics.requested_quality,
            forge_project::WaterQualityTier::Ultra
        );
        assert_eq!(water.requested_reflection_max_distance_m, Some(128.0));
        assert_eq!(
            water.resolved_optics.reflection,
            ResolvedWaterReflectionPath::Disabled
        );
        assert!(world.get::<ScreenSpaceReflections>(camera).is_none());
        assert!(world.get::<DeferredPrepass>(camera).is_none());
    }

    #[test]
    fn projection_ledger_reports_every_profile_field_without_simulation() {
        let fields = quality_field_projections();
        assert_eq!(fields.len(), 34);
        assert!(fields.iter().any(|field| {
            field.field == "terrain.lod0_distance_m"
                && field.status == QualityFieldProjectionStatus::NotApplicable
        }));
        assert!(fields.iter().any(|field| {
            field.field == "shadows.map_resolution"
                && field.status == QualityFieldProjectionStatus::AppliedOnTierTransition
        }));
        assert!(fields.iter().any(|field| {
            field.field == "water.reflection_steps"
                && field.status == QualityFieldProjectionStatus::NotApplicable
        }));
    }

    #[test]
    #[allow(
        clippy::too_many_lines,
        reason = "one ECS scenario proves both directions and stable-tier change detection"
    )]
    fn tier_projection_is_reversible_and_stable_between_transitions() {
        let profile = VisualValleyQualityProfileDocument::from_json(QUALITY_JSON).unwrap();
        let weather = forge_world::embedded_visual_valley_weather_profile().unwrap();
        let expected_high_bloom = visual_valley_bloom_component(&weather);
        let weather_hash = weather.content_hash().unwrap().to_string();
        let weather_state =
            WeatherRuntimeState::at_state(&weather, forge_project::WeatherState::Rain);
        let mut app = App::new();
        app.add_plugins(MinimalPlugins)
            .insert_resource(DirectionalLightShadowMap::default())
            .insert_resource(VisualValleyWeatherProfile {
                document: weather,
                source_hash: weather_hash,
            })
            .insert_resource(weather_state)
            .add_plugins(VisualValleyQualityPlugin::new(
                profile,
                VisualValleyQualitySelection::Fixed(VisualQualityTier::Low),
            ));
        let sun = app
            .world_mut()
            .spawn((
                VisualValleySun,
                DirectionalLight {
                    shadow_maps_enabled: true,
                    ..default()
                },
                CascadeShadowConfigBuilder::default().build(),
            ))
            .id();
        let camera = app
            .world_mut()
            .spawn((
                Camera3d::default(),
                ScreenSpaceReflections::default(),
                Bloom::NATURAL,
            ))
            .id();
        let rain = app
            .world_mut()
            .spawn((VisualValleyRainBatch, Visibility::Hidden))
            .id();

        app.update();

        let world = app.world();
        let light = world.get::<DirectionalLight>(sun).unwrap();
        let cascades = world.get::<CascadeShadowConfig>(sun).unwrap();
        assert!(light.shadow_maps_enabled);
        assert_eq!(cascades.bounds.len(), 2);
        assert_eq!(cascades.bounds.last().copied(), Some(64.0));
        assert_eq!(world.resource::<DirectionalLightShadowMap>().size, 1_024);
        assert!(world.get::<ContactShadows>(camera).is_none());
        assert!(world.get::<ScreenSpaceReflections>(camera).is_none());
        assert!(world.get::<Bloom>(camera).is_none());
        assert!(world.get::<ColorGrading>(camera).is_some());
        assert_eq!(
            world.get::<Tonemapping>(camera),
            Some(&Tonemapping::TonyMcMapface)
        );
        assert_eq!(world.get::<Visibility>(rain), Some(&Visibility::Visible));
        let stats = world.resource::<VisualValleyQualityStats>();
        assert_eq!(stats.current_tier, VisualQualityTier::Low);
        assert_eq!(stats.dynamically_applied_tier, Some(VisualQualityTier::Low));
        assert_eq!(
            stats.profile_hash,
            "8dc219ad88ba1472b0ebbb13d8f4beac13f3fae1eeff688c928abb044d96d0e2"
        );
        let ledger = serde_json::to_value(stats).unwrap();
        assert_eq!(ledger["current_tier"], "low");
        assert_eq!(ledger["selection"]["mode"], "fixed");

        app.world_mut()
            .resource_mut::<VisualValleyQualityRuntime>()
            .current_tier = VisualQualityTier::High;
        app.update();

        let world = app.world();
        let bloom = world.get::<Bloom>(camera).unwrap();
        assert_eq!(
            bloom.intensity.to_bits(),
            expected_high_bloom.intensity.to_bits()
        );
        assert_eq!(
            bloom.low_frequency_boost.to_bits(),
            expected_high_bloom.low_frequency_boost.to_bits()
        );
        assert_eq!(
            bloom.high_pass_frequency.to_bits(),
            expected_high_bloom.high_pass_frequency.to_bits()
        );
        assert!(world.get::<ColorGrading>(camera).is_some());
        assert!(world.get::<ContactShadows>(camera).is_some());
        let cascades = world.get::<CascadeShadowConfig>(sun).unwrap();
        assert_eq!(cascades.bounds.len(), 4);
        assert!((cascades.bounds.last().unwrap() - 128.0).abs() <= 0.001);
        assert!(world.get::<ScreenSpaceReflections>(camera).is_none());
        assert!(world.get::<DeferredPrepass>(camera).is_none());
        assert_eq!(
            world
                .resource::<VisualValleyQualityStats>()
                .dynamically_applied_tier,
            Some(VisualQualityTier::High)
        );

        app.world_mut().clear_trackers();
        app.update();
        let world = app.world();
        assert!(
            !world
                .entity(sun)
                .get_ref::<DirectionalLight>()
                .unwrap()
                .is_changed()
        );
        assert!(
            !world
                .entity(camera)
                .get_ref::<Bloom>()
                .unwrap()
                .is_changed()
        );
    }
}
