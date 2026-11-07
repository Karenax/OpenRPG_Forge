//! Real source inputs, with a second independent raster and explicit source pins.
use forge_assets::{
    source_ocean_package::OceanPackage, source_shore_sim_package::ShoreSimPackage,
    source_water_surface_package::WaterSurfacePackage,
};
use forge_core::bytes_sha256;
use forge_engine::source_shore_sim::PreparedShoreSim;
use forge_engine::source_water_optics::PreparedOptics;
use forge_project::{
    HeightSamplingAlgorithm, TERRAIN_DOCUMENT_SCHEMA_VERSION_V4, TerrainDocument, TerrainExtent,
    TerrainHeightDelta,
    authoring::new_terrain,
    sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
};
use forge_world::sampled_terrain::SampledTerrainPlan;
use std::{fs, path::PathBuf, sync::Arc};

fn document(heights: &[u8], key: &str) -> TerrainDocument {
    let mut doc = new_terrain(key, "shore", "Qualification shore").unwrap();
    doc.schema_version = TERRAIN_DOCUMENT_SCHEMA_VERSION_V4;
    doc.extent_m = TerrainExtent::new(2048.0, 2048.0);
    doc.height_sampling.algorithm = HeightSamplingAlgorithm::SampledRasterV1;
    doc.height_sampling.sample_spacing_m = 1.0;
    doc.height_sampling.base_elevation_m = 0.0;
    doc.height_sampling.amplitude_m = 0.0;
    doc.height_sampling.base_frequency_cycles_per_m = 1.0;
    doc.height_sampling.octaves = 1;
    doc.height_sampling.lacunarity = 2.0;
    doc.height_sampling.persistence = 0.5;
    doc.sampled_source = Some(SampledTerrainSource {
        schema: SAMPLED_TERRAIN_SCHEMA.into(),
        schema_version: 1,
        width: 2048,
        height: 2048,
        origin_xz_m: [-1024.0; 2],
        sample_spacing_m: 1.0,
        outside_height_m: -90.0,
        encoding: HeightEncoding::Float32Le,
        samples_sha256: bytes_sha256(heights),
    });
    doc
}

fn packages() -> (ShoreSimPackage, WaterSurfacePackage) {
    let shore_root = PathBuf::from(std::env::var_os("FORGE_SHORE_PACKAGE").expect("shore package"));
    let water_root = PathBuf::from(
        std::env::var_os("FORGE_WATER_SURFACE_PACKAGE").expect("water surface package"),
    );
    let shore_pin: forge_core::ContentHash = std::env::var("FORGE_SHORE_PACKAGE_SHA256")
        .unwrap()
        .parse()
        .unwrap();
    let water_pin: forge_core::ContentHash = std::env::var("FORGE_WATER_SURFACE_PACKAGE_SHA256")
        .unwrap()
        .parse()
        .unwrap();
    assert!(shore_root.is_absolute() && water_root.is_absolute());
    let shore_manifest = fs::read(shore_root.join("package.json")).unwrap();
    let water_manifest = fs::read(water_root.join("package.json")).unwrap();
    let shore = ShoreSimPackage::load(&shore_manifest, shore_pin, |name, _| {
        fs::read(shore_root.join(name)).map_err(|e| e.to_string())
    })
    .unwrap();
    let water = WaterSurfacePackage::load(&water_manifest, water_pin, |name, _| {
        fs::read(water_root.join(name)).map_err(|e| e.to_string())
    })
    .unwrap();
    (shore, water)
}

