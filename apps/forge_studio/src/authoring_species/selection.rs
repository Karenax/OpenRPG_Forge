//! One-shot triangle selection; no authoring mutation.
use bevy::{
    picking::mesh_picking::ray_cast::{MeshRayCast, MeshRayCastSettings},
    prelude::*,
};
use leptos::prelude::{Callable, Callback};
use std::cell::RefCell;

type Reply = Callback<Result<forge_engine::TreePromotionSelection, String>>;
thread_local! {
    static ARMED: RefCell<Option<(forge_core::ContentHash, Reply)>> = const { RefCell::new(None) };
    static CLICK: RefCell<Option<(Vec2, forge_core::ContentHash, Reply)>> = const { RefCell::new(None) };
}

pub(crate) fn cancel() {
    ARMED.with(|s| s.replace(None));
    CLICK.with(|s| s.replace(None));
}

pub(crate) fn is_armed() -> bool {
    ARMED.with(|slot| slot.borrow().is_some())
}

pub(crate) fn arm(hash: forge_core::ContentHash, reply: Reply) {
    crate::authoring_shortcuts::cancel_preview();
    crate::authoring_brush_pointer::cancel();
    ARMED.with(|s| s.replace(Some((hash, reply))));
}

pub(crate) fn click(position: Vec2) {
    if let Some((hash, reply)) = ARMED.with(|s| s.borrow_mut().take()) {
        CLICK.with(|s| s.replace(Some((position, hash, reply))));
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
pub(crate) fn pick(
    cameras: Query<
        (&Camera, &GlobalTransform),
        forge_engine::tidewater_sky::preview::SceneCameraFilter,
    >,
    parts: Query<&forge_engine::GovernedTreePrimitive>,
    source: Option<Res<forge_engine::VegetationScatterProjection>>,
    stats: Option<Res<forge_engine::VegetationProjectionStats>>,
    mut raycast: MeshRayCast,
) {
    let Some((position, head_hash, reply)) = CLICK.with(|s| s.borrow_mut().take()) else {
        return;
    };
    let result = (|| {
        if !crate::authoring_viewport::can_preview_asset()
            || !position.is_finite()
            || position.min_element() < 0.0
            || position.max_element() > 1.0
        {
            return Err("Sélection indisponible".into());
        }
        let stats = stats.as_deref().ok_or("Projection absente")?;
        let (terrain_hash, vegetation_hash) = crate::authoring_vegetation::selection_hashes(stats)?;
        let (camera, transform) = cameras.single().map_err(|e| e.to_string())?;
        let size = camera.logical_viewport_size().ok_or("Vue absente")?;
        let ray = camera
            .viewport_to_world(transform, position * size)
            .map_err(|e| e.to_string())?;
        let hits = raycast.cast_ray(ray, &MeshRayCastSettings::default());
        let part = hits
            .first()
            .and_then(|(entity, _)| parts.get(*entity).ok())
            .ok_or("Cliquer un arbre individuel visible, non masqué par un autre objet")?;
        let instance = source
            .as_deref()
            .ok_or("Scatter absent")?
            .trees()
            .find(|tree| tree.id == part.placement_id)
            .ok_or("Arbre périmé")?;
        Ok(forge_engine::TreePromotionSelection {
            head_hash,
            terrain_hash,
            vegetation_hash,
            layer_id: instance.layer_id.clone(),
            instance_id: instance.id.as_entity_id(),
        })
    })();
    reply.run(result);
}
