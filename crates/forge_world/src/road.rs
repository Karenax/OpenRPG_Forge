//! Deterministic terrain-draped road meshes and revision-bound road profile edits.

use crate::{
    GeneratedPath, PathGenerationError, PathGenerationPlan, TERRAIN_HALF_EXTENT_M,
    TerrainGenerationError, TerrainGenerationPlan,
};
use forge_core::{
    AreaId, CanonicalJsonError, CommandId, ContentHash, ModuleId, ProjectId, Revision,
    canonical_json_sha256,
};
use forge_project::{
    AuthoredRoad, PathDocument, PathId, RoadDocument, RoadDocumentError, RoadProfile,
    RoadProfileAlgorithm, RoadSurface, TerrainDocument, WorkspaceArea, WorkspaceChange,
    WorkspaceDocument, WorkspaceEdit, WorkspaceEditResult, WorkspaceError,
};
use serde::{Deserialize, Serialize};
use thiserror::Error;

/// Version of the complete derived road geometry contract, including clearance and winding.
pub const ROAD_GENERATION_VERSION: u32 = 1;
/// Fixed renderer-independent lift above every sampled terrain point.
pub const ROAD_MESH_CLEARANCE_M: f32 = 0.04;
/// Maximum horizontal chord between adjacent derived centerline rows. This is not an arc bound.
pub const ROAD_MESH_MAX_LONGITUDINAL_CHORD_M: f32 = 2.0;
/// Left edge, crowned centre and right edge.
pub const ROAD_CROSS_SECTION_VERTEX_COUNT: usize = 3;
/// Aggregate safety budget across every road in one generated area.
pub const MAX_GENERATED_ROAD_LONGITUDINAL_SAMPLES: usize = 16_384;
pub const MAX_GENERATED_ROAD_VERTICES: usize =
    MAX_GENERATED_ROAD_LONGITUDINAL_SAMPLES * ROAD_CROSS_SECTION_VERTEX_COUNT;
pub const MAX_GENERATED_ROAD_INDICES: usize = MAX_GENERATED_ROAD_LONGITUDINAL_SAMPLES * 12;

const ROAD_INDICES_PER_LONGITUDINAL_INTERVAL: usize = 12;
const ROAD_RESAMPLE_TARGET_CHORD_M: f64 = 1.99;
const MIN_HORIZONTAL_DELTA_SQUARED: f64 = 1.0e-12;
const MIN_TRIANGLE_NORMAL_SQUARED: f64 = 1.0e-18;

/// One engine-neutral road mesh. Positions are absolute Forge world metres.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct GeneratedRoad {
    pub path_id: PathId,
    pub profile: RoadProfile,
    pub surface: RoadSurface,
    pub positions_m: Vec<[f32; 3]>,
    pub normals: Vec<[f32; 3]>,
    /// U spans left-to-right from 0 to 1; V is horizontal distance in metres.
    pub uvs: Vec<[f32; 2]>,
    /// Four counter-clockwise triangles per longitudinal interval.
    pub indices: Vec<u32>,
}

impl GeneratedRoad {
    #[must_use]
    pub fn vertex_count(&self) -> usize {
        self.positions_m.len()
    }

    #[must_use]
    pub fn triangle_count(&self) -> usize {
        self.indices.len() / 3
    }

    #[must_use]
    pub fn longitudinal_sample_count(&self) -> usize {
        self.positions_m.len() / ROAD_CROSS_SECTION_VERTEX_COUNT
    }
}

/// Complete deterministic road projection for one terrain, path and road source triple.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct GeneratedRoads {
    pub generation_version: u32,
    pub terrain_document_hash: ContentHash,
    pub path_document_hash: ContentHash,
    pub road_document_hash: ContentHash,
    /// Strictly sorted by `path_id`.
    pub roads: Vec<GeneratedRoad>,
}

impl GeneratedRoads {
    #[must_use]
    pub fn vertex_count(&self) -> usize {
        self.roads.iter().map(GeneratedRoad::vertex_count).sum()
    }

    #[must_use]
    pub fn triangle_count(&self) -> usize {
        self.roads.iter().map(GeneratedRoad::triangle_count).sum()
    }

