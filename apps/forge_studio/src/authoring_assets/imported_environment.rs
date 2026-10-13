//! GPU preparation of a verified library; authored scene state remains authoritative.
mod programs;
use super::imported_buildings;
use bevy::{ecs as bevy_ecs, prelude::*};
use forge_engine::tidewater_scene_environment::ResidentEnvironment;
use std::{cell::Cell, sync::atomic::Ordering};
use wasm_bindgen::JsCast;

thread_local! {
    static PREVIEW_REQUEST: Cell<bool> = const { Cell::new(false) };
    static PREVIEW_EPOCH: Cell<u64> = const { Cell::new(0) };
}

pub(super) fn preview() {
    PREVIEW_REQUEST.with(|r| r.set(true));
}

#[derive(Resource)]
struct Preparing;

#[derive(Resource)]
struct PreviewGeneration(std::sync::Arc<std::sync::atomic::AtomicBool>, u64);

pub(crate) fn install(app: &mut App) -> Result<(), String> {
    forge_engine::tidewater_atmosphere::register(app)?;
    forge_engine::tidewater_clouds::register(app)?;
    forge_engine::tidewater_environment::live::register(app)?;
    imported_buildings::restore_environment()?;
    imported_buildings::restore_buildings()?;
    imported_buildings::restore_terrain()?;
    imported_buildings::restore_plants()?;
    imported_buildings::restore_fish_stand()?;
    app.add_plugins(MaterialPlugin::<
        forge_engine::tidewater_plants::PlantMaterial,
    >::default());
    forge_engine::tidewater_sky::preview::install(app);
    super::imported_scene::install(app);
    super::fish_stand_scene::install(app)?;
    super::fish_props_scene::install(app);
    app.add_systems(Update, update);
    Ok(())
}

fn stage_plants(world: &mut World, update: imported_buildings::PlantUpdate) {
    use forge_engine::tidewater_plants::{self, textures::ResidentTextures};
    let imported_buildings::PlantUpdate::Replace(prepared) = update else {
        world.remove_resource::<ResidentTextures>();
        return;
    };
    let result = (|| -> Result<(), String> {
        let package = imported_buildings::plant_package().ok_or("Paquet végétal absent")?;
        let shader =
            std::str::from_utf8(package.file("plant.wgsl").ok_or("Shader végétal absent")?)
                .map_err(|e| e.to_string())?;
        tidewater_plants::stage_shader(world, shader)?;
        tidewater_plants::picking::stage_shader(world, shader)?;
        let resident = prepared.stage(&mut world.resource_mut::<Assets<Image>>());
        world.insert_resource(resident);
        Ok(())
    })();
    if let Err(error) = result {
        world.remove_resource::<ResidentTextures>();
        imported_buildings::render_status(format!("Préparation végétale impossible : {error}"));
    }
}

fn update(world: &mut World) {
    let editing = world.contains_resource::<crate::authoring_render::FastEditing>();
    if let Some(update) = imported_buildings::take_plants() {
        stage_plants(world, update);
    }
    if let Some(terrain) = imported_buildings::take_terrain() {
        if let imported_buildings::TerrainUpdate::Replace(prepared) = terrain {
            let resident = prepared.stage(&mut world.resource_mut::<Assets<Image>>());
            world.insert_resource(resident);
        } else {
            world.remove_resource::<forge_engine::tidewater_terrain::textures::ResidentSurface>();
        }
    }
    if let Some((geometry, library)) = imported_buildings::take_buildings() {
        let meshes = geometry.stage(&mut world.resource_mut::<Assets<Mesh>>());
        let textures = library.stage(&mut world.resource_mut::<Assets<Image>>());
        world.insert_resource(meshes);
        world.insert_resource(textures);
    }
    if let Some(prepared) = imported_buildings::take_environment() {
        match prepared.stage(world) {
            Ok(resident) => {
                hide_preview();
                world.insert_resource(resident);
                if editing {
                    imported_buildings::render_status("Vue d’édition : éclairage fixe issu des assets vérifiés. Ciel et nuages animés disponibles dans le jeu séparé.".into());
                } else {
                    programs::request(world);
                    world.insert_resource(Preparing);
                    imported_buildings::render_status("Calcul de l'environnement en cours.".into());
                }
            }
            Err(error) => {
                world.remove_resource::<Preparing>();
                imported_buildings::render_status(format!(
                    "Préparation de l'environnement impossible : {error}"
                ));
            }
        }
    }
    if world.contains_resource::<Preparing>()
        && world
            .get_resource::<ResidentEnvironment>()
            .is_some_and(|r| r.clouds.ready.load(Ordering::Acquire))
    {
        world.remove_resource::<Preparing>();
        imported_buildings::render_status(
            "Calcul de l'environnement terminé. Vous pouvez afficher un aperçu du ciel de référence."
                .into(),
        );
    }
    if editing {
        if PREVIEW_REQUEST.with(|r| r.replace(false)) {
            imported_buildings::render_status("Le ciel animé est disponible dans la fenêtre du jeu ; la vue d’édition utilise un éclairage fixe.".into());
        }
    } else {
        programs::update(world);
        update_preview(world);
    }
}

