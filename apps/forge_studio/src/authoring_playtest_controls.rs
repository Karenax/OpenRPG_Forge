//! Persistent zone start and explicit temporary launch overrides use separate commands.
use forge_project::authoring::{AuthoringRequest, AuthoringState, playtest::PlaytestStart};
use leptos::prelude::*;

#[component]
pub(super) fn PlaytestActions(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    send: Callback<Result<AuthoringRequest, String>>,
) -> impl IntoView {
    view! {
        <div class="authoring-play-actions" role="group" aria-label="Tester la zone">
            <button class="authoring-play" disabled=move || busy.get() || state.get().is_none_or(|s| s.document.is_none() || s.playtest_hash.is_some()) on:click=move |_| send.run(super::authoring_controls::prepare_playtest(state.get_untracked()))>"Jouer"</button>
            <button disabled=move || busy.get() || state.get().is_none_or(|s| s.playtest_hash.is_none()) on:click=move |_| send.run(stop(state.get_untracked()))>"Retour à l’édition"</button>
        </div>
    }
}

#[component]
pub(super) fn PlaytestControls(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    send: Callback<Result<AuthoringRequest, String>>,
) -> impl IntoView {
    let x = RwSignal::new("0".to_owned());
    let z = RwSignal::new("0".to_owned());
    let facing = RwSignal::new("0".to_owned());
    let pick_message = RwSignal::new(String::new());
    let zone = Memo::new(move |_| {
        state.with(|s| {
            s.as_ref()
                .and_then(|s| s.document.as_ref())
                .map(|document| document.area_id)
        })
    });
    Effect::new(move |_| {
        let _ = zone.get();
        let start = state.with_untracked(|s| {
            s.as_ref()
                .and_then(|s| s.zone_head.as_ref())
                .and_then(|h| h.corrections.start.as_deref())
                .copied()
        });
        x.set(
            start
                .as_ref()
                .map_or_else(|| "0".into(), |p| p.x_m.to_string()),
        );
        z.set(
            start
                .as_ref()
                .map_or_else(|| "0".into(), |p| p.z_m.to_string()),
        );
        facing.set(
            start
                .and_then(|p| p.facing_degrees)
                .map_or_else(String::new, |angle| angle.to_string()),
        );
        pick_message.set(String::new());
    });
    let disabled = move || {
        busy.get()
            || state
                .get()
                .is_none_or(|s| s.document.is_none() || s.playtest_hash.is_some())
    };
    view! {
        <div class="authoring-playtest-settings" style="display:grid;gap:0.5rem">
            <Show when=move || state.get().and_then(|s| s.last_playtest_diagnostics).is_some()>
                <details><summary>"Bilan du dernier test"</summary>
                <output aria-live="polite">{move || state.get().and_then(|s| s.last_playtest_diagnostics).map(|report| format!(
                    "Dernier playtest · tick {} · position finale ({:.2}, {:.2}, {:.2}) m · au sol {} · simulation prête {} · {} contact(s) · {} · {}",
                    report.fixed_tick,
                    report.final_position_m[0], report.final_position_m[1], report.final_position_m[2],
                    report.grounded, report.simulation_ready, report.collisions,
                    report.end_reason, report.runtime_status,
                ))}</output>
                </details>
            </Show>
            <details style="grid-column:1/-1">
                <summary>"Choisir le départ du playtest"</summary>
                <output>{move || state.get().and_then(|s| s.zone_head).and_then(|h| h.corrections.start)
                    .map_or_else(|| "Aucun départ enregistré : Jouer utilise le départ par défaut.".into(), |p| format!("Départ de zone accepté : {}", super::authoring_start_label::describe(Some(&p))))}</output>
                <label>"Départ X (m)"<input type="number" step="0.1" disabled=disabled prop:value=move || x.get() on:input=move |e| { x.set(event_target_value(&e)); super::authoring_shortcuts::cancel_preview(); }/></label>
                <label>"Départ Z (m)"<input type="number" step="0.1" disabled=disabled prop:value=move || z.get() on:input=move |e| { z.set(event_target_value(&e)); super::authoring_shortcuts::cancel_preview(); }/></label>
                <button disabled=disabled on:click=move |_| send.run(prepare(state.get_untracked(), &x.get_untracked(), &z.get_untracked()))>"Jouer à cet endroit"</button>
                <label>"Cap du départ enregistré (°)"<input type="number" step="1" disabled=disabled prop:value=move || facing.get() on:input=move |e| { facing.set(event_target_value(&e)); super::authoring_shortcuts::cancel_preview(); }/></label>
                <button disabled=disabled on:click=move |_| {
                    if let Err(error) = super::authoring_start_pick::arm(state, busy, x, z, facing, pick_message) {
                        pick_message.set(error);
                    }
                }>"Choisir le départ dans la vue"</button>
                <button disabled=disabled on:click=move |_| {
                    if let Some(start) = state.get_untracked().and_then(|s| s.zone_head).and_then(|h| h.corrections.start) {
                        x.set(start.x_m.to_string()); z.set(start.z_m.to_string());
                        facing.set(start.facing_degrees.map(|value| value.to_string()).unwrap_or_default());
                        super::authoring_shortcuts::cancel_preview();
                        pick_message.set("Valeurs acceptées recopiées dans le brouillon ; aucune mutation.".into());
                    } else { pick_message.set("Aucun départ de zone à reprendre.".into()); }
                }>"Reprendre les valeurs du départ accepté"</button>
                <output aria-live="polite">{move || pick_message.get()}</output>
                <button disabled=disabled on:click=move |_| send.run(prepare_saved(state.get_untracked(), Some((&x.get_untracked(), &z.get_untracked(), &facing.get_untracked()))))>"Examiner ce départ pour la zone"</button>
                <button disabled=disabled on:click=move |_| send.run(prepare_saved(state.get_untracked(), None))>"Examiner le retrait du départ enregistré"</button>
                <p>"Départ temporaire sur le terrain. Aucun objet déplacé ni point sauvegardé. Choisir un espace libre, hors des objets."</p>
                <p>"Examiner puis Appliquer modifie le départ de zone ; Sauvegarder le conserve après fermeture. Jouer à cet endroit reste temporaire. Le point est projeté sur le terrain, pas sur un étage de bâtiment."</p>
            </details>
        </div>
    }
}

