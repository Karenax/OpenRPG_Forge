use super::*;

fn fixture() -> (ImportedTextureStorage, Vec<Vec<u8>>) {
    let data = vec![17; 8];
    let mip = ImportedStorageMip {
        width: 4,
        height: 4,
        offset: 0,
        bytes: 8,
        sha256: bytes_sha256(&data),
    };
    let sampler = serde_json::json!({"reflection_index":2,"max_anisotropy":9});
    let capture = serde_json::to_vec(&serde_json::json!({
        "schema_version":1,"capture":"F:/reference/frame.rdc","source_shaders_modified":false,
        "textures":[{"resource_name":"original_texture","view_format":"BC1_UNORM",
            "view_srgb":false,"width":4,"height":4,"depth":1,"array_size":1,"samples":1,
            "mip_count":1,"reflection_index":2,"exported":true,
            "mips":[{"level":0,"width":4,"height":4,"bytes":8,"file":"mip0.bin"}]}],
        "samplers":[sampler.clone()]
    }))
    .unwrap();
    let binding = serde_json::to_vec(&serde_json::json!({
        "schema_version":1,"pins":{
            "F:/reference/frame.rdc":bytes_sha256(b"original frame"),
            "F:/reference/capture.json":bytes_sha256(&capture),
            "F:/reference/mip0.bin":bytes_sha256(&data)},
        "textures":[{"source_guid":"a".repeat(32),"native_resource_name":"original_texture",
            "view_format":"BC1_UNORM","width":4,"height":4,
            "storage":{"sha256":bytes_sha256(&data)},
            "native_fragment_texture_slot":2,"native_sampler":sampler,
            "source_sha256":bytes_sha256(b"PNG"),"source_meta_sha256":bytes_sha256(b"meta"),
            "mips":[serde_json::to_value(&mip).unwrap()]}]
    }))
    .unwrap();
    let blob = |index, bytes: &[u8]| SourceGraphBlob {
        buffer_view: index,
        sha256: bytes_sha256(bytes),
    };
    let transport = ImportedTextureStorage {
        native_capture: Some(NativeCaptureIdentity {
            protocol: NativeCaptureProtocol::RenderDocOriginalMips,
            source_guid: "a".repeat(32),
        }),
        source_binding: Some(blob(2, &binding)),
        schema_version: 3,
        storage: blob(0, &data),
        capture: blob(1, &capture),
        capture_case_index: 0,
        width: 4,
        height: 4,
        format: ImportedStorageFormat::Bc1RgbaUnorm,
        mips: vec![mip],
        sampler: ResolvedStorageSampler {
            filter_mode: 1,
            wrap: [0; 3],
            anisotropy: 1,
            mip_bias: 0.0,
            source_mipmaps_enabled: false,
        },
    };
    (transport, vec![data, capture, binding])
}

#[test]
fn original_frame_srgb_names_match_the_capture_wire() {
    let (mut transport, mut resources) = fixture();
    let mut capture: serde_json::Value = serde_json::from_slice(&resources[1]).unwrap();
    let mut binding: serde_json::Value = serde_json::from_slice(&resources[2]).unwrap();
    capture["textures"][0]["view_format"] = serde_json::json!("BC1_SRGB");
    capture["textures"][0]["view_srgb"] = serde_json::json!(true);
    binding["textures"][0]["view_format"] = serde_json::json!("BC1_SRGB");
    resources[1] = serde_json::to_vec(&capture).unwrap();
    transport.capture.sha256 = bytes_sha256(&resources[1]);
    binding["pins"]["F:/reference/capture.json"] = serde_json::json!(transport.capture.sha256);
    resources[2] = serde_json::to_vec(&binding).unwrap();
    transport.source_binding.as_mut().unwrap().sha256 = bytes_sha256(&resources[2]);
    transport.format = ImportedStorageFormat::Bc1RgbaUnormSrgb;
    let views: Vec<_> = resources.iter().map(Vec::as_slice).collect();
    assert!(transport.validate(&views).is_ok());
    transport.format = ImportedStorageFormat::Bc1RgbaUnorm;
    assert!(transport.validate(&views).is_err());
}

#[test]
fn original_frame_binding_and_source_pins_refuse_misassociation() {
    let (transport, resources) = fixture();
    let views: Vec<_> = resources.iter().map(Vec::as_slice).collect();
    assert_eq!(transport.validate(&views).unwrap(), resources[0]);
    transport
        .validate_bound_source(&views, bytes_sha256(b"PNG"), bytes_sha256(b"meta"))
        .unwrap();
    assert!(
        transport
            .validate_bound_source(&views, bytes_sha256(b"other PNG"), bytes_sha256(b"meta"))
            .is_err()
    );
    assert!(
        transport
            .validate_bound_source(&views, bytes_sha256(b"PNG"), bytes_sha256(b"other meta"))
            .is_err()
    );
    for pointer in [
        "/textures/0/source_guid",
        "/textures/0/native_resource_name",
        "/textures/0/mips/0/sha256",
        "/pins/F:~1reference~1capture.json",
    ] {
        let mut changed = resources.clone();
        let mut binding: serde_json::Value = serde_json::from_slice(&changed[2]).unwrap();
        *binding.pointer_mut(pointer).unwrap() = serde_json::json!("b".repeat(32));
        changed[2] = serde_json::to_vec(&binding).unwrap();
        let mut candidate = transport.clone();
        candidate.source_binding.as_mut().unwrap().sha256 = bytes_sha256(&changed[2]);
        let views: Vec<_> = changed.iter().map(Vec::as_slice).collect();
        assert!(candidate.validate(&views).is_err(), "{pointer}");
    }
    let mut candidate = transport.clone();
    candidate.schema_version = 2;
    assert!(candidate.validate(&views).is_err());
    candidate.schema_version = 3;
    candidate.source_binding = None;
    assert!(candidate.validate(&views).is_err());
    let mut changed = resources;
    changed[0][0] ^= 1;
    let views: Vec<_> = changed.iter().map(Vec::as_slice).collect();
    assert!(transport.validate(&views).is_err());
}
