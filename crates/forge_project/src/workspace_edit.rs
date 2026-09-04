//! Pure, revision-bound authoring commands; the result is also the dry-run projection.
#[cfg(test)]
#[path = "workspace_edit_tests.rs"]
mod tests;
use crate::{
    AuthoredPath, PromotedVegetationInstance, TerrainHeightDelta, TerrainMaterialWeightOverride,
    VegetationDensityLayer, VegetationDensityOverride, VegetationExclusion, VegetationLayer,
    VegetationLayerId, WorkspaceDocument, WorkspaceError, WorkspaceModule,
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
    /// Resolved sparse density patch for one vegetation recipe layer.
    SetVegetationDensityOverrides {
        module_id: ModuleId,
        area_id: AreaId,
        layer_id: VegetationLayerId,
        expected_density_hash: ContentHash,
        density_overrides: Vec<VegetationDensityOverride>,
    },
    /// Resolved replacement of the procedural species catalogue. Density sources are retained by
    /// stable layer ID and synchronised atomically when catalogue membership or order changes.
    SetVegetationCatalog {
        module_id: ModuleId,
        area_id: AreaId,
        expected_vegetation_hash: ContentHash,
        expected_density_hash: ContentHash,
        layers: Vec<VegetationLayer>,
    },
    /// Resolved replacement of the bounded, ordered vegetation keep-clear shapes.
    SetVegetationExclusions {
        module_id: ModuleId,
        area_id: AreaId,
        expected_vegetation_hash: ContentHash,
        exclusions: Vec<VegetationExclusion>,
    },
    /// Resolved replacement of persistent promoted instances. Existing provenance is immutable.
    SetVegetationPromotedInstances {
        module_id: ModuleId,
        area_id: AreaId,
        expected_vegetation_hash: ContentHash,
        promoted_instances: Vec<PromotedVegetationInstance>,
    },
    /// Creates or replaces one complete generic authored path without implicitly deleting others.
    UpsertPath {
        module_id: ModuleId,
        area_id: AreaId,
        expected_path_document_hash: ContentHash,
        path: AuthoredPath,
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

    #[allow(
        clippy::too_many_lines,
        reason = "the exhaustive dispatcher keeps every typed workspace mutation visible"
    )]
    fn apply_change(
        &mut self,
        change: &WorkspaceChange,
        next: Revision,
    ) -> Result<(), WorkspaceError> {
        let document = self;
        let base_revision = document.revision;
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
            WorkspaceChange::SetVegetationDensityOverrides {
                module_id,
                area_id,
                layer_id,
                expected_density_hash,
                density_overrides,
            } => set_vegetation_density_overrides(
                document,
                *module_id,
                *area_id,
                layer_id,
                *expected_density_hash,
                density_overrides,
                next,
            )?,
            WorkspaceChange::SetVegetationCatalog {
                module_id,
                area_id,
                expected_vegetation_hash,
                expected_density_hash,
                layers,
            } => set_vegetation_catalog(
                document,
                *module_id,
                *area_id,
                *expected_vegetation_hash,
                *expected_density_hash,
                layers,
                next,
            )?,
            WorkspaceChange::SetVegetationExclusions {
                module_id,
                area_id,
                expected_vegetation_hash,
                exclusions,
            } => set_vegetation_exclusions(
                document,
                *module_id,
                *area_id,
                *expected_vegetation_hash,
                exclusions,
                next,
            )?,
            WorkspaceChange::SetVegetationPromotedInstances {
                module_id,
                area_id,
                expected_vegetation_hash,
                promoted_instances,
            } => set_vegetation_promoted_instances(
                document,
                *module_id,
                *area_id,
                *expected_vegetation_hash,
                promoted_instances,
                base_revision,
                next,
            )?,
            WorkspaceChange::UpsertPath {
                module_id,
                area_id,
                expected_path_document_hash,
                path,
            } => upsert_path(
                document,
                *module_id,
                *area_id,
                *expected_path_document_hash,
                path,
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
            } => update_area_metadata(document, *module_id, *area_id, name, *spawn, next)?,
        }
        Ok(())
    }
}

fn update_area_metadata(
    document: &mut WorkspaceDocument,
    module_id: ModuleId,
    area_id: AreaId,
    name: &str,
    spawn: Vec3,
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
    if area.name == name && area.spawn == spawn {
        return Err(WorkspaceError::NoChange);
    }
    name.clone_into(&mut area.name);
    area.spawn = spawn;
    name.clone_into(&mut area.terrain.name);
    area.terrain.revision = revision;
    name.clone_into(&mut area.vegetation.name);
    area.vegetation.revision = revision;
    name.clone_into(&mut area.vegetation_density.name);
    area.vegetation_density.revision = revision;
    name.clone_into(&mut area.paths.name);
    area.paths.revision = revision;
    Ok(())
}

fn upsert_path(
    document: &mut WorkspaceDocument,
    module_id: ModuleId,
    area_id: AreaId,
    expected_hash: ContentHash,
    path: &AuthoredPath,
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
    if area.paths.content_hash()? != expected_hash {
        return Err(WorkspaceError::StaleRevision);
    }

    let mut replacement = area.paths.clone();
    match replacement
        .paths
        .binary_search_by_key(&path.id, |candidate| candidate.id)
    {
        Ok(index) => {
            if replacement.paths[index] == *path {
                return Err(WorkspaceError::NoChange);
            }
            replacement.paths[index] = path.clone();
        }
        Err(index) => replacement.paths.insert(index, path.clone()),
    }
    replacement.revision = revision;
    replacement.validate()?;
    area.paths = replacement;
    Ok(())
}

