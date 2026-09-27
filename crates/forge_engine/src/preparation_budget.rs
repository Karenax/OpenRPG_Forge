//! Cooperative browser preparation: bounded work between existing chunk boundaries.
use bevy::platform::time::Instant;
use std::time::Duration;

/// A soft CPU budget per adapter call, with a deterministic step ceiling.
/// An indivisible chunk may exceed the budget; it is never interrupted halfway.
pub(crate) struct ChunkBudget {
    started: Instant,
    completed: usize,
}

impl ChunkBudget {
    pub(crate) fn new() -> Self {
        Self {
            started: Instant::now(),
            completed: 0,
        }
    }

    pub(crate) fn exhausted(&mut self) -> bool {
        self.after_step(self.started.elapsed())
    }

    fn after_step(&mut self, elapsed: Duration) -> bool {
        self.completed += 1;
        self.completed >= 8 || elapsed >= Duration::from_millis(4)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn cheap_chunks_share_one_call_but_never_exceed_eight_steps() {
        let mut budget = ChunkBudget::new();
        for _ in 0..7 {
            assert!(!budget.after_step(Duration::ZERO));
        }
        assert!(budget.after_step(Duration::ZERO));
    }

    #[test]
    fn time_budget_stops_at_exact_boundary() {
        let mut budget = ChunkBudget::new();
        assert!(!budget.after_step(Duration::from_micros(3999)));
        assert!(budget.after_step(Duration::from_millis(4)));
    }

    #[test]
    fn expensive_chunk_prevents_a_second_step() {
        let mut budget = ChunkBudget::new();
        assert!(budget.after_step(Duration::from_millis(25)));
    }
}
