use forge_core::CommandId;
use forge_project::{
    authoring::new_terrain,
    terrain_edit::{TerrainAction, TerrainEditRequest, TerrainEditSession},
};
use forge_world::{
    TerrainGenerationPlan, TerrainPartialUpdateRequest,
    terrain_brush::{BrushOperation, TerrainBrush},
};

fn brush(operation: BrushOperation) -> TerrainBrush {
    TerrainBrush {
        x_m: 0.0,
        z_m: 0.0,
        radius_m: 12.0,
        strength: 1.0,
        operation,
    }
}

#[test]
fn continuous_stroke_is_one_undo_and_independent_of_stationary_samples() {
    use forge_world::terrain_brush_stroke::TerrainBrushStroke;
    let source = new_terrain("brush", "stroke", "Stroke").unwrap();
    let make = |points| TerrainBrushStroke {
        brush: brush(BrushOperation::RaiseLower { metres: 2.0 }),
        points,
    };
    let stroke = make(vec![[0.0, 0.0], [24.0, 0.0]]);
    let edit = stroke.prepare(&source).unwrap();
    assert_eq!(
        edit,
        make(vec![
            [0.0, 0.0],
            [0.0, 0.0],
            [3.0, 0.0],
            [9.0, 0.0],
            [24.0, 0.0]
        ])
        .prepare(&source)
        .unwrap()
    );
    let mut session = TerrainEditSession::new(source.clone()).unwrap();
    apply(&mut session, TerrainAction::Edit(edit), "stroke");
    let painted = session.document().clone();
    let plan = TerrainGenerationPlan::from_document(&painted).unwrap();
    for x in [0.0, 6.0, 12.0, 18.0, 24.0] {
        assert!(plan.height_at(x, 0.0).unwrap() >= 2.0);
    }
    assert_eq!(
        plan.height_at(12.0, 20.0).unwrap().to_bits(),
        0.0_f32.to_bits()
    );
    apply(&mut session, TerrainAction::Undo, "undo-stroke");
    assert_eq!(session.document().height_deltas, source.height_deltas);
    apply(&mut session, TerrainAction::Redo, "redo-stroke");
    assert_eq!(session.document().height_deltas, painted.height_deltas);
    let restored =
        TerrainEditSession::from_archive_bytes(&session.archive_bytes().unwrap()).unwrap();
    assert_eq!(restored.document(), session.document());
    assert!(source.height_deltas.is_empty());
}

#[test]
fn stroke_rejects_invalid_or_excessive_paths_without_partial_edit() {
    use forge_world::terrain_brush_stroke::TerrainBrushStroke;
    let source = new_terrain("brush", "stroke-invalid", "Stroke").unwrap();
    let hash = source.content_hash().unwrap();
    for points in [
        vec![],
        vec![[f32::NAN, 0.0]],
        vec![[257.0, 0.0]],
        vec![[0.0, 0.0]; 1025],
        vec![[-256.0, 0.0], [256.0, 0.0]],
    ] {
        let mut brush = brush(BrushOperation::Smooth);
        brush.radius_m = 2.0;
        assert!(
            TerrainBrushStroke { brush, points }
                .prepare(&source)
                .is_err()
        );
        assert_eq!(source.content_hash().unwrap(), hash);
    }
}

fn apply(session: &mut TerrainEditSession, action: TerrainAction, key: &str) {
    session
        .execute(&TerrainEditRequest {
            command_id: CommandId::deterministic(key),
            expected_revision: session.document().revision,
            expected_hash: session.document().content_hash().unwrap(),
            action,
        })
        .unwrap();
}

#[test]
fn brush_history_archive_and_partial_generation_agree() {
    let original = new_terrain("brush", "zone", "Brush").unwrap();
    let plan = TerrainGenerationPlan::from_document(&original).unwrap();
    let mut cache = plan.generate().unwrap();
    let mut session = TerrainEditSession::new(original.clone()).unwrap();
    let edit = brush(BrushOperation::RaiseLower { metres: 4.0 })
        .prepare(&original)
        .unwrap();
    assert_eq!(
        edit,
        brush(BrushOperation::RaiseLower { metres: 4.0 })
            .prepare(&original)
            .unwrap()
    );
    apply(&mut session, TerrainAction::Edit(edit), "raise");
    let raised = session.document().clone();
    let updated = TerrainGenerationPlan::from_document(&raised).unwrap();
    assert_eq!(
        updated.height_at(0.0, 0.0).unwrap().to_bits(),
        4.0_f32.to_bits()
    );
    assert_eq!(
        updated.height_at(20.0, 0.0).unwrap().to_bits(),
        0.0_f32.to_bits()
    );
    let change = plan
        .apply_authoring_update(
            &cache,
            TerrainPartialUpdateRequest {
                base_revision: original.revision,
                base_document_hash: original.content_hash().unwrap(),
                target_document: raised.clone(),
            },
        )
        .unwrap();
    assert!(change.mesh_chunks().len() < 64);
    change.apply_to_cache(&mut cache).unwrap();
    assert_eq!(cache, updated.generate().unwrap());
    apply(&mut session, TerrainAction::Undo, "undo");
    assert_eq!(session.document().height_deltas, original.height_deltas);
    apply(&mut session, TerrainAction::Redo, "redo");
    assert_eq!(session.document().height_deltas, raised.height_deltas);
    let restored =
        TerrainEditSession::from_archive_bytes(&session.archive_bytes().unwrap()).unwrap();
    assert_eq!(restored.document(), session.document());
}

