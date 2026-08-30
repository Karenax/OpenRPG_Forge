use super::*;
use crate::VisualValleyTerrainRecipe;

fn plan() -> TerrainGenerationPlan {
    TerrainGenerationPlan::from_document(&VisualValleyTerrainRecipe::default().build().unwrap())
        .unwrap()
}

fn generated() -> GeneratedTerrain {
    plan().generate().unwrap()
}

#[test]
#[allow(clippy::float_cmp)]
fn centred_plan_and_chunks_cover_every_interval_exactly_once() {
    let generated = generated();
    assert_eq!(generated.origin_xz_m, [-256.0, -256.0]);
    assert_eq!(generated.chunks.len(), TERRAIN_CHUNK_COUNT);
    assert_eq!(generated.vertex_count(), TERRAIN_VERTEX_COUNT);
    assert_eq!(generated.triangle_count(), TERRAIN_TRIANGLE_COUNT);
    assert_eq!(generated.chunks[0].minimum_xz_m, [-256.0, -256.0]);
    assert_eq!(generated.chunks[0].maximum_xz_m, [-192.0, -192.0]);
    assert_eq!(generated.chunks[63].minimum_xz_m, [192.0, 192.0]);
    assert_eq!(generated.chunks[63].maximum_xz_m, [256.0, 256.0]);

    let identities = generated
        .chunks
        .iter()
        .map(|chunk| chunk.id)
        .collect::<std::collections::HashSet<_>>();
    assert_eq!(identities.len(), TERRAIN_CHUNK_COUNT);
    for (index, chunk) in generated.chunks.iter().enumerate() {
        assert_eq!(
            chunk.coordinate,
            TerrainChunkCoordinate {
                x: u16::try_from(index % usize::from(TERRAIN_CHUNKS_PER_AXIS)).unwrap(),
                z: u16::try_from(index / usize::from(TERRAIN_CHUNKS_PER_AXIS)).unwrap(),
            }
        );
    }

    let axis = usize::from(TERRAIN_GLOBAL_INTERVALS_PER_AXIS);
    let mut coverage = vec![0_u8; axis * axis];
    for chunk in &generated.chunks {
        let first_x = usize::from(chunk.coordinate.x * TERRAIN_CHUNK_INTERVALS);
        let first_z = usize::from(chunk.coordinate.z * TERRAIN_CHUNK_INTERVALS);
        for z in first_z..first_z + usize::from(TERRAIN_CHUNK_INTERVALS) {
            for x in first_x..first_x + usize::from(TERRAIN_CHUNK_INTERVALS) {
                coverage[z * axis + x] += 1;
            }
        }
    }
    assert!(coverage.iter().all(|count| *count == 1));
}

#[test]
fn golden_heights_lock_fractal_value_noise_v1_semantics() {
    let plan = plan();
    let samples = [
        plan.height_at(-256.0, -256.0).unwrap().to_bits(),
        plan.height_at(0.0, 0.0).unwrap().to_bits(),
        plan.height_at(64.0, -32.0).unwrap().to_bits(),
        plan.height_at(256.0, 256.0).unwrap().to_bits(),
    ];
    assert_eq!(
        samples,
        [1_079_024_385, 3_252_108_159, 3_248_088_013, 1_101_064_328]
    );
}

#[test]
fn triangles_wind_counter_clockwise_from_above() {
    let generated = generated();
    for chunk in &generated.chunks {
        let (triangles, remainder) = chunk.indices.as_chunks::<3>();
        assert!(remainder.is_empty());
        for triangle in triangles {
            let a = chunk.positions_m[usize::try_from(triangle[0]).unwrap()];
            let b = chunk.positions_m[usize::try_from(triangle[1]).unwrap()];
            let c = chunk.positions_m[usize::try_from(triangle[2]).unwrap()];
            let ab = [b[0] - a[0], b[1] - a[1], b[2] - a[2]];
            let ac = [c[0] - a[0], c[1] - a[1], c[2] - a[2]];
            let cross_y = ab[2] * ac[0] - ab[0] * ac[2];
            assert!(cross_y > 0.0);
        }
    }
}

