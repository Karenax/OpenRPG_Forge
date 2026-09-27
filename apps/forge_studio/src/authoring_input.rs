//! Input belongs to the playtest canvas only while it owns pointer capture.
use bevy::prelude::*;
use forge_engine::PlayerInputFocus;
use forge_project::authoring::playtest::{PlaytestExitDiagnostics, PlaytestIdentity};
use leptos::prelude::*;
use std::fmt::Write as _;

thread_local! {
    static PLAYING: std::cell::Cell<bool> = const { std::cell::Cell::new(false) };
    static METRICS: std::cell::Cell<bool> = const { std::cell::Cell::new(false) };
    static OUTPUT: std::cell::RefCell<Option<WriteSignal<String>>> = const { std::cell::RefCell::new(None) };
    static LAST_RUNTIME: std::cell::RefCell<Option<RuntimeObservation>> = const { std::cell::RefCell::new(None) };
}

#[derive(Clone)]
struct RuntimeObservation {
    fixed_tick: u64,
    final_position_m: [f32; 3],
    grounded: bool,
    collisions: usize,
    simulation_ready: bool,
    runtime_status: String,
}

pub(super) fn exit_diagnostics(identity: PlaytestIdentity) -> Option<PlaytestExitDiagnostics> {
    LAST_RUNTIME.with(|slot| {
        slot.borrow()
            .as_ref()
            .map(|runtime| PlaytestExitDiagnostics {
                identity,
                fixed_tick: runtime.fixed_tick,
                final_position_m: runtime.final_position_m,
                grounded: runtime.grounded,
                collisions: runtime.collisions,
                simulation_ready: runtime.simulation_ready,
                runtime_status: runtime.runtime_status.clone(),
                end_reason: "retour_edition".into(),
            })
    })
}

#[component]
pub(super) fn PlaytestStatus() -> impl IntoView {
    let metrics = RwSignal::new(false);
    let (status, output) = signal("Mode édition · aucune simulation joueur".to_owned());
    OUTPUT.with(|slot| slot.replace(Some(output)));
    on_cleanup(|| {
        METRICS.with(|value| value.set(false));
        OUTPUT.with(|slot| slot.replace(None));
    });
    view! {
        <details><summary>"Diagnostics du rendu et du playtest"</summary>
            <label><input type="checkbox" prop:checked=move || metrics.get()
                on:change=move |event| { let checked = event_target_checked(&event); metrics.set(checked); METRICS.with(|value| value.set(checked)); }/>
                "Afficher les mesures dans la vue 3D"</label>
            <super::input_capture::InputCaptureStatus/>
            <output id="authoring-playtest-status">{move || status.get()}</output>
        </details>
    }
}

/// Human-readable runtime observation, not a persisted authoring document.
#[derive(Default)]
pub(super) struct MovementObservation {
    last_tick: u64,
    last_movement: Option<(u64, Vec3, Vec3, usize)>,
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
pub(super) fn publish(
    player: Option<Res<forge_engine::PlayerRuntimeSnapshot>>,
    focus: Option<Res<PlayerInputFocus>>,
    ready: Option<Res<forge_engine::PlayerSimulationReady>>,
    time: Res<Time>,
    mut last: Local<f64>,
    mut movement: Local<MovementObservation>,
) {
    let Some(player) = player else {
        *movement = MovementObservation::default();
        LAST_RUNTIME.with(|slot| slot.replace(None));
        return;
    };
    let now = time.elapsed_secs_f64();
    if player.fixed_tick < movement.last_tick {
        movement.last_movement = None;
    }
    if player.fixed_tick != movement.last_tick {
        let requested = player.desired_translation;
        if requested.x * requested.x + requested.z * requested.z > 1.0e-8 {
            movement.last_movement = Some((
                player.fixed_tick,
                requested,
                player.effective_translation,
                player.collisions,
            ));
        }
        movement.last_tick = player.fixed_tick;
    }
    if now - *last < 0.25 {
        return;
    }
    *last = now;
    let position = player.player_position;
    let simulation_ready = ready.as_ref().is_some_and(|value| value.0);
    let mut text = format!(
        "{} · Tick {} · commande {:?} · position (m) {:.2}, {:.2}, {:.2} · au sol {} · cap caméra (°) {:.1} · caméra {:?}",
        super::authoring_playtest::status(),
        player.fixed_tick,
        player.last_applied_sequence,
        position.x,
        position.y,
        position.z,
        player.grounded,
        player.camera_yaw_radians.to_degrees().rem_euclid(360.0),
        player.camera_mode
    );
    let _ = write!(
        text,
        " · entrée active {} · simulation prête {}",
        focus.is_some_and(|value| value.0),
        simulation_ready
    );
    if let Some((tick, requested, effective, contacts)) = movement.last_movement {
        let _ = write!(
            text,
            " · dernier mouvement observé tick {tick} : demandé X/Z ({:.4}, {:.4}) m, résolu ({:.4}, {:.4}) m, {contacts} contact(s) (sol inclus)",
            requested.x, requested.z, effective.x, effective.z
        );
    } else {
        text.push_str(" · aucun mouvement horizontal observé depuis le démarrage");
    }
    LAST_RUNTIME.with(|slot| {
        slot.replace(Some(RuntimeObservation {
            fixed_tick: player.fixed_tick,
            final_position_m: [position.x, position.y, position.z],
            grounded: player.grounded,
            collisions: player.collisions,
            simulation_ready,
            runtime_status: text.clone(),
        }));
    });
    OUTPUT.with(|slot| {
        if let Some(output) = *slot.borrow() {
            output.set(text);
        }
    });
}

pub(super) fn set_playing(playing: bool) {
    let changed = PLAYING.with(|value| value.replace(playing) != playing);
    if changed {
        // A runtime observation belongs to exactly one playtest lifetime.  In
        // particular, never let a quick stop after a new start relabel the
        // previous session's position and contacts with the new identity.
        LAST_RUNTIME.with(|slot| slot.replace(None));
    }
}

pub(super) fn capture() {
    if !PLAYING.with(std::cell::Cell::get) {
        return;
    }
    if let Some(canvas) = web_sys::window()
        .and_then(|window| window.document())
        .and_then(|document| document.get_element_by_id("forge-authoring-canvas"))
    {
        super::input_capture::begin_request();
        super::input_capture::focus_then_request_pointer_lock(&canvas);
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameter")]
pub(super) fn update_focus(
    mut focus: ResMut<PlayerInputFocus>,
    mut overlay: ResMut<forge_engine::DiagnosticsOverlaySettings>,
    ready: Option<Res<forge_engine::PlayerSimulationReady>>,
) {
    let visible = METRICS.with(std::cell::Cell::get);
    if overlay.visible != visible {
        overlay.visible = visible;
    }
    focus.0 = ready.is_some_and(|ready| ready.0)
        && web_sys::window()
            .and_then(|window| window.document())
            .is_some_and(|document| {
                document.has_focus().unwrap_or(false)
                    && document
                        .pointer_lock_element()
                        .is_some_and(|element| element.id() == "forge-authoring-canvas")
            });
    let focused = web_sys::window()
        .and_then(|window| window.document())
        .is_some_and(|document| document.has_focus().unwrap_or(false));
    super::input_capture::observe(focus.0, focused);
}
