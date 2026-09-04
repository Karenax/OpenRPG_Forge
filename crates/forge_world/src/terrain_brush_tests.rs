use super::*;
use crate::TerrainPartialUpdateRequest;
use forge_core::Vec3;
use forge_project::NewProjectCommand;

fn source() -> WorkspaceDocument {
    let mut source = WorkspaceDocument::create(NewProjectCommand {
        command_id: CommandId::deterministic("create-brush-test"),
        project_id: ProjectId::deterministic("brush-project"),
        module_id: ModuleId::deterministic("brush-module"),
        area_id: AreaId::deterministic("brush-area"),
        project_name: "Brush project".into(),
        module_name: "Module".into(),
        area_name: "Terrain".into(),
        seed: u64::MAX,
        spawn: Vec3::new(0.0, 2.0, 0.0),
    })
    .unwrap()
    .document;
    source.project.modules[0].areas[0]
        .terrain
        .height_sampling
        .amplitude_m = 0.0;
    source.project.modules[0].areas[0]
        .terrain
        .height_sampling
        .base_elevation_m = 0.0;
    source
}

#[test]
fn continuous_stroke_is_one_revision_and_rejects_an_invalid_tail_atomically() {
    let source = source();
    let command = command(&source, TerrainBrushOperation::RaiseLower { delta_m: 2.0 });
    let result = preview_terrain_stroke(&source, command, &[[8.0, 0.0], [16.0, 0.0]]).unwrap();
    assert_eq!(
        result.edit.document.revision.get(),
        source.revision.get() + 1
    );
    assert_eq!(result.edit.receipt.inverse_snapshot.as_ref(), &source);
    assert_eq!(result.additional_centers.len(), 2);
    let terrain = plan(&result.edit.document);
    for x in [0.0, 8.0, 16.0] {
        assert!((terrain.height_at(x, 0.0).unwrap() - 2.0).abs() < 1e-6);
    }
    assert!(preview_terrain_stroke(&source, command, &[[8.0, 0.0], [f32::NAN, 0.0]]).is_err());
    assert!(preview_terrain_stroke(&source, command, &[[8.0, 0.0]; 64]).is_err());
    assert!(
        source.project.modules[0].areas[0]
            .terrain
            .height_deltas
            .is_empty()
    );
}

fn command(source: &WorkspaceDocument, operation: TerrainBrushOperation) -> TerrainBrushCommand {
    TerrainBrushCommand {
        command_id: CommandId::deterministic("brush-command"),
        base_revision: source.revision,
        project_id: source.project.id,
        module_id: source.project.modules[0].id,
        area_id: source.project.modules[0].areas[0].id,
        brush: TerrainBrush {
            schema_version: 1,
            center_x_m: 0.0,
            center_z_m: 0.0,
            radius_m: 8.0,
            strength: 1.0,
            operation,
        },
    }
}

fn plan(source: &WorkspaceDocument) -> TerrainGenerationPlan {
    TerrainGenerationPlan::from_document(&source.project.modules[0].areas[0].terrain).unwrap()
}

#[test]
fn raise_lower_has_exact_falloff_and_keeps_source_and_outside_immutable() {
    let source = source();
    let before = source.to_pretty_json().unwrap();
    for delta_m in [8.0, -8.0] {
        let result = preview_terrain_brush(
            &source,
            command(&source, TerrainBrushOperation::RaiseLower { delta_m }),
        )
        .unwrap();
        assert_eq!(result.changed_samples, 45);
        let projected = plan(&result.edit.document);
        assert_eq!(
            projected.height_at(0.0, 0.0).unwrap().to_bits(),
            delta_m.to_bits()
        );
        assert_eq!(
            projected.height_at(4.0, 0.0).unwrap().to_bits(),
            (delta_m * 0.5).to_bits()
        );
        assert_eq!(
            projected.height_at(8.0, 0.0).unwrap().to_bits(),
            0.0_f32.to_bits()
        );
        assert_eq!(result.edit.receipt.inverse_snapshot.as_ref(), &source);
        assert_eq!(
            result.edit.receipt.before_hash,
            source.content_hash().unwrap()
        );
        assert_ne!(
            result.edit.receipt.before_hash,
            result.edit.receipt.after_hash
        );
        let restored =
            WorkspaceDocument::from_json(&result.edit.document.to_pretty_json().unwrap()).unwrap();
        assert_eq!(restored, result.edit.document);
        assert_eq!(source.to_pretty_json().unwrap(), before);
    }
}

