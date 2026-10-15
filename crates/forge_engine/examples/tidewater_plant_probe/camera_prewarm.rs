//! Review candidate: observed yaw preparation before admission, never a timing capture.
use super::{AdmissionContext, AdmissionWindow, Perf, Sample};
use bevy::{diagnostic::FrameCount, prelude::*};
use forge_engine::{
    ForgePlayerCamera, PlayerRuntimeSnapshot, PlayerSimulationReady,
    gameplay::GameplayRuntime,
    playtest_timing::{PlaytestTiming, TimingState},
};
use forge_project::authoring::playtest::PlaytestIdentity;
use serde::Serialize;
use std::{io::Write, time::Instant};

const DEADLINE_S: f64 = 120.0;
const MAX_SAMPLES: usize = 4096;
const STEPS: u16 = 16;
const REQUIRED_MASK: u32 = (1 << (STEPS + 1)) - 1;
const YAW_UNITS: f32 = 540.0;
const YAW_TOLERANCE_RAD: f32 = 0.002;
const ROTATION_TOLERANCE_RAD: f32 = 0.005;
const TRANSLATION_TOLERANCE_M: f32 = 0.05;
const ANCHOR_TRANSLATION_TOLERANCE_M: f32 = 0.005;
// 16 outbound + 16 inbound steps have a nominal two-second minimum.
const MIN_STEP_SECONDS: f64 = 1.0 / 16.0;

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize)]
#[serde(rename_all = "snake_case")]
enum Stage {
    Waiting,
    Outbound,
    Inbound,
    Returned,
    Recorded,
    Failed,
}

#[derive(Clone, Debug, Serialize)]
struct Pose {
    camera_translation_m: [f32; 3],
    camera_rotation_xyzw: [f32; 4],
    camera_yaw_radians: f32,
    rig_yaw_radians: f32,
    rig_pitch_radians: f32,
    camera_mode: String,
    player_position_m: [f32; 3],
}
impl Pose {
    fn rotation(&self) -> Quat {
        Quat::from_array(self.camera_rotation_xyzw)
    }
    fn close_to(&self, other: &Self, translation_tolerance: f32) -> bool {
        self.camera_mode == other.camera_mode
            && angle(self.rig_pitch_radians - other.rig_pitch_radians).abs() <= YAW_TOLERANCE_RAD
            && self.rotation().angle_between(other.rotation()) <= ROTATION_TOLERANCE_RAD
            && Vec3::from_array(self.camera_translation_m)
                .distance(Vec3::from_array(other.camera_translation_m))
                <= translation_tolerance
            && Vec3::from_array(self.player_position_m)
                .distance(Vec3::from_array(other.player_position_m))
                <= translation_tolerance
    }
}

#[derive(Clone, Debug, Serialize)]
struct Observation {
    frame: u32,
    gameplay_tick: u64,
    fixed_tick: u64,
    last_applied_sequence: Option<u64>,
    elapsed_s: f64,
    wall_ms: f64,
    stage: Stage,
    waypoint: u16,
    target_units: f32,
    emitted_motion_delta_units: f32,
    player_ready: bool,
    gameplay_ready: bool,
    timing_state: TimingState,
    timing_capture: u64,
    epoch_started: bool,
    camera_samples: usize,
    render_samples: usize,
    pose: Pose,
}

pub(super) struct Prewarm {
    installed: Instant,
    stage: Stage,
    identity: Option<PlaytestIdentity>,
    context: Option<AdmissionContext>,
    initial_window: AdmissionWindow,
    anchor: Option<(Pose, u64)>,
    origin: Option<Pose>,
    begin: Option<Observation>,
    end: Option<Observation>,
    samples: Vec<Observation>,
    waypoint: u16,
    outbound_mask: u32,
    inbound_mask: u32,
    last_command_tick: Option<u64>,
    waypoint_started: Instant,
    pending_motion: Option<f32>,
    emitted_motion: f32,
    failure: Option<String>,
}
impl Prewarm {
    pub(super) fn requested() -> Option<Self> {
        (std::env::var("FORGE_CAMERA_PERF_PREWARM").as_deref() == Ok("1")).then(|| {
            let now = Instant::now();
            Self {
                installed: now,
                stage: Stage::Waiting,
                identity: None,
                context: None,
                initial_window: AdmissionWindow::default(),
                anchor: None,
                origin: None,
                begin: None,
                end: None,
                samples: Vec::new(),
                waypoint: 0,
                outbound_mask: 0,
                inbound_mask: 0,
                last_command_tick: None,
                waypoint_started: now,
                pending_motion: None,
                emitted_motion: 0.0,
                failure: None,
            }
        })
    }

