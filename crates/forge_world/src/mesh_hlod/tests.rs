use super::*;

fn grid(intervals: u16) -> (Vec<[f32; 3]>, Vec<u32>, Vec<f32>) {
    let mut positions = Vec::new();
    let mut uv = Vec::new();
    for z in 0..=intervals {
        for x in 0..=intervals {
            let [x, z] = [x, z].map(|value| f32::from(value) / f32::from(intervals));
            positions.push([x, 0.0, z]);
            uv.extend_from_slice(&[x, z]);
        }
    }
    let mut indices = Vec::new();
    let columns = u32::from(intervals) + 1;
    for z in 0..u32::from(intervals) {
        for x in 0..u32::from(intervals) {
            let a = z * columns + x;
            indices.extend_from_slice(&[
                a,
                a + columns,
                a + 1,
                a + 1,
                a + columns,
                a + columns + 1,
            ]);
        }
    }
    (positions, indices, uv)
}

fn assert_surface_mapping(
    positions: &[[f32; 3]],
    source: &[u32],
    output: &ReducedMesh,
    error: f64,
) {
    let mut edges = BTreeSet::new();
    let mut faces = BTreeSet::new();
    for &triangle in output.indices.as_chunks::<3>().0 {
        let first = (0..3).min_by_key(|&i| triangle[i]).unwrap();
        faces.insert([
            triangle[first],
            triangle[(first + 1) % 3],
            triangle[(first + 2) % 3],
        ]);
        for i in 0..3 {
            let (a, b) = (triangle[i], triangle[(i + 1) % 3]);
            edges.insert((a.min(b), a.max(b)));
        }
    }
    for &triangle in source.as_chunks::<3>().0 {
        let mapped = triangle.map(|vertex| {
            let target = output.source_to_output_vertex[vertex as usize]
                .expect("every used source vertex retains support");
            let point = positions[output.retained_vertices[target as usize] as usize];
            let delta = difference(point, positions[vertex as usize]);
            assert!(
                delta
                    .into_iter()
                    .map(|value| value * value)
                    .sum::<f64>()
                    .sqrt()
                    <= error
            );
            target
        });
        let unique: BTreeSet<_> = mapped.into_iter().collect();
        if unique.len() == 3 {
            let first = (0..3).min_by_key(|&i| mapped[i]).unwrap();
            assert!(faces.contains(&[
                mapped[first],
                mapped[(first + 1) % 3],
                mapped[(first + 2) % 3]
            ]));
        } else if unique.len() == 2 {
            let endpoints: Vec<_> = unique.into_iter().collect();
            assert!(edges.contains(&(endpoints[0], endpoints[1])));
        }
    }
}

#[test]
fn dense_mesh_reduces_with_bounded_geometry_and_attribute_error_without_source_changes() {
    let (positions, indices, uv) = grid(32);
    let original = (positions.clone(), indices.clone(), uv.clone());
    let domains = vec![0; positions.len()];
    let channels = [ErrorChannel {
        values: &uv,
        components: 2,
        maximum_delta: 0.2,
    }];
    let run = || {
        reduce(
            MeshSource {
                positions_m: &positions,
                indices: &indices,
                domains: &domains,
                channels: &channels,
            },
            0.12,
        )
        .unwrap()
    };
    let output = run();
    assert_surface_mapping(&positions, &indices, &output, 0.12);
    assert!(output.indices.len() < indices.len() / 2);
    assert!(output.retained_vertices.len() < positions.len() / 2);
    assert!(output.maximum_position_error_m <= 0.12);
    assert!(output.maximum_channel_deltas[0] <= f64::from(0.2_f32));
    assert_eq!(output, run());
    assert_eq!(original, (positions.clone(), indices.clone(), uv.clone()));
    for face in output.indices.as_chunks::<3>().0 {
        let source_face = face.map(|vertex| output.retained_vertices[vertex as usize]);
        assert!(normal(&positions, &source_face)[1] > 0.0);
    }
    println!(
        "mesh_hlod planar source_vertices={} source_triangles={} retained_vertices={} retained_triangles={} max_position_error_m={} max_uv_delta={} triangle_checks={}",
        positions.len(),
        indices.len() / 3,
        output.retained_vertices.len(),
        output.indices.len() / 3,
        output.maximum_position_error_m,
        output.maximum_channel_deltas[0],
        output.triangle_checks
    );
}

