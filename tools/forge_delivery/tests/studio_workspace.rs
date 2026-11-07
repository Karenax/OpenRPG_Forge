#![cfg(all(target_os = "windows", target_arch = "x86_64"))]

use forge_delivery as delivery;
use forge_delivery::{install, launch};
use std::fs;

#[path = "support/studio_workspace_fixture.rs"]
mod fixture;

#[test]
fn install_update_and_rollback_keep_the_same_user_workspace_and_projects() {
    let fixture = fixture::Fixture::new();
    let first = fixture.pack("1.0.0");
    let initial = fixture.first_plan(&first);
    let initial_state = install::apply(&initial).unwrap();
    assert_eq!(initial_state.revision, 1);
    let workspace = fixture.workspace();
    assert_eq!(fs::read_dir(&workspace).unwrap().count(), 0);
    forge_project::authoring::host::AuthoringHost::new(&workspace).unwrap();
    fs::create_dir(workspace.join("workspace-v1")).unwrap();
    let authored = workspace.join("workspace-v1/authoring.json");
    fs::write(&authored, b"unique authored workspace").unwrap();
    let sibling = fixture.user().join("workspaces/other-project");
    fs::create_dir(&sibling).unwrap();
    fs::write(sibling.join("user-data"), b"other project").unwrap();

    let second = fixture.pack("2.0.0");
    let updated =
        install::apply(&install::prepare(&fixture.install(), &[second]).unwrap()).unwrap();
    assert_eq!(updated.user_root, initial_state.user_root);
    assert_eq!(fs::read(&authored).unwrap(), b"unique authored workspace");
    assert_eq!(
        fs::read(sibling.join("user-data")).unwrap(),
        b"other project"
    );
    let rollback = install::prepare_rollback(&fixture.install()).unwrap();
    let restored = install::apply(&rollback).unwrap();
    assert_eq!(restored.user_root, initial_state.user_root);
    assert_eq!(
        restored.active.core.revision,
        initial_state.active.core.revision
    );
    assert_eq!(fs::read(&authored).unwrap(), b"unique authored workspace");
    assert_eq!(
        fs::read(sibling.join("user-data")).unwrap(),
        b"other project"
    );
    assert!(launch::command(&fixture.install(), "editor").is_err());
}

#[test]
fn first_install_does_not_replace_a_preexisting_workspace() {
    let fixture = fixture::Fixture::new();
    let workspace = fixture.workspace();
    fs::create_dir_all(workspace.join("workspace-v1")).unwrap();
    let existing = workspace.join("workspace-v1/authoring.json");
    fs::write(&existing, b"workspace already present").unwrap();
    let package = fixture.pack("1.0.0");
    install::apply(&fixture.first_plan(&package)).unwrap();
    assert_eq!(fs::read(existing).unwrap(), b"workspace already present");
}
