//! Pure deterministic generation and revision-bound editing of authored paths.

use crate::{TERRAIN_HALF_EXTENT_M, TerrainGenerationError, TerrainGenerationPlan};
use forge_core::{
    AreaId, CanonicalJsonError, CommandId, ContentHash, ModuleId, ProjectId, Revision, Vec3,
    canonical_json_sha256,
};
use forge_project::{
    AuthoredPath, PathControlPoint, PathDocument, PathDocumentError, PathId, PathInterpolation,
    TerrainDocument, WorkspaceArea, WorkspaceChange, WorkspaceDocument, WorkspaceEdit,
    WorkspaceEditResult, WorkspaceError,
};
use serde::{Deserialize, Serialize};
use thiserror::Error;

pub const PATH_SAMPLE_SPACING_M: f32 = 2.0;
pub const MIN_PATH_TRACE_POINTS: usize = 2;
pub const MAX_PATH_TRACE_POINTS: usize = 64;
pub const MIN_PATH_TRACE_POINT_DISTANCE_M: f32 = 0.5;

/// One terrain-projected sample of an authored path.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PathSample {
    pub position_m: Vec3,
    pub source_segment_index: u16,
    pub segment_fraction: f32,
}

/// One complete derived path. It is cache/output data, never canonical source.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct GeneratedPath {
    pub id: PathId,
    pub name: String,
    pub closed: bool,
    pub control_points: Vec<Vec3>,
    pub samples: Vec<PathSample>,
}

/// Complete deterministic path projection for one area.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct GeneratedPaths {
    pub terrain_document_hash: ContentHash,
    pub path_document_hash: ContentHash,
    pub paths: Vec<GeneratedPath>,
}

impl GeneratedPaths {
    /// Hashes the complete derived projection for deterministic replay evidence.
    pub fn content_hash(&self) -> Result<ContentHash, CanonicalJsonError> {
        canonical_json_sha256(self)
    }
}

/// Validated interpretation of one terrain/path source pair.
#[derive(Clone, Debug)]
pub struct PathGenerationPlan {
    terrain: TerrainGenerationPlan,
    paths: PathDocument,
    path_document_hash: ContentHash,
}

impl PathGenerationPlan {
    pub fn from_documents(
        terrain: &TerrainDocument,
        paths: &PathDocument,
    ) -> Result<Self, PathGenerationError> {
        paths.validate()?;
        let terrain = TerrainGenerationPlan::from_document(terrain)?;
        if terrain.document().project_id != paths.project_id {
            return Err(PathGenerationError::ProjectIdentityMismatch);
        }
        if terrain.document().area_id != paths.area_id {
            return Err(PathGenerationError::AreaIdentityMismatch);
        }
        Ok(Self {
            terrain,
            path_document_hash: paths.content_hash()?,
            paths: paths.clone(),
        })
    }

    #[must_use]
    pub const fn terrain(&self) -> &TerrainGenerationPlan {
        &self.terrain
    }

    #[must_use]
    pub const fn paths(&self) -> &PathDocument {
        &self.paths
    }

    pub fn generate(&self) -> Result<GeneratedPaths, PathGenerationError> {
        let paths = self
            .paths
            .paths
            .iter()
            .map(|path| self.generate_authored_path(path))
            .collect::<Result<Vec<_>, _>>()?;
        Ok(GeneratedPaths {
            terrain_document_hash: self.terrain.document_hash(),
            path_document_hash: self.path_document_hash,
            paths,
        })
    }

    pub fn generate_path(&self, path_id: PathId) -> Result<GeneratedPath, PathGenerationError> {
        let path = self
            .paths
            .paths
            .iter()
            .find(|path| path.id == path_id)
            .ok_or(PathGenerationError::MissingPath(path_id))?;
        self.generate_authored_path(path)
    }

