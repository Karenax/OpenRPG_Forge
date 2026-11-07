#![deny(unsafe_code)]

use forge_launcher_frontend::{LauncherCommand, LauncherResponse, LauncherSettings, WindowAction};
use std::{
    ffi::OsStr,
    path::PathBuf,
    sync::{Arc, Mutex},
};
use tauri::Manager;

mod installer;
mod maintenance;
#[path = "installed_play.rs"]
mod play;
mod webview_probe;
mod webview_profile;

#[derive(Default)]
struct LauncherState {
    settings: Mutex<LauncherSettings>,
    game: Mutex<play::GameProcess>,
    installer: Mutex<installer::Installer>,
    rollback: Mutex<Option<forge_delivery::install::Plan>>,
    uninstaller: Option<Mutex<maintenance::Session>>,
    startup_error: Option<String>,
    expected_game: Option<forge_delivery::standalone::ProjectId>,
    expected_user_root: Option<String>,
    game_profile: Option<PathBuf>,
}

#[tauri::command]
#[allow(
    clippy::needless_pass_by_value,
    reason = "Tauri deserializes command payloads and window handles into owned values"
)]
async fn execute_launcher_command(
    command: LauncherCommand,
    window: tauri::WebviewWindow,
    state: tauri::State<'_, Arc<LauncherState>>,
) -> Result<LauncherResponse, String> {
    if let LauncherCommand::Window { action } = &command {
        return Ok(execute_window_action(&window, *action));
    }
    // Inventory and admission hash installed files; keep this work off the UI thread.
    let state = Arc::clone(state.inner());
    let response =
        tauri::async_runtime::spawn_blocking(move || execute_state_command(command, &state))
            .await
            .map_err(|error| format!("La commande du launcher n’a pas abouti : {error}"))?;
    if let LauncherResponse::GameView { game, .. } = &response {
        if let Err(error) = window.set_title(&game.title) {
            eprintln!("Titre de la fenêtre indisponible : {error}");
        }
        if let Some(icon) = &game.presentation.icon {
            // The native package reader already admitted these exact raster bytes
            // and their image allocation limits; no path or URL comes from JS.
            use base64::Engine;
            let result = icon
                .split_once(',')
                .ok_or_else(|| "Icône du jeu invalide".to_owned())
                .and_then(|(_, encoded)| {
                    base64::engine::general_purpose::STANDARD
                        .decode(encoded)
                        .map_err(|error| error.to_string())
                })
                .and_then(|bytes| {
                    tauri::image::Image::from_bytes(&bytes).map_err(|error| error.to_string())
                })
                .and_then(|image| window.set_icon(image).map_err(|error| error.to_string()));
            if let Err(error) = result {
                eprintln!("Icône de la fenêtre indisponible : {error}");
            }
        } else if let Some(icon) = window.app_handle().default_window_icon()
            && let Err(error) = window.set_icon(icon.clone())
        {
            eprintln!("Icône de la fenêtre indisponible : {error}");
        }
    }
    Ok(response)
}

