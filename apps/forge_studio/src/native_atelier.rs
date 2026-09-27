//! Two local surfaces, one authoring host. Secondary gets presentation-only commands.
use forge_studio_frontend::{
    StudioResponse,
    atelier_protocol::{Relay, Request},
};
use std::sync::{Mutex, OnceLock};
use tauri::Manager;

pub const LABEL: &str = "atelier";
fn relay() -> &'static Mutex<Relay> {
    static RELAY: OnceLock<Mutex<Relay>> = OnceLock::new();
    RELAY.get_or_init(|| Mutex::new(Relay::default()))
}

pub fn closed() {
    if let Ok(mut relay) = relay().lock() {
        relay.set_open(false);
    }
}

pub fn allowed(label: &str, request: &Request) -> bool {
    match request {
        Request::Open | Request::Publish { .. } => label == "main",
        Request::Submit { .. } => label == LABEL,
        Request::Read | Request::Repatriate | Request::SaveDraft { .. } => {
            label == "main" || label == LABEL
        }
    }
}

/// Enough of the title bar must intersect a monitor to allow manual recovery.
fn title_reachable(position: (i32, i32), width: u32, monitors: &[(i32, i32, u32, u32)]) -> bool {
    let (x, y) = (i64::from(position.0), i64::from(position.1));
    monitors.iter().any(|&(mx, my, mw, mh)| {
        let (mx, my) = (i64::from(mx), i64::from(my));
        (x + i64::from(width)).min(mx + i64::from(mw)) - x.max(mx) >= 96
            && (y + 32).min(my + i64::from(mh)) - y.max(my) >= 16
    })
}

fn recover_offscreen(window: &tauri::WebviewWindow) -> Result<(), String> {
    let Some(other) = window.app_handle().get_webview_window(LABEL) else {
        return Ok(());
    };
    let result = recover_existing(window, &other);
    // A secondary can disappear between lookup and the native geometry calls.
    // That is a successful return to mono-screen, not an editor failure. Keep
    // errors for a window that still exists visible to the user.
    recovery_result(result, || {
        window.app_handle().get_webview_window(LABEL).is_some()
    })
}

fn recovery_result(
    result: Result<(), String>,
    still_exists: impl FnOnce() -> bool,
) -> Result<(), String> {
    match result {
        Err(_) if !still_exists() => Ok(()),
        other => other,
    }
}

fn recover_existing(
    window: &tauri::WebviewWindow,
    other: &tauri::WebviewWindow,
) -> Result<(), String> {
    if other.is_minimized().map_err(|e| e.to_string())? {
        return Ok(());
    }
    let position = other.outer_position().map_err(|e| e.to_string())?;
    let size = other.outer_size().map_err(|e| e.to_string())?;
    let monitors = window.available_monitors().map_err(|e| e.to_string())?;
    let bounds = monitors
        .iter()
        .map(|m| {
            (
                m.position().x,
                m.position().y,
                m.size().width,
                m.size().height,
            )
        })
        .collect::<Vec<_>>();
    if !bounds.is_empty() && !title_reachable((position.x, position.y), size.width, &bounds) {
        other.unmaximize().map_err(|e| e.to_string())?;
        if let Some(monitor) = window.current_monitor().map_err(|e| e.to_string())? {
            other
                .set_position(*monitor.position())
                .map_err(|e| e.to_string())?;
            other.maximize().map_err(|e| e.to_string())?;
        } else {
            other.center().map_err(|e| e.to_string())?;
        }
    }
    Ok(())
}

fn repatriate(window: &tauri::WebviewWindow) -> Result<(), String> {
    if window.label() == "main" {
        if let Some(other) = window.app_handle().get_webview_window(LABEL) {
            other
                .eval("window.dispatchEvent(new Event('forge-atelier-close-requested'))")
                .map_err(|e| e.to_string())?;
        } else {
            closed();
        }
    } else {
        if let Some(other) = window.app_handle().get_webview_window(LABEL) {
            other.destroy().map_err(|e| e.to_string())?;
        }
        closed();
        if let Some(main) = window.app_handle().get_webview_window("main") {
            let _ = main.set_focus();
        }
    }
    Ok(())
}

