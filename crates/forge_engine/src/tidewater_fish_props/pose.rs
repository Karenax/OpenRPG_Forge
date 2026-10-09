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
    use forge_core::bytes_sha256;
    use serde_json::Value;
    use std::path::Path;

    fn cross(first: [f32; 3], second: [f32; 3]) -> [f32; 3] {
        [
            first[1] * second[2] - first[2] * second[1],
            first[2] * second[0] - first[0] * second[2],
            first[0] * second[1] - first[1] * second[0],
        ]
    }

    fn rotate(quaternion: &[f32], vector: [f32; 3]) -> [f32; 3] {
        let axis = [quaternion[0], quaternion[1], quaternion[2]];
        let inner = cross(axis, vector);
        let outer = cross(
            axis,
            std::array::from_fn(|i| inner[i] + vector[i] * quaternion[3]),
        );
        std::array::from_fn(|i| vector[i] + outer[i] * 2.0)
    }

    fn floats(root: &Path, metadata: &Value, name: &str) -> Vec<f32> {
        let bytes = std::fs::read(root.join(name)).unwrap();
        assert_eq!(
            bytes_sha256(&bytes).to_string(),
            metadata["files"][name]["sha256"]
        );
        assert_eq!(
            bytes.len(),
            usize::try_from(metadata["files"][name]["bytes"].as_u64().unwrap()).unwrap()
        );
        assert!(bytes.len().is_multiple_of(4));
        bytes
            .as_chunks::<4>()
            .0
            .iter()
            .map(|chunk| f32::from_le_bytes(*chunk))
            .collect()
    }

    #[test]
    fn complete_source_gpu_pose_reference() {
        let root = std::env::var("FORGE_TIDEWATER_FISH_POSE_REFERENCE")
            .expect("Real GPU reference required");
        let root = Path::new(&root);
        let raw = std::fs::read(root.join("reference.json")).unwrap();
        assert_eq!(
            bytes_sha256(&raw).to_string(),
            "bb8182a80d303b55d535df5e27688712c56b6275fd114633cd861b576b005f52"
        );
        let metadata: Value = serde_json::from_slice(&raw).unwrap();
        assert_eq!(metadata["vertices"], 169_709);
        assert_eq!(metadata["cases"].as_array().unwrap().len(), 234);
        assert_eq!(metadata["gpu_executed"], true);
        let input = floats(root, &metadata, "input-f32.bin");
        let output = floats(root, &metadata, "output-f32.bin");
        let records = floats(root, &metadata, "records-f32.bin");
        let skin = floats(root, &metadata, "skin-f32.bin");
        assert_eq!(input.len(), 169_709 * 16);
        assert_eq!(output.len(), 169_709 * 8);
        assert_eq!(records.len(), 78 * 16);
        let mut maximum_position_error = 0.0_f32;
        let mut maximum_normal_error = 0.0_f32;
        for (vertex, (source, expected)) in input
            .as_chunks::<16>()
            .0
            .iter()
            .zip(output.as_chunks::<8>().0.iter())
            .enumerate()
        {
            let ordinal = (0_u8..78)
                .find(|&i| f32::from(i).to_bits() == source[12].to_bits())
                .unwrap();
            let record = &records[usize::from(ordinal) * 16..][..16];
            let pattern = (0_u8..21)
                .find(|&i| f32::from(i).to_bits() == record[8].floor().to_bits())
                .unwrap();
            let hinge = usize::from(pattern) * 32 + 28;
            let deformed = deform(
                source[..3].try_into().unwrap(),
                source[4..7].try_into().unwrap(),
                source[8..12].try_into().unwrap(),
                Bend {
                    curl: record[9],
                    sag: record[10],
                    jaw: record[11],
                    hinge: [skin[hinge + 2], skin[hinge + 3]],
                },
            )
            .unwrap();
            let scaled = deformed.position.map(|value| value * record[3]);
            let world_position = rotate(&record[4..8], scaled);
            let world_normal = rotate(&record[4..8], deformed.normal);
            for component in 0..3 {
                let position_error =
                    (world_position[component] + record[component] - expected[component]).abs();
                let normal_error = (world_normal[component] - expected[component + 4]).abs();
                maximum_position_error = maximum_position_error.max(position_error);
                maximum_normal_error = maximum_normal_error.max(normal_error);
                assert!(
                    position_error <= 3e-5,
                    "Vertex {vertex}: position error {position_error}"
                );
                assert!(
                    normal_error <= 2e-5,
                    "Vertex {vertex}: normal error {normal_error}"
                );
            }
        }
        println!(
            "{{\"vertices\":169709,\"cases\":234,\"maximum_position_error_m\":{maximum_position_error},\"maximum_normal_error\":{maximum_normal_error}}}"
        );
    }

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