    pub(super) fn allows_admission(&self) -> bool {
        matches!(self.stage, Stage::Returned | Stage::Recorded)
    }

    pub(super) fn observe_context(&mut self, context: Option<&AdmissionContext>) {
        if self.origin.is_none() {
            self.context = context.cloned();
        } else if self.context.as_ref() != context {
            self.fail("preparation context changed after pose anchoring".into());
        }
    }

    pub(super) fn take_motion(&mut self) -> f32 {
        self.emitted_motion = self.pending_motion.take().unwrap_or(0.0);
        self.emitted_motion
    }

    fn fail(&mut self, reason: String) {
        if self.failure.is_none() {
            self.failure = Some(reason);
        }
        self.stage = Stage::Failed;
        self.pending_motion = None;
    }

    fn target_units(&self) -> f32 {
        let point = if self.waypoint <= STEPS {
            self.waypoint
        } else {
            2 * STEPS - self.waypoint
        };
        f32::from(point) / f32::from(STEPS) * YAW_UNITS
    }

    fn update_pose(&mut self, now: Instant, sample: &Observation, grounded: bool) {
        if self.stage == Stage::Waiting {
            self.wait_for_anchor(now, sample, grounded);
            return;
        }
        if self.stage == Stage::Returned {
            if !self
                .origin
                .as_ref()
                .is_some_and(|origin| sample.pose.close_to(origin, TRANSLATION_TOLERANCE_M))
            {
                self.fail("observed return pose lost before capture start".into());
            }
            return;
        }
        if !matches!(self.stage, Stage::Outbound | Stage::Inbound) {
            return;
        }
        let origin = self.origin.as_ref().expect("anchored preparation");
        if sample.pose.camera_mode != origin.camera_mode
            || angle(sample.pose.rig_pitch_radians - origin.rig_pitch_radians).abs()
                > YAW_TOLERANCE_RAD
        {
            self.fail("camera mode or pitch changed during yaw preparation".into());
            return;
        }
        if !sample.player_ready
            || !sample.gameplay_ready
            || self.last_command_tick == Some(sample.fixed_tick)
        {
            return;
        }
        let expected_yaw = angle(origin.camera_yaw_radians - self.target_units() * gain());
        let observed = angle(sample.pose.camera_yaw_radians - expected_yaw).abs()
            <= YAW_TOLERANCE_RAD
            && angle(sample.pose.rig_yaw_radians - sample.pose.camera_yaw_radians).abs()
                <= YAW_TOLERANCE_RAD;
        if observed && now.duration_since(self.waypoint_started).as_secs_f64() >= MIN_STEP_SECONDS {
            let point = if self.waypoint <= STEPS {
                self.outbound_mask |= 1 << self.waypoint;
                self.waypoint
            } else {
                2 * STEPS - self.waypoint
            };
            if self.waypoint >= STEPS {
                self.inbound_mask |= 1 << point;
            }
            if self.waypoint == 2 * STEPS {
                if self.outbound_mask == REQUIRED_MASK
                    && self.inbound_mask == REQUIRED_MASK
                    && sample.pose.close_to(origin, TRANSLATION_TOLERANCE_M)
                {
                    self.stage = Stage::Returned;
                } else {
                    self.fail(
                        "yaw endpoints observed but full coverage or return pose missing".into(),
                    );
                }
                return;
            }
            self.waypoint += 1;
            self.stage = if self.waypoint <= STEPS {
                Stage::Outbound
            } else {
                Stage::Inbound
            };
            self.waypoint_started = now;
        }
        let origin_yaw = self
            .origin
            .as_ref()
            .expect("anchored preparation")
            .camera_yaw_radians;
        let target_yaw = angle(origin_yaw - self.target_units() * gain());
        let delta = -angle(target_yaw - sample.pose.camera_yaw_radians) / gain();
        if delta.abs() > f32::EPSILON {
            self.pending_motion = Some(delta.clamp(-YAW_UNITS, YAW_UNITS));
            self.last_command_tick = Some(sample.fixed_tick);
        }
    }

