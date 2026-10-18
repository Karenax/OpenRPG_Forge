use super::*;
use bevy::input::{
    ButtonState, InputPlugin,
    keyboard::{Key, KeyboardFocusLost},
};
use bevy::time::TimeUpdateStrategy;
use bevy_rapier3d::prelude::{
    KinematicCharacterControllerOutput, NoUserData, RapierPhysicsPlugin, TimestepMode,
};
use forge_engine::{
    ForgePlayerCharacter, PlayerCameraMode, PlayerControllerPlugin, PlayerRuntimeSnapshot,
};
use std::time::Duration;

fn test_app() -> App {
    let mut app = App::new();
    app.add_plugins((MinimalPlugins, TransformPlugin))
        .insert_resource(Time::<Fixed>::from_hz(60.0))
        .insert_resource(TimeUpdateStrategy::ManualDuration(Duration::from_secs_f64(
            1.0 / 60.0,
        )))
        .insert_resource(TimestepMode::Fixed {
            dt: 1.0 / 60.0,
            substeps: 1,
        })
        .add_plugins(RapierPhysicsPlugin::<NoUserData>::default().in_fixed_schedule())
        .add_plugins(PlayerControllerPlugin::new(
            forge_world::embedded_visual_valley_player_controller_profile().unwrap(),
            Vec3::Y * 2.0,
        ))
        .insert_resource(PlayerInputFocus(false))
        .init_resource::<ButtonInput<KeyCode>>()
        .init_resource::<ButtonInput<MouseButton>>()
        .init_resource::<bevy::diagnostic::FrameCount>()
        .add_message::<KeyboardInput>()
        .add_message::<MouseButtonInput>();
    install_player_input(&mut app);
    app.world_mut().spawn((
        Window {
            focused: true,
            ..default()
        },
        PrimaryWindow,
        CursorOptions::default(),
    ));
    app.update();
    app
}

fn click_to_play(app: &mut App) {
    app.world_mut()
        .resource_mut::<ButtonInput<MouseButton>>()
        .press(MouseButton::Left);
    app.update();
    app.world_mut()
        .resource_mut::<ButtonInput<MouseButton>>()
        .reset_all();
    assert!(app.world().resource::<PlayerInputFocus>().0);
}

fn queue_keyboard_actions(app: &mut App) {
    let mut keys = app.world_mut().resource_mut::<ButtonInput<KeyCode>>();
    keys.reset_all();
    keys.press(KeyCode::KeyD);
    keys.press(KeyCode::KeyV);
    app.world_mut().run_schedule(PreUpdate);
}

fn snapshot(app: &App) -> PlayerRuntimeSnapshot {
    app.world().resource::<PlayerRuntimeSnapshot>().clone()
}

fn assert_stationary_third_person(app: &App) {
    let observed = snapshot(app);
    assert_eq!(observed.horizontal_velocity, Vec3::ZERO);
    assert_eq!(observed.desired_translation.x, 0.0);
    assert_eq!(observed.camera_mode, PlayerCameraMode::ThirdPerson);
}

#[test]
fn keyboard_controls_survive_os_grab_rollback_after_a_real_input_click() {
    let mut app = test_app();
    queue_keyboard_actions(&mut app);
    app.world_mut().run_schedule(FixedUpdate);
    assert_stationary_third_person(&app);
    app.world_mut()
        .resource_mut::<ButtonInput<KeyCode>>()
        .reset_all();

    click_to_play(&mut app);
    let world = app.world_mut();
    let mut cursor = world
        .query::<&mut CursorOptions>()
        .single_mut(world)
        .unwrap();
    assert_eq!(cursor.grab_mode, CursorGrabMode::Locked);
    assert!(!cursor.visible);
    // Reproduce Bevy's component rollback after a denied native grab. This is
    // a CPU regression, not a claim to reproduce a kernel permission failure.
    cursor.grab_mode = CursorGrabMode::None;
    app.world_mut().run_schedule(Update);
    assert!(app.world().resource::<PlayerInputFocus>().0);

    queue_keyboard_actions(&mut app);
    app.world_mut().run_schedule(FixedUpdate);
    let observed = snapshot(&app);
    assert!(observed.horizontal_velocity.x > 0.0);
    assert!(observed.desired_translation.x > 0.0);
    assert_eq!(observed.camera_mode, PlayerCameraMode::FirstPerson);
    assert!(observed.last_applied_sequence.is_some());
    let world = app.world_mut();
    let output = world
        .query_filtered::<&KinematicCharacterControllerOutput, With<ForgePlayerCharacter>>()
        .single(world)
        .unwrap();
    assert!(output.desired_translation.x > 0.0);
    assert!(output.effective_translation.x > 0.0);
}

