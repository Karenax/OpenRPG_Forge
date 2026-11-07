//! Explicit bindings to the real resident scene, without fallback resources.
use super::WorldPlantMaterial;
use crate::{
    source_environment::scene::ResidentEnvironment,
    source_shadows::{casters::graph, heightfield},
};
use bevy::{
    prelude::*,
    render::{render_resource as gpu, storage::ShaderBuffer},
};
use std::sync::Arc;

pub(super) fn install_pbr_binding(world: &mut World) -> Result<(), String> {
    let mut images = world
        .get_resource_mut::<Assets<Image>>()
        .ok_or("World plant depth binding requires initialized image assets")?;
    if images.contains(super::PBR_DEPTH_BINDING.id()) {
        return Ok(());
    }
    let mut image = Image::new_uninit(
        gpu::Extent3d {
            width: 1,
            height: 1,
            depth_or_array_layers: 3,
        },
        gpu::TextureDimension::D2,
        gpu::TextureFormat::Depth32Float,
        bevy::asset::RenderAssetUsages::RENDER_WORLD,
    );
    image.texture_descriptor.usage =
        gpu::TextureUsages::TEXTURE_BINDING | gpu::TextureUsages::RENDER_ATTACHMENT;
    image.texture_view_descriptor = Some(gpu::TextureViewDescriptor {
        dimension: Some(gpu::TextureViewDimension::D2Array),
        ..default()
    });
    image.sampler = bevy::image::ImageSampler::Descriptor(bevy::image::ImageSamplerDescriptor {
        compare: Some(bevy::image::ImageCompareFunction::LessEqual),
        ..default()
    });
    images
        .insert(super::PBR_DEPTH_BINDING.id(), image)
        .map_err(|error| error.to_string())?;
    Ok(())
}

impl WorldPlantMaterial {
    /// Bind the admitted scene's existing light, environment and shadow targets.
    /// This validates CPU ownership and resources; it does not acknowledge GPU work.
    pub fn bind_source_scene(
        &mut self,
        world: &World,
        environment: &ResidentEnvironment,
        hill: &heightfield::Images,
        shadows: &graph::Target,
    ) -> Result<(), String> {
        let current_environment = world
            .get_resource::<ResidentEnvironment>()
            .ok_or("Source plant lighting requires its resident environment")?;
        let current_hill = world
            .get_resource::<heightfield::Images>()
            .ok_or("Source plant lighting requires its heightfield shadow generation")?;
        let current_shadows = world
            .get_resource::<graph::Target>()
            .ok_or("Source plant lighting requires its object shadow generation")?;
        let cloud = environment
            .clouds
            .images
            .get(2)
            .ok_or("Source cloud shadow target absent")?;
        if current_environment.environment != environment.environment
            || !Arc::ptr_eq(&current_environment.clouds.ready, &environment.clouds.ready)
            || current_hill.output != hill.output
            || !Arc::ptr_eq(&current_hill.ready, &hill.ready)
            || current_shadows.image != shadows.image
            || !Arc::ptr_eq(&current_shadows.ready, &shadows.ready)
        {
            return Err("Source plant lighting refuses foreign scene generations".into());
        }
        let images = world
            .get_resource::<Assets<Image>>()
            .ok_or("Source plant lighting images unavailable")?;
        for (handle, width, layers, format) in [
            (
                &environment.environment,
                128,
                6,
                gpu::TextureFormat::Rgba16Float,
            ),
            (&hill.output, 512, 1, gpu::TextureFormat::Rgba16Float),
            (
                &shadows.image,
                if shadows.settings.enabled > 0.5 {
                    2048
                } else {
                    1
                },
                3,
                gpu::TextureFormat::Depth32Float,
            ),
            (cloud, 256, 1, gpu::TextureFormat::R32Float),
        ] {
            let image = images
                .get(handle)
                .ok_or("Source plant lighting target disappeared")?;
            let descriptor = &image.texture_descriptor;
            if descriptor.size
                != (gpu::Extent3d {
                    width,
                    height: width,
                    depth_or_array_layers: layers,
                })
                || descriptor.dimension != gpu::TextureDimension::D2
                || descriptor.format != format
                || descriptor.sample_count != 1
            {
                return Err("Source plant lighting target layout differs".into());
            }
        }
        validate_buffers(world)?;
        if environment
            .cloud_view
            .iter()
            .any(|value| !value.is_finite())
        {
            return Err("Source plant cloud uniforms are non-finite".into());
        }
        self.scene_lighting = crate::source_lighting::scene_buffer::HANDLE;
        self.live_shadows = crate::source_shadows::receiver::DEFAULT_BUFFER;
        self.local_lights = crate::source_lighting::local_lights::gpu::BUFFER;
        self.environment = Some(environment.environment.clone());
        self.hill_shadow = Some(hill.output.clone());
        self.object_shadow = Some(shadows.image.clone());
        self.object_settings = shadows.settings.clone();
        self.clouds = environment.cloud_view;
        self.cloud_shadow = Some(cloud.clone());
        self.source_lighting = true;
        let settings = world.resource::<heightfield::Settings>();
        self.hill_domain = Vec4::new(settings.origin, settings.origin, settings.size, 0.0);
        Ok(())
    }
}

fn validate_buffers(world: &World) -> Result<(), String> {
    let buffers = world
        .get_resource::<Assets<ShaderBuffer>>()
        .ok_or("Source plant lighting buffers unavailable")?;
    for (handle, size) in [
        (&crate::source_lighting::scene_buffer::HANDLE, 176),
        (&crate::source_shadows::receiver::DEFAULT_BUFFER, 464),
        (
            &crate::source_lighting::local_lights::gpu::BUFFER,
            crate::source_lighting::local_lights::PACKET_BYTES,
        ),
    ] {
        if buffers
            .get(handle)
            .and_then(|buffer| buffer.data.as_ref())
            .is_none_or(|bytes| bytes.len() != size)
        {
            return Err("Source plant lighting buffer layout differs".into());
        }
    }
    Ok(())
}
