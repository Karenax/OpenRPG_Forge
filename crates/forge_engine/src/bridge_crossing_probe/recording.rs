//! Offline rendered movement evidence, deliberately separate from realtime performance probes.
use super::{BridgeCrossingProbePhase, BridgeCrossingProbeState};
use crate::GovernedAssetKitReadiness;
use crate::capture_readiness::{CaptureProgress, CaptureReadinessPlugin};
use bevy::platform::time::Instant;
use bevy::{
    app::AppExit,
    prelude::*,
    render::view::screenshot::{Screenshot, ScreenshotCaptured, save_to_disk},
    time::TimeUpdateStrategy,
    window::PrimaryWindow,
};
use std::{path::PathBuf, time::Duration};

const WARMUP_RENDER_FRAMES: u32 = 120;
const MAX_CAPTURE_FRAMES: u32 = 600;
const WATCHDOG_SECONDS: u64 = 180;

#[derive(Resource)]
pub(super) struct BridgeRecording {
    directory: PathBuf,
    started: Instant,
    warmup_frames: u32,
    ready: bool,
    requested: u32,
    saved: u32,
}

impl BridgeRecording {
    pub(super) fn requested_frame(&self) -> Option<u32> {
        self.requested.checked_sub(1)
    }
}

pub(super) fn install(app: &mut App, directory: PathBuf) {
    if !app.is_plugin_added::<CaptureReadinessPlugin>() {
        app.add_plugins(CaptureReadinessPlugin);
    }
    app.insert_resource(BridgeRecording {
        directory,
        started: Instant::now(),
        warmup_frames: 0,
        ready: false,
        requested: 0,
        saved: 0,
    })
    .insert_resource(TimeUpdateStrategy::ManualDuration(Duration::from_secs_f64(
        1.0 / 30.0,
    )))
    .add_systems(Startup, label_offline_capture)
    .add_systems(Last, (record_frame, trace_lod_inputs).chain());
}

/// CPU inputs paired with screenshot request numbers, not proof of GPU LOD selection.
#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn trace_lod_inputs(
    recording: Res<BridgeRecording>,
    mut traced: Local<u32>,
    cameras: Query<(&Camera, &GlobalTransform), With<Camera3d>>,
    scatter: Option<Res<crate::VegetationScatterProjection>>,
    presentation: Option<Res<crate::vegetation::VegetationPresentation>>,
    projection: Option<Res<crate::vegetation::VegetationProjectionStats>>,
) {
    if recording.requested == *traced {
        return;
    }
    let frame = recording.requested - 1;
    *traced = recording.requested;
    let active: Vec<_> = cameras
        .iter()
        .filter(|(camera, _)| camera.is_active)
        .collect();
    let [(camera, transform)] = active.as_slice() else {
        tracing::warn!(
            event_kind = "bridge_lod_trace_unavailable",
            frame,
            active_cameras = active.len(),
            "one active camera required for LOD trace"
        );
        return;
    };
    let position = transform.translation();
    let clip_from_world = camera.clip_from_view() * transform.to_matrix().inverse();
    tracing::info!(
        event_kind = "bridge_lod_camera_sample",
        frame,
        x = position.x,
        y = position.y,
        z = position.z,
        clip_from_world_json = %serde_json::json!(clip_from_world.to_cols_array()),
        evidence = "cpu_camera_at_screenshot_request_not_gpu_selection"
    );
    if frame == 0 {
        if let (Some(scatter), Some(presentation)) = (scatter, presentation) {
            let governed = projection.as_ref().is_some_and(|stats| {
                stats.prototype_preview.is_some() && stats.prototype_preview_error.is_none()
            });
            let ranges = trace_lod_ranges(presentation.0, governed);
            let trees: Vec<_> = scatter.trees().map(|tree| serde_json::json!({
                "id": tree.id, "root": [tree.position_m.x, tree.position_m.y, tree.position_m.z]
            })).collect();
            tracing::info!(event_kind = "bridge_lod_trace_roots",
                roots_json = %serde_json::Value::Array(trees),
                governed_prototypes = governed,
                near_start = ranges.near.end_margin.start,
                near_end = ranges.near.end_margin.end,
                far_start = ranges.medium.end_margin.start,
                far_end = ranges.medium.end_margin.end,
                evidence = "logical_tree_roots_not_frustum_or_gpu_visibility");
        } else {
            tracing::warn!(
                event_kind = "bridge_lod_trace_unavailable",
                frame,
                "vegetation source or presentation missing"
            );
        }
    }
}

