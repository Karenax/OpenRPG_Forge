//! Installed species: pinned buffers, existing three-LOD loader and an explicit host barrier.
use super::evidence_contract::{ObservedSpecies, ObservedSpeciesStatus, SpeciesIdentity};
use bevy::{
    asset::{
        AssetApp,
        io::{
            AssetSourceBuilder,
            memory::{Dir, MemoryAssetReader},
        },
    },
    prelude::*,
};
use forge_core::canonical_json_sha256;
use forge_delivery::module_runtime::LoadedModuleZone;
use forge_engine::{
    VegetationPrototypeForestPreview, VegetationPrototypeGpuReadinessPlugin,
    VegetationPrototypeLoadPlugin,
};
use std::{collections::BTreeSet, path::Path};

const MAX_MODEL_BYTES: usize = 64 * 1024 * 1024;
const MAX_RESIDENT_MODEL_BYTES: usize = 256 * 1024 * 1024;

#[derive(Resource)]
struct InstalledSpecies {
    identity: SpeciesIdentity,
    imported: bool,
}

pub struct Prepared {
    root: Dir,
    loader: VegetationPrototypeLoadPlugin,
    pub identity: SpeciesIdentity,
    imported: bool,
}

pub fn prepare(module: &LoadedModuleZone) -> Result<Option<Prepared>, String> {
    let Some(resolved) = module.vegetation_species()? else {
        return Ok(None);
    };
    let head = module.zone.head();
    let (terrain, vegetation) = head.effective()?;
    let vegetation = vegetation.ok_or("Species require an authored vegetation document")?;
    let imported = vegetation.imported_source.is_some();
    let population_source_hash = if imported {
        vegetation.content_hash().map_err(|e| e.to_string())?
    } else {
        forge_world::VegetationGenerationPlan::from_documents_with_buildings(
            &terrain,
            &vegetation,
            head.corrections.paths.as_deref().unwrap_or_default(),
            head.corrections.rivers.as_deref().unwrap_or_default(),
            head.corrections.buildings.as_deref().unwrap_or_default(),
        )
        .and_then(|plan| {
            plan.with_promotions(head.corrections.promotions.as_deref().unwrap_or_default())
        })
        .map_err(|error| error.to_string())?
        .source_hash()
    };
    let identity = SpeciesIdentity {
        population_source_hash,
        catalog_sha256: canonical_json_sha256(&resolved.catalog)
            .map_err(|error| error.to_string())?,
    };
    let root = Dir::default();
    let mut seen = BTreeSet::new();
    let mut total = 0;
    for binding in &resolved.catalog.bindings {
        let pin = resolved
            .assets
            .get(&binding.asset_id)
            .ok_or("Installed species model is absent")?;
        if pin.sha256 != binding.glb_sha256 {
            return Err("Installed species model differs from its catalog pin".into());
        }
        if !seen.insert(pin.sha256) {
            continue;
        }
        let bytes = module.read_payload(&pin.path, MAX_MODEL_BYTES)?;
        total += bytes.len();
        if total > MAX_RESIDENT_MODEL_BYTES {
            return Err("Installed species models exceed 256 MiB".into());
        }
        root.insert_asset(Path::new(&format!("{}.glb", pin.sha256)), bytes);
    }
    let loader =
        VegetationPrototypeLoadPlugin::new(resolved.catalog, &resolved.asset_kit, &vegetation)?
            .with_species_memory_source();
    Ok(Some(Prepared {
        root,
        loader,
        identity,
        imported,
    }))
}

impl Prepared {
    pub fn register(&self, app: &mut App) {
        let root = self.root.clone();
        app.register_asset_source(
            "forge-species",
            AssetSourceBuilder::new(move || Box::new(MemoryAssetReader { root: root.clone() })),
        );
    }

