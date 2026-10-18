//! Tidewater's adaptive spray-ring emission budget, independent of GPU/readback plumbing.
//! Call after encoding emission, with the latest completed head sample. The returned
//! value applies to the next emission, including when the latest sample is repeated.
use std::collections::VecDeque;

#[derive(Clone, Copy, Debug)]
pub struct HeadSample {
    pub time: f64,
    pub head: u32,
}

/// GPU submission order is independent of a simulation clock that can be reset.
#[derive(Clone, Copy, Debug)]
pub struct SequencedHeadSample {
    pub sequence: u64,
    pub sample: HeadSample,
}

#[derive(Debug)]
pub struct SprayBudget {
    capacity: u32,
    value: f64,
    history: VecDeque<HeadSample>,
    latest_sequence: Option<SequencedHeadSample>,
}

impl SprayBudget {
    pub fn new(capacity: u32) -> Result<Self, &'static str> {
        if capacity == 0 {
            return Err("spray ring capacity must be positive");
        }
        Ok(Self {
            capacity,
            value: 0.3,
            history: VecDeque::new(),
            latest_sequence: None,
        })
    }

    #[must_use]
    pub fn value(&self) -> f64 {
        self.value
    }

    /// None represents a readback that has not completed. Repeated samples still
    /// steer the budget, matching the source's asynchronous latest-value semantics.
    #[allow(clippy::float_cmp)] // Exact source timestamp identity, not approximate duration equality.
    pub fn after_emission(&mut self, latest: Option<HeadSample>) -> Result<(), &'static str> {
        let Some(sample) = latest else {
            return Ok(());
        };
        if !sample.time.is_finite() || sample.time < 0.0 {
            return Err("invalid spray sample time");
        }
        if self.latest_sequence.is_some() {
            return Err("cannot mix sequenced and time-ordered spray feedback");
        }
        if let Some(last) = self.history.back()
            && (sample.time < last.time || (sample.time == last.time && sample.head != last.head))
        {
            return Err("spray head sample is stale or inconsistent");
        }
        self.apply_sample(sample);
        Ok(())
    }

    /// Accept a clock rewind only when a newer GPU submission proves sample order.
    /// Repeated submissions must preserve their original timestamp and head.
    /// A controller uses either this API or time-ordered feedback for its lifetime.
    pub fn after_sequenced_emission(
        &mut self,
        latest: Option<SequencedHeadSample>,
    ) -> Result<(), &'static str> {
        let Some(latest) = latest else {
            return Ok(());
        };
        let sample = latest.sample;
        if !sample.time.is_finite() || sample.time < 0.0 {
            return Err("invalid spray sample time");
        }
        if self.latest_sequence.is_none() && !self.history.is_empty() {
            return Err("cannot mix sequenced and time-ordered spray feedback");
        }
        if let Some(previous) = self.latest_sequence
            && (latest.sequence < previous.sequence
                || (latest.sequence == previous.sequence
                    && (sample.time.to_bits() != previous.sample.time.to_bits()
                        || sample.head != previous.sample.head)))
        {
            return Err("spray head submission is stale or inconsistent");
        }
        self.apply_sample(sample);
        self.latest_sequence = Some(latest);
        Ok(())
    }

    #[allow(clippy::float_cmp)] // Source history deduplicates timestamps exactly.
    fn apply_sample(&mut self, sample: HeadSample) {
        if self
            .history
            .back()
            .is_none_or(|last| last.time != sample.time)
        {
            self.history.push_back(sample);
        }
        while self.history.len() > 2 && sample.time - self.history[0].time > 2.5 {
            self.history.pop_front();
        }
        if self.history.len() > 1 {
            let first = self.history[0];
            let last = self.history.back().expect("nonempty budget history");
            let dt = last.time - first.time;
            if dt > 1.0 {
                let rate = f64::from(last.head.wrapping_sub(first.head)) / dt;
                let target = f64::from(self.capacity) / 2.0;
                let want = if rate > 1.0 {
                    self.value * (target / rate).sqrt()
                } else {
                    1.0
                };
                self.value = (self.value + (want - self.value) * 0.04).clamp(0.05, 1.0);
            }
        }
    }
}

