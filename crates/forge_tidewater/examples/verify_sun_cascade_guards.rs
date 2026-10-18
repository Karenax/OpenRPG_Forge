//! Invalid inputs must not advance cascade scheduling or replace valid matrices.
use bevy::math::{DMat4, DVec3};
use forge_tidewater::sun_cascades::{CascadeConfig, CascadeView, SunCascades};

fn reject_configs() {
    let default = CascadeConfig::default();
    let configs = [
        CascadeConfig {
            size: 0,
            ..default.clone()
        },
        CascadeConfig {
            size: 65,
            ..default.clone()
        },
        CascadeConfig {
            splits: vec![],
            ..default.clone()
        },
        CascadeConfig {
            splits: vec![10.0, 10.0, 400.0],
            ..default.clone()
        },
        CascadeConfig {
            splits: vec![10.0, f64::NAN, 400.0],
            ..default.clone()
        },
        CascadeConfig {
            light_margin: f64::INFINITY,
            ..default.clone()
        },
        CascadeConfig {
            normal_bias: vec![0.1],
            ..default.clone()
        },
        CascadeConfig {
            normal_bias: vec![0.1, -0.1, 0.1],
            ..default.clone()
        },
    ];
    for config in configs {
        assert!(SunCascades::new(config).is_err());
    }
}

fn main() -> Result<(), &'static str> {
    reject_configs();
    let default = CascadeConfig::default();
    let view = CascadeView {
        world_from_view: DMat4::IDENTITY,
        near: 0.06,
        fov_degrees: 60.0,
        aspect: 16.0 / 9.0,
    };
    let sun = DVec3::new(0.3, 0.6, -0.7).normalize();
    let mut actual = SunCascades::new(default.clone())?;
    let mut control = SunCascades::new(default)?;
    actual.update(view, sun, true)?;
    control.update(view, sun, true)?;
    let invalid_views = [
        CascadeView {
            near: f64::NAN,
            ..view
        },
        CascadeView { near: 0.0, ..view },
        CascadeView {
            aspect: 0.0,
            ..view
        },
        CascadeView {
            fov_degrees: 180.0,
            ..view
        },
        CascadeView {
            world_from_view: DMat4::from_scale(DVec3::splat(2.0)),
            ..view
        },
        CascadeView {
            world_from_view: DMat4::from_scale(DVec3::new(-1.0, 1.0, 1.0)),
            ..view
        },
        CascadeView {
            world_from_view: DMat4::from_translation(DVec3::splat(f64::INFINITY)),
            ..view
        },
    ];
    for invalid in invalid_views {
        assert!(actual.update(invalid, sun, false).is_err());
    }
    for invalid in [DVec3::ZERO, DVec3::splat(f64::NAN), sun * 2.0] {
        assert!(actual.update(view, invalid, false).is_err());
    }
    assert!(actual.sampling_enabled);
    // A rejected request must not shift the staggered periods, even when it
    // requested disabled sampling. Compare subsequent valid frames to a control.
    for _ in 0..8 {
        assert_eq!(
            actual.update(view, sun, true)?,
            control.update(view, sun, true)?
        );
        for (a, b) in actual.cascades().iter().zip(control.cascades()) {
            assert_eq!(
                a.expect("initialized").clip_from_world,
                b.expect("initialized").clip_from_world
            );
        }
    }
    actual.invalidate();
    assert_eq!(actual.update(view, sun, true)?, vec![0, 1, 2]);
    println!(
        "18 invalid inputs rejected; subsequent scheduling/matrices unchanged; invalidation refreshes all layers"
    );
    Ok(())
}
