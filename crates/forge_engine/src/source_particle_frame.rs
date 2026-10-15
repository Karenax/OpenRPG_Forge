//! Explicit native projection-bank frame; source shader arithmetic stays intact.
use bevy::{math::Affine3A, prelude::*};
use forge_assets::source_particle_transport::SourceParticleFrameLayout;

/// Native shader subtracts camera position from raw object translation, then
/// uses a camera-relative world-to-clip matrix. Both inputs are caller supplied.
pub struct SourceParticleFrame {
    pub source_world_from_source_object: Mat4,
    pub source_relative_world_to_clip: Mat4,
    pub source_camera_position: Vec3,
}
impl SourceParticleFrame {
    /// Source shader outputs this current renderer's clip coordinates directly.
    /// Remove camera translation in source world explicitly, not by a captured offset.
    pub fn from_current_scene(
        source_world_from_target_world: Mat4,
        target_world_from_target_object: Mat4,
        target_object_from_source_object: Mat4,
        target_world_from_view: Mat4,
        clip_from_view: Mat4,
    ) -> Result<Self, String> {
        affine_inverse(source_world_from_target_world)?;
        let mut source_linear = source_world_from_target_world;
        source_linear.w_axis = Vec4::W;
        let target_from_source = affine_inverse(source_linear)?;
        let view_from_target = affine_inverse(target_world_from_view)?;
        affine_inverse(target_world_from_target_object)?;
        affine_inverse(target_object_from_source_object)?;
        let target_camera = target_world_from_view.w_axis.truncate();
        let camera = source_world_from_target_world.transform_point3(target_camera);
        let object = source_world_from_target_world
            * target_world_from_target_object
            * target_object_from_source_object;
        let clip = clip_from_view
            * view_from_target
            * Mat4::from_translation(target_camera)
            * target_from_source;
        let frame = Self {
            source_world_from_source_object: object,
            source_relative_world_to_clip: clip,
            source_camera_position: camera,
        };
        affine_inverse(object)?;
        if !clip.is_finite()
            || !clip.determinant().is_finite()
            || clip.determinant() == 0.0
            || !camera.is_finite()
        {
            return Err("Current particle camera or clip is singular/nonfinite".into());
        }
        Ok(frame)
    }
    #[allow(
        clippy::float_cmp,
        reason = "Exact affine row is required; a tolerance would admit projective object transforms"
    )]
    pub fn apply_bank(
        &self,
        layout: &SourceParticleFrameLayout,
        original: &[u8; 1024],
    ) -> Result<[u8; 1024], String> {
        layout.validate()?;
        let object = self.source_world_from_source_object;
        if !object.is_finite()
            || object.x_axis.w != 0.0
            || object.y_axis.w != 0.0
            || object.z_axis.w != 0.0
            || object.w_axis.w != 1.0
            || !object.determinant().is_finite()
            || object.determinant() == 0.0
            || !self.source_relative_world_to_clip.is_finite()
            || !self.source_relative_world_to_clip.determinant().is_finite()
            || self.source_relative_world_to_clip.determinant() == 0.0
            || !self.source_camera_position.is_finite()
        {
            return Err(
                "Particle source frame is nonfinite, singular or object frame is projective".into(),
            );
        }
        let inverse = Mat4::from(Affine3A::from_mat4(object).inverse());
        if !inverse.is_finite() {
            return Err("Particle object inverse is nonfinite".into());
        }
        let mut bank = *original;
        for (offset, matrix) in [
            (layout.object_to_world_offset, object),
            (layout.world_to_object_offset, inverse),
            (
                layout.relative_world_to_clip_offset,
                self.source_relative_world_to_clip,
            ),
        ] {
            for (index, value) in matrix.to_cols_array().iter().enumerate() {
                bank[offset + index * 4..offset + index * 4 + 4]
                    .copy_from_slice(&value.to_le_bytes());
            }
        }
        // Retain source w lane: only xyz has camera-position semantics.
        for (index, value) in self.source_camera_position.to_array().iter().enumerate() {
            let offset = layout.camera_position_offset + index * 4;
            bank[offset..offset + 4].copy_from_slice(&value.to_le_bytes());
        }
        Ok(bank)
    }
}

