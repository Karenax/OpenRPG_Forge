// SPDX-License-Identifier: MIT
// Adapted from Tidewater `src/ocean/Caustics.js` at 1438b1ab,
// copyright 2026 DRG Software Solutions LLC. See `third_party/tidewater/LICENSE`.
//! Additive photon maps generated after the native FFT, before camera rendering.
#![allow(clippy::needless_pass_by_value)] // Bevy render-system parameters are owned wrappers.

use super::{BROAD, CausticLayerSpec, FINE, PHOTON_SHADER, PhotonParams};
use bevy::{
    asset::RenderAssetUsages,
    image::{ImageAddressMode, ImageFilterMode, ImageSampler, ImageSamplerDescriptor},
    prelude::*,
    render::{
        Render, RenderApp, RenderSystems,
        extract_resource::{ExtractResource, ExtractResourcePlugin},
        render_asset::RenderAssets,
        render_resource::{
            self as gpu, BindGroup, BindGroupEntries, RenderPipeline, TextureView, UniformBuffer,
        },
        renderer::{RenderContext, RenderDevice, RenderQueue},
        texture::GpuImage,
    },
};
use std::sync::{
    Arc,
    atomic::{AtomicU32, Ordering},
};

const MIP_SHADER: &str = r"
struct Output { @builtin(position) position:vec4f, @location(0) uv:vec2f };
@vertex fn vertex(@builtin(vertex_index) index:u32)->Output {
    let corner=vec2f(f32((index<<1u)&2u),f32(index&2u));
    var output:Output;
    output.position=vec4f(corner*2.0-1.0,0.0,1.0);
    output.uv=vec2f(corner.x,1.0-corner.y);
    return output;
}
@group(0) @binding(0) var source:texture_2d<f32>;
@group(0) @binding(1) var linear_sampler:sampler;
@fragment fn fragment(input:Output)->@location(0) vec4f {
    return textureSampleLevel(source,linear_sampler,input.uv,0.0);
}
";

#[derive(Resource, Clone, ExtractResource)]
pub struct CausticMaps {
    pub fine: Handle<Image>,
    pub broad: Handle<Image>,
    derivatives: Handle<Image>,
    fine_tile_m: f32,
    broad_tile_m: f32,
    sun_direction: Vec3,
    completed_fft_step: Arc<AtomicU32>,
    /// Counts command encodings, not completed GPU submissions.
    pub encoded_frames: Arc<AtomicU32>,
    /// Last FFT step encoded into the photon maps; this is not a GPU fence.
    pub encoded_fft_step: Arc<AtomicU32>,
}

/// Allocates the source-sized RGBA16F maps. Their R/G channels hold the two
/// focal planes, and the remaining mips are box filtered in render order.
pub fn prepare_maps(
    app: &mut App,
    derivatives: Handle<Image>,
    fine_tile_m: f32,
    broad_tile_m: f32,
    sun_direction: Vec3,
    completed_fft_step: Arc<AtomicU32>,
) -> Result<CausticMaps, String> {
    let valid_inputs = app.get_sub_app(RenderApp).is_some()
        && app.world().contains_resource::<Assets<Image>>()
        && fine_tile_m.is_finite()
        && fine_tile_m > 0.0
        && broad_tile_m.is_finite()
        && broad_tile_m > fine_tile_m
        && sun_direction.is_finite()
        && sun_direction.y > 0.0
        && (sun_direction.length_squared() - 1.0).abs() <= 0.001;
    if !valid_inputs {
        return Err("Invalid Tidewater caustic render inputs".into());
    }
    let mut images = app.world_mut().resource_mut::<Assets<Image>>();
    let fine = images.add(map_image(FINE));
    let broad = images.add(map_image(BROAD));
    let maps = CausticMaps {
        fine,
        broad,
        derivatives,
        fine_tile_m,
        broad_tile_m,
        sun_direction,
        completed_fft_step,
        encoded_frames: Arc::new(AtomicU32::new(0)),
        encoded_fft_step: Arc::new(AtomicU32::new(0)),
    };
    app.add_plugins(ExtractResourcePlugin::<CausticMaps>::default());
    app.insert_resource(maps.clone());
    app.sub_app_mut(RenderApp)
        .add_systems(Render, prepare.in_set(RenderSystems::PrepareBindGroups));
    Ok(maps)
}

