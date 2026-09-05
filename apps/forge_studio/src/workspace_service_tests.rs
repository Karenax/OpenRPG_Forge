//! Real local-filesystem service tests, not crash/power-loss or concurrent-writer qualification.

use super::WorkspaceSession;
use crate::workspace_api::{
    MAX_WORKSPACE_IPC_REVISION, VegetationDensityStrokePreviewRequest, VegetationExclusionDraft,
    WorkspaceCommand, WorkspaceState,
};
use forge_project::{
    AuthoredRiver, AuthoredRoad, MAX_WORKSPACE_JSON_BYTES, PathId, RiverChannelProfile,
    RiverChannelProfileAlgorithm, RiverSurface, RoadProfile, RoadProfileAlgorithm, RoadSurface,
    VegetationExclusion, VegetationExclusionId, VegetationLayerId, VegetationSpeciesPreset,
    WORKSPACE_SCHEMA_VERSION, WorkspaceDocument,
};
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

fn area_request(
    state: &WorkspaceState,
    module_index: usize,
    area_index: usize,
) -> crate::area_preview::AreaPreviewRequest {
    let source = document(state);
    let module = &source.project.modules[module_index];
    let area = &module.areas[area_index];
    crate::area_preview::AreaPreviewRequest {
        key: crate::area_tabs::AreaKey {
            project_id: source.project.id,
            module_id: module.id,
            area_id: area.id,
        },
        base_revision: state.revision,
    }
}

fn create_path_command(state: &WorkspaceState, trace_xz_m: Vec<[f32; 2]>) -> WorkspaceCommand {
    WorkspaceCommand::CreatePath {
        request: area_request(state, 0, 0),
        name: String::new(),
        trace_xz_m,
    }
}

fn redraw_path_command(
    state: &WorkspaceState,
    path_id: PathId,
    trace_xz_m: Vec<[f32; 2]>,
) -> WorkspaceCommand {
    WorkspaceCommand::RedrawPath {
        request: area_request(state, 0, 0),
        path_id,
        trace_xz_m,
    }
}

fn upsert_road_command(
    state: &WorkspaceState,
    path_id: PathId,
    width_m: f32,
    crown_height_m: f32,
) -> WorkspaceCommand {
    WorkspaceCommand::UpsertRoad {
        request: area_request(state, 0, 0),
        road: authored_road(path_id, width_m, crown_height_m),
    }
}

fn authored_road(path_id: PathId, width_m: f32, crown_height_m: f32) -> AuthoredRoad {
    AuthoredRoad {
        path_id,
        profile: RoadProfile {
            algorithm: RoadProfileAlgorithm::TerrainDrapedCrownV1,
            width_m,
            crown_height_m,
        },
        surface: RoadSurface::CompactedEarthV1,
    }
}

fn upsert_river_command(
    state: &WorkspaceState,
    path_id: PathId,
    surface_width_m: f32,
    bed_depth_m: f32,
    bank_blend_width_m: f32,
) -> WorkspaceCommand {
    WorkspaceCommand::UpsertRiver {
        request: area_request(state, 0, 0),
        river: authored_river(path_id, surface_width_m, bed_depth_m, bank_blend_width_m),
    }
}

fn authored_river(
    path_id: PathId,
    surface_width_m: f32,
    bed_depth_m: f32,
    bank_blend_width_m: f32,
) -> AuthoredRiver {
    AuthoredRiver {
        path_id,
        channel: RiverChannelProfile {
            algorithm: RiverChannelProfileAlgorithm::TerrainCarvedChannelV1,
            surface_width_m,
            bed_depth_m,
            bank_blend_width_m,
        },
        surface: RiverSurface::ForgeWaterV1,
    }
}

