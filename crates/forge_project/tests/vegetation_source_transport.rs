use forge_core::{CommandId, bytes_sha256, document_store::DocumentStore};
use forge_project::{
    VegetationDocument,
    authoring::{
        AuthoringRequest,
        host::AuthoringHost,
        new_terrain,
        vegetation_source::{CHUNK_BYTES, VegetationSourceAssembly},
    },
    imported_vegetation::{
        ImportedInstanceEncoding, ImportedVegetationBlock, ImportedVegetationSource,
    },
    workspace::source_blobs::SourceBlobStore,
    zone_authoring::session::{ZoneAction, ZoneEditSession, ZoneRequest},
};

#[test]
fn population_transport_is_zone_bound_and_rejects_invalid_chunks() {
    let root = std::env::temp_dir().join(format!("forge-plant-transport-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let (session, bytes) = fixture();
    let head = session.head();
    let area = head.base.terrain.area_id;
    let hash = head.content_hash().unwrap();
    let authority = root.join(area.to_string());
    std::fs::create_dir(&authority).unwrap();
    DocumentStore::open(&authority)
        .unwrap()
        .save(None, &session.archive_bytes().unwrap())
        .unwrap();
    let mut host = AuthoringHost::new(&root).unwrap();
    assert!(host.read_vegetation_source(hash, 0, 0).is_err());
    host.execute(AuthoringRequest::Open { area_id: area })
        .unwrap();
    assert!(host.read_vegetation_source(hash, 0, 0).is_err());
    SourceBlobStore::open(&authority)
        .unwrap()
        .publish(bytes_sha256(&bytes), &bytes)
        .unwrap();
    assert!(
        host.read_vegetation_source(bytes_sha256(b"stale"), 0, 0)
            .is_err()
    );
    assert!(host.read_vegetation_source(hash, 1, 0).is_err());
    assert!(host.read_vegetation_source(hash, 0, 1).is_err());
    assert!(host.read_vegetation_source(hash, 0, bytes.len()).is_err());
    let before = host.execute(AuthoringRequest::Inspect).unwrap();
    let first = host.read_vegetation_source(hash, 0, 0).unwrap();
    assert_eq!(first.bytes.len(), CHUNK_BYTES);
    let mut assembly = VegetationSourceAssembly::new(head, 0).unwrap();
    for field in 0..6 {
        let mut bad = first.clone();
        match field {
            0 => bad.head_hash = bytes_sha256(b"stale"),
            1 => bad.source_hash = bytes_sha256(b"wrong blob"),
            2 => bad.block_index = 1,
            3 => bad.offset = CHUNK_BYTES,
            4 => bad.total_bytes += 1,
            _ => {
                bad.bytes.pop();
            }
        }
        assert!(assembly.accept(bad).is_err());
        assert_eq!(assembly.next_offset(), Some(0));
    }
    assembly.accept(first.clone()).unwrap();
    assert!(assembly.accept(first.clone()).is_err());
    while let Some(offset) = assembly.next_offset() {
        assembly
            .accept(host.read_vegetation_source(hash, 0, offset).unwrap())
            .unwrap();
    }
    assert!(assembly.accept(first.clone()).is_err());
    assert_eq!(assembly.finish().unwrap(), bytes);
    assert_eq!(before, host.execute(AuthoringRequest::Inspect).unwrap());
    let mut corrupt = VegetationSourceAssembly::new(head, 0).unwrap();
    let mut altered = first;
    altered.bytes[0] ^= 1;
    corrupt.accept(altered).unwrap();
    while let Some(offset) = corrupt.next_offset() {
        corrupt
            .accept(host.read_vegetation_source(hash, 0, offset).unwrap())
            .unwrap();
    }
    assert!(corrupt.finish().is_err());
    assert!(
        VegetationSourceAssembly::new(head, 0)
            .unwrap()
            .finish()
            .is_err()
    );
    host.execute(AuthoringRequest::Close).unwrap();
    assert!(host.read_vegetation_source(hash, 0, 0).is_err());
    std::fs::write(
        authority
            .join("source-blobs")
            .join(format!("{}.bin", bytes_sha256(&bytes))),
        b"corrupt",
    )
    .unwrap();
    host.execute(AuthoringRequest::Open { area_id: area })
        .unwrap();
    assert!(host.read_vegetation_source(hash, 0, 0).is_err());
    drop(host);
    // The test exclusively created this exact temporary directory.
    assert_eq!(root.parent(), Some(std::env::temp_dir().as_path()));
    std::fs::remove_dir_all(root).unwrap();
}

fn fixture() -> (ZoneEditSession, Vec<u8>) {
    let terrain = new_terrain("plant-transport", "island", "Island").unwrap();
    let mut vegetation = VegetationDocument::from_json(include_str!(
        "../../../examples/visual_valley/vegetation.v1.json"
    ))
    .unwrap();
    vegetation.project_id = terrain.project_id;
    vegetation.area_id = terrain.area_id;
    vegetation.schema_version = 2;
    vegetation.layers.truncate(1);
    vegetation.layers[0].material_filter = None;
    let mut bytes = Vec::new();
    for index in 0..3000_u32 {
        bytes.extend([0; 28]);
        bytes.extend(index.to_be_bytes());
        for value in [0_f64, 0., 0., 0., 0., 0., 1., 1., 1., 1.] {
            bytes.extend(value.to_le_bytes());
        }
    }
    vegetation.imported_source = Some(ImportedVegetationSource {
        schema: "forge.imported-vegetation".into(),
        version: 1,
        blocks: vec![ImportedVegetationBlock {
            layer_id: vegetation.layers[0].id.to_string(),
            prototype_key: "trees".into(),
            count: 3000,
            encoding: ImportedInstanceEncoding::TransformF64LeV1,
            instances_sha256: bytes_sha256(&bytes),
            parameter_names: vec![],
        }],
    });
    let mut session = ZoneEditSession::from_initial_terrain(terrain).unwrap();
    let mut base = session.head().base.clone();
    base.vegetation = Some(vegetation);
    session
        .execute(&ZoneRequest {
            command_id: CommandId::deterministic("import-transport"),
            expected_revision: session.head().revision,
            expected_hash: session.head().content_hash().unwrap(),
            action: ZoneAction::Regenerate(Box::new(base)),
        })
        .unwrap();
    (session, bytes)
}