    pub fn install(
        self,
        app: &mut App,
        manual: Vec<forge_project::zone_authoring::placements::ManualPlacement>,
        streamed: bool,
    ) {
        app.insert_resource(InstalledSpecies {
            identity: self.identity,
            imported: self.imported,
        })
        .init_resource::<HostWait>()
        .insert_resource(forge_engine::PlayerSimulationReady(false))
        .add_plugins(self.loader)
        .add_systems(PostUpdate, fail_on_error);
        if !self.imported {
            app.insert_resource(VegetationPrototypeForestPreview)
                .add_plugins(VegetationPrototypeGpuReadinessPlugin);
        }
        if streamed {
            app.add_systems(
                FixedUpdate,
                publish_streamed_readiness.in_set(
                    forge_engine::runtime_static_admission::StaticAdmissionSet::PublishSpecies,
                ),
            );
        } else {
            app.insert_resource(ManualSources(manual)).add_systems(
                FixedUpdate,
                hold.before(forge_engine::native_gameplay::NativeGameplayReadinessSet)
                    .before(forge_engine::PlayerControllerSet::Drive),
            );
        }
    }
}

#[derive(Resource)]
struct ManualSources(Vec<forge_project::zone_authoring::placements::ManualPlacement>);

#[derive(Resource, Default)]
struct HostWait(Option<f64>);

fn publish_streamed_readiness(world: &mut World) {
    let ready = simulation_ready(world);
    if let Some(mut publication) =
        world.get_resource_mut::<forge_engine::runtime_static_admission::SpeciesReadiness>()
    {
        publication.publish(ready);
    }
}

fn hold(world: &mut World) {
    let species_ready = simulation_ready(world);
    let manual_ready = world.resource_scope(|world, sources: Mut<ManualSources>| {
        forge_engine::manual_placements_ready(world, &sources.0)
    });
    let ready = species_ready
        && manual_ready
        && forge_engine::runtime_buildings::observe(world).is_some_and(|state| state.ready)
        && world
            .get_resource::<forge_engine::gameplay::GameplayProjectionStatus>()
            .is_none_or(|state| state.ready)
        && world
            .get_resource::<forge_engine::gameplay::GameplayRuntime>()
            .is_none_or(|runtime| runtime.error().is_none());
    world
        .resource_mut::<forge_engine::PlayerSimulationReady>()
        .0 = ready;
}

fn simulation_ready(world: &World) -> bool {
    if world
        .get_resource::<InstalledSpecies>()
        .is_some_and(|species| species.imported)
    {
        world
            .get_resource::<forge_engine::source_vegetation::scene::streamed::host::HostStatus>()
            .is_some_and(|host| host.simulation_ready && host.error.is_none())
    } else {
        observe(world).is_some_and(|observed| observed.ready())
    }
}

fn fail_on_error(world: &mut World) {
    let observed = observe(world);
    let failure = observed
        .as_ref()
        .and_then(|state| state.status.error.clone());
    // Visibility fences can legitimately advance on every camera selection.
    // The loading deadline follows admitted resources and physical readiness;
    // strict visibility evidence remains available through `observe`.
    let ready = simulation_ready(world);
    let seconds = world.resource::<Time<Real>>().elapsed_secs_f64();
    let timed_out = world.resource_scope(|_, mut wait: Mut<HostWait>| {
        if ready {
            wait.0 = None;
            false
        } else {
            seconds - *wait.0.get_or_insert(seconds) >= 120.0
        }
    });
    if failure.is_some() || timed_out {
        error!(event_kind = "installed_species_failed", reason = ?failure, timed_out,
            "Installed species require matching authoring sources, GPU resources and backend collisions within 120 seconds");
        world.write_message(bevy::app::AppExit::error());
    }
}

