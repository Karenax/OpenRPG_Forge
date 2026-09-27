use crate::terrain_update::TerrainRuntimeBindings;
use bevy::prelude::*;
use bevy_rapier3d::prelude::{Collider, NoUserData, RapierPhysicsPlugin, RigidBody, TimestepMode};
use forge_project::TerrainDocument;
use forge_world::{
    TERRAIN_CHUNK_COUNT, TERRAIN_CHUNK_INTERVALS, TERRAIN_CHUNK_VERTICES_PER_AXIS,
    TERRAIN_SAMPLE_SPACING_M, TerrainChunkCoordinate, TerrainChunkId, TerrainChunkMesh,
    TerrainColliderTriangleOrder, TerrainColliderVertexOrder, TerrainGenerationError,
    TerrainGenerationPlan,
};
use std::{error::Error, fmt};

/// Fixed simulation frequency shared by native and WebGPU Visual Valley targets.
pub const TERRAIN_PHYSICS_HZ: f64 = 60.0;

const TERRAIN_PHYSICS_DT_SECONDS: f32 = 1.0 / 60.0;
const TERRAIN_HEIGHTFIELD_AXIS: usize = 33;
const TERRAIN_HEIGHTFIELD_SAMPLE_COUNT: usize = TERRAIN_HEIGHTFIELD_AXIS * TERRAIN_HEIGHTFIELD_AXIS;
const TERRAIN_HEIGHTFIELD_TRIANGLE_COUNT: usize = 32 * 32 * 2;
const TERRAIN_HEIGHTFIELD_HORIZONTAL_SCALE_M: f32 = 64.0;

const _: () = {
    assert!(TERRAIN_CHUNK_VERTICES_PER_AXIS == 33);
    assert!(TERRAIN_CHUNK_INTERVALS == 32);
};

/// Stable marker for one static Rapier projection of a generated Forge terrain chunk.
#[derive(Clone, Copy, Component, Debug, Eq, PartialEq)]
pub struct TerrainHeightfieldCollider {
    pub id: TerrainChunkId,
    pub coordinate: TerrainChunkCoordinate,
}

/// Adds the fixed-step Rapier world and the 64 local Visual Valley heightfields.
pub struct TerrainPhysicsPlugin {
    document: TerrainDocument,
}

impl TerrainPhysicsPlugin {
    #[must_use]
    pub fn new(document: TerrainDocument) -> Self {
        Self { document }
    }
}

impl Plugin for TerrainPhysicsPlugin {
    fn build(&self, app: &mut App) {
        let prepared = prepare_terrain_heightfields(&self.document)
            .unwrap_or_else(|error| panic!("TerrainPhysicsPlugin requires valid terrain: {error}"));

        app.insert_resource(Time::<Fixed>::from_hz(TERRAIN_PHYSICS_HZ))
            .insert_resource(TimestepMode::Fixed {
                dt: TERRAIN_PHYSICS_DT_SECONDS,
                substeps: 1,
            })
            .insert_resource(PreparedTerrainHeightfields(Some(prepared)))
            .add_plugins(RapierPhysicsPlugin::<NoUserData>::default().in_fixed_schedule())
            .add_systems(Startup, spawn_terrain_heightfields);
    }
}

/// Typed failures raised before any Rapier collider is constructed.
#[derive(Debug)]
pub enum TerrainPhysicsAdapterError {
    Generation(TerrainGenerationError),
    UnexpectedChunkCount {
        expected: usize,
        actual: usize,
    },
    InvalidChunkPayload {
        coordinate: TerrainChunkCoordinate,
        reason: &'static str,
    },
}

impl fmt::Display for TerrainPhysicsAdapterError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Generation(error) => write!(formatter, "terrain generation failed: {error}"),
            Self::UnexpectedChunkCount { expected, actual } => write!(
                formatter,
                "terrain physics expected {expected} chunks, found {actual}"
            ),
            Self::InvalidChunkPayload { coordinate, reason } => write!(
                formatter,
                "terrain chunk ({}, {}) cannot become a heightfield: {reason}",
                coordinate.x, coordinate.z
            ),
        }
    }
}

