//! Exercise a late resident shore against the real engine render schedule.
use bevy::{
    app::{AppExit, ScheduleRunnerPlugin},
    diagnostic::FrameCount,
    prelude::*,
    render::gpu_readback::{Readback, ReadbackComplete},
    window::ExitCondition,
    winit::WinitPlugin,
};
use forge_engine::{source_ocean, source_shore_sim};
use std::{
    collections::BTreeMap,
    error::Error,
    ffi::OsString,
    path::{Path, PathBuf},
    sync::atomic::Ordering,
    time::Duration,
};

pub(super) fn run_module(
    args: &[OsString],
    coupled: bool,
    rectangular: bool,
) -> Result<(), Box<dyn Error>> {
    if args.len() != 5 {
        return Err("usage: --live-handshake-module PACKAGE SHA256 AREA CACHE OUTPUT".into());
    }
    let package = PathBuf::from(&args[0]);
    let expected = args[1].to_string_lossy();
    let area: forge_core::AreaId = args[2].to_string_lossy().parse()?;
    let cache = PathBuf::from(&args[3]);
    let output = PathBuf::from(&args[4]);
    if !package.is_absolute() || !cache.is_absolute() || !output.is_absolute() || output.exists() {
        return Err("absolute package, cache and fresh output required".into());
    }
    if forge_delivery::file_hash(&package)? != expected {
        return Err("module package hash differs".into());
    }
    let loaded = forge_delivery::module_runtime::load(&package, area, Some(&cache))?;
    let resources = loaded.ocean()?.ok_or("module ocean absent")?;
    let surface = resources.surface.ok_or("module water surface absent")?;
    let surface = if rectangular {
        synthetic_water_level(&surface, 3.5)?
    } else {
        surface
    };
    let shore_package = resources.shore_sim.ok_or("module shore absent")?;
    let (terrain, generated_heights) = if rectangular {
        let source_heights = shore_package
            .file("heights.f32.bin")
            .ok_or("reference terrain heights absent")?;
        let (terrain, heights) = rectangular_terrain(source_heights)?;
        (terrain, Some(heights))
    } else {
        (loaded.zone.head().effective()?.0, None)
    };
    let raster = terrain
        .sampled_source
        .as_ref()
        .ok_or("module raster absent")?;
    let heights = if let Some(heights) = &generated_heights {
        heights.as_slice()
    } else {
        loaded
            .source_blobs
            .get(&raster.samples_sha256)
            .ok_or("module heights absent")?
            .as_slice()
    };
    let plan = forge_world::sampled_terrain::SampledTerrainPlan::from_document(&terrain, heights)?;
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
    .add_plugins(ScheduleRunnerPlugin::run_loop(Duration::from_millis(16)));
    if coupled {
        if rectangular {
            validate_rectangular_inputs(&shore_package, &surface, &terrain, &plan, heights)?;
        }
        let host = forge_engine::source_water_host::PreparedHost::new(
            &resources.package,
            &shore_package,
            surface,
            &terrain,
            &plan,
            heights,
        )?;
        let center = [8.0, -85.0];
        if rectangular {
            validate_rectangular_gpu_inputs(&host, &plan, center)?;
        }
        let document = host.document_hash();
        // These statuses permit installation only; neither fabricated host is
        // marked ready, and the probe does not claim a complete rendered scene.
        app.insert_resource(forge_engine::source_environment::host::Status::default())
            .insert_resource(forge_engine::source_terrain::host::Status {
                document: Some(document),
                ..default()
            });
        host.prepare_resident_scene(std::sync::Arc::new(plan), center)?
            .install(&mut app, false)?;
        run_coupled(app, &output, rectangular)
    } else {
        let ocean = source_ocean::PreparedOcean::new(&resources.package)?;
        let shore = source_shore_sim::PreparedShoreSim::new(
            &shore_package,
            &surface,
            &terrain,
            &plan,
            heights,
        )?;
        source_ocean::register(&mut app)?;
        source_shore_sim::register(&mut app)?;
        ocean.stage(app.world_mut())?;
        run(app, shore, &output)
    }
}

