//! Deterministic, non-destructive river carving and water-mesh projection.

use crate::{
    GeneratedPath, GeneratedPaths, GeneratedRoads, MAX_GENERATED_ROAD_LONGITUDINAL_SAMPLES,
    PathGenerationError, PathGenerationPlan, ROAD_CROSS_SECTION_VERTEX_COUNT, RoadGenerationError,
    RoadGenerationPlan, TERRAIN_GLOBAL_INTERVALS_PER_AXIS, TERRAIN_GLOBAL_VERTICES_PER_AXIS,
    TERRAIN_HALF_EXTENT_M, TERRAIN_SAMPLE_SPACING_M, TerrainGenerationError, TerrainGenerationPlan,
};
use forge_core::{
    AreaId, CanonicalJsonError, CommandId, ContentHash, ModuleId, ProjectId, Revision,
    canonical_json_sha256,
};
use forge_project::{
    AuthoredRiver, AuthoredRoad, MAX_TERRAIN_HEIGHT_DELTAS, MAX_TERRAIN_MATERIAL_WEIGHT_OVERRIDES,
    MAX_TOTAL_RIVER_LENGTH_M, PathDocument, PathId, RiverChannelProfileAlgorithm, RiverDocument,
    RiverDocumentError, RiverSurface, RoadDocument, TerrainDocument, TerrainHeightDelta,
    TerrainMaterialWeightOverride, WorkspaceArea, WorkspaceChange, WorkspaceDocument,
    WorkspaceEdit, WorkspaceEditResult, WorkspaceError,
};
use serde::{Deserialize, Serialize};
use std::collections::BTreeMap;
use thiserror::Error;

pub const RIVER_GENERATION_VERSION: u32 = 2;
pub const RIVER_SURFACE_CLEARANCE_M: f32 = 0.04;
pub const RIVER_MESH_MAX_LONGITUDINAL_CHORD_M: f64 = 2.0;
pub const RIVER_CROSS_SECTION_VERTEX_COUNT: usize = 3;
pub const MAX_GENERATED_RIVER_LONGITUDINAL_SAMPLES: usize = 16_384;
pub const MAX_GENERATED_RIVER_VERTICES: usize =
    MAX_GENERATED_RIVER_LONGITUDINAL_SAMPLES * RIVER_CROSS_SECTION_VERTEX_COUNT;
pub const MAX_GENERATED_RIVER_INDICES: usize = MAX_GENERATED_RIVER_LONGITUDINAL_SAMPLES * 12;
pub const MAX_EFFECTIVE_RIVER_TERRAIN_SAMPLES: usize = 65_536;
pub const MAX_RIVER_OVERLAP_COMPARISONS: usize = 4_000_000;
pub const MAX_GENERATED_ROAD_RIVER_INTERSECTIONS: usize = 64;
pub const MIN_TERRAIN_FORD_CROSSING_ANGLE_DEGREES: f32 = 30.0;
pub const MAX_TERRAIN_FORD_CROSSING_ANGLE_DEGREES: f32 = 90.0;
pub const MIN_TERRAIN_FORD_WATER_DEPTH_M: f32 = 0.05;

const RIVER_INDICES_PER_LONGITUDINAL_INTERVAL: usize = 12;
const RIVER_RESAMPLE_TARGET_CHORD_M: f64 = 1.99;
const MIN_HORIZONTAL_DELTA_SQUARED: f64 = 1.0e-12;
const MIN_TRIANGLE_NORMAL_SQUARED: f64 = 1.0e-18;
const SEGMENT_INTERSECTION_EPSILON: f64 = 1.0e-10;
const TERRAIN_FORD_POINT_MERGE_DISTANCE_M: f64 = 1.0e-4;
const TERRAIN_FORD_TOPOLOGY_PROBE_DISTANCE_M: f64 = 0.25;
const TERRAIN_FORD_SAMPLE_GUARD_M: f64 = 2.0;

/// One complete, engine-neutral water ribbon derived from an authored river assignment.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct GeneratedRiver {
    pub path_id: PathId,
    pub channel: forge_project::RiverChannelProfile,
    pub surface: RiverSurface,
    pub positions_m: Vec<[f32; 3]>,
    pub normals: Vec<[f32; 3]>,
    pub uvs: Vec<[f32; 2]>,
    pub indices: Vec<u32>,
}

impl GeneratedRiver {
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
        self.positions_m.len() / RIVER_CROSS_SECTION_VERTEX_COUNT
    }
}

/// Versioned interpretation of a supported crossing between one road and one river.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum RoadRiverIntersectionKind {
    TerrainFordV1,
}

/// One bounded, renderer-independent ford derived from two distinct authored paths.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct GeneratedRoadRiverIntersection {
    pub river_path_id: PathId,
    pub road_path_id: PathId,
    pub kind: RoadRiverIntersectionKind,
    pub position_xz_m: [f32; 2],
    /// Acute horizontal angle, in the inclusive range 30..=90 degrees.
    pub crossing_angle_degrees: f32,
    /// Highest road centreline point at the crossing, including crown and mesh clearance.
    pub road_surface_height_m: f32,
    /// Water mesh height at the crossing, including mesh clearance.
    pub water_surface_height_m: f32,
}

/// Complete derived river payload bound to every canonical input and the effective terrain.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct GeneratedRivers {
    pub generation_version: u32,
    pub terrain_document_hash: ContentHash,
    pub path_document_hash: ContentHash,
    pub road_document_hash: ContentHash,
    pub river_document_hash: ContentHash,
    pub effective_terrain_document_hash: ContentHash,
    /// Strictly sorted by `path_id` like the canonical river document.
    pub rivers: Vec<GeneratedRiver>,
    /// Strictly sorted and unique by `(river_path_id, road_path_id)`.
    pub road_river_intersections: Vec<GeneratedRoadRiverIntersection>,
}

impl GeneratedRivers {
    #[must_use]
    pub fn vertex_count(&self) -> usize {
        self.rivers.iter().map(GeneratedRiver::vertex_count).sum()
    }

    #[must_use]
    pub fn triangle_count(&self) -> usize {
        self.rivers.iter().map(GeneratedRiver::triangle_count).sum()
    }

    pub fn content_hash(&self) -> Result<ContentHash, CanonicalJsonError> {
        canonical_json_sha256(self)
    }
}

/// Recomputes the complete bounded ford set from canonical path resampling and validates it.
///
/// This is intentionally shared with renderer adapters so omission, insertion and mutation of
/// derived intersection metadata are rejected without duplicating the geometric classifier.
pub fn validate_generated_road_river_intersections(
    paths: &GeneratedPaths,
    roads: &GeneratedRoads,
    generated: &GeneratedRivers,
) -> Result<(), RiverGenerationError> {
    validate_generated_intersection_sources(paths, roads, generated)?;
    validate_generated_centrelines(paths, roads, generated)?;
    if generated.road_river_intersections.len() > MAX_GENERATED_ROAD_RIVER_INTERSECTIONS {
        return Err(RiverGenerationError::TooManyRoadRiverIntersections {
            found: generated.road_river_intersections.len(),
            maximum: MAX_GENERATED_ROAD_RIVER_INTERSECTIONS,
        });
    }
    if generated.road_river_intersections.windows(2).any(|pair| {
        (pair[0].river_path_id, pair[0].road_path_id)
            >= (pair[1].river_path_id, pair[1].road_path_id)
    }) {
        return Err(
            RiverGenerationError::InvalidGeneratedRoadRiverIntersections(
                "metadata are not strictly sorted and unique",
            ),
        );
    }

    let mut comparisons = 0_usize;
    let mut expected = Vec::new();
    for river in &generated.rivers {
        let river_points = generated_centerline_points(river.path_id, &river.positions_m)?;
        let river_radius_m = f64::from(river.channel.surface_width_m) * 0.5
            + f64::from(river.channel.bank_blend_width_m);
        if !river_radius_m.is_finite() || river_radius_m <= 0.0 {
            return Err(
                RiverGenerationError::InvalidGeneratedRoadRiverIntersections(
                    "river footprint is invalid",
                ),
            );
        }
        for road in &roads.roads {
            if road.path_id == river.path_id {
                return Err(RiverGenerationError::RiverRoadConflict(river.path_id));
            }
            let path = paths
                .paths
                .binary_search_by_key(&road.path_id, |path| path.id)
                .ok()
                .map(|index| &paths.paths[index])
                .ok_or(RiverGenerationError::MissingRoadPath(road.path_id))?;
            let road_points = generated_path_points(path)?;
            let road_radius_m = f64::from(road.profile.width_m) * 0.5;
            if !road_radius_m.is_finite() || road_radius_m <= 0.0 {
                return Err(
                    RiverGenerationError::InvalidGeneratedRoadRiverIntersections(
                        "road footprint is invalid",
                    ),
                );
            }
            if !polylines_overlap(
                &river_points,
                river_radius_m,
                &road_points,
                road_radius_m,
                &mut comparisons,
            )? {
                continue;
            }
            expected.push(prepare_terrain_ford(
                river.path_id,
                river_radius_m,
                &river_points,
                &AuthoredRoad {
                    path_id: road.path_id,
                    profile: road.profile,
                    surface: road.surface,
                },
                &road_points,
                &mut comparisons,
            )?);
            if expected.len() > MAX_GENERATED_ROAD_RIVER_INTERSECTIONS {
                return Err(RiverGenerationError::TooManyRoadRiverIntersections {
                    found: expected.len(),
                    maximum: MAX_GENERATED_ROAD_RIVER_INTERSECTIONS,
                });
            }
        }
    }
    expected.sort_by_key(|intersection| (intersection.river_path_id, intersection.road_path_id));
    if expected.len() != generated.road_river_intersections.len() {
        return Err(
            RiverGenerationError::InvalidGeneratedRoadRiverIntersections(
                "metadata are not exhaustive",
            ),
        );
    }
    for (expected, actual) in expected.iter().zip(&generated.road_river_intersections) {
        validate_generated_terrain_ford(expected, actual, roads, generated)?;
    }
    Ok(())
}

fn validate_generated_intersection_sources(
    paths: &GeneratedPaths,
    roads: &GeneratedRoads,
    generated: &GeneratedRivers,
) -> Result<(), RiverGenerationError> {
    if generated.generation_version != RIVER_GENERATION_VERSION
        || generated.path_document_hash != paths.path_document_hash
        || generated.path_document_hash != roads.path_document_hash
        || generated.road_document_hash != roads.road_document_hash
        || generated.effective_terrain_document_hash != paths.terrain_document_hash
        || generated.effective_terrain_document_hash != roads.terrain_document_hash
    {
        return Err(
            RiverGenerationError::InvalidGeneratedRoadRiverIntersections(
                "source hashes or generation version differ",
            ),
        );
    }
    Ok(())
}

fn validate_generated_centrelines(
    paths: &GeneratedPaths,
    roads: &GeneratedRoads,
    generated: &GeneratedRivers,
) -> Result<(), RiverGenerationError> {
    validate_generated_river_centrelines(paths, generated)?;
    validate_generated_road_centrelines(paths, roads)
}

