use crate::{StudioQualitySelection, StudioScene, VisualQualityTier, WeatherState};
use serde::{Deserialize, Serialize};

const STUDIO_GATE1_WEATHER_PROOF_QUERY_KEY: &str = "forge_gate1_weather_proof";

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum StudioGate1WeatherProofSelection {
    #[default]
    Disabled,
    EnabledV1,
}

impl StudioGate1WeatherProofSelection {
    #[must_use]
    pub const fn is_enabled(self) -> bool {
        matches!(self, Self::EnabledV1)
    }
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case")]
pub enum StudioGate1WeatherProofQueryError {
    DuplicateParameter { requested_values: Vec<String> },
    UnsupportedValue { requested_value: String },
    NonCanonicalRoute { requested_entries: Vec<String> },
    NotApplicableToScene { scene: StudioScene },
    InitialWeatherMustBeClear { weather: WeatherState },
    QualityMustBeFixedLowOrHigh { quality: StudioQualitySelection },
}

impl std::fmt::Display for StudioGate1WeatherProofQueryError {
    fn fmt(&self, formatter: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Self::DuplicateParameter { requested_values } => write!(
                formatter,
                "le paramètre forge_gate1_weather_proof doit apparaître une seule fois (valeurs reçues : {})",
                requested_values.join(", ")
            ),
            Self::UnsupportedValue { requested_value } => write!(
                formatter,
                "valeur forge_gate1_weather_proof non prise en charge : {requested_value:?} (attendu : v1)"
            ),
            Self::NonCanonicalRoute { requested_entries } => write!(
                formatter,
                "forge_gate1_weather_proof exige exactement forge_scene=visual-valley, forge_weather=clear, forge_quality=low|high et forge_gate1_weather_proof=v1 (reçu : {})",
                requested_entries.join("&")
            ),
            Self::NotApplicableToScene { scene } => write!(
                formatter,
                "forge_gate1_weather_proof est réservé à visual-valley (scène sélectionnée : {})",
                scene.query_value()
            ),
            Self::InitialWeatherMustBeClear { weather } => write!(
                formatter,
                "forge_gate1_weather_proof exige forge_weather=clear (reçu : {weather:?})"
            ),
            Self::QualityMustBeFixedLowOrHigh { quality } => write!(
                formatter,
                "forge_gate1_weather_proof exige forge_quality=low ou high fixe (reçu : {quality:?})"
            ),
        }
    }
}

impl std::error::Error for StudioGate1WeatherProofQueryError {}

pub fn parse_studio_gate1_weather_proof_selection(
    search: &str,
    scene: StudioScene,
    weather: WeatherState,
    quality: StudioQualitySelection,
) -> Result<StudioGate1WeatherProofSelection, StudioGate1WeatherProofQueryError> {
    let requested_entries = search
        .trim_start_matches('?')
        .split('&')
        .filter(|entry| !entry.is_empty())
        .map(str::to_owned)
        .collect::<Vec<_>>();
    let requested_values = requested_entries
        .iter()
        .map(String::as_str)
        .map(|entry| entry.split_once('=').unwrap_or((entry, "")))
        .filter(|(key, _)| *key == STUDIO_GATE1_WEATHER_PROOF_QUERY_KEY)
        .map(|(_, value)| value.to_owned())
        .collect::<Vec<_>>();

    let requested_value = match requested_values.as_slice() {
        [] => return Ok(StudioGate1WeatherProofSelection::Disabled),
        [requested_value] => requested_value,
        _ => {
            return Err(StudioGate1WeatherProofQueryError::DuplicateParameter { requested_values });
        }
    };
    if requested_value != "v1" {
        return Err(StudioGate1WeatherProofQueryError::UnsupportedValue {
            requested_value: requested_value.clone(),
        });
    }
    if scene != StudioScene::VisualValley {
        return Err(StudioGate1WeatherProofQueryError::NotApplicableToScene { scene });
    }
    if weather != WeatherState::Clear {
        return Err(StudioGate1WeatherProofQueryError::InitialWeatherMustBeClear { weather });
    }
    let expected_quality = match quality {
        StudioQualitySelection::Fixed(VisualQualityTier::Low) => "low",
        StudioQualitySelection::Fixed(VisualQualityTier::High) => "high",
        _ => {
            return Err(StudioGate1WeatherProofQueryError::QualityMustBeFixedLowOrHigh { quality });
        }
    };
    let route_is_canonical = {
        let route_entry_matches = |key: &str, value: &str| {
            requested_entries
                .iter()
                .filter_map(|entry| entry.split_once('='))
                .filter(|(actual_key, actual_value)| *actual_key == key && *actual_value == value)
                .count()
                == 1
        };
        requested_entries.len() == 4
            && route_entry_matches("forge_scene", "visual-valley")
            && route_entry_matches("forge_weather", "clear")
            && route_entry_matches("forge_quality", expected_quality)
            && route_entry_matches(STUDIO_GATE1_WEATHER_PROOF_QUERY_KEY, "v1")
    };
    if !route_is_canonical {
        return Err(StudioGate1WeatherProofQueryError::NonCanonicalRoute { requested_entries });
    }

    Ok(StudioGate1WeatherProofSelection::EnabledV1)
}