    fn generate_authored_path(
        &self,
        path: &AuthoredPath,
    ) -> Result<GeneratedPath, PathGenerationError> {
        let is_two_point_line = match path.interpolation {
            PathInterpolation::CentripetalCatmullRomV1 => path.control_points.len() == 2,
        };
        let mut samples = Vec::new();
        let first = path.control_points[0].position_m;
        samples.push(PathSample {
            position_m: self.project_to_terrain(first.x, first.z)?,
            source_segment_index: 0,
            segment_fraction: 0.0,
        });

        let segment_count = if path.closed {
            path.control_points.len()
        } else {
            path.control_points.len() - 1
        };
        for segment_index in 0..segment_count {
            let start = path.control_points[segment_index].position_m;
            let end =
                path.control_points[(segment_index + 1) % path.control_points.len()].position_m;
            let delta_x = end.x - start.x;
            let delta_z = end.z - start.z;
            #[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
            let interval_count = (delta_x.hypot(delta_z) / PATH_SAMPLE_SPACING_M).ceil() as u32;

            for interval_index in 1..=interval_count {
                #[allow(clippy::cast_precision_loss)]
                let segment_fraction = interval_index as f32 / interval_count as f32;
                let position_m = if interval_index == interval_count {
                    self.project_to_terrain(end.x, end.z)?
                } else if is_two_point_line {
                    self.project_to_terrain(
                        linear_interpolate(start.x, end.x, segment_fraction),
                        linear_interpolate(start.z, end.z, segment_fraction),
                    )?
                } else {
                    let previous = if path.closed {
                        path.control_points[(segment_index + path.control_points.len() - 1)
                            % path.control_points.len()]
                        .position_m
                    } else if segment_index == 0 {
                        extrapolate_before(start, end)
                    } else {
                        path.control_points[segment_index - 1].position_m
                    };
                    let next = if path.closed {
                        path.control_points[(segment_index + 2) % path.control_points.len()]
                            .position_m
                    } else if segment_index + 2 == path.control_points.len() {
                        extrapolate_after(start, end)
                    } else {
                        path.control_points[segment_index + 2].position_m
                    };
                    let curved = centripetal_catmull_rom_plan_view(
                        previous,
                        start,
                        end,
                        next,
                        segment_fraction,
                    );
                    self.project_to_terrain(curved[0], curved[1])?
                };
                samples.push(PathSample {
                    position_m,
                    #[allow(clippy::cast_possible_truncation)]
                    source_segment_index: segment_index as u16,
                    segment_fraction,
                });
            }
        }

        Ok(GeneratedPath {
            id: path.id,
            name: path.name.clone(),
            closed: path.closed,
            control_points: path
                .control_points
                .iter()
                .map(|point| point.position_m)
                .collect(),
            samples,
        })
    }

    fn project_to_terrain(&self, x_m: f32, z_m: f32) -> Result<Vec3, PathGenerationError> {
        if !inside_path_extent(x_m, z_m) {
            return Err(PathGenerationError::CurveSampleOutOfBounds { x_m, z_m });
        }
        let surface = self.terrain.surface_at(x_m, z_m)?;
        Ok(Vec3::new(x_m, surface.height_m, z_m))
    }
}

/// Revision-bound request to create one open path from a bounded plan-view trace.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PathCreateCommand {
    pub command_id: CommandId,
    pub base_revision: Revision,
    pub project_id: ProjectId,
    pub module_id: ModuleId,
    pub area_id: AreaId,
    pub path_id: PathId,
    pub name: String,
    pub trace_xz_m: Vec<[f32; 2]>,
}

/// Revision-bound request to replace only one path's ordered control points.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PathRedrawCommand {
    pub command_id: CommandId,
    pub base_revision: Revision,
    pub project_id: ProjectId,
    pub module_id: ModuleId,
    pub area_id: AreaId,
    pub path_id: PathId,
    pub trace_xz_m: Vec<[f32; 2]>,
}

/// Candidate workspace edit which creates exactly one open authored path.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PathCreateResult {
    pub command: PathCreateCommand,
    pub path: AuthoredPath,
    pub edit: WorkspaceEditResult,
}

/// Candidate workspace edit which redraws only an existing path's controls.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PathRedrawResult {
    pub command: PathRedrawCommand,
    pub previous_control_points: Vec<PathControlPoint>,
    pub path: AuthoredPath,
    pub edit: WorkspaceEditResult,
}

