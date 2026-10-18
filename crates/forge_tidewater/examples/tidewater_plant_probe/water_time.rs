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
    underwater: Option<Res<forge_tidewater::tidewater_underwater::UnderwaterObservation>>,
    mut clock: ResMut<WaterTime>,
    mut water: ResMut<Assets<super::water_optics::WaterMaterial>>,
    mut spray: ResMut<Assets<super::spray_sprites::SprayMaterial>>,
    mut lips: Option<ResMut<Assets<super::breaker_lip::LipMaterial>>>,
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
    #[allow(clippy::cast_possible_truncation)]
    let time = simulation_time as f32;
    let apply = |frame: &mut [UVec4; 45]| {
        // Original Frame fields: frameIndex at byte 576, time at byte 580.
        frame[36].x = step;
        frame[36].y = time.to_bits();
        frame[36].z = dt.to_bits();
        if let Some(underwater) = underwater.as_deref() {
            // Frame.js: skyIrradiance.w is cameraUnderwater and
            // horizonColor.w is cameraWaterHeight.
            frame[39].w = (if underwater.camera_underwater() {
                1.0_f32
            } else {
                0.0_f32
            })
            .to_bits();
            frame[40].w = underwater.water_height_m.to_bits();
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
