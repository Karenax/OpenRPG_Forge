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
#[allow(clippy::float_cmp)] // Integer fixture heights and coordinates must remain exact.
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
