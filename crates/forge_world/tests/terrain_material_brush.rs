use forge_project::{authoring::new_terrain, terrain_edit::TerrainEdit};
use forge_world::terrain_material_brush::{MaterialBrushOperation, TerrainMaterialBrushStroke};

#[test]
fn paint_is_deterministic_local_normalized_and_erasable() {
    let mut source = new_terrain("brush", "world", "Paint").unwrap();
    source.schema_version = 3;
    let original = source.clone();
    let stroke = TerrainMaterialBrushStroke {
        radius_m: 8.0,
        strength: 0.5,
        points: vec![[0.0, 0.0], [8.0, 0.0]],
        operation: MaterialBrushOperation::Paint(source.material_layers[1].id.clone()),
    };
    let edit = stroke.prepare(&source).unwrap();
    let preview = forge_world::terrain_brush_preview::TerrainBrushPreview::prepare(&source, &edit)
        .unwrap()
        .unwrap();
    assert_eq!(preview.source_hash, source.content_hash().unwrap());
    assert!(preview.lines.is_empty());
    assert!(!preview.material_marks.is_empty());
    assert_eq!(preview.changed_samples, preview.material_marks.len());
    assert!(
        preview
            .material_marks
            .iter()
            .all(|(position, weights)| position.iter().chain(weights).all(|v| v.is_finite()))
    );
    assert_eq!(edit, stroke.prepare(&source).unwrap());
    let mut subdivided = stroke.clone();
    subdivided.points = vec![[0.0, 0.0], [4.0, 0.0], [8.0, 0.0]];
    assert_eq!(edit, subdivided.prepare(&source).unwrap());
    assert_eq!(source, original);
    let TerrainEdit::SetMaterialOverrides { samples } = edit else {
        panic!("material edit");
    };
    assert!(!samples.is_empty());
    for sample in &samples {
        assert_eq!(
            sample
                .weights
                .iter()
                .map(|w| u32::from(w.weight))
                .sum::<u32>(),
            65535
        );
        assert!(sample.sample_x >= 125 && sample.sample_x <= 135);
        assert!(sample.sample_z >= 125 && sample.sample_z <= 131);
    }
    source.material_overrides = samples;
    let erase = TerrainMaterialBrushStroke {
        operation: MaterialBrushOperation::Erase,
        ..stroke
    };
    let TerrainEdit::SetMaterialOverrides { samples } = erase.prepare(&source).unwrap() else {
        panic!("material edit");
    };
    assert!(samples.is_empty());
}

#[test]
fn migration_and_invalid_parameters_are_not_silently_accepted() {
    let mut source = new_terrain("brush", "world", "Paint").unwrap();
    let mut stroke = TerrainMaterialBrushStroke {
        radius_m: 8.0,
        strength: 1.0,
        points: vec![[0.0, 0.0]],
        operation: MaterialBrushOperation::Paint(source.material_layers[2].id.clone()),
    };
    assert!(stroke.prepare(&source).is_err());
    source.schema_version = 3;
    let TerrainEdit::SetMaterialOverrides { samples } = stroke.prepare(&source).unwrap() else {
        panic!("material edit");
    };
    let centre = samples
        .iter()
        .find(|s| s.sample_x == 128 && s.sample_z == 128)
        .unwrap();
    assert_eq!(centre.weights.len(), 1);
    assert_eq!(centre.weights[0].weight, 65535);
    stroke.strength = f32::NAN;
    assert!(stroke.prepare(&source).is_err());
    stroke.strength = 1.0;
    stroke.points = vec![[f32::INFINITY, 0.0]];
    assert!(stroke.prepare(&source).is_err());
    assert!(source.material_overrides.is_empty());
}
