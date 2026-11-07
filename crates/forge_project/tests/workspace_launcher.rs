use forge_core::{AreaId, AssetId, CommandId, ContentHash, ModuleId, ProjectId};
use forge_project::{
    VisualQualityTier,
    workspace::{
        ModuleCreatorReference, WorkspaceHead, ZoneReference,
        launcher::{
            DisplayMode, GAME_CONFIGURATION_VERSION, GameConfiguration, GameContent, GameLocation,
            PlayerDefaults, PresentationAsset,
        },
        session::{WorkspaceAction, WorkspaceRequest, WorkspaceSession},
    },
};

fn request(session: &WorkspaceSession, key: &str, action: WorkspaceAction) -> WorkspaceRequest {
    WorkspaceRequest {
        command_id: CommandId::deterministic(key),
        base_revision: session.head().revision,
        base_hash: session.head().content_hash().unwrap(),
        action,
    }
}

fn add_project(session: &mut WorkspaceSession, key: &str) -> (ProjectId, ModuleId, AreaId) {
    let project = ProjectId::deterministic(format!("project/{key}"));
    let module = ModuleId::deterministic(format!("module/{key}"));
    let area = AreaId::deterministic(format!("area/{key}"));
    let create = request(
        session,
        &format!("{key}/project"),
        WorkspaceAction::CreateProject {
            id: project,
            name: format!("Project {key}"),
        },
    );
    session.execute(&create).unwrap();
    let create = request(
        session,
        &format!("{key}/module"),
        WorkspaceAction::CreateModule {
            project_id: project,
            id: module,
            name: format!("Module {key}"),
        },
    );
    session.execute(&create).unwrap();
    let attach = request(
        session,
        &format!("{key}/area"),
        WorkspaceAction::AttachZone {
            project_id: project,
            module_id: module,
            reference: ZoneReference {
                area_id: area,
                archive_hash: ContentHash::from_bytes([1; 32]),
                head_hash: ContentHash::from_bytes([2; 32]),
            },
        },
    );
    session.execute(&attach).unwrap();
    (project, module, area)
}

fn game(module: ModuleId, area: AreaId, title: &str) -> GameConfiguration {
    GameConfiguration {
        version: GAME_CONFIGURATION_VERSION,
        title: title.into(),
        summary: "Une aventure créée dans ce projet.".into(),
        icon: Some(PresentationAsset {
            asset_id: AssetId::deterministic(format!("icon/{title}")),
            content_hash: ContentHash::from_bytes([3; 32]),
        }),
        logo: None,
        background: None,
        contents: vec![GameContent {
            module_id: module,
            areas: vec![area],
        }],
        entry: GameLocation {
            module_id: module,
            area_id: area,
        },
        defaults: PlayerDefaults {
            quality: VisualQualityTier::High,
            display_mode: DisplayMode::Borderless,
            master_volume: 80,
            music_volume: 60,
            reduce_motion: false,
            subtitles: true,
        },
    }
}

#[test]
fn legacy_versions_are_unchanged_and_game_upgrade_is_reversible() {
    let mut session = WorkspaceSession::new(WorkspaceHead::default()).unwrap();
    let (project, module, area) = add_project(&mut session, "legacy");
    let legacy = session.head().clone();
    let bytes = legacy.to_bytes().unwrap();
    assert_eq!(legacy.schema_version, 1);
    assert!(
        !String::from_utf8(bytes.clone())
            .unwrap()
            .contains("\"game\"")
    );
    assert_eq!(
        WorkspaceHead::from_bytes(&bytes)
            .unwrap()
            .to_bytes()
            .unwrap(),
        bytes
    );

    let zones = &legacy.projects[0].modules[0].zones;
    let creator = ModuleCreatorReference {
        sha256: ContentHash::from_bytes([4; 32]),
        bytes: 1,
    };
    let version_two = legacy
        .with_module_creator(project, module, None, creator, zones, zones)
        .unwrap();
    assert_eq!(version_two.schema_version, 2);
    let bytes = version_two.to_bytes().unwrap();
    assert_eq!(
        WorkspaceHead::from_bytes(&bytes)
            .unwrap()
            .to_bytes()
            .unwrap(),
        bytes
    );
    let configuration = game(module, area, "Une autre aventure");
    let version_three = version_two
        .with_game_configuration(project, None, Some(configuration.clone()))
        .unwrap();
    assert_eq!(version_three.schema_version, 3);
    let removed = version_three
        .with_game_configuration(project, Some(&configuration), None)
        .unwrap();
    assert_eq!(removed, version_two);
    assert!(legacy.derive_game_manifest(project).is_err());
}

