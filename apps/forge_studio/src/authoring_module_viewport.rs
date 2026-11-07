//! Editor adapter for the same scene hosts used by the native module game.
//! Canonical heads remain authoritative; these resources are disposable projections.
use crate::module_resources_acquisition::{Acquired, SceneScope};
use crate::{
    authoring_assets::{imported_buildings, placements as requested},
    authoring_module_resources as resources,
};
use bevy::{ecs as bevy_ecs, prelude::*};
use forge_core::{ContentHash, EntityId};
use forge_engine::{
    source_environment::host as environment,
    source_fish::host::{live as fish, scene as fish_scene},
    source_lighting::{
        local_lights::projection::placement::host as lights, surfaces::live as surfaces,
    },
    source_surface::package::host as objects,
    source_terrain::host as terrain_host,
    source_vegetation::scene::streamed::host as plants,
};
use forge_project::{
    TerrainDocument,
    zone_authoring::{ZoneAuthoringHead, placements::ManualPlacement},
};
use std::{
    cell::{Cell, RefCell},
    rc::Rc,
};
mod water;

thread_local! {
    static INSTALLED: Cell<bool> = const { Cell::new(false) };
    static IMPORTED: Cell<bool> = const { Cell::new(false) };
    static ENVIRONMENT: Cell<bool> = const { Cell::new(false) };
    static READY: RefCell<Result<Option<(ContentHash,u64)>, String>> = const { RefCell::new(Ok(None)) };
}
#[allow(
    clippy::struct_excessive_bools,
    reason = "Each family is an independently installed engine host"
)]
#[derive(Default)]
struct Families {
    environment: bool,
    objects: bool,
    imported: bool,
    fish: bool,
    water: bool,
}
#[derive(Resource)]
struct State {
    head: ContentHash,
    scope: SceneScope,
    placements: Vec<ManualPlacement>,
    fish_placements: Vec<ManualPlacement>,
    families: Families,
    error: Option<String>,
    pending: Option<ContentHash>,
    view_revision: u64,
    view_pending: Option<u64>,
    terrain_owner: Option<EntityId>,
}

fn terrain_input(
    world: &World,
    document: &TerrainDocument,
    data: Option<&Acquired>,
) -> Result<Option<terrain_host::PreparedHost>, String> {
    let Some(data) = data else {
        return Ok(None);
    };
    let Some(package) = &data.terrain else {
        return Ok(None);
    };
    if data.environment.is_none() {
        return Err("Ce terrain nécessite l'environnement associé à la carte.".into());
    }
    let plan = world
        .get_resource::<forge_engine::cell_streaming::ResidentSampledTerrain>()
        .ok_or("Le relief de la carte n'est pas encore disponible")?
        .plan()
        .clone();
    if plan.document_hash() != document.content_hash().map_err(|error| error.to_string())? {
        return Err("Le relief affiché correspond à une autre révision.".into());
    }
    let bytes = crate::authoring_raster::source_bytes(document)?;
    let terrain = terrain_host::PreparedHost::new(
        plan,
        bytes.to_vec(),
        package.as_ref().clone(),
        data.terrain_index
            .ok_or("Les ressources du terrain ne sont pas identifiées")?,
    )?;
    let terrain = terrain.with_water_level(water::level(Some(data))?)?;
    Ok(Some(terrain))
}

pub(crate) fn installed() -> bool {
    INSTALLED.with(Cell::get)
}
pub(crate) fn uses_environment() -> bool {
    ENVIRONMENT.with(Cell::get)
}
pub(crate) fn supports_vegetation(document: &forge_project::VegetationDocument) -> bool {
    document.imported_source.is_some() && IMPORTED.with(Cell::get)
}
pub(crate) fn ready() -> Result<bool, String> {
    READY.with(|slot| {
        slot.borrow()
            .as_ref()
            .map(|head| {
                head.is_some_and(|(hash, revision)| {
                    Some(hash) == requested::accepted_hash()
                        && revision == requested::requested_revision()
                })
            })
            .map_err(Clone::clone)
    })
}

