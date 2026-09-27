#![cfg(not(target_arch = "wasm32"))]
use forge_core::{CommandId, ModuleId, bytes_sha256};
use forge_project::{
    audit::AuditStamp,
    authoring::{
        AuthoringRequest, host::AuthoringHost, new_terrain, workspace::WorkspaceHostRequest as W,
    },
    workspace::{
        file_session::FileWorkspaceSession,
        objects::ReviewedZone,
        session::{WorkspaceAction, WorkspaceRequest},
    },
};

#[test]
fn host_adoption_persists_the_explicit_audit_stamp() {
    let root = std::env::temp_dir().join(format!(
        "forge-workspace-adopt-audit-{}",
        std::process::id()
    ));
    std::fs::create_dir(&root).unwrap();
    let document = new_terrain("adopt-audit", "area", "Source").unwrap();
    let area = document.area_id;
    let project = document.project_id;
    let module = ModuleId::deterministic("adopt-audit-module");
    let mut host = AuthoringHost::new(&root).unwrap();
    host.execute(AuthoringRequest::CreateZone {
        document: Box::new(document),
    })
    .unwrap();
    host.execute(AuthoringRequest::Save).unwrap();
    host.execute(AuthoringRequest::Close).unwrap();
    host.execute_workspace(W::Create).unwrap();

    for (key, action) in [
        (
            "adopt-audit-project",
            WorkspaceAction::CreateProject {
                id: project,
                name: "Projet".into(),
            },
        ),
        (
            "adopt-audit-module",
            WorkspaceAction::CreateModule {
                project_id: project,
                id: module,
                name: "Module".into(),
            },
        ),
    ] {
        let head = host.execute_workspace(W::Inspect).unwrap().head.unwrap();
        host.execute_workspace_with_audit(
            W::Apply {
                request: Box::new(WorkspaceRequest {
                    command_id: CommandId::deterministic(key),
                    base_revision: head.revision,
                    base_hash: head.content_hash().unwrap(),
                    action,
                }),
            },
            AuditStamp::human("author-42", "2026-09-22T12:34:56Z").unwrap(),
        )
        .unwrap();
    }
    host.execute_workspace(W::Save).unwrap();

    let bytes = std::fs::read(root.join(area.to_string()).join("authoring.json")).unwrap();
    let review = host
        .execute_workspace(W::ReviewAdoption {
            area_id: area,
            project_id: project,
            module_id: module,
            expected_archive_hash: bytes_sha256(&bytes),
        })
        .unwrap()
        .receipt
        .unwrap();
    let request = W::Adopt {
        request: Box::new(review.request),
    };
    assert!(
        host.execute_workspace_with_audit(request.clone(), AuditStamp::legacy())
            .is_err()
    );
    let stamp = AuditStamp::ai("forge-assistant", "2026-09-22T12:34:57Z").unwrap();
    let adopted = host
        .execute_workspace_with_audit(request, stamp.clone())
        .unwrap();
    assert_eq!(adopted.receipt.as_ref().unwrap().audit, stamp);
    drop(host);
    std::fs::remove_dir_all(root).unwrap();
}

fn request(file: &FileWorkspaceSession, key: &str, action: WorkspaceAction) -> WorkspaceRequest {
    WorkspaceRequest {
        command_id: CommandId::deterministic(key),
        base_revision: file.session().head().revision,
        base_hash: file.session().head().content_hash().unwrap(),
        action,
    }
}

