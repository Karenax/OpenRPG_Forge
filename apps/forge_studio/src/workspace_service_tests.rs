//! Real local-filesystem service tests, not crash/power-loss or concurrent-writer qualification.

use super::WorkspaceSession;
use crate::workspace_api::{MAX_WORKSPACE_IPC_REVISION, WorkspaceCommand, WorkspaceState};
use forge_project::{MAX_WORKSPACE_JSON_BYTES, WorkspaceDocument};
use serde_json::{Value, json};
use std::{
    collections::BTreeMap,
    ffi::OsString,
    fs,
    path::{Component, MAIN_SEPARATOR, Path, PathBuf},
    sync::atomic::{AtomicU64, Ordering},
    time::{SystemTime, UNIX_EPOCH},
};

static TEST_DIRECTORY_NONCE: AtomicU64 = AtomicU64::new(0);

/// Owns only a newly created, direct child of the canonical OS temporary directory.
struct TestDirectory {
    path: PathBuf,
    parent: PathBuf,
}

impl TestDirectory {
    fn new() -> Self {
        let parent = fs::canonicalize(std::env::temp_dir()).unwrap();
        let tick = SystemTime::now().duration_since(UNIX_EPOCH).unwrap();
        let path = parent.join(format!(
            "forge-workspace-service-test-{}-{}-{}",
            std::process::id(),
            tick.as_nanos(),
            TEST_DIRECTORY_NONCE.fetch_add(1, Ordering::Relaxed)
        ));
        // A collision fails rather than taking ownership of an existing directory.
        fs::create_dir(&path).unwrap();
        assert_eq!(fs::canonicalize(&path).unwrap(), path);
        Self { path, parent }
    }

    fn write(&self, name: &str, bytes: impl AsRef<[u8]>) -> PathBuf {
        let mut components = Path::new(name).components();
        assert!(matches!(components.next(), Some(Component::Normal(_))));
        assert!(components.next().is_none());
        let path = self.path.join(name);
        assert!(
            !path.exists(),
            "fixture must not overwrite an existing file"
        );
        fs::write(&path, bytes).unwrap();
        path
    }

    fn files(&self) -> BTreeMap<OsString, Vec<u8>> {
        fs::read_dir(&self.path)
            .unwrap()
            .map(|entry| {
                let entry = entry.unwrap();
                assert!(entry.file_type().unwrap().is_file());
                (entry.file_name(), fs::read(entry.path()).unwrap())
            })
            .collect()
    }
}

impl Drop for TestDirectory {
    fn drop(&mut self) {
        // Resolve and check the exact owned directory before deleting any direct child.
        // No recursive deletion, parent cleanup, glob, or symlink traversal is used.
        let metadata = fs::symlink_metadata(&self.path).unwrap();
        assert!(metadata.is_dir() && !metadata.file_type().is_symlink());
        let resolved = fs::canonicalize(&self.path).unwrap();
        assert_eq!(resolved, self.path);
        assert_eq!(resolved.parent(), Some(self.parent.as_path()));
        for entry in fs::read_dir(&resolved).unwrap() {
            let entry = entry.unwrap();
            assert!(entry.file_type().unwrap().is_file());
            assert_eq!(entry.path().parent(), Some(resolved.as_path()));
            fs::remove_file(entry.path()).unwrap();
        }
        fs::remove_dir(&resolved).unwrap();
    }
}

fn create_command() -> WorkspaceCommand {
    WorkspaceCommand::Create {
        project_name: "Projet des collines".into(),
        module_name: "Module de preuve".into(),
        area_name: "Clairière extérieure".into(),
        seed: format!(" {} ", u64::MAX),
        spawn_x: " -12.5 ".into(),
        spawn_y: "17.5".into(),
        spawn_z: "48.25".into(),
    }
}

fn document(state: &WorkspaceState) -> WorkspaceDocument {
    WorkspaceDocument::from_json(state.document_json.as_deref().unwrap()).unwrap()
}

fn receipt(state: &WorkspaceState) -> Value {
    serde_json::from_str(state.last_receipt_json.as_deref().unwrap()).unwrap()
}

fn current(session: &mut WorkspaceSession) -> WorkspaceState {
    session.execute(WorkspaceCommand::Current).unwrap()
}

fn save(session: &mut WorkspaceSession, directory: &TestDirectory) -> WorkspaceState {
    let base_revision = current(session).revision;
    session
        .execute(WorkspaceCommand::Save {
            directory: directory.path.to_str().unwrap().into(),
            base_revision,
        })
        .unwrap()
}

fn open_command(path: &Path) -> WorkspaceCommand {
    WorkspaceCommand::Open {
        path: path.to_str().unwrap().into(),
    }
}

fn assert_rejected_without_state_change(session: &mut WorkspaceSession, command: WorkspaceCommand) {
    let before = current(session);
    let error = session.execute(command).unwrap_err();
    assert!(!error.trim().is_empty());
    assert_eq!(current(session), before);
}

