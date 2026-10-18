//! A native child window, never an embedded playtest in the WebGPU canvas.
use forge_project::authoring::AuthoringState;
use leptos::prelude::*;
use std::cell::Cell;
use wasm_bindgen::JsValue;

thread_local! {
    static PAUSED: Cell<Option<RwSignal<bool>>> = const { Cell::new(None) };
    static DIAGNOSTICS: Cell<Option<RwSignal<Option<DiagnosticView>>>> = const { Cell::new(None) };
}

#[derive(Clone)]
struct DiagnosticView {
    summary: String,
    details: String,
    exit_success: Option<bool>,
    attention: bool,
}

#[derive(serde::Deserialize)]
struct DiagnosticWire {
    status: String,
    exit_success: bool,
    reason: Option<String>,
    report: Option<ReportWire>,
}

#[derive(serde::Deserialize)]
struct ReportWire {
    exit_success: bool,
    ready: bool,
    health: u32,
    stamina: u32,
    inventory_total: u64,
    quests: Vec<QuestWire>,
    feedback: String,
    last_command_error: Option<String>,
    simulation_error: Option<String>,
    resume_refused: Option<String>,
}

#[derive(serde::Deserialize)]
struct QuestWire {
    status: forge_gameplay::QuestStatus,
}

pub(super) fn initialize() {
    PAUSED.with(|slot| slot.set(Some(RwSignal::new(false))));
    DIAGNOSTICS.with(|slot| slot.set(Some(RwSignal::new(None))));
    on_cleanup(|| {
        PAUSED.with(|slot| slot.set(None));
        DIAGNOSTICS.with(|slot| slot.set(None));
    });
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

pub(crate) fn exported_game_opened() {
    set_diagnostics(None);
    set_paused(true);
}

pub(crate) fn exported_game_starting() {
    set_diagnostics(None);
}

pub(crate) async fn exported_game_refused() {
    match refresh_diagnostics().await {
        Ok(true) => (),
        Ok(false) => {
            diagnostic_unavailable("aucun bilan fourni après le refus de lancement");
        }
        Err(error) => diagnostic_unavailable(&error),
    }
}

fn diagnostic_view() -> Option<DiagnosticView> {
    DIAGNOSTICS.with(|slot| slot.get().and_then(|value| value.get()))
}

fn set_diagnostics(value: Option<DiagnosticView>) {
    DIAGNOSTICS.with(|slot| {
        if let Some(signal) = slot.get() {
            let _ = signal.try_set(value);
        }
    });
}

fn diagnostic_unavailable(reason: &str) {
    set_diagnostics(Some(DiagnosticView {
        summary: format!("Bilan du test indisponible : {reason}"),
        details: String::new(),
        exit_success: None,
        attention: true,
    }));
}

async fn refresh_diagnostics() -> Result<bool, String> {
    let value =
        crate::web::invoke_tauri_raw("native_game_diagnostics", js_sys::Object::new().as_ref())
            .await?;
    if value.is_null() {
        return Ok(false);
    }
    let value: serde_json::Value =
        serde_wasm_bindgen::from_value(value).map_err(|e| e.to_string())?;
    let wire: DiagnosticWire = serde_json::from_value(value.clone()).map_err(|e| e.to_string())?;
    let mut attention = !wire.exit_success || wire.status != "received";
    let summary = if wire.status == "received" {
        let report = wire.report.ok_or("Bilan reçu incomplet")?;
        attention |= !report.ready
            || report.last_command_error.is_some()
            || report.simulation_error.is_some()
            || report.resume_refused.is_some();
        let completed = report
            .quests
            .iter()
            .filter(|quest| quest.status == forge_gameplay::QuestStatus::Completed)
            .count();
        let mut summary = format!(
            "{} Vitalité {} · Endurance {} · Inventaire {} · Quêtes terminées {}/{}.",
            if report.exit_success {
                "Partie fermée."
            } else {
                "Partie arrêtée sur une erreur."
            },
            report.health,
            report.stamina,
            report.inventory_total,
            completed,
            report.quests.len(),
        );
        if !report.ready {
            summary.push_str(" La simulation n’était pas prête à la fermeture.");
        }
        for text in [&report.feedback]
            .into_iter()
            .chain(report.last_command_error.as_ref())
            .chain(report.simulation_error.as_ref())
            .chain(report.resume_refused.as_ref())
            .filter(|text| !text.is_empty())
        {
            summary.push(' ');
            summary.push_str(text);
        }
        summary
    } else if matches!(wire.status.as_str(), "absent" | "invalid") {
        format!(
            "{} {}",
            if wire.exit_success {
                "Le jeu est fermé."
            } else {
                "Le jeu s’est arrêté sur une erreur."
            },
            wire.reason
                .ok_or("Bilan absent ou refusé sans explication")?
        )
    } else {
        return Err("Statut de bilan inconnu".into());
    };
    let details = serde_json::to_string_pretty(&value).map_err(|e| e.to_string())?;
    set_diagnostics(Some(DiagnosticView {
        summary,
        details,
        exit_success: Some(wire.exit_success),
        attention,
    }));
    Ok(true)
}

#[component]
fn GameDiagnostics() -> impl IntoView {
    view! {
        <Show when=move || diagnostic_view().is_some()>
            <details class="authoring-native-game-diagnostics" open=move || diagnostic_view().is_some_and(|view| view.attention)>
                <summary aria-label="Bilan du dernier test" title="Bilan du dernier test">"Bilan"</summary>
                <div class="authoring-native-game-report">
                    <output aria-live="polite">{move || diagnostic_view().map(|view| view.summary)}</output>
                    <details>
                        <summary>"Détails de la session et de la sauvegarde"</summary>
                        <pre>{move || diagnostic_view().map(|view| view.details)}</pre>
                    </details>
                </div>
            </details>
        </Show>
    }
}

#[component]
pub(super) fn LaunchGame(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    preview: RwSignal<Option<forge_project::authoring::AuthoringRequest>>,
) -> impl IntoView {
    let launching = RwSignal::new(false);
    let launched = RwSignal::new(false);
    let status = RwSignal::new(String::new());
    view! {
        <div class="authoring-native-game" role="group" aria-label="Jeu séparé">
        <button class="authoring-launch-game" title="Jouer à la carte courante dans une fenêtre séparée"
            disabled=move || paused() || busy.get() || launching.get() || preview.get().is_some() || state.with(|s| s.as_ref().is_none_or(|s| s.zone_head.is_none()))
            on:click=move |_| {
                launched.set(false);
                let hash = state.with_untracked(|s| s.as_ref().and_then(|s| s.zone_head.as_ref()).ok_or("Ouvrir une carte de module").and_then(|head| head.content_hash().map_err(|_| "Identité de carte invalide")));
                let Ok(hash) = hash else { status.set("Ouvrir une carte de module avant de lancer le jeu.".into()); return; };
                set_diagnostics(None);
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
                            launched.set(true);
                        }
                        Err(error) => {
                            status.set(error);
                            match refresh_diagnostics().await {
                                Ok(true) => (),
                                Ok(false) => diagnostic_unavailable("aucun bilan fourni après le refus de lancement"),
                                Err(error) => diagnostic_unavailable(&error),
                            }
                        },
                    }
                    launching.set(false);
                });
            }>{move || if launching.get() { "Lancement…" } else { "Lancer le jeu" }}</button>
        <output aria-live="polite">{move || if launched.get() && !paused() {
            match diagnostic_view().and_then(|view| view.exit_success) {
                Some(true) => "Le jeu est fermé.".to_owned(),
                Some(false) => "Le jeu s’est arrêté sur une erreur.".to_owned(),
                None => "Le jeu est fermé ; son bilan est indisponible.".to_owned(),
            }
        } else { status.get() }}</output>
        <GameDiagnostics/>
        </div>
    }
}

