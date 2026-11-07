// SPDX-License-Identifier: MIT OR Apache-2.0
//! Same-tag command-encoding receipts for the explicitly selected HDR chain.
//! These are actual draw receipts, not GPU completion or source-fidelity claims.

use super::{frame::ViewPacket, resolve::ResolveEnabled};
use crate::scene_camera::FrameTag;
use bevy::prelude::*;
use bevy::render::{extract_component::ExtractComponent, renderer::ViewQuery};
use forge_core::ContentHash;
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

/// The scene adapter must account for every admitted water producer. This is
/// source data, not an inference from the visible meshes or a missing plugin.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
#[allow(
    clippy::struct_excessive_bools,
    reason = "Each flag records a separate scene-source admission condition."
)]
pub struct WaterSources {
    pub ocean: bool,
    pub rivers: bool,
    pub other_or_pending: bool,
    pub complete: bool,
}

/// An explicit scene assertion, extracted with the selected view. A render
/// receipt still requires same-frame coverage and no underwater camera state.
#[derive(Component, Clone, Copy, Debug, PartialEq, Eq, ExtractComponent)]
#[extract_component_filter(Without<crate::source_refraction::CaptureCamera>)]
pub struct DryScene {
    view: Entity,
    scene: ContentHash,
    revision: u64,
}

impl DryScene {
    /// Validate the adapter's complete inventory before making an assertion.
    /// Unknown, loading or unsupported water producers are not a dry scene.
    pub fn validate(
        view: Entity,
        scene: ContentHash,
        revision: u64,
        sources: WaterSources,
    ) -> Result<Self, &'static str> {
        if scene.as_bytes() == &[0; 32] || revision == 0 {
            return Err("Dry scene requires an identified scene revision");
        }
        if !sources.complete || sources.ocean || sources.rivers || sources.other_or_pending {
            return Err("Dry scene requires a complete inventory without water");
        }
        Ok(Self {
            view,
            scene,
            revision,
        })
    }

    #[must_use]
    pub const fn scene(self) -> ContentHash {
        self.scene
    }

    #[must_use]
    pub const fn revision(self) -> u64 {
        self.revision
    }
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
    /// An asserted scene is not itself a receipt; it is confirmed below for
    /// this frame only after independent render-side contradiction checks.
    pub dry_scene: Option<DryScene>,
    pub underwater_absent: bool,
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
            dry_scene: None,
            underwater_absent: false,
            errors: Vec::new(),
        }
    }
    fn encoded(&self, role: HdrRole) -> bool {
        match role {
            HdrRole::Haze => {
                (self.haze_draws == 1 && !self.haze_neutral_underwater)
                    || (self.haze_draws == 0 && self.haze_neutral_underwater)
            }
            HdrRole::Underwater => {
                (self.dry_scene.is_none()
                    && !self.underwater_absent
                    && self.underwater_draws == 1
                    && self.shaft_draws <= 1)
                    || (self.dry_scene.is_some()
                        && self.underwater_absent
                        && self.underwater_draws == 0
                        && self.shaft_draws == 0)
            }
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
    view: ViewQuery<(&ViewPacket, Option<&DryScene>), With<ResolveEnabled>>,
    bus: Option<Res<HdrObservations>>,
) {
    if let Some(bus) = bus {
        let (packet, dry) = view.into_inner();
        let _ = bus.begin_with_scene(packet.tag, dry.copied());
    }
}

/// Verify the absence against the actual extracted view and the independent
/// auxiliary census. This records no draw, and cannot certify GPU completion.
pub(crate) fn record_dry_scene(
    view: ViewQuery<
        (
            &ViewPacket,
            &DryScene,
            &super::targets::AuxTargets,
            Option<&crate::source_underwater::Settings>,
        ),
        With<ResolveEnabled>,
    >,
    bus: Option<Res<HdrObservations>>,
    auxiliary: Option<Res<super::render::AuxObservations>>,
) {
    let (packet, dry, targets, underwater) = view.into_inner();
    let Some(bus) = bus else { return };
    let observation = auxiliary.and_then(|bus| bus.snapshot(packet.tag.view));
    match dry_coverage(
        packet.tag,
        *dry,
        targets.generation,
        underwater.is_some(),
        observation.as_ref(),
    ) {
        Ok(true) => {
            let _ = bus.record_dry(packet.tag, *dry);
        }
        Ok(false) => {}
        Err(error) => bus.reject(packet.tag, HdrRole::Underwater, error),
    }
}

fn dry_coverage(
    tag: FrameTag,
    dry: DryScene,
    generation: u64,
    underwater: bool,
    observation: Option<&super::render::AuxObservation>,
) -> Result<bool, &'static str> {
    if dry.view != tag.view || underwater {
        return Err("Dry scene conflicts with the selected view or underwater pass");
    }
    let Some(observation) = observation else {
        return Ok(false);
    };
    if observation.tag != tag || observation.generation != generation || generation == 0 {
        return Ok(false);
    }
    if observation.coverage.water != 0 {
        return Err("Dry scene contains a water draw");
    }
    Ok(observation.coverage.complete() && observation.mesh_draws == observation.coverage.queued)
}

