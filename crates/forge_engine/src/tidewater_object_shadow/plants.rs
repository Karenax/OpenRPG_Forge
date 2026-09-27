//! Shared masked plant depth upload; caller supplies canonical instance streams.
use bevy::{
    prelude::{Mat4, Vec4, default},
    render::{
        render_resource::{
            self as gpu, BindGroup, BindGroupEntries, Buffer, RenderPipeline, ShaderType,
            UniformBuffer,
        },
        renderer::{RenderDevice, RenderQueue},
    },
};
pub struct Batch {
    pub kind: u8,
    pub vertices: Vec<u8>,
    pub indices: Vec<u8>,
    pub records: Vec<u8>,
    pub models: Vec<u8>,
    pub count: u32,
    pub range: Vec4,
}
pub struct Input {
    pub batches: Vec<Batch>,
    pub shaders: [String; 2],
    pub leaf: Vec<u8>,
    pub detail: Vec<u8>,
    pub size: u32,
    pub mips: u32,
    pub camera: Vec4,
}
pub struct GpuBatch {
    pub pipeline: RenderPipeline,
    pub vertices: Buffer,
    pub indices: Buffer,
    pub index_count: u32,
    pub count: u32,
    pub groups: Vec<BindGroup>,
}
#[derive(ShaderType)]
struct Params {
    clip: Mat4,
    main_camera: Vec4,
    shadow_camera: Vec4,
    lod_range: Vec4,
    wind_time: Vec4,
    gust_frame: Vec4,
}

impl Input {
    pub(super) fn validate(&self, matrices: &[Mat4], eyes: &[Vec4]) -> Result<(), String> {
        if !(1..=4).contains(&matrices.len())
            || matrices.len() != eyes.len()
            || matrices.iter().any(|m| !m.is_finite())
            || eyes.iter().any(|v| !v.is_finite())
            || self.size != 1024
            || self.mips != 11
            || self.leaf.len() != 5_592_404
            || self.detail.len() != 512 * 512 * 4
            || !self.camera.is_finite()
            || self
                .shaders
                .iter()
                .any(|s| s.trim().is_empty() || s.len() > 1_000_000)
            || self.batches.len() > 64
        {
            return Err("Invalid plant shadow inputs".into());
        }
        let mut total = 0_usize;
        for b in &self.batches {
            if b.kind > 1
                || !(1..=65536).contains(&b.count)
                || b.vertices.is_empty()
                || b.vertices.len() > 8_000_000
                || !b.vertices.len().is_multiple_of(80)
                || b.indices.is_empty()
                || b.indices.len() > 8_000_000
                || !b.indices.len().is_multiple_of(12)
                || b.records.len() != b.count as usize * 32
                || b.models.len() != b.count as usize * 64
                || !b.range.is_finite()
                || [&b.vertices, &b.records, &b.models]
                    .into_iter()
                    .any(|v| !super::finite_bytes(v))
                || b.indices
                    .as_chunks::<4>()
                    .0
                    .iter()
                    .any(|v| u32::from_le_bytes(*v) as usize >= b.vertices.len() / 80)
            {
                return Err("Invalid plant shadow batch".into());
            }
            total += b.vertices.len() + b.indices.len() + b.records.len() + b.models.len();
            if total > 256 * 1024 * 1024 {
                return Err("Plant shadow budget exceeded".into());
            }
        }
        Ok(())
    }
}
#[allow(clippy::too_many_lines)] // Uploads and explicit bind groups for the two source mask programs.
pub fn prepare(
    p: &Input,
    matrices: &[Mat4],
    eyes: &[Vec4],
    device: &RenderDevice,
    queue: &RenderQueue,
) -> Result<Vec<GpuBatch>, String> {
    p.validate(matrices, eyes)?;
    let detail = device.create_texture(&gpu::TextureDescriptor {
        label: Some("source gust detail"),
        size: gpu::Extent3d {
            width: 512,
            height: 512,
            depth_or_array_layers: 1,
        },
        mip_level_count: 1,
        sample_count: 1,
        dimension: gpu::TextureDimension::D2,
        format: gpu::TextureFormat::Rgba8Unorm,
        usage: gpu::TextureUsages::TEXTURE_BINDING | gpu::TextureUsages::COPY_DST,
        view_formats: &[],
    });
    queue.write_texture(
        detail.as_image_copy(),
        &p.detail,
        gpu::TexelCopyBufferLayout {
            offset: 0,
            bytes_per_row: Some(2048),
            rows_per_image: Some(512),
        },
        detail.size(),
    );
    let detail_view = detail.create_view(&default());
    let linear = device.create_sampler(&gpu::SamplerDescriptor {
        address_mode_u: gpu::AddressMode::Repeat,
        address_mode_v: gpu::AddressMode::Repeat,
        mag_filter: gpu::FilterMode::Linear,
        min_filter: gpu::FilterMode::Linear,
        ..default()
    });
    let texture = device.create_texture(&gpu::TextureDescriptor {
        label: Some("source leaf mask"),
        size: gpu::Extent3d {
            width: p.size,
            height: p.size,
            depth_or_array_layers: 1,
        },
        mip_level_count: p.mips,
        sample_count: 1,
        dimension: gpu::TextureDimension::D2,
        format: gpu::TextureFormat::Rgba8Unorm,
        usage: gpu::TextureUsages::TEXTURE_BINDING | gpu::TextureUsages::COPY_DST,
        view_formats: &[],
    });
    let mut offset = 0;
    for mip in 0..p.mips {
        let size = p.size >> mip;
        let len = (size * size * 4) as usize;
        queue.write_texture(
            gpu::TexelCopyTextureInfo {
                texture: &texture,
                mip_level: mip,
                origin: default(),
                aspect: gpu::TextureAspect::All,
            },
            &p.leaf[offset..offset + len],
            gpu::TexelCopyBufferLayout {
                offset: 0,
                bytes_per_row: Some(size * 4),
                rows_per_image: Some(size),
            },
            gpu::Extent3d {
                width: size,
                height: size,
                depth_or_array_layers: 1,
            },
        );
        offset += len;
    }
    let texture_view = texture.create_view(&default());
    let sampler = device.create_sampler(&gpu::SamplerDescriptor {
        address_mode_u: gpu::AddressMode::Repeat,
        address_mode_v: gpu::AddressMode::Repeat,
        mag_filter: gpu::FilterMode::Linear,
        min_filter: gpu::FilterMode::Linear,
        mipmap_filter: gpu::MipmapFilterMode::Linear,
        anisotropy_clamp: 8,
        ..default()
    });
    Ok(p.batches
        .iter()
        .map(|b| {
            let pipeline = pipeline(&p.shaders[usize::from(b.kind)], device);
            let layout: bevy::render::render_resource::BindGroupLayout =
                pipeline.get_bind_group_layout(0).into();
            let buffer = |bytes: &[u8], usage| {
                device.create_buffer_with_data(&gpu::BufferInitDescriptor {
                    label: Some("shadow plants"),
                    contents: bytes,
                    usage,
                })
            };
            let records = buffer(&b.records, gpu::BufferUsages::STORAGE);
            let models = buffer(&b.models, gpu::BufferUsages::STORAGE);
            let groups = matrices
                .iter()
                .zip(eyes)
                .map(|(&clip, &shadow_camera)| {
                    let mut uniform = UniformBuffer::from(Params {
                        clip,
                        main_camera: p.camera,
                        shadow_camera,
                        lod_range: b.range,
                        wind_time: Vec4::new(1.0, 0.0, 6.0, 2.0),
                        gust_frame: Vec4::new(0.0, 0.0, 124.0, 0.0),
                    });
                    uniform.write_buffer(device, queue);
                    if b.kind == 1 {
                        device.create_bind_group(
                            "canopy shadow",
                            &layout,
                            &BindGroupEntries::sequential((
                                &uniform,
                                records.as_entire_binding(),
                                models.as_entire_binding(),
                                &texture_view,
                                &sampler,
                                &detail_view,
                                &linear,
                            )),
                        )
                    } else {
                        device.create_bind_group(
                            "plant shadow",
                            &layout,
                            &BindGroupEntries::with_indices((
                                (0, &uniform),
                                (1, records.as_entire_binding()),
                                (2, models.as_entire_binding()),
                                (5, &detail_view),
                                (6, &linear),
                            )),
                        )
                    }
                })
                .collect();
            GpuBatch {
                pipeline,
                vertices: buffer(&b.vertices, gpu::BufferUsages::VERTEX),
                indices: buffer(&b.indices, gpu::BufferUsages::INDEX),
                index_count: u32::try_from(b.indices.len() / 4).expect("validated triangles"),
                count: b.count,
                groups,
            }
        })
        .collect())
}

