use super::*;
use forge_project::{TerrainMaterialOverride, TerrainMaterialWeight};
use forge_world::cell_streaming::{CellDetail, CellKey, CellStreamingPolicy, SampledCellSource};
use std::sync::Arc;

fn plan(painted: bool) -> Arc<SampledTerrainPlan> {
    let mut document =
        forge_project::authoring::new_terrain("paint-test", "source", "Paint source").unwrap();
    let bytes = vec![0; 32 * 32 * 4];
    document.schema_version = 4;
    document.extent_m.size_x_m = 32.0;
    document.extent_m.size_z_m = 32.0;
    document.height_sampling.algorithm = forge_project::HeightSamplingAlgorithm::SampledRasterV1;
    document.height_sampling.sample_spacing_m = 1.0;
    document.height_sampling.base_frequency_cycles_per_m = 1.0;
    document.sampled_source = Some(forge_project::sampled_terrain::SampledTerrainSource {
        schema: forge_project::sampled_terrain::SAMPLED_TERRAIN_SCHEMA.into(),
        schema_version: 1,
        width: 32,
        height: 32,
        origin_xz_m: [-16.0; 2],
        sample_spacing_m: 1.0,
        outside_height_m: 0.0,
        encoding: forge_project::sampled_terrain::HeightEncoding::Float32Le,
        samples_sha256: forge_core::bytes_sha256(&bytes),
    });
    if painted {
        let mut weights = vec![
            TerrainMaterialWeight {
                layer_id: document.material_layers[0].id.clone(),
                weight: 32_768,
            },
            TerrainMaterialWeight {
                layer_id: document.material_layers[1].id.clone(),
                weight: 32_767,
            },
        ];
        weights.sort_by(|a, b| a.layer_id.cmp(&b.layer_id));
        document.material_overrides = vec![
            TerrainMaterialOverride {
                sample_x: 2,
                sample_z: 3,
                weights,
            },
            TerrainMaterialOverride {
                sample_x: 30,
                sample_z: 31,
                weights: vec![TerrainMaterialWeight {
                    layer_id: document.material_layers[2].id.clone(),
                    weight: u16::MAX,
                }],
            },
        ];
    }
    Arc::new(SampledTerrainPlan::from_document(&document, &bytes).unwrap())
}

#[test]
fn absence_keeps_the_exact_program_and_allocates_no_paint_assets() {
    let plan = plan(false);
    assert!(plan.authored_material_weights(2, 3).is_none());
    let prepared = PreparedPaint::new(&plan).unwrap();
    assert!(prepared.weights.is_none());
    let original = "Original source program, including whitespace.\n";
    assert_eq!(
        prepared.shader(original).unwrap().as_bytes(),
        original.as_bytes()
    );
    let inputs = prepared.stage(&mut World::new()).unwrap();
    assert_eq!(inputs.weights, BUFFER);
    assert!(inputs.detail.is_none());
    assert_eq!(inputs.parameters.frame.w.to_bits(), 0.0_f32.to_bits());
    assert_eq!(inputs.parameters.shape, UVec4::ZERO);
}

#[test]
fn sparse_field_matches_existing_cell_weights_exactly_and_keeps_the_gaps_empty() {
    let plan = plan(true);
    let hash = plan.document_hash();
    let source = SampledCellSource::new(
        plan.clone(),
        CellStreamingPolicy {
            origin_xz_m: [-16.0; 2],
            cell_size_m: 32.0,
            dimensions: [1, 1],
            detail_radius: 0,
            far_radius: 1,
            max_resident_cells: 1,
            full_cell_bytes: 1_000_000,
            hlod_cell_bytes: 100_000,
            max_resident_bytes: 1_000_000,
            max_staging_bytes: 2_000_000,
        },
        8,
    )
    .unwrap();
    let mesh = source
        .build(CellKey { x: 0, z: 0 }, CellDetail::Full)
        .unwrap();
    let prepared = PreparedPaint::new(&plan).unwrap();
    assert_eq!(prepared.parameters.frame, Vec4::new(-15.0, -14.0, 1.0, 1.0));
    assert_eq!(prepared.parameters.shape, UVec4::new(31, 31, 0, 0));
    let data = prepared.weights.as_ref().unwrap().data.as_ref().unwrap();
    for sample in &plan.document().material_overrides {
        let index = usize::from(sample.sample_z) * 33 + usize::from(sample.sample_x);
        let x = usize::from(sample.sample_x - 2) + 1;
        let z = usize::from(sample.sample_z - 3) + 1;
        let at = (z * 31 + x) * 16;
        let values: Vec<_> = data[at..at + 16]
            .as_chunks::<4>()
            .0
            .iter()
            .map(|bytes| f32::from_le_bytes(*bytes))
            .collect();
        assert_eq!(values.as_slice(), mesh.material_weights[index]);
    }
    assert!(data[..16].iter().all(|b| *b == 0));
    assert!(
        data[(15 * 31 + 15) * 16..(15 * 31 + 16) * 16]
            .iter()
            .all(|b| *b == 0)
    );
    assert_eq!(hash, plan.document_hash());
}

#[test]
fn paint_field_budget_and_inverted_crops_refuse_before_any_allocation() {
    assert!(field_layout([0, 0], [4096, 4096]).is_err());
    assert!(field_layout([4, 0], [2, 8]).is_err());
    assert_eq!(field_layout([1, 1], [1, 1]).unwrap(), (3, 3, 144));
    assert!(field_layout([0, 0], [2048, 2048]).unwrap().2 < MAX_PAINT_FIELD_BYTES);
}

#[test]
fn paint_neutral_buffer_is_idempotent_and_refuses_a_colliding_identity() {
    let mut world = World::new();
    assert!(install_neutral(&mut world).is_err());
    world.init_resource::<Assets<ShaderBuffer>>();
    install_neutral(&mut world).unwrap();
    install_neutral(&mut world).unwrap();
    world
        .resource_mut::<Assets<ShaderBuffer>>()
        .get_mut(&BUFFER)
        .unwrap()
        .data
        .as_mut()
        .unwrap()[0] = 1;
    assert!(install_neutral(&mut world).is_err());
    assert_eq!(
        world
            .resource::<Assets<ShaderBuffer>>()
            .get(&BUFFER)
            .unwrap()
            .data
            .as_ref()
            .unwrap()[0],
        1
    );
}

#[test]
fn painted_shader_refuses_a_missing_seam_and_uniform_rows_match_the_gpu_contract() {
    let prepared = PreparedPaint::new(&plan(true)).unwrap();
    assert!(prepared.shader("sourceSurface(input,&surface);").is_err());
    assert_eq!(crate::terrain_palette::Parameters::min_size().get(), 112);
    assert_eq!(Parameters::min_size().get(), 144);
}
