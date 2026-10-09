// SPDX-License-Identifier: MIT OR Apache-2.0
//! Optional same-frame readiness for a consumer of the source temporal output.
//!
//! Installing this `RenderWorld` resource opts every selected resolve into a
//! fail-closed preflight. The consumer publishes readiness before the HDR chain;
//! absence, pending pipelines, a stale tag or a poisoned lock cannot consume a
//! Halton phase. This is preparation evidence, never a final draw receipt.
use super::frame::FrameTag;
use bevy::prelude::Resource;
use std::sync::Mutex;

#[derive(Resource, Default)]
pub struct ConsumerGate(Mutex<Option<(FrameTag, bool)>>);

impl ConsumerGate {
    /// Publish only after checking all required downstream resources/pipelines.
    /// Monotone preparation tags prevent a late producer overwriting a new view.
    #[must_use]
    pub fn publish(&self, tag: FrameTag, ready: bool) -> bool {
        let Ok(mut current) = self.0.lock() else {
            return false;
        };
        if current.is_some_and(|(old, _)| old.serial > tag.serial) {
            return false;
        }
        *current = Some((tag, ready));
        true
    }

    #[must_use]
    pub fn ready(&self, tag: FrameTag) -> bool {
        self.0
            .lock()
            .is_ok_and(|current| *current == Some((tag, true)))
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::prelude::World;

    #[test]
    fn pending_stale_and_other_view_cannot_advance_a_consumer() {
        let mut world = World::new();
        let first = world.spawn_empty().id();
        let second = world.spawn_empty().id();
        let tag = FrameTag {
            view: first,
            serial: 1,
        };
        let next = FrameTag { serial: 2, ..tag };
        let gate = ConsumerGate::default();
        assert!(!gate.ready(tag));
        assert!(gate.publish(tag, false));
        assert!(!gate.ready(tag));
        assert!(gate.publish(tag, true));
        assert!(gate.ready(tag));
        assert!(!gate.ready(FrameTag {
            view: second,
            ..tag
        }));
        assert!(!gate.ready(next));
        assert!(gate.publish(next, false));
        assert!(!gate.publish(tag, true));
        assert!(!gate.ready(tag));
        assert!(!gate.ready(next));
        assert!(gate.publish(next, true));
        assert!(gate.ready(next));
    }
}
