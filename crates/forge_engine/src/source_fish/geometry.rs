// SPDX-License-Identifier: MIT
// Adapted from dgreenheck/tidewater, copyright 2026 DRG Software Solutions LLC.
// Revision 1438b1ab; see third_party/tidewater/LICENSE.
//! Prepare posed fish meshes while retaining their source shading attributes.
use super::pose;
use bevy::{
    asset::RenderAssetUsages,
    mesh::{Indices, MeshVertexAttribute},
    prelude::Mesh,
    render::render_resource::{PrimitiveTopology, VertexFormat},
};

pub const REST_POSITION: MeshVertexAttribute =
    MeshVertexAttribute::new("FishRestPosition", 0x7457_0010, VertexFormat::Float32x3);
pub const FISH_DATA: MeshVertexAttribute =
    MeshVertexAttribute::new("FishData", 0x7457_0011, VertexFormat::Float32x4);

/// Validated geometry can be shared by any number of posed instances.
pub struct Geometry {
    positions: Vec<[f32; 3]>,
    normals: Vec<[f32; 3]>,
    data: Vec<[f32; 4]>,
    indices: Vec<u16>,
}

impl Geometry {
    /// Validate caller-supplied geometry once, independently of its module or storage.
    ///
    /// # Errors
    /// Refuses incomplete, nonfinite or out-of-range vertex and index streams.
    pub fn new(
        positions: Vec<[f32; 3]>,
        normals: Vec<[f32; 3]>,
        data: Vec<[f32; 4]>,
        indices: Vec<u16>,
    ) -> Result<Self, String> {
        if positions.is_empty()
            || normals.len() != positions.len()
            || data.len() != positions.len()
            || indices.is_empty()
            || !indices.len().is_multiple_of(3)
            || indices
                .iter()
                .any(|&index| usize::from(index) >= positions.len())
            || positions
                .iter()
                .flatten()
                .chain(normals.iter().flatten())
                .chain(data.iter().flatten())
                .any(|value| !value.is_finite())
        {
            return Err("Fish geometry shape or indices differ".into());
        }
        Ok(Self {
            positions,
            normals,
            data,
            indices,
        })
    }

    /// Prepare a mesh without changing the validated source arrays.
    ///
    /// # Errors
    /// Refuses a nonfinite pose or result.
    pub fn mesh(&self, bending: pose::Bend) -> Result<Mesh, String> {
        let posed = self
            .positions
            .iter()
            .zip(&self.normals)
            .zip(&self.data)
            .map(|((&position, &normal), &data)| pose::deform(position, normal, data, bending))
            .collect::<Result<Vec<_>, _>>()?;
        Ok(Mesh::new(
            PrimitiveTopology::TriangleList,
            RenderAssetUsages::default(),
        )
        .with_inserted_attribute(
            Mesh::ATTRIBUTE_POSITION,
            posed
                .iter()
                .map(|vertex| vertex.position)
                .collect::<Vec<_>>(),
        )
        .with_inserted_attribute(
            Mesh::ATTRIBUTE_NORMAL,
            posed.iter().map(|vertex| vertex.normal).collect::<Vec<_>>(),
        )
        .with_inserted_attribute(REST_POSITION, self.positions.clone())
        .with_inserted_attribute(FISH_DATA, self.data.clone())
        .with_inserted_indices(Indices::U16(self.indices.clone())))
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::mesh::VertexAttributeValues;

    #[test]
    fn mesh_keeps_rest_attributes_and_indices_when_pose_changes() {
        let positions = [[0.0, 0.0, 0.0], [0.2, 0.0, 0.5], [0.0, 0.2, 1.0]];
        let normals = [[0.0, 1.0, 0.0]; 3];
        let data = [[0.0, 0.4, 0.0, 0.0]; 3];
        let indices = [0, 1, 2];
        let geometry = Geometry::new(
            positions.to_vec(),
            normals.to_vec(),
            data.to_vec(),
            indices.to_vec(),
        )
        .unwrap();
        let bend = pose::Bend {
            curl: 0.5,
            sag: 0.25,
            jaw: 0.3,
            hinge: [0.0; 2],
        };
        let mesh = geometry.mesh(bend).unwrap();
        let Some(VertexAttributeValues::Float32x3(actual)) =
            mesh.attribute(Mesh::ATTRIBUTE_POSITION)
        else {
            panic!("posed positions absent")
        };
        assert_ne!(actual[2].map(f32::to_bits), positions[2].map(f32::to_bits));
        assert_eq!(
            actual[2].map(f32::to_bits),
            pose::deform(positions[2], normals[2], data[2], bend)
                .unwrap()
                .position
                .map(f32::to_bits)
        );
        assert_eq!(
            mesh.attribute(REST_POSITION),
            Some(&VertexAttributeValues::Float32x3(positions.to_vec()))
        );
        assert_eq!(
            mesh.attribute(FISH_DATA),
            Some(&VertexAttributeValues::Float32x4(data.to_vec()))
        );
        assert_eq!(mesh.indices(), Some(&Indices::U16(indices.to_vec())));
    }

    #[test]
    fn invalid_geometry_is_refused() {
        let positions = [[0.0; 3]; 3];
        let normals = [[0.0, 1.0, 0.0]; 3];
        let data = [[0.0; 4]; 3];
        for (normals, indices) in [
            (&normals[..2], &[0, 1, 2][..]),
            (&normals[..], &[0, 1, 3][..]),
        ] {
            assert!(
                Geometry::new(
                    positions.to_vec(),
                    normals.to_vec(),
                    data.to_vec(),
                    indices.to_vec()
                )
                .is_err(),
            );
        }
        let mut nonfinite = data;
        nonfinite[0][0] = f32::NAN;
        assert!(
            Geometry::new(
                positions.to_vec(),
                normals.to_vec(),
                nonfinite.to_vec(),
                vec![0, 1, 2],
            )
            .is_err()
        );
    }
}