#[component]
pub(super) fn PausedView() -> impl IntoView {
    let running = RwSignal::new(true);
    let failure = RwSignal::new(String::new());
    let polling = RwSignal::new(false);
    wasm_bindgen_futures::spawn_local(async {
        // A WebView reload does not terminate the native child. Restore its pause.
        if crate::web::invoke_tauri_raw("native_game_running", js_sys::Object::new().as_ref())
            .await
            .is_ok_and(|value| value.as_bool() == Some(true))
        {
            set_paused(true);
        }
        // Recover the last backend report after a UI remount, without resuming
        // or rebuilding the retained map.
        let _ = refresh_diagnostics().await;
    });
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
                        failure.set(String::new());
                        running.set(value);
                        if !value {
                            match refresh_diagnostics().await {
                                Ok(true) => (),
                                Ok(false) => {
                                    diagnostic_unavailable("aucun bilan de partie retourné");
                                }
                                Err(error) => diagnostic_unavailable(&error),
                            }
                            set_paused(false);
                        }
                    }
                }
                Err(error) => {
                    failure.set(error);
                    match refresh_diagnostics().await {
                        Ok(true) => running.set(false),
                        Ok(false) => {
                            // A polling error alone does not confirm that the
                            // backend's child has exited. Keep its last known state.
                            diagnostic_unavailable("la fermeture du jeu n’est pas confirmée");
                        }
                        Err(error) => diagnostic_unavailable(&error),
                    }
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
