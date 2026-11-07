use super::*;
use forge_core::{CommandId, bytes_sha256};
use forge_project::{
    HeightSamplingAlgorithm, VegetationDocument,
    imported_vegetation::{
        ImportedInstanceEncoding, ImportedVegetationBlock, ImportedVegetationSource,
    },
    sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
    zone_authoring::{
        imported_plants::{ImportedPlantEdit, ImportedPlantOffset},
        session::ZoneEditSession,
    },
};

struct Fixture {
    population: ImportedPopulation,
    zone: ZoneEditSession,
    heights: Vec<u8>,
    policy: CellStreamingPolicy,
    terrain_hash: ContentHash,
    envelopes: BTreeMap<String, PrototypeEnvelope>,
}

impl Fixture {
    fn new() -> Self {
        let mut vegetation = VegetationDocument::from_json(include_str!(
            "../../../../../examples/visual_valley/vegetation.v1.json"
        ))
        .unwrap();
        vegetation.schema_version = 2;
        vegetation.layers.truncate(1);
        vegetation.layers[0].material_filter = None;
        let mut bytes = Vec::new();
        for (key, x, z) in [
            (1_u8, -192.0_f64, -192.0_f64),
            (2, -64.0, -192.0),
            (3, 192.0, 192.0),
            (4, 256.0, 256.0),
        ] {
            bytes.extend([key; 32]);
            for value in [x, 5.0, z, 0.0, 0.0, 0.0, 1.0, 1.0, 1.0, 1.0, 0.75] {
                bytes.extend(value.to_le_bytes());
            }
        }
        vegetation.imported_source = Some(ImportedVegetationSource {
            grass: None,
            schema: "forge.imported-vegetation".into(),
            version: 1,
            blocks: vec![ImportedVegetationBlock {
                layer_id: vegetation.layers[0].id.to_string(),
                prototype_key: "trees".into(),
                count: 4,
                encoding: ImportedInstanceEncoding::TransformF64LeV1,
                instances_sha256: bytes_sha256(&bytes),
                parameter_names: vec!["wind_phase".into()],
            }],
        });
        let population =
            ImportedPopulation::resolve(&vegetation, |_, _| Ok(bytes.clone())).unwrap();
        let mut terrain = crate::embedded_visual_valley_terrain_document().unwrap();
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
        let terrain_hash = terrain.content_hash().unwrap();
        let mut zone = ZoneEditSession::from_initial_terrain(terrain).unwrap();
        let mut base = zone.head().base.clone();
        base.vegetation = Some(vegetation);
        let request = zone
            .head()
            .prepare_regeneration(base, CommandId::deterministic("streamed-plants"))
            .unwrap();
        zone.execute(&request).unwrap();
        Self {
            population,
            zone,
            heights,
            terrain_hash,
            policy: CellStreamingPolicy {
                origin_xz_m: [-256.0; 2],
                cell_size_m: 64.0,
                dimensions: [8; 2],
                detail_radius: 0,
                far_radius: 2,
                max_resident_cells: 25,
                full_cell_bytes: 10,
                hlod_cell_bytes: 5,
                max_resident_bytes: 1000,
                max_staging_bytes: 2000,
            },
            envelopes: BTreeMap::from([(
                "trees".into(),
                PrototypeEnvelope {
                    radius_m: 2.0,
                    deformation_m: 0.5,
                },
            )]),
        }
    }

    fn cells(&self, full: CellKey) -> BTreeMap<CellKey, LoadedCell> {
        BTreeMap::from([
            (
                full,
                LoadedCell {
                    key: full,
                    detail: CellDetail::Full,
                    source: self.terrain_hash,
                    bytes: 10,
                },
            ),
            (
                CellKey { x: 3, z: 1 },
                LoadedCell {
                    key: CellKey { x: 3, z: 1 },
                    detail: CellDetail::Hlod,
                    source: self.terrain_hash,
                    bytes: 5,
                },
            ),
        ])
    }

    fn admission<'a>(
        &'a self,
        cells: &'a BTreeMap<CellKey, LoadedCell>,
        distant: &'a TerrainHlodLayout,
    ) -> AdmittedPlantCells<'a> {
        AdmittedPlantCells {
            policy: &self.policy,
            cells,
            distant,
            terrain_hash: self.terrain_hash,
            head_hash: self.zone.head().content_hash().unwrap(),
        }
    }
}

