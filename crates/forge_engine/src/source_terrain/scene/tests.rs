use super::*;
use forge_core::bytes_sha256;
use forge_project::{
    HeightSamplingAlgorithm, TERRAIN_DOCUMENT_SCHEMA_VERSION_V4, TerrainExtent, TerrainHeightDelta,
    authoring::new_terrain,
    sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
};
use std::collections::BTreeMap;

fn surface_package(side: u32, outside: f32) -> TerrainPackage {
    rectangular_package([side, side], 1.0, outside)
}

fn rectangular_package(size: [u32; 2], spacing: f32, outside: f32) -> TerrainPackage {
    let normal_bytes: usize = (0..=size[0].max(size[1]).ilog2())
        .map(|level| ((size[0] >> level).max(1) * (size[1] >> level).max(1) * 4) as usize)
        .sum();
    let payloads = BTreeMap::from([
        ("terrain-normal.bin", vec![0_u8; normal_bytes]),
        ("terrain-splat.bin", vec![0_u8; 4]),
        ("terrain-detail.bin", vec![0_u8; 4]),
        ("LICENSE.txt", b"test fixture".to_vec()),
    ]);
    let files: serde_json::Map<_, _> = payloads
        .iter()
        .map(|(name, data)| {
            (
                (*name).to_owned(),
                serde_json::json!({"bytes": data.len(), "sha256": bytes_sha256(data)}),
            )
        })
        .collect();
    let extent = size.map(|count| f64::from(count) * f64::from(spacing));
    let origin = extent.map(|length| -length * 0.5);
    let index = serde_json::to_vec(&serde_json::json!({
        "schema": "forge.terrain-surface-package", "version": 1,
        "raster": {"width": size[0], "height": size[1], "origin_xz_m": origin,
            "sample_spacing_m": spacing, "outside_height_m": outside,
            "encoding": "float32_le", "extent_m": extent},
        "textures": {"normal": size, "splat": [1, 1], "detail": [1, 1]},
        "files": files
    }))
    .unwrap();
    TerrainPackage::load(&index, bytes_sha256(&index), |name, _| {
        payloads
            .get(name)
            .cloned()
            .ok_or_else(|| "unexpected file".into())
    })
    .unwrap()
}

fn sampled_plan(size: [u16; 2], spacing: f32, outside: f32) -> (SampledTerrainPlan, Vec<u8>) {
    let heights = vec![0_u8; usize::from(size[0]) * usize::from(size[1]) * 4];
    let mut doc = new_terrain("sampled", "authoring", "Terrain shader").unwrap();
    doc.schema_version = TERRAIN_DOCUMENT_SCHEMA_VERSION_V4;
    doc.extent_m = TerrainExtent::new(f32::from(size[0]) * spacing, f32::from(size[1]) * spacing);
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
        width: u32::from(size[0]),
        height: u32::from(size[1]),
        origin_xz_m: [
            -f64::from(doc.extent_m.size_x_m) * 0.5,
            -f64::from(doc.extent_m.size_z_m) * 0.5,
        ],
        sample_spacing_m: f64::from(spacing),
        outside_height_m: outside,
        encoding: HeightEncoding::Float32Le,
        samples_sha256: bytes_sha256(&heights),
    });
    (
        SampledTerrainPlan::from_document(&doc, &heights).unwrap(),
        heights,
    )
}

#[test]
fn data_only_reference_profile_prepares_real_scene_and_other_domains_fail_closed() {
    let package = surface_package(2048, -90.0);
    assert!(package.file("terrain.wgsl").is_none());
    assert!(require_fixed_package(&package).is_ok());
    assert!(require_fixed_package(&surface_package(2, -90.0)).is_err());
    assert!(require_fixed_package(&surface_package(2, -12.0)).is_err());

    let (plan, heights) = sampled_plan([2048, 2048], 1.0, -90.0);
    let geometry = lod::ViewGeometry::new(&plan).unwrap();
    let prepared =
        PreparedTerrain::for_host(&plan, &heights, &package, &geometry, Some(0.0)).unwrap();
    assert!(prepared.geometry.is_some());
    assert_eq!(prepared.normal.len(), 22_369_620);
    assert!(!prepared.edited);
    assert_eq!(prepared.hash, plan.document_hash());
    let identity = package.identity();
    let plan = std::sync::Arc::new(plan);
    assert!(
        super::super::host::PreparedHost::new(
            plan.clone(),
            heights.clone(),
            package.clone(),
            bytes_sha256(b"other manifest"),
        )
        .is_err()
    );
    assert!(super::super::host::PreparedHost::new(plan, heights, package, identity).is_ok());
}

