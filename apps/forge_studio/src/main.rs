#![deny(unsafe_code)]

use forge_studio_frontend::{StudioCommand, StudioResponse, decode_tauri_wire, encode_tauri_wire};
use forge_world::embedded_gate0_proof_document;
use std::sync::{Mutex, OnceLock};
use tauri::Manager;
mod native_atelier;
mod native_reconciliation;

/// Read-only adapter: the WebView supplies IDs and filters, never filesystem paths.
#[tauri::command]
async fn read_asset_library(request: String) -> Result<String, String> {
    if request.len() > 8192 {
        return Err("Requête de bibliothèque trop volumineuse".into());
    }
    tauri::async_runtime::spawn_blocking(move || {
        use forge_studio_frontend::{asset_library_native::Library, asset_library_protocol::LibraryRequest};
        static LIBRARY: OnceLock<Mutex<Result<Library, String>>> = OnceLock::new();
        let request: LibraryRequest = serde_json::from_str(&request).map_err(|e| e.to_string())?;
        let library = LIBRARY.get_or_init(|| Mutex::new(
            std::env::var_os("OPENRPG_ASSET_VAULT")
                .ok_or_else(|| "Définissez OPENRPG_ASSET_VAULT puis relancez le Studio pour ouvrir votre bibliothèque locale.".to_owned())
                .and_then(|root| Library::new(std::path::Path::new(&root)))
        ));
        let mut guard = library.lock().map_err(|_| "Bibliothèque temporairement indisponible".to_owned())?;
        let response = guard.as_mut().map_err(|e| e.clone())?.execute(request)?;
        serde_json::to_string(&response).map_err(|e| e.to_string())
    }).await.map_err(|e| format!("Lecture interrompue : {e}"))?
}

fn with_authoring_host<T>(
    operation: impl FnOnce(&mut forge_project::authoring::host::AuthoringHost) -> Result<T, String>,
) -> Result<T, String> {
    static HOST: OnceLock<Mutex<Result<forge_project::authoring::host::AuthoringHost, String>>> =
        OnceLock::new();
    let host = HOST.get_or_init(|| Mutex::new(
        std::env::var_os("FORGE_AUTHORING_ROOT")
            .ok_or_else(|| "Set FORGE_AUTHORING_ROOT to an existing authorized directory before launching Studio".to_owned())
            .and_then(|root| forge_project::authoring::host::AuthoringHost::new(std::path::Path::new(&root)))
            .map(|host| host.with_zone_validator(forge_world::promotion_preflight::validate_origins))
    ));
    host.lock()
        .map_err(|_| "authoring session unavailable after internal error".to_owned())
        .and_then(|mut guard| {
            guard
                .as_mut()
                .map_err(|error| error.clone())
                .and_then(operation)
        })
}

fn execute_authoring(request: forge_project::authoring::AuthoringRequest) -> StudioResponse {
    native_reconciliation::clear();
    let result = if request.uses_audit_context() {
        studio_human_audit()
            .and_then(|audit| with_authoring_host(|host| host.execute_with_audit(request, audit)))
    } else {
        with_authoring_host(|host| host.execute(request))
    };
    match result {
        Ok(state) => StudioResponse::Authoring {
            state: Box::new(state),
        },
        Err(reason) => StudioResponse::Rejected { reason },
    }
}

/// Native adapter ownership of local identity and clock. Neither value is read
/// by the project domain; test and automation callers can inject an AI stamp
/// directly through `AuthoringHost::execute_with_audit` instead.
fn studio_human_audit() -> Result<forge_project::audit::AuditStamp, String> {
    let subject = std::env::var("FORGE_AUTHOR_SUBJECT")
        .ok()
        .filter(|value| !value.trim().is_empty())
        .or_else(|| {
            std::env::var("USERNAME")
                .ok()
                .filter(|value| !value.trim().is_empty())
        })
        .unwrap_or_else(|| "studio-local-user".to_owned());
    studio_human_audit_at(subject, utc_now_rfc3339()?)
}

