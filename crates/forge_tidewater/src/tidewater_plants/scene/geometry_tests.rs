use super::*;
use bevy::mesh::Indices;
use bevy::mesh::VertexAttributeValues;
use forge_engine::source_vegetation::{LOBE, MAT, VEG};

fn fixture() -> (LevelDescription, BTreeMap<String, Vec<u8>>) {
    let data = serde_json::from_value(serde_json::json!({
        "near": true, "capacity": 3, "range": [0, 20, 30], "kind": 0,
        "attributes": {"position": "p", "normal": "n"}, "indices": "i",
    }))
    .unwrap();
    let files = BTreeMap::from([
        (
            "p".into(),
            [-0.0_f32, 2.0, 3.0, 1.0, 0.0, 2.0, 4.0, 5.0, 6.0]
                .into_iter()
                .flat_map(f32::to_le_bytes)
                .collect(),
        ),
        (
            "n".into(),
            [0.0_f32, 2.0, 0.0]
                .repeat(3)
                .into_iter()
                .flat_map(f32::to_le_bytes)
                .collect(),
        ),
        (
            "i".into(),
            [2_u32, 0, 1]
                .into_iter()
                .flat_map(u32::to_le_bytes)
                .collect(),
        ),
    ]);
    (data, files)
}

#[test]
fn neutral_geometry_reaches_bevy_without_changing_source_values_or_defaults() {
    let (description, files) = fixture();
    let result = mesh(&description, &files).unwrap();
    let Some(VertexAttributeValues::Float32x3(positions)) =
        result.attribute(Mesh::ATTRIBUTE_POSITION)
    else {
        panic!("positions layout changed")
    };
    assert_eq!(
        positions
            .iter()
            .flatten()
            .flat_map(|value| value.to_le_bytes())
            .collect::<Vec<_>>(),
        files["p"]
    );
    let Some(VertexAttributeValues::Float32x3(normals)) = result.attribute(Mesh::ATTRIBUTE_NORMAL)
    else {
        panic!("normals layout changed")
    };
    assert_eq!(
        normals
            .iter()
            .flatten()
            .flat_map(|value| value.to_le_bytes())
            .collect::<Vec<_>>(),
        files["n"]
    );
    let Some(VertexAttributeValues::Float32x2(uv)) = result.attribute(Mesh::ATTRIBUTE_UV_0) else {
        panic!("UV layout changed")
    };
    assert!(uv.iter().flatten().all(|value| value.to_bits() == 0));
    for attribute in [VEG, MAT, LOBE] {
        let Some(VertexAttributeValues::Float32x4(values)) = result.attribute(attribute) else {
            panic!("shader layout changed")
        };
        assert_eq!(values.len(), 3);
        assert!(values.iter().flatten().all(|value| value.to_bits() == 0));
    }
    assert!(matches!(result.indices(), Some(Indices::U32(values)) if values == &[2, 0, 1]));
}

#[test]
fn authored_shader_attributes_are_preserved_and_missing_declared_data_is_refused() {
    let (mut description, mut files) = fixture();
    for (name, width) in [("uv", 2), ("aVeg", 4), ("aMat", 4), ("aLobe", 4)] {
        description.attributes.insert(name.into(), name.into());
        files.insert(
            name.into(),
            (0..3 * width)
                .flat_map(|_| 1.25_f32.to_le_bytes())
                .collect(),
        );
    }
    let result = mesh(&description, &files).unwrap();
    for (name, attribute) in [("aVeg", VEG), ("aMat", MAT), ("aLobe", LOBE)] {
        let Some(VertexAttributeValues::Float32x4(values)) = result.attribute(attribute) else {
            panic!("shader layout changed")
        };
        assert_eq!(
            values
                .iter()
                .flatten()
                .flat_map(|v| v.to_le_bytes())
                .collect::<Vec<_>>(),
            files[name]
        );
    }
    let Some(VertexAttributeValues::Float32x2(uv)) = result.attribute(Mesh::ATTRIBUTE_UV_0) else {
        panic!("UV layout changed")
    };
    assert_eq!(
        uv.iter()
            .flatten()
            .flat_map(|v| v.to_le_bytes())
            .collect::<Vec<_>>(),
        files["uv"]
    );
    files.remove("aLobe");
    assert!(mesh(&description, &files).is_err());
}
