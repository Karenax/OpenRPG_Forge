//! Native Gate 7 diagnostic: original 2 km raster, three camera positions, bounded residency.
//! This does not qualify authored gameplay, continuous movement or general HLOD/distribution.
use bevy::{
    app::AppExit,
    prelude::*,
    render::{
        renderer::RenderAdapterInfo,
        view::screenshot::{Screenshot, ScreenshotCaptured, save_to_disk},
    },
    window::{PresentMode, WindowResolution},
};
use forge_core::{EntityId, FrameDistribution};
use forge_engine::cell_streaming::{
    CellStreamingObserver, CellStreamingStats, SampledCellStreamingPlugin, StreamedTerrainCell,
};
use forge_project::{
    HeightSamplingAlgorithm, TERRAIN_DOCUMENT_SCHEMA_VERSION_V4, TerrainExtent,
    sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
};
use forge_world::{
    cell_streaming::{CellDetail, CellStreamingPolicy},
    sampled_terrain::SampledTerrainPlan,
};
use std::{fs, io::Write, path::PathBuf, sync::Arc};

const ROUTE: [[f64; 2]; 3] = [[-512.0; 2], [512.0; 2], [-512.0; 2]];

#[path = "cell_streaming_region/quality.rs"]
mod quality;
#[path = "cell_streaming_region/walk.rs"]
mod walk;

#[derive(Resource)]
struct Proof {
    directory: PathBuf,
    timeout_seconds: u32,
    stage: usize,
    warmup: u32,
    samples: Vec<f64>,
    sample_start_observation: Option<u64>,
    reports: Vec<serde_json::Value>,
    initial_ids: Option<Vec<(u16, u16, String)>>,
    capture_requested: bool,
    capture_complete: bool,
    finished: bool,
}

fn source() -> Result<Arc<SampledTerrainPlan>, String> {
    // 4 MiB source raster kept once. No imported content, package, Vault or provider.
    let bytes: Vec<_> = (0_u32..1024 * 1024)
        .flat_map(|i| {
            let x = f64::from(i % 1024) * 2.0;
            let z = f64::from(i / 1024) * 2.0;
            #[allow(clippy::cast_possible_truncation)]
            ((x * 0.012).sin().mul_add(12.0, (z * 0.009).cos() * 8.0) as f32).to_le_bytes()
        })
        .collect();
    let mut document =
        forge_project::authoring::new_terrain("gate7", "streaming-region", "Région de mesure")?;
    document.schema_version = TERRAIN_DOCUMENT_SCHEMA_VERSION_V4;
    document.extent_m = TerrainExtent::new(2048.0, 2048.0);
    document.height_sampling.algorithm = HeightSamplingAlgorithm::SampledRasterV1;
    document.height_sampling.sample_spacing_m = 2.0;
    document.height_sampling.base_elevation_m = 0.0;
    document.height_sampling.amplitude_m = 0.0;
    document.height_sampling.base_frequency_cycles_per_m = 1.0;
    document.height_sampling.octaves = 1;
    document.height_sampling.lacunarity = 2.0;
    document.height_sampling.persistence = 0.5;
    document.sampled_source = Some(SampledTerrainSource {
        schema: SAMPLED_TERRAIN_SCHEMA.into(),
        schema_version: 1,
        width: 1024,
        height: 1024,
        origin_xz_m: [-1024.0; 2],
        sample_spacing_m: 2.0,
        outside_height_m: 0.0,
        encoding: HeightEncoding::Float32Le,
        samples_sha256: forge_core::bytes_sha256(&bytes),
    });
    SampledTerrainPlan::from_document(&document, &bytes)
        .map(Arc::new)
        .map_err(|e| e.to_string())
}

fn mode(args: &[std::ffi::OsString]) -> Option<AppExit> {
    match args.split_first() {
        Some((flag, rest)) if flag == "--mesh-quality" => Some(quality::run(rest)),
        Some((flag, rest)) if flag == "--walk" => Some(walk::run(rest)),
        _ => None,
    }
}