/// Produces one validated create revision without mutating the workspace or a renderer cache.
pub fn preview_create_path(
    workspace: &WorkspaceDocument,
    command: PathCreateCommand,
) -> Result<PathCreateResult, PathCommandError> {
    let area = validate_command_context(
        workspace,
        command.base_revision,
        command.project_id,
        command.module_id,
        command.area_id,
    )?;
    if area
        .paths
        .paths
        .iter()
        .any(|path| path.id == command.path_id)
    {
        return Err(PathCommandError::PathAlreadyExists(command.path_id));
    }
    let control_points = terrain_snap_trace(&area.terrain, &command.trace_xz_m)?;
    let path = AuthoredPath {
        id: command.path_id,
        name: command.name.clone(),
        closed: false,
        interpolation: PathInterpolation::CentripetalCatmullRomV1,
        control_points,
    };
    let edit = workspace.preview_edit(WorkspaceEdit {
        command_id: command.command_id,
        base_revision: command.base_revision,
        change: WorkspaceChange::UpsertPath {
            module_id: command.module_id,
            area_id: command.area_id,
            expected_path_document_hash: area.paths.content_hash().map_err(WorkspaceError::from)?,
            path: path.clone(),
        },
    })?;
    validate_candidate_paths(&edit, command.module_id, command.area_id)?;
    Ok(PathCreateResult {
        command,
        path,
        edit,
    })
}

/// Produces one validated redraw revision while preserving all non-control path fields.
pub fn preview_redraw_path(
    workspace: &WorkspaceDocument,
    command: PathRedrawCommand,
) -> Result<PathRedrawResult, PathCommandError> {
    let area = validate_command_context(
        workspace,
        command.base_revision,
        command.project_id,
        command.module_id,
        command.area_id,
    )?;
    let previous = area
        .paths
        .paths
        .iter()
        .find(|path| path.id == command.path_id)
        .ok_or(WorkspaceError::MissingTarget("path"))?;
    let previous_control_points = previous.control_points.clone();
    let mut path = previous.clone();
    path.control_points = terrain_snap_trace(&area.terrain, &command.trace_xz_m)?;
    let edit = workspace.preview_edit(WorkspaceEdit {
        command_id: command.command_id,
        base_revision: command.base_revision,
        change: WorkspaceChange::UpsertPath {
            module_id: command.module_id,
            area_id: command.area_id,
            expected_path_document_hash: area.paths.content_hash().map_err(WorkspaceError::from)?,
            path: path.clone(),
        },
    })?;
    validate_candidate_paths(&edit, command.module_id, command.area_id)?;
    Ok(PathRedrawResult {
        command,
        previous_control_points,
        path,
        edit,
    })
}

fn validate_candidate_paths(
    edit: &WorkspaceEditResult,
    module_id: ModuleId,
    area_id: AreaId,
) -> Result<(), PathCommandError> {
    let area = edit
        .document
        .area(module_id, area_id)
        .ok_or(WorkspaceError::MissingTarget("area"))?;
    PathGenerationPlan::from_documents(&area.terrain, &area.paths)?.generate()?;
    crate::road::validate_candidate_roads(edit, module_id, area_id)?;
    crate::river::validate_candidate_rivers(edit, module_id, area_id)?;
    Ok(())
}

fn validate_command_context(
    workspace: &WorkspaceDocument,
    base_revision: Revision,
    project_id: ProjectId,
    module_id: ModuleId,
    area_id: AreaId,
) -> Result<&WorkspaceArea, PathCommandError> {
    workspace.validate()?;
    if workspace.project.id != project_id {
        return Err(PathCommandError::WrongProject);
    }
    if workspace.revision != base_revision {
        return Err(WorkspaceError::StaleRevision.into());
    }
    workspace
        .area(module_id, area_id)
        .ok_or_else(|| WorkspaceError::MissingTarget("area").into())
}

fn terrain_snap_trace(
    terrain: &TerrainDocument,
    trace_xz_m: &[[f32; 2]],
) -> Result<Vec<PathControlPoint>, PathCommandError> {
    validate_trace(trace_xz_m)?;
    let terrain = TerrainGenerationPlan::from_document(terrain)?;
    trace_xz_m
        .iter()
        .map(|[x_m, z_m]| {
            let surface = terrain.surface_at(*x_m, *z_m)?;
            Ok(PathControlPoint::new(Vec3::new(
                *x_m,
                surface.height_m,
                *z_m,
            )))
        })
        .collect()
}

fn validate_trace(trace_xz_m: &[[f32; 2]]) -> Result<(), PathCommandError> {
    if !(MIN_PATH_TRACE_POINTS..=MAX_PATH_TRACE_POINTS).contains(&trace_xz_m.len()) {
        return Err(PathCommandError::InvalidTracePointCount(trace_xz_m.len()));
    }
    for (index, [x_m, z_m]) in trace_xz_m.iter().copied().enumerate() {
        if !inside_path_extent(x_m, z_m) {
            return Err(PathCommandError::InvalidTracePoint { index });
        }
    }
    for (index, points) in trace_xz_m.windows(2).enumerate() {
        let delta_x = f64::from(points[1][0]) - f64::from(points[0][0]);
        let delta_z = f64::from(points[1][1]) - f64::from(points[0][1]);
        if delta_x * delta_x + delta_z * delta_z
            < f64::from(MIN_PATH_TRACE_POINT_DISTANCE_M).powi(2)
        {
            return Err(PathCommandError::TracePointsTooClose {
                first_index: index,
                second_index: index + 1,
            });
        }
    }
    Ok(())
}

