use super::*;

fn policy() -> CellStreamingPolicy {
    CellStreamingPolicy {
        origin_xz_m: [-1024.0, -1024.0],
        cell_size_m: 64.0,
        dimensions: [32, 32],
        detail_radius: 1,
        far_radius: 4,
        max_resident_cells: 81,
        full_cell_bytes: 1000,
        hlod_cell_bytes: 100,
        max_resident_bytes: 20_000,
        max_staging_bytes: 40_000,
    }
}

fn streamer() -> CellStreamer {
    CellStreamer::new(
        EntityId::deterministic("stream-session-a"),
        forge_core::bytes_sha256(b"authored-region-a"),
        policy(),
    )
    .expect("valid test policy")
}

fn acknowledge(plan: &CellStreamPlan) -> Vec<LoadedCell> {
    plan.acquisitions()
        .iter()
        .map(|(&key, &detail)| LoadedCell {
            key,
            detail,
            source: plan.source(),
            bytes: if detail == CellDetail::Full {
                1000
            } else {
                100
            },
        })
        .collect()
}

fn move_to(stream: &mut CellStreamer, position: [f64; 2]) -> CellStreamPlan {
    let plan = stream.prepare(position).expect("bounded destination");
    stream
        .commit(&plan, &acknowledge(&plan))
        .expect("exact acknowledgement");
    plan
}

#[test]
fn protected_simulation_cells_survive_view_motion_and_release_only_when_unprotected() {
    let mut p = policy();
    p.max_resident_cells = 100;
    p.max_resident_bytes = 30_000;
    p.max_staging_bytes = 60_000;
    let mut stream =
        CellStreamer::new(EntityId::deterministic("protected"), streamer().source, p).unwrap();
    let protected = CellKey { x: 24, z: 24 };
    let first = stream
        .prepare_with_protected_cells([-512.0; 2], &[protected])
        .unwrap();
    assert_eq!(first.desired().len(), 90);
    assert_eq!(first.estimated_resident_bytes, 25_200);
    stream.commit(&first, &acknowledge(&first)).unwrap();
    for z in 23..=25 {
        for x in 23..=25 {
            assert_eq!(
                stream.resident()[&CellKey { x, z }].detail,
                CellDetail::Full
            );
        }
    }
    let away = stream
        .prepare_with_protected_cells([-384.0; 2], &[protected, protected])
        .unwrap();
    stream.commit(&away, &acknowledge(&away)).unwrap();
    assert_eq!(stream.resident()[&protected].detail, CellDetail::Full);
    let release = stream.prepare([-384.0; 2]).unwrap();
    assert!(release.retirements().contains(&protected));
    stream.commit(&release, &acknowledge(&release)).unwrap();
    assert!(!stream.resident().contains_key(&protected));
}

#[test]
fn protection_promotes_far_geometry_and_refuses_invalid_or_unbudgeted_coverage_atomically() {
    let mut p = policy();
    p.max_resident_bytes = 30_000;
    p.max_staging_bytes = 60_000;
    let mut stream =
        CellStreamer::new(EntityId::deterministic("promote"), streamer().source, p).unwrap();
    move_to(&mut stream, [0.0; 2]);
    let before = stream.resident().clone();
    let far = CellKey { x: 19, z: 16 };
    let plan = stream
        .prepare_with_protected_cells([0.0; 2], &[far])
        .unwrap();
    assert_eq!(plan.desired().len(), before.len());
    assert_eq!(plan.acquisitions()[&far], CellDetail::Full);
    assert!(plan.estimated_resident_bytes > 16_200);
    for (protected, expected) in [
        (vec![CellKey { x: 31, z: 31 }], CellStreamError::CellBudget),
        (vec![CellKey { x: 32, z: 0 }], CellStreamError::OutsideGrid),
        (vec![far; 82], CellStreamError::CellBudget),
    ] {
        assert_eq!(
            stream
                .prepare_with_protected_cells([0.0; 2], &protected)
                .unwrap_err(),
            expected
        );
        assert_eq!(stream.resident(), &before);
    }
    let mut p = policy();
    p.max_resident_cells = 100;
    let tight = CellStreamer::new(EntityId::deterministic("tight"), stream.source, p).unwrap();
    assert_eq!(
        tight
            .prepare_with_protected_cells([0.0; 2], &[CellKey { x: 31, z: 31 }])
            .unwrap_err(),
        CellStreamError::ByteBudget
    );
}

