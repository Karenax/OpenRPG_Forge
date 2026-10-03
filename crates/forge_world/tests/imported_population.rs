use forge_core::bytes_sha256;
use forge_project::{
    VegetationDocument,
    imported_vegetation::{
        ImportedInstanceEncoding, ImportedVegetationBlock, ImportedVegetationSource,
    },
};
use forge_world::{
    VegetationGenerationPlan, embedded_visual_valley_terrain_document,
    imported_vegetation::ImportedPopulation,
};

fn fixture() -> (VegetationDocument, Vec<u8>) {
    let mut doc = VegetationDocument::from_json(include_str!(
        "../../../examples/visual_valley/vegetation.v1.json"
    ))
    .unwrap();
    doc.schema_version = 2;
    doc.layers.truncate(1);
    let mut bytes = Vec::new();
    for (key, x, z) in [
        (1_u8, -256.0_f64, -256.0_f64),
        (2, 256.0, 256.0),
        (3, -192.0, -192.0),
    ] {
        bytes.extend([key; 32]);
        for value in [x, 5.0, z, 0.0, 0.0, 0.0, 1.0, 1.0, 1.0, 1.0] {
            bytes.extend(value.to_le_bytes());
        }
    }
    doc.imported_source = Some(ImportedVegetationSource {
        schema: "forge.imported-vegetation".into(),
        version: 1,
        blocks: vec![ImportedVegetationBlock {
            layer_id: doc.layers[0].id.to_string(),
            prototype_key: "trees".into(),
            count: 3,
            encoding: ImportedInstanceEncoding::TransformF64LeV1,
            instances_sha256: bytes_sha256(&bytes),
            parameter_names: vec![],
        }],
    });
    (doc, bytes)
}

#[test]
#[allow(
    clippy::float_cmp,
    clippy::too_many_lines,
    reason = "This integration test keeps the catalogue placement contract in one trace; fixture coordinates are exact."
)]
fn imported_plant_catalogue_places_nearest_source_at_world_position() {
    let (doc, bytes) = fixture();
    let population = ImportedPopulation::resolve(&doc, |_, _| Ok(bytes.clone())).unwrap();
    let entries = population.plant_catalogue();
    assert_eq!(entries.len(), 1);
    assert_eq!(entries[0].source_count, 3);
    assert_eq!(entries[0].prototype_key, "trees");
    assert_eq!(entries[0].display_name, doc.layers[0].display_name);
    let placed = population
        .plant_placement_at(
            &entries[0].layer_id,
            forge_core::EntityId::deterministic("catalogue-tree"),
            [-160.0, -192.0],
            1.0,
            Some(45.0),
            Some(1.25),
        )
        .unwrap();
    assert_eq!(
        placed.source_key,
        population.layers()[0].instances()[2].source.source_key
    );
    assert_eq!(placed.translation_m, [32.0, 1.0, 0.0]);
    assert_eq!(placed.yaw_degrees, Some(45.0));
    assert_eq!(placed.scale_factor, Some(1.25));
    assert!(
        population
            .plant_placement_at(
                &entries[0].layer_id,
                forge_core::EntityId::deterministic("outside-catalogue"),
                [300.0, 0.0],
                0.0,
                None,
                None,
            )
            .is_err()
    );
}

