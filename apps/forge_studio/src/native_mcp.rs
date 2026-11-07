//! Opt-in stdio shares the application's existing host; no second checkout/host.
use forge_project::assistant::selection::PlanningContext;
use forge_studio_frontend::{
    assistant_mcp::{self, Backend, ToolCall},
    assistant_mcp_controller::Controller,
    mcp_protocol::{McpRequest, McpView},
};
use serde_json::Value;
use std::sync::{Mutex, OnceLock};
use tauri::Manager;

static APP: OnceLock<tauri::AppHandle> = OnceLock::new();
fn notify() {
    if let Some(window) = APP.get().and_then(|app| app.get_webview_window("main")) {
        let _ = window.eval("window.dispatchEvent(new Event('forge-mcp-changed'))");
    }
}

fn with_controller<T>(
    operation: impl FnOnce(&mut Controller) -> Result<T, String>,
) -> Result<T, String> {
    static CONTROLLER: OnceLock<Mutex<Controller>> = OnceLock::new();
    let mut controller = CONTROLLER
        .get_or_init(|| Mutex::new(Controller::default()))
        .lock()
        .map_err(|_| "MCP indisponible après une erreur interne.")?;
    operation(&mut controller)
}

pub(super) fn execute(request: McpRequest) -> Result<McpView, String> {
    with_controller(|controller| match request {
        McpRequest::Status => controller.status(),
        McpRequest::Revoke => controller.revoke_view(),
        request => {
            let root = forge_studio_frontend::launch_configuration::authoring_root()?
                .ok_or("Configurer un espace utilisateur avant MCP.")?;
            controller.set_storage_root(&root)?;
            super::with_authoring_host(|host| {
                controller.ui_with_assets(host, request, |selection| {
                    super::with_asset_library(|library| {
                        forge_studio_frontend::assistant_assets_native::resolve(library, selection)
                    })
                })
            })
        }
    })
}

struct Bridge;
impl Backend for Bridge {
    fn context(&self) -> Result<Option<PlanningContext>, String> {
        if super::native_game::native_game_running()? {
            return Ok(None);
        }
        with_controller(|controller| {
            if !controller.authorized() {
                return Ok(None);
            }
            super::with_authoring_host(|host| controller.context(host))
        })
    }
    fn execute(&mut self, call: ToolCall) -> Result<Value, String> {
        if super::native_game::native_game_running()? {
            return Err("Fermer le jeu avant une opération MCP.".into());
        }
        let utc = super::utc_now_rfc3339()?;
        let result = with_controller(|controller| {
            super::with_authoring_host(|host| controller.tool(host, &utc, call))
        });
        notify();
        result
    }
}

pub(super) fn start_if_requested(app: tauri::AppHandle) -> Result<(), String> {
    if !std::env::args_os().any(|argument| argument == "--mcp-stdio") {
        return Ok(());
    }
    APP.set(app).map_err(|_| "Transport MCP déjà initialisé.")?;
    with_controller(|controller| {
        controller.set_transport_open(true);
        Ok(())
    })?;
    std::thread::Builder::new()
        .name("forge-mcp-stdio".into())
        .spawn(|| {
            let result = assistant_mcp::serve(
                std::io::stdin().lock(),
                std::io::stdout().lock(),
                &mut Bridge,
            );
            if result.is_err() {
                eprintln!("Transport MCP interrompu ; autorisation révoquée.");
            }
            let _ = with_controller(|controller| {
                controller.set_transport_open(false);
                Ok(())
            });
            notify();
        })
        .map_err(|error| format!("Ouverture du transport MCP impossible : {error}"))?;
    Ok(())
}
