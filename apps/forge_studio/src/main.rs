#![deny(unsafe_code)]

use forge_studio_frontend::{StudioCommand, StudioResponse, decode_tauri_wire, encode_tauri_wire};
use forge_world::embedded_gate0_proof_document;
use std::sync::{Mutex, OnceLock};
use tauri::Manager;
mod native_atelier;
mod native_game;
mod native_reconciliation;

static AUTHORING_HOST: OnceLock<
    Mutex<Result<forge_project::authoring::host::AuthoringHost, String>>,
> = OnceLock::new();

fn with_asset_imports<T>(
    operation: impl FnOnce(
        &forge_studio_frontend::asset_import_native::AssetImportStore,
    ) -> Result<T, String>,
) -> Result<T, String> {
    use forge_studio_frontend::asset_import_native::AssetImportStore;
    static IMPORTS: OnceLock<Mutex<Result<AssetImportStore, String>>> = OnceLock::new();
    let store = IMPORTS.get_or_init(|| {
        Mutex::new(
            forge_studio_frontend::launch_configuration::authoring_root()
                .and_then(|root| {
                    root.ok_or_else(|| {
                        "Configurez un espace utilisateur pour importer des assets.".to_owned()
                    })
                })
                .and_then(|root| AssetImportStore::new(&root.join("asset-imports"))),
        )
    });
    let guard = store
        .lock()
        .map_err(|_| "Bibliothèque d’imports temporairement indisponible".to_owned())?;
    operation(guard.as_ref().map_err(Clone::clone)?)
}

/// Typed raw uploads carry bytes and identities, never client filesystem paths.
#[tauri::command]
async fn execute_asset_import(
    request: tauri::ipc::Request<'_>,
) -> Result<tauri::ipc::Response, String> {
    use forge_studio_frontend::asset_import_protocol::{decode_request, encode_response};
    let decoded = match request.body() {
        tauri::ipc::InvokeBody::Raw(bytes) => decode_request(bytes)?,
        tauri::ipc::InvokeBody::Json(_) => return Err("Transfert binaire d’import attendu".into()),
    };
    tauri::async_runtime::spawn_blocking(move || {
        let response = with_asset_imports(|store| store.execute(decoded))?;
        encode_response(&response).map(tauri::ipc::Response::new)
    })
    .await
    .map_err(|error| format!("Import interrompu : {error}"))?
}

/// Read-only adapter: the `WebView` supplies IDs and filters, never filesystem paths.
#[tauri::command]
async fn read_asset_library(request: String) -> Result<String, String> {
    if request.len() > 8192 {
        return Err("Requête de bibliothèque trop volumineuse".into());
    }
    tauri::async_runtime::spawn_blocking(move || {
        use forge_studio_frontend::asset_library_protocol::LibraryRequest;
        let request: LibraryRequest = serde_json::from_str(&request).map_err(|e| e.to_string())?;
        if matches!(
            request,
            LibraryRequest::Model { .. } | LibraryRequest::ModelReference { .. }
        ) {
            return Err("Utiliser le transfert binaire des modèles".into());
        }
        let response = execute_library_request(request)?;
        serde_json::to_string(&response).map_err(|e| e.to_string())
    })
    .await
    .map_err(|e| format!("Lecture interrompue : {e}"))?
}

fn execute_library_request(
    request: forge_studio_frontend::asset_library_protocol::LibraryRequest,
) -> Result<forge_studio_frontend::asset_library_protocol::LibraryResponse, String> {
    with_asset_library(|library| library.execute(request))
}

