//! Opt-in, pinned application-level replay. No OS automation or direct state edits.
use super::{CommandResult, NativeGameplayCommand, observations::ObservationSink};
use crate::{
    PendingPlayerControlCommand, PlayerControlCommand, PlayerRuntimeSnapshot,
    gameplay::GameplayRuntime,
};
use bevy::{
    prelude::*,
    render::view::screenshot::{Screenshot, ScreenshotCaptured, save_to_disk},
};
use forge_core::{CommandId, ContentHash};
use forge_gameplay::{GameAction, GameRequest};
use serde::{Deserialize, Serialize};
use std::{path::PathBuf, time::Instant};

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Plan {
    schema: String,
    version: u32,
    source_hash: ContentHash,
    archive_hash: ContentHash,
    max_duration_s: u32,
    steps: Vec<Step>,
}
#[derive(Clone, Deserialize, Serialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
enum Step {
    WaitReady {
        timeout_s: u32,
    },
    WaitTicks {
        ticks: u32,
        timeout_s: u32,
    },
    Move {
        axis: [f32; 2],
        look_delta: [f32; 2],
        sprint: bool,
        jump: bool,
        ticks: u32,
        timeout_s: u32,
    },
    MoveTo {
        position_xz: [f32; 2],
        tolerance_m: f32,
        timeout_s: u32,
    },
    Action {
        action: GameAction,
        timeout_s: u32,
    },
    Save {
        timeout_s: u32,
    },
    ResumeNextLaunch {
        allow_compatible_edits: bool,
        timeout_s: u32,
    },
    Capture {
        name: String,
        timeout_s: u32,
    },
    Menu {
        open: bool,
        timeout_s: u32,
    },
    Exit,
}
impl Step {
    fn timeout(&self) -> u32 {
        match *self {
            Self::WaitReady { timeout_s }
            | Self::WaitTicks { timeout_s, .. }
            | Self::Move { timeout_s, .. }
            | Self::MoveTo { timeout_s, .. }
            | Self::Action { timeout_s, .. }
            | Self::Save { timeout_s }
            | Self::ResumeNextLaunch { timeout_s, .. }
            | Self::Capture { timeout_s, .. }
            | Self::Menu { timeout_s, .. } => timeout_s,
            Self::Exit => 1,
        }
    }
}
#[derive(Resource)]
struct Scenario {
    plan: Plan,
    hash: ContentHash,
    output: PathBuf,
    started: Instant,
    step_started: Instant,
    step: usize,
    step_ticks: u32,
    submitted: bool,
    command_sequence: u64,
    expected_command: Option<NativeGameplayCommand>,
    expected_action: Option<CommandId>,
    action_sequence: u64,
    captured: bool,
    finished: bool,
    sensitivity: f32,
    walk_speed: f32,
    braking: f32,
}

pub(super) fn install(app: &mut App) -> Result<(), String> {
    let Some(path) = std::env::var_os("FORGE_NATIVE_GAMEPLAY_SCENARIO_PATH") else {
        return Ok(());
    };
    let expected = std::env::var("FORGE_NATIVE_GAMEPLAY_SCENARIO_SHA256")
        .map_err(|_| "Scenario hash required")?
        .parse::<ContentHash>()
        .map_err(|e| e.to_string())?;
    let path = PathBuf::from(path);
    if path.metadata().map_err(|e| e.to_string())?.len() > 128 * 1024 {
        return Err("Scenario exceeds input quota".into());
    }
    let bytes = std::fs::read(path).map_err(|e| e.to_string())?;
    if forge_core::bytes_sha256(&bytes) != expected {
        return Err("Scenario hash mismatch".into());
    }
    let plan: Plan = serde_json::from_slice(&bytes).map_err(|e| e.to_string())?;
    validate(&plan)?;
    let runtime = app.world().resource::<GameplayRuntime>();
    if runtime.session().source_hash() != plan.source_hash {
        return Err("Scenario source differs from native launch".into());
    }
    let sink = app
        .world_mut()
        .get_resource_mut::<ObservationSink>()
        .ok_or("Scenario requires native observations")?;
    if !sink.matches_archive(plan.archive_hash) {
        return Err("Scenario archive differs from native launch".into());
    }
    let output = PathBuf::from(
        std::env::var_os("FORGE_NATIVE_GAMEPLAY_OUTPUT_ROOT")
            .ok_or("Scenario output root required")?,
    )
    .canonicalize()
    .map_err(|e| e.to_string())?;
    let sensitivity = crate::player_controller::look_sensitivity_radians(app.world())?;
    let walk_speed = crate::player_controller::walk_speed_mps(app.world())?;
    let braking = crate::player_controller::braking_mps2(app.world())?;
    app.world_mut().resource_mut::<ObservationSink>().scenario(
        expected,
        0,
        "prepared",
        &serde_json::json!({"steps":plan.steps.len(),"max_duration_s":plan.max_duration_s}),
    )?;
    let now = Instant::now();
    app.insert_resource(Scenario {
        plan,
        hash: expected,
        output,
        started: now,
        step_started: now,
        step: 0,
        step_ticks: 0,
        submitted: false,
        command_sequence: 0,
        expected_command: None,
        expected_action: None,
        action_sequence: 0,
        captured: false,
        finished: false,
        sensitivity,
        walk_speed,
        braking,
    })
    .insert_resource(bevy::winit::WinitSettings::continuous());
    install_driver(app);
    Ok(())
}

