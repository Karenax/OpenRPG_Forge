//! Bounded diagnostic stepping after asset preparation, never a benchmark clock.
use bevy::prelude::*;
use forge_engine::{FixedProofClock, GovernedAssetKitReadiness, GovernedAssetKitRuntimeStats};

pub fn install(app: &mut App, steps: u32) {
    app.insert_resource(TargetSteps(steps))
        .add_systems(Last, advance);
}

#[derive(Resource)]
struct TargetSteps(u32);

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn advance(
    target: Res<TargetSteps>,
    mut clock: ResMut<FixedProofClock>,
    kit: Option<Res<GovernedAssetKitReadiness>>,
    stats: Option<Res<GovernedAssetKitRuntimeStats>>,
    mut reported: Local<bool>,
) {
    if *reported {
        return;
    }
    // Loading a GLB is not enough: all collider shapes must exist before the
    // first physics tick. Subsequent fixed steps register them normally.
    if let Some(kit) = kit.as_deref()
        && !stats
            .as_deref()
            .is_some_and(|stats| kit.prepared_for_static_view(stats))
    {
        return;
    }
    if u32::from(clock.steps) < target.0 {
        clock.request_step();
        return;
    }
    if kit.as_deref().is_some_and(|kit| !kit.ready) {
        return;
    }
    info!(
        event_kind = "fixed_clock_steps_completed",
        steps = clock.steps,
        registered_colliders = kit.as_deref().map_or(0, |kit| kit.registered_colliders),
        "bounded clock reached requested phase after normal physics initialization"
    );
    *reported = true;
}
