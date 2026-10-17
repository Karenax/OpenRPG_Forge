use crate::{
    GovernedAssetKitRuntimeStats, RuntimeAutomation, diagnostic_value,
    validate_governed_asset_kit_runtime_stats,
};
use bevy::{
    diagnostic::{DiagnosticsStore, FrameCount},
    prelude::*,
    render::{
        ExtractSchedule, MainWorld, RenderApp,
        render_resource::{CachedPipelineState, PipelineCache},
        view::screenshot::{Screenshot, ScreenshotCaptured, save_to_disk},
    },
    window::PrimaryWindow,
};
use serde::Serialize;

const REQUIRED_STABLE_OBSERVATIONS: u32 = 8;
#[cfg(feature = "qualification")]
const FIXED_VIEW_STABLE_OBSERVATIONS: u32 = 30;
const CAPTURE_TIMEOUT_SECONDS: f64 = 120.0;

#[derive(Clone, Debug, Default, PartialEq, Serialize)]
struct PipelineSnapshot {
    observation: u64,
    total: usize,
    ready: usize,
    pending: usize,
    failed: usize,
}

#[derive(Clone, Debug, Default, Resource, Serialize)]
pub(super) struct CaptureProgress {
    pipeline: PipelineSnapshot,
    stable_observations: u32,
    required_stable_observations: u32,
    scene_ready: bool,
    earliest_frame: u32,
    issued_frame: Option<u32>,
    callback_received_frame: Option<u32>,
    #[serde(skip)]
    last_checked: PipelineSnapshot,
    #[serde(skip)]
    wait_logged: bool,
    #[serde(skip)]
    failed: bool,
}

/// Frame on which the readiness gate issued the screenshot request.
/// This does not assert GPU readback completion or correctness of the pixels.
pub fn issued_capture_frame(world: &World) -> Option<u32> {
    world
        .get_resource::<CaptureProgress>()
        .and_then(|progress| progress.issued_frame)
}

impl CaptureProgress {
    /// Shared startup gate for still captures and offline movement recordings.
    /// This observes known pipelines, not the correctness of the rendered pixels.
    #[cfg(feature = "qualification")]
    pub(super) fn render_ready(&mut self, scene_ready: bool) -> bool {
        self.observe(scene_ready);
        !self.failed
            && self.scene_ready
            && self.stable_observations
                >= self
                    .required_stable_observations
                    .max(REQUIRED_STABLE_OBSERVATIONS)
    }

    #[cfg(all(test, feature = "qualification"))]
    pub(super) fn set_test_pipeline_sample(&mut self, total: usize, pending: usize, failed: usize) {
        self.pipeline = PipelineSnapshot {
            observation: self.pipeline.observation + 1,
            total,
            ready: total.saturating_sub(pending + failed),
            pending,
            failed,
        };
    }

    fn observe(&mut self, scene_ready: bool) {
        self.scene_ready = scene_ready;
        if !scene_ready
            || self.pipeline.total == 0
            || self.pipeline.pending != 0
            || self.pipeline.failed != 0
        {
            self.stable_observations = 0;
        } else if self.pipeline.observation != self.last_checked.observation {
            self.stable_observations = if self.pipeline.total == self.last_checked.total {
                self.stable_observations.saturating_add(1)
            } else {
                1
            };
        }
        self.last_checked.clone_from(&self.pipeline);
    }

    fn can_capture(&self, frame: u32) -> bool {
        frame >= self.earliest_frame
            && self.issued_frame.is_none()
            && !self.failed
            && self.scene_ready
            && self.stable_observations
                >= self
                    .required_stable_observations
                    .max(REQUIRED_STABLE_OBSERVATIONS)
    }

    pub(super) fn ensure_completed(&self) -> Result<(), &'static str> {
        if self.issued_frame.is_none() {
            return Err(
                "capture was not issued: render readiness was not reached before the bounded exit",
            );
        }
        if self.callback_received_frame.is_none() {
            return Err(
                "capture was issued but its GPU readback callback was not received before the bounded exit",
            );
        }
        Ok(())
    }
}

pub(super) struct CaptureReadinessPlugin;

