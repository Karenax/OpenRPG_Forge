//! Original Forge height brushes. Pure source commands, never renderer-owned edits.
use crate::{TerrainGenerationError, TerrainGenerationPlan};
use forge_core::{AreaId, CommandId, ModuleId, ProjectId, Revision};
use forge_project::{
    TerrainHeightDelta, TerrainMaterialRole, TerrainMaterialWeightOverride, WorkspaceChange,
    WorkspaceDocument, WorkspaceEdit, WorkspaceEditResult, WorkspaceError,
};
use serde::{Deserialize, Serialize};
use std::collections::BTreeMap;
use thiserror::Error;

/// Version 1: a circular stamp with smoothstep radial falloff on the 2 m source grid.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TerrainBrush {
    pub schema_version: u32,
    pub center_x_m: f32,
    pub center_z_m: f32,
    pub radius_m: f32,
    /// Fraction in ]0, 1]. Raise/noise amplitudes are metres; other modes blend to a target.
    pub strength: f32,
    pub operation: TerrainBrushOperation,
}

#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum TerrainBrushOperation {
    RaiseLower {
        delta_m: f32,
    },
    Smooth,
    Flatten {
        height_m: f32,
    },
    Noise {
        amplitude_m: f32,
        seed: u32,
    },
    /// A plane through (`center_x`, height, `center_z`); gradients are rise/run, not angles.
    Slope {
        height_m: f32,
        gradient_x: f32,
        gradient_z: f32,
    },
}

impl TerrainBrush {
    pub fn validate(self) -> Result<(), TerrainBrushError> {
        if self.schema_version != 1 {
            return Err(TerrainBrushError::UnsupportedVersion(self.schema_version));
        }
        if !bounded(self.center_x_m, -256.0, 256.0)
            || !bounded(self.center_z_m, -256.0, 256.0)
            || !bounded(self.radius_m, 2.0, 64.0)
            || !bounded(self.strength, f32::MIN_POSITIVE, 1.0)
        {
            return Err(TerrainBrushError::InvalidParameters);
        }
        let valid = match self.operation {
            TerrainBrushOperation::RaiseLower { delta_m } => {
                bounded(delta_m, -32.0, 32.0) && delta_m != 0.0
            }
            TerrainBrushOperation::Smooth => true,
            TerrainBrushOperation::Flatten { height_m } => bounded(height_m, -1024.0, 1024.0),
            TerrainBrushOperation::Noise { amplitude_m, .. } => {
                bounded(amplitude_m, f32::MIN_POSITIVE, 32.0)
            }
            TerrainBrushOperation::Slope {
                height_m,
                gradient_x,
                gradient_z,
            } => {
                bounded(height_m, -1024.0, 1024.0)
                    && bounded(gradient_x, -4.0, 4.0)
                    && bounded(gradient_z, -4.0, 4.0)
            }
        };
        if valid {
            Ok(())
        } else {
            Err(TerrainBrushError::InvalidParameters)
        }
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TerrainBrushCommand {
    pub command_id: CommandId,
    pub base_revision: Revision,
    pub project_id: ProjectId,
    pub module_id: ModuleId,
    pub area_id: AreaId,
    pub brush: TerrainBrush,
}

/// Exact candidate plus original intent, source diff, inverse and hashes. Safe to discard.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TerrainBrushResult {
    pub command: TerrainBrushCommand,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub additional_centers: Vec<[f32; 2]>,
    pub changed_samples: usize,
    pub edit: WorkspaceEditResult,
}

/// Version 1 material stamp. It blends one semantic layer into the sparse source grid.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TerrainMaterialBrush {
    pub schema_version: u32,
    pub center_x_m: f32,
    pub center_z_m: f32,
    pub radius_m: f32,
    /// Fraction in ]0, 1]. Repeated strokes progressively converge on the selected layer.
    pub strength: f32,
    pub material: TerrainMaterialRole,
}