fn update_preview(world: &mut World) {
    use forge_engine::tidewater_sky::preview;
    if PREVIEW_REQUEST.with(|r| r.replace(false)) {
        match preview::request(world) {
            Ok(()) => {
                let generation = world.resource::<ResidentEnvironment>().clouds.ready.clone();
                world.insert_resource(PreviewGeneration(generation, PREVIEW_EPOCH.with(Cell::get)));
                imported_buildings::render_status("Calcul de l'aperçu du ciel en cours.".into());
            }
            Err(error) => {
                imported_buildings::render_status(format!("Aperçu indisponible : {error}"));
            }
        }
    }
    if let Some(preview::Snapshot(result)) = world.remove_resource::<preview::Snapshot>() {
        let current = world
            .remove_resource::<PreviewGeneration>()
            .is_some_and(|generation| {
                generation.1 == PREVIEW_EPOCH.with(Cell::get)
                    && world
                        .get_resource::<ResidentEnvironment>()
                        .is_some_and(|resident| {
                            std::sync::Arc::ptr_eq(&generation.0, &resident.clouds.ready)
                        })
            });
        if !current {
            imported_buildings::render_status("Environnement remplacé : relancez l'aperçu.".into());
            return;
        }
        let result = result.and_then(|bytes| paint(&bytes));
        imported_buildings::render_status(match result {
            Ok(()) => {
                "Aperçu du ciel de référence, vue et heure fixes. La carte n'a pas été modifiée."
                    .into()
            }
            Err(error) => format!("Aperçu indisponible : {error}"),
        });
    }
}

fn paint(bytes: &[u8]) -> Result<(), String> {
    use forge_engine::tidewater_sky::preview::{HEIGHT, WIDTH};
    let canvas = web_sys::window()
        .and_then(|w| w.document())
        .and_then(|d| d.get_element_by_id("forge-imported-sky-preview"))
        .ok_or("Preview canvas is not mounted")?
        .dyn_into::<web_sys::HtmlCanvasElement>()
        .map_err(|_| "Invalid preview canvas")?;
    let context = canvas
        .get_context("2d")
        .map_err(|_| "Canvas context failed")?
        .ok_or("Canvas context is absent")?
        .dyn_into::<web_sys::CanvasRenderingContext2d>()
        .map_err(|_| "Invalid canvas context")?;
    let pixels = web_sys::ImageData::new_with_u8_clamped_array_and_sh(
        wasm_bindgen::Clamped(bytes),
        WIDTH,
        HEIGHT,
    )
    .map_err(|_| "Invalid preview pixels")?;
    context
        .put_image_data(&pixels, 0.0, 0.0)
        .map_err(|_| "Could not display preview")?;
    canvas.set_hidden(false);
    Ok(())
}

pub(super) fn hide_preview() {
    PREVIEW_EPOCH.with(|epoch| epoch.set(epoch.get().wrapping_add(1)));
    if let Some(canvas) = web_sys::window()
        .and_then(|w| w.document())
        .and_then(|d| d.get_element_by_id("forge-imported-sky-preview"))
        .and_then(|e| e.dyn_into::<web_sys::HtmlCanvasElement>().ok())
    {
        canvas.set_hidden(true);
    }
}