fn trace_lod_ranges(
    profile: forge_project::VegetationPresentationProfile,
    governed: bool,
) -> crate::vegetation::VegetationLodRanges {
    if governed {
        crate::vegetation::VegetationLodRanges::from_governed_profile(profile)
    } else {
        crate::vegetation::VegetationLodRanges::from_profile(profile)
    }
}

fn label_offline_capture(mut commands: Commands) {
    commands.spawn((
        Text::new("CAPTURE HORS TEMPS REEL | 30 images/s imposees | Pas une mesure FPS"),
        TextFont {
            font_size: bevy::text::FontSize::Px(20.0),
            ..default()
        },
        BackgroundColor(Color::srgba(0.0, 0.0, 0.0, 0.8)),
        Node {
            position_type: PositionType::Absolute,
            bottom: px(12),
            left: px(12),
            ..default()
        },
    ));
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameter")]
pub(super) fn recording_ready(
    recording: Option<Res<BridgeRecording>>,
    kit: Option<Res<GovernedAssetKitReadiness>>,
) -> bool {
    recording.is_none_or(|recording| recording.ready) && kit.is_none_or(|kit| kit.ready)
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
pub(super) fn record_frame(
    mut commands: Commands,
    mut recording: ResMut<BridgeRecording>,
    mut probe: ResMut<BridgeCrossingProbeState>,
    kit: Option<Res<GovernedAssetKitReadiness>>,
    mut render: ResMut<CaptureProgress>,
    windows: Query<&Window, With<PrimaryWindow>>,
    mut exit: MessageWriter<AppExit>,
) {
    if probe.phase == BridgeCrossingProbePhase::Failed {
        return;
    }
    if recording.started.elapsed().as_secs() >= WATCHDOG_SECONDS {
        tracing::error!(
            event_kind = "bridge_recording_failed",
            "offline recording exceeded 180 seconds"
        );
        probe.phase = BridgeCrossingProbePhase::Failed;
        exit.write(AppExit::error());
        return;
    }
    if !recording.ready {
        recording.warmup_frames += 1;
        let scene_ready = kit.is_none_or(|kit| kit.ready)
            && windows
                .single()
                .is_ok_and(|window| window.physical_width() > 0 && window.physical_height() > 0);
        let render_ready = render.render_ready(scene_ready);
        recording.ready = recording.warmup_frames >= WARMUP_RENDER_FRAMES && render_ready;
        if recording.ready {
            tracing::info!(
                event_kind = "bridge_recording_render_ready",
                warmup_frames = recording.warmup_frames,
                readiness = %serde_json::json!(&*render),
                "known render pipelines stable before offline movement starts"
            );
        }
        return;
    }
    if probe.phase == BridgeCrossingProbePhase::Completed {
        if recording.requested > 0 && recording.saved == recording.requested {
            tracing::info!(
                event_kind = "bridge_recording_completed",
                success = true,
                frames = recording.saved,
                frames_per_second = 30_u32,
                mode = "offline_manual_time",
                performance_qualified = false,
                "all bridge screenshot callbacks received; final runtime verdict checks write errors"
            );
            exit.write(AppExit::Success);
        }
        return;
    }
    if recording.requested >= MAX_CAPTURE_FRAMES {
        tracing::error!(
            event_kind = "bridge_recording_failed",
            "offline recording exceeded 600 frames"
        );
        probe.phase = BridgeCrossingProbePhase::Failed;
        exit.write(AppExit::error());
        return;
    }
    let path = recording
        .directory
        .join(format!("frame-{:04}.png", recording.requested));
    recording.requested += 1;
    commands.spawn(Screenshot::primary_window()).observe(
        move |event: On<ScreenshotCaptured>, mut recording: ResMut<BridgeRecording>| {
            // Bevy logs conversion/I/O errors; the runtime's independent ERROR guard fails this run.
            save_to_disk(&path)(event);
            recording.saved += 1;
        },
    );
}

#[cfg(test)]
mod tests {
    use super::*;

    fn app() -> App {
        let mut app = App::new();
        app.add_plugins(MinimalPlugins)
            .add_message::<AppExit>()
            .insert_resource(BridgeCrossingProbeState::new());
        install(&mut app, std::env::temp_dir());
        app.world_mut().spawn((Window::default(), PrimaryWindow));
        app
    }

    #[test]
    fn trace_uses_governed_ranges_without_changing_procedural_ranges() {
        let mut profile = forge_world::VisualValleyVegetationRecipe::default()
            .build()
            .unwrap()
            .presentation;
        profile.near_lod_distance_m = 24.0;
        profile.medium_lod_distance_m = 72.0;
        let governed = trace_lod_ranges(profile, true);
        let procedural = trace_lod_ranges(profile, false);
        assert_eq!(governed.near.end_margin, 24.0..32.0);
        assert_eq!(procedural.near.end_margin, 64.0..72.0);
        assert_eq!(governed.medium.start_margin, governed.near.end_margin);
        assert!(governed.hlod == procedural.hlod);
    }

    #[test]
    fn warmup_waits_for_fresh_render_observations_and_kit_readiness() {
        let mut app = app();
        app.insert_resource(GovernedAssetKitReadiness::default());
        for _ in 0..=WARMUP_RENDER_FRAMES {
            app.update();
        }
        assert!(!app.world().resource::<BridgeRecording>().ready);
        app.world_mut()
            .resource_mut::<GovernedAssetKitReadiness>()
            .ready = true;
        // Main-world frames alone cannot qualify a missing renderer.
        app.update();
        assert!(!app.world().resource::<BridgeRecording>().ready);
        for _ in 0..7 {
            app.world_mut()
                .resource_mut::<CaptureProgress>()
                .set_test_pipeline_sample(12, 0, 0);
            app.update();
        }
        for _ in 0..20 {
            app.update();
        }
        assert!(!app.world().resource::<BridgeRecording>().ready);
        app.world_mut()
            .resource_mut::<CaptureProgress>()
            .set_test_pipeline_sample(12, 0, 0);
        app.update();
        let recording = app.world().resource::<BridgeRecording>();
        assert!(recording.ready);
        assert_eq!(recording.requested, 0);
    }

    #[test]
    fn pending_and_failed_pipelines_prevent_recording_after_warmup() {
        for (pending, failed) in [(1, 0), (0, 1)] {
            let mut app = app();
            for _ in 0..=WARMUP_RENDER_FRAMES {
                app.world_mut()
                    .resource_mut::<CaptureProgress>()
                    .set_test_pipeline_sample(12, pending, failed);
                app.update();
            }
            let recording = app.world().resource::<BridgeRecording>();
            assert!(!recording.ready);
            assert_eq!(recording.requested, 0);
        }
    }

    #[test]
    fn completed_probe_waits_for_every_capture_callback() {
        let mut app = app();
        app.world_mut()
            .resource_mut::<BridgeCrossingProbeState>()
            .phase = BridgeCrossingProbePhase::Completed;
        {
            let mut recording = app.world_mut().resource_mut::<BridgeRecording>();
            recording.ready = true;
            recording.requested = 3;
            recording.saved = 2;
        }
        app.update();
        assert!(app.world().resource::<Messages<AppExit>>().is_empty());
        app.world_mut().resource_mut::<BridgeRecording>().saved = 3;
        app.update();
        assert!(!app.world().resource::<Messages<AppExit>>().is_empty());
    }

    #[test]
    fn recording_frame_limit_and_watchdog_fail_instead_of_waiting_forever() {
        for timeout in [false, true] {
            let mut app = app();
            {
                let mut recording = app.world_mut().resource_mut::<BridgeRecording>();
                recording.ready = true;
                if timeout {
                    recording.started -= Duration::from_secs(WATCHDOG_SECONDS);
                } else {
                    recording.requested = MAX_CAPTURE_FRAMES;
                }
            }
            app.update();
            assert_eq!(
                app.world().resource::<BridgeCrossingProbeState>().phase,
                BridgeCrossingProbePhase::Failed
            );
            assert!(!app.world().resource::<Messages<AppExit>>().is_empty());
        }
    }
}
