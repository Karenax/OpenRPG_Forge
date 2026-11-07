//! Installed data feeds the shared placement producer; no module-specific light solver.
use super::evidence_contract::{ExpectedLocalLights, ObservedLocalLights};
use bevy::prelude::*;
use forge_delivery::module_runtime::LoadedModuleZone;
use forge_engine::source_lighting::local_lights::{gpu, projection::placement::host};
use forge_project::zone_authoring::placements::ManualPlacement;

pub fn prepare(
    loaded: &LoadedModuleZone,
    placements: &[ManualPlacement],
) -> Result<Option<host::PreparedHost>, String> {
    let prepared = host::PreparedHost::from_packages(placements, &loaded.local_lights()?)?;
    if prepared.is_empty() {
        return Ok(None);
    }
    if !loaded
        .manifest
        .environments
        .iter()
        .any(|e| e.area_id == loaded.zone.head().base.terrain.area_id)
    {
        return Err("Les lumières locales source nécessitent leur environnement et les surfaces communes déclarés dans le module.".into());
    }
    Ok(Some(prepared))
}

pub fn expected(prepared: &host::PreparedHost) -> ExpectedLocalLights {
    let status = prepared.expected();
    ExpectedLocalLights {
        identity: status.identity,
        placements: status.expected_placements,
        emitters: status.expected_emitters,
    }
}

pub fn install(app: &mut App, prepared: host::PreparedHost) -> Result<(), String> {
    prepared.install(app)?;
    app.add_systems(Last, guard);
    Ok(())
}

fn guard(world: &mut World) {
    if let Some(error) = world.resource::<host::Status>().error.clone() {
        error!(%error,"Installed module light projection failed");
        world.write_message(AppExit::error());
    }
}

pub fn observe(world: &World) -> Option<ObservedLocalLights> {
    let status = world.get_resource::<host::Status>()?;
    let queued = world
        .get_resource::<gpu::Status>()
        .and_then(|s| s.0.lock().ok().map(|s| s.clone()));
    Some(ObservedLocalLights {
        identity: status.identity,
        placements: status.staged_placements,
        emitters: status.expected_emitters,
        ready: status.ready,
        camera: status.camera.map(Entity::to_bits),
        queued_camera: queued
            .as_ref()
            .and_then(|q| q.queued_camera)
            .map(Entity::to_bits),
        queued_frame: queued.as_ref().and_then(|q| q.queued_main_frame),
        queued_writes: queued.as_ref().map_or(0, |q| q.queued_writes),
        queued_lights: queued.as_ref().map_or(0, |q| q.lights),
        error: status.error.clone(),
        queue_error: queued.as_ref().map_or_else(
            || Some("Source-light queue observation unavailable".into()),
            |q| q.error.clone(),
        ),
    })
}