#[test]
fn exact_domains_and_zero_attribute_error_preserve_discontinuities() {
    let (positions, indices, uv) = grid(16);
    let unique: Vec<_> = (0..u32::try_from(positions.len()).unwrap()).collect();
    let domains = vec![0; positions.len()];
    for (domains, channels) in [
        (&unique, vec![]),
        (
            &domains,
            vec![ErrorChannel {
                values: &uv,
                components: 2,
                maximum_delta: 0.0,
            }],
        ),
    ] {
        let output = reduce(
            MeshSource {
                positions_m: &positions,
                indices: &indices,
                domains,
                channels: &channels,
            },
            1.0,
        )
        .unwrap();
        assert_eq!(output.source_triangles, output.indices.len() / 3);
        assert_eq!(output.retained_vertices.len(), positions.len());
        assert_eq!(output.maximum_position_error_m.to_bits(), 0_f64.to_bits());
    }
}

#[test]
fn small_disconnected_components_cannot_disappear_or_become_joined() {
    let positions = [
        [0., 0., 0.],
        [0.01, 0., 0.],
        [0., 0., 0.01],
        [0.01, 0., 0.01],
        [0.02, 0., 0.],
        [0.03, 0., 0.],
        [0.02, 0., 0.01],
        [0.03, 0., 0.01],
    ];
    let indices = [0, 2, 1, 1, 2, 3, 4, 6, 5, 5, 6, 7];
    let output = reduce(
        MeshSource {
            positions_m: &positions,
            indices: &indices,
            domains: &[0; 8],
            channels: &[],
        },
        1.0,
    )
    .unwrap();
    let mut components = BTreeSet::new();
    assert_surface_mapping(&positions, &indices, &output, 1.0);
    for face in output.indices.as_chunks::<3>().0 {
        let source_face = face.map(|vertex| output.retained_vertices[vertex as usize]);
        assert!(
            source_face
                .iter()
                .all(|&vertex| vertex / 4 == source_face[0] / 4)
        );
        components.insert(source_face[0] / 4);
        assert!(normal(&positions, &source_face)[1] > 0.0);
    }
    assert_eq!(components, BTreeSet::from([0, 1]));
}

#[test]
fn annulus_keeps_two_boundary_loops_under_an_aggressive_error_budget() {
    let positions = [
        [-2., 0., -2.],
        [2., 0., -2.],
        [2., 0., 2.],
        [-2., 0., 2.],
        [-1., 0., -1.],
        [1., 0., -1.],
        [1., 0., 1.],
        [-1., 0., 1.],
    ];
    let mut indices = Vec::new();
    for a in 0_u32..4 {
        let b = (a + 1) % 4;
        indices.extend_from_slice(&[a, a + 4, b, b, a + 4, b + 4]);
    }
    let output = reduce(
        MeshSource {
            positions_m: &positions,
            indices: &indices,
            domains: &[0; 8],
            channels: &[],
        },
        16.0,
    )
    .unwrap();
    let mut edges = BTreeMap::<(u32, u32), usize>::new();
    assert_surface_mapping(&positions, &indices, &output, 16.0);
    for face in output.indices.as_chunks::<3>().0 {
        for index in 0..3 {
            let (a, b) = (face[index], face[(index + 1) % 3]);
            *edges.entry((a.min(b), a.max(b))).or_default() += 1;
        }
    }
    assert_eq!(
        output.retained_vertices.len() + output.indices.len() / 3,
        edges.len()
    ); // Euler characteristic zero.
    let mut boundary = BTreeMap::<u32, Vec<u32>>::new();
    for ((a, b), count) in edges {
        assert!((1..=2).contains(&count));
        if count == 1 {
            boundary.entry(a).or_default().push(b);
            boundary.entry(b).or_default().push(a);
        }
    }
    assert!(boundary.values().all(|neighbours| neighbours.len() == 2));
    let mut loops = 0;
    let mut remaining: BTreeSet<_> = boundary.keys().copied().collect();
    while let Some(first) = remaining.pop_first() {
        loops += 1;
        let mut pending = vec![first];
        while let Some(vertex) = pending.pop() {
            for next in &boundary[&vertex] {
                if remaining.remove(next) {
                    pending.push(*next);
                }
            }
        }
    }
    assert_eq!(loops, 2);
}