fn current() -> Result<(ZoneAuthoringHead, Option<Rc<Acquired>>), String> {
    let head =
        crate::authoring_assets::placements::accepted_head().ok_or("Aucune carte ouverte")?;
    let scope = resources::scope().ok_or("Origine de la carte absente")?;
    if !resources::available() || head.content_hash()? != scope.head {
        return Err("Chargement des ressources de la carte…".into());
    }
    let data = resources::acquired();
    if data
        .as_ref()
        .is_some_and(|data| data.snapshot.head_hash != scope.head)
    {
        return Err("Ressources remplacées par une autre révision".into());
    }
    Ok((head, data))
}

fn placements(
    head: &ZoneAuthoringHead,
    data: Option<&Acquired>,
) -> Result<Vec<ManualPlacement>, String> {
    select_placements(
        head.corrections.placements.as_deref().unwrap_or_default(),
        data,
    )
}

fn select_placements(
    placements: &[ManualPlacement],
    data: Option<&Acquired>,
) -> Result<Vec<ManualPlacement>, String> {
    let Some(package) = data.and_then(|data| data.objects.as_ref()) else {
        return Ok(Vec::new());
    };
    let mut result = Vec::new();
    for placement in placements {
        if package
            .catalog()
            .assets
            .iter()
            .any(|asset| asset.id == placement.asset_id)
        {
            package
                .catalog()
                .resolve_import_pin(placement.asset_id, placement.asset_hash)?;
            result.push(placement.clone());
        }
    }
    Ok(result)
}

fn select_fish(
    placements: &[ManualPlacement],
    data: Option<&Acquired>,
) -> Result<Vec<ManualPlacement>, String> {
    let Some(data) = data else {
        return Ok(Vec::new());
    };
    fish_scene::select_placements(data.fish_profiles.iter().map(AsRef::as_ref), placements)
}

fn prepare_fish(
    data: &Acquired,
    placements: &[ManualPlacement],
) -> Result<Option<fish_scene::PreparedScene>, String> {
    let shader = data.objects.as_ref().map_or(
        Ok(forge_engine::source_surface::PROFILE_PROGRAM),
        |package| forge_engine::source_surface::package::shader_source(package),
    )?;
    fish_scene::from_catalogs(
        data.fish_profiles.iter().map(AsRef::as_ref),
        placements,
        shader,
        forge_engine::source_fish::surface::SpecularMode::ThreeReference,
    )
}

fn vegetation(
    head: &ZoneAuthoringHead,
    data: &Acquired,
    owner: EntityId,
) -> Result<plants::PreparedHost, String> {
    let hash = head.content_hash()?;
    let (terrain, _) = head.effective()?;
    let raw = crate::authoring_raster::source_bytes(&terrain)?;
    let package = data.imported.as_ref().ok_or("Paquet végétal absent")?;
    let population = crate::authoring_imported_population::current_population(hash)?;
    let prepared = plants::PreparedHost::new(
        package,
        package.decode_geometry()?,
        population,
        head,
        &raw,
        owner,
    )?;
    let prepared = if data.environment.is_some() {
        let plan = forge_world::sampled_terrain::SampledTerrainPlan::from_document(&terrain, &raw)
            .map_err(|e| e.to_string())?;
        prepared.with_source_lighting(package, &plan)?
    } else {
        prepared
    };
    if data.snapshot.species.is_some() {
        let catalog = crate::authoring_species::module_catalog(head, data)?;
        prepared.with_species(
            head,
            forge_core::canonical_json_sha256(&catalog).map_err(|e| e.to_string())?,
        )
    } else {
        Ok(prepared)
    }
}

