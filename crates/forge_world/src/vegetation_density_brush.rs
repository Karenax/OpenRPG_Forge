//! Pure source brush for the versioned vegetation-density mask.

use forge_core::{AreaId, CommandId, ModuleId, ProjectId, Revision};
use forge_project::{
    VEGETATION_EXTENT_M, VegetationDensityOverride, VegetationLayerId, WorkspaceChange,
    WorkspaceDocument, WorkspaceEdit, WorkspaceEditResult, WorkspaceError,
};
use serde::{Deserialize, Serialize};
use std::collections::{BTreeMap, BTreeSet};
use thiserror::Error;

const MAX_STAMPS_PER_STROKE: usize = 64;

/// Version 1 circular density stamp with smoothstep falloff on vegetation-cell centres.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct VegetationDensityBrush {
    pub schema_version: u32,
    pub center_x_m: f32,
    pub center_z_m: f32,
    pub radius_m: f32,
    /// Fraction in ]0, 1] used to blend the current density toward `target_density`.
    pub strength: f32,
    /// Quantized density. 0 excludes the layer; 255 preserves its full recipe probability.
    pub target_density: u8,
    pub layer_id: VegetationLayerId,
}

impl VegetationDensityBrush {
    pub fn validate(&self) -> Result<(), VegetationDensityBrushError> {
        if self.schema_version != 1 {
            return Err(VegetationDensityBrushError::UnsupportedVersion(
                self.schema_version,
            ));
        }
        if !bounded(self.center_x_m, -256.0, 256.0)
            || !bounded(self.center_z_m, -256.0, 256.0)
            || !bounded(self.radius_m, 2.0, 64.0)
            || !bounded(self.strength, f32::MIN_POSITIVE, 1.0)
        {
            return Err(VegetationDensityBrushError::InvalidParameters);
        }
        let id = self.layer_id.as_str();
        if id.is_empty()
            || id.len() > 64
            || !id.as_bytes()[0].is_ascii_lowercase()
            || !id
                .bytes()
                .all(|byte| byte.is_ascii_lowercase() || byte.is_ascii_digit() || byte == b'_')
        {
            return Err(VegetationDensityBrushError::InvalidLayerId(id.to_owned()));
        }
        Ok(())
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct VegetationDensityBrushCommand {
    pub command_id: CommandId,
    pub base_revision: Revision,
    pub project_id: ProjectId,
    pub module_id: ModuleId,
    pub area_id: AreaId,
    pub brush: VegetationDensityBrush,
}

/// Exact density-mask candidate plus intent, inverse and hashes. Safe to discard for dry-run.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct VegetationDensityBrushResult {
    pub command: VegetationDensityBrushCommand,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub additional_centers: Vec<[f32; 2]>,
    pub changed_cells: usize,
    pub edit: WorkspaceEditResult,
}

/// Previews one density stamp without mutating the workspace or a renderer cache.
pub fn preview_vegetation_density_brush(
    workspace: &WorkspaceDocument,
    command: VegetationDensityBrushCommand,
) -> Result<VegetationDensityBrushResult, VegetationDensityBrushError> {
    workspace.validate()?;
    if workspace.project.id != command.project_id {
        return Err(VegetationDensityBrushError::WrongProject);
    }
    if workspace.revision != command.base_revision {
        return Err(WorkspaceError::StaleRevision.into());
    }
    command.brush.validate()?;
    let area = workspace
        .area(command.module_id, command.area_id)
        .ok_or(WorkspaceError::MissingTarget("area"))?;
    let density = &area.vegetation_density;
    let layer = density
        .layers
        .iter()
        .find(|layer| layer.layer_id == command.brush.layer_id)
        .ok_or(WorkspaceError::MissingTarget("vegetation layer"))?;
    let mut values = layer
        .overrides
        .iter()
        .map(|entry| ((entry.cell_z(), entry.cell_x()), entry.density()))
        .collect::<BTreeMap<_, _>>();
    let cells_per_axis = density.cells_per_axis();
    let cell_size_m = area.vegetation.cell_size_m();
    let mut changed_cells = 0;
    for cell_z in 0..cells_per_axis {
        for cell_x in 0..cells_per_axis {
            let x = -VEGETATION_EXTENT_M * 0.5 + (f32::from(cell_x) + 0.5) * cell_size_m;
            let z = -VEGETATION_EXTENT_M * 0.5 + (f32::from(cell_z) + 0.5) * cell_size_m;
            let dx = f64::from(x) - f64::from(command.brush.center_x_m);
            let dz = f64::from(z) - f64::from(command.brush.center_z_m);
            let radial = 1.0 - (dx * dx + dz * dz).sqrt() / f64::from(command.brush.radius_m);
            if radial <= 0.0 {
                continue;
            }
            let blend = radial * radial * (3.0 - 2.0 * radial) * f64::from(command.brush.strength);
            let before = values
                .get(&(cell_z, cell_x))
                .copied()
                .unwrap_or(layer.default_density);
            let after = blend_density(before, command.brush.target_density, blend);
            if after == before {
                continue;
            }
            if after == layer.default_density {
                values.remove(&(cell_z, cell_x));
            } else {
                values.insert((cell_z, cell_x), after);
            }
            changed_cells += 1;
        }
    }
    let edit = workspace.preview_edit(WorkspaceEdit {
        command_id: command.command_id,
        base_revision: command.base_revision,
        change: WorkspaceChange::SetVegetationDensityOverrides {
            module_id: command.module_id,
            area_id: command.area_id,
            layer_id: command.brush.layer_id.clone(),
            expected_density_hash: density.content_hash().map_err(WorkspaceError::from)?,
            density_overrides: values
                .into_iter()
                .map(|((cell_z, cell_x), value)| {
                    VegetationDensityOverride::new(cell_x, cell_z, value)
                })
                .collect(),
        },
    })?;
    edit.document
        .area(command.module_id, command.area_id)
        .ok_or(WorkspaceError::MissingTarget("area"))?
        .vegetation_density
        .validate()
        .map_err(WorkspaceError::from)?;
    Ok(VegetationDensityBrushResult {
        command,
        additional_centers: vec![],
        changed_cells,
        edit,
    })
}

/// A bounded continuous stroke is committed as one workspace revision and one undo entry.
pub fn preview_vegetation_density_stroke(
    workspace: &WorkspaceDocument,
    command: VegetationDensityBrushCommand,
    additional_centers: &[[f32; 2]],
) -> Result<VegetationDensityBrushResult, VegetationDensityBrushError> {
    if additional_centers.len() >= MAX_STAMPS_PER_STROKE {
        return Err(VegetationDensityBrushError::StrokeBudgetExceeded);
    }
    command.brush.validate()?;
    for center in additional_centers {
        VegetationDensityBrush {
            center_x_m: center[0],
            center_z_m: center[1],
            ..command.brush.clone()
        }
        .validate()?;
    }
    if workspace.revision != command.base_revision {
        return Err(WorkspaceError::StaleRevision.into());
    }
    let original = workspace
        .area(command.module_id, command.area_id)
        .ok_or(WorkspaceError::MissingTarget("area"))?;
    let original_density_revision = original.vegetation_density.revision;
    let mut candidate = workspace.clone();
    for center in std::iter::once([command.brush.center_x_m, command.brush.center_z_m])
        .chain(additional_centers.iter().copied())
    {
        let stamp = VegetationDensityBrushCommand {
            base_revision: candidate.revision,
            brush: VegetationDensityBrush {
                center_x_m: center[0],
                center_z_m: center[1],
                ..command.brush.clone()
            },
            ..command.clone()
        };
        match preview_vegetation_density_brush(&candidate, stamp) {
            Ok(result) => {
                candidate = result.edit.document;
                reset_stroke_candidate_revision(
                    &mut candidate,
                    command.module_id,
                    command.area_id,
                    command.base_revision,
                    original_density_revision,
                )?;
            }
            Err(VegetationDensityBrushError::Workspace(WorkspaceError::NoChange)) => {}
            Err(error) => return Err(error),
        }
    }
    let target = candidate
        .area(command.module_id, command.area_id)
        .ok_or(WorkspaceError::MissingTarget("area"))?;
    let original_layer = original
        .vegetation_density
        .layers
        .iter()
        .find(|layer| layer.layer_id == command.brush.layer_id)
        .ok_or(WorkspaceError::MissingTarget("vegetation layer"))?;
    let target_layer = target
        .vegetation_density
        .layers
        .iter()
        .find(|layer| layer.layer_id == command.brush.layer_id)
        .ok_or(WorkspaceError::MissingTarget("vegetation layer"))?;
    let before = original_layer
        .overrides
        .iter()
        .map(|entry| ((entry.cell_z(), entry.cell_x()), entry.density()))
        .collect::<BTreeMap<_, _>>();
    let after = target_layer
        .overrides
        .iter()
        .map(|entry| ((entry.cell_z(), entry.cell_x()), entry.density()))
        .collect::<BTreeMap<_, _>>();
    let changed_cells = before
        .keys()
        .chain(after.keys())
        .collect::<BTreeSet<_>>()
        .into_iter()
        .filter(|key| before.get(key) != after.get(key))
        .count();
    let edit = workspace.preview_edit(WorkspaceEdit {
        command_id: command.command_id,
        base_revision: command.base_revision,
        change: WorkspaceChange::SetVegetationDensityOverrides {
            module_id: command.module_id,
            area_id: command.area_id,
            layer_id: command.brush.layer_id.clone(),
            expected_density_hash: original
                .vegetation_density
                .content_hash()
                .map_err(WorkspaceError::from)?,
            density_overrides: target_layer.overrides.clone(),
        },
    })?;
    Ok(VegetationDensityBrushResult {
        command,
        additional_centers: additional_centers.to_vec(),
        changed_cells,
        edit,
    })
}

fn reset_stroke_candidate_revision(
    candidate: &mut WorkspaceDocument,
    module_id: ModuleId,
    area_id: AreaId,
    workspace_revision: Revision,
    density_revision: Revision,
) -> Result<(), VegetationDensityBrushError> {
    candidate.revision = workspace_revision;
    candidate
        .project
        .modules
        .iter_mut()
        .find(|module| module.id == module_id)
        .ok_or(WorkspaceError::MissingTarget("module"))?
        .areas
        .iter_mut()
        .find(|area| area.id == area_id)
        .ok_or(WorkspaceError::MissingTarget("area"))?
        .vegetation_density
        .revision = density_revision;
    Ok(())
}

#[allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    reason = "A convex blend of two u8 values is finite and remains in 0..=255"
)]
fn blend_density(before: u8, target: u8, blend: f64) -> u8 {
    (f64::from(before) * (1.0 - blend) + f64::from(target) * blend).round() as u8
}

