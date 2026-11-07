use super::*;
use crate::VisualValleyTerrainRecipe;
use crate::cell_streaming::CellStreamer;
use forge_core::{EntityId, bytes_sha256};
use forge_project::{
    TERRAIN_DOCUMENT_SCHEMA_VERSION_V3, TerrainHeightDelta, TerrainMaterialOverride,
    TerrainMaterialWeight,
};

fn fixture() -> (Arc<TerrainGenerationPlan>, CellStreamingPolicy) {
    let mut document = VisualValleyTerrainRecipe::default().build().unwrap();
    document.schema_version = TERRAIN_DOCUMENT_SCHEMA_VERSION_V3;
    document.height_deltas = vec![TerrainHeightDelta::new(128, 100, 5.0)];
    document.material_overrides = vec![TerrainMaterialOverride {
        sample_x: 128,
        sample_z: 100,
        weights: vec![TerrainMaterialWeight {
            layer_id: document.material_layers[1].id.clone(),
            weight: u16::MAX,
        }],
    }];
    let terrain = Arc::new(TerrainGenerationPlan::from_document(&document).unwrap());
    let policy = CellStreamingPolicy {
        origin_xz_m: [-256.0, -256.0],
        cell_size_m: 64.0,
        dimensions: [8, 8],
        detail_radius: 0,
        far_radius: 1,
        max_resident_cells: 9,
        full_cell_bytes: 100_000,
        hlod_cell_bytes: 30_000,
        max_resident_bytes: 500_000,
        max_staging_bytes: 1_000_000,
    };
    (terrain, policy)
}

fn assert_array_bits_equal<const N: usize>(left: &[[f32; N]], right: &[[f32; N]]) {
    assert_eq!(left.len(), right.len());
    assert!(
        left.iter()
            .zip(right)
            .all(|(a, b)| a.map(f32::to_bits) == b.map(f32::to_bits))
    );
}

#[test]
fn full_cells_match_existing_chunk_and_collider_including_identity_and_edits() {
    let (terrain, policy) = fixture();
    let source = ProceduralCellSource::new(terrain.clone(), policy, 8).unwrap();
    let all = terrain.generate().unwrap();
    for key in [
        CellKey { x: 0, z: 0 },
        CellKey { x: 3, z: 3 },
        CellKey { x: 4, z: 3 },
        CellKey { x: 7, z: 7 },
    ] {
        let cell = source.build(key, CellDetail::Full).unwrap();
        let chunk = &all.chunks[usize::from(key.z * TERRAIN_CHUNKS_PER_AXIS + key.x)];
        assert_eq!(cell.source_chunk_id, chunk.id);
        assert_eq!(cell.acknowledgement.key, key);
        assert_eq!(cell.acknowledgement.detail, CellDetail::Full);
        assert_eq!(cell.acknowledgement.source, source.residency_source());
        assert_array_bits_equal(&cell.positions_m, &chunk.positions_m);
        assert_array_bits_equal(&cell.positions_m, &chunk.collider.vertices_m);
        assert_array_bits_equal(&cell.normals, &chunk.normals);
        assert_array_bits_equal(&cell.uvs, &chunk.uvs);
        assert_eq!(cell.material_weights.len(), chunk.material_weights.len());
        assert!(
            cell.material_weights
                .iter()
                .zip(&chunk.material_weights)
                .all(|(a, b)| { a.as_array().map(f32::to_bits) == b.as_array().map(f32::to_bits) })
        );
        assert_eq!(cell.indices, chunk.indices);
        assert_eq!(
            cell.indices.as_chunks::<3>().0,
            chunk.collider.triangles.as_slice()
        );
        assert_eq!(cell.acknowledgement.bytes, buffer_bytes(&cell).unwrap());
    }
}

fn assert_seam(full: &ProceduralCellMesh, distant: &ProceduralCellMesh, axis: usize) {
    let mut matches = 0;
    for (index, point) in full.positions_m.iter().enumerate() {
        if point[axis].to_bits() != 0.0_f32.to_bits() {
            continue;
        }
        let neighbour = distant
            .positions_m
            .iter()
            .position(|other| other.map(f32::to_bits) == point.map(f32::to_bits))
            .expect("every boundary sample is retained by the distant topology");
        assert_eq!(
            full.normals[index].map(f32::to_bits),
            distant.normals[neighbour].map(f32::to_bits)
        );
        assert_eq!(
            full.uvs[index].map(f32::to_bits),
            distant.uvs[neighbour].map(f32::to_bits)
        );
        assert_eq!(
            full.material_weights[index].as_array().map(f32::to_bits),
            distant.material_weights[neighbour]
                .as_array()
                .map(f32::to_bits)
        );
        matches += 1;
    }
    assert_eq!(matches, usize::from(TERRAIN_CHUNK_VERTICES_PER_AXIS));
}

