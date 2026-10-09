//! Opt-in stationary/yaw profiling at native resolution, without image readbacks.
use bevy::{
    asset::AssetId,
    diagnostic::{DiagnosticsStore, FrameCount},
    input::{InputSystems, mouse::MouseMotion},
    prelude::*,
    render::{
        Render, RenderApp, RenderSystems,
        diagnostic::{
            DiagnosticsRecorder, RecordDiagnostics, RenderDiagnosticsPlugin, resolve_encoder,
        },
        extract_resource::{ExtractResource, ExtractResourcePlugin},
        mesh::RenderMesh,
        render_asset::RenderAssets,
        renderer::{PendingCommandBuffers, RenderDevice, RenderGraph, RenderGraphSystems},
    },
};
use forge_engine::{ForgePlayerCamera, PlayerInputFocus, PlayerSimulationReady};
use std::{
    collections::BTreeMap,
    sync::{Arc, Mutex},
    time::Instant,
};

const LENGTHS: [f64; 4] = [6.0, 10.0, 6.0, 10.0];
#[path = "asset_perf.rs"]
mod asset_perf;
#[derive(Resource, Default)]
struct Perf {
    sky_ab: bool,
    epoch: Option<Instant>,
    frame_start: Option<Instant>,
    previous_start: Option<Instant>,
    wall_ms: f64,
    phase: usize,
    phase_seconds: f64,
    yaw: f32,
    moved: usize,
    previous_pose: Option<Transform>,
    origin: Option<Vec3>,
    max_translation: f32,
    costs: BTreeMap<&'static str, f64>,
    frames: Vec<serde_json::Value>,
}
#[derive(Resource, Clone, Default, ExtractResource)]
struct Sample {
    frame: u32,
    phase: usize,
    mesh: Option<AssetId<Mesh>>,
    ocean: Option<AssetId<Mesh>>,
    tiles: Vec<AssetId<Mesh>>,
    tile_counts: [[usize; 2]; 2],
    records: Arc<Mutex<Vec<serde_json::Value>>>,
}
#[derive(Resource, Default)]
struct RenderClock {
    start: Option<Instant>,
    last: Option<Instant>,
    stages: BTreeMap<&'static str, f64>,
    graph_last: Option<Instant>,
    graph_bands: BTreeMap<&'static str, f64>,
}