fn install_driver(app: &mut App) {
    app.add_systems(
        FixedUpdate,
        drive
            .before(crate::PlayerControllerSet::Drive)
            .after(super::readiness),
    );
}

fn validate(plan: &Plan) -> Result<(), String> {
    if plan.schema != "forge.native-gameplay.scenario"
        || plan.version != 1
        || !(1..=600).contains(&plan.max_duration_s)
        || plan.steps.is_empty()
        || plan.steps.len() > 128
        || !matches!(plan.steps.last(), Some(Step::Exit))
    {
        return Err("Invalid bounded native scenario".into());
    }
    for (index, step) in plan.steps.iter().enumerate() {
        if !(1..=300).contains(&step.timeout())
            || matches!(step, Step::Exit) && index + 1 != plan.steps.len()
        {
            return Err("Invalid scenario step budget".into());
        }
        match step {
            Step::Move {
                axis,
                look_delta,
                ticks,
                ..
            } if !Vec2::from_array(*axis).is_finite()
                || !Vec2::from_array(*look_delta).is_finite()
                || !(1..=36_000).contains(ticks) =>
            {
                return Err("Invalid scenario input".into());
            }
            Step::WaitTicks { ticks, .. } if !(1..=36_000).contains(ticks) => {
                return Err("Invalid scenario tick budget".into());
            }
            Step::MoveTo {
                position_xz,
                tolerance_m,
                ..
            } if !Vec2::from_array(*position_xz).is_finite()
                || !tolerance_m.is_finite()
                || !(0.01..=2.0).contains(tolerance_m) =>
            {
                return Err("Invalid scenario destination".into());
            }
            Step::Capture { name, .. }
                if !std::path::Path::new(name)
                    .extension()
                    .and_then(|extension| extension.to_str())
                    .is_some_and(|extension| extension.eq_ignore_ascii_case("png"))
                    || name.len() > 128
                    || name.starts_with('.')
                    || name.contains(['/', '\\', ':']) =>
            {
                return Err("Invalid scenario capture basename".into());
            }
            _ => (),
        }
    }
    Ok(())
}

#[allow(
    clippy::too_many_lines,
    reason = "The pinned replay executes one bounded typed step through the existing input and gameplay command paths"
)]
fn drive(world: &mut World) {
    world.resource_scope(|world, mut scenario: Mut<Scenario>| {
        if scenario.finished {
            return;
        }
        let result = execute_step(world, &mut scenario);
        match result {
            Ok(false) => (),
            Ok(true) => {
                let step = serde_json::to_value(&scenario.plan.steps[scenario.step])
                    .unwrap_or(serde_json::Value::Null);
                if let Err(error) = world.resource_mut::<ObservationSink>().scenario(
                    scenario.hash,
                    scenario.step,
                    "completed",
                    &step,
                ) {
                    fail(world, &mut scenario, &error);
                    return;
                }
                scenario.step += 1;
                scenario.submitted = false;
                scenario.expected_command = None;
                scenario.expected_action = None;
                scenario.step_ticks = 0;
                scenario.captured = false;
                scenario.step_started = Instant::now();
                if scenario.step == scenario.plan.steps.len() {
                    scenario.finished = true;
                    world.write_message(bevy::app::AppExit::Success);
                }
            }
            Err(error) => fail(world, &mut scenario, &error),
        }
    });
}

fn fail(world: &mut World, scenario: &mut Scenario, error: &str) {
    scenario.finished = true;
    world
        .resource_mut::<PendingPlayerControlCommand>()
        .cancel_input();
    let _ = world.resource_mut::<ObservationSink>().scenario(
        scenario.hash,
        scenario.step,
        "failed",
        &serde_json::json!({"error":error}),
    );
    tracing::error!(%error, step=scenario.step, "native gameplay scenario failed closed");
    world.write_message(bevy::app::AppExit::error());
}

