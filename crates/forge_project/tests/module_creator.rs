#![cfg(not(target_arch = "wasm32"))]
use forge_core::{AreaId, CommandId, EntityId, ModuleId, ProjectId};
use forge_gameplay::{CreatorSurface, GameplayDocument, ModuleCreatorProfile};
use forge_project::{
    authoring::creation::{NewTerrainSettings, prepare_module_terrain},
    authoring::{
        AuthoringRequest as A, AuthoringState, host::AuthoringHost,
        workspace::WorkspaceHostRequest as W,
    },
    workspace::{
        file_session::FileWorkspaceSession,
        session::{WorkspaceAction, WorkspaceRequest},
    },
};
use std::path::{Path, PathBuf};

fn command(
    workspace: &FileWorkspaceSession,
    key: &str,
    action: WorkspaceAction,
) -> WorkspaceRequest {
    let head = workspace.session().head();
    WorkspaceRequest {
        command_id: CommandId::deterministic(key),
        base_revision: head.revision,
        base_hash: head.content_hash().unwrap(),
        action,
    }
}

fn preview_settings(state: &AuthoringState, key: &str) -> A {
    A::PreviewModuleCreator {
        expected_workspace_hash: state
            .module_creator_context
            .as_ref()
            .unwrap()
            .workspace_hash,
        profile: definition(state.gameplay_module_id.unwrap()).profile,
        surfaces: vec![CreatorSurface::Rules],
        required_capabilities: vec![forge_gameplay::GameplayCapability::Actors],
        command_id: CommandId::deterministic(key),
    }
}

fn host_history(host: &mut AuthoringHost, redo: bool, key: &str) -> AuthoringState {
    let state = host.execute(A::Inspect).unwrap();
    let review = host
        .execute(A::PreviewModuleHistory {
            expected_workspace_hash: state.module_creator_context.unwrap().workspace_hash,
            redo,
            command_id: CommandId::deterministic(key),
        })
        .unwrap()
        .module_history_receipt
        .unwrap();
    let applied = host
        .execute(A::ApplyModuleHistory {
            request: Box::new(review.request.clone()),
        })
        .unwrap();
    assert_eq!(
        host.execute(A::ApplyModuleHistory {
            request: Box::new(review.request)
        })
        .unwrap(),
        applied
    );
    applied
}

#[test]
fn separate_game_exports_current_unsaved_zone_without_saving_or_freezing_it() {
    use forge_project::terrain_edit::{TerrainAction, TerrainEdit};

    let (root, workspace, module, areas) = setup("game-export");
    let mut expected = workspace.read_zone(areas[0]).unwrap();
    let saved_archive = expected.archive_bytes().unwrap();
    drop(workspace);
    let mut host = AuthoringHost::new(&root).unwrap();
    host.execute_workspace(W::Open).unwrap();
    let initial = host.execute(A::OpenManaged { area_id: areas[0] }).unwrap();
    assert!(!initial.dirty);
    assert_eq!(initial.zone_head.as_ref(), Some(expected.head()));
    let request = expected
        .head()
        .prepare_terrain_action(
            TerrainAction::Edit(TerrainEdit::Rename {
                name: "Current unsaved map".into(),
            }),
            CommandId::deterministic("game-export/rename"),
        )
        .unwrap();
    host.execute(A::PreviewZone {
        request: request.clone(),
    })
    .unwrap();
    let applied = host
        .execute(A::ApplyZone {
            request: request.clone(),
        })
        .unwrap();
    expected.execute(&request).unwrap();
    assert_eq!(applied.zone_head.as_ref(), Some(expected.head()));
    let hash = expected.head().content_hash().unwrap();
    let expected_archive = expected.archive_bytes().unwrap();
    assert_ne!(expected_archive, saved_archive);
    let before_export = files(&root);
    assert_eq!(host.game_archive(hash).unwrap(), expected_archive);
    assert!(
        host.game_archive(forge_core::ContentHash::from_bytes([0; 32]))
            .is_err()
    );
    let state = host.execute(A::Inspect).unwrap();
    assert!(state.dirty);
    assert!(state.can_undo);
    assert_eq!(state.playtest_hash, None);
    assert_eq!(state.gameplay_module_id, Some(module));
    assert_eq!(state.module_creator_context.unwrap().module_id, module);
    assert_eq!(state.zone_head.unwrap().content_hash().unwrap(), hash);
    assert_eq!(files(&root), before_export);
    drop(host);
    std::fs::remove_dir_all(root).unwrap();
}