#[test]
fn smooth_uses_original_neighbours_without_directional_feedback() {
    let mut source = source();
    source.project.modules[0].areas[0].terrain.height_deltas =
        vec![TerrainHeightDelta::new(128, 128, 9.0)];
    let result =
        preview_terrain_brush(&source, command(&source, TerrainBrushOperation::Smooth)).unwrap();
    let output = plan(&result.edit.document);
    assert!((output.height_at(0.0, 0.0).unwrap() - 1.0).abs() < 1e-6);
    assert!(output.height_at(2.0, 0.0).unwrap() > 0.0);
    assert_eq!(
        output.height_at(-2.0, 0.0).unwrap().to_bits(),
        output.height_at(2.0, 0.0).unwrap().to_bits()
    );
    assert_eq!(
        output.height_at(0.0, -2.0).unwrap().to_bits(),
        output.height_at(0.0, 2.0).unwrap().to_bits()
    );
    assert!(
        preview_terrain_brush(
            &self::source(),
            command(&self::source(), TerrainBrushOperation::Smooth)
        )
        .is_err()
    );
}

#[test]
fn flatten_and_slope_blend_toward_explicit_absolute_targets() {
    let source = source();
    let flattened = preview_terrain_brush(
        &source,
        command(&source, TerrainBrushOperation::Flatten { height_m: 12.0 }),
    )
    .unwrap();
    assert!((plan(&flattened.edit.document).height_at(0.0, 0.0).unwrap() - 12.0).abs() < 1e-6);
    let sloped = preview_terrain_brush(
        &source,
        command(
            &source,
            TerrainBrushOperation::Slope {
                height_m: 10.0,
                gradient_x: 1.0,
                gradient_z: -0.5,
            },
        ),
    )
    .unwrap();
    let output = plan(&sloped.edit.document);
    assert!((output.height_at(4.0, 0.0).unwrap() - 7.0).abs() < 1e-6);
    assert!((output.height_at(0.0, 4.0).unwrap() - 4.0).abs() < 1e-6);
    let mut request = command(&source, TerrainBrushOperation::Flatten { height_m: 12.0 });
    request.brush.strength = 0.25;
    assert!(
        (plan(
            &preview_terrain_brush(&source, request)
                .unwrap()
                .edit
                .document
        )
        .height_at(0.0, 0.0)
        .unwrap()
            - 3.0)
            .abs()
            < 1e-6
    );
}

#[test]
fn noise_is_seeded_deterministic_and_bounded() {
    let source = source();
    let request = command(
        &source,
        TerrainBrushOperation::Noise {
            amplitude_m: 4.0,
            seed: u32::MAX,
        },
    );
    let first = preview_terrain_brush(&source, request).unwrap();
    assert_eq!(first, preview_terrain_brush(&source, request).unwrap());
    assert!(
        first.edit.document.project.modules[0].areas[0]
            .terrain
            .height_deltas
            .iter()
            .all(|sample| sample.delta_m.abs() <= 4.0)
    );
    let other = preview_terrain_brush(
        &source,
        command(
            &source,
            TerrainBrushOperation::Noise {
                amplitude_m: 4.0,
                seed: 1,
            },
        ),
    )
    .unwrap();
    assert_ne!(first.edit.receipt.after_hash, other.edit.receipt.after_hash);
}

#[test]
fn every_mode_matches_full_generation_after_partial_cache_replacement() {
    let mut source = source();
    source.project.modules[0].areas[0]
        .terrain
        .height_sampling
        .amplitude_m = 12.0;
    for operation in [
        TerrainBrushOperation::RaiseLower { delta_m: 8.0 },
        TerrainBrushOperation::RaiseLower { delta_m: -8.0 },
        TerrainBrushOperation::Smooth,
        TerrainBrushOperation::Flatten { height_m: 8.0 },
        TerrainBrushOperation::Noise {
            amplitude_m: 3.0,
            seed: 7,
        },
        TerrainBrushOperation::Slope {
            height_m: 4.0,
            gradient_x: 0.5,
            gradient_z: 0.0,
        },
    ] {
        let before = plan(&source);
        let mut cache = before.generate().unwrap();
        let untouched = cache.chunks[0].clone();
        let result = preview_terrain_brush(&source, command(&source, operation)).unwrap();
        let target = result.edit.document.project.modules[0].areas[0]
            .terrain
            .clone();
        let update = before
            .apply_partial_update(
                &cache,
                TerrainPartialUpdateRequest {
                    base_revision: before.document().revision,
                    base_document_hash: before.document_hash(),
                    target_document: target.clone(),
                },
            )
            .unwrap();
        assert_eq!(
            update.mesh_chunks().len(),
            4,
            "Stamp crosses the four central chunk seams"
        );
        update.apply_to_cache(&mut cache).unwrap();
        assert_eq!(cache.chunks[0], untouched);
        assert_eq!(
            cache,
            TerrainGenerationPlan::from_document(&target)
                .unwrap()
                .generate()
                .unwrap()
        );
    }
}

