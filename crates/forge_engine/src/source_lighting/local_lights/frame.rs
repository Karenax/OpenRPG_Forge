//! Inputs from the actual displayed sky and interactive camera, without a second clock.
use super::{Flashlight, Settings};
use crate::scene_camera::OffscreenSceneCamera;
use bevy::prelude::*;

pub struct SceneFrame {
    pub camera: Entity,
    pub settings: Settings,
}

/// Missing startup resources are pending. Ambiguous scene ownership is refused.
/// The caller controls strength and enablement; the displayed sky owns night.
pub fn from_scene(
    world: &mut World,
    enabled: bool,
    strength: f64,
    flashlight: Flashlight,
) -> Result<Option<SceneFrame>, String> {
    let mut cameras = world
        .query_filtered::<(Entity, &Camera), (With<Camera3d>, Without<OffscreenSceneCamera>)>();
    let mut active = cameras.iter(world).filter(|(_, camera)| camera.is_active);
    let Some((camera, _)) = active.next() else {
        return Ok(None);
    };
    if active.next().is_some() {
        return Err("Source local lights require one active scene camera".into());
    }
    let Some(night) = crate::source_sky::displayed_night(world)? else {
        return Ok(None);
    };
    let delta_seconds = world
        .get_resource::<Time>()
        .ok_or("Source local lights require the scene clock")?
        .delta_secs_f64();
    Ok(Some(SceneFrame {
        camera,
        settings: Settings {
            delta_seconds,
            night: f64::from(night),
            enabled,
            strength,
            flashlight,
        },
    }))
}
