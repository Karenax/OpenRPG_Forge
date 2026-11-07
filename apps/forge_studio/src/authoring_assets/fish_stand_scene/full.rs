//! Complete material projection using the viewport's common resident resources.
use super::{State, imported_buildings};
use bevy::prelude::*;
use forge_engine::source_scene_depth;
use forge_engine::source_scene_depth::SceneDepth;
use forge_engine::source_scene_depth::contact_gpu;
use forge_engine::source_surface::SourceSurfaceMaterial;
use forge_engine::source_surface::stall_material::{self as full_material, FullMaterial};
use forge_tidewater::tidewater_stall::FishStandResident;

pub(super) fn install(app: &mut App) -> Result<(), String> {
    source_scene_depth::register(app)?;
    contact_gpu::register_deferred(app)?;
    app.add_systems(Last, refresh);
    Ok(())
}

fn inputs(world: &World, lighting: &SourceSurfaceMaterial) -> Result<contact_gpu::Inputs, String> {
    let time = world
        .get_resource::<forge_engine::source_wind::WindFrame>()
        .ok_or("Horloge commune du stand absente")?
        .material_uniform()
        .w;
    let fast = world.contains_resource::<crate::authoring_render::FastEditing>();
    let night = if fast {
        0.0
    } else {
        world
            .get_resource::<Assets<forge_engine::source_sky::SkyMaterial>>()
            .and_then(|materials| materials.iter().next())
            .ok_or("Ciel commun du stand absent")?
            .1
            .settings
            .stars_time_night
            .z
    };
    let inputs = contact_gpu::Inputs {
        lighting: lighting.scene_lighting.clone(),
        time,
        night,
        frame_index: world.resource::<bevy::diagnostic::FrameCount>().0,
        sea_level: lighting.underwater_params.tiles_m.z,
        contact_strength: if fast { 0.0 } else { 1.0 },
    };
    inputs.validate()?;
    Ok(inputs)
}

fn resize_depth(world: &mut World) -> Result<(), String> {
    let camera = {
        let mut cameras = world.query_filtered::<(Entity, &Camera), (
            With<Camera3d>,
            Without<forge_engine::scene_camera::OffscreenSceneCamera>,
            Without<forge_engine::source_refraction::CaptureCamera>,
        )>();
        let mut active = cameras.iter(world).filter(|(_, camera)| camera.is_active);
        let camera = active.next().ok_or("Caméra principale du stand absente")?.0;
        if active.next().is_some() {
            return Err("Caméra principale du stand ambiguë".into());
        }
        camera
    };
    let required = bevy::render::render_resource::TextureUsages::TEXTURE_BINDING
        | bevy::render::render_resource::TextureUsages::COPY_SRC;
    if let Some(mut component) = world.get_mut::<Camera3d>(camera) {
        let existing =
            bevy::render::render_resource::TextureUsages::from(component.depth_texture_usages);
        if !existing.contains(required) {
            component.depth_texture_usages = (existing | required).into();
        }
    }
    source_scene_depth::resize(world, camera)?;
    Ok(())
}

pub(super) fn prepare(
    world: &mut World,
    resident: &FishStandResident,
    shader_generation: &mut Option<u64>,
) -> Result<FullMaterial, String> {
    let lighting = super::super::imported_scene::resident_lighting_material(world)
        .ok_or("Matériau commun du stand non résident")?;
    let inputs = inputs(world, &lighting)?;
    resize_depth(world)?;
    let generation = imported_buildings::published_generation();
    if *shader_generation != Some(generation) {
        imported_buildings::with_lighting_shader(|source| {
            full_material::stage_shader(world, source)
        })?;
        *shader_generation = Some(generation);
    }
    let depth = world.resource::<SceneDepth>().0.clone();
    let textures = full_material::TextureArrays::from_named(&resident.textures)?;
    let material = full_material::prepare_render_owned(&textures, &lighting, depth, &inputs)?;
    world.insert_resource(inputs);
    Ok(material)
}

fn refresh(world: &mut World) {
    let Some(handle) = world
        .get_resource::<State>()
        .and_then(|state| state.material.clone())
    else {
        return;
    };
    let result = (|| -> Result<(), String> {
        let lighting = super::super::imported_scene::resident_lighting_material(world)
            .ok_or("Matériau commun du stand retiré")?;
        let inputs = inputs(world, &lighting)?;
        resize_depth(world)?;
        let depth = world.resource::<SceneDepth>().0.clone();
        {
            let mut assets = world.resource_mut::<Assets<FullMaterial>>();
            let mut material = assets
                .get_mut(&handle)
                .ok_or("Matériau complet du stand retiré")?;
            material.update_render_receivers(&lighting)?;
            material.contact_depth = depth;
        }
        world.insert_resource(inputs);
        Ok(())
    })();
    if let Err(error) = result {
        // Do not leave animation/contact inputs from a removed scene active.
        world.remove_resource::<contact_gpu::Inputs>();
        super::READY.with(|slot| {
            *slot.borrow_mut() = Err(error.clone());
        });
        super::placements::report(format!("Matériau complet du stand indisponible : {error}"));
    }
}
