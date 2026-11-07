//! Derived common placements, source fish materials and camera-dependent LOD.
use super::{imported_buildings, imported_scene, placements};
use bevy::{
    camera::primitives::{Frustum, Sphere},
    ecs as bevy_ecs,
    prelude::*,
};
use forge_core::EntityId;
use forge_project::zone_authoring::placements::ManualPlacement;
use std::{
    cell::{Cell, RefCell},
    collections::BTreeSet,
};
use {
    forge_engine::ManualPlacementProjection,
    forge_engine::source_fish::{
        host::{
            PreparedProfile,
            scene::{PreparedScene, Scene},
        },
        material::FishMaterial,
        shadow,
        surface::SpecularMode,
    },
    forge_engine::source_shadows::casters::masked::merge as shadow_merge,
};

type Readiness = Result<Option<(u64, Vec<ManualPlacement>)>, String>;
thread_local! {
    static INSTALLED: Cell<bool> = const { Cell::new(false) };
    static READY: RefCell<Readiness> = const { RefCell::new(Ok(None)) };
    static FOCUS: Cell<Option<EntityId>> = const { Cell::new(None) };
}

#[derive(Resource, Default)]
struct State {
    generation: Option<u64>,
    sources: Vec<ManualPlacement>,
    scene: Option<Scene>,
    shadow_cache: shadow::cache::MaskedFishCache,
}

fn desired() -> Vec<ManualPlacement> {
    placements::desired_fish_props()
}

pub(super) fn focus(id: EntityId) -> bool {
    let found = desired().iter().any(|placement| placement.id == id);
    if found {
        FOCUS.with(|slot| slot.set(Some(id)));
    }
    found
}

pub(super) fn ready() -> Result<bool, String> {
    if !INSTALLED.with(Cell::get) {
        return if desired().is_empty() {
            Ok(true)
        } else {
            Err(
                "L’affichage des objets de poissonnerie n’est pas encore disponible sur ce relief."
                    .into(),
            )
        };
    }
    READY.with(|slot| match &*slot.borrow() {
        Err(error) => Err(error.clone()),
        Ok(None) => Ok(false),
        Ok(Some((generation, sources))) => {
            Ok(*generation == imported_buildings::published_generation() && *sources == desired())
        }
    })
}

pub(super) fn install(app: &mut App) {
    INSTALLED.with(|slot| slot.set(true));
    app.add_plugins(MaterialPlugin::<FishMaterial>::default())
        .init_resource::<State>()
        .add_systems(
            PostUpdate,
            update
                .after(imported_scene::update)
                .after(bevy::transform::TransformSystems::Propagate),
        );
}

fn clear(world: &mut World, state: &mut State) {
    let _ = shadow_merge::publish::<super::fish_shadow_scene::Village>(world, None);
    state.shadow_cache.clear();
    if let Some(scene) = state.scene.take() {
        scene.clear(world);
    }
    state.sources.clear();
    state.generation = None;
}

fn build_scene(
    world: &mut World,
    profile: &impl forge_assets::source_fish_payload::SourceFishProfile,
    package_sha256: forge_core::ContentHash,
    sources: &[ManualPlacement],
    lighting: &forge_engine::source_surface::SourceSurfaceMaterial,
    policy: forge_engine::distance_lod::Policy,
    time: f32,
) -> Result<Scene, String> {
    let assets: BTreeSet<_> = sources.iter().map(|source| source.asset_id).collect();
    let profile = imported_buildings::with_lighting_shader(|shader| {
        PreparedProfile::new_selected(
            profile,
            package_sha256,
            shader,
            SpecularMode::ThreeReference,
            &assets,
        )
    })?;
    PreparedScene::new(vec![profile], sources.to_vec(), policy)?.stage(world, lighting, time)
}

fn stage(world: &mut World, state: &mut State) -> Result<bool, String> {
    let generation = imported_buildings::published_generation();
    let sources = desired();
    if sources.is_empty() {
        if state.scene.is_some() {
            clear(world, state);
        }
        return Ok(true);
    }
    let Some((package_sha256, package)) = imported_buildings::fish_props_package() else {
        clear(world, state);
        return Err("Paquet FishProps absent pour les placements acceptés".into());
    };
    let Some(lighting) = imported_scene::resident_lighting_material(world) else {
        return Ok(false);
    };
    let editing = world.contains_resource::<crate::authoring_render::FastEditing>();
    let time = if editing {
        0.0
    } else {
        world.resource::<Time>().elapsed_secs()
    };
    if state.generation != Some(generation) {
        clear(world, state);
        state.scene = Some(build_scene(
            world,
            package.as_ref(),
            package_sha256,
            &sources,
            &lighting,
            forge_tidewater::tidewater_fish_props::lod::POLICY,
            time,
        )?);
        state.generation = Some(generation);
        state.sources.clone_from(&sources);
    } else if state.sources != sources {
        let same_inventory = state.sources.len() == sources.len()
            && state.sources.iter().all(|old| {
                sources.iter().any(|new| {
                    (old.id, old.asset_id, old.asset_hash) == (new.id, new.asset_id, new.asset_hash)
                })
            });
        if same_inventory {
            state
                .scene
                .as_mut()
                .ok_or("Projection FishProps absente")?
                .resident_mut()
                .move_authored(world, &sources)?;
        } else {
            let next = build_scene(
                world,
                package.as_ref(),
                package_sha256,
                &sources,
                &lighting,
                forge_tidewater::tidewater_fish_props::lod::POLICY,
                time,
            )?;
            if let Some(previous) = state.scene.replace(next) {
                previous.clear(world);
            }
            state.shadow_cache.clear();
        }
        state.sources.clone_from(&sources);
    }
    update_view(world, state, &sources, editing, time)
}

