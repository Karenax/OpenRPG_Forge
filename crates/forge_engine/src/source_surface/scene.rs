//! Shared live lighting and water bindings for source materials in a scene.
use super::{Settings, SourceSurfaceMaterial, underwater};
use crate::source_lighting::surfaces::{Receiver, live};
use bevy::{prelude::*, render::storage::ShaderBuffer};

#[derive(Clone)]
pub struct WaterBindings {
    pub params: underwater::CausticSampleParams,
    pub level: Handle<Image>,
    pub frame: Handle<ShaderBuffer>,
}

/// Bind neutral water only when the caller's scene explicitly has no water.
pub fn absent_water(world: &mut World) -> Result<WaterBindings, String> {
    let (params, level) = underwater::absent_water(world)?;
    Ok(WaterBindings {
        params,
        level,
        frame: underwater::NEUTRAL_FRAME,
    })
}

/// Reuse the scene's admitted live water inputs.
pub fn required_water(world: &World) -> Result<WaterBindings, String> {
    let input = world
        .get_resource::<underwater::CausticMaterialInputs>()
        .ok_or("Source object water lighting inputs disappeared")?;
    Ok(WaterBindings {
        params: input.params.clone(),
        level: input.light_maps.level.clone(),
        frame: input.light_maps.frame.clone(),
    })
}

/// Build the same live receiver material for any admitted source scene.
pub fn lighting_material(
    world: &mut World,
    water: &WaterBindings,
) -> Result<SourceSurfaceMaterial, String> {
    let snapshot = live::snapshot(world).ok_or("Source object scene frame absent")?;
    let receiver = Receiver::from_scene(world, snapshot.handle, false)?;
    let night = crate::source_sky::displayed_night(world)?
        .ok_or("Source object displayed sky is pending")?;
    Ok(SourceSurfaceMaterial {
        scene_buffer: receiver.scene,
        shared_scene_lighting: true,
        local_lights: receiver.local_lights,
        scene_lighting: world
            .resource::<crate::source_environment::scene::ResidentEnvironment>()
            .scene_lighting
            .clone(),
        environment: receiver.environment,
        hill_shadow: receiver.hill,
        object_shadow: receiver.objects,
        object_settings: receiver.object_settings,
        live_shadows: receiver.shadows,
        underwater_params: water.params.clone(),
        underwater_level: water.level.clone(),
        underwater_map_frame: water.frame.clone(),
        ground_bounce: None,
        bounce_settings: Vec4::ZERO,
        clouds: receiver.clouds,
        cloud_shadow: Some(receiver.cloud_shadow),
        kind: 0,
        first: default(),
        second: default(),
        third: default(),
        fourth: default(),
        settings: Settings {
            wind: snapshot.material_wind,
            color: Vec4::ONE,
            emissive: Vec4::ZERO,
            surface: Vec4::ZERO,
            flags: Vec4::new(0.0, night, 0.0, 0.0),
        },
    })
}
