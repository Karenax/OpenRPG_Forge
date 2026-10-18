use super::*;
use forge_core::{CommandId, EntityId, Transform, UnitQuaternion, Vec3, bytes_sha256};
use forge_project::{
    imported_vegetation::{
        ImportedInstanceEncoding, ImportedVegetationBlock, ImportedVegetationSource,
    },
    zone_authoring::{
        ZoneAuthoringHead,
        imported_plants::{ImportedPlantPlacement, ImportedPlantPlacementEdit},
        placements::PlacementEdit,
        session::{ZoneAction, ZoneEditSession, ZoneRequest},
    },
};

fn fixture() -> (ImportedPopulation, ZoneEditSession, Vec<u8>) {
    let mut document = forge_world::embedded_visual_valley_vegetation_document().unwrap();
    document.schema_version = 2;
    document.layers.truncate(1);
    let mut bytes = Vec::new();
    // Source keys are sorted, while render order deliberately runs backwards.
    for (key, x, order) in [(1_u8, -128.0_f64, 1.0), (2, 128.0, 0.0)] {
        bytes.extend([key; 32]);
        for value in [
            x, 5.0, x, 0.0, 0.0, 0.0, 1.0, 1.0, 1.0, 1.0, 0.25, 0.5, 2.0, 0.75, 1.0, order,
        ] {
            bytes.extend(value.to_le_bytes());
        }
    }
    let mut parameter_names: Vec<_> = forge_tidewater_assets::tidewater_vegetation::PARAMETERS
        .iter()
        .map(|value| (*value).to_owned())
        .collect();
    parameter_names.push("source_order".into());
    document.imported_source = Some(ImportedVegetationSource {
        schema: "forge.imported-vegetation".into(),
        version: 1,
        blocks: vec![ImportedVegetationBlock {
            layer_id: document.layers[0].id.to_string(),
            prototype_key: "tidewater_palms".into(),
            count: 2,
            encoding: ImportedInstanceEncoding::TransformF64LeV1,
            instances_sha256: bytes_sha256(&bytes),
            parameter_names,
        }],
    });
    let mut terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
    document.project_id = terrain.project_id;
    document.area_id = terrain.area_id;
    let population = ImportedPopulation::resolve(&document, |_, _| Ok(bytes.clone())).unwrap();
    let heights: Vec<_> = (0..512 * 512).flat_map(|_| 2.0_f32.to_le_bytes()).collect();
    terrain.schema_version = 4;
    terrain.height_sampling.algorithm = forge_project::HeightSamplingAlgorithm::SampledRasterV1;
    terrain.height_sampling.base_elevation_m = 0.0;
    terrain.height_sampling.sample_spacing_m = 1.0;
    terrain.height_sampling.amplitude_m = 0.0;
    terrain.height_sampling.base_frequency_cycles_per_m = 1.0;
    terrain.height_sampling.octaves = 1;
    terrain.height_sampling.lacunarity = 2.0;
    terrain.height_sampling.persistence = 0.5;
    terrain.sampled_source = Some(forge_project::sampled_terrain::SampledTerrainSource {
        schema: forge_project::sampled_terrain::SAMPLED_TERRAIN_SCHEMA.into(),
        schema_version: 1,
        width: 512,
        height: 512,
        origin_xz_m: [-256.0; 2],
        sample_spacing_m: 1.0,
        outside_height_m: -90.0,
        encoding: forge_project::sampled_terrain::HeightEncoding::Float32Le,
        samples_sha256: bytes_sha256(&heights),
    });
    let mut session = ZoneEditSession::from_initial_terrain(terrain).unwrap();
    let mut base = session.head().base.clone();
    base.vegetation = Some(document);
    let request = session
        .head()
        .prepare_regeneration(base, CommandId::deterministic("render-population-base"))
        .unwrap();
    session.execute(&request).unwrap();
    (population, session, heights)
}

