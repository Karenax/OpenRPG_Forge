#![deny(unsafe_code)]
#![cfg_attr(
    not(target_arch = "wasm32"),
    allow(
        linker_messages,
        reason = "MSVC reports its normal import-library creation message for the cdylib"
    )
)]

use forge_core::EntityId;
use forge_project::WeatherState;
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
use forge_project::{
    EnvironmentProfileDocument, PlayerControllerProfileDocument, SceneProofDocument,
    SceneProofKind, TerrainDocument, VegetationDocument, WaterDocument, WeatherProfileDocument,
};
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
use forge_world::{
    TerrainGenerationPlan, VegetationGenerationPlan, embedded_gate0_proof_document,
    embedded_visual_valley_environment_profile, embedded_visual_valley_player_controller_profile,
    embedded_visual_valley_terrain_document, embedded_visual_valley_vegetation_document,
    embedded_visual_valley_water_document, embedded_visual_valley_weather_profile,
};
use serde::{Deserialize, Serialize, de::DeserializeOwned};
pub mod asset_library_model_wire;
#[cfg(not(target_arch = "wasm32"))]
pub mod asset_library_native;
pub mod asset_library_protocol;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod asset_library_ui;
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
mod audit_labels;
#[cfg(not(target_arch = "wasm32"))]
pub mod module_asset_native;
pub mod promotion_preview_gate;
pub mod reconciliation_protocol;

const STUDIO_SCENE_QUERY_KEY: &str = "forge_scene";
const STUDIO_WEATHER_QUERY_KEY: &str = "forge_weather";

#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
mod input_key_observation;

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum StudioScene {
    #[default]
    Gate0,
    VisualValley,
    WorldSketchVisualValley,
}

impl StudioScene {
    #[must_use]
    pub const fn mode(self) -> StudioViewportMode {
        match self {
            Self::Gate0 => StudioViewportMode::Gate0SceneProof,
            Self::VisualValley | Self::WorldSketchVisualValley => {
                StudioViewportMode::Gate1VisualValley
            }
        }
    }