fn first_vegetation_promotion_target(
    state: &WorkspaceState,
) -> forge_world::VegetationPromotionTarget {
    let source = document(state);
    let area = &source.project.modules[0].areas[0];
    let effective_terrain = forge_world::RiverGenerationPlan::from_documents(
        &area.terrain,
        &area.paths,
        &area.roads,
        &area.rivers,
    )
    .unwrap()
    .generate()
    .unwrap()
    .effective_terrain;
    let generated = forge_world::VegetationGenerationPlan::from_documents_with_density(
        &effective_terrain,
        &area.vegetation,
        &area.vegetation_density,
    )
    .unwrap()
    .generate()
    .unwrap();
    let instance = generated
        .chunks
        .iter()
        .flat_map(|chunk| &chunk.instances)
        .next()
        .expect("default workspace generates vegetation");
    forge_world::VegetationPromotionTarget {
        schema_version: forge_world::VEGETATION_PROMOTION_SCHEMA_VERSION,
        center_x_m: instance.position_m.x,
        center_z_m: instance.position_m.z,
        maximum_distance_m: 0.25,
        layer_id: instance.layer_id.clone(),
    }
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
        additional_centers: vec![],
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

fn material_brush_command(state: &WorkspaceState, dry_run: bool) -> WorkspaceCommand {
    let source = document(state);
    WorkspaceCommand::PaintTerrainMaterial {
        additional_centers: vec![[8.0, 0.0], [16.0, 0.0]],
        request: crate::area_preview::AreaPreviewRequest {
            key: crate::area_tabs::AreaKey {
                project_id: source.project.id,
                module_id: source.project.modules[0].id,
                area_id: source.project.modules[0].areas[0].id,
            },
            base_revision: state.revision,
        },
        brush: forge_world::TerrainMaterialBrush {
            schema_version: 1,
            center_x_m: 0.0,
            center_z_m: 0.0,
            radius_m: 12.0,
            strength: 0.75,
            material: forge_project::TerrainMaterialRole::Earth,
        },
        dry_run,
    }
}

fn vegetation_density_brush_command(state: &WorkspaceState, dry_run: bool) -> WorkspaceCommand {
    let source = document(state);
    let area = &source.project.modules[0].areas[0];
    let layer_id = area
        .vegetation
        .layers
        .iter()
        .find(|layer| layer.kind == forge_project::VegetationKind::Grass)
        .unwrap()
        .id
        .clone();
    WorkspaceCommand::PaintVegetationDensity {
        additional_centers: vec![[16.0, 0.0], [32.0, 0.0]],
        request: crate::area_preview::AreaPreviewRequest {
            key: crate::area_tabs::AreaKey {
                project_id: source.project.id,
                module_id: source.project.modules[0].id,
                area_id: area.id,
            },
            base_revision: state.revision,
        },
        brush: forge_world::VegetationDensityBrush {
            schema_version: 1,
            center_x_m: 0.0,
            center_z_m: 0.0,
            radius_m: 20.0,
            strength: 0.75,
            target_density: u8::MAX,
            layer_id,
        },
        dry_run,
    }
}

fn vegetation_density_preview_request(
    state: &WorkspaceState,
    preview_serial: u64,
) -> VegetationDensityStrokePreviewRequest {
    let WorkspaceCommand::PaintVegetationDensity {
        request,
        brush,
        additional_centers,
        ..
    } = vegetation_density_brush_command(state, false)
    else {
        unreachable!();
    };
    VegetationDensityStrokePreviewRequest {
        request,
        brush,
        additional_centers,
        preview_serial,
        base_source_hash: None,
    }
}

fn resolve_density_preview(
    session: &WorkspaceSession,
    request: &VegetationDensityStrokePreviewRequest,
) -> Result<super::VegetationDensityStrokePreviewSource, String> {
    session
        .vegetation_density_stroke_preview_input(request.clone())?
        .resolve()
        .map(|(_, source)| source)
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
    assert_eq!(
        document(&undone).project.modules[0].areas[0]
            .roads
            .revision
            .get(),
        undone.revision
    );
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
            .roads
            .revision
            .get(),
        redone.revision
    );
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
fn material_brush_dry_run_apply_history_and_reopen_preserve_exact_sources() {
    let directory = TestDirectory::new();
    let mut session = WorkspaceSession::default();
    session.execute(create_command()).unwrap();
    let before = save(&mut session, &directory);
    let old_files = directory.files();

    let ipc_command = material_brush_command(&before, true);
    assert_eq!(
        serde_json::from_str::<WorkspaceCommand>(&serde_json::to_string(&ipc_command).unwrap())
            .unwrap(),
        ipc_command
    );

    let preview = session
        .execute(material_brush_command(&before, true))
        .unwrap();
    let unchanged = current(&mut session);
    assert_eq!(unchanged.document_json, before.document_json);
    assert_eq!(unchanged.revision, before.revision);
    let predicted: forge_world::TerrainMaterialBrushResult =
        serde_json::from_str(preview.last_receipt_json.as_deref().unwrap()).unwrap();
    assert!(predicted.changed_samples > 1);

    let applied = session
        .execute(material_brush_command(&before, false))
        .unwrap();
    let accepted: forge_world::TerrainMaterialBrushResult =
        serde_json::from_str(applied.last_receipt_json.as_deref().unwrap()).unwrap();
    assert_eq!(accepted.edit.document, predicted.edit.document);
    let modified = document(&applied).project.modules[0].areas[0]
        .terrain
        .material_weight_overrides
        .clone();
    assert!(!modified.is_empty());
    assert!(modified.iter().all(|sample| {
        sample.weights.iter().all(|weight| weight.is_finite())
            && (sample.weights.iter().sum::<f32>() - 1.0).abs() <= 0.000_01
    }));

    let undone = session
        .execute(WorkspaceCommand::Undo {
            base_revision: applied.revision,
        })
        .unwrap();
    assert!(
        document(&undone).project.modules[0].areas[0]
            .terrain
            .material_weight_overrides
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
            .material_weight_overrides,
        modified
    );
    assert_eq!(directory.files(), old_files);

    let saved = save(&mut session, &directory);
    let mut reopened = WorkspaceSession::default();
    let opened = reopened
        .execute(open_command(Path::new(
            saved.saved_path.as_deref().unwrap(),
        )))
        .unwrap();
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
fn vegetation_density_dry_run_matches_the_canonical_apply() {
    let mut session = WorkspaceSession::default();
    let created = session.execute(create_command()).unwrap();
    let source = document(&created);
    let module_id = source.project.modules[0].id;
    let before = session
        .execute(WorkspaceCommand::AddArea {
            base_revision: created.revision,
            module_id,
            name: "Zone témoin".into(),
            seed: "9".into(),
            spawn_x: "0".into(),
            spawn_y: "0".into(),
            spawn_z: "0".into(),
        })
        .unwrap();
    let before_document = document(&before);
    let other_area = before_document.project.modules[0].areas[1].clone();

    let ipc_command = vegetation_density_brush_command(&before, true);
    assert_eq!(
        serde_json::from_str::<WorkspaceCommand>(&serde_json::to_string(&ipc_command).unwrap())
            .unwrap(),
        ipc_command
    );
    let preview = session
        .execute(vegetation_density_brush_command(&before, true))
        .unwrap();
    let unchanged = current(&mut session);
    assert_eq!(unchanged.document_json, before.document_json);
    assert_eq!(unchanged.revision, before.revision);
    assert_eq!(unchanged.last_receipt_json, before.last_receipt_json);
    assert_eq!(
        (unchanged.can_undo, unchanged.can_redo, unchanged.dirty),
        (before.can_undo, before.can_redo, before.dirty)
    );
    let predicted: forge_world::VegetationDensityBrushResult =
        serde_json::from_str(preview.last_receipt_json.as_deref().unwrap()).unwrap();
    assert!(predicted.changed_cells > 1);

    let applied = session
        .execute(vegetation_density_brush_command(&before, false))
        .unwrap();
    let accepted: forge_world::VegetationDensityBrushResult =
        serde_json::from_str(applied.last_receipt_json.as_deref().unwrap()).unwrap();
    assert_eq!(applied.revision, before.revision + 1);
    assert_eq!(accepted.edit.document, predicted.edit.document);
    assert_eq!(
        accepted.edit.receipt.after_hash,
        predicted.edit.receipt.after_hash
    );
    assert_eq!(
        accepted.edit.receipt.inverse_snapshot.as_ref(),
        &before_document
    );
    let applied_document = document(&applied);
    assert_eq!(applied_document.project.modules[0].areas[1], other_area);
    let layer_id = accepted.command.brush.layer_id.clone();
    let density_layer = applied_document.project.modules[0].areas[0]
        .vegetation_density
        .layers
        .iter()
        .find(|layer| layer.layer_id == layer_id)
        .unwrap();
    let modified = density_layer.overrides.clone();
    assert!(!modified.is_empty());
    assert!(
        modified
            .iter()
            .all(|entry| entry.density() != density_layer.default_density)
    );
    assert!(modified.windows(2).all(|pair| {
        (pair[0].cell_z(), pair[0].cell_x()) < (pair[1].cell_z(), pair[1].cell_x())
    }));
}

#[test]
fn density_stroke_preview_candidate_matches_canonical_apply_and_leaves_session_bit_exact() {
    let directory = TestDirectory::new();
    let mut session = WorkspaceSession::default();
    session.execute(create_command()).unwrap();
    save(&mut session, &directory);
    let before = current(&mut session);
    let files_before = directory.files();
    let undo_before = session.undo.clone();
    let redo_before = session.redo.clone();
    let saved_hash_before = session.saved_hash;
    let saved_path_before = session.saved_path.clone();
    let receipt_before = session.receipt.clone();
    let request = vegetation_density_preview_request(&before, 41);

    let candidate = resolve_density_preview(&session, &request).unwrap();
    assert!(!candidate.no_op);
    assert!(candidate.changed_cells > 0);
    assert_eq!(candidate.candidate_revision, before.revision + 1);
    assert_eq!(current(&mut session), before);
    assert_eq!(session.undo, undo_before);
    assert_eq!(session.redo, redo_before);
    assert_eq!(session.saved_hash, saved_hash_before);
    assert_eq!(session.saved_path, saved_path_before);
    assert_eq!(session.receipt, receipt_before);
    assert_eq!(directory.files(), files_before);

    let applied = session
        .execute(WorkspaceCommand::PaintVegetationDensity {
            request: request.request,
            brush: request.brush,
            additional_centers: request.additional_centers,
            dry_run: false,
        })
        .unwrap();
    let applied_document = document(&applied);
    let applied_area = applied_document
        .area(request.request.key.module_id, request.request.key.area_id)
        .unwrap();
    assert_eq!(&candidate.area, applied_area);
    assert_eq!(
        candidate.candidate_density_source_hash,
        applied_area.vegetation_density.content_hash().unwrap()
    );
}

#[test]
fn successive_density_previews_share_the_canonical_base_and_noop_is_explicit() {
    let mut session = WorkspaceSession::default();
    session.execute(create_command()).unwrap();
    let before = current(&mut session);
    let before_document = document(&before);
    let mut first = vegetation_density_preview_request(&before, 1);
    first.additional_centers.clear();
    first.brush.center_x_m = -96.0;
    first.brush.center_z_m = 0.0;
    first.brush.strength = 1.0;
    let mut second = vegetation_density_preview_request(&before, 2);
    second.additional_centers.clear();
    second.brush.center_x_m = 96.0;
    second.brush.center_z_m = 0.0;
    second.brush.strength = 1.0;

    let first_candidate = resolve_density_preview(&session, &first).unwrap();
    let second_candidate = resolve_density_preview(&session, &second).unwrap();
    let expected_second = forge_world::preview_vegetation_density_stroke(
        &before_document,
        forge_world::VegetationDensityBrushCommand {
            command_id: forge_core::CommandId::deterministic("expected-second-preview"),
            base_revision: forge_core::Revision::new(before.revision),
            project_id: second.request.key.project_id,
            module_id: second.request.key.module_id,
            area_id: second.request.key.area_id,
            brush: second.brush.clone(),
        },
        &second.additional_centers,
    )
    .unwrap();
    let expected_area = expected_second
        .edit
        .document
        .area(second.request.key.module_id, second.request.key.area_id)
        .unwrap();
    assert_ne!(first_candidate.area, second_candidate.area);
    assert_eq!(&second_candidate.area, expected_area);
    assert_eq!(current(&mut session), before);

    let mut no_op = vegetation_density_preview_request(&before, 3);
    no_op.additional_centers.clear();
    no_op.brush.target_density = 0;
    let no_op_candidate = resolve_density_preview(&session, &no_op).unwrap();
    assert!(no_op_candidate.no_op);
    assert_eq!(no_op_candidate.changed_cells, 0);
    assert_eq!(no_op_candidate.candidate_revision, before.revision);
    assert_eq!(
        no_op_candidate.area,
        before_document.project.modules[0].areas[0]
    );
    assert_eq!(current(&mut session), before);
}

#[test]
fn rejected_density_previews_never_touch_session_history_dirty_state_or_disk() {
    let directory = TestDirectory::new();
    let mut session = WorkspaceSession::default();
    session.execute(create_command()).unwrap();
    save(&mut session, &directory);
    let before = current(&mut session);
    let files_before = directory.files();
    let undo_before = session.undo.clone();
    let redo_before = session.redo.clone();
    let saved_hash_before = session.saved_hash;
    let saved_path_before = session.saved_path.clone();
    let receipt_before = session.receipt.clone();

    let mut stale = vegetation_density_preview_request(&before, 1);
    stale.request.base_revision -= 1;
    let mut wrong_area = vegetation_density_preview_request(&before, 2);
    wrong_area.request.key.area_id = forge_core::AreaId::deterministic("missing-area");
    let mut invalid = vegetation_density_preview_request(&before, 3);
    invalid.brush.radius_m = 1.0;
    let mut too_many = vegetation_density_preview_request(&before, 4);
    too_many.additional_centers = vec![[0.0, 0.0]; 64];
    let mut unbounded_serial = vegetation_density_preview_request(&before, 5);
    unbounded_serial.preview_serial = MAX_WORKSPACE_IPC_REVISION + 1;

    for rejected in [stale, wrong_area, invalid, too_many, unbounded_serial] {
        assert!(resolve_density_preview(&session, &rejected).is_err());
        assert_eq!(current(&mut session), before);
        assert_eq!(session.undo, undo_before);
        assert_eq!(session.redo, redo_before);
        assert_eq!(session.saved_hash, saved_hash_before);
        assert_eq!(session.saved_path, saved_path_before);
        assert_eq!(session.receipt, receipt_before);
        assert_eq!(directory.files(), files_before);
    }
}

#[test]
fn vegetation_density_history_and_reopen_preserve_exact_sources() {
    let directory = TestDirectory::new();
    let mut session = WorkspaceSession::default();
    let created = session.execute(create_command()).unwrap();
    let source = document(&created);
    let module_id = source.project.modules[0].id;
    session
        .execute(WorkspaceCommand::AddArea {
            base_revision: created.revision,
            module_id,
            name: "Zone témoin".into(),
            seed: "9".into(),
            spawn_x: "0".into(),
            spawn_y: "0".into(),
            spawn_z: "0".into(),
        })
        .unwrap();
    let before = save(&mut session, &directory);
    let before_document = document(&before);
    let other_area = before_document.project.modules[0].areas[1].clone();
    let old_files = directory.files();

    let applied = session
        .execute(vegetation_density_brush_command(&before, false))
        .unwrap();
    let accepted: forge_world::VegetationDensityBrushResult =
        serde_json::from_str(applied.last_receipt_json.as_deref().unwrap()).unwrap();
    let layer_id = accepted.command.brush.layer_id;
    let modified = document(&applied).project.modules[0].areas[0]
        .vegetation_density
        .layers
        .iter()
        .find(|layer| layer.layer_id == layer_id)
        .unwrap()
        .overrides
        .clone();

    let undone = session
        .execute(WorkspaceCommand::Undo {
            base_revision: applied.revision,
        })
        .unwrap();
    assert!(
        document(&undone).project.modules[0].areas[0]
            .vegetation_density
            .layers
            .iter()
            .find(|layer| layer.layer_id == layer_id)
            .unwrap()
            .overrides
            .is_empty()
    );
    let redone = session
        .execute(WorkspaceCommand::Redo {
            base_revision: undone.revision,
        })
        .unwrap();
    assert_eq!(
        document(&redone).project.modules[0].areas[0]
            .vegetation_density
            .layers
            .iter()
            .find(|layer| layer.layer_id == layer_id)
            .unwrap()
            .overrides,
        modified
    );
    let redone_document = document(&redone);
    let mut expected_other_area = other_area;
    expected_other_area.terrain.revision = redone_document.revision;
    expected_other_area.vegetation.revision = redone_document.revision;
    expected_other_area.vegetation_density.revision = redone_document.revision;
    expected_other_area.paths.revision = redone_document.revision;
    expected_other_area.roads.revision = redone_document.revision;
    expected_other_area.rivers.revision = redone_document.revision;
    assert_eq!(
        redone_document.project.modules[0].areas[1],
        expected_other_area
    );
    assert_eq!(directory.files(), old_files);

    let saved = save(&mut session, &directory);
    let mut reopened = WorkspaceSession::default();
    let opened = reopened
        .execute(open_command(Path::new(
            saved.saved_path.as_deref().unwrap(),
        )))
        .unwrap();
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
fn material_brush_ipc_round_trip_invalid_and_stale_requests_do_not_touch_history() {
    let mut session = WorkspaceSession::default();
    let created = session.execute(create_command()).unwrap();
    let command = material_brush_command(&created, false);
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
    assert!(
        serde_json::from_str::<WorkspaceCommand>(
            &json.replace("\"material\":\"earth\"", "\"material\":\"unknown\"")
        )
        .is_err()
    );
    session.execute(command.clone()).unwrap();
    assert_rejected_without_state_change(&mut session, command);
    for mutation in 0..6 {
        let mut command = material_brush_command(&current(&mut session), false);
        if let WorkspaceCommand::PaintTerrainMaterial {
            request,
            brush,
            additional_centers,
            ..
        } = &mut command
        {
            match mutation {
                0 => {
                    request.key.project_id =
                        forge_core::ProjectId::deterministic("foreign-project");
                }
                1 => brush.radius_m = 1000.0,
                2 => brush.center_x_m = f32::NAN,
                3 => brush.strength = 0.0,
                4 => *additional_centers = vec![[f32::NAN, 0.0]],
                _ => request.base_revision = MAX_WORKSPACE_IPC_REVISION + 1,
            }
        }
        assert_rejected_without_state_change(&mut session, command);
    }
}

#[test]
fn vegetation_density_ipc_invalid_noop_and_stale_requests_do_not_touch_history() {
    let mut session = WorkspaceSession::default();
    let created = session.execute(create_command()).unwrap();
    let command = vegetation_density_brush_command(&created, false);
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
    assert!(
        serde_json::from_str::<WorkspaceCommand>(
            &json.replace("\"target_density\":255", "\"target_density\":256")
        )
        .is_err()
    );

    let mut no_op = command.clone();
    let WorkspaceCommand::PaintVegetationDensity { brush, .. } = &mut no_op else {
        unreachable!();
    };
    brush.target_density = 0;
    assert_rejected_without_state_change(&mut session, no_op);

    for mutation in 0..7 {
        let mut invalid = vegetation_density_brush_command(&current(&mut session), false);
        let WorkspaceCommand::PaintVegetationDensity {
            request,
            brush,
            additional_centers,
            ..
        } = &mut invalid
        else {
            unreachable!();
        };
        match mutation {
            0 => {
                request.key.project_id =
                    forge_core::ProjectId::deterministic("foreign-density-project");
            }
            1 => brush.radius_m = 1_000.0,
            2 => brush.center_x_m = f32::NAN,
            3 => brush.strength = 0.0,
            4 => *additional_centers = vec![[f32::NAN, 0.0]],
            5 => brush.layer_id = forge_project::VegetationLayerId::new("unknown_layer"),
            _ => request.base_revision = MAX_WORKSPACE_IPC_REVISION + 1,
        }
        assert_rejected_without_state_change(&mut session, invalid);
    }

    session.execute(command.clone()).unwrap();
    assert_rejected_without_state_change(&mut session, command);
}

#[test]
fn continuous_brush_is_one_history_entry_and_all_modes_round_trip_over_ipc() {
    use forge_world::TerrainBrushOperation;
    let mut session = WorkspaceSession::default();
    let created = session.execute(create_command()).unwrap();
    for operation in [
        TerrainBrushOperation::Smooth,
        TerrainBrushOperation::Flatten { height_m: 0.0 },
        TerrainBrushOperation::Noise {
            amplitude_m: 4.0,
            seed: u32::MAX,
        },
        TerrainBrushOperation::Slope {
            height_m: 4.0,
            gradient_x: 0.25,
            gradient_z: -0.5,
        },
    ] {
        let mut request = brush_command(&created, false);
        if let WorkspaceCommand::SculptTerrain { brush, .. } = &mut request {
            brush.operation = operation;
        }
        assert_eq!(
            serde_json::from_str::<WorkspaceCommand>(&serde_json::to_string(&request).unwrap())
                .unwrap(),
            request
        );
    }
    let mut request = brush_command(&created, false);
    if let WorkspaceCommand::SculptTerrain {
        additional_centers, ..
    } = &mut request
    {
        *additional_centers = vec![[12.0, 0.0], [24.0, 0.0]];
    }
    let applied = session.execute(request).unwrap();
    assert_eq!(applied.revision, created.revision + 1);
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
        document(&applied).project.modules[0].areas[0]
            .terrain
            .height_deltas
    );
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
    super::stamp_revision(&mut expected, 3);
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
    future["schema_version"] = json!(source.schema_version + 1);
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
        super::stamp_revision(&mut candidate, revision);
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
    super::stamp_revision(&mut boundary, MAX_WORKSPACE_IPC_REVISION);
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
    let typed_audit: forge_project::WorkspaceEditReceipt =
        serde_json::from_str(edited.last_receipt_json.as_deref().unwrap()).unwrap();
    assert_eq!(typed_audit.inverse_snapshot.as_ref(), &document(&added));
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
#[allow(
    clippy::too_many_lines,
    reason = "the end-to-end catalogue test keeps history, persistence and ID reuse in one scenario"
)]
fn vegetation_catalog_crud_reuses_free_ids_and_survives_history_and_reopen() {
    let directory = TestDirectory::new();
    let mut session = WorkspaceSession::default();
    let created = session.execute(create_command()).unwrap();

    let first = session
        .execute(WorkspaceCommand::AddVegetationSpecies {
            request: area_request(&created, 0, 0),
            preset: VegetationSpeciesPreset::Tree,
            display_name: "Arbre clair".into(),
        })
        .unwrap();
    let first_document = document(&first);
    let first_area = &first_document.project.modules[0].areas[0];
    assert_eq!(first.revision, 2);
    assert_eq!(
        first_area.vegetation.layers.last().unwrap().id.as_str(),
        "species_01"
    );
    assert_eq!(
        first_area
            .vegetation_density
            .layers
            .last()
            .unwrap()
            .layer_id
            .as_str(),
        "species_01"
    );
    assert_eq!(
        first_area
            .vegetation_density
            .layers
            .last()
            .unwrap()
            .default_density,
        0
    );
    assert!(
        first_area
            .vegetation_density
            .layers
            .last()
            .unwrap()
            .overrides
            .is_empty()
    );

    let second = session
        .execute(WorkspaceCommand::AddVegetationSpecies {
            request: area_request(&first, 0, 0),
            preset: VegetationSpeciesPreset::Fern,
            display_name: "Fougère claire".into(),
        })
        .unwrap();
    assert_eq!(
        document(&second).project.modules[0].areas[0]
            .vegetation
            .layers
            .last()
            .unwrap()
            .id
            .as_str(),
        "species_02"
    );

    let removed = session
        .execute(WorkspaceCommand::RemoveVegetationSpecies {
            request: area_request(&second, 0, 0),
            layer_id: VegetationLayerId::new("species_01"),
        })
        .unwrap();
    let reused = session
        .execute(WorkspaceCommand::AddVegetationSpecies {
            request: area_request(&removed, 0, 0),
            preset: VegetationSpeciesPreset::Shrub,
            display_name: "Arbuste haut".into(),
        })
        .unwrap();
    let reused_document = document(&reused);
    let reused_area = &reused_document.project.modules[0].areas[0];
    assert_eq!(
        reused_area.vegetation.layers.last().unwrap().id.as_str(),
        "species_01"
    );
    assert!(
        reused_area
            .vegetation_density
            .layers
            .iter()
            .any(|layer| layer.layer_id.as_str() == "species_01")
    );

    let renamed = session
        .execute(WorkspaceCommand::RenameVegetationSpecies {
            request: area_request(&reused, 0, 0),
            layer_id: VegetationLayerId::new("species_02"),
            display_name: "Fougère des ombres".into(),
        })
        .unwrap();
    let renamed_document = document(&renamed);
    let renamed_area = &renamed_document.project.modules[0].areas[0];
    assert_eq!(
        renamed_area
            .vegetation
            .layers
            .iter()
            .find(|layer| layer.id.as_str() == "species_02")
            .unwrap()
            .display_name,
        "Fougère des ombres"
    );
    assert_eq!(
        receipt(&renamed)["command"]["change"]["operation"],
        "set_vegetation_catalog"
    );

    let final_state = session
        .execute(WorkspaceCommand::RemoveVegetationSpecies {
            request: area_request(&renamed, 0, 0),
            layer_id: VegetationLayerId::new("species_01"),
        })
        .unwrap();
    let undone = session
        .execute(WorkspaceCommand::Undo {
            base_revision: final_state.revision,
        })
        .unwrap();
    assert!(
        document(&undone).project.modules[0].areas[0]
            .vegetation
            .layers
            .iter()
            .any(|layer| layer.id.as_str() == "species_01")
    );
    let redone = session
        .execute(WorkspaceCommand::Redo {
            base_revision: undone.revision,
        })
        .unwrap();
    assert!(
        document(&redone).project.modules[0].areas[0]
            .vegetation
            .layers
            .iter()
            .all(|layer| layer.id.as_str() != "species_01")
    );

    let saved = save(&mut session, &directory);
    let reopened = WorkspaceSession::default()
        .execute(open_command(Path::new(saved.saved_path.as_ref().unwrap())))
        .unwrap();
    assert_eq!(reopened.document_json, saved.document_json);
    assert!(!reopened.dirty && !reopened.can_undo && !reopened.can_redo);
}

#[test]
fn vegetation_catalog_reserves_the_layer_id_of_an_orphan_promotion() {
    let mut session = WorkspaceSession::default();
    let created = session.execute(create_command()).unwrap();
    let added = session
        .execute(WorkspaceCommand::AddVegetationSpecies {
            request: area_request(&created, 0, 0),
            preset: VegetationSpeciesPreset::Tree,
            display_name: "Arbre à conserver".into(),
        })
        .unwrap();
    let reserved_layer_id = VegetationLayerId::new("species_01");
    let painted = session
        .execute(WorkspaceCommand::PaintVegetationDensity {
            request: area_request(&added, 0, 0),
            brush: forge_world::VegetationDensityBrush {
                schema_version: 1,
                center_x_m: 0.0,
                center_z_m: 0.0,
                radius_m: 64.0,
                strength: 1.0,
                target_density: u8::MAX,
                layer_id: reserved_layer_id.clone(),
            },
            additional_centers: vec![],
            dry_run: false,
        })
        .unwrap();
    let target = {
        let source = document(&painted);
        let area = &source.project.modules[0].areas[0];
        let generated = forge_world::VegetationGenerationPlan::from_documents_with_density(
            &area.terrain,
            &area.vegetation,
            &area.vegetation_density,
        )
        .unwrap()
        .generate()
        .unwrap();
        let instance = generated
            .chunks
            .iter()
            .flat_map(|chunk| &chunk.instances)
            .find(|instance| instance.layer_id == reserved_layer_id)
            .expect("the painted species must produce a promotable instance");
        forge_world::VegetationPromotionTarget {
            schema_version: forge_world::VEGETATION_PROMOTION_SCHEMA_VERSION,
            center_x_m: instance.position_m.x,
            center_z_m: instance.position_m.z,
            maximum_distance_m: 0.25,
            layer_id: reserved_layer_id.clone(),
        }
    };
    let promoted = session
        .execute(WorkspaceCommand::PromoteVegetationInstance {
            request: area_request(&painted, 0, 0),
            target,
        })
        .unwrap();
    let removed = session
        .execute(WorkspaceCommand::RemoveVegetationSpecies {
            request: area_request(&promoted, 0, 0),
            layer_id: reserved_layer_id.clone(),
        })
        .unwrap();
    let removed_document = document(&removed);
    let removed_vegetation = &removed_document.project.modules[0].areas[0].vegetation;
    assert!(
        removed_vegetation
            .layers
            .iter()
            .all(|layer| layer.id != reserved_layer_id)
    );
    assert_eq!(
        removed_vegetation.promoted_instances[0].source.layer_id,
        reserved_layer_id
    );

    let added_again = session
        .execute(WorkspaceCommand::AddVegetationSpecies {
            request: area_request(&removed, 0, 0),
            preset: VegetationSpeciesPreset::Shrub,
            display_name: "Arbuste indépendant".into(),
        })
        .unwrap();
    let added_again_document = document(&added_again);
    let vegetation = &added_again_document.project.modules[0].areas[0].vegetation;
    assert_eq!(vegetation.layers.last().unwrap().id.as_str(), "species_02");
    assert_eq!(
        vegetation.promoted_instances[0].source.layer_id,
        reserved_layer_id
    );
}

#[test]
fn vegetation_exclusion_circle_and_corridor_are_crud_and_reuse_free_ids() {
    let directory = TestDirectory::new();
    let mut session = WorkspaceSession::default();
    let created = session.execute(create_command()).unwrap();
    let circle = session
        .execute(WorkspaceCommand::AddVegetationExclusion {
            request: area_request(&created, 0, 0),
            draft: VegetationExclusionDraft::Circle {
                center_x_m: 12.0,
                center_z_m: -18.0,
                radius_m: 9.0,
            },
        })
        .unwrap();
    assert!(matches!(
        &document(&circle).project.modules[0].areas[0].vegetation.exclusions[0],
        VegetationExclusion::Circle { id, center_x_m, center_z_m, radius_m }
            if id.as_str() == "exclusion_01"
                && center_x_m.to_bits() == 12.0_f32.to_bits()
                && center_z_m.to_bits() == (-18.0_f32).to_bits()
                && radius_m.to_bits() == 9.0_f32.to_bits()
    ));

    let corridor = session
        .execute(WorkspaceCommand::AddVegetationExclusion {
            request: area_request(&circle, 0, 0),
            draft: VegetationExclusionDraft::CorridorSegment {
                start_x_m: -40.0,
                start_z_m: -5.0,
                end_x_m: 44.0,
                end_z_m: 6.0,
                half_width_m: 3.0,
            },
        })
        .unwrap();
    assert!(matches!(
        &document(&corridor).project.modules[0].areas[0].vegetation.exclusions[1],
        VegetationExclusion::CorridorSegment { id, start_x_m, end_x_m, half_width_m, .. }
            if id.as_str() == "exclusion_02"
                && start_x_m.to_bits() == (-40.0_f32).to_bits()
                && end_x_m.to_bits() == 44.0_f32.to_bits()
                && half_width_m.to_bits() == 3.0_f32.to_bits()
    ));

    let removed = session
        .execute(WorkspaceCommand::RemoveVegetationExclusion {
            request: area_request(&corridor, 0, 0),
            exclusion_id: VegetationExclusionId::new("exclusion_01"),
        })
        .unwrap();
    let reused = session
        .execute(WorkspaceCommand::AddVegetationExclusion {
            request: area_request(&removed, 0, 0),
            draft: VegetationExclusionDraft::Circle {
                center_x_m: 0.0,
                center_z_m: 0.0,
                radius_m: 4.0,
            },
        })
        .unwrap();
    let reused_document = document(&reused);
    let exclusions = &reused_document.project.modules[0].areas[0]
        .vegetation
        .exclusions;
    assert_eq!(exclusions.len(), 2);
    assert_eq!(exclusions[0].id().as_str(), "exclusion_02");
    assert_eq!(exclusions[1].id().as_str(), "exclusion_01");
    assert_eq!(
        receipt(&reused)["command"]["change"]["operation"],
        "set_vegetation_exclusions"
    );
    let undone = session
        .execute(WorkspaceCommand::Undo {
            base_revision: reused.revision,
        })
        .unwrap();
    assert_eq!(
        document(&undone).project.modules[0].areas[0]
            .vegetation
            .exclusions
            .len(),
        1
    );
    let redone = session
        .execute(WorkspaceCommand::Redo {
            base_revision: undone.revision,
        })
        .unwrap();
    assert_eq!(
        document(&redone).project.modules[0].areas[0]
            .vegetation
            .exclusions
            .len(),
        2
    );
    let saved = save(&mut session, &directory);
    let reopened = WorkspaceSession::default()
        .execute(open_command(Path::new(saved.saved_path.as_ref().unwrap())))
        .unwrap();
    assert_eq!(reopened.document_json, saved.document_json);
}

#[test]
#[allow(
    clippy::too_many_lines,
    reason = "the atomic rejection matrix verifies every vegetation command against one unchanged session"
)]
fn invalid_stale_or_wrong_area_vegetation_edits_are_atomic_and_target_one_area() {
    let directory = TestDirectory::new();
    let mut session = WorkspaceSession::default();
    let created = session.execute(create_command()).unwrap();
    let source = document(&created);
    let module_id = source.project.modules[0].id;
    let added_area = session
        .execute(WorkspaceCommand::AddArea {
            base_revision: created.revision,
            module_id,
            name: "Zone témoin".into(),
            seed: "19".into(),
            spawn_x: "0".into(),
            spawn_y: "2".into(),
            spawn_z: "0".into(),
        })
        .unwrap();
    let untouched_area = document(&added_area).project.modules[0].areas[1].clone();
    save(&mut session, &directory);
    let files_before = directory.files();
    let valid_request = area_request(&added_area, 0, 0);
    let mut stale_request = valid_request;
    stale_request.base_revision -= 1;
    let mut wrong_project_request = valid_request;
    wrong_project_request.key.project_id = forge_core::ProjectId::deterministic("wrong-project");

    for command in [
        WorkspaceCommand::AddVegetationSpecies {
            request: stale_request,
            preset: VegetationSpeciesPreset::Tree,
            display_name: "Périmé".into(),
        },
        WorkspaceCommand::AddVegetationSpecies {
            request: wrong_project_request,
            preset: VegetationSpeciesPreset::Tree,
            display_name: "Autre projet".into(),
        },
        WorkspaceCommand::AddVegetationSpecies {
            request: valid_request,
            preset: VegetationSpeciesPreset::Tree,
            display_name: "\n".into(),
        },
        WorkspaceCommand::AddVegetationSpecies {
            request: valid_request,
            preset: VegetationSpeciesPreset::Grass,
            display_name: "Herbe supplémentaire hors budget".into(),
        },
        WorkspaceCommand::AddVegetationExclusion {
            request: valid_request,
            draft: VegetationExclusionDraft::Circle {
                center_x_m: 0.0,
                center_z_m: 0.0,
                radius_m: 0.0,
            },
        },
        WorkspaceCommand::AddVegetationExclusion {
            request: valid_request,
            draft: VegetationExclusionDraft::CorridorSegment {
                start_x_m: 1.0,
                start_z_m: 1.0,
                end_x_m: 1.0,
                end_z_m: 1.0,
                half_width_m: 2.0,
            },
        },
        WorkspaceCommand::RemoveVegetationSpecies {
            request: valid_request,
            layer_id: VegetationLayerId::new("missing_species"),
        },
        WorkspaceCommand::RemoveVegetationExclusion {
            request: valid_request,
            exclusion_id: VegetationExclusionId::new("missing_exclusion"),
        },
    ] {
        assert_rejected_without_state_change(&mut session, command);
        assert_eq!(directory.files(), files_before);
    }

    let added_species = session
        .execute(WorkspaceCommand::AddVegetationSpecies {
            request: valid_request,
            preset: VegetationSpeciesPreset::Shrub,
            display_name: "Arbuste de lisière".into(),
        })
        .unwrap();
    assert_eq!(
        document(&added_species).project.modules[0].areas[1],
        untouched_area
    );
    let added_exclusion = session
        .execute(WorkspaceCommand::AddVegetationExclusion {
            request: area_request(&added_species, 0, 0),
            draft: VegetationExclusionDraft::Circle {
                center_x_m: 0.0,
                center_z_m: 0.0,
                radius_m: 6.0,
            },
        })
        .unwrap();
    assert_eq!(
        document(&added_exclusion).project.modules[0].areas[1],
        untouched_area
    );
    assert_eq!(directory.files(), files_before);
}

