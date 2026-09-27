use super::*;
use crate::{TerrainGenerationPlan, embedded_visual_valley_terrain_document};
use std::collections::BTreeMap;

const STRIDES: [u8; 6] = [1, 2, 4, 8, 16, 32];

#[test]
#[allow(
    clippy::many_single_char_names,
    reason = "independent barycentric oracle uses conventional point and coordinate names"
)]
fn transition_refinement_preserves_both_surfaces_between_vertices() {
    let generated =
        TerrainGenerationPlan::from_document(&embedded_visual_valley_terrain_document().unwrap())
            .unwrap()
            .generate()
            .unwrap();
    let mut chunk = generated.chunks[0].clone();
    for (i, point) in chunk.positions_m.iter_mut().enumerate() {
        point[1] = f32::from(u16::try_from((i * 13 + i / 33 * 7) % 19).unwrap());
    }
    let before = chunk.clone();
    let fine = TerrainLodTopology::new(1).unwrap();
    let coarse = TerrainLodTopology::new(4).unwrap();
    let (start, end) = fine.project_transition(&chunk, &coarse).unwrap();
    assert_eq!(start.indices, end.indices);
    assert!(
        start.indices.len() > chunk.indices.len(),
        "boundary intersections require refinement"
    );
    let originals = [
        fine.project(&chunk).unwrap(),
        coarse.project(&chunk).unwrap(),
    ];
    let mut area = 0.0;
    for (mesh, original) in [&start, &end].into_iter().zip(&originals) {
        for indices in mesh.indices.as_chunks::<3>().0 {
            let tri = indices.map(|i| mesh.positions_m[i as usize]);
            let x = (f64::from(tri[0][0]) + f64::from(tri[1][0]) + f64::from(tri[2][0])) / 3.0;
            let z = (f64::from(tri[0][2]) + f64::from(tri[1][2]) + f64::from(tri[2][2])) / 3.0;
            let y = (f64::from(tri[0][1]) + f64::from(tri[1][1]) + f64::from(tri[2][1])) / 3.0;
            let expected = original
                .indices
                .as_chunks::<3>()
                .0
                .iter()
                .find_map(|indices| {
                    let [a, b, c] = [indices[0], indices[1], indices[2]]
                        .map(|i| original.positions_m[i as usize]);
                    let pa = [f64::from(a[0]), f64::from(a[2])];
                    let pb = [f64::from(b[0]), f64::from(b[2])];
                    let pc = [f64::from(c[0]), f64::from(c[2])];
                    let d = cross(subtract(pb, pa), subtract(pc, pa));
                    let wb = cross(subtract([x, z], pa), subtract(pc, pa)) / d;
                    let wc = cross(subtract(pb, pa), subtract([x, z], pa)) / d;
                    let wa = 1.0 - wb - wc;
                    (wa >= -1e-7 && wb >= -1e-7 && wc >= -1e-7).then_some(
                        wa * f64::from(a[1]) + wb * f64::from(b[1]) + wc * f64::from(c[1]),
                    )
                })
                .unwrap();
            assert!(
                (y - expected).abs() < 0.0002,
                "endpoint surface {y} != {expected}"
            );
        }
    }
    for indices in start.indices.as_chunks::<3>().0 {
        let triangle = indices.map(|i| start.positions_m[i as usize]);
        let [a, b, c] =
            [triangle[0], triangle[1], triangle[2]].map(|p| [f64::from(p[0]), f64::from(p[2])]);
        let signed = cross(subtract(b, a), subtract(c, a));
        assert!(signed < 0.0);
        area -= signed * 0.5;
    }
    assert!((area - 4096.0).abs() < 0.002, "area {area}");
    for (a, b) in start.positions_m.iter().zip(&end.positions_m) {
        assert_eq!(
            [a[0], a[2]].map(f32::to_bits),
            [b[0], b[2]].map(f32::to_bits)
        );
    }
    assert_eq!(chunk, before);
    assert_eq!(
        fine.project_transition(&chunk, &coarse).unwrap(),
        (start, end)
    );
}

