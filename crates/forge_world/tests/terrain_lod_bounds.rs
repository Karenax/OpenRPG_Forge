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
    use forge_world::terrain_lod_bounds::RasterLodBounds;
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
    assert_eq!(bounds.bounds_for([-4.0; 2], 4.0).unwrap(), [-91.0, 65.0]);
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
    assert_eq!(bounds.bounds_for([-4.0; 2], 4.0).unwrap(), [-95.0, 79.0]);
    assert!(bounds.bounds_for([f64::NAN, 0.0], 4.0).is_err());
}

#[test]
fn rectangular_and_partial_tiles_conservatively_cover_sampled_heights() {
    use forge_world::terrain_lod_bounds::RasterLodBounds;

    for (width, height, spacing) in [
        (11_u16, 19_u16, 1.0_f32),
        (8, 16, 0.5),
        (31, 7, 2.0),
        (4, 4, 1.0),
    ] {
        let (mut document, _) = fixture();
        document.extent_m =
            TerrainExtent::new(f32::from(width) * spacing, f32::from(height) * spacing);
        document.height_sampling.sample_spacing_m = spacing;
        let bytes: Vec<_> = (0..height)
            .flat_map(|z| (0..width).map(move |x| f32::from((x * 7 + z * 13) % 31) - 15.0))
            .flat_map(f32::to_le_bytes)
            .collect();
        let source = document.sampled_source.as_mut().unwrap();
        source.width = u32::from(width);
        source.height = u32::from(height);
        source.origin_xz_m = [
            -f64::from(width) * f64::from(spacing) * 0.5,
            -f64::from(height) * f64::from(spacing) * 0.5,
        ];
        source.sample_spacing_m = f64::from(spacing);
        source.samples_sha256 = bytes_sha256(&bytes);
        let source_origin = source.origin_xz_m;
        let plan = SampledTerrainPlan::from_document(&document, &bytes).unwrap();
        let bounds = RasterLodBounds::from_plan(&plan).unwrap();
        for x in [-0.5, 0.0, 0.4, 3.75, 7.9, 8.0, 10.0, 15.9, 30.5] {
            for z in [-0.5, 0.0, 0.4, 3.75, 7.9, 8.0, 10.0, 15.9, 30.5] {
                for size in [0.125, 1.0, 5.0, 12.0] {
                    let size = size * f64::from(spacing);
                    let origin = [
                        source_origin[0] + x * f64::from(spacing),
                        source_origin[1] + z * f64::from(spacing),
                    ];
                    let [low, high] = bounds.bounds_for(origin, size).unwrap();
                    for dx in [0.0, 0.25, 0.5, 0.75, 1.0] {
                        for dz in [0.0, 0.25, 0.5, 0.75, 1.0] {
                            let value = plan
                                .height_at(origin[0] + size * dx, origin[1] + size * dz)
                                .unwrap();
                            assert!(
                                (low..=high).contains(&value),
                                "{width}x{height} at {origin:?}, size={size}: {value} outside [{low}, {high}]"
                            );
                        }
                    }
                }
            }
        }
    }
}

#[test]
fn lod_bounds_cover_interpolation_across_internal_tile_edges() {
    use forge_world::terrain_lod_bounds::RasterLodBounds;

    let (mut document, _) = fixture();
    document.extent_m = TerrainExtent::new(16.0, 16.0);
    let mut heights = vec![0.0_f32; 16 * 16];
    // On the x=0 tile boundary, bilinear interpolation uses samples from
    // columns 7 and 8. The left sample must contribute to the right tile bound.
    heights[3 * 16 + 7] = -1000.0;
    heights[4 * 16 + 7] = -1000.0;
    // The same condition applies across a z tile boundary.
    heights[7 * 16 + 3] = 1000.0;
    heights[7 * 16 + 4] = 1000.0;
    let bytes: Vec<_> = heights.into_iter().flat_map(f32::to_le_bytes).collect();
    let source = document.sampled_source.as_mut().unwrap();
    source.width = 16;
    source.height = 16;
    source.origin_xz_m = [-8.0; 2];
    source.samples_sha256 = bytes_sha256(&bytes);
    let plan = SampledTerrainPlan::from_document(&document, &bytes).unwrap();
    let bounds = RasterLodBounds::from_plan(&plan).unwrap();
    for (position, query_origin) in [([0.0, -4.0], [0.0, -4.0]), ([-4.0, 0.0], [-4.0, 0.0])] {
        let height = plan.height_at(position[0], position[1]).unwrap();
        let [minimum, maximum] = bounds.bounds_for(query_origin, 0.25).unwrap();
        assert!(
            (minimum..=maximum).contains(&height),
            "height {height} at {position:?} escapes [{minimum}, {maximum}]"
        );
    }
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
