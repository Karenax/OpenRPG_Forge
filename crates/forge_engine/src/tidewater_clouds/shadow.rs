//! GPU execution; source acquisition lives outside the engine.
use bevy::render::{
    render_resource as gpu,
    renderer::{RenderDevice, RenderQueue},
};
pub(super) struct Cache {
    noise_view: bevy::render::render_resource::TextureView,
    sampler: bevy::render::render_resource::Sampler,
    pipeline: bevy::render::render_resource::ComputePipeline,
    trace: Option<super::trace::Cache>,
}
pub struct Input {
    pub shader: String,
    pub noise: Vec<Vec<u8>>,
    pub frames: Vec<(Vec<u8>, Vec<u8>)>,
    pub trace: Option<super::trace::Input>,
}
impl Input {
    #[allow(clippy::too_many_arguments, clippy::too_many_lines)]
    pub(super) fn encode(
        &self,
        device: &RenderDevice,
        queue: &RenderQueue,
        encoder: &mut gpu::CommandEncoder,
        settings: &gpu::Buffer,
        weather: &gpu::WgpuTextureView,
        output: &gpu::WgpuTextureView,
        trace_views: Option<[&gpu::WgpuTextureView; 3]>,
        history_views: Option<[&gpu::WgpuTextureView; 5]>,
        atmosphere_views: Option<[&gpu::WgpuTextureView; 2]>,
        frames: &[super::FrameView<'_>],
        cache: &mut Option<Cache>,
    ) {
        let cache = cache.get_or_insert_with(|| {
            let noise = device.create_texture(&gpu::TextureDescriptor {
                label: Some("original cloud noise"),
                size: gpu::Extent3d {
                    width: 64,
                    height: 64,
                    depth_or_array_layers: 64,
                },
                mip_level_count: 7,
                sample_count: 1,
                dimension: gpu::TextureDimension::D3,
                format: gpu::TextureFormat::Rgba8Unorm,
                usage: gpu::TextureUsages::TEXTURE_BINDING | gpu::TextureUsages::COPY_DST,
                view_formats: &[],
            });
            for (level, bytes) in self.noise.iter().enumerate() {
                let level = u32::try_from(level).expect("seven mips");
                let size = 64 >> level;
                queue.write_texture(
                    gpu::TexelCopyTextureInfo {
                        texture: &noise,
                        mip_level: level,
                        origin: gpu::Origin3d::ZERO,
                        aspect: gpu::TextureAspect::All,
                    },
                    bytes,
                    gpu::TexelCopyBufferLayout {
                        offset: 0,
                        bytes_per_row: Some(size * 4),
                        rows_per_image: Some(size),
                    },
                    gpu::Extent3d {
                        width: size,
                        height: size,
                        depth_or_array_layers: size,
                    },
                );
            }
            let noise_view = noise.create_view(&gpu::TextureViewDescriptor::default());
            let sampler = device.create_sampler(&gpu::SamplerDescriptor {
                address_mode_u: gpu::AddressMode::Repeat,
                address_mode_v: gpu::AddressMode::Repeat,
                address_mode_w: gpu::AddressMode::Repeat,
                mag_filter: gpu::FilterMode::Linear,
                min_filter: gpu::FilterMode::Linear,
                mipmap_filter: gpu::MipmapFilterMode::Linear,
                ..Default::default()
            });
            let module = device.create_and_validate_shader_module(gpu::ShaderModuleDescriptor {
                label: Some("cloud shadow"),
                source: gpu::ShaderSource::Wgsl(self.shader.as_str().into()),
            });
            let pipeline = device.create_compute_pipeline(&gpu::RawComputePipelineDescriptor {
                label: Some("cloud shadow"),
                layout: None,
                module: &module,
                entry_point: Some("main"),
                compilation_options: gpu::PipelineCompilationOptions::default(),
                cache: None,
            });
            Cache {
                noise_view,
                sampler,
                pipeline,
                trace: None,
            }
        });
        let Cache {
            noise_view,
            sampler,
            pipeline,
            trace: trace_cache,
        } = cache;
        for frame in frames {
            // Separate immutable buffers prevent all encoded steps reading the last upload.
            let make = |bytes: &[u8]| {
                device.create_buffer_with_data(&gpu::BufferInitDescriptor {
                    label: Some("cloud shadow frame"),
                    contents: bytes,
                    usage: gpu::BufferUsages::UNIFORM,
                })
            };
            let local = make(frame.local);
            let global = make(frame.global);
            let group0 = device.create_bind_group(
                "cloud shadow frame",
                &pipeline.get_bind_group_layout(0).into(),
                &[
                    gpu::BindGroupEntry {
                        binding: 0,
                        resource: global.as_entire_binding(),
                    },
                    gpu::BindGroupEntry {
                        binding: 1,
                        resource: gpu::BindingResource::Sampler(sampler),
                    },
                ],
            );
            let group1 = device.create_bind_group(
                "cloud shadow inputs",
                &pipeline.get_bind_group_layout(1).into(),
                &[
                    gpu::BindGroupEntry {
                        binding: 0,
                        resource: local.as_entire_binding(),
                    },
                    gpu::BindGroupEntry {
                        binding: 1,
                        resource: settings.as_entire_binding(),
                    },
                    gpu::BindGroupEntry {
                        binding: 2,
                        resource: gpu::BindingResource::TextureView(noise_view),
                    },
                    gpu::BindGroupEntry {
                        binding: 3,
                        resource: gpu::BindingResource::TextureView(weather),
                    },
                    gpu::BindGroupEntry {
                        binding: 8,
                        resource: gpu::BindingResource::TextureView(output),
                    },
                ],
            );
            let mut pass = encoder.begin_compute_pass(&gpu::ComputePassDescriptor {
                label: Some("cloud shadow"),
                ..Default::default()
            });
            pass.set_pipeline(pipeline);
            pass.set_bind_group(0, &*group0, &[]);
            pass.set_bind_group(1, &*group1, &[]);
            pass.dispatch_workgroups(32, if frame.shadow_full { 32 } else { 8 }, 1);
        }
        if let (Some(trace), Some(views)) = (&self.trace, trace_views) {
            trace.encode(
                device,
                queue,
                encoder,
                settings,
                noise_view,
                weather,
                sampler,
                views,
                frames,
                trace_cache,
                history_views,
                atmosphere_views,
            );
        }
    }
}
