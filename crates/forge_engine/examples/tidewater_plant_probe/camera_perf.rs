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
        renderer::{
            PendingCommandBuffers, RenderAdapterInfo, RenderDevice, RenderGraph, RenderGraphSystems,
        },
    },
};
use forge_engine::{
    ForgePlayerCamera, PlayerInputFocus, PlayerSimulationReady,
    gameplay::GameplayRuntime,
    navigation::{NavigationPolicy, NavigationRuntime, NavigationSet},
    playtest_timing::{PlaytestTiming, PlaytestTimingPlugin, PlaytestTimingSnapshot, TimingState},
};
use forge_project::{
    authoring::playtest::PlaytestSnapshot, zone_authoring::session::ZoneEditSession,
};
use std::{
    collections::BTreeMap,
    sync::{Arc, Mutex},
    time::Instant,
};

const LENGTHS: [f64; 4] = [6.0, 10.0, 6.0, 10.0];
// Graceful cleanup budget below the external 600-second watchdog, not a metric.
const LIFECYCLE_DEADLINE_S: f64 = 550.0;
const MAX_LIFECYCLE_OBSERVATIONS: u32 = 128;
const ADMISSION_SECONDS: f64 = 1.0;
const ADMISSION_FRAMES: u32 = 60;
#[path = "asset_perf.rs"]
mod asset_perf;
#[path = "camera_prewarm.rs"]
mod camera_prewarm;
#[derive(Clone, Debug, PartialEq, Eq, serde::Serialize)]
struct AdmissionContext {
    source: forge_core::ContentHash,
    graphics: forge_core::ContentHash,
    viewport: [u32; 2],
    policy_hash: String,
    policy_revision: u64,
}
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
enum AdmissionState {
    Waiting,
    Stabilizing,
    Complete,
}
#[derive(Clone, Copy, PartialEq, Eq)]
struct LifecycleKey {
    timing: Option<TimingState>,
    player_ready: bool,
    gameplay_ready: Option<bool>,
    epoch_started: bool,
    admission: AdmissionState,
    admission_resets: u32,
}
#[derive(Default)]
struct AdmissionWindow {
    context: Option<AdmissionContext>,
    started: Option<Instant>,
    last_frame: Option<u32>,
    frames: u32,
    completed: bool,
    resets: u32,
}
impl AdmissionWindow {
    // Called only while timing is Idle, including the FixedUpdate observations.
    fn observe(
        &mut self,
        now: Instant,
        frame: u32,
        eligible: bool,
        context: Option<AdmissionContext>,
    ) {
        if !eligible || context.is_none() {
            if self.started.is_some() {
                self.resets = self.resets.saturating_add(1);
            }
            self.context = None;
            self.started = None;
            self.last_frame = None;
            self.frames = 0;
            self.completed = false;
            return;
        }
        if self.context != context {
            if self.started.is_some() {
                self.resets = self.resets.saturating_add(1);
            }
            self.context = context;
            self.started = Some(now);
            self.last_frame = Some(frame);
            self.frames = 1;
            self.completed = false;
            return;
        }
        if self.last_frame != Some(frame) {
            self.last_frame = Some(frame);
            self.frames = self.frames.saturating_add(1);
        }
        self.completed = self.frames >= ADMISSION_FRAMES
            && self
                .started
                .is_some_and(|start| now.duration_since(start).as_secs_f64() >= ADMISSION_SECONDS);
    }
}
#[derive(Resource, Default)]
struct Perf {
    sky_ab: bool,
    lifecycle_start: Option<Instant>,
    lifecycle_last: Option<LifecycleKey>,
    lifecycle_observations: u32,
    lifecycle_dropped: u32,
    admission: AdmissionWindow,
    prewarm: Option<camera_prewarm::Prewarm>,
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
            lifecycle_start: Some(Instant::now()),
            prewarm: camera_prewarm::Prewarm::requested(),
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

pub(super) fn install_gameplay_timing(app: &mut App) -> Result<(), String> {
    if !requested() {
        return Ok(());
    }
    let Some(runtime) = app.world().get_resource::<GameplayRuntime>() else {
        return Ok(());
    };
    let archive = app
        .world()
        .resource::<crate::Probe>()
        .authored_archive
        .as_deref()
        .ok_or("Native gameplay timing requires its authored archive")?;
    let zone = ZoneEditSession::from_archive_bytes(archive)?;
    let snapshot = PlaytestSnapshot::from_zone(zone.head(), runtime.session().source_hash())?;
    if snapshot.gameplay() != Some(runtime.session().document()) {
        return Err("Native timing document differs from the installed gameplay runtime".into());
    }
    if app.world().contains_resource::<PlaytestTiming>()
        || app.is_plugin_added::<PlaytestTimingPlugin>()
    {
        return Err("Native timing must not replace another capture".into());
    }
    app.insert_resource(PlaytestTiming::new(
        snapshot.identity(),
        u64::from(std::process::id()),
    ))
    .add_plugins(PlaytestTimingPlugin)
    .add_systems(
        FixedUpdate,
        collect_gameplay_timing.after(NavigationSet::Refresh),
    );
    Ok(())
}

fn collect_gameplay_timing(world: &mut World) {
    update_gameplay_timing(world);
}

fn update_gameplay_timing(world: &mut World) -> bool {
    if !world.contains_resource::<PlaytestTiming>() {
        return true;
    }
    let runtime = world.resource::<GameplayRuntime>();
    let (source, tick, ready) = (
        runtime.session().source_hash(),
        runtime.session().state().tick,
        runtime.is_ready(),
    );
    let graphics = serde_json::to_vec(
        &world
            .resource::<super::graphics::GraphicsState>()
            .values_receipt(),
    )
    .map(|bytes| forge_core::bytes_sha256(&bytes));
    let viewport = world
        .query::<&Window>()
        .single(world)
        .map(|window| [window.physical_width(), window.physical_height()]);
    let policy = world.resource::<NavigationPolicy>();
    let (policy_hash, policy_revision) = (policy.source_hash().to_owned(), policy.revision());
    let context_available = graphics.is_ok() && viewport.is_ok();
    let admission_context =
        graphics
            .as_ref()
            .ok()
            .zip(viewport.as_ref().ok())
            .map(|(graphics, viewport)| AdmissionContext {
                source,
                graphics: *graphics,
                viewport: *viewport,
                policy_hash: policy_hash.clone(),
                policy_revision,
            });
    if world.resource::<PlaytestTiming>().state() == TimingState::Idle {
        let frame = world.resource::<FrameCount>().0;
        let preparation_complete = {
            let mut perf = world.resource_mut::<Perf>();
            perf.prewarm.as_mut().is_none_or(|prewarm| {
                prewarm.observe_context(admission_context.as_ref());
                prewarm.allows_admission()
            })
        };
        let eligible = ready
            && world.resource::<PlayerSimulationReady>().0
            && frame >= 240
            && preparation_complete;
        world.resource_mut::<Perf>().admission.observe(
            Instant::now(),
            frame,
            eligible,
            admission_context,
        );
    }
    world.resource_scope(|world, mut timing: Mut<PlaytestTiming>| {
        match (graphics, viewport) {
            (Ok(graphics), Ok(viewport)) => {
                timing.context(source, graphics, viewport, &policy_hash, policy_revision);
            }
            _ => timing.invalidate_if_active("native graphics or physical viewport unavailable"),
        }
        if !ready {
            timing.invalidate_if_active("native gameplay readiness lost");
        }
        let mut navigation = world.resource_mut::<NavigationRuntime>();
        timing.absorb_navigation(
            navigation.take_timing_samples(),
            navigation.take_timing_jobs(),
        );
        timing.note_tick(tick);
        navigation.set_timing_capture(timing.capture());
    });
    observe_lifecycle(world);
    ready && context_available
}

fn begin_gameplay_capture(world: &mut World, ready: bool) -> bool {
    if !world.contains_resource::<PlaytestTiming>() {
        return world.resource::<Perf>().prewarm.is_none();
    }
    let context_ready = update_gameplay_timing(world);
    if ready
        && context_ready
        && world.resource::<Perf>().admission.completed
        && world.resource::<PlaytestTiming>().state() == TimingState::Idle
        && camera_prewarm::record_before_capture(world)
    {
        world.resource_mut::<PlaytestTiming>().start();
        observe_lifecycle(world);
    }
    let capture = world.resource::<PlaytestTiming>().capture();
    world
        .resource_mut::<NavigationRuntime>()
        .set_timing_capture(capture);
    world.resource::<PlaytestTiming>().state() == TimingState::Recording
}

fn finish_gameplay_capture(world: &mut World) -> Option<PlaytestTimingSnapshot> {
    if !world.contains_resource::<PlaytestTiming>() {
        return None;
    }
    update_gameplay_timing(world);
    world.resource_mut::<PlaytestTiming>().stop();
    world
        .resource_mut::<NavigationRuntime>()
        .set_timing_capture(None);
    Some(world.resource::<PlaytestTiming>().snapshot())
}

fn begin_frame(world: &mut World) {
    let now = Instant::now();
    let ready =
        world.resource::<PlayerSimulationReady>().0 && world.resource::<FrameCount>().0 >= 240;
    let gameplay_ready = begin_gameplay_capture(world, ready);
    {
        let mut perf = world.resource_mut::<Perf>();
        perf.wall_ms = perf
            .previous_start
            .map_or(0.0, |p| now.duration_since(p).as_secs_f64() * 1000.0);
        perf.previous_start = Some(now);
        perf.frame_start = Some(now);
        perf.costs.clear();
        if ready && gameplay_ready && perf.epoch.is_none() {
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
    observe_lifecycle(world);
}
#[allow(clippy::cast_possible_truncation)] // Bounded two-second input waveform.
fn drive(
    mut perf: ResMut<Perf>,
    mut focus: ResMut<PlayerInputFocus>,
    mut motion: MessageWriter<MouseMotion>,
) {
    focus.0 = true;
    if perf.epoch.is_none()
        && let Some(prewarm) = &mut perf.prewarm
    {
        let delta = prewarm.take_motion();
        if delta != 0.0 {
            motion.write(MouseMotion {
                delta: Vec2::new(delta, 0.0),
            });
        }
        return;
    }
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

// Observe transitions only. No readiness is forced and no capture is restarted.
fn observe_lifecycle(world: &mut World) {
    let state = world
        .get_resource::<PlaytestTiming>()
        .map(PlaytestTiming::state);
    let player_ready = world.resource::<PlayerSimulationReady>().0;
    let gameplay_ready = world
        .get_resource::<GameplayRuntime>()
        .map(GameplayRuntime::is_ready);
    let perf = world.resource::<Perf>();
    let epoch_started = perf.epoch.is_some();
    let admission_active = perf.admission.started.is_some();
    let admission_completed = perf.admission.completed;
    let admission_resets = perf.admission.resets;
    let key = LifecycleKey {
        timing: state,
        player_ready,
        gameplay_ready,
        epoch_started,
        admission: if admission_completed {
            AdmissionState::Complete
        } else if admission_active {
            AdmissionState::Stabilizing
        } else {
            AdmissionState::Waiting
        },
        admission_resets,
    };
    {
        let mut perf = world.resource_mut::<Perf>();
        if perf.lifecycle_last == Some(key) {
            return;
        }
        perf.lifecycle_last = Some(key);
        if perf.lifecycle_observations >= MAX_LIFECYCLE_OBSERVATIONS {
            perf.lifecycle_dropped = perf.lifecycle_dropped.saturating_add(1);
            return;
        }
        perf.lifecycle_observations += 1;
    }
    let timing = world
        .get_resource::<PlaytestTiming>()
        .map(PlaytestTiming::snapshot);
    let tick = world
        .get_resource::<GameplayRuntime>()
        .map(|runtime| runtime.session().state().tick);
    let frame = world.resource::<FrameCount>().0;
    let admission = &world.resource::<Perf>().admission;
    debug!(target: "forge::gameplay", ?state, frame, ?tick, player_ready, ?gameplay_ready,
        epoch_started, admission_active, admission_completed, admission_resets,
        admission_frames = admission.frames,
        admission_elapsed_s = ?admission.started.map(|start| start.elapsed().as_secs_f64()),
        admission_context = ?admission.context,
        warmup_remaining = ?timing.as_ref().map(|value| value.warmup_frames_remaining),
        limit = ?timing.as_ref().and_then(|value| value.limit.as_deref()),
        "native camera profiler lifecycle changed");
}

fn lifecycle_failure(state: Option<TimingState>, elapsed_s: f64) -> Option<&'static str> {
    match state {
        Some(TimingState::Invalidated) => Some("gameplay timing capture invalidated"),
        Some(TimingState::LimitReached) => {
            Some("gameplay timing capacity reached before profiler completion")
        }
        Some(TimingState::Stopped) => Some("gameplay timing stopped before profiler completion"),
        _ if elapsed_s >= LIFECYCLE_DEADLINE_S => {
            Some("native camera profiler lifecycle deadline exceeded")
        }
        _ => None,
    }
}

fn close_failed_lifecycle(world: &mut World) -> bool {
    let state = world
        .get_resource::<PlaytestTiming>()
        .map(PlaytestTiming::state);
    let perf = world.resource::<Perf>();
    let elapsed_s = perf
        .lifecycle_start
        .map_or(0.0, |start| start.elapsed().as_secs_f64());
    let Some(reason) = lifecycle_failure(state, elapsed_s) else {
        return false;
    };
    let receipt = serde_json::json!({
        "schema": "forge.camera-perf-lifecycle-failure.v1",
        "pass": false, "metric_qualified": false, "gate_qualified": false,
        "failure": reason, "process_id": std::process::id(),
        "elapsed_since_install_s": elapsed_s, "native_deadline_s": LIFECYCLE_DEADLINE_S,
        "frame": world.resource::<FrameCount>().0,
        "player_ready": world.resource::<PlayerSimulationReady>().0,
        "gameplay_ready": world.get_resource::<GameplayRuntime>().map(GameplayRuntime::is_ready),
        "epoch_started": perf.epoch.is_some(), "phase": perf.phase,
        "camera_samples_observed": perf.frames.len(),
        "lifecycle_transitions_logged": perf.lifecycle_observations,
        "lifecycle_transitions_dropped": perf.lifecycle_dropped,
        "admission": {
            "completed": perf.admission.completed, "frames": perf.admission.frames,
            "elapsed_s": perf.admission.started.map(|start| start.elapsed().as_secs_f64()),
            "resets": perf.admission.resets, "context": perf.admission.context,
            "required_seconds": ADMISSION_SECONDS, "required_frames": ADMISSION_FRAMES
        },
        "rpg_timing": world.get_resource::<PlaytestTiming>().map(PlaytestTiming::snapshot),
        "limits": "Diagnostic failure; partial observations are not a completed measurement; no readiness override or capture restart"
    });
    let path = world
        .resource::<crate::Probe>()
        .screenshot
        .with_extension("perf-lifecycle.json");
    let saved = (|| -> Result<(), String> {
        use std::io::Write;
        let bytes = serde_json::to_vec_pretty(&receipt).map_err(|error| error.to_string())?;
        let mut file = std::fs::OpenOptions::new()
            .write(true)
            .create_new(true)
            .open(&path)
            .map_err(|error| error.to_string())?;
        file.write_all(&bytes).map_err(|error| error.to_string())
    })();
    match saved {
        Ok(()) => {
            error!(target: "forge::gameplay", reason, ?path, "native camera profiler failed; diagnostic retained");
        }
        Err(error) => {
            error!(target: "forge::gameplay", reason, ?path, error, "native camera profiler failed; diagnostic write failed");
        }
    }
    if let Some(mut navigation) = world.get_resource_mut::<NavigationRuntime>() {
        navigation.set_timing_capture(None);
    }
    world.write_message(bevy::app::AppExit::error());
    true
}

fn finish_frame(world: &mut World) {
    camera_prewarm::observe(world);
    if camera_prewarm::close_failed(world) || close_failed_lifecycle(world) {
        return;
    }
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
    // Completion updates the lifecycle observer, which requires Perf in the World.
    // resource_scope temporarily removes Perf, so finish timing before entering it.
    let finishing = {
        let perf = world.resource::<Perf>();
        perf.epoch.is_some() && perf.phase >= 4
    };
    let gameplay_timing = finishing.then(|| finish_gameplay_capture(world)).flatten();
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
            let gpu_adapter = world.get_resource::<RenderAdapterInfo>().map(|adapter| {
                serde_json::json!({
                    "name":adapter.name.clone(),"vendor":adapter.vendor,"device":adapter.device,
                    "device_type":format!("{:?}",adapter.device_type),"driver":adapter.driver.clone(),
                    "driver_info":adapter.driver_info.clone(),"backend":format!("{:?}",adapter.backend)
                })
            });
            let window = world.query::<&Window>().single(world).expect("native window");
            let receipt = serde_json::json!({"pass":perf.moved>30 && perf.max_translation<0.05 && ready,"phase_durations_s":LENGTHS,"rpg_timing":gameplay_timing,"gpu_adapter":gpu_adapter,"frames":perf.frames,"render":render,"moving_frames":perf.moved,"max_translation_m":perf.max_translation,"render_assets_ready":ready,"resolution":[window.physical_width(),window.physical_height()],"present_mode":format!("{:?}",window.present_mode),"graphics":world.resource::<super::graphics::GraphicsState>().values_receipt(),"gpu_scope":"camera_perf_gpu encloses RenderGraphSystems::Render command buffers; excludes uploads outside the graph and presentation waits; other nested spans are not additive; values arrive asynchronously","cpu_scope":"main schedule and render schedule wall times; stage timers enclose the named main-world functions; inter-frame wall intervals include pacing and synchronization","captures":0,"gate3_closed":false});
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

#[cfg(test)]
mod lifecycle_tests {
    use super::*;
    use std::time::Duration;

    fn context() -> AdmissionContext {
        AdmissionContext {
            source: forge_core::bytes_sha256(b"lifecycle test source"),
            graphics: forge_core::bytes_sha256(b"lifecycle test graphics"),
            viewport: [1920, 1080],
            policy_hash: "lifecycle test policy".into(),
            policy_revision: 1,
        }
    }

    #[test]
    fn admission_needs_one_second_and_sixty_distinct_frames() {
        let start = Instant::now();
        let mut frames_first = AdmissionWindow::default();
        for frame in 240..300 {
            let elapsed = Duration::from_millis(u64::from(frame - 240) * 10);
            frames_first.observe(start + elapsed, frame, true, Some(context()));
        }
        assert_eq!(frames_first.frames, 60);
        assert!(!frames_first.completed);
        frames_first.observe(
            start + Duration::from_millis(999),
            299,
            true,
            Some(context()),
        );
        assert!(!frames_first.completed);
        frames_first.observe(start + Duration::from_secs(1), 299, true, Some(context()));
        assert!(frames_first.completed);
        assert_eq!(frames_first.frames, 60);

        let mut time_first = AdmissionWindow::default();
        time_first.observe(start, 240, true, Some(context()));
        for _ in 0..120 {
            time_first.observe(start + Duration::from_secs(2), 240, true, Some(context()));
        }
        assert_eq!(time_first.frames, 1);
        assert!(!time_first.completed);
        for frame in 241..300 {
            time_first.observe(start + Duration::from_secs(2), frame, true, Some(context()));
        }
        assert!(time_first.completed);
        assert_eq!(time_first.frames, 60);
    }

    #[test]
    fn pre_capture_readiness_loss_requires_a_new_complete_admission() {
        let start = Instant::now();
        let mut admission = AdmissionWindow::default();
        for frame in 240..300 {
            admission.observe(start, frame, true, Some(context()));
        }
        // A loss may be observed in FixedUpdate without a new loop frame.
        admission.observe(
            start + Duration::from_millis(500),
            299,
            false,
            Some(context()),
        );
        assert_eq!(admission.frames, 0);
        assert_eq!(admission.resets, 1);
        assert!(admission.started.is_none());
        assert!(admission.context.is_none());
        assert!(!admission.completed);

        let resumed = start + Duration::from_millis(600);
        for frame in 300..360 {
            admission.observe(resumed, frame, true, Some(context()));
        }
        admission.observe(
            resumed + Duration::from_millis(999),
            359,
            true,
            Some(context()),
        );
        assert!(!admission.completed);
        admission.observe(resumed + Duration::from_secs(1), 359, true, Some(context()));
        assert!(admission.completed);
        assert_eq!(admission.resets, 1);
    }

    #[test]
    fn source_graphics_viewport_and_policy_changes_reset_pre_capture_admission() {
        let start = Instant::now();
        let original = context();
        let changed_contexts = [
            AdmissionContext {
                source: forge_core::bytes_sha256(b"other test source"),
                ..original.clone()
            },
            AdmissionContext {
                graphics: forge_core::bytes_sha256(b"other test graphics"),
                ..original.clone()
            },
            AdmissionContext {
                viewport: [1280, 720],
                ..original.clone()
            },
            AdmissionContext {
                policy_hash: "other test policy".into(),
                ..original.clone()
            },
            AdmissionContext {
                policy_revision: 2,
                ..original.clone()
            },
        ];
        for changed in changed_contexts {
            let mut admission = AdmissionWindow::default();
            for frame in 240..300 {
                admission.observe(start, frame, true, Some(original.clone()));
            }
            let resumed = start + Duration::from_millis(750);
            admission.observe(resumed, 300, true, Some(changed.clone()));
            assert_eq!(admission.frames, 1);
            assert_eq!(admission.resets, 1);
            assert_eq!(admission.started, Some(resumed));
            assert_eq!(admission.context, Some(changed.clone()));
            assert!(!admission.completed);
            for frame in 301..360 {
                admission.observe(resumed, frame, true, Some(changed.clone()));
            }
            admission.observe(
                resumed + Duration::from_millis(999),
                359,
                true,
                Some(changed.clone()),
            );
            assert!(!admission.completed);
            admission.observe(resumed + Duration::from_secs(1), 359, true, Some(changed));
            assert!(admission.completed);
        }
    }

    #[test]
    fn post_start_invalidation_remains_terminal_when_same_context_returns() {
        let context = context();
        let identity = forge_project::authoring::playtest::PlaytestIdentity {
            source_hash: context.source,
            terrain_hash: forge_core::bytes_sha256(b"lifecycle test terrain"),
            vegetation_hash: None,
        };
        let mut timing = PlaytestTiming::new(identity, 7);
        timing.context(
            context.source,
            context.graphics,
            context.viewport,
            &context.policy_hash,
            context.policy_revision,
        );
        timing.start();
        assert_eq!(timing.state(), TimingState::Warmup);
        assert_eq!(timing.snapshot().warmup_frames_remaining, 60);
        timing.invalidate_if_active("native gameplay readiness lost");
        assert_eq!(timing.state(), TimingState::Invalidated);

        // The real public timing API does not recover an invalidated capture.
        timing.context(
            context.source,
            context.graphics,
            context.viewport,
            &context.policy_hash,
            context.policy_revision,
        );
        timing.stop();
        let snapshot = timing.snapshot();
        assert_eq!(snapshot.state, TimingState::Invalidated);
        assert_eq!(
            snapshot.limit.as_deref(),
            Some("native gameplay readiness lost")
        );
        assert_eq!(snapshot.capture, 1);
        assert_eq!(snapshot.warmup_frames_remaining, 60);
        assert!(!timing.is_recording());
        assert_eq!(timing.capture(), None);
        assert_eq!(
            lifecycle_failure(Some(timing.state()), 0.0),
            Some("gameplay timing capture invalidated")
        );
        assert_eq!(
            lifecycle_failure(Some(timing.state()), 549.0),
            Some("gameplay timing capture invalidated")
        );
    }

    #[test]
    fn unfinished_capture_has_a_deadline_and_terminal_errors_take_priority() {
        for state in [
            None,
            Some(TimingState::Idle),
            Some(TimingState::Warmup),
            Some(TimingState::Recording),
        ] {
            assert_eq!(lifecycle_failure(state, 549.999), None);
            assert_eq!(
                lifecycle_failure(state, 550.0),
                Some("native camera profiler lifecycle deadline exceeded")
            );
        }
        assert_eq!(
            lifecycle_failure(Some(TimingState::Invalidated), 550.0),
            Some("gameplay timing capture invalidated")
        );
    }
}