#[test]
fn studio_host_reviews_all_areas_keeps_preview_audit_and_refreshes_module_history() {
    use forge_project::audit::AuditStamp;
    let (root, workspace, module, areas) = setup("host");
    drop(workspace);
    let mut host = AuthoringHost::new(&root).unwrap();
    host.execute_workspace(W::Open).unwrap();
    let initial = host.execute(A::OpenManaged { area_id: areas[0] }).unwrap();
    let before = files(&root);
    let stamp = AuditStamp::human("creator-author", "2026-10-19T21:25:00Z").unwrap();
    let preview = host
        .execute_with_audit(preview_settings(&initial, "host/configure"), stamp.clone())
        .unwrap();
    assert_eq!(files(&root), before);
    assert_eq!(
        preview
            .module_creator_affected
            .iter()
            .map(|zone| zone.area_id)
            .collect::<Vec<_>>(),
        areas
    );
    let review = preview.module_creator_review.unwrap();
    let mut forged = review.clone();
    forged.definition.profile.attack_damage = 99;
    assert!(
        host.execute(A::ApplyModuleCreator {
            review: Box::new(forged)
        })
        .is_err()
    );
    let later = AuditStamp::human("creator-author", "2026-10-19T21:26:00Z").unwrap();
    let applied = host
        .execute_with_audit(
            A::ApplyModuleCreator {
                review: Box::new(review.clone()),
            },
            later,
        )
        .unwrap();
    assert_eq!(
        applied
            .zone_head
            .as_ref()
            .unwrap()
            .corrections
            .gameplay
            .as_ref()
            .unwrap()
            .profile
            .attack_damage,
        37
    );
    assert_eq!(
        host.execute(A::ApplyModuleCreator {
            review: Box::new(review)
        })
        .unwrap(),
        applied
    );
    let second = host.execute(A::OpenManaged { area_id: areas[1] }).unwrap();
    assert_eq!(
        second.module_creator_context.unwrap().definition.unwrap(),
        definition(module)
    );
    let undone = host_history(&mut host, false, "host/undo");
    assert_eq!(
        undone
            .zone_head
            .unwrap()
            .corrections
            .gameplay
            .unwrap()
            .profile
            .attack_damage,
        20
    );
    assert!(undone.module_creator_context.unwrap().definition.is_none());
    let redone = host_history(&mut host, true, "host/redo");
    assert_eq!(
        redone
            .zone_head
            .unwrap()
            .corrections
            .gameplay
            .unwrap()
            .profile
            .attack_damage,
        37
    );
    host.execute(A::Save).unwrap();
    drop(host);
    assert_saved_module_audit(&root, &areas, &stamp);
    std::fs::remove_dir_all(root).unwrap();
}

fn assert_saved_module_audit(
    root: &Path,
    areas: &[AreaId],
    stamp: &forge_project::audit::AuditStamp,
) {
    let saved = FileWorkspaceSession::open(root).unwrap();
    let entry = saved
        .session()
        .audit()
        .iter()
        .find(|entry| entry.request.command_id == CommandId::deterministic("host/configure"))
        .unwrap();
    assert_eq!(&entry.audit, stamp);
    for area in areas {
        assert_eq!(
            saved
                .read_zone(*area)
                .unwrap()
                .head()
                .corrections
                .gameplay
                .as_ref()
                .unwrap()
                .profile
                .attack_damage,
            37
        );
    }
}

#[test]
fn module_history_refuses_another_module_and_the_active_area_removal() {
    let (root, mut workspace, module, _) = setup("scope");
    let project = workspace.session().head().projects[0].id;
    let other = ModuleId::deterministic("scope/other");
    workspace
        .execute(&command(
            &workspace,
            "scope/other-module",
            WorkspaceAction::CreateModule {
                project_id: project,
                id: other,
                name: "Other".into(),
            },
        ))
        .unwrap();
    let document =
        prepare_module_terrain(project, other, "new", "New", NewTerrainSettings::default())
            .unwrap();
    let area = document.area_id;
    let receipt = workspace.review_new_zone(other, document.clone()).unwrap();
    workspace
        .create_new_zone(&receipt.request, document)
        .unwrap();
    let expected = workspace.session().head().content_hash().unwrap();
    assert!(
        workspace
            .preview_module_history(
                module,
                expected,
                false,
                CommandId::deterministic("scope/wrong-undo")
            )
            .is_err()
    );
    workspace.save().unwrap();
    drop(workspace);
    let mut host = AuthoringHost::new(&root).unwrap();
    host.execute_workspace(W::Open).unwrap();
    let state = host.execute(A::OpenManaged { area_id: area }).unwrap();
    assert!(!state.module_creator_context.unwrap().can_undo);
    let before = files(&root);
    assert!(
        host.execute(A::PreviewModuleHistory {
            expected_workspace_hash: expected,
            redo: false,
            command_id: CommandId::deterministic("scope/remove-active")
        })
        .is_err()
    );
    assert_eq!(files(&root), before);
    assert_eq!(
        host.execute(A::Inspect).unwrap().document.unwrap().area_id,
        area
    );
    drop(host);
    std::fs::remove_dir_all(root).unwrap();
}