fn execute_state_command(command: LauncherCommand, state: &LauncherState) -> LauncherResponse {
    if let Some(reason) = &state.startup_error {
        return LauncherResponse::Rejected {
            reason: reason.clone(),
        };
    }
    if let Some(session) = &state.uninstaller {
        return match command {
            LauncherCommand::InstallerStatus => maintenance::status(session),
            LauncherCommand::ApplyUninstall => maintenance::apply(session),
            LauncherCommand::CleanupUninstallHelper => maintenance::cleanup(session),
            _ => LauncherResponse::Rejected {
                reason: "Cette fenêtre sert uniquement à désinstaller le jeu.".into(),
            },
        };
    }
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
        LauncherCommand::GameView => {
            play::game_view(state.expected_game, state.expected_user_root.as_deref())
        }
        LauncherCommand::InstallerStatus => installer::status(&state.installer),
        LauncherCommand::PrepareInstall { destination } => {
            installer::prepare(&state.installer, &destination)
        }
        LauncherCommand::ApplyInstall => installer::apply(&state.installer),
        LauncherCommand::EnsurePlayerEntry => installer::ensure_entry(&state.installer),
        LauncherCommand::ReverifyInstalled => installer::reverify_installed(&state.installer),
        LauncherCommand::LaunchInstalled => installer::launch(&state.installer),
        LauncherCommand::PrepareRollback => play::prepare_rollback(
            &state.game,
            &state.rollback,
            state.expected_game,
            state.expected_user_root.as_deref(),
        ),
        LauncherCommand::ApplyRollback => play::apply_rollback(
            &state.game,
            &state.rollback,
            state.expected_game,
            state.expected_user_root.as_deref(),
        ),
        LauncherCommand::StartUninstall => play::start_uninstall(
            &state.game,
            state.expected_game,
            state.expected_user_root.as_deref(),
        ),
        LauncherCommand::ApplyUninstall | LauncherCommand::CleanupUninstallHelper => {
            LauncherResponse::Rejected {
                reason: "Programme de désinstallation absent.".into(),
            }
        }
        LauncherCommand::SaveSettings { settings } => save_settings(state, settings),
        LauncherCommand::RequestLaunch => play::launch(
            &state.game,
            state.expected_game,
            state.expected_user_root.as_deref(),
        ),
        LauncherCommand::RequestEntryLaunch { selection } => play::launch_entry(
            &state.game,
            selection,
            state.expected_game,
            state.expected_user_root.as_deref(),
        ),
        LauncherCommand::ListModules => {
            play::modules(state.expected_game, state.expected_user_root.as_deref())
        }
        LauncherCommand::RequestModuleLaunch { selection } => play::launch_module(
            &state.game,
            selection,
            state.expected_game,
            state.expected_user_root.as_deref(),
        ),
        LauncherCommand::RequestResume { resume } => play::resume(
            &state.game,
            resume,
            state.expected_game,
            state.expected_user_root.as_deref(),
        ),
        LauncherCommand::ReviewLegacySave { selection } => play::legacy_save(
            &state.game,
            selection,
            None,
            state.expected_game,
            state.expected_user_root.as_deref(),
        ),
        LauncherCommand::ImportLegacySave { resume } => play::legacy_save(
            &state.game,
            resume.selection,
            Some(resume),
            state.expected_game,
            state.expected_user_root.as_deref(),
        ),
        LauncherCommand::GameStatus { launch_id } => play::game_status(&state.game, launch_id),
        LauncherCommand::Window { .. } => LauncherResponse::Rejected {
            reason: "L’action de fenêtre doit être traitée par la fenêtre appelante.".to_owned(),
        },
    }
}

fn save_settings(state: &LauncherState, settings: LauncherSettings) -> LauncherResponse {
    match settings.validate() {
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
    let mut context = tauri::generate_context!();
    if embedded_frontend_profile(&context) == Some("standalone") {
        for window in &mut context.config_mut().app.windows {
            window.title = "Jeu".into();
        }
    }
    context
}

fn embedded_frontend_ready(context: &tauri::Context<tauri::Wry>) -> bool {
    embedded_frontend_profile(context).is_some()
}

fn embedded_frontend_profile(context: &tauri::Context<tauri::Wry>) -> Option<&'static str> {
    let assets = context.assets();
    let index = assets.get(&"index.html".into())?;
    let html = std::str::from_utf8(&index).ok()?;
    let has = |name: &str| {
        assets
            .get(&name.into())
            .is_some_and(|bytes| !bytes.is_empty())
    };
    if !html.contains("src=\"desktop.js\"") || !has("desktop.js") || !has("styles.css") {
        return None;
    }
    #[cfg(feature = "standalone")]
    {
        if html.contains("<html lang=\"fr\" data-forge-launcher=\"standalone\">")
            && html.contains("<title>Jeu</title>")
            && context.config().product_name.as_deref() == Some("OpenRPG Forge")
            && context.config().app.windows.len() == 1
            && context.config().app.windows[0].title == "Jeu"
            && !has("content.js")
            && !has("app.js")
            && !has("aube.js")
            && !has("assets/aube.png")
        {
            return Some("standalone");
        }
    }
    #[cfg(not(feature = "standalone"))]
    {
        if html.contains("<title>OpenRPG Forge — L’Aube des royaumes</title>")
            && has("content.js")
            && has("app.js")
            && has("aube.js")
        {
            return Some("aube");
        }
    }
    None
}

