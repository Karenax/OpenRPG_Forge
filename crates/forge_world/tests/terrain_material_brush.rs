use forge_project::{authoring::new_terrain, terrain_edit::TerrainEdit};
use forge_world::terrain_material_brush::{MaterialBrushOperation, TerrainMaterialBrushStroke};

fn sampled(dimensions: [u32; 2], spacing: f32) -> forge_world::sampled_terrain::SampledTerrainPlan {
    let mut source = new_terrain("brush", "raster", "Raster paint").unwrap();
    let bytes = vec![0; dimensions[0] as usize * dimensions[1] as usize * 4];
    source.schema_version = 4;
    source.extent_m.size_x_m = f32::from(u16::try_from(dimensions[0]).unwrap()) * spacing;
    source.extent_m.size_z_m = f32::from(u16::try_from(dimensions[1]).unwrap()) * spacing;
    source.height_sampling.algorithm = forge_project::HeightSamplingAlgorithm::SampledRasterV1;
    source.height_sampling.sample_spacing_m = spacing;
    source.height_sampling.base_frequency_cycles_per_m = 1.0;
    source.sampled_source = Some(forge_project::sampled_terrain::SampledTerrainSource {
        schema: forge_project::sampled_terrain::SAMPLED_TERRAIN_SCHEMA.into(),
        schema_version: 1,
        width: dimensions[0],
        height: dimensions[1],
        origin_xz_m: [
            -f64::from(source.extent_m.size_x_m) * 0.5,
            -f64::from(source.extent_m.size_z_m) * 0.5,
        ],
        sample_spacing_m: f64::from(spacing),
        outside_height_m: 0.0,
        encoding: forge_project::sampled_terrain::HeightEncoding::Float32Le,
        samples_sha256: forge_core::bytes_sha256(&bytes),
    });
    forge_world::sampled_terrain::SampledTerrainPlan::from_document(&source, &bytes).unwrap()
}

#[test]
fn raster_paint_uses_its_actual_grid_and_previews_and_erases_without_mutating_the_source() {
    let plan = sampled([1200, 40], 1.0);
    let source = plan.document().clone();
    let original_hash = plan.document_hash();
    let source_pin = source.sampled_source.clone();
    let stroke = TerrainMaterialBrushStroke {
        radius_m: 4.0,
        strength: 1.0,
        points: vec![[500.0, 0.0], [506.0, 0.0]],
        operation: MaterialBrushOperation::Paint(source.material_layers[2].id.clone()),
    };
    assert!(
        stroke.prepare(&source).is_err(),
        "raster bytes must never be replaced by procedural evaluation"
    );
    let edit = stroke.prepare_sampled(&plan).unwrap();
    let mut split = stroke.clone();
    split.points = vec![[500.0, 0.0], [502.0, 0.0], [506.0, 0.0]];
    assert_eq!(edit, split.prepare_sampled(&plan).unwrap());
    assert_eq!(plan.document(), &source);
    assert_eq!(plan.document_hash(), original_hash);
    let TerrainEdit::SetMaterialOverrides { samples } = &edit else {
        panic!("paint edit")
    };
    assert!(!samples.is_empty());
    assert!(samples.iter().all(|sample| {
        (1097..=1109).contains(&sample.sample_x)
            && (17..=23).contains(&sample.sample_z)
            && sample
                .weights
                .iter()
                .map(|w| u32::from(w.weight))
                .sum::<u32>()
                == 65535
    }));
    let centre = samples
        .iter()
        .find(|s| s.sample_x == 1100 && s.sample_z == 20)
        .unwrap();
    assert_eq!(centre.weights.len(), 1);
    assert_eq!(centre.weights[0].layer_id, source.material_layers[2].id);
    assert_eq!(centre.weights[0].weight, u16::MAX);
    let preview =
        forge_world::terrain_brush_preview::TerrainBrushPreview::prepare_sampled(&plan, &edit)
            .unwrap()
            .unwrap();
    assert_eq!(preview.source_hash, original_hash);
    assert_eq!(preview.changed_samples, samples.len());
    assert_eq!(preview.material_marks.len(), samples.len());
    assert!(preview.lines.is_empty());
    assert!(
        preview
            .material_marks
            .iter()
            .any(|(position, weights)| position.map(f32::to_bits)
                == [500.0_f32, 0.0, 0.0].map(f32::to_bits)
                && weights.map(f32::to_bits) == [0.0_f32, 0.0, 1.0].map(f32::to_bits))
    );
    let mut painted = source.clone();
    painted.material_overrides.clone_from(samples);
    let painted = plan.with_document(&painted).unwrap();
    let erased = TerrainMaterialBrushStroke {
        operation: MaterialBrushOperation::Erase,
        ..stroke
    }
    .prepare_sampled(&painted)
    .unwrap();
    let TerrainEdit::SetMaterialOverrides {
        samples: erased_samples,
    } = &erased
    else {
        panic!("erase edit")
    };
    assert!(erased_samples.is_empty());
    let erasure =
        forge_world::terrain_brush_preview::TerrainBrushPreview::prepare_sampled(&painted, &erased)
            .unwrap()
            .unwrap();
    assert_eq!(erasure.changed_samples, samples.len());
    assert!(!erasure.material_marks.is_empty());
    assert_eq!(painted.document().sampled_source, source_pin);
    assert_eq!(plan.document(), &source);
}

#[test]
fn raster_material_brush_respects_fractional_spacing_and_rejects_invalid_work() {
    let plan = sampled([64, 64], 0.5);
    let mut stroke = TerrainMaterialBrushStroke {
        radius_m: 2.0,
        strength: 1.0,
        points: vec![[0.5, -0.5]],
        operation: MaterialBrushOperation::Paint(plan.document().material_layers[1].id.clone()),
    };
    let TerrainEdit::SetMaterialOverrides { samples } = stroke.prepare_sampled(&plan).unwrap()
    else {
        panic!("paint edit")
    };
    let centre = samples
        .iter()
        .find(|s| s.sample_x == 33 && s.sample_z == 31)
        .unwrap();
    assert_eq!(centre.weights.len(), 1);
    assert_eq!(centre.weights[0].weight, u16::MAX);
    stroke.strength = f32::NAN;
    assert!(stroke.prepare_sampled(&plan).is_err());
    stroke.strength = 1.0;
    stroke.points = vec![[40.0, 0.0]];
    assert!(stroke.prepare_sampled(&plan).is_err());
    let large = sampled([1200, 256], 1.0);
    stroke.radius_m = 128.0;
    stroke.points = vec![[-400.0, 0.0], [400.0, 0.0], [-400.0, 0.0], [400.0, 0.0]];
    assert!(
        stroke
            .prepare_sampled(&large)
            .unwrap_err()
            .contains("trop d’échantillons")
    );
    assert!(large.document().material_overrides.is_empty());
}

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
    let mut version_four = source.clone();
    version_four.schema_version = 4;
    assert_eq!(edit, stroke.prepare(&version_four).unwrap());
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