fn brush_command(state: &WorkspaceState, dry_run: bool) -> WorkspaceCommand {
    let source = document(state);
    WorkspaceCommand::SculptTerrain {
        request: crate::area_preview::AreaPreviewRequest {
            key: crate::area_tabs::AreaKey {
                project_id: source.project.id,
                module_id: source.project.modules[0].id,
                area_id: source.project.modules[0].areas[0].id,
            },
            base_revision: state.revision,
        },
        brush: forge_world::TerrainBrush {
            schema_version: 1,
            center_x_m: 0.0,
            center_z_m: 0.0,
            radius_m: 12.0,
            strength: 0.75,
            operation: forge_world::TerrainBrushOperation::RaiseLower { delta_m: 8.0 },
        },
        dry_run,
    }
}

#[test]
fn terrain_brush_dry_run_apply_history_and_reopen_preserve_exact_sources() {
    let directory = TestDirectory::new();
    let mut session = WorkspaceSession::default();
    session.execute(create_command()).unwrap();
    let before = save(&mut session, &directory);
    let old_files = directory.files();
    let preview = session.execute(brush_command(&before, true)).unwrap();
    let unchanged = current(&mut session);
    assert_eq!(unchanged.document_json, before.document_json);
    assert_eq!(unchanged.last_receipt_json, before.last_receipt_json);
    assert_eq!(unchanged.revision, before.revision);
    assert_eq!(
        (unchanged.can_undo, unchanged.can_redo, unchanged.dirty),
        (before.can_undo, before.can_redo, before.dirty)
    );
    let predicted: forge_world::TerrainBrushResult =
        serde_json::from_str(preview.last_receipt_json.as_deref().unwrap()).unwrap();
    assert!(predicted.changed_samples > 1);
    let applied = session.execute(brush_command(&before, false)).unwrap();
    let accepted: forge_world::TerrainBrushResult =
        serde_json::from_str(applied.last_receipt_json.as_deref().unwrap()).unwrap();
    assert_eq!(document(&applied), predicted.edit.document);
    assert_eq!(
        accepted.edit.receipt.after_hash,
        predicted.edit.receipt.after_hash
    );
    assert_eq!(
        accepted.edit.receipt.inverse_snapshot.as_ref(),
        &document(&before)
    );
    assert!(applied.can_undo && applied.dirty);
    let modified = document(&applied).project.modules[0].areas[0]
        .terrain
        .height_deltas
        .clone();
    assert!(!modified.is_empty());
    let undone = session
        .execute(WorkspaceCommand::Undo {
            base_revision: applied.revision,
        })
        .unwrap();
    assert!(
        document(&undone).project.modules[0].areas[0]
            .terrain
            .height_deltas
            .is_empty()
    );
    let redone = session
        .execute(WorkspaceCommand::Redo {
            base_revision: undone.revision,
        })
        .unwrap();
    assert_eq!(
        document(&redone).project.modules[0].areas[0]
            .terrain
            .height_deltas,
        modified
    );
    assert_eq!(directory.files(), old_files);
    let saved = save(&mut session, &directory);
    let path = saved.saved_path.as_deref().unwrap();
    let mut reopened = WorkspaceSession::default();
    let opened = reopened.execute(open_command(Path::new(path))).unwrap();
    assert_eq!(opened.document_json, saved.document_json);
    assert_eq!(
        document(&opened).content_hash().unwrap(),
        document(&saved).content_hash().unwrap()
    );
    for (name, bytes) in old_files {
        assert_eq!(directory.files()[&name], bytes);
    }
}

#[test]
fn terrain_brush_ipc_round_trip_invalid_and_stale_requests_do_not_touch_history() {
    let mut session = WorkspaceSession::default();
    let created = session.execute(create_command()).unwrap();
    let command = brush_command(&created, false);
    let json = serde_json::to_string(&command).unwrap();
    assert_eq!(
        serde_json::from_str::<WorkspaceCommand>(&json).unwrap(),
        command
    );
    assert!(
        serde_json::from_str::<WorkspaceCommand>(&json.replace(
            "\"schema_version\":1",
            "\"schema_version\":1,\"extra\":true"
        ))
        .is_err()
    );
    session.execute(command.clone()).unwrap();
    assert_rejected_without_state_change(&mut session, command);
    for mutation in 0..5 {
        let mut command = brush_command(&current(&mut session), false);
        if let WorkspaceCommand::SculptTerrain { request, brush, .. } = &mut command {
            match mutation {
                0 => {
                    request.key.project_id =
                        forge_core::ProjectId::deterministic("foreign-project");
                }
                1 => brush.radius_m = 1000.0,
                2 => brush.center_x_m = f32::NAN,
                3 => brush.strength = 0.0,
                _ => request.base_revision = MAX_WORKSPACE_IPC_REVISION + 1,
            }
        }
        assert_rejected_without_state_change(&mut session, command);
    }
}

