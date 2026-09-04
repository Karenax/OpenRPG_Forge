//! Small shared IPC boundary for the first project authoring workflow.

use forge_core::{AreaId, ModuleId};
use serde::{Deserialize, Serialize};

/// Largest revision representable exactly by the JavaScript number used by the IPC bridge.
/// The domain keeps its full `u64`; the native session rejects larger revisions before mutation.
pub const MAX_WORKSPACE_IPC_REVISION: u64 = (1_u64 << 53) - 1;

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(tag = "action", rename_all = "snake_case", deny_unknown_fields)]
pub enum WorkspaceCommand {
    Current,
    Create {
        project_name: String,
        module_name: String,
        area_name: String,
        seed: String,
        spawn_x: String,
        spawn_y: String,
        spawn_z: String,
    },
    AddModule {
        base_revision: u64,
        name: String,
    },
    AddArea {
        base_revision: u64,
        module_id: ModuleId,
        name: String,
        seed: String,
        spawn_x: String,
        spawn_y: String,
        spawn_z: String,
    },
    RenameProject {
        base_revision: u64,
        name: String,
    },
    RenameModule {
        base_revision: u64,
        module_id: ModuleId,
        name: String,
    },
    UpdateAreaMetadata {
        base_revision: u64,
        module_id: ModuleId,
        area_id: AreaId,
        name: String,
        spawn_x: String,
        spawn_y: String,
        spawn_z: String,
    },
    Undo {
        base_revision: u64,
    },
    Redo {
        base_revision: u64,
    },
    Close,
    Save {
        directory: String,
        base_revision: u64,
    },
    Open {
        path: String,
    },
}

/// The canonical document comes from the Rust service, never from a UI reconstruction.
#[derive(Clone, Debug, Default, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorkspaceState {
    /// Zero denotes an empty initial session; all emitted values fit the IPC integer bound.
    pub revision: u64,
    pub document_json: Option<String>,
    pub can_undo: bool,
    pub can_redo: bool,
    pub saved_path: Option<String>,
    pub dirty: bool,
    pub message: String,
    pub last_receipt_json: Option<String>,
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn commands_round_trip_and_reject_unknown_fields() {
        for command in [
            WorkspaceCommand::Current,
            WorkspaceCommand::Undo { base_revision: 1 },
            WorkspaceCommand::Redo { base_revision: 2 },
            WorkspaceCommand::Close,
            WorkspaceCommand::Save {
                directory: "workspace".into(),
                base_revision: 1,
            },
            WorkspaceCommand::Open {
                path: "project.forge.json".into(),
            },
        ] {
            let json = serde_json::to_string(&command).unwrap();
            assert_eq!(
                serde_json::from_str::<WorkspaceCommand>(&json).unwrap(),
                command
            );
        }
        assert!(
            serde_json::from_str::<WorkspaceCommand>(
                r#"{"action":"save","directory":"x","overwrite":true}"#
            )
            .is_err()
        );
    }
}