fn layout(fixture: &Fixture, cells: &BTreeMap<CellKey, LoadedCell>) -> TerrainHlodLayout {
    TerrainHlodLayout::new(
        &fixture.policy,
        &cells
            .iter()
            .map(|(&key, cell)| (key, cell.detail))
            .collect(),
    )
    .unwrap()
}

#[test]
fn edited_f32_rotation_remains_admissible_to_strict_streaming() {
    let mut fixture = Fixture::new();
    let id = fixture.population.layers()[0].instances()[0].id;
    let source = fixture
        .population
        .editable_placement(fixture.zone.head(), &fixture.heights, id)
        .unwrap()
        .unwrap();
    let mut transform = source.transform;
    // A valid authoring quaternion whose f32 norm drifts beyond the f64
    // streaming tolerance, as in the Studio's real 15-degree edit.
    transform.rotation = forge_core::UnitQuaternion::new(0.0, 0.650_520_7, 0.0, -0.759_488_46);
    transform.translation_m.y += 0.5;
    let command = fixture
        .zone
        .head()
        .prepare_source_object(
            source,
            forge_project::zone_authoring::placements::PlacementEdit::Transform { id, transform },
            CommandId::deterministic("edited-rotation-streaming"),
        )
        .unwrap();
    fixture.zone.execute(&command).unwrap();
    let view = fixture
        .population
        .terrain_view(fixture.zone.head(), &fixture.heights)
        .unwrap();
    let cells = fixture.cells(CellKey { x: 1, z: 1 });
    let distant = layout(&fixture, &cells);
    let selected = select(
        &fixture.population,
        &view,
        fixture.admission(&cells, &distant),
        &fixture.envelopes,
    )
    .unwrap();
    assert_eq!(selected.plants.len(), 4);
    assert_eq!(selected.collisions.len(), 4);
    assert!(selected.plants.iter().any(|plant| plant.entity.id == id));
}

#[test]
fn round_trip_reuses_authored_records_and_preserves_every_collision_source() {
    let fixture = Fixture::new();
    let view = fixture
        .population
        .terrain_view(fixture.zone.head(), &fixture.heights)
        .unwrap();
    let near = fixture.cells(CellKey { x: 1, z: 1 });
    let far = fixture.cells(CellKey { x: 7, z: 7 });
    let near_layout = layout(&fixture, &near);
    let far_layout = layout(&fixture, &far);
    let a = select(
        &fixture.population,
        &view,
        fixture.admission(&near, &near_layout),
        &fixture.envelopes,
    )
    .unwrap();
    let b = select(
        &fixture.population,
        &view,
        fixture.admission(&far, &far_layout),
        &fixture.envelopes,
    )
    .unwrap();
    let c = select(
        &fixture.population,
        &view,
        fixture.admission(&near, &near_layout),
        &fixture.envelopes,
    )
    .unwrap();
    assert_eq!(a.plants.len(), 4);
    assert_eq!(a.collisions.len(), 4);
    for ((old, away), returned) in a.plants.iter().zip(&b.plants).zip(&c.plants) {
        assert!(std::ptr::eq(old.entity, away.entity) && std::ptr::eq(old.entity, returned.entity));
        assert_eq!(old.representation, returned.representation);
        assert_eq!(old.parameter_names, ["wind_phase"]);
        assert_eq!(
            old.entity.source.parameters[0].to_bits(),
            0.75_f64.to_bits()
        );
    }
    let original = &view.layers[0].instances[0];
    assert_eq!(
        a.plants
            .iter()
            .find(|p| p.entity.id == original.id)
            .unwrap()
            .representation,
        PlantRepresentation::Detail
    );
    assert!(matches!(
        b.plants
            .iter()
            .find(|p| p.entity.id == original.id)
            .unwrap()
            .representation,
        PlantRepresentation::Group { lod: 2, .. }
    ));
    assert!(
        a.plants
            .iter()
            .any(|p| matches!(p.representation, PlantRepresentation::Group { lod: 1, .. }))
    );
    assert!(
        a.collisions
            .iter()
            .zip(&b.collisions)
            .all(|(x, y)| x.entity == y.entity && x.profile == y.profile)
    );
}

