#![cfg(not(target_arch = "wasm32"))]
use forge_core::{ModuleId, bytes_sha256, document_store::DocumentStore};
use forge_project::{
    authoring::{host::AuthoringHost, new_terrain, workspace::WorkspaceHostRequest as Request},
    workspace::{
        ModuleEntry, ProjectEntry, WorkspaceHead,
        file_session::FileWorkspaceSession,
        objects::{ReviewedZone, ZoneObjectStore},
        session::WorkspaceSession,
    },
    zone_authoring::session::ZoneEditSession,
};

#[test]
fn interrupted_workspace_recovery_is_explicit_hash_bound_and_preserves_versions() {
    let root =
        std::env::temp_dir().join(format!("forge-workspace-recovery-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let mut file = FileWorkspaceSession::create(&root).unwrap();
    file.save().unwrap();
    drop(file);
    let authority = root.join("workspace-v1");
    let saved_path = authority.join("authoring.json");
    let pending_path = authority.join("authoring.pending");
    let saved = std::fs::read(&saved_path).unwrap();
    std::fs::write(&pending_path, b"corrupt").unwrap();
    let mut host = AuthoringHost::new(&root).unwrap();
    assert!(host.execute_workspace(Request::Open).is_err());
    let review = host
        .execute_workspace(Request::ReviewRecovery)
        .unwrap()
        .recovery
        .unwrap();
    assert!(review.pending.head.is_none());
    let choice = |promote| Request::Recover {
        expected_saved: Some(bytes_sha256(&saved)),
        expected_pending: bytes_sha256(b"corrupt"),
        promote,
    };
    assert!(host.execute_workspace(choice(true)).is_err());
    assert_eq!(std::fs::read(&pending_path).unwrap(), b"corrupt");
    host.execute_workspace(choice(false)).unwrap();
    assert_eq!(std::fs::read(&saved_path).unwrap(), saved);
    assert_eq!(
        std::fs::read(authority.join(format!("recovery-{}.json", bytes_sha256(b"corrupt"))))
            .unwrap(),
        b"corrupt"
    );
    assert!(!pending_path.exists());

    let (area, zone, object, head) = candidate_zone();
    let candidate = WorkspaceSession::new(head.clone())
        .unwrap()
        .archive_bytes()
        .unwrap();
    std::fs::write(&pending_path, &candidate).unwrap();
    // The prior decision is stale, even when choosing to keep the saved version.
    assert!(host.execute_workspace(choice(false)).is_err());
    let request = Request::Recover {
        expected_saved: Some(bytes_sha256(&saved)),
        expected_pending: bytes_sha256(&candidate),
        promote: true,
    };
    assert!(
        host.execute_workspace(Request::ReviewRecovery)
            .unwrap()
            .recovery
            .unwrap()
            .pending
            .head
            .is_none()
    );
    assert!(host.execute_workspace(request.clone()).is_err());
    ZoneObjectStore::open(&authority)
        .unwrap()
        .publish(&object)
        .unwrap();
    assert_eq!(
        host.execute_workspace(Request::ReviewRecovery)
            .unwrap()
            .recovery
            .unwrap()
            .pending
            .head,
        Some(head.clone())
    );
    let source = root.join(area.to_string());
    std::fs::create_dir(&source).unwrap();
    std::fs::write(source.join("authoring.json"), &zone).unwrap();
    let source_lock = DocumentStore::open(&source).unwrap();
    assert!(host.execute_workspace(request.clone()).is_err());
    assert_eq!(std::fs::read(&saved_path).unwrap(), saved);
    drop(source_lock);
    let wire = serde_json::to_vec(&request).unwrap();
    host.execute_workspace(serde_json::from_slice(&wire).unwrap())
        .unwrap();
    assert!(!pending_path.exists());
    assert_eq!(std::fs::read(&saved_path).unwrap(), candidate);
    assert_eq!(std::fs::read(source.join("authoring.json")).unwrap(), zone);
    assert_eq!(
        std::fs::read(authority.join(format!("recovery-{}.json", bytes_sha256(&saved)))).unwrap(),
        saved
    );
    assert_eq!(
        std::fs::read(authority.join(format!("recovery-{}.json", bytes_sha256(&candidate))))
            .unwrap(),
        candidate
    );
    let opened = host.execute_workspace(Request::Open).unwrap();
    assert_eq!(opened.head, Some(head));
    assert!(!opened.dirty);
    assert!(host.execute_workspace(Request::ReviewRecovery).is_err());
    drop(host);
    std::fs::remove_dir_all(root).unwrap();
}

fn candidate_zone() -> (forge_core::AreaId, Vec<u8>, ReviewedZone, WorkspaceHead) {
    let terrain = new_terrain("recovery", "zone", "Recovered zone").unwrap();
    let area = terrain.area_id;
    let zone = ZoneEditSession::from_initial_terrain(terrain)
        .unwrap()
        .archive_bytes()
        .unwrap();
    let object = ReviewedZone::from_bytes(&zone, bytes_sha256(&zone)).unwrap();
    let head = WorkspaceHead {
        projects: vec![ProjectEntry {
            id: object.project_id(),
            name: "Recovered project".into(),
            modules: vec![ModuleEntry {
                id: ModuleId::deterministic("recovery-module"),
                name: "Recovered module".into(),
                zones: vec![object.reference().clone()],
            }],
        }],
        ..WorkspaceHead::default()
    };
    (area, zone, object, head)
}
