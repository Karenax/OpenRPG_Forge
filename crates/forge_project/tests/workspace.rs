use forge_core::{AreaId, CommandId, ModuleId, ProjectId};
use forge_project::workspace::session::{WorkspaceAction, WorkspaceRequest, WorkspaceSession};
use forge_project::workspace::{MAX_WORKSPACE_BYTES, WorkspaceHead, ZoneReference};

fn request(session: &WorkspaceSession, key: &str, action: WorkspaceAction) -> WorkspaceRequest {
    WorkspaceRequest {
        command_id: CommandId::deterministic(key),
        base_revision: session.head().revision,
        base_hash: session.head().content_hash().unwrap(),
        action,
    }
}

#[test]
fn creation_preview_retry_undo_redo_and_stale_review() {
    let mut session = WorkspaceSession::new(WorkspaceHead::default()).unwrap();
    let project = ProjectId::deterministic("p");
    let create = request(
        &session,
        "create",
        WorkspaceAction::CreateProject {
            id: project,
            name: "Forêt d’été".into(),
        },
    );
    let review = session.preview(&create).unwrap();
    assert!(session.head().projects.is_empty());
    assert_eq!(review, session.execute(&create).unwrap());
    assert_eq!(review, session.execute(&create).unwrap());
    assert_eq!(session.audit().len(), 1);
    let module = request(
        &session,
        "module",
        WorkspaceAction::CreateModule {
            project_id: project,
            id: ModuleId::deterministic("m"),
            name: "Chapitre 1".into(),
        },
    );
    session.execute(&module).unwrap();
    let mut stale = module.clone();
    stale.command_id = CommandId::deterministic("stale");
    assert!(session.execute(&stale).is_err());
    let undo = request(&session, "undo", WorkspaceAction::Undo);
    session.execute(&undo).unwrap();
    assert!(session.head().projects[0].modules.is_empty());
    let redo = request(&session, "redo", WorkspaceAction::Redo);
    session.execute(&redo).unwrap();
    assert_eq!(session.head().projects[0].modules.len(), 1);
    assert_eq!(session.head().revision.get(), 5);
    let bytes = session.head().to_bytes().unwrap();
    assert_eq!(*session.head(), WorkspaceHead::from_bytes(&bytes).unwrap());
    let archive = session.archive_bytes().unwrap();
    let mut reopened = WorkspaceSession::from_archive_bytes(&archive).unwrap();
    assert_eq!(session.audit(), reopened.audit());
    let undo = request(&reopened, "undo-after-reopen", WorkspaceAction::Undo);
    reopened.execute(&undo).unwrap();
    assert!(reopened.head().projects[0].modules.is_empty());
    let mut corrupt: serde_json::Value = serde_json::from_slice(&archive).unwrap();
    corrupt["head"]["projects"][0]["name"] = "Changed outside commands".into();
    assert!(WorkspaceSession::from_archive_bytes(&serde_json::to_vec(&corrupt).unwrap()).is_err());
    let mut reused = create;
    reused.action = WorkspaceAction::Undo;
    assert!(session.execute(&reused).is_err());
}

#[test]
fn invalid_documents_and_duplicate_membership_are_rejected() {
    let project = ProjectId::deterministic("p");
    let empty = WorkspaceHead::default();
    assert!(empty.with_project(project, " ".into()).is_err());
    assert!(empty.with_project(project, "line\nbreak".into()).is_err());
    let head = empty.with_project(project, "Projet".into()).unwrap();
    assert!(head.with_project(project, "Autre".into()).is_err());
    assert!(
        head.with_project(ProjectId::deterministic("other"), "Projet".into())
            .is_err()
    );
    assert!(
        empty
            .with_module(project, ModuleId::deterministic("m"), "M".into())
            .is_err()
    );
    let mut head = head
        .with_module(project, ModuleId::deterministic("m"), "M".into())
        .unwrap();
    let reference = ZoneReference {
        area_id: AreaId::deterministic("a"),
        archive_hash: head.content_hash().unwrap(),
        head_hash: head.content_hash().unwrap(),
    };
    head.projects[0].modules[0].zones.push(reference.clone());
    head.validate().unwrap();
    head.projects[0].modules[0].zones.push(reference);
    assert!(head.validate().is_err());
    let mut wrong = empty;
    wrong.schema_version = 99;
    assert!(WorkspaceHead::from_bytes(&serde_json::to_vec(&wrong).unwrap()).is_err());
    assert!(WorkspaceHead::from_bytes(&vec![b' '; MAX_WORKSPACE_BYTES + 1]).is_err());
}

#[cfg(not(target_arch = "wasm32"))]
#[test]
fn atomic_project_save_reopens_with_history_and_preserves_legacy() {
    use forge_project::workspace::file_session::FileWorkspaceSession;
    let root =
        std::env::temp_dir().join(format!("forge-workspace-contract-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let legacy = root.join("legacy-authoring.json");
    std::fs::write(&legacy, b"untouched legacy bytes").unwrap();
    let mut file = FileWorkspaceSession::create(&root).unwrap();
    let create = request(
        file.session(),
        "saved-project",
        WorkspaceAction::CreateProject {
            id: ProjectId::deterministic("saved"),
            name: "Projet sauvegardé".into(),
        },
    );
    file.execute(&create).unwrap();
    assert!(file.is_dirty());
    file.save().unwrap();
    assert!(!file.is_dirty());
    assert!(FileWorkspaceSession::open(&root).is_err());
    drop(file);
    let mut reopened = FileWorkspaceSession::open(&root).unwrap();
    assert_eq!(
        reopened.session().head().projects[0].name,
        "Projet sauvegardé"
    );
    let undo = request(reopened.session(), "saved-undo", WorkspaceAction::Undo);
    reopened.execute(&undo).unwrap();
    reopened.save().unwrap();
    drop(reopened);
    let reopened = FileWorkspaceSession::open(&root).unwrap();
    assert!(reopened.session().head().projects.is_empty());
    assert_eq!(std::fs::read(&legacy).unwrap(), b"untouched legacy bytes");
    drop(reopened);
    std::fs::remove_dir_all(&root).unwrap();
}