fn studio_human_audit_at(
    subject: impl Into<String>,
    recorded_at_utc: impl Into<String>,
) -> Result<forge_project::audit::AuditStamp, String> {
    forge_project::audit::AuditStamp::human(subject, recorded_at_utc)
}

fn utc_now_rfc3339() -> Result<String, String> {
    let seconds = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .map_err(|_| "Horloge système antérieure à l'époque Unix".to_owned())?
        .as_secs();
    let days = i64::try_from(seconds / 86_400)
        .map_err(|_| "Horloge système hors limite du calendrier d'audit".to_owned())?;
    let time = seconds % 86_400;
    let (year, month, day) = civil_from_days(days)?;
    Ok(format!(
        "{year:04}-{month:02}-{day:02}T{:02}:{:02}:{:02}Z",
        time / 3_600,
        (time % 3_600) / 60,
        time % 60
    ))
}

fn civil_from_days(days: i64) -> Result<(i64, u32, u32), String> {
    let z = days + 719_468;
    let era = if z >= 0 { z } else { z - 146_096 } / 146_097;
    let doe = z - era * 146_097;
    let yoe = (doe - doe / 1_460 + doe / 36_524 - doe / 146_096) / 365;
    let year = yoe + era * 400;
    let doy = doe - (365 * yoe + yoe / 4 - yoe / 100);
    let mp = (5 * doy + 2) / 153;
    let day = doy - (153 * mp + 2) / 5 + 1;
    let month = mp + if mp < 10 { 3 } else { -9 };
    let month = u32::try_from(month)
        .map_err(|_| "Mois UTC hors limite du calendrier d'audit".to_owned())?;
    let day =
        u32::try_from(day).map_err(|_| "Jour UTC hors limite du calendrier d'audit".to_owned())?;
    Ok((year + i64::from(month <= 2), month, day))
}

#[tauri::command]
async fn execute_studio_command(
    command: String,
    window: tauri::WebviewWindow,
) -> Result<String, String> {
    if let Ok(StudioCommand::Atelier { request }) = decode_tauri_wire(&command) {
        return encode_tauri_wire(
            &native_atelier::execute(&window, *request)
                .unwrap_or_else(|reason| StudioResponse::Rejected { reason }),
        );
    }
    if window.label() != "main" {
        return encode_tauri_wire(&StudioResponse::Rejected {
            reason: "L’atelier secondaire ne peut pas modifier directement les documents".into(),
        });
    }
    if let Ok(StudioCommand::CloseWindow { discard_unsaved }) = decode_tauri_wire(&command) {
        return finish_window_close(&window, discard_unsaved);
    }
    let response = tauri::async_runtime::spawn_blocking(move || {
        decode_tauri_wire(&command).map_or_else(
            |reason| StudioResponse::Rejected { reason },
            execute_studio_command_sync,
        )
    })
    .await
    .unwrap_or_else(|error| StudioResponse::Rejected {
        reason: format!("Commande native interrompue : {error}"),
    });
    encode_tauri_wire(&response)
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Tauri commands deserialize request payloads into owned Rust values"
)]
fn execute_studio_command_sync(command: StudioCommand) -> StudioResponse {
    let document = embedded_gate0_proof_document().expect("embedded Gate 0 JSON must remain valid");
    match command {
        StudioCommand::ReadVegetationSource {
            expected_head,
            block_index,
            offset,
        } => with_authoring_host(|host| {
            host.read_vegetation_source(expected_head, block_index, offset)
        })
        .map_or_else(
            |reason| StudioResponse::Rejected { reason },
            |chunk| StudioResponse::VegetationSource { chunk },
        ),
        StudioCommand::ReadTerrainSource {
            expected_document,
            offset,
        } => with_authoring_host(|host| host.read_terrain_source(expected_document, offset))
            .map_or_else(
                |reason| StudioResponse::Rejected { reason },
                |chunk| StudioResponse::TerrainSource { chunk },
            ),
        StudioCommand::Atelier { .. } => StudioResponse::Rejected {
            reason: "L’atelier nécessite la fenêtre native courante".into(),
        },
        StudioCommand::CloseWindow { .. } => StudioResponse::Rejected {
            reason: "La fermeture nécessite la fenêtre native courante".into(),
        },
        StudioCommand::Reconciliation { query } => native_reconciliation::query(&query)
            .map_or_else(
                |reason| StudioResponse::Rejected { reason },
                |page| StudioResponse::Reconciliation {
                    page: Box::new(page),
                },
            ),
        StudioCommand::Authoring { request } => execute_authoring(*request),
        StudioCommand::Workspace { request } => {
            native_reconciliation::clear();
            let result = if request.persists_audit_receipt() {
                studio_human_audit().and_then(|audit| {
                    with_authoring_host(|host| host.execute_workspace_with_audit(*request, audit))
                })
            } else {
                with_authoring_host(|host| host.execute_workspace(*request))
            };
            match result {
                Ok(state) => StudioResponse::Workspace {
                    state: Box::new(state),
                },
                Err(reason) => StudioResponse::Rejected { reason },
            }
        }
        StudioCommand::SelectEntity { entity_id }
            if document
                .entities
                .iter()
                .any(|entity| entity.id == entity_id) =>
        {
            StudioResponse::SelectionAccepted { entity_id }
        }
        StudioCommand::SelectEntity { entity_id } => StudioResponse::Rejected {
            reason: format!("entity {entity_id} is not present in the shared scene document"),
        },
        StudioCommand::SceneSummary => StudioResponse::SceneSummary {
            source_hash: document
                .content_hash()
                .expect("validated embedded Gate 0 document must be hashable")
                .to_string(),
            entity_count: document.entities.len(),
        },
    }
}

