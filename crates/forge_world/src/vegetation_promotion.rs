//! Pure promotion and retouch commands for generated vegetation instances.

use crate::{
    RiverGenerationError, RiverGenerationPlan, VegetationGenerationError, VegetationGenerationPlan,
    VegetationInstance,
};
use forge_core::{AreaId, CommandId, EntityId, ModuleId, ProjectId, Revision};
use forge_project::{
    PromotedVegetationInstance, VegetationGenerationAlgorithm, VegetationInstanceTransform,
    VegetationLayerId, VegetationPromotionSource, WorkspaceArea, WorkspaceChange,
    WorkspaceDocument, WorkspaceEdit, WorkspaceEditResult, WorkspaceError,
};
use serde::{Deserialize, Serialize};
use std::collections::BTreeSet;
use thiserror::Error;

pub const VEGETATION_PROMOTION_SCHEMA_VERSION: u32 = 1;
pub const MAX_VEGETATION_PROMOTION_RADIUS_M: f32 = 64.0;

/// Bounded, layer-specific intent used to resolve one generated placement.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct VegetationPromotionTarget {
    pub schema_version: u32,
    pub center_x_m: f32,
    pub center_z_m: f32,
    pub maximum_distance_m: f32,
    pub layer_id: VegetationLayerId,
}

impl VegetationPromotionTarget {
    pub fn validate(&self) -> Result<(), VegetationPromotionError> {
        if self.schema_version != VEGETATION_PROMOTION_SCHEMA_VERSION {
            return Err(VegetationPromotionError::UnsupportedVersion(
                self.schema_version,
            ));
        }
        if !self.center_x_m.is_finite()
            || !self.center_z_m.is_finite()
            || !(-256.0..256.0).contains(&self.center_x_m)
            || !(-256.0..256.0).contains(&self.center_z_m)
            || !self.maximum_distance_m.is_finite()
            || self.maximum_distance_m <= 0.0
            || self.maximum_distance_m > MAX_VEGETATION_PROMOTION_RADIUS_M
        {
            return Err(VegetationPromotionError::InvalidTarget);
        }
        Ok(())
    }
}

/// Revision-bound request to promote the nearest generated placement in one layer.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct VegetationPromotionCommand {
    pub command_id: CommandId,
    pub base_revision: Revision,
    pub project_id: ProjectId,
    pub module_id: ModuleId,
    pub area_id: AreaId,
    pub target: VegetationPromotionTarget,
}

/// Exact deterministic resolution, including immutable promotion provenance.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ResolvedVegetationInstance {
    pub instance_id: EntityId,
    pub distance_squared_m: f64,
    pub source: VegetationPromotionSource,
}

/// Candidate workspace edit which promotes exactly one generated placement.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct VegetationPromotionResult {
    pub command: VegetationPromotionCommand,
    pub promoted_instance: PromotedVegetationInstance,
    pub distance_squared_m: f64,
    pub edit: WorkspaceEditResult,
}

/// Revision-bound persistent transform replacement for one promoted instance.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct VegetationRetouchCommand {
    pub command_id: CommandId,
    pub base_revision: Revision,
    pub project_id: ProjectId,
    pub module_id: ModuleId,
    pub area_id: AreaId,
    pub instance_id: EntityId,
    pub authored_transform: VegetationInstanceTransform,
}

/// Candidate workspace edit which changes only the authored transform.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct VegetationRetouchResult {
    pub command: VegetationRetouchCommand,
    pub previous_transform: VegetationInstanceTransform,
    pub edit: WorkspaceEditResult,
}

