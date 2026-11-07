use super::*;
use crate::{cell_streaming::SampledCellSource, sampled_terrain::SampledTerrainPlan};
use forge_project::{
    HeightSamplingAlgorithm, TERRAIN_DOCUMENT_SCHEMA_VERSION_V4, TerrainExtent,
    sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
};
use std::sync::Arc;

mod procedural;

fn policy(dimensions: [u16; 2]) -> CellStreamingPolicy {
    CellStreamingPolicy {
        origin_xz_m: [-128.0; 2],
        cell_size_m: 32.0,
        dimensions,
        detail_radius: 0,
        far_radius: 2,
        max_resident_cells: 289,
        full_cell_bytes: 100_000,
        hlod_cell_bytes: 100_000,
        max_resident_bytes: 28_900_000,
        max_staging_bytes: 57_800_000,
    }
}

fn terrain() -> Arc<SampledTerrainPlan> {
    let mut document = crate::embedded_visual_valley_terrain_document().unwrap();
    let bytes: Vec<_> = (0..256 * 256)
        .flat_map(|i| {
            let x = f64::from(i % 256);
            let z = f64::from(i / 256);
            #[allow(clippy::cast_possible_truncation)]
            ((x * 0.04).sin().mul_add(3.0, (z * 0.03).cos() * 2.0) as f32).to_le_bytes()
        })
        .collect();
    document.schema_version = TERRAIN_DOCUMENT_SCHEMA_VERSION_V4;
    document.extent_m = TerrainExtent::new(256.0, 256.0);
    document.height_sampling.algorithm = HeightSamplingAlgorithm::SampledRasterV1;
    document.height_sampling.sample_spacing_m = 1.0;
    document.height_sampling.base_elevation_m = 0.0;
    document.height_sampling.amplitude_m = 0.0;
    document.height_sampling.base_frequency_cycles_per_m = 1.0;
    document.height_sampling.octaves = 1;
    document.height_sampling.lacunarity = 2.0;
    document.height_sampling.persistence = 0.5;
    document.height_deltas = vec![forge_project::TerrainHeightDelta {
        sample_x: 64,
        sample_z: 64,
        delta_m: 7.0,
    }];
    document.sampled_source = Some(SampledTerrainSource {
        schema: SAMPLED_TERRAIN_SCHEMA.into(),
        schema_version: 1,
        width: 256,
        height: 256,
        origin_xz_m: [-128.0; 2],
        sample_spacing_m: 1.0,
        outside_height_m: 0.0,
        encoding: HeightEncoding::Float32Le,
        samples_sha256: forge_core::bytes_sha256(&bytes),
    });
    Arc::new(SampledTerrainPlan::from_document(&document, &bytes).unwrap())
}

#[test]
fn quality_interiors_keep_the_same_refined_edges_and_bounded_tile_payload() {
    let terrain = terrain();
    let tile = TerrainHlodTile {
        key: TerrainHlodKey {
            x: 0,
            z: 0,
            level: 1,
        },
        finer_edges: [true; 4],
    };
    let mut counts = Vec::new();
    let mut boundary = None;
    for stride in [2, 4, 8, 16, 32] {
        let mesh = build_mesh_with_stride(&terrain, tile, stride).unwrap();
        assert!(mesh.bytes() <= 32_768);
        let edge: BTreeSet<_> = mesh
            .positions_m
            .iter()
            .zip(&mesh.normals)
            .zip(&mesh.material_weights)
            .filter(|((p, _), _)| {
                [p[0].to_bits(), p[2].to_bits()].iter().any(|value| {
                    *value == (-128.0_f32).to_bits() || *value == (-64.0_f32).to_bits()
                })
            })
            .map(|((p, n), w)| {
                (
                    p.map(f32::to_bits),
                    n.map(f32::to_bits),
                    w.map(f32::to_bits),
                )
            })
            .collect();
        assert!(!edge.is_empty());
        if let Some(expected) = &boundary {
            assert_eq!(&edge, expected);
        } else {
            boundary = Some(edge);
        }
        counts.push(mesh.indices.len());
    }
    assert!(counts.windows(2).all(|pair| pair[0] > pair[1]));
    for invalid in [0, 1, 3, 64] {
        assert!(build_mesh_with_stride(&terrain, tile, invalid).is_err());
    }
}