impl Error for TerrainPhysicsAdapterError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        match self {
            Self::Generation(error) => Some(error),
            Self::UnexpectedChunkCount { .. } | Self::InvalidChunkPayload { .. } => None,
        }
    }
}

impl From<TerrainGenerationError> for TerrainPhysicsAdapterError {
    fn from(error: TerrainGenerationError) -> Self {
        Self::Generation(error)
    }
}

#[derive(Resource)]
struct PreparedTerrainHeightfields(Option<Vec<PreparedTerrainHeightfield>>);

#[derive(Debug)]
struct PreparedTerrainHeightfield {
    marker: TerrainHeightfieldCollider,
    heights_column_major: Vec<f32>,
    rows: usize,
    columns: usize,
    scale: Vec3,
    center: Vec3,
}

fn prepare_terrain_heightfields(
    document: &TerrainDocument,
) -> Result<Vec<PreparedTerrainHeightfield>, TerrainPhysicsAdapterError> {
    let generated = TerrainGenerationPlan::from_document(document)?.generate()?;
    if generated.chunks.len() != TERRAIN_CHUNK_COUNT {
        return Err(TerrainPhysicsAdapterError::UnexpectedChunkCount {
            expected: TERRAIN_CHUNK_COUNT,
            actual: generated.chunks.len(),
        });
    }

    generated
        .chunks
        .iter()
        .map(prepare_chunk_heightfield)
        .collect()
}

fn prepare_chunk_heightfield(
    chunk: &TerrainChunkMesh,
) -> Result<PreparedTerrainHeightfield, TerrainPhysicsAdapterError> {
    validate_chunk_contract(chunk)?;

    let heights_column_major = row_major_heights_to_rapier_column_major(
        &chunk.collider.vertices_m,
        TERRAIN_HEIGHTFIELD_AXIS,
    )
    .map_err(|reason| invalid_chunk(chunk, reason))?;
    let center = Vec3::new(
        f32::midpoint(chunk.minimum_xz_m[0], chunk.maximum_xz_m[0]),
        0.0,
        f32::midpoint(chunk.minimum_xz_m[1], chunk.maximum_xz_m[1]),
    );
    let scale = Vec3::new(
        TERRAIN_HEIGHTFIELD_HORIZONTAL_SCALE_M,
        1.0,
        TERRAIN_HEIGHTFIELD_HORIZONTAL_SCALE_M,
    );
    if !center.is_finite() || !scale.is_finite() || scale.cmple(Vec3::ZERO).any() {
        return Err(invalid_chunk(
            chunk,
            "heightfield transform or scale is invalid",
        ));
    }

    Ok(PreparedTerrainHeightfield {
        marker: TerrainHeightfieldCollider {
            id: chunk.id,
            coordinate: chunk.coordinate,
        },
        heights_column_major,
        rows: TERRAIN_HEIGHTFIELD_AXIS,
        columns: TERRAIN_HEIGHTFIELD_AXIS,
        scale,
        center,
    })
}

/// Builds the Rapier shape for one already generated Forge chunk.
pub(crate) fn build_terrain_heightfield_collider(
    chunk: &TerrainChunkMesh,
) -> Result<Collider, TerrainPhysicsAdapterError> {
    let prepared = prepare_chunk_heightfield(chunk)?;
    Ok(Collider::heightfield(
        prepared.heights_column_major,
        prepared.rows,
        prepared.columns,
        prepared.scale,
    ))
}

