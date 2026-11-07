//! Owned, derived static HLOD geometry. The caller groups a compatible material
//! and shader contract; this module never edits source meshes or collision data.
use bevy::{
    mesh::{Indices, MeshVertexAttributeId, VertexAttributeValues},
    prelude::*,
    render::render_resource::{PrimitiveTopology, VertexFormat},
};
use forge_world::mesh_hlod::{self, ErrorChannel, MeshSource, ReductionError};
use std::collections::BTreeMap;

#[cfg(test)]
mod tests;

const MAX_VERTICES: usize = 100_000;
const MAX_INDICES: usize = 600_000;
const MAX_SOURCE_BYTES: u64 = 16 * 1024 * 1024;
const MAX_WORKING_BYTES: u64 = 256 * 1024 * 1024;
const MAX_PARTS: usize = 4096;

/// All errors are measured after transformation, in the aggregate's metric frame.
/// UV tolerance is an explicit caller choice, not a material-baking guarantee.
#[derive(Clone, Copy, Debug)]
pub struct HlodGeometryPolicy {
    pub maximum_position_error_m: f64,
    pub maximum_normal_delta: f32,
    pub maximum_uv_delta: f32,
}

pub struct HlodPrimitive<'a> {
    pub mesh: &'a Mesh,
    pub pose: Transform,
}

#[derive(Debug)]
pub struct HlodGeometryStats {
    pub source_primitives: usize,
    pub source_vertices: usize,
    pub source_triangles: usize,
    pub output_vertices: usize,
    pub output_triangles: usize,
    pub maximum_position_error_m: f64,
    pub maximum_attribute_deltas: Vec<(&'static str, f64)>,
    pub minimum_face_alignment: f64,
    pub triangle_checks: usize,
    /// Packed vertex/index byte lengths, excluding allocator and renderer overhead.
    pub output_buffer_bytes: u64,
}

pub struct PreparedHlodGeometry {
    pub mesh: Mesh,
    pub statistics: HlodGeometryStats,
}

#[derive(Debug)]
pub enum HlodGeometryError {
    Empty,
    InvalidPolicy,
    InvalidPose,
    Unsupported(&'static str),
    BudgetExceeded,
    Reduction(ReductionError),
    MeshOperation(String),
}

impl std::fmt::Display for HlodGeometryError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "static HLOD geometry: {self:?}")
    }
}
impl std::error::Error for HlodGeometryError {}

