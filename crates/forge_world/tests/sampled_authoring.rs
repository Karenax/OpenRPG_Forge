use forge_core::{CommandId, bytes_sha256};
use forge_project::{
    HeightSamplingAlgorithm, TERRAIN_DOCUMENT_SCHEMA_VERSION_V4, TerrainDocument, TerrainExtent,
    TerrainHeightDelta,
    authoring::new_terrain,
    sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
    terrain_edit::{TerrainAction, TerrainEdit, TerrainEditRequest, TerrainEditSession},
};
use forge_world::sampled_terrain::SampledTerrainPlan;
use forge_world::terrain_brush::{BrushOperation, TerrainBrush};
use forge_world::terrain_brush_stroke::TerrainBrushStroke;

fn flat_brush_source(width: u32, spacing: f32) -> (TerrainDocument, Vec<u8>) {
    let (mut document, _) = fixture();
    let extent = f32::from(u16::try_from(width).unwrap()) * spacing;
    let bytes: Vec<_> = (0..width * width)
        .flat_map(|_| 7.0_f32.to_le_bytes())
        .collect();
    document.extent_m = TerrainExtent::new(extent, extent);
    document.height_sampling.sample_spacing_m = spacing;
    let source = document.sampled_source.as_mut().unwrap();
    source.width = width;
    source.height = width;
    source.origin_xz_m = [-f64::from(extent) * 0.5; 2];
    source.sample_spacing_m = f64::from(spacing);
    source.outside_height_m = 7.0;
    source.samples_sha256 = bytes_sha256(&bytes);
    (document, bytes)
}

fn raster_brush(operation: BrushOperation) -> TerrainBrush {
    TerrainBrush {
        x_m: 600.0,
        z_m: 600.0,
        radius_m: 8.0,
        strength: 1.0,
        operation,
    }
}

#[test]
fn sampled_wire_preview_uses_the_source_grid_and_remains_bounded_and_non_mutating() {
    let (document, bytes) = flat_brush_source(1024, 2.0);
    let source = SampledTerrainPlan::from_document(&document, &bytes).unwrap();
    let edit = raster_brush(BrushOperation::RaiseLower { metres: 4.0 })
        .prepare_sampled(&source)
        .unwrap();
    let preview =
        forge_world::terrain_brush_preview::TerrainBrushPreview::prepare_sampled(&source, &edit)
            .unwrap()
            .unwrap();
    assert_eq!(preview.source_hash, source.document_hash());
    assert!(
        preview
            .lines
            .iter()
            .any(|[before, after, _]| before.map(f32::to_bits)
                == [600.0_f32, 7.0, 600.0].map(f32::to_bits)
                && after.map(f32::to_bits) == [600.0_f32, 11.0, 600.0].map(f32::to_bits))
    );
    let mut samples = Vec::new();
    for z in 0..=200 {
        for x in 0..=200 {
            samples.push(TerrainHeightDelta::new(x, z, 1.0));
        }
    }
    let large = forge_world::terrain_brush_preview::TerrainBrushPreview::prepare_sampled(
        &source,
        &TerrainEdit::SetHeightDeltas { samples },
    )
    .unwrap()
    .unwrap();
    assert_eq!(large.changed_samples, 40_401);
    assert!(large.displayed_samples <= 2048);
    assert!(source.document().height_deltas.is_empty());
    assert_eq!(
        bytes_sha256(&bytes),
        source.raster().source().samples_sha256
    );
}

#[test]
fn sampled_zone_brush_uses_the_existing_composition_and_refuses_a_stale_visible_source() {
    let (document, bytes) = flat_brush_source(1024, 2.0);
    let session =
        forge_project::zone_authoring::session::ZoneEditSession::from_initial_terrain(document)
            .unwrap();
    let head = session.head();
    let before = head.content_hash().unwrap();
    let visible = forge_world::river_projection::project(head)
        .unwrap()
        .terrain;
    let source = SampledTerrainPlan::from_document(&visible, &bytes).unwrap();
    let stroke =
        TerrainBrushStroke::single(raster_brush(BrushOperation::RaiseLower { metres: 4.0 }));
    let direct = stroke.prepare_sampled(&source).unwrap();
    assert_eq!(
        forge_world::river_projection::prepare_sampled_brush(head, &stroke, &source).unwrap(),
        direct
    );
    let mut stale = visible;
    stale.name = "Another source revision".into();
    let stale = source.with_document(&stale).unwrap();
    assert!(forge_world::river_projection::prepare_sampled_brush(head, &stroke, &stale).is_err());
    assert_eq!(head.content_hash().unwrap(), before);
}

