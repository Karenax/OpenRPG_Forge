//! Typed admission of installed imported plants into their immutable raster host.
use forge_core::EntityId;
use forge_delivery::module_runtime::LoadedModuleZone;
use forge_engine::source_vegetation::scene::streamed::host::PreparedHost;
use forge_world::imported_vegetation::ImportedPopulation;

pub fn expected(host: &PreparedHost) -> super::evidence_contract::HostIdentity {
    let identity = host.expected();
    super::evidence_contract::HostIdentity {
        source_lighting: identity.source_lighting,
        grass: identity.grass,
        projection: identity.projection,
        head: identity.head,
        terrain: identity.terrain,
        instances: identity.instances,
        colliders: identity.colliders,
    }
}

pub fn observe(world: &bevy::prelude::World) -> Option<super::evidence_contract::HostStatus> {
    let status = world
        .get_resource::<forge_engine::source_vegetation::scene::streamed::host::HostStatus>()?;
    Some(super::evidence_contract::HostStatus {
        surfaces: surface_observation(&status.surfaces),
        grass: grass_observation(&status.grass),
        identity: status.identity,
        head: status.head,
        terrain: status.terrain,
        ready: status.ready,
        admission: super::evidence_contract::HostAdmission {
            render_ready: status.admission.render_ready,
            collisions_ready: status.admission.collisions_ready,
            frame_ready: status.admission.frame_ready,
        },
        colliders: status.colliders,
        canonical_instances: status.canonical_instances,
        canonical_colliders: status.canonical_colliders,
        entities: status.entities,
        replacement_primitives: status.replacement_primitives,
        generation: status.generation,
        sequence: status.sequence,
        detail: status.detail,
        grouped: status.grouped,
        selected_triangles: status.selected_triangles,
        blocked_ticks: status.blocked_ticks,
        error: status.error.clone(),
    })
}

pub fn prepare(module: &LoadedModuleZone) -> Result<Option<PreparedHost>, String> {
    let Some(resolved) = module.vegetation_imported()? else {
        return Ok(None);
    };
    let head = module.zone.head();
    let (terrain, _) = head.effective()?;
    let source = terrain
        .sampled_source
        .as_ref()
        .ok_or("Les plantes importées nécessitent leur terrain raster et ses sources admises.")?;
    let heights = module
        .source_blobs
        .get(&source.samples_sha256)
        .ok_or("La source raster des plantes manque dans le module.")?;
    let vegetation = head
        .base
        .vegetation
        .as_ref()
        .ok_or("La base végétale du module manque.")?;
    let population = ImportedPopulation::resolve(vegetation, |hash, maximum| {
        let bytes = module
            .source_blobs
            .get(&hash)
            .ok_or("Un bloc de plantes manque dans le module.")?;
        if bytes.len() > maximum {
            return Err("Le bloc de plantes dépasse sa limite.".into());
        }
        Ok(bytes.clone())
    })?;
    let prepared = PreparedHost::new(
        &resolved.package,
        resolved.geometry,
        population,
        head,
        heights,
        EntityId::deterministic(format!(
            "module-imported:{}:{}",
            module.manifest.module_id, terrain.area_id
        )),
    )?;
    let prepared = if module.environment()?.is_some() {
        let plan =
            forge_world::sampled_terrain::SampledTerrainPlan::from_document(&terrain, heights)
                .map_err(|error| error.to_string())?;
        prepared.with_source_lighting(&resolved.package, &plan)?
    } else {
        prepared
    };
    if let Some(species) = module.vegetation_species()? {
        prepared
            .with_species(
                head,
                forge_core::canonical_json_sha256(&species.catalog).map_err(|e| e.to_string())?,
            )
            .map(Some)
    } else {
        Ok(Some(prepared))
    }
}

fn grass_observation(
    status: &forge_engine::source_vegetation::grass::host::Status,
) -> super::evidence_contract::GrassStatus {
    super::evidence_contract::GrassStatus {
        identity: status.identity,
        ready: status.ready,
        counts: status.counts,
        generation: status.generation,
        sequence: status.sequence,
        publication_frame: status.publication_frame,
        gpu_epoch: status.gpu_epoch,
        gpu_observed: status.gpu_observed,
        gpu_completed: status.gpu_completed,
        gpu_meshes: status.gpu_meshes,
        gpu_materials: status.gpu_materials,
        gpu_images: status.gpu_images,
        gpu_buffers: status.gpu_buffers,
        pipelines: status.pipelines,
    }
}

pub fn surface_observation(
    status: &forge_engine::source_lighting::surfaces::scene::Status,
) -> super::evidence_contract::SurfaceStatus {
    super::evidence_contract::SurfaceStatus {
        world_receivers: status.world_receivers,
        model_receivers: status.model_receivers,
        terrain_receivers: status.terrain_receivers,
        active: status.active,
        ready: status.ready,
        generation: status.generation,
        opaque_casters: status.opaque_casters,
        plant_batches: status.plant_batches,
        encoded_frames: status.encoded_frames,
        cascade_layers: status.cascade_layers,
        encoded_frame: status.encoded_frame,
        gpu_completed: status.gpu_completed,
        motion_matches: status.motion_matches,
    }
}