    pub fn content_hash(&self) -> Result<ContentHash, CanonicalJsonError> {
        canonical_json_sha256(self)
    }
}

/// Validated interpretation of one terrain/path/road source triple.
#[derive(Clone, Debug)]
pub struct RoadGenerationPlan {
    paths: PathGenerationPlan,
    roads: RoadDocument,
    path_document_hash: ContentHash,
    road_document_hash: ContentHash,
}

impl RoadGenerationPlan {
    pub fn from_documents(
        terrain: &TerrainDocument,
        paths: &PathDocument,
        roads: &RoadDocument,
    ) -> Result<Self, RoadGenerationError> {
        roads.validate()?;
        let path_document_hash = paths.content_hash().map_err(PathGenerationError::from)?;
        let paths = PathGenerationPlan::from_documents(terrain, paths)?;
        if terrain.project_id != roads.project_id {
            return Err(RoadGenerationError::ProjectIdentityMismatch);
        }
        if terrain.area_id != roads.area_id {
            return Err(RoadGenerationError::AreaIdentityMismatch);
        }
        for road in &roads.roads {
            let path = paths
                .paths()
                .paths
                .binary_search_by_key(&road.path_id, |path| path.id)
                .ok()
                .map(|index| &paths.paths().paths[index])
                .ok_or(RoadGenerationError::MissingPath(road.path_id))?;
            if path.closed {
                return Err(RoadGenerationError::ClosedPath(road.path_id));
            }
        }
        Ok(Self {
            paths,
            path_document_hash,
            road_document_hash: roads.content_hash()?,
            roads: roads.clone(),
        })
    }

    #[must_use]
    pub const fn terrain(&self) -> &TerrainGenerationPlan {
        self.paths.terrain()
    }

    #[must_use]
    pub const fn paths(&self) -> &PathGenerationPlan {
        &self.paths
    }

    #[must_use]
    pub const fn roads(&self) -> &RoadDocument {
        &self.roads
    }

    pub fn generate(&self) -> Result<GeneratedRoads, RoadGenerationError> {
        let mut remaining_samples = MAX_GENERATED_ROAD_LONGITUDINAL_SAMPLES;
        let mut generated = Vec::with_capacity(self.roads.roads.len());
        for road in &self.roads.roads {
            let (road, used_samples) = self.generate_authored_road(*road, remaining_samples)?;
            remaining_samples = remaining_samples
                .checked_sub(used_samples)
                .ok_or(RoadGenerationError::ArithmeticOverflow)?;
            generated.push(road);
        }
        Ok(GeneratedRoads {
            generation_version: ROAD_GENERATION_VERSION,
            terrain_document_hash: self.paths.terrain().document_hash(),
            path_document_hash: self.path_document_hash,
            road_document_hash: self.road_document_hash,
            roads: generated,
        })
    }

    pub fn generate_road(&self, path_id: PathId) -> Result<GeneratedRoad, RoadGenerationError> {
        let road = self
            .roads
            .roads
            .binary_search_by_key(&path_id, |road| road.path_id)
            .ok()
            .map(|index| self.roads.roads[index])
            .ok_or(RoadGenerationError::MissingRoad(path_id))?;
        self.generate_authored_road(road, MAX_GENERATED_ROAD_LONGITUDINAL_SAMPLES)
            .map(|(road, _)| road)
    }

