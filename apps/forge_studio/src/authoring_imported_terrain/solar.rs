//! Ordered solar/atmosphere publication for the imported scene generation.
use bevy::{ecs as bevy_ecs, prelude::*};
use forge_engine::source_atmosphere as atmosphere;
use forge_engine::source_environment::scene::ResidentEnvironment;
use forge_engine::source_sky::publication;
use forge_engine::source_sky::solar;
use std::sync::{Arc, atomic::AtomicBool};

#[derive(Resource)]
struct State {
    generation: Arc<AtomicBool>,
    clock: solar::Clock,
}

/// Defaults match the pinned source's paused 16.2-hour sun. Refuse a different
/// acquired solar pose rather than silently moving it to this initial clock.
pub(super) fn advance(
    world: &mut World,
    environment: &mut ResidentEnvironment,
    delta: f64,
    camera_y: f64,
) -> Result<Option<solar::Frame>, String> {
    publication::validate_camera_height(camera_y)?;
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
    let Some(publication) = publication::advance(world, environment, delta, camera_y, frame)?
    else {
        return Ok(None);
    };
    if world.contains_resource::<forge_engine::source_water_optics::ResidentOptics>() {
        forge_engine::source_water_optics::update_lighting(
            world,
            &publication.packed,
            publication.params,
        )?;
    }
    publication.publish_direct(environment);
    for (_, material) in world
        .resource_mut::<Assets<forge_engine::source_surface::SourceSurfaceMaterial>>()
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
