//! Byte admission for autonomous source graphs. Rendering remains a separate gate.
#[path = "support/source_graph_fixture.rs"]
mod fixture;

use forge_assets::{
    media_validation::{
        MediaFormat, MediaInfo, MediaLimits, MediaRoute, validate_declared_media, validate_media,
        validate_source_graph_media,
    },
    source_graph_glb::{SourceGraphGlbReadBudget, read_source_graph_glb},
};
use forge_core::bytes_sha256;
use serde_json::{Value, json};

fn validate(bytes: &[u8], limits: MediaLimits) -> Result<MediaInfo, String> {
    validate_source_graph_media(bytes, limits, SourceGraphGlbReadBudget::Standard)
        .map_err(|error| error.to_string())
}

fn parts(bytes: &[u8]) -> (Value, Vec<u8>) {
    let length = u32::from_le_bytes(bytes[12..16].try_into().unwrap()) as usize;
    let doc = serde_json::from_slice(&bytes[20..20 + length]).unwrap();
    let bin_start = 20 + length + 8;
    (doc, bytes[bin_start..].to_vec())
}

fn encode(document: &Value, mut bin: Vec<u8>) -> Vec<u8> {
    let mut json = serde_json::to_vec(document).unwrap();
    json.resize(json.len().next_multiple_of(4), b' ');
    bin.resize(bin.len().next_multiple_of(4), 0);
    let mut bytes = b"glTF".to_vec();
    bytes.extend(2_u32.to_le_bytes());
    bytes.extend(
        u32::try_from(28 + json.len() + bin.len())
            .unwrap()
            .to_le_bytes(),
    );
    bytes.extend(u32::try_from(json.len()).unwrap().to_le_bytes());
    bytes.extend(b"JSON");
    bytes.extend(json);
    bytes.extend(u32::try_from(bin.len()).unwrap().to_le_bytes());
    bytes.extend(b"BIN\0");
    bytes.extend(bin);
    bytes
}

fn edited(edit: impl FnOnce(&mut Value)) -> Vec<u8> {
    let original = fixture::fixture().unwrap();
    let (mut document, bin) = parts(&original);
    edit(&mut document);
    encode(&document, bin)
}

#[test]
fn declared_graph_is_validated_explicitly_and_remains_refused_by_ordinary_loader() {
    let bytes = fixture::fixture().unwrap();
    assert_eq!(
        bytes_sha256(&bytes).to_string(),
        "e3e9da75bca09a475a1cf73185715591ddaa31ea4dc1434004ae9b59cf7cc751"
    );
    assert!(validate_media(&bytes, MediaFormat::Glb, MediaLimits::default()).is_err());
    assert!(matches!(
        validate(&bytes, MediaLimits::default()).unwrap(),
        MediaInfo::Glb {
            meshes: 1,
            primitives: 1,
            vertices: 3,
            indices: 3,
            has_geometry: true
        }
    ));
}

#[test]
fn declared_media_selects_graph_and_standard_paths_without_changing_either_validator() {
    let limits = MediaLimits::default();
    let graph = fixture::fixture().unwrap();
    let admitted = validate_declared_media(
        &graph,
        MediaFormat::Glb,
        limits,
        SourceGraphGlbReadBudget::Standard,
    )
    .unwrap();
    assert_eq!(admitted.route, MediaRoute::SourceGraph);
    assert_eq!(admitted.info, validate(&graph, limits).unwrap());
    assert!(validate_media(&graph, MediaFormat::Glb, limits).is_err());

    let ordinary = edited(|doc| {
        doc["extensionsUsed"] = json!([]);
        doc["extensionsRequired"] = json!([]);
        doc["materials"][0]["extensions"]
            .as_object_mut()
            .unwrap()
            .remove("OPENRPG_source_graph");
    });
    let standard = validate_media(&ordinary, MediaFormat::Glb, limits).unwrap();
    let admitted = validate_declared_media(
        &ordinary,
        MediaFormat::Glb,
        limits,
        SourceGraphGlbReadBudget::Standard,
    )
    .unwrap();
    assert_eq!(admitted.route, MediaRoute::Standard);
    assert_eq!(admitted.info, standard);
}

#[test]
fn declared_media_never_falls_back_after_a_broken_graph_or_exceeded_budget() {
    let mut changed = fixture::fixture().unwrap();
    let offset = changed
        .windows(fixture::FORWARD.len())
        .position(|window| window == fixture::FORWARD.as_bytes())
        .unwrap();
    changed[offset] ^= 1;
    let inconsistent = edited(|doc| doc["extensionsRequired"] = json!([]));
    for bytes in [&changed, &inconsistent] {
        assert!(
            validate_declared_media(
                bytes,
                MediaFormat::Glb,
                MediaLimits::default(),
                SourceGraphGlbReadBudget::Standard,
            )
            .is_err()
        );
    }
    let valid = fixture::fixture().unwrap();
    let limits = MediaLimits {
        max_input_bytes: valid.len() as u64 - 1,
        ..MediaLimits::default()
    };
    assert!(
        validate_declared_media(
            &valid,
            MediaFormat::Glb,
            limits,
            SourceGraphGlbReadBudget::Standard,
        )
        .is_err()
    );
}

