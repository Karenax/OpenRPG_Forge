//! Canonical imported placements in the same scene, selection and gizmo path.
mod debris;
mod local_lights;
mod motion;
mod wind;
use super::{imported_buildings, placements};
use bevy::{
    asset::RenderAssetUsages,
    render::render_resource::{Extent3d, TextureDimension, TextureFormat},
};
use bevy::{ecs as bevy_ecs, prelude::*};
use forge_engine::{
    tidewater_assets::{
        ResidentLibrary, ResidentMeshes, materials,
        scene::{PreparedScene, ResidentScene},
    },
    tidewater_beach_props::{PreparedBeachProps, ResidentBeachProps},
    tidewater_boardwalk::{PreparedBoardwalk, ResidentBoardwalk},
    tidewater_material::VillageMaterial,
    tidewater_pier::{PreparedPier, ResidentPier},
    tidewater_plaza::{PreparedPlaza, ResidentPlaza},
    tidewater_scene_environment::ResidentEnvironment,
    tidewater_village_props::{PreparedVillageProps, ResidentVillageProps},
};
use forge_project::zone_authoring::placements::ManualPlacement;
use std::{cell::RefCell, collections::BTreeMap, sync::atomic::Ordering};

type Receipt = (
    u64,
    Vec<ManualPlacement>,
    Vec<ManualPlacement>,
    Vec<ManualPlacement>,
    Vec<ManualPlacement>,
    Vec<ManualPlacement>,
    Vec<ManualPlacement>,
);
type LightingKey = (u64, AssetId<Image>, AssetId<Image>, AssetId<Image>);
thread_local! { static FOCUS: std::cell::Cell<Option<forge_core::EntityId>> = const { std::cell::Cell::new(None) }; }

pub(super) fn focus(id: forge_core::EntityId) -> bool {
    let imported = placements::desired_imports().iter().any(|p| p.id == id)
        || placements::desired_boardwalks().iter().any(|p| p.id == id)
        || placements::desired_piers().iter().any(|p| p.id == id);
    let imported = imported || placements::desired_plaza_props().iter().any(|p| p.id == id);
    let imported = imported
        || placements::desired_village_props()
            .iter()
            .any(|p| p.id == id);
    let imported = imported || placements::desired_beach_props().iter().any(|p| p.id == id);
    let imported = imported || placements::desired_debris().iter().any(|p| p.id == id);
    if imported {
        FOCUS.with(|slot| slot.set(Some(id)));
    }
    imported
}
thread_local! { static READY: RefCell<Result<Option<Receipt>,String>> = const { RefCell::new(Ok(Some((0,Vec::new(),Vec::new(),Vec::new(),Vec::new(),Vec::new(),Vec::new())))) }; }
#[derive(Resource, Default)]
struct State {
    lighting: Option<materials::LightingBindings>,
    key: Option<LightingKey>,
    materials: BTreeMap<String, Handle<VillageMaterial>>,
    sources: Option<Vec<ManualPlacement>>,
    boardwalk_sources: Option<Vec<ManualPlacement>>,
    pier_sources: Option<Vec<ManualPlacement>>,
    plaza_sources: Option<Vec<ManualPlacement>>,
    village_props_sources: Option<Vec<ManualPlacement>>,
    beach_props_sources: Option<Vec<ManualPlacement>>,
    failed: bool,
    bounce: Option<(u64, Handle<Image>, Vec4)>,
}

