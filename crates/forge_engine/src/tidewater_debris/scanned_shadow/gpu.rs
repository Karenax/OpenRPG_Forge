//! Resident near-LOD prototypes and per-placement depth uniforms on Bevy's device.
use super::{MeshBytes, Pose};
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

#[derive(ShaderType)]
struct Uniforms {
    clip_from_world: Mat4,
    world_from_local: Mat4,
    data: Vec4,
    frame_index: u32,
    pad0: u32,
    pad1: u32,
    pad2: u32,
}
pub struct Geometry {
    pub vertices: Buffer,
    pub indices: Buffer,
    pub count: u32,
}
pub struct Draw {
    pub asset: usize,
    pub enabled: bool,
    pub groups: Vec<BindGroup>,
    uniforms: Vec<UniformBuffer<Uniforms>>,
}
pub struct Batch {
    pub pipeline: RenderPipeline,
    pub geometry: Vec<Geometry>,
    pub draws: Vec<Draw>,
}

fn validate(poses: &[Pose], matrices: &[Mat4]) -> Result<(), String> {
    if poses.len() > 16_384
        || matrices.len() != 3
        || matrices.iter().any(|m| !m.is_finite())
        || poses.iter().any(|p| {
            p.asset >= 4
                || !p.model.is_finite()
                || p.data.iter().any(|v| !v.is_finite())
                || p.data[0].to_bits() != f32::from(p.asset).to_bits()
                || !(0.0..=1.0).contains(&p.data[2])
                || ![0.0, 1.0].contains(&p.data[3])
        })
    {
        return Err("Invalid scanned depth poses or cascades".into());
    }
    Ok(())
}
impl Batch {
    /// Update only the cascade layers encoded by the shared graph.
    /// # Errors
    /// Refuses changed inventory, malformed records and invalid cascade layers.
    pub fn update(
        &mut self,
        poses: &[Pose],
        matrices: &[Mat4],
        layers: &[usize],
        frame_index: u32,
        device: &RenderDevice,
        queue: &RenderQueue,
    ) -> Result<(), String> {
        validate(poses, matrices)?;
        if poses.len() != self.draws.len() || layers.iter().any(|&l| l >= 3) {
            return Err("Scanned depth resident population differs".into());
        }
        for (draw, pose) in self.draws.iter_mut().zip(poses) {
            draw.asset = usize::from(pose.asset);
            draw.enabled = pose.enabled;
            for &layer in layers {
                let uniform = &mut draw.uniforms[layer];
                let value = uniform.get_mut();
                value.clip_from_world = matrices[layer];
                value.world_from_local = pose.model;
                value.data = Vec4::from_array(pose.data);
                value.frame_index = frame_index;
                uniform.write_buffer(device, queue);
            }
        }
        Ok(())
    }
}

/// Upload four shared source prototypes and allocate placement uniforms once.
/// # Errors
/// Refuses malformed geometry, poses, cascade matrices and upload budgets.
#[allow(
    clippy::too_many_lines,
    reason = "Keep preflight and the fixed depth pipeline ABI in one adapter."
)]
pub fn prepare(
    shader: &str,
    meshes: &[MeshBytes],
    poses: &[Pose],
    matrices: &[Mat4],
    frame_index: u32,
    device: &RenderDevice,
    queue: &RenderQueue,
) -> Result<Batch, String> {
    validate(poses, matrices)?;
    if meshes.len() != 4
        || meshes.iter().any(|m| {
            m.vertices.is_empty()
                || m.vertices.len() > 2_000_000
                || !m.vertices.len().is_multiple_of(12)
                || m.indices.is_empty()
                || m.indices.len() > 2_000_000
                || !m.indices.len().is_multiple_of(12)
                || m.vertices
                    .as_chunks::<4>()
                    .0
                    .iter()
                    .any(|v| !f32::from_le_bytes(*v).is_finite())
                || m.indices
                    .as_chunks::<4>()
                    .0
                    .iter()
                    .any(|v| u32::from_le_bytes(*v) as usize >= m.vertices.len() / 12)
        })
    {
        return Err("Invalid scanned near-LOD depth geometry".into());
    }
    let shader = device.create_and_validate_shader_module(gpu::ShaderModuleDescriptor {
        label: Some("Scanned source LOD depth"),
        source: gpu::ShaderSource::Wgsl(shader.into()),
    });
    let pipeline = device.create_render_pipeline(&gpu::RawRenderPipelineDescriptor {
        label: Some("Scanned source LOD depth"),
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
        fragment: Some(gpu::RawFragmentState {
            module: &shader,
            entry_point: Some("fragment"),
            compilation_options: default(),
            targets: &[],
        }),
        multiview_mask: None,
        cache: None,
    });
    let layout: gpu::BindGroupLayout = pipeline.get_bind_group_layout(0).into();
    let geometry = meshes
        .iter()
        .map(|mesh| {
            Ok(Geometry {
                vertices: device.create_buffer_with_data(&gpu::BufferInitDescriptor {
                    label: Some("Scanned near positions"),
                    contents: &mesh.vertices,
                    usage: gpu::BufferUsages::VERTEX,
                }),
                indices: device.create_buffer_with_data(&gpu::BufferInitDescriptor {
                    label: Some("Scanned near indices"),
                    contents: &mesh.indices,
                    usage: gpu::BufferUsages::INDEX,
                }),
                count: u32::try_from(mesh.indices.len() / 4).map_err(|e| e.to_string())?,
            })
        })
        .collect::<Result<Vec<_>, String>>()?;
    let draws = poses
        .iter()
        .map(|pose| {
            let uniforms: Vec<_> = matrices
                .iter()
                .map(|matrix| {
                    let mut uniform = UniformBuffer::from(Uniforms {
                        clip_from_world: *matrix,
                        world_from_local: pose.model,
                        data: Vec4::from_array(pose.data),
                        frame_index,
                        pad0: 0,
                        pad1: 0,
                        pad2: 0,
                    });
                    uniform.write_buffer(device, queue);
                    uniform
                })
                .collect();
            let groups = uniforms
                .iter()
                .map(|u| {
                    device.create_bind_group(
                        "Scanned depth pose",
                        &layout,
                        &BindGroupEntries::single(u),
                    )
                })
                .collect();
            Draw {
                asset: usize::from(pose.asset),
                enabled: pose.enabled,
                groups,
                uniforms,
            }
        })
        .collect();
    Ok(Batch {
        pipeline,
        geometry,
        draws,
    })
}