    fn wait_for_anchor(&mut self, now: Instant, sample: &Observation, grounded: bool) {
        let eligible = sample.player_ready
            && sample.gameplay_ready
            && grounded
            && sample.fixed_tick > 0
            && sample.frame >= 240
            && self.context.is_some();
        if !eligible {
            self.initial_window.observe(now, sample.frame, false, None);
            self.anchor = None;
            return;
        }
        if !self
            .anchor
            .as_ref()
            .is_some_and(|(pose, _)| sample.pose.close_to(pose, ANCHOR_TRANSLATION_TOLERANCE_M))
        {
            self.initial_window.observe(now, sample.frame, false, None);
            self.anchor = Some((sample.pose.clone(), sample.fixed_tick));
        }
        self.initial_window
            .observe(now, sample.frame, true, self.context.clone());
        if self.initial_window.completed
            && self
                .anchor
                .as_ref()
                .is_some_and(|(_, tick)| sample.fixed_tick > *tick)
        {
            self.origin = Some(sample.pose.clone());
            self.outbound_mask = 1;
            self.waypoint = 1;
            self.stage = Stage::Outbound;
            self.waypoint_started = now;
            // Only a real MouseMotion command; no pose/rig is assigned here.
            self.pending_motion = Some(YAW_UNITS / f32::from(STEPS));
            self.last_command_tick = Some(sample.fixed_tick);
        }
    }
}

fn gain() -> f32 {
    // Same configured profile conversion as the controller, no second engine API.
    super::super::CAMERA_LOOK_RADIANS_PER_UNIT
        .to_degrees()
        .to_radians()
}

fn angle(value: f32) -> f32 {
    (value + std::f32::consts::PI).rem_euclid(std::f32::consts::TAU) - std::f32::consts::PI
}

fn observation(world: &mut World, prewarm: &Prewarm) -> Result<Observation, String> {
    let player = world
        .get_resource::<PlayerRuntimeSnapshot>()
        .ok_or("preparation requires player snapshot")?
        .clone();
    let runtime = world
        .get_resource::<GameplayRuntime>()
        .ok_or("preparation requires native gameplay runtime")?;
    let gameplay_tick = runtime.session().state().tick;
    let gameplay_ready = runtime.is_ready();
    let timing = world
        .get_resource::<PlaytestTiming>()
        .ok_or("preparation requires native gameplay timing")?
        .snapshot();
    let transform = *world
        .query_filtered::<&Transform, With<ForgePlayerCamera>>()
        .single(world)
        .map_err(|error| error.to_string())?;
    let forward = transform.rotation * Vec3::NEG_Z;
    let pose = Pose {
        camera_translation_m: transform.translation.to_array(),
        camera_rotation_xyzw: transform.rotation.to_array(),
        camera_yaw_radians: (-forward.x).atan2(-forward.z),
        rig_yaw_radians: player.camera_yaw_radians,
        rig_pitch_radians: player.camera_pitch_radians,
        camera_mode: format!("{:?}", player.camera_mode),
        player_position_m: player.player_position.to_array(),
    };
    if !transform.translation.is_finite()
        || !transform.rotation.is_finite()
        || !transform.scale.is_finite()
        || !player.player_position.is_finite()
        || !pose.rig_yaw_radians.is_finite()
        || !pose.rig_pitch_radians.is_finite()
    {
        return Err("nonfinite observed preparation pose".into());
    }
    let perf = world.resource::<Perf>();
    let render_samples = world
        .resource::<Sample>()
        .records
        .lock()
        .map_err(|error| error.to_string())?
        .len();
    Ok(Observation {
        frame: world.resource::<FrameCount>().0,
        gameplay_tick,
        fixed_tick: player.fixed_tick,
        last_applied_sequence: player.last_applied_sequence,
        elapsed_s: prewarm.installed.elapsed().as_secs_f64(),
        wall_ms: perf.wall_ms,
        stage: prewarm.stage,
        waypoint: prewarm.waypoint,
        target_units: prewarm.target_units(),
        emitted_motion_delta_units: prewarm.emitted_motion,
        player_ready: world.resource::<PlayerSimulationReady>().0,
        gameplay_ready,
        timing_state: timing.state,
        timing_capture: timing.capture,
        epoch_started: perf.epoch.is_some(),
        camera_samples: perf.frames.len(),
        render_samples,
        pose,
    })
}

