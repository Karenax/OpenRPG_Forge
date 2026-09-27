//! Prepared cloud-layer parameters; not yet part of a persisted environment document.
use serde::{Deserialize, Serialize};
use thiserror::Error;

/// Provisional budget for the opt-in cloud preview, not a persisted preset or GPU acknowledgement.
#[must_use]
pub const fn cloud_preview_view_steps(tier: crate::VisualQualityTier) -> u8 {
    match tier {
        crate::VisualQualityTier::Low => 0,
        crate::VisualQualityTier::Medium => 12,
        crate::VisualQualityTier::High => 24,
        crate::VisualQualityTier::Ultra => 32,
    }
}

/// Engine-neutral parameters for a bounded horizontal cloud layer, not a volumetric weather model.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct CloudLayerSettings {
    pub seed: u64,
    pub altitude_m: f32,
    pub pattern_scale_m: f32,
    pub coverage: f32,
    pub opacity: f32,
    pub wind_xz_m_per_second: [f32; 2],
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Error)]
#[error("cloud layer has an invalid or out-of-budget field: {0}")]
pub struct CloudLayerSettingsError(pub &'static str);

impl CloudLayerSettings {
    pub fn validate(self) -> Result<(), CloudLayerSettingsError> {
        for (value, minimum, maximum, field) in [
            (self.altitude_m, 100.0, 10_000.0, "altitude_m"),
            (self.pattern_scale_m, 100.0, 20_000.0, "pattern_scale_m"),
            (self.coverage, 0.0, 1.0, "coverage"),
            (self.opacity, 0.0, 1.0, "opacity"),
            (
                self.wind_xz_m_per_second[0],
                -100.0,
                100.0,
                "wind_x_m_per_second",
            ),
            (
                self.wind_xz_m_per_second[1],
                -100.0,
                100.0,
                "wind_z_m_per_second",
            ),
        ] {
            if !value.is_finite() || !(minimum..=maximum).contains(&value) {
                return Err(CloudLayerSettingsError(field));
            }
        }
        Ok(())
    }

    /// Stable periodic texture displacement. Wrapping bounds precision over long play sessions.
    pub fn texture_offset(self, seconds: f64) -> Result<[f32; 2], CloudLayerSettingsError> {
        self.validate()?;
        if !seconds.is_finite() || seconds < 0.0 {
            return Err(CloudLayerSettingsError("seconds"));
        }
        let mut result = [0.0; 2];
        for (output, speed) in result.iter_mut().zip(self.wind_xz_m_per_second) {
            if speed != 0.0 {
                let period = f64::from(self.pattern_scale_m) / f64::from(speed).abs();
                let phase = (seconds % period) / period * f64::from(speed.signum());
                #[allow(
                    clippy::cast_possible_truncation,
                    reason = "phase is bounded to one texture period"
                )]
                {
                    *output = phase.rem_euclid(1.0) as f32;
                }
            }
        }
        Ok(result)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn settings() -> CloudLayerSettings {
        CloudLayerSettings {
            seed: 1337,
            altitude_m: 1_500.0,
            pattern_scale_m: 4_000.0,
            coverage: 0.45,
            opacity: 0.8,
            wind_xz_m_per_second: [10.0, -5.0],
        }
    }

    #[test]
    fn parameters_round_trip_without_engine_types() {
        let source = settings();
        let json = serde_json::to_string(&source).unwrap();
        assert_eq!(source, serde_json::from_str(&json).unwrap());
        assert!(source.validate().is_ok());
        assert!(
            serde_json::from_str::<CloudLayerSettings>(&json.replace("\"seed\"", "\"unknown\""))
                .is_err()
        );
    }

    #[test]
    fn rejects_nonfinite_and_out_of_budget_inputs() {
        for value in [f32::NAN, f32::INFINITY, -0.01, 1.01] {
            assert!(
                CloudLayerSettings {
                    coverage: value,
                    ..settings()
                }
                .validate()
                .is_err()
            );
        }
        assert!(
            CloudLayerSettings {
                altitude_m: 0.0,
                ..settings()
            }
            .validate()
            .is_err()
        );
        assert!(
            CloudLayerSettings {
                pattern_scale_m: 0.0,
                ..settings()
            }
            .validate()
            .is_err()
        );
        assert!(
            CloudLayerSettings {
                wind_xz_m_per_second: [101.0, 0.0],
                ..settings()
            }
            .validate()
            .is_err()
        );
    }

    #[test]
    fn wind_replays_and_wraps_without_unbounded_offsets() {
        let source = settings();
        assert_eq!(
            source.texture_offset(100.0).unwrap().map(f32::to_bits),
            [0.25_f32, 0.875].map(f32::to_bits)
        );
        assert_eq!(
            source.texture_offset(100.0).unwrap().map(f32::to_bits),
            source.texture_offset(900.0).unwrap().map(f32::to_bits)
        );
        assert!(
            source
                .texture_offset(f64::MAX)
                .unwrap()
                .iter()
                .all(|v| v.is_finite() && (0.0..=1.0).contains(v))
        );
        assert!(source.texture_offset(-1.0).is_err());
        assert!(source.texture_offset(f64::NAN).is_err());
        assert_eq!(
            CloudLayerSettings {
                wind_xz_m_per_second: [0.0; 2],
                ..source
            }
            .texture_offset(100.0)
            .unwrap()
            .map(f32::to_bits),
            [0.0_f32; 2].map(f32::to_bits)
        );
    }
}