#[test]
fn inspector_capabilities_include_hidden_content_and_refuse_future_or_duplicate_claims() {
    use forge_gameplay::{GameplayCapability as C, ItemDefinition};
    let (root, mut workspace, module, areas) = setup("requirements");
    let zone = workspace.read_zone(areas[1]).unwrap();
    let mut document = zone.head().corrections.gameplay.as_deref().unwrap().clone();
    let item = ItemDefinition {
        id: EntityId::deterministic("requirements/item"),
        name: "Hidden item".into(),
        max_stack: 2,
    };
    document.items.push(item.clone());
    let request = zone
        .head()
        .prepare_gameplay(
            Some(document),
            CommandId::deterministic("requirements/item"),
        )
        .unwrap();
    let (receipt, _) = workspace
        .preview_zone_edit(
            areas[1],
            &request,
            CommandId::deterministic("requirements/item-root"),
        )
        .unwrap();
    workspace
        .apply_zone_edit(&receipt.request, &request)
        .unwrap();
    let hash = workspace.session().head().content_hash().unwrap();
    let profile = definition(module).profile;
    let definition = workspace
        .build_module_creator(
            module,
            hash,
            profile.clone(),
            vec![CreatorSurface::Rules],
            vec![C::Actors],
        )
        .unwrap();
    assert!(definition.required_capabilities.contains(&C::Inventory));
    let before = files(&root);
    let supported = workspace
        .build_module_creator(
            module,
            hash,
            profile.clone(),
            vec![CreatorSurface::Rules],
            vec![C::SandboxedScripts],
        )
        .unwrap();
    assert!(
        supported
            .required_capabilities
            .contains(&C::SandboxedScripts)
    );
    let mut unknown = serde_json::to_value(&supported).unwrap();
    unknown["required_capabilities"] = serde_json::json!(["future_unavailable_api"]);
    assert!(serde_json::from_value::<ModuleCreatorProfile>(unknown).is_err());
    assert!(
        workspace
            .build_module_creator(
                module,
                hash,
                profile,
                vec![CreatorSurface::Rules],
                vec![C::Actors, C::Actors]
            )
            .is_err()
    );
    assert_eq!(files(&root), before);
    let review = workspace
        .preview_module_creator(
            definition,
            CommandId::deterministic("requirements/configure"),
        )
        .unwrap();
    workspace.apply_module_creator(&review).unwrap();
    assert_eq!(
        workspace
            .read_zone(areas[1])
            .unwrap()
            .head()
            .corrections
            .gameplay
            .as_ref()
            .unwrap()
            .items,
        vec![item]
    );
    drop(workspace);
    std::fs::remove_dir_all(root).unwrap();
}

#[test]
fn creator_host_refuses_unreviewed_stale_and_playtest_changes_without_writes() {
    let (root, workspace, module, areas) = setup("mode-guards");
    let unreviewed = workspace
        .preview_module_creator(
            definition(module),
            CommandId::deterministic("mode-guards/unreviewed"),
        )
        .unwrap();
    drop(workspace);
    let mut host = AuthoringHost::new(&root).unwrap();
    host.execute_workspace(W::Open).unwrap();
    let initial = host.execute(A::OpenManaged { area_id: areas[0] }).unwrap();
    let before = files(&root);
    assert!(
        host.execute(A::ApplyModuleCreator {
            review: Box::new(unreviewed)
        })
        .is_err()
    );
    assert_eq!(files(&root), before);
    let stale = preview_settings(&initial, "mode-guards/stale");
    let head = initial.zone_head.unwrap();
    let mut document = head.corrections.gameplay.as_deref().unwrap().clone();
    document.actors[0].name = "Retouche locale".into();
    let request = head
        .prepare_gameplay(
            Some(document),
            CommandId::deterministic("mode-guards/retouch"),
        )
        .unwrap();
    host.execute(A::PreviewZone {
        request: request.clone(),
    })
    .unwrap();
    let edited = host.execute(A::ApplyZone { request }).unwrap();
    let before = files(&root);
    assert!(host.execute(stale).is_err());
    assert_eq!(files(&root), before);
    let playing = host
        .execute(A::StartPlaytest {
            expected_hash: edited.zone_head.unwrap().content_hash().unwrap(),
        })
        .unwrap();
    let mode_error = host
        .execute(preview_settings(&playing, "mode-guards/playing"))
        .unwrap_err();
    assert!(mode_error.contains("stop the playtest"));
    let history_error = host
        .execute(A::PreviewModuleHistory {
            expected_workspace_hash: playing.module_creator_context.unwrap().workspace_hash,
            redo: false,
            command_id: CommandId::deterministic("mode-guards/history"),
        })
        .unwrap_err();
    assert!(history_error.contains("stop the playtest"));
    host.execute(A::StopPlaytest { diagnostics: None }).unwrap();
    assert_eq!(files(&root), before);
    drop(host);
    std::fs::remove_dir_all(root).unwrap();
}

