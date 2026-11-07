//! Connect stand attachments to the shared fish resident and shadow producer.
use bevy::{
    camera::primitives::{Frustum, Sphere},
    ecs as bevy_ecs,
    prelude::*,
};
use forge_engine::source_shadows::casters::masked::merge as shadow_merge;

pub(super) fn update(world: &mut World, state: &mut super::State) -> Result<(), String> {
    if state.objects.is_empty() {
        return Ok(());
    }
    let cameras: Vec<_> = world
        .query_filtered::<(&Camera, &GlobalTransform, &Frustum),
            forge_engine::source_sky::preview::SceneCameraFilter>()
        .iter(world)
        .filter(|(camera, _, _)| camera.is_active)
        .map(|(_, global, frustum)| (global.translation(), *frustum))
        .collect();
    if cameras.len() != 1 {
        return Ok(());
    }
    let (camera, frustum) = cameras[0];
    let (time, frame) = super::fish_clock_values(world);
    let lighting = super::super::imported_scene::resident_lighting_material(world)
        .ok_or("Éclairage résident des poissons d'étal absent")?;
    for object in state.objects.values_mut() {
        object.fish.update_view(
            world,
            camera,
            |center, radius| {
                frustum.intersects_sphere(
                    &Sphere {
                        center: center.into(),
                        radius,
                    },
                    true,
                )
            },
            time,
            frame,
        )?;
        object.fish.update_scene_receivers(world, &lighting)?;
    }
    Ok(())
}

#[derive(Resource, Default)]
struct ShadowCache(forge_engine::source_fish::shadow::cache::MaskedFishCache);

pub(super) fn clear_shadow(world: &mut World) {
    world.remove_resource::<ShadowCache>();
    let _ = shadow_merge::publish::<super::super::fish_shadow_scene::Stand>(world, None);
}

pub(super) fn publish_shadow(world: &mut World, state: &super::State) -> Result<(), String> {
    let shader = state.shadow_shader.clone();
    let cameras: Vec<_> = world.query_filtered::<(&Camera, &GlobalTransform),
        forge_engine::source_sky::preview::SceneCameraFilter>().iter(world)
        .filter(|(camera,_)| camera.is_active).map(|(_,global)|global.translation()).collect();
    let target = world
        .get_resource::<crate::authoring_imported_shadows::Receiver>()
        .filter(|receiver| receiver.0.settings.enabled >= 0.5)
        .map(|receiver| receiver.0.image.clone());
    let (Some(target), Some(shader)) = (target, shader) else {
        clear_shadow(world);
        return Ok(());
    };
    if cameras.len() != 1 {
        clear_shadow(world);
        return Ok(());
    }
    let mut inventory = Vec::new();
    for object in state.objects.values() {
        if object.fish.is_dormant() {
            continue;
        }
        inventory.extend(object.fish.shadow_inventory(world)?);
    }
    if inventory.is_empty() {
        clear_shadow(world);
        return Ok(());
    }
    let time = super::fish_clock_values(world).0;
    world.init_resource::<ShadowCache>();
    let frame = world.resource_scope(|world, mut cache: Mut<ShadowCache>| {
        cache.0.frame(world, inventory, target, shader, time)
    })?;
    shadow_merge::publish::<super::super::fish_shadow_scene::Stand>(world, frame)
}