impl HdrObservations {
    /// Reset before the selected depth pass, even if its pipeline is not ready.
    #[must_use]
    pub fn begin(&self, tag: FrameTag) -> bool {
        self.begin_with_scene(tag, None)
    }
    fn begin_with_scene(&self, tag: FrameTag, dry: Option<DryScene>) -> bool {
        let Ok(mut entries) = self.0.lock() else {
            return false;
        };
        if entries
            .get(&tag.view)
            .is_some_and(|entry| entry.tag.serial > tag.serial)
        {
            return false;
        }
        let mut observation = HdrObservation::new(tag);
        observation.dry_scene = dry;
        entries.insert(tag.view, observation);
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
    fn record_dry(&self, tag: FrameTag, dry: DryScene) -> bool {
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
            || entry.pending_role() != Some(HdrRole::Underwater)
            || entry.dry_scene != Some(dry)
            || dry.view != tag.view
            || entry.underwater_draws != 0
            || entry.shaft_draws != 0
        {
            entry.fail("Invalid dry-scene HDR receipt".into());
            return false;
        }
        entry.underwater_absent = true;
        true
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
            || (role == HdrRole::Underwater && entry.dry_scene.is_some())
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
    fn dry(tag: FrameTag) -> DryScene {
        DryScene::validate(
            tag.view,
            ContentHash::from_bytes([7; 32]),
            1,
            WaterSources {
                ocean: false,
                rivers: false,
                other_or_pending: false,
                complete: true,
            },
        )
        .unwrap()
    }
    fn gtao_for_dry(bus: &HdrObservations, tag: FrameTag, dry: DryScene) {
        assert!(bus.begin_with_scene(tag, Some(dry)));
        for role in &HdrRole::ORDER[..5] {
            assert!(bus.record(tag, *role, 1));
        }
        assert!(bus.record(tag, HdrRole::Haze, 1));
    }
    #[test]
    fn dry_scene_requires_a_complete_identified_inventory() {
        let view = tag(1).view;
        let hash = ContentHash::from_bytes([7; 32]);
        let sources = WaterSources {
            ocean: false,
            rivers: false,
            other_or_pending: false,
            complete: true,
        };
        assert!(DryScene::validate(view, hash, 1, sources).is_ok());
        assert!(DryScene::validate(view, ContentHash::from_bytes([0; 32]), 1, sources).is_err());
        assert!(DryScene::validate(view, hash, 0, sources).is_err());
        for invalid in [
            WaterSources {
                ocean: true,
                ..sources
            },
            WaterSources {
                rivers: true,
                ..sources
            },
            WaterSources {
                other_or_pending: true,
                ..sources
            },
            WaterSources {
                complete: false,
                ..sources
            },
        ] {
            assert!(DryScene::validate(view, hash, 1, invalid).is_err());
        }
    }
    #[test]
    fn dry_scene_is_distinct_from_a_draw_and_requires_the_same_tag_and_order() {
        let bus = HdrObservations::default();
        let first = tag(1);
        let dry = dry(first);
        assert!(bus.begin_with_scene(first, Some(dry)));
        assert!(!bus.record_dry(first, dry));
        gtao_for_dry(&bus, first, dry);
        assert!(!bus.record_dry(FrameTag { serial: 2, ..first }, dry));
        assert!(bus.record_dry(first, dry));
        let observation = bus.snapshot(first.view).unwrap();
        assert!(observation.complete());
        assert_eq!(observation.dry_scene, Some(dry));
        assert!(observation.underwater_absent);
        assert_eq!(
            (observation.underwater_draws, observation.shaft_draws),
            (0, 0)
        );
        assert!(!bus.record_dry(first, dry));
        assert!(!bus.complete(first));
        let next = FrameTag { serial: 2, ..first };
        assert!(bus.begin(next));
        assert!(!bus.complete(next));
        assert!(bus.snapshot(first.view).unwrap().dry_scene.is_none());
    }
    #[test]
    fn real_underwater_and_dry_scene_are_mutually_exclusive() {
        let bus = HdrObservations::default();
        let tag = tag(1);
        let dry = dry(tag);
        gtao_for_dry(&bus, tag, dry);
        assert!(!bus.record_underwater(tag, 1, 0));
        assert!(!bus.complete(tag));
        gtao_for_dry(&bus, tag, dry);
        let stale = DryScene::validate(
            tag.view,
            dry.scene(),
            dry.revision() + 1,
            WaterSources {
                ocean: false,
                rivers: false,
                other_or_pending: false,
                complete: true,
            },
        )
        .unwrap();
        assert!(!bus.record_dry(tag, stale));
        assert!(!bus.complete(tag));
        gtao(&bus, tag);
        assert!(bus.record(tag, HdrRole::Haze, 1));
        assert!(bus.record_underwater(tag, 1, 0));
        assert!(bus.complete(tag));
        assert!(!bus.snapshot(tag.view).unwrap().underwater_absent);
    }
    #[test]
    fn dry_render_confirmation_refuses_stale_or_contradictory_coverage() {
        let tag = tag(1);
        let dry = dry(tag);
        let mut observation = super::super::render::AuxObservation {
            tag,
            generation: 4,
            coverage: super::super::phase::AuxCoverage {
                visible: 1,
                registered: 1,
                queued: 1,
                opaque: 1,
                ..Default::default()
            },
            mesh_draws: 1,
            sky_draws: 1,
            hull_draws: 0,
            hull_contract_satisfied: false,
            final_depth_copied: false,
            previous_depth_copied: false,
            history_seeded: false,
            auxiliary_acknowledged: false,
            draw_diagnostics: Vec::new(),
            draw_diagnostics_omitted: 0,
            errors: vec!["Hull census not yet provided".into()],
        };
        // The dry receipt never substitutes for the independent hull contract.
        assert_eq!(
            dry_coverage(tag, dry, 4, false, Some(&observation)),
            Ok(true)
        );
        assert!(!observation.complete());
        assert_eq!(dry_coverage(tag, dry, 4, false, None), Ok(false));
        assert_eq!(
            dry_coverage(tag, dry, 5, false, Some(&observation)),
            Ok(false)
        );
        assert_eq!(
            dry_coverage(
                FrameTag { serial: 2, ..tag },
                dry,
                4,
                false,
                Some(&observation)
            ),
            Ok(false)
        );
        assert!(dry_coverage(tag, dry, 4, true, Some(&observation)).is_err());
        let other = FrameTag {
            view: Entity::from_raw_u32(123).unwrap(),
            ..tag
        };
        assert!(dry_coverage(other, dry, 4, false, Some(&observation)).is_err());
        observation.coverage.water = 1;
        assert!(dry_coverage(tag, dry, 4, false, Some(&observation)).is_err());
        observation.coverage.water = 0;
        observation.coverage.pending = 1;
        assert_eq!(
            dry_coverage(tag, dry, 4, false, Some(&observation)),
            Ok(false)
        );
        observation.coverage.pending = 0;
        observation.coverage.unsupported = 1;
        assert_eq!(
            dry_coverage(tag, dry, 4, false, Some(&observation)),
            Ok(false)
        );
        observation.coverage.unsupported = 0;
        observation.mesh_draws = 0;
        assert_eq!(
            dry_coverage(tag, dry, 4, false, Some(&observation)),
            Ok(false)
        );
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
