//! Latest accepted building authority projected into the existing Bevy world.
use crate::authoring_projection_diagnostics::{
    MutationObservation, ObjectObservation, PartObservation, ProjectionState, ScopeObservation,
};
use bevy::{ecs as bevy_ecs, prelude::*};
use forge_core::ContentHash;
use forge_engine::authoring_buildings::BuildingProjection;
use forge_project::{authoring::AuthoringState, zone_authoring::ZoneAuthoringHead};
use leptos::prelude::*;
use std::{cell::RefCell, collections::BTreeMap};

pub(crate) mod selection;

thread_local! {
    static PENDING: RefCell<Option<(ContentHash, ZoneAuthoringHead)>> = const { RefCell::new(None) };
    static PREVIEW: RefCell<Option<(ContentHash, ContentHash, ZoneAuthoringHead)>> = const { RefCell::new(None) };
    static STATUS: RefCell<String> = const { RefCell::new(String::new()) };
    static SIGNAL: RefCell<Option<WriteSignal<String>>> = const { RefCell::new(None) };
}

pub(super) fn observe(state: &AuthoringState) {
    restore();
    let pending = state
        .zone_head
        .as_ref()
        .map(|head| head.content_hash().map(|hash| (hash, head.clone())))
        .transpose();
    match pending {
        Ok(pending) => {
            PENDING.with(|slot| slot.replace(pending));
        }
        Err(error) => {
            PENDING.with(|slot| slot.replace(None));
            report(format!("Bâtiments : source refusée : {error}"));
        }
    }
}

pub(super) fn restore() {
    PREVIEW.with(|slot| slot.replace(None));
}

pub(super) fn preview(
    head: &ZoneAuthoringHead,
    receipt: &forge_project::zone_authoring::session::ZoneReceipt,
) -> Result<(), String> {
    restore();
    let candidate = head.building_preview(receipt)?;
    PREVIEW.with(|slot| {
        slot.replace(Some((
            receipt.request.expected_hash,
            receipt.resulting_hash,
            candidate,
        )))
    });
    Ok(())
}

fn report(text: String) {
    STATUS.with(|slot| slot.replace(text.clone()));
    SIGNAL.with(|slot| {
        if let Some(signal) = *slot.borrow() {
            let _ = signal.try_set(text);
        }
    });
}

#[component]
pub(super) fn ProjectionStatus() -> impl IntoView {
    let (status, setter) = signal(STATUS.with(|slot| slot.borrow().clone()));
    SIGNAL.with(|slot| slot.replace(Some(setter)));
    on_cleanup(move || {
        SIGNAL.with(|slot| {
            if *slot.borrow() == Some(setter) {
                slot.replace(None);
            }
        });
    });
    view! { <output aria-live="polite">{move || status.get()}</output> }
}

#[derive(Resource, Default)]
struct Projection {
    batch: BuildingProjection,
    attempted: Option<ContentHash>,
    ghost: BuildingProjection,
    attempted_preview: Option<ContentHash>,
}

pub(super) fn install(app: &mut App) {
    app.init_resource::<Projection>().add_systems(
        Update,
        (update, update_preview, publish_diagnostic, selection::pick).chain(),
    );
}

pub(super) fn ready(world: &World, expected_hash: ContentHash, count: usize) -> bool {
    let Some(projection) = world.get_resource::<Projection>() else {
        return false;
    };
    if PREVIEW.with(|slot| slot.borrow().is_some()) || projection.attempted_preview.is_some() {
        return false;
    }
    material_ready(world, projection, count) && projection.batch.ready(world, expected_hash, count)
}

fn material_ready(world: &World, projection: &Projection, count: usize) -> bool {
    if count == 0 {
        return true;
    }
    if !crate::authoring_assets::imported_buildings::has_environment() {
        return projection.batch.uses_standard_material();
    }
    crate::authoring_assets::imported_scene::resident_lighting_material(world)
        .is_some_and(|lighting| projection.batch.scene_material_is_current(world, &lighting))
}

/// Called by imported_scene after its real material publication, before its shadow pass.
pub(crate) fn sync_material(world: &mut World) {
    let Some(projection) = world.get_resource::<Projection>() else {
        return;
    };
    if projection.batch.part_count() == 0 {
        return;
    }
    let expected = crate::authoring_assets::imported_buildings::has_environment();
    let lighting = if expected {
        crate::authoring_assets::imported_scene::resident_lighting_material(world)
    } else {
        None
    };
    world.resource_scope(|world, mut projection: Mut<Projection>| {
        let result = if expected {
            let Some(lighting) = lighting else {
                return;
            };
            projection.batch.use_scene_material(world, &lighting)
        } else {
            projection.batch.use_standard_material(world)
        };
        if let Err(error) = result {
            report(format!(
                "Matériau source des bâtiments non projeté : {error}"
            ));
        }
    });
}

