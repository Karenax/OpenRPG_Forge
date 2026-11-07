//! Shared current/previous scene pose and clouds; one GPU publication per presented frame.
use bevy::{
    prelude::*,
    render::{render_resource::ShaderType, storage::ShaderBuffer},
};
pub(crate) mod motion;
/// Supported source-profile deformation range, shared by bounds and frame inputs.
pub const MAX_WIND_SPEED: f32 = forge_project::zone_authoring::wind::MAX_SCENE_WIND_SPEED_MPS;

/// One host-owned frame, shared by all plant materials. Previous values deform
/// the previous vertex for real motion vectors, rather than assuming rigid plants.
#[derive(Clone, Copy, Debug, ShaderType)]
pub struct Frame {
    camera: Vec4,
    wind: Vec4,
    gust: Vec4,
    previous_camera: Vec4,
    previous_wind: Vec4,
    previous_gust: Vec4,
    clouds: [Vec4; 5],
}

impl Frame {
    /// The caster consumes the exact published visible pose, without another clock.
    #[must_use]
    pub fn shadow_motion(&self) -> crate::source_shadows::casters::graph::animation::Motion {
        crate::source_shadows::casters::graph::animation::Motion {
            main_camera: self.camera.truncate().extend(0.0),
            wind_time: self.wind,
            gust_frame: self.gust,
        }
    }

    pub fn new(camera: Vec3, wind: Vec4, gust: Vec4) -> Result<Self, String> {
        Self::validate(camera, wind, gust)?;
        Ok(Self {
            camera: camera.extend(1.0),
            wind,
            gust,
            previous_camera: camera.extend(1.0),
            previous_wind: wind,
            previous_gust: gust,
            clouds: [Vec4::ZERO; 5],
        })
    }

    pub(crate) fn with_clouds(mut self, clouds: [Vec4; 5]) -> Result<Self, String> {
        if clouds.iter().any(|value| !value.is_finite()) {
            return Err("Non-finite source cloud receiver frame".into());
        }
        self.clouds = clouds;
        Ok(self)
    }

    pub(crate) fn validate(camera: Vec3, wind: Vec4, gust: Vec4) -> Result<(), String> {
        if !camera.is_finite()
            || !wind.is_finite()
            || !gust.is_finite()
            || wind.z < 0.0
            || wind.z > MAX_WIND_SPEED
            || (wind.xy().length_squared() - 1.0).abs() > 0.001
            || gust.z < 0.0
            || gust.z > 16_777_216.0
            || gust.z.fract() != 0.0
        {
            return Err("Invalid live-world plant camera, unit wind or frame index".into());
        }
        Ok(())
    }

    /// Publish all geometry inputs together. A failed validation preserves both
    /// the previous state and GPU payload. Call once per presented host frame.
    pub fn update(
        &mut self,
        world: &mut World,
        handle: &Handle<ShaderBuffer>,
        camera: Vec3,
        wind: Vec4,
        gust: Vec4,
    ) -> Result<(), String> {
        Self::validate(camera, wind, gust)?;
        let clouds = world
            .get_resource::<crate::source_environment::scene::ResidentEnvironment>()
            .map_or(self.clouds, |environment| environment.cloud_view);
        if clouds.iter().any(|value| !value.is_finite()) {
            return Err("Non-finite source cloud receiver frame".into());
        }
        let mut buffers = world
            .get_resource_mut::<Assets<ShaderBuffer>>()
            .ok_or("Plant frame buffers unavailable")?;
        let mut buffer = buffers
            .get_mut(handle)
            .ok_or("Plant frame buffer disappeared")?;
        let next = Self {
            camera: camera.extend(1.0),
            wind,
            gust,
            previous_camera: self.camera,
            previous_wind: self.wind,
            previous_gust: self.gust,
            clouds,
        };
        buffer.set_data(next);
        *self = next;
        Ok(())
    }

    pub fn stage(&self, world: &mut World) -> Result<Handle<ShaderBuffer>, String> {
        let mut buffers = world
            .get_resource_mut::<Assets<ShaderBuffer>>()
            .ok_or("Plant frame buffers unavailable")?;
        Ok(buffers.add(ShaderBuffer::from(*self)))
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn frame_update_keeps_actual_previous_pose_and_rejects_bad_inputs_atomically() {
        let mut world = World::new();
        world.init_resource::<Assets<ShaderBuffer>>();
        let initial = Vec4::new(1.0, 0.0, 4.0, 8.0);
        let clouds =
            std::array::from_fn(|index| Vec4::splat(f32::from(u8::try_from(index).unwrap())));
        let mut frame = Frame::new(Vec3::Z, initial, Vec4::ZERO)
            .unwrap()
            .with_clouds(clouds)
            .unwrap();
        assert!(frame.with_clouds([Vec4::NAN; 5]).is_err());
        let handle = frame.stage(&mut world).unwrap();
        let next = Vec4::new(0.0, 1.0, 5.0, 9.0);
        frame
            .update(&mut world, &handle, Vec3::X, next, Vec4::ONE)
            .unwrap();
        assert_eq!(frame.previous_camera, Vec3::Z.extend(1.0));
        assert_eq!(frame.previous_wind, initial);
        assert_eq!(frame.camera, Vec3::X.extend(1.0));
        let before = world
            .resource::<Assets<ShaderBuffer>>()
            .get(&handle)
            .unwrap()
            .data
            .clone();
        for (camera, wind, gust) in [
            (Vec3::NAN, next, Vec4::ONE),
            (Vec3::X, Vec4::ZERO, Vec4::ONE),
            (
                Vec3::X,
                Vec4::new(1.0, 0.0, MAX_WIND_SPEED.next_up(), 0.0),
                Vec4::ONE,
            ),
            (Vec3::X, Vec4::new(1.0, 0.0, -1.0, 0.0), Vec4::ONE),
            (Vec3::X, next, Vec4::new(0.0, 0.0, 0.5, 0.0)),
        ] {
            assert!(
                frame
                    .update(&mut world, &handle, camera, wind, gust)
                    .is_err()
            );
            assert_eq!(
                world
                    .resource::<Assets<ShaderBuffer>>()
                    .get(&handle)
                    .unwrap()
                    .data,
                before
            );
            assert_eq!(frame.previous_wind, initial);
        }
        assert_eq!(before.as_ref().unwrap().len(), 176);
        assert_eq!(
            &before.unwrap()[96..],
            ShaderBuffer::from(clouds).data.unwrap()
        );
        assert_eq!(frame.shadow_motion().wind_time, next);
        assert_eq!(frame.shadow_motion().gust_frame, Vec4::ONE);
    }

    #[test]
    fn missing_frame_buffer_does_not_advance_previous_geometry() {
        let mut world = World::new();
        world.init_resource::<Assets<ShaderBuffer>>();
        let mut frame = Frame::new(Vec3::Z, Vec4::X, Vec4::ZERO).unwrap();
        let handle = frame.stage(&mut world).unwrap();
        world
            .resource_mut::<Assets<ShaderBuffer>>()
            .remove(handle.id());
        assert!(
            frame
                .update(&mut world, &handle, Vec3::X, Vec4::X, Vec4::ONE)
                .is_err()
        );
        assert_eq!(frame.camera, Vec3::Z.extend(1.0));
    }
}
