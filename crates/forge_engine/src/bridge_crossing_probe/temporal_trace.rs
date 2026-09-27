//! Opt-in bounded CPU transport trace, paired with real viewport/frame captures.
//! It deliberately does not claim GPU pixel coverage or realtime performance.
use super::{
    BridgeCrossingProbeConfiguration, BridgeCrossingProbePhase, BridgeCrossingProbeState,
    drive_bridge_crossing_probe,
};
use crate::{
    PendingPlayerControlCommand, PlayerControlCommand, PlayerControllerSet,
    temporal_lod::render::Groups,
};
use bevy::prelude::*;
use serde::Serialize;

const HOLD_SECONDS: f64 = 0.6;
const SAMPLE_INTERVAL_SECONDS: f64 = 0.02;
const MAX_SAMPLES: usize = 64;
const MAX_GROUPS: usize = 8;

/// Qualification-only transport observations; not a GPU execution receipt.
#[derive(Clone, Debug, Default, Resource, Serialize)]
pub struct BridgeTemporalLodStatus {
    pub phase: String,
    pub selected_slots: Vec<u32>,
    pub samples: Vec<TemporalLodObservation>,
    pub cpu_settled_while_stopped: bool,
    pub failure: Option<String>,
}

#[derive(Clone, Debug, Serialize)]
pub struct TemporalLodObservation {
    pub elapsed_seconds: f64,
    pub recording_frame: Option<u32>,
    pub camera_position: [f32; 3],
    pub groups: Vec<TemporalLodGroupObservation>,
}

#[derive(Clone, Debug, Serialize)]
pub struct TemporalLodGroupObservation {
    pub slot: u32,
    pub active: bool,
    pub codes: [i32; 4],
    pub transition_active: bool,
}

#[derive(Default, Resource)]
struct TraceClock {
    started: Option<f64>,
    last_sample: Option<f64>,
    stop_sent: bool,
    resume_sent: bool,
}

