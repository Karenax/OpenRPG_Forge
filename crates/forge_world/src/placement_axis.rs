//! Axis dragging from camera rays. No renderer, screen resolution, or persistent editor state.
use crate::placement_transform::Axis;
use forge_core::{Transform, Vec3};

/// Closest parameter on a world axis through pivot to the forward half-ray.
/// Near-parallel axes are rejected rather than producing enormous cursor jumps.
pub fn ray_parameter(
    pivot: Vec3,
    axis: Axis,
    origin: Vec3,
    direction: Vec3,
) -> Result<f32, String> {
    if !pivot.is_finite() || !origin.is_finite() || !direction.is_finite() {
        return Err("Non-finite gizmo ray".into());
    }
    let length = direction.x.hypot(direction.y).hypot(direction.z);
    if !length.is_finite() || length < 1e-6 {
        return Err("Empty gizmo ray".into());
    }
    let direction = Vec3::new(
        direction.x / length,
        direction.y / length,
        direction.z / length,
    );
    let offset = Vec3::new(pivot.x - origin.x, pivot.y - origin.y, pivot.z - origin.z);
    let component = |v: Vec3| match axis {
        Axis::X => v.x,
        Axis::Y => v.y,
        Axis::Z => v.z,
    };
    let parallel = component(direction);
    let denominator = 1.0 - parallel * parallel;
    if denominator < 0.01 {
        return Err("Axis too close to view direction; orbit the camera".into());
    }
    let along = component(offset);
    let depth = direction.x * offset.x + direction.y * offset.y + direction.z * offset.z;
    let parameter = (parallel * depth - along) / denominator;
    let ray_distance = (depth - parallel * along) / denominator;
    if !parameter.is_finite() || !ray_distance.is_finite() || ray_distance < 0.0 {
        return Err("Axis behind camera or outside numeric range".into());
    }
    Ok(parameter)
}

pub fn translate(mut original: Transform, axis: Axis, distance: f32) -> Result<Transform, String> {
    original.validate().map_err(|e| e.to_string())?;
    if !distance.is_finite() {
        return Err("Non-finite axis movement".into());
    }
    match axis {
        Axis::X => original.translation_m.x += distance,
        Axis::Y => original.translation_m.y += distance,
        Axis::Z => original.translation_m.z += distance,
    }
    original.validate().map_err(|e| e.to_string())?;
    Ok(original)
}

/// Translate in metres on an initial local axis, independently of object scale.
/// Grid steps quantize the relative distance from the initial pivot, not world X/Z.
pub fn translate_local(
    mut original: Transform,
    axis: Axis,
    distance: f32,
    step_m: Option<f32>,
) -> Result<Transform, String> {
    original.validate().map_err(|e| e.to_string())?;
    crate::placement_snap::PlacementSnap {
        grid_step_m: step_m,
        surface_offset_m: None,
    }
    .validate()?;
    if !distance.is_finite() {
        return Err("Non-finite local movement".into());
    }
    #[allow(
        clippy::cast_possible_truncation,
        reason = "Canonical f32 distance validated below"
    )]
    let distance = step_m.map_or(distance, |step| {
        ((f64::from(distance) / f64::from(step)).round() * f64::from(step)) as f32
    });
    if !distance.is_finite() {
        return Err("Local snapping overflow".into());
    }
    if distance == 0.0 {
        return Ok(original);
    }
    let q = original.rotation;
    let direction = match axis {
        Axis::X => Vec3::new(
            1.0 - 2.0 * (q.y * q.y + q.z * q.z),
            2.0 * (q.x * q.y + q.w * q.z),
            2.0 * (q.x * q.z - q.w * q.y),
        ),
        Axis::Y => Vec3::new(
            2.0 * (q.x * q.y - q.w * q.z),
            1.0 - 2.0 * (q.x * q.x + q.z * q.z),
            2.0 * (q.y * q.z + q.w * q.x),
        ),
        Axis::Z => Vec3::new(
            2.0 * (q.x * q.z + q.w * q.y),
            2.0 * (q.y * q.z - q.w * q.x),
            1.0 - 2.0 * (q.x * q.x + q.y * q.y),
        ),
    };
    original.translation_m.x += direction.x * distance;
    original.translation_m.y += direction.y * distance;
    original.translation_m.z += direction.z * distance;
    original.validate().map_err(|e| e.to_string())?;
    Ok(original)
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn local_translation_uses_orientation_not_scale_and_snaps_relative_distance() {
        use crate::placement_transform::{RotationSpace, rotate};
        let mut source = rotate(
            Transform::from_translation(Vec3::new(2.0, 3.0, 4.0)),
            Axis::Z,
            90.0,
            RotationSpace::World,
        )
        .unwrap();
        source.scale = Vec3::new(3.0, 2.0, 5.0);
        let moved = translate_local(source, Axis::X, 1.3, Some(0.5)).unwrap();
        assert!((moved.translation_m.x - 2.0).abs() < 1e-5);
        assert!((moved.translation_m.y - 4.5).abs() < 1e-5);
        assert!((moved.translation_m.z - 4.0).abs() < 1e-5);
        assert_eq!(moved.rotation, source.rotation);
        assert_eq!(moved.scale, source.scale);
        assert_eq!(
            translate_local(source, Axis::X, 0.2, Some(0.5)).unwrap(),
            source
        );
        let restored = translate_local(moved, Axis::X, -1.3, Some(0.5)).unwrap();
        assert!((restored.translation_m.y - source.translation_m.y).abs() < 1e-5);
        assert!(translate_local(source, Axis::Y, 1.0, Some(0.0)).is_err());
        assert!(translate_local(source, Axis::Z, f32::NAN, None).is_err());
        let identity = Transform::from_translation(Vec3::new(1.0, 2.0, 3.0));
        for axis in [Axis::X, Axis::Y, Axis::Z] {
            assert_eq!(
                translate_local(identity, axis, 2.0, None).unwrap(),
                translate(identity, axis, 2.0).unwrap()
            );
        }
    }
    #[test]
    fn axis_ray_keeps_grab_offset_and_rejects_degenerate_views() {
        let pivot = Vec3::new(0.0, 0.0, 0.0);
        let direction = Vec3::new(0.0, -2.0, 0.0);
        let start = ray_parameter(pivot, Axis::X, Vec3::new(3.0, 2.0, 1.0), direction).unwrap();
        let end = ray_parameter(pivot, Axis::X, Vec3::new(7.0, 2.0, 1.0), direction).unwrap();
        assert!((start - 3.0).abs() < 1e-6);
        let source = Transform::from_translation(Vec3::new(2.0, 3.0, 4.0));
        assert_eq!(
            translate(source, Axis::X, end - start)
                .unwrap()
                .translation_m,
            Vec3::new(6.0, 3.0, 4.0)
        );
        assert!(ray_parameter(pivot, Axis::Y, Vec3::Y, direction).is_err());
        assert!(ray_parameter(pivot, Axis::X, Vec3::new(3.0, -2.0, 1.0), direction).is_err());
        assert!(ray_parameter(pivot, Axis::X, Vec3::ZERO, Vec3::ZERO).is_err());
        assert!(translate(source, Axis::Z, f32::NAN).is_err());
    }
}
