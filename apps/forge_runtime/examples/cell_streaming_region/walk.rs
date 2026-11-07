//! Rendered replay of the real player controller over streamed, painted terrain.
//! Synthetic integration evidence; no installed package, gameplay or saves are qualified here.
use bevy::{
    app::AppExit,
    prelude::*,
    render::{
        renderer::RenderAdapterInfo,
        view::screenshot::{Screenshot, ScreenshotCaptured, save_to_disk},
    },
    window::{PresentMode, WindowResolution},
};
use bevy_rapier3d::prelude::{Collider, RigidBody, Velocity};
use forge_core::{EntityId, FrameDistribution};
use forge_engine::cell_streaming::{
    CellStreamingStats, SampledCellStreamingPlugin, StreamedTerrainCell,
};
use forge_engine::{
    PendingPlayerControlCommand, PlayerControlCommand, PlayerControllerPlugin, PlayerControllerSet,
    PlayerRuntimeSnapshot,
    cell_streaming::{
        PlayerCellStreamingPlugin, PlayerCellStreamingSet, PlayerCellStreamingStatus,
    },
};
use forge_world::{cell_streaming::CellStreamingPolicy, sampled_terrain::SampledTerrainPlan};
use std::{ffi::OsString, fs, io::Write, path::PathBuf, sync::Arc};

const START: Vec3 = Vec3::new(-160.0, 0.0, -128.0);
const END_X: f32 = 160.0;
const BODY_X: f32 = -640.0;

#[derive(Clone, Copy, PartialEq, Eq)]
enum CaptureState {
    Idle,
    Requested,
    Complete,
}

#[derive(Resource)]
struct Walk {
    directory: PathBuf,
    source: Arc<SampledTerrainPlan>,
    phase: u8,
    body: Option<Entity>,
    body_floor: f32,
    body_minimum: f32,
    last_position: Option<Vec3>,
    checked_ticks: u64,
    trajectory: Vec<serde_json::Value>,
    failure: Option<String>,
    stable_frames: u32,
    frame_samples: Vec<f64>,
    stable_motion_intervals: usize,
    leg_started_seconds: f64,
    legs: Vec<serde_json::Value>,
    stages: Vec<serde_json::Value>,
    initial_ids: Option<Vec<(u16, u16, String)>>,
    capture: CaptureState,
    paint_capture: CaptureState,
    maximum_cells: usize,
    maximum_bytes: u64,
    held_updates: u64,
    finished: bool,
}

pub(super) fn run(args: &[OsString]) -> AppExit {
    match prepare(args) {
        Ok(mut app) => app.run(),
        Err(error) => {
            eprintln!("{error}");
            AppExit::error()
        }
    }
}

