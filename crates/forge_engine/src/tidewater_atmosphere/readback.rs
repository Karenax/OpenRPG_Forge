//! Last completed atmosphere readback applied to the current solar frame.
//! Encoded-command readiness is not treated as GPU completion.
use crate::{
    tidewater_atmosphere::Targets,
    tidewater_sky::{solar, solar_lighting},
};
use bevy::{
    prelude::*,
    render::gpu_readback::{Readback, ReadbackComplete},
    render::storage::ShaderBuffer,
};
use std::sync::{
    Arc,
    atomic::{AtomicBool, Ordering},
};

#[derive(Clone)]
struct Generation {
    ready: Arc<AtomicBool>,
    irradiance: Handle<ShaderBuffer>,
}
impl Generation {
    fn current(&self, world_targets: Option<&Targets>) -> bool {
        world_targets
            .is_some_and(|t| Arc::ptr_eq(&self.ready, &t.ready) && self.irradiance == t.irradiance)
    }
}
#[derive(Resource)]
struct Active {
    reader: Entity,
    generation: Generation,
    received: AtomicBool,
}
#[derive(Resource)]
struct Snapshot {
    reader: Entity,
    generation: Generation,
    result: Result<solar_lighting::Readback, String>,
}
/// Source applies the last completed irradiance to the current sun each frame.
pub struct Completed {
    pub current_solar: solar::Frame,
    pub lighting: solar_lighting::Lighting,
}

/// Request exactly the source kernel's three vec4f values after encoding.
/// The callback checks generation identity again; request alone proves no GPU result.
pub fn request(world: &mut World, frame: solar::Frame) -> Result<Entity, String> {
    if world.contains_resource::<Active>() {
        return Err("Atmosphere readback already pending".into());
    }
    let targets = world
        .get_resource::<Targets>()
        .ok_or("Atmosphere targets absent")?;
    if !targets.ready.load(Ordering::Acquire) {
        return Err("Atmosphere commands not encoded yet".into());
    }
    validate_frame(&frame, targets)?;
    let generation = Generation {
        ready: targets.ready.clone(),
        irradiance: targets.irradiance.clone(),
    };
    let reader = world
        .spawn(Readback::buffer_range(generation.irradiance.clone(), 0, 48))
        .observe(receive)
        .id();
    world.insert_resource(Active {
        reader,
        generation,
        received: AtomicBool::new(false),
    });
    Ok(reader)
}

/// Reuse only a completed payload belonging to this resident generation.
/// Parameter changes within that generation are expected during moving sunlight.
pub fn latest(world: &World, frame: solar::Frame) -> Option<Result<Completed, String>> {
    let snapshot = world.get_resource::<Snapshot>()?;
    Some((|| {
        let targets = world
            .get_resource::<Targets>()
            .ok_or("Atmosphere targets absent")?;
        if !snapshot.generation.current(Some(targets)) {
            return Err("Atmosphere readback generation was replaced".into());
        }
        validate_frame(&frame, targets)?;
        let data = snapshot.result.as_ref().map_err(Clone::clone)?;
        Ok(Completed {
            current_solar: frame,
            lighting: solar_lighting::evaluate(frame.sun.y, frame.night, data)?,
        })
    })())
}

/// Retire a pending reader when its scene closes or changes ownership.
pub fn cancel(world: &mut World) {
    if let Some(active) = world.remove_resource::<Active>() {
        world.despawn(active.reader);
    }
    world.remove_resource::<Snapshot>();
}

/// Whether a GPU readback entity is currently awaiting its completion event.
#[must_use]
pub fn is_pending(world: &World) -> bool {
    world.contains_resource::<Active>()
}

/// Diagnostic identity of the most recent successful, current-generation event.
/// This is a runtime receipt, never a persistent document identifier.
#[must_use]
pub fn last_completed_reader(world: &World) -> Option<Entity> {
    let snapshot = world.get_resource::<Snapshot>()?;
    (snapshot.result.is_ok() && snapshot.generation.current(world.get_resource::<Targets>()))
        .then_some(snapshot.reader)
}

pub(super) fn validate_frame(frame: &solar::Frame, targets: &Targets) -> Result<(), String> {
    if !frame.hours.is_finite()
        || !(0.0..=24.0).contains(&frame.hours)
        || !frame.sun.is_finite()
        || (frame.sun.length_squared() - 1.0).abs() > 1e-12
        || !frame.night.is_finite()
        || !(0.0..=1.0).contains(&frame.night)
        || !frame.moon.is_finite()
        || (frame.moon.length_squared() - 1.0).abs() > 1e-12
        || frame.key
            != if frame.sun.y > -0.07 {
                frame.sun
            } else {
                frame.moon
            }
    {
        return Err("Invalid solar frame for atmosphere readback".into());
    }
    let t = ((-frame.sun.y - 0.02) / (0.18 - 0.02)).clamp(0.0, 1.0);
    if (frame.night - t * t * (3.0 - 2.0 * t)).abs() > 1e-12 {
        return Err("Solar night differs from the real sun elevation".into());
    }
    for (i, value) in frame.sun.to_array().into_iter().enumerate() {
        #[allow(clippy::cast_possible_truncation)]
        let value = value as f32;
        if value.to_bits() != targets.params[3][i].to_bits() {
            return Err("Solar frame differs from atmosphere parameters".into());
        }
    }
    Ok(())
}