#[allow(
    clippy::too_many_lines,
    reason = "One typed step dispatch never modifies gameplay state or physical observations"
)]
fn execute_step(world: &mut World, scenario: &mut Scenario) -> Result<bool, String> {
    if scenario.started.elapsed().as_secs_f64() > f64::from(scenario.plan.max_duration_s) {
        return Err("Native scenario overall deadline exceeded".into());
    }
    let step = scenario.plan.steps[scenario.step].clone();
    if scenario.step_started.elapsed().as_secs_f64() > f64::from(step.timeout()) {
        return Err("Native scenario step deadline exceeded".into());
    }
    world.resource_mut::<crate::PlayerInputFocus>().0 = true;
    if let Some(error) = world.resource::<GameplayRuntime>().error() {
        return Err(error.into());
    }
    match step {
        Step::WaitReady { .. } | Step::Exit => {
            world
                .resource_mut::<PendingPlayerControlCommand>()
                .cancel_input();
            Ok(world.resource::<GameplayRuntime>().is_ready())
        }
        Step::WaitTicks { ticks, .. } => {
            world
                .resource_mut::<PendingPlayerControlCommand>()
                .cancel_input();
            if !world.resource::<GameplayRuntime>().is_ready() {
                return Ok(false);
            }
            scenario.step_ticks += 1;
            Ok(scenario.step_ticks >= ticks)
        }
        Step::Move {
            axis,
            look_delta,
            sprint,
            jump,
            ticks,
            ..
        } => {
            if !world.resource::<GameplayRuntime>().is_ready() {
                return Ok(false);
            }
            if scenario.step_ticks >= ticks {
                world
                    .resource_mut::<PendingPlayerControlCommand>()
                    .cancel_input();
                return Ok(true);
            }
            let command = PlayerControlCommand {
                move_axis: Vec2::from_array(axis),
                look_delta: if scenario.step_ticks == 0 {
                    Vec2::from_array(look_delta)
                } else {
                    Vec2::ZERO
                },
                sprint,
                jump: jump && scenario.step_ticks == 0,
                toggle_camera: false,
            };
            submit_input(world, scenario, command)?;
            scenario.step_ticks += 1;
            Ok(false)
        }
        Step::MoveTo {
            position_xz,
            tolerance_m,
            ..
        } => {
            if !world.resource::<GameplayRuntime>().is_ready() {
                return Ok(false);
            }
            let player = world.resource::<PlayerRuntimeSnapshot>();
            let delta = Vec2::from_array(position_xz) - player.player_position.xz();
            if delta.length() <= tolerance_m {
                world
                    .resource_mut::<PendingPlayerControlCommand>()
                    .cancel_input();
                return Ok(true);
            }
            let yaw = (-delta.x).atan2(-delta.y);
            let turn = (yaw - player.camera_yaw_radians + std::f32::consts::PI)
                .rem_euclid(std::f32::consts::TAU)
                - std::f32::consts::PI;
            let timestep = world.resource::<Time<Fixed>>().timestep().as_secs_f32();
            let near = delta.length()
                <= (player.horizontal_velocity.length_squared() / (2.0 * scenario.braking)
                    + scenario.walk_speed * timestep * 4.0)
                    .max(1.0);
            // The real controller normalizes non-zero axes. Near the target,
            // use one fixed-tick pulse then brake fully before the next pulse.
            let axis = move_to_axis(near, player.horizontal_velocity.length());
            submit_input(
                world,
                scenario,
                PlayerControlCommand {
                    move_axis: axis,
                    look_delta: Vec2::new(-turn / scenario.sensitivity, 0.0),
                    ..default()
                },
            )?;
            Ok(false)
        }
        Step::Action { action, .. } => {
            if !scenario.submitted {
                if !world.resource::<GameplayRuntime>().is_ready() {
                    return Ok(false);
                }
                scenario.action_sequence = world
                    .resource::<GameplayRuntime>()
                    .session()
                    .state()
                    .sequence;
                send_command(
                    world,
                    scenario,
                    NativeGameplayCommand::Action(action.clone()),
                )?;
                return Ok(false);
            }
            check_action(world, scenario, &action)
        }
        Step::Save { .. } => {
            world
                .resource_mut::<PendingPlayerControlCommand>()
                .cancel_input();
            if !scenario.submitted {
                if world.resource::<GameplayRuntime>().save_bytes().is_err() {
                    return Ok(false);
                }
                send_command(world, scenario, NativeGameplayCommand::Save)?;
                return Ok(false);
            }
            if !check_command(world, scenario)? {
                return Ok(false);
            }
            let saved = world
                .resource::<super::storage::GameStorage>()
                .copy_committed_save(&scenario.output)?;
            world.resource_mut::<ObservationSink>().scenario(
                scenario.hash,
                scenario.step,
                "save_copied",
                &saved,
            )?;
            Ok(true)
        }
        Step::ResumeNextLaunch {
            allow_compatible_edits,
            ..
        } => {
            if !scenario.submitted {
                send_command(
                    world,
                    scenario,
                    NativeGameplayCommand::ResumeNextLaunch {
                        allow_compatible_edits,
                    },
                )?;
                return Ok(false);
            }
            check_command(world, scenario)
        }
        Step::Capture { name, .. } => {
            world
                .resource_mut::<PendingPlayerControlCommand>()
                .cancel_input();
            let path = scenario.output.join(&name);
            if !scenario.submitted {
                if path.exists() {
                    return Err("Scenario capture already exists".into());
                }
                world
                    .spawn(Screenshot::primary_window())
                    .observe(save_to_disk(path.clone()))
                    .observe(
                        |_: On<ScreenshotCaptured>, mut scenario: ResMut<Scenario>| {
                            scenario.captured = true;
                        },
                    );
                scenario.submitted = true;
                return Ok(false);
            }
            if !scenario.captured {
                return Ok(false);
            }
            let bytes = std::fs::read(path).map_err(|e| e.to_string())?;
            if bytes.len() < 32 || !bytes.starts_with(b"\x89PNG\r\n\x1a\n") {
                return Err("Native screenshot export is invalid".into());
            }
            Ok(true)
        }
        Step::Menu { open, .. } => {
            world
                .resource_mut::<PendingPlayerControlCommand>()
                .cancel_input();
            if world.resource::<super::NativeGameplayUi>().is_open() == open {
                if scenario.submitted {
                    world
                        .resource_mut::<ButtonInput<KeyCode>>()
                        .release(KeyCode::KeyG);
                }
                return Ok(true);
            }
            if !open
                && world
                    .resource::<super::NativeGameplayUi>()
                    .recovery
                    .is_some()
            {
                return Err(
                    "A refused resume requires an explicit recovery choice before closing its menu"
                        .into(),
                );
            }
            if !scenario.submitted {
                world
                    .resource_mut::<ButtonInput<KeyCode>>()
                    .press(KeyCode::KeyG);
                scenario.submitted = true;
            }
            Ok(false)
        }
    }
}