fn main() -> AppExit {
    let args: Vec<_> = std::env::args_os().skip(1).collect();
    if let Some(exit) = mode(&args) {
        return exit;
    }
    let (directory, timeout_seconds) = match args.as_slice() {
        [directory] => (PathBuf::from(directory), 180),
        [directory, option, seconds] if option == "--timeout-seconds" => {
            let Some(seconds) = seconds
                .to_str()
                .and_then(|value| value.parse::<u32>().ok())
                .filter(|value| (180..=900).contains(value))
            else {
                eprintln!("Diagnostic timeout must be an integer from 180 to 900 seconds");
                return AppExit::error();
            };
            (PathBuf::from(directory), seconds)
        }
        _ => {
            eprintln!(
                "usage: cell_streaming_region ABSOLUTE_FRESH_OUTPUT_DIRECTORY [--timeout-seconds 180..900]"
            );
            return AppExit::error();
        }
    };
    if !directory.is_absolute() || directory.exists() {
        eprintln!("Diagnostic requires a fresh absolute output directory");
        return AppExit::error();
    }
    let source = match source() {
        Ok(source) => source,
        Err(error) => {
            eprintln!("{error}");
            return AppExit::error();
        }
    };
    let policy = CellStreamingPolicy {
        origin_xz_m: [-1024.0; 2],
        cell_size_m: 64.0,
        dimensions: [32; 2],
        detail_radius: 1,
        far_radius: 4,
        max_resident_cells: 81,
        full_cell_bytes: 100_000,
        hlod_cell_bytes: 100_000,
        max_resident_bytes: 10_000_000,
        max_staging_bytes: 20_000_000,
    };
    let plugin = match SampledCellStreamingPlugin::new(
        source,
        policy,
        EntityId::deterministic("gate7-native-region-001"),
        StandardMaterial {
            base_color: Color::srgb(0.27, 0.39, 0.18),
            perceptual_roughness: 0.95,
            ..default()
        },
        ROUTE[0],
    ) {
        Ok(plugin) => plugin,
        Err(error) => {
            eprintln!("{error}");
            return AppExit::error();
        }
    };
    if let Err(error) = fs::create_dir(&directory) {
        eprintln!("{error}");
        return AppExit::error();
    }
    let mut app = App::new();
    app.add_plugins(DefaultPlugins.set(WindowPlugin {
        primary_window: Some(Window {
            title: "OpenRPG Forge — Gate 7".into(),
            resolution: WindowResolution::new(1920, 1080),
            present_mode: PresentMode::AutoNoVsync,
            ..default()
        }),
        ..default()
    }))
    .add_plugins(forge_engine::CaptureReadinessPlugin)
    .insert_resource(ClearColor(Color::srgb(0.32, 0.43, 0.57)))
    .insert_resource(Proof {
        directory,
        timeout_seconds,
        stage: 0,
        warmup: 0,
        samples: Vec::new(),
        sample_start_observation: None,
        reports: Vec::new(),
        initial_ids: None,
        capture_requested: false,
        capture_complete: false,
        finished: false,
    });
    forge_engine::sampled_terrain_physics::register(&mut app);
    app.add_plugins(plugin)
        .add_systems(Startup, setup)
        .add_systems(Update, drive);
    app.run()
}

fn setup(mut commands: Commands) {
    commands.spawn((
        Camera3d::default(),
        AmbientLight {
            color: Color::WHITE,
            brightness: 180.0,
            ..default()
        },
        Transform::from_xyz(-682.0, 160.0, -312.0)
            .looking_at(Vec3::new(-512.0, 0.0, -512.0), Vec3::Y),
    ));
    commands.spawn((
        DirectionalLight {
            illuminance: 16_000.0,
            shadow_maps_enabled: true,
            ..default()
        },
        Transform::from_xyz(100.0, 200.0, 100.0).looking_at(Vec3::ZERO, Vec3::Y),
    ));
}