#[allow(clippy::cast_possible_truncation)] // Source grid and diagnostic coordinates are bounded.
#[allow(
    clippy::too_many_lines,
    reason = "One diagnostic bootstrap binds its immutable fixture, real renderer and real player before startup"
)]
fn prepare(args: &[OsString]) -> Result<App, String> {
    let [directory] = args else {
        return Err("usage: cell_streaming_region --walk ABSOLUTE_FRESH_OUTPUT_DIRECTORY".into());
    };
    let directory = PathBuf::from(directory);
    if !directory.is_absolute() || directory.exists() {
        return Err("Walk diagnostic needs a fresh absolute output directory".into());
    }
    let original = super::source()?;
    let mut document = original.document().clone();
    for z in 436..=460 {
        for x in 480..=544 {
            document
                .material_overrides
                .push(forge_project::TerrainMaterialOverride {
                    sample_x: x,
                    sample_z: z,
                    weights: vec![forge_project::TerrainMaterialWeight {
                        layer_id: document.material_layers[1].id.clone(),
                        weight: u16::MAX,
                    }],
                });
        }
    }
    let bytes: Vec<_> = original
        .raster()
        .heights()
        .iter()
        .flat_map(|v| v.to_le_bytes())
        .collect();
    let source =
        Arc::new(SampledTerrainPlan::from_document(&document, &bytes).map_err(|e| e.to_string())?);
    let floor = |x: f32| {
        source
            .height_at(f64::from(x), f64::from(START.z))
            .map(|v| v as f32)
            .map_err(|e| e.to_string())
    };
    let body_floor = floor(BODY_X)?;
    let profile = forge_world::embedded_visual_valley_player_controller_profile()
        .map_err(|e| e.to_string())?;
    let spawn = START
        + Vec3::Y * (floor(START.x)? + profile.controller.total_capsule_height_m() * 0.5 + 0.05);
    let plugin = SampledCellStreamingPlugin::new(
        source.clone(),
        CellStreamingPolicy {
            origin_xz_m: [-1024.0; 2],
            cell_size_m: 64.0,
            dimensions: [32; 2],
            detail_radius: 1,
            far_radius: 4,
            max_resident_cells: 121,
            full_cell_bytes: 100_000,
            hlod_cell_bytes: 100_000,
            max_resident_bytes: 12_100_000,
            max_staging_bytes: 24_200_000,
        },
        EntityId::deterministic("gate7-painted-player-walk"),
        StandardMaterial {
            base_color: Color::WHITE,
            ..default()
        },
        [f64::from(START.x), f64::from(START.z)],
    )?
    .with_authored_material_layers();
    fs::create_dir(&directory).map_err(|e| e.to_string())?;
    let mut app = App::new();
    app.add_plugins(DefaultPlugins.set(WindowPlugin {
        primary_window: Some(Window {
            title: "OpenRPG Forge — Gate 7 · parcours cellules".into(),
            resolution: WindowResolution::new(1920, 1080),
            present_mode: PresentMode::AutoNoVsync,
            ..default()
        }),
        ..default()
    }))
    .add_plugins(forge_engine::CaptureReadinessPlugin)
    .insert_resource(ClearColor(Color::srgb(0.32, 0.43, 0.57)));
    forge_engine::sampled_terrain_physics::register(&mut app);
    app.add_plugins((
        plugin,
        PlayerControllerPlugin::new(profile, spawn)
            .with_interactive_input(false)
            .with_initial_camera_target(spawn + Vec3::NEG_Z),
        PlayerCellStreamingPlugin,
    ))
    .insert_resource(Walk {
        directory,
        source,
        phase: 0,
        body: None,
        body_floor,
        body_minimum: f32::MAX,
        last_position: None,
        checked_ticks: 0,
        trajectory: Vec::new(),
        failure: None,
        stable_frames: 0,
        frame_samples: Vec::new(),
        stable_motion_intervals: 0,
        leg_started_seconds: 0.0,
        legs: Vec::new(),
        stages: Vec::new(),
        initial_ids: None,
        capture: CaptureState::Idle,
        paint_capture: CaptureState::Idle,
        maximum_cells: 0,
        maximum_bytes: 0,
        held_updates: 0,
        finished: false,
    })
    .add_systems(Startup, setup)
    .add_systems(
        FixedUpdate,
        command
            .before(PlayerCellStreamingSet::Request)
            .before(PlayerControllerSet::Drive),
    )
    .add_systems(FixedUpdate, observe.after(PlayerControllerSet::Observe))
    .add_systems(Update, frame);
    Ok(app)
}

fn setup(world: &mut World) {
    let floor = world.resource::<Walk>().body_floor;
    let mesh = world
        .resource_mut::<Assets<Mesh>>()
        .add(Cuboid::new(1.0, 1.0, 1.0));
    let material = world
        .resource_mut::<Assets<StandardMaterial>>()
        .add(Color::srgb(0.8, 0.35, 0.12));
    let body = world
        .spawn((
            RigidBody::Dynamic,
            Collider::cuboid(0.5, 0.5, 0.5),
            Velocity::default(),
            Mesh3d(mesh),
            MeshMaterial3d(material),
            Transform::from_xyz(BODY_X, floor + 4.0, START.z),
        ))
        .id();
    world.resource_mut::<Walk>().body = Some(body);
    world.spawn((
        DirectionalLight {
            illuminance: 30_000.0,
            shadow_maps_enabled: true,
            ..default()
        },
        Transform::IDENTITY.looking_at(Vec3::new(-0.45, -1.0, -0.28), Vec3::Y),
    ));
}

fn command(world: &mut World) {
    let phase = world.resource::<Walk>().phase;
    let axis = match phase {
        1 => 1.0,
        3 => -1.0,
        _ => 0.0,
    };
    if let Err(error) =
        world
            .resource_mut::<PendingPlayerControlCommand>()
            .submit(PlayerControlCommand {
                move_axis: Vec2::new(axis, 0.0),
                sprint: true,
                ..default()
            })
    {
        world.resource_mut::<Walk>().failure = Some(error.to_string());
    }
}

