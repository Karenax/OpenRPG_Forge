use forge_core::{CommandId, ModuleId, ProjectId, bytes_sha256, canonical_json_sha256};
use forge_project::workspace::{
    MAX_MODULE_ORIGIN_PACKAGE_BYTES, ModuleOriginPin, WorkspaceHead,
    session::{WorkspaceAction, WorkspaceRequest, WorkspaceSession},
};

fn pin() -> ModuleOriginPin {
    ModuleOriginPin {
        package_sha256: bytes_sha256(b"original package"),
        package_bytes: 128,
        receipt_sha256: bytes_sha256(b"original receipt"),
    }
}
fn request(session: &WorkspaceSession, key: &str, action: WorkspaceAction) -> WorkspaceRequest {
    WorkspaceRequest {
        command_id: CommandId::deterministic(key),
        base_revision: session.head().revision,
        base_hash: session.head().content_hash().unwrap(),
        action,
    }
}
fn session() -> (WorkspaceSession, ProjectId, ModuleId) {
    let project = ProjectId::deterministic("origin/project");
    let module = ModuleId::deterministic("origin/module");
    let mut session = WorkspaceSession::new(WorkspaceHead::default()).unwrap();
    let command = request(
        &session,
        "origin/project",
        WorkspaceAction::CreateProject {
            id: project,
            name: "Project".into(),
        },
    );
    session.execute(&command).unwrap();
    (session, project, module)
}

#[test]
fn legacy_none_preserves_exact_module_json_and_content_hash() {
    let project = ProjectId::deterministic("legacy/project");
    let module = ModuleId::deterministic("legacy/module");
    let head = WorkspaceHead::default()
        .with_project(project, "Project".into())
        .unwrap()
        .with_module(project, module, "Module".into())
        .unwrap();
    let expected = format!(
        "{{\"schema\":\"forge.workspace\",\"schema_version\":1,\"revision\":1,\"projects\":[{{\"id\":\"{project}\",\"name\":\"Project\",\"modules\":[{{\"id\":\"{module}\",\"name\":\"Module\",\"zones\":[]}}]}}]}}"
    );
    assert_eq!(head.to_bytes().unwrap(), expected.as_bytes());
    let legacy: serde_json::Value = serde_json::from_str(&expected).unwrap();
    assert_eq!(
        head.content_hash().unwrap(),
        canonical_json_sha256(&legacy).unwrap()
    );
    assert_eq!(
        WorkspaceHead::from_bytes(expected.as_bytes()).unwrap(),
        head
    );
}

#[test]
fn valid_origin_pin_is_admitted_and_unknown_pin_fields_are_refused() {
    pin().validate().unwrap();
    let mut value = serde_json::to_value(pin()).unwrap();
    value["native_path"] = serde_json::json!("untrusted/original.orpmod");
    assert!(serde_json::from_value::<ModuleOriginPin>(value).is_err());
}

#[test]
fn import_creation_anchor_survives_undo_redo_and_archive_and_cannot_be_replaced() {
    let (mut session, project, module) = session();
    let command = request(
        &session,
        "origin/import",
        WorkspaceAction::ImportModule {
            project_id: project,
            id: module,
            name: "Imported".into(),
            origin: pin(),
        },
    );
    session.execute(&command).unwrap();
    let accepted = session.head().clone();
    assert_eq!(accepted.projects[0].modules[0].origin, Some(pin()));
    let mut changed = pin();
    changed.package_sha256 = bytes_sha256(b"other package");
    let replacement = request(
        &session,
        "origin/replace",
        WorkspaceAction::ImportModule {
            project_id: project,
            id: module,
            name: "Replacement".into(),
            origin: changed,
        },
    );
    assert!(session.execute(&replacement).is_err());
    assert_eq!(session.head(), &accepted);
    let undo = request(&session, "origin/undo", WorkspaceAction::Undo);
    session.execute(&undo).unwrap();
    assert!(session.head().projects[0].modules.is_empty());
    let redo = request(&session, "origin/redo", WorkspaceAction::Redo);
    session.execute(&redo).unwrap();
    assert_eq!(session.head().projects[0].modules[0].origin, Some(pin()));
    let reopened = WorkspaceSession::from_archive_bytes(&session.archive_bytes().unwrap()).unwrap();
    assert_eq!(reopened.head(), session.head());
    assert_eq!(reopened.head().projects[0].modules[0].origin, Some(pin()));
}

#[test]
fn invalid_origin_size_is_refused_without_changing_head_or_history() {
    let (mut session, project, module) = session();
    let before = session.archive_bytes().unwrap();
    for bytes in [0, MAX_MODULE_ORIGIN_PACKAGE_BYTES + 1] {
        let mut origin = pin();
        origin.package_bytes = bytes;
        let command = request(
            &session,
            &format!("invalid-size/{bytes}"),
            WorkspaceAction::ImportModule {
                project_id: project,
                id: module,
                name: "Invalid".into(),
                origin,
            },
        );
        assert!(session.execute(&command).is_err());
        assert_eq!(session.archive_bytes().unwrap(), before);
    }
}

#[test]
fn altering_the_canonical_anchor_without_its_command_is_refused_on_replay() {
    let (mut session, project, module) = session();
    let command = request(
        &session,
        "origin/import",
        WorkspaceAction::ImportModule {
            project_id: project,
            id: module,
            name: "Imported".into(),
            origin: pin(),
        },
    );
    session.execute(&command).unwrap();
    let mut archive: serde_json::Value =
        serde_json::from_slice(&session.archive_bytes().unwrap()).unwrap();
    archive["head"]["projects"][0]["modules"][0]["origin"]["receipt_sha256"] =
        serde_json::to_value(bytes_sha256(b"tampered receipt")).unwrap();
    assert!(WorkspaceSession::from_archive_bytes(&serde_json::to_vec(&archive).unwrap()).is_err());
}
