//! Deterministic terrain-draped wall solids and revision-bound wall profile edits.

use crate::{
    GeneratedPath, PathGenerationError, PathGenerationPlan, RiverGenerationError,
    RiverGenerationPlan, RiverGenerationResult, TERRAIN_HALF_EXTENT_M, TerrainGenerationError,
};
use forge_core::{
    AreaId, CanonicalJsonError, CommandId, ContentHash, ModuleId, ProjectId, Revision,
    canonical_json_sha256,
};
use forge_project::{
    AuthoredWall, PathDocument, PathId, TerrainDocument, WallDocument, WallDocumentError,
    WallProfile, WallProfileAlgorithm, WallSurface, WorkspaceArea, WorkspaceChange,
    WorkspaceDocument, WorkspaceEdit, WorkspaceEditResult, WorkspaceError,
};
use serde::{Deserialize, Serialize};
use thiserror::Error;

/// Version of the complete derived wall geometry contract.
pub const WALL_GENERATION_VERSION: u32 = 1;
/// Fixed embed below the exact effective-terrain surface on each side of a wall.
pub const WALL_BASE_EMBED_M: f32 = 0.02;
/// Left side, top, right side and bottom each own two vertices per path sample.
pub const WALL_VERTICES_PER_ROW: usize = 8;
/// Start and end caps each own four vertices.
pub const WALL_CAP_VERTEX_COUNT: usize = 8;
/// Four bands, each made of two triangles, per longitudinal interval.
pub const WALL_INDICES_PER_LONGITUDINAL_INTERVAL: usize = 24;
/// Two triangles at each of the two end caps.
pub const WALL_CAP_INDEX_COUNT: usize = 12;
/// Aggregate path-sample rows across all walls in one area.
pub const MAX_GENERATED_WALL_LONGITUDINAL_SAMPLES: usize = 16_384;
/// Includes eight cap vertices for each of the maximum 64 walls.
pub const MAX_GENERATED_WALL_VERTICES: usize = 131_584;
/// Worst case: one maximum-row wall, four bands and two end caps.
pub const MAX_GENERATED_WALL_INDICES: usize = 393_204;

const MIN_HORIZONTAL_DELTA_SQUARED: f64 = 1.0e-12;
const MIN_TRIANGLE_NORMAL_SQUARED: f64 = 1.0e-18;

/// One engine-neutral solid wall. Positions are absolute Forge world metres.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct GeneratedWall {
    pub path_id: PathId,
    pub profile: WallProfile,
    pub surface: WallSurface,
    /// Eight vertices per path sample followed by eight end-cap vertices.
    pub positions_m: Vec<[f32; 3]>,
    pub normals: Vec<[f32; 3]>,
    /// Twenty-four indices per interval followed by twelve end-cap indices.
    pub indices: Vec<u32>,
}

impl GeneratedWall {
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
        self.positions_m.len().saturating_sub(WALL_CAP_VERTEX_COUNT) / WALL_VERTICES_PER_ROW
    }
}

/// Complete deterministic wall projection for one effective terrain, path and wall source triple.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct GeneratedWalls {
    pub generation_version: u32,
    pub terrain_document_hash: ContentHash,
    pub path_document_hash: ContentHash,
    pub wall_document_hash: ContentHash,
    /// Strictly sorted by `path_id`.
    pub walls: Vec<GeneratedWall>,
}

impl GeneratedWalls {
    #[must_use]
    pub fn vertex_count(&self) -> usize {
        self.walls.iter().map(GeneratedWall::vertex_count).sum()
    }

    #[must_use]
    pub fn triangle_count(&self) -> usize {
        self.walls.iter().map(GeneratedWall::triangle_count).sum()
    }

    pub fn content_hash(&self) -> Result<ContentHash, CanonicalJsonError> {
        canonical_json_sha256(self)
    }
}

