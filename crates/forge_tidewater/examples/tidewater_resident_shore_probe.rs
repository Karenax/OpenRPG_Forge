//! Read the actual resident `ShoreSim` after the acquired ocean has stopped advancing.
#[path = "tidewater_resident_shore_probe/handshake.rs"]
mod handshake;

use bevy::{
    app::{AppExit, ScheduleRunnerPlugin},
    diagnostic::FrameCount,
    prelude::*,
    render::gpu_readback::{Readback, ReadbackComplete},
    window::ExitCondition,
    winit::WinitPlugin,
};
use forge_assets::source_ocean_package::OceanPackage;
use forge_assets::{
    source_shore_sim_package::ShoreSimPackage, source_water_surface_package::WaterSurfacePackage,
};
use forge_core::{ContentHash, bytes_sha256};
use forge_engine::source_ocean::{self, PreparedOcean};
use forge_engine::source_shore_sim::{self, PreparedShoreSim, Targets};
use forge_project::{
    HeightSamplingAlgorithm, TERRAIN_DOCUMENT_SCHEMA_VERSION_V4, TerrainExtent,
    authoring::new_terrain,
    sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
};
use forge_world::sampled_terrain::SampledTerrainPlan;
use std::{
    error::Error,
    fs,
    path::{Path, PathBuf},
    sync::{
        Arc,
        atomic::{AtomicBool, Ordering},
    },
    time::Duration,
};

#[derive(Resource)]
struct Output {
    file: PathBuf,
    requested: bool,
    finished: Arc<AtomicBool>,
    hash_only: bool,
}