fn update_preview(world: &mut World) {
    let accepted = PENDING.with(|slot| slot.borrow().as_ref().map(|(hash, _)| *hash));
    let target = PREVIEW.with(|slot| {
        slot.borrow()
            .as_ref()
            .filter(|(base, _, _)| Some(*base) == accepted)
            .map(|(_, hash, _)| *hash)
    });
    if world.resource::<Projection>().attempted_preview == target {
        return;
    }
    if target.is_some() && !super::authoring_viewport::can_preview_asset() {
        return;
    }
    world.resource_scope(|world, mut projection: Mut<Projection>| {
        projection.attempted_preview = target;
        projection.ghost.clear(world);
        projection.batch.set_visible(world, true);
        if target.is_none() {
            report(format!("Aperçu retiré ; {} volumes acceptés restaurés visuellement.", projection.batch.part_count()));
            return;
        }
        let candidate = PREVIEW.with(|slot| slot.borrow().as_ref().map(|(_, _, head)| head.clone()));
        if let Some(candidate) = candidate {
            match projection.ghost.replace(world, &candidate, true) {
                Ok(()) => {
                    // Avoid coplanar flashing; only presentation changes. Accepted
                    // colliders stay untouched while the proposal is inspected.
                    projection.batch.set_visible(world, false);
                    report(format!("Aperçu cyan : {} volumes sans collision. Géométrie acceptée masquée, collisions conservées ; non appliqué.", projection.ghost.part_count()));
                }
                Err(error) => report(format!("Aperçu bâtiment non projeté : {error}")),
            }
        }
    });
}

fn update(world: &mut World) {
    // Restore the ordinary material even when source/viewport checks return early.
    if !crate::authoring_assets::imported_buildings::has_environment() {
        sync_material(world);
    }
    let hash = PENDING.with(|slot| slot.borrow().as_ref().map(|(hash, _)| *hash));
    // Closing/invalidating a source must dispose our batch even when no terrain
    // projection remains available. Only creation waits for the viewport.
    if hash.is_some() && !super::authoring_viewport::can_project_sources() {
        return;
    }
    if world.resource::<Projection>().attempted == hash {
        return;
    }
    world.resource_scope(|world, mut projection: Mut<Projection>| {
        projection.attempted = hash;
        let pending = PENDING.with(|slot| slot.borrow().as_ref().map(|(_, head)| head.clone()));
        let Some(head) = pending else {
            projection.batch.clear(world);
            report("Bâtiments : aucune zone".into());
            return;
        };
        match projection.batch.replace(world, &head, false) {
            Ok(()) => report(format!("Bâtiments acceptés : {} volumes préparés avec collisions fixes. Affichage GPU et passage joué non confirmés.", projection.batch.part_count())),
            Err(error) => {
                projection.batch.clear(world);
                report(format!("Bâtiments non projetés : {error}"));
            }
        }
    });
    sync_material(world);
}

fn part_key(identity: forge_engine::authoring_buildings::BuildingPartIdentity) -> String {
    format!("{}/{}", identity.building, identity.part)
}

/// This system only copies current adapter facts into a DOM ledger. No caller
/// reads that ledger back into the projection or the playtest barrier.
fn publish_diagnostic(world: &mut World) {
    let pending = PENDING.with(|slot| slot.borrow().clone());
    let (expected_hash, requested_object_ids) = pending.as_ref().map_or_else(
        || (None, Vec::new()),
        |(hash, head)| {
            (
                Some(hash.to_string()),
                head.corrections
                    .buildings
                    .as_deref()
                    .unwrap_or_default()
                    .iter()
                    .map(|building| building.id.to_string())
                    .collect::<Vec<_>>(),
            )
        },
    );
    let scope = {
        let projection = world.resource::<Projection>();
        let observed_hash = projection.batch.source_hash();
        let ready = pending.as_ref().is_some_and(|(hash, head)| {
            let count = head.corrections.buildings.as_ref().map_or(0, Vec::len);
            material_ready(world, projection, count) && projection.batch.ready(world, *hash, count)
        });
        let state = match pending.as_ref().map(|(hash, _)| *hash) {
            None => ProjectionState::NotRequested,
            Some(expected) if observed_hash != Some(expected) => ProjectionState::Stale,
            Some(_) if ready => ProjectionState::Ready,
            Some(_) => ProjectionState::Pending,
        };
        let mut objects = BTreeMap::<String, ObjectObservation>::new();
        for part in projection.batch.diagnostic_parts(world) {
            let object_id = part.building.to_string();
            let ecs_entity_id = format!("0x{:016x}", part.ecs_entity);
            let root_ecs_entity_id = format!("0x{:016x}", part.root_ecs_entity);
            objects
                .entry(object_id.clone())
                .or_insert_with(|| ObjectObservation {
                    object_id,
                    root_ecs_entity_id: None,
                    world_asset_handle: None,
                    parts: Vec::new(),
                })
                .parts
                .push(PartObservation {
                    part_id: Some(part.part.to_string()),
                    role: Some(part.role),
                    ecs_entity_id,
                    root_ecs_entity_id: Some(root_ecs_entity_id),
                    mesh_handle: part.mesh_handle,
                    material_handle: part.material_handle,
                    rapier_collider_handle: part.rapier_collider_handle,
                });
        }
        let mutation = projection.batch.last_mutation();
        ScopeObservation {
            expected_source_hash: expected_hash,
            observed_source_hash: observed_hash.map(|hash| hash.to_string()),
            expected_epoch: None,
            observed_epoch: None,
            state,
            requested_object_ids,
            objects: objects.into_values().collect(),
            mutation: MutationObservation {
                retained: mutation.retained.iter().copied().map(part_key).collect(),
                spawned: mutation.spawned.iter().copied().map(part_key).collect(),
                rebuilt: mutation.rebuilt.iter().copied().map(part_key).collect(),
                removed: mutation.removed.iter().copied().map(part_key).collect(),
                ..Default::default()
            },
        }
    };
    crate::authoring_projection_diagnostics::publish_buildings(scope);
}
