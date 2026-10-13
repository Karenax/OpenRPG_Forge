//! A native child window, never an embedded playtest in the WebGPU canvas.
use forge_project::authoring::AuthoringState;
use leptos::prelude::*;
use std::cell::Cell;
use wasm_bindgen::JsValue;

thread_local! {
    static PAUSED: Cell<Option<RwSignal<bool>>> = const { Cell::new(None) };
}

pub(super) fn initialize() {
    PAUSED.with(|slot| slot.set(Some(RwSignal::new(false))));
    on_cleanup(|| PAUSED.with(|slot| slot.set(None)));
}

pub(super) fn paused() -> bool {
    PAUSED.with(|slot| slot.get().is_some_and(|value| value.get()))
}

fn set_paused(value: bool) {
    PAUSED.with(|slot| {
        if let Some(signal) = slot.get() {
            signal.set(value);
        }
    });
}

#[component]
pub(super) fn LaunchGame(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    preview: RwSignal<Option<forge_project::authoring::AuthoringRequest>>,
) -> impl IntoView {
    let launching = RwSignal::new(false);
    let status = RwSignal::new(String::new());
    view! {
        <button class="authoring-launch-game" title="Jouer à la carte courante dans une fenêtre séparée"
            disabled=move || paused() || busy.get() || launching.get() || preview.get().is_some() || state.with(|s| s.as_ref().is_none_or(|s| s.zone_head.is_none()))
            on:click=move |_| {
                let hash = state.with_untracked(|s| s.as_ref().and_then(|s| s.zone_head.as_ref()).ok_or("Ouvrir une carte de module").and_then(|head| head.content_hash().map_err(|_| "Identité de carte invalide")));
                let Ok(hash) = hash else { status.set("Ouvrir une carte de module avant de lancer le jeu.".into()); return; };
                launching.set(true);
                status.set("Préparation de la carte pour le jeu…".into());
                wasm_bindgen_futures::spawn_local(async move {
                    let arguments = js_sys::Object::new();
                    let result = match js_sys::Reflect::set(&arguments, &JsValue::from_str("expectedHash"), &JsValue::from_str(&hash.to_string())) {
                        Ok(_) => crate::web::invoke_tauri_raw("launch_native_game", arguments.as_ref()).await,
                        Err(_) => Err("Requête de lancement indisponible".into()),
                    };
                    match result {
                        Ok(_) => {
                            status.set("Le jeu est ouvert dans une fenêtre séparée.".into());
                            set_paused(true);
                        }
                        Err(error) => status.set(error),
                    }
                    launching.set(false);
                });
            }>{move || if launching.get() { "Lancement…" } else { "Lancer le jeu" }}</button>
        <output aria-live="polite">{move || status.get()}</output>
    }
}

#[component]
pub(super) fn PausedView() -> impl IntoView {
    let running = RwSignal::new(true);
    let failure = RwSignal::new(String::new());
    let polling = RwSignal::new(false);
    let poll = move || {
        if !paused() || polling.get_untracked() {
            return;
        }
        polling.set(true);
        wasm_bindgen_futures::spawn_local(async move {
            match crate::web::invoke_tauri_raw(
                "native_game_running",
                js_sys::Object::new().as_ref(),
            )
            .await
            {
                Ok(value) => {
                    if let Some(value) = value.as_bool() {
                        running.set(value);
                        if !value {
                            set_paused(false);
                        }
                    }
                }
                Err(error) => {
                    running.set(false);
                    failure.set(error);
                }
            }
            polling.set(false);
        });
    };
    Effect::new(move |_| {
        if paused() {
            running.set(true);
            failure.set(String::new());
            poll();
        }
    });
    if let Ok(handle) = leptos::leptos_dom::helpers::set_interval_with_handle(
        poll,
        std::time::Duration::from_secs(1),
    ) {
        on_cleanup(move || handle.clear());
    }
    view! {
        <Show when=paused>
            <div class="forge-map-loading" aria-busy="false">
                <strong>"Le jeu utilise sa propre fenêtre"</strong>
                <span>"La carte et la caméra restent chargées. Le rendu de l’éditeur est en pause pendant la partie."</span>
                <span>{move || if running.get() { "Fermez la fenêtre du jeu pour reprendre l’édition." } else { "Le jeu est fermé. Vous pouvez reprendre l’édition." }}</span>
                <output role="status" aria-live="polite">{move || failure.get()}</output>
                <button disabled=move || running.get() on:click=move |_| set_paused(false)>"Revenir à la carte"</button>
            </div>
        </Show>
    }
}
