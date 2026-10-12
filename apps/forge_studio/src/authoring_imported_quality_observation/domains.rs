//! Current imported resources and CPU uniforms; unavailable budgets stay explicit.
use super::{CurrentProfileInventory, Origin, ParameterValue as V};
use bevy::prelude::*;
use forge_engine::{
    tidewater_plants::PlantMaterial,
    tidewater_terrain::{lod::LodMaterial, scene::ResidentTerrain},
};
use std::{collections::BTreeMap, sync::atomic::Ordering};

pub(super) fn terrain_profile(world: &World, out: &mut CurrentProfileInventory) {
    let Some(terrain) = world.get_resource::<ResidentTerrain>() else {
        return;
    };
    let resource = Origin::MainWorldResource;
    out.put(
        "terrain/source_hash",
        V::Text(terrain.document_hash.to_string()),
        resource,
        true,
    );
    if let Some(entity) = terrain.entity {
        out.put(
            "terrain/resident_entity",
            V::Unsigned(entity.to_bits()),
            resource,
            false,
        );
    }
    if let Some(handle) = &terrain.material {
        out.put(
            "terrain/material_identity",
            V::Text(format!("{:?}", handle.id())),
            resource,
            true,
        );
        if let Some(material) = world
            .get_resource::<Assets<LodMaterial>>()
            .and_then(|assets| assets.get(handle))
        {
            let uniform = Origin::CpuMaterialUniform;
            out.put(
                "terrain/lod_morph_uniform",
                V::NumberRows(
                    material
                        .extension
                        .settings
                        .morph
                        .iter()
                        .map(|v| v.to_array().into_iter().map(f64::from).collect())
                        .collect(),
                ),
                uniform,
                true,
            );
            out.numbers(
                "terrain/domain_uniform",
                material.base.settings.domain.to_array(),
                uniform,
                true,
            );
            let shadow = &material.base.object_settings;
            out.put(
                "shadows/cpu_receiver_count",
                V::Unsigned(u64::from(shadow.count)),
                uniform,
                true,
            );
            out.number(
                "shadows/cpu_receiver_map_size",
                shadow.map_size,
                uniform,
                true,
            );
            out.numbers(
                "shadows/cpu_receiver_bias_fade_sun_angle",
                [shadow.bias, shadow.fade, shadow.sun_angular_diameter],
                uniform,
                true,
            );
            out.put(
                "shadows/cpu_receiver_pcss_cascades",
                V::Unsigned(u64::from(shadow.pcss_cascades)),
                uniform,
                true,
            );
            out.image(world, "terrain/height", &material.extension.height);
            out.image(
                world,
                "terrain/corrections",
                &material.extension.corrections,
            );
        }
    }
}

pub(super) fn shadow_profile(world: &World, out: &mut CurrentProfileInventory) {
    let resource = Origin::MainWorldResource;
    if let Some(target) =
        world.get_resource::<forge_engine::tidewater_object_shadow::graph::Target>()
    {
        out.image(world, "shadows/depth", &target.image);
        out.put(
            "shadows/commands_encoded",
            V::Boolean(target.ready.load(Ordering::Acquire)),
            resource,
            false,
        );
    }
    if let Some(sample) = world
        .get_resource::<forge_engine::tidewater_object_shadow::receiver::GpuObservation>()
        .and_then(|observation| observation.0.as_ref())
    {
        out.numbers(
            "shadows/live_receiver_readback",
            [sample.enabled, sample.control, sample.map_size],
            Origin::AsyncGpuReadback,
            false,
        );
    }
}

pub(super) fn vegetation_profile(world: &mut World, out: &mut CurrentProfileInventory) {
    let count = world
        .query::<&MeshMaterial3d<PlantMaterial>>()
        .iter(world)
        .count();
    out.put(
        "vegetation/referenced_render_entities",
        V::Count(count),
        Origin::MainWorldComponent,
        false,
    );
    let Some(materials) = world.get_resource::<Assets<PlantMaterial>>() else {
        return;
    };
    // Material LOD budgets are stable; selected entities/instance counts are camera dependent.
    let mut ranges = BTreeMap::new();
    for (_, material) in materials.iter() {
        let range = [
            material.params.gust_lod.z,
            material.params.gust_lod.w,
            material.params.lod_end_frame.x,
        ];
        ranges.insert((material.kind, range.map(f32::to_bits)), range);
    }
    out.put(
        "vegetation/lod_uniforms",
        V::NumberRows(
            ranges
                .into_iter()
                .map(|((kind, _), range)| {
                    std::iter::once(f64::from(kind))
                        .chain(range.into_iter().map(f64::from))
                        .collect()
                })
                .collect(),
        ),
        Origin::CpuMaterialUniform,
        true,
    );
}

