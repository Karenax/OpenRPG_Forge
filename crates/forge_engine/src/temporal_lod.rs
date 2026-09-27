//! Shared, allocation-free temporal selection for a render object's LOD group.
//!
//! Adapters supply distance from one common group root and readiness of the actual
//! GPU representations. Authored transforms and meshes are never modified here.

const MAX_LEVELS: usize = 4;

pub(crate) mod render;

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct TemporalLodPolicy {
    thresholds_m: [f32; MAX_LEVELS - 1],
    levels: usize,
    hysteresis_m: f32,
    duration_seconds: f32,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum TemporalLodError {
    InvalidPolicy,
    InvalidObservation,
    InvalidInitialLevel,
    CurrentRepresentationUnavailable,
}

impl TemporalLodPolicy {
    /// Two to four representations, ordered from detailed to simplified.
    /// Thresholds are centres; hysteresis applies on either side of each centre.
    pub fn new(
        thresholds_m: &[f32],
        hysteresis_m: f32,
        duration_seconds: f32,
    ) -> Result<Self, TemporalLodError> {
        if thresholds_m.is_empty()
            || thresholds_m.len() >= MAX_LEVELS
            || !hysteresis_m.is_finite()
            || hysteresis_m < 0.0
            || !duration_seconds.is_finite()
            || !(0.01..=2.0).contains(&duration_seconds)
            || thresholds_m
                .iter()
                .any(|v| !v.is_finite() || *v <= hysteresis_m)
            || thresholds_m
                .windows(2)
                .any(|v| v[1] - v[0] <= 2.0 * hysteresis_m)
        {
            return Err(TemporalLodError::InvalidPolicy);
        }
        let mut thresholds = [0.0; MAX_LEVELS - 1];
        thresholds[..thresholds_m.len()].copy_from_slice(thresholds_m);
        Ok(Self {
            thresholds_m: thresholds,
            levels: thresholds_m.len() + 1,
            hysteresis_m,
            duration_seconds,
        })
    }

    fn requested(self, current: usize, distance_m: f32) -> usize {
        let mut target = current;
        while target + 1 < self.levels
            && distance_m >= self.thresholds_m[target] + self.hysteresis_m
        {
            target += 1;
        }
        while target > 0 && distance_m < self.thresholds_m[target - 1] - self.hysteresis_m {
            target -= 1;
        }
        target
    }
}

#[derive(Clone, Copy, Debug, PartialEq)]
struct Fade {
    target: usize,
    elapsed_seconds: f32,
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct TemporalLod {
    policy: TemporalLodPolicy,
    current: usize,
    fade: Option<Fade>,
}

/// One result is shared by all primitives and colour/depth/shadow consumers.
#[derive(Clone, Copy, Debug, PartialEq)]
pub struct TemporalLodSample {
    pub weights: [f32; MAX_LEVELS],
    pub transition_active: bool,
    pub waiting_for_ready: bool,
}

impl TemporalLod {
    /// `initial_level` must already have a usable representation in the adapter.
    pub fn new(policy: TemporalLodPolicy, initial_level: usize) -> Result<Self, TemporalLodError> {
        if initial_level >= policy.levels {
            return Err(TemporalLodError::InvalidInitialLevel);
        }
        Ok(Self {
            policy,
            current: initial_level,
            fade: None,
        })
    }

    /// A stopped camera cannot stop a started fade. A reversal finishes the current
    /// transition first; the next observation then selects from the committed LOD.
    /// False readiness holds the original representation, never an empty group.
    /// Invalid observations leave state intact. Readiness is not inferred from a
    /// CPU handle, a request, or elapsed wall time.
    pub fn advance(
        &mut self,
        distance_m: f32,
        delta_seconds: f32,
        gpu_ready: [bool; MAX_LEVELS],
    ) -> Result<TemporalLodSample, TemporalLodError> {
        if !distance_m.is_finite()
            || distance_m < 0.0
            || !delta_seconds.is_finite()
            || delta_seconds < 0.0
        {
            return Err(TemporalLodError::InvalidObservation);
        }
        if !gpu_ready[self.current] {
            return Err(TemporalLodError::CurrentRepresentationUnavailable);
        }
        let mut waiting = false;
        if let Some(fade) = self.fade {
            if !gpu_ready[fade.target] {
                self.fade = None;
                waiting = true;
            }
        } else {
            let requested = self.policy.requested(self.current, distance_m);
            if requested != self.current {
                if gpu_ready[requested] {
                    self.fade = Some(Fade {
                        target: requested,
                        elapsed_seconds: 0.0,
                    });
                } else {
                    waiting = true;
                }
            }
        }
        let mut weights = [0.0; MAX_LEVELS];
        if let Some(fade) = &mut self.fade {
            fade.elapsed_seconds =
                (fade.elapsed_seconds + delta_seconds).min(self.policy.duration_seconds);
            let fraction = fade.elapsed_seconds / self.policy.duration_seconds;
            weights[self.current] = 1.0 - fraction;
            weights[fade.target] = fraction;
            if fraction >= 1.0 {
                self.current = fade.target;
                self.fade = None;
            }
        } else {
            weights[self.current] = 1.0;
        }
        Ok(TemporalLodSample {
            weights,
            transition_active: self.fade.is_some(),
            waiting_for_ready: waiting,
        })
    }
}

#[cfg(test)]
#[allow(
    clippy::float_cmp,
    reason = "Coverage endpoints and exact binary quarter weights are contract values, not approximate measurements"
)]
mod tests {
    use super::*;