fn validate_generated_river_centrelines(
    paths: &GeneratedPaths,
    generated: &GeneratedRivers,
) -> Result<(), RiverGenerationError> {
    let mut remaining_rows = MAX_GENERATED_RIVER_LONGITUDINAL_SAMPLES;
    let mut previous_path_id = None;
    for river in &generated.rivers {
        if previous_path_id.is_some_and(|previous| previous >= river.path_id) {
            return Err(
                RiverGenerationError::InvalidGeneratedRoadRiverIntersections(
                    "generated rivers are not strictly sorted and unique",
                ),
            );
        }
        previous_path_id = Some(river.path_id);
        let path = paths
            .paths
            .binary_search_by_key(&river.path_id, |path| path.id)
            .ok()
            .map(|index| &paths.paths[index])
            .ok_or(RiverGenerationError::MissingPath(river.path_id))?;
        let expected_rows = adaptive_centerline(path, remaining_rows)?;
        let (actual_rows, remainder) = river
            .positions_m
            .as_chunks::<RIVER_CROSS_SECTION_VERTEX_COUNT>();
        if !remainder.is_empty() || actual_rows.len() != expected_rows.len() {
            return Err(
                RiverGenerationError::InvalidGeneratedRoadRiverIntersections(
                    "river centreline row count differs from deterministic path resampling",
                ),
            );
        }
        for (actual, expected) in actual_rows.iter().zip(&expected_rows) {
            let expected_xz_m = [
                f32_from_derived(expected.xz_m[0], river.path_id, "canonical river X")?,
                f32_from_derived(expected.xz_m[1], river.path_id, "canonical river Z")?,
            ];
            if [actual[1][0].to_bits(), actual[1][2].to_bits()] != expected_xz_m.map(f32::to_bits) {
                return Err(
                    RiverGenerationError::InvalidGeneratedRoadRiverIntersections(
                        "river centreline differs from deterministic path resampling",
                    ),
                );
            }
        }
        remaining_rows = remaining_rows
            .checked_sub(expected_rows.len())
            .ok_or(RiverGenerationError::ArithmeticOverflow)?;
    }
    Ok(())
}

fn validate_generated_road_centrelines(
    paths: &GeneratedPaths,
    roads: &GeneratedRoads,
) -> Result<(), RiverGenerationError> {
    let mut remaining_samples = MAX_GENERATED_ROAD_LONGITUDINAL_SAMPLES;
    let mut previous_path_id = None;
    for road in &roads.roads {
        if previous_path_id.is_some_and(|previous| previous >= road.path_id) {
            return Err(
                RiverGenerationError::InvalidGeneratedRoadRiverIntersections(
                    "generated roads are not strictly sorted and unique",
                ),
            );
        }
        previous_path_id = Some(road.path_id);
        let path = paths
            .paths
            .binary_search_by_key(&road.path_id, |path| path.id)
            .ok()
            .map(|index| &paths.paths[index])
            .ok_or(RiverGenerationError::MissingRoadPath(road.path_id))?;
        let expected_rows = crate::road::adaptive_centerline(path, remaining_samples)?;
        let (actual_rows, remainder) = road
            .positions_m
            .as_chunks::<ROAD_CROSS_SECTION_VERTEX_COUNT>();
        if !remainder.is_empty() || actual_rows.len() != expected_rows.len() {
            return Err(
                RiverGenerationError::InvalidGeneratedRoadRiverIntersections(
                    "road centreline row count differs from deterministic path resampling",
                ),
            );
        }
        for (actual, expected) in actual_rows.iter().zip(&expected_rows) {
            #[allow(clippy::cast_possible_truncation)]
            let expected_xz_m = [expected.xz_m[0] as f32, expected.xz_m[1] as f32];
            if expected_xz_m.iter().any(|value| !value.is_finite())
                || [actual[1][0].to_bits(), actual[1][2].to_bits()]
                    != expected_xz_m.map(f32::to_bits)
            {
                return Err(
                    RiverGenerationError::InvalidGeneratedRoadRiverIntersections(
                        "road centreline differs from deterministic path resampling",
                    ),
                );
            }
        }
        remaining_samples = remaining_samples
            .checked_sub(expected_rows.len())
            .ok_or(RiverGenerationError::ArithmeticOverflow)?;
    }
    Ok(())
}

/// One deterministic generation result. `effective_terrain` is derived and must never be persisted.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct RiverGenerationResult {
    pub effective_terrain: TerrainDocument,
    pub generated: GeneratedRivers,
}

/// Validated canonical inputs for non-destructive river projection.
#[derive(Clone, Debug)]
pub struct RiverGenerationPlan {
    terrain: TerrainDocument,
    paths: PathDocument,
    roads: RoadDocument,
    rivers: RiverDocument,
    terrain_hash: ContentHash,
    path_hash: ContentHash,
    road_hash: ContentHash,
    river_hash: ContentHash,
}

impl RiverGenerationPlan {
    pub fn from_documents(
        terrain: &TerrainDocument,
        paths: &PathDocument,
        roads: &RoadDocument,
        rivers: &RiverDocument,
    ) -> Result<Self, RiverGenerationError> {
        terrain.validate()?;
        paths.validate()?;
        roads.validate()?;
        rivers.validate()?;
        if terrain.project_id != paths.project_id
            || terrain.project_id != roads.project_id
            || terrain.project_id != rivers.project_id
        {
            return Err(RiverGenerationError::ProjectIdentityMismatch);
        }
        if terrain.area_id != paths.area_id
            || terrain.area_id != roads.area_id
            || terrain.area_id != rivers.area_id
        {
            return Err(RiverGenerationError::AreaIdentityMismatch);
        }

        // These plans validate every path/road reference consumed by overlap checks.
        PathGenerationPlan::from_documents(terrain, paths)?;
        RoadGenerationPlan::from_documents(terrain, paths, roads)?;

        let mut total_length_m = 0.0_f64;
        for river in &rivers.rivers {
            let path = paths
                .paths
                .binary_search_by_key(&river.path_id, |path| path.id)
                .ok()
                .map(|index| &paths.paths[index])
                .ok_or(RiverGenerationError::MissingPath(river.path_id))?;
            if path.closed {
                return Err(RiverGenerationError::ClosedPath(river.path_id));
            }
            if roads
                .roads
                .binary_search_by_key(&river.path_id, |road| road.path_id)
                .is_ok()
            {
                return Err(RiverGenerationError::RiverRoadConflict(river.path_id));
            }
            total_length_m += path.validate()?;
            if !total_length_m.is_finite() || total_length_m > MAX_TOTAL_RIVER_LENGTH_M {
                return Err(RiverGenerationError::TotalLengthExceeded {
                    length_m: total_length_m,
                    maximum_m: MAX_TOTAL_RIVER_LENGTH_M,
                });
            }
        }

        Ok(Self {
            terrain_hash: terrain.content_hash()?,
            path_hash: paths.content_hash()?,
            road_hash: roads.content_hash()?,
            river_hash: rivers.content_hash()?,
            terrain: terrain.clone(),
            paths: paths.clone(),
            roads: roads.clone(),
            rivers: rivers.clone(),
        })
    }

    pub fn generate(&self) -> Result<RiverGenerationResult, RiverGenerationError> {
        let terrain_plan = TerrainGenerationPlan::from_document(&self.terrain)?;
        let generated_paths =
            PathGenerationPlan::from_documents(&self.terrain, &self.paths)?.generate()?;
        let mut remaining_rows = MAX_GENERATED_RIVER_LONGITUDINAL_SAMPLES;
        let mut prepared = Vec::new();
        prepared
            .try_reserve_exact(self.rivers.rivers.len())
            .map_err(|_| RiverGenerationError::AllocationFailed("prepared rivers"))?;

        for river in &self.rivers.rivers {
            let path = generated_paths
                .paths
                .binary_search_by_key(&river.path_id, |path| path.id)
                .ok()
                .map(|index| &generated_paths.paths[index])
                .ok_or(RiverGenerationError::MissingPath(river.path_id))?;
            let candidate = PreparedRiver::new(*river, path, &terrain_plan, remaining_rows)?;
            remaining_rows = remaining_rows
                .checked_sub(candidate.rows.len())
                .ok_or(RiverGenerationError::ArithmeticOverflow)?;
            prepared.push(candidate);
        }

        let mut rivers = Vec::new();
        rivers
            .try_reserve_exact(prepared.len())
            .map_err(|_| RiverGenerationError::AllocationFailed("generated rivers"))?;
        for river in &prepared {
            rivers.push(river.generate_mesh()?);
        }

        let prepared_intersections =
            validate_geometric_separation(&prepared, &self.roads, &generated_paths.paths)?;
        let effective_terrain = carve_effective_terrain(&self.terrain, &terrain_plan, &prepared)?;
        let effective_terrain_hash = effective_terrain.content_hash()?;
        let road_river_intersections = if prepared_intersections.is_empty() {
            Vec::new()
        } else {
            let effective_roads =
                RoadGenerationPlan::from_documents(&effective_terrain, &self.paths, &self.roads)?
                    .generate()?;
            materialize_terrain_fords(&prepared_intersections, &rivers, &effective_roads)?
        };

        let total_vertices = rivers.iter().try_fold(0_usize, |total, river| {
            total.checked_add(river.positions_m.len())
        });
        let total_indices = rivers.iter().try_fold(0_usize, |total, river| {
            total.checked_add(river.indices.len())
        });
        let (Some(total_vertices), Some(total_indices)) = (total_vertices, total_indices) else {
            return Err(RiverGenerationError::ArithmeticOverflow);
        };
        if total_vertices > MAX_GENERATED_RIVER_VERTICES
            || total_indices > MAX_GENERATED_RIVER_INDICES
        {
            return Err(RiverGenerationError::MeshBudgetExceeded {
                vertices: total_vertices,
                indices: total_indices,
            });
        }

        Ok(RiverGenerationResult {
            effective_terrain,
            generated: GeneratedRivers {
                generation_version: RIVER_GENERATION_VERSION,
                terrain_document_hash: self.terrain_hash,
                path_document_hash: self.path_hash,
                road_document_hash: self.road_hash,
                river_document_hash: self.river_hash,
                effective_terrain_document_hash: effective_terrain_hash,
                rivers,
                road_river_intersections,
            },
        })
    }
}

#[derive(Clone, Copy, Debug)]
struct RiverRow {
    xz_m: [f64; 2],
    distance_m: f64,
    tangent: [f64; 2],
    water_surface_m: f64,
}

#[derive(Clone, Debug)]
struct PreparedRiver {
    authored: AuthoredRiver,
    rows: Vec<RiverRow>,
}

#[derive(Clone, Copy, Debug)]
struct PreparedRoadRiverIntersection {
    river_path_id: PathId,
    road_path_id: PathId,
    position_xz_m: [f64; 2],
    crossing_angle_degrees: f64,
    river_distance_m: f64,
    road_distance_m: f64,
}

#[derive(Clone, Copy, Debug)]
struct PolylineCrossing {
    point_xz: [f64; 2],
    first_distance_m: f64,
    second_distance_m: f64,
}

