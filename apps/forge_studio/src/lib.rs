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
    use std::sync::atomic::{AtomicBool, AtomicUsize, Ordering};
    use wasm_bindgen::{JsCast, prelude::*};

    static VIEWPORT_STARTED: AtomicBool = AtomicBool::new(false);
    static VIEWPORT_STARTS: AtomicUsize = AtomicUsize::new(0);
    const VIEWPORT_SELECTION_EVENT: &str = "forge-proof-selection";

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

        install_viewport_selection_listener(set_selected, set_rerenders, set_ipc_status);

        wasm_bindgen_futures::spawn_local(async move {
            match preflight_webgpu().await {
                Ok(()) => {
                    start_viewport_once();
                    set_webgpu_status.set(WebGpuStatus::Ready);
                }
                Err(reason) => set_webgpu_status.set(WebGpuStatus::Unavailable(reason)),
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
                                        set_selected.set(entity_id);
                                        LeptosUpdate::update(&set_rerenders, |value| *value += 1);
                                        dispatch_selection(entity_id, set_ipc_status);
                                    }
                                >{glyph} " " {entity_name}</button>
                            }
                        }).collect_view()}
                    </aside>
                    <section class="viewport-panel" aria-label="Viewport 3D">
                        <div class="viewport-toolbar">
                            <span>"Perspective"</span><span>"1920 × 1080"</span>
                        </div>
                        <canvas id="forge-canvas"></canvas>
                        {move || match webgpu_status.get() {
                            WebGpuStatus::Unavailable(reason) => Some(view! {
                                <div class="webgpu-error" role="alert">
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
                        <p class="hint">"La sélection change l’UI sans reconstruire le monde Bevy."</p>
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
    ) {
        let listener =
            Closure::<dyn FnMut(web_sys::CustomEvent)>::new(move |event: web_sys::CustomEvent| {
                let Some(value) = event.detail().as_string() else {
                    ipc_status.set("Sélection 3D invalide : EntityId absent".to_owned());
                    return;
                };
                match value.parse::<EntityId>() {
                    Ok(entity_id) => {
                        selected.set(entity_id);
                        LeptosUpdate::update(&rerenders, |count| *count += 1);
                        dispatch_selection(entity_id, ipc_status);
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

    async fn invoke_tauri(command: &StudioCommand) -> Result<StudioResponse, String> {
        let window = web_sys::window().ok_or_else(|| "Fenêtre navigateur absente".to_owned())?;
        let tauri = js_sys::Reflect::get(window.as_ref(), &JsValue::from_str("__TAURI__"))
            .map_err(|error| format!("API Tauri inaccessible : {}", js_error(&error)))?;
        if tauri.is_null() || tauri.is_undefined() {
            return Err("Mode navigateur autonome : backend Tauri absent".to_owned());
        }
        let core = js_sys::Reflect::get(&tauri, &JsValue::from_str("core"))
            .map_err(|error| format!("API Tauri core inaccessible : {}", js_error(&error)))?;
        let invoke = js_sys::Reflect::get(&core, &JsValue::from_str("invoke"))
            .map_err(|error| format!("API Tauri invoke inaccessible : {}", js_error(&error)))?
            .dyn_into::<js_sys::Function>()
            .map_err(|_| "API Tauri invoke invalide".to_owned())?;
        let arguments = js_sys::Object::new();
        let payload = serde_wasm_bindgen::to_value(command)
            .map_err(|error| format!("Commande Tauri non sérialisable : {error}"))?;
        js_sys::Reflect::set(&arguments, &JsValue::from_str("command"), &payload)
            .map_err(|error| format!("Arguments Tauri invalides : {}", js_error(&error)))?;
        let result = invoke
            .call2(
                &core,
                &JsValue::from_str("execute_studio_command"),
                &arguments,
            )
            .map_err(|error| format!("Invocation Tauri impossible : {}", js_error(&error)))?;
        let promise = result
            .dyn_into::<js_sys::Promise>()
            .map_err(|_| "Tauri invoke n’a pas retourné de Promise".to_owned())?;
        let response = wasm_bindgen_futures::JsFuture::from(promise)
            .await
            .map_err(|error| format!("Commande Tauri échouée : {}", js_error(&error)))?;
        serde_wasm_bindgen::from_value(response)
            .map_err(|error| format!("Réponse Tauri invalide : {error}"))
    }

    async fn preflight_webgpu() -> Result<(), String> {
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

        let request_device = js_sys::Reflect::get(&adapter, &JsValue::from_str("requestDevice"))
            .map_err(|error| format!("requestDevice inaccessible : {}", js_error(&error)))?
            .dyn_into::<js_sys::Function>()
            .map_err(|_| "adapter.requestDevice n’est pas appelable".to_owned())?;
        let device_promise = request_device
            .call0(&adapter)
            .map_err(|error| format!("requestDevice a levé une erreur : {}", js_error(&error)))?
            .dyn_into::<js_sys::Promise>()
            .map_err(|_| "requestDevice n’a pas retourné de Promise".to_owned())?;
        let device = wasm_bindgen_futures::JsFuture::from(device_promise)
            .await
            .map_err(|error| {
                format!(
                    "Création du périphérique WebGPU impossible : {}",
                    js_error(&error)
                )
            })?;
        if device.is_null() || device.is_undefined() {
            return Err("adapter.requestDevice n’a retourné aucun périphérique".to_owned());
        }

        Ok(())
    }

    fn js_error(value: &JsValue) -> String {
        value.as_string().unwrap_or_else(|| format!("{value:?}"))
    }

    fn start_viewport_once() {
        if !VIEWPORT_STARTED.swap(true, Ordering::SeqCst) {
            VIEWPORT_STARTS.fetch_add(1, Ordering::SeqCst);
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
            .add_systems(Update, publish_viewport_selection)
            .run();
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