#[test]
fn selection_order_is_independent_of_projected_instance_order() {
    let fixture = Fixture::new();
    let mut view = fixture
        .population
        .terrain_view(fixture.zone.head(), &fixture.heights)
        .unwrap();
    let cells = fixture.cells(CellKey { x: 1, z: 1 });
    let distant = layout(&fixture, &cells);
    let admission = fixture.admission(&cells, &distant);
    let expected = select(&fixture.population, &view, admission, &fixture.envelopes).unwrap();
    let plants: Vec<_> = expected
        .plants
        .iter()
        .map(|plant| (plant.entity.id, plant.representation))
        .collect();
    let collisions: Vec<_> = expected
        .collisions
        .iter()
        .map(|source| (source.entity.id, source.profile))
        .collect();
    drop(expected);
    view.layers[0].instances.reverse();
    let actual = select(&fixture.population, &view, admission, &fixture.envelopes).unwrap();
    assert_eq!(
        plants,
        actual
            .plants
            .iter()
            .map(|plant| (plant.entity.id, plant.representation))
            .collect::<Vec<_>>()
    );
    assert_eq!(
        collisions,
        actual
            .collisions
            .iter()
            .map(|source| (source.entity.id, source.profile))
            .collect::<Vec<_>>()
    );
}

#[test]
fn crown_touching_a_protected_cell_keeps_the_whole_plant_detailed() {
    let mut fixture = Fixture::new();
    // Plant 2 is rooted in coarse cell (3,1), but its crown touches full cell (2,1).
    fixture.envelopes.get_mut("trees").unwrap().radius_m = 1.0;
    let view = fixture
        .population
        .terrain_view(fixture.zone.head(), &fixture.heights)
        .unwrap();
    let cells = fixture.cells(CellKey { x: 2, z: 1 });
    let distant = layout(&fixture, &cells);
    let plan = select(
        &fixture.population,
        &view,
        fixture.admission(&cells, &distant),
        &fixture.envelopes,
    )
    .unwrap();
    let source = &view.layers[0].instances[1];
    assert_eq!(
        plan.plants
            .iter()
            .find(|p| p.entity.id == source.id)
            .unwrap()
            .representation,
        PlantRepresentation::Detail
    );
    assert_eq!(source.source.position_m[0].to_bits(), (-64.0_f64).to_bits());
}

#[test]
fn authored_offset_and_undo_are_consumed_without_changing_population() {
    let mut fixture = Fixture::new();
    let original = fixture.population.layers()[0].instances()[0].clone();
    let offset = ImportedPlantOffset {
        layer_id: fixture.population.document().layers[0].id.clone(),
        source_key: original.source.source_key,
        translation_m: [32.0, 1.0, 0.0],
    };
    let request = fixture
        .zone
        .head()
        .prepare_imported_plant_edit(
            ImportedPlantEdit::SetOffset { offset },
            CommandId::deterministic("move-runtime-plant"),
        )
        .unwrap();
    fixture.zone.execute(&request).unwrap();
    let cells = fixture.cells(CellKey { x: 1, z: 1 });
    let distant = layout(&fixture, &cells);
    let view = fixture
        .population
        .terrain_view(fixture.zone.head(), &fixture.heights)
        .unwrap();
    let selected = select(
        &fixture.population,
        &view,
        fixture.admission(&cells, &distant),
        &fixture.envelopes,
    )
    .unwrap();
    let moved = selected
        .plants
        .iter()
        .find(|p| p.entity.id == original.id)
        .unwrap();
    assert_eq!(
        moved.entity.source.position_m.map(f64::to_bits),
        [-160.0_f64, 6.0, -192.0].map(f64::to_bits)
    );
    assert_eq!(fixture.population.layers()[0].instances()[0], original);
    let undo = fixture
        .zone
        .head()
        .prepare_terrain_action(
            forge_project::terrain_edit::TerrainAction::Undo,
            CommandId::deterministic("undo-runtime-plant"),
        )
        .unwrap();
    fixture.zone.execute(&undo).unwrap();
    let restored = fixture
        .population
        .terrain_view(fixture.zone.head(), &fixture.heights)
        .unwrap();
    assert_eq!(restored.layers[0].instances[0], original);
}