fn altered_surface() -> WaterSurfacePackage {
    let root = PathBuf::from(std::env::var_os("FORGE_WATER_SURFACE_PACKAGE").unwrap());
    let mut index: serde_json::Value =
        serde_json::from_slice(&fs::read(root.join("package.json")).unwrap()).unwrap();
    let mut altered = fs::read(root.join("uniform-mat.bin")).unwrap();
    altered[0] ^= 1;
    index["files"]["uniform-mat.bin"]["sha256"] = serde_json::json!(bytes_sha256(&altered));
    let bytes = serde_json::to_vec(&index).unwrap();
    WaterSurfacePackage::load(&bytes, bytes_sha256(&bytes), |name, _| {
        if name == "uniform-mat.bin" {
            Ok(altered.clone())
        } else {
            fs::read(root.join(name)).map_err(|e| e.to_string())
        }
    })
    .unwrap()
}

/// Compare the existing reference's smoothed beach with generic contours on
/// exactly the same pinned heights. This is geometry evidence, not image parity.
#[test]
#[ignore = "Requires explicit admitted shore, water and source breaker packages"]
#[allow(
    clippy::too_many_lines,
    reason = "Compare both shoreline domains against the same pinned reference"
)]
fn reference_beach_and_general_contours_share_the_admitted_terrain() {
    let (shore, _) = packages();
    let heights = shore.file("heights.f32.bin").unwrap();
    let doc = document(heights, "shore-geometry-comparison");
    let plan = SampledTerrainPlan::from_document(&doc, heights).unwrap();
    let source = PathBuf::from(std::env::var_os("FORGE_BREAKER_SOURCE_DIR").unwrap());
    assert!(source.is_absolute());
    let station_bytes = fs::read(source.join("stations.f32.bin")).unwrap();
    assert_eq!(
        bytes_sha256(&station_bytes).to_string(),
        "0c8d25b4f743529a97a9264daafc17559184112025087d170a1da4a3fa99fe2b"
    );
    let reference =
        forge_tidewater::breaker_geometry::beach_stations(|x, z| plan.height_at(x, z).unwrap());
    let reference_bytes: Vec<_> = reference
        .iter()
        .flatten()
        .flat_map(|v| v.to_le_bytes())
        .collect();
    assert_eq!(reference_bytes, station_bytes);
    let quantile = |values: &mut Vec<f64>, n: usize, d: usize| {
        values.sort_by(f64::total_cmp);
        values[(values.len() - 1) * n / d]
    };
    let angle = |a: &[f32; 4], b: &[f32; 4]| {
        let dot = f64::from(a[2]) * f64::from(b[2]) + f64::from(a[3]) * f64::from(b[3]);
        dot.clamp(-1.0, 1.0).acos().to_degrees()
    };
    let mut reference_turns: Vec<_> = reference.windows(2).map(|w| angle(&w[0], &w[1])).collect();
    let mut reports = Vec::new();
    for radius in [240.0, 304.0] {
        let center = [8.0, -85.0];
        let config = forge_engine::source_breaker_contours::ContourConfig {
            min: [center[0] - radius, center[1] - radius],
            max: [center[0] + radius, center[1] + radius],
            cells: [if radius < 300.0 { 480 } else { 608 }; 2],
            water_level: 0.0,
            station_spacing: 0.6,
        };
        let contours = forge_engine::source_breaker_contours::shore_contours(config, |x, z| {
            plan.height_at(x, z).unwrap()
        })
        .unwrap();
        let all: Vec<_> = contours.iter().flat_map(|c| c.stations.iter()).collect();
        assert!(!all.is_empty());
        let mut turns: Vec<_> = contours
            .iter()
            .flat_map(|c| c.stations.windows(2).map(|w| angle(&w[0], &w[1])))
            .collect();
        let mut matched_turns: Vec<_> = contours
            .iter()
            .flat_map(|c| c.stations.windows(2))
            .filter(|w| {
                let x = f64::midpoint(f64::from(w[0][0]), f64::from(w[1][0]));
                let z = f64::midpoint(f64::from(w[0][1]), f64::from(w[1][1]));
                reference
                    .iter()
                    .any(|s| (x - f64::from(s[0])).hypot(z - f64::from(s[1])) <= 1.0)
            })
            .map(|w| angle(&w[0], &w[1]))
            .collect();
        let mut nearest_distance = Vec::new();
        let mut nearest_angle = Vec::new();
        for station in &reference {
            if (0..2).any(|axis| {
                f64::from(station[axis]) <= config.min[axis] + 1.0
                    || f64::from(station[axis]) >= config.max[axis] - 1.0
            }) {
                continue;
            }
            let (nearest, distance) = all
                .iter()
                .map(|candidate| {
                    let dx = f64::from(station[0] - candidate[0]);
                    let dz = f64::from(station[1] - candidate[1]);
                    (*candidate, dx.hypot(dz))
                })
                .min_by(|a, b| a.1.total_cmp(&b.1))
                .unwrap();
            nearest_distance.push(distance);
            nearest_angle.push(angle(station, nearest));
        }
        assert!(!nearest_distance.is_empty() && !turns.is_empty() && !matched_turns.is_empty());
        reports.push(
            serde_json::json!({"radius_m":radius,"contours":contours.len(),"stations":all.len(),
            "reference_compared":nearest_distance.len(),
            "nearest_distance_median_m":quantile(&mut nearest_distance,1,2),
            "nearest_distance_p95_m":quantile(&mut nearest_distance,95,100),
            "nearest_normal_angle_median_degrees":quantile(&mut nearest_angle,1,2),
            "nearest_normal_angle_p95_degrees":quantile(&mut nearest_angle,95,100),
            "near_reference_segment_count":matched_turns.len(),
            "near_reference_adjacent_normal_angle_p95_degrees":quantile(&mut matched_turns,95,100),
            "contour_adjacent_normal_angle_p95_degrees":quantile(&mut turns,95,100)}),
        );
    }
    println!(
        "SHORE_GEOMETRY_COMPARISON {}",
        serde_json::json!({"reference_stations":reference.len(),
        "reference_adjacent_normal_angle_p95_degrees":quantile(&mut reference_turns,95,100),"windows":reports})
    );
}

