//! Ephemeral R2-050 projection observations for the Studio DOM.
//!
//! This module accepts facts produced by the building and placement adapters
//! while they reconcile derived ECS state. It never writes a Forge document and
//! has no reader in projection, Apply, save, or playtest code.

use serde::Serialize;

pub(super) const DIAGNOSTIC_SCHEMA: &str = "forge.r2-050.projection-diagnostic.v1";
pub(super) const DIAGNOSTIC_DOM_ID: &str = "forge-r2-050-projection-diagnostic";
pub(super) const BUILDING_DIAGNOSTIC_DOM_ID: &str = "forge-r2-050-building-projection-diagnostic";
pub(super) const PLACEMENT_DIAGNOSTIC_DOM_ID: &str = "forge-r2-050-placement-projection-diagnostic";

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq, Serialize)]
#[serde(rename_all = "snake_case")]
pub(super) enum ProjectionState {
    #[default]
    NotRequested,
    Pending,
    Ready,
    Stale,
    Failed,
}

#[derive(Clone, Debug, Default, Eq, PartialEq, Serialize)]
pub(super) struct MutationObservation {
    pub retained: Vec<String>,
    pub retained_count: usize,
    pub spawned: Vec<String>,
    pub spawned_count: usize,
    pub rebuilt: Vec<String>,
    pub rebuilt_count: usize,
    pub removed: Vec<String>,
    pub removed_count: usize,
}

#[derive(Clone, Debug, Default, Eq, PartialEq, Serialize)]
pub(super) struct PartObservation {
    pub part_id: Option<String>,
    pub role: Option<String>,
    pub ecs_entity_id: String,
    pub root_ecs_entity_id: Option<String>,
    pub mesh_handle: Option<String>,
    pub material_handle: Option<String>,
    pub rapier_collider_handle: Option<String>,
}

#[derive(Clone, Debug, Default, Eq, PartialEq, Serialize)]
pub(super) struct ObjectObservation {
    pub object_id: String,
    pub root_ecs_entity_id: Option<String>,
    pub world_asset_handle: Option<String>,
    pub parts: Vec<PartObservation>,
}

