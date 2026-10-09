//! Additional UV streams are retained without asserting shader consumption.
use bevy::{
    mesh::{MeshVertexAttribute, VertexFormat},
    prelude::Mesh,
};

/// Slots correspond to channels one through seven. Channel one uses Bevy's
/// standard lightmap/secondary UV attribute; higher channels retain distinct IDs.
pub const SOURCE_GRAPH_STANDARD_ADDITIONAL_UVS: [MeshVertexAttribute; 7] = [
    Mesh::ATTRIBUTE_UV_1,
    MeshVertexAttribute::new("SourceGraphTexcoord2", 0x5347_0102, VertexFormat::Float32x2),
    MeshVertexAttribute::new("SourceGraphTexcoord3", 0x5347_0103, VertexFormat::Float32x2),
    MeshVertexAttribute::new("SourceGraphTexcoord4", 0x5347_0104, VertexFormat::Float32x2),
    MeshVertexAttribute::new("SourceGraphTexcoord5", 0x5347_0105, VertexFormat::Float32x2),
    MeshVertexAttribute::new("SourceGraphTexcoord6", 0x5347_0106, VertexFormat::Float32x2),
    MeshVertexAttribute::new("SourceGraphTexcoord7", 0x5347_0107, VertexFormat::Float32x2),
];

/// Full Vec4 source values are distinct from conventional glTF Vec2 UVs.
pub const SOURCE_GRAPH_ORIGINAL_ADDITIONAL_UVS: [MeshVertexAttribute; 7] = [
    MeshVertexAttribute::new(
        "SourceGraphOriginalUv1",
        0x5347_0201,
        VertexFormat::Float32x4,
    ),
    MeshVertexAttribute::new(
        "SourceGraphOriginalUv2",
        0x5347_0202,
        VertexFormat::Float32x4,
    ),
    MeshVertexAttribute::new(
        "SourceGraphOriginalUv3",
        0x5347_0203,
        VertexFormat::Float32x4,
    ),
    MeshVertexAttribute::new(
        "SourceGraphOriginalUv4",
        0x5347_0204,
        VertexFormat::Float32x4,
    ),
    MeshVertexAttribute::new(
        "SourceGraphOriginalUv5",
        0x5347_0205,
        VertexFormat::Float32x4,
    ),
    MeshVertexAttribute::new(
        "SourceGraphOriginalUv6",
        0x5347_0206,
        VertexFormat::Float32x4,
    ),
    MeshVertexAttribute::new(
        "SourceGraphOriginalUv7",
        0x5347_0207,
        VertexFormat::Float32x4,
    ),
];

/// Shader locations in the source-stage ABI. These avoid the existing position,
/// UV0, original normal/tangent/UV0/color locations and remain below WebGPU's
/// minimum sixteen vertex attributes. Channels are selected by source DAG reads.
pub const SOURCE_GRAPH_ADDITIONAL_UV_LOCATIONS: [u32; 7] = [12, 13, 14, 15, 3, 4, 5];

/// Bits one through seven require their exact original Vec4 source attributes.
/// UV0 has a separate mandatory ABI; bit zero is deliberately invalid here.
/// A conventional Vec2 glTF attribute cannot satisfy a source attribute request.
pub fn source_graph_required_uv_bindings(
    required_channels: u8,
) -> Result<Vec<(MeshVertexAttribute, u32)>, &'static str> {
    if required_channels & 1 != 0 {
        return Err("UV0 is handled by the mandatory source-stage ABI");
    }
    Ok((1..=7)
        .filter(|channel| required_channels & (1 << channel) != 0)
        .map(|channel| {
            (
                SOURCE_GRAPH_ORIGINAL_ADDITIONAL_UVS[channel - 1],
                SOURCE_GRAPH_ADDITIONAL_UV_LOCATIONS[channel - 1],
            )
        })
        .collect())
}

#[cfg(test)]
mod binding_tests {
    use super::*;
    use bevy::{
        asset::RenderAssetUsages, mesh::MeshVertexBufferLayouts,
        render::render_resource::PrimitiveTopology,
    };

    fn accepts_uv1(mesh: &Mesh) -> bool {
        let layout = mesh.get_mesh_vertex_buffer_layout(&mut MeshVertexBufferLayouts::default());
        let bindings = source_graph_required_uv_bindings(1 << 1).unwrap();
        let attributes: Vec<_> = bindings
            .into_iter()
            .map(|(attribute, location)| attribute.at_shader_location(location))
            .collect();
        layout.0.get_layout(&attributes).is_ok()
    }

    #[test]
    fn all_source_channels_have_unique_noncolliding_locations() {
        let bindings = source_graph_required_uv_bindings(0xfe).unwrap();
        assert_eq!(bindings.len(), 7);
        let reserved = [0, 2, 8, 9, 10, 11];
        let mut locations = std::collections::BTreeSet::new();
        for (attribute, location) in bindings {
            assert!(location < 16 && !reserved.contains(&location));
            assert!(locations.insert(location));
            assert_eq!(attribute.format, VertexFormat::Float32x4);
        }
    }

    #[test]
    fn uv0_request_is_refused_and_empty_request_preserves_existing_abi() {
        assert!(source_graph_required_uv_bindings(1).is_err());
        assert!(source_graph_required_uv_bindings(0xff).is_err());
        assert!(source_graph_required_uv_bindings(0).unwrap().is_empty());
    }

    #[test]
    fn exact_uv1_binds_but_standard_uv1_and_other_channels_cannot_substitute() {
        let mut mesh = Mesh::new(
            PrimitiveTopology::TriangleList,
            RenderAssetUsages::default(),
        );
        mesh.insert_attribute(Mesh::ATTRIBUTE_UV_1, vec![[0.0, 0.7]; 3]);
        mesh.insert_attribute(
            SOURCE_GRAPH_ORIGINAL_ADDITIONAL_UVS[1],
            vec![[0.0, 0.7, 0.0, 0.0]; 3],
        );
        assert!(!accepts_uv1(&mesh));
        mesh.insert_attribute(
            SOURCE_GRAPH_ORIGINAL_ADDITIONAL_UVS[0],
            vec![[0.0, 0.7, 0.0, 0.0]; 3],
        );
        assert!(accepts_uv1(&mesh));
    }
}
