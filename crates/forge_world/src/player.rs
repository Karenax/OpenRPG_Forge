use forge_project::{
    PlayerCameraProfile, PlayerControllerProfile, PlayerControllerProfileDocument,
    PlayerControllerProfileError,
};

pub const VISUAL_VALLEY_PLAYER_CONTROLLER_JSON: &str =
    include_str!("../../../examples/visual_valley/player_controller.v1.json");

/// Original, asset-free recipe for the Visual Valley player and camera profile.
#[derive(Clone, Copy, Debug, Default, Eq, PartialEq)]
pub struct VisualValleyPlayerControllerRecipe;

impl VisualValleyPlayerControllerRecipe {
    pub fn build(self) -> Result<PlayerControllerProfileDocument, PlayerControllerProfileError> {
        let document = PlayerControllerProfileDocument::new(
            "Visual Valley player controller",
            PlayerControllerProfile {
                capsule_radius_m: 0.35,
                capsule_half_segment_m: 0.55,
                walk_speed_mps: 5.0,
                sprint_multiplier: 1.6,
                acceleration_mps2: 24.0,
                braking_mps2: 32.0,
                gravity_mps2: -24.0,
                jump_speed_mps: 8.0,
                max_step_height_m: 0.4,
                min_step_width_m: 0.35,
                ground_snap_m: 0.25,
                max_slope_climb_degrees: 45.0,
                min_slope_slide_degrees: 50.0,
            },
            PlayerCameraProfile {
                eye_height_m: 1.62,
                pivot_height_m: 1.5,
                third_person_distance_m: 4.5,
                third_person_min_distance_m: 0.6,
                collision_radius_m: 0.18,
                recovery_speed_mps: 8.0,
                pitch_min_degrees: -70.0,
                pitch_max_degrees: 70.0,
                third_person_vertical_fov_degrees: 60.0,
                first_person_vertical_fov_degrees: 75.0,
                look_sensitivity_degrees_per_unit: 0.12,
            },
        );
        document.validate()?;
        Ok(document)
    }
}

/// Loads and validates the exact Visual Valley player-controller fixture embedded in Forge.
pub fn embedded_visual_valley_player_controller_profile()
-> Result<PlayerControllerProfileDocument, PlayerControllerProfileError> {
    PlayerControllerProfileDocument::from_json(VISUAL_VALLEY_PLAYER_CONTROLLER_JSON)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn embedded_fixture_matches_the_recipe_and_human_scale_capsule() {
        let generated = VisualValleyPlayerControllerRecipe.build().unwrap();
        let embedded = embedded_visual_valley_player_controller_profile().unwrap();

        assert_eq!(embedded, generated);
        assert_eq!(
            embedded.controller.total_capsule_height_m().to_bits(),
            1.8_f32.to_bits()
        );
        assert_eq!(
            embedded.to_pretty_json().unwrap(),
            generated.to_pretty_json().unwrap()
        );
        assert_eq!(
            VISUAL_VALLEY_PLAYER_CONTROLLER_JSON.trim_end(),
            embedded.to_pretty_json().unwrap()
        );
        assert_eq!(
            embedded.content_hash().unwrap(),
            generated.content_hash().unwrap()
        );
    }

    #[test]
    fn recipe_and_fixture_hash_are_deterministic() {
        let first = VisualValleyPlayerControllerRecipe.build().unwrap();
        let replay = VisualValleyPlayerControllerRecipe.build().unwrap();
        let fixture = embedded_visual_valley_player_controller_profile().unwrap();

        assert_eq!(first, replay);
        assert_eq!(
            first.content_hash().unwrap(),
            replay.content_hash().unwrap()
        );
        assert_eq!(
            fixture.content_hash().unwrap().to_string(),
            "941eceefb29835c0949a9801f83f421839bcb5b0cd79583899c22d6832a74f90"
        );
    }

    #[test]
    fn fixture_contains_no_asset_or_upstream_identity_reference() {
        let lowercase = VISUAL_VALLEY_PLAYER_CONTROLLER_JSON.to_ascii_lowercase();
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
