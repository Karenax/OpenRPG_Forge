//! Source irradiance cadence, retaining the last completed readback while pending.
use super::{Targets, live::Work, readback, update_parameters};
use crate::tidewater_sky::solar;
use bevy::prelude::*;
use std::sync::{
    Arc,
    atomic::{AtomicBool, Ordering},
};

#[derive(Resource, Clone)]
struct Controller {
    generation: Arc<AtomicBool>,
    timer: f64,
    awaiting_encoding: Option<u64>,
}

/// Advance atmosphere uniforms and the source 0.25-second irradiance cadence.
/// Call after the solar clock and before cloud submission. Shader/scattering
/// changes use explicit `invalidate_static`; values include the quantized view
/// height and true solar direction. Readback remains asynchronous.
pub fn advance(
    world: &mut World,
    delta: f64,
    frame: solar::Frame,
    values: [f32; 16],
    invalidate_static: bool,
) -> Result<(), String> {
    if !delta.is_finite() || delta < 0.0 || values.iter().any(|v| !v.is_finite()) {
        return Err("invalid atmosphere frame delta or uniforms".into());
    }
    let targets = world
        .get_resource::<Targets>()
        .ok_or("atmosphere targets absent")?;
    let mut next_targets = targets.clone();
    next_targets.params =
        std::array::from_fn(|i| Vec4::from_array(std::array::from_fn(|j| values[i * 4 + j])));
    readback::validate_frame(&frame, &next_targets)?;
    let replaced = world
        .get_resource::<Controller>()
        .is_some_and(|c| !Arc::ptr_eq(&c.generation, &targets.ready));
    let mut controller = world
        .get_resource::<Controller>()
        .filter(|_| !replaced)
        .cloned()
        .unwrap_or(Controller {
            generation: targets.ready.clone(),
            timer: 0.0,
            awaiting_encoding: None,
        });
    let encoded = world
        .get_resource::<Work>()
        .ok_or("atmosphere work absent")?
        .encoded
        .load(Ordering::Acquire);
    controller.timer -= delta;
    let integrate = controller.timer <= 0.0
        && !readback::is_pending(world)
        && controller.awaiting_encoding.is_none();
    update_parameters(world, values, invalidate_static, integrate)?;
    if replaced {
        readback::cancel(world);
    }
    if integrate {
        controller.timer = 0.25;
        controller.awaiting_encoding = Some(world.resource::<Work>().revision);
    }
    if controller
        .awaiting_encoding
        .is_some_and(|revision| encoded >= revision)
    {
        readback::request(world, frame)?;
        controller.awaiting_encoding = None;
    }
    world.insert_resource(controller);
    Ok(())
}

/// Remove the scene-owned controller and any pending/cached readback.
pub fn cancel(world: &mut World) {
    world.remove_resource::<Controller>();
    readback::cancel(world);
}

#[cfg(test)]
mod tests {
    use super::super::{Input, KernelInput, Registered};
    use super::*;
    use bevy::render::{
        gpu_readback::{Readback, ReadbackComplete},
        storage::ShaderBuffer,
    };

    fn scene() -> (World, solar::Frame, [f32; 16]) {
        let frame = *solar::Clock::new(solar::Settings::default())
            .unwrap()
            .frame();
        let mut values = [0.0; 16];
        values[12..15].copy_from_slice(&frame.sun.as_vec3().to_array());
        let mut world = World::new();
        world.init_resource::<Assets<Image>>();
        world.init_resource::<Assets<ShaderBuffer>>();
        world.insert_resource(Registered);
        world.insert_resource(KernelInput(Arc::new(
            Input::new(
                std::array::from_fn(|_| "qualified externally".into()),
                values,
            )
            .unwrap(),
        )));
        let irradiance = world
            .resource_mut::<Assets<ShaderBuffer>>()
            .add(ShaderBuffer::from(vec![Vec4::ZERO; 4]));
        world.insert_resource(Targets {
            params: std::array::from_fn(|i| {
                Vec4::from_array(std::array::from_fn(|j| values[i * 4 + j]))
            }),
            images: vec![],
            irradiance,
            ready: Arc::new(AtomicBool::new(true)),
        });
        world.insert_resource(Work::default());
        (world, frame, values)
    }

    #[test]
    #[expect(
        clippy::float_cmp,
        reason = "Exact source flags and preserved payload values must not admit a tolerance"
    )]
    fn cadence_waits_for_encoding_and_pending_readback_before_refreshing() {
        let (mut world, frame, values) = scene();
        advance(&mut world, 0.0, frame, values, false).unwrap();
        assert!(!readback::is_pending(&world));
        assert_eq!(world.resource::<Controller>().timer, 0.25);
        advance(&mut world, 0.1, frame, values, false).unwrap();
        assert!(!readback::is_pending(&world));
        let work = world.resource::<Work>();
        work.encoded.store(work.revision, Ordering::Release);
        advance(&mut world, 0.0, frame, values, false).unwrap();
        assert!(readback::is_pending(&world));
        let revision = world.resource::<Work>().revision;
        advance(&mut world, 1.0, frame, values, false).unwrap();
        assert_eq!(world.resource::<Work>().revision, revision);
        let reader = world
            .query_filtered::<Entity, With<Readback>>()
            .single(&world)
            .unwrap();
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
        advance(&mut world, 0.0, frame, values, false).unwrap();
        assert!(world.resource::<Work>().revision > revision);
        assert_eq!(world.resource::<Controller>().timer, 0.25);
        assert!(readback::latest(&world, frame).unwrap().is_ok());
        cancel(&mut world);
        assert!(world.get_resource::<Controller>().is_none());
        assert!(readback::latest(&world, frame).is_none());
    }

    #[test]
    #[expect(
        clippy::float_cmp,
        reason = "Exact source flags and preserved payload values must not admit a tolerance"
    )]
    fn invalid_frame_or_delta_does_not_create_a_controller_or_change_uniforms() {
        let (mut world, frame, values) = scene();
        assert!(advance(&mut world, -1.0, frame, values, false).is_err());
        let mut wrong = values;
        wrong[12] += 0.1;
        assert!(advance(&mut world, 0.1, frame, wrong, false).is_err());
        assert!(world.get_resource::<Controller>().is_none());
        assert_eq!(world.resource::<Work>().revision, 1);
        assert_eq!(world.resource::<Targets>().params[3].x, values[12]);
    }
}