pub(super) fn requested() -> bool {
    std::env::var("FORGE_TIDEWATER_CAMERA_PERF").as_deref() == Ok("1")
}
pub(super) fn install(app: &mut App) {
    app.insert_resource(bevy::winit::WinitSettings::continuous())
        .insert_resource(Perf {
            sky_ab: std::env::var("FORGE_TIDEWATER_SKY_AB").as_deref() == Ok("1"),
            ..default()
        })
        .init_resource::<Sample>()
        .add_plugins((
            RenderDiagnosticsPlugin,
            ExtractResourcePlugin::<Sample>::default(),
        ))
        .add_systems(First, begin_frame)
        .add_systems(PreUpdate, drive.before(InputSystems))
        .add_systems(
            PostUpdate,
            sky_visibility.before(bevy::camera::visibility::VisibilitySystems::VisibilityPropagate),
        )
        .add_systems(Last, finish_frame);
    asset_perf::install(app);
    app.sub_app_mut(RenderApp)
        .init_resource::<RenderClock>()
        .add_systems(
            Render,
            (
                render_begin.before(RenderSystems::ExtractCommands),
                render_mark::<0>
                    .after(RenderSystems::ExtractCommands)
                    .before(RenderSystems::PrepareAssets),
                render_mark::<1>
                    .after(RenderSystems::PrepareAssets)
                    .before(RenderSystems::PrepareMeshes),
                render_mark::<2>
                    .after(RenderSystems::PrepareMeshes)
                    .before(RenderSystems::CreateViews),
                render_mark::<3>
                    .after(RenderSystems::CreateViews)
                    .before(RenderSystems::Specialize),
                render_mark::<4>
                    .after(RenderSystems::Specialize)
                    .before(RenderSystems::PrepareViews),
                render_mark::<5>
                    .after(RenderSystems::PrepareViews)
                    .before(RenderSystems::Queue),
                render_mark::<6>
                    .after(RenderSystems::Queue)
                    .before(RenderSystems::PhaseSort),
                render_mark::<7>
                    .after(RenderSystems::PhaseSort)
                    .before(RenderSystems::Prepare),
                render_mark::<8>
                    .after(RenderSystems::Prepare)
                    .before(RenderSystems::Render),
                render_mark::<9>
                    .after(RenderSystems::Render)
                    .before(RenderSystems::Cleanup),
                render_mark::<10>
                    .after(RenderSystems::Cleanup)
                    .before(RenderSystems::PostCleanup),
                residency
                    .after(RenderSystems::QueueMeshes)
                    .before(RenderSystems::PhaseSort),
                render_end.after(RenderSystems::PostCleanup),
            ),
        )
        .add_systems(
            RenderGraph,
            (
                gpu_begin
                    .after(RenderGraphSystems::Begin)
                    .before(RenderGraphSystems::Render),
                gpu_end
                    .after(RenderGraphSystems::Render)
                    .before(resolve_encoder),
                graph_begin
                    .after(gpu_begin)
                    .before(forge_engine::tidewater_atmosphere::AtmosphereCompute),
                graph_mark::<0>
                    .after(forge_engine::tidewater_atmosphere::AtmosphereCompute)
                    .before(forge_engine::tidewater_clouds::CloudCompute),
                graph_mark::<1>
                    .after(forge_engine::tidewater_clouds::CloudCompute)
                    .before(bevy::core_pipeline::schedule::camera_driver),
                graph_mark::<2>
                    .after(bevy::core_pipeline::schedule::camera_driver)
                    .before(gpu_end),
            ),
        );
}
pub(crate) fn start(world: &World) -> Option<Instant> {
    world.contains_resource::<Perf>().then(Instant::now)
}
pub(crate) fn end(world: &mut World, started: Option<Instant>, name: &'static str) {
    if let Some(started) = started {
        world
            .resource_mut::<Perf>()
            .costs
            .insert(name, started.elapsed().as_secs_f64() * 1000.0);
    }
}
fn begin_frame(world: &mut World) {
    let now = Instant::now();
    let ready =
        world.resource::<PlayerSimulationReady>().0 && world.resource::<FrameCount>().0 >= 240;
    let mut perf = world.resource_mut::<Perf>();
    perf.wall_ms = perf
        .previous_start
        .map_or(0.0, |p| now.duration_since(p).as_secs_f64() * 1000.0);
    perf.previous_start = Some(now);
    perf.frame_start = Some(now);
    perf.costs.clear();
    if ready && perf.epoch.is_none() {
        perf.epoch = Some(now);
    }
    if let Some(epoch) = perf.epoch {
        let mut elapsed = now.duration_since(epoch).as_secs_f64();
        let mut phase = 0;
        while phase < LENGTHS.len() && elapsed >= LENGTHS[phase] {
            elapsed -= LENGTHS[phase];
            phase += 1;
        }
        perf.phase = phase;
        perf.phase_seconds = elapsed;
    }
}
#[allow(clippy::cast_possible_truncation)] // Bounded two-second input waveform.
fn drive(
    mut perf: ResMut<Perf>,
    mut focus: ResMut<PlayerInputFocus>,
    mut motion: MessageWriter<MouseMotion>,
) {
    focus.0 = true;
    let target = if perf.epoch.is_some() && perf.phase < 4 && perf.phase % 2 == 1 {
        let t = perf.phase_seconds % 2.0;
        (if t <= 1.0 { t } else { 2.0 - t }) as f32 * 540.0
    } else {
        0.0
    };
    let delta = target - perf.yaw;
    if delta != 0.0 {
        motion.write(MouseMotion {
            delta: Vec2::new(delta, 0.0),
        });
    }
    perf.yaw = target;
}
fn finish_frame(world: &mut World) {
    let transform = *world
        .query_filtered::<&Transform, With<ForgePlayerCamera>>()
        .single(world)
        .expect("profiling camera");
    let frame = world.resource::<FrameCount>().0;
    let diagnostics = world
        .resource::<DiagnosticsStore>()
        .iter()
        .filter(|d| {
            d.path().to_string().starts_with("render/")
                && (d.path().to_string().ends_with("elapsed_gpu")
                    || d.path().to_string().ends_with("elapsed_cpu"))
        })
        .filter_map(|d| {
            if d.measurement()
                .is_none_or(|m| m.time.elapsed().as_secs_f64() > 0.2)
            {
                return None;
            }
            d.value()
                .filter(|v| v.is_finite())
                .map(|v| (d.path().to_string(), v))
        })
        .collect::<BTreeMap<_, _>>();
    let atmosphere_sample = world.resource::<DiagnosticsStore>().iter()
        .find(|d| d.path().to_string() == "render/tidewater_atmosphere/elapsed_cpu")
        .and_then(|d| d.measurement().map(|m| serde_json::json!({"cpu_ms":m.value,"age_ms":m.time.elapsed().as_secs_f64()*1000.0,"sample":format!("{:?}",m.time)})));
    world.resource_scope(|world, mut perf: Mut<Perf>| {
        if perf.epoch.is_none() { return; }
        let origin = *perf.origin.get_or_insert(transform.translation);
        perf.max_translation = perf.max_translation.max(origin.distance(transform.translation));
        if perf.previous_pose.is_some_and(|old| old.rotation != transform.rotation) { perf.moved += 1; }
        perf.previous_pose = Some(transform);
        let sampled = perf.phase < 4 && perf.phase_seconds > 1.0 && perf.phase_seconds < LENGTHS[perf.phase]-1.0;
        if sampled {
            let value = serde_json::json!({"frame":frame,"phase":perf.phase,"phase_seconds":perf.phase_seconds,"wall_ms":perf.wall_ms,"main_schedule_ms":perf.frame_start.expect("frame start").elapsed().as_secs_f64()*1000.0,"costs_ms":perf.costs,"gpu_diagnostics_latest_ms":diagnostics,"atmosphere_sample":atmosphere_sample,"sky_visible":!perf.sky_ab || perf.phase<2,"fish_activity":crate::source_fish::activity(world),"refraction_activity":world.get_resource::<forge_engine::tidewater_refraction::Activity>(),"refraction_ready":world.get_resource::<forge_engine::tidewater_refraction::Observation>().map(|v|v.ready)});
            perf.frames.push(value);
        }
        let terrain = world.resource::<forge_engine::tidewater_terrain::scene::ResidentTerrain>().entity.expect("terrain");
        let mesh = world.get::<Mesh3d>(terrain).map(|m| m.0.id());
        let ocean = world.query_filtered::<&Mesh3d, With<crate::ocean_geometry::OceanSurface>>().single(world).ok().map(|m| m.0.id());
        let tiles = super::tile_entities(world).into_iter().filter_map(|entity| world.get::<Mesh3d>(entity).map(|mesh| mesh.0.id())).collect();
        let tile_counts = super::tile_counts(world);
        let mut sample = world.resource_mut::<Sample>();
        sample.frame = if sampled { frame } else { 0 }; sample.phase = perf.phase; sample.mesh = mesh; sample.ocean = ocean; sample.tiles = tiles; sample.tile_counts = tile_counts;
        if perf.phase >= 4 {
            let render = sample.records.lock().expect("render samples").clone();
            let ready = !render.is_empty() && render.iter().all(|s| s["mesh_ready"]==true && s["ocean_ready"]==true && s["all_tiles_ready"]==true);
            let window = world.query::<&Window>().single(world).expect("native window");
            let receipt = serde_json::json!({"pass":perf.moved>30 && perf.max_translation<0.05 && ready,"phase_durations_s":LENGTHS,"frames":perf.frames,"render":render,"moving_frames":perf.moved,"max_translation_m":perf.max_translation,"render_assets_ready":ready,"resolution":[window.physical_width(),window.physical_height()],"present_mode":format!("{:?}",window.present_mode),"graphics":world.resource::<super::graphics::GraphicsState>().values_receipt(),"gpu_scope":"camera_perf_gpu encloses RenderGraphSystems::Render command buffers; excludes uploads outside the graph and presentation waits; other nested spans are not additive; values arrive asynchronously","cpu_scope":"main schedule and render schedule wall times; stage timers enclose the named main-world functions; inter-frame wall intervals include pacing and synchronization","captures":0,"gate3_closed":false});
            let path = world.resource::<crate::Probe>().screenshot.with_extension("perf-proof.json");
            std::fs::write(path, serde_json::to_vec_pretty(&receipt).expect("profiling receipt")).expect("profiling output");
            world.write_message(bevy::app::AppExit::Success);
        }
    });
}
fn sky_visibility(
    perf: Res<Perf>,
    mut skies: Query<
        &mut Visibility,
        With<MeshMaterial3d<forge_engine::tidewater_sky::SkyMaterial>>,
    >,
) {
    if !perf.sky_ab {
        return;
    }
    let visibility = if perf.phase < 2 {
        Visibility::Inherited
    } else {
        Visibility::Hidden
    };
    for mut current in &mut skies {
        if *current != visibility {
            *current = visibility;
        }
    }
}
fn render_begin(mut clock: ResMut<RenderClock>) {
    let now = Instant::now();
    clock.start = Some(now);
    clock.last = Some(now);
    clock.stages.clear();
}
fn render_mark<const STAGE: usize>(mut clock: ResMut<RenderClock>) {
    const STAGES: [&str; 11] = [
        "ExtractCommands",
        "PrepareAssets",
        "PrepareMeshes",
        "CreateViews",
        "Specialize",
        "PrepareViews",
        "Queue",
        "PhaseSort",
        "Prepare",
        "Render",
        "Cleanup",
    ];
    let now = Instant::now();
    let elapsed = now
        .duration_since(clock.last.expect("render phase start"))
        .as_secs_f64()
        * 1000.0;
    clock.last = Some(now);
    clock.stages.insert(STAGES[STAGE], elapsed);
}
fn render_end(
    clock: Res<RenderClock>,
    sample: Res<Sample>,
    assets: Option<Res<asset_perf::Stats>>,
) {
    if sample.frame != 0 {
        let mut records = sample.records.lock().expect("render samples");
        if let Some(record) = records.last_mut().filter(|r| r["frame"] == sample.frame) {
            record["render_schedule_ms"] = serde_json::json!(
                clock.start.expect("render start").elapsed().as_secs_f64() * 1000.0
            );
            record["render_stages_ms"] = serde_json::json!(clock.stages);
            record["graph_bands_ms"] = serde_json::json!(clock.graph_bands);
            if let Some(assets) = assets {
                record["asset_prepare_bands_ms"] = serde_json::json!(assets.bands_ms);
                record["asset_extracted"] = serde_json::json!(assets.extracted);
            }
        }
    }
}
fn graph_begin(mut clock: ResMut<RenderClock>) {
    clock.graph_last = Some(Instant::now());
    clock.graph_bands.clear();
}
fn graph_mark<const BAND: usize>(mut clock: ResMut<RenderClock>) {
    const NAMES: [&str; 3] = ["atmosphere_band", "clouds_band", "camera_band"];
    let now = Instant::now();
    let ms = now
        .duration_since(clock.graph_last.expect("graph start"))
        .as_secs_f64()
        * 1000.0;
    clock.graph_last = Some(now);
    clock.graph_bands.insert(NAMES[BAND], ms);
}
fn residency(
    sample: Res<Sample>,
    meshes: Res<RenderAssets<RenderMesh>>,
    dirty: Res<bevy::render::camera::DirtySpecializations>,
) {
    if sample.frame != 0 {
        sample.records.lock().expect("render samples").push(serde_json::json!({"frame":sample.frame,"phase":sample.phase,"mesh":format!("{:?}",sample.mesh),"ocean":format!("{:?}",sample.ocean),"mesh_ready":sample.mesh.is_some_and(|id| meshes.get(id).is_some()),"ocean_ready":sample.ocean.is_some_and(|id| meshes.get(id).is_some()),"all_tiles_ready":sample.tiles.iter().all(|id| meshes.get(*id).is_some()),"tile_counts":sample.tile_counts,"dirty_renderables":dirty.changed_renderables.len(),"dirty_views":dirty.views.len()}));
    }
}
// Both functions are exclusive: the matching diagnostic span opens/closes on
// the render schedule's driving thread, while encoders retain queue order.
fn gpu_begin(world: &mut World) {
    let mut encoder = world
        .resource::<RenderDevice>()
        .create_command_encoder(&wgpu::CommandEncoderDescriptor::default());
    world
        .resource::<DiagnosticsRecorder>()
        .begin_time_span(&mut encoder, "camera_perf_gpu".into());
    world
        .resource_mut::<PendingCommandBuffers>()
        .push([encoder.finish()]);
}
fn gpu_end(world: &mut World) {
    let mut encoder = world
        .resource::<RenderDevice>()
        .create_command_encoder(&wgpu::CommandEncoderDescriptor::default());
    world
        .resource::<DiagnosticsRecorder>()
        .end_time_span(&mut encoder);
    world
        .resource_mut::<PendingCommandBuffers>()
        .push([encoder.finish()]);
}