/// Validated interpretation of one effective-terrain/path/wall source triple.
#[derive(Clone, Debug)]
pub struct WallGenerationPlan {
    paths: PathGenerationPlan,
    walls: WallDocument,
    path_document_hash: ContentHash,
    wall_document_hash: ContentHash,
}

impl WallGenerationPlan {
    pub fn from_documents(
        effective_terrain: &TerrainDocument,
        paths: &PathDocument,
        walls: &WallDocument,
    ) -> Result<Self, WallGenerationError> {
        walls.validate()?;
        let path_document_hash = paths.content_hash().map_err(PathGenerationError::from)?;
        let paths = PathGenerationPlan::from_documents(effective_terrain, paths)?;
        if effective_terrain.project_id != walls.project_id {
            return Err(WallGenerationError::ProjectIdentityMismatch);
        }
        if effective_terrain.area_id != walls.area_id {
            return Err(WallGenerationError::AreaIdentityMismatch);
        }
        for wall in &walls.walls {
            let path = paths
                .paths()
                .paths
                .binary_search_by_key(&wall.path_id, |path| path.id)
                .ok()
                .map(|index| &paths.paths().paths[index])
                .ok_or(WallGenerationError::MissingPath(wall.path_id))?;
            if path.closed {
                return Err(WallGenerationError::ClosedPath(wall.path_id));
            }
        }
        Ok(Self {
            paths,
            path_document_hash,
            wall_document_hash: walls.content_hash()?,
            walls: walls.clone(),
        })
    }

    #[must_use]
    pub const fn terrain(&self) -> &crate::TerrainGenerationPlan {
        self.paths.terrain()
    }

    #[must_use]
    pub const fn paths(&self) -> &PathGenerationPlan {
        &self.paths
    }

    #[must_use]
    pub const fn walls(&self) -> &WallDocument {
        &self.walls
    }

    pub fn generate(&self) -> Result<GeneratedWalls, WallGenerationError> {
        let mut remaining_rows = MAX_GENERATED_WALL_LONGITUDINAL_SAMPLES;
        let mut total_vertices = 0_usize;
        let mut total_indices = 0_usize;
        let mut generated = Vec::with_capacity(self.walls.walls.len());
        for wall in &self.walls.walls {
            let (wall, row_count) = self.generate_authored_wall(*wall, remaining_rows)?;
            remaining_rows = remaining_rows
                .checked_sub(row_count)
                .ok_or(WallGenerationError::ArithmeticOverflow)?;
            total_vertices = total_vertices
                .checked_add(wall.positions_m.len())
                .ok_or(WallGenerationError::ArithmeticOverflow)?;
            total_indices = total_indices
                .checked_add(wall.indices.len())
                .ok_or(WallGenerationError::ArithmeticOverflow)?;
            if total_vertices > MAX_GENERATED_WALL_VERTICES
                || total_indices > MAX_GENERATED_WALL_INDICES
            {
                return Err(WallGenerationError::MeshBudgetExceeded {
                    vertices: total_vertices,
                    indices: total_indices,
                });
            }
            generated.push(wall);
        }
        Ok(GeneratedWalls {
            generation_version: WALL_GENERATION_VERSION,
            terrain_document_hash: self.paths.terrain().document_hash(),
            path_document_hash: self.path_document_hash,
            wall_document_hash: self.wall_document_hash,
            walls: generated,
        })
    }

    pub fn generate_wall(&self, path_id: PathId) -> Result<GeneratedWall, WallGenerationError> {
        let wall = self
            .walls
            .walls
            .binary_search_by_key(&path_id, |wall| wall.path_id)
            .ok()
            .map(|index| self.walls.walls[index])
            .ok_or(WallGenerationError::MissingWall(path_id))?;
        self.generate_authored_wall(wall, MAX_GENERATED_WALL_LONGITUDINAL_SAMPLES)
            .map(|(wall, _)| wall)
    }

