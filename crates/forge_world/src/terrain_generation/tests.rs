use super::*;
use crate::VisualValleyTerrainRecipe;
use forge_core::{ContentHash, Revision};
use forge_project::{TERRAIN_DOCUMENT_SCHEMA_VERSION, TerrainHeightDelta};

fn plan() -> TerrainGenerationPlan {
    TerrainGenerationPlan::from_document(&VisualValleyTerrainRecipe::default().build().unwrap())
        .unwrap()
}

fn generated() -> GeneratedTerrain {
    plan().generate().unwrap()
}

#[test]
fn shading_normal_matches_emitted_mesh_without_changing_collision_sample() {
    let source =
        crate::compile_world_sketch(&crate::embedded_visual_valley_world_sketch().unwrap())
            .unwrap();
    let plan = TerrainGenerationPlan::from_document(&source.carved_terrain).unwrap();
    let mesh = plan.generate().unwrap();
    for chunk in &mesh.chunks {
        for triangle in chunk.indices.as_chunks::<3>().0.iter().step_by(127) {
            let weights = [0.2, 0.3, 0.5];
            let mut point = [0.0; 3];
            let mut expected = [0.0; 3];
            for (index, weight) in triangle.iter().zip(weights) {
                for axis in 0..3 {
                    point[axis] += chunk.positions_m[*index as usize][axis] * weight;
                    expected[axis] += chunk.normals[*index as usize][axis] * weight;
                }
            }
            let length = expected.iter().map(|v| v * v).sum::<f32>().sqrt();
            let (surface, normal) = plan
                .surface_with_shading_normal_at(point[0], point[2])
                .unwrap();
            assert_eq!(surface, plan.surface_at(point[0], point[2]).unwrap());
            for axis in 0..3 {
                assert!((normal[axis] - expected[axis] / length).abs() < 1.0e-4);
            }
        }
    }
    assert!(plan.surface_with_shading_normal_at(f32::NAN, 0.0).is_err());
    assert!(plan.surface_with_shading_normal_at(257.0, 0.0).is_err());
}