#[test]
#[allow(
    clippy::too_many_lines,
    reason = "the promotion flow proves one-entry history, immutable provenance and exact persistence together"
)]
fn vegetation_promotion_and_retouch_are_one_revision_each_and_survive_history_and_reopen() {
    let directory = TestDirectory::new();
    let mut session = WorkspaceSession::default();
    let created = session.execute(create_command()).unwrap();
    let painted = session
        .execute(vegetation_density_brush_command(&created, false))
        .unwrap();
    let target = first_vegetation_promotion_target(&painted);
    let history_before_promotion = session.undo.len();

    let promoted = session
        .execute(WorkspaceCommand::PromoteVegetationInstance {
            request: area_request(&painted, 0, 0),
            target,
        })
        .unwrap();
    assert_eq!(promoted.revision, painted.revision + 1);
    assert_eq!(session.undo.len(), history_before_promotion + 1);
    let promoted_document = document(&promoted);
    let promoted_instance = promoted_document.project.modules[0].areas[0]
        .vegetation
        .promoted_instances[0]
        .clone();
    assert_eq!(
        promoted_instance.authored_transform,
        promoted_instance.source.transform
    );
    assert_eq!(
        promoted_instance.source.workspace_revision.get(),
        painted.revision
    );
    assert_eq!(
        receipt(&promoted)["edit"]["receipt"]["command"]["change"]["operation"],
        "set_vegetation_promoted_instances"
    );

    let mut authored_transform = promoted_instance.authored_transform;
    authored_transform.position_m.y += 1.25;
    let history_before_retouch = session.undo.len();
    let retouched = session
        .execute(WorkspaceCommand::RetouchPromotedVegetationInstance {
            request: area_request(&promoted, 0, 0),
            instance_id: promoted_instance.id,
            authored_transform,
        })
        .unwrap();
    assert_eq!(retouched.revision, promoted.revision + 1);
    assert_eq!(session.undo.len(), history_before_retouch + 1);
    let retouched_instance = document(&retouched).project.modules[0].areas[0]
        .vegetation
        .promoted_instances[0]
        .clone();
    assert_eq!(retouched_instance.id, promoted_instance.id);
    assert_eq!(retouched_instance.source, promoted_instance.source);
    assert_eq!(retouched_instance.authored_transform, authored_transform);
    assert_eq!(
        receipt(&retouched)["edit"]["receipt"]["command"]["change"]["operation"],
        "set_vegetation_promoted_instances"
    );

    let undone = session
        .execute(WorkspaceCommand::Undo {
            base_revision: retouched.revision,
        })
        .unwrap();
    let undone_instance = document(&undone).project.modules[0].areas[0]
        .vegetation
        .promoted_instances[0]
        .clone();
    assert_eq!(undone_instance.source, promoted_instance.source);
    assert_eq!(
        undone_instance.authored_transform,
        promoted_instance.authored_transform
    );
    let redone = session
        .execute(WorkspaceCommand::Redo {
            base_revision: undone.revision,
        })
        .unwrap();
    let redone_instance = document(&redone).project.modules[0].areas[0]
        .vegetation
        .promoted_instances[0]
        .clone();
    assert_eq!(redone_instance.source, promoted_instance.source);
    assert_eq!(redone_instance.authored_transform, authored_transform);

    let saved = save(&mut session, &directory);
    let reopened = WorkspaceSession::default()
        .execute(open_command(Path::new(saved.saved_path.as_ref().unwrap())))
        .unwrap();
    assert_eq!(reopened.document_json, saved.document_json);
    assert!(!reopened.dirty && !reopened.can_undo && !reopened.can_redo);
}

