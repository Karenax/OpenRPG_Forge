//! Continue a resident cloud generation without replacing its temporal textures.
use super::{FrameView, StagedInput, Targets};
use crate::cloud_controller::{Output, bytes};
use bevy::{prelude::*, render::extract_resource::ExtractResource};
use std::sync::{Arc, atomic::Ordering};

#[derive(Resource, Clone, Default, ExtractResource)]
pub(super) struct Pending(pub Option<Arc<Frame>>);

pub(super) struct Frame {
    pub step: u32,
    pub output: Output,
    pub local: Vec<u8>,
    pub global: Vec<u8>,
    pub settings: Vec<u8>,
    pub atmosphere: Option<Vec<u8>>,
}
impl Frame {
    pub fn view(&self) -> FrameView<'_> {
        FrameView {
            index: self.step as usize - 1,
            local: &self.local,
            global: &self.global,
            atmosphere: self.atmosphere.as_deref(),
            shadow_full: self.output.shadow_full,
            panorama_full: self.output.panorama_full,
            trace_view: self.output.trace_view,
            view_index: self.output.view_index,
        }
    }
}

/// One frame awaiting encoding: the caller must retain its controller while waiting.
/// Already encoded GPU work stays ordered on Bevy's render queue; this is not a GPU fence.
/// Acquisition readback completion, if required, remains the caller's responsibility.
pub fn can_submit(world: &World) -> bool {
    let (Some(targets), Some(pending)) = (
        world.get_resource::<Targets>(),
        world.get_resource::<Pending>(),
    ) else {
        return false;
    };
    world.contains_resource::<StagedInput>()
        && targets.images.len() == 10
        && targets.ready.load(Ordering::Acquire)
        && pending
            .0
            .as_ref()
            .is_none_or(|frame| targets.completed_steps.load(Ordering::Acquire) >= frame.step)
        && targets.completed_steps.load(Ordering::Acquire) < u32::MAX
}

/// Submit a fixed-resolution controller frame with the generation's acquired lighting.
/// Camera, clock, wind, cloud history and shadow sampling are supplied by `output`.
/// Use `submit_solar` for current sunlight without replacing temporal history.
/// Changing resolution still requires a separately prepared generation.
pub fn submit(world: &mut World, output: &Output) -> Result<u32, String> {
    validate(output)?;
    if !can_submit(world) {
        return Err("cloud generation not ready for a live frame".into());
    }
    let staged = world.resource::<StagedInput>();
    let global = staged
        .input
        .shadow
        .as_ref()
        .and_then(|s| s.frames.last())
        .ok_or("cloud lighting frame absent")?
        .1
        .clone();
    let step = world
        .resource::<Targets>()
        .completed_steps
        .load(Ordering::Acquire)
        + 1;
    world.insert_resource(Pending(Some(Arc::new(Frame {
        step,
        output: output.clone(),
        local: bytes(&output.frame),
        global,
        settings: bytes(&output.settings),
        atmosphere: None,
    }))));
    Ok(step)
}

/// Submit the current sun and last completed atmosphere lighting with unchanged
/// temporal textures. The supplied solar frame must match the resident LUT params.
pub fn submit_solar(
    world: &mut World,
    output: &Output,
    solar: crate::tidewater_sky::solar::Frame,
) -> Result<u32, String> {
    validate(output)?;
    if !can_submit(world) {
        return Err("cloud generation not ready for a live solar frame".into());
    }
    let completed = crate::tidewater_atmosphere::readback::latest(world, solar)
        .ok_or("atmosphere lighting readback unavailable")??;
    let targets = world.resource::<crate::tidewater_atmosphere::Targets>();
    let staged = world.resource::<StagedInput>();
    let shared = staged
        .atmosphere
        .as_ref()
        .ok_or("shared cloud atmosphere absent")?;
    if !Arc::ptr_eq(&shared.ready, &targets.ready) || shared.images != targets.images {
        return Err("cloud atmosphere generation was replaced".into());
    }
    let mut global = staged
        .input
        .shadow
        .as_ref()
        .and_then(|s| s.frames.last())
        .ok_or("cloud lighting frame absent")?
        .1
        .clone();
    if global.len() != 720 {
        return Err("source Frame uniform must be 720 bytes".into());
    }
    write_solar(&mut global, &completed)?;
    let atmosphere = targets
        .params
        .iter()
        .flat_map(|v| v.to_array())
        .flat_map(f32::to_le_bytes)
        .collect();
    let step = world
        .resource::<Targets>()
        .completed_steps
        .load(Ordering::Acquire)
        + 1;
    world.insert_resource(Pending(Some(Arc::new(Frame {
        step,
        output: output.clone(),
        local: bytes(&output.frame),
        global,
        settings: bytes(&output.settings),
        atmosphere: Some(atmosphere),
    }))));
    Ok(step)
}

