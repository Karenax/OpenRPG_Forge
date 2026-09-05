//! Transient, renderer-correlated selection for persistent world placements.
use crate::area_tabs::AreaKey;
use forge_core::{AssetId, EntityId, Transform};
use forge_project::WorkspaceDocument;

const CLICK_TRAVEL_THRESHOLD_PX: i32 = 4;

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub(crate) struct WorldSelectionScope {
    pub key: AreaKey,
    pub revision: u64,
    pub generation: u64,
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub(crate) struct WorldPickRequest {
    pub generation: u64,
    pub serial: u64,
    pub viewport_uv: [f32; 2],
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub(crate) struct WorldPickSample {
    pub generation: u64,
    pub serial: u64,
    pub project_id: forge_core::ProjectId,
    pub area_id: forge_core::AreaId,
    pub revision: u64,
    pub placement_id: Option<EntityId>,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub(crate) struct SelectedPlacement {
    pub key: AreaKey,
    pub placement_id: EntityId,
}

#[derive(Clone, Debug, PartialEq)]
pub(crate) struct SelectedPlacementDetails {
    pub key: AreaKey,
    pub placement_id: EntityId,
    pub asset_id: AssetId,
    pub stable_key: String,
    pub transform: Transform,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
struct PendingPick {
    scope: WorldSelectionScope,
    serial: u64,
}

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq)]
pub(crate) struct WorldSelectionState {
    selected: Option<SelectedPlacement>,
    pending: Option<PendingPick>,
    last_serial: u64,
}

impl WorldSelectionState {
    #[must_use]
    #[cfg(test)]
    pub fn selected(self) -> Option<SelectedPlacement> {
        self.selected
    }

    #[must_use]
    pub fn pending_serial(self) -> Option<u64> {
        self.pending.map(|pending| pending.serial)
    }

    pub fn request_pick(
        &mut self,
        scope: WorldSelectionScope,
        viewport_uv: [f32; 2],
    ) -> Result<WorldPickRequest, WorldSelectionError> {
        if scope.revision == 0 || scope.generation == 0 {
            return Err(WorldSelectionError::InvalidScope);
        }
        if !viewport_uv
            .iter()
            .all(|value| value.is_finite() && (0.0..=1.0).contains(value))
        {
            return Err(WorldSelectionError::InvalidViewportCoordinates);
        }
        let serial = self
            .last_serial
            .checked_add(1)
            .ok_or(WorldSelectionError::SerialExhausted)?;
        self.last_serial = serial;
        self.pending = Some(PendingPick { scope, serial });
        Ok(WorldPickRequest {
            generation: scope.generation,
            serial,
            viewport_uv,
        })
    }

    pub fn apply_sample(
        &mut self,
        current_scope: WorldSelectionScope,
        sample: WorldPickSample,
        document: &WorkspaceDocument,
    ) -> Result<WorldPickResolution, WorldSelectionError> {
        let Some(pending) = self.pending else {
            return Ok(WorldPickResolution::IgnoredStale);
        };
        if pending.scope != current_scope
            || placement_revision(document, current_scope.key) != Some(current_scope.revision)
            || sample.generation != pending.scope.generation
            || sample.serial != pending.serial
            || sample.project_id != pending.scope.key.project_id
            || sample.area_id != pending.scope.key.area_id
            || sample.revision != pending.scope.revision
        {
            return Ok(WorldPickResolution::IgnoredStale);
        }
        self.pending = None;
        let Some(placement_id) = sample.placement_id else {
            let changed = self.selected.take().is_some();
            return Ok(WorldPickResolution::Cleared { changed });
        };
        let details = resolve_details(
            SelectedPlacement {
                key: pending.scope.key,
                placement_id,
            },
            document,
        )
        .ok_or(WorldSelectionError::UnknownPlacement(placement_id))?;
        self.selected = Some(SelectedPlacement {
            key: details.key,
            placement_id: details.placement_id,
        });
        Ok(WorldPickResolution::Selected(placement_id))
    }

    pub fn reconcile(
        &mut self,
        current_scope: Option<WorldSelectionScope>,
        document: Option<&WorkspaceDocument>,
    ) {
        if self
            .pending
            .is_some_and(|pending| Some(pending.scope) != current_scope)
        {
            self.pending = None;
        }
        let keep_selected = self.selected.is_some_and(|selected| {
            current_scope.is_some_and(|scope| scope.key == selected.key)
                && document.is_some_and(|source| resolve_details(selected, source).is_some())
        });
        if !keep_selected {
            self.selected = None;
        }
    }

    pub fn clear(&mut self) -> bool {
        self.pending = None;
        self.selected.take().is_some()
    }

    #[must_use]
    pub fn details(self, document: &WorkspaceDocument) -> Option<SelectedPlacementDetails> {
        resolve_details(self.selected?, document)
    }
}

fn resolve_details(
    selected: SelectedPlacement,
    document: &WorkspaceDocument,
) -> Option<SelectedPlacementDetails> {
    if !selected.key.exists(document) {
        return None;
    }
    let area = document.area(selected.key.module_id, selected.key.area_id)?;
    let placement = area
        .placements
        .placements
        .iter()
        .find(|placement| placement.id == selected.placement_id)?;
    let pin = area
        .placements
        .asset_pins
        .iter()
        .find(|pin| pin.asset_id == placement.asset_id)?;
    Some(SelectedPlacementDetails {
        key: selected.key,
        placement_id: placement.id,
        asset_id: placement.asset_id,
        stable_key: pin.stable_key.clone(),
        transform: placement.transform,
    })
}

fn placement_revision(document: &WorkspaceDocument, key: AreaKey) -> Option<u64> {
    if document.project.id != key.project_id {
        return None;
    }
    document
        .area(key.module_id, key.area_id)
        .map(|area| area.placements.revision.get())
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub(crate) enum WorldPickResolution {
    IgnoredStale,
    Cleared { changed: bool },
    Selected(EntityId),
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub(crate) enum WorldSelectionError {
    InvalidScope,
    InvalidViewportCoordinates,
    SerialExhausted,
    UnknownPlacement(EntityId),
}

impl std::fmt::Display for WorldSelectionError {
    fn fmt(&self, formatter: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(formatter, "{self:?}")
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub(crate) struct WorldPointerGesture {
    pointer_id: i32,
    origin: [i32; 2],
    current: [i32; 2],
    pub pan: bool,
    moved: bool,
}

impl WorldPointerGesture {
    #[must_use]
    pub fn begin(pointer_id: i32, position: [i32; 2], pan: bool) -> Self {
        Self {
            pointer_id,
            origin: position,
            current: position,
            pan,
            moved: false,
        }
    }

    pub fn move_to(&mut self, pointer_id: i32, position: [i32; 2]) -> Option<[i32; 2]> {
        if pointer_id != self.pointer_id {
            return None;
        }
        let incremental_delta = [
            position[0].saturating_sub(self.current[0]),
            position[1].saturating_sub(self.current[1]),
        ];
        self.current = position;
        if self.moved {
            return Some(incremental_delta);
        }
        let from_origin = [
            i64::from(position[0]) - i64::from(self.origin[0]),
            i64::from(position[1]) - i64::from(self.origin[1]),
        ];
        let squared_travel = from_origin[0]
            .unsigned_abs()
            .saturating_pow(2)
            .saturating_add(from_origin[1].unsigned_abs().saturating_pow(2));
        if squared_travel > u64::from(CLICK_TRAVEL_THRESHOLD_PX.unsigned_abs()).pow(2) {
            self.moved = true;
            return Some([
                position[0].saturating_sub(self.origin[0]),
                position[1].saturating_sub(self.origin[1]),
            ]);
        }
        Some([0, 0])
    }

    #[must_use]
    pub fn finish(self, pointer_id: i32, viewport_uv: [f32; 2]) -> Option<[f32; 2]> {
        (pointer_id == self.pointer_id
            && !self.moved
            && viewport_uv
                .iter()
                .all(|value| value.is_finite() && (0.0..=1.0).contains(value)))
        .then_some(viewport_uv)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_core::{CommandId, ContentHash, UnitQuaternion, Vec3};
    use forge_project::{AssetPin, AuthoredPlacement, NewProjectCommand, asset_id_from_stable_key};

    fn document_with_placement(key: &str) -> (WorkspaceDocument, AreaKey, EntityId) {
        let mut document = WorkspaceDocument::create(NewProjectCommand {
            command_id: CommandId::deterministic("world selection/create"),
            project_id: forge_core::ProjectId::deterministic("world selection/project"),
            module_id: forge_core::ModuleId::deterministic("world selection/module"),
            area_id: forge_core::AreaId::deterministic("world selection/area"),
            project_name: "Selection".into(),
            module_name: "Module".into(),
            area_name: "Area".into(),
            seed: 9,
            spawn: Vec3::ZERO,
        })
        .unwrap()
        .document;
        let stable_key = format!("fixture:selection/{key}");
        let asset_id = asset_id_from_stable_key(&stable_key);
        let placement_id = EntityId::deterministic(format!("world selection/{key}"));
        let area = &mut document.project.modules[0].areas[0];
        area.placements.asset_pins.push(AssetPin {
            asset_id,
            stable_key,
            subset_id: "fixture-subset".into(),
            pack_id: "fixture:selection".into(),
            source_version: "1".into(),
            author: "Fixture".into(),
            source_path: "fixture/source".into(),
            scene_path: "fixture/scene.gltf".into(),
            scene_content_hash: ContentHash::from_bytes([1; 32]),
            manifest_hash: ContentHash::from_bytes([2; 32]),
            license_spdx: "CC0-1.0".into(),
            license_evidence_hash: ContentHash::from_bytes([3; 32]),
            base_scale: 1.0,
        });
        area.placements.placements.push(AuthoredPlacement {
            id: placement_id,
            asset_id,
            transform: Transform {
                translation_m: Vec3::new(1.0, 2.0, 3.0),
                rotation: UnitQuaternion::IDENTITY,
                scale: Vec3::ONE,
            },
        });
        let area_id = area.id;
        let module_id = document.project.modules[0].id;
        let area_key = AreaKey {
            project_id: document.project.id,
            module_id,
            area_id,
        };
        document.validate().unwrap();
        (document, area_key, placement_id)
    }

    fn scope(document: &WorkspaceDocument, key: AreaKey, generation: u64) -> WorldSelectionScope {
        WorldSelectionScope {
            key,
            revision: placement_revision(document, key).unwrap(),
            generation,
        }
    }

    fn sample(
        request: WorldPickRequest,
        scope: WorldSelectionScope,
        placement_id: Option<EntityId>,
    ) -> WorldPickSample {
        WorldPickSample {
            generation: request.generation,
            serial: request.serial,
            project_id: scope.key.project_id,
            area_id: scope.key.area_id,
            revision: scope.revision,
            placement_id,
        }
    }

    #[test]
    fn click_without_travel_is_distinct_from_orbit_pan_and_wrong_pointer() {
        let click = WorldPointerGesture::begin(4, [100, 80], false);
        assert_eq!(click.finish(4, [0.5, 0.25]), Some([0.5, 0.25]));

        let mut orbit = WorldPointerGesture::begin(5, [100, 80], false);
        assert_eq!(orbit.move_to(9, [200, 80]), None);
        assert_eq!(orbit.move_to(5, [103, 82]), Some([0, 0]));
        assert!(orbit.finish(5, [0.5, 0.25]).is_some());

        let mut pan = WorldPointerGesture::begin(6, [100, 80], true);
        assert_eq!(pan.move_to(6, [105, 80]), Some([5, 0]));
        assert_eq!(pan.finish(6, [0.5, 0.25]), None);
        assert_eq!(click.finish(7, [0.5, 0.25]), None);

        let mut extreme = WorldPointerGesture::begin(8, [i32::MIN, i32::MIN], false);
        assert_eq!(
            extreme.move_to(8, [i32::MAX, i32::MAX]),
            Some([i32::MAX, i32::MAX])
        );
        assert_eq!(extreme.finish(8, [0.5, 0.25]), None);
    }

    #[test]
    fn camera_motion_is_buffered_until_drag_threshold_then_catches_up_exactly() {
        let mut gesture = WorldPointerGesture::begin(11, [100, 100], false);
        assert_eq!(gesture.move_to(11, [102, 101]), Some([0, 0]));
        assert_eq!(gesture.move_to(11, [103, 102]), Some([0, 0]));
        assert_eq!(gesture.move_to(11, [105, 100]), Some([5, 0]));
        assert_eq!(gesture.move_to(11, [108, 97]), Some([3, -3]));
        assert_eq!(gesture.finish(11, [0.5, 0.5]), None);
    }

    #[test]
    fn rapid_pick_is_latest_wins_and_stale_sample_cannot_clear_selection() {
        let (document, key, placement_id) = document_with_placement("latest");
        let scope = scope(&document, key, 3);
        let mut state = WorldSelectionState::default();
        let first = state.request_pick(scope, [0.1, 0.1]).unwrap();
        let second = state.request_pick(scope, [0.2, 0.2]).unwrap();
        assert_eq!(second.serial, first.serial + 1);
        assert_eq!(
            state.apply_sample(scope, sample(first, scope, None), &document),
            Ok(WorldPickResolution::IgnoredStale)
        );
        assert_eq!(state.pending_serial(), Some(second.serial));
        assert_eq!(
            state.apply_sample(scope, sample(second, scope, Some(placement_id)), &document),
            Ok(WorldPickResolution::Selected(placement_id))
        );
        assert_eq!(state.selected().unwrap().placement_id, placement_id);
    }

    #[test]
    fn exact_miss_clears_but_unknown_or_stale_hit_never_replaces_selection() {
        let (document, key, placement_id) = document_with_placement("miss");
        let scope = scope(&document, key, 4);
        let mut state = WorldSelectionState::default();
        let selected = state.request_pick(scope, [0.3, 0.3]).unwrap();
        state
            .apply_sample(
                scope,
                sample(selected, scope, Some(placement_id)),
                &document,
            )
            .unwrap();

        let unknown = state.request_pick(scope, [0.4, 0.4]).unwrap();
        let missing = EntityId::deterministic("missing placement");
        assert_eq!(
            state.apply_sample(scope, sample(unknown, scope, Some(missing)), &document),
            Err(WorldSelectionError::UnknownPlacement(missing))
        );
        assert_eq!(state.selected().unwrap().placement_id, placement_id);

        let miss = state.request_pick(scope, [0.5, 0.5]).unwrap();
        assert_eq!(
            state.apply_sample(scope, sample(miss, scope, None), &document),
            Ok(WorldPickResolution::Cleared { changed: true })
        );
        assert_eq!(state.selected(), None);
    }

    #[test]
    fn scope_revision_and_generation_must_match_exactly() {
        let (document, key, placement_id) = document_with_placement("scope");
        let scope = scope(&document, key, 7);
        let mut state = WorldSelectionState::default();
        let request = state.request_pick(scope, [0.5, 0.5]).unwrap();
        for stale in [
            WorldSelectionScope {
                generation: 8,
                ..scope
            },
            WorldSelectionScope {
                revision: scope.revision + 1,
                ..scope
            },
        ] {
            assert_eq!(
                state.apply_sample(stale, sample(request, scope, Some(placement_id)), &document),
                Ok(WorldPickResolution::IgnoredStale)
            );
        }
        let mut wrong_area = sample(request, scope, Some(placement_id));
        wrong_area.area_id = forge_core::AreaId::deterministic("wrong area");
        assert_eq!(
            state.apply_sample(scope, wrong_area, &document),
            Ok(WorldPickResolution::IgnoredStale)
        );
        assert_eq!(state.pending_serial(), Some(request.serial));
    }

    #[test]
    fn unrelated_workspace_revision_does_not_invalidate_the_exact_placement_projection() {
        let (mut document, key, placement_id) = document_with_placement("workspace-revision");
        let scope = scope(&document, key, 7);
        let mut state = WorldSelectionState::default();
        let request = state.request_pick(scope, [0.5, 0.5]).unwrap();

        document.revision = forge_core::Revision::new(document.revision.get() + 1);
        assert_eq!(
            state.apply_sample(scope, sample(request, scope, Some(placement_id)), &document),
            Ok(WorldPickResolution::Selected(placement_id))
        );

        let next = state.request_pick(scope, [0.5, 0.5]).unwrap();
        document.project.modules[0].areas[0].placements.revision =
            forge_core::Revision::new(scope.revision + 1);
        assert_eq!(
            state.apply_sample(scope, sample(next, scope, None), &document),
            Ok(WorldPickResolution::IgnoredStale)
        );
        assert_eq!(state.pending_serial(), Some(next.serial));
    }

    #[test]
    fn reconcile_retains_existing_id_then_clears_removed_or_closed_scope_without_redo_magic() {
        let (mut document, key, placement_id) = document_with_placement("reconcile");
        let scope = scope(&document, key, 2);
        let mut state = WorldSelectionState::default();
        let request = state.request_pick(scope, [0.5, 0.5]).unwrap();
        state
            .apply_sample(scope, sample(request, scope, Some(placement_id)), &document)
            .unwrap();

        let refreshed = WorldSelectionScope {
            revision: scope.revision + 1,
            generation: scope.generation + 1,
            ..scope
        };
        state.reconcile(Some(refreshed), Some(&document));
        assert_eq!(state.selected().unwrap().placement_id, placement_id);

        let placement = document.project.modules[0].areas[0]
            .placements
            .placements
            .pop()
            .unwrap();
        state.reconcile(Some(refreshed), Some(&document));
        assert_eq!(state.selected(), None);
        document.project.modules[0].areas[0]
            .placements
            .placements
            .push(placement);
        state.reconcile(Some(refreshed), Some(&document));
        assert_eq!(state.selected(), None);
        state.reconcile(None, Some(&document));
        assert_eq!(state.selected(), None);
        assert_eq!(state.pending_serial(), None);
        assert_eq!(
            state
                .request_pick(
                    WorldSelectionScope {
                        generation: refreshed.generation + 1,
                        ..refreshed
                    },
                    [0.5, 0.5],
                )
                .unwrap()
                .serial,
            request.serial + 1
        );
    }

    #[test]
    fn selection_is_transient_and_leaves_project_json_revision_and_hash_bit_exact() {
        let (document, key, placement_id) = document_with_placement("immutable");
        let before_json = document.to_pretty_json().unwrap();
        let before_hash = document.content_hash().unwrap();
        let before_revision = document.revision;
        let scope = scope(&document, key, 9);
        let mut state = WorldSelectionState::default();
        let request = state.request_pick(scope, [0.25, 0.75]).unwrap();
        state
            .apply_sample(scope, sample(request, scope, Some(placement_id)), &document)
            .unwrap();
        let details = state.details(&document).unwrap();
        assert_eq!(details.placement_id, placement_id);
        assert_eq!(details.stable_key, "fixture:selection/immutable");
        state.clear();
        assert_eq!(document.to_pretty_json().unwrap(), before_json);
        assert_eq!(document.content_hash().unwrap(), before_hash);
        assert_eq!(document.revision, before_revision);
    }

    #[test]
    fn invalid_scope_coordinates_and_serial_exhaustion_fail_without_pending_work() {
        let (document, key, _) = document_with_placement("invalid");
        let mut state = WorldSelectionState::default();
        assert_eq!(
            state.request_pick(scope(&document, key, 0), [0.5, 0.5]),
            Err(WorldSelectionError::InvalidScope)
        );
        assert_eq!(
            state.request_pick(scope(&document, key, 1), [f32::NAN, 0.5]),
            Err(WorldSelectionError::InvalidViewportCoordinates)
        );
        state.last_serial = u64::MAX;
        assert_eq!(
            state.request_pick(scope(&document, key, 1), [0.5, 0.5]),
            Err(WorldSelectionError::SerialExhausted)
        );
        assert_eq!(state.pending_serial(), None);
    }
}
