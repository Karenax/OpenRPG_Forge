//! Shared opaque caster preparation on Bevy's render device.
//! Geometry is validated before upload; authoring adapters own placement and cascades.
use bevy::{
    camera::primitives::{Aabb, Frustum},
    math::{Affine3A, Vec3A, primitives::ViewFrustum},
    prelude::*,
    render::{
        render_resource::{
            self as gpu, BindGroup, BindGroupEntries, Buffer, RenderPipeline, UniformBuffer,
        },
        renderer::{RenderDevice, RenderQueue},
    },
};

pub struct OpaqueDraw {
    vertices: Vec<u8>,
    indices: Vec<u8>,
    model: Mat4,
    bounds: Aabb,
}
impl OpaqueDraw {
    /// Preserve the resident triangle mesh positions/winding for an opaque caster.
    /// The caller owns material eligibility and the authored world transform.
    pub fn from_mesh(mesh: &Mesh, model: Mat4) -> Result<Self, String> {
        if mesh.primitive_topology()
            != bevy::render::render_resource::PrimitiveTopology::TriangleList
        {
            return Err("Opaque caster requires triangle-list geometry".into());
        }
        let Some(bevy::mesh::VertexAttributeValues::Float32x3(positions)) =
            mesh.attribute(Mesh::ATTRIBUTE_POSITION)
        else {
            return Err("Opaque caster requires float32 XYZ positions".into());
        };
        let vertices = positions
            .iter()
            .flatten()
            .flat_map(|value| value.to_le_bytes())
            .collect();
        let indices = match mesh.indices() {
            Some(indices) => indices
                .iter()
                .map(u32::try_from)
                .collect::<Result<Vec<_>, _>>(),
            None => (0..positions.len())
                .map(u32::try_from)
                .collect::<Result<Vec<_>, _>>(),
        }
        .map_err(|_| "Opaque caster index exceeds u32")?
        .into_iter()
        .flat_map(u32::to_le_bytes)
        .collect();
        Self::new(vertices, indices, model)
    }

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
        let bounds = Aabb::enclosing(vertices.as_chunks::<12>().0.iter().map(|point| {
            let [x, y, z] = point.as_chunks::<4>().0.try_into().expect("XYZ positions");
            Vec3::new(
                f32::from_le_bytes(x),
                f32::from_le_bytes(y),
                f32::from_le_bytes(z),
            )
        }))
        .expect("validated non-empty shadow geometry");
        Ok(Self {
            vertices,
            indices,
            model,
            bounds,
        })
    }
}
struct GpuDraw {
    vertices: Buffer,
    indices: Buffer,
    count: u32,
    groups: Vec<BindGroup>,
    models: Vec<UniformBuffer<Mat4>>,
    bounds: Aabb,
}
pub struct OpaqueBatch {
    pub(super) bundles: Vec<wgpu::RenderBundle>,
    draws: Vec<GpuDraw>,
    cascades: Vec<UniformBuffer<Mat4>>,
    visible_draws: Vec<Vec<usize>>,
    pipeline: RenderPipeline,
}
impl OpaqueBatch {
    pub fn update_cascades(
        &mut self,
        matrices: &[Mat4],
        layers: &[usize],
        device: &RenderDevice,
        queue: &RenderQueue,
    ) -> Result<(), String> {
        self.update_poses_and_cascades(matrices, layers, None, device, queue)
    }

    pub fn update_poses_and_cascades(
        &mut self,
        matrices: &[Mat4],
        layers: &[usize],
        models: Option<&[Mat4]>,
        device: &RenderDevice,
        queue: &RenderQueue,
    ) -> Result<(), String> {
        if matrices.len() != 3
            || self.cascades.len() != matrices.len()
            || matrices.iter().any(|matrix| !matrix.is_finite())
            || layers.iter().any(|&layer| layer >= matrices.len())
        {
            return Err("Invalid opaque cascade update".into());
        }
        if let Some(models) = models {
            validate_models(models, self.draws.len())?;
        }
        if self
            .draws
            .iter()
            .any(|draw| draw.models.len() != matrices.len())
        {
            return Err("Opaque cascade layout changed".into());
        }
        let _profile = crate::cpu_timing::Span::new("shadows.opaque_uniforms");
        for &layer in layers {
            let matrix = matrices[layer];
            let mut changed =
                write_changed_matrix(&mut self.cascades[layer], matrix, device, queue);
            if let Some(models) = models {
                for (draw, &model) in self.draws.iter_mut().zip(models) {
                    changed |= write_changed_matrix(&mut draw.models[layer], model, device, queue);
                }
            }
            if changed && !self.draws.is_empty() {
                let selected = select_draws(&self.draws, layer, &matrix);
                if self.visible_draws[layer] != selected {
                    self.bundles[layer] =
                        encode_bundle(device, &self.pipeline, &self.draws, layer, &selected);
                    self.visible_draws[layer] = selected;
                }
            }
        }
        Ok(())
    }
}