#[derive(Clone, Debug, Default, Eq, PartialEq, Serialize)]
pub(super) struct ScopeObservation {
    pub expected_source_hash: Option<String>,
    pub observed_source_hash: Option<String>,
    pub expected_epoch: Option<u64>,
    pub observed_epoch: Option<u64>,
    pub state: ProjectionState,
    pub requested_object_ids: Vec<String>,
    pub objects: Vec<ObjectObservation>,
    pub mutation: MutationObservation,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
struct ProjectionDiagnostic {
    schema: &'static str,
    read_only: bool,
    persisted: bool,
    used_for_projection_or_playtest: bool,
    gpu_presentation: &'static str,
    buildings: ScopeObservation,
    placements: ScopeObservation,
}

impl Default for ProjectionDiagnostic {
    fn default() -> Self {
        Self {
            schema: DIAGNOSTIC_SCHEMA,
            read_only: true,
            persisted: false,
            used_for_projection_or_playtest: false,
            gpu_presentation: "not_observed",
            buildings: ScopeObservation::default(),
            placements: ScopeObservation::default(),
        }
    }
}

fn normalize(scope: &mut ScopeObservation) {
    scope.requested_object_ids.sort();
    scope.requested_object_ids.dedup();
    for values in [
        &mut scope.mutation.retained,
        &mut scope.mutation.spawned,
        &mut scope.mutation.rebuilt,
        &mut scope.mutation.removed,
    ] {
        values.sort();
        values.dedup();
    }
    scope.mutation.retained_count = scope.mutation.retained.len();
    scope.mutation.spawned_count = scope.mutation.spawned.len();
    scope.mutation.rebuilt_count = scope.mutation.rebuilt.len();
    scope.mutation.removed_count = scope.mutation.removed.len();
    scope
        .objects
        .sort_by(|left, right| left.object_id.cmp(&right.object_id));
    for object in &mut scope.objects {
        object.parts.sort_by(|left, right| {
            left.part_id
                .cmp(&right.part_id)
                .then_with(|| left.ecs_entity_id.cmp(&right.ecs_entity_id))
        });
    }
}

fn encode<T: Serialize>(value: &T) -> String {
    serde_json::to_string(value).unwrap_or_else(|_| {
        format!(r#"{{"schema":"{DIAGNOSTIC_SCHEMA}","serialization_status":"failed"}}"#)
    })
}

#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod web {
    use super::{
        BUILDING_DIAGNOSTIC_DOM_ID, DIAGNOSTIC_DOM_ID, DIAGNOSTIC_SCHEMA,
        PLACEMENT_DIAGNOSTIC_DOM_ID, ProjectionDiagnostic, ScopeObservation, encode, normalize,
    };
    use leptos::prelude::*;
    use std::cell::RefCell;

    struct DomSignals {
        combined: WriteSignal<String>,
        buildings: WriteSignal<String>,
        placements: WriteSignal<String>,
    }

    thread_local! {
        static LEDGER: RefCell<ProjectionDiagnostic> = RefCell::new(ProjectionDiagnostic::default());
        static SIGNALS: RefCell<Option<DomSignals>> = const { RefCell::new(None) };
    }

    fn current_json() -> (String, String, String) {
        LEDGER.with(|slot| {
            let ledger = slot.borrow();
            (
                encode(&*ledger),
                encode(&ledger.buildings),
                encode(&ledger.placements),
            )
        })
    }

    fn publish(update: impl FnOnce(&mut ProjectionDiagnostic) -> bool) {
        let changed = LEDGER.with(|slot| {
            let mut ledger = slot.borrow_mut();
            update(&mut ledger)
        });
        if !changed {
            return;
        }
        let (combined, buildings, placements) = current_json();
        SIGNALS.with(|slot| {
            if let Some(signals) = slot.borrow().as_ref() {
                let _ = signals.combined.try_set(combined);
                let _ = signals.buildings.try_set(buildings);
                let _ = signals.placements.try_set(placements);
            }
        });
    }

    pub(crate) fn publish_buildings(mut scope: ScopeObservation) {
        normalize(&mut scope);
        publish(|ledger| {
            if ledger.buildings == scope {
                return false;
            }
            ledger.buildings = scope;
            true
        });
    }

    pub(crate) fn publish_placements(mut scope: ScopeObservation) {
        normalize(&mut scope);
        publish(|ledger| {
            if ledger.placements == scope {
                return false;
            }
            ledger.placements = scope;
            true
        });
    }

    #[component]
    pub(crate) fn ProjectionDiagnosticLedger() -> impl IntoView {
        let (combined_json, set_combined_json) = signal(String::new());
        let (building_json, set_building_json) = signal(String::new());
        let (placement_json, set_placement_json) = signal(String::new());
        let (combined, buildings, placements) = current_json();
        set_combined_json.set(combined);
        set_building_json.set(buildings);
        set_placement_json.set(placements);
        SIGNALS.with(|slot| {
            slot.replace(Some(DomSignals {
                combined: set_combined_json,
                buildings: set_building_json,
                placements: set_placement_json,
            }));
        });
        on_cleanup(|| {
            SIGNALS.with(|slot| slot.replace(None));
        });
        view! {
            <output
                id=DIAGNOSTIC_DOM_ID
                data-testid=DIAGNOSTIC_DOM_ID
                data-schema=DIAGNOSTIC_SCHEMA
                data-read-only="true"
                data-persisted="false"
                data-gpu-presentation="not_observed"
                hidden=true
                aria-live="off"
            >{move || combined_json.get()}</output>
            <output
                id=BUILDING_DIAGNOSTIC_DOM_ID
                data-testid=BUILDING_DIAGNOSTIC_DOM_ID
                data-schema=DIAGNOSTIC_SCHEMA
                data-read-only="true"
                hidden=true
                aria-live="off"
            >{move || building_json.get()}</output>
            <output
                id=PLACEMENT_DIAGNOSTIC_DOM_ID
                data-testid=PLACEMENT_DIAGNOSTIC_DOM_ID
                data-schema=DIAGNOSTIC_SCHEMA
                data-read-only="true"
                hidden=true
                aria-live="off"
            >{move || placement_json.get()}</output>
        }
    }
}

#[cfg(all(target_arch = "wasm32", feature = "web"))]
pub(super) use web::{ProjectionDiagnosticLedger, publish_buildings, publish_placements};

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn diagnostic_json_is_read_only_and_stable() {
        let mut diagnostic = ProjectionDiagnostic {
            buildings: ScopeObservation {
                requested_object_ids: vec!["building-b".into(), "building-a".into()],
                objects: vec![ObjectObservation {
                    object_id: "building-b".into(),
                    root_ecs_entity_id: Some("0x0000000000000002".into()),
                    world_asset_handle: None,
                    parts: vec![PartObservation {
                        part_id: Some("part-b".into()),
                        role: Some("Wall".into()),
                        ecs_entity_id: "0x0000000000000002".into(),
                        root_ecs_entity_id: Some("0x0000000000000002".into()),
                        mesh_handle: Some("mesh-b".into()),
                        material_handle: Some("material-b".into()),
                        rapier_collider_handle: Some("collider-b".into()),
                    }],
                }],
                mutation: MutationObservation {
                    retained: vec!["building-b/part-b".into()],
                    ..Default::default()
                },
                state: ProjectionState::Ready,
                ..Default::default()
            },
            ..Default::default()
        };
        let before = diagnostic.clone();
        normalize(&mut diagnostic.buildings);
        normalize(&mut diagnostic.placements);
        let first = encode(&diagnostic);
        let second = encode(&diagnostic);
        assert_eq!(first, second);
        assert_ne!(
            diagnostic, before,
            "only deterministic display ordering changed"
        );
        let parsed: serde_json::Value = serde_json::from_str(&first).unwrap();
        assert_eq!(parsed["schema"], DIAGNOSTIC_SCHEMA);
        assert_eq!(parsed["read_only"], true);
        assert_eq!(parsed["persisted"], false);
        assert_eq!(parsed["used_for_projection_or_playtest"], false);
        assert_eq!(parsed["gpu_presentation"], "not_observed");
        assert_eq!(
            parsed["buildings"]["requested_object_ids"],
            serde_json::json!(["building-a", "building-b"])
        );
    }

