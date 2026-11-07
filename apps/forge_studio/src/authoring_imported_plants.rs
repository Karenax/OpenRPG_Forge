//! Imported population in the terrain canvas; picking and plant shadows are separate.
use crate::{authoring_assets::imported_buildings, authoring_imported_population as population};
use bevy::{ecs as bevy_ecs, prelude::*};
use forge_engine::source_vegetation::captured::CapturedPlantMaterial;
use forge_engine::source_vegetation::grass;
use forge_engine::source_vegetation::picking;

mod lighting;
pub(crate) mod selection;
#[derive(Resource, Default)]
struct State {
    source: Option<(forge_core::ContentHash, u64, u64)>,
    resident:
        Option<forge_engine::source_vegetation::scene::resident::Resident<CapturedPlantMaterial>>,
    shadow: Option<forge_engine::source_vegetation::scene::shadow::Source>,
    shadow_selection: Option<(
        Vec4,
        Vec<forge_engine::source_vegetation::scene::shadow::SelectionObservation>,
    )>,
    grass: Option<GrassState>,
    camera: Option<[u32; 3]>,
    materials: Vec<Handle<CapturedPlantMaterial>>,
    lighting: Option<lighting::Key>,
    lighting_refreshes: u64,
    failed: Option<(forge_core::ContentHash, u64, u64)>,
    species: Option<forge_engine::ImportedTreePresentation>,
    species_committed: bool,
}

struct GrassState {
    grid: grass::Grid,
    resident: grass::Resident,
    view: Option<Vec<u32>>,
}

pub(crate) fn install(app: &mut App) -> Result<(), String> {
    let package = imported_buildings::building_package();
    let program = package.as_ref().map_or(
        Ok(forge_engine::source_surface::PROFILE_PROGRAM),
        |package| forge_engine::source_surface::package::shader_source(package),
    )?;
    picking::render::install(app);
    picking::governed::install(app)?;
    app.configure_sets(PostUpdate, picking::governed::SyncSet.after(update));
    picking::occluders::install::<
        forge_engine::source_terrain::lod::LodMaterial,
        forge_engine::source_surface::SourceSurfaceMaterial,
    >(app, program)?;
    selection::install(app);
    if !app.is_plugin_added::<forge_engine::render_inventory::RenderInventoryPlugin>() {
        app.add_plugins(forge_engine::render_inventory::RenderInventoryPlugin);
    }
    app.init_resource::<State>().add_systems(
        PostUpdate,
        update.after(crate::authoring_imported_terrain::project),
    );
    Ok(())
}

/// CPU/source barrier for the exact imported population shown in the viewport.
/// A presented GPU frame remains a separate qualification.
pub(crate) fn ready(world: &World, expected_head: forge_core::ContentHash) -> bool {
    let Some(state) = world.get_resource::<State>() else {
        return false;
    };
    let key = (
        expected_head,
        imported_buildings::published_generation(),
        crate::authoring_module_resources::revision(),
    );
    // Hidden candidate resources alone do not establish visible replacement.
    let selected_species = population::current_head().is_some_and(|head| {
        head.corrections
            .species
            .as_ref()
            .is_some_and(|species| !species.is_empty())
    });
    (!selected_species
        || state.species.as_ref().is_some_and(|species| {
            state.species_committed
                && species.is_visible()
                && species.gpu_ready(world)
                && species.source_shadows_ready(world)
        }))
        && imported_buildings::plant_resources_ready(world)
        && population::ready_head_hash() == Some(expected_head)
        && state.source == Some(key)
        && state.failed != Some(key)
        && state.resident.is_some()
        && state
            .grass
            .as_ref()
            .is_some_and(|grass| grass.view.is_some())
        && state.camera.is_some()
}

fn retire(world: &mut World, state: &mut State) {
    if let Some(species) = state.species.take() {
        species.retire(world);
    }
    state.species_committed = false;
    state.shadow = None;
    state.shadow_selection = None;
    if let Some(grass) = state.grass.take() {
        grass.resident.retire(world);
    }
    if let Some(resident) = state.resident.take() {
        resident.retire(world);
    }
    state.materials.clear();
    state.camera = None;
    state.lighting = None;
}

struct ProjectionView<'a> {
    camera_entity: Entity,
    camera: Vec3,
    light_key: lighting::Key,
    base: &'a CapturedPlantMaterial,
}