#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
mod runtime_contract {
    use super::{Deserialize, Serialize, VisualQualityTier, WeatherState};
    #[cfg(all(target_arch = "wasm32", feature = "web"))]
    use crate::StudioGpuPresentationStatus;
    use forge_project::{PlayerControllerProfileDocument, TerrainDocument, WaterDocument};
    use forge_world::{TerrainGenerationPlan, sample_river_centerline};

    pub(crate) const GATE1_BROWSER_WEATHER_PROTOCOL_ID: &str =
        "r1-023-025-026-browser-weather-matrix-v1";
    pub(crate) const GATE1_BROWSER_WEATHER_MINIMUM_SETTLED_FRAMES: u32 = 8;
    #[cfg(all(target_arch = "wasm32", feature = "web"))]
    pub(crate) const GATE1_BROWSER_WEATHER_TRANSITION_TIMEOUT_MS: u32 = 30_000;
    pub(crate) const GATE1_BROWSER_WATER_VIEW_ANCHOR_X_M: f32 = 16.0;
    pub(crate) const GATE1_BROWSER_WATER_VIEW_ANCHOR_Z_M: f32 = 160.0;
    pub(crate) const GATE1_BROWSER_WATER_VIEW_DISTANCE_M: f32 = 10.0;

    #[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
    #[serde(deny_unknown_fields)]
    pub(crate) struct BrowserWeatherProofCameraPose {
        pub(crate) translation_m: [f32; 3],
        pub(crate) target_m: [f32; 3],
        pub(crate) vertical_fov_degrees: f32,
    }

