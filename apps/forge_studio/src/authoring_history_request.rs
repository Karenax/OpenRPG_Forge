//! History follows the authority that owns the active document.
use forge_core::CommandId;
use forge_project::{
    authoring::{AuthoringRequest, AuthoringState},
    terrain_edit::{TerrainAction, TerrainEditRequest},
};

#[must_use]
pub fn available(state: &AuthoringState, redo: bool) -> bool {
    if state.document.is_none() || state.playtest_hash.is_some() {
        return false;
    }
    state.module_creator_context.as_ref().map_or_else(
        || if redo { state.can_redo } else { state.can_undo },
        |context| {
            if redo {
                context.can_redo
            } else {
                context.can_undo
            }
        },
    )
}

/// Managed history is reviewed across the module before the normal Apply action.
/// Standalone zone and legacy terrain history keep their existing scope.
pub fn prepare(state: &AuthoringState, redo: bool) -> Result<AuthoringRequest, String> {
    if !available(state, redo) {
        return Err("Aucune retouche à annuler ou rétablir dans cette autorité".into());
    }
    if let Some(context) = &state.module_creator_context {
        return Ok(AuthoringRequest::PreviewModuleHistory {
            expected_workspace_hash: context.workspace_hash,
            redo,
            command_id: CommandId::deterministic(format!(
                "module-history/{}/{redo}",
                context.workspace_hash
            )),
        });
    }
    let action = if redo {
        TerrainAction::Redo
    } else {
        TerrainAction::Undo
    };
    if let Some(head) = &state.zone_head {
        let hash = head.content_hash()?;
        return Ok(AuthoringRequest::ApplyZone {
            request: head.prepare_terrain_action(
                action,
                CommandId::deterministic(format!("zone-history/{hash}/{redo}")),
            )?,
        });
    }
    let document = state.document.as_ref().ok_or("Ouvrir une zone")?;
    let hash = document.content_hash().map_err(|error| error.to_string())?;
    Ok(AuthoringRequest::Apply {
        request: TerrainEditRequest {
            command_id: CommandId::deterministic(format!("terrain-history/{hash}/{redo}")),
            expected_revision: document.revision,
            expected_hash: hash,
            action,
        },
    })
}