fn publish_shadow(world: &mut World, state: &mut State, time: f32) -> Result<(), String> {
    let Some(receiver) = world.get_resource::<crate::authoring_imported_shadows::Receiver>() else {
        let _ = shadow_merge::publish::<super::fish_shadow_scene::Village>(world, None);
        return Ok(());
    };
    if receiver.0.settings.enabled < 0.5 {
        let _ = shadow_merge::publish::<super::fish_shadow_scene::Village>(world, None);
        return Ok(());
    }
    let target = receiver.0.image.clone();
    let scene = state.scene.as_ref().ok_or("Projection FishProps absente")?;
    let resident = scene.resident();
    if resident.is_dormant() {
        return shadow_merge::publish::<super::fish_shadow_scene::Village>(world, None);
    }
    let inventory = resident.shadow_inventory(world)?;
    if inventory.is_empty() {
        let _ = shadow_merge::publish::<super::fish_shadow_scene::Village>(world, None);
        state.shadow_cache.clear();
        return Ok(());
    }
    let frame = state
        .shadow_cache
        .frame(world, inventory, target, scene.shadow_shader(), time)?;
    shadow_merge::publish::<super::fish_shadow_scene::Village>(world, frame)
}

pub(super) fn update(world: &mut World) {
    if !crate::authoring_viewport::can_project_sources() {
        let _ = shadow_merge::publish::<super::fish_shadow_scene::Village>(world, None);
        READY.with(|slot| *slot.borrow_mut() = Ok(None));
        return;
    }
    world.resource_scope(|world, mut state: Mut<State>| {
        let ready = match stage(world, &mut state) {
            Ok(true) => {
                READY.with(|slot| {
                    *slot.borrow_mut() = Ok(Some((
                        imported_buildings::published_generation(),
                        desired(),
                    )));
                });
                true
            }
            Ok(false) => {
                READY.with(|slot| *slot.borrow_mut() = Ok(None));
                false
            }
            Err(error) => {
                clear(world, &mut state);
                READY.with(|slot| *slot.borrow_mut() = Err(error.clone()));
                placements::report(format!("Poissons non projetés : {error}"));
                false
            }
        };
        if !ready {
            let _ = shadow_merge::publish::<super::fish_shadow_scene::Village>(world, None);
        }
        if let Some(id) = FOCUS.with(Cell::get)
            && !desired().iter().any(|placement| placement.id == id)
        {
            FOCUS.with(|slot| slot.set(None));
        }
        if ready
            && let Some(id) = FOCUS.with(Cell::get)
            && let Some(resident) = state.scene.as_ref().map(Scene::resident)
            && let Some((root, _, _)) = resident
                .authored_roots()
                .iter()
                .find(|(_, placed, _)| *placed == id)
            && let Some(global) = world.get::<GlobalTransform>(*root)
        {
            crate::authoring_camera::focus_object(global.translation(), 3.0);
            FOCUS.with(|slot| slot.set(None));
        }
    });
}

fn update_view(
    world: &mut World,
    state: &mut State,
    sources: &[ManualPlacement],
    editing: bool,
    time: f32,
) -> Result<bool, String> {
    let cameras:Vec<_>=world.query_filtered::<(&Camera,&GlobalTransform,&Frustum),forge_engine::source_sky::preview::SceneCameraFilter>()
        .iter(world).filter(|(camera,_,_)|camera.is_active).map(|(_,global,frustum)|(global.translation(),*frustum)).collect();
    if cameras.len() != 1 {
        return Ok(false);
    }
    let (camera, frustum) = cameras[0];
    let frame = if editing {
        0
    } else {
        world.resource::<bevy::diagnostic::FrameCount>().0
    };
    let resident = state
        .scene
        .as_mut()
        .ok_or("Projection FishProps absente")?
        .resident_mut();
    resident.update_view(
        world,
        camera,
        |center, radius| {
            frustum.intersects_sphere(
                &Sphere {
                    center: center.into(),
                    radius,
                },
                true,
            )
        },
        time,
        frame,
    )?;
    let lighting = imported_scene::resident_lighting_material(world)
        .ok_or("Éclairage résident des poissons absent")?;
    resident.update_scene_receivers(world, &lighting)?;
    let resident = state
        .scene
        .as_ref()
        .ok_or("Projection FishProps absente")?
        .resident();
    let ready = resident.authored_roots().iter().all(|(root, id, _)| {
        sources
            .iter()
            .find(|source| source.id == *id)
            .is_some_and(|source| {
                world
                    .get::<ManualPlacementProjection>(*root)
                    .is_some_and(|projection| projection.0 == *source)
                    && world.get::<GlobalTransform>(*root).is_some_and(|global| {
                        world.get::<Transform>(*root).is_some_and(|local| {
                            global.to_matrix().abs_diff_eq(local.to_matrix(), 1e-5)
                        })
                    })
            })
    });
    if ready {
        publish_shadow(world, state, time)?;
    }
    Ok(ready)
}
