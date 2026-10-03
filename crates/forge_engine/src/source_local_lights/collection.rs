//! Scene collection transferable to the existing `RenderWorld`. The adapter owns
//! camera selection, source inventory, solar settings and scheduling.
use super::{Collector, Error, FramePacket, Packet, Settings, scene};
use crate::tidewater_temporal::frame::ViewPacket;
use bevy::{prelude::*, render::extract_resource::ExtractResource};

#[derive(Resource, Clone, Default, ExtractResource)]
pub struct Collection {
    collector: Collector,
    camera: Option<Entity>,
}

impl Collection {
    /// Run after transform propagation. Every refusal invalidates publication,
    /// while retaining numeric history for the next valid collection.
    pub fn collect(
        &mut self,
        world: &mut World,
        camera: Entity,
        settings: Settings,
        registration_orders: &[u64],
    ) -> Result<(), Error> {
        self.invalidate();
        let (pose, lights) = scene::harvest(world, camera, registration_orders)?;
        self.collector.update(pose, settings, &lights)?;
        self.camera = Some(camera);
        Ok(())
    }

    pub fn invalidate(&mut self) {
        self.camera = None;
    }

    pub fn toggle_flashlight(&mut self, enabled: bool) {
        self.invalidate();
        self.collector.toggle_flashlight(enabled);
    }

    /// Source `RenderWorld` packets retain their Main camera Entity in `FrameTag`.
    /// Checking pose alone would accept another camera at the same location.
    pub fn for_view(&self, view: &ViewPacket) -> Result<FramePacket, Error> {
        if self.camera != Some(view.tag.view) {
            return Err(Error::StaleFrame);
        }
        self.collector.for_view(view)
    }

    /// Bind to Bevy's extracted main camera in every graphics mode. Collection
    /// revision tracks fresh scene preparation independently of temporal resolve.
    pub fn for_camera(
        &self,
        camera: Entity,
        world_from_view: Mat4,
    ) -> Result<(u64, &Packet), Error> {
        if self.camera != Some(camera) {
            return Err(Error::StaleFrame);
        }
        self.collector.for_camera_pose(world_from_view)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::source_local_lights::{Flashlight, Light};
    use crate::tidewater_temporal::frame::Frames;
    use crate::tidewater_temporal_frame::Input;
    use bevy::{math::DVec3, transform::TransformPlugin};

    #[test]
    fn local_lights_collection_binds_camera_identity_and_invalidates_failed_inventory() {
        let mut app = App::new();
        app.add_plugins((MinimalPlugins, TransformPlugin));
        let camera = app
            .world_mut()
            .spawn((Camera3d::default(), Transform::default()))
            .id();
        let other = app
            .world_mut()
            .spawn((Camera3d::default(), Transform::default()))
            .id();
        app.world_mut().spawn((
            scene::SourceLight {
                registration_order: 1,
                parameters: Light {
                    position: DVec3::ZERO,
                    color: DVec3::ONE,
                    intensity: 1.5,
                    range: 11.0,
                    direction: None,
                    cos_inner: None,
                    cos_outer: None,
                    enabled: true,
                    scale: 1.0,
                    flicker: 0.08,
                    phase: 1.0,
                },
            },
            Transform::from_xyz(1.0, 1.0, -2.0),
        ));
        app.update();
        let input = || Input {
            world_from_view: Mat4::IDENTITY,
            clip_from_view: Mat4::perspective_infinite_reverse_rh(1.0, 1.0, 0.1),
            input_size: UVec2::splat(256),
            output_size: UVec2::splat(256),
            near: 0.1,
            far: 10_000.0,
        };
        let mut frames = Frames::default();
        frames.activate(Some(camera));
        let view = frames.prepare_aux(camera, input()).unwrap();
        let settings = Settings {
            delta_seconds: 0.016,
            night: 1.0,
            enabled: true,
            strength: 1.0,
            flashlight: Flashlight::default(),
        };
        let mut collection = Collection::default();
        collection
            .collect(app.world_mut(), camera, settings, &[1])
            .unwrap();
        assert_eq!(collection.for_view(&view).unwrap().packet().count, 1);
        let transferred = collection.clone();
        let (revision, packet) = transferred.for_camera(camera, Mat4::IDENTITY).unwrap();
        assert!(revision > 0);
        assert_eq!(packet.count, 1);
        assert!(transferred.for_camera(other, Mat4::IDENTITY).is_err());
        assert!(
            transferred
                .for_camera(camera, Mat4::from_translation(Vec3::X))
                .is_err()
        );
        assert!(
            transferred
                .for_camera(camera, Mat4::from_cols_array(&[f32::NAN; 16]))
                .is_err()
        );
        assert_eq!(transferred.for_view(&view).unwrap().tag(), view.tag);
        #[cfg(feature = "physics-rapier")]
        {
            use crate::tidewater_material::local_lights::Uniform;
            let packet = transferred.for_view(&view).unwrap();
            assert_eq!(Uniform::from_frame(&packet, view.tag).unwrap().count, 1);
            let mut stale = view.tag;
            stale.serial += 1;
            assert!(matches!(
                Uniform::from_frame(&packet, stale),
                Err(Error::StaleFrame)
            ));
        }
        frames.activate(Some(other));
        let foreign = frames.prepare_aux(other, input()).unwrap();
        assert!(matches!(
            collection.for_view(&foreign),
            Err(Error::StaleFrame)
        ));
        assert!(
            collection
                .collect(app.world_mut(), camera, settings, &[1, 2])
                .is_err()
        );
        assert!(matches!(collection.for_view(&view), Err(Error::StaleFrame)));
        assert!(collection.for_camera(camera, Mat4::IDENTITY).is_err());
        collection
            .collect(app.world_mut(), camera, settings, &[1])
            .unwrap();
        collection.toggle_flashlight(true);
        assert!(collection.for_view(&view).is_err());
        assert!(collection.for_camera(camera, Mat4::IDENTITY).is_err());
        collection
            .collect(app.world_mut(), camera, settings, &[1])
            .unwrap();
        assert_eq!(collection.for_view(&view).unwrap().packet().count, 2);
        assert!(collection.for_camera(camera, Mat4::IDENTITY).unwrap().0 > revision);
    }
}