fn validate_rectangular_inputs(
    shore_package: &forge_assets::source_shore_sim_package::ShoreSimPackage,
    surface: &forge_assets::source_water_surface_package::WaterSurfacePackage,
    terrain: &forge_project::TerrainDocument,
    plan: &forge_world::sampled_terrain::SampledTerrainPlan,
    heights: &[u8],
) -> Result<(), String> {
    let shore =
        source_shore_sim::PreparedShoreSim::new(shore_package, surface, terrain, plan, heights)?;
    let optics = forge_engine::source_water_optics::PreparedOptics::new(
        surface,
        terrain,
        plan,
        Some(shore.shore_field()),
        Some(shore_package),
    )?;
    if shore.shore_field().domain().side() != 1024
        || optics.shore_field().0 != shore.shore_field().bytes()
        || !optics.shore_field().1
    {
        return Err("rectangular shore and optical inputs disagree".into());
    }
    Ok(())
}

fn validate_rectangular_gpu_inputs(
    host: &forge_engine::source_water_host::PreparedHost,
    plan: &forge_world::sampled_terrain::SampledTerrainPlan,
    center: [f64; 2],
) -> Result<(), String> {
    let input = host.breakers_near_camera(plan, center)?;
    let gpu = input.gpu_inputs();
    gpu.validate()?;
    let layout = gpu.terrain_layout();
    if layout.heights != [4096, 2048]
        || layout.shore_side != 1024
        || layout.deltas != [4097, 2049]
        || input.shore_gpu_inputs().water_level.to_bits() != 3.5_f32.to_bits()
    {
        return Err("rectangular GPU inputs lost their admitted domain".into());
    }
    Ok(())
}

/// In-memory qualification fixture; the pinned module and its assets stay unchanged.
fn rectangular_terrain(
    reference_heights: &[u8],
) -> Result<(forge_project::TerrainDocument, Vec<u8>), String> {
    use forge_project::{
        HeightSamplingAlgorithm, TERRAIN_DOCUMENT_SCHEMA_VERSION_V4, TerrainExtent,
        TerrainHeightDelta,
        sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
    };
    let mut document = forge_project::authoring::new_terrain(
        "water-domain",
        "authoring",
        "Rectangular water qualification",
    )?;
    document.schema_version = TERRAIN_DOCUMENT_SCHEMA_VERSION_V4;
    document.extent_m = TerrainExtent::new(4096.0, 2048.0);
    document.height_sampling.algorithm = HeightSamplingAlgorithm::SampledRasterV1;
    document.height_sampling.sample_spacing_m = 1.0;
    document.height_sampling.base_elevation_m = 0.0;
    document.height_sampling.amplitude_m = 0.0;
    document.height_sampling.base_frequency_cycles_per_m = 1.0;
    document.height_sampling.octaves = 1;
    document.height_sampling.lacunarity = 2.0;
    document.height_sampling.persistence = 0.5;
    if reference_heights.len() != 2048 * 2048 * 4 {
        return Err("reference terrain height size differs".into());
    }
    let sea = (-86.5_f32).to_le_bytes();
    let mut heights = Vec::with_capacity(4096 * 2048 * 4);
    for row in reference_heights.as_chunks::<8192>().0 {
        for _ in 0..1024 {
            heights.extend_from_slice(&sea);
        }
        for bits in row.as_chunks::<4>().0 {
            heights.extend_from_slice(&(f32::from_le_bytes(*bits) + 3.5).to_le_bytes());
        }
        for _ in 0..1024 {
            heights.extend_from_slice(&sea);
        }
    }
    document.sampled_source = Some(SampledTerrainSource {
        schema: SAMPLED_TERRAIN_SCHEMA.into(),
        schema_version: 1,
        width: 4096,
        height: 2048,
        origin_xz_m: [-2048.0, -1024.0],
        sample_spacing_m: 1.0,
        outside_height_m: -86.5,
        encoding: HeightEncoding::Float32Le,
        samples_sha256: forge_core::bytes_sha256(&heights),
    });
    document.height_deltas = vec![TerrainHeightDelta::new(64, 64, 1.25)];
    document.validate().map_err(|error| error.to_string())?;
    Ok((document, heights))
}