impl TerrainMaterialBrush {
    pub fn validate(self) -> Result<(), TerrainBrushError> {
        if self.schema_version != 1 {
            return Err(TerrainBrushError::UnsupportedVersion(self.schema_version));
        }
        if !bounded(self.center_x_m, -256.0, 256.0)
            || !bounded(self.center_z_m, -256.0, 256.0)
            || !bounded(self.radius_m, 2.0, 64.0)
            || !bounded(self.strength, f32::MIN_POSITIVE, 1.0)
        {
            return Err(TerrainBrushError::InvalidParameters);
        }
        Ok(())
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TerrainMaterialBrushCommand {
    pub command_id: CommandId,
    pub base_revision: Revision,
    pub project_id: ProjectId,
    pub module_id: ModuleId,
    pub area_id: AreaId,
    pub brush: TerrainMaterialBrush,
}

/// Exact material-paint candidate plus intent, inverse and hashes. Safe to discard.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TerrainMaterialBrushResult {
    pub command: TerrainMaterialBrushCommand,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub additional_centers: Vec<[f32; 2]>,
    pub changed_samples: usize,
    pub edit: WorkspaceEditResult,
}

/// Shared by any application adapter (Studio, CLI or future MCP). Does not perform I/O.
pub fn preview_terrain_brush(
    workspace: &WorkspaceDocument,
    command: TerrainBrushCommand,
) -> Result<TerrainBrushResult, TerrainBrushError> {
    workspace.validate()?;
    if workspace.project.id != command.project_id {
        return Err(TerrainBrushError::WrongProject);
    }
    if workspace.revision != command.base_revision {
        return Err(WorkspaceError::StaleRevision.into());
    }
    command.brush.validate()?;
    let terrain = &workspace
        .area(command.module_id, command.area_id)
        .ok_or(WorkspaceError::MissingTarget("area"))?
        .terrain;
    let plan = TerrainGenerationPlan::from_document(terrain)?;
    let mut deltas: BTreeMap<_, _> = terrain
        .height_deltas
        .iter()
        .map(|sample| ((sample.sample_z, sample.sample_x), sample.delta_m))
        .collect();
    let mut changed_samples = 0;
    // The bounded radius visits at most 65 x 65 grid nodes, including clipped terrain edges.
    let (min_x, max_x) = grid_bounds(command.brush.center_x_m, command.brush.radius_m);
    let (min_z, max_z) = grid_bounds(command.brush.center_z_m, command.brush.radius_m);
    for sample_z in min_z..=max_z {
        for sample_x in min_x..=max_x {
            let x = -256.0 + f32::from(sample_x) * 2.0;
            let z = -256.0 + f32::from(sample_z) * 2.0;
            let dx = f64::from(x) - f64::from(command.brush.center_x_m);
            let dz = f64::from(z) - f64::from(command.brush.center_z_m);
            let radial = 1.0 - (dx * dx + dz * dz).sqrt() / f64::from(command.brush.radius_m);
            if radial <= 0.0 {
                continue;
            }
            let weight = radial * radial * (3.0 - 2.0 * radial) * f64::from(command.brush.strength);
            let before = plan.height_at(x, z)?;
            let difference =
                target_difference(&plan, command.brush.operation, x, z, dx, dz, before)? * weight;
            let target = f64::from(before) + difference;
            if !target.is_finite() || !(-1024.0..=1024.0).contains(&target) {
                return Err(TerrainBrushError::HeightOutOfBounds);
            }
            let old_delta = deltas.get(&(sample_z, sample_x)).copied().unwrap_or(0.0);
            #[allow(
                clippy::cast_possible_truncation,
                reason = "Bounded finite source heights and final document validation"
            )]
            let delta = (f64::from(old_delta) + difference) as f32;
            if delta.to_bits() == old_delta.to_bits() || (delta == 0.0 && old_delta == 0.0) {
                continue;
            }
            if delta == 0.0 {
                deltas.remove(&(sample_z, sample_x));
            } else {
                deltas.insert((sample_z, sample_x), delta);
            }
            changed_samples += 1;
        }
    }
    let edit = workspace.preview_edit(WorkspaceEdit {
        command_id: command.command_id,
        base_revision: command.base_revision,
        change: WorkspaceChange::SetTerrainHeightDeltas {
            module_id: command.module_id,
            area_id: command.area_id,
            expected_terrain_hash: plan.document_hash(),
            height_deltas: deltas
                .into_iter()
                .map(|((z, x), delta)| TerrainHeightDelta::new(x, z, delta))
                .collect(),
        },
    })?;
    // Reject derived arithmetic incompatibilities before an adapter can commit the source.
    let target = &edit
        .document
        .area(command.module_id, command.area_id)
        .ok_or(WorkspaceError::MissingTarget("area"))?
        .terrain;
    TerrainGenerationPlan::from_document(target)?;
    Ok(TerrainBrushResult {
        command,
        additional_centers: vec![],
        changed_samples,
        edit,
    })
}

