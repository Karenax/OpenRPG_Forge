use crate::gate1_browser_weather_matrix::{
    BrowserWeatherProofAdvanceCommand, BrowserWeatherProofCameraPose,
    BrowserWeatherProofCheckpoint, BrowserWeatherProofLedger, BrowserWeatherProofLedgerStatus,
    BrowserWeatherProofPhase, BrowserWeatherProofPostProcessingObservation,
    BrowserWeatherProofSequencer, BrowserWeatherProofStage, BrowserWeatherProofWaterObservation,
    GATE1_BROWSER_WATER_VIEW_ANCHOR_X_M, GATE1_BROWSER_WATER_VIEW_ANCHOR_Z_M,
    GATE1_BROWSER_WATER_VIEW_DISTANCE_M, GATE1_BROWSER_WEATHER_MINIMUM_SETTLED_FRAMES,
    GATE1_BROWSER_WEATHER_PROTOCOL_ID, GATE1_BROWSER_WEATHER_TRANSITION_TIMEOUT_MS,
};
use crate::{StudioGpuPresentationStatus, VisualQualityTier};
use bevy::prelude::*;
use forge_engine::{
    ForgePlayerCamera, VisualValleyQualityStats, WaterProjectionStats,
    WaterRuntimeObservationStatus, WeatherProjectionStats, WeatherProjectionStatus,
    WeatherRuntimeState,
};
use leptos::prelude::*;
use std::{cell::RefCell, collections::VecDeque};
use wasm_bindgen::{JsCast, JsValue, closure::Closure};

const BROWSER_WEATHER_PROOF_LEDGER_EVENT: &str = "forge-gate1-browser-weather-proof-ledger";
const BROWSER_WEATHER_PROOF_ADVANCE_EVENT: &str = "forge-gate1-browser-weather-proof-advance";

thread_local! {
    static ADVANCE_QUEUE: RefCell<VecDeque<String>> = const { RefCell::new(VecDeque::new()) };
}

pub(crate) fn install_listeners(ledger: WriteSignal<Option<BrowserWeatherProofLedger>>) {
    ADVANCE_QUEUE.with(|queue| queue.borrow_mut().clear());
    let ledger_listener =
        Closure::<dyn FnMut(web_sys::CustomEvent)>::new(move |event: web_sys::CustomEvent| {
            let Some(encoded) = event.detail().as_string() else {
                web_sys::console::error_1(&JsValue::from_str(
                    "Browser weather proof ledger event did not contain JSON text",
                ));
                return;
            };
            match serde_json::from_str::<BrowserWeatherProofLedger>(&encoded) {
                Ok(next) => ledger.set(Some(next)),
                Err(error) => web_sys::console::error_1(&JsValue::from_str(&format!(
                    "Browser weather proof ledger event was invalid: {error}"
                ))),
            }
        });
    let advance_listener =
        Closure::<dyn FnMut(web_sys::CustomEvent)>::new(move |event: web_sys::CustomEvent| {
            let encoded = event
                .detail()
                .as_string()
                .unwrap_or_else(|| "missing JSON text".to_owned());
            ADVANCE_QUEUE.with(|queue| queue.borrow_mut().push_back(encoded));
        });
    if let Some(window) = web_sys::window() {
        if window
            .add_event_listener_with_callback(
                BROWSER_WEATHER_PROOF_LEDGER_EVENT,
                ledger_listener.as_ref().unchecked_ref(),
            )
            .is_ok()
        {
            ledger_listener.forget();
        }
        if window
            .add_event_listener_with_callback(
                BROWSER_WEATHER_PROOF_ADVANCE_EVENT,
                advance_listener.as_ref().unchecked_ref(),
            )
            .is_ok()
        {
            advance_listener.forget();
        }
    }
}

pub(crate) fn install_controller(
    app: &mut App,
    quality_tier: VisualQualityTier,
    camera_pose: BrowserWeatherProofCameraPose,
) {
    app.insert_non_send(BrowserWeatherProofController::new(
        quality_tier,
        camera_pose,
    ))
    .add_systems(PreUpdate, drive_browser_weather_proof)
    .add_systems(Update, enforce_browser_weather_proof_camera)
    .add_systems(Last, collect_browser_weather_proof_checkpoint);
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
        WeatherProjectionStatus::IncompleteMaterialProjection => "incomplete_material_projection",
    }
}

