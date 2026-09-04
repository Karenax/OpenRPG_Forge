//! Native application session for project creation and explicit snapshot persistence.

use crate::{
    workspace_api::{MAX_WORKSPACE_IPC_REVISION, WorkspaceCommand, WorkspaceState},
    workspace_store,
};
use forge_core::{AreaId, CommandId, ContentHash, ModuleId, ProjectId, Revision, Vec3};
use forge_project::{NewProjectCommand, WorkspaceDocument};
use std::path::{Path, PathBuf};

#[derive(Default)]
pub struct WorkspaceSession {
    document: Option<WorkspaceDocument>,
    redo: Option<WorkspaceDocument>,
    undo_hash: Option<ContentHash>,
    saved_hash: Option<ContentHash>,
    saved_path: Option<PathBuf>,
    last_revision: u64,
    receipt: Option<String>,
}

impl WorkspaceSession {
    pub fn execute(&mut self, command: WorkspaceCommand) -> Result<WorkspaceState, String> {
        let message = match command {
            WorkspaceCommand::Current => "Projet courant".into(),
            WorkspaceCommand::Create {
                project_name,
                module_name,
                area_name,
                seed,
                spawn_x,
                spawn_y,
                spawn_z,
            } => {
                if self.document.is_some() {
                    return Err(
                        "Un projet est déjà ouvert. Annuler le brouillon avant une autre création."
                            .into(),
                    );
                }
                let key = workspace_store::unique_key()?;
                let command = NewProjectCommand {
                    command_id: CommandId::deterministic(&key),
                    project_id: ProjectId::deterministic(&key),
                    module_id: ModuleId::deterministic(&key),
                    area_id: AreaId::deterministic(&key),
                    project_name,
                    module_name,
                    area_name,
                    seed: seed.trim().parse().map_err(|_| {
                        "La seed doit être un entier positif sur 64 bits.".to_owned()
                    })?,
                    spawn: Vec3::new(
                        parse_coordinate(&spawn_x)?,
                        parse_coordinate(&spawn_y)?,
                        parse_coordinate(&spawn_z)?,
                    ),
                };
                let created = WorkspaceDocument::create(command).map_err(|e| e.to_string())?;
                let receipt = serde_json::to_string(&created.receipt).map_err(|e| e.to_string())?;
                self.last_revision = created.document.revision.get();
                self.undo_hash = Some(created.receipt.after_hash);
                self.document = Some(created.document);
                self.redo = None;
                self.saved_path = None;
                self.saved_hash = None;
                self.receipt = Some(receipt);
                "Projet, module et zone créés en mémoire — non enregistrés.".into()
            }
            WorkspaceCommand::Undo { base_revision } => {
                self.require_revision(base_revision)?;
                self.undo_creation()?
            }
            WorkspaceCommand::Redo { base_revision } => {
                self.require_revision(base_revision)?;
                self.redo_creation()?
            }
            WorkspaceCommand::Close => {
                if self.is_dirty()? {
                    return Err(
                        "Enregistrer ou annuler le brouillon avant de fermer le projet.".into(),
                    );
                }
                *self = Self::default();
                "Projet fermé. Aucun fichier supprimé.".into()
            }
            WorkspaceCommand::Save {
                directory,
                base_revision,
            } => {
                self.require_revision(base_revision)?;
                let document = self
                    .document
                    .as_ref()
                    .ok_or("Aucun projet à enregistrer.")?;
                let hash = document.content_hash().map_err(|e| e.to_string())?;
                let path = workspace_store::save_snapshot(document, Path::new(&directory))?;
                self.saved_hash = Some(hash);
                self.saved_path = Some(path);
                "Snapshot enregistré. Les fichiers précédents restent intacts.".into()
            }
            WorkspaceCommand::Open { path } => {
                if self.is_dirty()? {
                    return Err(
                        "Enregistrer ou annuler le brouillon avant d'ouvrir un projet.".into(),
                    );
                }
                let (document, path) = workspace_store::open_snapshot(Path::new(&path))?;
                ensure_ipc_revision(document.revision.get())?;
                let hash = document.content_hash().map_err(|e| e.to_string())?;
                self.last_revision = document.revision.get();
                self.document = Some(document);
                self.saved_hash = Some(hash);
                self.saved_path = Some(path);
                self.undo_hash = None;
                self.redo = None;
                self.receipt = None;
                "Projet rouvert depuis le snapshot vérifié.".into()
            }
        };
        self.state(message)
    }

