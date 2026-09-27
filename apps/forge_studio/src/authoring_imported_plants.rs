//! Imported population in the terrain canvas; picking and plant shadows are separate.
use crate::{authoring_assets::imported_buildings, authoring_imported_population as population};
use bevy::{ecs as bevy_ecs, prelude::*};
use forge_engine::tidewater_plants::{PlantMaterial, scene};

mod lighting;
#[derive(Resource, Default)]
struct State {
    source: Option<(forge_core::ContentHash, u64)>,
    resident: Option<scene::resident::Resident>,
    camera: Option<[u32; 3]>,
    materials: Vec<Handle<PlantMaterial>>,
    lighting: Option<lighting::Key>,
    failed: Option<(forge_core::ContentHash, u64)>,
}

pub(crate) fn install(app: &mut App) {
    app.init_resource::<State>().add_systems(
        PostUpdate,
        update.after(crate::authoring_imported_terrain::project),
    );
}

fn retire(world: &mut World, state: &mut State) {
    if let Some(resident) = state.resident.take() {
        resident.retire(world);
    }
    state.materials.clear();
    state.camera = None;
    state.lighting = None;
}

fn update(world: &mut World) {
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
        let Some((document, camera)) = crate::authoring_imported_terrain::shadow_source(world)
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
        let Some(pose) = world.get::<Transform>(camera) else {
            return;
        };
        let camera = pose.translation;
        let Some((light_key, base)) = lighting::material(world) else {
            return;
        };
        let result = (|| -> Result<(), String> {
            if state.source != Some(key) {
                let source = population::population(&head)?;
                let package =
                    imported_buildings::plant_package().ok_or("Bibliothèque végétale absente")?;
                let heights = crate::authoring_raster::source_bytes(&document)?;
                let prepared = scene::from_population(&package, &source, &head, &heights)
                    .map_err(|e| e.to_string())?;
                retire(world, &mut state);
                let resident = scene::resident::Resident::stage(world, prepared, &base)?;
                state.materials = resident.materials();
                state.resident = Some(resident);
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
                    "Plantes importées projetées. Sélection individuelle encore indisponible."
                        .into(),
                );
                state.camera = Some(view);
                state.lighting = None;
            }
            if state.lighting.as_ref() != Some(&light_key) {
                let mut assets = world.resource_mut::<Assets<PlantMaterial>>();
                for handle in &state.materials {
                    if let Some(mut material) = assets.get_mut(handle) {
                        lighting::refresh(&mut material, &base);
                    }
                }
                state.lighting = Some(light_key);
            }
            Ok(())
        })();
        if let Err(error) = result {
            // Loading is retryable; malformed ready resources fail this exact generation.
            if population::population(&head).is_ok()
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
