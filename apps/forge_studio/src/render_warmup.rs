//! Initial presentation guard, not proof of completed GPU work or artistic quality.
use serde::Serialize;

// Match the native capture watchdog; cold WebGPU preparation can exceed 30 seconds.
const TIMEOUT_MS: f64 = 120_000.0;

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq, Serialize)]
#[serde(rename_all = "snake_case")]
enum Phase {
    #[default]
    Preparing,
    Prepared,
    Failed,
    TimedOut,
}

#[derive(Clone, Debug, Default, Serialize)]
struct Warmup {
    phase: Phase,
    observations: u64,
    total: usize,
    pending: usize,
    failed: usize,
    peak_pending: usize,
    transient_shader_errors_peak: usize,
    stable: u32,
    elapsed_ms: f64,
}

impl Warmup {
    fn observe(
        &mut self,
        total: usize,
        pending: usize,
        failed: usize,
        scene_ready: bool,
        elapsed_ms: f64,
    ) {
        if self.phase != Phase::Preparing {
            return;
        }
        self.stable = if total > 0 && pending == 0 && failed == 0 && scene_ready {
            if total == self.total {
                self.stable.saturating_add(1)
            } else {
                1
            }
        } else {
            0
        };
        self.observations = self.observations.saturating_add(1);
        self.total = total;
        self.pending = pending;
        self.failed = failed;
        self.peak_pending = self.peak_pending.max(pending);
        self.elapsed_ms = elapsed_ms;
        self.phase = if failed > 0 {
            Phase::Failed
        } else if !elapsed_ms.is_finite() || elapsed_ms >= TIMEOUT_MS {
            Phase::TimedOut
        } else if self.stable >= 8 {
            Phase::Prepared
        } else {
            Phase::Preparing
        };
    }
}

#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod web;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
pub(crate) use web::{Loading, install};

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn waits_for_scene_and_eight_fresh_stable_pipeline_observations() {
        let mut state = Warmup::default();
        for _ in 0..20 {
            state.observe(10, 0, 0, false, 100.0);
        }
        assert_eq!(state.phase, Phase::Preparing);
        state.observe(10, 2, 0, true, 200.0);
        for _ in 0..7 {
            state.observe(10, 0, 0, true, 300.0);
        }
        assert_eq!(state.phase, Phase::Preparing);
        state.observe(11, 0, 0, true, 400.0);
        assert_eq!(state.stable, 1);
        for _ in 0..7 {
            state.observe(11, 0, 0, true, 500.0);
        }
        assert_eq!(state.phase, Phase::Prepared);
        assert_eq!(state.peak_pending, 2);
        state.observe(12, 1, 0, false, 600.0);
        assert_eq!(
            state.phase,
            Phase::Prepared,
            "initial guard must not hide later quality changes"
        );
    }

    #[test]
    fn failures_and_missing_observations_never_become_success() {
        let mut failed = Warmup::default();
        failed.observe(10, 0, 1, true, 20.0);
        assert_eq!(failed.phase, Phase::Failed);
        failed.observe(10, 0, 0, true, 30.0);
        assert_eq!(failed.phase, Phase::Failed);
        for elapsed in [TIMEOUT_MS, f64::NAN] {
            let mut empty = Warmup::default();
            empty.observe(0, 0, 0, true, elapsed);
            assert_eq!(empty.phase, Phase::TimedOut);
        }
    }

    #[test]
    fn deferred_shader_loads_stay_pending_and_recover_without_hiding_fatal_errors() {
        let mut state = Warmup::default();
        // Bevy requeues ShaderNotLoaded / ShaderImportNotYetAvailable: count as pending.
        state.observe(55, 53, 0, true, 200.0);
        assert_eq!(state.phase, Phase::Preparing);
        for _ in 0..8 {
            state.observe(55, 0, 0, true, 500.0);
        }
        assert_eq!(state.phase, Phase::Prepared);
        let mut fatal = Warmup::default();
        fatal.observe(55, 53, 1, true, 200.0);
        assert_eq!(fatal.phase, Phase::Failed);
    }

    #[test]
    fn cold_preparation_at_thirty_three_seconds_still_requires_eight_stable_observations() {
        let mut state = Warmup::default();
        state.observe(55, 53, 0, false, 100.0);
        for _ in 0..4 {
            state.observe(98, 0, 0, true, 33_036.7);
        }
        assert_eq!(state.phase, Phase::Preparing);
        for _ in 0..4 {
            state.observe(98, 0, 0, true, 33_300.0);
        }
        assert_eq!(state.phase, Phase::Prepared);
        let mut pending = Warmup::default();
        pending.observe(98, 0, 0, false, TIMEOUT_MS);
        assert_eq!(pending.phase, Phase::TimedOut);
    }
}
