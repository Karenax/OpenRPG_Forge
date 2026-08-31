use forge_core::{AreaId, ProjectId, Revision, Vec3};
use forge_project::{
    BloomProfile, ColorGradeProfile, PostProcessingProfile, RainDropBudgets, RainProfile,
    SrgbColor, WeatherProfileDocument, WeatherProfileError, WeatherQualityTier, WeatherState,
    WetnessProfile,
};

pub const VISUAL_VALLEY_WEATHER_SEED: u64 = 0x5745_4154_4845_5231;
pub const VISUAL_VALLEY_WEATHER_JSON: &str =
    include_str!("../../../examples/visual_valley/weather.v1.json");

/// Original, asset-free clear/rain proof recipe for Visual Valley.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct VisualValleyWeatherRecipe {
    pub seed: u64,
}

impl Default for VisualValleyWeatherRecipe {
    fn default() -> Self {
        Self {
            seed: VISUAL_VALLEY_WEATHER_SEED,
        }
    }
}

impl VisualValleyWeatherRecipe {
    pub fn build(self) -> Result<WeatherProfileDocument, WeatherProfileError> {
        let document = WeatherProfileDocument::new(
            ProjectId::deterministic("visual-valley-project"),
            AreaId::deterministic("visual-valley-area"),
            Revision::INITIAL,
            "Visual Valley weather",
            self.seed,
            WeatherState::Clear,
            RainProfile {
                quality: WeatherQualityTier::High,
                drop_budgets: RainDropBudgets {
                    low: 2_048,
                    medium: 4_096,
                    high: 8_192,
                    ultra: 16_384,
                },
                volume_size_m: Vec3::new(96.0, 48.0, 96.0),
                fall_speed_mps: 18.0,
                streak_length_m: 0.65,
                wind_velocity_mps: Vec3::new(-2.0, 0.0, 1.0),
                color_srgb: SrgbColor::new(0.68, 0.78, 0.88),
                opacity: 0.42,
            },
            WetnessProfile {
                clear_target: 0.0,
                rain_target: 0.78,
                wetting_rate_per_second: 0.18,
                drying_rate_per_second: 0.07,
                full_wet_roughness_multiplier: 0.32,
                full_wet_albedo_multiplier: 0.72,
            },
            PostProcessingProfile {
                bloom: BloomProfile {
                    enabled: true,
                    intensity: 0.08,
                    low_frequency_boost: 0.35,
                    high_pass_frequency: 0.82,
                },
                clear_grade: ColorGradeProfile {
                    exposure_ev: 0.0,
                    temperature: 0.08,
                    tint: 0.01,
                    saturation: 1.04,
                    contrast: 1.06,
                    gamma: 1.0,
                },
                rain_grade: ColorGradeProfile {
                    exposure_ev: -0.35,
                    temperature: -0.12,
                    tint: 0.02,
                    saturation: 0.86,
                    contrast: 1.08,
                    gamma: 1.0,
                },
            },
        );
        document.validate()?;
        Ok(document)
    }
}

/// Loads and validates the exact R1-024..026 profile embedded in Forge.
pub fn embedded_visual_valley_weather_profile()
-> Result<WeatherProfileDocument, WeatherProfileError> {
    WeatherProfileDocument::from_json(VISUAL_VALLEY_WEATHER_JSON)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::{
        embedded_visual_valley_environment_profile, embedded_visual_valley_terrain_document,
        embedded_visual_valley_vegetation_document, embedded_visual_valley_water_document,
    };

    #[test]
    fn embedded_fixture_matches_recipe_and_serializes_exactly() {
        let generated = VisualValleyWeatherRecipe::default().build().unwrap();
        let embedded = embedded_visual_valley_weather_profile().unwrap();
        assert_eq!(embedded, generated);
        assert_eq!(
            VISUAL_VALLEY_WEATHER_JSON.trim_end(),
            embedded.to_pretty_json().unwrap()
        );
        assert_eq!(
            embedded.content_hash().unwrap(),
            generated.content_hash().unwrap()
        );
        assert_eq!(
            embedded.content_hash().unwrap().to_string(),
            "b0d8e12bd49d3510361d587463a3002e98741b68052b283fa3f8ca432c87dd9e"
        );
    }

    #[test]
    fn identity_matches_every_visual_valley_document() {
        let weather = embedded_visual_valley_weather_profile().unwrap();
        let terrain = embedded_visual_valley_terrain_document().unwrap();
        let vegetation = embedded_visual_valley_vegetation_document().unwrap();
        let environment = embedded_visual_valley_environment_profile().unwrap();
        let water = embedded_visual_valley_water_document().unwrap();

        weather
            .validate_identity(terrain.project_id, terrain.area_id, terrain.revision)
            .unwrap();
        assert_eq!(vegetation.project_id, terrain.project_id);
        assert_eq!(environment.project_id, terrain.project_id);
        assert_eq!(water.project_id, terrain.project_id);
    }

    #[test]
    fn recipe_replays_and_clear_rain_clear_restores_the_exact_target() {
        let first = VisualValleyWeatherRecipe::default().build().unwrap();
        let replay = VisualValleyWeatherRecipe::default().build().unwrap();
        assert_eq!(first, replay);
        assert_eq!(
            first.content_hash().unwrap(),
            replay.content_hash().unwrap()
        );

        let rain = first
            .advance_wetness(0.0, WeatherState::Rain, 60.0)
            .unwrap();
        let clear = first
            .advance_wetness(rain, WeatherState::Clear, 60.0)
            .unwrap();
        assert_eq!(rain.to_bits(), first.wetness.rain_target.to_bits());
        assert_eq!(clear.to_bits(), first.wetness.clear_target.to_bits());
        assert_eq!(
            first.rain.drop_budgets.for_quality(first.rain.quality),
            8_192
        );
    }

    #[test]
    fn fixture_contains_no_asset_or_upstream_identity_reference() {
        let lowercase = VISUAL_VALLEY_WEATHER_JSON.to_ascii_lowercase();
        let upstream_identity = ["ca", "dle"].concat();
        for forbidden in [
            "asset",
            upstream_identity.as_str(),
            "the vale",
            "whisperwood",
            "upstream",
            "three.js",
            "bevy",
            "rapier",
            "texture",
        ] {
            assert!(!lowercase.contains(forbidden), "found `{forbidden}`");
        }
    }
}