#[test]
fn vegetation_promotion_and_retouch_rejections_are_atomic() {
    let directory = TestDirectory::new();
    let mut session = WorkspaceSession::default();
    let created = session.execute(create_command()).unwrap();
    let painted = session
        .execute(vegetation_density_brush_command(&created, false))
        .unwrap();
    let target = first_vegetation_promotion_target(&painted);
    let promoted = session
        .execute(WorkspaceCommand::PromoteVegetationInstance {
            request: area_request(&painted, 0, 0),
            target: target.clone(),
        })
        .unwrap();
    let promoted_instance = document(&promoted).project.modules[0].areas[0]
        .vegetation
        .promoted_instances[0]
        .clone();
    let saved = save(&mut session, &directory);
    let files_before = directory.files();
    let valid_request = area_request(&saved, 0, 0);
    let mut stale_request = valid_request;
    stale_request.base_revision -= 1;
    let mut wrong_area_request = valid_request;
    wrong_area_request.key.area_id = forge_core::AreaId::deterministic("missing-area");

    for command in [
        WorkspaceCommand::PromoteVegetationInstance {
            request: stale_request,
            target: target.clone(),
        },
        WorkspaceCommand::PromoteVegetationInstance {
            request: wrong_area_request,
            target: target.clone(),
        },
        WorkspaceCommand::PromoteVegetationInstance {
            request: valid_request,
            target: target.clone(),
        },
        WorkspaceCommand::RetouchPromotedVegetationInstance {
            request: stale_request,
            instance_id: promoted_instance.id,
            authored_transform: promoted_instance.authored_transform,
        },
        WorkspaceCommand::RetouchPromotedVegetationInstance {
            request: wrong_area_request,
            instance_id: promoted_instance.id,
            authored_transform: promoted_instance.authored_transform,
        },
        WorkspaceCommand::RetouchPromotedVegetationInstance {
            request: valid_request,
            instance_id: forge_core::EntityId::deterministic("missing-promoted-instance"),
            authored_transform: promoted_instance.authored_transform,
        },
        WorkspaceCommand::RetouchPromotedVegetationInstance {
            request: valid_request,
            instance_id: promoted_instance.id,
            authored_transform: promoted_instance.authored_transform,
        },
    ] {
        assert_rejected_without_state_change(&mut session, command);
        assert_eq!(directory.files(), files_before);
    }
}