#[allow(clippy::cast_possible_truncation)] // Validated diagnostic raster heights.
fn observe(world: &mut World) {
    let mut walk = world
        .remove_resource::<Walk>()
        .expect("Walk diagnostic owns its state");
    let snapshot = world.resource::<PlayerRuntimeSnapshot>();
    let position = snapshot.player_position;
    let dt = world.resource::<Time<Fixed>>().timestep().as_secs_f32();
    let profile = &world
        .resource::<forge_engine::player_controller::PlayerRuntimeProfile>()
        .0
        .controller;
    let maximum_step = profile.walk_speed_mps * profile.sprint_multiplier * dt * 2.0;
    if let Some(previous) = walk.last_position {
        let displacement = position - previous;
        if displacement.x.hypot(displacement.z) > maximum_step + 0.001 {
            walk.failure = Some("Player position discontinuity during continuous replay".into());
        }
    }
    walk.last_position = Some(position);
    walk.checked_ticks += 1;
    let floor = walk
        .source
        .height_at(f64::from(position.x), f64::from(position.z));
    if floor
        .as_ref()
        .is_ok_and(|floor| f64::from(position.y) < floor - 0.2)
    {
        walk.failure = Some("Player fell below the streamed surface".into());
    }
    if walk.checked_ticks.is_multiple_of(6) && walk.trajectory.len() < 10_000 {
        walk.trajectory.push(serde_json::json!({"tick":snapshot.fixed_tick,"phase":walk.phase,"position":position.to_array(),"grounded":snapshot.grounded}));
    }
    if let Some(body) = walk.body.and_then(|body| world.get::<Transform>(body)) {
        walk.body_minimum = walk.body_minimum.min(body.translation.y);
        if body.translation.y < walk.body_floor + 0.2 {
            walk.failure = Some("Distant dynamic body lost streamed support".into());
        }
    } else {
        walk.failure = Some("Distant dynamic body disappeared".into());
    }
    let arrived =
        (walk.phase == 1 && position.x >= END_X) || (walk.phase == 3 && position.x <= START.x);
    if arrived {
        let samples = std::mem::take(&mut walk.frame_samples);
        let elapsed = world.resource::<Time<Real>>().elapsed_secs_f64() - walk.leg_started_seconds;
        walk.legs.push(serde_json::json!({"phase":walk.phase,"arrived":position.to_array(),"wall_seconds":elapsed,"stable_pipeline_intervals":walk.stable_motion_intervals,"intervals_ms":samples,"distribution":FrameDistribution::from_samples_ms(&samples).ok()}));
        walk.stable_motion_intervals = 0;
        walk.phase += 1;
        walk.stable_frames = 0;
        world
            .resource_mut::<PendingPlayerControlCommand>()
            .cancel_input();
        forge_engine::observe_render_readiness(world, false);
    }
    world.insert_resource(walk);
}

fn frame(world: &mut World) {
    let mut walk = world
        .remove_resource::<Walk>()
        .expect("Walk diagnostic owns its state");
    if !walk.finished {
        match step(world, &mut walk) {
            Ok(true) => {
                walk.finished = true;
                world.write_message(AppExit::Success);
            }
            Ok(false) => {}
            Err(error) => {
                error!("Continuous streaming diagnostic failed: {error}");
                let _=fs::write(walk.directory.join("failure.json"),serde_json::json!({"error":error,"phase":walk.phase,"checked_ticks":walk.checked_ticks}).to_string());
                walk.finished = true;
                world.write_message(AppExit::error());
            }
        }
    }
    world.insert_resource(walk);
}