fn with_asset_library<T>(
    operation: impl FnOnce(
        &mut forge_studio_frontend::asset_library_native::Library,
    ) -> Result<T, String>,
) -> Result<T, String> {
    use forge_studio_frontend::asset_library_native::Library;
    static LIBRARY: OnceLock<Mutex<Result<Library, String>>> = OnceLock::new();
    let library = LIBRARY.get_or_init(|| Mutex::new(
            forge_studio_frontend::launch_configuration::asset_vault()
                .and_then(|root| root.ok_or_else(|| "Configurez asset_vault dans forge-studio.local.json pour ouvrir votre bibliothèque locale.".to_owned()))
                .and_then(|root| Library::new(std::path::Path::new(&root)))
        ));
    let mut guard = library
        .lock()
        .map_err(|_| "Bibliothèque temporairement indisponible".to_owned())?;
    operation(guard.as_mut().map_err(|e| e.clone())?)
}

#[tauri::command]
async fn read_asset_physics(request: String) -> Result<tauri::ipc::Response, String> {
    if request.len() > 8192 {
        return Err("Requête physique trop volumineuse".into());
    }
    tauri::async_runtime::spawn_blocking(move || {
        use forge_studio_frontend::asset_library_protocol::PhysicsReadRequest;
        let request: PhysicsReadRequest =
            serde_json::from_str(&request).map_err(|e| e.to_string())?;
        with_asset_library(|library| match request {
            PhysicsReadRequest::Current(request) => library.read_physics_package(request),
            PhysicsReadRequest::Saved(request) => library.read_saved_physics_package(&request),
        })
        .map(tauri::ipc::Response::new)
    })
    .await
    .map_err(|e| format!("Lecture physique interrompue : {e}"))?
}

/// Raw IPC avoids expanding a GLB into a JSON array of decimal bytes.
#[tauri::command]
async fn read_asset_particles(request: String) -> Result<tauri::ipc::Response, String> {
    if request.len() > 8192 {
        return Err("Requête d’effet trop volumineuse".into());
    }
    tauri::async_runtime::spawn_blocking(move || {
        let request: forge_studio_frontend::asset_library_protocol::ParticleReadRequest =
            serde_json::from_str(&request).map_err(|e| e.to_string())?;
        with_asset_library(|library| match request {
            forge_studio_frontend::asset_library_protocol::ParticleReadRequest::Current(
                request,
            ) => library.read_particle_package(request),
            forge_studio_frontend::asset_library_protocol::ParticleReadRequest::Saved(request) => {
                library.read_saved_particle_package(&request)
            }
        })
        .map(tauri::ipc::Response::new)
    })
    .await
    .map_err(|e| format!("Lecture d’effet interrompue : {e}"))?
}

/// Raw IPC avoids expanding a GLB into a JSON array of decimal bytes.
#[tauri::command]
async fn read_asset_model(request: String) -> Result<tauri::ipc::Response, String> {
    if request.len() > 8192 {
        return Err("Requête de modèle trop volumineuse".into());
    }
    tauri::async_runtime::spawn_blocking(move || {
        use forge_studio_frontend::asset_library_protocol::{
            LibraryRequest, LibraryResponse, ModelReadRequest,
        };
        let request: ModelReadRequest =
            serde_json::from_str(&request).map_err(|e| e.to_string())?;
        let wire = match request {
            ModelReadRequest::Vault(request) => {
                if !matches!(
                    request,
                    LibraryRequest::Model { .. } | LibraryRequest::ModelReference { .. }
                ) {
                    return Err("Requête de modèle attendue".into());
                }
                let LibraryResponse::Model { model } = execute_library_request(request)? else {
                    return Err("Réponse de modèle inattendue".into());
                };
                forge_studio_frontend::asset_library_model_wire::encode(model)?
            }
            ModelReadRequest::Module(request) => execute_module_model_request(&request)?,
        };
        Ok(tauri::ipc::Response::new(wire))
    })
    .await
    .map_err(|e| format!("Lecture interrompue : {e}"))?
}