fn apply_brush_edit(source: &SampledTerrainPlan, edit: TerrainEdit) -> SampledTerrainPlan {
    let TerrainEdit::SetHeightDeltas { samples } = edit else {
        panic!("Expected height brush edit")
    };
    let mut document = source.document().clone();
    document.height_deltas = samples;
    source.with_document(&document).unwrap()
}

#[test]
fn sampled_brush_outside_legacy_extent_is_local_and_undoable_with_immutable_source() {
    let (document, bytes) = flat_brush_source(1024, 2.0);
    let source = SampledTerrainPlan::from_document(&document, &bytes).unwrap();
    let brush = raster_brush(BrushOperation::RaiseLower { metres: 4.0 });
    let mut stroke = TerrainBrushStroke::single(brush);
    let edit = stroke.prepare_sampled(&source).unwrap();
    stroke.points.push([600.0, 600.0]);
    assert_eq!(stroke.prepare_sampled(&source).unwrap(), edit);
    let changed = apply_brush_edit(&source, edit.clone());
    assert_eq!(
        changed.height_at(600.0, 600.0).unwrap().to_bits(),
        11.0_f64.to_bits()
    );
    assert_eq!(
        changed.height_at(620.0, 600.0).unwrap().to_bits(),
        7.0_f64.to_bits()
    );
    assert!(source.document().height_deltas.is_empty());
    let mut session = TerrainEditSession::new(document).unwrap();
    execute(&mut session, "sampled-brush", TerrainAction::Edit(edit));
    execute(&mut session, "sampled-brush-undo", TerrainAction::Undo);
    assert!(session.document().height_deltas.is_empty());
    execute(&mut session, "sampled-brush-redo", TerrainAction::Redo);
    let restored =
        TerrainEditSession::from_archive_bytes(&session.archive_bytes().unwrap()).unwrap();
    assert_eq!(restored.document(), session.document());
    assert_eq!(
        source
            .with_document(restored.document())
            .unwrap()
            .height_at(600.0, 600.0)
            .unwrap()
            .to_bits(),
        11.0_f64.to_bits()
    );
    assert_eq!(
        bytes_sha256(&bytes),
        source.raster().source().samples_sha256
    );
}

#[test]
fn sampled_flatten_smooth_slope_and_noise_use_the_actual_raster_heights() {
    let (document, bytes) = flat_brush_source(1024, 2.0);
    let source = SampledTerrainPlan::from_document(&document, &bytes).unwrap();
    let flattened = apply_brush_edit(
        &source,
        raster_brush(BrushOperation::Flatten { height_m: 9.0 })
            .prepare_sampled(&source)
            .unwrap(),
    );
    assert_eq!(
        flattened.height_at(600.0, 600.0).unwrap().to_bits(),
        9.0_f64.to_bits()
    );
    let mut spike = document.clone();
    spike.height_deltas = vec![TerrainHeightDelta::new(812, 812, 8.0)];
    let spike = source.with_document(&spike).unwrap();
    let smoothed = apply_brush_edit(
        &spike,
        raster_brush(BrushOperation::Smooth)
            .prepare_sampled(&spike)
            .unwrap(),
    );
    assert_eq!(
        smoothed.height_at(600.0, 600.0).unwrap().to_bits(),
        7.0_f64.to_bits()
    );
    let slope = apply_brush_edit(
        &source,
        raster_brush(BrushOperation::Slope {
            height_m: 7.0,
            gradient_x: 0.5,
            gradient_z: 0.0,
        })
        .prepare_sampled(&source)
        .unwrap(),
    );
    assert_eq!(
        slope.height_at(602.0, 600.0).unwrap().to_bits(),
        7.75_f64.to_bits()
    );
    assert_eq!(
        slope.height_at(598.0, 600.0).unwrap().to_bits(),
        6.25_f64.to_bits()
    );
    let noise = raster_brush(BrushOperation::Noise {
        amplitude_m: 3.0,
        wavelength_m: 16.0,
        seed: 17,
    });
    let first = noise.prepare_sampled(&source).unwrap();
    assert_eq!(first, noise.prepare_sampled(&source).unwrap());
    assert_ne!(
        first,
        TerrainBrush {
            operation: BrushOperation::Noise {
                amplitude_m: 3.0,
                wavelength_m: 16.0,
                seed: 18
            },
            ..noise
        }
        .prepare_sampled(&source)
        .unwrap()
    );
    let TerrainEdit::SetHeightDeltas { samples } = first else {
        panic!("Expected noise deltas")
    };
    assert!(samples.iter().all(|sample| sample.delta_m.abs() <= 3.0));
}