fn bounded(value: f32, minimum: f32, maximum: f32) -> bool {
    value.is_finite() && (minimum..=maximum).contains(&value)
}

#[derive(Debug, Error)]
pub enum VegetationDensityBrushError {
    #[error(
        "vegetation-density stroke is limited to {MAX_STAMPS_PER_STROKE} stamps; use a shorter stroke"
    )]
    StrokeBudgetExceeded,
    #[error("unsupported vegetation-density brush version {0}")]
    UnsupportedVersion(u32),
    #[error(
        "vegetation-density brush requires finite coordinates in ±256 m, radius 2..64 m and strength ]0,1]"
    )]
    InvalidParameters,
    #[error("invalid vegetation layer id `{0}`")]
    InvalidLayerId(String),
    #[error("vegetation-density brush belongs to another project")]
    WrongProject,
    #[error("invalid vegetation-density brush source or result: {0}")]
    Workspace(#[from] WorkspaceError),
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_core::Vec3;
    use forge_project::{NewProjectCommand, VegetationKind};

    fn source() -> WorkspaceDocument {
        WorkspaceDocument::create(NewProjectCommand {
            command_id: CommandId::deterministic("create-density-brush-test"),
            project_id: ProjectId::deterministic("density-brush-project"),
            module_id: ModuleId::deterministic("density-brush-module"),
            area_id: AreaId::deterministic("density-brush-area"),
            project_name: "Density brush project".into(),
            module_name: "Module".into(),
            area_name: "Area".into(),
            seed: 42,
            spawn: Vec3::new(0.0, 2.0, 0.0),
        })
        .unwrap()
        .document
    }

    fn grass_layer(source: &WorkspaceDocument) -> VegetationLayerId {
        source.project.modules[0].areas[0]
            .vegetation
            .layers
            .iter()
            .find(|layer| layer.kind == VegetationKind::Grass)
            .unwrap()
            .id
            .clone()
    }

    fn command(source: &WorkspaceDocument) -> VegetationDensityBrushCommand {
        VegetationDensityBrushCommand {
            command_id: CommandId::deterministic("density-brush-command"),
            base_revision: source.revision,
            project_id: source.project.id,
            module_id: source.project.modules[0].id,
            area_id: source.project.modules[0].areas[0].id,
            brush: VegetationDensityBrush {
                schema_version: 1,
                center_x_m: 4.0,
                center_z_m: 4.0,
                radius_m: 20.0,
                strength: 1.0,
                target_density: u8::MAX,
                layer_id: grass_layer(source),
            },
        }
    }

    #[test]
    fn density_stamp_is_pure_revisioned_radial_and_canonical() {
        let source = source();
        let before = source.to_pretty_json().unwrap();
        let result = preview_vegetation_density_brush(&source, command(&source)).unwrap();
        assert!(result.changed_cells > 1);
        assert_eq!(result.edit.receipt.inverse_snapshot.as_ref(), &source);
        assert_eq!(
            result.edit.document.revision.get(),
            source.revision.get() + 1
        );
        let density = &result.edit.document.project.modules[0].areas[0].vegetation_density;
        let layer = grass_layer(&source);
        assert_eq!(density.density_u8(&layer, 32, 32), Some(u8::MAX));
        assert!(
            density
                .density_u8(&layer, 34, 32)
                .is_some_and(|value| value > 0 && value < u8::MAX)
        );
        assert_eq!(density.density_u8(&layer, 40, 40), Some(0));
        let overrides = &density
            .layers
            .iter()
            .find(|candidate| candidate.layer_id == layer)
            .unwrap()
            .overrides;
        assert!(overrides.windows(2).all(|pair| {
            (pair[0].cell_z(), pair[0].cell_x()) < (pair[1].cell_z(), pair[1].cell_x())
        }));
        assert_eq!(source.to_pretty_json().unwrap(), before);
    }

    #[test]
    fn density_stroke_is_one_revision_and_bounded() {
        let source = source();
        let command = command(&source);
        let result = preview_vegetation_density_stroke(
            &source,
            command.clone(),
            &[[20.0, 4.0], [36.0, 4.0]],
        )
        .unwrap();
        assert_eq!(
            result.edit.document.revision.get(),
            source.revision.get() + 1
        );
        assert_eq!(result.additional_centers.len(), 2);
        assert!(result.changed_cells > 1);
        assert!(
            preview_vegetation_density_stroke(&source, command.clone(), &[[f32::NAN, 0.0]])
                .is_err()
        );
        assert!(preview_vegetation_density_stroke(&source, command, &[[0.0, 0.0]; 64]).is_err());
    }

    #[test]
    fn density_brush_rejects_noop_invalid_wrong_project_and_missing_layer() {
        let source = source();
        let mut request = command(&source);
        request.brush.target_density = 0;
        assert!(matches!(
            preview_vegetation_density_brush(&source, request),
            Err(VegetationDensityBrushError::Workspace(
                WorkspaceError::NoChange
            ))
        ));
        let mut request = command(&source);
        request.brush.schema_version = 2;
        assert!(matches!(
            preview_vegetation_density_brush(&source, request),
            Err(VegetationDensityBrushError::UnsupportedVersion(2))
        ));
        let mut request = command(&source);
        request.brush.center_x_m = f32::NAN;
        assert!(matches!(
            preview_vegetation_density_brush(&source, request),
            Err(VegetationDensityBrushError::InvalidParameters)
        ));
        let mut request = command(&source);
        request.project_id = ProjectId::deterministic("other-project");
        assert!(matches!(
            preview_vegetation_density_brush(&source, request),
            Err(VegetationDensityBrushError::WrongProject)
        ));
        let mut request = command(&source);
        request.brush.layer_id = VegetationLayerId::new("unknown_layer");
        assert!(matches!(
            preview_vegetation_density_brush(&source, request),
            Err(VegetationDensityBrushError::Workspace(
                WorkspaceError::MissingTarget("vegetation layer")
            ))
        ));
    }

    #[test]
    fn resolved_density_patch_is_bound_to_the_exact_source_hash() {
        let source = source();
        let result = preview_vegetation_density_brush(&source, command(&source)).unwrap();
        let resolved = result.edit.receipt.command;
        let mut changed = source.clone();
        let grass = grass_layer(&source);
        let layer = changed.project.modules[0].areas[0]
            .vegetation_density
            .layers
            .iter_mut()
            .find(|layer| layer.layer_id == grass)
            .unwrap();
        layer.overrides = vec![VegetationDensityOverride::new(0, 0, 1)];
        changed.validate().unwrap();
        assert!(matches!(
            changed.preview_edit(resolved),
            Err(WorkspaceError::StaleRevision)
        ));
    }
}
