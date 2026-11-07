//! Shared observational clocks extracted from the reference performance walk.
//! No inputs, readiness, authored state or presentation mode are changed.
//! CPU bands and asynchronous GPU diagnostics overlap; they are not additive.
use crate::cpu_timing;
use bevy::{
    app::MainScheduleOrder,
    diagnostic::{DiagnosticsStore, FrameCount},
    ecs::schedule::ScheduleLabel,
    platform::time::Instant,
    prelude::*,
    render::{
        Render, RenderApp, RenderSystems,
        diagnostic::{
            DiagnosticsRecorder, RecordDiagnostics, RenderDiagnosticsPlugin, resolve_encoder,
        },
        renderer::{PendingCommandBuffers, RenderDevice, RenderGraph, RenderGraphSystems},
    },
};
use std::collections::BTreeMap;

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
}
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
            .add_systems(
                TimingComplete,
                (
                    main_end.in_set(TimingSet::MainEnd),
                    gpu_latest.after(TimingSet::MainEnd),
                ),
            )
            .add_systems(FixedFirst, fixed_begin)
            .add_systems(FixedLast, fixed_end);
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
    }
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
fn main_end(mut clock: ResMut<MainClock>) {
    clock.span.take();
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

const GRAPH_BANDS: [(&str, &str); 6] = [
    ("atmosphere_band", "render.graph.atmosphere"),
    ("clouds_band", "render.graph.clouds"),
    ("camera_band", "render.graph.camera"),
    ("graph_tail_before_submit", "render.graph.before_submit"),
    ("graph_submit_and_finish", "render.graph.submit_and_finish"),
    ("post_graph_render_tail", "render.graph.post_graph_render_tail"),
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
