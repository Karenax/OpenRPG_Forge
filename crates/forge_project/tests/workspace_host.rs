#![cfg(not(target_arch = "wasm32"))]
use forge_core::{CommandId, ProjectId};
use forge_project::{
    authoring::{host::AuthoringHost, workspace::WorkspaceHostRequest as Request},
    workspace::session::{WorkspaceAction, WorkspaceRequest},
};

#[test]
fn workspace_protocol_preview_apply_save_close_reopen() {
    let root = std::env::temp_dir().join(format!("forge-workspace-host-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let mut host = AuthoringHost::new(&root).unwrap();
    assert!(
        host.execute_workspace(Request::Inspect)
            .unwrap()
            .head
            .is_none()
    );
    let state = host.execute_workspace(Request::Create).unwrap();
    let head = state.head.unwrap();
    let command = WorkspaceRequest {
        command_id: CommandId::deterministic("host-project"),
        base_hash: head.content_hash().unwrap(),
        base_revision: head.revision,
        action: WorkspaceAction::CreateProject {
            id: ProjectId::deterministic("host-project"),
            name: "Projet natif".into(),
        },
    };
    let preview = host
        .execute_workspace(Request::Preview {
            request: Box::new(command.clone()),
        })
        .unwrap();
    assert!(preview.head.unwrap().projects.is_empty());
    let wire = serde_json::to_vec(&Request::Apply {
        request: Box::new(command),
    })
    .unwrap();
    let applied = host
        .execute_workspace(serde_json::from_slice(&wire).unwrap())
        .unwrap();
    assert_eq!(preview.receipt, applied.receipt);
    assert!(applied.dirty);
    assert!(host.execute_workspace(Request::Close).is_err());
    assert!(host.execute_workspace(Request::Open).is_err());
    assert!(!host.execute_workspace(Request::Save).unwrap().dirty);
    host.execute_workspace(Request::Close).unwrap();
    let restored = host.execute_workspace(Request::Open).unwrap();
    assert_eq!(restored.head, applied.head);
    assert!(!restored.dirty);
    let history = host
        .execute_workspace(Request::ReadHistory {
            expected_hash: restored.head.as_ref().unwrap().content_hash().unwrap(),
            offset: 0,
        })
        .unwrap();
    assert_eq!(history.head, restored.head);
    assert!(!history.dirty);
    assert_eq!(history.history.unwrap().entries.len(), 1);
    drop(host);
    std::fs::remove_dir_all(root).unwrap();
}
