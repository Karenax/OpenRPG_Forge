//! Selected models over the host's immutable corrected population and physics.
use super::runtime::State as Host;
use bevy::prelude::*;
use forge_core::ContentHash;

pub(super) struct Config {
    pub vegetation: forge_project::VegetationDocument,
    pub catalog: ContentHash,
}

pub(super) struct State {
    config: Config,
    pub candidate: Option<crate::ImportedTreePresentation>,
    pub committed: bool,
}

impl State {
    pub fn new(config: Config) -> Self {
        Self {
            config,
            candidate: None,
            committed: false,
        }
    }
}

#[derive(Default, Clone, Debug, PartialEq, Eq, serde::Serialize, serde::Deserialize)]
#[allow(
    clippy::struct_excessive_bools,
    reason = "Independent serialized observations, including explicit no-model coverage"
)]
pub struct Status {
    #[serde(default, skip_serializing_if = "is_false")]
    pub empty: bool,
    pub ready: bool,
    pub cpu_ready: bool,
    pub collisions_ready: bool,
    pub population: Option<ContentHash>,
    pub catalog: Option<ContentHash>,
    pub gpu_generation: Option<u64>,
    pub meshes_expected: usize,
    pub meshes_prepared: usize,
    pub materials_expected: usize,
    pub materials_bound: usize,
    pub images_expected: usize,
    pub images_prepared: usize,
}

#[allow(
    clippy::trivially_copy_pass_by_ref,
    reason = "Serde supplies a reference"
)]
fn is_false(value: &bool) -> bool {
    !*value
}

pub(super) fn prepare(world: &mut World, host: &mut Host) -> Result<bool, String> {
    let Some(species) = host.species.as_mut() else {
        return Ok(true);
    };
    if let Some(candidate) = species.candidate.as_mut() {
        candidate.request_gpu(world)?;
        return Ok(true);
    }
    match world.get_resource::<crate::VegetationPrototypeLoadStatus>() {
        Some(crate::VegetationPrototypeLoadStatus::Failed(error)) => return Err(error.clone()),
        Some(crate::VegetationPrototypeLoadStatus::Ready { .. }) => {}
        _ => return Ok(false),
    }
    let Some(cache) = world.get_resource::<crate::VegetationPrototypeWindCache>() else {
        return Ok(false);
    };
    if let Some(error) = &cache.failure {
        return Err(error.clone());
    }
    let Some(catalog) = cache.selection_catalog.as_ref() else {
        return Ok(false);
    };
    if forge_core::canonical_json_sha256(catalog).map_err(|e| e.to_string())?
        != species.config.catalog
    {
        return Err("Imported species catalog differs from its closed module".into());
    }
    let bindings = crate::bind_imported_trees(
        host.snapshot.as_ref(),
        host.head,
        &species.config.vegetation,
        catalog,
    )?;
    let mut candidate = crate::ImportedTreePresentation::prepare(
        forge_core::EntityId::deterministic(format!("imported-species:{}", host.owner)),
        host.head,
        &bindings,
        cache,
        &species.config.vegetation,
        world.resource::<Assets<Mesh>>(),
    )?;
    let bounds = std::sync::Arc::get_mut(&mut host.bounds)
        .ok_or("Species bounds must finish before plant partition admission")?;
    bounds.include_models(&host.snapshot, &candidate.streaming_radii())?;
    let warm = candidate.staging_selection();
    candidate.stage_hidden(world, host.head, &warm)?;
    if let Err(error) = candidate.request_gpu(world) {
        candidate.retire(world);
        return Err(error);
    }
    species.candidate = Some(candidate);
    Ok(true)
}

pub(super) fn commit(world: &mut World, host: &mut Host, camera: Vec3) -> Result<bool, String> {
    let Some(species) = host.species.as_mut() else {
        return Ok(true);
    };
    if species.committed {
        return Ok(true);
    }
    let Some(candidate) = species.candidate.as_mut() else {
        return Ok(false);
    };
    let ids = candidate.replacement_identities();
    crate::source_lighting::surfaces::live::validate_plant_replacement(world, host.head, &ids)?;
    let token = crate::render_inventory::acquire(
        world,
        host.owner,
        host.render.identity(),
        host.render.generation(),
    );
    let committed = candidate.commit_streamed_replacement(
        world,
        &mut host.render,
        host.admitted.as_ref().ok_or("Plant admission absent")?,
        camera,
        token.as_ref(),
    )?;
    if committed {
        crate::source_lighting::surfaces::live::replace_plant_instances(world, host.head, ids)?;
        species.committed = true;
        host.selected_camera = None;
        host.pending = None;
        host.published = true;
    }
    Ok(committed)
}

pub(super) fn publish_frame(world: &mut World, host: &Host, camera: Vec3) -> Result<(), String> {
    if let Some(candidate) = host.species.as_ref().and_then(|s| s.candidate.as_ref()) {
        candidate.publish_source_shadows(world, host.head, camera)?;
    }
    Ok(())
}

pub(super) fn observe(world: &World, host: &Host, collisions: bool) -> Option<Status> {
    observe_with_resources(world, host, collisions).0
}

pub(super) fn observe_with_resources(
    world: &World,
    host: &Host,
    collisions: bool,
) -> (Option<Status>, bool) {
    let Some(species) = host.species.as_ref() else {
        return (None, true);
    };
    let mut resources_ready = false;
    let mut status = Status {
        collisions_ready: collisions,
        ..default()
    };
    if let Some(candidate) = &species.candidate {
        let readiness = candidate.readiness(world, host.head);
        status.empty = candidate.is_empty();
        status.population = species.config.vegetation.content_hash().ok();
        status.catalog = Some(species.config.catalog);
        status.cpu_ready = species.committed && candidate.is_visible() && readiness.intact;
        status.ready =
            status.cpu_ready && collisions && readiness.gpu_ready && readiness.shadows_ready;
        resources_ready = status.cpu_ready
            && if status.empty {
                readiness.gpu_ready && readiness.shadows_ready
            } else {
                readiness.simulation_resources_ready
            };
        if let Some(gpu) = candidate.gpu_evidence(world) {
            status.gpu_generation = gpu.generation;
            status.meshes_expected = gpu.assets.meshes_expected;
            status.meshes_prepared = gpu.assets.meshes_prepared;
            status.materials_expected = gpu.assets.materials_expected;
            status.materials_bound = gpu.assets.materials_bound;
            status.images_expected = gpu.assets.images_expected;
            status.images_prepared = gpu.assets.images_prepared;
        }
    }
    (Some(status), resources_ready)
}
