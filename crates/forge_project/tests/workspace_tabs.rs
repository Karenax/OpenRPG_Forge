#![cfg(not(target_arch = "wasm32"))]
use forge_core::{AreaId, CommandId, ModuleId};
use forge_project::{
    authoring::{
        AuthoringRequest as R, host::AuthoringHost, new_terrain,
        workspace::WorkspaceHostRequest as W,
    },
    terrain_edit::{TerrainAction, TerrainEdit},
    workspace::{
        file_session::FileWorkspaceSession,
        session::{WorkspaceAction, WorkspaceRequest},
    },
    zone_authoring::session::{ZoneAction, ZoneRequest},
};

fn apply_elevation(
    host: &mut AuthoringHost,
    metres: f32,
) -> forge_project::authoring::AuthoringState {
    let head = host.execute(R::Inspect).unwrap().zone_head.unwrap();
    let request = head
        .prepare_terrain_action(
            TerrainAction::Edit(TerrainEdit::SetBaseElevation { metres }),
            CommandId::deterministic(format!("elevation:{}:{metres}", head.base.terrain.area_id)),
        )
        .unwrap();
    host.execute(R::PreviewZone {
        request: request.clone(),
    })
    .unwrap();
    host.execute(R::ApplyZone { request }).unwrap()
}