fn map_image(spec: CausticLayerSpec) -> Image {
    let mut image = Image::new_uninit(
        gpu::Extent3d {
            width: spec.resolution(),
            height: spec.resolution(),
            depth_or_array_layers: 1,
        },
        gpu::TextureDimension::D2,
        gpu::TextureFormat::Rgba16Float,
        RenderAssetUsages::RENDER_WORLD,
    );
    image.texture_descriptor.mip_level_count = spec.resolution().ilog2() + 1;
    image.texture_descriptor.usage = gpu::TextureUsages::RENDER_ATTACHMENT
        | gpu::TextureUsages::TEXTURE_BINDING
        | gpu::TextureUsages::COPY_SRC;
    image.sampler = ImageSampler::Descriptor(ImageSamplerDescriptor {
        address_mode_u: ImageAddressMode::Repeat,
        address_mode_v: ImageAddressMode::Repeat,
        min_filter: ImageFilterMode::Linear,
        mag_filter: ImageFilterMode::Linear,
        mipmap_filter: ImageFilterMode::Linear,
        ..default()
    });
    image
}

struct Layer {
    base: TextureView,
    index_buffer: gpu::Buffer,
    index_count: u32,
    focal_bindings: [BindGroup; 2],
    mips: Vec<(BindGroup, TextureView)>,
}

#[derive(Resource)]
pub struct Prepared {
    photons: RenderPipeline,
    mip_pipeline: RenderPipeline,
    layers: [Layer; 2],
    // Keep the bound GPU buffers alive for as long as the bind groups exist.
    _uniforms: [Vec<UniformBuffer<PhotonParams>>; 2],
}

#[allow(clippy::too_many_arguments, clippy::too_many_lines)]
fn prepare(
    mut commands: Commands,
    maps: Option<Res<CausticMaps>>,
    images: Res<RenderAssets<GpuImage>>,
    device: Res<RenderDevice>,
    queue: Res<RenderQueue>,
    existing: Option<Res<Prepared>>,
) {
    if existing.is_some() {
        return;
    }
    let Some(maps) = maps else { return };
    let (Some(derivatives), Some(fine), Some(broad)) = (
        images.get(&maps.derivatives),
        images.get(&maps.fine),
        images.get(&maps.broad),
    ) else {
        return;
    };
    let module = device.create_and_validate_shader_module(gpu::ShaderModuleDescriptor {
        label: Some("Tidewater caustic photon splat"),
        source: gpu::ShaderSource::Wgsl(PHOTON_SHADER.into()),
    });
    let add = gpu::BlendComponent {
        src_factor: gpu::BlendFactor::One,
        dst_factor: gpu::BlendFactor::One,
        operation: gpu::BlendOperation::Add,
    };
    let photons = device.create_render_pipeline(&gpu::RawRenderPipelineDescriptor {
        label: Some("Tidewater caustic photon splat"),
        layout: None,
        vertex: gpu::RawVertexState {
            module: &module,
            entry_point: Some("vertex"),
            compilation_options: default(),
            buffers: &[],
        },
        fragment: Some(gpu::RawFragmentState {
            module: &module,
            entry_point: Some("fragment"),
            compilation_options: default(),
            targets: &[Some(gpu::ColorTargetState {
                format: gpu::TextureFormat::Rgba16Float,
                blend: Some(gpu::BlendState {
                    color: add,
                    alpha: add,
                }),
                write_mask: gpu::ColorWrites::ALL,
            })],
        }),
        primitive: gpu::PrimitiveState {
            cull_mode: None,
            ..default()
        },
        depth_stencil: None,
        multisample: default(),
        multiview_mask: None,
        cache: None,
    });
    let mip_module = device.create_and_validate_shader_module(gpu::ShaderModuleDescriptor {
        label: Some("Tidewater caustic box mips"),
        source: gpu::ShaderSource::Wgsl(MIP_SHADER.into()),
    });
    let mip_pipeline = device.create_render_pipeline(&gpu::RawRenderPipelineDescriptor {
        label: Some("Tidewater caustic box mips"),
        layout: None,
        vertex: gpu::RawVertexState {
            module: &mip_module,
            entry_point: Some("vertex"),
            compilation_options: default(),
            buffers: &[],
        },
        fragment: Some(gpu::RawFragmentState {
            module: &mip_module,
            entry_point: Some("fragment"),
            compilation_options: default(),
            targets: &[Some(gpu::ColorTargetState {
                format: gpu::TextureFormat::Rgba16Float,
                blend: None,
                write_mask: gpu::ColorWrites::ALL,
            })],
        }),
        primitive: default(),
        depth_stencil: None,
        multisample: default(),
        multiview_mask: None,
        cache: None,
    });
    let photon_layout: gpu::BindGroupLayout = photons.get_bind_group_layout(0).into();
    let mip_layout: gpu::BindGroupLayout = mip_pipeline.get_bind_group_layout(0).into();
    let mut uniforms = [Vec::new(), Vec::new()];
    let layers = [
        (FINE, fine, maps.fine_tile_m),
        (BROAD, broad, maps.broad_tile_m),
    ]
    .into_iter()
    .enumerate()
    .map(|(layer_index, (spec, image, tile_m))| {
        let base = mip_view(image, 0);
        let index_bytes: Vec<_> = spec
            .grid_indices()
            .into_iter()
            .flat_map(u32::to_le_bytes)
            .collect();
        let index_buffer = device.create_buffer_with_data(&gpu::BufferInitDescriptor {
            label: Some("Tidewater caustic shared grid"),
            contents: &index_bytes,
            usage: gpu::BufferUsages::INDEX,
        });
        let focal_bindings = std::array::from_fn(|plane| {
            let params = spec
                .parameters(4, tile_m, maps.sun_direction, plane)
                .expect("validated pinned photon parameters");
            let mut uniform = UniformBuffer::from(params);
            uniform.write_buffer(&device, &queue);
            let binding = device.create_bind_group(
                "Tidewater caustic focal plane",
                &photon_layout,
                &BindGroupEntries::sequential((
                    &derivatives.texture_view,
                    &derivatives.sampler,
                    &uniform,
                )),
            );
            uniforms[layer_index].push(uniform);
            binding
        });
        let mut mip_steps = Vec::new();
        for level in 1..=spec.resolution().ilog2() {
            let source = mip_view(image, level - 1);
            let destination = mip_view(image, level);
            let binding = device.create_bind_group(
                "Tidewater caustic mip source",
                &mip_layout,
                &BindGroupEntries::sequential((&source, &image.sampler)),
            );
            mip_steps.push((binding, destination));
        }
        Layer {
            base,
            index_buffer,
            index_count: spec.grid() * spec.grid() * 6,
            focal_bindings,
            mips: mip_steps,
        }
    })
    .collect::<Vec<_>>()
    .try_into()
    .unwrap_or_else(|_| unreachable!("two pinned caustic layers"));
    commands.insert_resource(Prepared {
        photons,
        mip_pipeline,
        layers,
        _uniforms: uniforms,
    });
}

