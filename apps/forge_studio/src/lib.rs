#![deny(unsafe_code)]
#![cfg_attr(
    not(target_arch = "wasm32"),
    allow(
        linker_messages,
        reason = "MSVC reports its normal import-library creation message for the cdylib"
    )
)]

use forge_core::EntityId;
use serde::{Deserialize, Serialize};

#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
mod telemetry;

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(tag = "command", rename_all = "snake_case", deny_unknown_fields)]
pub enum StudioCommand {
    SelectEntity { entity_id: EntityId },
    SceneSummary,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(tag = "response", rename_all = "snake_case", deny_unknown_fields)]
pub enum StudioResponse {
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
    }
}

#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod web {
    use super::telemetry::{
        AllowedCommandProbeStatus, BrowserMemoryObservation, DeniedCommandProbeStatus,
        FRAME_MEASURED_SAMPLES, FRAME_WARMUP_SAMPLES, FrameDistribution, Gate0ProbeMode,
        Gate0StudioLedger, RuntimeTelemetryEvent, StudioTelemetryTarget, TAURI_DENIED_COMMAND,
        ViewportObservation, ViewportReadyEvent, WebGpuProbeStatus, is_explicit_acl_denial,
        parse_gate0_probe,
    };
    use super::{EntityId, StudioCommand, StudioResponse, validate_selection_response};
    use bevy::{
        prelude::*,
        render::{
            RenderPlugin,
            settings::{RenderCreation, WgpuSettings},
        },
        window::{PresentMode, WindowResolution},
    };
    use forge_engine::{ProofScenePlugin, ProofSelection};
    use forge_project::SceneProofKind;
    use forge_world::embedded_gate0_proof_document;
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
    const RUNTIME_TELEMETRY_EVENT: &str = "forge-gate0-runtime-telemetry";

    thread_local! {
        static INITIAL_CANVAS: RefCell<Option<web_sys::Element>> = const { RefCell::new(None) };
    }

    #[derive(Clone, Debug, Eq, PartialEq)]
    enum WebGpuStatus {
        Checking,
        Ready,
        Unavailable(String),
    }

    #[wasm_bindgen(start)]
    pub fn start() {
        console_error_panic_hook::set_once();
        mount_to_body(|| view! { <Studio/> });
    }

    #[component]
    fn Studio() -> impl IntoView {
        let probe_mode = current_probe_mode();
        let document =
            embedded_gate0_proof_document().expect("embedded Gate 0 JSON must remain valid");
        let scene_name = document.name.clone();
        let schema_version = document.schema_version;
        let initial_selection = document
            .entities
            .first()
            .expect("validated Gate 0 document must contain entities")
            .id;
        let hierarchy = document.entities;

        let (selected, set_selected) = signal(initial_selection);
        let (rerenders, set_rerenders) = signal(0_u32);
        let (ipc_status, set_ipc_status) = signal("Aucune commande".to_owned());
        let (webgpu_status, set_webgpu_status) = signal(WebGpuStatus::Checking);
        let (telemetry, set_telemetry) = signal(Gate0StudioLedger::new(
            studio_telemetry_target(),
            performance_now(),
        ));

        install_viewport_selection_listener(
            set_selected,
            set_rerenders,
            set_ipc_status,
            set_telemetry,
        );
        install_runtime_telemetry_listener(set_telemetry, set_webgpu_status);

        if probe_mode == Gate0ProbeMode::TauriPermission {
            wasm_bindgen_futures::spawn_local(run_tauri_permission_probe(set_telemetry));
        }

        wasm_bindgen_futures::spawn_local(async move {
            match preflight_webgpu(probe_mode).await {
                Ok(()) => {
                    start_viewport_once();
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
            <main class="studio-shell">
                <header class="topbar">
                    <div>
                        <span class="eyebrow">"OPENRPG FORGE · RUST"</span>
                        <h1>"Visual Workspace"</h1>
                    </div>
                    <div class="status-stack">
                        <span
                            class:status-ok=move || matches!(webgpu_status.get(), WebGpuStatus::Ready)
                            class:status-error=move || matches!(webgpu_status.get(), WebGpuStatus::Unavailable(_))
                        >
                            {move || match webgpu_status.get() {
                                WebGpuStatus::Checking => "Vérification WebGPU…",
                                WebGpuStatus::Ready => "WebGPU prêt",
                                WebGpuStatus::Unavailable(_) => "WebGPU indisponible",
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
                            let glyph = entity_kind_glyph(&entity.kind);
                            view! {
                                <button
                                    class:selected=move || selected.get() == entity_id
                                    on:click=move |_| {
                                        let interaction_started_ms = performance_now();
                                        set_selected.set(entity_id);
                                        LeptosUpdate::update(&set_rerenders, |value| *value += 1);
                                        dispatch_selection(entity_id, set_ipc_status);
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
                            <span>"Perspective"</span><span>"1920 × 1080"</span>
                        </div>
                        <canvas id="forge-canvas" data-testid="forge-webgpu-canvas"></canvas>
                        {move || match webgpu_status.get() {
                            WebGpuStatus::Unavailable(reason) => Some(view! {
                                <div class="webgpu-error" role="alert" data-testid="webgpu-unavailable">
                                    <strong>"WebGPU n’a pas pu initialiser un périphérique."</strong>
                                    <span>{reason}</span>
                                    <span>"Vérifiez l’accélération graphique, le pilote et la version de Chrome, Edge ou WebView2, puis relancez le Studio."</span>
                                </div>
                            }),
                            WebGpuStatus::Checking | WebGpuStatus::Ready => None,
                        }}
                    </section>
                    <aside class="panel inspector" aria-label="Inspecteur">
                        <h2>"Inspecteur"</h2>
                        <label>"EntityId"<output>{move || selected.get().to_string()}</output></label>
                        <label>"Scène"<output>{scene_name}</output></label>
                        <label>"Source"<output>"SceneProofDocument v" {schema_version}</output></label>
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
                    </aside>
                </section>
            </main>
        }
    }

    fn entity_kind_glyph(kind: &SceneProofKind) -> &'static str {
        match kind {
            SceneProofKind::Ground { .. } => "▱",
            SceneProofKind::Cube { .. } => "◈",
            SceneProofKind::DirectionalLight { .. } => "☀",
            SceneProofKind::Camera { .. } => "◉",
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
        webgpu_status: WriteSignal<WebGpuStatus>,
    ) {
        let ready_listener =
            Closure::<dyn FnMut(web_sys::CustomEvent)>::new(move |event: web_sys::CustomEvent| {
                let Some(encoded) = event.detail().as_string() else {
                    web_sys::console::error_1(&JsValue::from_str(
                        "Gate 0 viewport-ready event did not contain JSON text",
                    ));
                    return;
                };
                let ready = match serde_json::from_str::<ViewportReadyEvent>(&encoded) {
                    Ok(ready) => ready,
                    Err(error) => {
                        web_sys::console::error_1(&JsValue::from_str(&format!(
                            "Gate 0 viewport-ready event was invalid: {error}"
                        )));
                        return;
                    }
                };
                webgpu_status.set(WebGpuStatus::Ready);
                LeptosUpdate::update(&telemetry, |ledger| {
                    ledger.recorded_at_utc = Some(ready.recorded_at_utc);
                    ledger.webgpu.status = WebGpuProbeStatus::Ready;
                    ledger.loading.webgpu_ready_ms = Some(ready.first_real_frame_ms);
                    ledger.loading.scene_ready_ms = Some(ready.first_real_frame_ms);
                    ledger.viewport = ready.viewport;
                });
            });
        let frame_listener =
            Closure::<dyn FnMut(web_sys::CustomEvent)>::new(move |event: web_sys::CustomEvent| {
                let Some(encoded) = event.detail().as_string() else {
                    web_sys::console::error_1(&JsValue::from_str(
                        "Gate 0 telemetry event did not contain JSON text",
                    ));
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
                    ledger.loading.scene_ready_ms = Some(runtime.first_real_frame_ms);
                    ledger.memory = runtime.memory;
                    ledger.frame = Some(runtime.frame);
                    ledger.viewport = runtime.viewport;
                });
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
                    RUNTIME_TELEMETRY_EVENT,
                    frame_listener.as_ref().unchecked_ref(),
                )
                .is_ok()
            {
                frame_listener.forget();
            }
        }
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
        let document = embedded_gate0_proof_document();
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

    fn start_viewport_once() {
        if !VIEWPORT_LAUNCH_REQUESTED.swap(true, Ordering::SeqCst) {
            INITIAL_CANVAS.with(|initial| initial.replace(current_canvas()));
            start_viewport();
        }
    }

    fn start_viewport() {
        let document =
            embedded_gate0_proof_document().expect("embedded Gate 0 JSON must remain valid");
        let render_plugin = RenderPlugin {
            render_creation: RenderCreation::Automatic(Box::new(WgpuSettings {
                power_preference: bevy::render::settings::PowerPreference::HighPerformance,
                ..default()
            })),
            ..default()
        };
        App::new()
            .insert_resource(ClearColor(Color::srgb(0.015, 0.025, 0.055)))
            .insert_non_send(BrowserFrameCollector::new())
            .add_plugins(
                DefaultPlugins
                    .set(WindowPlugin {
                        primary_window: Some(Window {
                            title: "OpenRPG Forge WebGPU".to_owned(),
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
            )
            .add_plugins(ProofScenePlugin::new(document))
            .add_systems(
                Update,
                (publish_viewport_selection, collect_browser_frame_telemetry),
            )
            .run();
    }

    struct BrowserFrameCollector {
        first_real_frame_ms: Option<f64>,
        ready_reported: bool,
        warmup_samples: usize,
        samples_ms: Vec<f64>,
        metrics_reported: bool,
    }

    impl BrowserFrameCollector {
        fn new() -> Self {
            Self {
                first_real_frame_ms: None,
                ready_reported: false,
                warmup_samples: 0,
                samples_ms: Vec::with_capacity(FRAME_MEASURED_SAMPLES),
                metrics_reported: false,
            }
        }
    }

    #[allow(
        clippy::needless_pass_by_value,
        reason = "Bevy systems require owned system parameter wrappers"
    )]
    fn collect_browser_frame_telemetry(
        time: Res<Time<bevy::time::Real>>,
        mut collector: NonSendMut<BrowserFrameCollector>,
    ) {
        if collector.metrics_reported {
            return;
        }

        let delta_ms = time.delta_secs_f64() * 1_000.0;
        if !delta_ms.is_finite() || delta_ms <= 0.0 {
            return;
        }
        if !collector.ready_reported {
            let first_real_frame_ms = performance_now();
            collector.first_real_frame_ms = Some(first_real_frame_ms);
            VIEWPORT_STARTS.fetch_add(1, Ordering::SeqCst);
            let ready = ViewportReadyEvent {
                recorded_at_utc: recorded_at_utc(),
                first_real_frame_ms,
                viewport: current_viewport_observation(),
            };
            match serde_json::to_string(&ready) {
                Ok(encoded) => dispatch_json_event(VIEWPORT_READY_EVENT, &encoded),
                Err(error) => web_sys::console::error_1(&JsValue::from_str(&format!(
                    "Gate 0 viewport-ready serialization failed: {error}"
                ))),
            }
            collector.ready_reported = true;
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
}