fn main() {
    tauri::Builder::default()
        .on_window_event(|window, event| {
            if window.label() == native_atelier::LABEL
                && let tauri::WindowEvent::CloseRequested { api, .. } = event
            {
                api.prevent_close();
                if let Some(webview) = window.get_webview_window(native_atelier::LABEL)
                    && let Err(error) = webview
                        .eval("window.dispatchEvent(new Event('forge-atelier-close-requested'))")
                {
                    eprintln!("Rapatriement indisponible : {error}");
                }
                return;
            }
            if window.label() == native_atelier::LABEL
                && matches!(event, tauri::WindowEvent::Destroyed)
            {
                native_atelier::closed();
            }
            if let tauri::WindowEvent::CloseRequested { api, .. } = event
                && let Some(webview) = window.get_webview_window(window.label())
                && webview
                    .url()
                    .is_ok_and(|url| url.query_pairs().any(|(k, v)| k == "authoring" && v == "1"))
            {
                // A clean native document can still have an unaccepted frontend draft.
                api.prevent_close();
                if let Err(error) =
                    webview.eval("window.dispatchEvent(new Event('forge-close-requested'))")
                {
                    eprintln!("Confirmation de fermeture indisponible : {error}");
                }
            }
        })
        .invoke_handler(tauri::generate_handler![execute_studio_command])
        .run(tauri::generate_context!())
        .expect("failed to run OpenRPG Forge Studio");
}

fn finish_window_close(
    window: &tauri::WebviewWindow,
    discard_unsaved: bool,
) -> Result<String, String> {
    let result = with_authoring_host(|host| {
        let state = host.execute(forge_project::authoring::AuthoringRequest::Inspect)?;
        check_window_close(state.dirty, discard_unsaved)?;
        if let Some(atelier) = window
            .app_handle()
            .get_webview_window(native_atelier::LABEL)
        {
            atelier.destroy().map_err(|error| error.to_string())?;
            native_atelier::closed();
        }
        // Bypass CloseRequested only after explicit confirmation, under the host lock.
        window.destroy().map_err(|error| error.to_string())?;
        Ok(state)
    });
    encode_tauri_wire(&match result {
        Ok(state) => StudioResponse::Authoring {
            state: Box::new(state),
        },
        Err(reason) => StudioResponse::Rejected { reason },
    })
}