pub fn execute(window: &tauri::WebviewWindow, request: Request) -> Result<StudioResponse, String> {
    if !allowed(window.label(), &request) {
        return Err("Action non autorisée pour cette fenêtre".into());
    }
    // Only the polling consumer applies returned intents. Window-control replies
    // are not consumed by it and therefore must never drain its mailbox.
    let consume_pending =
        window.label() == "main" && matches!(&request, Request::Read | Request::Publish { .. });
    match request {
        Request::Open => {
            let app = window.app_handle();
            if let Some(other) = app.get_webview_window(LABEL) {
                recover_offscreen(window)?;
                other.show().map_err(|e| e.to_string())?;
                other.set_focus().map_err(|e| e.to_string())?;
            } else {
                let current = window.current_monitor().map_err(|e| e.to_string())?;
                let monitors = window.available_monitors().map_err(|e| e.to_string())?;
                let target = monitors.iter().find(|m| {
                    current
                        .as_ref()
                        .is_none_or(|c| c.position() != m.position())
                });
                let other = tauri::WebviewWindowBuilder::new(
                    app,
                    LABEL,
                    tauri::WebviewUrl::App("index.html?atelier=1".into()),
                )
                .title("OpenRPG Forge · Atelier secondaire")
                .inner_size(1280.0, 840.0)
                .min_inner_size(960.0, 640.0)
                .disable_drag_drop_handler()
                .visible(false)
                .build()
                .map_err(|e| e.to_string())?;
                let reveal = (|| -> Result<(), tauri::Error> {
                    if let Some(monitor) = target {
                        other.set_position(*monitor.position())?;
                        other.maximize()?;
                    } else {
                        other.center()?;
                    }
                    other.show()?;
                    other.set_focus()
                })();
                if let Err(error) = reveal {
                    let _ = other.destroy();
                    return Err(error.to_string());
                }
            }
            relay()
                .lock()
                .map_err(|_| "Atelier indisponible")?
                .set_open(true);
        }
        Request::Repatriate => repatriate(window)?,
        Request::Publish { snapshot } => relay()
            .lock()
            .map_err(|_| "Atelier indisponible")?
            .publish(snapshot)?,
        Request::Submit { revision, intent } => relay()
            .lock()
            .map_err(|_| "Atelier indisponible")?
            .submit(revision, intent)?,
        Request::Read => {
            if window.label() == "main" {
                recover_offscreen(window)?;
            }
        }
        Request::SaveDraft { expected, draft } => relay()
            .lock()
            .map_err(|_| "Atelier indisponible")?
            .save_draft(expected, draft)?,
    }
    Ok(StudioResponse::Atelier {
        state: Box::new(
            relay()
                .lock()
                .map_err(|_| "Atelier indisponible")?
                .read(consume_pending),
        ),
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn recovery_ignores_only_a_window_that_disappeared() {
        assert!(recovery_result(Err("window gone".into()), || false).is_ok());
        assert_eq!(
            recovery_result(Err("geometry failed".into()), || true),
            Err("geometry failed".into())
        );
        assert!(recovery_result(Ok(()), || panic!("no lookup on success")).is_ok());
    }
    #[test]
    fn removed_monitor_and_negative_coordinates() {
        let main = [(0, 0, 2560, 1440)];
        assert!(title_reachable((100, 100), 1280, &main));
        assert!(title_reachable((-8, -8), 2576, &main));
        assert!(!title_reachable((2560, 0), 1920, &main));
        assert!(!title_reachable((0, 1450), 1280, &main));
        assert!(title_reachable((-1920, 0), 1920, &[(-1920, 0, 1920, 1080)]));
        assert!(!title_reachable((i32::MAX, 0), u32::MAX, &main));
    }
    #[test]
    fn secondary_cannot_publish_or_open_windows() {
        assert!(!allowed(LABEL, &Request::Open));
        assert!(!allowed(
            LABEL,
            &Request::Publish {
                snapshot: forge_studio_frontend::atelier_protocol::Snapshot::default()
            }
        ));
        assert!(allowed(LABEL, &Request::Read));
        assert!(!allowed("untrusted", &Request::Read));
    }
}