    fn generate_authored_wall(
        &self,
        wall: AuthoredWall,
        remaining_rows: usize,
    ) -> Result<(GeneratedWall, usize), WallGenerationError> {
        match wall.profile.algorithm {
            WallProfileAlgorithm::TerrainDrapedSolidV1 => {}
        }
        match wall.surface {
            WallSurface::ForgeStoneV1 => {}
        }
        let generated_path = self.paths.generate_path(wall.path_id)?;
        let row_count = generated_path.samples.len();
        if row_count > remaining_rows {
            return Err(WallGenerationError::LongitudinalSampleBudgetExceeded {
                requested: MAX_GENERATED_WALL_LONGITUDINAL_SAMPLES
                    .saturating_sub(remaining_rows)
                    .saturating_add(row_count),
                maximum: MAX_GENERATED_WALL_LONGITUDINAL_SAMPLES,
            });
        }
        let (vertex_count, index_count) = checked_mesh_counts(row_count)?;
        let mut positions_m = Vec::with_capacity(vertex_count);
        for row_index in 0..row_count {
            positions_m.extend(self.generate_wall_row(&generated_path, wall, row_index)?);
        }
        append_caps(&mut positions_m, row_count)?;
        let indices = build_indices(row_count, index_count)?;
        let normals = build_normals(wall.path_id, &positions_m, &indices)?;
        Ok((
            GeneratedWall {
                path_id: wall.path_id,
                profile: wall.profile,
                surface: wall.surface,
                positions_m,
                normals,
                indices,
            },
            row_count,
        ))
    }

    fn generate_wall_row(
        &self,
        path: &GeneratedPath,
        wall: AuthoredWall,
        row_index: usize,
    ) -> Result<[[f32; 3]; WALL_VERTICES_PER_ROW], WallGenerationError> {
        let center = path
            .samples
            .get(row_index)
            .ok_or(WallGenerationError::ArithmeticOverflow)?
            .position_m;
        let tangent = tangent_at(path, row_index)?;
        let inverse_length = tangent[0].hypot(tangent[1]).recip();
        let right = [tangent[1] * inverse_length, -tangent[0] * inverse_length];
        let half_thickness_m = f64::from(wall.profile.thickness_m) * 0.5;
        let center_xz = [f64::from(center.x), f64::from(center.z)];
        let left_xz = [
            center_xz[0] - right[0] * half_thickness_m,
            center_xz[1] - right[1] * half_thickness_m,
        ];
        let right_xz = [
            center_xz[0] + right[0] * half_thickness_m,
            center_xz[1] + right[1] * half_thickness_m,
        ];
        let left_base = self.wall_base_vertex(wall.path_id, row_index, 0, left_xz)?;
        let right_base = self.wall_base_vertex(wall.path_id, row_index, 1, right_xz)?;
        let left_top = top_vertex(left_base, wall.profile.height_m, wall.path_id)?;
        let right_top = top_vertex(right_base, wall.profile.height_m, wall.path_id)?;
        Ok([
            left_base, left_top, left_top, right_top, right_top, right_base, right_base, left_base,
        ])
    }

    fn wall_base_vertex(
        &self,
        path_id: PathId,
        row_index: usize,
        side_index: usize,
        position_xz_m: [f64; 2],
    ) -> Result<[f32; 3], WallGenerationError> {
        if position_xz_m.iter().any(|coordinate| {
            !coordinate.is_finite()
                || *coordinate < -f64::from(TERRAIN_HALF_EXTENT_M)
                || *coordinate >= f64::from(TERRAIN_HALF_EXTENT_M)
        }) {
            return Err(WallGenerationError::FootprintOutOfBounds {
                path_id,
                row_index,
                side_index,
            });
        }
        #[allow(clippy::cast_possible_truncation)]
        let horizontal_m = [position_xz_m[0] as f32, position_xz_m[1] as f32];
        let surface = self
            .paths
            .terrain()
            .surface_at(horizontal_m[0], horizontal_m[1])
            .map_err(|error| WallGenerationError::FootprintTerrain {
                path_id,
                row_index,
                side_index,
                source: error,
            })?;
        let y_m = surface.height_m - WALL_BASE_EMBED_M;
        if !y_m.is_finite() {
            return Err(WallGenerationError::InvalidDerivedValue {
                path_id,
                field: "wall base position",
            });
        }
        Ok([horizontal_m[0], y_m, horizontal_m[1]])
    }
}