fn execute_module_model_request(
    request: &forge_studio_frontend::asset_library_protocol::ModuleModelRequest,
) -> Result<Vec<u8>, String> {
    use forge_studio_frontend::asset_library_protocol::{ModuleModelReply, ModuleModelRequest};
    use forge_studio_frontend::module_asset_native::{ModuleAssetResolver, ModuleModelError};
    static RESOLVER: OnceLock<Result<Option<ModuleAssetResolver>, ModuleModelError>> =
        OnceLock::new();
    let ModuleModelRequest::ModuleModelReference {
        schema_version,
        asset_id,
        sha256,
    } = request;
    if *schema_version != 2 {
        return Err("Version de requête de modèle du module invalide".into());
    }
    let (asset_id, sha256) = (*asset_id, *sha256);
    if forge_studio_frontend::launch_configuration::authoring_root()?.is_some()
        && let Some(file) = with_asset_imports(|store| store.lookup_model(asset_id, sha256))?
    {
        if file.kind != forge_assets::governed_import::GovernedAssetKind::Mesh {
            return Err("Cet asset importé n’est pas un modèle 3D".into());
        }
        let reply =
            ModuleModelReply::Model(forge_studio_frontend::asset_library_protocol::ModuleModel {
                asset_id,
                sha256,
                catalog_sha256: file.catalog_sha256,
                collision: None,
                bytes: file.upload.bytes,
            });
        return forge_studio_frontend::asset_library_model_wire::encode_module(reply);
    }
    let resolver = RESOLVER
        .get_or_init(ModuleAssetResolver::from_environment)
        .as_ref()
        .map_err(ToString::to_string)?;
    let reply = match resolver {
        None => ModuleModelReply::Missing { asset_id, sha256 },
        Some(resolver) => match resolver.resolve(asset_id, sha256) {
            Ok(model) => ModuleModelReply::Model(model),
            Err(ModuleModelError::Missing(_)) => ModuleModelReply::Missing { asset_id, sha256 },
            Err(error) => return Err(error.to_string()),
        },
    };
    forge_studio_frontend::asset_library_model_wire::encode_module(reply)
}