#[test]
#[ignore = "Requires explicit admitted ocean, shore and water surface packages"]
fn common_water_host_prepares_the_entire_admitted_chain_for_the_selected_terrain() {
    let root = PathBuf::from(std::env::var_os("FORGE_OCEAN_PACKAGE").expect("ocean package"));
    assert!(root.is_absolute());
    let pin: forge_core::ContentHash = std::env::var("FORGE_OCEAN_PACKAGE_SHA256")
        .unwrap()
        .parse()
        .unwrap();
    let index = fs::read(root.join("package.json")).unwrap();
    let ocean = OceanPackage::load(&index, pin, |name, _| {
        fs::read(root.join(name)).map_err(|error| error.to_string())
    })
    .unwrap();
    let (shore, surface) = packages();
    let heights = shore.file("heights.f32.bin").unwrap();
    let terrain = document(heights, "water-host-module");
    let plan = Arc::new(SampledTerrainPlan::from_document(&terrain, heights).unwrap());
    let prepared = forge_engine::source_water_host::PreparedHost::new(
        &ocean, &shore, surface, &terrain, &plan, heights,
    )
    .unwrap();
    assert_eq!(prepared.document_hash(), plan.document_hash());
    let resident = prepared
        .prepare_resident_scene(plan.clone(), [0.0, 0.0])
        .unwrap();
    assert_eq!(resident.document_hash(), plan.document_hash());
    assert!(resident.station_count() <= 4096);
    let mut incomplete_app = bevy::prelude::App::new();
    assert!(resident.install(&mut incomplete_app, true).is_err());
    assert!(
        !incomplete_app
            .world()
            .contains_resource::<forge_engine::source_ocean::Targets>()
    );
    assert!(
        !incomplete_app
            .world()
            .contains_resource::<forge_engine::source_shore_sim::Targets>()
    );
    let (_, surface) = packages();
    let relocated = forge_engine::source_water_host::PreparedHost::new(
        &ocean, &shore, surface, &terrain, &plan, heights,
    )
    .unwrap()
    .with_shore_simulation_center([550.0, -300.0])
    .unwrap();
    let effects = relocated
        .breakers_near_camera(&plan, [550.0, -300.0])
        .unwrap();
    let simulation_params = effects.shore_gpu_inputs().simulation_params.to_vec();
    let effect_params = effects
        .gpu_inputs()
        .files
        .file("shore-sim-params.bin")
        .unwrap()
        .to_vec();
    assert_eq!(simulation_params, effect_params);
    assert_eq!(&simulation_params[0..4], &360.0_f32.to_le_bytes());
    assert_eq!(&simulation_params[4..8], &(-490.0_f32).to_le_bytes());
    assert!(
        relocated
            .prepare_resident_scene(plan.clone(), [550.0, -300.0])
            .is_ok()
    );
    let (_, surface) = packages();
    let invalid_center = forge_engine::source_water_host::PreparedHost::new(
        &ocean, &shore, surface, &terrain, &plan, heights,
    )
    .unwrap();
    assert!(
        invalid_center
            .prepare_resident_scene(plan.clone(), [f64::NAN, 0.0])
            .is_err()
    );
    let (_, surface) = packages();
    let mut changed = terrain.clone();
    changed
        .height_deltas
        .push(TerrainHeightDelta::new(2, 2, 1.0));
    assert!(
        forge_engine::source_water_host::PreparedHost::new(
            &ocean, &shore, surface, &changed, &plan, heights,
        )
        .is_err()
    );
}