#[test]
fn rectangular_scene_preparation_keeps_matching_domain_edits_and_normal_dimensions() {
    use bevy::render::storage::ShaderBuffer;

    let (original, heights) = sampled_plan([11, 19], 0.5, -12.0);
    let mut document = original.document().clone();
    document.height_deltas = vec![TerrainHeightDelta::new(4, 7, 3.0)];
    let plan = SampledTerrainPlan::from_document(&document, &heights).unwrap();
    let package = rectangular_package([11, 19], 0.5, -12.0);
    let geometry = lod::ViewGeometry::new(&plan).unwrap();
    let prepared = PreparedTerrain::for_host(&plan, &heights, &package, &geometry, None).unwrap();
    assert_eq!(prepared.normal_dimensions, [11, 19]);
    assert_eq!(prepared.normal.len(), 1_060); // 11x19, 5x9, 2x4, 1x2, 1x1 RGBA.
    assert!(prepared.edited);
    assert!(prepared.normal[..11 * 19 * 4].iter().any(|byte| *byte != 0));
    assert!(
        PreparedTerrain::with_geometry(
            &plan,
            &rectangular_package([11, 19], 0.5, -90.0),
            None,
            None,
        )
        .is_err()
    );
    assert!(
        PreparedTerrain::with_geometry(
            &plan,
            &rectangular_package([11, 19], 1.0, -12.0),
            None,
            None,
        )
        .is_err()
    );
    assert!(PreparedTerrain::with_geometry(&plan, &package, None, Some(f64::NAN)).is_err());

    // This exercises CPU staging and asset ownership, without claiming a GPU render.
    let mut world = World::new();
    world.init_resource::<Assets<Image>>();
    world.init_resource::<Assets<Mesh>>();
    world.init_resource::<Assets<Shader>>();
    world.init_resource::<Assets<ShaderBuffer>>();
    world.init_resource::<Assets<LodMaterial>>();
    world.insert_resource(Registered);
    let image = world.resource_mut::<Assets<Image>>().add(Image::default());
    let bindings = Bindings {
        environment: image.clone(),
        hill_shadow: image.clone(),
        object_shadow: image.clone(),
        object_settings: default(),
        clouds: [Vec4::ZERO; 5],
        cloud_shadow: None,
        shore_state: image.clone(),
        shore_lace: image,
        shore_params: [UVec4::ZERO; 3],
        caustics: None,
        settings: TerrainSettings {
            wind: Vec4::ZERO,
            sun: Vec4::Y,
            domain: Vec4::new(-4.75, 9.5, 19.0, 0.58),
            flags: Vec4::ZERO,
        },
    };
    let resident = prepared.stage(&mut world, bindings.clone()).unwrap();
    let images = world.resource::<Assets<Image>>();
    let normal = images.get(&resident.normals.image).unwrap();
    assert_eq!([normal.width(), normal.height()], [11, 19]);
    assert_eq!(normal.texture_descriptor.mip_level_count, 5);
    let material = world
        .resource::<Assets<LodMaterial>>()
        .get(resident.material.as_ref().unwrap())
        .unwrap();
    assert_eq!(material.base.normal, resident.normals.image);
    let height = images.get(&material.extension.height).unwrap();
    let corrections = images.get(&material.extension.corrections).unwrap();
    assert_eq!([height.width(), height.height()], [11, 19]);
    assert_eq!([corrections.width(), corrections.height()], [12, 20]);
    assert!(
        !resident
            .normals
            .ready
            .load(std::sync::atomic::Ordering::Acquire)
    );
    assert!(
        PreparedRepaint::new(&plan, &package, &resident, None)
            .unwrap()
            .is_some()
    );
    assert!(
        PreparedRepaint::new(&plan, &package, &resident, Some(3.5))
            .unwrap()
            .is_none()
    );
    assert!(PreparedRepaint::new(&plan, &package, &resident, Some(f64::NAN)).is_err());

    let mut invalid = bindings;
    invalid.environment = bevy::asset::uuid_handle!("2ad336a7-c709-4e75-9f35-2cfbca140249");
    let rejected = PreparedTerrain::with_geometry(&plan, &package, None, None).unwrap();
    assert!(rejected.stage(&mut world, invalid).is_err());
    assert_eq!(world.resource::<ResidentTerrain>().entity, resident.entity);
}

#[test]
fn rectangular_host_requires_an_explicit_finite_water_context() {
    let (plan, heights) = sampled_plan([11, 19], 0.5, -12.0);
    let shared_plan = std::sync::Arc::new(plan);
    let package = rectangular_package([11, 19], 0.5, -12.0);
    let make_host = || {
        super::super::host::PreparedHost::new(
            shared_plan.clone(),
            heights.clone(),
            package.clone(),
            package.identity(),
        )
        .unwrap()
    };
    assert!(make_host().requires_water_context());
    assert!(make_host().install(&mut App::new()).is_err());
    assert!(make_host().with_water_level(Some(f64::NAN)).is_err());
    assert!(
        !make_host()
            .with_water_level(None)
            .unwrap()
            .requires_water_context()
    );
    assert!(
        !make_host()
            .with_water_level(Some(3.5))
            .unwrap()
            .requires_water_context()
    );
}
