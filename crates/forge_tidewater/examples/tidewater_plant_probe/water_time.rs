//! One simulation timestamp for all visible water layers in the walk.
use bevy::prelude::*;

#[derive(Resource, Default)]
pub(super) struct WaterTime {
    pub step: u32,
    pub time: f64,
    pub dt: f32,
}

pub(super) fn update(
    targets: Res<super::spray::Targets>,
    live: Option<Res<super::ocean::LiveClock>>,
    underwater: Option<Res<forge_engine::source_underwater::UnderwaterObservation>>,
    mut clock: ResMut<WaterTime>,
    mut water: ResMut<Assets<super::water_optics::WaterMaterial>>,
    mut spray: ResMut<Assets<super::spray_sprites::SprayMaterial>>,
    mut lips: Option<ResMut<Assets<forge_engine::source_breaker_render::LipMaterial>>>,
) {
    let step = targets
        .completed_steps
        .load(std::sync::atomic::Ordering::Acquire);
    if step <= 120 || clock.step == step {
        return;
    }
    let (simulation_time, dt) = if let Some(live) = live {
        let Some(frame) = live.completed(step) else {
            return;
        };
        (frame.time, frame.dt)
    } else {
        (f64::from(step) / 60.0, 1.0 / 60.0)
    };
    let apply = |frame: &mut [UVec4; 45]| {
        forge_engine::source_water_frame::write_time(frame, step, simulation_time, dt);
        if let Some(underwater) = underwater.as_deref() {
            forge_engine::source_water_frame::write_immersion(
                frame,
                underwater.camera_underwater(),
                underwater.water_height_m,
            );
        }
    };
    for (_, material) in water.iter_mut() {
        apply(&mut material.frame);
    }
    for (_, material) in spray.iter_mut() {
        apply(&mut material.frame);
    }
    if let Some(lips) = &mut lips {
        for (_, material) in lips.iter_mut() {
            apply(&mut material.frame);
        }
    }
    clock.step = step;
    clock.time = simulation_time;
    clock.dt = dt;
}
