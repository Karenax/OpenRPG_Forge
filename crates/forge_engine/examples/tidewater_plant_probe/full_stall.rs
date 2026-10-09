//! Native adoption of the complete stall using existing scene resources.
use super::{
    Probe,
    fish_stand::ResidentStall,
    village::{SharedMaterials, VillageMaterial},
};
use bevy::{diagnostic::FrameCount, prelude::*};
use forge_engine::{
    tidewater_ocean_depth::{self, SceneDepth},
    tidewater_stall::{
        FishStandMaterial, contact_gpu,
        full_material::{self, FullMaterial},
    },
};

#[derive(Resource)]
struct Live {
    source: Handle<VillageMaterial>,
    material: Handle<FullMaterial>,
}

pub fn install(app: &mut App) -> Result<(), String> {
    let probe = app.world().resource::<Probe>();
    if probe.fish_stand.is_none() {
        return Ok(());
    }
    if app
        .world()
        .resource::<Assets<Shader>>()
        .get(&full_material::SHADER)
        .is_none()
    {
        return Err("Complete stall verified scene shader has not been staged".into());
    }
    let lighting = probe
        .village
        .as_ref()
        .ok_or("Complete stall village input absent")?
        .lighting()
        .clone();
    // These are the same acquired bootstrap time/night installed by sky::setup
    // and village::setup. After Startup, update reads their resident values.
    let sea_level = if app
        .world()
        .contains_resource::<super::water_optics::Inputs>()
    {
        super::water_optics::underwater_column(app).0
    } else {
        0.0
    };
    tidewater_ocean_depth::register(app)?;
    contact_gpu::register(
        app,
        contact_gpu::Inputs {
            lighting,
            time: 2.0,
            night: 0.0,
            frame_index: 0,
            sea_level,
            contact_strength: 1.0,
        },
    )?;
    app.add_systems(PostStartup, stage)
        .add_systems(Last, update);
    Ok(())
}

fn shared(world: &World) -> Result<(Handle<VillageMaterial>, VillageMaterial), String> {
    let source = world
        .get_resource::<SharedMaterials>()
        .and_then(|shared| shared.0.first())
        .ok_or("Complete stall common village material absent")?
        .clone();
    let material = world
        .get_resource::<Assets<VillageMaterial>>()
        .and_then(|materials| materials.get(&source))
        .ok_or("Complete stall common village material unresolved")?
        .clone();
    Ok((source, material))
}

fn stage(world: &mut World) {
    let result = (|| -> Result<(), String> {
        let (source, lighting) = shared(world)?;
        let resident = world
            .get_resource::<ResidentStall>()
            .ok_or("Complete stall resident mesh absent")?;
        let entity = resident.entity;
        let material = full_material::prepare_render_owned(
            &resident.resident,
            &lighting,
            world
                .get_resource::<SceneDepth>()
                .ok_or("Complete stall opaque depth absent")?
                .0
                .clone(),
            world.resource::<contact_gpu::Inputs>(),
        )?;
        let handle = world.resource_mut::<Assets<FullMaterial>>().add(material);
        world
            .entity_mut(entity)
            .remove::<MeshMaterial3d<FishStandMaterial>>()
            .insert((
                MeshMaterial3d(handle.clone()),
                Name::new("FishStandStall complete source material"),
            ));
        world.insert_resource(Live {
            source,
            material: handle,
        });
        Ok(())
    })();
    if let Err(error) = result {
        fail(world, &error);
    }
}

fn update(world: &mut World) {
    let Some(live) = world.get_resource::<Live>() else {
        return;
    };
    let source = live.source.clone();
    let target = live.material.clone();
    let result = (|| -> Result<(), String> {
        let lighting = world
            .resource::<Assets<VillageMaterial>>()
            .get(&source)
            .ok_or("Complete stall common material removed")?
            .clone();
        let sky = world
            .resource::<Assets<super::sky::SkyMaterial>>()
            .iter()
            .next()
            .ok_or("Complete stall sky frame absent")?
            .1
            .settings
            .stars_time_night;
        let time = world
            .get_resource::<super::walk_wind::Receipt>()
            .filter(|receipt| receipt.submitted > 0)
            .map_or(lighting.settings.wind.w, |receipt| receipt.wind[3]);
        let mut next = world.resource::<contact_gpu::Inputs>().clone();
        next.lighting = lighting.scene_lighting.clone();
        next.time = time;
        next.night = sky.z;
        next.frame_index = world.resource::<FrameCount>().0;
        next.validate()?;
        let main = {
            let mut cameras = world.query_filtered::<(Entity, &Camera), (
                With<Camera3d>,
                Without<forge_engine::tidewater_refraction::CaptureCamera>,
            )>();
            let mut active = cameras.iter(world).filter(|(_, camera)| camera.is_active);
            let main = active.next().ok_or("Complete stall main camera absent")?.0;
            if active.next().is_some() {
                return Err("Complete stall main camera ambiguous".into());
            }
            main
        };
        let required = bevy::render::render_resource::TextureUsages::COPY_SRC
            | bevy::render::render_resource::TextureUsages::TEXTURE_BINDING;
        if let Some(mut camera) = world.get_mut::<Camera3d>(main) {
            let usages =
                bevy::render::render_resource::TextureUsages::from(camera.depth_texture_usages);
            if !usages.contains(required) {
                camera.depth_texture_usages = (usages | required).into();
            }
        }
        tidewater_ocean_depth::resize(world, main)?;
        let depth = world.resource::<SceneDepth>().0.clone();
        world
            .resource_mut::<Assets<FullMaterial>>()
            .get_mut(&target)
            .ok_or("Complete stall live material removed")?
            .update_render_receivers(&lighting)?;
        // Preserve the shared handle on resize; update if the scene replaced it.
        world
            .resource_mut::<Assets<FullMaterial>>()
            .get_mut(&target)
            .ok_or("Complete stall live material removed")?
            .contact_depth = depth;
        *world.resource_mut::<contact_gpu::Inputs>() = next;
        Ok(())
    })();
    if let Err(error) = result {
        fail(world, &error);
    }
}

fn fail(world: &mut World, error: &str) {
    error!(%error, "Complete stall native adoption failed");
    world.resource_mut::<Probe>().failed = true;
    world.write_message(AppExit::error());
}