#[test]
fn stale_source_layout_missing_prototype_and_invalid_pose_refuse_atomically() {
    let fixture = Fixture::new();
    let mut view = fixture
        .population
        .terrain_view(fixture.zone.head(), &fixture.heights)
        .unwrap();
    let mut cells = fixture.cells(CellKey { x: 1, z: 1 });
    let distant = layout(&fixture, &cells);
    let mut admission = fixture.admission(&cells, &distant);
    admission.head_hash = bytes_sha256(b"stale-head");
    assert!(select(&fixture.population, &view, admission, &fixture.envelopes).is_err());
    cells.values_mut().next().unwrap().source = bytes_sha256(b"stale-terrain");
    assert!(
        select(
            &fixture.population,
            &view,
            fixture.admission(&cells, &distant),
            &fixture.envelopes
        )
        .is_err()
    );
    let cells = fixture.cells(CellKey { x: 1, z: 1 });
    let different = layout(&fixture, &fixture.cells(CellKey { x: 7, z: 7 }));
    assert!(
        select(
            &fixture.population,
            &view,
            fixture.admission(&cells, &different),
            &fixture.envelopes
        )
        .is_err()
    );
    assert!(
        select(
            &fixture.population,
            &view,
            fixture.admission(&cells, &distant),
            &BTreeMap::new()
        )
        .is_err()
    );
    view.layers[0].instances[3].source.position_m[0] = 257.0;
    assert!(
        select(
            &fixture.population,
            &view,
            fixture.admission(&cells, &distant),
            &fixture.envelopes
        )
        .is_err()
    );
    view.layers[0].instances[3].source.position_m[0] = f64::NAN;
    assert!(
        select(
            &fixture.population,
            &view,
            fixture.admission(&cells, &distant),
            &fixture.envelopes
        )
        .is_err()
    );
    assert_eq!(fixture.population.instance_count(), 4);
}

#[test]
fn deformation_reaching_a_detailed_cell_prevents_a_coarse_plant_replacement() {
    let mut fixture = Fixture::new();
    let source = &fixture.population.layers()[0].instances()[1];
    let offset = ImportedPlantOffset {
        layer_id: fixture.population.document().layers[0].id.clone(),
        source_key: source.source.source_key,
        translation_m: [2.0, 0.0, 2.0],
    };
    let request = fixture
        .zone
        .head()
        .prepare_imported_plant_edit(
            ImportedPlantEdit::SetOffset { offset },
            CommandId::deterministic("crown-margin"),
        )
        .unwrap();
    fixture.zone.execute(&request).unwrap();
    let cells = fixture.cells(CellKey { x: 2, z: 1 });
    let distant = layout(&fixture, &cells);
    let view = fixture
        .population
        .terrain_view(fixture.zone.head(), &fixture.heights)
        .unwrap();
    let envelopes = |deformation_m| {
        BTreeMap::from([(
            "trees".into(),
            PrototypeEnvelope {
                radius_m: 1.0,
                deformation_m,
            },
        )])
    };
    let a = select(
        &fixture.population,
        &view,
        fixture.admission(&cells, &distant),
        &envelopes(0.5),
    )
    .unwrap();
    let b = select(
        &fixture.population,
        &view,
        fixture.admission(&cells, &distant),
        &envelopes(1.0),
    )
    .unwrap();
    let plant = &view.layers[0].instances[1];
    assert!(matches!(
        a.plants
            .iter()
            .find(|p| p.entity.id == plant.id)
            .unwrap()
            .representation,
        PlantRepresentation::Group { lod: 1, .. }
    ));
    assert_eq!(
        b.plants
            .iter()
            .find(|p| p.entity.id == plant.id)
            .unwrap()
            .representation,
        PlantRepresentation::Detail
    );
    assert_eq!(a.collisions.len(), b.collisions.len());
}

