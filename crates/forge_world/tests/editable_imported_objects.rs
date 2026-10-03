use forge_core::{CommandId, EntityId, Transform, UnitQuaternion, Vec3, bytes_sha256};
use forge_project::{
    VegetationDensityMask, VegetationDocument,
    imported_vegetation::{
        ImportedInstanceEncoding, ImportedVegetationBlock, ImportedVegetationSource,
    },
    zone_authoring::{
        DensityOverride,
        exclusions::VegetationExclusionEdit,
        placements::PlacementEdit,
        session::{ZoneAction, ZoneEditSession, ZoneRequest},
    },
};
use forge_world::{
    embedded_visual_valley_terrain_document, imported_vegetation::ImportedPopulation,
};

fn fixture() -> (VegetationDocument, Vec<u8>) {
    let mut document = VegetationDocument::from_json(include_str!(
        "../../../examples/visual_valley/vegetation.v1.json"
    ))
    .unwrap();
    document.schema_version = 2;
    document.layers.truncate(1);
    let mut bytes = Vec::new();
    for (key, x, z, parameter) in [
        (1_u8, -128.0_f64, -128.0_f64, 11.0),
        (2, 128.0, 128.0, 22.0),
        (3, -192.0, 192.0, 33.0),
    ] {
        bytes.extend([key; 32]);
        for value in [x, 5.0, z, 0.0, 0.0, 0.0, 1.0, 1.0, 1.0, 1.0, parameter] {
            bytes.extend(value.to_le_bytes());
        }
    }
    document.imported_source = Some(ImportedVegetationSource {
        schema: "forge.imported-vegetation".into(),
        version: 1,
        blocks: vec![ImportedVegetationBlock {
            layer_id: document.layers[0].id.to_string(),
            prototype_key: "trees".into(),
            count: 3,
            encoding: ImportedInstanceEncoding::TransformF64LeV1,
            instances_sha256: bytes_sha256(&bytes),
            parameter_names: vec!["variation".into()],
        }],
    });
    let terrain = embedded_visual_valley_terrain_document().unwrap();
    document.project_id = terrain.project_id;
    document.area_id = terrain.area_id;
    (document, bytes)
}

fn zone_fixture(document: &VegetationDocument) -> (ZoneEditSession, Vec<u8>) {
    let mut terrain = embedded_visual_valley_terrain_document().unwrap();
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
    base.vegetation = Some(document.clone());
    let request = session
        .head()
        .prepare_regeneration(base, CommandId::deterministic("editable-vegetation-base"))
        .unwrap();
    session.execute(&request).unwrap();
    (session, heights)
}

fn source_placement(
    population: &ImportedPopulation,
    session: &ZoneEditSession,
    heights: &[u8],
    id: EntityId,
) -> forge_project::zone_authoring::placements::ManualPlacement {
    population
        .editable_placement(session.head(), heights, id)
        .unwrap()
        .unwrap()
}

fn apply_edit(
    session: &mut ZoneEditSession,
    corrections: forge_project::zone_authoring::ZoneCorrections,
    label: &str,
) {
    let request = ZoneRequest {
        command_id: CommandId::deterministic(label),
        expected_revision: session.head().revision,
        expected_hash: session.head().content_hash().unwrap(),
        action: ZoneAction::edit(corrections),
    };
    session.execute(&request).unwrap();
}

fn add_source_copy(
    session: &mut ZoneEditSession,
    source: &forge_project::zone_authoring::placements::ManualPlacement,
    copy_id: EntityId,
) {
    let request = session
        .head()
        .prepare_source_object(
            source.clone(),
            PlacementEdit::Duplicate {
                source_id: source.id,
                id: copy_id,
                transform: Transform {
                    translation_m: Vec3::new(160.0, 9.0, -180.0),
                    rotation: UnitQuaternion::new(0.0, 0.382_683_43, 0.0, 0.923_879_5),
                    scale: Vec3::new(1.25, 0.75, 2.0),
                },
            },
            CommandId::deterministic("editable-source-copy"),
        )
        .unwrap();
    session.execute(&request).unwrap();
}

