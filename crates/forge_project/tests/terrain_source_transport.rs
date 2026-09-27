use forge_core::bytes_sha256;
use forge_project::{
    HeightSamplingAlgorithm, TERRAIN_DOCUMENT_SCHEMA_VERSION_V4,
    authoring::{
        AuthoringRequest,
        host::AuthoringHost,
        new_terrain,
        terrain_source::{CHUNK_BYTES, TerrainSourceAssembly},
    },
    sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
    workspace::source_blobs::SourceBlobStore,
};

#[test]
fn active_raster_transport_is_bounded_verified_and_document_bound() {
    let root = std::env::temp_dir().join(format!("forge-terrain-transport-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let (document, bytes) = fixture();
    let mut host = AuthoringHost::new(&root).unwrap();
    let hash = document.content_hash().unwrap();
    assert!(host.read_terrain_source(hash, 0).is_err());
    let state = host
        .execute(AuthoringRequest::CreateZone {
            document: Box::new(document.clone()),
        })
        .unwrap();
    let document = state.document.unwrap();
    let hash = document.content_hash().unwrap();
    assert!(host.read_terrain_source(hash, 0).is_err());
    let authority = root.join(document.area_id.to_string());
    SourceBlobStore::open(&authority)
        .unwrap()
        .publish(bytes_sha256(&bytes), &bytes)
        .unwrap();
    host.execute(AuthoringRequest::Save).unwrap();
    let before = host.execute(AuthoringRequest::Inspect).unwrap();
    assert!(host.read_terrain_source(bytes_sha256(b"stale"), 0).is_err());
    assert!(host.read_terrain_source(hash, 1).is_err());
    assert!(host.read_terrain_source(hash, bytes.len()).is_err());
    let first = host.read_terrain_source(hash, 0).unwrap();
    assert_eq!(first.bytes.len(), CHUNK_BYTES);
    let mut assembly = TerrainSourceAssembly::new(&document).unwrap();
    let mut bad = first.clone();
    bad.document_hash = bytes_sha256(b"stale");
    assert!(assembly.accept(bad).is_err());
    assert_eq!(assembly.next_offset(), Some(0));
    let mut bad = first.clone();
    bad.offset = CHUNK_BYTES;
    assert!(assembly.accept(bad).is_err());
    let mut bad = first.clone();
    bad.total_bytes += 1;
    assert!(assembly.accept(bad).is_err());
    let mut bad = first.clone();
    bad.bytes.pop();
    assert!(assembly.accept(bad).is_err());
    assert_eq!(assembly.next_offset(), Some(0));
    assembly.accept(first.clone()).unwrap();
    assert!(assembly.accept(first.clone()).is_err());
    while let Some(offset) = assembly.next_offset() {
        assembly
            .accept(host.read_terrain_source(hash, offset).unwrap())
            .unwrap();
    }
    assert_eq!(assembly.finish().unwrap(), bytes);
    assert_eq!(before, host.execute(AuthoringRequest::Inspect).unwrap());
    let mut corrupt = TerrainSourceAssembly::new(&document).unwrap();
    let mut first = first;
    first.bytes[0] = 1;
    corrupt.accept(first).unwrap();
    while let Some(offset) = corrupt.next_offset() {
        corrupt
            .accept(host.read_terrain_source(hash, offset).unwrap())
            .unwrap();
    }
    assert!(corrupt.finish().is_err());
    assert!(
        TerrainSourceAssembly::new(&document)
            .unwrap()
            .finish()
            .is_err()
    );
    host.execute(AuthoringRequest::Close).unwrap();
    assert!(host.read_terrain_source(hash, 0).is_err());
    let file = authority
        .join("source-blobs")
        .join(format!("{}.bin", bytes_sha256(&bytes)));
    let mut damaged = bytes.clone();
    damaged[0] = 1;
    std::fs::write(&file, damaged).unwrap();
    host.execute(AuthoringRequest::Open {
        area_id: document.area_id,
    })
    .unwrap();
    assert!(host.read_terrain_source(hash, 0).is_err());
    drop(host);
    std::fs::write(&file, &bytes).unwrap();
    check_managed(&root, &document, &bytes);
    std::fs::remove_dir_all(root).unwrap();
}

fn check_managed(root: &std::path::Path, document: &forge_project::TerrainDocument, bytes: &[u8]) {
    use forge_core::{CommandId, ModuleId};
    use forge_project::{
        authoring::workspace::WorkspaceHostRequest,
        workspace::{
            file_session::FileWorkspaceSession,
            session::{WorkspaceAction, WorkspaceRequest},
        },
    };
    let area = document.area_id;
    let module = ModuleId::deterministic("transport-module");
    let archive = std::fs::read(root.join(area.to_string()).join("authoring.json")).unwrap();
    let mut workspace = FileWorkspaceSession::create(root).unwrap();
    for (key, action) in [
        (
            "project",
            WorkspaceAction::CreateProject {
                id: document.project_id,
                name: "Raster".into(),
            },
        ),
        (
            "module",
            WorkspaceAction::CreateModule {
                project_id: document.project_id,
                id: module,
                name: "Island".into(),
            },
        ),
    ] {
        let head = workspace.session().head();
        let request = WorkspaceRequest {
            command_id: CommandId::deterministic(key),
            base_revision: head.revision,
            base_hash: head.content_hash().unwrap(),
            action,
        };
        workspace.execute(&request).unwrap();
    }
    workspace.save().unwrap();
    let review = workspace
        .review_adoption(area, document.project_id, module, bytes_sha256(&archive))
        .unwrap();
    workspace.adopt(&review.request).unwrap();
    workspace.save().unwrap();
    drop(workspace);
    // A managed zone must use its workspace closure, never the old loose zone.
    std::fs::write(
        root.join(area.to_string())
            .join("source-blobs")
            .join(format!("{}.bin", bytes_sha256(bytes))),
        b"broken loose source",
    )
    .unwrap();
    let mut host = AuthoringHost::new(root).unwrap();
    host.execute_workspace(WorkspaceHostRequest::Open).unwrap();
    let state = host
        .execute(AuthoringRequest::OpenManaged { area_id: area })
        .unwrap();
    let doc = state.document.unwrap();
    let hash = doc.content_hash().unwrap();
    let mut assembly = TerrainSourceAssembly::new(&doc).unwrap();
    while let Some(offset) = assembly.next_offset() {
        assembly
            .accept(host.read_terrain_source(hash, offset).unwrap())
            .unwrap();
    }
    assert_eq!(assembly.finish().unwrap(), bytes);
}

fn fixture() -> (forge_project::TerrainDocument, Vec<u8>) {
    let mut document = new_terrain("source-transport", "raster", "Raster").unwrap();
    document.schema_version = TERRAIN_DOCUMENT_SCHEMA_VERSION_V4;
    document.height_sampling.algorithm = HeightSamplingAlgorithm::SampledRasterV1;
    document.height_sampling.sample_spacing_m = 1.0;
    document.height_sampling.amplitude_m = 0.0;
    document.height_sampling.base_frequency_cycles_per_m = 1.0;
    document.height_sampling.octaves = 1;
    document.height_sampling.lacunarity = 2.0;
    document.height_sampling.persistence = 0.5;
    let bytes = vec![0; 512 * 512 * 4];
    document.sampled_source = Some(SampledTerrainSource {
        schema: SAMPLED_TERRAIN_SCHEMA.into(),
        schema_version: 1,
        width: 512,
        height: 512,
        origin_xz_m: [-256.0; 2],
        sample_spacing_m: 1.0,
        outside_height_m: -90.0,
        encoding: HeightEncoding::Float32Le,
        samples_sha256: bytes_sha256(&bytes),
    });
    (document, bytes)
}
