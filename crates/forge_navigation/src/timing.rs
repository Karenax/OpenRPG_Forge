//! Disposable elapsed-time observations; never part of canonical tile geometry.
use serde::{Deserialize, Serialize};

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum ElapsedStatus {
    Measured,
    BelowResolution,
    Unavailable,
    NotRequested,
}

#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ElapsedObservation {
    pub milliseconds: Option<f64>,
    pub raw_zero: bool,
    pub status: ElapsedStatus,
}

impl ElapsedObservation {
    #[must_use]
    pub const fn not_requested() -> Self {
        Self {
            milliseconds: None,
            raw_zero: false,
            status: ElapsedStatus::NotRequested,
        }
    }

    #[must_use]
    pub fn measured(milliseconds: f64, coarse_zero: bool) -> Self {
        if !milliseconds.is_finite() || milliseconds < 0.0 {
            return Self {
                milliseconds: None,
                raw_zero: false,
                status: ElapsedStatus::Unavailable,
            };
        }
        let raw_zero = milliseconds == 0.0;
        Self {
            milliseconds: (!raw_zero || !coarse_zero).then_some(milliseconds),
            raw_zero,
            status: if raw_zero && coarse_zero {
                ElapsedStatus::BelowResolution
            } else {
                ElapsedStatus::Measured
            },
        }
    }

    pub fn validate(self) -> Result<(), String> {
        let valid = match (self.status, self.milliseconds) {
            (ElapsedStatus::Measured, Some(value)) => {
                value.is_finite() && value >= 0.0 && self.raw_zero == (value == 0.0)
            }
            (ElapsedStatus::BelowResolution, None) => self.raw_zero,
            (ElapsedStatus::Unavailable | ElapsedStatus::NotRequested, None) => !self.raw_zero,
            _ => false,
        };
        if valid {
            Ok(())
        } else {
            Err("invalid elapsed observation".into())
        }
    }
}

/// Versioned worker transport envelope, separate from the historical `BuiltTile` export.
#[derive(Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ObservedNavigationTile {
    pub schema: String,
    pub version: u32,
    pub tile: crate::BuiltTile,
    pub generation: ElapsedObservation,
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn zero_absent_and_coarse_zero_remain_distinct() {
        let zero = ElapsedObservation::measured(0.0, false);
        assert_eq!(zero.milliseconds, Some(0.0));
        assert_eq!(zero.status, ElapsedStatus::Measured);
        let coarse = ElapsedObservation::measured(0.0, true);
        assert_eq!(coarse.milliseconds, None);
        assert!(coarse.raw_zero);
        assert_eq!(coarse.status, ElapsedStatus::BelowResolution);
        assert_eq!(ElapsedObservation::not_requested().milliseconds, None);
        for observation in [zero, coarse, ElapsedObservation::not_requested()] {
            observation.validate().unwrap();
            let json = serde_json::to_vec(&observation).unwrap();
            assert_eq!(
                serde_json::from_slice::<ElapsedObservation>(&json).unwrap(),
                observation
            );
        }
    }

    #[test]
    fn invalid_measurements_never_become_zero() {
        for value in [f64::NAN, f64::INFINITY, -1.0] {
            let observation = ElapsedObservation::measured(value, false);
            assert_eq!(observation.status, ElapsedStatus::Unavailable);
            assert!(!observation.raw_zero);
            observation.validate().unwrap();
        }
        assert!(
            ElapsedObservation {
                milliseconds: Some(-1.0),
                raw_zero: false,
                status: ElapsedStatus::Measured
            }
            .validate()
            .is_err()
        );
    }
}