#[test]
fn configuration_is_validated_against_its_project() {
    let mut session = WorkspaceSession::new(WorkspaceHead::default()).unwrap();
    let (project, module, area) = add_project(&mut session, "bound");
    let good = game(module, area, "Bound game");
    assert!(
        session
            .head()
            .with_game_configuration(project, None, Some(good.clone()))
            .is_ok()
    );
    let mut cases = Vec::new();
    let mut invalid = good.clone();
    invalid.version += 1;
    cases.push(invalid);
    let mut invalid = good.clone();
    invalid.title = " ".into();
    cases.push(invalid);
    let mut invalid = good.clone();
    invalid.summary = "bad\u{0}".into();
    cases.push(invalid);
    let mut invalid = good.clone();
    invalid.defaults.master_volume = 101;
    cases.push(invalid);
    let mut invalid = good.clone();
    invalid.contents[0].module_id = ModuleId::deterministic("foreign");
    cases.push(invalid);
    let mut invalid = good.clone();
    invalid.contents[0].areas.push(area);
    cases.push(invalid);
    let mut invalid = good.clone();
    invalid.contents.push(invalid.contents[0].clone());
    cases.push(invalid);
    let mut invalid = good.clone();
    invalid.contents[0].areas.clear();
    cases.push(invalid);
    let mut invalid = good.clone();
    invalid.entry.area_id = AreaId::deterministic("not-selected");
    cases.push(invalid);
    for invalid in cases {
        assert!(
            session
                .head()
                .with_game_configuration(project, None, Some(invalid))
                .is_err()
        );
    }
    let mut wrong_version = session.head().clone();
    wrong_version.schema_version = 3;
    assert!(wrong_version.validate().is_err());
    let nil_project: ProjectId = "00000000-0000-0000-0000-000000000000".parse().unwrap();
    let mut nil_head = WorkspaceHead::default()
        .with_project(nil_project, "Nil".into())
        .unwrap()
        .with_module(nil_project, module, "Module".into())
        .unwrap();
    nil_head.projects[0].modules[0].zones.push(ZoneReference {
        area_id: area,
        archive_hash: ContentHash::from_bytes([1; 32]),
        head_hash: ContentHash::from_bytes([2; 32]),
    });
    assert!(
        nil_head
            .with_game_configuration(nil_project, None, Some(good))
            .is_err()
    );
}

#[test]
fn distributable_refuses_partial_modules_until_the_exporter_can_close_them() {
    let mut session = WorkspaceSession::new(WorkspaceHead::default()).unwrap();
    let (project, module, area) = add_project(&mut session, "partial");
    let another_area = AreaId::deterministic("partial/another-area");
    let mut head = session.head().clone();
    head.projects[0].modules[0].zones.push(ZoneReference {
        area_id: another_area,
        archive_hash: ContentHash::from_bytes([8; 32]),
        head_hash: ContentHash::from_bytes([9; 32]),
    });
    head.validate().unwrap();
    let configuration = game(module, area, "Jeu partiel");
    let head = head
        .with_game_configuration(project, None, Some(configuration.clone()))
        .unwrap();
    assert!(
        head.derive_game_manifest(project)
            .unwrap_err()
            .to_string()
            .contains("partial module export")
    );
    let mut complete = configuration.clone();
    complete.contents[0].areas.push(another_area);
    let head = head
        .with_game_configuration(project, Some(&configuration), Some(complete))
        .unwrap();
    assert!(head.derive_game_manifest(project).is_ok());
}

