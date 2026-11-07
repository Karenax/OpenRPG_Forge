//! Adapt only the current module inputs to the same water host used by its game.
use super::{Acquired, TerrainDocument};
use bevy::prelude::*;
use forge_engine::source_water_host::{self, PreparedHost, PreparedResidentScene};
use std::sync::Arc;

pub(super) fn configure_camera(world: &mut World) -> Result<(), String> {
    let (camera, _) = forge_engine::scene_camera::primary(world)?;
    source_water_host::prepare_camera(world, camera)?;
    world.entity_mut(camera).insert((
        bevy::camera::Hdr,
        Msaa::Off,
        bevy::core_pipeline::tonemapping::Tonemapping::AcesFitted,
    ));
    Ok(())
}

pub(super) fn level(data: Option<&Acquired>) -> Result<Option<f64>, String> {
    let Some(packages) = data.and_then(|data| data.ocean.as_ref()) else {
        return Ok(None);
    };
    let surface = packages
        .surface
        .as_ref()
        .ok_or("La surface de l’océan est absente du module.")?;
    source_water_host::water_level_from_frame(
        surface
            .file("water-frame.bin")
            .ok_or("Le niveau de l’océan est absent du module.")?,
    )
    .map(Some)
}

pub(super) fn prepare(
    world: &mut World,
    document: &TerrainDocument,
    data: Option<&Acquired>,
) -> Result<Option<PreparedResidentScene>, String> {
    let Some(data) = data else {
        return Ok(None);
    };
    let Some(packages) = &data.ocean else {
        return Ok(None);
    };
    if data.environment.is_none() || data.terrain.is_none() {
        return Err(
            "L’océan nécessite le relief et l’environnement associés à cette carte.".into(),
        );
    }
    let plan = world
        .get_resource::<forge_engine::cell_streaming::ResidentSampledTerrain>()
        .ok_or("Le relief de l’océan n’est pas encore disponible.")?
        .plan()
        .clone();
    plan.require_document(document).map_err(|e| e.to_string())?;
    let surface = packages
        .surface
        .as_ref()
        .ok_or("La surface de l’océan est absente du module.")?;
    let shore = packages
        .shore
        .as_ref()
        .ok_or("Le ressac est absent du module.")?;
    let (camera, _) = forge_engine::scene_camera::primary(world)?;
    let position = world
        .get::<Transform>(camera)
        .ok_or("La caméra de la carte est absente.")?
        .translation;
    let center = [f64::from(position.x), f64::from(position.z)];
    let raw = crate::authoring_raster::source_bytes(document)?;
    PreparedHost::new(
        &packages.ocean,
        shore,
        Arc::clone(surface),
        document,
        &plan,
        &raw,
    )?
    .prepare_resident_scene(plan, center)
    .map(Some)
}
