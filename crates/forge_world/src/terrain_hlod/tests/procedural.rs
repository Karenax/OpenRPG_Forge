use super::*;
use crate::{
    GeneratedTerrain, TERRAIN_CHUNK_INTERVALS, TERRAIN_CHUNK_VERTICES_PER_AXIS,
    TERRAIN_CHUNKS_PER_AXIS, TerrainGenerationPlan, TerrainMaterialWeights,
    VisualValleyTerrainRecipe,
    cell_streaming::{ProceduralCellMesh, ProceduralCellSource},
};
use forge_project::{
    TERRAIN_DOCUMENT_SCHEMA_VERSION_V3, TerrainHeightDelta, TerrainMaterialOverride,
    TerrainMaterialWeight,
};

fn fixture() -> (ProceduralCellSource, CellStreamingPolicy) {
    let mut document = VisualValleyTerrainRecipe::default().build().unwrap();
    document.schema_version = TERRAIN_DOCUMENT_SCHEMA_VERSION_V3;
    document.height_deltas = vec![TerrainHeightDelta::new(64, 48, 5.0)];
    document.material_overrides = vec![TerrainMaterialOverride {
        sample_x: 64,
        sample_z: 48,
        weights: vec![TerrainMaterialWeight {
            layer_id: document.material_layers[1].id.clone(),
            weight: u16::MAX,
        }],
    }];
    let terrain = Arc::new(TerrainGenerationPlan::from_document(&document).unwrap());
    let mut policy = super::policy([8, 8]);
    policy.origin_xz_m = [-256.0; 2];
    policy.cell_size_m = 64.0;
    let source = ProceduralCellSource::new(terrain, policy.clone(), 8).unwrap();
    (source, policy)
}

fn assert_float_arrays<const N: usize>(left: &[[f32; N]], right: &[[f32; N]]) {
    assert_eq!(left.len(), right.len());
    assert!(
        left.iter()
            .zip(right)
            .all(|(a, b)| a.map(f32::to_bits) == b.map(f32::to_bits))
    );
}

fn assert_weights(left: &[TerrainMaterialWeights], right: &[TerrainMaterialWeights]) {
    assert_eq!(left.len(), right.len());
    assert!(
        left.iter()
            .zip(right)
            .all(|(a, b)| { a.as_array().map(f32::to_bits) == b.as_array().map(f32::to_bits) })
    );
}

fn compare_cell_and_tile(cell: &ProceduralCellMesh, tile: &ProceduralTerrainHlodMesh) {
    assert_float_arrays(&cell.positions_m, &tile.positions_m);
    assert_float_arrays(&cell.normals, &tile.normals);
    assert_float_arrays(&cell.uvs, &tile.uvs);
    assert_weights(&cell.material_weights, &tile.material_weights);
    assert_eq!(cell.indices, tile.indices);
    assert_eq!(cell.acknowledgement.source, tile.source);
}

#[test]
fn level_zero_reuses_exact_cell_topology_and_samples() {
    let (source, _) = fixture();
    for key in [
        CellKey { x: 0, z: 0 },
        CellKey { x: 1, z: 1 },
        CellKey { x: 7, z: 7 },
    ] {
        let cell = source.build(key, CellDetail::Hlod).unwrap();
        let tile = TerrainHlodTile {
            key: TerrainHlodKey {
                x: key.x,
                z: key.z,
                level: 0,
            },
            finer_edges: [false; 4],
        };
        let mesh = build_procedural_mesh(&source, tile).unwrap();
        assert_eq!(mesh.tile, tile);
        assert_eq!(mesh.sample_stride, 8);
        compare_cell_and_tile(&cell, &mesh);
    }
}