fn initial_launcher_state() -> Option<LauncherState> {
    let mut state = LauncherState::default();
    if cfg!(feature = "standalone") {
        match std::env::current_exe()
            .map_err(|error| error.to_string())
            .and_then(|path| maintenance::detect(&path))
        {
            Ok(Some(session)) => {
                state.uninstaller = Some(session);
                return Some(state);
            }
            Ok(None) => {}
            Err(error) => {
                state.startup_error = Some(error);
                return Some(state);
            }
        }
        match installer::durable_location() {
            Ok(Some((root, executable))) => {
                match forge_delivery::launch::start_player_entry(&root, &executable) {
                    Ok(_) => return None,
                    Err(error) => {
                        state.startup_error = Some(format!(
                            "Ouverture impossible. Vérifiez l’installation, puis fermez et rouvrez le lanceur. Détails : {error}"
                        ));
                    }
                }
            }
            Ok(None) => match play::verify_expected_startup() {
                Ok(Some((expected, profile, user_root))) => {
                    state.expected_game = Some(expected);
                    state.expected_user_root = user_root;
                    state.game_profile = profile;
                }
                Ok(None) => {}
                Err(error) => state.startup_error = Some(error),
            },
            Err(error) => state.startup_error = Some(error),
        }
    }
    Some(state)
}

fn setup_profile_window(
    app: &tauri::App,
    profile: Option<PathBuf>,
    profile_window: Option<tauri::utils::config::WindowConfig>,
    require_fresh_profile: bool,
    app_id: Option<&str>,
) -> Result<(), Box<dyn std::error::Error>> {
    match (profile, profile_window) {
        (Some(profile), Some(template)) => webview_profile::create_after_validation(
            || -> Result<(), Box<dyn std::error::Error>> {
                let historical = app.path().app_local_data_dir()?;
                webview_profile::validate(&profile, &historical, require_fresh_profile, app_id)
                    .map_err(std::io::Error::other)?;
                Ok(())
            },
            || -> Result<(), Box<dyn std::error::Error>> {
                tauri::WebviewWindowBuilder::from_config(app.handle(), &template)?
                    .data_directory(profile.clone())
                    .build()?;
                Ok(())
            },
        ),
        (None, None) => Ok(()),
        _ => Err(std::io::Error::other("Fenêtre de profil incohérente.").into()),
    }
}

fn main() {
    let context = launcher_context();
    if std::env::args_os().nth(1).as_deref() == Some(OsStr::new("--verify-game-launch")) {
        if cfg!(feature = "standalone")
            && match std::env::current_exe() {
                Ok(path) => installer::is_player_name(&path),
                Err(_) => true,
            }
        {
            eprintln!("Cette vérification ne peut pas utiliser l’entrée du jeu.");
            std::process::exit(1);
        }
        if !cfg!(feature = "production") || !embedded_frontend_ready(&context) {
            std::process::exit(1);
        }
        let mut state = LauncherState::default();
        if cfg!(feature = "standalone") {
            match play::verify_expected_startup() {
                Ok(Some((game, _, user_root))) => {
                    state.expected_game = Some(game);
                    state.expected_user_root = user_root;
                }
                Ok(None) => {}
                Err(error) => {
                    eprintln!("{error}");
                    std::process::exit(1);
                }
            }
        }
        // Exercise the exact application command used by the renderer button.
        let result = execute_state_command(LauncherCommand::RequestLaunch, &state);
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
                eprintln!("Interface du launcher absente ou configuration incompatible.");
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
                #[cfg(feature = "standalone")]
                println!("FORGE_LAUNCHER_STANDALONE_FRONTEND_READY");
                #[cfg(not(feature = "standalone"))]
                println!("FORGE_LAUNCHER_AUBE_FRONTEND_READY");
            }
            return;
        }
        eprintln!("Ce lanceur dépend du serveur de développement local.");
        std::process::exit(1);
    }
    if cfg!(feature = "production") && !embedded_frontend_ready(&context) {
        eprintln!("Interface du launcher absente ou configuration incompatible.");
        std::process::exit(1);
    }
    let Some(launcher_state) = initial_launcher_state() else {
        return;
    };
    #[cfg(all(windows, feature = "standalone"))]
    if let Some(reason) = launcher_state.startup_error.as_deref() {
        webview_profile::show_native_error(reason);
        std::process::exit(1);
    }
    run_launcher(context, launcher_state);
}

