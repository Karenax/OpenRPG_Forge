//! Optional, renderer-neutral solar response. This is not a cloud or wetness simulation.
use super::{WeatherProfileError, WeatherState, bounded, positive_bounded};
use serde::{Deserialize, Serialize};

#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WeatherSunlightProfile {
    /// Fraction of the authored clear illuminance retained at the rain endpoint.
    pub rain_illuminance_multiplier: f32,
    /// Duration of a complete clear-to-rain or rain-to-clear lighting transition.
    pub transition_seconds: f32,
}

impl WeatherSunlightProfile {
    pub fn validate(self) -> Result<(), WeatherProfileError> {
        bounded(
            self.rain_illuminance_multiplier,
            0.0,
            1.0,
            "sunlight.rain_illuminance_multiplier",
        )?;
        positive_bounded(
            self.transition_seconds,
            3_600.0,
            "sunlight.transition_seconds",
        )
    }

    /// Pure transition of a separate lighting blend; does not read or change wetness.
    pub fn advance_blend(
        self,
        current: f32,
        target: WeatherState,
        delta_seconds: f32,
    ) -> Result<f32, WeatherProfileError> {
        self.validate()?;
        validate_blend(current)?;
        if !delta_seconds.is_finite() || delta_seconds < 0.0 {
            return Err(WeatherProfileError::InvalidTransitionInput);
        }
        let step = delta_seconds / self.transition_seconds;
        Ok(match target {
            WeatherState::Clear => (current - step).max(0.0),
            WeatherState::Rain => (current + step).min(1.0),
        })
    }

    /// Multiply the immutable authored illuminance, never the previous projected value.
    pub fn illuminance_multiplier(self, rain_blend: f32) -> Result<f32, WeatherProfileError> {
        self.validate()?;
        validate_blend(rain_blend)?;
        if rain_blend == 0.0 {
            return Ok(1.0);
        }
        if rain_blend >= 1.0 {
            return Ok(self.rain_illuminance_multiplier);
        }
        Ok(1.0 + (self.rain_illuminance_multiplier - 1.0) * rain_blend)
    }
}

fn validate_blend(value: f32) -> Result<(), WeatherProfileError> {
    if value.is_finite() && (0.0..=1.0).contains(&value) {
        Ok(())
    } else {
        Err(WeatherProfileError::InvalidTransitionInput)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::WeatherProfileDocument;

    const LEGACY: &str = include_str!("../../../../examples/visual_valley/weather.v1.json");

    fn profile() -> WeatherSunlightProfile {
        WeatherSunlightProfile {
            rain_illuminance_multiplier: 0.25,
            transition_seconds: 4.0,
        }
    }

    fn assert_exact(actual: f32, expected: f32) {
        assert_eq!(actual.to_bits(), expected.to_bits());
    }

    #[test]
    fn absent_sunlight_preserves_the_existing_profile_hash() {
        let mut document = WeatherProfileDocument::from_json(LEGACY).unwrap();
        assert_eq!(document.sunlight, None);
        assert!(!document.to_pretty_json().unwrap().contains("sunlight"));
        // The recorded native WorldSketch proof binds weather to area revision 3.
        // The standalone source file is revision 1, not the profile measured by that run.
        document.revision = forge_core::Revision::new(3);
        assert_eq!(
            document.content_hash().unwrap().to_string(),
            "322ee487ea89a4709bbf101d78cffecc05084d7cfc1e8778c608c2bc7d2a2115"
        );
    }

    #[test]
    fn explicit_sunlight_round_trips_and_changes_the_source_identity() {
        let mut document = WeatherProfileDocument::from_json(LEGACY).unwrap();
        let old_hash = document.content_hash().unwrap();
        document.sunlight = Some(profile());
        let encoded = document.to_pretty_json().unwrap();
        assert_eq!(
            WeatherProfileDocument::from_json(&encoded).unwrap(),
            document
        );
        assert_ne!(document.content_hash().unwrap(), old_hash);
        let unknown = encoded.replace("\"transition_seconds\"", "\"unknown\"");
        assert!(WeatherProfileDocument::from_json(&unknown).is_err());
        document.sunlight = None;
        assert_eq!(document.content_hash().unwrap(), old_hash);
    }

    #[test]
    fn transitions_restore_clear_before_the_material_finishes_drying() {
        let document = WeatherProfileDocument::from_json(LEGACY).unwrap();
        let unchanged = document.content_hash().unwrap();
        let profile = profile();
        let mut blend = 0.0;
        for expected in [0.25, 0.5, 0.75, 1.0] {
            blend = profile
                .advance_blend(blend, WeatherState::Rain, 1.0)
                .unwrap();
            assert_exact(blend, expected);
        }
        assert_exact(profile.illuminance_multiplier(blend).unwrap(), 0.25);
        assert_exact(
            profile
                .advance_blend(blend, WeatherState::Rain, f32::MAX)
                .unwrap(),
            1.0,
        );
        blend = profile
            .advance_blend(blend, WeatherState::Clear, 4.0)
            .unwrap();
        assert_eq!(blend.to_bits(), 0.0_f32.to_bits());
        assert_eq!(
            profile.illuminance_multiplier(blend).unwrap().to_bits(),
            1.0_f32.to_bits()
        );
        assert!(
            document
                .advance_wetness(document.wetness.rain_target, WeatherState::Clear, 4.0)
                .unwrap()
                > 0.0
        );
        assert_eq!(document.content_hash().unwrap(), unchanged);
    }

    #[test]
    fn interrupted_transition_is_continuous_and_zero_delta_does_not_advance() {
        let profile = profile();
        let partial = profile.advance_blend(0.0, WeatherState::Rain, 2.0).unwrap();
        assert_exact(profile.illuminance_multiplier(partial).unwrap(), 0.625);
        assert_exact(
            profile
                .advance_blend(partial, WeatherState::Clear, 0.0)
                .unwrap(),
            partial,
        );
        assert_exact(
            profile
                .advance_blend(partial, WeatherState::Clear, 1.0)
                .unwrap(),
            0.25,
        );
        assert_exact(
            profile
                .advance_blend(partial, WeatherState::Clear, f32::MAX)
                .unwrap(),
            0.0,
        );
    }

    #[test]
    fn invalid_profiles_and_transition_inputs_are_rejected() {
        for value in [f32::NAN, f32::INFINITY, -0.1, 1.1] {
            let mut invalid = profile();
            invalid.rain_illuminance_multiplier = value;
            let mut document = WeatherProfileDocument::from_json(LEGACY).unwrap();
            document.sunlight = Some(invalid);
            assert!(document.validate().is_err());
            assert!(document.content_hash().is_err());
            assert!(profile().illuminance_multiplier(value).is_err());
            assert!(
                profile()
                    .advance_blend(value, WeatherState::Rain, 1.0)
                    .is_err()
            );
        }
        for value in [f32::NAN, f32::INFINITY, -1.0, 0.0, 3_601.0] {
            let mut invalid = profile();
            invalid.transition_seconds = value;
            assert!(invalid.validate().is_err());
        }
        for value in [f32::NAN, f32::INFINITY, -1.0] {
            assert!(
                profile()
                    .advance_blend(0.0, WeatherState::Rain, value)
                    .is_err()
            );
        }
        for endpoint in [0.0, 1.0] {
            let mut valid = profile();
            valid.rain_illuminance_multiplier = endpoint;
            assert!(valid.validate().is_ok());
        }
    }
}