pub(super) fn install(app: &mut App) {
    app.insert_resource(BridgeTemporalLodStatus {
        phase: "waiting_for_moving_transition".into(),
        ..default()
    })
    .init_resource::<TraceClock>()
    .add_systems(
        FixedUpdate,
        hold_walk
            .after(drive_bridge_crossing_probe)
            .before(PlayerControllerSet::Drive),
    )
    .add_systems(Last, observe.after(super::recording::record_frame));
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn hold_walk(
    status: Res<BridgeTemporalLodStatus>,
    mut clock: ResMut<TraceClock>,
    configuration: Res<BridgeCrossingProbeConfiguration>,
    probe: Res<BridgeCrossingProbeState>,
    mut pending: ResMut<PendingPlayerControlCommand>,
) {
    if probe.phase != BridgeCrossingProbePhase::Crossing {
        return;
    }
    let command = if status.phase == "holding" && !clock.stop_sent {
        clock.stop_sent = true;
        Some(PlayerControlCommand::default())
    } else if clock.stop_sent && status.phase != "holding" && !clock.resume_sent {
        clock.resume_sent = true;
        Some(PlayerControlCommand {
            move_axis: configuration.move_axis,
            ..default()
        })
    } else {
        None
    };
    if let Some(command) = command {
        // The sequence remains monotonic; the original walk was already acknowledged
        // before starting this diagnostic pause.
        if let Err(error) = pending.submit(command) {
            tracing::error!(event_kind = "temporal_lod_probe_command_failed", %error);
        }
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn observe(
    time: Res<Time>,
    groups: Option<Res<Groups>>,
    probe: Res<BridgeCrossingProbeState>,
    cameras: Query<(&Camera, &GlobalTransform), With<Camera3d>>,
    mut status: ResMut<BridgeTemporalLodStatus>,
    mut clock: ResMut<TraceClock>,
    recording: Option<Res<super::recording::BridgeRecording>>,
) {
    if matches!(status.phase.as_str(), "completed" | "failed") {
        return;
    }
    let Some(groups) = groups else {
        fail(&mut status, "temporal LOD renderer absent");
        return;
    };
    let mut active = cameras.iter().filter(|(camera, _)| camera.is_active);
    let Some((_, camera)) = active.next() else {
        return;
    };
    if active.next().is_some() {
        fail(&mut status, "multiple active cameras");
        return;
    }
    let now = time.elapsed_secs_f64();
    if clock.started.is_none() {
        if probe.phase.is_terminal() {
            fail(
                &mut status,
                "no moving temporal transition observed before itinerary ended",
            );
            return;
        }
        if probe.phase != BridgeCrossingProbePhase::Crossing || !probe.walk_sequence_applied {
            return;
        }
        status.selected_slots = groups
            .diagnostic()
            .into_iter()
            .filter(|group| {
                group.active && group.codes.iter().any(|code| (1..16).contains(&code.abs()))
            })
            .take(MAX_GROUPS)
            .map(|group| group.slot)
            .collect();
        if status.selected_slots.is_empty() {
            return;
        }
        clock.started = Some(now);
        status.phase = "holding".into();
        tracing::info!(event_kind = "temporal_lod_probe_started",
            selected_slots = ?status.selected_slots, hold_seconds = HOLD_SECONDS,
            evidence = "cpu_transport_not_gpu_pixel_coverage");
    }
    let elapsed = now - clock.started.unwrap_or(now);
    if should_sample(clock.last_sample, elapsed) || elapsed >= HOLD_SECONDS {
        if status.samples.len() >= MAX_SAMPLES {
            fail(&mut status, "bounded temporal trace exceeded 64 samples");
            return;
        }
        let observations = status
            .selected_slots
            .iter()
            .filter_map(|slot| {
                groups
                    .diagnostic_slot(*slot)
                    .map(
                        |(active, codes, transition_active)| TemporalLodGroupObservation {
                            slot: *slot,
                            active,
                            codes,
                            transition_active,
                        },
                    )
            })
            .collect::<Vec<_>>();
        if observations.len() != status.selected_slots.len() {
            fail(&mut status, "selected temporal group disappeared");
            return;
        }
        let sample = TemporalLodObservation {
            elapsed_seconds: elapsed,
            recording_frame: recording
                .as_deref()
                .and_then(super::recording::BridgeRecording::requested_frame),
            camera_position: camera.translation().to_array(),
            groups: observations,
        };
        tracing::info!(event_kind = "temporal_lod_probe_sample",
            sample = %serde_json::json!(&sample), evidence = "cpu_transport_at_frame_end");
        status.samples.push(sample);
        clock.last_sample = Some(elapsed);
    }
    if elapsed >= HOLD_SECONDS {
        status.cpu_settled_while_stopped = settled(&status.samples);
        if status.cpu_settled_while_stopped {
            status.phase = "completed".into();
        } else {
            fail(
                &mut status,
                "camera or temporal coverage did not settle during bounded pause",
            );
        }
        tracing::info!(event_kind = "temporal_lod_probe_completed",
            status = %serde_json::json!(&*status), gpu_pixel_coverage_verified = false,
            performance_qualified = false);
    }
}

fn should_sample(previous: Option<f64>, elapsed: f64) -> bool {
    previous.is_none_or(|previous| elapsed - previous >= SAMPLE_INTERVAL_SECONDS)
}

fn settled(samples: &[TemporalLodObservation]) -> bool {
    let stable = samples
        .iter()
        .filter(|sample| sample.elapsed_seconds >= 0.3)
        .collect::<Vec<_>>();
    let Some(first) = stable.first() else {
        return false;
    };
    stable.len() >= 3
        && stable
            .last()
            .is_some_and(|sample| sample.elapsed_seconds >= HOLD_SECONDS)
        && stable.iter().all(|sample| {
            Vec3::from_array(sample.camera_position)
                .distance(Vec3::from_array(first.camera_position))
                <= 0.002
                && !sample.groups.is_empty()
                && sample.groups.iter().all(|group| {
                    group.active
                        && !group.transition_active
                        && group
                            .codes
                            .iter()
                            .all(|code| *code == 0 || code.abs() == 16)
                })
                && sample
                    .groups
                    .iter()
                    .zip(&first.groups)
                    .all(|(group, original)| {
                        group.slot == original.slot && group.codes == original.codes
                    })
        })
}

fn fail(status: &mut BridgeTemporalLodStatus, message: &str) {
    status.phase = "failed".into();
    status.failure = Some(message.into());
    tracing::warn!(event_kind = "temporal_lod_probe_failed", reason = message);
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn sampling_is_time_bounded_not_render_frame_bounded() {
        assert!(should_sample(None, 0.0));
        assert!(!should_sample(Some(0.0), 0.001));
        assert!(should_sample(Some(0.0), 0.021));
    }
    #[test]
    fn stable_codes_require_camera_stop_and_real_observation_interval() {
        let mut samples = [0.3, 0.4, 0.6].map(|elapsed_seconds| TemporalLodObservation {
            elapsed_seconds,
            recording_frame: None,
            camera_position: [1.0, 2.0, 3.0],
            groups: vec![TemporalLodGroupObservation {
                slot: 1,
                active: true,
                codes: [16, 0, 16, 16],
                transition_active: false,
            }],
        });
        assert!(settled(&samples));
        samples[2].camera_position[0] += 0.1;
        assert!(!settled(&samples));
        samples[2].camera_position[0] = 1.0;
        samples[2].groups[0].codes = [8, -8, 16, 16];
        samples[2].groups[0].transition_active = true;
        assert!(!settled(&samples));
    }
}