fn check_window_close(dirty: bool, discard_unsaved: bool) -> Result<(), String> {
    if dirty && !discard_unsaved {
        Err("Modifications non enregistrées : sauvegarder ou confirmer leur abandon avant de quitter.".into())
    } else {
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_core::EntityId;
    use serde_json::Value;

    #[test]
    fn native_close_never_discards_dirty_work_without_explicit_choice() {
        assert!(check_window_close(true, false).is_err());
        assert!(check_window_close(false, false).is_ok());
        assert!(check_window_close(true, true).is_ok());
    }

    #[test]
    fn selection_accepts_only_entities_from_the_shared_document() {
        let document = embedded_gate0_proof_document().unwrap();
        let known = document.entities[0].id;
        assert_eq!(
            execute_studio_command_sync(StudioCommand::SelectEntity { entity_id: known }),
            StudioResponse::SelectionAccepted { entity_id: known }
        );

        let unknown = EntityId::deterministic("not-in-gate-0-scene");
        assert!(matches!(
            execute_studio_command_sync(StudioCommand::SelectEntity { entity_id: unknown }),
            StudioResponse::Rejected { .. }
        ));
    }

    #[test]
    fn embedded_frontend_requires_a_directory_not_a_windows_drive_url() {
        use tauri::utils::config::FrontendDist;
        let ambiguous: FrontendDist = serde_json::from_str(r#""E:/proof/web""#).unwrap();
        assert!(matches!(ambiguous, FrontendDist::Url(_)));
        let relative: FrontendDist = serde_json::from_str(r#""../../../proof/web""#).unwrap();
        assert!(matches!(relative, FrontendDist::Directory(_)));
        let config: tauri::utils::config::Config =
            serde_json::from_str(include_str!("../tauri.conf.json")).unwrap();
        assert!(matches!(
            config.build.frontend_dist,
            Some(FrontendDist::Directory(_))
        ));
    }

    #[test]
    fn gate0_capability_grants_no_privileged_tauri_command() {
        let capability: Value =
            serde_json::from_str(include_str!("../capabilities/default.json")).unwrap();
        let permissions = capability["permissions"].as_array().unwrap();
        assert_eq!(permissions.len(), 2);
        assert_eq!(permissions[0], "allow-execute-studio-command");
        assert_eq!(permissions[1], "core:window:deny-set-title");
        let granted: Vec<_> = permissions
            .iter()
            .filter_map(Value::as_str)
            .filter(|identifier| !identifier.contains(":deny-"))
            .collect();
        assert_eq!(granted, ["allow-execute-studio-command"]);
    }

    #[test]
    fn gate0_csp_blocks_remote_frames_objects_and_base_retargeting() {
        let config: Value = serde_json::from_str(include_str!("../tauri.conf.json")).unwrap();
        let csp = config["app"]["security"]["csp"].as_str().unwrap();
        assert!(csp.contains("default-src 'self'"));
        assert!(csp.contains("object-src 'none'"));
        assert!(csp.contains("frame-src 'none'"));
        assert!(csp.contains("base-uri 'none'"));
        assert!(!csp.contains("https:"));
    }

    #[test]
    fn native_author_stamp_is_deterministic_when_adapter_inputs_are_injected() {
        let stamp = studio_human_audit_at("alice", "2026-09-22T12:34:56Z").unwrap();
        assert!(
            matches!(stamp.actor, forge_project::audit::AuditActor::Human { ref subject } if subject == "alice")
        );
        assert_eq!(
            stamp.recorded_at_utc.as_deref(),
            Some("2026-09-22T12:34:56Z")
        );
    }
}
