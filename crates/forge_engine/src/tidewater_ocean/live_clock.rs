//! Source-compatible live delta clock without a Bevy dependency.
use std::sync::{Arc, Mutex};

/// One offered source-frame transaction shared by FFT, shore, spray and materials.
#[derive(Clone, Copy, Debug, PartialEq)]
pub struct LiveStep {
    pub step: u32,
    pub time: f64,
    /// Unscaled application delta, as packed by App.js into the global Frame.
    /// Wave time can advance faster without changing the foam integration delta.
    pub dt: f32,
}

/// Observable state used to hold deterministic rendered comparisons.
#[derive(Clone, Copy, Debug, PartialEq)]
pub struct Snapshot {
    pub paused: bool,
    pub quiescent: bool,
    pub time_scale: f64,
    pub next_step: u32,
    pub simulation_time: f64,
    pub pending: Option<LiveStep>,
    pub completed: Option<LiveStep>,
}

/// CPU-only source-frame clock. Its mutex owner publishes complete triples only.
#[derive(Clone, Debug)]
struct LiveTimeline {
    next: u32,
    time: f64,
    previous_elapsed: Option<f64>,
    pending: Option<LiveStep>,
    completed: Option<LiveStep>,
    paused: bool,
    time_scale: f64,
}

impl Default for LiveTimeline {
    fn default() -> Self {
        Self {
            next: 121,
            time: 2.0,
            previous_elapsed: None,
            pending: None,
            completed: None,
            paused: false,
            time_scale: 1.0,
        }
    }
}

impl LiveTimeline {
    fn offer(&mut self, elapsed: f64) {
        if self.paused || self.pending.is_some() || !elapsed.is_finite() || elapsed < 0.0 {
            return;
        }
        let Some(previous) = self.previous_elapsed else {
            self.previous_elapsed = Some(elapsed);
            return;
        };
        if elapsed < previous {
            return;
        }
        // Engine.js caps App.frame's delta before OceanFFT applies timeScale.
        // Capping after scaling suppresses the requested wave speed on slow frames.
        let source_delta = (elapsed - previous).min(0.1);
        let delta = source_delta * self.time_scale;
        if delta == 0.0 {
            return;
        }
        self.previous_elapsed = Some(elapsed);
        #[allow(clippy::cast_possible_truncation)]
        let dt = source_delta as f32;
        self.pending = Some(LiveStep {
            step: self.next,
            time: self.time + delta,
            dt,
        });
    }

    fn pending(&self, step: u32) -> Option<LiveStep> {
        self.pending.filter(|frame| frame.step == step)
    }

    #[allow(dead_code)] // The combined scene publishes this pose to visible materials.
    fn completed(&self, step: u32) -> Option<LiveStep> {
        self.completed.filter(|frame| frame.step == step)
    }

    fn acknowledge(&mut self, step: u32) {
        let frame = self
            .pending
            .filter(|frame| frame.step == step)
            .expect("live step");
        let next = step.checked_add(1).expect("live frame counter exhausted");
        self.pending = None;
        self.next = next;
        self.time = frame.time;
        self.completed = Some(frame);
    }

    fn pause(&mut self) {
        self.paused = true;
    }

    fn set_time_scale(&mut self, scale: f64) -> Result<(), &'static str> {
        if !scale.is_finite() || scale <= 0.0 || 0.1 * scale > f64::from(f32::MAX) {
            return Err("live ocean time scale must be finite, positive and GPU representable");
        }
        if self.pending.is_some() {
            return Err("live ocean time scale cannot change while a transaction is pending");
        }
        self.time_scale = scale;
        Ok(())
    }

    fn resume_at(&mut self, elapsed: f64) -> Result<(), &'static str> {
        if !elapsed.is_finite() || elapsed < 0.0 {
            return Err("live ocean resume time must be finite and non-negative");
        }
        if self.pending.is_some() {
            return Err("live ocean clock cannot resume while a transaction is pending");
        }
        self.previous_elapsed = Some(elapsed);
        self.paused = false;
        Ok(())
    }

    fn snapshot(&self) -> Snapshot {
        Snapshot {
            paused: self.paused,
            quiescent: self.paused && self.pending.is_none(),
            time_scale: self.time_scale,
            next_step: self.next,
            simulation_time: self.time,
            pending: self.pending,
            completed: self.completed,
        }
    }
}

/// Thread-safe owner shared between Bevy's main and render worlds.
#[derive(Clone)]
pub struct LiveClock(Arc<Mutex<LiveTimeline>>);

