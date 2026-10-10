//! Explicit inputs for Unity `AbsoluteWorld` -> Object position nodes.
//! These computations do not qualify source frames or shader execution.
use bevy::math::Affine3A;
use bevy::prelude::{Mat4, Vec3};

#[derive(Clone, Copy, Debug)]
pub struct SourceGraphPositionFrame {
    pub source_world_to_object: Mat4,
    pub source_camera_relative_offset: Vec3,
}

#[allow(
    clippy::float_cmp,
    reason = "The source mapping must have the exact affine row; tolerances would admit projective matrices."
)]
fn require_affine_inverse(matrix: Mat4) -> Result<Mat4, &'static str> {
    if !matrix.is_finite()
        || matrix.x_axis.w != 0.0
        || matrix.y_axis.w != 0.0
        || matrix.z_axis.w != 0.0
        || matrix.w_axis.w != 1.0
    {
        return Err("Source position mapping must be a finite affine matrix");
    }
    let determinant = matrix.determinant();
    if !determinant.is_finite() || determinant == 0.0 {
        return Err("Source position mapping is singular");
    }
    // The input has already passed the exact affine-row check. Inverting its
    // affine representation retains that row exactly; generic Mat4 inversion
    // can introduce roundoff in w even for ordinary rotated/scaled instances.
    let inverse = Mat4::from(Affine3A::from_mat4(matrix).inverse());
    if !inverse.is_finite() {
        return Err("Source position mapping has no finite inverse");
    }
    Ok(inverse)
}

/// Derive the two mandatory shader inputs from the explicit current instance
/// and source basis mappings. Camera-relative and absolute world must share
/// their linear basis: a difference is not silently treated as camera motion.
pub fn source_graph_position_frame(
    source_object_from_target_object: Mat4,
    target_world_from_target_object: Mat4,
    source_world_from_target_world: Mat4,
    source_absolute_world_from_target_world: Mat4,
) -> Result<SourceGraphPositionFrame, &'static str> {
    require_affine_inverse(source_object_from_target_object)?;
    let target_object_from_target_world = require_affine_inverse(target_world_from_target_object)?;
    let target_world_from_source_world = require_affine_inverse(source_world_from_target_world)?;
    require_affine_inverse(source_absolute_world_from_target_world)?;
    if source_world_from_target_world.x_axis != source_absolute_world_from_target_world.x_axis
        || source_world_from_target_world.y_axis != source_absolute_world_from_target_world.y_axis
        || source_world_from_target_world.z_axis != source_absolute_world_from_target_world.z_axis
    {
        return Err("Source absolute and relative worlds have different linear bases");
    }
    let frame = SourceGraphPositionFrame {
        source_world_to_object: source_object_from_target_object
            * target_object_from_target_world
            * target_world_from_source_world,
        source_camera_relative_offset: (source_absolute_world_from_target_world.w_axis
            - source_world_from_target_world.w_axis)
            .truncate(),
    };
    require_affine_inverse(frame.source_world_to_object)?;
    if !frame.source_camera_relative_offset.is_finite() {
        return Err("Source camera-relative offset is not finite");
    }
    Ok(frame)
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::prelude::Quat;

    #[test]
    fn absolute_position_keeps_translation_and_source_handedness() {
        let frame = source_graph_position_frame(
            Mat4::from_scale(Vec3::new(-1.0, 1.0, 1.0)),
            Mat4::from_translation(Vec3::new(1010.0, 4.0, 0.0)),
            Mat4::from_translation(Vec3::new(-1000.0, 0.0, 0.0)),
            Mat4::IDENTITY,
        )
        .unwrap();
        assert_eq!(frame.source_camera_relative_offset, Vec3::X * 1000.0);
        let object = frame
            .source_world_to_object
            .transform_point3(Vec3::new(1012.0, 5.0, 3.0) - frame.source_camera_relative_offset);
        assert_eq!(object, Vec3::new(-2.0, 1.0, 3.0));
    }

    #[test]
    #[allow(
        clippy::float_cmp,
        reason = "Exact copied fixture values and GPU wire fields must remain identical; an epsilon would weaken this regression."
    )]
    fn rotated_scaled_instance_recovers_its_original_position() {
        let instance = Mat4::from_scale_rotation_translation(
            Vec3::new(2.0, 3.0, 4.0),
            Quat::from_rotation_y(std::f32::consts::FRAC_PI_2),
            Vec3::new(13.0, 7.0, -9.0),
        );
        let frame =
            source_graph_position_frame(Mat4::IDENTITY, instance, Mat4::IDENTITY, Mat4::IDENTITY)
                .unwrap();
        assert_eq!(frame.source_world_to_object.w_axis.w, 1.0);
        let recovered = frame
            .source_world_to_object
            .transform_point3(Vec3::new(25.0, 13.0, -11.0));
        assert!(recovered.abs_diff_eq(Vec3::new(1.0, 2.0, 3.0), 0.00001));
    }

    #[test]
    fn mismatched_world_basis_is_not_camera_translation() {
        assert!(
            source_graph_position_frame(
                Mat4::IDENTITY,
                Mat4::IDENTITY,
                Mat4::from_scale(Vec3::new(-1.0, 1.0, 1.0)),
                Mat4::IDENTITY,
            )
            .is_err()
        );
    }

    #[test]
    fn invalid_frames_fail_before_shader_inputs_are_returned() {
        for bad in [
            Mat4::from_scale(Vec3::new(0.0, 1.0, 1.0)),
            Mat4::from_translation(Vec3::new(f32::NAN, 0.0, 0.0)),
            Mat4::perspective_rh(1.0, 1.0, 0.1, 100.0),
        ] {
            for index in 0..4 {
                let mut matrices = [Mat4::IDENTITY; 4];
                matrices[index] = bad;
                assert!(
                    source_graph_position_frame(matrices[0], matrices[1], matrices[2], matrices[3])
                        .is_err()
                );
            }
        }
    }
}
