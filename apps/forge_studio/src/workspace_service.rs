//! Native application session for project creation and explicit snapshot persistence.

use crate::{
    workspace_api::{MAX_WORKSPACE_IPC_REVISION, WorkspaceCommand, WorkspaceState},
    workspace_store,
};
use forge_core::{AreaId, CommandId, ContentHash, ModuleId, ProjectId, Revision, Vec3};
use forge_project::{
    NewProjectCommand, WorkspaceChange, WorkspaceCreationInverse, WorkspaceDocument, WorkspaceEdit,
};
use std::path::{Path, PathBuf};

/// At most 32 document snapshots across undo and redo (each validated at <= 1 MiB JSON).
pub const WORKSPACE_HISTORY_LIMIT: usize = 32;

#[derive(Default)]
pub struct WorkspaceSession {
    document: Option<WorkspaceDocument>,
    undo: Vec<Option<WorkspaceDocument>>,
    redo: Vec<Option<WorkspaceDocument>>,
    saved_hash: Option<ContentHash>,
    saved_path: Option<PathBuf>,
    last_revision: u64,
    receipt: Option<String>,
}

impl WorkspaceSession {
    /// Copies only the accepted source. Generation never holds the session lock.
    pub fn area_preview_source(
        &self,
        request: crate::area_preview::AreaPreviewRequest,
    ) -> Result<(forge_project::TerrainDocument, Vec3), String> {
        self.require_revision(request.base_revision)?;
        let document = self.document.as_ref().ok_or("Aucun projet ouvert.")?;
        if !request.key.exists(document) {
            return Err("La zone demandée n’appartient pas au projet courant.".into());
        }
        let area = document
            .area(request.key.module_id, request.key.area_id)
            .ok_or("Zone introuvable.")?;
        Ok((area.terrain.clone(), area.spawn))
    }

    pub fn execute(&mut self, command: WorkspaceCommand) -> Result<WorkspaceState, String> {
        let message = match command {
            WorkspaceCommand::Current => "Projet courant".into(),
            command @ WorkspaceCommand::Create { .. } => self.execute_creation(command)?,
            WorkspaceCommand::SculptTerrain {
                request,
                brush,
                additional_centers,
                dry_run,
            } => {
                return self.execute_brush(request, brush, &additional_centers, dry_run);
            }
            command @ (WorkspaceCommand::AddModule { .. }
            | WorkspaceCommand::AddArea { .. }
            | WorkspaceCommand::RenameProject { .. }
            | WorkspaceCommand::RenameModule { .. }
            | WorkspaceCommand::UpdateAreaMetadata { .. }) => self.execute_edit(command)?,
            WorkspaceCommand::Undo { base_revision } => {
                self.require_revision(base_revision)?;
                self.restore_history(false)?
            }
            WorkspaceCommand::Redo { base_revision } => {
                self.require_revision(base_revision)?;
                self.restore_history(true)?
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
                self.undo.clear();
                self.redo.clear();
                self.receipt = None;
                "Projet rouvert depuis le snapshot vérifié.".into()
            }
        };
        self.state(message)
    }

    fn execute_brush(
        &mut self,
        request: crate::area_preview::AreaPreviewRequest,
        brush: forge_world::TerrainBrush,
        additional_centers: &[[f32; 2]],
        dry_run: bool,
    ) -> Result<WorkspaceState, String> {
        self.require_revision(request.base_revision)?;
        self.next_revision()?;
        let document = self.document.as_ref().ok_or("Aucun projet ouvert.")?;
        let candidate = forge_world::preview_terrain_stroke(
            document,
            forge_world::TerrainBrushCommand {
                command_id: CommandId::deterministic(workspace_store::unique_key()?),
                base_revision: Revision::new(request.base_revision),
                project_id: request.key.project_id,
                module_id: request.key.module_id,
                area_id: request.key.area_id,
                brush,
            },
            additional_centers,
        )
        .map_err(|error| error.to_string())?;
        // Serialize intent, exact patch and inverse BEFORE the first mutation, including in dry-run.
        let receipt = serde_json::to_string(&candidate).map_err(|error| error.to_string())?;
        let samples = candidate.changed_samples;
        if dry_run {
            let mut state = self.state(format!(
                "Aperçu : {samples} hauteurs modifiées. Rien n’a été appliqué."
            ))?;
            state.last_receipt_json = Some(receipt);
            return Ok(state);
        }
        self.commit_edit(candidate.edit, receipt);
        self.state(format!(
            "Brosse appliquée : {samples} hauteurs modifiées. Annulation disponible."
        ))
    }

