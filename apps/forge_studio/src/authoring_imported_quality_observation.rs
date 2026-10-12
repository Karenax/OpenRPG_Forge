//! On-demand main-world inventory of the imported renderer's current profile.
//! This observes existing state only: no preset, renderer mutation, or GPU fence.
use forge_core::{CanonicalJsonError, ContentHash, canonical_json_sha256};
use forge_project::authoring::playtest::PlaytestIdentity;
use serde::Serialize;
use std::collections::BTreeMap;

#[derive(Clone, Debug, PartialEq, Serialize)]
#[serde(tag = "type", content = "value", rename_all = "snake_case")]
enum ParameterValue {
    Boolean(bool),
    Count(usize),
    Unsigned(u64),
    Number(f64),
    Numbers(Vec<f64>),
    NumberRows(Vec<Vec<f64>>),
    RawWordRows(Vec<Vec<u32>>),
    Dimensions(Vec<u32>),
    Text(String),
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize)]
#[serde(rename_all = "snake_case")]
enum Origin {
    MainWorldComponent,
    MainWorldResource,
    CpuMaterialUniform,
    CpuImageDescriptor,
    AsyncGpuReadback,
    Unavailable,
}

#[derive(Clone, Debug, PartialEq, Serialize)]
struct Observation {
    value: Option<ParameterValue>,
    origin: Origin,
    /// Explicit allowlist for the fingerprint; pose/counters never enter it.
    profile_setting: bool,
    unavailable: Option<&'static str>,
}

impl Observation {
    fn unavailable(reason: &'static str, profile_setting: bool) -> Self {
        Self {
            value: None,
            origin: Origin::Unavailable,
            profile_setting,
            unavailable: Some(reason),
        }
    }

    fn value(value: ParameterValue, origin: Origin, profile_setting: bool) -> Self {
        let finite = match &value {
            ParameterValue::Number(value) => value.is_finite(),
            ParameterValue::Numbers(values) => values.iter().all(|value| value.is_finite()),
            ParameterValue::NumberRows(rows) => {
                rows.iter().flatten().all(|value| value.is_finite())
            }
            _ => true,
        };
        if !finite {
            return Self::unavailable("non_finite_parameter", profile_setting);
        }
        Self {
            value: Some(value),
            origin,
            profile_setting,
            unavailable: None,
        }
    }
}

/// Transient DTO. The caller captures and serializes it only at collection boundaries.
#[derive(Clone, Debug, Serialize)]
pub(crate) struct CurrentProfileInventory {
    schema_version: u32,
    kind: &'static str,
    /// Supplied launch request, distinct from the resident source observed below.
    pub(crate) expected_identity_request: Option<PlaytestIdentity>,
    pub(crate) frame: Option<u32>,
    observations: BTreeMap<String, Observation>,
    gpu_completion: Option<bool>,
    presentation: Option<bool>,
    limits: [&'static str; 7],
}

impl CurrentProfileInventory {
    fn new(expected: Option<PlaytestIdentity>, frame: Option<u32>) -> Self {
        let observations = [
            ("camera/entity", "no_unique_active_player_camera"),
            ("camera/physical_viewport_px", "camera_target_unavailable"),
            (
                "authoring/archive_hash",
                "not_supplied_to_renderer_inventory",
            ),
            ("authoring/head_hash", "not_supplied_to_renderer_inventory"),
            ("terrain/source_hash", "resident_terrain_unavailable"),
            ("terrain/cdlod_config", "private_adapter_configuration"),
            ("terrain/lod_morph_uniform", "resident_material_unavailable"),
            (
                "vegetation/source_generation",
                "private_population_generation",
            ),
            (
                "vegetation/density_multiplier",
                "no_observed_density_policy",
            ),
            (
                "vegetation/lod_uniforms",
                "plant_material_store_unavailable",
            ),
            (
                "shadows/contact_shadow_budget",
                "no_imported_contact_shadow_observer",
            ),
            (
                "shadows/live_receiver_readback",
                "no_current_readback_sample",
            ),
            (
                "environment/policy_budgets",
                "no_imported_quality_policy_observer",
            ),
            (
                "water/optical_uniforms",
                "private_optical_material_uniforms",
            ),
            ("water/reflection_step_budget", "no_public_budget_observer"),
            ("water/refraction_step_budget", "no_public_budget_observer"),
            ("rain/drop_budget", "no_imported_rain_budget_observer"),
            ("rain/wetness", "no_imported_wetness_observer"),
            ("post/parameters", "no_unique_active_player_camera"),
            ("post/bloom_intensity", "component_unavailable"),
            ("post/fxaa_enabled", "component_unavailable"),
            ("post/tonemapping", "component_unavailable"),
            (
                "post/grading_exposure_saturation_contrast",
                "component_unavailable",
            ),
            ("post/motion_blur_shutter_angle", "component_unavailable"),
            ("post/gtao_strength", "component_unavailable"),
            ("post/lens_flare_strength", "component_unavailable"),
            ("post/film_grain_intensity", "component_unavailable"),
        ]
        .into_iter()
        .map(|(name, reason)| (name.to_owned(), Observation::unavailable(reason, true)))
        .collect();
        Self {
            schema_version: 1,
            kind: "forge.studio.imported-current-profile",
            expected_identity_request: expected,
            frame,
            observations,
            gpu_completion: None,
            presentation: None,
            limits: [
                "inventory_is_not_a_quality_tier_or_worldsketch_equivalence",
                "main_world_components_and_material_uniforms_are_cpu_state",
                "image_dimensions_are_cpu_descriptors_not_gpu_residency",
                "producer_ready_and_step_counters_are_not_gpu_completion",
                "readbacks_can_lag_and_are_not_bound_to_this_capture_frame",
                "fingerprint_covers_observed_settings_only_not_unavailable_private_budgets",
                "selected_public_parameters_only_not_a_complete_renderer_capability_inventory",
            ],
        }
    }

    /// Stable across ordinary movement/animation/ticks. Unknown parameters remain unknown.
    pub(crate) fn fingerprint(&self) -> Result<ContentHash, CanonicalJsonError> {
        #[derive(Serialize)]
        struct Profile<'a> {
            schema_version: u32,
            kind: &'static str,
            expected_identity_request: Option<PlaytestIdentity>,
            observed_settings: BTreeMap<&'a str, &'a Observation>,
        }
        canonical_json_sha256(&Profile {
            schema_version: self.schema_version,
            kind: self.kind,
            expected_identity_request: self.expected_identity_request,
            observed_settings: self
                .observations
                .iter()
                .filter(|(_, item)| item.profile_setting)
                .map(|(name, item)| (name.as_str(), item))
                .collect(),
        })
    }
}

#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod adapter;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod camera;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod domains;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
pub(crate) use adapter::snapshot;

#[cfg(test)]
mod tests;