/// Resolves the nearest visible generated placement in a bounded layer-specific radius.
///
/// Distance and stable instance identity form the canonical ordering. A promoted instance wins the
/// same ordering and reports `AlreadyPromoted`, so a repeated click cannot select through it.
pub fn resolve_nearest_generated_vegetation_instance(
    workspace: &WorkspaceDocument,
    command: &VegetationPromotionCommand,
) -> Result<ResolvedVegetationInstance, VegetationPromotionError> {
    let area = validate_command_context(
        workspace,
        command.base_revision,
        command.project_id,
        command.module_id,
        command.area_id,
    )?;
    command.target.validate()?;
    if !area
        .vegetation
        .layers
        .iter()
        .any(|layer| layer.id == command.target.layer_id)
    {
        return Err(WorkspaceError::MissingTarget("vegetation layer").into());
    }

    let terrain_document_hash = area.terrain.content_hash().map_err(WorkspaceError::from)?;
    let vegetation_document_hash = area
        .vegetation
        .content_hash()
        .map_err(WorkspaceError::from)?;
    let vegetation_density_document_hash = area
        .vegetation_density
        .content_hash()
        .map_err(WorkspaceError::from)?;
    let river_projection =
        RiverGenerationPlan::from_documents(&area.terrain, &area.paths, &area.roads, &area.rivers)?
            .generate()?;
    let generated = VegetationGenerationPlan::from_documents_with_density(
        &river_projection.effective_terrain,
        &area.vegetation,
        &area.vegetation_density,
    )?
    .generate()?;
    let promoted_ids = area
        .vegetation
        .promoted_instances
        .iter()
        .map(|instance| instance.id)
        .collect::<BTreeSet<_>>();
    let maximum_distance_squared = f64::from(command.target.maximum_distance_m).powi(2);
    let nearest = generated
        .chunks
        .iter()
        .flat_map(|chunk| &chunk.instances)
        .filter(|instance| instance.layer_id == command.target.layer_id)
        .filter_map(|instance| {
            let distance_squared = horizontal_distance_squared(
                instance,
                command.target.center_x_m,
                command.target.center_z_m,
            );
            (distance_squared <= maximum_distance_squared).then_some((instance, distance_squared))
        })
        .min_by(|(left, left_distance), (right, right_distance)| {
            let left_is_promoted = promoted_ids.contains(&left.id.as_entity_id());
            let right_is_promoted = promoted_ids.contains(&right.id.as_entity_id());
            left_distance
                .total_cmp(right_distance)
                // At equal distance the authored instance is the visible selection surface. It
                // must win before stable ID ordering so a click cannot promote through an overlap.
                .then_with(|| right_is_promoted.cmp(&left_is_promoted))
                .then_with(|| left.id.cmp(&right.id))
        })
        .ok_or(VegetationPromotionError::MissingGeneratedInstance)?;

    if promoted_ids.contains(&nearest.0.id.as_entity_id()) {
        return Err(VegetationPromotionError::AlreadyPromoted(
            nearest.0.id.as_entity_id(),
        ));
    }
    let transform = VegetationInstanceTransform::new(
        nearest.0.position_m,
        nearest.0.yaw_radians,
        nearest.0.scale,
    );
    Ok(ResolvedVegetationInstance {
        instance_id: nearest.0.id.as_entity_id(),
        distance_squared_m: nearest.1,
        source: VegetationPromotionSource {
            workspace_revision: workspace.revision,
            generation_algorithm: VegetationGenerationAlgorithm::ForgeScatterV1,
            terrain_document_hash,
            vegetation_document_hash,
            vegetation_density_document_hash,
            layer_id: nearest.0.layer_id.clone(),
            kind: nearest.0.kind,
            transform,
            wind_phase_radians: nearest.0.wind_phase_radians,
            wind_response: nearest.0.wind_response,
            slope_gradient_squared: nearest.0.slope_gradient_squared,
            material_weight: nearest.0.material_weight,
        },
    })
}

/// Produces one validated promotion revision without mutating the workspace or a renderer cache.
pub fn preview_promote_generated_vegetation_instance(
    workspace: &WorkspaceDocument,
    command: VegetationPromotionCommand,
) -> Result<VegetationPromotionResult, VegetationPromotionError> {
    let resolved = resolve_nearest_generated_vegetation_instance(workspace, &command)?;
    let area = workspace
        .area(command.module_id, command.area_id)
        .ok_or(WorkspaceError::MissingTarget("area"))?;
    let promoted_instance = PromotedVegetationInstance {
        id: resolved.instance_id,
        authored_transform: resolved.source.transform,
        source: resolved.source,
    };
    let mut promoted_instances = area.vegetation.promoted_instances.clone();
    promoted_instances.push(promoted_instance.clone());
    promoted_instances.sort_by_key(|instance| instance.id);
    let edit = workspace.preview_edit(WorkspaceEdit {
        command_id: command.command_id,
        base_revision: command.base_revision,
        change: WorkspaceChange::SetVegetationPromotedInstances {
            module_id: command.module_id,
            area_id: command.area_id,
            expected_vegetation_hash: area
                .vegetation
                .content_hash()
                .map_err(WorkspaceError::from)?,
            promoted_instances,
        },
    })?;
    Ok(VegetationPromotionResult {
        command,
        promoted_instance,
        distance_squared_m: resolved.distance_squared_m,
        edit,
    })
}