#[test]
#[allow(clippy::float_cmp)]
fn generic_source_transform_preserves_parameters_and_copy_identity() {
    let (document, bytes) = fixture();
    let population = ImportedPopulation::resolve(&document, |_, _| Ok(bytes.clone())).unwrap();
    let (mut session, heights) = zone_fixture(&document);
    let original = population.layers()[0].instances()[0].clone();
    let source = source_placement(&population, &session, &heights, original.id);
    let transformed = Transform {
        translation_m: Vec3::new(-200.0, -3.0, 180.0),
        rotation: UnitQuaternion::new(0.391_903_85, 0.200_562_12, 0.531_975_7, 0.723_317_4),
        scale: Vec3::new(2.0, 0.5, 3.0),
    };
    let request = session
        .head()
        .prepare_source_object(
            source.clone(),
            PlacementEdit::Transform {
                id: source.id,
                transform: transformed,
            },
            CommandId::deterministic("editable-source-transform"),
        )
        .unwrap();
    session.execute(&request).unwrap();
    let copy_id = EntityId::deterministic("editable-copy");
    add_source_copy(&mut session, &source, copy_id);

    let view = population.terrain_view(session.head(), &heights).unwrap();
    let edited = view.layers[0]
        .instances
        .iter()
        .find(|entity| entity.id == source.id)
        .unwrap();
    assert_eq!(edited.source.parameters, original.source.parameters);
    assert_eq!(edited.source.position_m, [-200.0, -3.0, 180.0]);
    let expected_rotation = [
        transformed.rotation.x,
        transformed.rotation.y,
        transformed.rotation.z,
        transformed.rotation.w,
    ]
    .map(f64::from);
    assert_eq!(edited.source.rotation_xyzw, expected_rotation);
    assert_eq!(edited.source.scale, [2.0, 0.5, 3.0]);
    let copy = view.layers[0]
        .instances
        .iter()
        .find(|entity| entity.id == copy_id)
        .unwrap();
    assert_eq!(copy.source.parameters, original.source.parameters);
    assert_eq!(bytes, {
        let mut unchanged = Vec::new();
        for (key, x, z, parameter) in [
            (1_u8, -128.0_f64, -128.0_f64, 11.0),
            (2, 128.0, 128.0, 22.0),
            (3, -192.0, 192.0, 33.0),
        ] {
            unchanged.extend([key; 32]);
            for value in [x, 5.0, z, 0.0, 0.0, 0.0, 1.0, 1.0, 1.0, 1.0, parameter] {
                unchanged.extend(value.to_le_bytes());
            }
        }
        unchanged
    });
}

#[test]
#[allow(
    clippy::cast_possible_truncation,
    reason = "Fixture coordinates are bounded and intentionally projected to the exclusion API"
)]
fn generic_copy_is_retained_when_immutable_source_is_excluded_or_density_filtered() {
    let (document, bytes) = fixture();
    let population = ImportedPopulation::resolve(&document, |_, _| Ok(bytes.clone())).unwrap();
    let (mut session, heights) = zone_fixture(&document);
    let source = source_placement(
        &population,
        &session,
        &heights,
        population.layers()[0].instances()[0].id,
    );
    let copy_id = EntityId::deterministic("copy-survives-filter");
    add_source_copy(&mut session, &source, copy_id);

    let source_record = population.layers()[0].instances()[0].source.position_m;
    let exclusions = vec![forge_project::VegetationExclusion::Circle {
        id: forge_project::VegetationExclusionId::new("hide_source"),
        center_x_m: source_record[0] as f32,
        center_z_m: source_record[2] as f32,
        radius_m: 4.0,
    }];
    let request = session
        .head()
        .prepare_vegetation_exclusions(
            VegetationExclusionEdit::Replace { exclusions },
            CommandId::deterministic("exclude-source-for-copy"),
        )
        .unwrap();
    session.execute(&request).unwrap();
    let excluded_view = population.terrain_view(session.head(), &heights).unwrap();
    assert!(
        excluded_view.layers[0]
            .instances
            .iter()
            .any(|entity| entity.id == copy_id)
    );

    let mut density_session = zone_fixture(&document).0;
    let density_source = source_placement(
        &population,
        &density_session,
        &heights,
        population.layers()[0].instances()[0].id,
    );
    add_source_copy(&mut density_session, &density_source, copy_id);
    let mut corrections = density_session.head().corrections.clone();
    corrections.density.push(DensityOverride {
        layer_id: document.layers[0].id.clone(),
        mask: VegetationDensityMask {
            vertices_per_axis: 2,
            samples_u8: vec![0; 4],
        },
        locked: false,
    });
    apply_edit(
        &mut density_session,
        corrections,
        "density-hide-source-for-copy",
    );
    let density_view = population
        .terrain_view(density_session.head(), &heights)
        .unwrap();
    assert!(
        density_view.layers[0]
            .instances
            .iter()
            .any(|entity| entity.id == copy_id)
    );
}

#[test]
fn generic_source_projection_refuses_out_of_extent_transform_without_touching_source_bytes() {
    let (document, bytes) = fixture();
    let original_bytes = bytes.clone();
    let population = ImportedPopulation::resolve(&document, |_, _| Ok(bytes.clone())).unwrap();
    let (mut session, heights) = zone_fixture(&document);
    let source = source_placement(
        &population,
        &session,
        &heights,
        population.layers()[0].instances()[0].id,
    );
    let request = session
        .head()
        .prepare_source_object(
            source.clone(),
            PlacementEdit::Transform {
                id: source.id,
                transform: Transform {
                    translation_m: Vec3::new(4096.0, 2.0, -4096.0),
                    rotation: UnitQuaternion::IDENTITY,
                    scale: Vec3::ONE,
                },
            },
            CommandId::deterministic("editable-out-of-extent"),
        )
        .unwrap();
    session.execute(&request).unwrap();
    assert!(population.terrain_view(session.head(), &heights).is_err());
    assert_eq!(
        population
            .document()
            .imported_source
            .as_ref()
            .unwrap()
            .blocks[0]
            .instances_sha256,
        bytes_sha256(&original_bytes)
    );
}