struct BrowserWeatherProofController {
    sequencer: BrowserWeatherProofSequencer,
    camera_pose: BrowserWeatherProofCameraPose,
    stage_started_ms: Option<f64>,
    initial_ledger_published: bool,
    ledger: BrowserWeatherProofLedger,
}

impl BrowserWeatherProofController {
    fn new(quality_tier: VisualQualityTier, camera_pose: BrowserWeatherProofCameraPose) -> Self {
        let now_ms = js_sys::Date::now();
        let runtime_instance_id =
            format!("forge-weather-proof-{now_ms:.0}-{:.3}", performance_now());
        Self {
            sequencer: BrowserWeatherProofSequencer::new(),
            camera_pose,
            stage_started_ms: None,
            initial_ledger_published: false,
            ledger: BrowserWeatherProofLedger {
                schema: "forge.gate1-browser-weather-proof-ledger".to_owned(),
                schema_version: 1,
                protocol_id: GATE1_BROWSER_WEATHER_PROTOCOL_ID.to_owned(),
                build_source_commit: option_env!("FORGE_SOURCE_COMMIT")
                    .filter(|value| {
                        value.len() == 40
                            && value.bytes().all(|byte| byte.is_ascii_hexdigit())
                    })
                    .map(str::to_owned),
                runtime_instance_id,
                requested_quality_tier: quality_tier,
                status: BrowserWeatherProofLedgerStatus::Initializing,
                checkpoints: Vec::new(),
                failure_reason: None,
                same_bevy_instance_required: true,
                minimum_settled_frames: GATE1_BROWSER_WEATHER_MINIMUM_SETTLED_FRAMES,
                transition_timeout_ms: GATE1_BROWSER_WEATHER_TRANSITION_TIMEOUT_MS,
                water_camera_anchor_x_m: GATE1_BROWSER_WATER_VIEW_ANCHOR_X_M,
                water_camera_anchor_z_m: GATE1_BROWSER_WATER_VIEW_ANCHOR_Z_M,
                water_camera_view_distance_m: GATE1_BROWSER_WATER_VIEW_DISTANCE_M,
                browser_captures_performed_by_controller: false,
                weather_shader_consumption_proven: false,
                water_draw_attribution_proven: false,
                exact_render_frame_identity_proven: false,
                human_review_performed: false,
                gate1_routes_qualified: false,
                limitations: vec![
                    "The Rust controller records CPU/ECS projection endpoints; it does not prove that wetness was consumed by a shader."
                        .to_owned(),
                    "Water optics values describe requested and applied Bevy main-world state without draw or exact render-frame attribution."
                        .to_owned(),
                    "CDP captures, console/network checks, artifact hashes and human inspection belong to the external driver."
                        .to_owned(),
                    "gpu_presentation remains not_observed and Gate 1 routes remain open."
                        .to_owned(),
                ],
            },
        }
    }

    fn publish(&self) {
        match serde_json::to_string(&self.ledger) {
            Ok(encoded) => dispatch_json_event(BROWSER_WEATHER_PROOF_LEDGER_EVENT, &encoded),
            Err(error) => web_sys::console::error_1(&JsValue::from_str(&format!(
                "Browser weather proof ledger serialization failed: {error}"
            ))),
        }
    }

    fn fail(&mut self, reason: String) {
        self.sequencer.fail();
        self.ledger.status = BrowserWeatherProofLedgerStatus::Failed;
        self.ledger.failure_reason = Some(reason);
        self.publish();
    }

