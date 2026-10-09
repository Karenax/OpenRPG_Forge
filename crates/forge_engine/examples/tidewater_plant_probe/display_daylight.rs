//! Fixed solar clock driven by the presentation menu, with resident GPU inputs.
use super::State;
use bevy::prelude::*;
use forge_engine::{
    tidewater_atmosphere::{self as atmosphere, controller, readback},
    tidewater_material::{SceneLighting, scene_buffer},
    tidewater_sky::solar,
};
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
                world.get_resource_mut::<forge_engine::tidewater_hill_shadow::Settings>()
            {
                settings.sun_bake = daylight.frame.key.as_vec3();
            }
        }
        let frame = daylight.frame;
        let params = world.resource::<atmosphere::Targets>().params;
        let mut values: [f32; 16] = std::array::from_fn(|i| params[i / 4][i % 4]);
        values[12..15].copy_from_slice(&frame.sun.as_vec3().to_array());
        controller::advance(
            world,
            world.resource::<Time<Real>>().delta_secs_f64(),
            frame,
            values,
            false,
        )?;
        if let Some(mut skies) =
            world.get_resource_mut::<Assets<forge_engine::tidewater_sky::SkyMaterial>>()
        {
            let params: [Vec4; 4] = std::array::from_fn(|i| {
                Vec4::from_array(std::array::from_fn(|j| values[i * 4 + j]))
            });
            let changed: Vec<_> = skies
                .iter()
                .filter_map(|(id, m)| {
                    (m.atmosphere != params
                        || m.settings.moon_disk.truncate() != frame.moon.as_vec3()
                        || m.settings.stars_time_night.z.to_bits()
                            != (frame.night as f32).to_bits())
                    .then_some(id)
                })
                .collect();
            for id in changed {
                let mut m = skies.get_mut(id).expect("resident sky");
                m.atmosphere = params;
                m.settings.moon_disk = frame.moon.as_vec3().extend(1.0);
                m.settings.stars_time_night.z = frame.night as f32;
            }
        }
        let Some(completed) = readback::latest(world, frame) else {
            return Ok(());
        };
        let lighting = completed?.lighting;
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
        let ratio = Vec3::from_array(std::array::from_fn(|i| {
            (lighting.sky_irradiance[i] / irradiance[i].max(1e-8)).clamp(0.0, 8.0) as f32
        }));
        let scene = SceneLighting {
            sun_direction: frame.key.as_vec3().extend(0.0),
            sun_color: Vec3::from_array(lighting.sun_color.map(|x| x as f32)).extend(0.0),
            environment_sh: reference
                .environment_sh
                .map(|v| (v.truncate() * ratio).extend(0.0)),
        };
        scene_buffer::publish(world, &scene)?;
        if let Some(mut materials) =
            world.get_resource_mut::<Assets<forge_engine::tidewater_material::VillageMaterial>>()
        {
            for (_, material) in materials.iter_mut() {
                material.scene_lighting = scene.clone();
            }
        }
        let color = scene.sun_color.truncate();
        let strength = color.max_element().max(0.00001);
        let direction = -frame.key.as_vec3();
        let up = if direction.dot(Vec3::Y).abs() > 0.999 {
            Vec3::Z
        } else {
            Vec3::Y
        };
        let mut lights = world.query::<(&mut DirectionalLight, &mut Transform)>();
        for (mut light, mut pose) in lights.iter_mut(world) {
            light.color =
                Color::linear_rgb(color.x / strength, color.y / strength, color.z / strength);
            light.illuminance = 10000.0 * strength / 11.0;
            *pose = Transform::default().looking_to(direction, up);
        }
        daylight.published = Some(signature);
        Ok(())
    });
    if let Err(error) = result {
        error!(%error,"presentation daylight update failed");
        world.write_message(bevy::app::AppExit::error());
    }
}