#[test]
fn height_only_surface_is_bit_exact_for_both_triangles_edges_and_authored_relief() {
    let source =
        crate::compile_world_sketch(&crate::embedded_visual_valley_world_sketch().unwrap())
            .unwrap();
    for plan in [
        plan(),
        TerrainGenerationPlan::from_document(&source.carved_terrain).unwrap(),
    ] {
        let hash = plan.document_hash();
        for cell_x in [0_u16, 1, 31, 32, 127, 128, 254, 255] {
            for cell_z in [0_u16, 31, 32, 127, 128, 255] {
                for (u, v) in [(0.0, 0.0), (0.1, 0.2), (0.5, 0.5), (0.9, 0.8), (1.0, 1.0)] {
                    let x = -256.0 + (f32::from(cell_x) + u) * 2.0;
                    let z = -256.0 + (f32::from(cell_z) + v) * 2.0;
                    assert_eq!(
                        plan.surface_height_at(x, z).unwrap().to_bits(),
                        plan.surface_at(x, z).unwrap().height_m.to_bits()
                    );
                }
            }
        }
        for (x, z) in [
            (f32::NAN, 0.0),
            (0.0, f32::INFINITY),
            (-256.1, 0.0),
            (0.0, 256.1),
        ] {
            assert_eq!(
                plan.surface_height_at(x, z).unwrap_err().to_string(),
                plan.surface_at(x, z).unwrap_err().to_string()
            );
        }
        assert_eq!(plan.document_hash(), hash);
    }
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
fn surface_samples_match_both_mesh_triangles_and_choose_first_on_diagonal() {
    let plan = plan();
    let terrain = generated();
    let chunk = &terrain.chunks[0];
    let vertices = usize::from(TERRAIN_CHUNK_VERTICES_PER_AXIS);
    let a = 0;
    let b = 1;
    let c = vertices;
    let d = vertices + 1;
    assert_ne!(
        triangle_geometry([
            chunk.positions_m[a],
            chunk.positions_m[c],
            chunk.positions_m[b],
        ])
        .0
        .map(f32::to_bits),
        triangle_geometry([
            chunk.positions_m[b],
            chunk.positions_m[c],
            chunk.positions_m[d],
        ])
        .0
        .map(f32::to_bits),
        "the diagonal oracle must distinguish the two triangle normals"
    );

    let cases = [
        (-256.0, -256.0, [a, c, b], [1.0, 0.0, 0.0]),
        (-255.5, -255.5, [a, c, b], [0.5, 0.25, 0.25]),
        (-254.5, -254.5, [b, c, d], [0.25, 0.25, 0.5]),
        (-255.5, -254.5, [a, c, b], [0.0, 0.75, 0.25]),
    ];
    for (x_m, z_m, indices, barycentric) in cases {
        let surface = plan.surface_at(x_m, z_m).unwrap();
        #[allow(clippy::cast_possible_truncation)]
        let expected_height = (f64::from(chunk.positions_m[indices[0]][1])
            * f64::from(barycentric[0])
            + f64::from(chunk.positions_m[indices[1]][1]) * f64::from(barycentric[1])
            + f64::from(chunk.positions_m[indices[2]][1]) * f64::from(barycentric[2]))
            as f32;
        assert_eq!(surface.height_m.to_bits(), expected_height.to_bits());

        let expected_weights = normalized_weights(TerrainMaterialWeights {
            grass: chunk.material_weights[indices[0]].grass * barycentric[0]
                + chunk.material_weights[indices[1]].grass * barycentric[1]
                + chunk.material_weights[indices[2]].grass * barycentric[2],
            earth: chunk.material_weights[indices[0]].earth * barycentric[0]
                + chunk.material_weights[indices[1]].earth * barycentric[1]
                + chunk.material_weights[indices[2]].earth * barycentric[2],
            rock: chunk.material_weights[indices[0]].rock * barycentric[0]
                + chunk.material_weights[indices[1]].rock * barycentric[1]
                + chunk.material_weights[indices[2]].rock * barycentric[2],
        });
        assert_eq!(surface.material_weights, expected_weights);
        let expected_geometry = triangle_geometry([
            chunk.positions_m[indices[0]],
            chunk.positions_m[indices[1]],
            chunk.positions_m[indices[2]],
        ]);
        assert_eq!(
            surface.normal.map(f32::to_bits),
            expected_geometry.0.map(f32::to_bits)
        );
        assert_eq!(
            surface.slope_gradient_squared.to_bits(),
            expected_geometry.1.to_bits()
        );
    }
}

#[test]
fn surface_sampling_rejects_non_finite_and_out_of_bounds_coordinates() {
    let plan = plan();
    assert!(matches!(
        plan.surface_at(f32::NAN, 0.0),
        Err(TerrainGenerationError::NonFiniteSampleCoordinate)
    ));
    assert!(matches!(
        plan.surface_at(256.1, 0.0),
        Err(TerrainGenerationError::SurfaceCoordinateOutOfBounds)
    ));
}

fn normalized_weights(weights: TerrainMaterialWeights) -> TerrainMaterialWeights {
    let grass = weights.grass.max(0.0);
    let earth = weights.earth.max(0.0);
    let rock = weights.rock.max(0.0);
    let total = grass + earth + rock;
    if total <= 0.000_001 {
        TerrainMaterialWeights {
            grass: 1.0,
            earth: 0.0,
            rock: 0.0,
        }
    } else {
        TerrainMaterialWeights {
            grass: grass / total,
            earth: earth / total,
            rock: rock / total,
        }
    }
}

fn triangle_geometry(vertices: [[f32; 3]; 3]) -> ([f32; 3], f64) {
    let first_edge = [
        f64::from(vertices[1][0]) - f64::from(vertices[0][0]),
        f64::from(vertices[1][1]) - f64::from(vertices[0][1]),
        f64::from(vertices[1][2]) - f64::from(vertices[0][2]),
    ];
    let second_edge = [
        f64::from(vertices[2][0]) - f64::from(vertices[0][0]),
        f64::from(vertices[2][1]) - f64::from(vertices[0][1]),
        f64::from(vertices[2][2]) - f64::from(vertices[0][2]),
    ];
    let cross = [
        first_edge[1] * second_edge[2] - first_edge[2] * second_edge[1],
        first_edge[2] * second_edge[0] - first_edge[0] * second_edge[2],
        first_edge[0] * second_edge[1] - first_edge[1] * second_edge[0],
    ];
    let inverse_length = (cross[0] * cross[0] + cross[1] * cross[1] + cross[2] * cross[2])
        .sqrt()
        .recip();
    #[allow(clippy::cast_possible_truncation)]
    let normal = [
        (cross[0] * inverse_length) as f32,
        (cross[1] * inverse_length) as f32,
        (cross[2] * inverse_length) as f32,
    ];
    let slope_gradient_squared =
        (cross[0] * cross[0] + cross[2] * cross[2]) / (cross[1] * cross[1]);
    (normal, slope_gradient_squared)
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

    let plan = TerrainGenerationPlan::from_document(&document).unwrap();
    for (x_m, z_m) in [
        (-256.0, -256.0),
        (-255.5, -255.5),
        (0.0, 0.0),
        (255.5, 255.5),
        (256.0, 256.0),
    ] {
        let surface = plan.surface_at(x_m, z_m).unwrap();
        assert!(surface.height_m.is_finite());
        assert!(surface.normal.into_iter().all(f32::is_finite));
        assert!(surface.slope_gradient_squared.is_finite());
    }
    let generated = plan.generate().unwrap();
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

#[test]
#[allow(
    clippy::too_many_lines,
    reason = "the end-to-end partial-update oracle keeps preparation, commit, rebuild parity, and allocation identity assertions in one auditable proof"
)]
fn sparse_delta_at_31_31_patches_three_meshes_one_collider_and_matches_full_rebuild() {
    let source_document = VisualValleyTerrainRecipe::default().build().unwrap();
    let source_document_before = source_document.clone();
    let source_plan = TerrainGenerationPlan::from_document(&source_document).unwrap();
    let mut source_cache = source_plan.generate().unwrap();
    let source_cache_before = source_cache.clone();
    let allocation_addresses_before = source_cache
        .chunks
        .iter()
        .map(chunk_allocation_addresses)
        .collect::<Vec<_>>();

    let mut target_document = source_document.migrate_to_latest().unwrap();
    target_document.revision = source_document.revision.next().unwrap();
    target_document.height_deltas = vec![TerrainHeightDelta::new(31, 31, 5.0)];
    assert_eq!(
        target_document.schema_version,
        TERRAIN_DOCUMENT_SCHEMA_VERSION
    );
    let update = source_plan
        .apply_partial_update(
            &source_cache,
            TerrainPartialUpdateRequest {
                base_revision: source_document.revision,
                base_document_hash: source_plan.document_hash(),
                target_document: target_document.clone(),
            },
        )
        .unwrap();

    assert_eq!(source_document, source_document_before);
    assert_eq!(source_cache, source_cache_before);
    assert_eq!(
        update.changed_samples(),
        &[TerrainHeightSampleCoordinate::new(31, 31)]
    );
    assert_eq!(update.replacement_chunks().len(), 3);
    assert_eq!(
        update
            .mesh_chunks()
            .iter()
            .map(|chunk| chunk.coordinate)
            .collect::<Vec<_>>(),
        vec![
            TerrainChunkCoordinate { x: 0, z: 0 },
            TerrainChunkCoordinate { x: 1, z: 0 },
            TerrainChunkCoordinate { x: 0, z: 1 },
        ]
    );
    assert_eq!(
        update
            .collider_chunks()
            .iter()
            .map(|chunk| chunk.coordinate)
            .collect::<Vec<_>>(),
        vec![TerrainChunkCoordinate { x: 0, z: 0 }]
    );
    assert_eq!(update.source_document_hash(), source_plan.document_hash());
    assert_eq!(update.target_document(), &target_document);

    let full_rebuild = TerrainGenerationPlan::from_document(&target_document)
        .unwrap()
        .generate()
        .unwrap();
    assert_eq!(
        update.target_document_hash(),
        full_rebuild.source_document_hash
    );
    let commit = update.apply_to_cache(&mut source_cache).unwrap();
    assert_eq!(commit.target_document(), &target_document);
    assert_eq!(commit.source_document_hash(), source_plan.document_hash());
    assert_eq!(
        commit.target_document_hash(),
        full_rebuild.source_document_hash
    );
    assert_eq!(
        commit.changed_samples(),
        &[TerrainHeightSampleCoordinate::new(31, 31)]
    );
    assert_eq!(commit.mesh_chunks().len(), 3);
    assert_eq!(commit.collider_chunks().len(), 1);
    assert_eq!(source_cache, full_rebuild);
    assert_eq!(
        source_cache.content_hash().unwrap(),
        full_rebuild.content_hash().unwrap()
    );

    let changed_meshes = source_cache_before
        .chunks
        .iter()
        .zip(&source_cache.chunks)
        .filter(|(source, target)| source != target)
        .count();
    let changed_colliders = source_cache_before
        .chunks
        .iter()
        .zip(&source_cache.chunks)
        .filter(|(source, target)| source.collider != target.collider)
        .count();
    assert_eq!(changed_meshes, 3);
    assert_eq!(changed_colliders, 1);
    assert_eq!(TERRAIN_CHUNK_COUNT - changed_meshes, 61);
    for (index, (source, target)) in source_cache_before
        .chunks
        .iter()
        .zip(&source_cache.chunks)
        .enumerate()
    {
        assert_eq!(source.id, target.id);
        if !commit
            .mesh_chunks()
            .iter()
            .any(|dirty| dirty.coordinate == target.coordinate)
        {
            assert_eq!(
                allocation_addresses_before[index],
                chunk_allocation_addresses(target),
                "untouched chunk {:?} must retain all internal allocations",
                target.coordinate
            );
        }
    }
}