// Small CPU fixtures exercise the same grouping and stream emission as prepare;
// the production entry point retains its complete-island inventory check.
fn projected(
    population: &ImportedPopulation,
    head: &ZoneAuthoringHead,
    heights: &[u8],
) -> Result<PreparedPopulation, Box<dyn Error>> {
    let visible: ProjectedEntities<'_> = population
        .terrain_view(head, heights)?
        .layers
        .into_iter()
        .flat_map(|layer| {
            layer
                .instances
                .into_iter()
                .map(move |entity| (entity.id, (layer.layer_id, entity)))
        })
        .collect();
    emit_records(
        group_records(population, Some(head), Some(&visible))?,
        Some(&visible),
        Some(head.content_hash()?),
    )
}

fn source_edit(
    population: &ImportedPopulation,
    session: &mut ZoneEditSession,
    heights: &[u8],
    id: EntityId,
    edit: PlacementEdit,
    label: &str,
) {
    let source = population
        .editable_placement(session.head(), heights, id)
        .unwrap()
        .unwrap();
    let request = session
        .head()
        .prepare_source_object(source, edit, CommandId::deterministic(label))
        .unwrap();
    session.execute(&request).unwrap();
}

fn insert_legacy(
    population: &ImportedPopulation,
    session: &mut ZoneEditSession,
    id: EntityId,
    label: &str,
) {
    let layer = &population.layers()[0];
    let request = session
        .head()
        .prepare_imported_plant_placement(
            ImportedPlantPlacementEdit::Insert {
                placement: ImportedPlantPlacement {
                    id,
                    layer_id: forge_project::VegetationLayerId::new(&layer.layer_id),
                    source_key: layer.instances()[0].source.source_key,
                    translation_m: [4.0, 1.0, 6.0],
                    yaw_degrees: None,
                    scale_factor: None,
                },
            },
            CommandId::deterministic(label),
        )
        .unwrap();
    session.execute(&request).unwrap();
}

fn undo(session: &mut ZoneEditSession, label: &str) {
    let request = ZoneRequest {
        command_id: CommandId::deterministic(label),
        expected_revision: session.head().revision,
        expected_hash: session.head().content_hash().unwrap(),
        action: ZoneAction::Undo,
    };
    session.execute(&request).unwrap();
}

#[test]
fn generic_copy_is_emitted_once_with_final_transform_and_prototype_parameters() {
    let (population, mut session, heights) = fixture();
    let source = population.layers()[0].instances()[0].id;
    let copy = EntityId::deterministic("render-common-copy");
    source_edit(
        &population,
        &mut session,
        &heights,
        source,
        PlacementEdit::Duplicate {
            source_id: source,
            id: copy,
            transform: Transform {
                translation_m: Vec3::new(17.0, 19.0, 23.0),
                rotation: UnitQuaternion::new(0.0, 0.707_106_77, 0.0, 0.707_106_77),
                scale: Vec3::new(2.0, 3.0, 4.0),
            },
        },
        "render-common-copy-edit",
    );
    let result = projected(&population, session.head(), &heights).unwrap();
    assert_eq!(
        result.identities["palms"],
        vec![population.layers()[0].instances()[1].id, source, copy]
    );
    let streams = &result.streams["palms"];
    assert_eq!(streams["matrices"].len(), 3 * 16 * 4);
    assert_eq!(
        &streams["matrices"][2 * 64 + 48..2 * 64 + 64],
        [17.0_f32, 19.0, 23.0, 1.0].map(f32::to_le_bytes).concat()
    );
    assert_eq!(
        &streams["iPos"][2 * 16..],
        [17.0_f32, 19.0, 23.0, 2.0].map(f32::to_le_bytes).concat()
    );
    assert_eq!(
        &streams["iDat"][2 * 16..],
        [0.25_f32, 0.5, 2.0, 0.75].map(f32::to_le_bytes).concat()
    );
    let matrix: Vec<_> = streams["matrices"][128..192]
        .as_chunks::<4>()
        .0
        .iter()
        .map(|bytes| f32::from_le_bytes(*bytes))
        .collect();
    assert!(matrix[0].abs() < 0.00001);
    assert!((matrix[2] + 2.0).abs() < 0.00001);
    assert!((matrix[5] - 3.0).abs() < 0.00001);
    assert!((matrix[8] - 4.0).abs() < 0.00001);
    assert!(matrix[10].abs() < 0.00001);

    let before_remove = result;
    source_edit(
        &population,
        &mut session,
        &heights,
        source,
        PlacementEdit::Remove { id: source },
        "render-source-remove",
    );
    let removed = projected(&population, session.head(), &heights).unwrap();
    assert_eq!(
        removed.identities["palms"],
        vec![population.layers()[0].instances()[1].id, copy]
    );
    undo(&mut session, "render-source-remove-undo");
    let restored = projected(&population, session.head(), &heights).unwrap();
    assert_eq!(restored.identities, before_remove.identities);
    assert_eq!(restored.streams, before_remove.streams);
}

