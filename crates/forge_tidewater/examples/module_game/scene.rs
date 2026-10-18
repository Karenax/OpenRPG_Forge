use super::input::ModuleGameOptions;
use bevy::{
    asset::AssetPlugin,
    ecs::system::SystemParam,
    input::{keyboard::KeyboardInput, mouse::MouseButtonInput},
    prelude::*,
    window::{CursorGrabMode, CursorOptions, PrimaryWindow},
};
use forge_delivery::module_runtime::LoadedModuleZone;
use forge_engine::{
    DiagnosticsOverlaySettings, PendingPlayerControlCommand, PlayerControllerSet, PlayerInputFocus,
    RuntimeAutomation, VisualValleyPlugin, VisualValleyVegetationPlugin,
    native_gameplay::{NativeGameplayUi, NativeGameplayUiSet, PreparedNativeGameplay},
};

pub fn run(options: ModuleGameOptions, loaded: LoadedModuleZone) -> Result<(), String> {
    let (terrain, vegetation) = loaded.zone.head().effective()?;
    if terrain.sampled_source.is_some() {
        return Err("This generic renderer does not yet support imported raster terrains".into());
    }
    let plan = forge_world::TerrainGenerationPlan::from_document(&terrain)
        .map_err(|error| error.to_string())?;
    let profile = forge_world::embedded_visual_valley_player_controller_profile()
        .map_err(|error| error.to_string())?;
    let gameplay = loaded.zone.head().corrections.gameplay.as_deref();
    let start = loaded.zone.head().corrections.start.as_deref();
    let spawn = if let Some(document) = gameplay {
        let player = document
            .actor(document.player)
            .ok_or("Module player is absent")?;
        Vec3::new(player.position.x, player.position.y, player.position.z)
    } else {
        let (x, z) = start.map_or((0.0, 0.0), |start| (start.x_m, start.z_m));
        Vec3::new(
            x,
            plan.surface_height_at(x, z)
                .map_err(|error| error.to_string())?
                + profile.controller.total_capsule_height_m() * 0.5
                + 0.05,
            z,
        )
    };
    super::output::prepare(
        &options.save_root,
        options.capture.as_deref(),
        &loaded.package_root,
    )?;
    let manual = super::manual::prepare(&loaded)?;
    let capture_frame = options
        .exit_after_frames
        .map_or(120, |last| last.saturating_sub(30).max(1));
    let evidence = super::evidence::prepare(&options, &loaded, &manual, capture_frame)?;
    let prepared_gameplay = PreparedNativeGameplay::from_archive(
        &loaded.archive,
        &options.save_root,
        std::env::var_os("FORGE_GAME_OBSERVATIONS")
            .as_deref()
            .map(std::path::Path::new),
    )?;
    let root = loaded
        .package_root
        .to_str()
        .ok_or("Non-Unicode installed module root")?;
    let mut app = App::new();
    app.add_plugins(DefaultPlugins.set(AssetPlugin { file_path: root.into(), ..default() })
        .set(WindowPlugin { primary_window: Some(Window {
            title: format!("{} | clic : jouer · G : actions · F5 : sauvegarder · F9 : reprendre · Échap : souris", loaded.manifest.name),
            resolution: (1280, 800).into(), ..default()
        }), ..default() }));
    let mut terrain_plugin = VisualValleyPlugin::new(terrain.clone())
        .with_player_controller(profile)
        .with_player_spawn_position(spawn)
        .with_automation(RuntimeAutomation {
            capture_path: options.capture,
            capture_frame,
            exit_after_frames: options.exit_after_frames,
            ..default()
        });
    if let Some(angle) = start.and_then(|start| start.facing_degrees) {
        let angle = angle.to_radians();
        terrain_plugin = terrain_plugin
            .with_player_camera_target(spawn + Vec3::new(-angle.sin(), 0.0, -angle.cos()));
    }
    app.add_plugins((
        forge_engine::finite_perspective::FinitePerspectivePlugin,
        terrain_plugin,
    ));
    app.insert_resource(DiagnosticsOverlaySettings { visible: false });
    if let Some(vegetation) = vegetation {
        app.add_plugins(VisualValleyVegetationPlugin::new(terrain, vegetation));
    }
    super::manual::install(&mut app, manual);
    if let Some(prepared) = prepared_gameplay {
        prepared.install(&mut app)?;
    }
    install_gameplay_visuals(&mut app, &loaded)?;
    if let Some(evidence) = &evidence {
        evidence.install(&mut app);
    }
    install_player_input(&mut app);
    let exit = app.run();
    if let Some(evidence) = evidence {
        evidence.finish(&exit)?;
    }
    if exit.is_success() {
        Ok(())
    } else {
        Err(format!("Native module runtime exited with {exit:?}"))
    }
}