#[must_use]
fn chunk_allocation_addresses(chunk: &TerrainChunkMesh) -> [usize; 8] {
    [
        std::ptr::from_ref(chunk) as usize,
        chunk.positions_m.as_ptr() as usize,
        chunk.normals.as_ptr() as usize,
        chunk.uvs.as_ptr() as usize,
        chunk.material_weights.as_ptr() as usize,
        chunk.indices.as_ptr() as usize,
        chunk.collider.vertices_m.as_ptr() as usize,
        chunk.collider.triangles.as_ptr() as usize,
    ]
}

#[test]
fn sparse_delta_sampling_is_exact_on_grid_and_bilinear_between_samples() {
    let source_document = VisualValleyTerrainRecipe::default().build().unwrap();
    let mut target_document = source_document.migrate_to_latest().unwrap();
    target_document.height_deltas = vec![TerrainHeightDelta::new(31, 31, 8.0)];
    let source_plan = TerrainGenerationPlan::from_document(&source_document).unwrap();
    let target_plan = TerrainGenerationPlan::from_document(&target_document).unwrap();
    let x_m = -TERRAIN_HALF_EXTENT_M + 31.0 * TERRAIN_SAMPLE_SPACING_M;
    let z_m = -TERRAIN_HALF_EXTENT_M + 31.0 * TERRAIN_SAMPLE_SPACING_M;

    let exact_difference =
        target_plan.height_at(x_m, z_m).unwrap() - source_plan.height_at(x_m, z_m).unwrap();
    let halfway_difference = target_plan
        .height_at(x_m + TERRAIN_SAMPLE_SPACING_M * 0.5, z_m)
        .unwrap()
        - source_plan
            .height_at(x_m + TERRAIN_SAMPLE_SPACING_M * 0.5, z_m)
            .unwrap();
    assert!((exact_difference - 8.0).abs() <= 1.0e-5);
    assert!((halfway_difference - 4.0).abs() <= 1.0e-5);
}

