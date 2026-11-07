//! Exercise the common transient masked-caster extraction without a GPU.
fn main() {}
#[cfg(test)]
mod tests {
    use bevy::{
        prelude::*,
        render::{ExtractSchedule, MainWorld},
    };
    use forge_engine::source_shadows::casters::masked::{Frame, extraction};
    #[test]
    fn source_producer_retirement_removes_common_render_frame() {
        let mut main = MainWorld::default();
        main.insert_resource(Frame {
            target: Handle::default(),
            shader: std::sync::Arc::new(String::new()),
            meshes: std::sync::Arc::new(Vec::new()),
            poses: Vec::new(),
            time: 0.0,
        });
        let mut render = World::new();
        render.insert_resource(main);
        let mut schedule = Schedule::new(ExtractSchedule);
        schedule.add_systems(extraction::extract);
        schedule.run(&mut render);
        assert!(render.contains_resource::<Frame>());
        render
            .resource_mut::<MainWorld>()
            .remove_resource::<Frame>();
        schedule.run(&mut render);
        assert!(!render.contains_resource::<Frame>());
    }
}
