//! Shared imported-scene wind clock and shader upload, derived from source Frame.
use bevy::prelude::*;
use forge_engine::source_surface::SourceSurfaceMaterial;
use forge_engine::source_surface::nature::NatureMaterial;
use forge_engine::source_terrain::TerrainMaterial;
use forge_engine::source_terrain::lod::LodMaterial;
use forge_engine::source_vegetation::captured::CapturedPlantMaterial;
use forge_engine::source_wind::AcquiredFrame;
use forge_engine::source_wind::WindFrame;
use std::cell::RefCell;

thread_local! {
    static READY: RefCell<Result<bool,String>> = const { RefCell::new(Ok(false)) };
}

pub(super) fn install(app: &mut App, acquired: &AcquiredFrame) -> Result<(), String> {
    READY.with(|slot| *slot.borrow_mut() = Ok(false));
    // The caller supplies either a verified content phase or the neutral scene state.
    let acquired = acquired.clone();
    app.insert_resource(acquired.wind_frame()?)
        .insert_resource(acquired)
        .add_systems(Update, advance_clock)
        .add_systems(
            PostUpdate,
            upload
                .after(super::update)
                .after(crate::authoring_imported_plants::update)
                .after(crate::authoring_imported_terrain::project),
        );
    Ok(())
}

pub(super) fn ready() -> Result<bool, String> {
    READY.with(|slot| slot.borrow().clone())
}

pub(super) fn advance_clock(world: &mut World) {
    if !crate::authoring_viewport::can_project_sources() {
        READY.with(|slot| *slot.borrow_mut() = Ok(false));
        return;
    }
    if world.contains_resource::<crate::authoring_render::FastEditing>() {
        // Keep the acquired frame and readiness; editing never advances wind.
        READY.with(|slot| *slot.borrow_mut() = Ok(true));
        return;
    }
    let Some(time) = world.get_resource::<Time>() else {
        return;
    };
    let delta = time.delta_secs_f64();
    let result = world.resource_mut::<WindFrame>().advance(delta);
    READY.with(|slot| *slot.borrow_mut() = result.map(|()| true));
}

fn upload(world: &mut World) {
    if ready() != Ok(true) {
        return;
    }
    let uniform = world.resource::<WindFrame>().material_uniform();
    if world.contains_resource::<crate::authoring_render::FastEditing>() {
        upload_static::<SourceSurfaceMaterial>(
            world,
            uniform,
            |m| m.settings.wind,
            |m, v| m.settings.wind = v,
        );
        upload_static::<CapturedPlantMaterial>(
            world,
            uniform,
            |m| m.params.wind,
            |m, v| m.params.wind = v,
        );
        upload_static::<TerrainMaterial>(
            world,
            uniform,
            |m| m.settings.wind,
            |m, v| m.settings.wind = v,
        );
        upload_static::<LodMaterial>(
            world,
            uniform,
            |m| m.base.settings.wind,
            |m, v| m.base.settings.wind = v,
        );
        upload_static::<NatureMaterial>(
            world,
            uniform,
            |m| m.base.settings.wind,
            |m, v| m.base.settings.wind = v,
        );
        return;
    }
    // Refresh already-resident materials as well as those created this frame.
    // Their shared input changes without rebuilding meshes or source textures.
    if let Some(mut materials) = world.get_resource_mut::<Assets<SourceSurfaceMaterial>>() {
        for (_, material) in materials.iter_mut() {
            material.settings.wind = uniform;
        }
    }
    if let Some(mut materials) = world.get_resource_mut::<Assets<CapturedPlantMaterial>>() {
        for (_, material) in materials.iter_mut() {
            material.params.wind = uniform;
        }
    }
    if let Some(mut materials) = world.get_resource_mut::<Assets<TerrainMaterial>>() {
        for (_, material) in materials.iter_mut() {
            material.settings.wind = uniform;
        }
    }
    if let Some(mut materials) = world.get_resource_mut::<Assets<LodMaterial>>() {
        for (_, material) in materials.iter_mut() {
            material.base.settings.wind = uniform;
        }
    }
    if let Some(mut materials) = world.get_resource_mut::<Assets<NatureMaterial>>() {
        for (_, material) in materials.iter_mut() {
            material.base.settings.wind = uniform;
        }
    }
}

/// `Assets::iter_mut` queues `Modified` for every visited material, even when its
/// value stays the same. Inspect immutable entries before touching new/changed
/// editor materials so a frozen wind does not re-upload them every frame.
fn upload_static<M: Asset>(
    world: &mut World,
    uniform: Vec4,
    read: impl Fn(&M) -> Vec4,
    write: impl Fn(&mut M, Vec4),
) {
    let Some(mut materials) = world.get_resource_mut::<Assets<M>>() else {
        return;
    };
    let changed: Vec<_> = materials
        .iter()
        .filter(|(_, material)| read(material) != uniform)
        .map(|(id, _)| id)
        .collect();
    for id in changed {
        if let Some(mut material) = materials.get_mut(id) {
            write(&mut material, uniform);
        }
    }
}
