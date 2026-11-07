use super::*;
use bevy::{asset::RenderAssetUsages, mesh::MeshVertexAttribute};

const SECTION: MeshVertexAttribute =
    MeshVertexAttribute::new("ExactSourceSection", 0x464f_0010, VertexFormat::Uint32);

fn mesh(intervals: u16, section: u32) -> Mesh {
    let mut positions = Vec::new();
    let mut uv = Vec::new();
    for z in 0..=intervals {
        for x in 0..=intervals {
            let [x, z] = [x, z].map(|n| f32::from(n) / f32::from(intervals));
            positions.push([x, 0.0, z]);
            uv.push([x, z]);
        }
    }
    let n = positions.len();
    let mut mesh = Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::default(),
    );
    mesh.insert_attribute(Mesh::ATTRIBUTE_POSITION, positions);
    mesh.insert_attribute(Mesh::ATTRIBUTE_NORMAL, vec![[0.0, 1.0, 0.0]; n]);
    mesh.insert_attribute(Mesh::ATTRIBUTE_UV_0, uv);
    mesh.insert_attribute(Mesh::ATTRIBUTE_COLOR, vec![[0.1, 0.2, 0.3, 1.0]; n]);
    mesh.insert_attribute(SECTION, vec![section; n]);
    let columns = u32::from(intervals) + 1;
    let mut indices = Vec::new();
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
    mesh.insert_indices(Indices::U32(indices));
    mesh
}

fn policy() -> HlodGeometryPolicy {
    HlodGeometryPolicy {
        maximum_position_error_m: 0.12,
        maximum_normal_delta: 0.05,
        maximum_uv_delta: 0.1,
    }
}

#[test]
fn transformed_primitives_are_aggregated_and_reduced_with_all_source_attributes_retained() {
    let first = mesh(32, 11);
    let second = mesh(32, 22);
    let original = (first.clone(), second.clone());
    let pose = Transform::from_xyz(2.0, 1.0, 0.0)
        .with_rotation(Quat::from_rotation_y(0.5))
        .with_scale(Vec3::new(2.0, 1.0, 1.5));
    let result = prepare_aggregate(
        &[
            HlodPrimitive {
                mesh: &first,
                pose: Transform::IDENTITY,
            },
            HlodPrimitive {
                mesh: &second,
                pose,
            },
        ],
        policy(),
    )
    .unwrap();
    let stats = &result.statistics;
    assert_eq!(stats.source_primitives, 2);
    assert_eq!(stats.source_triangles, 4096);
    assert!(stats.output_triangles < stats.source_triangles / 2);
    assert!(stats.maximum_position_error_m <= policy().maximum_position_error_m);
    assert_eq!(original, (first, second));
    assert!(
        stats
            .maximum_attribute_deltas
            .iter()
            .all(|(name, value)| *value
                <= if name.contains("Uv") || name.contains("UV") {
                    f64::from(policy().maximum_uv_delta)
                } else {
                    f64::from(policy().maximum_normal_delta)
                })
    );
    let Some(VertexAttributeValues::Uint32(sections)) = result.mesh.attribute(SECTION) else {
        panic!("section attribute");
    };
    assert!(sections.contains(&11) && sections.contains(&22));
    assert!(sections.iter().all(|value| [11, 22].contains(value)));
    let Some(VertexAttributeValues::Float32x4(colors)) =
        result.mesh.attribute(Mesh::ATTRIBUTE_COLOR)
    else {
        panic!("colors");
    };
    assert!(
        colors
            .iter()
            .all(|value| value.map(f32::to_bits) == [0.1_f32, 0.2, 0.3, 1.0].map(f32::to_bits))
    );
    assert_eq!(stats.output_buffer_bytes, buffer_bytes(&result.mesh));
    println!(
        "static_hlod_geometry primitives={} source_triangles={} output_triangles={} source_vertices={} output_vertices={} max_world_error_m={} buffers_bytes={}",
        stats.source_primitives,
        stats.source_triangles,
        stats.output_triangles,
        stats.source_vertices,
        stats.output_vertices,
        stats.maximum_position_error_m,
        stats.output_buffer_bytes
    );
}

#[test]
fn zero_uv_error_can_keep_full_geometry_without_changing_the_requested_material_contract() {
    let source = mesh(16, 7);
    let mut exact = policy();
    exact.maximum_uv_delta = 0.0;
    let result = prepare_aggregate(
        &[HlodPrimitive {
            mesh: &source,
            pose: Transform::IDENTITY,
        }],
        exact,
    )
    .unwrap();
    assert_eq!(
        result.statistics.output_triangles,
        result.statistics.source_triangles
    );
    assert_eq!(result.statistics.output_vertices, source.count_vertices());
    assert!(
        result
            .statistics
            .maximum_attribute_deltas
            .iter()
            .all(|(_, delta)| delta.to_bits() == 0_f64.to_bits())
    );
}

