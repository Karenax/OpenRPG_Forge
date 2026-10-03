#![cfg(not(target_arch = "wasm32"))]

use forge_core::{AreaId, CommandId, ModuleId, bytes_sha256};
use forge_gameplay::GameplayDocument;
use forge_project::{
    authoring::{
        AuthoringRequest as R, host::AuthoringHost, new_terrain,
        workspace::WorkspaceHostRequest as W,
    },
    workspace::{
        file_session::FileWorkspaceSession,
        session::{WorkspaceAction, WorkspaceRequest},
    },
};
use std::{fs, path::PathBuf};

fn root(label: &str) -> PathBuf {
    let root = std::env::temp_dir().join(format!(
        "forge-gameplay-module-ownership-{label}-{}",
        std::process::id()
    ));
    fs::create_dir(&root).unwrap();
    root
}

fn workspace_change(file: &mut FileWorkspaceSession, key: &str, action: &WorkspaceAction) {
    let (base_revision, base_hash) = {
        let head = file.session().head();
        (head.revision, head.content_hash().unwrap())
    };
    file.execute(&WorkspaceRequest {
        command_id: CommandId::deterministic(key),
        base_revision,
        base_hash,
        action: action.clone(),
    })
    .unwrap();
}

fn managed_setup(label: &str) -> (PathBuf, AuthoringHost, AreaId, ModuleId) {
    let root = root(label);
    let mut host = AuthoringHost::new(&root).unwrap();
    let terrain = new_terrain("gameplay-ownership", "area", "Zone").unwrap();
    let area = terrain.area_id;
    let project = terrain.project_id;
    let module = ModuleId::deterministic("gameplay-ownership/module");
    let mut file = FileWorkspaceSession::create(&root).unwrap();
    workspace_change(
        &mut file,
        "project",
        &WorkspaceAction::CreateProject {
            id: project,
            name: "Gameplay ownership".into(),
        },
    );
    workspace_change(
        &mut file,
        "module",
        &WorkspaceAction::CreateModule {
            project_id: project,
            id: module,
            name: "Gameplay module".into(),
        },
    );
    host.execute(R::CreateZone {
        document: Box::new(terrain),
    })
    .unwrap();
    host.execute(R::Save).unwrap();
    host.execute(R::Close).unwrap();
    let source = root.join(area.to_string()).join("authoring.json");
    let bytes = fs::read(&source).unwrap();
    let review = file
        .review_adoption(area, project, module, bytes_sha256(&bytes))
        .unwrap();
    file.adopt(&review.request).unwrap();
    file.save().unwrap();
    drop(file);
    host.execute_workspace(W::Open).unwrap();
    host.execute(R::OpenManaged { area_id: area }).unwrap();
    (root, host, area, module)
}

fn gameplay(area_id: AreaId, module_id: ModuleId) -> GameplayDocument {
    let player = forge_core::EntityId::deterministic("gameplay-ownership/player");
    serde_json::from_value(serde_json::json!({
        "schema": "forge.gameplay",
        "version": 1,
        "module_id": module_id,
        "area_id": area_id,
        "profile": {
            "id": "forge.gate3-minimal",
            "version": 1,
            "interaction_range_m": 3.0,
            "attack_range_m": 2.0,
            "attack_damage": 20,
            "attack_stamina_cost": 5,
            "max_stamina": 100,
            "max_action_receipts": 512,
            "max_events_per_tick": 128
        },
        "player": player,
        "actors": [{
            "id": player,
            "name": "Player",
            "role": "player",
            "position": {"x": 0.0, "y": 0.0, "z": 0.0},
            "max_vitality": 100,
            "protection": 0,
            "inventory_capacity": 12,
            "dialogue": null,
            "patrol": [],
            "flees_danger": false
        }],
        "items": [],
        "objects": [],
        "places": [],
        "dialogues": [],
        "quests": []
    }))
    .unwrap()
}

fn cleanup(root: PathBuf) {
    fs::remove_dir_all(root).unwrap();
}

#[test]
fn managed_state_uses_workspace_module_and_rejects_mismatch_atomically() {
    let (root, mut host, area, module) = managed_setup("managed-mismatch");
    let before = host.execute(R::Inspect).unwrap();
    assert_eq!(before.gameplay_module_id, Some(module));
    let workspace_before = host.execute_workspace(W::Inspect).unwrap().head.unwrap();
    let head = before.zone_head.as_ref().unwrap();
    let request = head
        .prepare_gameplay(
            Some(gameplay(
                area,
                ModuleId::deterministic("gameplay-ownership/other"),
            )),
            CommandId::deterministic("mismatch"),
        )
        .unwrap();
    let error = host.execute(R::ApplyZone { request }).unwrap_err();
    assert!(error.contains("module"));
    assert_eq!(host.execute(R::Inspect).unwrap(), before);
    assert_eq!(
        host.execute_workspace(W::Inspect).unwrap().head.unwrap(),
        workspace_before
    );
    drop(host);
    cleanup(root);
}

#[test]
fn managed_gameplay_with_workspace_module_is_accepted() {
    let (root, mut host, area, module) = managed_setup("managed-accepted");
    let head = host.execute(R::Inspect).unwrap().zone_head.unwrap();
    let request = head
        .prepare_gameplay(
            Some(gameplay(area, module)),
            CommandId::deterministic("accepted"),
        )
        .unwrap();
    host.execute(R::PreviewZone {
        request: request.clone(),
    })
    .unwrap();
    let applied = host.execute(R::ApplyZone { request }).unwrap();
    let gameplay = applied.zone_head.unwrap().corrections.gameplay.unwrap();
    assert_eq!(gameplay.module_id, module);
    assert_eq!(gameplay.area_id, area);
    assert_eq!(applied.gameplay_module_id, Some(module));
    drop(host);
    cleanup(root);
}

#[test]
fn standalone_gameplay_module_id_is_stable_across_reopen() {
    let root = root("standalone-stable");
    let mut host = AuthoringHost::new(&root).unwrap();
    let terrain = new_terrain("gameplay-ownership", "standalone", "Standalone").unwrap();
    let area = terrain.area_id;
    host.execute(R::CreateZone {
        document: Box::new(terrain),
    })
    .unwrap();
    host.execute(R::Save).unwrap();
    let first = host.execute(R::Inspect).unwrap().gameplay_module_id;
    assert!(first.is_some());
    host.execute(R::Close).unwrap();
    let reopened = host.execute(R::Open { area_id: area }).unwrap();
    assert_eq!(reopened.gameplay_module_id, first);
    drop(host);
    cleanup(root);
}
