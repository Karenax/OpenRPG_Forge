//! Studio diagnostic panel for the synthetic Aurora M5 scenario.
//!
//! The panel is deliberately read-only: it calls the native
//! `run_aurora_gameplay` command and renders the deterministic report. It does
//! not import, migrate or write any NWN source.

use leptos::prelude::*;
use wasm_bindgen::JsValue;
use wasm_bindgen_futures::spawn_local;

#[derive(Clone, Debug, PartialEq, Eq)]
struct GameplaySummary {
    module: String,
    seed: u64,
    winner: String,
    rounds: u32,
    trace_entries: usize,
    save_sha256: String,
    migration: String,
    resume_idempotent: bool,
    oracle_status: String,
    unsupported: usize,
}

impl GameplaySummary {
    fn from_json(value: &serde_json::Value) -> Result<Self, String> {
        let text = |key: &str| {
            value
                .get(key)
                .and_then(serde_json::Value::as_str)
                .map(str::to_owned)
                .ok_or_else(|| format!("champ {key} absent"))
        };
        let number = |key: &str| {
            value
                .get(key)
                .and_then(serde_json::Value::as_u64)
                .ok_or_else(|| format!("champ {key} absent"))
        };
        Ok(Self {
            module: text("module")?,
            seed: number("seed")?,
            winner: value
                .get("winner")
                .and_then(serde_json::Value::as_str)
                .unwrap_or("aucun")
                .to_owned(),
            rounds: u32::try_from(number("rounds")?)
                .map_err(|_| "rounds hors bornes".to_owned())?,
            trace_entries: usize::try_from(number("traceEntries")?)
                .map_err(|_| "traceEntries hors bornes".to_owned())?,
            save_sha256: text("saveSha256")?,
            migration: format!(
                "accepted={} restored={}",
                value
                    .get("migrationAccepted")
                    .and_then(serde_json::Value::as_bool)
                    .unwrap_or(false),
                value
                    .get("migrationRestored")
                    .and_then(serde_json::Value::as_bool)
                    .unwrap_or(false)
            ),
            resume_idempotent: value
                .get("resumeIdempotent")
                .and_then(serde_json::Value::as_bool)
                .unwrap_or(false),
            oracle_status: text("oracleStatus")?,
            unsupported: value
                .get("unsupported")
                .and_then(serde_json::Value::as_array)
                .map_or(0, Vec::len),
        })
    }

    fn lines(&self) -> Vec<String> {
        vec![
            format!("Module : {}", self.module),
            format!("Graine : {}", self.seed),
            format!("Vainqueur : {}", self.winner),
            format!("Rounds : {}", self.rounds),
            format!("Entrées de trace : {}", self.trace_entries),
            format!("Sauvegarde : {}", self.save_sha256),
            format!("Migration M4 → M5 : {}", self.migration),
            format!("Reprise idempotente : {}", self.resume_idempotent),
            format!("Oracle NWN : {}", self.oracle_status),
            format!("Diagnostics non supportés : {}", self.unsupported),
        ]
    }
}

async fn run_gameplay(bundle: &str, seed: u64) -> Result<GameplaySummary, String> {
    let arguments = js_sys::Object::new();
    js_sys::Reflect::set(
        &arguments,
        &JsValue::from_str("bundle"),
        &JsValue::from_str(bundle),
    )
    .map_err(|error| format!("argument bundle refusé : {error:?}"))?;
    js_sys::Reflect::set(
        &arguments,
        &JsValue::from_str("seed"),
        &JsValue::from_f64(seed as f64),
    )
    .map_err(|error| format!("argument seed refusé : {error:?}"))?;
    let response = crate::web::invoke_tauri_raw("run_aurora_gameplay", arguments.as_ref()).await?;
    let value: serde_json::Value = serde_wasm_bindgen::from_value(response)
        .map_err(|error| format!("réponse Aurora invalide : {error}"))?;
    GameplaySummary::from_json(&value)
}

#[component]
pub fn AuroraM5Panel() -> impl IntoView {
    let (bundle, set_bundle) = signal(String::new());
    let (seed, set_seed) = signal("2026".to_owned());
    let (status, set_status) = signal("Aucun scénario exécuté".to_owned());
    let (summary, set_summary) = signal(None::<GameplaySummary>);

    let run = move |_| {
        let path = bundle.get();
        let seed_text = seed.get();
        if path.trim().is_empty() {
            set_status.set("Indiquez le dossier du bundle Aurora.".to_owned());
            return;
        }
        let Ok(seed) = seed_text.trim().parse::<u64>() else {
            set_status.set("La graine doit être un entier positif.".to_owned());
            return;
        };
        set_status.set("Scénario en cours…".to_owned());
        set_summary.set(None);
        spawn_local(async move {
            match run_gameplay(&path, seed).await {
                Ok(value) => {
                    set_status
                        .set("Scénario terminé ; diagnostic synthétique uniquement.".to_owned());
                    set_summary.set(Some(value));
                }
                Err(error) => set_status.set(format!("Échec : {error}")),
            }
        });
    };

    view! {
        <details class="scene-navigation" id="studio-aurora-m5">
            <summary>"Diagnostic Aurora M5"</summary>
            <p>
                "Exécute le scénario de compatibilité synthétique et affiche sa trace, sa sauvegarde et ses limites. Aucun module NWN n'est importé ni modifié."
            </p>
            <label>
                "Bundle Aurora "
                <input
                    type="text"
                    placeholder="chemin du bundle .module-migration-v1"
                    prop:value=move || bundle.get()
                    on:input=move |event| set_bundle.set(event_target_value(&event))
                />
            </label>
            <label>
                "Graine "
                <input
                    type="text"
                    inputmode="numeric"
                    prop:value=move || seed.get()
                    on:input=move |event| set_seed.set(event_target_value(&event))
                />
            </label>
            <button type="button" on:click=run>"Exécuter le scénario"</button>
            <output id="studio-aurora-m5-status">{move || status.get()}</output>
            {move || {
                summary
                    .get()
                    .map(|value| {
                        view! {
                            <ul id="studio-aurora-m5-summary">
                                {value.lines().into_iter().map(|line| view! { <li>{line}</li> }).collect_view()}
                            </ul>
                        }
                    })
            }}
        </details>
    }
}