#[test]
fn sampled_brush_rejects_excessive_work_and_invalid_parameters_without_mutation() {
    let (document, bytes) = flat_brush_source(512, 0.03125);
    let dense = SampledTerrainPlan::from_document(&document, &bytes).unwrap();
    let brush = TerrainBrush {
        x_m: 0.0,
        z_m: 0.0,
        radius_m: 128.0,
        ..raster_brush(BrushOperation::RaiseLower { metres: 1.0 })
    };
    assert!(
        brush
            .prepare_sampled(&dense)
            .unwrap_err()
            .contains("échantillons")
    );
    assert!(dense.document().height_deltas.is_empty());
    let (document, bytes) = flat_brush_source(512, 0.25);
    let source = SampledTerrainPlan::from_document(&document, &bytes).unwrap();
    let mut stroke = TerrainBrushStroke::single(TerrainBrush {
        radius_m: 32.0,
        x_m: 0.0,
        z_m: 0.0,
        ..brush
    });
    stroke.points = (0..12)
        .map(|index| [if index % 2 == 0 { -32.0 } else { 32.0 }, 0.0])
        .collect();
    assert!(
        stroke
            .prepare_sampled(&source)
            .unwrap_err()
            .contains("échantillons")
    );
    for invalid in [
        TerrainBrush {
            x_m: 65.0,
            radius_m: 8.0,
            ..brush
        },
        TerrainBrush {
            strength: f32::NAN,
            radius_m: 8.0,
            ..brush
        },
        TerrainBrush {
            radius_m: 8.0,
            operation: BrushOperation::Noise {
                amplitude_m: 1.0,
                wavelength_m: 0.0,
                seed: 1,
            },
            ..brush
        },
    ] {
        assert!(invalid.prepare_sampled(&source).is_err());
    }
    assert!(source.document().height_deltas.is_empty());
}

#[test]
fn edit_plans_share_immutable_samples_and_reject_another_source_or_zone() {
    let (mut document, bytes) = fixture();
    let original = SampledTerrainPlan::from_document(&document, &bytes).unwrap();
    document.height_deltas = vec![TerrainHeightDelta::new(2, 2, 8.0)];
    let changed = original.with_document(&document).unwrap();
    assert!(std::ptr::eq(original.raster(), changed.raster()));
    assert_eq!(
        original.height_at(0.0, 0.0).unwrap().to_bits(),
        7.5_f64.to_bits()
    );
    assert_eq!(
        changed.height_at(0.0, 0.0).unwrap().to_bits(),
        15.5_f64.to_bits()
    );
    assert_ne!(original.document_hash(), changed.document_hash());
    assert!(std::ptr::eq(
        changed.with_document(original.document()).unwrap().raster(),
        original.raster(),
    ));
    let mut wrong = document.clone();
    wrong.sampled_source.as_mut().unwrap().samples_sha256 = bytes_sha256(b"another raster");
    assert!(original.with_document(&wrong).is_err());
    wrong = document.clone();
    wrong.area_id = forge_core::AreaId::deterministic("another zone");
    assert!(original.with_document(&wrong).is_err());
    wrong = document.clone();
    wrong.sampled_source.as_mut().unwrap().origin_xz_m[0] += 1.0;
    assert!(original.with_document(&wrong).is_err());
    wrong = document.clone();
    wrong.height_sampling.base_elevation_m = f32::MAX;
    wrong.height_deltas = vec![TerrainHeightDelta::new(2, 2, f32::MAX)];
    assert!(original.with_document(&wrong).is_err());
    assert_eq!(
        bytes_sha256(&bytes),
        original.raster().source().samples_sha256
    );
}

