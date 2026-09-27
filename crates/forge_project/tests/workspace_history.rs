use forge_core::{CommandId, ProjectId};
use forge_project::workspace::{
    WorkspaceHead, history,
    session::{WorkspaceAction, WorkspaceRequest, WorkspaceSession},
};

fn apply(session: &mut WorkspaceSession, key: &str, action: WorkspaceAction) {
    session
        .execute(&WorkspaceRequest {
            command_id: CommandId::deterministic(key),
            base_hash: session.head().content_hash().unwrap(),
            base_revision: session.head().revision,
            action,
        })
        .unwrap();
}

#[test]
fn workspace_history_pages_are_bounded_exact_read_only_and_replayed() {
    let mut session = WorkspaceSession::new(WorkspaceHead::default()).unwrap();
    assert!(
        history::read(&session, session.head().content_hash().unwrap(), 0)
            .unwrap()
            .entries
            .is_empty()
    );
    for i in 0..23 {
        let key = format!("project-{i}");
        apply(
            &mut session,
            &key,
            WorkspaceAction::CreateProject {
                id: ProjectId::deterministic(&key),
                name: format!("Projet {i}"),
            },
        );
    }
    let bytes = session.archive_bytes().unwrap();
    let hash = session.head().content_hash().unwrap();
    let first = history::read(&session, hash, 0).unwrap();
    assert_eq!(first.total, 23);
    assert_eq!(first.entries.len(), 20);
    assert_eq!(
        first.entries[0].command_id,
        CommandId::deterministic("project-22")
    );
    let second = history::read(&session, hash, first.next_offset.unwrap()).unwrap();
    assert_eq!(second.entries.len(), 3);
    assert_eq!(
        second.entries[0].command_id,
        CommandId::deterministic("project-2")
    );
    assert!(second.next_offset.is_none());
    assert!(history::read(&session, hash, usize::MAX).is_err());
    assert_eq!(session.archive_bytes().unwrap(), bytes);
    assert_eq!(
        history::read(
            &WorkspaceSession::from_archive_bytes(&bytes).unwrap(),
            hash,
            0
        )
        .unwrap(),
        first
    );
    let wire = serde_json::to_vec(&first).unwrap();
    assert!(wire.len() < 16 * 1024);
    assert_eq!(
        serde_json::from_slice::<history::WorkspaceHistoryPage>(&wire).unwrap(),
        first
    );
    apply(&mut session, "undo", WorkspaceAction::Undo);
    assert!(history::read(&session, hash, 20).is_err());
    apply(&mut session, "redo", WorkspaceAction::Redo);
    let latest = history::read(&session, session.head().content_hash().unwrap(), 0).unwrap();
    assert_eq!(latest.entries[0].action, WorkspaceAction::Redo);
    assert_eq!(latest.entries[1].action, WorkspaceAction::Undo);
}