impl Default for LiveClock {
    fn default() -> Self {
        Self(Arc::new(Mutex::new(LiveTimeline::default())))
    }
}

impl LiveClock {
    /// Resume after the validated acquired sequence, preserving its exact time.
    pub fn from_bootstrap(frames: u32, time: f64) -> Result<Self, &'static str> {
        if !(1..=256).contains(&frames)
            || !time.is_finite()
            || time < 0.0
            || time > f64::from(f32::MAX)
        {
            return Err("invalid acquired ocean clock");
        }
        Ok(Self(Arc::new(Mutex::new(LiveTimeline {
            next: frames + 1,
            time,
            ..LiveTimeline::default()
        }))))
    }
    pub fn offer(&self, elapsed: f64) {
        self.0
            .lock()
            .expect("live ocean clock mutex")
            .offer(elapsed);
    }

    /// Publish only when the renderer has encoded the final acquired FFT step.
    /// Construction alone must not expose a completed GPU transaction.
    pub(crate) fn publish_bootstrap(&self, frame: LiveStep) -> Result<(), &'static str> {
        let mut timeline = self.0.lock().expect("live ocean clock mutex");
        if timeline.completed.is_some()
            || timeline.pending.is_some()
            || frame.step.checked_add(1) != Some(timeline.next)
            || frame.time.to_bits() != timeline.time.to_bits()
            || frame.dt.to_bits() != (1.0_f32 / 60.0).to_bits()
        {
            return Err("acquired ocean completion differs from validated bootstrap");
        }
        timeline.completed = Some(frame);
        Ok(())
    }

    #[must_use]
    pub fn pending(&self, step: u32) -> Option<LiveStep> {
        self.0.lock().expect("live ocean clock mutex").pending(step)
    }

    #[allow(dead_code)] // The standalone compute probe has no visible materials.
    #[must_use]
    pub fn completed(&self, step: u32) -> Option<LiveStep> {
        self.0
            .lock()
            .expect("live ocean clock mutex")
            .completed(step)
    }

    pub fn acknowledge(&self, step: u32) {
        self.0
            .lock()
            .expect("live ocean clock mutex")
            .acknowledge(step);
    }

    /// Sets the simulation seconds advanced per real second for future transactions.
    ///
    /// The source delta is capped at 100 ms before scaling. A pending transaction
    /// must drain before changing scale so its timestamp retains one clear contract.
    pub fn set_time_scale(&self, scale: f64) -> Result<(), &'static str> {
        self.0
            .lock()
            .expect("live ocean clock mutex")
            .set_time_scale(scale)
    }

    /// Stops accepting new source-frame transactions.
    ///
    /// A transaction already offered remains pending so the render graph can
    /// finish and acknowledge it. Callers can wait for `snapshot().quiescent`
    /// before recording deterministic evidence.
    pub fn pause(&self) {
        self.0.lock().expect("live ocean clock mutex").pause();
    }

    /// Resumes after a quiescent pause and anchors the next delta to `elapsed`.
    ///
    /// Rebasing prevents the time spent capturing from becoming a clamped
    /// 100 ms simulation jump on the first resumed frame.
    pub fn resume_at(&self, elapsed: f64) -> Result<(), &'static str> {
        self.0
            .lock()
            .expect("live ocean clock mutex")
            .resume_at(elapsed)
    }

    /// Returns one coherent view of the shared main/render clock state.
    #[must_use]
    pub fn snapshot(&self) -> Snapshot {
        self.0.lock().expect("live ocean clock mutex").snapshot()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn acquired_bootstrap_resumes_without_losing_preparation_frames() {
        let mut time = 0.0;
        for _ in 0..124 {
            time += 1.0 / 60.0;
        }
        let clock = LiveClock::from_bootstrap(124, time).unwrap();
        clock.offer(10.0);
        clock.offer(10.0 + 1.0 / 60.0);
        let step = clock.pending(125).unwrap();
        assert_eq!(step.step, 125);
        assert!((step.time - (time + 1.0 / 60.0)).abs() < 1e-12);
        for (count, value) in [
            (0, time),
            (257, time),
            (124, f64::NAN),
            (124, f64::INFINITY),
            (124, -1.0),
        ] {
            assert!(LiveClock::from_bootstrap(count, value).is_err());
        }
    }

    #[test]
    fn acquired_frame_is_visible_only_after_final_bootstrap_encoding() {
        let mut time = 0.0;
        for _ in 0..124 {
            time += 1.0 / 60.0;
        }
        let clock = LiveClock::from_bootstrap(124, time).unwrap();
        clock.pause();
        assert_eq!(clock.completed(124), None);
        let acquired = LiveStep {
            step: 124,
            time,
            dt: 1.0 / 60.0,
        };
        for invalid in [
            LiveStep {
                step: 123,
                ..acquired
            },
            LiveStep {
                time: 2.0,
                ..acquired
            },
            LiveStep {
                dt: 0.0,
                ..acquired
            },
        ] {
            assert!(clock.publish_bootstrap(invalid).is_err());
            assert_eq!(clock.completed(124), None);
        }
        clock.publish_bootstrap(acquired).unwrap();
        assert_eq!(clock.completed(124), Some(acquired));
        assert!(clock.snapshot().quiescent);
        assert!(clock.publish_bootstrap(acquired).is_err());
        clock.resume_at(10.0).unwrap();
        clock.offer(10.0 + 1.0 / 60.0);
        assert_eq!(clock.completed(124), Some(acquired));
        let next = clock.pending(125).unwrap();
        clock.acknowledge(125);
        assert_eq!(clock.completed(125), Some(next));
        assert_eq!(clock.completed(124), None);
    }

    fn advance_for_one_second(fps: u32) -> LiveTimeline {
        let mut timeline = LiveTimeline::default();
        timeline.offer(0.0);
        for frame in 1..=fps {
            timeline.offer(f64::from(frame) / f64::from(fps));
            let step = timeline.pending.expect("offered step").step;
            timeline.acknowledge(step);
        }
        timeline
    }

    #[test]
    fn source_time_tracks_one_second_at_common_render_rates() {
        for fps in [30, 60, 144] {
            let timeline = advance_for_one_second(fps);
            assert!((timeline.time - 3.0).abs() < 0.000_002, "{fps} FPS");
            assert_eq!(timeline.next, 121 + fps);
            assert_eq!(timeline.snapshot().time_scale.to_bits(), 1.0_f64.to_bits());
        }
    }

    #[test]
    fn product_scales_track_real_time_at_common_render_rates() {
        for (scale, expected) in [(1.25, 3.25), (1.5, 3.5)] {
            for fps in [30, 60, 144] {
                let mut timeline = LiveTimeline::default();
                timeline.set_time_scale(scale).expect("valid product scale");
                timeline.offer(0.0);
                for frame in 1..=fps {
                    timeline.offer(f64::from(frame) / f64::from(fps));
                    let step = timeline.pending.expect("offered step").step;
                    timeline.acknowledge(step);
                }
                assert!((timeline.time - expected).abs() < 0.000_002, "{fps} FPS");
                assert_eq!(timeline.next, 121 + fps);
                assert_eq!(timeline.snapshot().time_scale.to_bits(), scale.to_bits());
            }
        }
    }

    #[test]
    fn pending_transaction_retains_elapsed_delta_until_it_is_acknowledged() {
        let mut timeline = LiveTimeline::default();
        timeline.offer(0.0);
        timeline.offer(1.0 / 60.0);
        timeline.offer(0.5);
        timeline.acknowledge(121);
        timeline.offer(0.6);
        let frame = timeline.pending(122).expect("next pending frame");
        assert_eq!(frame.dt.to_bits(), 0.1_f32.to_bits());
        assert!((frame.time - (2.0 + 1.0 / 60.0 + 0.1)).abs() < 0.000_002);
    }

    #[test]
    fn long_stall_is_clamped_without_a_catch_up_loop() {
        let mut timeline = LiveTimeline::default();
        timeline.set_time_scale(1.25).expect("valid product scale");
        timeline.offer(0.0);
        timeline.offer(1.0);
        let first = timeline.pending(121).expect("clamped frame");
        assert_eq!(first.dt.to_bits(), 0.1_f32.to_bits());
        assert!((first.time - 2.125).abs() < 0.000_001);
        timeline.acknowledge(121);
        timeline.offer(1.01);
        let second = timeline.pending(122).expect("following frame");
        assert!((second.dt - 0.01).abs() < 0.000_001);
        assert!((second.time - 2.1375).abs() < 0.000_001);
    }

    #[test]
    fn source_global_delta_is_independent_of_wave_time_scale() {
        for scale in [0.75, 1.0, 1.25, 1.5] {
            let mut timeline = LiveTimeline::default();
            timeline.set_time_scale(scale).unwrap();
            timeline.offer(0.0);
            let mut elapsed = 0.0_f64;
            let mut source_wave_time = 2.0;
            for delta in [1.0_f64 / 60.0, 0.025, 0.25] {
                elapsed += delta;
                // App._frame packs the capped application dt; OceanFFT.update
                // separately adds dt * timeScale to OceanParams.time.
                let source_dt = delta.min(0.1);
                source_wave_time += source_dt * scale;
                timeline.offer(elapsed);
                let frame = timeline.pending.unwrap();
                #[allow(clippy::cast_possible_truncation)]
                let expected_global_dt = source_dt as f32;
                assert_eq!(frame.dt.to_bits(), expected_global_dt.to_bits());
                assert!((frame.time - source_wave_time).abs() < 0.000_001);
                timeline.acknowledge(frame.step);
            }
        }
    }

    #[test]
    fn time_scale_rejects_invalid_values_and_an_undrained_transaction() {
        let clock = LiveClock::default();
        let before = clock.snapshot();
        for invalid in [
            0.0,
            -1.0,
            f64::NAN,
            f64::INFINITY,
            f64::NEG_INFINITY,
            f64::MAX,
        ] {
            assert!(clock.set_time_scale(invalid).is_err());
            assert_eq!(clock.snapshot(), before);
        }

        clock.offer(0.0);
        clock.offer(1.0 / 60.0);
        assert!(clock.set_time_scale(1.25).is_err());
        assert_eq!(clock.snapshot().time_scale.to_bits(), 1.0_f64.to_bits());
    }

    #[test]
    fn invalid_or_backwards_elapsed_values_preserve_state() {
        let mut timeline = LiveTimeline::default();
        timeline.offer(2.0);
        let before = (
            timeline.next,
            timeline.time.to_bits(),
            timeline.previous_elapsed.map(f64::to_bits),
            timeline.pending,
        );
        timeline.offer(f64::NAN);
        timeline.offer(f64::INFINITY);
        timeline.offer(1.0);
        let after = (
            timeline.next,
            timeline.time.to_bits(),
            timeline.previous_elapsed.map(f64::to_bits),
            timeline.pending,
        );
        assert_eq!(after, before);
    }

    #[test]
    fn pause_blocks_new_offers_while_the_pending_transaction_drains() {
        let clock = LiveClock::default();
        clock.offer(0.0);
        clock.offer(1.0 / 60.0);
        let pending = clock.snapshot().pending.expect("offered transaction");

        clock.pause();
        clock.offer(10.0);
        let draining = clock.snapshot();
        assert!(draining.paused);
        assert!(!draining.quiescent);
        assert_eq!(draining.pending, Some(pending));

        clock.acknowledge(pending.step);
        clock.offer(11.0);
        let held = clock.snapshot();
        assert!(held.quiescent);
        assert_eq!(held.completed, Some(pending));
        assert_eq!(held.pending, None);
        assert_eq!(held.next_step, pending.step + 1);
    }

    #[test]
    fn resume_rebases_elapsed_time_without_a_capture_gap_jump() {
        let clock = LiveClock::default();
        clock.offer(0.0);
        clock.offer(0.02);
        clock.acknowledge(121);
        let before = clock.snapshot().completed.expect("completed frame");

        clock.pause();
        assert!(clock.snapshot().quiescent);
        clock.resume_at(30.0).expect("quiescent clock");
        clock.offer(30.0);
        assert_eq!(clock.snapshot().pending, None);
        clock.offer(30.02);

        let resumed = clock.snapshot();
        let pending = resumed.pending.expect("rebased frame");
        assert!(!resumed.paused);
        assert!(!resumed.quiescent);
        assert_eq!(pending.step, 122);
        assert!((pending.dt - 0.02).abs() < 0.000_001);
        assert!((pending.time - (before.time + 0.02)).abs() < 0.000_001);
    }

    #[test]
    fn resume_refuses_invalid_time_or_an_undrained_transaction() {
        let clock = LiveClock::default();
        clock.offer(0.0);
        clock.offer(1.0 / 60.0);
        clock.pause();

        assert!(clock.resume_at(5.0).is_err());
        assert!(clock.snapshot().paused);
        clock.acknowledge(121);
        assert!(clock.resume_at(f64::NAN).is_err());
        assert!(clock.resume_at(-1.0).is_err());
        assert!(clock.snapshot().quiescent);
    }
}
