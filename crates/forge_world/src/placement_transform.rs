//! Engine-neutral operations about a placement's own pivot, shared by authoring tools.
use forge_core::{Transform, UnitQuaternion, Vec3};

#[derive(Clone, Copy, Debug)]
pub enum Axis {
    X,
    Y,
    Z,
}

#[derive(Clone, Copy, Debug)]
pub enum RotationSpace {
    Local,
    World,
}

/// Compose a relative rotation without decomposing the existing orientation into Euler angles.
/// Translation and non-uniform scale stay unchanged. World refers to axis direction, not origin.
pub fn rotate(
    mut source: Transform,
    axis: Axis,
    degrees: f32,
    space: RotationSpace,
) -> Result<Transform, String> {
    source.validate().map_err(|e| e.to_string())?;
    if !degrees.is_finite() {
        return Err("Rotation angle must be finite".into());
    }
    let degrees = degrees % 360.0;
    if degrees == 0.0 {
        return Ok(source);
    }
    let (sine, cosine) = (degrees.to_radians() * 0.5).sin_cos();
    let delta = match axis {
        Axis::X => UnitQuaternion::new(sine, 0.0, 0.0, cosine),
        Axis::Y => UnitQuaternion::new(0.0, sine, 0.0, cosine),
        Axis::Z => UnitQuaternion::new(0.0, 0.0, sine, cosine),
    };
    let (a, b) = match space {
        RotationSpace::Local => (source.rotation, delta),
        RotationSpace::World => (delta, source.rotation),
    };
    let q = UnitQuaternion::new(
        a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
        a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
        a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w,
        a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z,
    );
    // Inputs are validated unit quaternions; renormalize only the composed result.
    let length = (q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w).sqrt();
    source.rotation = UnitQuaternion::new(q.x / length, q.y / length, q.z / length, q.w / length);
    source.validate().map_err(|e| e.to_string())?;
    Ok(source)
}

/// Absolute local scale, not a multiplier; does not move the pivot or rotate the object.
pub fn set_scale(mut source: Transform, scale: Vec3) -> Result<Transform, String> {
    source.validate().map_err(|e| e.to_string())?;
    source.scale = scale;
    source.validate().map_err(|e| e.to_string())?;
    Ok(source)
}

/// Relative factor on one local scale axis, always based on the gesture's original transform.
pub fn scale_axis(source: Transform, axis: Axis, factor: f32) -> Result<Transform, String> {
    if !factor.is_finite() || factor <= 0.0 {
        return Err("Scale factor must be finite and positive".into());
    }
    let mut scale = source.scale;
    match axis {
        Axis::X => scale.x *= factor,
        Axis::Y => scale.y *= factor,
        Axis::Z => scale.z *= factor,
    }
    set_scale(source, scale)
}

