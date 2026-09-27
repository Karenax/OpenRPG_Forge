//! Wall-clock phase boundaries for the native walk measurement only.
//! The outside interval includes Last, rendering, presentation and runner waits;
//! it is not a GPU timer or a measurement of one specific subsystem.
use bevy::{app::MainScheduleOrder, ecs::schedule::ScheduleLabel, prelude::*};
use std::time::Instant;

pub const NAMES: [&str; 5] = [
    "outside_first_to_post_update",
    "first_through_pre_update",
    "fixed_main_loop",
    "update",
    "spawn_scene_through_post_update",
];

#[derive(ScheduleLabel, Clone, Debug, PartialEq, Eq, Hash)]
struct Boundary(u8);

#[derive(Resource, Default)]
pub struct CpuPhases {
    previous: Option<Instant>,
    pending: [f64; 5],
    pub latest: Option<[f64; 5]>,
}

impl CpuPhases {
    fn record(&mut self, index: usize, now: Instant) {
        if let Some(previous) = self.previous {
            self.pending[index] = now.duration_since(previous).as_secs_f64() * 1000.0;
            if index == 4 {
                self.latest = Some(self.pending);
            }
        }
        self.previous = Some(now);
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameter")]
fn mark<const INDEX: usize>(mut phases: ResMut<CpuPhases>) {
    phases.record(INDEX, Instant::now());
}

pub fn install(app: &mut App) {
    app.init_resource::<CpuPhases>()
        .add_systems(Boundary(0), mark::<0>)
        .add_systems(Boundary(1), mark::<1>)
        .add_systems(Boundary(2), mark::<2>)
        .add_systems(Boundary(3), mark::<3>)
        .add_systems(Boundary(4), mark::<4>);
    let mut order = app.world_mut().resource_mut::<MainScheduleOrder>();
    order.insert_before(First, Boundary(0));
    order.insert_after(PreUpdate, Boundary(1));
    order.insert_after(RunFixedMainLoop, Boundary(2));
    order.insert_after(Update, Boundary(3));
    order.insert_after(PostUpdate, Boundary(4));
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn boundaries_partition_wall_time_without_using_virtual_time() {
        let mut phases = CpuPhases::default();
        let start = Instant::now();
        phases.record(0, start);
        for index in 1..5 {
            phases.record(
                index,
                start + std::time::Duration::from_millis(index as u64),
            );
        }
        phases.record(0, start + std::time::Duration::from_millis(14));
        for index in 1..5 {
            phases.record(
                index,
                start + std::time::Duration::from_millis(14 + index as u64),
            );
        }
        assert_eq!(phases.latest, Some([10.0, 1.0, 1.0, 1.0, 1.0]));
    }

    #[test]
    fn installed_boundaries_execute_in_the_real_main_schedule() {
        let mut app = App::new();
        install(&mut app);
        app.update();
        app.update();
        assert!(app.world().resource::<CpuPhases>().latest.is_some());
    }
}
