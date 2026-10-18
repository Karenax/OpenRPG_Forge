//! Disposable projection of accepted common placements; no authoring mutation.
use bevy::{
    ecs as bevy_ecs,
    image::{ImageAddressMode, ImageFilterMode, ImageSampler},
    prelude::*,
    render::render_resource::TextureFormat,
};
use forge_project::zone_authoring::placements::ManualPlacement;
use forge_tidewater_assets::tidewater_debris::collisions::CollisionFrames;
use std::{cell::RefCell, collections::BTreeMap};
use {
    forge_engine::{ManualPlacementCollisionStatus, ManualPlacementProjection},
    forge_tidewater::{
        tidewater_debris::{
            PreparedDebris,
            nature::{self, NatureExtension, NatureMaterial},
            scene::{PreparedScene, ResidentMaterials, ResidentScene},
        },
        tidewater_material::VillageMaterial,
        tidewater_terrain::{lod::LodMaterial, scene::ResidentTerrain},
    },
};

type Receipt = (u64, Vec<ManualPlacement>);
thread_local! {
    static READY: RefCell<Result<Option<Receipt>, String>> = const { RefCell::new(Ok(None)) };
}

#[derive(Resource, Default)]
struct State {
    generation: Option<u64>,
    sources: Vec<ManualPlacement>,
    materials: Option<ResidentMaterials>,
}

pub(super) fn install(app: &mut App) {
    app.add_plugins(MaterialPlugin::<NatureMaterial>::default())
        .init_resource::<State>();
}

pub(super) fn ready(generation: u64, sources: &[ManualPlacement]) -> Result<bool, String> {
    READY.with(|slot| match &*slot.borrow() {
        Err(error) => Err(error.clone()),
        Ok(None) => Ok(false),
        Ok(Some((actual, placements))) => {
            Ok(*actual == generation && placements.as_slice() == sources)
        }
    })
}

/// The caller supplies the one verified geometry batch and resolved source/manual edits.
pub(super) fn project(
    world: &mut World,
    geometry: &PreparedDebris,
    collisions: &CollisionFrames,
    shader: &str,
    generation: u64,
    sources: &[ManualPlacement],
    shared: &BTreeMap<String, Handle<VillageMaterial>>,
) -> Result<bool, String> {
    // Library availability alone must not require imported terrain in another zone.
    if sources.is_empty() {
        return clear(world, generation);
    }
    let result = world.resource_scope(|world, mut state: Mut<State>| {
        let Some(materials) = prepare_materials(world, &state, shared)? else {
            return Ok(false);
        };
        if state.generation != Some(generation)
            || state.sources.as_slice() != sources
            || !world.contains_resource::<ResidentScene>()
        {
            // Validate the whole batch before replacing any existing roots/colliders.
            nature::stage_shader(world, shader)?;
            let prepared = PreparedScene::new(geometry, sources, &materials, world)?
                .with_collision_frames(geometry, collisions)?;
            prepared.stage(world);
            let roots: Vec<_> = world
                .resource::<ResidentScene>()
                .instances
                .iter()
                .map(|instance| (instance.root, instance.source.id, instance.source.transform))
                .collect();
            for (root, id, transform) in roots {
                world
                    .entity_mut(root)
                    .insert(super::placements::gizmo::PlacementRoot(id, transform));
            }
            state.generation = Some(generation);
            state.sources = sources.to_vec();
        }
        state.materials = Some(materials);
        synchronized(world, sources)
    });
    publish(result, generation, sources)
}

/// Retire the prior batch through the same adapter so backend handles remain tracked.
pub(super) fn clear(world: &mut World, generation: u64) -> Result<bool, String> {
    let result = world.resource_scope(|world, mut state: Mut<State>| {
        if state.generation != Some(generation) || !state.sources.is_empty() {
            if let Some(materials) = &state.materials {
                let empty = PreparedDebris {
                    objects: Vec::new(),
                    triangles: 0,
                    shadow_triangles: 0,
                };
                PreparedScene::new(&empty, &[], materials, world)?.stage(world);
            } else if world.contains_resource::<ResidentScene>() {
                return Err("Projection des débris sans propriétaire de matériaux".into());
            }
            state.generation = Some(generation);
            state.sources.clear();
        }
        if world.contains_resource::<ResidentScene>() {
            synchronized(world, &[])
        } else {
            Ok(true)
        }
    });
    publish(result, generation, &[])
}

fn publish(
    result: Result<bool, String>,
    generation: u64,
    sources: &[ManualPlacement],
) -> Result<bool, String> {
    READY.with(|slot| {
        *slot.borrow_mut() = match &result {
            Err(error) => Err(error.clone()),
            Ok(true) => Ok(Some((generation, sources.to_vec()))),
            Ok(false) => Ok(None),
        };
    });
    result
}

