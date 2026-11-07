//! A caller-owned fish clock updates one resident buffer instead of every material.
use bevy::{prelude::*, render::storage::ShaderBuffer};

pub const NEUTRAL: Handle<ShaderBuffer> =
    bevy::asset::uuid_handle!("f3804f43-3d78-41da-a9a6-18d169263bb5");

/// Install the neutral clock before publishing a material that uses it.
pub fn install_neutral(world: &mut World) -> Result<(), String> {
    let mut buffers = world
        .get_resource_mut::<Assets<ShaderBuffer>>()
        .ok_or("Fish clock shader buffers unavailable")?;
    if buffers.get(NEUTRAL.id()).is_none() {
        buffers
            .insert(NEUTRAL.id(), ShaderBuffer::from(UVec4::ZERO))
            .map_err(|error| error.to_string())?;
    }
    Ok(())
}

/// Create a clock scoped to the caller's materials. Other callers keep their own clock.
pub fn create(world: &mut World) -> Result<Handle<ShaderBuffer>, String> {
    let mut buffers = world
        .get_resource_mut::<Assets<ShaderBuffer>>()
        .ok_or("Fish clock shader buffers unavailable")?;
    Ok(buffers.add(ShaderBuffer::from(UVec4::new(0, 0, 1, 0))))
}

/// Publish exact float32 time bits and the full uint32 frame index before extraction.
pub fn update(
    world: &mut World,
    handle: &Handle<ShaderBuffer>,
    time: f32,
    frame: u32,
) -> Result<(), String> {
    if !time.is_finite() || time < 0.0 || handle.id() == NEUTRAL.id() {
        return Err("Invalid live fish clock".into());
    }
    let mut buffers = world
        .get_resource_mut::<Assets<ShaderBuffer>>()
        .ok_or("Fish clock shader buffers unavailable")?;
    let expected = [time.to_bits(), frame, 1, 0];
    if buffers
        .get(handle)
        .ok_or("Live fish clock removed")?
        .data
        .as_deref()
        .is_some_and(|bytes| {
            bytes.len() == 16
                && bytes
                    .as_chunks::<4>()
                    .0
                    .iter()
                    .zip(expected)
                    .all(|(chunk, word)| *chunk == word.to_le_bytes())
        })
    {
        return Ok(());
    }
    let mut buffer = buffers.get_mut(handle).ok_or("Live fish clock removed")?;
    buffer.set_data(UVec4::new(time.to_bits(), frame, 1, 0));
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::asset::{AssetApp, AssetPlugin};

    #[test]
    fn unchanged_clock_emits_no_buffer_upload_event() {
        let mut app = App::new();
        app.add_plugins((MinimalPlugins, AssetPlugin::default()))
            .init_asset::<ShaderBuffer>();
        let handle = create(app.world_mut()).unwrap();
        app.update();
        app.world_mut()
            .resource_mut::<Messages<AssetEvent<ShaderBuffer>>>()
            .clear();
        update(app.world_mut(), &handle, 0.0, 0).unwrap();
        app.update();
        assert!(
            app.world()
                .resource::<Messages<AssetEvent<ShaderBuffer>>>()
                .is_empty()
        );
        update(app.world_mut(), &handle, 1.25, 7).unwrap();
        app.update();
        assert!(
            !app.world()
                .resource::<Messages<AssetEvent<ShaderBuffer>>>()
                .is_empty()
        );
        app.world_mut()
            .resource_mut::<Messages<AssetEvent<ShaderBuffer>>>()
            .clear();
        update(app.world_mut(), &handle, 1.25, 7).unwrap();
        app.update();
        assert!(
            app.world()
                .resource::<Messages<AssetEvent<ShaderBuffer>>>()
                .is_empty()
        );
    }

    #[test]
    fn clocks_are_independent_and_preserve_long_session_frame_bits() {
        let mut world = World::new();
        world.init_resource::<Assets<ShaderBuffer>>();
        install_neutral(&mut world).unwrap();
        let first = create(&mut world).unwrap();
        let second = create(&mut world).unwrap();
        let other = world
            .resource::<Assets<ShaderBuffer>>()
            .get(&second)
            .unwrap()
            .data
            .clone();
        update(&mut world, &first, 1.625, u32::MAX).unwrap();
        let buffers = world.resource::<Assets<ShaderBuffer>>();
        let bytes = buffers.get(&first).unwrap().data.as_ref().unwrap();
        let expected: Vec<_> = [1.625_f32.to_bits(), u32::MAX, 1, 0]
            .into_iter()
            .flat_map(u32::to_le_bytes)
            .collect();
        assert_eq!(bytes, &expected);
        assert_eq!(buffers.get(&second).unwrap().data, other);
        assert_eq!(
            buffers.get(NEUTRAL.id()).unwrap().data.as_deref(),
            Some([0; 16].as_slice())
        );
    }
    #[test]
    fn invalid_updates_preserve_the_clock_and_neutral_mode() {
        let mut world = World::new();
        world.init_resource::<Assets<ShaderBuffer>>();
        install_neutral(&mut world).unwrap();
        let handle = create(&mut world).unwrap();
        update(&mut world, &handle, 3.5, 10).unwrap();
        let before = world
            .resource::<Assets<ShaderBuffer>>()
            .get(&handle)
            .unwrap()
            .data
            .clone();
        for time in [f32::NAN, f32::INFINITY, -1.0] {
            assert!(update(&mut world, &handle, time, 11).is_err());
        }
        assert!(update(&mut world, &NEUTRAL, 4.0, 12).is_err());
        assert_eq!(
            world
                .resource::<Assets<ShaderBuffer>>()
                .get(&handle)
                .unwrap()
                .data,
            before
        );
    }
}