#[test]
#[allow(
    clippy::float_cmp,
    clippy::too_many_lines,
    reason = "This integration test keeps the terrain archive/undo/redo contract in one trace; fixture offsets are exact."
)]
fn terrain_projection_preserves_offsets_identity_and_restores_after_undo() {
    use forge_core::CommandId;
    use forge_project::{
        HeightSamplingAlgorithm, TerrainHeightDelta,
        sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
        terrain_edit::{TerrainAction, TerrainEdit},
        zone_authoring::session::ZoneEditSession,
    };
    let (mut doc, bytes) = fixture();
    let mut terrain = embedded_visual_valley_terrain_document().unwrap();
    let heights: Vec<_> = (0..512 * 512).flat_map(|_| 2.0_f32.to_le_bytes()).collect();
    terrain.schema_version = 4;
    terrain.height_sampling.algorithm = HeightSamplingAlgorithm::SampledRasterV1;
    terrain.height_sampling.base_elevation_m = 0.0;
    terrain.height_sampling.sample_spacing_m = 1.0;
    terrain.height_sampling.amplitude_m = 0.0;
    terrain.height_sampling.base_frequency_cycles_per_m = 1.0;
    terrain.height_sampling.octaves = 1;
    terrain.height_sampling.lacunarity = 2.0;
    terrain.height_sampling.persistence = 0.5;
    terrain.sampled_source = Some(SampledTerrainSource {
        schema: SAMPLED_TERRAIN_SCHEMA.into(),
        schema_version: 1,
        width: 512,
        height: 512,
        origin_xz_m: [-256.0; 2],
        sample_spacing_m: 1.0,
        outside_height_m: -90.0,
        encoding: HeightEncoding::Float32Le,
        samples_sha256: bytes_sha256(&heights),
    });
    doc.project_id = terrain.project_id;
    doc.area_id = terrain.area_id;
    doc.layers[0].material_filter = None;
    let mut zone = ZoneEditSession::from_initial_terrain(terrain).unwrap();
    let mut base = zone.head().base.clone();
    base.vegetation = Some(doc.clone());
    let request = zone
        .head()
        .prepare_regeneration(base, CommandId::deterministic("terrain-import"))
        .unwrap();
    zone.execute(&request).unwrap();
    let population = ImportedPopulation::resolve(&doc, |_, _| Ok(bytes.clone())).unwrap();
    let original = population.layers()[0].instances();
    assert_eq!(
        population
            .terrain_view(zone.head(), &heights)
            .unwrap()
            .layers[0]
            .instances,
        original
    );
    let request = zone
        .head()
        .prepare_terrain_action(
            TerrainAction::Edit(TerrainEdit::SetHeightDeltas {
                samples: vec![TerrainHeightDelta::new(64, 64, 4.0)],
            }),
            CommandId::deterministic("raise-local"),
        )
        .unwrap();
    zone.execute(&request).unwrap();
    let restored = ZoneEditSession::from_archive_bytes(&zone.archive_bytes().unwrap()).unwrap();
    let projected = population.terrain_view(restored.head(), &heights).unwrap();
    assert_eq!(projected.moved_count, 1);
    for (old, new) in original.iter().zip(&projected.layers[0].instances) {
        let mut expected = old.clone();
        if old.source.position_m[0] == -192.0 {
            expected.source.position_m[1] = 9.0;
        }
        assert_eq!(new, &expected);
    }
    let undo = zone
        .head()
        .prepare_terrain_action(TerrainAction::Undo, CommandId::deterministic("undo-local"))
        .unwrap();
    zone.execute(&undo).unwrap();
    assert_eq!(
        population
            .terrain_view(zone.head(), &heights)
            .unwrap()
            .layers[0]
            .instances,
        original
    );
    let raise = zone
        .head()
        .prepare_terrain_action(
            TerrainAction::Edit(TerrainEdit::SetBaseElevation { metres: 3.0 }),
            CommandId::deterministic("raise-all"),
        )
        .unwrap();
    zone.execute(&raise).unwrap();
    let projected = population.terrain_view(zone.head(), &heights).unwrap();
    assert_eq!(projected.moved_count, 3);
    assert!(
        projected.layers[0]
            .instances
            .iter()
            .all(|p| p.source.position_m[1] == 8.0)
    );
    assert!(original.iter().all(|p| p.source.position_m[1] == 5.0));
    let mut corrupt = heights;
    corrupt[0] ^= 1;
    assert!(population.terrain_view(zone.head(), &corrupt).is_err());
}