/// Relative horizontal drag: keep the initial grab offset instead of teleporting the pivot.
pub fn drag_xz(mut source: Transform, start: [f32; 2], end: [f32; 2]) -> Result<Transform, String> {
    source.validate().map_err(|e| e.to_string())?;
    if start.into_iter().chain(end).any(|value| !value.is_finite()) {
        return Err("Drag coordinates must be finite".into());
    }
    source.translation_m.x += end[0] - start[0];
    source.translation_m.z += end[1] - start[1];
    source.validate().map_err(|e| e.to_string())?;
    Ok(source)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn axis_scaling_keeps_pivot_orientation_and_other_scales() {
        let original = Transform {
            translation_m: Vec3::new(1.0, 2.0, 3.0),
            rotation: UnitQuaternion::new(0.0, 1.0, 0.0, 0.0),
            scale: Vec3::new(2.0, 3.0, 4.0),
        };
        let scaled = scale_axis(original, Axis::Y, 0.5).unwrap();
        assert_eq!(scaled.scale, Vec3::new(2.0, 1.5, 4.0));
        assert_eq!(scaled.rotation, original.rotation);
        assert_eq!(scaled.translation_m, original.translation_m);
        assert_eq!(scale_axis(original, Axis::Z, 1.0).unwrap(), original);
        for factor in [0.0, -1.0, f32::INFINITY, f32::NAN, f32::MAX] {
            assert!(scale_axis(original, Axis::X, factor).is_err());
        }
    }

    #[test]
    fn local_and_world_axes_compose_in_the_documented_order() {
        let source = Transform {
            translation_m: Vec3::new(13.0, -2.0, 7.0),
            scale: Vec3::new(2.0, 3.0, 4.0),
            ..Transform::IDENTITY
        };
        let turned = rotate(source, Axis::Y, 90.0, RotationSpace::World).unwrap();
        let local = rotate(turned, Axis::X, 90.0, RotationSpace::Local).unwrap();
        let world = rotate(turned, Axis::X, 90.0, RotationSpace::World).unwrap();
        for (actual, expected) in [(local, -0.5), (world, 0.5)] {
            assert!((actual.rotation.z - expected).abs() < 1e-6);
            assert!((actual.rotation.x - 0.5).abs() < 1e-6);
            assert!((actual.rotation.y - 0.5).abs() < 1e-6);
            assert!((actual.rotation.w - 0.5).abs() < 1e-6);
            assert_eq!(actual.translation_m, source.translation_m);
            assert_eq!(actual.scale, source.scale);
        }
        assert_eq!(
            rotate(turned, Axis::Z, 720.0, RotationSpace::World).unwrap(),
            turned
        );
        let inverse = rotate(turned, Axis::Y, -90.0, RotationSpace::World).unwrap();
        assert!(inverse.rotation.y.abs() < 1e-6);
        assert!((inverse.rotation.w - 1.0).abs() < 1e-6);
        let scaled = set_scale(local, Vec3::new(1.0, 2.0, 0.5)).unwrap();
        assert_eq!(scaled.rotation, local.rotation);
        assert_eq!(scaled.translation_m, local.translation_m);
        assert_eq!(scaled.scale, Vec3::new(1.0, 2.0, 0.5));
    }

    #[test]
    fn invalid_input_is_rejected_without_repairing_the_source() {
        for value in [f32::NAN, f32::INFINITY, f32::NEG_INFINITY] {
            assert!(rotate(Transform::IDENTITY, Axis::Z, value, RotationSpace::Local).is_err());
        }
        for value in [0.0, -1.0, f32::NAN, f32::INFINITY] {
            assert!(set_scale(Transform::IDENTITY, Vec3::new(1.0, value, 1.0)).is_err());
        }
        let invalid = Transform {
            rotation: UnitQuaternion::new(0.0, 0.0, 0.0, 2.0),
            ..Transform::IDENTITY
        };
        assert!(rotate(invalid, Axis::Y, 0.0, RotationSpace::World).is_err());
        assert!(set_scale(invalid, Vec3::ONE).is_err());
    }

    #[test]
    fn horizontal_drag_keeps_grab_offset_height_and_shape() {
        let original = Transform {
            translation_m: Vec3::new(3.0, 5.0, 7.0),
            scale: Vec3::new(2.0, 3.0, 4.0),
            ..rotate(Transform::IDENTITY, Axis::Y, 33.0, RotationSpace::Local).unwrap()
        };
        let moved = drag_xz(original, [10.0, 20.0], [12.0, 17.0]).unwrap();
        assert_eq!(moved.translation_m, Vec3::new(5.0, 5.0, 4.0));
        assert_eq!(moved.rotation, original.rotation);
        assert_eq!(moved.scale, original.scale);
        assert_eq!(
            drag_xz(original, [10.0, 20.0], [10.0, 20.0]).unwrap(),
            original
        );
        assert!(drag_xz(original, [f32::NAN, 0.0], [0.0, 0.0]).is_err());
        assert!(drag_xz(original, [-f32::MAX, 0.0], [f32::MAX, 0.0]).is_err());
    }
}