fn run_launcher(mut context: tauri::Context<tauri::Wry>, launcher_state: LauncherState) {
    let probe = webview_probe::Probe::parse().unwrap_or_else(|error| {
        eprintln!("{error}");
        std::process::exit(1);
    });
    probe.configure(&mut context);
    #[cfg(all(windows, feature = "standalone"))]
    let diagnostic_profile = probe.profile();
    #[cfg(not(all(windows, feature = "standalone")))]
    let diagnostic_profile: Option<PathBuf> = None;
    let require_fresh_profile = diagnostic_profile.is_some();
    let profile = diagnostic_profile.or_else(|| launcher_state.game_profile.clone());
    #[cfg(all(windows, feature = "standalone"))]
    let app_id = if profile.is_some() {
        webview_profile::explicit_app_id().unwrap_or_else(|error| {
            webview_profile::show_native_error(&error);
            std::process::exit(1);
        })
    } else {
        None
    };
    #[cfg(not(all(windows, feature = "standalone")))]
    let app_id: Option<String> = None;
    let profile_window = if profile.is_some() {
        let windows = &mut context.config_mut().app.windows;
        if windows.len() != 1 || !windows[0].create {
            #[cfg(all(windows, feature = "standalone"))]
            webview_profile::show_native_error("Fenêtre du lanceur invalide.");
            #[cfg(not(all(windows, feature = "standalone")))]
            eprintln!("Fenêtre du lanceur invalide.");
            std::process::exit(1);
        }
        let template = windows[0].clone();
        windows[0].create = false;
        Some(template)
    } else {
        None
    };
    let outcome = tauri::Builder::default()
        .manage(Arc::new(launcher_state))
        .manage(probe)
        .setup(move |app| {
            let setup_result = setup_profile_window(
                app,
                profile,
                profile_window,
                require_fresh_profile,
                app_id.as_deref(),
            )
            .and_then(|()| webview_probe::setup(app));
            #[cfg(all(windows, feature = "standalone"))]
            if webview_profile::report_setup_failure(
                &setup_result,
                webview_profile::show_native_error,
            ) {
                // AppHandle::exit is an asynchronous, preventable request.
                // Terminate here after the native message; do not return an
                // error to Tauri or reopen a default profile.
                std::process::exit(1);
            }
            setup_result
        })
        .on_page_load(webview_probe::loaded)
        .on_window_event(webview_probe::window_event)
        .invoke_handler(tauri::generate_handler![
            execute_launcher_command,
            webview_probe::complete_launcher_probe
        ])
        .run(context);
    if let Err(error) = outcome {
        let reason = format!("Impossible d’ouvrir le lanceur : {error}");
        #[cfg(all(windows, feature = "standalone"))]
        webview_profile::show_native_error(&reason);
        #[cfg(not(all(windows, feature = "standalone")))]
        eprintln!("{reason}");
        std::process::exit(1);
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_launcher_frontend::{DisplayMode, QualityPreset};

    #[test]
    fn startup_identity_failure_blocks_game_commands() {
        let state = LauncherState {
            startup_error: Some("Le jeu installé a changé.".into()),
            ..LauncherState::default()
        };
        assert!(matches!(
            execute_state_command(LauncherCommand::GameView, &state),
            LauncherResponse::Rejected { .. }
        ));
        assert!(matches!(
            execute_state_command(LauncherCommand::RequestLaunch, &state),
            LauncherResponse::Rejected { .. }
        ));
    }

    #[test]
    #[cfg(feature = "production")]
    fn production_context_contains_the_frontend_for_its_build_profile() {
        assert!(embedded_frontend_ready(&launcher_context()));
        let expected = if cfg!(feature = "standalone") {
            "standalone"
        } else {
            "aube"
        };
        assert_eq!(
            embedded_frontend_profile(&launcher_context()),
            Some(expected)
        );
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