fn set_vegetation_density_overrides(
    document: &mut WorkspaceDocument,
    module_id: ModuleId,
    area_id: AreaId,
    layer_id: &VegetationLayerId,
    expected_hash: ContentHash,
    overrides: &[VegetationDensityOverride],
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
    if area.vegetation_density.content_hash()? != expected_hash {
        return Err(WorkspaceError::StaleRevision);
    }
    let layer = area
        .vegetation_density
        .layers
        .iter_mut()
        .find(|layer| layer.layer_id == *layer_id)
        .ok_or(WorkspaceError::MissingTarget("vegetation layer"))?;
    if layer.overrides == overrides {
        return Err(WorkspaceError::NoChange);
    }
    layer.overrides = overrides.to_vec();
    area.vegetation_density.revision = revision;
    area.vegetation_density.validate()?;
    Ok(())
}

fn set_vegetation_exclusions(
    document: &mut WorkspaceDocument,
    module_id: ModuleId,
    area_id: AreaId,
    expected_hash: ContentHash,
    exclusions: &[VegetationExclusion],
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
    if area.vegetation.content_hash()? != expected_hash {
        return Err(WorkspaceError::StaleRevision);
    }
    if area.vegetation.exclusions == exclusions {
        return Err(WorkspaceError::NoChange);
    }
    area.vegetation.exclusions = exclusions.to_vec();
    area.vegetation.revision = revision;
    area.vegetation.validate()?;
    Ok(())
}

fn set_vegetation_promoted_instances(
    document: &mut WorkspaceDocument,
    module_id: ModuleId,
    area_id: AreaId,
    expected_hash: ContentHash,
    promoted_instances: &[PromotedVegetationInstance],
    base_revision: Revision,
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
    if area.vegetation.content_hash()? != expected_hash {
        return Err(WorkspaceError::StaleRevision);
    }
    if area.vegetation.promoted_instances == promoted_instances {
        return Err(WorkspaceError::NoChange);
    }
    if let Some(removed) = area.vegetation.promoted_instances.iter().find(|current| {
        promoted_instances
            .iter()
            .all(|replacement| replacement.id != current.id)
    }) {
        return Err(WorkspaceError::VegetationPromotionRemovalRequiresExplicitCommand(removed.id));
    }

    let terrain_hash = area.terrain.content_hash()?;
    let density_hash = area.vegetation_density.content_hash()?;
    for promoted in promoted_instances {
        if let Some(current) = area
            .vegetation
            .promoted_instances
            .iter()
            .find(|current| current.id == promoted.id)
        {
            if current.source != promoted.source {
                return Err(WorkspaceError::VegetationPromotionProvenanceChanged(
                    promoted.id,
                ));
            }
            continue;
        }

        let source = &promoted.source;
        let source_layer_matches = area
            .vegetation
            .layers
            .iter()
            .any(|layer| layer.id == source.layer_id && layer.kind == source.kind);
        let source_hashes_match = source.terrain_document_hash == terrain_hash
            && source.vegetation_document_hash == expected_hash
            && source.vegetation_density_document_hash == density_hash;
        if source.workspace_revision != base_revision
            || !source_hashes_match
            || !source_layer_matches
            || promoted.authored_transform != source.transform
        {
            return Err(WorkspaceError::VegetationPromotionSourceMismatch(
                promoted.id,
            ));
        }
    }

    let mut replacement = area.vegetation.clone();
    replacement.promoted_instances = promoted_instances.to_vec();
    replacement.revision = revision;
    replacement.validate()?;
    area.vegetation = replacement;
    Ok(())
}

fn set_vegetation_catalog(
    document: &mut WorkspaceDocument,
    module_id: ModuleId,
    area_id: AreaId,
    expected_vegetation_hash: ContentHash,
    expected_density_hash: ContentHash,
    layers: &[VegetationLayer],
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
    if area.vegetation.content_hash()? != expected_vegetation_hash
        || area.vegetation_density.content_hash()? != expected_density_hash
    {
        return Err(WorkspaceError::StaleRevision);
    }
    if area.vegetation.layers == layers {
        return Err(WorkspaceError::NoChange);
    }

    let density_membership_changed = area.vegetation.layers.len() != layers.len()
        || area
            .vegetation
            .layers
            .iter()
            .zip(layers)
            .any(|(current, replacement)| current.id != replacement.id);
    if density_membership_changed {
        let previous_density = std::mem::take(&mut area.vegetation_density.layers);
        area.vegetation_density.layers = layers
            .iter()
            .map(|layer| {
                previous_density
                    .iter()
                    .find(|density| density.layer_id == layer.id)
                    .cloned()
                    .unwrap_or_else(|| VegetationDensityLayer::new(layer.id.clone(), 0, vec![]))
            })
            .collect();
        area.vegetation_density.revision = revision;
    }
    area.vegetation.layers = layers.to_vec();
    area.vegetation.revision = revision;
    area.vegetation.validate()?;
    area.vegetation_density.validate()?;
    Ok(())
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
