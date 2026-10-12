#![deny(unsafe_code)]

use forge_launcher_frontend::{LauncherCommand, LauncherResponse, LauncherSettings, WindowAction};
use std::{ffi::OsStr, process::Child, sync::Mutex};

#[path = "installed_play.rs"]
mod play;
mod webview_probe;

#[derive(Default)]
struct LauncherState {
    settings: Mutex<LauncherSettings>,
    game: Mutex<Option<Child>>,
}

#[tauri::command]
#[allow(
    clippy::needless_pass_by_value,
    reason = "Tauri deserializes command payloads and window handles into owned values"
)]
fn execute_launcher_command(
    command: LauncherCommand,
    window: tauri::WebviewWindow,
    state: tauri::State<'_, LauncherState>,
) -> LauncherResponse {
    if let LauncherCommand::Window { action } = command {
        return execute_window_action(&window, action);
    }
    execute_state_command(command, &state)
}

fn execute_state_command(command: LauncherCommand, state: &LauncherState) -> LauncherResponse {
    match command {
        LauncherCommand::Snapshot => match state.settings.lock() {
            Ok(settings) => LauncherResponse::Snapshot {
                settings: *settings,
                runtime_connected: play::runtime_connected(),
                prototype_version: env!("CARGO_PKG_VERSION").to_owned(),
            },
            Err(_) => LauncherResponse::Rejected {
                reason: "L’état des préférences est temporairement indisponible.".to_owned(),
            },
        },
        LauncherCommand::SaveSettings { settings } => match settings.validate() {
            Ok(validated) => match state.settings.lock() {
                Ok(mut current) => {
                    *current = validated;
                    LauncherResponse::SettingsSaved {
                        settings: validated,
                    }
                }
                Err(_) => LauncherResponse::Rejected {
                    reason: "Impossible de verrouiller les préférences du prototype.".to_owned(),
                },
            },
            Err(reason) => LauncherResponse::Rejected { reason },
        },
        LauncherCommand::RequestLaunch => play::launch(&state.game),
        LauncherCommand::Window { .. } => LauncherResponse::Rejected {
            reason: "L’action de fenêtre doit être traitée par la fenêtre appelante.".to_owned(),
        },
    }
}

fn execute_window_action(window: &tauri::WebviewWindow, action: WindowAction) -> LauncherResponse {
    let result = match action {
        WindowAction::Minimize => window.minimize(),
        WindowAction::ToggleMaximize => match window.is_maximized() {
            Ok(true) => window.unmaximize(),
            Ok(false) => window.maximize(),
            Err(error) => Err(error),
        },
        WindowAction::Close => window.close(),
    };
    match result {
        Ok(()) => LauncherResponse::WindowActionAccepted { action },
        Err(error) => LauncherResponse::Rejected {
            reason: format!("Action de fenêtre impossible : {error}"),
        },
    }
}

fn launcher_context() -> tauri::Context<tauri::Wry> {
    tauri::generate_context!()
}

fn embedded_frontend_ready(context: &tauri::Context<tauri::Wry>) -> bool {
    let assets = context.assets();
    let Some(index) = assets.get(&"index.html".into()) else {
        return false;
    };
    let Ok(html) = std::str::from_utf8(&index) else {
        return false;
    };
    html.contains("<title>OpenRPG Forge — L’Aube des royaumes</title>")
        && html.contains("src=\"desktop.js\"")
        && ["desktop.js", "styles.css", "content.js", "app.js"]
            .iter()
            .all(|name| {
                assets
                    .get(&(*name).into())
                    .is_some_and(|bytes| !bytes.is_empty())
            })
}

fn main() {
    let mut context = launcher_context();
    if std::env::args_os().nth(1).as_deref() == Some(OsStr::new("--verify-game-launch")) {
        if !cfg!(feature = "production") || !embedded_frontend_ready(&context) {
            std::process::exit(1);
        }
        // Exercise the exact application command used by the renderer button.
        let result =
            execute_state_command(LauncherCommand::RequestLaunch, &LauncherState::default());
        println!(
            "{}",
            serde_json::to_string(&result).expect("launcher response")
        );
        if !matches!(result, LauncherResponse::LaunchStarted { .. }) {
            std::process::exit(1);
        }
        return;
    }
    if std::env::args_os().nth(1).as_deref() == Some(OsStr::new("--verify-distributable")) {
        if cfg!(feature = "production") {
            if !embedded_frontend_ready(&context) {
                eprintln!(
                    "L’interface Aube et son raccord desktop sont absents du contexte embarqué."
                );
                std::process::exit(1);
            }
            if std::env::args_os().nth(2).as_deref() == Some(OsStr::new("--debris-runtime")) {
                println!("FORGE_LAUNCHER_DEBRIS_RUNTIME_V1");
            } else if std::env::args_os().nth(2).as_deref() == Some(OsStr::new("--pebble-runtime"))
            {
                println!("FORGE_LAUNCHER_PEBBLE_RUNTIME_V1");
            } else if std::env::args_os().nth(2).as_deref()
                == Some(OsStr::new("--source-fish-runtime"))
            {
                println!("FORGE_LAUNCHER_SOURCE_FISH_RUNTIME_V1");
            } else {
                println!("FORGE_LAUNCHER_AUBE_FRONTEND_READY");
            }
            return;
        }
        eprintln!("Ce lanceur dépend du serveur de développement local.");
        std::process::exit(1);
    }
    let probe = webview_probe::Probe::parse().unwrap_or_else(|error| {
        eprintln!("{error}");
        std::process::exit(1);
    });
    probe.configure(&mut context);
    tauri::Builder::default()
        .manage(LauncherState::default())
        .manage(probe)
        .setup(webview_probe::setup)
        .on_page_load(webview_probe::loaded)
        .on_window_event(webview_probe::window_event)
        .invoke_handler(tauri::generate_handler![
            execute_launcher_command,
            webview_probe::complete_launcher_probe
        ])
        .run(context)
        .expect("failed to run the OpenRPG Forge launcher prototype");
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_launcher_frontend::{DisplayMode, QualityPreset};

    #[test]
    #[cfg(feature = "production")]
    fn production_context_contains_the_embedded_aube_frontend() {
        assert!(embedded_frontend_ready(&launcher_context()));
    }

    #[test]
    fn snapshot_reports_the_runtime_boundary_truthfully() {
        let state = LauncherState::default();
        assert!(matches!(
            execute_state_command(LauncherCommand::Snapshot, &state),
            LauncherResponse::Snapshot {
                runtime_connected,
                ..
            } if runtime_connected == play::runtime_connected()
        ));
    }

    #[test]
    fn valid_settings_are_kept_for_the_current_session() {
        let state = LauncherState::default();
        let updated = LauncherSettings {
            quality: QualityPreset::High,
            display_mode: DisplayMode::Fullscreen,
            master_volume: 73,
            music_volume: 51,
            reduce_motion: true,
            subtitles: true,
        };
        assert_eq!(
            execute_state_command(LauncherCommand::SaveSettings { settings: updated }, &state,),
            LauncherResponse::SettingsSaved { settings: updated }
        );
        assert!(matches!(
            execute_state_command(LauncherCommand::Snapshot, &state),
            LauncherResponse::Snapshot { settings, .. } if settings == updated
        ));
    }

    #[test]
    fn absent_runtime_is_reported_before_launch() {
        if play::runtime_connected() {
            return;
        }
        let state = LauncherState::default();
        assert!(
            matches!(execute_state_command(LauncherCommand::RequestLaunch, &state), LauncherResponse::LaunchBlocked { code, .. } if code == "runtime-missing")
        );
    }
}