#[test]
fn edge_stamps_are_clipped_and_sparse_zero_offsets_are_removed() {
    let mut source = source();
    let mut request = command(&source, TerrainBrushOperation::RaiseLower { delta_m: 8.0 });
    request.brush.center_x_m = -256.0;
    request.brush.center_z_m = -256.0;
    let result = preview_terrain_brush(&source, request).unwrap();
    assert!(result.changed_samples < 45);
    assert!(
        (plan(&result.edit.document)
            .height_at(-256.0, -256.0)
            .unwrap()
            - 8.0)
            .abs()
            < 1e-6
    );
    source.project.modules[0].areas[0].terrain.height_deltas =
        vec![TerrainHeightDelta::new(128, 128, 8.0)];
    let mut request = command(&source, TerrainBrushOperation::Flatten { height_m: 0.0 });
    request.brush.radius_m = 2.0;
    let result = preview_terrain_brush(&source, request).unwrap();
    assert!(
        result.edit.document.project.modules[0].areas[0]
            .terrain
            .height_deltas
            .is_empty()
    );
}

#[test]
fn invalid_stale_foreign_exhausted_and_height_overflow_requests_fail_without_mutation() {
    let source = source();
    let original = source.content_hash().unwrap();
    for value in [f32::NAN, f32::INFINITY, -1.0, 0.0, 1.0, 65.0] {
        let mut request = command(&source, TerrainBrushOperation::Smooth);
        request.brush.radius_m = value;
        assert!(preview_terrain_brush(&source, request).is_err());
    }
    for mutation in 0..7 {
        let mut request = command(&source, TerrainBrushOperation::RaiseLower { delta_m: 8.0 });
        match mutation {
            0 => request.brush.schema_version = 2,
            1 => request.base_revision = Revision::new(12),
            2 => request.project_id = ProjectId::deterministic("foreign"),
            3 => request.area_id = AreaId::deterministic("foreign"),
            4 => request.module_id = ModuleId::deterministic("foreign"),
            5 => request.brush.strength = 1.01,
            _ => request.brush.center_x_m = f32::NEG_INFINITY,
        }
        assert!(preview_terrain_brush(&source, request).is_err());
    }
    assert_eq!(source.content_hash().unwrap(), original);
    let mut high = source.clone();
    high.project.modules[0].areas[0]
        .terrain
        .height_sampling
        .base_elevation_m = 1024.0;
    assert!(matches!(
        preview_terrain_brush(
            &high,
            command(&high, TerrainBrushOperation::RaiseLower { delta_m: 1.0 })
        ),
        Err(TerrainBrushError::HeightOutOfBounds)
    ));
    let mut exhausted = source.clone();
    exhausted.revision = Revision::new(u64::MAX);
    assert!(
        preview_terrain_brush(
            &exhausted,
            command(
                &exhausted,
                TerrainBrushOperation::RaiseLower { delta_m: 1.0 }
            )
        )
        .is_err()
    );
}

#[test]
fn workspace_revision_can_advance_independently_of_terrain_and_other_areas_stay_intact() {
    let mut source = source();
    let mut other = source.project.modules[0].areas[0].clone();
    other.id = AreaId::deterministic("other-area");
    other.terrain.area_id = other.id;
    source.project.modules[0].areas.push(other.clone());
    source.revision = Revision::new(9);
    let result = preview_terrain_brush(
        &source,
        command(&source, TerrainBrushOperation::RaiseLower { delta_m: 2.0 }),
    )
    .unwrap();
    assert_eq!(result.edit.document.revision.get(), 10);
    assert_eq!(
        result.edit.document.project.modules[0].areas[0]
            .terrain
            .revision
            .get(),
        10
    );
    assert_eq!(result.edit.document.project.modules[0].areas[1], other);
    let mut forged = result.edit.receipt.command.clone();
    if let WorkspaceChange::SetTerrainHeightDeltas {
        expected_terrain_hash,
        ..
    } = &mut forged.change
    {
        *expected_terrain_hash = result.edit.receipt.after_hash;
    }
    assert!(source.preview_edit(forged).is_err());
}
