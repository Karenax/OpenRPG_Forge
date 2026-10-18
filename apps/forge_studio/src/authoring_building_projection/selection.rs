//! Explicit selection of rendered building parts; never writes authoring data.
use bevy::{
    picking::mesh_picking::ray_cast::{MeshRayCast, MeshRayCastSettings},
    prelude::*,
};
use forge_core::ContentHash;
use forge_tidewater::authoring_buildings::BuildingPartIdentity;
use leptos::prelude::{RwSignal, Set};
use std::cell::Cell;

thread_local! {
    static ARMED: Cell<Option<ContentHash>> = const { Cell::new(None) };
    static CLICK: Cell<Option<(ContentHash, Vec2)>> = const { Cell::new(None) };
    static OUTPUT: Cell<Option<RwSignal<String>>> = const { Cell::new(None) };
}

pub(crate) fn bind() -> RwSignal<String> {
    let selected = RwSignal::new(String::new());
    OUTPUT.with(|slot| slot.set(Some(selected)));
    leptos::prelude::on_cleanup(move || {
        cancel();
        OUTPUT.with(|slot| {
            if slot.get() == Some(selected) {
                slot.set(None);
            }
        });
    });
    selected
}

pub(crate) fn cancel() {
    ARMED.with(|slot| slot.set(None));
    CLICK.with(|slot| slot.set(None));
}

pub(crate) fn is_armed() -> bool {
    ARMED.with(Cell::get).is_some()
}

pub(crate) fn arm() {
    crate::authoring_brush_pointer::cancel();
    if !crate::authoring_viewport::can_preview_asset()
        || super::PREVIEW.with(|slot| slot.borrow().is_some())
    {
        super::report("Sélection bâtiment indisponible pendant un aperçu ou le playtest.".into());
        return;
    }
    let hash = super::PENDING.with(|slot| slot.borrow().as_ref().map(|(hash, _)| *hash));
    ARMED.with(|slot| slot.set(hash));
    super::report(
        "Cliquer un bâtiment visible pour l’inspecter ; Échap annule. Aucune retouche appliquée."
            .into(),
    );
}

pub(crate) fn click(position: Vec2) {
    if let Some(hash) = ARMED.with(Cell::take) {
        CLICK.with(|slot| slot.set(Some((hash, position))));
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
pub(super) fn pick(
    projection: Res<super::Projection>,
    cameras: Query<
        (&Camera, &GlobalTransform),
        forge_tidewater::tidewater_sky::preview::SceneCameraFilter,
    >,
    parts: Query<&BuildingPartIdentity>,
    mut raycast: MeshRayCast,
) {
    let Some((hash, position)) = CLICK.with(Cell::take) else {
        return;
    };
    let current = super::PENDING.with(|slot| slot.borrow().as_ref().map(|(hash, _)| *hash));
    if !crate::authoring_viewport::can_preview_asset()
        || current != Some(hash)
        || projection.batch.source_hash() != Some(hash)
        || projection.attempted_preview.is_some()
        || super::PREVIEW.with(|slot| slot.borrow().is_some())
        || !position.is_finite()
        || position.min_element() < 0.0
        || position.max_element() > 1.0
    {
        super::report(
            "Sélection refusée : source modifiée, aperçu ou projection en attente.".into(),
        );
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
    // Other visible meshes occlude the building: never select through a wall.
    let hit = raycast
        .cast_ray(ray, &MeshRayCastSettings::default())
        .first()
        .and_then(|(entity, _)| parts.get(*entity).ok())
        .map(|part| part.building);
    OUTPUT.with(|slot| {
        if let Some(output) = slot.get() {
            output.set(hit.map_or_else(String::new, |id| id.to_string()));
        }
    });
    super::report(hit.map_or_else(
        || "Aucun bâtiment sélectionnable à ce point.".into(),
        |id| format!("Bâtiment sélectionné : {id}. Inspecteur mis à jour, carte intacte."),
    ));
}
