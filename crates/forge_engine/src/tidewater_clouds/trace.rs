//! GPU execution; source acquisition lives outside the engine.
use bevy::render::{
    render_resource as gpu,
    renderer::{RenderDevice, RenderQueue},
};
pub(super) struct Cache {
    inputs: Vec<Option<bevy::render::render_resource::TextureView>>,
    atmosphere: bevy::render::render_resource::Buffer,
    clamp: bevy::render::render_resource::Sampler,
    pipeline: bevy::render::render_resource::ComputePipeline,
    temporal: Option<bevy::render::render_resource::ComputePipeline>,
    panorama: Option<bevy::render::render_resource::ComputePipeline>,
}
pub struct Input {
    pub shader: String,
    pub atmosphere: Vec<u8>,
    pub textures: Vec<Vec<u8>>,
    pub temporal: Option<String>,
    pub panorama: Option<String>,
}
impl Input {
    #[allow(clippy::too_many_arguments, clippy::too_many_lines)]
    pub(super) fn encode(
        &self,
        device: &RenderDevice,
        queue: &RenderQueue,
        encoder: &mut gpu::CommandEncoder,
        settings: &gpu::Buffer,
        noise: &gpu::WgpuTextureView,
        weather: &gpu::WgpuTextureView,
        repeat: &gpu::WgpuSampler,
        views: [&gpu::WgpuTextureView; 3],
        frames: &[super::FrameView<'_>],
        cache: &mut Option<Cache>,
        history_views: Option<[&gpu::WgpuTextureView; 5]>,
        atmosphere_views: Option<[&gpu::WgpuTextureView; 2]>,
    ) {
        let cache = cache.get_or_insert_with(|| {
            let mut inputs = Vec::new();
            for (index, (width, height)) in
                [(256, 64), (192, 108), (64, 64)].into_iter().enumerate()
            {
                if index < 2 && atmosphere_views.is_some() {
                    inputs.push(None);
                    continue;
                }
                let format = if index == 2 {
                    gpu::TextureFormat::R8Unorm
                } else {
                    gpu::TextureFormat::Rgba16Float
                };
                let texture = device.create_texture(&gpu::TextureDescriptor {
                    label: Some("paired cloud atmosphere/noise"),
                    size: gpu::Extent3d {
                        width,
                        height,
                        depth_or_array_layers: 1,
                    },
                    mip_level_count: 1,
                    sample_count: 1,
                    dimension: gpu::TextureDimension::D2,
                    format,
                    usage: gpu::TextureUsages::TEXTURE_BINDING | gpu::TextureUsages::COPY_DST,
                    view_formats: &[],
                });
                queue.write_texture(
                    gpu::TexelCopyTextureInfo {
                        texture: &texture,
                        mip_level: 0,
                        origin: gpu::Origin3d::ZERO,
                        aspect: gpu::TextureAspect::All,
                    },
                    &self.textures[index],
                    gpu::TexelCopyBufferLayout {
                        offset: 0,
                        bytes_per_row: Some(width * if index == 2 { 1 } else { 8 }),
                        rows_per_image: Some(height),
                    },
                    gpu::Extent3d {
                        width,
                        height,
                        depth_or_array_layers: 1,
                    },
                );
                inputs.push(Some(
                    texture.create_view(&gpu::TextureViewDescriptor::default()),
                ));
            }
            let uniform = |bytes: &[u8]| {
                device.create_buffer_with_data(&gpu::BufferInitDescriptor {
                    label: Some("paired cloud trace"),
                    contents: bytes,
                    usage: gpu::BufferUsages::UNIFORM,
                })
            };
            let atmosphere = uniform(&self.atmosphere);
            let clamp = device.create_sampler(&gpu::SamplerDescriptor {
                mag_filter: gpu::FilterMode::Linear,
                min_filter: gpu::FilterMode::Linear,
                mipmap_filter: gpu::MipmapFilterMode::Linear,
                ..gpu::SamplerDescriptor::default()
            });
            let module = device.create_and_validate_shader_module(gpu::ShaderModuleDescriptor {
                label: Some("original cloud trace"),
                source: gpu::ShaderSource::Wgsl(self.shader.as_str().into()),
            });
            let pipeline = device.create_compute_pipeline(&gpu::RawComputePipelineDescriptor {
                label: Some("original cloud trace"),
                layout: None,
                module: &module,
                entry_point: Some("main"),
                compilation_options: gpu::PipelineCompilationOptions::default(),
                cache: None,
            });
            let temporal = self.temporal.as_ref().map(|source| {
                let module =
                    device.create_and_validate_shader_module(gpu::ShaderModuleDescriptor {
                        label: Some("cloud temporal"),
                        source: gpu::ShaderSource::Wgsl(source.as_str().into()),
                    });
                device.create_compute_pipeline(&gpu::RawComputePipelineDescriptor {
                    label: Some("cloud temporal"),
                    layout: None,
                    module: &module,
                    entry_point: Some("main"),
                    compilation_options: gpu::PipelineCompilationOptions::default(),
                    cache: None,
                })
            });
            let panorama = self.panorama.as_ref().map(|source| {
                let module =
                    device.create_and_validate_shader_module(gpu::ShaderModuleDescriptor {
                        label: Some("cloud panorama"),
                        source: gpu::ShaderSource::Wgsl(source.as_str().into()),
                    });
                device.create_compute_pipeline(&gpu::RawComputePipelineDescriptor {
                    label: Some("cloud panorama"),
                    layout: None,
                    module: &module,
                    entry_point: Some("main"),
                    compilation_options: gpu::PipelineCompilationOptions::default(),
                    cache: None,
                })
            });
            Cache {
                inputs,
                atmosphere,
                clamp,
                pipeline,
                temporal,
                panorama,
            }
        });
        let Cache {
            inputs,
            atmosphere,
            clamp,
            pipeline,
            temporal,
            panorama,
        } = cache;
        let uniform = |bytes: &[u8]| {
            device.create_buffer_with_data(&gpu::BufferInitDescriptor {
                label: Some("cloud step"),
                contents: bytes,
                usage: gpu::BufferUsages::UNIFORM,
            })
        };
        for frame in frames {
            if temporal.is_none() && frame.index != 119 {
                continue;
            }
            let local = uniform(frame.local);
            let global = uniform(frame.global);
            let live_atmosphere = frame.atmosphere.map(uniform);
            let atmosphere = live_atmosphere.as_ref().unwrap_or(atmosphere);
            let group0 = device.create_bind_group(
                "cloud trace frame",
                &pipeline.get_bind_group_layout(0).into(),
                &[
                    gpu::BindGroupEntry {
                        binding: 0,
                        resource: global.as_entire_binding(),
                    },
                    gpu::BindGroupEntry {
                        binding: 1,
                        resource: gpu::BindingResource::Sampler(repeat),
                    },
                    gpu::BindGroupEntry {
                        binding: 2,
                        resource: gpu::BindingResource::Sampler(clamp),
                    },
                ],
            );
            let mut entries = vec![
                gpu::BindGroupEntry {
                    binding: 0,
                    resource: local.as_entire_binding(),
                },
                gpu::BindGroupEntry {
                    binding: 1,
                    resource: settings.as_entire_binding(),
                },
                gpu::BindGroupEntry {
                    binding: 4,
                    resource: atmosphere.as_entire_binding(),
                },
            ];
            for (binding, view) in [
                (2, noise),
                (3, weather),
                (
                    5,
                    atmosphere_views.map_or_else(
                        || &**inputs[0].as_ref().expect("paired transmittance"),
                        |views| views[0],
                    ),
                ),
                (
                    6,
                    atmosphere_views.map_or_else(
                        || &**inputs[1].as_ref().expect("paired sky view"),
                        |views| views[1],
                    ),
                ),
                (7, views[0]),
                (8, &**inputs[2].as_ref().expect("blue noise")),
                (9, views[1]),
                (10, views[2]),
            ] {
                entries.push(gpu::BindGroupEntry {
                    binding,
                    resource: gpu::BindingResource::TextureView(view),
                });
            }
            let group1 = device.create_bind_group(
                "cloud trace resources",
                &pipeline.get_bind_group_layout(1).into(),
                &entries,
            );
            if frame.trace_view {
                let mut pass = encoder.begin_compute_pass(&gpu::ComputePassDescriptor {
                    label: Some("cloud volume trace"),
                    ..gpu::ComputePassDescriptor::default()
                });
                pass.set_pipeline(pipeline);
                pass.set_bind_group(0, &*group0, &[]);
                pass.set_bind_group(1, &*group1, &[]);
                pass.dispatch_workgroups(30, 17, 1);
            }
            if let (Some(panorama), Some(history)) = (&panorama, history_views) {
                let g0 = device.create_bind_group(
                    "panorama frame",
                    &panorama.get_bind_group_layout(0).into(),
                    &[
                        gpu::BindGroupEntry {
                            binding: 0,
                            resource: global.as_entire_binding(),
                        },
                        gpu::BindGroupEntry {
                            binding: 1,
                            resource: gpu::BindingResource::Sampler(repeat),
                        },
                        gpu::BindGroupEntry {
                            binding: 2,
                            resource: gpu::BindingResource::Sampler(clamp),
                        },
                    ],
                );
                let mut entries: Vec<_> = entries
                    .iter()
                    .filter(|entry| entry.binding < 8)
                    .cloned()
                    .collect();
                entries.push(gpu::BindGroupEntry {
                    binding: 8,
                    resource: gpu::BindingResource::TextureView(history[4]),
                });
                let g1 = device.create_bind_group(
                    "panorama resources",
                    &panorama.get_bind_group_layout(1).into(),
                    &entries,
                );
                let mut pass = encoder.begin_compute_pass(&gpu::ComputePassDescriptor {
                    label: Some("cloud panorama"),
                    ..gpu::ComputePassDescriptor::default()
                });
                pass.set_pipeline(panorama);
                pass.set_bind_group(0, &*g0, &[]);
                pass.set_bind_group(1, &*g1, &[]);
                if frame.panorama_full {
                    pass.dispatch_workgroups(64, 20, 1);
                } else {
                    pass.dispatch_workgroups(16, 5, 1);
                }
            }
            if frame.trace_view
                && let (Some(temporal), Some(history)) = (&temporal, history_views)
            {
                let write = frame.view_index * 2;
                let previous = (1 - frame.view_index) * 2;
                let g0 = device.create_bind_group(
                    "cloud temporal sampler",
                    &temporal.get_bind_group_layout(0).into(),
                    &[gpu::BindGroupEntry {
                        binding: 2,
                        resource: gpu::BindingResource::Sampler(clamp),
                    }],
                );
                let mut entries = vec![gpu::BindGroupEntry {
                    binding: 0,
                    resource: local.as_entire_binding(),
                }];
                for (binding, view) in [
                    (4, views[1]),
                    (5, views[2]),
                    (6, history[previous]),
                    (7, history[previous + 1]),
                    (8, history[write]),
                    (9, history[write + 1]),
                ] {
                    entries.push(gpu::BindGroupEntry {
                        binding,
                        resource: gpu::BindingResource::TextureView(view),
                    });
                }
                let g1 = device.create_bind_group(
                    "cloud temporal inputs",
                    &temporal.get_bind_group_layout(1).into(),
                    &entries,
                );
                let mut pass = encoder.begin_compute_pass(&gpu::ComputePassDescriptor {
                    label: Some("cloud temporal"),
                    ..gpu::ComputePassDescriptor::default()
                });
                pass.set_pipeline(temporal);
                pass.set_bind_group(0, &*g0, &[]);
                pass.set_bind_group(1, &*g1, &[]);
                pass.dispatch_workgroups(120, 68, 1);
            }
        }
    }
}