fn receive(
    event: On<ReadbackComplete>,
    state: Option<Res<Active>>,
    targets: Option<Res<Targets>>,
    mut commands: Commands,
) {
    let Some(state) = state else {
        return;
    };
    if event.entity != state.reader {
        return;
    }
    // Render extraction can deliver multiple queued completions before deferred
    // removal of Active. Publish and retire this reader only once.
    if state.received.swap(true, Ordering::AcqRel) {
        return;
    }
    let result = if state.generation.current(targets.as_deref()) {
        solar_lighting::Readback::decode(&event.data)
    } else {
        Err("Completed readback belongs to a retired atmosphere generation".into())
    };
    commands.entity(state.reader).despawn();
    commands.insert_resource(Snapshot {
        reader: state.reader,
        generation: state.generation.clone(),
        result,
    });
    commands.remove_resource::<Active>();
}

#[cfg(test)]
mod tests {
    use super::*;
    fn solar(hours: f64) -> solar::Frame {
        *solar::Clock::new(solar::Settings { hours, ..default() })
            .unwrap()
            .frame()
    }
    fn scene() -> (World, solar::Frame) {
        let frame = solar(16.2);
        let mut world = World::new();
        world.init_resource::<Assets<ShaderBuffer>>();
        let buffer = world
            .resource_mut::<Assets<ShaderBuffer>>()
            .add(ShaderBuffer::from(vec![Vec4::ZERO; 4]));
        let mut params = [Vec4::ZERO; 4];
        params[3] = frame.sun.as_vec3().extend(0.0);
        world.insert_resource(Targets {
            params,
            images: vec![],
            irradiance: buffer,
            ready: Arc::new(AtomicBool::new(true)),
        });
        (world, frame)
    }
    fn bytes() -> Vec<u8> {
        [
            0.15_f32, 0.21, 0.33, 1.0, 0.8, 0.7, 0.6, 1.0, 0.24, 0.31, 0.49, 1.0,
        ]
        .into_iter()
        .flat_map(f32::to_le_bytes)
        .collect()
    }
    fn complete(world: &mut World, reader: Entity) {
        world.trigger(ReadbackComplete {
            entity: reader,
            data: bytes(),
        });
        world.flush();
    }
    fn expected(frame: solar::Frame) -> solar_lighting::Lighting {
        solar_lighting::evaluate(
            frame.sun.y,
            frame.night,
            &solar_lighting::Readback::decode(&bytes()).unwrap(),
        )
        .unwrap()
    }
    #[test]
    fn exact_payload_is_reused_after_reader_retires() {
        let (mut world, frame) = scene();
        let reader = request(&mut world, frame).unwrap();
        match world.get::<Readback>(reader).unwrap() {
            Readback::Buffer {
                start_offset_and_size,
                ..
            } => assert_eq!(*start_offset_and_size, Some((0, 48))),
            _ => panic!("buffer required"),
        }
        assert!(latest(&world, frame).is_none());
        complete(&mut world, reader);
        for _ in 0..3 {
            let result = latest(&world, frame).unwrap().unwrap();
            assert_eq!(result.current_solar, frame);
            assert_eq!(result.lighting, expected(frame));
        }
        assert!(world.get_entity(reader).is_err());
    }
    #[test]
    fn sunlight_can_move_during_and_after_readback() {
        let (mut world, original) = scene();
        let reader = request(&mut world, original).unwrap();
        let twilight = solar(18.2);
        world.resource_mut::<Targets>().params[3] = twilight.sun.as_vec3().extend(0.0);
        complete(&mut world, reader);
        assert_eq!(
            latest(&world, twilight).unwrap().unwrap().lighting,
            expected(twilight)
        );
        let next_reader = request(&mut world, twilight).unwrap();
        let night = solar(23.0);
        world.resource_mut::<Targets>().params[3] = night.sun.as_vec3().extend(0.0);
        // A pending new read must not erase the completed irradiance used by App.
        assert_eq!(
            latest(&world, night).unwrap().unwrap().lighting,
            expected(night)
        );
        assert!(request(&mut world, night).is_err());
        complete(&mut world, next_reader);
        assert_eq!(
            latest(&world, night).unwrap().unwrap().lighting,
            expected(night)
        );
    }
    #[test]
    fn scene_generation_and_buffer_changes_are_still_refused() {
        let (mut world, frame) = scene();
        let reader = request(&mut world, frame).unwrap();
        world.resource_mut::<Targets>().ready = Arc::new(AtomicBool::new(true));
        complete(&mut world, reader);
        assert!(latest(&world, frame).unwrap().is_err());
        let reader = request(&mut world, frame).unwrap();
        complete(&mut world, reader);
        let replacement = world
            .resource_mut::<Assets<ShaderBuffer>>()
            .add(ShaderBuffer::from(vec![Vec4::ZERO; 4]));
        world.resource_mut::<Targets>().irradiance = replacement;
        assert!(latest(&world, frame).unwrap().is_err());
    }
    #[test]
    fn wrong_solar_requests_do_not_change_pending_state() {
        let (mut world, frame) = scene();
        assert!(request(&mut world, solar(10.0)).is_err());
        assert!(!world.contains_resource::<Active>());
        let reader = request(&mut world, frame).unwrap();
        assert!(request(&mut world, solar(10.0)).is_err());
        assert_eq!(world.resource::<Active>().reader, reader);
    }
    #[test]
    fn cancel_retires_reader_and_cached_payload() {
        let (mut world, frame) = scene();
        let reader = request(&mut world, frame).unwrap();
        complete(&mut world, reader);
        let pending = request(&mut world, frame).unwrap();
        cancel(&mut world);
        assert!(world.get_entity(pending).is_err());
        assert!(latest(&world, frame).is_none());
        assert!(!world.contains_resource::<Active>());
    }
}
