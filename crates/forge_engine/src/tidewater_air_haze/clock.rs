//! AirHaze.update owns its cadence; cloud and TAA counters are unrelated.
use bevy::{prelude::*, render::extract_component::ExtractComponent};

#[derive(Component, Clone, Copy, Default, ExtractComponent)]
pub(super) struct Clock(pub u16);

pub(super) fn update(
    mut commands: Commands,
    mut views: Query<(Entity, Option<&mut Clock>), With<super::TidewaterAirHaze>>,
) {
    for (entity, clock) in &mut views {
        if let Some(mut clock) = clock {
            clock.0 = (clock.0 + 1) % 1024;
        } else {
            commands.entity(entity).insert(Clock(1));
        }
    }
}
