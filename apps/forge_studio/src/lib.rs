#![deny(unsafe_code)]
#![cfg_attr(
    not(target_arch = "wasm32"),
    allow(
        linker_messages,
        reason = "MSVC reports its normal import-library creation message for the cdylib"
    )
)]

pub mod area_preview;
pub mod area_tabs;
pub mod desktop_state;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod desktop_ui;
pub mod workspace_api;
#[cfg(not(target_arch = "wasm32"))]
pub mod workspace_service;
#[cfg(not(target_arch = "wasm32"))]
mod workspace_store;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod workspace_ui;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod world_ui;

mod gate1_browser_weather_matrix;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod gate1_browser_weather_matrix_web;

#[cfg(all(target_arch = "wasm32", feature = "web"))]
use gate1_browser_weather_matrix::{BrowserWeatherProofLedger, browser_weather_proof_camera_pose};
pub use gate1_browser_weather_matrix::{
    StudioGate1WeatherProofQueryError, StudioGate1WeatherProofSelection,
    parse_studio_gate1_weather_proof_selection,
};

use forge_core::EntityId;
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
use forge_core::canonical_json_sha256;
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
use forge_project::{
    EnvironmentProfileDocument, PlayerControllerProfileDocument, SceneProofDocument,
    SceneProofKind, TerrainDocument, VegetationDocument, VisualValleyQualityProfileDocument,
    WaterDocument, WeatherProfileDocument,
};
use forge_project::{VisualQualityTier, WeatherState};
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
use forge_world::{
    TerrainGenerationPlan, VegetationGenerationPlan, embedded_gate0_proof_document,
    embedded_visual_valley_environment_profile, embedded_visual_valley_player_controller_profile,
    embedded_visual_valley_terrain_document, embedded_visual_valley_vegetation_document,
    embedded_visual_valley_water_document, embedded_visual_valley_weather_profile,
};
use serde::{Deserialize, Serialize};

const STUDIO_SCENE_QUERY_KEY: &str = "forge_scene";
const STUDIO_WEATHER_QUERY_KEY: &str = "forge_weather";
const STUDIO_QUALITY_QUERY_KEY: &str = "forge_quality";
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
const VISUAL_VALLEY_SCENERY_MANIFEST_JSON: &str = include_str!(
    "../../../assets/visual_valley/scenery/quaternius/stylized-nature-megakit-standard-1.0/subset.manifest.json"
);

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum StudioScene {
    #[default]
    Gate0,
    VisualValley,
}

impl StudioScene {
    #[must_use]
    pub const fn mode(self) -> StudioViewportMode {
        match self {
            Self::Gate0 => StudioViewportMode::Gate0SceneProof,
            Self::VisualValley => StudioViewportMode::Gate1VisualValley,
        }
    }

    #[must_use]
    pub const fn query_value(self) -> &'static str {
        match self {
            Self::Gate0 => "gate0",
            Self::VisualValley => "visual-valley",
        }
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum StudioViewportMode {
    Gate0SceneProof,
    Gate1VisualValley,
}

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum StudioSceneSelectionSource {
    #[default]
    Default,
    QueryString,
    UnknownQueryFallback,
}

#[derive(Clone, Debug, Default, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct StudioSceneSelection {
    pub scene: StudioScene,
    pub source: StudioSceneSelectionSource,
    pub requested_value: Option<String>,
}

