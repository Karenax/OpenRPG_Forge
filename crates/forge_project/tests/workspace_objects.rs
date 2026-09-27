#![cfg(not(target_arch = "wasm32"))]
use forge_core::{ProjectId, bytes_sha256};
use forge_project::{
    authoring::new_terrain,
    workspace::objects::{ReviewedZone, ZoneObjectStore},
    zone_authoring::session::ZoneEditSession,
};

#[test]
fn immutable_object_publication_preserves_source_and_rejects_corruption() {
    let root = std::env::temp_dir().join(format!("forge-zone-objects-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let bytes =
        ZoneEditSession::from_initial_terrain(new_terrain("objects", "area", "Forêt").unwrap())
            .unwrap()
            .archive_bytes()
            .unwrap();
    let source = root.join("legacy.json");
    std::fs::write(&source, &bytes).unwrap();
    let reviewed = ReviewedZone::from_bytes(&bytes, bytes_sha256(&bytes)).unwrap();
    let store = ZoneObjectStore::open(&root).unwrap();
    assert!(
        store
            .verify(reviewed.project_id(), reviewed.reference())
            .is_err()
    );
    store.publish(&reviewed).unwrap();
    store.publish(&reviewed).unwrap();
    assert_eq!(
        bytes,
        store
            .read(reviewed.project_id(), reviewed.reference())
            .unwrap()
    );
    assert_eq!(bytes, std::fs::read(&source).unwrap());
    assert!(
        store
            .verify(ProjectId::deterministic("wrong"), reviewed.reference())
            .is_err()
    );
    let mut wrong_head = reviewed.reference().clone();
    wrong_head.head_hash = bytes_sha256(b"wrong");
    assert!(store.verify(reviewed.project_id(), &wrong_head).is_err());
    let object = root
        .join("objects")
        .join(format!("{}.json", reviewed.reference().archive_hash));
    std::fs::write(&object, b"corrupt").unwrap();
    assert!(store.publish(&reviewed).is_err());
    assert_eq!(std::fs::read(&object).unwrap(), b"corrupt");
    assert_eq!(bytes, std::fs::read(&source).unwrap());
    assert!(!root.join("authoring.json").exists());
    drop(store);
    std::fs::remove_dir_all(root).unwrap();
}

#[test]
fn review_requires_exact_bytes_and_valid_archive() {
    assert!(ReviewedZone::from_bytes(b"{}", bytes_sha256(b"other")).is_err());
    assert!(ReviewedZone::from_bytes(b"{}", bytes_sha256(b"{}")).is_err());
    let oversized = vec![0; 4 * 1024 * 1024 + 1];
    assert!(ReviewedZone::from_bytes(&oversized, bytes_sha256(&oversized)).is_err());
}
