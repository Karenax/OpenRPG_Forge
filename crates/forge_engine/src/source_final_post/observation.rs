//! Actual final-pass encodings, separate from renderer fence completion.
use super::Controls;
use bevy::{
    diagnostic::FrameCount,
    prelude::*,
    render::{ExtractSchedule, MainWorld, RenderApp},
};
use std::{
    collections::{HashMap, HashSet},
    sync::{Arc, Mutex},
};

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct Submission {
    pub first_frame: u32,
    pub last_frame: u32,
    pub submitted: u32,
    pub size: UVec2,
    pub control_bits: [u32; 10],
    pub night_bits: u32,
}

impl Submission {
    /// Encoding is fresh and belongs to these exact controls and target size.
    /// A separate renderer fence must cover `first_frame` before GPU readiness.
    #[must_use]
    pub fn matches(&self, controls: &Controls, size: UVec2, frame: u32) -> bool {
        self.submitted > 0
            && self.size == size
            && self.control_bits == controls.settings_bits()
            && self.first_frame <= self.last_frame
            && self.last_frame <= frame
            && self.last_frame >= frame.saturating_sub(1)
    }
}

#[derive(Resource, Default, Clone)]
pub(crate) struct Observations(Arc<Mutex<HashMap<Entity, Submission>>>);
#[derive(Resource, Default)]
pub(crate) struct Frame(pub u32);

impl Observations {
    pub(crate) fn snapshot(&self, view: Entity) -> Option<Submission> {
        self.0.lock().ok()?.get(&view).cloned()
    }
    pub(super) fn publish(
        &self,
        view: Entity,
        frame: u32,
        size: UVec2,
        controls: &Controls,
        submitted: u32,
    ) {
        let Ok(mut entries) = self.0.lock() else {
            return;
        };
        if entries.get(&view).is_some_and(|old| old.last_frame > frame) {
            return;
        }
        let bits = controls.settings_bits();
        let first_frame = entries
            .get(&view)
            .filter(|old| old.control_bits == bits && old.size == size && old.last_frame <= frame)
            .map_or(frame, |old| old.first_frame);
        entries.insert(
            view,
            Submission {
                first_frame,
                last_frame: frame,
                submitted,
                size,
                control_bits: bits,
                night_bits: controls.timing.y.to_bits(),
            },
        );
    }
}

pub(super) fn install(app: &mut App) {
    let observations = Observations::default();
    app.insert_resource(observations.clone());
    if let Some(render) = app.get_sub_app_mut(RenderApp) {
        render
            .insert_resource(observations)
            .init_resource::<Frame>()
            .add_systems(ExtractSchedule, extract);
    }
}

fn extract(mut main: ResMut<MainWorld>, mut frame: ResMut<Frame>, observations: Res<Observations>) {
    frame.0 = main.get_resource::<FrameCount>().map_or(0, |f| f.0);
    let active: HashSet<_> = main
        .query_filtered::<Entity, With<Controls>>()
        .iter(&main)
        .collect();
    if let Ok(mut entries) = observations.0.lock() {
        entries.retain(|view, _| active.contains(view));
    }
}

/// Last actual encoding for this main-world view; not a GPU-completion receipt.
#[must_use]
pub fn submitted(world: &World, view: Entity) -> Option<Submission> {
    world.get::<Controls>(view)?;
    world.get_resource::<Observations>()?.snapshot(view)
}
