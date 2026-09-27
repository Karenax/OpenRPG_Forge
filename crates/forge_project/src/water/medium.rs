//! Optional renderer-neutral homogeneous optical medium for `WaterDocument` v1.
use super::WaterDocumentError;
use serde::{Deserialize, Serialize};

/// Homogeneous RGB approximation, not measured spectral water data.
/// Coefficients are inverse metres in linear light, never sRGB colours.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WaterMediumProfile {
    pub absorption_per_m: [f32; 3],
    pub scattering_per_m: [f32; 3],
}

impl WaterMediumProfile {
    /// Bound imported coefficients before evaluating or projecting them.
    pub fn validate(self) -> Result<(), WaterDocumentError> {
        for (values, field) in [
            (self.absorption_per_m, "medium.absorption_per_m"),
            (self.scattering_per_m, "medium.scattering_per_m"),
        ] {
            if values
                .iter()
                .any(|v| !v.is_finite() || !(0.0..=100.0).contains(v))
            {
                return Err(WaterDocumentError::InvalidNumericValue(field));
            }
        }
        Ok(())
    }

    /// Beer-Lambert extinction for a homogeneous segment; no surface reflection.
    pub fn transmittance(self, distance_m: f32) -> Result<[f32; 3], WaterDocumentError> {
        self.validate()?;
        if !distance_m.is_finite() || !(0.0..=100_000.0).contains(&distance_m) {
            return Err(WaterDocumentError::InvalidNumericValue("medium.distance_m"));
        }
        Ok(std::array::from_fn(|i| {
            (-(self.absorption_per_m[i] + self.scattering_per_m[i]) * distance_m).exp()
        }))
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn medium_preserves_clear_limit_and_attenuates_by_distance_and_channel() {
        let medium = WaterMediumProfile {
            absorption_per_m: [0.8, 0.2, 0.1],
            scattering_per_m: [0.1; 3],
        };
        assert_eq!(
            medium.transmittance(0.0).unwrap().map(f32::to_bits),
            [1.0_f32.to_bits(); 3]
        );
        let one = medium.transmittance(1.0).unwrap();
        let two = medium.transmittance(2.0).unwrap();
        assert!(one[0] < one[1] && one[1] < one[2]);
        for i in 0..3 {
            assert!(two[i] > 0.0 && two[i] < one[i]);
            assert!((two[i] - one[i] * one[i]).abs() < 1e-6);
        }
        let clear = WaterMediumProfile {
            absorption_per_m: [0.0; 3],
            scattering_per_m: [0.0; 3],
        };
        assert_eq!(
            clear.transmittance(100_000.0).unwrap().map(f32::to_bits),
            [1.0_f32.to_bits(); 3]
        );
        let json = serde_json::to_string(&medium).unwrap();
        assert_eq!(
            serde_json::from_str::<WaterMediumProfile>(&json).unwrap(),
            medium
        );
        assert!(
            serde_json::from_str::<WaterMediumProfile>(&json.replace("absorption_per_m", "colour"))
                .is_err()
        );
    }

    #[test]
    fn invalid_optical_inputs_are_rejected_without_clamping() {
        let clear = WaterMediumProfile {
            absorption_per_m: [0.0; 3],
            scattering_per_m: [0.0; 3],
        };
        for invalid in [-1.0, 100.1, f32::NAN, f32::INFINITY] {
            for channel in 0..3 {
                let mut medium = clear;
                medium.absorption_per_m[channel] = invalid;
                assert!(medium.transmittance(1.0).is_err());
                medium = clear;
                medium.scattering_per_m[channel] = invalid;
                assert!(medium.transmittance(1.0).is_err());
            }
        }
        for invalid in [-1.0, 100_001.0, f32::NAN, f32::INFINITY] {
            assert!(clear.transmittance(invalid).is_err());
        }
    }
}