#[test]
fn changed_program_missing_attributes_and_unrelated_required_extensions_are_refused() {
    let mut changed_program = fixture::fixture().unwrap();
    let position = changed_program
        .windows(fixture::FORWARD.len())
        .position(|window| window == fixture::FORWARD.as_bytes())
        .unwrap();
    changed_program[position] ^= 1;
    assert!(validate(&changed_program, MediaLimits::default()).is_err());
    let missing_source = edited(|doc| {
        doc["meshes"][0]["primitives"][0]["attributes"]["_OPENRPG_SOURCE_NORMAL"] = json!(999);
    });
    assert!(validate(&missing_source, MediaLimits::default()).is_err());
    let unknown_required = edited(|doc| {
        doc["extensionsUsed"]
            .as_array_mut()
            .unwrap()
            .push(json!("UNKNOWN_required"));
        doc["extensionsRequired"]
            .as_array_mut()
            .unwrap()
            .push(json!("UNKNOWN_required"));
    });
    assert!(validate(&unknown_required, MediaLimits::default()).is_err());
    let external = edited(|doc| doc["buffers"][0]["uri"] = json!("external.bin"));
    assert!(validate(&external, MediaLimits::default()).is_err());
    let mixed = edited(|doc| {
        doc["extensionsUsed"]
            .as_array_mut()
            .unwrap()
            .push(json!("OPENRPG_source_fragment"));
    });
    assert!(validate(&mixed, MediaLimits::default()).is_err());
}

#[test]
fn caller_budgets_bound_carrier_and_decoded_source_geometry() {
    let bytes = fixture::fixture().unwrap();
    let mut limits = MediaLimits {
        max_input_bytes: bytes.len() as u64 - 1,
        ..MediaLimits::default()
    };
    assert!(
        validate(&bytes, limits)
            .unwrap_err()
            .contains("input byte budget")
    );
    limits.max_input_bytes = bytes.len() as u64;
    // Ordinary accessor bytes fit, but the independent expanded source geometry does not.
    limits.max_decoded_bytes = 200;
    assert!(
        validate(&bytes, limits)
            .unwrap_err()
            .contains("decoded geometry allocation limit")
    );
    assert!(validate(&bytes, MediaLimits::default()).is_ok());
}

fn textured_graph(png: &[u8]) -> Vec<u8> {
    let (mut doc, mut bin) = parts(&fixture::fixture().unwrap());
    let mut add = |payload: &[u8]| {
        bin.resize(bin.len().next_multiple_of(4), 0);
        let index = doc["bufferViews"].as_array().unwrap().len();
        doc["bufferViews"]
            .as_array_mut()
            .unwrap()
            .push(json!({"buffer":0,"byteOffset":bin.len(),"byteLength":payload.len()}));
        bin.extend_from_slice(payload);
        index
    };
    let declarations = "@group(#{MATERIAL_BIND_GROUP}) @binding(101) var t0:texture_2d<f32>;\n@group(#{MATERIAL_BIND_GROUP}) @binding(102) var s0:sampler;\n";
    let forward = format!("{declarations}{}", fixture::FORWARD);
    let prepass = format!(
        "{declarations}@vertex fn vertex() -> @builtin(position) vec4<f32> {{ return vec4<f32>(0.); }}\n"
    );
    let f = add(forward.as_bytes());
    let p = add(prepass.as_bytes());
    let image = add(png);
    let metadata = b"TextureImporter:\n";
    let meta = add(metadata);
    bin.resize(bin.len().next_multiple_of(4), 0);
    doc["buffers"][0]["byteLength"] = json!(bin.len());
    let graph = &mut doc["materials"][0]["extensions"]["OPENRPG_source_graph"];
    graph["schema_version"] = json!(2);
    graph["state"]
        .as_object_mut()
        .unwrap()
        .remove("color_write_mask");
    graph["forward"] = json!({"buffer_view":f,"sha256":bytes_sha256(forward.as_bytes())});
    graph["prepass"] = json!({"buffer_view":p,"sha256":bytes_sha256(prepass.as_bytes())});
    graph["textures"] = json!([{"png":{"buffer_view":image,"sha256":bytes_sha256(png)},
        "source_meta":{"buffer_view":meta,"sha256":bytes_sha256(metadata)},
        "source_meta_sha256":bytes_sha256(metadata),"is_srgb":false,"source_mipmaps":false,
        "filter_mode":0,"wrap":[0,0,0],"anisotropy":1,"mip_bias":0.0}]);
    encode(&doc, bin)
}

#[test]
fn pinned_source_png_is_fully_decoded_and_obeys_image_budgets() {
    const PNG: &[u8] = &[
        137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 1, 0, 0, 0, 1, 8, 6,
        0, 0, 0, 31, 21, 196, 137, 0, 0, 0, 13, 73, 68, 65, 84, 120, 156, 99, 104, 104, 248, 255,
        31, 0, 6, 130, 2, 255, 179, 190, 81, 51, 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130,
    ];
    let valid = textured_graph(PNG);
    assert!(validate(&valid, MediaLimits::default()).is_ok());
    let limited = MediaLimits {
        max_image_pixels: 0,
        ..MediaLimits::default()
    };
    assert!(validate(&valid, limited).is_err());
    let truncated = textured_graph(&PNG[..33]);
    assert!(
        read_source_graph_glb(&truncated).unwrap().is_some(),
        "the pinned header alone passes integrity inspection"
    );
    assert!(validate(&truncated, MediaLimits::default()).is_err());
}
