//! The module owns its explicit inputs; the common engine owns the water renderer.
use forge_delivery::module_runtime::LoadedModuleZone;
use forge_engine::source_water_host::PreparedHost;

pub fn prepare(
    loaded: &LoadedModuleZone,
    sampled: Option<&super::streamed::Prepared>,
    environment: bool,
    terrain: bool,
) -> Result<Option<PreparedHost>, String> {
    let Some(resources) = loaded.ocean()? else {
        return Ok(None);
    };
    if !environment || !terrain {
        return Err(
            "L'eau nécessite le terrain et l'environnement source admis de cette zone.".into(),
        );
    }
    let sampled = sampled.ok_or("L'eau nécessite le raster admis de cette zone.")?;
    let surface = resources
        .surface
        .ok_or("La surface d'eau est absente du module.")?;
    let shore = resources
        .shore_sim
        .ok_or("Le ressac est absent du module.")?;
    let heights = loaded
        .source_blobs
        .get(&sampled.plan.raster().source().samples_sha256)
        .ok_or("Le raster de l'eau est absent du module.")?;
    PreparedHost::new(
        &resources.package,
        &shore,
        surface,
        sampled.plan.document(),
        &sampled.plan,
        heights,
    )
    .map(Some)
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_core::AreaId;
    use forge_world::sampled_terrain::SampledTerrainPlan;
    use std::{path::PathBuf, sync::Arc};

    #[test]
    #[ignore = "Requires an explicitly pinned installed module with real ocean inputs"]
    #[allow(
        clippy::too_many_lines,
        reason = "One admitted module and its derived shoreline are verified together."
    )]
    fn installed_module_prepares_only_its_declared_zone_and_requires_its_hosts() {
        let package = PathBuf::from(std::env::var_os("FORGE_WATER_MODULE").unwrap());
        let cache = PathBuf::from(std::env::var_os("FORGE_WATER_MODULE_CACHE").unwrap());
        assert!(package.is_absolute() && cache.is_absolute());
        assert_eq!(
            forge_delivery::file_hash(&package).unwrap(),
            std::env::var("FORGE_WATER_MODULE_SHA256").unwrap()
        );
        let area: AreaId = std::env::var("FORGE_WATER_MODULE_AREA")
            .unwrap()
            .parse()
            .unwrap();
        let loaded = forge_delivery::module_runtime::load(&package, area, Some(&cache)).unwrap();
        assert!(loaded.environment().unwrap().is_some());
        assert!(loaded.terrain().unwrap().is_some());
        let terrain = loaded.zone.head().effective().unwrap().0;
        let bytes = loaded
            .source_blobs
            .get(&terrain.sampled_source.as_ref().unwrap().samples_sha256)
            .unwrap();
        let plan = Arc::new(SampledTerrainPlan::from_document(&terrain, bytes).unwrap());
        let sampled = super::super::streamed::Prepared {
            policy: super::super::streamed::policy(plan.raster().source()).unwrap(),
            plan,
        };
        assert!(prepare(&loaded, Some(&sampled), false, true).is_err());
        assert!(prepare(&loaded, Some(&sampled), true, false).is_err());
        assert!(prepare(&loaded, None, true, true).is_err());
        let prepared = prepare(&loaded, Some(&sampled), true, true)
            .unwrap()
            .unwrap();
        assert_eq!(prepared.document_hash(), sampled.plan.document_hash());
        let lace = prepared.breaker_lace().unwrap();
        assert_eq!(lace.texture_descriptor.mip_level_count, 10);
        assert_eq!(
            lace.texture_descriptor.format,
            bevy::render::render_resource::TextureFormat::Rgba8Unorm
        );
        let lace_bytes = lace.data.as_ref().unwrap();
        assert_eq!(lace_bytes.len(), 1_398_100);
        assert_eq!(
            forge_core::bytes_sha256(lace_bytes).to_string(),
            "8352073dc60a12b09a7a7c08ca0fcc050861b7879f0db4a6c451663dd7ab419f"
        );
        // This installed zone supplies its own scan bounds; no map name or
        // workspace path is passed into the shared engine calculation.
        let scan = forge_engine::source_breaker_geometry::ShorelineScan {
            origin_xz: [0.0, 0.0],
            rotation_radians: 0.0,
            x_start: -10.0,
            x_steps: 40,
            x_step: 0.5,
            z_start: 12.0,
            z_steps: 244,
            z_step: 0.5,
            water_probe_height: -0.3,
            shoreline_height: 0.0,
            maximum_depth_jump: 3.0,
            maximum_x_gap: 1.01,
            bisections: 16,
            smoothing_passes: 30,
            station_spacing: 0.6,
        };
        let stations = prepared.shoreline_stations(&sampled.plan, scan).unwrap();
        assert!(!stations.is_empty());
        assert!(stations.iter().all(|station| {
            station.iter().all(|value| value.is_finite())
                && (-10.0..=10.0).contains(&station[0])
                && (station[2].hypot(station[3]) - 1.0).abs() < 0.00001
        }));
        let bytes: Vec<_> = stations
            .iter()
            .flatten()
            .flat_map(|value| value.to_le_bytes())
            .collect();
        println!(
            "INSTALLED_MODULE_SHORELINE count={} sha256={}",
            stations.len(),
            forge_core::bytes_sha256(&bytes)
        );
        let contour_config = forge_engine::source_breaker_contours::ContourConfig {
            min: [-10.0, -110.0],
            max: [10.0, 12.0],
            cells: [40, 244],
            water_level: 0.0,
            station_spacing: 0.6,
        };
        let contours = prepared
            .shoreline_contours(&sampled.plan, contour_config)
            .unwrap();
        assert!(!contours.is_empty());
        assert!(
            contours
                .iter()
                .flat_map(|contour| &contour.stations)
                .all(|station| {
                    station.iter().all(|value| value.is_finite())
                        && (station[2].hypot(station[3]) - 1.0).abs() < 0.00001
                })
        );
        println!(
            "INSTALLED_MODULE_SHORE_CONTOURS count={} stations={}",
            contours.len(),
            contours
                .iter()
                .map(|contour| contour.stations.len())
                .sum::<usize>()
        );
        let contour_programs = prepared
            .common_breaker_contour_programs(&sampled.plan, contour_config)
            .unwrap();
        assert_eq!(contour_programs.ranges().len(), contours.len());
        assert_eq!(
            contour_programs.stations().len(),
            contours
                .iter()
                .map(|contour| contour.stations.len() + usize::from(contour.closed))
                .sum::<usize>()
        );
        let contour_mesh =
            forge_engine::source_breaker_render::lip_mesh_ranges(contour_programs.ranges())
                .unwrap();
        assert_eq!(
            contour_mesh.count_vertices(),
            contour_programs
                .ranges()
                .iter()
                .map(|range| (range.count as usize - 1) * 80)
                .sum::<usize>()
        );
        let separate = forge_engine::source_breaker_render::lip_mesh_ranges(&[
            forge_engine::source_breaker_geometry::LipRange { start: 0, count: 3 },
            forge_engine::source_breaker_geometry::LipRange { start: 3, count: 3 },
        ])
        .unwrap();
        let Some(bevy::mesh::VertexAttributeValues::Float32x4(ids)) =
            separate.attribute(forge_engine::source_breaker_render::SHEET)
        else {
            panic!("Separate ribbon station IDs are missing");
        };
        assert_eq!(
            ids.as_chunks::<80>()
                .0
                .iter()
                .map(|chunk| chunk[0][0])
                .collect::<Vec<_>>(),
            [0.0, 1.0, 3.0, 4.0]
        );
        assert!(
            prepared
                .breaker_input(&contour_programs)
                .unwrap()
                .gpu_inputs()
                .validate()
                .is_ok()
        );
        let rotated_scan = forge_engine::source_breaker_geometry::ShorelineScan {
            rotation_radians: std::f64::consts::FRAC_PI_2,
            x_start: -40.0,
            x_steps: 8,
            z_start: -100.0,
            z_steps: 200,
            ..scan
        };
        let rotated = prepared
            .shoreline_stations(&sampled.plan, rotated_scan)
            .unwrap();
        assert!(!rotated.is_empty());
        assert!(rotated.iter().all(|station| {
            station.iter().all(|value| value.is_finite())
                && (100.0..=200.0).contains(&station[0])
                && (-40.0..=-36.0).contains(&station[1])
                && station[2] < 0.0
                && (station[2].hypot(station[3]) - 1.0).abs() < 0.00001
        }));
        let rotated_bytes: Vec<_> = rotated
            .iter()
            .flatten()
            .flat_map(|value| value.to_le_bytes())
            .collect();
        println!(
            "INSTALLED_MODULE_ROTATED_SHORELINE count={} sha256={}",
            rotated.len(),
            forge_core::bytes_sha256(&rotated_bytes)
        );
        let mut other_terrain = terrain.clone();
        other_terrain.revision = other_terrain.revision.next().unwrap();
        let other_plan = SampledTerrainPlan::from_document(
            &other_terrain,
            bytes_from_module(&loaded, &other_terrain),
        )
        .unwrap();
        let nearby = prepared
            .breakers_near_camera(&sampled.plan, [8.0, -85.0])
            .unwrap();
        let stations = nearby.gpu_inputs().stations.len() / 16;
        assert!((1..=4096).contains(&stations));
        assert_eq!(nearby.identities().0, sampled.plan.document_hash());
        assert!(
            prepared
                .breakers_near_camera(&sampled.plan, [f64::NAN, 0.0])
                .is_err()
        );
        assert!(
            prepared
                .breakers_near_camera(&other_plan, [8.0, -85.0])
                .is_err()
        );
        println!("INSTALLED_MODULE_NEARBY_BREAKERS stations={stations}");
        assert!(prepared.shoreline_stations(&other_plan, scan).is_err());
        assert!(
            prepared
                .shoreline_contours(&other_plan, contour_config)
                .is_err()
        );
        assert!(
            prepared
                .common_breaker_contour_programs(&other_plan, contour_config)
                .is_err()
        );
        // Compose the same shared resident scene used by module gameplay from
        // the installed zone's admitted inputs. This is CPU preparation only.
        let resident = prepared
            .prepare_resident_scene(sampled.plan.clone(), [8.0, -85.0])
            .unwrap();
        assert_eq!(resident.document_hash(), sampled.plan.document_hash());
        assert_eq!(resident.station_count(), stations);
        let mut missing_hosts = bevy::prelude::App::new();
        let error = resident.install(&mut missing_hosts, true).unwrap_err();
        assert!(error.contains("exclusive admitted environment and terrain"));
        assert!(
            !missing_hosts
                .world()
                .contains_resource::<forge_engine::source_ocean::Targets>()
        );
        assert!(
            !missing_hosts
                .world()
                .contains_resource::<forge_engine::source_shore_sim::Targets>()
        );
        assert!(
            !missing_hosts
                .world()
                .contains_resource::<forge_engine::source_water_host::Status>()
        );
        assert!(loaded.reject_unrendered_ocean().is_err());
        let other = loaded
            .manifest
            .zones
            .iter()
            .find(|zone| zone.area_id != area)
            .unwrap()
            .area_id;
        let other = forge_delivery::module_runtime::load(&package, other, Some(&cache)).unwrap();
        assert!(other.ocean().unwrap().is_none());
        assert!(prepare(&other, None, false, false).unwrap().is_none());
        assert!(other.reject_unrendered_ocean().is_ok());
    }

    fn bytes_from_module<'a>(
        loaded: &'a LoadedModuleZone,
        terrain: &forge_project::TerrainDocument,
    ) -> &'a [u8] {
        loaded
            .source_blobs
            .get(&terrain.sampled_source.as_ref().unwrap().samples_sha256)
            .unwrap()
    }

    #[test]
    #[ignore = "Requires the pinned installed water module and original spray export"]
    #[allow(
        clippy::too_many_lines,
        clippy::cast_precision_loss,
        reason = "One retained GPU transaction uses indices bounded to 64..704, exactly representable in f32."
    )]
    fn breaker_programs_follow_the_admitted_terrain_revision() {
        let package = PathBuf::from(std::env::var_os("FORGE_WATER_MODULE").unwrap());
        let cache = PathBuf::from(std::env::var_os("FORGE_WATER_MODULE_CACHE").unwrap());
        let source = PathBuf::from(std::env::var_os("FORGE_BREAKER_SOURCE_DIR").unwrap());
        assert!(package.is_absolute() && cache.is_absolute() && source.is_absolute());
        assert_eq!(
            forge_delivery::file_hash(&package).unwrap(),
            std::env::var("FORGE_WATER_MODULE_SHA256").unwrap()
        );
        let area: AreaId = std::env::var("FORGE_WATER_MODULE_AREA")
            .unwrap()
            .parse()
            .unwrap();
        let loaded = forge_delivery::module_runtime::load(&package, area, Some(&cache)).unwrap();
        let metadata: serde_json::Value =
            serde_json::from_slice(&std::fs::read(source.join("spray.json")).unwrap()).unwrap();
        assert_eq!(
            metadata["source_commit"],
            "1438b1abfcaee3267092b75573014f4d9b4a983c"
        );
        let read = |name: &str| {
            let bytes = std::fs::read(source.join(name)).unwrap();
            assert_eq!(
                metadata["files"][name],
                forge_core::bytes_sha256(&bytes).to_string()
            );
            String::from_utf8(bytes).unwrap()
        };
        let crest = read("kernel.wgsl");
        let spray = read("spray-update.wgsl");
        assert!(forge_engine::source_breaker_gpu::COMMON_CREST_PROGRAM.ends_with(&crest));
        assert!(forge_engine::source_breaker_gpu::COMMON_SPRAY_PROGRAM.ends_with(&spray));
        let terrain = loaded.zone.head().effective().unwrap().0;
        let bytes = bytes_from_module(&loaded, &terrain);
        let plan = SampledTerrainPlan::from_document(&terrain, bytes).unwrap();
        let inputs = loaded.ocean().unwrap().unwrap();
        let host = PreparedHost::new(
            &inputs.package,
            inputs.shore_sim.as_ref().unwrap(),
            inputs.surface.unwrap(),
            &terrain,
            &plan,
            bytes,
        )
        .unwrap();
        let scan = forge_engine::source_breaker_geometry::ShorelineScan {
            origin_xz: [0.0, 0.0],
            rotation_radians: std::f64::consts::FRAC_PI_2,
            x_start: -40.0,
            x_steps: 8,
            x_step: 0.5,
            z_start: -100.0,
            z_steps: 200,
            z_step: 0.5,
            water_probe_height: -0.3,
            shoreline_height: 0.0,
            maximum_depth_jump: 3.0,
            maximum_x_gap: 1.01,
            bisections: 16,
            smoothing_passes: 30,
            station_spacing: 0.6,
        };
        let baseline = host.common_breaker_programs(&plan, scan).unwrap();
        assert_eq!(baseline.stations().len(), 8);
        assert!(
            baseline.crest().ends_with(
                &forge_engine::source_breaker_gpu::prepare_station_guard(
                    &forge_engine::source_breaker_gpu::prepare_program(&crest, None).unwrap(),
                    577,
                    8,
                )
                .unwrap()
            )
        );
        assert!(
            baseline.spray().ends_with(
                &forge_engine::source_breaker_gpu::prepare_program(&spray, None).unwrap()
            )
        );
        let baseline_binding = host.breaker_input(&baseline).unwrap();
        let contour_programs = host
            .common_breaker_contour_programs(
                &plan,
                forge_engine::source_breaker_contours::ContourConfig {
                    min: [-10.0, -110.0],
                    max: [10.0, 12.0],
                    cells: [40, 244],
                    water_level: 0.0,
                    station_spacing: 0.6,
                },
            )
            .unwrap();
        let contour_binding = host.breaker_input(&contour_programs).unwrap();
        assert_eq!(contour_programs.ranges().len(), 1);
        assert_eq!(contour_programs.stations().len(), 35);
        let baseline_gpu = baseline_binding.gpu_inputs();
        assert!(baseline_gpu.validate().is_ok());
        assert_eq!(baseline_gpu.stations.len(), 8 * 16);
        assert_eq!(
            baseline_gpu
                .files
                .file("authored-height-deltas.bin")
                .unwrap(),
            0_f32.to_le_bytes()
        );
        assert_eq!(
            forge_core::bytes_sha256(baseline_gpu.files.file("heights.f32.bin").unwrap()),
            forge_core::bytes_sha256(bytes)
        );
        let retained = baseline_binding.clone();
        let retained_gpu = retained.gpu_inputs();
        assert_eq!(
            baseline_gpu.files.file("heights.f32.bin").unwrap().as_ptr(),
            retained_gpu.files.file("heights.f32.bin").unwrap().as_ptr()
        );
        let invalid = forge_engine::source_breaker_gpu::Inputs {
            files: &baseline_binding,
            stations: &[0],
            integrate: true,
        };
        assert!(invalid.validate().is_err());

        let mut edited = terrain.clone();
        edited.revision = edited.revision.next().unwrap();
        edited.height_sampling.base_elevation_m = 1.25;
        edited.height_deltas = vec![forge_project::TerrainHeightDelta::new(100, 200, 2.5)];
        let edited_plan = SampledTerrainPlan::from_document(&edited, bytes).unwrap();
        assert!(
            host.breaker_programs(&edited_plan, &crest, &spray, 577, scan)
                .is_err()
        );
        let inputs = loaded.ocean().unwrap().unwrap();
        let edited_host = PreparedHost::new(
            &inputs.package,
            inputs.shore_sim.as_ref().unwrap(),
            inputs.surface.unwrap(),
            &edited,
            &edited_plan,
            bytes,
        )
        .unwrap();
        let corrected = edited_host
            .common_breaker_programs(&edited_plan, scan)
            .unwrap();
        for program in [corrected.crest(), corrected.spray()] {
            assert!(program.contains("fn forgeOriginalTerrainHeightAt("));
            assert!(program.contains("const forgeBaseElevation:f32=1.25;"));
            assert!(program.contains("forgeTerrainDeltas"));
        }
        assert!(
            corrected
                .crest()
                .contains(&format!("if ( i >= {}u )", corrected.stations().len()))
        );
        assert!(
            edited_host
                .breaker_programs(&edited_plan, &crest, &spray, 576, scan)
                .is_err()
        );
        assert!(host.breaker_input(&corrected).is_err());
        assert!(edited_host.breaker_input(&baseline).is_err());
        let corrected_binding = edited_host.breaker_input(&corrected).unwrap();
        let corrected_gpu = corrected_binding.gpu_inputs();
        assert!(corrected_gpu.validate().is_ok());
        let deltas = corrected_gpu
            .files
            .file("authored-height-deltas.bin")
            .unwrap();
        assert_eq!(deltas.len(), 2049 * 2049 * 4);
        let offset = (200 * 2049 + 100) * 4;
        assert_eq!(&deltas[offset..offset + 4], &2.5_f32.to_le_bytes());
        assert!(
            edited_host
                .breaker_programs(&edited_plan, "invalid", &spray, 577, scan)
                .is_err()
        );
        let replacement = if std::env::var_os("FORGE_BREAKER_GPU").is_some() {
            let before = host.breakers_near_camera(&plan, [8.0, -85.0]).unwrap();
            let after = host.breakers_near_camera(&plan, [208.0, -85.0]).unwrap();
            assert_ne!(before.gpu_inputs().stations, after.gpu_inputs().stations);
            Some([before, after])
        } else {
            None
        };
        drop(host);
        assert!(retained.gpu_inputs().validate().is_ok());
        if std::env::var_os("FORGE_BREAKER_GPU").is_some() {
            let [before, expected] = replacement.unwrap();
            assert!(
                before
                    .retarget_near_camera(&edited_plan, [208.0, -85.0])
                    .is_err()
            );
            assert!(before.retarget_near_camera(&plan, [f64::NAN, 0.0]).is_err());
            assert!(
                retained
                    .retarget_near_camera(&plan, [208.0, -85.0])
                    .is_err()
            );
            let after = before.retarget_near_camera(&plan, [208.0, -85.0]).unwrap();
            assert_eq!(after.gpu_inputs().stations, expected.gpu_inputs().stations);
            assert_eq!(after.ranges(), expected.ranges());
            for name in ["kernel.wgsl", "spray-update.wgsl"] {
                assert_eq!(
                    after.gpu_inputs().files.file(name),
                    before.gpu_inputs().files.file(name)
                );
            }
            drop(edited_host);
            let read_bytes = |name: &str| {
                let bytes = std::fs::read(source.join(name)).unwrap();
                assert_eq!(
                    metadata["files"][name],
                    forge_core::bytes_sha256(&bytes).to_string()
                );
                bytes
            };
            let frame = ["frame", "ocean", "params", "spray-params"]
                .map(|name| read_bytes(&format!("frame-0001-{name}.bin")));
            let source_camera = std::array::from_fn(|index| {
                f32::from_le_bytes(frame[2][index * 4..index * 4 + 4].try_into().unwrap())
            });
            let source_params = forge_engine::source_breaker_params::BreakerParams {
                camera: source_camera,
                ..Default::default()
            };
            assert_eq!(source_params.encode().unwrap(), frame[2].as_slice());
            assert_eq!(
                forge_engine::source_breaker_params::spray_without_body(1),
                frame[3].as_slice()
            );
            let scene = source.parent().unwrap().join("scene");
            let fixture: serde_json::Value =
                serde_json::from_slice(&std::fs::read(scene.join("fixture.json")).unwrap())
                    .unwrap();
            let expected_mips = [("sprayPuff", 7), ("sprayDots", 8)].map(|(name, count)| {
                (0..count)
                    .map(|mip| {
                        let filename = format!("sprite-{name}-m{mip}.rgba8.bin");
                        let bytes = std::fs::read(scene.join(&filename)).unwrap();
                        assert_eq!(
                            fixture["files"][&filename],
                            forge_core::bytes_sha256(&bytes).to_string()
                        );
                        bytes
                    })
                    .collect::<Vec<_>>()
            });
            let mut puff_chain = expected_mips[0].concat();
            let dots_chain = expected_mips[1].concat();
            let capture_hashes = [
                "7e2646287b29d176f7048d981008e6c6139ba6a1781b631f20b15e5d5037d868"
                    .parse()
                    .unwrap(),
                "2da7417941e41c1108268f96af747d9d8e9832d983832ab126da6d0d42d341ed"
                    .parse()
                    .unwrap(),
            ];
            assert!(forge_engine::source_spray_textures::captured_chains_match(
                &puff_chain,
                &dots_chain,
                capture_hashes,
            ));
            puff_chain[0] ^= 1;
            assert!(!forge_engine::source_spray_textures::captured_chains_match(
                &puff_chain,
                &dots_chain,
                capture_hashes,
            ));
            let float_at = |bytes: &[u8], offset| {
                f32::from_le_bytes(bytes[offset..offset + 4].try_into().unwrap())
            };
            let shore_params = retained.shore_gpu_inputs().simulation_params;
            let min = [float_at(shore_params, 0), float_at(shore_params, 4)];
            let size = float_at(shore_params, 8);
            let sea = f64::from(float_at(&frame[0], 588));
            let (point, deposit_index) = (64_u32..704)
                .step_by(32)
                .flat_map(|z| (64_u32..704).step_by(32).map(move |x| (x, z)))
                .find_map(|(x, z)| {
                    let point = [
                        min[0] + (x as f32 + 0.5) * size / 768.0,
                        min[1] + (z as f32 + 0.5) * size / 768.0,
                    ];
                    let low = |plan: &SampledTerrainPlan| {
                        plan.height_at(f64::from(point[0]), f64::from(point[1]))
                            .is_ok_and(|height| height < sea - 2.0)
                    };
                    (low(&plan) && low(&edited_plan)).then_some((point, z * 768 + x))
                })
                .expect("water inside the shared shore domain");
            assert_breaker_gpu_resources(
                [retained, corrected_binding, contour_binding],
                [before, after],
                frame,
                point,
                deposit_index,
                expected_mips,
            );
        }
    }

    #[allow(
        clippy::too_many_lines,
        reason = "This qualification verifies the complete ordered GPU transaction and its readback."
    )]
    fn assert_breaker_gpu_resources(
        inputs: [forge_engine::source_water_host::PreparedBreakerInput; 3],
        replacement: [forge_engine::source_water_host::PreparedBreakerInput; 2],
        frame: [Vec<u8>; 4],
        drop_point: [f32; 2],
        drop_index: u32,
        expected_mips: [Vec<Vec<u8>>; 2],
    ) {
        use bevy::{
            app::{AppExit, ScheduleRunnerPlugin},
            diagnostic::FrameCount,
            prelude::*,
            render::{
                RenderApp,
                extract_resource::{ExtractResource, ExtractResourcePlugin},
                renderer::{RenderDevice, RenderGraph, RenderQueue},
            },
            window::ExitCondition,
            winit::WinitPlugin,
        };
        use std::{
            sync::atomic::{AtomicBool, Ordering},
            time::Duration,
        };
        use wgpu::util::DeviceExt;

        #[derive(Resource, Clone)]
        struct Done(Arc<AtomicBool>);

        #[derive(Resource, Clone, ExtractResource)]
        struct Probes {
            inputs: [forge_engine::source_water_host::PreparedBreakerInput; 3],
            replacement: [forge_engine::source_water_host::PreparedBreakerInput; 2],
            frame: [Vec<u8>; 4],
            drop_point: [f32; 2],
            drop_index: u32,
            expected_mips: [Vec<Vec<u8>>; 2],
        }

        fn assert_sprite_mips(
            device: &wgpu::Device,
            queue: &RenderQueue,
            expected: &[Vec<Vec<u8>>; 2],
            textures: [&wgpu::Texture; 2],
        ) {
            for (name, side, texture, reference) in [
                ("puff", 64_u32, textures[0], &expected[0]),
                ("dots", 128_u32, textures[1], &expected[1]),
            ] {
                let mut locations = Vec::new();
                let mut total = 0_u64;
                for level in 0..texture.mip_level_count() {
                    let width = side >> level;
                    let row = (width * 4).div_ceil(256) * 256;
                    locations.push((total, row, width));
                    total += u64::from(row) * u64::from(width);
                }
                let staging = device.create_buffer(&wgpu::BufferDescriptor {
                    label: Some("source sprite mip readback"),
                    size: total,
                    usage: wgpu::BufferUsages::COPY_DST | wgpu::BufferUsages::MAP_READ,
                    mapped_at_creation: false,
                });
                let mut encoder = device.create_command_encoder(&wgpu::CommandEncoderDescriptor {
                    label: Some("source sprite mip qualification"),
                });
                for (level, &(offset, row, width)) in locations.iter().enumerate() {
                    encoder.copy_texture_to_buffer(
                        wgpu::TexelCopyTextureInfo {
                            texture,
                            mip_level: u32::try_from(level).unwrap(),
                            origin: wgpu::Origin3d::ZERO,
                            aspect: wgpu::TextureAspect::All,
                        },
                        wgpu::TexelCopyBufferInfo {
                            buffer: &staging,
                            layout: wgpu::TexelCopyBufferLayout {
                                offset,
                                bytes_per_row: Some(row),
                                rows_per_image: Some(width),
                            },
                        },
                        wgpu::Extent3d {
                            width,
                            height: width,
                            depth_or_array_layers: 1,
                        },
                    );
                }
                queue.submit([encoder.finish()]);
                let (sender, receiver) = std::sync::mpsc::channel();
                staging
                    .slice(..)
                    .map_async(wgpu::MapMode::Read, move |result| {
                        let _ = sender.send(result);
                    });
                device
                    .poll(wgpu::PollType::Wait {
                        submission_index: None,
                        timeout: Some(Duration::from_secs(15)),
                    })
                    .unwrap();
                receiver
                    .recv_timeout(Duration::from_secs(15))
                    .unwrap()
                    .unwrap();
                let bytes = staging.slice(..).get_mapped_range();
                assert_eq!(locations.len(), reference.len());
                for (level, (&(offset, row, width), expected)) in
                    locations.iter().zip(reference).enumerate()
                {
                    assert_eq!(expected.len(), (width * width * 4) as usize);
                    let mut different = 0;
                    for y in 0..width {
                        let start = usize::try_from(offset + u64::from(y * row))
                            .expect("bounded GPU readback offset");
                        let source = &bytes[start..start + (width * 4) as usize];
                        let target =
                            &expected[(y * width * 4) as usize..((y + 1) * width * 4) as usize];
                        different += source.iter().zip(target).filter(|(a, b)| a != b).count();
                    }
                    println!("SPRAY_MIP_GPU name={name} level={level} different={different}");
                    assert_eq!(different, 0, "sprite mip differs: {name} level {level}");
                }
                drop(bytes);
                staging.unmap();
            }
        }

        #[allow(
            clippy::too_many_lines,
            reason = "Exercise the real GPU cutoff and slot shader together"
        )]
        fn assert_shadow_handoff(device: &RenderDevice, queue: &RenderQueue) {
            let raw = device.wgpu_device();
            let buffer = |label, size| {
                raw.create_buffer(&wgpu::BufferDescriptor {
                    label: Some(label),
                    size,
                    usage: wgpu::BufferUsages::STORAGE
                        | wgpu::BufferUsages::COPY_SRC
                        | wgpu::BufferUsages::COPY_DST,
                    mapped_at_creation: false,
                })
            };
            let head = buffer("spray shadow test head", 4);
            let cutoff = buffer("spray shadow test cutoff", 8);
            let result = buffer("spray shadow test result", 20);
            let shader = format!(
                r"
                @group(0) @binding(0) var<storage,read> residentSprayHead:array<u32>;
                @group(0) @binding(1) var<storage,read> residentShadowCutoff:array<u32>;
                @group(0) @binding(2) var<storage,read_write> result:array<u32>;
                {}
                @compute @workgroup_size(1)
                fn main(@builtin(global_invocation_id) id:vec3u) {{
                    let slots = array<u32,5>(32766u,32767u,0u,1u,40000u);
                    result[id.x] = select(0u, 1u, residentShadowIsCurrent(slots[id.x]));
                }}",
                forge_engine::source_spray_generation::CLASSIFIER_WGSL,
            );
            let shader = raw.create_shader_module(wgpu::ShaderModuleDescriptor {
                label: Some("spray shadow slot classifier"),
                source: wgpu::ShaderSource::Wgsl(shader.into()),
            });
            let pipeline = raw.create_compute_pipeline(&wgpu::ComputePipelineDescriptor {
                label: Some("spray shadow slot classifier"),
                layout: None,
                module: &shader,
                entry_point: Some("main"),
                compilation_options: wgpu::PipelineCompilationOptions::default(),
                cache: None,
            });
            let group = raw.create_bind_group(&wgpu::BindGroupDescriptor {
                label: Some("spray shadow slot classifier"),
                layout: &pipeline.get_bind_group_layout(0),
                entries: &[
                    wgpu::BindGroupEntry {
                        binding: 0,
                        resource: head.as_entire_binding(),
                    },
                    wgpu::BindGroupEntry {
                        binding: 1,
                        resource: cutoff.as_entire_binding(),
                    },
                    wgpu::BindGroupEntry {
                        binding: 2,
                        resource: result.as_entire_binding(),
                    },
                ],
            });
            let handoff = forge_engine::source_spray_generation::ShadowHandoff::new(raw);
            let retirement = handoff.bind(raw, &head, &cutoff).unwrap();
            queue.write_buffer(&head, 0, &(u32::MAX - 1).to_le_bytes());
            let mut encoder =
                raw.create_command_encoder(&wgpu::CommandEncoderDescriptor::default());
            assert!(handoff.begin(&mut encoder, &head, &head).is_err());
            handoff.begin(&mut encoder, &head, &cutoff).unwrap();
            queue.submit([encoder.finish()]);
            let run = |head_value: u32, expected: [u32; 5], cutoff_head: u32, active: u32| {
                queue.write_buffer(&head, 0, &head_value.to_le_bytes());
                let mut encoder =
                    raw.create_command_encoder(&wgpu::CommandEncoderDescriptor::default());
                handoff.retire(&mut encoder, &retirement);
                {
                    let mut pass =
                        encoder.begin_compute_pass(&wgpu::ComputePassDescriptor::default());
                    pass.set_pipeline(&pipeline);
                    pass.set_bind_group(0, &group, &[]);
                    pass.dispatch_workgroups(5, 1, 1);
                }
                let staging = raw.create_buffer(&wgpu::BufferDescriptor {
                    label: Some("spray shadow handoff readback"),
                    size: 28,
                    usage: wgpu::BufferUsages::COPY_DST | wgpu::BufferUsages::MAP_READ,
                    mapped_at_creation: false,
                });
                encoder.copy_buffer_to_buffer(&result, 0, &staging, 0, 20);
                encoder.copy_buffer_to_buffer(&cutoff, 0, &staging, 20, 8);
                queue.submit([encoder.finish()]);
                let (sender, receiver) = std::sync::mpsc::channel();
                staging
                    .slice(..)
                    .map_async(wgpu::MapMode::Read, move |value| {
                        let _ = sender.send(value);
                    });
                raw.poll(wgpu::PollType::Wait {
                    submission_index: None,
                    timeout: Some(Duration::from_secs(15)),
                })
                .unwrap();
                receiver
                    .recv_timeout(Duration::from_secs(15))
                    .unwrap()
                    .unwrap();
                let bytes = staging.slice(..).get_mapped_range();
                let values: Vec<_> = bytes
                    .as_chunks::<4>()
                    .0
                    .iter()
                    .map(|v| u32::from_le_bytes(*v))
                    .collect();
                assert_eq!(&values[..5], expected);
                assert_eq!(values[5], cutoff_head);
                assert_eq!(values[6], active);
                drop(bytes);
                staging.unmap();
            };
            run(1, [1, 1, 1, 0, 0], u32::MAX - 1, 1);
            run(32_766, [1, 1, 1, 1, 0], u32::MAX - 1, 2);
            run(1, [1, 1, 1, 1, 0], u32::MAX - 1, 2);
            let mut encoder =
                raw.create_command_encoder(&wgpu::CommandEncoderDescriptor::default());
            handoff.begin(&mut encoder, &head, &cutoff).unwrap();
            queue.submit([encoder.finish()]);
            run(3, [0, 0, 0, 1, 0], 1, 1);
            println!("SPRAY_SHADOW_GENERATION_GPU_OK wrap=1 ring_retired=1 rearmed=1");
        }

        #[allow(
            clippy::too_many_lines,
            reason = "Exercise one ordered GPU handoff end to end"
        )]
        fn assert_station_replacement(
            inputs: &Probes,
            device: &RenderDevice,
            queue: &RenderQueue,
            displacement: &wgpu::TextureView,
        ) {
            let raw = device.wgpu_device();
            let [before, after] = &inputs.replacement;
            let count = |input: &forge_engine::source_water_host::PreparedBreakerInput| {
                u32::try_from(input.gpu_inputs().stations.len() / 16).unwrap()
            };
            let [old_count, new_count] = [count(before), count(after)];
            assert!(old_count > 0 && new_count > 0);
            let storage = |label, size| {
                raw.create_buffer(&wgpu::BufferDescriptor {
                    label: Some(label),
                    size,
                    usage: wgpu::BufferUsages::STORAGE
                        | wgpu::BufferUsages::COPY_SRC
                        | wgpu::BufferUsages::COPY_DST,
                    mapped_at_creation: false,
                })
            };
            let uniform = |bytes: &[u8]| {
                raw.create_buffer_init(&wgpu::util::BufferInitDescriptor {
                    label: Some("station replacement uniform"),
                    contents: bytes,
                    usage: wgpu::BufferUsages::UNIFORM,
                })
            };
            let particles = [
                storage("replacement position", 40960 * 16),
                storage("replacement velocity", 40960 * 16),
                storage("replacement info", 40960 * 16),
                storage("replacement head", 4),
            ];
            let offset = 30000 * 16;
            let pack = |values: [f32; 4]| {
                values
                    .into_iter()
                    .flat_map(f32::to_le_bytes)
                    .collect::<Vec<_>>()
            };
            queue.write_buffer(&particles[0], offset, &pack([0.0, 100.0, 0.0, 0.0]));
            queue.write_buffer(&particles[1], offset, &pack([0.0, 0.0, 0.0, 0.05]));
            queue.write_buffer(&particles[2], offset, &pack([0.0, 5.0, 100.0, 0.3]));
            let resident = forge_engine::source_shore_sim::initialize_gpu(
                &before.shore_gpu_inputs(),
                raw,
                queue,
                None,
            )
            .unwrap();
            let mut coupled = forge_engine::source_water_host::CoupledBreakerGpu::adopt(
                device, queue, before, &resident, 0,
            )
            .unwrap();
            let ocean = uniform(&inputs.frame[1]);
            let parameters = uniform(&inputs.frame[2]);
            let spray_parameters = uniform(&inputs.frame[3]);
            let output = |stations: u32| {
                let data = 1_f32.to_le_bytes().repeat(stations as usize * 6 * 4);
                raw.create_buffer_init(&wgpu::util::BufferInitDescriptor {
                    label: Some("replacement crest output"),
                    contents: &data,
                    usage: wgpu::BufferUsages::STORAGE | wgpu::BufferUsages::COPY_SRC,
                })
            };
            let old_output = output(old_count);
            let new_output = output(new_count);
            let mut encoder =
                raw.create_command_encoder(&wgpu::CommandEncoderDescriptor::default());
            let mut frame = forge_engine::source_water_host::CoupledBreakerFrame {
                frame: &inputs.frame[0],
                ocean: &ocean,
                parameters: &parameters,
                spray_parameters: &spray_parameters,
                displacement,
                particles: particles.each_ref(),
                output: &old_output,
                station_count: old_count,
                emit: true,
            };
            let _ = coupled.encode(raw, &mut encoder, &frame).unwrap();
            queue.submit([encoder.finish()]);
            let cutoff = storage("replacement shadow cutoff", 8);
            let mut encoder =
                raw.create_command_encoder(&wgpu::CommandEncoderDescriptor::default());
            assert!(
                coupled
                    .replace_stations(
                        device,
                        &mut encoder,
                        &particles[3],
                        &cutoff,
                        &inputs.inputs[1]
                    )
                    .is_err()
            );
            assert!(
                coupled
                    .replace_stations(
                        device,
                        &mut encoder,
                        &particles[3],
                        &cutoff,
                        &inputs.inputs[0]
                    )
                    .is_err()
            );
            assert!(
                coupled
                    .replace_stations(device, &mut encoder, &particles[3], &particles[3], after)
                    .is_err()
            );
            coupled
                .replace_stations(device, &mut encoder, &particles[3], &cutoff, after)
                .unwrap();
            let mut next_frame = inputs.frame[0].clone();
            next_frame[576..580].copy_from_slice(&2_u32.to_le_bytes());
            frame.frame = &next_frame;
            frame.station_count = new_count;
            frame.output = &new_output;
            let _ = coupled.encode(raw, &mut encoder, &frame).unwrap();
            let staging = raw.create_buffer(&wgpu::BufferDescriptor {
                label: Some("replacement readback"),
                size: 124,
                usage: wgpu::BufferUsages::COPY_DST | wgpu::BufferUsages::MAP_READ,
                mapped_at_creation: false,
            });
            encoder.copy_buffer_to_buffer(&new_output, 0, &staging, 0, 96);
            encoder.copy_buffer_to_buffer(&particles[0], offset, &staging, 96, 16);
            encoder.copy_buffer_to_buffer(&cutoff, 0, &staging, 112, 8);
            encoder.copy_buffer_to_buffer(&particles[3], 0, &staging, 120, 4);
            queue.submit([encoder.finish()]);
            let (sender, receiver) = std::sync::mpsc::channel();
            staging
                .slice(..)
                .map_async(wgpu::MapMode::Read, move |result| {
                    let _ = sender.send(result);
                });
            raw.poll(wgpu::PollType::Wait {
                submission_index: None,
                timeout: Some(Duration::from_secs(15)),
            })
            .unwrap();
            receiver
                .recv_timeout(Duration::from_secs(15))
                .unwrap()
                .unwrap();
            let bytes = staging.slice(..).get_mapped_range();
            assert_ne!(&bytes[..96], 1_f32.to_le_bytes().repeat(24));
            let age = f32::from_le_bytes(bytes[108..112].try_into().unwrap());
            assert!((age - 2.0 / 60.0).abs() < 1e-5);
            let cutoff_head = u32::from_le_bytes(bytes[112..116].try_into().unwrap());
            let active = u32::from_le_bytes(bytes[116..120].try_into().unwrap());
            let head = u32::from_le_bytes(bytes[120..124].try_into().unwrap());
            assert!(head.wrapping_sub(cutoff_head) < 32_768);
            assert_eq!(active, 1);
            drop(bytes);
            staging.unmap();
            println!(
                "INSTALLED_MODULE_STATION_REPLACEMENT_GPU_OK old={old_count} new={new_count} age={age}"
            );
        }

        #[allow(
            clippy::too_many_arguments,
            clippy::too_many_lines,
            reason = "One Bevy render system stages and reads back the qualification resources."
        )]
        fn build_resources(
            inputs: Res<Probes>,
            device: Res<RenderDevice>,
            queue: Res<RenderQueue>,
            done: Res<Done>,
            sprites: Option<Res<forge_engine::source_spray_mips::SpriteTextures>>,
            images: Res<bevy::render::render_asset::RenderAssets<bevy::render::texture::GpuImage>>,
            mut built: Local<bool>,
            mut observed_ready: Local<bool>,
        ) {
            if *built {
                return;
            }
            let Some(sprites) = sprites.filter(|s| s.ready.load(Ordering::Acquire)) else {
                return;
            };
            // The previous frame's render context must be submitted before readback.
            if !*observed_ready {
                *observed_ready = true;
                return;
            }
            let (Some(puff), Some(dots)) = (images.get(&sprites.puff), images.get(&sprites.dots))
            else {
                return;
            };
            let raw_device = device.wgpu_device();
            assert_shadow_handoff(&device, &queue);
            assert_sprite_mips(
                raw_device,
                &queue,
                &inputs.expected_mips,
                [&puff.texture, &dots.texture],
            );
            let uniform = |bytes: &[u8]| {
                raw_device.create_buffer_init(&wgpu::util::BufferInitDescriptor {
                    label: Some("module breaker qualification uniform"),
                    contents: bytes,
                    usage: wgpu::BufferUsages::UNIFORM,
                })
            };
            let ocean = uniform(&inputs.frame[1]);
            let params = uniform(&inputs.frame[2]);
            let spray_params = uniform(&inputs.frame[3]);
            let mip_data: Vec<_> = (0..9)
                .map(|m| {
                    let side = 256_usize >> m;
                    vec![0_u8; side * side * 4 * 8]
                })
                .collect();
            let mips: Vec<_> = mip_data.iter().map(Vec::as_slice).collect();
            let displacement = forge_engine::source_breaker_gpu::texture(
                raw_device,
                &queue,
                256,
                4,
                wgpu::TextureFormat::Rgba16Float,
                &mips,
                8,
            );
            for (index, input) in inputs.inputs.iter().enumerate() {
                let view = input.gpu_inputs();
                view.validate().unwrap();
                let storage = |label, size| {
                    raw_device.create_buffer(&wgpu::BufferDescriptor {
                        label: Some(label),
                        size,
                        usage: wgpu::BufferUsages::STORAGE
                            | wgpu::BufferUsages::COPY_SRC
                            | wgpu::BufferUsages::COPY_DST,
                        mapped_at_creation: false,
                    })
                };
                let particles = [
                    storage("module breaker position", 40960 * 16),
                    storage("module breaker velocity", 40960 * 16),
                    storage("module breaker info", 40960 * 16),
                    storage("module breaker head", 4),
                ];
                let particle_offset = 30000 * 16;
                let pack = |values: [f32; 4]| {
                    values
                        .into_iter()
                        .flat_map(f32::to_le_bytes)
                        .collect::<Vec<_>>()
                };
                let initial_particle = pack([0.0, 100.0, 0.0, 0.0]);
                queue.write_buffer(&particles[0], particle_offset, &initial_particle);
                queue.write_buffer(&particles[1], particle_offset, &pack([0.0, 0.0, 0.0, 0.05]));
                queue.write_buffer(
                    &particles[2],
                    particle_offset,
                    &pack([0.0, 5.0, 100.0, 0.3]),
                );
                let drop_offset = particle_offset + 16;
                queue.write_buffer(
                    &particles[0],
                    drop_offset,
                    &pack([inputs.drop_point[0], -100.0, inputs.drop_point[1], 0.1]),
                );
                queue.write_buffer(&particles[1], drop_offset, &pack([0.0, -1.0, 0.0, 0.05]));
                queue.write_buffer(&particles[2], drop_offset, &pack([0.0, 5.0, -100.0, 0.3]));
                let shore = storage("module breaker deposit", 768 * 768 * 4);
                let deposit_offset = 12_345 * 4;
                queue.write_buffer(&shore, deposit_offset, &7_u32.to_le_bytes());
                assert!(
                    forge_engine::source_water_host::CoupledBreakerGpu::new(
                        &device,
                        &queue,
                        input,
                        storage("wrong deposit size", 4),
                    )
                    .is_err()
                );
                let resident = forge_engine::source_shore_sim::initialize_gpu(
                    &input.shore_gpu_inputs(),
                    raw_device,
                    &queue,
                    Some(shore.clone()),
                )
                .unwrap();
                assert!(
                    forge_engine::source_water_host::CoupledBreakerGpu::adopt(
                        &device,
                        &queue,
                        &inputs.inputs[usize::from(index != 1)],
                        &resident,
                        0,
                    )
                    .is_err()
                );
                let mut coupled = forge_engine::source_water_host::CoupledBreakerGpu::adopt(
                    &device, &queue, input, &resident, 0,
                )
                .unwrap();
                let station_count = u32::try_from(view.stations.len() / 16).unwrap();
                let initial = 1_f32
                    .to_le_bytes()
                    .repeat(station_count.max(1) as usize * 6 * 4);
                let output = raw_device.create_buffer_init(&wgpu::util::BufferInitDescriptor {
                    label: Some("module breaker crest output"),
                    contents: &initial,
                    usage: wgpu::BufferUsages::STORAGE | wgpu::BufferUsages::COPY_SRC,
                });
                let staging = raw_device.create_buffer(&wgpu::BufferDescriptor {
                    label: Some("module breaker crest readback"),
                    size: initial.len() as u64 + 28,
                    usage: wgpu::BufferUsages::COPY_DST | wgpu::BufferUsages::MAP_READ,
                    mapped_at_creation: false,
                });
                let mut encoder =
                    raw_device.create_command_encoder(&wgpu::CommandEncoderDescriptor {
                        label: Some("module breaker qualification dispatch"),
                    });
                let particle_refs = particles.each_ref();
                let mut crest = forge_engine::source_water_host::CoupledBreakerFrame {
                    frame: &inputs.frame[0],
                    ocean: &ocean,
                    parameters: &params,
                    spray_parameters: &spray_params,
                    displacement: &displacement,
                    particles: particle_refs,
                    output: &output,
                    station_count: station_count + 1,
                    emit: true,
                };
                assert!(coupled.encode(raw_device, &mut encoder, &crest).is_err());
                crest.station_count = station_count;
                crest.frame = &[];
                assert!(coupled.encode(raw_device, &mut encoder, &crest).is_err());
                crest.frame = &inputs.frame[0];
                let short_uniform = uniform(&[0; 4]);
                crest.parameters = &short_uniform;
                assert!(coupled.encode(raw_device, &mut encoder, &crest).is_err());
                crest.parameters = &output;
                assert!(coupled.encode(raw_device, &mut encoder, &crest).is_err());
                crest.parameters = &params;
                let short_storage = storage("invalid small breaker storage", 4);
                crest.particles[0] = &short_storage;
                assert!(coupled.encode(raw_device, &mut encoder, &crest).is_err());
                crest.particles[0] = &particles[0];
                crest.output = &short_storage;
                assert!(coupled.encode(raw_device, &mut encoder, &crest).is_err());
                crest.output = &output;
                let mut skipped_frame = inputs.frame[0].clone();
                skipped_frame[576..580].copy_from_slice(&3_u32.to_le_bytes());
                crest.frame = &skipped_frame;
                assert!(coupled.encode(raw_device, &mut encoder, &crest).is_err());
                crest.frame = &inputs.frame[0];
                let _spray_group = coupled.encode(raw_device, &mut encoder, &crest).unwrap();
                assert!(coupled.encode(raw_device, &mut encoder, &crest).is_err());
                encoder.copy_buffer_to_buffer(&output, 0, &staging, 0, initial.len() as u64);
                encoder.copy_buffer_to_buffer(
                    &particles[0],
                    particle_offset,
                    &staging,
                    initial.len() as u64,
                    16,
                );
                encoder.copy_buffer_to_buffer(
                    &shore,
                    deposit_offset,
                    &staging,
                    initial.len() as u64 + 16,
                    4,
                );
                encoder.copy_buffer_to_buffer(
                    &shore,
                    u64::from(inputs.drop_index) * 4,
                    &staging,
                    initial.len() as u64 + 20,
                    4,
                );
                resident.encode_frame(raw_device, &mut encoder, &inputs.frame[0]);
                encoder.copy_buffer_to_buffer(
                    &shore,
                    u64::from(inputs.drop_index) * 4,
                    &staging,
                    initial.len() as u64 + 24,
                    4,
                );
                queue.submit([encoder.finish()]);
                let (sender, receiver) = std::sync::mpsc::channel();
                staging
                    .slice(..)
                    .map_async(wgpu::MapMode::Read, move |result| {
                        let _ = sender.send(result);
                    });
                raw_device
                    .poll(wgpu::PollType::Wait {
                        submission_index: None,
                        timeout: Some(Duration::from_secs(15)),
                    })
                    .unwrap();
                receiver
                    .recv_timeout(Duration::from_secs(15))
                    .unwrap()
                    .unwrap();
                let output_bytes = staging.slice(..).get_mapped_range();
                assert!(
                    output_bytes[..initial.len() + 16]
                        .as_chunks::<4>()
                        .0
                        .iter()
                        .all(|bytes| f32::from_le_bytes(*bytes).is_finite())
                );
                if station_count == 0 {
                    assert_eq!(&output_bytes[..initial.len()], initial);
                } else {
                    assert_ne!(&output_bytes[..initial.len()], initial);
                }
                let particle = &output_bytes[initial.len()..initial.len() + 16];
                assert_ne!(particle, initial_particle);
                let age = f32::from_le_bytes(particle[12..16].try_into().unwrap());
                assert!((age - 1.0 / 60.0).abs() < 1e-5);
                assert_eq!(
                    &output_bytes[initial.len() + 16..initial.len() + 20],
                    &[0; 4]
                );
                assert_eq!(
                    &output_bytes[initial.len() + 20..initial.len() + 24],
                    &1_u32.to_le_bytes()
                );
                assert_eq!(&output_bytes[initial.len() + 24..], &[0; 4]);
                drop(output_bytes);
                staging.unmap();
                println!("INSTALLED_MODULE_COUPLED_GPU_DISPATCH_OK stations={station_count}");
            }
            assert_station_replacement(&inputs, &device, &queue, &displacement);
            *built = true;
            done.0.store(true, Ordering::Release);
        }

        fn finish(done: Res<Done>, frame: Res<FrameCount>, mut exit: MessageWriter<AppExit>) {
            if done.0.load(Ordering::Acquire) {
                exit.write(AppExit::Success);
            } else if frame.0 > 600 {
                exit.write(AppExit::error());
            }
        }

        let done = Done(Arc::new(AtomicBool::new(false)));
        let mut app = App::new();
        app.add_plugins(
            DefaultPlugins
                .set(WindowPlugin {
                    primary_window: None,
                    exit_condition: ExitCondition::DontExit,
                    ..default()
                })
                .disable::<WinitPlugin>(),
        )
        .add_plugins(ScheduleRunnerPlugin::run_loop(Duration::from_millis(16)))
        .add_plugins(ExtractResourcePlugin::<Probes>::default())
        .insert_resource(Probes {
            inputs,
            replacement,
            frame,
            drop_point,
            drop_index,
            expected_mips,
        })
        .insert_resource(done.clone())
        .add_systems(Update, finish);
        forge_engine::source_spray_mips::register(&mut app).unwrap();
        let sprites = forge_engine::source_spray_mips::SpriteTextures::stage(
            &mut app.world_mut().resource_mut::<Assets<Image>>(),
        )
        .unwrap();
        app.insert_resource(sprites);
        app.sub_app_mut(RenderApp)
            .insert_resource(done.clone())
            .add_systems(
                RenderGraph,
                build_resources.after(forge_engine::source_spray_mips::SpriteMipSet),
            );
        assert_eq!(app.run(), AppExit::Success);
        assert!(done.0.load(Ordering::Acquire));
    }
}