#[test]
fn two_kilometre_region_keeps_only_bounded_neighbourhood_resident() {
    let mut stream = streamer();
    let plan = move_to(&mut stream, [0.0, 0.0]);
    assert_eq!(stream.resident().len(), 81);
    assert_eq!(
        plan.desired()
            .values()
            .filter(|&&detail| detail == CellDetail::Full)
            .count(),
        9
    );
    assert_eq!(plan.estimated_resident_bytes, 16_200);
    assert_eq!(plan.estimated_peak_bytes, 16_200);
    assert_eq!(32 * 32 - stream.resident().len(), 943);
}

#[test]
fn cells_are_half_open_and_region_edges_are_clamped() {
    let p = policy();
    assert_eq!(p.cell_at([-1024.0, -1024.0]), Ok(CellKey { x: 0, z: 0 }));
    assert_eq!(p.cell_at([-960.0, -960.0]), Ok(CellKey { x: 1, z: 1 }));
    assert_eq!(
        p.cell_at([-960.000_001, -960.000_001]),
        Ok(CellKey { x: 0, z: 0 })
    );
    let mut stream = streamer();
    move_to(&mut stream, [-1024.0, -1024.0]);
    assert_eq!(stream.resident().len(), 25);
    assert_eq!(
        stream.prepare([1024.0, 0.0]).unwrap_err(),
        CellStreamError::OutsideGrid
    );
}

#[test]
fn invalid_positions_and_policies_never_change_existing_residency() {
    let mut stream = streamer();
    move_to(&mut stream, [0.0, 0.0]);
    let before = stream.resident().clone();
    for position in [
        [f64::NAN, 0.0],
        [0.0, f64::INFINITY],
        [-1024.001, 0.0],
        [1e300, 0.0],
    ] {
        assert_eq!(
            stream.prepare(position).unwrap_err(),
            CellStreamError::OutsideGrid
        );
        assert_eq!(stream.resident(), &before);
    }
    let mut p = policy();
    p.cell_size_m = f64::NAN;
    assert_eq!(p.validate(), Err(CellStreamError::InvalidPolicy));
    p = policy();
    p.dimensions[0] = 0;
    assert_eq!(p.validate(), Err(CellStreamError::InvalidPolicy));
    p = policy();
    p.far_radius = MAX_RADIUS + 1;
    assert_eq!(p.validate(), Err(CellStreamError::InvalidPolicy));
    p = policy();
    p.detail_radius = p.far_radius;
    assert_eq!(p.validate(), Err(CellStreamError::InvalidPolicy));
}

#[test]
fn movement_retires_old_cells_and_return_preserves_logical_identities() {
    let mut stream = streamer();
    move_to(&mut stream, [-512.0, -512.0]);
    let first = stream.resident().clone();
    let away = move_to(&mut stream, [512.0, 512.0]);
    assert_eq!(away.retirements().len(), 81);
    assert!(first.keys().all(|key| !stream.resident().contains_key(key)));
    move_to(&mut stream, [-512.0, -512.0]);
    assert_eq!(stream.resident(), &first);
    let area = AreaId::deterministic("region-a");
    let key = CellKey { x: 8, z: 8 };
    assert_eq!(key.entity_id(area), key.entity_id(area));
    assert_ne!(
        key.entity_id(area),
        key.entity_id(AreaId::deterministic("region-b"))
    );
    assert_ne!(key.entity_id(area), CellKey { x: 9, z: 8 }.entity_id(area));
}

#[test]
fn unchanged_neighbourhood_needs_no_acquisitions() {
    let mut stream = streamer();
    move_to(&mut stream, [0.0, 0.0]);
    let plan = stream.prepare([63.0, 63.0]).unwrap();
    assert!(plan.acquisitions().is_empty());
    assert!(plan.retirements().is_empty());
    stream.commit(&plan, &[]).unwrap();
    assert_eq!(stream.resident().len(), 81);
}