#[test]
fn independent_geometry_and_undo_survive_switch_save_and_host_recreation() {
    let root = std::env::temp_dir().join(format!("forge-tabs-geometry-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let (mut host, [a, b]) = setup(&root);
    let disk_before = std::fs::read(root.join("workspace-v1/authoring.json")).unwrap();
    let original_a = host
        .execute(R::OpenManaged { area_id: a })
        .unwrap()
        .zone_head
        .unwrap();
    let edited_a = apply_elevation(&mut host, 3.0).zone_head.unwrap();
    host.execute(R::OpenManaged { area_id: b }).unwrap();
    let edited_b = apply_elevation(&mut host, -4.0).zone_head.unwrap();
    assert_ne!(
        edited_a.effective().unwrap().0.content_hash().unwrap(),
        original_a.effective().unwrap().0.content_hash().unwrap()
    );
    assert_eq!(
        std::fs::read(root.join("workspace-v1/authoring.json")).unwrap(),
        disk_before
    );
    let returned = host.execute(R::OpenManaged { area_id: a }).unwrap();
    assert_eq!(returned.zone_head.unwrap(), edited_a);
    assert!(returned.managed_tabs.iter().all(|tab| tab.dirty));
    let head = host.execute(R::Inspect).unwrap().zone_head.unwrap();
    let undo = ZoneRequest {
        command_id: CommandId::deterministic("geometry-undo"),
        expected_hash: head.content_hash().unwrap(),
        expected_revision: head.revision,
        action: ZoneAction::Undo,
    };
    let undone = host
        .execute(R::ApplyZone { request: undo })
        .unwrap()
        .zone_head
        .unwrap();
    assert_eq!(undone.corrections, original_a.corrections);
    // B is not affected by A's undo, and its source must not be used to redo A.
    assert_eq!(
        host.execute(R::OpenManaged { area_id: b })
            .unwrap()
            .zone_head
            .unwrap(),
        edited_b
    );
    let head = host
        .execute(R::OpenManaged { area_id: a })
        .unwrap()
        .zone_head
        .unwrap();
    let redo = ZoneRequest {
        command_id: CommandId::deterministic("geometry-redo"),
        expected_hash: head.content_hash().unwrap(),
        expected_revision: head.revision,
        action: ZoneAction::Redo,
    };
    let redone_a = host
        .execute(R::ApplyZone { request: redo })
        .unwrap()
        .zone_head
        .unwrap();
    assert_eq!(redone_a.corrections, edited_a.corrections);
    host.execute(R::OpenManaged { area_id: b }).unwrap();
    let saved = host.execute(R::Save).unwrap();
    assert!(saved.managed_tabs.iter().all(|tab| !tab.dirty));
    drop(host);
    let mut reopened = AuthoringHost::new(&root).unwrap();
    reopened.execute_workspace(W::Open).unwrap();
    for (area, expected) in [(a, redone_a), (b, edited_b)] {
        let actual = reopened.execute(R::OpenManaged { area_id: area }).unwrap();
        assert!(!actual.dirty);
        assert_eq!(actual.zone_head.unwrap(), expected);
    }
    drop(reopened);
    std::fs::remove_dir_all(root).unwrap();
}

#[test]
fn saved_undo_preserves_redo_after_reopening_managed_zone() {
    let root = std::env::temp_dir().join(format!("forge-tabs-saved-undo-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let (mut host, [area, neighbour]) = setup(&root);
    let initial = host
        .execute(R::OpenManaged { area_id: area })
        .unwrap()
        .zone_head
        .unwrap();
    let edited = apply_elevation(&mut host, 8.0).zone_head.unwrap();
    let undo = ZoneRequest {
        command_id: CommandId::deterministic("saved-undo"),
        expected_hash: edited.content_hash().unwrap(),
        expected_revision: edited.revision,
        action: ZoneAction::Undo,
    };
    let undone = host.execute(R::ApplyZone { request: undo }).unwrap();
    assert!(undone.can_redo);
    assert_eq!(
        undone.zone_head.as_ref().unwrap().corrections,
        initial.corrections
    );
    host.execute(R::Save).unwrap();
    drop(host);
    let disk = std::fs::read(root.join("workspace-v1/authoring.json")).unwrap();
    let mut host = AuthoringHost::new(&root).unwrap();
    host.execute_workspace(W::Open).unwrap();
    let reopened = host.execute(R::OpenManaged { area_id: area }).unwrap();
    assert_eq!(reopened.zone_head, undone.zone_head);
    assert!(reopened.can_redo);
    assert!(!reopened.dirty);
    let head = reopened.zone_head.unwrap();
    let redo = ZoneRequest {
        command_id: CommandId::deterministic("saved-undo-redo"),
        expected_hash: head.content_hash().unwrap(),
        expected_revision: head.revision,
        action: ZoneAction::Redo,
    };
    let redone = host
        .execute(R::ApplyZone {
            request: redo.clone(),
        })
        .unwrap();
    assert_eq!(
        redone.zone_head.as_ref().unwrap().corrections,
        edited.corrections
    );
    assert!(redone.dirty);
    assert!(!redone.can_redo);
    assert_eq!(
        host.execute(R::ApplyZone { request: redo })
            .unwrap()
            .zone_head,
        redone.zone_head
    );
    assert_eq!(
        std::fs::read(root.join("workspace-v1/authoring.json")).unwrap(),
        disk
    );
    host.execute(R::OpenManaged { area_id: neighbour }).unwrap();
    host.execute(R::Save).unwrap();
    drop(host);
    let mut host = AuthoringHost::new(&root).unwrap();
    host.execute_workspace(W::Open).unwrap();
    let final_state = host.execute(R::OpenManaged { area_id: area }).unwrap();
    assert_eq!(final_state.zone_head, redone.zone_head);
    assert!(!final_state.dirty);
    drop(host);
    std::fs::remove_dir_all(root).unwrap();
}

fn setup(root: &std::path::Path) -> (AuthoringHost, [AreaId; 2]) {
    let mut host = AuthoringHost::new(root).unwrap();
    let mut file = FileWorkspaceSession::create(root).unwrap();
    let documents = [
        new_terrain("tabs", "a", "A").unwrap(),
        new_terrain("tabs", "b", "B").unwrap(),
    ];
    let project = documents[0].project_id;
    let module = ModuleId::deterministic("tabs-module");
    for (key, action) in [
        (
            "project",
            WorkspaceAction::CreateProject {
                id: project,
                name: "Tabs".into(),
            },
        ),
        (
            "module",
            WorkspaceAction::CreateModule {
                project_id: project,
                id: module,
                name: "Module".into(),
            },
        ),
    ] {
        file.execute(&WorkspaceRequest {
            command_id: CommandId::deterministic(key),
            base_revision: file.session().head().revision,
            base_hash: file.session().head().content_hash().unwrap(),
            action,
        })
        .unwrap();
    }
    let areas = [documents[0].area_id, documents[1].area_id];
    for document in documents {
        let area = document.area_id;
        host.execute(R::CreateZone {
            document: Box::new(document),
        })
        .unwrap();
        host.execute(R::Save).unwrap();
        host.execute(R::Close).unwrap();
        let bytes = std::fs::read(root.join(area.to_string()).join("authoring.json")).unwrap();
        let review = file
            .review_adoption(area, project, module, forge_core::bytes_sha256(&bytes))
            .unwrap();
        file.adopt(&review.request).unwrap();
        file.save().unwrap();
    }
    drop(file);
    host.execute_workspace(W::Open).unwrap();
    (host, areas)
}

#[test]
fn tab_limit_refuses_seventeenth_without_eviction_and_releases_after_close() {
    let root = std::env::temp_dir().join(format!("forge-tabs-limit-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let (mut host, initial) = setup(&root);
    let module_id = ModuleId::deterministic("tabs-module");
    let mut areas = initial.to_vec();
    for index in 2..17 {
        let document = Box::new(
            new_terrain("tabs", &format!("limit-{index}"), &format!("Zone {index}")).unwrap(),
        );
        areas.push(document.area_id);
        let reviewed = host
            .execute_workspace(W::ReviewNewZone {
                module_id,
                document: document.clone(),
            })
            .unwrap();
        host.execute_workspace(W::CreateNewZone {
            request: Box::new(reviewed.receipt.unwrap().request),
            document,
        })
        .unwrap();
    }
    host.execute_workspace(W::Save).unwrap();
    for &area_id in &areas[..16] {
        host.execute(R::OpenManaged { area_id }).unwrap();
    }
    let before = host.execute(R::Inspect).unwrap();
    let disk = std::fs::read(root.join("workspace-v1/authoring.json")).unwrap();
    assert_eq!(before.managed_tabs.len(), 16);
    assert!(
        host.execute(R::OpenManaged { area_id: areas[16] })
            .unwrap_err()
            .contains("16")
    );
    let refused = host.execute(R::Inspect).unwrap();
    assert_eq!(refused.managed_tabs, before.managed_tabs);
    assert_eq!(refused.zone_head, before.zone_head);
    // Existing tabs remain switchable at the limit; no LRU eviction or save.
    host.execute(R::OpenManaged { area_id: areas[0] }).unwrap();
    host.execute(R::Close).unwrap();
    let opened = host.execute(R::OpenManaged { area_id: areas[16] }).unwrap();
    assert_eq!(opened.managed_tabs.len(), 16);
    assert_eq!(opened.document.unwrap().area_id, areas[16]);
    assert_eq!(
        std::fs::read(root.join("workspace-v1/authoring.json")).unwrap(),
        disk
    );
    drop(host);
    std::fs::remove_dir_all(root).unwrap();
}

#[test]
fn switching_tabs_retains_unsaved_edits_and_save_reopens_both_zones() {
    let root = std::env::temp_dir().join(format!("forge-workspace-tabs-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let (mut host, [a, b]) = setup(&root);
    let state = host.execute(R::OpenManaged { area_id: a }).unwrap();
    let head = state.zone_head.unwrap();
    let mut corrections = head.corrections.clone();
    corrections.name = Some("A retouchée".into());
    let request = ZoneRequest {
        command_id: CommandId::deterministic("tabs-edit"),
        expected_hash: head.content_hash().unwrap(),
        expected_revision: head.revision,
        action: ZoneAction::edit(corrections),
    };
    host.execute(R::PreviewZone {
        request: request.clone(),
    })
    .unwrap();
    let applied = host.execute(R::ApplyZone { request }).unwrap();
    let edited_hash = applied.zone_head.unwrap().content_hash().unwrap();
    let switched = host.execute(R::OpenManaged { area_id: b }).unwrap();
    assert_eq!(switched.managed_tabs.len(), 2);
    assert!(!switched.dirty);
    assert!(
        switched
            .managed_tabs
            .iter()
            .find(|t| t.area_id == a)
            .unwrap()
            .dirty
    );
    assert!(
        !switched
            .managed_tabs
            .iter()
            .find(|t| t.area_id == b)
            .unwrap()
            .dirty
    );
    let returned = host.execute(R::OpenManaged { area_id: a }).unwrap();
    assert_eq!(
        returned.zone_head.unwrap().content_hash().unwrap(),
        edited_hash
    );
    assert!(returned.dirty);
    assert!(host.execute(R::Close).is_err());
    let saved = host.execute(R::Save).unwrap();
    assert!(saved.managed_tabs.iter().all(|t| !t.dirty));
    let closed = host.execute(R::Close).unwrap();
    assert_eq!(closed.document.unwrap().area_id, b);
    assert_eq!(closed.managed_tabs.len(), 1);
    host.execute(R::Close).unwrap();
    host.execute_workspace(W::Close).unwrap();
    host.execute_workspace(W::Open).unwrap();
    let reopened = host.execute(R::OpenManaged { area_id: a }).unwrap();
    assert_eq!(
        reopened.zone_head.unwrap().content_hash().unwrap(),
        edited_hash
    );
    assert!(!reopened.dirty);
    drop(host);
    std::fs::remove_dir_all(root).unwrap();
}

#[test]
fn closing_clean_tab_returns_to_dirty_neighbour_without_saving() {
    let root = std::env::temp_dir().join(format!("forge-tabs-close-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let (mut host, [a, b]) = setup(&root);
    let saved = std::fs::read(root.join("workspace-v1/authoring.json")).unwrap();
    let head = host
        .execute(R::OpenManaged { area_id: a })
        .unwrap()
        .zone_head
        .unwrap();
    let mut corrections = head.corrections.clone();
    corrections.name = Some("Retouche conservée".into());
    let request = ZoneRequest {
        command_id: CommandId::deterministic("close-neighbour-edit"),
        expected_hash: head.content_hash().unwrap(),
        expected_revision: head.revision,
        action: ZoneAction::edit(corrections),
    };
    host.execute(R::PreviewZone {
        request: request.clone(),
    })
    .unwrap();
    let edited = host
        .execute(R::ApplyZone { request })
        .unwrap()
        .zone_head
        .unwrap();
    host.execute(R::OpenManaged { area_id: b }).unwrap();
    let closed = host.execute(R::Close).unwrap();
    assert_eq!(closed.document.unwrap().area_id, a);
    assert_eq!(closed.zone_head.unwrap(), edited);
    assert!(closed.dirty);
    assert_eq!(closed.managed_tabs.len(), 1);
    assert!(host.execute(R::Close).is_err());
    assert_eq!(
        std::fs::read(root.join("workspace-v1/authoring.json")).unwrap(),
        saved
    );
    drop(host);
    std::fs::remove_dir_all(root).unwrap();
}

#[test]
fn failed_neighbour_read_keeps_current_tab_and_retry_is_possible() {
    let root = std::env::temp_dir().join(format!("forge-tabs-corrupt-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let (mut host, [a, b]) = setup(&root);
    let workspace = host.execute_workspace(W::Inspect).unwrap().head.unwrap();
    let reference = workspace.projects[0].modules[0]
        .zones
        .iter()
        .find(|r| r.area_id == a)
        .unwrap();
    let object = root
        .join("workspace-v1/objects")
        .join(format!("{}.json", reference.archive_hash));
    let original = std::fs::read(&object).unwrap();
    let saved = std::fs::read(root.join("workspace-v1/authoring.json")).unwrap();
    host.execute(R::OpenManaged { area_id: a }).unwrap();
    let before = host.execute(R::OpenManaged { area_id: b }).unwrap();
    // Damage only this synthetic fixture, after both projections were opened.
    std::fs::write(&object, b"invalid archive").unwrap();
    let error = host.execute(R::Close).unwrap_err();
    assert!(error.contains("current tab kept"));
    assert!(error.contains(&a.to_string()));
    let after = host.execute(R::Inspect).unwrap();
    assert_eq!(after.zone_head, before.zone_head);
    assert_eq!(after.managed_tabs, before.managed_tabs);
    assert_eq!(
        std::fs::read(root.join("workspace-v1/authoring.json")).unwrap(),
        saved
    );
    std::fs::write(&object, original).unwrap();
    let retried = host.execute(R::Close).unwrap();
    assert_eq!(retried.document.unwrap().area_id, a);
    assert_eq!(retried.managed_tabs.len(), 1);
    drop(host);
    std::fs::remove_dir_all(root).unwrap();
}
