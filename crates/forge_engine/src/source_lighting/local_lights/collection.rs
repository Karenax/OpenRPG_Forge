//! Scene collection transferable to the existing `RenderWorld`. The adapter owns
//! camera selection, source inventory, solar settings and scheduling.
use super::PreparedView;
use super::{Collector, Error, FramePacket, Packet, Settings, scene};
use bevy::{prelude::*, render::extract_resource::ExtractResource};

#[derive(Resource, Clone, Default, ExtractResource)]
pub struct Collection {
    collector: Collector,
    camera: Option<Entity>,
    scene_frame: Option<u32>,
}

#[derive(Resource)]
struct Producer;

/// The shared packet has one scene producer, independent of its view adapter.
pub fn claim_producer(world: &mut World) -> Result<(), String> {
    if world.contains_resource::<Producer>() {
        return Err("Source local-light collection already has a scene producer".into());
    }
    world.insert_resource(Producer);
    Ok(())
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
        self.scene_frame = world
            .get_resource::<bevy::diagnostic::FrameCount>()
            .map(|f| f.0);
        Ok(())
    }

    pub fn invalidate(&mut self) {
        self.camera = None;
        self.scene_frame = None;
    }

    #[must_use]
    pub const fn scene_frame(&self) -> Option<u32> {
        self.scene_frame
    }

    pub fn toggle_flashlight(&mut self, enabled: bool) {
        self.invalidate();
        self.collector.toggle_flashlight(enabled);
    }

    /// Source `RenderWorld` packets retain their Main camera Entity in `FrameTag`.
    /// Checking pose alone would accept another camera at the same location.
    pub fn for_view(&self, view: &impl PreparedView) -> Result<FramePacket, Error> {
        if self.camera != Some(view.tag().view) {
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
    use crate::scene_camera::FrameTag;
    use crate::source_lighting::local_lights::{Flashlight, Light};
    #[derive(Component)]
    struct View {
        tag: FrameTag,
        matrix: Mat4,
    }
    impl PreparedView for View {
        fn tag(&self) -> FrameTag {
            self.tag
        }
        fn view_from_world(&self) -> Mat4 {
            self.matrix
        }
    }
    use bevy::{math::DVec3, transform::TransformPlugin};

    #[test]
    #[expect(
        clippy::too_many_lines,
        reason = "Ordered source validation and GPU preparation remain a single auditable operation"
    )]
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
        let view = View {
            tag: FrameTag {
                view: camera,
                serial: 1,
            },
            matrix: Mat4::IDENTITY,
        };
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
        let zero = View {
            tag: FrameTag {
                serial: 0,
                ..view.tag
            },
            matrix: Mat4::IDENTITY,
        };
        assert!(matches!(collection.for_view(&zero), Err(Error::StaleFrame)));

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
            use crate::source_lighting::local_lights::receiver::Uniform;
            let packet = transferred.for_view(&view).unwrap();
            assert_eq!(Uniform::from_frame(&packet, view.tag).unwrap().count, 1);
            let mut stale = view.tag;
            stale.serial += 1;
            assert!(matches!(
                Uniform::from_frame(&packet, stale),
                Err(Error::StaleFrame)
            ));
        }
        let foreign = View {
            tag: FrameTag {
                view: other,
                serial: 1,
            },
            matrix: Mat4::IDENTITY,
        };
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
