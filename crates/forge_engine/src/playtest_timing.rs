//! Transient observations of actual operations. No automation or authored state.
use bevy::{diagnostic::FrameTimeDiagnosticsPlugin, platform::time::Instant, prelude::*};
use forge_core::FrameDistribution;
use forge_navigation::{ElapsedObservation, ElapsedStatus, TileKey};
use forge_project::authoring::playtest::PlaytestIdentity;
use serde::{Deserialize, Serialize};

pub const MAX_TIMING_SAMPLES: usize = 6_000;
pub const MAX_TIMING_JOBS: usize = 128;
const WARMUP_FRAMES: u32 = 60;

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum TimingPhase {
    GameplayObserve,
    GameplayAdvance,
    NpcDrive,
    NavigationGuard,
    NavigationTick,
    ArchipelagoUpdate,
    NavigationRefresh,
    PhysicsScheduleSpan,
}

impl TimingPhase {
    const ALL: [Self; 8] = [
        Self::GameplayObserve,
        Self::GameplayAdvance,
        Self::NpcDrive,
        Self::NavigationGuard,
        Self::NavigationTick,
        Self::ArchipelagoUpdate,
        Self::NavigationRefresh,
        Self::PhysicsScheduleSpan,
    ];

    const fn parent(self) -> Option<Self> {
        match self {
            Self::GameplayAdvance => Some(Self::GameplayObserve),
            Self::NavigationTick => Some(Self::NpcDrive),
            Self::ArchipelagoUpdate => Some(Self::NavigationTick),
            _ => None,
        }
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum TimingState {
    Idle,
    Warmup,
    Recording,
    Stopped,
    LimitReached,
    Invalidated,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
pub struct DurationSummary {
    pub sample_count: usize,
    pub measured_count: usize,
    pub zero_count: usize,
    pub unavailable_count: usize,
    pub not_requested_count: usize,
    pub absent_count: usize,
    pub below_resolution_count: usize,
    pub mean_ms: Option<f64>,
    pub p95_ms: Option<f64>,
    pub p99_ms: Option<f64>,
    pub max_ms: Option<f64>,
}

impl DurationSummary {
    #[must_use]
    pub fn from_observations(samples: &[ElapsedObservation]) -> Self {
        let mut values: Vec<_> = samples
            .iter()
            .filter(|sample| sample.validate().is_ok())
            .filter_map(|sample| sample.milliseconds)
            .collect();
        values.sort_by(f64::total_cmp);
        let percentile = |percent: usize| {
            values
                .get(
                    values
                        .len()
                        .saturating_mul(percent)
                        .div_ceil(100)
                        .saturating_sub(1),
                )
                .copied()
        };
        Self {
            sample_count: samples.len(),
            measured_count: values.len(),
            zero_count: samples.iter().filter(|sample| sample.raw_zero).count(),
            unavailable_count: samples
                .iter()
                .filter(|sample| sample.status == ElapsedStatus::Unavailable)
                .count(),
            not_requested_count: samples
                .iter()
                .filter(|sample| sample.status == ElapsedStatus::NotRequested)
                .count(),
            absent_count: samples
                .iter()
                .filter(|sample| sample.milliseconds.is_none())
                .count(),
            below_resolution_count: samples
                .iter()
                .filter(|sample| sample.status == ElapsedStatus::BelowResolution)
                .count(),
            mean_ms: (!values.is_empty()).then(|| {
                values.iter().sum::<f64>()
                    / f64::from(u32::try_from(values.len()).unwrap_or(u32::MAX))
            }),
            p95_ms: percentile(95),
            p99_ms: percentile(99),
            max_ms: values.last().copied(),
        }
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
pub struct TimingJobObservation {
    pub capture: u64,
    pub inside_capture: bool,
    pub key: TileKey,
    pub token: u64,
    pub revision: u64,
    pub source_hash: String,
    pub generation: ElapsedObservation,
    pub wrapper: ElapsedObservation,
    /// Existing dispatch-to-publication observation, not provider computation.
    pub build_ms: f64,
    pub outcome: String,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
pub struct TimingPhaseObservation {
    pub phase: TimingPhase,
    pub included_in: Option<TimingPhase>,
    pub summary: DurationSummary,
    pub samples: Option<Vec<ElapsedObservation>>,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
pub struct TimingJobSummary {
    pub generation: DurationSummary,
    pub worker_wrapper: DurationSummary,
    pub dispatch_to_publication: DurationSummary,
}
impl TimingJobSummary {
    fn new(jobs: &[TimingJobObservation]) -> Self {
        Self {
            generation: DurationSummary::from_observations(
                &jobs.iter().map(|job| job.generation).collect::<Vec<_>>(),
            ),
            worker_wrapper: DurationSummary::from_observations(
                &jobs.iter().map(|job| job.wrapper).collect::<Vec<_>>(),
            ),
            dispatch_to_publication: DurationSummary::from_observations(
                &jobs
                    .iter()
                    .map(|job| {
                        ElapsedObservation::measured(job.build_ms, cfg!(target_arch = "wasm32"))
                    })
                    .collect::<Vec<_>>(),
            ),
        }
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
pub struct PlaytestTimingSnapshot {
    pub schema: String,
    pub version: u32,
    pub identity: PlaytestIdentity,
    pub run: u64,
    pub capture: u64,
    pub state: TimingState,
    pub limit: Option<String>,
    pub clock: String,
    pub clock_resolution_ms: Option<f64>,
    pub semantics: String,
    pub provider_source_hash: String,
    pub policy_source_hash: String,
    pub policy_revision: u64,
    pub graphics_hash: Option<forge_core::ContentHash>,
    pub viewport_physical: [u32; 2],
    pub warmup_frames_remaining: u32,
    pub warmup_frames_requested: u32,
    pub first_frame: Option<u32>,
    pub last_frame: Option<u32>,
    pub first_tick: Option<u64>,
    pub last_tick: Option<u64>,
    pub sample_capacity: usize,
    pub job_capacity: usize,
    pub jobs_dropped: usize,
    pub frame_cadence: Option<FrameDistribution>,
    pub frame_summary: DurationSummary,
    pub frame_observations: Option<Vec<ElapsedObservation>>,
    pub frame_samples_ms: Option<Vec<f64>>,
    pub phases: Vec<TimingPhaseObservation>,
    pub jobs: Vec<TimingJobObservation>,
    pub capture_jobs: Vec<TimingJobObservation>,
    pub job_semantics: String,
    pub job_history_summary: TimingJobSummary,
    pub capture_job_summary: TimingJobSummary,
}

/// An optional clock start: disabled observation does not execute clock reads.
pub(crate) struct ElapsedTimer(Option<Instant>);
impl ElapsedTimer {
    pub(crate) fn start(enabled: bool) -> Self {
        Self(enabled.then(Instant::now))
    }
    pub(crate) fn finish(self) -> ElapsedObservation {
        self.0
            .map_or_else(ElapsedObservation::not_requested, |started| {
                ElapsedObservation::measured(
                    started.elapsed().as_secs_f64() * 1_000.0,
                    cfg!(target_arch = "wasm32"),
                )
            })
    }
}

/// Drop records early returns as well as successful observations.
pub(crate) struct TimingScope<'a> {
    timing: Option<&'a mut PlaytestTiming>,
    phase: TimingPhase,
    timer: Option<ElapsedTimer>,
}
impl<'a> TimingScope<'a> {
    pub(crate) fn new(timing: Option<&'a mut PlaytestTiming>, phase: TimingPhase) -> Self {
        let enabled = timing.as_ref().is_some_and(|timing| timing.is_recording());
        Self {
            timing,
            phase,
            timer: Some(ElapsedTimer::start(enabled)),
        }
    }
    pub(crate) fn measure<T>(&mut self, phase: TimingPhase, operation: impl FnOnce() -> T) -> T {
        let timer = ElapsedTimer::start(
            self.timing
                .as_ref()
                .is_some_and(|timing| timing.is_recording()),
        );
        let result = operation();
        if let Some(timing) = self.timing.as_deref_mut() {
            timing.record(phase, timer.finish());
        }
        result
    }
}
impl Drop for TimingScope<'_> {
    fn drop(&mut self) {
        if let (Some(timing), Some(timer)) = (self.timing.as_deref_mut(), self.timer.take()) {
            timing.record(self.phase, timer.finish());
        }
    }
}

struct TimingContext {
    graphics: forge_core::ContentHash,
    viewport: [u32; 2],
    policy_hash: String,
    policy_revision: u64,
}

#[derive(Resource)]
pub struct PlaytestTiming {
    identity: PlaytestIdentity,
    run: u64,
    capture: u64,
    state: TimingState,
    limit: Option<String>,
    graphics_hash: Option<forge_core::ContentHash>,
    viewport: [u32; 2],
    policy_hash: String,
    policy_revision: u64,
    next_context: Option<TimingContext>,
    warmup: u32,
    first_frame: Option<u32>,
    last_frame: Option<u32>,
    first_tick: Option<u64>,
    last_tick: Option<u64>,
    phases: [Vec<ElapsedObservation>; 8],
    frames: Vec<f64>,
    frame_observations: Vec<ElapsedObservation>,
    jobs: Vec<TimingJobObservation>,
    jobs_dropped: usize,
    physics: Option<ElapsedTimer>,
}

impl PlaytestTiming {
    #[must_use]
    pub fn new(identity: PlaytestIdentity, run: u64) -> Self {
        Self {
            identity,
            run,
            capture: 0,
            state: TimingState::Idle,
            limit: None,
            graphics_hash: None,
            viewport: [0; 2],
            policy_hash: String::new(),
            policy_revision: 0,
            next_context: None,
            warmup: 0,
            first_frame: None,
            last_frame: None,
            first_tick: None,
            last_tick: None,
            phases: std::array::from_fn(|_| Vec::new()),
            frames: Vec::new(),
            frame_observations: Vec::new(),
            jobs: Vec::new(),
            jobs_dropped: 0,
            physics: None,
        }
    }
    #[must_use]
    pub const fn is_recording(&self) -> bool {
        matches!(self.state, TimingState::Recording)
    }
    #[must_use]
    pub fn capture(&self) -> Option<u64> {
        self.is_recording().then_some(self.capture)
    }
    #[must_use]
    pub const fn state(&self) -> TimingState {
        self.state
    }
    #[must_use]
    pub const fn identity(&self) -> PlaytestIdentity {
        self.identity
    }
    #[must_use]
    pub const fn run(&self) -> u64 {
        self.run
    }
    pub fn start(&mut self) {
        let Some(next) = self.capture.checked_add(1) else {
            self.invalidate("capture sequence overflow");
            return;
        };
        if let Some(context) = self.next_context.take() {
            self.graphics_hash = Some(context.graphics);
            self.viewport = context.viewport;
            self.policy_hash = context.policy_hash;
            self.policy_revision = context.policy_revision;
        }
        self.capture = next;
        self.state = TimingState::Warmup;
        self.limit = None;
        self.warmup = WARMUP_FRAMES;
        self.first_frame = None;
        self.last_frame = None;
        self.first_tick = None;
        self.last_tick = None;
        for samples in &mut self.phases {
            samples.clear();
            samples.reserve(MAX_TIMING_SAMPLES);
        }
        self.frames.clear();
        self.frame_observations.clear();
        self.frames.reserve(MAX_TIMING_SAMPLES);
        self.frame_observations.reserve(MAX_TIMING_SAMPLES);
        self.jobs.reserve(MAX_TIMING_JOBS);
        self.physics = None;
    }
    pub fn stop(&mut self) {
        if matches!(self.state, TimingState::Warmup | TimingState::Recording) {
            self.state = TimingState::Stopped;
        }
        self.physics = None;
    }
    fn invalidate(&mut self, message: &str) {
        self.state = TimingState::Invalidated;
        self.limit = Some(message.into());
        self.physics = None;
    }
    pub fn invalidate_if_active(&mut self, message: &str) {
        if matches!(
            self.state,
            TimingState::Warmup
                | TimingState::Recording
                | TimingState::Stopped
                | TimingState::LimitReached
        ) {
            self.invalidate(message);
        }
    }
    pub fn context(
        &mut self,
        source: forge_core::ContentHash,
        graphics: forge_core::ContentHash,
        viewport: [u32; 2],
        policy_hash: &str,
        policy_revision: u64,
    ) {
        if source != self.identity.source_hash {
            self.invalidate("playtest source changed");
            return;
        }
        let changed = self.graphics_hash != Some(graphics)
            || self.viewport != viewport
            || self.policy_hash != policy_hash
            || self.policy_revision != policy_revision;
        if matches!(
            self.state,
            TimingState::Stopped | TimingState::LimitReached | TimingState::Invalidated
        ) {
            self.stage_context(graphics, viewport, policy_hash, policy_revision);
            if changed {
                self.invalidate("context changed after collection closed; captured pins retained");
            }
            return;
        }
        if matches!(self.state, TimingState::Warmup | TimingState::Recording) && changed {
            self.stage_context(graphics, viewport, policy_hash, policy_revision);
            self.invalidate(
                "graphics, physical viewport or navigation policy changed during collection",
            );
            return;
        }
        self.graphics_hash = Some(graphics);
        self.viewport = viewport;
        if self.policy_hash != policy_hash {
            self.policy_hash.clear();
            self.policy_hash.push_str(policy_hash);
        }
        self.policy_revision = policy_revision;
    }
    fn stage_context(
        &mut self,
        graphics: forge_core::ContentHash,
        viewport: [u32; 2],
        policy_hash: &str,
        policy_revision: u64,
    ) {
        if self.next_context.as_ref().is_none_or(|context| {
            context.graphics != graphics
                || context.viewport != viewport
                || context.policy_hash != policy_hash
                || context.policy_revision != policy_revision
        }) {
            self.next_context = Some(TimingContext {
                graphics,
                viewport,
                policy_hash: policy_hash.into(),
                policy_revision,
            });
        }
    }
    pub fn note_tick(&mut self, tick: u64) {
        if self.is_recording() {
            self.first_tick.get_or_insert(tick);
            self.last_tick = Some(tick);
        }
    }
    pub(crate) fn record(&mut self, phase: TimingPhase, elapsed: ElapsedObservation) {
        if !self.is_recording() {
            return;
        }
        if elapsed.validate().is_err() {
            self.invalidate("invalid timer observation");
            return;
        }
        let samples = &mut self.phases[phase as usize];
        if samples.len() == MAX_TIMING_SAMPLES {
            self.state = TimingState::LimitReached;
            self.limit = Some("bounded scope sample capacity reached".into());
        } else {
            samples.push(elapsed);
        }
    }
    pub fn absorb_navigation(
        &mut self,
        samples: Vec<(TimingPhase, ElapsedObservation)>,
        jobs: Vec<TimingJobObservation>,
    ) {
        for (phase, elapsed) in samples {
            self.record(phase, elapsed);
        }
        for mut job in jobs {
            job.inside_capture &= self.capture() == Some(job.capture);
            if !job.build_ms.is_finite()
                || job.build_ms < 0.0
                || job.generation.validate().is_err()
                || job.wrapper.validate().is_err()
            {
                self.invalidate("invalid job timer observation");
                break;
            }
            if self.jobs.len() == MAX_TIMING_JOBS {
                self.jobs.remove(0);
                self.jobs_dropped = self.jobs_dropped.saturating_add(1);
            }
            self.jobs.push(job);
        }
    }
    fn frame(&mut self, frame: u32, milliseconds: f64) {
        if self.state == TimingState::Warmup {
            self.warmup = self.warmup.saturating_sub(1);
            if self.warmup == 0 {
                self.state = TimingState::Recording;
            }
            return;
        }
        if !self.is_recording() {
            return;
        }
        self.first_frame.get_or_insert(frame);
        self.last_frame = Some(frame);
        let elapsed = ElapsedObservation::measured(milliseconds, cfg!(target_arch = "wasm32"));
        self.frame_observations.push(elapsed);
        if milliseconds.is_finite() && milliseconds >= 0.0 {
            self.frames.push(milliseconds);
        }
        if self.frame_observations.len() == MAX_TIMING_SAMPLES {
            self.state = TimingState::LimitReached;
            self.limit = Some("bounded frame sample capacity reached".into());
        }
    }
    #[must_use]
    pub fn snapshot(&self) -> PlaytestTimingSnapshot {
        let complete = !matches!(
            self.state,
            TimingState::Recording | TimingState::Warmup | TimingState::Idle
        );
        let capture_jobs: Vec<_> = self
            .jobs
            .iter()
            .filter(|job| job.inside_capture && job.capture == self.capture)
            .cloned()
            .collect();
        PlaytestTimingSnapshot {
            schema: "forge.playtest-timing-observation".into(), version: 1, identity: self.identity,
            run: self.run, capture: self.capture, state: self.state, limit: self.limit.clone(),
            clock: if cfg!(target_arch = "wasm32") { "bevy_web_time_performance" } else { "bevy_native_instant" }.into(),
            clock_resolution_ms: None,
            semantics: "elapsed wall time, not exclusive OS CPU; physics span includes scheduler work; nested scopes are not additive; frame cadence includes observation overhead and is Bevy loop timing, not presented frames; warmup is 60 loop frames, not an attested 10 seconds; clock resolution, GPU total and effective High unavailable".into(),
            provider_source_hash: forge_navigation::PROVIDER_SOURCE_SHA256.into(),
            policy_source_hash: self.policy_hash.clone(), policy_revision: self.policy_revision, graphics_hash: self.graphics_hash,
            viewport_physical: self.viewport, warmup_frames_remaining: self.warmup, warmup_frames_requested: WARMUP_FRAMES,
            first_frame: self.first_frame, last_frame: self.last_frame,
            first_tick: self.first_tick, last_tick: self.last_tick,
            sample_capacity: MAX_TIMING_SAMPLES, job_capacity: MAX_TIMING_JOBS, jobs_dropped: self.jobs_dropped,
            frame_cadence: if self.frames.len() == self.frame_observations.len() { FrameDistribution::from_samples_ms(&self.frames).ok() } else { None },
            frame_summary: DurationSummary::from_observations(&self.frame_observations),
            frame_observations: complete.then(|| self.frame_observations.clone()),
            frame_samples_ms: complete.then(|| self.frames.clone()),
            phases: TimingPhase::ALL.into_iter().map(|phase| {
                let samples = &self.phases[phase as usize];
                TimingPhaseObservation { phase, included_in: phase.parent(), summary: DurationSummary::from_observations(samples),
                    samples: complete.then(|| samples.clone()) }
            }).collect(),
            jobs: self.jobs.clone(),
            job_history_summary: TimingJobSummary::new(&self.jobs),
            capture_job_summary: TimingJobSummary::new(&capture_jobs),
            capture_jobs,
            job_semantics: "jobs are bounded history of published session results; capture=0 is outside a measurement window; capture_jobs require dispatch and publication in the same active capture; generation is provider computation; worker wrapper includes parse/generate/serialize after worker initialization; failed or terminated worker computation duration unavailable; build_ms remains dispatch-to-publication latency; clocks from distinct origins are never subtracted".into(),
        }
    }
}

pub struct PlaytestTimingPlugin;
impl Plugin for PlaytestTimingPlugin {
    fn build(&self, app: &mut App) {
        use bevy_rapier3d::prelude::PhysicsSet;
        if !app.is_plugin_added::<FrameTimeDiagnosticsPlugin>() {
            app.add_plugins(FrameTimeDiagnosticsPlugin::default());
        }
        app.add_systems(
            Last,
            |time: Res<Time<Real>>,
             frame: Res<bevy::diagnostic::FrameCount>,
             mut timing: ResMut<PlaytestTiming>| {
                timing.frame(frame.0, time.delta_secs_f64() * 1_000.0);
            },
        )
        .add_systems(
            FixedUpdate,
            (|mut timing: ResMut<PlaytestTiming>| {
                timing.physics = Some(ElapsedTimer::start(timing.is_recording()));
            })
            .after(crate::PlayerControllerSet::Drive)
            .after(crate::navigation::NavigationSet::Guard)
            .after(crate::gameplay::GameplaySet::NpcDrive)
            .before(PhysicsSet::SyncBackend),
        )
        .add_systems(
            FixedUpdate,
            (|mut timing: ResMut<PlaytestTiming>| {
                if let Some(timer) = timing.physics.take() {
                    timing.record(TimingPhase::PhysicsScheduleSpan, timer.finish());
                }
            })
            .after(PhysicsSet::Writeback)
            .before(crate::PlayerControllerSet::Observe)
            .before(crate::gameplay::GameplaySet::Observe),
        );
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    fn timing() -> PlaytestTiming {
        PlaytestTiming::new(
            PlaytestIdentity {
                source_hash: forge_core::bytes_sha256(b"source"),
                terrain_hash: forge_core::bytes_sha256(b"terrain"),
                vegetation_hash: None,
            },
            7,
        )
    }
    #[test]
    fn duration_statistics_keep_zero_and_absence_without_fps() {
        let summary = DurationSummary::from_observations(&[
            ElapsedObservation::measured(0.0, false),
            ElapsedObservation::measured(2.0, false),
            ElapsedObservation::measured(0.0, true),
            ElapsedObservation::not_requested(),
        ]);
        assert_eq!(summary.measured_count, 2);
        assert_eq!(summary.zero_count, 2);
        assert_eq!(summary.unavailable_count, 0);
        assert_eq!(summary.not_requested_count, 1);
        assert_eq!(summary.absent_count, 2);
        assert_eq!(summary.mean_ms, Some(1.0));
        assert_eq!(summary.p95_ms, Some(2.0));
        assert_eq!(DurationSummary::from_observations(&[]).mean_ms, None);
    }
    #[test]
    fn warmup_stop_capacity_and_source_changes_are_explicit() {
        let mut timing = timing();
        timing.start();
        for frame in 0..WARMUP_FRAMES {
            timing.frame(frame, 16.0);
        }
        assert!(timing.is_recording());
        for _ in 0..MAX_TIMING_SAMPLES {
            timing.record(
                TimingPhase::GameplayAdvance,
                ElapsedObservation::measured(0.0, false),
            );
        }
        timing.record(
            TimingPhase::GameplayAdvance,
            ElapsedObservation::measured(1.0, false),
        );
        assert_eq!(timing.state, TimingState::LimitReached);
        assert_eq!(
            timing.phases[TimingPhase::GameplayAdvance as usize].len(),
            MAX_TIMING_SAMPLES
        );
        timing.start();
        timing.stop();
        assert_eq!(timing.state, TimingState::Stopped);
        timing.context(
            forge_core::bytes_sha256(b"other"),
            forge_core::bytes_sha256(b"graphics"),
            [0; 2],
            "",
            0,
        );
        assert_eq!(timing.state, TimingState::Invalidated);
    }
    #[test]
    fn snapshot_is_reopenable_and_nested_scopes_are_not_totals() {
        let mut timing = timing();
        timing.start();
        timing.warmup = 0;
        timing.state = TimingState::Recording;
        timing.record(
            TimingPhase::GameplayObserve,
            ElapsedObservation::measured(4.0, false),
        );
        timing.record(
            TimingPhase::GameplayAdvance,
            ElapsedObservation::measured(1.0, false),
        );
        timing.frame(61, 16.0);
        timing.note_tick(42);
        timing.stop();
        let snapshot = timing.snapshot();
        let json = serde_json::to_value(snapshot).unwrap();
        assert_eq!(json["phases"][1]["included_in"], "gameplay_observe");
        assert_eq!(json["phases"][1]["samples"][0]["milliseconds"], 1.0);
        assert_eq!(json["last_tick"], 42);
        assert!(json["clock_resolution_ms"].is_null());
        assert!(json.get("gpu_total_ms").is_none());
    }
    #[test]
    fn restart_retains_session_jobs_and_rejects_active_window_claims() {
        let mut timing = timing();
        let job = |capture, inside_capture| TimingJobObservation {
            capture,
            inside_capture,
            key: TileKey { x: 0, z: 0 },
            token: 5,
            revision: 2,
            source_hash: "geometry".into(),
            generation: ElapsedObservation::measured(3.0, false),
            wrapper: ElapsedObservation::not_requested(),
            build_ms: 7.0,
            outcome: "accepted".into(),
        };
        timing.absorb_navigation(Vec::new(), vec![job(0, false)]);
        timing.start();
        timing.state = TimingState::Recording;
        timing.absorb_navigation(Vec::new(), vec![job(1, true), job(0, false)]);
        timing.stop();
        assert_eq!(timing.snapshot().jobs.len(), 3);
        assert_eq!(timing.snapshot().capture_jobs.len(), 1);
        timing.start();
        assert_eq!(timing.snapshot().jobs.len(), 3);
        assert!(timing.snapshot().capture_jobs.is_empty());
        for token in 0..MAX_TIMING_JOBS {
            let mut observation = job(0, false);
            observation.token = u64::try_from(token).unwrap();
            timing.absorb_navigation(Vec::new(), vec![observation]);
        }
        assert_eq!(timing.snapshot().jobs.len(), MAX_TIMING_JOBS);
        assert_eq!(timing.snapshot().jobs_dropped, 3);
    }
    #[test]
    fn full_samples_are_stopped_only_and_context_changes_invalidate() {
        let mut timing = timing();
        let source = timing.identity.source_hash;
        timing.context(
            source,
            forge_core::bytes_sha256(b"graphics"),
            [1920, 1080],
            "policy",
            1,
        );
        timing.start();
        timing.state = TimingState::Recording;
        timing.frame(80, 0.0);
        timing.note_tick(100);
        assert!(timing.snapshot().frame_observations.is_none());
        assert!(timing.snapshot().frame_cadence.is_none());
        assert_eq!(timing.snapshot().frame_summary.zero_count, 1);
        timing.context(
            source,
            forge_core::bytes_sha256(b"graphics"),
            [1920, 1080],
            "policy",
            1,
        );
        assert_eq!(timing.state(), TimingState::Recording);
        timing.context(
            source,
            forge_core::bytes_sha256(b"graphics"),
            [1920, 1080],
            "policy",
            2,
        );
        assert_eq!(timing.state(), TimingState::Invalidated);
        let serialized = serde_json::to_vec(&timing.snapshot()).unwrap();
        let reopened: PlaytestTimingSnapshot = serde_json::from_slice(&serialized).unwrap();
        assert_eq!(reopened, timing.snapshot());
        assert_eq!(
            reopened.frame_observations.unwrap()[0].milliseconds,
            if cfg!(target_arch = "wasm32") {
                None
            } else {
                Some(0.0)
            }
        );
        timing.start();
        timing.context(
            source,
            forge_core::bytes_sha256(b"graphics"),
            [1280, 720],
            "policy",
            1,
        );
        assert_eq!(timing.state(), TimingState::Invalidated);
    }
    #[test]
    fn scopes_execute_once_and_record_on_early_drop_without_changing_result() {
        let mut timing = timing();
        timing.start();
        timing.state = TimingState::Recording;
        let mut executions = 0;
        {
            let mut scope = TimingScope::new(Some(&mut timing), TimingPhase::GameplayObserve);
            let result = scope.measure(TimingPhase::GameplayAdvance, || {
                executions += 1;
                Err::<(), _>("unchanged")
            });
            assert_eq!(result, Err("unchanged"));
        }
        assert_eq!(executions, 1);
        assert_eq!(timing.phases[0].len(), 1);
        assert_eq!(timing.phases[1].len(), 1);
        assert!(timing.phases[0][0].validate().is_ok());
    }
    #[test]
    fn late_job_is_history_only_and_profile_failure_preserves_closed_samples() {
        let mut timing = timing();
        timing.start();
        timing.state = TimingState::Recording;
        timing.record(
            TimingPhase::GameplayAdvance,
            ElapsedObservation::measured(1.0, false),
        );
        timing.stop();
        timing.invalidate_if_active("effective profile changed");
        timing.absorb_navigation(
            Vec::new(),
            vec![TimingJobObservation {
                capture: 1,
                inside_capture: true,
                key: TileKey { x: 1, z: 2 },
                token: 8,
                revision: 3,
                source_hash: "geometry".into(),
                generation: ElapsedObservation::measured(2.0, false),
                wrapper: ElapsedObservation::not_requested(),
                build_ms: 9.0,
                outcome: "accepted".into(),
            }],
        );
        let snapshot = timing.snapshot();
        assert_eq!(snapshot.state, TimingState::Invalidated);
        assert_eq!(snapshot.phases[1].samples.as_ref().unwrap().len(), 1);
        assert_eq!(snapshot.jobs.len(), 1);
        assert!(!snapshot.jobs[0].inside_capture);
        assert!(snapshot.capture_jobs.is_empty());
        timing.state = TimingState::LimitReached;
        timing.invalidate_if_active("fingerprint unavailable");
        assert_eq!(timing.state(), TimingState::Invalidated);
    }
    #[test]
    fn closed_context_pins_remain_attached_to_samples_and_restart_adopts_current() {
        let mut timing = timing();
        let source = timing.identity.source_hash;
        let first = forge_core::bytes_sha256(b"first graphics");
        let second = forge_core::bytes_sha256(b"second graphics");
        timing.context(source, first, [1920, 1080], "first policy", 1);
        timing.start();
        timing.state = TimingState::Recording;
        timing.record(
            TimingPhase::GameplayAdvance,
            ElapsedObservation::measured(2.0, false),
        );
        timing.stop();
        timing.context(source, second, [1280, 720], "second policy", 2);
        let closed = timing.snapshot();
        assert_eq!(closed.state, TimingState::Invalidated);
        assert_eq!(closed.graphics_hash, Some(first));
        assert_eq!(closed.viewport_physical, [1920, 1080]);
        assert_eq!(closed.policy_source_hash, "first policy");
        assert_eq!(closed.policy_revision, 1);
        assert_eq!(
            closed.phases[1].samples.as_ref().unwrap()[0].milliseconds,
            Some(2.0)
        );
        timing.start();
        timing.context(source, second, [1280, 720], "second policy", 2);
        let restarted = timing.snapshot();
        assert_eq!(restarted.state, TimingState::Warmup);
        assert_eq!(restarted.capture, 2);
        assert_eq!(restarted.graphics_hash, Some(second));
        assert_eq!(restarted.viewport_physical, [1280, 720]);
        assert_eq!(restarted.policy_source_hash, "second policy");
        assert_eq!(restarted.policy_revision, 2);
        assert_eq!(restarted.phases[1].summary.sample_count, 0);
    }
}
