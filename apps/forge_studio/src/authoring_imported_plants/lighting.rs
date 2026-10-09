use bevy::prelude::*;
use forge_engine::{
    tidewater_plants::{Params, PlantMaterial, textures::ResidentTextures},
    tidewater_scene_environment::ResidentEnvironment,
    tidewater_terrain::{lod::LodMaterial, scene::ResidentTerrain},
};
#[derive(PartialEq)]
pub(super) struct Key([AssetId<Image>; 5], [u32; 4]);

pub(super) fn material(world: &World) -> Option<(Key, PlantMaterial)> {
    let textures = world.get_resource::<ResidentTextures>()?;
    let environment = world.get_resource::<ResidentEnvironment>()?;
    let hill = world.get_resource::<forge_engine::tidewater_hill_shadow::Images>()?;
    let shadow = &world
        .get_resource::<crate::authoring_imported_shadows::Receiver>()?
        .0;
    let terrain = world.get_resource::<ResidentTerrain>()?;
    let material = world
        .get_resource::<Assets<LodMaterial>>()?
        .get(terrain.material.as_ref()?)?;
    let sun = environment.scene_lighting.sun_direction;
    let acquired = world.get_resource::<forge_engine::tidewater_wind::AcquiredFrame>()?;
    let wind = acquired.wind_frame().ok()?.material_uniform();
    let gust_lod = bevy::math::DVec4::new(
        acquired.vegetation_gust_offset[0],
        acquired.vegetation_gust_offset[1],
        0.0,
        10000.0,
    )
    .as_vec4();
    let lod_end_frame =
        bevy::math::DVec4::new(10001.0, f64::from(acquired.global_frame), 0.0, 0.0).as_vec4();
    let key = Key(
        [
            environment.environment.id(),
            hill.output.id(),
            shadow.image.id(),
            textures.leaf.id(),
            material.extension.height.id(),
        ],
        sun.to_array().map(f32::to_bits),
    );
    Some((
        key,
        PlantMaterial {
            scene_lighting: forge_engine::tidewater_material::scene_buffer::HANDLE.clone(),
            motion: forge_engine::tidewater_plants::motion::DEFAULT_BUFFER.clone(),
            params: Params {
                i_pos: Vec4::ZERO,
                i_dat: Vec4::ZERO,
                wind,
                gust_lod,
                sun_night: sun,
                lod_end_frame,
            },
            detail: textures.detail.clone(),
            leaf: textures.leaf.clone(),
            impostor_a: textures.impostors[0].clone(),
            impostor_b: textures.impostors[1].clone(),
            instances: default(),
            models: default(),
            environment: environment.environment.clone(),
            hill_shadow: hill.output.clone(),
            object_shadow: shadow.image.clone(),
            object_settings: shadow.settings.clone(),
            live_shadows: forge_engine::tidewater_object_shadow::receiver::DEFAULT_BUFFER.clone(),
            clouds: environment.cloud_view,
            cloud_shadow: Some(environment.clouds.images[2].clone()),
            grass_heights: material.extension.height.clone(),
            grass_mask: textures.grass_mask.clone(),
            kind: 0,
        },
    ))
}

pub(super) fn refresh(material: &mut PlantMaterial, base: &PlantMaterial) {
    material.environment.clone_from(&base.environment);
    material.hill_shadow.clone_from(&base.hill_shadow);
    material.object_shadow.clone_from(&base.object_shadow);
    material.object_settings.clone_from(&base.object_settings);
    material.clouds = base.clouds;
    material.cloud_shadow.clone_from(&base.cloud_shadow);
    material.params.sun_night = base.params.sun_night;
}