/// Produces one validated transform-only retouch revision for a promoted instance.
pub fn preview_retouch_promoted_vegetation_instance(
    workspace: &WorkspaceDocument,
    command: VegetationRetouchCommand,
) -> Result<VegetationRetouchResult, VegetationPromotionError> {
    let area = validate_command_context(
        workspace,
        command.base_revision,
        command.project_id,
        command.module_id,
        command.area_id,
    )?;
    let mut promoted_instances = area.vegetation.promoted_instances.clone();
    let promoted = promoted_instances
        .iter_mut()
        .find(|instance| instance.id == command.instance_id)
        .ok_or(VegetationPromotionError::MissingPromotedInstance(
            command.instance_id,
        ))?;
    let previous_transform = promoted.authored_transform;
    promoted.authored_transform = command.authored_transform;
    let edit = workspace.preview_edit(WorkspaceEdit {
        command_id: command.command_id,
        base_revision: command.base_revision,
        change: WorkspaceChange::SetVegetationPromotedInstances {
            module_id: command.module_id,
            area_id: command.area_id,
            expected_vegetation_hash: area
                .vegetation
                .content_hash()
                .map_err(WorkspaceError::from)?,
            promoted_instances,
        },
    })?;
    Ok(VegetationRetouchResult {
        command,
        previous_transform,
        edit,
    })
}

fn validate_command_context(
    workspace: &WorkspaceDocument,
    base_revision: Revision,
    project_id: ProjectId,
    module_id: ModuleId,
    area_id: AreaId,
) -> Result<&WorkspaceArea, VegetationPromotionError> {
    workspace.validate()?;
    if workspace.project.id != project_id {
        return Err(VegetationPromotionError::WrongProject);
    }
    if workspace.revision != base_revision {
        return Err(WorkspaceError::StaleRevision.into());
    }
    workspace
        .area(module_id, area_id)
        .ok_or_else(|| WorkspaceError::MissingTarget("area").into())
}

fn horizontal_distance_squared(instance: &VegetationInstance, x_m: f32, z_m: f32) -> f64 {
    let delta_x = f64::from(instance.position_m.x) - f64::from(x_m);
    let delta_z = f64::from(instance.position_m.z) - f64::from(z_m);
    delta_x * delta_x + delta_z * delta_z
}