/// Regenerates and validates the complete wall payload against its exact canonical sources.
pub fn validate_generated_walls(
    effective_terrain: &TerrainDocument,
    paths: &PathDocument,
    walls: &WallDocument,
    generated: &GeneratedWalls,
) -> Result<(), WallGenerationError> {
    let expected =
        WallGenerationPlan::from_documents(effective_terrain, paths, walls)?.generate()?;
    if generated.generation_version != WALL_GENERATION_VERSION
        || generated.terrain_document_hash != expected.terrain_document_hash
        || generated.path_document_hash != expected.path_document_hash
        || generated.wall_document_hash != expected.wall_document_hash
    {
        return Err(WallGenerationError::InvalidGeneratedWalls(
            "source hashes or generation version differ",
        ));
    }
    if generated.walls.len() != expected.walls.len() {
        return Err(WallGenerationError::InvalidGeneratedWalls(
            "generated wall set is not exhaustive",
        ));
    }
    for (actual, expected) in generated.walls.iter().zip(&expected.walls) {
        validate_generated_wall_exact(actual, expected)?;
    }
    Ok(())
}

fn validate_generated_wall_exact(
    actual: &GeneratedWall,
    expected: &GeneratedWall,
) -> Result<(), WallGenerationError> {
    if actual.path_id != expected.path_id
        || actual.profile != expected.profile
        || actual.surface != expected.surface
    {
        return Err(WallGenerationError::InvalidGeneratedWalls(
            "wall order, identity, profile or surface differs",
        ));
    }
    if actual.indices != expected.indices {
        return Err(WallGenerationError::InvalidGeneratedWalls(
            "wall topology or indices differ",
        ));
    }
    if !same_vec3_slice_bits(&actual.positions_m, &expected.positions_m) {
        return Err(WallGenerationError::InvalidGeneratedWalls(
            "wall positions, footprint or heights differ",
        ));
    }
    if !same_vec3_slice_bits(&actual.normals, &expected.normals) {
        return Err(WallGenerationError::InvalidGeneratedWalls(
            "wall normals differ",
        ));
    }
    Ok(())
}

fn tangent_at(path: &GeneratedPath, index: usize) -> Result<[f64; 2], WallGenerationError> {
    let last = path
        .samples
        .len()
        .checked_sub(1)
        .ok_or(WallGenerationError::ArithmeticOverflow)?;
    let current = path
        .samples
        .get(index)
        .ok_or(WallGenerationError::ArithmeticOverflow)?
        .position_m;
    let candidates = if index == 0 {
        [Some((path.samples[1].position_m, current)), None, None]
    } else if index == last {
        [
            Some((current, path.samples[last - 1].position_m)),
            None,
            None,
        ]
    } else {
        [
            Some((
                path.samples[index + 1].position_m,
                path.samples[index - 1].position_m,
            )),
            Some((path.samples[index + 1].position_m, current)),
            Some((current, path.samples[index - 1].position_m)),
        ]
    };
    for (second, first) in candidates.into_iter().flatten() {
        let tangent = [
            f64::from(second.x) - f64::from(first.x),
            f64::from(second.z) - f64::from(first.z),
        ];
        let squared_length = tangent[0] * tangent[0] + tangent[1] * tangent[1];
        if squared_length.is_finite() && squared_length > MIN_HORIZONTAL_DELTA_SQUARED {
            return Ok(tangent);
        }
    }
    Err(WallGenerationError::InvalidTangent {
        path_id: path.id,
        sample_index: index,
    })
}