#[test]
fn mismatched_grid_resident_budget_duplicate_identity_and_parameter_layout_are_rejected() {
    let mut fixture = Fixture::new();
    let mut cells = fixture.cells(CellKey { x: 1, z: 1 });
    let distant = layout(&fixture, &cells);
    fixture.policy.origin_xz_m[0] += 1.0;
    let mut view = fixture
        .population
        .terrain_view(fixture.zone.head(), &fixture.heights)
        .unwrap();
    assert!(
        select(
            &fixture.population,
            &view,
            fixture.admission(&cells, &distant),
            &fixture.envelopes
        )
        .is_err()
    );
    fixture.policy.origin_xz_m[0] -= 1.0;
    cells.values_mut().next().unwrap().bytes = fixture.policy.max_resident_bytes + 1;
    assert!(
        select(
            &fixture.population,
            &view,
            fixture.admission(&cells, &distant),
            &fixture.envelopes
        )
        .is_err()
    );
    let cells = fixture.cells(CellKey { x: 1, z: 1 });
    let duplicate = view.layers[0].instances[0].clone();
    view.layers[0].instances.push(duplicate);
    assert!(
        select(
            &fixture.population,
            &view,
            fixture.admission(&cells, &distant),
            &fixture.envelopes
        )
        .is_err()
    );
    view.layers[0].instances.pop();
    view.layers[0].instances[3].source.parameters.clear();
    assert!(
        select(
            &fixture.population,
            &view,
            fixture.admission(&cells, &distant),
            &fixture.envelopes
        )
        .is_err()
    );
    view.layers.clear();
    assert!(
        select(
            &fixture.population,
            &view,
            fixture.admission(&cells, &distant),
            &fixture.envelopes
        )
        .is_err()
    );
}

#[test]
fn actual_cell_admission_keeps_pending_requests_out_and_preserves_a_protected_island() {
    use crate::cell_streaming::CellStreamer;
    let fixture = Fixture::new();
    let view = fixture
        .population
        .terrain_view(fixture.zone.head(), &fixture.heights)
        .unwrap();
    let mut streamer = CellStreamer::new(
        forge_core::EntityId::deterministic("vegetation-test-host"),
        fixture.terrain_hash,
        fixture.policy.clone(),
    )
    .unwrap();
    let pending = streamer.prepare([-180.0, -180.0]).unwrap();
    let source = &view.layers[0].instances[0];
    let distant = layout(&fixture, streamer.resident());
    let before = select(
        &fixture.population,
        &view,
        fixture.admission(streamer.resident(), &distant),
        &fixture.envelopes,
    )
    .unwrap();
    assert!(matches!(
        before
            .plants
            .iter()
            .find(|p| p.entity.id == source.id)
            .unwrap()
            .representation,
        PlantRepresentation::Group { lod: 2, .. }
    ));
    let loaded: Vec<_> = pending
        .acquisitions()
        .iter()
        .map(|(&key, &detail)| LoadedCell {
            key,
            detail,
            source: fixture.terrain_hash,
            bytes: if detail == CellDetail::Full { 10 } else { 5 },
        })
        .collect();
    // An incomplete acknowledgement does not change the only map available to the selector.
    assert!(streamer.commit(&pending, &loaded[1..]).is_err());
    assert!(streamer.resident().is_empty());
    streamer.commit(&pending, &loaded).unwrap();
    let admitted = layout(&fixture, streamer.resident());
    let near = select(
        &fixture.population,
        &view,
        fixture.admission(streamer.resident(), &admitted),
        &fixture.envelopes,
    )
    .unwrap();
    assert_eq!(
        near.plants
            .iter()
            .find(|p| p.entity.id == source.id)
            .unwrap()
            .representation,
        PlantRepresentation::Detail
    );
    let away = streamer
        .prepare_with_protected_cells([220.0, 220.0], &[CellKey { x: 1, z: 1 }])
        .unwrap();
    let loaded: Vec<_> = away
        .acquisitions()
        .iter()
        .map(|(&key, &detail)| LoadedCell {
            key,
            detail,
            source: fixture.terrain_hash,
            bytes: if detail == CellDetail::Full { 10 } else { 5 },
        })
        .collect();
    streamer.commit(&away, &loaded).unwrap();
    let admitted = layout(&fixture, streamer.resident());
    let protected = select(
        &fixture.population,
        &view,
        fixture.admission(streamer.resident(), &admitted),
        &fixture.envelopes,
    )
    .unwrap();
    assert_eq!(
        protected
            .plants
            .iter()
            .find(|p| p.entity.id == source.id)
            .unwrap()
            .representation,
        PlantRepresentation::Detail
    );
    assert_eq!(protected.collisions.len(), near.collisions.len());
}