    fn generate_authored_road(
        &self,
        road: AuthoredRoad,
        remaining_samples: usize,
    ) -> Result<(GeneratedRoad, usize), RoadGenerationError> {
        match road.profile.algorithm {
            RoadProfileAlgorithm::TerrainDrapedCrownV1 => {}
        }
        match road.surface {
            RoadSurface::CompactedEarthV1 => {}
        }
        let generated_path = self.paths.generate_path(road.path_id)?;
        let centerline = adaptive_centerline(&generated_path, remaining_samples)?;
        let sample_count = centerline.len();
        let (vertex_count, index_count) = checked_mesh_counts(sample_count)?;
        let mut positions_m = Vec::with_capacity(vertex_count);
        let mut uvs = Vec::with_capacity(vertex_count);
        let half_width_m = f64::from(road.profile.width_m) * 0.5;

        for (row_index, row) in centerline.iter().enumerate() {
            let tangent = tangent_at(&centerline, row_index, road.path_id)?;
            let inverse_length = (tangent[0] * tangent[0] + tangent[1] * tangent[1])
                .sqrt()
                .recip();
            let right = [tangent[1] * inverse_length, -tangent[0] * inverse_length];
            let left_xz = [
                row.xz_m[0] - right[0] * half_width_m,
                row.xz_m[1] - right[1] * half_width_m,
            ];
            let right_xz = [
                row.xz_m[0] + right[0] * half_width_m,
                row.xz_m[1] + right[1] * half_width_m,
            ];
            let left = self.road_vertex(road.path_id, row_index, 0, left_xz, 0.0)?;
            let centre = self.road_vertex(
                road.path_id,
                row_index,
                1,
                row.xz_m,
                road.profile.crown_height_m,
            )?;
            let right = self.road_vertex(road.path_id, row_index, 2, right_xz, 0.0)?;
            positions_m.extend([left, centre, right]);
            #[allow(clippy::cast_possible_truncation)]
            let distance_m = row.distance_m as f32;
            if !distance_m.is_finite() {
                return Err(RoadGenerationError::InvalidDerivedValue {
                    path_id: road.path_id,
                    field: "uv distance",
                });
            }
            uvs.extend([[0.0, distance_m], [0.5, distance_m], [1.0, distance_m]]);
        }

        let indices = build_indices(sample_count, index_count)?;
        let normals = build_normals(road.path_id, &positions_m, &indices)?;
        Ok((
            GeneratedRoad {
                path_id: road.path_id,
                profile: road.profile,
                surface: road.surface,
                positions_m,
                normals,
                uvs,
                indices,
            },
            sample_count,
        ))
    }

    fn road_vertex(
        &self,
        path_id: PathId,
        row_index: usize,
        cross_section_index: usize,
        position_xz_m: [f64; 2],
        crown_height_m: f32,
    ) -> Result<[f32; 3], RoadGenerationError> {
        if position_xz_m.iter().any(|coordinate| {
            !coordinate.is_finite()
                || *coordinate < -f64::from(TERRAIN_HALF_EXTENT_M)
                || *coordinate >= f64::from(TERRAIN_HALF_EXTENT_M)
        }) {
            return Err(RoadGenerationError::FootprintOutOfBounds {
                path_id,
                row_index,
                cross_section_index,
            });
        }
        #[allow(clippy::cast_possible_truncation)]
        let horizontal_m = [position_xz_m[0] as f32, position_xz_m[1] as f32];
        let surface = self
            .paths
            .terrain()
            .surface_at(horizontal_m[0], horizontal_m[1])
            .map_err(|error| RoadGenerationError::FootprintTerrain {
                path_id,
                row_index,
                cross_section_index,
                source: error,
            })?;
        let y_m = surface.height_m + ROAD_MESH_CLEARANCE_M + crown_height_m;
        if horizontal_m
            .iter()
            .any(|coordinate| !coordinate.is_finite())
            || !y_m.is_finite()
        {
            return Err(RoadGenerationError::InvalidDerivedValue {
                path_id,
                field: "road position",
            });
        }
        Ok([horizontal_m[0], y_m, horizontal_m[1]])
    }
}

/// Revision-bound request to create or replace one road profile.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct RoadUpsertCommand {
    pub command_id: CommandId,
    pub base_revision: Revision,
    pub project_id: ProjectId,
    pub module_id: ModuleId,
    pub area_id: AreaId,
    pub road: AuthoredRoad,
}

/// Candidate workspace edit which changes exactly one road assignment.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct RoadUpsertResult {
    pub command: RoadUpsertCommand,
    pub previous_road: Option<AuthoredRoad>,
    pub road: AuthoredRoad,
    pub edit: WorkspaceEditResult,
}