#[cfg(test)]
#[allow(clippy::float_cmp)] // Exact clamp values and source timestamp boundaries are intentional.
mod tests {
    use super::*;
    #[allow(
        clippy::unnecessary_wraps,
        reason = "Constructs optional feedback with the same shape used by the missing-feedback fixture."
    )]
    fn sample(time: f64, head: u32) -> Option<HeadSample> {
        Some(HeadSample { time, head })
    }

    #[allow(
        clippy::unnecessary_wraps,
        reason = "Mirrors optional GPU readback samples in the sequenced API."
    )]
    fn sequenced(sequence: u64, time: f64, head: u32) -> Option<SequencedHeadSample> {
        Some(SequencedHeadSample {
            sequence,
            sample: HeadSample { time, head },
        })
    }

    #[test]
    fn newer_submission_at_same_time_keeps_source_history_head() {
        let mut changed_head = SprayBudget::new(32768).unwrap();
        let mut repeated_head = SprayBudget::new(32768).unwrap();
        for budget in [&mut changed_head, &mut repeated_head] {
            budget
                .after_sequenced_emission(sequenced(1, 0.0, 0))
                .unwrap();
            budget
                .after_sequenced_emission(sequenced(2, 2.0, 1000))
                .unwrap();
        }
        changed_head
            .after_sequenced_emission(sequenced(3, 2.0, 2000))
            .unwrap();
        repeated_head
            .after_sequenced_emission(sequenced(2, 2.0, 1000))
            .unwrap();
        assert_eq!(changed_head.value(), repeated_head.value());
        assert_eq!(changed_head.history.len(), 2);
        assert_eq!(changed_head.history[1].head, 1000);
    }

    #[test]
    fn submission_order_allows_clock_rewind_without_clearing_history() {
        let mut budget = SprayBudget::new(32768).unwrap();
        budget
            .after_sequenced_emission(sequenced(4, 4.0 / 60.0, 40))
            .unwrap();
        budget
            .after_sequenced_emission(sequenced(5, 1.0 / 60.0, 50))
            .unwrap();
        assert_eq!(budget.history.len(), 2);
        assert_eq!(budget.history[0].head, 40);
        assert_eq!(budget.history[1].head, 50);
        assert_eq!(budget.value(), 0.3);
    }

    #[test]
    fn sequenced_stale_and_inconsistent_returns_preserve_controller() {
        let mut budget = SprayBudget::new(32768).unwrap();
        budget
            .after_sequenced_emission(sequenced(5, 0.1, 50))
            .unwrap();
        for bad in [
            sequenced(4, 0.2, 51),
            sequenced(5, 0.2, 50),
            sequenced(5, 0.1, 51),
            sequenced(6, f64::NAN, 60),
        ] {
            assert!(budget.after_sequenced_emission(bad).is_err());
            assert_eq!(budget.history.len(), 1);
            assert_eq!(budget.latest_sequence.unwrap().sequence, 5);
            assert_eq!(budget.value(), 0.3);
        }
    }

    #[test]
    fn repeated_submission_steers_without_duplicate_history() {
        let mut budget = SprayBudget::new(32768).unwrap();
        budget
            .after_sequenced_emission(sequenced(1, 0.0, 0))
            .unwrap();
        budget
            .after_sequenced_emission(sequenced(2, 2.0, 1000))
            .unwrap();
        let before = budget.value();
        budget
            .after_sequenced_emission(sequenced(2, 2.0, 1000))
            .unwrap();
        assert!(budget.value() > before);
        assert_eq!(budget.history.len(), 2);
    }

    #[test]
    fn feedback_ordering_modes_cannot_be_mixed() {
        let mut timed = SprayBudget::new(32768).unwrap();
        timed.after_emission(sample(0.0, 0)).unwrap();
        assert!(
            timed
                .after_sequenced_emission(sequenced(1, 0.1, 1))
                .is_err()
        );
        let mut ordered = SprayBudget::new(32768).unwrap();
        ordered
            .after_sequenced_emission(sequenced(1, 0.0, 0))
            .unwrap();
        assert!(ordered.after_emission(sample(0.1, 1)).is_err());
    }

    #[test]
    fn no_feedback_or_exactly_one_second_does_not_adjust() {
        let mut budget = SprayBudget::new(32768).unwrap();
        budget.after_emission(None).unwrap();
        budget.after_emission(sample(0.0, 0)).unwrap();
        budget.after_emission(sample(1.0, 10)).unwrap();
        assert_eq!(budget.value(), 0.3);
        budget.after_emission(sample(1.1, 11)).unwrap();
        assert!(budget.value() > 0.3);
    }

    #[test]
    fn repeated_latest_sample_still_steers_without_growing_history() {
        let mut budget = SprayBudget::new(32768).unwrap();
        budget.after_emission(sample(0.0, 0)).unwrap();
        budget.after_emission(sample(2.0, 1000)).unwrap();
        let before = budget.value();
        budget.after_emission(sample(2.0, 1000)).unwrap();
        assert!(budget.value() > before);
        assert_eq!(budget.history.len(), 2);
    }

    #[test]
    fn unsigned_wrap_preserves_emission_rate() {
        let mut plain = SprayBudget::new(32768).unwrap();
        let mut wrapped = SprayBudget::new(32768).unwrap();
        plain.after_emission(sample(0.0, 0)).unwrap();
        plain.after_emission(sample(2.0, 40000)).unwrap();
        wrapped.after_emission(sample(0.0, u32::MAX - 99)).unwrap();
        wrapped.after_emission(sample(2.0, 39900)).unwrap();
        assert_eq!(plain.value(), wrapped.value());
        assert!(plain.value() < 0.3);
    }

    #[test]
    fn history_expires_but_keeps_two_samples() {
        let mut budget = SprayBudget::new(32768).unwrap();
        for i in 0..10 {
            budget
                .after_emission(sample(f64::from(i), i * 100))
                .unwrap();
        }
        assert_eq!(budget.history.len(), 3);
        assert_eq!(budget.history[0].time, 7.0);
        budget.after_emission(sample(100.0, 2000)).unwrap();
        assert_eq!(budget.history.len(), 2);
    }

    #[test]
    fn clamps_low_and_high_under_sustained_feedback() {
        let mut low = SprayBudget::new(1).unwrap();
        let mut high = SprayBudget::new(32768).unwrap();
        for i in 0..1000 {
            low.after_emission(sample(f64::from(i) / 60.0, i * 100_000))
                .unwrap();
            high.after_emission(sample(f64::from(i) / 60.0, i * 2))
                .unwrap();
        }
        assert_eq!(low.value(), 0.05);
        assert_eq!(high.value(), 1.0);
    }

    #[test]
    fn invalid_feedback_is_rejected_without_mutation() {
        assert!(SprayBudget::new(0).is_err());
        let mut budget = SprayBudget::new(32768).unwrap();
        budget.after_emission(sample(2.0, 10)).unwrap();
        for invalid in [
            sample(f64::NAN, 10),
            sample(f64::INFINITY, 10),
            sample(-1.0, 10),
            sample(1.0, 10),
            sample(2.0, 11),
        ] {
            assert!(budget.after_emission(invalid).is_err());
            assert_eq!(budget.value(), 0.3);
            assert_eq!(budget.history.len(), 1);
        }
    }
}
