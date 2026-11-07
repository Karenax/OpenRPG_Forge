//! Shared indexed source-depth prototypes and transient per-owner poses.
use bevy::{prelude::*, render::Extract};
use std::sync::Arc;
pub mod gpu;
pub struct MeshBytes {
    pub vertices: Vec<u8>,
    pub indices: Vec<u8>,
}
#[derive(Clone, PartialEq)]
pub struct Pose {
    pub asset: u8,
    pub model: Mat4,
    pub data: [f32; 4],
    pub enabled: bool,
}
#[derive(Resource, Clone)]
pub struct Frame {
    pub target: Handle<Image>,
    pub shader: Arc<String>,
    pub meshes: Arc<Vec<MeshBytes>>,
    pub poses: Vec<Pose>,
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy extraction systems require owned Extract and Commands parameter wrappers."
)]
pub(crate) fn extract(mut commands: Commands, frame: Extract<Option<Res<Frame>>>) {
    if let Some(frame) = frame.as_ref() {
        commands.insert_resource((**frame).clone());
    } else {
        commands.remove_resource::<Frame>();
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::render::{ExtractSchedule, MainWorld};
    #[test]
    fn retired_scanned_scene_does_not_leave_a_render_world_frame() {
        let mut main = MainWorld::default();
        let meshes = Arc::new(Vec::new());
        main.insert_resource(Frame {
            target: Handle::default(),
            shader: Arc::new(String::new()),
            meshes: meshes.clone(),
            poses: Vec::new(),
        });
        let mut render = World::new();
        render.insert_resource(main);
        let mut schedule = Schedule::new(ExtractSchedule);
        schedule.add_systems(extract);
        schedule.run(&mut render);
        assert!(Arc::ptr_eq(&render.resource::<Frame>().meshes, &meshes));
        render
            .resource_mut::<MainWorld>()
            .remove_resource::<Frame>();
        schedule.run(&mut render);
        assert!(!render.contains_resource::<Frame>());
        schedule.run(&mut render);
        assert!(!render.contains_resource::<Frame>());
    }
}