impl PreparedRiver {
    fn new(
        authored: AuthoredRiver,
        path: &GeneratedPath,
        terrain: &TerrainGenerationPlan,
        maximum_rows: usize,
    ) -> Result<Self, RiverGenerationError> {
        if authored.channel.algorithm != RiverChannelProfileAlgorithm::TerrainCarvedChannelV1 {
            return Err(RiverGenerationError::UnsupportedAlgorithm);
        }
        let centreline = adaptive_centerline(path, maximum_rows)?;
        let outer_radius_m = f64::from(authored.channel.surface_width_m * 0.5)
            + f64::from(authored.channel.bank_blend_width_m);
        let mut rows = Vec::new();
        rows.try_reserve_exact(centreline.len())
            .map_err(|_| RiverGenerationError::AllocationFailed("river rows"))?;
        let mut downstream_surface_m = f64::INFINITY;

        for (row_index, row) in centreline.iter().enumerate() {
            let tangent = tangent_at(&centreline, row_index, authored.path_id)?;
            for (point_index, point) in [
                [row.xz_m[0] - outer_radius_m, row.xz_m[1]],
                [row.xz_m[0] + outer_radius_m, row.xz_m[1]],
                [row.xz_m[0], row.xz_m[1] - outer_radius_m],
                [row.xz_m[0], row.xz_m[1] + outer_radius_m],
            ]
            .into_iter()
            .enumerate()
            {
                if !inside_river_extent(point) {
                    return Err(RiverGenerationError::FootprintOutOfBounds {
                        path_id: authored.path_id,
                        row_index,
                        point_index,
                    });
                }
            }
            let raw_surface_m = sample_height_f64(terrain, row.xz_m, authored.path_id, row_index)?;
            downstream_surface_m = downstream_surface_m.min(raw_surface_m);
            if !downstream_surface_m.is_finite() {
                return Err(RiverGenerationError::InvalidDerivedValue {
                    path_id: authored.path_id,
                    field: "water surface",
                });
            }
            rows.push(RiverRow {
                xz_m: row.xz_m,
                distance_m: row.distance_m,
                tangent,
                water_surface_m: downstream_surface_m,
            });
        }
        Ok(Self { authored, rows })
    }

    fn footprint_radius_m(&self) -> f64 {
        f64::from(self.authored.channel.surface_width_m * 0.5)
            + f64::from(self.authored.channel.bank_blend_width_m)
    }

    fn generate_mesh(&self) -> Result<GeneratedRiver, RiverGenerationError> {
        let (vertex_count, index_count) = checked_mesh_counts(self.rows.len())?;
        let mut positions_m = Vec::new();
        let mut uvs = Vec::new();
        positions_m
            .try_reserve_exact(vertex_count)
            .map_err(|_| RiverGenerationError::AllocationFailed("river positions"))?;
        uvs.try_reserve_exact(vertex_count)
            .map_err(|_| RiverGenerationError::AllocationFailed("river UVs"))?;
        let half_width_m = f64::from(self.authored.channel.surface_width_m) * 0.5;
        let mut previous_distance = None;
        for (row_index, row) in self.rows.iter().enumerate() {
            let lateral = [-row.tangent[1], row.tangent[0]];
            let surface_y_m = row.water_surface_m + f64::from(RIVER_SURFACE_CLEARANCE_M);
            let distance_m = f32_from_derived(
                row.distance_m,
                self.authored.path_id,
                "longitudinal UV distance",
            )?;
            if previous_distance.is_some_and(|previous| distance_m <= previous) {
                return Err(RiverGenerationError::InvalidDerivedValue {
                    path_id: self.authored.path_id,
                    field: "longitudinal UV order",
                });
            }
            previous_distance = Some(distance_m);
            for (lane_index, (factor, lateral_uv)) in [(1.0_f64, 0.0_f32), (0.0, 0.5), (-1.0, 1.0)]
                .into_iter()
                .enumerate()
            {
                let position = [
                    row.xz_m[0] + lateral[0] * half_width_m * factor,
                    surface_y_m,
                    row.xz_m[1] + lateral[1] * half_width_m * factor,
                ];
                if !inside_river_extent([position[0], position[2]]) {
                    return Err(RiverGenerationError::FootprintOutOfBounds {
                        path_id: self.authored.path_id,
                        row_index,
                        point_index: lane_index,
                    });
                }
                positions_m.push([
                    f32_from_derived(position[0], self.authored.path_id, "mesh X")?,
                    f32_from_derived(position[1], self.authored.path_id, "mesh Y")?,
                    f32_from_derived(position[2], self.authored.path_id, "mesh Z")?,
                ]);
                uvs.push([lateral_uv, distance_m]);
            }
        }
        let indices = build_indices(self.rows.len(), index_count)?;
        let normals = build_normals(self.authored.path_id, &positions_m, &indices)?;
        Ok(GeneratedRiver {
            path_id: self.authored.path_id,
            channel: self.authored.channel,
            surface: self.authored.surface,
            positions_m,
            normals,
            uvs,
            indices,
        })
    }
}

fn materialize_terrain_fords(
    prepared: &[PreparedRoadRiverIntersection],
    rivers: &[GeneratedRiver],
    roads: &GeneratedRoads,
) -> Result<Vec<GeneratedRoadRiverIntersection>, RiverGenerationError> {
    let mut generated = Vec::new();
    generated
        .try_reserve_exact(prepared.len())
        .map_err(|_| RiverGenerationError::AllocationFailed("generated terrain fords"))?;
    for intersection in prepared {
        let river = rivers
            .binary_search_by_key(&intersection.river_path_id, |river| river.path_id)
            .ok()
            .map(|index| &rivers[index])
            .ok_or(RiverGenerationError::MissingPath(
                intersection.river_path_id,
            ))?;
        let road = roads
            .roads
            .binary_search_by_key(&intersection.road_path_id, |road| road.path_id)
            .ok()
            .map(|index| &roads.roads[index])
            .ok_or(RiverGenerationError::MissingRoadPath(
                intersection.road_path_id,
            ))?;
        let x_m = f32_from_derived(
            intersection.position_xz_m[0],
            intersection.river_path_id,
            "ford X",
        )?;
        let z_m = f32_from_derived(
            intersection.position_xz_m[1],
            intersection.river_path_id,
            "ford Z",
        )?;
        let road_surface_height_m = generated_surface_height_at_distance(
            road.path_id,
            &road.positions_m,
            intersection.road_distance_m,
            "ford road surface",
        )?;
        let water_surface_height_m = generated_surface_height_at_distance(
            river.path_id,
            &river.positions_m,
            intersection.river_distance_m,
            "ford water surface",
        )?;
        let water_depth_m = water_surface_height_m - road_surface_height_m;
        if !road_surface_height_m.is_finite()
            || !water_depth_m.is_finite()
            || water_depth_m < MIN_TERRAIN_FORD_WATER_DEPTH_M
        {
            return Err(RiverGenerationError::InsufficientTerrainFordWaterDepth {
                river_path_id: intersection.river_path_id,
                road_path_id: intersection.road_path_id,
                depth_m: water_depth_m,
                minimum_m: MIN_TERRAIN_FORD_WATER_DEPTH_M,
            });
        }
        generated.push(GeneratedRoadRiverIntersection {
            river_path_id: intersection.river_path_id,
            road_path_id: intersection.road_path_id,
            kind: RoadRiverIntersectionKind::TerrainFordV1,
            position_xz_m: [x_m, z_m],
            crossing_angle_degrees: f32_from_derived(
                intersection.crossing_angle_degrees,
                intersection.river_path_id,
                "ford crossing angle",
            )?,
            road_surface_height_m: canonicalize_zero(road_surface_height_m),
            water_surface_height_m,
        });
    }
    Ok(generated)
}

fn generated_surface_height_at_distance(
    path_id: PathId,
    positions_m: &[[f32; 3]],
    distance_m: f64,
    field: &'static str,
) -> Result<f32, RiverGenerationError> {
    let (rows, remainder) = positions_m.as_chunks::<RIVER_CROSS_SECTION_VERTEX_COUNT>();
    if !remainder.is_empty() || rows.len() < 2 || !distance_m.is_finite() || distance_m < 0.0 {
        return Err(
            RiverGenerationError::InvalidGeneratedRoadRiverIntersections(
                "surface centreline is incomplete",
            ),
        );
    }
    let mut row_distance_m = 0.0_f64;
    for pair in rows.windows(2) {
        let first = pair[0][1];
        let second = pair[1][1];
        let segment_distance_m =
            f64::from(second[0] - first[0]).hypot(f64::from(second[2] - first[2]));
        if !segment_distance_m.is_finite() || segment_distance_m <= 0.0 {
            return Err(
                RiverGenerationError::InvalidGeneratedRoadRiverIntersections(
                    "surface centreline is degenerate",
                ),
            );
        }
        let next_distance_m = row_distance_m + segment_distance_m;
        if distance_m <= next_distance_m + TERRAIN_FORD_POINT_MERGE_DISTANCE_M {
            let fraction = ((distance_m - row_distance_m) / segment_distance_m).clamp(0.0, 1.0);
            return f32_from_derived(
                linear_interpolate_f64(f64::from(first[1]), f64::from(second[1]), fraction),
                path_id,
                field,
            );
        }
        row_distance_m = next_distance_m;
    }
    Err(
        RiverGenerationError::InvalidGeneratedRoadRiverIntersections(
            "intersection lies outside its surface centreline",
        ),
    )
}

fn generated_centerline_points(
    path_id: PathId,
    positions_m: &[[f32; 3]],
) -> Result<Vec<[f64; 2]>, RiverGenerationError> {
    let (rows, remainder) = positions_m.as_chunks::<RIVER_CROSS_SECTION_VERTEX_COUNT>();
    if !remainder.is_empty() || rows.len() < 2 {
        return Err(
            RiverGenerationError::InvalidGeneratedRoadRiverIntersections(
                "surface centreline is incomplete",
            ),
        );
    }
    let mut points = Vec::new();
    points
        .try_reserve_exact(rows.len())
        .map_err(|_| RiverGenerationError::AllocationFailed("generated centreline"))?;
    for row in rows {
        let centre = row[1];
        if !centre.iter().all(|value| value.is_finite())
            || !inside_river_extent([f64::from(centre[0]), f64::from(centre[2])])
        {
            return Err(RiverGenerationError::InvalidDerivedValue {
                path_id,
                field: "generated centreline",
            });
        }
        points.push([f64::from(centre[0]), f64::from(centre[2])]);
    }
    polyline_distances(&points)?;
    Ok(points)
}

fn generated_path_points(path: &GeneratedPath) -> Result<Vec<[f64; 2]>, RiverGenerationError> {
    if path.samples.len() < 2 {
        return Err(RiverGenerationError::InvalidOverlapPolyline);
    }
    let mut points = Vec::new();
    points
        .try_reserve_exact(path.samples.len())
        .map_err(|_| RiverGenerationError::AllocationFailed("generated path centreline"))?;
    for sample in &path.samples {
        if !sample.position_m.is_finite() {
            return Err(
                RiverGenerationError::InvalidGeneratedRoadRiverIntersections(
                    "road path contains a non-finite sample",
                ),
            );
        }
        points.push([
            f64::from(sample.position_m.x),
            f64::from(sample.position_m.z),
        ]);
    }
    polyline_distances(&points)?;
    Ok(points)
}