#[test]
fn hierarchy_covers_every_cell_once_and_balances_rectangles_and_simulation_islands() {
    for dimensions in [[32, 32], [31, 23], [128, 128], [1, 17], [1, 1]] {
        let policy = policy(dimensions);
        let fine = BTreeMap::from([
            (CellKey { x: 0, z: 0 }, CellDetail::Full),
            (
                CellKey {
                    x: dimensions[0] - 1,
                    z: dimensions[1] - 1,
                },
                CellDetail::Full,
            ),
        ]);
        let layout = TerrainHlodLayout::new(&policy, &fine).unwrap();
        let mut leaves: BTreeSet<_> = layout.tiles.keys().copied().collect();
        leaves.extend(fine.keys().map(|key| TerrainHlodKey {
            x: key.x,
            z: key.z,
            level: 0,
        }));
        let owners = owners(dimensions, &leaves).unwrap();
        assert_eq!(
            layout.covered_cells() + fine.len(),
            usize::from(dimensions[0]) * usize::from(dimensions[1])
        );
        assert!(layout.tiles.len() <= MAX_TERRAIN_HLOD_TILES);
        for &key in &leaves {
            visit_neighbours(key, dimensions, &owners, |edge, other| {
                assert!(key.level.abs_diff(other.level) <= 1);
                if let Some(tile) = layout.tiles.get(&key) {
                    assert_eq!(tile.finer_edges[edge], other.level < key.level);
                }
            });
        }
        assert_eq!(layout, TerrainHlodLayout::new(&policy, &fine).unwrap());
    }
}

#[test]
fn distant_parent_replaces_four_children_without_changing_canonical_samples() {
    let terrain = terrain();
    let policy = policy([8, 8]);
    let source = SampledCellSource::new(terrain.clone(), policy.clone(), 8).unwrap();
    let parent = build_mesh(
        &terrain,
        TerrainHlodTile {
            key: TerrainHlodKey {
                x: 0,
                z: 0,
                level: 1,
            },
            finer_edges: [false; 4],
        },
    )
    .unwrap();
    let children: Vec<_> = [
        CellKey { x: 0, z: 0 },
        CellKey { x: 1, z: 0 },
        CellKey { x: 0, z: 1 },
        CellKey { x: 1, z: 1 },
    ]
    .into_iter()
    .map(|key| source.build(key, CellDetail::Hlod).unwrap())
    .collect();
    assert_eq!(
        children
            .iter()
            .map(|mesh| mesh.indices.len())
            .sum::<usize>(),
        parent.indices.len() * 4
    );
    assert!(
        parent.bytes()
            < children
                .iter()
                .map(|mesh| mesh.acknowledgement.bytes)
                .sum::<u64>()
                / 2
    );
    for (position, normal) in parent.positions_m.iter().zip(&parent.normals) {
        #[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
        let sample = [(position[0] + 128.0) as u16, (position[2] + 128.0) as u16];
        let canonical = terrain.sample_vertex(sample).unwrap();
        assert_eq!(
            position.map(f32::to_bits),
            canonical.position_m.map(f32::to_bits)
        );
        assert_eq!(normal.map(f32::to_bits), canonical.normal.map(f32::to_bits));
    }
    assert_eq!(parent.source, terrain.document_hash());
}

type Point = [u32; 3];
type Edge = (Point, Point);

fn boundary_edges(positions: &[[f32; 3]], indices: &[u32]) -> BTreeMap<Edge, usize> {
    let mut edges = BTreeMap::new();
    for triangle in indices.as_chunks::<3>().0 {
        for i in 0..3 {
            let a = positions[triangle[i] as usize].map(f32::to_bits);
            let b = positions[triangle[(i + 1) % 3] as usize].map(f32::to_bits);
            *edges
                .entry(if a < b { (a, b) } else { (b, a) })
                .or_default() += 1;
        }
    }
    edges.retain(|_, count| *count == 1);
    edges
}