#[test]
fn removing_legacy_copy_keeps_its_generic_duplicate_and_undo_restores_streams() {
    let (population, mut session, heights) = fixture();
    let legacy = EntityId::deterministic("render-legacy-copy");
    insert_legacy(&population, &mut session, legacy, "render-legacy-insert");
    let duplicate = EntityId::deterministic("render-legacy-common-duplicate");
    source_edit(
        &population,
        &mut session,
        &heights,
        legacy,
        PlacementEdit::Duplicate {
            source_id: legacy,
            id: duplicate,
            transform: Transform::IDENTITY,
        },
        "render-legacy-duplicate",
    );
    let before = projected(&population, session.head(), &heights).unwrap();
    assert_eq!(before.identities["palms"].len(), 4);
    assert_eq!(&before.identities["palms"][2..], [legacy, duplicate]);
    source_edit(
        &population,
        &mut session,
        &heights,
        legacy,
        PlacementEdit::Remove { id: legacy },
        "render-legacy-remove",
    );
    let removed = projected(&population, session.head(), &heights).unwrap();
    assert!(!removed.identities["palms"].contains(&legacy));
    assert!(removed.identities["palms"].contains(&duplicate));
    assert_eq!(removed.identities["palms"].len(), 3);
    undo(&mut session, "render-legacy-remove-undo");
    let restored = projected(&population, session.head(), &heights).unwrap();
    assert_eq!(restored.identities, before.identities);
    assert_eq!(restored.streams, before.streams);
}

#[test]
fn unchanged_projection_preserves_original_order_and_legacy_copy_layout() {
    let (population, mut session, heights) = fixture();
    let original =
        emit_records(group_records(&population, None, None).unwrap(), None, None).unwrap();
    let unchanged = projected(&population, session.head(), &heights).unwrap();
    assert_eq!(unchanged.identities, original.identities);
    assert_eq!(unchanged.streams, original.streams);
    let mut legacy = [
        EntityId::deterministic("render-old-a"),
        EntityId::deterministic("render-old-b"),
    ];
    legacy.sort();
    insert_legacy(&population, &mut session, legacy[1], "render-old-b-insert");
    insert_legacy(&population, &mut session, legacy[0], "render-old-a-insert");
    let result = projected(&population, session.head(), &heights).unwrap();
    assert_eq!(
        &result.identities["palms"][..2],
        original.identities["palms"]
    );
    assert_eq!(&result.identities["palms"][2..], legacy);
    assert_eq!(
        &result.streams["palms"]["matrices"][..128],
        original.streams["palms"]["matrices"]
    );
    let expected_position = [-124.0_f32, 6.0, -122.0, 1.0]
        .map(f32::to_le_bytes)
        .concat();
    assert_eq!(&result.streams["palms"]["iPos"][32..48], expected_position);
    assert_eq!(&result.streams["palms"]["iPos"][48..64], expected_position);
}

#[test]
fn missing_legacy_template_is_rejected_before_gpu_projection() {
    let (population, mut session, heights) = fixture();
    insert_legacy(
        &population,
        &mut session,
        EntityId::deterministic("render-invalid-template"),
        "render-invalid-insert",
    );
    let mut head = session.head().clone();
    head.corrections.imported_plant_placements.as_mut().unwrap()[0].source_key =
        bytes_sha256(b"missing");
    let error = prepare(&population, Some((&head, &heights)))
        .err()
        .unwrap()
        .to_string();
    assert!(error.contains("source key disappeared"), "{error}");
}