/// Produces and fully generates one candidate without mutating the supplied workspace.
pub fn preview_upsert_road(
    workspace: &WorkspaceDocument,
    command: RoadUpsertCommand,
) -> Result<RoadUpsertResult, RoadCommandError> {
    let area = validate_command_context(
        workspace,
        command.base_revision,
        command.project_id,
        command.module_id,
        command.area_id,
    )?;
    command.road.validate()?;
    let previous_road = area
        .roads
        .roads
        .binary_search_by_key(&command.road.path_id, |road| road.path_id)
        .ok()
        .map(|index| area.roads.roads[index]);
    let edit = workspace.preview_edit(WorkspaceEdit {
        command_id: command.command_id,
        base_revision: command.base_revision,
        change: WorkspaceChange::UpsertRoad {
            module_id: command.module_id,
            area_id: command.area_id,
            expected_path_document_hash: area.paths.content_hash().map_err(WorkspaceError::from)?,
            expected_road_document_hash: area.roads.content_hash().map_err(WorkspaceError::from)?,
            road: command.road,
        },
    })?;
    validate_candidate_roads(&edit, command.module_id, command.area_id)?;
    Ok(RoadUpsertResult {
        road: command.road,
        command,
        previous_road,
        edit,
    })
}

pub(crate) fn validate_candidate_roads(
    edit: &WorkspaceEditResult,
    module_id: ModuleId,
    area_id: AreaId,
) -> Result<(), RoadGenerationError> {
    let area = edit
        .document
        .area(module_id, area_id)
        .ok_or(RoadGenerationError::MissingArea)?;
    RoadGenerationPlan::from_documents(&area.terrain, &area.paths, &area.roads)?.generate()?;
    Ok(())
}

fn validate_command_context(
    workspace: &WorkspaceDocument,
    base_revision: Revision,
    project_id: ProjectId,
    module_id: ModuleId,
    area_id: AreaId,
) -> Result<&WorkspaceArea, RoadCommandError> {
    workspace.validate()?;
    if workspace.project.id != project_id {
        return Err(RoadCommandError::WrongProject);
    }
    if workspace.revision != base_revision {
        return Err(WorkspaceError::StaleRevision.into());
    }
    workspace
        .area(module_id, area_id)
        .ok_or_else(|| WorkspaceError::MissingTarget("area").into())
}

#[derive(Clone, Copy)]
struct CenterlineRow {
    xz_m: [f64; 2],
    distance_m: f64,
}

fn adaptive_centerline(
    path: &GeneratedPath,
    maximum_samples: usize,
) -> Result<Vec<CenterlineRow>, RoadGenerationError> {
    if maximum_samples < 2 || path.samples.len() < 2 {
        return Err(RoadGenerationError::LongitudinalSampleBudgetExceeded {
            requested: path.samples.len().max(2),
            maximum: maximum_samples,
        });
    }
    let first = path.samples[0].position_m;
    let mut rows = Vec::with_capacity(path.samples.len().min(maximum_samples));
    rows.push(CenterlineRow {
        xz_m: [f64::from(first.x), f64::from(first.z)],
        distance_m: 0.0,
    });
    let mut total_distance_m = 0.0_f64;
    for (source_index, pair) in path.samples.windows(2).enumerate() {
        let first = pair[0].position_m;
        let second = pair[1].position_m;
        let delta_x = f64::from(second.x) - f64::from(first.x);
        let delta_z = f64::from(second.z) - f64::from(first.z);
        let squared_distance = delta_x * delta_x + delta_z * delta_z;
        if !squared_distance.is_finite() || squared_distance <= MIN_HORIZONTAL_DELTA_SQUARED {
            return Err(RoadGenerationError::InvalidTangent {
                path_id: path.id,
                sample_index: source_index,
            });
        }
        let distance_m = squared_distance.sqrt();
        let required_sections = (distance_m / ROAD_RESAMPLE_TARGET_CHORD_M).ceil().max(1.0);
        let maximum_samples_u32 =
            u32::try_from(maximum_samples).map_err(|_| RoadGenerationError::ArithmeticOverflow)?;
        if !required_sections.is_finite() || required_sections > f64::from(maximum_samples_u32) {
            return Err(RoadGenerationError::LongitudinalSampleBudgetExceeded {
                requested: maximum_samples.saturating_add(1),
                maximum: maximum_samples,
            });
        }
        #[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
        let subdivisions = usize::try_from(required_sections as u32)
            .map_err(|_| RoadGenerationError::ArithmeticOverflow)?;
        let requested = rows
            .len()
            .checked_add(subdivisions)
            .ok_or(RoadGenerationError::ArithmeticOverflow)?;
        if requested > maximum_samples {
            return Err(RoadGenerationError::LongitudinalSampleBudgetExceeded {
                requested,
                maximum: maximum_samples,
            });
        }
        for subdivision in 1..=subdivisions {
            let xz_m = if subdivision == subdivisions {
                [f64::from(second.x), f64::from(second.z)]
            } else {
                #[allow(clippy::cast_precision_loss)]
                let fraction = subdivision as f64 / subdivisions as f64;
                [
                    f64::from(first.x) + delta_x * fraction,
                    f64::from(first.z) + delta_z * fraction,
                ]
            };
            let previous = rows.last().ok_or(RoadGenerationError::ArithmeticOverflow)?;
            let step_x = xz_m[0] - previous.xz_m[0];
            let step_z = xz_m[1] - previous.xz_m[1];
            let step_distance_m = step_x.hypot(step_z);
            if !step_distance_m.is_finite()
                || step_distance_m <= MIN_HORIZONTAL_DELTA_SQUARED.sqrt()
                || step_distance_m > f64::from(ROAD_MESH_MAX_LONGITUDINAL_CHORD_M)
            {
                return Err(RoadGenerationError::InvalidTangent {
                    path_id: path.id,
                    sample_index: source_index,
                });
            }
            total_distance_m += step_distance_m;
            if !total_distance_m.is_finite() {
                return Err(RoadGenerationError::InvalidDerivedValue {
                    path_id: path.id,
                    field: "longitudinal distance",
                });
            }
            rows.push(CenterlineRow {
                xz_m,
                distance_m: total_distance_m,
            });
        }
    }
    Ok(rows)
}