pub fn observe(world: &World) -> Option<ObservedSpecies> {
    let installed = world.get_resource::<InstalledSpecies>()?;
    let identity = installed.identity.clone();
    if installed.imported {
        return observe_imported(world, identity);
    }
    let gpu = world.get_resource::<forge_engine::VegetationPrototypeGpuEvidence>()?;
    let status = forge_engine::governed_forest_runtime_status(world);
    Some(ObservedSpecies {
        empty: false,
        identity,
        projected_population_hash: world
            .get_resource::<forge_engine::VegetationProjectionStats>()
            .map(|stats| stats.vegetation_document_hash.clone()),
        projected_catalog_sha256: world
            .get_resource::<forge_engine::VegetationPrototypeWindCache>()
            .and_then(|cache| cache.selection_catalog.as_ref())
            .and_then(|catalog| canonical_json_sha256(catalog).ok()),
        status: ObservedSpeciesStatus {
            ready: status.ready,
            cpu_ready: status.cpu_ready,
            collisions_ready: status.collisions_ready,
            error: status.error,
        },
        gpu_generation: gpu.generation,
        meshes_expected: gpu.assets.meshes_expected,
        meshes_prepared: gpu.assets.meshes_prepared,
        materials_expected: gpu.assets.materials_expected,
        materials_bound: gpu.assets.materials_bound,
        images_expected: gpu.assets.images_expected,
        images_prepared: gpu.assets.images_prepared,
    })
}

fn observe_imported(world: &World, identity: SpeciesIdentity) -> Option<ObservedSpecies> {
    let host = world
        .get_resource::<forge_engine::source_vegetation::scene::streamed::host::HostStatus>()?;
    let species = host.species.clone().unwrap_or_default();
    Some(ObservedSpecies {
        empty: species.empty,
        identity,
        projected_population_hash: species.population.map(|hash| hash.to_string()),
        projected_catalog_sha256: species.catalog,
        status: ObservedSpeciesStatus {
            ready: host.ready && species.ready,
            cpu_ready: species.cpu_ready,
            collisions_ready: species.collisions_ready,
            error: host.error.clone(),
        },
        gpu_generation: species.gpu_generation,
        meshes_expected: species.meshes_expected,
        meshes_prepared: species.meshes_prepared,
        materials_expected: species.materials_expected,
        materials_bound: species.materials_bound,
        images_expected: species.images_expected,
        images_prepared: species.images_prepared,
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_engine::runtime_static_admission::SpeciesReadiness;

    #[test]
    fn streamed_publication_is_not_ready_without_real_species_evidence() {
        let mut world = World::new();
        world.init_resource::<SpeciesReadiness>();
        publish_streamed_readiness(&mut world);
        assert_eq!(world.resource::<SpeciesReadiness>().observed(), Some(false));
        world.remove_resource::<SpeciesReadiness>();
        // Missing publication is a closed condition in the common service.
        publish_streamed_readiness(&mut world);
        assert!(!world.contains_resource::<SpeciesReadiness>());
    }

    #[test]
    fn imported_simulation_uses_separate_physical_admission_and_rejects_errors() {
        let mut world = World::new();
        world.insert_resource(InstalledSpecies {
            imported: true,
            identity: SpeciesIdentity {
                population_source_hash: forge_core::ContentHash::from_bytes([1; 32]),
                catalog_sha256: forge_core::ContentHash::from_bytes([2; 32]),
            },
        });
        world.insert_resource(
            forge_engine::source_vegetation::scene::streamed::host::HostStatus {
                ready: true,
                ..default()
            },
        );
        assert!(!simulation_ready(&world));
        let mut host = world
            .resource_mut::<forge_engine::source_vegetation::scene::streamed::host::HostStatus>();
        host.ready = false;
        host.simulation_ready = true;
        assert!(simulation_ready(&world));
        let mut clock = Time::<Real>::default();
        clock.advance_by(std::time::Duration::from_secs(121));
        world.insert_resource(clock);
        world.insert_resource(HostWait(Some(0.0)));
        world.init_resource::<Messages<bevy::app::AppExit>>();
        fail_on_error(&mut world);
        assert!(world.resource::<HostWait>().0.is_none());
        assert!(world.resource::<Messages<bevy::app::AppExit>>().is_empty());
        world
            .resource_mut::<forge_engine::source_vegetation::scene::streamed::host::HostStatus>()
            .error = Some("stale source".into());
        assert!(!simulation_ready(&world));
        fail_on_error(&mut world);
        assert_eq!(world.resource::<Messages<bevy::app::AppExit>>().len(), 1);
    }
}