#[test]
fn creation_builds_a_real_project_module_area_and_source_receipt() {
    let directory = TestDirectory::new();
    let mut session = WorkspaceSession::default();
    let state = session.execute(create_command()).unwrap();
    let source = document(&state);
    source.validate().unwrap();
    assert_eq!(state.revision, 1);
    assert_eq!(source.revision.get(), 1);
    assert_eq!(source.project.name, "Projet des collines");
    assert_eq!(source.project.modules[0].name, "Module de preuve");
    assert_eq!(
        source.project.modules[0].areas[0].name,
        "Clairière extérieure"
    );
    assert_eq!(source.project.modules[0].areas[0].terrain.seed, u64::MAX);
    assert_eq!(
        source.project.modules[0].areas[0].terrain.project_id,
        source.project.id
    );
    assert_eq!(
        source.project.modules[0].areas[0].terrain.area_id,
        source.project.modules[0].areas[0].id
    );
    assert_eq!(
        source.project.modules[0].areas[0].terrain.revision,
        source.revision
    );
    assert_eq!(
        source.project.modules[0].areas[0]
            .terrain
            .material_layers
            .len(),
        3
    );
    let spawn = source.project.modules[0].areas[0].spawn;
    assert_eq!(
        [spawn.x, spawn.y, spawn.z].map(f32::to_bits),
        [-12.5_f32, 17.5, 48.25].map(f32::to_bits)
    );
    assert!(state.dirty && state.can_undo && !state.can_redo);
    assert!(state.saved_path.is_none());
    let audit = receipt(&state);
    assert_eq!(audit["new_revision"], 1);
    assert_eq!(audit["base_revision"], Value::Null);
    assert_eq!(audit["before_hash"], Value::Null);
    assert_eq!(audit["after_hash"], json!(source.content_hash().unwrap()));
    assert_eq!(audit["inverse"]["operation"], "discard_draft");
    assert_eq!(audit["inverse"]["expected_after_hash"], audit["after_hash"]);
    assert!(directory.files().is_empty());
}

#[test]
fn invalid_creation_parameters_leave_the_empty_session_and_files_unchanged() {
    let directory = TestDirectory::new();
    let mut session = WorkspaceSession::default();
    for (field, values) in [
        ("project_name", vec!["", " \t ", "nom\0invalide"]),
        ("module_name", vec!["", "module\n"]),
        ("area_name", vec!["", "zone\r"]),
        ("seed", vec!["", "-1", "1.5", "18446744073709551616"]),
        ("spawn_x", vec!["bad", "NaN", "256.1"]),
        ("spawn_y", vec!["inf", "-inf", "1024.1"]),
        ("spawn_z", vec!["", "NaN", "-256.1"]),
    ] {
        for value in values {
            let mut command = serde_json::to_value(create_command()).unwrap();
            command[field] = json!(value);
            assert_rejected_without_state_change(
                &mut session,
                serde_json::from_value(command).unwrap(),
            );
        }
    }
    let mut command = serde_json::to_value(create_command()).unwrap();
    command["project_name"] = json!("é".repeat(65));
    assert_rejected_without_state_change(&mut session, serde_json::from_value(command).unwrap());
    assert!(directory.files().is_empty());
    assert_eq!(
        document(&session.execute(create_command()).unwrap())
            .revision
            .get(),
        1
    );
}

#[test]
fn identical_names_in_independent_sessions_receive_distinct_identities() {
    let directory = TestDirectory::new();
    let first = document(
        &WorkspaceSession::default()
            .execute(create_command())
            .unwrap(),
    );
    let second = document(
        &WorkspaceSession::default()
            .execute(create_command())
            .unwrap(),
    );
    assert_eq!(first.project.name, second.project.name);
    assert_eq!(
        first.project.modules[0].name,
        second.project.modules[0].name
    );
    assert_eq!(
        first.project.modules[0].areas[0].name,
        second.project.modules[0].areas[0].name
    );
    assert_ne!(first.project.id, second.project.id);
    assert_ne!(first.project.modules[0].id, second.project.modules[0].id);
    assert_ne!(
        first.project.modules[0].areas[0].id,
        second.project.modules[0].areas[0].id
    );
    assert_ne!(
        first.content_hash().unwrap(),
        second.content_hash().unwrap()
    );
    assert_eq!(
        first.project.modules[0].areas[0].terrain.seed,
        second.project.modules[0].areas[0].terrain.seed
    );
    assert!(directory.files().is_empty());
}

#[test]
fn create_cannot_replace_a_draft_or_a_reopened_saved_document() {
    let directory = TestDirectory::new();
    let mut session = WorkspaceSession::default();
    session.execute(create_command()).unwrap();
    assert_rejected_without_state_change(&mut session, create_command());
    let saved = save(&mut session, &directory);
    let path = PathBuf::from(saved.saved_path.unwrap());
    let before = directory.files();
    let mut reopened = WorkspaceSession::default();
    reopened.execute(open_command(&path)).unwrap();
    assert_rejected_without_state_change(&mut reopened, create_command());
    assert_eq!(directory.files(), before);
}

