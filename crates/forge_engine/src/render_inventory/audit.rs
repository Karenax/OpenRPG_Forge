//! Shared change/removal guard for immutable renderer projections.
//! GPU fences remain independent and must be acquired on every readiness call.
use bevy::{
    ecs::{
        change_detection::{MAX_CHANGE_AGE, Tick},
        component::ComponentId,
        lifecycle::RemovedComponentEntity,
        message::MessageCursor,
    },
    prelude::{Entity, World},
};

pub(crate) struct Guard {
    previous: Option<Tick>,
    count: usize,
    valid: bool,
    marker: ComponentId,
    removed: Vec<(ComponentId, MessageCursor<RemovedComponentEntity>)>,
}

impl Guard {
    pub(crate) fn new(
        marker: ComponentId,
        components: impl IntoIterator<Item = ComponentId>,
    ) -> Self {
        Self {
            previous: None,
            count: 0,
            valid: false,
            marker,
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
        let mut requires_audit = !self.valid
            || count != self.count
            || self
                .previous
                .is_none_or(|previous| now.get().wrapping_sub(previous.get()) > MAX_CHANGE_AGE);
        for (id, cursor) in &mut self.removed {
            if let Some(messages) = world.removed_components().get(*id) {
                if !requires_audit {
                    // A removed marker includes despawns. An aged-out message
                    // has unknown ownership and must also revoke the cache.
                    requires_audit = cursor.missed_messages(messages) > 0
                        || if *id == self.marker {
                            !cursor.is_empty(messages)
                        } else {
                            cursor.read(messages).any(|event| {
                                world
                                    .get_entity(Entity::from(event.clone()))
                                    .is_ok_and(|entity| entity.contains_id(self.marker))
                            })
                        };
                }
                cursor.clear(messages);
            }
        }
        if requires_audit || self.previous.is_some_and(|previous| changed(previous, now)) {
            self.valid = validate();
            self.count = count;
        }
        // Preserve same-tick mutation detection and Bevy's wrapping Tick logic.
        self.previous = Some(Tick::new(now.get().wrapping_sub(1)));
        self.valid
    }
}