pub(super) fn environment_profile(world: &World, out: &mut CurrentProfileInventory) {
    let Some(environment) =
        world.get_resource::<forge_engine::tidewater_scene_environment::ResidentEnvironment>()
    else {
        return;
    };
    let resource = Origin::MainWorldResource;
    out.image(world, "environment/map", &environment.environment);
    for (index, image) in environment.atmosphere.images.iter().enumerate() {
        out.image(world, &format!("environment/atmosphere_{index}"), image);
    }
    for (index, image) in environment.clouds.images.iter().enumerate() {
        out.image(world, &format!("environment/cloud_{index}"), image);
    }
    out.put(
        "environment/atmosphere_commands_encoded",
        V::Boolean(environment.atmosphere.ready.load(Ordering::Acquire)),
        resource,
        false,
    );
    out.put(
        "environment/cloud_ready_flag",
        V::Boolean(environment.clouds.ready.load(Ordering::Acquire)),
        resource,
        false,
    );
    out.put(
        "environment/cloud_completed_step_counter",
        V::Unsigned(u64::from(
            environment.clouds.completed_steps.load(Ordering::Acquire),
        )),
        resource,
        false,
    );
}

pub(super) fn water_profile(world: &World, out: &mut CurrentProfileInventory) {
    let resource = Origin::MainWorldResource;
    out.put(
        "water/resident_optics_present",
        V::Boolean(
            world.contains_resource::<forge_engine::tidewater_ocean_optics::ResidentOptics>(),
        ),
        resource,
        true,
    );
    if let Some(ocean) = world.get_resource::<forge_engine::tidewater_ocean::Targets>() {
        for (index, image) in ocean.images.iter().enumerate() {
            out.image(world, &format!("water/ocean_{index}"), image);
        }
        out.put(
            "water/ocean_uniform_raw_words",
            V::RawWordRows(
                ocean
                    .parameters
                    .iter()
                    .map(|v| v.to_array().to_vec())
                    .collect(),
            ),
            resource,
            true,
        );
        out.put(
            "water/ocean_completed_step_counter",
            V::Unsigned(u64::from(ocean.completed_steps.load(Ordering::Acquire))),
            resource,
            false,
        );
    }
    if let Some(control) = world.get_resource::<forge_engine::tidewater_refraction::Control>() {
        out.put(
            "water/refraction_capture_enabled",
            V::Boolean(control.enabled),
            resource,
            true,
        );
    }
    underwater_profile(world, out);
    if let Some(observation) =
        world.get_resource::<forge_engine::tidewater_refraction::Observation>()
    {
        out.put(
            "water/refraction_descriptor_size",
            V::Dimensions(observation.size.to_vec()),
            resource,
            true,
        );
        out.put(
            "water/refraction_cpu_sync_ready",
            V::Boolean(observation.ready),
            resource,
            false,
        );
    }
}

fn underwater_profile(world: &World, out: &mut CurrentProfileInventory) {
    let resource = Origin::MainWorldResource;
    if let Some(control) =
        world.get_resource::<forge_engine::tidewater_underwater::TidewaterUnderwaterControl>()
    {
        out.put(
            "water/underwater_enabled",
            V::Boolean(control.enabled),
            resource,
            true,
        );
        out.number(
            "water/underwater_shafts_strength",
            control.shafts_strength,
            resource,
            true,
        );
    }
    if let Some(control) =
        world.get_resource::<forge_engine::tidewater_underwater_lighting::Control>()
    {
        out.put(
            "water/underwater_lighting_enabled",
            V::Boolean(control.enabled),
            resource,
            true,
        );
        out.put(
            "water/underwater_shadow_refraction",
            V::Boolean(control.shadow_refraction),
            resource,
            true,
        );
        out.number(
            "water/underwater_caustics_strength",
            control.caustics_strength,
            resource,
            true,
        );
    }
}
