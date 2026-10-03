use super::*;

#[test]
fn building_light_anchor_preserves_translation_yaw_and_scale_without_substitution() {
    let yaw = 1.234_f64;
    let frame = Frame {
        translation_m: [12.0, 0.0, -34.0],
        rotation_xyzw: [0.0, (yaw / 2.0).sin(), 0.0, (yaw / 2.0).cos()],
        scale: [1.0, 1.0, 1.0],
    };
    let captured = DMat4::from_scale_rotation_translation(
        DVec3::ONE,
        DQuat::from_rotation_y(yaw),
        DVec3::from_array(frame.translation_m),
    )
    .to_cols_array();
    assert!(verify_anchor(&frame, captured).is_ok());
    for index in [0, 5, 12, 14] {
        let mut invalid = captured;
        invalid[index] += 0.001;
        assert!(verify_anchor(&frame, invalid).is_err());
    }
    let mut invalid = captured;
    invalid[2] = f64::NAN;
    assert!(verify_anchor(&frame, invalid).is_err());
    let singular = Frame {
        scale: [0.0, 1.0, 1.0],
        ..frame
    };
    assert!(verify_anchor(&singular, captured).is_err());
}
