// SPDX-License-Identifier: MIT OR Apache-2.0
//! Same-tag command-encoding receipts for the explicitly selected HDR chain.
//! These are actual draw receipts, not GPU completion or source-fidelity claims.

use super::{
    frame::{FrameTag, ViewPacket},
    resolve::ResolveEnabled,
};
use bevy::prelude::*;
use bevy::render::renderer::ViewQuery;
use std::{
    collections::HashMap,
    sync::{Arc, Mutex},
};

#[derive(SystemSet, Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum SourceHdrSystems {
    Gtao,
    Haze,
    Underwater,
    Resolve,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum HdrRole {
    GtaoDepth,
    GtaoRaw,
    GtaoBlurX,
    GtaoBlurY,
    GtaoComposite,
    Haze,
    Underwater,
}

impl HdrRole {
    const ORDER: [Self; 7] = [
        Self::GtaoDepth,
        Self::GtaoRaw,
        Self::GtaoBlurX,
        Self::GtaoBlurY,
        Self::GtaoComposite,
        Self::Haze,
        Self::Underwater,
    ];
    fn index(self) -> usize {
        Self::ORDER
            .iter()
            .position(|role| *role == self)
            .expect("known HDR role")
    }
}

#[derive(Debug, Clone)]
pub struct HdrObservation {
    pub tag: FrameTag,
    /// Depth, raw AO, blur X, blur Y, composite: one real fullscreen draw each.
    pub gtao_draws: [u32; 5],
    pub haze_draws: u32,
    /// Explicit neutral branch of the actual haze control (camera is underwater).
    pub haze_neutral_underwater: bool,
    pub underwater_draws: u32,
    pub shaft_draws: u32,
    pub errors: Vec<String>,
}

impl HdrObservation {
    fn new(tag: FrameTag) -> Self {
        Self {
            tag,
            gtao_draws: [0; 5],
            haze_draws: 0,
            haze_neutral_underwater: false,
            underwater_draws: 0,
            shaft_draws: 0,
            errors: Vec::new(),
        }
    }
    fn encoded(&self, role: HdrRole) -> bool {
        match role {
            HdrRole::Haze => {
                (self.haze_draws == 1 && !self.haze_neutral_underwater)
                    || (self.haze_draws == 0 && self.haze_neutral_underwater)
            }
            HdrRole::Underwater => self.underwater_draws == 1 && self.shaft_draws <= 1,
            role => self.gtao_draws[role.index()] == 1,
        }
    }
    #[must_use]
    pub fn pending_role(&self) -> Option<HdrRole> {
        HdrRole::ORDER.into_iter().find(|role| !self.encoded(*role))
    }
    #[must_use]
    pub fn complete(&self) -> bool {
        self.pending_role().is_none() && self.errors.is_empty()
    }
    fn fail(&mut self, message: String) {
        if self.errors.len() < 8 {
            self.errors.push(message);
        }
    }
}

/// Clone into `MainWorld` and `RenderWorld` for diagnostics; ordering is `RenderWorld`-owned.
#[derive(Resource, Clone, Default)]
pub struct HdrObservations(Arc<Mutex<HashMap<Entity, HdrObservation>>>);

/// Runs even when an effect's target/uniform component is absent or pending.
pub(crate) fn begin_selected(
    view: ViewQuery<&ViewPacket, With<ResolveEnabled>>,
    bus: Option<Res<HdrObservations>>,
) {
    if let Some(bus) = bus {
        let _ = bus.begin(view.into_inner().tag);
    }
}

