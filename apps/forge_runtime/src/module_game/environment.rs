//! Installed environment data feeds the common host; no workspace or Vault lookup.
use super::evidence_contract::{EnvironmentIdentity, EnvironmentObservation};
use forge_core::{EntityId, canonical_json_sha256};
use forge_delivery::module_runtime::LoadedModuleZone;
use forge_engine::source_environment::host::{PreparedHost, Status};
use forge_project::module_package::ModuleEnvironmentSource;

pub fn expected(loaded: &LoadedModuleZone) -> Result<Option<EnvironmentIdentity>, String> {
    loaded
        .manifest
        .environments
        .iter()
        .find(|binding| binding.area_id == loaded.zone.head().base.terrain.area_id)
        .map(|binding| {
            let ModuleEnvironmentSource::SourceProfile { programs, .. } = &binding.source;
            Ok(EnvironmentIdentity {
                solar_clock: binding.solar_clock,
                identity: canonical_json_sha256(binding).map_err(|e| e.to_string())?,
                live_programs: programs.is_some(),
            })
        })
        .transpose()
}

pub fn prepare(loaded: &LoadedModuleZone) -> Result<Option<PreparedHost>, String> {
    let Some(resources) = loaded.environment()? else {
        return Ok(None);
    };
    let identity = expected(loaded)?.ok_or("Admitted environment binding disappeared")?;
    let input = forge_engine::source_environment::package::prepare(&resources.environment)
        .map_err(|error| error.to_string())?;
    let programs = resources
        .programs
        .as_ref()
        .map(forge_engine::source_environment::programs::package::from_package)
        .transpose()?;
    Ok(Some(PreparedHost {
        solar_clock: loaded
            .manifest
            .environments
            .iter()
            .find(|b| b.area_id == loaded.zone.head().base.terrain.area_id)
            .and_then(|b| b.solar_clock),
        input,
        programs,
        identity: identity.identity,
        owner: EntityId::deterministic(format!(
            "module-environment:{}:{}",
            loaded.manifest.module_id,
            loaded.zone.head().base.terrain.area_id
        )),
    }))
}

pub fn observe(world: &bevy::prelude::World) -> Option<EnvironmentObservation> {
    let status = world.get_resource::<Status>()?;
    Some(EnvironmentObservation {
        solar: status.solar_start_minutes.map(|start_minutes| {
            super::evidence_contract::ObservedSolarClock {
                start_minutes,
                hours_bits: status.solar_hours_bits,
                night_bits: status.solar_night_bits,
                ready: status.solar_ready,
            }
        }),
        identity: status.identity,
        ready: status.ready,
        bootstrap_steps: status.bootstrap_steps,
        cloud_steps: status.cloud_steps,
        cloud_submissions: status.cloud_submissions,
        live_submissions: status.live_submissions,
        programs_expected: status.programs_expected,
        programs_prepared: status.programs_prepared,
        hdr_publication: status.hdr_publication,
        publication_frame: status.publication_frame,
        gpu_observed: status.gpu_observed,
        gpu_completed: status.gpu_completed,
        gpu_images: status.gpu_images,
        gpu_materials: status.gpu_materials,
        error: status.error.clone(),
    })
}

/// A declared environment activates the same surface host even when no plant package exists.
pub fn surfaces(
    environment: bool,
    imported: bool,
    sampled: Option<&super::streamed::Prepared>,
) -> Result<Option<forge_engine::source_lighting::surfaces::live::PreparedHost>, String> {
    if !environment || imported {
        return Ok(None);
    }
    Ok(Some(
        forge_engine::source_lighting::surfaces::live::PreparedHost {
            scene: forge_engine::source_lighting::surfaces::scene::Prepared::new(
                &sampled
                    .ok_or("Source surfaces require their admitted raster terrain")?
                    .plan,
            )?,
            wind_speed: 0.0,
            wind_frequency: 0.0,
        },
    ))
}
