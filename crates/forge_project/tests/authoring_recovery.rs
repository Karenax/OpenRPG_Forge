use forge_project::{
    authoring::{AuthoringRequest as Request, RecoveryReview, host::AuthoringHost, new_terrain},
    terrain_edit::TerrainEditSession,
};

fn inspect(host: &mut AuthoringHost, id: forge_core::AreaId) -> RecoveryReview {
    host.execute(Request::InspectRecovery { area_id: id })
        .unwrap()
        .recovery
        .unwrap()
}
fn choice(r: RecoveryReview, promote: bool) -> Request {
    Request::Recover {
        area_id: r.area_id,
        expected_saved: r.saved.map(|v| v.archive_hash),
        expected_pending: r.pending.archive_hash,
        promote,
    }
}

#[test]
fn recovery_binds_both_versions_archives_them_and_requires_closed_session() {
    let root = std::env::temp_dir().join(format!("forge-recovery-host-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let mut host = AuthoringHost::new(&root).unwrap();
    let original = new_terrain("recovery", "zone", "Saved").unwrap();
    let id = original.area_id;
    host.execute(Request::Create {
        document: Box::new(original.clone()),
    })
    .unwrap();
    host.execute(Request::Save).unwrap();
    let dir = root.join(id.to_string());
    let old = std::fs::read(dir.join("authoring.json")).unwrap();
    let mut candidate = original.clone();
    candidate.name = "Recovered".into();
    let bytes = TerrainEditSession::new(candidate.clone())
        .unwrap()
        .archive_bytes()
        .unwrap();
    std::fs::write(dir.join("authoring.pending"), &bytes).unwrap();
    assert!(
        host.execute(Request::InspectRecovery { area_id: id })
            .is_err()
    );
    host.execute(Request::Close).unwrap();
    let reviewed = inspect(&mut host, id);
    assert_eq!(reviewed.pending.document, Some(candidate.clone()));
    std::fs::write(dir.join("authoring.pending"), b"corrupt").unwrap();
    assert!(host.execute(choice(reviewed.clone(), true)).is_err());
    let invalid = inspect(&mut host, id);
    assert!(invalid.pending.document.is_none());
    assert!(host.execute(choice(invalid.clone(), true)).is_err());
    host.execute(choice(invalid.clone(), false)).unwrap();
    assert_eq!(
        std::fs::read(dir.join(format!("recovery-{}.json", invalid.pending.archive_hash))).unwrap(),
        b"corrupt"
    );
    assert_eq!(std::fs::read(dir.join("authoring.json")).unwrap(), old);
    std::fs::write(dir.join("authoring.pending"), &bytes).unwrap();
    // A changed saved archive must also invalidate the earlier review.
    std::fs::write(dir.join("authoring.json"), b"changed saved bytes").unwrap();
    assert!(host.execute(choice(reviewed, true)).is_err());
    std::fs::write(dir.join("authoring.json"), &old).unwrap();
    let current = inspect(&mut host, id);
    host.execute(choice(current.clone(), true)).unwrap();
    assert_eq!(
        std::fs::read(dir.join(format!(
            "recovery-{}.json",
            current.saved.unwrap().archive_hash
        )))
        .unwrap(),
        old
    );
    assert_eq!(
        std::fs::read(dir.join(format!("recovery-{}.json", current.pending.archive_hash))).unwrap(),
        bytes
    );
    assert_eq!(
        host.execute(Request::Open { area_id: id })
            .unwrap()
            .document,
        Some(candidate)
    );
    drop(host);
    std::fs::remove_dir_all(root).unwrap();
}

#[test]
fn first_save_recovery_and_foreign_identity_rejection() {
    let root = std::env::temp_dir().join(format!("forge-recovery-first-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let mut host = AuthoringHost::new(&root).unwrap();
    let original = new_terrain("recovery", "first", "First").unwrap();
    let id = original.area_id;
    let dir = root.join(id.to_string());
    std::fs::create_dir(&dir).unwrap();
    let bytes = TerrainEditSession::new(original.clone())
        .unwrap()
        .archive_bytes()
        .unwrap();
    std::fs::write(dir.join("authoring.pending"), &bytes).unwrap();
    let review = inspect(&mut host, id);
    assert!(review.saved.is_none());
    host.execute(choice(review, true)).unwrap();
    let mut foreign = original.clone();
    foreign.project_id = forge_core::ProjectId::deterministic("different");
    std::fs::write(
        dir.join("authoring.pending"),
        TerrainEditSession::new(foreign)
            .unwrap()
            .archive_bytes()
            .unwrap(),
    )
    .unwrap();
    let review = inspect(&mut host, id);
    assert!(review.pending.document.is_none());
    assert!(host.execute(choice(review, true)).is_err());
    let other = new_terrain("other", "other", "Other").unwrap();
    std::fs::write(
        dir.join("authoring.pending"),
        TerrainEditSession::new(other)
            .unwrap()
            .archive_bytes()
            .unwrap(),
    )
    .unwrap();
    assert!(inspect(&mut host, id).pending.document.is_none());
    assert_eq!(std::fs::read(dir.join("authoring.json")).unwrap(), bytes);
    drop(host);
    std::fs::remove_dir_all(root).unwrap();
}
