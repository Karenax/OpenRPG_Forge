//! Shared observational clocks extracted from the reference performance walk.
//! No inputs, readiness, authored state or presentation mode are changed.
//! CPU bands and asynchronous GPU diagnostics overlap; they are not additive.
use crate::cpu_timing;
use bevy::{
    app::MainScheduleOrder,
    camera::{NormalizedRenderTarget, visibility::CascadesVisibleEntities},
    core_pipeline::{
        core_3d::main_opaque_pass_3d,
        mip_generation::experimental::depth::early_downsample_depth,
        schedule::{Core3d, Core3dSystems},
    },
    diagnostic::{DiagnosticsStore, FrameCount},
    ecs::schedule::{ScheduleLabel, SingleThreadedExecutor},
    pbr::{
        EARLY_SHADOW_PASS, LightEntity, ShadowView, early_prepass_build_indirect_parameters,
        shared_shadow_pass,
    },
    platform::time::Instant,
    prelude::*,
    render::{
        Extract, ExtractSchedule, Render, RenderApp, RenderSystems,
        batching::gpu_preprocessing::BinUnpackingBuffers,
        camera::ExtractedCamera,
        diagnostic::{
            DiagnosticsRecorder, RecordDiagnostics, RenderDiagnosticsPlugin, resolve_encoder,
        },
        renderer::{
            CurrentView, PendingCommandBuffers, RenderContext, RenderDevice, RenderGraph,
            RenderGraphSystems, ViewQuery, render_system,
        },
        view::{ExtractedView, ExtractedWindows, prepare_windows},
    },
};
use std::{any::TypeId, collections::BTreeMap};

#[derive(SystemSet, Clone, Debug, PartialEq, Eq, Hash)]
pub enum TimingSet {
    MainBegin,
    MainEnd,
    RenderEnd,
    GpuBegin,
    GpuEnd,
}

#[derive(ScheduleLabel, Clone, Debug, PartialEq, Eq, Hash)]
struct TimingComplete;

pub(crate) fn ensure_diagnostics(app: &mut App) {
    if !app.is_plugin_added::<RenderDiagnosticsPlugin>() {
        app.add_plugins(RenderDiagnosticsPlugin);
    }
}

#[derive(Resource, Default)]
pub struct MainClock {
    start: Option<Instant>,
    previous: Option<Instant>,
    wall_ms: Option<f64>,
    span: Option<cpu_timing::Span>,
    gpu_logged: Option<Instant>,
}
impl MainClock {
    #[must_use]
    pub fn elapsed_ms(&self) -> Option<f64> {
        self.start
            .map(|start| start.elapsed().as_secs_f64() * 1000.0)
    }
    #[must_use]
    pub fn wall_ms(&self) -> Option<f64> {
        self.wall_ms
    }
}
#[derive(Resource, Default)]
struct FixedClock(Option<cpu_timing::Span>);

#[derive(Clone, Debug, serde::Serialize)]
pub struct RenderTiming {
    pub schedule_ms: Option<f64>,
    pub stages_ms: BTreeMap<&'static str, f64>,
    pub graph_bands_ms: BTreeMap<&'static str, f64>,
}
#[derive(Resource, Default)]
pub struct RenderClock {
    start: Option<Instant>,
    last: Option<Instant>,
    elapsed_ms: Option<f64>,
    stages: BTreeMap<&'static str, f64>,
    graph_last: Option<Instant>,
    graph_bands: BTreeMap<&'static str, f64>,
    schedule_span: Option<cpu_timing::Span>,
    stage_span: Option<cpu_timing::Span>,
    prepare_span: Option<cpu_timing::Span>,
    window_prepare_span: Option<cpu_timing::Span>,
}
#[derive(Resource, Default)]
struct ExtractedFrame(Option<u32>);

#[derive(Resource, Default)]
struct PrimaryPresentTrace(Option<(Entity, bool)>);