fn tangent_at(
    rows: &[CenterlineRow],
    index: usize,
    path_id: PathId,
) -> Result<[f64; 2], RoadGenerationError> {
    let last = rows
        .len()
        .checked_sub(1)
        .ok_or(RoadGenerationError::ArithmeticOverflow)?;
    let current = rows
        .get(index)
        .ok_or(RoadGenerationError::ArithmeticOverflow)?;
    let candidates = if index == 0 {
        [Some((rows[1], *current)), None, None]
    } else if index == last {
        [Some((*current, rows[last - 1])), None, None]
    } else {
        [
            Some((rows[index + 1], rows[index - 1])),
            Some((rows[index + 1], *current)),
            Some((*current, rows[index - 1])),
        ]
    };
    for (second, first) in candidates.into_iter().flatten() {
        let tangent = [
            second.xz_m[0] - first.xz_m[0],
            second.xz_m[1] - first.xz_m[1],
        ];
        let squared_length = tangent[0] * tangent[0] + tangent[1] * tangent[1];
        if squared_length.is_finite() && squared_length > MIN_HORIZONTAL_DELTA_SQUARED {
            return Ok(tangent);
        }
    }
    Err(RoadGenerationError::InvalidTangent {
        path_id,
        sample_index: index,
    })
}

fn checked_mesh_counts(sample_count: usize) -> Result<(usize, usize), RoadGenerationError> {
    if sample_count > MAX_GENERATED_ROAD_LONGITUDINAL_SAMPLES {
        return Err(RoadGenerationError::LongitudinalSampleBudgetExceeded {
            requested: sample_count,
            maximum: MAX_GENERATED_ROAD_LONGITUDINAL_SAMPLES,
        });
    }
    let vertex_count = sample_count
        .checked_mul(ROAD_CROSS_SECTION_VERTEX_COUNT)
        .ok_or(RoadGenerationError::ArithmeticOverflow)?;
    let interval_count = sample_count
        .checked_sub(1)
        .ok_or(RoadGenerationError::ArithmeticOverflow)?;
    let index_count = interval_count
        .checked_mul(ROAD_INDICES_PER_LONGITUDINAL_INTERVAL)
        .ok_or(RoadGenerationError::ArithmeticOverflow)?;
    if vertex_count > MAX_GENERATED_ROAD_VERTICES || index_count > MAX_GENERATED_ROAD_INDICES {
        return Err(RoadGenerationError::MeshBudgetExceeded {
            vertices: vertex_count,
            indices: index_count,
        });
    }
    Ok((vertex_count, index_count))
}