fn validate_chunk_contract(chunk: &TerrainChunkMesh) -> Result<(), TerrainPhysicsAdapterError> {
    if chunk.collider.vertex_order != TerrainColliderVertexOrder::RowMajorZThenX {
        return Err(invalid_chunk(
            chunk,
            "collider vertices are not row-major Z-then-X",
        ));
    }
    if chunk.collider.triangle_order
        != TerrainColliderTriangleOrder::CellRowMajorZThenXCounterClockwiseFromAbove
    {
        return Err(invalid_chunk(
            chunk,
            "collider triangles use an unsupported ordering",
        ));
    }
    if chunk.collider.vertices_m.len() != TERRAIN_HEIGHTFIELD_SAMPLE_COUNT {
        return Err(invalid_chunk(
            chunk,
            "collider vertex count is not 33 by 33",
        ));
    }
    if chunk
        .collider
        .vertices_m
        .iter()
        .chain(chunk.positions_m.iter())
        .any(|vertex| vertex.iter().any(|value| !value.is_finite()))
    {
        return Err(invalid_chunk(
            chunk,
            "collider contains a non-finite vertex",
        ));
    }
    if chunk.collider.vertices_m != chunk.positions_m {
        return Err(invalid_chunk(
            chunk,
            "collider vertices diverge from the generated terrain mesh",
        ));
    }
    if chunk.collider.triangles.len() != TERRAIN_HEIGHTFIELD_TRIANGLE_COUNT {
        return Err(invalid_chunk(
            chunk,
            "collider triangle count is not 32 by 32 by two",
        ));
    }
    if chunk
        .minimum_xz_m
        .iter()
        .chain(chunk.maximum_xz_m.iter())
        .any(|value| !value.is_finite())
    {
        return Err(invalid_chunk(chunk, "chunk bounds are non-finite"));
    }
    let width_m = chunk.maximum_xz_m[0] - chunk.minimum_xz_m[0];
    let depth_m = chunk.maximum_xz_m[1] - chunk.minimum_xz_m[1];
    if width_m.to_bits() != TERRAIN_HEIGHTFIELD_HORIZONTAL_SCALE_M.to_bits()
        || depth_m.to_bits() != TERRAIN_HEIGHTFIELD_HORIZONTAL_SCALE_M.to_bits()
    {
        return Err(invalid_chunk(chunk, "chunk bounds are not 64 by 64 metres"));
    }

    for z in 0..TERRAIN_HEIGHTFIELD_AXIS {
        for x in 0..TERRAIN_HEIGHTFIELD_AXIS {
            let vertex = chunk.collider.vertices_m[row_major_index(x, z, TERRAIN_HEIGHTFIELD_AXIS)];
            let expected_x = chunk.minimum_xz_m[0] + grid_offset_m(x);
            let expected_z = chunk.minimum_xz_m[1] + grid_offset_m(z);
            if vertex[0].to_bits() != expected_x.to_bits()
                || vertex[2].to_bits() != expected_z.to_bits()
            {
                return Err(invalid_chunk(
                    chunk,
                    "collider vertices do not follow the expected local grid",
                ));
            }
        }
    }

    validate_rapier_split_compatibility(chunk)
}

#[must_use]
fn grid_offset_m(index: usize) -> f32 {
    f32::from(u16::try_from(index).expect("a 33-sample terrain axis fits u16"))
        * TERRAIN_SAMPLE_SPACING_M
}

fn validate_rapier_split_compatibility(
    chunk: &TerrainChunkMesh,
) -> Result<(), TerrainPhysicsAdapterError> {
    let cell_axis = TERRAIN_HEIGHTFIELD_AXIS - 1;
    for z in 0..cell_axis {
        for x in 0..cell_axis {
            let cell_index = z * cell_axis + x;
            let actual_first = chunk.collider.triangles[cell_index * 2];
            let actual_second = chunk.collider.triangles[cell_index * 2 + 1];
            let (rapier_first, rapier_second) = rapier_default_split_in_forge_indices(x, z);
            if !same_oriented_triangle(actual_first, rapier_first)
                || !same_oriented_triangle(actual_second, rapier_second)
            {
                return Err(invalid_chunk(
                    chunk,
                    "mesh triangles do not match Rapier's default heightfield split",
                ));
            }
        }
    }
    Ok(())
}

