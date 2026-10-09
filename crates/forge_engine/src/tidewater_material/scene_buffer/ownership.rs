//! Prevent CPU asset uploads from overwriting a live GPU lighting buffer.
use bevy::{prelude::*, render::storage::ShaderBuffer};
use std::{collections::HashMap, sync::Arc};
#[derive(Resource, Default)]
struct Owners(HashMap<AssetId<ShaderBuffer>, Arc<()>>);

#[must_use]
pub fn gpu_owned(world: &World, handle: &Handle<ShaderBuffer>) -> bool {
    world
        .get_resource::<Owners>()
        .is_some_and(|owners| owners.0.contains_key(&handle.id()))
}

pub(crate) fn claim(world: &mut World, handle: &Handle<ShaderBuffer>) -> Result<Arc<()>, String> {
    let buffer = world
        .get_resource::<Assets<ShaderBuffer>>()
        .and_then(|assets| assets.get(handle))
        .ok_or("Scene lighting destination absent")?;
    if buffer.data.as_ref().is_none_or(|data| data.len() != 176)
        || !buffer
            .buffer_description
            .usage
            .contains(bevy::render::render_resource::BufferUsages::COPY_DST)
    {
        return Err("Scene lighting destination ABI or usage differs".into());
    }
    let lease = Arc::new(());
    world.init_resource::<Owners>();
    world
        .resource_mut::<Owners>()
        .0
        .insert(handle.id(), lease.clone());
    Ok(lease)
}

pub(crate) fn release(world: &mut World, handle: &Handle<ShaderBuffer>, lease: &Arc<()>) {
    if let Some(mut owners) = world.get_resource_mut::<Owners>()
        && owners
            .0
            .get(&handle.id())
            .is_some_and(|current| Arc::ptr_eq(current, lease))
    {
        owners.0.remove(&handle.id());
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn only_current_owner_can_release_and_cpu_publish_preserves_gpu_seed() {
        let mut world = World::new();
        world.init_resource::<Assets<ShaderBuffer>>();
        let light =
            crate::tidewater_material::SceneLighting::new([0.0, 1.0, 0.0], [1.0; 3], [[0.1; 3]; 9])
                .unwrap();
        let handle = crate::tidewater_material::scene_buffer::HANDLE;
        crate::tidewater_material::scene_buffer::publish(&mut world, &light).unwrap();
        let before = world
            .resource::<Assets<ShaderBuffer>>()
            .get(&handle)
            .unwrap()
            .data
            .clone();
        let old = claim(&mut world, &handle).unwrap();
        let current = claim(&mut world, &handle).unwrap();
        release(&mut world, &handle, &old);
        assert!(gpu_owned(&world, &handle));
        assert!(crate::tidewater_material::scene_buffer::publish(&mut world, &light).is_err());
        assert_eq!(
            world
                .resource::<Assets<ShaderBuffer>>()
                .get(&handle)
                .unwrap()
                .data,
            before
        );
        release(&mut world, &handle, &current);
        assert!(!gpu_owned(&world, &handle));
        crate::tidewater_material::scene_buffer::publish(&mut world, &light).unwrap();
    }
}
