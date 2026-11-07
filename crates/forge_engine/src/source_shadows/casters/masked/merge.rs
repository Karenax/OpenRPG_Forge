//! Merge independent transient producers of the same masked shadow frame.
use super::{Frame, MaskedMesh};
use bevy::prelude::*;
use std::{any::TypeId, collections::BTreeMap, sync::Arc};

/// Current-frame contributions and their reusable merged geometry.
#[derive(Resource, Default)]
pub struct Contributions {
    frames: BTreeMap<TypeId, (u32, Frame)>,
    geometry_key: Vec<Arc<Vec<MaskedMesh>>>,
    geometry: Option<Arc<Vec<MaskedMesh>>>,
}

/// Publish or retire one producer without affecting the others.
///
/// # Errors
/// Refuses non-finite time and mismatched mesh/pose inventories.
pub fn publish<Owner: 'static>(world: &mut World, frame: Option<Frame>) -> Result<(), String> {
    let tick = world.resource::<bevy::diagnostic::FrameCount>().0;
    if let Some(frame) = &frame
        && (frame.meshes.len() != frame.poses.len() || !frame.time.is_finite())
    {
        return Err("Invalid masked shadow contribution".into());
    }
    world.init_resource::<Contributions>();
    let mut state = world.resource_mut::<Contributions>();
    let owner = TypeId::of::<Owner>();
    if let Some(frame) = frame {
        state.frames.insert(owner, (tick, frame));
    } else {
        state.frames.remove(&owner);
    }
    Ok(())
}

/// Combine only this frame's valid contributions and publish one masked frame.
///
/// # Errors
/// Refuses incompatible target, shader or time without leaving a partial frame.
pub fn emit(world: &mut World) -> Result<(), String> {
    world.init_resource::<Contributions>();
    let tick = world.resource::<bevy::diagnostic::FrameCount>().0;
    world.resource_scope(|world, mut state: Mut<Contributions>| {
        state.frames.retain(|_, (observed, _)| *observed == tick);
        if state.frames.is_empty() {
            state.geometry_key.clear();
            state.geometry = None;
            world.remove_resource::<Frame>();
            return Ok(());
        }
        let first = state
            .frames
            .values()
            .next()
            .expect("nonempty contributions")
            .1
            .clone();
        if state.frames.values().any(|(_, value)| {
            value.target != first.target
                || value.shader != first.shader
                || value.time.to_bits() != first.time.to_bits()
        }) {
            world.remove_resource::<Frame>();
            return Err("Masked shadow producers disagree on target, shader or clock".into());
        }
        let keys: Vec<_> = state
            .frames
            .values()
            .map(|(_, value)| value.meshes.clone())
            .collect();
        if state.geometry_key.len() != keys.len()
            || state
                .geometry_key
                .iter()
                .zip(&keys)
                .any(|(a, b)| !Arc::ptr_eq(a, b))
        {
            state.geometry = Some(Arc::new(
                keys.iter()
                    .flat_map(|meshes| meshes.iter().cloned())
                    .collect(),
            ));
            state.geometry_key = keys;
        }
        let poses = state
            .frames
            .values()
            .flat_map(|(_, value)| value.poses.iter().copied())
            .collect();
        let meshes = state
            .geometry
            .as_ref()
            .expect("merged geometry prepared")
            .clone();
        world.insert_resource(Frame {
            target: first.target,
            shader: first.shader,
            meshes,
            poses,
            time: first.time,
        });
        Ok(())
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    struct ProducerA;
    struct ProducerB;

    fn frame(x: u8) -> Frame {
        Frame {
            target: Handle::default(),
            shader: Arc::new("same verified mask".into()),
            meshes: Arc::new(vec![MaskedMesh {
                vertices: vec![x; 84],
                indices: vec![0; 12],
            }]),
            poses: vec![crate::source_shadows::casters::masked::gpu::Pose {
                model: Mat4::from_translation(Vec3::X * f32::from(x)),
                enabled: true,
            }],
            time: 2.0,
        }
    }

    #[test]
    fn independent_owners_merge_and_retire_without_removing_the_other() {
        let mut world = World::new();
        world.insert_resource(bevy::diagnostic::FrameCount(1));
        let village = frame(3);
        let stand = frame(8);
        publish::<ProducerA>(&mut world, Some(village.clone())).unwrap();
        publish::<ProducerB>(&mut world, Some(stand.clone())).unwrap();
        emit(&mut world).unwrap();
        let merged = world.resource::<Frame>();
        assert_eq!(merged.meshes.len(), 2);
        assert_eq!(merged.poses.len(), 2);
        for (mesh, pose) in merged.meshes.iter().zip(&merged.poses) {
            assert!((f32::from(mesh.vertices[0]) - pose.model.w_axis.x).abs() < f32::EPSILON);
        }
        let geometry = merged.meshes.clone();
        world.resource_mut::<bevy::diagnostic::FrameCount>().0 = 2;
        publish::<ProducerA>(&mut world, Some(village)).unwrap();
        publish::<ProducerB>(&mut world, Some(stand.clone())).unwrap();
        emit(&mut world).unwrap();
        assert!(Arc::ptr_eq(&geometry, &world.resource::<Frame>().meshes));
        publish::<ProducerA>(&mut world, None).unwrap();
        emit(&mut world).unwrap();
        assert_eq!(world.resource::<Frame>().poses.len(), 1);
        assert!((world.resource::<Frame>().poses[0].model.w_axis.x - 8.0).abs() < f32::EPSILON);
        publish::<ProducerA>(&mut world, Some(frame(3))).unwrap();
        emit(&mut world).unwrap();
        assert_eq!(world.resource::<Frame>().poses.len(), 2);
        world.resource_mut::<bevy::diagnostic::FrameCount>().0 = 3;
        emit(&mut world).unwrap();
        assert!(!world.contains_resource::<Frame>());
    }

    #[test]
    fn conflicting_or_incomplete_contributions_do_not_publish_a_partial_frame() {
        let mut world = World::new();
        world.insert_resource(bevy::diagnostic::FrameCount(1));
        publish::<ProducerA>(&mut world, Some(frame(1))).unwrap();
        emit(&mut world).unwrap();
        let mut conflict = frame(2);
        conflict.shader = Arc::new("different mask".into());
        publish::<ProducerB>(&mut world, Some(conflict)).unwrap();
        assert!(emit(&mut world).is_err());
        assert!(!world.contains_resource::<Frame>());
        let mut conflict = frame(2);
        conflict.time = 3.0;
        publish::<ProducerB>(&mut world, Some(conflict)).unwrap();
        assert!(emit(&mut world).is_err());
        assert!(!world.contains_resource::<Frame>());
        let mut invalid = frame(3);
        invalid.poses.clear();
        assert!(publish::<ProducerB>(&mut world, Some(invalid)).is_err());
        let mut invalid = frame(3);
        invalid.time = f32::NAN;
        assert!(publish::<ProducerB>(&mut world, Some(invalid)).is_err());
    }
}
