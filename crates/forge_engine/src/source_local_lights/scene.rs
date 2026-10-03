//! Harvest only live projections; source descriptors remain owned by the module.
use super::{CameraPose, Error, Light};
use crate::tidewater_refraction::CaptureCamera;
use bevy::{math::DMat4, prelude::*};

/// A source light projected from a canonical asset/module descriptor. The
/// position is local to this entity; `GlobalTransform` supplies its live world pose.
/// Stable registration order reproduces the source's distance-sort tie behavior.
#[derive(Component, Clone, Debug)]
pub struct SourceLight {
    pub registration_order: u64,
    pub parameters: Light,
}

/// No camera fallback, implicit phase, intensity conversion or kind/slug branch.
/// Call after transform propagation. Hidden entities are not automatically
/// disabled: the source controls enabled independently of object visibility.
pub fn harvest(
    world: &mut World,
    camera: Entity,
    expected_registration_orders: &[u64],
) -> Result<(CameraPose, Vec<Light>), Error> {
    let active = world
        .get::<Camera>(camera)
        .is_some_and(|camera| camera.is_active);
    if !active || world.get::<CaptureCamera>(camera).is_some() {
        return Err(Error::MissingCamera);
    }
    if world.get::<Camera3d>(camera).is_none() {
        return Err(Error::MissingCamera);
    }
    #[cfg(feature = "physics-rapier")]
    if world
        .get::<crate::tidewater_sky::preview::PreviewCamera>(camera)
        .is_some()
    {
        return Err(Error::MissingCamera);
    }
    let global = world
        .get::<GlobalTransform>(camera)
        .ok_or(Error::MissingCamera)?;
    if world.get::<Transform>(camera).is_none() {
        return Err(Error::MissingCamera);
    }
    let pose = CameraPose {
        // The source scene camera is a root. Flatten a possible Bevy camera
        // rig into that convention, keeping world-space lamp selection.
        local_position: global.translation().as_dvec3(),
        world_from_view: DMat4::from_cols_array(&global.to_matrix().to_cols_array().map(f64::from)),
    };
    let mut query = world.query::<(&SourceLight, Option<&GlobalTransform>)>();
    let mut lights = Vec::new();
    for (source, transform) in query.iter(world) {
        let transform = transform.ok_or(Error::MissingLightTransform)?;
        let matrix = DMat4::from_cols_array(&transform.to_matrix().to_cols_array().map(f64::from));
        let mut light = source.parameters.clone();
        light.position = matrix.transform_point3(light.position);
        if let Some(direction) = light.direction {
            let direction = matrix.transform_vector3(direction);
            let length =
                (direction.x * direction.x + direction.y * direction.y + direction.z * direction.z)
                    .sqrt();
            if !length.is_finite() || length == 0.0 {
                return Err(Error::InvalidLight);
            }
            light.direction = Some(direction * (1.0 / length));
        }
        lights.push((source.registration_order, light));
    }
    lights.sort_by_key(|(order, _)| *order);
    if lights.windows(2).any(|pair| pair[0].0 == pair[1].0) {
        return Err(Error::DuplicateOrder);
    }
    let mut expected = expected_registration_orders.to_vec();
    expected.sort_unstable();
    if expected.windows(2).any(|pair| pair[0] == pair[1]) {
        return Err(Error::DuplicateOrder);
    }
    if lights.iter().map(|(order, _)| *order).ne(expected) {
        return Err(Error::InventoryMismatch);
    }
    Ok((pose, lights.into_iter().map(|(_, light)| light).collect()))
}