impl Plugin for CaptureReadinessPlugin {
    fn build(&self, app: &mut App) {
        app.init_resource::<CaptureProgress>();
        if let Some(render_app) = app.get_sub_app_mut(RenderApp) {
            // ExtractSchedule provides exclusive access to MainWorld, including pipelined rendering.
            render_app.add_systems(ExtractSchedule, observe_pipeline_cache);
        }
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy extraction system parameters"
)]
fn observe_pipeline_cache(mut main_world: ResMut<MainWorld>, pipelines: Res<PipelineCache>) {
    let Some(mut progress) = main_world.get_resource_mut::<CaptureProgress>() else {
        return;
    };
    if progress.issued_frame.is_some() || progress.failed {
        return;
    }
    let mut snapshot = PipelineSnapshot {
        observation: progress.pipeline.observation.saturating_add(1),
        ..default()
    };
    for pipeline in pipelines.pipelines() {
        snapshot.total += 1;
        match &pipeline.state {
            CachedPipelineState::Ok(_) => snapshot.ready += 1,
            CachedPipelineState::Queued | CachedPipelineState::Creating(_) => snapshot.pending += 1,
            CachedPipelineState::Err(_) => snapshot.failed += 1,
        }
    }
    progress.pipeline = snapshot;
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::too_many_arguments,
    reason = "independent Bevy capture prerequisites"
)]
pub(super) fn capture_at_requested_frame(
    mut commands: Commands,
    frame: Res<FrameCount>,
    time: Res<Time<Real>>,
    automation: Res<RuntimeAutomation>,
    progress: Option<ResMut<CaptureProgress>>,
    #[cfg(feature = "qualification")] fixed_view: Option<Res<crate::FixedProofViewStatus>>,
    #[cfg(feature = "qualification")] virtual_time: Res<Time<Virtual>>,
    #[cfg(feature = "qualification")] fixed_time: Res<Time<Fixed>>,
    kit: Option<Res<GovernedAssetKitRuntimeStats>>,
    #[cfg(feature = "physics-rapier")] warmup: Option<Res<crate::ProfileWarmup>>,
    diagnostics: Res<DiagnosticsStore>,
    windows: Query<&Window, With<PrimaryWindow>>,
    mut exit: MessageWriter<AppExit>,
) {
    let (Some(path), Some(mut progress)) = (&automation.capture_path, progress) else {
        return;
    };
    if progress.callback_received_frame.is_some() || progress.failed {
        return;
    }
    progress.earliest_frame = automation.capture_frame;
    #[cfg(feature = "qualification")]
    let fixed_view_ready = fixed_view.as_deref().is_none_or(|view| {
        view.installed
            && view.error.is_none()
            && view.current_view_matches
            && view.stable_main_frames >= FIXED_VIEW_STABLE_OBSERVATIONS
            && virtual_time.is_paused()
            && view.virtual_elapsed_ns == virtual_time.elapsed().as_nanos()
            && view.fixed_elapsed_ns == fixed_time.elapsed().as_nanos()
    });
    #[cfg(not(feature = "qualification"))]
    let fixed_view_ready = true;
    #[cfg(feature = "qualification")]
    {
        progress.required_stable_observations = if fixed_view.is_some() {
            FIXED_VIEW_STABLE_OBSERVATIONS
        } else {
            REQUIRED_STABLE_OBSERVATIONS
        };
    }
    if time.elapsed_secs_f64() >= CAPTURE_TIMEOUT_SECONDS {
        progress.failed = true;
        error!(target: "forge::proof", schema = "forge.runtime-event", schema_version = 1_u64,
            event_kind = "capture_readiness_timeout", frame = frame.0,
            "capture did not complete within the 120 second watchdog");
        exit.write(AppExit::error());
        return;
    }
    if progress.issued_frame.is_some() {
        return;
    }
    #[cfg(feature = "physics-rapier")]
    let fixed_view_ready = fixed_view_ready
        && warmup
            .as_deref()
            .is_none_or(crate::ProfileWarmup::permits_capture);
    let scene_ready = fixed_view_ready && windows.single().is_ok_and(|window|
        window.physical_width() > 0 && window.physical_height() > 0)
        && validate_governed_asset_kit_runtime_stats(kit.as_deref()).is_ok()
        // Some backends do not expose pipeline statistics. Absence is not a GPU-success claim.
        && diagnostic_value(&diagnostics, "render/main_opaque_pass_3d/clipper_primitives_out")
            .is_none_or(|primitives| primitives > 0.0);
    progress.observe(scene_ready);
    if progress.can_capture(frame.0) {
        progress.issued_frame = Some(frame.0);
        commands
            .spawn(Screenshot::primary_window())
            .observe(save_to_disk(path.clone()))
            .observe(record_capture_callback);
        info!(target: "forge::proof", schema = "forge.runtime-event", schema_version = 1_u64,
            event_kind = "capture_requested", path = %path.display(), frame = frame.0,
            earliest_frame = automation.capture_frame, pipelines_ready = progress.pipeline.ready,
            stable_observations = progress.stable_observations,
            required_stable_observations = progress.required_stable_observations.max(REQUIRED_STABLE_OBSERVATIONS),
            "capture requested after observed render readiness");
    } else if frame.0 >= automation.capture_frame && !progress.wait_logged {
        progress.wait_logged = true;
        info!(target: "forge::proof", schema = "forge.runtime-event", schema_version = 1_u64,
            event_kind = "capture_waiting_for_render", frame = frame.0,
            pipelines_pending = progress.pipeline.pending, pipelines_failed = progress.pipeline.failed,
            scene_ready, "capture deferred; bounded exit remains unchanged");
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy observer parameters")]
fn record_capture_callback(
    _: On<ScreenshotCaptured>,
    frame: Res<FrameCount>,
    mut progress: ResMut<CaptureProgress>,
) {
    progress.callback_received_frame = Some(frame.0);
    info!(target: "forge::proof", schema = "forge.runtime-event", schema_version = 1_u64,
        event_kind = "capture_readback_received", frame = frame.0,
        "screenshot readback received; file still requires verification");
}

#[cfg(test)]
mod tests {
    use super::*;

    fn ready_sample(progress: &mut CaptureProgress, total: usize) {
        progress.pipeline = PipelineSnapshot {
            observation: progress.pipeline.observation + 1,
            total,
            ready: total,
            ..default()
        };
        progress.observe(true);
    }

    #[test]
    fn capture_waits_for_real_nonempty_stable_observations_and_only_issues_once() {
        let mut progress = CaptureProgress {
            earliest_frame: 10,
            ..default()
        };
        for _ in 0..20 {
            progress.observe(true);
        }
        assert!(!progress.can_capture(10));
        for _ in 0..7 {
            ready_sample(&mut progress, 12);
        }
        assert!(!progress.can_capture(10));
        ready_sample(&mut progress, 12);
        assert!(!progress.can_capture(9));
        assert!(progress.can_capture(15));
        progress.issued_frame = Some(15);
        assert!(!progress.can_capture(16));
        assert!(progress.ensure_completed().is_err());
        progress.callback_received_frame = Some(17);
        assert!(progress.ensure_completed().is_ok());
    }

    #[test]
    fn fixed_capture_requires_thirty_fresh_pipeline_observations_after_scene_ready() {
        let mut progress = CaptureProgress {
            required_stable_observations: 30,
            ..default()
        };
        for _ in 0..29 {
            ready_sample(&mut progress, 12);
        }
        assert!(!progress.can_capture(300));
        for _ in 0..50 {
            progress.observe(true);
        }
        assert!(!progress.can_capture(300));
        ready_sample(&mut progress, 12);
        assert!(progress.can_capture(300));
        progress.observe(false);
        assert!(!progress.can_capture(300));
        for _ in 0..29 {
            ready_sample(&mut progress, 12);
        }
        assert!(!progress.can_capture(300));
        ready_sample(&mut progress, 12);
        assert!(progress.can_capture(300));
    }

    #[test]
    fn expired_watchdog_fails_without_issuing_a_screenshot() {
        let mut app = App::new();
        app.add_plugins(MinimalPlugins)
            .init_resource::<FrameCount>()
            .init_resource::<DiagnosticsStore>()
            .init_resource::<CaptureProgress>()
            .insert_resource(RuntimeAutomation {
                capture_path: Some("must-not-be-created.png".into()),
                capture_frame: 1,
                ..default()
            })
            .insert_resource(bevy::time::TimeUpdateStrategy::ManualDuration(
                std::time::Duration::from_secs(121),
            ))
            .add_systems(Update, capture_at_requested_frame);
        app.update();
        app.update();
        let progress = app.world().resource::<CaptureProgress>();
        assert!(progress.failed);
        assert!(progress.issued_frame.is_none());
        assert!(
            app.world_mut()
                .resource_mut::<Messages<AppExit>>()
                .drain()
                .any(|event| matches!(event, AppExit::Error(_)))
        );
        assert_eq!(
            app.world_mut()
                .query::<&Screenshot>()
                .iter(app.world())
                .count(),
            0
        );
    }

    #[test]
    fn new_pipelines_errors_and_incomplete_scene_reset_readiness() {
        let mut progress = CaptureProgress::default();
        for _ in 0..8 {
            ready_sample(&mut progress, 12);
        }
        ready_sample(&mut progress, 13);
        assert!(!progress.can_capture(20));
        for _ in 0..7 {
            ready_sample(&mut progress, 13);
        }
        assert!(progress.can_capture(20));
        progress.pipeline.pending = 1;
        progress.observe(true);
        assert!(!progress.can_capture(20));
        progress.pipeline.pending = 0;
        progress.pipeline.failed = 1;
        progress.observe(true);
        assert!(!progress.can_capture(20));
        for _ in 0..8 {
            ready_sample(&mut progress, 13);
        }
        progress.observe(false);
        assert!(!progress.can_capture(20));
        assert!(progress.ensure_completed().is_err());
    }
}
