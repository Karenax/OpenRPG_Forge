use forge_core::bytes_sha256;
use forge_project::{
    HeightSamplingAlgorithm, TERRAIN_DOCUMENT_SCHEMA_VERSION_V4, TerrainDocument, TerrainExtent,
    TerrainHeightDelta,
    authoring::new_terrain,
    sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
};
use forge_world::sampled_terrain::SampledTerrainPlan;

#[test]
#[allow(clippy::float_cmp)]
fn lod_bounds_include_source_border_and_author_adjustments() {
    use forge_tidewater_assets::terrain_bounds::RasterLodBounds;
    let (mut document, _) = fixture();
    let bytes: Vec<_> = (0_u16..64)
        .flat_map(|v| f32::from(v).to_le_bytes())
        .collect();
    document.extent_m = TerrainExtent::new(8.0, 8.0);
    let source = document.sampled_source.as_mut().unwrap();
    source.width = 8;
    source.height = 8;
    source.origin_xz_m = [-4.0; 2];
    source.samples_sha256 = bytes_sha256(&bytes);
    let bounds =
        RasterLodBounds::from_plan(&SampledTerrainPlan::from_document(&document, &bytes).unwrap())
            .unwrap();
    assert_eq!(bounds.bounds_for([-4.0; 2], 4.0).unwrap(), [-1.0, 65.0]);
    assert_eq!(bounds.bounds_for([-4.0; 2], 8.0).unwrap(), [-91.0, 65.0]);
    assert_eq!(bounds.bounds_for([20.0; 2], 4.0).unwrap(), [-91.0, -88.0]);
    document.height_sampling.base_elevation_m = 3.0;
    document.height_deltas = vec![
        TerrainHeightDelta::new(1, 1, -7.0),
        TerrainHeightDelta::new(2, 2, 11.0),
    ];
    let bounds =
        RasterLodBounds::from_plan(&SampledTerrainPlan::from_document(&document, &bytes).unwrap())
            .unwrap();
    assert_eq!(bounds.bounds_for([-4.0; 2], 4.0).unwrap(), [-5.0, 79.0]);
    assert!(bounds.bounds_for([f64::NAN, 0.0], 4.0).is_err());
}

fn fixture() -> (TerrainDocument, Vec<u8>) {
    let bytes: Vec<_> = (0_u16..16)
        .flat_map(|v| f32::from(v).to_le_bytes())
        .collect();
    let mut document = new_terrain("sampled", "authoring", "Raster").unwrap();
    document.schema_version = TERRAIN_DOCUMENT_SCHEMA_VERSION_V4;
    document.extent_m = TerrainExtent::new(4.0, 4.0);
    document.height_sampling.algorithm = HeightSamplingAlgorithm::SampledRasterV1;
    document.height_sampling.sample_spacing_m = 1.0;
    document.height_sampling.base_elevation_m = 0.0;
    document.height_sampling.amplitude_m = 0.0;
    document.height_sampling.base_frequency_cycles_per_m = 1.0;
    document.height_sampling.octaves = 1;
    document.height_sampling.lacunarity = 2.0;
    document.height_sampling.persistence = 0.5;
    document.sampled_source = Some(SampledTerrainSource {
        schema: SAMPLED_TERRAIN_SCHEMA.into(),
        schema_version: 1,
        width: 4,
        height: 4,
        origin_xz_m: [-2.0; 2],
        sample_spacing_m: 1.0,
        outside_height_m: -90.0,
        encoding: HeightEncoding::Float32Le,
        samples_sha256: bytes_sha256(&bytes),
    });
    (document, bytes)
}