fn top_vertex(
    base: [f32; 3],
    height_m: f32,
    path_id: PathId,
) -> Result<[f32; 3], WallGenerationError> {
    let y_m = base[1] + height_m;
    if !y_m.is_finite() {
        return Err(WallGenerationError::InvalidDerivedValue {
            path_id,
            field: "wall top position",
        });
    }
    Ok([base[0], y_m, base[2]])
}

fn append_caps(
    positions_m: &mut Vec<[f32; 3]>,
    row_count: usize,
) -> Result<(), WallGenerationError> {
    let last_offset = row_count
        .checked_sub(1)
        .and_then(|row| row.checked_mul(WALL_VERTICES_PER_ROW))
        .ok_or(WallGenerationError::ArithmeticOverflow)?;
    let first = positions_m
        .get(..WALL_VERTICES_PER_ROW)
        .ok_or(WallGenerationError::ArithmeticOverflow)?;
    let first_cap = [first[0], first[5], first[4], first[1]];
    let last = positions_m
        .get(last_offset..last_offset + WALL_VERTICES_PER_ROW)
        .ok_or(WallGenerationError::ArithmeticOverflow)?;
    let last_cap = [last[0], last[5], last[4], last[1]];
    positions_m.extend(first_cap);
    positions_m.extend(last_cap);
    Ok(())
}

fn checked_mesh_counts(row_count: usize) -> Result<(usize, usize), WallGenerationError> {
    if !(2..=MAX_GENERATED_WALL_LONGITUDINAL_SAMPLES).contains(&row_count) {
        return Err(WallGenerationError::LongitudinalSampleBudgetExceeded {
            requested: row_count,
            maximum: MAX_GENERATED_WALL_LONGITUDINAL_SAMPLES,
        });
    }
    let vertex_count = row_count
        .checked_mul(WALL_VERTICES_PER_ROW)
        .and_then(|count| count.checked_add(WALL_CAP_VERTEX_COUNT))
        .ok_or(WallGenerationError::ArithmeticOverflow)?;
    let index_count = row_count
        .checked_sub(1)
        .and_then(|intervals| intervals.checked_mul(WALL_INDICES_PER_LONGITUDINAL_INTERVAL))
        .and_then(|count| count.checked_add(WALL_CAP_INDEX_COUNT))
        .ok_or(WallGenerationError::ArithmeticOverflow)?;
    if vertex_count > MAX_GENERATED_WALL_VERTICES || index_count > MAX_GENERATED_WALL_INDICES {
        return Err(WallGenerationError::MeshBudgetExceeded {
            vertices: vertex_count,
            indices: index_count,
        });
    }
    Ok((vertex_count, index_count))
}

fn build_indices(row_count: usize, expected_count: usize) -> Result<Vec<u32>, WallGenerationError> {
    let mut indices = Vec::with_capacity(expected_count);
    for row in 0..row_count - 1 {
        let current = row
            .checked_mul(WALL_VERTICES_PER_ROW)
            .ok_or(WallGenerationError::ArithmeticOverflow)?;
        let next = current
            .checked_add(WALL_VERTICES_PER_ROW)
            .ok_or(WallGenerationError::ArithmeticOverflow)?;
        for band in 0..4 {
            let first = checked_mesh_index(current, band * 2)?;
            let second = checked_mesh_index(current, band * 2 + 1)?;
            let next_first = checked_mesh_index(next, band * 2)?;
            let next_second = checked_mesh_index(next, band * 2 + 1)?;
            indices.extend([first, next_first, second, second, next_first, next_second]);
        }
    }
    let cap_start = row_count
        .checked_mul(WALL_VERTICES_PER_ROW)
        .ok_or(WallGenerationError::ArithmeticOverflow)?;
    let start = checked_mesh_index(cap_start, 0)?;
    indices.extend([start, start + 3, start + 2, start, start + 2, start + 1]);
    let end = checked_mesh_index(cap_start, 4)?;
    indices.extend([end, end + 1, end + 2, end, end + 2, end + 3]);
    if indices.len() != expected_count {
        return Err(WallGenerationError::IndexCountMismatch {
            expected: expected_count,
            generated: indices.len(),
        });
    }
    Ok(indices)
}

