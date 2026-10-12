//! Explicit CLI diagnostic for the real embedded `WebView` and read-only IPC.
use serde::{Deserialize, Serialize};
use std::{
    fs::OpenOptions,
    io::Write,
    path::PathBuf,
    sync::{
        Arc, Mutex,
        atomic::{AtomicBool, Ordering},
    },
    time::{Duration, SystemTime, UNIX_EPOCH},
};
use tauri::{
    Manager,
    webview::{PageLoadEvent, PageLoadPayload},
};

#[derive(Clone, Default)]
pub struct Probe(Option<Arc<Active>>);
struct Active {
    exit_menu: bool,
    exit_report: Mutex<Option<Report>>,
    output: PathBuf,
    token: String,
    completed: AtomicBool,
    events: Mutex<Vec<String>>,
}

#[derive(Clone, Default, Deserialize, Serialize)]
#[allow(
    clippy::struct_excessive_bools,
    reason = "Independent observations in the IPC diagnostic report"
)]
#[serde(default)]
pub struct Report {
    dialog_opened: bool,
    escape_cancelled: bool,
    cancel_button_worked: bool,
    focus_on_cancel: bool,
    dialog_fits_viewport: bool,
    native_close_confirmed: bool,
    complete: bool,
    stage: String,
    token: String,
    title: String,
    url: String,
    bridge: bool,
    styles_loaded: bool,
    button_present: bool,
    button_disabled: bool,
    snapshot_kind: String,
    runtime_connected: bool,
    status: String,
    error: Option<String>,
}

impl Probe {
    pub fn parse() -> Result<Self, String> {
        let mode = std::env::args_os().nth(1);
        let exit_menu = mode.as_deref() == Some(std::ffi::OsStr::new("--verify-exit-menu"));
        if !exit_menu && mode.as_deref() != Some(std::ffi::OsStr::new("--verify-webview")) {
            return Ok(Self::default());
        }
        if !cfg!(feature = "production") {
            return Err("The WebView probe requires embedded production assets.".into());
        }
        let output = PathBuf::from(
            std::env::args_os()
                .nth(2)
                .ok_or("A fresh receipt path is required.")?,
        );
        if output.exists() || !output.parent().is_some_and(std::path::Path::is_dir) {
            return Err("The receipt must be new, inside an existing directory.".into());
        }
        let now = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .map_err(|error| error.to_string())?;
        Ok(Self(Some(Arc::new(Active {
            exit_menu,
            exit_report: Mutex::new(None),
            output,
            token: format!("{}-{}", std::process::id(), now.as_nanos()),
            completed: AtomicBool::new(false),
            events: Mutex::new(Vec::new()),
        }))))
    }

    pub fn configure(&self, context: &mut tauri::Context<tauri::Wry>) {
        if let Some(active) = &self.0 {
            for window in &mut context.config_mut().app.windows {
                window.visible = false;
                window.maximized = false;
                window.data_directory = Some(
                    active
                        .output
                        .parent()
                        .expect("validated receipt parent")
                        .join("webview-profile"),
                );
            }
        }
    }

    fn finish(&self, app: &tauri::AppHandle, report: &Report, failure: Option<&str>) {
        let Some(active) = &self.0 else { return };
        if active.completed.swap(true, Ordering::SeqCst) {
            return;
        }
        let result = serde_json::json!({
            "schema": "forge.tidewater.launcher-webview-probe", "version": 1,
            "pass": failure.is_none(), "failure": failure, "report": report,
            "events": active.events.lock().map(|events| events.clone()).unwrap_or_default(),
            "scope": "Real embedded WebView, Snapshot IPC, Escape dialog DOM input and native close event; no OS input or game spawn",
        });
        let written = OpenOptions::new()
            .write(true)
            .create_new(true)
            .open(&active.output)
            .and_then(|mut file| file.write_all(serde_json::to_string_pretty(&result)?.as_bytes()));
        if let Err(error) = &written {
            eprintln!("WebView receipt failed: {error}");
        }
        app.exit(i32::from(failure.is_some() || written.is_err()));
    }
}

#[allow(
    clippy::unnecessary_wraps,
    reason = "Required Tauri setup callback signature"
)]
pub fn setup(app: &mut tauri::App) -> Result<(), Box<dyn std::error::Error>> {
    let probe = app.state::<Probe>().inner().clone();
    if probe.0.is_some() {
        if let Some(active) = &probe.0 {
            active
                .events
                .lock()
                .expect("probe events")
                .push("setup".into());
        }
        if let Some(window) = app.get_webview_window("main") {
            if let (Some(active), Ok(url)) = (&probe.0, window.url()) {
                active
                    .events
                    .lock()
                    .expect("probe events")
                    .push(format!("setup url {url}"));
            }
            dispatch(window.as_ref(), &probe);
        }
        let handle = app.handle().clone();
        std::thread::spawn(move || {
            std::thread::sleep(Duration::from_secs(25));
            probe.finish(&handle, &Report::default(), Some("WebView/IPC timed out."));
        });
    }
    Ok(())
}

