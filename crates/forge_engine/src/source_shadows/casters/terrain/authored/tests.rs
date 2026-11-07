use super::*;
use bevy::{math::DVec3, prelude::Mat4};
use forge_core::bytes_sha256;
use forge_project::{
    HeightSamplingAlgorithm, TERRAIN_DOCUMENT_SCHEMA_VERSION_V4, TerrainExtent, TerrainHeightDelta,
    authoring::new_terrain,
    sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
};

const LOD: &str = include_str!("../../../../source_terrain/engine_lod.wgsl");

fn fixture(width: u16, height: u16, spacing: f32, outside: f32) -> (TerrainDocument, Vec<u8>) {
    let heights: Vec<_> = (0..usize::from(width) * usize::from(height))
        .flat_map(|index| f32::from(u8::try_from(index % 7).unwrap()).to_le_bytes())
        .collect();
    let mut doc = new_terrain("shadow-test", "authoring", "Shadow test").unwrap();
    doc.schema_version = TERRAIN_DOCUMENT_SCHEMA_VERSION_V4;
    doc.extent_m = TerrainExtent::new(f32::from(width) * spacing, f32::from(height) * spacing);
    doc.height_sampling.algorithm = HeightSamplingAlgorithm::SampledRasterV1;
    doc.height_sampling.sample_spacing_m = spacing;
    doc.height_sampling.base_elevation_m = 0.0;
    doc.height_sampling.amplitude_m = 0.0;
    doc.height_sampling.base_frequency_cycles_per_m = 1.0;
    doc.height_sampling.octaves = 1;
    doc.height_sampling.lacunarity = 2.0;
    doc.height_sampling.persistence = 0.5;
    doc.sampled_source = Some(SampledTerrainSource {
        schema: SAMPLED_TERRAIN_SCHEMA.into(),
        schema_version: 1,
        width: u32::from(width),
        height: u32::from(height),
        origin_xz_m: [
            -f64::from(doc.extent_m.size_x_m) * 0.5,
            -f64::from(doc.extent_m.size_z_m) * 0.5,
        ],
        sample_spacing_m: f64::from(spacing),
        outside_height_m: outside,
        encoding: HeightEncoding::Float32Le,
        samples_sha256: bytes_sha256(&heights),
    });
    (doc, heights)
}

fn view() -> CascadeView {
    CascadeView {
        world_from_view: DMat4::from_translation(DVec3::new(0.0, 2.0, 20.0)),
        near: 0.1,
        fov_degrees: 60.0,
        aspect: 1.6,
    }
}

#[test]
fn rectangular_terrain_prepares_shadow_textures_edits_and_matching_boundary() {
    let (mut doc, heights) = fixture(11, 19, 0.5, -12.0);
    doc.height_deltas.push(TerrainHeightDelta::new(3, 4, 2.0));
    let prepared = PreparedSource::new(&doc, heights.clone(), LOD).unwrap();
    let input = prepared.at(view()).unwrap();
    assert_eq!(input.height_size, [11, 19]);
    assert_eq!([input.edit_side, input.edit_height], [12, 20]);
    let offset = (4 * 12 + 3) * 4;
    assert_eq!(&input.edits[offset..offset + 4], &2.0_f32.to_le_bytes());
    assert!(input.count > 0);
    assert!(input.shader.contains("vec2f(-2.75,-4.75))/0.5"));
    assert!(
        input
            .shader
            .contains("forgeTerrainClip(cv.worldXZ,forgeUnmorphedXZ)")
    );
    assert!(input.shader.contains("vec2f(-2.75,-4.75)"));
    assert!(input.validate(&[Mat4::IDENTITY]).is_ok());
    let module = naga::front::wgsl::parse_str(&input.shader).unwrap();
    naga::valid::Validator::new(
        naga::valid::ValidationFlags::all(),
        naga::valid::Capabilities::all(),
    )
    .validate(&module)
    .unwrap();

    let mut invalid = input;
    invalid.edit_height -= 1;
    assert!(invalid.validate(&[Mat4::IDENTITY]).is_err());
    let mut bad_heights = heights;
    bad_heights[0] ^= 1;
    assert!(PreparedSource::new(&doc, bad_heights, LOD).is_err());
}

#[test]
fn reference_shader_is_unchanged_and_keeps_legacy_dimensions() {
    let (doc, heights) = fixture(2048, 2048, 1.0, -90.0);
    let (_, functions) = LOD.split_once("fn terrainHeightAt(").unwrap();
    let original = format!("{HEADER}\nfn terrainHeightAt({functions}");
    let expected = crate::sampled_terrain_corrections::Corrections::from_document(&doc)
        .unwrap()
        .shader(&original, "0", 2)
        .unwrap();
    let prepared = PreparedSource::new(&doc, heights, LOD).unwrap();
    assert_eq!(prepared.shader, expected);
    let input = prepared.at(view()).unwrap();
    assert_eq!(input.height_size, [2048, 2048]);
    assert_eq!([input.edit_side, input.edit_height], [1, 1]);
    assert!(input.validate(&[Mat4::IDENTITY]).is_ok());
}