#[test]
#[allow(
    clippy::float_cmp,
    clippy::too_many_lines,
    reason = "This integration test keeps the imported-plant archive/undo/redo contract in one trace; fixture offsets are exact."
)]
fn imported_plant_offset_survives_archive_undo_redo_and_terrain_change() {
    use forge_core::{CommandId, EntityId};
    use forge_project::{
        HeightSamplingAlgorithm,
        sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
        terrain_edit::{TerrainAction, TerrainEdit},
        zone_authoring::{
            imported_plants::{ImportedPlantEdit, ImportedPlantOffset, ImportedPlantPlacementEdit},
            session::{ZoneAction, ZoneEditSession, ZoneRequest},
        },
    };
    let (mut doc, bytes) = fixture();
    let mut terrain = embedded_visual_valley_terrain_document().unwrap();
    let heights: Vec<_> = (0..512 * 512).flat_map(|_| 2.0_f32.to_le_bytes()).collect();
    terrain.schema_version = 4;
    terrain.height_sampling.algorithm = HeightSamplingAlgorithm::SampledRasterV1;
    terrain.height_sampling.base_elevation_m = 0.0;
    terrain.height_sampling.sample_spacing_m = 1.0;
    terrain.height_sampling.amplitude_m = 0.0;
    terrain.height_sampling.base_frequency_cycles_per_m = 1.0;
    terrain.height_sampling.octaves = 1;
    terrain.height_sampling.lacunarity = 2.0;
    terrain.height_sampling.persistence = 0.5;
    terrain.sampled_source = Some(SampledTerrainSource {
        schema: SAMPLED_TERRAIN_SCHEMA.into(),
        schema_version: 1,
        width: 512,
        height: 512,
        origin_xz_m: [-256.0; 2],
        sample_spacing_m: 1.0,
        outside_height_m: -90.0,
        encoding: HeightEncoding::Float32Le,
        samples_sha256: bytes_sha256(&heights),
    });
    doc.project_id = terrain.project_id;
    doc.area_id = terrain.area_id;
    doc.layers[0].material_filter = None;
    let mut zone = ZoneEditSession::from_initial_terrain(terrain).unwrap();
    let mut base = zone.head().base.clone();
    base.vegetation = Some(doc.clone());
    let request = zone
        .head()
        .prepare_regeneration(base, CommandId::deterministic("offset-import"))
        .unwrap();
    zone.execute(&request).unwrap();
    let population = ImportedPopulation::resolve(&doc, |_, _| Ok(bytes.clone())).unwrap();
    let original = population.layers()[0].instances()[2].clone();
    let offset = ImportedPlantOffset {
        layer_id: doc.layers[0].id.clone(),
        source_key: original.source.source_key,
        translation_m: [16.0, 1.0, 0.0],
    };
    let request = zone
        .head()
        .prepare_imported_plant_edit(
            ImportedPlantEdit::SetOffset {
                offset: offset.clone(),
            },
            CommandId::deterministic("move-imported-plant"),
        )
        .unwrap();
    assert_eq!(zone.head().version, 1);
    assert!(zone.head().corrections.imported_plant_offsets.is_none());
    let preview = zone.preview(&request).unwrap();
    assert_eq!(
        preview.applied.imported_plant_offsets.as_deref(),
        Some(&[offset.clone()][..])
    );
    assert!(zone.head().corrections.imported_plant_offsets.is_none());
    zone.execute(&request).unwrap();
    assert_eq!(zone.head().version, 10);
    let archive = zone.archive_bytes().unwrap();
    let mut reopened = ZoneEditSession::from_archive_bytes(&archive).unwrap();
    let projected = population.terrain_view(reopened.head(), &heights).unwrap();
    assert_eq!(projected.moved_count, 1);
    assert_eq!(projected.layers[0].instances[2].id, original.id);
    assert_eq!(
        projected.layers[0].instances[2].source.position_m,
        [-176.0, 6.0, -192.0]
    );
    assert_eq!(original.source.position_m, [-192.0, 5.0, -192.0]);
    let raise = reopened
        .head()
        .prepare_terrain_action(
            TerrainAction::Edit(TerrainEdit::SetBaseElevation { metres: 3.0 }),
            CommandId::deterministic("raise-offset-plant"),
        )
        .unwrap();
    reopened.execute(&raise).unwrap();
    let projected = population.terrain_view(reopened.head(), &heights).unwrap();
    assert_eq!(
        projected.layers[0].instances[2].source.position_m,
        [-176.0, 9.0, -192.0]
    );
    let undo_terrain = ZoneRequest {
        command_id: CommandId::deterministic("undo-offset-terrain"),
        expected_revision: reopened.head().revision,
        expected_hash: reopened.head().content_hash().unwrap(),
        action: ZoneAction::Undo,
    };
    reopened.execute(&undo_terrain).unwrap();
    let undo_plant = ZoneRequest {
        command_id: CommandId::deterministic("undo-offset-plant"),
        expected_revision: reopened.head().revision,
        expected_hash: reopened.head().content_hash().unwrap(),
        action: ZoneAction::Undo,
    };
    reopened.execute(&undo_plant).unwrap();
    assert_eq!(reopened.head().version, 1);
    assert_eq!(
        population
            .terrain_view(reopened.head(), &heights)
            .unwrap()
            .layers[0]
            .instances[2],
        original
    );
    let redo = ZoneRequest {
        command_id: CommandId::deterministic("redo-offset-plant"),
        expected_revision: reopened.head().revision,
        expected_hash: reopened.head().content_hash().unwrap(),
        action: ZoneAction::Redo,
    };
    reopened.execute(&redo).unwrap();
    assert_eq!(
        population
            .terrain_view(reopened.head(), &heights)
            .unwrap()
            .layers[0]
            .instances[2]
            .source
            .position_m,
        [-176.0, 6.0, -192.0]
    );
    let mut outside = reopened.head().clone();
    outside.corrections.imported_plant_offsets.as_mut().unwrap()[0].translation_m =
        [2000.0, 0.0, 0.0];
    outside.validate().unwrap();
    assert!(population.validate_plant_offsets(&outside).is_err());
    assert!(population.terrain_view(&outside, &heights).is_err());
    let mut missing = reopened.head().clone();
    missing.corrections.imported_plant_offsets.as_mut().unwrap()[0].source_key =
        forge_core::ContentHash::from_bytes([9; 32]);
    missing.validate().unwrap();
    assert!(population.validate_plant_offsets(&missing).is_err());
    assert!(population.terrain_view(&missing, &heights).is_err());
    let mut changed_source = reopened.head().base.clone();
    changed_source
        .vegetation
        .as_mut()
        .unwrap()
        .imported_source
        .as_mut()
        .unwrap()
        .blocks[0]
        .instances_sha256 = forge_core::ContentHash::from_bytes([8; 32]);
    assert!(
        reopened
            .head()
            .review_regeneration(changed_source)
            .unwrap()
            .refusal_message()
            .unwrap()
            .contains("ImportedPlantSourceChanged")
    );
    let mut empty = reopened.head().clone();
    empty.corrections.imported_plant_offsets = Some(Vec::new());
    assert!(empty.validate().is_err());
    let restore = reopened
        .head()
        .prepare_imported_plant_edit(
            ImportedPlantEdit::RestoreSource {
                layer_id: offset.layer_id.clone(),
                source_key: offset.source_key,
            },
            CommandId::deterministic("restore-imported-plant"),
        )
        .unwrap();
    reopened.execute(&restore).unwrap();
    assert!(reopened.head().corrections.imported_plant_offsets.is_none());
    assert_eq!(reopened.head().version, 1);
    assert_eq!(
        population
            .terrain_view(reopened.head(), &heights)
            .unwrap()
            .layers[0]
            .instances[2],
        original
    );
    assert!(
        reopened
            .head()
            .prepare_imported_plant_edit(
                ImportedPlantEdit::SetOffset {
                    offset: ImportedPlantOffset {
                        translation_m: [9000.0, 0.0, 0.0],
                        ..offset
                    }
                },
                CommandId::deterministic("invalid-offset"),
            )
            .is_err()
    );
    let placement = population
        .plant_placement_at(
            &doc.layers[0].id,
            EntityId::deterministic("authored-palm-1"),
            [-160.0, -192.0],
            0.0,
            None,
            None,
        )
        .unwrap();
    assert_eq!(placement.source_key, original.source.source_key);
    let serialized_legacy = serde_json::to_value(&placement).unwrap();
    assert!(serialized_legacy.get("yaw_degrees").is_none());
    assert!(serialized_legacy.get("scale_factor").is_none());
    let insert = reopened
        .head()
        .prepare_imported_plant_placement(
            ImportedPlantPlacementEdit::Insert {
                placement: placement.clone(),
            },
            CommandId::deterministic("insert-authored-plant"),
        )
        .unwrap();
    assert!(
        reopened
            .preview(&insert)
            .unwrap()
            .applied
            .imported_plant_placements
            .is_some()
    );
    reopened.execute(&insert).unwrap();
    assert_eq!(reopened.head().version, 11);
    let archive = reopened.archive_bytes().unwrap();
    let mut reopened = ZoneEditSession::from_archive_bytes(&archive).unwrap();
    assert!(!String::from_utf8_lossy(&archive).contains("yaw_degrees"));
    assert!(!String::from_utf8_lossy(&archive).contains("scale_factor"));
    let projected = population.terrain_view(reopened.head(), &heights).unwrap();
    assert_eq!(projected.placed_count, 1);
    assert_eq!(projected.layers[0].instances.len(), 4);
    assert_eq!(projected.layers[0].instances[3].id, placement.id);
    assert_eq!(
        projected.layers[0].instances[3].source.position_m,
        [-160.0, 5.0, -192.0]
    );
    let appearance = reopened
        .head()
        .prepare_imported_plant_placement(
            ImportedPlantPlacementEdit::SetAppearance {
                id: placement.id,
                yaw_degrees: Some(90.0),
                scale_factor: Some(2.0),
            },
            CommandId::deterministic("appearance-authored-plant"),
        )
        .unwrap();
    reopened.execute(&appearance).unwrap();
    assert_eq!(reopened.head().version, 12);
    let appearance_archive = reopened.archive_bytes().unwrap();
    let mut reopened = ZoneEditSession::from_archive_bytes(&appearance_archive).unwrap();
    let transformed = population.terrain_view(reopened.head(), &heights).unwrap();
    let source = &projected.layers[0].instances[3].source;
    let transformed_source = &transformed.layers[0].instances[3].source;
    assert_eq!(
        transformed_source.scale,
        source.scale.map(|value| value * 2.0)
    );
    assert_ne!(transformed_source.rotation_xyzw, source.rotation_xyzw);
    let expected = std::f64::consts::FRAC_1_SQRT_2;
    for (actual, expected) in transformed_source
        .rotation_xyzw
        .iter()
        .zip([0.0, expected, 0.0, expected])
    {
        assert!((actual - expected).abs() < 1e-12);
    }
    assert_eq!(transformed.layers[0].instances[3].id, placement.id);
    assert!(
        reopened
            .head()
            .prepare_imported_plant_placement(
                ImportedPlantPlacementEdit::SetAppearance {
                    id: placement.id,
                    yaw_degrees: Some(361.0),
                    scale_factor: Some(2.0),
                },
                CommandId::deterministic("invalid-plant-yaw"),
            )
            .is_err()
    );
    assert!(
        reopened
            .head()
            .prepare_imported_plant_placement(
                ImportedPlantPlacementEdit::SetAppearance {
                    id: placement.id,
                    yaw_degrees: Some(90.0),
                    scale_factor: Some(0.0),
                },
                CommandId::deterministic("invalid-plant-scale"),
            )
            .is_err()
    );
    let undo_appearance = ZoneRequest {
        command_id: CommandId::deterministic("undo-authored-plant-appearance"),
        expected_revision: reopened.head().revision,
        expected_hash: reopened.head().content_hash().unwrap(),
        action: ZoneAction::Undo,
    };
    reopened.execute(&undo_appearance).unwrap();
    assert_eq!(reopened.head().version, 11);
    assert_eq!(
        population
            .terrain_view(reopened.head(), &heights)
            .unwrap()
            .layers[0]
            .instances[3]
            .source
            .rotation_xyzw,
        source.rotation_xyzw
    );
    let mut raised = reopened.head().clone();
    raised.corrections.base_elevation_m = Some(3.0);
    assert_eq!(
        population.terrain_view(&raised, &heights).unwrap().layers[0].instances[3]
            .source
            .position_m,
        [-160.0, 8.0, -192.0]
    );
    let mut missing_template = reopened.head().clone();
    missing_template
        .corrections
        .imported_plant_placements
        .as_mut()
        .unwrap()[0]
        .source_key = forge_core::ContentHash::from_bytes([10; 32]);
    missing_template.validate().unwrap();
    assert!(
        population
            .validate_plant_placements(&missing_template)
            .is_err()
    );
    let mut outside_copy = reopened.head().clone();
    outside_copy
        .corrections
        .imported_plant_placements
        .as_mut()
        .unwrap()[0]
        .translation_m = [2000.0, 0.0, 0.0];
    outside_copy.validate().unwrap();
    assert!(population.validate_plant_placements(&outside_copy).is_err());
    let mut changed_template = reopened.head().base.clone();
    changed_template
        .vegetation
        .as_mut()
        .unwrap()
        .imported_source
        .as_mut()
        .unwrap()
        .blocks[0]
        .instances_sha256 = forge_core::ContentHash::from_bytes([11; 32]);
    assert!(
        reopened
            .head()
            .review_regeneration(changed_template)
            .unwrap()
            .refusal_message()
            .unwrap()
            .contains("ImportedPlantSourceChanged")
    );
    let move_request = reopened
        .head()
        .prepare_imported_plant_placement(
            ImportedPlantPlacementEdit::Move {
                id: placement.id,
                translation_m: [48.0, 1.0, 0.0],
            },
            CommandId::deterministic("move-authored-plant"),
        )
        .unwrap();
    reopened.execute(&move_request).unwrap();
    assert_eq!(
        population
            .terrain_view(reopened.head(), &heights)
            .unwrap()
            .layers[0]
            .instances[3]
            .source
            .position_m,
        [-144.0, 6.0, -192.0]
    );
    let undo = ZoneRequest {
        command_id: CommandId::deterministic("undo-authored-plant-move"),
        expected_revision: reopened.head().revision,
        expected_hash: reopened.head().content_hash().unwrap(),
        action: ZoneAction::Undo,
    };
    reopened.execute(&undo).unwrap();
    assert_eq!(
        population
            .terrain_view(reopened.head(), &heights)
            .unwrap()
            .layers[0]
            .instances[3]
            .source
            .position_m,
        [-160.0, 5.0, -192.0]
    );
    let remove = reopened
        .head()
        .prepare_imported_plant_placement(
            ImportedPlantPlacementEdit::Remove { id: placement.id },
            CommandId::deterministic("remove-authored-plant"),
        )
        .unwrap();
    reopened.execute(&remove).unwrap();
    assert!(
        reopened
            .head()
            .corrections
            .imported_plant_placements
            .is_none()
    );
    assert_eq!(reopened.head().version, 1);
    assert_eq!(
        population
            .terrain_view(reopened.head(), &heights)
            .unwrap()
            .placed_count,
        0
    );
}