#[test]
fn morph_targets_sample_parent_plane_and_preserve_topology_and_source() {
    let generated =
        TerrainGenerationPlan::from_document(&embedded_visual_valley_terrain_document().unwrap())
            .unwrap()
            .generate()
            .unwrap();
    let mut chunk = generated.chunks[0].clone();
    for (i, point) in chunk.positions_m.iter_mut().enumerate() {
        let x = f32::from(u16::try_from(i % 33).unwrap());
        let z = f32::from(u16::try_from(i / 33).unwrap());
        point[1] = x * 2.0 + z * 3.0;
    }
    // A fine-only bump must morph to the coarse plane, not to a nearest vertex.
    let bump = usize::from(grid_index(9, 9));
    chunk.positions_m[bump][1] += 10.0;
    let before = chunk.clone();
    let fine = TerrainLodTopology::new(1).unwrap();
    let target = fine
        .project_morph_target(&chunk, &TerrainLodTopology::new(4).unwrap())
        .unwrap();
    assert_eq!(target.positions_m[bump][1].to_bits(), 45.0_f32.to_bits());
    assert_eq!(target.indices, chunk.indices);
    assert_eq!(target.uvs, chunk.uvs);
    for (point, original) in target.positions_m.iter().zip(&chunk.positions_m) {
        assert_eq!(
            [point[0], point[2]].map(f32::to_bits),
            [original[0], original[2]].map(f32::to_bits)
        );
    }
    assert_eq!(chunk, before);
    assert_eq!(
        fine.project_morph_target(&chunk, &fine).unwrap(),
        fine.project(&chunk).unwrap()
    );
    chunk.positions_m[0][1] = f32::NAN;
    assert!(fine.project_morph_target(&chunk, &fine).is_err());
}

#[test]
fn morph_targets_keep_every_shared_boundary_sample_exact() {
    let generated =
        TerrainGenerationPlan::from_document(&embedded_visual_valley_terrain_document().unwrap())
            .unwrap()
            .generate()
            .unwrap();
    let chunk = &generated.chunks[0];
    for stride in STRIDES {
        let target = TerrainLodTopology::new(stride).unwrap();
        let projected = TerrainLodTopology::new(1)
            .unwrap()
            .project_morph_target(chunk, &target)
            .unwrap();
        for original in target.source_vertices {
            let index = usize::from(original);
            assert_eq!(
                projected.positions_m[index].map(f32::to_bits),
                chunk.positions_m[index].map(f32::to_bits)
            );
            assert_eq!(
                projected.normals[index].map(f32::to_bits),
                chunk.normals[index].map(f32::to_bits)
            );
            assert_eq!(
                projected.material_weights[index],
                chunk.material_weights[index]
            );
        }
    }
}

fn grid_point(index: u16) -> [i32; 2] {
    [i32::from(index % 33), i32::from(index / 33)]
}

#[test]
fn topology_covers_the_chunk_once_with_watertight_edges_and_positive_winding() {
    let mut last_counts = (usize::MAX, usize::MAX);
    for stride in STRIDES {
        let topology = TerrainLodTopology::new(stride).unwrap();
        assert_eq!(topology, TerrainLodTopology::new(stride).unwrap());
        let counts = (topology.source_vertices.len(), topology.indices.len() / 3);
        assert!(counts.0 < last_counts.0 && counts.1 < last_counts.1);
        last_counts = counts;
        let mut edges = BTreeMap::<(u32, u32), (usize, i32)>::new();
        let mut area_twice = 0;
        for triangle in topology.indices.as_chunks::<3>().0 {
            let points = [triangle[0], triangle[1], triangle[2]]
                .map(|index| grid_point(topology.source_vertices[usize::try_from(index).unwrap()]));
            let [a, b, c] = points;
            let area = (b[1] - a[1]) * (c[0] - a[0]) - (b[0] - a[0]) * (c[1] - a[1]);
            assert!(area > 0, "stride {stride}: {points:?}");
            area_twice += area;
            for (a, b) in [
                (triangle[0], triangle[1]),
                (triangle[1], triangle[2]),
                (triangle[2], triangle[0]),
            ] {
                let edge = edges.entry((a.min(b), a.max(b))).or_default();
                edge.0 += 1;
                edge.1 += if a < b { 1 } else { -1 };
            }
        }
        assert_eq!(area_twice, 2 * 32 * 32);
        let mut boundary_count = 0;
        for ((a, b), (count, balance)) in edges {
            if count == 2 {
                assert_eq!(balance, 0);
                continue;
            }
            assert_eq!(count, 1);
            let a = grid_point(topology.source_vertices[usize::try_from(a).unwrap()]);
            let b = grid_point(topology.source_vertices[usize::try_from(b).unwrap()]);
            assert!(
                (a[0] == b[0] && (a[0] == 0 || a[0] == 32))
                    || (a[1] == b[1] && (a[1] == 0 || a[1] == 32))
            );
            assert_eq!((a[0] - b[0]).abs() + (a[1] - b[1]).abs(), 1);
            boundary_count += 1;
        }
        assert_eq!(boundary_count, 128);
        eprintln!(
            "terrain_lod stride={stride} vertices={} triangles={}",
            counts.0, counts.1
        );
    }
}