    pub(crate) fn browser_weather_proof_camera_pose(
        terrain: &TerrainDocument,
        water: &WaterDocument,
        player: &PlayerControllerProfileDocument,
    ) -> Result<BrowserWeatherProofCameraPose, String> {
        let terrain = TerrainGenerationPlan::from_document(terrain)
            .map_err(|error| format!("cannot prepare Browser proof terrain: {error}"))?;
        let samples = sample_river_centerline(water)
            .map_err(|error| format!("cannot sample Browser proof river: {error}"))?;
        let (index, focus) = samples
            .iter()
            .enumerate()
            .min_by(|(_, left), (_, right)| {
                browser_water_anchor_distance_squared(left.center_m.x, left.center_m.z).total_cmp(
                    &browser_water_anchor_distance_squared(right.center_m.x, right.center_m.z),
                )
            })
            .ok_or_else(|| "Browser proof river has no centreline sample".to_owned())?;
        let previous = samples[index.saturating_sub(1)].center_m;
        let next = samples[(index + 1).min(samples.len() - 1)].center_m;
        let tangent_x = next.x - previous.x;
        let tangent_z = next.z - previous.z;
        let tangent_length = tangent_x.hypot(tangent_z);
        if !tangent_length.is_finite() || tangent_length <= f32::EPSILON {
            return Err("Browser proof river anchor has no plan-view tangent".to_owned());
        }
        let normal_x = -tangent_z / tangent_length;
        let normal_z = tangent_x / tangent_length;
        let candidates = [
            (
                focus.center_m.x + normal_x * GATE1_BROWSER_WATER_VIEW_DISTANCE_M,
                focus.center_m.z + normal_z * GATE1_BROWSER_WATER_VIEW_DISTANCE_M,
            ),
            (
                focus.center_m.x - normal_x * GATE1_BROWSER_WATER_VIEW_DISTANCE_M,
                focus.center_m.z - normal_z * GATE1_BROWSER_WATER_VIEW_DISTANCE_M,
            ),
        ];
        let (camera_x, camera_z, terrain_height) = candidates
            .into_iter()
            .filter_map(|(x, z)| terrain.height_at(x, z).ok().map(|height| (x, z, height)))
            .max_by(|(_, _, left_height), (_, _, right_height)| left_height.total_cmp(right_height))
            .ok_or_else(|| {
                "Browser proof water camera cannot be placed inside the terrain".to_owned()
            })?;

        Ok(BrowserWeatherProofCameraPose {
            translation_m: [
                camera_x,
                terrain_height + player.camera.eye_height_m,
                camera_z,
            ],
            target_m: [focus.center_m.x, focus.center_m.y + 0.05, focus.center_m.z],
            vertical_fov_degrees: player.camera.first_person_vertical_fov_degrees,
        })
    }

    fn browser_water_anchor_distance_squared(x: f32, z: f32) -> f32 {
        (x - GATE1_BROWSER_WATER_VIEW_ANCHOR_X_M).powi(2)
            + (z - GATE1_BROWSER_WATER_VIEW_ANCHOR_Z_M).powi(2)
    }

    #[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
    #[serde(rename_all = "snake_case")]
    pub(crate) enum BrowserWeatherProofStage {
        InitialClear,
        Rain,
        RestoredClear,
    }

    impl BrowserWeatherProofStage {
        pub(crate) const fn expected_weather(self) -> WeatherState {
            match self {
                Self::InitialClear | Self::RestoredClear => WeatherState::Clear,
                Self::Rain => WeatherState::Rain,
            }
        }
    }

    #[derive(Clone, Copy, Debug, Eq, PartialEq)]
    pub(crate) enum BrowserWeatherProofPhase {
        Settling(BrowserWeatherProofStage),
        AwaitingAdvance(BrowserWeatherProofStage),
        Complete,
        #[cfg(all(target_arch = "wasm32", feature = "web"))]
        Failed,
    }

    #[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
    #[serde(deny_unknown_fields)]
    pub(crate) struct BrowserWeatherProofAdvanceCommand {
        schema: String,
        schema_version: u32,
        protocol_id: String,
        sequence: u32,
        from_stage: BrowserWeatherProofStage,
    }

    #[derive(Clone, Copy, Debug)]
    pub(crate) struct BrowserWeatherProofSequencer {
        pub(crate) phase: BrowserWeatherProofPhase,
        pub(crate) settled_frames: u32,
    }

    impl BrowserWeatherProofSequencer {
        pub(crate) const fn new() -> Self {
            Self {
                phase: BrowserWeatherProofPhase::Settling(BrowserWeatherProofStage::InitialClear),
                settled_frames: 0,
            }
        }

        pub(crate) fn record_endpoint_frame(
            &mut self,
            endpoint_ready: bool,
        ) -> Option<BrowserWeatherProofStage> {
            let BrowserWeatherProofPhase::Settling(stage) = self.phase else {
                return None;
            };
            if !endpoint_ready {
                self.settled_frames = 0;
                return None;
            }
            self.settled_frames = self.settled_frames.saturating_add(1);
            if self.settled_frames < GATE1_BROWSER_WEATHER_MINIMUM_SETTLED_FRAMES {
                return None;
            }
            self.phase = match stage {
                BrowserWeatherProofStage::InitialClear | BrowserWeatherProofStage::Rain => {
                    BrowserWeatherProofPhase::AwaitingAdvance(stage)
                }
                BrowserWeatherProofStage::RestoredClear => BrowserWeatherProofPhase::Complete,
            };
            Some(stage)
        }