#[test]
fn reviewed_edit_replay_undo_and_redo_preserve_game_intent() {
    let mut session = WorkspaceSession::new(WorkspaceHead::default()).unwrap();
    let (project, module, area) = add_project(&mut session, "history");
    let configuration = game(module, area, "First title");
    let command = request(
        &session,
        "configure",
        WorkspaceAction::ConfigureGame {
            project_id: project,
            before: None,
            after: Some(Box::new(configuration.clone())),
        },
    );
    let preview = session.preview(&command).unwrap();
    assert_eq!(session.head().schema_version, 1);
    assert_eq!(session.execute(&command).unwrap(), preview);
    assert_eq!(session.execute(&command).unwrap(), preview);
    assert_eq!(session.head().schema_version, 3);
    let stale = request(
        &session,
        "stale-game",
        WorkspaceAction::ConfigureGame {
            project_id: project,
            before: None,
            after: Some(Box::new(game(module, area, "Stale"))),
        },
    );
    assert!(session.execute(&stale).is_err());
    let archive = session.archive_bytes().unwrap();
    let mut reopened = WorkspaceSession::from_archive_bytes(&archive).unwrap();
    assert_eq!(reopened.head(), session.head());
    let undo = request(&reopened, "undo-game", WorkspaceAction::Undo);
    reopened.execute(&undo).unwrap();
    assert_eq!(reopened.head().schema_version, 1);
    let redo = request(&reopened, "redo-game", WorkspaceAction::Redo);
    reopened.execute(&redo).unwrap();
    assert_eq!(
        reopened.head().projects[0].game.as_ref(),
        Some(&configuration)
    );
    let mut tampered: serde_json::Value = serde_json::from_slice(&archive).unwrap();
    tampered["head"]["projects"][0]["game"]["title"] = "Altered".into();
    assert!(WorkspaceSession::from_archive_bytes(&serde_json::to_vec(&tampered).unwrap()).is_err());
}

#[test]
fn two_games_derive_distinct_stable_namespaces_without_reference_content() {
    let mut session = WorkspaceSession::new(WorkspaceHead::default()).unwrap();
    let (a, a_module, a_area) = add_project(&mut session, "first");
    let (b, b_module, b_area) = add_project(&mut session, "second");
    let first = game(a_module, a_area, "Jeu un");
    let second = game(b_module, b_area, "Jeu deux");
    for (key, id, config) in [("first-game", a, first.clone()), ("second-game", b, second)] {
        let command = request(
            &session,
            key,
            WorkspaceAction::ConfigureGame {
                project_id: id,
                before: None,
                after: Some(Box::new(config)),
            },
        );
        session.execute(&command).unwrap();
    }
    let one = session.head().derive_game_manifest(a).unwrap();
    let two = session.head().derive_game_manifest(b).unwrap();
    assert_eq!(one.game_id(), a);
    assert_eq!(two.game_id(), b);
    assert_ne!(one.save_namespace(), two.save_namespace());
    assert!(!one.save_namespace().contains(".."));
    let serialized = serde_json::to_string(&one).unwrap();
    assert!(!serialized.contains("Tidewater"));
    assert!(!serialized.contains("Aube"));
    assert_eq!(one.configuration().contents[0].module_id, a_module);
    let mut renamed = first.clone();
    renamed.title = "Nouveau titre".into();
    let renamed_head = session
        .head()
        .with_game_configuration(a, Some(&first), Some(renamed))
        .unwrap();
    let renamed_manifest = renamed_head.derive_game_manifest(a).unwrap();
    assert_eq!(renamed_manifest.save_namespace(), one.save_namespace());
    assert_ne!(
        serde_json::to_string(&renamed_manifest).unwrap(),
        serialized
    );
}