fn checked_mesh_index(base: usize, offset: usize) -> Result<u32, WallGenerationError> {
    let index = base
        .checked_add(offset)
        .ok_or(WallGenerationError::ArithmeticOverflow)?;
    u32::try_from(index).map_err(|_| WallGenerationError::IndexOverflow)
}

fn build_normals(
    path_id: PathId,
    positions_m: &[[f32; 3]],
    indices: &[u32],
) -> Result<Vec<[f32; 3]>, WallGenerationError> {
    let mut accumulated = vec![[0.0_f64; 3]; positions_m.len()];
    let (triangles, remainder) = indices.as_chunks::<3>();
    if !remainder.is_empty() {
        return Err(WallGenerationError::InvalidIndexCount(indices.len()));
    }
    for (triangle_index, triangle) in triangles.iter().enumerate() {
        accumulate_triangle_normal(
            path_id,
            triangle_index,
            *triangle,
            positions_m,
            &mut accumulated,
        )?;
    }
    accumulated
        .into_iter()
        .enumerate()
        .map(|(vertex_index, normal)| normalize_normal(path_id, vertex_index, normal))
        .collect()
}

fn accumulate_triangle_normal(
    path_id: PathId,
    triangle_index: usize,
    triangle: [u32; 3],
    positions_m: &[[f32; 3]],
    accumulated: &mut [[f64; 3]],
) -> Result<(), WallGenerationError> {
    let indices = triangle.map(|index| usize::try_from(index).ok());
    let [Some(first_index), Some(second_index), Some(third_index)] = indices else {
        return Err(WallGenerationError::IndexOverflow);
    };
    let points =
        [first_index, second_index, third_index].map(|index| positions_m.get(index).copied());
    let [Some(first), Some(second), Some(third)] = points else {
        return Err(WallGenerationError::IndexOverflow);
    };
    let first_edge = subtract(second, first);
    let second_edge = subtract(third, first);
    let normal = cross(first_edge, second_edge);
    let squared_length = normal.iter().map(|value| value * value).sum::<f64>();
    if normal.iter().any(|value| !value.is_finite())
        || squared_length <= MIN_TRIANGLE_NORMAL_SQUARED
    {
        return Err(WallGenerationError::InvalidTriangle {
            path_id,
            triangle_index,
        });
    }
    for index in [first_index, second_index, third_index] {
        for axis in 0..3 {
            accumulated[index][axis] += normal[axis];
        }
    }
    Ok(())
}

fn normalize_normal(
    path_id: PathId,
    vertex_index: usize,
    normal: [f64; 3],
) -> Result<[f32; 3], WallGenerationError> {
    let squared_length = normal.iter().map(|value| value * value).sum::<f64>();
    if !squared_length.is_finite() || squared_length <= MIN_TRIANGLE_NORMAL_SQUARED {
        return Err(WallGenerationError::InvalidVertexNormal {
            path_id,
            vertex_index,
        });
    }
    let inverse_length = squared_length.sqrt().recip();
    #[allow(clippy::cast_possible_truncation)]
    let normalized = normal.map(|component| (component * inverse_length) as f32);
    if normalized.iter().any(|component| !component.is_finite()) {
        return Err(WallGenerationError::InvalidVertexNormal {
            path_id,
            vertex_index,
        });
    }
    Ok(normalized)
}

fn subtract(second: [f32; 3], first: [f32; 3]) -> [f64; 3] {
    [
        f64::from(second[0]) - f64::from(first[0]),
        f64::from(second[1]) - f64::from(first[1]),
        f64::from(second[2]) - f64::from(first[2]),
    ]
}

fn cross(first: [f64; 3], second: [f64; 3]) -> [f64; 3] {
    [
        first[1] * second[2] - first[2] * second[1],
        first[2] * second[0] - first[0] * second[2],
        first[0] * second[1] - first[1] * second[0],
    ]
}