/// A bounded stroke is one source revision and one undo operation. Intermediate stamps are pure
/// candidates only; they are never committed or retained as independent history entries.
pub fn preview_terrain_stroke(
    workspace: &WorkspaceDocument,
    command: TerrainBrushCommand,
    additional_centers: &[[f32; 2]],
) -> Result<TerrainBrushResult, TerrainBrushError> {
    if additional_centers.len() > 63 {
        return Err(TerrainBrushError::StrokeBudgetExceeded);
    }
    for center in additional_centers {
        TerrainBrush {
            center_x_m: center[0],
            center_z_m: center[1],
            ..command.brush
        }
        .validate()?;
    }
    let mut candidate = workspace.clone();
    for center in std::iter::once([command.brush.center_x_m, command.brush.center_z_m])
        .chain(additional_centers.iter().copied())
    {
        let stamp = TerrainBrushCommand {
            base_revision: candidate.revision,
            brush: TerrainBrush {
                center_x_m: center[0],
                center_z_m: center[1],
                ..command.brush
            },
            ..command
        };
        // The caller's original revision must be checked even if all stamps are no-ops.
        if workspace.revision != command.base_revision {
            return Err(WorkspaceError::StaleRevision.into());
        }
        match preview_terrain_brush(&candidate, stamp) {
            Ok(result) => {
                candidate = result.edit.document;
                reset_stroke_candidate_revision(
                    &mut candidate,
                    command.module_id,
                    command.area_id,
                    command.base_revision,
                    workspace
                        .area(command.module_id, command.area_id)
                        .ok_or(WorkspaceError::MissingTarget("area"))?
                        .terrain
                        .revision,
                )?;
            }
            Err(TerrainBrushError::Workspace(WorkspaceError::NoChange)) => {}
            Err(error) => return Err(error),
        }
    }
    let original = &workspace
        .area(command.module_id, command.area_id)
        .ok_or(WorkspaceError::MissingTarget("area"))?
        .terrain;
    let target = &candidate
        .area(command.module_id, command.area_id)
        .ok_or(WorkspaceError::MissingTarget("area"))?
        .terrain;
    let before: BTreeMap<_, _> = original
        .height_deltas
        .iter()
        .map(|d| (d.sort_key(), d.delta_m.to_bits()))
        .collect();
    let after: BTreeMap<_, _> = target
        .height_deltas
        .iter()
        .map(|d| (d.sort_key(), d.delta_m.to_bits()))
        .collect();
    let changed_samples = before
        .keys()
        .chain(after.keys())
        .collect::<std::collections::BTreeSet<_>>()
        .into_iter()
        .filter(|key| before.get(key) != after.get(key))
        .count();
    let edit = workspace.preview_edit(WorkspaceEdit {
        command_id: command.command_id,
        base_revision: command.base_revision,
        change: WorkspaceChange::SetTerrainHeightDeltas {
            module_id: command.module_id,
            area_id: command.area_id,
            expected_terrain_hash: original.content_hash().map_err(WorkspaceError::from)?,
            height_deltas: target.height_deltas.clone(),
        },
    })?;
    Ok(TerrainBrushResult {
        command,
        additional_centers: additional_centers.to_vec(),
        changed_samples,
        edit,
    })
}