fn synchronized(world: &World, sources: &[ManualPlacement]) -> Result<bool, String> {
    let resident = world
        .get_resource::<ResidentScene>()
        .ok_or("Projection des débris absente")?;
    if resident.instances.len() != sources.len() || !resident.collisions_retired(world) {
        return Ok(false);
    }
    for (instance, source) in resident.instances.iter().zip(sources) {
        if &instance.source != source
            || world
                .get::<ManualPlacementProjection>(instance.root)
                .is_none_or(|p| &p.0 != source)
            || world
                .get::<GlobalTransform>(instance.root)
                .is_none_or(|global| {
                    world.get::<Transform>(instance.root).is_none_or(|local| {
                        !global.to_matrix().abs_diff_eq(local.to_matrix(), 1e-5)
                    })
                })
        {
            return Ok(false);
        }
        match world.get::<ManualPlacementCollisionStatus>(instance.root) {
            Some(ManualPlacementCollisionStatus::Registered { .. }) => {}
            Some(ManualPlacementCollisionStatus::Failed(reason)) => {
                return Err(format!("Collision des débris refusée : {reason}"));
            }
            _ => return Ok(false),
        }
    }
    Ok(true)
}

fn prepare_materials(
    world: &mut World,
    state: &State,
    shared: &BTreeMap<String, Handle<VillageMaterial>>,
) -> Result<Option<ResidentMaterials>, String> {
    let (Some(wood), Some(hard), Some(fabric)) = (
        shared.get("material-0"),
        shared.get("material-1"),
        shared.get("material-5"),
    ) else {
        return Ok(None);
    };
    let Some(base) = world
        .get_resource::<Assets<VillageMaterial>>()
        .and_then(|assets| assets.get(wood))
        .cloned()
    else {
        return Ok(None);
    };
    let Some(terrain) = world
        .get_resource::<ResidentTerrain>()
        .and_then(|terrain| terrain.material.as_ref())
        .and_then(|handle| world.get_resource::<Assets<LodMaterial>>()?.get(handle))
    else {
        return Ok(None);
    };
    let extension = NatureExtension {
        heights: terrain.extension.height.clone(),
        detail: terrain.base.detail.clone(),
        terrain_domain: Vec4::new(
            terrain.base.settings.domain.x,
            terrain.base.settings.domain.y,
            2048.0,
            1.0,
        ),
        terrain_sun: terrain.base.settings.sun,
    };
    verify_textures(world, &extension)?;
    let material = nature::prepare_material(base, extension)?;
    let target = if let Some(previous) = &state.materials {
        let mut assets = world.resource_mut::<Assets<NatureMaterial>>();
        let mut existing = assets
            .get_mut(&previous.nature)
            .ok_or("Matériau Nature disparu")?;
        *existing = material;
        previous.nature.clone()
    } else {
        world.resource_mut::<Assets<NatureMaterial>>().add(material)
    };
    Ok(Some(ResidentMaterials {
        wood: wood.clone(),
        hard: hard.clone(),
        fabric: fabric.clone(),
        nature: target,
    }))
}

fn verify_textures(world: &World, extension: &NatureExtension) -> Result<(), String> {
    let images = world
        .get_resource::<Assets<Image>>()
        .ok_or("Images des débris absentes")?;
    let height = images
        .get(&extension.heights)
        .ok_or("Hauteurs des débris absentes")?;
    let detail = images
        .get(&extension.detail)
        .ok_or("Détail des débris absent")?;
    if height.texture_descriptor.format != TextureFormat::R32Float
        || height.width() != 2048
        || height.height() != 2048
        || detail.texture_descriptor.format != TextureFormat::Rgba8Unorm
        || detail.width() != 512
        || detail.height() != 512
        || detail.texture_descriptor.mip_level_count != 10
    {
        return Err("Textures Nature incompatibles avec le paquet acquis".into());
    }
    let ImageSampler::Descriptor(sampler) = &detail.sampler else {
        return Err("Sampler Nature absent".into());
    };
    if sampler.address_mode_u != ImageAddressMode::Repeat
        || sampler.address_mode_v != ImageAddressMode::Repeat
        || sampler.mag_filter != ImageFilterMode::Linear
        || sampler.min_filter != ImageFilterMode::Linear
        || sampler.mipmap_filter != ImageFilterMode::Linear
        || sampler.anisotropy_clamp != 4
    {
        return Err("Sampler Nature incompatible avec le paquet acquis".into());
    }
    Ok(())
}