#[test]
fn path_create_and_redraw_are_one_revision_each_and_survive_history_and_reopen() {
    let directory = TestDirectory::new();
    let mut session = WorkspaceSession::default();
    let created = session.execute(create_command()).unwrap();
    let history_before_create = session.undo.len();
    let first_trace = vec![[-24.0, -12.0], [0.0, 8.0], [24.0, 12.0]];

    let with_path = session
        .execute(create_path_command(&created, first_trace.clone()))
        .unwrap();
    assert_eq!(with_path.revision, created.revision + 1);
    assert_eq!(session.undo.len(), history_before_create + 1);
    let path = document(&with_path).project.modules[0].areas[0].paths.paths[0].clone();
    assert!(!path.id.as_entity_id().as_uuid().is_nil());
    assert_eq!(path.name, "Chemin 1");
    assert_eq!(
        path.control_points
            .iter()
            .map(|point| [point.position_m.x, point.position_m.z])
            .collect::<Vec<_>>(),
        first_trace
    );
    assert_eq!(
        receipt(&with_path)["edit"]["receipt"]["command"]["change"]["operation"],
        "upsert_path"
    );

    let redraw_trace = vec![[-32.0, 18.0], [-4.0, -2.0], [28.0, -18.0]];
    let history_before_redraw = session.undo.len();
    let redrawn = session
        .execute(redraw_path_command(
            &with_path,
            path.id,
            redraw_trace.clone(),
        ))
        .unwrap();
    assert_eq!(redrawn.revision, with_path.revision + 1);
    assert_eq!(session.undo.len(), history_before_redraw + 1);
    let redrawn_path = document(&redrawn).project.modules[0].areas[0].paths.paths[0].clone();
    assert_eq!(redrawn_path.id, path.id);
    assert_eq!(redrawn_path.name, path.name);
    assert_eq!(
        redrawn_path
            .control_points
            .iter()
            .map(|point| [point.position_m.x, point.position_m.z])
            .collect::<Vec<_>>(),
        redraw_trace
    );

    let undone = session
        .execute(WorkspaceCommand::Undo {
            base_revision: redrawn.revision,
        })
        .unwrap();
    let undone_path = document(&undone).project.modules[0].areas[0].paths.paths[0].clone();
    assert_eq!(undone_path.id, path.id);
    assert_eq!(undone_path.control_points, path.control_points);
    let redone = session
        .execute(WorkspaceCommand::Redo {
            base_revision: undone.revision,
        })
        .unwrap();
    let redone_path = document(&redone).project.modules[0].areas[0].paths.paths[0].clone();
    assert_eq!(redone_path.id, path.id);
    assert_eq!(redone_path.control_points, redrawn_path.control_points);

    let saved = save(&mut session, &directory);
    let reopened = WorkspaceSession::default()
        .execute(open_command(Path::new(saved.saved_path.as_ref().unwrap())))
        .unwrap();
    assert_eq!(reopened.document_json, saved.document_json);
    assert!(!reopened.dirty && !reopened.can_undo && !reopened.can_redo);
}