#[test]
#[allow(clippy::float_cmp)] // Exact imported boundary coordinates.
fn boundary_instances_have_one_chunk_and_keep_exact_transforms() {
    let (doc, bytes) = fixture();
    let population = ImportedPopulation::resolve(&doc, |hash, length| {
        assert_eq!(hash, bytes_sha256(&bytes));
        assert_eq!(length, bytes.len());
        Ok(bytes.clone())
    })
    .unwrap();
    assert_eq!(population.instance_count(), 3);
    let layer = &population.layers()[0];
    assert_eq!(
        layer.chunk([0, 0]).next().unwrap().source.position_m,
        [-256.0, 5.0, -256.0]
    );
    assert_eq!(
        layer.chunk([7, 7]).next().unwrap().source.position_m,
        [256.0, 5.0, 256.0]
    );
    assert_eq!(layer.chunk([1, 1]).count(), 1);
    assert_eq!(layer.chunk([8, 8]).count(), 0);
    assert_eq!(population.document(), &doc);
}

#[test]
fn missing_or_corrupt_source_never_falls_back_to_procedural_generation() {
    let (doc, mut bytes) = fixture();
    assert!(ImportedPopulation::resolve(&doc, |_, _| Err("missing blob".into())).is_err());
    bytes[35] ^= 1;
    assert!(ImportedPopulation::resolve(&doc, |_, _| Ok(bytes.clone())).is_err());
    let terrain = embedded_visual_valley_terrain_document().unwrap();
    assert!(matches!(
        VegetationGenerationPlan::from_documents(&terrain, &doc),
        Err(forge_world::VegetationGenerationError::ImportedSourceRequiresResolver)
    ));
    let mut legacy = doc;
    legacy.schema_version = 1;
    legacy.imported_source = None;
    assert!(
        ImportedPopulation::resolve(&legacy, |_, _| panic!("legacy must not request blobs"))
            .is_err()
    );
}