/// Re-pin only the in-memory water frame used by this synthetic qualification.
fn synthetic_water_level(
    surface: &forge_assets::source_water_surface_package::WaterSurfacePackage,
    water_level: f32,
) -> Result<forge_assets::source_water_surface_package::WaterSurfacePackage, String> {
    let mut names: Vec<String> = [
        "ocean-geometry.wgsl",
        "water-optics.wgsl",
        "shore-field.rgba32.bin",
        "sea-detail-noise.rgba16.bin",
        "LICENSE-Tidewater.txt",
        "water-frame.bin",
        "uniform-waterSurface.bin",
        "uniform-mat.bin",
        "uniform-skyParams.bin",
        "uniform-whaleMarks.bin",
        "uniform-shoreP.bin",
        "uniform-shoreSimP.bin",
        "uniform-seaDetailParams.bin",
    ]
    .into_iter()
    .map(str::to_owned)
    .collect();
    names.extend((0..11).map(|mip| format!("foam-m{mip}.rgba16.bin")));
    names.extend((0..10).map(|mip| format!("lace-m{mip}.rgba8.bin")));
    let mut files = BTreeMap::new();
    let mut pins = BTreeMap::new();
    for name in names {
        let mut bytes = surface
            .file(&name)
            .ok_or("water fixture input absent")?
            .to_vec();
        if name == "water-frame.bin" {
            bytes[588..592].copy_from_slice(&water_level.to_le_bytes());
        }
        pins.insert(
            name.clone(),
            serde_json::json!({"sha256":forge_core::bytes_sha256(&bytes).to_string(),"bytes":bytes.len()}),
        );
        files.insert(name, bytes);
    }
    let manifest = serde_json::to_vec(&serde_json::json!({
        "schema":"forge.tidewater.water-surface-package",
        "version":1,
        "source_commit":"1438b1abfcaee3267092b75573014f4d9b4a983c",
        "source_bundle_sha256":"c739c737914261991fd9aba21c4372e7e9cafa31527f7a4139001a4db8e9b335",
        "files":pins,
    }))
    .map_err(|error| error.to_string())?;
    forge_assets::source_water_surface_package::WaterSurfacePackage::load(
        &manifest,
        forge_core::bytes_sha256(&manifest),
        |name, _| {
            files
                .remove(name)
                .ok_or_else(|| format!("missing synthetic input: {name}"))
        },
    )
}

#[derive(Resource)]
struct Scenario {
    prepared: Option<source_shore_sim::PreparedShoreSim>,
    output: PathBuf,
    staged_at: Option<u32>,
    joined_at: Option<u32>,
    waiting_samples: u32,
    coherent_samples: u32,
    max_live_lag: u32,
}

pub(super) fn run(
    mut app: App,
    shore: source_shore_sim::PreparedShoreSim,
    output: &Path,
) -> Result<(), Box<dyn Error>> {
    app.insert_resource(Scenario {
        prepared: Some(shore),
        output: output.to_path_buf(),
        staged_at: None,
        joined_at: None,
        waiting_samples: 0,
        coherent_samples: 0,
        max_live_lag: 0,
    })
    .add_systems(Update, observe);
    if app.run() != AppExit::Success || !output.is_file() {
        return Err("live resident shore handshake did not complete".into());
    }
    Ok(())
}