/// Previews one material stamp without mutating a workspace or renderer cache.
pub fn preview_terrain_material_brush(
    workspace: &WorkspaceDocument,
    command: TerrainMaterialBrushCommand,
) -> Result<TerrainMaterialBrushResult, TerrainBrushError> {
    workspace.validate()?;
    if workspace.project.id != command.project_id {
        return Err(TerrainBrushError::WrongProject);
    }
    if workspace.revision != command.base_revision {
        return Err(WorkspaceError::StaleRevision.into());
    }
    command.brush.validate()?;
    let terrain = &workspace
        .area(command.module_id, command.area_id)
        .ok_or(WorkspaceError::MissingTarget("area"))?
        .terrain;
    let plan = TerrainGenerationPlan::from_document(terrain)?;
    let mut weights: BTreeMap<_, _> = terrain
        .material_weight_overrides
        .iter()
        .map(|sample| (sample.sort_key(), sample.weights))
        .collect();
    let mut changed_samples = 0;
    let (min_x, max_x) = grid_bounds(command.brush.center_x_m, command.brush.radius_m);
    let (min_z, max_z) = grid_bounds(command.brush.center_z_m, command.brush.radius_m);
    let selected = command.brush.material.index();
    for sample_z in min_z..=max_z {
        for sample_x in min_x..=max_x {
            let x = -256.0 + f32::from(sample_x) * 2.0;
            let z = -256.0 + f32::from(sample_z) * 2.0;
            let dx = f64::from(x) - f64::from(command.brush.center_x_m);
            let dz = f64::from(z) - f64::from(command.brush.center_z_m);
            let radial = 1.0 - (dx * dx + dz * dz).sqrt() / f64::from(command.brush.radius_m);
            if radial <= 0.0 {
                continue;
            }
            let blend = radial * radial * (3.0 - 2.0 * radial) * f64::from(command.brush.strength);
            let before = plan
                .material_weights_at_sample(sample_x, sample_z)?
                .as_array();
            let mut after = [0.0; 3];
            for index in 0..3 {
                let target = if index == selected { 1.0 } else { 0.0 };
                #[allow(
                    clippy::cast_possible_truncation,
                    reason = "The convex blend remains finite and in the normalized unit interval"
                )]
                let blended = (f64::from(before[index]) * (1.0 - blend) + target * blend) as f32;
                after[index] = blended;
            }
            let total = after.iter().sum::<f32>();
            for weight in &mut after {
                *weight /= total;
            }
            if after.map(f32::to_bits) == before.map(f32::to_bits) {
                continue;
            }
            weights.insert((sample_z, sample_x), after);
            changed_samples += 1;
        }
    }
    let edit = workspace.preview_edit(WorkspaceEdit {
        command_id: command.command_id,
        base_revision: command.base_revision,
        change: WorkspaceChange::SetTerrainMaterialWeights {
            module_id: command.module_id,
            area_id: command.area_id,
            expected_terrain_hash: plan.document_hash(),
            material_weight_overrides: weights
                .into_iter()
                .map(|((z, x), weights)| TerrainMaterialWeightOverride::new(x, z, weights))
                .collect(),
        },
    })?;
    let target = &edit
        .document
        .area(command.module_id, command.area_id)
        .ok_or(WorkspaceError::MissingTarget("area"))?
        .terrain;
    TerrainGenerationPlan::from_document(target)?;
    Ok(TerrainMaterialBrushResult {
        command,
        additional_centers: vec![],
        changed_samples,
        edit,
    })
}

/// A material stroke is one source revision and one undo operation, like height sculpting.
pub fn preview_terrain_material_stroke(
    workspace: &WorkspaceDocument,
    command: TerrainMaterialBrushCommand,
    additional_centers: &[[f32; 2]],
) -> Result<TerrainMaterialBrushResult, TerrainBrushError> {
    if additional_centers.len() > 63 {
        return Err(TerrainBrushError::StrokeBudgetExceeded);
    }
    for center in additional_centers {
        TerrainMaterialBrush {
            center_x_m: center[0],
            center_z_m: center[1],
            ..command.brush
        }
        .validate()?;
    }
    let mut candidate = workspace.clone();
    for center in std::iter::once([command.brush.center_x_m, command.brush.center_z_m])
        .chain(additional_centers.iter().copied())
    {
        let stamp = TerrainMaterialBrushCommand {
            base_revision: candidate.revision,
            brush: TerrainMaterialBrush {
                center_x_m: center[0],
                center_z_m: center[1],
                ..command.brush
            },
            ..command
        };
        if workspace.revision != command.base_revision {
            return Err(WorkspaceError::StaleRevision.into());
        }
        match preview_terrain_material_brush(&candidate, stamp) {
            Ok(result) => {
                candidate = result.edit.document;
                reset_stroke_candidate_revision(
                    &mut candidate,
                    command.module_id,
                    command.area_id,
                    command.base_revision,
                    workspace
                        .area(command.module_id, command.area_id)
                        .ok_or(WorkspaceError::MissingTarget("area"))?
                        .terrain
                        .revision,
                )?;
            }
            Err(TerrainBrushError::Workspace(WorkspaceError::NoChange)) => {}
            Err(error) => return Err(error),
        }
    }
    let original = &workspace
        .area(command.module_id, command.area_id)
        .ok_or(WorkspaceError::MissingTarget("area"))?
        .terrain;
    let target = &candidate
        .area(command.module_id, command.area_id)
        .ok_or(WorkspaceError::MissingTarget("area"))?
        .terrain;
    let before: BTreeMap<_, _> = original
        .material_weight_overrides
        .iter()
        .map(|sample| (sample.sort_key(), sample.weights.map(f32::to_bits)))
        .collect();
    let after: BTreeMap<_, _> = target
        .material_weight_overrides
        .iter()
        .map(|sample| (sample.sort_key(), sample.weights.map(f32::to_bits)))
        .collect();
    let changed_samples = before
        .keys()
        .chain(after.keys())
        .collect::<std::collections::BTreeSet<_>>()
        .into_iter()
        .filter(|key| before.get(key) != after.get(key))
        .count();
    let edit = workspace.preview_edit(WorkspaceEdit {
        command_id: command.command_id,
        base_revision: command.base_revision,
        change: WorkspaceChange::SetTerrainMaterialWeights {
            module_id: command.module_id,
            area_id: command.area_id,
            expected_terrain_hash: original.content_hash().map_err(WorkspaceError::from)?,
            material_weight_overrides: target.material_weight_overrides.clone(),
        },
    })?;
    Ok(TerrainMaterialBrushResult {
        command,
        additional_centers: additional_centers.to_vec(),
        changed_samples,
        edit,
    })
}

