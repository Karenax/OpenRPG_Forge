// SPDX-License-Identifier: MIT
// Adapted from Tidewater src/engine/gpu/Mipmaps.js, copyright 2026 DRG Software Solutions LLC.
// Revision 1438b1abfcaee3267092b75573014f4d9b4a983c; see third_party/tidewater/LICENSE.
//! Sprite mipmaps encoded with the common source GPU downsampling shader.

use bevy::{
    core_pipeline::schedule::camera_driver,
    prelude::*,
    render::{
        RenderApp,
        extract_resource::{ExtractResource, ExtractResourcePlugin},
        render_asset::RenderAssets,
        renderer::{RenderContext, RenderDevice, RenderGraph},
        texture::GpuImage,
    },
};
use std::sync::{
    Arc,
    atomic::{AtomicBool, Ordering},
};

/// Images generated for one effect generation. Readiness means encoded before
/// the camera pass, not that the GPU has completed the work.
#[derive(Resource, Clone, ExtractResource)]
pub struct SpriteTextures {
    pub puff: Handle<Image>,
    pub dots: Handle<Image>,
    pub ready: Arc<AtomicBool>,
}

impl SpriteTextures {
    /// Stage fresh procedural source textures without reading an asset archive.
    pub fn stage(images: &mut Assets<Image>) -> Result<Self, &'static str> {
        let mut image = |side: u32, base: Vec<u8>| {
            let mips = side.ilog2() + 1;
            let mut data = base;
            for level in 1..mips {
                let width = (side >> level) as usize;
                data.resize(data.len() + width * width * 4, 0);
            }
            let mut image = crate::source_spray_render::sprite_texture(side, mips, data)?;
            image.texture_descriptor.usage |=
                wgpu::TextureUsages::RENDER_ATTACHMENT | wgpu::TextureUsages::COPY_SRC;
            Ok::<_, &'static str>(images.add(image))
        };
        Ok(Self {
            puff: image(64, crate::source_spray_textures::puff_rgba8())?,
            dots: image(128, crate::source_spray_textures::dots_rgba8())?,
            ready: Arc::new(AtomicBool::new(false)),
        })
    }
}

#[derive(Resource)]
struct Registered;

#[derive(SystemSet, Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub struct SpriteMipSet;

/// Register the GPU stage once before staging or replacing sprite textures.
pub fn register(app: &mut App) -> Result<(), &'static str> {
    if app.world().contains_resource::<Registered>() {
        return Ok(());
    }
    if app.get_sub_app(RenderApp).is_none() || !app.world().contains_resource::<Assets<Image>>() {
        return Err("Sprite mipmaps require initialized renderer image assets");
    }
    app.add_plugins(ExtractResourcePlugin::<SpriteTextures>::default());
    app.sub_app_mut(RenderApp).add_systems(
        RenderGraph,
        generate.in_set(SpriteMipSet).before(camera_driver),
    );
    app.insert_resource(Registered);
    Ok(())
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy render systems require owned system parameter wrappers."
)]
fn generate(
    mut context: RenderContext,
    shared: Option<Res<SpriteTextures>>,
    images: Res<RenderAssets<GpuImage>>,
    device: Res<RenderDevice>,
    mut pipeline: Local<Option<SpriteMipPipeline>>,
) {
    let Some(shared) = shared else {
        return;
    };
    if shared.ready.load(Ordering::Acquire) {
        return;
    }
    let (Some(puff), Some(dots)) = (images.get(&shared.puff), images.get(&shared.dots)) else {
        return;
    };
    let pipeline = pipeline.get_or_insert_with(|| SpriteMipPipeline::new(device.wgpu_device()));
    for image in [puff, dots] {
        if let Err(error) = pipeline.encode(
            device.wgpu_device(),
            context.command_encoder(),
            &image.texture,
        ) {
            error!("Unable to generate source sprite mipmaps: {error}");
            return;
        }
    }
    shared.ready.store(true, Ordering::Release);
}

/// GPU downsampling using the same source mip shader as authored terrain.
pub struct SpriteMipPipeline {
    pipeline: wgpu::RenderPipeline,
    sampler: wgpu::Sampler,
}

