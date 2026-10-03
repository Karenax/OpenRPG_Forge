//! Source-compatible live delta clock without a Bevy dependency.
use std::sync::{Arc, Mutex};

/// One offered source-frame transaction shared by FFT, shore, spray and materials.
#[derive(Clone, Copy, Debug, PartialEq)]
pub struct LiveStep {
    pub step: u32,
    pub time: f64,
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
        let delta = ((elapsed - previous) * self.time_scale).min(0.1);
        if delta == 0.0 {
            return;
        }
        self.previous_elapsed = Some(elapsed);
        #[allow(clippy::cast_possible_truncation)]
        let dt = delta as f32;
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
        if !scale.is_finite() || scale <= 0.0 {
            return Err("live ocean time scale must be finite and positive");
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
    pub fn offer(&self, elapsed: f64) {
        self.0
            .lock()
            .expect("live ocean clock mutex")
            .offer(elapsed);
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
    /// The final simulation delta remains capped at 100 ms. A pending transaction
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
        timeline.acknowledge(121);
        timeline.offer(1.01);
        let second = timeline.pending(122).expect("following frame");
        assert!((second.dt - 0.0125).abs() < 0.000_001);
    }

    #[test]
    fn time_scale_rejects_invalid_values_and_an_undrained_transaction() {
        let clock = LiveClock::default();
        let before = clock.snapshot();
        for invalid in [0.0, -1.0, f64::NAN, f64::INFINITY, f64::NEG_INFINITY] {
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