impl RenderClock {
    #[must_use]
    pub fn snapshot(&self) -> RenderTiming {
        RenderTiming {
            schedule_ms: self.elapsed_ms,
            stages_ms: self.stages.clone(),
            graph_bands_ms: self.graph_bands.clone(),
        }
    }
    fn begin_graph(&mut self) {
        self.graph_last = Some(Instant::now());
        self.graph_bands.clear();
    }
    fn mark_graph(&mut self, name: &'static str) {
        let now = Instant::now();
        if let Some(last) = self.graph_last.replace(now) {
            self.graph_bands
                .insert(name, now.duration_since(last).as_secs_f64() * 1000.0);
        }
    }
}

pub struct RenderTimingPlugin;
impl Plugin for RenderTimingPlugin {
    fn build(&self, app: &mut App) {
        ensure_diagnostics(app);
        app.init_schedule(TimingComplete);
        app.world_mut()
            .resource_mut::<MainScheduleOrder>()
            .insert_after(Last, TimingComplete);
        app.init_resource::<FrameCount>()
            .init_resource::<DiagnosticsStore>()
            .init_resource::<MainClock>()
            .init_resource::<FixedClock>()
            .add_systems(First, main_begin.in_set(TimingSet::MainBegin))
            .add_systems(TimingComplete, log_directional_shadow_candidates)
            .add_systems(
                TimingComplete,
                (
                    main_end.in_set(TimingSet::MainEnd),
                    gpu_latest.after(TimingSet::MainEnd),
                ),
            )
            .add_systems(FixedFirst, fixed_begin)
            .add_systems(FixedLast, fixed_end);
        if cpu_timing::enabled() {
            cpu_timing::main_phases::install(app);
        }
        let Some(render) = app.get_sub_app_mut(RenderApp) else {
            return;
        };
        render
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
                    render_end
                        .after(RenderSystems::PostCleanup)
                        .in_set(TimingSet::RenderEnd),
                ),
            )
            .add_systems(
                RenderGraph,
                (
                    gpu_begin
                        .after(RenderGraphSystems::Begin)
                        .before(RenderGraphSystems::Render)
                        .in_set(TimingSet::GpuBegin),
                    graph_begin.before(crate::source_atmosphere::AtmosphereCompute),
                    graph_mark::<0>
                        .after(crate::source_atmosphere::AtmosphereCompute)
                        .before(crate::source_clouds::CloudCompute),
                    graph_mark::<1>
                        .after(crate::source_clouds::CloudCompute)
                        .before(bevy::core_pipeline::schedule::camera_driver),
                    graph_mark::<2>.after(bevy::core_pipeline::schedule::camera_driver),
                    gpu_end
                        .after(RenderGraphSystems::Render)
                        .before(resolve_encoder)
                        .in_set(TimingSet::GpuEnd),
                    graph_mark::<3>
                        .after(resolve_encoder)
                        .before(RenderGraphSystems::Submit),
                    graph_mark::<4>.after(RenderGraphSystems::Finish),
                )
                    .chain(),
            );
        if cpu_timing::enabled() {
            install_render_profiling(render);
        }
    }
}

fn install_render_profiling(render: &mut bevy::app::SubApp) {
    install_frame_breakdown(render);
    if std::env::var_os("FORGE_RUNTIME_GPU_VIEW_PROFILE").as_deref() != Some("1".as_ref()) {
        return;
    }
    // Bevy associates nested spans on the same thread. Keep this executor
    // override confined to opt-in GPU attribution runs.
    render.edit_schedule(Core3d, |schedule| {
        schedule.set_executor(SingleThreadedExecutor::new());
    });
    render.add_systems(
        Core3d,
        (
            opaque_view_begin.before(main_opaque_pass_3d),
            opaque_view_end
                .after(main_opaque_pass_3d)
                .before(bevy::pbr::main_transmissive_pass_3d),
        )
            .in_set(Core3dSystems::MainPass),
    );
    render.add_systems(
        Core3d,
        (
            point_shadow_begin
                .after(early_prepass_build_indirect_parameters)
                .before(shared_shadow_pass::<EARLY_SHADOW_PASS>),
            point_shadow_end
                .after(shared_shadow_pass::<EARLY_SHADOW_PASS>)
                .before(early_downsample_depth),
        ),
    );
}