#[test]
#[allow(clippy::float_cmp)] // Binary fractions deliberately give exact expected texels.
fn derived_cell_centres_preserve_source_edges_and_average_corner_edits() {
    let (mut doc, bytes) = fixture();
    let original = SampledTerrainPlan::from_document(&doc, &bytes).unwrap();
    let heights = original.cell_center_heights();
    assert_eq!(
        heights
            .iter()
            .flat_map(|v| v.to_le_bytes())
            .collect::<Vec<_>>(),
        bytes
    );
    doc.height_sampling.base_elevation_m = 2.0;
    doc.height_deltas = vec![
        TerrainHeightDelta::new(1, 1, 8.0),
        TerrainHeightDelta::new(4, 4, 12.0),
    ];
    let plan = SampledTerrainPlan::from_document(&doc, &bytes).unwrap();
    let derived = plan.cell_center_heights();
    for z in 0..4 {
        for x in 0..4 {
            let extra = if x < 2 && z < 2 {
                2.0
            } else if x == 3 && z == 3 {
                3.0
            } else {
                0.0
            };
            assert_eq!(derived[z * 4 + x], heights[z * 4 + x] + 2.0 + extra);
            if x < 3 && z < 3 {
                let wx = f64::from(u32::try_from(x).unwrap()) - 1.5;
                let wz = f64::from(u32::try_from(z).unwrap()) - 1.5;
                assert_eq!(
                    f64::from(derived[z * 4 + x]),
                    plan.height_at(wx, wz).unwrap()
                );
            }
        }
    }
    // The outside-query sentinel must never overwrite the final source texel.
    assert_eq!(derived[15], 20.0);
    assert_eq!(original.cell_center_heights(), heights);
}

fn fixture() -> (TerrainDocument, Vec<u8>) {
    let bytes: Vec<_> = (0_u16..16)
        .flat_map(|v| f32::from(v).to_le_bytes())
        .collect();
    let mut document = new_terrain("sampled", "authoring", "Raster").unwrap();
    document.schema_version = TERRAIN_DOCUMENT_SCHEMA_VERSION_V4;
    document.extent_m = TerrainExtent::new(4.0, 4.0);
    document.height_sampling.algorithm = HeightSamplingAlgorithm::SampledRasterV1;
    document.height_sampling.sample_spacing_m = 1.0;
    document.height_sampling.base_elevation_m = 0.0;
    document.height_sampling.amplitude_m = 0.0;
    document.height_sampling.base_frequency_cycles_per_m = 1.0;
    document.height_sampling.octaves = 1;
    document.height_sampling.lacunarity = 2.0;
    document.height_sampling.persistence = 0.5;
    document.sampled_source = Some(SampledTerrainSource {
        schema: SAMPLED_TERRAIN_SCHEMA.into(),
        schema_version: 1,
        width: 4,
        height: 4,
        origin_xz_m: [-2.0; 2],
        sample_spacing_m: 1.0,
        outside_height_m: -90.0,
        encoding: HeightEncoding::Float32Le,
        samples_sha256: bytes_sha256(&bytes),
    });
    (document, bytes)
}

#[test]
fn sampled_plan_requires_the_same_document_revision_before_projection() {
    let (document, bytes) = fixture();
    let plan = SampledTerrainPlan::from_document(&document, &bytes).unwrap();
    assert!(plan.require_document(&document).is_ok());
    let mut edited = document.clone();
    edited
        .height_deltas
        .push(TerrainHeightDelta::new(2, 2, 8.0));
    assert!(plan.require_document(&edited).is_err());
    let updated = plan.with_document(&edited).unwrap();
    assert!(updated.require_document(&edited).is_ok());
    assert!(updated.require_document(&document).is_err());
    let mut other_area = edited;
    other_area.area_id = forge_core::AreaId::deterministic("other-area");
    assert!(updated.require_document(&other_area).is_err());
}