fn row_major_heights_to_rapier_column_major(
    vertices_m: &[[f32; 3]],
    axis: usize,
) -> Result<Vec<f32>, &'static str> {
    if axis < 2 || vertices_m.len() != axis * axis {
        return Err("heightfield source is not a square grid");
    }
    if vertices_m
        .iter()
        .any(|vertex| vertex.iter().any(|value| !value.is_finite()))
    {
        return Err("heightfield source contains a non-finite value");
    }

    let mut heights = Vec::with_capacity(vertices_m.len());
    for x in 0..axis {
        for z in 0..axis {
            heights.push(vertices_m[row_major_index(x, z, axis)][1]);
        }
    }
    Ok(heights)
}

#[must_use]
const fn row_major_index(x: usize, z: usize, axis: usize) -> usize {
    z * axis + x
}

#[must_use]
fn rapier_default_split_in_forge_indices(cell_x: usize, cell_z: usize) -> ([u32; 3], [u32; 3]) {
    let axis = TERRAIN_HEIGHTFIELD_AXIS;
    let top_left =
        u32::try_from(row_major_index(cell_x, cell_z, axis)).expect("33 by 33 indices fit u32");
    let top_right =
        u32::try_from(row_major_index(cell_x + 1, cell_z, axis)).expect("33 by 33 indices fit u32");
    let bottom_left =
        u32::try_from(row_major_index(cell_x, cell_z + 1, axis)).expect("33 by 33 indices fit u32");
    let bottom_right = u32::try_from(row_major_index(cell_x + 1, cell_z + 1, axis))
        .expect("33 by 33 indices fit u32");

    // Rapier's default cell split is (p00,p10,p01) then (p10,p11,p01), where its
    // first index advances +Z and its second advances +X.
    (
        [top_left, bottom_left, top_right],
        [bottom_left, bottom_right, top_right],
    )
}

#[must_use]
const fn same_oriented_triangle(actual: [u32; 3], expected: [u32; 3]) -> bool {
    actual[0] == expected[0] && actual[1] == expected[1] && actual[2] == expected[2]
        || actual[0] == expected[1] && actual[1] == expected[2] && actual[2] == expected[0]
        || actual[0] == expected[2] && actual[1] == expected[0] && actual[2] == expected[1]
}

fn invalid_chunk(chunk: &TerrainChunkMesh, reason: &'static str) -> TerrainPhysicsAdapterError {
    TerrainPhysicsAdapterError::InvalidChunkPayload {
        coordinate: chunk.coordinate,
        reason,
    }
}