#[test]
fn snapshot_exports_all_module_areas_and_content_revision_ignores_another_module() {
    let (root, mut workspace, module, areas) = setup("snapshot");
    let expected = workspace.session().head().content_hash().unwrap();
    let primary = workspace
        .read_zone(areas[0])
        .unwrap()
        .head()
        .content_hash()
        .unwrap();
    let snapshot = workspace
        .module_export_snapshot(module, areas[0], primary, expected)
        .unwrap();
    assert_eq!(
        snapshot
            .zones
            .iter()
            .map(|zone| zone.area_id)
            .collect::<Vec<_>>(),
        areas
    );
    assert_eq!(
        snapshot.content_hash().unwrap(),
        workspace.module_content_hash(module).unwrap()
    );
    let source_hash = snapshot.content_hash().unwrap();
    let project = workspace.session().head().projects[0].id;
    workspace
        .execute(&command(
            &workspace,
            "snapshot/other-module",
            WorkspaceAction::CreateModule {
                project_id: project,
                id: ModuleId::deterministic("snapshot/other"),
                name: "Other".into(),
            },
        ))
        .unwrap();
    assert_eq!(workspace.module_content_hash(module).unwrap(), source_hash);
    assert!(
        workspace
            .module_export_snapshot(module, areas[0], primary, expected)
            .is_err()
    );
    let zone = workspace.read_zone(areas[1]).unwrap();
    let mut document = zone.head().corrections.gameplay.as_deref().unwrap().clone();
    document.actors[0].name = "Second area retouched".into();
    let request = zone
        .head()
        .prepare_gameplay(Some(document), CommandId::deterministic("snapshot/retouch"))
        .unwrap();
    let (receipt, _) = workspace
        .preview_zone_edit(
            areas[1],
            &request,
            CommandId::deterministic("snapshot/retouch-root"),
        )
        .unwrap();
    workspace
        .apply_zone_edit(&receipt.request, &request)
        .unwrap();
    assert_ne!(workspace.module_content_hash(module).unwrap(), source_hash);
    assert_eq!(snapshot.content_hash().unwrap(), source_hash);
    drop(workspace);
    std::fs::remove_dir_all(root).unwrap();
}

#[test]
fn admitting_already_materialized_values_pins_the_definition_without_rewriting_archives() {
    let (root, mut workspace, module, areas) = setup("already-materialized");
    let mut definition = definition(module);
    definition.surfaces.push(CreatorSurface::Actors);
    for area in &areas {
        let zone = workspace.read_zone(*area).unwrap();
        let mut document = definition
            .apply_to(zone.head().corrections.gameplay.as_deref().unwrap())
            .unwrap();
        document.creator.as_mut().unwrap().surfaces.reverse();
        let request = zone
            .head()
            .prepare_gameplay(
                Some(document),
                CommandId::deterministic(format!("already-materialized/{area}")),
            )
            .unwrap();
        let (receipt, _) = workspace
            .preview_zone_edit(
                *area,
                &request,
                CommandId::deterministic(format!("already-materialized/root/{area}")),
            )
            .unwrap();
        workspace
            .apply_zone_edit(&receipt.request, &request)
            .unwrap();
    }
    let original: Vec<_> = areas
        .iter()
        .map(|area| workspace.read_zone(*area).unwrap().archive_bytes().unwrap())
        .collect();
    let review = workspace
        .preview_module_creator(
            definition,
            CommandId::deterministic("already-materialized/pin"),
        )
        .unwrap();
    assert!(review.changed_areas.is_empty());
    workspace.apply_module_creator(&review).unwrap();
    workspace.save().unwrap();
    drop(workspace);
    let workspace = FileWorkspaceSession::open(&root).unwrap();
    for (area, bytes) in areas.iter().zip(original) {
        assert_eq!(
            workspace.read_zone(*area).unwrap().archive_bytes().unwrap(),
            bytes
        );
    }
    drop(workspace);
    std::fs::remove_dir_all(root).unwrap();
}