#[test]
fn path_create_and_redraw_rejections_are_atomic() {
    let directory = TestDirectory::new();
    let mut session = WorkspaceSession::default();
    let created = session.execute(create_command()).unwrap();
    let with_path = session
        .execute(create_path_command(
            &created,
            vec![[-16.0, 0.0], [16.0, 0.0]],
        ))
        .unwrap();
    let path_id = document(&with_path).project.modules[0].areas[0].paths.paths[0].id;
    let saved = save(&mut session, &directory);
    let files_before = directory.files();
    let valid_request = area_request(&saved, 0, 0);
    let mut stale_request = valid_request;
    stale_request.base_revision -= 1;
    let mut wrong_area_request = valid_request;
    wrong_area_request.key.area_id = forge_core::AreaId::deterministic("missing-path-area");
    let mut wrong_project_request = valid_request;
    wrong_project_request.key.project_id =
        forge_core::ProjectId::deterministic("wrong-path-project");

    for command in [
        WorkspaceCommand::CreatePath {
            request: stale_request,
            name: String::new(),
            trace_xz_m: vec![[0.0, 0.0], [8.0, 0.0]],
        },
        WorkspaceCommand::CreatePath {
            request: wrong_project_request,
            name: String::new(),
            trace_xz_m: vec![[0.0, 0.0], [8.0, 0.0]],
        },
        WorkspaceCommand::CreatePath {
            request: valid_request,
            name: "\n".into(),
            trace_xz_m: vec![[0.0, 0.0], [8.0, 0.0]],
        },
        WorkspaceCommand::CreatePath {
            request: valid_request,
            name: String::new(),
            trace_xz_m: vec![[0.0, 0.0]],
        },
        WorkspaceCommand::RedrawPath {
            request: wrong_area_request,
            path_id,
            trace_xz_m: vec![[0.0, 0.0], [8.0, 0.0]],
        },
        WorkspaceCommand::RedrawPath {
            request: valid_request,
            path_id: PathId::deterministic("missing-path"),
            trace_xz_m: vec![[0.0, 0.0], [8.0, 0.0]],
        },
        WorkspaceCommand::RedrawPath {
            request: valid_request,
            path_id,
            trace_xz_m: vec![[-16.0, 0.0], [16.0, 0.0]],
        },
    ] {
        assert_rejected_without_state_change(&mut session, command);
        assert_eq!(directory.files(), files_before);
    }
}