#[test]
fn indexed_and_nonindexed_sources_share_the_same_compaction_path() {
    let indexed = mesh(32, 7);
    let mut nonindexed = indexed.clone();
    nonindexed.duplicate_vertices();
    let result = prepare_aggregate(
        &[
            HlodPrimitive {
                mesh: &indexed,
                pose: Transform::IDENTITY,
            },
            HlodPrimitive {
                mesh: &nonindexed,
                pose: Transform::from_xyz(2.0, 0.0, 0.0),
            },
        ],
        policy(),
    )
    .unwrap();
    assert_eq!(result.statistics.source_primitives, 2);
    assert!(result.statistics.output_triangles < result.statistics.source_triangles / 2);
    assert!(result.mesh.indices().is_some());
}

#[test]
fn malformed_poses_layouts_and_animated_sources_refuse_without_modifying_inputs() {
    let source = mesh(2, 1);
    let original = source.clone();
    for pose in [
        Transform::from_scale(Vec3::new(-1., 1., 1.)),
        Transform::from_xyz(f32::NAN, 0., 0.),
    ] {
        assert!(matches!(
            prepare_aggregate(
                &[HlodPrimitive {
                    mesh: &source,
                    pose
                }],
                policy()
            ),
            Err(HlodGeometryError::InvalidPose)
        ));
    }
    let mut other = source.clone();
    other.remove_attribute(SECTION);
    assert!(matches!(
        prepare_aggregate(
            &[
                HlodPrimitive {
                    mesh: &source,
                    pose: Transform::IDENTITY
                },
                HlodPrimitive {
                    mesh: &other,
                    pose: Transform::IDENTITY
                }
            ],
            policy()
        ),
        Err(HlodGeometryError::Unsupported(_))
    ));
    let mut animated = source.clone();
    animated.insert_attribute(
        Mesh::ATTRIBUTE_JOINT_WEIGHT,
        vec![[1., 0., 0., 0.]; animated.count_vertices()],
    );
    assert!(matches!(
        prepare_aggregate(
            &[HlodPrimitive {
                mesh: &animated,
                pose: Transform::IDENTITY
            }],
            policy()
        ),
        Err(HlodGeometryError::Unsupported(_))
    ));
    assert_eq!(source, original);
}

#[test]
fn preparation_quotas_and_invalid_attribute_tolerances_are_explicit() {
    let source = mesh(2, 1);
    let many: Vec<_> = (0..=MAX_PARTS)
        .map(|_| HlodPrimitive {
            mesh: &source,
            pose: Transform::IDENTITY,
        })
        .collect();
    assert!(matches!(
        prepare_aggregate(&many, policy()),
        Err(HlodGeometryError::BudgetExceeded)
    ));
    for maximum_uv_delta in [f32::NAN, -1.0, 1.0] {
        let policy = HlodGeometryPolicy {
            maximum_uv_delta,
            ..policy()
        };
        assert!(matches!(
            prepare_aggregate(
                &[HlodPrimitive {
                    mesh: &source,
                    pose: Transform::IDENTITY
                }],
                policy
            ),
            Err(HlodGeometryError::InvalidPolicy)
        ));
    }
}

#[test]
fn gpu_only_source_data_is_an_explicit_refusal_instead_of_a_mesh_access_panic() {
    let mut source = mesh(2, 1);
    let _gpu = source.take_gpu_data().unwrap();
    let result = prepare_aggregate(
        &[HlodPrimitive {
            mesh: &source,
            pose: Transform::IDENTITY,
        }],
        policy(),
    );
    assert!(matches!(
        result,
        Err(HlodGeometryError::Unsupported(
            "source CPU buffers are no longer retained"
        ))
    ));
}

#[test]
fn nonfinite_geometry_and_transform_overflow_are_rejected_before_preparation() {
    let mut source = mesh(2, 1);
    let Some(VertexAttributeValues::Float32x3(positions)) =
        source.attribute_mut(Mesh::ATTRIBUTE_POSITION)
    else {
        panic!("positions");
    };
    positions[0][0] = f32::NAN;
    assert!(matches!(
        prepare_aggregate(
            &[HlodPrimitive {
                mesh: &source,
                pose: Transform::IDENTITY
            }],
            policy()
        ),
        Err(HlodGeometryError::Unsupported(_))
    ));
    let source = mesh(2, 1);
    let pose = Transform::from_scale(Vec3::splat(f32::MAX)).with_translation(Vec3::splat(f32::MAX));
    assert!(matches!(
        prepare_aggregate(
            &[HlodPrimitive {
                mesh: &source,
                pose
            }],
            policy()
        ),
        Err(HlodGeometryError::InvalidPose)
    ));
}