fn install_frame_breakdown(render: &mut bevy::app::SubApp) {
    render
        .init_resource::<ExtractedFrame>()
        .init_resource::<PrimaryPresentTrace>()
        .add_systems(ExtractSchedule, extract_frame)
        .add_systems(Render, log_render_frame.after(TimingSet::RenderEnd));
    render.add_systems(
        Render,
        (
            prepare_mark::<0>
                .after(render_mark::<7>)
                .before(RenderSystems::PrepareResources),
            prepare_mark::<1>
                .after(RenderSystems::PrepareResources)
                .before(RenderSystems::PrepareResourcesBatchPhases),
            prepare_mark::<2>
                .after(RenderSystems::PrepareResourcesCollectPhaseBuffers)
                .before(RenderSystems::PrepareResourcesFlush),
            prepare_mark::<3>
                .after(RenderSystems::PrepareResourcesFlush)
                .before(RenderSystems::PrepareBindGroups),
            prepare_end
                .after(RenderSystems::PrepareBindGroups)
                .before(render_mark::<8>),
            window_prepare_begin
                .after(render_mark::<4>)
                .before(prepare_windows),
            window_prepare_end
                .after(prepare_windows)
                .before(render_mark::<5>),
            observe_primary_texture
                .after(render_mark::<8>)
                .before(render_system),
            trace_primary_present
                .after(render_system)
                .before(render_mark::<9>),
            log_mesh_bins.after(render_end),
        ),
    );
}
fn main_begin(mut clock: ResMut<MainClock>) {
    let now = Instant::now();
    clock.wall_ms = clock
        .previous
        .replace(now)
        .map(|last| now.duration_since(last).as_secs_f64() * 1000.0);
    clock.start = Some(now);
    clock.span = Some(cpu_timing::Span::new("app.main_schedule"));
}
#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn main_end(
    mut clock: ResMut<MainClock>,
    frame: Res<FrameCount>,
    phases: Option<Res<cpu_timing::main_phases::CpuPhases>>,
) {
    clock.span.take();
    if let Some(interval_ms) = clock.wall_ms
        && interval_ms > 25.0
        && let Some(phases) = phases.and_then(|phases| phases.latest)
    {
        let [outside_ms, pre_update_ms, fixed_ms, update_ms, post_update_ms] = phases;
        bevy::log::debug!(
            target: "forge::frame_cpu",
            event_kind = "slow_main_frame",
            frame = frame.0,
            interval_ms,
            outside_ms,
            pre_update_ms,
            fixed_ms,
            update_ms,
            post_update_ms,
            "Slow main frame across the existing schedule boundaries"
        );
    }
}
fn fixed_begin(mut clock: ResMut<FixedClock>) {
    clock.0 = Some(cpu_timing::Span::new("app.fixed_schedule"));
}
fn fixed_end(mut clock: ResMut<FixedClock>) {
    clock.0.take();
}
const STAGES: [&str; 12] = [
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
    "PostCleanup",
];
const LABELS: [&str; 12] = [
    "render.ExtractCommands",
    "render.PrepareAssets",
    "render.PrepareMeshes",
    "render.CreateViews",
    "render.Specialize",
    "render.PrepareViews",
    "render.Queue",
    "render.PhaseSort",
    "render.Prepare",
    "render.Render",
    "render.Cleanup",
    "render.PostCleanup",
];
fn render_begin(mut clock: ResMut<RenderClock>) {
    let now = Instant::now();
    clock.start = Some(now);
    clock.last = Some(now);
    clock.elapsed_ms = None;
    clock.stages.clear();
    clock.graph_last = None;
    clock.graph_bands.clear();
    clock.schedule_span = Some(cpu_timing::Span::new("render.schedule"));
    clock.stage_span = Some(cpu_timing::Span::new(LABELS[0]));
}
fn render_mark<const STAGE: usize>(mut clock: ResMut<RenderClock>) {
    if STAGE == 9 && clock.graph_last.is_some() {
        record_graph_band(&mut clock, 5);
        clock.graph_last = None;
    }
    let now = Instant::now();
    if let Some(last) = clock.last.replace(now) {
        clock.stages.insert(
            STAGES[STAGE],
            now.duration_since(last).as_secs_f64() * 1000.0,
        );
    }
    clock.stage_span.take();
    clock.stage_span = Some(cpu_timing::Span::new(LABELS[STAGE + 1]));
}

