// SPDX-License-Identifier: MIT
// Adapted from Tidewater engine/render/Frame.js, copyright 2026 DRG Software Solutions LLC.
// Revision 1438b1abfcaee3267092b75573014f4d9b4a983c; see third_party/tidewater/LICENSE.
//! Shared camera and simulation fields for the source water frame.
use bevy::prelude::{GlobalTransform, Mat4, UVec2, UVec4, Vec3};

/// Camera values prepared once for every water material of this view.
pub struct CameraFrame {
    matrices: [Mat4; 8],
    position: Vec3,
}

impl CameraFrame {
    #[must_use]
    pub fn new(clip: Mat4, pose: GlobalTransform) -> Self {
        let inverse_view = pose.to_matrix();
        let view = inverse_view.inverse();
        let view_projection = clip * view;
        Self {
            matrices: [
                view,
                clip,
                view_projection,
                inverse_view,
                clip.inverse(),
                view_projection.inverse(),
                view_projection,
                view_projection,
            ],
            position: pose.translation(),
        }
    }

    /// Update only camera fields; all other source frame words are retained.
    pub fn write(&self, frame: &mut [UVec4; 45]) {
        for (matrix_index, matrix) in self.matrices.iter().enumerate() {
            for (column, values) in matrix.to_cols_array_2d().into_iter().enumerate() {
                frame[matrix_index * 4 + column] = UVec4::from_array(values.map(f32::to_bits));
            }
        }
        for row in [32, 33] {
            frame[row].x = self.position.x.to_bits();
            frame[row].y = self.position.y.to_bits();
            frame[row].z = self.position.z.to_bits();
        }
    }
}

/// Write the nonzero render-target dimensions without altering other frame fields.
///
/// # Panics
/// Panics if either dimension is zero.
pub fn write_resolution(frame: &mut [UVec4; 45], size: UVec2) {
    assert!(size.min_element() > 0, "water resolution must be nonzero");
    let size = size.as_vec2();
    frame[34] = UVec4::new(
        size.x.to_bits(),
        size.y.to_bits(),
        (1.0 / size.x).to_bits(),
        (1.0 / size.y).to_bits(),
    );
    frame[35].x = size.x.to_bits();
    frame[35].y = size.y.to_bits();
}

/// Write the exact shared simulation step without altering sea level in the same word.
#[allow(
    clippy::cast_possible_truncation,
    reason = "The source uniform stores simulation time as f32."
)]
pub fn write_time(frame: &mut [UVec4; 45], step: u32, time: f64, dt: f32) {
    frame[36].x = step;
    frame[36].y = (time as f32).to_bits();
    frame[36].z = dt.to_bits();
}

/// Publish an observed underwater state without altering other lighting fields.
pub fn write_immersion(frame: &mut [UVec4; 45], underwater: bool, water_height: f32) {
    frame[39].w = f32::from(underwater).to_bits();
    frame[40].w = water_height.to_bits();
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::prelude::{Quat, Transform};

    #[test]
    fn shared_resolution_writes_only_the_expected_fields() {
        let mut frame = [UVec4::splat(0x1234_5678); 45];
        write_resolution(&mut frame, UVec2::new(1919, 1079));
        assert_eq!(
            frame[34],
            UVec4::new(
                1919.0_f32.to_bits(),
                1079.0_f32.to_bits(),
                (1.0_f32 / 1919.0).to_bits(),
                (1.0_f32 / 1079.0).to_bits(),
            )
        );
        assert_eq!(
            frame[35],
            UVec4::new(
                1919.0_f32.to_bits(),
                1079.0_f32.to_bits(),
                0x1234_5678,
                0x1234_5678,
            )
        );
        assert_eq!(frame[33], UVec4::splat(0x1234_5678));
        assert_eq!(frame[36], UVec4::splat(0x1234_5678));
    }

    #[test]
    #[should_panic(expected = "water resolution must be nonzero")]
    fn shared_resolution_rejects_a_zero_dimension() {
        write_resolution(&mut [UVec4::ZERO; 45], UVec2::new(1, 0));
    }

    #[test]
    fn shared_camera_and_time_keep_unrelated_frame_fields() {
        let pose = GlobalTransform::from(
            Transform::from_xyz(3.0, 7.0, -11.0).with_rotation(Quat::from_rotation_y(0.3)),
        );
        let clip = Mat4::perspective_rh(1.0, 1.5, 0.1, 1000.0);
        let mut frame = [UVec4::splat(0x1234_5678); 45];
        CameraFrame::new(clip, pose).write(&mut frame);
        let expected = (clip * pose.to_matrix().inverse()).to_cols_array_2d();
        for (index, col) in expected.into_iter().enumerate() {
            assert_eq!(frame[8 + index], UVec4::from_array(col.map(f32::to_bits)));
            assert_eq!(frame[24 + index], frame[8 + index]);
            assert_eq!(frame[28 + index], frame[8 + index]);
        }
        assert_eq!(
            frame[32].to_array(),
            [
                3.0_f32.to_bits(),
                7.0_f32.to_bits(),
                (-11.0_f32).to_bits(),
                0x1234_5678
            ]
        );
        assert_eq!(frame[33], frame[32]);
        write_time(&mut frame, 17, 1.25, 0.016);
        write_immersion(&mut frame, true, 3.5);
        assert_eq!(
            frame[36].to_array(),
            [17, 1.25_f32.to_bits(), 0.016_f32.to_bits(), 0x1234_5678]
        );
        assert_eq!(frame[39].w, 1.0_f32.to_bits());
        assert_eq!(frame[40].w, 3.5_f32.to_bits());
        assert_eq!(frame[38], UVec4::splat(0x1234_5678));
    }
}