#[derive(Debug, Error)]
pub enum VegetationPromotionError {
    #[error("unsupported vegetation-promotion target version {0}")]
    UnsupportedVersion(u32),
    #[error(
        "vegetation promotion requires finite coordinates in [-256,256), a radius in ]0,64] metres and one valid layer"
    )]
    InvalidTarget,
    #[error("vegetation promotion belongs to another project")]
    WrongProject,
    #[error("no generated vegetation instance matches the bounded target")]
    MissingGeneratedInstance,
    #[error("vegetation instance {0} is already promoted")]
    AlreadyPromoted(EntityId),
    #[error("promoted vegetation instance {0} does not exist in the target area")]
    MissingPromotedInstance(EntityId),
    #[error("vegetation generation failed while resolving the target: {0}")]
    Generation(#[from] VegetationGenerationError),
    #[error("river projection failed while resolving the visible target: {0}")]
    River(#[from] RiverGenerationError),
    #[error("invalid vegetation promotion source or result: {0}")]
    Workspace(#[from] WorkspaceError),
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::{
        PathCreateCommand, RiverUpsertCommand, VegetationRuntimeCache, VegetationRuntimeCacheError,
        preview_create_path, preview_upsert_river,
    };
    use forge_core::Vec3;
    use forge_project::{
        AuthoredRiver, MAX_VEGETATION_EXPANDED_GRASS_BLADES, NewProjectCommand, PathId,
        RiverChannelProfile, RiverChannelProfileAlgorithm, RiverDocument, RiverSurface,
        VegetationDensityLayer, VegetationDensityOverride, VegetationKind,
    };
    use std::sync::Arc;

    fn source() -> WorkspaceDocument {
        let mut document = WorkspaceDocument::create(NewProjectCommand {
            command_id: CommandId::deterministic("vegetation-promotion/create"),
            project_id: ProjectId::deterministic("vegetation-promotion/project"),
            module_id: ModuleId::deterministic("vegetation-promotion/module"),
            area_id: AreaId::deterministic("vegetation-promotion/area"),
            project_name: "Promotion project".into(),
            module_name: "Promotion module".into(),
            area_name: "Promotion area".into(),
            seed: 0x5052_4f4d_4f54_4556,
            spawn: Vec3::new(0.0, 16.0, 0.0),
        })
        .unwrap()
        .document;
        let area = &mut document.project.modules[0].areas[0];
        let mut grass = area
            .vegetation
            .layers
            .iter()
            .find(|layer| layer.kind == VegetationKind::Grass)
            .unwrap()
            .clone();
        grass.acceptance_probability = 1.0;
        grass.attempts_per_cell = 1;
        grass.minimum_spacing_m = 0.0;
        grass.maximum_slope_gradient = 16.0;
        grass.minimum_altitude_m = -10_000.0;
        grass.maximum_altitude_m = 10_000.0;
        grass.material_filter = None;
        area.vegetation.layers = vec![grass];
        area.vegetation.exclusions.clear();
        area.vegetation_density.layers = vec![VegetationDensityLayer::new(
            area.vegetation.layers[0].id.clone(),
            u8::MAX,
            vec![],
        )];
        document.validate().unwrap();
        document
    }

    fn generated(workspace: &WorkspaceDocument) -> std::sync::Arc<crate::GeneratedVegetation> {
        let area = &workspace.project.modules[0].areas[0];
        VegetationGenerationPlan::from_documents_with_density(
            &area.terrain,
            &area.vegetation,
            &area.vegetation_density,
        )
        .unwrap()
        .generate()
        .unwrap()
    }

    fn first_instance(workspace: &WorkspaceDocument) -> VegetationInstance {
        generated(workspace)
            .chunks
            .iter()
            .flat_map(|chunk| &chunk.instances)
            .next()
            .unwrap()
            .clone()
    }

    fn with_river(source: &WorkspaceDocument, path_id: PathId, z_m: f32) -> WorkspaceDocument {
        let module_id = source.project.modules[0].id;
        let area_id = source.project.modules[0].areas[0].id;
        let with_path = preview_create_path(
            source,
            PathCreateCommand {
                command_id: CommandId::deterministic(format!(
                    "vegetation-promotion/river-path/{path_id}"
                )),
                base_revision: source.revision,
                project_id: source.project.id,
                module_id,
                area_id,
                path_id,
                name: "Cours de promotion".to_owned(),
                trace_xz_m: vec![[-96.0, z_m], [96.0, z_m]],
            },
        )
        .unwrap()
        .edit
        .document;
        preview_upsert_river(
            &with_path,
            RiverUpsertCommand {
                command_id: CommandId::deterministic(format!(
                    "vegetation-promotion/river-upsert/{path_id}"
                )),
                base_revision: with_path.revision,
                project_id: with_path.project.id,
                module_id,
                area_id,
                river: AuthoredRiver {
                    path_id,
                    channel: RiverChannelProfile {
                        algorithm: RiverChannelProfileAlgorithm::TerrainCarvedChannelV1,
                        surface_width_m: 16.0,
                        bed_depth_m: 3.0,
                        bank_blend_width_m: 8.0,
                    },
                    surface: RiverSurface::ForgeWaterV1,
                },
            },
        )
        .unwrap()
        .edit
        .document
    }

    fn generated_on_effective_terrain(
        workspace: &WorkspaceDocument,
    ) -> (
        crate::RiverGenerationResult,
        Arc<crate::GeneratedVegetation>,
    ) {
        let area = &workspace.project.modules[0].areas[0];
        let projection = RiverGenerationPlan::from_documents(
            &area.terrain,
            &area.paths,
            &area.roads,
            &area.rivers,
        )
        .unwrap()
        .generate()
        .unwrap();
        let vegetation = VegetationGenerationPlan::from_documents_with_density(
            &projection.effective_terrain,
            &area.vegetation,
            &area.vegetation_density,
        )
        .unwrap()
        .generate()
        .unwrap();
        (projection, vegetation)
    }

    fn promote_command(
        workspace: &WorkspaceDocument,
        instance: &VegetationInstance,
        key: &str,
    ) -> VegetationPromotionCommand {
        VegetationPromotionCommand {
            command_id: CommandId::deterministic(key),
            base_revision: workspace.revision,
            project_id: workspace.project.id,
            module_id: workspace.project.modules[0].id,
            area_id: workspace.project.modules[0].areas[0].id,
            target: VegetationPromotionTarget {
                schema_version: VEGETATION_PROMOTION_SCHEMA_VERSION,
                center_x_m: instance.position_m.x,
                center_z_m: instance.position_m.z,
                maximum_distance_m: 0.25,
                layer_id: instance.layer_id.clone(),
            },
        }
    }

    #[test]
    fn resolver_is_deterministic_and_initial_promotion_is_bit_identical() {
        let source = source();
        let source_generation = generated(&source);
        let instance = first_instance(&source);
        let command = promote_command(&source, &instance, "vegetation-promotion/promote");
        let first_resolution =
            resolve_nearest_generated_vegetation_instance(&source, &command).unwrap();
        let second_resolution =
            resolve_nearest_generated_vegetation_instance(&source, &command).unwrap();
        assert_eq!(first_resolution, second_resolution);
        assert_eq!(first_resolution.instance_id, instance.id.as_entity_id());
        assert_eq!(
            first_resolution.distance_squared_m.to_bits(),
            0.0_f64.to_bits()
        );

        let result = preview_promote_generated_vegetation_instance(&source, command).unwrap();
        assert_eq!(result.edit.receipt.inverse_snapshot.as_ref(), &source);
        assert_eq!(
            result.edit.document.revision.get(),
            source.revision.get() + 1
        );
        assert_eq!(
            result.promoted_instance.source.transform,
            result.promoted_instance.authored_transform
        );
        let promoted_generation = generated(&result.edit.document);
        // Whole-vector equality proves both exact placement substitution and unchanged ordering.
        assert_eq!(source_generation.chunks, promoted_generation.chunks);
        assert_eq!(
            source_generation.kind_counts,
            promoted_generation.kind_counts
        );

        let reopened =
            WorkspaceDocument::from_json(&result.edit.document.to_pretty_json().unwrap()).unwrap();
        assert_eq!(generated(&reopened).chunks, promoted_generation.chunks);
    }

    #[test]
    fn river_visible_instance_resolution_and_promotion_are_bit_identical() {
        let path_id = PathId::deterministic("vegetation-promotion/visible-river");
        let source = with_river(&source(), path_id, 0.0);
        let area = &source.project.modules[0].areas[0];
        let canonical_hash = area.terrain.content_hash().unwrap();
        let canonical = generated(&source);
        let (river_projection, visible) = generated_on_effective_terrain(&source);
        assert_ne!(
            canonical_hash,
            river_projection.generated.effective_terrain_document_hash
        );

        let candidate = visible
            .chunks
            .iter()
            .flat_map(|chunk| &chunk.instances)
            .find(|visible_instance| {
                canonical
                    .chunks
                    .iter()
                    .flat_map(|chunk| &chunk.instances)
                    .find(|canonical_instance| canonical_instance.id == visible_instance.id)
                    .is_some_and(|canonical_instance| {
                        canonical_instance.position_m.y.to_bits()
                            != visible_instance.position_m.y.to_bits()
                    })
            })
            .unwrap()
            .clone();
        let command = promote_command(
            &source,
            &candidate,
            "vegetation-promotion/visible-river/promote",
        );
        let resolved = resolve_nearest_generated_vegetation_instance(&source, &command).unwrap();
        assert_eq!(resolved.instance_id, candidate.id.as_entity_id());
        assert_eq!(resolved.distance_squared_m.to_bits(), 0.0_f64.to_bits());
        assert_eq!(resolved.source.transform.position_m, candidate.position_m);
        assert_eq!(
            resolved.source.transform.yaw_radians.to_bits(),
            candidate.yaw_radians.to_bits()
        );
        assert_eq!(
            resolved.source.transform.uniform_scale.to_bits(),
            candidate.scale.to_bits()
        );
        assert_eq!(resolved.source.terrain_document_hash, canonical_hash);
        assert_eq!(resolved.source.workspace_revision, source.revision);

        let promoted = preview_promote_generated_vegetation_instance(&source, command).unwrap();
        assert_eq!(promoted.promoted_instance.id, candidate.id.as_entity_id());
        assert_eq!(promoted.promoted_instance.source, resolved.source);
        assert_eq!(
            promoted.promoted_instance.authored_transform,
            resolved.source.transform
        );
        assert_eq!(promoted.edit.receipt.inverse_snapshot.as_ref(), &source);
    }

    #[test]
    fn resolver_propagates_an_invalid_river_projection_without_mutation() {
        let first_path_id = PathId::deterministic("vegetation-promotion/overlap-river-a");
        let second_path_id = PathId::deterministic("vegetation-promotion/overlap-river-b");
        let mut invalid = source();
        for (path_id, z_m) in [(first_path_id, 0.0), (second_path_id, 5.0)] {
            invalid = preview_create_path(
                &invalid,
                PathCreateCommand {
                    command_id: CommandId::deterministic(format!(
                        "vegetation-promotion/overlap-path/{path_id}"
                    )),
                    base_revision: invalid.revision,
                    project_id: invalid.project.id,
                    module_id: invalid.project.modules[0].id,
                    area_id: invalid.project.modules[0].areas[0].id,
                    path_id,
                    name: format!("Cours {path_id}"),
                    trace_xz_m: vec![[-40.0, z_m], [40.0, z_m]],
                },
            )
            .unwrap()
            .edit
            .document;
        }
        let revision = invalid.revision;
        let project_id = invalid.project.id;
        let area = &mut invalid.project.modules[0].areas[0];
        let mut rivers = [first_path_id, second_path_id]
            .map(|path_id| AuthoredRiver {
                path_id,
                channel: RiverChannelProfile {
                    algorithm: RiverChannelProfileAlgorithm::TerrainCarvedChannelV1,
                    surface_width_m: 4.0,
                    bed_depth_m: 1.0,
                    bank_blend_width_m: 2.0,
                },
                surface: RiverSurface::ForgeWaterV1,
            })
            .to_vec();
        rivers.sort_by_key(|river| river.path_id);
        area.rivers = RiverDocument::new(project_id, area.id, revision, "Rivières", rivers);
        invalid.validate().unwrap();
        let source_hash = invalid.content_hash().unwrap();
        let instance = first_instance(&invalid);
        let command = promote_command(
            &invalid,
            &instance,
            "vegetation-promotion/overlap-river/query",
        );
        assert!(matches!(
            resolve_nearest_generated_vegetation_instance(&invalid, &command),
            Err(VegetationPromotionError::River(
                RiverGenerationError::RiverOverlap { .. }
            ))
        ));
        assert_eq!(invalid.content_hash().unwrap(), source_hash);
    }

    #[test]
    fn resolver_rejects_stale_wrong_area_missing_and_already_promoted_targets() {
        let source = source();
        let instance = first_instance(&source);

        let mut stale = promote_command(&source, &instance, "vegetation-promotion/stale");
        stale.base_revision = source.revision.next().unwrap();
        assert!(matches!(
            resolve_nearest_generated_vegetation_instance(&source, &stale),
            Err(VegetationPromotionError::Workspace(
                WorkspaceError::StaleRevision
            ))
        ));

        let mut wrong_area = promote_command(&source, &instance, "vegetation-promotion/wrong-area");
        wrong_area.area_id = AreaId::deterministic("vegetation-promotion/other-area");
        assert!(matches!(
            resolve_nearest_generated_vegetation_instance(&source, &wrong_area),
            Err(VegetationPromotionError::Workspace(
                WorkspaceError::MissingTarget("area")
            ))
        ));

        let mut invalid = promote_command(&source, &instance, "vegetation-promotion/invalid");
        invalid.target.maximum_distance_m = MAX_VEGETATION_PROMOTION_RADIUS_M + 1.0;
        assert!(matches!(
            resolve_nearest_generated_vegetation_instance(&source, &invalid),
            Err(VegetationPromotionError::InvalidTarget)
        ));

        let mut missing_layer =
            promote_command(&source, &instance, "vegetation-promotion/missing-layer");
        missing_layer.target.layer_id = VegetationLayerId::new("missing_layer");
        assert!(matches!(
            resolve_nearest_generated_vegetation_instance(&source, &missing_layer),
            Err(VegetationPromotionError::Workspace(
                WorkspaceError::MissingTarget("vegetation layer")
            ))
        ));

        let mut missing = promote_command(&source, &instance, "vegetation-promotion/missing");
        missing.target.center_x_m = 255.999;
        missing.target.center_z_m = 255.999;
        missing.target.maximum_distance_m = 0.000_001;
        assert!(matches!(
            resolve_nearest_generated_vegetation_instance(&source, &missing),
            Err(VegetationPromotionError::MissingGeneratedInstance)
        ));

        let promoted = preview_promote_generated_vegetation_instance(
            &source,
            promote_command(&source, &instance, "vegetation-promotion/first"),
        )
        .unwrap()
        .edit
        .document;
        let repeated = promote_command(
            &promoted,
            &instance,
            "vegetation-promotion/already-promoted",
        );
        assert!(matches!(
            resolve_nearest_generated_vegetation_instance(&promoted, &repeated),
            Err(VegetationPromotionError::AlreadyPromoted(id))
                if id == instance.id.as_entity_id()
        ));
    }

    #[test]
    fn resolver_prioritizes_an_overlapping_promotion_before_stable_id_order() {
        let source = source();
        let source_generation = generated(&source);
        let mut instances = source_generation
            .chunks
            .iter()
            .flat_map(|chunk| &chunk.instances)
            .cloned()
            .collect::<Vec<_>>();
        instances.sort_by_key(|instance| instance.id);
        let unpromoted = instances.first().unwrap().clone();
        let promoted_source = instances.last().unwrap().clone();
        assert!(unpromoted.id < promoted_source.id);

        let promoted = preview_promote_generated_vegetation_instance(
            &source,
            promote_command(
                &source,
                &promoted_source,
                "vegetation-promotion/overlap-source",
            ),
        )
        .unwrap()
        .edit
        .document;
        let promotion = &promoted.project.modules[0].areas[0]
            .vegetation
            .promoted_instances[0];
        let mut overlap_transform = promotion.authored_transform;
        overlap_transform.position_m = unpromoted.position_m;
        let overlapped = preview_retouch_promoted_vegetation_instance(
            &promoted,
            VegetationRetouchCommand {
                command_id: CommandId::deterministic("vegetation-promotion/overlap-retouch"),
                base_revision: promoted.revision,
                project_id: promoted.project.id,
                module_id: promoted.project.modules[0].id,
                area_id: promoted.project.modules[0].areas[0].id,
                instance_id: promoted_source.id.as_entity_id(),
                authored_transform: overlap_transform,
            },
        )
        .unwrap()
        .edit
        .document;

        let query = promote_command(
            &overlapped,
            &unpromoted,
            "vegetation-promotion/overlap-query",
        );
        assert!(matches!(
            resolve_nearest_generated_vegetation_instance(&overlapped, &query),
            Err(VegetationPromotionError::AlreadyPromoted(id))
                if id == promoted_source.id.as_entity_id()
        ));
    }

    #[test]
    fn orphan_grass_promotion_over_budget_fails_atomically() {
        let mut source = source();
        let area = &mut source.project.modules[0].areas[0];
        area.vegetation.layers[0].attempts_per_cell = 4;
        area.vegetation.presentation.grass_blades_per_patch = 64;
        source.validate().unwrap();
        let instance = first_instance(&source);
        let promoted = preview_promote_generated_vegetation_instance(
            &source,
            promote_command(&source, &instance, "vegetation-promotion/budget-source"),
        )
        .unwrap()
        .edit
        .document;
        let mut cache =
            VegetationRuntimeCache::from_area(&promoted.project.modules[0].areas[0]).unwrap();
        let before_key = cache.key();
        let before_snapshot = cache.snapshot();
        let before_handles = cache.chunks().to_vec();

        let mut orphaned = promoted.clone();
        orphaned.project.modules[0].areas[0].vegetation.seed = orphaned.project.modules[0].areas[0]
            .vegetation
            .seed
            .wrapping_add(1);
        orphaned.validate().unwrap();
        let error = cache
            .refresh_from_area(&orphaned.project.modules[0].areas[0])
            .unwrap_err();
        assert!(matches!(
            error,
            VegetationRuntimeCacheError::Generation(
                VegetationGenerationError::ExpandedGrassBladeBudgetExceeded {
                    blades,
                    maximum: MAX_VEGETATION_EXPANDED_GRASS_BLADES,
                }
            ) if blades == MAX_VEGETATION_EXPANDED_GRASS_BLADES + 64
        ));
        assert_eq!(cache.key(), before_key);
        assert_eq!(cache.snapshot(), before_snapshot);
        assert!(
            before_handles
                .iter()
                .zip(cache.chunks())
                .all(|(before, after)| Arc::ptr_eq(before, after))
        );
    }

    #[test]
    #[allow(
        clippy::cast_possible_truncation,
        clippy::cast_sign_loss,
        reason = "validated in-bounds generated positions map exactly to the bounded u16 grid"
    )]
    fn retouch_persists_and_promotion_survives_a_missing_procedural_source() {
        let source = source();
        let instance = first_instance(&source);
        let promoted = preview_promote_generated_vegetation_instance(
            &source,
            promote_command(&source, &instance, "vegetation-promotion/orphan-source"),
        )
        .unwrap()
        .edit
        .document;
        let module_id = promoted.project.modules[0].id;
        let area_id = promoted.project.modules[0].areas[0].id;
        let mut transform = promoted.project.modules[0].areas[0]
            .vegetation
            .promoted_instances[0]
            .authored_transform;
        transform.yaw_radians = (transform.yaw_radians + 0.25) % std::f32::consts::TAU;
        let retouched = preview_retouch_promoted_vegetation_instance(
            &promoted,
            VegetationRetouchCommand {
                command_id: CommandId::deterministic("vegetation-promotion/retouch"),
                base_revision: promoted.revision,
                project_id: promoted.project.id,
                module_id,
                area_id,
                instance_id: instance.id.as_entity_id(),
                authored_transform: transform,
            },
        )
        .unwrap();
        assert_eq!(retouched.edit.receipt.inverse_snapshot.as_ref(), &promoted);
        let retouched = retouched.edit.document;

        let area = retouched.area(module_id, area_id).unwrap();
        let cell_size = area.vegetation.cell_size_m();
        let cell_x = ((instance.position_m.x + 256.0) / cell_size).floor() as u16;
        let cell_z = ((instance.position_m.z + 256.0) / cell_size).floor() as u16;
        let hidden_source = retouched
            .preview_edit(WorkspaceEdit {
                command_id: CommandId::deterministic("vegetation-promotion/hide-source"),
                base_revision: retouched.revision,
                change: WorkspaceChange::SetVegetationDensityOverrides {
                    module_id,
                    area_id,
                    layer_id: instance.layer_id.clone(),
                    expected_density_hash: area.vegetation_density.content_hash().unwrap(),
                    density_overrides: vec![VegetationDensityOverride::new(cell_x, cell_z, 0)],
                },
            })
            .unwrap()
            .document;
        let matching = generated(&hidden_source)
            .chunks
            .iter()
            .flat_map(|chunk| &chunk.instances)
            .filter(|candidate| candidate.id == instance.id)
            .cloned()
            .collect::<Vec<_>>();
        assert_eq!(matching.len(), 1);
        assert_eq!(matching[0].position_m, transform.position_m);
        assert_eq!(
            matching[0].yaw_radians.to_bits(),
            transform.yaw_radians.to_bits()
        );
        assert_eq!(
            matching[0].scale.to_bits(),
            transform.uniform_scale.to_bits()
        );

        let reopened =
            WorkspaceDocument::from_json(&hidden_source.to_pretty_json().unwrap()).unwrap();
        assert_eq!(
            generated(&reopened).chunks,
            generated(&hidden_source).chunks
        );
    }

    #[test]
    fn retouch_rejects_a_missing_promoted_instance() {
        let source = source();
        let result = preview_retouch_promoted_vegetation_instance(
            &source,
            VegetationRetouchCommand {
                command_id: CommandId::deterministic("vegetation-promotion/missing-retouch"),
                base_revision: source.revision,
                project_id: source.project.id,
                module_id: source.project.modules[0].id,
                area_id: source.project.modules[0].areas[0].id,
                instance_id: EntityId::deterministic("vegetation-promotion/missing-instance"),
                authored_transform: VegetationInstanceTransform::new(Vec3::ZERO, 0.0, 1.0),
            },
        );
        assert!(matches!(
            result,
            Err(VegetationPromotionError::MissingPromotedInstance(_))
        ));
    }

    #[test]
    fn retouch_rejects_a_noop_without_creating_a_revision() {
        let source = source();
        let instance = first_instance(&source);
        let promoted = preview_promote_generated_vegetation_instance(
            &source,
            promote_command(&source, &instance, "vegetation-promotion/noop-source"),
        )
        .unwrap()
        .edit
        .document;
        let promotion = &promoted.project.modules[0].areas[0]
            .vegetation
            .promoted_instances[0];
        let result = preview_retouch_promoted_vegetation_instance(
            &promoted,
            VegetationRetouchCommand {
                command_id: CommandId::deterministic("vegetation-promotion/noop-retouch"),
                base_revision: promoted.revision,
                project_id: promoted.project.id,
                module_id: promoted.project.modules[0].id,
                area_id: promoted.project.modules[0].areas[0].id,
                instance_id: promotion.id,
                authored_transform: promotion.authored_transform,
            },
        );
        assert!(matches!(
            result,
            Err(VegetationPromotionError::Workspace(
                WorkspaceError::NoChange
            ))
        ));
        assert_eq!(promoted.revision, source.revision.next().unwrap());
    }
}
