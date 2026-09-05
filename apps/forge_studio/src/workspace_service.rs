//! Native application session for project creation and explicit snapshot persistence.

use crate::{
    workspace_api::{
        MAX_WORKSPACE_IPC_REVISION, VegetationDensityStrokePreviewRequest,
        VegetationExclusionDraft, WorkspaceCommand, WorkspaceState,
    },
    workspace_store,
};
use forge_core::{AreaId, CommandId, ContentHash, ModuleId, ProjectId, Revision, Vec3};
use forge_project::{
    AssetPin, AuthoredRiver, AuthoredRoad, AuthoredWall, MAX_PATHS_PER_DOCUMENT,
    MAX_VEGETATION_EXCLUSIONS, MAX_VEGETATION_LAYERS, NewProjectCommand, PathId,
    TerrainMaterialRole, VegetationExclusion, VegetationExclusionId, VegetationLayer,
    VegetationLayerId, WorkspaceArea, WorkspaceChange, WorkspaceCreationInverse, WorkspaceDocument,
    WorkspaceEdit, WorkspaceError,
};
use std::path::{Path, PathBuf};

/// At most 32 document snapshots across undo and redo (each validated at <= 1 MiB JSON).
pub const WORKSPACE_HISTORY_LIMIT: usize = 32;

/// Disposable candidate copied out of the canonical session for renderer preparation.
pub struct VegetationDensityStrokePreviewSource {
    pub area: WorkspaceArea,
    pub changed_cells: usize,
    pub no_op: bool,
    pub candidate_revision: u64,
    pub candidate_density_source_hash: ContentHash,
}

/// Canonical snapshot copied under the session lock and resolved on the blocking pool.
pub struct VegetationDensityStrokePreviewInput {
    document: WorkspaceDocument,
    preview: VegetationDensityStrokePreviewRequest,
}