pub fn loaded(window: &tauri::Webview, payload: &PageLoadPayload<'_>) {
    let probe = window.state::<Probe>();
    let Some(active) = &probe.0 else { return };
    active.events.lock().expect("probe events").push(format!(
        "page {:?} {}",
        payload.event(),
        payload.url()
    ));
    if window.label() != "main" || payload.event() != PageLoadEvent::Finished {
        return;
    }
    dispatch(window, &probe);
}

fn dispatch(window: &tauri::Webview, probe: &Probe) {
    let Some(active) = &probe.0 else { return };
    let script = include_str!("webview_probe.js")
        .replace(
            "__FORGE_PROBE_EXIT__",
            if active.exit_menu { "true" } else { "false" },
        )
        .replace(
            "__FORGE_PROBE_TOKEN__",
            &serde_json::to_string(&active.token).expect("string serialization"),
        );
    if let Err(error) = window.eval(script) {
        probe.finish(
            window.app_handle(),
            &Report::default(),
            Some(&error.to_string()),
        );
    } else {
        active
            .events
            .lock()
            .expect("probe events")
            .push("probe script dispatched".into());
    }
}

#[tauri::command]
#[allow(
    clippy::needless_pass_by_value,
    reason = "Tauri supplies owned command payloads and window/state handles"
)]
pub fn complete_launcher_probe(
    report: Report,
    window: tauri::WebviewWindow,
    probe: tauri::State<'_, Probe>,
) -> Result<(), String> {
    let Some(active) = &probe.0 else {
        return Err("No WebView diagnostic is active.".into());
    };
    if report.token != active.token || window.label() != "main" {
        return Err("Wrong diagnostic session.".into());
    }
    active
        .events
        .lock()
        .expect("probe events")
        .push(format!("IPC {}", report.stage));
    if !report.complete && report.stage != "exit-menu-ready" {
        return Ok(());
    }
    let url = window.url().map_err(|error| error.to_string())?;
    let embedded_origin = (matches!(url.scheme(), "http" | "https")
        && url.host_str() == Some("tauri.localhost"))
        || (url.scheme() == "tauri" && url.host_str() == Some("localhost"));
    let connected = crate::play::runtime_connected();
    let pass = report.error.is_none()
        && report.dialog_opened
        && report.escape_cancelled
        && report.cancel_button_worked
        && report.focus_on_cancel
        && report.dialog_fits_viewport
        && embedded_origin
        && report.url == url.as_str()
        && report.title == "Aventures — OpenRPG Forge"
        && report.bridge
        && report.styles_loaded
        && report.button_present
        && report.snapshot_kind == "snapshot"
        && report.runtime_connected == connected
        && report.button_disabled != connected
        && if connected {
            report.status == "L’île est prête à jouer."
        } else {
            report
                .status
                .contains("Les fichiers du jeu sont absents ou incomplets.")
        };
    let failure = if pass {
        None
    } else {
        Some("Embedded document or real desktop IPC availability check failed.")
    };
    if active.exit_menu && pass && report.stage == "exit-menu-ready" {
        *active
            .exit_report
            .lock()
            .map_err(|error| error.to_string())? = Some(report);
    } else {
        probe.finish(window.app_handle(), &report, failure);
    }
    Ok(())
}

/// Record the actual native close event after the real confirmation button IPC.
pub fn window_event(window: &tauri::Window, event: &tauri::WindowEvent) {
    if window.label() != "main" || !matches!(event, tauri::WindowEvent::CloseRequested { .. }) {
        return;
    }
    let probe = window.state::<Probe>();
    let Some(active) = &probe.0 else {
        return;
    };
    if !active.exit_menu {
        return;
    }
    let report = active
        .exit_report
        .lock()
        .ok()
        .and_then(|mut report| report.take());
    if let Some(mut report) = report {
        report.native_close_confirmed = true;
        report.complete = true;
        report.stage = "native-close-confirmed".into();
        probe.finish(window.app_handle(), &report, None);
    } else {
        probe.finish(
            window.app_handle(),
            &Report::default(),
            Some("Native window closed before the dialog was verified."),
        );
    }
}
