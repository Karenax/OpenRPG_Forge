use super::{Bridge, RendererObservation};
use bevy::{
    prelude::*,
    render::{
        MainWorld,
        render_resource::{CachedPipelineState, PipelineCache},
    },
    shader::ShaderCacheError,
};

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy borrows extraction resources"
)]
pub(super) fn observe(main: Res<MainWorld>, cache: Res<PipelineCache>, bridge: Res<Bridge>) {
    let mut observation = RendererObservation {
        observed: true,
        frame: main
            .get_resource::<bevy::diagnostic::FrameCount>()
            .map_or(0, |frame| frame.0),
        ..default()
    };
    for pipeline in cache.pipelines() {
        observation.pipelines += 1;
        match &pipeline.state {
            CachedPipelineState::Ok(_) => {}
            CachedPipelineState::Queued
            | CachedPipelineState::Creating(_)
            | CachedPipelineState::Err(
                ShaderCacheError::ShaderNotLoaded(_)
                | ShaderCacheError::ShaderImportNotYetAvailable,
            ) => observation.pending += 1,
            CachedPipelineState::Err(_) => observation.failed += 1,
        }
    }
    if let Ok(mut shared) = bridge.0.lock() {
        shared.renderer = observation;
    }
}
