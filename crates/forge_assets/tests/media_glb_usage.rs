#![cfg(not(target_arch = "wasm32"))]
use forge_assets::media_validation::{MediaFormat, MediaInfo, MediaLimits, validate_media};
use serde_json::{Value, json};

#[test]
fn morph_position_vec2_is_refused_before_the_typed_morph_reader() {
    let (mut document, mut binary) = triangle();
    let accessor = append_accessor(&mut document, &mut binary, 5126, "VEC2", 3, &[0; 24]);
    document["meshes"][0]["primitives"][0]["targets"] = json!([{"POSITION":accessor}]);
    document["meshes"][0]["weights"] = json!([0.0]);
    assert_refused(&document, &binary, "morph POSITION");
}

#[test]
fn animation_input_vec3_is_refused_before_the_timestamp_reader() {
    let (mut document, mut binary) = triangle();
    let accessor = append_accessor(&mut document, &mut binary, 5126, "VEC3", 1, &[0; 12]);
    document["animations"] = json!([{
        "samplers":[{"input":accessor,"output":accessor,"interpolation":"LINEAR"}],
        "channels":[{"sampler":0,"target":{"node":0,"path":"translation"}}]
    }]);
    assert_refused(&document, &binary, "animation input");
}

#[test]
fn rotation_u32_is_refused_before_the_unreachable_reader_branch() {
    let (mut document, mut binary) = triangle();
    let output_offset = binary.len();
    let mut rotation = vec![0; 12];
    rotation.extend(1_u32.to_le_bytes());
    let output = append_accessor(&mut document, &mut binary, 5125, "VEC4", 1, &rotation);
    let input = append_accessor(&mut document, &mut binary, 5126, "SCALAR", 1, &[0; 4]);
    document["accessors"][input]["min"] = json!([0.0]);
    document["accessors"][input]["max"] = json!([0.0]);
    document["animations"] = json!([{
        "samplers":[{"input":input,"output":output,"interpolation":"LINEAR"}],
        "channels":[{"sampler":0,"target":{"node":0,"path":"rotation"}}]
    }]);
    let mut valid_document = document.clone();
    valid_document["accessors"][output]["componentType"] = json!(5126);
    let mut valid_binary = binary.clone();
    valid_binary[output_offset + 12..output_offset + 16].copy_from_slice(&1_f32.to_le_bytes());
    validate_media(
        &glb(&valid_document, &valid_binary),
        MediaFormat::Glb,
        MediaLimits::default(),
    )
    .expect("the Float32 identity rotation counterpart must remain supported");
    assert_refused(&document, &binary, "animation rotation");
}

#[test]
fn inverse_bind_vec3_is_refused_before_the_matrix_reader() {
    let (mut document, mut binary) = triangle();
    let accessor = append_accessor(&mut document, &mut binary, 5126, "VEC3", 1, &[0; 12]);
    document["skins"] = json!([{"inverseBindMatrices":accessor,"joints":[1]}]);
    document["nodes"][0]["skin"] = json!(0);
    document["nodes"][0]["children"] = json!([1]);
    document["nodes"].as_array_mut().unwrap().push(json!({}));
    assert_refused(&document, &binary, "inverseBindMatrices");
}

#[test]
fn unknown_animation_target_is_refused_before_checked_property_unwrap() {
    let (mut document, mut binary) = triangle();
    let mut rotation = vec![0; 12];
    rotation.extend(1_f32.to_le_bytes());
    let output = append_accessor(&mut document, &mut binary, 5126, "VEC4", 1, &rotation);
    let input = append_accessor(&mut document, &mut binary, 5126, "SCALAR", 1, &[0; 4]);
    document["accessors"][input]["min"] = json!([0.0]);
    document["accessors"][input]["max"] = json!([0.0]);
    document["animations"] = json!([{
        "samplers":[{"input":input,"output":output,"interpolation":"LINEAR"}],
        "channels":[{"sampler":0,"target":{"node":0,"path":"rotation"}}]
    }]);
    validate_media(
        &glb(&document, &binary),
        MediaFormat::Glb,
        MediaLimits::default(),
    )
    .expect("the unchanged Float32 animation must remain admitted");
    document["animations"][0]["channels"][0]["target"]["path"] = json!("invalid-property");
    assert_refused(&document, &binary, "animation target");
}

fn assert_refused(document: &Value, binary: &[u8], usage: &str) {
    let bytes = glb(document, binary);
    let result = std::panic::catch_unwind(|| {
        validate_media(&bytes, MediaFormat::Glb, MediaLimits::default())
    });
    assert!(result.is_ok(), "{usage} reached a panicking reader");
    let Err(error) = result.unwrap() else {
        panic!("invalid {usage} type was admitted");
    };
    assert!(
        error.reason.contains(usage),
        "unrelated rejection for {usage}: {error}"
    );
}

fn triangle() -> (Value, Vec<u8>) {
    let mut binary = Vec::new();
    for value in [0.0_f32, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0, 0.0] {
        binary.extend(value.to_le_bytes());
    }
    let document = json!({
        "asset":{"version":"2.0"},"buffers":[{"byteLength":36}],
        "bufferViews":[{"buffer":0,"byteLength":36}],
        "accessors":[{"bufferView":0,"componentType":5126,"count":3,
                      "type":"VEC3","min":[0,0,0],"max":[1,1,0]}],
        "meshes":[{"primitives":[{"attributes":{"POSITION":0}}]}],
        "nodes":[{"mesh":0}],"scenes":[{"nodes":[0]}],"scene":0
    });
    assert!(matches!(
        validate_media(
            &glb(&document, &binary),
            MediaFormat::Glb,
            MediaLimits::default()
        )
        .unwrap(),
        MediaInfo::Glb {
            has_geometry: true,
            vertices: 3,
            ..
        }
    ));
    (document, binary)
}

fn append_accessor(
    document: &mut Value,
    binary: &mut Vec<u8>,
    component: u32,
    dimensions: &str,
    count: usize,
    bytes: &[u8],
) -> usize {
    let offset = binary.len();
    binary.extend(bytes);
    document["buffers"][0]["byteLength"] = json!(binary.len());
    let views = document["bufferViews"].as_array_mut().unwrap();
    let view = views.len();
    views.push(json!({"buffer":0,"byteOffset":offset,"byteLength":bytes.len()}));
    let accessors = document["accessors"].as_array_mut().unwrap();
    let accessor = accessors.len();
    accessors.push(json!({"bufferView":view,"componentType":component,
                          "count":count,"type":dimensions}));
    accessor
}

fn glb(document: &Value, binary: &[u8]) -> Vec<u8> {
    let mut json = serde_json::to_vec(document).unwrap();
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