#[test]
fn partial_update_rejects_stale_or_structural_requests_without_mutating_inputs() {
    let source_document = VisualValleyTerrainRecipe::default().build().unwrap();
    let source_plan = TerrainGenerationPlan::from_document(&source_document).unwrap();
    let source_cache = source_plan.generate().unwrap();
    let source_cache_before = source_cache.clone();
    let mut target_document = source_document.migrate_to_latest().unwrap();
    target_document.revision = source_document.revision.next().unwrap();
    target_document.height_deltas = vec![TerrainHeightDelta::new(31, 31, 5.0)];

    let stale_hash = source_plan.apply_partial_update(
        &source_cache,
        TerrainPartialUpdateRequest {
            base_revision: source_document.revision,
            base_document_hash: ContentHash::from_bytes([0x5a; 32]),
            target_document: target_document.clone(),
        },
    );
    assert!(matches!(
        stale_hash,
        Err(TerrainPartialUpdateError::StaleBaseDocumentHash { .. })
    ));
    assert_eq!(source_cache, source_cache_before);

    let stale_revision = source_plan.apply_partial_update(
        &source_cache,
        TerrainPartialUpdateRequest {
            base_revision: Revision::new(source_document.revision.get() + 1),
            base_document_hash: source_plan.document_hash(),
            target_document: target_document.clone(),
        },
    );
    assert!(matches!(
        stale_revision,
        Err(TerrainPartialUpdateError::StaleBaseRevision { .. })
    ));
    assert_eq!(source_cache, source_cache_before);

    let mut structural_target = target_document;
    structural_target.seed = structural_target.seed.wrapping_add(1);
    let structural = source_plan.apply_partial_update(
        &source_cache,
        TerrainPartialUpdateRequest {
            base_revision: source_document.revision,
            base_document_hash: source_plan.document_hash(),
            target_document: structural_target,
        },
    );
    assert!(matches!(
        structural,
        Err(TerrainPartialUpdateError::FullRebuildRequired)
    ));
    assert_eq!(source_cache, source_cache_before);
}