#[allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    reason = "the bounded integer grid coordinates are checked before conversion"
)]
fn assert_matches_canonical_chunks(mesh: &ProceduralTerrainHlodMesh, full: &GeneratedTerrain) {
    for (index, position) in mesh.positions_m.iter().enumerate() {
        // Positions are even integer metres within [-256, 256], so these grid indices are exact.
        let grid = [position[0].midpoint(256.0), position[2].midpoint(256.0)];
        assert!(grid.iter().all(|value| (0.0..=256.0).contains(value)));
        let [x, z] = grid.map(|value| value as u16);
        assert_eq!(
            grid.map(f32::to_bits),
            [f32::from(x), f32::from(z)].map(f32::to_bits)
        );
        let chunk_x = x.min(255) / TERRAIN_CHUNK_INTERVALS;
        let chunk_z = z.min(255) / TERRAIN_CHUNK_INTERVALS;
        let chunk = &full.chunks[usize::from(chunk_z * TERRAIN_CHUNKS_PER_AXIS + chunk_x)];
        let local_x = x - chunk_x * TERRAIN_CHUNK_INTERVALS;
        let local_z = z - chunk_z * TERRAIN_CHUNK_INTERVALS;
        let i = usize::from(local_z) * usize::from(TERRAIN_CHUNK_VERTICES_PER_AXIS)
            + usize::from(local_x);
        assert_eq!(
            position.map(f32::to_bits),
            chunk.positions_m[i].map(f32::to_bits)
        );
        assert_eq!(
            mesh.normals[index].map(f32::to_bits),
            chunk.normals[i].map(f32::to_bits)
        );
        assert_eq!(
            mesh.uvs[index].map(f32::to_bits),
            chunk.uvs[i].map(f32::to_bits)
        );
        assert_eq!(
            mesh.material_weights[index].as_array().map(f32::to_bits),
            chunk.material_weights[i].as_array().map(f32::to_bits)
        );
    }
}

#[test]
fn aggregated_tiles_sample_canonical_chunks_including_authored_relief_and_paint() {
    let (source, _) = fixture();
    let full = source.terrain().generate().unwrap();
    for level in [1, 2, 3] {
        let tile = TerrainHlodTile {
            key: TerrainHlodKey { x: 0, z: 0, level },
            finer_edges: [true; 4],
        };
        let mesh = build_procedural_mesh(&source, tile).unwrap();
        assert_eq!(mesh.source, source.residency_source());
        assert_matches_canonical_chunks(&mesh, &full);
    }
    let tile = TerrainHlodTile {
        key: TerrainHlodKey {
            x: 0,
            z: 0,
            level: 1,
        },
        finer_edges: [true; 4],
    };
    let mesh = build_procedural_mesh(&source, tile).unwrap();
    let index = mesh
        .positions_m
        .iter()
        .position(|p| [p[0], p[2]].map(f32::to_bits) == [-128.0_f32, -160.0].map(f32::to_bits))
        .unwrap();
    assert_eq!(
        mesh.material_weights[index].as_array().map(f32::to_bits),
        [0.0_f32, 1.0, 0.0].map(f32::to_bits)
    );
}

#[test]
fn all_stitch_masks_share_the_raster_topology_and_bound_every_payload() {
    let (source, _) = fixture();
    let raster = super::terrain();
    for mask in 0_u8..16 {
        let tile = TerrainHlodTile {
            key: TerrainHlodKey {
                x: 0,
                z: 0,
                level: 1,
            },
            finer_edges: std::array::from_fn(|edge| mask & (1 << edge) != 0),
        };
        for stride in [2, 4, 8, 16, 32] {
            let mesh = build_procedural_mesh_with_stride(&source, tile, stride).unwrap();
            let reference = build_mesh_with_stride(&raster, tile, stride).unwrap();
            assert_eq!(mesh.indices, reference.indices);
            assert_eq!(mesh.sample_stride, stride);
            assert!(mesh.positions_m.len() <= 541 && mesh.indices.len() <= 2472);
            let mut twice_area = 0.0;
            for triangle in mesh.indices.as_chunks::<3>().0 {
                let [a, b, c] = triangle.map(|i| mesh.positions_m[i as usize]);
                let cross = (b[0] - a[0]) * (c[2] - a[2]) - (b[2] - a[2]) * (c[0] - a[0]);
                assert!(
                    cross < 0.0,
                    "invalid winding for mask {mask}, stride {stride}"
                );
                twice_area -= cross;
            }
            assert_eq!(twice_area.to_bits(), (2.0_f32 * 128.0 * 128.0).to_bits());
            assert_eq!(
                boundary_edges(&mesh.positions_m, &mesh.indices).len(),
                128 + mask.count_ones() as usize * 32
            );
            let expected = mesh.positions_m.capacity() * size_of::<[f32; 3]>()
                + mesh.normals.capacity() * size_of::<[f32; 3]>()
                + mesh.uvs.capacity() * size_of::<[f32; 2]>()
                + mesh.material_weights.capacity() * size_of::<TerrainMaterialWeights>()
                + mesh.indices.capacity() * size_of::<u32>();
            assert_eq!(mesh.bytes(), u64::try_from(expected).unwrap());
            if mask == 15 && stride == 2 {
                assert_eq!(mesh.positions_m.len(), 541);
                assert_eq!(mesh.indices.len(), 2472);
                assert!(mesh.bytes() >= 33_692);
            }
        }
    }
}