fn pipeline(code: &str, device: &RenderDevice) -> RenderPipeline {
    let shader = device.create_and_validate_shader_module(gpu::ShaderModuleDescriptor {
        label: Some("source plant shadow mask"),
        source: gpu::ShaderSource::Wgsl(code.into()),
    });
    let attributes = [
        (0, 0, gpu::VertexFormat::Float32x3),
        (1, 12, gpu::VertexFormat::Float32x3),
        (2, 24, gpu::VertexFormat::Float32x2),
        (3, 32, gpu::VertexFormat::Float32x4),
        (4, 48, gpu::VertexFormat::Float32x4),
        (5, 64, gpu::VertexFormat::Float32x4),
    ]
    .map(|(shader_location, offset, format)| gpu::VertexAttribute {
        format,
        offset,
        shader_location,
    });
    device.create_render_pipeline(&gpu::RawRenderPipelineDescriptor {
        label: Some("source plant shadow"),
        layout: None,
        vertex: gpu::RawVertexState {
            module: &shader,
            entry_point: Some("vertex"),
            compilation_options: default(),
            buffers: &[gpu::RawVertexBufferLayout {
                array_stride: 80,
                step_mode: gpu::VertexStepMode::Vertex,
                attributes: &attributes,
            }],
        },
        primitive: gpu::PrimitiveState {
            cull_mode: None,
            ..default()
        },
        depth_stencil: Some(gpu::DepthStencilState {
            format: gpu::TextureFormat::Depth32Float,
            depth_write_enabled: Some(true),
            depth_compare: Some(gpu::CompareFunction::LessEqual),
            stencil: default(),
            bias: gpu::DepthBiasState {
                constant: 2,
                slope_scale: 1.5,
                clamp: 0.0,
            },
        }),
        multisample: default(),
        fragment: Some(gpu::RawFragmentState {
            module: &shader,
            entry_point: Some("fragment"),
            compilation_options: default(),
            targets: &[],
        }),
        multiview_mask: None,
        cache: None,
    })
}
