//! Pure, revision-bound placement of governed assets on the exact effective terrain surface.

use crate::{
    RiverGenerationError, RiverGenerationPlan, TERRAIN_HALF_EXTENT_M, TerrainGenerationError,
    TerrainGenerationPlan,
};
use forge_core::{
    AreaId, CommandId, ContentHash, EntityId, ModuleId, ProjectId, Revision, Transform,
    UnitQuaternion, Vec3,
};
use forge_project::{
    AssetPin, AuthoredPlacement, PlacementDocumentError, WorkspaceArea, WorkspaceChange,
    WorkspaceDocument, WorkspaceEdit, WorkspaceEditResult, WorkspaceError,
};
use serde::{Deserialize, Serialize};
use thiserror::Error;

/// Client-independent placement intent resolved against exact authored source hashes.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PlaceAssetCommand {
    pub command_id: CommandId,
    pub base_revision: Revision,
    pub project_id: ProjectId,
    pub module_id: ModuleId,
    pub area_id: AreaId,
    pub expected_terrain_document_hash: ContentHash,
    pub expected_path_document_hash: ContentHash,
    pub expected_road_document_hash: ContentHash,
    pub expected_river_document_hash: ContentHash,
    pub expected_placement_document_hash: ContentHash,
    pub asset_pin: AssetPin,
    pub drop_x_m: f32,
    pub drop_z_m: f32,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PlaceAssetResult {
    pub command: PlaceAssetCommand,
    pub placement: AuthoredPlacement,
    pub edit: WorkspaceEditResult,
}

/// Resolves a drop to one persistent transform without mutating the workspace or performing I/O.
pub fn preview_place_asset(
    workspace: &WorkspaceDocument,
    command: &PlaceAssetCommand,
) -> Result<PlaceAssetResult, PlaceAssetError> {
    let area = validate_command_context(workspace, command)?;
    validate_drop(command.drop_x_m, command.drop_z_m)?;
    command.asset_pin.validate()?;

    let hashes = PlacementSourceHashes::from_area(area)?;
    if hashes.terrain != command.expected_terrain_document_hash
        || hashes.paths != command.expected_path_document_hash
        || hashes.roads != command.expected_road_document_hash
        || hashes.rivers != command.expected_river_document_hash
        || hashes.placements != command.expected_placement_document_hash
    {
        return Err(WorkspaceError::StaleRevision.into());
    }

    let river_projection =
        RiverGenerationPlan::from_documents(&area.terrain, &area.paths, &area.roads, &area.rivers)?
            .generate()?;
    let effective_terrain =
        TerrainGenerationPlan::from_document(&river_projection.effective_terrain)?;
    let surface = effective_terrain.surface_at(command.drop_x_m, command.drop_z_m)?;
    let placement = AuthoredPlacement {
        id: placement_entity_id(command.command_id),
        asset_id: command.asset_pin.asset_id,
        transform: Transform {
            translation_m: Vec3::new(command.drop_x_m, surface.height_m, command.drop_z_m),
            rotation: UnitQuaternion::IDENTITY,
            scale: Vec3::new(
                command.asset_pin.base_scale,
                command.asset_pin.base_scale,
                command.asset_pin.base_scale,
            ),
        },
    };
    placement.validate()?;

    let edit = workspace.preview_edit(WorkspaceEdit {
        command_id: command.command_id,
        base_revision: command.base_revision,
        change: WorkspaceChange::PlaceAsset {
            module_id: command.module_id,
            area_id: command.area_id,
            expected_terrain_document_hash: hashes.terrain,
            expected_path_document_hash: hashes.paths,
            expected_road_document_hash: hashes.roads,
            expected_river_document_hash: hashes.rivers,
            expected_placement_document_hash: hashes.placements,
            asset_pin: Box::new(command.asset_pin.clone()),
            placement,
        },
    })?;

    Ok(PlaceAssetResult {
        command: command.clone(),
        placement,
        edit,
    })
}

#[must_use]
pub fn placement_entity_id(command_id: CommandId) -> EntityId {
    EntityId::deterministic(format!("asset-placement/{command_id}").as_bytes())
}

#[derive(Clone, Copy)]
struct PlacementSourceHashes {
    terrain: ContentHash,
    paths: ContentHash,
    roads: ContentHash,
    rivers: ContentHash,
    placements: ContentHash,
}

impl PlacementSourceHashes {
    fn from_area(area: &WorkspaceArea) -> Result<Self, WorkspaceError> {
        Ok(Self {
            terrain: area.terrain.content_hash()?,
            paths: area.paths.content_hash()?,
            roads: area.roads.content_hash()?,
            rivers: area.rivers.content_hash()?,
            placements: area.placements.content_hash()?,
        })
    }
}

fn validate_command_context<'a>(
    workspace: &'a WorkspaceDocument,
    command: &PlaceAssetCommand,
) -> Result<&'a WorkspaceArea, PlaceAssetError> {
    workspace.validate()?;
    if command.command_id.as_uuid().is_nil() {
        return Err(WorkspaceError::NilIdentity("command_id").into());
    }
    if workspace.project.id != command.project_id {
        return Err(PlaceAssetError::WrongProject);
    }
    if workspace.revision != command.base_revision {
        return Err(WorkspaceError::StaleRevision.into());
    }
    workspace
        .area(command.module_id, command.area_id)
        .ok_or_else(|| WorkspaceError::MissingTarget("area").into())
}

fn validate_drop(x_m: f32, z_m: f32) -> Result<(), PlaceAssetError> {
    if !x_m.is_finite()
        || !z_m.is_finite()
        || x_m < -TERRAIN_HALF_EXTENT_M
        || x_m >= TERRAIN_HALF_EXTENT_M
        || z_m < -TERRAIN_HALF_EXTENT_M
        || z_m >= TERRAIN_HALF_EXTENT_M
    {
        return Err(PlaceAssetError::InvalidDrop {
            x_m,
            z_m,
            half_extent_m: TERRAIN_HALF_EXTENT_M,
        });
    }
    Ok(())
}

#[derive(Debug, Error)]
pub enum PlaceAssetError {
    #[error("asset placement command belongs to another project")]
    WrongProject,
    #[error(
        "asset drop ({x_m}, {z_m}) must be finite and inside [-{half_extent_m}, {half_extent_m}) metres"
    )]
    InvalidDrop {
        x_m: f32,
        z_m: f32,
        half_extent_m: f32,
    },
    #[error("asset pin or placement is invalid: {0}")]
    Placement(#[from] PlacementDocumentError),
    #[error("asset placement workspace edit failed: {0}")]
    Workspace(#[from] WorkspaceError),
    #[error("asset placement effective river terrain failed: {0}")]
    River(#[from] RiverGenerationError),
    #[error("asset placement terrain sample failed: {0}")]
    Terrain(#[from] TerrainGenerationError),
}

#[cfg(test)]
#[path = "placement_tests.rs"]
mod tests;