#[test]
#[ignore = "Requires explicit admitted source shore and water surface packages"]
fn relocated_shore_window_is_shared_with_optics_before_publication() {
    let (shore, water) = packages();
    let heights = shore.file("heights.f32.bin").unwrap();
    let doc = document(heights, "relocated-shore");
    let plan = SampledTerrainPlan::from_document(&doc, heights).unwrap();
    let original = shore.file("simulation-params.bin").unwrap();
    let prepared = PreparedShoreSim::new(&shore, &water, &doc, &plan, heights).unwrap();
    let old_generation = prepared.gpu_inputs().generation.unwrap();
    let moved = prepared.with_simulation_center([550.0, -300.0]).unwrap();
    let inputs = moved.gpu_inputs();
    let parameters = inputs.simulation_params;
    assert_eq!(&parameters[0..4], &360.0_f32.to_le_bytes());
    assert_eq!(&parameters[4..8], &(-490.0_f32).to_le_bytes());
    assert_eq!(&parameters[8..], &original[8..]);
    assert!(!Arc::ptr_eq(&old_generation, &inputs.generation.unwrap()));
    let optics =
        PreparedOptics::new(&water, &doc, &plan, Some(moved.shore_field()), Some(&shore)).unwrap();
    let optical_bytes: Vec<u8> = optics
        .simulation_parameters()
        .iter()
        .flat_map(bevy::math::UVec4::to_array)
        .flat_map(u32::to_le_bytes)
        .collect();
    assert_eq!(optical_bytes, parameters);

    let shared = moved.clone();
    assert!(moved.with_simulation_center([0.0, 0.0]).is_err());
    assert_eq!(shared.gpu_inputs().simulation_params, optical_bytes);
}

