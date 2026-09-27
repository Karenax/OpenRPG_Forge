//! CPU preparation for the experimental governed far-batch renderer.
//! The caller groups one chunk/material; authored attributes are never repurposed.
use bevy::{
    mesh::{Indices, MeshVertexAttribute, VertexAttributeValues},
    prelude::{Mesh, Transform},
    render::render_resource::{PrimitiveTopology, VertexFormat},
};

pub const ROOT_SCALE: MeshVertexAttribute =
    MeshVertexAttribute::new("ForgeFarRootScale", 0x464f_0001, VertexFormat::Float32x4);
pub const WIND_DATA: MeshVertexAttribute =
    MeshVertexAttribute::new("ForgeFarWindData", 0x464f_0002, VertexFormat::Float32x4);
const MAX_VERTICES: usize = 1_000_000;
const MAX_INDICES: usize = 6_000_000;

pub struct FarPrimitive<'a> {
    pub mesh: &'a Mesh,
    /// Positive uniform scale and yaw, with translation relative to the chunk origin.
    pub pose: Transform,
    pub height_m: f32,
    /// Original packed per-instance phase/response; never converted wholesale to f32.
    pub tag: u32,
    /// Derived renderer slot, zero keeps the original spatial path. Never an authored ID.
    pub temporal_slot: u16,
}

#[derive(Debug, PartialEq, Eq)]
pub enum FarGeometryError {
    Empty,
    InvalidPose,
    InvalidMesh,
    IncompatibleAttributes,
    BudgetExceeded,
    TransformOrMerge(String),
}

impl std::fmt::Display for FarGeometryError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "far vegetation geometry: {self:?}")
    }
}
impl std::error::Error for FarGeometryError {}