    #[must_use]
    pub const fn query_value(self) -> &'static str {
        match self {
            Self::Gate0 => "gate0",
            Self::VisualValley => "visual-valley",
            Self::WorldSketchVisualValley => "world-sketch-visual-valley",
        }
    }

    #[must_use]
    pub const fn is_visual_valley(self) -> bool {
        !matches!(self, Self::Gate0)
    }

    /// Reload the same local page with only the selected scene, clearing transient proof modes.
    #[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
    fn preview_href(self) -> String {
        format!("?{STUDIO_SCENE_QUERY_KEY}={}", self.query_value())
    }

    #[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
    const fn valley_display_name(self) -> &'static str {
        match self {
            Self::WorldSketchVisualValley => "Visual Valley · WorldSketch verrouillé",
            Self::Gate0 | Self::VisualValley => "Visual Valley",
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
        Some("world-sketch-visual-valley") => StudioSceneSelection {
            scene: StudioScene::WorldSketchVisualValley,
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
        [_] if !scene.is_visual_valley() => {
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
    pub gpu_presentation: StudioGpuPresentationStatus,
    pub evidence_source: String,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct StudioSceneTelemetry {
    pub schema: String,
    pub schema_version: u32,
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
            schema_version: 3,
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
    if !observation.first_real_frame_ms.is_finite()
        || !observation.runtime_ready_ms.is_finite()
        || observation.first_real_frame_ms < 0.0
        || observation.runtime_ready_ms < observation.first_real_frame_ms
    {
        return Err("horodatages runtime non finis, négatifs ou désordonnés".to_owned());
    }
    if selected.mode != runtime.mode
        || selected.scene != runtime.scene
        || selected.display_name != runtime.display_name
        || selected.camera_owner != runtime.camera_owner
        || selected.expected_active_camera_count != runtime.expected_active_camera_count
        || selected.water_adapter != runtime.water_adapter
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
    if selected.scene.is_visual_valley() {
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
#[derive(Clone, Debug)]
enum LoadedStudioScene {
    Gate0(SceneProofDocument),
    VisualValley(Box<LoadedVisualValley>),
}

#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
#[derive(Clone, Debug)]
struct LoadedVisualValley {
    scene: StudioScene,
    terrain: TerrainDocument,
    vegetation: VegetationDocument,
    environment: EnvironmentProfileDocument,
    player_controller: PlayerControllerProfileDocument,
    water: WaterDocument,
    weather: WeatherProfileDocument,
    bridge: Option<forge_world::WorldSketchBridgePlacementDocument>,
    player_spawn: Option<[f32; 3]>,
    road_surface: Option<forge_world::RoadSurfaceProjection>,
}

#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
impl LoadedStudioScene {
    fn load(scene: StudioScene) -> Result<Self, String> {
        match scene {
            StudioScene::Gate0 => embedded_gate0_proof_document()
                .map(Self::Gate0)
                .map_err(|error| format!("invalid embedded Gate 0 scene: {error}")),
            StudioScene::VisualValley | StudioScene::WorldSketchVisualValley => {
                let mut terrain = embedded_visual_valley_terrain_document()
                    .map_err(|error| format!("invalid embedded Visual Valley terrain: {error}"))?;
                let mut vegetation =
                    embedded_visual_valley_vegetation_document().map_err(|error| {
                        format!("invalid embedded Visual Valley vegetation: {error}")
                    })?;
                let mut environment =
                    embedded_visual_valley_environment_profile().map_err(|error| {
                        format!("invalid embedded Visual Valley environment: {error}")
                    })?;
                let player_controller = embedded_visual_valley_player_controller_profile()
                    .map_err(|error| {
                        format!("invalid embedded Visual Valley player controller: {error}")
                    })?;
                let mut water = embedded_visual_valley_water_document()
                    .map_err(|error| format!("invalid embedded Visual Valley water: {error}"))?;
                let mut weather = embedded_visual_valley_weather_profile()
                    .map_err(|error| format!("invalid embedded Visual Valley weather: {error}"))?;
                let mut bridge = None;
                let mut player_spawn = None;
                let mut road_surface = None;

                if scene == StudioScene::WorldSketchVisualValley {
                    let sketch = forge_world::embedded_visual_valley_world_sketch()
                        .map_err(|error| format!("invalid locked WorldSketch: {error}"))?;
                    let compiled = forge_world::compile_world_sketch(&sketch)
                        .map_err(|error| format!("cannot compile locked WorldSketch: {error}"))?;
                    let rivers: Vec<_> = compiled
                        .spline_carving
                        .splines
                        .iter()
                        .filter(|spline| spline.kind == forge_world::WorldSketchSplineKind::River)
                        .collect();
                    if rivers.len() != 1 {
                        return Err(
                            "WorldSketch runtime requires exactly one primary river".to_owned()
                        );
                    }
                    // Derive revision-bound copies; retain the project/area checks below.
                    environment.revision = sketch.revision;
                    water.revision = sketch.revision;
                    weather.revision = sketch.revision;
                    water = forge_world::water_from_carved_spline(water, rivers[0])
                        .map_err(|error| format!("invalid carved river water: {error}"))?;
                    player_spawn = Some(
                        forge_world::world_sketch_player_spawn(
                            &sketch,
                            &compiled.carved_terrain,
                            &player_controller,
                        )
                        .map_err(|error| format!("invalid WorldSketch player spawn: {error}"))?,
                    );
                    road_surface = Some(
                        forge_world::RoadSurfaceProjection::from_compiled(&compiled)
                            .map_err(|error| format!("invalid compiled road surface: {error}"))?,
                    );
                    terrain = compiled.carved_terrain;
                    vegetation = compiled.vegetation;
                    if !compiled.bridge_placements.placements.is_empty() {
                        bridge = Some(compiled.bridge_placements);
                    }
                }

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

                Ok(Self::VisualValley(Box::new(LoadedVisualValley {
                    scene,
                    terrain,
                    vegetation,
                    environment,
                    player_controller,
                    water,
                    weather,
                    bridge,
                    player_spawn,
                    road_surface,
                })))
            }
        }
    }

    fn manifest(
        &self,
        selected_weather_state: Option<WeatherState>,
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
            }),
            Self::VisualValley(loaded) => {
                let LoadedVisualValley {
                    scene,
                    terrain,
                    vegetation,
                    environment,
                    player_controller,
                    water,
                    weather,
                    bridge: _,
                    player_spawn: _,
                    road_surface,
                } = loaded.as_ref();
                if let Some(surface) = road_surface {
                    surface
                        .validate_terrain(terrain)
                        .map_err(|error| format!("invalid road surface: {error}"))?;
                }
                Ok(StudioSceneManifest {
                    mode: StudioViewportMode::Gate1VisualValley,
                    scene: *scene,
                    display_name: scene.valley_display_name().to_owned(),
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
                    },
                    camera_owner: StudioCameraOwner::VisualValleyPlayerController,
                    expected_active_camera_count: 1,
                    water_adapter: StudioWaterAdapterStatus::InstalledEngineAdapter,
                    weather_state: selected_weather_state,
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

#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
mod library_resume;

#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_building_projection;
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
mod authoring_building_view;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_buildings;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_controls;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_density;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_density_inspector;
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
mod authoring_density_review;
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
mod authoring_draft_scope;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_exclusions;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_gameplay;
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
mod authoring_imported_quality_observation;
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
mod authoring_index_page;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_lighting;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_path_guides;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_path_projection;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_paths;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_physics;
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
mod authoring_physics_request;
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
mod authoring_placement_reuse;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_playtest;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_playtest_controls;
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
mod authoring_playtest_sources;
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
mod authoring_projection_diagnostics;
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
mod authoring_regeneration_review;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_render;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_rivers;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_scene;
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
mod authoring_scene_index;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_species;
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
mod authoring_start_label;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_start_pick;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_vegetation;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_vegetation_recipe;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_viewport;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_zone;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod workspace_controls;

#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_immersion;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_input;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_temporal;

#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_brush;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_brush_pointer;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_materials;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_preview;

#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_camera;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_compass;

#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_history;

#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
mod editor_shortcut;

#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_shortcuts;

#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_assets;

#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_raster;

#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_imported_population;

#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_imported_plants;

#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_imported_terrain;

#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_imported_shadows;

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "command", rename_all = "snake_case", deny_unknown_fields)]
pub enum StudioCommand {
    ReadVegetationSource {
        expected_head: forge_core::ContentHash,
        block_index: usize,
        offset: usize,
    },
    ReadTerrainSource {
        expected_document: forge_core::ContentHash,
        offset: usize,
    },
    Atelier {
        request: Box<atelier_protocol::Request>,
    },
    /// Explicit response to the native-window close confirmation, not a zone edit.
    CloseWindow {
        discard_unsaved: bool,
    },
    Reconciliation {
        query: Box<reconciliation_protocol::Query>,
    },
    Workspace {
        request: Box<forge_project::authoring::workspace::WorkspaceHostRequest>,
    },
    Authoring {
        request: Box<forge_project::authoring::AuthoringRequest>,
    },
    GameSave {
        request: Box<forge_project::authoring::game_save::GameSaveRequest>,
    },
    SelectEntity {
        entity_id: EntityId,
    },
    SceneSummary,
}

#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_close;

pub mod atelier_drafts;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod atelier_inspector;
pub mod atelier_protocol;
pub mod atelier_transform;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod atelier_ui;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod authoring_dock;
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
mod authoring_feedback;
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
mod panel_layout;

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "response", rename_all = "snake_case", deny_unknown_fields)]
pub enum StudioResponse {
    /// The native shell accepted an explicit close confirmation and destroyed
    /// the window. No authoring state is implied when the host was unavailable.
    WindowClosed,
    VegetationSource {
        chunk: forge_project::authoring::vegetation_source::VegetationSourceChunk,
    },
    TerrainSource {
        chunk: forge_project::authoring::terrain_source::TerrainSourceChunk,
    },
    Atelier {
        state: Box<atelier_protocol::Reply>,
    },
    Reconciliation {
        page: Box<reconciliation_protocol::Page>,
    },
    Workspace {
        state: Box<forge_project::authoring::workspace::WorkspaceHostState>,
    },
    Authoring {
        state: Box<forge_project::authoring::AuthoringState>,
    },
    GameSave {
        state: Box<forge_project::authoring::game_save::GameSaveResponse>,
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

/// Keep every nested integer out of JavaScript's lossy `Number` representation.
///
/// Tauri transports this JSON as one string. Documents retain their canonical JSON
/// representation, including numeric `u64` fields in archives.
pub fn encode_tauri_wire<T: Serialize>(value: &T) -> Result<String, String> {
    serde_json::to_string(value)
        .map_err(|error| format!("Payload Tauri non sérialisable : {error}"))
}

/// Decode the lossless JSON string carried by the Tauri command boundary.
pub fn decode_tauri_wire<T: DeserializeOwned>(payload: &str) -> Result<T, String> {
    serde_json::from_str(payload).map_err(|error| format!("Payload Tauri invalide : {error}"))
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
        StudioResponse::WindowClosed
        | StudioResponse::SceneSummary { .. }
        | StudioResponse::TerrainSource { .. }
        | StudioResponse::VegetationSource { .. }
        | StudioResponse::Atelier { .. }
        | StudioResponse::Reconciliation { .. }
        | StudioResponse::Authoring { .. }
        | StudioResponse::GameSave { .. }
        | StudioResponse::Workspace { .. } => {
            Err("Réponse Tauri inattendue : résumé reçu pour une sélection".to_owned())
        }
    }
}

#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod bridge_probe;
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
mod cloud_preview;
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
mod effective_quality_status;
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
mod fixed_proof_view;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod governed_kit;
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
mod governed_kit_request;
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
mod gpu_memory_preview;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod input_capture;
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
mod proof_export;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod quality_motion_probe;
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
mod quality_motion_trace;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod quality_preview;
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
mod render_warmup;
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
mod shadow_candidate;
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
mod water_optics_request;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod water_preview;
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
mod weather_input;

#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod web {
    use super::quality_preview::QualityControls;
    use super::telemetry::{
        AllowedCommandProbeStatus, BrowserMemoryObservation, DeniedCommandProbeStatus,
        FRAME_MEASURED_SAMPLES, FRAME_WARMUP_SAMPLES, FrameDistribution, Gate0ProbeMode,
        Gate0StudioLedger, RuntimeTelemetryEvent, StudioTelemetryTarget, TAURI_DENIED_COMMAND,
        ViewportFailureEvent, ViewportObservation, ViewportReadyEvent, WebGpuProbeStatus,
        is_explicit_acl_denial, parse_gate0_probe,
    };
    use super::{
        EntityId, LoadedStudioScene, StudioCommand, StudioGpuPresentationStatus, StudioResponse,
        StudioScene, StudioSceneManifest, StudioSceneParityStatus, StudioSceneRuntimeObservation,
        StudioSceneSourceHashes, StudioSceneTelemetry, StudioViewportMode,
        StudioWaterAdapterStatus, is_validated_gate0_runtime, parse_studio_scene_selection,
        parse_studio_weather_selection, validate_selection_response,
    };
    use bevy::{
        prelude::*,
        render::{
            RenderPlugin,
            settings::{RenderCreation, WgpuSettings},
        },
        window::{PresentMode, WindowResolution},
    };
    use forge_engine::{
        EnvironmentProjectionStats, EnvironmentProjectionStatus, PlayerInputFocus,
        PlayerRuntimeSnapshot, ProofScene, ProofScenePlugin, ProofSelection,
        VegetationProjectionStats, VisualValleyEnvironmentPlugin, VisualValleyPlugin,
        VisualValleyStats, VisualValleyVegetationPlugin, VisualValleyWaterPlugin,
        VisualValleyWeatherPlugin, WaterProjectionStats, WaterRuntimeObservationStatus,
        WeatherProjectionStats, WeatherProjectionStatus, WeatherRuntimeState,
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
        static WEATHER_PREVIEW_REQUEST: RefCell<Option<super::WeatherState>> = const { RefCell::new(None) };
        static WEATHER_PREVIEW_OUTPUT: RefCell<Option<WriteSignal<String>>> = const { RefCell::new(None) };
        static WEATHER_PREVIEW_LEDGER: RefCell<Option<WriteSignal<String>>> = const { RefCell::new(None) };
        static BRIDGE_PROJECTION_LEDGER: RefCell<Option<WriteSignal<String>>> = const { RefCell::new(None) };
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
        if super::asset_library_ui::requested() {
            mount_to_body(|| view! { <super::asset_library_ui::LibraryWorkspace/> });
        } else if super::atelier_ui::requested() {
            mount_to_body(|| view! { <super::atelier_ui::SecondaryWorkspace/> });
        } else if super::authoring_viewport::requested() {
            mount_to_body(|| view! { <super::authoring_viewport::AuthoringWorkspace/> });
        } else {
            mount_to_body(|| view! { <Studio/> });
        }
    }

    #[component]
    fn Studio() -> impl IntoView {
        let probe_mode = current_probe_mode();
        let scene_selection = current_scene_selection();
        let selected_scene = scene_selection.scene;
        let weather_selection = current_weather_selection(selected_scene);
        let weather_configuration_error = weather_selection.as_ref().err().map(ToString::to_string);
        let selected_weather_state = weather_selection
            .as_ref()
            .ok()
            .filter(|_| selected_scene.is_visual_valley())
            .map(|selection| selection.state);
        let loaded_scene = LoadedStudioScene::load(selected_scene)
            .expect("embedded Studio scene documents must remain valid and compatible");
        let scene_manifest = loaded_scene
            .manifest(selected_weather_state)
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
            (_, Some(super::WeatherState::Clear)) => "clair · adapter installé".to_owned(),
            (_, Some(super::WeatherState::Rain)) => "pluie · adapter installé".to_owned(),
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
        let (scene_telemetry, set_scene_telemetry) = signal(initial_scene_telemetry);
        let (weather_preview, set_weather_preview) = signal("Runtime non observé".to_owned());
        let (weather_preview_ledger, set_weather_preview_ledger) = signal(String::new());
        let (weather_input_status, set_weather_input_status) =
            signal("Profil météo en attente de vérification".to_owned());
        let (bridge_ledger, set_bridge_ledger) = signal(String::new());
        BRIDGE_PROJECTION_LEDGER.with(|output| *output.borrow_mut() = Some(set_bridge_ledger));
        WEATHER_PREVIEW_OUTPUT.with(|output| output.replace(Some(set_weather_preview)));
        WEATHER_PREVIEW_LEDGER.with(|output| output.replace(Some(set_weather_preview_ledger)));

        install_viewport_selection_listener(
            set_selected,
            set_rerenders,
            set_ipc_status,
            set_telemetry,
        );
        install_runtime_telemetry_listener(set_telemetry, set_scene_telemetry, set_webgpu_status);

        if probe_mode == Gate0ProbeMode::TauriPermission {
            wasm_bindgen_futures::spawn_local(run_tauri_permission_probe(set_telemetry));
        }

        let launch_configuration_error = weather_configuration_error.clone();
        wasm_bindgen_futures::spawn_local(async move {
            if let Some(reason) = launch_configuration_error {
                set_webgpu_status.set(WebGpuStatus::ConfigurationError(reason));
                return;
            }
            let mut loaded_scene = loaded_scene;
            if let Err(reason) = super::shadow_candidate::from_location(selected_scene) {
                LeptosUpdate::update(&set_scene_telemetry, |ledger| {
                    ledger.fail_configuration(reason.clone());
                });
                set_webgpu_status.set(WebGpuStatus::ConfigurationError(reason));
                return;
            }
            if let Err(reason) =
                super::water_optics_request::load(&mut loaded_scene, selected_scene)
            {
                LeptosUpdate::update(&set_scene_telemetry, |ledger| {
                    ledger.fail_configuration(reason.clone());
                });
                set_webgpu_status.set(WebGpuStatus::ConfigurationError(reason));
                return;
            }
            match super::weather_input::load(&mut loaded_scene, selected_scene).await {
                Ok(status) => set_weather_input_status.set(status),
                Err(reason) => {
                    set_weather_input_status.set(reason.clone());
                    LeptosUpdate::update(&set_scene_telemetry, |ledger| {
                        ledger.fail_configuration(reason.clone());
                    });
                    set_webgpu_status.set(WebGpuStatus::ConfigurationError(reason));
                    return;
                }
            }
            let manifest = match loaded_scene.manifest(selected_weather_state) {
                Ok(manifest) => manifest,
                Err(reason) => {
                    set_webgpu_status.set(WebGpuStatus::ConfigurationError(reason));
                    return;
                }
            };
            LeptosUpdate::update(&set_scene_telemetry, |ledger| {
                ledger.selected_manifest = manifest;
            });
            match preflight_webgpu(probe_mode).await {
                Ok(()) => {
                    let probe_configuration =
                        super::bridge_probe::requested().and_then(|requested| {
                            if requested && selected_scene != StudioScene::WorldSketchVisualValley {
                                Err("Le parcours requiert la vallée WorldSketch".to_owned())
                            } else {
                                Ok(())
                            }
                        });
                    if let Err(reason) = probe_configuration {
                        set_webgpu_status.set(WebGpuStatus::ConfigurationError(reason));
                        return;
                    }
                    let kit = match super::governed_kit::load(&loaded_scene).await {
                        Ok(kit) => kit,
                        Err(reason) => {
                            super::governed_kit::report_failure(&reason);
                            set_webgpu_status.set(WebGpuStatus::ConfigurationError(reason));
                            return;
                        }
                    };
                    let fixed_view = match super::fixed_proof_view::prepare(selected_scene).await {
                        Ok(view) => view,
                        Err(reason) => {
                            set_webgpu_status.set(WebGpuStatus::ConfigurationError(reason));
                            return;
                        }
                    };
                    start_viewport_once(
                        selected_scene,
                        selected_weather_state.unwrap_or(super::WeatherState::Clear),
                        loaded_scene,
                        kit,
                        fixed_view,
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
                    <a class="studio-return-editor" href="?authoring=1"
                        title="Revenir au Studio ; rouvrir ensuite la zone sauvegardée">
                        "← Retour à l’éditeur"
                    </a>
                    <super::authoring_controls::AuthoringControls/>
                    <details class="scene-navigation">
                        <summary>"Scènes de preuve"</summary>
                        <nav aria-label="Choisir une scène de preuve">
                            <p>"Ouvrir une scène recharge la vue et réinitialise les aperçus. Aucun projet n’est modifié."</p>
                            {[
                                (StudioScene::WorldSketchVisualValley, "Vallée WorldSketch"),
                                (StudioScene::VisualValley, "Vallée de référence"),
                                (StudioScene::Gate0, "Scène technique Gate 0"),
                            ].into_iter().map(move |(scene, label)| view! {
                                <a href=scene.preview_href()
                                    aria-current=if scene == selected_scene { Some("page") } else { None }
                                >{label}</a>
                            }).collect_view()}
                            <a href=super::governed_kit_request::FOREST_PREVIEW_HREF>
                                "Vallée avec kit et forêt · candidat expérimental"
                            </a>
                            <p>"Ce candidat requiert le kit local déployé. Les fichiers et leurs hashes sont contrôlés avant chargement ; aucun asset n’est promu ni modifié."</p>
                            <a href=super::cloud_preview::PREVIEW_HREF>"Vallée avec ciel expérimental"</a>
                            <a href=super::cloud_preview::INTEGRATED_PREVIEW_HREF>"Candidat complet · kit, forêt, eau et ciel"</a>
                            <p>"Le candidat complet requiert le kit local. Il réunit les aperçus expérimentaux sans valider leur qualité ni sauvegarder le projet."</p>
                            <a href=super::water_optics_request::PREVIEW_HREF>"Vallée avec eau transmissive · candidat expérimental"</a>
                            <a href=super::water_optics_request::MEDIUM_HREF>"Vallée avec eau claire RGB · candidat expérimental"</a>
                            <a href=super::water_optics_request::REFLECTION_HREF>"Vallée avec eau claire et reflets du décor · candidat expérimental"</a>
                            <a href=super::shadow_candidate::HREF>"Vallée avec ombres équilibrées · candidat expérimental"</a>
                            <output id="studio-water-optics-status">"Candidat optique non demandé"</output>
                            <output id="studio-cloud-preview-status">"Ciel expérimental non demandé"</output>
                            <pre id="studio-cloud-preview-ledger" hidden></pre>
                        </nav>
                    </details>
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
                            <span>"Perspective"</span>
                            <span id="studio-viewport-resolution">{move || telemetry.get().viewport.resolution_summary()}</span>
                        </div>
                        { (selected_scene.is_visual_valley() && !super::bridge_probe::requested().unwrap_or(false) && !super::fixed_proof_view::is_requested()).then(|| view! {
                            <div class="viewport-controls" id="studio-explore-help">
                                <super::input_capture::InputCaptureStatus/>
                                <br/>
                                "Cliquer dans la vue pour explorer · ZQSD/WASD · Maj : courir · Espace : sauter · V : caméra · Échap : libérer"
                            </div>
                        }) }
                        { super::fixed_proof_view::is_requested().then(|| view! {
                            <div class="viewport-controls"><super::fixed_proof_view::Status/></div>
                        }) }
                        { super::bridge_probe::requested().unwrap_or(false).then(|| view! {
                            <div class="viewport-controls">"Parcours de qualification automatique · commandes manuelles désactivées · recharger sans forge_probe pour explorer"</div>
                        }) }
                        <canvas id="forge-canvas" data-testid="forge-webgpu-canvas" tabindex="0"
                            on:keydown=move |event| {
                                if selected_scene.is_visual_valley() {
                                    super::input_capture::observe_key(&event.code());
                                }
                                if selected_scene.is_visual_valley()
                                    && let Some(document) = web_sys::window().and_then(|window| window.document())
                                    && document.pointer_lock_element().is_some()
                                {
                                    if event.key() == "Escape" {
                                        document.exit_pointer_lock();
                                    }
                                    if event.code() == "Space" {
                                        event.prevent_default();
                                    }
                                }
                            }
                            on:click=move |_| {
                                if selected_scene.is_visual_valley()
                                    && !super::fixed_proof_view::is_requested()
                                    && let Some(canvas) = current_canvas()
                                {
                                    super::input_capture::begin_request();
                                    canvas.request_pointer_lock();
                                }
                            }></canvas>
                        {selected_scene.is_visual_valley().then(|| view! { <super::render_warmup::Loading/> })}
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
                                    <strong>"Configuration de démarrage invalide."</strong>
                                    <span>{reason}</span>
                                    <span>"Vérifiez les paramètres de scène/météo et les fichiers du kit local, puis rechargez le Studio."</span>
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
                        <label>"Météo au démarrage"<output>{weather_status_label}</output></label>
                        {selected_scene.is_visual_valley().then(|| view! { <QualityControls/> })}
                        <super::proof_export::ProofExportControls/>
                        {(selected_scene == StudioScene::WorldSketchVisualValley).then(|| view! { <super::governed_kit::KitStatus/> })}
                        {(selected_scene == StudioScene::WorldSketchVisualValley).then(|| view! { <super::bridge_probe::ProbeStatus/> })}
                        {selected_scene.is_visual_valley().then(|| view! {
                            <div class="weather-preview" aria-label="Aperçu météo sans sauvegarde">
                                <p class="hint">"Aperçu météo · ne modifie pas le projet"</p>
                                <output id="studio-weather-input-status">{move || weather_input_status.get()}</output>
                                <button type="button"
                                    disabled=move || scene_telemetry.get().parity_status != StudioSceneParityStatus::SourceManifestMatched
                                    on:click=move |_| { WEATHER_PREVIEW_REQUEST.with(|request| request.replace(Some(super::WeatherState::Clear))); }
                                >"Aperçu clair"</button>
                                <button type="button"
                                    disabled=move || scene_telemetry.get().parity_status != StudioSceneParityStatus::SourceManifestMatched
                                    on:click=move |_| { WEATHER_PREVIEW_REQUEST.with(|request| request.replace(Some(super::WeatherState::Rain))); }
                                >"Aperçu pluie"</button>
                                <output id="studio-weather-preview-status">{move || weather_preview.get()}</output>
                                <pre id="studio-weather-preview-ledger" hidden>{move || weather_preview_ledger.get()}</pre>
                            </div>
                        })}
                        <label>"Parité au démarrage"<output id="studio-scene-parity-summary">{move || scene_parity_summary(&scene_telemetry.get())}</output></label>
                        {(selected_scene == StudioScene::WorldSketchVisualValley).then(|| view! {
                            <label>"Pont dérivé"<output id="studio-bridge-summary">{move || if bridge_ledger.get().is_empty() {
                                "Projection non observée"
                            } else {
                                "Pont et colliders créés · parcours automatique : voir son verdict · revue humaine non vérifiée"
                            }}</output></label>
                            <pre id="studio-bridge-projection-ledger" hidden>{move || bridge_ledger.get()}</pre>
                        })}
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
                    "hashes alignés · caméras {}/{} (attendu {}) · eau {} · météo {} ({})",
                    runtime.active_camera_3d_entities,
                    runtime.camera_3d_entities,
                    telemetry.selected_manifest.expected_active_camera_count,
                    runtime.water_observation.as_deref().unwrap_or("n/a"),
                    runtime.weather_observation.as_deref().unwrap_or("n/a"),
                    runtime.weather_target_state.as_deref().unwrap_or("n/a")
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

    pub(super) async fn invoke_tauri(command: &StudioCommand) -> Result<StudioResponse, String> {
        let arguments = js_sys::Object::new();
        let payload = super::encode_tauri_wire(command)?;
        js_sys::Reflect::set(
            &arguments,
            &JsValue::from_str("command"),
            &JsValue::from_str(&payload),
        )
        .map_err(|error| format!("Arguments Tauri invalides : {}", js_error(&error)))?;
        let response = invoke_tauri_raw("execute_studio_command", arguments.as_ref()).await?;
        let payload = response
            .as_string()
            .ok_or("Réponse Tauri invalide : payload texte attendu")?;
        super::decode_tauri_wire(&payload)
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

    pub(super) async fn invoke_tauri_raw(
        command: &str,
        arguments: &JsValue,
    ) -> Result<JsValue, String> {
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
        loaded_scene: LoadedStudioScene,
        kit: Option<super::governed_kit::LoadedKit>,
        fixed_view: Option<super::fixed_proof_view::PreparedView>,
    ) {
        if !VIEWPORT_LAUNCH_REQUESTED.swap(true, Ordering::SeqCst) {
            INITIAL_CANVAS.with(|initial| initial.replace(current_canvas()));
            start_viewport(scene, weather_state, loaded_scene, kit, fixed_view);
        }
    }

    fn start_viewport(
        scene: StudioScene,
        weather_state: super::WeatherState,
        loaded_scene: LoadedStudioScene,
        kit: Option<super::governed_kit::LoadedKit>,
        fixed_view: Option<super::fixed_proof_view::PreparedView>,
    ) {
        let scene_manifest = loaded_scene
            .manifest(scene.is_visual_valley().then_some(weather_state))
            .expect("validated embedded Studio scene documents must remain hashable");
        let render_plugin = RenderPlugin {
            render_creation: RenderCreation::Automatic(Box::new(WgpuSettings {
                power_preference: bevy::render::settings::PowerPreference::HighPerformance,
                ..default()
            })),
            ..default()
        };
        let window_title = format!("OpenRPG Forge WebGPU · {}", scene_manifest.display_name);
        let mut app = App::new();
        if let Err(error) = super::water_optics_request::install(&mut app, scene) {
            report_runtime_failure(&mut BrowserFrameCollector::new(scene), 0.0, error);
            return;
        }
        if let Some(kit) = &kit {
            kit.register_source(&mut app);
        }
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
                            resolution: WindowResolution::new(1280, 720),
                            present_mode: PresentMode::AutoVsync,
                            ..default()
                        }),
                        ..default()
                    })
                    .set(render_plugin),
            );

        app.add_plugins(forge_engine::source_fragment::SourceFragmentPlugin);
        if let Some(kit) = kit {
            kit.install(&mut app);
        }

        if let Err(error) = super::cloud_preview::install(&mut app, scene.is_visual_valley()) {
            report_runtime_failure(&mut BrowserFrameCollector::new(scene), 0.0, error);
            return;
        }
        if scene.is_visual_valley() {
            super::render_warmup::install(&mut app);
        }
        super::fixed_proof_view::install(&mut app, fixed_view);
        match loaded_scene {
            LoadedStudioScene::Gate0(document) => {
                app.add_plugins(ProofScenePlugin::new(document))
                    .add_systems(Update, publish_viewport_selection);
            }
            LoadedStudioScene::VisualValley(loaded) => {
                if let Err(error) = install_visual_valley_plugins(&mut app, *loaded, weather_state)
                {
                    report_runtime_failure(&mut BrowserFrameCollector::new(scene), 0.0, error);
                    return;
                }
            }
        }
        super::proof_export::install(&mut app);
        app.add_systems(Update, collect_browser_frame_telemetry)
            .run();
    }

    fn install_visual_valley_plugins(
        app: &mut App,
        loaded: super::LoadedVisualValley,
        weather_state: super::WeatherState,
    ) -> Result<(), String> {
        let super::LoadedVisualValley {
            scene: _,
            terrain,
            vegetation,
            environment,
            player_controller,
            water,
            weather,
            bridge,
            player_spawn,
            road_surface,
        } = loaded;
        super::quality_preview::install(app, &terrain, &vegetation)?;
        let probe = if super::bridge_probe::requested()? {
            Some(
                forge_engine::BridgeCrossingProbePlugin::new(
                    &terrain,
                    bridge.as_ref().ok_or("Pont absent pour le parcours")?,
                    &player_controller,
                )?
                .in_viewport()
                .with_temporal_lod_probe(super::bridge_probe::temporal_requested()?),
            )
        } else {
            None
        };
        if let Some(bridge) = bridge {
            app.insert_resource(StudioBridgeSource(bridge.clone()));
            // The private loaded scene holds the compiler's validated, terrain-bound result.
            app.add_plugins(
                forge_engine::WorldSketchBridgePlugin::new(&terrain, bridge)
                    .expect("compiled Studio bridge matches its carved terrain"),
            );
            app.add_systems(Last, publish_bridge_projection);
        }
        let mut water_plugin = VisualValleyWaterPlugin::new(terrain.clone(), water);
        let weather_plugin =
            VisualValleyWeatherPlugin::new(weather).with_initial_state(weather_state);
        let mut visual_valley = VisualValleyPlugin::new(terrain.clone())
            .with_player_controller(player_controller)
            .with_player_interactive_input(
                probe.is_none()
                    && !app
                        .world()
                        .contains_resource::<forge_engine::FixedProofViewStatus>(),
            );
        if let Some(surface) = road_surface {
            water_plugin = water_plugin
                .with_road_surface(surface.clone())
                .map_err(|error| format!("invalid bank road surface: {error}"))?;
            visual_valley = visual_valley
                .with_road_surface(surface)
                .map_err(|error| format!("invalid road surface: {error}"))?;
        }
        if let Some(probe) = probe {
            let probe = if super::quality_motion_probe::install(app)? {
                probe.after_profile_warmup()
            } else {
                probe
            };
            visual_valley = visual_valley
                .with_player_spawn_position(probe.spawn_position())
                .with_player_camera_target(probe.camera_target());
            app.add_plugins(probe)
                .add_systems(Last, super::bridge_probe::publish);
        } else if let Some(position) = player_spawn {
            // WorldSketch zones are centred at the origin; do not edit the source landmark.
            visual_valley = visual_valley
                .with_player_spawn_position(Vec3::from_array(position))
                .with_player_camera_target(Vec3::ZERO);
        }
        app.insert_resource(PlayerInputFocus(false))
            .add_plugins(visual_valley)
            .add_plugins(VisualValleyEnvironmentPlugin::new(environment))
            .add_plugins(VisualValleyVegetationPlugin::new(terrain, vegetation))
            .add_plugins(water_plugin)
            .add_plugins(weather_plugin)
            .add_systems(PreUpdate, apply_weather_preview_request)
            .add_systems(Last, publish_weather_preview)
            .add_systems(
                PreUpdate,
                update_viewport_input_focus.before(bevy::input::InputSystems),
            );
        Ok(())
    }

    fn apply_weather_preview_request(mut state: ResMut<WeatherRuntimeState>) {
        if let Some(target) = WEATHER_PREVIEW_REQUEST.with(|request| request.borrow_mut().take()) {
            state.request_state(target);
        }
    }

    #[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
    fn publish_bridge_projection(
        stats: Res<forge_engine::WorldSketchBridgeStats>,
        source: Res<StudioBridgeSource>,
        mut published: Local<bool>,
    ) {
        if *published || stats.deck_colliders == 0 {
            return;
        }
        let ledger = serde_json::json!({
            "schema": "forge.studio-bridge-projection",
            "schema_version": 1,
            "recorded_at_utc": recorded_at_utc(),
            "evidence_source": "engine_cpu_spawn_counts_not_browser_crossing",
            "browser_crossing_verified": false,
            "projection": &*stats,
            "compiled_document": &source.0,
        })
        .to_string();
        BRIDGE_PROJECTION_LEDGER.with(|output| {
            if let Some(output) = *output.borrow() {
                output.set(ledger);
            }
        });
        *published = true;
    }

    use bevy::ecs as bevy_ecs;

    #[derive(Resource)]
    struct StudioBridgeSource(forge_world::WorldSketchBridgePlacementDocument);

    #[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
    fn publish_weather_preview(
        stats: Res<WeatherProjectionStats>,
        environment: Option<Res<forge_engine::EnvironmentProjectionStats>>,
        time: Res<Time<Real>>,
        mut last_observation_seconds: Local<f64>,
    ) {
        let now = time.elapsed_secs_f64();
        if now - *last_observation_seconds < 0.25 {
            return;
        }
        *last_observation_seconds = now;
        let sunlight = environment
            .as_ref()
            .map(|environment| &environment.sun.weather_response);
        let sun_label = environment
            .as_ref()
            .and_then(|environment| environment.sun.illuminance_lux)
            .map_or_else(
                || "soleil non observé".to_owned(),
                |lux| format!("soleil {lux:.0} lx"),
            );
        let summary = format!(
            "{} · humidité {:.2} · pluie {} · {} · observation CPU",
            weather_state_label(stats.target_state),
            stats.wetness.current,
            if stats.rain.visible {
                "visible"
            } else {
                "masquée"
            },
            sun_label,
        );
        WEATHER_PREVIEW_OUTPUT.with(|output| {
            if let Some(output) = *output.borrow() {
                output.set(summary);
            }
        });
        let ledger = serde_json::json!({
            "schema": "forge.studio-weather-preview",
            "schema_version": 1,
            "recorded_at_utc": recorded_at_utc(),
            "preview_only": true,
            "projection": &*stats,
            "sunlight": sunlight,
        })
        .to_string();
        WEATHER_PREVIEW_LEDGER.with(|output| {
            if let Some(output) = *output.borrow() {
                output.set(ledger);
            }
        });
    }

    #[allow(clippy::needless_pass_by_value, reason = "Bevy system parameter")]
    fn update_viewport_input_focus(
        mut focus: ResMut<PlayerInputFocus>,
        kit: Option<Res<forge_engine::GovernedAssetKitReadiness>>,
    ) {
        focus.0 = web_sys::window()
            .and_then(|window| window.document())
            .is_some_and(|document| {
                document.has_focus().unwrap_or(false)
                    && document
                        .pointer_lock_element()
                        .is_some_and(|element| element.id() == "forge-canvas")
            });
        focus.0 &= kit.is_none_or(|kit| kit.ready);
        let focused = web_sys::window()
            .and_then(|window| window.document())
            .is_some_and(|document| document.has_focus().unwrap_or(false));
        super::input_capture::observe(focus.0, focused);
    }

    struct BrowserFrameCollector {
        scene: StudioScene,
        projection_wait_started_ms: Option<f64>,
        first_real_frame_ms: Option<f64>,
        ready_reported: bool,
        warmup_samples: usize,
        samples_ms: Vec<f64>,
        metrics_reported: bool,
    }

    impl BrowserFrameCollector {
        fn new(scene: StudioScene) -> Self {
            Self {
                scene,
                projection_wait_started_ms: None,
                first_real_frame_ms: None,
                ready_reported: false,
                warmup_samples: 0,
                samples_ms: Vec::with_capacity(FRAME_MEASURED_SAMPLES),
                metrics_reported: false,
            }
        }
    }

    struct ObservedRuntimeScene {
        manifest: StudioSceneManifest,
        water_observation: Option<String>,
        weather_observation: Option<String>,
        weather_target_state: Option<String>,
    }

    enum RuntimeSceneProgress {
        Pending(String),
        Ready(ObservedRuntimeScene),
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
                RuntimeSceneProgress::Ready(ObservedRuntimeScene {
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
                    },
                    water_observation: None,
                    weather_observation: None,
                    weather_target_state: None,
                })
            }
            StudioScene::VisualValley | StudioScene::WorldSketchVisualValley => {
                observe_visual_valley_runtime(resources, scene)
            }
        }
    }

    fn observe_visual_valley_runtime(
        resources: &RuntimeSceneResources<'_>,
        scene: StudioScene,
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
        ) = (
            resources.terrain,
            resources.vegetation,
            resources.environment,
            resources.player,
            resources.water,
            resources.weather,
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

        RuntimeSceneProgress::Ready(ObservedRuntimeScene {
            manifest: StudioSceneManifest {
                mode: StudioViewportMode::Gate1VisualValley,
                scene,
                display_name: scene.valley_display_name().to_owned(),
                source_hashes: StudioSceneSourceHashes {
                    scene_proof: None,
                    terrain: Some(terrain.source_hash.clone()),
                    vegetation: Some(vegetation.vegetation_document_hash.clone()),
                    environment: Some(environment.source_hash.clone()),
                    player_controller: Some(player_hash.to_string()),
                    water: Some(water.source_hash.clone()),
                    weather: Some(weather.source_hash.clone()),
                },
                camera_owner: super::StudioCameraOwner::VisualValleyPlayerController,
                expected_active_camera_count: 1,
                water_adapter: StudioWaterAdapterStatus::InstalledEngineAdapter,
                weather_state: Some(weather.target_state),
            },
            water_observation: Some(water_observation_label(water.runtime_observation).to_owned()),
            weather_observation: Some(weather_observation_label(weather.status).to_owned()),
            weather_target_state: Some(weather_state_label(weather.target_state).to_owned()),
        })
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
        VIEWPORT_STARTS.fetch_add(1, Ordering::SeqCst);
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
                camera_3d_entities,
                active_camera_3d_entities,
            };
            if !collect_runtime_readiness(&mut collector, &resources) {
                return;
            }
        }
        if collector.warmup_samples < FRAME_WARMUP_SAMPLES {
            collector.warmup_samples += 1;
            return;
        }
        collector.samples_ms.push(delta_ms);
        if collector.samples_ms.len() < FRAME_MEASURED_SAMPLES {
            return;
        }

        let Some(frame) =
            FrameDistribution::from_samples(collector.warmup_samples, &collector.samples_ms)
        else {
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
            render_width_px: canvas
                .as_ref()
                .and_then(|element| element.get_attribute("width"))
                .and_then(|value| value.parse().ok()),
            render_height_px: canvas
                .as_ref()
                .and_then(|element| element.get_attribute("height"))
                .and_then(|value| value.parse().ok()),
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
    fn boxed_authoring_command_preserves_the_wire_contract() {
        let json = serde_json::json!({"command":"authoring","request":{"action":"inspect"}});
        let command: StudioCommand = serde_json::from_value(json.clone()).unwrap();
        assert_eq!(serde_json::to_value(&command).unwrap(), json);
        let StudioCommand::Authoring { request } = command else {
            panic!("authoring command")
        };
        assert_eq!(
            *request,
            forge_project::authoring::AuthoringRequest::Inspect
        );
    }

    #[test]
    fn scene_navigation_targets_only_explicit_local_scenes() {
        for scene in [
            StudioScene::Gate0,
            StudioScene::VisualValley,
            StudioScene::WorldSketchVisualValley,
        ] {
            let href = scene.preview_href();
            assert!(href.starts_with("?forge_scene="));
            assert!(!href.contains('&'));
            assert!(!href.contains('#'));
            let parsed = parse_studio_scene_selection(&href);
            assert_eq!(parsed.scene, scene);
            assert_eq!(parsed.source, StudioSceneSelectionSource::QueryString);
        }
    }

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
    fn tauri_wire_preserves_u64_beyond_javascript_safe_integer() {
        let water = WaterDocument::from_json(include_str!(
            "../../../examples/visual_valley/water.v1.json"
        ))
        .unwrap();
        assert!(water.seed > 9_007_199_254_740_991);

        let encoded = encode_tauri_wire(&water).unwrap();
        assert!(encoded.contains("5929365240144680497"));
        let decoded: WaterDocument = decode_tauri_wire(&encoded).unwrap();
        assert_eq!(decoded, water);
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
    fn world_sketch_scene_is_explicit_and_uses_locked_compiler_outputs() {
        let selection = parse_studio_scene_selection("?forge_scene=world-sketch-visual-valley");
        assert_eq!(selection.scene, StudioScene::WorldSketchVisualValley);
        assert_eq!(selection.source, StudioSceneSelectionSource::QueryString);
        assert_eq!(selection.scene.query_value(), "world-sketch-visual-valley");
        assert_eq!(parse_studio_scene_selection("").scene, StudioScene::Gate0);
        assert_eq!(
            parse_studio_weather_selection("?forge_weather=rain", selection.scene)
                .unwrap()
                .state,
            WeatherState::Rain
        );
        let sketch = forge_world::embedded_visual_valley_world_sketch().unwrap();
        let source_before = sketch.content_hash().unwrap();
        let compiled = forge_world::compile_world_sketch(&sketch).unwrap();
        let loaded = LoadedStudioScene::load(selection.scene).unwrap();
        let manifest = loaded.manifest(Some(WeatherState::Clear)).unwrap();
        assert_eq!(manifest.scene, selection.scene);
        assert_eq!(
            manifest.source_hashes.terrain,
            Some(compiled.report.carved_terrain_sha256.to_string())
        );
        assert_eq!(
            manifest.source_hashes.vegetation,
            Some(compiled.report.vegetation_sha256.to_string())
        );
        let river = compiled
            .spline_carving
            .splines
            .iter()
            .find(|spline| spline.kind == forge_world::WorldSketchSplineKind::River)
            .unwrap();
        let mut expected_water = forge_world::water_from_carved_spline(
            embedded_visual_valley_water_document().unwrap(),
            river,
        )
        .unwrap();
        expected_water.revision = sketch.revision;
        assert_eq!(
            manifest.source_hashes.water,
            Some(expected_water.content_hash().unwrap().to_string())
        );
        assert_ne!(
            manifest.source_hashes.terrain,
            LoadedStudioScene::load(StudioScene::VisualValley)
                .unwrap()
                .manifest(Some(WeatherState::Clear))
                .unwrap()
                .source_hashes
                .terrain
        );
        assert_eq!(sketch.content_hash().unwrap(), source_before);
        assert_eq!(manifest.source_hashes.populated_count(), 6);
        let encoded = serde_json::to_string(&manifest).unwrap();
        assert_eq!(
            serde_json::from_str::<StudioSceneManifest>(&encoded).unwrap(),
            manifest
        );
    }

    #[test]
    fn visual_valley_manifest_exposes_the_six_canonical_source_hashes() {
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
            Some("b26f8ec68b996f41887bda3eddd9d641e861cc8bbd00645c0de2df6739e51b5c")
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
        assert_eq!(manifest.source_hashes.populated_count(), 6);
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
        assert_eq!(hierarchy.len(), 6);
        assert_eq!(unique_ids.len(), hierarchy.len());
        assert!(hierarchy.iter().all(|entry| !entry.name.is_empty()));
        assert!(hierarchy.iter().all(|entry| !entry.glyph.is_empty()));
    }

    #[test]
    fn world_sketch_studio_keeps_the_compiled_bridge_bound_to_its_terrain() {
        let loaded = LoadedStudioScene::load(StudioScene::WorldSketchVisualValley).unwrap();
        let LoadedStudioScene::VisualValley(loaded) = loaded else {
            panic!("wrong scene");
        };
        let bridge = loaded.bridge.as_ref().unwrap();
        bridge.validate().unwrap();
        assert_eq!(
            bridge.source_carved_terrain_sha256,
            loaded.terrain.content_hash().unwrap()
        );
        let compiled = forge_world::compile_world_sketch(
            &forge_world::embedded_visual_valley_world_sketch().unwrap(),
        )
        .unwrap();
        assert_eq!(bridge, &compiled.bridge_placements);
        assert_eq!(bridge.placements.len(), 1);
        assert_eq!(bridge.placements[0].landmark_id, "bridge_south");
        let spawn = loaded.player_spawn.unwrap();
        assert_eq!(spawn[0].to_bits(), 15.0_f32.to_bits());
        assert_eq!(spawn[2].to_bits(), (-225.0_f32).to_bits());
        assert_eq!(
            spawn.map(f32::to_bits),
            forge_world::world_sketch_player_spawn(
                &forge_world::embedded_visual_valley_world_sketch().unwrap(),
                &loaded.terrain,
                &loaded.player_controller,
            )
            .unwrap()
            .map(f32::to_bits)
        );
        let LoadedStudioScene::VisualValley(legacy) =
            LoadedStudioScene::load(StudioScene::VisualValley).unwrap()
        else {
            panic!("wrong scene");
        };
        assert!(legacy.bridge.is_none());
        assert!(legacy.player_spawn.is_none());
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
            gpu_presentation: StudioGpuPresentationStatus::NotObserved,
            evidence_source: "synthetic_headless_contract_test".to_owned(),
        }
    }

    fn visual_valley_rain_manifest() -> StudioSceneManifest {
        LoadedStudioScene::load(StudioScene::VisualValley)
            .unwrap()
            .manifest(Some(WeatherState::Rain))
            .unwrap()
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
