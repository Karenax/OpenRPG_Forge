//! Pure, revision-bound authoring commands; the result is also the dry-run projection.
#[cfg(test)]
#[path = "workspace_edit_tests.rs"]
mod tests;
use crate::{
    TerrainHeightDelta, TerrainMaterialWeightOverride, WorkspaceDocument, WorkspaceError,
    WorkspaceModule,
};
use forge_core::{AreaId, CommandId, ContentHash, ModuleId, Revision, Vec3};
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorkspaceEdit {
    pub command_id: CommandId,
    pub base_revision: Revision,
    pub change: WorkspaceChange,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "operation", rename_all = "snake_case", deny_unknown_fields)]
pub enum WorkspaceChange {
    /// Resolved source patch. Brush calculation belongs to `forge_world`, not the renderer.
    SetTerrainHeightDeltas {
        module_id: ModuleId,
        area_id: AreaId,
        expected_terrain_hash: ContentHash,
        height_deltas: Vec<TerrainHeightDelta>,
    },
    /// Resolved sparse material blend patch in grass, earth, rock order.
    SetTerrainMaterialWeights {
        module_id: ModuleId,
        area_id: AreaId,
        expected_terrain_hash: ContentHash,
        material_weight_overrides: Vec<TerrainMaterialWeightOverride>,
    },
    AddModule {
        module_id: ModuleId,
        name: String,
    },
    AddExteriorArea {
        module_id: ModuleId,
        area_id: AreaId,
        name: String,
        seed: u64,
        spawn: Vec3,
    },
    RenameProject {
        name: String,
    },
    RenameModule {
        module_id: ModuleId,
        name: String,
    },
    UpdateAreaMetadata {
        module_id: ModuleId,
        area_id: AreaId,
        name: String,
        spawn: Vec3,
    },
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorkspaceEditReceipt {
    pub command: WorkspaceEdit,
    pub new_revision: Revision,
    pub before_hash: ContentHash,
    pub after_hash: ContentHash,
    /// In-memory inverse only. Consumers must guard it with `after_hash`; it never deletes files.
    pub inverse_snapshot: Box<WorkspaceDocument>,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorkspaceEditResult {
    pub document: WorkspaceDocument,
    pub receipt: WorkspaceEditReceipt,
}

impl WorkspaceDocument {
    /// Produces a validated candidate without mutating self or performing platform I/O.
    pub fn preview_edit(
        &self,
        command: WorkspaceEdit,
    ) -> Result<WorkspaceEditResult, WorkspaceError> {
        let before_hash = self.content_hash()?;
        if command.command_id.as_uuid().is_nil() {
            return Err(WorkspaceError::NilIdentity("command_id"));
        }
        if command.base_revision != self.revision {
            return Err(WorkspaceError::StaleRevision);
        }
        let next = self
            .revision
            .next()
            .map_err(|_| WorkspaceError::RevisionExhausted)?;
        let mut document = self.clone();
        document.apply_change(&command.change, next)?;
        if document == *self {
            return Err(WorkspaceError::NoChange);
        }
        document.revision = next;
        let after_hash = document.content_hash()?;
        Ok(WorkspaceEditResult {
            document,
            receipt: WorkspaceEditReceipt {
                command,
                new_revision: next,
                before_hash,
                after_hash,
                inverse_snapshot: Box::new(self.clone()),
            },
        })
    }

    fn apply_change(
        &mut self,
        change: &WorkspaceChange,
        next: Revision,
    ) -> Result<(), WorkspaceError> {
        let document = self;
        match change {
            WorkspaceChange::SetTerrainHeightDeltas {
                module_id,
                area_id,
                expected_terrain_hash,
                height_deltas,
            } => set_terrain_heights(
                document,
                *module_id,
                *area_id,
                *expected_terrain_hash,
                height_deltas,
                next,
            )?,
            WorkspaceChange::SetTerrainMaterialWeights {
                module_id,
                area_id,
                expected_terrain_hash,
                material_weight_overrides,
            } => set_terrain_material_weights(
                document,
                *module_id,
                *area_id,
                *expected_terrain_hash,
                material_weight_overrides,
                next,
            )?,
            WorkspaceChange::AddModule { module_id, name } => {
                document.project.modules.push(WorkspaceModule {
                    id: *module_id,
                    name: name.clone(),
                    areas: vec![],
                });
            }
            WorkspaceChange::AddExteriorArea {
                module_id,
                area_id,
                name,
                seed,
                spawn,
            } => {
                let area = super::workspace::new_exterior_area(
                    document.project.id,
                    *area_id,
                    next,
                    name.clone(),
                    *seed,
                    *spawn,
                );
                document
                    .project
                    .modules
                    .iter_mut()
                    .find(|m| m.id == *module_id)
                    .ok_or(WorkspaceError::MissingTarget("module"))?
                    .areas
                    .push(area);
            }
            WorkspaceChange::RenameProject { name } => document.project.name.clone_from(name),
            WorkspaceChange::RenameModule { module_id, name } => {
                document
                    .project
                    .modules
                    .iter_mut()
                    .find(|m| m.id == *module_id)
                    .ok_or(WorkspaceError::MissingTarget("module"))?
                    .name
                    .clone_from(name);
            }
            WorkspaceChange::UpdateAreaMetadata {
                module_id,
                area_id,
                name,
                spawn,
            } => {
                let area = document
                    .project
                    .modules
                    .iter_mut()
                    .find(|m| m.id == *module_id)
                    .ok_or(WorkspaceError::MissingTarget("module"))?
                    .areas
                    .iter_mut()
                    .find(|a| a.id == *area_id)
                    .ok_or(WorkspaceError::MissingTarget("area"))?;
                if area.name == *name && area.spawn == *spawn {
                    return Err(WorkspaceError::NoChange);
                }
                area.name.clone_from(name);
                area.spawn = *spawn;
                area.terrain.name.clone_from(name);
                area.terrain.revision = next;
            }
        }
        Ok(())
    }
}

fn set_terrain_material_weights(
    document: &mut WorkspaceDocument,
    module_id: ModuleId,
    area_id: AreaId,
    expected_hash: ContentHash,
    weights: &[TerrainMaterialWeightOverride],
    revision: Revision,
) -> Result<(), WorkspaceError> {
    let area = document
        .project
        .modules
        .iter_mut()
        .find(|module| module.id == module_id)
        .ok_or(WorkspaceError::MissingTarget("module"))?
        .areas
        .iter_mut()
        .find(|area| area.id == area_id)
        .ok_or(WorkspaceError::MissingTarget("area"))?;
    if area.terrain.content_hash()? != expected_hash {
        return Err(WorkspaceError::StaleRevision);
    }
    if area.terrain.material_weight_overrides == weights {
        return Err(WorkspaceError::NoChange);
    }
    area.terrain.material_weight_overrides = weights.to_vec();
    area.terrain.revision = revision;
    Ok(())
}

fn set_terrain_heights(
    document: &mut WorkspaceDocument,
    module_id: ModuleId,
    area_id: AreaId,
    expected_hash: ContentHash,
    deltas: &[TerrainHeightDelta],
    revision: Revision,
) -> Result<(), WorkspaceError> {
    let area = document
        .project
        .modules
        .iter_mut()
        .find(|module| module.id == module_id)
        .ok_or(WorkspaceError::MissingTarget("module"))?
        .areas
        .iter_mut()
        .find(|area| area.id == area_id)
        .ok_or(WorkspaceError::MissingTarget("area"))?;
    if area.terrain.content_hash()? != expected_hash {
        return Err(WorkspaceError::StaleRevision);
    }
    if area.terrain.height_deltas == deltas {
        return Err(WorkspaceError::NoChange);
    }
    area.terrain.height_deltas = deltas.to_vec();
    area.terrain.revision = revision;
    Ok(())
}
