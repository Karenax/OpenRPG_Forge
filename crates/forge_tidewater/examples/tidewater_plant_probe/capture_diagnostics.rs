//! Read-only capture diagnostics. CPU assets are not GPU readback or an admission fence.
use bevy::{prelude::*, render::storage::ShaderBuffer};
use forge_engine::source_lighting::scene_buffer::{
    HANDLE, observation::GpuObservation, ownership::Owners,
};
use serde_json::{Value, json};

pub(super) fn snapshot(
    buffers: &Assets<ShaderBuffer>,
    owners: Option<&Owners>,
    gpu: Option<&GpuObservation>,
    cameras: &Query<(&Camera, &GlobalTransform, &Projection), With<Camera3d>>,
) -> Value {
    let buffer = buffers.get(&HANDLE);
    let data = buffer.and_then(|value| value.data.as_deref());
    let vectors = data.filter(|bytes| bytes.len() == 176).map(|bytes| {
        bytes
            .as_chunks::<16>()
            .0
            .iter()
            .map(|vector| {
                std::array::from_fn::<_, 4, _>(|index| {
                    f32::from_le_bytes(
                        vector[index * 4..index * 4 + 4]
                            .try_into()
                            .expect("one validated vec4 lane"),
                    )
                })
            })
            .collect::<Vec<_>>()
    });
    let views: Vec<_> = cameras
        .iter()
        .take(4)
        .map(|(camera, transform, projection)| {
            let perspective = match projection {
                Projection::Perspective(value) => Some(json!({
                    "near":value.near, "far":value.far,
                    "fov":value.fov, "aspect_ratio":value.aspect_ratio,
                })),
                _ => None,
            };
            json!({
                "active":camera.is_active,
                "physical_viewport_size":camera.physical_viewport_size().map(|size| size.to_array()),
                "clip_from_view":camera.clip_from_view().to_cols_array(),
                "affine_world_from_view":transform.affine().to_cols_array(),
                "perspective":perspective,
            })
        })
        .collect();
    json!({
        "scope":"MainWorld CPU assets/current camera and optional asynchronous buffer readback; not a same-frame GPU fence",
        "lighting":{
            "asset_present":buffer.is_some(),
            "gpu_owned":owners.is_some_and(|owners| owners.owns(&HANDLE)),
            "cpu_bytes":data.map(<[u8]>::len),
            "cpu_sha256":data.map(forge_core::bytes_sha256),
            "finite":vectors.as_ref().map(|values| values.iter().flatten().all(|value| value.is_finite())),
            "direction_color_sh_vec4":vectors,
            "gpu_readback":gpu.and_then(|observation| observation.0.as_ref()),
        },
        "cameras":views,
    })
}
