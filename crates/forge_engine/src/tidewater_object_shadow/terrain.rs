//! Shared terrain depth upload and main-view morph parameters.
pub mod authored;
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
pub struct Input {
    pub heights: Vec<u8>,
    pub edit_side: u32,
    pub edits: Vec<u8>,
    pub nodes: Vec<u8>,
    pub count: u32,
    pub shader: String,
    pub view_position: Vec4,
    pub morph: [Vec4; 9],
}
impl Input {
    pub(super) fn validate(&self, matrices: &[Mat4]) -> Result<(), String> {
        if !(1..=4).contains(&matrices.len())
            || matrices.iter().any(|m| !m.is_finite())
            || self.heights.len() != 2048 * 2048 * 4
            || !matches!(self.edit_side, 1 | 2049)
            || self.edits.len() != self.edit_side as usize * self.edit_side as usize * 4
            || self.count > 1500
            || self.nodes.len() != self.count as usize * 16
            || self.shader.trim().is_empty()
            || self.shader.len() > 1_000_000
            || !self.view_position.is_finite()
            || self.morph.iter().any(|v| !v.is_finite())
            || [&self.heights, &self.edits, &self.nodes]
                .into_iter()
                .any(|b| !super::finite_bytes(b))
        {
            return Err("Invalid terrain shadow inputs".into());
        }
        Ok(())
    }
}
pub struct Stream {
    pub nodes: Vec<u8>,
    pub count: u32,
    pub view_position: Vec4,
    pub morph: [Vec4; 9],
}
impl Stream {
    pub fn validate(&self) -> Result<(), String> {
        if self.count > 1500
            || self.nodes.len() != self.count as usize * 16
            || !self.view_position.is_finite()
            || self.morph.iter().any(|v| !v.is_finite())
            || !super::finite_bytes(&self.nodes)
        {
            return Err("Invalid terrain shadow stream".into());
        }
        Ok(())
    }
}
pub struct Gpu {
    pub pipeline: RenderPipeline,
    pub grid: Buffer,
    pub nodes: Buffer,
    pub indices: Buffer,
    pub count: u32,
    pub groups: Vec<BindGroup>,
    uniforms: Vec<UniformBuffer<Params>>,
}
const NODE_CAPACITY: usize = 1500;
#[derive(ShaderType)]
struct Params {
    view_projection: Mat4,
    view_position: Vec4,
    morph: [Vec4; 9],
}

impl Gpu {
    pub fn update_cascades(
        &mut self,
        matrices: &[Mat4],
        layers: &[usize],
        device: &RenderDevice,
        queue: &RenderQueue,
    ) -> Result<(), String> {
        if matrices.len() != 3
            || matrices.iter().any(|matrix| !matrix.is_finite())
            || layers.iter().any(|&layer| layer >= matrices.len())
            || self.uniforms.len() != matrices.len()
        {
            return Err("Invalid terrain cascade update".into());
        }
        for &layer in layers {
            let uniform = &mut self.uniforms[layer];
            uniform.get_mut().view_projection = matrices[layer];
            uniform.write_buffer(device, queue);
        }
        Ok(())
    }
    pub fn update_stream(
        &mut self,
        stream: &Stream,
        device: &RenderDevice,
        queue: &RenderQueue,
    ) -> Result<(), String> {
        stream.validate()?;
        queue.write_buffer(&self.nodes, 0, &stream.nodes);
        self.count = stream.count;
        for uniform in &mut self.uniforms {
            let params = uniform.get_mut();
            params.view_position = stream.view_position;
            params.morph = stream.morph;
            uniform.write_buffer(device, queue);
        }
        Ok(())
    }
}