fn reset_stroke_candidate_revision(
    candidate: &mut WorkspaceDocument,
    module_id: ModuleId,
    area_id: AreaId,
    workspace_revision: Revision,
    terrain_revision: Revision,
) -> Result<(), TerrainBrushError> {
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
        .terrain
        .revision = terrain_revision;
    Ok(())
}

fn target_difference(
    plan: &TerrainGenerationPlan,
    operation: TerrainBrushOperation,
    x: f32,
    z: f32,
    dx: f64,
    dz: f64,
    before: f32,
) -> Result<f64, TerrainGenerationError> {
    Ok(match operation {
        TerrainBrushOperation::RaiseLower { delta_m } => f64::from(delta_m),
        TerrainBrushOperation::Flatten { height_m } => f64::from(height_m) - f64::from(before),
        TerrainBrushOperation::Slope {
            height_m,
            gradient_x,
            gradient_z,
        } => {
            f64::from(height_m) + dx * f64::from(gradient_x) + dz * f64::from(gradient_z)
                - f64::from(before)
        }
        TerrainBrushOperation::Noise { amplitude_m, seed } => {
            f64::from(amplitude_m) * stamp_noise(x, z, seed)
        }
        TerrainBrushOperation::Smooth => {
            let mut total = 0.0;
            let mut count = 0.0;
            for offset_z in [-2.0, 0.0, 2.0] {
                for offset_x in [-2.0, 0.0, 2.0] {
                    if (-256.0..=256.0).contains(&(x + offset_x))
                        && (-256.0..=256.0).contains(&(z + offset_z))
                    {
                        total += f64::from(plan.height_at(x + offset_x, z + offset_z)?);
                        count += 1.0;
                    }
                }
            }
            total / count - f64::from(before)
        }
    })
}

#[allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    reason = "Grid bounds are clamped to 0..256"
)]
fn grid_bounds(center: f32, radius: f32) -> (u16, u16) {
    (
        (center - radius).midpoint(256.0).ceil().clamp(0.0, 256.0) as u16,
        (center + radius).midpoint(256.0).floor().clamp(0.0, 256.0) as u16,
    )
}

fn bounded(value: f32, min: f32, max: f32) -> bool {
    value.is_finite() && (min..=max).contains(&value)
}

#[allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    reason = "Coordinates are exact grid nodes in 0..256"
)]
fn stamp_noise(x: f32, z: f32, seed: u32) -> f64 {
    let grid_x = x.midpoint(256.0) as u64;
    let grid_z = z.midpoint(256.0) as u64;
    let mut value = u64::from(seed)
        ^ grid_x.wrapping_mul(0x9e37_79b9_7f4a_7c15)
        ^ grid_z.wrapping_mul(0xbf58_476d_1ce4_e5b9);
    value = (value ^ (value >> 30)).wrapping_mul(0xbf58_476d_1ce4_e5b9);
    value = (value ^ (value >> 27)).wrapping_mul(0x94d0_49bb_1331_11eb);
    value ^= value >> 31;
    // The upper 32 bits convert exactly, including on WASM. No platform random generator.
    f64::from((value >> 32) as u32) / f64::from(u32::MAX) * 2.0 - 1.0
}

#[derive(Debug, Error)]
pub enum TerrainBrushError {
    #[error("terrain stroke is limited to 64 stamps; use a shorter stroke")]
    StrokeBudgetExceeded,
    #[error("unsupported terrain brush version {0}")]
    UnsupportedVersion(u32),
    #[error(
        "brush requires finite coordinates in ±256 m, radius 2..64 m, strength ]0,1] and bounded operation parameters"
    )]
    InvalidParameters,
    #[error("terrain brush belongs to another project")]
    WrongProject,
    #[error("terrain brush would exceed the supported height range ±1024 m")]
    HeightOutOfBounds,
    #[error("invalid terrain brush source or result: {0}")]
    Workspace(#[from] WorkspaceError),
    #[error("terrain brush generation: {0}")]
    Generation(#[from] TerrainGenerationError),
}

#[cfg(test)]
#[path = "terrain_brush_tests.rs"]
mod tests;