#[test]
fn undo_redo_advance_revisions_preserve_identities_and_never_delete_saved_files() {
    let directory = TestDirectory::new();
    let mut session = WorkspaceSession::default();
    let created = session.execute(create_command()).unwrap();
    let original = document(&created);
    let initial_receipt = receipt(&created);
    save(&mut session, &directory);
    let saved_files = directory.files();

    let undone = session
        .execute(WorkspaceCommand::Undo { base_revision: 1 })
        .unwrap();
    assert_eq!(undone.revision, 2);
    assert!(undone.document_json.is_none() && undone.saved_path.is_none());
    assert!(!undone.dirty && !undone.can_undo && undone.can_redo);
    let undo_receipt = receipt(&undone);
    assert_eq!(undo_receipt["action"], "undo_create");
    assert_eq!(undo_receipt["base_revision"], 1);
    assert_eq!(undo_receipt["new_revision"], 2);
    assert_eq!(undo_receipt["before_hash"], initial_receipt["after_hash"]);
    assert_eq!(undo_receipt["after_hash"], Value::Null);
    assert_eq!(undo_receipt["disk_files_modified"], false);
    assert_ne!(undo_receipt["command_id"], initial_receipt["command_id"]);
    assert_eq!(directory.files(), saved_files);

    let redone = session
        .execute(WorkspaceCommand::Redo { base_revision: 2 })
        .unwrap();
    assert_eq!(redone.revision, 3);
    let restored = document(&redone);
    let mut expected = original;
    expected.revision = forge_core::Revision::new(3);
    expected.project.modules[0].areas[0].terrain.revision = expected.revision;
    assert_eq!(restored, expected);
    assert!(redone.dirty && redone.can_undo && !redone.can_redo);
    assert!(redone.saved_path.is_none());
    let redo_receipt = receipt(&redone);
    assert_eq!(redo_receipt["action"], "redo_create");
    assert_eq!(redo_receipt["base_revision"], 2);
    assert_eq!(redo_receipt["new_revision"], 3);
    assert_eq!(redo_receipt["before_hash"], Value::Null);
    assert_eq!(
        redo_receipt["after_hash"],
        json!(restored.content_hash().unwrap())
    );
    assert_eq!(redo_receipt["disk_files_modified"], false);
    assert_ne!(redo_receipt["command_id"], undo_receipt["command_id"]);
    assert_eq!(directory.files(), saved_files);
    save(&mut session, &directory);
    let new_files = directory.files();
    assert_eq!(new_files.len(), 2);
    for (path, bytes) in saved_files {
        assert_eq!(new_files[&path], bytes);
    }
}

#[test]
fn save_then_open_in_a_fresh_session_preserves_exact_source_hash_seed_and_spawn() {
    let directory = TestDirectory::new();
    let mut session = WorkspaceSession::default();
    let created = session.execute(create_command()).unwrap();
    let source = document(&created);
    let saved = save(&mut session, &directory);
    assert!(!saved.dirty && saved.can_undo);
    let path = PathBuf::from(saved.saved_path.as_ref().unwrap());
    assert_eq!(
        fs::read_to_string(&path).unwrap(),
        created.document_json.unwrap()
    );
    let before = directory.files();
    let mut fresh = WorkspaceSession::default();
    assert!(current(&mut fresh).document_json.is_none());
    let opened = fresh.execute(open_command(&path)).unwrap();
    assert_eq!(opened.document_json, saved.document_json);
    assert_eq!(opened.saved_path, saved.saved_path);
    assert!(!opened.dirty && !opened.can_undo && !opened.can_redo);
    assert!(opened.last_receipt_json.is_none());
    let restored = document(&opened);
    assert_eq!(restored, source);
    assert_eq!(
        restored.content_hash().unwrap(),
        source.content_hash().unwrap()
    );
    assert_eq!(restored.project.modules[0].areas[0].terrain.seed, u64::MAX);
    assert_eq!(
        restored.project.modules[0].areas[0].spawn.y.to_bits(),
        17.5_f32.to_bits()
    );
    assert_eq!(directory.files(), before);
}

#[test]
fn saving_an_identical_snapshot_is_idempotent_without_pending_files() {
    let directory = TestDirectory::new();
    let mut session = WorkspaceSession::default();
    session.execute(create_command()).unwrap();
    let first = save(&mut session, &directory);
    let before = directory.files();
    assert_eq!(before.len(), 1);
    let second = save(&mut session, &directory);
    assert_eq!(first, second);
    assert_eq!(directory.files(), before);
    assert!(
        before
            .keys()
            .all(|name| name.to_str().unwrap().ends_with(".forge.json"))
    );
}

#[test]
fn conflicting_existing_snapshot_is_never_overwritten_or_marked_saved() {
    let directory = TestDirectory::new();
    let mut session = WorkspaceSession::default();
    let source = document(&session.execute(create_command()).unwrap());
    let mut conflict = source.clone();
    conflict.project.name = "Autre contenu préexistant".into();
    let filename = format!("{}-r1.forge.json", source.project.id);
    directory.write(&filename, conflict.to_pretty_json().unwrap());
    let before = directory.files();
    assert_rejected_without_state_change(
        &mut session,
        WorkspaceCommand::Save {
            directory: directory.path.to_str().unwrap().into(),
            base_revision: 1,
        },
    );
    assert_eq!(directory.files(), before);
    assert!(current(&mut session).dirty);
}