pub(super) fn observe(world: &mut World) {
    let Some(mut prewarm) = world.resource_mut::<Perf>().prewarm.take() else {
        return;
    };
    if !matches!(prewarm.stage, Stage::Recorded | Stage::Failed) {
        match observation(world, &prewarm) {
            Ok(sample) => {
                if sample.timing_state != TimingState::Idle
                    || sample.timing_capture != 0
                    || sample.epoch_started
                    || sample.camera_samples != 0
                    || sample.render_samples != 0
                {
                    prewarm.fail("preparation crossed into an active measurement".into());
                } else if sample.elapsed_s >= DEADLINE_S {
                    prewarm.fail("preparation deadline exceeded".into());
                } else if prewarm.samples.len() >= MAX_SAMPLES {
                    prewarm.fail("preparation sample capacity reached".into());
                } else {
                    if prewarm.identity.is_none() {
                        prewarm.identity =
                            Some(world.resource::<PlaytestTiming>().snapshot().identity);
                    }
                    let grounded = world.resource::<PlayerRuntimeSnapshot>().grounded;
                    prewarm.begin.get_or_insert_with(|| sample.clone());
                    prewarm.update_pose(Instant::now(), &sample, grounded);
                    prewarm.samples.push(sample);
                }
            }
            Err(error) => prewarm.fail(error),
        }
    }
    world.resource_mut::<Perf>().prewarm = Some(prewarm);
}

fn write_trace(world: &World, prewarm: &Prewarm) -> Result<(), String> {
    let receipt = serde_json::json!({
        "schema": "forge.camera-prewarm.v1", "version": 1,
        "status": if prewarm.failure.is_some() { "failed" } else { "prepared" },
        "process_id": std::process::id(), "identity": prewarm.identity, "context": prewarm.context,
        "limits": { "seconds": DEADLINE_S, "max_samples": MAX_SAMPLES,
            "waypoints_per_direction": STEPS + 1, "yaw_units": YAW_UNITS,
            "look_gain_radians_per_unit": gain(),
            "yaw_tolerance_rad": YAW_TOLERANCE_RAD,
            "rotation_tolerance_rad": ROTATION_TOLERANCE_RAD,
            "translation_tolerance_m": TRANSLATION_TOLERANCE_M,
            "anchor_translation_tolerance_m": ANCHOR_TRANSLATION_TOLERANCE_M,
            "minimum_step_seconds": MIN_STEP_SECONDS },
        "begin": prewarm.begin, "end": prewarm.end, "origin": prewarm.origin,
        "coverage": { "outbound_mask": prewarm.outbound_mask, "inbound_mask": prewarm.inbound_mask,
            "required_mask": REQUIRED_MASK },
        "samples": prewarm.samples, "failure": prewarm.failure,
        "metric_qualified": false, "gate_qualified": false,
        "semantics": "Preparation includes startup settling, observed yaw first visits and final admission; frame samples before timing start only. Fixed ticks order player poses; gameplay_tick is the real GameState.tick. Navigation jobs may outlive preparation. Coverage attests 17 observed yaw waypoints per direction, not every possible GPU specialization. No readiness override or active capture reset."
    });
    let path = world
        .resource::<crate::Probe>()
        .screenshot
        .with_extension("prewarm.json");
    let bytes = serde_json::to_vec_pretty(&receipt).map_err(|error| error.to_string())?;
    let mut file = std::fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(path)
        .map_err(|error| error.to_string())?;
    file.write_all(&bytes).map_err(|error| error.to_string())
}

pub(super) fn record_before_capture(world: &mut World) -> bool {
    let Some(mut prewarm) = world.resource_mut::<Perf>().prewarm.take() else {
        return true;
    };
    let allowed = if prewarm.stage == Stage::Returned {
        match observation(world, &prewarm) {
            Ok(end)
                if end.timing_state == TimingState::Idle
                    && end.timing_capture == 0
                    && !end.epoch_started
                    && end.camera_samples == 0
                    && end.render_samples == 0
                    && end.elapsed_s < DEADLINE_S
                    && prewarm.samples.len() <= MAX_SAMPLES
                    && end.player_ready
                    && end.gameplay_ready
                    && prewarm.outbound_mask == REQUIRED_MASK
                    && prewarm.inbound_mask == REQUIRED_MASK
                    && prewarm.origin.as_ref().is_some_and(|origin| {
                        end.pose.close_to(origin, TRANSLATION_TOLERANCE_M)
                    }) =>
            {
                prewarm.end = Some(end);
                match write_trace(world, &prewarm) {
                    Ok(()) => {
                        prewarm.stage = Stage::Recorded;
                        true
                    }
                    Err(error) => {
                        prewarm.fail(format!("preparation trace write failed: {error}"));
                        false
                    }
                }
            }
            Ok(_) => {
                prewarm.fail("final preparation boundary or return pose invalid".into());
                false
            }
            Err(error) => {
                prewarm.fail(error);
                false
            }
        }
    } else {
        false
    };
    world.resource_mut::<Perf>().prewarm = Some(prewarm);
    allowed
}