#[allow(
    clippy::too_many_lines,
    reason = "This probe checks one complete bounded GPU input and readback path."
)]
fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args_os().skip(1).collect();
    if args
        .first()
        .is_some_and(|value| value == "--live-handshake-module")
    {
        return handshake::run_module(&args[1..], false, false);
    }
    if args
        .first()
        .is_some_and(|value| value == "--live-coupled-module")
    {
        return handshake::run_module(&args[1..], true, false);
    }
    if args
        .first()
        .is_some_and(|value| value == "--live-rectangular-water")
    {
        return handshake::run_module(&args[1..], true, true);
    }
    if !(8..=9).contains(&args.len()) {
        return Err("usage: tidewater_resident_shore_probe OCEAN OCEAN_SHA SHORE SHORE_SHA SURFACE SURFACE_SHA OUTPUT reference|alternate|reference-hash|alternate-hash [live-handshake]".into());
    }
    let live_handshake = match args.get(8).and_then(|value| value.to_str()) {
        None => false,
        Some("live-handshake") => true,
        Some(_) => return Err("unknown probe scenario".into()),
    };
    let roots = [
        PathBuf::from(&args[0]),
        PathBuf::from(&args[2]),
        PathBuf::from(&args[4]),
    ];
    let pins: Vec<ContentHash> = [1, 3, 5]
        .into_iter()
        .map(|i| args[i].to_string_lossy().parse())
        .collect::<Result<_, _>>()?;
    let output = PathBuf::from(&args[6]);
    let (alternate, hash_only) = match args[7].to_str() {
        Some("reference") => (false, false),
        Some("alternate") => (true, false),
        Some("reference-hash") => (false, true),
        Some("alternate-hash") => (true, true),
        _ => return Err("unknown terrain mode".into()),
    };
    if live_handshake && hash_only {
        return Err("hash-only output is unavailable for the live handshake".into());
    }
    if !output.is_absolute() || output.exists() || roots.iter().any(|p| !p.is_absolute()) {
        return Err("absolute existing inputs and a fresh absolute output are required".into());
    }
    let manifests: Vec<_> = roots
        .iter()
        .map(|root| fs::read(root.join("package.json")))
        .collect::<Result<_, _>>()?;
    let read = |root: &Path, name: &str| fs::read(root.join(name)).map_err(|e| e.to_string());
    let ocean = OceanPackage::load(&manifests[0], pins[0], |name, _| read(&roots[0], name))?;
    let shore = ShoreSimPackage::load(&manifests[1], pins[1], |name, _| read(&roots[1], name))?;
    let surface =
        WaterSurfacePackage::load(&manifests[2], pins[2], |name, _| read(&roots[2], name))?;
    if ocean.bootstrap_frames() != 124 {
        return Err("expected the 124-step acquired reference".into());
    }
    let heights = if alternate {
        let row: Vec<u8> = (0_u16..2048)
            .flat_map(|x| {
                ((f32::from(x) - 1024.0) / 16.0)
                    .clamp(-15.0, 15.0)
                    .to_le_bytes()
            })
            .collect();
        row.repeat(2048)
    } else {
        shore
            .file("heights.f32.bin")
            .ok_or("source raster absent")?
            .to_vec()
    };
    let mut doc = new_terrain("resident-shore-probe", "shore", "Shore probe")?;
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
        samples_sha256: bytes_sha256(&heights),
    });
    let plan = SampledTerrainPlan::from_document(&doc, &heights)?;
    let prepared_ocean = PreparedOcean::new(&ocean)?;
    let prepared_shore = PreparedShoreSim::new(&shore, &surface, &doc, &plan, &heights)?;
    if prepared_shore.shore_field().is_derived() != alternate {
        return Err("unexpected shore source selection".into());
    }
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
    source_ocean::register(&mut app)?;
    source_shore_sim::register(&mut app)?;
    let ocean_targets = prepared_ocean.stage(app.world_mut())?;
    if live_handshake {
        return handshake::run(app, prepared_shore, &output);
    }
    let clock = source_ocean::live_clock(app.world()).ok_or("ocean clock absent")?;
    clock.pause();
    let shore_targets = prepared_shore.stage(app.world_mut())?;
    let finished = Arc::new(AtomicBool::new(false));
    app.insert_resource(Output {
        file: output,
        requested: false,
        finished: finished.clone(),
        hash_only,
    })
    .add_systems(Update, request_readback);
    let exit = app.run();
    if exit != AppExit::Success
        || !finished.load(Ordering::Acquire)
        || shore_targets.completed_steps.load(Ordering::Acquire) != 124
        || ocean_targets.completed_steps.load(Ordering::Acquire) != 124
    {
        return Err("resident shore readback did not complete at the expected frame".into());
    }
    Ok(())
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn request_readback(
    mut commands: Commands,
    targets: Res<Targets>,
    mut output: ResMut<Output>,
    frames: Res<FrameCount>,
    mut exit: MessageWriter<AppExit>,
) {
    if frames.0 > 1200 {
        exit.write(AppExit::error());
        return;
    }
    if output.requested
        || !targets.ready.load(Ordering::Acquire)
        || targets.completed_steps.load(Ordering::Acquire) != 124
    {
        return;
    }
    output.requested = true;
    commands
        .spawn(Readback::texture(targets.state.clone()))
        .observe(
            |event: On<ReadbackComplete>,
             output: Res<Output>,
             mut commands: Commands,
             mut exit: MessageWriter<AppExit>| {
                let write = || -> std::io::Result<()> {
                    use std::io::Write;
                    if event.data.len() != 768 * 768 * 8 {
                        return Err(std::io::Error::other("shore readback size mismatch"));
                    }
                    let mut file = fs::OpenOptions::new()
                        .write(true)
                        .create_new(true)
                        .open(&output.file)?;
                    if output.hash_only {
                        let values = event.data.as_chunks::<2>().0;
                        let nonfinite = values
                            .iter()
                            .filter(|bits| u16::from_le_bytes(**bits) & 0x7c00 == 0x7c00)
                            .count();
                        let nonzero = values
                            .iter()
                            .filter(|bits| u16::from_le_bytes(**bits) & 0x7fff != 0)
                            .count();
                        serde_json::to_writer(
                            &mut file,
                            &serde_json::json!({
                                "bytes":event.data.len(),
                                "sha256":bytes_sha256(&event.data).to_string(),
                                "nonfinite_f16":nonfinite,
                                "nonzero_f16":nonzero,
                            }),
                        )
                        .map_err(std::io::Error::other)?;
                    } else {
                        file.write_all(&event.data)?;
                    }
                    Ok(())
                };
                if let Err(error) = write() {
                    eprintln!("{error}");
                    exit.write(AppExit::error());
                } else {
                    output.finished.store(true, Ordering::Release);
                    exit.write(AppExit::Success);
                }
                commands.entity(event.entity).despawn();
            },
        );
}
