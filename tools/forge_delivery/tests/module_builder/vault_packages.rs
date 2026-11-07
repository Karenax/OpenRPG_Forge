//! A compacted Vault model must become a self-contained logical module payload.
use super::*;
use flate2::{Compression, write::ZlibEncoder};
use forge_delivery::vault_storage::{self, Descriptor, Segment};
use std::io::Write;

#[test]
fn packed_vault_model_exports_an_autonomous_module_with_its_logical_hash() {
    let temp = Temp::new();
    let root = temp.0.join("vault");
    let model = root.join("models/triangle.glb");
    let pack = root.join(".forge-storage/packs/22222222222222222222222222222222.pack");
    fs::create_dir_all(model.parent().unwrap()).unwrap();
    fs::create_dir_all(pack.parent().unwrap()).unwrap();
    fs::write(
        root.join(".forge-storage/root.v1.json"),
        br#"{"schema":1,"vault_id":"11111111111111111111111111111111"}"#,
    )
    .unwrap();
    let mut fixture = Fixture::new(false);
    let expected = fixture.payloads.remove("assets/triangle.glb").unwrap();
    let mut encoder = ZlibEncoder::new(Vec::new(), Compression::default());
    encoder.write_all(&expected).unwrap();
    let encoded = encoder.finish().unwrap();
    fs::write(&pack, &encoded).unwrap();
    let descriptor = Descriptor {
        schema: 1,
        vault_id: "1".repeat(32),
        bytes: expected.len() as u64,
        sha256: forge_delivery::hash(&expected),
        segments: vec![Segment {
            pack: "2".repeat(32),
            offset: 0,
            stored_bytes: encoded.len() as u64,
            bytes: expected.len() as u64,
            sha256: forge_delivery::hash(&expected),
            codec: "zlib".into(),
        }],
    };
    let mut envelope = vault_storage::MAGIC.to_vec();
    envelope.extend(serde_json::to_vec(&descriptor).unwrap());
    fs::write(&model, &envelope).unwrap();

    let reconstructed = vault_storage::read_file(&root, &model, 1024 * 1024).unwrap();
    assert_eq!(reconstructed, expected);
    fixture
        .payloads
        .insert("assets/triangle.glb".into(), reconstructed);
    let exported = fixture.build(&temp.0.join("exports")).unwrap();

    // Neither the Vault nor the authoring staging directory may be needed by a player.
    fs::rename(&root, temp.0.join("unavailable-vault")).unwrap();
    fs::rename(&exported.source_root, temp.0.join("unavailable-source")).unwrap();
    let module = orpmod::inspect(&exported.package_path, false).unwrap();
    let entry = module
        .files
        .iter()
        .find(|entry| entry.path == "assets/triangle.glb")
        .unwrap();
    assert_eq!(entry.sha256, bytes_sha256(&expected));
    assert_eq!(entry.bytes, expected.len() as u64);
    let loaded = module_runtime::load(
        &exported.package_path,
        exported.area_id,
        Some(&temp.0.join("independent-cache")),
    )
    .unwrap();
    assert_eq!(
        loaded
            .read_payload("assets/triangle.glb", 1024 * 1024)
            .unwrap(),
        expected
    );
}