fn step(world: &mut World, walk: &mut Walk) -> Result<bool, String> {
    if let Some(error) = &walk.failure {
        return Err(error.clone());
    }
    if world.resource::<Time<Real>>().elapsed_secs_f64() > 240.0 {
        return Err("Continuous replay watchdog expired".into());
    }
    let stats = world.resource::<CellStreamingStats>().clone();
    let player = world.resource::<PlayerCellStreamingStatus>().clone();
    if let Some(error) = stats.last_error.as_ref().or(player.error.as_ref()) {
        return Err(error.clone());
    }
    walk.maximum_cells = walk.maximum_cells.max(stats.resident_cells);
    walk.maximum_bytes = walk.maximum_bytes.max(stats.cpu_mesh_bytes);
    if stats.resident_cells > 121 || stats.cpu_mesh_bytes > 12_100_000 {
        return Err("Continuous replay exceeded residency budget".into());
    }
    let readiness = forge_engine::observe_render_readiness(world, player.ready && !stats.pending)
        .ok_or("Pipeline observer absent")?;
    if walk.phase % 2 == 1 {
        record_motion(world, walk, readiness.ready_to_sample)?;
        return Ok(false);
    }
    if !readiness.ready_to_sample {
        walk.held_updates += 1;
        walk.stable_frames = 0;
        return Ok(false);
    }
    walk.stable_frames += 1;
    if walk.checked_ticks < 120 || walk.stable_frames < 120 {
        return Ok(false);
    }
    if walk.capture == CaptureState::Idle {
        let mut ids: Vec<_> = world
            .query::<&StreamedTerrainCell>()
            .iter(world)
            .map(|cell| (cell.key.x, cell.key.z, cell.logical_id.to_string()))
            .collect();
        ids.sort();
        if walk.phase == 0 {
            walk.initial_ids = Some(ids.clone());
        }
        if walk.phase == 4 && walk.initial_ids.as_ref() != Some(&ids) {
            return Err("Return residency identities differ".into());
        }
        walk.stages.push(serde_json::json!({"phase":walk.phase,"player":world.resource::<PlayerRuntimeSnapshot>().player_position.to_array(),"resident_cells":stats.resident_cells,"full_cells":stats.detailed_cells,"coarse_cells":stats.coarse_cells,"cpu_mesh_bytes":stats.cpu_mesh_bytes,"transitions":stats.committed_transitions,"mesh_assets":world.resource::<Assets<Mesh>>().len(),"logical_ids":ids,"render_readiness":readiness,"physics":player}));
        let image = walk.directory.join(format!("stage-{}.png", walk.phase));
        world.spawn(Screenshot::primary_window()).observe(
            move |event: On<ScreenshotCaptured>, mut walk: ResMut<Walk>| {
                save_to_disk(&image)(event);
                walk.capture = CaptureState::Complete;
            },
        );
        walk.capture = CaptureState::Requested;
        return Ok(false);
    }
    if walk.capture != CaptureState::Complete {
        return Ok(false);
    }
    let image = walk.directory.join(format!("stage-{}.png", walk.phase));
    if fs::metadata(image).map_err(|e| e.to_string())?.len() == 0 {
        return Err("Empty continuous replay capture".into());
    }
    if walk.phase == 4 {
        if walk.paint_capture != CaptureState::Complete
            || fs::metadata(walk.directory.join("paint-crossing.png"))
                .map_err(|e| e.to_string())?
                .len()
                == 0
        {
            return Err("Paint crossing was not captured".into());
        }
        if stats.committed_transitions < 9 {
            return Err("Too few physical cell transitions".into());
        }
        let renderer=world.get_resource::<RenderAdapterInfo>().map(|v|serde_json::json!({"name":v.name,"driver":v.driver,"backend":format!("{:?}",v.backend)}));
        let report = serde_json::json!({"schema":"forge.gate7.rendered-player-cell-walk.v2","scope":"synthetic_real_controller_continuous_640m_painted_terrain_with_distant_dynamic_body","renderer":renderer,"resolution":[1920,1080],"region_extent_m":[2048,2048],"source_hash":walk.source.document_hash(),"source_unchanged":walk.source.document().content_hash().map_err(|e|e.to_string())?==walk.source.document_hash(),"paint_overrides":walk.source.document().material_overrides.len(),"checked_fixed_ticks":walk.checked_ticks,"trajectory_every_six_checked_ticks":walk.trajectory,"stages":walk.stages,"legs":walk.legs,"maximum_resident_cells":walk.maximum_cells,"maximum_cpu_mesh_bytes":walk.maximum_bytes,"held_render_updates":walk.held_updates,"blocked_fixed_ticks":player.blocked_fixed_ticks,"distant_body_minimum_y":walk.body_minimum,"distant_body_source_floor":walk.body_floor,"frame_timing_scope":"all_main_app_intervals_during_route_phases_including_streaming_waits_and_capture_not_gpu_timestamps","paint_crossing_capture":"paint-crossing.png","cpu_mesh_bytes_exclude":"raster_gpu_rapier_internal_allocations","installed_game_qualified":false,"gameplay_saves_qualified":false,"general_hlod_qualified":false,"gate_closed":false});
        let mut file = fs::OpenOptions::new()
            .create_new(true)
            .write(true)
            .open(walk.directory.join("report.json"))
            .map_err(|e| e.to_string())?;
        file.write_all(&serde_json::to_vec_pretty(&report).map_err(|e| e.to_string())?)
            .and_then(|()| file.sync_all())
            .map_err(|e| e.to_string())?;
        return Ok(true);
    }
    walk.phase += 1;
    walk.leg_started_seconds = world.resource::<Time<Real>>().elapsed_secs_f64();
    walk.stable_frames = 0;
    walk.capture = CaptureState::Idle;
    Ok(false)
}

fn record_motion(world: &mut World, walk: &mut Walk, ready: bool) -> Result<(), String> {
    let delta = world.resource::<Time<Real>>().delta_secs_f64() * 1000.0;
    if walk.frame_samples.len() >= 100_000 {
        return Err("Bounded per-leg interval sample capacity exceeded".into());
    }
    if delta > 0.0 {
        walk.frame_samples.push(delta);
    }
    if !ready {
        walk.held_updates += 1;
        return Ok(());
    }
    walk.stable_motion_intervals += 1;
    let position = world.resource::<PlayerRuntimeSnapshot>().player_position;
    if walk.paint_capture == CaptureState::Idle && position.x.abs() < 1.0 {
        let image = walk.directory.join("paint-crossing.png");
        world.spawn(Screenshot::primary_window()).observe(
            move |event: On<ScreenshotCaptured>, mut walk: ResMut<Walk>| {
                save_to_disk(&image)(event);
                walk.paint_capture = CaptureState::Complete;
            },
        );
        walk.paint_capture = CaptureState::Requested;
    }
    Ok(())
}
