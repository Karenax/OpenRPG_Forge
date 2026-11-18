//! Mount an admitted stall against the common live environment and opaque depth.
use super::{
    super::{
        scene,
        stall_assets::PreparedStall,
        stall_material::{self, FullMaterial, TextureArrays},
    },
    placement,
};
use crate::{
    source_fish::host::live::FishHostSet,
    source_lighting::surfaces::live::{self as surfaces, SurfaceHostSet},
    source_scene_depth::{self, SceneDepth, contact_gpu},
    source_sky::preview::SceneCameraFilter,
};
use bevy::{prelude::*, render::render_resource::TextureUsages};
use forge_project::{
    module_package::ModuleStallAnimatedModel, zone_authoring::placements::ManualPlacement,
};
use std::collections::BTreeSet;
mod animated;

#[derive(Resource)]
struct Pending {
    assets: PreparedStall,
    placements: Vec<ManualPlacement>,
    water: bool,
    animated: Option<ModuleStallAnimatedModel>,
}

#[derive(Resource)]
struct Resident {
    mesh: Handle<Mesh>,
    material: Handle<FullMaterial>,
    dry_water: Option<scene::WaterBindings>,
    actors: Option<(animated::Animated, Vec<Entity>)>,
    roots: BTreeSet<Entity>,
}

/// Confirm that the draw still belongs to the pinned stall projection.
pub fn owns(world: &World, draw: Entity) -> bool {
    world.get_resource::<Resident>().is_some_and(|resident| {
        world
            .get::<ChildOf>(draw)
            .is_some_and(|parent| resident.roots.contains(&parent.parent()))
            && world
                .get::<Mesh3d>(draw)
                .is_some_and(|mesh| mesh.0 == resident.mesh)
            && world
                .get::<MeshMaterial3d<FullMaterial>>(draw)
                .is_some_and(|material| material.0 == resident.material)
    })
}

#[derive(Resource, Default)]
pub struct Status {
    pub error: Option<String>,
    pub ready: bool,
}

pub fn install(
    app: &mut App,
    assets: PreparedStall,
    placements: Vec<ManualPlacement>,
    lighting_source: &str,
    water: bool,
    animated: Option<ModuleStallAnimatedModel>,
) -> Result<(), String> {
    if !surfaces::installed(app.world()) || app.world().contains_resource::<Status>() {
        return Err("Source stalls require one live surface owner".into());
    }
    source_scene_depth::register(app)?;
    contact_gpu::register_deferred(app)?;
    stall_material::stage_shader(app.world_mut(), lighting_source)?;
    app.add_plugins(MaterialPlugin::<FullMaterial>::default())
        .insert_resource(Pending {
            assets,
            placements,
            water,
            animated,
        })
        .init_resource::<Status>()
        .add_systems(PostStartup, start.after(SurfaceHostSet).before(FishHostSet))
        .add_systems(Last, update);
    Ok(())
}

fn start(world: &mut World) {
    let Some(pending) = world.remove_resource::<Pending>() else {
        return;
    };
    let result = (|| {
        let dry_water = if pending.water {
            None
        } else {
            Some(scene::absent_water(world)?)
        };
        let water = water_bindings(world, dry_water.as_ref())?;
        let lighting = scene::lighting_material(world, &water)?;
        let inputs = contact_inputs(world, &lighting)?;
        let depth = depth_target(world)?;
        let mesh = world
            .resource_mut::<Assets<Mesh>>()
            .add(pending.assets.mesh);
        let textures = {
            let mut images = world.resource_mut::<Assets<Image>>();
            pending
                .assets
                .textures
                .into_iter()
                .map(|(name, image)| (name, images.add(image)))
                .collect()
        };
        let material = stall_material::prepare_render_owned(
            &TextureArrays::from_named(&textures)?,
            &lighting,
            depth,
            &inputs,
        )?;
        let material = world.resource_mut::<Assets<FullMaterial>>().add(material);
        let mut actors = pending
            .animated
            .map(|model| (animated::Animated::load(world, model), Vec::new()));
        let mut roots = BTreeSet::new();
        for source in &pending.placements {
            let root = placement::spawn_root(world, source, &mesh, &material);
            roots.insert(root);
            if let Some((model, instances)) = &mut actors {
                instances.push(model.spawn(world, root));
            }
        }
        world.insert_resource(Resident {
            mesh,
            material,
            dry_water,
            actors,
            roots,
        });
        world.insert_resource(inputs);
        Ok::<_, String>(())
    })();
    if let Err(error) = result {
        world.resource_mut::<Status>().error = Some(error);
    }
}

fn update(world: &mut World) {
    if world.resource::<Status>().error.is_some() || !world.contains_resource::<Resident>() {
        return;
    }
    let result = world.resource_scope(|world, resident: Mut<Resident>| {
        let ready = resident
            .actors
            .as_ref()
            .map_or(Ok(true), |(model, actors)| model.ready(world, actors))?;
        let water = water_bindings(world, resident.dry_water.as_ref())?;
        let lighting = scene::lighting_material(world, &water)?;
        let inputs = contact_inputs(world, &lighting)?;
        let depth = depth_target(world)?;
        {
            let mut materials = world.resource_mut::<Assets<FullMaterial>>();
            let mut material = materials
                .get_mut(&resident.material)
                .ok_or("Source stall material was removed")?;
            material.update_render_receivers(&lighting)?;
            material.contact_depth = depth;
        }
        world.insert_resource(inputs);
        Ok::<_, String>(ready)
    });
    match result {
        Ok(ready) => world.resource_mut::<Status>().ready = ready,
        Err(error) => {
            world.remove_resource::<contact_gpu::Inputs>();
            let mut status = world.resource_mut::<Status>();
            status.ready = false;
            status.error = Some(error);
        }
    }
}

fn water_bindings(
    world: &World,
    dry: Option<&scene::WaterBindings>,
) -> Result<scene::WaterBindings, String> {
    dry.map_or_else(|| scene::required_water(world), |water| Ok(water.clone()))
}

fn contact_inputs(
    world: &World,
    lighting: &crate::source_surface::SourceSurfaceMaterial,
) -> Result<contact_gpu::Inputs, String> {
    let inputs = contact_gpu::Inputs {
        lighting: lighting.scene_lighting.clone(),
        time: lighting.settings.wind.w,
        night: lighting.settings.flags.y,
        frame_index: world.resource::<bevy::diagnostic::FrameCount>().0,
        sea_level: lighting.underwater_params.tiles_m.z,
        contact_strength: 1.0,
    };
    inputs.validate()?;
    Ok(inputs)
}

fn depth_target(world: &mut World) -> Result<Handle<Image>, String> {
    let camera = {
        let mut query = world.query_filtered::<(Entity, &Camera), (
            SceneCameraFilter,
            Without<crate::source_refraction::CaptureCamera>,
        )>();
        let mut active = query.iter(world).filter(|(_, camera)| camera.is_active);
        let first = active.next().map(|(entity, _)| entity);
        if active.next().is_some() {
            return Err("Source stall requires one active scene camera".into());
        }
        first
    };
    if let Some(camera) = camera {
        let required = TextureUsages::COPY_SRC | TextureUsages::TEXTURE_BINDING;
        if let Some(mut projection) = world.get_mut::<Camera3d>(camera) {
            let existing = TextureUsages::from(projection.depth_texture_usages);
            if !existing.contains(required) {
                projection.depth_texture_usages = (existing | required).into();
            }
        }
        source_scene_depth::resize(world, camera)?;
    }
    Ok(world.resource::<SceneDepth>().0.clone())
}