fn build_indices(
    sample_count: usize,
    expected_count: usize,
) -> Result<Vec<u32>, RoadGenerationError> {
    let mut indices = Vec::with_capacity(expected_count);
    let interval_count = sample_count
        .checked_sub(1)
        .ok_or(RoadGenerationError::ArithmeticOverflow)?;
    for row in 0..interval_count {
        let current = row
            .checked_mul(ROAD_CROSS_SECTION_VERTEX_COUNT)
            .ok_or(RoadGenerationError::ArithmeticOverflow)?;
        let next = current
            .checked_add(ROAD_CROSS_SECTION_VERTEX_COUNT)
            .ok_or(RoadGenerationError::ArithmeticOverflow)?;
        let current_left = checked_mesh_index(current, 0)?;
        let current_centre = checked_mesh_index(current, 1)?;
        let current_right = checked_mesh_index(current, 2)?;
        let next_left = checked_mesh_index(next, 0)?;
        let next_centre = checked_mesh_index(next, 1)?;
        let next_right = checked_mesh_index(next, 2)?;
        indices.extend([
            current_left,
            next_left,
            current_centre,
            current_centre,
            next_left,
            next_centre,
            current_centre,
            next_centre,
            current_right,
            current_right,
            next_centre,
            next_right,
        ]);
    }
    if indices.len() != expected_count {
        return Err(RoadGenerationError::IndexCountMismatch {
            expected: expected_count,
            generated: indices.len(),
        });
    }
    Ok(indices)
}

fn checked_mesh_index(base: usize, offset: usize) -> Result<u32, RoadGenerationError> {
    let index = base
        .checked_add(offset)
        .ok_or(RoadGenerationError::ArithmeticOverflow)?;
    u32::try_from(index).map_err(|_| RoadGenerationError::IndexOverflow)
}

fn build_normals(
    path_id: PathId,
    positions_m: &[[f32; 3]],
    indices: &[u32],
) -> Result<Vec<[f32; 3]>, RoadGenerationError> {
    let mut accumulated = vec![[0.0_f64; 3]; positions_m.len()];
    let (triangles, remainder) = indices.as_chunks::<3>();
    if !remainder.is_empty() {
        return Err(RoadGenerationError::InvalidIndexCount(indices.len()));
    }
    for (triangle_index, triangle) in triangles.iter().enumerate() {
        let indices = [
            usize::try_from(triangle[0]).map_err(|_| RoadGenerationError::IndexOverflow)?,
            usize::try_from(triangle[1]).map_err(|_| RoadGenerationError::IndexOverflow)?,
            usize::try_from(triangle[2]).map_err(|_| RoadGenerationError::IndexOverflow)?,
        ];
        let points = indices.map(|index| positions_m.get(index).copied());
        let [Some(first), Some(second), Some(third)] = points else {
            return Err(RoadGenerationError::IndexOverflow);
        };
        let first_edge = [
            f64::from(second[0]) - f64::from(first[0]),
            f64::from(second[1]) - f64::from(first[1]),
            f64::from(second[2]) - f64::from(first[2]),
        ];
        let second_edge = [
            f64::from(third[0]) - f64::from(first[0]),
            f64::from(third[1]) - f64::from(first[1]),
            f64::from(third[2]) - f64::from(first[2]),
        ];
        let cross = [
            first_edge[1] * second_edge[2] - first_edge[2] * second_edge[1],
            first_edge[2] * second_edge[0] - first_edge[0] * second_edge[2],
            first_edge[0] * second_edge[1] - first_edge[1] * second_edge[0],
        ];
        let squared_length = cross[0] * cross[0] + cross[1] * cross[1] + cross[2] * cross[2];
        if cross.iter().any(|value| !value.is_finite())
            || cross[1] <= 0.0
            || squared_length <= MIN_TRIANGLE_NORMAL_SQUARED
        {
            return Err(RoadGenerationError::InvalidTriangle {
                path_id,
                triangle_index,
            });
        }
        for index in indices {
            for axis in 0..3 {
                accumulated[index][axis] += cross[axis];
            }
        }
    }

    accumulated
        .into_iter()
        .enumerate()
        .map(|(vertex_index, normal)| {
            let squared_length =
                normal[0] * normal[0] + normal[1] * normal[1] + normal[2] * normal[2];
            if normal.iter().any(|value| !value.is_finite())
                || squared_length <= MIN_TRIANGLE_NORMAL_SQUARED
            {
                return Err(RoadGenerationError::InvalidVertexNormal {
                    path_id,
                    vertex_index,
                });
            }
            let inverse_length = squared_length.sqrt().recip();
            #[allow(clippy::cast_possible_truncation)]
            let normalized = [
                (normal[0] * inverse_length) as f32,
                (normal[1] * inverse_length) as f32,
                (normal[2] * inverse_length) as f32,
            ];
            if normalized.iter().any(|value| !value.is_finite()) || normalized[1] <= 0.0 {
                return Err(RoadGenerationError::InvalidVertexNormal {
                    path_id,
                    vertex_index,
                });
            }
            Ok(normalized)
        })
        .collect()
}