fn execute(session: &mut TerrainEditSession, id: &str, action: TerrainAction) {
    session
        .execute(&TerrainEditRequest {
            command_id: CommandId::deterministic(id),
            expected_revision: session.document().revision,
            expected_hash: session.document().content_hash().unwrap(),
            action,
        })
        .unwrap();
}

#[test]
#[allow(clippy::float_cmp)]
fn raster_retouch_survives_history_archive_and_keeps_source_identity() {
    let (document, bytes) = fixture();
    let source = document.sampled_source.clone();
    let original = SampledTerrainPlan::from_document(&document, &bytes).unwrap();
    assert_eq!(original.height_at(0.0, 0.0).unwrap(), 7.5);
    let mut session = TerrainEditSession::new(document).unwrap();
    execute(
        &mut session,
        "raise",
        TerrainAction::Edit(TerrainEdit::SetHeightDeltas {
            samples: vec![TerrainHeightDelta::new(2, 2, 8.0)],
        }),
    );
    let altered = SampledTerrainPlan::from_document(session.document(), &bytes).unwrap();
    assert_eq!(altered.height_at(0.0, 0.0).unwrap(), 15.5);
    assert_eq!(altered.height_at(0.5, 0.5).unwrap(), 12.0);
    assert_eq!(altered.height_at(-1.5, -1.5).unwrap(), 0.0);
    assert_ne!(altered.document_hash(), original.document_hash());
    execute(&mut session, "undo", TerrainAction::Undo);
    assert_eq!(
        SampledTerrainPlan::from_document(session.document(), &bytes)
            .unwrap()
            .height_at(0.0, 0.0)
            .unwrap(),
        7.5
    );
    execute(&mut session, "redo", TerrainAction::Redo);
    let archive = session.archive_bytes().unwrap();
    let restored = TerrainEditSession::from_archive_bytes(&archive).unwrap();
    assert_eq!(restored.document(), session.document());
    assert_eq!(restored.document().sampled_source, source);
    assert_eq!(
        SampledTerrainPlan::from_document(restored.document(), &bytes)
            .unwrap()
            .height_at(0.0, 0.0)
            .unwrap(),
        15.5
    );
}

#[test]
fn incompatible_or_unresolved_sources_fail_closed() {
    let (document, bytes) = fixture();
    for version in [1, 2, 3] {
        let mut bad = document.clone();
        bad.schema_version = version;
        assert!(bad.validate().is_err());
    }
    let mut bad = document.clone();
    bad.sampled_source = None;
    assert!(bad.validate().is_err());
    let mut bad = document.clone();
    bad.height_sampling.algorithm = HeightSamplingAlgorithm::FractalValueNoiseV1;
    assert!(bad.validate().is_err());
    let mut bad = document.clone();
    bad.sampled_source.as_mut().unwrap().origin_xz_m[0] += 0.5;
    assert!(bad.validate().is_err());
    let mut bad = document.clone();
    bad.sampled_source.as_mut().unwrap().width = 3;
    assert!(bad.validate().is_err());
    let mut bad = document.clone();
    bad.height_sampling.amplitude_m = 1.0;
    assert!(bad.validate().is_err());
    assert!(SampledTerrainPlan::from_document(&document, &[]).is_err());
    let mut corrupt = bytes.clone();
    corrupt[0] ^= 1;
    assert!(SampledTerrainPlan::from_document(&document, &corrupt).is_err());
    let legacy = new_terrain("legacy", "legacy", "Legacy").unwrap();
    assert!(!legacy.to_pretty_json().unwrap().contains("sampled_source"));
    assert!(forge_world::TerrainGenerationPlan::from_document(&document).is_err());
}