fn assert_window_release_cancels_pending(escape: bool) {
    let mut app = test_app();
    click_to_play(&mut app);
    let before = snapshot(&app);
    // The real Main schedule must sample D/V before its fixed loop. Do not
    // arrange Update before FixedUpdate: that would conceal the release race.
    let mut keys = app.world_mut().resource_mut::<ButtonInput<KeyCode>>();
    keys.reset_all();
    keys.press(KeyCode::KeyD);
    keys.press(KeyCode::KeyV);
    if escape {
        app.world_mut()
            .resource_mut::<ButtonInput<KeyCode>>()
            .press(KeyCode::Escape);
    } else {
        let world = app.world_mut();
        world
            .query::<&mut Window>()
            .single_mut(world)
            .unwrap()
            .focused = false;
    }
    app.update();
    assert!(!app.world().resource::<PlayerInputFocus>().0);
    assert_stationary_third_person(&app);
    let after = snapshot(&app);
    assert!(
        after.fixed_tick > before.fixed_tick,
        "Main must actually run a fixed tick"
    );
    assert_eq!(after.last_applied_sequence, before.last_applied_sequence);

    app.world_mut()
        .resource_mut::<ButtonInput<KeyCode>>()
        .reset_all();
    let world = app.world_mut();
    world
        .query::<&mut Window>()
        .single_mut(world)
        .unwrap()
        .focused = true;
    app.update();
    assert!(!app.world().resource::<PlayerInputFocus>().0);
    queue_keyboard_actions(&mut app);
    app.world_mut().run_schedule(FixedUpdate);
    assert_stationary_third_person(&app);
    click_to_play(&mut app);
    queue_keyboard_actions(&mut app);
    app.world_mut().run_schedule(FixedUpdate);
    assert!(snapshot(&app).desired_translation.x > 0.0);
    assert_eq!(snapshot(&app).camera_mode, PlayerCameraMode::FirstPerson);
}

#[test]
fn escape_cancels_queued_actions_and_requires_a_new_click() {
    assert_window_release_cancels_pending(true);
}

#[test]
fn losing_window_focus_cancels_queued_actions_and_requires_a_new_click() {
    assert_window_release_cancels_pending(false);
}

#[test]
fn open_ui_cancels_queued_actions_and_closing_it_does_not_reacquire_control() {
    let mut app = test_app();
    click_to_play(&mut app);
    queue_keyboard_actions(&mut app);
    let before = snapshot(&app).last_applied_sequence;
    let mut requested = true;
    // The production system maps NativeGameplayUi::is_open() to this same
    // blocked transition, after the G handler has updated the actual panel.
    let focus = update_control_intent(
        &mut requested,
        true,
        true,
        &mut app
            .world_mut()
            .resource_mut::<PendingPlayerControlCommand>(),
    );
    app.world_mut().resource_mut::<PlayerInputFocus>().0 = focus;
    assert!(!focus && !requested);
    app.world_mut().run_schedule(FixedUpdate);
    assert_stationary_third_person(&app);
    assert_eq!(snapshot(&app).last_applied_sequence, before);
    let focus = update_control_intent(
        &mut requested,
        false,
        false,
        &mut app
            .world_mut()
            .resource_mut::<PendingPlayerControlCommand>(),
    );
    app.world_mut().resource_mut::<PlayerInputFocus>().0 = focus;
    queue_keyboard_actions(&mut app);
    app.world_mut().run_schedule(FixedUpdate);
    assert!(!focus);
    assert_stationary_third_person(&app);
    let focus = update_control_intent(
        &mut requested,
        false,
        true,
        &mut app
            .world_mut()
            .resource_mut::<PendingPlayerControlCommand>(),
    );
    app.world_mut().resource_mut::<PlayerInputFocus>().0 = focus;
    queue_keyboard_actions(&mut app);
    app.world_mut().run_schedule(FixedUpdate);
    assert!(snapshot(&app).desired_translation.x > 0.0);
    assert_eq!(snapshot(&app).camera_mode, PlayerCameraMode::FirstPerson);
}

fn input_event_app() -> App {
    let mut app = test_app();
    app.add_plugins(InputPlugin);
    let window = primary_window(&mut app);
    app.world_mut().write_message(MouseButtonInput {
        button: MouseButton::Left,
        state: ButtonState::Pressed,
        window,
    });
    app.update();
    app.world_mut().write_message(MouseButtonInput {
        button: MouseButton::Left,
        state: ButtonState::Released,
        window,
    });
    app.update();
    assert!(app.world().resource::<PlayerInputFocus>().0);
    app
}

fn primary_window(app: &mut App) -> Entity {
    let world = app.world_mut();
    world
        .query_filtered::<Entity, With<PrimaryWindow>>()
        .single(world)
        .unwrap()
}