const PREPARE_LABELS: [&str; 4] = [
    "render.prepare.resources",
    "render.prepare.batch_and_collect",
    "render.prepare.flush",
    "render.prepare.bind_groups",
];

fn prepare_mark<const BAND: usize>(mut clock: ResMut<RenderClock>) {
    clock.prepare_span.take();
    clock.prepare_span = Some(cpu_timing::Span::new(PREPARE_LABELS[BAND]));
}

fn prepare_end(mut clock: ResMut<RenderClock>) {
    clock.prepare_span.take();
}

// Bracket Bevy's window preparation, including texture acquisition. Other
// parallel work in PrepareViews may overlap this elapsed interval.
fn window_prepare_begin(mut clock: ResMut<RenderClock>) {
    clock.window_prepare_span = Some(cpu_timing::Span::new("render.prepare_views.windows"));
}

fn window_prepare_end(mut clock: ResMut<RenderClock>) {
    clock.window_prepare_span.take();
}

fn mesh_bin_phase(phase: TypeId) -> &'static str {
    use bevy::core_pipeline::{
        core_3d::{AlphaMask3d, Opaque3d},
        prepass::{AlphaMask3dPrepass, Opaque3dPrepass},
    };
    if phase == TypeId::of::<bevy::pbr::Shadow>() {
        "shadow"
    } else if phase == TypeId::of::<Opaque3d>() || phase == TypeId::of::<AlphaMask3d>() {
        "forward"
    } else if phase == TypeId::of::<Opaque3dPrepass>()
        || phase == TypeId::of::<AlphaMask3dPrepass>()
    {
        "prepass"
    } else {
        "other"
    }
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::type_complexity,
    reason = "Bevy supplies the resources and material classification query as system parameters."
)]
fn log_directional_shadow_candidates(
    frame: Res<FrameCount>,
    lights: Query<(Entity, &CascadesVisibleEntities)>,
    casters: Query<(
        Option<&MeshMaterial3d<crate::source_vegetation::world::WorldPlantMaterial>>,
        Has<MeshMaterial3d<crate::source_lighting::surfaces::ModelMaterial>>,
        Has<MeshMaterial3d<crate::source_lighting::surfaces::TerrainMaterial>>,
        Has<MeshMaterial3d<crate::source_lighting::surfaces::VegetationMaterial>>,
    )>,
    plant_materials: Option<Res<Assets<crate::source_vegetation::world::WorldPlantMaterial>>>,
    mut last: Local<Option<Instant>>,
) {
    if last.is_some_and(|time| time.elapsed().as_secs_f64() < 5.0) {
        return;
    }
    *last = Some(Instant::now());
    for (light, visible) in &lights {
        for (camera, cascades) in &visible.entities {
            for (cascade, visible) in cascades.iter().enumerate() {
                let mut source_plants = 0;
                let mut other_world_plants = 0;
                let mut source_surfaces = 0;
                for &entity in &visible.entities {
                    let Ok((plant, model, terrain, vegetation)) = casters.get(entity) else {
                        continue;
                    };
                    if let Some(plant) = plant {
                        if plant_materials
                            .as_ref()
                            .and_then(|materials| materials.get(&plant.0))
                            .is_some_and(|material| material.source_lighting)
                        {
                            source_plants += 1;
                        } else {
                            other_world_plants += 1;
                        }
                    } else if model || terrain || vegetation {
                        source_surfaces += 1;
                    }
                }
                let candidates = visible.entities.len();
                info!(
                    event_kind = "runtime_directional_shadow_candidates",
                    frame = frame.0,
                    ?light,
                    ?camera,
                    cascade,
                    candidates,
                    source_plants,
                    other_world_plants,
                    source_surfaces,
                    other = candidates - source_plants - other_world_plants - source_surfaces,
                    "CPU-visible directional shadow candidates before render preparation"
                );
            }
        }
    }
}