fn observe(world: &mut World) {
    let Some(mut scenario) = world.remove_resource::<Scenario>() else {
        return;
    };
    match observe_frame(world, &mut scenario) {
        Ok(true) => {
            let report = serde_json::json!({
                "scenario": "late-resident-shore",
                "staged_at": scenario.staged_at,
                "joined_at": scenario.joined_at,
                "waiting_samples": scenario.waiting_samples,
                "coherent_live_samples": scenario.coherent_samples,
                "maximum_live_step_lag": scenario.max_live_lag,
                "pass": true,
            });
            match std::fs::OpenOptions::new()
                .write(true)
                .create_new(true)
                .open(&scenario.output)
                .and_then(|file| {
                    serde_json::to_writer_pretty(file, &report).map_err(std::io::Error::other)
                }) {
                Ok(()) => {
                    world.write_message(AppExit::Success);
                }
                Err(error) => {
                    eprintln!("handshake receipt: {error}");
                    world.write_message(AppExit::error());
                }
            }
        }
        Ok(false) => {
            world.insert_resource(scenario);
        }
        Err(error) => {
            eprintln!("handshake: {error}");
            world.write_message(AppExit::error());
        }
    }
}

fn observe_frame(world: &mut World, scenario: &mut Scenario) -> Result<bool, String> {
    if world.resource::<FrameCount>().0 > 2_000 {
        return Err("timed out before live shore synchronization".into());
    }
    let ocean = world.resource::<source_ocean::Targets>().clone();
    let current = ocean.completed_steps.load(Ordering::Acquire);
    if scenario.prepared.is_some() {
        if ocean.ready.load(Ordering::Acquire) && current >= 140 {
            scenario
                .prepared
                .take()
                .expect("checked shore")
                .stage(world)?;
            scenario.staged_at = Some(current);
        }
        return Ok(false);
    }
    let shore = world.resource::<source_shore_sim::Targets>();
    if !shore.ready.load(Ordering::Acquire) {
        scenario.waiting_samples += 1;
        return Ok(false);
    }
    let shore_step = shore.completed_steps.load(Ordering::Acquire);
    let latest = ocean.completed_steps.load(Ordering::Acquire);
    if latest != current {
        return Ok(false); // Sampling straddled a render-world update.
    }
    if scenario.joined_at.is_none() {
        if shore_step == latest && latest >= scenario.staged_at.expect("staged") {
            scenario.joined_at = Some(latest);
        }
        return Ok(false);
    }
    let lag = latest
        .checked_sub(shore_step)
        .ok_or("shore ran ahead after joining")?;
    scenario.max_live_lag = scenario.max_live_lag.max(lag);
    if lag > 1 {
        return Err(format!("ocean advanced {lag} steps ahead of shore"));
    }
    scenario.coherent_samples += 1;
    Ok(scenario.waiting_samples > 0
        && scenario.coherent_samples >= 10
        && latest >= scenario.joined_at.expect("joined") + 20)
}

#[derive(Resource)]
struct CoupledScenario {
    output: PathBuf,
    frame: Option<source_ocean::live_clock::LiveStep>,
    final_frame: Option<source_ocean::live_clock::LiveStep>,
    held_samples: u32,
    rectangular: bool,
    phase: CoupledPhase,
}

#[derive(Clone, Copy, PartialEq, Eq)]
enum CoupledPhase {
    Holding,
    Running,
    Draining,
    Reading,
}

#[derive(Resource, Default)]
struct CoupledReadback {
    shore_sha256: Option<String>,
    shore_nonzero_f16: Option<usize>,
    head: Option<u32>,
    error: Option<String>,
}

fn run_coupled(mut app: App, output: &Path, rectangular: bool) -> Result<(), Box<dyn Error>> {
    app.insert_resource(CoupledScenario {
        output: output.to_path_buf(),
        frame: None,
        final_frame: None,
        held_samples: 0,
        rectangular,
        phase: CoupledPhase::Holding,
    })
    .init_resource::<CoupledReadback>()
    .add_systems(Update, observe_coupled);
    if app.run() != AppExit::Success || !output.is_file() {
        return Err("coupled live water handshake did not complete".into());
    }
    Ok(())
}

