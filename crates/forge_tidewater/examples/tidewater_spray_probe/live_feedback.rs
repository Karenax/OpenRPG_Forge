//! Connect the shared feedback observer after the acquired scene is ready.
use super::Targets;
use bevy::{prelude::*, render::gpu_readback::Readback};
use forge_engine::source_spray_feedback::{self, LiveFeedback};
use std::sync::atomic::Ordering;

pub(super) fn install(app: &mut App) -> Result<(), &'static str> {
    let targets = app.world().resource::<Targets>();
    let budget = targets.budget.clone().expect("validated native budget");
    let bootstrap_steps = targets.bootstrap_steps;
    LiveFeedback::install(app, budget, bootstrap_steps)?;
    app.add_systems(Update, request);
    Ok(())
}

fn request(
    mut commands: Commands,
    targets: Res<Targets>,
    feedback: Res<LiveFeedback>,
    ocean: Res<super::super::ocean::Targets>,
    mut started: Local<bool>,
    queries: Option<Res<super::water_queries::Targets>>,
) {
    if *started
        || !targets.readback_complete.load(Ordering::Acquire)
        || queries.is_some_and(|q| !q.bootstrap_ready.load(Ordering::Acquire))
    {
        return;
    }
    *started = true;
    commands
        .spawn(Readback::buffer(feedback.packet().clone()))
        .observe(source_spray_feedback::receive);
    // No live step can overwrite frame 120 until every initial readback is saved.
    ocean.permitted_step.store(121, Ordering::Release);
}