#[test]
fn relative_and_parent_traversal_paths_are_refused_for_save_and_open() {
    let directory = TestDirectory::new();
    let mut session = WorkspaceSession::default();
    session.execute(create_command()).unwrap();
    let saved = save(&mut session, &directory);
    let path = PathBuf::from(saved.saved_path.unwrap());
    let before = directory.files();
    // PathBuf::join normalizes '..' for Windows verbatim paths. Keep the raw input intact,
    // and prove the negative fixture is still traversal before passing it to the service.
    let parent_directory = PathBuf::from(format!("{}{MAIN_SEPARATOR}..", directory.path.display()));
    assert!(
        parent_directory
            .components()
            .any(|part| part == Component::ParentDir)
    );
    for invalid in [PathBuf::from("."), parent_directory] {
        assert_rejected_without_state_change(
            &mut session,
            WorkspaceCommand::Save {
                directory: invalid.to_str().unwrap().into(),
                base_revision: 1,
            },
        );
    }
    let parent_path = PathBuf::from(format!(
        "{}{MAIN_SEPARATOR}..{MAIN_SEPARATOR}{}{MAIN_SEPARATOR}{}",
        directory.path.display(),
        directory.path.file_name().unwrap().to_str().unwrap(),
        path.file_name().unwrap().to_str().unwrap()
    ));
    assert!(
        parent_path
            .components()
            .any(|part| part == Component::ParentDir)
    );
    let mut fresh = WorkspaceSession::default();
    for invalid in [PathBuf::from(path.file_name().unwrap()), parent_path] {
        assert_rejected_without_state_change(&mut fresh, open_command(&invalid));
    }
    assert_eq!(directory.files(), before);
}

#[test]
fn invalid_snapshot_extension_size_encoding_schema_and_json_are_refused() {
    let directory = TestDirectory::new();
    let source = document(
        &WorkspaceSession::default()
            .execute(create_command())
            .unwrap(),
    );
    let valid = source.to_pretty_json().unwrap();
    let mut unknown = serde_json::to_value(&source).unwrap();
    unknown["unexpected"] = json!(true);
    let mut nested_unknown = serde_json::to_value(&source).unwrap();
    nested_unknown["project"]["modules"][0]["areas"][0]["unexpected"] = json!(true);
    let mut future = serde_json::to_value(&source).unwrap();
    future["schema_version"] = json!(3);
    let mut oversized = valid.as_bytes().to_vec();
    oversized.resize(MAX_WORKSPACE_JSON_BYTES + 1, b' ');
    let fixtures = [
        ("wrong.json", valid.into_bytes()),
        ("broken.forge.json", b"{\"project\":".to_vec()),
        ("utf8.forge.json", vec![0xff, 0xfe]),
        ("oversized.forge.json", oversized),
        ("unknown.forge.json", unknown.to_string().into_bytes()),
        (
            "nested-unknown.forge.json",
            nested_unknown.to_string().into_bytes(),
        ),
        ("future.forge.json", future.to_string().into_bytes()),
    ];
    let mut fresh = WorkspaceSession::default();
    for (name, bytes) in fixtures {
        let path = directory.write(name, bytes);
        let before = directory.files();
        assert_rejected_without_state_change(&mut fresh, open_command(&path));
        assert_eq!(directory.files(), before);
    }
}

#[test]
fn dirty_project_is_preserved_when_opening_another_valid_snapshot_is_refused() {
    let directory = TestDirectory::new();
    let mut saved_session = WorkspaceSession::default();
    saved_session.execute(create_command()).unwrap();
    let saved = save(&mut saved_session, &directory);
    let path = PathBuf::from(saved.saved_path.unwrap());
    let before = directory.files();
    let mut dirty_session = WorkspaceSession::default();
    let draft = dirty_session.execute(create_command()).unwrap();
    assert!(draft.dirty);
    assert_rejected_without_state_change(&mut dirty_session, open_command(&path));
    assert_eq!(
        current(&mut dirty_session).document_json,
        draft.document_json
    );
    assert_eq!(directory.files(), before);
}

#[test]
fn orphan_pending_files_are_neither_automatically_opened_nor_deleted() {
    let directory = TestDirectory::new();
    let source = document(
        &WorkspaceSession::default()
            .execute(create_command())
            .unwrap(),
    );
    let valid = source.to_pretty_json().unwrap();
    let pending = directory.write(".forge-pending-valid-orphan", &valid);
    let partial = directory.write(".forge-pending-partial-orphan", b"{partial");
    let snapshot = directory.write("explicit.forge.json", &valid);
    let before = directory.files();
    let mut fresh = WorkspaceSession::default();
    let empty = current(&mut fresh);
    assert!(empty.document_json.is_none() && empty.saved_path.is_none());
    assert!(!empty.dirty && !empty.can_undo && !empty.can_redo);
    for path in [&pending, &partial] {
        assert_rejected_without_state_change(&mut fresh, open_command(path));
    }
    let opened = fresh.execute(open_command(&snapshot)).unwrap();
    assert_eq!(document(&opened), source);
    assert_eq!(directory.files(), before);
}

#[test]
fn empty_session_refuses_undo_redo_and_save_without_creating_files() {
    let directory = TestDirectory::new();
    let mut session = WorkspaceSession::default();
    for command in [
        WorkspaceCommand::Undo { base_revision: 0 },
        WorkspaceCommand::Redo { base_revision: 0 },
        WorkspaceCommand::Save {
            directory: directory.path.to_str().unwrap().into(),
            base_revision: 0,
        },
    ] {
        assert_rejected_without_state_change(&mut session, command);
    }
    assert!(directory.files().is_empty());
}