fn log_mesh_bins(
    frame: Res<ExtractedFrame>,
    bins: Option<Res<BinUnpackingBuffers>>,
    shadow_views: Query<(&ExtractedView, &bevy::pbr::LightEntity)>,
    camera_views: Query<(
        Entity,
        &ExtractedView,
        &ExtractedCamera,
        Has<crate::source_refraction::CaptureCamera>,
    )>,
    mut last: Local<Option<Instant>>,
) {
    let Some(bins) = bins else { return };
    if last.is_some_and(|time| time.elapsed().as_secs_f64() < 5.0) {
        return;
    }
    *last = Some(Instant::now());
    let mut phases = BTreeMap::new();
    let shadow_kinds: bevy::platform::collections::HashMap<_, _> = shadow_views
        .iter()
        .map(|(view, light)| {
            use bevy::pbr::LightEntity;
            let group = match light {
                LightEntity::Directional { cascade_index, .. } => ("directional", *cascade_index),
                LightEntity::Point { face_index, .. } => ("point", *face_index),
                LightEntity::Spot { .. } => ("spot", 0),
            };
            (view.retained_view_entity, group)
        })
        .collect();
    let camera_kinds: bevy::platform::collections::HashMap<_, _> = camera_views
        .iter()
        .map(|(entity, view, camera, refraction)| {
            (
                view.retained_view_entity,
                (camera_kind(camera, refraction), entity),
            )
        })
        .collect();
    let mut shadow_groups = BTreeMap::new();
    let mut camera_groups = BTreeMap::new();
    for (key, buffers) in &bins.view_phase_buffers {
        let jobs = buffers
            .indexed_unpacking_jobs
            .iter()
            .chain(&buffers.non_indexed_unpacking_jobs);
        let (count, total) = jobs
            .filter(|job| job.mesh_instance_count > 0)
            .fold((0, 0_u64), |(count, total), job| {
                (count + 1, total + u64::from(job.mesh_instance_count))
            });
        if count == 0 {
            continue;
        }
        let phase = mesh_bin_phase(key.phase);
        let (view_phases, batches, instances) =
            phases.entry(phase).or_insert((0_usize, 0_usize, 0_u64));
        *view_phases += 1;
        *batches += count;
        *instances += total;
        if phase == "shadow" {
            let group = shadow_kinds
                .get(&key.view)
                .copied()
                .unwrap_or(("unknown", 0));
            let (views, batches, instances) = shadow_groups.entry(group).or_insert((0, 0, 0));
            *views += 1;
            *batches += count;
            *instances += total;
        }
        if matches!(phase, "forward" | "prepass")
            && let Some(&(kind, camera)) = camera_kinds.get(&key.view)
        {
            let (view_phases, batches, instances) = camera_groups
                .entry((kind, camera, phase))
                .or_insert((0, 0, 0));
            *view_phases += 1;
            *batches += count;
            *instances += total;
        }
    }
    for (phase, (view_phases, batches, instances)) in phases {
        info!(
            event_kind = "runtime_mesh_bins",
            phase,
            view_phases,
            batches,
            instances,
            "Prepared nonempty mesh bin jobs across render views"
        );
    }
    let frame = frame.into_inner().0;
    let shadow_instances = |kind| {
        shadow_groups
            .iter()
            .filter(|((light, _), _)| *light == kind)
            .map(|(_, (_, _, instances))| *instances)
            .sum::<u64>()
    };
    info!(
        event_kind = "runtime_shadow_mesh_sample",
        ?frame,
        directional_instances = shadow_instances("directional"),
        point_instances = shadow_instances("point"),
        spot_instances = shadow_instances("spot"),
        unknown_instances = shadow_instances("unknown"),
        "Prepared shadow mesh instances for this sample, including empty groups"
    );
    for ((light, subview), (views, batches, instances)) in shadow_groups {
        info!(
            event_kind = "runtime_shadow_mesh_bins",
            ?frame,
            light,
            subview,
            views,
            batches,
            instances,
            "Prepared nonempty shadow mesh bin jobs by light type and subview"
        );
    }
    log_camera_mesh_bins(frame, camera_groups);
}