fn validate_generated_terrain_ford(
    expected: &PreparedRoadRiverIntersection,
    actual: &GeneratedRoadRiverIntersection,
    roads: &GeneratedRoads,
    rivers: &GeneratedRivers,
) -> Result<(), RiverGenerationError> {
    if actual.river_path_id != expected.river_path_id
        || actual.road_path_id != expected.road_path_id
        || actual.kind != RoadRiverIntersectionKind::TerrainFordV1
    {
        return Err(
            RiverGenerationError::InvalidGeneratedRoadRiverIntersections(
                "metadata identity or kind differs from derived geometry",
            ),
        );
    }
    let expected_position_xz_m = [
        f32_from_derived(expected.position_xz_m[0], expected.river_path_id, "ford X")?,
        f32_from_derived(expected.position_xz_m[1], expected.river_path_id, "ford Z")?,
    ];
    let expected_angle_degrees = f32_from_derived(
        expected.crossing_angle_degrees,
        expected.river_path_id,
        "ford crossing angle",
    )?;
    let road = roads
        .roads
        .binary_search_by_key(&expected.road_path_id, |road| road.path_id)
        .ok()
        .map(|index| &roads.roads[index])
        .ok_or(RiverGenerationError::MissingRoadPath(expected.road_path_id))?;
    let river = rivers
        .rivers
        .binary_search_by_key(&expected.river_path_id, |river| river.path_id)
        .ok()
        .map(|index| &rivers.rivers[index])
        .ok_or(RiverGenerationError::MissingPath(expected.river_path_id))?;
    let expected_road_height_m = generated_surface_height_at_distance(
        road.path_id,
        &road.positions_m,
        expected.road_distance_m,
        "ford road surface",
    )?;
    let expected_water_height_m = generated_surface_height_at_distance(
        river.path_id,
        &river.positions_m,
        expected.river_distance_m,
        "ford water surface",
    )?;
    let exact_values_match = actual.position_xz_m.map(f32::to_bits)
        == expected_position_xz_m.map(f32::to_bits)
        && actual.crossing_angle_degrees.to_bits() == expected_angle_degrees.to_bits()
        && actual.road_surface_height_m.to_bits() == expected_road_height_m.to_bits()
        && actual.water_surface_height_m.to_bits() == expected_water_height_m.to_bits();
    let residual_depth_m = actual.water_surface_height_m - actual.road_surface_height_m;
    if !exact_values_match
        || !actual.position_xz_m.iter().all(|value| value.is_finite())
        || !actual.crossing_angle_degrees.is_finite()
        || !(MIN_TERRAIN_FORD_CROSSING_ANGLE_DEGREES..=MAX_TERRAIN_FORD_CROSSING_ANGLE_DEGREES)
            .contains(&actual.crossing_angle_degrees)
        || !residual_depth_m.is_finite()
        || residual_depth_m < MIN_TERRAIN_FORD_WATER_DEPTH_M
    {
        return Err(
            RiverGenerationError::InvalidGeneratedRoadRiverIntersections(
                "metadata values differ from visible generated surfaces",
            ),
        );
    }
    Ok(())
}

#[derive(Clone, Copy)]
struct CenterlineRow {
    xz_m: [f64; 2],
    distance_m: f64,
}

