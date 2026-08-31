use serde::{Deserialize, Serialize};
use thiserror::Error;

/// Stable schema name for engine-neutral frame-time distributions.
pub const FRAME_DISTRIBUTION_SCHEMA: &str = "forge.frame-distribution";
/// Current schema version for [`FrameDistribution`].
pub const FRAME_DISTRIBUTION_SCHEMA_VERSION: u32 = 1;
/// Upper bound of the frame-time bucket matching the Gate 1 60 FPS budget.
pub const FRAME_TIME_60_FPS_BUDGET_MS: f64 = 16.7;
/// Upper bound of the frame-time bucket matching the 30 FPS diagnostic boundary.
pub const FRAME_TIME_30_FPS_BUDGET_MS: f64 = 33.3;

/// Exhaustive, non-overlapping frame-time buckets for Gate 1 evidence.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct FrameTimeHistogram {
    /// Frames whose duration is less than or equal to 16.7 ms.
    pub frames_at_or_below_16_7_ms: u32,
    /// Frames over 16.7 ms and less than or equal to 33.3 ms.
    pub frames_over_16_7_ms_at_or_below_33_3_ms: u32,
    /// Frames whose duration is strictly greater than 33.3 ms.
    pub frames_over_33_3_ms: u32,
}

impl FrameTimeHistogram {
    /// Returns the bucket total without risking an intermediate `u32` overflow.
    #[must_use]
    pub const fn total_samples(self) -> u64 {
        self.frames_at_or_below_16_7_ms as u64
            + self.frames_over_16_7_ms_at_or_below_33_3_ms as u64
            + self.frames_over_33_3_ms as u64
    }
}

/// Versioned frame-time distribution shared by native and WASM consumers.
///
/// `one_percent_low_fps` follows the existing Forge convention: take the slowest
/// `ceil(sample_count / 100)` frame times, compute their arithmetic mean, then
/// convert that mean duration to FPS with `1_000 / mean_ms`.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct FrameDistribution {
    pub schema: String,
    pub schema_version: u32,
    pub samples: u32,
    pub duration_ms: f64,
    pub mean_ms: f64,
    pub mean_fps: f64,
    pub p95_ms: f64,
    pub slowest_one_percent_samples: u32,
    pub one_percent_low_fps: f64,
    pub histogram: FrameTimeHistogram,
}

impl FrameDistribution {
    /// Builds a distribution from finite, strictly positive frame times in milliseconds.
    pub fn from_samples_ms(samples_ms: &[f64]) -> Result<Self, FrameDistributionError> {
        if samples_ms.is_empty() {
            return Err(FrameDistributionError::EmptySamples);
        }

        let samples = u32::try_from(samples_ms.len())
            .map_err(|_| FrameDistributionError::TooManySamples(samples_ms.len()))?;
        for (index, sample) in samples_ms.iter().copied().enumerate() {
            if !sample.is_finite() {
                return Err(FrameDistributionError::NonFiniteSample { index });
            }
            if sample <= 0.0 {
                return Err(FrameDistributionError::NonPositiveSample { index });
            }
        }

        let duration_ms = samples_ms.iter().sum::<f64>();
        ensure_positive_finite_metric("duration_ms", duration_ms)?;
        let mean_ms = duration_ms / f64::from(samples);
        ensure_positive_finite_metric("mean_ms", mean_ms)?;
        let mean_fps = 1_000.0 / mean_ms;
        ensure_positive_finite_metric("mean_fps", mean_fps)?;

        let mut sorted = samples_ms.to_vec();
        sorted.sort_by(f64::total_cmp);
        let p95_ms = nearest_rank(&sorted, samples, 95);

        let slowest_one_percent_samples = samples.div_ceil(100).max(1);
        let slowest_mean_ms = sorted
            .iter()
            .rev()
            .take(slowest_one_percent_samples as usize)
            .sum::<f64>()
            / f64::from(slowest_one_percent_samples);
        ensure_positive_finite_metric("slowest_one_percent_mean_ms", slowest_mean_ms)?;
        let one_percent_low_fps = 1_000.0 / slowest_mean_ms;
        ensure_positive_finite_metric("one_percent_low_fps", one_percent_low_fps)?;

        let histogram = histogram(samples_ms)?;
        let distribution = Self {
            schema: FRAME_DISTRIBUTION_SCHEMA.to_owned(),
            schema_version: FRAME_DISTRIBUTION_SCHEMA_VERSION,
            samples,
            duration_ms,
            mean_ms,
            mean_fps,
            p95_ms,
            slowest_one_percent_samples,
            one_percent_low_fps,
            histogram,
        };
        distribution.validate()?;
        Ok(distribution)
    }

