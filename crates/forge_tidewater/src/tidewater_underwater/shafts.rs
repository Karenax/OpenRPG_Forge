//! Half-resolution targets and shared photon-map binding for source shafts.
// SPDX-License-Identifier: MIT
// Tidewater adaptation; copyright 2026 DRG Software Solutions LLC, 1438b1ab.
use bevy::{
    prelude::*,
    render::{
        extract_resource::ExtractResource,
        render_asset::RenderAssets,
        render_resource::{
            self as gpu,
            binding_types::{sampler, texture_2d, uniform_buffer},
        },
        renderer::{RenderDevice, RenderQueue},
        texture::GpuImage,
    },
};

#[derive(Resource, Clone, ExtractResource)]
pub(super) struct Source {
    pub maps: crate::tidewater_caustics::CausticMaps,
    pub params: Vec4,
}

pub(super) fn layout() -> gpu::BindGroupLayoutDescriptor {
    gpu::BindGroupLayoutDescriptor::new(
        "Tidewater underwater photon maps",
        &gpu::BindGroupLayoutEntries::sequential(
            gpu::ShaderStages::FRAGMENT,
            (
                texture_2d(gpu::TextureSampleType::Float { filterable: true }),
                texture_2d(gpu::TextureSampleType::Float { filterable: true }),
                sampler(gpu::SamplerBindingType::Filtering),
                uniform_buffer::<Vec4>(false),
            ),
        ),
    )
}

#[derive(Resource)]
pub(super) struct Prepared {
    pub group: gpu::BindGroup,
    pub neutral: gpu::TextureView,
    _neutral_texture: gpu::Texture,
    _params: gpu::UniformBuffer<Vec4>,
}

pub(super) fn prepare(
    mut commands: Commands,
    source: Option<Res<Source>>,
    images: Res<RenderAssets<GpuImage>>,
    device: Res<RenderDevice>,
    queue: Res<RenderQueue>,
    pipeline_cache: Res<gpu::PipelineCache>,
    existing: Option<Res<Prepared>>,
) {
    if existing.is_some() {
        return;
    }
    let Some(source) = source else {
        return;
    };
    let (Some(fine), Some(broad)) = (
        images.get(&source.maps.fine),
        images.get(&source.maps.broad),
    ) else {
        return;
    };
    let mut params = gpu::UniformBuffer::from(source.params);
    params.write_buffer(&device, &queue);
    let Some(binding) = params.binding() else {
        return;
    };
    let group = device.create_bind_group(
        "Tidewater shaft photon binding",
        &pipeline_cache.get_bind_group_layout(&layout()),
        &gpu::BindGroupEntries::sequential((
            &fine.texture_view,
            &broad.texture_view,
            &fine.sampler,
            binding,
        )),
    );
    let neutral_texture = texture(&device, 1, 1);
    let neutral = neutral_texture.create_view(&default());
    commands.insert_resource(Prepared {
        group,
        neutral,
        _neutral_texture: neutral_texture,
        _params: params,
    });
}

pub(super) struct Target {
    pub view: gpu::TextureView,
    pub width: u32,
    pub height: u32,
    _texture: gpu::Texture,
}

impl Target {
    pub fn new(device: &RenderDevice, width: u32, height: u32) -> Self {
        let texture = texture(device, width, height);
        let view = texture.create_view(&default());
        Self {
            view,
            width,
            height,
            _texture: texture,
        }
    }
}

fn texture(device: &RenderDevice, width: u32, height: u32) -> gpu::Texture {
    device.create_texture(&gpu::TextureDescriptor {
        label: Some("Tidewater half-resolution shaft light"),
        size: gpu::Extent3d {
            width,
            height,
            depth_or_array_layers: 1,
        },
        mip_level_count: 1,
        sample_count: 1,
        dimension: gpu::TextureDimension::D2,
        format: gpu::TextureFormat::Rgba16Float,
        usage: gpu::TextureUsages::TEXTURE_BINDING | gpu::TextureUsages::RENDER_ATTACHMENT,
        view_formats: &[],
    })
}