#[test]
fn adoption_locks_source_publishes_exact_bytes_and_blocks_legacy_even_after_undo() {
    let root = std::env::temp_dir().join(format!("forge-workspace-adopt-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let document = new_terrain("adopt", "area", "Source").unwrap();
    let area = document.area_id;
    let project = document.project_id;
    let module = ModuleId::deterministic("module");
    let mut host = AuthoringHost::new(&root).unwrap();
    host.execute(AuthoringRequest::CreateZone {
        document: Box::new(document),
    })
    .unwrap();
    host.execute(AuthoringRequest::Save).unwrap();
    let source = root.join(area.to_string()).join("authoring.json");
    let bytes = std::fs::read(&source).unwrap();
    let reviewed = ReviewedZone::from_bytes(&bytes, bytes_sha256(&bytes)).unwrap();
    let mut file = FileWorkspaceSession::create(&root).unwrap();
    let create = request(
        &file,
        "project",
        WorkspaceAction::CreateProject {
            id: project,
            name: "Projet".into(),
        },
    );
    file.execute(&create).unwrap();
    let create = request(
        &file,
        "module",
        WorkspaceAction::CreateModule {
            project_id: project,
            id: module,
            name: "Module".into(),
        },
    );
    file.execute(&create).unwrap();
    file.save().unwrap();
    let adopt = request(
        &file,
        "adopt",
        WorkspaceAction::AttachZone {
            project_id: project,
            module_id: module,
            reference: reviewed.reference().clone(),
        },
    );
    assert!(file.adopt(&adopt).is_err()); // source still open
    host.execute(AuthoringRequest::Close).unwrap();
    let adopt = check_adoption_review(&file, &reviewed, module);
    assert!(file.execute(&adopt).is_err()); // no bypass of explicit adoption
    let mut wrong = adopt.clone();
    if let WorkspaceAction::AttachZone { reference, .. } = &mut wrong.action {
        reference.archive_hash = bytes_sha256(b"changed");
    }
    assert!(file.adopt(&wrong).is_err());
    file.adopt(&adopt).unwrap();
    drop(file); // abandon after object publication, before head save
    host.execute(AuthoringRequest::Open { area_id: area })
        .unwrap();
    host.execute(AuthoringRequest::Close).unwrap();
    let mut file = FileWorkspaceSession::open(&root).unwrap();
    assert!(
        file.session().head().projects[0].modules[0]
            .zones
            .is_empty()
    );
    let receipt = file.adopt(&adopt).unwrap();
    assert_eq!(receipt, file.adopt(&adopt).unwrap());
    assert!(
        host.execute(AuthoringRequest::Open { area_id: area })
            .is_err()
    ); // held lock
    file.save().unwrap();
    assert!(
        host.execute(AuthoringRequest::Open { area_id: area })
            .unwrap_err()
            .contains("belongs to the workspace")
    );
    assert_eq!(bytes, std::fs::read(&source).unwrap());
    drop(file);
    let file = FileWorkspaceSession::open(&root).unwrap();
    assert_eq!(
        file.session().head().projects[0].modules[0].zones[0],
        *reviewed.reference()
    );
    drop(file);
    check_managed_host(&mut host, area);
    let mut file = check_managed_edit(FileWorkspaceSession::open(&root).unwrap(), &root, area);
    let undo = request(&file, "undo", WorkspaceAction::Undo);
    file.execute(&undo).unwrap();
    file.save().unwrap();
    assert!(
        host.execute(AuthoringRequest::Open { area_id: area })
            .is_err()
    );
    assert_eq!(bytes, std::fs::read(&source).unwrap());
    drop(file);
    drop(host);
    std::fs::remove_dir_all(root).unwrap();
}

fn check_adoption_review(
    file: &FileWorkspaceSession,
    reviewed: &ReviewedZone,
    module: ModuleId,
) -> WorkspaceRequest {
    let before = file.session().head().clone();
    let zone = reviewed.reference();
    let project = reviewed.project_id();
    let receipt = file
        .review_adoption(zone.area_id, project, module, zone.archive_hash)
        .unwrap();
    assert_eq!(*file.session().head(), before);
    assert!(
        file.review_adoption(zone.area_id, project, module, bytes_sha256(b"stale"))
            .is_err()
    );
    receipt.request
}

fn check_managed_host(host: &mut AuthoringHost, area: forge_core::AreaId) {
    use forge_project::authoring::workspace::WorkspaceHostRequest as W;
    use forge_project::zone_authoring::session::{ZoneAction, ZoneRequest};
    host.execute_workspace(W::Open).unwrap();
    let opened = host
        .execute(AuthoringRequest::OpenManaged { area_id: area })
        .unwrap();
    let head = opened.zone_head.unwrap();
    let mut corrections = head.corrections.clone();
    corrections.name = Some("Zone gérée".into());
    let edit = ZoneRequest {
        command_id: CommandId::deterministic("host-managed-edit"),
        expected_hash: head.content_hash().unwrap(),
        expected_revision: head.revision,
        action: ZoneAction::edit(corrections),
    };
    let preview = host
        .execute(AuthoringRequest::PreviewZone {
            request: edit.clone(),
        })
        .unwrap();
    let applied = host
        .execute(AuthoringRequest::ApplyZone { request: edit })
        .unwrap();
    assert_eq!(preview.zone_receipt, applied.zone_receipt);
    assert!(applied.dirty);
    assert!(host.execute(AuthoringRequest::Close).is_err());
    assert!(host.execute_workspace(W::Close).is_err());
    let hash = applied.zone_head.unwrap().content_hash().unwrap();
    let playing = host
        .execute(AuthoringRequest::StartPlaytest {
            expected_hash: hash,
        })
        .unwrap();
    assert_eq!(playing.playtest_hash, Some(hash));
    assert!(host.execute_workspace(W::Save).is_err());
    host.execute(AuthoringRequest::StopPlaytest { diagnostics: None })
        .unwrap();
    assert!(!host.execute(AuthoringRequest::Save).unwrap().dirty);
    host.execute(AuthoringRequest::Close).unwrap();
    let reopened = host
        .execute(AuthoringRequest::OpenManaged { area_id: area })
        .unwrap();
    let head = reopened.zone_head.unwrap();
    assert_eq!(head.content_hash().unwrap(), hash);
    let undo = ZoneRequest {
        command_id: CommandId::deterministic("managed-direct-undo"),
        expected_hash: hash,
        expected_revision: head.revision,
        action: ZoneAction::Undo,
    };
    let undone = host
        .execute(AuthoringRequest::ApplyZone { request: undo })
        .unwrap();
    assert_ne!(undone.zone_head.unwrap().content_hash().unwrap(), hash);
    check_managed_compaction(host);
    host.execute(AuthoringRequest::Save).unwrap();
    host.execute(AuthoringRequest::Close).unwrap();
    host.execute_workspace(W::Close).unwrap();
}

fn check_managed_compaction(host: &mut AuthoringHost) {
    let before = host
        .execute(AuthoringRequest::Inspect)
        .unwrap()
        .zone_head
        .unwrap();
    let hash = before.content_hash().unwrap();
    let history = host
        .execute(AuthoringRequest::ReadHistory {
            expected_hash: hash,
            offset: 0,
        })
        .unwrap()
        .history;
    let review = host
        .execute(AuthoringRequest::ReviewZoneCompaction)
        .unwrap()
        .zone_compaction
        .unwrap();
    let mut wrong = review.clone();
    wrong.target_bytes += 1;
    assert!(
        host.execute(AuthoringRequest::CompactZoneArchive { review: wrong })
            .is_err()
    );
    let applied = host
        .execute(AuthoringRequest::CompactZoneArchive {
            review: review.clone(),
        })
        .unwrap();
    assert_eq!(applied.zone_head.unwrap(), before);
    assert!(applied.dirty);
    host.execute(AuthoringRequest::CompactZoneArchive { review })
        .unwrap();
    assert_eq!(
        host.execute(AuthoringRequest::ReadHistory {
            expected_hash: hash,
            offset: 0
        })
        .unwrap()
        .history,
        history
    );
    assert!(
        host.execute(AuthoringRequest::ReviewZoneCompaction)
            .is_err()
    );
}

fn check_managed_edit(
    mut file: FileWorkspaceSession,
    root: &std::path::Path,
    area: forge_core::AreaId,
) -> FileWorkspaceSession {
    let start = forge_project::zone_authoring::start::ZoneStart {
        x_m: 10.0,
        z_m: 12.0,
        facing_degrees: Some(90.0),
    };
    let zone = file.read_zone(area).unwrap();
    assert!(zone.review_compaction().is_err()); // persisted compact encoding
    let zone_request = zone
        .head()
        .prepare_start(Some(start), CommandId::deterministic("managed-start"))
        .unwrap();
    let old_workspace = file.session().head().clone();
    let (preview, zone_preview) = file
        .preview_zone_edit(
            area,
            &zone_request,
            CommandId::deterministic("managed-edit"),
        )
        .unwrap();
    assert_eq!(*file.session().head(), old_workspace);
    assert!(
        file.read_zone(area)
            .unwrap()
            .head()
            .corrections
            .start
            .is_none()
    );
    assert!(file.execute(&preview.request).is_err());
    let result = file
        .apply_zone_edit(&preview.request, &zone_request)
        .unwrap();
    assert_eq!(result, (preview.clone(), zone_preview));
    assert_eq!(
        result,
        file.apply_zone_edit(&preview.request, &zone_request)
            .unwrap()
    );
    assert!(file.is_dirty());
    assert!(
        file.read_zone(area)
            .unwrap()
            .head()
            .corrections
            .start
            .is_some()
    );
    file.save().unwrap();
    drop(file);
    let mut file = FileWorkspaceSession::open(root).unwrap();
    assert!(
        file.read_zone(area)
            .unwrap()
            .head()
            .corrections
            .start
            .is_some()
    );
    let undo_edit = request(&file, "undo-edit", WorkspaceAction::Undo);
    file.execute(&undo_edit).unwrap();
    assert!(
        file.read_zone(area)
            .unwrap()
            .head()
            .corrections
            .start
            .is_none()
    );
    file
}
