// SPDX-License-Identifier: MIT
// Adapted from dgreenheck/tidewater, copyright 2026 DRG Software Solutions LLC.
// Revision 1438b1ab; see third_party/tidewater/LICENSE.
//! Source-exact static fish bending, separate from the editable placement transform.

#[derive(Clone, Copy, Debug)]
pub struct Bend {
    pub curl: f32,
    pub sag: f32,
    pub jaw: f32,
    /// Source `FishSkin` row 7, components z/w: local mouth hinge z/y.
    pub hinge: [f32; 2],
}

#[derive(Clone, Copy, Debug)]
pub struct BentVertex {
    pub position: [f32; 3],
    pub normal: [f32; 3],
}

/// Bend in model space; preserve the rest position and `aData` for procedural shading.
/// Length, quaternion and translation are supplied by the common placement root.
///
/// # Errors
/// Rejects non-finite inputs or a deformation that cannot be represented finitely.
pub fn deform(
    mut position: [f32; 3],
    mut normal: [f32; 3],
    data: [f32; 4],
    bend: Bend,
) -> Result<BentVertex, String> {
    if position
        .iter()
        .chain(&normal)
        .chain(&data)
        .chain(&bend.hinge)
        .chain([&bend.curl, &bend.sag, &bend.jaw])
        .any(|value| !value.is_finite())
    {
        return Err("Non-finite fish pose input".into());
    }
    let encoded_jaw = data[1] + 0.01;
    let jaw_weight = ((encoded_jaw - encoded_jaw.floor()) - 0.01).max(0.0) / 0.9;
    let jaw_angle = bend.jaw * jaw_weight;
    let jaw_cos = jaw_angle.cos();
    let jaw_sin = jaw_angle.sin();
    let hinge_y = position[1] - bend.hinge[1];
    let hinge_z = position[2] - bend.hinge[0];
    position[1] = bend.hinge[1] + hinge_y * jaw_cos - hinge_z * jaw_sin;
    position[2] = bend.hinge[0] + hinge_y * jaw_sin + hinge_z * jaw_cos;
    let jaw_normal_y = normal[1] * jaw_cos - normal[2] * jaw_sin;
    let jaw_normal_z = normal[1] * jaw_sin + normal[2] * jaw_cos;
    normal[1] = jaw_normal_y;
    normal[2] = jaw_normal_z;

    let curl = bend.curl + if bend.curl >= 0.0 { 1e-4 } else { -1e-4 };
    let curl_angle = curl * position[2];
    let curl_cos = curl_angle.cos();
    let curl_sin = curl_angle.sin();
    let curl_half = (curl_angle * 0.5).sin();
    let curled_x = curl_half * curl_half * 2.0 / curl + position[0] * curl_cos;
    let curled_z = curl_sin / curl - position[0] * curl_sin;
    let curled_normal_x = normal[0] * curl_cos + normal[2] * curl_sin;
    let curled_normal_z = normal[2] * curl_cos - normal[0] * curl_sin;

    let sag = bend.sag + if bend.sag >= 0.0 { 1e-4 } else { -1e-4 };
    let sag_angle = sag * curled_z;
    let sag_cos = sag_angle.cos();
    let sag_sin = sag_angle.sin();
    let sag_half = (sag_angle * 0.5).sin();
    let deformed = BentVertex {
        position: [
            curled_x,
            sag_half * sag_half * 2.0 / sag + position[1] * sag_cos,
            sag_sin / sag - position[1] * sag_sin,
        ],
        normal: [
            curled_normal_x,
            normal[1] * sag_cos + curled_normal_z * sag_sin,
            curled_normal_z * sag_cos - normal[1] * sag_sin,
        ],
    };
    if deformed
        .position
        .iter()
        .chain(&deformed.normal)
        .any(|v| !v.is_finite())
    {
        return Err("Non-finite fish pose result".into());
    }
    Ok(deformed)
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn invalid_pose_is_refused() {
        let bend = Bend {
            curl: 0.0,
            sag: 0.0,
            jaw: 0.0,
            hinge: [0.0; 2],
        };
        assert!(deform([f32::NAN, 0.0, 0.0], [0.0, 1.0, 0.0], [0.0; 4], bend).is_err());
        assert!(
            deform(
                [0.0; 3],
                [0.0, 1.0, 0.0],
                [0.0; 4],
                Bend {
                    jaw: f32::INFINITY,
                    ..bend
                }
            )
            .is_err()
        );
    }
}