#[test]
fn crossing_a_boundary_changes_detail_and_loads_only_needed_cells() {
    let mut stream = streamer();
    move_to(&mut stream, [0.0, 0.0]);
    let before = stream.resident().clone();
    let plan = stream.prepare([64.0, 0.0]).unwrap();
    assert_eq!(plan.retirements().len(), 9);
    assert_eq!(plan.acquisitions().len(), 15); // 9 incoming, 3 promotions, 3 demotions.
    assert_eq!(
        plan.acquisitions()[&CellKey { x: 18, z: 16 }],
        CellDetail::Full
    );
    assert_eq!(
        plan.acquisitions()[&CellKey { x: 15, z: 16 }],
        CellDetail::Hlod
    );
    assert_eq!(stream.resident(), &before); // preparation is read-only.
    stream.commit(&plan, &acknowledge(&plan)).unwrap();
    assert_eq!(
        stream.resident()[&CellKey { x: 18, z: 16 }].detail,
        CellDetail::Full
    );
}

#[test]
fn incomplete_duplicate_wrong_source_and_wrong_detail_refuse_atomically() {
    let mut stream = streamer();
    move_to(&mut stream, [0.0, 0.0]);
    let before = stream.resident().clone();
    let plan = stream.prepare([64.0, 0.0]).unwrap();
    let exact = acknowledge(&plan);
    let mut bad = vec![exact[..exact.len() - 1].to_vec()];
    let mut duplicate = exact.clone();
    duplicate.push(exact[0]);
    bad.push(duplicate);
    let mut stale = exact.clone();
    stale[0].source = forge_core::bytes_sha256(b"different");
    bad.push(stale);
    let mut wrong = exact.clone();
    wrong[0].detail = if wrong[0].detail == CellDetail::Full {
        CellDetail::Hlod
    } else {
        CellDetail::Full
    };
    bad.push(wrong);
    let mut empty = exact.clone();
    empty[0].bytes = 0;
    bad.push(empty);
    for cells in bad {
        assert_eq!(
            stream.commit(&plan, &cells),
            Err(CellStreamError::InvalidAcknowledgement)
        );
        assert_eq!(stream.resident(), &before);
    }
    stream.commit(&plan, &exact).unwrap(); // failed acknowledgements did not consume the plan.
}

#[test]
fn stale_generation_other_session_and_other_source_are_rejected() {
    let mut stream = streamer();
    let first = stream.prepare([0.0, 0.0]).unwrap();
    let second = stream.prepare([64.0, 0.0]).unwrap();
    stream.commit(&first, &acknowledge(&first)).unwrap();
    let before = stream.resident().clone();
    assert_eq!(
        stream.commit(&second, &acknowledge(&second)),
        Err(CellStreamError::StalePlan)
    );
    assert_eq!(stream.resident(), &before);
    let mut other = CellStreamer::new(
        EntityId::deterministic("other-session"),
        first.source(),
        policy(),
    )
    .unwrap();
    assert_eq!(
        other.commit(&first, &acknowledge(&first)),
        Err(CellStreamError::StalePlan)
    );
    let mut other_source = CellStreamer::new(
        EntityId::deterministic("stream-session-a"),
        forge_core::bytes_sha256(b"new-source"),
        policy(),
    )
    .unwrap();
    assert_eq!(
        other_source.commit(&first, &acknowledge(&first)),
        Err(CellStreamError::StalePlan)
    );
}

