use forge_core::CommandId;
use forge_project::{
    authoring::{
        AuthoringRequest,
        history::{self, HistoryAction, HistoryChange},
        host::AuthoringHost,
        new_terrain,
    },
    terrain_edit::{TerrainAction, TerrainEdit, TerrainEditRequest, TerrainEditSession},
};

fn apply(session: &mut TerrainEditSession, action: TerrainAction, index: usize) {
    session
        .execute(&TerrainEditRequest {
            command_id: CommandId::deterministic(format!("history-{index}")),
            expected_revision: session.document().revision,
            expected_hash: session.document().content_hash().unwrap(),
            action,
        })
        .unwrap();
}

#[test]
fn journal_pages_survive_reopen_and_preserve_undo_redo_semantics() {
    let mut session =
        TerrainEditSession::new(new_terrain("history", "zone", "Initial").unwrap()).unwrap();
    assert!(!session.can_undo() && !session.can_redo());
    for index in 0..35 {
        apply(
            &mut session,
            TerrainAction::Edit(TerrainEdit::Rename {
                name: format!("Name {index}"),
            }),
            index,
        );
    }
    assert!(session.can_undo() && !session.can_redo());
    let old_hash = session.document().content_hash().unwrap();
    apply(&mut session, TerrainAction::Undo, 35);
    assert!(session.can_undo() && session.can_redo());
    apply(&mut session, TerrainAction::Redo, 36);
    let hash = session.document().content_hash().unwrap();
    let bytes = session.archive_bytes().unwrap();
    let page = history::read(&session, hash, 0).unwrap();
    assert_eq!(page.total, 37);
    assert_eq!(page.entries.len(), history::PAGE_SIZE);
    assert_eq!(page.entries[0].action, HistoryAction::Redo);
    assert_eq!(page.entries[1].action, HistoryAction::Undo);
    assert_eq!(
        page.entries[0].change,
        HistoryChange::Rename {
            before: "Name 33".into(),
            after: "Name 34".into()
        }
    );
    assert_eq!(history::read(&session, hash, 32).unwrap().entries.len(), 5);
    assert!(history::read(&session, old_hash, 0).is_err());
    assert!(history::read(&session, hash, usize::MAX).is_err());
    assert_eq!(session.archive_bytes().unwrap(), bytes);
    let mut restored = TerrainEditSession::from_archive_bytes(&bytes).unwrap();
    assert_eq!(history::read(&restored, hash, 0).unwrap(), page);
    apply(&mut restored, TerrainAction::Undo, 37);
    apply(
        &mut restored,
        TerrainAction::Edit(TerrainEdit::SetBaseElevation { metres: 8.0 }),
        38,
    );
    assert!(!restored.can_redo());
    let page = history::read(&restored, restored.document().content_hash().unwrap(), 0).unwrap();
    assert_eq!(
        page.entries[0].change,
        HistoryChange::BaseElevation {
            before: 0.0,
            after: 8.0
        }
    );
}

#[test]
fn host_history_read_preserves_dirty_session_and_playtest() {
    let root = std::env::temp_dir().join(format!("forge-history-host-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let mut host = AuthoringHost::new(&root).unwrap();
    let doc = new_terrain("history", "host", "Initial").unwrap();
    let hash = doc.content_hash().unwrap();
    host.execute(AuthoringRequest::Create {
        document: Box::new(doc.clone()),
    })
    .unwrap();
    let read = host
        .execute(AuthoringRequest::ReadHistory {
            expected_hash: hash,
            offset: 0,
        })
        .unwrap();
    assert!(read.dirty);
    assert!(!read.can_undo && !read.can_redo);
    assert!(read.history.unwrap().entries.is_empty());
    assert!(
        !root
            .join(doc.area_id.to_string())
            .join("authoring.json")
            .exists()
    );
    host.execute(AuthoringRequest::StartPlaytest {
        expected_hash: hash,
    })
    .unwrap();
    let read = host
        .execute(AuthoringRequest::ReadHistory {
            expected_hash: hash,
            offset: 0,
        })
        .unwrap();
    assert_eq!(read.playtest_hash, Some(hash));
    assert_eq!(read.document, Some(doc));
    assert!(read.dirty);
    drop(host);
    std::fs::remove_dir_all(root).unwrap();
}