    #[test]
    fn stable_dom_ids_and_mutation_sets_are_machine_readable() {
        assert_eq!(DIAGNOSTIC_DOM_ID, "forge-r2-050-projection-diagnostic");
        assert_eq!(
            BUILDING_DIAGNOSTIC_DOM_ID,
            "forge-r2-050-building-projection-diagnostic"
        );
        assert_eq!(
            PLACEMENT_DIAGNOSTIC_DOM_ID,
            "forge-r2-050-placement-projection-diagnostic"
        );
        for (state, expected) in [
            (ProjectionState::NotRequested, "not_requested"),
            (ProjectionState::Pending, "pending"),
            (ProjectionState::Ready, "ready"),
            (ProjectionState::Stale, "stale"),
            (ProjectionState::Failed, "failed"),
        ] {
            assert_eq!(
                serde_json::to_string(&state).unwrap(),
                format!("\"{expected}\"")
            );
        }
        let mut scope = ScopeObservation {
            mutation: MutationObservation {
                retained: vec!["object-b".into(), "object-a".into(), "object-a".into()],
                spawned: vec!["object-c".into()],
                rebuilt: vec!["object-d".into()],
                removed: vec!["object-e".into()],
                ..Default::default()
            },
            ..Default::default()
        };
        normalize(&mut scope);
        assert_eq!(
            scope.mutation.retained,
            vec!["object-a".to_owned(), "object-b".to_owned()]
        );
        assert_eq!(scope.mutation.spawned, vec!["object-c".to_owned()]);
        assert_eq!(scope.mutation.rebuilt, vec!["object-d".to_owned()]);
        assert_eq!(scope.mutation.removed, vec!["object-e".to_owned()]);
        assert_eq!(scope.mutation.retained_count, 2);
        assert_eq!(scope.mutation.spawned_count, 1);
        assert_eq!(scope.mutation.rebuilt_count, 1);
        assert_eq!(scope.mutation.removed_count, 1);
    }
}