#[cfg(not(target_arch = "wasm32"))]
#[test]
#[allow(
    clippy::too_many_lines,
    reason = "the host transaction covers preview, save, reopen and persisted undo"
)]
fn authoring_host_saves_and_reopens_game_configuration_with_history() {
    use forge_project::{
        audit::AuditStamp,
        authoring::{
            creation::{NewTerrainSettings, prepare_module_terrain},
            host::AuthoringHost,
            workspace::WorkspaceHostRequest as HostRequest,
        },
    };

    let temporary = std::env::temp_dir().canonicalize().unwrap();
    let root = temporary.join(format!("forge-game-workspace-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let root = root.canonicalize().unwrap();
    assert_eq!(root.parent(), Some(temporary.as_path()));
    let mut host = AuthoringHost::new(&root).unwrap();
    host.execute_workspace(HostRequest::Create).unwrap();
    let project = ProjectId::deterministic("game-workspace-project");
    let module = ModuleId::deterministic("game-workspace-module");
    for (key, action) in [
        (
            "file-project",
            WorkspaceAction::CreateProject {
                id: project,
                name: "Projet sauvegardé".into(),
            },
        ),
        (
            "file-module",
            WorkspaceAction::CreateModule {
                project_id: project,
                id: module,
                name: "Module sauvegardé".into(),
            },
        ),
    ] {
        let head = host
            .execute_workspace(HostRequest::Inspect)
            .unwrap()
            .head
            .unwrap();
        host.execute_workspace(HostRequest::Apply {
            request: Box::new(WorkspaceRequest {
                command_id: CommandId::deterministic(key),
                base_revision: head.revision,
                base_hash: head.content_hash().unwrap(),
                action,
            }),
        })
        .unwrap();
    }
    host.execute_workspace(HostRequest::Save).unwrap();
    let document = prepare_module_terrain(
        project,
        module,
        "game-area",
        "Carte sauvegardée",
        NewTerrainSettings::default(),
    )
    .unwrap();
    let area = document.area_id;
    let review = host
        .execute_workspace(HostRequest::ReviewNewZone {
            module_id: module,
            document: Box::new(document.clone()),
        })
        .unwrap()
        .receipt
        .unwrap();
    host.execute_workspace_with_audit(
        HostRequest::CreateNewZone {
            request: Box::new(review.request),
            document: Box::new(document),
        },
        AuditStamp::human("contract-test", "2026-11-02T20:00:00Z").unwrap(),
    )
    .unwrap();
    let head = host
        .execute_workspace(HostRequest::Inspect)
        .unwrap()
        .head
        .unwrap();
    let configuration = game(module, area, "Jeu sauvegardé");
    let request = WorkspaceRequest {
        command_id: CommandId::deterministic("file-configure-game"),
        base_revision: head.revision,
        base_hash: head.content_hash().unwrap(),
        action: WorkspaceAction::ConfigureGame {
            project_id: project,
            before: None,
            after: Some(Box::new(configuration.clone())),
        },
    };
    let preview = host
        .execute_workspace(HostRequest::Preview {
            request: Box::new(request.clone()),
        })
        .unwrap();
    assert_eq!(preview.head.as_ref(), Some(&head));
    let applied = host
        .execute_workspace(HostRequest::Apply {
            request: Box::new(request),
        })
        .unwrap();
    assert_eq!(applied.receipt, preview.receipt);
    host.execute_workspace(HostRequest::Save).unwrap();
    drop(host);

    let mut reopened = AuthoringHost::new(&root).unwrap();
    let head = reopened
        .execute_workspace(HostRequest::Open)
        .unwrap()
        .head
        .unwrap();
    assert_eq!(head.schema_version, 3);
    assert_eq!(head.projects[0].game.as_ref(), Some(&configuration));
    assert_eq!(
        head.derive_game_manifest(project).unwrap().save_namespace(),
        format!("games/{project}/saves")
    );
    reopened
        .execute_workspace(HostRequest::Apply {
            request: Box::new(WorkspaceRequest {
                command_id: CommandId::deterministic("file-undo-game"),
                base_revision: head.revision,
                base_hash: head.content_hash().unwrap(),
                action: WorkspaceAction::Undo,
            }),
        })
        .unwrap();
    reopened.execute_workspace(HostRequest::Save).unwrap();
    drop(reopened);
    let mut reopened = AuthoringHost::new(&root).unwrap();
    let head = reopened
        .execute_workspace(HostRequest::Open)
        .unwrap()
        .head
        .unwrap();
    assert_eq!(head.schema_version, 1);
    assert!(head.projects[0].game.is_none());
    drop(reopened);
    std::fs::remove_dir_all(&root).unwrap();
}