pub(super) fn ready() -> Result<bool, String> {
    if !debris::ready(
        imported_buildings::published_generation(),
        &placements::desired_debris(),
    )? {
        return Ok(false);
    }
    if !wind::ready()? || !motion::ready()? || !local_lights::ready()? {
        return Ok(false);
    }
    let desired = placements::desired_imports();
    let boardwalks = placements::desired_boardwalks();
    let piers = placements::desired_piers();
    let plaza = placements::desired_plaza_props();
    let village_props = placements::desired_village_props();
    let beach_props = placements::desired_beach_props();
    READY.with(|slot| match &*slot.borrow() {
        Err(error) => Err(error.clone()),
        Ok(None) => Ok(false),
        Ok(Some((
            generation,
            sources,
            boardwalk_sources,
            pier_sources,
            plaza_sources,
            village_props_sources,
            beach_props_sources,
        ))) => Ok(*generation == imported_buildings::published_generation()
            && *sources == desired
            && *boardwalk_sources == boardwalks
            && *pier_sources == piers
            && *plaza_sources == plaza
            && *village_props_sources == village_props
            && *beach_props_sources == beach_props),
    })
}
pub(super) fn install(app: &mut App) {
    if let Err(error) = wind::install(app) {
        READY.with(|slot| *slot.borrow_mut() = Err(error));
    }
    motion::install(app);
    local_lights::install(app);
    debris::install(app);
    app.add_plugins(MaterialPlugin::<VillageMaterial>::default())
        .init_resource::<State>()
        .add_systems(
            PostUpdate,
            update.after(crate::authoring_imported_terrain::project),
        );
}
pub(crate) fn update(world: &mut World) {
    imported_buildings::stage_local_lights(world);
    if !crate::authoring_viewport::can_project_sources() {
        let _ = READY.with(|slot| slot.replace(Ok(None)));
        return;
    }
    world.resource_scope(|world,mut state:Mut<State>| {
        let desired=placements::desired_imports();
        let boardwalks=placements::desired_boardwalks();
        let piers=placements::desired_piers();
        let plaza=placements::desired_plaza_props();
        let village_props=placements::desired_village_props();
        let beach_props=placements::desired_beach_props();
        let generation=imported_buildings::published_generation();
        if desired.is_empty() && boardwalks.is_empty() && piers.is_empty() && plaza.is_empty() && village_props.is_empty() && beach_props.is_empty()
            && imported_buildings::boardwalk_package().is_none()
            && imported_buildings::pier_package().is_none()
            && imported_buildings::plaza_package().is_none()
            && imported_buildings::village_props_package().is_none()
            && imported_buildings::beach_props_package().is_none() {
            if state.sources.as_ref().is_none_or(|sources| !sources.is_empty()) {
                clear(world);
                state.sources=Some(Vec::new());
                state.boardwalk_sources=Some(Vec::new());
                state.pier_sources=Some(Vec::new());
                state.plaza_sources=Some(Vec::new());
                state.village_props_sources=Some(Vec::new());
                state.beach_props_sources=Some(Vec::new());
                state.failed=false;
            }
            let synchronized=world.get_resource::<ResidentScene>().is_some_and(|scene|scene.is_synchronized(world));
            let _ = READY.with(|slot| slot.replace(Ok(synchronized.then_some((generation,desired,boardwalks,piers,plaza,village_props,beach_props)))));
            return;
        }
        if state.sources.as_ref()!=Some(&desired) || state.boardwalk_sources.as_ref()!=Some(&boardwalks)
            || state.pier_sources.as_ref()!=Some(&piers) || state.plaza_sources.as_ref()!=Some(&plaza) || state.village_props_sources.as_ref()!=Some(&village_props) || state.beach_props_sources.as_ref()!=Some(&beach_props) { state.failed=false; }
        let Some(environment)=world.get_resource::<ResidentEnvironment>() else { return; };
        let Some(hill)=world.get_resource::<forge_engine::tidewater_hill_shadow::Images>() else { return; };
        let Some(shadows)=world.get_resource::<crate::authoring_imported_shadows::Receiver>() else { return; };
        let key=(generation,environment.environment.id(),hill.output.id(),shadows.0.image.id());
        if state.key!=Some(key) { state.failed=false; }
        if state.failed || (!world.contains_resource::<crate::authoring_render::FastEditing>() && !environment.clouds.ready.load(Ordering::Acquire)) { return; }
        if state.key!=Some(key) || state.sources.as_ref()!=Some(&desired)
            || state.boardwalk_sources.as_ref()!=Some(&boardwalks)
            || state.pier_sources.as_ref()!=Some(&piers) || state.plaza_sources.as_ref()!=Some(&plaza) || state.village_props_sources.as_ref()!=Some(&village_props) || state.beach_props_sources.as_ref()!=Some(&beach_props) {
            let _ = READY.with(|slot| slot.replace(Ok(None)));
            let result=replace(world,&mut state,&desired,[&boardwalks,&piers,&plaza,&village_props,&beach_props],key);
            if let Err(error)=result {
                state.sources=Some(desired);
                state.boardwalk_sources=Some(boardwalks);
                state.pier_sources=Some(piers);
                state.plaza_sources=Some(plaza);
                state.village_props_sources=Some(village_props);
                state.beach_props_sources=Some(beach_props);
                state.key=Some(key);
                state.failed=true;
                clear(world);
                let _ = READY.with(|slot| slot.replace(Err(error.clone())));
                placements::report(format!("Bâtiments importés non projetés : {error}"));
                return;
            }
        }
        let synchronized=world.get_resource::<ResidentScene>().is_some_and(|scene|scene.is_synchronized(world))
            && imported_buildings::boardwalk_package().is_none_or(|_| {
                world.get_resource::<ResidentBoardwalk>().is_some_and(|boardwalk| boardwalk.is_synchronized(world))
            })
            && imported_buildings::pier_package().is_none_or(|_| {
                world.get_resource::<ResidentPier>().is_some_and(|pier| pier.is_synchronized(world))
            })
            && imported_buildings::plaza_package().is_none_or(|_| {
                world.get_resource::<ResidentPlaza>().is_some_and(|plaza| plaza.is_synchronized(world))
            })
            && imported_buildings::village_props_package().is_none_or(|_| {
                world.get_resource::<ResidentVillageProps>().is_some_and(|props| props.is_synchronized(world))
            })
            && imported_buildings::beach_props_package().is_none_or(|_| {
                world.get_resource::<ResidentBeachProps>().is_some_and(|props| props.is_synchronized(world))
            });
        if synchronized {
            let changed=READY.with(|slot| slot.borrow().as_ref().ok().and_then(|v|v.as_ref()).is_none_or(|(g,p,b,q,r,v,a)|*g!=generation || *p!=desired || *b!=boardwalks || *q!=piers || *r!=plaza || *v!=village_props || *a!=beach_props));
            let _ = READY.with(|slot| slot.replace(Ok(Some((generation,desired.clone(),boardwalks.clone(),piers.clone(),plaza.clone(),village_props.clone(),beach_props.clone())))));
            if changed { placements::report(format!("{} bâtiments, {} passerelles, {} quais, {} accessoires de place, {} accessoires de village et {} accessoires de plage éditables projetés · surfaces synchronisées dans Rapier. Rendu visuel à qualifier.",desired.len(),boardwalks.len(),piers.len(),plaza.len(),village_props.len(),beach_props.len())); }
        } else {
            let _ = READY.with(|slot| slot.replace(Ok(None)));
        }
    });
    let generation = imported_buildings::published_generation();
    let sources = placements::desired_debris();
    let shared = world.resource::<State>().materials.clone();
    let projected = if let Some(input) = imported_buildings::debris_input() {
        debris::project(
            world,
            &input.geometry,
            &input.input.collisions,
            &input.input.nature_program,
            generation,
            &sources,
            &shared,
        )
    } else {
        debris::clear(world, generation)
    };
    if let Err(error) = projected {
        placements::report(format!("Décors importés non projetés : {error}"));
    }
    focus_current(world);
    crate::authoring_building_projection::sync_material(world);
}

