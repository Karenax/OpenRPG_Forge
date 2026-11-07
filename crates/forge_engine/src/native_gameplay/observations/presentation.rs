//! Actual camera and renderer input observations; no requested-pose or GPU-success inference.
use bevy::{ecs::system::SystemParam, prelude::*};

type CameraRow = (
    Entity,
    &'static Transform,
    &'static Camera,
    &'static Projection,
    Has<ChildOf>,
);

#[derive(SystemParam)]
pub(in crate::native_gameplay) struct PresentationObservation<'w, 's> {
    pub(super) static_hlod: Option<Res<'w, crate::static_hlod::StaticHlodStats>>,
    pub(super) static_gpu: Option<Res<'w, crate::static_hlod::StaticHlodGpuEvidence>>,
    vegetation: Option<Res<'w, crate::VegetationProjectionStats>>,
    species: Option<Res<'w, crate::VegetationPrototypeRuntimeStatus>>,
    materials: Option<Res<'w, Assets<crate::VisualValleyVegetationMaterial>>>,
    cameras: Query<'w, 's, CameraRow, With<crate::ForgePlayerCamera>>,
}

impl PresentationObservation<'_, '_> {
    pub(super) fn snapshot(&self) -> serde_json::Value {
        let camera = self.cameras.single().ok().map(|(entity, transform, camera, projection, parented)| {
            let perspective = match projection {
                Projection::Perspective(p) => Some(serde_json::json!({
                    "vertical_fov_radians": p.fov, "aspect_ratio": p.aspect_ratio, "near_m": p.near, "far_m": p.far,
                })),
                _ => None,
            };
            serde_json::json!({
                "entity": format!("{entity:?}"), "active": camera.is_active, "parented": parented,
                "position": transform.translation.to_array(), "rotation_xyzw": transform.rotation.to_array(),
                "scale": transform.scale.to_array(), "physical_viewport": camera.physical_viewport_size().map(|size| size.to_array()),
                "perspective": perspective,
            })
        });
        let wind = self.materials.as_deref().map(|materials| {
            materials
                .iter()
                .filter(|(_, material)| {
                    material.extension.prototype_parameters.x > 0.5
                        && material.extension.temporal_parameters.y <= 0.5
                })
                .map(|(_, material)| material.extension.wind_parameters.to_array())
                .collect::<Vec<_>>()
        });
        serde_json::json!({
            "camera": camera, "governed_wind_parameters": wind,
            "vegetation_last_visibility_pass": self.vegetation.as_deref(),
            "species_runtime_status": self.species.as_deref(),
            "scope": "Camera root transform after its fixed update; CPU visibility cache and CPU material uniforms, not draw calls or GPU timing",
        })
    }
}
