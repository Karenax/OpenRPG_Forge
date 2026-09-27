use forge_project::{
    TerrainHeightDelta, TerrainMaterialOverride, TerrainMaterialWeight, authoring::new_terrain,
};
use forge_world::{TerrainGenerationPlan, TerrainPartialUpdateRequest};

#[test]
fn material_partial_matches_full_without_touching_collisions_and_survives_relief() {
    let source = new_terrain("paint", "world", "Peinture").unwrap();
    let source_plan = TerrainGenerationPlan::from_document(&source).unwrap();
    let mut cache = source_plan.generate().unwrap();
    let baseline = cache.clone();
    let mut target = source.clone();
    target.schema_version = 3;
    target.revision = target.revision.next().unwrap();
    target.material_overrides = vec![TerrainMaterialOverride {
        sample_x: 32,
        sample_z: 32,
        weights: vec![TerrainMaterialWeight {
            layer_id: target.material_layers[1].id.clone(),
            weight: u16::MAX,
        }],
    }];
    let update = source_plan
        .apply_authoring_update(
            &cache,
            TerrainPartialUpdateRequest {
                base_revision: source.revision,
                base_document_hash: source_plan.document_hash(),
                target_document: target.clone(),
            },
        )
        .unwrap();
    assert_eq!(
        update.mesh_chunks().len(),
        4,
        "shared corner touches four chunks"
    );
    assert!(update.collider_chunks().is_empty());
    update.apply_to_cache(&mut cache).unwrap();
    let plan = TerrainGenerationPlan::from_document(&target).unwrap();
    assert_eq!(cache, plan.generate().unwrap());
    for (before, after) in baseline.chunks.iter().zip(&cache.chunks) {
        assert_eq!(before.positions_m, after.positions_m);
        assert_eq!(before.normals, after.normals);
        assert_eq!(before.collider, after.collider);
        assert_eq!(before.indices, after.indices);
    }
    let mut elevated = target.clone();
    elevated.revision = elevated.revision.next().unwrap();
    elevated.height_deltas = vec![TerrainHeightDelta::new(32, 32, 5.0)];
    plan.apply_authoring_update(
        &cache,
        TerrainPartialUpdateRequest {
            base_revision: target.revision,
            base_document_hash: plan.document_hash(),
            target_document: elevated.clone(),
        },
    )
    .unwrap()
    .apply_to_cache(&mut cache)
    .unwrap();
    let final_plan = TerrainGenerationPlan::from_document(&elevated).unwrap();
    assert_eq!(cache, final_plan.generate().unwrap());
    let weights = final_plan
        .surface_at(-192.0, -192.0)
        .unwrap()
        .material_weights;
    assert_eq!(weights.earth.to_bits(), 1.0_f32.to_bits());
    assert_eq!(weights.grass.to_bits(), 0.0_f32.to_bits());
    assert_eq!(weights.rock.to_bits(), 0.0_f32.to_bits());
    // Removing the correction is an explicit operation, not a side effect of relief.
    let mut erased = elevated.clone();
    erased.material_overrides.clear();
    erased.revision = erased.revision.next().unwrap();
    let update = final_plan
        .apply_authoring_update(
            &cache,
            TerrainPartialUpdateRequest {
                base_revision: elevated.revision,
                base_document_hash: final_plan.document_hash(),
                target_document: erased.clone(),
            },
        )
        .unwrap();
    assert!(update.collider_chunks().is_empty());
    update.apply_to_cache(&mut cache).unwrap();
    assert_eq!(
        cache,
        TerrainGenerationPlan::from_document(&erased)
            .unwrap()
            .generate()
            .unwrap()
    );
}