fn focus_current(world: &World) {
    if ready() != Ok(true) {
        return;
    }
    let Some(id) = FOCUS.with(std::cell::Cell::take) else {
        return;
    };
    let building_parts = world
        .get_resource::<ResidentScene>()
        .and_then(|s| s.instances.iter().find(|i| i.source.id == id))
        .map(|instance| instance.parts.clone());
    let boardwalk_parts = world
        .get_resource::<ResidentBoardwalk>()
        .and_then(|resident| resident.authored_parts(world, id));
    let pier_parts = world
        .get_resource::<ResidentPier>()
        .and_then(|resident| resident.authored_parts(world, id));
    let plaza_parts = world
        .get_resource::<ResidentPlaza>()
        .and_then(|resident| resident.authored_parts(world, id));
    let village_parts = world
        .get_resource::<ResidentVillageProps>()
        .and_then(|resident| resident.authored_parts(world, id));
    let beach_parts = world
        .get_resource::<ResidentBeachProps>()
        .and_then(|resident| resident.authored_parts(world, id));
    let debris_parts = world
        .get_resource::<forge_engine::tidewater_debris::scene::ResidentScene>()
        .and_then(|resident| {
            resident
                .instances
                .iter()
                .find(|instance| instance.source.id == id)
        })
        .map(|instance| instance.parts.clone());
    let Some(parts) = building_parts
        .or(boardwalk_parts)
        .or(pier_parts)
        .or(plaza_parts)
        .or(village_parts)
        .or(beach_parts)
        .or(debris_parts)
    else {
        return;
    };
    let mut low = Vec3::splat(f32::INFINITY);
    let mut high = Vec3::splat(f32::NEG_INFINITY);
    for part in parts {
        let (Some(bounds), Some(transform)) = (
            world.get::<bevy::camera::primitives::Aabb>(part),
            world.get::<GlobalTransform>(part),
        ) else {
            return;
        };
        for x in [-1.0, 1.0] {
            for y in [-1.0, 1.0] {
                for z in [-1.0, 1.0] {
                    let point = transform.transform_point(
                        Vec3::from(bounds.center)
                            + Vec3::from(bounds.half_extents) * Vec3::new(x, y, z),
                    );
                    low = low.min(point);
                    high = high.max(point);
                }
            }
        }
    }
    if low.is_finite() && high.is_finite() {
        crate::authoring_camera::focus_object(
            (low + high) * 0.5,
            (high - low).length().max(4.0) * 1.5,
        );
    }
}
fn clear(world: &mut World) {
    PreparedScene::default().stage(world);
    if let Some(boardwalk) = world.remove_resource::<ResidentBoardwalk>() {
        boardwalk.retire(world);
    }
    if let Some(pier) = world.remove_resource::<ResidentPier>() {
        pier.retire(world);
    }
    if let Some(plaza) = world.remove_resource::<ResidentPlaza>() {
        plaza.retire(world);
    }
    if let Some(props) = world.remove_resource::<ResidentVillageProps>() {
        props.retire(world);
    }
    if let Some(props) = world.remove_resource::<ResidentBeachProps>() {
        props.retire(world);
    }
}