impl HdrObservations {
    /// Reset before the selected depth pass, even if its pipeline is not ready.
    #[must_use]
    pub fn begin(&self, tag: FrameTag) -> bool {
        let Ok(mut entries) = self.0.lock() else {
            return false;
        };
        if entries
            .get(&tag.view)
            .is_some_and(|entry| entry.tag.serial > tag.serial)
        {
            return false;
        }
        entries.insert(tag.view, HdrObservation::new(tag));
        true
    }
    #[must_use]
    pub fn snapshot(&self, view: Entity) -> Option<HdrObservation> {
        self.0.lock().ok()?.get(&view).cloned()
    }
    #[must_use]
    pub fn snapshots(&self) -> Vec<HdrObservation> {
        self.0.lock().map_or_else(
            |_| Vec::new(),
            |entries| entries.values().cloned().collect(),
        )
    }
    /// A pending predecessor never permits a draw to read a previous frame's target.
    #[must_use]
    pub fn ready_for(&self, tag: FrameTag, role: HdrRole) -> bool {
        self.snapshot(tag.view).is_some_and(|entry| {
            entry.tag == tag && entry.errors.is_empty() && entry.pending_role() == Some(role)
        })
    }
    #[must_use]
    pub fn complete(&self, tag: FrameTag) -> bool {
        self.snapshot(tag.view)
            .is_some_and(|entry| entry.tag == tag && entry.complete())
    }
    /// Call only after an actual fullscreen draw has been encoded.
    #[must_use]
    pub fn record(&self, tag: FrameTag, role: HdrRole, draws: u32) -> bool {
        self.record_inner(tag, role, draws, false, 0)
    }
    #[must_use]
    pub fn record_neutral_haze(&self, tag: FrameTag) -> bool {
        self.record_inner(tag, HdrRole::Haze, 0, true, 0)
    }
    #[must_use]
    pub fn record_underwater(&self, tag: FrameTag, medium_draws: u32, shaft_draws: u32) -> bool {
        self.record_inner(tag, HdrRole::Underwater, medium_draws, false, shaft_draws)
    }
    pub fn reject(&self, tag: FrameTag, role: HdrRole, reason: &str) {
        if let Ok(mut entries) = self.0.lock()
            && let Some(entry) = entries.get_mut(&tag.view).filter(|entry| entry.tag == tag)
        {
            entry.fail(format!("HDR {role:?}: {reason}"));
        }
    }
    fn record_inner(
        &self,
        tag: FrameTag,
        role: HdrRole,
        draws: u32,
        neutral: bool,
        shaft_draws: u32,
    ) -> bool {
        let Ok(mut entries) = self.0.lock() else {
            return false;
        };
        let Some(entry) = entries.get_mut(&tag.view) else {
            return false;
        };
        if entry.tag != tag {
            return false;
        }
        if !entry.errors.is_empty()
            || entry.pending_role() != Some(role)
            || (!neutral && draws != 1)
            || (neutral && (role != HdrRole::Haze || draws != 0))
            || shaft_draws > 1
        {
            entry.fail(format!("Invalid HDR receipt {role:?}: draws={draws}, neutral={neutral}, shafts={shaft_draws}"));
            return false;
        }
        match role {
            HdrRole::Haze => {
                entry.haze_draws = draws;
                entry.haze_neutral_underwater = neutral;
            }
            HdrRole::Underwater => {
                entry.underwater_draws = draws;
                entry.shaft_draws = shaft_draws;
            }
            role => entry.gtao_draws[role.index()] = draws,
        }
        true
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    fn tag(serial: u64) -> FrameTag {
        FrameTag {
            view: World::new().spawn_empty().id(),
            serial,
        }
    }
    fn gtao(bus: &HdrObservations, tag: FrameTag) {
        assert!(bus.begin(tag));
        for role in &HdrRole::ORDER[..5] {
            assert!(bus.ready_for(tag, *role));
            assert!(bus.record(tag, *role, 1));
        }
    }
    #[test]
    fn only_same_tag_actual_complete_order_is_ready() {
        let bus = HdrObservations::default();
        let first = tag(1);
        gtao(&bus, first);
        assert!(!bus.complete(first));
        assert!(bus.record(first, HdrRole::Haze, 1));
        assert!(bus.record_underwater(first, 1, 1));
        assert!(bus.complete(first));
        let next = FrameTag { serial: 2, ..first };
        assert!(!bus.complete(next));
        assert!(bus.begin(next));
        assert!(!bus.complete(first));
        assert!(!bus.record(first, HdrRole::GtaoDepth, 1));
        assert_eq!(
            bus.snapshot(next.view).unwrap().pending_role(),
            Some(HdrRole::GtaoDepth)
        );
    }
    #[test]
    fn neutral_haze_requires_real_predecessors_and_medium_draw() {
        let bus = HdrObservations::default();
        let tag = tag(1);
        gtao(&bus, tag);
        assert!(bus.record_neutral_haze(tag));
        assert!(!bus.complete(tag));
        assert!(bus.record_underwater(tag, 1, 0));
        assert!(bus.complete(tag));
        assert_eq!(bus.snapshot(tag.view).unwrap().haze_draws, 0);
    }
    #[test]
    fn duplicate_missing_or_zero_draw_invalidates_receipt_without_stale_reuse() {
        let bus = HdrObservations::default();
        let tag = tag(1);
        assert!(bus.begin(tag));
        assert!(!bus.record(tag, HdrRole::GtaoRaw, 1));
        assert!(!bus.ready_for(tag, HdrRole::GtaoDepth));
        gtao(&bus, tag);
        assert!(!bus.record(tag, HdrRole::Haze, 0));
        assert!(!bus.complete(tag));
        gtao(&bus, tag);
        assert!(bus.record(tag, HdrRole::Haze, 1));
        assert!(!bus.record(tag, HdrRole::Haze, 1));
        assert!(!bus.complete(tag));
        assert!(bus.snapshot(tag.view).unwrap().errors.len() <= 8);
    }
    #[test]
    fn wrong_view_older_tag_and_repeated_pass_cannot_reuse_complete_receipt() {
        let mut world = World::new();
        let view = world.spawn_empty().id();
        let other = world.spawn_empty().id();
        let tag = FrameTag { view, serial: 2 };
        let bus = HdrObservations::default();
        gtao(&bus, tag);
        assert!(bus.record_neutral_haze(tag));
        assert!(bus.record_underwater(tag, 1, 0));
        assert!(bus.complete(tag));
        assert!(!bus.complete(FrameTag { view: other, ..tag }));
        assert!(!bus.begin(FrameTag { serial: 1, ..tag }));
        assert!(bus.complete(tag));
        // A new render attempt invalidates even the same tag before querying effects.
        assert!(bus.begin(tag));
        assert!(!bus.complete(tag));
        assert_eq!(
            bus.snapshot(view).unwrap().pending_role(),
            Some(HdrRole::GtaoDepth)
        );
    }
}