/// Validate every part and preparation bound before cloning. All attributes,
/// including custom discrete shader data, are retained from original vertices.
/// Skinning/morph targets and incompatible layouts require another explicit adapter.
pub fn prepare_aggregate(
    parts: &[HlodPrimitive<'_>],
    policy: HlodGeometryPolicy,
) -> Result<PreparedHlodGeometry, HlodGeometryError> {
    let first = parts.first().ok_or(HlodGeometryError::Empty)?;
    if !policy.maximum_position_error_m.is_finite()
        || !(0.0001..=16.0).contains(&policy.maximum_position_error_m)
        || [policy.maximum_normal_delta, policy.maximum_uv_delta]
            .into_iter()
            .any(|delta| !delta.is_finite() || !(0.0..=0.25).contains(&delta))
    {
        return Err(HlodGeometryError::InvalidPolicy);
    }
    if parts.len() > MAX_PARTS {
        return Err(HlodGeometryError::BudgetExceeded);
    }
    validate_mesh(first.mesh)?;
    let layout = layout(first.mesh);
    let mut vertices = 0;
    let mut indices = 0;
    let mut bytes = 0_u64;
    for part in parts {
        validate_mesh(part.mesh)?;
        let pose = part.pose;
        if !pose.translation.is_finite()
            || !pose.scale.is_finite()
            || pose.scale.min_element() <= 0.0
            || !pose.rotation.is_finite()
            || !pose.rotation.is_normalized()
        {
            return Err(HlodGeometryError::InvalidPose);
        }
        if part
            .mesh
            .attribute(Mesh::ATTRIBUTE_POSITION)
            .and_then(VertexAttributeValues::as_float3)
            .is_some_and(|positions| {
                positions.iter().any(|position| {
                    !pose
                        .transform_point(Vec3::from_array(*position))
                        .is_finite()
                })
            })
        {
            return Err(HlodGeometryError::InvalidPose);
        }
        if layout != self::layout(part.mesh) {
            return Err(HlodGeometryError::Unsupported(
                "incompatible vertex layouts",
            ));
        }
        vertices += part.mesh.count_vertices();
        indices += part
            .mesh
            .indices()
            .map_or(part.mesh.count_vertices(), Indices::len);
        bytes = bytes
            .checked_add(buffer_bytes(part.mesh))
            .ok_or(HlodGeometryError::BudgetExceeded)?;
        if vertices > MAX_VERTICES || indices > MAX_INDICES || bytes > MAX_SOURCE_BYTES {
            return Err(HlodGeometryError::BudgetExceeded);
        }
    }
    // Includes source clones, triangle-expanded attribute compaction and the
    // domain's bounded adjacency/signature work. This is not process/GPU memory.
    let expanded = indices as u64 * (first.mesh.get_vertex_size() + 4);
    let work = bytes * 3 + expanded * 2 + vertices as u64 * 640 + indices as u64 * 32;
    if work > MAX_WORKING_BYTES {
        return Err(HlodGeometryError::BudgetExceeded);
    }
    let mut aggregate: Option<Mesh> = None;
    for part in parts {
        let mut mesh = part.mesh.clone();
        mesh.try_transform_by(part.pose)
            .map_err(|error| HlodGeometryError::MeshOperation(error.to_string()))?;
        // Normalize indexed and non-indexed primitives to one merge representation.
        let indices = mesh.indices().map_or_else(
            || (0..u32::try_from(mesh.count_vertices()).expect("bounded vertices")).collect(),
            |indices| {
                indices
                    .iter()
                    .map(|index| u32::try_from(index).expect("bounded indices"))
                    .collect()
            },
        );
        mesh.insert_indices(Indices::U32(indices));
        // Weld each primitive independently so non-indexed triangles recover
        // their shared topology without joining two separate source objects.
        mesh.try_duplicate_vertices()
            .map_err(|error| HlodGeometryError::MeshOperation(error.to_string()))?;
        mesh.merge_duplicate_vertices()
            .map_err(|error| HlodGeometryError::MeshOperation(error.to_string()))?;
        if let Some(aggregate) = &mut aggregate {
            aggregate
                .merge(&mesh)
                .map_err(|error| HlodGeometryError::MeshOperation(error.to_string()))?;
        } else {
            aggregate = Some(mesh);
        }
    }
    let mesh = aggregate.ok_or(HlodGeometryError::Empty)?;
    reduce_mesh(mesh, parts.len(), vertices, policy)
}

fn layout(mesh: &Mesh) -> Vec<(MeshVertexAttributeId, VertexFormat)> {
    mesh.attributes()
        .map(|(attribute, values)| (attribute.id, VertexFormat::from(values)))
        .collect()
}

pub(super) fn validate_mesh(mesh: &Mesh) -> Result<(), HlodGeometryError> {
    if mesh.try_attributes().is_err() || mesh.try_indices_option().is_err() {
        return Err(HlodGeometryError::Unsupported(
            "source CPU buffers are no longer retained",
        ));
    }
    let morph = mesh.try_has_morph_targets().map_err(|_| {
        HlodGeometryError::Unsupported("source morph buffers are no longer retained")
    })?;
    if mesh.primitive_topology() != PrimitiveTopology::TriangleList
        || mesh.count_vertices() == 0
        || mesh.attributes().count() > 16
        || mesh
            .attribute(Mesh::ATTRIBUTE_POSITION)
            .and_then(VertexAttributeValues::as_float3)
            .is_none_or(|positions| positions.iter().flatten().any(|value| !value.is_finite()))
    {
        return Err(HlodGeometryError::Unsupported(
            "requires a finite float32 triangle mesh",
        ));
    }
    if mesh.count_vertices() > MAX_VERTICES {
        return Err(HlodGeometryError::BudgetExceeded);
    }
    if mesh.contains_attribute(Mesh::ATTRIBUTE_JOINT_INDEX)
        || mesh.contains_attribute(Mesh::ATTRIBUTE_JOINT_WEIGHT)
        || morph
    {
        return Err(HlodGeometryError::Unsupported(
            "animated geometry requires its own projection",
        ));
    }
    let n = mesh.count_vertices();
    let indices = mesh.indices().map_or(n, Indices::len);
    if indices > MAX_INDICES {
        return Err(HlodGeometryError::BudgetExceeded);
    }
    if indices == 0
        || !indices.is_multiple_of(3)
        || mesh
            .indices()
            .is_some_and(|indices| indices.iter().any(|index| index >= n))
        || mesh.attributes().any(|(_, values)| values.len() != n)
    {
        return Err(HlodGeometryError::Unsupported(
            "invalid vertex or index buffers",
        ));
    }
    Ok(())
}

pub(super) fn buffer_bytes(mesh: &Mesh) -> u64 {
    mesh.get_vertex_buffer_size() as u64
        + mesh
            .get_index_buffer_bytes()
            .map_or(0, |bytes| bytes.len() as u64)
}

fn reduce_mesh(
    mut mesh: Mesh,
    primitives: usize,
    source_vertices: usize,
    policy: HlodGeometryPolicy,
) -> Result<PreparedHlodGeometry, HlodGeometryError> {
    let n = mesh.count_vertices();
    let source_indices: Vec<_> = mesh
        .indices()
        .ok_or(HlodGeometryError::Unsupported("missing aggregate indices"))?
        .iter()
        .map(|index| u32::try_from(index).expect("bounded aggregate vertices"))
        .collect();
    let positions = mesh
        .attribute(Mesh::ATTRIBUTE_POSITION)
        .and_then(VertexAttributeValues::as_float3)
        .ok_or(HlodGeometryError::Unsupported(
            "missing aggregate positions",
        ))?;
    let mut channels = Vec::new();
    let mut names = Vec::new();
    let mut exact = Vec::new();
    for (attribute, values) in mesh.attributes() {
        if attribute.id == Mesh::ATTRIBUTE_POSITION.id {
            continue;
        }
        let (components, tolerance) = if attribute.id == Mesh::ATTRIBUTE_NORMAL.id {
            (3, policy.maximum_normal_delta)
        } else if attribute.id == Mesh::ATTRIBUTE_TANGENT.id {
            (4, policy.maximum_normal_delta)
        } else if [Mesh::ATTRIBUTE_UV_0.id, Mesh::ATTRIBUTE_UV_1.id].contains(&attribute.id) {
            (2, policy.maximum_uv_delta)
        } else {
            exact.push(values);
            continue;
        };
        let flattened = match values {
            VertexAttributeValues::Float32x2(values) if components == 2 => values.as_flattened(),
            VertexAttributeValues::Float32x3(values) if components == 3 => values.as_flattened(),
            VertexAttributeValues::Float32x4(values) if components == 4 => values.as_flattened(),
            _ => {
                return Err(HlodGeometryError::Unsupported(
                    "unexpected normal/tangent/UV format",
                ));
            }
        };
        names.push(attribute.name);
        channels.push(ErrorChannel {
            values: flattened,
            components,
            maximum_delta: tolerance,
        });
    }
    let mut signatures = BTreeMap::<Vec<u8>, u32>::new();
    let mut domains = Vec::with_capacity(n);
    for vertex in 0..n {
        let mut key = Vec::new();
        for attribute in &exact {
            let bytes = attribute.get_bytes();
            let stride = bytes.len() / n;
            key.extend_from_slice(&bytes[vertex * stride..(vertex + 1) * stride]);
        }
        let next = u32::try_from(signatures.len()).expect("bounded signatures");
        domains.push(*signatures.entry(key).or_insert(next));
    }
    let reduced = mesh_hlod::reduce(
        MeshSource {
            positions_m: positions,
            indices: &source_indices,
            domains: &domains,
            channels: &channels,
        },
        policy.maximum_position_error_m,
    )
    .map_err(HlodGeometryError::Reduction)?;
    let errors = names
        .into_iter()
        .zip(reduced.maximum_channel_deltas)
        .collect();
    let remapped = reduced
        .indices
        .into_iter()
        .map(|index| reduced.retained_vertices[index as usize])
        .collect();
    mesh.insert_indices(Indices::U32(remapped));
    // Public Bevy operations compact arbitrary supported attributes together,
    // without a private vertex-format clone or an unsafe byte reinterpretation.
    mesh.try_duplicate_vertices()
        .map_err(|error| HlodGeometryError::MeshOperation(error.to_string()))?;
    mesh.merge_duplicate_vertices()
        .map_err(|error| HlodGeometryError::MeshOperation(error.to_string()))?;
    let statistics = HlodGeometryStats {
        source_primitives: primitives,
        source_vertices,
        source_triangles: source_indices.len() / 3,
        output_vertices: mesh.count_vertices(),
        output_triangles: mesh.indices().map_or(0, Indices::len) / 3,
        maximum_position_error_m: reduced.maximum_position_error_m,
        maximum_attribute_deltas: errors,
        minimum_face_alignment: reduced.minimum_face_alignment,
        triangle_checks: reduced.triangle_checks,
        output_buffer_bytes: buffer_bytes(&mesh),
    };
    Ok(PreparedHlodGeometry { mesh, statistics })
}