#[derive(Debug, Error)]
pub enum RoadGenerationError {
    #[error("road document is invalid: {0}")]
    InvalidRoadDocument(#[from] RoadDocumentError),
    #[error("road path generation failed: {0}")]
    Path(#[from] PathGenerationError),
    #[error("terrain and road documents belong to different projects")]
    ProjectIdentityMismatch,
    #[error("terrain and road documents belong to different areas")]
    AreaIdentityMismatch,
    #[error("road references missing path {0}")]
    MissingPath(PathId),
    #[error("road path {0} must be open")]
    ClosedPath(PathId),
    #[error("road profile for path {0} does not exist")]
    MissingRoad(PathId),
    #[error("road candidate area does not exist")]
    MissingArea,
    #[error("road path {path_id} has an invalid tangent at sample {sample_index}")]
    InvalidTangent {
        path_id: PathId,
        sample_index: usize,
    },
    #[error(
        "road path {path_id} footprint row {row_index}, point {cross_section_index} is outside [-256,256) metres"
    )]
    FootprintOutOfBounds {
        path_id: PathId,
        row_index: usize,
        cross_section_index: usize,
    },
    #[error(
        "road path {path_id} footprint row {row_index}, point {cross_section_index} cannot sample terrain: {source}"
    )]
    FootprintTerrain {
        path_id: PathId,
        row_index: usize,
        cross_section_index: usize,
        source: TerrainGenerationError,
    },
    #[error("road path {path_id} derived {field} is not finite")]
    InvalidDerivedValue {
        path_id: PathId,
        field: &'static str,
    },
    #[error("road projection requests {requested} longitudinal samples; maximum is {maximum}")]
    LongitudinalSampleBudgetExceeded { requested: usize, maximum: usize },
    #[error("road mesh requests {vertices} vertices and {indices} indices")]
    MeshBudgetExceeded { vertices: usize, indices: usize },
    #[error("road mesh arithmetic overflow")]
    ArithmeticOverflow,
    #[error("road mesh index cannot be represented")]
    IndexOverflow,
    #[error("road mesh index count {0} is not divisible by three")]
    InvalidIndexCount(usize),
    #[error("road mesh expected {expected} indices but generated {generated}")]
    IndexCountMismatch { expected: usize, generated: usize },
    #[error("road path {path_id} generated invalid triangle {triangle_index}")]
    InvalidTriangle {
        path_id: PathId,
        triangle_index: usize,
    },
    #[error("road path {path_id} generated invalid normal at vertex {vertex_index}")]
    InvalidVertexNormal {
        path_id: PathId,
        vertex_index: usize,
    },
}

#[derive(Debug, Error)]
pub enum RoadCommandError {
    #[error("road command belongs to another project")]
    WrongProject,
    #[error("road profile is invalid: {0}")]
    InvalidRoad(#[from] RoadDocumentError),
    #[error("workspace road edit failed: {0}")]
    Workspace(#[from] WorkspaceError),
    #[error("generated road candidate is invalid: {0}")]
    Generation(#[from] RoadGenerationError),
}

#[cfg(test)]
#[path = "road_tests.rs"]
mod tests;