        pub(crate) fn request_advance(
            &mut self,
            command: &BrowserWeatherProofAdvanceCommand,
        ) -> Result<WeatherState, String> {
            if command.schema != "forge.gate1-browser-weather-proof-advance"
                || command.schema_version != 1
                || command.protocol_id != GATE1_BROWSER_WEATHER_PROTOCOL_ID
            {
                return Err("Browser weather proof advance command contract mismatch".to_owned());
            }
            let (expected_sequence, expected_stage, next_stage) = match self.phase {
                BrowserWeatherProofPhase::AwaitingAdvance(
                    BrowserWeatherProofStage::InitialClear,
                ) => (
                    1,
                    BrowserWeatherProofStage::InitialClear,
                    BrowserWeatherProofStage::Rain,
                ),
                BrowserWeatherProofPhase::AwaitingAdvance(BrowserWeatherProofStage::Rain) => (
                    2,
                    BrowserWeatherProofStage::Rain,
                    BrowserWeatherProofStage::RestoredClear,
                ),
                _ => {
                    return Err(format!(
                        "Browser weather proof cannot advance from phase {:?}",
                        self.phase
                    ));
                }
            };
            if command.sequence != expected_sequence || command.from_stage != expected_stage {
                return Err(format!(
                    "Browser weather proof advance order mismatch: expected sequence {expected_sequence} from {expected_stage:?}, received sequence {} from {:?}",
                    command.sequence, command.from_stage
                ));
            }
            self.phase = BrowserWeatherProofPhase::Settling(next_stage);
            self.settled_frames = 0;
            Ok(next_stage.expected_weather())
        }

        #[cfg(all(target_arch = "wasm32", feature = "web"))]
        pub(crate) fn fail(&mut self) {
            self.phase = BrowserWeatherProofPhase::Failed;
        }
    }

    #[cfg(all(target_arch = "wasm32", feature = "web"))]
    #[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
    #[serde(deny_unknown_fields)]
    pub(crate) struct BrowserWeatherProofWaterObservation {
        pub(crate) runtime_observation: String,
        pub(crate) shader_material_assets: usize,
        pub(crate) shader_material_entities: usize,
        pub(crate) shader_uniforms_match_profile: bool,
        pub(crate) requested_reflection_intensity: Option<f32>,
        pub(crate) applied_material_reflectance: Option<f32>,
        pub(crate) requested_reflection_steps: u16,
        pub(crate) reflection_intensity_status: String,
        pub(crate) requested_refraction_strength: Option<f32>,
        pub(crate) applied_specular_transmission: Option<f32>,
        pub(crate) requested_refraction_steps: u16,
        pub(crate) screen_space_transmission_steps: usize,
        pub(crate) screen_space_transmission_quality: String,
        pub(crate) screen_space_transmission_blur_taps: u8,
        pub(crate) refraction_status: String,
        pub(crate) environment_reflection_status: String,
        pub(crate) screen_space_transmission_status: String,
        pub(crate) cpu_ecs_application_status: String,
        pub(crate) gpu_pass_observed: bool,
        pub(crate) webgpu_execution_status: String,
    }

    #[cfg(all(target_arch = "wasm32", feature = "web"))]
    #[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
    #[serde(deny_unknown_fields)]
    pub(crate) struct BrowserWeatherProofPostProcessingObservation {
        pub(crate) bloom_requested: bool,
        pub(crate) bloom_component_present: bool,
        pub(crate) color_grading_component_present: bool,
        pub(crate) mapped_exposure_ev: Option<f32>,
        pub(crate) mapped_temperature: Option<f32>,
        pub(crate) mapped_tint: Option<f32>,
        pub(crate) mapped_post_saturation: Option<f32>,
        pub(crate) mapped_contrast: Option<f32>,
        pub(crate) mapped_gamma: Option<f32>,
    }