fn gameplay(module: ModuleId, area: AreaId) -> GameplayDocument {
    let player = EntityId::deterministic(format!("{area}/player"));
    serde_json::from_value(serde_json::json!({
        "schema":"forge.gameplay", "version":1, "module_id":module, "area_id":area, "player":player,
        "profile": {"id":"forge.gate3-minimal", "version":1, "interaction_range_m":3.0,
            "attack_range_m":2.0, "attack_damage":20, "attack_stamina_cost":5, "max_stamina":100,
            "max_action_receipts":512, "max_events_per_tick":128},
        "actors":[{"id":player,"name":"Player","role":"player","position":{"x":0.0,"y":0.0,"z":0.0},
            "max_vitality":100,"protection":0,"inventory_capacity":12,"dialogue":null,"patrol":[],"flees_danger":false}],
        "items":[],"objects":[],"places":[],"dialogues":[],"quests":[]
    })).unwrap()
}

fn setup(key: &str) -> (PathBuf, FileWorkspaceSession, ModuleId, Vec<AreaId>) {
    let root =
        std::env::temp_dir().join(format!("forge-module-creator-{key}-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let mut workspace = FileWorkspaceSession::create(&root).unwrap();
    let project = ProjectId::deterministic(key);
    let module = ModuleId::deterministic(key);
    for (suffix, action) in [
        (
            "project",
            WorkspaceAction::CreateProject {
                id: project,
                name: "Project".into(),
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
        workspace
            .execute(&command(&workspace, &format!("{key}/{suffix}"), action))
            .unwrap();
    }
    let mut areas = Vec::new();
    for index in 0..2 {
        let document = prepare_module_terrain(
            project,
            module,
            &format!("{key}-{index}"),
            "Area",
            NewTerrainSettings::default(),
        )
        .unwrap();
        let area = document.area_id;
        let review = workspace.review_new_zone(module, document.clone()).unwrap();
        workspace
            .create_new_zone(&review.request, document)
            .unwrap();
        let source = workspace.read_zone(area).unwrap();
        let request = source
            .head()
            .prepare_gameplay(
                Some(gameplay(module, area)),
                CommandId::deterministic(format!("{key}/init/{index}")),
            )
            .unwrap();
        let (review, _) = workspace
            .preview_zone_edit(
                area,
                &request,
                CommandId::deterministic(format!("{key}/attach/{index}")),
            )
            .unwrap();
        workspace
            .apply_zone_edit(&review.request, &request)
            .unwrap();
        areas.push(area);
    }
    workspace.save().unwrap();
    (root, workspace, module, areas)
}

fn definition(module: ModuleId) -> ModuleCreatorProfile {
    let mut profile = gameplay(module, AreaId::deterministic("profile-only")).profile;
    profile.attack_damage = 37;
    ModuleCreatorProfile::for_documents(module, profile, vec![CreatorSurface::Rules], &[]).unwrap()
}

fn files(path: &Path) -> Vec<(PathBuf, Vec<u8>)> {
    let mut result = Vec::new();
    for entry in std::fs::read_dir(path).unwrap() {
        let path = entry.unwrap().path();
        if path.is_dir() {
            result.extend(files(&path));
        } else if path.file_name().is_none_or(|name| name != "authoring.lock") {
            // Windows locks this coordination file exclusively; it contains no source data.
            result.push((path.clone(), std::fs::read(path).unwrap()));
        }
    }
    result.sort();
    result
}

#[test]
fn preview_writes_nothing_and_one_operation_survives_save_open_undo_redo() {
    let (root, mut workspace, module, areas) = setup("roundtrip");
    let original = workspace.session().head().projects.clone();
    let before = files(&root);
    let review = workspace
        .preview_module_creator(
            definition(module),
            CommandId::deterministic("roundtrip/configure"),
        )
        .unwrap();
    assert_eq!(files(&root), before);
    assert_eq!(review.changed_areas, areas);
    assert_eq!(workspace.session().head().projects, original);
    let receipt = workspace.apply_module_creator(&review).unwrap();
    assert_eq!(workspace.apply_module_creator(&review).unwrap(), receipt);
    let configured = workspace.session().head().projects.clone();
    for area in &areas {
        let zone = workspace.read_zone(*area).unwrap();
        definition(module)
            .validate_document(zone.head().corrections.gameplay.as_deref().unwrap())
            .unwrap();
    }
    workspace.save().unwrap();
    drop(workspace);
    let mut workspace = FileWorkspaceSession::open(&root).unwrap();
    assert_eq!(workspace.session().head().projects, configured);
    assert_eq!(workspace.session().head().schema_version, 2);
    workspace
        .execute(&command(
            &workspace,
            "roundtrip/undo",
            WorkspaceAction::Undo,
        ))
        .unwrap();
    assert_eq!(workspace.session().head().projects, original);
    assert_eq!(workspace.session().head().schema_version, 1);
    workspace.save().unwrap();
    workspace
        .execute(&command(
            &workspace,
            "roundtrip/redo",
            WorkspaceAction::Redo,
        ))
        .unwrap();
    assert_eq!(workspace.session().head().projects, configured);
    workspace.save().unwrap();
    drop(workspace);
    let workspace = FileWorkspaceSession::open(&root).unwrap();
    assert_eq!(workspace.session().head().projects, configured);
    drop(workspace);
    std::fs::remove_dir_all(root).unwrap();
}

#[test]
fn stale_forged_foreign_and_independent_rule_edits_cannot_publish() {
    let (root, mut workspace, module, areas) = setup("refusal");
    let review = workspace
        .preview_module_creator(
            definition(module),
            CommandId::deterministic("refusal/configure"),
        )
        .unwrap();
    let mut forged = review.clone();
    forged.changed_areas.clear();
    let before = files(&root);
    assert!(workspace.apply_module_creator(&forged).is_err());
    let foreign = definition(ModuleId::deterministic("foreign"));
    assert!(
        workspace
            .preview_module_creator(foreign, CommandId::deterministic("refusal/foreign"))
            .is_err()
    );
    assert_eq!(files(&root), before);
    workspace.apply_module_creator(&review).unwrap();
    let source = workspace.read_zone(areas[0]).unwrap();
    let mut document = source
        .head()
        .corrections
        .gameplay
        .as_deref()
        .unwrap()
        .clone();
    document.profile.attack_damage = 99;
    let request = source
        .head()
        .prepare_gameplay(
            Some(document),
            CommandId::deterministic("refusal/zone-rule"),
        )
        .unwrap();
    let before = files(&root);
    let head = workspace.session().head().clone();
    assert!(
        workspace
            .preview_zone_edit(
                areas[0],
                &request,
                CommandId::deterministic("refusal/isolated")
            )
            .is_err()
    );
    assert_eq!(workspace.session().head(), &head);
    assert_eq!(files(&root), before);
    let mut revised = definition(module);
    revised.profile.attack_damage = 40;
    let stale = workspace
        .preview_module_creator(revised, CommandId::deterministic("refusal/stale"))
        .unwrap();
    workspace
        .execute(&command(&workspace, "refusal/undo", WorkspaceAction::Undo))
        .unwrap();
    assert!(workspace.apply_module_creator(&stale).is_err());
    drop(workspace);
    std::fs::remove_dir_all(root).unwrap();
}

#[test]
fn corrupt_definition_preserves_saved_head_and_is_checked_in_undo_history() {
    let (root, mut workspace, module, _) = setup("corruption");
    let review = workspace
        .preview_module_creator(
            definition(module),
            CommandId::deterministic("corruption/configure"),
        )
        .unwrap();
    workspace.apply_module_creator(&review).unwrap();
    workspace.save().unwrap();
    workspace
        .execute(&command(
            &workspace,
            "corruption/undo",
            WorkspaceAction::Undo,
        ))
        .unwrap();
    workspace.save().unwrap();
    let object = root.join("workspace-v1/objects").join(format!(
        "{}.json",
        definition(module).content_hash().unwrap()
    ));
    std::fs::write(&object, b"corrupt").unwrap();
    let before = files(&root);
    assert!(workspace.save().is_err());
    assert_eq!(files(&root), before);
    drop(workspace);
    assert!(FileWorkspaceSession::open(&root).is_err());
    assert_eq!(files(&root), before);
    std::fs::remove_dir_all(root).unwrap();
}