fn stop(state: Option<AuthoringState>) -> Result<AuthoringRequest, String> {
    let state = state.ok_or("Aucune zone")?;
    let identity = state.playtest_identity.ok_or("Playtest non initialisé")?;
    let diagnostics = super::authoring_input::exit_diagnostics(identity)
        .ok_or("Aucun bilan runtime disponible : attendre l’initialisation du playtest")?;
    Ok(AuthoringRequest::StopPlaytest {
        diagnostics: Some(diagnostics),
    })
}

fn prepare_saved(
    state: Option<AuthoringState>,
    fields: Option<(&str, &str, &str)>,
) -> Result<AuthoringRequest, String> {
    let state = state.ok_or("Aucune zone")?;
    if state.playtest_hash.is_some() {
        return Err("Revenir en édition".into());
    }
    let head = state.zone_head.ok_or("Une zone agrégée est requise")?;
    let start = fields
        .map(|(x, z, facing)| -> Result<PlaytestStart, String> {
            Ok(PlaytestStart {
                x_m: x.trim().parse().map_err(|_| "Coordonnée X invalide")?,
                z_m: z.trim().parse().map_err(|_| "Coordonnée Z invalide")?,
                facing_degrees: if facing.trim().is_empty() {
                    None
                } else {
                    Some(facing.trim().parse().map_err(|_| "Cap invalide")?)
                },
            })
        })
        .transpose()?;
    let identity =
        serde_json::to_string(&(head.content_hash()?, start)).map_err(|e| e.to_string())?;
    head.prepare_start(start, forge_core::CommandId::deterministic(identity))
        .map(|request| AuthoringRequest::PreviewZone { request })
}

fn prepare(state: Option<AuthoringState>, x: &str, z: &str) -> Result<AuthoringRequest, String> {
    let start = PlaytestStart {
        x_m: x.parse().map_err(|_| "Coordonnée X invalide")?,
        z_m: z.parse().map_err(|_| "Coordonnée Z invalide")?,
        facing_degrees: None,
    };
    prepare_at(state, start)
}

pub(super) fn prepare_at(
    state: Option<AuthoringState>,
    start: PlaytestStart,
) -> Result<AuthoringRequest, String> {
    if state.as_ref().is_some_and(|s| s.playtest_hash.is_some()) {
        return Err("Revenir en édition avant de choisir un autre départ".into());
    }
    let document = state
        .as_ref()
        .and_then(|s| s.document.as_ref())
        .ok_or("Aucune zone")?;
    start.validate(document)?;
    let AuthoringRequest::StartPlaytest { expected_hash } =
        super::authoring_controls::prepare_playtest(state)?
    else {
        return Err("Préparation du playtest incohérente".into());
    };
    Ok(AuthoringRequest::StartPlaytestAt {
        expected_hash,
        start,
    })
}
