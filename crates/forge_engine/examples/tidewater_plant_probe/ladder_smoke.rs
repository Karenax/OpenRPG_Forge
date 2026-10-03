//! Native pier-ladder exercise through the real player, KCC, and village collision mesh.
use bevy::{
    app::AppExit,
    prelude::*,
    render::view::screenshot::{Screenshot, ScreenshotCaptured, save_to_disk},
};
use forge_engine::{
    PendingPlayerControlCommand, PlayerControlCommand, PlayerControllerSet, PlayerRuntimeBindings,
    PlayerRuntimeSnapshot, PlayerSimulationReady, TidewaterPlayerInput, TidewaterPlayerState,
    tidewater_locomotion::Mode,
};
use std::{io::Write, path::PathBuf};

const LADDER_CENTER: Vec2 = Vec2::new(62.32, 35.1);
const DECK_EXIT: Vec3 = Vec3::new(61.12, 2.3, 35.1);
const EXERCISE_DEADLINE_SECS: f32 = 180.0;
const CAPTURE_DEADLINE_SECS: f32 = 10.0;

#[derive(Clone, Copy, Debug, Default, PartialEq, serde::Serialize)]
enum Stage {
    #[default]
    EnterWater,
    FindLadder,
    Climb,
    SettleOnDeck,
    Done,
}

#[derive(Resource)]
struct Smoke {
    stage: Stage,
    stage_ticks: u32,
    total_ticks: u32,
    snapshots: Vec<serde_json::Value>,
    observed: Observations,
    requested: bool,
    requested_at: Option<f32>,
    captured: bool,
    capture_error: Option<String>,
    finalized: bool,
}

#[derive(Default)]
struct Observations {
    outside_trigger: bool,
    ladder_available: bool,
    stable_backend: bool,
}

pub(super) fn install(app: &mut App) {
    app.insert_resource(bevy::winit::WinitSettings::continuous())
        .insert_resource(Smoke {
            stage: Stage::EnterWater,
            stage_ticks: 0,
            total_ticks: 0,
            snapshots: Vec::new(),
            observed: Observations {
                stable_backend: true,
                ..default()
            },
            requested: false,
            requested_at: None,
            captured: false,
            capture_error: None,
            finalized: false,
        })
        .add_systems(FixedUpdate, drive.before(PlayerControllerSet::Drive))
        .add_systems(Update, finish);
}

fn drive(
    ready: Res<PlayerSimulationReady>,
    state: Res<TidewaterPlayerState>,
    player: Res<PlayerRuntimeSnapshot>,
    mut smoke: ResMut<Smoke>,
    mut pending: ResMut<PendingPlayerControlCommand>,
    mut vertical: ResMut<TidewaterPlayerInput>,
) {
    if !ready.0 || smoke.requested {
        return;
    }
    smoke.total_ticks += 1;
    smoke.stage_ticks += 1;
    if smoke.stage == Stage::FindLadder {
        smoke.observed.outside_trigger |= !state.ladder_nearby;
    }
    smoke.observed.ladder_available |= state.ladder_nearby;
    smoke.observed.stable_backend &=
        player.backend_identity_stable && state.motion.position.is_finite();
    let next = match smoke.stage {
        Stage::EnterWater if state.motion.mode == Mode::Swim => Some(Stage::FindLadder),
        Stage::FindLadder if state.ladder_nearby => Some(Stage::Climb),
        Stage::Climb if state.ladder_climbs == 1 && state.motion.mode == Mode::Walk => {
            Some(Stage::SettleOnDeck)
        }
        Stage::SettleOnDeck if player.grounded && smoke.stage_ticks >= 30 => Some(Stage::Done),
        _ => None,
    };
    if let Some(next) = next {
        let completed = smoke.stage;
        let sample = serde_json::json!({
            "completed_stage": completed,
            "tick": smoke.total_ticks,
            "stage_ticks": smoke.stage_ticks,
            "anchor": state.motion.position.to_array(),
            "velocity": state.motion.velocity.to_array(),
            "mode": format!("{:?}", state.motion.mode),
            "grounded": player.grounded,
            "ladder_nearby": state.ladder_nearby,
            "ladder_climbing": state.ladder_climbing,
            "ladder_climbs": state.ladder_climbs,
            "swim_entries": state.swim_entries,
            "walk_returns": state.walk_returns,
            "water_height": state.motion.water_h,
            "floor": state.ground_height,
            "query_step": state.water_query_step,
            "query_time": state.water_query_time,
            "stable_backend": player.backend_identity_stable,
        });
        println!("TIDEWATER_LADDER_STAGE {sample}");
        smoke.snapshots.push(sample);
        smoke.stage = next;
        smoke.stage_ticks = 0;
    }
    *vertical = TidewaterPlayerInput::default();
    let mut command = PlayerControlCommand::default();
    match smoke.stage {
        Stage::FindLadder => command.move_axis = Vec2::Y,
        Stage::Climb => vertical.ascend = true,
        _ => {}
    }
    pending
        .submit(command)
        .expect("finite native ladder command");
}

fn request_capture(
    commands: &mut Commands,
    smoke: &mut Smoke,
    screenshot: PathBuf,
    elapsed: f32,
) -> bool {
    smoke.requested = true;
    smoke.requested_at = Some(elapsed);
    match std::fs::remove_file(&screenshot) {
        Ok(()) => {}
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => {}
        Err(error) => {
            smoke.capture_error = Some(error.to_string());
            return false;
        }
    }
    commands
        .spawn(Screenshot::primary_window())
        .observe(save_to_disk(screenshot))
        .observe(|_: On<ScreenshotCaptured>, mut smoke: ResMut<Smoke>| {
            smoke.captured = true;
        });
    true
}

