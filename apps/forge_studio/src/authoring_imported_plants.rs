//! Imported population in the terrain canvas; picking and plant shadows are separate.
use crate::{authoring_assets::imported_buildings, authoring_imported_population as population};
use bevy::{ecs as bevy_ecs, prelude::*};
use forge_engine::tidewater_plants::{PlantMaterial, grass, picking, scene};

mod lighting;
pub(crate) mod selection;
#[derive(Resource, Default)]
struct State {
    source: Option<(forge_core::ContentHash, u64)>,
    resident: Option<scene::resident::Resident>,
    shadow: Option<scene::shadow::Source>,
    shadow_selection: Option<(Vec4, Vec<scene::shadow::SelectionObservation>)>,
    grass: Option<GrassState>,
    camera: Option<[u32; 3]>,
    materials: Vec<Handle<PlantMaterial>>,
    lighting: Option<lighting::Key>,
    lighting_refreshes: u64,
    failed: Option<(forge_core::ContentHash, u64)>,
}

struct GrassState {
    grid: grass::Grid,
    resident: grass::Resident,
    view: Option<Vec<u32>>,
}

pub(crate) fn install(app: &mut App) -> Result<(), String> {
    let package =
        imported_buildings::building_package().ok_or("Bibliothèque de bâtiments absente")?;
    let village = std::str::from_utf8(
        package
            .file("village.wgsl")
            .ok_or("Shader bâtiment absent")?,
    )
    .map_err(|error| error.to_string())?;
    picking::render::install(app);
    picking::occluders::install(app, village)?;
    selection::install(app);
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
    let key = (expected_head, imported_buildings::published_generation());
    let Some(head) = population::current_head() else {
        return false;
    };
    head.content_hash() == Ok(expected_head)
        && population::population(&head).is_ok()
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
    base: &'a PlantMaterial,
}

fn update_projection(
    world: &mut World,
    state: &mut State,
    head: &forge_project::zone_authoring::ZoneAuthoringHead,
    key: (forge_core::ContentHash, u64),
    document: &forge_project::TerrainDocument,
    view: ProjectionView<'_>,
) -> Result<(), String> {
    let ProjectionView {
        camera_entity,
        camera,
        light_key,
        base,
    } = view;
    if state.source != Some(key) {
        let source = population::population(head)?;
        let package = imported_buildings::plant_package().ok_or("Bibliothèque végétale absente")?;
        let heights = crate::authoring_raster::source_bytes(document)?;
        let prepared =
            scene::from_population(&package, &source, head, &heights).map_err(|e| e.to_string())?;
        let shadow = scene::shadow::Source::from_vegetation(&prepared, &package)?;
        let prepared_grass = grass::Prepared::from_package(&package)?;
        retire(world, state);
        let resident = scene::resident::Resident::stage(world, prepared, base)?;
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
    let view = camera.to_array().map(f32::to_bits);
    if state.camera != Some(view) {
        state
            .resident
            .as_mut()
            .ok_or("Scène végétale absente")?
            .select(
                world,
                scene::View {
                    camera,
                    visibility: Visibility::Visible,
                },
            )?;
        crate::authoring_vegetation::report(
            "Plantes importées projetées. Sélection GPU disponible dans Nature ; parcours GUI à qualifier."
                .into(),
        );
        state.camera = Some(view);
        // Selection retains material handles. Camera translation does not
        // invalidate lighting; the light key and retire handle real changes.
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
        let view = crate::authoring_imported_terrain::camera_view(&transform, projection)?;
        let cells = grass.grid.select(view)?;
        let report = grass.resident.update(world, &cells, Visibility::Visible)?;
        grass.view = Some(grass_view);
        crate::authoring_vegetation::report(format!(
            "Plantes importées projetées · herbe source {} / {} / {} cellules actives.",
            report.counts[0], report.counts[1], report.counts[2]
        ));
    }
    if state.lighting.as_ref() != Some(&light_key) {
        let mut assets = world.resource_mut::<Assets<PlantMaterial>>();
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
) -> Option<Result<forge_engine::tidewater_object_shadow::plants::Input, String>> {
    world
        .get_resource::<State>()?
        .shadow
        .as_ref()
        .map(|source| source.at(camera))
}

pub(crate) fn shadow_streams(
    world: &mut World,
    camera: Vec4,
) -> Option<Result<Vec<forge_engine::tidewater_object_shadow::plants::Stream>, String>> {
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
        let Some(head) = population::current_head() else {
            retire(world, &mut state);
            state.source = None;
            state.failed = None;
            return;
        };
        let Ok(hash) = head.content_hash() else {
            return;
        };
        let key = (hash, imported_buildings::published_generation());
        if state.source.is_some_and(|source| source != key) {
            retire(world, &mut state);
            state.source = None;
        }
        if state.failed == Some(key) {
            return;
        }
        let Some((document, camera_entity)) =
            crate::authoring_imported_terrain::shadow_source(world)
        else {
            return;
        };
        let Ok((effective, _)) = head.effective() else {
            return;
        };
        if &effective != document {
            return;
        }
        let document = document.clone();
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
            } else if population::population(&head).is_ok()
                && imported_buildings::plant_package().is_some()
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