#[allow(clippy::too_many_lines, clippy::many_single_char_names)] // Explicit GPU descriptors and grid corner indices.
pub fn prepare(
    t: &Input,
    matrices: &[Mat4],
    device: &RenderDevice,
    queue: &RenderQueue,
) -> Result<Gpu, String> {
    t.validate(matrices)?;
    let shader = device.create_and_validate_shader_module(gpu::ShaderModuleDescriptor {
        label: Some("Tidewater terrain shadow"),
        source: gpu::ShaderSource::Wgsl(t.shader.clone().into()),
    });
    let pipeline = device.create_render_pipeline(&gpu::RawRenderPipelineDescriptor {
        label: Some("Tidewater terrain shadow"),
        layout: None,
        vertex: gpu::RawVertexState {
            module: &shader,
            entry_point: Some("vertex"),
            compilation_options: default(),
            buffers: &[
                gpu::RawVertexBufferLayout {
                    array_stride: 8,
                    step_mode: gpu::VertexStepMode::Vertex,
                    attributes: &[gpu::VertexAttribute {
                        format: gpu::VertexFormat::Float32x2,
                        offset: 0,
                        shader_location: 0,
                    }],
                },
                gpu::RawVertexBufferLayout {
                    array_stride: 16,
                    step_mode: gpu::VertexStepMode::Instance,
                    attributes: &[gpu::VertexAttribute {
                        format: gpu::VertexFormat::Float32x4,
                        offset: 0,
                        shader_location: 1,
                    }],
                },
            ],
        },
        primitive: gpu::PrimitiveState {
            cull_mode: Some(gpu::Face::Back),
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
        fragment: None,
        multiview_mask: None,
        cache: None,
    });
    let texture = device.create_texture(&gpu::TextureDescriptor {
        label: Some("Tidewater shadow heightfield"),
        size: gpu::Extent3d {
            width: 2048,
            height: 2048,
            depth_or_array_layers: 1,
        },
        mip_level_count: 1,
        sample_count: 1,
        dimension: gpu::TextureDimension::D2,
        format: gpu::TextureFormat::R32Float,
        usage: gpu::TextureUsages::TEXTURE_BINDING | gpu::TextureUsages::COPY_DST,
        view_formats: &[],
    });
    queue.write_texture(
        texture.as_image_copy(),
        &t.heights,
        gpu::TexelCopyBufferLayout {
            offset: 0,
            bytes_per_row: Some(8192),
            rows_per_image: Some(2048),
        },
        texture.size(),
    );
    let view = texture.create_view(&default());
    let edit_texture = device.create_texture(&gpu::TextureDescriptor {
        label: Some("Tidewater author terrain deltas"),
        size: gpu::Extent3d {
            width: t.edit_side,
            height: t.edit_side,
            depth_or_array_layers: 1,
        },
        mip_level_count: 1,
        sample_count: 1,
        dimension: gpu::TextureDimension::D2,
        format: gpu::TextureFormat::R32Float,
        usage: gpu::TextureUsages::TEXTURE_BINDING | gpu::TextureUsages::COPY_DST,
        view_formats: &[],
    });
    queue.write_texture(
        edit_texture.as_image_copy(),
        &t.edits,
        gpu::TexelCopyBufferLayout {
            offset: 0,
            bytes_per_row: Some(t.edit_side * 4),
            rows_per_image: Some(t.edit_side),
        },
        edit_texture.size(),
    );
    let edit_view = edit_texture.create_view(&default());
    let layout: bevy::render::render_resource::BindGroupLayout =
        pipeline.get_bind_group_layout(0).into();
    let uniforms = matrices
        .iter()
        .map(|&view_projection| {
            let mut uniform = UniformBuffer::from(Params {
                view_projection,
                view_position: t.view_position,
                morph: t.morph,
            });
            uniform.write_buffer(device, queue);
            uniform
        })
        .collect::<Vec<_>>();
    let groups = uniforms
        .iter()
        .map(|uniform| {
            device.create_bind_group(
                "terrain shadow draw",
                &layout,
                &BindGroupEntries::sequential((uniform, &view, &edit_view)),
            )
        })
        .collect();
    let grid: Vec<u8> = (0..=40_u16)
        .flat_map(|z| (0..=40_u16).flat_map(move |x| [f32::from(x) / 40.0, f32::from(z) / 40.0]))
        .flat_map(f32::to_le_bytes)
        .collect();
    let indices: Vec<u8> = (0..40_u32)
        .flat_map(|z| {
            (0..40_u32).flat_map(move |x| {
                let a = z * 41 + x;
                let b = a + 1;
                let c = a + 41;
                let d = c + 1;
                if (x + z).is_multiple_of(2) {
                    [a, c, b, b, c, d]
                } else {
                    [a, c, d, a, d, b]
                }
            })
        })
        .flat_map(u32::to_le_bytes)
        .collect();
    let buffer = |bytes: &[u8], usage| {
        device.create_buffer_with_data(&gpu::BufferInitDescriptor {
            label: Some("shadow terrain grid"),
            contents: bytes,
            usage,
        })
    };
    Ok(Gpu {
        pipeline,
        grid: buffer(&grid, gpu::BufferUsages::VERTEX),
        nodes: buffer(
            &[
                t.nodes.as_slice(),
                &vec![0_u8; (NODE_CAPACITY - t.count as usize) * 16],
            ]
            .concat(),
            gpu::BufferUsages::VERTEX | gpu::BufferUsages::COPY_DST,
        ),
        indices: buffer(&indices, gpu::BufferUsages::INDEX),
        count: t.count,
        groups,
        uniforms,
    })
}