fn validate_parts(parts: &[FarPrimitive<'_>]) -> Result<(), FarGeometryError> {
    let first = parts.first().ok_or(FarGeometryError::Empty)?;
    let layout: Vec<_> = first.mesh.attributes().map(|(a, _)| *a).collect();
    let mut vertices = 0usize;
    let mut indices = 0usize;
    // Validate the entire input and budget before cloning any mesh.
    for part in parts {
        let pose = part.pose;
        if !pose.translation.is_finite()
            || !pose.scale.is_finite()
            || pose.scale.x <= 0.0
            || pose.scale.x.to_bits() != pose.scale.y.to_bits()
            || pose.scale.x.to_bits() != pose.scale.z.to_bits()
            || !pose.rotation.is_finite()
            || !pose.rotation.is_normalized()
            || pose.rotation.x.abs() > 0.000_001
            || pose.rotation.z.abs() > 0.000_001
            || !part.height_m.is_finite()
            || part.height_m <= 0.0
        {
            return Err(FarGeometryError::InvalidPose);
        }
        let mesh = part.mesh;
        let Some(VertexAttributeValues::Float32x3(positions)) =
            mesh.attribute(Mesh::ATTRIBUTE_POSITION)
        else {
            return Err(FarGeometryError::InvalidMesh);
        };
        let n = positions.len();
        if n == 0
            || mesh.primitive_topology() != PrimitiveTopology::TriangleList
            || positions.iter().flatten().any(|v| !v.is_finite())
            || positions
                .iter()
                .any(|p| !pose.transform_point((*p).into()).is_finite())
            || mesh.attributes().any(|(_, v)| v.len() != n)
            || mesh.contains_attribute(ROOT_SCALE)
            || mesh.contains_attribute(WIND_DATA)
        {
            return Err(FarGeometryError::InvalidMesh);
        }
        let count = mesh.indices().map_or(n, Indices::len);
        if !count.is_multiple_of(3)
            || mesh
                .indices()
                .is_some_and(|ids| ids.iter().any(|id| id >= n))
        {
            return Err(FarGeometryError::InvalidMesh);
        }
        if mesh.attributes().map(|(a, _)| *a).collect::<Vec<_>>() != layout {
            return Err(FarGeometryError::IncompatibleAttributes);
        }
        vertices = vertices
            .checked_add(n)
            .ok_or(FarGeometryError::BudgetExceeded)?;
        indices = indices
            .checked_add(count)
            .ok_or(FarGeometryError::BudgetExceeded)?;
        if vertices > MAX_VERTICES || indices > MAX_INDICES {
            return Err(FarGeometryError::BudgetExceeded);
        }
    }
    Ok(())
}

/// Builds an owned mesh without modifying sources or allocating renderer assets.
/// This preserves triangle count, not a simplifier or a claim of qualified HLOD.
pub fn build_far_geometry(parts: &[FarPrimitive<'_>]) -> Result<Mesh, FarGeometryError> {
    validate_parts(parts)?;
    let mut result: Option<Mesh> = None;
    for part in parts {
        let mut mesh = part.mesh.clone();
        let Some(VertexAttributeValues::Float32x3(positions)) =
            mesh.attribute(Mesh::ATTRIBUTE_POSITION)
        else {
            return Err(FarGeometryError::InvalidMesh);
        };
        let (phase, response) = decode_tag(part.tag);
        let wind: Vec<_> = positions
            .iter()
            .map(|p| {
                let weight = (p[1] / part.height_m).clamp(0.0, 1.0).powi(2);
                [weight, phase, response, f32::from(part.temporal_slot)]
            })
            .collect();
        let n = positions.len();
        let index_data = match mesh.indices() {
            Some(ids) => ids
                .iter()
                .map(u32::try_from)
                .collect::<Result<Vec<_>, _>>()
                .map_err(|_| FarGeometryError::BudgetExceeded)?,
            None => (0..u32::try_from(n).map_err(|_| FarGeometryError::BudgetExceeded)?).collect(),
        };
        mesh.insert_indices(Indices::U32(index_data));
        mesh.try_transform_by(part.pose)
            .map_err(|e| FarGeometryError::TransformOrMerge(e.to_string()))?;
        mesh.insert_attribute(
            ROOT_SCALE,
            vec![
                [
                    part.pose.translation.x,
                    part.pose.translation.y,
                    part.pose.translation.z,
                    part.pose.scale.x
                ];
                n
            ],
        );
        mesh.insert_attribute(WIND_DATA, wind);
        if let Some(result) = &mut result {
            result
                .merge(&mesh)
                .map_err(|e| FarGeometryError::TransformOrMerge(e.to_string()))?;
        } else {
            result = Some(mesh);
        }
    }
    result.ok_or(FarGeometryError::Empty)
}

#[allow(
    clippy::cast_precision_loss,
    reason = "24-bit phase and 8-bit response are exactly representable in f32, matching WGSL"
)]
fn decode_tag(tag: u32) -> (f32, f32) {
    (
        (tag & 0x00ff_ffff) as f32 / 16_777_215.0 * std::f32::consts::TAU,
        (tag >> 24) as f32 / 255.0,
    )
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::{asset::RenderAssetUsages, prelude::Vec3};

    fn triangle() -> Mesh {
        Mesh::new(
            PrimitiveTopology::TriangleList,
            RenderAssetUsages::default(),
        )
        .with_inserted_attribute(
            Mesh::ATTRIBUTE_POSITION,
            vec![[0., 0., 0.], [0., 2., 0.], [1., 0., 0.]],
        )
        .with_inserted_attribute(Mesh::ATTRIBUTE_UV_1, vec![[0.2, 0.7]; 3])
        .with_inserted_attribute(Mesh::ATTRIBUTE_COLOR, vec![[0.1, 0.2, 0.3, 0.4]; 3])
    }

    #[test]
    #[allow(
        clippy::float_cmp,
        reason = "Exact payload preservation and quantized tag decoding are the contract under test"
    )]
    fn merge_preserves_authored_attributes_and_individual_roots() {
        let source = triangle();
        let parts = [
            FarPrimitive {
                mesh: &source,
                pose: Transform::IDENTITY,
                height_m: 2.,
                tag: 0xff00_0000,
                temporal_slot: 0,
            },
            FarPrimitive {
                mesh: &source,
                pose: Transform::from_xyz(10., 4., -2.).with_scale(Vec3::splat(2.)),
                height_m: 2.,
                tag: 0x80ff_ffff,
                temporal_slot: 32_768,
            },
        ];
        let mesh = build_far_geometry(&parts).unwrap();
        assert_eq!(mesh.count_vertices(), 6);
        assert_eq!(
            mesh.indices().unwrap().iter().collect::<Vec<_>>(),
            vec![0, 1, 2, 3, 4, 5]
        );
        assert_eq!(source.count_vertices(), 3);
        assert!(source.indices().is_none());
        assert_eq!(
            mesh.attribute(Mesh::ATTRIBUTE_UV_1).unwrap(),
            &VertexAttributeValues::Float32x2(vec![[0.2, 0.7]; 6])
        );
        assert_eq!(
            mesh.attribute(Mesh::ATTRIBUTE_COLOR).unwrap(),
            &VertexAttributeValues::Float32x4(vec![[0.1, 0.2, 0.3, 0.4]; 6])
        );
        let VertexAttributeValues::Float32x4(roots) = mesh.attribute(ROOT_SCALE).unwrap() else {
            panic!()
        };
        assert_eq!(roots[3], [10., 4., -2., 2.]);
        let VertexAttributeValues::Float32x4(wind) = mesh.attribute(WIND_DATA).unwrap() else {
            panic!()
        };
        assert_eq!(wind[0], [0., 0., 1., 0.]);
        assert_eq!(wind[4], [1., std::f32::consts::TAU, 128. / 255., 32_768.]);
    }

    #[test]
    fn rejects_mixed_layout_bad_indices_and_nonuniform_pose() {
        let source = triangle();
        let mut other = triangle();
        other.remove_attribute(Mesh::ATTRIBUTE_UV_1);
        let part = |mesh| FarPrimitive {
            mesh,
            pose: Transform::IDENTITY,
            height_m: 2.,
            tag: 0,
            temporal_slot: 0,
        };
        assert!(matches!(
            build_far_geometry(&[part(&source), part(&other)]),
            Err(FarGeometryError::IncompatibleAttributes)
        ));
        let mut bad = triangle();
        bad.insert_indices(Indices::U32(vec![0, 1, 99]));
        assert!(matches!(
            build_far_geometry(&[part(&bad)]),
            Err(FarGeometryError::InvalidMesh)
        ));
        let mut bad_pose = part(&source);
        bad_pose.pose.scale = Vec3::new(1., 2., 1.);
        assert!(matches!(
            build_far_geometry(&[bad_pose]),
            Err(FarGeometryError::InvalidPose)
        ));
        assert!(matches!(
            build_far_geometry(&[]),
            Err(FarGeometryError::Empty)
        ));
    }
}