fn observe_coupled(world: &mut World) {
    let Some(mut scenario) = world.remove_resource::<CoupledScenario>() else {
        return;
    };
    match observe_coupled_frame(world, &mut scenario) {
        Ok(true) => {
            let readback = world.resource::<CoupledReadback>();
            let first = scenario.frame.expect("observed live frame");
            let last = scenario.final_frame.expect("drained live frame");
            let report = serde_json::json!({
                "scenario": "resident-ocean-shore-spray",
                "initial_step": first.step,
                "step": last.step,
                "live_steps_after_resume": last.step - first.step,
                "held_samples_before_camera": scenario.held_samples,
                "synthetic_rectangular_terrain": scenario.rectangular,
                "terrain_samples": if scenario.rectangular { Some([4096, 2048]) } else { None },
                "shore_field_side": if scenario.rectangular { Some(1024) } else { None },
                "terrain_extent_m": if scenario.rectangular { Some([4096, 2048]) } else { None },
                "terrain_sample_spacing_m": if scenario.rectangular { Some(1) } else { None },
                "terrain_shape": if scenario.rectangular { Some("reference-shore-in-extended-sea") } else { None },
                "water_level_m": if scenario.rectangular { Some(3.5) } else { None },
                "authored_height_delta": scenario.rectangular,
                "fft_shore_spray_encoded_same_step": true,
                "shore_readback_sha256": readback.shore_sha256,
                "shore_nonzero_f16": readback.shore_nonzero_f16,
                "spray_ring_head": readback.head,
                "shoreline_stations": world.resource::<forge_engine::source_breaker_resident::Targets>().stations(),
                "gpu_completion_fence_observed": true,
                "visual_or_performance_qualification": false,
                "pass": true,
            });
            let result = std::fs::OpenOptions::new()
                .write(true)
                .create_new(true)
                .open(&scenario.output)
                .and_then(|file| {
                    serde_json::to_writer_pretty(file, &report).map_err(std::io::Error::other)
                });
            match result {
                Ok(()) => {
                    world.write_message(AppExit::Success);
                }
                Err(error) => {
                    eprintln!("coupled handshake receipt: {error}");
                    world.write_message(AppExit::error());
                }
            }
        }
        Ok(false) => {
            world.insert_resource(scenario);
        }
        Err(error) => {
            eprintln!("coupled handshake: {error}");
            world.write_message(AppExit::error());
        }
    }
}