#[test]
fn projection_preserves_exact_source_attributes_and_full_resolution_identity() {
    let source =
        TerrainGenerationPlan::from_document(&embedded_visual_valley_terrain_document().unwrap())
            .unwrap()
            .generate()
            .unwrap();
    let before = source.content_hash().unwrap();
    for stride in STRIDES {
        let topology = TerrainLodTopology::new(stride).unwrap();
        for chunk in &source.chunks {
            let projected = topology.project(chunk).unwrap();
            assert_eq!(projected.source_chunk_id, chunk.id);
            for (index, original) in topology.source_vertices.iter().enumerate() {
                let original = usize::from(*original);
                assert_eq!(
                    projected.positions_m[index].map(f32::to_bits),
                    chunk.positions_m[original].map(f32::to_bits)
                );
                assert_eq!(
                    projected.normals[index].map(f32::to_bits),
                    chunk.normals[original].map(f32::to_bits)
                );
                assert_eq!(
                    projected.uvs[index].map(f32::to_bits),
                    chunk.uvs[original].map(f32::to_bits)
                );
                assert_eq!(
                    projected.material_weights[index],
                    chunk.material_weights[original]
                );
            }
            if stride == 1 {
                assert_eq!(projected.positions_m, chunk.positions_m);
                assert_eq!(projected.normals, chunk.normals);
                assert_eq!(projected.uvs, chunk.uvs);
                assert_eq!(projected.material_weights, chunk.material_weights);
                assert_eq!(projected.indices, chunk.indices);
            }
        }
    }
    // Includes all original collider vertices and triangles, not just the render arrays.
    assert_eq!(source.content_hash().unwrap(), before);
}

type EdgeSample = ([f32; 3], [f32; 3], [f32; 2], TerrainMaterialWeights);

fn edge(
    topology: &TerrainLodTopology,
    mesh: &TerrainLodMesh,
    axis: usize,
    side: i32,
) -> Vec<EdgeSample> {
    topology
        .source_vertices
        .iter()
        .enumerate()
        .filter_map(|(index, source)| {
            (grid_point(*source)[axis] == side).then_some((
                mesh.positions_m[index],
                mesh.normals[index],
                mesh.uvs[index],
                mesh.material_weights[index],
            ))
        })
        .collect()
}

#[test]
fn all_mixed_lod_neighbours_share_exact_seam_samples_on_the_sculpted_world() {
    let sketch = crate::embedded_visual_valley_world_sketch().unwrap();
    let compiled = crate::compile_world_sketch(&sketch).unwrap();
    assert_ne!(compiled.carved_terrain, compiled.terrain);
    let source = TerrainGenerationPlan::from_document(&compiled.carved_terrain)
        .unwrap()
        .generate()
        .unwrap();
    let topologies = STRIDES.map(|stride| TerrainLodTopology::new(stride).unwrap());
    let meshes = source
        .chunks
        .iter()
        .map(|chunk| {
            topologies
                .iter()
                .map(|topology| topology.project(chunk).unwrap())
                .collect::<Vec<_>>()
        })
        .collect::<Vec<_>>();
    for z in 0..8 {
        for x in 0..8 {
            let current = z * 8 + x;
            for (axis, neighbour) in [
                (0, (x < 7).then_some(current + 1)),
                (1, (z < 7).then_some(current + 8)),
            ] {
                let Some(neighbour) = neighbour else {
                    continue;
                };
                for first in 0..STRIDES.len() {
                    for second in 0..STRIDES.len() {
                        let left = edge(&topologies[first], &meshes[current][first], axis, 32);
                        let right = edge(&topologies[second], &meshes[neighbour][second], axis, 0);
                        assert_eq!(left.len(), 33);
                        assert_eq!(
                            left, right,
                            "chunk {current} neighbour {neighbour}, strides {first}/{second}"
                        );
                    }
                }
            }
        }
    }
}

#[test]
fn invalid_stride_and_damaged_source_are_refused() {
    for stride in [0, 3, 6, 31, 33, 64, 255] {
        assert_eq!(
            TerrainLodTopology::new(stride),
            Err(TerrainLodError::UnsupportedStride(stride))
        );
    }
    let source =
        TerrainGenerationPlan::from_document(&embedded_visual_valley_terrain_document().unwrap())
            .unwrap()
            .generate()
            .unwrap()
            .chunks
            .remove(0);
    let topology = TerrainLodTopology::new(4).unwrap();
    let mut damaged = source.clone();
    damaged.positions_m.pop();
    assert!(topology.project(&damaged).is_err());
    let mut damaged = source.clone();
    damaged.positions_m[1][0] += 0.5;
    assert!(topology.project(&damaged).is_err());
    let mut damaged = source.clone();
    damaged.normals[30][1] = f32::NAN;
    assert!(topology.project(&damaged).is_err());
    let mut damaged = source.clone();
    damaged.indices.swap(1, 2);
    assert!(topology.project(&damaged).is_err());
    let mut damaged = source;
    damaged.material_weights[4].grass = -1.0;
    assert!(topology.project(&damaged).is_err());
}