fn prepare_projection(
    world: &mut World,
    state: &mut State,
    head: &forge_project::zone_authoring::ZoneAuthoringHead,
    key: (forge_core::ContentHash, u64, u64),
    document: &forge_project::TerrainDocument,
    base: &CapturedPlantMaterial,
    camera: Vec3,
) -> Result<(), String> {
    if state.source != Some(key) {
        let source = population::current_population(key.0)?;
        let package = imported_buildings::authored_plant_package()
            .ok_or("Ressources végétales de la carte absentes")?;
        let heights = crate::authoring_raster::source_bytes(document)?;
        let prepared = forge_engine::source_vegetation::scene::from_population(
            &package, &source, head, &heights,
        )?;
        let shadow = forge_engine::source_vegetation::scene::shadow::Source::from_vegetation(
            &prepared, &package,
        )?;
        let prepared_grass = grass::Prepared::from_package(&package)?;
        retire(world, state);
        let resident = forge_engine::source_vegetation::scene::resident::Resident::stage(
            world, prepared, base,
        )?;
        state.materials = resident.materials();
        state.resident = Some(resident);
        state.shadow = Some(shadow);
        let grass_resident = grass::Resident::stage(world, base, prepared_grass.meshes)?;
        state.materials.extend(grass_resident.materials());
        state.grass = Some(GrassState {
            grid: prepared_grass.grid,
            resident: grass_resident,
            view: None,
        });
        state.source = Some(key);
    }
    if state.species.is_none()
        && head
            .corrections
            .species
            .as_ref()
            .is_some_and(|species| !species.is_empty())
        && crate::authoring_species::ready(world)?
    {
        let source = population::current_population(key.0)?;
        let heights = crate::authoring_raster::source_bytes(document)?;
        let authored = source.terrain_view(head, &heights)?;
        let (_, vegetation) = head.effective()?;
        let vegetation = vegetation.ok_or("Végétation des espèces absente")?;
        let cache = world.resource::<forge_engine::VegetationPrototypeWindCache>();
        let catalog = cache
            .selection_catalog
            .as_ref()
            .ok_or("Catalogue d'espèces absent")?;
        let bindings = forge_engine::bind_imported_trees(&authored, key.0, &vegetation, catalog)?;
        let mut candidate = forge_engine::ImportedTreePresentation::prepare(
            forge_core::EntityId::deterministic(format!(
                "studio-imported-species:{}:{}",
                document.project_id, document.area_id
            )),
            key.0,
            &bindings,
            cache,
            &vegetation,
            world.resource::<Assets<Mesh>>(),
        )?;
        let selected = candidate.staging_selection();
        candidate.stage_hidden(world, key.0, &selected)?;
        if let Err(error) = candidate.request_gpu(world) {
            candidate.retire(world);
            return Err(error);
        }
        state.species = Some(candidate);
        crate::authoring_vegetation::report(
            "Préparation des modèles d’espèces et de leurs ombres…".into(),
        );
    }
    if let Some(species) = state.species.as_mut()
        && !state.species_committed
    {
        species.publish_source_shadows(world, key.0, camera)?;
        let ids = species.replacement_identities();
        let shadow = state
            .shadow
            .as_mut()
            .ok_or("Ombres de la végétation absentes")?;
        shadow.validate_replacement(key.0, &ids)?;
        if species.commit_source_replacement(
            world,
            state.resident.as_mut().ok_or("Scène végétale absente")?,
            key.0,
            forge_engine::source_vegetation::scene::View {
                camera,
                visibility: Visibility::Visible,
            },
        )? {
            shadow.replace_instances(key.0, ids)?;
            state.species_committed = true;
            state.camera = None;
            crate::authoring_vegetation::report(
                "Espèces appliquées aux arbres importés ; positions et retouches conservées."
                    .into(),
            );
        }
    }
    Ok(())
}

fn update_projection(
    world: &mut World,
    state: &mut State,
    head: &forge_project::zone_authoring::ZoneAuthoringHead,
    key: (forge_core::ContentHash, u64, u64),
    document: &forge_project::TerrainDocument,
    view: ProjectionView<'_>,
) -> Result<(), String> {
    let ProjectionView {
        camera_entity,
        camera,
        light_key,
        base,
    } = view;
    prepare_projection(world, state, head, key, document, base, camera)?;
    let view = camera.to_array().map(f32::to_bits);
    if state.camera != Some(view) {
        let resident = state.resident.as_mut().ok_or("Scène végétale absente")?;
        let source_view = forge_engine::source_vegetation::scene::View {
            camera,
            visibility: Visibility::Visible,
        };
        if state.species_committed {
            let species = state.species.as_mut().ok_or("Modèles d’espèces absents")?;
            let selected = species.selection_for_camera(camera)?;
            species.select(world, key.0, &selected)?;
            resident.select_excluding(world, source_view, &species.replacement_identities())?;
        } else {
            resident.select(world, source_view)?;
        }
        crate::authoring_vegetation::report(
            "Plantes importées projetées. Sélection GPU disponible dans Nature ; parcours GUI à qualifier."
                .into(),
        );
        state.camera = Some(view);
        // Selection retains material handles. Camera translation does not
        // invalidate lighting; the light key and retire handle real changes.
    }
    if let Some(species) = &state.species {
        species.publish_source_shadows(world, key.0, camera)?;
    }
    let transform = *world
        .get::<Transform>(camera_entity)
        .ok_or("Caméra végétale absente")?;
    let Projection::Perspective(projection) = world
        .get::<Projection>(camera_entity)
        .ok_or("Perspective végétale absente")?
    else {
        return Err("Perspective végétale invalide".into());
    };
    let grass_view: Vec<u32> = transform
        .to_matrix()
        .to_cols_array()
        .into_iter()
        .chain([
            projection.fov,
            projection.aspect_ratio,
            projection.near,
            projection.far,
        ])
        .map(f32::to_bits)
        .collect();
    if let Some(grass) = state.grass.as_mut()
        && grass.view.as_ref() != Some(&grass_view)
    {
        let view = forge_engine::scene_camera::source_view(&transform, projection)?;
        let cells = grass.grid.select(view)?;
        let report = grass.resident.update(world, &cells, Visibility::Visible)?;
        grass.view = Some(grass_view);
        crate::authoring_vegetation::report(format!(
            "Plantes importées projetées · herbe source {} / {} / {} cellules actives.",
            report.counts[0], report.counts[1], report.counts[2]
        ));
    }
    if state.lighting.as_ref() != Some(&light_key) {
        let mut assets = world.resource_mut::<Assets<CapturedPlantMaterial>>();
        for handle in &state.materials {
            if let Some(mut material) = assets.get_mut(handle) {
                lighting::refresh(&mut material, base);
            }
        }
        state.lighting = Some(light_key);
        state.lighting_refreshes += 1;
    }
    Ok(())
}