#[test]
fn stale_revision_refuses_undo_redo_and_save_without_session_or_disk_changes() {
    let directory = TestDirectory::new();
    let mut session = WorkspaceSession::default();
    session.execute(create_command()).unwrap();
    save(&mut session, &directory);
    let before = directory.files();
    for command in [
        WorkspaceCommand::Undo { base_revision: 0 },
        WorkspaceCommand::Save {
            directory: directory.path.to_str().unwrap().into(),
            base_revision: 0,
        },
    ] {
        assert_rejected_without_state_change(&mut session, command);
    }
    session
        .execute(WorkspaceCommand::Undo { base_revision: 1 })
        .unwrap();
    assert_rejected_without_state_change(&mut session, WorkspaceCommand::Redo { base_revision: 1 });
    let restored = session
        .execute(WorkspaceCommand::Redo { base_revision: 2 })
        .unwrap();
    assert_eq!(restored.revision, 3);
    for command in [
        WorkspaceCommand::Undo { base_revision: 1 },
        WorkspaceCommand::Save {
            directory: directory.path.to_str().unwrap().into(),
            base_revision: 1,
        },
    ] {
        assert_rejected_without_state_change(&mut session, command);
    }
    assert_eq!(directory.files(), before);
}

#[test]
fn close_refuses_dirty_draft_but_allows_create_after_closing_a_reopened_snapshot() {
    let directory = TestDirectory::new();
    let mut session = WorkspaceSession::default();
    let draft = session.execute(create_command()).unwrap();
    assert_rejected_without_state_change(&mut session, WorkspaceCommand::Close);
    let saved = save(&mut session, &directory);
    let path = PathBuf::from(saved.saved_path.unwrap());
    let before = directory.files();
    let mut fresh = WorkspaceSession::default();
    fresh.execute(open_command(&path)).unwrap();
    let closed = fresh.execute(WorkspaceCommand::Close).unwrap();
    assert!(closed.document_json.is_none() && closed.saved_path.is_none());
    assert!(closed.last_receipt_json.is_none());
    assert!(!closed.dirty && !closed.can_undo && !closed.can_redo);
    let new_draft = fresh.execute(create_command()).unwrap();
    assert!(new_draft.dirty);
    assert_ne!(document(&new_draft).project.id, document(&draft).project.id);
    assert_eq!(directory.files(), before);
}

#[test]
fn open_rejects_revisions_outside_ipc_integer_range_without_replacing_the_session() {
    let directory = TestDirectory::new();
    let mut session = WorkspaceSession::default();
    let source = document(&session.execute(create_command()).unwrap());
    save(&mut session, &directory);
    for revision in [MAX_WORKSPACE_IPC_REVISION + 1, u64::MAX] {
        let mut candidate = source.clone();
        candidate.revision = forge_core::Revision::new(revision);
        candidate.project.modules[0].areas[0].terrain.revision = candidate.revision;
        // These remain valid full-u64 domain documents; only the Studio transport is bounded.
        candidate.validate().unwrap();
        let path = directory.write(
            &format!("large-{revision}.forge.json"),
            candidate.to_pretty_json().unwrap(),
        );
        let before = directory.files();
        assert_rejected_without_state_change(&mut session, open_command(&path));
        assert_rejected_without_state_change(&mut WorkspaceSession::default(), open_command(&path));
        assert_eq!(directory.files(), before);
    }
    let mut boundary = source;
    boundary.revision = forge_core::Revision::new(MAX_WORKSPACE_IPC_REVISION);
    boundary.project.modules[0].areas[0].terrain.revision = boundary.revision;
    let path = directory.write("boundary.forge.json", boundary.to_pretty_json().unwrap());
    let opened = session.execute(open_command(&path)).unwrap();
    assert_eq!(opened.revision, MAX_WORKSPACE_IPC_REVISION);
    assert_eq!(document(&opened), boundary);
    assert!(!opened.dirty);
}

#[test]
fn redo_accepts_last_exact_ipc_revision_and_refuses_crossing_it_before_mutation() {
    let directory = TestDirectory::new();
    let source = document(
        &WorkspaceSession::default()
            .execute(create_command())
            .unwrap(),
    );
    // Direct boundary setup avoids quadrillions of undo/redo cycles; the tested operation is real.
    let mut session = WorkspaceSession {
        redo: vec![Some(source.clone())],
        last_revision: MAX_WORKSPACE_IPC_REVISION - 1,
        ..WorkspaceSession::default()
    };
    let restored = session
        .execute(WorkspaceCommand::Redo {
            base_revision: MAX_WORKSPACE_IPC_REVISION - 1,
        })
        .unwrap();
    assert_eq!(restored.revision, MAX_WORKSPACE_IPC_REVISION);
    assert_eq!(document(&restored).project.id, source.project.id);
    assert_eq!(
        receipt(&restored)["new_revision"],
        json!(MAX_WORKSPACE_IPC_REVISION)
    );
    assert_rejected_without_state_change(
        &mut session,
        WorkspaceCommand::Undo {
            base_revision: MAX_WORKSPACE_IPC_REVISION,
        },
    );

    let mut pending = WorkspaceSession {
        redo: vec![Some(source.clone())],
        last_revision: MAX_WORKSPACE_IPC_REVISION,
        ..WorkspaceSession::default()
    };
    assert_rejected_without_state_change(
        &mut pending,
        WorkspaceCommand::Redo {
            base_revision: MAX_WORKSPACE_IPC_REVISION,
        },
    );
    assert_eq!(pending.redo.last(), Some(&Some(source)));
    assert!(directory.files().is_empty());
}