fn inside_path_extent(x_m: f32, z_m: f32) -> bool {
    x_m.is_finite()
        && z_m.is_finite()
        && (-TERRAIN_HALF_EXTENT_M..TERRAIN_HALF_EXTENT_M).contains(&x_m)
        && (-TERRAIN_HALF_EXTENT_M..TERRAIN_HALF_EXTENT_M).contains(&z_m)
}

fn extrapolate_before(start: Vec3, end: Vec3) -> Vec3 {
    Vec3::new(2.0 * start.x - end.x, 0.0, 2.0 * start.z - end.z)
}

fn extrapolate_after(start: Vec3, end: Vec3) -> Vec3 {
    Vec3::new(2.0 * end.x - start.x, 0.0, 2.0 * end.z - start.z)
}

fn centripetal_catmull_rom_plan_view(
    point_0: Vec3,
    point_1: Vec3,
    point_2: Vec3,
    point_3: Vec3,
    segment_fraction: f32,
) -> [f32; 2] {
    let point_0 = Vec3::new(point_0.x, 0.0, point_0.z);
    let point_1 = Vec3::new(point_1.x, 0.0, point_1.z);
    let point_2 = Vec3::new(point_2.x, 0.0, point_2.z);
    let point_3 = Vec3::new(point_3.x, 0.0, point_3.z);
    let time_0 = 0.0;
    let time_1 = centripetal_knot(time_0, point_0, point_1);
    let time_2 = centripetal_knot(time_1, point_1, point_2);
    let time_3 = centripetal_knot(time_2, point_2, point_3);
    let time = linear_interpolate(time_1, time_2, segment_fraction);

    let first = interpolate_at_time(point_0, point_1, time_0, time_1, time);
    let second = interpolate_at_time(point_1, point_2, time_1, time_2, time);
    let third = interpolate_at_time(point_2, point_3, time_2, time_3, time);
    let left = interpolate_at_time(first, second, time_0, time_2, time);
    let right = interpolate_at_time(second, third, time_1, time_3, time);
    let result = interpolate_at_time(left, right, time_1, time_2, time);
    [result.x, result.z]
}

fn centripetal_knot(previous_time: f32, first: Vec3, second: Vec3) -> f32 {
    let delta_x = second.x - first.x;
    let delta_z = second.z - first.z;
    let squared_distance = delta_x * delta_x + delta_z * delta_z;
    previous_time + squared_distance.sqrt().sqrt()
}

fn interpolate_at_time(
    first: Vec3,
    second: Vec3,
    first_time: f32,
    second_time: f32,
    time: f32,
) -> Vec3 {
    let fraction = (time - first_time) / (second_time - first_time);
    Vec3::new(
        linear_interpolate(first.x, second.x, fraction),
        0.0,
        linear_interpolate(first.z, second.z, fraction),
    )
}

fn linear_interpolate(first: f32, second: f32, fraction: f32) -> f32 {
    first + (second - first) * fraction
}