#[test]
fn all_supported_distant_strides_keep_every_full_detail_seam_and_authored_paint() {
    let (terrain, policy) = fixture();
    for stride in [2, 4, 8, 16, 32] {
        let source = ProceduralCellSource::new(terrain.clone(), policy.clone(), stride).unwrap();
        let full = source
            .build(CellKey { x: 3, z: 3 }, CellDetail::Full)
            .unwrap();
        let right = source
            .build(CellKey { x: 4, z: 3 }, CellDetail::Hlod)
            .unwrap();
        let front = source
            .build(CellKey { x: 3, z: 4 }, CellDetail::Hlod)
            .unwrap();
        assert_seam(&full, &right, 0);
        assert_seam(&full, &front, 2);
        assert!(right.indices.len() < full.indices.len());
        assert!(right.acknowledgement.bytes < full.acknowledgement.bytes);
        for cell in [&full, &right] {
            let index = cell
                .positions_m
                .iter()
                .position(|p| [p[0], p[2]].map(f32::to_bits) == [0.0_f32, -56.0].map(f32::to_bits))
                .unwrap();
            assert_eq!(
                cell.material_weights[index].as_array().map(f32::to_bits),
                [0.0_f32, 1.0, 0.0].map(f32::to_bits)
            );
        }
    }
}

#[test]
fn prepared_cells_are_pinned_bounded_and_transactionally_committed() {
    let (terrain, policy) = fixture();
    let source = ProceduralCellSource::new(terrain.clone(), policy.clone(), 8).unwrap();
    let mut stream = CellStreamer::new(
        EntityId::deterministic("procedural-cell-test"),
        source.residency_source(),
        policy.clone(),
    )
    .unwrap();
    let plan = stream.prepare([-32.0, -32.0]).unwrap();
    let different_stride = ProceduralCellSource::new(terrain.clone(), policy.clone(), 4).unwrap();
    assert_ne!(
        source.residency_source(),
        different_stride.residency_source()
    );
    assert!(different_stride.prepare(&plan).is_err());
    let cells = source.prepare(&plan).unwrap();
    assert_eq!(cells.len(), 9);
    assert_eq!(cells.len(), plan.acquisitions().len());
    stream
        .commit(
            &plan,
            &cells
                .iter()
                .map(|cell| cell.acknowledgement)
                .collect::<Vec<_>>(),
        )
        .unwrap();
    assert_eq!(stream.resident().len(), 9);
    assert!(
        stream
            .resident()
            .values()
            .map(|cell| cell.bytes)
            .sum::<u64>()
            <= policy.max_resident_bytes
    );

    let next = stream.prepare([32.0, 32.0]).unwrap();
    assert!(!next.retirements().is_empty());
    let replacement = source.prepare(&next).unwrap();
    stream
        .commit(
            &next,
            &replacement
                .iter()
                .map(|cell| cell.acknowledgement)
                .collect::<Vec<_>>(),
        )
        .unwrap();
    assert_eq!(stream.resident().len(), 9);
    assert!(stream.commit(&next, &[]).is_err());
}

#[test]
fn invalid_grid_source_policy_and_byte_budgets_are_refused() {
    let (terrain, policy) = fixture();
    for stride in [0, 1, 3, 33, u8::MAX] {
        assert!(ProceduralCellSource::new(terrain.clone(), policy.clone(), stride).is_err());
    }
    let mut invalid = policy.clone();
    invalid.origin_xz_m[0] += 1.0;
    assert!(ProceduralCellSource::new(terrain.clone(), invalid, 8).is_err());
    let mut invalid = policy.clone();
    invalid.dimensions = [7, 8];
    assert!(ProceduralCellSource::new(terrain.clone(), invalid, 8).is_err());
    let mut invalid = policy.clone();
    invalid.cell_size_m = 63.0;
    assert!(ProceduralCellSource::new(terrain.clone(), invalid, 8).is_err());
    let mut invalid = policy.clone();
    invalid.full_cell_bytes = 1;
    assert!(ProceduralCellSource::new(terrain.clone(), invalid, 8).is_err());
    let mut invalid = policy.clone();
    invalid.hlod_cell_bytes = 1;
    assert!(ProceduralCellSource::new(terrain.clone(), invalid, 8).is_err());
    let source = ProceduralCellSource::new(terrain.clone(), policy.clone(), 8).unwrap();
    for key in [
        CellKey { x: 8, z: 0 },
        CellKey { x: 0, z: 8 },
        CellKey { x: u16::MAX, z: 0 },
    ] {
        assert!(source.build(key, CellDetail::Full).is_err());
    }
    let wrong_source = CellStreamer::new(
        EntityId::deterministic("other"),
        bytes_sha256(b"other"),
        policy.clone(),
    )
    .unwrap();
    assert!(
        source
            .prepare(&wrong_source.prepare([-32.0, -32.0]).unwrap())
            .is_err()
    );
    let mut other_policy = policy.clone();
    other_policy.detail_radius = 1;
    other_policy.far_radius = 2;
    other_policy.max_resident_cells = 25;
    other_policy.max_resident_bytes = 2_000_000;
    other_policy.max_staging_bytes = 4_000_000;
    let other = CellStreamer::new(
        EntityId::deterministic("other-policy"),
        source.residency_source(),
        other_policy,
    )
    .unwrap();
    assert!(
        source
            .prepare(&other.prepare([-32.0, -32.0]).unwrap())
            .is_err()
    );

    let stream = CellStreamer::new(
        EntityId::deterministic("budget"),
        source.residency_source(),
        policy.clone(),
    )
    .unwrap();
    let plan = stream.prepare([-32.0, -32.0]).unwrap();
    let mut forged_peak = plan.clone();
    forged_peak.estimated_peak_bytes = 0;
    forged_peak.resident_before_bytes = policy.max_staging_bytes;
    assert!(source.prepare(&forged_peak).is_err());
    let mut forged_resident = plan;
    forged_resident.estimated_resident_bytes = 0;
    forged_resident.retained_bytes = policy.max_resident_bytes;
    assert!(source.prepare(&forged_resident).is_err());
}