#[test]
#[allow(clippy::float_cmp)]
fn normals_are_unit_length_and_bit_identical_across_seams() {
    let generated = generated();
    for chunk in &generated.chunks {
        for normal in &chunk.normals {
            let length =
                (normal[0] * normal[0] + normal[1] * normal[1] + normal[2] * normal[2]).sqrt();
            assert!((length - 1.0).abs() <= 1.0e-6);
            assert!(normal[1] > 0.0);
        }
    }

    let vertices = usize::from(TERRAIN_CHUNK_VERTICES_PER_AXIS);
    for z in 0..TERRAIN_CHUNKS_PER_AXIS {
        for x in 0..TERRAIN_CHUNKS_PER_AXIS - 1 {
            let left = &generated.chunks[usize::from(z * TERRAIN_CHUNKS_PER_AXIS + x)];
            let right = &generated.chunks[usize::from(z * TERRAIN_CHUNKS_PER_AXIS + x + 1)];
            for row in 0..vertices {
                assert_eq!(
                    left.positions_m[row * vertices + vertices - 1],
                    right.positions_m[row * vertices]
                );
                assert_eq!(
                    left.normals[row * vertices + vertices - 1],
                    right.normals[row * vertices]
                );
                assert_eq!(
                    left.uvs[row * vertices + vertices - 1],
                    right.uvs[row * vertices]
                );
                assert_eq!(
                    left.material_weights[row * vertices + vertices - 1],
                    right.material_weights[row * vertices]
                );
            }
        }
    }
    for z in 0..TERRAIN_CHUNKS_PER_AXIS - 1 {
        for x in 0..TERRAIN_CHUNKS_PER_AXIS {
            let back = &generated.chunks[usize::from(z * TERRAIN_CHUNKS_PER_AXIS + x)];
            let front = &generated.chunks[usize::from((z + 1) * TERRAIN_CHUNKS_PER_AXIS + x)];
            for column in 0..vertices {
                assert_eq!(
                    back.positions_m[(vertices - 1) * vertices + column],
                    front.positions_m[column]
                );
                assert_eq!(
                    back.normals[(vertices - 1) * vertices + column],
                    front.normals[column]
                );
                assert_eq!(
                    back.uvs[(vertices - 1) * vertices + column],
                    front.uvs[column]
                );
                assert_eq!(
                    back.material_weights[(vertices - 1) * vertices + column],
                    front.material_weights[column]
                );
            }
        }
    }
}

#[test]
fn material_weights_are_finite_non_negative_and_normalized() {
    let generated = generated();
    for chunk in &generated.chunks {
        for weights in &chunk.material_weights {
            let values = weights.as_array();
            assert!(
                values
                    .iter()
                    .all(|value| value.is_finite() && *value >= 0.0)
            );
            assert!((values.iter().sum::<f32>() - 1.0).abs() <= 1.0e-6);
        }
    }
}

#[test]
fn collider_payload_preserves_explicit_mesh_order() {
    let generated = generated();
    for chunk in &generated.chunks {
        assert_eq!(
            chunk.collider.vertex_order,
            TerrainColliderVertexOrder::RowMajorZThenX
        );
        assert_eq!(
            chunk.collider.triangle_order,
            TerrainColliderTriangleOrder::CellRowMajorZThenXCounterClockwiseFromAbove
        );
        assert_eq!(chunk.collider.vertices_m, chunk.positions_m);
        let flattened: Vec<u32> = chunk.collider.triangles.iter().flatten().copied().collect();
        assert_eq!(flattened, chunk.indices);
    }
}

#[test]
fn generation_replays_byte_exactly_and_has_a_golden_hash() {
    let first = generated();
    let replay = generated();
    assert_eq!(first, replay);
    assert_eq!(
        first.content_hash().unwrap().to_string(),
        "e01ecc7a878c2df6cf698feb2de557eb011af0c3fa307ef3b21ea29d0aa42af0"
    );

    let changed_document = VisualValleyTerrainRecipe {
        seed: crate::VISUAL_VALLEY_SEED + 1,
    }
    .build()
    .unwrap();
    let changed = TerrainGenerationPlan::from_document(&changed_document)
        .unwrap()
        .generate()
        .unwrap();
    assert_ne!(
        first.content_hash().unwrap(),
        changed.content_hash().unwrap()
    );
    assert_ne!(first.chunks[0].id, changed.chunks[0].id);
}