type CameraMeshBins = BTreeMap<(&'static str, Entity, &'static str), (usize, usize, u64)>;

fn log_camera_mesh_bins(frame: Option<u32>, groups: CameraMeshBins) {
    for ((kind, camera, phase), (view_phases, batches, instances)) in groups {
        info!(
            event_kind = "runtime_camera_mesh_bins",
            ?frame,
            kind,
            ?camera,
            phase,
            view_phases,
            batches,
            instances,
            "Prepared nonempty mesh bin jobs for an extracted camera"
        );
    }
}

const GRAPH_BANDS: [(&str, &str); 6] = [
    ("atmosphere_band", "render.graph.atmosphere"),
    ("clouds_band", "render.graph.clouds"),
    ("camera_band", "render.graph.camera"),
    ("graph_tail_before_submit", "render.graph.before_submit"),
    ("graph_submit_and_finish", "render.graph.submit_and_finish"),
    (
        "post_graph_render_tail",
        "render.graph.post_graph_render_tail",
    ),
];

fn graph_begin(mut clock: ResMut<RenderClock>) {
    clock.begin_graph();
}

fn graph_mark<const BAND: usize>(mut clock: ResMut<RenderClock>) {
    record_graph_band(&mut clock, BAND);
}

fn record_graph_band(clock: &mut RenderClock, band: usize) {
    let (name, label) = GRAPH_BANDS[band];
    clock.mark_graph(name);
    if let Some(&elapsed_ms) = clock.graph_bands.get(name) {
        cpu_timing::record(label, elapsed_ms);
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy extract system parameter."
)]
fn extract_frame(mut extracted: ResMut<ExtractedFrame>, frame: Extract<Option<Res<FrameCount>>>) {
    extracted.0 = frame.as_ref().map(|frame| frame.0);
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy render system parameters."
)]
fn log_render_frame(frame: Res<ExtractedFrame>, clock: Res<RenderClock>) {
    if let (Some(frame), Some(elapsed_ms)) = (frame.0, clock.elapsed_ms) {
        bevy::log::debug!(
            event_kind = "render_cpu_frame",
            frame,
            elapsed_ms,
            stages_ms = ?clock.stages,
            "Render CPU timings by extracted frame"
        );
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy render system parameters."
)]
fn observe_primary_texture(
    windows: Option<Res<ExtractedWindows>>,
    mut trace: ResMut<PrimaryPresentTrace>,
) {
    trace.0 = windows.and_then(|windows| {
        let entity = windows.primary?;
        Some((entity, windows.get(&entity)?.swap_chain_texture.is_some()))
    });
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy render system parameters."
)]
fn trace_primary_present(
    windows: Option<Res<ExtractedWindows>>,
    frame: Res<ExtractedFrame>,
    trace: Res<PrimaryPresentTrace>,
) {
    let (Some(windows), Some(frame), Some((entity, texture_before))) =
        (windows, frame.0, trace.0)
    else {
        return;
    };
    let Some(window) = windows.get(&entity) else {
        return;
    };
    let texture_after = window.swap_chain_texture.is_some();
    // Bevy takes this texture when it requests presentation from wgpu.
    // The window compositor can still choose when to display it.
    debug!(
        target: "forge::present",
        event_kind = "primary_window_present",
        frame,
        texture_before,
        texture_after,
        submitted = texture_before && !texture_after,
        "Primary window surface texture across the renderer"
    );
}