fn refresh_materials(world: &mut World, state: &mut State, key: LightingKey) -> Result<(), String> {
    let package = imported_buildings::building_package().ok_or("Bibliothèque absente")?;
    let shader = std::str::from_utf8(
        package
            .file("village.wgsl")
            .ok_or("Shader bâtiment absent")?,
    )
    .map_err(|e| e.to_string())?;
    let caustics = world
        .get_resource::<forge_engine::tidewater_caustics::CausticMaterialInputs>()
        .cloned()
        .unwrap_or_else(|| {
            forge_engine::tidewater_caustics::CausticMaterialInputs::disabled(
                &mut world.resource_mut::<Assets<Image>>(),
            )
        });
    let sun_direction = world
        .resource::<ResidentEnvironment>()
        .scene_lighting
        .sun_direction
        .truncate();
    refresh_bounce(world, state, key, sun_direction)?;
    let (ground_bounce, bounce_settings) = state.bounce.as_ref().map_or_else(
        || (None, Vec4::ZERO),
        |(_, handle, settings)| (Some(handle.clone()), *settings),
    );
    let bounce_settings = Vec4::new(
        bounce_settings.x * crate::authoring_input::graphics().bounce_light,
        bounce_settings.y,
        bounce_settings.z,
        bounce_settings.w,
    );
    let environment = world.resource::<ResidentEnvironment>();
    let hill = world.resource::<forge_engine::tidewater_hill_shadow::Images>();
    let shadow = &world
        .resource::<crate::authoring_imported_shadows::Receiver>()
        .0;
    let lighting = materials::LightingBindings {
        scene_lighting: environment.scene_lighting.clone(),
        environment: environment.environment.clone(),
        hill_shadow: hill.output.clone(),
        object_shadow: shadow.image.clone(),
        object_settings: shadow.settings.clone(),
        clouds: environment.cloud_view,
        cloud_shadow: Some(environment.clouds.images[2].clone()),
        wind: Vec4::new(1.0, 0.0, 6.0, 2.0),
        flags: Vec4::ZERO,
        ground_bounce,
        bounce_settings,
        caustics,
    };
    let library = world
        .get_resource::<ResidentLibrary>()
        .ok_or("Textures bâtiments absentes")?;
    let prepared = library
        .materials
        .iter()
        .map(|(name, params)| {
            materials::prepare(params, &library.textures, &lighting).map(|mut material| {
                material.shared_scene_lighting = true;
                (name.clone(), material)
            })
        })
        .collect::<Result<Vec<_>, _>>()?;
    if state.key.is_none_or(|previous| previous.0 != key.0) {
        forge_engine::tidewater_material::stage_shader(world, shader)?;
    }
    let mut assets = world.resource_mut::<Assets<VillageMaterial>>();
    for (name, material) in prepared {
        if let Some(mut existing) = state.materials.get(&name).and_then(|h| assets.get_mut(h)) {
            *existing = material;
        } else {
            state.materials.insert(name, assets.add(material));
        }
    }
    state.lighting = Some(lighting);
    Ok(())
}