    /// Validates schema identity, metric ranges and the histogram/sample invariant.
    pub fn validate(&self) -> Result<(), FrameDistributionError> {
        if self.schema != FRAME_DISTRIBUTION_SCHEMA {
            return Err(FrameDistributionError::InvalidSchema(self.schema.clone()));
        }
        if self.schema_version != FRAME_DISTRIBUTION_SCHEMA_VERSION {
            return Err(FrameDistributionError::UnsupportedSchemaVersion(
                self.schema_version,
            ));
        }
        if self.samples == 0 {
            return Err(FrameDistributionError::EmptySamples);
        }
        ensure_positive_finite_metric("duration_ms", self.duration_ms)?;
        ensure_positive_finite_metric("mean_ms", self.mean_ms)?;
        ensure_positive_finite_metric("mean_fps", self.mean_fps)?;
        ensure_positive_finite_metric("p95_ms", self.p95_ms)?;
        ensure_positive_finite_metric("one_percent_low_fps", self.one_percent_low_fps)?;

        let expected_slowest_samples = self.samples.div_ceil(100).max(1);
        if self.slowest_one_percent_samples != expected_slowest_samples {
            return Err(
                FrameDistributionError::InvalidSlowestOnePercentSampleCount {
                    expected: expected_slowest_samples,
                    actual: self.slowest_one_percent_samples,
                },
            );
        }

        let histogram_samples = self.histogram.total_samples();
        if histogram_samples != u64::from(self.samples) {
            return Err(FrameDistributionError::HistogramSampleCountMismatch {
                samples: self.samples,
                histogram_samples,
            });
        }
        Ok(())
    }

    /// Parses and validates a serialized distribution.
    pub fn from_json(json: &str) -> Result<Self, FrameDistributionError> {
        let distribution: Self = serde_json::from_str(json)?;
        distribution.validate()?;
        Ok(distribution)
    }

    /// Serializes a validated distribution as human-readable JSON.
    pub fn to_pretty_json(&self) -> Result<String, FrameDistributionError> {
        self.validate()?;
        Ok(serde_json::to_string_pretty(self)?)
    }
}

#[derive(Debug, Error)]
pub enum FrameDistributionError {
    #[error("frame distribution requires at least one sample")]
    EmptySamples,
    #[error("frame distribution contains too many samples: {0}")]
    TooManySamples(usize),
    #[error("frame sample at index {index} must be finite")]
    NonFiniteSample { index: usize },
    #[error("frame sample at index {index} must be strictly positive")]
    NonPositiveSample { index: usize },
    #[error("derived frame metric `{field}` must be finite and strictly positive")]
    InvalidMetric { field: &'static str },
    #[error("invalid frame-distribution schema `{0}`")]
    InvalidSchema(String),
    #[error("unsupported frame-distribution schema version {0}")]
    UnsupportedSchemaVersion(u32),
    #[error("slowest one-percent sample count mismatch: expected {expected}, found {actual}")]
    InvalidSlowestOnePercentSampleCount { expected: u32, actual: u32 },
    #[error(
        "frame histogram count mismatch: distribution declares {samples}, buckets total {histogram_samples}"
    )]
    HistogramSampleCountMismatch {
        samples: u32,
        histogram_samples: u64,
    },
    #[error("invalid frame-distribution JSON: {0}")]
    Json(#[from] serde_json::Error),
}

fn ensure_positive_finite_metric(
    field: &'static str,
    value: f64,
) -> Result<(), FrameDistributionError> {
    if value.is_finite() && value > 0.0 {
        Ok(())
    } else {
        Err(FrameDistributionError::InvalidMetric { field })
    }
}

fn nearest_rank(sorted: &[f64], samples: u32, percentile: u32) -> f64 {
    let rank = u64::from(samples)
        .saturating_mul(u64::from(percentile))
        .div_ceil(100);
    let index = usize::try_from(rank.saturating_sub(1)).unwrap_or(usize::MAX);
    sorted[index.min(sorted.len() - 1)]
}