fn render_end(mut clock: ResMut<RenderClock>) {
    let now = Instant::now();
    if let Some(last) = clock.last {
        clock
            .stages
            .insert(STAGES[11], now.duration_since(last).as_secs_f64() * 1000.0);
    }
    clock.elapsed_ms = clock
        .start
        .map(|start| now.duration_since(start).as_secs_f64() * 1000.0);
    clock.stage_span.take();
    clock.schedule_span.take();
}
fn camera_kind(camera: &ExtractedCamera, refraction: bool) -> &'static str {
    if refraction {
        "refraction"
    } else if matches!(camera.target, Some(NormalizedRenderTarget::Window(_))) {
        "window"
    } else {
        "offscreen"
    }
}

fn opaque_view_begin(
    view: Res<CurrentView>,
    cameras: Query<(
        &ExtractedCamera,
        Has<crate::source_refraction::CaptureCamera>,
    )>,
    mut context: RenderContext,
) {
    let view = view.into_inner();
    let kind = cameras
        .get(view.0)
        .map_or("offscreen", |(camera, refraction)| {
            camera_kind(camera, refraction)
        });
    if let Some(recorder) = context.diagnostic_recorder() {
        let name = format!("opaque_{kind}_{:?}", view.0);
        recorder.begin_time_span(context.command_encoder(), name.into());
    }
}

fn opaque_view_end(mut context: RenderContext) {
    if let Some(recorder) = context.diagnostic_recorder() {
        recorder.end_time_span(context.command_encoder());
    }
}

// Bevy runs the shared shadow pass once per punctual shadow view. The
// point light's retained entity and face identify the same work across frames.
fn point_shadow_begin(view: ViewQuery<&LightEntity, With<ShadowView>>, mut context: RenderContext) {
    let LightEntity::Point {
        light_entity,
        face_index,
    } = view.into_inner()
    else {
        return;
    };
    if let Some(recorder) = context.diagnostic_recorder() {
        let name = format!("point_shadow_early_{light_entity:?}_{face_index}");
        recorder.begin_time_span(context.command_encoder(), name.into());
    }
}

fn point_shadow_end(view: ViewQuery<&LightEntity, With<ShadowView>>, mut context: RenderContext) {
    if matches!(view.into_inner(), LightEntity::Point { .. })
        && let Some(recorder) = context.diagnostic_recorder()
    {
        recorder.end_time_span(context.command_encoder());
    }
}