#[test]
fn road_profile_is_one_revision_and_survives_redraw_history_and_reopen() {
    let directory = TestDirectory::new();
    let mut session = WorkspaceSession::default();
    let created = session.execute(create_command()).unwrap();
    let with_path = session
        .execute(create_path_command(
            &created,
            vec![[-24.0, -8.0], [0.0, 4.0], [24.0, -8.0]],
        ))
        .unwrap();
    let before = document(&with_path);
    let path = before.project.modules[0].areas[0].paths.paths[0].clone();
    let path_hash = before.project.modules[0].areas[0]
        .paths
        .content_hash()
        .unwrap();
    let history_before = session.undo.len();

    let with_road = session
        .execute(upsert_road_command(&with_path, path.id, 4.5, 0.09))
        .unwrap();
    assert_eq!(with_road.revision, with_path.revision + 1);
    assert_eq!(session.undo.len(), history_before + 1);
    let road_document = document(&with_road);
    let area = &road_document.project.modules[0].areas[0];
    assert_eq!(area.paths.content_hash().unwrap(), path_hash);
    assert_eq!(area.paths.paths[0], path);
    assert_eq!(area.roads.roads, vec![authored_road(path.id, 4.5, 0.09)]);
    assert_eq!(area.roads.revision.get(), with_road.revision);
    assert_eq!(
        receipt(&with_road)["command"]["road"]["path_id"],
        json!(path.id)
    );

    let updated = session
        .execute(upsert_road_command(&with_road, path.id, 6.0, 0.12))
        .unwrap();
    assert_eq!(updated.revision, with_road.revision + 1);
    assert_eq!(
        document(&updated).project.modules[0].areas[0].roads.roads,
        vec![authored_road(path.id, 6.0, 0.12)]
    );

    let undone = session
        .execute(WorkspaceCommand::Undo {
            base_revision: updated.revision,
        })
        .unwrap();
    assert_eq!(
        document(&undone).project.modules[0].areas[0].roads.roads,
        vec![authored_road(path.id, 4.5, 0.09)]
    );
    let redone = session
        .execute(WorkspaceCommand::Redo {
            base_revision: undone.revision,
        })
        .unwrap();
    assert_eq!(
        document(&redone).project.modules[0].areas[0].roads.roads,
        vec![authored_road(path.id, 6.0, 0.12)]
    );

    let redrawn = session
        .execute(redraw_path_command(
            &redone,
            path.id,
            vec![[-20.0, -12.0], [0.0, 8.0], [20.0, -12.0]],
        ))
        .unwrap();
    let redrawn_document = document(&redrawn);
    let redrawn_area = &redrawn_document.project.modules[0].areas[0];
    assert_eq!(
        redrawn_area.roads.roads,
        vec![authored_road(path.id, 6.0, 0.12)]
    );
    forge_world::RoadGenerationPlan::from_documents(
        &redrawn_area.terrain,
        &redrawn_area.paths,
        &redrawn_area.roads,
    )
    .unwrap()
    .generate()
    .unwrap();

    let saved = save(&mut session, &directory);
    let reopened = WorkspaceSession::default()
        .execute(open_command(Path::new(saved.saved_path.as_ref().unwrap())))
        .unwrap();
    assert_eq!(reopened.document_json, saved.document_json);
    assert!(!reopened.dirty && !reopened.can_undo && !reopened.can_redo);
}

#[test]
fn road_profile_rejections_are_atomic() {
    let directory = TestDirectory::new();
    let mut session = WorkspaceSession::default();
    let created = session.execute(create_command()).unwrap();
    let with_path = session
        .execute(create_path_command(
            &created,
            vec![[-16.0, 0.0], [16.0, 0.0]],
        ))
        .unwrap();
    let path_id = document(&with_path).project.modules[0].areas[0].paths.paths[0].id;
    let with_road = session
        .execute(upsert_road_command(&with_path, path_id, 4.0, 0.08))
        .unwrap();
    let saved = save(&mut session, &directory);
    let files_before = directory.files();
    let valid_request = area_request(&saved, 0, 0);
    let mut stale_request = valid_request;
    stale_request.base_revision -= 1;
    let mut wrong_project_request = valid_request;
    wrong_project_request.key.project_id =
        forge_core::ProjectId::deterministic("wrong-road-project");

    for command in [
        WorkspaceCommand::UpsertRoad {
            request: stale_request,
            road: authored_road(path_id, 5.0, 0.10),
        },
        WorkspaceCommand::UpsertRoad {
            request: wrong_project_request,
            road: authored_road(path_id, 5.0, 0.10),
        },
        WorkspaceCommand::UpsertRoad {
            request: valid_request,
            road: authored_road(PathId::deterministic("missing-road-path"), 4.0, 0.08),
        },
        WorkspaceCommand::UpsertRoad {
            request: valid_request,
            road: authored_road(path_id, 0.5, 0.08),
        },
        WorkspaceCommand::UpsertRoad {
            request: valid_request,
            road: authored_road(path_id, 4.0, 0.25),
        },
        upsert_road_command(&with_road, path_id, 4.0, 0.08),
    ] {
        assert_rejected_without_state_change(&mut session, command);
        assert_eq!(directory.files(), files_before);
    }
}

#[test]
#[allow(
    clippy::too_many_lines,
    reason = "one river lifecycle proves revision, history, redraw, generation and persistence together"
)]
fn river_profile_is_one_revision_and_survives_redraw_history_and_reopen() {
    let directory = TestDirectory::new();
    let mut session = WorkspaceSession::default();
    let created = session.execute(create_command()).unwrap();
    let with_path = session
        .execute(create_path_command(
            &created,
            vec![[-24.0, -8.0], [0.0, 4.0], [24.0, -8.0]],
        ))
        .unwrap();
    let before = document(&with_path);
    let area_before = &before.project.modules[0].areas[0];
    let path = area_before.paths.paths[0].clone();
    let terrain_hash = area_before.terrain.content_hash().unwrap();
    let path_hash = area_before.paths.content_hash().unwrap();
    let history_before = session.undo.len();

    let with_river = session
        .execute(upsert_river_command(&with_path, path.id, 8.0, 1.5, 4.0))
        .unwrap();
    assert_eq!(with_river.revision, with_path.revision + 1);
    assert_eq!(session.undo.len(), history_before + 1);
    let river_document = document(&with_river);
    let area = &river_document.project.modules[0].areas[0];
    assert_eq!(area.terrain.content_hash().unwrap(), terrain_hash);
    assert_eq!(area.paths.content_hash().unwrap(), path_hash);
    assert_eq!(area.paths.paths[0], path);
    assert_eq!(
        area.rivers.rivers,
        vec![authored_river(path.id, 8.0, 1.5, 4.0)]
    );
    assert_eq!(area.rivers.revision.get(), with_river.revision);
    assert_eq!(
        receipt(&with_river)["command"]["river"]["path_id"],
        json!(path.id)
    );
    let generated = forge_world::RiverGenerationPlan::from_documents(
        &area.terrain,
        &area.paths,
        &area.roads,
        &area.rivers,
    )
    .unwrap()
    .generate()
    .unwrap();
    assert_eq!(generated.generated.rivers.len(), 1);
    assert_ne!(
        generated.effective_terrain.content_hash().unwrap(),
        terrain_hash
    );

    let updated = session
        .execute(upsert_river_command(&with_river, path.id, 10.0, 2.0, 5.0))
        .unwrap();
    assert_eq!(updated.revision, with_river.revision + 1);
    assert_eq!(
        document(&updated).project.modules[0].areas[0].rivers.rivers,
        vec![authored_river(path.id, 10.0, 2.0, 5.0)]
    );

    let undone = session
        .execute(WorkspaceCommand::Undo {
            base_revision: updated.revision,
        })
        .unwrap();
    assert_eq!(
        document(&undone).project.modules[0].areas[0].rivers.rivers,
        vec![authored_river(path.id, 8.0, 1.5, 4.0)]
    );
    let redone = session
        .execute(WorkspaceCommand::Redo {
            base_revision: undone.revision,
        })
        .unwrap();
    assert_eq!(
        document(&redone).project.modules[0].areas[0].rivers.rivers,
        vec![authored_river(path.id, 10.0, 2.0, 5.0)]
    );

    let redrawn = session
        .execute(redraw_path_command(
            &redone,
            path.id,
            vec![[-20.0, -12.0], [0.0, 8.0], [20.0, -12.0]],
        ))
        .unwrap();
    let redrawn_document = document(&redrawn);
    let redrawn_area = &redrawn_document.project.modules[0].areas[0];
    assert_eq!(
        redrawn_area.rivers.rivers,
        vec![authored_river(path.id, 10.0, 2.0, 5.0)]
    );
    forge_world::RiverGenerationPlan::from_documents(
        &redrawn_area.terrain,
        &redrawn_area.paths,
        &redrawn_area.roads,
        &redrawn_area.rivers,
    )
    .unwrap()
    .generate()
    .unwrap();

    let saved = save(&mut session, &directory);
    let reopened = WorkspaceSession::default()
        .execute(open_command(Path::new(saved.saved_path.as_ref().unwrap())))
        .unwrap();
    assert_eq!(reopened.document_json, saved.document_json);
    assert!(!reopened.dirty && !reopened.can_undo && !reopened.can_redo);
}

