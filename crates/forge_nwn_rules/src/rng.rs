//! Injected deterministic RNG with a bounded trace journal.
//!
//! Same seed and same labelled calls produce the same values and trace. No wall
//! clock, thread runtime or OS entropy is used.

use serde::{Deserialize, Serialize};
use std::fmt;

pub const RNG_SCHEMA: &str = "forge.nwn-rng@1";
pub const MAX_TRACE_ENTRIES: usize = 4096;

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct RollTrace {
    pub label: String,
    pub sides: u32,
    pub value: u32,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct RngSnapshot {
    pub schema: String,
    pub state: u64,
    pub trace: Vec<RollTrace>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct DeterministicRng {
    state: u64,
    trace: Vec<RollTrace>,
}

impl DeterministicRng {
    #[must_use]
    pub fn new(seed: u64) -> Self {
        Self {
            state: seed ^ 0x9E37_79B9_7F4A_7C15,
            trace: Vec::new(),
        }
    }

    #[must_use]
    pub const fn state(&self) -> u64 {
        self.state
    }

    #[must_use]
    pub fn trace(&self) -> &[RollTrace] {
        &self.trace
    }

    pub fn roll(&mut self, sides: u32, label: impl Into<String>) -> Result<u32, RngError> {
        if !(2..=1000).contains(&sides) {
            return Err(RngError::InvalidSides(sides));
        }
        if self.trace.len() >= MAX_TRACE_ENTRIES {
            return Err(RngError::TraceFull(MAX_TRACE_ENTRIES));
        }
        let value = u32::try_from(self.next_u64() % u64::from(sides))
            .map_err(|_| RngError::InvalidSides(sides))?
            + 1;
        self.trace.push(RollTrace {
            label: label.into(),
            sides,
            value,
        });
        Ok(value)
    }

    pub fn d20(&mut self, label: impl Into<String>) -> Result<u32, RngError> {
        self.roll(20, label)
    }

    pub fn roll_dice(
        &mut self,
        count: u8,
        sides: u32,
        label: impl Into<String>,
    ) -> Result<Vec<u32>, RngError> {
        let base = label.into();
        let mut values = Vec::with_capacity(usize::from(count));
        for index in 0..count {
            values.push(self.roll(sides, format!("{base}[{index}]"))?);
        }
        Ok(values)
    }

    #[must_use]
    pub fn snapshot(&self) -> RngSnapshot {
        RngSnapshot {
            schema: RNG_SCHEMA.to_owned(),
            state: self.state,
            trace: self.trace.clone(),
        }
    }

    pub fn restore(snapshot: RngSnapshot) -> Result<Self, RngError> {
        if snapshot.schema != RNG_SCHEMA {
            return Err(RngError::SnapshotSchema(snapshot.schema));
        }
        if snapshot.trace.len() > MAX_TRACE_ENTRIES {
            return Err(RngError::TraceFull(snapshot.trace.len()));
        }
        for entry in &snapshot.trace {
            if !(2..=1000).contains(&entry.sides) || entry.value == 0 || entry.value > entry.sides {
                return Err(RngError::InvalidTraceEntry {
                    label: entry.label.clone(),
                    sides: entry.sides,
                    value: entry.value,
                });
            }
        }
        Ok(Self {
            state: snapshot.state,
            trace: snapshot.trace,
        })
    }

    fn next_u64(&mut self) -> u64 {
        self.state = self.state.wrapping_add(0x9E37_79B9_7F4A_7C15);
        let mut value = self.state;
        value = (value ^ (value >> 30)).wrapping_mul(0xBF58_476D_1CE4_E5B9);
        value = (value ^ (value >> 27)).wrapping_mul(0x94D0_49BB_1331_11EB);
        value ^ (value >> 31)
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum RngError {
    InvalidSides(u32),
    TraceFull(usize),
    SnapshotSchema(String),
    InvalidTraceEntry {
        label: String,
        sides: u32,
        value: u32,
    },
}

impl fmt::Display for RngError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::InvalidSides(sides) => write!(formatter, "invalid die size {sides}"),
            Self::TraceFull(limit) => write!(formatter, "RNG trace limit exceeded ({limit})"),
            Self::SnapshotSchema(schema) => write!(formatter, "unsupported RNG schema {schema}"),
            Self::InvalidTraceEntry {
                label,
                sides,
                value,
            } => write!(
                formatter,
                "invalid RNG trace entry {label}: {value} on d{sides}"
            ),
        }
    }
}

impl std::error::Error for RngError {}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn same_seed_and_calls_produce_the_same_trace() {
        let mut left = DeterministicRng::new(42);
        let mut right = DeterministicRng::new(42);
        for _ in 0..8 {
            assert_eq!(
                left.d20("attack").expect("roll"),
                right.d20("attack").expect("roll")
            );
        }
        assert_eq!(left.trace(), right.trace());
        assert_eq!(left.state(), right.state());
    }

    #[test]
    fn different_seeds_diverge() {
        let mut left = DeterministicRng::new(1);
        let mut right = DeterministicRng::new(2);
        assert_ne!(left.d20("x").expect("roll"), right.d20("x").expect("roll"));
    }

    #[test]
    fn snapshot_restore_continues_the_same_sequence() {
        let mut rng = DeterministicRng::new(7);
        rng.d20("initiative").expect("roll");
        let snapshot = rng.snapshot();
        let mut restored = DeterministicRng::restore(snapshot).expect("restore");
        assert_eq!(
            rng.d20("attack").expect("roll"),
            restored.d20("attack").expect("roll")
        );
        assert_eq!(rng.trace(), restored.trace());
    }

    #[test]
    fn invalid_die_and_trace_are_refused() {
        let mut rng = DeterministicRng::new(1);
        assert_eq!(rng.roll(1, "bad"), Err(RngError::InvalidSides(1)));
        let mut snapshot = rng.snapshot();
        snapshot.trace.push(RollTrace {
            label: "bad".to_owned(),
            sides: 6,
            value: 7,
        });
        assert!(DeterministicRng::restore(snapshot).is_err());
    }
}
