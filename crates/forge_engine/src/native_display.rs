//! Shared native window requests. A request is not proof of an OS-applied mode.
use bevy::{
    ecs::system::NonSendMarker,
    prelude::*,
    window::{
        Monitor, MonitorSelection, PrimaryMonitor, PrimaryWindow, RawHandleWrapper, WindowMode,
    },
};
use forge_project::workspace::launcher::DisplayMode;

const MONITOR_WAIT_SECONDS: f64 = 3.0;
const NATIVE_CONFIRM_SECONDS: f64 = 1.0;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum DisplayFallback {
    WindowUnavailable,
    MonitorUnavailable,
    ExclusiveUnavailable,
    NativeRefused,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum DisplayOutcome {
    Waiting,
    /// Bevy received the request; native presentation still requires observation.
    Requested,
    Fallback(DisplayFallback),
    SupersededByPlayer,
}

#[derive(Resource, Debug)]
pub struct InitialDisplayStatus {
    pub requested: DisplayMode,
    pub outcome: DisplayOutcome,
    started_at: Option<f64>,
    native_checked: bool,
}

impl InitialDisplayStatus {
    #[must_use]
    pub const fn new(requested: DisplayMode) -> Self {
        Self {
            requested,
            outcome: DisplayOutcome::Waiting,
            started_at: None,
            native_checked: false,
        }
    }

    #[must_use]
    pub const fn fallback_message(&self) -> Option<&'static str> {
        match self.outcome {
            DisplayOutcome::Fallback(DisplayFallback::WindowUnavailable) => {
                Some("Affichage non confirmé : mode fenêtré demandé.")
            }
            DisplayOutcome::Fallback(DisplayFallback::MonitorUnavailable) => {
                Some("Écran principal indisponible : mode fenêtré demandé.")
            }
            DisplayOutcome::Fallback(DisplayFallback::ExclusiveUnavailable) => {
                Some("Plein écran exclusif non appliqué : mode fenêtré demandé.")
            }
            DisplayOutcome::Fallback(DisplayFallback::NativeRefused) => {
                Some("Mode plein écran refusé : mode fenêtré demandé.")
            }
            DisplayOutcome::Waiting
            | DisplayOutcome::Requested
            | DisplayOutcome::SupersededByPlayer => None,
        }
    }

    /// An explicit player action takes precedence over a pending startup request.
    pub fn supersede_by_player(&mut self) {
        self.outcome = DisplayOutcome::SupersededByPlayer;
        self.native_checked = true;
    }
}

pub struct InitialDisplayPlugin(pub DisplayMode);

impl Plugin for InitialDisplayPlugin {
    fn build(&self, app: &mut App) {
        app.insert_resource(InitialDisplayStatus::new(self.0))
            .add_systems(PreUpdate, request_initial_display);
    }
}

#[derive(Debug, PartialEq, Eq)]
enum Selection {
    Wait,
    Request(WindowMode),
    Fallback(DisplayFallback),
}

