//! Merge transient producers of the same masked shader and cascade target.
#[cfg(target_arch = "wasm32")]
use bevy::ecs as bevy_ecs;
use bevy::prelude::*;
use forge_engine::tidewater_fish_props::shadow::{Frame, MaskedMesh};
use std::{any::TypeId, collections::BTreeMap, sync::Arc};

pub(super) struct Village;
pub(super) struct Stand;

#[derive(Resource, Default)]
struct Contributions {
    frames: BTreeMap<TypeId, (u32, Frame)>,
    geometry_key: Vec<Arc<Vec<MaskedMesh>>>,
    geometry: Option<Arc<Vec<MaskedMesh>>>,
}

pub(super) fn publish<Owner: 'static>(
    world: &mut World,
    frame: Option<Frame>,
) -> Result<(), String> {
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

pub(super) fn install(app: &mut App) {
    app.init_resource::<Contributions>().add_systems(
        PostUpdate,
        emit.after(super::fish_props_scene::update)
            .after(super::fish_stand_scene::update),
    );
}

fn emit(world: &mut World) {
    let tick = world.resource::<bevy::diagnostic::FrameCount>().0;
    let result: Result<(), String> =
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
        });
    if let Err(error) = result {
        super::placements::report(format!("Ombres des poissons non projetées : {error}"));
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn frame(x: f32) -> Frame {
        Frame {
            target: Handle::default(),
            shader: Arc::new("same verified mask".into()),
            meshes: Arc::new(vec![MaskedMesh {
                vertices: vec![x as u8; 84],
                indices: vec![0; 12],
            }]),
            poses: vec![forge_engine::tidewater_fish_props::shadow::gpu::Pose {
                model: Mat4::from_translation(Vec3::X * x),
                enabled: true,
            }],
            time: 2.0,
        }
    }

    #[test]
    fn independent_owners_merge_and_retire_without_removing_the_other() {
        let mut world = World::new();
        world.insert_resource(bevy::diagnostic::FrameCount(1));
        let village = frame(3.0);
        let stand = frame(8.0);
        publish::<Village>(&mut world, Some(village.clone())).unwrap();
        publish::<Stand>(&mut world, Some(stand.clone())).unwrap();
        emit(&mut world);
        let merged = world.resource::<Frame>();
        assert_eq!(merged.meshes.len(), 2);
        assert_eq!(merged.poses.len(), 2);
        for (mesh, pose) in merged.meshes.iter().zip(&merged.poses) {
            assert_eq!(mesh.vertices[0], pose.model.w_axis.x as u8);
        }
        let geometry = merged.meshes.clone();
        world.resource_mut::<bevy::diagnostic::FrameCount>().0 = 2;
        publish::<Village>(&mut world, Some(village)).unwrap();
        publish::<Stand>(&mut world, Some(stand.clone())).unwrap();
        emit(&mut world);
        assert!(Arc::ptr_eq(&geometry, &world.resource::<Frame>().meshes));
        publish::<Village>(&mut world, None).unwrap();
        emit(&mut world);
        assert_eq!(world.resource::<Frame>().poses.len(), 1);
        assert_eq!(world.resource::<Frame>().poses[0].model.w_axis.x, 8.0);
        world.resource_mut::<bevy::diagnostic::FrameCount>().0 = 3;
        emit(&mut world);
        assert!(!world.contains_resource::<Frame>());
    }

    #[test]
    fn conflicting_or_incomplete_contributions_do_not_publish_a_partial_frame() {
        let mut world = World::new();
        world.insert_resource(bevy::diagnostic::FrameCount(1));
        publish::<Village>(&mut world, Some(frame(1.0))).unwrap();
        emit(&mut world);
        let mut conflict = frame(2.0);
        conflict.shader = Arc::new("different mask".into());
        publish::<Stand>(&mut world, Some(conflict)).unwrap();
        emit(&mut world);
        assert!(!world.contains_resource::<Frame>());
        let mut invalid = frame(3.0);
        invalid.poses.clear();
        assert!(publish::<Stand>(&mut world, Some(invalid)).is_err());
        let mut invalid = frame(3.0);
        invalid.time = f32::NAN;
        assert!(publish::<Stand>(&mut world, Some(invalid)).is_err());
    }
}
