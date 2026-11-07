//! Saved project intent and existing module snapshots for standalone export.
#![cfg(not(target_arch = "wasm32"))]
use forge_core::{AreaId, CommandId, ContentHash, ModuleId, ProjectId};
use forge_project::{
    VisualQualityTier,
    audit::AuditStamp,
    authoring::{
        AuthoringRequest,
        creation::{NewTerrainSettings, prepare_module_terrain},
        host::AuthoringHost,
        workspace::WorkspaceHostRequest as HostRequest,
    },
    workspace::{
        launcher::{
            DisplayMode, GAME_CONFIGURATION_VERSION, GameConfiguration, GameContent, GameLocation,
            PlayerDefaults,
        },
        session::{WorkspaceAction, WorkspaceRequest},
    },
};
use std::path::PathBuf;

struct Fixture(PathBuf);

impl Fixture {
    fn new() -> Self {
        let root = std::env::temp_dir().canonicalize().unwrap();
        let path = root.join(format!(
            "forge-game-export-{}-{}",
            std::process::id(),
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        ));
        std::fs::create_dir(&path).unwrap();
        Self(path.canonicalize().unwrap())
    }
}

impl Drop for Fixture {
    fn drop(&mut self) {
        assert_eq!(
            self.0.parent(),
            Some(std::env::temp_dir().canonicalize().unwrap().as_path())
        );
        std::fs::remove_dir_all(&self.0).unwrap();
    }
}

fn request(host: &mut AuthoringHost, key: &str, action: WorkspaceAction) -> WorkspaceRequest {
    let head = host
        .execute_workspace(HostRequest::Inspect)
        .unwrap()
        .head
        .unwrap();
    WorkspaceRequest {
        command_id: CommandId::deterministic(key),
        base_revision: head.revision,
        base_hash: head.content_hash().unwrap(),
        action,
    }
}

fn apply(host: &mut AuthoringHost, key: &str, action: WorkspaceAction) {
    let command = request(host, key, action);
    host.execute_workspace_with_audit(
        HostRequest::Apply {
            request: Box::new(command),
        },
        AuditStamp::human("game-export-test", "2026-11-02T20:00:00Z").unwrap(),
    )
    .unwrap();
}

fn create_area(
    host: &mut AuthoringHost,
    project: ProjectId,
    module: ModuleId,
    name: &str,
) -> AreaId {
    let document = prepare_module_terrain(
        project,
        module,
        &format!("game-export-{name}"),
        name,
        NewTerrainSettings::default(),
    )
    .unwrap();
    let area = document.area_id;
    let reviewed = host
        .execute_workspace(HostRequest::ReviewNewZone {
            module_id: module,
            document: Box::new(document.clone()),
        })
        .unwrap()
        .receipt
        .unwrap();
    host.execute_workspace_with_audit(
        HostRequest::CreateNewZone {
            request: Box::new(reviewed.request),
            document: Box::new(document),
        },
        AuditStamp::human("game-export-test", "2026-11-02T20:00:00Z").unwrap(),
    )
    .unwrap();
    host.execute_workspace(HostRequest::Save).unwrap();
    area
}