fn observe_coupled_frame(
    world: &mut World,
    scenario: &mut CoupledScenario,
) -> Result<bool, String> {
    if world.resource::<FrameCount>().0 > 4_000 {
        return Err("timed out before coupled water synchronization".into());
    }
    let clock = source_ocean::live_clock(world).ok_or("ocean clock absent")?;
    let ocean = world.resource::<source_ocean::Targets>();
    let shore = world.resource::<source_shore_sim::Targets>();
    let effects = world.resource::<forge_engine::source_breaker_resident::Targets>();
    if let Some(error) = effects.error() {
        return Err(error);
    }
    if scenario.frame.is_none() {
        let Some(frame) = clock.snapshot().pending else {
            return Ok(false);
        };
        if clock.encoded_for_dependent(frame.step) != Some(frame) {
            return Ok(false);
        }
        clock.pause();
        scenario.frame = Some(frame);
    }
    let frame = scenario.frame.expect("observed live frame");
    if scenario.held_samples < 8 {
        let state = clock.snapshot();
        if state.quiescent
            || state.pending != Some(frame)
            || clock.completed(frame.step).is_some()
            || ocean.completed_steps.load(Ordering::Acquire) != frame.step
            || effects.completed_steps.load(Ordering::Acquire) != 0
        {
            return Err("water frame advanced before the effect camera was ready".into());
        }
        scenario.held_samples += 1;
        if scenario.held_samples == 8 {
            effects.update_camera(Mat4::IDENTITY, GlobalTransform::default())?;
        }
        return Ok(false);
    }
    let snapshot = clock.snapshot();
    if scenario.phase == CoupledPhase::Holding {
        if !snapshot.quiescent {
            return Ok(false);
        }
        if clock.completed(frame.step) != Some(frame)
            || shore.completed_steps.load(Ordering::Acquire) != frame.step
            || effects.completed_steps.load(Ordering::Acquire) != frame.step
            || source_ocean::completed_frame(world) != Some(frame)
        {
            return Err("ocean, shore and spray published different live frames".into());
        }
        let elapsed = world.resource::<Time<Real>>().elapsed_secs_f64();
        clock.resume_at(elapsed)?;
        scenario.phase = CoupledPhase::Running;
        return Ok(false);
    }
    if scenario.phase == CoupledPhase::Running {
        if snapshot
            .completed
            .is_some_and(|last| last.step >= frame.step + 30)
        {
            clock.pause();
            scenario.phase = CoupledPhase::Draining;
        }
        return Ok(false);
    }
    if !snapshot.quiescent {
        return Ok(false);
    }
    let last = snapshot.completed.ok_or("completed live frame absent")?;
    if last.step < frame.step + 30
        || shore.completed_steps.load(Ordering::Acquire) != last.step
        || effects.completed_steps.load(Ordering::Acquire) != last.step
        || source_ocean::completed_frame(world) != Some(last)
    {
        return Err("coupled water lost synchronization after resuming".into());
    }
    scenario.final_frame = Some(last);
    if scenario.phase == CoupledPhase::Draining {
        let shore_texture = shore.state.clone();
        let spray_head = effects.buffers[4].clone();
        world
            .spawn(Readback::texture(shore_texture))
            .observe(capture_shore);
        world
            .spawn(Readback::buffer(spray_head))
            .observe(capture_spray_head);
        scenario.phase = CoupledPhase::Reading;
        return Ok(false);
    }
    let readback = world.resource::<CoupledReadback>();
    if let Some(error) = &readback.error {
        return Err(error.clone());
    }
    Ok(readback.shore_sha256.is_some()
        && readback.shore_nonzero_f16.is_some_and(|count| count > 0)
        && readback.head.is_some())
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy observer parameters")]
fn capture_shore(
    event: On<ReadbackComplete>,
    mut result: ResMut<CoupledReadback>,
    mut commands: Commands,
) {
    if event.data.len() != 768 * 768 * 8
        || event
            .data
            .as_chunks::<2>()
            .0
            .iter()
            .any(|bits| u16::from_le_bytes(*bits) & 0x7c00 == 0x7c00)
    {
        result.error = Some("invalid shore GPU readback".into());
    } else {
        result.shore_sha256 = Some(forge_core::bytes_sha256(&event.data).to_string());
        result.shore_nonzero_f16 = Some(
            event
                .data
                .as_chunks::<2>()
                .0
                .iter()
                .filter(|bits| u16::from_le_bytes(**bits) & 0x7fff != 0)
                .count(),
        );
    }
    commands.entity(event.entity).despawn();
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy observer parameters")]
fn capture_spray_head(
    event: On<ReadbackComplete>,
    mut result: ResMut<CoupledReadback>,
    mut commands: Commands,
) {
    if let Ok(bytes) = <[u8; 4]>::try_from(event.data.as_slice()) {
        // The source head is a monotonically increasing reservation counter,
        // not an index bounded by the particle-ring capacity.
        result.head = Some(u32::from_le_bytes(bytes));
    } else {
        result.error = Some("invalid spray GPU readback size".into());
    }
    commands.entity(event.entity).despawn();
}