fn adaptive_centerline(
    path: &GeneratedPath,
    maximum_rows: usize,
) -> Result<Vec<CenterlineRow>, RiverGenerationError> {
    if maximum_rows < 2 || path.samples.len() < 2 {
        return Err(RiverGenerationError::LongitudinalSampleBudgetExceeded {
            requested: path.samples.len().max(2),
            maximum: maximum_rows,
        });
    }
    let first = path.samples[0].position_m;
    let mut rows = Vec::new();
    rows.try_reserve(path.samples.len().min(maximum_rows))
        .map_err(|_| RiverGenerationError::AllocationFailed("adaptive centreline"))?;
    rows.push(CenterlineRow {
        xz_m: [f64::from(first.x), f64::from(first.z)],
        distance_m: 0.0,
    });
    let mut total_distance_m = 0.0_f64;
    for (sample_index, pair) in path.samples.windows(2).enumerate() {
        let first = pair[0].position_m;
        let second = pair[1].position_m;
        let delta = [
            f64::from(second.x) - f64::from(first.x),
            f64::from(second.z) - f64::from(first.z),
        ];
        let squared_distance = delta[0] * delta[0] + delta[1] * delta[1];
        if !squared_distance.is_finite() || squared_distance <= MIN_HORIZONTAL_DELTA_SQUARED {
            return Err(RiverGenerationError::InvalidTangent {
                path_id: path.id,
                sample_index,
            });
        }
        let distance_m = squared_distance.sqrt();
        let required_sections = (distance_m / RIVER_RESAMPLE_TARGET_CHORD_M).ceil().max(1.0);
        let maximum_rows_u32 =
            u32::try_from(maximum_rows).map_err(|_| RiverGenerationError::ArithmeticOverflow)?;
        if !required_sections.is_finite() || required_sections > f64::from(maximum_rows_u32) {
            return Err(RiverGenerationError::LongitudinalSampleBudgetExceeded {
                requested: maximum_rows.saturating_add(1),
                maximum: maximum_rows,
            });
        }
        #[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
        let subdivisions = usize::try_from(required_sections as u32)
            .map_err(|_| RiverGenerationError::ArithmeticOverflow)?;
        let requested = rows
            .len()
            .checked_add(subdivisions)
            .ok_or(RiverGenerationError::ArithmeticOverflow)?;
        if requested > maximum_rows {
            return Err(RiverGenerationError::LongitudinalSampleBudgetExceeded {
                requested,
                maximum: maximum_rows,
            });
        }
        for subdivision in 1..=subdivisions {
            let xz_m = if subdivision == subdivisions {
                [f64::from(second.x), f64::from(second.z)]
            } else {
                #[allow(clippy::cast_precision_loss)]
                let fraction = subdivision as f64 / subdivisions as f64;
                [
                    f64::from(first.x) + delta[0] * fraction,
                    f64::from(first.z) + delta[1] * fraction,
                ]
            };
            let previous = rows
                .last()
                .ok_or(RiverGenerationError::ArithmeticOverflow)?;
            let step_distance_m = (xz_m[0] - previous.xz_m[0]).hypot(xz_m[1] - previous.xz_m[1]);
            if !step_distance_m.is_finite()
                || step_distance_m <= MIN_HORIZONTAL_DELTA_SQUARED.sqrt()
                || step_distance_m > RIVER_MESH_MAX_LONGITUDINAL_CHORD_M
            {
                return Err(RiverGenerationError::InvalidTangent {
                    path_id: path.id,
                    sample_index,
                });
            }
            total_distance_m += step_distance_m;
            if !total_distance_m.is_finite() {
                return Err(RiverGenerationError::InvalidDerivedValue {
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
) -> Result<[f64; 2], RiverGenerationError> {
    let last = rows
        .len()
        .checked_sub(1)
        .ok_or(RiverGenerationError::ArithmeticOverflow)?;
    let current = rows
        .get(index)
        .ok_or(RiverGenerationError::ArithmeticOverflow)?;
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
            let inverse_length = squared_length.sqrt().recip();
            return Ok([tangent[0] * inverse_length, tangent[1] * inverse_length]);
        }
    }
    Err(RiverGenerationError::InvalidTangent {
        path_id,
        sample_index: index,
    })
}

fn sample_height_f64(
    terrain: &TerrainGenerationPlan,
    point_xz_m: [f64; 2],
    path_id: PathId,
    sample_index: usize,
) -> Result<f64, RiverGenerationError> {
    let x_m = f32_from_derived(point_xz_m[0], path_id, "sample X")?;
    let z_m = f32_from_derived(point_xz_m[1], path_id, "sample Z")?;
    terrain
        .height_at(x_m, z_m)
        .map(f64::from)
        .map_err(|source| RiverGenerationError::TerrainSample {
            path_id,
            sample_index,
            source,
        })
}

#[derive(Clone, Copy)]
struct TerrainInfluence {
    canonical_height_m: f32,
    target_height_m: f32,
    earth_blend: f32,
}

fn carve_effective_terrain(
    canonical: &TerrainDocument,
    terrain: &TerrainGenerationPlan,
    rivers: &[PreparedRiver],
) -> Result<TerrainDocument, RiverGenerationError> {
    if rivers.is_empty() {
        return Ok(canonical.clone());
    }
    let grid_axis = usize::from(TERRAIN_GLOBAL_VERTICES_PER_AXIS);
    let grid_len = grid_axis
        .checked_mul(grid_axis)
        .ok_or(RiverGenerationError::ArithmeticOverflow)?;
    let mut influences = Vec::new();
    influences
        .try_reserve_exact(grid_len)
        .map_err(|_| RiverGenerationError::AllocationFailed("terrain influences"))?;
    influences.resize(grid_len, None);

    for river in rivers {
        rasterize_river_influences(terrain, river, &mut influences, grid_axis)?;
    }
    let affected_count = influences.iter().flatten().count();
    ensure_effective_sample_budget(affected_count)?;

    let mut effective = canonical.migrate_to_latest()?;
    let mut height_deltas = canonical
        .height_deltas
        .iter()
        .map(|delta| (delta.sort_key(), delta.delta_m))
        .collect::<BTreeMap<_, _>>();
    let mut material_weights = canonical
        .material_weight_overrides
        .iter()
        .map(|weights| (weights.sort_key(), weights.weights))
        .collect::<BTreeMap<_, _>>();

    for (index, influence) in influences.into_iter().enumerate() {
        let Some(influence) = influence else {
            continue;
        };
        let sample_x = u16::try_from(index % grid_axis)
            .map_err(|_| RiverGenerationError::ArithmeticOverflow)?;
        let sample_z = u16::try_from(index / grid_axis)
            .map_err(|_| RiverGenerationError::ArithmeticOverflow)?;
        let key = (sample_z, sample_x);
        if influence.target_height_m < influence.canonical_height_m {
            let source_delta = height_deltas.get(&key).copied().unwrap_or(0.0);
            let adjustment = influence.target_height_m - influence.canonical_height_m;
            let derived_delta = source_delta + adjustment;
            if !derived_delta.is_finite() {
                return Err(RiverGenerationError::InvalidEffectiveTerrainSample {
                    sample_x,
                    sample_z,
                });
            }
            if derived_delta == 0.0 {
                height_deltas.remove(&key);
            } else {
                height_deltas.insert(key, canonicalize_zero(derived_delta));
            }
        }

        if influence.earth_blend > 0.0 {
            let source = terrain
                .material_weights_at_sample(sample_x, sample_z)?
                .as_array();
            let derived = blend_to_earth(source, influence.earth_blend)?;
            if derived.map(f32::to_bits) != source.map(f32::to_bits) {
                material_weights.insert(key, derived);
            }
        }
    }

    if height_deltas.len() > MAX_TERRAIN_HEIGHT_DELTAS
        || material_weights.len() > MAX_TERRAIN_MATERIAL_WEIGHT_OVERRIDES
    {
        return Err(RiverGenerationError::EffectiveTerrainSampleBudgetExceeded {
            requested: height_deltas.len().max(material_weights.len()),
            maximum: MAX_EFFECTIVE_RIVER_TERRAIN_SAMPLES,
        });
    }
    effective.height_deltas = height_deltas
        .into_iter()
        .map(|((sample_z, sample_x), delta_m)| TerrainHeightDelta::new(sample_x, sample_z, delta_m))
        .collect();
    effective.material_weight_overrides = material_weights
        .into_iter()
        .map(|((sample_z, sample_x), weights)| {
            TerrainMaterialWeightOverride::new(sample_x, sample_z, weights)
        })
        .collect();
    effective.validate()?;
    Ok(effective)
}

fn rasterize_river_influences(
    terrain: &TerrainGenerationPlan,
    river: &PreparedRiver,
    influences: &mut [Option<TerrainInfluence>],
    grid_axis: usize,
) -> Result<(), RiverGenerationError> {
    let half_width_m = f64::from(river.authored.channel.surface_width_m) * 0.5;
    let bank_blend_m = f64::from(river.authored.channel.bank_blend_width_m);
    let outer_radius_m = half_width_m + bank_blend_m;
    let bed_depth_m = f64::from(river.authored.channel.bed_depth_m);
    for segment in river.rows.windows(2) {
        let start = segment[0];
        let end = segment[1];
        let ([minimum_x, minimum_z], [maximum_x, maximum_z]) =
            expanded_segment_bounds(start.xz_m, end.xz_m, outer_radius_m);
        let (minimum_sample_x, maximum_sample_x) = grid_index_range(minimum_x, maximum_x)?;
        let (minimum_sample_z, maximum_sample_z) = grid_index_range(minimum_z, maximum_z)?;
        for sample_z in minimum_sample_z..=maximum_sample_z {
            for sample_x in minimum_sample_x..=maximum_sample_x {
                let point = sample_xz(sample_x, sample_z);
                let (distance_squared_m, fraction) =
                    point_segment_distance_squared(point, start.xz_m, end.xz_m);
                if !distance_squared_m.is_finite()
                    || !fraction.is_finite()
                    || distance_squared_m > outer_radius_m * outer_radius_m
                {
                    continue;
                }
                let distance_m = distance_squared_m.sqrt();
                let canonical_height_m =
                    terrain.height_at(f32_from_plain(point[0])?, f32_from_plain(point[1])?)?;
                let surface_m =
                    linear_interpolate_f64(start.water_surface_m, end.water_surface_m, fraction);
                let (target_m, earth_blend) = if distance_m <= half_width_m {
                    let fraction = (distance_m / half_width_m).clamp(0.0, 1.0);
                    let depth_fraction = 1.0 - smoothstep(fraction);
                    (surface_m - bed_depth_m * depth_fraction, 1.0)
                } else {
                    let fraction = ((distance_m - half_width_m) / bank_blend_m).clamp(0.0, 1.0);
                    let blend = smoothstep(fraction);
                    (
                        linear_interpolate_f64(surface_m, f64::from(canonical_height_m), blend),
                        1.0 - blend,
                    )
                };
                let target_height_m = f32_from_derived(
                    target_m.min(f64::from(canonical_height_m)),
                    river.authored.path_id,
                    "carved terrain height",
                )?;
                let earth_blend = f32_from_derived(
                    earth_blend.clamp(0.0, 1.0),
                    river.authored.path_id,
                    "earth blend",
                )?;
                if target_height_m.to_bits() == canonical_height_m.to_bits() && earth_blend == 0.0 {
                    continue;
                }
                let index = usize::from(sample_z)
                    .checked_mul(grid_axis)
                    .and_then(|value| value.checked_add(usize::from(sample_x)))
                    .ok_or(RiverGenerationError::ArithmeticOverflow)?;
                let slot = influences
                    .get_mut(index)
                    .ok_or(RiverGenerationError::ArithmeticOverflow)?;
                match slot {
                    Some(current) => {
                        current.target_height_m = current.target_height_m.min(target_height_m);
                        current.earth_blend = current.earth_blend.max(earth_blend);
                    }
                    None => {
                        *slot = Some(TerrainInfluence {
                            canonical_height_m,
                            target_height_m,
                            earth_blend,
                        });
                    }
                }
            }
        }
    }
    Ok(())
}

fn blend_to_earth(source: [f32; 3], earth_blend: f32) -> Result<[f32; 3], RiverGenerationError> {
    if !earth_blend.is_finite() || !(0.0..=1.0).contains(&earth_blend) {
        return Err(RiverGenerationError::InvalidMaterialBlend);
    }
    if earth_blend.to_bits() == 1.0_f32.to_bits() {
        return Ok([0.0, 1.0, 0.0]);
    }
    let remaining = 1.0 - earth_blend;
    let mut result = [
        source[0] * remaining,
        source[1] * remaining + earth_blend,
        source[2] * remaining,
    ];
    let sum = result.iter().sum::<f32>();
    if !sum.is_finite() || sum <= 0.0 {
        return Err(RiverGenerationError::InvalidMaterialBlend);
    }
    for value in &mut result {
        *value = canonicalize_zero(*value / sum);
    }
    Ok(result)
}

fn validate_geometric_separation(
    rivers: &[PreparedRiver],
    roads: &RoadDocument,
    paths: &[GeneratedPath],
) -> Result<Vec<PreparedRoadRiverIntersection>, RiverGenerationError> {
    let mut comparisons = 0_usize;
    let mut river_points = Vec::new();
    river_points
        .try_reserve_exact(rivers.len())
        .map_err(|_| RiverGenerationError::AllocationFailed("river overlap points"))?;
    for river in rivers {
        let mut points = Vec::new();
        points
            .try_reserve_exact(river.rows.len())
            .map_err(|_| RiverGenerationError::AllocationFailed("river overlap polyline"))?;
        for row in &river.rows {
            points.push([
                f64::from(f32_from_derived(
                    row.xz_m[0],
                    river.authored.path_id,
                    "ford X",
                )?),
                f64::from(f32_from_derived(
                    row.xz_m[1],
                    river.authored.path_id,
                    "ford Z",
                )?),
            ]);
        }
        river_points.push(points);
    }
    for first_index in 0..rivers.len() {
        for (second_offset, second) in rivers[first_index + 1..].iter().enumerate() {
            let first = &rivers[first_index];
            if polylines_overlap(
                &river_points[first_index],
                first.footprint_radius_m(),
                &river_points[first_index + second_offset + 1],
                second.footprint_radius_m(),
                &mut comparisons,
            )? {
                return Err(RiverGenerationError::RiverOverlap {
                    first_path_id: first.authored.path_id,
                    second_path_id: second.authored.path_id,
                });
            }
        }
    }
    let mut intersections = Vec::new();
    intersections
        .try_reserve_exact(rivers.len().min(MAX_GENERATED_ROAD_RIVER_INTERSECTIONS))
        .map_err(|_| RiverGenerationError::AllocationFailed("road-river intersections"))?;
    for (river_index, river) in rivers.iter().enumerate() {
        for road in &roads.roads {
            let path = paths
                .binary_search_by_key(&road.path_id, |path| path.id)
                .ok()
                .map(|index| &paths[index])
                .ok_or(RiverGenerationError::MissingRoadPath(road.path_id))?;
            let mut road_points = Vec::new();
            road_points
                .try_reserve_exact(path.samples.len())
                .map_err(|_| RiverGenerationError::AllocationFailed("road overlap polyline"))?;
            road_points.extend(path.samples.iter().map(|sample| {
                [
                    f64::from(sample.position_m.x),
                    f64::from(sample.position_m.z),
                ]
            }));
            if !polylines_overlap(
                &river_points[river_index],
                river.footprint_radius_m(),
                &road_points,
                f64::from(road.profile.width_m) * 0.5,
                &mut comparisons,
            )? {
                continue;
            }
            intersections.push(prepare_terrain_ford(
                river.authored.path_id,
                river.footprint_radius_m(),
                &river_points[river_index],
                road,
                &road_points,
                &mut comparisons,
            )?);
            if intersections.len() > MAX_GENERATED_ROAD_RIVER_INTERSECTIONS {
                return Err(RiverGenerationError::TooManyRoadRiverIntersections {
                    found: intersections.len(),
                    maximum: MAX_GENERATED_ROAD_RIVER_INTERSECTIONS,
                });
            }
        }
    }
    intersections
        .sort_by_key(|intersection| (intersection.river_path_id, intersection.road_path_id));
    if intersections.windows(2).any(|pair| {
        pair[0].river_path_id == pair[1].river_path_id
            && pair[0].road_path_id == pair[1].road_path_id
    }) {
        return Err(RiverGenerationError::ArithmeticOverflow);
    }
    Ok(intersections)
}

fn prepare_terrain_ford(
    river_path_id: PathId,
    river_radius_m: f64,
    river_points: &[[f64; 2]],
    road: &AuthoredRoad,
    road_points: &[[f64; 2]],
    comparisons: &mut usize,
) -> Result<PreparedRoadRiverIntersection, RiverGenerationError> {
    let crossings = polyline_intersections(river_points, road_points, comparisons)?;
    let [crossing] = crossings.as_slice() else {
        return if crossings.is_empty() {
            Err(RiverGenerationError::RiverRoadOverlap {
                river_path_id,
                road_path_id: road.path_id,
            })
        } else {
            Err(RiverGenerationError::MultipleRiverRoadIntersections {
                river_path_id,
                road_path_id: road.path_id,
                found: crossings.len(),
            })
        };
    };

    let river_distances = polyline_distances(river_points)?;
    let road_distances = polyline_distances(road_points)?;
    let river_total_distance_m = *river_distances
        .last()
        .ok_or(RiverGenerationError::InvalidOverlapPolyline)?;
    let road_total_distance_m = *road_distances
        .last()
        .ok_or(RiverGenerationError::InvalidOverlapPolyline)?;
    let endpoint_distances = [
        (crossing.first_distance_m, river_total_distance_m, "river"),
        (crossing.second_distance_m, road_total_distance_m, "road"),
    ];
    ensure_ford_endpoint_clearance(
        river_path_id,
        road.path_id,
        endpoint_distances,
        TERRAIN_FORD_TOPOLOGY_PROBE_DISTANCE_M,
        true,
    )?;
    let river_probe = crossing_probe(river_points, &river_distances, crossing.first_distance_m)?;
    let road_probe = crossing_probe(road_points, &road_distances, crossing.second_distance_m)?;
    let crossing_angle_degrees =
        validate_ford_crossing_angle(river_path_id, road.path_id, river_probe, road_probe)?;

    let crossing_sine = crossing_angle_degrees.to_radians().sin();
    if !crossing_sine.is_finite() || crossing_sine <= 0.0 {
        return Err(RiverGenerationError::TangentialRiverRoadIntersection {
            river_path_id,
            road_path_id: road.path_id,
        });
    }
    let combined_radius_m = river_radius_m + f64::from(road.profile.width_m) * 0.5;
    let local_radius_m = combined_radius_m / crossing_sine + TERRAIN_FORD_SAMPLE_GUARD_M;
    ensure_ford_endpoint_clearance(
        river_path_id,
        road.path_id,
        endpoint_distances,
        local_radius_m,
        false,
    )?;
    if !overlap_is_local_to_crossing(
        river_points,
        river_radius_m,
        road_points,
        f64::from(road.profile.width_m) * 0.5,
        crossing.point_xz,
        local_radius_m,
        comparisons,
    )? {
        return Err(RiverGenerationError::ExtendedRiverRoadOverlap {
            river_path_id,
            road_path_id: road.path_id,
        });
    }

    Ok(PreparedRoadRiverIntersection {
        river_path_id,
        road_path_id: road.path_id,
        position_xz_m: crossing.point_xz,
        crossing_angle_degrees,
        river_distance_m: crossing.first_distance_m,
        road_distance_m: crossing.second_distance_m,
    })
}

fn ensure_ford_endpoint_clearance(
    river_path_id: PathId,
    road_path_id: PathId,
    distances: [(f64, f64, &'static str); 2],
    required_clearance_m: f64,
    reject_equal: bool,
) -> Result<(), RiverGenerationError> {
    for (distance_m, total_distance_m, role) in distances {
        let upstream_m = distance_m;
        let downstream_m = total_distance_m - distance_m;
        let too_close = if reject_equal {
            upstream_m <= required_clearance_m || downstream_m <= required_clearance_m
        } else {
            upstream_m < required_clearance_m || downstream_m < required_clearance_m
        };
        if too_close {
            return Err(RiverGenerationError::TerrainFordNearEndpoint {
                river_path_id,
                road_path_id,
                role,
                required_clearance_m: f32_from_derived(
                    required_clearance_m,
                    river_path_id,
                    "ford endpoint clearance",
                )?,
            });
        }
    }
    Ok(())
}

fn validate_ford_crossing_angle(
    river_path_id: PathId,
    road_path_id: PathId,
    river_probe: [[f64; 2]; 2],
    road_probe: [[f64; 2]; 2],
) -> Result<f64, RiverGenerationError> {
    if !crosses_line(river_probe, road_probe) || !crosses_line(road_probe, river_probe) {
        return Err(RiverGenerationError::TangentialRiverRoadIntersection {
            river_path_id,
            road_path_id,
        });
    }
    let river_tangent = subtract(river_probe[1], river_probe[0]);
    let road_tangent = subtract(road_probe[1], road_probe[0]);
    let angle_degrees = acute_angle_degrees(river_tangent, road_tangent)?;
    if angle_degrees < f64::from(MIN_TERRAIN_FORD_CROSSING_ANGLE_DEGREES) {
        return Err(RiverGenerationError::ShallowTerrainFordAngle {
            river_path_id,
            road_path_id,
            angle_degrees: f32_from_derived(angle_degrees, river_path_id, "ford crossing angle")?,
            minimum_degrees: MIN_TERRAIN_FORD_CROSSING_ANGLE_DEGREES,
        });
    }
    Ok(angle_degrees)
}

fn polyline_intersections(
    first: &[[f64; 2]],
    second: &[[f64; 2]],
    comparisons: &mut usize,
) -> Result<Vec<PolylineCrossing>, RiverGenerationError> {
    let first_distances = polyline_distances(first)?;
    let second_distances = polyline_distances(second)?;
    let mut intersections = Vec::<PolylineCrossing>::new();
    for (first_index, first_segment) in first.windows(2).enumerate() {
        let first_bounds = expanded_segment_bounds(first_segment[0], first_segment[1], 0.0);
        for (second_index, second_segment) in second.windows(2).enumerate() {
            let second_bounds = expanded_segment_bounds(second_segment[0], second_segment[1], 0.0);
            if !bounds_overlap(first_bounds, second_bounds) {
                continue;
            }
            consume_overlap_comparison(comparisons)?;
            let Some((first_fraction, second_fraction, position_xz_m)) =
                segment_intersection_parameters(
                    first_segment[0],
                    first_segment[1],
                    second_segment[0],
                    second_segment[1],
                )
            else {
                continue;
            };
            let first_distance_m = first_distances[first_index]
                + segment_length(first_segment[0], first_segment[1])? * first_fraction;
            let second_distance_m = second_distances[second_index]
                + segment_length(second_segment[0], second_segment[1])? * second_fraction;
            let candidate = PolylineCrossing {
                point_xz: position_xz_m,
                first_distance_m,
                second_distance_m,
            };
            let merge_distance_squared = TERRAIN_FORD_POINT_MERGE_DISTANCE_M.powi(2);
            if intersections.iter().any(|existing| {
                squared_distance(existing.point_xz, candidate.point_xz) <= merge_distance_squared
                    && (existing.first_distance_m - candidate.first_distance_m).abs()
                        <= TERRAIN_FORD_POINT_MERGE_DISTANCE_M
                    && (existing.second_distance_m - candidate.second_distance_m).abs()
                        <= TERRAIN_FORD_POINT_MERGE_DISTANCE_M
            }) {
                continue;
            }
            intersections
                .try_reserve(1)
                .map_err(|_| RiverGenerationError::AllocationFailed("polyline intersections"))?;
            intersections.push(candidate);
            if intersections.len() == 2 {
                return Ok(intersections);
            }
        }
    }
    Ok(intersections)
}

fn polyline_distances(points: &[[f64; 2]]) -> Result<Vec<f64>, RiverGenerationError> {
    if points.len() < 2 {
        return Err(RiverGenerationError::InvalidOverlapPolyline);
    }
    let mut distances = Vec::new();
    distances
        .try_reserve_exact(points.len())
        .map_err(|_| RiverGenerationError::AllocationFailed("polyline distances"))?;
    distances.push(0.0);
    for segment in points.windows(2) {
        let distance = distances
            .last()
            .copied()
            .ok_or(RiverGenerationError::ArithmeticOverflow)?
            + segment_length(segment[0], segment[1])?;
        if !distance.is_finite() {
            return Err(RiverGenerationError::InvalidOverlapPolyline);
        }
        distances.push(distance);
    }
    Ok(distances)
}

fn crossing_probe(
    points: &[[f64; 2]],
    distances: &[f64],
    crossing_distance_m: f64,
) -> Result<[[f64; 2]; 2], RiverGenerationError> {
    let total_distance_m = *distances
        .last()
        .ok_or(RiverGenerationError::InvalidOverlapPolyline)?;
    let before_distance_m = crossing_distance_m - TERRAIN_FORD_TOPOLOGY_PROBE_DISTANCE_M;
    let after_distance_m = crossing_distance_m + TERRAIN_FORD_TOPOLOGY_PROBE_DISTANCE_M;
    if before_distance_m <= 0.0 || after_distance_m >= total_distance_m {
        return Err(RiverGenerationError::InvalidOverlapPolyline);
    }
    Ok([
        sample_polyline_at_distance(points, distances, before_distance_m)?,
        sample_polyline_at_distance(points, distances, after_distance_m)?,
    ])
}

fn sample_polyline_at_distance(
    points: &[[f64; 2]],
    distances: &[f64],
    distance_m: f64,
) -> Result<[f64; 2], RiverGenerationError> {
    if points.len() != distances.len() || !distance_m.is_finite() {
        return Err(RiverGenerationError::InvalidOverlapPolyline);
    }
    for (index, interval) in distances.windows(2).enumerate() {
        if distance_m <= interval[1] {
            let span = interval[1] - interval[0];
            if !span.is_finite() || span <= 0.0 {
                return Err(RiverGenerationError::InvalidOverlapPolyline);
            }
            let fraction = ((distance_m - interval[0]) / span).clamp(0.0, 1.0);
            return Ok([
                linear_interpolate_f64(points[index][0], points[index + 1][0], fraction),
                linear_interpolate_f64(points[index][1], points[index + 1][1], fraction),
            ]);
        }
    }
    points
        .last()
        .copied()
        .ok_or(RiverGenerationError::InvalidOverlapPolyline)
}

fn crosses_line(first: [[f64; 2]; 2], second: [[f64; 2]; 2]) -> bool {
    opposite_sides(
        orientation(second[0], second[1], first[0]),
        orientation(second[0], second[1], first[1]),
    )
}

fn acute_angle_degrees(first: [f64; 2], second: [f64; 2]) -> Result<f64, RiverGenerationError> {
    let first_length = first[0].hypot(first[1]);
    let second_length = second[0].hypot(second[1]);
    if !first_length.is_finite()
        || !second_length.is_finite()
        || first_length <= 0.0
        || second_length <= 0.0
    {
        return Err(RiverGenerationError::InvalidOverlapPolyline);
    }
    let cross = (first[0] * second[1] - first[1] * second[0]).abs();
    let dot = (first[0] * second[0] + first[1] * second[1]).abs();
    let angle = cross.atan2(dot).to_degrees();
    if !angle.is_finite()
        || angle < 0.0
        || angle > f64::from(MAX_TERRAIN_FORD_CROSSING_ANGLE_DEGREES)
    {
        return Err(RiverGenerationError::InvalidOverlapPolyline);
    }
    Ok(angle)
}

fn overlap_is_local_to_crossing(
    first: &[[f64; 2]],
    first_radius_m: f64,
    second: &[[f64; 2]],
    second_radius_m: f64,
    crossing_xz_m: [f64; 2],
    local_radius_m: f64,
    comparisons: &mut usize,
) -> Result<bool, RiverGenerationError> {
    let maximum_distance_squared_m = (first_radius_m + second_radius_m).powi(2);
    let local_radius_squared_m = local_radius_m.powi(2);
    for first_segment in first.windows(2) {
        let first_bounds =
            expanded_segment_bounds(first_segment[0], first_segment[1], first_radius_m);
        for second_segment in second.windows(2) {
            let second_bounds =
                expanded_segment_bounds(second_segment[0], second_segment[1], second_radius_m);
            if !bounds_overlap(first_bounds, second_bounds) {
                continue;
            }
            consume_overlap_comparison(comparisons)?;
            if segment_distance_squared(
                first_segment[0],
                first_segment[1],
                second_segment[0],
                second_segment[1],
            ) > maximum_distance_squared_m
            {
                continue;
            }
            if point_segment_distance_squared(crossing_xz_m, first_segment[0], first_segment[1]).0
                > local_radius_squared_m
                || point_segment_distance_squared(
                    crossing_xz_m,
                    second_segment[0],
                    second_segment[1],
                )
                .0 > local_radius_squared_m
            {
                return Ok(false);
            }
        }
    }
    Ok(true)
}

fn segment_intersection_parameters(
    first_start: [f64; 2],
    first_end: [f64; 2],
    second_start: [f64; 2],
    second_end: [f64; 2],
) -> Option<(f64, f64, [f64; 2])> {
    let first_delta = subtract(first_end, first_start);
    let second_delta = subtract(second_end, second_start);
    let denominator = cross_2d(first_delta, second_delta);
    if !denominator.is_finite() || denominator.abs() <= SEGMENT_INTERSECTION_EPSILON {
        return None;
    }
    let separation = subtract(second_start, first_start);
    let first_fraction = cross_2d(separation, second_delta) / denominator;
    let second_fraction = cross_2d(separation, first_delta) / denominator;
    let inclusive = -SEGMENT_INTERSECTION_EPSILON..=1.0 + SEGMENT_INTERSECTION_EPSILON;
    if !first_fraction.is_finite()
        || !second_fraction.is_finite()
        || !inclusive.contains(&first_fraction)
        || !inclusive.contains(&second_fraction)
    {
        return None;
    }
    let first_fraction = first_fraction.clamp(0.0, 1.0);
    let second_fraction = second_fraction.clamp(0.0, 1.0);
    Some((
        first_fraction,
        second_fraction,
        [
            first_start[0] + first_delta[0] * first_fraction,
            first_start[1] + first_delta[1] * first_fraction,
        ],
    ))
}

fn segment_length(start: [f64; 2], end: [f64; 2]) -> Result<f64, RiverGenerationError> {
    let delta = subtract(end, start);
    let length = delta[0].hypot(delta[1]);
    if !length.is_finite() || length <= 0.0 {
        Err(RiverGenerationError::InvalidOverlapPolyline)
    } else {
        Ok(length)
    }
}

fn subtract(first: [f64; 2], second: [f64; 2]) -> [f64; 2] {
    [first[0] - second[0], first[1] - second[1]]
}

fn cross_2d(first: [f64; 2], second: [f64; 2]) -> f64 {
    first[0] * second[1] - first[1] * second[0]
}

fn squared_distance(first: [f64; 2], second: [f64; 2]) -> f64 {
    let delta = subtract(first, second);
    delta[0] * delta[0] + delta[1] * delta[1]
}

fn consume_overlap_comparison(comparisons: &mut usize) -> Result<(), RiverGenerationError> {
    *comparisons = comparisons
        .checked_add(1)
        .ok_or(RiverGenerationError::ArithmeticOverflow)?;
    if *comparisons > MAX_RIVER_OVERLAP_COMPARISONS {
        Err(RiverGenerationError::OverlapComparisonBudgetExceeded {
            maximum: MAX_RIVER_OVERLAP_COMPARISONS,
        })
    } else {
        Ok(())
    }
}

fn polylines_overlap(
    first: &[[f64; 2]],
    first_radius_m: f64,
    second: &[[f64; 2]],
    second_radius_m: f64,
    comparisons: &mut usize,
) -> Result<bool, RiverGenerationError> {
    if first.len() < 2 || second.len() < 2 {
        return Err(RiverGenerationError::InvalidOverlapPolyline);
    }
    if !bounds_overlap(
        polyline_bounds(first, first_radius_m),
        polyline_bounds(second, second_radius_m),
    ) {
        return Ok(false);
    }
    let maximum_distance_m = first_radius_m + second_radius_m;
    let maximum_distance_squared_m = maximum_distance_m * maximum_distance_m;
    for first_segment in first.windows(2) {
        let first_bounds =
            expanded_segment_bounds(first_segment[0], first_segment[1], first_radius_m);
        for second_segment in second.windows(2) {
            let second_bounds =
                expanded_segment_bounds(second_segment[0], second_segment[1], second_radius_m);
            if !bounds_overlap(first_bounds, second_bounds) {
                continue;
            }
            consume_overlap_comparison(comparisons)?;
            if segment_distance_squared(
                first_segment[0],
                first_segment[1],
                second_segment[0],
                second_segment[1],
            ) <= maximum_distance_squared_m
            {
                return Ok(true);
            }
        }
    }
    Ok(false)
}

fn polyline_bounds(points: &[[f64; 2]], radius_m: f64) -> ([f64; 2], [f64; 2]) {
    let mut minimum = [f64::INFINITY; 2];
    let mut maximum = [f64::NEG_INFINITY; 2];
    for point in points {
        for axis in 0..2 {
            minimum[axis] = minimum[axis].min(point[axis] - radius_m);
            maximum[axis] = maximum[axis].max(point[axis] + radius_m);
        }
    }
    (minimum, maximum)
}

fn expanded_segment_bounds(start: [f64; 2], end: [f64; 2], radius_m: f64) -> ([f64; 2], [f64; 2]) {
    (
        [
            start[0].min(end[0]) - radius_m,
            start[1].min(end[1]) - radius_m,
        ],
        [
            start[0].max(end[0]) + radius_m,
            start[1].max(end[1]) + radius_m,
        ],
    )
}

fn bounds_overlap(first: ([f64; 2], [f64; 2]), second: ([f64; 2], [f64; 2])) -> bool {
    first.0[0] <= second.1[0]
        && second.0[0] <= first.1[0]
        && first.0[1] <= second.1[1]
        && second.0[1] <= first.1[1]
}

fn segment_distance_squared(
    first_start: [f64; 2],
    first_end: [f64; 2],
    second_start: [f64; 2],
    second_end: [f64; 2],
) -> f64 {
    if segments_intersect(first_start, first_end, second_start, second_end) {
        return 0.0;
    }
    [
        point_segment_distance_squared(first_start, second_start, second_end).0,
        point_segment_distance_squared(first_end, second_start, second_end).0,
        point_segment_distance_squared(second_start, first_start, first_end).0,
        point_segment_distance_squared(second_end, first_start, first_end).0,
    ]
    .into_iter()
    .fold(f64::INFINITY, f64::min)
}

fn segments_intersect(
    first_start: [f64; 2],
    first_end: [f64; 2],
    second_start: [f64; 2],
    second_end: [f64; 2],
) -> bool {
    let first_side_start = orientation(first_start, first_end, second_start);
    let first_side_end = orientation(first_start, first_end, second_end);
    let second_side_start = orientation(second_start, second_end, first_start);
    let second_side_end = orientation(second_start, second_end, first_end);
    if opposite_sides(first_side_start, first_side_end)
        && opposite_sides(second_side_start, second_side_end)
    {
        return true;
    }
    (first_side_start.abs() <= SEGMENT_INTERSECTION_EPSILON
        && point_inside_segment_bounds(second_start, first_start, first_end))
        || (first_side_end.abs() <= SEGMENT_INTERSECTION_EPSILON
            && point_inside_segment_bounds(second_end, first_start, first_end))
        || (second_side_start.abs() <= SEGMENT_INTERSECTION_EPSILON
            && point_inside_segment_bounds(first_start, second_start, second_end))
        || (second_side_end.abs() <= SEGMENT_INTERSECTION_EPSILON
            && point_inside_segment_bounds(first_end, second_start, second_end))
}

fn orientation(start: [f64; 2], end: [f64; 2], point: [f64; 2]) -> f64 {
    (end[0] - start[0]) * (point[1] - start[1]) - (end[1] - start[1]) * (point[0] - start[0])
}

fn opposite_sides(first: f64, second: f64) -> bool {
    (first > SEGMENT_INTERSECTION_EPSILON && second < -SEGMENT_INTERSECTION_EPSILON)
        || (first < -SEGMENT_INTERSECTION_EPSILON && second > SEGMENT_INTERSECTION_EPSILON)
}

fn point_inside_segment_bounds(point: [f64; 2], start: [f64; 2], end: [f64; 2]) -> bool {
    point[0] >= start[0].min(end[0]) - SEGMENT_INTERSECTION_EPSILON
        && point[0] <= start[0].max(end[0]) + SEGMENT_INTERSECTION_EPSILON
        && point[1] >= start[1].min(end[1]) - SEGMENT_INTERSECTION_EPSILON
        && point[1] <= start[1].max(end[1]) + SEGMENT_INTERSECTION_EPSILON
}

fn point_segment_distance_squared(point: [f64; 2], start: [f64; 2], end: [f64; 2]) -> (f64, f64) {
    let delta = [end[0] - start[0], end[1] - start[1]];
    let squared_length = delta[0] * delta[0] + delta[1] * delta[1];
    if squared_length <= MIN_HORIZONTAL_DELTA_SQUARED || !squared_length.is_finite() {
        let distance = [point[0] - start[0], point[1] - start[1]];
        return (distance[0] * distance[0] + distance[1] * distance[1], 0.0);
    }
    let fraction = (((point[0] - start[0]) * delta[0] + (point[1] - start[1]) * delta[1])
        / squared_length)
        .clamp(0.0, 1.0);
    let closest = [
        start[0] + delta[0] * fraction,
        start[1] + delta[1] * fraction,
    ];
    let distance = [point[0] - closest[0], point[1] - closest[1]];
    (
        distance[0] * distance[0] + distance[1] * distance[1],
        fraction,
    )
}

fn grid_index_range(minimum_m: f64, maximum_m: f64) -> Result<(u16, u16), RiverGenerationError> {
    if !minimum_m.is_finite() || !maximum_m.is_finite() || minimum_m > maximum_m {
        return Err(RiverGenerationError::InvalidGridBounds);
    }
    let maximum_index = f64::from(TERRAIN_GLOBAL_INTERVALS_PER_AXIS);
    let minimum = ((minimum_m + f64::from(TERRAIN_HALF_EXTENT_M))
        / f64::from(TERRAIN_SAMPLE_SPACING_M))
    .floor()
    .clamp(0.0, maximum_index);
    let maximum = ((maximum_m + f64::from(TERRAIN_HALF_EXTENT_M))
        / f64::from(TERRAIN_SAMPLE_SPACING_M))
    .ceil()
    .clamp(0.0, maximum_index);
    #[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
    Ok((minimum as u16, maximum as u16))
}

fn sample_xz(sample_x: u16, sample_z: u16) -> [f64; 2] {
    [
        -f64::from(TERRAIN_HALF_EXTENT_M)
            + f64::from(sample_x) * f64::from(TERRAIN_SAMPLE_SPACING_M),
        -f64::from(TERRAIN_HALF_EXTENT_M)
            + f64::from(sample_z) * f64::from(TERRAIN_SAMPLE_SPACING_M),
    ]
}

fn smoothstep(value: f64) -> f64 {
    value * value * (3.0 - 2.0 * value)
}

fn linear_interpolate_f64(first: f64, second: f64, fraction: f64) -> f64 {
    first + (second - first) * fraction
}

fn inside_river_extent(point: [f64; 2]) -> bool {
    let half_extent = f64::from(TERRAIN_HALF_EXTENT_M);
    (-half_extent..half_extent).contains(&point[0])
        && (-half_extent..half_extent).contains(&point[1])
}

fn f32_from_derived(
    value: f64,
    path_id: PathId,
    field: &'static str,
) -> Result<f32, RiverGenerationError> {
    #[allow(clippy::cast_possible_truncation)]
    let converted = value as f32;
    if converted.is_finite() {
        Ok(canonicalize_zero(converted))
    } else {
        Err(RiverGenerationError::InvalidDerivedValue { path_id, field })
    }
}

fn f32_from_plain(value: f64) -> Result<f32, RiverGenerationError> {
    #[allow(clippy::cast_possible_truncation)]
    let converted = value as f32;
    if converted.is_finite() {
        Ok(canonicalize_zero(converted))
    } else {
        Err(RiverGenerationError::InvalidGridBounds)
    }
}

fn canonicalize_zero(value: f32) -> f32 {
    if value == 0.0 { 0.0 } else { value }
}

fn checked_mesh_counts(sample_count: usize) -> Result<(usize, usize), RiverGenerationError> {
    if sample_count > MAX_GENERATED_RIVER_LONGITUDINAL_SAMPLES {
        return Err(RiverGenerationError::LongitudinalSampleBudgetExceeded {
            requested: sample_count,
            maximum: MAX_GENERATED_RIVER_LONGITUDINAL_SAMPLES,
        });
    }
    let vertex_count = sample_count
        .checked_mul(RIVER_CROSS_SECTION_VERTEX_COUNT)
        .ok_or(RiverGenerationError::ArithmeticOverflow)?;
    let interval_count = sample_count
        .checked_sub(1)
        .ok_or(RiverGenerationError::ArithmeticOverflow)?;
    let index_count = interval_count
        .checked_mul(RIVER_INDICES_PER_LONGITUDINAL_INTERVAL)
        .ok_or(RiverGenerationError::ArithmeticOverflow)?;
    if vertex_count > MAX_GENERATED_RIVER_VERTICES || index_count > MAX_GENERATED_RIVER_INDICES {
        return Err(RiverGenerationError::MeshBudgetExceeded {
            vertices: vertex_count,
            indices: index_count,
        });
    }
    Ok((vertex_count, index_count))
}

fn ensure_effective_sample_budget(sample_count: usize) -> Result<(), RiverGenerationError> {
    if sample_count > MAX_EFFECTIVE_RIVER_TERRAIN_SAMPLES {
        Err(RiverGenerationError::EffectiveTerrainSampleBudgetExceeded {
            requested: sample_count,
            maximum: MAX_EFFECTIVE_RIVER_TERRAIN_SAMPLES,
        })
    } else {
        Ok(())
    }
}

fn build_indices(
    sample_count: usize,
    expected_count: usize,
) -> Result<Vec<u32>, RiverGenerationError> {
    let mut indices = Vec::new();
    indices
        .try_reserve_exact(expected_count)
        .map_err(|_| RiverGenerationError::AllocationFailed("river indices"))?;
    for row in 0..sample_count
        .checked_sub(1)
        .ok_or(RiverGenerationError::ArithmeticOverflow)?
    {
        let current = row
            .checked_mul(RIVER_CROSS_SECTION_VERTEX_COUNT)
            .ok_or(RiverGenerationError::ArithmeticOverflow)?;
        let next = current
            .checked_add(RIVER_CROSS_SECTION_VERTEX_COUNT)
            .ok_or(RiverGenerationError::ArithmeticOverflow)?;
        let current = u32::try_from(current).map_err(|_| RiverGenerationError::IndexOverflow)?;
        let next = u32::try_from(next).map_err(|_| RiverGenerationError::IndexOverflow)?;
        indices.extend([
            current,
            next,
            current + 1,
            current + 1,
            next,
            next + 1,
            current + 1,
            next + 1,
            current + 2,
            current + 2,
            next + 1,
            next + 2,
        ]);
    }
    if indices.len() != expected_count {
        return Err(RiverGenerationError::IndexCountMismatch {
            expected: expected_count,
            generated: indices.len(),
        });
    }
    Ok(indices)
}

fn build_normals(
    path_id: PathId,
    positions_m: &[[f32; 3]],
    indices: &[u32],
) -> Result<Vec<[f32; 3]>, RiverGenerationError> {
    let mut accumulated = vec![[0.0_f64; 3]; positions_m.len()];
    let (triangles, remainder) = indices.as_chunks::<3>();
    if !remainder.is_empty() {
        return Err(RiverGenerationError::InvalidIndexCount(indices.len()));
    }
    for (triangle_index, triangle) in triangles.iter().enumerate() {
        let indices = triangle.map(|index| usize::try_from(index).ok());
        let [Some(first_index), Some(second_index), Some(third_index)] = indices else {
            return Err(RiverGenerationError::IndexOverflow);
        };
        let points =
            [first_index, second_index, third_index].map(|index| positions_m.get(index).copied());
        let [Some(first), Some(second), Some(third)] = points else {
            return Err(RiverGenerationError::IndexOverflow);
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
        let squared_length = cross.iter().map(|value| value * value).sum::<f64>();
        if cross.iter().any(|value| !value.is_finite())
            || cross[1] <= 0.0
            || squared_length <= MIN_TRIANGLE_NORMAL_SQUARED
        {
            return Err(RiverGenerationError::InvalidTriangle {
                path_id,
                triangle_index,
            });
        }
        for index in [first_index, second_index, third_index] {
            for axis in 0..3 {
                accumulated[index][axis] += cross[axis];
            }
        }
    }

    accumulated
        .into_iter()
        .enumerate()
        .map(|(vertex_index, normal)| {
            let squared_length = normal.iter().map(|value| value * value).sum::<f64>();
            if normal.iter().any(|value| !value.is_finite())
                || squared_length <= MIN_TRIANGLE_NORMAL_SQUARED
            {
                return Err(RiverGenerationError::InvalidVertexNormal {
                    path_id,
                    vertex_index,
                });
            }
            let inverse_length = squared_length.sqrt().recip();
            Ok([
                f32_from_derived(normal[0] * inverse_length, path_id, "normal X")?,
                f32_from_derived(normal[1] * inverse_length, path_id, "normal Y")?,
                f32_from_derived(normal[2] * inverse_length, path_id, "normal Z")?,
            ])
        })
        .collect()
}

/// Revision-bound request to create or replace one river channel.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct RiverUpsertCommand {
    pub command_id: CommandId,
    pub base_revision: Revision,
    pub project_id: ProjectId,
    pub module_id: ModuleId,
    pub area_id: AreaId,
    pub river: AuthoredRiver,
}

/// Candidate workspace edit which changes exactly one river assignment.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct RiverUpsertResult {
    pub command: RiverUpsertCommand,
    pub previous_river: Option<AuthoredRiver>,
    pub river: AuthoredRiver,
    pub edit: WorkspaceEditResult,
}

/// Produces and fully generates one candidate without mutating the supplied workspace.
pub fn preview_upsert_river(
    workspace: &WorkspaceDocument,
    command: RiverUpsertCommand,
) -> Result<RiverUpsertResult, RiverCommandError> {
    let area = validate_command_context(
        workspace,
        command.base_revision,
        command.project_id,
        command.module_id,
        command.area_id,
    )?;
    command.river.validate()?;
    let previous_river = area
        .rivers
        .rivers
        .binary_search_by_key(&command.river.path_id, |river| river.path_id)
        .ok()
        .map(|index| area.rivers.rivers[index]);
    let edit = workspace.preview_edit(WorkspaceEdit {
        command_id: command.command_id,
        base_revision: command.base_revision,
        change: WorkspaceChange::UpsertRiver {
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
            river: command.river,
        },
    })?;
    validate_candidate_rivers(&edit, command.module_id, command.area_id)?;
    Ok(RiverUpsertResult {
        river: command.river,
        command,
        previous_river,
        edit,
    })
}

pub(crate) fn validate_candidate_rivers(
    edit: &WorkspaceEditResult,
    module_id: ModuleId,
    area_id: AreaId,
) -> Result<(), RiverGenerationError> {
    let area = edit
        .document
        .area(module_id, area_id)
        .ok_or(RiverGenerationError::MissingArea)?;
    RiverGenerationPlan::from_documents(&area.terrain, &area.paths, &area.roads, &area.rivers)?
        .generate()?;
    Ok(())
}

fn validate_command_context(
    workspace: &WorkspaceDocument,
    base_revision: Revision,
    project_id: ProjectId,
    module_id: ModuleId,
    area_id: AreaId,
) -> Result<&WorkspaceArea, RiverCommandError> {
    workspace.validate()?;
    if workspace.project.id != project_id {
        return Err(RiverCommandError::WrongProject);
    }
    if workspace.revision != base_revision {
        return Err(WorkspaceError::StaleRevision.into());
    }
    workspace
        .area(module_id, area_id)
        .ok_or_else(|| WorkspaceError::MissingTarget("area").into())
}

#[derive(Debug, Error)]
pub enum RiverGenerationError {
    #[error("river terrain document is invalid: {0}")]
    TerrainDocument(#[from] forge_project::TerrainDocumentError),
    #[error("river path document is invalid: {0}")]
    PathDocument(#[from] forge_project::PathDocumentError),
    #[error("river road document is invalid: {0}")]
    RoadDocument(#[from] forge_project::RoadDocumentError),
    #[error("river document is invalid: {0}")]
    RiverDocument(#[from] RiverDocumentError),
    #[error("river terrain generation failed: {0}")]
    Terrain(#[from] TerrainGenerationError),
    #[error("river path generation failed: {0}")]
    Path(#[from] PathGenerationError),
    #[error("river road validation failed: {0}")]
    Road(#[from] RoadGenerationError),
    #[error("terrain, path, road and river documents belong to different projects")]
    ProjectIdentityMismatch,
    #[error("terrain, path, road and river documents belong to different areas")]
    AreaIdentityMismatch,
    #[error("river references missing path {0}")]
    MissingPath(PathId),
    #[error("river path {0} must be open")]
    ClosedPath(PathId),
    #[error("path {0} cannot be both a road and a river")]
    RiverRoadConflict(PathId),
    #[error("road overlap validation references missing path {0}")]
    MissingRoadPath(PathId),
    #[error("river total path length {length_m} m exceeds {maximum_m} m")]
    TotalLengthExceeded { length_m: f64, maximum_m: f64 },
    #[error("river generation only supports TerrainCarvedChannelV1")]
    UnsupportedAlgorithm,
    #[error("river path {path_id} has an invalid tangent at sample {sample_index}")]
    InvalidTangent {
        path_id: PathId,
        sample_index: usize,
    },
    #[error(
        "river path {path_id} footprint row {row_index}, point {point_index} is outside [-256,256) metres"
    )]
    FootprintOutOfBounds {
        path_id: PathId,
        row_index: usize,
        point_index: usize,
    },
    #[error("river path {path_id} cannot sample terrain at row {sample_index}: {source}")]
    TerrainSample {
        path_id: PathId,
        sample_index: usize,
        source: TerrainGenerationError,
    },
    #[error("river path {path_id} derived {field} is not finite or canonical")]
    InvalidDerivedValue {
        path_id: PathId,
        field: &'static str,
    },
    #[error("river projection requests {requested} longitudinal rows; maximum is {maximum}")]
    LongitudinalSampleBudgetExceeded { requested: usize, maximum: usize },
    #[error("river mesh requests {vertices} vertices and {indices} indices")]
    MeshBudgetExceeded { vertices: usize, indices: usize },
    #[error("river carving affects {requested} terrain samples; maximum is {maximum}")]
    EffectiveTerrainSampleBudgetExceeded { requested: usize, maximum: usize },
    #[error("river terrain sample ({sample_x}, {sample_z}) is invalid")]
    InvalidEffectiveTerrainSample { sample_x: u16, sample_z: u16 },
    #[error("river terrain material blend is invalid")]
    InvalidMaterialBlend,
    #[error("river paths {first_path_id} and {second_path_id} overlap")]
    RiverOverlap {
        first_path_id: PathId,
        second_path_id: PathId,
    },
    #[error(
        "river path {river_path_id} and road path {road_path_id} do not form one isolated transversal ford"
    )]
    RiverRoadOverlap {
        river_path_id: PathId,
        road_path_id: PathId,
    },
    #[error(
        "river path {river_path_id} and road path {road_path_id} intersect at least {found} times; exactly one crossing is supported"
    )]
    MultipleRiverRoadIntersections {
        river_path_id: PathId,
        road_path_id: PathId,
        found: usize,
    },
    #[error("river path {river_path_id} is tangent to road path {road_path_id}")]
    TangentialRiverRoadIntersection {
        river_path_id: PathId,
        road_path_id: PathId,
    },
    #[error(
        "river path {river_path_id} crosses road path {road_path_id} at {angle_degrees} degrees; minimum is {minimum_degrees} degrees"
    )]
    ShallowTerrainFordAngle {
        river_path_id: PathId,
        road_path_id: PathId,
        angle_degrees: f32,
        minimum_degrees: f32,
    },
    #[error(
        "river path {river_path_id} and road path {road_path_id} cross too near the {role} endpoint; required clearance is {required_clearance_m} m"
    )]
    TerrainFordNearEndpoint {
        river_path_id: PathId,
        road_path_id: PathId,
        role: &'static str,
        required_clearance_m: f32,
    },
    #[error(
        "river path {river_path_id} and road path {road_path_id} have non-local footprint overlap"
    )]
    ExtendedRiverRoadOverlap {
        river_path_id: PathId,
        road_path_id: PathId,
    },
    #[error(
        "terrain ford between river path {river_path_id} and road path {road_path_id} has {depth_m} m residual water depth; minimum is {minimum_m} m"
    )]
    InsufficientTerrainFordWaterDepth {
        river_path_id: PathId,
        road_path_id: PathId,
        depth_m: f32,
        minimum_m: f32,
    },
    #[error("generated road-river intersection count {found} exceeds {maximum}")]
    TooManyRoadRiverIntersections { found: usize, maximum: usize },
    #[error("generated road-river intersections are invalid: {0}")]
    InvalidGeneratedRoadRiverIntersections(&'static str),
    #[error("river overlap comparison budget exceeds {maximum}")]
    OverlapComparisonBudgetExceeded { maximum: usize },
    #[error("river overlap polyline is incomplete")]
    InvalidOverlapPolyline,
    #[error("river grid bounds are invalid")]
    InvalidGridBounds,
    #[error("river generation arithmetic overflow")]
    ArithmeticOverflow,
    #[error("river allocation failed for {0}")]
    AllocationFailed(&'static str),
    #[error("river mesh index cannot be represented")]
    IndexOverflow,
    #[error("river mesh index count {0} is not divisible by three")]
    InvalidIndexCount(usize),
    #[error("river mesh expected {expected} indices but generated {generated}")]
    IndexCountMismatch { expected: usize, generated: usize },
    #[error("river path {path_id} generated invalid triangle {triangle_index}")]
    InvalidTriangle {
        path_id: PathId,
        triangle_index: usize,
    },
    #[error("river path {path_id} generated invalid normal at vertex {vertex_index}")]
    InvalidVertexNormal {
        path_id: PathId,
        vertex_index: usize,
    },
    #[error("river candidate area does not exist")]
    MissingArea,
}

#[derive(Debug, Error)]
pub enum RiverCommandError {
    #[error("river command belongs to another project")]
    WrongProject,
    #[error("river profile is invalid: {0}")]
    InvalidRiver(#[from] RiverDocumentError),
    #[error("workspace river edit failed: {0}")]
    Workspace(#[from] WorkspaceError),
    #[error("generated river candidate is invalid: {0}")]
    Generation(#[from] RiverGenerationError),
}

#[cfg(test)]
#[path = "river_tests.rs"]
mod tests;
