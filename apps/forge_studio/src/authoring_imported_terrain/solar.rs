//! Ordered solar/atmosphere publication for the imported scene generation.
use bevy::{ecs as bevy_ecs, prelude::*};
use forge_tidewater::{
    tidewater_atmosphere as atmosphere,
    tidewater_scene_environment::ResidentEnvironment,
    tidewater_sky::{self, frame_lighting::FrameLighting, solar},
};
use std::sync::{Arc, atomic::AtomicBool};

#[derive(Resource)]
struct State {
    generation: Arc<AtomicBool>,
    clock: solar::Clock,
}

/// Defaults match the pinned source's paused 16.2-hour sun. Refuse a different
/// acquired solar pose rather than silently moving it to this initial clock.
#[allow(clippy::cast_possible_truncation)] // Validated solar and atmosphere ranges.
pub(super) fn advance(
    world: &mut World,
    environment: &mut ResidentEnvironment,
    delta: f64,
    camera_y: f64,
) -> Result<Option<solar::Frame>, String> {
    if !camera_y.is_finite() || !(-1000.0..=60000.0).contains(&camera_y) {
        return Err("Hauteur caméra invalide pour l'atmosphère solaire".into());
    }
    let generation = world.resource::<atmosphere::Targets>().ready.clone();
    if world
        .get_resource::<State>()
        .is_none_or(|state| !Arc::ptr_eq(&state.generation, &generation))
    {
        let clock = solar::Clock::new(default())?;
        let acquired = world.resource::<atmosphere::Targets>().params[3].truncate();
        if clock.frame().sun.as_vec3() != acquired {
            return Err("Le soleil acquis diffère de l'horloge solaire initiale".into());
        }
        world.insert_resource(State { generation, clock });
    }
    let frame = *world.resource_mut::<State>().clock.advance(delta)?;
    let mut params = world.resource::<atmosphere::Targets>().params;
    // Same view-height quantization as the source acquisition adapter.
    let height = (camera_y + 0.5).max(0.5);
    let step = 1.02_f64.ln();
    let quantized = if height < 100.0 {
        (height / 2.0).round() * 2.0
    } else {
        ((height.ln() / step).round() * step).exp()
    };
    params[1].w = (6360.0 + (quantized / 1000.0).max(0.001)) as f32;
    params[3] = frame.sun.as_vec3().extend(params[3].w);
    let values = std::array::from_fn(|i| params[i / 4][i % 4]);
    atmosphere::controller::advance(world, delta, frame, values, false)?;
    environment.atmosphere.params = params;
    for (_, sky) in world
        .resource_mut::<Assets<tidewater_sky::SkyMaterial>>()
        .iter_mut()
        .filter(|(_, sky)| sky.transmittance == environment.atmosphere.images[0])
    {
        sky.atmosphere = params;
        sky.settings.moon_disk = frame.moon.as_vec3().extend(sky.settings.moon_disk.w);
        sky.settings.stars_time_night.z = frame.night as f32;
    }
    let Some(completed) = atmosphere::readback::latest(world, frame) else {
        return Ok(None);
    };
    let completed = completed?;
    let lighting = FrameLighting::new(frame, &completed.lighting)?;
    if world.contains_resource::<forge_tidewater::tidewater_ocean_optics::ResidentOptics>() {
        forge_tidewater::tidewater_ocean_optics::update_lighting(world, &lighting, params)?;
    }
    // CPU consumers (shadow projection and flare) need the same direct light.
    // The diffuse SH stays GPU-owned; never overwrite it with the acquired tail.
    environment.scene_lighting.sun_direction = frame.key.as_vec3().extend(0.0);
    environment.scene_lighting.sun_color =
        Vec3::from_array(completed.lighting.sun_color.map(|value| value as f32)).extend(0.0);
    for (_, material) in world
        .resource_mut::<Assets<forge_tidewater::tidewater_material::VillageMaterial>>()
        .iter_mut()
        .filter(|(_, material)| {
            material.shared_scene_lighting
                && (material.scene_lighting.sun_direction
                    != environment.scene_lighting.sun_direction
                    || material.scene_lighting.sun_color != environment.scene_lighting.sun_color)
        })
    {
        material.scene_lighting.sun_direction = environment.scene_lighting.sun_direction;
        material.scene_lighting.sun_color = environment.scene_lighting.sun_color;
    }
    Ok(Some(frame))
}
