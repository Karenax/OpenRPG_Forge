//! Resident masked depth geometry; the shared cascade graph owns pass submission.
use super::MaskedMesh;
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

#[derive(Clone, Copy, PartialEq)]
pub struct Pose {
    pub model: Mat4,
    /// Source distance cull, independent of the main camera frustum.
    pub enabled: bool,
}

#[derive(ShaderType)]
struct Uniforms {
    clip_from_world: Mat4,
    world_from_local: Mat4,
    clock: Vec4,
}

pub struct Draw {
    pub vertices: Buffer,
    pub indices: Buffer,
    pub count: u32,
    pub enabled: bool,
    pub groups: Vec<BindGroup>,
    uniforms: Vec<UniformBuffer<Uniforms>>,
}

pub struct Batch {
    pub pipeline: RenderPipeline,
    pub draws: Vec<Draw>,
}

fn validate(poses: &[Pose], matrices: &[Mat4], time: f32) -> Result<(), String> {
    if poses.len() > 4096
        || poses.iter().any(|pose| !pose.model.is_finite())
        || matrices.len() != 3
        || matrices.iter().any(|matrix| !matrix.is_finite())
        || !time.is_finite()
        || time < 0.0
    {
        return Err("Invalid masked fish shadow pose or cascades".into());
    }
    Ok(())
}

impl Batch {
    /// Change uniforms and caster flags without reallocating geometry or bind groups.
    ///
    /// # Errors
    /// Rejects changed inventory, invalid matrices/layers and non-finite time/poses.
    pub fn update(
        &mut self,
        poses: &[Pose],
        matrices: &[Mat4],
        layers: &[usize],
        time: f32,
        device: &RenderDevice,
        queue: &RenderQueue,
    ) -> Result<(), String> {
        validate(poses, matrices, time)?;
        if poses.len() != self.draws.len()
            || layers.iter().any(|layer| *layer >= 3)
            || self.draws.iter().any(|draw| draw.uniforms.len() != 3)
        {
            return Err("Masked fish shadow resident layout changed".into());
        }
        for (draw, pose) in self.draws.iter_mut().zip(poses) {
            draw.enabled = pose.enabled;
            if !pose.enabled {
                continue;
            }
            for &layer in layers {
                let uniform = &mut draw.uniforms[layer];
                uniform.get_mut().clip_from_world = matrices[layer];
                uniform.get_mut().world_from_local = pose.model;
                uniform.get_mut().clock.x = time;
                uniform.write_buffer(device, queue);
            }
        }
        Ok(())
    }
}

/// Upload the exact posed position/fin streams and original fragment mask.
///
/// # Errors
/// Rejects invalid poses, stream bounds, indices, triangle counts and non-finite data.
// Keep validation before any GPU allocation and the fixed pipeline layout together.
#[allow(clippy::too_many_lines)]
pub fn prepare(
    shader_source: &str,
    meshes: &[MaskedMesh],
    poses: &[Pose],
    matrices: &[Mat4],
    time: f32,
    device: &RenderDevice,
    queue: &RenderQueue,
) -> Result<Batch, String> {
    validate(poses, matrices, time)?;
    if meshes.len() != poses.len()
        || meshes.iter().any(|mesh| {
            mesh.vertices.is_empty()
                || mesh.vertices.len() > 65536 * 28
                || !mesh.vertices.len().is_multiple_of(28)
                || mesh.indices.is_empty()
                || mesh.indices.len() > 8_000_000
                || !mesh.indices.len().is_multiple_of(12)
                || mesh
                    .vertices
                    .as_chunks::<4>()
                    .0
                    .iter()
                    .any(|value| !f32::from_le_bytes(*value).is_finite())
                || mesh
                    .indices
                    .as_chunks::<4>()
                    .0
                    .iter()
                    .any(|value| u32::from_le_bytes(*value) as usize >= mesh.vertices.len() / 28)
        })
        || meshes
            .iter()
            .map(|mesh| mesh.vertices.len() + mesh.indices.len())
            .sum::<usize>()
            > 256 * 1024 * 1024
    {
        return Err("Invalid masked fish shadow upload".into());
    }
    let shader = device.create_and_validate_shader_module(gpu::ShaderModuleDescriptor {
        label: Some("Tidewater masked fish depth"),
        source: gpu::ShaderSource::Wgsl(shader_source.into()),
    });
    let pipeline = device.create_render_pipeline(&gpu::RawRenderPipelineDescriptor {
        label: Some("Tidewater masked fish depth"),
        layout: None,
        vertex: gpu::RawVertexState {
            module: &shader,
            entry_point: Some("vertex"),
            compilation_options: default(),
            buffers: &[gpu::RawVertexBufferLayout {
                array_stride: 28,
                step_mode: gpu::VertexStepMode::Vertex,
                attributes: &[
                    gpu::VertexAttribute {
                        format: gpu::VertexFormat::Float32x3,
                        offset: 0,
                        shader_location: 0,
                    },
                    gpu::VertexAttribute {
                        format: gpu::VertexFormat::Float32x4,
                        offset: 12,
                        shader_location: 1,
                    },
                ],
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
    let draws = meshes
        .iter()
        .zip(poses)
        .map(|(mesh, pose)| {
            let uniforms = matrices
                .iter()
                .map(|matrix| {
                    let mut uniform = UniformBuffer::from(Uniforms {
                        clip_from_world: *matrix,
                        world_from_local: pose.model,
                        clock: Vec4::new(time, 0.0, 0.0, 0.0),
                    });
                    uniform.write_buffer(device, queue);
                    uniform
                })
                .collect::<Vec<_>>();
            let groups = uniforms
                .iter()
                .map(|uniform| {
                    device.create_bind_group(
                        "Masked fish depth pose",
                        &layout,
                        &BindGroupEntries::single(uniform),
                    )
                })
                .collect();
            Ok(Draw {
                vertices: device.create_buffer_with_data(&gpu::BufferInitDescriptor {
                    label: Some("Masked fish depth vertices"),
                    contents: &mesh.vertices,
                    usage: gpu::BufferUsages::VERTEX,
                }),
                indices: device.create_buffer_with_data(&gpu::BufferInitDescriptor {
                    label: Some("Masked fish depth indices"),
                    contents: &mesh.indices,
                    usage: gpu::BufferUsages::INDEX,
                }),
                count: u32::try_from(mesh.indices.len() / 4).map_err(|error| error.to_string())?,
                enabled: pose.enabled,
                groups,
                uniforms,
            })
        })
        .collect::<Result<Vec<_>, String>>()?;
    Ok(Batch { pipeline, draws })
}