fn build_receipt(
    state: &TidewaterPlayerState,
    player: &PlayerRuntimeSnapshot,
    bindings: &PlayerRuntimeBindings,
    smoke: &Smoke,
    timeout: bool,
    capture_timeout: bool,
    screenshot_bytes: &std::io::Result<Option<u64>>,
) -> (bool, serde_json::Value) {
    let screenshot_nonempty = matches!(screenshot_bytes, Ok(Some(bytes)) if *bytes > 0);
    let exit_error = state.motion.position.distance(DECK_EXIT);
    let trigger_position = Vec2::new(state.motion.position.x, state.motion.position.z);
    let trigger_distance = trigger_position.distance(LADDER_CENTER);
    let binding = bindings.snapshot();
    let has_rapier_backend = binding.is_some_and(|binding| {
        binding.rapier_collider_handle.is_some() && binding.rapier_context_entity.is_some()
    });
    let pass = !timeout
        && smoke.observed.stable_backend
        && smoke.observed.outside_trigger
        && smoke.observed.ladder_available
        && state.swim_entries == 1
        && state.ladder_climbs == 1
        && state.walk_returns == 1
        && player.grounded
        && has_rapier_backend
        && exit_error < 0.08
        && smoke.snapshots.len() == 4
        && screenshot_nonempty;
    let receipt = serde_json::json!({
        "pass": pass,
        "scope": "native source pier ladder on canonical terrain, GPU water, and village Rapier mesh; command injection, not OS input",
        "timeout": timeout,
        "stage": smoke.stage,
        "ticks": smoke.total_ticks,
        "saw_outside_trigger": smoke.observed.outside_trigger,
        "saw_ladder_available": smoke.observed.ladder_available,
        "swim_entries": state.swim_entries,
        "ladder_climbs": state.ladder_climbs,
        "walk_returns": state.walk_returns,
        "grounded": player.grounded,
        "anchor": state.motion.position.to_array(),
        "expected_exit": DECK_EXIT.to_array(),
        "exit_error_m": exit_error,
        "distance_from_trigger_m": trigger_distance,
        "water_query_position": state.water_query_position,
        "water_query_step": state.water_query_step,
        "water_query_time": state.water_query_time,
        "rapier_backend_present": has_rapier_backend,
        "rapier_collider_handle": binding.and_then(|binding| binding.rapier_collider_handle),
        "stable_backend": smoke.observed.stable_backend,
        "capture_requested": smoke.requested,
        "capture_event_received": smoke.captured,
        "capture_error": smoke.capture_error,
        "capture_timeout": capture_timeout,
        "screenshot_nonempty": screenshot_nonempty,
        "screenshot_bytes": screenshot_bytes.as_ref().ok().copied().flatten(),
        "screenshot_error": screenshot_bytes.as_ref().err().map(ToString::to_string),
        "snapshots": smoke.snapshots,
    });
    (pass, receipt)
}

fn finish(
    mut commands: Commands,
    time: Res<Time<Real>>,
    (state, player, bindings): (
        Res<TidewaterPlayerState>,
        Res<PlayerRuntimeSnapshot>,
        Res<PlayerRuntimeBindings>,
    ),
    mut smoke: ResMut<Smoke>,
    probe: Res<super::Probe>,
    mut exit: MessageWriter<AppExit>,
) {
    if smoke.finalized {
        return;
    }
    let elapsed = time.elapsed_secs();
    let exercise_timeout = !smoke.requested && elapsed > EXERCISE_DEADLINE_SECS;
    if !smoke.requested {
        if smoke.stage != Stage::Done && !exercise_timeout {
            return;
        }
        if !exercise_timeout
            && request_capture(&mut commands, &mut smoke, probe.screenshot.clone(), elapsed)
        {
            return;
        }
    }
    let capture_timeout = smoke.requested_at.is_some_and(|requested_at| {
        !smoke.captured && elapsed - requested_at > CAPTURE_DEADLINE_SECS
    });
    if smoke.requested && !smoke.captured && smoke.capture_error.is_none() && !capture_timeout {
        return;
    }
    smoke.finalized = true;
    let screenshot_bytes = smoke
        .captured
        .then(|| std::fs::metadata(&probe.screenshot).map(|metadata| metadata.len()))
        .transpose();
    let timeout = exercise_timeout || capture_timeout;
    let (pass, receipt) = build_receipt(
        &state,
        &player,
        &bindings,
        &smoke,
        timeout,
        capture_timeout,
        &screenshot_bytes,
    );
    let saved = serde_json::to_vec_pretty(&receipt)
        .map_err(std::io::Error::other)
        .and_then(|data| {
            std::fs::OpenOptions::new()
                .create_new(true)
                .write(true)
                .open(probe.screenshot.with_extension("walk.json"))?
                .write_all(&data)
        });
    if !pass || saved.is_err() {
        error!(
            ?receipt,
            ?saved,
            ?screenshot_bytes,
            "native ladder smoke failed"
        );
        exit.write(AppExit::error());
        return;
    }
    println!("TIDEWATER_LADDER_PASS {receipt}");
    exit.write(AppExit::Success);
}
