#![deny(unsafe_code)]

use forge_studio_frontend::{StudioCommand, StudioResponse};
use forge_world::embedded_gate0_proof_document;

#[tauri::command]
#[allow(
    clippy::needless_pass_by_value,
    reason = "Tauri commands deserialize request payloads into owned Rust values"
)]
fn execute_studio_command(command: StudioCommand) -> StudioResponse {
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
    }
}

fn main() {
    tauri::Builder::default()
        .invoke_handler(tauri::generate_handler![execute_studio_command])
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
        let document = embedded_gate0_proof_document().unwrap();
        let known = document.entities[0].id;
        assert_eq!(
            execute_studio_command(StudioCommand::SelectEntity { entity_id: known }),
            StudioResponse::SelectionAccepted { entity_id: known }
        );

        let unknown = EntityId::deterministic("not-in-gate-0-scene");
        assert!(matches!(
            execute_studio_command(StudioCommand::SelectEntity { entity_id: unknown }),
            StudioResponse::Rejected { .. }
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
}
