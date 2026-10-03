//! Explicit click selection of visible mesh triangles; no document mutation.
use bevy::{
    picking::mesh_picking::ray_cast::{MeshRayCast, MeshRayCastSettings},
    prelude::*,
};
use forge_core::EntityId;
use leptos::prelude::Set;
use std::cell::Cell;

pub(super) use forge_engine::ManualPlacementPart as PlacementPart;

thread_local! {
    static CLICK: Cell<Option<(Vec2, u64)>> = const { Cell::new(None) };
    static SELECTED: Cell<Option<EntityId>> = const { Cell::new(None) };
    static OUTPUT: Cell<Option<leptos::prelude::RwSignal<Option<EntityId>>>> = const { Cell::new(None) };
}

pub(crate) fn bind() -> leptos::prelude::RwSignal<Option<EntityId>> {
    if let Some(selected) = OUTPUT.with(Cell::get) {
        return selected;
    }
    let selected = leptos::prelude::RwSignal::new(SELECTED.with(Cell::get));
    OUTPUT.with(|slot| slot.set(Some(selected)));
    leptos::prelude::on_cleanup(|| {
        cancel();
        OUTPUT.with(|slot| slot.set(None));
    });
    selected
}

fn select(id: Option<EntityId>) {
    SELECTED.with(|slot| slot.set(id));
    OUTPUT.with(|slot| {
        if let Some(output) = slot.get() {
            output.set(id);
        }
    });
}

/// Choose from the current authoring head, without a raycast or document mutation.
pub(crate) fn choose(id: Option<EntityId>) {
    super::super::cancel_preview();
    cancel();
    select(id);
}

pub(crate) fn clear() {
    cancel();
    select(None);
}

pub(crate) fn cancel() {
    CLICK.with(|slot| slot.set(None));
}

pub(crate) fn click(position: Vec2) {
    if !crate::authoring_viewport::can_preview_asset()
        || super::gizmo::active()
        || crate::authoring_path_guides::is_dragging()
        || crate::authoring_building_projection::selection::is_armed()
        || crate::authoring_imported_plants::selection::is_armed()
        || crate::authoring_species::selection::is_armed()
    {
        return;
    }
    CLICK.with(|slot| slot.set(Some((position, super::EPOCH.with(Cell::get)))));
}

pub(super) fn reconcile(placements: &[super::ManualPlacement]) {
    // A newly accepted head invalidates a gesture begun on the previous head,
    // even when the new zone happens to contain the same object identities.
    cancel();
    if SELECTED.with(Cell::get).is_some_and(|id| {
        !placements.iter().any(|p| p.id == id)
            && crate::authoring_imported_population::current_head()
                .is_none_or(|head| super::super::objects::lookup(&head, id).is_err())
    }) {
        clear();
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
pub(super) fn pick(
    cameras: Query<
        (&Camera, &GlobalTransform),
        forge_engine::tidewater_sky::preview::SceneCameraFilter,
    >,
    parts: Query<&PlacementPart>,
    mut raycast: MeshRayCast,
) {
    let Some((position, epoch)) = CLICK.with(Cell::take) else {
        return;
    };
    if !crate::authoring_viewport::can_preview_asset()
        || !super::current(epoch)
        || !super::READY.with(Cell::get)
        || super::proposal_ready() != Ok(true)
        || super::REQUEST.with(|request| request.borrow().is_some())
        || !position.is_finite()
        || position.min_element() < 0.0
        || position.max_element() > 1.0
        || !super::DESIRED.with(|s| super::ACCEPTED.with(|a| *s.borrow() == *a.borrow()))
    {
        super::report("Sélection indisponible pendant un aperçu ou une projection périmée.");
        return;
    }
    let Ok((camera, transform)) = cameras.single() else {
        return;
    };
    let Some(size) = camera.logical_viewport_size() else {
        return;
    };
    let Ok(ray) = camera.viewport_to_world(transform, position * size) else {
        return;
    };
    // Include other visible meshes as occluders, not only selectable placements.
    let hit = raycast
        .cast_ray(ray, &MeshRayCastSettings::default())
        .first()
        .and_then(|(entity, _)| parts.get(*entity).ok())
        .map(|part| part.0)
        .filter(|id| {
            super::ACCEPTED.with(|placements| placements.borrow().iter().any(|p| p.id == *id))
        });
    select(hit);
    super::report(hit.map_or_else(
        || "Aucun placement sélectionnable à ce point.".into(),
        |id| format!("Objet sélectionné : {id}. Inspecteur filtré ; aucune source modifiée."),
    ));
}
