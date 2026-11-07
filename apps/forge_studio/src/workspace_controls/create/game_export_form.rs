//! Pure readiness check for exporting the saved project selection.
use forge_core::{ContentHash, ProjectId};
use forge_project::authoring::workspace::WorkspaceHostState;

pub(super) fn reviewed_workspace_hash(
    state: &WorkspaceHostState,
    project_id: ProjectId,
    drafts_pending: bool,
    proposal_pending: bool,
) -> Result<ContentHash, String> {
    if drafts_pending || proposal_pending {
        return Err(
            "Examinez ou abandonnez les saisies et propositions en cours avant l’export.".into(),
        );
    }
    if state.dirty {
        return Err("Sauvegardez le projet et ses cartes avant l’export.".into());
    }
    if state.recovery.is_some() {
        return Err("Terminez la reprise de l’espace de travail avant l’export.".into());
    }
    let head = state.head.as_ref().ok_or("Ouvrez un espace de travail.")?;
    head.derive_game_manifest(project_id)
        .map_err(|error| format!("Configuration du jeu incomplète : {error}"))?;
    head.content_hash().map_err(|error| error.to_string())
}