    fn execute_creation(&mut self, command: WorkspaceCommand) -> Result<String, String> {
        let WorkspaceCommand::Create {
            project_name,
            module_name,
            area_name,
            seed,
            spawn_x,
            spawn_y,
            spawn_z,
        } = command
        else {
            return Err("Commande de création attendue.".into());
        };
        if self.document.is_some() {
            return Err(
                "Un projet est déjà ouvert. Annuler le brouillon avant une autre création.".into(),
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
            seed: seed
                .trim()
                .parse()
                .map_err(|_| "La seed doit être un entier positif sur 64 bits.".to_owned())?,
            spawn: Vec3::new(
                parse_coordinate(&spawn_x)?,
                parse_coordinate(&spawn_y)?,
                parse_coordinate(&spawn_z)?,
            ),
        };
        let mut created = WorkspaceDocument::create(command).map_err(|e| e.to_string())?;
        let next = self.next_revision()?;
        stamp_revision(&mut created.document, next);
        let hash = created.document.content_hash().map_err(|e| e.to_string())?;
        created.receipt.new_revision = created.document.revision;
        created.receipt.after_hash = hash;
        created.receipt.inverse = WorkspaceCreationInverse::DiscardDraft {
            expected_after_hash: hash,
        };
        let receipt = serde_json::to_string(&created.receipt).map_err(|e| e.to_string())?;
        self.last_revision = created.document.revision.get();
        self.undo.clear();
        self.undo.push(None);
        self.document = Some(created.document);
        self.redo.clear();
        self.saved_path = None;
        self.saved_hash = None;
        self.receipt = Some(receipt);
        Ok("Projet, module et zone créés en mémoire — non enregistrés.".into())
    }

    fn execute_edit(&mut self, command: WorkspaceCommand) -> Result<String, String> {
        let message = match command {
            WorkspaceCommand::AddModule {
                base_revision,
                name,
            } => {
                let id = ModuleId::deterministic(workspace_store::unique_key()?);
                self.apply_change(
                    base_revision,
                    WorkspaceChange::AddModule {
                        module_id: id,
                        name,
                    },
                )?;
                "Module ajouté — sélectionnez-le pour créer sa première zone.".into()
            }
            WorkspaceCommand::AddArea {
                base_revision,
                module_id,
                name,
                seed,
                spawn_x,
                spawn_y,
                spawn_z,
            } => {
                let area_id = AreaId::deterministic(workspace_store::unique_key()?);
                let seed = seed
                    .trim()
                    .parse()
                    .map_err(|_| "Seed invalide : entier de 0 à 18446744073709551615.")?;
                let spawn = Vec3::new(
                    parse_coordinate(&spawn_x)?,
                    parse_coordinate(&spawn_y)?,
                    parse_coordinate(&spawn_z)?,
                );
                self.apply_change(
                    base_revision,
                    WorkspaceChange::AddExteriorArea {
                        module_id,
                        area_id,
                        name,
                        seed,
                        spawn,
                    },
                )?;
                "Zone extérieure ajoutée avec son propre terrain — non enregistrée.".into()
            }
            WorkspaceCommand::RenameProject {
                base_revision,
                name,
            } => {
                self.apply_change(base_revision, WorkspaceChange::RenameProject { name })?;
                "Nom du projet modifié — non enregistré.".into()
            }
            WorkspaceCommand::RenameModule {
                base_revision,
                module_id,
                name,
            } => {
                self.apply_change(
                    base_revision,
                    WorkspaceChange::RenameModule { module_id, name },
                )?;
                "Nom du module modifié — non enregistré.".into()
            }
            WorkspaceCommand::UpdateAreaMetadata {
                base_revision,
                module_id,
                area_id,
                name,
                spawn_x,
                spawn_y,
                spawn_z,
            } => {
                let spawn = Vec3::new(
                    parse_coordinate(&spawn_x)?,
                    parse_coordinate(&spawn_y)?,
                    parse_coordinate(&spawn_z)?,
                );
                self.apply_change(
                    base_revision,
                    WorkspaceChange::UpdateAreaMetadata {
                        module_id,
                        area_id,
                        name,
                        spawn,
                    },
                )?;
                "Nom et point de départ de la zone modifiés — non enregistrés.".into()
            }
            _ => return Err("Commande d'édition attendue.".into()),
        };
        Ok(message)
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

    fn apply_change(&mut self, base: u64, change: WorkspaceChange) -> Result<(), String> {
        self.require_revision(base)?;
        self.next_revision()?;
        let document = self.document.as_ref().ok_or("Aucun projet ouvert.")?;
        let candidate = document
            .preview_edit(WorkspaceEdit {
                command_id: CommandId::deterministic(workspace_store::unique_key()?),
                base_revision: Revision::new(base),
                change,
            })
            .map_err(|e| e.to_string())?;
        let receipt = serde_json::to_string(&candidate.receipt).map_err(|e| e.to_string())?;
        self.commit_edit(candidate, receipt);
        Ok(())
    }

    fn commit_edit(&mut self, candidate: forge_project::WorkspaceEditResult, receipt: String) {
        self.undo.push(self.document.take());
        if self.undo.len() > WORKSPACE_HISTORY_LIMIT {
            self.undo.remove(0);
        }
        self.redo.clear();
        self.last_revision = candidate.document.revision.get();
        self.document = Some(candidate.document);
        self.receipt = Some(receipt);
    }

    fn restore_history(&mut self, redo: bool) -> Result<String, String> {
        let history = if redo { &self.redo } else { &self.undo };
        let mut target = history
            .last()
            .ok_or("Aucune opération disponible dans cet historique.")?
            .clone();
        let next = self.next_revision()?;
        let before = self
            .document
            .as_ref()
            .map(WorkspaceDocument::content_hash)
            .transpose()
            .map_err(|e| e.to_string())?;
        if let Some(document) = &mut target {
            stamp_revision(document, next);
        }
        let after = target
            .as_ref()
            .map(WorkspaceDocument::content_hash)
            .transpose()
            .map_err(|e| e.to_string())?;
        let action = match (redo, self.document.is_some(), target.is_some()) {
            (false, true, false) => "undo_create",
            (true, false, true) => "redo_create",
            (false, _, _) => "undo",
            (true, _, _) => "redo",
        };
        let receipt = transition_receipt(action, self.last_revision, next, before, after)?;
        if redo {
            self.redo.pop();
            self.undo.push(self.document.take());
        } else {
            self.undo.pop();
            self.redo.push(self.document.take());
        }
        self.document = target;
        self.last_revision = next;
        self.receipt = Some(receipt);
        if self.document.is_none() {
            self.saved_hash = None;
            self.saved_path = None;
        }
        Ok(
            "Historique appliqué avec une nouvelle révision. Aucun fichier enregistré modifié."
                .into(),
        )
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
            can_undo: !self.undo.is_empty(),
            can_redo: !self.redo.is_empty(),
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

fn stamp_revision(document: &mut WorkspaceDocument, next: u64) {
    document.revision = Revision::new(next);
    for module in &mut document.project.modules {
        for area in &mut module.areas {
            area.terrain.revision = document.revision;
        }
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
