use forge_core::{AreaId, ProjectId, Revision, Vec3};
use forge_project::{
    DistanceFogFalloff, DistanceFogProfile, EnvironmentProfileDocument, EnvironmentProfileError,
    EnvironmentQualityTier, SkyProfile, SrgbColor, SunProfile, VolumetricFogFallback,
    VolumetricFogProfile,
};

pub const VISUAL_VALLEY_ENVIRONMENT_JSON: &str =
    include_str!("../../../examples/visual_valley/environment.v1.json");

/// Original, asset-free recipe for the Visual Valley daylight and fog profile.
#[derive(Clone, Copy, Debug, Default, Eq, PartialEq)]
pub struct VisualValleyEnvironmentRecipe;

impl VisualValleyEnvironmentRecipe {
    pub fn build(self) -> Result<EnvironmentProfileDocument, EnvironmentProfileError> {
        let document = EnvironmentProfileDocument::new(
            ProjectId::deterministic("visual-valley-project"),
            AreaId::deterministic("visual-valley-area"),
            Revision::INITIAL,
            "Visual Valley environment",
            SkyProfile {
                enabled: true,
                quality: EnvironmentQualityTier::High,
                fallback_clear_color_srgb: SrgbColor::new(0.08, 0.14, 0.24),
                medium_density_multiplier: 1.0,
                environment_light_intensity: 180.0,
                environment_resolution: 256,
                exposure_ev100: 12.0,
                aerial_view_max_distance_m: 1_600.0,
            },
            SunProfile {
                azimuth_degrees: -52.0,
                elevation_degrees: 38.0,
                color_srgb: SrgbColor::new(1.0, 0.93, 0.78),
                illuminance_lux: 55_000.0,
                disk_angular_diameter_degrees: 0.53,
                disk_intensity: 1.0,
                shadow_distance_m: 128.0,
            },
            DistanceFogProfile {
                enabled: true,
                color_srgb: SrgbColor::new(0.64, 0.72, 0.78),
                maximum_opacity: 0.82,
                directional_light_color_srgb: SrgbColor::new(1.0, 0.87, 0.68),
                directional_light_exponent: 8.0,
                falloff: DistanceFogFalloff::Atmospheric {
                    visibility_distance_m: 640.0,
                    contrast_threshold: 0.05,
                },
            },
            VolumetricFogProfile {
                enabled: true,
                quality: EnvironmentQualityTier::High,
                step_count: 64,
                jitter: true,
                ambient_color_srgb: SrgbColor::new(0.56, 0.65, 0.72),
                ambient_intensity: 0.18,
                volume_center_m: Vec3::new(0.0, 32.0, 0.0),
                volume_size_m: Vec3::new(512.0, 192.0, 512.0),
                density_factor: 0.035,
                absorption: 0.08,
                scattering: 0.22,
                scattering_asymmetry: 0.35,
                maximum_distance_m: 320.0,
                fallback: VolumetricFogFallback::DistanceFog,
            },
        );
        document.validate()?;
        Ok(document)
    }
}

/// Loads and validates the exact R1-019..021 fixture embedded in Forge.
pub fn embedded_visual_valley_environment_profile()
-> Result<EnvironmentProfileDocument, EnvironmentProfileError> {
    EnvironmentProfileDocument::from_json(VISUAL_VALLEY_ENVIRONMENT_JSON)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::{
        embedded_visual_valley_terrain_document, embedded_visual_valley_vegetation_document,
    };
    use forge_project::{EnvironmentFogResolution, VolumetricFogCapability};

    #[test]
    fn embedded_fixture_matches_recipe_and_serializes_exactly() {
        let generated = VisualValleyEnvironmentRecipe.build().unwrap();
        let embedded = embedded_visual_valley_environment_profile().unwrap();

        assert_eq!(embedded, generated);
        assert_eq!(
            VISUAL_VALLEY_ENVIRONMENT_JSON.trim_end(),
            embedded.to_pretty_json().unwrap()
        );
        assert_eq!(
            embedded.content_hash().unwrap(),
            generated.content_hash().unwrap()
        );
        assert_eq!(
            embedded.content_hash().unwrap().to_string(),
            "4b252f036887ec6dbe6ae0942862d15bdba589e7deab92e62a9091e21b6cdd6f"
        );
    }

    #[test]
    fn identity_matches_the_visual_valley_terrain_and_vegetation_revision() {
        let environment = embedded_visual_valley_environment_profile().unwrap();
        let terrain = embedded_visual_valley_terrain_document().unwrap();
        let vegetation = embedded_visual_valley_vegetation_document().unwrap();

        environment
            .validate_identity(terrain.project_id, terrain.area_id, terrain.revision)
            .unwrap();
        assert_eq!(vegetation.project_id, terrain.project_id);
        assert_eq!(vegetation.area_id, terrain.area_id);
        assert_eq!(vegetation.revision, terrain.revision);
    }

    #[test]
    fn recipe_is_deterministic_and_fallback_path_is_observable() {
        let first = VisualValleyEnvironmentRecipe.build().unwrap();
        let replay = VisualValleyEnvironmentRecipe.build().unwrap();

        assert_eq!(first, replay);
        assert_eq!(
            first.content_hash().unwrap(),
            replay.content_hash().unwrap()
        );
        assert_eq!(
            first.resolve_fog_path(VolumetricFogCapability::Available),
            EnvironmentFogResolution::Volumetric { step_count: 64 }
        );
        assert_eq!(
            first.resolve_fog_path(VolumetricFogCapability::Unavailable),
            EnvironmentFogResolution::DistanceFogFallback {
                requested_step_count: 64,
                capability: VolumetricFogCapability::Unavailable,
            }
        );
    }

    #[test]
    fn fixture_contains_no_asset_or_upstream_identity_reference() {
        let lowercase = VISUAL_VALLEY_ENVIRONMENT_JSON.to_ascii_lowercase();
        for forbidden in [
            "asset",
            "cadle",
            "the vale",
            "whisperwood",
            "upstream",
            "three.js",
            "bevy",
            "rapier",
        ] {
            assert!(!lowercase.contains(forbidden), "found `{forbidden}`");
        }
    }
}