#[test]
fn vegetation_promotion_with_a_river_uses_the_visible_effective_projection() {
    let directory = TestDirectory::new();
    let mut session = WorkspaceSession::default();
    let created = session.execute(create_command()).unwrap();
    let with_path = session
        .execute(create_path_command(
            &created,
            vec![[-24.0, -8.0], [0.0, 4.0], [24.0, -8.0]],
        ))
        .unwrap();
    let path_id = document(&with_path).project.modules[0].areas[0].paths.paths[0].id;
    let with_river = session
        .execute(upsert_river_command(&with_path, path_id, 8.0, 1.5, 4.0))
        .unwrap();
    let painted = session
        .execute(vegetation_density_brush_command(&with_river, false))
        .unwrap();
    let source = document(&painted);
    let area = &source.project.modules[0].areas[0];
    let canonical_terrain_hash = area.terrain.content_hash().unwrap();
    let effective_terrain = forge_world::RiverGenerationPlan::from_documents(
        &area.terrain,
        &area.paths,
        &area.roads,
        &area.rivers,
    )
    .unwrap()
    .generate()
    .unwrap()
    .effective_terrain;
    let visible = forge_world::VegetationGenerationPlan::from_documents_with_density(
        &effective_terrain,
        &area.vegetation,
        &area.vegetation_density,
    )
    .unwrap()
    .generate()
    .unwrap()
    .chunks
    .iter()
    .flat_map(|chunk| &chunk.instances)
    .next()
    .expect("the effective river terrain keeps visible vegetation")
    .clone();
    let expected_transform = forge_project::VegetationInstanceTransform::new(
        visible.position_m,
        visible.yaw_radians,
        visible.scale,
    );
    let target = forge_world::VegetationPromotionTarget {
        schema_version: forge_world::VEGETATION_PROMOTION_SCHEMA_VERSION,
        center_x_m: visible.position_m.x,
        center_z_m: visible.position_m.z,
        maximum_distance_m: 0.25,
        layer_id: visible.layer_id,
    };

    let promoted = session
        .execute(WorkspaceCommand::PromoteVegetationInstance {
            request: area_request(&painted, 0, 0),
            target,
        })
        .unwrap();
    assert_eq!(promoted.revision, painted.revision + 1);
    let promoted_document = document(&promoted);
    let promoted_instance = &promoted_document.project.modules[0].areas[0]
        .vegetation
        .promoted_instances[0];
    assert_eq!(promoted_instance.id, visible.id.as_entity_id());
    assert_eq!(promoted_instance.source.transform, expected_transform);
    assert_eq!(promoted_instance.authored_transform, expected_transform);
    assert_eq!(
        promoted_instance.source.terrain_document_hash,
        canonical_terrain_hash
    );
    assert_eq!(
        promoted_instance.source.workspace_revision.get(),
        painted.revision
    );

    let saved = save(&mut session, &directory);
    let reopened = WorkspaceSession::default()
        .execute(open_command(Path::new(saved.saved_path.as_ref().unwrap())))
        .unwrap();
    assert_eq!(reopened.document_json, saved.document_json);
}

#[test]
fn river_profile_rejections_are_atomic() {
    let directory = TestDirectory::new();
    let mut session = WorkspaceSession::default();
    let created = session.execute(create_command()).unwrap();
    let with_river_path = session
        .execute(create_path_command(
            &created,
            vec![[-24.0, -20.0], [24.0, -20.0]],
        ))
        .unwrap();
    let river_path_id = document(&with_river_path).project.modules[0].areas[0]
        .paths
        .paths[0]
        .id;
    let with_river = session
        .execute(upsert_river_command(
            &with_river_path,
            river_path_id,
            8.0,
            1.5,
            4.0,
        ))
        .unwrap();
    let with_road_path = session
        .execute(create_path_command(
            &with_river,
            vec![[-24.0, 20.0], [24.0, 20.0]],
        ))
        .unwrap();
    let road_path_id = document(&with_road_path).project.modules[0].areas[0]
        .paths
        .paths
        .iter()
        .find(|path| path.id != river_path_id)
        .unwrap()
        .id;
    let with_road = session
        .execute(upsert_road_command(
            &with_road_path,
            road_path_id,
            4.0,
            0.08,
        ))
        .unwrap();
    let saved = save(&mut session, &directory);
    let files_before = directory.files();
    let valid_request = area_request(&saved, 0, 0);
    let mut stale_request = valid_request;
    stale_request.base_revision -= 1;
    let mut wrong_project_request = valid_request;
    wrong_project_request.key.project_id =
        forge_core::ProjectId::deterministic("wrong-river-project");

    for command in [
        WorkspaceCommand::UpsertRiver {
            request: stale_request,
            river: authored_river(river_path_id, 9.0, 1.5, 4.0),
        },
        WorkspaceCommand::UpsertRiver {
            request: wrong_project_request,
            river: authored_river(river_path_id, 9.0, 1.5, 4.0),
        },
        WorkspaceCommand::UpsertRiver {
            request: valid_request,
            river: authored_river(PathId::deterministic("missing-river-path"), 8.0, 1.5, 4.0),
        },
        WorkspaceCommand::UpsertRiver {
            request: valid_request,
            river: authored_river(river_path_id, 1.0, 1.5, 4.0),
        },
        WorkspaceCommand::UpsertRiver {
            request: valid_request,
            river: authored_river(river_path_id, 8.0, 0.1, 4.0),
        },
        WorkspaceCommand::UpsertRiver {
            request: valid_request,
            river: authored_river(river_path_id, 8.0, 1.5, 0.5),
        },
        WorkspaceCommand::UpsertRiver {
            request: valid_request,
            river: authored_river(road_path_id, 8.0, 1.5, 4.0),
        },
        WorkspaceCommand::UpsertRoad {
            request: valid_request,
            road: authored_road(river_path_id, 4.0, 0.08),
        },
        upsert_river_command(&with_road, river_path_id, 8.0, 1.5, 4.0),
    ] {
        assert_rejected_without_state_change(&mut session, command);
        assert_eq!(directory.files(), files_before);
    }
}

#[test]
fn path_identity_and_default_name_are_deterministic_after_undo() {
    let mut session = WorkspaceSession::default();
    let created = session.execute(create_command()).unwrap();
    let first = session
        .execute(create_path_command(
            &created,
            vec![[-12.0, -4.0], [12.0, 4.0]],
        ))
        .unwrap();
    let first_path = document(&first).project.modules[0].areas[0].paths.paths[0].clone();
    let undone = session
        .execute(WorkspaceCommand::Undo {
            base_revision: first.revision,
        })
        .unwrap();
    let recreated = session
        .execute(create_path_command(&undone, vec![[-8.0, -8.0], [8.0, 8.0]]))
        .unwrap();
    let recreated_path = document(&recreated).project.modules[0].areas[0].paths.paths[0].clone();
    assert_eq!(recreated_path.id, first_path.id);
    assert_eq!(recreated_path.name, first_path.name);
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
    let mut area = module["areas"][0].clone();
    area.as_object_mut().unwrap().remove("vegetation");
    area.as_object_mut().unwrap().remove("vegetation_density");
    area.as_object_mut().unwrap().remove("paths");
    area.as_object_mut().unwrap().remove("roads");
    area.as_object_mut().unwrap().remove("rivers");
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
    assert_eq!(document(&fresh).schema_version, WORKSPACE_SCHEMA_VERSION);
}

#[test]
fn opening_v2_v3_v4_and_v5_adds_new_area_documents_without_rewriting_legacy_snapshots() {
    let source = document(
        &WorkspaceSession::default()
            .execute(create_command())
            .unwrap(),
    );
    for version in [2_u32, 3_u32, 4_u32, 5_u32] {
        let directory = TestDirectory::new();
        let mut legacy = serde_json::to_value(&source).unwrap();
        legacy["schema_version"] = json!(version);
        for area in legacy["project"]["modules"][0]["areas"]
            .as_array_mut()
            .unwrap()
        {
            area.as_object_mut().unwrap().remove("rivers");
            if version <= 4 {
                area.as_object_mut().unwrap().remove("roads");
            }
            if version <= 3 {
                area.as_object_mut().unwrap().remove("paths");
            }
            if version == 2 {
                area.as_object_mut().unwrap().remove("vegetation");
                area.as_object_mut().unwrap().remove("vegetation_density");
            }
        }
        let path = directory.write(
            &format!("{}-r1.forge.json", source.project.id),
            legacy.to_string(),
        );
        let bytes = fs::read(&path).unwrap();
        let mut session = WorkspaceSession::default();
        let opened = session.execute(open_command(&path)).unwrap();
        assert_eq!(document(&opened), source);
        assert!(!opened.dirty);
        save(&mut session, &directory);
        assert_eq!(fs::read(&path).unwrap(), bytes);
    }
}
