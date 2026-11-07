//! Fixed solar clock driven by the presentation menu, with resident GPU inputs.
use super::State;
use bevy::prelude::*;
use forge_engine::source_atmosphere as atmosphere;
use forge_engine::source_atmosphere::controller;
use forge_engine::source_lighting::SceneLighting;
use forge_engine::source_lighting::scene_buffer;
use forge_engine::source_sky::publication;
use forge_engine::source_sky::solar;
#[derive(Resource)]
pub(super) struct Daylight {
    pub frame: solar::Frame,
    pub completed_hours: Option<f64>,
    reference: Option<(SceneLighting, [f64; 3])>,
    published: Option<(f64, [f64; 3], [f64; 3])>,
}
impl Daylight {
    pub fn new(hours: f64) -> Result<Self, String> {
        solar::Clock::new(solar::Settings { hours, ..default() })?;
        Ok(Self {
            frame: *solar::Clock::new(default())?.frame(),
            completed_hours: None,
            reference: None,
            published: None,
        })
    }
}
#[allow(
    clippy::too_many_lines,
    reason = "Publish one coherent solar frame to the resident scene, sky and shadows"
)]
#[allow(
    clippy::cast_possible_truncation,
    reason = "Solar values are bounded and completed lighting is validated as representable in f32"
)]
pub(super) fn update(world: &mut World) {
    if !world.resource::<forge_engine::PlayerSimulationReady>().0
        || !world.contains_resource::<atmosphere::Targets>()
    {
        return;
    }
    let result = world.resource_scope(|world, mut daylight: Mut<Daylight>| -> Result<(), String> {
        let hours = f64::from(world.resource::<State>().preferences.minutes) / 60.0;
        if daylight.reference.is_some() && hours.to_bits() != daylight.frame.hours.to_bits() {
            daylight.frame = *solar::Clock::new(solar::Settings { hours, ..default() })?.frame();
            daylight.completed_hours = None;
            daylight.published = None;
            controller::cancel(world);
            if let Some(mut sources) =
                world.get_resource_mut::<crate::object_shadow::live::Sources>()
            {
                sources.set_sun(daylight.frame.key)?;
            }
            if let Some(mut settings) =
                world.get_resource_mut::<forge_engine::source_shadows::heightfield::Settings>()
            {
                settings.sun_bake = daylight.frame.key.as_vec3();
            }
        }
        let frame = daylight.frame;
        let params = world.resource::<atmosphere::Targets>().params;
        let mut values: [f32; 16] = std::array::from_fn(|i| params[i / 4][i % 4]);
        values[12..15].copy_from_slice(&frame.sun.as_vec3().to_array());
        let params =
            std::array::from_fn(|i| Vec4::from_array(std::array::from_fn(|j| values[i * 4 + j])));
        let Some(publication) = publication::advance_uniforms(
            world,
            world.resource::<Time<Real>>().delta_secs_f64(),
            frame,
            params,
            None,
            Some(1.0),
        )?
        else {
            return Ok(());
        };
        let lighting = &publication.lighting;
        daylight.completed_hours = Some(frame.hours);
        let signature = (frame.hours, lighting.sun_color, lighting.sky_irradiance);
        if daylight.published == Some(signature) {
            return Ok(());
        }
        if daylight.reference.is_none() {
            let acquired = world
                .resource::<crate::Probe>()
                .village
                .as_ref()
                .ok_or("village lighting absent")?
                .lighting()
                .clone();
            // Bootstrap at the acquired clock before applying a persisted hour.
            daylight.reference = Some((acquired, lighting.sky_irradiance));
        }
        let (reference, irradiance) = daylight.reference.as_ref().expect("reference initialized");
        let scene = publication::scaled_reference(reference, *irradiance, &publication);
        scene_buffer::publish(world, &scene)?;
        if let Some(mut materials) =
            world.get_resource_mut::<Assets<forge_engine::source_surface::SourceSurfaceMaterial>>()
        {
            for (_, material) in materials.iter_mut() {
                material.scene_lighting = scene.clone();
            }
        }
        publication::sync_directional_lights(world, &scene, frame);
        daylight.published = Some(signature);
        Ok(())
    });
    if let Err(error) = result {
        error!(%error,"presentation daylight update failed");
        world.write_message(bevy::app::AppExit::error());
    }
}