fn select_mode(requested: DisplayMode, monitor_available: bool, expired: bool) -> Selection {
    match requested {
        DisplayMode::Windowed => Selection::Request(WindowMode::Windowed),
        DisplayMode::Fullscreen => Selection::Fallback(DisplayFallback::ExclusiveUnavailable),
        DisplayMode::Borderless if monitor_available => {
            Selection::Request(WindowMode::BorderlessFullscreen(MonitorSelection::Primary))
        }
        DisplayMode::Borderless if expired => {
            Selection::Fallback(DisplayFallback::MonitorUnavailable)
        }
        DisplayMode::Borderless => Selection::Wait,
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn request_initial_display(
    time: Res<Time<Real>>,
    mut status: ResMut<InitialDisplayStatus>,
    mut windows: Query<(Entity, &mut Window, Option<&RawHandleWrapper>), With<PrimaryWindow>>,
    monitors: Query<&Monitor, With<PrimaryMonitor>>,
    _main_thread: NonSendMarker,
) {
    if matches!(
        status.outcome,
        DisplayOutcome::Fallback(_) | DisplayOutcome::SupersededByPlayer
    ) || status.native_checked
    {
        return;
    }
    let now = time.elapsed_secs_f64();
    let started = *status.started_at.get_or_insert(now);
    if status.outcome == DisplayOutcome::Requested {
        if status.requested == DisplayMode::Windowed {
            status.native_checked = true;
            return;
        }
        if now - started < NATIVE_CONFIRM_SECONDS {
            return;
        }
        let Ok((entity, mut window, _)) = windows.single_mut() else {
            status.outcome = DisplayOutcome::Fallback(DisplayFallback::WindowUnavailable);
            return;
        };
        let fullscreen = bevy::winit::WINIT_WINDOWS.with(|windows| {
            windows
                .borrow()
                .get_window(entity)
                .map(|window| window.fullscreen().is_some())
        });
        match fullscreen {
            Some(true) => status.native_checked = true,
            Some(false) => {
                window.mode = WindowMode::Windowed;
                status.outcome = DisplayOutcome::Fallback(DisplayFallback::NativeRefused);
                warn!("Native fullscreen was not reported; windowed mode requested");
            }
            None => {
                window.mode = WindowMode::Windowed;
                status.outcome = DisplayOutcome::Fallback(DisplayFallback::WindowUnavailable);
                warn!("Native window unavailable; windowed mode requested");
            }
        }
        return;
    }
    let expired = now - started >= MONITOR_WAIT_SECONDS;
    let Ok((entity, mut window, raw_handle)) = windows.single_mut() else {
        if expired {
            status.outcome = DisplayOutcome::Fallback(DisplayFallback::WindowUnavailable);
            warn!("Native window unavailable; windowed mode requested");
        }
        return;
    };
    // Do not ask for exclusive fullscreen while Bevy is creating the native window.
    if raw_handle.is_none() {
        if expired {
            window.mode = WindowMode::Windowed;
            status.outcome = DisplayOutcome::Fallback(DisplayFallback::WindowUnavailable);
            warn!("Native window unavailable; windowed mode requested");
        }
        return;
    }
    if status.requested == DisplayMode::Borderless {
        let native_monitor = bevy::winit::WINIT_WINDOWS.with(|windows| {
            windows
                .borrow()
                .get_window(entity)
                .is_some_and(|window| window.primary_monitor().is_some())
        });
        if !native_monitor {
            if expired {
                window.mode = WindowMode::Windowed;
                status.outcome = DisplayOutcome::Fallback(DisplayFallback::MonitorUnavailable);
                warn!("Native monitor unavailable; windowed mode requested");
            }
            return;
        }
    }
    match select_mode(status.requested, monitors.single().is_ok(), expired) {
        Selection::Wait => {}
        Selection::Request(mode) => {
            window.mode = mode;
            status.outcome = DisplayOutcome::Requested;
            status.started_at = Some(now);
        }
        Selection::Fallback(reason) => {
            window.mode = WindowMode::Windowed;
            status.outcome = DisplayOutcome::Fallback(reason);
            warn!(
                ?reason,
                "Requested display unavailable; windowed mode requested"
            );
        }
    }
}

/// The same F11 transition used by Tidewater; it does not persist a preference.
#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
pub fn toggle_play_window(
    keys: Res<ButtonInput<KeyCode>>,
    mut windows: Query<&mut Window, With<PrimaryWindow>>,
) {
    if !keys.just_pressed(KeyCode::F11) {
        return;
    }
    let Ok(mut window) = windows.single_mut() else {
        return;
    };
    toggle_window_mode(&mut window);
}

/// Apply Tidewater's windowed/borderless transition to one primary window.
pub fn toggle_window_mode(window: &mut Window) {
    window.mode = if matches!(window.mode, WindowMode::Windowed) {
        WindowMode::BorderlessFullscreen(MonitorSelection::Primary)
    } else {
        WindowMode::Windowed
    };
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::window::VideoModeSelection;

    #[test]
    fn initial_modes_refuse_exclusive_and_keep_a_bounded_windowed_fallback() {
        assert_eq!(
            select_mode(DisplayMode::Windowed, false, false),
            Selection::Request(WindowMode::Windowed)
        );
        assert_eq!(
            select_mode(DisplayMode::Borderless, false, false),
            Selection::Wait
        );
        assert_eq!(
            select_mode(DisplayMode::Borderless, false, true),
            Selection::Fallback(DisplayFallback::MonitorUnavailable)
        );
        assert_eq!(
            select_mode(DisplayMode::Borderless, true, false),
            Selection::Request(WindowMode::BorderlessFullscreen(MonitorSelection::Primary))
        );
        assert_eq!(
            select_mode(DisplayMode::Fullscreen, true, false),
            Selection::Fallback(DisplayFallback::ExclusiveUnavailable)
        );
        let mut status = InitialDisplayStatus::new(DisplayMode::Fullscreen);
        status.outcome = DisplayOutcome::Fallback(DisplayFallback::ExclusiveUnavailable);
        assert!(status.fallback_message().unwrap().contains("non appliqué"));
    }

    #[test]
    fn f11_preserves_windowed_borderless_transition() {
        let mut app = App::new();
        app.init_resource::<ButtonInput<KeyCode>>()
            .add_systems(Update, toggle_play_window);
        let window = app
            .world_mut()
            .spawn((Window::default(), PrimaryWindow))
            .id();
        app.world_mut()
            .resource_mut::<ButtonInput<KeyCode>>()
            .press(KeyCode::F11);
        app.update();
        assert_eq!(
            app.world().get::<Window>(window).unwrap().mode,
            WindowMode::BorderlessFullscreen(MonitorSelection::Primary)
        );
        {
            let mut keys = app.world_mut().resource_mut::<ButtonInput<KeyCode>>();
            keys.reset_all();
            keys.press(KeyCode::F11);
        }
        app.update();
        assert_eq!(
            app.world().get::<Window>(window).unwrap().mode,
            WindowMode::Windowed
        );
        app.world_mut().get_mut::<Window>(window).unwrap().mode =
            WindowMode::Fullscreen(MonitorSelection::Primary, VideoModeSelection::Current);
        {
            let mut keys = app.world_mut().resource_mut::<ButtonInput<KeyCode>>();
            keys.reset_all();
            keys.press(KeyCode::F11);
        }
        app.update();
        assert_eq!(
            app.world().get::<Window>(window).unwrap().mode,
            WindowMode::Windowed
        );
    }

    #[test]
    fn missing_native_window_enters_visible_fallback_without_claiming_success() {
        let mut app = App::new();
        app.insert_resource(Time::<Real>::default())
            .add_plugins(InitialDisplayPlugin(DisplayMode::Fullscreen));
        let window = app
            .world_mut()
            .spawn((Window::default(), PrimaryWindow))
            .id();
        app.update();
        assert_eq!(
            app.world().resource::<InitialDisplayStatus>().outcome,
            DisplayOutcome::Waiting
        );
        app.world_mut()
            .resource_mut::<Time<Real>>()
            .advance_by(std::time::Duration::from_secs(4));
        app.update();
        let status = app.world().resource::<InitialDisplayStatus>();
        assert_eq!(
            status.outcome,
            DisplayOutcome::Fallback(DisplayFallback::WindowUnavailable)
        );
        assert!(status.fallback_message().is_some());
        assert_eq!(
            app.world().get::<Window>(window).unwrap().mode,
            WindowMode::Windowed
        );
    }
}