#[test]
fn budgets_include_retained_actual_bytes_and_old_payloads_during_staging() {
    let mut stream = streamer();
    move_to(&mut stream, [0.0, 0.0]);
    stream.policy.max_staging_bytes = 20_000;
    assert_eq!(
        stream.prepare([512.0, 512.0]).unwrap_err(),
        CellStreamError::ByteBudget
    );
    stream.policy.max_staging_bytes = 40_000;
    stream.policy.max_resident_cells = 80;
    assert_eq!(
        stream.prepare([0.0, 0.0]).unwrap_err(),
        CellStreamError::CellBudget
    );
    stream.policy.max_resident_cells = 81;
    let plan = stream.prepare([64.0, 0.0]).unwrap();
    let before = stream.resident().clone();
    let mut oversized = acknowledge(&plan);
    oversized[0].bytes = 40_000;
    assert_eq!(
        stream.commit(&plan, &oversized),
        Err(CellStreamError::ByteBudget)
    );
    oversized[0].bytes = u64::MAX;
    assert_eq!(
        stream.commit(&plan, &oversized),
        Err(CellStreamError::ByteBudget)
    );
    assert_eq!(stream.resident(), &before);
}

#[test]
fn larger_actual_payloads_are_charged_on_following_plans() {
    let mut stream = streamer();
    let plan = stream.prepare([0.0, 0.0]).unwrap();
    let mut cells = acknowledge(&plan);
    for cell in &mut cells {
        cell.bytes += 10;
    }
    stream.commit(&plan, &cells).unwrap();
    let next = stream.prepare([1.0, 1.0]).unwrap();
    assert_eq!(next.estimated_resident_bytes, 17_010);
    assert_eq!(next.estimated_peak_bytes, 17_010);
}

#[test]
fn generation_exhaustion_cannot_partially_publish() {
    let mut stream = streamer();
    stream.generation = u64::MAX;
    let plan = stream.prepare([0.0, 0.0]).unwrap();
    assert_eq!(
        stream.commit(&plan, &acknowledge(&plan)),
        Err(CellStreamError::GenerationExhausted)
    );
    assert!(stream.resident().is_empty());
}

#[test]
fn estimates_cannot_overflow_or_be_overridden_by_public_report_fields() {
    let mut stream = streamer();
    stream.policy.full_cell_bytes = u64::MAX;
    assert_eq!(
        stream.prepare([0.0, 0.0]).unwrap_err(),
        CellStreamError::ByteBudget
    );
    stream.policy = policy();
    let mut plan = stream.prepare([0.0, 0.0]).unwrap();
    plan.estimated_resident_bytes = 0;
    plan.estimated_peak_bytes = 0;
    let mut cells = acknowledge(&plan);
    cells[0].bytes = 100_000;
    assert_eq!(
        stream.commit(&plan, &cells),
        Err(CellStreamError::ByteBudget)
    );
    assert!(stream.resident().is_empty());
}

fn sampled_source() -> SampledCellSource {
    sampled_source_with_paint(false)
}

#[test]
fn mesh_quality_preserves_full_collision_samples_and_every_shared_edge() {
    let base = sampled_source_with_paint(true);
    let full = base
        .build(CellKey { x: 3, z: 3 }, CellDetail::Full)
        .unwrap();
    let mut counts = Vec::new();
    for stride in [2, 4, 8, 16, 32] {
        let quality = SampledMeshQuality::new(stride, stride).unwrap();
        let mut policy = policy();
        policy.dimensions = [8; 2];
        policy.cell_size_m = 256.0;
        let source =
            SampledCellSource::with_mesh_quality(base.terrain().clone(), policy, quality).unwrap();
        let same = source
            .build(CellKey { x: 3, z: 3 }, CellDetail::Full)
            .unwrap();
        assert_eq!(same.positions_m, full.positions_m);
        assert_eq!(same.indices, full.indices);
        assert_eq!(same.normals, full.normals);
        assert_eq!(same.material_weights, full.material_weights);
        let coarse = source
            .build(CellKey { x: 4, z: 3 }, CellDetail::Hlod)
            .unwrap();
        for (index, position) in full
            .positions_m
            .iter()
            .enumerate()
            .filter(|(_, p)| p[0] == 0.0)
        {
            let adjacent = coarse
                .positions_m
                .iter()
                .position(|v| v.map(f32::to_bits) == position.map(f32::to_bits))
                .unwrap();
            assert_eq!(
                coarse.normals[adjacent].map(f32::to_bits),
                full.normals[index].map(f32::to_bits)
            );
            assert_eq!(
                coarse.material_weights[adjacent].map(f32::to_bits),
                full.material_weights[index].map(f32::to_bits)
            );
        }
        counts.push(coarse.indices.len());
    }
    assert!(counts.windows(2).all(|pair| pair[0] > pair[1]));
    for invalid in [0, 1, 3, 33, 64, 255] {
        assert!(SampledMeshQuality::new(invalid, 8).is_err());
        assert!(SampledMeshQuality::new(8, invalid).is_err());
    }
}

