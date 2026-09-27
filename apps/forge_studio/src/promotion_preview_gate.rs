//! Presentation acknowledgement only; never authorizes a domain mutation by itself.
use forge_core::{ContentHash, EntityId};
use std::collections::BTreeSet;

#[derive(Default)]
pub struct PromotionPreviewGate {
    key: Option<ContentHash>,
    ready: bool,
    held: BTreeSet<EntityId>,
}

impl PromotionPreviewGate {
    pub fn begin(&mut self, key: ContentHash, held: BTreeSet<EntityId>) {
        self.key = Some(key);
        self.ready = false;
        self.held = held;
    }

    pub fn cancel(&mut self) {
        self.key = None;
        self.ready = false;
        // Keep proposed roots hidden until the placement projector removes them.
        // Cancellation must not briefly reveal them over the restored scatter.
    }

    pub fn reset(&mut self) {
        *self = Self::default();
    }

    #[must_use]
    pub fn placement_visible(&self, id: EntityId) -> bool {
        !self.held.contains(&id) || self.ready
    }

    pub fn acknowledge(&mut self, key: ContentHash) {
        if self.key == Some(key) {
            self.ready = true;
        }
    }

    pub fn check(&self, key: ContentHash, placements_ready: bool) -> Result<(), String> {
        if self.key != Some(key) {
            return Err("Réexaminer la promotion : aperçu absent ou périmé".into());
        }
        if !self.ready || !placements_ready {
            return Err(
                "Attendre les projections du placement et de la végétation avant Appliquer".into(),
            );
        }
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn both_projections_and_exact_live_proposal_are_required() {
        let first = ContentHash::from_bytes([1; 32]);
        let next = ContentHash::from_bytes([2; 32]);
        let mut gate = PromotionPreviewGate::default();
        assert!(gate.check(first, true).is_err());
        gate.begin(first, BTreeSet::new());
        assert!(gate.check(first, true).is_err());
        gate.acknowledge(next);
        assert!(gate.check(first, true).is_err());
        gate.acknowledge(first);
        assert!(gate.check(first, false).is_err());
        assert!(gate.check(first, true).is_ok());
        gate.begin(next, BTreeSet::new());
        gate.acknowledge(first);
        assert!(gate.check(first, true).is_err());
        assert!(gate.check(next, true).is_err());
        gate.acknowledge(next);
        assert!(gate.check(next, true).is_ok());
        gate.cancel();
        gate.acknowledge(next);
        assert!(gate.check(next, true).is_err());
    }

    #[test]
    fn promoted_root_stays_hidden_until_scatter_ack_and_during_cancel() {
        let key = ContentHash::from_bytes([3; 32]);
        let stale = ContentHash::from_bytes([4; 32]);
        let promoted = EntityId::deterministic("promoted");
        let accepted = EntityId::deterministic("accepted");
        let mut gate = PromotionPreviewGate::default();
        gate.begin(key, BTreeSet::from([promoted]));
        assert!(gate.placement_visible(accepted));
        assert!(!gate.placement_visible(promoted));
        gate.acknowledge(stale);
        assert!(!gate.placement_visible(promoted));
        gate.acknowledge(key);
        assert!(gate.placement_visible(promoted));
        gate.cancel();
        gate.acknowledge(key);
        assert!(!gate.placement_visible(promoted));
        assert!(gate.placement_visible(accepted));
        gate.reset();
        assert!(gate.placement_visible(promoted));
        assert!(gate.check(key, true).is_err());
    }
}