#[test]
fn flatten_smooth_and_lower_use_current_height_and_preserve_outside() {
    let mut session =
        TerrainEditSession::new(new_terrain("brush", "second", "Brush").unwrap()).unwrap();
    for (key, operation) in [
        ("raise", BrushOperation::RaiseLower { metres: 4.0 }),
        ("flatten", BrushOperation::Flatten { height_m: 8.0 }),
        ("lower", BrushOperation::RaiseLower { metres: -2.0 }),
    ] {
        let edit = brush(operation).prepare(session.document()).unwrap();
        apply(&mut session, TerrainAction::Edit(edit), key);
    }
    let plan = TerrainGenerationPlan::from_document(session.document()).unwrap();
    assert_eq!(
        plan.height_at(0.0, 0.0).unwrap().to_bits(),
        6.0_f32.to_bits()
    );
    let outside = session.document().height_deltas.clone();
    let edit = brush(BrushOperation::Smooth)
        .prepare(session.document())
        .unwrap();
    apply(&mut session, TerrainAction::Edit(edit), "smooth");
    let smoothed = TerrainGenerationPlan::from_document(session.document()).unwrap();
    assert!(smoothed.height_at(0.0, 0.0).unwrap() < 6.0);
    assert_eq!(
        smoothed.height_at(20.0, 0.0).unwrap().to_bits(),
        0.0_f32.to_bits()
    );
    assert!(!outside.is_empty());
}

#[test]
fn invalid_brush_does_not_produce_an_edit() {
    let original = new_terrain("brush", "invalid", "Brush").unwrap();
    for radius in [f32::NAN, 0.0, 129.0] {
        let mut invalid = brush(BrushOperation::Smooth);
        invalid.radius_m = radius;
        assert!(invalid.prepare(&original).is_err());
    }
    assert!(
        brush(BrushOperation::RaiseLower {
            metres: f32::INFINITY
        })
        .prepare(&original)
        .is_err()
    );
}

#[test]
fn noise_is_seeded_bounded_and_slope_has_the_requested_direction() {
    let document = new_terrain("brush", "noise-slope", "Brush").unwrap();
    let noise = |seed| {
        brush(BrushOperation::Noise {
            amplitude_m: 3.0,
            wavelength_m: 16.0,
            seed,
        })
    };
    let first = noise(42).prepare(&document).unwrap();
    assert_eq!(first, noise(42).prepare(&document).unwrap());
    assert_ne!(first, noise(43).prepare(&document).unwrap());
    let mut session = TerrainEditSession::new(document.clone()).unwrap();
    apply(&mut session, TerrainAction::Edit(first), "noise");
    assert!(
        session
            .document()
            .height_deltas
            .iter()
            .all(|s| s.delta_m.abs() <= 3.0)
    );
    apply(&mut session, TerrainAction::Undo, "noise-undo");
    assert!(session.document().height_deltas.is_empty());
    let slope = brush(BrushOperation::Slope {
        height_m: 0.0,
        gradient_x: 0.5,
        gradient_z: -0.25,
    });
    let edit = slope.prepare(session.document()).unwrap();
    apply(&mut session, TerrainAction::Edit(edit), "slope");
    let plan = TerrainGenerationPlan::from_document(session.document()).unwrap();
    assert!((plan.height_at(2.0, 0.0).unwrap() - 5.0 / 6.0).abs() < 1e-5);
    assert!((plan.height_at(0.0, 2.0).unwrap() + 5.0 / 12.0).abs() < 1e-5);
    assert!(
        brush(BrushOperation::Noise {
            amplitude_m: 1.0,
            wavelength_m: 0.0,
            seed: 0
        })
        .prepare(&document)
        .is_err()
    );
    assert!(
        brush(BrushOperation::Slope {
            height_m: 0.0,
            gradient_x: f32::NAN,
            gradient_z: 0.0
        })
        .prepare(&document)
        .is_err()
    );
}

#[test]
fn geometric_preview_is_bounded_and_never_changes_the_source() {
    use forge_world::terrain_brush_preview::TerrainBrushPreview;
    let document = new_terrain("preview", "zone", "Preview").unwrap();
    let hash = document.content_hash().unwrap();
    let edit = brush(BrushOperation::RaiseLower { metres: 4.0 })
        .prepare(&document)
        .unwrap();
    let preview = TerrainBrushPreview::prepare(&document, &edit)
        .unwrap()
        .unwrap();
    assert_eq!(preview.source_hash, hash);
    assert_eq!(document.content_hash().unwrap(), hash);
    assert!(
        preview
            .lines
            .iter()
            .any(|[before, after, _]| before.map(f32::to_bits)
                == [0.0_f32, 0.0, 0.0].map(f32::to_bits)
                && after.map(f32::to_bits) == [0.0_f32, 4.0, 0.0].map(f32::to_bits))
    );
    let mut wide = brush(BrushOperation::RaiseLower { metres: -2.0 });
    wide.radius_m = 128.0;
    let edit = wide.prepare(&document).unwrap();
    let preview = TerrainBrushPreview::prepare(&document, &edit)
        .unwrap()
        .unwrap();
    assert!(preview.changed_samples > 2048);
    assert!(preview.displayed_samples <= 2048);
    assert_eq!(preview.displayed_samples, preview.lines.len());
    assert!(
        preview
            .lines
            .iter()
            .all(|[before, after, _]| after[1] < before[1])
    );
    assert_eq!(document.content_hash().unwrap(), hash);
}