#[test]
#[ignore = "Requires explicit admitted source shore and water surface packages"]
#[allow(
    clippy::too_many_lines,
    reason = "One pinned fixture compares the source, independent terrain and edited revision."
)]
fn source_shore_is_reused_only_for_its_raster_and_other_terrain_is_recomputed() {
    let (shore, water) = packages();
    let original = shore.file("heights.f32.bin").unwrap();
    assert_eq!(shore.source_height_sha256(), bytes_sha256(original));
    assert_eq!(shore.shore_field_sha256(), water.shore_field_sha256());
    let doc = document(original, "source");
    let plan = SampledTerrainPlan::from_document(&doc, original).unwrap();
    let prepared = PreparedShoreSim::new(&shore, &water, &doc, &plan, original).unwrap();
    assert!(!prepared.shore_field().is_derived());
    assert_eq!(
        prepared.shore_field().bytes(),
        water.file("shore-field.rgba32.bin").unwrap()
    );
    let optics = PreparedOptics::new(
        &water,
        &doc,
        &plan,
        Some(prepared.shore_field()),
        Some(&shore),
    )
    .unwrap();
    assert_eq!(
        optics.shore_field(),
        (prepared.shore_field().bytes(), false)
    );
    let other_surface = altered_surface();
    assert_eq!(
        water.shore_field_sha256(),
        other_surface.shore_field_sha256()
    );
    assert_ne!(water.identity(), other_surface.identity());
    assert!(
        PreparedOptics::new(
            &other_surface,
            &doc,
            &plan,
            Some(prepared.shore_field()),
            Some(&shore)
        )
        .is_err()
    );
    let optics_without_sim = PreparedOptics::new(&water, &doc, &plan, None, Some(&shore)).unwrap();
    assert_eq!(
        optics_without_sim.shore_field(),
        (prepared.shore_field().bytes(), false)
    );
    let unverified = PreparedOptics::new(&water, &doc, &plan, None, None).unwrap();
    assert!(unverified.shore_field().1);

    let foreign: Vec<u8> = std::iter::repeat_n((-10.0_f32).to_le_bytes(), 2048 * 2048)
        .flatten()
        .collect();
    let other = document(&foreign, "other-module");
    let other_plan = SampledTerrainPlan::from_document(&other, &foreign).unwrap();
    let other_shore = PreparedShoreSim::new(&shore, &water, &other, &other_plan, &foreign).unwrap();
    assert!(other_shore.shore_field().is_derived());
    assert_ne!(
        other_shore.shore_field().bytes(),
        prepared.shore_field().bytes()
    );
    let other_optics = PreparedOptics::new(
        &water,
        &other,
        &other_plan,
        Some(other_shore.shore_field()),
        Some(&shore),
    )
    .unwrap();
    assert_eq!(
        other_optics.shore_field(),
        (other_shore.shore_field().bytes(), true)
    );
    let without_sim = PreparedOptics::new(&water, &other, &other_plan, None, Some(&shore)).unwrap();
    assert_eq!(
        without_sim.shore_field(),
        (other_shore.shore_field().bytes(), true)
    );
    assert!(
        PreparedOptics::new(
            &water,
            &other,
            &other_plan,
            Some(prepared.shore_field()),
            Some(&shore)
        )
        .is_err()
    );
    assert!(PreparedShoreSim::new(&shore, &water, &other, &plan, &foreign).is_err());

    let mut edited = other.clone();
    edited
        .height_deltas
        .push(TerrainHeightDelta::new(2, 2, 1.0));
    let edited_plan = other_plan.with_document(&edited).unwrap();
    assert!(
        PreparedOptics::new(
            &water,
            &edited,
            &edited_plan,
            Some(other_shore.shore_field()),
            Some(&shore)
        )
        .is_err()
    );
    let edited_shore =
        PreparedShoreSim::new(&shore, &water, &edited, &edited_plan, &foreign).unwrap();
    assert!(edited_shore.shore_field().is_derived());
    let edited_optics = PreparedOptics::new(
        &water,
        &edited,
        &edited_plan,
        Some(edited_shore.shore_field()),
        Some(&shore),
    )
    .unwrap();
    assert_eq!(
        edited_optics.shore_field(),
        (edited_shore.shore_field().bytes(), true)
    );
    let mut misaligned = other;
    misaligned.sampled_source.as_mut().unwrap().origin_xz_m[0] += 1.0;
    assert!(SampledTerrainPlan::from_document(&misaligned, &foreign).is_err());
}