fn write_changed_matrix(
    uniform: &mut UniformBuffer<Mat4>,
    matrix: Mat4,
    device: &RenderDevice,
    queue: &RenderQueue,
) -> bool {
    if uniform.get().to_cols_array().map(f32::to_bits) != matrix.to_cols_array().map(f32::to_bits) {
        uniform.set(matrix);
        uniform.write_buffer(device, queue);
        true
    } else {
        false
    }
}

pub(crate) fn validate_models(models: &[Mat4], count: usize) -> Result<(), String> {
    if models.len() != count || models.iter().any(|model| !model.is_finite()) {
        return Err("Live opaque caster poses differ from resident draws".into());
    }
    Ok(())
}

const SHADER: &str = "
@group(0) @binding(0) var<uniform> view_projection: mat4x4f;
@group(0) @binding(1) var<uniform> model: mat4x4f;

@vertex fn vertex(@location(0) position: vec3f) -> @builtin(position) vec4f {
    let world = model * vec4f(position, 1.0);
    return view_projection * world;
}";

#[allow(
    clippy::too_many_lines,
    reason = "The shadow pipeline and its resource layout are prepared together."
)]
pub fn prepare(
    draws: &[OpaqueDraw],
    matrices: &[Mat4],
    device: &RenderDevice,
    queue: &RenderQueue,
) -> Result<OpaqueBatch, String> {
    validate(draws, matrices)?;
    let shader = device.create_and_validate_shader_module(gpu::ShaderModuleDescriptor {
        label: Some("Source opaque shadow"),
        source: gpu::ShaderSource::Wgsl(SHADER.into()),
    });
    let pipeline = device.create_render_pipeline(&gpu::RawRenderPipelineDescriptor {
        label: Some("Source opaque shadow"),
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
    let cascades: Vec<_> = matrices
        .iter()
        .map(|&matrix| {
            let mut uniform = UniformBuffer::from(matrix);
            uniform.write_buffer(device, queue);
            uniform
        })
        .collect();
    let draws: Vec<_> = draws
        .iter()
        .map(|d| {
            let make = |bytes: &[u8], usage| {
                device.create_buffer_with_data(&gpu::BufferInitDescriptor {
                    label: Some("Source shadow geometry"),
                    contents: bytes,
                    usage,
                })
            };
            // Each layer retains its last submitted pose when a frame omits models.
            let models: Vec<_> = matrices
                .iter()
                .map(|_| {
                    let mut uniform = UniformBuffer::from(d.model);
                    uniform.write_buffer(device, queue);
                    uniform
                })
                .collect();
            let groups = models
                .iter()
                .zip(&cascades)
                .map(|(model, cascade)| {
                    device.create_bind_group(
                        "Source shadow draw",
                        &layout,
                        &BindGroupEntries::sequential((cascade, model)),
                    )
                })
                .collect();
            GpuDraw {
                vertices: make(&d.vertices, gpu::BufferUsages::VERTEX),
                indices: make(&d.indices, gpu::BufferUsages::INDEX),
                count: u32::try_from(d.indices.len() / 4).expect("validated indices"),
                groups,
                models,
                bounds: d.bounds,
            }
        })
        .collect();
    let visible_draws: Vec<_> = matrices
        .iter()
        .enumerate()
        .map(|(layer, matrix)| select_draws(&draws, layer, matrix))
        .collect();
    let bundles = if draws.is_empty() {
        Vec::new()
    } else {
        visible_draws
            .iter()
            .enumerate()
            .map(|(layer, selected)| encode_bundle(device, &pipeline, &draws, layer, selected))
            .collect()
    };
    Ok(OpaqueBatch {
        bundles,
        draws,
        cascades,
        visible_draws,
        pipeline,
    })
}

fn select_draws(draws: &[GpuDraw], layer: usize, matrix: &Mat4) -> Vec<usize> {
    let frustum = Frustum(ViewFrustum::from_clip_from_world(matrix));
    draws
        .iter()
        .enumerate()
        .filter_map(|(index, draw)| {
            in_cascade_side_planes(&frustum, *draw.models[layer].get(), draw.bounds)
                .then_some(index)
        })
        .collect()
}

fn in_cascade_side_planes(frustum: &Frustum, model: Mat4, mut bounds: Aabb) -> bool {
    if model.row(3) != Vec4::W || !bounds.center.is_finite() || !bounds.half_extents.is_finite() {
        return true;
    }
    // The source uses standard depth. Only cull against the four side planes
    // and keep casters touching a plane despite rounding of their mesh bounds.
    bounds.half_extents +=
        (bounds.center.abs() + bounds.half_extents + Vec3A::ONE) * (32.0 * f32::EPSILON);
    frustum.intersects_obb(&bounds, &Affine3A::from_mat4(model), false, false)
}

fn encode_bundle(
    device: &RenderDevice,
    pipeline: &RenderPipeline,
    draws: &[GpuDraw],
    layer: usize,
    selected: &[usize],
) -> wgpu::RenderBundle {
    let mut encoder = device.create_render_bundle_encoder(&wgpu::RenderBundleEncoderDescriptor {
        label: Some("Source opaque shadow cascade"),
        color_formats: &[],
        depth_stencil: Some(wgpu::RenderBundleDepthStencil {
            format: gpu::TextureFormat::Depth32Float,
            depth_read_only: false,
            stencil_read_only: true,
        }),
        sample_count: 1,
        multiview: None,
    });
    encoder.set_pipeline(pipeline);
    for &index in selected {
        let draw = &draws[index];
        encoder.set_bind_group(0, &*draw.groups[layer], &[]);
        encoder.set_vertex_buffer(0, *draw.vertices.slice(..));
        encoder.set_index_buffer(*draw.indices.slice(..), gpu::IndexFormat::Uint32);
        encoder.draw_indexed(0..draw.count, 0, 0..1);
    }
    encoder.finish(&wgpu::RenderBundleDescriptor::default())
}

pub mod plant_shader;
pub mod plants;
pub mod terrain;
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
pub(crate) mod governed;
pub mod graph;
pub mod live;
pub mod masked;
pub mod scene;
pub mod textured;

#[cfg(test)]
mod pose_tests {
    use super::*;

    #[test]
    fn cascade_culling_keeps_boundary_casters_and_ignores_depth_range() {
        let frustum = Frustum(ViewFrustum::from_clip_from_world(&Mat4::IDENTITY));
        let bounds = Aabb::from_min_max(Vec3::splat(-0.25), Vec3::splat(0.25));
        assert!(!in_cascade_side_planes(
            &frustum,
            Mat4::from_translation(Vec3::new(2.0, 0.0, 0.5)),
            bounds
        ));
        assert!(in_cascade_side_planes(
            &frustum,
            Mat4::from_translation(Vec3::new(1.25, 0.0, 0.5)),
            bounds
        ));
        assert!(in_cascade_side_planes(
            &frustum,
            Mat4::from_translation(Vec3::new(0.0, 0.0, 5.0)),
            bounds
        ));
    }

    #[test]
    fn resident_mesh_caster_preserves_positions_winding_and_authored_pose() {
        use bevy::{asset::RenderAssetUsages, mesh::Indices};
        let positions: Vec<[f32; 3]> = vec![[0.0, 1.0, 2.0], [3.0, 4.0, 5.0], [6.0, 7.0, 8.0]];
        let mesh = Mesh::new(
            gpu::PrimitiveTopology::TriangleList,
            RenderAssetUsages::default(),
        )
        .with_inserted_attribute(Mesh::ATTRIBUTE_POSITION, positions.clone())
        .with_inserted_indices(Indices::U16(vec![2, 0, 1]));
        let pose = Mat4::from_rotation_translation(
            Quat::from_rotation_y(1.45),
            Vec3::new(49.9, 0.25, -74.6),
        );
        let draw = OpaqueDraw::from_mesh(&mesh, pose).unwrap();
        assert_eq!(
            draw.vertices,
            positions
                .iter()
                .flatten()
                .flat_map(|v| v.to_le_bytes())
                .collect::<Vec<_>>()
        );
        assert_eq!(
            draw.indices,
            [2_u32, 0, 1]
                .into_iter()
                .flat_map(u32::to_le_bytes)
                .collect::<Vec<_>>()
        );
        assert_eq!(draw.model, pose);
        let mut bad = mesh.clone();
        bad.insert_indices(Indices::U32(vec![0, 1, 3]));
        assert!(OpaqueDraw::from_mesh(&bad, pose).is_err());
        let mut bad = mesh;
        bad.insert_attribute(Mesh::ATTRIBUTE_POSITION, vec![[f32::NAN, 0.0, 0.0]; 3]);
        assert!(OpaqueDraw::from_mesh(&bad, pose).is_err());
        let lines = Mesh::new(
            gpu::PrimitiveTopology::LineList,
            RenderAssetUsages::default(),
        )
        .with_inserted_attribute(Mesh::ATTRIBUTE_POSITION, positions);
        assert!(OpaqueDraw::from_mesh(&lines, pose).is_err());
    }

    #[test]
    fn resident_pose_updates_refuse_cardinality_changes_and_nonfinite_matrices() {
        let poses = [Mat4::IDENTITY, Mat4::from_translation(Vec3::Y)];
        assert!(validate_models(&poses, 2).is_ok());
        assert!(validate_models(&poses, 1).is_err());
        assert!(validate_models(&poses, 3).is_err());
        assert!(validate_models(&[], 0).is_ok());
        assert!(validate_models(&[], 1).is_err());
        let mut invalid = poses;
        invalid[1].w_axis.x = f32::NAN;
        assert!(validate_models(&invalid, 2).is_err());
        invalid[1].w_axis.x = f32::INFINITY;
        assert!(validate_models(&invalid, 2).is_err());
    }
}