fn spawn_terrain_heightfields(
    mut commands: Commands,
    mut prepared: ResMut<PreparedTerrainHeightfields>,
    mut bindings: ResMut<TerrainRuntimeBindings>,
) {
    let heightfields = prepared
        .0
        .take()
        .expect("terrain physics preparation is consumed exactly once at startup");
    let count = heightfields.len();

    for heightfield in heightfields {
        let collider = Collider::heightfield(
            heightfield.heights_column_major,
            heightfield.rows,
            heightfield.columns,
            heightfield.scale,
        );
        let entity = commands
            .spawn((
                Name::new(format!(
                    "Visual Valley terrain collider ({}, {})",
                    heightfield.marker.coordinate.x, heightfield.marker.coordinate.z
                )),
                heightfield.marker,
                RigidBody::Fixed,
                collider,
                Transform::from_translation(heightfield.center),
            ))
            .id();
        bindings.register_collider(heightfield.marker.id, heightfield.marker.coordinate, entity);
    }

    info!(
        target: "forge::physics",
        schema = "forge.runtime-event",
        schema_version = 1_u64,
        event_kind = "terrain_heightfields_materialized",
        colliders = count,
        simulation_hz = TERRAIN_PHYSICS_HZ,
        "static terrain heightfields materialized from generated Forge chunks"
    );
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy_rapier3d::parry::{
        math::Vector,
        query::{Ray, RayCast},
    };
    use forge_world::{
        compile_world_sketch, embedded_visual_valley_terrain_document,
        embedded_visual_valley_world_sketch,
    };

    fn prepared() -> Vec<PreparedTerrainHeightfield> {
        let document = embedded_visual_valley_terrain_document().unwrap();
        prepare_terrain_heightfields(&document).unwrap()
    }

    #[test]
    fn asymmetric_row_major_grid_is_transposed_to_rapier_column_major() {
        let vertices = vec![
            [0.0, 0.0, 0.0],
            [1.0, 1.0, 0.0],
            [2.0, 2.0, 0.0],
            [0.0, 10.0, 1.0],
            [1.0, 11.0, 1.0],
            [2.0, 12.0, 1.0],
            [0.0, 20.0, 2.0],
            [1.0, 21.0, 2.0],
            [2.0, 22.0, 2.0],
        ];

        let heights = row_major_heights_to_rapier_column_major(&vertices, 3).unwrap();

        assert_eq!(
            heights,
            vec![0.0, 10.0, 20.0, 1.0, 11.0, 21.0, 2.0, 12.0, 22.0]
        );
    }

    #[test]
    fn canonical_payloads_have_exact_count_scale_center_and_shape() {
        let payloads = prepared();

        assert_eq!(payloads.len(), TERRAIN_CHUNK_COUNT);
        for (index, payload) in payloads.iter().enumerate() {
            let expected_x = u16::try_from(index % 8).unwrap();
            let expected_z = u16::try_from(index / 8).unwrap();
            assert_eq!(payload.marker.coordinate.x, expected_x);
            assert_eq!(payload.marker.coordinate.z, expected_z);
            assert_eq!(payload.rows, 33);
            assert_eq!(payload.columns, 33);
            assert_eq!(payload.heights_column_major.len(), 1_089);
            assert_eq!(payload.scale, Vec3::new(64.0, 1.0, 64.0));
            assert_eq!(payload.center.y.to_bits(), 0.0_f32.to_bits());
            let expected_center_x = -224.0 + f32::from(expected_x) * 64.0;
            let expected_center_z = -224.0 + f32::from(expected_z) * 64.0;
            assert_eq!(payload.center.x.to_bits(), expected_center_x.to_bits());
            assert_eq!(payload.center.z.to_bits(), expected_center_z.to_bits());
        }
    }

    #[test]
    fn every_forge_cell_uses_the_same_oriented_split_as_rapier() {
        let document = embedded_visual_valley_terrain_document().unwrap();
        let generated = TerrainGenerationPlan::from_document(&document)
            .unwrap()
            .generate()
            .unwrap();

        for chunk in &generated.chunks {
            validate_rapier_split_compatibility(chunk).unwrap();
        }
    }

    #[test]
    fn all_64_payloads_are_finite_and_bit_identical_across_seams() {
        let payloads = prepared();
        assert!(payloads.iter().all(|payload| {
            payload
                .heights_column_major
                .iter()
                .all(|height| height.is_finite())
        }));

        for z in 0..8 {
            for x in 0..7 {
                let left = &payloads[z * 8 + x];
                let right = &payloads[z * 8 + x + 1];
                for row_z in 0..TERRAIN_HEIGHTFIELD_AXIS {
                    assert_eq!(
                        left.heights_column_major
                            [column_major_index(TERRAIN_HEIGHTFIELD_AXIS - 1, row_z)]
                        .to_bits(),
                        right.heights_column_major[column_major_index(0, row_z)].to_bits()
                    );
                }
            }
        }
        for z in 0..7 {
            for x in 0..8 {
                let back = &payloads[z * 8 + x];
                let front = &payloads[(z + 1) * 8 + x];
                for column_x in 0..TERRAIN_HEIGHTFIELD_AXIS {
                    assert_eq!(
                        back.heights_column_major
                            [column_major_index(column_x, TERRAIN_HEIGHTFIELD_AXIS - 1,)]
                        .to_bits(),
                        front.heights_column_major[column_major_index(column_x, 0)].to_bits()
                    );
                }
            }
        }
    }

    #[test]
    fn malformed_or_non_finite_payloads_are_rejected_before_rapier() {
        let document = embedded_visual_valley_terrain_document().unwrap();
        let generated = TerrainGenerationPlan::from_document(&document)
            .unwrap()
            .generate()
            .unwrap();
        let mut malformed = generated.chunks[0].clone();
        malformed.collider.vertices_m[0][1] = f32::NAN;
        malformed.positions_m[0][1] = f32::NAN;
        assert!(matches!(
            prepare_chunk_heightfield(&malformed),
            Err(TerrainPhysicsAdapterError::InvalidChunkPayload {
                reason: "collider contains a non-finite vertex",
                ..
            })
        ));

        let mut wrong_order = generated.chunks[0].clone();
        wrong_order.collider.triangles.swap(0, 1);
        assert!(matches!(
            prepare_chunk_heightfield(&wrong_order),
            Err(TerrainPhysicsAdapterError::InvalidChunkPayload {
                reason: "mesh triangles do not match Rapier's default heightfield split",
                ..
            })
        ));
    }

    #[test]
    fn headless_heightfield_raycast_hits_a_stable_flat_surface() {
        let collider = Collider::heightfield(
            vec![3.25; TERRAIN_HEIGHTFIELD_SAMPLE_COUNT],
            TERRAIN_HEIGHTFIELD_AXIS,
            TERRAIN_HEIGHTFIELD_AXIS,
            Vec3::new(64.0, 1.0, 64.0),
        );
        let heightfield = collider.as_heightfield().unwrap();
        let ray = Ray::new(Vector::new(0.0, 20.0, 0.0), Vector::new(0.0, -1.0, 0.0));

        let time_of_impact = heightfield.raw.cast_local_ray(&ray, 100.0, true).unwrap();

        assert!((time_of_impact - 16.75).abs() <= 1.0e-5);
    }

    #[test]
    fn canonical_adjacent_heightfields_both_answer_near_their_shared_seam() {
        const SEAM_EPSILON_M: f32 = 0.001;
        const SAMPLE_Z: usize = 16;

        let payloads = prepared();
        let left = &payloads[3 * 8];
        let right = &payloads[3 * 8 + 1];
        assert_eq!(
            left.marker.coordinate,
            TerrainChunkCoordinate { x: 0, z: 3 }
        );
        assert_eq!(
            right.marker.coordinate,
            TerrainChunkCoordinate { x: 1, z: 3 }
        );

        let left_seam_height = left.heights_column_major[column_major_index(32, SAMPLE_Z)];
        let right_seam_height = right.heights_column_major[column_major_index(0, SAMPLE_Z)];
        assert_eq!(left_seam_height.to_bits(), right_seam_height.to_bits());

        // Rapier treats the horizontal bounds as half-open during broad-phase traversal. Cast one
        // millimetre inside each adjacent collider so both sides own their query unambiguously.
        let left_local_x = 32.0 - SEAM_EPSILON_M;
        let right_local_x = -32.0 + SEAM_EPSILON_M;
        let local_z = 0.0;
        let sample_fraction = SEAM_EPSILON_M / TERRAIN_SAMPLE_SPACING_M;
        let expected_left_height = lerp(
            left.heights_column_major[column_major_index(31, SAMPLE_Z)],
            left_seam_height,
            1.0 - sample_fraction,
        );
        let expected_right_height = lerp(
            right_seam_height,
            right.heights_column_major[column_major_index(1, SAMPLE_Z)],
            sample_fraction,
        );
        let ray_origin_y = expected_left_height.max(expected_right_height) + 100.0;

        let left_time_of_impact = cast_vertical_ray(left, left_local_x, local_z, ray_origin_y)
            .expect("the left canonical heightfield must answer next to its shared seam");
        let right_time_of_impact = cast_vertical_ray(right, right_local_x, local_z, ray_origin_y)
            .expect("the right canonical heightfield must answer next to its shared seam");
        let left_hit_height = ray_origin_y - left_time_of_impact;
        let right_hit_height = ray_origin_y - right_time_of_impact;

        assert!((left_hit_height - expected_left_height).abs() <= 1.0e-4);
        assert!((right_hit_height - expected_right_height).abs() <= 1.0e-4);
        assert!(
            ((left_time_of_impact - right_time_of_impact)
                - (expected_right_height - expected_left_height))
                .abs()
                <= 1.0e-4
        );
        assert!((left_hit_height - right_hit_height).abs() <= 0.05);
        assert!((left.center.x + left_local_x - (right.center.x + right_local_x)).abs() <= 0.0021);
    }

    #[test]
    fn world_sketch_carved_candidate_reaches_all_64_rapier_heightfields_and_raycast() {
        let compiled =
            compile_world_sketch(&embedded_visual_valley_world_sketch().unwrap()).unwrap();
        assert_eq!(
            compiled.carved_terrain.height_deltas.len(),
            compiled.spline_carving.modified_sample_count
        );
        let payloads = prepare_terrain_heightfields(&compiled.carved_terrain).unwrap();
        assert_eq!(payloads.len(), TERRAIN_CHUNK_COUNT);
        assert!(payloads.iter().all(|payload| {
            payload
                .heights_column_major
                .iter()
                .all(|height| height.is_finite())
        }));

        let probe_delta = compiled
            .carved_terrain
            .height_deltas
            .iter()
            .filter(|delta| {
                delta.sample_x % TERRAIN_CHUNK_INTERVALS != 0
                    && delta.sample_z % TERRAIN_CHUNK_INTERVALS != 0
                    && delta.sample_x < 256
                    && delta.sample_z < 256
            })
            .max_by(|left, right| left.delta_m.abs().total_cmp(&right.delta_m.abs()))
            .expect("carving must modify at least one sample away from a chunk seam");
        let world_x = -256.0 + f32::from(probe_delta.sample_x) * TERRAIN_SAMPLE_SPACING_M + 0.25;
        let world_z = -256.0 + f32::from(probe_delta.sample_z) * TERRAIN_SAMPLE_SPACING_M + 0.25;
        let base_plan = TerrainGenerationPlan::from_document(&compiled.terrain).unwrap();
        let carved_plan = TerrainGenerationPlan::from_document(&compiled.carved_terrain).unwrap();
        let base_height = base_plan.surface_at(world_x, world_z).unwrap().height_m;
        let expected_height = carved_plan.surface_at(world_x, world_z).unwrap().height_m;
        assert!((expected_height - base_height).abs() > 1.0);

        #[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
        let chunk_x = ((world_x + 256.0) / 64.0).floor() as usize;
        #[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
        let chunk_z = ((world_z + 256.0) / 64.0).floor() as usize;
        let payload = &payloads[chunk_z * 8 + chunk_x];
        let local_x = world_x - payload.center.x;
        let local_z = world_z - payload.center.z;
        let origin_y = expected_height + 100.0;
        let time_of_impact = cast_vertical_ray(payload, local_x, local_z, origin_y)
            .expect("the carved Rapier heightfield must answer the terrain probe");
        let hit_height = origin_y - time_of_impact;
        assert!((hit_height - expected_height).abs() <= 1.0e-3);
    }

    fn cast_vertical_ray(
        payload: &PreparedTerrainHeightfield,
        local_x: f32,
        local_z: f32,
        origin_y: f32,
    ) -> Option<f32> {
        let collider = Collider::heightfield(
            payload.heights_column_major.clone(),
            payload.rows,
            payload.columns,
            payload.scale,
        );
        let heightfield = collider
            .as_heightfield()
            .expect("prepared collider must remain a heightfield");
        let ray = Ray::new(
            Vector::new(local_x, origin_y, local_z),
            Vector::new(0.0, -1.0, 0.0),
        );
        heightfield.raw.cast_local_ray(&ray, 200.0, true)
    }

    #[must_use]
    fn lerp(start: f32, end: f32, amount: f32) -> f32 {
        start + (end - start) * amount
    }

    #[must_use]
    const fn column_major_index(x: usize, z: usize) -> usize {
        z + x * TERRAIN_HEIGHTFIELD_AXIS
    }
}
