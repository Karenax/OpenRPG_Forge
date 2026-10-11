#![cfg(not(target_arch = "wasm32"))]
use forge_core::{AreaId, CommandId, ModuleId, ProjectId};
use forge_project::{
    authoring::{
        AuthoringRequest as A,
        creation::{NewTerrainSettings, prepare_module_terrain},
        host::AuthoringHost,
        workspace::WorkspaceHostRequest as Request,
    },
    workspace::session::{WorkspaceAction, WorkspaceRequest},
    zone_authoring::session::{ZoneAction, ZoneRequest},
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

#[test]
fn browse_absent_workspace_never_creates_an_authority() {
    let root = std::env::temp_dir().join(format!("forge-browse-absent-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let mut host = AuthoringHost::new(&root).unwrap();
    for _ in 0..2 {
        assert!(host.execute_workspace(Request::Browse).is_err());
        assert!(
            host.execute_workspace(Request::Inspect)
                .unwrap()
                .head
                .is_none()
        );
        assert_eq!(std::fs::read_dir(&root).unwrap().count(), 0);
    }
    drop(host);
    std::fs::remove_dir_all(root).unwrap();
}

#[test]
fn browse_opens_existing_modules_and_retains_effective_names_edits_and_playtest() {
    let root = std::env::temp_dir().join(format!("forge-browse-modules-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let areas = prepare_browser_fixture(&root);
    let persisted = disk_inventory(&root);
    let mut host = AuthoringHost::new(&root).unwrap();
    assert!(
        host.execute_workspace(Request::Inspect)
            .unwrap()
            .head
            .is_none()
    );
    let browse = host.execute_workspace(Request::Browse).unwrap();
    assert_eq!(browse.modules.len(), 2);
    assert_eq!(browse.modules[0].name, browse.modules[1].name);
    assert_ne!(browse.modules[0].id, browse.modules[1].id);
    assert_eq!(browse.modules[0].project_name, "North");
    assert_eq!(browse.modules[1].project_name, "South");
    for (module, area) in browse.modules.iter().zip(areas) {
        assert_eq!(module.maps[0].area_id, area);
        assert_eq!(module.maps[0].name, format!("{} map", module.project_name));
        assert!(!module.maps[0].dirty);
    }
    assert!(host.execute(A::Inspect).unwrap().document.is_none());
    assert_eq!(host.execute_workspace(Request::Browse).unwrap(), browse);
    assert_eq!(disk_inventory(&root), persisted);
    let head = host
        .execute(A::OpenManaged { area_id: areas[0] })
        .unwrap()
        .zone_head
        .unwrap();
    let mut corrections = head.corrections.clone();
    corrections.name = Some("Retouched map".into());
    let request = ZoneRequest {
        command_id: CommandId::deterministic("browser-rename"),
        expected_hash: head.content_hash().unwrap(),
        expected_revision: head.revision,
        action: ZoneAction::edit(corrections),
    };
    host.execute(A::PreviewZone {
        request: request.clone(),
    })
    .unwrap();
    let edited = host.execute(A::ApplyZone { request }).unwrap();
    let inventory_after_edit = disk_inventory(&root);
    let browser_after_edit = host.execute_workspace(Request::Browse).unwrap();
    assert_eq!(browser_after_edit.modules[0].maps[0].name, "Retouched map");
    assert!(browser_after_edit.modules[0].maps[0].dirty);
    assert!(!browser_after_edit.modules[1].maps[0].dirty);
    assert_eq!(
        host.execute(A::Inspect).unwrap().zone_head,
        edited.zone_head
    );
    assert_eq!(
        host.execute_workspace(Request::Browse).unwrap(),
        browser_after_edit
    );
    let neighbour = host.execute(A::OpenManaged { area_id: areas[1] }).unwrap();
    assert_eq!(
        host.execute_workspace(Request::Browse).unwrap(),
        browser_after_edit
    );
    assert_eq!(
        host.execute(A::Inspect).unwrap().zone_head,
        neighbour.zone_head
    );
    verify_browser_during_playtest(&mut host, areas[0], &browser_after_edit);
    let returned = host.execute(A::OpenManaged { area_id: areas[0] }).unwrap();
    assert_eq!(returned.zone_head, edited.zone_head);
    assert!(returned.dirty);
    assert!(host.execute(A::Close).is_err());
    assert_eq!(disk_inventory(&root), inventory_after_edit);
    assert_eq!(
        std::fs::read(root.join("workspace-v1/authoring.json")).unwrap(),
        persisted[&std::path::PathBuf::from("workspace-v1/authoring.json")]
    );
    drop(host);
    std::fs::remove_dir_all(root).unwrap();
}

fn verify_browser_during_playtest(
    host: &mut AuthoringHost,
    other_area: AreaId,
    expected: &forge_project::authoring::workspace::WorkspaceHostState,
) {
    let before = host.execute(A::Inspect).unwrap();
    let playing = host
        .execute(A::StartPlaytest {
            expected_hash: before.zone_head.as_ref().unwrap().content_hash().unwrap(),
        })
        .unwrap();
    assert_eq!(&host.execute_workspace(Request::Browse).unwrap(), expected);
    assert_eq!(
        host.execute(A::Inspect).unwrap().playtest_hash,
        playing.playtest_hash
    );
    assert!(
        host.execute(A::OpenManaged {
            area_id: other_area
        })
        .is_err()
    );
    host.execute(A::StopPlaytest { diagnostics: None }).unwrap();
    assert_eq!(
        host.execute(A::Inspect).unwrap().zone_head,
        before.zone_head
    );
}

fn prepare_browser_fixture(root: &std::path::Path) -> [AreaId; 2] {
    let mut host = AuthoringHost::new(root).unwrap();
    host.execute_workspace(Request::Create).unwrap();
    let mut areas = Vec::new();
    for project_name in ["North", "South"] {
        let project = ProjectId::deterministic(project_name);
        let module = ModuleId::deterministic(format!("{project_name}/module"));
        for action in [
            WorkspaceAction::CreateProject {
                id: project,
                name: project_name.into(),
            },
            WorkspaceAction::CreateModule {
                project_id: project,
                id: module,
                name: "Adventure".into(),
            },
        ] {
            let head = host
                .execute_workspace(Request::Inspect)
                .unwrap()
                .head
                .unwrap();
            host.execute_workspace(Request::Apply {
                request: Box::new(WorkspaceRequest {
                    command_id: CommandId::deterministic(format!("{project_name}/{action:?}")),
                    base_hash: head.content_hash().unwrap(),
                    base_revision: head.revision,
                    action,
                }),
            })
            .unwrap();
        }
        let document = Box::new(
            prepare_module_terrain(
                project,
                module,
                "map",
                &format!("{project_name} map"),
                NewTerrainSettings::default(),
            )
            .unwrap(),
        );
        areas.push(document.area_id);
        let review = host
            .execute_workspace(Request::ReviewNewZone {
                module_id: module,
                document: document.clone(),
            })
            .unwrap();
        host.execute_workspace(Request::CreateNewZone {
            request: Box::new(review.receipt.unwrap().request),
            document,
        })
        .unwrap();
    }
    host.execute_workspace(Request::Save).unwrap();
    [areas[0], areas[1]]
}

fn disk_inventory(
    root: &std::path::Path,
) -> std::collections::BTreeMap<std::path::PathBuf, Vec<u8>> {
    fn visit(
        root: &std::path::Path,
        directory: &std::path::Path,
        files: &mut std::collections::BTreeMap<std::path::PathBuf, Vec<u8>>,
    ) {
        for entry in std::fs::read_dir(directory).unwrap() {
            let path = entry.unwrap().path();
            if path.is_dir() {
                visit(root, &path, files);
            } else if path.file_name().is_none_or(|name| name != "authoring.lock") {
                // OS lock files cannot be read while the authoring host owns them.
                // Inventory canonical archives and immutable objects instead.
                files.insert(
                    path.strip_prefix(root).unwrap().to_owned(),
                    std::fs::read(path).unwrap(),
                );
            }
        }
    }
    let mut files = std::collections::BTreeMap::new();
    visit(root, root, &mut files);
    files
}