#[test]
fn dirty_region_plans_interior_border_corner_and_outer_corner() {
    let plan = plan();
    let interior = plan
        .plan_dirty_region(TerrainSampleRegion::point(16, 16))
        .unwrap();
    assert_eq!(
        interior.dependency_samples,
        TerrainSampleRegion::new(15, 15, 17, 17).unwrap()
    );
    assert_eq!(
        interior
            .affected_chunks
            .iter()
            .map(|chunk| chunk.coordinate)
            .collect::<Vec<_>>(),
        vec![TerrainChunkCoordinate { x: 0, z: 0 }]
    );

    let border = plan
        .plan_dirty_region(TerrainSampleRegion::point(32, 16))
        .unwrap();
    assert_eq!(
        border
            .affected_chunks
            .iter()
            .map(|chunk| chunk.coordinate)
            .collect::<Vec<_>>(),
        vec![
            TerrainChunkCoordinate { x: 0, z: 0 },
            TerrainChunkCoordinate { x: 1, z: 0 },
        ]
    );

    let corner = plan
        .plan_dirty_region(TerrainSampleRegion::point(32, 32))
        .unwrap();
    assert_eq!(
        corner
            .affected_chunks
            .iter()
            .map(|chunk| chunk.coordinate)
            .collect::<Vec<_>>(),
        vec![
            TerrainChunkCoordinate { x: 0, z: 0 },
            TerrainChunkCoordinate { x: 1, z: 0 },
            TerrainChunkCoordinate { x: 0, z: 1 },
            TerrainChunkCoordinate { x: 1, z: 1 },
        ]
    );

    let outer_corner = plan
        .plan_dirty_region(TerrainSampleRegion::point(0, 0))
        .unwrap();
    assert_eq!(
        outer_corner.dependency_samples,
        TerrainSampleRegion::new(0, 0, 1, 1).unwrap()
    );
    assert_eq!(outer_corner.affected_chunks.len(), 1);

    let near_corner = plan
        .plan_dirty_region(TerrainSampleRegion::point(31, 31))
        .unwrap();
    assert_eq!(
        near_corner.dependency_samples,
        TerrainSampleRegion::new(30, 30, 32, 32).unwrap()
    );
    assert_eq!(
        near_corner
            .affected_chunks
            .iter()
            .map(|chunk| chunk.coordinate)
            .collect::<Vec<_>>(),
        vec![
            TerrainChunkCoordinate { x: 0, z: 0 },
            TerrainChunkCoordinate { x: 1, z: 0 },
            TerrainChunkCoordinate { x: 0, z: 1 },
        ]
    );
}

#[test]
fn invalid_plan_inputs_and_dirty_ranges_are_rejected() {
    let mut document = VisualValleyTerrainRecipe::default().build().unwrap();
    document.extent_m.size_x_m = 256.0;
    assert!(matches!(
        TerrainGenerationPlan::from_document(&document),
        Err(TerrainGenerationError::UnsupportedExtent)
    ));

    let plan = plan();
    assert!(matches!(
        plan.height_at(f32::NAN, 0.0),
        Err(TerrainGenerationError::NonFiniteSampleCoordinate)
    ));
    assert!(matches!(
        plan.height_at(259.0, 0.0),
        Err(TerrainGenerationError::SampleCoordinateOutOfBounds)
    ));
    assert!(matches!(
        plan.plan_dirty_region(TerrainSampleRegion::point(257, 0)),
        Err(TerrainGenerationError::DirtyRegionOutOfBounds)
    ));
    assert!(matches!(
        TerrainSampleRegion::new(2, 0, 1, 0),
        Err(TerrainGenerationError::InvertedDirtyRegion)
    ));
}

#[test]
fn valid_documents_with_unsafe_sampling_arithmetic_are_rejected_before_generation() {
    let mut excessive_frequency = VisualValleyTerrainRecipe::default().build().unwrap();
    excessive_frequency
        .height_sampling
        .base_frequency_cycles_per_m = f32::MAX;
    assert!(excessive_frequency.validate().is_ok());
    assert!(matches!(
        TerrainGenerationPlan::from_document(&excessive_frequency),
        Err(TerrainGenerationError::OctaveLatticeCoordinateOutOfRange { octave: 0 })
    ));

    let mut excessive_lacunarity = VisualValleyTerrainRecipe::default().build().unwrap();
    excessive_lacunarity.height_sampling.lacunarity = f32::MAX;
    assert!(excessive_lacunarity.validate().is_ok());
    assert!(matches!(
        TerrainGenerationPlan::from_document(&excessive_lacunarity),
        Err(TerrainGenerationError::OctaveLatticeCoordinateOutOfRange { octave: 1 })
    ));

    let mut excessive_height_range = VisualValleyTerrainRecipe::default().build().unwrap();
    excessive_height_range.height_sampling.base_elevation_m = f32::MAX;
    excessive_height_range.height_sampling.amplitude_m = f32::MAX;
    assert!(excessive_height_range.validate().is_ok());
    assert!(matches!(
        TerrainGenerationPlan::from_document(&excessive_height_range),
        Err(TerrainGenerationError::HeightOutputOutOfRange)
    ));
}

#[test]
fn maximum_finite_amplitude_stays_finite_through_derived_payloads() {
    let mut document = VisualValleyTerrainRecipe::default().build().unwrap();
    document.height_sampling.base_elevation_m = 0.0;
    document.height_sampling.amplitude_m = f32::MAX;
    assert!(document.validate().is_ok());

    let generated = TerrainGenerationPlan::from_document(&document)
        .unwrap()
        .generate()
        .unwrap();
    for chunk in &generated.chunks {
        assert!(
            chunk
                .positions_m
                .iter()
                .flatten()
                .all(|value| value.is_finite())
        );
        assert!(
            chunk
                .normals
                .iter()
                .flatten()
                .all(|value| value.is_finite())
        );
        assert!(
            chunk
                .material_weights
                .iter()
                .all(|weights| weights.as_array().iter().all(|value| value.is_finite()))
        );
    }
    assert!(generated.content_hash().is_ok());
}
