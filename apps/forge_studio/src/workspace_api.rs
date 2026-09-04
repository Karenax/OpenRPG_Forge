//! Small shared IPC boundary for the first project authoring workflow.

use forge_core::{AreaId, ModuleId};
use forge_project::{VegetationExclusionId, VegetationLayerId, VegetationSpeciesPreset};
use serde::{Deserialize, Serialize};

/// Largest revision representable exactly by the JavaScript number used by the IPC bridge.
/// The domain keeps its full `u64`; the native session rejects larger revisions before mutation.
pub const MAX_WORKSPACE_IPC_REVISION: u64 = (1_u64 << 53) - 1;

/// Geometry entered by the Studio before the native service assigns a stable exclusion ID.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "shape", rename_all = "snake_case", deny_unknown_fields)]
pub enum VegetationExclusionDraft {
    Circle {
        center_x_m: f32,
        center_z_m: f32,
        radius_m: f32,
    },
    CorridorSegment {
        start_x_m: f32,
        start_z_m: f32,
        end_x_m: f32,
        end_z_m: f32,
        half_width_m: f32,
    },
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "action", rename_all = "snake_case", deny_unknown_fields)]
pub enum WorkspaceCommand {
    SculptTerrain {
        request: crate::area_preview::AreaPreviewRequest,
        brush: forge_world::TerrainBrush,
        #[serde(default)]
        additional_centers: Vec<[f32; 2]>,
        /// Preview computes the same candidate but leaves the session and its history intact.
        dry_run: bool,
    },
    PaintTerrainMaterial {
        request: crate::area_preview::AreaPreviewRequest,
        brush: forge_world::TerrainMaterialBrush,
        #[serde(default)]
        additional_centers: Vec<[f32; 2]>,
        /// Preview computes the same candidate but leaves the session and its history intact.
        dry_run: bool,
    },
    PaintVegetationDensity {
        request: crate::area_preview::AreaPreviewRequest,
        brush: forge_world::VegetationDensityBrush,
        #[serde(default)]
        additional_centers: Vec<[f32; 2]>,
        /// Preview computes the same candidate but leaves the session and its history intact.
        dry_run: bool,
    },
    AddVegetationSpecies {
        request: crate::area_preview::AreaPreviewRequest,
        preset: VegetationSpeciesPreset,
        display_name: String,
    },
    RenameVegetationSpecies {
        request: crate::area_preview::AreaPreviewRequest,
        layer_id: VegetationLayerId,
        display_name: String,
    },
    RemoveVegetationSpecies {
        request: crate::area_preview::AreaPreviewRequest,
        layer_id: VegetationLayerId,
    },
    AddVegetationExclusion {
        request: crate::area_preview::AreaPreviewRequest,
        draft: VegetationExclusionDraft,
    },
    RemoveVegetationExclusion {
        request: crate::area_preview::AreaPreviewRequest,
        exclusion_id: VegetationExclusionId,
    },
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
        let request = crate::area_preview::AreaPreviewRequest {
            key: crate::area_tabs::AreaKey {
                project_id: forge_core::ProjectId::deterministic("ipc-project"),
                module_id: ModuleId::deterministic("ipc-module"),
                area_id: AreaId::deterministic("ipc-area"),
            },
            base_revision: 7,
        };
        for command in [
            WorkspaceCommand::Current,
            WorkspaceCommand::AddVegetationSpecies {
                request,
                preset: VegetationSpeciesPreset::Fern,
                display_name: "Fougère des sous-bois".into(),
            },
            WorkspaceCommand::RenameVegetationSpecies {
                request,
                layer_id: VegetationLayerId::new("species_01"),
                display_name: "Fougère renommée".into(),
            },
            WorkspaceCommand::RemoveVegetationSpecies {
                request,
                layer_id: VegetationLayerId::new("species_01"),
            },
            WorkspaceCommand::AddVegetationExclusion {
                request,
                draft: VegetationExclusionDraft::Circle {
                    center_x_m: 1.5,
                    center_z_m: -2.5,
                    radius_m: 8.0,
                },
            },
            WorkspaceCommand::AddVegetationExclusion {
                request,
                draft: VegetationExclusionDraft::CorridorSegment {
                    start_x_m: -8.0,
                    start_z_m: 0.0,
                    end_x_m: 8.0,
                    end_z_m: 0.0,
                    half_width_m: 2.0,
                },
            },
            WorkspaceCommand::RemoveVegetationExclusion {
                request,
                exclusion_id: VegetationExclusionId::new("exclusion_01"),
            },
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
        assert!(
            serde_json::from_str::<WorkspaceCommand>(
                r#"{"action":"add_vegetation_exclusion","request":{"key":{"project_id":"00000000-0000-0000-0000-000000000001","module_id":"00000000-0000-0000-0000-000000000002","area_id":"00000000-0000-0000-0000-000000000003"},"base_revision":1},"draft":{"shape":"circle","center_x_m":0.0,"center_z_m":0.0,"radius_m":2.0,"unexpected":true}}"#
            )
            .is_err()
        );
        assert!(
            serde_json::from_str::<VegetationExclusionDraft>(
                r#"{"shape":"corridor_segment","start_x_m":0.0,"start_z_m":0.0,"end_x_m":1.0,"end_z_m":1.0,"half_width_m":1.0,"radius_m":2.0}"#
            )
            .is_err()
        );
    }
}