    #[cfg(all(target_arch = "wasm32", feature = "web"))]
    #[allow(
        clippy::struct_excessive_bools,
        reason = "the evidence checkpoint keeps each independently audited observation explicit"
    )]
    #[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
    #[serde(deny_unknown_fields)]
    pub(crate) struct BrowserWeatherProofCheckpoint {
        pub(crate) sequence: u32,
        pub(crate) stage: BrowserWeatherProofStage,
        pub(crate) recorded_at_utc: String,
        pub(crate) runtime_instance_id: String,
        pub(crate) viewport_starts: u32,
        pub(crate) requested_weather: WeatherState,
        pub(crate) runtime_target_weather: WeatherState,
        pub(crate) weather_projection_status: String,
        pub(crate) weather_evidence_source: String,
        pub(crate) weather_gpu_execution_observed: bool,
        pub(crate) current_wetness: f32,
        pub(crate) target_wetness: f32,
        pub(crate) terrain_materials: usize,
        pub(crate) vegetation_materials: usize,
        pub(crate) terrain_uniforms_match: bool,
        pub(crate) vegetation_uniforms_match: bool,
        pub(crate) clear_target_restored_exactly: Option<bool>,
        pub(crate) rain_visible: bool,
        pub(crate) quality_current_tier: VisualQualityTier,
        pub(crate) quality_applied_tier: VisualQualityTier,
        pub(crate) settled_frames: u32,
        pub(crate) minimum_settled_frames: u32,
        pub(crate) weather_simulation_seconds: f64,
        pub(crate) camera_pose: BrowserWeatherProofCameraPose,
        pub(crate) camera_pose_matches: bool,
        pub(crate) active_camera_count: u32,
        pub(crate) water: BrowserWeatherProofWaterObservation,
        pub(crate) post_processing: BrowserWeatherProofPostProcessingObservation,
        pub(crate) gpu_presentation: StudioGpuPresentationStatus,
    }

    #[cfg(all(target_arch = "wasm32", feature = "web"))]
    #[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
    #[serde(rename_all = "snake_case")]
    pub(crate) enum BrowserWeatherProofLedgerStatus {
        Initializing,
        SettlingInitialClear,
        AwaitingAdvanceFromInitialClear,
        SettlingRain,
        AwaitingAdvanceFromRain,
        SettlingRestoredClear,
        CompletePendingExternalValidation,
        Failed,
    }

    #[cfg(all(target_arch = "wasm32", feature = "web"))]
    #[allow(
        clippy::struct_excessive_bools,
        reason = "the proof ledger serializes explicit non-claims instead of collapsing them"
    )]
    #[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
    #[serde(deny_unknown_fields)]
    pub(crate) struct BrowserWeatherProofLedger {
        pub(crate) schema: String,
        pub(crate) schema_version: u32,
        pub(crate) protocol_id: String,
        pub(crate) build_source_commit: Option<String>,
        pub(crate) runtime_instance_id: String,
        pub(crate) requested_quality_tier: VisualQualityTier,
        pub(crate) status: BrowserWeatherProofLedgerStatus,
        pub(crate) checkpoints: Vec<BrowserWeatherProofCheckpoint>,
        pub(crate) failure_reason: Option<String>,
        pub(crate) same_bevy_instance_required: bool,
        pub(crate) minimum_settled_frames: u32,
        pub(crate) transition_timeout_ms: u32,
        pub(crate) water_camera_anchor_x_m: f32,
        pub(crate) water_camera_anchor_z_m: f32,
        pub(crate) water_camera_view_distance_m: f32,
        pub(crate) browser_captures_performed_by_controller: bool,
        pub(crate) weather_shader_consumption_proven: bool,
        pub(crate) water_draw_attribution_proven: bool,
        pub(crate) exact_render_frame_identity_proven: bool,
        pub(crate) human_review_performed: bool,
        pub(crate) gate1_routes_qualified: bool,
        pub(crate) limitations: Vec<String>,
    }

    #[cfg(test)]
    mod tests {
        use super::super::{
            StudioGate1WeatherProofQueryError, StudioGate1WeatherProofSelection,
            StudioQualitySelection, StudioScene, parse_studio_gate1_weather_proof_selection,
        };
        use super::*;
        use forge_world::{
            embedded_visual_valley_player_controller_profile,
            embedded_visual_valley_terrain_document, embedded_visual_valley_water_document,
        };

        #[test]
        fn query_is_explicit_and_fail_closed() {
            let low = StudioQualitySelection::Fixed(VisualQualityTier::Low);
            let high = StudioQualitySelection::Fixed(VisualQualityTier::High);
            assert_eq!(
            parse_studio_gate1_weather_proof_selection(
                "?forge_scene=visual-valley&forge_weather=clear&forge_quality=low&forge_gate1_weather_proof=v1",
                StudioScene::VisualValley,
                WeatherState::Clear,
                low,
            )
            .unwrap(),
            StudioGate1WeatherProofSelection::EnabledV1
        );
            assert_eq!(
                parse_studio_gate1_weather_proof_selection(
                    "?forge_scene=visual-valley&forge_weather=clear&forge_quality=high",
                    StudioScene::VisualValley,
                    WeatherState::Clear,
                    high,
                )
                .unwrap(),
                StudioGate1WeatherProofSelection::Disabled
            );
            assert!(matches!(
                parse_studio_gate1_weather_proof_selection(
                    "?forge_gate1_weather_proof=v2",
                    StudioScene::VisualValley,
                    WeatherState::Clear,
                    high,
                ),
                Err(StudioGate1WeatherProofQueryError::UnsupportedValue { .. })
            ));
            assert!(matches!(
                parse_studio_gate1_weather_proof_selection(
                    "?forge_gate1_weather_proof=v1&forge_gate1_weather_proof=v1",
                    StudioScene::VisualValley,
                    WeatherState::Clear,
                    high,
                ),
                Err(StudioGate1WeatherProofQueryError::DuplicateParameter { .. })
            ));
            for non_canonical in [
                "?forge_gate1_weather_proof=v1",
                "?forge_scene=visual-valley&forge_weather=clear&forge_quality=high&forge_gate1_weather_proof=v1&extra=true",
                "?forge_scene=visual-valley&forge_weather=clear&forge_weather=clear&forge_quality=high&forge_gate1_weather_proof=v1",
            ] {
                assert!(matches!(
                    parse_studio_gate1_weather_proof_selection(
                        non_canonical,
                        StudioScene::VisualValley,
                        WeatherState::Clear,
                        high,
                    ),
                    Err(StudioGate1WeatherProofQueryError::NonCanonicalRoute { .. })
                ));
            }
            assert!(matches!(
                parse_studio_gate1_weather_proof_selection(
                    "?forge_gate1_weather_proof=v1",
                    StudioScene::Gate0,
                    WeatherState::Clear,
                    high,
                ),
                Err(StudioGate1WeatherProofQueryError::NotApplicableToScene { .. })
            ));
            assert!(matches!(
                parse_studio_gate1_weather_proof_selection(
                    "?forge_gate1_weather_proof=v1",
                    StudioScene::VisualValley,
                    WeatherState::Rain,
                    high,
                ),
                Err(StudioGate1WeatherProofQueryError::InitialWeatherMustBeClear { .. })
            ));
            for quality in [
                StudioQualitySelection::Fixed(VisualQualityTier::Medium),
                StudioQualitySelection::Fixed(VisualQualityTier::Ultra),
                StudioQualitySelection::Adaptive,
                StudioQualitySelection::Benchmark,
            ] {
                assert!(matches!(
                    parse_studio_gate1_weather_proof_selection(
                        "?forge_gate1_weather_proof=v1",
                        StudioScene::VisualValley,
                        WeatherState::Clear,
                        quality,
                    ),
                    Err(StudioGate1WeatherProofQueryError::QualityMustBeFixedLowOrHigh { .. })
                ));
            }
        }

        fn advance(
            sequence: u32,
            from_stage: BrowserWeatherProofStage,
        ) -> BrowserWeatherProofAdvanceCommand {
            BrowserWeatherProofAdvanceCommand {
                schema: "forge.gate1-browser-weather-proof-advance".to_owned(),
                schema_version: 1,
                protocol_id: GATE1_BROWSER_WEATHER_PROTOCOL_ID.to_owned(),
                sequence,
                from_stage,
            }
        }

        #[test]
        fn requires_eight_stable_frames_and_ordered_advances() {
            let mut sequencer = BrowserWeatherProofSequencer::new();
            for _ in 0..(GATE1_BROWSER_WEATHER_MINIMUM_SETTLED_FRAMES - 1) {
                assert_eq!(sequencer.record_endpoint_frame(true), None);
            }
            assert_eq!(
                sequencer.record_endpoint_frame(true),
                Some(BrowserWeatherProofStage::InitialClear)
            );
            assert_eq!(
                sequencer
                    .request_advance(&advance(1, BrowserWeatherProofStage::InitialClear))
                    .unwrap(),
                WeatherState::Rain
            );
            assert_eq!(sequencer.record_endpoint_frame(true), None);
            sequencer.record_endpoint_frame(false);
            assert_eq!(sequencer.settled_frames, 0);
            for _ in 0..GATE1_BROWSER_WEATHER_MINIMUM_SETTLED_FRAMES {
                if sequencer.record_endpoint_frame(true).is_some() {
                    break;
                }
            }
            assert_eq!(
                sequencer.phase,
                BrowserWeatherProofPhase::AwaitingAdvance(BrowserWeatherProofStage::Rain)
            );
            assert_eq!(
                sequencer
                    .request_advance(&advance(2, BrowserWeatherProofStage::Rain))
                    .unwrap(),
                WeatherState::Clear
            );
            let mut restored = None;
            for _ in 0..GATE1_BROWSER_WEATHER_MINIMUM_SETTLED_FRAMES {
                restored = sequencer.record_endpoint_frame(true).or(restored);
            }
            assert_eq!(restored, Some(BrowserWeatherProofStage::RestoredClear));
            assert_eq!(sequencer.phase, BrowserWeatherProofPhase::Complete);
        }

        #[test]
        fn rejects_early_duplicate_and_mismatched_advances() {
            let mut sequencer = BrowserWeatherProofSequencer::new();
            assert!(
                sequencer
                    .request_advance(&advance(1, BrowserWeatherProofStage::InitialClear))
                    .is_err()
            );
            for _ in 0..GATE1_BROWSER_WEATHER_MINIMUM_SETTLED_FRAMES {
                sequencer.record_endpoint_frame(true);
            }
            assert!(
                sequencer
                    .request_advance(&advance(2, BrowserWeatherProofStage::InitialClear))
                    .is_err()
            );
            let mut wrong_protocol = advance(1, BrowserWeatherProofStage::InitialClear);
            wrong_protocol.protocol_id = "wrong".to_owned();
            assert!(sequencer.request_advance(&wrong_protocol).is_err());
        }

        #[test]
        fn water_pose_is_deterministic_and_finite() {
            let terrain = embedded_visual_valley_terrain_document().unwrap();
            let water = embedded_visual_valley_water_document().unwrap();
            let player = embedded_visual_valley_player_controller_profile().unwrap();
            let first = browser_weather_proof_camera_pose(&terrain, &water, &player).unwrap();
            let replay = browser_weather_proof_camera_pose(&terrain, &water, &player).unwrap();
            assert_eq!(first, replay);
            assert!(
                first
                    .translation_m
                    .into_iter()
                    .chain(first.target_m)
                    .all(f32::is_finite)
            );
            let view_vector_length_squared = first
                .translation_m
                .into_iter()
                .zip(first.target_m)
                .map(|(translation, target)| (translation - target).powi(2))
                .sum::<f32>();
            assert!(view_vector_length_squared > f32::EPSILON);
            assert_eq!(
                first.vertical_fov_degrees.to_bits(),
                player.camera.first_person_vertical_fov_degrees.to_bits()
            );
        }
    }
}

#[cfg(all(target_arch = "wasm32", feature = "web"))]
pub(crate) use runtime_contract::*;
