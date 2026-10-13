//! Derived common placements, source fish materials and camera-dependent LOD.
use super::{imported_buildings, imported_scene, placements};
use bevy::{
    camera::primitives::{Frustum, Sphere},
    ecs as bevy_ecs,
    prelude::*,
    render::storage::ShaderBuffer,
};
use forge_core::{AssetId as ForgeAssetId, EntityId};
use forge_engine::{
    ManualPlacementProjection,
    tidewater_fish_props::{
        PreparedFishProps,
        material::{self, FishMaterial},
        resident::ResidentFishProps,
        shadow,
        surface::SpecularMode,
    },
};
use forge_project::zone_authoring::placements::ManualPlacement;
use std::{
    cell::{Cell, RefCell},
    collections::BTreeMap,
};

thread_local! {
    static READY: RefCell<Result<Option<(u64,Vec<ManualPlacement>)>,String>> = const { RefCell::new(Ok(None)) };
    static FOCUS: Cell<Option<EntityId>> = const { Cell::new(None) };
}

#[derive(Resource, Default)]
struct State {
    generation: Option<u64>,
    sources: Vec<ManualPlacement>,
    resident: Option<ResidentFishProps>,
    skin: Option<Handle<ShaderBuffer>>,
    materials: BTreeMap<ForgeAssetId, Handle<FishMaterial>>,
    shadow_shader: Option<std::sync::Arc<String>>,
    shadow_geometry: Option<(
        Vec<(Entity, bevy::asset::AssetId<Mesh>)>,
        std::sync::Arc<Vec<shadow::MaskedMesh>>,
    )>,
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
    READY.with(|slot| match &*slot.borrow() {
        Err(error) => Err(error.clone()),
        Ok(None) => Ok(false),
        Ok(Some((generation, sources))) => {
            Ok(*generation == imported_buildings::published_generation() && *sources == desired())
        }
    })
}

pub(super) fn install(app: &mut App) {
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
    let _ = super::fish_shadow_scene::publish::<super::fish_shadow_scene::Village>(world, None);
    state.shadow_shader = None;
    state.shadow_geometry = None;
    if let Some(resident) = state.resident.take() {
        resident.clear(world);
    }
    for handle in std::mem::take(&mut state.materials).into_values() {
        world
            .resource_mut::<Assets<FishMaterial>>()
            .remove(handle.id());
    }
    if let Some(handle) = state.skin.take() {
        world
            .resource_mut::<Assets<ShaderBuffer>>()
            .remove(handle.id());
    }
    state.sources.clear();
    state.generation = None;
}