fn move_to_axis(near: bool, speed: f32) -> Vec2 {
    if near && speed > 0.01 {
        Vec2::ZERO
    } else {
        Vec2::Y
    }
}
fn send_command(
    world: &mut World,
    scenario: &mut Scenario,
    command: NativeGameplayCommand,
) -> Result<(), String> {
    scenario.expected_action = match &command {
        NativeGameplayCommand::Action(action) => Some(
            super::request_for_action(world.resource::<GameplayRuntime>(), action.clone())?
                .command_id,
        ),
        _ => None,
    };
    scenario.expected_command = Some(command.clone());
    scenario.command_sequence = world.resource::<CommandResult>().sequence;
    scenario.submitted = true;
    world.write_message(command);
    Ok(())
}
fn check_command(world: &World, scenario: &Scenario) -> Result<bool, String> {
    let result = world.resource::<CommandResult>();
    if result.sequence <= scenario.command_sequence {
        return Ok(false);
    }
    if result.sequence != scenario.command_sequence.saturating_add(1) {
        return Err("A concurrent command cannot acknowledge this scenario step".into());
    }
    match result.last.as_ref() {
        Some((command, result)) if scenario.expected_command.as_ref() == Some(command) => {
            result.as_ref().map(|()| true).map_err(Clone::clone)
        }
        Some(_) => Err("A concurrent command cannot acknowledge this scenario step".into()),
        None => Ok(false),
    }
}
fn check_action(world: &World, scenario: &Scenario, action: &GameAction) -> Result<bool, String> {
    if !check_command(world, scenario)? {
        return Ok(false);
    }
    let runtime = world.resource::<GameplayRuntime>();
    let matches = |request: &GameRequest| {
        Some(request.command_id) == scenario.expected_action
            && request.expected_sequence == scenario.action_sequence
            && request.actor == runtime.session().document().player
            && &request.action == action
    };
    if let Some((request, completion)) = world.resource::<CommandResult>().completed_action.as_ref()
        && matches(request)
    {
        completion.as_ref().map_err(Clone::clone)?;
    }
    Ok(runtime
        .session()
        .state()
        .receipts
        .last()
        .is_some_and(|receipt| {
            receipt.sequence > scenario.action_sequence && matches(&receipt.request)
        }))
}
fn submit_input(
    world: &mut World,
    scenario: &mut Scenario,
    command: PlayerControlCommand,
) -> Result<(), String> {
    let submitted = world
        .resource_mut::<PendingPlayerControlCommand>()
        .submit(command)
        .map_err(|e| e.to_string())?;
    if !scenario.submitted {
        world.resource_mut::<ObservationSink>().scenario(scenario.hash, scenario.step, "input_submitted", &serde_json::json!({"submitted_sequence":submitted,"move_axis":command.move_axis.to_array(),"look_delta":command.look_delta.to_array()}))?;
        scenario.submitted = true;
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_gameplay::{ActionObservation, ObjectKind};

    fn exit_app(
        directory: &super::super::tests::Directory,
        player_ready: bool,
        projection_ready: bool,
    ) -> (App, PathBuf, PathBuf) {
        let (zone, document) = super::super::tests::fixture();
        let source = zone.head().content_hash().unwrap();
        let archive = forge_core::bytes_sha256(&zone.archive_bytes().unwrap());
        let functional = document.functional_hash().unwrap();
        let action = GameAction::Push {
            object: document.objects[0].id,
            impulse: forge_core::Vec3::new(0.0, 0.0, 1.0),
        };
        let storage = super::super::storage::GameStorage::open(&directory.root, &document).unwrap();
        let mut runtime = GameplayRuntime::new(document, source, None, false).unwrap();
        // Reproduce the preceding fixed tick's readiness, not the current one.
        runtime.set_enabled(true);
        let observation_path = directory.root.join("observations.jsonl");
        let report_path = directory.root.join("game-report.json");
        let mut observations =
            ObservationSink::new(&observation_path, source, archive, functional).unwrap();
        observations.boot(&runtime, false, None, None).unwrap();
        let report =
            super::super::report::ReportSink::new(&report_path, source, archive, functional)
                .unwrap();
        let mut scenario = action_scenario(&action);
        scenario.plan.source_hash = source;
        scenario.plan.archive_hash = archive;
        scenario.plan.steps = vec![Step::Exit];
        scenario.output.clone_from(&directory.root);
        let mut app = App::new();
        app.add_plugins(MinimalPlugins)
            .insert_resource(bevy::time::TimeUpdateStrategy::ManualDuration(
                std::time::Duration::from_micros(15_625),
            ))
            .insert_resource(runtime)
            .insert_resource(storage)
            .insert_resource(observations)
            .insert_resource(super::super::NativeGameplayUi {
                open: false,
                feedback: String::new(),
                last_view: None,
                recovery: None,
            })
            .insert_resource(crate::PlayerSimulationReady(player_ready))
            .insert_resource(crate::gameplay::GameplayProjectionStatus {
                ready: projection_ready,
            })
            .insert_resource(crate::PlayerInputFocus(true))
            .init_resource::<PendingPlayerControlCommand>()
            .init_resource::<CommandResult>()
            .add_message::<bevy::app::AppExit>();
        // Initialize the injected clock before installing the scenario.
        app.update();
        app.insert_resource(scenario)
            .add_systems(FixedUpdate, super::super::readiness);
        install_driver(&mut app);
        super::super::install_exit_observers(&mut app, Some(report));
        (app, observation_path, report_path)
    }

    fn assert_exit_waits_for_current_readiness(player_ready: bool, projection_ready: bool) {
        let directory = super::super::tests::Directory::new();
        let (mut app, observation_path, report_path) =
            exit_app(&directory, player_ready, projection_ready);
        assert!(app.world().resource::<GameplayRuntime>().is_ready());
        assert_eq!(
            app.world().resource::<Scenario>().plan.steps[0].timeout(),
            1
        );
        app.update();
        assert!(!app.world().resource::<GameplayRuntime>().is_ready());
        assert!(app.should_exit().is_none());
        assert_eq!(app.world().resource::<Scenario>().step, 0);
        assert!(!app.world().resource::<Scenario>().finished);
        assert!(std::fs::read(&report_path).unwrap().is_empty());

        app.world_mut()
            .resource_mut::<crate::PlayerSimulationReady>()
            .0 = true;
        app.world_mut()
            .resource_mut::<crate::gameplay::GameplayProjectionStatus>()
            .ready = true;
        app.update();
        assert_eq!(app.should_exit(), Some(bevy::app::AppExit::Success));
        assert!(app.world().resource::<Scenario>().finished);
        let report: serde_json::Value =
            serde_json::from_slice(&std::fs::read(&report_path).unwrap()).unwrap();
        let observations = std::fs::read_to_string(observation_path).unwrap();
        let terminal: serde_json::Value =
            serde_json::from_str(observations.lines().last().unwrap()).unwrap();
        assert_eq!(report["ready"], true);
        assert_eq!(report["exit_success"], true);
        assert_eq!(terminal["kind"], "exit");
        assert_eq!(terminal["exit_success"], true);
        assert_eq!(report["pid"], std::process::id());
        assert_eq!(report["tick"], terminal["tick"]);
        assert_eq!(report["sequence"], terminal["sequence"]);
        assert_eq!(report["simulation_error"], serde_json::Value::Null);
    }

    #[test]
    fn native_exit_waits_for_current_projection_readiness() {
        assert_exit_waits_for_current_readiness(true, false);
    }

    #[test]
    fn native_exit_waits_for_current_player_readiness() {
        assert_exit_waits_for_current_readiness(false, true);
    }

    #[test]
    fn native_exit_readiness_deadline_remains_a_failure() {
        let directory = super::super::tests::Directory::new();
        let (mut app, observation_path, report_path) = exit_app(&directory, true, false);
        app.world_mut().resource_mut::<Scenario>().step_started = Instant::now()
            .checked_sub(std::time::Duration::from_secs(2))
            .unwrap();
        app.update();
        assert!(matches!(
            app.should_exit(),
            Some(bevy::app::AppExit::Error(_))
        ));
        assert!(app.world().resource::<Scenario>().finished);
        let report: serde_json::Value =
            serde_json::from_slice(&std::fs::read(report_path).unwrap()).unwrap();
        assert_eq!(report["exit_success"], false);
        let observations = std::fs::read_to_string(observation_path).unwrap();
        assert!(observations.lines().any(|line| {
            let row: serde_json::Value = serde_json::from_str(line).unwrap();
            row["kind"] == "scenario"
                && row["status"] == "failed"
                && row["detail"]["error"] == "Native scenario step deadline exceeded"
        }));
    }

    fn action_scenario(action: &GameAction) -> Scenario {
        let now = Instant::now();
        Scenario {
            plan: Plan {
                schema: "forge.native-gameplay.scenario".into(),
                version: 1,
                source_hash: ContentHash::from_bytes([4; 32]),
                archive_hash: ContentHash::from_bytes([4; 32]),
                max_duration_s: 60,
                steps: vec![
                    Step::Action {
                        action: action.clone(),
                        timeout_s: 10,
                    },
                    Step::Exit,
                ],
            },
            hash: ContentHash::from_bytes([3; 32]),
            output: PathBuf::new(),
            started: now,
            step_started: now,
            step: 0,
            step_ticks: 0,
            submitted: false,
            command_sequence: 0,
            expected_command: None,
            expected_action: None,
            action_sequence: 0,
            captured: false,
            finished: false,
            sensitivity: 0.01,
            walk_speed: 3.0,
            braking: 12.0,
        }
    }

    fn action_app(directory: &super::super::tests::Directory) -> (App, Scenario, GameAction) {
        let (_, mut document) = super::super::tests::fixture();
        let object = document.objects[0].id;
        document.objects[0].kind = ObjectKind::Prop;
        document.objects[0].physics.mass_kg = 1.0;
        document.objects[0].physics.pushable = true;
        let storage = super::super::storage::GameStorage::open(&directory.root, &document).unwrap();
        let mut runtime =
            GameplayRuntime::new(document, ContentHash::from_bytes([4; 32]), None, false).unwrap();
        runtime.set_enabled(true);
        let mut app = App::new();
        app.insert_resource(runtime)
            .insert_resource(storage)
            .insert_resource(super::super::NativeGameplayUi {
                open: false,
                feedback: String::new(),
                last_view: None,
                recovery: None,
            })
            .init_resource::<CommandResult>()
            .add_message::<NativeGameplayCommand>()
            .add_message::<bevy::app::AppExit>()
            .add_systems(Update, super::super::process_commands);
        let action = GameAction::Push {
            object,
            impulse: forge_core::Vec3::new(0.0, 0.0, 1.0),
        };
        let mut scenario = action_scenario(&action);
        send_command(
            app.world_mut(),
            &mut scenario,
            NativeGameplayCommand::Action(action.clone()),
        )
        .unwrap();
        (app, scenario, action)
    }

    // Exercise the real domain refusal/receipt; only the adapter observation is
    // supplied by this unit fixture. No qualification observation is fabricated.
    fn resolve_queued_action(
        app: &mut App,
        confirmed_contact: bool,
    ) -> (GameRequest, Result<(), String>) {
        let mut runtime = app.world_mut().resource_mut::<GameplayRuntime>();
        let request = runtime.pending.pop_front().unwrap();
        let before = runtime.session.state().clone();
        let document = runtime.session.document().clone();
        let result = runtime.session.execute(
            &request,
            ActionObservation {
                line_of_sight: true,
                confirmed_contact,
                water_contact: false,
            },
        );
        runtime.feedback = match &result {
            Ok(receipt) => format!("Action acceptée · étape {}", receipt.sequence),
            Err(error) => error.to_string(),
        };
        if result.is_err() {
            assert_eq!(runtime.session.state(), &before);
        }
        assert_eq!(runtime.session.document(), &document);
        (
            request,
            result.map(|_| ()).map_err(|error| error.to_string()),
        )
    }

    #[test]
    fn native_scenario_action_pending_ignores_historical_errors() {
        let directory = super::super::tests::Directory::new();
        let (mut app, scenario, action) = action_app(&directory);
        assert_eq!(check_action(app.world(), &scenario, &action), Ok(false));
        app.update();
        app.world_mut().resource_mut::<CommandResult>().last_error =
            Some("an earlier unrelated command failed".into());
        assert!(
            app.world()
                .resource::<GameplayRuntime>()
                .has_queued_actions()
        );
        assert!(
            app.world()
                .resource::<CommandResult>()
                .completed_action
                .is_none()
        );
        assert_eq!(check_action(app.world(), &scenario, &action), Ok(false));
    }

    #[test]
    fn native_scenario_action_refusal_is_exact_and_preserves_state() {
        let directory = super::super::tests::Directory::new();
        let (mut app, scenario, action) = action_app(&directory);
        app.update();
        let completion = resolve_queued_action(&mut app, false);
        assert!(
            completion
                .1
                .as_ref()
                .unwrap_err()
                .contains("confirmed contact")
        );
        app.update();
        assert_eq!(
            app.world()
                .resource::<CommandResult>()
                .completed_action
                .as_ref(),
            Some(&completion)
        );
        assert_eq!(
            check_action(app.world(), &scenario, &action),
            completion.1.map(|()| true)
        );
    }

    #[test]
    fn native_scenario_action_ignores_stale_or_foreign_completions() {
        let directory = super::super::tests::Directory::new();
        let (mut app, scenario, action) = action_app(&directory);
        app.update();
        let (request, rejection) = resolve_queued_action(&mut app, false);
        app.update();
        let mut foreign = vec![request.clone(); 4];
        foreign[0].command_id = CommandId::deterministic("native/foreign-action");
        foreign[1].expected_sequence += 1;
        foreign[2].actor = forge_core::EntityId::deterministic("native/foreign-actor");
        foreign[3].action = GameAction::Throw {
            object: match &action {
                GameAction::Push { object, .. } => *object,
                _ => unreachable!(),
            },
            impulse: forge_core::Vec3::new(0.0, 0.0, 1.0),
        };
        for request in foreign {
            app.world_mut()
                .resource_mut::<CommandResult>()
                .completed_action = Some((request, rejection.clone()));
            assert_eq!(check_action(app.world(), &scenario, &action), Ok(false));
        }
        app.world_mut()
            .resource_mut::<CommandResult>()
            .completed_action = Some((request, rejection.clone()));
        assert_eq!(
            check_action(app.world(), &scenario, &action),
            rejection.map(|()| true)
        );
    }

    #[test]
    fn native_scenario_action_retry_clears_the_same_id_rejection() {
        let directory = super::super::tests::Directory::new();
        let (mut app, scenario, action) = action_app(&directory);
        app.update();
        let (first, rejection) = resolve_queued_action(&mut app, false);
        app.update();
        assert_eq!(
            check_action(app.world(), &scenario, &action),
            rejection.map(|()| true)
        );
        let mut retry = action_scenario(&action);
        send_command(
            app.world_mut(),
            &mut retry,
            NativeGameplayCommand::Action(action.clone()),
        )
        .unwrap();
        assert_eq!(retry.expected_action, Some(first.command_id));
        app.update();
        assert!(
            app.world()
                .resource::<CommandResult>()
                .completed_action
                .is_none()
        );
        assert!(app.world().resource::<CommandResult>().last_error.is_some());
        assert_eq!(check_action(app.world(), &retry, &action), Ok(false));
        let (second, success) = resolve_queued_action(&mut app, true);
        assert_eq!(second, first);
        assert_eq!(success, Ok(()));
        app.update();
        assert_eq!(check_action(app.world(), &retry, &action), Ok(true));
    }

    #[test]
    fn native_scenario_action_refuses_concurrent_identical_or_different_commands() {
        for identical in [false, true] {
            let directory = super::super::tests::Directory::new();
            let (mut app, scenario, action) = action_app(&directory);
            app.update();
            assert!(resolve_queued_action(&mut app, true).1.is_ok());
            app.update();
            assert_eq!(check_action(app.world(), &scenario, &action), Ok(true));
            let intervening = if identical {
                NativeGameplayCommand::Action(action.clone())
            } else {
                NativeGameplayCommand::Save
            };
            app.world_mut().write_message(intervening);
            app.update();
            assert!(
                check_action(app.world(), &scenario, &action)
                    .unwrap_err()
                    .contains("concurrent command")
            );
        }
    }

    #[test]
    fn native_scenario_rejects_unbounded_plans_and_capture_path_escape() {
        let mut plan = Plan {
            schema: "forge.native-gameplay.scenario".into(),
            version: 1,
            source_hash: ContentHash::from_bytes([1; 32]),
            archive_hash: ContentHash::from_bytes([2; 32]),
            max_duration_s: 60,
            steps: vec![Step::WaitReady { timeout_s: 30 }, Step::Exit],
        };
        assert!(validate(&plan).is_ok());
        plan.steps.insert(
            1,
            Step::Capture {
                name: "../outside.png".into(),
                timeout_s: 30,
            },
        );
        assert!(validate(&plan).is_err());
        plan.steps.remove(1);
        plan.max_duration_s = 601;
        assert!(validate(&plan).is_err());
        plan.max_duration_s = 60;
        plan.steps.insert(
            1,
            Step::Move {
                axis: [f32::NAN, 0.0],
                look_delta: [0.0, 0.0],
                sprint: false,
                jump: false,
                ticks: 1,
                timeout_s: 1,
            },
        );
        assert!(validate(&plan).is_err());
    }
    #[test]
    fn native_scenario_ack_refuses_a_concurrent_different_command() {
        let mut app = App::new();
        app.insert_resource(CommandResult {
            sequence: 1,
            last: Some((
                NativeGameplayCommand::ResumeNextLaunch {
                    allow_compatible_edits: false,
                },
                Ok(()),
            )),
            last_error: None,
            pending_action: None,
            completed_action: None,
        });
        let now = Instant::now();
        let scenario = Scenario {
            plan: Plan {
                schema: "forge.native-gameplay.scenario".into(),
                version: 1,
                source_hash: ContentHash::from_bytes([1; 32]),
                archive_hash: ContentHash::from_bytes([2; 32]),
                max_duration_s: 60,
                steps: vec![Step::Save { timeout_s: 10 }, Step::Exit],
            },
            hash: ContentHash::from_bytes([3; 32]),
            output: PathBuf::new(),
            started: now,
            step_started: now,
            step: 0,
            step_ticks: 0,
            submitted: true,
            command_sequence: 0,
            expected_command: Some(NativeGameplayCommand::Save),
            expected_action: None,
            action_sequence: 0,
            captured: false,
            finished: false,
            sensitivity: 0.01,
            walk_speed: 3.0,
            braking: 12.0,
        };
        assert!(check_command(app.world(), &scenario).is_err());
        app.world_mut().resource_mut::<CommandResult>().last =
            Some((NativeGameplayCommand::Save, Ok(())));
        assert_eq!(check_command(app.world(), &scenario), Ok(true));
    }
    #[test]
    fn native_replay_reads_the_configured_input_profile_instead_of_scene_constants() {
        let mut app = App::new();
        assert!(crate::player_controller::look_sensitivity_radians(app.world()).is_err());
        let mut profile = forge_world::embedded_visual_valley_player_controller_profile().unwrap();
        profile.camera.look_sensitivity_degrees_per_unit = 0.75;
        profile.controller.walk_speed_mps = 4.25;
        profile.controller.braking_mps2 = 12.5;
        app.add_plugins(crate::PlayerControllerPlugin::new(profile, Vec3::ZERO));
        assert!(
            (crate::player_controller::look_sensitivity_radians(app.world()).unwrap()
                - 0.75_f32.to_radians())
            .abs()
                < f32::EPSILON
        );
        assert_eq!(
            crate::player_controller::walk_speed_mps(app.world())
                .unwrap()
                .to_bits(),
            4.25_f32.to_bits()
        );
        assert_eq!(
            crate::player_controller::braking_mps2(app.world())
                .unwrap()
                .to_bits(),
            12.5_f32.to_bits()
        );
    }
}