#[derive(Debug, Error)]
pub enum PathGenerationError {
    #[error("path document is invalid: {0}")]
    InvalidPathDocument(#[from] PathDocumentError),
    #[error("path generation terrain is invalid: {0}")]
    Terrain(#[from] TerrainGenerationError),
    #[error("terrain and path documents belong to different projects")]
    ProjectIdentityMismatch,
    #[error("terrain and path documents belong to different areas")]
    AreaIdentityMismatch,
    #[error("authored path {0} does not exist")]
    MissingPath(PathId),
    #[error("derived path curve sample ({x_m}, {z_m}) is outside [-256,256) metres")]
    CurveSampleOutOfBounds { x_m: f32, z_m: f32 },
}

#[derive(Debug, Error)]
pub enum PathCommandError {
    #[error("path command belongs to another project")]
    WrongProject,
    #[error(
        "path trace contains {0} points; expected between {MIN_PATH_TRACE_POINTS} and {MAX_PATH_TRACE_POINTS}"
    )]
    InvalidTracePointCount(usize),
    #[error("path trace point {index} must be finite and inside [-256,256) metres")]
    InvalidTracePoint { index: usize },
    #[error(
        "path trace points {first_index} and {second_index} must be at least {MIN_PATH_TRACE_POINT_DISTANCE_M} metres apart"
    )]
    TracePointsTooClose {
        first_index: usize,
        second_index: usize,
    },
    #[error("path {0} already exists")]
    PathAlreadyExists(PathId),
    #[error("workspace path edit failed: {0}")]
    Workspace(#[from] WorkspaceError),
    #[error("path terrain projection failed: {0}")]
    Terrain(#[from] TerrainGenerationError),
    #[error("generated path candidate is invalid: {0}")]
    Generation(#[from] PathGenerationError),
    #[error("road projection affected by the path edit is invalid: {0}")]
    RoadGeneration(#[from] crate::road::RoadGenerationError),
    #[error("river projection affected by the path edit is invalid: {0}")]
    RiverGeneration(#[from] crate::river::RiverGenerationError),
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_project::{NewProjectCommand, WorkspaceError};

    fn project_id() -> ProjectId {
        ProjectId::deterministic("path-world-test/project")
    }

    fn module_id() -> ModuleId {
        ModuleId::deterministic("path-world-test/module")
    }

    fn area_id() -> AreaId {
        AreaId::deterministic("path-world-test/area")
    }

    fn workspace() -> WorkspaceDocument {
        WorkspaceDocument::create(NewProjectCommand {
            command_id: CommandId::deterministic("path-world-test/create-workspace"),
            project_id: project_id(),
            module_id: module_id(),
            area_id: area_id(),
            project_name: "Projet chemins".to_owned(),
            module_name: "Module chemins".to_owned(),
            area_name: "Zone chemins".to_owned(),
            seed: 0x21_21,
            spawn: Vec3::ZERO,
        })
        .unwrap()
        .document
    }

    fn path_id() -> PathId {
        PathId::deterministic("path-world-test/path")
    }

    fn curved_trace() -> Vec<[f32; 2]> {
        vec![[-12.0, -8.0], [-2.0, 9.0], [11.0, 4.0], [18.0, -10.0]]
    }

    fn overshoot_trace() -> Vec<[f32; 2]> {
        vec![
            [-255.0, -255.0],
            [255.0, -85.0],
            [255.0, 85.0],
            [-255.0, 255.0],
        ]
    }

    fn create_command(
        workspace: &WorkspaceDocument,
        path_id: PathId,
        trace_xz_m: Vec<[f32; 2]>,
    ) -> PathCreateCommand {
        PathCreateCommand {
            command_id: CommandId::deterministic(format!("path-world-test/create/{path_id}")),
            base_revision: workspace.revision,
            project_id: project_id(),
            module_id: module_id(),
            area_id: area_id(),
            path_id,
            name: "Chemin du coteau".to_owned(),
            trace_xz_m,
        }
    }

    fn redraw_command(
        workspace: &WorkspaceDocument,
        path_id: PathId,
        trace_xz_m: Vec<[f32; 2]>,
    ) -> PathRedrawCommand {
        PathRedrawCommand {
            command_id: CommandId::deterministic(format!("path-world-test/redraw/{path_id}")),
            base_revision: workspace.revision,
            project_id: project_id(),
            module_id: module_id(),
            area_id: area_id(),
            path_id,
            trace_xz_m,
        }
    }

    fn workspace_with_path(trace_xz_m: Vec<[f32; 2]>) -> WorkspaceDocument {
        let workspace = workspace();
        preview_create_path(
            &workspace,
            create_command(&workspace, path_id(), trace_xz_m),
        )
        .unwrap()
        .edit
        .document
    }

    #[test]
    fn two_point_path_is_a_terrain_projected_line_with_exact_endpoints() {
        let workspace = workspace_with_path(vec![[-4.0, 0.0], [4.0, 0.0]]);
        let area = workspace.area(module_id(), area_id()).unwrap();
        let generated = PathGenerationPlan::from_documents(&area.terrain, &area.paths)
            .unwrap()
            .generate()
            .unwrap();
        let path = &generated.paths[0];

        assert_eq!(path.samples.len(), 5);
        assert_eq!(
            path.samples
                .iter()
                .map(|sample| sample.position_m.x)
                .collect::<Vec<_>>(),
            vec![-4.0, -2.0, 0.0, 2.0, 4.0]
        );
        assert_eq!(
            path.samples.first().unwrap().position_m,
            path.control_points[0]
        );
        assert_eq!(
            path.samples.last().unwrap().position_m,
            path.control_points[1]
        );
        assert!(
            path.samples
                .iter()
                .all(|sample| sample.source_segment_index == 0)
        );
    }

    #[test]
    fn curved_generation_replays_hashes_and_tracks_the_exact_terrain_surface() {
        let workspace = workspace_with_path(curved_trace());
        let area = workspace.area(module_id(), area_id()).unwrap();
        let plan = PathGenerationPlan::from_documents(&area.terrain, &area.paths).unwrap();
        let first = plan.generate().unwrap();
        let replay = PathGenerationPlan::from_documents(&area.terrain, &area.paths)
            .unwrap()
            .generate()
            .unwrap();
        let generated_path = &first.paths[0];

        assert_eq!(first, replay);
        assert_eq!(
            first.content_hash().unwrap(),
            replay.content_hash().unwrap()
        );
        assert_eq!(
            first.terrain_document_hash,
            area.terrain.content_hash().unwrap()
        );
        assert_eq!(first.path_document_hash, area.paths.content_hash().unwrap());
        assert_eq!(plan.generate_path(path_id()).unwrap(), *generated_path);
        assert_eq!(
            generated_path.samples.first().unwrap().position_m,
            generated_path.control_points[0]
        );
        assert_eq!(
            generated_path.samples.last().unwrap().position_m,
            *generated_path.control_points.last().unwrap()
        );
        for sample in &generated_path.samples {
            let surface = plan
                .terrain()
                .surface_at(sample.position_m.x, sample.position_m.z)
                .unwrap();
            assert_eq!(sample.position_m.y.to_bits(), surface.height_m.to_bits());
        }
        assert_eq!(
            first.content_hash().unwrap().to_string(),
            "2ed0df0fb5f050022c84bde5817962b7b71c87a1ed5d88659e672e68d1d9112a"
        );
    }

    #[test]
    fn create_is_open_snapped_canonical_and_exactly_one_revision() {
        let workspace = workspace();
        let before_hash = workspace.content_hash().unwrap();
        let command = create_command(&workspace, path_id(), curved_trace());
        let first = preview_create_path(&workspace, command.clone()).unwrap();
        let replay = preview_create_path(&workspace, command).unwrap();

        assert_eq!(first, replay);
        assert_eq!(workspace.content_hash().unwrap(), before_hash);
        assert!(!first.path.closed);
        assert_eq!(
            first.path.interpolation,
            PathInterpolation::CentripetalCatmullRomV1
        );
        assert_eq!(
            first.edit.receipt.new_revision,
            workspace.revision.next().unwrap()
        );
        assert_eq!(
            first.edit.document.revision,
            first.edit.receipt.new_revision
        );
        let area = first.edit.document.area(module_id(), area_id()).unwrap();
        assert_eq!(area.paths.revision, first.edit.receipt.new_revision);
        assert_eq!(area.paths.paths, vec![first.path.clone()]);
        assert_eq!(*first.edit.receipt.inverse_snapshot, workspace);
        let terrain = TerrainGenerationPlan::from_document(&area.terrain).unwrap();
        for (source, control) in curved_trace().iter().zip(&first.path.control_points) {
            assert_eq!(control.position_m.x.to_bits(), source[0].to_bits());
            assert_eq!(control.position_m.z.to_bits(), source[1].to_bits());
            assert_eq!(
                control.position_m.y.to_bits(),
                terrain
                    .surface_at(source[0], source[1])
                    .unwrap()
                    .height_m
                    .to_bits()
            );
        }
    }

    #[test]
    fn redraw_preserves_identity_metadata_closed_state_and_interpolation() {
        let mut workspace = workspace_with_path(curved_trace());
        workspace.project.modules[0].areas[0].paths.paths[0].closed = true;
        workspace.validate().unwrap();
        let previous = workspace.project.modules[0].areas[0].paths.paths[0].clone();
        let new_trace = vec![[-20.0, -3.0], [-7.0, 13.0], [9.0, 15.0], [24.0, 1.0]];
        let result = preview_redraw_path(
            &workspace,
            redraw_command(&workspace, path_id(), new_trace.clone()),
        )
        .unwrap();

        assert_eq!(result.previous_control_points, previous.control_points);
        assert_eq!(result.path.id, previous.id);
        assert_eq!(result.path.name, previous.name);
        assert_eq!(result.path.closed, previous.closed);
        assert_eq!(result.path.interpolation, previous.interpolation);
        assert_ne!(result.path.control_points, previous.control_points);
        assert_eq!(
            result.edit.receipt.new_revision,
            workspace.revision.next().unwrap()
        );
        let updated = &result.edit.document.project.modules[0].areas[0].paths.paths[0];
        assert_eq!(updated, &result.path);
        assert_eq!(
            updated
                .control_points
                .iter()
                .map(|point| [point.position_m.x, point.position_m.z])
                .collect::<Vec<_>>(),
            new_trace
        );
        let area = result.edit.document.area(module_id(), area_id()).unwrap();
        let generated = PathGenerationPlan::from_documents(&area.terrain, &area.paths)
            .unwrap()
            .generate()
            .unwrap();
        assert_eq!(
            generated.paths[0].samples.last().unwrap().position_m,
            generated.paths[0].samples[0].position_m
        );
        assert_eq!(
            usize::from(
                generated.paths[0]
                    .samples
                    .last()
                    .unwrap()
                    .source_segment_index
            ),
            updated.control_points.len() - 1
        );
    }

    #[test]
    fn identical_redraw_is_a_fail_closed_noop() {
        let workspace = workspace_with_path(curved_trace());
        let before_hash = workspace.content_hash().unwrap();
        let error = preview_redraw_path(
            &workspace,
            redraw_command(&workspace, path_id(), curved_trace()),
        )
        .unwrap_err();

        assert!(matches!(
            error,
            PathCommandError::Workspace(WorkspaceError::NoChange)
        ));
        assert_eq!(workspace.content_hash().unwrap(), before_hash);
    }

    #[test]
    fn create_and_redraw_reject_curve_overshoot_without_exposing_an_edit() {
        let empty = workspace();
        let empty_hash = empty.content_hash().unwrap();
        assert!(matches!(
            preview_create_path(&empty, create_command(&empty, path_id(), overshoot_trace())),
            Err(PathCommandError::Generation(
                PathGenerationError::CurveSampleOutOfBounds { .. }
            ))
        ));
        assert!(
            empty
                .area(module_id(), area_id())
                .unwrap()
                .paths
                .paths
                .is_empty()
        );
        assert_eq!(empty.content_hash().unwrap(), empty_hash);

        let existing = workspace_with_path(curved_trace());
        let existing_hash = existing.content_hash().unwrap();
        let previous = existing.area(module_id(), area_id()).unwrap().paths.paths[0].clone();
        assert!(matches!(
            preview_redraw_path(
                &existing,
                redraw_command(&existing, path_id(), overshoot_trace())
            ),
            Err(PathCommandError::Generation(
                PathGenerationError::CurveSampleOutOfBounds { .. }
            ))
        ));
        assert_eq!(
            existing.area(module_id(), area_id()).unwrap().paths.paths[0],
            previous
        );
        assert_eq!(existing.content_hash().unwrap(), existing_hash);
    }

    #[test]
    fn trace_shape_and_command_context_rejections_are_atomic() {
        let workspace = workspace();
        let before_hash = workspace.content_hash().unwrap();

        for (trace, expected) in [
            (vec![[0.0, 0.0]], "count"),
            (
                (0_u8..=u8::try_from(MAX_PATH_TRACE_POINTS).unwrap())
                    .map(|index| [f32::from(index), 0.0])
                    .collect(),
                "count",
            ),
            (vec![[0.0, 0.0], [f32::NAN, 1.0]], "point"),
            (vec![[0.0, 0.0], [256.0, 1.0]], "point"),
            (vec![[0.0, 0.0], [0.25, 0.0]], "distance"),
        ] {
            let error = preview_create_path(
                &workspace,
                create_command(&workspace, PathId::deterministic(expected), trace),
            )
            .unwrap_err();
            match expected {
                "count" => assert!(matches!(error, PathCommandError::InvalidTracePointCount(_))),
                "point" => assert!(matches!(error, PathCommandError::InvalidTracePoint { .. })),
                "distance" => {
                    assert!(matches!(
                        error,
                        PathCommandError::TracePointsTooClose { .. }
                    ));
                }
                _ => unreachable!(),
            }
        }

        let mut wrong_project = create_command(&workspace, path_id(), curved_trace());
        wrong_project.project_id = ProjectId::deterministic("other-project");
        assert!(matches!(
            preview_create_path(&workspace, wrong_project),
            Err(PathCommandError::WrongProject)
        ));

        let mut stale = create_command(&workspace, path_id(), curved_trace());
        stale.base_revision = workspace.revision.next().unwrap();
        assert!(matches!(
            preview_create_path(&workspace, stale),
            Err(PathCommandError::Workspace(WorkspaceError::StaleRevision))
        ));

        let mut missing_area = create_command(&workspace, path_id(), curved_trace());
        missing_area.area_id = AreaId::deterministic("missing-area");
        assert!(matches!(
            preview_create_path(&workspace, missing_area),
            Err(PathCommandError::Workspace(WorkspaceError::MissingTarget(
                "area"
            )))
        ));

        let mut missing_module = create_command(&workspace, path_id(), curved_trace());
        missing_module.module_id = ModuleId::deterministic("missing-module");
        assert!(matches!(
            preview_create_path(&workspace, missing_module),
            Err(PathCommandError::Workspace(WorkspaceError::MissingTarget(
                "area"
            )))
        ));
        assert_eq!(workspace.content_hash().unwrap(), before_hash);
    }

    #[test]
    fn create_and_redraw_reject_existing_or_missing_paths() {
        let workspace = workspace_with_path(curved_trace());
        assert!(matches!(
            preview_create_path(
                &workspace,
                create_command(&workspace, path_id(), vec![[-2.0, 0.0], [2.0, 0.0]])
            ),
            Err(PathCommandError::PathAlreadyExists(id)) if id == path_id()
        ));

        let missing = PathId::deterministic("missing-path");
        assert!(matches!(
            preview_redraw_path(
                &workspace,
                redraw_command(&workspace, missing, vec![[-2.0, 0.0], [2.0, 0.0]])
            ),
            Err(PathCommandError::Workspace(WorkspaceError::MissingTarget(
                "path"
            )))
        ));
    }

    #[test]
    fn generation_rejects_identity_mismatch_missing_path_and_curve_overshoot() {
        let workspace = workspace_with_path(curved_trace());
        let area = workspace.area(module_id(), area_id()).unwrap();
        let mut wrong_project = area.paths.clone();
        wrong_project.project_id = ProjectId::deterministic("wrong-path-project");
        assert!(matches!(
            PathGenerationPlan::from_documents(&area.terrain, &wrong_project),
            Err(PathGenerationError::ProjectIdentityMismatch)
        ));
        let mut wrong_area = area.paths.clone();
        wrong_area.area_id = AreaId::deterministic("wrong-path-area");
        assert!(matches!(
            PathGenerationPlan::from_documents(&area.terrain, &wrong_area),
            Err(PathGenerationError::AreaIdentityMismatch)
        ));
        let plan = PathGenerationPlan::from_documents(&area.terrain, &area.paths).unwrap();
        assert!(matches!(
            plan.generate_path(PathId::deterministic("missing")),
            Err(PathGenerationError::MissingPath(_))
        ));

        let terrain = TerrainGenerationPlan::from_document(&area.terrain).unwrap();
        let points = overshoot_trace()
            .into_iter()
            .map(|[x_m, z_m]| {
                PathControlPoint::new(Vec3::new(
                    x_m,
                    terrain.surface_at(x_m, z_m).unwrap().height_m,
                    z_m,
                ))
            })
            .collect();
        let overshoot = PathDocument::new(
            project_id(),
            area_id(),
            area.paths.revision,
            "Courbe hors zone",
            vec![AuthoredPath {
                id: PathId::deterministic("overshoot"),
                name: "Courbe hors zone".to_owned(),
                closed: false,
                interpolation: PathInterpolation::CentripetalCatmullRomV1,
                control_points: points,
            }],
        );
        overshoot.validate().unwrap();
        assert!(matches!(
            PathGenerationPlan::from_documents(&area.terrain, &overshoot)
                .unwrap()
                .generate(),
            Err(PathGenerationError::CurveSampleOutOfBounds { .. })
        ));
    }

    #[test]
    fn invalid_name_is_delegated_to_the_canonical_source_validator() {
        let workspace = workspace();
        let mut command = create_command(&workspace, path_id(), curved_trace());
        command.name = "\n".to_owned();
        assert!(matches!(
            preview_create_path(&workspace, command),
            Err(PathCommandError::Workspace(WorkspaceError::Path(_)))
        ));
    }
}