pub(super) fn lighting(world: &World) -> Option<materials::LightingBindings> {
    let state = world.get_resource::<State>()?;
    if state.key?.0 != imported_buildings::published_generation() {
        return None;
    }
    state.lighting.clone()
}

/// Clone the actual resident common receiver bindings, including live buffers.
pub(crate) fn resident_lighting_material(world: &World) -> Option<VillageMaterial> {
    let state = world.get_resource::<State>()?;
    if state.key?.0 != imported_buildings::published_generation() {
        return None;
    }
    let handle = state.materials.values().next()?;
    world
        .get_resource::<Assets<VillageMaterial>>()?
        .get(handle)
        .cloned()
}

fn refresh_bounce(
    world: &mut World,
    state: &mut State,
    key: LightingKey,
    sun_direction: Vec3,
) -> Result<(), String> {
    if state
        .bounce
        .as_ref()
        .is_none_or(|(generation, _, _)| *generation != key.0)
    {
        state.bounce = imported_buildings::ground_bounce_package()
            .map(|package| -> Result<_, String> {
                let source_sun = Vec3::from_array(package.sun_direction());
                if (source_sun - sun_direction).length() > 0.001 {
                    return Err(
                        "La carte de lumière rebond ne correspond pas au soleil importé".into(),
                    );
                }
                let image = Image::new(
                    Extent3d {
                        width: 512,
                        height: 512,
                        depth_or_array_layers: 1,
                    },
                    TextureDimension::D2,
                    package.pixels().to_vec(),
                    TextureFormat::Rgba16Float,
                    RenderAssetUsages::default(),
                );
                let handle = world.resource_mut::<Assets<Image>>().add(image);
                Ok((
                    key.0,
                    handle,
                    Vec4::new(package.strength(), package.sea_level(), 0.0, 0.0),
                ))
            })
            .transpose()?;
    }
    Ok(())
}