fn install_player_input(app: &mut App) {
    app.insert_resource(PlayerInputFocus(false))
        .add_systems(Update, pointer_capture.after(NativeGameplayUiSet::Input))
        .add_systems(
            FixedUpdate,
            cancel_blocked_player_input.before(PlayerControllerSet::Drive),
        );
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy borrows system parameters"
)]
fn cancel_blocked_player_input(
    keys: Res<ButtonInput<KeyCode>>,
    ui: Option<Res<NativeGameplayUi>>,
    windows: Query<&Window, With<PrimaryWindow>>,
    mut focus: ResMut<PlayerInputFocus>,
    mut pending: ResMut<PendingPlayerControlCommand>,
) {
    // Main samples keyboard before fixed ticks and toggles G's panel in Update.
    // Cancel that frame's queued input before Drive, including the G edge.
    if !windows.single().is_ok_and(|window| window.focused)
        || keys.just_pressed(KeyCode::Escape)
        || ui
            .as_ref()
            .is_some_and(|ui| ui.is_open() || keys.just_pressed(KeyCode::KeyG))
    {
        focus.0 = false;
        pending.cancel_input();
    }
}

fn install_gameplay_visuals(app: &mut App, loaded: &LoadedModuleZone) -> Result<(), String> {
    let Some(document) = loaded.zone.head().corrections.gameplay.as_deref() else {
        return Ok(());
    };
    let models = loaded
        .manifest
        .assets
        .iter()
        .map(
            |asset| forge_engine::gameplay::visuals::GameplayVisualModel {
                pin: forge_project::module_gameplay_visuals::VisualAssetPin {
                    asset_id: asset.asset_id,
                    sha256: asset.sha256,
                },
                asset_path: asset.file_path.clone(),
            },
        )
        .collect::<Vec<_>>();
    let bindings = loaded
        .manifest
        .gameplay_visuals
        .iter()
        .filter(|binding| binding.area_id == document.area_id)
        .cloned()
        .collect::<Vec<_>>();
    forge_engine::gameplay::visuals::install(app, document, &bindings, &models)
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy borrows system parameters"
)]
#[allow(
    clippy::too_many_arguments,
    reason = "The optional diagnostic only observes the existing capture transition"
)]
fn pointer_capture(
    mouse: Res<ButtonInput<MouseButton>>,
    keys: Res<ButtonInput<KeyCode>>,
    ui: Option<Res<NativeGameplayUi>>,
    mut focus: ResMut<PlayerInputFocus>,
    mut control_requested: Local<bool>,
    mut pending: ResMut<PendingPlayerControlCommand>,
    mut windows: Query<(&Window, &mut CursorOptions), With<PrimaryWindow>>,
    mut trace: PlayerInputTrace,
) {
    let focus_before_capture = focus.0;
    let ui_open = ui.as_ref().is_some_and(|ui| ui.is_open());
    let Ok((window, mut cursor)) = windows.single_mut() else {
        *control_requested = false;
        focus.0 = false;
        pending.cancel_input();
        trace.record(
            &keys,
            &mouse,
            InputTraceFlags {
                window_focused: None,
                ui_open,
                control_requested: false,
                focus_before_capture,
                focus_after_capture: focus.0,
            },
        );
        return;
    };
    let blocked = !window.focused
        || keys.just_pressed(KeyCode::Escape)
        || ui.as_ref().is_some_and(|ui| ui.is_open());
    let clicked = mouse.just_pressed(MouseButton::Left);
    if blocked {
        cursor.grab_mode = CursorGrabMode::None;
        cursor.visible = true;
    } else if clicked {
        cursor.grab_mode = CursorGrabMode::Locked;
        cursor.visible = false;
    }
    focus.0 = update_control_intent(&mut control_requested, blocked, clicked, &mut pending);
    trace.record(
        &keys,
        &mouse,
        InputTraceFlags {
            window_focused: Some(window.focused),
            ui_open,
            control_requested: *control_requested,
            focus_before_capture,
            focus_after_capture: focus.0,
        },
    );
}

