use forge_core::{CommandId, bytes_sha256};
use forge_project::{
    VegetationDocument,
    authoring::new_terrain,
    imported_vegetation::{
        ImportedInstanceEncoding, ImportedVegetationBlock, ImportedVegetationSource,
    },
    workspace::{
        objects::{ReviewedZone, ZoneObjectStore},
        source_blobs::SourceBlobStore,
    },
    zone_authoring::session::ZoneEditSession,
};

#[test]
fn workspace_requires_current_and_historical_blobs_and_never_overwrites_corruption() {
    let root = std::env::temp_dir().join(format!("forge-source-blobs-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let blobs = SourceBlobStore::open(&root).unwrap();
    let objects = ZoneObjectStore::open(&root).unwrap();
    let mut zone =
        ZoneEditSession::from_initial_terrain(new_terrain("blob-test", "zone", "Zone").unwrap())
            .unwrap();
    let mut document = VegetationDocument::from_json(include_str!(
        "../../../examples/visual_valley/vegetation.v1.json"
    ))
    .unwrap();
    document.schema_version = 2;
    document.project_id = zone.head().base.terrain.project_id;
    document.area_id = zone.head().base.terrain.area_id;
    document.layers.truncate(1);
    document.layers[0].material_filter = None;
    let mut first = vec![1_u8; 32];
    for v in [0.0_f64, 0.0, 0.0, 0.0, 0.0, 0.0, 1.0, 1.0, 1.0, 1.0] {
        first.extend(v.to_le_bytes());
    }
    let mut second = first.clone();
    second[0] = 2;
    for bytes in [&first, &second] {
        document.imported_source = Some(ImportedVegetationSource {
            schema: "forge.imported-vegetation".into(),
            version: 1,
            blocks: vec![ImportedVegetationBlock {
                layer_id: document.layers[0].id.to_string(),
                prototype_key: "test".into(),
                count: 1,
                encoding: ImportedInstanceEncoding::TransformF64LeV1,
                instances_sha256: bytes_sha256(bytes),
                parameter_names: vec![],
            }],
        });
        let mut base = zone.head().base.clone();
        base.vegetation = Some(document.clone());
        let request = zone
            .head()
            .prepare_regeneration(base, CommandId::deterministic(bytes))
            .unwrap();
        zone.execute(&request).unwrap();
    }
    let archive = zone.archive_bytes().unwrap();
    let reopened = ZoneEditSession::from_archive_bytes(&archive).unwrap();
    let required = reopened.required_source_blobs().unwrap();
    assert_eq!(required.len(), 2);
    assert_eq!(required[&bytes_sha256(&first)], first.len());
    let reviewed = ReviewedZone::from_bytes(&archive, bytes_sha256(&archive)).unwrap();
    assert!(objects.publish(&reviewed).is_err());
    blobs.publish(bytes_sha256(&second), &second).unwrap();
    assert!(
        objects.publish(&reviewed).is_err(),
        "undo source is missing"
    );
    blobs.publish(bytes_sha256(&first), &first).unwrap();
    blobs.publish(bytes_sha256(&first), &first).unwrap();
    objects.publish(&reviewed).unwrap();
    assert_eq!(
        objects
            .read(reviewed.project_id(), reviewed.reference())
            .unwrap(),
        archive
    );
    check_adoption(&root, &archive, &reviewed, &first, &second);
    assert!(blobs.read(bytes_sha256(&first), first.len() - 1).is_err());
    assert!(blobs.publish(bytes_sha256(&first), &second).is_err());
    let source_path = root
        .join("source-blobs")
        .join(format!("{}.bin", bytes_sha256(&first)));
    std::fs::write(&source_path, &second).unwrap();
    assert!(
        objects
            .read(reviewed.project_id(), reviewed.reference())
            .is_err()
    );
    assert!(blobs.publish(bytes_sha256(&first), &first).is_err());
    assert_eq!(
        std::fs::read(&source_path).unwrap(),
        second,
        "corruption must not be silently repaired"
    );
    std::fs::remove_dir_all(root).unwrap();
}

fn check_adoption(
    root: &std::path::Path,
    archive: &[u8],
    reviewed: &ReviewedZone,
    first: &[u8],
    second: &[u8],
) {
    use forge_core::{ModuleId, document_store::DocumentStore};
    use forge_project::workspace::{
        file_session::FileWorkspaceSession,
        session::{WorkspaceAction, WorkspaceRequest},
    };
    let root = root.join("adoption");
    std::fs::create_dir(&root).unwrap();
    let area = reviewed.reference().area_id;
    let source = root.join(area.to_string());
    std::fs::create_dir(&source).unwrap();
    let document_store = DocumentStore::open(&source).unwrap();
    document_store.save(None, archive).unwrap();
    drop(document_store);
    let source_blobs = SourceBlobStore::open(&source).unwrap();
    source_blobs.publish(bytes_sha256(first), first).unwrap();
    let mut workspace = FileWorkspaceSession::create(&root).unwrap();
    let module = ModuleId::deterministic("imported-adoption-module");
    for (key, action) in [
        (
            "project",
            WorkspaceAction::CreateProject {
                id: reviewed.project_id(),
                name: "Imported".into(),
            },
        ),
        (
            "module",
            WorkspaceAction::CreateModule {
                project_id: reviewed.project_id(),
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
    let before = workspace.session().head().content_hash().unwrap();
    assert!(
        workspace
            .review_adoption(
                area,
                reviewed.project_id(),
                module,
                reviewed.reference().archive_hash
            )
            .is_err()
    );
    assert_eq!(workspace.session().head().content_hash().unwrap(), before);
    source_blobs.publish(bytes_sha256(second), second).unwrap();
    let review = workspace
        .review_adoption(
            area,
            reviewed.project_id(),
            module,
            reviewed.reference().archive_hash,
        )
        .unwrap();
    assert!(
        !root.join("workspace-v1/source-blobs").exists(),
        "review must not copy blobs"
    );
    workspace.adopt(&review.request).unwrap();
    workspace.save().unwrap();
    drop(workspace);
    // Original-zone payloads are no longer dependencies of the managed workspace.
    std::fs::write(
        source
            .join("source-blobs")
            .join(format!("{}.bin", bytes_sha256(first))),
        b"damaged source after adoption",
    )
    .unwrap();
    let workspace = FileWorkspaceSession::open(&root).unwrap();
    assert_eq!(
        workspace.session().head().projects[0].modules[0].zones[0],
        *reviewed.reference()
    );
    let destination = SourceBlobStore::open_existing(&root.join("workspace-v1")).unwrap();
    assert_eq!(
        destination.read(bytes_sha256(first), first.len()).unwrap(),
        first
    );
    assert_eq!(
        destination
            .read(bytes_sha256(second), second.len())
            .unwrap(),
        second
    );
    assert_eq!(
        std::fs::read(source.join("authoring.json")).unwrap(),
        archive
    );
}
