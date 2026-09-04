#![deny(unsafe_code)]

use forge_studio_frontend::workspace_api::WorkspaceCommand;
use forge_studio_frontend::workspace_service::WorkspaceSession;
use forge_studio_frontend::{StudioCommand, StudioResponse};
use forge_world::embedded_gate0_proof_document;
use std::sync::{Arc, Mutex, TryLockError};
use tauri::Manager;

const STUDIO_WINDOW_TITLE: &str = "OpenRPG Forge Studio";
const DIRTY_CLOSE_MESSAGE: &str =
    "Brouillon non enregistré — revenir à Projets, enregistrer ou annuler avant de fermer";
const BUSY_CLOSE_MESSAGE: &str = "Opération en cours — fermeture bloquée, réessayer après sa fin";
const UNAVAILABLE_CLOSE_MESSAGE: &str =
    "Session indisponible — fermeture bloquée pour protéger le brouillon";

#[tauri::command]
#[allow(
    clippy::needless_pass_by_value,
    reason = "Tauri commands deserialize request payloads into owned Rust values"
)]
async fn execute_studio_command(
    command: StudioCommand,
    session: tauri::State<'_, Arc<Mutex<WorkspaceSession>>>,
) -> Result<StudioResponse, String> {
    if matches!(
        &command,
        StudioCommand::Workspace {
            request: WorkspaceCommand::SculptTerrain { .. }
        }
    ) {
        let session = Arc::clone(session.inner());
        return tauri::async_runtime::spawn_blocking(move || execute_command(command, &session))
            .await
            .map_err(|error| format!("Brosse interrompue : {error}"));
    }
    if let StudioCommand::AreaPreview { request } = command {
        let source = match session.lock() {
            Ok(session) => session.area_preview_source(request),
            Err(_) => Err("Session projet indisponible.".into()),
        };
        let (document, spawn) = match source {
            Ok(source) => source,
            Err(reason) => return Ok(StudioResponse::Rejected { reason }),
        };
        let permit = PreviewPermit::acquire()?;
        return Ok(
            match tauri::async_runtime::spawn_blocking(move || {
                let _permit = permit;
                forge_studio_frontend::area_preview::prepare_area_preview(request, &document, spawn)
            })
            .await
            {
                Ok(Ok(preview_json)) => StudioResponse::AreaPreview { preview_json },
                Ok(Err(reason)) => StudioResponse::Rejected { reason },
                Err(error) => StudioResponse::Rejected {
                    reason: format!("Génération interrompue : {error}"),
                },
            },
        );
    }
    Ok(execute_command(command, &session))
}

/// One bounded generation per Studio, including callers outside the UI queue.
static PREVIEW_BUSY: std::sync::atomic::AtomicBool = std::sync::atomic::AtomicBool::new(false);
struct PreviewPermit;
impl PreviewPermit {
    fn acquire() -> Result<Self, String> {
        PREVIEW_BUSY
            .compare_exchange(
                false,
                true,
                std::sync::atomic::Ordering::AcqRel,
                std::sync::atomic::Ordering::Acquire,
            )
            .map(|_| Self)
            .map_err(|_| "Un aperçu est déjà en préparation. Réessayez après sa fin.".into())
    }
}
impl Drop for PreviewPermit {
    fn drop(&mut self) {
        PREVIEW_BUSY.store(false, std::sync::atomic::Ordering::Release);
    }
}

fn execute_command(command: StudioCommand, session: &Mutex<WorkspaceSession>) -> StudioResponse {
    if let StudioCommand::Workspace { request } = command {
        return match session.lock() {
            Ok(mut session) => match session.execute(request) {
                Ok(state) => StudioResponse::Workspace { state },
                Err(reason) => StudioResponse::Rejected { reason },
            },
            Err(_) => StudioResponse::Rejected {
                reason: "Session projet indisponible après une erreur interne.".into(),
            },
        };
    }
    let document = embedded_gate0_proof_document().expect("embedded Gate 0 JSON must remain valid");
    match command {
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
        StudioCommand::Workspace { .. } | StudioCommand::AreaPreview { .. } => {
            StudioResponse::Rejected {
                reason: "Commande projet déjà traitée.".into(),
            }
        }
    }
}

