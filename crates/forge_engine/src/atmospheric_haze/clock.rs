//! Per-view scattering cadence, independent of map, cloud and TAA counters.
use bevy::{prelude::*, render::extract_component::ExtractComponent};

#[derive(Component, Clone, Copy, Default, ExtractComponent)]
pub struct Clock(pub u16);

/// Advance every configured view once per main-world update, including disabled
/// haze so toggling the effect does not reset its temporal sampling sequence.
pub fn update(
    mut commands: Commands,
    mut views: Query<(Entity, Option<&mut Clock>), With<super::AirHazeSettings>>,
) {
    for (entity, clock) in &mut views {
        if let Some(mut clock) = clock {
            clock.0 = clock.0.wrapping_add(1) % 1024;
        } else {
            commands.entity(entity).insert(Clock(1));
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn independent_views_advance_and_wrap_without_a_map_component() {
        let mut app = App::new();
        app.add_systems(PostUpdate, update);
        let first = app
            .world_mut()
            .spawn(super::super::AirHazeSettings::default())
            .id();
        let disabled = super::super::AirHazeSettings {
            enabled: false,
            ..super::super::AirHazeSettings::default()
        };
        let second = app.world_mut().spawn((disabled, Clock(1023))).id();
        let unrelated = app.world_mut().spawn(Clock(17)).id();
        app.update();
        assert_eq!(app.world().get::<Clock>(first).unwrap().0, 1);
        assert_eq!(app.world().get::<Clock>(second).unwrap().0, 0);
        assert_eq!(app.world().get::<Clock>(unrelated).unwrap().0, 17);
        app.world_mut()
            .entity_mut(first)
            .remove::<super::super::AirHazeSettings>();
        app.update();
        assert_eq!(app.world().get::<Clock>(first).unwrap().0, 1);
        assert_eq!(app.world().get::<Clock>(second).unwrap().0, 1);
    }
}