fn stage_side_structures(
    world: &mut World,
    state: &State,
    side: [&[ManualPlacement]; 5],
) -> Result<(), String> {
    let [boardwalks, piers, plaza, village_props, beach_props] = side;
    if let Some(previous) = world.remove_resource::<ResidentBoardwalk>() {
        previous.retire(world);
    }
    if let Some(package) = imported_buildings::boardwalk_package() {
        let prepared = PreparedBoardwalk::from_package(&package)?;
        let resident = prepared.stage_authored(world, &state.materials, boardwalks)?;
        let roots = resident.authored_roots().to_vec();
        for (root, id, transform) in roots {
            world
                .entity_mut(root)
                .insert(placements::gizmo::PlacementRoot(id, transform));
        }
        world.insert_resource(resident);
    } else if !boardwalks.is_empty() {
        return Err("Paquet des passerelles éditables absent".into());
    }
    if let Some(previous) = world.remove_resource::<ResidentPier>() {
        previous.retire(world);
    }
    if let Some(package) = imported_buildings::pier_package() {
        let prepared = PreparedPier::from_package(&package)?;
        let resident = prepared.stage_authored(world, &state.materials, piers)?;
        for &(root, id, transform) in resident.authored_roots() {
            world
                .entity_mut(root)
                .insert(placements::gizmo::PlacementRoot(id, transform));
        }
        world.insert_resource(resident);
    } else if !piers.is_empty() {
        return Err("Paquet du quai éditable absent".into());
    }
    if let Some(previous) = world.remove_resource::<ResidentPlaza>() {
        previous.retire(world);
    }
    if let Some(package) = imported_buildings::plaza_package() {
        let prepared = PreparedPlaza::from_package(&package)?;
        let resident = prepared.stage_authored(world, &state.materials, plaza)?;
        for &(root, id, transform) in resident.authored_roots() {
            world
                .entity_mut(root)
                .insert(placements::gizmo::PlacementRoot(id, transform));
        }
        world.insert_resource(resident);
    } else if !plaza.is_empty() {
        return Err("Paquet des accessoires de place éditables absent".into());
    }
    if let Some(previous) = world.remove_resource::<ResidentVillageProps>() {
        previous.retire(world);
    }
    if let Some(package) = imported_buildings::village_props_package() {
        let prepared = PreparedVillageProps::from_package(&package)?;
        let resident = prepared.stage_authored(world, &state.materials, village_props)?;
        for &(root, id, transform) in resident.authored_roots() {
            world
                .entity_mut(root)
                .insert(placements::gizmo::PlacementRoot(id, transform));
        }
        world.insert_resource(resident);
    } else if !village_props.is_empty() {
        return Err("Paquet des accessoires de village éditables absent".into());
    }
    if let Some(previous) = world.remove_resource::<ResidentBeachProps>() {
        previous.retire(world);
    }
    if let Some(package) = imported_buildings::beach_props_package() {
        let prepared = PreparedBeachProps::from_package(&package)?;
        let resident = prepared.stage_authored(world, &state.materials, beach_props)?;
        for &(root, id, transform) in resident.authored_roots() {
            world
                .entity_mut(root)
                .insert(placements::gizmo::PlacementRoot(id, transform));
        }
        world.insert_resource(resident);
    } else if !beach_props.is_empty() {
        return Err("Paquet des accessoires de plage éditables absent".into());
    }
    Ok(())
}

fn replace(
    world: &mut World,
    state: &mut State,
    desired: &[ManualPlacement],
    side: [&[ManualPlacement]; 5],
    key: LightingKey,
) -> Result<(), String> {
    let [boardwalks, piers, plaza, village_props, beach_props] = side;
    let rebuild = state.sources.as_deref() != Some(desired)
        || state.boardwalk_sources.as_deref() != Some(boardwalks)
        || state.pier_sources.as_deref() != Some(piers)
        || state.plaza_sources.as_deref() != Some(plaza)
        || state.village_props_sources.as_deref() != Some(village_props)
        || state.beach_props_sources.as_deref() != Some(beach_props)
        || state.key.is_none_or(|previous| previous.0 != key.0);
    if state.key != Some(key) {
        refresh_materials(world, state, key)?;
    }
    if !rebuild {
        state.key = Some(key);
        return Ok(());
    }
    let prepared = PreparedScene::new(
        desired,
        world
            .get_resource::<ResidentMeshes>()
            .ok_or("Géométrie bâtiment absente")?,
        &state.materials,
        world.resource::<Assets<Mesh>>(),
        world.resource::<Assets<VillageMaterial>>(),
    )?;
    prepared.stage(world);
    stage_side_structures(
        world,
        state,
        [boardwalks, piers, plaza, village_props, beach_props],
    )?;
    let roots = world
        .resource::<ResidentScene>()
        .instances
        .iter()
        .map(|i| (i.root, i.source.id, i.source.transform))
        .collect::<Vec<_>>();
    for (root, id, transform) in roots {
        world
            .entity_mut(root)
            .insert(placements::gizmo::PlacementRoot(id, transform));
    }
    state.sources = Some(desired.to_vec());
    state.boardwalk_sources = Some(boardwalks.to_vec());
    state.pier_sources = Some(piers.to_vec());
    state.plaza_sources = Some(plaza.to_vec());
    state.village_props_sources = Some(village_props.to_vec());
    state.beach_props_sources = Some(beach_props.to_vec());
    state.key = Some(key);
    Ok(())
}
