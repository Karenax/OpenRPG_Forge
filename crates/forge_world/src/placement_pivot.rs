//! Operation pivots are editor parameters, not edits to the asset's origin.
use crate::{
    placement_axis::translate_local,
    placement_transform::{Axis, RotationSpace, rotate},
};
use forge_core::{Transform, Vec3};

/// Rotate an entire placement about a world-space point, keeping its scale.
/// Local chooses the initial object's axis direction, not a scaled axis.
pub fn rotate_about(
    source: Transform,
    axis: Axis,
    degrees: f32,
    space: RotationSpace,
    pivot: Vec3,
) -> Result<Transform, String> {
    if !pivot.is_finite() {
        return Err("Rotation pivot must be finite".into());
    }
    let mut result = rotate(source, axis, degrees, space)?;
    if degrees % 360.0 == 0.0 || pivot == source.translation_m {
        return Ok(result);
    }
    let direction = match space {
        RotationSpace::World => match axis {
            Axis::X => Vec3::new(1.0, 0.0, 0.0),
            Axis::Y => Vec3::Y,
            Axis::Z => Vec3::new(0.0, 0.0, 1.0),
        },
        RotationSpace::Local => {
            let mut basis = source;
            basis.translation_m = Vec3::ZERO;
            translate_local(basis, axis, 1.0, None)?.translation_m
        }
    };
    let length = direction.x.hypot(direction.y).hypot(direction.z);
    let n = Vec3::new(
        direction.x / length,
        direction.y / length,
        direction.z / length,
    );
    let p = Vec3::new(
        source.translation_m.x - pivot.x,
        source.translation_m.y - pivot.y,
        source.translation_m.z - pivot.z,
    );
    let (s, c) = (degrees % 360.0).to_radians().sin_cos();
    let dot = n.x * p.x + n.y * p.y + n.z * p.z;
    // Rodrigues rotation around the chosen axis through the external pivot.
    result.translation_m = Vec3::new(
        pivot.x + p.x * c + (n.y * p.z - n.z * p.y) * s + n.x * dot * (1.0 - c),
        pivot.y + p.y * c + (n.z * p.x - n.x * p.z) * s + n.y * dot * (1.0 - c),
        pivot.z + p.z * c + (n.x * p.y - n.y * p.x) * s + n.z * dot * (1.0 - c),
    );
    result.validate().map_err(|e| e.to_string())?;
    Ok(result)
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn external_pivot_rotates_position_and_orientation_without_changing_scale() {
        let mut source = Transform::from_translation(Vec3::new(3.0, 0.0, 0.0));
        source.scale = Vec3::new(2.0, 3.0, 4.0);
        let pivot = Vec3::new(1.0, 0.0, 0.0);
        let rotated = rotate_about(source, Axis::Z, 90.0, RotationSpace::World, pivot).unwrap();
        assert!((rotated.translation_m.x - 1.0).abs() < 1e-5);
        assert!((rotated.translation_m.y - 2.0).abs() < 1e-5);
        assert_eq!(rotated.scale, source.scale);
        assert_eq!(
            rotated.rotation,
            rotate(source, Axis::Z, 90.0, RotationSpace::World)
                .unwrap()
                .rotation
        );
        let restored = rotate_about(rotated, Axis::Z, -90.0, RotationSpace::World, pivot).unwrap();
        assert!((restored.translation_m.x - source.translation_m.x).abs() < 1e-5);
        assert!(restored.translation_m.y.abs() < 1e-5);
        assert_eq!(
            rotate_about(source, Axis::Z, 360.0, RotationSpace::World, pivot).unwrap(),
            source
        );
        assert!(
            rotate_about(
                source,
                Axis::Z,
                15.0,
                RotationSpace::World,
                Vec3::new(f32::NAN, 0.0, 0.0)
            )
            .is_err()
        );
    }
    #[test]
    fn local_pivot_uses_initial_orientation_and_own_pivot_matches_regular_rotation() {
        let source = rotate(
            Transform::from_translation(Vec3::new(2.0, 0.0, 0.0)),
            Axis::Z,
            90.0,
            RotationSpace::World,
        )
        .unwrap();
        let turned = rotate_about(source, Axis::X, 90.0, RotationSpace::Local, Vec3::ZERO).unwrap();
        assert!(turned.translation_m.x.abs() < 1e-5);
        assert!(turned.translation_m.y.abs() < 1e-5);
        assert!((turned.translation_m.z + 2.0).abs() < 1e-5);
        assert_eq!(
            rotate_about(
                source,
                Axis::Y,
                35.0,
                RotationSpace::Local,
                source.translation_m
            )
            .unwrap(),
            rotate(source, Axis::Y, 35.0, RotationSpace::Local).unwrap()
        );
    }
}
