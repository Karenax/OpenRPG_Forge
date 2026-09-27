#![cfg(not(target_arch = "wasm32"))]
use forge_core::{CommandId, ModuleId, ProjectId};
use forge_project::{
    audit::AuditStamp,
    authoring::{
        AuthoringRequest as A,
        creation::{NewTerrainSettings, prepare_module_terrain},
        host::AuthoringHost,
        workspace::WorkspaceHostRequest as W,
    },
    workspace::session::{WorkspaceAction, WorkspaceRequest},
};

fn change(host: &mut AuthoringHost, key: &str, action: WorkspaceAction) {
    let head = host.execute_workspace(W::Inspect).unwrap().head.unwrap();
    host.execute_workspace(W::Apply {
        request: Box::new(WorkspaceRequest {
            command_id: CommandId::deterministic(key),
            base_hash: head.content_hash().unwrap(),
            base_revision: head.revision,
            action,
        }),
    })
    .unwrap();
}

#[test]
#[allow(
    clippy::too_many_lines,
    reason = "the transaction scenario must retain its create, save, reopen and undo sequence"
)]
fn create_zone_in_module_without_legacy_archive_then_save_reopen_and_undo() {
    let root = std::env::temp_dir().join(format!(
        "forge-workspace-create-zone-{}",
        std::process::id()
    ));
    std::fs::create_dir(&root).unwrap();
    let (mut host, project, module) = setup(&root);
    let document = prepare_module_terrain(
        project,
        module,
        "zone",
        "Collines",
        NewTerrainSettings {
            seed: 42,
            amplitude_m: 24.0,
            ..NewTerrainSettings::default()
        },
    )
    .unwrap();
    let area = document.area_id;
    assert_eq!(document.project_id, project);
    let review = host
        .execute_workspace(W::ReviewNewZone {
            module_id: module,
            document: Box::new(document.clone()),
        })
        .unwrap()
        .receipt
        .unwrap();
    assert!(!root.join(area.to_string()).exists());
    assert_eq!(
        std::fs::read_dir(root.join("workspace-v1/objects"))
            .unwrap()
            .count(),
        0
    );
    let mut changed = document.clone();
    changed.seed = 43;
    assert!(
        host.execute_workspace(W::CreateNewZone {
            request: Box::new(review.request.clone()),
            document: Box::new(changed)
        })
        .is_err()
    );
    assert!(!root.join(area.to_string()).exists());
    let request = W::CreateNewZone {
        request: Box::new(review.request),
        document: Box::new(document.clone()),
    };
    assert!(
        host.execute_workspace_with_audit(request.clone(), AuditStamp::legacy())
            .is_err()
    );
    assert!(!root.join(area.to_string()).exists());
    let stamp = AuditStamp::human("author-42", "2026-09-22T12:34:56Z").unwrap();
    let created = host
        .execute_workspace_with_audit(request.clone(), stamp.clone())
        .unwrap();
    assert_eq!(created.receipt.as_ref().unwrap().audit, stamp);
    assert!(created.dirty);
    assert_eq!(
        host.execute_workspace(request).unwrap().receipt,
        created.receipt
    );
    let source = root.join(area.to_string()).join("authoring.json");
    assert!(!source.exists());
    assert!(
        host.execute(A::CreateZone {
            document: Box::new(document.clone())
        })
        .is_err()
    );
    let active = host.execute(A::OpenManaged { area_id: area }).unwrap();
    assert_eq!(active.document.unwrap().name, "Collines");
    check_history(
        &mut host,
        created.head.as_ref().unwrap().content_hash().unwrap(),
    );
    assert!(host.execute(A::Close).is_err());
    host.execute(A::Save).unwrap();
    host.execute(A::Close).unwrap();
    host.execute_workspace(W::Close).unwrap();
    assert_eq!(host.execute_workspace(W::Open).unwrap().head, created.head);
    assert!(!source.exists());
    change(&mut host, "undo", WorkspaceAction::Undo);
    assert!(host.execute(A::OpenManaged { area_id: area }).is_err());
    change(&mut host, "redo", WorkspaceAction::Redo);
    assert_eq!(
        host.execute(A::OpenManaged { area_id: area })
            .unwrap()
            .document
            .unwrap()
            .name,
        "Collines"
    );
    assert!(
        host.execute_workspace(W::ReviewNewZone {
            module_id: module,
            document: Box::new(document)
        })
        .is_err()
    );
    drop(host);
    std::fs::remove_dir_all(root).unwrap();
}

fn check_history(host: &mut AuthoringHost, expected_hash: forge_core::ContentHash) {
    let history = host
        .execute_workspace(W::ReadHistory {
            expected_hash,
            offset: 0,
        })
        .unwrap()
        .history
        .unwrap();
    assert_eq!(
        history.entries[0].scope.as_deref(),
        Some("Projet / Module / zone 1")
    );
}

fn setup(root: &std::path::Path) -> (AuthoringHost, ProjectId, ModuleId) {
    let mut host = AuthoringHost::new(root).unwrap();
    host.execute_workspace(W::Create).unwrap();
    let project = ProjectId::deterministic("create-project");
    let module = ModuleId::deterministic("create-module");
    change(
        &mut host,
        "project",
        WorkspaceAction::CreateProject {
            id: project,
            name: "Projet".into(),
        },
    );
    change(
        &mut host,
        "module",
        WorkspaceAction::CreateModule {
            project_id: project,
            id: module,
            name: "Module".into(),
        },
    );
    host.execute_workspace(W::Save).unwrap();
    (host, project, module)
}