pub(super) fn close_failed(world: &mut World) -> bool {
    let Some(mut prewarm) = world.resource_mut::<Perf>().prewarm.take() else {
        return false;
    };
    let failed = prewarm.stage == Stage::Failed;
    if failed {
        prewarm.end = observation(world, &prewarm).ok();
        let saved = write_trace(world, &prewarm);
        error!(target: "forge::gameplay", failure = ?prewarm.failure, ?saved,
            "native camera preparation failed; no measurement admitted");
        world.write_message(bevy::app::AppExit::error());
    }
    world.resource_mut::<Perf>().prewarm = Some(prewarm);
    failed
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn same_fixed_tick_cannot_queue_another_feedback_packet() {
        // The command high-water mark is shared by the outbound/inbound state, not a render clock.
        let mut prewarm = Prewarm::requested_for_test();
        let sample = observation_for_test();
        prewarm.origin = Some(sample.pose.clone());
        prewarm.stage = Stage::Outbound;
        prewarm.waypoint = 1;
        prewarm.last_command_tick = Some(sample.fixed_tick);
        prewarm.update_pose(Instant::now(), &sample, true);
        assert!(prewarm.pending_motion.is_none());
        assert_eq!(prewarm.waypoint, 1);
    }

    #[test]
    fn commanded_targets_cannot_admit_without_observed_coverage_and_return() {
        let mut prewarm = Prewarm::requested_for_test();
        let mut sample = observation_for_test();
        prewarm.origin = Some(sample.pose.clone());
        prewarm.stage = Stage::Inbound;
        prewarm.waypoint = 2 * STEPS;
        prewarm.waypoint_started = Instant::now() - std::time::Duration::from_secs(1);
        prewarm.update_pose(Instant::now(), &sample, true);
        assert_eq!(prewarm.stage, Stage::Failed);
        assert!(!prewarm.allows_admission());
        let mut returned = Prewarm::requested_for_test();
        returned.origin = Some(sample.pose.clone());
        returned.stage = Stage::Inbound;
        returned.waypoint = 2 * STEPS;
        returned.outbound_mask = REQUIRED_MASK;
        returned.inbound_mask = REQUIRED_MASK;
        returned.waypoint_started = Instant::now() - std::time::Duration::from_secs(1);
        sample.pose.camera_translation_m[0] += TRANSLATION_TOLERANCE_M * 2.0;
        returned.update_pose(Instant::now(), &sample, true);
        assert_eq!(returned.stage, Stage::Failed);
        assert!(!returned.allows_admission());
        let mut covered = Prewarm::requested_for_test();
        covered.origin = Some(sample.pose.clone());
        covered.stage = Stage::Inbound;
        covered.waypoint = 2 * STEPS;
        covered.outbound_mask = REQUIRED_MASK;
        covered.inbound_mask = REQUIRED_MASK;
        covered.waypoint_started = Instant::now() - std::time::Duration::from_secs(1);
        covered.update_pose(Instant::now(), &sample, true);
        assert_eq!(covered.stage, Stage::Returned);
        assert!(covered.allows_admission());
    }

    impl Prewarm {
        fn requested_for_test() -> Self {
            let now = Instant::now();
            Self {
                installed: now,
                stage: Stage::Waiting,
                identity: None,
                context: None,
                initial_window: AdmissionWindow::default(),
                anchor: None,
                origin: None,
                begin: None,
                end: None,
                samples: Vec::new(),
                waypoint: 0,
                outbound_mask: 0,
                inbound_mask: 0,
                last_command_tick: None,
                waypoint_started: now,
                pending_motion: None,
                emitted_motion: 0.0,
                failure: None,
            }
        }
    }
    fn observation_for_test() -> Observation {
        Observation {
            frame: 300,
            gameplay_tick: 42,
            fixed_tick: 51,
            last_applied_sequence: Some(1),
            elapsed_s: 2.0,
            wall_ms: 16.0,
            stage: Stage::Waiting,
            waypoint: 0,
            target_units: 0.0,
            emitted_motion_delta_units: 0.0,
            player_ready: true,
            gameplay_ready: true,
            timing_state: TimingState::Idle,
            timing_capture: 0,
            epoch_started: false,
            camera_samples: 0,
            render_samples: 0,
            pose: Pose {
                camera_translation_m: [0.0; 3],
                camera_rotation_xyzw: [0.0, 0.0, 0.0, 1.0],
                camera_yaw_radians: 0.0,
                rig_yaw_radians: 0.0,
                rig_pitch_radians: 0.0,
                camera_mode: "FirstPerson".into(),
                player_position_m: [0.0; 3],
            },
        }
    }
}