#[allow(
    clippy::float_cmp,
    reason = "Exact affine row prevents projective inputs"
)]
fn affine_inverse(matrix: Mat4) -> Result<Mat4, String> {
    if !matrix.is_finite()
        || matrix.x_axis.w != 0.0
        || matrix.y_axis.w != 0.0
        || matrix.z_axis.w != 0.0
        || matrix.w_axis.w != 1.0
        || !matrix.determinant().is_finite()
        || matrix.determinant() == 0.0
    {
        return Err("Current particle mapping must be finite, affine and invertible".into());
    }
    let inverse = Mat4::from(Affine3A::from_mat4(matrix).inverse());
    if !inverse.is_finite() {
        return Err("Current particle mapping inverse is nonfinite".into());
    }
    Ok(inverse)
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn camera_relative_source_frame_matches_current_target_clip_for_reflected_scaled_and_moved_objects()
     {
        let source_world = Mat4::from_scale_rotation_translation(
            Vec3::new(-1., 1., 1.),
            Quat::from_rotation_y(0.3),
            Vec3::new(23., -9., 4.),
        );
        let basis = Mat4::from_scale(Vec3::new(-1., 1., 1.));
        let object = Mat4::from_scale_rotation_translation(
            Vec3::new(2., 3., 4.),
            Quat::from_rotation_y(0.7),
            Vec3::new(11., -2., 8.),
        );
        let camera =
            Mat4::from_rotation_translation(Quat::from_rotation_y(-0.4), Vec3::new(7., 4., -6.));
        for clip in [
            Mat4::perspective_infinite_reverse_rh(1., 1.5, 0.1),
            Mat4::orthographic_rh(-5., 5., -4., 4., 0.1, 200.),
        ] {
            let frame =
                SourceParticleFrame::from_current_scene(source_world, object, basis, camera, clip)
                    .unwrap();
            for position in [
                Vec3::ZERO,
                Vec3::new(0.2, 0.4, -0.7),
                Vec3::new(-1., 2., 3.),
            ] {
                let relative = frame
                    .source_world_from_source_object
                    .transform_point3(position)
                    - frame.source_camera_position;
                let actual = frame.source_relative_world_to_clip * relative.extend(1.);
                let expected = clip * camera.inverse() * object * basis * position.extend(1.);
                assert!(
                    (actual - expected)
                        .to_array()
                        .iter()
                        .all(|v| v.abs() < 2e-5),
                    "actual={actual:?}, expected={expected:?}, delta={:?}",
                    actual - expected
                );
            }
        }
        assert!(
            SourceParticleFrame::from_current_scene(
                Mat4::ZERO,
                object,
                basis,
                camera,
                Mat4::IDENTITY
            )
            .is_err()
        );
    }
    fn layout() -> SourceParticleFrameLayout {
        SourceParticleFrameLayout {
            object_to_world_offset: 0,
            world_to_object_offset: 64,
            relative_world_to_clip_offset: 128,
            camera_position_offset: 192,
            evidence_sha256: forge_core::bytes_sha256(b"generated frame-layout fixture"),
        }
    }
    #[test]
    fn current_object_inverse_camera_and_clip_replace_only_declared_lanes() {
        let frame = SourceParticleFrame {
            source_world_from_source_object: Mat4::from_scale_rotation_translation(
                Vec3::new(2., 3., 4.),
                Quat::from_rotation_y(0.7),
                Vec3::new(11., -2., 8.),
            ),
            source_relative_world_to_clip: Mat4::perspective_rh(1., 1.5, 0.1, 200.),
            source_camera_position: Vec3::new(4., 5., -6.),
        };
        let original = [0xA5; 1024];
        let bank = frame.apply_bank(&layout(), &original).unwrap();
        assert_eq!(&bank[204..], &original[204..]);
        let words: Vec<f32> = bank[..204]
            .as_chunks::<4>()
            .0
            .iter()
            .map(|b| f32::from_le_bytes(*b))
            .collect();
        assert_eq!(
            &words[..16],
            &frame.source_world_from_source_object.to_cols_array()
        );
        let inverse = Mat4::from_cols_array(words[16..32].try_into().unwrap());
        assert!(
            (frame.source_world_from_source_object * inverse - Mat4::IDENTITY)
                .to_cols_array()
                .iter()
                .all(|v| v.abs() < 1e-5)
        );
        assert_eq!(
            &words[32..48],
            &frame.source_relative_world_to_clip.to_cols_array()
        );
        assert_eq!(&words[48..51], &frame.source_camera_position.to_array());
        assert_eq!(&bank[992..996], &original[992..996]);
    }
    #[test]
    fn malformed_layout_and_bad_frame_refuse_without_a_partial_bank() {
        let mut declared = layout();
        declared.world_to_object_offset = 0;
        assert!(declared.validate().is_err());
        declared = layout();
        declared.camera_position_offset = usize::MAX;
        assert!(declared.validate().is_err());
        let mut frame = SourceParticleFrame {
            source_world_from_source_object: Mat4::IDENTITY,
            source_relative_world_to_clip: Mat4::IDENTITY,
            source_camera_position: Vec3::ZERO,
        };
        frame.source_world_from_source_object = Mat4::ZERO;
        assert!(frame.apply_bank(&layout(), &[0; 1024]).is_err());
        frame.source_world_from_source_object = Mat4::IDENTITY;
        frame.source_camera_position.x = f32::NAN;
        assert!(frame.apply_bank(&layout(), &[0; 1024]).is_err());
    }
}