#[test]
fn all_sixteen_stitch_masks_preserve_area_winding_and_exact_edge_segments() {
    let terrain = terrain();
    for mask in 0_u8..16 {
        let tile = TerrainHlodTile {
            key: TerrainHlodKey {
                x: 0,
                z: 0,
                level: 1,
            },
            finer_edges: std::array::from_fn(|edge| mask & (1 << edge) != 0),
        };
        let mesh = build_mesh(&terrain, tile).unwrap();
        let mut twice_area = 0.0;
        for triangle in mesh.indices.as_chunks::<3>().0 {
            let [a, b, c] = triangle.map(|i| mesh.positions_m[i as usize]);
            let cross = (b[0] - a[0]) * (c[2] - a[2]) - (b[2] - a[2]) * (c[0] - a[0]);
            assert!(cross < 0.0, "zero/negative-Y triangle for mask {mask}");
            twice_area -= cross;
        }
        assert_eq!(twice_area.to_bits(), (2.0_f32 * 64.0 * 64.0).to_bits());
        assert_eq!(
            boundary_edges(&mesh.positions_m, &mesh.indices).len(),
            128 + mask.count_ones() as usize * 32
        );
        assert!(mesh.positions_m.len() <= 417 && mesh.indices.len() <= 2304);
    }
}

#[test]
fn mixed_hierarchy_and_fine_cells_share_every_internal_edge_without_t_junctions() {
    let terrain = terrain();
    let policy = policy([8, 8]);
    let fine = BTreeMap::from([
        (CellKey { x: 0, z: 0 }, CellDetail::Full),
        (CellKey { x: 7, z: 7 }, CellDetail::Hlod),
    ]);
    let layout = TerrainHlodLayout::new(&policy, &fine).unwrap();
    assert!(layout.tiles.keys().any(|key| key.level >= 1));
    let source = SampledCellSource::new(terrain.clone(), policy, 8).unwrap();
    let mut edges = BTreeMap::<Edge, usize>::new();
    let mut vertices = BTreeMap::new();
    let mut collect = |positions: Vec<[f32; 3]>,
                       normals: Vec<[f32; 3]>,
                       weights: Vec<[f32; 4]>,
                       indices: Vec<u32>| {
        for ((position, normal), weight) in positions.iter().zip(&normals).zip(&weights) {
            if let Some(previous) = vertices.insert(position.map(f32::to_bits), (*normal, *weight))
            {
                assert_eq!(previous, (*normal, *weight));
            }
        }
        for (edge, count) in boundary_edges(&positions, &indices) {
            *edges.entry(edge).or_default() += count;
        }
    };
    for tile in layout.tiles.values() {
        let mesh = build_mesh(&terrain, *tile).unwrap();
        collect(
            mesh.positions_m,
            mesh.normals,
            mesh.material_weights,
            mesh.indices,
        );
    }
    for (key, detail) in fine {
        let mesh = source.build(key, detail).unwrap();
        collect(
            mesh.positions_m,
            mesh.normals,
            mesh.material_weights,
            mesh.indices,
        );
    }
    for ((a, b), count) in edges {
        let [a, b] = [a, b].map(|point| point.map(f32::from_bits));
        let outer = [0, 2].iter().any(|&axis| {
            a[axis].to_bits() == b[axis].to_bits() && a[axis].abs().to_bits() == 128.0_f32.to_bits()
        });
        assert_eq!(
            count,
            if outer { 1 } else { 2 },
            "unmatched edge {a:?} -> {b:?}"
        );
    }
}

#[test]
fn invalid_coverage_keys_and_excessive_fragmentation_are_refused() {
    let policy = policy([256, 256]);
    assert!(
        TerrainHlodLayout::new(
            &policy,
            &BTreeMap::from([(CellKey { x: 256, z: 0 }, CellDetail::Full)])
        )
        .is_err()
    );
    let fragmented: BTreeMap<_, _> = (0..17)
        .flat_map(|z| {
            (0..17).map(move |x| {
                (
                    CellKey {
                        x: x * 15,
                        z: z * 15,
                    },
                    CellDetail::Full,
                )
            })
        })
        .collect();
    assert!(
        TerrainHlodLayout::new(&policy, &fragmented)
            .unwrap_err()
            .contains("budget")
    );
    let terrain = terrain();
    for tile in [
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
                level: 255,
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
        assert!(build_mesh(&terrain, tile).is_err());
    }
}