fn drive(world: &mut World) {
    let Some(mut proof) = world.remove_resource::<Proof>() else {
        return;
    };
    if !proof.finished {
        match step(world, &mut proof) {
            Ok(done) => {
                if done {
                    proof.finished = true;
                    world.write_message(AppExit::Success);
                }
            }
            Err(error) => {
                error!("Gate 7 diagnostic failed: {error}");
                proof.finished = true;
                world.write_message(AppExit::error());
            }
        }
    }
    world.insert_resource(proof);
}

fn step(world: &mut World, proof: &mut Proof) -> Result<bool, String> {
    if world.resource::<Time<Real>>().elapsed_secs_f64() > f64::from(proof.timeout_seconds) {
        return Err(format!(
            "Native region watchdog expired; render readiness: {:?}",
            forge_engine::observe_render_readiness(world, true)
        ));
    }
    let stats = world.resource::<CellStreamingStats>().clone();
    if let Some(error) = stats.last_error {
        return Err(error);
    }
    if stats.pending || stats.committed_transitions < proof.stage as u64 + 1 {
        forge_engine::observe_render_readiness(world, false);
        return Ok(false);
    }
    let cells: Vec<_> = world
        .query::<(Entity, &StreamedTerrainCell)>()
        .iter(world)
        .map(|(entity, cell)| (entity, *cell))
        .collect();
    if cells.iter().any(|&(entity, cell)| {
        cell.detail == CellDetail::Full
            && !forge_engine::physics::static_collider_ready(world, entity)
    }) {
        forge_engine::observe_render_readiness(world, false);
        return Ok(false);
    }
    if stats.resident_cells != 81 || stats.detailed_cells != 9 || cells.len() != 81 {
        return Err("Unexpected native cell residency".into());
    }
    let readiness = forge_engine::observe_render_readiness(world, true)
        .ok_or("Render pipeline observer absent")?;
    // Bevy also uses Err for shaders not loaded yet and retries them. Keep the
    // gate closed until every known pipeline is ready; persistent errors time out.
    if !readiness.ready_to_sample {
        proof.warmup = 0;
        proof.samples.clear();
        proof.sample_start_observation = None;
        return Ok(false);
    }
    proof.warmup += 1;
    if proof.warmup <= 120 {
        return Ok(false);
    }
    if proof.samples.len() < 300 {
        let delta = world.resource::<Time<Real>>().delta_secs_f64() * 1000.0;
        if delta > 0.0 {
            proof
                .sample_start_observation
                .get_or_insert(readiness.observation);
            proof.samples.push(delta);
        }
        return Ok(false);
    }
    let image = proof.directory.join(format!("stage-{}.png", proof.stage));
    if !proof.capture_requested {
        let mut ids: Vec<_> = cells
            .iter()
            .map(|(_, cell)| (cell.key.x, cell.key.z, cell.logical_id.to_string()))
            .collect();
        ids.sort();
        if proof.stage == 0 {
            proof.initial_ids = Some(ids.clone());
        }
        if proof.stage == 2 && proof.initial_ids.as_ref() != Some(&ids) {
            return Err("Cell identities changed after return".into());
        }
        let camera = observed_camera(world)?;
        proof.reports.push(serde_json::json!({"stage":proof.stage, "observer_xz_m":ROUTE[proof.stage], "camera":camera,
            "source_hash":cells[0].1.source, "resident_cells":stats.resident_cells,
            "full_cells":stats.detailed_cells,"coarse_cells":stats.coarse_cells,
            "cpu_mesh_bytes":stats.cpu_mesh_bytes,"resident_triangles":stats.resident_triangles,
            "committed_transitions":stats.committed_transitions,"preparation_ms":stats.last_preparation_ms,
            "colliders_backend_synchronized":true,"logical_cells":ids,
            "render_readiness":readiness,"sample_start_render_observation":proof.sample_start_observation,
            "frame_samples_ms":proof.samples,"distribution":FrameDistribution::from_samples_ms(&proof.samples).map_err(|e|e.to_string())?}));
        world.spawn(Screenshot::primary_window()).observe(
            move |event: On<ScreenshotCaptured>, mut proof: ResMut<Proof>| {
                save_to_disk(&image)(event);
                proof.capture_complete = true;
            },
        );
        proof.capture_requested = true;
        return Ok(false);
    }
    if !proof.capture_complete {
        return Ok(false);
    }
    if fs::metadata(&image).map_err(|e| e.to_string())?.len() == 0 {
        return Err("Native screenshot is empty".into());
    }
    advance(world, proof)
}