impl VegetationDensityStrokePreviewInput {
    pub fn resolve(
        self,
    ) -> Result<
        (
            VegetationDensityStrokePreviewRequest,
            VegetationDensityStrokePreviewSource,
        ),
        String,
    > {
        let preview = self.preview;
        let document = self.document;
        let command = forge_world::VegetationDensityBrushCommand {
            command_id: CommandId::deterministic(format!(
                "studio/vegetation-density-preview/{}/{}/{}",
                preview.request.key.area_id, preview.request.base_revision, preview.preview_serial
            )),
            base_revision: Revision::new(preview.request.base_revision),
            project_id: preview.request.key.project_id,
            module_id: preview.request.key.module_id,
            area_id: preview.request.key.area_id,
            brush: preview.brush.clone(),
        };
        let source = match forge_world::preview_vegetation_density_stroke(
            &document,
            command,
            &preview.additional_centers,
        ) {
            Ok(candidate) => changed_density_preview_source(&preview, &candidate)?,
            Err(forge_world::VegetationDensityBrushError::Workspace(WorkspaceError::NoChange)) => {
                no_op_density_preview_source(&preview, &document)?
            }
            Err(error) => return Err(error.to_string()),
        };
        Ok((preview, source))
    }
}

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
    /// Applies one native-resolved governed asset pin. Browser-originated paths or provenance never
    /// enter this boundary; only the renderer-derived X/Z coordinates remain client input.
    #[allow(
        clippy::too_many_arguments,
        reason = "surface correlation stays explicit beside the canonical placement request"
    )]
    pub fn execute_place_asset(
        &mut self,
        request: crate::area_preview::AreaPreviewRequest,
        asset_pin: AssetPin,
        world_x_m: f32,
        world_depth_m: f32,
        surface_generation: u64,
        surface_serial: u64,
    ) -> Result<WorkspaceState, String> {
        ensure_ipc_revision(surface_generation)?;
        ensure_ipc_revision(surface_serial)?;
        if surface_generation == 0 || surface_serial == 0 {
            return Err("Le ciblage du terrain doit être corrélé à une projection active.".into());
        }
        self.require_revision(request.base_revision)?;
        self.next_revision()?;
        let document = self.document.as_ref().ok_or("Aucun projet ouvert.")?;
        if !request.key.exists(document) {
            return Err("La zone demandée n’appartient pas au projet courant.".into());
        }
        let area = document
            .area(request.key.module_id, request.key.area_id)
            .ok_or("Zone introuvable.")?;
        let command = forge_world::PlaceAssetCommand {
            command_id: CommandId::deterministic(workspace_store::unique_key()?),
            base_revision: Revision::new(request.base_revision),
            project_id: request.key.project_id,
            module_id: request.key.module_id,
            area_id: request.key.area_id,
            expected_terrain_document_hash: area
                .terrain
                .content_hash()
                .map_err(|error| error.to_string())?,
            expected_path_document_hash: area
                .paths
                .content_hash()
                .map_err(|error| error.to_string())?,
            expected_road_document_hash: area
                .roads
                .content_hash()
                .map_err(|error| error.to_string())?,
            expected_river_document_hash: area
                .rivers
                .content_hash()
                .map_err(|error| error.to_string())?,
            expected_placement_document_hash: area
                .placements
                .content_hash()
                .map_err(|error| error.to_string())?,
            asset_pin,
            drop_x_m: world_x_m,
            drop_z_m: world_depth_m,
        };
        let candidate = forge_world::preview_place_asset(document, &command)
            .map_err(|error| error.to_string())?;
        let placement_id = candidate.placement.id;
        let receipt = serde_json::to_string(&serde_json::json!({
            "schema": "forge.studio-place-asset-receipt",
            "schema_version": 1,
            "surface_generation": surface_generation,
            "surface_serial": surface_serial,
            "result": &candidate,
        }))
        .map_err(|error| error.to_string())?;
        self.commit_edit(candidate.edit, receipt);
        self.state(format!(
            "Asset placé sous l’identifiant {placement_id}. Annulation disponible."
        ))
    }

    /// Copies only the accepted source. Generation never holds the session lock.
    pub fn area_preview_source(
        &self,
        request: crate::area_preview::AreaPreviewRequest,
    ) -> Result<forge_project::WorkspaceArea, String> {
        self.require_revision(request.base_revision)?;
        let document = self.document.as_ref().ok_or("Aucun projet ouvert.")?;
        if !request.key.exists(document) {
            return Err("La zone demandée n’appartient pas au projet courant.".into());
        }
        let area = document
            .area(request.key.module_id, request.key.area_id)
            .ok_or("Zone introuvable.")?;
        Ok(area.clone())
    }

    /// Copies the accepted source; candidate computation happens after releasing the session lock.
    pub fn vegetation_density_stroke_preview_input(
        &self,
        preview: VegetationDensityStrokePreviewRequest,
    ) -> Result<VegetationDensityStrokePreviewInput, String> {
        ensure_ipc_revision(preview.preview_serial)?;
        self.require_revision(preview.request.base_revision)?;
        let document = self.document.as_ref().ok_or("Aucun projet ouvert.")?;
        if !preview.request.key.exists(document) {
            return Err("La zone demandée n’appartient pas au projet courant.".into());
        }
        Ok(VegetationDensityStrokePreviewInput {
            document: document.clone(),
            preview,
        })
    }

    pub fn execute(&mut self, command: WorkspaceCommand) -> Result<WorkspaceState, String> {
        let message = match command {
            WorkspaceCommand::Current => "Projet courant".into(),
            WorkspaceCommand::PlaceAsset { .. } => {
                return Err(
                    "Le placement doit être résolu par le service Assets natif vérifié.".into(),
                );
            }
            command @ WorkspaceCommand::Create { .. } => self.execute_creation(command)?,
            WorkspaceCommand::SculptTerrain {
                request,
                brush,
                additional_centers,
                dry_run,
            } => {
                return self.execute_brush(request, brush, &additional_centers, dry_run);
            }
            WorkspaceCommand::PaintTerrainMaterial {
                request,
                brush,
                additional_centers,
                dry_run,
            } => {
                return self.execute_material_brush(request, brush, &additional_centers, dry_run);
            }
            WorkspaceCommand::PaintVegetationDensity {
                request,
                brush,
                additional_centers,
                dry_run,
            } => {
                return self.execute_vegetation_density_brush(
                    request,
                    brush,
                    &additional_centers,
                    dry_run,
                );
            }
            command @ (WorkspaceCommand::PromoteVegetationInstance { .. }
            | WorkspaceCommand::RetouchPromotedVegetationInstance { .. }) => {
                return self.execute_vegetation_instance_edit(command);
            }
            command @ (WorkspaceCommand::CreatePath { .. }
            | WorkspaceCommand::RedrawPath { .. }
            | WorkspaceCommand::UpsertRoad { .. }
            | WorkspaceCommand::UpsertRiver { .. }
            | WorkspaceCommand::UpsertWall { .. }) => return self.execute_path_edit(command),
            command @ (WorkspaceCommand::AddModule { .. }
            | WorkspaceCommand::AddArea { .. }
            | WorkspaceCommand::RenameProject { .. }
            | WorkspaceCommand::RenameModule { .. }
            | WorkspaceCommand::UpdateAreaMetadata { .. }) => self.execute_edit(command)?,
            command @ (WorkspaceCommand::AddVegetationSpecies { .. }
            | WorkspaceCommand::RenameVegetationSpecies { .. }
            | WorkspaceCommand::RemoveVegetationSpecies { .. }
            | WorkspaceCommand::AddVegetationExclusion { .. }
            | WorkspaceCommand::RemoveVegetationExclusion { .. }) => {
                self.execute_vegetation_edit(command)?
            }
            command @ (WorkspaceCommand::Undo { .. } | WorkspaceCommand::Redo { .. }) => {
                self.execute_history(&command)?
            }
            WorkspaceCommand::Close => self.close_workspace()?,
            WorkspaceCommand::Save {
                directory,
                base_revision,
            } => self.save_workspace(&directory, base_revision)?,
            WorkspaceCommand::Open { path } => self.open_workspace(&path)?,
        };
        self.state(message)
    }

    fn close_workspace(&mut self) -> Result<String, String> {
        if self.is_dirty()? {
            return Err("Enregistrer ou annuler le brouillon avant de fermer le projet.".into());
        }
        *self = Self::default();
        Ok("Projet fermé. Aucun fichier supprimé.".into())
    }

    fn save_workspace(&mut self, directory: &str, base_revision: u64) -> Result<String, String> {
        self.require_revision(base_revision)?;
        let document = self
            .document
            .as_ref()
            .ok_or("Aucun projet à enregistrer.")?;
        let hash = document.content_hash().map_err(|error| error.to_string())?;
        let path = workspace_store::save_snapshot(document, Path::new(directory))?;
        self.saved_hash = Some(hash);
        self.saved_path = Some(path);
        Ok("Snapshot enregistré. Les fichiers précédents restent intacts.".into())
    }

    fn open_workspace(&mut self, path: &str) -> Result<String, String> {
        if self.is_dirty()? {
            return Err("Enregistrer ou annuler le brouillon avant d'ouvrir un projet.".into());
        }
        let (document, path) = workspace_store::open_snapshot(Path::new(path))?;
        ensure_ipc_revision(document.revision.get())?;
        let hash = document.content_hash().map_err(|error| error.to_string())?;
        self.last_revision = document.revision.get();
        self.document = Some(document);
        self.saved_hash = Some(hash);
        self.saved_path = Some(path);
        self.undo.clear();
        self.redo.clear();
        self.receipt = None;
        Ok("Projet rouvert depuis le snapshot vérifié.".into())
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

    fn execute_history(&mut self, command: &WorkspaceCommand) -> Result<String, String> {
        match command {
            WorkspaceCommand::Undo { base_revision } => {
                self.require_revision(*base_revision)?;
                self.restore_history(false)
            }
            WorkspaceCommand::Redo { base_revision } => {
                self.require_revision(*base_revision)?;
                self.restore_history(true)
            }
            _ => Err("Commande d’historique attendue.".into()),
        }
    }

    fn execute_material_brush(
        &mut self,
        request: crate::area_preview::AreaPreviewRequest,
        brush: forge_world::TerrainMaterialBrush,
        additional_centers: &[[f32; 2]],
        dry_run: bool,
    ) -> Result<WorkspaceState, String> {
        self.require_revision(request.base_revision)?;
        self.next_revision()?;
        let document = self.document.as_ref().ok_or("Aucun projet ouvert.")?;
        let candidate = forge_world::preview_terrain_material_stroke(
            document,
            forge_world::TerrainMaterialBrushCommand {
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
        let receipt = serde_json::to_string(&candidate).map_err(|error| error.to_string())?;
        let samples = candidate.changed_samples;
        if dry_run {
            let mut state = self.state(format!(
                "Aperçu : {samples} poids de matériaux modifiés. Rien n’a été appliqué."
            ))?;
            state.last_receipt_json = Some(receipt);
            return Ok(state);
        }
        self.commit_edit(candidate.edit, receipt);
        self.state(format!(
            "Matériau peint : {samples} poids modifiés. Annulation disponible."
        ))
    }

    fn execute_vegetation_density_brush(
        &mut self,
        request: crate::area_preview::AreaPreviewRequest,
        brush: forge_world::VegetationDensityBrush,
        additional_centers: &[[f32; 2]],
        dry_run: bool,
    ) -> Result<WorkspaceState, String> {
        self.require_revision(request.base_revision)?;
        self.next_revision()?;
        let document = self.document.as_ref().ok_or("Aucun projet ouvert.")?;
        let candidate = forge_world::preview_vegetation_density_stroke(
            document,
            forge_world::VegetationDensityBrushCommand {
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
        let receipt = serde_json::to_string(&candidate).map_err(|error| error.to_string())?;
        let cells = candidate.changed_cells;
        if dry_run {
            let mut state = self.state(format!(
                "Aperçu : {cells} cellules de densité végétale modifiées. Rien n’a été appliqué."
            ))?;
            state.last_receipt_json = Some(receipt);
            return Ok(state);
        }
        self.commit_edit(candidate.edit, receipt);
        self.state(format!(
            "Densité végétale peinte : {cells} cellules modifiées. Annulation disponible."
        ))
    }

    fn execute_vegetation_promotion(
        &mut self,
        request: crate::area_preview::AreaPreviewRequest,
        target: forge_world::VegetationPromotionTarget,
    ) -> Result<WorkspaceState, String> {
        self.require_revision(request.base_revision)?;
        self.next_revision()?;
        let document = self.document.as_ref().ok_or("Aucun projet ouvert.")?;
        let candidate = forge_world::preview_promote_generated_vegetation_instance(
            document,
            forge_world::VegetationPromotionCommand {
                command_id: CommandId::deterministic(workspace_store::unique_key()?),
                base_revision: Revision::new(request.base_revision),
                project_id: request.key.project_id,
                module_id: request.key.module_id,
                area_id: request.key.area_id,
                target,
            },
        )
        .map_err(|error| error.to_string())?;
        let promoted_id = candidate.promoted_instance.id;
        let receipt = serde_json::to_string(&candidate).map_err(|error| error.to_string())?;
        self.commit_edit(candidate.edit, receipt);
        self.state(format!(
            "Instance végétale {promoted_id} promue. Annulation disponible."
        ))
    }

    fn execute_vegetation_instance_edit(
        &mut self,
        command: WorkspaceCommand,
    ) -> Result<WorkspaceState, String> {
        match command {
            WorkspaceCommand::PromoteVegetationInstance { request, target } => {
                self.execute_vegetation_promotion(request, target)
            }
            WorkspaceCommand::RetouchPromotedVegetationInstance {
                request,
                instance_id,
                authored_transform,
            } => self.execute_promoted_vegetation_retouch(request, instance_id, authored_transform),
            _ => Err("Commande de promotion végétale attendue.".into()),
        }
    }

    fn execute_promoted_vegetation_retouch(
        &mut self,
        request: crate::area_preview::AreaPreviewRequest,
        instance_id: forge_core::EntityId,
        authored_transform: forge_project::VegetationInstanceTransform,
    ) -> Result<WorkspaceState, String> {
        self.require_revision(request.base_revision)?;
        self.next_revision()?;
        let document = self.document.as_ref().ok_or("Aucun projet ouvert.")?;
        let candidate = forge_world::preview_retouch_promoted_vegetation_instance(
            document,
            forge_world::VegetationRetouchCommand {
                command_id: CommandId::deterministic(workspace_store::unique_key()?),
                base_revision: Revision::new(request.base_revision),
                project_id: request.key.project_id,
                module_id: request.key.module_id,
                area_id: request.key.area_id,
                instance_id,
                authored_transform,
            },
        )
        .map_err(|error| error.to_string())?;
        let receipt = serde_json::to_string(&candidate).map_err(|error| error.to_string())?;
        self.commit_edit(candidate.edit, receipt);
        self.state(format!(
            "Instance végétale {instance_id} retouchée. Annulation disponible."
        ))
    }

    fn execute_path_create(
        &mut self,
        request: crate::area_preview::AreaPreviewRequest,
        requested_name: &str,
        trace_xz_m: Vec<[f32; 2]>,
    ) -> Result<WorkspaceState, String> {
        self.require_revision(request.base_revision)?;
        self.next_revision()?;
        let (path_id, name) = {
            let area = self.area_for_request(request)?;
            first_free_path_identity_and_name(area, requested_name)?
        };
        let document = self.document.as_ref().ok_or("Aucun projet ouvert.")?;
        let candidate = forge_world::preview_create_path(
            document,
            forge_world::PathCreateCommand {
                command_id: CommandId::deterministic(workspace_store::unique_key()?),
                base_revision: Revision::new(request.base_revision),
                project_id: request.key.project_id,
                module_id: request.key.module_id,
                area_id: request.key.area_id,
                path_id,
                name: name.clone(),
                trace_xz_m,
            },
        )
        .map_err(|error| error.to_string())?;
        let receipt = serde_json::to_string(&candidate).map_err(|error| error.to_string())?;
        self.commit_edit(candidate.edit, receipt);
        self.state(format!("Chemin « {name} » créé. Annulation disponible."))
    }

    fn execute_path_edit(&mut self, command: WorkspaceCommand) -> Result<WorkspaceState, String> {
        match command {
            WorkspaceCommand::CreatePath {
                request,
                name,
                trace_xz_m,
            } => self.execute_path_create(request, &name, trace_xz_m),
            WorkspaceCommand::RedrawPath {
                request,
                path_id,
                trace_xz_m,
            } => self.execute_path_redraw(request, path_id, trace_xz_m),
            WorkspaceCommand::UpsertRoad { request, road } => {
                self.execute_road_upsert(request, road)
            }
            WorkspaceCommand::UpsertRiver { request, river } => {
                self.execute_river_upsert(request, river)
            }
            WorkspaceCommand::UpsertWall { request, wall } => {
                self.execute_wall_upsert(request, wall)
            }
            _ => Err("Commande de chemin, de route, de rivière ou de mur attendue.".into()),
        }
    }

    fn execute_road_upsert(
        &mut self,
        request: crate::area_preview::AreaPreviewRequest,
        road: AuthoredRoad,
    ) -> Result<WorkspaceState, String> {
        self.require_revision(request.base_revision)?;
        self.next_revision()?;
        let document = self.document.as_ref().ok_or("Aucun projet ouvert.")?;
        let candidate = forge_world::preview_upsert_road(
            document,
            forge_world::RoadUpsertCommand {
                command_id: CommandId::deterministic(workspace_store::unique_key()?),
                base_revision: Revision::new(request.base_revision),
                project_id: request.key.project_id,
                module_id: request.key.module_id,
                area_id: request.key.area_id,
                road,
            },
        )
        .map_err(|error| error.to_string())?;
        let receipt = serde_json::to_string(&candidate).map_err(|error| error.to_string())?;
        self.commit_edit(candidate.edit, receipt);
        self.state("Profil de route appliqué. Annulation disponible.".into())
    }

    fn execute_river_upsert(
        &mut self,
        request: crate::area_preview::AreaPreviewRequest,
        river: AuthoredRiver,
    ) -> Result<WorkspaceState, String> {
        self.require_revision(request.base_revision)?;
        self.next_revision()?;
        let document = self.document.as_ref().ok_or("Aucun projet ouvert.")?;
        let candidate = forge_world::preview_upsert_river(
            document,
            forge_world::RiverUpsertCommand {
                command_id: CommandId::deterministic(workspace_store::unique_key()?),
                base_revision: Revision::new(request.base_revision),
                project_id: request.key.project_id,
                module_id: request.key.module_id,
                area_id: request.key.area_id,
                river,
            },
        )
        .map_err(|error| error.to_string())?;
        let receipt = serde_json::to_string(&candidate).map_err(|error| error.to_string())?;
        self.commit_edit(candidate.edit, receipt);
        self.state("Profil de rivière appliqué. Annulation disponible.".into())
    }

    fn execute_wall_upsert(
        &mut self,
        request: crate::area_preview::AreaPreviewRequest,
        wall: AuthoredWall,
    ) -> Result<WorkspaceState, String> {
        self.require_revision(request.base_revision)?;
        self.next_revision()?;
        let document = self.document.as_ref().ok_or("Aucun projet ouvert.")?;
        let candidate = forge_world::preview_upsert_wall(
            document,
            forge_world::WallUpsertCommand {
                command_id: CommandId::deterministic(workspace_store::unique_key()?),
                base_revision: Revision::new(request.base_revision),
                project_id: request.key.project_id,
                module_id: request.key.module_id,
                area_id: request.key.area_id,
                wall,
            },
        )
        .map_err(|error| error.to_string())?;
        let receipt = serde_json::to_string(&candidate).map_err(|error| error.to_string())?;
        self.commit_edit(candidate.edit, receipt);
        self.state("Mur procédural appliqué. Annulation disponible.".into())
    }

    fn execute_path_redraw(
        &mut self,
        request: crate::area_preview::AreaPreviewRequest,
        path_id: PathId,
        trace_xz_m: Vec<[f32; 2]>,
    ) -> Result<WorkspaceState, String> {
        self.require_revision(request.base_revision)?;
        self.next_revision()?;
        let document = self.document.as_ref().ok_or("Aucun projet ouvert.")?;
        let candidate = forge_world::preview_redraw_path(
            document,
            forge_world::PathRedrawCommand {
                command_id: CommandId::deterministic(workspace_store::unique_key()?),
                base_revision: Revision::new(request.base_revision),
                project_id: request.key.project_id,
                module_id: request.key.module_id,
                area_id: request.key.area_id,
                path_id,
                trace_xz_m,
            },
        )
        .map_err(|error| error.to_string())?;
        let name = candidate.path.name.clone();
        let receipt = serde_json::to_string(&candidate).map_err(|error| error.to_string())?;
        self.commit_edit(candidate.edit, receipt);
        self.state(format!(
            "Chemin « {name} » redessiné. Annulation disponible."
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

    fn execute_vegetation_edit(&mut self, command: WorkspaceCommand) -> Result<String, String> {
        match command {
            WorkspaceCommand::AddVegetationSpecies {
                request,
                preset,
                display_name,
            } => self.add_vegetation_species(request, preset, display_name),
            WorkspaceCommand::RenameVegetationSpecies {
                request,
                layer_id,
                display_name,
            } => self.rename_vegetation_species(request, &layer_id, display_name),
            WorkspaceCommand::RemoveVegetationSpecies { request, layer_id } => {
                self.remove_vegetation_species(request, &layer_id)
            }
            WorkspaceCommand::AddVegetationExclusion { request, draft } => {
                self.add_vegetation_exclusion(request, draft)
            }
            WorkspaceCommand::RemoveVegetationExclusion {
                request,
                exclusion_id,
            } => self.remove_vegetation_exclusion(request, &exclusion_id),
            _ => Err("Commande d’édition végétale attendue.".into()),
        }
    }

    fn add_vegetation_species(
        &mut self,
        request: crate::area_preview::AreaPreviewRequest,
        preset: forge_project::VegetationSpeciesPreset,
        display_name: String,
    ) -> Result<String, String> {
        let area = self.area_for_request(request)?;
        if area.vegetation.layers.len() >= MAX_VEGETATION_LAYERS {
            return Err("Le catalogue végétal a atteint sa limite de 16 espèces.".into());
        }
        let layer_id = first_free_species_id(area)?;
        let grass_material_layer_id = area.terrain.material_layers
            [TerrainMaterialRole::Grass.index()]
        .id
        .clone();
        let expected_vegetation_hash = area
            .vegetation
            .content_hash()
            .map_err(|error| error.to_string())?;
        let expected_density_hash = area
            .vegetation_density
            .content_hash()
            .map_err(|error| error.to_string())?;
        let mut layers = area.vegetation.layers.clone();
        layers.push(preset.build_layer(
            layer_id.to_string(),
            display_name,
            grass_material_layer_id,
        ));
        self.replace_vegetation_catalog(
            request,
            expected_vegetation_hash,
            expected_density_hash,
            layers,
        )?;
        Ok(format!(
            "Espèce ajoutée au catalogue sous l’identifiant {layer_id}."
        ))
    }

    fn rename_vegetation_species(
        &mut self,
        request: crate::area_preview::AreaPreviewRequest,
        layer_id: &VegetationLayerId,
        display_name: String,
    ) -> Result<String, String> {
        let area = self.area_for_request(request)?;
        let expected_vegetation_hash = area
            .vegetation
            .content_hash()
            .map_err(|error| error.to_string())?;
        let expected_density_hash = area
            .vegetation_density
            .content_hash()
            .map_err(|error| error.to_string())?;
        let mut layers = area.vegetation.layers.clone();
        let layer = layers
            .iter_mut()
            .find(|layer| layer.id == *layer_id)
            .ok_or("Espèce végétale introuvable dans la zone.")?;
        layer.display_name = display_name;
        self.replace_vegetation_catalog(
            request,
            expected_vegetation_hash,
            expected_density_hash,
            layers,
        )?;
        Ok("Espèce renommée dans le catalogue. Annulation disponible.".into())
    }

    fn remove_vegetation_species(
        &mut self,
        request: crate::area_preview::AreaPreviewRequest,
        layer_id: &VegetationLayerId,
    ) -> Result<String, String> {
        let area = self.area_for_request(request)?;
        let expected_vegetation_hash = area
            .vegetation
            .content_hash()
            .map_err(|error| error.to_string())?;
        let expected_density_hash = area
            .vegetation_density
            .content_hash()
            .map_err(|error| error.to_string())?;
        let mut layers = area.vegetation.layers.clone();
        let before = layers.len();
        layers.retain(|layer| layer.id != *layer_id);
        if layers.len() == before {
            return Err("Espèce végétale introuvable dans la zone.".into());
        }
        self.replace_vegetation_catalog(
            request,
            expected_vegetation_hash,
            expected_density_hash,
            layers,
        )?;
        Ok(
            "Espèce retirée du catalogue et de son masque de densité. Annulation disponible."
                .into(),
        )
    }

    fn replace_vegetation_catalog(
        &mut self,
        request: crate::area_preview::AreaPreviewRequest,
        expected_vegetation_hash: ContentHash,
        expected_density_hash: ContentHash,
        layers: Vec<VegetationLayer>,
    ) -> Result<(), String> {
        self.apply_change(
            request.base_revision,
            WorkspaceChange::SetVegetationCatalog {
                module_id: request.key.module_id,
                area_id: request.key.area_id,
                expected_vegetation_hash,
                expected_density_hash,
                layers,
            },
        )
    }

    fn add_vegetation_exclusion(
        &mut self,
        request: crate::area_preview::AreaPreviewRequest,
        draft: VegetationExclusionDraft,
    ) -> Result<String, String> {
        let area = self.area_for_request(request)?;
        if area.vegetation.exclusions.len() >= MAX_VEGETATION_EXCLUSIONS {
            return Err("La zone a atteint sa limite de 32 exclusions végétales.".into());
        }
        let exclusion_id = first_free_exclusion_id(&area.vegetation.exclusions)?;
        let expected_vegetation_hash = area
            .vegetation
            .content_hash()
            .map_err(|error| error.to_string())?;
        let mut exclusions = area.vegetation.exclusions.clone();
        exclusions.push(exclusion_from_draft(exclusion_id.clone(), draft));
        self.apply_change(
            request.base_revision,
            WorkspaceChange::SetVegetationExclusions {
                module_id: request.key.module_id,
                area_id: request.key.area_id,
                expected_vegetation_hash,
                exclusions,
            },
        )?;
        Ok(format!(
            "Exclusion végétale {exclusion_id} ajoutée. Annulation disponible."
        ))
    }

    fn remove_vegetation_exclusion(
        &mut self,
        request: crate::area_preview::AreaPreviewRequest,
        exclusion_id: &VegetationExclusionId,
    ) -> Result<String, String> {
        let area = self.area_for_request(request)?;
        let expected_vegetation_hash = area
            .vegetation
            .content_hash()
            .map_err(|error| error.to_string())?;
        let mut exclusions = area.vegetation.exclusions.clone();
        let before = exclusions.len();
        exclusions.retain(|exclusion| exclusion.id() != exclusion_id);
        if exclusions.len() == before {
            return Err("Exclusion végétale introuvable dans la zone.".into());
        }
        self.apply_change(
            request.base_revision,
            WorkspaceChange::SetVegetationExclusions {
                module_id: request.key.module_id,
                area_id: request.key.area_id,
                expected_vegetation_hash,
                exclusions,
            },
        )?;
        Ok("Exclusion végétale supprimée. Annulation disponible.".into())
    }

    fn area_for_request(
        &self,
        request: crate::area_preview::AreaPreviewRequest,
    ) -> Result<&WorkspaceArea, String> {
        self.require_revision(request.base_revision)?;
        let document = self.document.as_ref().ok_or("Aucun projet ouvert.")?;
        if !request.key.exists(document) {
            return Err("La zone demandée n’appartient pas au projet courant.".into());
        }
        document
            .area(request.key.module_id, request.key.area_id)
            .ok_or_else(|| "Zone introuvable.".into())
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

fn changed_density_preview_source(
    preview: &VegetationDensityStrokePreviewRequest,
    candidate: &forge_world::VegetationDensityBrushResult,
) -> Result<VegetationDensityStrokePreviewSource, String> {
    let candidate_revision = candidate.edit.document.revision.get();
    ensure_ipc_revision(candidate_revision)?;
    let area = candidate
        .edit
        .document
        .area(preview.request.key.module_id, preview.request.key.area_id)
        .ok_or("Zone candidate introuvable.")?
        .clone();
    let candidate_density_source_hash = area
        .vegetation_density
        .content_hash()
        .map_err(|error| error.to_string())?;
    Ok(VegetationDensityStrokePreviewSource {
        area,
        changed_cells: candidate.changed_cells,
        no_op: false,
        candidate_revision,
        candidate_density_source_hash,
    })
}

fn no_op_density_preview_source(
    preview: &VegetationDensityStrokePreviewRequest,
    document: &WorkspaceDocument,
) -> Result<VegetationDensityStrokePreviewSource, String> {
    let area = document
        .area(preview.request.key.module_id, preview.request.key.area_id)
        .ok_or("Zone introuvable.")?
        .clone();
    let candidate_density_source_hash = area
        .vegetation_density
        .content_hash()
        .map_err(|error| error.to_string())?;
    Ok(VegetationDensityStrokePreviewSource {
        area,
        changed_cells: 0,
        no_op: true,
        candidate_revision: preview.request.base_revision,
        candidate_density_source_hash,
    })
}

fn first_free_species_id(area: &WorkspaceArea) -> Result<VegetationLayerId, String> {
    (1..=MAX_VEGETATION_LAYERS)
        .map(|suffix| VegetationLayerId::new(format!("species_{suffix:02}")))
        .find(|candidate| {
            area.vegetation
                .layers
                .iter()
                .all(|layer| &layer.id != candidate)
                && area
                    .vegetation
                    .promoted_instances
                    .iter()
                    .all(|promoted| &promoted.source.layer_id != candidate)
        })
        .ok_or_else(|| {
            "Aucun identifiant d’espèce libre ou non réservé entre species_01 et species_16.".into()
        })
}

fn first_free_exclusion_id(
    exclusions: &[VegetationExclusion],
) -> Result<VegetationExclusionId, String> {
    (1..=MAX_VEGETATION_EXCLUSIONS)
        .map(|suffix| VegetationExclusionId::new(format!("exclusion_{suffix:02}")))
        .find(|candidate| {
            exclusions
                .iter()
                .all(|exclusion| exclusion.id() != candidate)
        })
        .ok_or_else(|| {
            "Aucun identifiant d’exclusion libre entre exclusion_01 et exclusion_32.".into()
        })
}

fn exclusion_from_draft(
    id: VegetationExclusionId,
    draft: VegetationExclusionDraft,
) -> VegetationExclusion {
    match draft {
        VegetationExclusionDraft::Circle {
            center_x_m,
            center_z_m,
            radius_m,
        } => VegetationExclusion::Circle {
            id,
            center_x_m,
            center_z_m,
            radius_m,
        },
        VegetationExclusionDraft::CorridorSegment {
            start_x_m,
            start_z_m,
            end_x_m,
            end_z_m,
            half_width_m,
        } => VegetationExclusion::CorridorSegment {
            id,
            start_x_m,
            start_z_m,
            end_x_m,
            end_z_m,
            half_width_m,
        },
    }
}

fn first_free_path_identity_and_name(
    area: &WorkspaceArea,
    requested_name: &str,
) -> Result<(PathId, String), String> {
    if area.paths.paths.len() >= MAX_PATHS_PER_DOCUMENT {
        return Err(format!(
            "La zone contient déjà la limite de {MAX_PATHS_PER_DOCUMENT} chemins."
        ));
    }
    (1..=MAX_PATHS_PER_DOCUMENT)
        .find_map(|index| {
            let id = PathId::deterministic(format!(
                "studio/{}/{}/path-{index:02}",
                area.paths.project_id, area.id
            ));
            (!area.paths.paths.iter().any(|path| path.id == id)).then(|| {
                let name = if requested_name.is_empty() {
                    format!("Chemin {index}")
                } else {
                    requested_name.to_owned()
                };
                (id, name)
            })
        })
        .filter(|(id, _)| !id.as_entity_id().as_uuid().is_nil())
        .ok_or_else(|| "Aucun identifiant de chemin déterministe libre.".into())
}

fn stamp_revision(document: &mut WorkspaceDocument, next: u64) {
    document.revision = Revision::new(next);
    for module in &mut document.project.modules {
        for area in &mut module.areas {
            area.terrain.revision = document.revision;
            area.vegetation.revision = document.revision;
            area.vegetation_density.revision = document.revision;
            area.paths.revision = document.revision;
            area.roads.revision = document.revision;
            area.rivers.revision = document.revision;
            area.walls.revision = document.revision;
            area.placements.revision = document.revision;
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
