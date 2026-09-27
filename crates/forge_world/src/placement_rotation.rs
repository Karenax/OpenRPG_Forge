//! World-axis rotation gestures, independent of camera APIs and renderer entities.
use crate::placement_transform::Axis;
use forge_core::Vec3;

/// Quantize a relative rotation from the gesture's initial transform, not each frame.
/// Half steps round away from zero. The supported steps divide 180 degrees so
/// the equivalent-orientation wrap at +/-180 cannot produce a different snap.
pub fn snap_degrees(angle: f32, step: Option<f32>) -> Result<f32, String> {
    if !angle.is_finite() {
        return Err("Non-finite rotation angle".into());
    }
    let Some(step) = step else { return Ok(angle) };
    if ![1.0_f32, 5.0, 15.0, 30.0, 45.0, 90.0].contains(&step) {
        return Err("Rotation step must be 1, 5, 15, 30, 45 or 90 degrees".into());
    }
    let snapped = (angle / step).round() * step;
    if !snapped.is_finite() {
        return Err("Rotation snapping overflow".into());
    }
    Ok(snapped)
}

/// Angle on the plane normal to the world axis, intersected by a forward camera ray.
pub fn ray_angle(pivot: Vec3, axis: Axis, origin: Vec3, direction: Vec3) -> Result<f32, String> {
    if !pivot.is_finite() || !origin.is_finite() || !direction.is_finite() {
        return Err("Non-finite rotation ray".into());
    }
    let component = |vector: Vec3| match axis {
        Axis::X => vector.x,
        Axis::Y => vector.y,
        Axis::Z => vector.z,
    };
    let length = direction.x.hypot(direction.y).hypot(direction.z);
    if !length.is_finite() || length < 1e-6 || component(direction).abs() / length < 0.05 {
        return Err("Rotation plane is edge-on; orbit the camera".into());
    }
    let distance = (component(pivot) - component(origin)) / component(direction);
    if !distance.is_finite() || distance < 0.0 {
        return Err("Rotation plane behind camera".into());
    }
    let relative = Vec3::new(
        origin.x + direction.x * distance - pivot.x,
        origin.y + direction.y * distance - pivot.y,
        origin.z + direction.z * distance - pivot.z,
    );
    let (horizontal, vertical) = match axis {
        Axis::X => (relative.y, relative.z),
        Axis::Y => (relative.z, relative.x),
        Axis::Z => (relative.x, relative.y),
    };
    if !relative.is_finite() || horizontal.hypot(vertical) < 1e-5 {
        return Err("Rotation angle undefined at pivot".into());
    }
    Ok(vertical.atan2(horizontal))
}

/// Equivalent orientation across ±π; deliberately not a count of authored full turns.
pub fn delta_degrees(start: f32, current: f32) -> Result<f32, String> {
    if !start.is_finite() || !current.is_finite() || (current - start).is_infinite() {
        return Err("Invalid rotation angle".into());
    }
    let delta = current - start;
    Ok(delta.sin().atan2(delta.cos()).to_degrees())
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn angular_snap_is_relative_symmetric_and_rejects_invalid_settings() {
        for (angle, expected) in [(22.0, 15.0_f32), (22.5, 30.0), (-22.5, -30.0), (7.0, 0.0)] {
            assert_eq!(
                snap_degrees(angle, Some(15.0)).unwrap().to_bits(),
                expected.to_bits()
            );
        }
        assert_eq!(
            snap_degrees(-0.0, None).unwrap().to_bits(),
            (-0.0_f32).to_bits()
        );
        for step in [1.0, 5.0, 15.0, 30.0, 45.0, 90.0] {
            let positive = snap_degrees(179.9, Some(step)).unwrap();
            let negative = snap_degrees(-179.9, Some(step)).unwrap();
            assert!((positive - negative - 360.0).abs() < 1e-5);
        }
        for step in [0.0, -15.0, 7.0, f32::NAN, f32::INFINITY] {
            assert!(snap_degrees(10.0, Some(step)).is_err());
        }
        assert!(snap_degrees(f32::NAN, None).is_err());
    }
    #[test]
    fn ray_rotation_follows_right_handed_axes_and_wraps_without_a_turn_jump() {
        let origin = Vec3::new(0.0, 0.0, 5.0);
        let start = ray_angle(Vec3::ZERO, Axis::Z, origin, Vec3::new(1.0, 0.0, -5.0)).unwrap();
        let end = ray_angle(Vec3::ZERO, Axis::Z, origin, Vec3::new(0.0, 1.0, -5.0)).unwrap();
        assert!((delta_degrees(start, end).unwrap() - 90.0).abs() < 1e-5);
        assert!(
            (delta_degrees(179_f32.to_radians(), -179_f32.to_radians()).unwrap() - 2.0).abs()
                < 1e-4
        );
        assert!(ray_angle(Vec3::ZERO, Axis::Z, origin, Vec3::new(1.0, 0.0, 0.0)).is_err());
        assert!(ray_angle(Vec3::ZERO, Axis::Z, origin, Vec3::new(0.0, 0.0, -1.0)).is_err());
        assert!(ray_angle(Vec3::ZERO, Axis::Z, origin, Vec3::new(0.0, 1.0, 1.0)).is_err());
        assert!(delta_degrees(f32::NAN, 0.0).is_err());
    }
}