    fn set_transition_status(&mut self, stage: BrowserWeatherProofStage) {
        self.ledger.status = match stage {
            BrowserWeatherProofStage::InitialClear => {
                BrowserWeatherProofLedgerStatus::SettlingInitialClear
            }
            BrowserWeatherProofStage::Rain => BrowserWeatherProofLedgerStatus::SettlingRain,
            BrowserWeatherProofStage::RestoredClear => {
                BrowserWeatherProofLedgerStatus::SettlingRestoredClear
            }
        };
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn drive_browser_weather_proof(
    mut weather: ResMut<WeatherRuntimeState>,
    mut controller: NonSendMut<BrowserWeatherProofController>,
) {
    let queued = ADVANCE_QUEUE.with(|queue| queue.borrow_mut().drain(..).collect::<Vec<_>>());
    for encoded in queued {
        let command = match serde_json::from_str::<BrowserWeatherProofAdvanceCommand>(&encoded) {
            Ok(command) => command,
            Err(error) => {
                controller.fail(format!(
                    "Browser weather proof advance command was invalid: {error}"
                ));
                return;
            }
        };
        match controller.sequencer.request_advance(&command) {
            Ok(target_weather) => {
                weather.request_state(target_weather);
                controller.stage_started_ms = Some(performance_now());
                let BrowserWeatherProofPhase::Settling(stage) = controller.sequencer.phase else {
                    controller.fail(
                        "Browser weather proof sequencer did not enter a settling phase".to_owned(),
                    );
                    return;
                };
                controller.set_transition_status(stage);
                controller.publish();
            }
            Err(reason) => {
                controller.fail(reason);
                return;
            }
        }
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn enforce_browser_weather_proof_camera(
    controller: NonSend<BrowserWeatherProofController>,
    mut cameras: Query<
        (&mut Transform, &mut Projection),
        bevy::ecs::query::With<ForgePlayerCamera>,
    >,
) {
    let Ok((mut transform, mut projection)) = cameras.single_mut() else {
        return;
    };
    let translation = Vec3::from_array(controller.camera_pose.translation_m);
    let target = Vec3::from_array(controller.camera_pose.target_m);
    *transform = Transform::from_translation(translation).looking_at(target, Vec3::Y);
    if let Projection::Perspective(perspective) = &mut *projection {
        perspective.fov = controller.camera_pose.vertical_fov_degrees.to_radians();
    }
}

fn browser_weather_proof_camera_matches(
    camera: &Camera,
    transform: &Transform,
    projection: &Projection,
    expected: BrowserWeatherProofCameraPose,
) -> bool {
    if !camera.is_active {
        return false;
    }
    let expected_transform = Transform::from_translation(Vec3::from_array(expected.translation_m))
        .looking_at(Vec3::from_array(expected.target_m), Vec3::Y);
    let Projection::Perspective(perspective) = projection else {
        return false;
    };
    transform.translation == expected_transform.translation
        && transform.rotation == expected_transform.rotation
        && perspective.fov.to_bits() == expected.vertical_fov_degrees.to_radians().to_bits()
}

#[allow(
    clippy::too_many_arguments,
    reason = "each independent main-world proof input remains explicit and fail-closed"
)]
fn browser_weather_proof_endpoint_ready(
    stage: BrowserWeatherProofStage,
    quality_tier: VisualQualityTier,
    weather_runtime: &WeatherRuntimeState,
    weather: &WeatherProjectionStats,
    water: &WaterProjectionStats,
    quality: &VisualValleyQualityStats,
    active_camera_count: usize,
    camera_pose_matches: bool,
) -> bool {
    let expected_weather = stage.expected_weather();
    let expected_bloom = quality_tier == VisualQualityTier::High;
    let at_exact_target = weather.wetness.current.to_bits() == weather.wetness.target.to_bits()
        && weather_runtime.current_wetness().to_bits() == weather.wetness.current.to_bits();
    let clear_restored = expected_weather != super::WeatherState::Clear
        || weather.wetness.clear_target_restored_exactly == Some(true);
    weather_runtime.target_state() == expected_weather
        && weather.target_state == expected_weather
        && weather.status == WeatherProjectionStatus::ObservedCpuProjection
        && at_exact_target
        && weather.wetness.terrain_materials > 0
        && weather.wetness.vegetation_materials > 0
        && weather.wetness.terrain_uniforms_match
        && weather.wetness.vegetation_uniforms_match
        && clear_restored
        && weather.rain.visible == (expected_weather == super::WeatherState::Rain)
        && weather.post_processing.bloom_requested == expected_bloom
        && weather.post_processing.bloom_component_present == expected_bloom
        && weather.post_processing.color_grading_component_present
        && quality.current_tier == quality_tier
        && quality.dynamically_applied_tier == Some(quality_tier)
        && water.runtime_observation == WaterRuntimeObservationStatus::ObservedMainWorldComponents
        && water.shader_material_assets == 1
        && water.shader_material_entities == 1
        && water.shader_uniforms_match_profile
        && active_camera_count == 1
        && camera_pose_matches
        && crate::web::viewport_starts() == 1
}

#[allow(
    clippy::too_many_arguments,
    reason = "the serialized checkpoint records each observed Bevy resource explicitly"
)]
fn browser_weather_proof_checkpoint(
    controller: &BrowserWeatherProofController,
    stage: BrowserWeatherProofStage,
    weather_runtime: &WeatherRuntimeState,
    weather: &WeatherProjectionStats,
    water: &WaterProjectionStats,
    quality: &VisualValleyQualityStats,
    active_camera_count: usize,
    camera_pose_matches: bool,
) -> BrowserWeatherProofCheckpoint {
    BrowserWeatherProofCheckpoint {
        sequence: u32::try_from(controller.ledger.checkpoints.len() + 1).unwrap_or(u32::MAX),
        stage,
        recorded_at_utc: recorded_at_utc(),
        runtime_instance_id: controller.ledger.runtime_instance_id.clone(),
        viewport_starts: u32::try_from(crate::web::viewport_starts()).unwrap_or(u32::MAX),
        requested_weather: stage.expected_weather(),
        runtime_target_weather: weather.target_state,
        weather_projection_status: weather_observation_label(weather.status).to_owned(),
        weather_evidence_source: weather.evidence_source.to_owned(),
        weather_gpu_execution_observed: weather.gpu_execution_observed,
        current_wetness: weather.wetness.current,
        target_wetness: weather.wetness.target,
        terrain_materials: weather.wetness.terrain_materials,
        vegetation_materials: weather.wetness.vegetation_materials,
        terrain_uniforms_match: weather.wetness.terrain_uniforms_match,
        vegetation_uniforms_match: weather.wetness.vegetation_uniforms_match,
        clear_target_restored_exactly: weather.wetness.clear_target_restored_exactly,
        rain_visible: weather.rain.visible,
        quality_current_tier: quality.current_tier,
        quality_applied_tier: quality
            .dynamically_applied_tier
            .unwrap_or(quality.current_tier),
        settled_frames: controller.sequencer.settled_frames,
        minimum_settled_frames: GATE1_BROWSER_WEATHER_MINIMUM_SETTLED_FRAMES,
        weather_simulation_seconds: weather_runtime.simulation_seconds(),
        camera_pose: controller.camera_pose,
        camera_pose_matches,
        active_camera_count: u32::try_from(active_camera_count).unwrap_or(u32::MAX),
        water: BrowserWeatherProofWaterObservation {
            runtime_observation: water_observation_label(water.runtime_observation).to_owned(),
            shader_material_assets: water.shader_material_assets,
            shader_material_entities: water.shader_material_entities,
            shader_uniforms_match_profile: water.shader_uniforms_match_profile,
            requested_reflection_intensity: water.requested_reflection_intensity,
            applied_material_reflectance: water.applied_material_reflectance,
            requested_reflection_steps: water.requested_reflection_steps,
            reflection_intensity_status: water.reflection_intensity_status.to_owned(),
            requested_refraction_strength: water.requested_refraction_strength,
            applied_specular_transmission: water.applied_specular_transmission,
            requested_refraction_steps: water.requested_refraction_steps,
            screen_space_transmission_steps: water.screen_space_transmission_steps,
            screen_space_transmission_quality: water.screen_space_transmission_quality.to_owned(),
            screen_space_transmission_blur_taps: water.screen_space_transmission_blur_taps,
            refraction_status: water.refraction_status.to_owned(),
            environment_reflection_status: water.environment_reflection_status.to_owned(),
            screen_space_transmission_status: water.screen_space_transmission_status.to_owned(),
            cpu_ecs_application_status: water.cpu_ecs_application_status.to_owned(),
            gpu_pass_observed: water.gpu_pass_observed,
            webgpu_execution_status: water.webgpu_execution_status.to_owned(),
        },
        post_processing: BrowserWeatherProofPostProcessingObservation {
            bloom_requested: weather.post_processing.bloom_requested,
            bloom_component_present: weather.post_processing.bloom_component_present,
            color_grading_component_present: weather
                .post_processing
                .color_grading_component_present,
            mapped_exposure_ev: weather.post_processing.mapped_exposure_ev,
            mapped_temperature: weather.post_processing.mapped_temperature,
            mapped_tint: weather.post_processing.mapped_tint,
            mapped_post_saturation: weather.post_processing.mapped_post_saturation,
            mapped_contrast: weather.post_processing.mapped_contrast,
            mapped_gamma: weather.post_processing.mapped_gamma,
        },
        gpu_presentation: StudioGpuPresentationStatus::NotObserved,
    }
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::too_many_arguments,
    reason = "Bevy systems require owned system parameter wrappers and explicit proof inputs"
)]
fn collect_browser_weather_proof_checkpoint(
    cameras: Query<(&Camera, &Transform, &Projection), bevy::ecs::query::With<ForgePlayerCamera>>,
    weather_runtime: Res<WeatherRuntimeState>,
    weather: Res<WeatherProjectionStats>,
    water: Res<WaterProjectionStats>,
    quality: Res<VisualValleyQualityStats>,
    mut controller: NonSendMut<BrowserWeatherProofController>,
) {
    if !controller.initial_ledger_published {
        controller.ledger.status = BrowserWeatherProofLedgerStatus::SettlingInitialClear;
        controller.initial_ledger_published = true;
        controller.stage_started_ms = Some(performance_now());
        controller.publish();
    }
    let BrowserWeatherProofPhase::Settling(stage) = controller.sequencer.phase else {
        return;
    };
    let now_ms = performance_now();
    let Some(stage_started_ms) = controller.stage_started_ms else {
        controller.fail("Browser weather proof stage timer was not initialized".to_owned());
        return;
    };
    if now_ms - stage_started_ms > f64::from(GATE1_BROWSER_WEATHER_TRANSITION_TIMEOUT_MS) {
        controller.fail(format!(
            "Browser weather proof timed out after {GATE1_BROWSER_WEATHER_TRANSITION_TIMEOUT_MS} ms while settling {stage:?}"
        ));
        return;
    }

    let active_camera_count = cameras
        .iter()
        .filter(|(camera, ..)| camera.is_active)
        .count();
    let camera_pose_matches = cameras
        .single()
        .is_ok_and(|(camera, transform, projection)| {
            browser_weather_proof_camera_matches(
                camera,
                transform,
                projection,
                controller.camera_pose,
            )
        });
    let endpoint_ready = browser_weather_proof_endpoint_ready(
        stage,
        controller.ledger.requested_quality_tier,
        &weather_runtime,
        &weather,
        &water,
        &quality,
        active_camera_count,
        camera_pose_matches,
    );
    let Some(checkpoint_stage) = controller.sequencer.record_endpoint_frame(endpoint_ready) else {
        return;
    };
    let checkpoint = browser_weather_proof_checkpoint(
        &controller,
        checkpoint_stage,
        &weather_runtime,
        &weather,
        &water,
        &quality,
        active_camera_count,
        camera_pose_matches,
    );
    controller.ledger.checkpoints.push(checkpoint);
    controller.ledger.status = match checkpoint_stage {
        BrowserWeatherProofStage::InitialClear => {
            BrowserWeatherProofLedgerStatus::AwaitingAdvanceFromInitialClear
        }
        BrowserWeatherProofStage::Rain => BrowserWeatherProofLedgerStatus::AwaitingAdvanceFromRain,
        BrowserWeatherProofStage::RestoredClear => {
            BrowserWeatherProofLedgerStatus::CompletePendingExternalValidation
        }
    };
    controller.publish();
}