/// Never wait on a save from the native event loop, and never assume an unreadable draft is clean.
fn native_close_block_reason(session: &Mutex<WorkspaceSession>) -> Option<&'static str> {
    match session.try_lock() {
        Ok(mut session) => match session.execute(WorkspaceCommand::Current) {
            Ok(state) if state.dirty => Some(DIRTY_CLOSE_MESSAGE),
            Ok(_) => None,
            Err(_) => Some(UNAVAILABLE_CLOSE_MESSAGE),
        },
        Err(TryLockError::WouldBlock) => Some(BUSY_CLOSE_MESSAGE),
        Err(TryLockError::Poisoned(_)) => Some(UNAVAILABLE_CLOSE_MESSAGE),
    }
}

fn main() {
    tauri::Builder::default()
        .manage(Arc::new(Mutex::new(WorkspaceSession::default())))
        .invoke_handler(tauri::generate_handler![execute_studio_command])
        .on_window_event(|window, event| {
            if let tauri::WindowEvent::CloseRequested { api, .. } = event {
                let session = window.state::<Arc<Mutex<WorkspaceSession>>>();
                let title = if let Some(reason) = native_close_block_reason(&session) {
                    api.prevent_close();
                    // Native chrome remains visible on the project page and both proof scenes.
                    // This needs neither a browser listener nor an additional Tauri permission.
                    format!("{STUDIO_WINDOW_TITLE} — {reason}")
                } else {
                    STUDIO_WINDOW_TITLE.to_owned()
                };
                if let Err(error) = window.set_title(&title) {
                    eprintln!("Cannot display native close status: {error}");
                }
            }
        })
        .run(tauri::generate_context!())
        .expect("failed to run OpenRPG Forge Studio");
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_core::EntityId;
    use serde_json::Value;

    #[test]
    fn selection_accepts_only_entities_from_the_shared_document() {
        let session = Mutex::new(WorkspaceSession::default());
        let document = embedded_gate0_proof_document().unwrap();
        let known = document.entities[0].id;
        assert_eq!(
            execute_command(StudioCommand::SelectEntity { entity_id: known }, &session),
            StudioResponse::SelectionAccepted { entity_id: known }
        );

        let unknown = EntityId::deterministic("not-in-gate-0-scene");
        assert!(matches!(
            execute_command(StudioCommand::SelectEntity { entity_id: unknown }, &session),
            StudioResponse::Rejected { .. }
        ));
    }

    #[test]
    fn native_close_preserves_dirty_draft_until_its_creation_is_explicitly_undone() {
        let session = Mutex::new(WorkspaceSession::default());
        assert_eq!(native_close_block_reason(&session), None);
        let created = session
            .lock()
            .unwrap()
            .execute(WorkspaceCommand::Create {
                project_name: "Projet de fermeture".into(),
                module_name: "Module".into(),
                area_name: "Zone".into(),
                seed: "42".into(),
                spawn_x: "0".into(),
                spawn_y: "2".into(),
                spawn_z: "0".into(),
            })
            .unwrap();
        assert_eq!(
            native_close_block_reason(&session),
            Some(DIRTY_CLOSE_MESSAGE)
        );
        let current = session
            .lock()
            .unwrap()
            .execute(WorkspaceCommand::Current)
            .unwrap();
        assert_eq!(current.document_json, created.document_json);
        assert_eq!(current.last_receipt_json, created.last_receipt_json);
        assert_eq!(current.revision, created.revision);
        let undone = session
            .lock()
            .unwrap()
            .execute(WorkspaceCommand::Undo {
                base_revision: current.revision,
            })
            .unwrap();
        assert_eq!(native_close_block_reason(&session), None);
        session
            .lock()
            .unwrap()
            .execute(WorkspaceCommand::Redo {
                base_revision: undone.revision,
            })
            .unwrap();
        assert_eq!(
            native_close_block_reason(&session),
            Some(DIRTY_CLOSE_MESSAGE)
        );
    }

    #[test]
    fn native_close_refuses_busy_or_poisoned_session_without_blocking() {
        let session = Mutex::new(WorkspaceSession::default());
        let held = session.lock().unwrap();
        assert_eq!(
            native_close_block_reason(&session),
            Some(BUSY_CLOSE_MESSAGE)
        );
        drop(held);
        assert_eq!(native_close_block_reason(&session), None);
        let poison = std::panic::catch_unwind(|| {
            let _guard = session.lock().unwrap();
            panic!("synthetic session failure");
        });
        assert!(poison.is_err());
        assert_eq!(
            native_close_block_reason(&session),
            Some(UNAVAILABLE_CLOSE_MESSAGE)
        );
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
}