#[test]
fn ipc_unsafe_command_revisions_are_refused_without_writing_or_changing_state() {
    let directory = TestDirectory::new();
    let mut session = WorkspaceSession::default();
    session.execute(create_command()).unwrap();
    for base_revision in [MAX_WORKSPACE_IPC_REVISION + 1, u64::MAX] {
        for command in [
            WorkspaceCommand::Undo { base_revision },
            WorkspaceCommand::Redo { base_revision },
            WorkspaceCommand::Save {
                directory: directory.path.to_str().unwrap().into(),
                base_revision,
            },
        ] {
            assert_rejected_without_state_change(&mut session, command);
        }
    }
    assert!(directory.files().is_empty());
}

#[test]
fn multiple_modules_areas_metadata_history_and_reopen_preserve_sources() {
    let directory = TestDirectory::new();
    let mut session = WorkspaceSession::default();
    let initial = session.execute(create_command()).unwrap();
    let source = document(&initial);
    let saved_initial = save(&mut session, &directory);
    let saved_bytes = directory.files();
    let added = session
        .execute(WorkspaceCommand::AddModule {
            base_revision: 1,
            name: "Deuxième chapitre".into(),
        })
        .unwrap();
    let module_id = document(&added).project.modules[1].id;
    assert_eq!(
        document(&added).project.modules[0],
        source.project.modules[0]
    );
    let added = session
        .execute(WorkspaceCommand::AddArea {
            base_revision: 2,
            module_id,
            name: "Montagne".into(),
            seed: u64::MAX.to_string(),
            spawn_x: "12".into(),
            spawn_y: "3.5".into(),
            spawn_z: "-21".into(),
        })
        .unwrap();
    let area_id = document(&added).project.modules[1].areas[0].id;
    let edited = session
        .execute(WorkspaceCommand::UpdateAreaMetadata {
            base_revision: 3,
            module_id,
            area_id,
            name: "Sommet".into(),
            spawn_x: "24".into(),
            spawn_y: "17.5".into(),
            spawn_z: "-36".into(),
        })
        .unwrap();
    let final_source = document(&edited);
    assert_eq!(final_source.project.modules[0], source.project.modules[0]);
    assert_eq!(
        final_source
            .area(module_id, area_id)
            .unwrap()
            .spawn
            .y
            .to_bits(),
        17.5_f32.to_bits()
    );
    let audit = receipt(&edited);
    assert_eq!(
        audit["command"]["change"]["operation"],
        "update_area_metadata"
    );
    assert_eq!(audit["command"]["base_revision"], 3);
    assert_eq!(
        audit["before_hash"],
        json!(document(&added).content_hash().unwrap())
    );
    assert_eq!(
        audit["after_hash"],
        json!(final_source.content_hash().unwrap())
    );
    assert_eq!(
        audit["inverse_snapshot"],
        serde_json::to_value(document(&added)).unwrap()
    );
    for base_revision in 4..7 {
        session
            .execute(WorkspaceCommand::Undo { base_revision })
            .unwrap();
    }
    let undone = document(&current(&mut session));
    assert_eq!(undone.project.modules.len(), 1);
    assert_eq!(
        undone.project.modules[0].areas[0].id,
        source.project.modules[0].areas[0].id
    );
    for base_revision in 7..10 {
        session
            .execute(WorkspaceCommand::Redo { base_revision })
            .unwrap();
    }
    let redone = document(&current(&mut session));
    let mut expected = final_source;
    super::stamp_revision(&mut expected, 10);
    assert_eq!(redone, expected);
    let saved = save(&mut session, &directory);
    assert_eq!(directory.files().len(), 2);
    for (name, bytes) in saved_bytes {
        assert_eq!(directory.files()[&name], bytes);
    }
    let fresh = WorkspaceSession::default()
        .execute(open_command(Path::new(saved.saved_path.as_ref().unwrap())))
        .unwrap();
    assert_eq!(fresh.document_json, saved.document_json);
    assert!(!fresh.can_undo && !fresh.can_redo && !fresh.dirty);
    assert_ne!(saved.saved_path, saved_initial.saved_path);
}