fn mip_view(image: &GpuImage, level: u32) -> TextureView {
    image.texture.create_view(&gpu::TextureViewDescriptor {
        dimension: Some(gpu::TextureViewDimension::D2),
        base_mip_level: level,
        mip_level_count: Some(1),
        ..default()
    })
}

/// Schedule after the FFT producer set and before the camera driver.
#[expect(
    clippy::default_trait_access,
    reason = "wgpu Color is not reexported by Bevy"
)]
pub fn render_maps(
    mut context: RenderContext,
    prepared: Option<Res<Prepared>>,
    maps: Option<Res<CausticMaps>>,
    mut last_step: Local<Option<u32>>,
) {
    let (Some(prepared), Some(maps)) = (prepared, maps) else {
        return;
    };
    let step = maps.completed_fft_step.load(Ordering::Acquire);
    if step == 0 || *last_step == Some(step) {
        return;
    }
    for layer in &prepared.layers {
        let attachments = [Some(gpu::RenderPassColorAttachment {
            view: &layer.base,
            depth_slice: None,
            resolve_target: None,
            ops: gpu::Operations {
                load: gpu::LoadOp::Clear(Default::default()),
                store: gpu::StoreOp::Store,
            },
        })];
        let mut pass = context
            .command_encoder()
            .begin_render_pass(&gpu::RenderPassDescriptor {
                label: Some("Tidewater caustic photon layers"),
                color_attachments: &attachments,
                ..default()
            });
        pass.set_pipeline(&prepared.photons);
        pass.set_index_buffer(*layer.index_buffer.slice(..), gpu::IndexFormat::Uint32);
        for binding in &layer.focal_bindings {
            pass.set_bind_group(0, binding, &[]);
            pass.draw_indexed(0..layer.index_count, 0, 0..1);
        }
        drop(pass);
        for (binding, destination) in &layer.mips {
            let attachments = [Some(gpu::RenderPassColorAttachment {
                view: destination,
                depth_slice: None,
                resolve_target: None,
                ops: gpu::Operations {
                    load: gpu::LoadOp::Clear(Default::default()),
                    store: gpu::StoreOp::Store,
                },
            })];
            let mut pass =
                context
                    .command_encoder()
                    .begin_render_pass(&gpu::RenderPassDescriptor {
                        label: Some("Tidewater caustic box mip"),
                        color_attachments: &attachments,
                        ..default()
                    });
            pass.set_pipeline(&prepared.mip_pipeline);
            pass.set_bind_group(0, binding, &[]);
            pass.draw(0..3, 0..1);
        }
    }
    *last_step = Some(step);
    maps.encoded_fft_step.store(step, Ordering::Release);
    maps.encoded_frames.fetch_add(1, Ordering::Release);
}
