//! Shared change/removal guard for immutable renderer projections.
//! GPU fences remain independent and must be acquired on every readiness call.
use bevy::{
    ecs::{
        change_detection::{MAX_CHANGE_AGE, Tick},
        component::ComponentId,
        lifecycle::RemovedComponentEntity,
        message::MessageCursor,
    },
    prelude::World,
};

pub(crate) struct Guard {
    previous: Option<Tick>,
    count: usize,
    valid: bool,
    removed: Vec<(ComponentId, MessageCursor<RemovedComponentEntity>)>,
}

impl Guard {
    pub(crate) fn new(components: impl IntoIterator<Item = ComponentId>) -> Self {
        Self {
            previous: None,
            count: 0,
            valid: false,
            removed: components
                .into_iter()
                .map(|id| (id, MessageCursor::default()))
                .collect(),
        }
    }

    pub(crate) fn intact(
        &mut self,
        world: &World,
        count: usize,
        changed: impl FnOnce(Tick, Tick) -> bool,
        validate: impl FnOnce() -> bool,
    ) -> bool {
        let now = world.read_change_tick();
        let mut changed = self.previous.is_none_or(|previous| {
            now.get().wrapping_sub(previous.get()) > MAX_CHANGE_AGE || changed(previous, now)
        });
        // Balanced removals and events that aged out cannot become cache hits.
        for (id, cursor) in &mut self.removed {
            if let Some(messages) = world.removed_components().get(*id) {
                changed |= cursor.missed_messages(messages) > 0 || !cursor.is_empty(messages);
                cursor.clear(messages);
            }
        }
        if !self.valid || count != self.count || changed {
            self.valid = validate();
            self.count = count;
        }
        // Preserve same-tick mutation detection and Bevy's wrapping Tick logic.
        self.previous = Some(Tick::new(now.get().wrapping_sub(1)));
        self.valid
    }
}
