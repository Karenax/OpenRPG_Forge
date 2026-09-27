//! Bake prototype normalization into a persistent raw-GLB placement transform.
use forge_core::{Transform, UnitQuaternion, Vec3};

/// Compose scatter pose with scale * translation(-source pivot).
/// Source node transforms remain inside the GLB and must not be applied twice.
pub fn from_scatter(
    position: Vec3,
    yaw_radians: f32,
    scatter_scale: f32,
    source_pivot: Vec3,
    prototype_scale: f32,
) -> Result<Transform, String> {
    if !position.is_finite()
        || !source_pivot.is_finite()
        || !yaw_radians.is_finite()
        || !scatter_scale.is_finite()
        || scatter_scale <= 0.0
        || !prototype_scale.is_finite()
        || prototype_scale <= 0.0
    {
        return Err("Invalid promotion normalization or scatter pose".into());
    }
    let scale = scatter_scale * prototype_scale;
    let (sine, cosine) = yaw_radians.sin_cos();
    let (half_sine, half_cosine) = (yaw_radians * 0.5).sin_cos();
    let transform = Transform {
        translation_m: Vec3::new(
            position.x - scale * (cosine * source_pivot.x + sine * source_pivot.z),
            position.y - scale * source_pivot.y,
            position.z - scale * (-sine * source_pivot.x + cosine * source_pivot.z),
        ),
        rotation: UnitQuaternion::new(0.0, half_sine, 0.0, half_cosine),
        scale: Vec3::new(scale, scale, scale),
    };
    transform.validate().map_err(|error| error.to_string())?;
    Ok(transform)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn preserves_offset_pivot_and_combines_height_with_scatter_scale() {
        let pose = from_scatter(
            Vec3::new(10.0, 20.0, 30.0),
            std::f32::consts::FRAC_PI_2,
            1.5,
            Vec3::new(2.0, 3.0, 4.0),
            2.0,
        )
        .unwrap();
        assert!((pose.translation_m.x + 2.0).abs() < 1e-5);
        assert!((pose.translation_m.y - 11.0).abs() < 1e-5);
        assert!((pose.translation_m.z - 36.0).abs() < 1e-5);
        assert_eq!(pose.scale, Vec3::new(3.0, 3.0, 3.0));
        assert!(pose.rotation.is_finite_and_normalized());
    }

    #[test]
    fn invalid_or_overflowing_normalization_is_refused() {
        for scale in [0.0, -1.0, f32::NAN, f32::INFINITY, f32::MAX] {
            assert!(from_scatter(Vec3::ZERO, 0.0, scale, Vec3::ONE, 2.0).is_err());
        }
        assert!(from_scatter(Vec3::ZERO, f32::NAN, 1.0, Vec3::ZERO, 1.0).is_err());
    }
}
