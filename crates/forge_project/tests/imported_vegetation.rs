use forge_core::{AreaId, ProjectId, bytes_sha256, canonical_json_sha256};
use forge_project::imported_vegetation::{
    ImportedInstanceEncoding, ImportedVegetationBlock, ImportedVegetationSource,
};
use forge_project::{VegetationDocument, VegetationExtent};

const LEGACY: &str = include_str!("../../../examples/visual_valley/vegetation.v1.json");

fn row(key: u8) -> Vec<u8> {
    let mut bytes = vec![key; 32];
    for value in [
        12.5_f64, 7.0, -23.0, 0.0, 0.0, 0.0, 1.0, 1.2, 1.3, 1.4, 0.25,
    ] {
        bytes.extend(value.to_le_bytes());
    }
    bytes
}

fn block(bytes: &[u8], count: u32) -> ImportedVegetationBlock {
    ImportedVegetationBlock {
        layer_id: "trees".into(),
        prototype_key: "fixture_trees".into(),
        count,
        encoding: ImportedInstanceEncoding::TransformF64LeV1,
        instances_sha256: bytes_sha256(bytes),
        parameter_names: vec!["lean".into()],
    }
}

fn extent() -> VegetationExtent {
    VegetationExtent {
        size_x_m: 2048.0,
        size_z_m: 2048.0,
    }
}

#[test]
fn legacy_hash_and_serialization_are_preserved() {
    let document = VegetationDocument::from_json(LEGACY).unwrap();
    // The old document stores f32 values, so raw JSON numbers are not its canonical hash.
    // Construct the old serialized field set after the same typed normalization.
    let mut value = serde_json::to_value(&document).unwrap();
    assert!(
        value
            .as_object_mut()
            .unwrap()
            .remove("imported_source")
            .is_none()
    );
    assert_eq!(
        document.content_hash().unwrap(),
        canonical_json_sha256(&value).unwrap()
    );
    assert!(
        !document
            .to_pretty_json()
            .unwrap()
            .contains("imported_source")
    );
}

#[test]
fn version_and_layer_coverage_are_explicit() {
    let mut document = VegetationDocument::from_json(LEGACY).unwrap();
    document.layers.truncate(1);
    let mut imported = block(&row(1), 1);
    imported.layer_id = document.layers[0].id.to_string();
    document.imported_source = Some(ImportedVegetationSource {
        schema: "forge.imported-vegetation".into(),
        version: 1,
        blocks: vec![imported],
    });
    assert!(document.validate().is_err());
    document.schema_version = 2;
    document.extent_m = extent();
    document.validate().unwrap();
    let restored = VegetationDocument::from_json(&document.to_pretty_json().unwrap()).unwrap();
    assert_eq!(restored, document);
    document.extent_m.size_x_m = 2049.0;
    assert!(document.validate().is_err());
    document.extent_m = extent();
    document.imported_source.as_mut().unwrap().blocks[0].layer_id = "unknown".into();
    assert!(document.validate().is_err());
    document.imported_source = None;
    assert!(document.validate().is_err());
}

#[test]
fn exact_decode_and_stable_identity() {
    let bytes = [row(1), row(2)].concat();
    let original = block(&bytes, 2);
    let instances = original.decode(&bytes, extent()).unwrap();
    assert_eq!(instances.len(), 2);
    assert_eq!(
        instances[0].position_m.map(f64::to_bits),
        [12.5_f64, 7.0, -23.0].map(f64::to_bits)
    );
    assert_eq!(
        instances[0].scale.map(f64::to_bits),
        [1.2_f64, 1.3, 1.4].map(f64::to_bits)
    );
    assert_eq!(instances[0].parameters, [0.25]);
    let project = ProjectId::deterministic("project");
    let area = AreaId::deterministic("area");
    let id = original.instance_id(project, area, instances[0].source_key);
    let mut revised = original.clone();
    revised.instances_sha256 = bytes_sha256(b"reimport");
    revised.count = 1;
    revised.prototype_key = "another_prototype".into();
    assert_eq!(
        id,
        revised.instance_id(project, area, instances[0].source_key)
    );
    assert_ne!(
        id,
        revised.instance_id(project, area, instances[1].source_key)
    );
    assert_ne!(
        id,
        revised.instance_id(
            project,
            AreaId::deterministic("other"),
            instances[0].source_key
        )
    );
    revised.layer_id = "other_layer".into();
    assert_ne!(
        id,
        revised.instance_id(project, area, instances[0].source_key)
    );
}

#[test]
fn damaged_bytes_and_invalid_transforms_are_rejected() {
    let bytes = row(1);
    let original = block(&bytes, 1);
    assert!(
        original
            .decode(&bytes[..bytes.len() - 1], extent())
            .is_err()
    );
    assert!(
        original
            .decode(&[bytes.clone(), vec![0]].concat(), extent())
            .is_err()
    );
    let mut damaged = bytes.clone();
    damaged[40] ^= 1;
    assert!(original.decode(&damaged, extent()).is_err());
    for (index, value) in [
        (0, 1025.0),
        (1, f64::NAN),
        (6, 0.5),
        (7, 0.0),
        (8, -1.0),
        (10, f64::INFINITY),
    ] {
        let mut invalid = bytes.clone();
        invalid[32 + 8 * index..40 + 8 * index].copy_from_slice(&value.to_le_bytes());
        assert!(
            block(&invalid, 1).decode(&invalid, extent()).is_err(),
            "index {index}"
        );
    }
    for invalid in [[row(1), row(1)].concat(), [row(2), row(1)].concat()] {
        assert!(block(&invalid, 2).decode(&invalid, extent()).is_err());
    }
    let mut invalid = original;
    invalid.parameter_names.push("lean".into());
    assert!(invalid.validate().is_err());
    invalid.parameter_names.clear();
    invalid.count = u32::MAX;
    assert!(invalid.byte_len().is_err());
}
