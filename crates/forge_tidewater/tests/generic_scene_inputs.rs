//! Independent scenes exercise shared renderer inputs without imported map data.
use bevy::prelude::App;
use forge_tidewater::{
    exposure_metering::{ExposureProfile, center_weighted_metering_mask},
    water_height_queries::Queries,
};

#[test]
fn unrelated_scene_query_registries_do_not_share_slots_or_positions() {
    let mut cave = App::new();
    let mut mountain = App::new();
    cave.init_resource::<Queries>();
    mountain.init_resource::<Queries>();
    let cave_queries = cave.world().resource::<Queries>();
    let mountain_queries = mountain.world().resource::<Queries>();
    assert_eq!(cave_queries.allocate("ferry", 2), Ok(1));
    assert_eq!(mountain_queries.allocate("weather-station", 1), Ok(1));
    cave_queries.set_point(1, -83.0, 42.0).unwrap();
    mountain_queries.set_point(1, 712.0, -909.0).unwrap();
    assert_eq!(cave_queries.snapshot().count, 3);
    assert_eq!(mountain_queries.snapshot().count, 2);
    assert_ne!(
        cave_queries.snapshot().points[1].map(f32::to_bits),
        mountain_queries.snapshot().points[1].map(f32::to_bits)
    );
    assert!(mountain_queries.set_point(2, 0.0, 0.0).is_err());
    assert!(cave_queries.set_point(1, f32::NAN, 0.0).is_err());
}

#[test]
fn exposure_supports_independent_scene_profiles_and_refuses_invalid_inputs() {
    let cave = ExposureProfile {
        target_luminance: 0.125,
        max_multiplier: 12.0,
        ..Default::default()
    };
    let mountain = ExposureProfile {
        target_luminance: 0.5,
        dark_adaptation: 1.0,
        ..Default::default()
    };
    assert!(cave.compensation_curve().is_ok());
    mountain
        .compensation_curve()
        .expect("independent mountain profile");
    assert!(
        ExposureProfile {
            target_luminance: f32::NAN,
            ..cave
        }
        .compensation_curve()
        .is_err()
    );
    assert!(
        ExposureProfile {
            dark_adaptation: 0.0,
            ..cave
        }
        .compensation_curve()
        .is_err()
    );
    assert!(
        ExposureProfile {
            min_multiplier: 1.0,
            ..cave
        }
        .compensation_curve()
        .is_err()
    );
    assert!(
        ExposureProfile {
            max_multiplier: f32::INFINITY,
            ..cave
        }
        .compensation_curve()
        .is_err()
    );
    let mask = center_weighted_metering_mask();
    let pixels = mask.data.as_ref().unwrap();
    assert_eq!(pixels.len(), 64 * 64);
    assert!(pixels[32 * 64 + 32] > pixels[0]);
}

#[test]
fn independent_motion_profiles_control_gravity_speed_and_transitions() {
    use bevy::prelude::{Vec2, Vec3};
    use forge_tidewater::amphibious_locomotion::{Input, MotionProfile, State};
    let mut cave = State::new(Vec3::ZERO);
    let mut plateau = State::new(Vec3::ZERO);
    cave.set_profile(
        MotionProfile {
            walk_speed_mps: 1.0,
            gravity_magnitude_mps2: 2.0,
            ..Default::default()
        }
        .validate()
        .unwrap(),
    );
    plateau.set_profile(
        MotionProfile {
            walk_speed_mps: 5.0,
            gravity_magnitude_mps2: 12.0,
            ..Default::default()
        }
        .validate()
        .unwrap(),
    );
    let input = Input {
        move_axis: Vec2::Y,
        ..Default::default()
    };
    let _ = cave.advance(0.1, input, -10.0);
    let _ = plateau.advance(0.1, input, -10.0);
    assert!(plateau.velocity.z.abs() > cave.velocity.z.abs());
    assert!(plateau.velocity.y < cave.velocity.y);
    for bad in [f32::NAN, f32::INFINITY, -1.0, 0.0] {
        assert!(
            MotionProfile {
                walk_speed_mps: bad,
                ..Default::default()
            }
            .validate()
            .is_err()
        );
    }
    assert!(
        MotionProfile {
            swim_entry_depth_m: 0.5,
            ..Default::default()
        }
        .validate()
        .is_err()
    );
}

#[test]
fn independent_wind_clocks_preserve_their_step_policy_when_wind_changes() {
    use forge_tidewater::{pendulum_motion::Wind, scene_wind::WindFrame};
    let mut cave = WindFrame::with_max_delta(
        0.0,
        Wind {
            speed: 1.0,
            direction: [0.0, 1.0],
        },
        0.05,
    )
    .unwrap();
    let mut plateau = WindFrame::with_max_delta(
        9.0,
        Wind {
            speed: 12.0,
            direction: [1.0, 0.0],
        },
        0.25,
    )
    .unwrap();
    cave.advance(1.0).unwrap();
    plateau.advance(1.0).unwrap();
    assert_eq!(cave.time_seconds().to_bits(), 0.05_f64.to_bits());
    assert_eq!(plateau.time_seconds().to_bits(), 9.25_f64.to_bits());
    plateau
        .set_wind(Wind {
            speed: 4.0,
            direction: [0.0, -1.0],
        })
        .unwrap();
    plateau.advance(1.0).unwrap();
    assert_eq!(plateau.time_seconds().to_bits(), 9.5_f64.to_bits());
    assert!(WindFrame::with_max_delta(0.0, cave.wind(), f64::NAN).is_err());
    assert!(WindFrame::with_max_delta(0.0, cave.wind(), 0.0).is_err());
    assert!(
        WindFrame::with_max_delta(
            0.0,
            Wind {
                speed: 1e100,
                direction: [1.0, 0.0]
            },
            0.1
        )
        .is_err()
    );
}

#[test]
fn optical_flare_has_no_implicit_imported_material_dependency() {
    use bevy::{
        asset::{AssetApp, AssetPlugin},
        prelude::*,
    };
    use {forge_engine::lens_flare_sun::SceneSun, forge_tidewater::lens_flare::LensFlarePlugin};
    let mut app = App::new();
    app.add_plugins((MinimalPlugins, AssetPlugin::default()));
    app.init_asset::<Shader>();
    app.add_plugins(LensFlarePlugin);
    app.insert_resource(SceneSun::new(Vec3::Y, Vec3::new(1.0, 3.0, 5.0)).unwrap());
    app.update();
    assert!(
        !app.world()
            .contains_resource::<Assets<forge_tidewater::tidewater_material::VillageMaterial>>()
    );
    assert!(SceneSun::new(Vec3::ZERO, Vec3::ONE).is_err());
    assert!(SceneSun::new(Vec3::Y, Vec3::splat(-1.0)).is_err());
    assert!(SceneSun::new(Vec3::Y, Vec3::splat(f32::NAN)).is_err());
}