fn stage(world: &mut World, state: &mut State) -> Result<bool, String> {
    let generation = imported_buildings::published_generation();
    let sources = desired();
    // The catalogue remains available, but absent authored fish must not create
    // every template/material or repeatedly upload its source receivers.
    if sources.is_empty() {
        if state.resident.is_some() || !state.materials.is_empty() {
            clear(world, state);
        }
        return Ok(true);
    }
    let Some(package) = imported_buildings::fish_props_package() else {
        clear(world, state);
        if !sources.is_empty() {
            return Err("Paquet FishProps absent pour les placements acceptés".into());
        }
        return Ok(true);
    };
    let Some(mut lighting) = imported_scene::lighting(world) else {
        return Ok(false);
    };
    // Cascade matrices move without changing the image handle used by the library key.
    if let Some(receiver) = world.get_resource::<crate::authoring_imported_shadows::Receiver>() {
        lighting.object_shadow = receiver.0.image.clone();
        lighting.object_settings = receiver.0.settings.clone();
    }
    if let Some(environment) =
        world.get_resource::<forge_engine::tidewater_scene_environment::ResidentEnvironment>()
    {
        lighting.scene_lighting = environment.scene_lighting.clone();
        lighting.environment = environment.environment.clone();
        lighting.clouds = environment.cloud_view;
        lighting.cloud_shadow = Some(environment.clouds.images[2].clone());
    }
    let editing = world.contains_resource::<crate::authoring_render::FastEditing>();
    let time = if editing {
        0.0
    } else {
        world.resource::<Time>().elapsed_secs()
    };
    if state.generation != Some(generation) {
        clear(world, state);
        let building = imported_buildings::building_package().ok_or("Éclairage source absent")?;
        let shader = std::str::from_utf8(
            building
                .file("village.wgsl")
                .ok_or("Shader partagé absent")?,
        )
        .map_err(|error| error.to_string())?;
        material::stage_shader(world, &package, shader, SpecularMode::ThreeReference)?;
        state.shadow_shader = Some(std::sync::Arc::new(shadow::shader_source(&package)?));
        let skin = world
            .resource_mut::<Assets<ShaderBuffer>>()
            .add(material::skin_buffer(&package)?);
        state.skin = Some(skin.clone());
        let prepared = PreparedFishProps::from_package(&package)?;
        for template in &prepared.templates {
            let mut value = material::prepare(template.surface, skin.clone(), &lighting, time)?;
            value.shared_scene_lighting = true;
            state.materials.insert(
                template.asset_id,
                world.resource_mut::<Assets<FishMaterial>>().add(value),
            );
        }
        state.resident = Some(prepared.stage_authored(world, &state.materials, &sources)?);
        state.generation = Some(generation);
        state.sources = sources.clone();
    } else if state.sources != sources {
        let same_inventory = state.sources.len() == sources.len()
            && state.sources.iter().all(|old| {
                sources.iter().any(|new| {
                    (old.id, old.asset_id, old.asset_hash) == (new.id, new.asset_id, new.asset_hash)
                })
            });
        if same_inventory {
            let resident = state
                .resident
                .as_ref()
                .ok_or("Projection FishProps absente")?;
            for (root, id, _) in resident.authored_roots() {
                let source = sources
                    .iter()
                    .find(|source| source.id == *id)
                    .ok_or("Placement FishProps absent")?;
                let t = source.transform;
                world.entity_mut(*root).insert((
                    ManualPlacementProjection(source.clone()),
                    Transform {
                        translation: Vec3::new(
                            t.translation_m.x,
                            t.translation_m.y,
                            t.translation_m.z,
                        ),
                        rotation: Quat::from_xyzw(
                            t.rotation.x,
                            t.rotation.y,
                            t.rotation.z,
                            t.rotation.w,
                        ),
                        scale: Vec3::new(t.scale.x, t.scale.y, t.scale.z),
                    },
                ));
            }
        } else {
            let prepared = PreparedFishProps::from_package(&package)?;
            let next = prepared.stage_authored(world, &state.materials, &sources)?;
            if let Some(previous) = state.resident.replace(next) {
                previous.clear(world);
            }
        }
        state.sources = sources.clone();
    }
    // Copy only lighting from the common scene; preserve each instance's LOD/time uniforms.
    let resident_lighting = imported_scene::resident_lighting_material(world)
        .ok_or("Éclairage résident des poissons absent")?;
    forge_engine::tidewater_material::SceneLighting::new(
        resident_lighting
            .scene_lighting
            .sun_direction
            .truncate()
            .to_array(),
        resident_lighting
            .scene_lighting
            .sun_color
            .truncate()
            .to_array(),
        resident_lighting
            .scene_lighting
            .environment_sh
            .map(|value| value.truncate().to_array()),
    )?;
    let mut replacements = Vec::new();
    for (id, value) in world.resource::<Assets<FishMaterial>>().iter() {
        let mut next = value.clone();
        next.update_scene_receivers(&resident_lighting)?;
        if next != *value {
            replacements.push((id, next));
        }
    }
    let mut materials = world.resource_mut::<Assets<FishMaterial>>();
    for (id, next) in replacements {
        if let Some(mut value) = materials.get_mut(id) {
            *value = next;
        }
    }
    let cameras:Vec<_>=world.query_filtered::<(&Camera,&GlobalTransform,&Frustum),forge_engine::tidewater_sky::preview::SceneCameraFilter>()
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
        .resident
        .as_mut()
        .ok_or("Projection FishProps absente")?;
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
    let resident = state
        .resident
        .as_ref()
        .ok_or("Projection FishProps absente")?;
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

fn publish_shadow(world: &mut World, state: &mut State, time: f32) -> Result<(), String> {
    let Some(receiver) = world.get_resource::<crate::authoring_imported_shadows::Receiver>() else {
        let _ = super::fish_shadow_scene::publish::<super::fish_shadow_scene::Village>(world, None);
        return Ok(());
    };
    if receiver.0.settings.enabled < 0.5 {
        let _ = super::fish_shadow_scene::publish::<super::fish_shadow_scene::Village>(world, None);
        return Ok(());
    }
    let target = receiver.0.image.clone();
    let inventory = state
        .resident
        .as_ref()
        .ok_or("Projection FishProps absente")?
        .shadow_inventory(world)?;
    if inventory.is_empty() {
        let _ = super::fish_shadow_scene::publish::<super::fish_shadow_scene::Village>(world, None);
        state.shadow_geometry = None;
        return Ok(());
    }
    let key: Vec<_> = inventory
        .iter()
        .map(|(instance, _)| (instance.root, instance.mesh.id()))
        .collect();
    if state
        .shadow_geometry
        .as_ref()
        .is_none_or(|(previous, _)| *previous != key)
    {
        let meshes = world.resource::<Assets<Mesh>>();
        let geometry = inventory
            .iter()
            .map(|(instance, _)| {
                shadow::MaskedMesh::from_mesh(
                    meshes
                        .get(&instance.mesh)
                        .ok_or("Mesh ombre poisson absent")?,
                )
            })
            .collect::<Result<Vec<_>, String>>()?;
        state.shadow_geometry = Some((key, std::sync::Arc::new(geometry)));
    }
    let frame = shadow::Frame {
        target,
        shader: state
            .shadow_shader
            .as_ref()
            .ok_or("Shader ombre poisson absent")?
            .clone(),
        meshes: state
            .shadow_geometry
            .as_ref()
            .ok_or("Géométrie ombre poisson absente")?
            .1
            .clone(),
        poses: inventory
            .iter()
            .map(|(instance, enabled)| shadow::gpu::Pose {
                model: instance.world_from_local,
                enabled: *enabled,
            })
            .collect(),
        time,
    };
    super::fish_shadow_scene::publish::<super::fish_shadow_scene::Village>(world, Some(frame))
}

pub(super) fn update(world: &mut World) {
    if !crate::authoring_viewport::can_project_sources() {
        let _ = super::fish_shadow_scene::publish::<super::fish_shadow_scene::Village>(world, None);
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
                    )))
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
            let _ =
                super::fish_shadow_scene::publish::<super::fish_shadow_scene::Village>(world, None);
        }
        if let Some(id) = FOCUS.with(Cell::get)
            && !desired().iter().any(|placement| placement.id == id)
        {
            FOCUS.with(|slot| slot.set(None));
        }
        if ready
            && let Some(id) = FOCUS.with(Cell::get)
            && let Some(resident) = state.resident.as_ref()
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