#[must_use]
pub fn parse_studio_scene_selection(search: &str) -> StudioSceneSelection {
    let requested_value = search
        .trim_start_matches('?')
        .split('&')
        .filter_map(|entry| entry.split_once('='))
        .find_map(|(key, value)| (key == STUDIO_SCENE_QUERY_KEY).then(|| value.to_owned()));

    match requested_value.as_deref() {
        None => StudioSceneSelection::default(),
        Some("gate0") => StudioSceneSelection {
            scene: StudioScene::Gate0,
            source: StudioSceneSelectionSource::QueryString,
            requested_value,
        },
        Some("visual-valley") => StudioSceneSelection {
            scene: StudioScene::VisualValley,
            source: StudioSceneSelectionSource::QueryString,
            requested_value,
        },
        Some(_) => StudioSceneSelection {
            scene: StudioScene::Gate0,
            source: StudioSceneSelectionSource::UnknownQueryFallback,
            requested_value,
        },
    }
}

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum StudioWeatherSelectionSource {
    #[default]
    Default,
    QueryString,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct StudioWeatherSelection {
    pub state: WeatherState,
    pub source: StudioWeatherSelectionSource,
    pub requested_value: Option<String>,
}

impl Default for StudioWeatherSelection {
    fn default() -> Self {
        Self {
            state: WeatherState::Clear,
            source: StudioWeatherSelectionSource::Default,
            requested_value: None,
        }
    }
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case")]
pub enum StudioWeatherQueryError {
    DuplicateParameter { requested_values: Vec<String> },
    UnsupportedValue { requested_value: String },
    NotApplicableToScene { scene: StudioScene },
}

impl std::fmt::Display for StudioWeatherQueryError {
    fn fmt(&self, formatter: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Self::DuplicateParameter { requested_values } => write!(
                formatter,
                "le paramètre forge_weather doit apparaître une seule fois (valeurs reçues : {})",
                requested_values.join(", ")
            ),
            Self::UnsupportedValue { requested_value } => write!(
                formatter,
                "valeur forge_weather non prise en charge : {requested_value:?} (attendu : clear ou rain)"
            ),
            Self::NotApplicableToScene { scene } => write!(
                formatter,
                "forge_weather est réservé à visual-valley (scène sélectionnée : {})",
                scene.query_value()
            ),
        }
    }
}

impl std::error::Error for StudioWeatherQueryError {}

pub fn parse_studio_weather_selection(
    search: &str,
    scene: StudioScene,
) -> Result<StudioWeatherSelection, StudioWeatherQueryError> {
    let requested_values = search
        .trim_start_matches('?')
        .split('&')
        .map(|entry| entry.split_once('=').unwrap_or((entry, "")))
        .filter(|(key, _)| *key == STUDIO_WEATHER_QUERY_KEY)
        .map(|(_, value)| value.to_owned())
        .collect::<Vec<_>>();

    match requested_values.as_slice() {
        [] => Ok(StudioWeatherSelection::default()),
        [_] if scene != StudioScene::VisualValley => {
            Err(StudioWeatherQueryError::NotApplicableToScene { scene })
        }
        [requested_value] => {
            let state = match requested_value.as_str() {
                "clear" => WeatherState::Clear,
                "rain" => WeatherState::Rain,
                _ => {
                    return Err(StudioWeatherQueryError::UnsupportedValue {
                        requested_value: requested_value.clone(),
                    });
                }
            };
            Ok(StudioWeatherSelection {
                state,
                source: StudioWeatherSelectionSource::QueryString,
                requested_value: Some(requested_value.clone()),
            })
        }
        _ => Err(StudioWeatherQueryError::DuplicateParameter { requested_values }),
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(tag = "mode", content = "tier", rename_all = "snake_case")]
pub enum StudioQualitySelection {
    Fixed(VisualQualityTier),
    Adaptive,
    Benchmark,
}

impl Default for StudioQualitySelection {
    fn default() -> Self {
        Self::Fixed(VisualQualityTier::High)
    }
}

impl StudioQualitySelection {
    #[must_use]
    pub const fn initial_tier(self) -> VisualQualityTier {
        match self {
            Self::Fixed(tier) => tier,
            Self::Adaptive | Self::Benchmark => VisualQualityTier::High,
        }
    }
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case")]
pub enum StudioQualityQueryError {
    DuplicateParameter { requested_values: Vec<String> },
    UnsupportedValue { requested_value: String },
    NotApplicableToScene { scene: StudioScene },
}

impl std::fmt::Display for StudioQualityQueryError {
    fn fmt(&self, formatter: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Self::DuplicateParameter { requested_values } => write!(
                formatter,
                "le paramètre forge_quality doit apparaître une seule fois (valeurs reçues : {})",
                requested_values.join(", ")
            ),
            Self::UnsupportedValue { requested_value } => write!(
                formatter,
                "valeur forge_quality non prise en charge : {requested_value:?} (attendu : low, medium, high, ultra, adaptive ou benchmark)"
            ),
            Self::NotApplicableToScene { scene } => write!(
                formatter,
                "forge_quality est réservé à visual-valley (scène sélectionnée : {})",
                scene.query_value()
            ),
        }
    }
}

impl std::error::Error for StudioQualityQueryError {}

pub fn parse_studio_quality_selection(
    search: &str,
    scene: StudioScene,
) -> Result<StudioQualitySelection, StudioQualityQueryError> {
    let requested_values = search
        .trim_start_matches('?')
        .split('&')
        .map(|entry| entry.split_once('=').unwrap_or((entry, "")))
        .filter(|(key, _)| *key == STUDIO_QUALITY_QUERY_KEY)
        .map(|(_, value)| value.to_owned())
        .collect::<Vec<_>>();
    match requested_values.as_slice() {
        [] => Ok(StudioQualitySelection::default()),
        [_] if scene != StudioScene::VisualValley => {
            Err(StudioQualityQueryError::NotApplicableToScene { scene })
        }
        [value] => match value.as_str() {
            "low" => Ok(StudioQualitySelection::Fixed(VisualQualityTier::Low)),
            "medium" => Ok(StudioQualitySelection::Fixed(VisualQualityTier::Medium)),
            "high" => Ok(StudioQualitySelection::Fixed(VisualQualityTier::High)),
            "ultra" => Ok(StudioQualitySelection::Fixed(VisualQualityTier::Ultra)),
            "adaptive" => Ok(StudioQualitySelection::Adaptive),
            "benchmark" => Ok(StudioQualitySelection::Benchmark),
            _ => Err(StudioQualityQueryError::UnsupportedValue {
                requested_value: value.clone(),
            }),
        },
        _ => Err(StudioQualityQueryError::DuplicateParameter { requested_values }),
    }
}

#[derive(Clone, Debug, Default, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct StudioSceneSourceHashes {
    pub scene_proof: Option<String>,
    pub terrain: Option<String>,
    pub vegetation: Option<String>,
    pub environment: Option<String>,
    pub player_controller: Option<String>,
    pub water: Option<String>,
    pub weather: Option<String>,
    pub scenery: Option<String>,
    pub quality: Option<String>,
}

impl StudioSceneSourceHashes {
    #[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
    fn populated_count(&self) -> usize {
        [
            &self.scene_proof,
            &self.terrain,
            &self.vegetation,
            &self.environment,
            &self.player_controller,
            &self.water,
            &self.weather,
            &self.scenery,
            &self.quality,
        ]
        .into_iter()
        .filter(|hash| hash.is_some())
        .count()
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum StudioWaterAdapterStatus {
    NotApplicable,
    InstalledEngineAdapter,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum StudioCameraOwner {
    Gate0ProofDocument,
    VisualValleyPlayerController,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct StudioSceneManifest {
    pub mode: StudioViewportMode,
    pub scene: StudioScene,
    pub display_name: String,
    pub source_hashes: StudioSceneSourceHashes,
    pub camera_owner: StudioCameraOwner,
    pub expected_active_camera_count: u32,
    pub water_adapter: StudioWaterAdapterStatus,
    pub weather_state: Option<WeatherState>,
    pub quality_selection: Option<StudioQualitySelection>,
    pub quality_tier: Option<VisualQualityTier>,
}

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum StudioSceneParityStatus {
    #[default]
    AwaitingRuntime,
    SourceManifestMatched,
    SourceManifestMismatch,
    RuntimeContractMismatch,
    ConfigurationError,
}

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum StudioGpuPresentationStatus {
    #[default]
    NotObserved,
}

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct StudioSceneryInstanceCounts {
    pub trees: u32,
    pub shrubs: u32,
    pub ferns: u32,
    pub rocks: u32,
    pub details: u32,
}

impl StudioSceneryInstanceCounts {
    fn replacement_instances(self) -> Option<usize> {
        u64::from(self.trees)
            .checked_add(u64::from(self.shrubs))?
            .checked_add(u64::from(self.ferns))?
            .try_into()
            .ok()
    }

    fn detail_instances(self) -> Option<usize> {
        u64::from(self.rocks)
            .checked_add(u64::from(self.details))?
            .try_into()
            .ok()
    }

    fn requested_instances(self) -> Option<usize> {
        self.replacement_instances()?
            .checked_add(self.detail_instances()?)
    }

    fn hidden_procedural_entities(self) -> Option<usize> {
        self.trees.try_into().ok()
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct StudioSceneryRuntimeObservation {
    pub status: String,
    pub loaded_models: usize,
    pub failed_models: usize,
    pub unique_models: usize,
    pub requested_instances: usize,
    pub replacement_instances: usize,
    pub detail_instances: usize,
    pub selected_by_kind: StudioSceneryInstanceCounts,
    pub hidden_procedural_entities: usize,
    pub tracked_files: usize,
    pub tracked_bytes: u64,
    pub replacement_lod_start_m: f32,
    pub replacement_lod_end_m: f32,
    pub detail_cull_distance_m: f32,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct StudioSceneRuntimeObservation {
    pub first_real_frame_ms: f64,
    pub runtime_ready_ms: f64,
    pub camera_3d_entities: u32,
    pub active_camera_3d_entities: u32,
    pub water_observation: Option<String>,
    pub weather_observation: Option<String>,
    pub weather_target_state: Option<String>,
    pub quality_current_tier: Option<VisualQualityTier>,
    pub quality_applied_tier: Option<VisualQualityTier>,
    pub quality_replacement_lod_start_m: Option<f32>,
    pub quality_replacement_lod_end_m: Option<f32>,
    pub scenery_observation: Option<StudioSceneryRuntimeObservation>,
    pub gpu_presentation: StudioGpuPresentationStatus,
    pub evidence_source: String,
}

#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
struct FrameQualityFingerprint {
    scene: StudioScene,
    current_tier: Option<VisualQualityTier>,
    applied_tier: Option<VisualQualityTier>,
    quality_lod_start_bits: Option<u32>,
    quality_lod_end_bits: Option<u32>,
    scenery_lod_start_bits: Option<u32>,
    scenery_lod_end_bits: Option<u32>,
}

#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
impl FrameQualityFingerprint {
    const fn gate0() -> Self {
        Self {
            scene: StudioScene::Gate0,
            current_tier: None,
            applied_tier: None,
            quality_lod_start_bits: None,
            quality_lod_end_bits: None,
            scenery_lod_start_bits: None,
            scenery_lod_end_bits: None,
        }
    }

    fn visual_valley(
        current_tier: Option<VisualQualityTier>,
        applied_tier: Option<VisualQualityTier>,
        quality_lod_start_m: Option<f32>,
        quality_lod_end_m: Option<f32>,
        scenery_lod_start_m: Option<f32>,
        scenery_lod_end_m: Option<f32>,
    ) -> Self {
        Self {
            scene: StudioScene::VisualValley,
            current_tier,
            applied_tier,
            quality_lod_start_bits: quality_lod_start_m.map(f32::to_bits),
            quality_lod_end_bits: quality_lod_end_m.map(f32::to_bits),
            scenery_lod_start_bits: scenery_lod_start_m.map(f32::to_bits),
            scenery_lod_end_bits: scenery_lod_end_m.map(f32::to_bits),
        }
    }

    fn is_converged(self) -> bool {
        match self.scene {
            StudioScene::Gate0 => {
                self.current_tier.is_none()
                    && self.applied_tier.is_none()
                    && self.quality_lod_start_bits.is_none()
                    && self.quality_lod_end_bits.is_none()
                    && self.scenery_lod_start_bits.is_none()
                    && self.scenery_lod_end_bits.is_none()
            }
            StudioScene::VisualValley => {
                self.current_tier.is_some()
                    && self.current_tier == self.applied_tier
                    && valid_fingerprint_lod_range(
                        self.quality_lod_start_bits,
                        self.quality_lod_end_bits,
                    )
                    && valid_fingerprint_lod_range(
                        self.scenery_lod_start_bits,
                        self.scenery_lod_end_bits,
                    )
                    && self.quality_lod_start_bits == self.scenery_lod_start_bits
                    && self.quality_lod_end_bits == self.scenery_lod_end_bits
            }
        }
    }
}

#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
fn valid_fingerprint_lod_range(start_bits: Option<u32>, end_bits: Option<u32>) -> bool {
    let (Some(start_bits), Some(end_bits)) = (start_bits, end_bits) else {
        return false;
    };
    let start_m = f32::from_bits(start_bits);
    let end_m = f32::from_bits(end_bits);
    start_m.is_finite() && end_m.is_finite() && start_m > 0.0 && end_m >= start_m
}

#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
enum FrameSamplingProgress {
    Reset,
    WarmingUp,
    Measuring,
    Complete,
}

#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
#[derive(Debug)]
struct StableQualityFrameWindow {
    fingerprint: Option<FrameQualityFingerprint>,
    warmup_samples: usize,
    samples_ms: Vec<f64>,
}

#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
impl StableQualityFrameWindow {
    fn new() -> Self {
        Self {
            fingerprint: None,
            warmup_samples: 0,
            samples_ms: Vec::with_capacity(telemetry::FRAME_MEASURED_SAMPLES),
        }
    }

    fn record_frame(
        &mut self,
        fingerprint: FrameQualityFingerprint,
        delta_ms: f64,
    ) -> FrameSamplingProgress {
        let fingerprint_changed = self.fingerprint.is_some_and(|stable| stable != fingerprint);
        if !delta_ms.is_finite()
            || delta_ms <= 0.0
            || !fingerprint.is_converged()
            || fingerprint_changed
        {
            self.reset();
            return FrameSamplingProgress::Reset;
        }
        if self.fingerprint.is_none() {
            self.fingerprint = Some(fingerprint);
        }
        if self.warmup_samples < telemetry::FRAME_WARMUP_SAMPLES {
            self.warmup_samples += 1;
            return FrameSamplingProgress::WarmingUp;
        }
        self.samples_ms.push(delta_ms);
        if self.samples_ms.len() < telemetry::FRAME_MEASURED_SAMPLES {
            FrameSamplingProgress::Measuring
        } else {
            FrameSamplingProgress::Complete
        }
    }

    fn reset(&mut self) {
        self.fingerprint = None;
        self.warmup_samples = 0;
        self.samples_ms.clear();
    }

    const fn warmup_samples(&self) -> usize {
        self.warmup_samples
    }

    fn samples_ms(&self) -> &[f64] {
        &self.samples_ms
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct StudioSceneTelemetry {
    pub schema: String,
    pub schema_version: u32,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub build_source_commit: Option<String>,
    pub selection: StudioSceneSelection,
    pub selected_manifest: StudioSceneManifest,
    pub runtime_manifest: Option<StudioSceneManifest>,
    pub parity_status: StudioSceneParityStatus,
    pub runtime_observation: Option<StudioSceneRuntimeObservation>,
    pub failure_reason: Option<String>,
    pub limitations: Vec<String>,
}

impl StudioSceneTelemetry {
    #[must_use]
    pub fn new(selection: StudioSceneSelection, selected_manifest: StudioSceneManifest) -> Self {
        Self {
            schema: "forge.studio-scene-telemetry".to_owned(),
            schema_version: 8,
            build_source_commit: option_env!("FORGE_SOURCE_COMMIT")
                .filter(|value| {
                    value.len() == 40 && value.bytes().all(|byte| byte.is_ascii_hexdigit())
                })
                .map(str::to_owned),
            selection,
            selected_manifest,
            runtime_manifest: None,
            parity_status: StudioSceneParityStatus::AwaitingRuntime,
            runtime_observation: None,
            failure_reason: None,
            limitations: vec![
                "A native or wasm32 build does not prove browser WebGPU execution.".to_owned(),
                "The runtime-ready event observes Bevy CPU/ECS state; GPU presentation remains not_observed until a dedicated presentation probe exists."
                    .to_owned(),
            ],
        }
    }

    pub fn observe_runtime(
        &mut self,
        runtime_manifest: StudioSceneManifest,
        runtime_observation: StudioSceneRuntimeObservation,
    ) {
        let source_manifest_mismatch =
            self.selected_manifest.source_hashes != runtime_manifest.source_hashes;
        let runtime_contract_error = validate_runtime_contract(
            &self.selected_manifest,
            &runtime_manifest,
            &runtime_observation,
        )
        .err();
        self.parity_status = if source_manifest_mismatch {
            StudioSceneParityStatus::SourceManifestMismatch
        } else if runtime_contract_error.is_some() {
            StudioSceneParityStatus::RuntimeContractMismatch
        } else {
            StudioSceneParityStatus::SourceManifestMatched
        };
        self.failure_reason = if source_manifest_mismatch {
            Some(
                "les hashes des sources sélectionnées et observées dans le runtime divergent"
                    .to_owned(),
            )
        } else {
            runtime_contract_error
        };
        self.runtime_manifest = Some(runtime_manifest);
        self.runtime_observation = Some(runtime_observation);
    }

    pub fn fail_configuration(&mut self, reason: impl Into<String>) {
        self.parity_status = StudioSceneParityStatus::ConfigurationError;
        self.failure_reason = Some(reason.into());
    }

    pub fn fail_runtime(&mut self, reason: impl Into<String>) {
        self.parity_status = StudioSceneParityStatus::RuntimeContractMismatch;
        self.failure_reason = Some(reason.into());
    }
}

fn validate_runtime_contract(
    selected: &StudioSceneManifest,
    runtime: &StudioSceneManifest,
    observation: &StudioSceneRuntimeObservation,
) -> Result<(), String> {
    validate_runtime_timestamps(observation)?;
    validate_runtime_manifest(selected, runtime)?;
    validate_runtime_camera_topology(selected, observation)?;

    if selected.scene == StudioScene::VisualValley {
        validate_visual_valley_runtime_contract(selected, runtime, observation)
    } else {
        validate_gate0_quality_contract(observation)
    }
}

fn validate_runtime_timestamps(observation: &StudioSceneRuntimeObservation) -> Result<(), String> {
    if !observation.first_real_frame_ms.is_finite()
        || !observation.runtime_ready_ms.is_finite()
        || observation.first_real_frame_ms < 0.0
        || observation.runtime_ready_ms < observation.first_real_frame_ms
    {
        return Err("horodatages runtime non finis, négatifs ou désordonnés".to_owned());
    }
    Ok(())
}

fn validate_runtime_manifest(
    selected: &StudioSceneManifest,
    runtime: &StudioSceneManifest,
) -> Result<(), String> {
    let quality_tier_mismatch = selected.quality_tier != runtime.quality_tier
        && selected.quality_selection != Some(StudioQualitySelection::Adaptive);
    if selected.mode != runtime.mode
        || selected.scene != runtime.scene
        || selected.display_name != runtime.display_name
        || selected.camera_owner != runtime.camera_owner
        || selected.expected_active_camera_count != runtime.expected_active_camera_count
        || selected.water_adapter != runtime.water_adapter
        || selected.quality_selection != runtime.quality_selection
        || quality_tier_mismatch
    {
        return Err(
            "le runtime observé ne correspond pas au mode, à l’identité, à la caméra ou aux adapters sélectionnés"
                .to_owned(),
        );
    }
    if selected.weather_state != runtime.weather_state {
        return Err(format!(
            "la météo observée {:?} diverge de la météo sélectionnée {:?}",
            runtime.weather_state, selected.weather_state
        ));
    }
    Ok(())
}

fn validate_runtime_camera_topology(
    selected: &StudioSceneManifest,
    observation: &StudioSceneRuntimeObservation,
) -> Result<(), String> {
    if observation.camera_3d_entities != selected.expected_active_camera_count
        || observation.active_camera_3d_entities != selected.expected_active_camera_count
    {
        return Err(format!(
            "topologie caméra invalide : {} caméra(s) 3D, {} active(s), {} attendue(s)",
            observation.camera_3d_entities,
            observation.active_camera_3d_entities,
            selected.expected_active_camera_count
        ));
    }
    Ok(())
}

fn validate_visual_valley_runtime_contract(
    selected: &StudioSceneManifest,
    runtime: &StudioSceneManifest,
    observation: &StudioSceneRuntimeObservation,
) -> Result<(), String> {
    validate_visual_valley_selection(selected)?;
    let quality_lod_range = validate_visual_valley_quality(runtime, observation)?;
    validate_visual_valley_water_and_weather(selected, observation)?;
    validate_visual_valley_scenery(observation, quality_lod_range)
}

fn validate_visual_valley_selection(selected: &StudioSceneManifest) -> Result<(), String> {
    if selected.source_hashes.quality.is_none()
        || selected.quality_selection.is_none()
        || selected.quality_tier.is_none()
    {
        return Err("profil qualité absent de la sélection Visual Valley".to_owned());
    }
    if selected.source_hashes.scenery.is_none() {
        return Err("hash du manifeste de décor CC0 absent de la sélection".to_owned());
    }
    Ok(())
}

fn validate_visual_valley_quality(
    runtime: &StudioSceneManifest,
    observation: &StudioSceneRuntimeObservation,
) -> Result<(f32, f32), String> {
    let Some(runtime_quality_tier) = runtime.quality_tier else {
        return Err("tier qualité absent du manifeste runtime Visual Valley".to_owned());
    };
    if observation.quality_current_tier != Some(runtime_quality_tier)
        || observation.quality_applied_tier != Some(runtime_quality_tier)
    {
        return Err(format!(
            "application qualité runtime invalide : tier courant={:?}, appliqué={:?}, manifeste={runtime_quality_tier:?}",
            observation.quality_current_tier, observation.quality_applied_tier
        ));
    }
    let Some(quality_lod_start_m) = observation.quality_replacement_lod_start_m else {
        return Err("LOD de remplacement attendu du preset qualité absent".to_owned());
    };
    let Some(quality_lod_end_m) = observation.quality_replacement_lod_end_m else {
        return Err("HLOD de remplacement attendu du preset qualité absent".to_owned());
    };
    if !quality_lod_start_m.is_finite()
        || !quality_lod_end_m.is_finite()
        || quality_lod_start_m <= 0.0
        || quality_lod_end_m < quality_lod_start_m
    {
        return Err(format!(
            "plage LOD du preset qualité invalide : {quality_lod_start_m}-{quality_lod_end_m} m"
        ));
    }
    Ok((quality_lod_start_m, quality_lod_end_m))
}

fn validate_visual_valley_water_and_weather(
    selected: &StudioSceneManifest,
    observation: &StudioSceneRuntimeObservation,
) -> Result<(), String> {
    if observation.water_observation.as_deref() != Some("observed_main_world_components") {
        return Err(format!(
            "projection eau invalide : {}",
            observation
                .water_observation
                .as_deref()
                .unwrap_or("not_observed")
        ));
    }
    if observation.weather_observation.as_deref() != Some("observed_cpu_projection") {
        return Err(format!(
            "projection météo invalide : {}",
            observation
                .weather_observation
                .as_deref()
                .unwrap_or("not_observed")
        ));
    }
    let expected_weather = selected.weather_state.map(weather_state_label_contract);
    if observation.weather_target_state.as_deref() != expected_weather {
        return Err(format!(
            "cible météo runtime invalide : {} (attendu {})",
            observation
                .weather_target_state
                .as_deref()
                .unwrap_or("not_observed"),
            expected_weather.unwrap_or("not_applicable")
        ));
    }
    Ok(())
}

fn validate_visual_valley_scenery(
    observation: &StudioSceneRuntimeObservation,
    quality_lod_range: (f32, f32),
) -> Result<(), String> {
    let Some(scenery) = observation.scenery_observation.as_ref() else {
        return Err("projection décor CC0 not_observed".to_owned());
    };
    let selected_counts_match = scenery.selected_by_kind.requested_instances()
        == Some(scenery.requested_instances)
        && scenery.selected_by_kind.replacement_instances() == Some(scenery.replacement_instances)
        && scenery.selected_by_kind.detail_instances() == Some(scenery.detail_instances)
        && scenery.selected_by_kind.hidden_procedural_entities()
            == Some(scenery.hidden_procedural_entities);
    if scenery.status != "activated"
        || scenery.failed_models != 0
        || scenery.unique_models == 0
        || scenery.loaded_models != scenery.unique_models
        || scenery.requested_instances == 0
        || scenery.replacement_instances == 0
        || !selected_counts_match
        || scenery.tracked_files == 0
        || scenery.tracked_bytes == 0
        || !scenery.replacement_lod_start_m.is_finite()
        || !scenery.replacement_lod_end_m.is_finite()
        || !scenery.detail_cull_distance_m.is_finite()
        || scenery.replacement_lod_start_m <= 0.0
        || scenery.replacement_lod_end_m < scenery.replacement_lod_start_m
        || scenery.detail_cull_distance_m <= 0.0
    {
        return Err(format!(
            "projection décor CC0 invalide : statut={}, modèles={}/{}, échecs={}, instances demandées/remplacements/détails/cachées={}/{}/{}/{}, sélection={:?}, manifeste={} fichiers/{} octets",
            scenery.status,
            scenery.loaded_models,
            scenery.unique_models,
            scenery.failed_models,
            scenery.requested_instances,
            scenery.replacement_instances,
            scenery.detail_instances,
            scenery.hidden_procedural_entities,
            scenery.selected_by_kind,
            scenery.tracked_files,
            scenery.tracked_bytes
        ));
    }
    let (quality_lod_start_m, quality_lod_end_m) = quality_lod_range;
    if scenery.replacement_lod_start_m.to_bits() != quality_lod_start_m.to_bits()
        || scenery.replacement_lod_end_m.to_bits() != quality_lod_end_m.to_bits()
    {
        return Err(format!(
            "plage LOD du décor CC0 {}-{} m divergente du preset qualité {}-{} m",
            scenery.replacement_lod_start_m,
            scenery.replacement_lod_end_m,
            quality_lod_start_m,
            quality_lod_end_m
        ));
    }
    Ok(())
}

fn validate_gate0_quality_contract(
    observation: &StudioSceneRuntimeObservation,
) -> Result<(), String> {
    if observation.quality_current_tier.is_some()
        || observation.quality_applied_tier.is_some()
        || observation.quality_replacement_lod_start_m.is_some()
        || observation.quality_replacement_lod_end_m.is_some()
    {
        return Err(
            "la preuve Gate 0 doit conserver les tiers qualité à not_applicable".to_owned(),
        );
    }
    Ok(())
}

#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
fn is_validated_gate0_runtime(telemetry: &StudioSceneTelemetry) -> bool {
    telemetry.parity_status == StudioSceneParityStatus::SourceManifestMatched
        && telemetry.selected_manifest.scene == StudioScene::Gate0
        && telemetry
            .runtime_manifest
            .as_ref()
            .is_some_and(|manifest| manifest.scene == StudioScene::Gate0)
}

const fn weather_state_label_contract(state: WeatherState) -> &'static str {
    match state {
        WeatherState::Clear => "clear",
        WeatherState::Rain => "rain",
    }
}

#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
fn visual_valley_scenery_manifest_hash() -> Result<String, String> {
    let manifest: serde_json::Value = serde_json::from_str(VISUAL_VALLEY_SCENERY_MANIFEST_JSON)
        .map_err(|error| format!("invalid embedded Visual Valley scenery manifest: {error}"))?;
    canonical_json_sha256(&manifest)
        .map(|hash| hash.to_string())
        .map_err(|error| format!("cannot hash Visual Valley scenery manifest: {error}"))
}

#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
#[derive(Clone, Debug)]
enum LoadedStudioScene {
    Gate0(SceneProofDocument),
    VisualValley(Box<LoadedVisualValley>),
}

#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
#[derive(Clone, Debug)]
struct LoadedVisualValley {
    terrain: TerrainDocument,
    vegetation: VegetationDocument,
    environment: EnvironmentProfileDocument,
    player_controller: PlayerControllerProfileDocument,
    water: WaterDocument,
    weather: WeatherProfileDocument,
    quality: VisualValleyQualityProfileDocument,
}

#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
impl LoadedStudioScene {
    fn load(scene: StudioScene) -> Result<Self, String> {
        match scene {
            StudioScene::Gate0 => embedded_gate0_proof_document()
                .map(Self::Gate0)
                .map_err(|error| format!("invalid embedded Gate 0 scene: {error}")),
            StudioScene::VisualValley => {
                let terrain = embedded_visual_valley_terrain_document()
                    .map_err(|error| format!("invalid embedded Visual Valley terrain: {error}"))?;
                let vegetation = embedded_visual_valley_vegetation_document().map_err(|error| {
                    format!("invalid embedded Visual Valley vegetation: {error}")
                })?;
                let environment =
                    embedded_visual_valley_environment_profile().map_err(|error| {
                        format!("invalid embedded Visual Valley environment: {error}")
                    })?;
                let player_controller = embedded_visual_valley_player_controller_profile()
                    .map_err(|error| {
                        format!("invalid embedded Visual Valley player controller: {error}")
                    })?;
                let water = embedded_visual_valley_water_document()
                    .map_err(|error| format!("invalid embedded Visual Valley water: {error}"))?;
                let weather = embedded_visual_valley_weather_profile()
                    .map_err(|error| format!("invalid embedded Visual Valley weather: {error}"))?;
                let quality = VisualValleyQualityProfileDocument::from_json(include_str!(
                    "../../../examples/visual_valley/quality-profile.v1.json"
                ))
                .map_err(|error| {
                    format!("invalid embedded Visual Valley quality profile: {error}")
                })?;

                TerrainGenerationPlan::from_document(&terrain)
                    .map_err(|error| format!("unsupported Visual Valley terrain: {error}"))?;
                VegetationGenerationPlan::from_documents(&terrain, &vegetation)
                    .map_err(|error| format!("unsupported Visual Valley vegetation: {error}"))?;
                environment
                    .validate_identity(terrain.project_id, terrain.area_id, terrain.revision)
                    .map_err(|error| format!("incompatible Visual Valley environment: {error}"))?;
                water
                    .validate_identity(terrain.project_id, terrain.area_id, terrain.revision)
                    .map_err(|error| format!("incompatible Visual Valley water: {error}"))?;
                weather
                    .validate_identity(terrain.project_id, terrain.area_id, terrain.revision)
                    .map_err(|error| format!("incompatible Visual Valley weather: {error}"))?;
                quality
                    .validate_identity(terrain.project_id, terrain.area_id, terrain.revision)
                    .map_err(|error| {
                        format!("incompatible Visual Valley quality profile: {error}")
                    })?;

                Ok(Self::VisualValley(Box::new(LoadedVisualValley {
                    terrain,
                    vegetation,
                    environment,
                    player_controller,
                    water,
                    weather,
                    quality,
                })))
            }
        }
    }

    #[cfg_attr(
        all(target_arch = "wasm32", feature = "web"),
        allow(
            dead_code,
            reason = "native contract tests use the default-quality wrapper"
        )
    )]
    fn manifest(
        &self,
        selected_weather_state: Option<WeatherState>,
    ) -> Result<StudioSceneManifest, String> {
        self.manifest_with_quality(selected_weather_state, StudioQualitySelection::default())
    }

    #[allow(
        clippy::too_many_lines,
        reason = "one manifest projection preserves correlated canonical source hashes"
    )]
    fn manifest_with_quality(
        &self,
        selected_weather_state: Option<WeatherState>,
        selected_quality: StudioQualitySelection,
    ) -> Result<StudioSceneManifest, String> {
        match self {
            Self::Gate0(document) => Ok(StudioSceneManifest {
                mode: StudioViewportMode::Gate0SceneProof,
                scene: StudioScene::Gate0,
                display_name: document.name.clone(),
                source_hashes: StudioSceneSourceHashes {
                    scene_proof: Some(
                        document
                            .content_hash()
                            .map_err(|error| format!("cannot hash Gate 0 scene: {error}"))?
                            .to_string(),
                    ),
                    ..StudioSceneSourceHashes::default()
                },
                camera_owner: StudioCameraOwner::Gate0ProofDocument,
                expected_active_camera_count: 1,
                water_adapter: StudioWaterAdapterStatus::NotApplicable,
                weather_state: None,
                quality_selection: None,
                quality_tier: None,
            }),
            Self::VisualValley(loaded) => {
                let LoadedVisualValley {
                    terrain,
                    vegetation,
                    environment,
                    player_controller,
                    water,
                    weather,
                    quality,
                } = loaded.as_ref();
                Ok(StudioSceneManifest {
                    mode: StudioViewportMode::Gate1VisualValley,
                    scene: StudioScene::VisualValley,
                    display_name: "Visual Valley".to_owned(),
                    source_hashes: StudioSceneSourceHashes {
                        scene_proof: None,
                        terrain: Some(
                            terrain
                                .content_hash()
                                .map_err(|error| {
                                    format!("cannot hash Visual Valley terrain: {error}")
                                })?
                                .to_string(),
                        ),
                        vegetation: Some(
                            vegetation
                                .content_hash()
                                .map_err(|error| {
                                    format!("cannot hash Visual Valley vegetation: {error}")
                                })?
                                .to_string(),
                        ),
                        environment: Some(
                            environment
                                .content_hash()
                                .map_err(|error| {
                                    format!("cannot hash Visual Valley environment: {error}")
                                })?
                                .to_string(),
                        ),
                        player_controller: Some(
                            player_controller
                                .content_hash()
                                .map_err(|error| {
                                    format!("cannot hash Visual Valley player controller: {error}")
                                })?
                                .to_string(),
                        ),
                        water: Some(
                            water
                                .content_hash()
                                .map_err(|error| {
                                    format!("cannot hash Visual Valley water: {error}")
                                })?
                                .to_string(),
                        ),
                        weather: Some(
                            weather
                                .content_hash()
                                .map_err(|error| {
                                    format!("cannot hash Visual Valley weather: {error}")
                                })?
                                .to_string(),
                        ),
                        scenery: Some(visual_valley_scenery_manifest_hash()?),
                        quality: Some(
                            quality
                                .content_hash()
                                .map_err(|error| {
                                    format!("cannot hash Visual Valley quality profile: {error}")
                                })?
                                .to_string(),
                        ),
                    },
                    camera_owner: StudioCameraOwner::VisualValleyPlayerController,
                    expected_active_camera_count: 1,
                    water_adapter: StudioWaterAdapterStatus::InstalledEngineAdapter,
                    weather_state: selected_weather_state,
                    quality_selection: Some(selected_quality),
                    quality_tier: Some(selected_quality.initial_tier()),
                })
            }
        }
    }

    fn hierarchy(&self) -> Vec<StudioHierarchyEntry> {
        match self {
            Self::Gate0(document) => document
                .entities
                .iter()
                .map(|entity| StudioHierarchyEntry {
                    id: entity.id,
                    name: entity.name.clone(),
                    glyph: scene_proof_kind_glyph(&entity.kind),
                })
                .collect(),
            Self::VisualValley(_) => [
                ("terrain", "Terrain document", "▱"),
                ("vegetation", "Vegetation document", "♧"),
                ("environment", "Environment profile", "☀"),
                ("player-controller", "Player controller", "◉"),
                ("water", "Water document", "≈"),
                ("weather", "Weather profile", "☂"),
                ("scenery", "Governed CC0 scenery", "♣"),
                ("quality", "Quality profile", "◇"),
            ]
            .into_iter()
            .map(|(key, name, glyph)| StudioHierarchyEntry {
                id: EntityId::deterministic(format!("studio/visual-valley/{key}")),
                name: name.to_owned(),
                glyph,
            })
            .collect(),
        }
    }
}

#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
#[derive(Clone, Debug)]
struct StudioHierarchyEntry {
    id: EntityId,
    name: String,
    glyph: &'static str,
}

#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
const fn scene_proof_kind_glyph(kind: &SceneProofKind) -> &'static str {
    match kind {
        SceneProofKind::Ground { .. } => "▱",
        SceneProofKind::Cube { .. } => "◈",
        SceneProofKind::DirectionalLight { .. } => "☀",
        SceneProofKind::Camera { .. } => "◉",
    }
}

#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
mod telemetry;

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "command", rename_all = "snake_case", deny_unknown_fields)]
pub enum StudioCommand {
    AreaPreview {
        request: area_preview::AreaPreviewRequest,
    },
    SelectEntity {
        entity_id: EntityId,
    },
    SceneSummary,
    Workspace {
        request: workspace_api::WorkspaceCommand,
    },
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(tag = "response", rename_all = "snake_case", deny_unknown_fields)]
pub enum StudioResponse {
    AreaPreview {
        preview_json: String,
    },
    Workspace {
        state: workspace_api::WorkspaceState,
    },
    SelectionAccepted {
        entity_id: EntityId,
    },
    SceneSummary {
        source_hash: String,
        entity_count: usize,
    },
    Rejected {
        reason: String,
    },
}

#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
fn validate_selection_response(
    expected_entity_id: EntityId,
    response: StudioResponse,
) -> Result<String, String> {
    match response {
        StudioResponse::SelectionAccepted { entity_id } if entity_id == expected_entity_id => {
            Ok(format!("Sélection confirmée par Tauri : {entity_id}"))
        }
        StudioResponse::SelectionAccepted { entity_id } => Err(format!(
            "Réponse Tauri incohérente : sélection {entity_id}, attendu {expected_entity_id}"
        )),
        StudioResponse::Rejected { reason } => {
            Err(format!("Commande rejetée par Tauri : {reason}"))
        }
        StudioResponse::SceneSummary { .. } => {
            Err("Réponse Tauri inattendue : résumé reçu pour une sélection".to_owned())
        }
        StudioResponse::Workspace { .. } | StudioResponse::AreaPreview { .. } => {
            Err("Réponse Tauri inattendue : projet reçu pour une sélection".to_owned())
        }
    }
}

#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod web {
    use super::telemetry::{
        AllowedCommandProbeStatus, BrowserMemoryObservation, DeniedCommandProbeStatus,
        FRAME_MEASURED_SAMPLES, FrameDistribution, Gate0ProbeMode, Gate0StudioLedger,
        RuntimeTelemetryEvent, StudioTelemetryTarget, TAURI_DENIED_COMMAND, ViewportFailureEvent,
        ViewportObservation, ViewportReadyEvent, WebGpuProbeStatus, is_explicit_acl_denial,
        parse_gate0_probe,
    };
    use super::{
        BrowserWeatherProofLedger, EntityId, FrameQualityFingerprint, FrameSamplingProgress,
        LoadedStudioScene, StableQualityFrameWindow, StudioCommand,
        StudioGate1WeatherProofSelection, StudioGpuPresentationStatus, StudioQualitySelection,
        StudioResponse, StudioScene, StudioSceneManifest, StudioSceneParityStatus,
        StudioSceneRuntimeObservation, StudioSceneSourceHashes, StudioSceneTelemetry,
        StudioSceneryInstanceCounts, StudioSceneryRuntimeObservation, StudioViewportMode,
        StudioWaterAdapterStatus, VisualQualityTier, browser_weather_proof_camera_pose,
        is_validated_gate0_runtime, parse_studio_gate1_weather_proof_selection,
        parse_studio_quality_selection, parse_studio_scene_selection,
        parse_studio_weather_selection, validate_selection_response,
    };
    use bevy::{
        asset::{AssetMetaCheck, AssetPlugin},
        prelude::*,
        render::{
            RenderPlugin,
            settings::{RenderCreation, WgpuSettings},
        },
        window::{PresentMode, WindowResolution},
    };
    use forge_engine::{
        EnvironmentProjectionStats, EnvironmentProjectionStatus, PlayerRuntimeSnapshot, ProofScene,
        ProofScenePlugin, ProofSelection, SceneryProjectionStats, SceneryProjectionStatus,
        VegetationProjectionStats, VisualValleyEnvironmentPlugin, VisualValleyPlugin,
        VisualValleyQualityPlugin, VisualValleyQualitySelection, VisualValleyQualityStats,
        VisualValleySceneryPlugin, VisualValleyStats, VisualValleyVegetationPlugin,
        VisualValleyWaterPlugin, VisualValleyWeatherPlugin, WaterProjectionStats,
        WaterRuntimeObservationStatus, WeatherProjectionStats, WeatherProjectionStatus,
    };
    use leptos::prelude::Update as LeptosUpdate;
    use leptos::prelude::*;
    use std::{
        cell::RefCell,
        sync::atomic::{AtomicBool, AtomicUsize, Ordering},
    };
    use wasm_bindgen::{JsCast, prelude::*};

    static VIEWPORT_LAUNCH_REQUESTED: AtomicBool = AtomicBool::new(false);
    static VIEWPORT_STARTS: AtomicUsize = AtomicUsize::new(0);
    const VIEWPORT_SELECTION_EVENT: &str = "forge-proof-selection";
    const VIEWPORT_READY_EVENT: &str = "forge-gate0-viewport-ready";
    const VIEWPORT_FAILURE_EVENT: &str = "forge-studio-runtime-failure";
    const RUNTIME_TELEMETRY_EVENT: &str = "forge-gate0-runtime-telemetry";
    const VISUAL_VALLEY_PROJECTION_TIMEOUT_MS: f64 = 10_000.0;

    thread_local! {
        static INITIAL_CANVAS: RefCell<Option<web_sys::Element>> = const { RefCell::new(None) };
    }

    pub(super) fn viewport_starts() -> usize {
        VIEWPORT_STARTS.load(Ordering::SeqCst)
    }

    #[derive(Clone, Debug, Eq, PartialEq)]
    enum WebGpuStatus {
        Checking,
        Ready,
        RuntimeObservedGpuPresentationNotObserved,
        Unavailable(String),
        ConfigurationError(String),
        RuntimeFailure(String),
    }

    #[wasm_bindgen(start)]
    pub fn start() {
        console_error_panic_hook::set_once();
        let query = web_sys::window()
            .and_then(|window| window.location().search().ok())
            .unwrap_or_default();
        if query
            .trim_start_matches('?')
            .split('&')
            .any(|entry| entry == "forge_editor=project")
        {
            super::workspace_ui::mount_project_workspace();
            return;
        }
        // An explicit proof query keeps the historical scene route and ledgers intact.
        if query.is_empty()
            || query
                .trim_start_matches('?')
                .split('&')
                .any(|entry| entry == "forge_editor=desktop")
        {
            super::desktop_ui::mount_desktop();
        } else {
            mount_to_body(|| view! { <Studio/> });
        }
    }

    #[component]
    #[allow(
        clippy::too_many_lines,
        reason = "Existing proof view and one-time renderer setup stay together; desktop embedding does not refactor the qualified scene"
    )]
    pub(crate) fn Studio(#[prop(default = false)] embedded: bool) -> impl IntoView {
        let probe_mode = current_probe_mode();
        let scene_selection = if embedded {
            super::parse_studio_scene_selection("")
        } else {
            current_scene_selection()
        };
        let selected_scene = scene_selection.scene;
        let weather_selection = current_weather_selection(selected_scene);
        let weather_configuration_error = weather_selection.as_ref().err().map(ToString::to_string);
        let quality_selection = current_quality_selection(selected_scene);
        let quality_configuration_error = quality_selection.as_ref().err().map(ToString::to_string);
        let selected_weather_state = weather_selection
            .as_ref()
            .ok()
            .filter(|_| selected_scene == StudioScene::VisualValley)
            .map(|selection| selection.state);
        let selected_quality = quality_selection.as_ref().copied().unwrap_or_default();
        let weather_proof_selection = match (&weather_selection, &quality_selection) {
            (Ok(weather), Ok(quality)) => {
                current_gate1_weather_proof_selection(selected_scene, weather.state, *quality)
            }
            _ => Ok(StudioGate1WeatherProofSelection::Disabled),
        };
        let weather_proof_configuration_error = weather_proof_selection
            .as_ref()
            .err()
            .map(ToString::to_string);
        let weather_proof_enabled = weather_proof_selection
            .as_ref()
            .is_ok_and(|selection| selection.is_enabled());
        let loaded_scene = LoadedStudioScene::load(selected_scene)
            .expect("embedded Studio scene documents must remain valid and compatible");
        let scene_manifest = loaded_scene
            .manifest_with_quality(selected_weather_state, selected_quality)
            .expect("validated embedded Studio scene documents must remain hashable");
        let scene_name = scene_manifest.display_name.clone();
        let scene_mode_label = studio_mode_label(scene_manifest.mode);
        let scene_source_label = format!(
            "{} source hash(es)",
            scene_manifest.source_hashes.populated_count()
        );
        let water_status_label = water_adapter_label(scene_manifest.water_adapter);
        let weather_status_label = match (selected_scene, selected_weather_state) {
            (_, None) => weather_configuration_error.as_ref().map_or_else(
                || "hors scène Gate 0".to_owned(),
                |reason| format!("ERREUR · {reason}"),
            ),
            (StudioScene::Gate0, Some(_)) => "hors scène Gate 0".to_owned(),
            (StudioScene::VisualValley, Some(super::WeatherState::Clear)) => {
                "clair · adapter installé".to_owned()
            }
            (StudioScene::VisualValley, Some(super::WeatherState::Rain)) => {
                "pluie · adapter installé".to_owned()
            }
        };
        let quality_status_label = match selected_scene {
            StudioScene::Gate0 => "hors scène Gate 0".to_owned(),
            StudioScene::VisualValley => match selected_quality {
                StudioQualitySelection::Fixed(tier) => {
                    format!("{} · fixe", quality_tier_label(tier))
                }
                StudioQualitySelection::Adaptive => "high · adaptatif borné".to_owned(),
                StudioQualitySelection::Benchmark => {
                    "high · benchmark fixe · adaptation désactivée".to_owned()
                }
            },
        };
        let hierarchy = loaded_scene.hierarchy();
        let initial_selection = hierarchy
            .first()
            .expect("every embedded Studio scene must expose at least one hierarchy node")
            .id;

        let (selected, set_selected) = signal(initial_selection);
        let (rerenders, set_rerenders) = signal(0_u32);
        let (ipc_status, set_ipc_status) = signal("Aucune commande".to_owned());
        let (webgpu_status, set_webgpu_status) = signal(WebGpuStatus::Checking);
        let (telemetry, set_telemetry) = signal(Gate0StudioLedger::new(
            studio_telemetry_target(),
            performance_now(),
        ));
        let mut initial_scene_telemetry =
            StudioSceneTelemetry::new(scene_selection, scene_manifest.clone());
        if let Some(reason) = weather_configuration_error.as_ref() {
            initial_scene_telemetry.fail_configuration(reason.clone());
        }
        if let Some(reason) = quality_configuration_error.as_ref() {
            initial_scene_telemetry.fail_configuration(reason.clone());
        }
        if let Some(reason) = weather_proof_configuration_error.as_ref() {
            initial_scene_telemetry.fail_configuration(reason.clone());
        }
        let (scene_telemetry, set_scene_telemetry) = signal(initial_scene_telemetry);
        let (weather_proof_ledger, set_weather_proof_ledger) =
            signal(None::<BrowserWeatherProofLedger>);

        install_viewport_selection_listener(
            set_selected,
            set_rerenders,
            set_ipc_status,
            set_telemetry,
        );
        install_runtime_telemetry_listener(set_telemetry, set_scene_telemetry, set_webgpu_status);
        if weather_proof_enabled {
            super::gate1_browser_weather_matrix_web::install_listeners(set_weather_proof_ledger);
        }

        if probe_mode == Gate0ProbeMode::TauriPermission {
            wasm_bindgen_futures::spawn_local(run_tauri_permission_probe(set_telemetry));
        }

        let launch_configuration_error = weather_configuration_error
            .clone()
            .or_else(|| quality_configuration_error.clone())
            .or_else(|| weather_proof_configuration_error.clone());
        wasm_bindgen_futures::spawn_local(async move {
            if let Some(reason) = launch_configuration_error {
                set_webgpu_status.set(WebGpuStatus::ConfigurationError(reason));
                return;
            }
            match preflight_webgpu(probe_mode).await {
                Ok(()) => {
                    start_viewport_once(
                        selected_scene,
                        selected_weather_state.unwrap_or(super::WeatherState::Clear),
                        selected_quality,
                        weather_proof_enabled,
                        embedded,
                    );
                }
                Err(reason) => {
                    set_webgpu_status.set(WebGpuStatus::Unavailable(reason.clone()));
                    LeptosUpdate::update(&set_telemetry, |ledger| {
                        ledger.webgpu.status = WebGpuProbeStatus::Unavailable;
                        ledger.webgpu.diagnostic_injection =
                            probe_mode == Gate0ProbeMode::WebGpuUnavailable;
                        ledger.webgpu.reason = Some(reason);
                        ledger.recorded_at_utc = Some(recorded_at_utc());
                    });
                }
            }
        });

        view! {
            <main
                class="studio-shell"
                data-forge-mode=scene_mode_label
                data-forge-scene=selected_scene.query_value()
            >
                <header class="topbar">
                    <div>
                        <span class="eyebrow">"OPENRPG FORGE · RUST"</span>
                        <h1>"Visual Workspace"</h1>
                    </div>
                    <a href="?forge_editor=project" hidden=embedded>"Projets · Gate 2"</a>
                    <div class="status-stack">
                        <span
                            class:status-ok=move || matches!(webgpu_status.get(), WebGpuStatus::Ready)
                            class:status-error=move || matches!(
                                webgpu_status.get(),
                                WebGpuStatus::Unavailable(_)
                                    | WebGpuStatus::ConfigurationError(_)
                                    | WebGpuStatus::RuntimeFailure(_)
                            )
                        >
                            {move || match webgpu_status.get() {
                                WebGpuStatus::Checking => "Vérification WebGPU…",
                                WebGpuStatus::Ready => "WebGPU prêt",
                                WebGpuStatus::RuntimeObservedGpuPresentationNotObserved => {
                                    "Runtime ECS observé · présentation GPU non observée"
                                }
                                WebGpuStatus::Unavailable(_) => "WebGPU indisponible",
                                WebGpuStatus::ConfigurationError(_) => "Configuration invalide",
                                WebGpuStatus::RuntimeFailure(_) => "Contrat runtime en échec",
                            }}
                        </span>
                        <span>"Monde initialisé " {move || {
                            let _ = webgpu_status.get();
                            VIEWPORT_STARTS.load(Ordering::SeqCst)
                        }} " fois"</span>
                    </div>
                </header>
                <section class="workspace">
                    <aside class="panel hierarchy" aria-label="Hiérarchie de scène">
                        <h2>"Hiérarchie"</h2>
                        {hierarchy.into_iter().map(|entity| {
                            let entity_id = entity.id;
                            let entity_name = entity.name;
                            let glyph = entity.glyph;
                            view! {
                                <button
                                    class:selected=move || selected.get() == entity_id
                                    on:click=move |_| {
                                        let interaction_started_ms = performance_now();
                                        set_selected.set(entity_id);
                                        LeptosUpdate::update(&set_rerenders, |value| *value += 1);
                                        if selected_scene == StudioScene::Gate0 {
                                            dispatch_selection(entity_id, set_ipc_status);
                                        } else {
                                            set_ipc_status.set(
                                                "Sélection locale d’un document Visual Valley"
                                                    .to_owned(),
                                            );
                                        }
                                        schedule_interaction_observation(
                                            interaction_started_ms,
                                            set_telemetry,
                                        );
                                    }
                                >{glyph} " " {entity_name}</button>
                            }
                        }).collect_view()}
                    </aside>
                    <section class="viewport-panel" aria-label="Viewport 3D">
                        <div class="viewport-toolbar">
                            <span>"Perspective"</span><span>{if embedded { "Dimensions adaptatives" } else { "1920 × 1080" }}</span>
                        </div>
                        <canvas id="forge-canvas" data-testid="forge-webgpu-canvas"></canvas>
                        {move || match webgpu_status.get() {
                            WebGpuStatus::Unavailable(reason) => Some(view! {
                                <div class="webgpu-error" role="alert" data-testid="webgpu-unavailable">
                                    <strong>"WebGPU n’a pas pu initialiser un périphérique."</strong>
                                    <span>{reason}</span>
                                    <span>"Vérifiez l’accélération graphique, le pilote et la version de Chrome, Edge ou WebView2, puis relancez le Studio."</span>
                                </div>
                            }.into_any()),
                            WebGpuStatus::ConfigurationError(reason) => Some(view! {
                                <div class="webgpu-error" role="alert" data-testid="forge-weather-query-error">
                                    <strong>"Paramètre météo invalide."</strong>
                                    <span>{reason}</span>
                                    <span>"Utilisez forge_weather=clear|rain et forge_quality=low|medium|high|ultra|adaptive|benchmark avec forge_scene=visual-valley."</span>
                                </div>
                            }.into_any()),
                            WebGpuStatus::RuntimeFailure(reason) => Some(view! {
                                <div class="webgpu-error" role="alert" data-testid="forge-runtime-contract-error">
                                    <strong>"Le runtime n’a pas satisfait le contrat de scène."</strong>
                                    <span>{reason}</span>
                                </div>
                            }.into_any()),
                            WebGpuStatus::Checking
                            | WebGpuStatus::Ready
                            | WebGpuStatus::RuntimeObservedGpuPresentationNotObserved => None,
                        }}
                    </section>
                    <aside class="panel inspector" aria-label="Inspecteur">
                        <h2>"Inspecteur"</h2>
                        <label>"NodeId"<output>{move || selected.get().to_string()}</output></label>
                        <label>"Mode"<output>{scene_mode_label}</output></label>
                        <label>"Scène"<output>{scene_name}</output></label>
                        <label>"Sources"<output>{scene_source_label}</output></label>
                        <label>"Eau"<output>{water_status_label}</output></label>
                        <label>"Météo"<output>{weather_status_label}</output></label>
                        <label>"Qualité"<output>{quality_status_label}</output></label>
                        <label>"Parité scène"<output id="studio-scene-parity-summary">{move || scene_parity_summary(&scene_telemetry.get())}</output></label>
                        <label>"Rerenders UI"<output>{move || rerenders.get()}</output></label>
                        <label>"IPC Rust"<output>{move || ipc_status.get()}</output></label>
                        <label>"Frames Web"<output id="gate0-frame-summary">{move || frame_summary(&telemetry.get())}</output></label>
                        <label>"Latence UI"<output id="gate0-interaction-summary">{move || interaction_summary(&telemetry.get())}</output></label>
                        <label>"Isolation Tauri"<output id="gate0-permission-summary">{move || permission_summary(&telemetry.get())}</output></label>
                        <p class="hint">"La sélection change l’UI sans reconstruire le monde Bevy."</p>
                        <pre id="gate0-telemetry-ledger" hidden>{move || {
                            serde_json::to_string(&telemetry.get()).unwrap_or_else(|error| {
                                format!("{{\"serialization_error\":\"{error}\"}}")
                            })
                        }}</pre>
                        <pre id="forge-scene-telemetry-ledger" hidden>{move || {
                            serde_json::to_string(&scene_telemetry.get()).unwrap_or_else(|error| {
                                format!("{{\"serialization_error\":\"{error}\"}}")
                            })
                        }}</pre>
                        <pre id="forge-gate1-browser-weather-proof-ledger" hidden>{move || {
                            weather_proof_ledger.get().map_or_else(String::new, |ledger| {
                                serde_json::to_string(&ledger).unwrap_or_else(|error| {
                                    format!("{{\"serialization_error\":\"{error}\"}}")
                                })
                            })
                        }}</pre>
                    </aside>
                </section>
            </main>
        }
    }

    fn dispatch_selection(entity_id: EntityId, status: WriteSignal<String>) {
        let command = StudioCommand::SelectEntity { entity_id };
        let started_at_ms = js_sys::Date::now();
        status.set("Commande envoyée à Tauri…".to_owned());
        wasm_bindgen_futures::spawn_local(async move {
            let message = match invoke_tauri(&command).await {
                Ok(response) => validate_selection_response(entity_id, response),
                Err(reason) => Err(reason),
            };
            let elapsed_ms = js_sys::Date::now() - started_at_ms;
            status.set(format!(
                "{} · {elapsed_ms:.1} ms",
                message.unwrap_or_else(|reason| reason)
            ));
        });
    }

    fn install_viewport_selection_listener(
        selected: WriteSignal<EntityId>,
        rerenders: WriteSignal<u32>,
        ipc_status: WriteSignal<String>,
        telemetry: WriteSignal<Gate0StudioLedger>,
    ) {
        let listener =
            Closure::<dyn FnMut(web_sys::CustomEvent)>::new(move |event: web_sys::CustomEvent| {
                let Some(value) = event.detail().as_string() else {
                    ipc_status.set("Sélection 3D invalide : EntityId absent".to_owned());
                    return;
                };
                match value.parse::<EntityId>() {
                    Ok(entity_id) => {
                        let interaction_started_ms = performance_now();
                        selected.set(entity_id);
                        LeptosUpdate::update(&rerenders, |count| *count += 1);
                        dispatch_selection(entity_id, ipc_status);
                        schedule_interaction_observation(interaction_started_ms, telemetry);
                    }
                    Err(error) => ipc_status.set(format!("Sélection 3D invalide : {error}")),
                }
            });
        if let Some(window) = web_sys::window()
            && window
                .add_event_listener_with_callback(
                    VIEWPORT_SELECTION_EVENT,
                    listener.as_ref().unchecked_ref(),
                )
                .is_ok()
        {
            // The Studio component owns the page for the full application lifetime.
            listener.forget();
        }
    }

    fn install_runtime_telemetry_listener(
        telemetry: WriteSignal<Gate0StudioLedger>,
        scene_telemetry: WriteSignal<StudioSceneTelemetry>,
        webgpu_status: WriteSignal<WebGpuStatus>,
    ) {
        let ready_listener =
            Closure::<dyn FnMut(web_sys::CustomEvent)>::new(move |event: web_sys::CustomEvent| {
                handle_viewport_ready_event(&event, telemetry, scene_telemetry, webgpu_status);
            });
        let failure_listener =
            Closure::<dyn FnMut(web_sys::CustomEvent)>::new(move |event: web_sys::CustomEvent| {
                handle_viewport_failure_event(&event, telemetry, scene_telemetry, webgpu_status);
            });
        let frame_listener =
            Closure::<dyn FnMut(web_sys::CustomEvent)>::new(move |event: web_sys::CustomEvent| {
                handle_runtime_telemetry_event(&event, telemetry);
            });
        if let Some(window) = web_sys::window() {
            if window
                .add_event_listener_with_callback(
                    VIEWPORT_READY_EVENT,
                    ready_listener.as_ref().unchecked_ref(),
                )
                .is_ok()
            {
                ready_listener.forget();
            }
            if window
                .add_event_listener_with_callback(
                    VIEWPORT_FAILURE_EVENT,
                    failure_listener.as_ref().unchecked_ref(),
                )
                .is_ok()
            {
                failure_listener.forget();
            }
            if window
                .add_event_listener_with_callback(
                    RUNTIME_TELEMETRY_EVENT,
                    frame_listener.as_ref().unchecked_ref(),
                )
                .is_ok()
            {
                frame_listener.forget();
            }
        }
    }

    fn event_json(event: &web_sys::CustomEvent, missing_message: &str) -> Option<String> {
        let encoded = event.detail().as_string();
        if encoded.is_none() {
            web_sys::console::error_1(&JsValue::from_str(missing_message));
        }
        encoded
    }

    fn handle_viewport_ready_event(
        event: &web_sys::CustomEvent,
        telemetry: WriteSignal<Gate0StudioLedger>,
        scene_telemetry: WriteSignal<StudioSceneTelemetry>,
        webgpu_status: WriteSignal<WebGpuStatus>,
    ) {
        let Some(encoded) = event_json(
            event,
            "Studio viewport-ready event did not contain JSON text",
        ) else {
            return;
        };
        let ready = match serde_json::from_str::<ViewportReadyEvent>(&encoded) {
            Ok(ready) => ready,
            Err(error) => {
                web_sys::console::error_1(&JsValue::from_str(&format!(
                    "Studio viewport-ready event was invalid: {error}"
                )));
                return;
            }
        };
        let runtime_observation = StudioSceneRuntimeObservation {
            first_real_frame_ms: ready.first_real_frame_ms,
            runtime_ready_ms: ready.runtime_ready_ms,
            camera_3d_entities: ready.camera_3d_entities,
            active_camera_3d_entities: ready.active_camera_3d_entities,
            water_observation: ready.water_observation.clone(),
            weather_observation: ready.weather_observation.clone(),
            weather_target_state: ready.weather_target_state.clone(),
            quality_current_tier: ready.quality_current_tier,
            quality_applied_tier: ready.quality_applied_tier,
            quality_replacement_lod_start_m: ready.quality_replacement_lod_start_m,
            quality_replacement_lod_end_m: ready.quality_replacement_lod_end_m,
            scenery_observation: ready.scenery_observation.clone(),
            gpu_presentation: ready.gpu_presentation,
            evidence_source: "bevy_main_world_cpu_ecs_projection_observed_not_gpu_presentation"
                .to_owned(),
        };
        let mut runtime_failure = None;
        let mut validated_gate0 = false;
        LeptosUpdate::update(&scene_telemetry, |ledger| {
            ledger.observe_runtime(ready.scene_manifest.clone(), runtime_observation);
            if ledger.parity_status == StudioSceneParityStatus::SourceManifestMatched {
                validated_gate0 = is_validated_gate0_runtime(ledger);
            } else {
                runtime_failure.clone_from(&ledger.failure_reason);
            }
        });
        LeptosUpdate::update(&telemetry, |ledger| {
            ledger.recorded_at_utc = Some(ready.recorded_at_utc.clone());
            ledger.viewport.clone_from(&ready.viewport);
            if let Some(reason) = &runtime_failure {
                ledger.loading.scene_ready_ms = None;
                ledger.loading.webgpu_ready_ms = None;
                ledger.webgpu.status = WebGpuProbeStatus::Checking;
                ledger.webgpu.reason = Some(format!("contrat runtime invalide : {reason}"));
            } else {
                ledger.loading.scene_ready_ms = Some(ready.runtime_ready_ms);
                if validated_gate0 {
                    ledger.webgpu.status = WebGpuProbeStatus::Ready;
                    ledger.webgpu.reason = None;
                    ledger.loading.webgpu_ready_ms = Some(ready.runtime_ready_ms);
                } else {
                    ledger.webgpu.reason = Some(
                        "Gate 1 runtime CPU/ECS observed; GPU presentation remains not_observed"
                            .to_owned(),
                    );
                }
            }
        });
        webgpu_status.set(if let Some(reason) = runtime_failure {
            WebGpuStatus::RuntimeFailure(reason)
        } else if validated_gate0 {
            WebGpuStatus::Ready
        } else {
            WebGpuStatus::RuntimeObservedGpuPresentationNotObserved
        });
    }

    fn handle_viewport_failure_event(
        event: &web_sys::CustomEvent,
        telemetry: WriteSignal<Gate0StudioLedger>,
        scene_telemetry: WriteSignal<StudioSceneTelemetry>,
        webgpu_status: WriteSignal<WebGpuStatus>,
    ) {
        let Some(encoded) = event_json(
            event,
            "Studio runtime-failure event did not contain JSON text",
        ) else {
            return;
        };
        let failure = match serde_json::from_str::<ViewportFailureEvent>(&encoded) {
            Ok(failure) => failure,
            Err(error) => {
                web_sys::console::error_1(&JsValue::from_str(&format!(
                    "Studio runtime-failure event was invalid: {error}"
                )));
                return;
            }
        };
        webgpu_status.set(WebGpuStatus::RuntimeFailure(failure.reason.clone()));
        LeptosUpdate::update(&scene_telemetry, |ledger| {
            ledger.fail_runtime(failure.reason.clone());
        });
        LeptosUpdate::update(&telemetry, |ledger| {
            ledger.recorded_at_utc = Some(failure.recorded_at_utc.clone());
            ledger.loading.scene_ready_ms = None;
            ledger.loading.webgpu_ready_ms = None;
            ledger.webgpu.status = WebGpuProbeStatus::Checking;
            ledger.webgpu.reason = Some(format!("échec runtime : {}", failure.reason));
        });
    }

    fn handle_runtime_telemetry_event(
        event: &web_sys::CustomEvent,
        telemetry: WriteSignal<Gate0StudioLedger>,
    ) {
        let Some(encoded) = event_json(event, "Gate 0 telemetry event did not contain JSON text")
        else {
            return;
        };
        let runtime = match serde_json::from_str::<RuntimeTelemetryEvent>(&encoded) {
            Ok(runtime) => runtime,
            Err(error) => {
                web_sys::console::error_1(&JsValue::from_str(&format!(
                    "Gate 0 telemetry event was invalid: {error}"
                )));
                return;
            }
        };
        LeptosUpdate::update(&telemetry, |ledger| {
            ledger.recorded_at_utc = Some(runtime.recorded_at_utc);
            ledger.memory = runtime.memory;
            ledger.frame = Some(runtime.frame);
            ledger.viewport = runtime.viewport;
        });
    }

    fn schedule_interaction_observation(
        started_at_ms: f64,
        telemetry: WriteSignal<Gate0StudioLedger>,
    ) {
        let canvas_before = current_canvas();
        wasm_bindgen_futures::spawn_local(async move {
            if let Err(reason) = next_animation_frame().await {
                web_sys::console::error_1(&JsValue::from_str(&reason));
                return;
            }
            let post_paint_ms = match next_animation_frame().await {
                Ok(timestamp) => timestamp,
                Err(reason) => {
                    web_sys::console::error_1(&JsValue::from_str(&reason));
                    return;
                }
            };
            let canvas_after = current_canvas();
            let canvas_stable = match (canvas_before.as_ref(), canvas_after.as_ref()) {
                (Some(before), Some(after)) => before.is_same_node(Some(after)),
                _ => false,
            };
            let viewport_starts =
                u32::try_from(VIEWPORT_STARTS.load(Ordering::SeqCst)).unwrap_or(u32::MAX);
            LeptosUpdate::update(&telemetry, |ledger| {
                ledger
                    .interaction
                    .record(post_paint_ms - started_at_ms, canvas_stable);
                ledger.viewport.starts = viewport_starts;
                ledger.recorded_at_utc = Some(recorded_at_utc());
            });
        });
    }

    async fn next_animation_frame() -> Result<f64, String> {
        let promise = js_sys::Promise::new(&mut |resolve, reject| {
            let Some(window) = web_sys::window() else {
                let _ = reject.call1(
                    &JsValue::UNDEFINED,
                    &JsValue::from_str("Fenêtre navigateur absente pendant la mesure rAF"),
                );
                return;
            };
            let callback = Closure::once(move |timestamp_ms: f64| {
                let _ = resolve.call1(&JsValue::UNDEFINED, &JsValue::from_f64(timestamp_ms));
            });
            match window.request_animation_frame(callback.as_ref().unchecked_ref()) {
                Ok(_) => callback.forget(),
                Err(error) => {
                    let _ = reject.call1(&JsValue::UNDEFINED, &error);
                }
            }
        });
        let value = wasm_bindgen_futures::JsFuture::from(promise)
            .await
            .map_err(|error| format!("requestAnimationFrame échoué : {}", js_error(&error)))?;
        value
            .as_f64()
            .ok_or_else(|| "requestAnimationFrame n’a pas retourné de timestamp".to_owned())
    }

    fn current_canvas() -> Option<web_sys::Element> {
        web_sys::window()?
            .document()?
            .get_element_by_id("forge-canvas")
    }

    fn current_probe_mode() -> Gate0ProbeMode {
        let search = web_sys::window()
            .and_then(|window| window.location().search().ok())
            .unwrap_or_default();
        parse_gate0_probe(&search)
    }

    fn current_scene_selection() -> super::StudioSceneSelection {
        let search = web_sys::window()
            .and_then(|window| window.location().search().ok())
            .unwrap_or_default();
        parse_studio_scene_selection(&search)
    }

    fn current_weather_selection(
        scene: StudioScene,
    ) -> Result<super::StudioWeatherSelection, super::StudioWeatherQueryError> {
        let search = web_sys::window()
            .and_then(|window| window.location().search().ok())
            .unwrap_or_default();
        parse_studio_weather_selection(&search, scene)
    }

    fn current_quality_selection(
        scene: StudioScene,
    ) -> Result<super::StudioQualitySelection, super::StudioQualityQueryError> {
        let search = web_sys::window()
            .and_then(|window| window.location().search().ok())
            .unwrap_or_default();
        parse_studio_quality_selection(&search, scene)
    }

    fn current_gate1_weather_proof_selection(
        scene: StudioScene,
        weather: super::WeatherState,
        quality: StudioQualitySelection,
    ) -> Result<StudioGate1WeatherProofSelection, super::StudioGate1WeatherProofQueryError> {
        let search = web_sys::window()
            .and_then(|window| window.location().search().ok())
            .unwrap_or_default();
        parse_studio_gate1_weather_proof_selection(&search, scene, weather, quality)
    }

    const fn studio_mode_label(mode: StudioViewportMode) -> &'static str {
        match mode {
            StudioViewportMode::Gate0SceneProof => "gate0_scene_proof",
            StudioViewportMode::Gate1VisualValley => "gate1_visual_valley",
        }
    }

    const fn water_adapter_label(status: StudioWaterAdapterStatus) -> &'static str {
        match status {
            StudioWaterAdapterStatus::NotApplicable => "hors scène Gate 0",
            StudioWaterAdapterStatus::InstalledEngineAdapter => "adapter moteur installé",
        }
    }

    const fn quality_tier_label(tier: super::VisualQualityTier) -> &'static str {
        match tier {
            super::VisualQualityTier::Low => "low",
            super::VisualQualityTier::Medium => "medium",
            super::VisualQualityTier::High => "high",
            super::VisualQualityTier::Ultra => "ultra",
        }
    }

    const fn water_observation_label(status: WaterRuntimeObservationStatus) -> &'static str {
        match status {
            WaterRuntimeObservationStatus::NotObserved => "not_observed",
            WaterRuntimeObservationStatus::ObservedMainWorldComponents => {
                "observed_main_world_components"
            }
            WaterRuntimeObservationStatus::ObservedIncompleteComponents => {
                "observed_incomplete_components"
            }
        }
    }

    const fn weather_observation_label(status: WeatherProjectionStatus) -> &'static str {
        match status {
            WeatherProjectionStatus::NotObserved => "not_observed",
            WeatherProjectionStatus::ObservedCpuProjection => "observed_cpu_projection",
            WeatherProjectionStatus::InvalidCameraTopology => "invalid_camera_topology",
            WeatherProjectionStatus::IncompleteRainBatch => "incomplete_rain_batch",
            WeatherProjectionStatus::IncompleteMaterialProjection => {
                "incomplete_material_projection"
            }
        }
    }

    const fn scenery_observation_label(status: SceneryProjectionStatus) -> &'static str {
        match status {
            SceneryProjectionStatus::Pending => "pending",
            SceneryProjectionStatus::Activated => "activated",
            SceneryProjectionStatus::ProceduralFallbackLoadFailure => {
                "procedural_fallback_load_failure"
            }
        }
    }

    const fn weather_state_label(state: super::WeatherState) -> &'static str {
        match state {
            super::WeatherState::Clear => "clear",
            super::WeatherState::Rain => "rain",
        }
    }

    fn scene_parity_summary(telemetry: &StudioSceneTelemetry) -> String {
        match telemetry.parity_status {
            StudioSceneParityStatus::AwaitingRuntime => {
                "hashes sélectionnés · runtime non observé".to_owned()
            }
            StudioSceneParityStatus::SourceManifestMatched => {
                let Some(runtime) = telemetry.runtime_observation.as_ref() else {
                    return "hashes sources alignés · observation runtime absente".to_owned();
                };
                format!(
                    "hashes alignés · caméras {}/{} (attendu {}) · eau {} · météo {} ({}) · décor {}",
                    runtime.active_camera_3d_entities,
                    runtime.camera_3d_entities,
                    telemetry.selected_manifest.expected_active_camera_count,
                    runtime.water_observation.as_deref().unwrap_or("n/a"),
                    runtime.weather_observation.as_deref().unwrap_or("n/a"),
                    runtime.weather_target_state.as_deref().unwrap_or("n/a"),
                    runtime
                        .scenery_observation
                        .as_ref()
                        .map_or("n/a", |scenery| scenery.status.as_str())
                )
            }
            StudioSceneParityStatus::SourceManifestMismatch => {
                "ÉCHEC : manifestes source sélection/runtime divergents".to_owned()
            }
            StudioSceneParityStatus::RuntimeContractMismatch => format!(
                "ÉCHEC runtime : {}",
                telemetry
                    .failure_reason
                    .as_deref()
                    .unwrap_or("raison absente")
            ),
            StudioSceneParityStatus::ConfigurationError => format!(
                "ÉCHEC configuration : {}",
                telemetry
                    .failure_reason
                    .as_deref()
                    .unwrap_or("raison absente")
            ),
        }
    }

    fn studio_telemetry_target() -> StudioTelemetryTarget {
        if tauri_core().is_ok() {
            StudioTelemetryTarget::TauriStudio
        } else {
            StudioTelemetryTarget::WasmBrowser
        }
    }

    fn performance_now() -> f64 {
        web_sys::window()
            .and_then(|window| window.performance())
            .map_or_else(js_sys::Date::now, |performance| performance.now())
    }

    fn recorded_at_utc() -> String {
        js_sys::Date::new_0()
            .to_iso_string()
            .as_string()
            .unwrap_or_else(|| "date-unavailable".to_owned())
    }

    fn frame_summary(ledger: &Gate0StudioLedger) -> String {
        ledger.frame.as_ref().map_or_else(
            || format!("collecte 0/{FRAME_MEASURED_SAMPLES}"),
            |frame| {
                format!(
                    "{} échantillons · moyenne {:.2} ms · p95 {:.2} ms",
                    frame.samples, frame.mean_ms, frame.p95_ms
                )
            },
        )
    }

    fn interaction_summary(ledger: &Gate0StudioLedger) -> String {
        ledger
            .interaction
            .handler_to_post_paint
            .as_ref()
            .map_or_else(
                || "aucune interaction mesurée".to_owned(),
                |latency| {
                    format!(
                        "{} échantillons · p95 {:.2} ms · canvas stable {}/{}",
                        latency.samples,
                        latency.p95_ms,
                        ledger.interaction.stable_canvas_observations,
                        ledger
                            .interaction
                            .stable_canvas_observations
                            .saturating_add(ledger.interaction.unstable_canvas_observations)
                    )
                },
            )
    }

    fn permission_summary(ledger: &Gate0StudioLedger) -> String {
        match (
            ledger.permissions.allowed.status,
            ledger.permissions.denied.status,
        ) {
            (AllowedCommandProbeStatus::NotRun, DeniedCommandProbeStatus::NotRun) => {
                "non testée".to_owned()
            }
            (AllowedCommandProbeStatus::Succeeded, DeniedCommandProbeStatus::ExplicitlyDenied) => {
                "commande Forge autorisée · refus ACL confirmé".to_owned()
            }
            (_, DeniedCommandProbeStatus::NotRun) => "refus ACL non testé".to_owned(),
            (_, DeniedCommandProbeStatus::BrowserBackendAbsent) => {
                "backend absent (navigateur)".to_owned()
            }
            (_, DeniedCommandProbeStatus::ExplicitlyDenied) => {
                "refus ACL confirmé · commande Forge en échec".to_owned()
            }
            (_, DeniedCommandProbeStatus::UnexpectedlyAllowed) => {
                "ÉCHEC : commande core autorisée".to_owned()
            }
            (_, DeniedCommandProbeStatus::ProbeFailed) => "sonde non concluante".to_owned(),
        }
    }

    async fn invoke_tauri(command: &StudioCommand) -> Result<StudioResponse, String> {
        let arguments = js_sys::Object::new();
        let payload = serde_wasm_bindgen::to_value(command)
            .map_err(|error| format!("Commande Tauri non sérialisable : {error}"))?;
        js_sys::Reflect::set(&arguments, &JsValue::from_str("command"), &payload)
            .map_err(|error| format!("Arguments Tauri invalides : {}", js_error(&error)))?;
        let response = invoke_tauri_raw("execute_studio_command", arguments.as_ref()).await?;
        serde_wasm_bindgen::from_value(response)
            .map_err(|error| format!("Réponse Tauri invalide : {error}"))
    }

    fn tauri_core() -> Result<JsValue, String> {
        let window = web_sys::window().ok_or_else(|| "Fenêtre navigateur absente".to_owned())?;
        let tauri = js_sys::Reflect::get(window.as_ref(), &JsValue::from_str("__TAURI__"))
            .map_err(|error| format!("API Tauri inaccessible : {}", js_error(&error)))?;
        if tauri.is_null() || tauri.is_undefined() {
            return Err("Mode navigateur autonome : backend Tauri absent".to_owned());
        }
        js_sys::Reflect::get(&tauri, &JsValue::from_str("core"))
            .map_err(|error| format!("API Tauri core inaccessible : {}", js_error(&error)))
    }

    async fn invoke_tauri_raw(command: &str, arguments: &JsValue) -> Result<JsValue, String> {
        let core = tauri_core()?;
        let invoke = js_sys::Reflect::get(&core, &JsValue::from_str("invoke"))
            .map_err(|error| format!("API Tauri invoke inaccessible : {}", js_error(&error)))?
            .dyn_into::<js_sys::Function>()
            .map_err(|_| "API Tauri invoke invalide".to_owned())?;
        let result = invoke
            .call2(&core, &JsValue::from_str(command), arguments)
            .map_err(|error| format!("Invocation Tauri impossible : {}", js_error(&error)))?;
        let promise = result
            .dyn_into::<js_sys::Promise>()
            .map_err(|_| "Tauri invoke n’a pas retourné de Promise".to_owned())?;
        wasm_bindgen_futures::JsFuture::from(promise)
            .await
            .map_err(|error| format!("Commande Tauri échouée : {}", js_error(&error)))
    }

    async fn run_tauri_permission_probe(telemetry: WriteSignal<Gate0StudioLedger>) {
        let document = super::embedded_gate0_proof_document();
        let (allowed_status, allowed_reason) = match (
            document,
            invoke_tauri(&StudioCommand::SceneSummary).await,
        ) {
            (
                Ok(document),
                Ok(StudioResponse::SceneSummary {
                    source_hash,
                    entity_count,
                }),
            ) => {
                let expected_hash = document.content_hash().map(|hash| hash.to_string());
                if expected_hash.is_ok_and(|hash| hash == source_hash)
                    && entity_count == document.entities.len()
                {
                    (
                        AllowedCommandProbeStatus::Succeeded,
                        Some(format!(
                            "SceneSummary accepté : {entity_count} entités, source {source_hash}"
                        )),
                    )
                } else {
                    (
                        AllowedCommandProbeStatus::Failed,
                        Some(format!(
                            "SceneSummary incohérent : {entity_count} entités, source {source_hash}"
                        )),
                    )
                }
            }
            (Err(error), _) => (
                AllowedCommandProbeStatus::Failed,
                Some(format!("Document partagé invalide : {error}")),
            ),
            (_, Ok(response)) => (
                AllowedCommandProbeStatus::Failed,
                Some(format!("Réponse inattendue à SceneSummary : {response:?}")),
            ),
            (_, Err(reason)) => (AllowedCommandProbeStatus::Failed, Some(reason)),
        };

        let arguments = js_sys::Object::new();
        let label_set = js_sys::Reflect::set(
            &arguments,
            &JsValue::from_str("label"),
            &JsValue::from_str("main"),
        );
        let value_set = js_sys::Reflect::set(
            &arguments,
            &JsValue::from_str("value"),
            &JsValue::from_str("OpenRPG Forge Studio"),
        );
        if label_set.is_err() || value_set.is_err() {
            LeptosUpdate::update(&telemetry, |ledger| {
                ledger.permissions.allowed.status = allowed_status;
                ledger.permissions.allowed.reason = allowed_reason;
                ledger.permissions.denied.status = DeniedCommandProbeStatus::ProbeFailed;
                ledger.permissions.denied.reason =
                    Some("Impossible de construire les arguments de la sonde ACL".to_owned());
                ledger.recorded_at_utc = Some(recorded_at_utc());
            });
            return;
        }

        let (status, reason) =
            match invoke_tauri_raw(TAURI_DENIED_COMMAND, arguments.as_ref()).await {
                Ok(_) => (
                    DeniedCommandProbeStatus::UnexpectedlyAllowed,
                    Some("La commande explicitement refusée a été exécutée".to_owned()),
                ),
                Err(reason) if reason.contains("backend Tauri absent") => {
                    (DeniedCommandProbeStatus::BrowserBackendAbsent, Some(reason))
                }
                Err(reason) if is_explicit_acl_denial(&reason) => {
                    (DeniedCommandProbeStatus::ExplicitlyDenied, Some(reason))
                }
                Err(reason) => (DeniedCommandProbeStatus::ProbeFailed, Some(reason)),
            };
        LeptosUpdate::update(&telemetry, |ledger| {
            ledger.permissions.allowed.status = allowed_status;
            ledger.permissions.allowed.reason = allowed_reason;
            ledger.permissions.denied.status = status;
            ledger.permissions.denied.reason = reason;
            ledger.recorded_at_utc = Some(recorded_at_utc());
        });
    }

    async fn preflight_webgpu(probe_mode: Gate0ProbeMode) -> Result<(), String> {
        if probe_mode == Gate0ProbeMode::WebGpuUnavailable {
            return Err(
                "Injection diagnostique Gate 0 : WebGPU déclaré indisponible avant toute création d’adaptateur"
                    .to_owned(),
            );
        }
        let window = web_sys::window().ok_or_else(|| "Fenêtre navigateur absente".to_owned())?;
        let gpu = js_sys::Reflect::get(window.navigator().as_ref(), &JsValue::from_str("gpu"))
            .map_err(|error| format!("Accès à navigator.gpu refusé : {}", js_error(&error)))?;
        if gpu.is_null() || gpu.is_undefined() {
            return Err(
                "navigator.gpu est absent : WebGPU n’est pas disponible dans ce contexte"
                    .to_owned(),
            );
        }

        let request_adapter = js_sys::Reflect::get(&gpu, &JsValue::from_str("requestAdapter"))
            .map_err(|error| format!("requestAdapter inaccessible : {}", js_error(&error)))?
            .dyn_into::<js_sys::Function>()
            .map_err(|_| "navigator.gpu.requestAdapter n’est pas appelable".to_owned())?;
        let adapter_promise = request_adapter
            .call0(&gpu)
            .map_err(|error| format!("requestAdapter a levé une erreur : {}", js_error(&error)))?
            .dyn_into::<js_sys::Promise>()
            .map_err(|_| "requestAdapter n’a pas retourné de Promise".to_owned())?;
        let adapter = wasm_bindgen_futures::JsFuture::from(adapter_promise)
            .await
            .map_err(|error| {
                format!("Aucun adaptateur WebGPU utilisable : {}", js_error(&error))
            })?;
        if adapter.is_null() || adapter.is_undefined() {
            return Err("navigator.gpu.requestAdapter n’a trouvé aucun adaptateur".to_owned());
        }

        Ok(())
    }

    fn js_error(value: &JsValue) -> String {
        value.as_string().unwrap_or_else(|| format!("{value:?}"))
    }

    fn start_viewport_once(
        scene: StudioScene,
        weather_state: super::WeatherState,
        quality_selection: StudioQualitySelection,
        browser_weather_proof_enabled: bool,
        embedded: bool,
    ) {
        if !VIEWPORT_LAUNCH_REQUESTED.swap(true, Ordering::SeqCst) {
            INITIAL_CANVAS.with(|initial| initial.replace(current_canvas()));
            start_viewport(
                scene,
                weather_state,
                quality_selection,
                browser_weather_proof_enabled,
                embedded,
            );
        }
    }

    fn start_viewport(
        scene: StudioScene,
        weather_state: super::WeatherState,
        quality_selection: StudioQualitySelection,
        browser_weather_proof_enabled: bool,
        embedded: bool,
    ) {
        let loaded_scene = LoadedStudioScene::load(scene)
            .expect("embedded Studio scene documents must remain valid and compatible");
        let scene_manifest = loaded_scene
            .manifest_with_quality(
                (scene == StudioScene::VisualValley).then_some(weather_state),
                quality_selection,
            )
            .expect("validated embedded Studio scene documents must remain hashable");
        let render_plugin = RenderPlugin {
            render_creation: RenderCreation::Automatic(Box::new(WgpuSettings {
                power_preference: bevy::render::settings::PowerPreference::None,
                ..default()
            })),
            ..default()
        };
        let window_title = format!("OpenRPG Forge WebGPU · {}", scene_manifest.display_name);
        let mut app = App::new();
        app.insert_resource(ClearColor(Color::srgb(0.015, 0.025, 0.055)))
            .insert_non_send(BrowserFrameCollector::new(scene))
            .add_plugins(
                DefaultPlugins
                    .set(WindowPlugin {
                        primary_window: Some(Window {
                            title: window_title,
                            canvas: Some("#forge-canvas".to_owned()),
                            fit_canvas_to_parent: true,
                            prevent_default_event_handling: false,
                            // The asynchronous renderer must not steal an active project field.
                            focused: !embedded,
                            resolution: WindowResolution::new(1280, 720),
                            present_mode: PresentMode::AutoVsync,
                            ..default()
                        }),
                        ..default()
                    })
                    .set(render_plugin)
                    .set(AssetPlugin {
                        meta_check: AssetMetaCheck::Never,
                        ..default()
                    }),
            );

        match loaded_scene {
            LoadedStudioScene::Gate0(document) => {
                app.add_plugins(ProofScenePlugin::new(document))
                    .add_systems(Update, publish_viewport_selection);
            }
            LoadedStudioScene::VisualValley(loaded) => {
                install_visual_valley_plugins(
                    &mut app,
                    *loaded,
                    weather_state,
                    quality_selection,
                    browser_weather_proof_enabled,
                );
            }
        }
        app.add_systems(Update, collect_browser_frame_telemetry)
            .run();
    }

    fn install_visual_valley_plugins(
        app: &mut App,
        loaded: super::LoadedVisualValley,
        weather_state: super::WeatherState,
        quality_selection: StudioQualitySelection,
        browser_weather_proof_enabled: bool,
    ) {
        let browser_weather_proof_camera = browser_weather_proof_enabled.then(|| {
            browser_weather_proof_camera_pose(
                &loaded.terrain,
                &loaded.water,
                &loaded.player_controller,
            )
            .expect(
                "embedded Visual Valley documents must provide the registered Browser water pose",
            )
        });
        let super::LoadedVisualValley {
            terrain,
            vegetation,
            environment,
            player_controller,
            water,
            weather,
            quality,
        } = loaded;
        let scenery_plugin = VisualValleySceneryPlugin::new(terrain.clone(), vegetation.clone());
        let engine_selection = match quality_selection {
            StudioQualitySelection::Fixed(tier) => VisualValleyQualitySelection::Fixed(tier),
            StudioQualitySelection::Adaptive => VisualValleyQualitySelection::Adaptive,
            StudioQualitySelection::Benchmark => VisualValleyQualitySelection::Benchmark,
        };
        let quality_plugin = VisualValleyQualityPlugin::new(quality, engine_selection);
        let startup_tier = quality_plugin.startup_tier();
        let preset = quality_plugin.startup_preset();
        let water_plugin = VisualValleyWaterPlugin::new(terrain.clone(), water)
            .with_quality_preset(preset.clone(), startup_tier);
        let weather_plugin = VisualValleyWeatherPlugin::new(weather)
            .with_initial_state(weather_state)
            .with_quality_preset(preset.clone(), startup_tier);
        let visual_valley = VisualValleyPlugin::new(terrain.clone())
            .with_governed_metrics_required()
            .with_player_controller(player_controller)
            .with_player_interactive_input(false);
        app.add_plugins(visual_valley)
            .add_plugins(
                VisualValleyEnvironmentPlugin::new(environment).with_quality_preset(preset.clone()),
            )
            .add_plugins(
                VisualValleyVegetationPlugin::new(terrain, vegetation).with_quality_preset(preset),
            )
            .add_plugins(water_plugin)
            .add_plugins(weather_plugin)
            .add_plugins(scenery_plugin)
            .add_plugins(quality_plugin);
        if let Some(camera_pose) = browser_weather_proof_camera {
            let StudioQualitySelection::Fixed(quality_tier) = quality_selection else {
                unreachable!("the Browser weather proof query parser accepts only fixed tiers");
            };
            super::gate1_browser_weather_matrix_web::install_controller(
                app,
                quality_tier,
                camera_pose,
            );
        }
    }

    struct BrowserFrameCollector {
        scene: StudioScene,
        projection_wait_started_ms: Option<f64>,
        first_real_frame_ms: Option<f64>,
        ready_reported: bool,
        viewport_start_counted: bool,
        sampling_window: StableQualityFrameWindow,
        metrics_reported: bool,
    }

    impl BrowserFrameCollector {
        fn new(scene: StudioScene) -> Self {
            Self {
                scene,
                projection_wait_started_ms: None,
                first_real_frame_ms: None,
                ready_reported: false,
                viewport_start_counted: false,
                sampling_window: StableQualityFrameWindow::new(),
                metrics_reported: false,
            }
        }
    }

    struct ObservedRuntimeScene {
        manifest: StudioSceneManifest,
        water_observation: Option<String>,
        weather_observation: Option<String>,
        weather_target_state: Option<String>,
        quality_current_tier: Option<VisualQualityTier>,
        quality_applied_tier: Option<VisualQualityTier>,
        quality_replacement_lod_start_m: Option<f32>,
        quality_replacement_lod_end_m: Option<f32>,
        scenery_observation: Option<StudioSceneryRuntimeObservation>,
    }

    enum RuntimeSceneProgress {
        Pending(String),
        Ready(Box<ObservedRuntimeScene>),
        Failed(String),
    }

    struct RuntimeSceneResources<'a> {
        proof_scene: Option<&'a ProofScene>,
        terrain: Option<&'a VisualValleyStats>,
        vegetation: Option<&'a VegetationProjectionStats>,
        environment: Option<&'a EnvironmentProjectionStats>,
        player: Option<&'a PlayerRuntimeSnapshot>,
        water: Option<&'a WaterProjectionStats>,
        weather: Option<&'a WeatherProjectionStats>,
        scenery: Option<&'a SceneryProjectionStats>,
        quality: Option<&'a VisualValleyQualityStats>,
        camera_3d_entities: u32,
        active_camera_3d_entities: u32,
    }

    fn observe_runtime_scene(
        scene: StudioScene,
        resources: &RuntimeSceneResources<'_>,
    ) -> RuntimeSceneProgress {
        match scene {
            StudioScene::Gate0 => {
                let Some(document) = resources.proof_scene else {
                    return RuntimeSceneProgress::Pending("ProofScene absent".to_owned());
                };
                let source_hash = match document.0.content_hash() {
                    Ok(hash) => hash.to_string(),
                    Err(error) => {
                        return RuntimeSceneProgress::Failed(format!(
                            "impossible de hasher la ProofScene runtime : {error}"
                        ));
                    }
                };
                RuntimeSceneProgress::Ready(Box::new(ObservedRuntimeScene {
                    manifest: StudioSceneManifest {
                        mode: StudioViewportMode::Gate0SceneProof,
                        scene: StudioScene::Gate0,
                        display_name: document.0.name.clone(),
                        source_hashes: StudioSceneSourceHashes {
                            scene_proof: Some(source_hash),
                            ..StudioSceneSourceHashes::default()
                        },
                        camera_owner: super::StudioCameraOwner::Gate0ProofDocument,
                        expected_active_camera_count: 1,
                        water_adapter: StudioWaterAdapterStatus::NotApplicable,
                        weather_state: None,
                        quality_selection: None,
                        quality_tier: None,
                    },
                    water_observation: None,
                    weather_observation: None,
                    weather_target_state: None,
                    quality_current_tier: None,
                    quality_applied_tier: None,
                    quality_replacement_lod_start_m: None,
                    quality_replacement_lod_end_m: None,
                    scenery_observation: None,
                }))
            }
            StudioScene::VisualValley => observe_visual_valley_runtime(resources),
        }
    }

    fn observe_visual_valley_runtime(
        resources: &RuntimeSceneResources<'_>,
    ) -> RuntimeSceneProgress {
        if let Err(reason) = validate_visual_valley_projection_statuses(resources) {
            return RuntimeSceneProgress::Failed(reason);
        }
        let pending = pending_visual_valley_resources(resources);
        if !pending.is_empty() {
            return RuntimeSceneProgress::Pending(format!(
                "ressources/projections encore not_observed : {}",
                pending.join(", ")
            ));
        }

        let (
            Some(terrain),
            Some(vegetation),
            Some(environment),
            Some(player),
            Some(water),
            Some(weather),
            Some(scenery),
            Some(quality),
        ) = (
            resources.terrain,
            resources.vegetation,
            resources.environment,
            resources.player,
            resources.water,
            resources.weather,
            resources.scenery,
            resources.quality,
        )
        else {
            return RuntimeSceneProgress::Pending(
                "ressources runtime Visual Valley incomplètes".to_owned(),
            );
        };
        let Some(player_hash) = player.profile_content_hash.as_ref() else {
            return RuntimeSceneProgress::Pending(
                "hash du profil joueur runtime not_observed".to_owned(),
            );
        };
        if let Err(reason) = validate_visual_valley_runtime_consistency(
            resources,
            terrain,
            vegetation,
            environment,
            water,
            weather,
        ) {
            return RuntimeSceneProgress::Failed(reason);
        }

        RuntimeSceneProgress::Ready(Box::new(ObservedRuntimeScene {
            manifest: StudioSceneManifest {
                mode: StudioViewportMode::Gate1VisualValley,
                scene: StudioScene::VisualValley,
                display_name: "Visual Valley".to_owned(),
                source_hashes: StudioSceneSourceHashes {
                    scene_proof: None,
                    terrain: Some(terrain.source_hash.clone()),
                    vegetation: Some(vegetation.vegetation_document_hash.clone()),
                    environment: Some(environment.source_hash.clone()),
                    player_controller: Some(player_hash.to_string()),
                    water: Some(water.source_hash.clone()),
                    weather: Some(weather.source_hash.clone()),
                    scenery: Some(scenery.manifest_hash.clone()),
                    quality: Some(quality.profile_hash.clone()),
                },
                camera_owner: super::StudioCameraOwner::VisualValleyPlayerController,
                expected_active_camera_count: 1,
                water_adapter: StudioWaterAdapterStatus::InstalledEngineAdapter,
                weather_state: Some(weather.target_state),
                quality_selection: Some(match quality.selection {
                    VisualValleyQualitySelection::Fixed(tier) => {
                        StudioQualitySelection::Fixed(tier)
                    }
                    VisualValleyQualitySelection::Adaptive => StudioQualitySelection::Adaptive,
                    VisualValleyQualitySelection::Benchmark => StudioQualitySelection::Benchmark,
                }),
                quality_tier: Some(quality.current_tier),
            },
            water_observation: Some(water_observation_label(water.runtime_observation).to_owned()),
            weather_observation: Some(weather_observation_label(weather.status).to_owned()),
            weather_target_state: Some(weather_state_label(weather.target_state).to_owned()),
            quality_current_tier: Some(quality.current_tier),
            quality_applied_tier: quality.dynamically_applied_tier,
            quality_replacement_lod_start_m: Some(
                SceneryProjectionStats::replacement_lod_range_for_quality(
                    &quality.requested_preset,
                )
                .0,
            ),
            quality_replacement_lod_end_m: Some(
                SceneryProjectionStats::replacement_lod_range_for_quality(
                    &quality.requested_preset,
                )
                .1,
            ),
            scenery_observation: Some(studio_scenery_runtime_observation(scenery)),
        }))
    }

    fn studio_scenery_runtime_observation(
        scenery: &SceneryProjectionStats,
    ) -> StudioSceneryRuntimeObservation {
        StudioSceneryRuntimeObservation {
            status: scenery_observation_label(scenery.status).to_owned(),
            loaded_models: scenery.loaded_models,
            failed_models: scenery.failed_models,
            unique_models: scenery.unique_models,
            requested_instances: scenery.requested_instances,
            replacement_instances: scenery.replacement_instances,
            detail_instances: scenery.detail_instances,
            selected_by_kind: StudioSceneryInstanceCounts {
                trees: scenery.selected_by_kind.trees,
                shrubs: scenery.selected_by_kind.shrubs,
                ferns: scenery.selected_by_kind.ferns,
                rocks: scenery.selected_by_kind.rocks,
                details: scenery.selected_by_kind.details,
            },
            hidden_procedural_entities: scenery.hidden_procedural_entities,
            tracked_files: scenery.tracked_files,
            tracked_bytes: scenery.tracked_bytes,
            replacement_lod_start_m: scenery.replacement_lod_start_m,
            replacement_lod_end_m: scenery.replacement_lod_end_m,
            detail_cull_distance_m: scenery.detail_cull_distance_m,
        }
    }

    fn validate_visual_valley_projection_statuses(
        resources: &RuntimeSceneResources<'_>,
    ) -> Result<(), String> {
        if let Some(environment) = resources.environment
            && environment.status != EnvironmentProjectionStatus::ObservedApproximation
        {
            return Err(format!(
                "projection environnement invalide : {:?}",
                environment.status
            ));
        }
        if let Some(water) = resources.water
            && water.runtime_observation
                == WaterRuntimeObservationStatus::ObservedIncompleteComponents
        {
            return Err("projection eau invalide : observed_incomplete_components".to_owned());
        }
        if let Some(weather) = resources.weather
            && !matches!(
                weather.status,
                WeatherProjectionStatus::NotObserved
                    | WeatherProjectionStatus::ObservedCpuProjection
            )
        {
            return Err(format!(
                "projection météo invalide : {}",
                weather_observation_label(weather.status)
            ));
        }
        if let Some(scenery) = resources.scenery
            && scenery.status == SceneryProjectionStatus::ProceduralFallbackLoadFailure
        {
            return Err(format!(
                "projection décor CC0 en fallback après échec de chargement : modèles={}/{}, échecs={}",
                scenery.loaded_models, scenery.unique_models, scenery.failed_models
            ));
        }
        Ok(())
    }

    fn pending_visual_valley_resources(resources: &RuntimeSceneResources<'_>) -> Vec<&'static str> {
        let mut pending = Vec::new();
        if resources.terrain.is_none() {
            pending.push("VisualValleyStats");
        }
        if resources.vegetation.is_none() {
            pending.push("VegetationProjectionStats");
        }
        if resources.environment.is_none() {
            pending.push("EnvironmentProjectionStats");
        }
        if resources
            .player
            .and_then(|player| player.profile_content_hash.as_ref())
            .is_none()
        {
            pending.push("PlayerRuntimeSnapshot.profile_content_hash");
        }
        match resources.water {
            None => {
                pending.push("WaterProjectionStats");
            }
            Some(water)
                if water.runtime_observation == WaterRuntimeObservationStatus::NotObserved =>
            {
                pending.push("WaterProjectionStats.runtime_observation");
            }
            Some(_) => {}
        }
        match resources.weather {
            None => {
                pending.push("WeatherProjectionStats");
            }
            Some(weather) if weather.status == WeatherProjectionStatus::NotObserved => {
                pending.push("WeatherProjectionStats.status");
            }
            Some(_) => {}
        }
        match resources.scenery {
            None => pending.push("SceneryProjectionStats"),
            Some(scenery) if scenery.status == SceneryProjectionStatus::Pending => {
                pending.push("SceneryProjectionStats.status");
            }
            Some(_) => {}
        }
        match resources.quality {
            None => pending.push("VisualValleyQualityStats"),
            Some(quality) if quality.dynamically_applied_tier != Some(quality.current_tier) => {
                pending.push("VisualValleyQualityStats.dynamically_applied_tier");
            }
            Some(_) => {}
        }
        if let (Some(scenery), Some(quality)) = (resources.scenery, resources.quality)
            && quality.dynamically_applied_tier == Some(quality.current_tier)
            && (scenery.replacement_lod_start_m.to_bits()
                != SceneryProjectionStats::replacement_lod_range_for_quality(
                    &quality.requested_preset,
                )
                .0
                .to_bits()
                || scenery.replacement_lod_end_m.to_bits()
                    != SceneryProjectionStats::replacement_lod_range_for_quality(
                        &quality.requested_preset,
                    )
                    .1
                    .to_bits())
        {
            pending.push("SceneryProjectionStats.quality_lod_range");
        }
        pending
    }

    fn validate_visual_valley_runtime_consistency(
        resources: &RuntimeSceneResources<'_>,
        terrain: &VisualValleyStats,
        vegetation: &VegetationProjectionStats,
        environment: &EnvironmentProjectionStats,
        water: &WaterProjectionStats,
        weather: &WeatherProjectionStats,
    ) -> Result<(), String> {
        if vegetation.terrain_document_hash != terrain.source_hash {
            return Err("le hash terrain de la végétation diverge du terrain runtime".to_owned());
        }
        if water.terrain_source_hash != terrain.source_hash {
            return Err("le hash terrain de l’eau diverge du terrain runtime".to_owned());
        }
        let observed_camera_count =
            usize::try_from(resources.camera_3d_entities).unwrap_or(usize::MAX);
        let observed_active_camera_count =
            usize::try_from(resources.active_camera_3d_entities).unwrap_or(usize::MAX);
        if environment.camera_3d_entities != observed_camera_count
            || environment.active_camera_3d_entities != observed_active_camera_count
            || water.camera_3d_entities != observed_camera_count
            || weather.post_processing.camera_3d_entities != observed_camera_count
            || weather.post_processing.active_camera_3d_entities != observed_active_camera_count
        {
            return Err(
                "les ressources moteur et la requête ECS divergent sur la topologie caméra"
                    .to_owned(),
            );
        }
        Ok(())
    }

    fn report_runtime_failure(
        collector: &mut BrowserFrameCollector,
        waited_ms: f64,
        reason: String,
    ) {
        let event = ViewportFailureEvent {
            recorded_at_utc: recorded_at_utc(),
            scene: collector.scene,
            waited_ms,
            reason,
        };
        match serde_json::to_string(&event) {
            Ok(encoded) => dispatch_json_event(VIEWPORT_FAILURE_EVENT, &encoded),
            Err(error) => web_sys::console::error_1(&JsValue::from_str(&format!(
                "Studio runtime-failure serialization failed: {error}"
            ))),
        }
        collector.metrics_reported = true;
    }

    fn collect_runtime_readiness(
        collector: &mut BrowserFrameCollector,
        resources: &RuntimeSceneResources<'_>,
    ) -> bool {
        let observed = match observe_runtime_scene(collector.scene, resources) {
            RuntimeSceneProgress::Ready(observed) => observed,
            RuntimeSceneProgress::Failed(reason) => {
                let now_ms = performance_now();
                let waited_ms = collector
                    .projection_wait_started_ms
                    .map_or(0.0, |started_ms| now_ms - started_ms);
                report_runtime_failure(collector, waited_ms, reason);
                return false;
            }
            RuntimeSceneProgress::Pending(reason) => {
                let now_ms = performance_now();
                let started_ms = collector.projection_wait_started_ms.get_or_insert(now_ms);
                let waited_ms = now_ms - *started_ms;
                if waited_ms < VISUAL_VALLEY_PROJECTION_TIMEOUT_MS {
                    return false;
                }
                report_runtime_failure(
                    collector,
                    waited_ms,
                    format!(
                        "timeout borné de {VISUAL_VALLEY_PROJECTION_TIMEOUT_MS:.0} ms : {reason}"
                    ),
                );
                return false;
            }
        };
        let runtime_ready_ms = performance_now();
        let first_real_frame_ms = collector.first_real_frame_ms.unwrap_or(runtime_ready_ms);
        if !collector.viewport_start_counted {
            VIEWPORT_STARTS.fetch_add(1, Ordering::SeqCst);
            collector.viewport_start_counted = true;
        }
        collector.projection_wait_started_ms = None;
        let ready = ViewportReadyEvent {
            recorded_at_utc: recorded_at_utc(),
            first_real_frame_ms,
            runtime_ready_ms,
            viewport: current_viewport_observation(),
            scene_manifest: observed.manifest,
            camera_3d_entities: resources.camera_3d_entities,
            active_camera_3d_entities: resources.active_camera_3d_entities,
            water_observation: observed.water_observation,
            weather_observation: observed.weather_observation,
            weather_target_state: observed.weather_target_state,
            quality_current_tier: observed.quality_current_tier,
            quality_applied_tier: observed.quality_applied_tier,
            quality_replacement_lod_start_m: observed.quality_replacement_lod_start_m,
            quality_replacement_lod_end_m: observed.quality_replacement_lod_end_m,
            scenery_observation: observed.scenery_observation,
            gpu_presentation: StudioGpuPresentationStatus::NotObserved,
        };
        match serde_json::to_string(&ready) {
            Ok(encoded) => dispatch_json_event(VIEWPORT_READY_EVENT, &encoded),
            Err(error) => web_sys::console::error_1(&JsValue::from_str(&format!(
                "Studio runtime-ready serialization failed: {error}"
            ))),
        }
        collector.ready_reported = true;
        true
    }

    fn current_frame_quality_fingerprint(
        scene: StudioScene,
        quality: Option<&VisualValleyQualityStats>,
        scenery: Option<&SceneryProjectionStats>,
    ) -> FrameQualityFingerprint {
        match scene {
            StudioScene::Gate0 => FrameQualityFingerprint::gate0(),
            StudioScene::VisualValley => FrameQualityFingerprint::visual_valley(
                quality.map(|stats| stats.current_tier),
                quality.and_then(|stats| stats.dynamically_applied_tier),
                quality.map(|stats| {
                    SceneryProjectionStats::replacement_lod_range_for_quality(
                        &stats.requested_preset,
                    )
                    .0
                }),
                quality.map(|stats| {
                    SceneryProjectionStats::replacement_lod_range_for_quality(
                        &stats.requested_preset,
                    )
                    .1
                }),
                scenery.map(|stats| stats.replacement_lod_start_m),
                scenery.map(|stats| stats.replacement_lod_end_m),
            ),
        }
    }

    #[allow(
        clippy::needless_pass_by_value,
        clippy::too_many_arguments,
        reason = "Bevy systems require owned system parameter wrappers"
    )]
    fn collect_browser_frame_telemetry(
        time: Res<Time<bevy::time::Real>>,
        cameras: Query<&Camera, bevy::ecs::query::With<Camera3d>>,
        proof_scene: Option<Res<ProofScene>>,
        terrain: Option<Res<VisualValleyStats>>,
        vegetation: Option<Res<VegetationProjectionStats>>,
        environment: Option<Res<EnvironmentProjectionStats>>,
        player: Option<Res<PlayerRuntimeSnapshot>>,
        water: Option<Res<WaterProjectionStats>>,
        weather: Option<Res<WeatherProjectionStats>>,
        scenery: Option<Res<SceneryProjectionStats>>,
        quality: Option<Res<VisualValleyQualityStats>>,
        mut collector: NonSendMut<BrowserFrameCollector>,
    ) {
        if collector.metrics_reported {
            return;
        }

        let delta_ms = time.delta_secs_f64() * 1_000.0;
        if !delta_ms.is_finite() || delta_ms <= 0.0 {
            return;
        }
        if collector.first_real_frame_ms.is_none() {
            collector.first_real_frame_ms = Some(performance_now());
        }
        if !collector.ready_reported {
            let camera_3d_entities = u32::try_from(cameras.iter().count()).unwrap_or(u32::MAX);
            let active_camera_3d_entities =
                u32::try_from(cameras.iter().filter(|camera| camera.is_active).count())
                    .unwrap_or(u32::MAX);
            let resources = RuntimeSceneResources {
                proof_scene: proof_scene.as_deref(),
                terrain: terrain.as_deref(),
                vegetation: vegetation.as_deref(),
                environment: environment.as_deref(),
                player: player.as_deref(),
                water: water.as_deref(),
                weather: weather.as_deref(),
                scenery: scenery.as_deref(),
                quality: quality.as_deref(),
                camera_3d_entities,
                active_camera_3d_entities,
            };
            if !collect_runtime_readiness(&mut collector, &resources) {
                return;
            }
        }
        let fingerprint = current_frame_quality_fingerprint(
            collector.scene,
            quality.as_deref(),
            scenery.as_deref(),
        );
        match collector
            .sampling_window
            .record_frame(fingerprint, delta_ms)
        {
            FrameSamplingProgress::Reset => {
                collector.ready_reported = false;
                collector.projection_wait_started_ms = None;
                return;
            }
            FrameSamplingProgress::WarmingUp | FrameSamplingProgress::Measuring => return,
            FrameSamplingProgress::Complete => {}
        }

        let Some(frame) = FrameDistribution::from_samples(
            collector.sampling_window.warmup_samples(),
            collector.sampling_window.samples_ms(),
        ) else {
            web_sys::console::error_1(&JsValue::from_str(
                "Gate 0 frame telemetry contained invalid samples",
            ));
            collector.metrics_reported = true;
            return;
        };
        let event = RuntimeTelemetryEvent {
            recorded_at_utc: recorded_at_utc(),
            first_real_frame_ms: collector
                .first_real_frame_ms
                .unwrap_or_else(performance_now),
            memory: collect_browser_memory(),
            frame,
            viewport: current_viewport_observation(),
        };
        match serde_json::to_string(&event) {
            Ok(encoded) => dispatch_json_event(RUNTIME_TELEMETRY_EVENT, &encoded),
            Err(error) => web_sys::console::error_1(&JsValue::from_str(&format!(
                "Gate 0 telemetry serialization failed: {error}"
            ))),
        }
        collector.metrics_reported = true;
    }

    fn current_viewport_observation() -> ViewportObservation {
        let canvas = current_canvas();
        ViewportObservation {
            starts: u32::try_from(VIEWPORT_STARTS.load(Ordering::SeqCst)).unwrap_or(u32::MAX),
            canvas_width_px: canvas
                .as_ref()
                .and_then(|element| u32::try_from(element.client_width()).ok()),
            canvas_height_px: canvas
                .as_ref()
                .and_then(|element| u32::try_from(element.client_height()).ok()),
        }
    }

    fn collect_browser_memory() -> BrowserMemoryObservation {
        let wasm_linear_bytes =
            js_sys::Reflect::get(&wasm_bindgen::memory(), &JsValue::from_str("buffer"))
                .ok()
                .and_then(|buffer| {
                    js_sys::Reflect::get(&buffer, &JsValue::from_str("byteLength"))
                        .ok()
                        .and_then(|value| finite_non_negative_u64(value.as_f64()))
                });

        let memory = web_sys::window()
            .and_then(|window| window.performance())
            .and_then(|performance| {
                js_sys::Reflect::get(performance.as_ref(), &JsValue::from_str("memory")).ok()
            })
            .filter(|value| !value.is_null() && !value.is_undefined());
        let js_heap_used_bytes = memory.as_ref().and_then(|value| {
            js_sys::Reflect::get(value, &JsValue::from_str("usedJSHeapSize"))
                .ok()
                .and_then(|number| finite_non_negative_u64(number.as_f64()))
        });
        let js_heap_limit_bytes = memory.as_ref().and_then(|value| {
            js_sys::Reflect::get(value, &JsValue::from_str("jsHeapSizeLimit"))
                .ok()
                .and_then(|number| finite_non_negative_u64(number.as_f64()))
        });

        BrowserMemoryObservation {
            wasm_linear_bytes,
            js_heap_used_bytes,
            js_heap_limit_bytes,
            js_heap_api_available: memory.is_some(),
        }
    }

    fn finite_non_negative_u64(value: Option<f64>) -> Option<u64> {
        value
            .filter(|number| number.is_finite() && *number >= 0.0 && number.fract() == 0.0)
            .and_then(|number| format!("{number:.0}").parse().ok())
    }

    fn dispatch_json_event(name: &str, encoded: &str) {
        let Some(window) = web_sys::window() else {
            return;
        };
        let init = web_sys::CustomEventInit::new();
        init.set_detail(&JsValue::from_str(encoded));
        match web_sys::CustomEvent::new_with_event_init_dict(name, &init) {
            Ok(event) => {
                if let Err(error) = window.dispatch_event(&event) {
                    web_sys::console::error_1(&error);
                }
            }
            Err(error) => web_sys::console::error_1(&error),
        }
    }

    #[allow(
        clippy::needless_pass_by_value,
        reason = "Bevy systems require owned system parameter wrappers"
    )]
    fn publish_viewport_selection(selection: Res<ProofSelection>) {
        if !selection.is_changed() {
            return;
        }
        let Some(entity_id) = selection.0 else {
            return;
        };
        let Some(window) = web_sys::window() else {
            return;
        };
        let init = web_sys::CustomEventInit::new();
        init.set_detail(&JsValue::from_str(&entity_id.to_string()));
        match web_sys::CustomEvent::new_with_event_init_dict(VIEWPORT_SELECTION_EVENT, &init) {
            Ok(event) => {
                if let Err(error) = window.dispatch_event(&event) {
                    web_sys::console::error_1(&error);
                }
            }
            Err(error) => web_sys::console::error_1(&error),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn typed_command_and_response_round_trip() {
        let id = EntityId::deterministic("selected-cube");
        let command = StudioCommand::SelectEntity { entity_id: id };
        let encoded = serde_json::to_string(&command).unwrap();
        let decoded: StudioCommand = serde_json::from_str(&encoded).unwrap();
        assert_eq!(decoded, command);

        let response = StudioResponse::SelectionAccepted { entity_id: id };
        let encoded = serde_json::to_string(&response).unwrap();
        let decoded: StudioResponse = serde_json::from_str(&encoded).unwrap();
        assert_eq!(decoded, response);
    }

    #[test]
    fn selection_response_must_match_the_requested_entity() {
        let requested = EntityId::deterministic("requested");
        let other = EntityId::deterministic("other");

        assert!(
            validate_selection_response(
                requested,
                StudioResponse::SelectionAccepted {
                    entity_id: requested,
                },
            )
            .is_ok()
        );
        assert!(
            validate_selection_response(
                requested,
                StudioResponse::SelectionAccepted { entity_id: other },
            )
            .unwrap_err()
            .contains("incohérente")
        );
    }

    #[test]
    fn scene_query_is_explicit_deterministic_and_gate0_compatible() {
        assert_eq!(
            parse_studio_scene_selection(""),
            StudioSceneSelection::default()
        );
        assert_eq!(
            parse_studio_scene_selection("?forge_gate0_probe=tauri-permission").scene,
            StudioScene::Gate0
        );
        assert_eq!(
            parse_studio_scene_selection(
                "?forge_gate0_probe=tauri-permission&forge_scene=visual-valley"
            ),
            StudioSceneSelection {
                scene: StudioScene::VisualValley,
                source: StudioSceneSelectionSource::QueryString,
                requested_value: Some("visual-valley".to_owned()),
            }
        );
        assert_eq!(
            parse_studio_scene_selection("?forge_scene=gate0").source,
            StudioSceneSelectionSource::QueryString
        );
        assert_eq!(
            parse_studio_scene_selection("?forge_scene=unknown"),
            StudioSceneSelection {
                scene: StudioScene::Gate0,
                source: StudioSceneSelectionSource::UnknownQueryFallback,
                requested_value: Some("unknown".to_owned()),
            }
        );
        assert_eq!(
            parse_studio_scene_selection("?forge_scene_extra=visual-valley").scene,
            StudioScene::Gate0
        );
        assert_eq!(
            parse_studio_weather_selection("", StudioScene::VisualValley).unwrap(),
            StudioWeatherSelection::default()
        );
        assert_eq!(
            parse_studio_weather_selection(
                "?forge_scene=visual-valley&forge_weather=rain",
                StudioScene::VisualValley
            )
            .unwrap()
            .state,
            WeatherState::Rain,
        );
        assert!(matches!(
            parse_studio_weather_selection(
                "?forge_weather=unknown",
                StudioScene::VisualValley
            ),
            Err(StudioWeatherQueryError::UnsupportedValue { requested_value })
                if requested_value == "unknown"
        ));
        assert!(matches!(
            parse_studio_weather_selection("?forge_weather", StudioScene::VisualValley),
            Err(StudioWeatherQueryError::UnsupportedValue { requested_value })
                if requested_value.is_empty()
        ));
        assert!(matches!(
            parse_studio_weather_selection(
                "?forge_weather=clear&forge_weather=rain",
                StudioScene::VisualValley
            ),
            Err(StudioWeatherQueryError::DuplicateParameter { requested_values })
                if requested_values == ["clear", "rain"]
        ));
        assert!(matches!(
            parse_studio_weather_selection(
                "?forge_weather=clear&forge_weather",
                StudioScene::VisualValley
            ),
            Err(StudioWeatherQueryError::DuplicateParameter { requested_values })
                if requested_values == ["clear", ""]
        ));
        assert!(matches!(
            parse_studio_weather_selection("?forge_weather=rain", StudioScene::Gate0),
            Err(StudioWeatherQueryError::NotApplicableToScene {
                scene: StudioScene::Gate0
            })
        ));
        assert_eq!(
            parse_studio_weather_selection("?forge_weather_extra=rain", StudioScene::VisualValley)
                .unwrap(),
            StudioWeatherSelection::default()
        );
    }

    #[test]
    fn quality_query_is_strict_and_scene_scoped() {
        assert_eq!(
            parse_studio_quality_selection("", StudioScene::VisualValley).unwrap(),
            StudioQualitySelection::Fixed(VisualQualityTier::High)
        );
        for (value, expected) in [
            ("low", StudioQualitySelection::Fixed(VisualQualityTier::Low)),
            (
                "medium",
                StudioQualitySelection::Fixed(VisualQualityTier::Medium),
            ),
            (
                "high",
                StudioQualitySelection::Fixed(VisualQualityTier::High),
            ),
            (
                "ultra",
                StudioQualitySelection::Fixed(VisualQualityTier::Ultra),
            ),
            ("adaptive", StudioQualitySelection::Adaptive),
            ("benchmark", StudioQualitySelection::Benchmark),
        ] {
            assert_eq!(
                parse_studio_quality_selection(
                    &format!("?forge_scene=visual-valley&forge_quality={value}"),
                    StudioScene::VisualValley,
                )
                .unwrap(),
                expected
            );
        }
        assert!(matches!(
            parse_studio_quality_selection(
                "?forge_quality=low&forge_quality=ultra",
                StudioScene::VisualValley,
            ),
            Err(StudioQualityQueryError::DuplicateParameter { .. })
        ));
        assert!(matches!(
            parse_studio_quality_selection("?forge_quality=cinematic", StudioScene::VisualValley),
            Err(StudioQualityQueryError::UnsupportedValue { .. })
        ));
        assert!(matches!(
            parse_studio_quality_selection("?forge_quality=high", StudioScene::Gate0),
            Err(StudioQualityQueryError::NotApplicableToScene { .. })
        ));
        assert_eq!(
            parse_studio_quality_selection(
                "?forge_quality_extra=ultra",
                StudioScene::VisualValley,
            )
            .unwrap(),
            StudioQualitySelection::Fixed(VisualQualityTier::High)
        );
    }

    #[test]
    fn visual_valley_manifest_exposes_the_eight_canonical_source_hashes() {
        let loaded = LoadedStudioScene::load(StudioScene::VisualValley).unwrap();
        let manifest = loaded.manifest(Some(WeatherState::Rain)).unwrap();

        assert_eq!(manifest.mode, StudioViewportMode::Gate1VisualValley);
        assert_eq!(manifest.scene, StudioScene::VisualValley);
        assert_eq!(
            manifest.source_hashes.terrain.as_deref(),
            Some("9f16780a1de1e9dd4b65931624a8b45281713829434292bada320a578420b2ef")
        );
        assert_eq!(
            manifest.source_hashes.vegetation.as_deref(),
            Some("664df33ffe5ea42ae8e6dc9b30e496abe62f054583f483f8019f9abb6f8d00f7")
        );
        assert_eq!(
            manifest.source_hashes.environment.as_deref(),
            Some("4b252f036887ec6dbe6ae0942862d15bdba589e7deab92e62a9091e21b6cdd6f")
        );
        assert_eq!(
            manifest.source_hashes.player_controller.as_deref(),
            Some("941eceefb29835c0949a9801f83f421839bcb5b0cd79583899c22d6832a74f90")
        );
        assert_eq!(
            manifest.source_hashes.water.as_deref(),
            Some("c581e9ef65ca1bc34d5f94908d5f43df77794da4ad5b97e1b3c4f8c0d025ebce")
        );
        assert_eq!(
            manifest.source_hashes.weather.as_deref(),
            Some("b0d8e12bd49d3510361d587463a3002e98741b68052b283fa3f8ca432c87dd9e")
        );
        assert_eq!(
            manifest.source_hashes.scenery.as_deref(),
            Some("6b41ec73efde5b8a40ca0132e1d32462176d6b4810104f6bed69496569f81f5f")
        );
        assert_eq!(
            manifest.source_hashes.quality.as_deref(),
            Some("0c4573220cc8c314e58938f617fbd7a46bdac4037a599b9a73c3be61aeb699cb")
        );
        assert_eq!(manifest.source_hashes.populated_count(), 8);
        assert_eq!(
            manifest.camera_owner,
            StudioCameraOwner::VisualValleyPlayerController
        );
        assert_eq!(manifest.expected_active_camera_count, 1);
        assert_eq!(
            manifest.water_adapter,
            StudioWaterAdapterStatus::InstalledEngineAdapter
        );
        assert_eq!(manifest.weather_state, Some(WeatherState::Rain));

        let hierarchy = loaded.hierarchy();
        let unique_ids = hierarchy
            .iter()
            .map(|entry| entry.id)
            .collect::<std::collections::HashSet<_>>();
        assert_eq!(hierarchy.len(), 8);
        assert_eq!(unique_ids.len(), hierarchy.len());
        assert!(hierarchy.iter().all(|entry| !entry.name.is_empty()));
        assert!(hierarchy.iter().all(|entry| !entry.glyph.is_empty()));
    }

    #[test]
    fn scene_telemetry_matches_source_manifests_without_claiming_webgpu_execution() {
        let selection = StudioSceneSelection {
            scene: StudioScene::VisualValley,
            source: StudioSceneSelectionSource::QueryString,
            requested_value: Some("visual-valley".to_owned()),
        };
        let manifest = LoadedStudioScene::load(selection.scene)
            .unwrap()
            .manifest(Some(WeatherState::Rain))
            .unwrap();
        let mut telemetry = StudioSceneTelemetry::new(selection, manifest.clone());

        assert_eq!(telemetry.schema_version, 8);
        assert!(
            telemetry
                .build_source_commit
                .as_deref()
                .is_none_or(|commit| {
                    commit.len() == 40 && commit.bytes().all(|byte| byte.is_ascii_hexdigit())
                })
        );

        assert_eq!(
            telemetry.parity_status,
            StudioSceneParityStatus::AwaitingRuntime
        );
        assert!(telemetry.runtime_observation.is_none());
        assert!(
            telemetry
                .limitations
                .iter()
                .any(|limit| limit.contains("does not prove browser WebGPU execution"))
        );

        telemetry.observe_runtime(
            manifest,
            StudioSceneRuntimeObservation {
                first_real_frame_ms: 12.5,
                runtime_ready_ms: 16.0,
                camera_3d_entities: 1,
                active_camera_3d_entities: 1,
                water_observation: Some("observed_main_world_components".to_owned()),
                weather_observation: Some("observed_cpu_projection".to_owned()),
                weather_target_state: Some("rain".to_owned()),
                quality_current_tier: Some(VisualQualityTier::High),
                quality_applied_tier: Some(VisualQualityTier::High),
                quality_replacement_lod_start_m: Some(51.0),
                quality_replacement_lod_end_m: Some(120.0),
                scenery_observation: Some(valid_studio_scenery_observation()),
                gpu_presentation: StudioGpuPresentationStatus::NotObserved,
                evidence_source: "synthetic_test_not_webgpu_execution".to_owned(),
            },
        );
        assert_eq!(
            telemetry.parity_status,
            StudioSceneParityStatus::SourceManifestMatched
        );
        assert_eq!(
            telemetry
                .runtime_observation
                .as_ref()
                .map(|runtime| runtime.active_camera_3d_entities),
            Some(1)
        );
    }

    #[test]
    fn adaptive_quality_runtime_tier_may_change_without_breaking_scene_parity() {
        let loaded = LoadedStudioScene::load(StudioScene::VisualValley).unwrap();
        let selected = loaded
            .manifest_with_quality(Some(WeatherState::Rain), StudioQualitySelection::Adaptive)
            .unwrap();
        let mut runtime = selected.clone();
        runtime.quality_tier = Some(VisualQualityTier::Medium);
        let mut telemetry = StudioSceneTelemetry::new(
            StudioSceneSelection {
                scene: StudioScene::VisualValley,
                source: StudioSceneSelectionSource::QueryString,
                requested_value: Some("visual-valley".to_owned()),
            },
            selected,
        );

        let mut observation = valid_visual_valley_runtime_observation();
        observation.quality_current_tier = Some(VisualQualityTier::Medium);
        observation.quality_applied_tier = Some(VisualQualityTier::Medium);
        observation.quality_replacement_lod_start_m = Some(43.0);
        observation.quality_replacement_lod_end_m = Some(104.0);
        if let Some(scenery) = observation.scenery_observation.as_mut() {
            scenery.replacement_lod_start_m = 43.0;
            scenery.replacement_lod_end_m = 104.0;
        }
        telemetry.observe_runtime(runtime, observation);

        assert_eq!(
            telemetry.parity_status,
            StudioSceneParityStatus::SourceManifestMatched
        );
    }

    #[test]
    fn adaptive_transition_near_frame_90_restarts_the_entire_sampling_window() {
        const ADAPTIVE_TRANSITION_FRAME: usize = 90;
        let high = FrameQualityFingerprint::visual_valley(
            Some(VisualQualityTier::High),
            Some(VisualQualityTier::High),
            Some(72.0),
            Some(180.0),
            Some(72.0),
            Some(180.0),
        );
        let transitioning = FrameQualityFingerprint::visual_valley(
            Some(VisualQualityTier::Medium),
            Some(VisualQualityTier::High),
            Some(60.0),
            Some(160.0),
            Some(72.0),
            Some(180.0),
        );
        let medium = FrameQualityFingerprint::visual_valley(
            Some(VisualQualityTier::Medium),
            Some(VisualQualityTier::Medium),
            Some(60.0),
            Some(160.0),
            Some(60.0),
            Some(160.0),
        );
        let mut window = StableQualityFrameWindow::new();

        for frame_index in 0..ADAPTIVE_TRANSITION_FRAME {
            let progress = window.record_frame(high, 10.0);
            if frame_index < telemetry::FRAME_WARMUP_SAMPLES {
                assert_eq!(progress, FrameSamplingProgress::WarmingUp);
            } else {
                assert_eq!(progress, FrameSamplingProgress::Measuring);
            }
        }
        assert_eq!(window.warmup_samples(), telemetry::FRAME_WARMUP_SAMPLES);
        assert_eq!(
            window.samples_ms().len(),
            ADAPTIVE_TRANSITION_FRAME - telemetry::FRAME_WARMUP_SAMPLES
        );

        assert_eq!(
            window.record_frame(transitioning, 11.0),
            FrameSamplingProgress::Reset
        );
        assert_eq!(window.warmup_samples(), 0);
        assert!(window.samples_ms().is_empty());

        let mut atomic_transition = StableQualityFrameWindow::new();
        assert_eq!(
            atomic_transition.record_frame(high, 10.0),
            FrameSamplingProgress::WarmingUp
        );
        assert_eq!(
            atomic_transition.record_frame(medium, 20.0),
            FrameSamplingProgress::Reset
        );

        for frame_index in 0..(telemetry::FRAME_WARMUP_SAMPLES + telemetry::FRAME_MEASURED_SAMPLES)
        {
            let progress = window.record_frame(medium, 20.0);
            let expected = if frame_index < telemetry::FRAME_WARMUP_SAMPLES {
                FrameSamplingProgress::WarmingUp
            } else if frame_index + 1
                < telemetry::FRAME_WARMUP_SAMPLES + telemetry::FRAME_MEASURED_SAMPLES
            {
                FrameSamplingProgress::Measuring
            } else {
                FrameSamplingProgress::Complete
            };
            assert_eq!(progress, expected);
        }
        assert_eq!(window.warmup_samples(), telemetry::FRAME_WARMUP_SAMPLES);
        assert_eq!(window.samples_ms().len(), telemetry::FRAME_MEASURED_SAMPLES);
        assert!(
            window
                .samples_ms()
                .iter()
                .all(|sample| sample.to_bits() == 20.0_f64.to_bits())
        );
    }

    #[test]
    fn gate0_sampling_fingerprint_remains_converged_without_quality_resources() {
        let mut window = StableQualityFrameWindow::new();

        assert_eq!(
            window.record_frame(FrameQualityFingerprint::gate0(), 16.0),
            FrameSamplingProgress::WarmingUp
        );
        assert_eq!(window.warmup_samples(), 1);
        assert!(window.samples_ms().is_empty());
    }

    #[test]
    fn gate0_runtime_contract_remains_compatible_with_explicit_timestamps() {
        let manifest = LoadedStudioScene::load(StudioScene::Gate0)
            .unwrap()
            .manifest(None)
            .unwrap();
        let mut telemetry =
            StudioSceneTelemetry::new(StudioSceneSelection::default(), manifest.clone());

        telemetry.observe_runtime(
            manifest,
            StudioSceneRuntimeObservation {
                first_real_frame_ms: 12.5,
                runtime_ready_ms: 13.0,
                camera_3d_entities: 1,
                active_camera_3d_entities: 1,
                water_observation: None,
                weather_observation: None,
                weather_target_state: None,
                quality_current_tier: None,
                quality_applied_tier: None,
                quality_replacement_lod_start_m: None,
                quality_replacement_lod_end_m: None,
                scenery_observation: None,
                gpu_presentation: StudioGpuPresentationStatus::NotObserved,
                evidence_source: "synthetic_gate0_contract_regression".to_owned(),
            },
        );

        assert_eq!(
            telemetry.parity_status,
            StudioSceneParityStatus::SourceManifestMatched
        );
        assert!(is_validated_gate0_runtime(&telemetry));
        let runtime = telemetry.runtime_observation.as_ref().unwrap();
        assert!((runtime.first_real_frame_ms - 12.5).abs() < f64::EPSILON);
        assert!((runtime.runtime_ready_ms - 13.0).abs() < f64::EPSILON);
        assert_eq!(runtime.quality_current_tier, None);
        assert_eq!(runtime.quality_applied_tier, None);
        assert_eq!(runtime.quality_replacement_lod_start_m, None);
        assert_eq!(runtime.quality_replacement_lod_end_m, None);
        assert_eq!(
            runtime.gpu_presentation,
            StudioGpuPresentationStatus::NotObserved
        );

        let selected = telemetry.selected_manifest.clone();
        let mut mismatched_runtime = selected.clone();
        mismatched_runtime.source_hashes.scene_proof = Some("mismatched-gate0-hash".to_owned());
        let mut mismatch = StudioSceneTelemetry::new(StudioSceneSelection::default(), selected);
        mismatch.observe_runtime(
            mismatched_runtime,
            StudioSceneRuntimeObservation {
                first_real_frame_ms: 12.5,
                runtime_ready_ms: 13.0,
                camera_3d_entities: 1,
                active_camera_3d_entities: 1,
                water_observation: None,
                weather_observation: None,
                weather_target_state: None,
                quality_current_tier: None,
                quality_applied_tier: None,
                quality_replacement_lod_start_m: None,
                quality_replacement_lod_end_m: None,
                scenery_observation: None,
                gpu_presentation: StudioGpuPresentationStatus::NotObserved,
                evidence_source: "synthetic_gate0_contract_mismatch".to_owned(),
            },
        );
        assert_eq!(
            mismatch.parity_status,
            StudioSceneParityStatus::SourceManifestMismatch
        );
        assert!(!is_validated_gate0_runtime(&mismatch));
    }

    fn valid_visual_valley_runtime_observation() -> StudioSceneRuntimeObservation {
        StudioSceneRuntimeObservation {
            first_real_frame_ms: 12.5,
            runtime_ready_ms: 16.0,
            camera_3d_entities: 1,
            active_camera_3d_entities: 1,
            water_observation: Some("observed_main_world_components".to_owned()),
            weather_observation: Some("observed_cpu_projection".to_owned()),
            weather_target_state: Some("rain".to_owned()),
            quality_current_tier: Some(VisualQualityTier::High),
            quality_applied_tier: Some(VisualQualityTier::High),
            quality_replacement_lod_start_m: Some(51.0),
            quality_replacement_lod_end_m: Some(120.0),
            scenery_observation: Some(valid_studio_scenery_observation()),
            gpu_presentation: StudioGpuPresentationStatus::NotObserved,
            evidence_source: "synthetic_headless_contract_test".to_owned(),
        }
    }

    fn valid_studio_scenery_observation() -> StudioSceneryRuntimeObservation {
        StudioSceneryRuntimeObservation {
            status: "activated".to_owned(),
            loaded_models: 9,
            failed_models: 0,
            unique_models: 9,
            requested_instances: 15,
            replacement_instances: 12,
            detail_instances: 3,
            selected_by_kind: StudioSceneryInstanceCounts {
                trees: 5,
                shrubs: 4,
                ferns: 3,
                rocks: 2,
                details: 1,
            },
            hidden_procedural_entities: 5,
            tracked_files: 27,
            tracked_bytes: 16_652_493,
            replacement_lod_start_m: 51.0,
            replacement_lod_end_m: 120.0,
            detail_cull_distance_m: 120.0,
        }
    }

    fn visual_valley_rain_manifest() -> StudioSceneManifest {
        LoadedStudioScene::load(StudioScene::VisualValley)
            .unwrap()
            .manifest(Some(WeatherState::Rain))
            .unwrap()
    }

    #[test]
    fn headless_runtime_scenery_counts_accept_exact_kind_contract() {
        let selected = visual_valley_rain_manifest();
        let runtime = selected.clone();
        let mut telemetry = StudioSceneTelemetry::new(
            StudioSceneSelection {
                scene: StudioScene::VisualValley,
                source: StudioSceneSelectionSource::QueryString,
                requested_value: Some("visual-valley".to_owned()),
            },
            selected,
        );

        telemetry.observe_runtime(runtime, valid_visual_valley_runtime_observation());

        assert_eq!(
            telemetry.parity_status,
            StudioSceneParityStatus::SourceManifestMatched
        );
        assert_eq!(telemetry.failure_reason, None);
    }

    #[test]
    fn headless_runtime_scenery_counts_reject_each_derived_mismatch() {
        let valid = valid_visual_valley_runtime_observation();

        let mut requested_mismatch = valid.clone();
        requested_mismatch
            .scenery_observation
            .as_mut()
            .unwrap()
            .requested_instances += 1;

        let mut replacement_mismatch = valid.clone();
        replacement_mismatch
            .scenery_observation
            .as_mut()
            .unwrap()
            .replacement_instances += 1;

        let mut detail_mismatch = valid.clone();
        detail_mismatch
            .scenery_observation
            .as_mut()
            .unwrap()
            .detail_instances += 1;

        let mut hidden_mismatch = valid.clone();
        hidden_mismatch
            .scenery_observation
            .as_mut()
            .unwrap()
            .hidden_procedural_entities -= 1;

        let mut kind_mismatch = valid;
        kind_mismatch
            .scenery_observation
            .as_mut()
            .unwrap()
            .selected_by_kind
            .trees += 1;

        for (case_index, observation) in [
            requested_mismatch,
            replacement_mismatch,
            detail_mismatch,
            hidden_mismatch,
            kind_mismatch,
        ]
        .into_iter()
        .enumerate()
        {
            let selected = visual_valley_rain_manifest();
            let runtime = selected.clone();
            let mut telemetry = StudioSceneTelemetry::new(
                StudioSceneSelection {
                    scene: StudioScene::VisualValley,
                    source: StudioSceneSelectionSource::QueryString,
                    requested_value: Some("visual-valley".to_owned()),
                },
                selected,
            );

            telemetry.observe_runtime(runtime, observation);

            assert_eq!(
                telemetry.parity_status,
                StudioSceneParityStatus::RuntimeContractMismatch,
                "scenery mismatch case {case_index}"
            );
            assert!(
                telemetry
                    .failure_reason
                    .as_deref()
                    .is_some_and(|reason| reason.contains("décor CC0")),
                "scenery mismatch case {case_index}"
            );
        }
    }

    #[test]
    fn headless_runtime_manifest_hash_mismatch_is_explicit() {
        let selected = visual_valley_rain_manifest();
        let mut runtime = selected.clone();
        runtime.source_hashes.water = Some("different-water-hash".to_owned());
        let mut telemetry = StudioSceneTelemetry::new(
            StudioSceneSelection {
                scene: StudioScene::VisualValley,
                source: StudioSceneSelectionSource::QueryString,
                requested_value: Some("visual-valley".to_owned()),
            },
            selected,
        );

        telemetry.observe_runtime(runtime, valid_visual_valley_runtime_observation());

        assert_eq!(
            telemetry.parity_status,
            StudioSceneParityStatus::SourceManifestMismatch
        );
        assert!(
            telemetry
                .failure_reason
                .as_deref()
                .is_some_and(|reason| reason.contains("hashes"))
        );
    }

    #[test]
    fn headless_runtime_missing_scenery_observation_is_explicit() {
        let selected = visual_valley_rain_manifest();
        let runtime = selected.clone();
        let mut observation = valid_visual_valley_runtime_observation();
        observation.scenery_observation = None;
        let mut telemetry = StudioSceneTelemetry::new(
            StudioSceneSelection {
                scene: StudioScene::VisualValley,
                source: StudioSceneSelectionSource::QueryString,
                requested_value: Some("visual-valley".to_owned()),
            },
            selected,
        );

        telemetry.observe_runtime(runtime, observation);

        assert_eq!(
            telemetry.parity_status,
            StudioSceneParityStatus::RuntimeContractMismatch
        );
        assert!(
            telemetry
                .failure_reason
                .as_deref()
                .is_some_and(|reason| reason.contains("décor CC0"))
        );
    }

    #[test]
    fn headless_runtime_unapplied_quality_tier_is_explicit() {
        let selected = visual_valley_rain_manifest();
        let runtime = selected.clone();
        let mut observation = valid_visual_valley_runtime_observation();
        observation.quality_applied_tier = None;
        let mut telemetry = StudioSceneTelemetry::new(
            StudioSceneSelection {
                scene: StudioScene::VisualValley,
                source: StudioSceneSelectionSource::QueryString,
                requested_value: Some("visual-valley".to_owned()),
            },
            selected,
        );

        telemetry.observe_runtime(runtime, observation);

        assert_eq!(
            telemetry.parity_status,
            StudioSceneParityStatus::RuntimeContractMismatch
        );
        assert!(
            telemetry
                .failure_reason
                .as_deref()
                .is_some_and(|reason| reason.contains("qualité"))
        );
    }

    #[test]
    fn headless_runtime_scenery_lod_must_match_the_applied_quality_preset() {
        let selected = visual_valley_rain_manifest();
        let runtime = selected.clone();
        let mut observation = valid_visual_valley_runtime_observation();
        observation
            .scenery_observation
            .as_mut()
            .unwrap()
            .replacement_lod_start_m = 73.0;
        let mut telemetry = StudioSceneTelemetry::new(
            StudioSceneSelection {
                scene: StudioScene::VisualValley,
                source: StudioSceneSelectionSource::QueryString,
                requested_value: Some("visual-valley".to_owned()),
            },
            selected,
        );

        telemetry.observe_runtime(runtime, observation);

        assert_eq!(
            telemetry.parity_status,
            StudioSceneParityStatus::RuntimeContractMismatch
        );
        assert!(
            telemetry
                .failure_reason
                .as_deref()
                .is_some_and(|reason| reason.contains("plage LOD"))
        );
    }

    #[test]
    fn headless_runtime_scenery_fallback_is_explicit() {
        let selected = visual_valley_rain_manifest();
        let runtime = selected.clone();
        let mut observation = valid_visual_valley_runtime_observation();
        let scenery = observation.scenery_observation.as_mut().unwrap();
        scenery.status = "procedural_fallback_load_failure".to_owned();
        scenery.loaded_models = 8;
        scenery.failed_models = 1;
        let mut telemetry = StudioSceneTelemetry::new(
            StudioSceneSelection {
                scene: StudioScene::VisualValley,
                source: StudioSceneSelectionSource::QueryString,
                requested_value: Some("visual-valley".to_owned()),
            },
            selected,
        );

        telemetry.observe_runtime(runtime, observation);

        assert_eq!(
            telemetry.parity_status,
            StudioSceneParityStatus::RuntimeContractMismatch
        );
        assert!(
            telemetry
                .failure_reason
                .as_deref()
                .is_some_and(|reason| reason.contains("décor CC0"))
        );
    }

    #[test]
    fn headless_runtime_weather_mismatch_is_explicit() {
        let selected = visual_valley_rain_manifest();
        let mut runtime = selected.clone();
        runtime.weather_state = Some(WeatherState::Clear);
        let mut observation = valid_visual_valley_runtime_observation();
        observation.weather_target_state = Some("clear".to_owned());
        let mut telemetry = StudioSceneTelemetry::new(
            StudioSceneSelection {
                scene: StudioScene::VisualValley,
                source: StudioSceneSelectionSource::QueryString,
                requested_value: Some("visual-valley".to_owned()),
            },
            selected,
        );

        telemetry.observe_runtime(runtime, observation);

        assert_eq!(
            telemetry.parity_status,
            StudioSceneParityStatus::RuntimeContractMismatch
        );
        assert!(
            telemetry
                .failure_reason
                .as_deref()
                .is_some_and(|reason| reason.contains("météo"))
        );
    }

    #[test]
    fn headless_runtime_camera_mismatch_is_explicit() {
        let selected = visual_valley_rain_manifest();
        let runtime = selected.clone();
        let mut observation = valid_visual_valley_runtime_observation();
        observation.active_camera_3d_entities = 0;
        let mut telemetry = StudioSceneTelemetry::new(
            StudioSceneSelection {
                scene: StudioScene::VisualValley,
                source: StudioSceneSelectionSource::QueryString,
                requested_value: Some("visual-valley".to_owned()),
            },
            selected,
        );

        telemetry.observe_runtime(runtime, observation);

        assert_eq!(
            telemetry.parity_status,
            StudioSceneParityStatus::RuntimeContractMismatch
        );
        assert!(
            telemetry
                .failure_reason
                .as_deref()
                .is_some_and(|reason| reason.contains("caméra"))
        );
    }

    #[test]
    fn headless_runtime_manifest_camera_contract_mismatch_is_explicit() {
        let selected = visual_valley_rain_manifest();
        let mut runtime = selected.clone();
        runtime.expected_active_camera_count = 99;
        let mut telemetry = StudioSceneTelemetry::new(
            StudioSceneSelection {
                scene: StudioScene::VisualValley,
                source: StudioSceneSelectionSource::QueryString,
                requested_value: Some("visual-valley".to_owned()),
            },
            selected,
        );

        telemetry.observe_runtime(runtime, valid_visual_valley_runtime_observation());

        assert_eq!(
            telemetry.parity_status,
            StudioSceneParityStatus::RuntimeContractMismatch
        );
        assert!(
            telemetry
                .failure_reason
                .as_deref()
                .is_some_and(|reason| reason.contains("caméra"))
        );
    }

    #[test]
    fn headless_runtime_timestamp_order_is_explicit() {
        let selected = visual_valley_rain_manifest();
        let runtime = selected.clone();
        let mut observation = valid_visual_valley_runtime_observation();
        observation.runtime_ready_ms = observation.first_real_frame_ms - 1.0;
        let mut telemetry = StudioSceneTelemetry::new(
            StudioSceneSelection {
                scene: StudioScene::VisualValley,
                source: StudioSceneSelectionSource::QueryString,
                requested_value: Some("visual-valley".to_owned()),
            },
            selected,
        );

        telemetry.observe_runtime(runtime, observation);

        assert_eq!(
            telemetry.parity_status,
            StudioSceneParityStatus::RuntimeContractMismatch
        );
        assert!(
            telemetry
                .failure_reason
                .as_deref()
                .is_some_and(|reason| reason.contains("horodatages"))
        );
    }
}