#[test]
#[allow(
    clippy::too_many_lines,
    reason = "one transaction checks saved identity, tampering, pending recovery and partial selection"
)]
fn saved_game_plan_pins_exact_selected_modules_and_refuses_stale_or_partial_sources() {
    let fixture = Fixture::new();
    let mut host = AuthoringHost::new(&fixture.0).unwrap();
    host.execute_workspace(HostRequest::Create).unwrap();
    let project = ProjectId::deterministic("game-export-project");
    let other = ProjectId::deterministic("game-export-other-project");
    let first = ModuleId::deterministic("game-export-first-module");
    let second = ModuleId::deterministic("game-export-second-module");
    let foreign = ModuleId::deterministic("game-export-foreign-module");
    apply(
        &mut host,
        "project",
        WorkspaceAction::CreateProject {
            id: project,
            name: "Jeu indépendant".into(),
        },
    );
    apply(
        &mut host,
        "other-project",
        WorkspaceAction::CreateProject {
            id: other,
            name: "Autre jeu".into(),
        },
    );
    for (key, owner, module) in [
        ("first", project, first),
        ("second", project, second),
        ("foreign", other, foreign),
    ] {
        apply(
            &mut host,
            key,
            WorkspaceAction::CreateModule {
                project_id: owner,
                id: module,
                name: format!("Module {key}"),
            },
        );
    }
    host.execute_workspace(HostRequest::Save).unwrap();
    let area_a = create_area(&mut host, project, first, "first-map");
    let area_b = create_area(&mut host, project, second, "second-map");
    let _ = create_area(&mut host, other, foreign, "foreign-map");
    let game = GameConfiguration {
        version: GAME_CONFIGURATION_VERSION,
        title: "Jeu indépendant".into(),
        summary: "Aventure".into(),
        icon: None,
        logo: None,
        background: None,
        contents: vec![
            GameContent {
                module_id: first,
                areas: vec![area_a],
            },
            GameContent {
                module_id: second,
                areas: vec![area_b],
            },
        ],
        entry: GameLocation {
            module_id: second,
            area_id: area_b,
        },
        defaults: PlayerDefaults {
            quality: VisualQualityTier::High,
            display_mode: DisplayMode::Borderless,
            master_volume: 80,
            music_volume: 60,
            reduce_motion: false,
            subtitles: true,
        },
    };
    apply(
        &mut host,
        "game",
        WorkspaceAction::ConfigureGame {
            project_id: project,
            before: None,
            after: Some(Box::new(game)),
        },
    );
    let dirty = host
        .execute_workspace(HostRequest::Inspect)
        .unwrap()
        .head
        .unwrap();
    let expected = dirty.content_hash().unwrap();
    assert!(
        host.game_export_plan(project, expected)
            .err()
            .unwrap()
            .contains("Save the workspace")
    );
    host.execute_workspace(HostRequest::Save).unwrap();
    let plan = host.game_export_plan(project, expected).unwrap();
    assert_eq!(plan.workspace_hash, expected);
    assert_eq!(plan.game.game_id(), project);
    assert_eq!(
        plan.modules
            .iter()
            .map(|module| module.module_id)
            .collect::<Vec<_>>(),
        vec![first, second]
    );
    assert_eq!(plan.modules[1].primary_area, area_b);
    assert!(plan.modules.iter().all(|module| module.origin.is_none()));
    assert!(
        host.game_export_plan(project, ContentHash::from_bytes([42; 32]))
            .is_err()
    );
    let authority = fixture.0.join("workspace-v1");
    let saved_file = authority.join("authoring.json");
    let original = std::fs::read(&saved_file).unwrap();
    std::fs::write(&saved_file, b"externally altered").unwrap();
    assert!(
        host.game_export_plan(project, expected)
            .err()
            .unwrap()
            .contains("changed outside")
    );
    std::fs::write(&saved_file, &original).unwrap();
    let pending = authority.join("authoring.pending");
    std::fs::write(&pending, b"interrupted fixture").unwrap();
    assert!(
        host.game_export_plan(project, expected)
            .err()
            .unwrap()
            .contains("interrupted")
    );
    assert_eq!(std::fs::read(&pending).unwrap(), b"interrupted fixture");
    assert_eq!(std::fs::read(&saved_file).unwrap(), original);
    std::fs::remove_file(pending).unwrap();
    assert!(host.game_export_plan(project, expected).is_ok());
    assert!(host.game_export_module(project, foreign, expected).is_err());
    let module = host.game_export_module(project, first, expected).unwrap();
    assert_eq!(module.project_id, project);
    assert_eq!(module.module_id, first);
    assert_eq!(module.zones.len(), 1);
    assert_eq!(module.zones[0].area_id, area_a);
    assert_eq!(module.workspace_hash, Some(expected));

    host.execute(AuthoringRequest::OpenManaged { area_id: area_a })
        .unwrap();
    assert!(host.game_export_plan(project, expected).is_err());
    host.execute(AuthoringRequest::Close).unwrap();
    assert!(host.game_export_plan(project, expected).is_ok());

    // Adding a second map leaves a valid authoring configuration, but the
    // current exporter refuses to leak an unselected map into the package.
    let _ = create_area(&mut host, project, first, "first-extra-map");
    let current = host
        .execute_workspace(HostRequest::Inspect)
        .unwrap()
        .head
        .unwrap()
        .content_hash()
        .unwrap();
    assert!(
        host.game_export_plan(project, current)
            .err()
            .unwrap()
            .contains("partial module export")
    );
}