#[test]
fn mixed_cells_and_hierarchy_share_every_edge_and_all_vertex_attributes() {
    let (source, policy) = fixture();
    let fine = BTreeMap::from([
        (CellKey { x: 0, z: 0 }, CellDetail::Full),
        (CellKey { x: 7, z: 7 }, CellDetail::Hlod),
    ]);
    let layout = TerrainHlodLayout::new(&policy, &fine).unwrap();
    assert_eq!(layout.covered_cells() + fine.len(), 64);
    assert!(layout.tiles().keys().any(|key| key.level >= 1));
    let mut edges = BTreeMap::<Edge, usize>::new();
    let mut vertices = BTreeMap::new();
    let mut collect = |positions: &[[f32; 3]],
                       normals: &[[f32; 3]],
                       uvs: &[[f32; 2]],
                       weights: &[TerrainMaterialWeights],
                       indices: &[u32]| {
        for (((position, normal), uv), weight) in
            positions.iter().zip(normals).zip(uvs).zip(weights)
        {
            let attributes = (
                normal.map(f32::to_bits),
                uv.map(f32::to_bits),
                weight.as_array().map(f32::to_bits),
            );
            if let Some(previous) = vertices.insert(position.map(f32::to_bits), attributes) {
                assert_eq!(previous, attributes);
            }
        }
        for (edge, count) in boundary_edges(positions, indices) {
            *edges.entry(edge).or_default() += count;
        }
    };
    for tile in layout.tiles().values() {
        let mesh = build_procedural_mesh(&source, *tile).unwrap();
        collect(
            &mesh.positions_m,
            &mesh.normals,
            &mesh.uvs,
            &mesh.material_weights,
            &mesh.indices,
        );
    }
    for (key, detail) in fine {
        let mesh = source.build(key, detail).unwrap();
        collect(
            &mesh.positions_m,
            &mesh.normals,
            &mesh.uvs,
            &mesh.material_weights,
            &mesh.indices,
        );
    }
    for ((a, b), count) in edges {
        let [a, b] = [a, b].map(|point| point.map(f32::from_bits));
        let outer = [0, 2].iter().any(|&axis| {
            a[axis].to_bits() == b[axis].to_bits() && a[axis].abs().to_bits() == 256.0_f32.to_bits()
        });
        assert_eq!(
            count,
            if outer { 1 } else { 2 },
            "unmatched edge {a:?} -> {b:?}"
        );
    }
}

#[test]
fn invalid_tile_coordinates_masks_and_strides_are_refused() {
    let (source, _) = fixture();
    for tile in [
        TerrainHlodTile {
            key: TerrainHlodKey {
                x: 8,
                z: 0,
                level: 0,
            },
            finer_edges: [false; 4],
        },
        TerrainHlodTile {
            key: TerrainHlodKey {
                x: 1,
                z: 0,
                level: 1,
            },
            finer_edges: [false; 4],
        },
        TerrainHlodTile {
            key: TerrainHlodKey {
                x: 0,
                z: 0,
                level: 4,
            },
            finer_edges: [false; 4],
        },
        TerrainHlodTile {
            key: TerrainHlodKey {
                x: 0,
                z: 0,
                level: u8::MAX,
            },
            finer_edges: [false; 4],
        },
        TerrainHlodTile {
            key: TerrainHlodKey {
                x: 0,
                z: 0,
                level: 0,
            },
            finer_edges: [true; 4],
        },
    ] {
        assert!(build_procedural_mesh(&source, tile).is_err());
    }
    let tile = TerrainHlodTile {
        key: TerrainHlodKey {
            x: 0,
            z: 0,
            level: 1,
        },
        finer_edges: [false; 4],
    };
    for stride in [0, 1, 3, 33, u8::MAX] {
        assert!(build_procedural_mesh_with_stride(&source, tile, stride).is_err());
    }
}
