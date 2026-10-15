//! Imported constant single-burst emission, independent of GPU allocation.
//!
//! This profile implements one finite loop with no prewarm or inter-loop delays.
//! Other source profiles require their own admitted translation. The importer
//! retains graph provenance separately; no source name or path is built in here.
use serde::{Deserialize, Serialize};

#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceSingleBurstLoopParameters {
    pub loop_duration_seconds: f32,
    pub loop_count: u32,
    pub burst_delay_seconds: f32,
    pub burst_count: f32,
    pub burst_active: bool,
    pub initial_play: bool,
    pub prewarm_steps: u32,
}

impl SourceSingleBurstLoopParameters {
    pub fn validate(self) -> Result<(), &'static str> {
        if !self.loop_duration_seconds.is_finite()
            || self.loop_duration_seconds <= 0.0
            || !self.burst_delay_seconds.is_finite()
            || self.burst_delay_seconds < 0.0
            || !self.burst_count.is_finite()
            || self.burst_count < 0.0
        {
            return Err("Invalid constant single-burst source parameters");
        }
        if self.loop_count != 1 || self.prewarm_steps != 0 {
            return Err("Source emission profile requires one finite loop without prewarm");
        }
        Ok(())
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum SourceEmissionEvent {
    Play,
    Stop,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum SourceEmissionLoopState {
    Finished,
    Looping,
}

#[derive(Clone, Copy, Debug, PartialEq, Serialize)]
pub struct SourceEmissionSnapshot {
    pub effect_time_seconds: f32,
    pub loop_total_time_seconds: f32,
    pub loop_duration_seconds: f32,
    pub loop_count: u32,
    pub loop_index: u32,
    pub loop_state: SourceEmissionLoopState,
    pub spawn_count: f32,
    pub new_loop: bool,
}

impl SourceEmissionSnapshot {
    const EMPTY: Self = Self {
        effect_time_seconds: 0.0,
        loop_total_time_seconds: 0.0,
        loop_duration_seconds: 0.0,
        loop_count: 0,
        loop_index: 0,
        loop_state: SourceEmissionLoopState::Finished,
        spawn_count: 0.0,
        new_loop: false,
    };

    #[must_use]
    pub const fn playing(self) -> bool {
        matches!(self.loop_state, SourceEmissionLoopState::Looping)
    }
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct SourceEmissionAdvance {
    pub reset_particles: bool,
    /// Emission intent; a full GPU pool may reject every requested particle.
    /// It remains separate from the final spawn counter after loop termination.
    pub spawn_request: f32,
    pub delta_seconds: f32,
    pub snapshot: SourceEmissionSnapshot,
}

#[derive(Clone, Debug)]
pub struct SourceSingleBurstLoop {
    parameters: SourceSingleBurstLoopParameters,
    snapshot: SourceEmissionSnapshot,
    events: Vec<SourceEmissionEvent>,
    emitted: bool,
    reset_pending: bool,
}

impl SourceSingleBurstLoop {
    pub fn new(parameters: SourceSingleBurstLoopParameters) -> Result<Self, &'static str> {
        parameters.validate()?;
        let mut clock = Self {
            parameters,
            snapshot: SourceEmissionSnapshot::EMPTY,
            events: Vec::new(),
            emitted: false,
            reset_pending: false,
        };
        clock.reinitialize();
        Ok(clock)
    }

    pub fn queue_event(&mut self, event: SourceEmissionEvent) {
        self.events.push(event);
    }

    /// Reinitialization resets the owner clock and cancels older queued events.
    /// GPU reset/allocation is requested by the next accepted advance.
    pub fn reinitialize(&mut self) {
        self.snapshot = SourceEmissionSnapshot::EMPTY;
        self.events.clear();
        if self.parameters.initial_play {
            self.events.push(SourceEmissionEvent::Play);
        }
        self.emitted = false;
        self.reset_pending = true;
    }

    #[must_use]
    pub const fn snapshot(&self) -> SourceEmissionSnapshot {
        self.snapshot
    }

    /// Call on a candidate clone while GPU phases are pending. Commit that clone
    /// only after the whole ordered group is encoded. Invalid time consumes no
    /// event and changes no clock. Particle state is never read back here.
    pub fn advance(&mut self, delta_seconds: f32) -> Result<SourceEmissionAdvance, &'static str> {
        if !delta_seconds.is_finite() || delta_seconds < 0.0 {
            return Err("Invalid source emission delta");
        }
        let mut state = self.snapshot;
        let mut emitted = self.emitted;
        state.new_loop = false;
        state.spawn_count = 0.0;
        for event in &self.events {
            match event {
                SourceEmissionEvent::Play => {
                    state.loop_state = SourceEmissionLoopState::Looping;
                    state.loop_total_time_seconds = 0.0;
                    state.loop_duration_seconds = self.parameters.loop_duration_seconds;
                    state.loop_count = self.parameters.loop_count;
                    state.loop_index = 0;
                    state.new_loop = true;
                    emitted = false;
                }
                SourceEmissionEvent::Stop => {
                    if state.playing() {
                        state.loop_total_time_seconds = 0.0;
                    }
                    state.loop_state = SourceEmissionLoopState::Finished;
                }
            }
        }
        state.effect_time_seconds += delta_seconds;
        state.loop_total_time_seconds += delta_seconds;
        if !state.effect_time_seconds.is_finite() || !state.loop_total_time_seconds.is_finite() {
            return Err("Source emission clock overflow");
        }
        let request = if state.playing()
            && !emitted
            && self.parameters.burst_active
            && state.loop_total_time_seconds >= self.parameters.burst_delay_seconds
        {
            emitted = true;
            self.parameters.burst_count
        } else {
            0.0
        };
        state.spawn_count = request;
        if state.playing() && state.loop_total_time_seconds >= self.parameters.loop_duration_seconds
        {
            state.loop_state = SourceEmissionLoopState::Finished;
            state.loop_index = self.parameters.loop_count;
            state.loop_total_time_seconds = 0.0;
            state.spawn_count = 0.0;
        }
        let result = SourceEmissionAdvance {
            reset_particles: self.reset_pending,
            spawn_request: request,
            delta_seconds,
            snapshot: state,
        };
        self.snapshot = state;
        self.emitted = emitted;
        self.events.clear();
        self.reset_pending = false;
        Ok(result)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn parameters() -> SourceSingleBurstLoopParameters {
        SourceSingleBurstLoopParameters {
            loop_duration_seconds: 3.0,
            loop_count: 1,
            burst_delay_seconds: 0.0,
            burst_count: 1.0,
            burst_active: true,
            initial_play: true,
            prewarm_steps: 0,
        }
    }

    #[test]
    fn repeated_zero_updates_do_not_repeat_emission_and_play_keeps_owner_time() {
        let mut clock = SourceSingleBurstLoop::new(parameters()).unwrap();
        let initial = clock.advance(0.0).unwrap();
        assert!(initial.reset_particles);
        assert_eq!(initial.spawn_request.to_bits(), 1.0_f32.to_bits());
        assert_eq!(clock.advance(0.0).unwrap().spawn_request.to_bits(), 0);
        clock.advance(0.125).unwrap();
        clock.queue_event(SourceEmissionEvent::Play);
        let replay = clock.advance(0.0).unwrap();
        assert!(!replay.reset_particles);
        assert_eq!(
            replay.snapshot.effect_time_seconds.to_bits(),
            0.125_f32.to_bits()
        );
        assert_eq!(replay.snapshot.loop_total_time_seconds.to_bits(), 0);
        assert_eq!(replay.spawn_request.to_bits(), 1.0_f32.to_bits());
    }

    #[test]
    fn exact_boundary_finishes_and_discards_overshoot_but_not_owner_time() {
        let mut clock = SourceSingleBurstLoop::new(parameters()).unwrap();
        clock.advance(0.0).unwrap();
        let before = f32::from_bits(3.0_f32.to_bits() - 1);
        assert!(clock.advance(before).unwrap().snapshot.playing());
        let end = clock.advance(3.0 - before).unwrap();
        assert!(!end.snapshot.playing());
        assert_eq!(end.snapshot.loop_index, 1);
        assert_eq!(end.snapshot.loop_total_time_seconds.to_bits(), 0);
        clock.queue_event(SourceEmissionEvent::Play);
        let large = clock.advance(6.0).unwrap();
        assert!(!large.snapshot.playing());
        assert_eq!(large.spawn_request.to_bits(), 1.0_f32.to_bits());
        assert_eq!(large.snapshot.spawn_count.to_bits(), 0);
        assert_eq!(
            large.snapshot.effect_time_seconds.to_bits(),
            9.0_f32.to_bits()
        );
    }

    #[test]
    fn event_order_and_duplicate_play_control_one_request() {
        let mut clock = SourceSingleBurstLoop::new(parameters()).unwrap();
        clock.advance(0.0).unwrap();
        clock.queue_event(SourceEmissionEvent::Stop);
        clock.queue_event(SourceEmissionEvent::Play);
        assert_eq!(
            clock.advance(0.0).unwrap().spawn_request.to_bits(),
            1.0_f32.to_bits()
        );
        clock.queue_event(SourceEmissionEvent::Play);
        clock.queue_event(SourceEmissionEvent::Stop);
        let stopped = clock.advance(0.0).unwrap();
        assert!(!stopped.snapshot.playing());
        assert!(stopped.snapshot.new_loop);
        assert_eq!(stopped.spawn_request.to_bits(), 0);
        clock.queue_event(SourceEmissionEvent::Play);
        clock.queue_event(SourceEmissionEvent::Play);
        assert_eq!(
            clock.advance(0.0).unwrap().spawn_request.to_bits(),
            1.0_f32.to_bits()
        );
    }

    #[test]
    fn stopping_a_running_loop_resets_its_time_but_stopping_finished_does_not() {
        let mut clock = SourceSingleBurstLoop::new(parameters()).unwrap();
        clock.advance(0.0).unwrap();
        clock.advance(0.25).unwrap();
        clock.queue_event(SourceEmissionEvent::Stop);
        let stop = clock.advance(0.125).unwrap();
        assert_eq!(
            stop.snapshot.loop_total_time_seconds.to_bits(),
            0.125_f32.to_bits()
        );
        clock.queue_event(SourceEmissionEvent::Stop);
        let again = clock.advance(6.0).unwrap();
        assert_eq!(
            again.snapshot.loop_total_time_seconds.to_bits(),
            6.125_f32.to_bits()
        );
        assert!(!again.snapshot.playing());
    }

    #[test]
    fn reinit_cancels_queued_events_and_requests_one_gpu_reset() {
        let mut clock = SourceSingleBurstLoop::new(parameters()).unwrap();
        clock.advance(0.125).unwrap();
        clock.queue_event(SourceEmissionEvent::Stop);
        clock.reinitialize();
        assert_eq!(clock.snapshot().effect_time_seconds.to_bits(), 0);
        let first = clock.advance(0.0).unwrap();
        assert!(first.reset_particles);
        assert_eq!(first.spawn_request.to_bits(), 1.0_f32.to_bits());
        assert!(!clock.advance(0.0).unwrap().reset_particles);
    }

    #[test]
    fn invalid_delta_keeps_queued_events_and_last_committed_snapshot() {
        let mut clock = SourceSingleBurstLoop::new(parameters()).unwrap();
        clock.advance(0.0).unwrap();
        clock.queue_event(SourceEmissionEvent::Stop);
        let before = clock.snapshot();
        assert!(clock.advance(f32::NAN).is_err());
        assert!(clock.advance(-0.125).is_err());
        assert_eq!(clock.snapshot(), before);
        assert!(!clock.advance(0.0).unwrap().snapshot.playing());
    }

    #[test]
    fn unsupported_source_profiles_are_rejected_without_defaults() {
        let mut source = parameters();
        source.prewarm_steps = 1;
        assert!(SourceSingleBurstLoop::new(source).is_err());
        source.prewarm_steps = 0;
        source.loop_count = 2;
        assert!(SourceSingleBurstLoop::new(source).is_err());
        source.loop_count = 1;
        source.burst_count = f32::INFINITY;
        assert!(SourceSingleBurstLoop::new(source).is_err());
    }
}
