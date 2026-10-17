#![cfg(not(target_arch = "wasm32"))]
use forge_assets::media_validation::{MediaFormat, MediaLimits, validate_media};

fn glb(json: &serde_json::Value, binary: &[u8]) -> Vec<u8> {
    let mut json = serde_json::to_vec(json).unwrap();
    while !json.len().is_multiple_of(4) {
        json.push(b' ');
    }
    let mut binary = binary.to_vec();
    while !binary.len().is_multiple_of(4) {
        binary.push(0);
    }
    let mut bytes = b"glTF".to_vec();
    bytes.extend(2_u32.to_le_bytes());
    bytes.extend(
        u32::try_from(28 + json.len() + binary.len())
            .unwrap()
            .to_le_bytes(),
    );
    bytes.extend(u32::try_from(json.len()).unwrap().to_le_bytes());
    bytes.extend(b"JSON");
    bytes.extend(json);
    bytes.extend(u32::try_from(binary.len()).unwrap().to_le_bytes());
    bytes.extend(b"BIN\0");
    bytes.extend(binary);
    bytes
}

#[test]
fn float_primitive_indices_are_refused_without_panicking_in_gltf_reader() {
    let bytes = glb(
        &serde_json::json!({
            "asset":{"version":"2.0"},"buffers":[{"byteLength":48}],
            "bufferViews":[{"buffer":0,"byteLength":36},{"buffer":0,"byteOffset":36,"byteLength":12}],
            "accessors":[{"bufferView":0,"componentType":5126,"count":3,"type":"VEC3","min":[0,0,0],"max":[1,1,0]},
                         {"bufferView":1,"componentType":5126,"count":3,"type":"SCALAR"}],
            "meshes":[{"primitives":[{"attributes":{"POSITION":0},"indices":1}]}]
        }),
        &[0; 48],
    );
    let result = std::panic::catch_unwind(|| {
        validate_media(&bytes, MediaFormat::Glb, MediaLimits::default())
    });
    assert!(
        result.is_ok(),
        "invalid GLB indices reached a panicking reader"
    );
    assert!(result.unwrap().is_err(), "Float32 indices were admitted");
}

#[test]
fn wrong_position_component_or_dimensions_are_refused_without_panicking() {
    for (component, dimensions, length) in [(5123, "VEC3", 18), (5126, "VEC2", 24)] {
        let bytes = glb(
            &serde_json::json!({
                "asset":{"version":"2.0"},"buffers":[{"byteLength":length}],
                "bufferViews":[{"buffer":0,"byteLength":length}],
                "accessors":[{"bufferView":0,"componentType":component,"count":3,"type":dimensions,"min":[0,0,0],"max":[1,1,0]}],
                "meshes":[{"primitives":[{"attributes":{"POSITION":0}}]}]
            }),
            &vec![0; length],
        );
        let result = std::panic::catch_unwind(|| {
            validate_media(&bytes, MediaFormat::Glb, MediaLimits::default())
        });
        assert!(
            result.is_ok(),
            "invalid POSITION reached a panicking reader"
        );
        assert!(
            result.unwrap().is_err(),
            "invalid POSITION accessor was admitted"
        );
    }
}

#[test]
fn missing_position_accessor_is_refused_before_gltf_json_validation_hook() {
    let bytes = glb(
        &serde_json::json!({
            "asset":{"version":"2.0"},"buffers":[{"byteLength":4}],
            "bufferViews":[{"buffer":0,"byteLength":4}],
            "meshes":[{"primitives":[{"attributes":{"POSITION":999}}]}]
        }),
        &[0; 4],
    );
    let result = std::panic::catch_unwind(|| {
        validate_media(&bytes, MediaFormat::Glb, MediaLimits::default())
    });
    assert!(
        result.is_ok(),
        "missing POSITION accessor reached the gltf-json indexing hook"
    );
    assert!(
        result.unwrap().is_err(),
        "missing POSITION accessor was admitted"
    );
}

#[test]
fn zero_accessor_count_is_refused_before_reader_subtracts_one() {
    let bytes = glb(
        &serde_json::json!({
            "asset":{"version":"2.0"},"buffers":[{"byteLength":12}],
            "bufferViews":[{"buffer":0,"byteLength":12}],
            "accessors":[{"bufferView":0,"componentType":5126,"count":0,"type":"VEC3","min":[0,0,0],"max":[0,0,0]}],
            "meshes":[{"primitives":[{"attributes":{"POSITION":0}}]}]
        }),
        &[0; 12],
    );
    let result = std::panic::catch_unwind(|| {
        validate_media(&bytes, MediaFormat::Glb, MediaLimits::default())
    });
    assert!(
        result.is_ok(),
        "zero accessor count reached unchecked reader arithmetic"
    );
    assert!(result.unwrap().is_err(), "zero accessor count was admitted");
}

#[test]
fn zero_sparse_count_is_refused_before_sparse_reader_subtracts_one() {
    let bytes = glb(
        &serde_json::json!({
            "asset":{"version":"2.0"},"buffers":[{"byteLength":52}],
            "bufferViews":[{"buffer":0,"byteLength":36},{"buffer":0,"byteOffset":36,"byteLength":1},{"buffer":0,"byteOffset":40,"byteLength":12}],
            "accessors":[{"bufferView":0,"componentType":5126,"count":3,"type":"VEC3","min":[0,0,0],"max":[0,0,0],
              "sparse":{"count":0,"indices":{"bufferView":1,"componentType":5121},"values":{"bufferView":2}}}],
            "meshes":[{"primitives":[{"attributes":{"POSITION":0}}]}]
        }),
        &[0; 52],
    );
    let result = std::panic::catch_unwind(|| {
        validate_media(&bytes, MediaFormat::Glb, MediaLimits::default())
    });
    assert!(
        result.is_ok(),
        "zero sparse count reached unchecked reader arithmetic"
    );
    assert!(result.unwrap().is_err(), "zero sparse count was admitted");
}