#[test]
fn invalid_or_stale_edits_preserve_document_history_and_files() {
    let directory = TestDirectory::new();
    let mut session = WorkspaceSession::default();
    let created = session.execute(create_command()).unwrap();
    let source = document(&created);
    let module_id = source.project.modules[0].id;
    let area_id = source.project.modules[0].areas[0].id;
    save(&mut session, &directory);
    let before = directory.files();
    for command in [
        WorkspaceCommand::AddModule {
            base_revision: 0,
            name: "Stale".into(),
        },
        WorkspaceCommand::AddModule {
            base_revision: 1,
            name: "\n".into(),
        },
        WorkspaceCommand::RenameModule {
            base_revision: 1,
            module_id: forge_core::ModuleId::deterministic("missing"),
            name: "Nom".into(),
        },
        WorkspaceCommand::UpdateAreaMetadata {
            base_revision: 1,
            module_id,
            area_id,
            name: "Nom".into(),
            spawn_x: "NaN".into(),
            spawn_y: "0".into(),
            spawn_z: "0".into(),
        },
        WorkspaceCommand::AddArea {
            base_revision: 1,
            module_id,
            name: "Nom".into(),
            seed: "-1".into(),
            spawn_x: "0".into(),
            spawn_y: "0".into(),
            spawn_z: "0".into(),
        },
        WorkspaceCommand::AddArea {
            base_revision: 1,
            module_id,
            name: "Nom".into(),
            seed: "0".into(),
            spawn_x: "257".into(),
            spawn_y: "0".into(),
            spawn_z: "0".into(),
        },
        WorkspaceCommand::RenameProject {
            base_revision: MAX_WORKSPACE_IPC_REVISION + 1,
            name: "Nom".into(),
        },
    ] {
        assert_rejected_without_state_change(&mut session, command);
        assert_eq!(directory.files(), before);
    }
    session
        .execute(WorkspaceCommand::RenameProject {
            base_revision: 1,
            name: "Nom différent".into(),
        })
        .unwrap();
    session
        .execute(WorkspaceCommand::Undo { base_revision: 2 })
        .unwrap();
    assert!(current(&mut session).can_redo);
    assert_rejected_without_state_change(
        &mut session,
        WorkspaceCommand::AddModule {
            base_revision: 3,
            name: String::new(),
        },
    );
    session
        .execute(WorkspaceCommand::AddModule {
            base_revision: 3,
            name: "Nouvelle branche".into(),
        })
        .unwrap();
    assert!(!current(&mut session).can_redo);
}

#[test]
fn history_is_bounded_and_new_creation_does_not_reuse_a_session_revision() {
    let mut session = WorkspaceSession::default();
    session.execute(create_command()).unwrap();
    session
        .execute(WorkspaceCommand::Undo { base_revision: 1 })
        .unwrap();
    assert_eq!(session.execute(create_command()).unwrap().revision, 3);
    for i in 0..40 {
        let revision = current(&mut session).revision;
        session
            .execute(WorkspaceCommand::RenameProject {
                base_revision: revision,
                name: format!("Projet {i}"),
            })
            .unwrap();
    }
    assert_eq!(session.undo.len(), super::WORKSPACE_HISTORY_LIMIT);
    for _ in 0..super::WORKSPACE_HISTORY_LIMIT {
        let revision = current(&mut session).revision;
        session
            .execute(WorkspaceCommand::Undo {
                base_revision: revision,
            })
            .unwrap();
        assert_eq!(
            session.undo.len() + session.redo.len(),
            super::WORKSPACE_HISTORY_LIMIT
        );
    }
    let state = current(&mut session);
    assert!(!state.can_undo && state.can_redo);
    assert_eq!(document(&state).project.name, "Projet 7");
    assert_rejected_without_state_change(
        &mut session,
        WorkspaceCommand::Undo {
            base_revision: state.revision,
        },
    );
}

#[test]
fn opening_v1_migrates_in_memory_and_never_rewrites_the_old_snapshot() {
    let directory = TestDirectory::new();
    let source = document(
        &WorkspaceSession::default()
            .execute(create_command())
            .unwrap(),
    );
    let mut legacy = serde_json::to_value(&source).unwrap();
    legacy["schema_version"] = json!(1);
    let mut module = legacy["project"]["modules"][0].clone();
    let area = module["areas"][0].clone();
    module.as_object_mut().unwrap().remove("areas");
    module["area"] = area;
    legacy["project"].as_object_mut().unwrap().remove("modules");
    legacy["project"]["module"] = module;
    let path = directory.write(
        &format!("{}-r1.forge.json", source.project.id),
        legacy.to_string(),
    );
    let bytes = fs::read(&path).unwrap();
    let mut session = WorkspaceSession::default();
    let opened = session.execute(open_command(&path)).unwrap();
    assert_eq!(document(&opened), source);
    assert!(!opened.dirty);
    save(&mut session, &directory); // Existing v1 content is semantically identical and stays intact.
    assert_eq!(fs::read(&path).unwrap(), bytes);
    session
        .execute(WorkspaceCommand::AddModule {
            base_revision: 1,
            name: "Après migration".into(),
        })
        .unwrap();
    let saved = save(&mut session, &directory);
    assert_eq!(fs::read(&path).unwrap(), bytes);
    assert_eq!(directory.files().len(), 2);
    let fresh = WorkspaceSession::default()
        .execute(open_command(Path::new(saved.saved_path.as_ref().unwrap())))
        .unwrap();
    assert_eq!(fresh.document_json, saved.document_json);
    assert_eq!(document(&fresh).schema_version, 2);
}
