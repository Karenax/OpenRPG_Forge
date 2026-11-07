//! Shared CPU mesh adapter for admitted source-profile geometry.
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
/// Original source-frame positions, preserved for source-space deformation.
pub const SOURCE_REST_POSITION: MeshVertexAttribute = MeshVertexAttribute::new(
    "DebrisSourceRestPosition",
    0x7457_0020,
    VertexFormat::Float32x3,
);

/// A source mesh and its independently indexed shadow-caster subset.
pub struct MeshWithSourceShadow {
    pub mesh: Mesh,
    pub shadow_mesh: Option<Mesh>,
}

/// Little-endian source attributes, including the original shader parameters.
pub struct SourceMeshBytes<'a> {
    pub positions: &'a [u8],
    pub normals: &'a [u8],
    pub uv: &'a [u8],
    pub tint: &'a [u8],
    pub data: &'a [u8],
    pub indices: &'a [u8],
}

impl SourceMeshBytes<'_> {
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
            return Err("Invalid or oversized source mesh attributes");
        }
        let indices = triangle_indices(self.indices, count)?;
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

    /// Preserve source-frame rest positions and a separately indexed caster mesh.
    /// The caller remains responsible for identity, hashes and source-frame policy.
    pub fn into_mesh_with_source_shadow(
        self,
        source_rest: &[u8],
        shadow_indices: &[u8],
    ) -> Result<MeshWithSourceShadow, &'static str> {
        let position_bytes = self.positions.len();
        let vertex_count = position_bytes / 12;
        let mut mesh = self.into_mesh()?;
        if source_rest.len() != position_bytes {
            return Err("Source rest coordinates differ from mesh positions");
        }
        mesh.insert_attribute(SOURCE_REST_POSITION, vectors::<3>(source_rest)?);
        let shadow_mesh = if shadow_indices.is_empty() {
            None
        } else {
            let indices = triangle_indices(shadow_indices, vertex_count)?;
            Some(mesh.clone().with_inserted_indices(Indices::U32(indices)))
        };
        Ok(MeshWithSourceShadow { mesh, shadow_mesh })
    }
}

fn triangle_indices(bytes: &[u8], vertices: usize) -> Result<Vec<u32>, &'static str> {
    if bytes.is_empty() || !bytes.len().is_multiple_of(12) || bytes.len() > 24_000_000 {
        return Err("Invalid or oversized source triangle indices");
    }
    let indices: Vec<_> = bytes
        .as_chunks::<4>()
        .0
        .iter()
        .map(|b| u32::from_le_bytes(*b))
        .collect();
    if indices.iter().any(|i| *i as usize >= vertices) {
        return Err("source triangle index out of bounds");
    }
    Ok(indices)
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
                Err("Non-finite source mesh attribute")
            } else {
                Ok(values)
            }
        })
        .collect()
}

#[cfg(test)]
mod tests {
    use super::*;
    const POSITIONS: [u8; 36] = [0; 36];
    const UVS: [u8; 24] = [0; 24];
    const DATA_VALUES: [u8; 48] = [0; 48];
    const MAIN_INDICES: [u8; 12] = [0, 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0];
    const SHADOW_INDICES: [u8; 12] = [2, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0];

    fn fixture() -> SourceMeshBytes<'static> {
        SourceMeshBytes {
            positions: &POSITIONS,
            normals: &POSITIONS,
            uv: &UVS,
            tint: &POSITIONS,
            data: &DATA_VALUES,
            indices: &MAIN_INDICES,
        }
    }

    fn source_rest() -> Vec<u8> {
        (0..9_u16)
            .flat_map(|value| f32::from(value).to_le_bytes())
            .collect()
    }

    #[test]
    fn source_rest_and_caster_preserve_visual_mesh_and_independent_index_order() {
        use bevy::mesh::VertexAttributeValues;
        let base = fixture().into_mesh().unwrap();
        let prepared = fixture()
            .into_mesh_with_source_shadow(&source_rest(), &SHADOW_INDICES)
            .unwrap();
        for attribute in [
            Mesh::ATTRIBUTE_POSITION,
            Mesh::ATTRIBUTE_NORMAL,
            Mesh::ATTRIBUTE_UV_0,
            TINT,
            DATA,
        ] {
            assert_eq!(
                prepared.mesh.attribute(attribute),
                base.attribute(attribute)
            );
        }
        assert_eq!(prepared.mesh.indices(), base.indices());
        let shadow = prepared.shadow_mesh.unwrap();
        assert_eq!(shadow.indices(), Some(&Indices::U32(vec![2, 1, 0])));
        for attribute in [
            Mesh::ATTRIBUTE_POSITION,
            Mesh::ATTRIBUTE_NORMAL,
            Mesh::ATTRIBUTE_UV_0,
            TINT,
            DATA,
            SOURCE_REST_POSITION,
        ] {
            assert_eq!(
                shadow.attribute(attribute),
                prepared.mesh.attribute(attribute)
            );
        }
        let Some(VertexAttributeValues::Float32x3(rest)) =
            prepared.mesh.attribute(SOURCE_REST_POSITION)
        else {
            panic!("source-frame rest positions must remain Float32x3");
        };
        assert_eq!(
            rest[2].map(f32::to_bits),
            [6.0_f32, 7.0, 8.0].map(f32::to_bits)
        );
        assert!(
            fixture()
                .into_mesh_with_source_shadow(&source_rest(), &[])
                .unwrap()
                .shadow_mesh
                .is_none()
        );
    }

    #[test]
    fn source_rest_and_caster_reject_bad_lengths_non_finite_values_and_indices() {
        let rest = source_rest();
        assert!(
            fixture()
                .into_mesh_with_source_shadow(&rest[..rest.len() - 1], &SHADOW_INDICES)
                .is_err()
        );
        let mut nonfinite = rest.clone();
        nonfinite[..4].copy_from_slice(&f32::NAN.to_le_bytes());
        assert!(
            fixture()
                .into_mesh_with_source_shadow(&nonfinite, &SHADOW_INDICES)
                .is_err()
        );
        assert!(
            fixture()
                .into_mesh_with_source_shadow(&rest, &SHADOW_INDICES[..8])
                .is_err()
        );
        let mut out_of_bounds = SHADOW_INDICES;
        out_of_bounds[..4].copy_from_slice(&3_u32.to_le_bytes());
        assert!(
            fixture()
                .into_mesh_with_source_shadow(&rest, &out_of_bounds)
                .is_err()
        );
    }

    #[test]
    #[allow(
        clippy::float_cmp,
        reason = "Verifies exact float32 values decoded from a known binary attribute payload."
    )]
    fn preserves_custom_shader_data_and_rejects_bad_payloads() {
        let p: Vec<u8> = [0.0_f32, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0, 0.0]
            .into_iter()
            .flat_map(f32::to_le_bytes)
            .collect();
        let uv = [0_u8; 24];
        let data: Vec<u8> = (0..12_u16)
            .flat_map(|i| f32::from(i).to_le_bytes())
            .collect();
        let indices: Vec<u8> = [0_u32, 1, 2]
            .into_iter()
            .flat_map(u32::to_le_bytes)
            .collect();
        let input = |position, idx| SourceMeshBytes {
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
