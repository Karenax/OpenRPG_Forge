//! Exercise the Studio form against the real workspace host and its history.
#![cfg(not(target_arch = "wasm32"))]
#[path = "../src/workspace_controls/create/game_form.rs"]
mod game_form;

use forge_core::{AreaId, AssetId, CommandId, ContentHash, ModuleId, ProjectId};
use forge_project::{
    audit::AuditStamp,
    authoring::{
        creation::{NewTerrainSettings, prepare_module_terrain},
        host::AuthoringHost,
        workspace::WorkspaceHostRequest as HostRequest,
    },
    workspace::{
        ProjectEntry,
        launcher::{GameLocation, PresentationAsset},
        session::{WorkspaceAction, WorkspaceRequest},
    },
};
use game_form::{GameForm, describe_workspace_change};
use std::path::PathBuf;

struct Fixture(PathBuf);
impl Fixture {
    fn new() -> Self {
        let temp = std::env::temp_dir().canonicalize().unwrap();
        let name = format!(
            "forge-studio-game-form-{}-{}",
            std::process::id(),
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        );
        let path = temp.join(name);
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

fn stamp() -> AuditStamp {
    AuditStamp::human("studio-form-test", "2026-11-02T20:00:00Z").unwrap()
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

fn apply(host: &mut AuthoringHost, request: WorkspaceRequest) {
    host.execute_workspace_with_audit(
        HostRequest::Apply {
            request: Box::new(request),
        },
        stamp(),
    )
    .unwrap();
}

fn create_project(
    host: &mut AuthoringHost,
    key: &str,
    name: &str,
) -> (ProjectId, ModuleId, AreaId) {
    let project = ProjectId::deterministic(format!("form/{key}/project"));
    let module = ModuleId::deterministic(format!("form/{key}/module"));
    let action = WorkspaceAction::CreateProject {
        id: project,
        name: name.into(),
    };
    let command = request(host, &format!("{key}/project"), action);
    apply(host, command);
    let action = WorkspaceAction::CreateModule {
        project_id: project,
        id: module,
        name: format!("Module {key}"),
    };
    let command = request(host, &format!("{key}/module"), action);
    apply(host, command);
    host.execute_workspace(HostRequest::Save).unwrap();
    let document = prepare_module_terrain(
        project,
        module,
        &format!("form-{key}-area"),
        &format!("Carte {key}"),
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
        stamp(),
    )
    .unwrap();
    host.execute_workspace(HostRequest::Save).unwrap();
    (project, module, area)
}

#[test]
fn form_preserves_presentation_and_requires_a_selected_entry() {
    let project_id = ProjectId::deterministic("form-project");
    let module = ModuleId::deterministic("form-module");
    let area = AreaId::deterministic("form-area");
    let project = ProjectEntry {
        id: project_id,
        name: "Mon jeu".into(),
        game: None,
        modules: vec![],
    };
    let mut form = GameForm::from_project(&project);
    assert!(!form.is_dirty());
    assert!(form.action().unwrap_err().contains("au moins une carte"));
    form.set_area(module, area, true);
    assert!(form.action().unwrap_err().contains("carte de départ"));
    form.fields.entry = Some(GameLocation {
        module_id: module,
        area_id: area,
    });
    let mut game = form.configuration().unwrap();
    let icon = PresentationAsset {
        asset_id: AssetId::deterministic("form-icon"),
        content_hash: ContentHash::from_bytes([7; 32]),
    };
    game.icon = Some(icon);
    let project = ProjectEntry {
        game: Some(game),
        ..project
    };
    let mut editing = GameForm::from_project(&project);
    editing.fields.title = "Nouveau titre".into();
    assert_eq!(editing.configuration().unwrap().icon, Some(icon));
    editing.set_area(module, area, false);
    assert!(editing.fields.entry.is_none());
    assert!(editing.action().is_err());
}

#[test]
fn start_selection_keeps_module_and_area_together_even_with_a_shared_area_id() {
    let project = ProjectEntry {
        id: ProjectId::deterministic("pair-project"),
        name: "Deux modules".into(),
        game: None,
        modules: vec![],
    };
    let first = ModuleId::deterministic("pair-first");
    let second = ModuleId::deterministic("pair-second");
    let area = AreaId::deterministic("pair-shared-area");
    let mut form = GameForm::from_project(&project);
    form.set_area(first, area, true);
    form.set_area(second, area, true);
    let first_entry = GameLocation {
        module_id: first,
        area_id: area,
    };
    let second_entry = GameLocation {
        module_id: second,
        area_id: area,
    };
    let first_key = GameForm::entry_key(first_entry);
    let second_key = GameForm::entry_key(second_entry);
    assert_ne!(first_key, second_key);
    assert_eq!(form.resolve_entry(&first_key), Some(first_entry));
    assert_eq!(form.resolve_entry(&second_key), Some(second_entry));
    form.set_area(second, area, false);
    assert_eq!(form.resolve_entry(&second_key), None);
    assert_eq!(form.resolve_entry(&first_key), Some(first_entry));
}

fn configure_games(host: &mut AuthoringHost, selections: [(&str, ProjectId, ModuleId, AreaId); 2]) {
    for (key, project, module, area) in selections {
        let current = host
            .execute_workspace(HostRequest::Inspect)
            .unwrap()
            .head
            .unwrap();
        let source = current
            .projects
            .iter()
            .find(|item| item.id == project)
            .unwrap();
        let mut form = GameForm::from_project(source);
        form.fields.title = format!("Jeu {key}");
        form.set_area(module, area, true);
        form.fields.entry = Some(GameLocation {
            module_id: module,
            area_id: area,
        });
        let command = request(host, &format!("{key}/game"), form.action().unwrap());
        let preview = host
            .execute_workspace(HostRequest::Preview {
                request: Box::new(command.clone()),
            })
            .unwrap();
        assert_eq!(preview.head.as_ref(), Some(&current));
        let receipt = preview.receipt.as_ref().unwrap();
        let details = describe_workspace_change(&receipt.before, &receipt.after, &preview.modules);
        assert_eq!(details.len(), 1);
        assert!(details[0].contains(if key == "a" {
            "Premier projet"
        } else {
            "Second projet"
        }));
        assert!(details[0].contains("titre"));
        assert!(details[0].contains("cartes incluses"));
        assert!(details[0].contains("carte de départ"));
        assert_eq!(
            preview
                .receipt
                .unwrap()
                .after
                .projects
                .iter()
                .find(|item| item.id == project)
                .unwrap()
                .game
                .as_ref()
                .unwrap()
                .title,
            format!("Jeu {key}")
        );
        apply(host, command);
        host.execute_workspace(HostRequest::Save).unwrap();
    }
}

fn undo_and_redo_game(
    fixture: &Fixture,
    mut host: AuthoringHost,
    first: ProjectId,
    second: ProjectId,
) {
    // The selected UI project could be the first one: Undo still describes the
    // actual second project from the host's reviewed before/after snapshots.
    let undo = request(&mut host, "undo-game-b", WorkspaceAction::Undo);
    let preview = host
        .execute_workspace(HostRequest::Preview {
            request: Box::new(undo.clone()),
        })
        .unwrap();
    let receipt = preview.receipt.unwrap();
    let labels = describe_workspace_change(&receipt.before, &receipt.after, &preview.modules);
    assert_eq!(labels.len(), 1);
    assert!(labels[0].contains("Second projet"));
    assert!(!labels[0].contains("Premier projet"));
    apply(&mut host, undo);
    host.execute_workspace(HostRequest::Save).unwrap();
    drop(host);
    let mut host = AuthoringHost::new(&fixture.0).unwrap();
    let head = host
        .execute_workspace(HostRequest::Open)
        .unwrap()
        .head
        .unwrap();
    assert!(
        head.projects
            .iter()
            .find(|p| p.id == first)
            .unwrap()
            .game
            .is_some()
    );
    assert!(
        head.projects
            .iter()
            .find(|p| p.id == second)
            .unwrap()
            .game
            .is_none()
    );
    let redo = request(&mut host, "redo-game-b", WorkspaceAction::Redo);
    let reviewed = host
        .execute_workspace(HostRequest::Preview {
            request: Box::new(redo.clone()),
        })
        .unwrap()
        .receipt
        .unwrap();
    assert!(
        describe_workspace_change(&reviewed.before, &reviewed.after, &[])[0]
            .contains("Second projet")
    );
    apply(&mut host, redo);
    host.execute_workspace(HostRequest::Save).unwrap();
    drop(host);
}

#[test]
fn studio_form_saves_reopens_and_reviews_the_actual_workspace_history() {
    let fixture = Fixture::new();
    let mut host = AuthoringHost::new(&fixture.0).unwrap();
    host.execute_workspace(HostRequest::Create).unwrap();
    let (first, module_a, area_a) = create_project(&mut host, "a", "Premier projet");
    let (second, module_b, area_b) = create_project(&mut host, "b", "Second projet");

    configure_games(
        &mut host,
        [
            ("a", first, module_a, area_a),
            ("b", second, module_b, area_b),
        ],
    );
    drop(host);
    let mut host = AuthoringHost::new(&fixture.0).unwrap();
    let opened = host.execute_workspace(HostRequest::Open).unwrap();
    let saved = opened.head.unwrap();
    assert_eq!(
        saved
            .projects
            .iter()
            .find(|p| p.id == first)
            .unwrap()
            .game
            .as_ref()
            .unwrap()
            .title,
        "Jeu a"
    );
    assert_eq!(
        saved
            .projects
            .iter()
            .find(|p| p.id == second)
            .unwrap()
            .game
            .as_ref()
            .unwrap()
            .title,
        "Jeu b"
    );

    undo_and_redo_game(&fixture, host, first, second);
    let mut reopened = AuthoringHost::new(&fixture.0).unwrap();
    let opened = reopened.execute_workspace(HostRequest::Open).unwrap();
    let head = opened.head.unwrap();
    assert_eq!(
        head.projects
            .iter()
            .find(|p| p.id == second)
            .unwrap()
            .game
            .as_ref()
            .unwrap()
            .title,
        "Jeu b"
    );

    // Describing a workspace-wide operation must identify a changed map and
    // its actual project, even if the UI happens to show the other project.
    let mut changed = head.clone();
    let zone = &mut changed
        .projects
        .iter_mut()
        .find(|p| p.id == second)
        .unwrap()
        .modules[0]
        .zones[0];
    zone.head_hash = ContentHash::from_bytes([42; 32]);
    let labels = describe_workspace_change(&head, &changed, &opened.modules);
    assert_eq!(labels.len(), 1);
    assert!(labels[0].contains("Second projet"));
    assert!(labels[0].contains("Module b"));
    assert!(labels[0].contains("Carte b"));

    // A form retains its reviewed before-value and cannot overwrite a newer
    // configuration even when it uses the latest workspace revision.
    let source = head.projects.iter().find(|p| p.id == second).unwrap();
    let mut stale = GameForm::from_project(source);
    stale.fields.title = "Ancienne proposition".into();
    let mut newer = GameForm::from_project(source);
    newer.fields.title = "Modification concurrente".into();
    let command = request(&mut reopened, "newer-game-b", newer.action().unwrap());
    apply(&mut reopened, command);
    let command = request(&mut reopened, "stale-game-b", stale.action().unwrap());
    let failure = reopened
        .execute_workspace(HostRequest::Preview {
            request: Box::new(command),
        })
        .unwrap_err();
    assert!(failure.contains("configuration changed since review"));
}