#[test]
fn reverse_order_dense_mesh_stays_inside_the_bounded_triangle_work() {
    let (positions, mut indices, _) = grid(64);
    let domains = vec![0; positions.len()];
    let mut faces = indices.as_chunks::<3>().0.to_vec();
    faces.reverse();
    indices = faces.into_iter().flatten().collect();
    let output = reduce(
        MeshSource {
            positions_m: &positions,
            indices: &indices,
            domains: &domains,
            channels: &[],
        },
        1.0,
    )
    .unwrap();
    assert!(output.triangle_checks <= MAX_TRIANGLE_CHECKS);
    assert!(output.indices.len() < indices.len());
}

#[test]
fn malformed_sources_channels_and_budgets_are_refused_before_work() {
    let positions = [[0., 0., 0.], [1., 0., 0.], [0., 0., 1.]];
    let indices = [0, 2, 1];
    for error in [0.0, f64::NAN, f64::INFINITY, 17.0] {
        assert_eq!(
            reduce(
                MeshSource {
                    positions_m: &positions,
                    indices: &indices,
                    domains: &[0; 3],
                    channels: &[]
                },
                error
            ),
            Err(ReductionError::InvalidErrorBound)
        );
    }
    for channels in [
        vec![ErrorChannel {
            values: &[],
            components: usize::MAX,
            maximum_delta: 0.0,
        }],
        vec![ErrorChannel {
            values: &[0.; 3],
            components: 1,
            maximum_delta: f32::NAN,
        }],
        vec![ErrorChannel {
            values: &[f32::NAN; 3],
            components: 1,
            maximum_delta: 1.0,
        }],
    ] {
        assert_eq!(
            reduce(
                MeshSource {
                    positions_m: &positions,
                    indices: &indices,
                    domains: &[0; 3],
                    channels: &channels
                },
                1.0
            ),
            Err(ReductionError::InvalidSource)
        );
    }
    for bad in [&[0, 2, 3][..], &[0, 0, 1][..], &[0, 2][..]] {
        assert_eq!(
            reduce(
                MeshSource {
                    positions_m: &positions,
                    indices: bad,
                    domains: &[0; 3],
                    channels: &[]
                },
                1.0
            ),
            Err(ReductionError::InvalidSource)
        );
    }
    let too_many = vec![[0.; 3]; MAX_VERTICES + 1];
    assert_eq!(
        reduce(
            MeshSource {
                positions_m: &too_many,
                indices: &indices,
                domains: &[],
                channels: &[]
            },
            1.0
        ),
        Err(ReductionError::BudgetExceeded)
    );
}

#[test]
fn surface_support_keeps_the_tip_of_a_thin_attached_component() {
    let positions = [
        [0., 0., 0.],
        [1., 0., 0.],
        [0., 0., 1.],
        [1., 0., 1.],
        [0.001, 0., 0.],
        [0., 0., -2.],
    ];
    let indices = [0, 2, 1, 1, 2, 3, 0, 4, 5];
    let output = reduce(
        MeshSource {
            positions_m: &positions,
            indices: &indices,
            domains: &[0; 6],
            channels: &[],
        },
        0.1,
    )
    .unwrap();
    assert!(
        output.retained_vertices.contains(&5),
        "a tiny base edge must not erase its two-metre attached tip"
    );
    assert_surface_mapping(&positions, &indices, &output, 0.1);
    assert_eq!(output.source_triangles, output.indices.len() / 3);
}

#[test]
fn surface_support_preserves_a_closed_minimal_tetrahedron() {
    let positions = [[0., 0., 0.], [0.01, 0., 0.], [0., 0.01, 0.], [0., 0., 0.01]];
    let indices = [0, 2, 1, 0, 1, 3, 0, 3, 2, 1, 2, 3];
    let output = reduce(
        MeshSource {
            positions_m: &positions,
            indices: &indices,
            domains: &[0; 4],
            channels: &[],
        },
        1.0,
    )
    .unwrap();
    assert_eq!(
        output.indices.len() / 3,
        4,
        "a tetrahedron must not become two coincident sheets"
    );
    assert_surface_mapping(&positions, &indices, &output, 1.0);
}