fn forward_event(app: &mut App, state: ButtonState) {
    let window = primary_window(app);
    // Physical KeyW is labelled Z on AZERTY. The sampler consumes the physical
    // code; no repeated press, resource press(), or scenario injection follows.
    app.world_mut().write_message(KeyboardInput {
        key_code: KeyCode::KeyW,
        logical_key: Key::Character("z".into()),
        state,
        text: None,
        repeat: false,
        window,
    });
}

#[test]
fn one_keyboard_event_keeps_walking_across_main_frames_until_release() {
    let mut app = input_event_app();
    let world = app.world_mut();
    world
        .query::<&mut CursorOptions>()
        .single_mut(world)
        .unwrap()
        .grab_mode = CursorGrabMode::None;
    let start = snapshot(&app);
    forward_event(&mut app, ButtonState::Pressed);
    for _ in 0..60 {
        let before = snapshot(&app);
        app.update();
        let after = snapshot(&app);
        assert!(after.fixed_tick > before.fixed_tick);
        assert!(after.player_position.z < before.player_position.z);
        assert!(after.desired_translation.z < 0.0);
        assert!(after.effective_translation.z < 0.0);
        assert!(
            app.world()
                .resource::<ButtonInput<KeyCode>>()
                .pressed(KeyCode::KeyW)
        );
        assert!(app.world().resource::<PlayerInputFocus>().0);
    }
    assert!(start.player_position.z - snapshot(&app).player_position.z > 1.0);
    forward_event(&mut app, ButtonState::Released);
    for _ in 0..30 {
        app.update();
    }
    assert!(
        !app.world()
            .resource::<ButtonInput<KeyCode>>()
            .pressed(KeyCode::KeyW)
    );
    assert_eq!(snapshot(&app).horizontal_velocity, Vec3::ZERO);
    let stopped = snapshot(&app).player_position.z;
    for _ in 0..10 {
        app.update();
    }
    assert_eq!(snapshot(&app).player_position.z, stopped);
}

#[test]
fn keyboard_focus_loss_releases_a_held_event_and_requires_a_new_click() {
    let mut app = input_event_app();
    forward_event(&mut app, ButtonState::Pressed);
    for _ in 0..15 {
        app.update();
    }
    assert!(snapshot(&app).horizontal_velocity.length() > 0.0);
    let world = app.world_mut();
    world
        .query::<&mut Window>()
        .single_mut(world)
        .unwrap()
        .focused = false;
    app.world_mut().write_message(KeyboardFocusLost);
    app.update();
    assert!(
        !app.world()
            .resource::<ButtonInput<KeyCode>>()
            .pressed(KeyCode::KeyW)
    );
    assert!(!app.world().resource::<PlayerInputFocus>().0);
    // The ordinary controller brakes; cancelling input does not teleport it.
    for _ in 0..30 {
        app.update();
    }
    assert_eq!(snapshot(&app).horizontal_velocity, Vec3::ZERO);
    let stopped = snapshot(&app).player_position.z;
    let world = app.world_mut();
    world
        .query::<&mut Window>()
        .single_mut(world)
        .unwrap()
        .focused = true;
    forward_event(&mut app, ButtonState::Pressed);
    for _ in 0..10 {
        app.update();
        assert!(!app.world().resource::<PlayerInputFocus>().0);
        assert_eq!(snapshot(&app).player_position.z, stopped);
    }
    let window = primary_window(&mut app);
    app.world_mut().write_message(MouseButtonInput {
        button: MouseButton::Left,
        state: ButtonState::Pressed,
        window,
    });
    app.update();
    app.update();
    assert!(app.world().resource::<PlayerInputFocus>().0);
    assert!(snapshot(&app).player_position.z < stopped);
}

#[test]
fn input_trace_cadence_ignores_unchanged_frames_and_stops_at_its_total_limit() {
    let state = InputTraceState {
        flags: InputTraceFlags::default(),
        ready: true,
        held_keys: 1,
        left_mouse: false,
    };
    let mut history = InputTraceHistory::default();
    assert!(history.should_record(state, 0.0, false));
    for frame in 1..=100 {
        history.should_record(state, f64::from(frame) / 100.0, false);
    }
    assert!(
        (4..=5).contains(&history.records),
        "held input must stay at most 4 Hz after its initial record"
    );
    let idle = InputTraceState {
        held_keys: 0,
        ..state
    };
    assert!(history.should_record(idle, 1.01, true));
    assert!(!history.should_record(idle, 100.0, false));
    let mut limited = InputTraceHistory::default();
    for index in 0..INPUT_TRACE_MAX_RECORDS {
        assert!(limited.should_record(state, f64::from(index), true));
    }
    assert!(!limited.should_record(idle, 1000.0, true));
    assert_eq!(limited.records, INPUT_TRACE_MAX_RECORDS);
}