    fn require_revision(&self, base_revision: u64) -> Result<(), String> {
        ensure_ipc_revision(base_revision)?;
        if base_revision != self.last_revision {
            return Err(format!(
                "Révision périmée : {base_revision}, révision courante {}.",
                self.last_revision
            ));
        }
        Ok(())
    }

    fn next_revision(&self) -> Result<u64, String> {
        let next = self
            .last_revision
            .checked_add(1)
            .ok_or("Révision épuisée.")?;
        ensure_ipc_revision(next)?;
        Ok(next)
    }

    fn undo_creation(&mut self) -> Result<String, String> {
        let expected = self.undo_hash.ok_or("Aucune création à annuler.")?;
        let document = self.document.as_ref().ok_or("Aucun brouillon à annuler.")?;
        if document.content_hash().map_err(|e| e.to_string())? != expected {
            return Err("Le brouillon a changé depuis la création : annulation refusée.".into());
        }
        let next = self.next_revision()?;
        let receipt = transition_receipt(
            "undo_create",
            self.last_revision,
            next,
            Some(expected),
            None,
        )?;
        self.redo = self.document.take();
        self.undo_hash = None;
        self.last_revision = next;
        self.saved_path = None;
        self.saved_hash = None;
        self.receipt = Some(receipt);
        Ok("Création annulée dans la session. Aucun fichier enregistré n'a été supprimé.".into())
    }

    fn redo_creation(&mut self) -> Result<String, String> {
        let mut document = self
            .redo
            .as_ref()
            .ok_or("Aucune création à rétablir.")?
            .clone();
        let next = self.next_revision()?;
        document.revision = Revision::new(next);
        document.project.module.area.terrain.revision = document.revision;
        let hash = document.content_hash().map_err(|e| e.to_string())?;
        let receipt =
            transition_receipt("redo_create", self.last_revision, next, None, Some(hash))?;
        self.document = Some(document);
        self.redo = None;
        self.undo_hash = Some(hash);
        self.last_revision = next;
        self.receipt = Some(receipt);
        Ok("Création rétablie avec une nouvelle révision — non enregistrée.".into())
    }

    fn is_dirty(&self) -> Result<bool, String> {
        self.document.as_ref().map_or(Ok(false), |document| {
            Ok(Some(document.content_hash().map_err(|e| e.to_string())?) != self.saved_hash)
        })
    }

    fn state(&self, message: String) -> Result<WorkspaceState, String> {
        Ok(WorkspaceState {
            revision: self.last_revision,
            document_json: self
                .document
                .as_ref()
                .map(WorkspaceDocument::to_pretty_json)
                .transpose()
                .map_err(|e| e.to_string())?,
            can_undo: self.undo_hash.is_some(),
            can_redo: self.redo.is_some(),
            saved_path: self
                .saved_path
                .as_ref()
                .map(|path| path.display().to_string()),
            dirty: self.is_dirty()?,
            message,
            last_receipt_json: self.receipt.clone(),
        })
    }
}

fn ensure_ipc_revision(revision: u64) -> Result<(), String> {
    if revision > MAX_WORKSPACE_IPC_REVISION {
        return Err(format!(
            "La révision dépasse la limite entière exacte du Studio ({MAX_WORKSPACE_IPC_REVISION})."
        ));
    }
    Ok(())
}

fn parse_coordinate(value: &str) -> Result<f32, String> {
    let coordinate: f32 = value
        .trim()
        .parse()
        .map_err(|_| "Coordonnée invalide.".to_owned())?;
    if !coordinate.is_finite() {
        return Err("Une coordonnée doit être finie.".into());
    }
    Ok(coordinate)
}

fn transition_receipt(
    action: &str,
    base: u64,
    next: u64,
    before: Option<ContentHash>,
    after: Option<ContentHash>,
) -> Result<String, String> {
    serde_json::to_string(&serde_json::json!({
        "schema": "forge.workspace-draft-transition", "schema_version": 1,
        "command_id": CommandId::deterministic(workspace_store::unique_key()?),
        "action": action, "base_revision": base, "new_revision": next,
        "before_hash": before, "after_hash": after, "disk_files_modified": false
    }))
    .map_err(|e| e.to_string())
}

#[cfg(test)]
#[path = "workspace_service_tests.rs"]
mod tests;
