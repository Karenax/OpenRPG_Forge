//! Canonical imported placements in the same scene, selection and gizmo path.
use super::{imported_buildings, placements};
use bevy::{ecs as bevy_ecs, prelude::*};
use forge_engine::{
    tidewater_assets::{
        ResidentLibrary, ResidentMeshes, materials,
        scene::{PreparedScene, ResidentScene},
    },
    tidewater_material::VillageMaterial,
    tidewater_scene_environment::ResidentEnvironment,
};
use forge_project::zone_authoring::placements::ManualPlacement;
use std::{cell::RefCell, collections::BTreeMap, sync::atomic::Ordering};

type Receipt = (u64, Vec<ManualPlacement>);
type LightingKey = (u64, AssetId<Image>, AssetId<Image>, AssetId<Image>);
thread_local! { static FOCUS: std::cell::Cell<Option<forge_core::EntityId>> = const { std::cell::Cell::new(None) }; }

pub(super) fn focus(id: forge_core::EntityId) -> bool {
    let imported = placements::desired_imports().iter().any(|p| p.id == id);
    if imported {
        FOCUS.with(|slot| slot.set(Some(id)));
    }
    imported
}
thread_local! { static READY: RefCell<Result<Option<Receipt>,String>> = const { RefCell::new(Ok(Some((0,Vec::new())))) }; }
#[derive(Resource, Default)]
struct State {
    key: Option<LightingKey>,
    materials: BTreeMap<String, Handle<VillageMaterial>>,
    sources: Option<Vec<ManualPlacement>>,
    failed: bool,
}

pub(super) fn ready() -> Result<bool, String> {
    let desired = placements::desired_imports();
    READY.with(|slot| match &*slot.borrow() {
        Err(error) => Err(error.clone()),
        Ok(None) => Ok(false),
        Ok(Some((generation, sources))) => {
            Ok(*generation == imported_buildings::published_generation() && *sources == desired)
        }
    })
}
pub(super) fn install(app: &mut App) {
    app.add_plugins(MaterialPlugin::<VillageMaterial>::default())
        .init_resource::<State>()
        .add_systems(
            PostUpdate,
            update.after(crate::authoring_imported_terrain::project),
        );
}
pub(crate) fn update(world: &mut World) {
    if !crate::authoring_viewport::can_project_sources() {
        let _ = READY.with(|slot| slot.replace(Ok(None)));
        return;
    }
    world.resource_scope(|world,mut state:Mut<State>| {
        let desired=placements::desired_imports();
        let generation=imported_buildings::published_generation();
        if desired.is_empty() {
            if state.sources.as_ref().is_none_or(|sources| !sources.is_empty()) {
                clear(world);
                state.sources=Some(Vec::new());
                state.failed=false;
            }
            let synchronized=world.get_resource::<ResidentScene>().is_some_and(|scene|scene.is_synchronized(world));
            let _ = READY.with(|slot| slot.replace(Ok(synchronized.then_some((generation,desired)))));
            return;
        }
        if state.sources.as_ref()!=Some(&desired) { state.failed=false; }
        let Some(environment)=world.get_resource::<ResidentEnvironment>() else { return; };
        let Some(hill)=world.get_resource::<forge_engine::tidewater_hill_shadow::Images>() else { return; };
        let Some(shadows)=world.get_resource::<crate::authoring_imported_shadows::Receiver>() else { return; };
        let key=(generation,environment.environment.id(),hill.output.id(),shadows.0.image.id());
        if state.key!=Some(key) { state.failed=false; }
        if state.failed || !environment.clouds.ready.load(Ordering::Acquire) { return; }
        if state.key!=Some(key) || state.sources.as_ref()!=Some(&desired) {
            let _ = READY.with(|slot| slot.replace(Ok(None)));
            let result=replace(world,&mut state,&desired,key);
            if let Err(error)=result {
                state.sources=Some(desired);
                state.key=Some(key);
                state.failed=true;
                clear(world);
                let _ = READY.with(|slot| slot.replace(Err(error.clone())));
                placements::report(format!("Bâtiments importés non projetés : {error}"));
                return;
            }
        }
        let synchronized=world.get_resource::<ResidentScene>().is_some_and(|scene|scene.is_synchronized(world));
        if synchronized {
            let changed=READY.with(|slot| slot.borrow().as_ref().ok().and_then(|v|v.as_ref()).is_none_or(|(g,p)|*g!=generation || *p!=desired));
            let _ = READY.with(|slot| slot.replace(Ok(Some((generation,desired.clone())))));
            if changed { placements::report(format!("{} bâtiments importés projetés · surfaces synchronisées dans Rapier. Rendu visuel à qualifier.",desired.len())); }
        } else {
            let _ = READY.with(|slot| slot.replace(Ok(None)));
        }
    });
    focus_current(world);
}

fn focus_current(world: &World) {
    if ready() != Ok(true) {
        return;
    }
    let Some(id) = FOCUS.with(std::cell::Cell::take) else {
        return;
    };
    let Some(instance) = world
        .get_resource::<ResidentScene>()
        .and_then(|s| s.instances.iter().find(|i| i.source.id == id))
    else {
        return;
    };
    let mut low = Vec3::splat(f32::INFINITY);
    let mut high = Vec3::splat(f32::NEG_INFINITY);
    for &part in &instance.parts {
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
}

fn replace(
    world: &mut World,
    state: &mut State,
    desired: &[ManualPlacement],
    key: LightingKey,
) -> Result<(), String> {
    let rebuild = state.sources.as_deref() != Some(desired)
        || state.key.is_none_or(|previous| previous.0 != key.0);
    if state.key != Some(key) {
        let package = imported_buildings::building_package().ok_or("Bibliothèque absente")?;
        let shader = std::str::from_utf8(
            package
                .file("village.wgsl")
                .ok_or("Shader bâtiment absent")?,
        )
        .map_err(|e| e.to_string())?;
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
        };
        let library = world
            .get_resource::<ResidentLibrary>()
            .ok_or("Textures bâtiments absentes")?;
        let prepared = library
            .materials
            .iter()
            .map(|(name, params)| {
                materials::prepare(params, &library.textures, &lighting)
                    .map(|material| (name.clone(), material))
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
    state.key = Some(key);
    Ok(())
}