#[allow(
    clippy::too_many_lines,
    reason = "Orders admission and installation of the existing environment, plant, object and light hosts"
)]
pub(crate) fn install(app: &mut App, document: &TerrainDocument) -> Result<(), String> {
    if !resources::owns_source() {
        return Ok(());
    }
    let (head, data) = current()?;
    forge_engine::source_wind::SceneWind::publish(app.world_mut(), head.corrections.wind)?;
    let scope = resources::scope().ok_or("Origine de la carte absente")?;
    let owner = EntityId::deterministic(format!(
        "studio-environment:{}:{}",
        scope.module, scope.area
    ));
    let mut families = Families::default();
    if let Some(data) = &data {
        if let Some(binding) = &data.snapshot.environment {
            let Some(imported_buildings::EnvironmentUpdate::Replace { prepared, .. }) =
                resources::take_environment()
            else {
                return Err("Préparation de l'environnement de la carte absente".into());
            };
            let programs = data
                .programs
                .as_ref()
                .map(|package| {
                    forge_engine::source_environment::programs::package::from_package(package)
                })
                .transpose()?;
            environment::PreparedHost {
                solar_clock: binding.solar_clock,
                input: *prepared,
                programs,
                identity: forge_core::canonical_json_sha256(binding).map_err(|e| e.to_string())?,
                owner,
            }
            .install(app)?;
            families.environment = true;
        }
        if data.imported.is_some() {
            app.add_plugins(forge_engine::VegetationPrototypeLoaderPlugin);
            install_picking(app, data)?;
            let plant_owner =
                EntityId::deterministic(format!("studio-imported:{}:{}", scope.module, scope.area));
            vegetation(&head, data, plant_owner)?.install(app)?;
            let _ = resources::take_plants();
            families.imported = true;
        } else if families.environment {
            let raw = crate::authoring_raster::source_bytes(document)?;
            let plan =
                forge_world::sampled_terrain::SampledTerrainPlan::from_document(document, &raw)
                    .map_err(|e| e.to_string())?;
            surfaces::PreparedHost {
                scene: forge_engine::source_lighting::surfaces::scene::Prepared::new(&plan)?,
                wind_speed: 0.0,
                wind_frequency: 0.0,
            }
            .install(app)?;
        }
    }
    let terrain_owner = if let Some(input) = terrain_input(app.world(), document, data.as_deref())?
    {
        input.install(app)?;
        Some(EntityId::deterministic(format!(
            "studio-terrain:{}:{}",
            scope.module, scope.area
        )))
    } else {
        None
    };
    if let Some(input) = water::prepare(app.world_mut(), document, data.as_deref())? {
        water::configure_camera(app.world_mut())?;
        input.install(app, true)?;
        families.water = true;
    }
    let selected = placements(&head, data.as_deref())?;
    if let Some(package) = data.as_ref().and_then(|data| data.objects.as_ref()) {
        let shader = forge_engine::source_surface::package::shader_source(package)?;
        forge_engine::source_surface::receivers::install_shader(app, shader)
            .map_err(|e| e.to_string())?;
        let Some(imported_buildings::BuildingUpdate::Replace { prepared, .. }) =
            resources::take_buildings()
        else {
            return Err("Préparation des objets de la carte absente".into());
        };
        let (geometry, library) = *prepared;
        let input = objects::PreparedHost::from_prepared(geometry, library, selected.clone())?;
        if families.water {
            input.install_with_water(app)?;
        } else {
            input.install_without_water(app)?;
        }
        families.objects = true;
    }
    let fish_placements = select_fish(&requested::requested(), data.as_deref())?;
    if let Some(data) = data.as_ref().filter(|data| !data.fish_profiles.is_empty()) {
        if families.environment {
            fish::install(app, prepare_fish(data, &fish_placements)?, false)?;
            families.fish = true;
        } else if !fish_placements.is_empty() {
            return Err("Les poissons nécessitent l'environnement déclaré de la carte.".into());
        }
    }
    let all = head.corrections.placements.as_deref().unwrap_or_default();
    let light_input = lights::PreparedHost::from_packages(
        all,
        &data
            .as_ref()
            .map_or_else(Default::default, |data| data.local_lights.clone()),
    )?;
    if !families.environment && !light_input.is_empty() {
        return Err("Les lumières locales requièrent l'environnement déclaré de la carte.".into());
    }
    light_input.install(app)?;
    IMPORTED.with(|imported| imported.set(families.imported));
    ENVIRONMENT.with(|environment| environment.set(families.environment));
    app.insert_resource(State {
        head: scope.head,
        scope: scope.scene(),
        placements: selected,
        fish_placements,
        families,
        error: None,
        pending: None,
        view_revision: requested::requested_revision(),
        view_pending: None,
        terrain_owner,
    })
    .add_systems(PreUpdate, suspend)
    .add_systems(
        Update,
        (
            apply.after(forge_engine::cell_streaming::CellStreamingSet::Update),
            sync_fish_clock,
        ),
    )
    .add_systems(
        PostUpdate,
        publish
            .after(terrain_host::TerrainHostSet)
            .after(objects::ObjectHostSet)
            .after(lights::PlacementLightHostSet)
            .after(surfaces::SurfaceHostSet)
            .after(forge_engine::source_water_host::WaterHostSet)
            .after(fish::FishHostSet),
    );
    INSTALLED.with(|installed| installed.set(true));
    Ok(())
}

