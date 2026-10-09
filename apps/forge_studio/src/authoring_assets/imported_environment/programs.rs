//! Prepare library programs on this scene's device, with generation-safe adoption.
use super::imported_buildings;
use bevy::{ecs as bevy_ecs, prelude::*, render::renderer::RenderDevice};
use forge_engine::{
    tidewater_environment::{
        live,
        programs::{Input, Prepared},
    },
    tidewater_scene_environment::ResidentEnvironment,
};
use std::sync::{Arc, Mutex, atomic::AtomicBool};

#[derive(Resource)]
struct Preparation {
    library: u64,
    generation: Arc<AtomicBool>,
    input: Option<Input>,
    completed: Arc<Mutex<Option<Result<Prepared, String>>>>,
}

#[derive(Resource)]
struct ActivePrograms {
    library: u64,
    generation: Arc<AtomicBool>,
    reported_failure: bool,
}

pub(super) fn request(world: &mut World) {
    // Replacing this resource abandons the old completion slot, including in-flight work.
    world.remove_resource::<Preparation>();
    world.remove_resource::<ActivePrograms>();
    live::clear(world);
    let Some((library, package)) = imported_buildings::environment_programs() else {
        return;
    };
    let result = Input::from_package(&package);
    match result {
        Ok(input) => {
            let generation = world.resource::<ResidentEnvironment>().clouds.ready.clone();
            world.insert_resource(Preparation {
                library,
                generation,
                input: Some(input),
                completed: Arc::new(Mutex::new(None)),
            });
        }
        Err(error) => imported_buildings::render_status(format!(
            "Programmes d'environnement invalides : {error}"
        )),
    }
}

pub(super) fn update(world: &mut World) {
    if let Some(active) = world.get_resource::<ActivePrograms>() {
        let current = imported_buildings::environment_programs()
            .is_some_and(|(ticket, _)| ticket == active.library)
            && world
                .get_resource::<ResidentEnvironment>()
                .is_some_and(|r| Arc::ptr_eq(&r.clouds.ready, &active.generation));
        if !current {
            live::clear(world);
            world.remove_resource::<ActivePrograms>();
        }
    }
    if let Some(error) = live::failure(world)
        && let Some(mut active) = world.get_resource_mut::<ActivePrograms>()
        && !active.reported_failure
    {
        active.reported_failure = true;
        imported_buildings::render_status(format!("Calcul de l'environnement suspendu : {error}"));
    }
    let Some(state) = world.get_resource::<Preparation>() else {
        return;
    };
    let current = imported_buildings::environment_programs()
        .is_some_and(|(ticket, _)| ticket == state.library)
        && world
            .get_resource::<ResidentEnvironment>()
            .is_some_and(|r| Arc::ptr_eq(&r.clouds.ready, &state.generation));
    if !current {
        world.remove_resource::<Preparation>();
        return;
    }
    // RenderPlugin may not have installed its device when the scene is first staged.
    if let Some(device) = world.get_resource::<RenderDevice>().cloned() {
        let mut state = world.resource_mut::<Preparation>();
        if let Some(input) = state.input.take() {
            let completed = state.completed.clone();
            wasm_bindgen_futures::spawn_local(async move {
                let result = input.prepare(&device).await;
                if let Ok(mut slot) = completed.lock() {
                    *slot = Some(result);
                }
            });
        }
    }
    let result = world
        .resource::<Preparation>()
        .completed
        .lock()
        .ok()
        .and_then(|mut slot| slot.take());
    let Some(result) = result else {
        return;
    };
    let state = world
        .remove_resource::<Preparation>()
        .expect("preparation present");
    let result = result.and_then(|programs| {
        let resident = world.resource::<ResidentEnvironment>();
        let (targets, atmosphere, clouds) = (
            resident.environment_targets.clone(),
            resident.atmosphere.clone(),
            resident.clouds.clone(),
        );
        live::stage(world, programs, targets, atmosphere, clouds)?;
        if let Err(error) = live::bind_lighting(
            world,
            forge_engine::tidewater_material::scene_buffer::HANDLE,
        ) {
            live::clear(world);
            return Err(error);
        }
        world.insert_resource(ActivePrograms {
            library: state.library,
            generation: state.generation,
            reported_failure: false,
        });
        Ok(())
    });
    if let Err(error) = result {
        imported_buildings::render_status(format!(
            "Activation de l'environnement impossible : {error}"
        ));
    }
}