fn histogram(samples_ms: &[f64]) -> Result<FrameTimeHistogram, FrameDistributionError> {
    let mut at_or_below_16_7 = 0_usize;
    let mut over_16_7_at_or_below_33_3 = 0_usize;
    let mut over_33_3 = 0_usize;
    for sample in samples_ms {
        if *sample <= FRAME_TIME_60_FPS_BUDGET_MS {
            at_or_below_16_7 += 1;
        } else if *sample <= FRAME_TIME_30_FPS_BUDGET_MS {
            over_16_7_at_or_below_33_3 += 1;
        } else {
            over_33_3 += 1;
        }
    }

    Ok(FrameTimeHistogram {
        frames_at_or_below_16_7_ms: u32::try_from(at_or_below_16_7)
            .map_err(|_| FrameDistributionError::TooManySamples(samples_ms.len()))?,
        frames_over_16_7_ms_at_or_below_33_3_ms: u32::try_from(over_16_7_at_or_below_33_3)
            .map_err(|_| FrameDistributionError::TooManySamples(samples_ms.len()))?,
        frames_over_33_3_ms: u32::try_from(over_33_3)
            .map_err(|_| FrameDistributionError::TooManySamples(samples_ms.len()))?,
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    fn assert_approx_eq(actual: f64, expected: f64) {
        let tolerance = f64::EPSILON * expected.abs().max(1.0) * 8.0;
        assert!(
            (actual - expected).abs() <= tolerance,
            "expected {expected}, got {actual} (tolerance {tolerance})"
        );
    }

    #[test]
    fn two_hundred_sample_reference_uses_nearest_rank_and_slowest_one_percent() {
        let samples = (1..=200).map(f64::from).collect::<Vec<_>>();
        let distribution = FrameDistribution::from_samples_ms(&samples).unwrap();

        assert_eq!(distribution.schema, FRAME_DISTRIBUTION_SCHEMA);
        assert_eq!(
            distribution.schema_version,
            FRAME_DISTRIBUTION_SCHEMA_VERSION
        );
        assert_eq!(distribution.samples, 200);
        assert_approx_eq(distribution.duration_ms, 20_100.0);
        assert_approx_eq(distribution.mean_ms, 100.5);
        assert_approx_eq(distribution.mean_fps, 1_000.0 / 100.5);
        assert_approx_eq(distribution.p95_ms, 190.0);
        assert_eq!(distribution.slowest_one_percent_samples, 2);
        assert_approx_eq(distribution.one_percent_low_fps, 1_000.0 / 199.5);
        assert_eq!(
            distribution.histogram,
            FrameTimeHistogram {
                frames_at_or_below_16_7_ms: 16,
                frames_over_16_7_ms_at_or_below_33_3_ms: 17,
                frames_over_33_3_ms: 167,
            }
        );
        assert_eq!(distribution.histogram.total_samples(), 200);
        assert!(distribution.validate().is_ok());
    }

    #[test]
    fn one_hundred_sample_reference_preserves_the_existing_studio_convention() {
        let samples = (1..=100).map(f64::from).collect::<Vec<_>>();
        let distribution = FrameDistribution::from_samples_ms(&samples).unwrap();

        assert_eq!(distribution.samples, 100);
        assert_approx_eq(distribution.p95_ms, 95.0);
        assert_eq!(distribution.slowest_one_percent_samples, 1);
        assert_approx_eq(distribution.one_percent_low_fps, 10.0);
    }

    #[test]
    fn histogram_boundaries_are_exhaustive_non_overlapping_and_inclusive() {
        let distribution =
            FrameDistribution::from_samples_ms(&[16.7, 16.700_001, 33.3, 33.300_001]).unwrap();

        assert_eq!(
            distribution.histogram,
            FrameTimeHistogram {
                frames_at_or_below_16_7_ms: 1,
                frames_over_16_7_ms_at_or_below_33_3_ms: 2,
                frames_over_33_3_ms: 1,
            }
        );
        assert_eq!(distribution.histogram.total_samples(), 4);
    }

    #[test]
    fn empty_non_finite_and_non_positive_samples_are_rejected() {
        assert!(matches!(
            FrameDistribution::from_samples_ms(&[]),
            Err(FrameDistributionError::EmptySamples)
        ));
        assert!(matches!(
            FrameDistribution::from_samples_ms(&[16.0, 0.0]),
            Err(FrameDistributionError::NonPositiveSample { index: 1 })
        ));
        assert!(matches!(
            FrameDistribution::from_samples_ms(&[-1.0]),
            Err(FrameDistributionError::NonPositiveSample { index: 0 })
        ));
        for invalid in [f64::NAN, f64::INFINITY, f64::NEG_INFINITY] {
            assert!(matches!(
                FrameDistribution::from_samples_ms(&[invalid]),
                Err(FrameDistributionError::NonFiniteSample { index: 0 })
            ));
        }
    }

    #[test]
    fn json_round_trip_revalidates_version_and_histogram_total() {
        let distribution =
            FrameDistribution::from_samples_ms(&[8.0, 16.7, 24.0, 33.3, 40.0]).unwrap();
        let json = distribution.to_pretty_json().unwrap();
        let restored = FrameDistribution::from_json(&json).unwrap();
        assert_eq!(restored, distribution);

        let mut wrong_version = distribution.clone();
        wrong_version.schema_version += 1;
        assert!(matches!(
            wrong_version.validate(),
            Err(FrameDistributionError::UnsupportedSchemaVersion(2))
        ));

        let mut wrong_histogram = distribution;
        wrong_histogram.histogram.frames_over_33_3_ms += 1;
        assert!(matches!(
            wrong_histogram.validate(),
            Err(FrameDistributionError::HistogramSampleCountMismatch {
                samples: 5,
                histogram_samples: 6
            })
        ));
    }

    #[test]
    fn one_sample_distribution_has_well_defined_percentiles_and_low() {
        let distribution = FrameDistribution::from_samples_ms(&[20.0]).unwrap();

        assert_approx_eq(distribution.mean_ms, 20.0);
        assert_approx_eq(distribution.p95_ms, 20.0);
        assert_eq!(distribution.slowest_one_percent_samples, 1);
        assert_approx_eq(distribution.one_percent_low_fps, 50.0);
    }
}
