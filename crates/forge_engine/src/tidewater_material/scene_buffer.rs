//! One scene lighting buffer shared by the terrain and plant material families.
use super::{SceneLighting, scene_uniform::Seed};
use bevy::{prelude::*, render::storage::ShaderBuffer};
pub mod ownership;
pub mod receiver;

pub const HANDLE: Handle<ShaderBuffer> =
    bevy::asset::uuid_handle!("7e8f75d0-a0e5-4197-92ef-f86a17dbbf04");

/// Seed the shared buffer once from the acquired shader literals. Rebuilding an
/// edited mesh must not reset light that the live scene already published.
pub fn install(world: &mut World, seed: &Seed) -> Result<(), String> {
    let lighting = SceneLighting::new(seed.direction, seed.color, seed.sh)?;
    let assets = world
        .get_resource::<Assets<ShaderBuffer>>()
        .ok_or("scene lighting buffer assets absent")?;
    if assets.get(&HANDLE).is_some() {
        return Ok(());
    }
    publish(world, &lighting)
}

/// Publish an explicitly coherent direct-light/SH snapshot. Asset identity stays
/// stable; Bevy uploads the new storage data through its existing asset pipeline.
pub fn publish(world: &mut World, lighting: &SceneLighting) -> Result<(), String> {
    if ownership::gpu_owned(world, &HANDLE) {
        return Err("Scene lighting is controlled by the live GPU generation".into());
    }
    let checked = SceneLighting::new(
        lighting.sun_direction.truncate().to_array(),
        lighting.sun_color.truncate().to_array(),
        lighting.environment_sh.map(|v| v.truncate().to_array()),
    )?;
    let data: Vec<_> = std::iter::once(checked.sun_direction)
        .chain(std::iter::once(checked.sun_color))
        .chain(checked.environment_sh)
        .collect();
    let buffer = ShaderBuffer::from(data);
    if world
        .get_resource::<Assets<ShaderBuffer>>()
        .and_then(|assets| assets.get(&HANDLE))
        .is_some_and(|current| current.data == buffer.data)
    {
        return Ok(());
    }
    world
        .get_resource_mut::<Assets<ShaderBuffer>>()
        .ok_or("scene lighting buffer assets absent")?
        .insert(HANDLE.id(), buffer)
        .map_err(|e| e.to_string())?;
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn publishes_aligned_values_without_resetting_on_mesh_rebuild() {
        let mut world = World::new();
        world.init_resource::<Assets<ShaderBuffer>>();
        let seed = Seed {
            direction: [0.0, 1.0, 0.0],
            color: [2.0, 3.0, 4.0],
            sh: [[0.1, 0.2, 0.3]; 9],
        };
        install(&mut world, &seed).unwrap();
        let mut light = SceneLighting::new(seed.direction, seed.color, seed.sh).unwrap();
        light.sun_color = Vec4::new(5.0, 6.0, 7.0, 0.0);
        publish(&mut world, &light).unwrap();
        install(&mut world, &seed).unwrap();
        let data = world
            .resource::<Assets<ShaderBuffer>>()
            .get(&HANDLE)
            .unwrap()
            .data
            .as_ref()
            .unwrap()
            .clone();
        assert_eq!(data.len(), 11 * 16);
        assert_eq!(f32::from_le_bytes(data[16..20].try_into().unwrap()), 5.0);
        assert_eq!(f32::from_le_bytes(data[32..36].try_into().unwrap()), 0.1);
        light.sun_color.x = f32::NAN;
        assert!(publish(&mut world, &light).is_err());
        assert_eq!(
            &data,
            world
                .resource::<Assets<ShaderBuffer>>()
                .get(&HANDLE)
                .unwrap()
                .data
                .as_ref()
                .unwrap()
        );
    }
}
