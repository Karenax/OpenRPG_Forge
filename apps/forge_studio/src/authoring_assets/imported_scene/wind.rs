//! Shared imported-scene wind clock and shader upload, derived from source Frame.
use bevy::prelude::*;
use forge_engine::{
    tidewater_debris::nature::NatureMaterial,
    tidewater_material::VillageMaterial,
    tidewater_plants::PlantMaterial,
    tidewater_terrain::{TerrainMaterial, lod::LodMaterial},
    tidewater_wind::{AcquiredFrame, WindFrame},
};
use std::cell::RefCell;

thread_local! {
    static READY: RefCell<Result<bool,String>> = const { RefCell::new(Ok(false)) };
}

pub(super) fn install(app: &mut App) -> Result<(), String> {
    READY.with(|slot| *slot.borrow_mut() = Ok(false));
    // Resume the captured global phase; acquired pendulums keep their own phases.
    let acquired = AcquiredFrame::reference()?;
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
    // Refresh already-resident materials as well as those created this frame.
    // Their shared input changes without rebuilding meshes or source textures.
    if let Some(mut materials) = world.get_resource_mut::<Assets<VillageMaterial>>() {
        for (_, material) in materials.iter_mut() {
            material.settings.wind = uniform;
        }
    }
    if let Some(mut materials) = world.get_resource_mut::<Assets<PlantMaterial>>() {
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