fn advance(world: &mut World, proof: &mut Proof) -> Result<bool, String> {
    proof.stage += 1;
    if proof.stage == ROUTE.len() {
        write_report(world, proof)?;
        return Ok(true);
    }
    proof.warmup = 0;
    proof.samples.clear();
    proof.sample_start_observation = None;
    proof.capture_requested = false;
    proof.capture_complete = false;
    world.resource_mut::<CellStreamingObserver>().position_xz_m = ROUTE[proof.stage];
    forge_engine::observe_render_readiness(world, false);
    let [x, z] = ROUTE[proof.stage];
    #[allow(clippy::cast_possible_truncation)]
    // Small explicit diagnostic route, not external coordinates.
    let target = Vec3::new(x as f32, 0.0, z as f32);
    for mut camera in world
        .query_filtered::<&mut Transform, With<Camera3d>>()
        .iter_mut(world)
    {
        *camera = Transform::from_translation(target + Vec3::new(-170.0, 160.0, 200.0))
            .looking_at(target, Vec3::Y);
    }
    Ok(false)
}

fn observed_camera(world: &mut World) -> Result<serde_json::Value, String> {
    let (local, global, projection) = world
        .query_filtered::<(&Transform, &GlobalTransform, &Projection), With<Camera3d>>()
        .single(world)
        .map_err(|error| error.to_string())?;
    let Projection::Perspective(perspective) = projection else {
        return Err("Region diagnostic requires a perspective camera".into());
    };
    let global = global.compute_transform();
    Ok(serde_json::json!({
        "local_translation":local.translation.to_array(), "local_rotation":local.rotation.to_array(),
        "global_translation":global.translation.to_array(), "global_rotation":global.rotation.to_array(),
        "perspective":{"fov":perspective.fov,"aspect_ratio":perspective.aspect_ratio,
            "near":perspective.near,"far":perspective.far}
    }))
}

fn write_report(world: &World, proof: &Proof) -> Result<(), String> {
    let adapter = world.get_resource::<RenderAdapterInfo>().map(|info| {
        serde_json::json!({"name":info.name,
        "driver":info.driver,"driver_info":info.driver_info,"backend":format!("{:?}",info.backend)})
    });
    let report = serde_json::json!({"schema":"forge.gate7.native-cell-region-smoke.v3",
        "region_extent_m":[2048,2048],"raster_bytes":4_194_304,"resolution":[1920,1080],
        "scope":"synthetic_terrain_camera_teleports_not_authored_gameplay_or_continuous_traversal",
        "frame_timing_scope":"main_app_intervals_after_known_pipelines_stable_not_gpu_timestamps",
        "renderer":adapter,"stages":proof.reports,"watchdog_seconds":proof.timeout_seconds,
        "cpu_mesh_bytes_exclude":"raster_gpu_rapier_internal_allocations",
        "general_hlod_qualified":false,"gate_closed":false});
    let mut file = fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(proof.directory.join("report.json"))
        .map_err(|e| e.to_string())?;
    file.write_all(&serde_json::to_vec_pretty(&report).map_err(|e| e.to_string())?)
        .and_then(|()| file.sync_all())
        .map_err(|e| e.to_string())
}