fn sync_fish_clock(world: &mut World) {
    let frozen = world.contains_resource::<crate::authoring_render::FastEditing>();
    if let Some(mut policy) = world.get_resource_mut::<fish::ClockPolicy>() {
        policy.frozen = frozen;
    }
}

fn install_picking(app: &mut App, data: &Acquired) -> Result<(), String> {
    let package = data.imported.as_ref().ok_or("Paquet végétal absent")?;
    let shader = std::str::from_utf8(package.file("plant.wgsl").ok_or("Shader végétal absent")?)
        .map_err(|error| error.to_string())?;
    forge_engine::source_vegetation::picking::world::install(app, shader)?;
    forge_engine::source_vegetation::picking::governed::install(app)?;
    let program = data.objects.as_ref().map_or(
        Ok(forge_engine::source_surface::PROFILE_PROGRAM),
        |package| forge_engine::source_surface::package::shader_source(package),
    )?;
    forge_engine::source_vegetation::picking::occluders::install::<
        forge_engine::source_terrain::lod::LodMaterial,
        forge_engine::source_surface::SourceSurfaceMaterial,
    >(app, program)?;
    crate::authoring_imported_plants::selection::install(app);
    Ok(())
}

fn suspend(world: &mut World) {
    let Some(target) = requested::accepted_hash() else {
        return;
    };
    let revision = requested::requested_revision();
    let state = world.resource::<State>();
    if state.error.is_some() {
        return;
    }
    let head_changed = state.head != target || state.pending.is_some();
    let head_request = head_changed && state.pending != Some(target);
    let view_request = state.view_revision != revision && state.view_pending != Some(revision);
    if !head_request && !view_request {
        return;
    }
    let previous = state.head;
    let imported = state.families.imported;
    let result = (|| {
        if let Some(owner) = world.resource::<State>().terrain_owner {
            terrain_host::release_inventory(world, owner);
        }
        let expected = world
            .resource::<lights::Status>()
            .identity
            .ok_or("Éclairage de la carte absent")?;
        lights::suspend_authoring(world, expected)?;
        if imported && head_request {
            let head = requested::accepted_head().ok_or("Aucune carte ouverte")?;
            plants::suspend_authoring(world, previous, &head)?;
        }
        Ok::<_, String>(())
    })();
    let mut state = world.resource_mut::<State>();
    if let Err(error) = result {
        state.error = Some(error);
        return;
    }
    if head_request {
        state.pending = Some(target);
    }
    state.view_pending = Some(revision);
}

fn update_view(
    world: &mut World,
    state: &mut State,
    data: Option<&Acquired>,
) -> Result<(), String> {
    let all = requested::requested();
    let selected = select_placements(&all, data)?;
    let fish_placements = select_fish(&all, data)?;
    if state.families.fish && state.fish_placements != fish_placements {
        let prepared = if fish::needs_rebuild(&state.fish_placements, &fish_placements)
            && !fish_placements.is_empty()
        {
            prepare_fish(
                data.ok_or("Ressources des poissons absentes")?,
                &fish_placements,
            )?
        } else {
            None
        };
        fish::replace(world, &state.fish_placements, &fish_placements, prepared)?;
    } else if !state.families.fish && !fish_placements.is_empty() {
        return Err("Les poissons nécessitent l'environnement déclaré de la carte.".into());
    }
    let input = lights::PreparedHost::from_packages(
        &all,
        &data.map_or_else(Default::default, |data| data.local_lights.clone()),
    )?;
    let expected = world
        .resource::<lights::Status>()
        .identity
        .ok_or("Éclairage de la carte absent")?;
    input.replace(world, expected)?;
    if state.families.objects && state.placements != selected {
        objects::replace_placements(world, &state.placements, &selected)?;
    }
    state.placements = selected;
    state.fish_placements = fish_placements;
    state.view_revision = requested::requested_revision();
    state.view_pending = None;
    Ok(())
}