// Opt-in local diagnostics: no text, logical keys, unrelated keys or OS hooks.
// Update still runs when gameplay has not advanced. Events are read through
// independent readers and never consumed from the actual input systems.
const INPUT_TRACE_KEYS: [KeyCode; 10] = [
    KeyCode::KeyW,
    KeyCode::KeyA,
    KeyCode::KeyS,
    KeyCode::KeyD,
    KeyCode::ShiftLeft,
    KeyCode::ShiftRight,
    KeyCode::Space,
    KeyCode::KeyV,
    KeyCode::KeyG,
    KeyCode::Escape,
];
const INPUT_TRACE_INTERVAL_S: f64 = 0.25;
const INPUT_TRACE_MAX_RECORDS: u16 = 512;

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
#[allow(
    clippy::struct_excessive_bools,
    reason = "These independent observed input barriers are a diagnostic snapshot, not a new state machine"
)]
struct InputTraceFlags {
    window_focused: Option<bool>,
    ui_open: bool,
    control_requested: bool,
    focus_before_capture: bool,
    focus_after_capture: bool,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
struct InputTraceState {
    flags: InputTraceFlags,
    ready: bool,
    held_keys: u16,
    left_mouse: bool,
}

#[derive(Clone, Copy, Debug, Default)]
struct InputTraceEvents {
    pressed: u32,
    released: u32,
    repeated: u32,
}

#[derive(Default)]
struct InputTraceHistory {
    previous: Option<InputTraceState>,
    last_record_s: f64,
    records: u16,
    keys: [InputTraceEvents; INPUT_TRACE_KEYS.len()],
    left_mouse: InputTraceEvents,
}

impl InputTraceHistory {
    fn should_record(&mut self, state: InputTraceState, now_s: f64, event_edge: bool) -> bool {
        if self.records >= INPUT_TRACE_MAX_RECORDS {
            return false;
        }
        let changed = self.previous != Some(state);
        let held = state.held_keys != 0 || state.left_mouse;
        if !(changed || event_edge || held && now_s - self.last_record_s >= INPUT_TRACE_INTERVAL_S)
        {
            return false;
        }
        self.previous = Some(state);
        self.last_record_s = now_s;
        self.records += 1;
        true
    }
}

#[derive(SystemParam)]
struct PlayerInputTrace<'w, 's> {
    keyboard: MessageReader<'w, 's, KeyboardInput>,
    mouse: MessageReader<'w, 's, MouseButtonInput>,
    clock: Res<'w, Time<Real>>,
    frame: Res<'w, bevy::diagnostic::FrameCount>,
    ready: Res<'w, forge_engine::PlayerSimulationReady>,
    player: Res<'w, forge_engine::PlayerRuntimeSnapshot>,
    history: Local<'s, InputTraceHistory>,
}

impl PlayerInputTrace<'_, '_> {
    fn record(
        &mut self,
        keys: &ButtonInput<KeyCode>,
        mouse: &ButtonInput<MouseButton>,
        flags: InputTraceFlags,
    ) {
        if !tracing::enabled!(target: "forge::input", tracing::Level::DEBUG)
            || self.history.records >= INPUT_TRACE_MAX_RECORDS
        {
            self.keyboard.clear();
            self.mouse.clear();
            return;
        }
        let mut event_edge = false;
        for event in self.keyboard.read() {
            let Some(index) = INPUT_TRACE_KEYS
                .iter()
                .position(|key| *key == event.key_code)
            else {
                continue;
            };
            let counts = &mut self.history.keys[index];
            if event.repeat {
                counts.repeated = counts.repeated.saturating_add(1);
            } else if event.state.is_pressed() {
                counts.pressed = counts.pressed.saturating_add(1);
                event_edge = true;
            } else {
                counts.released = counts.released.saturating_add(1);
                event_edge = true;
            }
        }
        for event in self
            .mouse
            .read()
            .filter(|event| event.button == MouseButton::Left)
        {
            let counts = &mut self.history.left_mouse;
            if event.state.is_pressed() {
                counts.pressed = counts.pressed.saturating_add(1);
            } else {
                counts.released = counts.released.saturating_add(1);
            }
            event_edge = true;
        }
        let state = InputTraceState {
            flags,
            ready: self.ready.0,
            held_keys: INPUT_TRACE_KEYS
                .iter()
                .enumerate()
                .fold(0, |mask, (index, key)| {
                    mask | (u16::from(keys.pressed(*key)) << index)
                }),
            left_mouse: mouse.pressed(MouseButton::Left),
        };
        let elapsed_s = self.clock.elapsed_secs_f64();
        if self.history.should_record(state, elapsed_s, event_edge) {
            tracing::debug!(target: "forge::input", frame = self.frame.0, tick = self.player.fixed_tick,
                elapsed_s, input = ?state, key_order = ?INPUT_TRACE_KEYS,
                keyboard_events = ?self.history.keys, left_mouse_events = ?self.history.left_mouse,
                position = ?self.player.player_position, desired_translation = ?self.player.desired_translation,
                effective_translation = ?self.player.effective_translation, grounded = self.player.grounded,
                collisions = self.player.collisions, last_applied_sequence = ?self.player.last_applied_sequence,
                diagnostic_limit_reached = self.history.records == INPUT_TRACE_MAX_RECORDS,
                "native player input observation");
            self.history.keys.fill(InputTraceEvents::default());
            self.history.left_mouse = InputTraceEvents::default();
        }
    }
}

fn update_control_intent(
    control_requested: &mut bool,
    blocked: bool,
    clicked: bool,
    pending: &mut PendingPlayerControlCommand,
) -> bool {
    if blocked {
        *control_requested = false;
        pending.cancel_input();
    } else if clicked {
        *control_requested = true;
    }
    // Bevy restores grab_mode after an OS refusal. Keyboard ownership follows
    // the player's click until explicitly released, even when locking is denied.
    *control_requested
}

#[cfg(test)]
mod tests;