// Exclusive functions preserve the reference driving-thread and encoder ordering.
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
fn gpu_latest(
    mut clock: ResMut<MainClock>,
    frame: Res<FrameCount>,
    diagnostics: Option<Res<DiagnosticsStore>>,
    windows: Query<&Window, With<bevy::window::PrimaryWindow>>,
) {
    let frame = frame.into_inner().0;
    let diagnostics = diagnostics.map(Res::into_inner);
    if !cpu_timing::enabled()
        || clock
            .gpu_logged
            .is_some_and(|last| last.elapsed().as_secs_f64() < 5.0)
    {
        return;
    }
    clock.gpu_logged = Some(Instant::now());
    let values: BTreeMap<_,_> = diagnostics.into_iter().flat_map(DiagnosticsStore::iter)
        .filter(|d| d.path().to_string().starts_with("render/") && (d.path().to_string().ends_with("elapsed_gpu") || d.path().to_string().ends_with("elapsed_cpu")))
        .filter_map(|d| d.measurement().filter(|m| m.value.is_finite()).map(|m| (d.path().to_string(), serde_json::json!({"milliseconds":m.value,"age_ms":m.time.elapsed().as_secs_f64()*1000.0}))))
        .collect();
    let window = windows.single().ok().map(|window| {
        serde_json::json!({
            "focused": window.focused,
            "present_mode": format!("{:?}", window.present_mode),
            "physical_size": [window.physical_width(), window.physical_height()],
        })
    });
    info!(event_kind="runtime_gpu_latest",frame,values=%serde_json::json!(values),window=%serde_json::json!(window),"Asynchronous diagnostic values retain their age; not current-frame GPU proof");
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn scene_evidence_and_timing_share_diagnostics_in_either_install_order() {
        for timing_first in [false, true] {
            let mut app = App::new();
            if timing_first {
                app.add_plugins(RenderTimingPlugin);
            }
            app.add_plugins(crate::RuntimeEvidencePlugin::new(
                crate::RuntimeAutomation::default(),
            ));
            if !timing_first {
                app.add_plugins(RenderTimingPlugin);
            }
            assert!(app.is_plugin_added::<RenderDiagnosticsPlugin>());
            assert!(app.world().contains_resource::<MainClock>());
        }
    }

    #[test]
    fn complete_main_scope_contains_every_last_system() {
        fn during_last(clock: Res<MainClock>) {
            assert!(clock.into_inner().span.is_some());
        }
        fn after_main(clock: Res<MainClock>) {
            assert!(clock.into_inner().span.is_none());
        }
        let mut app = App::new();
        app.add_plugins(RenderTimingPlugin)
            .add_systems(Last, during_last)
            .add_systems(TimingComplete, after_main.after(TimingSet::MainEnd));
        app.update();
        app.update();
        assert!(app.world().resource::<MainClock>().elapsed_ms().is_some());
    }

    #[test]
    fn missing_clock_data_remains_unavailable_and_graph_samples_are_owned() {
        let mut clock = RenderClock::default();
        assert!(clock.snapshot().schedule_ms.is_none());
        assert!(clock.snapshot().stages_ms.is_empty());
        clock.mark_graph("no_begin");
        assert!(clock.snapshot().graph_bands_ms.is_empty());
        clock.begin_graph();
        clock.mark_graph("caller_band");
        let previous = clock.snapshot();
        assert!(previous.graph_bands_ms["caller_band"].is_finite());
        clock.begin_graph();
        assert!(clock.snapshot().graph_bands_ms.is_empty());
        assert_eq!(previous.graph_bands_ms.len(), 1);
    }

    #[test]
    fn render_stage_chain_finishes_every_band_and_restarts_without_stale_values() {
        let mut app = App::new();
        app.init_resource::<RenderClock>().add_systems(
            Update,
            (
                render_begin,
                render_mark::<0>,
                render_mark::<1>,
                render_mark::<2>,
                render_mark::<3>,
                render_mark::<4>,
                render_mark::<5>,
                render_mark::<6>,
                render_mark::<7>,
                render_mark::<8>,
                render_mark::<9>,
                render_mark::<10>,
                render_end,
            )
                .chain(),
        );
        app.update();
        let previous = app.world().resource::<RenderClock>().snapshot();
        assert_eq!(previous.stages_ms.len(), STAGES.len());
        assert!(
            previous
                .stages_ms
                .values()
                .all(|value| value.is_finite() && *value >= 0.0)
        );
        assert!(
            previous
                .schedule_ms
                .is_some_and(|value| value.is_finite() && value >= 0.0)
        );
        assert!(app.world().resource::<RenderClock>().stage_span.is_none());
        assert!(
            app.world()
                .resource::<RenderClock>()
                .schedule_span
                .is_none()
        );
        app.update();
        assert_eq!(
            app.world()
                .resource::<RenderClock>()
                .snapshot()
                .stages_ms
                .len(),
            STAGES.len()
        );
        assert_eq!(previous.stages_ms.len(), STAGES.len());
    }

    #[test]
    fn main_schedule_closes_its_scope_and_first_interval_is_unavailable() {
        let mut app = App::new();
        app.init_resource::<MainClock>()
            .add_systems(First, main_begin)
            .add_systems(Last, main_end);
        assert!(app.world().resource::<MainClock>().elapsed_ms().is_none());
        app.update();
        assert!(app.world().resource::<MainClock>().span.is_none());
        assert!(app.world().resource::<MainClock>().wall_ms().is_none());
        app.update();
        assert!(
            app.world()
                .resource::<MainClock>()
                .wall_ms()
                .is_some_and(|value| value >= 0.0)
        );
        assert!(
            app.world()
                .resource::<MainClock>()
                .elapsed_ms()
                .is_some_and(|value| value >= 0.0)
        );
    }
}