#[allow(clippy::cast_possible_truncation)]
fn write_solar(
    global: &mut [u8],
    completed: &crate::tidewater_atmosphere::readback::Completed,
) -> Result<(), String> {
    // Exact canonical Frame.js layout, verified by frame-lighting-layout-v1.json.
    let fields = [
        (592, completed.current_solar.key.to_array().to_vec()),
        (604, vec![completed.current_solar.night]),
        (608, completed.lighting.sun_color.to_vec()),
        (624, completed.lighting.sky_irradiance.to_vec()),
        (640, completed.lighting.horizon_color.to_vec()),
    ];
    if global.len() != 720
        || fields
            .iter()
            .flat_map(|(_, v)| v)
            .any(|v| !v.is_finite() || !(*v as f32).is_finite())
    {
        return Err("invalid live solar Frame uniforms".into());
    }
    for (offset, values) in fields {
        for (index, value) in values.into_iter().enumerate() {
            global[offset + index * 4..offset + index * 4 + 4]
                .copy_from_slice(&(value as f32).to_le_bytes());
        }
    }
    Ok(())
}

fn validate(output: &Output) -> Result<(), String> {
    if output.source_size != [240, 135]
        || output.history_size != [960, 540]
        || output.view_index > 1
        || output.weather_dispatch
        || output
            .frame
            .iter()
            .chain(output.settings.iter())
            .chain(output.public.iter())
            .flatten()
            .any(|v| !v.is_finite())
    {
        return Err("unsupported or invalid live cloud frame".into());
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::sync::atomic::{AtomicBool, AtomicU32};
    #[test]
    fn solar_uniform_update_preserves_camera_water_wind_and_exposure() {
        let solar = *crate::tidewater_sky::solar::Clock::new(default())
            .unwrap()
            .frame();
        let lighting = crate::tidewater_sky::solar_lighting::Lighting {
            sun_color: [1.0, 2.0, 3.0],
            sky_irradiance: [0.1, 0.2, 0.3],
            horizon_color: [0.4, 0.5, 0.6],
        };
        let completed = crate::tidewater_atmosphere::readback::Completed {
            current_solar: solar,
            lighting,
        };
        let mut global = vec![0x5a; 720];
        write_solar(&mut global, &completed).unwrap();
        for (i, byte) in global.iter().enumerate() {
            if !(592..620).contains(&i) && !(624..636).contains(&i) && !(640..652).contains(&i) {
                assert_eq!(*byte, 0x5a, "unrelated Frame byte {i}");
            }
        }
        assert_eq!(
            f32::from_le_bytes(global[608..612].try_into().unwrap()),
            1.0
        );
        assert_eq!(
            f32::from_le_bytes(global[604..608].try_into().unwrap()),
            solar.night as f32
        );
        let before = global.clone();
        let mut invalid = completed;
        invalid.lighting.sun_color[0] = f64::NAN;
        assert!(write_solar(&mut global, &invalid).is_err());
        assert_eq!(before, global);
    }

    fn output() -> Output {
        Output {
            frame: [[0.0; 4]; 34],
            settings: [[0.0; 4]; 13],
            public: [[0.0; 4]; 5],
            weather_dispatch: false,
            shadow_full: false,
            panorama_full: false,
            trace_view: true,
            view_index: 0,
            source_size: [240, 135],
            history_size: [960, 540],
        }
    }
    fn world() -> World {
        let mut world = World::new();
        world.insert_resource(Targets {
            completed_steps: Arc::new(AtomicU32::new(120)),
            submissions: Arc::new(AtomicU32::new(120)),
            images: vec![Handle::default(); 10],
            ready: Arc::new(AtomicBool::new(true)),
        });
        world.insert_resource(Pending::default());
        world.insert_resource(StagedInput {
            input: Arc::new(super::super::Input {
                stepped: true,
                shaders: default(),
                settings: vec![0; 208],
                shadow: Some(super::super::shadow::Input {
                    shader: String::new(),
                    noise: vec![],
                    frames: vec![(vec![0; 544], vec![0; 720])],
                    trace: None,
                }),
            }),
            atmosphere: None,
        });
        world
    }
    #[test]
    fn backpressure_keeps_each_temporal_frame_until_encoded() {
        assert!(!can_submit(&World::new()));
        let mut world = world();
        assert_eq!(submit(&mut world, &output()).unwrap(), 121);
        assert!(!can_submit(&world));
        assert!(submit(&mut world, &output()).is_err());
        world
            .resource::<Targets>()
            .completed_steps
            .store(121, Ordering::Release);
        assert!(can_submit(&world));
        assert_eq!(submit(&mut world, &output()).unwrap(), 122);
        world
            .resource::<Targets>()
            .ready
            .store(false, Ordering::Release);
        assert!(!can_submit(&world));
    }
    #[test]
    fn invalid_frame_preserves_pending_history() {
        let mut world = world();
        let mut invalid = output();
        for variant in 0..4 {
            match variant {
                0 => invalid.view_index = 2,
                1 => invalid.history_size[0] = 1,
                2 => invalid.frame[0][0] = f32::NAN,
                _ => invalid.weather_dispatch = true,
            }
            assert!(submit(&mut world, &invalid).is_err());
            assert!(world.resource::<Pending>().0.is_none());
            invalid = output();
        }
        assert_eq!(
            world
                .resource::<Targets>()
                .completed_steps
                .load(Ordering::Acquire),
            120
        );
    }

    #[test]
    fn solar_submission_uses_completed_light_and_refuses_replaced_atmosphere() {
        use bevy::render::{gpu_readback::ReadbackComplete, storage::ShaderBuffer};
        let mut world = world();
        let solar = *crate::tidewater_sky::solar::Clock::new(default())
            .unwrap()
            .frame();
        world.init_resource::<Assets<ShaderBuffer>>();
        let irradiance = world
            .resource_mut::<Assets<ShaderBuffer>>()
            .add(ShaderBuffer::from(vec![Vec4::ZERO; 4]));
        let mut params = [Vec4::ZERO; 4];
        params[3] = solar.sun.as_vec3().extend(0.0);
        let atmosphere = crate::tidewater_atmosphere::Targets {
            params,
            images: vec![],
            irradiance,
            ready: Arc::new(AtomicBool::new(true)),
        };
        world.resource_mut::<StagedInput>().atmosphere = Some(super::super::SharedAtmosphere {
            images: atmosphere.images.clone(),
            params,
            ready: atmosphere.ready.clone(),
        });
        world.insert_resource(atmosphere);
        assert!(submit_solar(&mut world, &output(), solar).is_err());
        assert!(world.resource::<Pending>().0.is_none());
        let reader = crate::tidewater_atmosphere::readback::request(&mut world, solar).unwrap();
        world.trigger(ReadbackComplete {
            entity: reader,
            data: [
                0.1_f32, 0.2, 0.3, 1.0, 0.8, 0.7, 0.6, 1.0, 0.2, 0.3, 0.4, 1.0,
            ]
            .into_iter()
            .flat_map(f32::to_le_bytes)
            .collect(),
        });
        world.flush();
        assert_eq!(submit_solar(&mut world, &output(), solar).unwrap(), 121);
        let pending = world.resource::<Pending>().0.as_ref().unwrap().clone();
        assert_eq!(pending.atmosphere.as_ref().unwrap().len(), 64);
        assert_eq!(pending.global.len(), 720);
        assert_eq!(
            world
                .resource::<Targets>()
                .completed_steps
                .load(Ordering::Acquire),
            120
        );
        assert!(submit_solar(&mut world, &output(), solar).is_err());
        assert!(Arc::ptr_eq(
            &pending,
            world.resource::<Pending>().0.as_ref().unwrap()
        ));
        world
            .resource_mut::<Targets>()
            .completed_steps
            .store(121, Ordering::Release);
        world
            .resource_mut::<StagedInput>()
            .atmosphere
            .as_mut()
            .unwrap()
            .ready = Arc::new(AtomicBool::new(true));
        assert!(submit_solar(&mut world, &output(), solar).is_err());
        assert!(Arc::ptr_eq(
            &pending,
            world.resource::<Pending>().0.as_ref().unwrap()
        ));
    }
}