pub(crate) fn lighting_refresh_count(world: &World) -> u64 {
    world
        .get_resource::<State>()
        .map_or(0, |state| state.lighting_refreshes)
}

pub(crate) fn shadow_head(world: &World) -> Option<forge_core::ContentHash> {
    let state = world.get_resource::<State>()?;
    state
        .shadow
        .as_ref()
        .and(state.source.map(|source| source.0))
}

pub(crate) fn shadow_input(
    world: &World,
    camera: Vec4,
) -> Option<Result<forge_engine::source_shadows::casters::plants::Input, String>> {
    world
        .get_resource::<State>()?
        .shadow
        .as_ref()
        .map(|source| source.at(camera))
}

pub(crate) fn shadow_streams(
    world: &mut World,
    camera: Vec4,
) -> Option<Result<Vec<forge_engine::source_shadows::casters::plants::Stream>, String>> {
    let mut state = world.get_resource_mut::<State>()?;
    let selected = state.shadow.as_ref()?.select_observed(camera);
    Some(selected.map(|(streams, observation)| {
        state.shadow_selection = Some((camera, observation));
        streams
    }))
}

pub(crate) fn shadow_selection_observation(world: &World) -> Option<serde_json::Value> {
    let state = world.get_resource::<State>()?;
    let (camera, groups) = state.shadow_selection.as_ref()?;
    Some(serde_json::json!({
        "scope": "CPU streams selected for shadow animation upload; not GPU depth coverage",
        "source": state.source,
        "camera": camera.to_array(),
        "groups": groups,
    }))
}

pub(crate) fn update(world: &mut World) {
    world.resource_scope(|world, mut state: Mut<State>| {
        let Some((hash, head, effective)) = population::frame_source() else {
            retire(world, &mut state);
            state.source = None;
            state.failed = None;
            return;
        };
        let key = (
            hash,
            imported_buildings::published_generation(),
            crate::authoring_module_resources::revision(),
        );
        if state.source.is_some_and(|source| source != key) {
            retire(world, &mut state);
            state.source = None;
        }
        if state.failed == Some(key) {
            return;
        }
        if !imported_buildings::plant_resources_ready(world) {
            retire(world, &mut state);
            state.source = None;
            return;
        }
        let Some((document, camera_entity)) =
            crate::authoring_imported_terrain::shadow_source(world)
        else {
            return;
        };
        if effective.as_ref() != document {
            return;
        }
        let document = effective;
        let Some(pose) = world.get::<Transform>(camera_entity) else {
            return;
        };
        let camera = pose.translation;
        let Some((light_key, base)) = lighting::material(world) else {
            return;
        };
        let result = update_projection(
            world,
            &mut state,
            &head,
            key,
            &document,
            ProjectionView {
                camera_entity,
                camera,
                light_key,
                base: &base,
            },
        );
        if let Err(error) = result {
            // A camera/projection failure must not retire an otherwise valid population.
            if state.source == Some(key) {
                crate::authoring_vegetation::report(format!(
                    "Vue végétale momentanément indisponible : {error}"
                ));
            } else if population::current_population(hash).is_ok()
                && imported_buildings::authored_plant_package().is_some()
            {
                state.failed = Some(key);
                retire(world, &mut state);
                crate::authoring_vegetation::report(format!(
                    "Projection végétale indisponible : {error}"
                ));
            }
        }
    });
}