#[test]
fn partial_update_rejects_non_next_revision_cache_mismatch_and_height_overflow() {
    let source_document = VisualValleyTerrainRecipe::default().build().unwrap();
    let source_plan = TerrainGenerationPlan::from_document(&source_document).unwrap();
    let source_cache = source_plan.generate().unwrap();
    let mut target_document = source_document.migrate_to_latest().unwrap();
    target_document.revision = source_document.revision.next().unwrap();
    target_document.height_deltas = vec![TerrainHeightDelta::new(31, 31, 5.0)];

    let mut wrong_revision = target_document.clone();
    wrong_revision.revision = Revision::new(target_document.revision.get() + 1);
    assert!(matches!(
        source_plan.apply_partial_update(
            &source_cache,
            TerrainPartialUpdateRequest {
                base_revision: source_document.revision,
                base_document_hash: source_plan.document_hash(),
                target_document: wrong_revision,
            }
        ),
        Err(TerrainPartialUpdateError::UnexpectedTargetRevision { .. })
    ));

    let mut stale_cache = source_cache.clone();
    stale_cache.source_document_hash = ContentHash::from_bytes([0x33; 32]);
    assert!(matches!(
        source_plan.apply_partial_update(
            &stale_cache,
            TerrainPartialUpdateRequest {
                base_revision: source_document.revision,
                base_document_hash: source_plan.document_hash(),
                target_document: target_document.clone(),
            }
        ),
        Err(TerrainPartialUpdateError::SourceCacheHashMismatch { .. })
    ));

    let mut overflow = target_document;
    overflow.height_sampling.base_elevation_m = 0.0;
    overflow.height_sampling.amplitude_m = f32::MAX;
    overflow.height_deltas = vec![TerrainHeightDelta::new(31, 31, f32::MAX)];
    assert!(matches!(
        TerrainGenerationPlan::from_document(&overflow),
        Err(TerrainGenerationError::HeightOutputOutOfRange)
    ));
}

#[test]
fn prepared_update_revalidates_live_cache_atomically_before_commit() {
    let source_document = VisualValleyTerrainRecipe::default().build().unwrap();
    let source_plan = TerrainGenerationPlan::from_document(&source_document).unwrap();
    let source_cache = source_plan.generate().unwrap();
    let mut target_document = source_document.migrate_to_latest().unwrap();
    target_document.revision = source_document.revision.next().unwrap();
    target_document.height_deltas = vec![TerrainHeightDelta::new(31, 31, 5.0)];

    let prepare = || {
        source_plan
            .apply_partial_update(
                &source_cache,
                TerrainPartialUpdateRequest {
                    base_revision: source_document.revision,
                    base_document_hash: source_plan.document_hash(),
                    target_document: target_document.clone(),
                },
            )
            .unwrap()
    };

    let mut stale_cache = source_cache.clone();
    stale_cache.source_document_hash = ContentHash::from_bytes([0x44; 32]);
    let stale_cache_before = stale_cache.clone();
    assert!(matches!(
        prepare().apply_to_cache(&mut stale_cache),
        Err(TerrainPartialUpdateError::SourceCacheHashMismatch { .. })
    ));
    assert_eq!(stale_cache, stale_cache_before);

    let mut reordered_cache = source_cache.clone();
    reordered_cache.chunks.swap(0, 1);
    let reordered_cache_before = reordered_cache.clone();
    assert!(matches!(
        prepare().apply_to_cache(&mut reordered_cache),
        Err(TerrainPartialUpdateError::InvalidSourceCacheLayout(_))
    ));
    assert_eq!(reordered_cache, reordered_cache_before);

    let mut wrong_identity_cache = source_cache.clone();
    wrong_identity_cache.chunks[0].id = wrong_identity_cache.chunks[1].id;
    let wrong_identity_cache_before = wrong_identity_cache.clone();
    assert!(matches!(
        prepare().apply_to_cache(&mut wrong_identity_cache),
        Err(TerrainPartialUpdateError::InvalidSourceCacheLayout(_))
    ));
    assert_eq!(wrong_identity_cache, wrong_identity_cache_before);
}