fn with_authoring_host<T>(
    operation: impl FnOnce(&mut forge_project::authoring::host::AuthoringHost) -> Result<T, String>,
) -> Result<T, String> {
    let host = AUTHORING_HOST.get_or_init(|| Mutex::new(
        forge_studio_frontend::launch_configuration::authoring_root()
            .and_then(|root| root.ok_or_else(|| "Configurez authoring_root dans forge-studio.local.json pour ouvrir vos modules.".to_owned()))
            .and_then(|root| forge_project::authoring::host::AuthoringHost::new(std::path::Path::new(&root)))
            .map(|host| host.with_zone_validator(validate_studio_zone))
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

mod native_module_export;

#[tauri::command]
async fn export_module(
    request: String,
) -> Result<forge_studio_frontend::module_export_protocol::ModuleExportResult, String> {
    if request.len() > 64 * 1024 {
        return Err("Requête d’export trop volumineuse".into());
    }
    tauri::async_runtime::spawn_blocking(move || {
        let request = serde_json::from_str(&request).map_err(|e| e.to_string())?;
        native_module_export::execute(request)
    })
    .await
    .map_err(|e| format!("Export interrompu : {e}"))?
}

fn validate_studio_zone(
    head: &forge_project::zone_authoring::ZoneAuthoringHead,
) -> Result<(), String> {
    forge_world::promotion_preflight::validate_origins(head)?;
    let bindings = head
        .corrections
        .gameplay_visuals
        .as_deref()
        .unwrap_or_default();
    if bindings.is_empty() {
        return Ok(());
    }
    with_asset_imports(|store| {
        forge_studio_frontend::gameplay_visual_native::verify_models(bindings, |id, pin| {
            store.lookup_model(id, pin)
        })
    })
}

fn execute_authoring(request: forge_project::authoring::AuthoringRequest) -> StudioResponse {
    if let Err(reason) = forge_studio_frontend::validate_authoring_mode(&request) {
        return StudioResponse::Rejected { reason };
    }
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
        StudioCommand::GameSave { request } => {
            with_authoring_host(|host| host.execute_game_save(*request)).map_or_else(
                |reason| StudioResponse::Rejected { reason },
                |state| StudioResponse::GameSave {
                    state: Box::new(state),
                },
            )
        }
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
        .invoke_handler(tauri::generate_handler![
            execute_studio_command,
            execute_asset_import,
            export_module,
            read_asset_library,
            read_asset_model,
            read_asset_physics,
            read_asset_particles,
            native_game::launch_native_game,
            native_game::launch_exported_module,
            native_game::native_game_running,
            native_game::native_game_diagnostics
        ])
        .run(tauri::generate_context!())
        .expect("failed to run OpenRPG Forge Studio");
}

fn finish_window_close(
    window: &tauri::WebviewWindow,
    discard_unsaved: bool,
) -> Result<String, String> {
    let result = match AUTHORING_HOST.get() {
        Some(host) => match host.lock() {
            Ok(mut host) => match host.as_mut() {
                Ok(host) => {
                    let inspected =
                        host.execute(forge_project::authoring::AuthoringRequest::Inspect);
                    close_window_when_authorized(
                        inspected.as_ref().ok().map(|state| state.dirty),
                        discard_unsaved,
                        || destroy_confirmed_windows(window),
                    )
                }
                Err(_) => close_window_when_authorized(None, discard_unsaved, || {
                    destroy_confirmed_windows(window)
                }),
            },
            Err(_) => close_window_when_authorized(None, discard_unsaved, || {
                destroy_confirmed_windows(window)
            }),
        },
        None => close_window_when_authorized(None, discard_unsaved, || {
            destroy_confirmed_windows(window)
        }),
    };
    encode_tauri_wire(&match result {
        Ok(()) => StudioResponse::WindowClosed,
        Err(reason) => StudioResponse::Rejected { reason },
    })
}

/// The native authoring host is authoritative when it is available. If it was
/// never initialized or failed during startup, the user may still explicitly
/// abandon the unavailable session; ordinary save-and-close is never upgraded
/// into a discard.
fn close_window_when_authorized(
    dirty: Option<bool>,
    discard_unsaved: bool,
    close: impl FnOnce() -> Result<(), String>,
) -> Result<(), String> {
    match dirty {
        Some(dirty) => check_window_close(dirty, discard_unsaved)?,
        None if !discard_unsaved => return Err(
            "État auteur indisponible : choisissez « Quitter sans enregistrer » pour abandonner explicitement cette session."
                .into(),
        ),
        None => {}
    }
    close()
}

fn destroy_confirmed_windows(window: &tauri::WebviewWindow) -> Result<(), String> {
    if let Some(atelier) = window
        .app_handle()
        .get_webview_window(native_atelier::LABEL)
    {
        atelier.destroy().map_err(|error| error.to_string())?;
        native_atelier::closed();
    }
    // Bypass CloseRequested only after explicit confirmation.
    window.destroy().map_err(|error| error.to_string())
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
    fn retired_playtest_rejects_both_launch_commands_before_opening_a_session() {
        use forge_project::authoring::{AuthoringRequest, playtest::PlaytestStart};
        let hash = forge_core::bytes_sha256(b"old-client-zone");
        for request in [
            AuthoringRequest::StartPlaytest {
                expected_hash: hash,
            },
            AuthoringRequest::StartPlaytestAt {
                expected_hash: hash,
                start: PlaytestStart {
                    x_m: 40.0,
                    z_m: -115.0,
                    facing_degrees: None,
                },
            },
        ] {
            assert_eq!(
                execute_studio_command_sync(StudioCommand::Authoring {
                    request: Box::new(request)
                }),
                StudioResponse::Rejected {
                    reason: forge_studio_frontend::EMBEDDED_PLAYTEST_RETIRED.into()
                },
            );
        }
        assert!(!forge_studio_frontend::embedded_playtest_available());
        assert!(forge_studio_frontend::validate_authoring_mode(&AuthoringRequest::Inspect).is_ok());
        assert!(forge_studio_frontend::validate_authoring_mode(&AuthoringRequest::Save).is_ok());
        assert!(
            forge_studio_frontend::validate_authoring_mode(&AuthoringRequest::StopPlaytest {
                diagnostics: None
            })
            .is_ok()
        );
    }

    #[test]
    fn native_close_never_discards_dirty_work_without_explicit_choice() {
        assert!(check_window_close(true, false).is_err());
        assert!(check_window_close(false, false).is_ok());
        assert!(check_window_close(true, true).is_ok());
    }

    #[test]
    fn unavailable_authoring_state_requires_an_explicit_discard() {
        let mut calls = 0;
        assert!(
            close_window_when_authorized(None, false, || {
                calls += 1;
                Ok(())
            })
            .is_err()
        );
        assert_eq!(calls, 0);

        assert!(
            close_window_when_authorized(None, true, || {
                calls += 1;
                Ok(())
            })
            .is_ok()
        );
        assert_eq!(calls, 1);
    }

    #[test]
    fn close_policy_keeps_dirty_guard_and_allows_confirmed_paths_once() {
        let mut calls = 0;
        assert!(
            close_window_when_authorized(Some(true), false, || {
                calls += 1;
                Ok(())
            })
            .is_err()
        );
        assert_eq!(calls, 0);

        assert!(
            close_window_when_authorized(Some(false), false, || {
                calls += 1;
                Ok(())
            })
            .is_ok()
        );
        assert_eq!(calls, 1);

        assert!(
            close_window_when_authorized(Some(true), true, || {
                calls += 1;
                Ok(())
            })
            .is_ok()
        );
        assert_eq!(calls, 2);
    }

    #[test]
    fn close_failure_is_returned_without_retrying_the_action() {
        let mut calls = 0;
        let error = close_window_when_authorized(Some(false), false, || {
            calls += 1;
            Err("native destroy failed".into())
        })
        .unwrap_err();
        assert_eq!(error, "native destroy failed");
        assert_eq!(calls, 1);
    }

    #[test]
    fn save_and_close_is_not_silently_converted_to_discard() {
        let mut calls = 0;
        assert!(
            close_window_when_authorized(None, false, || {
                calls += 1;
                Ok(())
            })
            .is_err()
        );
        assert_eq!(calls, 0);
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
        assert_eq!(permissions.len(), 11);
        assert_eq!(permissions[0], "allow-execute-studio-command");
        assert_eq!(permissions[1], "allow-execute-asset-import");
        assert_eq!(permissions[2], "allow-export-module");
        assert_eq!(permissions[3], "allow-read-asset-library");
        assert_eq!(permissions[4], "allow-read-asset-model");
        assert_eq!(permissions[5], "allow-read-asset-physics");
        assert_eq!(permissions[6], "allow-launch-native-game");
        assert_eq!(permissions[7], "allow-launch-exported-module");
        assert_eq!(permissions[8], "allow-native-game-running");
        assert_eq!(permissions[9], "allow-native-game-diagnostics");
        assert_eq!(permissions[10], "core:window:deny-set-title");
        let granted: Vec<_> = permissions
            .iter()
            .filter_map(Value::as_str)
            .filter(|identifier| !identifier.contains(":deny-"))
            .collect();
        assert_eq!(
            granted,
            [
                "allow-execute-studio-command",
                "allow-execute-asset-import",
                "allow-export-module",
                "allow-read-asset-library",
                "allow-read-asset-model",
                "allow-read-asset-physics",
                "allow-launch-native-game",
                "allow-launch-exported-module",
                "allow-native-game-running",
                "allow-native-game-diagnostics"
            ]
        );
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
