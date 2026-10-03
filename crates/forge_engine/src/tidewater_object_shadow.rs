//! Shared opaque caster preparation on Bevy's render device.
//! Geometry is validated before upload; authoring adapters own placement and cascades.
use bevy::{
    prelude::*,
    render::{
        render_resource::{
            self as gpu, BindGroup, BindGroupEntries, Buffer, RenderPipeline, ShaderType,
            UniformBuffer,
        },
        renderer::{RenderDevice, RenderQueue},
    },
};

pub struct OpaqueDraw {
    vertices: Vec<u8>,
    indices: Vec<u8>,
    model: Mat4,
}
impl OpaqueDraw {
    pub fn new(vertices: Vec<u8>, indices: Vec<u8>, model: Mat4) -> Result<Self, String> {
        if vertices.is_empty()
            || !vertices.len().is_multiple_of(12)
            || indices.is_empty()
            || !indices.len().is_multiple_of(12)
            || vertices.len() > 32_000_000
            || indices.len() > 8_000_000
            || vertices
                .as_chunks::<4>()
                .0
                .iter()
                .any(|v| !f32::from_le_bytes(*v).is_finite())
            || indices
                .as_chunks::<4>()
                .0
                .iter()
                .any(|v| u32::from_le_bytes(*v) as usize >= vertices.len() / 12)
        {
            return Err("Invalid opaque shadow geometry".into());
        }
        if !model.is_finite() {
            return Err("Invalid opaque caster transform".into());
        }
        Ok(Self {
            vertices,
            indices,
            model,
        })
    }
}
#[derive(ShaderType)]
struct Uniforms {
    view_projection: Mat4,
    model: Mat4,
}
pub struct GpuDraw {
    pub vertices: Buffer,
    pub indices: Buffer,
    pub count: u32,
    pub groups: Vec<BindGroup>,
    uniforms: Vec<UniformBuffer<Uniforms>>,
}
pub struct OpaqueBatch {
    pub pipeline: RenderPipeline,
    pub draws: Vec<GpuDraw>,
}
impl OpaqueBatch {
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
        {
            return Err("Invalid opaque cascade update".into());
        }
        for draw in &mut self.draws {
            if draw.uniforms.len() != matrices.len() {
                return Err("Opaque cascade layout changed".into());
            }
            for &layer in layers {
                let uniform = &mut draw.uniforms[layer];
                uniform.get_mut().view_projection = matrices[layer];
                uniform.write_buffer(device, queue);
            }
        }
        Ok(())
    }
}

const SHADER: &str = "struct Params{view_projection:mat4x4f,model:mat4x4f};\n@group(0) @binding(0) var<uniform> params:Params;\n@vertex fn vertex(@location(0) position:vec3f)->@builtin(position) vec4f {let world=params.model*vec4f(position,1.0);return params.view_projection*world;}";

pub fn prepare(
    draws: &[OpaqueDraw],
    matrices: &[Mat4],
    device: &RenderDevice,
    queue: &RenderQueue,
) -> Result<OpaqueBatch, String> {
    validate(draws, matrices)?;
    let shader = device.create_and_validate_shader_module(gpu::ShaderModuleDescriptor {
        label: Some("Tidewater opaque shadow"),
        source: gpu::ShaderSource::Wgsl(SHADER.into()),
    });
    let pipeline = device.create_render_pipeline(&gpu::RawRenderPipelineDescriptor {
        label: Some("Tidewater opaque shadow"),
        layout: None,
        vertex: gpu::RawVertexState {
            module: &shader,
            entry_point: Some("vertex"),
            compilation_options: default(),
            buffers: &[gpu::RawVertexBufferLayout {
                array_stride: 12,
                step_mode: gpu::VertexStepMode::Vertex,
                attributes: &[gpu::VertexAttribute {
                    format: gpu::VertexFormat::Float32x3,
                    offset: 0,
                    shader_location: 0,
                }],
            }],
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
    let layout: bevy::render::render_resource::BindGroupLayout =
        pipeline.get_bind_group_layout(0).into();
    let draws = draws
        .iter()
        .map(|d| {
            let make = |bytes: &[u8], usage| {
                device.create_buffer_with_data(&gpu::BufferInitDescriptor {
                    label: Some("Tidewater shadow geometry"),
                    contents: bytes,
                    usage,
                })
            };
            let uniforms = matrices
                .iter()
                .map(|&view_projection| {
                    let mut uniform = UniformBuffer::from(Uniforms {
                        view_projection,
                        model: d.model,
                    });
                    uniform.write_buffer(device, queue);
                    uniform
                })
                .collect::<Vec<_>>();
            let groups = uniforms
                .iter()
                .map(|uniform| {
                    device.create_bind_group(
                        "Tidewater shadow draw",
                        &layout,
                        &BindGroupEntries::single(uniform),
                    )
                })
                .collect();
            GpuDraw {
                vertices: make(&d.vertices, gpu::BufferUsages::VERTEX),
                indices: make(&d.indices, gpu::BufferUsages::INDEX),
                count: u32::try_from(d.indices.len() / 4).expect("validated indices"),
                groups,
                uniforms,
            }
        })
        .collect();
    Ok(OpaqueBatch { pipeline, draws })
}

pub mod plant_shader;
pub mod plants;
pub mod terrain;
pub mod wind;
fn finite_bytes(bytes: &[u8]) -> bool {
    bytes.len().is_multiple_of(4)
        && bytes
            .as_chunks::<4>()
            .0
            .iter()
            .all(|v| f32::from_le_bytes(*v).is_finite())
}

fn validate(draws: &[OpaqueDraw], matrices: &[Mat4]) -> Result<(), String> {
    if (matrices.is_empty() && !draws.is_empty())
        || matrices.len() > 4
        || matrices.iter().any(|m| !m.is_finite())
        || draws.len() > 4096
        || draws
            .iter()
            .map(|d| d.vertices.len() + d.indices.len())
            .sum::<usize>()
            > 256 * 1024 * 1024
    {
        return Err("Invalid opaque shadow batch or cascades".into());
    }
    Ok(())
}
pub mod graph;
pub mod live;
pub mod receiver;
pub mod scene;
