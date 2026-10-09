//! Imported decal lifetime, separate from projector rendering and material lookup.
//! The host selects eligible active descendant projectors, clones their materials,
//! and supplies the sampled delay. This state machine never edits shared materials.

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct DissolveParameters {
    pub minimum_delay_seconds: f32,
    pub maximum_delay_seconds: f32,
    pub duration_seconds: f32,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
enum Phase {
    Waiting,
    Dissolving,
    Destroyed,
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct DissolveUpdate {
    /// Apply only to the per-projector material instances created by the host.
    pub material_dissolve: Option<f32>,
    /// Remove the owning placement and its descendants, once.
    pub destroy_owner: bool,
}

#[derive(Clone, Debug, PartialEq)]
pub struct DecalDissolveLifetime {
    parameters: DissolveParameters,
    sampled_delay_seconds: f32,
    eligible_material_count: usize,
    timer_seconds: f32,
    phase: Phase,
}

impl DecalDissolveLifetime {
    /// RNG choice belongs to the host; accepting a delay does not reproduce
    /// Unity's global random sequence. The selected value remains explicit.
    pub fn new(
        parameters: DissolveParameters,
        sampled_delay_seconds: f32,
        eligible_material_count: usize,
    ) -> Result<Self, &'static str> {
        if !parameters.minimum_delay_seconds.is_finite()
            || !parameters.maximum_delay_seconds.is_finite()
            || parameters.minimum_delay_seconds > parameters.maximum_delay_seconds
            || !parameters.duration_seconds.is_finite()
            || parameters.duration_seconds <= 0.0
            || !sampled_delay_seconds.is_finite()
            || sampled_delay_seconds < parameters.minimum_delay_seconds
            || sampled_delay_seconds > parameters.maximum_delay_seconds
        {
            return Err("invalid imported dissolve timing or sampled delay");
        }
        Ok(Self {
            parameters,
            sampled_delay_seconds,
            eligible_material_count,
            timer_seconds: 0.0,
            phase: Phase::Waiting,
        })
    }

    pub fn initial_material_dissolve(&self) -> Option<f32> {
        (self.eligible_material_count != 0).then_some(0.0)
    }

    /// Preserve source frame semantics: crossing the delay resets the timer,
    /// discarding overshoot, before evaluating this frame's dissolve value.
    pub fn advance(&mut self, delta_seconds: f32) -> Result<DissolveUpdate, &'static str> {
        if !delta_seconds.is_finite() || delta_seconds < 0.0 {
            return Err("invalid frame delta");
        }
        let idle = DissolveUpdate {
            material_dissolve: None,
            destroy_owner: false,
        };
        if self.eligible_material_count == 0 || self.phase == Phase::Destroyed {
            return Ok(idle);
        }
        self.timer_seconds += delta_seconds;
        if self.phase == Phase::Waiting && self.timer_seconds >= self.sampled_delay_seconds {
            self.phase = Phase::Dissolving;
            self.timer_seconds = 0.0;
        }
        if self.phase == Phase::Waiting {
            return Ok(idle);
        }
        let value = (self.timer_seconds / self.parameters.duration_seconds).clamp(0.0, 1.0);
        let destroy_owner = value >= 1.0;
        if destroy_owner {
            self.phase = Phase::Destroyed;
        }
        Ok(DissolveUpdate {
            material_dissolve: Some(value),
            destroy_owner,
        })
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn timing() -> DissolveParameters {
        DissolveParameters {
            minimum_delay_seconds: 0.5,
            maximum_delay_seconds: 1.5,
            duration_seconds: 1.5,
        }
    }

    #[test]
    fn crossing_delay_discards_overshoot_and_destroys_owner_once() {
        let mut state = DecalDissolveLifetime::new(timing(), 1.0, 2).unwrap();
        assert_eq!(state.initial_material_dissolve(), Some(0.0));
        assert_eq!(state.advance(0.75).unwrap().material_dissolve, None);
        let crossed = state.advance(0.5).unwrap();
        assert_eq!(crossed.material_dissolve, Some(0.0));
        assert!(!crossed.destroy_owner);
        assert_eq!(state.advance(0.75).unwrap().material_dissolve, Some(0.5));
        let end = state.advance(1.0).unwrap();
        assert_eq!(end.material_dissolve, Some(1.0));
        assert!(end.destroy_owner);
        assert!(!state.advance(10.0).unwrap().destroy_owner);
    }

    #[test]
    fn empty_eligible_targets_never_age_or_destroy_owner() {
        let mut state = DecalDissolveLifetime::new(timing(), 0.5, 0).unwrap();
        assert_eq!(state.initial_material_dissolve(), None);
        assert_eq!(state.advance(100.0).unwrap().material_dissolve, None);
        assert_eq!(state.timer_seconds, 0.0);
        assert_eq!(state.phase, Phase::Waiting);
    }

    #[test]
    fn zero_delta_and_exact_delay_keep_source_frame_boundary() {
        let mut state = DecalDissolveLifetime::new(timing(), 0.5, 1).unwrap();
        assert_eq!(state.advance(0.0).unwrap().material_dissolve, None);
        assert_eq!(state.advance(0.5).unwrap().material_dissolve, Some(0.0));
        assert_eq!(state.advance(0.0).unwrap().material_dissolve, Some(0.0));
    }

    #[test]
    fn invalid_deltas_do_not_mutate_lifetime() {
        let mut state = DecalDissolveLifetime::new(timing(), 0.5, 1).unwrap();
        for invalid in [-1.0, f32::NAN, f32::INFINITY] {
            let before = state.clone();
            assert!(state.advance(invalid).is_err());
            assert_eq!(state, before);
        }
    }

    #[test]
    fn invalid_timing_or_rng_samples_are_refused() {
        for invalid in [0.49, 1.51, f32::NAN, f32::INFINITY] {
            assert!(DecalDissolveLifetime::new(timing(), invalid, 1).is_err());
        }
        let mut invalid = timing();
        invalid.duration_seconds = 0.0;
        assert!(DecalDissolveLifetime::new(invalid, 0.5, 1).is_err());
        invalid = timing();
        invalid.minimum_delay_seconds = 2.0;
        assert!(DecalDissolveLifetime::new(invalid, 1.0, 1).is_err());
    }
}
