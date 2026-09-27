//! Shared CPU mesh adapter for acquired Tidewater village geometry.
//! Payload provenance and dependency hashes are checked by the calling loader.
use bevy::{
    asset::RenderAssetUsages,
    mesh::{Indices, MeshVertexAttribute},
    prelude::Mesh,
    render::render_resource::{PrimitiveTopology, VertexFormat},
};

pub const TINT: MeshVertexAttribute =
    MeshVertexAttribute::new("VillageTint", 0x7457_0005, VertexFormat::Float32x3);
pub const DATA: MeshVertexAttribute =
    MeshVertexAttribute::new("VillageData", 0x7457_0006, VertexFormat::Float32x4);

/// Little-endian source attributes, including the original shader parameters.
pub struct VillageMeshBytes<'a> {
    pub positions: &'a [u8],
    pub normals: &'a [u8],
    pub uv: &'a [u8],
    pub tint: &'a [u8],
    pub data: &'a [u8],
    pub indices: &'a [u8],
}

impl VillageMeshBytes<'_> {
    /// Validate before allocation/upload; preserve ordered attributes and triangles.
    pub fn into_mesh(self) -> Result<Mesh, &'static str> {
        let count = self.positions.len() / 12;
        if count == 0
            || count > 2_000_000
            || self.positions.len() != count * 12
            || self.normals.len() != count * 12
            || self.uv.len() != count * 8
            || self.tint.len() != count * 12
            || self.data.len() != count * 16
            || self.indices.is_empty()
            || !self.indices.len().is_multiple_of(12)
            || self.indices.len() > 24_000_000
        {
            return Err("Invalid or oversized Tidewater mesh attributes");
        }
        let indices: Vec<_> = self
            .indices
            .as_chunks::<4>()
            .0
            .iter()
            .map(|b| u32::from_le_bytes(*b))
            .collect();
        if indices.iter().any(|i| *i as usize >= count) {
            return Err("Tidewater triangle index out of bounds");
        }
        Ok(Mesh::new(
            PrimitiveTopology::TriangleList,
            RenderAssetUsages::default(),
        )
        .with_inserted_attribute(Mesh::ATTRIBUTE_POSITION, vectors::<3>(self.positions)?)
        .with_inserted_attribute(Mesh::ATTRIBUTE_NORMAL, vectors::<3>(self.normals)?)
        .with_inserted_attribute(Mesh::ATTRIBUTE_UV_0, vectors::<2>(self.uv)?)
        .with_inserted_attribute(TINT, vectors::<3>(self.tint)?)
        .with_inserted_attribute(DATA, vectors::<4>(self.data)?)
        .with_inserted_indices(Indices::U32(indices)))
    }
}

fn vectors<const N: usize>(bytes: &[u8]) -> Result<Vec<[f32; N]>, &'static str> {
    bytes
        .as_chunks::<4>()
        .0
        .as_chunks::<N>()
        .0
        .iter()
        .map(|chunk| {
            let values = std::array::from_fn(|i| f32::from_le_bytes(chunk[i]));
            if values.iter().any(|v| !v.is_finite()) {
                Err("Non-finite Tidewater mesh attribute")
            } else {
                Ok(values)
            }
        })
        .collect()
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn preserves_custom_shader_data_and_rejects_bad_payloads() {
        let p: Vec<u8> = [0.0_f32, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0, 0.0]
            .into_iter()
            .flat_map(f32::to_le_bytes)
            .collect();
        let uv = [0_u8; 24];
        let data: Vec<u8> = (0..12).flat_map(|i| (i as f32).to_le_bytes()).collect();
        let indices: Vec<u8> = [0_u32, 1, 2]
            .into_iter()
            .flat_map(u32::to_le_bytes)
            .collect();
        let input = |position, idx| VillageMeshBytes {
            positions: position,
            normals: &p,
            uv: &uv,
            tint: &p,
            data: &data,
            indices: idx,
        };
        let mesh = input(&p, &indices).into_mesh().unwrap();
        let Some(bevy::mesh::VertexAttributeValues::Float32x4(values)) = mesh.attribute(DATA)
        else {
            panic!("shader data must remain Float32x4");
        };
        assert_eq!(values[2], [8.0, 9.0, 10.0, 11.0]);
        assert!(input(&p[..p.len() - 1], &indices).into_mesh().is_err());
        let mut bad = indices.clone();
        bad[0..4].copy_from_slice(&3_u32.to_le_bytes());
        assert!(input(&p, &bad).into_mesh().is_err());
        let mut nonfinite = p.clone();
        nonfinite[0..4].copy_from_slice(&f32::NAN.to_le_bytes());
        assert!(input(&nonfinite, &indices).into_mesh().is_err());
    }
}