fn sampled_source_with_paint(painted: bool) -> SampledCellSource {
    use forge_project::sampled_terrain::{
        HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource,
    };
    use forge_project::{
        HeightSamplingAlgorithm, TERRAIN_DOCUMENT_SCHEMA_VERSION_V4, TerrainExtent,
    };
    let mut document = crate::embedded_visual_valley_terrain_document().unwrap();
    let bytes: Vec<_> = (0_u32..256 * 256)
        .flat_map(|i| {
            // Original synthetic ramp; no external content or licensed model.
            let x = f64::from(i % 256);
            let z = f64::from(i / 256);
            #[allow(clippy::cast_possible_truncation)]
            ((x * 0.04).sin().mul_add(3.0, (z * 0.03).cos() * 2.0) as f32).to_le_bytes()
        })
        .collect();
    document.schema_version = TERRAIN_DOCUMENT_SCHEMA_VERSION_V4;
    document.extent_m = TerrainExtent::new(2048.0, 2048.0);
    document.height_sampling.algorithm = HeightSamplingAlgorithm::SampledRasterV1;
    document.height_sampling.sample_spacing_m = 8.0;
    document.height_sampling.base_elevation_m = 0.0;
    document.height_sampling.amplitude_m = 0.0;
    document.height_sampling.base_frequency_cycles_per_m = 1.0;
    document.height_sampling.octaves = 1;
    document.height_sampling.lacunarity = 2.0;
    document.height_sampling.persistence = 0.5;
    document.sampled_source = Some(SampledTerrainSource {
        schema: SAMPLED_TERRAIN_SCHEMA.into(),
        schema_version: 1,
        width: 256,
        height: 256,
        origin_xz_m: [-1024.0; 2],
        sample_spacing_m: 8.0,
        outside_height_m: 0.0,
        encoding: HeightEncoding::Float32Le,
        samples_sha256: forge_core::bytes_sha256(&bytes),
    });
    if painted {
        document.material_overrides = vec![forge_project::TerrainMaterialOverride {
            sample_x: 128,
            sample_z: 100,
            weights: vec![forge_project::TerrainMaterialWeight {
                layer_id: document.material_layers[1].id.clone(),
                weight: u16::MAX,
            }],
        }];
    }
    let terrain =
        crate::sampled_terrain::SampledTerrainPlan::from_document(&document, &bytes).unwrap();
    let mut p = policy();
    p.dimensions = [8; 2];
    p.cell_size_m = 256.0;
    p.full_cell_bytes = 100_000;
    p.hlod_cell_bytes = 100_000;
    p.max_resident_bytes = 10_000_000;
    p.max_staging_bytes = 20_000_000;
    SampledCellSource::new(std::sync::Arc::new(terrain), p, 8).unwrap()
}

#[test]
fn sampled_cells_reduce_geometry_and_preserve_every_mixed_lod_seam_sample() {
    let source = sampled_source();
    let full = source
        .build(CellKey { x: 3, z: 3 }, CellDetail::Full)
        .unwrap();
    let far = source
        .build(CellKey { x: 4, z: 3 }, CellDetail::Hlod)
        .unwrap();
    assert!(far.indices.len() < full.indices.len() / 2);
    assert!(far.acknowledgement.bytes < full.acknowledgement.bytes / 2);
    let mut matches = 0;
    for (position, normal) in full
        .positions_m
        .iter()
        .zip(&full.normals)
        .filter(|(p, _)| p[0] == 0.0)
    {
        let adjacent = far
            .positions_m
            .iter()
            .position(|candidate| candidate.map(f32::to_bits) == position.map(f32::to_bits))
            .unwrap();
        assert_eq!(
            far.normals[adjacent].map(f32::to_bits),
            normal.map(f32::to_bits)
        );
        matches += 1;
    }
    assert_eq!(matches, 33);
    assert_eq!(
        full.acknowledgement.source,
        source.terrain().document_hash()
    );
}