fn apply(world: &mut World) {
    if !crate::authoring_viewport::can_project_sources() {
        return;
    }
    let Some(target) = crate::authoring_assets::placements::accepted_hash() else {
        return;
    };
    if world.resource::<State>().families.imported {
        match crate::authoring_species::ready(world) {
            Ok(true) => {}
            Ok(false) => return,
            Err(error) => {
                world.resource_mut::<State>().error = Some(error);
                return;
            }
        }
    }
    let state = world.resource::<State>();
    if state.error.is_some() || !resources::available() {
        return;
    }
    if state.head == target
        && state.pending.is_none()
        && state.view_pending.is_none()
        && state.view_revision == requested::requested_revision()
        && resources::scope().is_some_and(|scope| scope.scene() == state.scope)
    {
        return;
    }
    let Ok((head, data)) = current() else { return };
    if resources::scope().is_none_or(|scope| scope.scene() != world.resource::<State>().scope) {
        world.resource_mut::<State>().error =
            Some("Les ressources ont changé : sauvegardez puis rechargez l'atelier.".into());
        return;
    }
    match requested::base_ready() {
        Ok(true) => {}
        Ok(false) => return,
        Err(error) => {
            world.resource_mut::<State>().error = Some(error);
            return;
        }
    }
    if world.resource::<State>().head == target && world.resource::<State>().pending.is_none() {
        let result = world.resource_scope(|world, mut state: Mut<State>| {
            update_view(world, &mut state, data.as_deref())
        });
        if let Err(error) = result {
            world.resource_mut::<State>().error = Some(error);
        }
        return;
    }

    if data.as_ref().is_some_and(|data| data.imported.is_some())
        && crate::authoring_imported_population::ready_head_hash() != Some(target)
    {
        return;
    }
    let Ok((terrain, _)) = head.effective() else {
        return;
    };
    let Ok(terrain_hash) = terrain.content_hash() else {
        return;
    };
    if !forge_engine::cell_streaming::source_is_synchronized(world, terrain_hash) {
        return;
    }
    let result = replace_head(world, &head, data.as_deref(), &terrain, target);
    if let Err(error) = result {
        world.resource_mut::<State>().error = Some(error.clone());
        crate::authoring_viewport::report(format!("Affichage de la carte incomplet : {error}"));
    }
}

fn replace_head(
    world: &mut World,
    head: &ZoneAuthoringHead,
    data: Option<&Acquired>,
    terrain: &TerrainDocument,
    target: ContentHash,
) -> Result<(), String> {
    world.resource_scope(|world, mut state: Mut<State>| -> Result<(), String> {
        let scope = resources::scope().ok_or("Origine de la carte absente")?;
        if scope.scene() != state.scope {
            return Err("Les ressources ont changé : sauvegardez puis rechargez l'atelier.".into());
        }
        let next_plants = if state.families.imported {
            let owner =
                EntityId::deterministic(format!("studio-imported:{}:{}", scope.module, scope.area));
            Some(vegetation(
                head,
                data.ok_or("Ressources végétales absentes")?,
                owner,
            )?)
        } else {
            None
        };
        let next_terrain = if state.terrain_owner.is_some() {
            let expected = world
                .resource::<terrain_host::Status>()
                .document
                .ok_or("Le terrain courant n'est pas identifié")?;
            let next = terrain.content_hash().map_err(|error| error.to_string())?;
            if expected == next {
                None
            } else {
                Some((
                    terrain_input(world, terrain, data)?
                        .ok_or("Les ressources du terrain ont disparu")?,
                    expected,
                ))
            }
        } else {
            None
        };
        let next_water = if state.families.water && next_terrain.is_some() {
            let expected = world
                .resource::<forge_engine::source_water_host::Status>()
                .document
                .ok_or("L’océan courant n’est pas identifié")?;
            let prepared = water::prepare(world, terrain, data)?
                .ok_or("Les ressources d’océan ont disparu")?;
            prepared.validate_replacement(world, expected)?;
            Some((prepared, expected))
        } else {
            None
        };
        update_view(world, &mut state, data)?;
        if let Some(prepared) = next_plants {
            prepared.replace(world, state.head)?;
        } else if state.families.environment {
            let raw = crate::authoring_raster::source_bytes(terrain)?;
            let plan =
                forge_world::sampled_terrain::SampledTerrainPlan::from_document(terrain, &raw)
                    .map_err(|e| e.to_string())?;
            surfaces::replace_scene(
                world,
                forge_engine::source_lighting::surfaces::scene::Prepared::new(&plan)?,
            )?;
        }
        if let Some((prepared, expected)) = next_terrain {
            prepared.replace(world, expected)?;
        }
        if let Some((prepared, expected)) = next_water {
            prepared.replace(world, expected)?;
        }
        forge_engine::source_wind::SceneWind::publish(world, head.corrections.wind)?;
        state.head = target;
        state.pending = None;
        Ok(())
    })
}