impl SpriteMipPipeline {
    #[must_use]
    pub fn new(device: &wgpu::Device) -> Self {
        let shader = device.create_shader_module(wgpu::ShaderModuleDescriptor {
            label: Some("Source sprite mipmaps"),
            source: wgpu::ShaderSource::Wgsl(crate::source_terrain::normals::MIPS.into()),
        });
        let pipeline = device.create_render_pipeline(&wgpu::RenderPipelineDescriptor {
            label: Some("Source sprite mipmaps"),
            layout: None,
            vertex: wgpu::VertexState {
                module: &shader,
                entry_point: Some("vs"),
                compilation_options: wgpu::PipelineCompilationOptions::default(),
                buffers: &[],
            },
            fragment: Some(wgpu::FragmentState {
                module: &shader,
                entry_point: Some("fs"),
                compilation_options: wgpu::PipelineCompilationOptions::default(),
                targets: &[Some(wgpu::ColorTargetState {
                    format: wgpu::TextureFormat::Rgba8Unorm,
                    blend: None,
                    write_mask: wgpu::ColorWrites::ALL,
                })],
            }),
            primitive: wgpu::PrimitiveState::default(),
            depth_stencil: None,
            multisample: wgpu::MultisampleState::default(),
            multiview_mask: None,
            cache: None,
        });
        let sampler = device.create_sampler(&wgpu::SamplerDescriptor {
            label: Some("Source mip linear clamp"),
            address_mode_u: wgpu::AddressMode::ClampToEdge,
            address_mode_v: wgpu::AddressMode::ClampToEdge,
            mag_filter: wgpu::FilterMode::Linear,
            min_filter: wgpu::FilterMode::Linear,
            mipmap_filter: wgpu::MipmapFilterMode::Nearest,
            ..Default::default()
        });
        Self { pipeline, sampler }
    }

    /// Encode the complete chain after the base level has been uploaded.
    pub fn encode(
        &self,
        device: &wgpu::Device,
        encoder: &mut wgpu::CommandEncoder,
        texture: &wgpu::Texture,
    ) -> Result<(), &'static str> {
        let side = texture.width();
        if side == 0
            || !side.is_power_of_two()
            || side > 4096
            || texture.height() != side
            || texture.depth_or_array_layers() != 1
            || texture.dimension() != wgpu::TextureDimension::D2
            || texture.format() != wgpu::TextureFormat::Rgba8Unorm
            || texture.sample_count() != 1
            || texture.mip_level_count() != side.ilog2() + 1
            || !texture.usage().contains(
                wgpu::TextureUsages::TEXTURE_BINDING | wgpu::TextureUsages::RENDER_ATTACHMENT,
            )
        {
            return Err("Invalid sprite mip texture");
        }
        let layout = self.pipeline.get_bind_group_layout(0);
        for level in 1..texture.mip_level_count() {
            let view = |mip| {
                texture.create_view(&wgpu::TextureViewDescriptor {
                    dimension: Some(wgpu::TextureViewDimension::D2),
                    base_mip_level: mip,
                    mip_level_count: Some(1),
                    ..Default::default()
                })
            };
            let source = view(level - 1);
            let target = view(level);
            let group = device.create_bind_group(&wgpu::BindGroupDescriptor {
                label: Some("Source sprite mipmaps"),
                layout: &layout,
                entries: &[
                    wgpu::BindGroupEntry {
                        binding: 0,
                        resource: wgpu::BindingResource::TextureView(&source),
                    },
                    wgpu::BindGroupEntry {
                        binding: 1,
                        resource: wgpu::BindingResource::Sampler(&self.sampler),
                    },
                ],
            });
            let attachments = [Some(wgpu::RenderPassColorAttachment {
                view: &target,
                depth_slice: None,
                resolve_target: None,
                ops: wgpu::Operations {
                    load: wgpu::LoadOp::Clear(wgpu::Color::TRANSPARENT),
                    store: wgpu::StoreOp::Store,
                },
            })];
            let mut pass = encoder.begin_render_pass(&wgpu::RenderPassDescriptor {
                label: Some("Source sprite mipmaps"),
                color_attachments: &attachments,
                ..Default::default()
            });
            pass.set_pipeline(&self.pipeline);
            pass.set_bind_group(0, &group, &[]);
            pass.draw(0..3, 0..1);
        }
        Ok(())
    }
}