fn sphere(rings: u16, segments: u16) -> (Vec<[f32; 3]>, Vec<u32>) {
    let mut positions = vec![[0.0, 1.0, 0.0]];
    for ring in 1..rings {
        let latitude = std::f32::consts::PI * f32::from(ring) / f32::from(rings);
        for segment in 0..segments {
            let longitude = std::f32::consts::TAU * f32::from(segment) / f32::from(segments);
            positions.push([
                latitude.sin() * longitude.cos(),
                latitude.cos(),
                latitude.sin() * longitude.sin(),
            ]);
        }
    }
    let south = u32::try_from(positions.len()).unwrap();
    positions.push([0.0, -1.0, 0.0]);
    let mut indices = Vec::new();
    let count = u32::from(segments);
    for segment in 0..count {
        let next = (segment + 1) % count;
        indices.extend_from_slice(&[0, 1 + next, 1 + segment]);
        for ring in 0..u32::from(rings - 2) {
            let a = 1 + ring * count + segment;
            let adjacent = 1 + ring * count + next;
            indices.extend_from_slice(&[
                a,
                adjacent,
                a + count,
                adjacent,
                adjacent + count,
                a + count,
            ]);
        }
        let last = 1 + u32::from(rings - 2) * count;
        indices.extend_from_slice(&[last + segment, last + next, south]);
    }
    (positions, indices)
}

#[test]
fn curved_closed_surface_reduces_without_losing_support_or_manifold_edges() {
    let (positions, indices) = sphere(32, 64);
    let normals: Vec<_> = positions.iter().flatten().copied().collect();
    for triangle in indices.as_chunks::<3>().0 {
        let perpendicular = normal(&positions, triangle);
        let centroid = std::array::from_fn::<_, 3, _>(|axis| {
            triangle
                .iter()
                .map(|&vertex| f64::from(positions[vertex as usize][axis]))
                .sum::<f64>()
                / 3.0
        });
        assert!(
            perpendicular
                .iter()
                .zip(centroid)
                .map(|(a, b)| a * b)
                .sum::<f64>()
                > 0.0,
            "source sphere winding"
        );
    }
    let channels = [ErrorChannel {
        values: &normals,
        components: 3,
        maximum_delta: 0.15,
    }];
    let domains = vec![0; positions.len()];
    let output = reduce(
        MeshSource {
            positions_m: &positions,
            indices: &indices,
            domains: &domains,
            channels: &channels,
        },
        0.2,
    )
    .unwrap();
    assert_surface_mapping(&positions, &indices, &output, 0.2);
    assert!(output.indices.len() < indices.len());
    assert!(output.maximum_channel_deltas[0] <= f64::from(0.15_f32));
    assert!(output.minimum_face_alignment >= MIN_FACE_ALIGNMENT);
    let mut edges = BTreeMap::<(u32, u32), usize>::new();
    for face in output.indices.as_chunks::<3>().0 {
        let source = face.map(|vertex| output.retained_vertices[vertex as usize]);
        let perpendicular = normal(&positions, &source);
        let centroid = std::array::from_fn::<_, 3, _>(|axis| {
            source
                .iter()
                .map(|&vertex| f64::from(positions[vertex as usize][axis]))
                .sum::<f64>()
                / 3.0
        });
        assert!(
            perpendicular
                .iter()
                .zip(centroid)
                .map(|(a, b)| a * b)
                .sum::<f64>()
                > 0.0,
            "inward output face {source:?}"
        );
        for edge in 0..3 {
            let (a, b) = (face[edge], face[(edge + 1) % 3]);
            *edges.entry((a.min(b), a.max(b))).or_default() += 1;
        }
    }
    assert!(edges.values().all(|&count| count == 2));
    assert_eq!(
        output.retained_vertices.len() + output.indices.len() / 3,
        edges.len() + 2
    );
    println!(
        "mesh_hlod sphere source_vertices={} source_triangles={} output_vertices={} output_triangles={} max_position_error_m={} max_normal_component_delta={}",
        positions.len(),
        indices.len() / 3,
        output.retained_vertices.len(),
        output.indices.len() / 3,
        output.maximum_position_error_m,
        output.maximum_channel_deltas[0]
    );
}
