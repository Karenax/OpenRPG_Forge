//! Retire ephemeral fish input from the render world when its producer disappears.
use crate::tidewater_fish_props::shadow::Frame;
use bevy::{prelude::*, render::Extract};

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
    use std::sync::Arc;

    fn frame(time: f32) -> Frame {
        Frame {
            target: Handle::default(),
            shader: Arc::new("verified shader".into()),
            meshes: Arc::new(Vec::new()),
            poses: Vec::new(),
            time,
        }
    }

    #[test]
    fn extracted_frame_updates_retires_and_can_be_reintroduced() {
        let mut main = MainWorld::default();
        let initial = frame(2.0);
        let geometry = initial.meshes.clone();
        main.insert_resource(initial);
        let mut render = World::new();
        render.insert_resource(main);
        let mut schedule = Schedule::new(ExtractSchedule);
        schedule.add_systems(extract);
        schedule.run(&mut render);
        assert_eq!(render.resource::<Frame>().time, 2.0);
        assert!(Arc::ptr_eq(&geometry, &render.resource::<Frame>().meshes));

        render
            .resource_mut::<MainWorld>()
            .resource_mut::<Frame>()
            .time = 2.25;
        schedule.run(&mut render);
        assert_eq!(render.resource::<Frame>().time, 2.25);
        assert!(Arc::ptr_eq(&geometry, &render.resource::<Frame>().meshes));

        render
            .resource_mut::<MainWorld>()
            .remove_resource::<Frame>();
        schedule.run(&mut render);
        assert!(!render.contains_resource::<Frame>());
        schedule.run(&mut render);
        assert!(!render.contains_resource::<Frame>());

        let replacement = frame(3.0);
        let replacement_geometry = replacement.meshes.clone();
        render
            .resource_mut::<MainWorld>()
            .insert_resource(replacement);
        schedule.run(&mut render);
        assert_eq!(render.resource::<Frame>().time, 3.0);
        assert!(Arc::ptr_eq(
            &replacement_geometry,
            &render.resource::<Frame>().meshes
        ));
        assert!(!Arc::ptr_eq(&geometry, &render.resource::<Frame>().meshes));
    }
}