#[test]
fn patches_share_edges_normals_and_author_corrections_without_seams() {
    let (mut document, bytes) = fixture();
    document.height_deltas = vec![TerrainHeightDelta::new(2, 2, 3.0)];
    let plan = SampledTerrainPlan::from_document(&document, &bytes).unwrap();
    let left = plan.build_patch([0, 0], [2, 4], 1).unwrap();
    let right = plan.build_patch([2, 0], [2, 4], 1).unwrap();
    for row in 0..5 {
        assert_eq!(
            left.positions_m[row * 3 + 2].map(f32::to_bits),
            right.positions_m[row * 3].map(f32::to_bits)
        );
        assert_eq!(
            left.normals[row * 3 + 2].map(f32::to_bits),
            right.normals[row * 3].map(f32::to_bits)
        );
    }
    assert_eq!(left.indices.len(), 48);
    assert!(
        left.indices
            .iter()
            .all(|i| (*i as usize) < left.positions_m.len())
    );
    for triangle in left.indices.as_chunks::<3>().0 {
        let [a, b, c] =
            [triangle[0], triangle[1], triangle[2]].map(|i| left.positions_m[i as usize]);
        assert!((b[2] - a[2]) * (c[0] - a[0]) - (b[0] - a[0]) * (c[2] - a[2]) > 0.0);
    }
    assert!(plan.build_patch([3, 0], [2, 1], 1).is_err());
    assert!(plan.build_patch([0, 0], [1, 1], 0).is_err());
    assert!(plan.build_patch([0, 0], [129, 1], 1).is_err());
    assert!(plan.build_patch([0, 0], [128, 128], u16::MAX).is_err());
}

#[test]
fn sampled_placement_alignment_uses_grid_then_authored_height_and_preserves_transform() {
    use forge_core::{Transform, UnitQuaternion, Vec3};
    use forge_world::{placement_snap::PlacementSnap, placement_transform::Axis};
    let (mut doc, bytes) = fixture();
    let original_hash = bytes_sha256(&bytes);
    doc.height_sampling.base_elevation_m = 2.0;
    doc.height_deltas = vec![TerrainHeightDelta::new(2, 2, 8.0)];
    let plan = SampledTerrainPlan::from_document(&doc, &bytes).unwrap();
    let source = Transform {
        translation_m: Vec3::new(0.2, 99.0, -0.2),
        rotation: UnitQuaternion::new(0.0, 1.0, 0.0, 0.0),
        scale: Vec3::new(2.0, 3.0, 4.0),
    };
    let snap = PlacementSnap {
        grid_step_m: Some(1.0),
        surface_offset_m: Some(0.25),
    };
    let aligned = snap.resolve_sampled(source, &plan).unwrap();
    // Raster centre 7.5 + base elevation 2 + authored corner 8 + offset .25.
    assert_eq!(aligned.translation_m, Vec3::new(0.0, 17.75, 0.0));
    assert_eq!(aligned.rotation, source.rotation);
    assert_eq!(aligned.scale, source.scale);
    assert_eq!(source.translation_m, Vec3::new(0.2, 99.0, -0.2));
    let axis = snap.resolve_axis_sampled(source, Axis::X, &plan).unwrap();
    assert_eq!(axis.translation_m.x.to_bits(), 0.0_f32.to_bits());
    assert_eq!(
        axis.translation_m.z.to_bits(),
        source.translation_m.z.to_bits()
    );
    assert!(axis.translation_m.y > 15.34 && axis.translation_m.y < 15.36);
    assert!(snap.resolve_axis_sampled(source, Axis::Y, &plan).is_err());
    assert_eq!(
        PlacementSnap::default()
            .resolve_sampled(source, &plan)
            .unwrap(),
        source
    );
    let outside = Transform::from_translation(Vec3::new(3.0, 9.0, 0.0));
    assert!(snap.resolve_sampled(outside, &plan).is_err());
    assert_eq!(
        PlacementSnap::default()
            .resolve_sampled(outside, &plan)
            .unwrap(),
        outside
    );
    assert_eq!(bytes_sha256(&bytes), original_hash);
    assert_eq!(plan.document(), &doc);
}