    fn state() -> TemporalLod {
        TemporalLod::new(TemporalLodPolicy::new(&[24.0, 80.0], 2.0, 0.2).unwrap(), 0).unwrap()
    }

    #[test]
    fn stopped_camera_finishes_at_full_coverage() {
        let mut state = state();
        let first = state.advance(26.0, 0.05, [true; 4]).unwrap();
        assert_eq!(first.weights, [0.75, 0.25, 0.0, 0.0]);
        let done = state.advance(26.0, 0.15, [true; 4]).unwrap();
        assert_eq!(done.weights, [0.0, 1.0, 0.0, 0.0]);
        assert!(!done.transition_active);
        for _ in 0..100 {
            assert_eq!(
                state.advance(24.0, 1.0, [true; 4]).unwrap().weights,
                done.weights
            );
        }
    }

    #[test]
    fn hysteresis_prevents_threshold_chatter() {
        let mut state = state();
        for distance in [23.0, 24.0, 25.9, 23.9, 25.0] {
            assert_eq!(
                state.advance(distance, 1.0, [true; 4]).unwrap().weights[0],
                1.0
            );
        }
        state.advance(27.0, 1.0, [true; 4]).unwrap();
        for distance in [23.0, 24.0, 26.0, 22.0] {
            assert_eq!(
                state.advance(distance, 1.0, [true; 4]).unwrap().weights[1],
                1.0
            );
        }
        assert_eq!(state.advance(21.9, 1.0, [true; 4]).unwrap().weights[0], 1.0);
    }

    #[test]
    fn waits_for_gpu_and_recovers_from_lost_target() {
        let mut state = state();
        let pending = state
            .advance(30.0, 10.0, [true, false, false, false])
            .unwrap();
        assert_eq!(pending.weights, [1.0, 0.0, 0.0, 0.0]);
        assert!(pending.waiting_for_ready);
        state.advance(30.0, 0.1, [true; 4]).unwrap();
        let failed = state
            .advance(30.0, 0.1, [true, false, false, false])
            .unwrap();
        assert_eq!(failed.weights, [1.0, 0.0, 0.0, 0.0]);
        assert!(failed.waiting_for_ready);
        assert_eq!(state.advance(30.0, 0.2, [true; 4]).unwrap().weights[1], 1.0);
    }

    #[test]
    fn fast_motion_keeps_at_most_two_complementary_representations() {
        let mut state = state();
        for distance in [200.0, 0.0, 200.0, 0.0, 200.0, 0.0] {
            let sample = state.advance(distance, 0.05, [true; 4]).unwrap();
            assert!(sample.weights.iter().filter(|&&w| w > 0.0).count() <= 2);
            assert!((sample.weights.iter().sum::<f32>() - 1.0).abs() < f32::EPSILON);
        }
        state.advance(200.0, 1.0, [true; 4]).unwrap();
        assert_eq!(
            state.advance(200.0, 1.0, [true; 4]).unwrap().weights[2],
            1.0
        );
    }

    #[test]
    fn invalid_input_is_transactional() {
        let mut state = state();
        let before = state;
        for (distance, delta) in [
            (f32::NAN, 0.1),
            (1.0, f32::INFINITY),
            (-1.0, 0.1),
            (1.0, -0.1),
        ] {
            assert_eq!(
                state.advance(distance, delta, [true; 4]),
                Err(TemporalLodError::InvalidObservation)
            );
            assert_eq!(state, before);
        }
        assert_eq!(
            state.advance(30.0, 0.1, [false; 4]),
            Err(TemporalLodError::CurrentRepresentationUnavailable)
        );
        assert_eq!(state, before);
    }

    #[test]
    fn malformed_policies_are_rejected() {
        for thresholds in [
            &[][..],
            &[10.0, 9.0],
            &[1.0],
            &[10.0, 11.0],
            &[f32::NAN],
            &[10.0, 20.0, 30.0, 40.0],
        ] {
            assert_eq!(
                TemporalLodPolicy::new(thresholds, 2.0, 0.2),
                Err(TemporalLodError::InvalidPolicy)
            );
        }
        assert_eq!(
            TemporalLodPolicy::new(&[24.0], 2.0, 0.0),
            Err(TemporalLodError::InvalidPolicy)
        );
    }
}