#[test]
#[allow(clippy::too_many_lines)] // One sequential save/undo/promotion/density scenario.
fn authored_exclusions_persist_reopen_undo_and_leave_base_unchanged() {
    use forge_core::CommandId;
    use forge_project::{
        VegetationExclusion, VegetationExclusionId,
        zone_authoring::{
            exclusions::VegetationExclusionEdit,
            session::{ZoneAction, ZoneEditSession, ZoneRequest},
        },
    };
    let (mut doc, bytes) = fixture();
    let terrain = embedded_visual_valley_terrain_document().unwrap();
    let mut session = ZoneEditSession::from_initial_terrain(terrain).unwrap();
    doc.project_id = session.head().base.terrain.project_id;
    doc.area_id = session.head().base.terrain.area_id;
    doc.layers[0].material_filter = None;
    doc.layers[0].kind = forge_project::VegetationKind::Tree;
    doc.layers[0].minimum_spacing_m = 0.1;
    let mut base = session.head().base.clone();
    base.vegetation = Some(doc.clone());
    let request = session
        .head()
        .prepare_regeneration(base, CommandId::deterministic("import-population"))
        .unwrap();
    session.execute(&request).unwrap();
    let population = ImportedPopulation::resolve(&doc, |_, _| Ok(bytes.clone())).unwrap();
    let request = session
        .head()
        .prepare_vegetation_exclusions(
            VegetationExclusionEdit::Replace {
                exclusions: vec![VegetationExclusion::Circle {
                    id: VegetationExclusionId::new("clearing"),
                    center_x_m: -192.0,
                    center_z_m: -192.0,
                    radius_m: 1.0,
                }],
            },
            CommandId::deterministic("clear-imported"),
        )
        .unwrap();
    session.execute(&request).unwrap();
    let view = population.exclusion_view(session.head()).unwrap();
    assert_eq!(view.excluded_count, 1);
    assert_eq!(view.layers[0].instances.len(), 2);
    assert_eq!(population.instance_count(), 3);
    let mut reopened =
        ZoneEditSession::from_archive_bytes(&session.archive_bytes().unwrap()).unwrap();
    assert_eq!(
        population
            .exclusion_view(reopened.head())
            .unwrap()
            .excluded_count,
        1
    );
    let undo = ZoneRequest {
        command_id: CommandId::deterministic("undo-imported-clearing"),
        expected_revision: reopened.head().revision,
        expected_hash: reopened.head().content_hash().unwrap(),
        action: ZoneAction::Undo,
    };
    reopened.execute(&undo).unwrap();
    assert_eq!(
        population
            .exclusion_view(reopened.head())
            .unwrap()
            .excluded_count,
        0
    );
    let mut stale = reopened.head().clone();
    stale.base.vegetation.as_mut().unwrap().seed += 1;
    assert!(population.exclusion_view(&stale).is_err());
    // A promoted origin survives even after its manual placement was deleted.
    let mut corrections = reopened.head().corrections.clone();
    corrections.promotions = Some(vec![
        forge_project::zone_authoring::promotion::PromotedInstance {
            layer_id: doc.layers[0].id.clone(),
            instance_id: population.layers()[0].instances()[0].id,
            generation_hash: doc.content_hash().unwrap(),
            placement_id: forge_core::EntityId::deterministic("deleted-tree"),
        },
    ]);
    let promotion = ZoneRequest {
        command_id: CommandId::deterministic("retain-imported-origin"),
        expected_revision: reopened.head().revision,
        expected_hash: reopened.head().content_hash().unwrap(),
        action: ZoneAction::edit(corrections),
    };
    reopened.execute(&promotion).unwrap();
    let archived = ZoneEditSession::from_archive_bytes(&reopened.archive_bytes().unwrap()).unwrap();
    assert_eq!(
        population
            .exclusion_view(archived.head())
            .unwrap()
            .excluded_count,
        1
    );
    let mut unresolved = archived.head().clone();
    unresolved.corrections.promotions.as_mut().unwrap()[0].instance_id =
        forge_core::EntityId::deterministic("absent-origin");
    assert!(population.exclusion_view(&unresolved).is_err());
    let paint = reopened
        .head()
        .prepare_vegetation_edit(
            forge_project::zone_authoring::vegetation_commands::VegetationEdit::SetDensityMask {
                layer_id: doc.layers[0].id.clone(),
                mask: forge_project::VegetationDensityMask {
                    vertices_per_axis: 2,
                    samples_u8: vec![0; 4],
                },
            },
            CommandId::deterministic("zero-imported-density"),
        )
        .unwrap();
    reopened.execute(&paint).unwrap();
    let mut painted =
        ZoneEditSession::from_archive_bytes(&reopened.archive_bytes().unwrap()).unwrap();
    let view = population.authored_view(painted.head()).unwrap();
    assert_eq!(view.excluded_count, 1);
    assert_eq!(view.density_removed_count, 2);
    assert!(view.layers[0].instances.is_empty());
    let undo = ZoneRequest {
        command_id: CommandId::deterministic("undo-imported-density"),
        expected_revision: painted.head().revision,
        expected_hash: painted.head().content_hash().unwrap(),
        action: ZoneAction::Undo,
    };
    painted.execute(&undo).unwrap();
    let restored = population.authored_view(painted.head()).unwrap();
    assert_eq!(restored.layers[0].instances.len(), 2);
    assert_eq!(restored.excluded_count, 1);
    assert_eq!(restored.density_removed_count, 0);
}