fn same_vec3_slice_bits(first: &[[f32; 3]], second: &[[f32; 3]]) -> bool {
    first.len() == second.len()
        && first.iter().zip(second).all(|(first, second)| {
            first
                .iter()
                .zip(second)
                .all(|(first, second)| first.to_bits() == second.to_bits())
        })
}

/// Revision-bound request to create or replace one wall profile.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WallUpsertCommand {
    pub command_id: CommandId,
    pub base_revision: Revision,
    pub project_id: ProjectId,
    pub module_id: ModuleId,
    pub area_id: AreaId,
    pub wall: AuthoredWall,
}

/// Candidate workspace edit which changes exactly one wall assignment.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WallUpsertResult {
    pub command: WallUpsertCommand,
    pub previous_wall: Option<AuthoredWall>,
    pub wall: AuthoredWall,
    pub edit: WorkspaceEditResult,
}

/// Produces and fully generates one candidate without mutating the supplied workspace.
pub fn preview_upsert_wall(
    workspace: &WorkspaceDocument,
    command: WallUpsertCommand,
) -> Result<WallUpsertResult, WallCommandError> {
    let area = validate_command_context(
        workspace,
        command.base_revision,
        command.project_id,
        command.module_id,
        command.area_id,
    )?;
    command.wall.validate()?;
    let previous_wall = area
        .walls
        .walls
        .binary_search_by_key(&command.wall.path_id, |wall| wall.path_id)
        .ok()
        .map(|index| area.walls.walls[index]);
    let edit = workspace.preview_edit(WorkspaceEdit {
        command_id: command.command_id,
        base_revision: command.base_revision,
        change: WorkspaceChange::UpsertWall {
            module_id: command.module_id,
            area_id: command.area_id,
            expected_terrain_document_hash: area
                .terrain
                .content_hash()
                .map_err(WorkspaceError::from)?,
            expected_path_document_hash: area.paths.content_hash().map_err(WorkspaceError::from)?,
            expected_road_document_hash: area.roads.content_hash().map_err(WorkspaceError::from)?,
            expected_river_document_hash: area
                .rivers
                .content_hash()
                .map_err(WorkspaceError::from)?,
            expected_wall_document_hash: area.walls.content_hash().map_err(WorkspaceError::from)?,
            wall: command.wall,
        },
    })?;
    validate_candidate_walls(&edit, command.module_id, command.area_id)?;
    Ok(WallUpsertResult {
        wall: command.wall,
        command,
        previous_wall,
        edit,
    })
}

pub(crate) fn validate_candidate_walls(
    edit: &WorkspaceEditResult,
    module_id: ModuleId,
    area_id: AreaId,
) -> Result<(), WallGenerationError> {
    let area = edit
        .document
        .area(module_id, area_id)
        .ok_or(WallGenerationError::MissingArea)?;
    if area.walls.walls.is_empty() {
        return Ok(());
    }
    let river_projection =
        RiverGenerationPlan::from_documents(&area.terrain, &area.paths, &area.roads, &area.rivers)?
            .generate()?;
    validate_area_walls_with_effective_terrain(area, &river_projection.effective_terrain)
}

pub(crate) fn validate_candidate_walls_with_river_projection(
    edit: &WorkspaceEditResult,
    module_id: ModuleId,
    area_id: AreaId,
    river_projection: &RiverGenerationResult,
) -> Result<(), WallGenerationError> {
    let area = edit
        .document
        .area(module_id, area_id)
        .ok_or(WallGenerationError::MissingArea)?;
    validate_area_walls_with_effective_terrain(area, &river_projection.effective_terrain)
}

fn validate_area_walls_with_effective_terrain(
    area: &WorkspaceArea,
    effective_terrain: &TerrainDocument,
) -> Result<(), WallGenerationError> {
    if area.walls.walls.is_empty() {
        return Ok(());
    }
    WallGenerationPlan::from_documents(effective_terrain, &area.paths, &area.walls)?.generate()?;
    Ok(())
}