#[test]
fn sampled_cells_keep_authored_paint_on_mixed_detail_seams_and_charge_its_buffers() {
    let source = sampled_source_with_paint(true);
    let hash = source.terrain().document_hash();
    let full = source
        .build(CellKey { x: 3, z: 3 }, CellDetail::Full)
        .unwrap();
    let far = source
        .build(CellKey { x: 4, z: 3 }, CellDetail::Hlod)
        .unwrap();
    // Authored corner (128,100) lies on a seam retained at both levels.
    let position = [0.0_f32, -224.0].map(f32::to_bits);
    for mesh in [&full, &far] {
        let index = mesh
            .positions_m
            .iter()
            .position(|v| [v[0], v[2]].map(f32::to_bits) == position)
            .unwrap();
        assert_eq!(
            mesh.material_weights[index].map(f32::to_bits),
            [0.0_f32, 1.0, 0.0, 1.0].map(f32::to_bits)
        );
        assert_eq!(mesh.material_weights.len(), mesh.positions_m.len());
        assert!(
            mesh.material_weights
                .iter()
                .all(|weights| weights.iter().all(|v| v.is_finite())
                    && ((weights[0] + weights[1] + weights[2]) - 1.0).abs() < 0.0001)
        );
        let bytes = mesh.positions_m.capacity() * size_of::<[f32; 3]>()
            + mesh.normals.capacity() * size_of::<[f32; 3]>()
            + mesh.material_weights.capacity() * size_of::<[f32; 4]>()
            + mesh.indices.capacity() * size_of::<u32>();
        assert_eq!(mesh.acknowledgement.bytes, bytes as u64);
    }
    for (index, vertex) in full
        .positions_m
        .iter()
        .enumerate()
        .filter(|(_, v)| v[0] == 0.0)
    {
        let adjacent = far
            .positions_m
            .iter()
            .position(|v| v.map(f32::to_bits) == vertex.map(f32::to_bits))
            .unwrap();
        assert_eq!(
            full.material_weights[index].map(f32::to_bits),
            far.material_weights[adjacent].map(f32::to_bits)
        );
    }
    assert_eq!(source.terrain().document().content_hash().unwrap(), hash);
}

#[test]
fn sampled_cell_preparation_is_pinned_deterministic_and_accepted_by_controller() {
    let source = sampled_source();
    let mut p = policy();
    p.dimensions = [8; 2];
    p.cell_size_m = 256.0;
    p.full_cell_bytes = 100_000;
    p.hlod_cell_bytes = 100_000;
    p.max_resident_bytes = 10_000_000;
    p.max_staging_bytes = 20_000_000;
    let mut stream = CellStreamer::new(
        EntityId::deterministic("raster"),
        source.terrain().document_hash(),
        p.clone(),
    )
    .unwrap();
    let plan = stream.prepare([0.0; 2]).unwrap();
    let meshes = source.prepare(&plan).unwrap();
    stream
        .commit(
            &plan,
            &meshes
                .iter()
                .map(|mesh| mesh.acknowledgement)
                .collect::<Vec<_>>(),
        )
        .unwrap();
    assert_eq!(stream.resident().len(), 64);
    let first = &meshes[0];
    let again = source
        .build(first.acknowledgement.key, first.acknowledgement.detail)
        .unwrap();
    assert_eq!(first.positions_m, again.positions_m);
    assert_eq!(first.indices, again.indices);
    let before = source.terrain().document().content_hash().unwrap();
    assert_eq!(before, source.terrain().document_hash());
    assert!(
        source
            .prepare(&streamer().prepare([0.0; 2]).unwrap())
            .is_err()
    );
    assert!(
        source
            .build(CellKey { x: 8, z: 0 }, CellDetail::Full)
            .is_err()
    );
    p.origin_xz_m[0] += 1.0;
    assert!(SampledCellSource::new(source.terrain().clone(), p, 8).is_err());
}