pub(crate) fn publish(world: &mut World) {
    world.resource_scope(|world, mut state: Mut<State>| publish_state(world, &mut state));
}

fn publish_state(world: &mut World, state: &mut State) {
    let water_ready = if state.families.water {
        state
            .terrain_owner
            .ok_or("L’océan ne possède pas son relief courant".to_owned())
            .and_then(|owner| {
                forge_engine::source_water_host::readiness::ready(
                    world,
                    EntityId::deterministic(format!("studio-water:{owner}")),
                )
            })
    } else {
        Ok(true)
    };
    let terrain_ready = terrain_readiness(world, state);
    let error = state
        .error
        .clone()
        .or_else(|| water_ready.as_ref().err().cloned())
        .or_else(|| terrain_ready.as_ref().err().cloned())
        .or_else(|| {
            world
                .get_resource::<surfaces::Error>()
                .and_then(|error| error.0.clone())
        })
        .or_else(|| {
            world
                .get_resource::<environment::Status>()
                .and_then(|status| status.error.clone())
        })
        .or_else(|| {
            world
                .get_resource::<objects::Status>()
                .and_then(|status| status.error.clone())
        })
        .or_else(|| {
            world
                .get_resource::<fish::Status>()
                .and_then(|status| status.error.clone())
        })
        .or_else(|| {
            world
                .get_resource::<plants::HostStatus>()
                .and_then(|status| status.error.clone())
        })
        .or_else(|| {
            world
                .get_resource::<lights::Status>()
                .and_then(|status| status.error.clone())
        });
    let sources_current = resources::available()
        && resources::scope()
            .is_some_and(|scope| scope.head == state.head && scope.scene() == state.scope);
    let ready = sources_current
        && water_ready.unwrap_or(false)
        && terrain_ready.unwrap_or(false)
        && state.pending.is_none()
        && state.view_pending.is_none()
        && state.view_revision == requested::requested_revision()
        && (!state.families.environment
            || world
                .get_resource::<environment::Status>()
                .is_some_and(|status| status.ready))
        && (!state.families.objects
            || world
                .get_resource::<objects::Status>()
                .is_some_and(|status| status.synchronized))
        && (!state.families.fish
            || world
                .get_resource::<fish::Status>()
                .is_some_and(|status| status.synchronized))
        && (!state.families.imported
            || world
                .get_resource::<plants::HostStatus>()
                .is_some_and(|status| status.ready && status.head == Some(state.head)))
        && world.resource::<lights::Status>().error.is_none()
        && (world.resource::<lights::Status>().expected_emitters == 0
            || world.resource::<lights::Status>().ready);
    READY.with(|slot| {
        *slot.borrow_mut() = error.map_or_else(
            || Ok(ready.then_some((state.head, state.view_revision))),
            Err,
        );
    });
    if state.families.fish {
        for (root, id, transform) in fish::authored_roots(world) {
            let changed = world
                .get::<crate::authoring_assets::placements::gizmo::PlacementRoot>(root)
                .is_none_or(|current| current.0 != id || current.1 != transform);
            if changed {
                world.entity_mut(root).insert(
                    crate::authoring_assets::placements::gizmo::PlacementRoot(id, transform),
                );
            }
        }
    }
    crate::authoring_assets::imported_scene::focus_current(world);
}

fn terrain_readiness(world: &mut World, state: &State) -> Result<bool, String> {
    let Some(owner) = state.terrain_owner else {
        return Ok(true);
    };
    let status = world
        .get_resource::<terrain_host::Status>()
        .ok_or("Le terrain n'est pas initialisé")?;
    if let Some(error) = &status.error {
        return Err(error.clone());
    }
    if status.pending
        || !status.normals_ready
        || state.pending.is_some()
        || state.view_pending.is_some()
    {
        return Ok(false);
    }
    let identity = forge_core::canonical_json_sha256(&(state.head, state.view_revision))
        .map_err(|error| error.to_string())?;
    let generation = terrain_host::request_inventory(world, owner, identity)?;
    let token = forge_engine::render_inventory::acquire_scoped(world, owner, identity, generation);
    Ok(terrain_host::ready(world, token.as_ref()))
}