fn validate_command_context(
    workspace: &WorkspaceDocument,
    base_revision: Revision,
    project_id: ProjectId,
    module_id: ModuleId,
    area_id: AreaId,
) -> Result<&WorkspaceArea, WallCommandError> {
    workspace.validate()?;
    if workspace.project.id != project_id {
        return Err(WallCommandError::WrongProject);
    }
    if workspace.revision != base_revision {
        return Err(WorkspaceError::StaleRevision.into());
    }
    workspace
        .area(module_id, area_id)
        .ok_or_else(|| WorkspaceError::MissingTarget("area").into())
}

#[derive(Debug, Error)]
pub enum WallGenerationError {
    #[error("wall document is invalid: {0}")]
    InvalidWallDocument(#[from] WallDocumentError),
    #[error("wall path generation failed: {0}")]
    Path(#[from] PathGenerationError),
    #[error("river projection used by wall generation failed: {0}")]
    River(#[from] RiverGenerationError),
    #[error("effective terrain and wall documents belong to different projects")]
    ProjectIdentityMismatch,
    #[error("effective terrain and wall documents belong to different areas")]
    AreaIdentityMismatch,
    #[error("wall references missing path {0}")]
    MissingPath(PathId),
    #[error("wall path {0} must be open")]
    ClosedPath(PathId),
    #[error("wall profile for path {0} does not exist")]
    MissingWall(PathId),
    #[error("wall candidate area does not exist")]
    MissingArea,
    #[error("wall path {path_id} has an invalid tangent at sample {sample_index}")]
    InvalidTangent {
        path_id: PathId,
        sample_index: usize,
    },
    #[error(
        "wall path {path_id} footprint row {row_index}, side {side_index} is outside [-256,256) metres"
    )]
    FootprintOutOfBounds {
        path_id: PathId,
        row_index: usize,
        side_index: usize,
    },
    #[error(
        "wall path {path_id} footprint row {row_index}, side {side_index} cannot sample effective terrain: {source}"
    )]
    FootprintTerrain {
        path_id: PathId,
        row_index: usize,
        side_index: usize,
        source: TerrainGenerationError,
    },
    #[error("wall path {path_id} derived {field} is not finite")]
    InvalidDerivedValue {
        path_id: PathId,
        field: &'static str,
    },
    #[error("wall projection requests {requested} longitudinal rows; maximum is {maximum}")]
    LongitudinalSampleBudgetExceeded { requested: usize, maximum: usize },
    #[error("wall mesh requests {vertices} vertices and {indices} indices")]
    MeshBudgetExceeded { vertices: usize, indices: usize },
    #[error("generated walls are invalid: {0}")]
    InvalidGeneratedWalls(&'static str),
    #[error("wall mesh arithmetic overflow")]
    ArithmeticOverflow,
    #[error("wall mesh index cannot be represented")]
    IndexOverflow,
    #[error("wall mesh index count {0} is not divisible by three")]
    InvalidIndexCount(usize),
    #[error("wall mesh expected {expected} indices but generated {generated}")]
    IndexCountMismatch { expected: usize, generated: usize },
    #[error("wall path {path_id} generated invalid triangle {triangle_index}")]
    InvalidTriangle {
        path_id: PathId,
        triangle_index: usize,
    },
    #[error("wall path {path_id} generated invalid normal at vertex {vertex_index}")]
    InvalidVertexNormal {
        path_id: PathId,
        vertex_index: usize,
    },
}

#[derive(Debug, Error)]
pub enum WallCommandError {
    #[error("wall command belongs to another project")]
    WrongProject,
    #[error("wall profile is invalid: {0}")]
    InvalidWall(#[from] WallDocumentError),
    #[error("workspace wall edit failed: {0}")]
    Workspace(#[from] WorkspaceError),
    #[error("generated wall candidate is invalid: {0}")]
    Generation(#[from] WallGenerationError),
}

#[cfg(test)]
#[path = "wall_tests.rs"]
mod tests;
